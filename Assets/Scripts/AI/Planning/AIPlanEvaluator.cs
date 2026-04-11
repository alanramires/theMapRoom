using System;
using System.Collections.Generic;
using UnityEngine;

// Avaliador stateless do planner da IA.
// Planos de setor sao selecionados como ativos a partir da intel publica do turno.
public static class AIPlanEvaluator
{
    public struct MissionAssignmentMemory
    {
        public int UnitInstanceId;
        public string PlanKey;
        public AIPlanRole Role;
        public int LastProgressTurn;
        public int LastDistanceToTarget;
    }

    public struct PlannerRuntimeConfig
    {
        public int StagnationTurns;
        public int MinimumRangeForDefensePlan;
        public int MaxVariablePlans;
        public int MaxBackupPlans;
        public AIStance CurrentStance;
        public int CurrentTurn;
    }

    public static List<AIPlanIntent> Evaluate(AISnapshot snapshot)
    {
        PlannerRuntimeConfig config = new PlannerRuntimeConfig
        {
            StagnationTurns = 2,
            MinimumRangeForDefensePlan = AIGeneralProfile.DefaultMinimumRangeForDefensePlan,
            MaxVariablePlans = 3,
            MaxBackupPlans = 0,
            CurrentStance = AIStance.Attack,
            CurrentTurn = 0
        };

        return Evaluate(snapshot, null, config, null);
    }

    public static List<AIPlanIntent> Evaluate(
        AISnapshot snapshot,
        IReadOnlyCollection<MissionAssignmentMemory> previousAssignments,
        PlannerRuntimeConfig config,
        List<string> plannerLogs)
    {
        var result = new List<AIPlanIntent>();
        if (snapshot == null)
            return result;

        // Impede que a mesma unidade entre em dois planos.
        var assignedUnits = new HashSet<int>();

        // Invasion stance: ativa plano de invasao com prioridade maxima antes dos setores.
        int sectorPlanSlots = config.MaxVariablePlans;
        if (config.CurrentStance == AIStance.Invasion)
        {
            if (TryActivateInvasionPlan(snapshot, assignedUnits, result, plannerLogs))
                sectorPlanSlots = Mathf.Max(0, sectorPlanSlots - 1);
        }

        // Planos de setor: seleciona os setores ativos do turno.
        SelectActiveSectorPlans(
            snapshot,
            sectorPlanSlots,
            assignedUnits,
            result,
            plannerLogs);

        SelectSectorHoldPlans(
            snapshot,
            previousAssignments,
            result,
            plannerLogs);

        // Planos backup: setores proprios ja conquistados mas atualmente contestados.
        SelectBackupSectorPlans(
            snapshot,
            Mathf.Max(0, config.MaxBackupPlans),
            assignedUnits,
            result,
            plannerLogs);

        EnsurePreviousActivePlansPersist(
            snapshot,
            previousAssignments,
            config,
            result,
            plannerLogs);

        ApplyMissionPersistenceAndReallocation(snapshot, result, previousAssignments, config, plannerLogs);
        ResolveTransportDemand(snapshot, result, plannerLogs);

        return result;
    }

    private static bool TryActivateInvasionPlan(
        AISnapshot snapshot,
        HashSet<int> assignedUnits,
        List<AIPlanIntent> result,
        List<string> plannerLogs)
    {
        // Coleta bases inimigas com construcoes capturaveis.
        ConstructionSector bestSector = default;
        int bestScore = int.MinValue;
        bool found = false;

        var seenBases = new System.Collections.Generic.HashSet<ConstructionSector>();
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable) continue;
            if (!ConstructionSectorHelper.IsBase(info.Sector)) continue;
            if (info.TeamId == snapshot.AiTeam) continue; // ignora propria base
            if (!seenBases.Add(info.Sector)) continue;

            // Score: prefere base com menos pontos de captura inimigos (mais fraca) e mais perto do HQ.
            int enemyCapture = CountEnemyCaptureInBase(info.Sector, snapshot);
            int distToHq = snapshot.HasHq
                ? (Mathf.Abs(info.Cell.x - snapshot.HqCell.x) + Mathf.Abs(info.Cell.y - snapshot.HqCell.y))
                : 0;
            int score = -enemyCapture - distToHq;

            if (!found || score > bestScore)
            {
                bestScore = score;
                bestSector = info.Sector;
                found = true;
            }
        }

        if (!found)
        {
            AddPlannerLog(plannerLogs, "invasao | nenhuma base inimiga encontrada");
            return false;
        }

        int uncaptured = CountUncapturedInSector(bestSector, snapshot);
        AIPlanIntent intent = BuildSectorPlanIntent(bestSector, snapshot);
        intent.DisplayName = $"Invasao {bestSector}";
        intent.BadgeSymbol = ">>";
        intent.SelectionReason = $"invasion | stance=Invasion | target={bestSector} | uncaptured={uncaptured} | score={bestScore}";

        // Aloca infantaria como plano de setor prioritario.
        PlannedForce force = new PlannedForce { Capture = 2, Escort = 1, FireSupport = 0, Logistics = 0 };
        var draft = new ActiveSectorDraft
        {
            Candidate = new SectorCandidate { Sector = bestSector, Uncaptured = uncaptured },
            Force = force,
            Intent = intent,
            PlanOrder = 0,
        };
        draft.CaptureTargets = CollectUncapturedTargetsInSector(bestSector, snapshot);
        draft.InfantryDemand = Mathf.Max(0, Mathf.Min(force.Capture, draft.CaptureTargets.Count));
        ApplyPlannedForceToIntent(intent, force, draft.InfantryDemand);

        var singleDraft = new System.Collections.Generic.List<ActiveSectorDraft> { draft };
        AssignSectorPlanInfantryAcrossActivePlans(singleDraft, snapshot, assignedUnits);
        AssignSectorPlanSupportForcesAcrossActivePlans(singleDraft, snapshot, assignedUnits);

        if (!HasAssignedRole(intent, AIPlanRole.Capture))
        {
            AddPlannerLog(plannerLogs, $"invasao | {bestSector} sem capturadores disponiveis - plano nao ativado");
            return false;
        }

        AddPlannerLog(plannerLogs, $"invasao | ativado | target={bestSector} uncaptured={uncaptured}");
        result.Add(intent);
        return true;
    }

    private static void EnsurePreviousActivePlansPersist(
        AISnapshot snapshot,
        IReadOnlyCollection<MissionAssignmentMemory> previousAssignments,
        PlannerRuntimeConfig config,
        List<AIPlanIntent> result,
        List<string> plannerLogs)
    {
        if (snapshot == null || previousAssignments == null || previousAssignments.Count == 0 || result == null)
            return;

        var existingPlanKeys = new HashSet<string>();
        for (int i = 0; i < result.Count; i++)
        {
            AIPlanIntent intent = result[i];
            if (intent == null)
                continue;
            existingPlanKeys.Add(BuildPlanKey(intent));
        }

        var activePlanByKey = BuildPlanByKey(result);
        var stagnatedPlans = ComputeStagnatedPlans(previousAssignments, activePlanByKey, config);
        var seenMissingKeys = new HashSet<string>();

        foreach (MissionAssignmentMemory memory in previousAssignments)
        {
            if (string.IsNullOrWhiteSpace(memory.PlanKey))
                continue;
            if (existingPlanKeys.Contains(memory.PlanKey))
                continue;
            if (!seenMissingKeys.Add(memory.PlanKey))
                continue;
            if (stagnatedPlans.Contains(memory.PlanKey))
            {
                AddPlannerLog(plannerLogs, $"persistencia-plano | ignorado {memory.PlanKey} por estagnacao");
                continue;
            }
            if (!TryParseDynamicCapturePlanKey(memory.PlanKey, out ConstructionSector sector))
                continue;
            if (IsSectorCompletedAndClear(snapshot, sector, config))
            {
                AddPlannerLog(plannerLogs, $"persistencia-plano | liberado {memory.PlanKey} setor-concluido-clear");
                continue;
            }

            AIPlanIntent intent = BuildSectorPlanIntent(sector, snapshot);
            if (intent == null || !intent.HasCaptureTarget)
                continue;

            List<AIConstructionInfo> captureTargets = CollectUncapturedTargetsInSector(sector, snapshot);
            int uncaptured = CountUncapturedInSector(sector, snapshot);
            SectorCandidate sectorInfo = BuildPersistedSectorCandidate(snapshot, sector, uncaptured);
            PlannedForce force = ComputePlannedForce(sectorInfo);
            int infantryDemand = Mathf.Max(0, Mathf.Min(force.Capture, captureTargets.Count));
            ApplyPlannedForceToIntent(intent, force, infantryDemand);
            intent.SelectionReason = $"persisted-active-plan | setor={sector} | reason=sticky-assignment";
            intent.DisplayName = $"Captura {sector} [CAP {force.Capture}, ESC {force.Escort}, ART {force.FireSupport}, TRN 0, SUP {force.Logistics}]";
            intent.TacticalRiskScore = ComputeSupportRiskScore(sectorInfo);

            result.Add(intent);
            existingPlanKeys.Add(memory.PlanKey);
            AddPlannerLog(plannerLogs, $"persistencia-plano | manteve {memory.PlanKey} ativo ate conclusao/falha");
        }
    }

    private static int CountEnemyCaptureInBase(ConstructionSector sector, AISnapshot snapshot)
    {
        int total = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector || !info.IsCapturable) continue;
            if (info.TeamId != snapshot.AiTeam)
                total += Mathf.Max(0, info.CapturePoints);
        }
        return total;
    }

    private static string BuildSectorSelectionReason(SectorCandidate sector, int rank)
    {
        List<string> tags = new List<string>();
        tags.Add($"rank={rank}");
        tags.Add($"near-hq={sector.DistToOwnHq}");
        tags.Add($"uncaptured={sector.Uncaptured}");

        if (sector.DistToOwnHq <= 4)
            tags.Add("safe-near-hq");
        else if (sector.DistToOwnHq >= 8)
            tags.Add("far-from-hq");

        if (sector.EnemyPressure > 0)
        {
            tags.Add($"relevance:pressure={sector.EnemyPressure}");
            tags.Add(sector.EnemyPressure >= 2 ? "hot-sector" : "light-pressure");
        }
        else
        {
            tags.Add("low-pressure");
        }

        if (sector.HasEnemyHq)
        {
            tags.Add("relevance:enemy-hq");
            tags.Add("near-enemy-hq");
        }
        else if (sector.DistToEnemyHq < int.MaxValue)
        {
            tags.Add($"enemy-hq-dist={sector.DistToEnemyHq}");
            if (sector.DistToEnemyHq <= 6)
                tags.Add("near-enemy-hq");
        }

        if (sector.EnemyHqNearbyCount > 0)
            tags.Add($"enemy-hq-count={sector.EnemyHqNearbyCount}");
        if (sector.EnemyHqThreatSum > 0)
            tags.Add($"enemy-hq-threat={sector.EnemyHqThreatSum}");

        int risk = ComputeSupportRiskScore(sector);
        tags.Add($"risk={risk}");
        if (risk >= 45)
            tags.Add("high-risk");
        else if (risk >= 25)
            tags.Add("mid-risk");
        else
            tags.Add("low-risk");

        return string.Join(" | ", tags);
    }

    private static void SelectActiveSectorPlans(
        AISnapshot snapshot,
        int maxPlans,
        HashSet<int> assignedUnits,
        List<AIPlanIntent> result,
        List<string> plannerLogs)
    {
        var candidateSectors = new List<SectorCandidate>();
        var seenSectors = new HashSet<ConstructionSector>();

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable) continue;
            if (!seenSectors.Add(info.Sector)) continue;

            int uncaptured = CountUncapturedInSector(info.Sector, snapshot);
            if (uncaptured == 0)
            {
                AddPlannerLog(
                    plannerLogs,
                    $"setor-selecao | setor={info.Sector} excluido=ja-conquistado constr=[{BuildSectorConstructionsDebug(info.Sector, snapshot)}]");
                continue;
            }

            bool hasEnemyHq = SectorHasEnemyHq(info.Sector, snapshot);
            int distToOwnHq = ComputeSectorDistanceToOwnHq(info.Sector, snapshot);
            int enemyPressure = EstimateEnemyPressure(info.Sector, snapshot);
            int distToEnemyHq = ComputeSectorDistanceToNearestEnemyHq(info.Sector, snapshot);
            int enemyHqNearbyCount = CountEnemyHqsWithinRange(info.Sector, snapshot, 8);
            int enemyHqThreatSum = ComputeEnemyHqThreatSum(info.Sector, snapshot, 12);

            candidateSectors.Add(new SectorCandidate
            {
                Sector = info.Sector,
                Uncaptured = uncaptured,
                HasEnemyHq = hasEnemyHq,
                DistToOwnHq = distToOwnHq,
                EnemyPressure = enemyPressure,
                DistToEnemyHq = distToEnemyHq,
                EnemyHqNearbyCount = enemyHqNearbyCount,
                EnemyHqThreatSum = enemyHqThreatSum,
            });

            AddPlannerLog(
                plannerLogs,
                $"setor-selecao | setor={info.Sector} incluido=sim uncaptured={uncaptured} distOwnHq={distToOwnHq} distEnemyHq={distToEnemyHq} enemyHqCount={enemyHqNearbyCount} enemyHqThreat={enemyHqThreatSum} pressure={enemyPressure} hasEnemyHq={hasEnemyHq} constr=[{BuildSectorConstructionsDebug(info.Sector, snapshot)}]");
        }

        // Priorizacao: mais proximo do HQ proprio > mais construcoes > pressao inimiga > HQ inimigo.
        // Regra de negocio: setores proximos da base devem entrar antes no pipeline de captura.
        candidateSectors.Sort((a, b) =>
        {
            if (a.DistToOwnHq != b.DistToOwnHq) return a.DistToOwnHq.CompareTo(b.DistToOwnHq);
            if (a.Uncaptured != b.Uncaptured) return b.Uncaptured.CompareTo(a.Uncaptured);
            if (a.EnemyPressure != b.EnemyPressure) return b.EnemyPressure.CompareTo(a.EnemyPressure);
            return b.HasEnemyHq.CompareTo(a.HasEnemyHq);
        });

        var activeSectorDrafts = new List<ActiveSectorDraft>();
        int generated = 0;
        for (int i = 0; i < candidateSectors.Count && generated < maxPlans; i++)
        {
            SectorCandidate sector = candidateSectors[i];
            PlannedForce force = ComputePlannedForce(sector);
            AIPlanIntent intent = BuildSectorPlanIntent(sector.Sector, snapshot);

            var draft = new ActiveSectorDraft
            {
                Candidate = sector,
                Force = force,
                Intent = intent,
                PlanOrder = generated,
            };

            draft.CaptureTargets = CollectUncapturedTargetsInSector(sector.Sector, snapshot);
            draft.InfantryDemand = Mathf.Max(0, Mathf.Min(force.Capture, draft.CaptureTargets.Count));
            ApplyPlannedForceToIntent(intent, force, draft.InfantryDemand);

            // Ajusta nome para facilitar leitura no log/debug sem colapsar logistica em escolta.
            intent.DisplayName = $"Captura {sector.Sector} [CAP {force.Capture}, ESC {force.Escort}, ART {force.FireSupport}, TRN 0, SUP {force.Logistics}]";
            intent.TacticalRiskScore = ComputeSupportRiskScore(sector);
            intent.SelectionReason = BuildSectorSelectionReason(sector, generated + 1);

            AddPlannerLog(
                plannerLogs,
                $"setor-ativo | setor={sector.Sector} rank={generated + 1} score={intent.TacticalRiskScore} force=CAP{force.Capture}/ESC{force.Escort}/FS{force.FireSupport}/TRN0/LOG{force.Logistics} targets={draft.CaptureTargets.Count}");

            activeSectorDrafts.Add(draft);
            generated++;
        }

        AssignSectorPlanInfantryAcrossActivePlans(activeSectorDrafts, snapshot, assignedUnits);

        List<ActiveSectorDraft> supportPriority = new List<ActiveSectorDraft>(activeSectorDrafts);
        supportPriority.Sort((a, b) => ComputeSupportRiskScore(b.Candidate).CompareTo(ComputeSupportRiskScore(a.Candidate)));

        AssignSectorPlanSupportForcesAcrossActivePlans(supportPriority, snapshot, assignedUnits);

        for (int i = 0; i < supportPriority.Count; i++)
        {
            ActiveSectorDraft draft = supportPriority[i];
            if (!HasAssignedRole(draft.Intent, AIPlanRole.Capture))
                continue;

            result.Add(draft.Intent);
        }
    }

    private static void SelectSectorHoldPlans(
        AISnapshot snapshot,
        IReadOnlyCollection<MissionAssignmentMemory> previousAssignments,
        List<AIPlanIntent> result,
        List<string> plannerLogs)
    {
        if (snapshot == null || previousAssignments == null || previousAssignments.Count == 0)
            return;

        var activeKeys = new HashSet<string>();
        for (int i = 0; i < result.Count; i++)
        {
            AIPlanIntent existing = result[i];
            if (existing == null)
                continue;
            activeKeys.Add(BuildPlanKey(existing));
        }

        var addedSectors = new HashSet<ConstructionSector>();
        foreach (MissionAssignmentMemory memory in previousAssignments)
        {
            if (string.IsNullOrWhiteSpace(memory.PlanKey))
                continue;
            if (!TryGetDynamicSectorFromPlanKey(memory.PlanKey, out ConstructionSector sector))
                continue;
            if (activeKeys.Contains(memory.PlanKey) || addedSectors.Contains(sector))
                continue;
            if (!ShouldKeepSectorHoldPlan(snapshot, sector, out int nearbyThreats, out Vector3Int representativeCell, out string representativeLabel))
                continue;

            AIPlanIntent intent = BuildSectorPlanIntent(sector, snapshot);
            if (intent == null)
                continue;

            intent.DisplayName = $"Captura {sector} [hold]";
            intent.HasCaptureTarget = true;
            intent.CaptureTargetCell = representativeCell;
            intent.CaptureTargetLabel = !string.IsNullOrWhiteSpace(representativeLabel) ? representativeLabel : sector.ToString();
            intent.DesiredCaptureCount = 0;
            intent.DesiredEscortCount = 0;
            intent.DesiredArtilleryCount = 0;
            intent.DesiredSupportCount = 0;
            intent.TacticalRiskScore = nearbyThreats * 20;
            intent.SelectionReason = $"sector-hold | conquered-not-clear | threats={nearbyThreats}";
            intent.SectorClear = false;

            result.Add(intent);
            activeKeys.Add(memory.PlanKey);
            addedSectors.Add(sector);
            AddPlannerLog(plannerLogs, $"setor-hold | setor={sector} representante={intent.CaptureTargetLabel} threats={nearbyThreats}");
        }
    }

    private static bool TryGetDynamicSectorFromPlanKey(string planKey, out ConstructionSector sector)
    {
        sector = default;
        const string prefix = "dynamic:capture:";
        if (string.IsNullOrWhiteSpace(planKey) || !planKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string sectorName = planKey.Substring(prefix.Length);
        return !string.IsNullOrWhiteSpace(sectorName)
            && Enum.TryParse(sectorName, true, out sector);
    }

    private static bool ShouldKeepSectorHoldPlan(
        AISnapshot snapshot,
        ConstructionSector sector,
        out int nearbyThreats,
        out Vector3Int representativeCell,
        out string representativeLabel)
    {
        nearbyThreats = 0;
        representativeCell = default;
        representativeLabel = string.Empty;

        if (snapshot == null)
            return false;

        SectorManager.SectorInfo sectorInfo;
        bool hasInfo = SectorManager.TryGetSectorInfo(sector, out sectorInfo) || SectorManager.TryGetBaseInfo(sector, out sectorInfo);
        if (!hasInfo || sectorInfo == null)
            return false;

        if (!sectorInfo.IsFullyControlled || sectorInfo.ControllingTeam != snapshot.AiTeam)
            return false;

        if (sectorInfo.IsDisputed || sectorInfo.HasPartialCapture)
            return false;

        representativeCell = sectorInfo.RepresentativeCell;
        representativeCell.z = 0;
        representativeLabel = sectorInfo.RepresentativeLabel ?? string.Empty;

        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;
            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (GetHexDistance(representativeCell, enemyCell) <= 2)
                nearbyThreats++;
        }

        return nearbyThreats > 0;
    }
    private static void SelectBackupSectorPlans(
        AISnapshot snapshot,
        int maxBackupPlans,
        HashSet<int> assignedUnits,
        List<AIPlanIntent> result,
        List<string> plannerLogs)
    {
        if (snapshot == null || maxBackupPlans <= 0)
            return;

        var candidateSectors = new List<SectorCandidate>();
        var seenSectors = new HashSet<ConstructionSector>();
        var activeKeys = new HashSet<string>();
        for (int i = 0; i < result.Count; i++)
        {
            AIPlanIntent existing = result[i];
            if (existing == null)
                continue;
            activeKeys.Add(BuildPlanKey(existing));
        }

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable) continue;
            if (!seenSectors.Add(info.Sector)) continue;
            if (!IsOwnedSectorContested(info.Sector, snapshot)) continue;

            AIPlanIntent probe = BuildSectorPlanIntent(info.Sector, snapshot);
            if (probe == null || activeKeys.Contains(BuildPlanKey(probe)))
                continue;

            bool hasEnemyHq = SectorHasEnemyHq(info.Sector, snapshot);
            int distToOwnHq = ComputeSectorDistanceToOwnHq(info.Sector, snapshot);
            int enemyPressure = EstimateEnemyPressure(info.Sector, snapshot);
            int distToEnemyHq = ComputeSectorDistanceToNearestEnemyHq(info.Sector, snapshot);
            int enemyHqNearbyCount = CountEnemyHqsWithinRange(info.Sector, snapshot, 8);
            int enemyHqThreatSum = ComputeEnemyHqThreatSum(info.Sector, snapshot, 12);

            candidateSectors.Add(new SectorCandidate
            {
                Sector = info.Sector,
                Uncaptured = CountOwnedContestedTargetsInSector(info.Sector, snapshot),
                HasEnemyHq = hasEnemyHq,
                DistToOwnHq = distToOwnHq,
                EnemyPressure = enemyPressure,
                DistToEnemyHq = distToEnemyHq,
                EnemyHqNearbyCount = enemyHqNearbyCount,
                EnemyHqThreatSum = enemyHqThreatSum,
            });
        }

        candidateSectors.Sort((a, b) =>
        {
            if (a.EnemyPressure != b.EnemyPressure) return b.EnemyPressure.CompareTo(a.EnemyPressure);
            if (a.Uncaptured != b.Uncaptured) return b.Uncaptured.CompareTo(a.Uncaptured);
            return a.DistToOwnHq.CompareTo(b.DistToOwnHq);
        });

        int generated = 0;
        for (int i = 0; i < candidateSectors.Count && generated < maxBackupPlans; i++)
        {
            SectorCandidate sector = candidateSectors[i];
            AIPlanIntent intent = BuildSectorPlanIntent(sector.Sector, snapshot);
            if (intent == null)
                continue;

            PlannedForce force = new PlannedForce { Capture = 1, Escort = sector.EnemyPressure > 0 ? 1 : 0, FireSupport = 0, Logistics = 0 };
            var draft = new ActiveSectorDraft
            {
                Candidate = sector,
                Force = force,
                Intent = intent,
                PlanOrder = generated,
                CaptureTargets = CollectOwnedContestedTargetsInSector(sector.Sector, snapshot),
            };
            draft.InfantryDemand = Mathf.Max(1, Mathf.Min(force.Capture, draft.CaptureTargets.Count));
            ApplyPlannedForceToIntent(intent, force, draft.InfantryDemand);
            intent.DisplayName = $"Retomada {sector.Sector} [backup]";
            intent.TacticalRiskScore = ComputeSupportRiskScore(sector) + 25;
            intent.SelectionReason = $"backup-retake | contested-owned | pressure={sector.EnemyPressure} | uncaptured={sector.Uncaptured}";

            var singleDraft = new List<ActiveSectorDraft> { draft };
            AssignSectorPlanInfantryAcrossActivePlans(singleDraft, snapshot, assignedUnits);
            AssignSectorPlanSupportForcesAcrossActivePlans(singleDraft, snapshot, assignedUnits);

            if (!HasAssignedRole(intent, AIPlanRole.Capture))
            {
                AddPlannerLog(plannerLogs, $"setor-backup | setor={sector.Sector} sem capturador disponivel");
                continue;
            }

            AddPlannerLog(plannerLogs, $"setor-backup | setor={sector.Sector} rank={generated + 1} force=CAP{force.Capture}/ESC{force.Escort}");
            result.Add(intent);
            generated++;
        }
    }

    private static bool IsOwnedSectorContested(ConstructionSector sector, AISnapshot snapshot)
    {
        return CountOwnedContestedTargetsInSector(sector, snapshot) > 0;
    }

    private static int CountOwnedContestedTargetsInSector(ConstructionSector sector, AISnapshot snapshot)
    {
        int count = 0;
        if (snapshot == null || snapshot.KnownConstructions == null)
            return 0;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable || info.Sector != sector)
                continue;
            if (info.TeamId != snapshot.AiTeam)
                continue;
            if (info.CapturePoints >= info.CapturePointsMax)
                continue;
            count++;
        }

        return count;
    }

    private static List<AIConstructionInfo> CollectOwnedContestedTargetsInSector(ConstructionSector sector, AISnapshot snapshot)
    {
        var targets = new List<AIConstructionInfo>();
        if (snapshot == null || snapshot.KnownConstructions == null)
            return targets;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable || info.Sector != sector)
                continue;
            if (info.TeamId != snapshot.AiTeam)
                continue;
            if (info.CapturePoints >= info.CapturePointsMax)
                continue;
            targets.Add(info);
        }

        return targets;
    }
    private struct SectorCandidate
    {
        public ConstructionSector Sector;
        public int Uncaptured;
        public bool HasEnemyHq;
        public int DistToOwnHq;
        public int EnemyPressure;
        public int DistToEnemyHq;
        public int EnemyHqNearbyCount;
        public int EnemyHqThreatSum;
    }

    private struct PlannedForce
    {
        public int Capture;
        public int Escort;
        public int FireSupport;
        public int Logistics;
    }
    private sealed class ActiveSectorDraft
    {
        public SectorCandidate Candidate;
        public PlannedForce Force;
        public AIPlanIntent Intent;
        public List<AIConstructionInfo> CaptureTargets = new List<AIConstructionInfo>();
        public int InfantryDemand;
        public int PlanOrder;
    }
    private sealed class CaptureSlot
    {
        public ActiveSectorDraft Draft;
        public Vector3Int Cell;
        public string Label;
        public int PriorityPenalty;
    }

    private static string BuildSectorConstructionsDebug(ConstructionSector sector, AISnapshot snapshot)
    {
        if (snapshot == null || snapshot.KnownConstructions == null || snapshot.KnownConstructions.Count == 0)
            return string.Empty;
        var chunks = new List<string>();
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector || !info.IsCapturable)
                continue;
            string name = !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : sector.ToString();
            chunks.Add($"{name}(team={info.TeamId},cp={info.CapturePoints}/{info.CapturePointsMax})");
        }
        return chunks.Count > 0 ? string.Join(" | ", chunks) : "sem-construcoes-capturaveis";
    }

    private static PlannedForce ComputePlannedForce(SectorCandidate sector)
    {
        bool distant = sector.DistToOwnHq >= 8;
        bool enemyHqProximity = sector.HasEnemyHq || sector.DistToEnemyHq <= 6;
        bool multiEnemyHqPressure = sector.EnemyHqNearbyCount >= 2 || sector.EnemyHqThreatSum >= 10;
        int capture = Mathf.Max(1, sector.Uncaptured);
        if (distant) capture += 1;
        if (sector.EnemyPressure >= 2) capture += 1 + (sector.EnemyPressure - 2);
        if (enemyHqProximity) capture += 1;
        if (multiEnemyHqPressure) capture += 1;
        int escort = 0;
        if (sector.Uncaptured >= 2 || distant || sector.EnemyPressure > 0) escort += 1;
        escort += sector.Uncaptured / 3;
        escort += sector.EnemyPressure / 2;
        if (enemyHqProximity) escort += 1;
        if (multiEnemyHqPressure) escort += 1;
        int fireSupport = 0;
        if (sector.Uncaptured >= 4 || sector.EnemyPressure >= 2 || enemyHqProximity) fireSupport += 1;
        fireSupport += sector.Uncaptured / 4;
        fireSupport += sector.EnemyPressure / 2;
        if (multiEnemyHqPressure) fireSupport += 1;
        int logistics = 0;
        if (capture >= 3 && (distant || sector.Uncaptured >= 4)) logistics += 1;
        if (sector.EnemyPressure >= 3) logistics += 1;
        if (multiEnemyHqPressure) logistics += 1;
        return new PlannedForce
        {
            Capture = Mathf.Max(1, capture),
            Escort = Mathf.Max(0, escort),
            FireSupport = Mathf.Max(0, fireSupport),
            Logistics = Mathf.Max(0, logistics),
        };
    }

    private static int CountUncapturedInSector(ConstructionSector sector, AISnapshot snapshot)
    {
        int uncaptured = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo c = snapshot.KnownConstructions[i];
            if (c == null || c.Sector != sector || !c.IsCapturable) continue;

            bool ownedByAi = c.TeamId == snapshot.AiTeam;
            if (!ownedByAi)
                uncaptured++;
        }
        return uncaptured;
    }

    private static bool SectorHasEnemyHq(ConstructionSector sector, AISnapshot snapshot)
    {
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo c = snapshot.KnownConstructions[i];
            if (c == null || c.Sector != sector || !c.IsCapturable) continue;
            if (c.IsHq && c.TeamId != snapshot.AiTeam)
                return true;
        }
        return false;
    }

    private static int ComputeSectorDistanceToOwnHq(ConstructionSector sector, AISnapshot snapshot)
    {
        if (!snapshot.HasHq)
            return int.MaxValue;

        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        return Mathf.RoundToInt((new Vector2(snapshot.HqCell.x, snapshot.HqCell.y) - centroid).magnitude);
    }

    private static int ComputeSectorDistanceToNearestEnemyHq(ConstructionSector sector, AISnapshot snapshot)
    {
        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        int best = int.MaxValue;

        for (int i = 0; i < snapshot.EnemyHqs.Count; i++)
        {
            AIConstructionInfo hq = snapshot.EnemyHqs[i];
            int dist = Mathf.RoundToInt((new Vector2(hq.Cell.x, hq.Cell.y) - centroid).magnitude);
            if (dist < best)
                best = dist;
        }

        return best;
    }


    private static int CountEnemyHqsWithinRange(ConstructionSector sector, AISnapshot snapshot, int maxDistance)
    {
        if (snapshot == null || snapshot.EnemyHqs == null || snapshot.EnemyHqs.Count == 0)
            return 0;

        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        int count = 0;
        for (int i = 0; i < snapshot.EnemyHqs.Count; i++)
        {
            AIConstructionInfo hq = snapshot.EnemyHqs[i];
            if (hq == null)
                continue;

            int dist = Mathf.RoundToInt((new Vector2(hq.Cell.x, hq.Cell.y) - centroid).magnitude);
            if (dist <= maxDistance)
                count++;
        }

        return count;
    }

    private static int ComputeEnemyHqThreatSum(ConstructionSector sector, AISnapshot snapshot, int influenceRadius)
    {
        if (snapshot == null || snapshot.EnemyHqs == null || snapshot.EnemyHqs.Count == 0)
            return 0;

        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        int threat = 0;
        for (int i = 0; i < snapshot.EnemyHqs.Count; i++)
        {
            AIConstructionInfo hq = snapshot.EnemyHqs[i];
            if (hq == null)
                continue;

            int dist = Mathf.RoundToInt((new Vector2(hq.Cell.x, hq.Cell.y) - centroid).magnitude);
            threat += Mathf.Max(0, influenceRadius - dist);
        }

        return threat;
    }

    private static int EstimateEnemyPressure(ConstructionSector sector, AISnapshot snapshot)
    {
        if (snapshot.VisibleEnemies.Count == 0)
            return 0;

        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        int pressure = 0;

        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead) continue;

            Vector3Int ec = enemy.CurrentCellPosition;
            float sqrDist = (new Vector2(ec.x, ec.y) - centroid).sqrMagnitude;

            // Dentro de ~4 tiles do centroide conta como pressao local.
            if (sqrDist <= 16f)
                pressure++;
        }

        return pressure;
    }

    private static AIPlanIntent BuildSectorPlanIntent(ConstructionSector sector, AISnapshot snapshot)
    {
        string sectorName = sector.ToString();
        string badge = !string.IsNullOrWhiteSpace(sectorName) ? sectorName.Substring(0, 1).ToUpperInvariant() : string.Empty;

        var intent = new AIPlanIntent
        {
            Sector = sector,
            DisplayName = $"Captura {sector}",
            BadgeSymbol = badge,
        };

        FillCaptureTarget(intent, sector, snapshot);
        FillSectorEnemy(intent, sector, snapshot);
        return intent;
    }

    private static void ApplyPlannedForceToIntent(AIPlanIntent intent, PlannedForce force, int resolvedCaptureDemand)
    {
        if (intent == null)
            return;

        intent.DesiredCaptureCount = Mathf.Max(0, resolvedCaptureDemand);
        intent.DesiredEscortCount = Mathf.Max(0, force.Escort);
        intent.DesiredArtilleryCount = Mathf.Max(0, force.FireSupport);
        intent.DesiredTransportCount = 0;
        intent.DesiredSupportCount = Mathf.Max(0, force.Logistics);
    }

    private static void ResolveTransportDemand(
        AISnapshot snapshot,
        List<AIPlanIntent> plans,
        List<string> plannerLogs)
    {
        if (snapshot == null || plans == null || plans.Count == 0)
            return;

        var unitById = BuildFriendlyUnitById(snapshot);
        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent intent = plans[i];
            if (intent == null)
                continue;

            intent.DesiredTransportCount = ComputeDesiredTransportCount(snapshot, intent, unitById);
            RefreshIntentDisplayTransportDemand(intent);
            if (intent.DesiredTransportCount > 0)
            {
                AddPlannerLog(
                    plannerLogs,
                    $"transporte-demanda | plano={BuildPlanKey(intent)} setor={intent.Sector} desired={intent.DesiredTransportCount}");
            }
        }
    }

    private static int ComputeDesiredTransportCount(
        AISnapshot snapshot,
        AIPlanIntent intent,
        Dictionary<int, UnitManager> unitById)
    {
        if (snapshot == null || intent == null || unitById == null || intent.Assignments == null || intent.Assignments.Count == 0)
            return 0;

        int transportWorthyCapturers = 0;
        for (int i = 0; i < intent.Assignments.Count; i++)
        {
            AIPlanAssignment assignment = intent.Assignments[i];
            if (assignment == null || assignment.Role != AIPlanRole.Capture)
                continue;
            if (!unitById.TryGetValue(assignment.UnitInstanceId, out UnitManager unit) || unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            if (data.domain != Domain.Land || !data.aiUnitProfile.HasPlanCapability(AIPlanCapability.Capture, data))
                continue;

            Vector3Int targetCell = assignment.HasPlannedCaptureTarget
                ? assignment.PlannedCaptureCell
                : (intent.HasCaptureTarget ? intent.CaptureTargetCell : unit.CurrentCellPosition);
            targetCell.z = 0;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            int distanceToTarget = GetHexDistance(unitCell, targetCell);
            int move = Mathf.Max(1, unit.GetMovementRange());
            int worthwhileThreshold = Mathf.CeilToInt(move * 1.5f);
            if (distanceToTarget < 8 || distanceToTarget <= move || distanceToTarget <= worthwhileThreshold)
                continue;

            transportWorthyCapturers++;
        }

        return Mathf.CeilToInt(transportWorthyCapturers / 2f);
    }

    private static void RefreshIntentDisplayTransportDemand(AIPlanIntent intent)
    {
        if (intent == null || string.IsNullOrWhiteSpace(intent.DisplayName))
            return;

        const string token = "TRN 0";
        if (intent.DisplayName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
            return;

        intent.DisplayName = intent.DisplayName.Replace(token, $"TRN {Mathf.Max(0, intent.DesiredTransportCount)}");
    }

    private static Dictionary<int, UnitManager> BuildFriendlyUnitById(AISnapshot snapshot)
    {
        var map = new Dictionary<int, UnitManager>();
        if (snapshot == null || snapshot.FriendlyUnits == null)
            return map;

        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager unit = snapshot.FriendlyUnits[i];
            if (unit == null || unit.IsDead)
                continue;
            map[unit.InstanceId] = unit;
        }

        return map;
    }

    private static void FillCaptureTarget(AIPlanIntent intent, ConstructionSector sector, AISnapshot snapshot)
    {
        AIConstructionInfo bestCapture = null;
        int bestCategory = int.MaxValue;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector || !info.IsCapturable) continue;

            int category;
            if (info.TeamId == snapshot.AiTeam)
            {
                if (info.CapturePoints >= info.CapturePointsMax) continue;
                category = 2;
            }
            else if (info.IsHq)
            {
                category = 0;
            }
            else
            {
                category = 1;
            }

            if (category < bestCategory)
            {
                bestCategory = category;
                bestCapture = info;
            }
        }

        if (bestCapture == null) return;

        intent.HasCaptureTarget = true;
        intent.CaptureTargetCell = bestCapture.Cell;
        intent.CaptureTargetCell.z = 0;
        intent.CaptureTargetLabel = !string.IsNullOrWhiteSpace(bestCapture.DisplayName)
            ? bestCapture.DisplayName
            : sector.ToString();
    }

    private static void FillSectorEnemy(AIPlanIntent intent, ConstructionSector sector, AISnapshot snapshot)
    {
        if (snapshot.VisibleEnemies.Count == 0 || ConstructionSectorHelper.IsBase(sector))
            return;

        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        float bestDist = float.MaxValue;

        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead) continue;

            Vector3Int ec = enemy.CurrentCellPosition;
            float dist = (new Vector2(ec.x, ec.y) - centroid).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                intent.SectorEnemy = enemy;
            }
        }
    }

    private static Vector2 ComputeSectorCentroid(ConstructionSector sector, AISnapshot snapshot)
    {
        Vector2 sum = Vector2.zero;
        int count = 0;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector) continue;
            sum += new Vector2(info.Cell.x, info.Cell.y);
            count++;
        }

        return count > 0 ? sum / count : Vector2.zero;
    }

    private static List<AIConstructionInfo> CollectUncapturedTargetsInSector(ConstructionSector sector, AISnapshot snapshot)
    {
        var targets = new List<AIConstructionInfo>();
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector || !info.IsCapturable)
                continue;

            bool aiOwnsFully = info.TeamId == snapshot.AiTeam && info.CapturePoints >= info.CapturePointsMax;
            if (aiOwnsFully)
                continue;

            targets.Add(info);
        }

        Vector3Int hq = snapshot.HqCell;
        targets.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.Cell.x - hq.x) + Mathf.Abs(a.Cell.y - hq.y);
            int db = Mathf.Abs(b.Cell.x - hq.x) + Mathf.Abs(b.Cell.y - hq.y);
            return da.CompareTo(db);
        });

        return targets;
    }

    private static void AssignSectorPlanInfantryAcrossActivePlans(
        List<ActiveSectorDraft> drafts,
        AISnapshot snapshot,
        HashSet<int> assignedUnits)
    {
        if (drafts == null || drafts.Count == 0)
            return;

        var infantryUnits = new List<UnitManager>();
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager unit = snapshot.FriendlyUnits[i];
            if (unit == null || unit.IsDead)
                continue;
            if (assignedUnits.Contains(unit.InstanceId))
                continue;

            unit.TryGetUnitData(out UnitData data);
            if (!CanUnitPerformRole(data, AIPlanRole.Capture))
                continue;
            infantryUnits.Add(unit);
        }

        var slots = new List<CaptureSlot>();
        for (int i = 0; i < drafts.Count; i++)
        {
            ActiveSectorDraft draft = drafts[i];
            if (draft.InfantryDemand <= 0 || draft.CaptureTargets == null || draft.CaptureTargets.Count == 0)
                continue;

            int demand = Mathf.Min(draft.InfantryDemand, draft.CaptureTargets.Count);
            for (int t = 0; t < demand; t++)
            {
                AIConstructionInfo target = draft.CaptureTargets[t];
                Vector3Int cell = target.Cell;
                cell.z = 0;
                slots.Add(new CaptureSlot
                {
                    Draft = draft,
                    Cell = cell,
                    Label = !string.IsNullOrWhiteSpace(target.DisplayName) ? target.DisplayName : draft.Candidate.Sector.ToString(),
                    PriorityPenalty = draft.PlanOrder == 0 ? -1000 : 0,
                });
            }
        }

        if (infantryUnits.Count == 0 || slots.Count == 0)
            return;

        int unitCount = infantryUnits.Count;
        int slotCount = slots.Count;
        int source = 0;
        int unitOffset = 1;
        int slotOffset = unitOffset + unitCount;
        int sink = slotOffset + slotCount;
        int nodeCount = sink + 1;

        var graph = new List<List<FlowEdge>>(nodeCount);
        for (int i = 0; i < nodeCount; i++)
            graph.Add(new List<FlowEdge>());

        var unitToSlotEdges = new FlowEdge[unitCount, slotCount];

        for (int u = 0; u < unitCount; u++)
            AddFlowEdge(graph, source, unitOffset + u, 1, 0);

        for (int s = 0; s < slotCount; s++)
            AddFlowEdge(graph, slotOffset + s, sink, 1, 0);

        for (int u = 0; u < unitCount; u++)
        {
            Vector3Int uc = infantryUnits[u].CurrentCellPosition;
            uc.z = 0;
            for (int s = 0; s < slotCount; s++)
            {
                Vector3Int sc = slots[s].Cell;
                int dist = Mathf.Abs(uc.x - sc.x) + Mathf.Abs(uc.y - sc.y);
                int cost = dist * 10 + slots[s].PriorityPenalty;
                unitToSlotEdges[u, s] = AddFlowEdge(graph, unitOffset + u, slotOffset + s, 1, cost);
            }
        }

        int targetFlow = Mathf.Min(unitCount, slotCount);
        MinCostMaxFlow(graph, source, sink, targetFlow);

        for (int u = 0; u < unitCount; u++)
        {
            for (int s = 0; s < slotCount; s++)
            {
                FlowEdge edge = unitToSlotEdges[u, s];
                if (edge == null || edge.Capacity != 0)
                    continue;

                UnitManager unit = infantryUnits[u];
                CaptureSlot slot = slots[s];
                assignedUnits.Add(unit.InstanceId);
                slot.Draft.Intent.Assignments.Add(new AIPlanAssignment
                {
                    UnitInstanceId = unit.InstanceId,
                    Role = AIPlanRole.Capture,
                    Intent = slot.Draft.Intent,
                    HasPlannedCaptureTarget = true,
                    PlannedCaptureCell = slot.Cell,
                    PlannedCaptureLabel = slot.Label,
                });
                break;
            }
        }
    }

    private static void AssignSectorPlanSupportForcesAcrossActivePlans(
        List<ActiveSectorDraft> drafts,
        AISnapshot snapshot,
        HashSet<int> assignedUnits)
    {
        if (drafts == null || drafts.Count == 0 || snapshot == null)
            return;
        AssignSupportRoleAcrossDrafts(
            drafts,
            snapshot,
            assignedUnits,
            AIPlanRole.Artillery,
            d => d.Force.FireSupport);
        AssignSupportRoleAcrossDrafts(
            drafts,
            snapshot,
            assignedUnits,
            AIPlanRole.Support,
            d => d.Force.Logistics);
        AssignSupportRoleAcrossDrafts(
            drafts,
            snapshot,
            assignedUnits,
            AIPlanRole.Escort,
            d => d.Force.Escort);
    }
    private static void AssignSupportRoleAcrossDrafts(
        List<ActiveSectorDraft> drafts,
        AISnapshot snapshot,
        HashSet<int> assignedUnits,
        AIPlanRole role,
        Func<ActiveSectorDraft, int> getWantedCount)
    {
        if (drafts == null || drafts.Count == 0 || getWantedCount == null)
            return;
        bool assignedInWave = true;
        while (assignedInWave)
        {
            assignedInWave = false;
            for (int i = 0; i < drafts.Count; i++)
            {
                ActiveSectorDraft draft = drafts[i];
                if (draft == null || draft.Intent == null)
                    continue;
                if (!HasAssignedRole(draft.Intent, AIPlanRole.Capture))
                    continue;
                int wanted = Mathf.Max(0, getWantedCount(draft));
                if (wanted <= 0)
                    continue;
                int current = CountAssignments(draft.Intent, role);
                if (current >= wanted)
                    continue;
                Vector3Int targetCell = draft.Intent.HasCaptureTarget ? draft.Intent.CaptureTargetCell : snapshot.HqCell;
                int assignedNow = AssignClosestUnitsForRole(draft.Intent, snapshot, assignedUnits, targetCell, 1, role);
                if (assignedNow > 0)
                    assignedInWave = true;
            }
        }
    }

    private sealed class FlowEdge
    {
        public int To;
        public int ReverseIndex;
        public int Capacity;
        public int Cost;
    }

    private static FlowEdge AddFlowEdge(List<List<FlowEdge>> graph, int from, int to, int capacity, int cost)
    {
        var fwd = new FlowEdge { To = to, ReverseIndex = graph[to].Count, Capacity = capacity, Cost = cost };
        var rev = new FlowEdge { To = from, ReverseIndex = graph[from].Count, Capacity = 0, Cost = -cost };
        graph[from].Add(fwd);
        graph[to].Add(rev);
        return fwd;
    }

    private static void MinCostMaxFlow(List<List<FlowEdge>> graph, int source, int sink, int targetFlow)
    {
        int n = graph.Count;
        int flow = 0;

        var dist = new int[n];
        var inQueue = new bool[n];
        var prevNode = new int[n];
        var prevEdge = new int[n];

        while (flow < targetFlow)
        {
            for (int i = 0; i < n; i++)
            {
                dist[i] = int.MaxValue;
                inQueue[i] = false;
                prevNode[i] = -1;
                prevEdge[i] = -1;
            }

            dist[source] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(source);
            inQueue[source] = true;

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                inQueue[v] = false;

                List<FlowEdge> edges = graph[v];
                for (int ei = 0; ei < edges.Count; ei++)
                {
                    FlowEdge e = edges[ei];
                    if (e.Capacity <= 0 || dist[v] == int.MaxValue)
                        continue;

                    int nd = dist[v] + e.Cost;
                    if (nd >= dist[e.To])
                        continue;

                    dist[e.To] = nd;
                    prevNode[e.To] = v;
                    prevEdge[e.To] = ei;

                    if (!inQueue[e.To])
                    {
                        inQueue[e.To] = true;
                        queue.Enqueue(e.To);
                    }
                }
            }

            if (dist[sink] == int.MaxValue)
                break;

            int add = targetFlow - flow;
            int cur = sink;
            while (cur != source)
            {
                int pv = prevNode[cur];
                int pe = prevEdge[cur];
                if (pv < 0 || pe < 0)
                {
                    add = 0;
                    break;
                }

                FlowEdge edge = graph[pv][pe];
                if (edge.Capacity < add)
                    add = edge.Capacity;

                cur = pv;
            }

            if (add <= 0)
                break;

            cur = sink;
            while (cur != source)
            {
                int pv = prevNode[cur];
                int pe = prevEdge[cur];
                FlowEdge edge = graph[pv][pe];
                edge.Capacity -= add;
                graph[cur][edge.ReverseIndex].Capacity += add;
                cur = pv;
            }

            flow += add;
        }
    }
    private static int ComputeSupportRiskScore(SectorCandidate sector)
    {
        // Risco tatico para escoltas:
        // - mais longe do HQ proprio = mais risco logistico;
        // - mais perto de HQ inimigo = maior risco de contato;
        // - pressao inimiga/HQ inimigo no setor aumentam prioridade.
        int score = 0;
        score += Mathf.Max(0, sector.DistToOwnHq) * 3;
        score += Mathf.Max(0, 12 - sector.DistToEnemyHq) * 4;
        score += Mathf.Max(0, sector.EnemyPressure) * 8;
        score += Mathf.Max(0, sector.EnemyHqThreatSum) * 2;
        if (sector.EnemyHqNearbyCount > 1)
            score += (sector.EnemyHqNearbyCount - 1) * 10;
        if (sector.HasEnemyHq) score += 12;
        return score;
    }

    private static bool HasAssignedRole(AIPlanIntent intent, AIPlanRole role)
    {
        if (intent == null || intent.Assignments == null || intent.Assignments.Count == 0)
            return false;

        for (int i = 0; i < intent.Assignments.Count; i++)
        {
            AIPlanAssignment assignment = intent.Assignments[i];
            if (assignment != null && assignment.Role == role)
                return true;
        }

        return false;
    }

    private static bool TryParseDynamicCapturePlanKey(string planKey, out ConstructionSector sector)
    {
        sector = default;
        if (string.IsNullOrWhiteSpace(planKey))
            return false;

        const string prefix = "dynamic:capture:";
        if (!planKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string sectorName = planKey.Substring(prefix.Length);
        return Enum.TryParse(sectorName, true, out sector);
    }

    private static bool IsSectorCompletedAndClear(AISnapshot snapshot, ConstructionSector sector, PlannerRuntimeConfig config)
    {
        if (snapshot == null)
            return true;

        bool fullyCaptured = CountUncapturedInSector(sector, snapshot) <= 0;
        bool hasThreat = HasVisibleEnemyNearSector(snapshot, sector, config);
        return fullyCaptured && !hasThreat;
    }

    private static SectorCandidate BuildPersistedSectorCandidate(AISnapshot snapshot, ConstructionSector sector, int uncaptured)
    {
        return new SectorCandidate
        {
            Sector = sector,
            Uncaptured = Mathf.Max(0, uncaptured),
            DistToOwnHq = ComputeSectorDistanceToOwnHq(sector, snapshot),
            DistToEnemyHq = ComputeSectorDistanceToNearestEnemyHq(sector, snapshot),
            EnemyPressure = EstimateEnemyPressure(sector, snapshot),
            HasEnemyHq = SectorHasEnemyHq(sector, snapshot),
            EnemyHqNearbyCount = CountEnemyHqsWithinRange(sector, snapshot, 8),
            EnemyHqThreatSum = ComputeEnemyHqThreatSum(sector, snapshot, 12)
        };
    }

    private static bool CanUnitPerformRole(UnitData unitData, AIPlanRole role)
    {
        if (unitData == null)
            return false;
        AIUnitProfile profile = unitData.aiUnitProfile;
        if (profile == null)
            return false;
        switch (role)
        {
            case AIPlanRole.Capture:
                return profile.HasPlanCapability(AIPlanCapability.Capture, unitData);
            case AIPlanRole.Escort:
                return profile.HasPlanCapability(AIPlanCapability.Escort, unitData);
            case AIPlanRole.Artillery:
                return profile.HasPlanCapability(AIPlanCapability.FireSupport, unitData);
            case AIPlanRole.Support:
                return profile.HasPlanCapability(AIPlanCapability.Logistics, unitData);
            case AIPlanRole.Assault:
                return profile.HasPlanCapability(AIPlanCapability.Assault, unitData);
            default:
                return profile.HasSensorInStance(AIStance.Attack, AIUnitSensorKind.Attack);
        }
    }

    private static int AssignClosestUnitsForRole(
        AIPlanIntent intent,
        AISnapshot snapshot,
        HashSet<int> assignedUnits,
        Vector3Int targetCell,
        int wanted,
        AIPlanRole role)
    {
        if (wanted <= 0)
            return 0;
        var candidates = new List<(UnitManager unit, int dist)>();
        for (int u = 0; u < snapshot.FriendlyUnits.Count; u++)
        {
            UnitManager unit = snapshot.FriendlyUnits[u];
            if (unit == null || unit.IsDead)
                continue;
            if (assignedUnits.Contains(unit.InstanceId))
                continue;
            unit.TryGetUnitData(out UnitData unitData);
            if (!CanUnitPerformRole(unitData, role))
                continue;
            Vector3Int uc = unit.CurrentCellPosition;
            int dist = Mathf.Abs(uc.x - targetCell.x) + Mathf.Abs(uc.y - targetCell.y);
            candidates.Add((unit, dist));
        }
        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
        int assigned = 0;
        for (int i = 0; i < candidates.Count && assigned < wanted; i++)
        {
            UnitManager unit = candidates[i].unit;
            assignedUnits.Add(unit.InstanceId);
            intent.Assignments.Add(new AIPlanAssignment
            {
                UnitInstanceId = unit.InstanceId,
                Role = role,
                Intent = intent,
            });
            assigned++;
        }
        return assigned;
    }

    public static string BuildPlanKey(AIPlanIntent intent)
    {
        if (intent == null)
            return string.Empty;

        if (ConstructionSectorHelper.IsBase(intent.Sector))
            return $"invasion:{intent.Sector}";

        return $"dynamic:capture:{intent.Sector}";
    }

    private static void ApplyMissionPersistenceAndReallocation(
        AISnapshot snapshot,
        List<AIPlanIntent> plans,
        IReadOnlyCollection<MissionAssignmentMemory> previousAssignments,
        PlannerRuntimeConfig config,
        List<string> plannerLogs)
    {
        if (plans == null || plans.Count == 0)
            return;

        var desiredRoleCounts = CaptureDesiredRoleCounts(plans);
        var unitById = BuildFriendlyUnitIndex(snapshot);
        var planByKey = BuildPlanByKey(plans);
        var unitAssignments = BuildUnitAssignments(plans);
        var stagnatedPlans = ComputeStagnatedPlans(previousAssignments, planByKey, config);

        foreach (string stagnatedKey in stagnatedPlans)
            AddPlannerLog(plannerLogs, $"liberado-estagnacao | {stagnatedKey} | sem progresso > {config.StagnationTurns} turnos");

        if (previousAssignments != null)
        {
            foreach (MissionAssignmentMemory memory in previousAssignments)
            {
                if (!unitById.ContainsKey(memory.UnitInstanceId))
                    continue;
                if (string.IsNullOrWhiteSpace(memory.PlanKey))
                    continue;
                if (!planByKey.TryGetValue(memory.PlanKey, out AIPlanIntent targetPlan))
                    continue;

                if (unitAssignments.TryGetValue(memory.UnitInstanceId, out AIPlanAssignment existing) && existing != null && existing.Intent == targetPlan && existing.Role == memory.Role)
                {
                    AddPlannerLog(plannerLogs, $"preservado | {unitById[memory.UnitInstanceId].name} -> {memory.PlanKey} [{memory.Role.ToDebugLabel()}]");
                    continue;
                }

                if (TryMoveUnitToPlan(snapshot, unitById[memory.UnitInstanceId], targetPlan, memory.Role, unitAssignments, plannerLogs, "preservado", null))
                    continue;
            }
        }

        RefillMissingRoles(snapshot, plans, desiredRoleCounts, unitAssignments, unitById, plannerLogs);
        RefillMinimumPlanOccupancy(snapshot, plans, unitAssignments, unitById, plannerLogs);
    }

    private static Dictionary<string, Dictionary<AIPlanRole, int>> CaptureDesiredRoleCounts(List<AIPlanIntent> plans)
    {
        var desired = new Dictionary<string, Dictionary<AIPlanRole, int>>();
        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent plan = plans[i];
            if (plan == null)
                continue;

            string key = BuildPlanKey(plan);
            if (!desired.TryGetValue(key, out Dictionary<AIPlanRole, int> roles))
            {
                roles = new Dictionary<AIPlanRole, int>();
                desired[key] = roles;
            }

            for (int a = 0; a < plan.Assignments.Count; a++)
            {
                AIPlanAssignment assignment = plan.Assignments[a];
                if (assignment == null)
                    continue;

                if (!roles.ContainsKey(assignment.Role))
                    roles[assignment.Role] = 0;
                roles[assignment.Role]++;
            }
        }

        return desired;
    }

    private static Dictionary<int, UnitManager> BuildFriendlyUnitIndex(AISnapshot snapshot)
    {
        var map = new Dictionary<int, UnitManager>();
        if (snapshot == null)
            return map;

        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager unit = snapshot.FriendlyUnits[i];
            if (unit == null || unit.IsDead)
                continue;
            map[unit.InstanceId] = unit;
        }

        IReadOnlyList<UnitManager> allUnits = UnitManager.AllActive;
        for (int i = 0; allUnits != null && i < allUnits.Count; i++)
        {
            UnitManager unit = allUnits[i];
            if (unit == null || unit.IsDead || unit.TeamId != snapshot.AiTeam)
                continue;
            map[unit.InstanceId] = unit;
        }

        return map;
    }

    private static Dictionary<string, AIPlanIntent> BuildPlanByKey(List<AIPlanIntent> plans)
    {
        var map = new Dictionary<string, AIPlanIntent>();
        if (plans == null)
            return map;

        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent intent = plans[i];
            if (intent == null)
                continue;
            map[BuildPlanKey(intent)] = intent;
        }

        return map;
    }

    private static Dictionary<int, AIPlanAssignment> BuildUnitAssignments(List<AIPlanIntent> plans)
    {
        var map = new Dictionary<int, AIPlanAssignment>();
        if (plans == null)
            return map;

        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent intent = plans[i];
            if (intent == null)
                continue;

            for (int a = 0; a < intent.Assignments.Count; a++)
            {
                AIPlanAssignment assignment = intent.Assignments[a];
                if (assignment == null)
                    continue;
                assignment.Intent = intent;
                map[assignment.UnitInstanceId] = assignment;
            }
        }

        return map;
    }

    private static HashSet<string> ComputeStagnatedPlans(
        IReadOnlyCollection<MissionAssignmentMemory> previousAssignments,
        Dictionary<string, AIPlanIntent> activePlanByKey,
        PlannerRuntimeConfig config)
    {
        var stagnated = new HashSet<string>();
        if (previousAssignments == null || previousAssignments.Count == 0)
            return stagnated;

        var allOld = new Dictionary<string, int>();
        var recent = new Dictionary<string, int>();

        foreach (MissionAssignmentMemory memory in previousAssignments)
        {
            if (string.IsNullOrWhiteSpace(memory.PlanKey))
                continue;
            if (!activePlanByKey.ContainsKey(memory.PlanKey))
                continue;

            if (!allOld.ContainsKey(memory.PlanKey))
                allOld[memory.PlanKey] = 0;
            allOld[memory.PlanKey]++;

            int turnsWithoutProgress = Mathf.Max(0, config.CurrentTurn - memory.LastProgressTurn);
            if (turnsWithoutProgress <= Mathf.Max(0, config.StagnationTurns))
            {
                if (!recent.ContainsKey(memory.PlanKey))
                    recent[memory.PlanKey] = 0;
                recent[memory.PlanKey]++;
            }
        }

        foreach (KeyValuePair<string, int> kv in allOld)
        {
            if (!recent.ContainsKey(kv.Key))
                stagnated.Add(kv.Key);
        }

        return stagnated;
    }

    private static bool TryMoveUnitToPlan(
        AISnapshot snapshot,
        UnitManager unit,
        AIPlanIntent targetPlan,
        AIPlanRole role,
        Dictionary<int, AIPlanAssignment> unitAssignments,
        List<string> plannerLogs,
        string eventTag,
        AIPlanIntent sourceOverride)
    {
        if (unit == null || unit.IsDead || targetPlan == null)
            return false;

        unit.TryGetUnitData(out UnitData unitData);
        if (!CanUnitPerformRole(unitData, role))
            return false;

        AIPlanIntent sourcePlan = sourceOverride;
        AIPlanAssignment oldAssignment = null;
        if (unitAssignments.TryGetValue(unit.InstanceId, out AIPlanAssignment existing))
        {
            oldAssignment = existing;
            sourcePlan = existing.Intent;

            if (existing.Intent == targetPlan && existing.Role == role)
                return false;

            if (sourcePlan != null)
                sourcePlan.Assignments.Remove(existing);
            unitAssignments.Remove(unit.InstanceId);
        }

        AIPlanAssignment moved = new AIPlanAssignment
        {
            UnitInstanceId = unit.InstanceId,
            Role = role,
            Intent = targetPlan
        };

        if (role == AIPlanRole.Capture)
            ResolvePlannedCaptureForAssignment(snapshot, targetPlan, moved);

        targetPlan.Assignments.Add(moved);
        unitAssignments[unit.InstanceId] = moved;

        string sourceKey = sourcePlan != null ? BuildPlanKey(sourcePlan) : "(livre)";
        string targetKey = BuildPlanKey(targetPlan);
        int dist = GetHexDistance(unit.CurrentCellPosition, GetIntentAnchorCell(targetPlan, snapshot));
        AddPlannerLog(plannerLogs, $"{eventTag} | {unit.name} {sourceKey} -> {targetKey} [{role.ToDebugLabel()}] dist={dist}");
        return true;
    }

    private static void ResolvePlannedCaptureForAssignment(AISnapshot snapshot, AIPlanIntent intent, AIPlanAssignment assignment)
    {
        if (snapshot == null || intent == null || assignment == null)
            return;

        List<AIConstructionInfo> targets = CollectUncapturedTargetsInSector(intent.Sector, snapshot);
        var occupiedTargets = new HashSet<Vector3Int>();
        for (int i = 0; i < intent.Assignments.Count; i++)
        {
            AIPlanAssignment existing = intent.Assignments[i];
            if (existing == null || !existing.HasPlannedCaptureTarget)
                continue;
            Vector3Int occupied = existing.PlannedCaptureCell;
            occupied.z = 0;
            occupiedTargets.Add(occupied);
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Vector3Int cell = targets[i].Cell;
            cell.z = 0;
            if (occupiedTargets.Contains(cell))
                continue;

            assignment.HasPlannedCaptureTarget = true;
            assignment.PlannedCaptureCell = cell;
            assignment.PlannedCaptureLabel = !string.IsNullOrWhiteSpace(targets[i].DisplayName)
                ? targets[i].DisplayName
                : intent.Sector.ToString();
            return;
        }

        if (intent.HasCaptureTarget)
        {
            assignment.HasPlannedCaptureTarget = true;
            assignment.PlannedCaptureCell = intent.CaptureTargetCell;
            assignment.PlannedCaptureLabel = intent.CaptureTargetLabel;
        }
    }

    private static int CountVisibleEnemiesNearHq(AISnapshot snapshot, int radius)
    {
        if (snapshot == null || !snapshot.HasHq)
            return 0;

        int count = 0;
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            int distance = GetHexDistance(snapshot.HqCell, enemyCell);
            if (distance <= radius)
                count++;
        }

        return count;
    }

    private sealed class ReassignCandidate
    {
        public UnitManager Unit;
        public AIPlanIntent SourcePlan;
        public AIPlanRole Role;
        public int DistanceToDestination;
    }

    private static List<ReassignCandidate> CollectCandidatesForReassignment(
        List<AIPlanIntent> plans,
        AIPlanIntent destination,
        Dictionary<int, UnitManager> unitById,
        Vector3Int destinationCell,
        bool onlyNonCapture)
    {
        var candidates = new List<ReassignCandidate>();
        if (plans == null)
            return candidates;

        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent source = plans[i];
            if (source == null || source == destination)
                continue;

            for (int a = 0; a < source.Assignments.Count; a++)
            {
                AIPlanAssignment assignment = source.Assignments[a];
                if (assignment == null)
                    continue;
                if (onlyNonCapture && assignment.Role == AIPlanRole.Capture)
                    continue;

                if (!unitById.TryGetValue(assignment.UnitInstanceId, out UnitManager unit))
                    continue;

                int distance = GetHexDistance(unit.CurrentCellPosition, destinationCell);
                candidates.Add(new ReassignCandidate
                {
                    Unit = unit,
                    SourcePlan = source,
                    Role = assignment.Role,
                    DistanceToDestination = distance
                });
            }
        }

        candidates.Sort((a, b) => a.DistanceToDestination.CompareTo(b.DistanceToDestination));
        return candidates;
    }

    private static bool IsPlanCritical(
        AISnapshot snapshot,
        AIPlanIntent plan,
        Dictionary<string, AIPlanIntent> activePlans,
        HashSet<string> stagnatedPlans,
        PlannerRuntimeConfig config)
    {
        if (plan == null)
            return false;

        string key = BuildPlanKey(plan);
        if (stagnatedPlans != null && stagnatedPlans.Contains(key))
            return false;

        bool hasSectorThreat = HasVisibleEnemyNearSector(snapshot, plan.Sector, config);
        bool hasCaptureGap = plan.HasCaptureTarget && !HasAssignedRole(plan, AIPlanRole.Capture);
        return hasSectorThreat || hasCaptureGap;
    }

    private static bool HasVisibleEnemyNearSector(AISnapshot snapshot, ConstructionSector sector, PlannerRuntimeConfig config)
    {
        if (snapshot == null || snapshot.VisibleEnemies.Count == 0)
            return false;
        if (ConstructionSectorHelper.IsBase(sector))
            return CountVisibleEnemiesNearHq(snapshot, Mathf.Max(1, config.MinimumRangeForDefensePlan)) > 0;

        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            Vector3Int cell = enemy.CurrentCellPosition;
            float sqrDist = (new Vector2(cell.x, cell.y) - centroid).sqrMagnitude;
            if (sqrDist <= 36f)
                return true;
        }

        return false;
    }

    private static bool CanSourcePlanSpareRole(AIPlanIntent sourcePlan, AIPlanRole role)
    {
        if (sourcePlan == null)
            return true;

        int sameRole = CountAssignments(sourcePlan, role);
        if (role == AIPlanRole.Capture)
            return sameRole > 1;

        return sourcePlan.Assignments.Count > 1 || sameRole > 1;
    }

    private static int CountAssignments(AIPlanIntent intent, AIPlanRole? role)
    {
        if (intent == null)
            return 0;

        int count = 0;
        for (int i = 0; i < intent.Assignments.Count; i++)
        {
            AIPlanAssignment assignment = intent.Assignments[i];
            if (assignment == null)
                continue;
            if (role.HasValue && assignment.Role != role.Value)
                continue;
            count++;
        }

        return count;
    }

    private static void RefillMissingRoles(
        AISnapshot snapshot,
        List<AIPlanIntent> plans,
        Dictionary<string, Dictionary<AIPlanRole, int>> desiredRoleCounts,
        Dictionary<int, AIPlanAssignment> unitAssignments,
        Dictionary<int, UnitManager> unitById,
        List<string> plannerLogs)
    {
        if (snapshot == null || plans == null || desiredRoleCounts == null)
            return;
        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent plan = plans[i];
            if (plan == null)
                continue;
            string key = BuildPlanKey(plan);
            if (!desiredRoleCounts.TryGetValue(key, out Dictionary<AIPlanRole, int> desiredByRole))
                continue;
            foreach (KeyValuePair<AIPlanRole, int> roleDemand in desiredByRole)
            {
                int current = CountAssignments(plan, roleDemand.Key);
                int missing = Mathf.Max(0, roleDemand.Value - current);
                for (int m = 0; m < missing; m++)
                {
                    if (!TryAssignClosestFreeUnit(snapshot, plan, roleDemand.Key, unitAssignments, unitById, plannerLogs))
                        break;
                }
            }
        }
    }
    private static void RefillMinimumPlanOccupancy(
        AISnapshot snapshot,
        List<AIPlanIntent> plans,
        Dictionary<int, AIPlanAssignment> unitAssignments,
        Dictionary<int, UnitManager> unitById,
        List<string> plannerLogs)
    {
        if (snapshot == null || plans == null)
            return;
        const int minimumParticipants = 2;
        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent plan = plans[i];
            if (plan == null || !plan.HasCaptureTarget)
                continue;
            int currentParticipants = CountAssignments(plan, null);
            int missingParticipants = Mathf.Max(0, minimumParticipants - currentParticipants);
            for (int m = 0; m < missingParticipants; m++)
            {
                bool filled = TryAssignClosestFreeUnit(snapshot, plan, AIPlanRole.Escort, unitAssignments, unitById, plannerLogs, "ocupacao-minima")
                    || TryAssignClosestFreeUnit(snapshot, plan, AIPlanRole.Capture, unitAssignments, unitById, plannerLogs, "ocupacao-minima");
                if (!filled)
                    break;
            }
        }
    }
    private static bool TryAssignClosestFreeUnit(
        AISnapshot snapshot,
        AIPlanIntent plan,
        AIPlanRole role,
        Dictionary<int, AIPlanAssignment> unitAssignments,
        Dictionary<int, UnitManager> unitById,
        List<string> plannerLogs,
        string eventTag = "preenchido-livre")
    {
        UnitManager bestUnit = null;
        int bestDistance = int.MaxValue;
        Vector3Int anchor = GetIntentAnchorCell(plan, snapshot);
        foreach (KeyValuePair<int, UnitManager> kv in unitById)
        {
            int unitId = kv.Key;
            UnitManager unit = kv.Value;
            if (unit == null || unit.IsDead)
                continue;
            if (unit.IsEmbarked)
                continue;
            if (unitAssignments.ContainsKey(unitId))
                continue;
            unit.TryGetUnitData(out UnitData data);
            if (!CanUnitPerformRole(data, role))
                continue;
            int distance = GetHexDistance(unit.CurrentCellPosition, anchor);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestUnit = unit;
            }
        }
        if (bestUnit == null)
            return false;
        AIPlanAssignment assignment = new AIPlanAssignment
        {
            UnitInstanceId = bestUnit.InstanceId,
            Role = role,
            Intent = plan
        };
        if (role == AIPlanRole.Capture)
            ResolvePlannedCaptureForAssignment(snapshot, plan, assignment);
        plan.Assignments.Add(assignment);
        unitAssignments[bestUnit.InstanceId] = assignment;
        AddPlannerLog(plannerLogs, $"{eventTag} | {bestUnit.name} -> {BuildPlanKey(plan)} [{role.ToDebugLabel()}] dist={bestDistance}");
        return true;
    }

    private static Vector3Int GetIntentAnchorCell(AIPlanIntent intent, AISnapshot snapshot)
    {
        if (intent != null && intent.HasCaptureTarget)
            return intent.CaptureTargetCell;
        if (snapshot != null && snapshot.HasHq)
            return snapshot.HqCell;
        return Vector3Int.zero;
    }

    private static int GetHexDistance(Vector3Int a, Vector3Int b)
    {
        a.z = 0;
        b.z = 0;
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static void AddPlannerLog(List<string> plannerLogs, string message)
    {
        if (plannerLogs == null || string.IsNullOrWhiteSpace(message))
            return;
        plannerLogs.Add(message);
    }
}


