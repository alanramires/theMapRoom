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

        return DecideSurfaceEvacDeliveryAction(unit, evacuee, snapshot, fromCell, paths, occupied, naval: false);
    }

    private PlayerAction DecideSurfaceEvacDeliveryAction(UnitManager transporter, UnitManager patient,
        AIWorldSnapshot snapshot, Vector3Int from, Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied, bool naval)
    {
        var hospitals = RankRepairConstructions(patient, from, snapshot.AITeam,
            occupied != null ? new HashSet<Vector3Int>(occupied) : new HashSet<Vector3Int>(),
            rejectBaseCluster: !IsEliteRepairUnit(patient) && IsBaseUnderPressureForRepair(snapshot),
            lineSnapshot: snapshot, log: false, allowOccupiedDestinations: true);
        var repairCells = BuildEvacRepairCells(transporter, patient, snapshot, from, hospitals);
        var stopBans = occupied != null ? new HashSet<Vector3Int>(occupied) : new HashSet<Vector3Int>();
        foreach (ConstructionManager building in ConstructionManager.AllActive)
            if (building != null) stopBans.Add(building.CurrentCellPosition);
        var candidates = paths != null ? new HashSet<Vector3Int>(paths.Keys) : new HashSet<Vector3Int>();
        candidates.Add(from);
        var neighbors = new List<Vector3Int>(6);
        List<PodeDesembarcarOption> best = null;
        Vector3Int bestLz = from;
        int bestRepairs = -1;
        var threatByCell = new EvacThreatMap(transporter, snapshot);
        var routeThreats = BuildEvacRouteThreats(from, paths, snapshot.AITeam, threatByCell);
        ((EvacRepairCells)repairCells).RouteThreats = routeThreats;
        EvacDeliveryScore bestDelivery = default;
        foreach (Vector3Int lz in candidates)
        {
            if ((stopBans.Contains(lz) && lz != from)
                || ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, lz) != null
                || !IsConfirmedVisibleOrExploredCellForAI(lz)
                || !IsEvacDropCellSafe(lz, snapshot.AITeam)) continue;
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, lz, neighbors);
            bool nearDelivery = false;
            foreach (Vector3Int drop in neighbors)
                if (((EvacRepairCells)repairCells).HqRing.Cells.Contains(drop)
                    || TryResolveEvacHospitalDrop(patient, drop, snapshot, repairCells, out _, out _))
                { nearDelivery = true; break; }
            if (!nearDelivery) continue;
            var options = SimulateDisembarkFromCell(transporter, lz);
            foreach (var option in options)
            {
                if (option.passengerUnit != patient) continue;
                bool inHq = ((EvacRepairCells)repairCells).HqRing.Cells.Contains(option.disembarkCell);
                Vector3Int hospital; int walk;
                bool hasHospital = TryResolveEvacHospitalDrop(patient, option.disembarkCell, snapshot,
                    repairCells, out hospital, out walk);
                if (!hasHospital && !inHq) continue;
                var orders = MatchEvacPatients(patient, option.disembarkCell, options, snapshot, repairCells);
                if (orders.Count == 0) continue;
                var risk = routeThreats.TryGetValue(lz, out var routeRisk) ? routeRisk : (0f, 0f);
                var delivery = ScoreEvacDelivery(orders, repairCells, snapshot, threatByCell,
                    risk, GetPathStepCount(paths, lz), snapshot.MyHQ != null
                        ? ScoreRepairHqFallbackCell(option.disembarkCell, snapshot.MyHQ.CurrentCellPosition, snapshot.AITeam) : 0f,
                    GetAirEvacThreat(lz, snapshot.AITeam, threatByCell));
                if (best == null || delivery.Beats(bestDelivery))
                {
                    best = orders; bestLz = lz; bestRepairs = delivery.Repairs; bestDelivery = delivery;
                }
            }
        }
        if (best != null)
        {
            Debug.Log($"{TL("Transporte")} #{transporter.InstanceId} EVAC entrega {best.Count} passageiro(s), em reparo={bestRepairs}, parada fora dos predios={bestLz}");
            if (bestLz == from) return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, best);
            return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, best, bestLz, paths[bestLz]);
        }
        // No materializable drop: try each ranked hospital, then the existing HQ region.
        var anchors = hospitals.ConvertAll(building => building.CurrentCellPosition);

        foreach (Vector3Int anchor in anchors)
        {
            Vector3Int target = naval ? ResolveNavalApproachTarget(transporter, anchor, from) : anchor;
            Vector3Int move = paths != null
                ? FindTransportMove(transporter, from, target, paths, stopBans, snapshot.AITeam) : from;
            if (move == from) continue;
            Debug.Log($"{TL("Transporte")} #{transporter.InstanceId} EVAC aproxima de {anchor} via {move}; entrega ainda nao materializavel");
            return BuildMoveBatch(transporter, snapshot.AITeam, from, move, paths);
        }
        if (snapshot.MyHQ != null)
            return DecideAirEvacHqFallback(transporter, patient, snapshot, from, paths, stopBans,
                new Dictionary<Vector3Int, List<PodeDesembarcarOption>>(), threatByCell,
                !IsEliteRepairUnit(patient) && IsBaseUnderPressureForRepair(snapshot), repairCells);
        Debug.Log($"{TL("Transporte")} #{transporter.InstanceId} EVAC sem entrega ou aproximacao legal; mantem carga");
        return BuildMoveBatch(transporter, snapshot.AITeam, from, from, paths);
    }

    // Hospital eligibility and order belong to Repair. EVAC adds the free
    // patient cell and adjacent LZ; only this turn's LZs run the drop sensor.
    private PlayerAction DecideAirEvacDeliveryAction(
        UnitManager transporter, UnitManager patient, AIWorldSnapshot snapshot,
        Vector3Int from, Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        var threatByCell = new EvacThreatMap(transporter, snapshot);
        var originalPaths = paths;
        var routeThreats = BuildEvacRouteThreats(from, paths, snapshot.AITeam, threatByCell);
        var safePaths = new Dictionary<Vector3Int, List<Vector3Int>>();
        if (paths != null) foreach (var pair in paths)
            if (routeThreats[pair.Key].peak <= 0f) safePaths[pair.Key] = pair.Value;
            else AIDecisionPerf.AddCount("AirEvacUnsafeRoutesRejected");
        paths = safePaths;
        var approachStopBans = occupied != null ? new HashSet<Vector3Int>(occupied) : new HashSet<Vector3Int>();
        foreach (Vector3Int cell in paths.Keys)
            if (ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell) != null) approachStopBans.Add(cell);
        ConstructionManager bestBuilding = null;
        Vector3Int bestLz = from;
        PodeDesembarcarOption bestDrop = null;
        ConstructionManager approachBuilding = null;
        Vector3Int approachLz = from;
        var dropsByLz = new Dictionary<Vector3Int, List<PodeDesembarcarOption>>();
        bool rejectBaseCluster = !IsEliteRepairUnit(patient)
            && IsBaseUnderPressureForRepair(snapshot);
        List<ConstructionManager> hospitals = RankRepairConstructions(
            patient, from, snapshot.AITeam,
            occupied != null ? new HashSet<Vector3Int>(occupied) : new HashSet<Vector3Int>(),
            rejectBaseCluster: rejectBaseCluster, lineSnapshot: snapshot, log: false, allowOccupiedDestinations: true);
        var repairCells = BuildEvacRepairCells(transporter, patient, snapshot, from, hospitals);
        ((EvacRepairCells)repairCells).RouteThreats = routeThreats;
        List<PodeDesembarcarOption> bestOrders = null;
        int bestRepairCount = -1;
        EvacDeliveryScore bestDelivery = default;

        for (int flightTier = 0; flightTier < 2; flightTier++)
        {
            paths = flightTier == 0 ? safePaths : originalPaths ?? new Dictionary<Vector3Int, List<Vector3Int>>();
            foreach (ConstructionManager building in hospitals)
            {
                // Do not bypass ReceivedSuppliesThisTurn. This is a concrete
                // delivery decision, not the "does any repair facility exist?" query.
                if (!CanSurfaceRepairConstructionServe(building, patient, snapshot.AITeam))
                    continue;
                Vector3Int target = building.CurrentCellPosition;
                target.z = 0;
                if (!IsConfirmedVisibleOrExploredCellForAI(target)
                    || !IsEvacDropCellSafe(target, snapshot.AITeam))
                    continue;

                float futureScore = float.MinValue;
                Vector3Int futureLz = from;
                bool hasFutureLz = false;
                int stalledFutureLzs = 0;
                foreach (Vector3Int rawLz in GetEvacHospitalLzs(patient, target, snapshot, repairCells))
                {
                    Vector3Int lz = rawLz; lz.z = 0;
                    if (!boardTilemap.HasTile(lz)
                        || !IsConfirmedVisibleOrExploredCellForAI(lz)
                        || (occupied != null && occupied.Contains(lz))
                        || (plannedDestinations != null && plannedDestinations.Contains(lz) && lz != from)
                        || ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, lz) != null
                        || !CanAirTransportStopAtCell(transporter, lz))
                        continue;

                    float threat = GetAirEvacThreat(lz, snapshot.AITeam, threatByCell);
                    if (flightTier == 0 && (threat > 0f || !IsEvacDropCellSafe(lz, snapshot.AITeam)))
                        continue;
                    bool reachable = lz == from || (paths != null && paths.ContainsKey(lz));
                    if (!reachable)
                    {
                        if (flightTier > 0 || (originalPaths != null && originalPaths.ContainsKey(lz)))
                            continue; // reachable only through a threatened flight path
                        // A theoretical LZ must permit real progress this turn.
                        // Cheap endpoint checks on the already-built safe paths;
                        // do not run sensors or pathfinding for a distant LZ.
                        float currentDistance = SectorManager.HexDistance(from, lz);
                        bool canAdvance = false;
                        foreach (Vector3Int step in paths.Keys)
                            if (step != from && step != target && !approachStopBans.Contains(step)
                                && SectorManager.HexDistance(step, lz) < currentDistance)
                            { canAdvance = true; break; }
                        if (!canAdvance) { stalledFutureLzs++; continue; }
                        // Direction only. No remote drop simulation or landing promise.
                        float score = -currentDistance * 100f - threat;
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
                    foreach (PodeDesembarcarOption drop in options)
                    {
                        if (drop.passengerUnit != patient
                            || !IsEvacDropForHospital(patient, drop.disembarkCell, target, snapshot, repairCells)
                            || GetEvacDropThreat(drop.disembarkCell, snapshot.AITeam, threatByCell) > 0f) continue;
                        var orders = MatchEvacPatients(patient, drop.disembarkCell, options, snapshot, repairCells);
                        if (orders.Count == 0) continue;
                        var risk = routeThreats.TryGetValue(lz, out var routeRisk) ? routeRisk : (0f, 0f);
                        var delivery = ScoreEvacDelivery(orders, repairCells, snapshot, threatByCell,
                            risk, GetPathStepCount(paths, lz), snapshot.MyHQ != null
                                ? ScoreRepairHqFallbackCell(drop.disembarkCell, snapshot.MyHQ.CurrentCellPosition, snapshot.AITeam) : 0f,
                            GetAirEvacThreat(lz, snapshot.AITeam, threatByCell));
                        // Hospital drops keep the same zero-threat patient-cell gate in both flight tiers.
                        if (delivery.DropTier != 0) continue;
                        if (bestOrders == null || delivery.Beats(bestDelivery))
                        {
                            bestDelivery = delivery; bestBuilding = building; bestLz = lz; bestDrop = drop;
                            bestOrders = orders; bestRepairCount = delivery.Repairs;
                        }
                    }
                }
                if (!hasFutureLz && stalledFutureLzs > 0)
                    Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC recusa aproximação de {building.name}: {stalledFutureLzs} LZ(s) estimada(s) sem avanço permitido nesta rodada; tenta outro destino");
                // Approach only after both hospital flight tiers have no concrete delivery.
                // No materializable delivery: approach its highest-ranked destination.
                if (approachBuilding == null && hasFutureLz)
                {
                    approachBuilding = building; approachLz = futureLz;
                }
            }

            if (bestDrop != null)
            {
                Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC hospital {(flightTier == 0 ? "rota segura" : "menor ameaca")}: entrega #{patient.InstanceId} em {bestBuilding.name}@{bestDrop.disembarkCell} via LZ={bestLz}, ameacaRota={bestDelivery.Peak:F1}, exposicao={bestDelivery.Total:F1}");
                var selected = bestOrders;
                Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC entrega conjunta: feridos={selected.Count}, em reparo={bestRepairCount}, LZ={bestLz}");
                if (bestLz == from)
                    return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, selected);
                paths.TryGetValue(bestLz, out List<Vector3Int> path);
                return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, selected, bestLz, path);
            }

        }
        paths = safePaths;
        if (approachBuilding == null)
        {
            // P3: no safe approach. Hospitals try the contested tier, by Repair's ranking,
            // with the same approach ruler as the ring, before the ring becomes the last resort.
            foreach (ConstructionManager building in hospitals)
            {
                Vector3Int target = building.CurrentCellPosition; target.z = 0;
                if (!CanSurfaceRepairConstructionServe(building, patient, snapshot.AITeam)
                    || !IsConfirmedVisibleOrExploredCellForAI(target)
                    || !IsEvacDropCellSafe(target, snapshot.AITeam)) continue;
                if (!TryPickEvacApproachMove(transporter, snapshot, from, target, originalPaths, routeThreats,
                        threatByCell, occupied, safeOnly: false, allowEscape: false,
                        out Vector3Int contestedMove, out string info)) continue;
                Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC hospital menor ameaca: aproxima de {building.name} via {contestedMove}, {info}; mantém #{patient.InstanceId} a bordo");
                return BuildMoveBatch(transporter, snapshot.AITeam, from, contestedMove, originalPaths);
            }
            return DecideAirEvacHqFallback(transporter, patient, snapshot, from,
                originalPaths, occupied, dropsByLz, threatByCell, rejectBaseCluster, repairCells);
        }

        Vector3Int move = from;
        if (approachBuilding != null && paths != null)
        {
            var stopBans = new HashSet<Vector3Int>(approachStopBans) { approachBuilding.CurrentCellPosition };
            move = FindAirTransportMove(from, approachLz, paths, stopBans, snapshot.AITeam);
        }
        if (move == from)
        {
            Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC aproximação sem avanço; aciona fallback do HQ para #{patient.InstanceId}");
            return DecideAirEvacHqFallback(transporter, patient, snapshot, from,
                originalPaths, occupied, dropsByLz, threatByCell, rejectBaseCluster, repairCells);
        }
        Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC mantém #{patient.InstanceId} a bordo; "
            + (approachBuilding != null
                ? $"aproxima via {move} para LZ estimada={approachLz}, reparo={approachBuilding.name}"
                : "sem prédio elegível de reparo vago com LZ disponível"));
        return BuildMoveBatch(transporter, snapshot.AITeam, from, move, paths);
    }

    // One decision-local context; direct repair cells stay distinct from walking dropoffs.
    private sealed class EvacRepairCells : Dictionary<int, HashSet<Vector3Int>>
    {
        public readonly Dictionary<int, List<ConstructionManager>> Hospitals = new Dictionary<int, List<ConstructionManager>>();
        public readonly Dictionary<(int patient, Vector3Int hospital), Dictionary<Vector3Int, int>> Walks
            = new Dictionary<(int, Vector3Int), Dictionary<Vector3Int, int>>();
        public readonly HashSet<int> Patients = new HashSet<int>();
        public EvacHqRing HqRing;
        public Dictionary<Vector3Int, (float peak, float total)> RouteThreats;
    }

    private Dictionary<Vector3Int, int> GetEvacHospitalWalk(
        UnitManager patient, Vector3Int hospital, EvacRepairCells context)
    {
        var key = (patient.InstanceId, hospital);
        if (!context.Walks.TryGetValue(key, out var walk))
        {
            walk = GetOrBuildDisembarkPassengerRoute(patient, hospital, Mathf.Max(1, patient.MaxMovementPoints));
            context.Walks[key] = walk;
        }
        return walk;
    }

    private sealed class EvacHqRing
    {
        public HashSet<Vector3Int> Cells = new HashSet<Vector3Int>();
        public HashSet<Vector3Int> Free = new HashSet<Vector3Int>();
        public bool Safe;
    }

    private EvacHqRing BuildEvacHqRing(UnitManager transporter, UnitManager patient,
        AIWorldSnapshot snapshot, bool avoidBaseClusterCells, Dictionary<Vector3Int, float> threatByCell)
    {
        AIDecisionPerf.AddCount("EvacHqRingBuilds");
        if (snapshot.MyHQ == null) return new EvacHqRing();
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
                    if (IsWithinOwnHqRing(neighbor, snapshot) && ring.Add(neighbor)) next.Add(neighbor);
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
            if (IsSafeRepairHqFallbackPosition(patient, cell, snapshot, avoidBaseClusterCells)
                && IsEvacDropCellSafe(cell, snapshot.AITeam)
                && GetEvacDropThreat(cell, snapshot.AITeam, threatByCell) <= 0f)
                safeRing.Add(cell);
        }
        HashSet<Vector3Int> targetRing = safeRing.Count > 0 ? safeRing : freeRing;
        return new EvacHqRing { Cells = targetRing, Free = freeRing, Safe = safeRing.Count > 0 };
    }

    private bool IsEvacDropForHospital(UnitManager passenger, Vector3Int cell, Vector3Int hospital,
        AIWorldSnapshot snapshot, Dictionary<int, HashSet<Vector3Int>> repairCells)
    {
        if (!IsEvacDropCellSafe(cell, snapshot.AITeam)) return false;
        if (cell == hospital) return repairCells.TryGetValue(passenger.InstanceId, out var direct) && direct.Contains(cell);
        if (ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell) != null) return false;
        if (!(repairCells is EvacRepairCells context)) return false;
        var walk = GetEvacHospitalWalk(passenger, hospital, context);
        return walk != null && walk.TryGetValue(cell, out int cost) && cost <= passenger.MaxMovementPoints;
    }

    private bool TryResolveEvacHospitalDrop(UnitManager passenger, Vector3Int cell, AIWorldSnapshot snapshot,
        Dictionary<int, HashSet<Vector3Int>> repairCells, out Vector3Int hospital, out int cost)
    {
        hospital = cell; cost = 0;
        if (!IsEvacDropCellSafe(cell, snapshot.AITeam)) return false;
        if (!(repairCells is EvacRepairCells context))
            return repairCells.TryGetValue(passenger.InstanceId, out var direct) && direct.Contains(cell);
        if (!context.Patients.Contains(passenger.InstanceId)) return false;
        if (!context.Hospitals.TryGetValue(passenger.InstanceId, out var hospitals)) return false;
        foreach (ConstructionManager building in hospitals)
        {
            Vector3Int target = building.CurrentCellPosition; target.z = 0;
            if (!IsEvacDropForHospital(passenger, cell, target, snapshot, repairCells)) continue;
            hospital = target;
            if (cell != target) GetEvacHospitalWalk(passenger, target, context).TryGetValue(cell, out cost);
            return true;
        }
        return false;
    }

    private HashSet<Vector3Int> GetEvacHospitalLzs(UnitManager patient, Vector3Int hospital,
        AIWorldSnapshot snapshot, Dictionary<int, HashSet<Vector3Int>> repairCells)
    {
        var result = new HashSet<Vector3Int>();
        var neighbors = new List<Vector3Int>(6);
        var drops = new HashSet<Vector3Int> { hospital };
        if (repairCells is EvacRepairCells context)
        {
            var walk = GetEvacHospitalWalk(patient, hospital, context);
            if (walk != null) foreach (var pair in walk)
                if (pair.Value <= patient.MaxMovementPoints) drops.Add(pair.Key);
        }
        foreach (Vector3Int drop in drops)
        {
            if (!IsEvacDropForHospital(patient, drop, hospital, snapshot, repairCells)) continue;
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, drop, neighbors);
            foreach (Vector3Int neighbor in neighbors) result.Add(neighbor);
        }
        return result;
    }

    private Dictionary<int, HashSet<Vector3Int>> BuildEvacRepairCells(
        UnitManager transporter, UnitManager primary, AIWorldSnapshot snapshot, Vector3Int from,
        List<ConstructionManager> primaryHospitals = null)
    {
        AIDecisionPerf.AddCount("EvacRepairCellsBuilds");
        var result = new EvacRepairCells {
            HqRing = BuildEvacHqRing(transporter, primary, snapshot,
                !IsEliteRepairUnit(primary) && IsBaseUnderPressureForRepair(snapshot),
                new EvacThreatMap(transporter, snapshot))
        };
        foreach (UnitManager patient in CollectPassengers(transporter))
        {

            var cells = new HashSet<Vector3Int>();
            result[patient.InstanceId] = cells;
            if (patient != primary && !patient.IsUnderRepair) continue;
            result.Patients.Add(patient.InstanceId);
            var hospitals = patient == primary && primaryHospitals != null ? primaryHospitals
                : RankRepairConstructions(patient, from, snapshot.AITeam, new HashSet<Vector3Int>(),
                    rejectBaseCluster: !IsEliteRepairUnit(patient) && IsBaseUnderPressureForRepair(snapshot),
                    lineSnapshot: snapshot, log: false, allowOccupiedDestinations: true);
            result.Hospitals[patient.InstanceId] = new List<ConstructionManager>();
            foreach (var building in hospitals)
            {
                Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
                if (!CanSurfaceRepairConstructionServe(building, patient, snapshot.AITeam)
                    || !IsEvacDropCellSafe(cell, snapshot.AITeam)) continue;
                result.Hospitals[patient.InstanceId].Add(building);
                if (OccupancyResolver.CanEndMove(patient, cell,
                    UnitOccupancyRules.GetUnitsAtCell(boardTilemap, cell, transporter))) cells.Add(cell);
            }
        }
        return result;
    }

    private List<PodeDesembarcarOption> MatchEvacPatients(
        UnitManager primary, Vector3Int primaryCell, List<PodeDesembarcarOption> options,
        AIWorldSnapshot snapshot, Dictionary<int, HashSet<Vector3Int>> repairCells,
        HashSet<Vector3Int> finalRing = null, bool requireSafeOutsideRing = false)
    {
        var hqRing = (repairCells as EvacRepairCells)?.HqRing;
        if (finalRing == null && hqRing != null && hqRing.Cells.Contains(primaryCell))
        {
            finalRing = hqRing.Cells;
            requireSafeOutsideRing = hqRing.Safe;
        }
        var refusals = new Dictionary<string, int>();
        bool Reject(string reason)
        {
            refusals.TryGetValue(reason, out int count); refusals[reason] = count + 1;
            return false;
        }
        bool NeedsTreatment(UnitManager passenger) => repairCells is EvacRepairCells context
            ? context.Patients.Contains(passenger.InstanceId) : passenger == primary || passenger.IsUnderRepair;
        var secondaryWalkRoutes = new Dictionary<int, Dictionary<Vector3Int, int>>();
        bool Resolve(UnitManager passenger, Vector3Int cell, out Vector3Int target, out int routeCost)
        {
            target = cell; routeCost = 0;
            if (!repairCells.ContainsKey(passenger.InstanceId)) return false;
            if (plannedDestinations != null && plannedDestinations.Contains(cell))
                return finalRing != null ? Reject("estacionamento") : false;
            ConstructionManager dropBuilding = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            // Healthy companions use ground and never compete for a patient's hospital.
            if (!NeedsTreatment(passenger) && dropBuilding != null)
                return finalRing != null ? Reject("hospital reservado a ferido") : false;
            if (passenger == primary && cell != primaryCell) return false;
            if (finalRing != null)
            {
                // Each passenger must satisfy the patient's parking policy too;
                // the primary's accepted ring never grants a closed producer to another unit.
                if (!IsConfirmedVisibleOrExploredCellForAI(cell)) return Reject("nevoa");
                if (!IsRepairHqFallbackCellAllowed(passenger, cell, snapshot.AITeam,
                        !IsEliteRepairUnit(passenger) && IsBaseUnderPressureForRepair(snapshot))
                    || !OccupancyResolver.CanEndMove(passenger, cell,
                        UnitOccupancyRules.GetUnitsAtCell(boardTilemap, cell, passenger.EmbarkedTransporter))) return Reject("estacionamento");
                // A hospital next to this LZ is still useful beyond the accepted HQ ring.
                if (dropBuilding != null && NeedsTreatment(passenger)
                    && TryResolveEvacHospitalDrop(passenger, cell, snapshot, repairCells, out var hospital, out routeCost)
                    && hospital == cell && IsEvacDropCellSafe(cell, snapshot.AITeam)) return true;
                if (finalRing.Contains(cell)) return true;
                if (requireSafeOutsideRing && !IsEvacDropCellSafe(cell, snapshot.AITeam))
                    return Reject("fora do anel seguro");
                // The primary anchors the HQ delivery. The second patient may
                // use adjacent free ground and walk the last tactical leg;
                // do not require both drop cells to fit the exact same ring.
                if (dropBuilding != null) return Reject("construcao sem atendimento");
                if (!secondaryWalkRoutes.TryGetValue(passenger.InstanceId, out var walk))
                {
                    walk = GetOrBuildDisembarkPassengerRoute(
                        passenger, primaryCell, Mathf.Max(1, passenger.MaxMovementPoints));
                    secondaryWalkRoutes[passenger.InstanceId] = walk;
                }
                return (walk != null && walk.TryGetValue(cell, out int cost)
                    && cost <= passenger.MaxMovementPoints) || Reject("fora do anel seguro/sem trecho tatico");
            }
            return TryResolveEvacHospitalDrop(passenger, cell, snapshot, repairCells, out target, out routeCost);
        }
        var match = MelhorDesembarqueService.MatchOptions(new MelhorDesembarqueRequest {
            requiredPassengerInstanceId = primary.InstanceId,
            resolvePassengerTarget = Resolve,
            disembarkPreferenceCost = (passenger, cell) => {
                return ResolveEvacDisembarkPreference(passenger, cell, snapshot, repairCells,
                    NeedsTreatment(passenger));
            }
        }, options);
        if (finalRing != null && refusals.Count > 0)
        {
            var reasons = new List<string>();
            foreach (var refusal in refusals) reasons.Add($"{refusal.Key}={refusal.Value}");
            Debug.Log($"{TL("Transporte")} EVAC matching anel #{primary.InstanceId}@{primaryCell}: recusas {string.Join(", ", reasons)}");
        }
        return match.spots.ConvertAll(spot => spot.option);
    }

    // Author 2026-11-14 (P1): the TRANSPORT's threat at a cell is the HP the visible enemies
    // that can reach it would take, per AICombatHpSimulator (the HP Matrix tables). A rifle
    // takes 2 of a full Chinook, an AAA takes 7; proximity weighed them the same.
    // The decision-local cache carries the subject, so every transport reader shares one ruler.
    private sealed class EvacThreatMap : Dictionary<Vector3Int, float>
    {
        public readonly UnitManager Transporter;
        public readonly AIWorldSnapshot Snapshot;
        public List<(UnitReachEnvelope envelope, float damage)> Sources;
        public bool SourcesBuilt;
        public EvacThreatMap(UnitManager transporter, AIWorldSnapshot snapshot)
        { Transporter = transporter; Snapshot = snapshot; }
        public int TransporterHp => Transporter != null ? Mathf.Max(1, Transporter.CurrentHP) : int.MaxValue;
    }

    private const int EvacThreatMaxSimulatedDistance = 5;

    private float GetAirEvacThreat(Vector3Int cell, TeamId team, Dictionary<Vector3Int, float> cache)
    {
        cell.z = 0;
        if (!cache.TryGetValue(cell, out float threat))
        {
            var map = cache as EvacThreatMap;
            if (map != null && !map.SourcesBuilt)
            {
                map.Sources = BuildEvacThreatSources(map);
                map.SourcesBuilt = true;
            }
            if (map?.Sources != null)
            {
                threat = 0f;
                foreach (var source in map.Sources)
                    if (source.envelope.AttackableCells.Contains(cell)) threat += source.damage;
            }
            else
                // No transport subject, no visible-enemy list or no HP Matrix tables:
                // "unknown" must never read as "safe", so keep the proximity ruler.
                threat = CalculateThreatLevel(cell, team);
            cache[cell] = threat;
        }
        return threat;
    }

    // Once per EVAC decision: each visible enemy that could reach the transport's area,
    // its next-turn combat envelope, and the worst HP it takes from this transport.
    private List<(UnitReachEnvelope envelope, float damage)> BuildEvacThreatSources(EvacThreatMap map)
    {
        AIDecisionPerf.AddCount("EvacThreatSourceBuilds");
        UnitManager transporter = map.Transporter;
        // null = matrix unavailable; the caller falls back to proximity instead of reading zero.
        if (transporter == null || map.Snapshot?.EnemyUnits == null
            || !transporter.TryGetUnitData(out UnitData transportData) || transportData == null
            || turnStateManager == null || turnStateManager.RpsDatabaseRef == null) return null;
        var sources = new List<(UnitReachEnvelope, float)>();
        Vector3Int origin = transporter.CurrentCellPosition; origin.z = 0;
        int hp = Mathf.Max(1, transporter.CurrentHP);
        foreach (UnitManager enemy in map.Snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked
                || !enemy.TryGetUnitData(out UnitData enemyData) || enemyData == null) continue;
            // Cheap reach prefilter: the enemy's next move plus a weapon margin must touch
            // the transport's own flight this turn. No envelope for enemies across the map.
            Vector3Int enemyCell = enemy.CurrentCellPosition; enemyCell.z = 0;
            if (SectorManager.HexDistance(enemyCell, origin) > transporter.MaxMovementPoints
                + enemy.MaxMovementPoints + EvacThreatMaxSimulatedDistance) continue;
            float damage = 0f;
            for (int distance = 1; distance <= EvacThreatMaxSimulatedDistance; distance++)
            {
                AICombatHpSimulator.AICombatHpResult result = AICombatHpSimulator.Simulate(
                    enemyData, transportData, Mathf.Max(1, enemy.CurrentHP), hp, distance,
                    turnStateManager.RpsDatabaseRef, turnStateManager.DpqMatchupDatabaseRef,
                    turnStateManager.WeaponPriorityDataRef);
                if (result.isValid) damage = Mathf.Max(damage, hp - result.defenderHpAfter);
            }
            if (damage <= 0f) continue;
            UnitReachEnvelope envelope = AIAirExposureService.BuildEnvelope(enemy, boardTilemap,
                terrainDatabase, ReachIntent.Combat, Mathf.Max(1, enemy.MaxMovementPoints));
            if (envelope == null) continue;
            sources.Add((envelope, damage));
        }
        return sources;
    }

    // The PASSENGER's drop cell keeps the proximity ruler (P4: only the transport changed).
    private float GetEvacDropThreat(Vector3Int cell, TeamId team, Dictionary<Vector3Int, float> cache)
    {
        cell.z = 0;
        // In the transport map the cell key holds transport HP, so the passenger uses its own
        // key there; a plain single-subject cache keeps the shared key it always had.
        var key = cache is EvacThreatMap ? new Vector3Int(cell.x, cell.y, int.MinValue) : cell;
        if (!cache.TryGetValue(key, out float threat))
        {
            threat = CalculateThreatLevel(cell, team);
            cache[key] = threat;
        }
        return threat;
    }

    // Survival is an HP question: without the matrix the proximity score is not HP, so it never gates.
    private bool EvacTransportSurvives(float damage, Dictionary<Vector3Int, float> cache)
        => !(cache is EvacThreatMap map) || map.Sources == null || damage < map.TransporterHp;

    // P2, one approach ruler for hospital and HQ ring. The transport is shot on the enemy turn
    // at the cell where it ends, so survival is that cell's predicted damage, not a route sum.
    // Survivors: progress, then less damage, then exposure. Nobody survives: less damage first.
    // In the safe tier every cell has zero damage, which reduces to the old progress/exposure order.
    private bool TryPickEvacApproachMove(UnitManager transporter, AIWorldSnapshot snapshot,
        Vector3Int from, Vector3Int anchor, Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, (float peak, float total)> routeThreats,
        Dictionary<Vector3Int, float> threatByCell, HashSet<Vector3Int> occupied,
        bool safeOnly, bool allowEscape, out Vector3Int move, out string info)
    {
        move = from; info = string.Empty;
        if (paths == null || routeThreats == null) return false;
        bool moveSurvives = false;
        float moveDamage = float.MaxValue, moveDistance = float.MaxValue, moveTotal = float.MaxValue;
        float originDistance = SectorManager.HexDistance(from, anchor);
        for (int escape = 0; escape < (allowEscape ? 2 : 1) && move == from; escape++)
            foreach (var pair in paths)
            {
                Vector3Int cell = pair.Key;
                if (!routeThreats.TryGetValue(cell, out var risk)) continue;
                float dist = SectorManager.HexDistance(cell, anchor);
                if (cell == from || (safeOnly && risk.peak > 0f)
                    || (escape == 0 && dist >= originDistance)
                    || (occupied != null && occupied.Contains(cell))
                    || (plannedDestinations != null && plannedDestinations.Contains(cell))
                    || ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell) != null
                    || !CanEvacTransportStopAtCell(transporter, cell)) continue;
                float damage = GetAirEvacThreat(cell, snapshot.AITeam, threatByCell);
                bool survives = EvacTransportSurvives(damage, threatByCell);
                // Author: "fly fastest and farthest out of the threat zone, still toward base,
                // the edge". A zero-damage cell beats any damaged one; among them, progress.
                bool clear = damage <= 0f;
                bool better;
                if (move == from) better = true;
                else if (clear != (moveDamage <= 0f)) better = clear;
                else if (survives != moveSurvives) better = survives;
                else if (survives)
                    better = dist < moveDistance || (dist == moveDistance
                        && (damage < moveDamage || (damage == moveDamage && risk.total < moveTotal)));
                else
                    better = damage < moveDamage || (damage == moveDamage
                        && (dist < moveDistance || (dist == moveDistance && risk.total < moveTotal)));
                if (!better) continue;
                move = cell; moveSurvives = survives; moveDamage = damage;
                moveDistance = dist; moveTotal = risk.total;
            }
        if (move == from) return false;
        int hp = threatByCell is EvacThreatMap map ? map.TransporterHp : 0;
        info = $"avanço={originDistance - moveDistance:F0}h, passos={Mathf.Max(0, GetPathStepCount(paths, move) - 1)}, "
            + $"dano previsto={moveDamage:F0}/{hp} HP, sobrevive={(moveSurvives ? "sim" : "não")}, exposição={moveTotal:F1}";
        return true;
    }

    private Dictionary<Vector3Int, (float peak, float total)> BuildEvacRouteThreats(
        Vector3Int from, Dictionary<Vector3Int, List<Vector3Int>> paths,
        TeamId team, Dictionary<Vector3Int, float> threatByCell)
    {
        AIDecisionPerf.AddCount("EvacRouteThreatBuilds");
        paths ??= new Dictionary<Vector3Int, List<Vector3Int>>();
        var routeThreats = new Dictionary<Vector3Int, (float peak, float total)>();
        foreach (var pair in paths)
        {
            float peak = GetAirEvacThreat(pair.Key, team, threatByCell);
            float total = peak;
            if (pair.Value != null)
                foreach (Vector3Int cell in pair.Value)
                {
                    if (cell == from || cell == pair.Key) continue;
                    float threat = GetAirEvacThreat(cell, team, threatByCell);
                    peak = Mathf.Max(peak, threat); total += threat;
                }
            routeThreats[pair.Key] = (peak, total);
        }
        return routeThreats;
    }

    // One ordinal preference for matching and comparison between deliveries.
    // Own force: direct 0 / walking 1; other force: direct 2 / walking 3; ground 4.
    private int ResolveEvacDisembarkPreference(UnitManager passenger, Vector3Int cell,
        AIWorldSnapshot snapshot, Dictionary<int, HashSet<Vector3Int>> repairCells, bool needsTreatment)
    {
        int safety = IsEvacDropCellSafe(cell, snapshot.AITeam) ? 0 : 10000;
        if (!needsTreatment) return safety;
        if (repairCells is EvacRepairCells context
            && TryResolveEvacHospitalDrop(passenger, cell, snapshot, repairCells,
                out Vector3Int hospital, out _)
            && context.Hospitals.TryGetValue(passenger.InstanceId, out var hospitals))
        {
            var building = hospitals.Find(candidate => candidate.CurrentCellPosition == hospital);
            int affinity = IsPreferredRepairFacility(passenger, building) ? 0 : 2;
            return safety + affinity + (cell == hospital ? 0 : 1);
        }
        // Plain facts are accepted by the isolated HQ fixtures too.
        if (repairCells.TryGetValue(passenger.InstanceId, out var cells) && cells.Contains(cell))
            return safety + (IsPreferredRepairFacility(passenger,
                ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell)) ? 0 : 2);
        return safety + 4;
    }

    private struct EvacDeliveryScore
    {
        public int Count, DropTier, Repairs, Preference, Cost;
        public float DropThreat, Peak, Total, Score, LzDamage;
        public bool Survives;
        public bool Beats(EvacDeliveryScore other)
        {
            if (Count != other.Count) return Count > other.Count;
            if (DropTier != other.DropTier) return DropTier < other.DropTier;
            if (DropThreat != other.DropThreat) return DropThreat < other.DropThreat;
            if (Preference != other.Preference) return Preference < other.Preference;
            // P2: the transport takes fire on the enemy turn, at the LZ where it ends.
            if (Survives != other.Survives) return Survives;
            if (LzDamage != other.LzDamage) return LzDamage < other.LzDamage;
            if (Peak != other.Peak) return Peak < other.Peak;
            if (Total != other.Total) return Total < other.Total;
            if (Cost != other.Cost) return Cost < other.Cost;
            return Score > other.Score;
        }
    }

    private EvacDeliveryScore ScoreEvacDelivery(List<PodeDesembarcarOption> orders,
        Dictionary<int, HashSet<Vector3Int>> repairCells, AIWorldSnapshot snapshot,
        Dictionary<Vector3Int, float> threatByCell, (float peak, float total) risk, int cost, float score,
        float lzDamage)
    {
        // LZ damage only orders deliveries when it is HP from the matrix; the proximity
        // fallback keeps the old route-peak order.
        bool matrix = threatByCell is EvacThreatMap map && map.Sources != null;
        var result = new EvacDeliveryScore { Count = orders.Count, Peak = risk.peak,
            Total = risk.total, Cost = cost, Score = score, LzDamage = matrix ? lzDamage : 0f,
            Survives = EvacTransportSurvives(lzDamage, threatByCell) };
        foreach (var order in orders)
        {
            bool needsTreatment = repairCells is EvacRepairCells context
                ? context.Patients.Contains(order.passengerUnit.InstanceId) : order.passengerUnit.IsUnderRepair;
            if (needsTreatment)
                result.Preference += ResolveEvacDisembarkPreference(order.passengerUnit, order.disembarkCell,
                    snapshot, repairCells, needsTreatment: true);
            result.DropThreat = Mathf.Max(result.DropThreat, GetEvacDropThreat(order.disembarkCell, snapshot.AITeam, threatByCell));
            if (!IsEvacDropCellSafe(order.disembarkCell, snapshot.AITeam)
                || GetEvacDropThreat(order.disembarkCell, snapshot.AITeam, threatByCell) > 0f) result.DropTier = 1;
            if (repairCells.TryGetValue(order.passengerUnit.InstanceId, out var cells)
                && cells.Contains(order.disembarkCell)) result.Repairs++;
        }
        return result;
    }

    private bool CanEvacTransportStopAtCell(UnitManager transporter, Vector3Int cell)
        => IsAirTransporter(transporter) ? CanAirTransportStopAtCell(transporter, cell)
            : OccupancyResolver.CanEndMove(transporter, cell,
                UnitOccupancyRules.GetUnitsAtCell(boardTilemap, cell, transporter));

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
        var hqRing = (repairCells as EvacRepairCells)?.HqRing
            ?? BuildEvacHqRing(transporter, patient, snapshot, avoidBaseClusterCells, threatByCell);
        var targetRing = hqRing.Cells;
        var freeRing = hqRing.Free;
        bool safeHqRing = hqRing.Safe;
        string tier = safeHqRing ? "anel seguro" : "anel contestado, menor ameaça";
        Vector3Int anchor = hq;
        float anchorThreat = float.MaxValue, anchorScore = float.MinValue;
        foreach (Vector3Int cell in targetRing)
        {
            float threat = GetEvacDropThreat(cell, snapshot.AITeam, threatByCell);
            float score = ScoreRepairHqFallbackCell(cell, hq, snapshot.AITeam);
            if (threat < anchorThreat || (threat == anchorThreat && score > anchorScore))
            {
                anchorThreat = threat; anchorScore = score; anchor = cell;
            }
        }
        if (paths == null) paths = new Dictionary<Vector3Int, List<Vector3Int>>();
        var routeThreats = (repairCells as EvacRepairCells)?.RouteThreats
            ?? BuildEvacRouteThreats(from, paths, snapshot.AITeam, threatByCell);

        // Patient cell threat wins first, then flight peak/total threat.
        // A zero-threat route wins within the selected patient-cell tier.
        // Nearby enemies never veto this final fallback.
        {
            PodeDesembarcarOption bestDrop = null;
            List<PodeDesembarcarOption> bestOrders = null;
            Vector3Int bestLz = from;
            EvacDeliveryScore bestDelivery = default;
            foreach (var pair in paths)
            {
                Vector3Int lz = pair.Key;
                var risk = routeThreats[lz];
                if (targetRing.Count == 0 || SectorManager.HexDistance(lz, hq) > DefenseEnemyRange + 1
                    || (occupied != null && occupied.Contains(lz) && lz != from)
                    || (plannedDestinations != null && plannedDestinations.Contains(lz) && lz != from)
                    || ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, lz) != null
                    || !CanEvacTransportStopAtCell(transporter, lz)) continue;
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
                    float score = ScoreRepairHqFallbackCell(option.disembarkCell, hq, snapshot.AITeam);
                    var orders = MatchEvacPatients(patient, option.disembarkCell, options, snapshot, repairCells, targetRing,
                        requireSafeOutsideRing: safeHqRing);
                    if (orders.Count == 0) continue;
                    var delivery = ScoreEvacDelivery(orders, repairCells, snapshot, threatByCell, risk, cost, score,
                        GetAirEvacThreat(lz, snapshot.AITeam, threatByCell));
                    if (bestOrders == null || delivery.Beats(bestDelivery))
                    {
                        bestDrop = option; bestOrders = orders; bestLz = lz; bestDelivery = delivery;
                    }
                }
            }
            if (bestDrop != null)
            {
                Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC {tier}: libera #{patient.InstanceId} @ {bestDrop.disembarkCell}, LZ={bestLz}, degrauDescida={bestDelivery.DropTier}, em reparo={bestDelivery.Repairs}, ameaçaRota={bestDelivery.Peak:F1}");
                var selected = bestOrders;
                Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC {tier}: entrega conjunta de {selected.Count} ferido(s)");
                if (bestLz == from) return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, selected);
                return BuildDesembarcarBatch(transporter, snapshot.AITeam, from, selected, bestLz, paths[bestLz]);
            }

        }
        // A materializable delivery wins before any approach, including when
        // its route only becomes available in the least-threat flight tier.
        bool originThreatened = GetAirEvacThreat(from, snapshot.AITeam, threatByCell) > 0f;
        for (int flightTier = 0; flightTier < 2; flightTier++)
        {
            // Progress to the ring first. If trapped on a threatened origin,
            // allow escape in any direction instead of returning a wait action.
            if (TryPickEvacApproachMove(transporter, snapshot, from, anchor, paths, routeThreats,
                    threatByCell, occupied, safeOnly: flightTier == 0, allowEscape: originThreatened,
                    out Vector3Int move, out string info))
            {
                Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC {tier}: aproxima/escapa via {move}, {info}; paciente a bordo até desembarque materializável");
                return BuildMoveBatch(transporter, snapshot.AITeam, from, move, paths);
            }
        }
        Debug.Log($"{TL("Transporte")} {transporter.InstanceId} EVAC {tier}: sem movimento ou desembarque fisicamente permitido; células livres={freeRing.Count}");
        return BuildMoveBatch(transporter, snapshot.AITeam, from, from, paths);
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
