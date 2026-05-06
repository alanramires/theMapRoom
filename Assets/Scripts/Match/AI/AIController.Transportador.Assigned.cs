using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Assigned transport — has a formal slot in a distant objective's plan
    // -------------------------------------------------------------------------

    private PlayerAction DecideAssignedTransportAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        SectorObjective assigned,
        bool hasCargo)
    {
        if (hasCargo)
        {
            // Deliver the cargo; courier decides based on passenger targets
            return DecideTransportadorCourierAction(unit, snapshot);
        }

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // Find the capturer unit this transport is supposed to ferry
        UnitManager targetPassenger = ResolveAssignedPassengerUnit(assigned, snapshot.AITeam);

        if (targetPassenger == null)
        {
            // Assigned capturer not found or already acted: pressure the objective sector
            ConstructionManager sectorTarget = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            Vector3Int sectorCell = fromCell;
            if (sectorTarget != null) { sectorCell = sectorTarget.CurrentCellPosition; sectorCell.z = 0; }

            Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — sem passageiro, pressiona {sectorCell}");
            Vector3Int sectorMove = FindTransportMove(fromCell, sectorCell, paths, occupied, snapshot.AITeam);
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, sectorMove, paths);
        }

        // Move to be adjacent to the assigned passenger
        Vector3Int passengerCell = targetPassenger.CurrentCellPosition; passengerCell.z = 0;
        Vector3Int moveTarget = FindTransportShuttleMove(fromCell, passengerCell, paths, occupied, snapshot.AITeam);
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — pickup {targetPassenger.InstanceId}@{passengerCell} via {moveTarget}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    // Returns the first live, unacted capturer assigned to this objective's slots.
    private static UnitManager ResolveAssignedPassengerUnit(SectorObjective assigned, TeamId aiTeam)
    {
        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (capturer != null && !capturer.IsEmbarked && !capturer.HasActed)
                return capturer;
        }
        return null;
    }
}
