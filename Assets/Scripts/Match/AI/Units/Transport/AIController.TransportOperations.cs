using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction TryDecideTransportOperationsAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        bool allowOpportunisticPickup = false)
    {
        if (unit == null
            || snapshot == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null)
            return null;

        TransportCapabilityProfile capabilities =
            TransportCapabilityProfile.From(data);
        if (!capabilities.CanTransport && !capabilities.CanSupply)
            return null;

        bool hasCargo = capabilities.CanTransport && HasTransportCargo(unit);
        PlayerAction criticalStockAction =
            TryDecideCriticalTransportStockAction(
                unit, snapshot, data);
        if (criticalStockAction != null)
            return criticalStockAction;

        var context = new TransportOperationContext
        {
            Unit = unit,
            Capabilities = capabilities,
            HasCargo = hasCargo,
            HasPatient = hasCargo && FindEmbarkedPatient(unit) != null,
            StrategicAllowed = true,
            DiagnosticLog = showAILogs ? (System.Action<string>)Debug.Log : null,
            Evaluate = (TransportOperationType operation, AIReachDecisionTier tier,
                int movementBudget,
                out TransportOperationDecision decision) =>
                TryEvaluateTransportOperation(
                    unit, data, snapshot, plan, operation, tier,
                    movementBudget, allowOpportunisticPickup,
                    out decision)
        };

        TransportOperationDecision selected =
            TransportOperationsService.Evaluate(context);
        return MaterializeTransportOperation(
            unit, snapshot, plan, selected);
    }

    /// <summary>
    /// Um transportador puro continua priorizando EVAC/Pickup. Ja um Hub que
    /// presta servicos embarcados (por exemplo, Porta-Avioes) nao deve ficar
    /// aguardando uma aeronave critica quando seus insumos essenciais estao
    /// zerados: sem estoque ele nao consegue cumprir o atendimento que a
    /// carona procura. A guarda exige criticidade real, a flag da ficha e
    /// capacidade Transfer; meia carga ou um transporter de carga pura nao
    /// passam por este desvio.
    /// </summary>
    private PlayerAction TryDecideCriticalTransportStockAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        UnitData data)
    {
        if (unit == null
            || snapshot == null
            || data == null
            || ServiceData.ResolveSupplierServiceProfile(
                    data.supplierServicesProvided)
                != SupplierServiceProfile.FieldService
            || !HasStockTransferCapability(unit, data)
            || !ShouldRestockLogisticsUnit(
                unit, out string restockReason))
            return null;

        StockNeedAssessment need =
            StockNeedAssessmentService.Evaluate(unit);
        if (need == null || need.level < StockNeedLevel.Critical)
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap,
                unit,
                Mathf.Max(0, unit.RemainingMovementPoints),
                terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        if (!TryBuildStockNetworkAction(
                unit,
                snapshot,
                fromCell,
                paths,
                occupied,
                out PlayerAction action,
                out string stockReason,
                allowStrategicDirection: true))
        {
            Debug.Log(
                $"{TL("Transport")} {unit.InstanceId} " +
                $"estoque critico sem rota de estoque; " +
                $"mantem prioridade de transporte ({restockReason}; " +
                $"{stockReason})");
            return null;
        }

        Debug.Log(
            $"{TL("Transport")} {unit.InstanceId} " +
            $"estoque critico preempta EVAC/Pickup " +
            $"({restockReason}; {need.reason}; {stockReason})");
        return action;
    }

    /// <summary>
    /// Depois de varrer pedidos reais de EVAC/Pickup, um transportador vazio
    /// nao fica esperando quem respondeu "Nao quero carona". Unidades normais
    /// regressam para a area da propria producao/HQ; rebeldes ja consumiram a
    /// varredura Strategic e aguardam, sem inventar uma carona local.
    /// </summary>
    private PlayerAction TryBuildEmptyTransportFallbackAction(
        UnitManager unit,
        AIWorldSnapshot snapshot)
    {
        if (unit == null || snapshot == null)
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit,
                Mathf.Max(0, unit.RemainingMovementPoints),
                terrainDatabase);
        if (paths == null || paths.Count == 0)
            return null;

        bool isRebel = matchController != null
            && matchController.IsSlotRebel(
                PlayerSlotId.FromIndex(
                    ResolveAISlotKey(snapshot.AITeam)));
        if (isRebel)
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} sem pedido " +
                "materializavel apos Tactical/Operational/Strategic; " +
                "rebelde aguarda nova oportunidade.");
            return BuildMoveBatch(
                unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        Vector3Int home = FindTransportWaitTarget(
            snapshot.AITeam, fromCell);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int bestCell = fromCell;
        float bestDistance = SectorManager.HexDistance(fromCell, home);
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied.Contains(cell))
                continue;

            float distance = SectorManager.HexDistance(cell, home);
            if (distance < bestDistance)
            {
                bestCell = cell;
                bestDistance = distance;
            }
        }

        Debug.Log($"{TL("Transporte")} {unit.InstanceId} sem pedido " +
            "materializavel apos Tactical/Operational/Strategic; " +
            $"retorna para producao/HQ via {bestCell}.");
        return BuildMoveBatch(
            unit, snapshot.AITeam, fromCell, bestCell, paths);
    }

    private bool TryEvaluateTransportOperation(
        UnitManager unit,
        UnitData data,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        TransportOperationType operation,
        AIReachDecisionTier tier,
        int movementBudget,
        bool allowOpportunisticPickup,
        out TransportOperationDecision decision)
    {
        decision = null;
        switch (operation)
        {
            case TransportOperationType.Hospital:
                if (tier != AIReachDecisionTier.Tactical)
                    return false;
                UnitManager patient = FindEmbarkedPatient(unit);
                if (patient == null)
                    return false;
                decision = CreateTransportDecision(
                    patient, patient.CurrentCellPosition,
                    movementBudget, 100000f,
                    $"paciente embarcado #{patient.InstanceId}");
                return true;

            case TransportOperationType.Courier:
            case TransportOperationType.Delivery:
                if (!HasTransportCargo(unit))
                    return false;
                List<UnitManager> passengers = CollectPassengers(unit);
                if (passengers == null || passengers.Count == 0)
                    return false;
                UnitManager primary =
                    ResolvePrimaryPassenger(unit, passengers, plan);
                Vector3Int cargoTarget = unit.CurrentCellPosition;
                TryResolveCourierPassengerTarget(
                    primary, plan, snapshot, Vector3Int.zero,
                    cargoTarget, out cargoTarget);
                Vector3Int cargoOrigin = unit.CurrentCellPosition;
                cargoOrigin.z = 0;
                float cargoDistance =
                    SectorManager.HexDistance(cargoOrigin, cargoTarget);
                AIReachDecisionTier cargoTier =
                    cargoDistance
                    <= Mathf.Max(0, unit.RemainingMovementPoints)
                       + TransportDropOffRange
                        ? AIReachDecisionTier.Tactical
                        : AIReachDecisionTier.Operational;
                if (cargoTier != tier)
                    return false;
                decision = CreateTransportDecision(
                    primary, cargoTarget, movementBudget, 90000f,
                    $"carga embarcada count={passengers.Count} " +
                    $"dist={cargoDistance:F0}");
                return true;

            case TransportOperationType.Evac:
                return TryQueryTransportEvacOperation(
                    unit, snapshot, plan, tier, movementBudget,
                    out decision);

            case TransportOperationType.Supply:
                return TryQueryTransportSupplyOperation(
                    unit, snapshot, tier, movementBudget, out decision);

            case TransportOperationType.Pickup:
                return TryQueryTransportPickupOperation(
                    unit, snapshot, plan, tier, movementBudget,
                    includeOpportunisticPickup:
                        allowOpportunisticPickup,
                    requiredDisposition: null,
                    out decision);
        }

        return false;
    }

    private static TransportOperationDecision CreateTransportDecision(
        UnitManager target,
        Vector3Int targetCell,
        int movementBudget,
        float score,
        string reason,
        Vector3Int? rendezvousCell = null)
    {
        targetCell.z = 0;
        Vector3Int meetingCell = rendezvousCell ?? targetCell;
        meetingCell.z = 0;
        return new TransportOperationDecision
        {
            TargetUnit = target,
            TargetCell = targetCell,
            RendezvousCell = meetingCell,
            MovementBudget = Mathf.Max(0, movementBudget),
            Score = score,
            Reason = reason
        };
    }

    private bool TryQueryTransportEvacOperation(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        AIReachDecisionTier requestedTier,
        int movementBudget,
        out TransportOperationDecision decision)
    {
        bool found = TryQueryTransportPickupOperation(
            unit, snapshot, plan, requestedTier, movementBudget,
            includeOpportunisticPickup: false,
            requiredDisposition:
                MelhorEmbarqueRideDisposition.Emergency,
            out decision);
        if (!found || decision == null)
            return false;

        decision.Reason = "EVAC " + decision.Reason;
        return true;
    }

    private bool TryQueryTransportSupplyOperation(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        AIReachDecisionTier requestedTier,
        int movementBudget,
        out TransportOperationDecision decision)
    {
        decision = null;
        if (!unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isSupplier)
            return false;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            BuildLogisticsPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        bool baseDefense = IsLogisticsBaseDefenseEmergency(snapshot);

        UnitManager serviceTarget = FindLogisticsServiceTarget(
            unit, snapshot, fromCell, paths, occupied, baseDefense,
            AIReachDecisionStages.Operational);
        if (serviceTarget == null)
            return false;

        Vector3Int targetCell = serviceTarget.CurrentCellPosition;
        targetCell.z = 0;
        float distance = SectorManager.HexDistance(fromCell, targetCell);
        AIReachDecisionTier actualTier =
            distance <= Mathf.Max(0, unit.RemainingMovementPoints)
                ? AIReachDecisionTier.Tactical
                : AIReachDecisionTier.Operational;
        if (actualTier != requestedTier)
            return false;

        decision = CreateTransportDecision(
            serviceTarget, targetCell, movementBudget,
            70000f - distance * 100f,
            $"suprimento=#{serviceTarget.InstanceId} dist={distance:F0}");
        return true;
    }

    private bool TryQueryTransportPickupOperation(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        AIReachDecisionTier requestedTier,
        int movementBudget,
        bool includeOpportunisticPickup,
        MelhorEmbarqueRideDisposition? requiredDisposition,
        out TransportOperationDecision decision)
    {
        decision = null;
        if (!unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isTransporter
            || IsTransporterAtCapacity(unit, data))
            return false;

        int serviceTacticalBudget =
            Mathf.Max(0, unit.RemainingMovementPoints);
        int serviceOperationalTurns = serviceTacticalBudget > 0
            ? Mathf.Max(
                1,
                Mathf.CeilToInt(
                    Mathf.Max(serviceTacticalBudget, movementBudget)
                    / (float)serviceTacticalBudget))
            : 2;
        MelhorEmbarqueResult pickup =
            MelhorEmbarqueService.Evaluate(
                new MelhorEmbarqueRequest
                {
                    transporter = unit,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    tacticalBudget = serviceTacticalBudget,
                    operationalTurns = serviceOperationalTurns,
                    includeStrategic =
                        requestedTier ==
                        AIReachDecisionTier.Strategic,
                    allowPassenger = candidate =>
                        IsStructurallyEligiblePickupCandidate(
                            unit, candidate, snapshot, plan),
                    includeInLegacyRanking = _ => false,
                    evaluateRideNeed = candidate =>
                        EvaluatePickupRideNeed(
                            candidate, plan,
                            serviceOperationalTurns),
                    diagnosticLog = showAILogs
                        ? message => Debug.Log(
                            $"{TL("Transporte")}[MelhorEmbarque] {message}")
                        : null
                });
        MelhorEmbarqueTier serviceTier =
            requestedTier == AIReachDecisionTier.Tactical
                ? MelhorEmbarqueTier.Tactical
                : requestedTier == AIReachDecisionTier.Operational
                    ? MelhorEmbarqueTier.Operational
                    : MelhorEmbarqueTier.Strategic;
        MelhorEmbarqueOption selectedOption =
            pickup.options.Find(option =>
                option != null
                && option.transporterTier == serviceTier
                && CanMaterializePickupRendezvous(option, serviceTier)
                // Um primeiro candidato Strategic inseguro nao pode encerrar
                // a onda inteira: continue procurando o proximo pedido que
                // seja materializavel e seguro.
                && (requestedTier != AIReachDecisionTier.Strategic
                    || IsTransportStrategicTargetSafe(
                        unit, option.lzCell, snapshot))
                && (!requiredDisposition.HasValue
                    || option.rideDisposition ==
                        requiredDisposition.Value)
                && (includeOpportunisticPickup
                    || option.rideDisposition !=
                        MelhorEmbarqueRideDisposition
                            .OpportunisticFallback));
        if (selectedOption?.passenger == null)
            return false;

        Vector3Int servicePassengerCell =
            selectedOption.passenger.CurrentCellPosition;
        servicePassengerCell.z = 0;
        decision = CreateTransportDecision(
            selectedOption.passenger,
            servicePassengerCell,
            movementBudget,
            selectedOption.score,
            $"passageiro=#{selectedOption.passenger.InstanceId} " +
            $"encontro={selectedOption.lzCell} " +
            $"tier={selectedOption.transporterTier} " +
            $"carona={selectedOption.rideDisposition} " +
            $"rotaPax={selectedOption.passengerRouteState} " +
            $"dist={selectedOption.transporterDistance}",
            selectedOption.lzCell);
        decision.PickupOption = selectedOption;
        decision.RideDisposition = selectedOption.rideDisposition;
        decision.PassengerRouteState =
            selectedOption.passengerRouteState;
        decision.PassengerRouteCost =
            selectedOption.passengerRouteCost;
        decision.TransporterRouteCost =
            selectedOption.transporterRouteCost;
        return true;

    }

    private static bool CanMaterializePickupRendezvous(
        MelhorEmbarqueOption option,
        MelhorEmbarqueTier serviceTier)
    {
        if (option?.passenger == null)
            return false;

        // A falta de rota e uma excecao de CAMADA do passageiro aereo:
        // Apache/jet podem precisar que o conves se aproxime para o
        // PodeEmbarcar/PodePousar resolver a transicao Air -> slot. Ela nao
        // vale para infantaria, veiculos ou artilharia; para eles um LZ na
        // outra ilha nao e rendezvous, e sim um destino impossivel.
        bool passengerIsAircraft = option.passenger.TryGetUnitData(
                out UnitData passengerData)
            && passengerData != null
            && passengerData.domain == Domain.Air;
        if (passengerIsAircraft)
            return true;

        if (option.passengerRouteState ==
            MelhorEmbarquePassengerRouteState.NoCurrentRoute)
            return false;

        // Pickup Tactical significa embarque possivel nesta rodada. Um
        // passageiro terrestre que so chega no Operational nao pode puxar o
        // transportador para uma praia que ele ainda nao alcanca.
        return serviceTier != MelhorEmbarqueTier.Tactical
            || option.passengerRouteState ==
                MelhorEmbarquePassengerRouteState.ReachableNow;
    }

    private QueroCaronaResult EvaluatePickupRideNeed(
        UnitManager passenger,
        TeamObjectivePlan plan,
        int operationalTurns)
    {
        SectorObjective assigned = plan != null
            ? ResolveAssignedObjective(passenger, plan)
            : null;
        return QueroCaronaService.Evaluate(
            new QueroCaronaRequest
            {
                unit = passenger,
                map = boardTilemap,
                terrainDatabase = terrainDatabase,
                context = assigned != null
                    ? QueroCaronaContext.ComPlano
                    : QueroCaronaContext.RogueOuRebelde,
                plannedSector = assigned != null
                    ? assigned.Sector
                    : ConstructionSector.None,
                operationalTurns = Mathf.Max(
                    1, operationalTurns),
                emulateUnderRepairFromUnitData = false,
                diagnosticLog = showAILogs
                    ? message => Debug.Log(
                        $"{TL("Transporte")}[QueroCarona] " +
                        $"pax=#{passenger.InstanceId} {message}")
                    : null
            });
    }

    private bool IsStructurallyEligiblePickupCandidate(
        UnitManager transporter,
        UnitManager candidate,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        return candidate != null
            && candidate != transporter
            && candidate.SlotIndex == snapshot.AISlotIndex
            && !candidate.IsDead
            && !candidate.IsEmbarked
            && !IsTransportPassengerClaimedByOther(
                transporter, candidate)
            && !IsAlreadyFormalPassenger(
                candidate, transporter, plan);
    }

    private bool IsTransportPassengerClaimedByOther(
        UnitManager transporter,
        UnitManager passenger)
    {
        if (transporter == null || passenger == null)
            return false;

        foreach (KeyValuePair<int, int> claim in assignedTransportClaims)
        {
            if (claim.Key != transporter.InstanceId
                && claim.Value == passenger.InstanceId)
                return true;
        }

        return false;
    }

    private PlayerAction BuildClaimedTransportPickupMove(
        UnitManager transporter,
        UnitManager passenger,
        TeamId team,
        Vector3Int fromCell,
        Vector3Int destination,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        // Reserva de planejamento da Phase 2. Nao altera unidade, tabuleiro
        // nem recurso confirmado; apenas impede outro transportador de
        // materializar, na mesma passada, uma ordem para o mesmo passageiro.
        assignedTransportClaims[transporter.InstanceId] =
            passenger.InstanceId;
        return BuildMoveBatch(
            transporter, team, fromCell, destination, paths);
    }

    private PlayerAction MaterializeTransportOperation(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        TransportOperationDecision decision)
    {
        if (decision == null)
            return null;

        switch (decision.Operation)
        {
            case TransportOperationType.Hospital:
                return TryDecideSupplierHospitalAction(unit, snapshot, plan);

            case TransportOperationType.Courier:
            case TransportOperationType.Delivery:
                return TryDecideTransportadorAction(unit, snapshot, plan);

            case TransportOperationType.Evac:
                return TryBuildTransportPickupOperation(
                    unit, snapshot, plan, decision);

            case TransportOperationType.Supply:
                return TryBuildTransportSupplyOperation(
                    unit, snapshot, plan, decision);

            case TransportOperationType.Pickup:
                return TryBuildTransportPickupOperation(
                    unit, snapshot, plan, decision);
        }

        return null;
    }

    private PlayerAction TryBuildTransportPickupOperation(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        TransportOperationDecision decision)
    {
        if (decision?.TargetUnit == null)
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit,
                Mathf.Max(0, unit.RemainingMovementPoints),
                terrainDatabase);
        HashSet<Vector3Int> occupied = unit.GetDomain() == Domain.Air
            ? BuildAirOccupied(unit)
            : BuildOccupied(unit);
        Vector3Int serviceRendezvous = decision.RendezvousCell;
        serviceRendezvous.z = 0;

        if (paths != null
            && paths.ContainsKey(serviceRendezvous)
            && serviceRendezvous != fromCell)
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} pickup " +
                      $"{decision.ReachTier}: segue MelhorEmbarque " +
                      $"LZ={serviceRendezvous} passageiro=" +
                      $"#{decision.TargetUnit.InstanceId}.");
            return BuildClaimedTransportPickupMove(
                unit, decision.TargetUnit, snapshot.AITeam,
                fromCell, serviceRendezvous, paths);
        }

        if (serviceRendezvous != fromCell
            && TryFindBestToolProgressionCell(
                unit,
                snapshot,
                fromCell,
                serviceRendezvous,
                paths,
                occupied,
                ToolProgressionIntent.TransportRendezvous,
                out Vector3Int progressionCell,
                out _,
                out string progressionReason)
            && progressionCell != fromCell)
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} pickup " +
                      $"{decision.ReachTier}: progride para MelhorEmbarque " +
                      $"LZ={serviceRendezvous} via={progressionCell} " +
                      $"passageiro=#{decision.TargetUnit.InstanceId} " +
                      $"({progressionReason}).");
            return BuildClaimedTransportPickupMove(
                unit, decision.TargetUnit, snapshot.AITeam,
                fromCell, progressionCell, paths);
        }

        if (serviceRendezvous == fromCell)
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} pickup " +
                      $"{decision.ReachTier}: aguarda na LZ " +
                      $"{serviceRendezvous} passageiro=" +
                      $"#{decision.TargetUnit.InstanceId} " +
                      $"carona={decision.RideDisposition} " +
                      $"rotaPax={decision.PassengerRouteState}.");
            return BuildClaimedTransportPickupMove(
                unit, decision.TargetUnit, snapshot.AITeam,
                fromCell, fromCell, paths);
        }

        Debug.Log($"{TL("Transporte")} {unit.InstanceId} pickup " +
                  $"{decision.ReachTier}: LZ={serviceRendezvous} sem " +
                  "progressao materializavel; libera outras atividades.");
        return null;
    }

    private PlayerAction TryBuildTransportSupplyOperation(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        TransportOperationDecision decision)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            BuildLogisticsPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        bool baseDefense = IsLogisticsBaseDefenseEmergency(snapshot);

        if (decision.ReachTier == AIReachDecisionTier.Tactical)
        {
            return TryBuildLogisticsSupplyAction(
                    unit, snapshot, fromCell, paths, occupied,
                    baseDefense, out PlayerAction supply, out _)
                ? supply
                : null;
        }

        UnitManager serviceTarget = decision.TargetUnit;
        if (decision.ReachTier != AIReachDecisionTier.Operational
            || serviceTarget == null
            || paths == null
            || paths.Count == 0)
        {
            return null;
        }

        Vector3Int anchor = ResolveLogisticsAnchor(snapshot, fromCell);
        if (!TryFindLogisticsRepositionCell(
                unit, snapshot, fromCell, anchor, serviceTarget,
                baseDefense, paths, occupied,
                out Vector3Int moveCell, out _)
            || moveCell == fromCell)
        {
            return null;
        }

        return BuildMoveBatch(
            unit, snapshot.AITeam, fromCell, moveCell, paths);
    }

    private bool IsTransportStrategicTargetSafe(
        UnitManager transporter,
        Vector3Int targetCell,
        AIWorldSnapshot snapshot)
    {
        if (transporter == null || snapshot == null)
            return true;

        targetCell.z = 0;
        if (!IsCellInSafeRear(transporter, snapshot, targetCell))
            return false;

        return !transporter.TryGetUnitData(out UnitData data)
            || data == null
            || !data.playConservative
            || CalculateThreatLevel(targetCell, snapshot.AITeam) <= 0f;
    }
}
