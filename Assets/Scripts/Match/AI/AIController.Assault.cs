using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int AssaultScoutZoneRadius = 2;
    // Se qualquer slot de Capturador no objetivo tiver DistanceToObjective ≤ esse limiar,
    // o escort entra em "advance mode": prioriza avançar ao objetivo em vez de patrulhar.
    private const int AdvancedCapturerThreshold = 6;
    // Penalidade por congestionamento à frente: vizinhos com custo menor (próximo passo de rota)
    // que estão bloqueados por aliados. Ratio 0..1 × esse peso é subtraído do score.
    private const float ForwardCongestionWeight = 700f;

    // -------------------------------------------------------------------------
    // Assalto Batedor - protege e varre a zona do objetivo de captura atribuido.
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideAssaultAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null || plan == null)
            return null;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Assalto)
            return null;

        SectorObjective assigned = ResolveAssignedAssaultObjective(unit, plan);
        if (assigned == null)
        {
            PlayerAction embarkAction = TryDecideAssaultEmbarkAction(unit, snapshot, plan);
            if (embarkAction != null) return embarkAction;
            return DecideRogueAssaultBreakerAction(unit, snapshot);
        }

        if (!IsCriticalHomeDefenseObjective(assigned, snapshot.AITeam)
            && TryFindCriticalHomeDefenseObjectiveForUnit(plan, snapshot.AITeam, unit, unit.CurrentCellPosition, "Assalto", out SectorObjective criticalHome))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} redireciona {assigned.Sector} -> {criticalHome.Sector}: Base/HQ sob ameaca");
            assigned = criticalHome;
        }

        return DecideAssignedAssaultEscortAction(unit, snapshot, assigned);
    }

    private PlayerAction DecideRogueAssaultBreakerAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction vacateAction))
            return vacateAction;

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (TryFindAssaultBreakerAttack(unit, snapshot, fromCell, paths, occupied, enemies,
                out Vector3Int attackCell, out UnitManager attackTarget, out string attackReason))
        {
            Vector3Int targetCell = attackTarget.CurrentCellPosition; targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} breaker — ataca via {attackCell} → {attackTarget.UnitDisplayName}#{attackTarget.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                attackTarget.InstanceId.ToString(), targetCell, paths);
        }

        Vector3Int pressureTarget = ResolveAssaultPressureTarget(snapshot, enemies, fromCell);
        Vector3Int bestMove = FindAssaultPressureMove(unit, snapshot, fromCell, pressureTarget, paths, occupied);
        if (bestMove != fromCell)
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} breaker — pressiona via {bestMove} alvo={pressureTarget}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestMove, paths);
        }

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} breaker — mantém posição");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private PlayerAction DecideAssignedAssaultEscortAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Vector3Int scoutAnchorCell = ResolveAssaultEscortCell(assigned, snapshot.AITeam, fromCell);
        int scoutZoneRadius = ResolveAssaultScoutZoneRadius(unit, assigned);

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // Se o escort está no corredor de avanço do capturador, exclui a célula atual
        // do patrol para forçar movimento real e liberar o caminho.
        if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction assignedVacateAction))
            return assignedVacateAction;

        TeamObjectivePlan escortPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        bool inCorridor = escortPlan != null && IsAssaultEscortInCapturerCorridor(unit, fromCell, escortPlan, snapshot.AITeam);
        if (inCorridor)
        {
            occupied.Add(fromCell);
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — cede corredor, força movimento");
        }

        List<UnitManager> threats = CollectAssaultEscortThreats(snapshot.AITeam, scoutAnchorCell, scoutZoneRadius);
        AddAssaultEscortTravelThreats(snapshot.AITeam, fromCell, paths, threats);
        bool defensiveContext = assigned.Status == ObjectiveStatus.Defending;
        if (TryFindAssaultEscortAttack(unit, snapshot, fromCell, scoutAnchorCell, scoutZoneRadius, defensiveContext, paths, occupied, threats,
                out Vector3Int attackCell, out UnitManager attackTarget, out string attackReason))
        {
            Vector3Int targetCell = attackTarget.CurrentCellPosition; targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — ataca via {attackCell} → {attackTarget.UnitDisplayName}#{attackTarget.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                attackTarget.InstanceId.ToString(), targetCell, paths);
        }

        int bestCapturerDist = GetBestCapturerDistanceToObjective(assigned);
        bool escortAdvanceMode = bestCapturerDist >= 0 && bestCapturerDist <= AdvancedCapturerThreshold;
        if (escortAdvanceMode)
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — ADVANCE MODE: capturador mais próximo a {bestCapturerDist}PM de {assigned.Sector}");

        if (escortAdvanceMode
            && TryFindAssaultAdvanceRouteAttack(unit, snapshot, fromCell, scoutAnchorCell, defensiveContext, paths, occupied,
                out Vector3Int routeAttackCell, out UnitManager routeAttackTarget, out string routeAttackReason))
        {
            Vector3Int targetCell = routeAttackTarget.CurrentCellPosition; targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — intercepta via {routeAttackCell} → {routeAttackTarget.UnitDisplayName}#{routeAttackTarget.InstanceId} ({routeAttackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, routeAttackCell,
                routeAttackTarget.InstanceId.ToString(), targetCell, paths);
        }

        List<Vector3Int> suspectCells = CollectSweepSuspectCells(snapshot.AITeam, scoutAnchorCell, scoutZoneRadius);
        if (TryFindAssaultScoutRevealMove(unit, snapshot, fromCell, scoutAnchorCell, scoutZoneRadius, paths, occupied, suspectCells,
                out Vector3Int revealCell, out string revealReason))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — abre FoW via {revealCell} ({revealReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, revealCell, paths);
        }

        Vector3Int coverCell = FindAssaultEscortCoverCell(unit, snapshot, fromCell, scoutAnchorCell, scoutZoneRadius, paths, occupied, threats, bestCapturerDist, out string coverEvaluationLog);
        if (!string.IsNullOrEmpty(coverEvaluationLog))
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — HexEvaluator.Batedor target={scoutAnchorCell} zona={scoutZoneRadius}h advanceMode={escortAdvanceMode} melhorCapt={bestCapturerDist}PM\n{coverEvaluationLog}");
        if (coverCell != fromCell)
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — patrulha via {coverCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, coverCell, paths);
        }

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — mantém patrulha");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private List<UnitManager> CollectVisibleAssaultEnemies(TeamId aiTeam)
    {
        var enemies = new List<UnitManager>();
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            enemies.Add(enemy);
        }
        return enemies;
    }

    private bool TryFindAssaultBreakerAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        List<UnitManager> enemies,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        out string reason)
    {
        bestCell = fromCell;
        bestTarget = null;
        reason = "";
        if (enemies == null || enemies.Count == 0)
            return false;

        bool preferDpq = unit.TryGetUnitData(out UnitData attackerUd) && attackerUd != null && attackerUd.prioritizeDpqAtBattle;
        float dpqWeight = preferDpq ? 2000f : 40f;

        Vector3Int enemyHqCell = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : fromCell;
        enemyHqCell.z = 0;

        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied.Contains(cell)) continue;

            foreach (UnitManager enemy in enemies)
            {
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, cell, false, out string attackDecisionReason))
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition; enemyCell.z = 0;
                ConstructionManager enemyBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, enemyCell);
                bool inOwnConstruction = enemyBldg != null && enemyBldg.TeamId == snapshot.AITeam;
                bool inConstruction = enemyBldg != null;
                // Enemy on OUR building (capturing it) is far more urgent than any other position.
                float constructionBonus = inOwnConstruction ? 20000f : inConstruction ? 5000f : 0f;
                float enemyHqDist = SectorManager.HexDistance(enemyCell, enemyHqCell);
                float cellHqDist = SectorManager.HexDistance(cell, enemyHqCell);
                float dpq = GetTerrainDpqPontos(cell);
                BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, enemy);
                float targetPreferenceScore = GetAssaultTargetPreferenceScore(targetPreference);
                // enemyHqDist penalises enemies far from their HQ (advancing enemies).
                // If they are on OUR building, distance to their HQ is irrelevant — skip the penalty.
                float score =
                    targetPreferenceScore
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 900f
                    + constructionBonus
                    - (inOwnConstruction ? 0f : enemyHqDist * 120f)
                    - cellHqDist * 30f
                    + dpq * dpqWeight
                    - GetPathStepCount(paths, cell) * 5f
                    - enemy.InstanceId * 0.001f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = enemy;
                    reason = $"score={score:F0} pref={targetPreference} hp={enemy.CurrentHP} bldg={inConstruction} ownBldg={inOwnConstruction} enemyHqDist={enemyHqDist:F1} dpq={dpq:F1} dpqW={dpqWeight:F0} preferDpq={preferDpq} {attackDecisionReason}";
                }
            }
        }

        return bestTarget != null;
    }

    private static BazookaTargetPriority ResolveAssaultTargetPreference(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return BazookaTargetPriority.Tertiary;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return BazookaTargetPriority.Tertiary;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return BazookaTargetPriority.Tertiary;

        return attackerData.ResolveAiTargetPriorityForTargetClass(targetData.unitClass);
    }

    private static float GetAssaultTargetPreferenceScore(BazookaTargetPriority priority)
    {
        switch (priority)
        {
            case BazookaTargetPriority.Primary:
                return 30000f;
            case BazookaTargetPriority.Secondary:
                return 15000f;
            default:
                return 0f;
        }
    }

    private Vector3Int ResolveAssaultPressureTarget(AIWorldSnapshot snapshot, List<UnitManager> enemies, Vector3Int fromCell)
    {
        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition; hq.z = 0;
            return hq;
        }

        if (snapshot.EnemyBuildings != null && snapshot.EnemyBuildings.Count > 0)
        {
            ConstructionManager closest = null;
            float bestD = float.MaxValue;
            foreach (ConstructionManager eb in snapshot.EnemyBuildings)
            {
                Vector3Int ec = eb.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(fromCell, ec);
                if (d < bestD) { bestD = d; closest = eb; }
            }
            if (closest != null)
            {
                Vector3Int ec = closest.CurrentCellPosition; ec.z = 0;
                return ec;
            }
        }

        if (enemies != null && enemies.Count > 0)
        {
            UnitManager best = null;
            float bestDist = float.MaxValue;
            foreach (UnitManager enemy in enemies)
            {
                Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(fromCell, ec);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = enemy;
                }
            }
            if (best != null)
            {
                Vector3Int bc = best.CurrentCellPosition; bc.z = 0;
                return bc;
            }
        }

        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition; hq.z = 0;
            return hq;
        }

        // Fallback: edifício inimigo mais próximo (sem filtro FoW)
        if (snapshot.EnemyBuildings != null && snapshot.EnemyBuildings.Count > 0)
        {
            ConstructionManager closest = null;
            float bestD = float.MaxValue;
            foreach (ConstructionManager eb in snapshot.EnemyBuildings)
            {
                Vector3Int ec = eb.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(fromCell, ec);
                if (d < bestD) { bestD = d; closest = eb; }
            }
            if (closest != null)
            {
                Vector3Int ec = closest.CurrentCellPosition; ec.z = 0;
                return ec;
            }
        }

        return fromCell;
    }

    private Vector3Int FindAssaultPressureMove(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int pressureTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        float fromDist = SectorManager.HexDistance(fromCell, pressureTarget);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, pressureTarget, out float fromRouteDist);
        Vector3Int bestCell = fromCell;
        Vector3Int bestFallbackCell = fromCell;
        float bestProgress = float.MinValue;
        float bestLine = float.MinValue;
        int bestPathCost = int.MinValue;
        float bestThreat = float.MaxValue;
        float bestFallbackProgress = float.MinValue;
        float bestFallbackLine = float.MinValue;
        int bestFallbackPathCost = int.MinValue;
        float bestFallbackThreat = float.MaxValue;
        bool foundMove = false;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (cell != fromCell && occupied.Contains(cell)) continue;

            float dist = SectorManager.HexDistance(cell, pressureTarget);
            bool cellRouteFound = TryCalculateRouteDistance(unit, cell, pressureTarget, out float routeDist);
            float dpq = GetTerrainDpqPontos(cell);
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            // Bonus forte para células que avançam; penalidade leve para as que regridem
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - routeDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            float progress = recoversMissingRoute
                ? -routeDist
                : (fromRouteFound && cellRouteFound) ? routeProgress : fromDist - dist;
            float line = CalculateLineProgressTieBreak(fromCell, pressureTarget, cell);
            int pathCost = GetPathStepCount(paths, cell);

            if (IsBetterAssaultPressureMove(progress, line, pathCost, threat, dpq,
                    bestFallbackProgress, bestFallbackLine, bestFallbackPathCost, bestFallbackThreat, GetTerrainDpqPontos(bestFallbackCell)))
            {
                bestFallbackProgress = progress;
                bestFallbackLine = line;
                bestFallbackPathCost = pathCost;
                bestFallbackThreat = threat;
                bestFallbackCell = cell;
            }

            bool movesCloser = recoversMissingRoute
                || routeProgress > 0f
                || (!fromRouteFound && !cellRouteFound && dist <= fromDist);
            if (!movesCloser) continue;

            if (IsBetterAssaultPressureMove(progress, line, pathCost, threat, dpq,
                    bestProgress, bestLine, bestPathCost, bestThreat, GetTerrainDpqPontos(bestCell)))
            {
                bestProgress = progress;
                bestLine = line;
                bestPathCost = pathCost;
                bestThreat = threat;
                bestCell = cell;
                foundMove = true;
            }
        }

        return foundMove ? bestCell : bestFallbackCell;
    }

    private static bool IsBetterAssaultPressureMove(
        float candidateProgress,
        float candidateLine,
        int candidatePathCost,
        float candidateThreat,
        float candidateDpq,
        float bestProgress,
        float bestLine,
        int bestPathCost,
        float bestThreat,
        float bestDpq)
    {
        const float epsilon = 0.001f;
        if (candidateProgress > bestProgress + epsilon) return true;
        if (candidateProgress < bestProgress - epsilon) return false;

        if (candidateLine > bestLine + epsilon) return true;
        if (candidateLine < bestLine - epsilon) return false;

        if (candidatePathCost > bestPathCost) return true;
        if (candidatePathCost < bestPathCost) return false;

        if (candidateThreat < bestThreat - epsilon) return true;
        if (candidateThreat > bestThreat + epsilon) return false;

        return candidateDpq > bestDpq + epsilon;
    }

    private static SectorObjective ResolveAssignedAssaultObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Assalto && slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                    return obj;
        return null;
    }

    // Retorna o menor DistanceToObjective entre os slots de Capturador preenchidos no objetivo.
    // Retorna -1 se nenhum capturador tiver distância conhecida.
    private static int GetBestCapturerDistanceToObjective(SectorObjective obj)
    {
        int best = int.MaxValue;
        foreach (SlotNeed slot in obj.Slots)
        {
            if (slot.Role != UnitRole.Capturador || !slot.Filled || slot.DistanceToObjective < 0) continue;
            if (slot.DistanceToObjective < best) best = slot.DistanceToObjective;
        }
        return best == int.MaxValue ? -1 : best;
    }
}
