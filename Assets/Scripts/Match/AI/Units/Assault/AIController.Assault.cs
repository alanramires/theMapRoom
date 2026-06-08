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
            if (TryFindCriticalHomeDefenseObjectiveForUnit(plan, snapshot.AITeam, unit, unit.CurrentCellPosition, "Assalto Rogue", out SectorObjective rogueCriticalHome))
            {
                Debug.Log($"{TL("Assalto")} {unit.InstanceId} rogue redireciona -> {rogueCriticalHome.Sector}: Base/HQ sob ameaca");
                return DecideAssignedAssaultEscortAction(unit, snapshot, rogueCriticalHome);
            }

            PlayerAction embarkAction = TryDecideAssaultEmbarkAction(unit, snapshot, plan);
            if (embarkAction != null) return embarkAction;
            return DecideRogueAssaultBreakerAction(unit, snapshot, plan);
        }

        if (!IsCriticalHomeDefenseObjective(assigned, snapshot.AITeam)
            && TryFindCriticalHomeDefenseObjectiveForUnit(plan, snapshot.AITeam, unit, unit.CurrentCellPosition, "Assalto", out SectorObjective criticalHome))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} redireciona {assigned.Sector} -> {criticalHome.Sector}: Base/HQ sob ameaca");
            assigned = criticalHome;
        }

        if (IsRallyAssemblyObjective(assigned))
            return DecideRallyAssemblyAssaultAction(unit, snapshot, assigned);

        return DecideAssignedAssaultEscortAction(unit, snapshot, assigned);
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

        if (TryFindAssaultCaptureTargetVacateAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction targetVacateAction))
            return targetVacateAction;

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

    private bool TryFindAssaultCaptureTargetVacateAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        if (plan == null || !IsOtherAssignedCapturerTarget(fromCell, unit, null, plan, snapshot.AITeam))
            return false;

        if (TryFindAssaultCaptureTargetVacateAttackAction(unit, snapshot, fromCell, paths, occupied, plan, out action))
            return true;

        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;
            if (IsOtherAssignedCapturerTarget(cell, unit, null, plan, snapshot.AITeam))
                continue;

            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (construction != null && construction.CanProduceUnitsForTeam(snapshot.AITeam))
                continue;

            int pathCost = GetPathStepCount(paths, cell);
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float dpq = GetTerrainDpqPontos(cell);
            float score =
                dpq * 90f
                - threat * 70f
                - pathCost * 25f
                - SectorManager.HexDistance(fromCell, cell) * 10f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        if (bestCell == fromCell)
            return false;

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} cede predio-alvo de capturador {fromCell} via {bestCell}");
        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestCell, paths);
        return true;
    }

    private bool TryFindAssaultCaptureTargetVacateAttackAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamObjectivePlan plan,
        out PlayerAction action)
    {
        action = null;
        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (enemies == null || enemies.Count == 0)
            return false;

        bool preferDpq = unit.TryGetUnitData(out UnitData attackerUd) && attackerUd != null && attackerUd.prioritizeDpqAtBattle;
        float dpqWeight = preferDpq ? 2000f : 40f;
        Vector3Int enemyHqCell = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : fromCell;
        enemyHqCell.z = 0;

        Vector3Int bestCell = fromCell;
        UnitManager bestTarget = null;
        string bestReason = "";
        float bestScore = float.MinValue;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;
            if (IsOtherAssignedCapturerTarget(cell, unit, null, plan, snapshot.AITeam))
                continue;

            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (construction != null && construction.CanProduceUnitsForTeam(snapshot.AITeam))
                continue;

            foreach (UnitManager enemy in enemies)
            {
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy))
                    continue;
                if (!PassesAttackDecision(unit, enemy, cell, false, out string attackDecisionReason))
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                ConstructionManager enemyBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, enemyCell);
                bool inOwnConstruction = enemyBldg != null && enemyBldg.TeamId == snapshot.AITeam;
                bool inConstruction = enemyBldg != null;
                float constructionBonus = inOwnConstruction ? 20000f : inConstruction ? 5000f : 0f;
                float enemyHqDist = SectorManager.HexDistance(enemyCell, enemyHqCell);
                float cellHqDist = SectorManager.HexDistance(cell, enemyHqCell);
                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                float dpq = GetTerrainDpqPontos(cell);
                int pathCost = GetPathStepCount(paths, cell);
                BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, enemy);
                float targetPreferenceScore = GetAssaultTargetPreferenceScore(targetPreference);
                float score =
                    targetPreferenceScore
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 900f
                    + constructionBonus
                    - (inOwnConstruction ? 0f : enemyHqDist * 120f)
                    - cellHqDist * 30f
                    + dpq * dpqWeight
                    - threat * 70f
                    - pathCost * 10f
                    - SectorManager.HexDistance(fromCell, cell) * 8f
                    - enemy.InstanceId * 0.001f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = enemy;
                    bestReason = $"score={score:F0} pref={targetPreference} hp={enemy.CurrentHP} bldg={inConstruction} ownBldg={inOwnConstruction} dpq={dpq:F1} dpqW={dpqWeight:F0} threat={threat:F1} preferDpq={preferDpq} {attackDecisionReason}";
                }
            }
        }

        if (bestTarget == null)
            return false;

        Vector3Int targetCell = bestTarget.CurrentCellPosition;
        targetCell.z = 0;
        Debug.Log($"{TL("Assalto")} {unit.InstanceId} cede predio-alvo de capturador {fromCell} e ataca via {bestCell} \u2192 {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} ({bestReason})");
        action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestCell, bestTarget.InstanceId.ToString(), targetCell, paths);
        return true;
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
