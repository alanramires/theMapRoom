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

        unit.TryGetUnitData(out UnitData transporterUnitData);
        int pickupRadius = transporterUnitData?.aiUnitProfile?.pickupZoneRadius ?? 2;

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
            && distToAnchor <= pickupRadius
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
                    if (distToAnchorCandidate == int.MaxValue || distToAnchorCandidate <= 0 || distToAnchorCandidate > pickupRadius)
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

    private static AIUnitProfile FindAnyTransporterProfile(AISnapshot snapshot)
    {
        if (snapshot?.FriendlyUnits == null) return null;
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager u = snapshot.FriendlyUnits[i];
            if (u == null || u.IsDead || !u.TryGetUnitData(out UnitData d) || d == null || !d.isTransporter)
                continue;
            if (d.aiUnitProfile != null)
                return d.aiUnitProfile;
        }
        return null;
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
        int distanceToTarget = GetHexDistance(snapshot.BoardTilemap, passengerCell, captureTarget, 64);
        int passengerMove = Mathf.Max(1, passenger.GetMovementRange());
        AIUnitProfile transporterProfile = FindAnyTransporterProfile(snapshot);
        int minTransportDist = transporterProfile?.minTransportDistanceHexes ?? 8;
        float worthwhileMult = transporterProfile?.transportWorthwhileMultiplier ?? 1.5f;
        int worthwhileThreshold = Mathf.CeilToInt(passengerMove * worthwhileMult);
        if (distanceToTarget == int.MaxValue || distanceToTarget < minTransportDist || distanceToTarget <= passengerMove || distanceToTarget <= worthwhileThreshold)
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

        if (transporter == null || snapshot == null)
            return false;

        bool hasPassenger = TryGetEmbarkedCapturePassenger(transporter, snapshot, out passenger, out passengerTargetCell, out objectiveLabel);
        if (!hasPassenger)
            hasPassenger = TryGetEmbarkedRoguePassenger(transporter, snapshot, out passenger, out passengerTargetCell, out objectiveLabel);
        if (!hasPassenger)
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

    private bool TryGetTransportRendezvousObjective(
        UnitManager transporter,
        AISnapshot snapshot,
        out Vector3Int rendezvousCell,
        out string objectiveLabel)
    {
        rendezvousCell = transporter != null ? transporter.CurrentCellPosition : Vector3Int.zero;
        objectiveLabel = string.Empty;

        if (transporter == null || snapshot == null || snapshot.ActivePlans == null)
            return false;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null || !transporterData.isTransporter)
            return false;

        int minTransportDist = transporterData.aiUnitProfile?.minTransportDistanceHexes ?? 8;
        float worthwhileMult = transporterData.aiUnitProfile?.transportWorthwhileMultiplier ?? 1.5f;

        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        UnitManager bestPassenger = null;
        AIPlanIntent bestIntent = null;
        Vector3Int bestCaptureTarget = Vector3Int.zero;
        int bestPassengerMove = 1;
        int bestScore = int.MinValue;

        for (int p = 0; p < snapshot.ActivePlans.Count; p++)
        {
            AIPlanIntent intent = snapshot.ActivePlans[p];
            if (intent == null || intent.DesiredTransportCount <= 0 || intent.Assignments == null)
                continue;

            for (int a = 0; a < intent.Assignments.Count; a++)
            {
                AIPlanAssignment assignment = intent.Assignments[a];
                if (assignment == null || assignment.Role != AIPlanRole.Capture)
                    continue;

                if (snapshot.UnitPlanAssignments == null ||
                    !snapshot.UnitPlanAssignments.TryGetValue(assignment.UnitInstanceId, out AIPlanAssignment _))
                    continue;

                UnitManager candidate = null;
                if (snapshot.FriendlyUnits != null)
                {
                    for (int u = 0; u < snapshot.FriendlyUnits.Count; u++)
                    {
                        if (snapshot.FriendlyUnits[u] != null && snapshot.FriendlyUnits[u].InstanceId == assignment.UnitInstanceId)
                        {
                            candidate = snapshot.FriendlyUnits[u];
                            break;
                        }
                    }
                }

                if (candidate == null || candidate.IsDead || candidate.IsEmbarked)
                    continue;
                if (!HasAvailableTransportSeatForPassenger(transporter, candidate))
                    continue;
                if (!candidate.TryGetUnitData(out UnitData candidateData) || candidateData == null || candidateData.domain != Domain.Land)
                    continue;

                Vector3Int captureTarget = assignment.HasPlannedCaptureTarget
                    ? assignment.PlannedCaptureCell
                    : (intent.HasCaptureTarget ? intent.CaptureTargetCell : candidate.CurrentCellPosition);
                captureTarget.z = 0;

                Vector3Int candidateCell = candidate.CurrentCellPosition;
                candidateCell.z = 0;
                int distanceToTarget = GetHexDistance(snapshot.BoardTilemap, candidateCell, captureTarget, 64);
                int passengerMove = Mathf.Max(1, candidate.GetMovementRange());
                int worthwhileThreshold = Mathf.CeilToInt(passengerMove * worthwhileMult);
                if (distanceToTarget == int.MaxValue || distanceToTarget < minTransportDist || distanceToTarget <= passengerMove || distanceToTarget <= worthwhileThreshold)
                    continue;

                // Prioriza quem esta mais longe do objetivo (maior necessidade de transporte),
                // com distancia ao APC como desempate (prefere o mais proximo).
                int score = distanceToTarget * 1000 - GetHexDistance(transporterCell, candidateCell);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPassenger = candidate;
                    bestIntent = intent;
                    bestCaptureTarget = captureTarget;
                    bestPassengerMove = passengerMove;
                }
            }
        }

        if (bestPassenger == null)
            return false;

        Vector3Int passengerCell = bestPassenger.CurrentCellPosition;
        passengerCell.z = 0;

        // Posiciona o APC 1 hex à frente do capturer na direção do objetivo.
        // searchDepth=1 garante que o capturer ainda tem movimento sobrando para embarcar.
        if (!TryGetTransportInterceptCell(transporter, snapshot, passengerCell, bestCaptureTarget, 1, out rendezvousCell))
            rendezvousCell = passengerCell;

        objectiveLabel = bestIntent != null && !string.IsNullOrWhiteSpace(bestIntent.DisplayName)
            ? bestIntent.DisplayName
            : "captura distante";
        return true;
    }

    private bool TryGetTransportForwardStagingCell(UnitManager transporter, AISnapshot snapshot, out Vector3Int stagingCell)
    {
        stagingCell = default;
        if (transporter == null || snapshot?.ActivePlans == null)
            return false;

        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        // Sombra o capturer que genuinamente precisa de transporte (dist > worthwhileThreshold),
        // priorizando quem esta mais longe do objetivo (maior necessidade), depois o mais proximo do APC.
        AIUnitProfile transporterProfile = FindAnyTransporterProfile(snapshot);
        float worthwhileMult = transporterProfile?.transportWorthwhileMultiplier ?? 1.5f;

        UnitManager nearestCapturer = null;
        Vector3Int nearestCapturerTarget = Vector3Int.zero;
        int nearestCapturerMove = 1;
        int bestScore = int.MinValue;

        for (int p = 0; p < snapshot.ActivePlans.Count; p++)
        {
            AIPlanIntent intent = snapshot.ActivePlans[p];
            if (intent == null || intent.Assignments == null)
                continue;

            for (int a = 0; a < intent.Assignments.Count; a++)
            {
                AIPlanAssignment assignment = intent.Assignments[a];
                if (assignment == null || assignment.Role != AIPlanRole.Capture)
                    continue;

                UnitManager candidate = null;
                if (snapshot.FriendlyUnits != null)
                {
                    for (int u = 0; u < snapshot.FriendlyUnits.Count; u++)
                    {
                        if (snapshot.FriendlyUnits[u] != null && snapshot.FriendlyUnits[u].InstanceId == assignment.UnitInstanceId)
                        {
                            candidate = snapshot.FriendlyUnits[u];
                            break;
                        }
                    }
                }

                if (candidate == null || candidate.IsDead || candidate.IsEmbarked)
                    continue;
                if (!HasAvailableTransportSeatForPassenger(transporter, candidate))
                    continue;

                Vector3Int captureTarget = assignment.HasPlannedCaptureTarget
                    ? assignment.PlannedCaptureCell
                    : (intent.HasCaptureTarget ? intent.CaptureTargetCell : candidate.CurrentCellPosition);
                captureTarget.z = 0;

                Vector3Int candidateCell = candidate.CurrentCellPosition;
                candidateCell.z = 0;
                int passengerMove = Mathf.Max(1, candidate.GetMovementRange());
                int worthwhileThreshold = Mathf.CeilToInt(passengerMove * worthwhileMult);
                int distToObjective = GetHexDistance(snapshot.BoardTilemap, candidateCell, captureTarget, 64);

                // Só sombra capturers que estao genuinamente longe do objetivo.
                if (distToObjective == int.MaxValue || distToObjective <= worthwhileThreshold)
                    continue;

                // Prioriza: maior distancia ao objetivo (maior necessidade), depois menor distancia ao APC.
                int distToApc = GetHexDistance(transporterCell, candidateCell);
                int score = distToObjective * 1000 - distToApc;
                if (score > bestScore)
                {
                    bestScore = score;
                    nearestCapturer = candidate;
                    nearestCapturerTarget = captureTarget;
                    nearestCapturerMove = passengerMove;
                }
            }
        }

        if (nearestCapturer == null)
            return false;

        Vector3Int capturerCell = nearestCapturer.CurrentCellPosition;
        capturerCell.z = 0;

        // Posiciona o APC 1 hex à frente do capturer na direção do objetivo.
        if (!TryGetTransportInterceptCell(transporter, snapshot, capturerCell, nearestCapturerTarget, 1, out stagingCell))
            stagingCell = capturerCell;

        return true;
    }

    // Encontra a célula de interceptação ideal: BFS ao redor da posição do capturer (até passengerMove passos),
    // retornando a célula mais próxima ao objetivo de captura que o APC consegue ocupar.
    // Isso posiciona o APC ENTRE o capturer e o destino dele, não atrás.
    private bool TryGetTransportInterceptCell(
        UnitManager transporter,
        AISnapshot snapshot,
        Vector3Int capturerCell,
        Vector3Int captureTarget,
        int searchDepth,
        out Vector3Int interceptCell)
    {
        interceptCell = capturerCell;
        if (transporter == null || snapshot?.BoardTilemap == null || searchDepth <= 0)
            return false;

        capturerCell.z = 0;
        captureTarget.z = 0;

        TerrainDatabase terrainDb = turnStateManager?.TerrainDatabaseRef;
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        List<Vector3Int> frontier = new List<Vector3Int> { capturerCell };
        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { capturerCell };

        bool found = false;
        int bestDistToTarget = int.MaxValue;
        int bestEnterCost = int.MaxValue;

        for (int depth = 0; depth < searchDepth; depth++)
        {
            List<Vector3Int> nextFrontier = new List<Vector3Int>();
            for (int fi = 0; fi < frontier.Count; fi++)
            {
                UnitMovementPathRules.GetImmediateHexNeighbors(snapshot.BoardTilemap, frontier[fi], neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Vector3Int candidate = neighbors[i];
                    candidate.z = 0;
                    if (!visited.Add(candidate))
                        continue;

                    nextFrontier.Add(candidate);

                    if (IsAnyConstructionCell(snapshot, candidate))
                        continue;
                    if (IsCellOccupiedBySnapshotUnit(snapshot, transporter, null, candidate))
                        continue;
                    if (!CanAiUnitEndMoveAtCell(transporter, snapshot.BoardTilemap, candidate))
                        continue;
                    if (!UnitMovementPathRules.TryGetEnterCellCost(
                            snapshot.BoardTilemap, transporter, candidate, terrainDb,
                            applyOperationalAutonomyModifier: false, out int enterCost))
                        continue;

                    int distToTarget = GetHexDistance(candidate, captureTarget);
                    if (distToTarget == int.MaxValue)
                        continue;

                    bool better = !found
                        || distToTarget < bestDistToTarget
                        || (distToTarget == bestDistToTarget && enterCost < bestEnterCost);
                    if (!better)
                        continue;

                    found = true;
                    bestDistToTarget = distToTarget;
                    bestEnterCost = enterCost;
                    interceptCell = candidate;
                }
            }

            frontier = nextFrontier;
            if (frontier.Count == 0)
                break;
        }

        return found;
    }

    private bool TryGetTransportStagingCellNearObjective(UnitManager transporter, AISnapshot snapshot, Vector3Int objectiveCell, out Vector3Int stagingCell)
    {
        stagingCell = objectiveCell;
        if (transporter == null || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        objectiveCell.z = 0;
        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        // Penalidade de hot zone: hexes com inimigos visíveis nas adjacências têm custo aumentado.
        // O APC prefere rotas mais longas porém seguras; se todas forem hot zone, segue pela melhor disponível.
        const int dangerPenalty = 1000; // equivale a 1 hex extra de distância ao objetivo
        HashSet<Vector3Int> dangerCells = BuildEnemyDangerCells(snapshot, 1);

        bool found = false;
        int bestScore = int.MinValue;

        List<Vector3Int> frontier = new List<Vector3Int> { objectiveCell };
        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { objectiveCell };

        for (int depth = 0; depth < 2; depth++)
        {
            List<Vector3Int> nextFrontier = new List<Vector3Int>();
            for (int fi = 0; fi < frontier.Count; fi++)
            {
                UnitMovementPathRules.GetImmediateHexNeighbors(snapshot.BoardTilemap, frontier[fi], neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Vector3Int candidate = neighbors[i];
                    candidate.z = 0;
                    if (!visited.Add(candidate))
                        continue;

                    nextFrontier.Add(candidate);

                    if (IsAnyConstructionCell(snapshot, candidate))
                        continue;
                    if (IsCellOccupiedBySnapshotUnit(snapshot, transporter, null, candidate))
                        continue;
                    if (!CanAiUnitEndMoveAtCell(transporter, snapshot.BoardTilemap, candidate))
                        continue;

                    int distToTransporter = GetHexDistance(snapshot.BoardTilemap, transporterCell, candidate, 64);
                    if (distToTransporter == int.MaxValue)
                        continue;

                    int distToObjective = GetHexDistance(snapshot.BoardTilemap, objectiveCell, candidate, 8);
                    if (distToObjective == int.MaxValue)
                        continue;

                    if (!UnitMovementPathRules.TryGetEnterCellCost(
                            snapshot.BoardTilemap,
                            transporter,
                            candidate,
                            terrainDb,
                            applyOperationalAutonomyModifier: false,
                            out int enterCost))
                        continue;

                    // Score combinado: prioridade (1) distância ao objetivo, (2) distância ao transporter,
                    // (3) custo de terreno, (4) penalidade de hot zone (inimigo adjacente).
                    bool inDanger = dangerCells != null && dangerCells.Contains(candidate);
                    int score = -distToObjective * 1000
                                - distToTransporter * 10
                                - enterCost
                                - (inDanger ? dangerPenalty : 0);

                    if (found && score <= bestScore)
                        continue;

                    found = true;
                    bestScore = score;
                    stagingCell = candidate;
                }
            }

            frontier = nextFrontier;
            if (found || frontier.Count <= 0)
                break;
        }

        return found;
    }

    // -------------------------------------------------------------------------
    // Rogue transport (táxi de reforços)
    // -------------------------------------------------------------------------

    private bool TryGetNearestEnemyHqCell(AISnapshot snapshot, Vector3Int fromCell, out Vector3Int enemyHqCell)
    {
        enemyHqCell = Vector3Int.zero;
        if (snapshot?.EnemyHqs == null || snapshot.EnemyHqs.Count == 0)
            return false;

        fromCell.z = 0;
        bool found = false;
        int bestDist = int.MaxValue;

        for (int i = 0; i < snapshot.EnemyHqs.Count; i++)
        {
            if (snapshot.EnemyHqs[i] == null) continue;
            Vector3Int hqCell = snapshot.EnemyHqs[i].Cell;
            hqCell.z = 0;
            int dist = GetHexDistance(snapshot.BoardTilemap, fromCell, hqCell, 64);
            if (dist == int.MaxValue) dist = GetHexDistance(fromCell, hqCell);
            if (!found || dist < bestDist)
            {
                found = true;
                bestDist = dist;
                enemyHqCell = hqCell;
            }
        }
        return found;
    }

    private static bool TryGetMostForwardFriendlyConstruction(AISnapshot snapshot, Vector3Int enemyHqCell, out Vector3Int constructionCell)
    {
        constructionCell = Vector3Int.zero;
        if (snapshot?.KnownConstructions == null)
            return false;

        enemyHqCell.z = 0;
        bool found = false;
        int bestDist = int.MaxValue;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Source == null)
                continue;
            if (info.TeamId != snapshot.AiTeam)
                continue;

            Vector3Int cell = info.Cell;
            cell.z = 0;
            int dist = GetHexDistance(cell, enemyHqCell);
            if (!found || dist < bestDist)
            {
                found = true;
                bestDist = dist;
                constructionCell = cell;
            }
        }
        return found;
    }

    // Carry side: verifica se há passageiro rogue embarcado no transporter.
    // Retorna o drop-off na construção amiga mais avançada (mais próxima do HQ inimigo).
    private bool TryGetEmbarkedRoguePassenger(
        UnitManager transporter,
        AISnapshot snapshot,
        out UnitManager passenger,
        out Vector3Int dropOffCell,
        out string objectiveLabel)
    {
        passenger = null;
        dropOffCell = transporter != null ? transporter.CurrentCellPosition : Vector3Int.zero;
        objectiveLabel = string.Empty;

        if (transporter == null || snapshot == null)
            return false;

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null)
            return false;

        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            UnitManager seatPassenger = seat != null ? seat.embarkedUnit : null;
            if (seatPassenger == null || !seatPassenger.IsEmbarked)
                continue;

            // Rogue = sem atribuição de plano
            if (snapshot.UnitPlanAssignments != null
                && snapshot.UnitPlanAssignments.TryGetValue(seatPassenger.InstanceId, out AIPlanAssignment assignment)
                && assignment != null)
                continue;

            if (!TryGetNearestEnemyHqCell(snapshot, transporterCell, out Vector3Int enemyHqCell))
                continue;

            if (!TryGetMostForwardFriendlyConstruction(snapshot, enemyHqCell, out dropOffCell))
                continue;

            passenger = seatPassenger;
            objectiveLabel = "reforco";
            return true;
        }
        return false;
    }

    // Transporter side: APC procura rogues e posiciona 1 hex à frente deles na direção do HQ inimigo.
    private bool TryGetTransportRoguePickupObjective(
        UnitManager transporter,
        AISnapshot snapshot,
        out Vector3Int rendezvousCell,
        out string objectiveLabel)
    {
        rendezvousCell = transporter != null ? transporter.CurrentCellPosition : Vector3Int.zero;
        objectiveLabel = string.Empty;

        if (transporter == null || snapshot?.FriendlyUnits == null)
            return false;

        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        UnitManager bestRogue = null;
        Vector3Int bestEnemyHq = Vector3Int.zero;
        int bestScore = int.MinValue;

        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager candidate = snapshot.FriendlyUnits[i];
            if (candidate == null || candidate.IsDead || candidate.IsEmbarked)
                continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData) || candidateData == null)
                continue;
            if (candidateData.domain != Domain.Land || candidateData.isTransporter)
                continue;

            // Só rogues (sem atribuição de plano)
            if (snapshot.UnitPlanAssignments != null
                && snapshot.UnitPlanAssignments.TryGetValue(candidate.InstanceId, out AIPlanAssignment assignment)
                && assignment != null)
                continue;

            if (!HasAvailableTransportSeatForPassenger(transporter, candidate))
                continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition;
            candidateCell.z = 0;

            if (!TryGetNearestEnemyHqCell(snapshot, candidateCell, out Vector3Int enemyHqCell))
                continue;

            // Prefere rogues mais próximos do APC (mais fáceis de buscar)
            int distToApc = GetHexDistance(transporterCell, candidateCell);
            int score = -distToApc;
            if (score > bestScore)
            {
                bestScore = score;
                bestRogue = candidate;
                bestEnemyHq = enemyHqCell;
            }
        }

        if (bestRogue == null)
            return false;

        Vector3Int rogueCell = bestRogue.CurrentCellPosition;
        rogueCell.z = 0;

        // Posiciona 1 hex à frente do rogue na direção do HQ inimigo
        if (!TryGetTransportInterceptCell(transporter, snapshot, rogueCell, bestEnemyHq, 1, out rendezvousCell))
            rendezvousCell = rogueCell;

        objectiveLabel = "reforco";
        return true;
    }

    // Passenger side (rogue): embarca se APC adjacente, usa HQ inimigo como direção efetiva.
    private bool TryGetTransportPickupObjectiveForRogue(
        UnitManager passenger,
        AISnapshot snapshot,
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

        // Só rogues
        if (snapshot.UnitPlanAssignments != null
            && snapshot.UnitPlanAssignments.TryGetValue(passenger.InstanceId, out AIPlanAssignment assignment)
            && assignment != null)
            return false;

        if (!passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null)
            return false;
        if (passengerData.domain != Domain.Land || passengerData.isTransporter)
            return false;

        Vector3Int passengerCell = passenger.CurrentCellPosition;
        passengerCell.z = 0;

        if (!TryGetNearestEnemyHqCell(snapshot, passengerCell, out Vector3Int enemyHqCell))
            return false;

        // Tenta embarcar imediatamente se APC está adjacente
        if (TryGetBestEmbarkTargetNow(passenger, snapshot, null, enemyHqCell, out transporter))
        {
            pickupObjectiveCell = passengerCell;
            embarkNow = true;
            objectiveLabel = "reforco";
            return true;
        }

        // Procura o melhor APC que venha buscar este rogue
        transporter = FindBestRogueTransporter(passenger, snapshot);
        if (transporter == null)
            return false;

        pickupObjectiveCell = transporter.CurrentCellPosition;
        pickupObjectiveCell.z = 0;
        embarkNow = GetHexDistance(passengerCell, pickupObjectiveCell) <= 1;
        objectiveLabel = "reforco";
        return true;
    }

    private UnitManager FindBestRogueTransporter(UnitManager passenger, AISnapshot snapshot)
    {
        if (passenger == null || snapshot?.FriendlyUnits == null)
            return null;

        Vector3Int passengerCell = passenger.CurrentCellPosition;
        passengerCell.z = 0;

        UnitManager best = null;
        int bestScore = int.MinValue;

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
            int score = -GetHexDistance(candidateCell, passengerCell) * 100;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
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
