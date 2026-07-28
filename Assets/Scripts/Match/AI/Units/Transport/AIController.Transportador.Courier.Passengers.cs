using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private static List<UnitManager> CollectPassengers(UnitManager transporter)
    {
        var list = new List<UnitManager>();
        if (transporter.TransportedUnitSlots == null) return list;
        foreach (UnitTransportSeatRuntime seat in transporter.TransportedUnitSlots)
            if (seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked)
                list.Add(seat.embarkedUnit);
        return list;
    }


    private UnitManager ResolvePrimaryPassenger(
        UnitManager transporter,
        List<UnitManager> passengers,
        TeamObjectivePlan plan = null)
    {
        // Fila FIFO por turno confirmado. Em empate, a ordem fisica das vagas
        // (slotIndex/seatIndex, ja materializada na lista runtime) decide.
        UnitManager oldest = null;
        int oldestTurn = int.MaxValue;
        if (transporter?.TransportedUnitSlots != null)
        {
            foreach (UnitTransportSeatRuntime seat in transporter.TransportedUnitSlots)
            {
                UnitManager passenger = seat?.embarkedUnit;
                if (passenger == null || !passenger.IsEmbarked || !passengers.Contains(passenger))
                    continue;

                int turn = seat.embarkedOnTurn >= 0 ? seat.embarkedOnTurn : int.MaxValue - 1;
                if (oldest == null || turn < oldestTurn)
                {
                    oldest = passenger;
                    oldestTurn = turn;
                }
            }
        }

        return oldest != null ? oldest : passengers[0];
    }


    private static bool IsPassengerInPlanSlot(UnitManager passenger, TeamObjectivePlan plan)
    {
        if (plan == null || passenger == null || plan.Objectives == null) return false;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Slots == null) continue;
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && slot.AssignedUnitId == passenger.InstanceId) return true;
        }
        return false;
    }

    // For each passenger, picks the best delivery cell, preferring immediate capture chances
    // that do not pull the courier away from its current delivery route.

    private bool TryResolveCourierPassengerTarget(
        UnitManager passenger,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int assignedSectorTarget,
        Vector3Int fallbackCell,
        out Vector3Int resolvedTarget)
    {
        resolvedTarget = Vector3Int.zero;
        if (passenger == null) return false;
        if (assignedSectorTarget != Vector3Int.zero) assignedSectorTarget.z = 0;
        fallbackCell.z = 0;

        if (IsStationaryMobileAirSurveillanceRadar(passenger))
        {
            if (TryResolveMobileRadarTransportTarget(
                    passenger,
                    snapshot,
                    plan,
                    fallbackCell,
                    requireCoverageGain: false,
                    out Vector3Int surveillanceTarget,
                    out float surveillanceGain,
                    out string surveillanceReason))
            {
                Debug.Log(
                    $"{TL("Transporte")} PassengerTarget " +
                    $"Radar#{passenger.InstanceId} -> " +
                    $"{surveillanceTarget} gain={surveillanceGain:F0} " +
                    $"({surveillanceReason})");
                resolvedTarget = surveillanceTarget;
                return true;
            }

            resolvedTarget = fallbackCell;
            Debug.Log(
                $"{TL("Transporte")} PassengerTarget " +
                $"Radar#{passenger.InstanceId} sem zona segura; " +
                $"aguarda em {fallbackCell}");
            return true;
        }

        // Facção sem QG: o passageiro nao tem slot de plano, entao o fluxo normal cairia no
        // funil rogue-para-o-QG-inimigo. O rebelde a pe ja captura por PROXIMIDADE (ver
        // AIController.Rebel); o mesmo criterio vale quando ele e carga, senao APC/Chinook/
        // navio entregariam a tropa no lugar errado. Reusa o mesmo buscador — o transporte
        // so precisa saber PARA ONDE levar.
        if (ConstructionManager.IsHeadQuarterlessTeam(snapshot.AITeam))
        {
            ConstructionManager rebelTarget = FindNearestRebelCaptureTarget(passenger, snapshot, fallbackCell);
            if (rebelTarget != null)
            {
                Vector3Int rc = rebelTarget.CurrentCellPosition; rc.z = 0;
                Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} rebelde → capturavel proximo {rc}");
                resolvedTarget = rc;
                return true;
            }
            // Sem capturavel a vista: nao inventa alvo. Deixa o transporte decidir esperar
            // em vez de marchar para o QG inimigo por falta de opcao.
            return false;
        }

        if (IsFireSupportUnit(passenger))
        {
            SectorObjective assignedFireSupport = ResolveAssignedFireSupportObjective(passenger, plan);
            if (assignedFireSupport != null)
            {
                // Do NOT use fallbackCell (truck position) as last resort — when the assigned
                // sector has no capturable building (already AI-controlled), passing the truck's
                // cell as fallback makes the truck believe it has already arrived, so it never drives.
                ConstructionManager tgt = FindCapturableInSector(assignedFireSupport.Sector, snapshot.AITeam);
                if (tgt != null)
                {
                    Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
                    Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} setor={assignedFireSupport.Sector} capturable={tc}");
                    resolvedTarget = tc; return true;
                }
                if (TryGetAnySectorInfo(assignedFireSupport.Sector, out SectorManager.SectorInfo si))
                {
                    Vector3Int rc = si.RepresentativeCell; rc.z = 0;
                    Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} setor={assignedFireSupport.Sector} repCell={rc} (sem capturable)");
                    resolvedTarget = rc; return true;
                }
                Debug.LogWarning($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} setor={assignedFireSupport.Sector} sem sectorInfo");
            }

            if (assignedSectorTarget != Vector3Int.zero)
            { resolvedTarget = assignedSectorTarget; return true; }

            Vector3Int passengerCell = passenger.CurrentCellPosition;
            passengerCell.z = 0;
            if (TryFindTowDeliveryTarget(passenger, passengerCell, snapshot, plan, out Vector3Int towTarget))
            { resolvedTarget = towTarget; return true; }

            return false;
        }

        // Look up capturable in the passenger's assigned sector; do NOT fall back to
        // the sector RepresentativeCell — for already-captured sectors it equals the
        // truck's own starting position, causing a distance-0 disembark without moving.
        Vector3Int target = Vector3Int.zero;
        bool passengerHasPlanSlot = false;
        if (plan != null)
        {
            bool slotFound = false;
            foreach (SectorObjective obj in plan.Objectives)
            {
                if (slotFound) break;
                foreach (SlotNeed slot in obj.Slots)
                {
                    if (!slot.Filled || slot.AssignedUnitId != passenger.InstanceId) continue;
                    passengerHasPlanSlot = true;
                    ConstructionManager tgt = FindCapturableInSector(obj.Sector, snapshot.AITeam, fallbackCell);
                    if (tgt != null) { target = tgt.CurrentCellPosition; target.z = 0; }
                    else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo si))
                    { target = si.RepresentativeCell; target.z = 0; }
                    Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} setor={obj.Sector} capturable={target} (fallback={fallbackCell})");
                    slotFound = true; break;
                }
            }
        }

        // Rogue de uma IA com HQ mantém o vetor macro rumo ao HQ e escolhe
        // apenas capturas próximas desse corredor. Não usa a expansão radial
        // do rebelde nem herda automaticamente o setor do transportador.
        if (!passengerHasPlanSlot
            && IsRogueCapturerPassenger(passenger, plan)
            && TryResolveRogueCorridorCaptureTarget(
                passenger,
                snapshot,
                fallbackCell,
                null,
                out Vector3Int corridorTarget))
        {
            Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} " +
                      $"rogue corredor HQ -> {corridorTarget}");
            resolvedTarget = corridorTarget;
            return true;
        }

        // Passenger without a plan slot is extra cargo. On an assigned transporter,
        // it follows the transporter's assigned sector instead of hijacking the route to HQ.
        if (!passengerHasPlanSlot && assignedSectorTarget != Vector3Int.zero)
        {
            resolvedTarget = assignedSectorTarget;
            return true;
        }

        // Rogue capturer — no plan slot. Head to the HQ sector and drop at the nearest
        // capturable building within it (could be a factory before the HQ itself).
        if (target == Vector3Int.zero && snapshot.EnemyHQ != null)
        {
            ConstructionManager tgt = FindCapturableInSector(snapshot.EnemyHQ.Sector, snapshot.AITeam, fallbackCell);
            if (tgt != null)
            {
                target = tgt.CurrentCellPosition; target.z = 0;
                Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} rogue → setor HQ capturable={target}");
            }
        }

        bool targetIsHQFallback = snapshot.EnemyHQ != null
            && target == snapshot.EnemyHQ.CurrentCellPosition;
        if ((target == Vector3Int.zero || targetIsHQFallback)
            && assignedSectorTarget != Vector3Int.zero)
            target = assignedSectorTarget;
        if (target == Vector3Int.zero && snapshot.EnemyHQ != null)
        { target = snapshot.EnemyHQ.CurrentCellPosition; target.z = 0; }
        if (target == Vector3Int.zero && snapshot.EnemyBuildings != null)
        {
            Vector3Int pCell = passenger.CurrentCellPosition; pCell.z = 0;
            float nearestDist = float.MaxValue;
            foreach (ConstructionManager eb in snapshot.EnemyBuildings)
            {
                if (eb == null) continue;
                Vector3Int ec = eb.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(pCell, ec);
                if (d < nearestDist) { nearestDist = d; target = ec; }
            }
        }
        if (target != Vector3Int.zero) { resolvedTarget = target; return true; }
        return false;
    }

    // -------------------------------------------------------------------------
    // Restricted combat (courier mode): HP <= 2, deviation <= 2h from route
    // -------------------------------------------------------------------------


}
