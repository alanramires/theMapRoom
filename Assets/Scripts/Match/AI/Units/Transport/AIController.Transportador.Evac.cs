using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // Slightly wider than ShuttlePickupRange — repair units can't always walk to the APC.
    private const int EvacPickupRange = 3;
    private const float RepairEvacRouteThreshold = 6f;
    private const float RepairEvacTurnThreshold = 2f;
    private const int AirEvacPrepositionEnemyRange = 3;
    private const int AirEvacPatientEnemyRange = 2;

    // -------------------------------------------------------------------------
    // EVAC Shuttle — empty APC positions next to a frontline unit-under-repair
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideEvacShuttleAction(
        UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan,
        Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied,
        float maxTransportDistance = -1f)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;

        UnitManager evacuee = FindBestEvacCandidate(
            unit, snapshot, fromCell, paths, maxTransportDistance);
        if (evacuee == null) return null;

        Vector3Int evacueeCell = evacuee.CurrentCellPosition; evacueeCell.z = 0;
        ConstructionManager repairDest = FindRepairConstruction(
            evacuee, evacueeCell, snapshot.AITeam, BuildOccupied(evacuee), lineSnapshot: snapshot);
        Vector3Int objective = repairDest != null ? repairDest.CurrentCellPosition : FindTransportWaitTarget(snapshot.AITeam, evacueeCell);
        objective.z = 0;
        HashSet<Vector3Int> passengerReachable = BuildPassengerReachableSet(evacuee);
        Vector3Int moveTarget = FindTransportShuttleMove(
            unit, fromCell, evacueeCell, paths, occupied, snapshot.AITeam,
            objective, passengerReachable, plan, null, paths);
        bool airTransport = IsAirTransporter(unit);
        bool immediatePickup = IsPassengerInPickupRange(moveTarget, evacueeCell, EvacPickupRange, passengerReachable);
        if (airTransport
            && moveTarget != fromCell
            && !immediatePickup
            && !IsAirEvacPrepositionSafe(moveTarget, evacueeCell, snapshot.AITeam, out string unsafeReason))
        {
            Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} EVAC aborta pre-pickup #{evacuee.InstanceId}@{evacueeCell} via {moveTarget}: {unsafeReason}");
            return BuildAirTransportReturnToWaitTarget(unit, snapshot, fromCell, paths, occupied,
                $"EVAC pre-pickup inseguro ({unsafeReason})");
        }

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
        Dictionary<Vector3Int, List<Vector3Int>> transporterPaths,
        float maxTransportDistance = -1f)
    {
        // A mesma política do serviço de operações (Papeis > Transportador): esta
        // é a porta do shuttle e do naval para o EVAC.
        if (!FazAtenderEvac)
            return null;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
            return null;

        UnitManager best = null;
        float bestScore = float.MinValue;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == transporter) continue;
            if (candidate.SlotIndex != snapshot.AISlotIndex || candidate.IsDead || candidate.IsEmbarked) continue;
            if (!candidate.IsUnderRepair) continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData)) continue;
            // A ficha do transportador e a autoridade sobre quem pode ser
            // evacuado. Nao exclua aeronaves (ou qualquer outro dominio) por
            // uma regra generica: classe, skill, camada, vaga e exclusividade
            // ja sao validadas pelo slot configurado no UnitData.
            if (FindFittingSlotIndex(transporter, transporterData, candidate, candidateData) < 0) continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition; candidateCell.z = 0;
            if (!ShouldPreferRepairEvac(candidate, candidateCell, snapshot.AITeam, out float routeDist, out float turnEstimate, out bool danger))
                continue;

            float transportDist = SectorManager.HexDistance(transporterCell, candidateCell);
            // Primeiro envelope do EVAC: encontro tatico/operacional curto.
            // A compatibilidade nao depende do dominio do transportador.
            float pickupHorizon = maxTransportDistance >= 0f
                ? maxTransportDistance
                : Mathf.Max(
                    EvacPickupRange + transporter.MaxMovementPoints,
                    transporter.MaxMovementPoints * 2);
            if (transportDist > pickupHorizon) continue;

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
        if (IsAirTransporter(unit))
            return DecideAirEvacDeliveryAction(unit, evacuee, snapshot, fromCell, paths, occupied);

        // Reuse the same destination logic as the repair system
        ConstructionManager repairDest = FindRepairConstruction(
            evacuee, fromCell, snapshot.AITeam, new HashSet<Vector3Int>(occupied),
            rejectBaseCluster: !IsEliteRepairUnit(evacuee) && IsBaseUnderPressureForRepair(snapshot),
            lineSnapshot: snapshot);
        var repairCells = BuildEvacRepairCells(unit, evacuee, snapshot, fromCell,
            repairDest != null ? new List<ConstructionManager> { repairDest } : new List<ConstructionManager>());
        Vector3Int target = repairDest != null ? repairDest.CurrentCellPosition : fromCell;
        target.z = 0;

        float distToTarget = SectorManager.HexDistance(fromCell, target);
        bool airTransport = IsAirTransporter(unit);
        Vector3Int moveTarget = airTransport
            ? FindAirTransportMove(fromCell, target, paths, occupied, snapshot.AITeam)
            : FindTransportMove(unit, fromCell, target, paths, occupied, snapshot.AITeam);
        float moveImprovement = distToTarget - SectorManager.HexDistance(moveTarget, target);

        // Park adjacent to the repair building so the passenger lands on it,
        // rather than the APC occupying the building cell itself.
        if (moveTarget == target && repairDest != null)
        {
            Vector3Int park = FindEvacParkCell(unit, target, paths, occupied, evacuee, snapshot, repairCells);
            if (park != target)
            {
                moveTarget = park;
                moveImprovement = distToTarget - SectorManager.HexDistance(moveTarget, target);
            }
        }

        // A faixa de entrega é a do FERIDO a partir do reparo (PassageiroChegaAoAlvo),
        // não o número fixo do transportador (C7).

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
                    if (PassageiroChegaAoAlvo(evacuee, dc, target)
                        && IsEvacDropCellSafe(dc, snapshot.AITeam))
                    {
                        List<PodeDesembarcarOption> selected = SelectEvacDisembarkForPassenger(opts, evacuee, target, snapshot, repairCells);
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
                if (PassageiroChegaAoAlvo(evacuee, dc, target)
                    && IsEvacDropCellSafe(dc, snapshot.AITeam))
                {
                    List<PodeDesembarcarOption> selected = SelectEvacDisembarkForPassenger(disembarkOpts, evacuee, target, snapshot, repairCells);
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

    // Hospital eligibility and order belong to Repair. EVAC adds the free
    // patient cell and adjacent LZ; only this turn's LZs run the drop sensor.
    private PlayerAction DecideAirEvacDeliveryAction(
        UnitManager transporter, UnitManager patient, AIWorldSnapshot snapshot,
        Vector3Int from, Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        var threatByCell = new Dictionary<Vector3Int, float>();
        var originalPaths = paths;
        paths = BuildSafeAirEvacPaths(from, paths, snapshot.AITeam, threatByCell);
        ConstructionManager bestBuilding = null;
        Vector3Int bestLz = from;
        PodeDesembarcarOption bestDrop = null;
        ConstructionManager approachBuilding = null;
        Vector3Int approachLz = from;
        var neighbors = new List<Vector3Int>(6);
        var dropsByLz = new Dictionary<Vector3Int, List<PodeDesembarcarOption>>();
        bool rejectBaseCluster = !IsEliteRepairUnit(patient)
            && IsBaseUnderPressureForRepair(snapshot);
        List<ConstructionManager> hospitals = RankRepairConstructions(
            patient, from, snapshot.AITeam,
            occupied != null ? new HashSet<Vector3Int>(occupied) : new HashSet<Vector3Int>(),
            rejectBaseCluster: rejectBaseCluster, lineSnapshot: snapshot, log: false);
        var repairCells = BuildEvacRepairCells(transporter, patient, snapshot, from, hospitals);
        List<PodeDesembarcarOption> bestOrders = null;
        int bestRepairCount = -1;
        int hospitalRank = -1, bestHospitalRank = int.MaxValue;
        float bestScore = float.MinValue;

        foreach (ConstructionManager building in hospitals)
        {
            hospitalRank++;
            // Do not bypass ReceivedSuppliesThisTurn. This is a concrete
            // delivery decision, not the "does any repair facility exist?" query.
            if (!CanSurfaceRepairConstructionServe(building, patient, snapshot.AITeam))
                continue;
            Vector3Int target = building.CurrentCellPosition;
            target.z = 0;
            if (!IsConfirmedVisibleOrExploredCellForAI(target)
                || !IsEvacDropCellSafe(target, snapshot.AITeam)
                || (plannedDestinations != null && plannedDestinations.Contains(target))
                || !OccupancyResolver.CanEndMove(patient, target,
                    UnitOccupancyRules.GetUnitsAtCell(boardTilemap, target, transporter)))
                continue;

            float futureScore = float.MinValue;
            Vector3Int futureLz = from;
            bool hasFutureLz = false;
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, target, neighbors);
            foreach (Vector3Int rawLz in neighbors)
            {
                Vector3Int lz = rawLz; lz.z = 0;
                if (!boardTilemap.HasTile(lz)
                    || !IsConfirmedVisibleOrExploredCellForAI(lz)
                    || (occupied != null && occupied.Contains(lz))
                    || (plannedDestinations != null && plannedDestinations.Contains(lz) && lz != from)
                    || IsOwnProductionCell(lz, snapshot.AITeam)
                    || !CanAirTransportStopAtCell(transporter, lz))
                    continue;

                float threat = GetAirEvacThreat(lz, snapshot.AITeam, threatByCell);
                if (threat > 0f || !IsEvacDropCellSafe(lz, snapshot.AITeam))
                    continue;
                bool reachable = lz == from || (paths != null && paths.ContainsKey(lz));
                if (!reachable)
                {
                    if (originalPaths != null && originalPaths.ContainsKey(lz))
                        continue; // reachable only through a threatened flight path
                    // Direction only. No remote drop simulation or landing promise.
                    float score = -SectorManager.HexDistance(from, lz) * 100f - threat;
                    if (!hasFutureLz || score > futureScore)
                    {
                        futureScore = score; futureLz = lz; hasFutureLz = true;
                    }
                    continue;
                }

                if (!dropsByLz.TryGetValue(lz, out List<PodeDesembarcarOption> options))
                {
                    options = new List<PodeDesembarcarOption>();
                    AIDecisionPerf.AddCount("AirEvacDisembarkProbes");
                    PodeDesembarcarSensor.CollectOptionsFromCell(
                        transporter, lz, boardTilemap, terrainDatabase, options, out _);
                    dropsByLz[lz] = options;
                }
                PodeDesembarcarOption drop = options.Find(option =>
                    option.passengerUnit == patient && option.disembarkCell == target);
                if (drop == null)
                    continue;
                var orders = MatchEvacPatients(patient, target, options, snapshot, repairCells);
                if (orders.Count == 0) continue;
                int repaired = orders.FindAll(order => repairCells[order.passengerUnit.InstanceId].Contains(order.disembarkCell)).Count;
                float immediateScore = -GetPathStepCount(paths, lz) * 100f - threat;
                if (bestOrders == null || orders.Count > bestOrders.Count
                    || (orders.Count == bestOrders.Count && (repaired > bestRepairCount
                        || (repaired == bestRepairCount && (hospitalRank < bestHospitalRank
                            || (hospitalRank == bestHospitalRank && immediateScore > bestScore))))))
                {
                    bestScore = immediateScore; bestBuilding = building; bestLz = lz; bestDrop = drop;
                    bestOrders = orders; bestRepairCount = repaired; bestHospitalRank = hospitalRank;
                }
            }
            // Joint deliveries win; Repair's order breaks equivalent delivery ties.
            // No materializable delivery: approach its highest-ranked destination.
            if (approachBuilding == null && hasFutureLz)
            {
                approachBuilding = building; approachLz = futureLz;
            }
        }

        if (bestDrop != null)
        {
            Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC entrega #{patient.InstanceId} em {bestBuilding.name}@{bestDrop.disembarkCell} via LZ={bestLz}");
            var selected = bestOrders;
            Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC entrega conjunta: feridos={selected.Count}, em reparo={bestRepairCount}, LZ={bestLz}");
            if (bestLz == from)
                return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, selected);
            paths.TryGetValue(bestLz, out List<Vector3Int> path);
            return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, selected, bestLz, path);
        }

        if (approachBuilding == null)
            return DecideAirEvacHqFallback(transporter, patient, snapshot, from,
                originalPaths, occupied, dropsByLz, threatByCell, rejectBaseCluster, repairCells);

        Vector3Int move = from;
        if (approachBuilding != null && paths != null)
        {
            var stopBans = occupied != null ? new HashSet<Vector3Int>(occupied) : new HashSet<Vector3Int>();
            foreach (Vector3Int cell in paths.Keys)
                if (IsOwnProductionCell(cell, snapshot.AITeam)
                    || cell == approachBuilding.CurrentCellPosition)
                    stopBans.Add(cell);
            move = FindAirTransportMove(from, approachLz, paths, stopBans, snapshot.AITeam);
        }
        Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC mantém #{patient.InstanceId} a bordo; "
            + (approachBuilding != null
                ? $"aproxima via {move} para LZ estimada={approachLz}, reparo={approachBuilding.name}"
                : "sem prédio elegível de reparo vago com LZ disponível"));
        return BuildMoveBatch(transporter, snapshot.AITeam, from, move, paths);
    }

    private Dictionary<int, HashSet<Vector3Int>> BuildEvacRepairCells(
        UnitManager transporter, UnitManager primary, AIWorldSnapshot snapshot, Vector3Int from,
        List<ConstructionManager> primaryHospitals = null)
    {
        AIDecisionPerf.AddCount("EvacRepairCellsBuilds");
        var result = new Dictionary<int, HashSet<Vector3Int>>();
        foreach (UnitManager patient in CollectPassengers(transporter))
        {
            if (patient != primary && !patient.IsUnderRepair) continue;
            var cells = new HashSet<Vector3Int>();
            result[patient.InstanceId] = cells;
            var hospitals = patient == primary && primaryHospitals != null ? primaryHospitals
                : RankRepairConstructions(patient, from, snapshot.AITeam, new HashSet<Vector3Int>(),
                    rejectBaseCluster: !IsEliteRepairUnit(patient) && IsBaseUnderPressureForRepair(snapshot),
                    lineSnapshot: snapshot, log: false);
            foreach (var building in hospitals)
            {
                Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
                if (CanSurfaceRepairConstructionServe(building, patient, snapshot.AITeam)
                    && IsEvacDropCellSafe(cell, snapshot.AITeam)
                    && OccupancyResolver.CanEndMove(patient, cell,
                        UnitOccupancyRules.GetUnitsAtCell(boardTilemap, cell, transporter)))
                    cells.Add(cell);
            }
        }
        return result;
    }

    private List<PodeDesembarcarOption> MatchEvacPatients(
        UnitManager primary, Vector3Int primaryCell, List<PodeDesembarcarOption> options,
        AIWorldSnapshot snapshot, Dictionary<int, HashSet<Vector3Int>> repairCells,
        HashSet<Vector3Int> finalRing = null, bool requireSafeOutsideRing = false)
    {
        var secondaryWalkRoutes = new Dictionary<int, Dictionary<Vector3Int, int>>();
        bool Resolve(UnitManager passenger, Vector3Int cell, out Vector3Int target, out int routeCost)
        {
            target = cell; routeCost = 0;
            if (!repairCells.ContainsKey(passenger.InstanceId)) return false;
            if (plannedDestinations != null && plannedDestinations.Contains(cell)) return false;
            if (passenger == primary) return cell == primaryCell;
            if (finalRing != null)
            {
                if (finalRing.Contains(cell)) return true;
                if (requireSafeOutsideRing && !IsEvacDropCellSafe(cell, snapshot.AITeam))
                    return false;
                // The primary anchors the HQ delivery. The second patient may
                // use adjacent free ground and walk the last tactical leg;
                // do not require both drop cells to fit the exact same ring.
                if (ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell) != null)
                    return false;
                if (!secondaryWalkRoutes.TryGetValue(passenger.InstanceId, out var walk))
                {
                    walk = GetOrBuildDisembarkPassengerRoute(
                        passenger, primaryCell, Mathf.Max(1, passenger.MaxMovementPoints));
                    secondaryWalkRoutes[passenger.InstanceId] = walk;
                }
                return walk != null && walk.TryGetValue(cell, out int cost)
                    && cost <= passenger.MaxMovementPoints;
            }
            if (!IsEvacDropCellSafe(cell, snapshot.AITeam)) return false;
            if (repairCells[passenger.InstanceId].Contains(cell)) return true;
            // No repair vacancy: stay aboard. Ground release belongs only
            // to the final HQ-ring fallback above, not an ordinary hospital.
            return false;
        }
        var match = MelhorDesembarqueService.MatchOptions(new MelhorDesembarqueRequest {
            requiredPassengerInstanceId = primary.InstanceId,
            resolvePassengerTarget = Resolve,
            disembarkPreferenceCost = (passenger, cell) => repairCells[passenger.InstanceId].Contains(cell) ? 0 : 1
        }, options);
        return match.spots.ConvertAll(spot => spot.option);
    }

    // Same threat estimator and zero-threat gate already used by conservative
    // transport/rear-area decisions. Cache once per cell for the whole EVAC.
    private float GetAirEvacThreat(Vector3Int cell, TeamId team, Dictionary<Vector3Int, float> cache)
    {
        cell.z = 0;
        if (!cache.TryGetValue(cell, out float threat))
        {
            threat = CalculateThreatLevel(cell, team);
            cache[cell] = threat;
        }
        return threat;
    }

    private Dictionary<Vector3Int, List<Vector3Int>> BuildSafeAirEvacPaths(
        Vector3Int from, Dictionary<Vector3Int, List<Vector3Int>> paths,
        TeamId team, Dictionary<Vector3Int, float> threatByCell)
    {
        var safe = new Dictionary<Vector3Int, List<Vector3Int>>();
        if (paths == null) return safe;
        foreach (var pair in paths)
        {
            bool clear = pair.Key == from || GetAirEvacThreat(pair.Key, team, threatByCell) <= 0f;
            if (clear && pair.Value != null)
                foreach (Vector3Int cell in pair.Value)
                    if (cell != from && GetAirEvacThreat(cell, team, threatByCell) > 0f)
                    {
                        clear = false;
                        break;
                    }
            if (clear) safe[pair.Key] = pair.Value;
            else AIDecisionPerf.AddCount("AirEvacUnsafeRoutesRejected");
        }
        return safe;
    }

    private PlayerAction DecideAirEvacHqFallback(
        UnitManager transporter, UnitManager patient, AIWorldSnapshot snapshot,
        Vector3Int from, Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Dictionary<Vector3Int, List<PodeDesembarcarOption>> dropsByLz,
        Dictionary<Vector3Int, float> threatByCell, bool avoidBaseClusterCells,
        Dictionary<int, HashSet<Vector3Int>> repairCells)
    {
        if (snapshot.MyHQ == null)
        {
            Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC sem hospital elegível nem HQ para fallback seguro; mantém paciente #{patient.InstanceId}");
            return BuildMoveBatch(transporter, snapshot.AITeam, from, from, paths);
        }
        Vector3Int hq = snapshot.MyHQ.CurrentCellPosition; hq.z = 0;
        // Enumerate only the HQ ring, not the map; policy and scoring belong
        // to the same fallback used by the patient when travelling alone.
        var ring = new HashSet<Vector3Int> { hq };
        var frontier = new List<Vector3Int> { hq };
        var neighbors = new List<Vector3Int>(6);
        for (int distance = 0; distance < DefenseEnemyRange; distance++)
        {
            var next = new List<Vector3Int>();
            foreach (Vector3Int cell in frontier)
            {
                UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, cell, neighbors);
                foreach (Vector3Int neighbor in neighbors)
                    if (ring.Add(neighbor)) next.Add(neighbor);
            }
            frontier = next;
        }
        var freeRing = new HashSet<Vector3Int>();
        var safeRing = new HashSet<Vector3Int>();
        foreach (Vector3Int cell in ring)
        {
            if (!boardTilemap.HasTile(cell) || !IsConfirmedVisibleOrExploredCellForAI(cell)
                || !IsRepairHqFallbackCellAllowed(patient, cell, snapshot.AITeam, avoidBaseClusterCells)
                || (plannedDestinations != null && plannedDestinations.Contains(cell))
                || !OccupancyResolver.CanEndMove(patient, cell,
                    UnitOccupancyRules.GetUnitsAtCell(boardTilemap, cell, transporter)))
                continue;
            freeRing.Add(cell);
            if (IsSafeRepairHqFallbackPosition(patient, cell, hq, snapshot.AITeam, avoidBaseClusterCells)
                && IsEvacDropCellSafe(cell, snapshot.AITeam)
                && GetAirEvacThreat(cell, snapshot.AITeam, threatByCell) <= 0f)
                safeRing.Add(cell);
        }
        HashSet<Vector3Int> targetRing = safeRing.Count > 0 ? safeRing : freeRing;
        string tier = safeRing.Count > 0 ? "anel seguro" : "anel contestado, menor ameaça";
        Vector3Int anchor = hq;
        float anchorThreat = float.MaxValue, anchorScore = float.MinValue;
        foreach (Vector3Int cell in targetRing)
        {
            float threat = GetAirEvacThreat(cell, snapshot.AITeam, threatByCell);
            float score = ScoreRepairHqFallbackCell(cell, hq, snapshot.AITeam);
            if (threat < anchorThreat || (threat == anchorThreat && score > anchorScore))
            {
                anchorThreat = threat; anchorScore = score; anchor = cell;
            }
        }
        if (paths == null) paths = new Dictionary<Vector3Int, List<Vector3Int>>();
        var routeThreats = new Dictionary<Vector3Int, (float peak, float total)>();
        foreach (var pair in paths)
        {
            float peak = GetAirEvacThreat(pair.Key, snapshot.AITeam, threatByCell);
            float total = peak;
            if (pair.Value != null)
                foreach (Vector3Int cell in pair.Value)
                {
                    if (cell == from || cell == pair.Key) continue;
                    float threat = GetAirEvacThreat(cell, snapshot.AITeam, threatByCell);
                    peak = Mathf.Max(peak, threat); total += threat;
                }
            routeThreats[pair.Key] = (peak, total);
        }

        // Patient cell threat wins first, then flight peak/total threat.
        // A zero-threat route wins within the selected patient-cell tier.
        // Nearby enemies never veto this final fallback.
        {
            PodeDesembarcarOption bestDrop = null;
            List<PodeDesembarcarOption> bestOrders = null;
            Vector3Int bestLz = from;
            float bestDropThreat = float.MaxValue, bestPeak = float.MaxValue, bestTotal = float.MaxValue;
            int bestCost = int.MaxValue;
            float bestScore = float.MinValue;
            foreach (var pair in paths)
            {
                Vector3Int lz = pair.Key;
                var risk = routeThreats[lz];
                if (targetRing.Count == 0 || SectorManager.HexDistance(lz, hq) > DefenseEnemyRange + 1
                    || (occupied != null && occupied.Contains(lz) && lz != from)
                    || (plannedDestinations != null && plannedDestinations.Contains(lz) && lz != from)
                    || IsOwnProductionCell(lz, snapshot.AITeam)
                    || !CanAirTransportStopAtCell(transporter, lz)) continue;
                if (!dropsByLz.TryGetValue(lz, out List<PodeDesembarcarOption> options))
                {
                    options = new List<PodeDesembarcarOption>();
                    AIDecisionPerf.AddCount("AirEvacDisembarkProbes");
                    PodeDesembarcarSensor.CollectOptionsFromCell(
                        transporter, lz, boardTilemap, terrainDatabase, options, out _);
                    dropsByLz[lz] = options;
                }
                int cost = GetPathStepCount(paths, lz);
                foreach (var option in options)
                {
                    if (option.passengerUnit != patient || !targetRing.Contains(option.disembarkCell)) continue;
                    float threat = GetAirEvacThreat(option.disembarkCell, snapshot.AITeam, threatByCell);
                    float score = ScoreRepairHqFallbackCell(option.disembarkCell, hq, snapshot.AITeam);
                    var orders = MatchEvacPatients(patient, option.disembarkCell, options, snapshot, repairCells, targetRing,
                        requireSafeOutsideRing: safeRing.Count > 0);
                    if (orders.Count == 0) continue;
                    bool betterRisk = threat < bestDropThreat
                        || (threat == bestDropThreat && (risk.peak < bestPeak
                            || (risk.peak == bestPeak && (risk.total < bestTotal
                                || (risk.total == bestTotal && (cost < bestCost
                                    || (cost == bestCost && score > bestScore)))))));
                    bool better = bestOrders == null || orders.Count > bestOrders.Count
                        || (orders.Count == bestOrders.Count && betterRisk);
                    if (better)
                    {
                        bestDrop = option; bestOrders = orders; bestLz = lz; bestCost = cost; bestScore = score;
                        bestDropThreat = threat; bestPeak = risk.peak; bestTotal = risk.total;
                    }
                }
            }
            if (bestDrop != null)
            {
                Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC {tier}: libera #{patient.InstanceId} @ {bestDrop.disembarkCell}, LZ={bestLz}, ameaçaDescida={bestDropThreat:F1}, ameaçaRota={bestPeak:F1}");
                var selected = bestOrders;
                Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC {tier}: entrega conjunta de {selected.Count} ferido(s)");
                if (bestLz == from) return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, selected);
                return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, selected, bestLz, paths[bestLz]);
            }

        }
        // A materializable delivery wins before any approach, including when
        // its route only becomes available in the least-threat flight tier.
        for (int flightTier = 0; flightTier < 2; flightTier++)
        {
            Vector3Int move = from;
            float movePeak = float.MaxValue, moveTotal = float.MaxValue, moveDistance = float.MaxValue;
            float originDistance = SectorManager.HexDistance(from, anchor);
            bool originThreatened = GetAirEvacThreat(from, snapshot.AITeam, threatByCell) > 0f;
            // Progress to the ring first. If trapped on a threatened origin,
            // allow escape in any direction instead of returning a wait action.
            for (int escape = 0; escape < (originThreatened ? 2 : 1) && move == from; escape++)
                foreach (var pair in paths)
                {
                    Vector3Int cell = pair.Key;
                    var risk = routeThreats[cell];
                    float dist = SectorManager.HexDistance(cell, anchor);
                    if (cell == from || (flightTier == 0 && risk.peak > 0f)
                        || (escape == 0 && dist >= originDistance)
                        || (occupied != null && occupied.Contains(cell))
                        || (plannedDestinations != null && plannedDestinations.Contains(cell))
                        || IsOwnProductionCell(cell, snapshot.AITeam)
                        || !CanAirTransportStopAtCell(transporter, cell)) continue;
                    if (move == from || risk.peak < movePeak
                        || (risk.peak == movePeak && (risk.total < moveTotal
                            || (risk.total == moveTotal && dist < moveDistance))))
                    {
                        move = cell; movePeak = risk.peak; moveTotal = risk.total; moveDistance = dist;
                    }
                }
            if (move != from)
            {
                Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC {tier}: aproxima/escapa via {move}, ameaçaRota={movePeak:F1}; paciente a bordo até desembarque materializável");
                return BuildMoveBatch(transporter, snapshot.AITeam, from, move, paths);
            }
        }
        Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC {tier}: sem movimento ou desembarque fisicamente permitido; células livres={freeRing.Count}");
        return BuildMoveBatch(transporter, snapshot.AITeam, from, from, paths);
    }

    private List<PodeDesembarcarOption> SelectEvacDisembarkForPassenger(
        List<PodeDesembarcarOption> options,
        UnitManager evacuee,
        Vector3Int repairTarget,
        AIWorldSnapshot snapshot,
        Dictionary<int, HashSet<Vector3Int>> repairCells)
    {
        TeamId aiTeam = snapshot.AITeam;
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

        if (best == null) return selected;
        UnitManager transporter = best.transporterUnit;
        return MatchEvacPatients(evacuee, best.disembarkCell, options, snapshot, repairCells);
    }

    private bool IsEvacDropCellSafe(Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;
        return !HasEnemyUnitAnyLayerAtCell(cell, aiTeam)
            && !HasNearbyVisibleEnemy(cell, aiTeam, DefenseEnemyRange);
    }

    private bool IsAirEvacPrepositionSafe(
        Vector3Int lzCell,
        Vector3Int evacueeCell,
        TeamId aiTeam,
        out string reason)
    {
        lzCell.z = 0;
        evacueeCell.z = 0;
        reason = "";

        if (HasEnemyUnitAnyLayerAtCell(lzCell, aiTeam))
        {
            reason = "LZ ocupada por inimigo";
            return false;
        }

        int lzEnemies = CountVisibleEnemiesNearNoCache(lzCell, aiTeam, AirEvacPrepositionEnemyRange);
        if (lzEnemies > 0)
        {
            reason = $"LZ quente inimigos={lzEnemies}<={AirEvacPrepositionEnemyRange}h";
            return false;
        }

        int patientEnemies = CountVisibleEnemiesNearNoCache(evacueeCell, aiTeam, AirEvacPatientEnemyRange);
        if (patientEnemies > 0)
        {
            reason = $"paciente sob pressao inimigos={patientEnemies}<={AirEvacPatientEnemyRange}h";
            return false;
        }

        float lzThreat = CalculateThreatLevel(lzCell, aiTeam);
        if (lzThreat > 0.1f)
        {
            reason = $"threat LZ={lzThreat:F1}";
            return false;
        }

        return true;
    }

    private int CountVisibleEnemiesNearNoCache(Vector3Int cell, TeamId aiTeam, int range)
    {
        cell.z = 0;
        int count = 0;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.SlotIndex == ResolveAISlotKey(aiTeam) || enemy.IsDead || enemy.IsEmbarked)
                continue;
            if (mc != null && !mc.IsUnitVisibleForSlotNoCache(enemy, PlayerSlotId.FromIndex(currentAISlotIndex)))
                continue;

            Vector3Int ec = enemy.CurrentCellPosition;
            ec.z = 0;
            if (SectorManager.HexDistance(ec, cell) <= range)
                count++;
        }

        return count;
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
            if (opt.transporterUnit == null || opt.transporterUnit.SlotIndex != ResolveAISlotKey(aiTeam)) continue;
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

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        Vector3Int best = fromCell;
        float fromDist = SectorManager.HexDistance(fromCell, transporterCell);
        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;

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

    // logCategory/logVerb: o mecanismo (mover ate o transporte e embarcar) e o
    // mesmo para EVAC de reparo e para rebasing aereo, mas o MOTIVO nao. Sem
    // isso um caca 10/10 trocando de base aparecia no log como "Repair EVAC".
    private bool TryBuildRepairEvacExtendedEmbarkAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        Vector3Int fromCell,
        UnitManager transporter,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out PlayerAction action,
        string logCategory = "Repair",
        string logVerb = "EVAC")
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
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
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
            if (stopCell != fromCell && occupied != null && occupied.Contains(stopCell))
                continue;

            if (stopCell == fromCell)
            {
                float directPickupDist = SectorManager.HexDistance(fromCell, transporterCell);
                if (directPickupDist <= 1.5f
                    && TryEmbarkFromHex(fromCell, null, unit.RemainingMovementPoints,
                            transporterCell, unit, unitData, plan, null, snapshot, out action,
                            requireSectorMatch: false, allowOverflow: true, requireFormalPassenger: false,
                            expectedTransporter: transporter, logCategory: logCategory))
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
                    expectedTransporter: transporter, logCategory: logCategory))
            {
                Debug.Log($"{TL(logCategory)} {unit.InstanceId} {logVerb} — move+embarca em transporter #{transporter.InstanceId} via {stopCell}");
                return true;
            }
        }

        return false;
    }

    // Returns a cell adjacent to buildingCell from which the APC can drop the passenger
    // directly onto the building. Falls back to buildingCell if none is found.
    private Vector3Int FindEvacParkCell(
        UnitManager unit, Vector3Int buildingCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        UnitManager evacuee, AIWorldSnapshot snapshot,
        Dictionary<int, HashSet<Vector3Int>> repairCells)
    {
        TeamId aiTeam = snapshot.AITeam;
        int bestCount = -1, bestRepairCount = -1;
        var neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, buildingCell, neighbors);

        Vector3Int best = buildingCell;
        float bestThreat = float.MaxValue;

        foreach (Vector3Int raw in neighbors)
        {
            Vector3Int nc = raw; nc.z = 0;
            if (!paths.ContainsKey(nc)) continue;
            if (occupied.Contains(nc)) continue;

            List<PodeDesembarcarOption> opts = SimulateDisembarkFromCell(unit, nc);
            if (opts == null || opts.Count == 0) continue;

            bool canDropAtBuilding = false;
            foreach (PodeDesembarcarOption opt in opts)
            {
                if (opt.passengerUnit != evacuee) continue;
                Vector3Int dc = opt.disembarkCell; dc.z = 0;
                if (dc == buildingCell) { canDropAtBuilding = true; break; }
            }
            if (!canDropAtBuilding) continue;

            var orders = MatchEvacPatients(evacuee, buildingCell, opts, snapshot, repairCells);
            int repaired = orders.FindAll(order => repairCells[order.passengerUnit.InstanceId].Contains(order.disembarkCell)).Count;
            float threat = CalculateThreatLevel(nc, aiTeam);
            if (orders.Count > bestCount || (orders.Count == bestCount
                && (repaired > bestRepairCount || (repaired == bestRepairCount && threat < bestThreat))))
            {
                bestCount = orders.Count; bestRepairCount = repaired; bestThreat = threat; best = nc;
            }
        }

        return best;
    }

    private UnitManager FindBestRepairEvacTransporterForPassenger(UnitManager passenger, TeamId aiTeam, Vector3Int passengerCell)
    {
        if (passenger == null || !passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null)
            return null;

        UnitManager best = null;
        float bestScore = float.MinValue;
        foreach (UnitManager transporter in UnitManager.AllActive)
        {
            if (transporter == null || transporter.SlotIndex != ResolveAISlotKey(aiTeam)) continue;
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
        if (unit == null)
            return false;

        // Aeronave em reparo nao precisa de uma construcao terrestre para
        // justificar EVAC. A vaga compativel do transportador ja foi validada
        // antes; aqui medimos apenas a urgencia operacional. Isso permite que
        // porta-avioes e outros transportadores aereos recuperem aeronaves em
        // voo sem reintroduzir uma exclusao generica por Domain.Air.
        if (unit.GetDomain() == Domain.Air)
        {
            bool fuelCritical = IsAircraftFuelCriticalForNextUpkeep(
                unit,
                out _,
                out _,
                out int criticalThreshold);
            routeDist = Mathf.Max(
                0f, criticalThreshold - unit.CurrentFuel);
            float fuelUrgency = 1f
                - unit.CurrentFuel
                  / (float)Mathf.Max(1, unit.GetMaxFuel());
            turnEstimate = fuelUrgency * 2f
                + (fuelCritical ? 3f : 0f);
            danger = fuelCritical
                || HasNearbyVisibleEnemy(
                    fromCell, aiTeam, AirEvacPatientEnemyRange);
            return unit.IsUnderRepair;
        }

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
