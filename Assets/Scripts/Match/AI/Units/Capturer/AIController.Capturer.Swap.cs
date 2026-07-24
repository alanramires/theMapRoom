using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Capturer Swap
    // Quando um capturador fraco está sobre o edificio do seu objetivo e outro
    // capturador do MESMO objetivo (HP maior) consegue chegar este turno,
    // o fraco cede o hex e o forte captura no lugar.
    // -------------------------------------------------------------------------

    // Versão rápida (sem pathfinding) usada na ordenação de iniciativa.
    private bool HasSwapIncomingCapturerFast(UnitManager occupant, TeamObjectivePlan plan, TeamId aiTeam)
    {
        return FindSwapIncomingCapturer(occupant, plan, aiTeam, fullPathCheck: false) != null;
    }

    // Versão completa (com pathfinding) usada na decisão de ação.
    private UnitManager FindSwapIncomingCapturer(UnitManager occupant, TeamObjectivePlan plan, TeamId aiTeam)
    {
        return FindSwapIncomingCapturer(occupant, plan, aiTeam, fullPathCheck: true);
    }

    private UnitManager FindSwapIncomingCapturer(UnitManager occupant, TeamObjectivePlan plan, TeamId aiTeam, bool fullPathCheck)
    {
        if (occupant == null || plan == null) return null;
        if (!occupant.TryGetUnitData(out UnitData data) || data == null
            || UnitRoleCompatibility.ResolveCompositionRole(data) != UnitRole.Capturador) return null;

        SectorObjective objective = ResolveAssignedObjective(occupant, plan);
        if (objective == null) return null;

        ConstructionManager capturable = FindCapturableInSector(objective.Sector, aiTeam);
        if (capturable == null) return null;
        Vector3Int capCell = capturable.CurrentCellPosition; capCell.z = 0;
        Vector3Int occCell = occupant.CurrentCellPosition;  occCell.z = 0;
        if (occCell != capCell) return null;

        UnitManager best  = null;
        int         bestHP = occupant.CurrentHP;

        foreach (SlotNeed slot in objective.Slots)
        {
            if (slot.Role != UnitRole.Capturador || !slot.Filled) continue;
            if (slot.AssignedUnitId == occupant.InstanceId) continue;

            UnitManager candidate = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (candidate == null || candidate.HasActed || candidate.IsDead || candidate.IsEmbarked) continue;
            if (candidate.CurrentHP <= bestHP) continue;

            Vector3Int candCell = candidate.CurrentCellPosition; candCell.z = 0;
            float hexDist = SectorManager.HexDistance(candCell, capCell);
            if (hexDist > candidate.RemainingMovementPoints) continue;

            if (fullPathCheck)
            {
                // CalcularCaminhosValidos inclui células ocupadas por aliados (são passáveis),
                // portanto capCell estará no dicionário mesmo com o ocupante ainda lá.
                Dictionary<Vector3Int, List<Vector3Int>> candPaths =
                    UnitMovementPathRules.CalcularCaminhosValidos(
                        boardTilemap, candidate, Mathf.Max(0, candidate.RemainingMovementPoints), terrainDatabase);
                if (candPaths == null || !candPaths.ContainsKey(capCell)) continue;
            }

            best   = candidate;
            bestHP = candidate.CurrentHP;
        }

        return best;
    }

    // Libera o edificio e tenta continuar a agenda normal da unidade:
    // primeiro combate util em qualquer hex alcancavel, depois movimento de agenda.
    private PlayerAction DecideSwapVacateAction(UnitManager occupant, UnitManager incoming, AIWorldSnapshot snapshot)
    {
        Vector3Int fromCell = occupant.CurrentCellPosition; fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, occupant, Mathf.Max(0, occupant.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0) return null;

        HashSet<Vector3Int> occupied = BuildOccupied(occupant);
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        Vector3Int objectiveCell = ResolveUnitObjectiveCell(occupant, plan, snapshot);
        if (objectiveCell == Vector3Int.zero)
            objectiveCell = fromCell;
        objectiveCell.z = 0;

        if (TryBuildSwapVacateCombatAction(occupant, incoming, snapshot, fromCell, paths, occupied, plan, out PlayerAction combatAction))
            return combatAction;

        if (!TryFindSwapAgendaVacateCell(occupant, snapshot, fromCell, objectiveCell, paths, occupied, plan, out Vector3Int bestCell, out string bestReason))
            return null;

        plannedDestinations.Add(bestCell);
        Debug.Log($"{TL("Swap")} {occupant.InstanceId} cede edificio para #{incoming.InstanceId} (HP {occupant.CurrentHP}->{incoming.CurrentHP}) e segue agenda -> {fromCell}->{bestCell} ({bestReason})");
        return BuildMoveBatch(occupant, snapshot.AITeam, fromCell, bestCell, paths);
    }

    private bool TryBuildSwapVacateCombatAction(
        UnitManager occupant,
        UnitManager incoming,
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

        bool preferDpq = occupant.TryGetUnitData(out UnitData attackerUd) && attackerUd != null && attackerUd.prioritizeDpqAtBattle;
        float dpqWeight = preferDpq ? 2000f : 40f;
        Vector3Int objectiveCell = ResolveUnitObjectiveCell(occupant, plan, snapshot);
        if (objectiveCell == Vector3Int.zero)
            objectiveCell = snapshot.EnemyHQ != null ? snapshot.EnemyHQ.CurrentCellPosition : fromCell;
        objectiveCell.z = 0;

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
            if (IsOtherAssignedCapturerTarget(cell, occupant, null, plan, snapshot.AITeam))
                continue;

            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (construction != null && construction.CanProduceUnitsForTeam(snapshot.AITeam))
                continue;

            foreach (UnitManager enemy in enemies)
            {
                if (!CanAttackTargetFrom(fromCell, cell, occupant, enemy))
                    continue;
                if (!PassesAttackDecision(occupant, enemy, cell, false, out string attackDecisionReason))
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                ConstructionManager enemyBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, enemyCell);
                bool inOwnConstruction = enemyBldg != null && enemyBldg.SlotIndex == snapshot.AISlotIndex;
                bool inConstruction = enemyBldg != null;
                float constructionBonus = inOwnConstruction ? 20000f : inConstruction ? 5000f : 0f;
                float progress = SectorManager.HexDistance(fromCell, objectiveCell) - SectorManager.HexDistance(cell, objectiveCell);
                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                float dpq = GetTerrainDpqPontos(cell);
                int pathCost = GetPathStepCount(paths, cell);
                BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(occupant, enemy);
                float targetPreferenceScore = GetAssaultTargetPreferenceScore(targetPreference);
                float score =
                    targetPreferenceScore
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 900f
                    + constructionBonus
                    + progress * 120f
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
                    bestReason = $"score={score:F0} pref={targetPreference} hp={enemy.CurrentHP} bldg={inConstruction} ownBldg={inOwnConstruction} progress={progress:F1} dpq={dpq:F1} dpqW={dpqWeight:F0} threat={threat:F1} preferDpq={preferDpq} {attackDecisionReason}";
                }
            }
        }

        if (bestTarget == null)
            return false;

        plannedDestinations.Add(bestCell);
        Vector3Int targetCell = bestTarget.CurrentCellPosition;
        targetCell.z = 0;
        Debug.Log($"{TL("Swap")} {occupant.InstanceId} cede edificio para #{incoming.InstanceId} (HP {occupant.CurrentHP}->{incoming.CurrentHP}) e ataca via {bestCell} -> {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} ({bestReason})");
        action = BuildAttackBatch(occupant, snapshot.AITeam, fromCell, bestCell, bestTarget.InstanceId.ToString(), targetCell, paths);
        return true;
    }

    private bool TryFindSwapAgendaVacateCell(
        UnitManager occupant,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int objectiveCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamObjectivePlan plan,
        out Vector3Int bestCell,
        out string bestReason)
    {
        bestCell = fromCell;
        bestReason = "";
        objectiveCell.z = 0;
        bool preferDpq = occupant.TryGetUnitData(out UnitData attackerUd) && attackerUd != null && attackerUd.prioritizeDpqAtBattle;
        float dpqWeight = preferDpq ? 180f : 70f;
        float bestScore = float.MinValue;

        float fromDist = SectorManager.HexDistance(fromCell, objectiveCell);
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;
            if (IsOtherAssignedCapturerTarget(cell, occupant, null, plan, snapshot.AITeam))
                continue;

            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (construction != null && construction.CanProduceUnitsForTeam(snapshot.AITeam))
                continue;

            float cellDist = SectorManager.HexDistance(cell, objectiveCell);
            float progress = fromDist - cellDist;
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float dpq = GetTerrainDpqPontos(cell);
            int pathCost = GetPathStepCount(paths, cell);
            float score =
                progress * 650f
                + dpq * dpqWeight
                - threat * 80f
                - pathCost * 25f
                - SectorManager.HexDistance(fromCell, cell) * 10f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestReason = $"score={score:F0} progress={progress:F1} dpq={dpq:F1} dpqW={dpqWeight:F0} threat={threat:F1} path={pathCost} preferDpq={preferDpq}";
            }
        }

        return bestCell != fromCell;
    }
}
