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

        // Find the capturer unit this transport is supposed to ferry.
        // Discard the passenger if they're already close enough to walk — same threshold as
        // TryDecideCapturerEmbarkAction, so both sides agree on when transport is no longer needed.
        UnitManager targetPassenger = ResolveAssignedPassengerUnit(assigned, snapshot.AITeam);
        if (targetPassenger != null)
        {
            ConstructionManager objCheck = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            if (objCheck != null)
            {
                Vector3Int objCheckCell = objCheck.CurrentCellPosition; objCheckCell.z = 0;
                Vector3Int passCell = targetPassenger.CurrentCellPosition; passCell.z = 0;
                if (SectorManager.HexDistance(passCell, objCheckCell) < GetEffectiveTransportThreshold(snapshot.AITeam))
                    targetPassenger = null;
            }
        }

        bool preferNoMove = unit.TryGetUnitData(out UnitData assignedData) && assignedData.prioritizeDpqAtBattle;

        if (targetPassenger == null)
        {
            // Formal passenger unavailable (already acted or within walking distance).
            // Before pressuring the sector, check if there is ANY nearby pickup candidate
            // (e.g. a freshly-purchased capturer at the base waiting for a ride).
            // If found, act as shuttle toward them instead of rushing into combat.
            ConstructionManager sectorTarget = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            Vector3Int sectorCell = fromCell;
            if (sectorTarget != null) { sectorCell = sectorTarget.CurrentCellPosition; sectorCell.z = 0; }

            UnitManager nearbyCandidate = FindBestShuttleCandidate(unit, snapshot, plan, fromCell, out Vector3Int nearbyCell, assigned);
            if (nearbyCandidate != null)
            {
                Vector3Int objCell2 = ResolveUnitObjectiveCell(nearbyCandidate, plan, snapshot);
                if (objCell2 == Vector3Int.zero)
                    objCell2 = sectorCell;
                Vector3Int shuttleMove = FindTransportShuttleMove(unit, fromCell, nearbyCell, paths, occupied, snapshot.AITeam, objCell2);
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — sem passageiro formal, aguarda candidato {nearbyCandidate.InstanceId}@{nearbyCell} obj={objCell2} via {shuttleMove}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, shuttleMove, paths);
            }

            Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — sem passageiro, pressiona {sectorCell}");
            Vector3Int sectorMove = FindTransportMove(unit, fromCell, sectorCell, paths, occupied, snapshot.AITeam);

            if (TryFindTransportBreakerAttack(unit, snapshot, fromCell, paths, occupied, sectorCell,
                    out Vector3Int attackCell, out UnitManager attackTarget, preferNoMove))
            {
                Vector3Int targetCell = attackTarget.CurrentCellPosition; targetCell.z = 0;
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — ataca {attackTarget.InstanceId} via {attackCell}");
                return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                    attackTarget.InstanceId.ToString(), targetCell, paths);
            }

            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, sectorMove, paths);
        }

        // Park on the path to the objective: within ShuttlePickupRange of the passenger,
        // as close to the objective as possible so the journey starts immediately after boarding.
        // Use ResolveUnitObjectiveCell so the objective falls back to enemy HQ when the
        // sector capturable is gone (e.g. already taken by another AI unit this turn).
        Vector3Int passengerCell = targetPassenger.CurrentCellPosition; passengerCell.z = 0;
        Vector3Int objCell = ResolveUnitObjectiveCell(targetPassenger, plan, snapshot);
        Vector3Int moveTarget = FindTransportShuttleMove(unit, fromCell, passengerCell, paths, occupied, snapshot.AITeam, objCell);

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
