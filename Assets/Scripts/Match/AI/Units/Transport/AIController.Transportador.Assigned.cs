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
            // Deliver the cargo toward the assigned sector.
            // Pass the sector building as an override so the courier doesn't fall back to
            // enemy HQ when the passenger was picked up informally (no plan slot).
            ConstructionManager assignedBldg = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            Vector3Int assignedTarget = IsActiveRallyAssemblyObjective(assigned)
                ? ResolveRallyAssemblyAnchor(assigned, snapshot.AITeam, unit.CurrentCellPosition)
                : assignedBldg != null
                    ? (Vector3Int)(assignedBldg.CurrentCellPosition)
                    : Vector3Int.zero;
            if (assignedTarget != Vector3Int.zero) assignedTarget.z = 0;
            return DecideTransportadorCourierAction(unit, snapshot, assignedTarget);
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
                int transportThreshold = GetEffectiveTransportThreshold(snapshot.AITeam);
                int passTerrainCost = TerrainCostToCell(targetPassenger, passCell, objCheckCell, transportThreshold);
                if (passTerrainCost < transportThreshold)
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
            Vector3Int sectorCell = IsActiveRallyAssemblyObjective(assigned)
                ? ResolveRallyAssemblyAnchor(assigned, snapshot.AITeam, fromCell)
                : fromCell;
            if (!IsActiveRallyAssemblyObjective(assigned) && sectorTarget != null) { sectorCell = sectorTarget.CurrentCellPosition; sectorCell.z = 0; }

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
                    out Vector3Int attackCell, out UnitManager attackTarget, preferNoMove, plan))
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
        if (ShouldLeaveAssignedPassengerOnLocalRallyPressure(targetPassenger, snapshot, plan, passengerCell, objCell, out string leaveReason))
        {
            Vector3Int waitTarget = FindTransportWaitTarget(snapshot.AITeam, fromCell);
            waitTarget.z = 0;
            Vector3Int waitMove = FindTransportMove(unit, fromCell, waitTarget, paths, occupied, snapshot.AITeam);
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — deixa passageiro {targetPassenger.InstanceId} entregue ({leaveReason}); retorna pickup/base alvo={waitTarget} via {waitMove}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, waitMove, paths);
        }

        Vector3Int moveTarget = FindTransportShuttleMove(unit, fromCell, passengerCell, paths, occupied, snapshot.AITeam, objCell);

        if (TryFindTransportBreakerAttack(unit, snapshot, fromCell, paths, occupied, passengerCell,
                out Vector3Int pickupAttackCell, out UnitManager pickupAttackTarget, preferNoMove, plan))
        {
            Vector3Int pickupTargetCell = pickupAttackTarget.CurrentCellPosition; pickupTargetCell.z = 0;
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — ataca {pickupAttackTarget.InstanceId} via {pickupAttackCell} (en route pickup {targetPassenger.InstanceId})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, pickupAttackCell,
                pickupAttackTarget.InstanceId.ToString(), pickupTargetCell, paths);
        }

        Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — pickup {targetPassenger.InstanceId}@{passengerCell} via {moveTarget}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    private bool ShouldLeaveAssignedPassengerOnLocalRallyPressure(
        UnitManager passenger,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int passengerCell,
        Vector3Int objectiveCell,
        out string reason)
    {
        reason = "";
        if (passenger == null || snapshot == null || passenger.HasActed || passenger.IsEmbarked)
            return false;

        passengerCell.z = 0;
        objectiveCell.z = 0;

        if (TryFindNearbyPassengerRallyCapture(passenger, snapshot, plan, passengerCell, out ConstructionManager localTarget, out Vector3Int localCell))
        {
            reason = $"captura local {localTarget.Sector}@{localCell}";
            return true;
        }

        if (TryFindNearbyUnheldPassengerRallyPressure(passengerCell, snapshot.AITeam, plan, out ConstructionManager pressureTarget))
        {
            reason = $"pressao local {pressureTarget.Sector}@{pressureTarget.CurrentCellPosition}";
            return true;
        }

        if (objectiveCell != Vector3Int.zero
            && IsPassengerForwardDeployed(passengerCell, snapshot.AITeam)
            && !HasSafeHeldRallyNearCell(objectiveCell, snapshot.AITeam, 6f))
        {
            reason = $"sem rally aliado seguro perto do objetivo {objectiveCell}";
            return true;
        }

        return false;
    }

    private bool TryFindNearbyPassengerRallyCapture(
        UnitManager passenger,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int passengerCell,
        out ConstructionManager bestTarget,
        out Vector3Int bestCell)
    {
        bestTarget = null;
        bestCell = passengerCell;
        if (passenger == null || snapshot == null)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> passengerPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, passenger, Mathf.Max(0, passenger.RemainingMovementPoints), terrainDatabase);
        if (passengerPaths == null || passengerPaths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(passenger);
        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in passengerPaths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != passengerCell && occupied.Contains(cell))
                continue;
            if (SectorManager.HexDistance(passengerCell, cell) > 3f)
                continue;
            if (!SimulateCaptureSensor(passenger, cell, out ConstructionManager target))
                continue;
            if (target == null || !target.IsCapturable || target.CapturePointsMax <= 0)
                continue;
            if (target.TeamId == snapshot.AITeam && target.CurrentCapturePoints >= target.CapturePointsMax)
                continue;
            if (ShouldReserveOpportunisticCaptureForCloserUnit(passenger, snapshot.AITeam, cell, passengerPaths, out _))
                continue;

            bool rallyLike = target.IsRallyPoint || IsPlannedRallyAssemblySector(target.Sector, plan);
            float missingCapture = Mathf.Max(0, target.CapturePointsMax - target.CurrentCapturePoints);
            float score = missingCapture * 80f
                + (rallyLike ? 3000f : 0f)
                - SectorManager.HexDistance(passengerCell, cell) * 250f
                - CalculateThreatLevel(cell, snapshot.AITeam) * 45f
                - target.InstanceId * 0.001f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestTarget = target;
            bestCell = cell;
        }

        return bestTarget != null;
    }

    private bool IsPassengerForwardDeployed(Vector3Int passengerCell, TeamId aiTeam)
    {
        passengerCell.z = 0;
        Vector3Int home = FindTransportWaitTarget(aiTeam, passengerCell);
        home.z = 0;
        return SectorManager.HexDistance(passengerCell, home) > TransportDropOffRange + 1f;
    }

    private bool TryFindNearbyUnheldPassengerRallyPressure(
        Vector3Int passengerCell,
        TeamId aiTeam,
        TeamObjectivePlan plan,
        out ConstructionManager bestTarget)
    {
        bestTarget = null;
        passengerCell.z = 0;
        float bestScore = float.MinValue;

        for (int i = 0; i < ConstructionManager.AllActive.Count; i++)
        {
            ConstructionManager building = ConstructionManager.AllActive[i];
            if (building == null || !building.IsCapturable || building.CapturePointsMax <= 0)
                continue;
            if (building.TeamId == aiTeam && building.CurrentCapturePoints >= building.CapturePointsMax)
                continue;

            bool rallyLike = building.IsRallyPoint || IsPlannedRallyAssemblySector(building.Sector, plan);
            if (!rallyLike)
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            float distance = SectorManager.HexDistance(passengerCell, cell);
            if (distance > TransportDropOffRange + 1f)
                continue;

            float score = (TransportDropOffRange + 1f - distance) * 400f
                + Mathf.Max(0, building.CapturePointsMax - building.CurrentCapturePoints) * 40f
                - CalculateThreatLevel(cell, aiTeam) * 35f
                - building.InstanceId * 0.001f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestTarget = building;
        }

        return bestTarget != null;
    }

    private bool HasSafeHeldRallyNearCell(Vector3Int anchor, TeamId aiTeam, float maxDistance)
    {
        anchor.z = 0;
        for (int i = 0; i < ConstructionManager.AllActive.Count; i++)
        {
            ConstructionManager building = ConstructionManager.AllActive[i];
            if (building == null || !building.IsRallyPoint || building.TeamId != aiTeam)
                continue;
            if (building.CurrentCapturePoints < building.CapturePointsMax)
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            if (SectorManager.HexDistance(anchor, cell) > maxDistance)
                continue;
            if (CalculateThreatLevel(cell, aiTeam) > 0f)
                continue;

            return true;
        }

        return false;
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

    private static UnitManager ResolveAssignedPassengerSlotUnit(SectorObjective assigned, TeamId aiTeam)
    {
        if (assigned == null || assigned.Slots == null) return null;
        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (capturer != null && !capturer.IsDead)
                return capturer;
        }
        return null;
    }
}
