using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int TransportPlanningOperationalTurns = 2;
    private readonly Dictionary<int, TransportPlanningSnapshot>
        transportPlanningSnapshots =
            new Dictionary<int, TransportPlanningSnapshot>();
    // Fato puro, congelado junto com a fila da Fase 2: quais passageiros
    // formam um encontro Tactical materializavel AGORA com cada casco.
    // Nasce do mesmo TransportPlanningSnapshot consumido pela decisao do
    // transportador; iniciativa e cessao de vez nao mantem um segundo modelo
    // de distancia ao lado do MelhorEmbarque.
    private readonly Dictionary<int, HashSet<int>>
        tacticalPickupInitiativePassengers =
            new Dictionary<int, HashSet<int>>();

    private void BuildTacticalPickupInitiativeFacts(
        List<UnitManager> units,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        tacticalPickupInitiativePassengers.Clear();
        if (units == null || snapshot == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager transporter = units[i];
            if (transporter == null
                || transporter.IsDead
                || transporter.IsEmbarked
                || transporter.HasActed
                || !transporter.TryGetUnitData(out UnitData data)
                || data == null
                || !data.isTransporter
                || HasTransportCargo(transporter)
                || IsTransporterAtCapacity(transporter, data))
            {
                continue;
            }

            if (!HasStructurallyEligiblePublishedRideCandidate(
                    transporter, snapshot))
            {
                continue;
            }

            TransportPlanningSnapshot planning =
                GetOrCreateTransportPlanningSnapshot(
                    transporter, snapshot, plan);
            MelhorEmbarqueResult pickup =
                EvaluateTacticalPickupInitiativeFact(
                    planning, snapshot, plan);
            if (pickup?.options == null)
                continue;

            HashSet<int> passengerIds = null;
            for (int optionIndex = 0;
                 optionIndex < pickup.options.Count;
                 optionIndex++)
            {
                MelhorEmbarqueOption option = pickup.options[optionIndex];
                UnitManager passenger = option?.passenger;
                if (passenger == null
                    || passenger.IsDead
                    || passenger.IsEmbarked
                    || passenger.HasActed
                    || option.transporter != transporter
                    || option.transporterTier != MelhorEmbarqueTier.Tactical
                    || option.passengerRouteState !=
                        MelhorEmbarquePassengerRouteState.ReachableNow
                    || (option.rideDisposition !=
                            MelhorEmbarqueRideDisposition.Requested
                        && option.rideDisposition !=
                            MelhorEmbarqueRideDisposition.Emergency)
                    || !CanMaterializePickupRendezvous(
                        option, MelhorEmbarqueTier.Tactical))
                {
                    continue;
                }

                passengerIds ??= new HashSet<int>();
                passengerIds.Add(passenger.InstanceId);
            }

            if (passengerIds == null || passengerIds.Count == 0)
                continue;

            tacticalPickupInitiativePassengers[
                transporter.InstanceId] = passengerIds;

            if (showAILogs)
            {
                var orderedIds = new List<int>(passengerIds);
                orderedIds.Sort();
                Debug.Log(
                    $"{TL("Iniciativa")}[MelhorEmbarque] " +
                    $"transportador=#{transporter.InstanceId} sobe com " +
                    "encontro Tactical ReachableNow para pax=" +
                    $"[{string.Join(",", orderedIds)}].");
            }
        }
    }

    private bool HasStructurallyEligiblePublishedRideCandidate(
        UnitManager transporter,
        AIWorldSnapshot snapshot)
    {
        if (transporter == null || snapshot == null)
            return false;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate != null
                && candidate.AIWantsRide
                && IsStructurallyEligiblePickupCandidate(
                    transporter, candidate, snapshot))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Prova somente o fato consumido pela iniciativa: encontro Tactical,
    /// ReachableNow e carona publicada. Nao marca PickupEvaluated e nao
    /// preenche planning.Pickup; portanto a decisao posterior ainda constroi,
    /// preguiçosamente, o snapshot completo Operational/Strategic.
    /// </summary>
    private MelhorEmbarqueResult EvaluateTacticalPickupInitiativeFact(
        TransportPlanningSnapshot planning,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        UnitManager transporter = planning?.Transporter;
        if (transporter == null
            || snapshot == null)
        {
            return new MelhorEmbarqueResult();
        }

        return MelhorEmbarqueService.Evaluate(
            new MelhorEmbarqueRequest
            {
                transporter = transporter,
                map = boardTilemap,
                terrainDatabase = terrainDatabase,
                tacticalBudget = planning.MovementBudget,
                operationalTurns = 1,
                includeStrategic = false,
                resolveLongRangePassengerMeeting = false,
                buildTransporterManifests = false,
                tacticalOnly = true,
                stopAfterDecisiveTactical = true,
                transporterPaths = planning.TransporterReach,
                // PublishRideNeed acabou de publicar o farol uma vez por
                // unidade. Quem respondeu nao nao entra no produto cartesiano
                // de cada transportador apenas para receber -5000 depois.
                allowPassenger = candidate =>
                    candidate != null
                    && candidate.AIWantsRide
                    && IsStructurallyEligiblePickupCandidate(
                        transporter, candidate, snapshot),
                includeInLegacyRanking = _ => false,
                evaluateRideNeed = candidate =>
                    GetOrEvaluateTransportRideNeed(
                        planning,
                        candidate,
                        plan,
                        TransportPlanningOperationalTurns),
                // Setup de iniciativa nunca emite diagnostico por par. O
                // snapshot completo conserva os logs quando a unidade decide.
                diagnosticLog = null
            });
    }

    private bool HasTacticalPickupInitiativeFact(
        UnitManager transporter,
        UnitManager passenger = null)
    {
        if (transporter == null
            || transporter.IsDead
            || transporter.IsEmbarked
            || transporter.HasActed
            || !tacticalPickupInitiativePassengers.TryGetValue(
                transporter.InstanceId,
                out HashSet<int> passengerIds)
            || passengerIds == null
            || passengerIds.Count == 0)
        {
            return false;
        }

        if (passenger == null)
            return true;

        return !passenger.IsDead
            && !passenger.IsEmbarked
            && !passenger.HasActed
            && passengerIds.Contains(passenger.InstanceId);
    }

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

        // A ficha poe Embarcar no topo dos DOIS modos. Fica aqui, e nao acima
        // do estoque critico, porque reordenar uma preempcao que ja existe e
        // mudanca de outra frente — e um APC nao e supridor, entao na pratica
        // as duas nunca disputam.
        PlayerAction nestedEmbarkAction =
            TryDecideNestedTransportEmbarkAction(unit, snapshot);
        if (nestedEmbarkAction != null)
            return nestedEmbarkAction;

        // Esta autorizacao precisa viver antes do roteador Courier: do
        // contrario, "carga embarcada" vence e transforma a aeronave pronta
        // em uma missao de entrega. Havendo paciente ainda em UnderRepair, o
        // Hospital continua soberano e segura o grupo.
        if (hasCargo
            && unit.GetDomain() == Domain.Naval
            && FindEmbarkedPatient(unit) == null)
        {
            Vector3Int fromCell = unit.CurrentCellPosition;
            fromCell.z = 0;
            if (TryBuildReadyAircraftReleaseAfterNavalPosture(
                    unit,
                    snapshot,
                    plan,
                    fromCell,
                    out PlayerAction aircraftRelease))
            {
                Debug.Log(
                    $"{TL("TransportOps")} {unit.InstanceId} " +
                    "flag=ReadyAircraftToLaunch preempta Courier.");
                return aircraftRelease;
            }
        }

        TransportPlanningSnapshot planningSnapshot = null;
        bool planningSnapshotRequested = false;
        TransportPlanningSnapshot GetPlanningSnapshot()
        {
            if (planningSnapshotRequested)
            {
                AIDecisionPerf.AddCount(
                    "TransportPlanningSnapshotHits");
                return planningSnapshot;
            }

            planningSnapshotRequested = true;
            planningSnapshot =
                GetOrCreateTransportPlanningSnapshot(
                    unit, snapshot, plan);
            return planningSnapshot;
        }

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
                    GetPlanningSnapshot,
                    out decision)
        };

        TransportOperationDecision selected =
            TransportOperationsService.Evaluate(context);
        return MaterializeTransportOperation(
            unit, snapshot, plan, selected);
    }

    /// <summary>
    /// Transportador vazio nao transforma a cabeca de praia em estacionamento.
    /// Se ocupa a construcao de uma missao Capture ainda nao agida, libera o
    /// hex antes de avaliar Pickup — inclusive na IA sem HQ, onde nao existe
    /// TeamObjectivePlan.
    /// </summary>
    private bool TryBuildEmptyTransportCaptureTargetVacateAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        out PlayerAction action)
    {
        action = null;
        if (unit == null
            || snapshot == null
            || HasTransportCargo(unit)
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isTransporter
            || !UnitRoleCompatibility.CanSatisfy(
                data, UnitRole.Transportador)
            // Se a propria ficha captura, o controlador do Capturador decide
            // entre tomar e ceder; esta regra e para o taxi alheio a captura.
            || UnitRoleCompatibility.CanSatisfy(
                data, UnitRole.Capturador))
        {
            return false;
        }

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        if (!TryResolveUnactedCaptureMissionAtCell(
                fromCell,
                unit.SlotIndex,
                unit.InstanceId,
                out UnitManager capturer,
                out ConstructionManager blockedConstruction))
        {
            return false;
        }

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap,
                unit,
                Mathf.Max(0, unit.RemainingMovementPoints),
                terrainDatabase);
        if (paths == null || paths.Count <= 1)
        {
            Debug.LogWarning(
                $"{TL("Transporte")} {unit.InstanceId} bloqueia Capture " +
                $"de #{capturer.InstanceId} em {fromCell}, mas nao possui " +
                "celula valida para liberar o alvo.");
            return false;
        }

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int bestCell = fromCell;
        int bestConstructionRank = int.MaxValue;
        int bestPathCost = int.MaxValue;
        float bestThreat = float.MaxValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell
                || (occupied != null && occupied.Contains(cell))
                // Nao resolve um bloqueio criando o mesmo bloqueio para outro
                // capturador do lote.
                || TryResolveUnactedCaptureMissionAtCell(
                    cell,
                    unit.SlotIndex,
                    unit.InstanceId,
                    out _,
                    out _))
            {
                continue;
            }

            ConstructionManager candidateConstruction =
                ConstructionOccupancyRules.GetConstructionAtCell(
                    boardTilemap, cell);
            if (candidateConstruction != null
                && candidateConstruction.CanProduceUnitsForSlot(
                    snapshot.AISlotIndex))
            {
                continue;
            }

            int constructionRank =
                candidateConstruction == null ? 0 : 1;
            int pathCost = GetPathStepCount(paths, cell);
            float threat = CalculateThreatLevel(
                cell, snapshot.AITeam);
            bool better =
                constructionRank < bestConstructionRank
                || (constructionRank == bestConstructionRank
                    && pathCost < bestPathCost)
                || (constructionRank == bestConstructionRank
                    && pathCost == bestPathCost
                    && threat < bestThreat - 0.001f)
                || (constructionRank == bestConstructionRank
                    && pathCost == bestPathCost
                    && Mathf.Abs(threat - bestThreat) <= 0.001f
                    && (cell.x < bestCell.x
                        || (cell.x == bestCell.x
                            && cell.y < bestCell.y)));
            if (!better)
                continue;

            bestConstructionRank = constructionRank;
            bestPathCost = pathCost;
            bestThreat = threat;
            bestCell = cell;
        }

        if (bestCell == fromCell)
            return false;

        string constructionName = blockedConstruction != null
            ? blockedConstruction.ConstructionDisplayName
            : "construcao";
        Debug.Log(
            $"{TL("Transporte")} {unit.InstanceId} libera " +
            $"{constructionName}@{fromCell} para capturador " +
            $"#{capturer.InstanceId}: " +
            $"vacate {fromCell}->{bestCell} antes de Pickup.");
        action = BuildMoveBatch(
            unit, snapshot.AITeam, fromCell, bestCell, paths);
        return true;
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
    /// ANINHAMENTO — o transportador tambem e passageiro.
    ///
    /// "O Soldado no APC, o APC no navio; um carrega o outro para atravessar o
    /// rio." A ficha poe Embarcar no TOPO dos dois modos justamente por isto:
    /// um transportador que sabe que nao entrega sozinho pede carona tambem.
    ///
    /// A GUARDA DA FICHA JA ESTA PUBLICADA. O verso diz que ele so sobe se isso
    /// encurtar a missao — "nao o faria, pois ja esta no tatico dos passageiros"
    /// — e e exatamente o que o QueroCarona responde: um APC que alcanca o
    /// destino da carga devolve wantsRide=false e nunca entra na fila. Entao o
    /// gate e a propria fila, e nao uma regra nova.
    ///
    /// Irmao de TryEvacEmbarkAction, com tres filtros trocados: vaga em vez de
    /// casco vazio (o navio pode ja levar alguem), a fila como necessidade, e a
    /// guarda contra subir em quem esta embarcado — inclusive dentro de mim.
    /// </summary>
    private PlayerAction TryDecideNestedTransportEmbarkAction(
        UnitManager unit,
        AIWorldSnapshot snapshot)
    {
        if (unit == null
            || snapshot == null
            || unit.IsEmbarked
            || unit.IsDead
            || !HasTransportCargo(unit)
            || !unit.AIIsWaitingForRide)
        {
            return null;
        }

        int budget = Mathf.Max(0, unit.RemainingMovementPoints);
        var options = new List<PodeEmbarcarOption>();
        PodeEmbarcarSensor.CollectOptions(
            unit, boardTilemap, terrainDatabase, budget, options);
        if (options.Count == 0)
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, budget, terrainDatabase);

        for (int i = 0; i < options.Count; i++)
        {
            PodeEmbarcarOption option = options[i];
            UnitManager host = option?.transporterUnit;
            if (host == null
                || host == unit
                || host.IsDead
                || host.IsUnderRepair
                // Embarcado cobre os dois casos de aninhamento mutuo: quem ja
                // esta dentro de outro, e quem esta dentro de MIM.
                || host.IsEmbarked
                || host.SlotIndex != unit.SlotIndex
                || !host.TryGetUnitData(out UnitData hostData)
                || hostData == null
                || IsTransporterAtCapacity(host, hostData))
            {
                continue;
            }

            Debug.Log(
                $"{TL("Transporte")} {unit.InstanceId} ANINHA — embarca em " +
                $"#{host.InstanceId} slot {option.transporterSlotIndex} " +
                $"levando a carga junto.");
            return BuildEmbarcarBatch(
                unit, snapshot.AITeam, fromCell,
                host, option.transporterSlotIndex, paths);
        }

        return null;
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
        Dictionary<Vector3Int, List<Vector3Int>> paths;
        if (!TryGetTransportPlanningReach(
                unit, snapshot, out paths))
        {
            paths = UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit,
                Mathf.Max(0, unit.RemainingMovementPoints),
                terrainDatabase);
        }
        if (paths == null || paths.Count == 0)
            return null;

        PlayerAction rearFollowAction =
            TryBuildConservativeRearFollowAction(
                unit,
                snapshot,
                paths,
                context: "transporte vazio");
        if (rearFollowAction != null)
            return rearFollowAction;

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
        Func<TransportPlanningSnapshot> getPlanningSnapshot,
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
                    getPlanningSnapshot,
                    out decision);

            case TransportOperationType.Supply:
                return TryQueryTransportSupplyOperation(
                    unit, snapshot, tier, movementBudget,
                    getPlanningSnapshot, out decision);

            case TransportOperationType.Pickup:
                return TryQueryTransportPickupOperation(
                    unit, snapshot, plan, tier, movementBudget,
                    includeOpportunisticPickup:
                        allowOpportunisticPickup,
                    requiredDisposition: null,
                    getPlanningSnapshot:
                        getPlanningSnapshot,
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
        Func<TransportPlanningSnapshot> getPlanningSnapshot,
        out TransportOperationDecision decision)
    {
        bool found = TryQueryTransportPickupOperation(
            unit, snapshot, plan, requestedTier, movementBudget,
            includeOpportunisticPickup: false,
            requiredDisposition:
                MelhorEmbarqueRideDisposition.Emergency,
            getPlanningSnapshot:
                getPlanningSnapshot,
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
        Func<TransportPlanningSnapshot> getPlanningSnapshot,
        out TransportOperationDecision decision)
    {
        decision = null;
        if (!unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isSupplier)
            return false;

        TransportPlanningSnapshot planning =
            getPlanningSnapshot?.Invoke();
        EnsureTransportSupplyPlanning(
            planning, snapshot);
        if (planning == null
            || !planning.SupplyEvaluated
            || planning.SupplyTarget == null
            || planning.SupplyTier != requestedTier)
            return false;

        Vector3Int fromCell = planning.Origin;
        UnitManager serviceTarget = planning.SupplyTarget;
        Vector3Int targetCell = serviceTarget.CurrentCellPosition;
        targetCell.z = 0;
        float distance = SectorManager.HexDistance(fromCell, targetCell);
        decision = CreateTransportDecision(
            serviceTarget, targetCell, movementBudget,
            70000f - distance * 100f,
            $"suprimento=#{serviceTarget.InstanceId} dist={distance:F0}");
        decision.PlanningSnapshot = planning;
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
        Func<TransportPlanningSnapshot> getPlanningSnapshot,
        out TransportOperationDecision decision)
    {
        decision = null;
        if (!unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isTransporter
            || IsTransporterAtCapacity(unit, data)
            || getPlanningSnapshot == null)
            return false;

        TransportPlanningSnapshot planning =
            getPlanningSnapshot();
        EnsureTransportPickupPlanning(
            planning, snapshot, plan);
        MelhorEmbarqueResult pickup = planning?.Pickup;
        if (pickup == null)
            return false;
        MelhorEmbarqueTier serviceTier =
            requestedTier == AIReachDecisionTier.Tactical
                ? MelhorEmbarqueTier.Tactical
                : requestedTier == AIReachDecisionTier.Operational
                    ? MelhorEmbarqueTier.Operational
                    : MelhorEmbarqueTier.Strategic;
        // O FUNIL PRECISA DIZER ONDE CADA OPCAO MORREU.
        //
        // Sao cinco clausulas e todas somem pelo mesmo `false`. Com 48 opcoes
        // recusadas e seis tiers em miss, o log dizia exatamente o mesmo que
        // diria se nao houvesse opcao nenhuma — e auditar isso virava chute.
        // O contador por motivo custa nada e transforma a proxima corrida em
        // resposta. Mesma ordem, mesmas condicoes, zero mudanca de regra.
        string ResolveRejection(MelhorEmbarqueOption option)
        {
            if (option == null)
                return "nula";
            if (option.transporterTier != serviceTier)
                return $"tier={option.transporterTier}!={serviceTier}";
            if (!CanMaterializePickupRendezvous(option, serviceTier))
                return $"rotaPax={option.passengerRouteState}";
            // Um primeiro candidato Strategic inseguro nao pode encerrar
            // a onda inteira: continue procurando o proximo pedido que
            // seja materializavel e seguro.
            if (requestedTier == AIReachDecisionTier.Strategic
                && !IsTransportStrategicTargetSafe(
                    unit, option.lzCell, snapshot))
                return $"lzInsegura={option.lzCell}";
            if (requiredDisposition.HasValue
                && option.rideDisposition != requiredDisposition.Value)
                return $"carona={option.rideDisposition}" +
                       $"!={requiredDisposition.Value}";
            if (!includeOpportunisticPickup
                && option.rideDisposition ==
                    MelhorEmbarqueRideDisposition.OpportunisticFallback)
                return "carona=OpportunisticFallback";
            return null;
        }

        System.Predicate<MelhorEmbarqueOption> isMaterializable =
            option => ResolveRejection(option) == null;

        bool IsTacticalGroupManifest(
            MelhorEmbarqueManifestScore manifest)
        {
            if (requestedTier != AIReachDecisionTier.Tactical
                || requiredDisposition.HasValue
                || manifest == null
                || manifest.tier != MelhorEmbarqueTier.Tactical
                || manifest.passengers.Count < 2)
            {
                return false;
            }

            for (int i = 0; i < manifest.passengers.Count; i++)
            {
                MelhorEmbarqueOption passengerOption =
                    manifest.passengers[i];
                if (passengerOption == null
                    || passengerOption.passenger == null
                    || passengerOption.passenger.HasActed
                    || passengerOption.passengerRouteState !=
                        MelhorEmbarquePassengerRouteState.ReachableNow
                    || !isMaterializable(passengerOption))
                {
                    return false;
                }
            }

            return true;
        }

        int CountUnbeaconedManifestPassengers(
            MelhorEmbarqueManifestScore manifest)
        {
            int count = 0;
            for (int i = 0; i < manifest.passengers.Count; i++)
            {
                UnitManager passenger = manifest.passengers[i]?.passenger;
                if (passenger != null
                    && !IsPickupBeaconedByOtherTransport(unit, passenger))
                {
                    count++;
                }
            }
            return count;
        }

        MelhorEmbarqueManifestScore FindDistributedManifest(
            UnitManager requiredPassenger)
        {
            MelhorEmbarqueManifestScore best = null;
            int bestUnbeaconed = -1;
            for (int i = 0; i < pickup.manifests.Count; i++)
            {
                MelhorEmbarqueManifestScore manifest = pickup.manifests[i];
                if (!IsTacticalGroupManifest(manifest)
                    || (requiredPassenger != null
                        && !manifest.passengers.Exists(option =>
                            option?.passenger == requiredPassenger)))
                {
                    continue;
                }

                int unbeaconed = CountUnbeaconedManifestPassengers(manifest);
                if (unbeaconed <= bestUnbeaconed)
                    continue;
                best = manifest;
                bestUnbeaconed = unbeaconed;
            }
            return best;
        }

        // O FAROL. Se este transportador tem viagem devida e ela e
        // materializavel neste tier, ela vem primeiro — mas so isso. Nao ha
        // preempcao: promessa que nao se materializa agora nao trava o veiculo,
        // que segue com a coleta normal e volta a puxar no proximo turno.
        // Uma intersecao Tactical comprovada e consumida como grupo. Nao se
        // procura grupo em Operational/Strategic; sem P2+ ReachableNow na
        // mesma LZ, o funil individual abaixo permanece inalterado.
        TryGetRidePromise(
            unit, out UnitManager promisedPassenger);
        MelhorEmbarqueManifestScore selectedManifest = null;
        if (promisedPassenger != null)
        {
            selectedManifest = FindDistributedManifest(promisedPassenger);
        }
        selectedManifest ??= FindDistributedManifest(null);

        MelhorEmbarqueOption selectedOption = null;
        if (selectedManifest != null)
        {
            selectedOption = promisedPassenger != null
                ? selectedManifest.passengers.Find(option =>
                    option?.passenger == promisedPassenger)
                : null;
            selectedOption ??= selectedManifest.passengers.Find(option =>
                option?.passenger != null
                && !IsPickupBeaconedByOtherTransport(
                    unit, option.passenger));
            selectedOption ??= selectedManifest.passengers[0];
        }
        else if (promisedPassenger != null)
        {
            selectedOption = pickup.options.Find(option =>
                option != null
                && option.passenger == promisedPassenger
                && isMaterializable(option));
        }

        selectedOption ??= pickup.options.Find(option =>
            isMaterializable(option)
            && !IsPickupBeaconedByOtherTransport(
                unit, option.passenger));
        selectedOption ??= pickup.options.Find(isMaterializable);
        if (selectedOption?.passenger == null)
        {
            if (showAILogs && pickup.options.Count > 0)
            {
                var byReason = new Dictionary<string, int>();
                for (int i = 0; i < pickup.options.Count; i++)
                {
                    string why = ResolveRejection(pickup.options[i])
                                 ?? "aceita";
                    byReason.TryGetValue(why, out int count);
                    byReason[why] = count + 1;
                }
                var sb = new System.Text.StringBuilder();
                foreach (KeyValuePair<string, int> entry in byReason)
                {
                    if (sb.Length > 0)
                        sb.Append(" · ");
                    sb.Append($"{entry.Key}={entry.Value}");
                }
                Debug.Log(
                    $"{TL("Transporte")} {unit.InstanceId} " +
                    $"Pickup[{serviceTier}] recusa " +
                    $"{pickup.options.Count} opcoes: {sb}");
            }
            return false;
        }

        Vector3Int servicePassengerCell =
            selectedOption.passenger.CurrentCellPosition;
        servicePassengerCell.z = 0;
        decision = CreateTransportDecision(
            selectedOption.passenger,
            servicePassengerCell,
            movementBudget,
            selectedManifest != null
                ? selectedManifest.score
                : selectedOption.score,
            (selectedManifest != null
                ? $"manifestoPax={selectedManifest.passengers.Count} " +
                  $"passageiros={FormatPickupManifestPassengerIds(selectedManifest)} " +
                  $"coberturaNova={CountUnbeaconedManifestPassengers(selectedManifest)} "
                : $"passageiro=#{selectedOption.passenger.InstanceId} " +
                  $"farol={(IsPickupBeaconedByOtherTransport(unit, selectedOption.passenger) ? "compartilhado" : "novo")} ") +
            $"encontro={selectedOption.lzCell} " +
            $"tier={selectedOption.transporterTier} " +
            $"carona={selectedOption.rideDisposition} " +
            $"rotaPax={selectedOption.passengerRouteState} " +
            $"custoPax={selectedOption.passengerRouteCost}+" +
            $"{selectedOption.passengerEmbarkCost}=" +
            $"{selectedOption.passengerTotalCost} " +
            $"dist={selectedOption.transporterDistance}",
            selectedOption.lzCell);
        decision.PickupOption = selectedOption;
        decision.PickupManifest = selectedManifest;
        decision.RideDisposition = selectedOption.rideDisposition;
        decision.PassengerRouteState =
            selectedOption.passengerRouteState;
        decision.PassengerRouteCost =
            selectedOption.passengerRouteCost;
        decision.TransporterRouteCost =
            selectedOption.transporterRouteCost;
        decision.PlanningSnapshot = planning;

        // Viagem que NAO termina hoje vira promessa. Coleta Tactical se resolve
        // na rodada e nao tem o que prometer — prometer tudo encheria o Mission
        // Intent de ruido.
        if (serviceTier != MelhorEmbarqueTier.Tactical)
        {
            CommitRidePromise(
                unit, selectedOption.passenger, selectedOption.lzCell);
        }

        return true;

    }

    private static string FormatPickupManifestPassengerIds(
        MelhorEmbarqueManifestScore manifest)
    {
        if (manifest == null || manifest.passengers.Count == 0)
            return "[]";

        var builder = new System.Text.StringBuilder("[");
        for (int i = 0; i < manifest.passengers.Count; i++)
        {
            if (i > 0)
                builder.Append(',');
            UnitManager passenger = manifest.passengers[i]?.passenger;
            builder.Append(passenger != null
                ? $"#{passenger.InstanceId}"
                : "?");
        }
        builder.Append(']');
        return builder.ToString();
    }

    private TransportPlanningSnapshot
        GetOrCreateTransportPlanningSnapshot(
            UnitManager unit,
            AIWorldSnapshot snapshot,
            TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null)
            return null;

        int confirmedRevision =
            ResolveTransportPlanningConfirmedRevision(unit);
        if (transportPlanningSnapshots.TryGetValue(
                unit.InstanceId,
                out TransportPlanningSnapshot cached)
            && cached != null
            && cached.Matches(
                unit, snapshot, plan, confirmedRevision))
        {
            AIDecisionPerf.AddCount(
                "TransportPlanningSnapshotHits");
            return cached;
        }

        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;
        var created = new TransportPlanningSnapshot
        {
            Transporter = unit,
            WorldSnapshot = snapshot,
            ObjectivePlan = plan,
            ConfirmedOccupancyRevision = confirmedRevision,
            Origin = origin,
            MovementBudget =
                Mathf.Max(0, unit.RemainingMovementPoints),
            CurrentFuel = Mathf.Max(0, unit.CurrentFuel),
            TransporterReach = BuildLogisticsPaths(unit)
        };

        // Somente um snapshot comprovadamente confirmado pode sobreviver a
        // esta consulta. Durante qualquer estado provisório ele continua
        // local e jamais é publicado no cache da Phase 2.
        if (confirmedRevision >= 0)
            transportPlanningSnapshots[unit.InstanceId] = created;

        AIDecisionPerf.AddCount(
            "TransportPlanningSnapshotBuilds");
        return created;
    }

    private bool TryGetTransportPlanningReach(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        out Dictionary<Vector3Int, List<Vector3Int>> reach)
    {
        reach = null;
        if (unit == null
            || snapshot == null
            || !transportPlanningSnapshots.TryGetValue(
                unit.InstanceId,
                out TransportPlanningSnapshot planning)
            || planning == null
            || !planning.Matches(
                unit,
                snapshot,
                planning.ObjectivePlan,
                ResolveTransportPlanningConfirmedRevision(unit))
            || planning.TransporterReach == null)
        {
            return false;
        }

        reach = planning.TransporterReach;
        AIDecisionPerf.AddCount(
            "TransportPlanningReachReuses");
        return true;
    }

    private int ResolveTransportPlanningConfirmedRevision(
        UnitManager unit)
    {
        if (unit == null
            || boardTilemap == null
            || !ConfirmedOccupancyIndex.TryGetFor(
                boardTilemap,
                out ConfirmedOccupancyIndex occupancy)
            || occupancy == null
            || !occupancy.CanServeLiveQueries
            || !occupancy.TryGetRecord(
                unit,
                out ConfirmedUnitOccupancyRecord record))
        {
            return -1;
        }

        Vector3Int liveCell = unit.CurrentCellPosition;
        liveCell.z = 0;
        return record.cell == liveCell
            && record.domain == unit.GetDomain()
            && record.height == unit.GetHeightLevel()
            && record.slotIndex == unit.SlotIndex
            && record.team == unit.TeamId
            && record.isEmbarked == unit.IsEmbarked
                ? occupancy.ConfirmedRevision
                : -1;
    }

    private void EnsureTransportPickupPlanning(
        TransportPlanningSnapshot planning,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        if (planning == null || planning.PickupEvaluated)
            return;

        planning.PickupEvaluated = true;
        UnitManager unit = planning.Transporter;
        if (unit == null
            || snapshot == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isTransporter
            || IsTransporterAtCapacity(unit, data))
        {
            planning.Pickup = new MelhorEmbarqueResult();
            return;
        }

        planning.Pickup =
            MelhorEmbarqueService.Evaluate(
                new MelhorEmbarqueRequest
                {
                    transporter = unit,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    tacticalBudget = planning.MovementBudget,
                    operationalTurns =
                        TransportPlanningOperationalTurns,
                    // Uma coleta produz Tactical, Operational e Strategic.
                    // EVAC/Pickup apenas filtram esta mesma lista.
                    includeStrategic = true,
                    // ANDAM JUNTAS. Produzir LZ de tier Strategic e recusar
                    // calcular a rota Strategic do passageiro fazia o tier
                    // negar a si mesmo: as LZs distantes existiam, e todo
                    // passageiro fora do orcamento now/later voltava
                    // NoCurrentRoute — 48 opcoes, 48 recusas, no mapa do canal.
                    // Quem pede alcance estrategico paga o mapa estrategico.
                    resolveLongRangePassengerMeeting = true,
                    stopAfterDecisiveTactical = true,
                    // O runtime agrega apenas para descobrir uma coleta
                    // conjunta que se resolve AGORA. O consumidor ignora
                    // manifestos Operational/Strategic e conserva o fluxo
                    // individual nesses horizontes.
                    buildTransporterManifests = true,
                    transporterPaths = planning.TransporterReach,
                    allowPassenger = candidate =>
                        IsStructurallyEligiblePickupCandidate(
                            unit, candidate, snapshot),
                    includeInLegacyRanking = _ => false,
                    // O Quero Carona monta o proprio envelope de Captura
                    // (turnos encadeados). A malha do MelhorEmbarque continua
                    // sendo MP x N num bolso so — emprestar ela de volta era
                    // justamente o alcance fantasma que fazia o passageiro
                    // recusar carona. Ver Fase 4 da migracao do envelope.
                    evaluateRideNeed = candidate =>
                        GetOrEvaluateTransportRideNeed(
                            planning,
                            candidate,
                            plan,
                            TransportPlanningOperationalTurns),
                    diagnosticLog = showAILogs
                        ? message => Debug.Log(
                            $"{TL("Transporte")}[MelhorEmbarque] {message}")
                        : null
                });

        if (showAILogs)
        {
            Debug.Log(
                $"{TL("Transporte")}[PlanningSnapshot] " +
                $"unit=#{unit.InstanceId} " +
                $"confirmedRev={planning.ConfirmedOccupancyRevision} " +
                $"reach={planning.TransporterReach?.Count ?? 0} " +
                $"rideNeeds={planning.RideNeedByPassenger.Count} " +
                $"tiers=3 options={planning.Pickup.options.Count} " +
                $"manifests={planning.Pickup.manifests.Count} " +
                $"ranking={planning.Pickup.ranking.Count}");
        }
    }

    private void EnsureTransportSupplyPlanning(
        TransportPlanningSnapshot planning,
        AIWorldSnapshot snapshot)
    {
        if (planning == null || planning.SupplyEvaluated)
            return;

        planning.SupplyEvaluated = true;
        UnitManager unit = planning.Transporter;
        if (unit == null
            || snapshot == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isSupplier)
        {
            return;
        }

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        planning.SupplyBaseDefense =
            IsLogisticsBaseDefenseEmergency(snapshot);
        planning.SupplyTarget = FindLogisticsServiceTarget(
            unit,
            snapshot,
            planning.Origin,
            planning.TransporterReach,
            occupied,
            planning.SupplyBaseDefense,
            AIReachDecisionStages.Operational);
        if (planning.SupplyTarget == null)
            return;

        Vector3Int targetCell =
            planning.SupplyTarget.CurrentCellPosition;
        targetCell.z = 0;
        planning.SupplyTier =
            SectorManager.HexDistance(
                planning.Origin, targetCell)
            <= planning.MovementBudget
                ? AIReachDecisionTier.Tactical
                : AIReachDecisionTier.Operational;
    }

    private QueroCaronaResult GetOrEvaluateTransportRideNeed(
        TransportPlanningSnapshot planning,
        UnitManager passenger,
        TeamObjectivePlan plan,
        int operationalTurns)
    {
        if (planning == null || passenger == null)
            return null;
        if (planning.RideNeedByPassenger.TryGetValue(
                passenger.InstanceId,
                out QueroCaronaResult cached))
        {
            AIDecisionPerf.AddCount(
                "TransportPlanningRideNeedHits");
            return cached;
        }

        QueroCaronaResult evaluated = EvaluatePickupRideNeed(
            passenger,
            plan,
            operationalTurns,
            planning.WorldSnapshot);
        ApplyRideWaitStamp(passenger, evaluated);
        planning.RideNeedByPassenger[passenger.InstanceId] =
            evaluated;
        return evaluated;
    }

    private MelhorEmbarqueResult
        GetOrBuildTransportPassengerProjection(
            TransportPlanningSnapshot planning,
            UnitManager passenger,
            QueroCaronaResult rideNeed)
    {
        if (planning == null || passenger == null)
            return new MelhorEmbarqueResult();

        bool rideNeedChanged =
            !planning.RideNeedByPassenger.TryGetValue(
                passenger.InstanceId,
                out QueroCaronaResult previousRideNeed)
            || !ReferenceEquals(previousRideNeed, rideNeed);
        planning.RideNeedByPassenger[passenger.InstanceId] =
            rideNeed;
        if (rideNeedChanged)
        {
            planning.PassengerPickupProjections.Remove(
                passenger.InstanceId);
        }

        // Se o transportador já produziu o panorama completo, Assault apenas
        // filtra esse resultado. Caso contrário, cria uma projeção estreita
        // para o passageiro, mas reutiliza o mesmo alcance do transportador.
        if (planning.PickupEvaluated && planning.Pickup != null)
        {
            bool containsPassenger =
                planning.Pickup.options != null
                && planning.Pickup.options.Exists(option =>
                    option?.passenger == passenger
                    && (option.rideDisposition
                            == MelhorEmbarqueRideDisposition.Requested
                        || option.rideDisposition
                            == MelhorEmbarqueRideDisposition.Emergency));
            if (containsPassenger)
            {
                AIDecisionPerf.AddCount(
                    "TransportPlanningPassengerProjectionHits");
                return planning.Pickup;
            }

            planning.PickupEvaluated = false;
        }

        if (planning.PassengerPickupProjections.TryGetValue(
                passenger.InstanceId,
                out MelhorEmbarqueResult cached))
        {
            AIDecisionPerf.AddCount(
                "TransportPlanningPassengerProjectionHits");
            return cached;
        }

        MelhorEmbarqueResult evaluated =
            MelhorEmbarqueService.Evaluate(
                new MelhorEmbarqueRequest
                {
                    transporter = planning.Transporter,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    tacticalBudget = planning.MovementBudget,
                    operationalTurns =
                        TransportPlanningOperationalTurns,
                    includeStrategic = false,
                    resolveLongRangePassengerMeeting = true,
                    transporterPaths = planning.TransporterReach,
                    allowPassenger = candidate =>
                        candidate == passenger,
                    includeInLegacyRanking = _ => false,
                    evaluateRideNeed = candidate =>
                        candidate == passenger ? rideNeed : null
                });
        planning.PassengerPickupProjections[
            passenger.InstanceId] = evaluated;
        AIDecisionPerf.AddCount(
            "TransportPlanningPassengerProjectionBuilds");
        return evaluated;
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

        // SEM ROTA e impossibilidade; ReachableStrategic e DISTANCIA.
        //
        // Os dois eram recusados juntos, e isso fazia o tier Strategic negar o
        // proprio horizonte: com orcamento infinito, ele so aceitava estados de
        // rota que cabem no Operational. Um navio nunca buscava infantaria a
        // 30 hexes da praia — nao por alcance, mas porque a unica classificacao
        // que aquela infantaria podia ter era a proibida.
        //
        // NoCurrentRoute continua fora: ali o mapa de longo alcance NAO achou
        // encontro nenhum, e LZ na outra ilha segue sendo destino impossivel.
        if (option.passengerRouteState ==
                MelhorEmbarquePassengerRouteState.NoCurrentRoute)
            return false;

        // Cada tier aceita ate o SEU horizonte, e nao alem:
        //   Tactical    embarque nesta rodada
        //   Operational o passageiro chega andando nos proximos turnos
        //   Strategic   vale o encontro de longo alcance — e so aqui
        switch (serviceTier)
        {
            case MelhorEmbarqueTier.Tactical:
                return option.passengerRouteState ==
                    MelhorEmbarquePassengerRouteState.ReachableNow;
            case MelhorEmbarqueTier.Operational:
                return option.passengerRouteState ==
                        MelhorEmbarquePassengerRouteState.ReachableNow
                    || option.passengerRouteState ==
                        MelhorEmbarquePassengerRouteState.ReachableLater;
            default:
                return true;
        }
    }

    // Classificacao ESTRUTURAL, nao por altura: uma aeronave pousada continua
    // aeronave e continua sem responder a pergunta terrestre. Cobre asa fixa,
    // helicoptero e hibridos (GetAircraftType deriva de UnitData.IsAircraft).
    private static bool IsAircraftPassenger(UnitManager passenger)
    {
        return passenger != null
            && passenger.GetAircraftType() != AircraftType.None;
    }

    /// <summary>
    /// A PERGUNTA DA CARONA TEM DONO DECLARADO.
    ///
    /// O QueroCarona terrestre mede necessidade pelo envelope de CAPTURA
    /// (Intent=Capture, SubStep=Terrestre). Ele so responde por quem captura.
    /// Perguntado a outra peca, devolve sempre a mesma coisa e sempre falsa —
    /// "sem predio capturavel alcancavel" — porque nao existe predio que ela
    /// capture. Foi assim que um caca de tanque cheio e um APC com missao
    /// propria entraram na fila da carona na frente da infantaria encalhada.
    ///
    /// Aeronave fica de fora mesmo que satisfaca Capturador: rebasing e decisao
    /// dela, no turno dela, pelo QueroCaronaAerea — que compara a plataforma
    /// com a MISSAO dela, algo que o transportador nao tem como saber.
    ///
    /// Sem dono, a resposta e "nao se aplica" — NUNCA "sim".
    /// </summary>
    private static bool ClaimsGroundCaptureRideQuestion(
        UnitManager passenger)
    {
        return passenger != null
            && !IsAircraftPassenger(passenger)
            && passenger.TryGetUnitData(out UnitData data)
            && data != null
            && UnitRoleCompatibility.CanSatisfy(
                data,
                UnitRole.Capturador);
    }

    /// <summary>
    /// A CARONA NAO PERGUNTA A MISSAO — PERGUNTA A COORDENADA.
    ///
    /// Um transportador carregado tem destino: o da carga. Antes disto, ele
    /// caia no ramo "nao captura nada, logo nao tem assunto" e ficava MUDO —
    /// e um APC cheio de soldados no lado errado do canal nunca pedia o navio,
    /// porque a unica fonte de coordenada embutida na pergunta era "capturavel
    /// que EU capturo".
    ///
    /// A fonte da ancora e de quem pergunta, nao da pergunta. Com a celula em
    /// maos, o ApplyBeyondReachRideNeed responde por topologia pura: esta fora
    /// do meu componente? entao so chego de carona.
    /// </summary>
    private bool TryResolveCargoDestinationAnchor(
        UnitManager passenger,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        out Vector3Int anchor)
    {
        anchor = Vector3Int.zero;
        if (passenger == null
            || snapshot == null
            || !HasTransportCargo(passenger))
        {
            return false;
        }

        List<UnitManager> cargo = CollectPassengers(passenger);
        UnitManager primary =
            ResolvePrimaryPassenger(passenger, cargo, plan);
        if (primary == null)
            return false;

        Vector3Int fromCell = passenger.CurrentCellPosition;
        fromCell.z = 0;
        if (!TryResolveCourierPassengerTarget(
                primary, plan, snapshot,
                Vector3Int.zero, fromCell,
                out Vector3Int resolved))
        {
            return false;
        }

        resolved.z = 0;
        // Destino igual a posicao atual nao e destino: nao ha para onde pedir
        // carona, e publicar isso encheria a fila de pedido vazio.
        if (resolved == fromCell)
            return false;

        anchor = resolved;
        return true;
    }

    /// <summary>
    /// A missao herdada do transportador CARREGADO, publicada no setup da Fase
    /// 2 — antes de qualquer unidade agir.
    ///
    /// Plano escrito depois de executar nao coordena ninguem, que e a unica
    /// funcao dele. O invariante transacional protege o TABULEIRO (FoW,
    /// ocupacao, recurso, unidade marcada como agida); missao e intencao, e
    /// intencao tem de estar publicada quando os outros olham.
    ///
    /// E ela NAO depende de decidir nada: quem esta a bordo e para onde essa
    /// carga vai sao fatos, sabiveis antes da primeira selecao. Comparar com a
    /// promessa, que E a decisao e por isso continua no commit pos-acao:
    ///
    ///     courier / need a lift   ancora = destino da carga   -> aqui, cedo
    ///     pickup  / ASAP          ancora = o encontro         -> commit pos-acao
    ///
    /// Sem isto o navio (grp=2) lia a ficha do APC (grp=4) exatamente na janela
    /// em que ela descrevia o turno anterior.
    ///
    /// ❓ Carga E vaga livre com alguem na fila e o caso que a tabela de quatro
    /// estados nao resolve: aqui a carga ganha, porque courier e o estado que a
    /// unidade ja esta vivendo.
    /// </summary>
    private void PublishInheritedMissionIntent(
        UnitManager unit,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot)
    {
        if (unit == null
            || unit.IsDead
            || unit.IsEmbarked
            || !HasTransportCargo(unit))
        {
            return;
        }

        // Nao piso em missao de outro dono (Restock, por exemplo) — mesma
        // guarda que CommitRidePromise usa.
        if (unit.AIHasDesignatedMission
            && unit.AIDesignatedMissionIntent
                != AIPlanRuntimeIntent.Transport)
        {
            return;
        }

        List<UnitManager> cargo = CollectPassengers(unit);
        UnitManager primary =
            ResolvePrimaryPassenger(unit, cargo, plan);
        if (primary == null
            || !TryResolveCargoDestinationAnchor(
                unit, plan, snapshot, out Vector3Int anchor))
        {
            return;
        }

        bool changed =
            !unit.AIHasDesignatedMission
            || unit.AIDesignatedMissionTargetCell != anchor
            || unit.AIDesignatedMissionTargetUnitInstanceId
                != primary.InstanceId;

        unit.SetAIDesignatedMission(
            AIPlanRuntimeIntent.Transport,
            anchor,
            targetUnitInstanceId: primary.InstanceId);

        if (changed && showAILogs)
        {
            Debug.Log(
                $"{TL("Missao")} {unit.InstanceId} HERDA da carga " +
                $"#{primary.InstanceId} -> Transport {anchor} " +
                "(publicada no setup).");
        }
    }

    private QueroCaronaResult EvaluatePickupRideNeed(
        UnitManager passenger,
        TeamObjectivePlan plan,
        int operationalTurns,
        AIWorldSnapshot snapshot)
    {
        // ANTES do dono da pergunta: quem carrega tem coordenada propria, e a
        // coordenada dispensa a pergunta de captura inteira.
        if (!ClaimsGroundCaptureRideQuestion(passenger)
            && TryResolveCargoDestinationAnchor(
                passenger, plan, snapshot, out Vector3Int cargoAnchor))
        {
            return QueroCaronaService.Evaluate(
                new QueroCaronaRequest
                {
                    unit = passenger,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    context = QueroCaronaContext.RogueOuRebelde,
                    useExplicitTarget = true,
                    explicitTarget = cargoAnchor,
                    explicitTargetLabel =
                        $"destino da carga {cargoAnchor}",
                    operationalTurns = Mathf.Max(1, operationalTurns),
                    emulateUnderRepairFromUnitData = true,
                    diagnosticLog = showAILogs
                        ? message => Debug.Log(
                            $"{TL("Transporte")}[QueroCarona] " +
                            $"pax=#{passenger.InstanceId} (carregado) " +
                            $"{message}")
                        : null
                });
        }

        // Sem dono para a pergunta, sobra a unica necessidade que o
        // transportador enxerga sozinho: a emergencia de recuperacao. Ela
        // continua valendo para TODA peca — artilharia ferida sendo evacuada
        // nao depende de capturar coisa nenhuma.
        if (!ClaimsGroundCaptureRideQuestion(passenger))
        {
            string tag = IsAircraftPassenger(passenger)
                ? "[QueroCaronaAerea]"
                : "[QueroCarona]";
            QueroCaronaResult emergencyOnly =
                QueroCaronaService.EvaluateEmergencyOnly(
                    new QueroCaronaRequest
                    {
                        unit = passenger,
                        map = boardTilemap,
                        terrainDatabase = terrainDatabase,
                        context = QueroCaronaContext.RogueOuRebelde,
                        operationalTurns = Mathf.Max(
                            1, operationalTurns),
                        emulateUnderRepairFromUnitData = true,
                        diagnosticLog = showAILogs
                            ? message => Debug.Log(
                                $"{TL("Transporte")}{tag} " +
                                $"pax=#{passenger.InstanceId} {message}")
                            : null
                    });
            if (emergencyOnly != null)
                emergencyOnly.captureQuestionInapplicable = true;
            return emergencyOnly;
        }

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

    // ESTRUTURAL, e so. Este callback e consultado dentro de
    // TryResolvePassengerSlot ANTES do teste de slot compativel, entao tudo que
    // custa caro aqui e pago tambem por submarino, fragata e trem de carga —
    // gente que jamais caberia no hangar. A necessidade de carona (consulta de
    // envelope) mora no MelhorEmbarque, depois do filtro de vaga.
    private bool IsStructurallyEligiblePickupCandidate(
        UnitManager transporter,
        UnitManager candidate,
        AIWorldSnapshot snapshot)
    {
        if (candidate == null
            || candidate == transporter
            || candidate.SlotIndex != snapshot.AISlotIndex
            || candidate.IsDead
            || candidate.IsEmbarked)
        {
            return false;
        }

        // Farol impossivel nao orienta: se os componentes de movimento nao se
        // tocam, este par fisico nao existe. Isso nao diz nada sobre os outros
        // transportadores, que avaliam o mesmo passageiro independentemente.
        if (!CanTransporterMeetPassenger(transporter, candidate))
        {
            if (showAILogs)
            {
                Debug.Log(
                    $"{TL("Transporte")} #{transporter.InstanceId} descarta " +
                    $"pax=#{candidate.InstanceId}: componentes de movimento " +
                    "nao se tocam (o veiculo nao chega ate ele).");
            }
            return false;
        }

        return true;
    }

    private bool IsTransportPickupBeaconFor(
        UnitManager transporter,
        UnitManager passenger)
    {
        return transporter != null
            && passenger != null
            && ((transportPickupBeacons.TryGetValue(
                     transporter.InstanceId,
                     out int primaryPassengerId)
                 && primaryPassengerId == passenger.InstanceId)
                || (tacticalPickupManifestBeacons.TryGetValue(
                        transporter.InstanceId,
                        out HashSet<int> manifestPassengers)
                    && manifestPassengers != null
                    && manifestPassengers.Contains(passenger.InstanceId)));
    }

    private bool IsPickupBeaconedByOtherTransport(
        UnitManager transporter,
        UnitManager passenger)
    {
        if (transporter == null || passenger == null)
            return false;

        foreach (KeyValuePair<int, int> beacon in transportPickupBeacons)
        {
            if (beacon.Key != transporter.InstanceId
                && beacon.Value == passenger.InstanceId)
            {
                return true;
            }
        }

        foreach (KeyValuePair<int, HashSet<int>> beacon
                 in tacticalPickupManifestBeacons)
        {
            if (beacon.Key != transporter.InstanceId
                && beacon.Value != null
                && beacon.Value.Contains(passenger.InstanceId))
            {
                return true;
            }
        }

        // Os dicionarios acima so vivem durante a Phase 2 atual. A promessa
        // gravada no Mission Intent sobrevive entre turnos e precisa participar
        // da mesma distribuicao; caso contrario, todo transportador que decide
        // antes/depois em outra rodada enxerga a fila como se fosse o primeiro.
        // Continua sendo farol, nao lock: os chamadores primeiro preferem um
        // passageiro sem farol e depois fazem fallback para qualquer candidato.
        foreach (UnitManager other in UnitManager.AllActive)
        {
            if (other == null
                || other == transporter)
            {
                continue;
            }

            if (HasActiveRidePromiseFor(other, passenger))
                return true;
        }

        return false;
    }

    private PlayerAction BuildTransportPickupMove(
        UnitManager transporter,
        UnitManager passenger,
        TeamId team,
        Vector3Int fromCell,
        Vector3Int destination,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        MelhorEmbarqueManifestScore manifest = null)
    {
        // Farol de planejamento da Phase 2. Nao altera unidade, tabuleiro nem
        // recurso confirmado e, principalmente, nao bloqueia outro veiculo de
        // escolher os mesmos passageiros. Se o batch abortar, Phase2 remove o
        // farol deste transportador.
        if (passenger != null)
        {
            transportPickupBeacons[transporter.InstanceId] =
                passenger.InstanceId;
        }

        if (manifest != null)
        {
            var manifestPassengers = new HashSet<int>();
            for (int i = 0; i < manifest.passengers.Count; i++)
            {
                UnitManager manifestPassenger =
                    manifest.passengers[i]?.passenger;
                if (manifestPassenger != null)
                    manifestPassengers.Add(manifestPassenger.InstanceId);
            }
            tacticalPickupManifestBeacons[transporter.InstanceId] =
                manifestPassengers;
        }
        else
        {
            tacticalPickupManifestBeacons.Remove(transporter.InstanceId);
        }
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
            decision.PlanningSnapshot?.TransporterReach
            ?? UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit,
                Mathf.Max(0, unit.RemainingMovementPoints),
                terrainDatabase);
        if (decision.PlanningSnapshot?.TransporterReach != null)
            AIDecisionPerf.AddCount(
                "TransportPlanningReachReuses");
        HashSet<Vector3Int> occupied = unit.GetDomain() == Domain.Air
            ? BuildAirOccupied(unit)
            : BuildOccupied(unit);
        Vector3Int serviceRendezvous = decision.RendezvousCell;
        serviceRendezvous.z = 0;
        string pickupTargets = decision.PickupManifest != null
            ? $"manifesto={FormatPickupManifestPassengerIds(decision.PickupManifest)}"
            : $"passageiro=#{decision.TargetUnit.InstanceId}";

        if (paths != null
            && paths.ContainsKey(serviceRendezvous)
            && serviceRendezvous != fromCell)
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} pickup " +
                      $"{decision.ReachTier}: segue MelhorEmbarque " +
                      $"LZ={serviceRendezvous} {pickupTargets}.");
            return BuildTransportPickupMove(
                unit, decision.TargetUnit, snapshot.AITeam,
                fromCell, serviceRendezvous, paths,
                decision.PickupManifest);
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
                      $"{pickupTargets} " +
                      $"({progressionReason}).");
            return BuildTransportPickupMove(
                unit, decision.TargetUnit, snapshot.AITeam,
                fromCell, progressionCell, paths,
                decision.PickupManifest);
        }

        if (serviceRendezvous == fromCell)
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} pickup " +
                      $"{decision.ReachTier}: aguarda na LZ " +
                      $"{serviceRendezvous} {pickupTargets} " +
                      $"carona={decision.RideDisposition} " +
                      $"rotaPax={decision.PassengerRouteState}.");
            return BuildTransportPickupMove(
                unit, decision.TargetUnit, snapshot.AITeam,
                fromCell, fromCell, paths,
                decision.PickupManifest);
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
            decision?.PlanningSnapshot?.TransporterReach
            ?? BuildLogisticsPaths(unit);
        if (decision?.PlanningSnapshot?.TransporterReach != null)
            AIDecisionPerf.AddCount(
                "TransportPlanningReachReuses");
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        bool baseDefense =
            decision?.PlanningSnapshot != null
                ? decision.PlanningSnapshot.SupplyBaseDefense
                : IsLogisticsBaseDefenseEmergency(snapshot);

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
        if (TryBuildTargetedLogisticsSupplyAction(
                unit,
                snapshot,
                fromCell,
                serviceTarget,
                paths,
                occupied,
                baseDefense,
                out PlayerAction targetedSupply,
                out string targetedSupplyReason))
        {
            Debug.Log(
                $"{TL("Transporte")} {unit.InstanceId} supply " +
                $"{decision.ReachTier}: move e atende " +
                $"{serviceTarget.UnitDisplayName}#" +
                $"{serviceTarget.InstanceId} " +
                $"({targetedSupplyReason}).");
            return targetedSupply;
        }

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

        // A PRUDENCIA E DE QUEM PEDE A PRUDENCIA.
        //
        // Retaguarda e ameaca nasceram para proteger a artilharia de campanha
        // que andava de caminhao: a carga e que era fragil, nao o oficio. Mas
        // a regra estava no PAPEL, entao valia para todo transportador — e
        // exigir InRearSlice de uma LZ estrategica torna o resgate
        // estruturalmente impossivel, porque o ponto de recolha esta, por
        // definicao, onde a tropa ficou: fora da linha. No mapa do canal as
        // 36 opcoes estrategicas eram recusadas TODAS, turno apos turno.
        //
        // Agora quem responde e a ficha. Porta-Avioes e caminhao de
        // suprimentos declaram playConservative e continuam presos a
        // retaguarda e ao hex sem ameaca. Chinook, hidroaviao, APC, trem de
        // carga e navio de desembarque nao declaram nada — e nao lhes e
        // perguntado. A Marcha ja tinha decidido: "Nao fico esperando a tropa
        // me encontrar; eu vou ao encontro de quem precisa embarcar."
        if (!transporter.TryGetUnitData(out UnitData data)
            || data == null
            || !data.playConservative)
        {
            return true;
        }

        return IsCellInSafeRear(transporter, snapshot, targetCell)
            && CalculateThreatLevel(targetCell, snapshot.AITeam) <= 0f;
    }
}
