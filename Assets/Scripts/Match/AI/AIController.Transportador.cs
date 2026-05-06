using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int TransportDropOffRange = 3;

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

        // Courier does NOT scan for new pickups — it is delivering existing cargo.
        // The next rogue-shuttle turn (empty APC) handles new pickups.
        bool hasCargo = HasTransportCargo(unit);

        SectorObjective assigned = plan != null ? ResolveAssignedTransportObjective(unit, plan) : null;

        if (assigned != null)
            return DecideAssignedTransportAction(unit, snapshot, plan, assigned, hasCargo);
        if (hasCargo)
            return DecideTransportadorCourierAction(unit, snapshot);
        return DecideRogueShuttleAction(unit, snapshot, plan);
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

    // Max-displacement move: minimises remaining distance to target,
    // prefers cells without non-team constructions, uses threat as tiebreaker.
    private Vector3Int FindTransportMove(
        Vector3Int fromCell,
        Vector3Int pressureTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam)
    {
        Vector3Int bestCell = fromCell;
        float bestDist = SectorManager.HexDistance(fromCell, pressureTarget);
        bool bestIsNonTeamBldg = false;
        float bestThreat = float.MaxValue;

        const float eps = 0.01f;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (occupied.Contains(cell)) continue;

            float dist = SectorManager.HexDistance(cell, pressureTarget);
            bool isNonTeamBldg = IsNonTeamConstruction(cell, aiTeam);
            float threat = CalculateThreatLevel(cell, aiTeam);

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
            }
        }

        return bestCell;
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
                        if (tgt != null)
                        {
                            Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
                            return tc;
                        }
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
