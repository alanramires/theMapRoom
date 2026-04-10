using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIPlayerController
{
    private static bool HasAnyTransportedPassenger(UnitManager unit)
    {
        if (unit == null)
            return false;

        IReadOnlyList<UnitTransportSeatRuntime> seats = unit.TransportedUnitSlots;
        if (seats == null || seats.Count <= 0)
            return false;

        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i] != null && seats[i].embarkedUnit != null)
                return true;
        }

        return false;
    }

    private bool TryGetTransportIdlePickupCell(UnitManager unit, AISnapshot snapshot, HashSet<Vector3Int> occupiedByAllies, out Vector3Int targetCell)
    {
        targetCell = default;
        if (unit == null || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        Vector3Int anchorCell;
        if (TryGetNearestOwnedConstruction(unit, snapshot, out _, out anchorCell, out _))
        {
            anchorCell.z = 0;
        }
        else if (snapshot.HasHq)
        {
            anchorCell = snapshot.HqCell;
            anchorCell.z = 0;
        }
        else
        {
            return false;
        }

        int distToAnchor = GetHexDistance(snapshot.BoardTilemap, unitCell, anchorCell, 16);
        bool alreadyInPickupZone = distToAnchor != int.MaxValue
            && distToAnchor > 0
            && distToAnchor <= 2
            && !IsAnyConstructionCell(snapshot, unitCell);

        if (alreadyInPickupZone)
        {
            targetCell = unitCell;
            return true;
        }

        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        List<Vector3Int> frontier = new List<Vector3Int> { anchorCell };
        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { anchorCell };
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        bool found = false;
        int bestDistToUnit = int.MaxValue;
        int bestDistToAnchor = int.MaxValue;
        int bestEnterCost = int.MaxValue;
        Vector3Int bestCell = default;

        for (int depth = 0; depth < 2; depth++)
        {
            List<Vector3Int> nextFrontier = new List<Vector3Int>();
            for (int i = 0; i < frontier.Count; i++)
            {
                Vector3Int current = frontier[i];
                UnitMovementPathRules.GetImmediateHexNeighbors(snapshot.BoardTilemap, current, neighbors);
                for (int n = 0; n < neighbors.Count; n++)
                {
                    Vector3Int candidate = neighbors[n];
                    candidate.z = 0;
                    if (!visited.Add(candidate))
                        continue;

                    nextFrontier.Add(candidate);

                    if (IsAnyConstructionCell(snapshot, candidate))
                        continue;
                    if (occupiedByAllies != null && occupiedByAllies.Contains(candidate))
                        continue;
                    if (IsCellOccupiedBySnapshotUnit(snapshot, unit, null, candidate))
                        continue;
                    if (!CanAiUnitEndMoveAtCell(unit, snapshot.BoardTilemap, candidate))
                        continue;

                    int distToAnchorCandidate = GetHexDistance(snapshot.BoardTilemap, anchorCell, candidate, 16);
                    if (distToAnchorCandidate == int.MaxValue || distToAnchorCandidate <= 0 || distToAnchorCandidate > 2)
                        continue;

                    int distToUnit = GetHexDistance(snapshot.BoardTilemap, unitCell, candidate, 64);
                    if (distToUnit == int.MaxValue)
                        continue;

                    int enterCost = int.MaxValue;
                    if (!UnitMovementPathRules.TryGetEnterCellCost(
                            snapshot.BoardTilemap,
                            unit,
                            candidate,
                            terrainDb,
                            applyOperationalAutonomyModifier: false,
                            out enterCost))
                        continue;

                    bool better = !found
                        || distToUnit < bestDistToUnit
                        || (distToUnit == bestDistToUnit && distToAnchorCandidate < bestDistToAnchor)
                        || (distToUnit == bestDistToUnit && distToAnchorCandidate == bestDistToAnchor && enterCost < bestEnterCost);
                    if (!better)
                        continue;

                    found = true;
                    bestDistToUnit = distToUnit;
                    bestDistToAnchor = distToAnchorCandidate;
                    bestEnterCost = enterCost;
                    bestCell = candidate;
                }
            }

            frontier = nextFrontier;
            if (frontier.Count <= 0)
                break;
        }

        if (!found)
            return false;

        targetCell = bestCell;
        targetCell.z = 0;
        return true;
    }

    private static bool IsAnyConstructionCell(AISnapshot snapshot, Vector3Int cell)
    {
        if (snapshot == null || snapshot.KnownConstructions == null)
            return false;

        cell.z = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Source == null)
                continue;

            Vector3Int c = info.Cell;
            c.z = 0;
            if (c == cell)
                return true;
        }

        return false;
    }

    private bool TryGetTransportPickupObjective(
        UnitManager passenger,
        AISnapshot snapshot,
        AIPlanIntent unitIntent,
        AIPlanAssignment unitAssignment,
        Vector3Int? plannedCaptureCell,
        out UnitManager transporter,
        out Vector3Int pickupObjectiveCell,
        out string objectiveLabel,
        out bool embarkNow)
    {
        transporter = null;
        pickupObjectiveCell = passenger != null ? passenger.CurrentCellPosition : Vector3Int.zero;
        objectiveLabel = string.Empty;
        embarkNow = false;

        if (passenger == null || snapshot == null || passenger.IsEmbarked)
            return false;
        if (unitAssignment == null || unitAssignment.Role != AIPlanRole.Capture)
            return false;

        Vector3Int captureTarget = plannedCaptureCell ?? (unitIntent != null && unitIntent.HasCaptureTarget ? unitIntent.CaptureTargetCell : pickupObjectiveCell);
        captureTarget.z = 0;
        Vector3Int passengerCell = passenger.CurrentCellPosition;
        passengerCell.z = 0;
        int distanceToTarget = GetHexDistance(passengerCell, captureTarget);
        int passengerMove = Mathf.Max(1, passenger.GetMovementRange());
        int worthwhileThreshold = Mathf.CeilToInt(passengerMove * 1.5f);
        if (distanceToTarget < 8 || distanceToTarget <= passengerMove || distanceToTarget <= worthwhileThreshold)
            return false;

        if (TryGetBestEmbarkTargetNow(passenger, snapshot, unitIntent, captureTarget, out transporter))
        {
            pickupObjectiveCell = passengerCell;
            embarkNow = true;
            objectiveLabel = !string.IsNullOrWhiteSpace(unitIntent?.DisplayName) ? unitIntent.DisplayName : "captura distante";
            return true;
        }

        transporter = FindBestTransporterForPassenger(passenger, snapshot, unitIntent, captureTarget);
        if (transporter == null)
            return false;

        pickupObjectiveCell = transporter.CurrentCellPosition;
        pickupObjectiveCell.z = 0;
        embarkNow = GetHexDistance(passengerCell, pickupObjectiveCell) <= 1;
        objectiveLabel = !string.IsNullOrWhiteSpace(unitIntent?.DisplayName) ? unitIntent.DisplayName : "captura distante";
        return true;
    }

    private bool TryGetBestEmbarkTargetNow(UnitManager passenger, AISnapshot snapshot, AIPlanIntent passengerIntent, Vector3Int captureTargetCell, out UnitManager transporter)
    {
        transporter = null;
        if (passenger == null || snapshot == null || turnStateManager == null)
            return false;

        List<PodeEmbarcarOption> options = new List<PodeEmbarcarOption>();
        if (!PodeEmbarcarSensor.CollectOptions(
                passenger,
                passenger.BoardTilemap,
                turnStateManager.TerrainDatabaseRef,
                Mathf.Max(0, passenger.RemainingMovementPoints),
                options)
            || options.Count <= 0)
            return false;

        int bestScore = int.MinValue;
        for (int i = 0; i < options.Count; i++)
        {
            PodeEmbarcarOption option = options[i];
            UnitManager candidate = option != null ? option.transporterUnit : null;
            if (candidate == null)
                continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition;
            candidateCell.z = 0;
            int score = -GetHexDistance(candidateCell, captureTargetCell) * 100;
            if (snapshot.UnitPlanAssignments != null
                && snapshot.UnitPlanAssignments.TryGetValue(candidate.InstanceId, out AIPlanAssignment candidateAssignment)
                && candidateAssignment != null
                && candidateAssignment.Intent == passengerIntent)
            {
                score += 2500;
            }

            if (score > bestScore)
            {
                bestScore = score;
                transporter = candidate;
            }
        }

        return transporter != null;
    }

    private bool TryGetTransportCarryObjective(
        UnitManager transporter,
        AISnapshot snapshot,
        out UnitManager passenger,
        out Vector3Int passengerTargetCell,
        out Vector3Int moveObjectiveCell,
        out string objectiveLabel,
        out bool disembarkNow)
    {
        passenger = null;
        passengerTargetCell = transporter != null ? transporter.CurrentCellPosition : Vector3Int.zero;
        moveObjectiveCell = passengerTargetCell;
        objectiveLabel = string.Empty;
        disembarkNow = false;

        if (transporter == null || snapshot == null || !TryGetEmbarkedCapturePassenger(transporter, snapshot, out passenger, out passengerTargetCell, out objectiveLabel))
            return false;

        if (!TryGetTransportStagingCellNearObjective(transporter, snapshot, passengerTargetCell, out moveObjectiveCell))
        {
            moveObjectiveCell = passengerTargetCell;
            moveObjectiveCell.z = 0;
        }

        int passengerMove = Mathf.Max(1, passenger.GetMovementRange());
        disembarkNow = ShouldTransportDisembarkNow(
            transporter,
            passenger,
            passengerTargetCell,
            moveObjectiveCell,
            passengerMove);
        return true;
    }

    private bool ShouldTransportDisembarkNow(
        UnitManager transporter,
        UnitManager passenger,
        Vector3Int passengerTargetCell,
        Vector3Int stagingCell,
        int passengerMove)
    {
        if (transporter == null || passenger == null)
            return false;

        Tilemap boardMap = transporter.BoardTilemap;
        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        if (boardMap == null)
            return false;

        List<PodeDesembarcarOption> validOptions = new List<PodeDesembarcarOption>();
        if (!PodeDesembarcarSensor.CollectOptions(transporter, boardMap, terrainDb, validOptions) || validOptions.Count <= 0)
            return false;

        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;
        passengerTargetCell.z = 0;
        stagingCell.z = 0;

        int bestDropDistance = int.MaxValue;
        for (int i = 0; i < validOptions.Count; i++)
        {
            PodeDesembarcarOption option = validOptions[i];
            if (option == null || option.passengerUnit != passenger)
                continue;

            Vector3Int dropCell = option.disembarkCell;
            dropCell.z = 0;
            int dist = GetHexDistance(dropCell, passengerTargetCell);
            if (dist < bestDropDistance)
                bestDropDistance = dist;
        }

        if (bestDropDistance == int.MaxValue)
            return false;

        if (bestDropDistance <= passengerMove + 1)
            return true;

        if (transporterCell == stagingCell)
            return true;

        return false;
    }

    private UnitManager FindBestTransporterForPassenger(UnitManager passenger, AISnapshot snapshot, AIPlanIntent passengerIntent, Vector3Int captureTargetCell)
    {
        if (passenger == null || snapshot == null || snapshot.FriendlyUnits == null)
            return null;

        UnitManager best = null;
        int bestScore = int.MinValue;
        Vector3Int passengerCell = passenger.CurrentCellPosition;
        passengerCell.z = 0;

        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager candidate = snapshot.FriendlyUnits[i];
            if (candidate == null || candidate == passenger || candidate.IsDead || candidate.IsEmbarked)
                continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData) || candidateData == null || !candidateData.isTransporter)
                continue;
            if (!HasTransportSensor(candidateData.aiUnitProfile))
                continue;
            if (!HasAvailableTransportSeatForPassenger(candidate, passenger))
                continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition;
            candidateCell.z = 0;
            int score = -GetHexDistance(candidateCell, passengerCell) * 1000 - GetHexDistance(candidateCell, captureTargetCell) * 100;
            if (snapshot.UnitPlanAssignments != null && snapshot.UnitPlanAssignments.TryGetValue(candidate.InstanceId, out AIPlanAssignment candidateAssignment) && candidateAssignment != null && candidateAssignment.Intent == passengerIntent)
                score += 2500;
            if (candidate.CombatClassification != UnitCombatClassification.Civil)
                score -= 250;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private bool TryGetEmbarkedCapturePassenger(UnitManager transporter, AISnapshot snapshot, out UnitManager passenger, out Vector3Int captureTargetCell, out string objectiveLabel)
    {
        passenger = null;
        captureTargetCell = transporter != null ? transporter.CurrentCellPosition : Vector3Int.zero;
        objectiveLabel = string.Empty;

        if (transporter == null)
            return false;

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null)
            return false;

        int bestDistance = int.MinValue;
        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            UnitManager seatPassenger = seat != null ? seat.embarkedUnit : null;
            if (seatPassenger == null || !seatPassenger.IsEmbarked)
                continue;
            if (snapshot.UnitPlanAssignments == null || !snapshot.UnitPlanAssignments.TryGetValue(seatPassenger.InstanceId, out AIPlanAssignment assignment) || assignment == null)
                continue;
            if (assignment.Role != AIPlanRole.Capture || assignment.Intent == null || !assignment.Intent.HasCaptureTarget)
                continue;

            Vector3Int target = assignment.HasPlannedCaptureTarget ? assignment.PlannedCaptureCell : assignment.Intent.CaptureTargetCell;
            target.z = 0;
            int dist = GetHexDistance(transporter.CurrentCellPosition, target);
            if (dist > bestDistance)
            {
                bestDistance = dist;
                passenger = seatPassenger;
                captureTargetCell = target;
                objectiveLabel = !string.IsNullOrWhiteSpace(assignment.Intent.DisplayName) ? assignment.Intent.DisplayName : assignment.Intent.Sector.ToString();
            }
        }

        return passenger != null;
    }

    private bool TryExecuteAutomatedTransportDisembark(UnitManager passenger, Vector3Int captureTargetCell, out Vector3Int chosenCell)
    {
        chosenCell = Vector3Int.zero;
        if (turnStateManager == null || passenger == null)
            return false;
        if (!turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.Disembark))
            return false;

        IReadOnlyList<PodeDesembarcarOption> options = turnStateManager.CachedPodeDesembarcarTargets;
        if (options == null || options.Count <= 0)
            return false;

        PodeDesembarcarOption best = null;
        int bestDist = int.MaxValue;
        for (int i = 0; i < options.Count; i++)
        {
            PodeDesembarcarOption option = options[i];
            if (option == null || option.passengerUnit != passenger)
                continue;
            Vector3Int cell = option.disembarkCell;
            cell.z = 0;
            int dist = GetHexDistance(cell, captureTargetCell);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = option;
            }
        }

        if (best == null)
            return false;

        chosenCell = best.disembarkCell;
        chosenCell.z = 0;
        if (!turnStateManager.TryQueueAutomatedDisembarkReplayOrder(passenger.InstanceId.ToString(), chosenCell))
            return false;
        return turnStateManager.TryStartAutomatedDisembarkReplayExecution();
    }

    private bool TryGetTransportStagingCellNearObjective(UnitManager transporter, AISnapshot snapshot, Vector3Int objectiveCell, out Vector3Int stagingCell)
    {
        stagingCell = objectiveCell;
        if (transporter == null || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        objectiveCell.z = 0;
        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(snapshot.BoardTilemap, objectiveCell, neighbors);

        bool found = false;
        int bestDistToTransporter = int.MaxValue;
        int bestEnterCost = int.MaxValue;
        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int candidate = neighbors[i];
            candidate.z = 0;
            if (IsAnyConstructionCell(snapshot, candidate))
                continue;
            if (IsCellOccupiedBySnapshotUnit(snapshot, transporter, null, candidate))
                continue;
            if (!CanAiUnitEndMoveAtCell(transporter, snapshot.BoardTilemap, candidate))
                continue;

            int distToTransporter = GetHexDistance(snapshot.BoardTilemap, transporterCell, candidate, 64);
            if (distToTransporter == int.MaxValue)
                continue;

            if (!UnitMovementPathRules.TryGetEnterCellCost(
                    snapshot.BoardTilemap,
                    transporter,
                    candidate,
                    terrainDb,
                    applyOperationalAutonomyModifier: false,
                    out int enterCost))
                continue;

            bool better = !found
                || distToTransporter < bestDistToTransporter
                || (distToTransporter == bestDistToTransporter && enterCost < bestEnterCost);
            if (!better)
                continue;

            found = true;
            bestDistToTransporter = distToTransporter;
            bestEnterCost = enterCost;
            stagingCell = candidate;
        }

        return found;
    }

    private bool HasTransportSensor(AIUnitProfile profile)
    {
        return profile != null && profile.HasSensorInStance(currentStance, AIUnitSensorKind.Transport);
    }

    private static bool HasAvailableTransportSeatForPassenger(UnitManager transporter, UnitManager passenger)
    {
        if (transporter == null || passenger == null)
            return false;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null || !transporterData.isTransporter || transporterData.transportSlots == null)
            return false;
        if (!passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null)
            return false;

        for (int slotIndex = 0; slotIndex < transporterData.transportSlots.Count; slotIndex++)
        {
            UnitTransportSlotRule slot = transporterData.transportSlots[slotIndex];
            if (slot == null)
                continue;
            slot.EnsureDefaults();
            if (!TransportSlotSupportsPassenger(slot, passenger, passengerData))
                continue;
            int occupied = transporter.GetOccupiedTransportSeatCountForSlot(slotIndex);
            if (occupied < Mathf.Max(1, slot.capacity))
                return true;
        }

        return false;
    }

    private static bool TransportSlotSupportsPassenger(UnitTransportSlotRule slot, UnitManager passenger, UnitData passengerData)
    {
        if (slot == null || passenger == null || passengerData == null)
            return false;

        bool layerAllowed = false;
        if (slot.allowedLayerModes != null)
        {
            for (int i = 0; i < slot.allowedLayerModes.Count; i++)
            {
                TransportSlotLayerMode mode = slot.allowedLayerModes[i];
                if (mode.domain == passenger.GetDomain() && mode.heightLevel == passenger.GetHeightLevel())
                {
                    layerAllowed = true;
                    break;
                }
            }
        }
        if (!layerAllowed)
            return false;

        if (slot.allowedClasses != null && slot.allowedClasses.Count > 0 && !slot.allowedClasses.Contains(passengerData.unitClass))
            return false;

        if (slot.requiredSkills != null && slot.requiredSkills.Count > 0)
        {
            bool hasAny = false;
            for (int i = 0; i < slot.requiredSkills.Count; i++)
            {
                SkillData required = slot.requiredSkills[i];
                if (required != null && passenger.HasSkill(required))
                {
                    hasAny = true;
                    break;
                }
            }
            if (!hasAny)
                return false;
        }

        if (slot.blockedSkills != null)
        {
            for (int i = 0; i < slot.blockedSkills.Count; i++)
            {
                SkillData blocked = slot.blockedSkills[i];
                if (blocked != null && passenger.HasSkill(blocked))
                    return false;
            }
        }

        return true;
    }
}
