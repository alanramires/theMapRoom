using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const float ExplorerForwardObserverProgressWeight = 1200f;
    private const float ExplorerForwardObserverDpqWeight = 35f;
    private const float ExplorerForwardObserverThreatWeight = 90f;
    private const int ExplorerForwardObserverTargetRadius = 4;

    // -------------------------------------------------------------------------
    // Capturador Explorador - revela alvo oculto por FoW
    // -------------------------------------------------------------------------

    private bool TryDecideCapturerExplorer(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned, Vector3Int fromCell, Vector3Int targetCell, Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied, out PlayerAction action)
    {
        action = null;
        // Explorador: ocupante invisível no alvo → DPQ mais elevado + ataque lateral oportunista
        {
            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(targetCell);
            if (occupant != null && occupant.TeamId != snapshot.AITeam)
            {
                MatchController mc = GetMatchController();
                if (mc == null || !mc.IsUnitVisibleForTeam(occupant, snapshot.AITeam))
                {
                    if (TryFindBestLoSCell(unit, paths, occupied, targetCell, out Vector3Int dpqCell))
                    {
                        assigned.Status = ObjectiveStatus.Pursuing;

                        SensorMovementMode dpqMode = dpqCell != fromCell
                            ? SensorMovementMode.MoveuAndando
                            : SensorMovementMode.MoveuParado;
                        var dpqTargets = new List<PodeMirarTargetOption>();
                        if (PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                                dpqMode, dpqTargets, fromCell: dpqCell) && dpqTargets.Count > 0)
                        {
                            UnitManager lateralTarget = null; float lateralPri = float.MinValue;
                            foreach (PodeMirarTargetOption opt in dpqTargets)
                            {
                                if (opt?.targetUnit == null) continue;
                                if (!PassesAttackDecision(unit, opt.targetUnit, dpqCell, assigned.Status == ObjectiveStatus.Defending, out _)) continue;
                                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                                if (SectorManager.HexDistance(tc, targetCell) > DefenseEnemyRange + 1) continue;
                                float p = AttackTargetPriorityPursuer(tc, targetCell);
                                if (p > lateralPri) { lateralPri = p; lateralTarget = opt.targetUnit; }
                            }
                            if (lateralTarget != null)
                            {
                                Vector3Int ltCell = lateralTarget.CurrentCellPosition; ltCell.z = 0;
                                Debug.Log($"{TL("Explorador")} {unit.InstanceId} DPQ {assigned.Sector} via {dpqCell} + ataque lateral → {lateralTarget.UnitDisplayName}#{lateralTarget.InstanceId}");
                                action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, dpqCell,
                                    lateralTarget.InstanceId.ToString(), ltCell, paths);
                                return true;
                            }
                        }

                        Debug.Log($"{TL("Explorador")} {unit.InstanceId} DPQ para revelar {assigned.Sector} via {dpqCell} (ev={GetTerrainEv(dpqCell):F0})");
                        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, dpqCell, paths);
                        return true;
                    }
                }
            }
        }

        if (TryFindBestForwardObserverSpot(unit, snapshot, assigned, fromCell, targetCell, paths, occupied, out ConstructionManager observerSpot, out Vector3Int observerCell))
        {
            assigned.Status = ObjectiveStatus.Pursuing;

            if (observerCell == fromCell)
            {
                Debug.Log($"{TL("Explorador")} {unit.InstanceId} segura observador avancado {observerSpot.ConstructionDisplayName} em {assigned.Sector} @ {observerCell}");
                action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
                return true;
            }

            Vector3Int spotCell = observerSpot.CurrentCellPosition;
            spotCell.z = 0;
            string verb = observerCell == spotCell ? "ocupa" : "aproxima";
            Debug.Log($"{TL("Explorador")} {unit.InstanceId} {verb} observador avancado {observerSpot.ConstructionDisplayName} em {assigned.Sector} via {observerCell}");
            action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, observerCell, paths);
            return true;
        }

        return false;
    }

    private bool TryFindBestForwardObserverSpot(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out ConstructionManager bestSpot,
        out Vector3Int bestCell)
    {
        bestSpot = null;
        bestCell = fromCell;

        if (unit == null || assigned == null || paths == null || paths.Count == 0)
            return false;

        float bestScore = float.MinValue;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || !construction.IsForwardObserverSpot)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;

            float distToTarget = SectorManager.HexDistance(cell, targetCell);
            if (distToTarget > ExplorerForwardObserverTargetRadius)
                continue;

            float fromSpotDist = SectorManager.HexDistance(fromCell, cell);
            bool alreadyHoldingSpot = cell == fromCell;

            foreach (Vector3Int candidate in paths.Keys)
            {
                if (candidate != fromCell && occupied != null && occupied.Contains(candidate))
                    continue;

                float candidateSpotDist = SectorManager.HexDistance(candidate, cell);
                float progressToSpot = fromSpotDist - candidateSpotDist;
                bool reachesSpot = candidate == cell;
                if (reachesSpot && candidate != fromCell && occupied != null && occupied.Contains(candidate))
                    continue;
                if (progressToSpot <= 0f && !reachesSpot && !alreadyHoldingSpot)
                    continue;

                float candidateTargetDist = SectorManager.HexDistance(candidate, targetCell);
                float threat = CalculateThreatLevel(candidate, snapshot.AITeam);
                float moveCost = paths.TryGetValue(candidate, out List<Vector3Int> path) && path != null ? path.Count : 0f;
                float score = progressToSpot * ExplorerForwardObserverProgressWeight
                    + Mathf.Max(0f, ExplorerForwardObserverTargetRadius - distToTarget) * 200f
                    + GetTerrainDpqPontos(candidate) * ExplorerForwardObserverDpqWeight
                    - candidateTargetDist * 20f
                    - threat * ExplorerForwardObserverThreatWeight
                    - moveCost;

                if (reachesSpot)
                    score += 5000f;
                if (alreadyHoldingSpot)
                    score += 500f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSpot = construction;
                    bestCell = candidate;
                }
            }
        }

        return bestSpot != null;
    }

    private bool HasForwardObserverApproachForHiddenObjective(
        UnitManager unit,
        TeamObjectivePlan plan,
        TeamId aiTeam)
    {
        if (unit == null || plan == null || unit.HasActed || unit.IsDead || unit.IsEmbarked)
            return false;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || !data.roles.Contains(UnitRole.Capturador)
            || data.unitClass != GameUnitClass.Infantry)
            return false;

        SectorObjective assigned = ResolveAnyAssignedObjective(unit, plan);
        if (assigned == null || assigned.Status == ObjectiveStatus.Defending)
            return false;

        ConstructionManager target = FindCapturableInSector(assigned.Sector, aiTeam);
        if (target == null)
            return false;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;

        MatchController mc = GetMatchController();
        if (mc != null && mc.IsCellVisibleForActiveTeam(targetCell))
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || !construction.IsForwardObserverSpot)
                continue;

            Vector3Int spotCell = construction.CurrentCellPosition;
            spotCell.z = 0;
            if (SectorManager.HexDistance(spotCell, targetCell) > ExplorerForwardObserverTargetRadius)
                continue;

            float fromSpotDist = SectorManager.HexDistance(fromCell, spotCell);
            foreach (Vector3Int candidate in paths.Keys)
            {
                if (candidate != fromCell && occupied.Contains(candidate))
                    continue;

                if (candidate == spotCell)
                    return true;

                if (SectorManager.HexDistance(candidate, spotCell) < fromSpotDist)
                    return true;
            }
        }

        return false;
    }
}
