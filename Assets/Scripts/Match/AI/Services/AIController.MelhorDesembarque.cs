using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // Adaptador da IA para o avaliador puro de desembarque. Cada comportamento
    // resolve a intencao das cargas (captura, patrulha, fogo ou logistica) e
    // entrega aqui apenas passageiro -> celula alvo. O resultado ja contem a
    // celula concreta do transportador e um spot exclusivo para cada carga.
    private bool TryEvaluateBestDisembark(
        UnitManager transporter,
        IReadOnlyDictionary<int, Vector3Int> passengerTargets,
        out MelhorDesembarqueResult result,
        int routeHorizon = 120,
        int movementBudget = -1,
        int maxRemainingRouteCost = int.MaxValue,
        Dictionary<Vector3Int, List<Vector3Int>> pathsByDestination = null,
        IReadOnlyDictionary<int, int> passengerPriorityByInstanceId = null)
    {
        result = new MelhorDesembarqueResult();
        if (transporter == null
            || boardTilemap == null
            || terrainDatabase == null
            || passengerTargets == null
            || passengerTargets.Count == 0)
            return false;

        int budget = movementBudget >= 0
            ? movementBudget
            : Mathf.Max(0, transporter.RemainingMovementPoints);

        // Uma rota reversa por passageiro/alvo, compartilhada por todas as LZs.
        // Sem isto a mesma malha seria reconstruida uma vez para cada spot.
        var routeCache =
            new Dictionary<int, Dictionary<Vector3Int, int>>();

        bool ResolveTarget(
            UnitManager passenger,
            Vector3Int from,
            out Vector3Int target,
            out int routeCost)
        {
            target = Vector3Int.zero;
            routeCost = int.MaxValue;
            if (passenger == null
                || !passengerTargets.TryGetValue(
                    passenger.InstanceId, out target))
                return false;

            target.z = 0;
            from.z = 0;
            if (from == target)
            {
                routeCost = 0;
                return true;
            }

            if (!routeCache.TryGetValue(
                    passenger.InstanceId,
                    out Dictionary<Vector3Int, int> reverseRoute))
            {
                reverseRoute = UnitMovementPathRules.CalculateMovementCostMap(
                    boardTilemap,
                    passenger,
                    target,
                    Mathf.Max(1, routeHorizon),
                    terrainDatabase);
                routeCache[passenger.InstanceId] = reverseRoute;
            }

            return reverseRoute != null
                && reverseRoute.TryGetValue(from, out routeCost)
                && routeCost <= maxRemainingRouteCost;
        }

        result = MelhorDesembarqueService.Evaluate(
            new MelhorDesembarqueRequest
            {
                transporter = transporter,
                map = boardTilemap,
                terrainDatabase = terrainDatabase,
                movementBudget = budget,
                pathsByDestination = pathsByDestination,
                resolvePassengerTarget = ResolveTarget,
                passengerPriorityByInstanceId =
                    passengerPriorityByInstanceId,
                allowTransporterCell = IsConfirmedVisibleCellForAI,
                allowDisembarkCell = IsConfirmedVisibleCellForAI,
                diagnosticLog = showAILogs
                    ? message => Debug.Log(
                        $"{TL("Transporte")}[MelhorDesembarque] {message}")
                    : null
            });
        return result.best != null;
    }

    private bool TryEvaluateBestDisembark(
        UnitManager transporter,
        UnitManager passenger,
        Vector3Int target,
        out MelhorDesembarqueResult result,
        int routeHorizon = 120,
        int movementBudget = -1,
        int maxRemainingRouteCost = int.MaxValue,
        Dictionary<Vector3Int, List<Vector3Int>> pathsByDestination = null,
        IReadOnlyDictionary<int, int> passengerPriorityByInstanceId = null)
    {
        var targets = new Dictionary<int, Vector3Int>();
        if (passenger != null)
        {
            target.z = 0;
            targets[passenger.InstanceId] = target;
        }

        return TryEvaluateBestDisembark(
            transporter,
            targets,
            out result,
            routeHorizon,
            movementBudget,
            maxRemainingRouteCost,
            pathsByDestination,
            passengerPriorityByInstanceId);
    }

    private bool TryBuildBestCourierDisembarkAction(
        UnitManager transporter,
        List<UnitManager> passengers,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int assignedSectorTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        int maxRemainingRouteCost,
        string consumer,
        out PlayerAction action)
    {
        action = null;
        if (transporter == null
            || passengers == null
            || passengers.Count == 0
            || snapshot == null)
            return false;

        UnitManager primary =
            ResolvePrimaryPassenger(transporter, passengers, plan);
        if (primary == null
            || !TryBuildCourierDisembarkTargets(
                transporter,
                passengers,
                plan,
                snapshot,
                assignedSectorTarget,
                out Dictionary<int, Vector3Int> targets))
            return false;

        ApplyOperationalDisembarkCapacity(
            transporter,
            passengers,
            snapshot,
            targets);
        if (targets.Count == 0)
            return false;

        int requiredJointDeliveries = ResolveRequiredJointDeliveries(
            transporter, passengers, plan, snapshot, targets);
        bool usesFreeCargoFlow = UsesFreeCargoDisembarkFlow(
            passengers, plan, snapshot, targets);
        // Um grupo rebelde/rogue de duas cargas usa a semantica integral da
        // ferramenta Melhor Desembarque: primeiro maximiza passageiros
        // entregues e depois minimiza a soma das rotas. O antigo
        // TransportDropOffRange era uma regra de entrega pingada e eliminava
        // justamente o segundo passageiro (por exemplo R5), embora a melhor
        // LZ conjunta fosse alcancavel no mesmo turno.
        int effectiveRemainingRouteCost =
            requiredJointDeliveries >= 2
                ? int.MaxValue
                : maxRemainingRouteCost;

        bool EvaluateTacticalDisembark(
            int budget,
            out AIReachDecisionCandidate<MelhorDesembarqueResult> candidate)
        {
            candidate = null;
            if (!TryEvaluateBestDisembark(
                    transporter,
                    targets,
                    out MelhorDesembarqueResult immediateEvaluation,
                    routeHorizon: 120,
                    movementBudget: budget,
                    maxRemainingRouteCost:
                        effectiveRemainingRouteCost,
                    pathsByDestination: paths,
                    passengerPriorityByInstanceId:
                        BuildPassengerDeliveryPriority(
                            transporter, passengers))
                || immediateEvaluation.best == null)
                return false;

            MelhorDesembarqueLzScore immediateBest =
                immediateEvaluation.best;
            candidate =
                new AIReachDecisionCandidate<MelhorDesembarqueResult>
                {
                    Value = immediateEvaluation,
                    ActionCell = immediateBest.cell,
                    TargetCell =
                        immediateBest.spots.Count > 0
                            ? immediateBest.spots[0].target
                            : immediateBest.cell,
                    Score = immediateBest.score,
                    Reason =
                        $"{immediateBest.delivered}p/" +
                        $"{requiredJointDeliveries}p"
                };
            return true;
        }

        AIReachDecisionResult<MelhorDesembarqueResult>
            immediateReach = AIActionReachCoordinator.Evaluate(
                new AIReachDecisionRequest<MelhorDesembarqueResult>
                {
                    Context =
                        $"TransportDelivery:{transporter.InstanceId}",
                    Policy = AIReachDecisionPolicy.TacticalOnly,
                    CurrentMovementBudget = Mathf.Max(
                        0, transporter.RemainingMovementPoints),
                    EvaluateTactical = EvaluateTacticalDisembark,
                    DiagnosticLog = showAILogs
                        ? message => Debug.Log(message)
                        : null
                });
        MelhorDesembarqueResult evaluation =
            immediateReach.Found
                ? immediateReach.Decision.Value
                : null;
        if (evaluation?.best == null)
        {
            if ((usesFreeCargoFlow || requiredJointDeliveries >= 2)
                && TryBuildJointDisembarkProgressionAction(
                    transporter,
                    passengers,
                    snapshot,
                    targets,
                    paths,
                    effectiveRemainingRouteCost,
                    requiredJointDeliveries,
                    usesFreeCargoFlow,
                    consumer,
                    out action,
                    out _))
                return true;

            Debug.Log($"{TL("Transporte")} {consumer} " +
                      $"{transporter.InstanceId} MelhorDesembarque sem LZ: " +
                      $"alvos={targets.Count} rangeRota={maxRemainingRouteCost} " +
                      $"paths={(paths != null ? paths.Count : 0)}.");
            return false;
        }

        MelhorDesembarqueLzScore best = evaluation.best;
        if (best.delivered < requiredJointDeliveries)
        {
            string partialReleaseReason = string.Empty;
            bool releaseUsefulPartialNow =
                usesFreeCargoFlow
                && ShouldReleasePartialFreeCargoAtTacticalLz(
                    transporter,
                    passengers,
                    best,
                    operationalTurns: 2,
                    out partialReleaseReason);
            if (releaseUsefulPartialNow)
            {
                Debug.Log($"{TL("Transporte")} {consumer} " +
                          $"{transporter.InstanceId} libera desembarque parcial util: " +
                          $"pax={best.delivered}/{requiredJointDeliveries}; " +
                          $"{partialReleaseReason}.");
            }

            bool jointLzKnown = false;
            if (!releaseUsefulPartialNow
                && TryBuildJointDisembarkProgressionAction(
                    transporter,
                    passengers,
                    snapshot,
                    targets,
                    paths,
                    effectiveRemainingRouteCost,
                    requiredJointDeliveries,
                    usesFreeCargoFlow,
                    consumer,
                    out action,
                    out jointLzKnown))
                return true;

            if (!releaseUsefulPartialNow && jointLzKnown)
            {
                Debug.Log($"{TL("Transporte")} {consumer} " +
                          $"{transporter.InstanceId} rejeita desembarque parcial: " +
                          $"pax={best.delivered}/{requiredJointDeliveries}; " +
                          $"LZ conjunta conhecida ainda nao alcancada.");
                return false;
            }

            // A busca longa percorreu todo o envelope conhecido do
            // transportador e provou que esta geografia nao oferece spots
            // exclusivos para o grupo inteiro. Nao transforme uma ilha de um
            // unico hex em deadlock: entrega o passageiro prioritario e
            // conserva os demais a bordo.
            Debug.Log($"{TL("Transporte")} {consumer} " +
                      $"{transporter.InstanceId} libera desembarque parcial por " +
                      $"capacidade fisica: melhor global={best.delivered}p, " +
                      $"grupo={requiredJointDeliveries}p; sem LZ conjunta.");
        }

        var selected = new List<PodeDesembarcarOption>();
        for (int i = 0; i < best.spots.Count; i++)
            selected.Add(best.spots[i].option);
        if (selected.Count == 0
            || !selected.Exists(o => o.passengerUnit == primary))
            return false;

        Vector3Int fromCell = transporter.CurrentCellPosition;
        fromCell.z = 0;
        List<Vector3Int> movePath = null;
        paths?.TryGetValue(best.cell, out movePath);
        string spots = string.Join(
            ", ",
            best.spots.ConvertAll(
                s => $"#{s.option.passengerUnit.InstanceId}" +
                     $"->{s.option.disembarkCell}" +
                     $" alvo={s.target} R{s.routeCost}"));
        Debug.Log($"{TL("Transporte")} {consumer} {transporter.InstanceId} " +
                  $"MelhorDesembarque LZ={best.cell} pax={best.delivered} " +
                  $"score={best.displayScore} spots=[{spots}].");

        action = best.cell == fromCell
            ? BuildDesembarcarBatch(
                transporter, snapshot.AITeam, fromCell, selected)
            : BuildDesembarcarBatch(
                transporter, snapshot.AITeam, fromCell, selected,
                best.cell, movePath);
        return true;
    }

    private bool ShouldReleasePartialFreeCargoAtTacticalLz(
        UnitManager transporter,
        List<UnitManager> passengers,
        MelhorDesembarqueLzScore tacticalBest,
        int operationalTurns,
        out string reason)
    {
        reason = string.Empty;
        if (transporter == null
            || passengers == null
            || tacticalBest == null
            || tacticalBest.spots == null
            || tacticalBest.spots.Count == 0
            || tacticalBest.delivered >= passengers.Count)
            return false;

        var deliveredPassengerIds = new HashSet<int>();
        var deliveredTargetCells = new HashSet<Vector3Int>();
        for (int i = 0; i < tacticalBest.spots.Count; i++)
        {
            MelhorDesembarqueSpotScore spot = tacticalBest.spots[i];
            if (spot?.option?.passengerUnit == null)
                continue;
            deliveredPassengerIds.Add(spot.option.passengerUnit.InstanceId);
            Vector3Int target = spot.target;
            target.z = 0;
            deliveredTargetCells.Add(target);
        }

        List<PodeDesembarcarOption> options =
            SimulateDisembarkFromCell(transporter, tacticalBest.cell);
        int remainingFreeCargo = 0;
        for (int passengerIndex = 0; passengerIndex < passengers.Count; passengerIndex++)
        {
            UnitManager passenger = passengers[passengerIndex];
            if (passenger == null
                || deliveredPassengerIds.Contains(passenger.InstanceId))
                continue;

            remainingFreeCargo++;
            int operationalBudget =
                Mathf.Max(1, passenger.GetMovementRange())
                * Mathf.Max(1, operationalTurns);
            bool hasAlternativeBuilding = false;

            if (options != null)
            {
                for (int optionIndex = 0;
                     optionIndex < options.Count && !hasAlternativeBuilding;
                     optionIndex++)
                {
                    PodeDesembarcarOption option = options[optionIndex];
                    if (option?.passengerUnit != passenger)
                        continue;

                    Vector3Int disembarkCell = option.disembarkCell;
                    disembarkCell.z = 0;
                    Dictionary<Vector3Int, int> operationalReach =
                        UnitMovementPathRules.CalculateMovementCostMap(
                            boardTilemap,
                            passenger,
                            disembarkCell,
                            operationalBudget,
                            terrainDatabase);

                    foreach (ConstructionManager building in
                             ConstructionManager.AllActive)
                    {
                        if (building == null
                            || !IsRebelCapturable(passenger, building)
                            || HasBlockingSurfaceUnitAtCell(
                                building.CurrentCellPosition))
                            continue;

                        Vector3Int buildingCell =
                            building.CurrentCellPosition;
                        buildingCell.z = 0;
                        if (deliveredTargetCells.Contains(buildingCell)
                            || operationalReach == null
                            || !operationalReach.ContainsKey(buildingCell))
                            continue;

                        hasAlternativeBuilding = true;
                        break;
                    }
                }
            }

            if (hasAlternativeBuilding)
            {
                reason =
                    $"passageiro #{passenger.InstanceId} ainda possui outro " +
                    $"predio na progressao operacional da LZ {tacticalBest.cell}";
                return false;
            }
        }

        if (remainingFreeCargo <= 0)
            return false;

        reason =
            $"{remainingFreeCargo} passageiro(s) restante(s) sem outro predio " +
            $"alcancavel em {Mathf.Max(1, operationalTurns)} turnos a partir " +
            $"da LZ {tacticalBest.cell}";
        return true;
    }

    private void ApplyOperationalDisembarkCapacity(
        UnitManager transporter,
        List<UnitManager> passengers,
        AIWorldSnapshot snapshot,
        Dictionary<int, Vector3Int> targets)
    {
        if (transporter == null
            || passengers == null
            || targets == null
            || targets.Count <= 1)
            return;

        List<UnitManager> ordered =
            OrderPassengersForDelivery(transporter, passengers);
        var claimedSafeTargets = new HashSet<Vector3Int>();
        var removePassengerIds = new List<int>();

        for (int i = 0; i < ordered.Count; i++)
        {
            UnitManager passenger = ordered[i];
            if (passenger == null
                || !targets.TryGetValue(
                    passenger.InstanceId, out Vector3Int target))
                continue;

            target.z = 0;
            if (claimedSafeTargets.Add(target))
                continue;

            // Um unico objetivo seguro justifica somente o passageiro mais
            // antigo. Sob pressao confirmada, a segunda unidade tem funcao
            // operacional de reforco e conserva sua intencao.
            if (IsCaptureTargetUnderConfirmedPressure(target, snapshot))
            {
                Debug.Log($"{TL("Transporte")} capacidade operacional: " +
                          $"objetivo quente {target} aceita reforco " +
                          $"#{passenger.InstanceId}.");
                continue;
            }

            removePassengerIds.Add(passenger.InstanceId);
            Debug.Log($"{TL("Transporte")} capacidade operacional: " +
                      $"passageiro #{passenger.InstanceId} permanece a bordo; " +
                      $"objetivo seguro {target} ja reservado pelo passageiro " +
                      $"prioritario.");
        }

        for (int i = 0; i < removePassengerIds.Count; i++)
            targets.Remove(removePassengerIds[i]);
    }

    private bool TryBuildJointDisembarkProgressionAction(
        UnitManager transporter,
        List<UnitManager> passengers,
        AIWorldSnapshot snapshot,
        IReadOnlyDictionary<int, Vector3Int> targets,
        Dictionary<Vector3Int, List<Vector3Int>> currentPaths,
        int maxRemainingRouteCost,
        int requiredJointDeliveries,
        bool usesFreeCargoFlow,
        string consumer,
        out PlayerAction action,
        out bool jointLzKnown)
    {
        action = null;
        jointLzKnown = false;
        if (transporter == null
            || passengers == null
            || snapshot == null
            || targets == null
            || currentPaths == null
            || requiredJointDeliveries < 1)
            return false;

        bool EvaluateDisembarkTier(
            int budget,
            out AIReachDecisionCandidate<MelhorDesembarqueResult> candidate)
        {
            candidate = null;
            Dictionary<Vector3Int, List<Vector3Int>> tierPaths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap,
                    transporter,
                    Mathf.Max(0, budget),
                    terrainDatabase);
            if (tierPaths == null
                || tierPaths.Count == 0
                || !TryEvaluateBestDisembark(
                    transporter,
                    targets,
                    out MelhorDesembarqueResult tierEvaluation,
                    routeHorizon: Mathf.Max(1, budget),
                    movementBudget: budget,
                    maxRemainingRouteCost: maxRemainingRouteCost,
                    pathsByDestination: tierPaths,
                    passengerPriorityByInstanceId:
                        BuildPassengerDeliveryPriority(
                            transporter, passengers))
                || tierEvaluation.best == null
                || tierEvaluation.best.delivered
                    < requiredJointDeliveries)
                return false;

            MelhorDesembarqueLzScore tierBest =
                tierEvaluation.best;
            Vector3Int targetCell =
                tierBest.spots.Count > 0
                    ? tierBest.spots[0].target
                    : tierBest.cell;
            candidate =
                new AIReachDecisionCandidate<MelhorDesembarqueResult>
                {
                    Value = tierEvaluation,
                    ActionCell = tierBest.cell,
                    TargetCell = targetCell,
                    Score = tierBest.score,
                    Reason =
                        $"{tierBest.delivered}p/" +
                        $"{requiredJointDeliveries}p"
                };
            return true;
        }

        bool EvaluateStrategicAnchor(
            int _,
            out AIReachDecisionCandidate<MelhorDesembarqueResult> candidate)
        {
            candidate = null;
            Vector3Int strategicFromCell =
                transporter.CurrentCellPosition;
            strategicFromCell.z = 0;
            Vector3Int bestTarget = Vector3Int.zero;
            int bestDistance = int.MaxValue;

            // Carga planejada navega em funcao do passageiro prioritario, nao
            // do predio geometricamente mais perto entre todos os passageiros.
            // Rogue/rebelde nao possui eixo formal e cai no ranking cubico
            // abaixo.
            UnitManager strategicPrimary =
                ResolvePrimaryPassenger(
                    transporter, passengers, plan: null);
            if (!usesFreeCargoFlow
                && strategicPrimary != null
                && targets.TryGetValue(
                    strategicPrimary.InstanceId,
                    out Vector3Int plannedTarget))
            {
                plannedTarget.z = 0;
                bestTarget = plannedTarget;
                bestDistance =
                    AIActionReachCoordinator.CubicDistance(
                        strategicFromCell, plannedTarget);
            }

            foreach (KeyValuePair<int, Vector3Int> pair in targets)
            {
                if (bestDistance < int.MaxValue
                    && !usesFreeCargoFlow)
                    break;
                Vector3Int target = pair.Value;
                target.z = 0;
                int distance = AIActionReachCoordinator.CubicDistance(
                    strategicFromCell, target);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                bestTarget = target;
            }

            if (bestDistance == int.MaxValue)
                return false;

            // Value nulo identifica uma ancora, nao uma LZ pronta. O
            // controlador do dominio traduz o alvo em praia, heliponto ou
            // celula terrestre valida e usa Caminhos Validos para progredir.
            candidate =
                new AIReachDecisionCandidate<MelhorDesembarqueResult>
                {
                    Value = null,
                    ActionCell = bestTarget,
                    TargetCell = bestTarget,
                    Score = -bestDistance,
                    Reason = $"cubic={bestDistance}"
                };
            return true;
        }

        AIReachDecisionPolicy reachPolicy =
            new AIReachDecisionPolicy(
                AIReachDecisionStages.Operational
                | AIReachDecisionStages.Strategic,
                operationalTurns: 2);
        var reachRequest =
            new AIReachDecisionRequest<MelhorDesembarqueResult>
            {
                Context =
                    $"TransportDelivery:{transporter.InstanceId}",
                Policy = reachPolicy,
                CurrentMovementBudget = Mathf.Max(
                    1, transporter.RemainingMovementPoints),
                StrategicSearchBudget = 120,
                // O reach tatico ja foi consultado pelo chamador com os
                // caminhos reais da rodada. Este segundo passe recebe o
                // controle somente depois do miss ou de uma entrega parcial.
                EvaluateOperational = EvaluateDisembarkTier,
                EvaluateStrategic = EvaluateStrategicAnchor,
                DiagnosticLog = showAILogs
                    ? message => Debug.Log(message)
                    : null
            };
        AIReachDecisionResult<MelhorDesembarqueResult>
            reachDecision =
                AIActionReachCoordinator.Evaluate(reachRequest);
        if (!reachDecision.Found)
        {
            if (usesFreeCargoFlow)
                Debug.Log($"{TL("Transporte")} {consumer} " +
                          $"{transporter.InstanceId} fluxo livre: " +
                          $"sem LZ na progressao de " +
                          $"{reachPolicy.OperationalTurns} turnos; " +
                          $"usa ancora costeira.");
            return false;
        }

        if (reachDecision.Tier == AIReachDecisionTier.Strategic
            && reachDecision.Decision.Value == null)
        {
            Vector3Int strategicBranchFromCell =
                transporter.CurrentCellPosition;
            strategicBranchFromCell.z = 0;
            Vector3Int strategicTarget =
                reachDecision.Decision.TargetCell;
            strategicTarget.z = 0;
            Vector3Int strategicAnchor =
                transporter.GetDomain() == Domain.Naval
                    ? ResolveNavalApproachTarget(
                        transporter,
                        strategicTarget,
                        strategicBranchFromCell)
                    : strategicTarget;
            strategicAnchor.z = 0;
            HashSet<Vector3Int> strategicOccupied =
                BuildOccupied(transporter);

            if (strategicAnchor != strategicBranchFromCell
                && TryFindBestToolProgressionCell(
                    transporter,
                    snapshot,
                    strategicBranchFromCell,
                    strategicAnchor,
                    currentPaths,
                    strategicOccupied,
                    ToolProgressionIntent.TransportDelivery,
                    out Vector3Int strategicProgressionCell,
                    out _,
                    out string strategicProgressionReason))
            {
                Debug.Log($"{TL("Transporte")} {consumer} " +
                          $"{transporter.InstanceId} reach estrategico: " +
                          $"alvo cubico {strategicTarget} -> " +
                          $"ancora {strategicAnchor}; progride " +
                          $"{strategicBranchFromCell}->" +
                          $"{strategicProgressionCell} " +
                          $"({strategicProgressionReason}).");
                action = BuildMoveBatch(
                    transporter,
                    snapshot.AITeam,
                    strategicBranchFromCell,
                    strategicProgressionCell,
                    currentPaths);
                return true;
            }

            Debug.Log($"{TL("Transporte")} {consumer} " +
                      $"{transporter.InstanceId} reach estrategico: " +
                      $"ancora cubica " +
                      $"{strategicTarget} " +
                      $"({reachDecision.Decision.Reason}); " +
                      $"sem progressao valida.");
            return false;
        }

        MelhorDesembarqueResult longEvaluation =
            reachDecision.Decision.Value;
        jointLzKnown = true;
        Vector3Int fromCell = transporter.CurrentCellPosition;
        fromCell.z = 0;
        Vector3Int jointLz = longEvaluation.best.cell;
        jointLz.z = 0;
        if (jointLz == fromCell)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(transporter);
        if (!TryFindBestToolProgressionCell(
                transporter,
                snapshot,
                fromCell,
                jointLz,
                currentPaths,
                occupied,
                ToolProgressionIntent.TransportDelivery,
                out Vector3Int progressionCell,
                out _,
                out string progressionReason))
            return false;

        Debug.Log($"{TL("Transporte")} {consumer} " +
                  $"{transporter.InstanceId} preserva grupo " +
                  $"{requiredJointDeliveries}p e progride {fromCell}" +
                  $"->{progressionCell} rumo a LZ conjunta {jointLz} " +
                  $"score={longEvaluation.best.displayScore} " +
                  $"tier={reachDecision.Tier} " +
                  $"({progressionReason}).");
        action = BuildMoveBatch(
            transporter,
            snapshot.AITeam,
            fromCell,
            progressionCell,
            currentPaths);
        return true;
    }

    private bool UsesFreeCargoDisembarkFlow(
        List<UnitManager> passengers,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        IReadOnlyDictionary<int, Vector3Int> targets)
    {
        if (passengers == null
            || targets == null
            || targets.Count == 0)
            return false;

        if (IsRuntimeRebelSnapshot(snapshot))
            return true;

        bool hasTargetedPassenger = false;
        for (int i = 0; i < passengers.Count; i++)
        {
            UnitManager passenger = passengers[i];
            if (passenger == null
                || !targets.ContainsKey(passenger.InstanceId))
                continue;

            hasTargetedPassenger = true;
            if (!IsRogueCapturerPassenger(passenger, plan))
                return false;
        }

        return hasTargetedPassenger;
    }

    private int ResolveRequiredJointDeliveries(
        UnitManager transporter,
        List<UnitManager> passengers,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        IReadOnlyDictionary<int, Vector3Int> targets)
    {
        if (transporter == null
            || passengers == null
            || targets == null
            || targets.Count < 2)
            return 1;

        if (IsRuntimeRebelSnapshot(snapshot))
            return Mathf.Min(passengers.Count, targets.Count);

        int rogueTargets = 0;
        for (int i = 0; i < passengers.Count; i++)
        {
            UnitManager passenger = passengers[i];
            if (passenger != null
                && targets.ContainsKey(passenger.InstanceId)
                && IsRogueCapturerPassenger(passenger, plan))
                rogueTargets++;
        }

        return rogueTargets >= 2 ? rogueTargets : 1;
    }

    private bool ShouldRejectPartialRebelOrRogueSelection(
        UnitManager transporter,
        List<PodeDesembarcarOption> selected,
        List<UnitManager> passengers,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int assignedSectorTarget,
        string consumer)
    {
        if (transporter == null
            || selected == null
            || passengers == null
            || !TryBuildCourierDisembarkTargets(
                transporter,
                passengers,
                plan,
                snapshot,
                assignedSectorTarget,
                out Dictionary<int, Vector3Int> targets))
            return false;

        ApplyOperationalDisembarkCapacity(
            transporter,
            passengers,
            snapshot,
            targets);
        int required = ResolveRequiredJointDeliveries(
            transporter, passengers, plan, snapshot, targets);
        if (selected.Count >= required)
            return false;

        Debug.Log($"{TL("Transporte")} {consumer} " +
                  $"{transporter.InstanceId} rejeita fallback parcial: " +
                  $"pax={selected.Count}/{required}; aguarda LZ conjunta.");
        return true;
    }

    private bool IsConfirmedVisibleCellForAI(Vector3Int cell)
    {
        cell.z = 0;
        return matchController != null
            && matchController.IsCellVisibleForActiveTeam(cell);
    }

    private bool TryBuildCourierDisembarkTargets(
        UnitManager transporter,
        List<UnitManager> passengers,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int assignedSectorTarget,
        out Dictionary<int, Vector3Int> targets)
    {
        targets = new Dictionary<int, Vector3Int>();
        if (transporter == null || passengers == null || snapshot == null)
            return false;

        if (!IsRuntimeRebelSnapshot(snapshot))
        {
            var claimedTargetCells = new HashSet<Vector3Int>();
            List<UnitManager> orderedPassengers =
                OrderPassengersForDelivery(transporter, passengers);
            for (int i = 0; i < orderedPassengers.Count; i++)
            {
                UnitManager passenger = orderedPassengers[i];
                if (passenger == null)
                    continue;

                // Rogue dentro de uma IA com HQ continua sem eixo/slot formal.
                // Para desembarque ele é irmão da IA rebelde: escolhe o
                // capturável livre mais próximo e não herda o setor/HQ do
                // transportador. O HashSet compartilhado distribui dois
                // rogues entre dois prédios distintos.
                if (IsRogueCapturerPassenger(passenger, plan))
                {
                    if (TryResolveRogueCorridorCaptureTarget(
                            passenger,
                            snapshot,
                            transporter.CurrentCellPosition,
                            claimedTargetCells,
                            out Vector3Int rogueTarget))
                    {
                        claimedTargetCells.Add(rogueTarget);
                        targets[passenger.InstanceId] = rogueTarget;
                        Debug.Log($"{TL("Transporte")} alvo conjunto rogue: " +
                                  $"passageiro #{passenger.InstanceId} -> " +
                                  $"{rogueTarget}.");
                    }
                    else if (TryResolveSharedHotCaptureTarget(
                            passenger,
                            snapshot,
                            claimedTargetCells,
                            out Vector3Int supportTarget))
                    {
                        targets[passenger.InstanceId] = supportTarget;
                        Debug.Log($"{TL("Transporte")} alvo conjunto rogue: " +
                                  $"passageiro #{passenger.InstanceId} reforca " +
                                  $"objetivo quente {supportTarget}; sem segundo " +
                                  $"capturavel util.");
                    }
                    else
                    {
                        Debug.Log($"{TL("Transporte")} alvo conjunto rogue: " +
                                  $"passageiro #{passenger.InstanceId} permanece " +
                                  $"embarcado; sem capturavel livre distinto ou " +
                                  $"objetivo sob pressao.");
                    }
                    continue;
                }

                if (TryResolveCourierPassengerTarget(
                        passenger,
                        plan,
                        snapshot,
                        assignedSectorTarget,
                        transporter.CurrentCellPosition,
                        out Vector3Int target))
                {
                    target.z = 0;
                    if (claimedTargetCells.Contains(target)
                        && IsCapturerPassenger(passenger))
                    {
                        if (!TryResolveDistinctPlanCaptureTarget(
                                passenger,
                                plan,
                                snapshot,
                                transporter.CurrentCellPosition,
                                claimedTargetCells,
                                out target))
                        {
                            Debug.Log($"{TL("Transporte")} plano conjunto: " +
                                      $"passageiro #{passenger.InstanceId} permanece " +
                                      $"sem segundo predio distinto.");
                            continue;
                        }
                    }

                    claimedTargetCells.Add(target);
                    targets[passenger.InstanceId] = target;
                    Debug.Log($"{TL("Transporte")} alvo conjunto plano: " +
                              $"passageiro #{passenger.InstanceId} -> {target}.");
                }
            }
            return targets.Count > 0;
        }

        List<UnitManager> ordered =
            OrderPassengersForDelivery(transporter, passengers);

        var claimed = new HashSet<ConstructionManager>();
        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;
        for (int i = 0; i < ordered.Count; i++)
        {
            UnitManager passenger = ordered[i];
            ConstructionManager best = null;
            float bestDistance = float.MaxValue;
            foreach (ConstructionManager building in ConstructionManager.AllActive)
            {
                if (building == null
                    || claimed.Contains(building)
                    || !IsRebelCapturable(passenger, building)
                    || HasBlockingSurfaceUnitAtCell(building.CurrentCellPosition))
                    continue;

                Vector3Int target = building.CurrentCellPosition;
                target.z = 0;
                // O passageiro esta embarcado: sua CurrentCellPosition pode ser
                // a praia/ilha anterior e nao serve como origem de caminhada.
                // Aqui apenas distribuimos INTENCOES distintas por proximidade
                // ao courier. A viabilidade real e o custo terrestre sao
                // comprovados depois, de cada spot, pelo servico de desembarque.
                float distance =
                    AIActionReachCoordinator.CubicDistance(
                        transporterCell, target);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = building;
            }

            if (best == null)
            {
                var claimedCells = new HashSet<Vector3Int>();
                foreach (ConstructionManager claimedBuilding in claimed)
                {
                    if (claimedBuilding == null)
                        continue;
                    Vector3Int claimedCell =
                        claimedBuilding.CurrentCellPosition;
                    claimedCell.z = 0;
                    claimedCells.Add(claimedCell);
                }

                if (TryResolveSharedHotCaptureTarget(
                        passenger,
                        snapshot,
                        claimedCells,
                        out Vector3Int supportTarget))
                {
                    targets[passenger.InstanceId] = supportTarget;
                    Debug.Log($"{TL("Transporte")} alvo conjunto rebelde: " +
                              $"passageiro #{passenger.InstanceId} reforca " +
                              $"objetivo quente {supportTarget}; sem segundo " +
                              $"capturavel util.");
                }
                continue;
            }
            claimed.Add(best);
            Vector3Int bestTarget = best.CurrentCellPosition;
            bestTarget.z = 0;
            targets[passenger.InstanceId] = bestTarget;
            Debug.Log($"{TL("Transporte")} alvo conjunto rebelde: " +
                      $"passageiro #{passenger.InstanceId} -> " +
                      $"{best.InstanceId}@{bestTarget} " +
                      $"distTransportador={bestDistance:F0}h.");
        }

        return targets.Count > 0;
    }

    private bool TryResolveSharedHotCaptureTarget(
        UnitManager passenger,
        AIWorldSnapshot snapshot,
        HashSet<Vector3Int> claimedTargetCells,
        out Vector3Int target)
    {
        target = Vector3Int.zero;
        if (passenger == null
            || snapshot == null
            || claimedTargetCells == null
            || claimedTargetCells.Count == 0)
            return false;

        float bestDistance = float.MaxValue;
        foreach (Vector3Int claimedCellValue in claimedTargetCells)
        {
            Vector3Int claimedCell = claimedCellValue;
            claimedCell.z = 0;
            ConstructionManager building = null;
            foreach (ConstructionManager candidate in
                     ConstructionManager.AllActive)
            {
                if (candidate == null)
                    continue;
                Vector3Int candidateCell = candidate.CurrentCellPosition;
                candidateCell.z = 0;
                if (candidateCell == claimedCell)
                {
                    building = candidate;
                    break;
                }
            }

            if (building == null
                || !IsRebelCapturable(passenger, building)
                || !IsCaptureTargetUnderConfirmedPressure(
                    claimedCell, snapshot))
                continue;

            float distance = AIActionReachCoordinator.CubicDistance(
                passenger.CurrentCellPosition, claimedCell);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            target = claimedCell;
        }

        return bestDistance < float.MaxValue;
    }

    private static bool IsCaptureTargetUnderConfirmedPressure(
        Vector3Int target,
        AIWorldSnapshot snapshot)
    {
        if (snapshot?.EnemyUnits == null)
            return false;

        target.z = 0;
        for (int i = 0; i < snapshot.EnemyUnits.Count; i++)
        {
            UnitManager enemy = snapshot.EnemyUnits[i];
            if (enemy == null || enemy.CurrentHP <= 0)
                continue;
            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (AIActionReachCoordinator.CubicDistance(
                    target, enemyCell) <= 2)
                return true;
        }

        return false;
    }

    private bool TryResolveRogueCorridorCaptureTarget(
        UnitManager passenger,
        AIWorldSnapshot snapshot,
        Vector3Int transporterCell,
        HashSet<Vector3Int> claimedTargetCells,
        out Vector3Int target)
    {
        target = Vector3Int.zero;
        if (passenger == null || snapshot == null)
            return false;

        transporterCell.z = 0;
        Vector3Int hqCell = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : Vector3Int.zero;
        hqCell.z = 0;
        bool hasHq = snapshot.EnemyHQ != null;
        float currentHqDistance = hasHq
            ? AIActionReachCoordinator.CubicDistance(
                transporterCell, hqCell)
            : 0f;

        ConstructionManager best = null;
        float bestScore = float.MinValue;
        foreach (ConstructionManager building in ConstructionManager.AllActive)
        {
            if (building == null
                || !building.IsCapturable
                || building.CapturePointsMax <= 0
                || !IsRebelCapturable(passenger, building)
                || HasBlockingSurfaceUnitAtCell(building.CurrentCellPosition))
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            if (claimedTargetCells != null && claimedTargetCells.Contains(cell))
                continue;

            float transportDistance =
                AIActionReachCoordinator.CubicDistance(
                    transporterCell, cell);
            float targetHqDistance = hasHq
                ? AIActionReachCoordinator.CubicDistance(cell, hqCell)
                : 0f;
            float progress = hasHq
                ? currentHqDistance - targetHqDistance
                : 0f;
            float corridorDetour = hasHq
                ? transportDistance + targetHqDistance - currentHqDistance
                : 0f;

            if (hasHq
                && corridorDetour > TransportDropOffRange + 0.5f)
                continue;

            // O rogue a pe captura qualquer predio util alcançavel antes de
            // continuar ao HQ. Embarcado, a equivalencia e um alvo local que
            // caiba no envelope de entrega do transportador. Ele pode exigir
            // um pequeno passo lateral/para tras, desde que continue dentro
            // do desvio maximo do corredor.
            bool localOpportunity =
                transportDistance <= TransportDropOffRange + 0.5f;
            float score =
                -transportDistance * 120f
                - corridorDetour * 300f
                + progress * 35f
                + (localOpportunity ? 2000f : 0f)
                - building.InstanceId * 0.001f;
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = building;
            target = cell;
        }

        return best != null;
    }

    private static List<UnitManager> OrderPassengersForDelivery(
        UnitManager transporter,
        List<UnitManager> passengers)
    {
        var ordered = new List<UnitManager>(passengers);
        ordered.Sort((a, b) =>
        {
            int turnA = transporter.GetPassengerEmbarkedOnTurn(a);
            int turnB = transporter.GetPassengerEmbarkedOnTurn(b);
            int safeA = turnA >= 0 ? turnA : int.MaxValue;
            int safeB = turnB >= 0 ? turnB : int.MaxValue;
            int byTurn = safeA.CompareTo(safeB);
            if (byTurn != 0) return byTurn;
            return passengers.IndexOf(a).CompareTo(passengers.IndexOf(b));
        });
        return ordered;
    }

    private static Dictionary<int, int> BuildPassengerDeliveryPriority(
        UnitManager transporter,
        List<UnitManager> passengers)
    {
        List<UnitManager> ordered =
            OrderPassengersForDelivery(transporter, passengers);
        var priorities = new Dictionary<int, int>();
        for (int i = 0; i < ordered.Count; i++)
        {
            UnitManager passenger = ordered[i];
            if (passenger != null)
                priorities[passenger.InstanceId] = i;
        }
        return priorities;
    }

    private static bool IsCapturerPassenger(UnitManager passenger)
    {
        return passenger != null
            && passenger.TryGetUnitData(out UnitData data)
            && data != null
            && data.roles != null
            && data.roles.Contains(UnitRole.Capturador);
    }

    private bool TryResolveDistinctPlanCaptureTarget(
        UnitManager passenger,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int transporterCell,
        HashSet<Vector3Int> claimedTargetCells,
        out Vector3Int target)
    {
        target = Vector3Int.zero;
        if (passenger == null || snapshot == null)
            return false;

        ConstructionSector assignedSector = ConstructionSector.None;
        if (plan?.Objectives != null)
        {
            for (int i = 0; i < plan.Objectives.Count; i++)
            {
                SectorObjective objective = plan.Objectives[i];
                if (objective?.Slots == null)
                    continue;
                bool assigned = objective.Slots.Exists(
                    slot => slot != null
                        && slot.Filled
                        && slot.AssignedUnitId == passenger.InstanceId);
                if (assigned)
                {
                    assignedSector = objective.Sector;
                    break;
                }
            }
        }

        transporterCell.z = 0;
        ConstructionManager best = null;
        float bestDistance = float.MaxValue;
        foreach (ConstructionManager building in ConstructionManager.AllActive)
        {
            if (building == null
                || !building.IsCapturable
                || building.CapturePointsMax <= 0
                || building.SlotIndex == snapshot.AISlotIndex
                || HasBlockingSurfaceUnitAtCell(building.CurrentCellPosition))
                continue;
            if (assignedSector != ConstructionSector.None
                && building.Sector != assignedSector)
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            if (claimedTargetCells.Contains(cell))
                continue;
            float distance =
                AIActionReachCoordinator.CubicDistance(
                    transporterCell, cell);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = building;
            target = cell;
        }

        return best != null;
    }
}
