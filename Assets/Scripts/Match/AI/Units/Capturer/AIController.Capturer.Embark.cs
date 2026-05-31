using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Intercepção de embarque — capturador embarca em transporte no alcance
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideCapturerEmbarkAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!unit.TryGetUnitData(out UnitData data) || data?.roles == null || data.roles.Count == 0
            || !data.roles.Contains(UnitRole.Capturador)) return null;

        // Primary capturer: strict sector alignment (don't board a wrong-direction APC).
        // Secondary capturer (e.g. Assalto+Capturador): can board any APC that has no formal
        // passenger — it is acting as shuttle and will reorient to the passenger's objective.
        bool isPrimaryCapturador = data.roles[0] == UnitRole.Capturador;

        // Pass 1: sensor padrão — encontra transporters adjacentes (1h)
        var options = new List<PodeEmbarcarOption>();
        PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
            Mathf.Max(0, unit.RemainingMovementPoints), options);

        // capturerAssigned: slot de capturador exclusivo — usado para o skip de embarque.
        // Rogues (sem slot de capturador) recebem null e nunca pulam o embarque por
        // "estar perto do objetivo", pois seu destino real é o HQ inimigo, não o setor.
        SectorObjective capturerAssigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;

        // assigned: ampliado para multi-role (e.g. Assalto+Capturador) que não têm slot de
        // capturador mas têm atribuição em outro role — usado para sector match do APC.
        SectorObjective assigned = capturerAssigned;
        if (assigned == null && plan != null)
            assigned = ResolveAnyAssignedObjective(unit, plan);

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        if (ShouldSkipCapturerEmbarkForShortWalk(unit, capturerAssigned, fromCell, "origem"))
            return null;

        // Não embarcar em transporters ainda no aeroporto/fábrica — espera sair primeiro.
        options.RemoveAll(opt =>
        {
            if (opt?.transporterUnit == null) return false;
            Vector3Int tc = opt.transporterUnit.CurrentCellPosition; tc.z = 0;
            return IsTeamProductionBuilding(tc, unit.TeamId)
                && !IsAirTransporter(opt.transporterUnit);
        });

        PodeEmbarcarOption best = null;
        int bestPriority = int.MaxValue;
        float bestDistance = float.MaxValue;

        if (options.Count > 0)
        {
            foreach (PodeEmbarcarOption opt in options)
            {
                if (!TryGetCapturerEmbarkPreference(unit, assigned, opt, plan, snapshot.AITeam,
                        out int priority, out float distance))
                    continue;

                if (priority < bestPriority
                    || (priority == bestPriority && distance < bestDistance))
                {
                    best = opt;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }
        }

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        if (best != null && bestPriority == 0)
        {
            if (ShouldYieldEmbarkToNeedierCapturer(unit, best.transporterUnit, assigned, plan))
                return null;
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca → {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
        }

        // Pass 2: simula PodeEmbarcarSensor em cada hex candidato (ficar parado + hexes alcançáveis).
        // Pass 2a: exige transporter formalmente pareado com este passageiro.
        // Pass 2b: exige transporter do mesmo setor do plano.
        // Pass 2c: aceita transporter livre (sem passageiro formal).
        // Pass 3: overflow — embarca em qualquer transporter com slot físico livre (último recurso).
        if (paths == null || paths.Count == 0)
        {
            Debug.Log(BuildCapturerEmbarkScanDebug(unit, data, assigned, plan, snapshot,
                fromCell, options.Count, best, bestPriority, "sem paths"));
            return null;
        }
        PlayerAction formalExtendedEmbark =
            TryBuildExtendedEmbarkBatch(unit, data, snapshot, plan, assigned, fromCell, paths, requireFormalPassenger: true);
        if (formalExtendedEmbark != null) return formalExtendedEmbark;

        if (best != null)
        {
            if (ShouldYieldEmbarkToNeedierCapturer(unit, best.transporterUnit, assigned, plan))
                return null;
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca fallback p{bestPriority} â†’ {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
        }

        PlayerAction extendedEmbark =
            TryBuildExtendedEmbarkBatch(unit, data, snapshot, plan, assigned, fromCell, paths, requireSectorMatch: true)
            ?? TryBuildExtendedEmbarkBatch(unit, data, snapshot, plan, assigned, fromCell, paths, requireSectorMatch: false)
            ?? TryBuildExtendedEmbarkBatch(unit, data, snapshot, plan, assigned, fromCell, paths, requireSectorMatch: false, allowOverflow: true);
        if (extendedEmbark != null) return extendedEmbark;

        // Rogue capturer: extended embark failed — move toward nearest rogue transporter so
        // it enters embark range next turn. Only applies when there is no sector assignment
        // (rogues march to enemy HQ; boarding any rogue transport accelerates the push).
        if (assigned == null)
        {
            UnitManager rogueTransport = FindNearestRogueTransporter(unit, data, plan, snapshot);
            if (rogueTransport != null)
            {
                Vector3Int tCell = rogueTransport.CurrentCellPosition; tCell.z = 0;
                HashSet<Vector3Int> occ = BuildOccupied(unit);
                Vector3Int moveTarget = FindTransportMove(unit, fromCell, tCell, paths, occ, snapshot.AITeam);
                if (moveTarget != fromCell)
                {
                    Debug.Log($"{TL("Capturador")} {unit.InstanceId} rogue — avança para transporte rogue {rogueTransport.InstanceId}@{tCell} via {moveTarget}");
                    return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
                }
            }
        }
        Debug.Log(BuildCapturerEmbarkScanDebug(unit, data, assigned, plan, snapshot,
            fromCell, options.Count, best, bestPriority, "sem embarque valido"));
        return null;
    }

    private string BuildCapturerEmbarkScanDebug(
        UnitManager unit,
        UnitData unitData,
        SectorObjective assigned,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        int adjacentOptions,
        PodeEmbarcarOption best,
        int bestPriority,
        string reason)
    {
        var sb = new System.Text.StringBuilder();
        string sector = assigned != null ? assigned.Sector.ToString() : "rogue";
        sb.AppendLine($"{TL("Capturador")} {unit.InstanceId} embarque scan: assigned={sector} reason={reason} adjacentOptions={adjacentOptions} best={(best?.transporterUnit != null ? best.transporterUnit.InstanceId.ToString() : "-")} p={(bestPriority == int.MaxValue ? "-" : bestPriority.ToString())}");

        int listed = 0;
        foreach (UnitManager t in UnitManager.AllActive)
        {
            if (t == null || t == unit || t.TeamId != unit.TeamId || t.IsDead || t.IsEmbarked) continue;
            if (!t.TryGetUnitData(out UnitData tData) || tData == null || !tData.isTransporter) continue;

            Vector3Int tCell = t.CurrentCellPosition; tCell.z = 0;
            float dist = SectorManager.HexDistance(fromCell, tCell);
            if (dist > 8f) continue;

            SectorObjective tObj = plan != null ? ResolveAssignedTransportObjective(t, plan) : null;
            UnitManager formalPassenger = tObj != null ? ResolveAssignedPassengerUnit(tObj, unit.TeamId) : null;
            bool sameSector = assigned != null && tObj != null && tObj.Sector == assigned.Sector;
            bool compatibleSector = assigned != null && tObj != null
                && AreEmbarkSectorsCompatible(assigned.Sector, tObj.Sector);
            bool production = IsTeamProductionBuilding(tCell, unit.TeamId);
            int slot = FindFittingSlotIndex(t, tData, unit, unitData);
            string cargo = HasTransportCargo(t) ? "cargo" : "empty";

            sb.AppendLine($"  heli/trans {t.InstanceId}@{tCell} dist={dist:F0} sector={(tObj != null ? tObj.Sector.ToString() : "free")} formal={(formalPassenger != null ? formalPassenger.InstanceId.ToString() : "-")} slot={slot} same={sameSector} compat={compatibleSector} prod={production} acted={t.HasActed} repair={t.IsUnderRepair} {cargo}");
            listed++;
        }

        if (listed == 0)
            sb.AppendLine("  nenhum transporte aliado <=8h");

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Pass 2: simula o sensor em cada hex candidato para achar embarque válido
    // -------------------------------------------------------------------------

    private bool TryGetCapturerEmbarkPreference(
        UnitManager unit,
        SectorObjective assigned,
        PodeEmbarcarOption option,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out int priority,
        out float distance)
    {
        priority = int.MaxValue;
        distance = float.MaxValue;

        UnitManager transporter = option != null ? option.transporterUnit : null;
        if (unit == null || transporter == null || transporter.IsUnderRepair)
            return false;

        SectorObjective transporterObjective = plan != null
            ? ResolveAssignedTransportObjective(transporter, plan)
            : null;

        Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
        Vector3Int transporterCell = transporter.CurrentCellPosition; transporterCell.z = 0;
        distance = SectorManager.HexDistance(unitCell, transporterCell);

        if (assigned == null)
        {
            if (transporterObjective != null
                && !CanRogueUseAssignedTransporter(unit, transporter, transporterObjective, aiTeam))
                return false;

            priority = transporterObjective == null ? 0 : 1;
            return true;
        }

        bool sameSector = transporterObjective != null && transporterObjective.Sector == assigned.Sector;
        bool compatibleSector = transporterObjective != null
            && AreEmbarkSectorsCompatible(assigned.Sector, transporterObjective.Sector);
        UnitManager formalPassenger = transporterObjective != null
            ? ResolveAssignedPassengerUnit(transporterObjective, aiTeam)
            : null;
        bool formalMatch = sameSector && formalPassenger == unit;
        bool freeTransport = transporterObjective == null;
        bool compatibleFreeTransport = transporterObjective != null
            && compatibleSector
            && formalPassenger == null;

        if (formalMatch)
            priority = 0;
        else if (sameSector)
            priority = 1;
        else if (compatibleFreeTransport)
            priority = 2;
        else if (freeTransport)
            priority = 3;
        else
            return false;

        return true;
    }

    private static bool AreEmbarkSectorsCompatible(ConstructionSector assignedSector, ConstructionSector transportSector)
    {
        if (assignedSector == transportSector)
            return true;

        if (SectorManager.TryGetSectorInfo(assignedSector, out SectorManager.SectorInfo assignedInfo)
            && assignedInfo != null
            && (assignedInfo.ClosestNeighbor1 == transportSector || assignedInfo.ClosestNeighbor2 == transportSector))
            return true;

        if (SectorManager.TryGetSectorInfo(transportSector, out SectorManager.SectorInfo transportInfo)
            && transportInfo != null
            && (transportInfo.ClosestNeighbor1 == assignedSector || transportInfo.ClosestNeighbor2 == assignedSector))
            return true;

        return false;
    }

    private PlayerAction TryBuildExtendedEmbarkBatch(
        UnitManager unit, UnitData unitData, AIWorldSnapshot snapshot, TeamObjectivePlan plan,
        SectorObjective assigned, Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        bool requireSectorMatch = false, bool allowOverflow = false, bool requireFormalPassenger = false)
    {
        // Usa os paths completos e calcula a sobra real por caminho. Reservar 1 PM
        // antecipadamente quebra casos em que o passageiro anda 2 casas e embarca na 3a.
        var movePaths = paths;

        var neighborBuf = new List<Vector3Int>(6);
        var pickupBuf = new List<Vector3Int>(12);
        var seenPickupCells = new HashSet<Vector3Int>();

        // 1) Ficar parado em fromCell — MP completo disponível para embarque
        CollectEmbarkTargetCells(fromCell, unit, neighborBuf, pickupBuf, seenPickupCells);
        foreach (Vector3Int tCell in pickupBuf)
        {
            if (TryEmbarkFromHex(fromCell, null, unit.RemainingMovementPoints,
                    tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction a, requireSectorMatch, allowOverflow, requireFormalPassenger))
                return a;
        }

        // 2) Hexes alcançáveis — simula sensor de cada um com a sobra real de PM
        if (movePaths == null) return null;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        PlayerAction directTransporterEmbark = TryBuildDirectTransporterExtendedEmbarkBatch(
            unit, unitData, snapshot, plan, assigned, fromCell, movePaths, occupied,
            neighborBuf, requireSectorMatch, allowOverflow, requireFormalPassenger);
        if (directTransporterEmbark != null)
            return directTransporterEmbark;

        foreach (var kvp in movePaths)
        {
            Vector3Int hex = kvp.Key;
            if (hex == fromCell) continue;
            if (occupied.Contains(hex)) continue; // unidade não pode parar num hex ocupado

            CollectEmbarkTargetCells(hex, unit, neighborBuf, pickupBuf, seenPickupCells);
            foreach (Vector3Int tCell in pickupBuf)
            {
                int remainingMPAtHex = CalculateRemainingMovementAfterPath(unit, kvp.Value);
                if (remainingMPAtHex <= 0) continue;

                if (TryEmbarkFromHex(hex, kvp.Value, remainingMPAtHex,
                        tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction a, requireSectorMatch, allowOverflow, requireFormalPassenger))
                    return a;
            }
        }

        return null;
    }

    private PlayerAction TryBuildDirectTransporterExtendedEmbarkBatch(
        UnitManager unit,
        UnitData unitData,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        SectorObjective assigned,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> movePaths,
        HashSet<Vector3Int> occupied,
        List<Vector3Int> neighborBuf,
        bool requireSectorMatch,
        bool allowOverflow,
        bool requireFormalPassenger)
    {
        foreach (UnitManager transporter in UnitManager.AllActive)
        {
            if (transporter == null || transporter == unit || transporter.TeamId != unit.TeamId)
                continue;
            if (transporter.IsDead || transporter.IsEmbarked || transporter.IsUnderRepair)
                continue;
            if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null || !transporterData.isTransporter)
                continue;
            if (FindFittingSlotIndex(transporter, transporterData, unit, unitData) < 0)
                continue;

            Vector3Int tCell = transporter.CurrentCellPosition;
            tCell.z = 0;
            float distFromOrigin = SectorManager.HexDistance(fromCell, tCell);
            if (distFromOrigin > unit.RemainingMovementPoints + 0.5f)
                continue;

            neighborBuf.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, tCell, neighborBuf);
            neighborBuf.Sort((a, b) =>
            {
                Vector3Int ca = a; ca.z = 0;
                Vector3Int cb = b; cb.z = 0;
                int cmp = SectorManager.HexDistance(fromCell, ca).CompareTo(SectorManager.HexDistance(fromCell, cb));
                if (cmp != 0) return cmp;
                return ca.GetHashCode().CompareTo(cb.GetHashCode());
            });
            foreach (Vector3Int rawStop in neighborBuf)
            {
                Vector3Int stopCell = rawStop;
                stopCell.z = 0;

                if (stopCell == fromCell)
                {
                    if (TryEmbarkFromHex(fromCell, null, unit.RemainingMovementPoints,
                            tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction action,
                            requireSectorMatch, allowOverflow, requireFormalPassenger, transporter))
                        return action;
                    continue;
                }

                if (!TryGetEmbarkStopPath(unit, fromCell, stopCell, movePaths, out List<Vector3Int> pathToStop))
                    continue;

                int remainingMPAtStop = CalculateRemainingMovementAfterPath(unit, pathToStop);
                if (remainingMPAtStop <= 0)
                    continue;

                if (TryEmbarkFromHex(stopCell, pathToStop, remainingMPAtStop,
                        tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction movedAction,
                        requireSectorMatch, allowOverflow, requireFormalPassenger, transporter))
                    return movedAction;
            }
        }

        return null;
    }

    private bool TryGetEmbarkStopPath(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int stopCell,
        Dictionary<Vector3Int, List<Vector3Int>> movePaths,
        out List<Vector3Int> path)
    {
        path = null;
        fromCell.z = 0;
        stopCell.z = 0;

        if (movePaths != null && movePaths.TryGetValue(stopCell, out path) && path != null && path.Count > 0)
        {
            Vector3Int last = path[path.Count - 1];
            last.z = 0;
            if (last == stopCell && CanUseCellForEmbarkApproach(unit, stopCell, isDestination: true))
                return true;

            Debug.Log($"{TL("Capturador")} {unit.InstanceId} descarta path de embarque stale para {stopCell}: last={last} usable={CanUseCellForEmbarkApproach(unit, stopCell, isDestination: true)}");
        }

        // Pathfinding can miss an otherwise valid embark stop when the target hex is
        // occupied by a non-blocking air layer unit or the occupancy grid is stale after
        // another AI action. Rebuild a small local path using current unit positions.
        return TryFindEmbarkStopPath(unit, fromCell, stopCell, Mathf.Max(0, unit.RemainingMovementPoints), out path);
    }

    private bool TryFindEmbarkStopPath(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int stopCell,
        int maxMovement,
        out List<Vector3Int> path)
    {
        path = null;
        if (unit == null || maxMovement <= 0)
            return false;

        fromCell.z = 0;
        stopCell.z = 0;

        var frontier = new Queue<Vector3Int>();
        var costByCell = new Dictionary<Vector3Int, int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var neighbors = new List<Vector3Int>(6);

        frontier.Enqueue(fromCell);
        costByCell[fromCell] = 0;
        cameFrom[fromCell] = fromCell;

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            if (current == stopCell)
                break;

            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, current, neighbors);
            foreach (Vector3Int rawNext in neighbors)
            {
                Vector3Int next = rawNext;
                next.z = 0;

                if (!UnitMovementPathRules.TryGetEnterCellCost(
                        boardTilemap, unit, next, terrainDatabase, false, out int enterCost))
                    continue;

                enterCost = Mathf.Max(1, enterCost);
                int nextCost = costByCell[current] + enterCost;
                if (nextCost > maxMovement)
                    continue;

                if (!CanUseCellForEmbarkApproach(unit, next, isDestination: next == stopCell))
                    continue;

                if (costByCell.TryGetValue(next, out int knownCost) && knownCost <= nextCost)
                    continue;

                costByCell[next] = nextCost;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(stopCell))
            return false;

        path = new List<Vector3Int>();
        Vector3Int cursor = stopCell;
        while (true)
        {
            path.Add(cursor);
            if (cursor == fromCell)
                break;
            cursor = cameFrom[cursor];
        }
        path.Reverse();
        return path.Count >= 2;
    }

    private bool CanUseCellForEmbarkApproach(UnitManager unit, Vector3Int cell, bool isDestination)
    {
        foreach (UnitManager occupant in UnitManager.AllActive)
        {
            if (occupant == null || occupant == unit || occupant.IsDead || occupant.IsEmbarked)
                continue;

            Vector3Int occCell = occupant.CurrentCellPosition;
            occCell.z = 0;
            if (occCell != cell)
                continue;

            if (!OccupancyResolver.CanPassThrough(unit, occupant, cell))
                return false;

            if (isDestination
                && occupant.TeamId == unit.TeamId
                && OccupancyResolver.GetHeightBand(occupant) == OccupancyResolver.GetHeightBand(unit))
                return false;
        }

        return true;
    }

    private int CalculateRemainingMovementAfterPath(UnitManager unit, IReadOnlyList<Vector3Int> path)
    {
        if (unit == null)
            return 0;
        int used = UnitMovementPathRules.CalculateAutonomyCostForPath(
            boardTilemap,
            unit,
            path,
            terrainDatabase,
            applyOperationalAutonomyModifier: false);
        return Mathf.Max(0, unit.RemainingMovementPoints - used);
    }

    private void CollectEmbarkTargetCells(
        Vector3Int passengerCell,
        UnitManager passenger,
        List<Vector3Int> neighborBuf,
        List<Vector3Int> output,
        HashSet<Vector3Int> seenCells)
    {
        output.Clear();
        seenCells.Clear();
        neighborBuf.Clear();

        passengerCell.z = 0;
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, passengerCell, neighborBuf);
        foreach (Vector3Int n in neighborBuf)
        {
            Vector3Int cell = n;
            cell.z = 0;
            if (seenCells.Add(cell))
                output.Add(cell);
        }
    }

    // Retorna true se outro capturer do mesmo setor está mais longe do objetivo e
    // ainda dentro do pickup range do APC — este capturer deve ceder a vaga.
    private bool ShouldYieldEmbarkToNeedierCapturer(
        UnitManager unit, UnitManager transporter, SectorObjective assigned, TeamObjectivePlan plan)
    {
        if (assigned == null || plan == null) return false;

        ConstructionManager objBuilding = FindCapturableInSector(assigned.Sector, unit.TeamId);
        if (objBuilding == null) return false;
        Vector3Int objCell = objBuilding.CurrentCellPosition; objCell.z = 0;

        Vector3Int myCell = unit.CurrentCellPosition; myCell.z = 0;
        float myDist = SectorManager.HexDistance(myCell, objCell);

        Vector3Int apcCell = transporter.CurrentCellPosition; apcCell.z = 0;

        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
            if (slot.AssignedUnitId == unit.InstanceId) continue;

            UnitManager other = FindActiveUnit(slot.AssignedUnitId, unit.TeamId);
            if (other == null || other.HasActed || other.IsEmbarked || other.IsDead) continue;

            Vector3Int otherCell = other.CurrentCellPosition; otherCell.z = 0;
            float otherDist = SectorManager.HexDistance(otherCell, objCell);
            if (otherDist <= myDist) continue; // não está mais longe

            float otherDistToAPC = SectorManager.HexDistance(otherCell, apcCell);
            if (otherDistToAPC > ShuttlePickupRange + 1 + 0.5f) continue; // fora do alcance do APC

            int openSeats = CountAvailableSeatsForPassenger(transporter, unit);
            if (openSeats > 1)
            {
                Debug.Log($"{TL("Capturador")} {unit.InstanceId} mantem embarque com {other.InstanceId}: transporter={transporter.InstanceId}@{apcCell} openSeats={openSeats} ({otherDist:F0}h > {myDist:F0}h ao objetivo)");
                return false;
            }

            Debug.Log($"{TL("Capturador")} {unit.InstanceId} cede embarque para {other.InstanceId}: transporter={transporter.InstanceId}@{apcCell} openSeats={openSeats} myCell={myCell} otherCell={otherCell} ({otherDist:F0}h > {myDist:F0}h ao objetivo)");
            return true;
        }

        return false;
    }

    private int CountAvailableSeatsForPassenger(UnitManager transporter, UnitManager passenger)
    {
        if (transporter == null || passenger == null)
            return 0;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null
            || transporterData.transportSlots == null)
            return 0;
        if (!passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null)
            return 0;

        int total = 0;
        for (int i = 0; i < transporterData.transportSlots.Count; i++)
        {
            UnitTransportSlotRule slot = transporterData.transportSlots[i];
            if (slot == null) continue;
            if (!PodeEmbarcarSensor.CanUseSlot(passenger, passengerData, slot, out _)) continue;

            int capacity = Mathf.Max(1, slot.capacity);
            int occupied = transporter.GetOccupiedTransportSeatCountForSlot(i);
            total += Mathf.Max(0, capacity - occupied);
        }

        return total;
    }

    // Verifica se há um transporter válido em tCell acessível a partir de fromHex,
    // com MP restante suficiente para embarcar. Retorna true e preenche action se válido.
    private bool TryEmbarkFromHex(
        Vector3Int fromHex, List<Vector3Int> pathToHex, int remainingMPAtHex,
        Vector3Int tCell, UnitManager unit, UnitData unitData,
        TeamObjectivePlan plan, SectorObjective assigned,
        AIWorldSnapshot snapshot, out PlayerAction action,
        bool requireSectorMatch = false, bool allowOverflow = false, bool requireFormalPassenger = false,
        UnitManager expectedTransporter = null)
    {
        action = null;

        tCell.z = 0;
        UnitManager transporter = ResolveEmbarkTransporterAtCell(unit, tCell, expectedTransporter);
        if (transporter == null || transporter.TeamId != unit.TeamId) return false;
        if (transporter.IsDead || transporter.IsEmbarked || transporter.IsUnderRepair) return false;
        if (!transporter.TryGetUnitData(out UnitData tData) || !tData.isTransporter) return false;
        if (!PodeEmbarcarSensor.CanEmbarkAtTransporterContext(
                boardTilemap, terrainDatabase, transporter, tData, out string contextReason))
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO contexto: transporter={transporter.InstanceId}@{tCell} {contextReason}");
            return false;
        }
        // Não embarcar em transporter ainda no aeroporto/fábrica — espera ele sair primeiro.
        Vector3Int transporterCell = transporter.CurrentCellPosition; transporterCell.z = 0;
        if (IsTeamProductionBuilding(transporterCell, unit.TeamId)
            && !IsAirTransporter(transporter)) return false;

        // Primary capturer: APC must be assigned to the same sector.
        // Secondary capturer: also accepts an APC with no formal passenger (shuttle mode).
        // Rogue capturer (no assigned sector): accepts any APC with no formal passenger.
        SectorObjective tObj = ResolveAssignedTransportObjective(transporter, plan);
        bool isPrimary = unitData.roles != null && unitData.roles.Count > 0
            && unitData.roles[0] == UnitRole.Capturador;
        bool sameSector = assigned != null && tObj != null && tObj.Sector == assigned.Sector;
        bool compatibleSector = assigned != null && tObj != null
            && AreEmbarkSectorsCompatible(assigned.Sector, tObj.Sector);
        UnitManager formalPassenger = tObj != null ? ResolveAssignedPassengerUnit(tObj, unit.TeamId) : null;
        bool formalMatch = sameSector && formalPassenger == unit;
        // Any capturer may board an APC with no formal passenger — free shuttle reorients to this objective.
        bool assignedRogueExtra = assigned == null
            && tObj != null
            && CanRogueUseAssignedTransporter(unit, transporter, tObj, unit.TeamId);
        bool shuttleFree = assigned == null
            ? (tObj == null || assignedRogueExtra)
            : (tObj == null || (compatibleSector && formalPassenger == null));
        bool rogueEmbark = assigned == null && shuttleFree;
        if (requireFormalPassenger && !formalMatch) return false;
        // requireSectorMatch: called from the first preference pass — only accept the plan-assigned transporter.
        if (requireSectorMatch && !sameSector) return false;
        if (assigned != null && tObj != null && !compatibleSector)
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO setor distante: assigned={assigned.Sector} tObj={tObj.Sector} transporter={transporter.InstanceId}");
            return false;
        }
        if (assigned == null && tObj != null && !shuttleFree)
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO reserva: rogue nao usa transporter={transporter.InstanceId} reservado para {tObj.Sector}");
            return false;
        }
        if (!sameSector && !shuttleFree)
        {
            if (!allowOverflow)
            {
                Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO setor: assigned={assigned?.Sector} tObj={tObj?.Sector} sameSector={sameSector} compatible={compatibleSector} shuttleFree={shuttleFree} isPrimary={isPrimary} transporter={transporter.InstanceId}");
                return false;
            }
            // overflow: slot físico check abaixo confirma capacidade disponível
        }
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        // Short-walk guard uses the unit's actual position, not the intermediate stop cell —
        // moving 1-2h toward the transporter should not make the destination appear "walkable."
        if (ShouldSkipCapturerEmbarkForShortWalk(unit, assigned, fromCell, "hex embarque"))
            return false;

        // Pickup range: usa fromHex (posição após movimento) para não bloquear embarque estendido.
        Vector3Int pickupRef = fromHex; pickupRef.z = 0;
        float pickupDist = SectorManager.HexDistance(pickupRef, tCell);
        if (pickupDist > ShuttlePickupRange + 1 + 0.5f)
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO pickup: pickupDist={pickupDist:F0}h > {ShuttlePickupRange + 1 + 0.5f} fromHex={fromHex} tCell={tCell}");
            return false;
        }

        // Verifica custo de embarque vs MP restante no hex intermediário
        if (!UnitMovementPathRules.TryGetEnterCellCost(
                boardTilemap, unit, tCell, terrainDatabase, false, out int embarkCost))
            embarkCost = 1;
        embarkCost = Mathf.Max(1, embarkCost);
        if (remainingMPAtHex < embarkCost)
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO MP: remainingMPAtHex={remainingMPAtHex} < embarkCost={embarkCost} tCell={tCell}");
            return false;
        }

        int slotIdx = assignedRogueExtra
            ? FindFittingSecondarySlotIndex(transporter, tData, unit, unitData)
            : FindFittingSlotIndex(transporter, tData, unit, unitData);
        if (slotIdx < 0 && assignedRogueExtra)
            slotIdx = FindFittingSlotIndexRespectingFormalReservation(transporter, tData, unit, unitData, formalPassenger);
        if (slotIdx < 0)
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO slot: sem slot disponível em transporter={transporter.InstanceId}");
            return false;
        }

        if (ShouldYieldEmbarkToNeedierCapturer(unit, transporter, assigned, plan))
            return false;

        tCell.z = 0;
        var pathsForBatch = pathToHex != null
            ? new Dictionary<Vector3Int, List<Vector3Int>> { [tCell] = pathToHex }
            : null;

        string overflowTag = allowOverflow && !sameSector && !shuttleFree ? " [overflow→" + tObj?.Sector + "]" : "";
        Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca{overflowTag} (ext {(int)SectorManager.HexDistance(fromCell, tCell)}h) → {transporter.InstanceId} slot {slotIdx} via {fromHex}");
        action = BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, transporter, slotIdx, pathsForBatch);
        return true;
    }

    private UnitManager ResolveEmbarkTransporterAtCell(
        UnitManager passenger,
        Vector3Int transporterCell,
        UnitManager expectedTransporter = null)
    {
        transporterCell.z = 0;

        if (IsUsableTransporterAtCell(expectedTransporter, passenger, transporterCell))
            return expectedTransporter;

        // CurrentCellPosition is the source of truth during AI Phase 2. Tile occupancy can
        // temporarily lag after earlier batches, so prefer an explicit live scan for
        // transporters over the generic "first unit at cell" lookup.
        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (IsUsableTransporterAtCell(candidate, passenger, transporterCell))
                return candidate;
        }

        UnitManager occupied = UnitOccupancyRules.GetUnitAtCell(boardTilemap, transporterCell, passenger);
        return IsUsableTransporterAtCell(occupied, passenger, transporterCell) ? occupied : null;
    }

    private static bool IsUsableTransporterAtCell(
        UnitManager candidate,
        UnitManager passenger,
        Vector3Int transporterCell)
    {
        if (candidate == null || candidate == passenger || candidate.IsDead || candidate.IsEmbarked)
            return false;

        Vector3Int candidateCell = candidate.CurrentCellPosition;
        candidateCell.z = 0;
        if (candidateCell != transporterCell)
            return false;

        return candidate.TryGetUnitData(out UnitData data) && data != null && data.isTransporter;
    }

    private bool CanRogueUseAssignedTransporter(
        UnitManager passenger,
        UnitManager transporter,
        SectorObjective transportObjective,
        TeamId aiTeam)
    {
        if (passenger == null || transporter == null || transportObjective == null)
            return false;

        UnitManager formalPassenger = ResolveAssignedPassengerSlotUnit(transportObjective, aiTeam);
        if (formalPassenger == null)
            return CountAvailableSeatsForPassenger(transporter, passenger) > 0;
        if (formalPassenger == passenger)
            return true;

        if (IsPassengerAlreadyOnboard(transporter, formalPassenger))
            return CountAvailableSeatsForPassenger(transporter, passenger) > 0;

        // Formal passenger already acted this turn without embarking — reservation is void.
        if (formalPassenger.HasActed)
            return CountAvailableSeatsForPassenger(transporter, passenger) > 0;

        if (!passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null)
            return false;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
            return false;

        // A reserva do plano cobre o slot primario. Slot secundario fisicamente
        // livre continua disponivel para rogue/oportunista.
        if (FindFittingSecondarySlotIndex(transporter, transporterData, passenger, passengerData) >= 0)
            return true;

        return FindFittingSlotIndexRespectingFormalReservation(
            transporter, transporterData, passenger, passengerData, formalPassenger) >= 0;
    }

    private static int FindFittingSecondarySlotIndex(
        UnitManager transporter,
        UnitData transporterData,
        UnitManager passenger,
        UnitData passengerData)
    {
        if (transporter == null || transporterData == null || transporterData.transportSlots == null)
            return -1;

        for (int i = 1; i < transporterData.transportSlots.Count; i++)
        {
            UnitTransportSlotRule slot = transporterData.transportSlots[i];
            if (slot == null) continue;
            if (!PodeEmbarcarSensor.CanUseSlot(passenger, passengerData, slot, out _)) continue;

            int occupancy = transporter.GetOccupiedTransportSeatCountForSlot(i);
            if (occupancy >= Mathf.Max(1, slot.capacity)) continue;
            return i;
        }

        return -1;
    }

    private static int FindFittingSlotIndexRespectingFormalReservation(
        UnitManager transporter,
        UnitData transporterData,
        UnitManager passenger,
        UnitData passengerData,
        UnitManager formalPassenger)
    {
        if (formalPassenger == null || formalPassenger == passenger || IsPassengerAlreadyOnboard(transporter, formalPassenger))
            return FindFittingSlotIndex(transporter, transporterData, passenger, passengerData);
        if (transporter == null || transporterData == null || transporterData.transportSlots == null)
            return -1;
        if (!formalPassenger.TryGetUnitData(out UnitData formalData) || formalData == null)
            return FindFittingSlotIndex(transporter, transporterData, passenger, passengerData);

        int bestSlot = -1;
        int bestReservedSlot = -1;
        int bestScore = int.MinValue;

        for (int reserveIdx = 0; reserveIdx < transporterData.transportSlots.Count; reserveIdx++)
        {
            UnitTransportSlotRule reserveSlot = transporterData.transportSlots[reserveIdx];
            if (reserveSlot == null) continue;
            if (!PodeEmbarcarSensor.CanUseSlot(formalPassenger, formalData, reserveSlot, out _)) continue;

            int reserveCapacity = Mathf.Max(1, reserveSlot.capacity);
            int reserveOccupied = transporter.GetOccupiedTransportSeatCountForSlot(reserveIdx);
            if (reserveOccupied >= reserveCapacity) continue;

            for (int passengerIdx = 0; passengerIdx < transporterData.transportSlots.Count; passengerIdx++)
            {
                UnitTransportSlotRule passengerSlot = transporterData.transportSlots[passengerIdx];
                if (passengerSlot == null) continue;
                if (!PodeEmbarcarSensor.CanUseSlot(passenger, passengerData, passengerSlot, out _)) continue;

                int passengerCapacity = Mathf.Max(1, passengerSlot.capacity);
                int passengerOccupied = transporter.GetOccupiedTransportSeatCountForSlot(passengerIdx);
                if (passengerIdx == reserveIdx)
                    passengerOccupied++;
                if (passengerOccupied >= passengerCapacity) continue;

                // Prefer preserving the formal slot untouched; if both must share,
                // prefer the arrangement with more remaining slack.
                int score = passengerIdx == reserveIdx ? 0 : 1000;
                score += passengerCapacity - passengerOccupied;
                score += reserveCapacity - reserveOccupied;
                if (score <= bestScore) continue;

                bestScore = score;
                bestSlot = passengerIdx;
                bestReservedSlot = reserveIdx;
            }
        }

        if (bestSlot >= 0)
            return bestSlot;

        // If no formal-compatible physical seat is currently open, do not consume
        // a reserved transporter opportunistically; that would hide the real issue.
        return bestReservedSlot >= 0 ? bestSlot : -1;
    }

    private static bool IsPassengerAlreadyOnboard(UnitManager transporter, UnitManager passenger)
    {
        if (transporter == null || passenger == null || transporter.TransportedUnitSlots == null)
            return false;

        foreach (UnitTransportSeatRuntime seat in transporter.TransportedUnitSlots)
            if (seat.embarkedUnit == passenger && passenger.IsEmbarked)
                return true;

        return false;
    }

    private bool ShouldSkipCapturerEmbarkForShortWalk(
        UnitManager unit,
        SectorObjective assigned,
        Vector3Int candidateCell,
        string context)
    {
        if (unit == null)
            return false;

        // Rogues marcham ao HQ inimigo, não ao capturável mais próximo.
        // A referência de distância seria inválida — sempre permite embarque.
        if (assigned == null)
            return false;

        candidateCell.z = 0;

        ConstructionManager objBuilding = FindCapturableInSector(assigned.Sector, unit.TeamId);

        if (objBuilding == null)
            return false;

        Vector3Int objCell = objBuilding.CurrentCellPosition; objCell.z = 0;
        int effectiveThreshold = GetEffectiveTransportThreshold(unit.TeamId);
        // Slow units benefit from transport from farther away (+2h per MP below 3).
        int baseMP = unit.MaxMovementPoints;
        if (baseMP < 3) effectiveThreshold += (3 - baseMP) * 2;
        int terrainCost = TerrainCostToCell(unit, candidateCell, objCell, effectiveThreshold);
        if (terrainCost >= effectiveThreshold)
            return false;

        string sectorLabel = assigned != null ? assigned.Sector.ToString() : objBuilding.Sector.ToString();
        Debug.Log($"{TL("Capturador")} {unit.InstanceId} ignora embarque ({context} terreno={terrainCost}<{effectiveThreshold}h de {sectorLabel})");
        return true;
    }

    private ConstructionManager FindNearestCapturableForUnit(UnitManager unit, Vector3Int fromCell)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (!c.IsCapturable || c.CapturePointsMax <= 0) continue;
            if (c.TeamId == unit.TeamId && c.CurrentCapturePoints >= c.CapturePointsMax) continue;
            Vector3Int tc = c.CurrentCellPosition; tc.z = 0;
            float dist = SectorManager.HexDistance(fromCell, tc);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }
        return best;
    }

    // Finds the nearest rogue transporter (no formal plan assignment) that has a fitting
    // slot for this capturer. Used as a fallback move target when extended embark fails.
    private UnitManager FindNearestRogueTransporter(UnitManager capturer, UnitData capturerData,
        TeamObjectivePlan plan, AIWorldSnapshot snapshot)
    {
        UnitManager best = null;
        float bestDist = float.MaxValue;
        Vector3Int fromCell = capturer.CurrentCellPosition; fromCell.z = 0;

        foreach (UnitManager t in UnitManager.AllActive)
        {
            if (t == capturer) continue;
            if (t.TeamId != capturer.TeamId || t.IsDead || t.IsEmbarked) continue;
            if (!t.TryGetUnitData(out UnitData tData) || !tData.isTransporter) continue;
            SectorObjective tObj = plan != null ? ResolveAssignedTransportObjective(t, plan) : null;
            if (tObj != null) continue;
            if (FindFittingSlotIndex(t, tData, capturer, capturerData) < 0) continue;
            Vector3Int tCell = t.CurrentCellPosition; tCell.z = 0;
            float dist = SectorManager.HexDistance(fromCell, tCell);
            if (dist < bestDist) { bestDist = dist; best = t; }
        }

        return best;
    }
}
