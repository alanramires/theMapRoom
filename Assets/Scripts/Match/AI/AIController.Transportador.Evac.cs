using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // Slightly wider than ShuttlePickupRange — repair units can't always walk to the APC.
    private const int EvacPickupRange = 3;

    // -------------------------------------------------------------------------
    // EVAC Shuttle — empty APC positions next to a frontline unit-under-repair
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideEvacShuttleAction(
        UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan,
        Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;

        UnitManager evacuee = FindBestEvacCandidate(unit, snapshot, fromCell);
        if (evacuee == null) return null;

        Vector3Int evacueeCell = evacuee.CurrentCellPosition; evacueeCell.z = 0;
        Vector3Int moveTarget = FindTransportShuttleMove(unit, fromCell, evacueeCell, paths, occupied, snapshot.AITeam);
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} EVAC shuttle — resgata #{evacuee.InstanceId}@{evacueeCell} via {moveTarget}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    private UnitManager FindBestEvacCandidate(UnitManager transporter, AIWorldSnapshot snapshot, Vector3Int transporterCell)
    {
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
            return null;

        UnitManager best = null;
        float bestScore = float.MinValue;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == transporter) continue;
            if (candidate.TeamId != snapshot.AITeam || candidate.IsDead || candidate.IsEmbarked) continue;
            if (!candidate.IsUnderRepair) continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData)) continue;
            if (FindFittingSlotIndex(transporter, transporterData, candidate, candidateData) < 0) continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition; candidateCell.z = 0;
            if (!HasNearbyVisibleEnemy(candidateCell, snapshot.AITeam, DefenseEnemyRange)) continue;

            float transportDist = SectorManager.HexDistance(transporterCell, candidateCell);
            // Only consider candidates reachable within a reasonable horizon
            if (transportDist > EvacPickupRange + transporter.MaxMovementPoints) continue;

            // Prefer nearby + most damaged (highest urgency)
            float score = -transportDist * 50f + (20f - candidate.CurrentHP) * 80f;
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
        ConstructionManager repairDest = FindRepairConstruction(fromCell, snapshot.AITeam, new HashSet<Vector3Int>(occupied));
        Vector3Int target = repairDest != null ? repairDest.CurrentCellPosition : fromCell;
        target.z = 0;

        float distToTarget = SectorManager.HexDistance(fromCell, target);
        Vector3Int moveTarget = FindTransportMove(unit, fromCell, target, paths, occupied, snapshot.AITeam);
        float moveImprovement = distToTarget - SectorManager.HexDistance(moveTarget, target);

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
                    if (SectorManager.HexDistance(dc, target) <= TransportDropOffRange
                        || SectorManager.HexDistance(moveTarget, target) <= TransportDropOffRange)
                    {
                        List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(opts, passengers, null, snapshot);
                        paths.TryGetValue(moveTarget, out List<Vector3Int> movePath);
                        Debug.Log($"{TL("Transporte")} {unit.InstanceId} EVAC courier — move+desembarca #{evacuee.InstanceId} via {moveTarget} @ {dc} destino={target}");
                        return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected, moveTarget, movePath);
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
                if (isStuck || SectorManager.HexDistance(dc, target) <= TransportDropOffRange
                           || distToTarget <= TransportDropOffRange)
                {
                    List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(disembarkOpts, passengers, null, snapshot);
                    Debug.Log($"{TL("Transporte")} {unit.InstanceId} EVAC courier — desembarca #{evacuee.InstanceId} @ {dc} destino={target}");
                    return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                }
            }
        }

        // Priority 3: keep moving toward repair destination
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} EVAC courier — move para {moveTarget} destino={target} dist={SectorManager.HexDistance(moveTarget, target):F0}h");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
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
}
