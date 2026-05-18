using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int TransportDropOffRange = 4;
    // Delivery range, not weapon range: artillery should be carried to the sector front
    // before DPQ decides the exact landing hex.
    private const int FireSupportDropOffRange = 3;

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideTransportadorAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null) return null;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Transportador) return null;

        PlayerAction repairAction = TryDecideRepairAction(unit, snapshot, plan);
        if (repairAction != null) return repairAction;

        if (data.domain == Domain.Air)
            return TryDecideAirTransportAction(unit, snapshot, plan);

        // Courier does NOT scan for new pickups — it is delivering existing cargo.
        // The next rogue-shuttle turn (empty APC) handles new pickups.
        bool hasCargo = HasTransportCargo(unit);

        SectorObjective assigned = plan != null ? ResolveAssignedTransportObjective(unit, plan) : null;

        if (assigned != null)
            return DecideAssignedTransportAction(unit, snapshot, plan, assigned, hasCargo);
        if (hasCargo)
            return DecideTransportadorCourierAction(unit, snapshot);

        PlayerAction rogueAction = DecideRogueShuttleAction(unit, snapshot, plan);
        if (rogueAction != null) return rogueAction;
        return TryDecideTowShuttleAction(unit, snapshot, plan);
    }

    // -------------------------------------------------------------------------
    // Helpers shared across Shuttle / Courier / Assigned
    // -------------------------------------------------------------------------

    private static bool HasTransportCargo(UnitManager unit)
    {
        if (unit.TransportedUnitSlots == null) return false;
        foreach (UnitTransportSeatRuntime seat in unit.TransportedUnitSlots)
            if (seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked) return true;
        return false;
    }

    private static SectorObjective ResolveAssignedTransportObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Transportador && slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                    return obj;
        return null;
    }

    private bool IsNonTeamConstruction(Vector3Int cell, TeamId aiTeam)
    {
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return bldg != null && bldg.TeamId != aiTeam;
    }

    // Max-displacement move: minimises real MP cost to target (unit-aware terrain costs),
    // prefers cells without non-team constructions, uses threat as tiebreaker.
    private Vector3Int FindTransportMove(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int pressureTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam)
    {
        // Reverse cost map: how many MP does this unit need to go from pressureTarget to each cell?
        // Keep this horizon generous: transport objectives can require long detours around
        // terrain chokepoints, and a short horizon looks like a false blockade.
        Dictionary<Vector3Int, int> costFromTarget =
            UnitMovementPathRules.CalculateMovementCostMap(boardTilemap, unit, pressureTarget, 120, terrainDatabase);

        float GetCost(Vector3Int c) =>
            costFromTarget.TryGetValue(c, out int v) ? (float)v : float.MaxValue;

        Vector3Int bestCell = fromCell;
        float bestDist = GetCost(fromCell);
        bool bestIsNonTeamBldg = false;
        float bestThreat = float.MaxValue;
        bool foundReachableRoute = bestDist < float.MaxValue;
        bool foundImprovingMove = false;

        const float eps = 0.01f;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (occupied.Contains(cell)) continue;

            float dist = GetCost(cell);
            bool isNonTeamBldg = IsNonTeamConstruction(cell, aiTeam);
            float threat = CalculateThreatLevel(cell, aiTeam);
            if (dist < float.MaxValue)
                foundReachableRoute = true;

            bool isBetter;
            if (dist < bestDist - eps)
                isBetter = true;
            else if (dist < bestDist + eps)
                isBetter = (!isNonTeamBldg && bestIsNonTeamBldg)
                           || (isNonTeamBldg == bestIsNonTeamBldg && threat < bestThreat - eps);
            else
                isBetter = false;

            if (isBetter)
            {
                bestCell = cell;
                bestDist = dist;
                bestIsNonTeamBldg = isNonTeamBldg;
                bestThreat = threat;
                if (dist < GetCost(fromCell) - eps)
                    foundImprovingMove = true;
            }
        }

        if (foundReachableRoute && (bestCell != fromCell || foundImprovingMove))
            return bestCell;

        return FindTransportExplorationMove(unit, fromCell, pressureTarget, paths, occupied, aiTeam);
    }

    private Vector3Int FindTransportExplorationMove(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int pressureTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam)
    {
        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        float fromHexDist = SectorManager.HexDistance(fromCell, pressureTarget);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, pressureTarget, out float fromRouteDist);

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;

            float hexDist = SectorManager.HexDistance(cell, pressureTarget);
            bool cellRouteFound = TryCalculateRouteDistance(unit, cell, pressureTarget, out float routeDist);
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - routeDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            float progress = recoversMissingRoute
                ? -routeDist
                : (fromRouteFound && cellRouteFound) ? routeProgress : fromHexDist - hexDist;
            int pathSteps = GetPathStepCount(paths, cell);
            float threat = CalculateThreatLevel(cell, aiTeam);
            bool isNonTeamBldg = IsNonTeamConstruction(cell, aiTeam);

            float score =
                Mathf.Min(pathSteps, 8) * 55f
                + Mathf.Max(0f, progress) * 35f
                - Mathf.Max(0f, -progress) * 18f
                - threat * 8f
                - (isNonTeamBldg ? 300f : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    // Returns the effective transport slot threshold for the current map.
    // If the farthest capturable sector is closer than MinDistanceForTransportSlot,
    // the threshold adapts so transport slots are still created and shuttle candidates still qualify.
    public int GetEffectiveTransportThreshold(TeamId aiTeam)
    {
        int maxDist = 0;
        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
        {
            if (info.IsFullyControlled && info.ControllingTeam == aiTeam) continue;
            int d = Mathf.RoundToInt(info.GetDistanceToHQ(aiTeam));
            if (d > maxDist) maxDist = d;
        }
        // Include enemy base sectors so threshold stays at MinDistanceForTransportSlot in late
        // game when all regular sectors are captured (maxDist would otherwise collapse to 0).
        foreach (SectorManager.SectorInfo baseInfo in SectorManager.GetAllBaseInfos())
        {
            if (FindHQTeamInSector(baseInfo.Sector) == aiTeam) continue;
            int d = Mathf.RoundToInt(baseInfo.GetDistanceToHQ(aiTeam));
            if (d > maxDist) maxDist = d;
        }
        return Mathf.Min(MinDistanceForTransportSlot, Mathf.Max(3, maxDist));
    }

    private static List<UnitManager> GetAvailableTransporters(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (data.roles != null && data.roles.Count > 0 && data.roles[0] == UnitRole.Transportador)
                list.Add(u);
        }
        return list;
    }

    // Resolves the objective target cell for a unit: assigned capturable, or fallback to enemy HQ/building.
    private static Vector3Int ResolveUnitObjectiveCell(UnitManager unit, TeamObjectivePlan plan, AIWorldSnapshot snapshot)
    {
        if (plan != null)
        {
            foreach (SectorObjective obj in plan.Objectives)
                foreach (SlotNeed slot in obj.Slots)
                    if (slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                    {
                        ConstructionManager tgt = FindCapturableInSector(obj.Sector, snapshot.AITeam);
                        if (tgt != null) { Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0; return tc; }
                        // No capturable (e.g. FireSupport assigned to a support sector already controlled):
                        // use the sector's representative cell so navigation still targets the right area.
                        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo si))
                        { Vector3Int rc = si.RepresentativeCell; rc.z = 0; return rc; }
                    }
        }

        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition; hq.z = 0;
            return hq;
        }

        if (snapshot.EnemyBuildings != null && snapshot.EnemyBuildings.Count > 0)
        {
            Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
            ConstructionManager nearest = null;
            float nearestDist = float.MaxValue;
            foreach (ConstructionManager eb in snapshot.EnemyBuildings)
            {
                Vector3Int ec = eb.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(unitCell, ec);
                if (d < nearestDist) { nearestDist = d; nearest = eb; }
            }
            if (nearest != null)
            {
                Vector3Int nc = nearest.CurrentCellPosition; nc.z = 0;
                return nc;
            }
        }

        return Vector3Int.zero;
    }
}
