using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
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

    private bool TryCapturerEmbarkFromHex(
        Vector3Int fromHex, List<Vector3Int> pathToHex, int remainingMPAtHex,
        Vector3Int tCell, UnitManager unit, UnitData unitData,
        TeamObjectivePlan plan, SectorObjective assigned,
        QueroCaronaResult rideNeed,
        AIWorldSnapshot snapshot, out PlayerAction action,
        bool requireSectorMatch = false, bool allowOverflow = false, bool requireFormalPassenger = false,
        UnitManager expectedTransporter = null)
    {
        action = null;
        if (rideNeed == null || !rideNeed.wantsRide)
            return false;

        return TryEmbarkFromHex(
            fromHex, pathToHex, remainingMPAtHex,
            tCell, unit, unitData, plan, assigned,
            snapshot, out action,
            requireSectorMatch, allowOverflow,
            requireFormalPassenger, expectedTransporter);
    }

    // Executor físico compartilhado. O Capturer chega aqui pelo wrapper acima,
    // já autorizado pelo Quero Carona; EVAC reutiliza a legalidade de embarque
    // sem precisar fabricar uma decisão de agenda do Capturer.
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
        if (transporter == null || transporter.SlotIndex != unit.SlotIndex) return false;
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
        if (IsTeamProductionBuilding(transporterCell, unit.TeamId)) return false;

        // Primary capturer: APC must be assigned to the same sector.
        // Secondary capturer: also accepts an APC with no formal passenger (shuttle mode).
        // Rogue capturer (no assigned sector): accepts any APC with no formal passenger.
        SectorObjective tObj = ResolveAssignedTransportObjective(transporter, plan);
        bool isPrimary = UnitRoleCompatibility.ResolveCompositionRole(unitData) == UnitRole.Capturador;
        bool sameSector = assigned != null && tObj != null && tObj.Sector == assigned.Sector;
        bool compatibleSector = assigned != null && tObj != null
            && AreEmbarkSectorsCompatible(assigned.Sector, tObj.Sector);
        bool compatibleNavalRoute = assigned != null && tObj != null
            && AreNavalEmbarkRoutesCompatible(
                transporter, assigned, tObj, snapshot);
        bool compatibleTransportObjective = compatibleSector || compatibleNavalRoute;
        UnitManager formalPassenger = tObj != null ? ResolveAssignedPassengerUnit(tObj, unit.TeamId) : null;
        bool formalMatch = sameSector && formalPassenger == unit;
        // Chinook e navio operam como courier com fila. O objetivo do passageiro
        // extra nao precisa coincidir com a primeira entrega: ele ocupa o banco #2,
        // aguarda o passageiro formal descer e entao vira a nova entrega principal.
        bool supportsQueuedPassenger =
            tData.domain == Domain.Air || tData.domain == Domain.Naval;
        bool queuedPassengerSeat =
            supportsQueuedPassenger &&
            assigned != null &&
            tObj != null &&
            formalPassenger != unit &&
            CountAvailableSeatsForPassenger(transporter, unit) > 0;
        // Any capturer may board an APC with no formal passenger — free shuttle reorients to this objective.
        bool assignedRogueExtra = assigned == null
            && tObj != null
            && CanRogueUseAssignedTransporter(unit, transporter, tObj, unit.TeamId);
        bool shuttleFree = assigned == null
            ? (tObj == null || assignedRogueExtra)
            : (tObj == null || (compatibleTransportObjective && formalPassenger == null));
        bool rogueEmbark = assigned == null && shuttleFree;
        if (requireFormalPassenger && !formalMatch) return false;
        // requireSectorMatch: called from the first preference pass — only accept the plan-assigned transporter.
        if (requireSectorMatch && !sameSector) return false;
        if (assigned != null && tObj != null
            && !compatibleTransportObjective
            && !queuedPassengerSeat)
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO setor distante: assigned={assigned.Sector} tObj={tObj.Sector} transporter={transporter.InstanceId}");
            return false;
        }
        if (assigned == null && tObj != null && !shuttleFree)
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO reserva: rogue nao usa transporter={transporter.InstanceId} reservado para {tObj.Sector}");
            return false;
        }
        if (!sameSector && !shuttleFree && !queuedPassengerSeat)
        {
            if (!allowOverflow)
            {
                Debug.Log($"{TL("Capturador")} {unit.InstanceId} TryEmbarkFromHex BLOQUEADO setor: assigned={assigned?.Sector} tObj={tObj?.Sector} sameSector={sameSector} compatible={compatibleSector} navalRoute={compatibleNavalRoute} shuttleFree={shuttleFree} isPrimary={isPrimary} transporter={transporter.InstanceId}");
                return false;
            }
            // overflow: slot físico check abaixo confirma capacidade disponível
        }
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;

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

        if (ShouldYieldEmbarkToNeedierCapturer(
                unit, transporter, assigned, plan))
            return false;

        if (queuedPassengerSeat && !compatibleTransportObjective)
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} usa banco courier #2 de " +
                      $"{transporter.InstanceId}: entrega atual={tObj.Sector}, fila seguinte={assigned.Sector}.");
        }

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
        if (candidate == null || candidate == passenger
            || candidate.IsDead || candidate.IsEmbarked || candidate.IsUnderRepair)
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


}
