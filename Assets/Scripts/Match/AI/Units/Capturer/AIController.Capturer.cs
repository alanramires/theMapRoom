using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Pesos de scoring do capturador (Fases 1-3 — calibrar na Fase 4)
    // -------------------------------------------------------------------------

    private const float CaptureProximityBase  = 500f;
    private const float DpqWeight             = 200f;
    private const float ThreatWeight          = 50f;
    private const float AttackHexBonus        = 800f;
    private const float SafetyThresholdFactor = 0f;
    private const int   ThreatRadius          = 3;

    // -------------------------------------------------------------------------
    // Entrada principal
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideCapturerAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        PlayerAction repairAction = TryDecideRepairAction(unit, snapshot, plan);
        if (repairAction != null) return repairAction;

        // Swap: se um capturador mais forte do mesmo objetivo consegue chegar este turno,
        // cede o edificio e sai do caminho.
        if (plan != null)
        {
            UnitManager incoming = FindSwapIncomingCapturer(unit, plan, snapshot.AITeam);
            if (incoming != null)
            {
                PlayerAction swapAction = DecideSwapVacateAction(unit, incoming, snapshot);
                if (swapAction != null) return swapAction;
            }
        }

        // Opportunistic capture of current cell — takes priority over embark.
        // If already standing on a capturable construction not claimed by another capturer, capture now.
        {
            Vector3Int currentCell = unit.CurrentCellPosition; currentCell.z = 0;
            if (SimulateCaptureSensor(unit, currentCell, out _))
            {
                SectorObjective ownObjective = plan != null ? ResolveAssignedObjective(unit, plan) : null;
                TeamObjectivePlan selfPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
                bool isOtherTarget = selfPlan != null
                    && IsOtherAssignedCapturerTarget(currentCell, unit, ownObjective, selfPlan, snapshot.AITeam);
                if (!isOtherTarget)
                {
                    Debug.Log($"{TL("Oportunista")} {unit.InstanceId} captura célula atual {currentCell} antes de embarcar");
                    return BuildCaptureBatch(unit, snapshot.AITeam, currentCell, currentCell);
                }
            }

            if (TryDecideNearbyRallyCaptureBeforeEmbark(unit, snapshot, plan, currentCell, out PlayerAction nearbyRallyAction))
                return nearbyRallyAction;

            if (TryDecideCapturerOwnedBuildingDefenseBeforeEmbark(unit, snapshot, currentCell, out PlayerAction ownedBuildingDefenseAction))
                return ownedBuildingDefenseAction;
        }

        PlayerAction embarkAction = TryDecideCapturerEmbarkAction(unit, snapshot, plan);
        if (embarkAction != null) return embarkAction;

        SectorObjective assigned = ResolveAssignedObjective(unit, plan);

        if (assigned == null)
        {
            if (!plan.RogueUnitIds.Contains(unit.InstanceId)) return null;
            if (snapshot.EnemyHQ == null) return null;
            return DecideRogueCapturerAction(unit, snapshot);
        }

        return DecideAssignedCapturerAction(unit, snapshot, assigned);
    }

    private bool TryDecideNearbyRallyCaptureBeforeEmbark(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0)
            return false;

        ConstructionManager best = null;
        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int captureCell = rawCell;
            captureCell.z = 0;
            if (captureCell != fromCell && occupied.Contains(captureCell))
                continue;
            float distance = SectorManager.HexDistance(fromCell, captureCell);
            if (distance > 3f)
                continue;

            if (!SimulateCaptureSensor(unit, captureCell, out ConstructionManager captureTarget))
                continue;
            if (captureTarget == null || !captureTarget.IsCapturable || captureTarget.CapturePointsMax <= 0)
                continue;
            if (captureTarget.TeamId == snapshot.AITeam && captureTarget.CurrentCapturePoints >= captureTarget.CapturePointsMax)
                continue;
            if (ShouldReserveOpportunisticCaptureForCloserUnit(unit, snapshot.AITeam, captureCell, paths, out UnitManager reservedFor))
            {
                Debug.Log($"{TL("Oportunista")} {unit.InstanceId} cede captura local perto @ {captureCell} para {reservedFor.InstanceId}");
                continue;
            }

            bool rallyLike = captureTarget.IsRallyPoint || IsPlannedRallyAssemblySector(captureTarget.Sector, plan);
            float missingCapture = Mathf.Max(0, captureTarget.CapturePointsMax - captureTarget.CurrentCapturePoints);
            float score = (3f - distance) * 1000f
                + missingCapture * 80f
                + (rallyLike ? 3000f : 0f)
                - CalculateThreatLevel(captureCell, snapshot.AITeam) * 45f
                - captureTarget.InstanceId * 0.001f;
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = captureTarget;
            bestCell = captureCell;
        }

        if (best == null)
            return false;

        Debug.Log($"{TL("Oportunista")} {unit.InstanceId} captura local perto {best.Sector} @ {bestCell} antes de embarcar score={bestScore:F0} rally={best.IsRallyPoint}");
        action = BuildCaptureBatch(unit, snapshot.AITeam, fromCell, bestCell, paths);
        return true;
    }

    private static bool IsPlannedRallyAssemblySector(ConstructionSector sector, TeamObjectivePlan plan)
    {
        if (plan == null || sector == ConstructionSector.None)
            return false;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj != null && obj.Sector == sector && IsActiveRallyAssemblyObjective(obj))
                return true;
        }
        return false;
    }


    // -------------------------------------------------------------------------
    // Capturador com plano — captura o setor atribuído, defende após conquista
    // -------------------------------------------------------------------------

    private PlayerAction DecideAssignedCapturerAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        ConstructionManager target = FindCapturableInSector(assigned.Sector, snapshot.AITeam, fromCell);

        // (C) Setor ja conquistado - modo Defensor
        if (target == null)
            return DecideCapturerDefenderAction(unit, snapshot, assigned, fromCell);

        Vector3Int targetCell = target.CurrentCellPosition; targetCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        // Se está sobre o objetivo de captura de outro setor com capturador ativo,
        // marca o próprio hex como ocupado para forçar saída no scoring.
        TeamObjectivePlan selfPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        bool onOtherTarget = selfPlan != null && IsOtherAssignedCapturerTarget(fromCell, unit, assigned, selfPlan, snapshot.AITeam);
        if (onOtherTarget)
        {
            occupied.Add(fromCell);
            Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} — sobre objetivo alheio {fromCell}, cedendo hex");
        }

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        if (TryDecideCapturerSpearheadArrival(unit, snapshot, assigned, fromCell, targetCell, paths, occupied, out PlayerAction spearheadAction))
            return spearheadAction;

        if (TryDecideCapturerPursuerCurrent(unit, snapshot, assigned, fromCell, targetCell, paths, occupied, out PlayerAction pursuerAction))
            return pursuerAction;

        HashSet<Vector3Int> reservedOpportunisticCells = null;
        while (TryFindOpportunisticCapture(
            unit,
            paths,
            occupied,
            targetCell,
            out Vector3Int opCell,
            excludeCurrentCell: false,
            skippedCaptureCells: reservedOpportunisticCells))
        {
            if (ShouldReserveOpportunisticCaptureForCloserUnit(unit, snapshot.AITeam, opCell, paths, out UnitManager reservedFor))
            {
                Debug.Log($"{TL("Oportunista")} {unit.InstanceId} cede captura oportunista @ {opCell} para {reservedFor.InstanceId}");
                reservedOpportunisticCells ??= new HashSet<Vector3Int>();
                reservedOpportunisticCells.Add(opCell);
                continue;
            }

            return DecideCapturerOpportunistAction(unit, snapshot, assigned, fromCell, opCell, paths);
        }

        bool hasRecommendedAdvanceCell = TryFindRecommendedCapturerAdvanceCell(
            unit,
            snapshot,
            assigned,
            fromCell,
            targetCell,
            paths,
            occupied,
            out Vector3Int recommendedAdvanceCell);

        if (TryDecideCapturerDefensiveOpportunityAttack(
            unit,
            snapshot,
            assigned,
            fromCell,
            paths,
            occupied,
            out PlayerAction defensiveOpportunityAttack))
            return defensiveOpportunityAttack;

        if (TryDecideCapturerExplorer(
            unit,
            snapshot,
            assigned,
            fromCell,
            targetCell,
            paths,
            occupied,
            recommendedAdvanceCell,
            hasRecommendedAdvanceCell,
            out PlayerAction explorerAction))
            return explorerAction;

        // Scoring: avança pelo melhor hex (PontaLanca) — ataca defensor visível (Perseguidor)
        float fromDist = SectorManager.HexDistance(fromCell, targetCell);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, targetCell, out float fromRouteDist);

        // Coleta todos os inimigos visíveis dentro de fromDist do objetivo (fonte: AllActive + FoW)
        MatchController mcDef = GetMatchController();
        var nearbyEnemies = new List<UnitManager>();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mcDef != null && !mcDef.IsUnitVisibleForTeam(enemy, snapshot.AITeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, targetCell) > fromDist) continue;
            nearbyEnemies.Add(enemy);
        }
        nearbyEnemies.Sort((a, b) =>
        {
            Vector3Int ca = a.CurrentCellPosition; ca.z = 0;
            Vector3Int cb = b.CurrentCellPosition; cb.z = 0;
            return SectorManager.HexDistance(ca, targetCell)
                .CompareTo(SectorManager.HexDistance(cb, targetCell));
        });

        Vector3Int bestMove       = fromCell;
        float      bestScore     = float.MinValue;
        float      bestSectorTie = float.MinValue;
        float      bestHqTie     = float.MinValue;
        float      bestRoute2    = 0f;
        float      bestNextDist  = float.MaxValue;
        bool       canAdvance    = false;

        Vector3Int attackMove       = fromCell;
        UnitManager attackTarget    = null;
        float      attackScore     = float.MinValue;
        float      attackPriority  = float.MinValue;
        float      attackDpqTie    = float.MinValue;
        float      attackSectorTie = float.MinValue;
        float      attackHqTie     = float.MinValue;
        bool       hasAttackHex    = false;

        bool preferDpqMove     = unit.TryGetUnitData(out UnitData moveUd) && moveUd.preferMoveOnBestDPQ;
        bool preferDpqAtBattle = unit.TryGetUnitData(out UnitData dpqUd)  && dpqUd.prioritizeDpqAtBattle;
        bool conservative      = unit.TryGetUnitData(out UnitData consUd)  && consUd.playConservative;

        var scoringLog = showAIUnitHUD ? new System.Text.StringBuilder() : null;
        bool defCtx = assigned.Status == ObjectiveStatus.Defending;
        scoringLog?.AppendLine($"{TL("Score")} Unit{unit.InstanceId} → {assigned.Sector} status={assigned.Status} defCtx={defCtx} (fromDist={fromDist:F1} dpqMove={preferDpqMove} dpqBattle={preferDpqAtBattle} conservative={conservative})");
        AppendMissingDpqReachabilityDiagnostics(scoringLog, unit, paths, targetCell);

        foreach (Vector3Int cell in paths.Keys)
        {
            float dpqPontos = GetTerrainDpqPontos(cell);
            if (occupied.Contains(cell))
            {
                scoringLog?.AppendLine($"  {cell} SKIP occupied dpqPts={dpqPontos:F1} occ={DescribeAnyUnitAtCellForDiagnostics(cell)}");
                continue;
            }
            float dist     = SectorManager.HexDistance(cell, targetCell);
            bool cellRouteFound = TryCalculateRouteDistance(unit, cell, targetCell, out float routeDist);
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - routeDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            bool advancesByRoute = recoversMissingRoute || routeProgress > 0f;
            bool advancesByHex = !fromRouteFound && !cellRouteFound && dist < fromDist;
            bool  advances = advancesByRoute || advancesByHex;
            // prioritizeDpqAtBattle: considera qualquer célula alcançável como
            // origem de tiro; ela só vence se realmente conseguir atacar.
            bool  eligibleForAttack = advances || preferDpqAtBattle;

            float threat    = conservative ? CalculateThreatLevel(cell, snapshot.AITeam) : 0f;
            float effectiveDist = cellRouteFound ? routeDist : dist;
            float prox      = (1f / (effectiveDist + 1f)) * CaptureProximityBase;
            float dpq       = preferDpqMove ? dpqPontos * DpqWeight : 0f;
            float moveCost  = paths[cell].Count;
            float score     = prox - moveCost + dpq - threat * ThreatWeight;
            float route2    = 0f;
            float nextDist  = float.MaxValue;
            if (TryScoreTwoTurnProgression(unit, fromCell, targetCell, cell, paths[cell], occupied, out route2, out nextDist))
                score += route2;
            float sectorTie = -effectiveDist;
            float hqDist    = CalculateEnemyHqDistance(cell, snapshot, unit);
            float hqTie     = CalculateEnemyHqTieBreak(hqDist);

            string hqDistText = hqDist < float.MaxValue ? hqDist.ToString("F1") : "?";
            string routeText = cellRouteFound ? routeDist.ToString("F1") : "?";
            string route2Text = nextDist < float.MaxValue ? nextDist.ToString("F1") : "?";
            // dpqPontos sempre exibido (independente de preferDpqMove) para diagnóstico
            scoringLog?.AppendLine($"  {cell} dist={dist:F1} rota={routeText} progRota={routeProgress:+0.0;-0.0;0.0} rota2={route2:F0}/{route2Text} prox={prox:F0} mv={moveCost:F0} dpqPts={dpqPontos:F1} dpq={dpq:F0} thr={threat:F0} secTie={sectorTie:F1} hq={hqDistText} hqTie={hqTie:F1} -> {score:F0}");

            // bestMove: só células que avançam em direção ao objetivo
            if (advances && IsBetterScore(score, sectorTie, hqTie, bestScore, bestSectorTie, bestHqTie))
            {
                TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
                if (IsOtherAssignedCapturerTarget(cell, unit, assigned, capPlan, snapshot.AITeam))
                    scoringLog?.AppendLine($"    ↳ SKIP bestMove: hex de captura de outro setor");
                else
                {
                    bestScore     = score;
                    bestSectorTie = sectorTie;
                    bestHqTie     = hqTie;
                    bestRoute2    = route2;
                    bestNextDist  = nextDist;
                    bestMove      = cell;
                    canAdvance    = true;
                }
            }

            if (!advances && !eligibleForAttack) continue;

            if (eligibleForAttack && score >= SafetyThresholdFactor && nearbyEnemies.Count > 0)
            {
                foreach (UnitManager nearbyEnemy in nearbyEnemies)
                {
                    if (!CanAttackTargetFrom(fromCell, cell, unit, nearbyEnemy)) continue;
                    bool attackAllowed = PassesAttackDecision(unit, nearbyEnemy, cell, defCtx, out string attackDecisionReason);
                    if (!attackAllowed)
                    {
                        scoringLog?.AppendLine($"    ↳ ATK {nearbyEnemy.UnitDisplayName}#{nearbyEnemy.InstanceId} BLOCK {attackDecisionReason}");
                        continue;
                    }
                    Vector3Int enemyCell = nearbyEnemy.CurrentCellPosition; enemyCell.z = 0;
                    float targetPriority = AttackTargetPriorityPursuer(enemyCell, targetCell);
                    BazookaTargetPriority targetPreference = ResolveCapturerTargetPreference(unit, nearbyEnemy);
                    float targetPreferenceScore = GetCapturerTargetPreferenceScore(targetPreference);
                    float targetPreferenceTie = GetCapturerTargetPreferenceTie(targetPreference);
                    float targetPriorityWithPreference = targetPriority + targetPreferenceTie;
                    float objectiveBonus = enemyCell == targetCell ? 100000f : 0f;
                    float attackDpq = preferDpqAtBattle ? dpqPontos : 0f;
                    float aScore    = objectiveBonus + targetPreferenceScore + targetPriority * 1000f + score + AttackHexBonus;
                    bool  isNewBest = IsBetterAttackCandidate(
                        preferDpqAtBattle,
                        targetPriorityWithPreference,
                        attackDpq,
                        aScore,
                        sectorTie,
                        hqTie,
                        attackPriority,
                        attackDpqTie,
                        attackScore,
                        attackSectorTie,
                        attackHqTie);
                    scoringLog?.AppendLine($"    ↳ ATK {nearbyEnemy.UnitDisplayName}#{nearbyEnemy.InstanceId} pri={targetPriority:F1} pref={targetPreference} prefScore={targetPreferenceScore:F0} objBonus={objectiveBonus:F0} atkDpqPts={attackDpq:F1} aScore={aScore:F0} defCtx={defCtx} {attackDecisionReason}{(isNewBest ? " ★" : "")}");
                    if (isNewBest)
                    {
                        attackScore      = aScore;
                        attackPriority   = targetPriorityWithPreference;
                        attackDpqTie     = attackDpq;
                        attackSectorTie  = sectorTie;
                        attackHqTie      = hqTie;
                        attackMove       = cell;
                        attackTarget     = nearbyEnemy;
                        hasAttackHex     = true;
                    }
                }
            }
        }

        if (scoringLog != null) Debug.Log(scoringLog.ToString());
        if (hasAttackHex && attackTarget != null)
        {
            assigned.Status = ObjectiveStatus.Pursuing;
            Vector3Int atCell = attackTarget.CurrentCellPosition; atCell.z = 0;
            string targetRole = atCell == targetCell ? "defensor do objetivo" : "inimigo";
            Debug.Log($"{TL("Perseguidor")} {unit.InstanceId} move+ataca {targetRole} via {attackMove} → {attackTarget.UnitDisplayName}#{attackTarget.InstanceId}");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackMove,
                attackTarget.InstanceId.ToString(), atCell, paths);
        }
        if (!canAdvance)
        {
            // Está bloqueado mas sobre o objetivo de outro capturador — tenta ceder qualquer hex livre
            if (onOtherTarget)
            {
                foreach (Vector3Int cell in paths.Keys)
                {
                    if (cell == fromCell || occupied.Contains(cell)) continue;
                    Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} cede objetivo alheio {fromCell} → {cell}");
                    return BuildMoveBatch(unit, snapshot.AITeam, fromCell, cell, paths);
                }
            }
            // Bloqueado sem avanço possível (aliado ocupa o alvo, fogo inimigo controla o setor, etc.).
            // Tenta captura oportunista de emergência antes de esperar — inclui hex atual e ignora
            // reservas, pois qualquer captura útil vale mais que ficar parado.
            if (TryFindOpportunisticCapture(unit, paths, occupied, targetCell,
                    out Vector3Int emergencyOpCell, excludeCurrentCell: false, skippedCaptureCells: null))
            {
                Debug.Log($"{TL("Oportunista")} {unit.InstanceId} captura oportunista de emergência @ {emergencyOpCell} (avanço bloqueado em {assigned.Sector})");
                return DecideCapturerOpportunistAction(unit, snapshot, assigned, fromCell, emergencyOpCell, paths);
            }

            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(targetCell);
            if (occupant != null && occupant.TeamId == snapshot.AITeam)
            {
                Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} aguarda {assigned.Sector} — aliado {occupant.InstanceId} ocupa o alvo");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);
            }

            return null;
        }

        SensorMovementMode advanceMode = bestMove != fromCell
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;
        var advanceBuffer = new List<PodeMirarTargetOption>();
        if (PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                advanceMode, advanceBuffer, fromCell: bestMove)
            && advanceBuffer.Count > 0)
        {
            UnitManager bestTarget    = null;
            float       bestPriority  = float.MinValue;
            foreach (PodeMirarTargetOption opt in advanceBuffer)
            {
                if (opt?.targetUnit == null) continue;
                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                if (SectorManager.HexDistance(tc, targetCell) > fromDist) continue;
                if (!PassesAttackDecision(unit, opt.targetUnit, bestMove, assigned.Status == ObjectiveStatus.Defending, out _)) continue;
                float priority = AttackTargetPriorityPursuer(tc, targetCell);
                if (priority > bestPriority) { bestPriority = priority; bestTarget = opt.targetUnit; }
            }
            if (bestTarget != null)
            {
                assigned.Status = ObjectiveStatus.Pursuing;
                Vector3Int btCell = bestTarget.CurrentCellPosition; btCell.z = 0;
                Debug.Log($"{TL("Perseguidor")} {unit.InstanceId} move+ataca inimigo via {bestMove} → {bestTarget.UnitDisplayName}#{bestTarget.InstanceId}");
                return BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestMove,
                    bestTarget.InstanceId.ToString(), btCell, paths);
            }
        }

        assigned.Status = ObjectiveStatus.Pursuing;
        float bestHqDist = CalculateEnemyHqDistance(bestMove, snapshot, unit);
        string bestHqText = bestHqDist < float.MaxValue ? bestHqDist.ToString("F1") : "?";
        UnitManager advOccupant = HexOccupancyQuery.FindUnitAtCell(targetCell);
        MatchController mcAdv   = GetMatchController();
        bool hiddenOccupant = advOccupant != null
            && advOccupant.TeamId != snapshot.AITeam
            && (mcAdv == null || !mcAdv.IsUnitVisibleForTeam(advOccupant, snapshot.AITeam));
        bool sectorInContest = HasNearbyVisibleEnemy(targetCell, snapshot.AITeam, DefenseEnemyRange);
        string advTag = hiddenOccupant ? "Explorador" : sectorInContest ? "Perseguidor" : "PontaLanca";
        string bestNextText = bestNextDist < float.MaxValue ? bestNextDist.ToString("F1") : "?";
        Debug.Log($"{TL("Progressao2")} capturador {unit.InstanceId} {assigned.Sector} escolheu {bestMove} rota2={bestRoute2:F0}/{bestNextText}");
        Debug.Log($"{TL(advTag)} {unit.InstanceId} avança para {assigned.Sector} via {bestMove} (score={bestScore:F0}, secTie={bestSectorTie:F1}, hq={bestHqText}, hqTie={bestHqTie:F1})");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestMove, paths);
    }

    // Retorna true se `cell` é o alvo de captura de OUTRO setor com capturador ativo designado.
    // Usado para evitar que um capturador avance para o objetivo alheio e bloqueie seu designado.
    private bool IsOtherAssignedCapturerTarget(Vector3Int cell, UnitManager unit, SectorObjective ownObjective, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null) return false;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == ownObjective) continue;
            if (obj.Status == ObjectiveStatus.Defending) continue;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt == null) continue;
            Vector3Int tgtCell = tgt.CurrentCellPosition; tgtCell.z = 0;
            if (tgtCell != cell) continue;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
                UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (capturer != null) return true;
            }
        }
        return false;
    }

    private bool TryFindRecommendedCapturerAdvanceCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestMove)
    {
        bestMove = fromCell;
        if (unit == null || snapshot == null || assigned == null || paths == null || paths.Count == 0)
            return false;

        float fromDist = SectorManager.HexDistance(fromCell, targetCell);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, targetCell, out float fromRouteDist);
        bool preferDpqMove = unit.TryGetUnitData(out UnitData moveUd) && moveUd.preferMoveOnBestDPQ;
        bool conservative = unit.TryGetUnitData(out UnitData consUd) && consUd.playConservative;

        float bestScore = float.MinValue;
        float bestSectorTie = float.MinValue;
        float bestHqTie = float.MinValue;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied != null && occupied.Contains(cell))
                continue;

            float dist = SectorManager.HexDistance(cell, targetCell);
            bool cellRouteFound = TryCalculateRouteDistance(unit, cell, targetCell, out float routeDist);
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - routeDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            bool advancesByRoute = recoversMissingRoute || routeProgress > 0f;
            bool advancesByHex = !fromRouteFound && !cellRouteFound && dist < fromDist;
            if (!advancesByRoute && !advancesByHex)
                continue;

            TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
            if (IsOtherAssignedCapturerTarget(cell, unit, assigned, capPlan, snapshot.AITeam))
                continue;

            float threat = conservative ? CalculateThreatLevel(cell, snapshot.AITeam) : 0f;
            float effectiveDist = cellRouteFound ? routeDist : dist;
            float prox = (1f / (effectiveDist + 1f)) * CaptureProximityBase;
            float dpq = preferDpqMove ? GetTerrainDpqPontos(cell) * DpqWeight : 0f;
            float moveCost = paths[cell].Count;
            float score = prox - moveCost + dpq - threat * ThreatWeight;
            if (TryScoreTwoTurnProgression(unit, fromCell, targetCell, cell, paths[cell], occupied, out float route2, out _))
                score += route2;

            float sectorTie = -effectiveDist;
            float hqDist = CalculateEnemyHqDistance(cell, snapshot, unit);
            float hqTie = CalculateEnemyHqTieBreak(hqDist);
            if (!IsBetterScore(score, sectorTie, hqTie, bestScore, bestSectorTie, bestHqTie))
                continue;

            bestScore = score;
            bestSectorTie = sectorTie;
            bestHqTie = hqTie;
            bestMove = cell;
        }

        return bestMove != fromCell || bestScore > float.MinValue;
    }
}
