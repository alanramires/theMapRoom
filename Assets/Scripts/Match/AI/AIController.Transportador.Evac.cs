using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // Slightly wider than ShuttlePickupRange — repair units can't always walk to the APC.
    private const int EvacPickupRange = 3;
    private const float RepairEvacRouteThreshold = 6f;
    private const float RepairEvacTurnThreshold = 2f;

    // -------------------------------------------------------------------------
    // EVAC Shuttle — empty APC positions next to a frontline unit-under-repair
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideEvacShuttleAction(
        UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan,
        Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;

        UnitManager evacuee = FindBestEvacCandidate(unit, snapshot, fromCell, paths);
        if (evacuee == null) return null;

        Vector3Int evacueeCell = evacuee.CurrentCellPosition; evacueeCell.z = 0;
        ConstructionManager repairDest = FindRepairConstruction(evacuee, evacueeCell, snapshot.AITeam, BuildOccupied(evacuee));
        Vector3Int objective = repairDest != null ? repairDest.CurrentCellPosition : FindTransportWaitTarget(snapshot.AITeam, evacueeCell);
        objective.z = 0;
        HashSet<Vector3Int> passengerReachable = BuildPassengerReachableSet(evacuee);
        Vector3Int moveTarget = FindTransportShuttleMove(
            unit, fromCell, evacueeCell, paths, occupied, snapshot.AITeam,
            objective, passengerReachable, plan, null, paths);
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} EVAC shuttle — resgata #{evacuee.InstanceId}@{evacueeCell} via {moveTarget}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    private PlayerAction TryDecideAirEvacShuttleAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildAirOccupied(unit);

        if (paths == null || paths.Count == 0)
            return null;

        return TryDecideEvacShuttleAction(unit, snapshot, plan, paths, occupied);
    }

    private UnitManager FindBestEvacCandidate(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int transporterCell,
        Dictionary<Vector3Int, List<Vector3Int>> transporterPaths)
    {
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
            return null;

        bool airTransport = IsAirTransporter(transporter);
        UnitManager best = null;
        float bestScore = float.MinValue;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == transporter) continue;
            if (candidate.TeamId != snapshot.AITeam || candidate.IsDead || candidate.IsEmbarked) continue;
            if (!candidate.IsUnderRepair) continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData)) continue;
            if (candidateData.domain == Domain.Air) continue;
            if (FindFittingSlotIndex(transporter, transporterData, candidate, candidateData) < 0) continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition; candidateCell.z = 0;
            if (!ShouldPreferRepairEvac(candidate, candidateCell, snapshot.AITeam, out float routeDist, out float turnEstimate, out bool danger))
                continue;

            float transportDist = SectorManager.HexDistance(transporterCell, candidateCell);
            if (airTransport)
            {
                if (transportDist > Mathf.Max(EvacPickupRange + transporter.MaxMovementPoints, 10)) continue;
            }
            else
            {
                // Only consider candidates reachable within a reasonable horizon.
                if (transportDist > EvacPickupRange + transporter.MaxMovementPoints) continue;
            }

            float pickupReachBonus = IsPassengerInPickupRange(transporterCell, candidateCell, EvacPickupRange, BuildPassengerReachableSet(candidate)) ? 500f : 0f;
            float pathReachBonus = transporterPaths != null && transporterPaths.ContainsKey(candidateCell) ? 150f : 0f;
            float dangerBonus = danger ? 600f : 0f;
            // Prefer nearby + most damaged + worst walking route.
            float score = routeDist * 35f + turnEstimate * 220f - transportDist * 45f
                + (20f - candidate.CurrentHP) * 80f + pickupReachBonus + pathReachBonus + dangerBonus;
            if (score > bestScore) { bestScore = score; best = candidate; }
        }

        return best;
    }

    // -------------------------------------------------------------------------
    // EVAC Courier — delivers IsUnderRepair passenger to nearest safe repair spot
    // -------------------------------------------------------------------------

    private PlayerAction DecideEvacCourierAction(
        UnitManager unit, UnitManager evacuee, List<UnitManager> passengers,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        // Reuse the same destination logic as the repair system
        ConstructionManager repairDest = FindRepairConstruction(evacuee, fromCell, snapshot.AITeam, new HashSet<Vector3Int>(occupied));
        Vector3Int target = repairDest != null ? repairDest.CurrentCellPosition : fromCell;
        target.z = 0;

        float distToTarget = SectorManager.HexDistance(fromCell, target);
        bool airTransport = IsAirTransporter(unit);
        Vector3Int moveTarget = airTransport
            ? FindAirTransportMove(fromCell, target, paths, occupied, snapshot.AITeam)
            : FindTransportMove(unit, fromCell, target, paths, occupied, snapshot.AITeam);
        float moveImprovement = distToTarget - SectorManager.HexDistance(moveTarget, target);

        int dropOffRange = airTransport ? AirDropOffRange : TransportDropOffRange;

        // Priority 1: move + disembark when making progress
        if (moveTarget != fromCell && moveImprovement > 0f)
        {
            List<PodeDesembarcarOption> opts = SimulateDisembarkFromCell(unit, moveTarget);
            if (opts != null && opts.Count > 0)
            {
                PodeDesembarcarOption primaryOpt = PickEvacDisembarkOption(opts, evacuee, target);
                if (primaryOpt != null)
                {
                    Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                    if (SectorManager.HexDistance(dc, target) <= dropOffRange
                        && IsEvacDropCellSafe(dc, snapshot.AITeam))
                    {
                        List<PodeDesembarcarOption> selected = SelectEvacDisembarkForPassenger(opts, evacuee, target, snapshot.AITeam);
                        if (selected.Count > 0)
                        {
                            paths.TryGetValue(moveTarget, out List<Vector3Int> movePath);
                            Debug.Log($"{TL("Transporte")} {unit.InstanceId} EVAC courier — move+desembarca #{evacuee.InstanceId} via {moveTarget} @ {dc} destino={target}");
                            return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected, moveTarget, movePath);
                        }
                    }
                }
            }
        }

        // Priority 2: disembark in place when near destination or stuck
        bool isStuck = moveTarget == fromCell;
        var disembarkOpts = new List<PodeDesembarcarOption>();
        if (PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, disembarkOpts)
            && disembarkOpts.Count > 0 && (moveImprovement <= 1f || isStuck))
        {
            PodeDesembarcarOption primaryOpt = PickEvacDisembarkOption(disembarkOpts, evacuee, target);
            if (primaryOpt != null)
            {
                Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                if (SectorManager.HexDistance(dc, target) <= dropOffRange
                    && IsEvacDropCellSafe(dc, snapshot.AITeam))
                {
                    List<PodeDesembarcarOption> selected = SelectEvacDisembarkForPassenger(disembarkOpts, evacuee, target, snapshot.AITeam);
                    if (selected.Count == 0) return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
                    Debug.Log($"{TL("Transporte")} {unit.InstanceId} EVAC courier — desembarca #{evacuee.InstanceId} @ {dc} destino={target}");
                    return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                }
            }
        }

        // Priority 3: keep moving toward repair destination
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} EVAC courier — move para {moveTarget} destino={target} dist={SectorManager.HexDistance(moveTarget, target):F0}h");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    private List<PodeDesembarcarOption> SelectEvacDisembarkForPassenger(
        List<PodeDesembarcarOption> options,
        UnitManager evacuee,
        Vector3Int repairTarget,
        TeamId aiTeam)
    {
        var selected = new List<PodeDesembarcarOption>(1);
        PodeDesembarcarOption best = null;
        float bestDist = float.MaxValue;
        float bestThreat = float.MaxValue;

        foreach (PodeDesembarcarOption opt in options)
        {
            if (opt == null || opt.passengerUnit != evacuee) continue;
            Vector3Int dc = opt.disembarkCell; dc.z = 0;
            if (!IsEvacDropCellSafe(dc, aiTeam)) continue;

            float dist = SectorManager.HexDistance(dc, repairTarget);
            float threat = CalculateThreatLevel(dc, aiTeam);
            bool isBetter = best == null
                || dist < bestDist - 0.1f
                || (dist < bestDist + 0.1f && threat < bestThreat - 0.001f);
            if (!isBetter) continue;

            best = opt;
            bestDist = dist;
            bestThreat = threat;
        }

        if (best != null) selected.Add(best);
        return selected;
    }

    private bool IsEvacDropCellSafe(Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;
        return !HasEnemyUnitAnyLayerAtCell(cell, aiTeam)
            && !HasNearbyVisibleEnemy(cell, aiTeam, DefenseEnemyRange);
    }

    // Picks the disembark cell for the evacuee closest to the repair destination.
    private static PodeDesembarcarOption PickEvacDisembarkOption(
        List<PodeDesembarcarOption> opts, UnitManager evacuee, Vector3Int repairTarget)
    {
        PodeDesembarcarOption best = null;
        float bestDist = float.MaxValue;
        foreach (PodeDesembarcarOption opt in opts)
        {
            if (opt.passengerUnit != evacuee) continue;
            Vector3Int dc = opt.disembarkCell; dc.z = 0;
            float dist = SectorManager.HexDistance(dc, repairTarget);
            if (dist < bestDist) { bestDist = dist; best = opt; }
        }
        return best;
    }

    // -------------------------------------------------------------------------
    // Repair unit side — boards a nearby empty transporter when in danger
    // -------------------------------------------------------------------------

    private PlayerAction TryEvacEmbarkAction(UnitManager unit, TeamId aiTeam, Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        var embarkOpts = new List<PodeEmbarcarOption>();
        PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
            Mathf.Max(0, unit.RemainingMovementPoints), embarkOpts);

        foreach (PodeEmbarcarOption opt in embarkOpts)
        {
            if (opt.transporterUnit == null || opt.transporterUnit.TeamId != aiTeam) continue;
            if (opt.transporterUnit.IsDead || opt.transporterUnit.IsUnderRepair) continue;
            if (HasTransportCargo(opt.transporterUnit)) continue; // already carrying someone

            Debug.Log($"{TL("Repair")} {unit.InstanceId} EVAC — embarca em transporter #{opt.transporterUnit.InstanceId}");
            return BuildEmbarcarBatch(unit, aiTeam, fromCell, opt.transporterUnit, opt.transporterSlotIndex, paths);
        }

        return null;
    }

    private PlayerAction TryEvacEmbarkOrApproachAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        PlayerAction direct = TryEvacEmbarkAction(unit, aiTeam, fromCell, paths);
        if (direct != null)
            return direct;

        UnitManager transporter = FindBestRepairEvacTransporterForPassenger(unit, aiTeam, fromCell);
        if (transporter == null)
            return null;

        if (TryBuildRepairEvacExtendedEmbarkAction(unit, snapshot, plan, aiTeam, fromCell, transporter, paths, out PlayerAction extendedEmbark))
            return extendedEmbark;

        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        Vector3Int best = fromCell;
        float fromDist = SectorManager.HexDistance(fromCell, transporterCell);
        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            float dist = SectorManager.HexDistance(cell, transporterCell);
            float progress = fromDist - dist;
            float threat = CalculateThreatLevel(cell, aiTeam);
            float score = progress * 500f - dist * 80f - threat * ThreatWeight;
            if (IsPassengerInPickupRange(cell, transporterCell, EvacPickupRange, null))
                score += 800f;
            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        if (best == fromCell)
            return null;

        Debug.Log($"{TL("Repair")} {unit.InstanceId} EVAC — aproxima pickup transporter #{transporter.InstanceId}@{transporterCell} via {best}");
        return BuildMoveBatch(unit, aiTeam, fromCell, best, paths);
    }

    private bool TryBuildRepairEvacExtendedEmbarkAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        Vector3Int fromCell,
        UnitManager transporter,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || transporter == null || snapshot == null)
            return false;
        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null)
            return false;

        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        var stopCandidates = new List<Vector3Int>();
        var seen = new HashSet<Vector3Int>();
        if (seen.Add(fromCell))
            stopCandidates.Add(fromCell);

        if (paths != null)
        {
            foreach (Vector3Int raw in paths.Keys)
            {
                Vector3Int cell = raw;
                cell.z = 0;
                if (seen.Add(cell))
                    stopCandidates.Add(cell);
            }
        }

        var neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, transporterCell, neighbors);
        foreach (Vector3Int raw in neighbors)
        {
            Vector3Int cell = raw;
            cell.z = 0;
            if (seen.Add(cell))
                stopCandidates.Add(cell);
        }

        stopCandidates.Sort((a, b) =>
        {
            float aDistToTransport = SectorManager.HexDistance(a, transporterCell);
            float bDistToTransport = SectorManager.HexDistance(b, transporterCell);
            bool aCanEmbark = aDistToTransport <= 1.5f;
            bool bCanEmbark = bDistToTransport <= 1.5f;
            if (aCanEmbark != bCanEmbark)
                return aCanEmbark ? -1 : 1;

            int cmp = SectorManager.HexDistance(fromCell, a).CompareTo(SectorManager.HexDistance(fromCell, b));
            if (cmp != 0) return cmp;
            return aDistToTransport.CompareTo(bDistToTransport);
        });

        foreach (Vector3Int stopCell in stopCandidates)
        {
            if (stopCell == fromCell)
            {
                float directPickupDist = SectorManager.HexDistance(fromCell, transporterCell);
                if (directPickupDist <= 1.5f
                    && TryEmbarkFromHex(fromCell, null, unit.RemainingMovementPoints,
                            transporterCell, unit, unitData, plan, null, snapshot, out action,
                            requireSectorMatch: false, allowOverflow: true, requireFormalPassenger: false,
                            expectedTransporter: transporter))
                    return true;
                continue;
            }

            if (SectorManager.HexDistance(stopCell, transporterCell) > 1.5f)
                continue;

            if (!TryGetEmbarkStopPath(unit, fromCell, stopCell, paths, out List<Vector3Int> pathToStop))
                continue;

            int remainingMPAtStop = CalculateRemainingMovementAfterPath(unit, pathToStop);
            if (remainingMPAtStop <= 0)
                continue;

            if (TryEmbarkFromHex(stopCell, pathToStop, remainingMPAtStop,
                    transporterCell, unit, unitData, plan, null, snapshot, out action,
                    requireSectorMatch: false, allowOverflow: true, requireFormalPassenger: false,
                    expectedTransporter: transporter))
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} EVAC — move+embarca em transporter #{transporter.InstanceId} via {stopCell}");
                return true;
            }
        }

        return false;
    }

    private UnitManager FindBestRepairEvacTransporterForPassenger(UnitManager passenger, TeamId aiTeam, Vector3Int passengerCell)
    {
        if (passenger == null || !passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null)
            return null;

        UnitManager best = null;
        float bestScore = float.MinValue;
        foreach (UnitManager transporter in UnitManager.AllActive)
        {
            if (transporter == null || transporter.TeamId != aiTeam) continue;
            if (transporter == passenger || transporter.IsDead || transporter.IsEmbarked || transporter.IsUnderRepair) continue;
            if (HasTransportCargo(transporter)) continue;
            if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null || !transporterData.isTransporter) continue;
            if (!IsAirTransporter(transporter)) continue;
            if (FindFittingSlotIndex(transporter, transporterData, passenger, passengerData) < 0) continue;

            Vector3Int transporterCell = transporter.CurrentCellPosition;
            transporterCell.z = 0;
            float dist = SectorManager.HexDistance(passengerCell, transporterCell);
            if (dist > Mathf.Max(EvacPickupRange + transporter.MaxMovementPoints, 10)) continue;

            float actedPenalty = transporter.HasActed ? 250f : 0f;
            float score = -dist * 100f - actedPenalty + (transporter.RemainingMovementPoints * 10f);
            if (score > bestScore)
            {
                bestScore = score;
                best = transporter;
            }
        }
        return best;
    }

    private bool ShouldPreferRepairEvac(UnitManager unit, Vector3Int fromCell, TeamId aiTeam)
    {
        return ShouldPreferRepairEvac(unit, fromCell, aiTeam, out _, out _, out _);
    }

    private bool ShouldPreferRepairEvac(
        UnitManager unit,
        Vector3Int fromCell,
        TeamId aiTeam,
        out float routeDist,
        out float turnEstimate,
        out bool danger)
    {
        routeDist = 0f;
        turnEstimate = 0f;
        danger = false;
        if (unit == null || unit.GetDomain() == Domain.Air)
            return false;

        ConstructionManager repairDest = FindRepairConstruction(unit, fromCell, aiTeam, BuildOccupied(unit));
        if (repairDest == null)
            return false;

        Vector3Int destCell = repairDest.CurrentCellPosition;
        destCell.z = 0;
        routeDist = CalculateRouteDistanceOrHex(unit, fromCell, destCell);
        int maxMove = Mathf.Max(1, unit.MaxMovementPoints);
        turnEstimate = routeDist / maxMove;
        danger = HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange);

        return danger || routeDist >= RepairEvacRouteThreshold || turnEstimate >= RepairEvacTurnThreshold;
    }
}
