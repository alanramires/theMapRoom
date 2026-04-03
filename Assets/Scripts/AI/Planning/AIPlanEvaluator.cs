using System;
using System.Collections.Generic;
using UnityEngine;

// Avaliador stateless do planner da IA.
// Planos fixos (defesa/ataque) sao ScriptableObjects configurados no editor.
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
        public int DefensePullRadius;
        public int MaxAttackReassignPerTurn;
        public int StagnationTurns;
        public AIStance CurrentStance;
        public int CurrentTurn;
    }

    public static List<AIPlanIntent> Evaluate(AIPlanDatabase database, AISnapshot snapshot)
    {
        PlannerRuntimeConfig config = new PlannerRuntimeConfig
        {
            DefensePullRadius = 6,
            MaxAttackReassignPerTurn = 2,
            StagnationTurns = 2,
            CurrentStance = AIStance.Attack,
            CurrentTurn = 0
        };

        return Evaluate(database, snapshot, null, config, null);
    }

    public static List<AIPlanIntent> Evaluate(
        AIPlanDatabase database,
        AISnapshot snapshot,
        IReadOnlyCollection<MissionAssignmentMemory> previousAssignments,
        PlannerRuntimeConfig config,
        List<string> plannerLogs)
    {
        var result = new List<AIPlanIntent>();
        if (database == null || snapshot == null)
            return result;

        database.EnsureDefaults();

        // Impede que a mesma unidade entre em dois planos.
        var assignedUnits = new HashSet<int>();

        // Planos fixos: defesa primeiro, depois ataque.
        if (ShouldActivateDefensePlan(database.defensePlan, snapshot))
            TryActivateFixedPlan(database.defensePlan, snapshot, assignedUnits, result, "0");
        if (ShouldActivateAttackPlan(database.attackPlan, snapshot))
            TryActivateFixedPlan(database.attackPlan, snapshot, assignedUnits, result, ">");

        // Planos de setor: seleciona os setores ativos do turno.
        SelectActiveSectorPlans(
            snapshot,
            database.maxVariablePlans,
            assignedUnits,
            result,
            plannerLogs);

        ApplyMissionPersistenceAndReallocation(database, snapshot, result, previousAssignments, config, plannerLogs);

        return result;
    }

    private static bool TryActivateFixedPlan(
        AIPlanData plan,
        AISnapshot snapshot,
        HashSet<int> assignedUnits,
        List<AIPlanIntent> result,
        string fixedBadgeSymbol)
    {
        if (plan == null)
            return false;

        if (!EvaluateConditions(plan, snapshot))
            return false;

        AIPlanIntent intent = BuildIntentFromPlan(plan, snapshot, fixedBadgeSymbol);
        intent.SelectionReason = BuildFixedPlanSelectionReason(fixedBadgeSymbol, snapshot);
        AssignUnitsFromParticipants(intent, plan, snapshot, assignedUnits);
        result.Add(intent);
        return true;
    }

    private static bool ShouldActivateDefensePlan(AIPlanData defensePlan, AISnapshot snapshot)
    {
        if (defensePlan == null || snapshot == null || !snapshot.HasHq)
            return false;

        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            int dist = Mathf.Abs(enemy.CurrentCellPosition.x - snapshot.HqCell.x)
                + Mathf.Abs(enemy.CurrentCellPosition.y - snapshot.HqCell.y);
            if (dist <= AISnapshot.DefaultDefendRadius)
                return true;
        }

        return false;
    }

    private static bool ShouldActivateAttackPlan(AIPlanData attackPlan, AISnapshot snapshot)
    {
        if (attackPlan == null || snapshot == null)
            return false;

        int total = 0;
        int owned = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo c = snapshot.KnownConstructions[i];
            if (c == null || !c.IsCapturable)
                continue;

            total++;
            if (c.TeamId == snapshot.AiTeam)
                owned++;
        }

        if (total <= 0)
            return false;

        return ((float)owned / total) >= 0.5f;
    }


    private static string BuildFixedPlanSelectionReason(string fixedBadgeSymbol, AISnapshot snapshot)
    {
        if (string.Equals(fixedBadgeSymbol, "0", System.StringComparison.Ordinal))
        {
            int nearbyEnemies = CountVisibleEnemiesNearHq(snapshot, AISnapshot.DefaultDefendRadius);
            return $"near HQ | visible-enemies<={AISnapshot.DefaultDefendRadius}hex={nearbyEnemies}";
        }

        if (string.Equals(fixedBadgeSymbol, ">", System.StringComparison.Ordinal))
        {
            int total = 0;
            int owned = 0;
            if (snapshot != null)
            {
                for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
                {
                    AIConstructionInfo c = snapshot.KnownConstructions[i];
                    if (c == null || !c.IsCapturable)
                        continue;
                    total++;
                    if (c.TeamId == snapshot.AiTeam)
                        owned++;
                }
            }

            return $"map control >= 50% | owned={owned}/{total}";
        }

        return "fixed-plan gate";
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
            if (info.Sector == ConstructionSector.BaseTeam) continue;
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
            draft.InfantryDemand = Mathf.Max(0, Mathf.Min(force.Infantry, draft.CaptureTargets.Count));

            // Ajusta nome para facilitar leitura no log/debug.
            int escortDemand = force.ArmoredEscort + force.Artillery + force.ApcEscort;
            intent.DisplayName = $"Captura {sector.Sector} [INF {force.Infantry}, ESC {escortDemand}]";
            intent.TacticalRiskScore = ComputeSupportRiskScore(sector);
            intent.SelectionReason = BuildSectorSelectionReason(sector, generated + 1);

            AddPlannerLog(
                plannerLogs,
                $"setor-ativo | setor={sector.Sector} rank={generated + 1} score={intent.TacticalRiskScore} force=INF{force.Infantry}/ESC{(force.ArmoredEscort + force.Artillery + force.ApcEscort)} targets={draft.CaptureTargets.Count}");

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
        public int Infantry;
        public int ArmoredEscort;
        public int Artillery;
        public int ApcEscort;
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

        int infantry = Mathf.Max(1, sector.Uncaptured);
        if (distant) infantry += 1;
        if (sector.EnemyPressure >= 2) infantry += 1 + (sector.EnemyPressure - 2);
        if (enemyHqProximity) infantry += 1;
        if (multiEnemyHqPressure) infantry += 1;

        int armored = 0;
        if (sector.Uncaptured >= 2 || distant || sector.EnemyPressure > 0) armored += 1;
        armored += sector.Uncaptured / 3;
        armored += sector.EnemyPressure / 2;
        if (enemyHqProximity) armored += 1;
        if (multiEnemyHqPressure) armored += 1;

        int artillery = 0;
        if (sector.Uncaptured >= 4 || sector.EnemyPressure >= 2 || enemyHqProximity) artillery += 1;
        artillery += sector.Uncaptured / 4;
        artillery += sector.EnemyPressure / 2;
        if (multiEnemyHqPressure) artillery += 1;

        int apc = 0;
        if (infantry >= 3 && (distant || sector.Uncaptured >= 4)) apc += 1;
        apc += sector.Uncaptured / 5;
        if (sector.EnemyPressure >= 3) apc += 1;

        return new PlannedForce
        {
            Infantry = Mathf.Max(1, infantry),
            ArmoredEscort = Mathf.Max(0, armored),
            Artillery = Mathf.Max(0, artillery),
            ApcEscort = Mathf.Max(0, apc),
        };
    }

    private static int CountUncapturedInSector(ConstructionSector sector, AISnapshot snapshot)
    {
        int uncaptured = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo c = snapshot.KnownConstructions[i];
            if (c == null || c.Sector != sector || !c.IsCapturable) continue;

            bool aiOwnsFully = c.TeamId == snapshot.AiTeam && c.CapturePoints >= c.CapturePointsMax;
            if (!aiOwnsFully)
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

    private static bool EvaluateConditions(AIPlanData plan, AISnapshot snapshot)
    {
        if (plan.activationConditions == null || plan.activationConditions.Count == 0)
            return true;

        for (int i = 0; i < plan.activationConditions.Count; i++)
        {
            if (!EvaluateCondition(plan.activationConditions[i], plan.targetSector, snapshot))
                return false;
        }
        return true;
    }

    private static bool EvaluateCondition(PlanCondition cond, ConstructionSector sector, AISnapshot snapshot)
    {
        switch (cond.type)
        {
            case PlanConditionType.AlwaysActive:
                return true;

            case PlanConditionType.SectorNotControlledByAI:
            {
                for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
                {
                    AIConstructionInfo info = snapshot.KnownConstructions[i];
                    if (info == null || info.Sector != sector || !info.IsCapturable) continue;
                    if (info.TeamId != snapshot.AiTeam)
                        return true;
                }
                return false;
            }

            case PlanConditionType.SectorPartiallyControlledByAI:
            {
                bool hasAI = false;
                bool hasOther = false;
                for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
                {
                    AIConstructionInfo info = snapshot.KnownConstructions[i];
                    if (info == null || info.Sector != sector || !info.IsCapturable) continue;
                    if (info.TeamId == snapshot.AiTeam) hasAI = true;
                    else hasOther = true;
                }
                return hasAI && hasOther;
            }

            case PlanConditionType.EnemyUnitsVisibleInSector:
            {
                Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
                for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
                {
                    UnitManager enemy = snapshot.VisibleEnemies[i];
                    if (enemy == null || enemy.IsDead) continue;
                    Vector3Int ec = enemy.CurrentCellPosition;
                    float sqrDist = (new Vector2(ec.x, ec.y) - centroid).sqrMagnitude;
                    if (sqrDist <= 16f)
                        return true;
                }
                return false;
            }

            case PlanConditionType.FriendlyStrengthBelowPercent:
                return false; // MVP

            default:
                return true;
        }
    }

    private static AIPlanIntent BuildIntentFromPlan(AIPlanData plan, AISnapshot snapshot, string fixedBadgeSymbol)
    {
        var intent = new AIPlanIntent
        {
            Plan = plan,
            Sector = plan.targetSector,
            DisplayName = plan.displayName,
            BadgeSymbol = fixedBadgeSymbol ?? string.Empty,
        };

        FillCaptureTarget(intent, plan.targetSector, snapshot);
        FillSectorEnemy(intent, plan.targetSector, snapshot);
        return intent;
    }

    private static AIPlanIntent BuildSectorPlanIntent(ConstructionSector sector, AISnapshot snapshot)
    {
        string sectorName = sector.ToString();
        string badge = !string.IsNullOrWhiteSpace(sectorName) ? sectorName.Substring(0, 1).ToUpperInvariant() : string.Empty;

        var intent = new AIPlanIntent
        {
            Plan = null,
            Sector = sector,
            DisplayName = $"Captura {sector}",
            BadgeSymbol = badge,
        };

        FillCaptureTarget(intent, sector, snapshot);
        FillSectorEnemy(intent, sector, snapshot);
        return intent;
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
        if (snapshot.VisibleEnemies.Count == 0 || sector == ConstructionSector.BaseTeam)
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

    private static void AssignUnitsFromParticipants(
        AIPlanIntent intent,
        AIPlanData plan,
        AISnapshot snapshot,
        HashSet<int> assignedUnits)
    {
        if (plan.participants == null || plan.participants.Count == 0)
            return;

        Vector3Int targetCell = intent.HasCaptureTarget ? intent.CaptureTargetCell : snapshot.HqCell;

        for (int p = 0; p < plan.participants.Count; p++)
        {
            AIPlanParticipantDefinition def = plan.participants[p];
            if (def == null) continue;

            UnitManager bestUnit = null;
            int bestDist = int.MaxValue;

            for (int u = 0; u < snapshot.FriendlyUnits.Count; u++)
            {
                UnitManager unit = snapshot.FriendlyUnits[u];
                if (unit == null || unit.IsDead) continue;
                if (assignedUnits.Contains(unit.InstanceId)) continue;

                unit.TryGetUnitData(out UnitData unitData);

                if (def.unitData != null)
                {
                    if (unitData != def.unitData) continue;
                }
                else
                {
                    if (!CanUnitPerformRole(unitData, def.role)) continue;
                }

                Vector3Int uc = unit.CurrentCellPosition;
                int dist = Mathf.Abs(uc.x - targetCell.x) + Mathf.Abs(uc.y - targetCell.y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestUnit = unit;
                }
            }

            if (bestUnit == null) continue;

            assignedUnits.Add(bestUnit.InstanceId);
            intent.Assignments.Add(new AIPlanAssignment
            {
                UnitInstanceId = bestUnit.InstanceId,
                Role = def.role,
                Intent = intent,
            });
        }
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
            AIPlanRole.Escort,
            d => d.Force.ArmoredEscort + d.Force.Artillery + d.Force.ApcEscort);
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
                int assignedNow = AssignClosestEscortUnits(draft.Intent, snapshot, assignedUnits, targetCell, 1, role);
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
                return profile.allowCapture;

            case AIPlanRole.Escort:
            case AIPlanRole.Artillery:
            case AIPlanRole.Support:
                return profile.canEscort;

            case AIPlanRole.Assault:
            default:
                return profile.allowAttack;
        }
    }

    private static int AssignClosestEscortUnits(
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
            if (unitData == null || unitData.aiUnitProfile == null || !unitData.aiUnitProfile.canEscort)
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

        if (intent.Plan != null)
        {
            string stableId = !string.IsNullOrWhiteSpace(intent.Plan.planId)
                ? intent.Plan.planId
                : (!string.IsNullOrWhiteSpace(intent.Plan.displayName) ? intent.Plan.displayName : intent.Plan.name);
            return $"fixed:{stableId}";
        }

        return $"dynamic:capture:{intent.Sector}";
    }

    private static void ApplyMissionPersistenceAndReallocation(
        AIPlanDatabase database,
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

        bool defenseGate = IsDefenseGateOpen(snapshot);
        bool invasionGate = IsInvasionGateOpen(snapshot);
        AIPlanIntent defensePlan = FindFixedIntent(plans, database != null ? database.defensePlan : null);
        AIPlanIntent invasionPlan = FindFixedIntent(plans, database != null ? database.attackPlan : null);

        if (defenseGate && config.CurrentStance == AIStance.Defend && defensePlan != null)
        {
            int wanted = Mathf.Max(1, CountVisibleEnemiesNearHq(snapshot, AISnapshot.DefaultDefendRadius));
            int current = CountAssignments(defensePlan, null);
            int missing = Mathf.Max(0, wanted - current);
            if (missing > 0)
            {
                var candidates = CollectCandidatesForReassignment(plans, defensePlan, unitById, snapshot.HqCell, onlyNonCapture: false);
                for (int i = 0; i < candidates.Count && missing > 0; i++)
                {
                    ReassignCandidate candidate = candidates[i];
                    if (candidate.DistanceToDestination > Mathf.Max(1, config.DefensePullRadius))
                    {
                        AddPlannerLog(plannerLogs, $"bloqueado-realocacao | {candidate.Unit.name} -> defesa | motivo=fora do raio ({candidate.DistanceToDestination}>{config.DefensePullRadius})");
                        continue;
                    }

                    bool originCritical = IsPlanCritical(snapshot, candidate.SourcePlan, planByKey, stagnatedPlans);
                    if (originCritical && !CanSourcePlanSpareRole(candidate.SourcePlan, candidate.Role))
                    {
                        AddPlannerLog(plannerLogs, $"bloqueado-realocacao | {candidate.Unit.name} {BuildPlanKey(candidate.SourcePlan)} -> defesa | motivo=plano origem critico");
                        continue;
                    }

                    if (TryMoveUnitToPlan(snapshot, candidate.Unit, defensePlan, candidate.Role, unitAssignments, plannerLogs, "realocado-defesa", candidate.SourcePlan))
                        missing--;
                }
            }
        }

        if (invasionGate && config.CurrentStance == AIStance.Attack && invasionPlan != null)
        {
            int pulled = 0;
            int maxPull = Mathf.Max(0, config.MaxAttackReassignPerTurn);
            Vector3Int invasionAnchor = GetIntentAnchorCell(invasionPlan, snapshot);
            var candidates = CollectCandidatesForReassignment(plans, invasionPlan, unitById, invasionAnchor, onlyNonCapture: true);
            for (int i = 0; i < candidates.Count && pulled < maxPull; i++)
            {
                ReassignCandidate candidate = candidates[i];
                bool originCritical = IsPlanCritical(snapshot, candidate.SourcePlan, planByKey, stagnatedPlans);
                if (originCritical && !CanSourcePlanSpareRole(candidate.SourcePlan, candidate.Role))
                {
                    AddPlannerLog(plannerLogs, $"bloqueado-realocacao | {candidate.Unit.name} {BuildPlanKey(candidate.SourcePlan)} -> invasao | motivo=plano origem critico");
                    continue;
                }

                if (TryMoveUnitToPlan(snapshot, candidate.Unit, invasionPlan, candidate.Role, unitAssignments, plannerLogs, "realocado-invasao", candidate.SourcePlan))
                    pulled++;
            }
        }

        RefillMissingRoles(snapshot, plans, desiredRoleCounts, unitAssignments, unitById, plannerLogs);
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

    private static bool IsDefenseGateOpen(AISnapshot snapshot)
    {
        return snapshot != null && snapshot.HasHq && CountVisibleEnemiesNearHq(snapshot, AISnapshot.DefaultDefendRadius) > 0;
    }

    private static bool IsInvasionGateOpen(AISnapshot snapshot)
    {
        if (snapshot == null || snapshot.KnownConstructions.Count == 0)
            return false;

        int owned = 0;
        int total = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo construction = snapshot.KnownConstructions[i];
            if (construction == null || !construction.IsCapturable)
                continue;

            total++;
            if (construction.TeamId == snapshot.AiTeam)
                owned++;
        }

        if (total <= 0)
            return false;

        float control = (float)owned / total;
        return control >= 0.5f;
    }

    private static AIPlanIntent FindFixedIntent(List<AIPlanIntent> plans, AIPlanData fixedPlan)
    {
        if (plans == null || fixedPlan == null)
            return null;

        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent intent = plans[i];
            if (intent != null && intent.Plan == fixedPlan)
                return intent;
        }

        return null;
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
        HashSet<string> stagnatedPlans)
    {
        if (plan == null)
            return false;

        string key = BuildPlanKey(plan);
        if (stagnatedPlans != null && stagnatedPlans.Contains(key))
            return false;

        bool hasSectorThreat = HasVisibleEnemyNearSector(snapshot, plan.Sector);
        bool hasCaptureGap = plan.HasCaptureTarget && !HasAssignedRole(plan, AIPlanRole.Capture);
        return hasSectorThreat || hasCaptureGap;
    }

    private static bool HasVisibleEnemyNearSector(AISnapshot snapshot, ConstructionSector sector)
    {
        if (snapshot == null || snapshot.VisibleEnemies.Count == 0)
            return false;
        if (sector == ConstructionSector.BaseTeam)
            return CountVisibleEnemiesNearHq(snapshot, AISnapshot.DefaultDefendRadius) > 0;

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

    private static bool TryAssignClosestFreeUnit(
        AISnapshot snapshot,
        AIPlanIntent plan,
        AIPlanRole role,
        Dictionary<int, AIPlanAssignment> unitAssignments,
        Dictionary<int, UnitManager> unitById,
        List<string> plannerLogs)
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
        AddPlannerLog(plannerLogs, $"preenchido-livre | {bestUnit.name} -> {BuildPlanKey(plan)} [{role.ToDebugLabel()}] dist={bestDistance}");
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
















