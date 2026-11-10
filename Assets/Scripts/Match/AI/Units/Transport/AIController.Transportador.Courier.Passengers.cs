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
        if (transporter != null && airDeliveryPrimary.TryGetValue(transporter, out var promoted))
        {
            if (promoted != null && promoted.IsEmbarked && passengers.Contains(promoted)) return promoted;
            airDeliveryPrimary.Remove(transporter);
        }
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


    private readonly Dictionary<UnitManager, UnitManager> airDeliveryPrimary = new Dictionary<UnitManager, UnitManager>();

    private void PromoteUnservedAirPassenger(UnitManager carrier, List<UnitManager> cargo,
        TeamObjectivePlan plan, AIWorldSnapshot snapshot, Vector3Int fallback)
    {
        // A delivery preference, not a board mutation. Keep it until the promoted passenger leaves.
        if (airDeliveryPrimary.TryGetValue(carrier, out var remembered)
            && remembered != null && remembered.IsEmbarked && cargo.Contains(remembered)) return;
        airDeliveryPrimary.Remove(carrier);
        var original = ResolvePrimaryPassenger(carrier, cargo, plan);
        if (!IsCapturerPassenger(original) || original.IsUnderRepair) return;
        if (!TryResolvePassengerDesignatedMissionTarget(original, out var originalTarget)
            && !TryResolveCourierPassengerTargetCore(original, plan, snapshot, fallback,
                carrier.CurrentCellPosition, out originalTarget)) return;
        if (TryResolveUnservedAirCapture(original, originalTarget, plan, snapshot, null, out _)) return;
        foreach (var candidate in OrderPassengersForDelivery(carrier, cargo))
        {
            if (candidate == original || candidate.IsUnderRepair
                || !TryResolveCourierPassengerTarget(candidate, plan, snapshot, fallback,
                    carrier.CurrentCellPosition, out var target)) continue;
            airDeliveryPrimary[carrier] = candidate;
            Debug.Log($"{TL("Transporte")} heli {carrier.InstanceId} promove passageiro #{candidate.InstanceId} para {target}; "
                + $"entrega de #{original.InstanceId} em {originalTarget} ja atendida; antigo principal vira oportunista.");
            return;
        }
    }

    private bool TryResolveCourierPassengerTarget(UnitManager passenger, TeamObjectivePlan plan,
        AIWorldSnapshot snapshot, Vector3Int assignedSectorTarget, Vector3Int fallbackCell, out Vector3Int target)
    {
        if (IsAirCapturePassenger(passenger) && TryResolvePassengerDesignatedMissionTarget(passenger, out target))
            return TryResolveUnservedAirCapture(passenger, target, plan, snapshot, null, out target);
        if (!TryResolveCourierPassengerTargetCore(passenger, plan, snapshot, assignedSectorTarget, fallbackCell, out target)) return false;
        return TryResolveUnservedAirCapture(passenger, target, plan, snapshot, null, out target);
    }

    private bool IsAirCapturePassenger(UnitManager passenger)
    {
        return IsCapturerPassenger(passenger)
            && (!passenger.AIHasDesignatedMission || passenger.AIDesignatedMissionIntent == AIPlanRuntimeIntent.Capture)
            && passenger.EmbarkedTransporter != null
            && passenger.EmbarkedTransporter.TryGetUnitData(out var carrierData) && carrierData.IsAircraft()
            && !passenger.IsUnderRepair;
    }

    // One concrete task per delivered capturer, unless confirmed pressure justifies reinforcement.
    private bool TryResolveUnservedAirCapture(UnitManager passenger, Vector3Int original,
        TeamObjectivePlan plan, AIWorldSnapshot snapshot, HashSet<Vector3Int> claimed, out Vector3Int target)
    {
        target = original; target.z = 0;
        if (!IsAirCapturePassenger(passenger) || snapshot == null) return true;
        var building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, target);
        if (building == null || !building.IsCapturable) return true; // Non-capture mission.
        bool Usable(ConstructionManager b)
        {
            var cell = b.CurrentCellPosition; cell.z = 0;
            bool pressure = IsCaptureTargetUnderConfirmedPressure(cell, snapshot);
            if (!pressure && ((claimed != null && claimed.Contains(cell))
                || IsAirDeliveryCaptureAlreadyServed(cell, plan, snapshot))) return false;
            return PodeCapturarSensor.TryGetCaptureTargetAtCell(passenger, boardTilemap, cell,
                SensorMovementMode.MoveuAndando, out _, out _, out _, matchController, applyEmbarkedGate: false);
        }
        if (Usable(building)) return true;
        ConstructionManager best = null; float distance = float.MaxValue;
        foreach (var alternative in ConstructionManager.AllActive)
        {
            if (alternative == null || alternative == building || alternative.gameObject.scene != passenger.gameObject.scene
                || alternative.Sector != building.Sector || alternative.Sector == ConstructionSector.None
                || !alternative.IsCapturable || !Usable(alternative)) continue;
            float d = SectorManager.HexDistance(original, alternative.CurrentCellPosition);
            if (d < distance) { distance = d; best = alternative; }
        }
        if (best == null) return false;
        target = best.CurrentCellPosition; target.z = 0;
        return true;
    }

    private bool IsAirDeliveryCaptureAlreadyServed(Vector3Int target, TeamObjectivePlan plan, AIWorldSnapshot snapshot)
    {
        var building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, target);
        if (building == null || snapshot?.MyUnits == null) return false;
        foreach (var ally in snapshot.MyUnits)
        {
            if (ally == null || ally.IsDead || ally.IsEmbarked || ally.HasActed || ally.IsUnderRepair
                || !PodeCapturarSensor.HasAnyCaptureKey(ally)) continue;
            bool committed = ally.CurrentCellPosition == target;
            if (ally.AIDesignatedMissionIntent == AIPlanRuntimeIntent.Capture
                && TryResolvePassengerDesignatedMissionTarget(ally, out var missionCell) && missionCell == target) committed = true;
            // Sector assignment is sufficient only when it identifies a unique capturable building.
            if (!committed && plan?.Objectives != null)
            {
                var objective = plan.Objectives.Find(x => x.Sector == building.Sector && x.Slots != null
                    && x.Slots.Exists(n => n.Filled && n.AssignedUnitId == ally.InstanceId));
                if (objective != null)
                {
                    int eligible = 0;
                    foreach (var b in ConstructionManager.AllActive)
                        if (b != null && b.gameObject.scene == ally.gameObject.scene && b.Sector == building.Sector
                            && b.IsCapturable && PodeCapturarSensor.TryGetCaptureTargetAtCell(ally, boardTilemap,
                                b.CurrentCellPosition, SensorMovementMode.MoveuAndando, out _, out _, out _, matchController)) eligible++;
                    committed = eligible == 1;
                }
            }
            if (!committed || !PodeCapturarSensor.TryGetCaptureTargetAtCell(ally, boardTilemap, target,
                    SensorMovementMode.MoveuAndando, out _, out _, out _, matchController)) continue;
            var paths = UnitMovementPathRules.CalcularCaminhosValidos(boardTilemap, ally,
                Mathf.Max(0, ally.RemainingMovementPoints), terrainDatabase);
            if (paths != null && paths.ContainsKey(target)) return true;
        }
        return false;
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

    private static bool AreAllPassengersPlanless(List<UnitManager> passengers, TeamObjectivePlan plan)
    {
        if (passengers == null || passengers.Count == 0) return false;
        foreach (UnitManager passenger in passengers)
            if (passenger == null || IsPassengerInPlanSlot(passenger, plan)) return false;
        return true;
    }

    // For each passenger, picks the best delivery cell, preferring immediate capture chances
    // that do not pull the courier away from its current delivery route.

    private bool TryResolveCourierPassengerTargetCore(
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

        // Plataforma aerea le a intencao do passageiro pelo papel. Sem este
        // ramo, EWACS/caca embarcado cairia no fallback de capturador e faria
        // porta-avioes/fragata marchar para uma construcao inimiga.
        if (IsAirborneAirSurveillanceUnit(passenger))
        {
            if (TryResolveAirSurveillanceAnchor(
                    passenger,
                    snapshot,
                    plan,
                    fallbackCell,
                    out Vector3Int surveillanceAnchor,
                    out _,
                    out string surveillanceReason))
            {
                resolvedTarget = surveillanceAnchor;
                Debug.Log(
                    $"{TL("Transporte")} PassengerTarget " +
                    $"EWACS#{passenger.InstanceId} -> " +
                    $"{surveillanceAnchor} ({surveillanceReason})");
                return true;
            }

            resolvedTarget = fallbackCell;
            Debug.Log(
                $"{TL("Transporte")} PassengerTarget " +
                $"EWACS#{passenger.InstanceId} sem ancora; " +
                $"mantem plataforma em {fallbackCell}");
            return true;
        }

        if (IsAirCombatUnit(passenger))
        {
            List<UnitManager> visibleEnemies =
                CollectVisibleAirCombatEnemies(
                    passenger,
                    snapshot);
            Vector3Int airCombatAnchor =
                ResolveAirCombatFallbackAnchor(
                    passenger,
                    snapshot,
                    fallbackCell,
                    visibleEnemies,
                    out string airCombatTier, out _, registerEscortClaim: false);
            airCombatAnchor.z = 0;
            resolvedTarget = airCombatAnchor;
            Debug.Log(
                $"{TL("Transporte")} PassengerTarget " +
                $"AirCombat#{passenger.InstanceId} -> " +
                $"{airCombatAnchor} tier={airCombatTier}");
            return true;
        }

        // Sem vaga, a missao declarada do passageiro manda; para capturador
        // rogue sem endereco, reusa o buscador do proprio papel. Nunca herda
        // automaticamente o setor ou QG do transportador.
        if (!IsPassengerInPlanSlot(passenger, plan))
        {
            // A MISSAO DO PASSAGEIRO MANDA — E O VERBO NAO IMPORTA.
            //
            // "O transportador e alheio a missao do passageiro. Nao importa se
            // ele vai capturar, fazer pressao, etc. Ele so quer saber da
            // coordenada." (autor, 2026-08-07)
            //
            // A primeira versao disto perguntava AIHasDesignatedCaptureTarget e
            // so atendia Capture. Trocar o intent para Pressure no Inspector
            // fazia o casamento falhar, o courier caia no
            // FindNearestPlanlessCaptureTarget, nao achava capturavel, e o alvo
            // virava a propria celula do APC — "a carga nao vai a lugar nenhum".
            //
            // Sem missao declarada o courier ainda inventa um alvo (o capturavel
            // proximo abaixo), e e por isso que a missao precisa nascer antes dos
            // atalhos do capturador. Ver AIController.Planless.
            if (passenger.AIHasDesignatedMission)
            {
                Vector3Int missionCell = passenger.AIDesignatedMissionTargetCell;
                missionCell.z = 0;
                resolvedTarget = missionCell;
                Debug.Log(
                    $"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} "
                    + $"MISSAO {missionCell} "
                    + $"verbo={passenger.AIDesignatedMissionIntent}");
                return true;
            }

            if (IsRogueCapturerPassenger(passenger, plan))
            {
                ConstructionManager planlessTarget = FindNearestPlanlessCaptureTarget(passenger, snapshot, fallbackCell);
                if (planlessTarget != null)
                {
                    Vector3Int rc = planlessTarget.CurrentCellPosition; rc.z = 0;
                    Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} sem plano → capturavel proximo {rc}");
                    resolvedTarget = rc;
                    return true;
                }
                // Sem capturavel a vista: nao inventa alvo. Deixa o transporte decidir esperar
                // em vez de marchar para o QG inimigo por falta de opcao.
                return false;
            }
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
        // do rogue nem herda automaticamente o setor do transportador.
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
