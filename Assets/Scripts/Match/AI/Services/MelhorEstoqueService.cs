using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum MelhorEstoqueIntent
{
    Auto = 0,
    ReplenishSelf = 1,
    SupplyReceiver = 2,
    RebalanceHub = 3,
    SupplyConstruction = 4,
    CollectFromConstruction = 5,
    EmbarkedDistribution = 6
}

public sealed class MelhorEstoqueRequest
{
    public UnitManager unit;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public MelhorEstoqueIntent intent = MelhorEstoqueIntent.Auto;
    public int tacticalBudget;
    public int operationalTurns = 2;
    public bool includeStrategic;
    public Dictionary<Vector3Int, List<Vector3Int>> tacticalPaths;
    public Dictionary<Vector3Int, int> operationalCosts;
    public bool emulateStockFromUnitData;
    public float maxThreat = float.PositiveInfinity;
    public Func<Vector3Int, float> evaluateThreat;
    // Filtro de politica do controller. A consulta continua calculando e
    // validando a opcao pelo PodeTransferir; quem a consome pode, por exemplo,
    // pedir apenas destinos criticos antes de preemptar transporte.
    public Func<MelhorEstoqueOption, bool> includeOption;
    public Action<string> diagnosticLog;
}

public sealed class MelhorEstoqueOption
{
    public PodeTransferirOption prospectiveTransfer;
    public MelhorEstoqueIntent intent;
    public AIReachDecisionTier tier;
    public UnitManager sourceUnit;
    public ConstructionManager sourceConstruction;
    public UnitManager destinationUnit;
    public ConstructionManager destinationConstruction;
    public Vector3Int actionCell;
    public Vector3Int endpointCell;
    public int routeCost = -1;
    public int cubicDistance;
    public int estimatedAmount;
    public StockNeedAssessment sourceStockNeed;
    public StockNeedAssessment stockNeed;
    public ConstructionStockNeedAssessment constructionStockNeed;
    public readonly List<StockTransferEstimate> compatibleSupplies =
        new List<StockTransferEstimate>();
    public float threat;
    public float score;
    public string reason;
}

public sealed class MelhorEstoqueReject
{
    public Vector3Int actionCell;
    public string reason;
}

public sealed class MelhorEstoqueResult
{
    public StockNeedAssessment actorNeed;
    public readonly List<MelhorEstoqueOption> ranking =
        new List<MelhorEstoqueOption>();
    public readonly List<MelhorEstoqueReject> rejected =
        new List<MelhorEstoqueReject>();
    public AIReachDecisionResult<MelhorEstoqueOption> reachDecision;

    public MelhorEstoqueOption best =>
        reachDecision != null && reachDecision.Found
            ? reachDecision.Decision.Value
            : ranking.Count > 0 ? ranking[0] : null;

    public string BuildRejectedSummary(int maxReasons = 4)
    {
        if (rejected.Count <= 0)
            return "nenhum motivo registrado";

        var counts = new Dictionary<string, int>();
        for (int i = 0; i < rejected.Count; i++)
        {
            string reason = rejected[i]?.reason;
            if (string.IsNullOrWhiteSpace(reason))
                reason = "motivo vazio";
            counts.TryGetValue(reason, out int count);
            counts[reason] = count + 1;
        }

        var ordered =
            new List<KeyValuePair<string, int>>(counts);
        ordered.Sort((a, b) =>
        {
            int byCount = b.Value.CompareTo(a.Value);
            return byCount != 0
                ? byCount
                : string.CompareOrdinal(a.Key, b.Key);
        });

        int limit = Mathf.Min(
            Mathf.Max(1, maxReasons),
            ordered.Count);
        var parts = new List<string>(limit);
        for (int i = 0; i < limit; i++)
        {
            parts.Add(
                $"{ordered[i].Value}x {ordered[i].Key}");
        }
        return string.Join(" | ", parts);
    }
}

/// <summary>
/// Consulta pura de planejamento da rede de estoque. A onda nasce na unidade
/// que tomara a decisao, classifica encontros em Tactical, Operational e
/// Strategic e usa a consulta prospectiva do PodeTransferir para validar cada
/// rendezvous. Nao transfere, move ou altera qualquer estado confirmado.
/// </summary>
public static class MelhorEstoqueService
{
    public static MelhorEstoqueResult Evaluate(
        MelhorEstoqueRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.unit,
            "melhorEstoque");
        AIDecisionPerf.AddCount("MelhorEstoqueCalls");
        var result = new MelhorEstoqueResult();
        if (!TryValidateRequest(request, result, out UnitData data))
            return result;

        UnitManager actor = request.unit;
        Vector3Int origin = Normalize(actor.CurrentCellPosition);
        int tacticalBudget = Mathf.Max(0, request.tacticalBudget);
        int operationalBudget = Mathf.Max(
            tacticalBudget,
            actor.MaxMovementPoints
                * Mathf.Max(1, request.operationalTurns));

        Dictionary<Vector3Int, List<Vector3Int>> tacticalPaths =
            request.tacticalPaths
            ?? UnitMovementPathRules.CalcularCaminhosValidos(
                request.map,
                actor,
                tacticalBudget,
                request.terrainDatabase);
        if (request.tacticalPaths != null)
            AIDecisionPerf.AddCount("MelhorEstoqueTacticalReachReuses");
        if (!tacticalPaths.ContainsKey(origin))
        {
            if (ReferenceEquals(
                    tacticalPaths, request.tacticalPaths))
            {
                tacticalPaths =
                    ClonePaths(tacticalPaths);
            }
            tacticalPaths[origin] = new List<Vector3Int>();
        }
        Dictionary<Vector3Int, int> routeCosts =
            request.operationalCosts
            ?? UnitMovementPathRules.CalculateMovementCostMap(
                request.map,
                actor,
                origin,
                operationalBudget,
                request.terrainDatabase);
        if (request.operationalCosts != null)
            AIDecisionPerf.AddCount("MelhorEstoqueOperationalReachReuses");
        if (!routeCosts.ContainsKey(origin))
        {
            if (ReferenceEquals(
                    routeCosts, request.operationalCosts))
            {
                routeCosts =
                    new Dictionary<Vector3Int, int>(
                        routeCosts);
            }
            routeCosts[origin] = 0;
        }

        HashSet<Vector3Int> actionCells =
            CollectProspectiveActionCells(request, data, origin);
        var options = new List<PodeTransferirOption>();
        var invalids = new List<PodeTransferirInvalidOption>();
        foreach (Vector3Int rawActionCell in actionCells)
        {
            Vector3Int actionCell = Normalize(rawActionCell);
            AIReachDecisionTier tier = ResolveTier(
                actionCell,
                tacticalPaths,
                routeCosts,
                request.includeStrategic);
            if (tier == AIReachDecisionTier.None)
                continue;
            if (!CanActorStopAt(request, origin, actionCell))
            {
                result.rejected.Add(new MelhorEstoqueReject
                {
                    actionCell = actionCell,
                    reason = "Ocupacao impede terminar o movimento."
                });
                continue;
            }

            options.Clear();
            invalids.Clear();
            bool valid = PodeTransferirSensor.CollectOptionsFromCell(
                actor,
                request.map,
                request.terrainDatabase,
                actionCell == origin
                    ? SensorMovementMode.MoveuParado
                    : SensorMovementMode.MoveuAndando,
                actionCell,
                options,
                out string sensorReason,
                invalids);
            if (!valid)
            {
                string rejectionReason = sensorReason;
                if (invalids.Count > 0
                    && !string.IsNullOrWhiteSpace(
                        invalids[0]?.reason))
                {
                    rejectionReason = invalids[0].reason;
                }
                result.rejected.Add(new MelhorEstoqueReject
                {
                    actionCell = actionCell,
                    reason = rejectionReason
                });
                continue;
            }

            for (int i = 0; i < options.Count; i++)
            {
                PodeTransferirOption transfer = options[i];
                if (transfer == null)
                    continue;
                MelhorEstoqueIntent resolvedIntent =
                    ResolveIntent(
                        result.actorNeed,
                        data,
                        transfer);
                if (!IntentMatches(request.intent, resolvedIntent))
                {
                    result.rejected.Add(new MelhorEstoqueReject
                    {
                        actionCell = actionCell,
                        reason =
                            $"Intent {resolvedIntent} nao atende " +
                            $"{request.intent}."
                    });
                    continue;
                }

                MelhorEstoqueOption candidate = BuildOption(
                    request,
                    transfer,
                    resolvedIntent,
                    tier,
                    origin,
                    actionCell,
                    routeCosts,
                    out string buildRejectionReason);
                if (candidate == null)
                {
                    result.rejected.Add(new MelhorEstoqueReject
                    {
                        actionCell = actionCell,
                        reason = buildRejectionReason
                    });
                    continue;
                }
                if (candidate.threat > request.maxThreat)
                {
                    result.rejected.Add(new MelhorEstoqueReject
                    {
                        actionCell = actionCell,
                        reason =
                            $"Ameaca {candidate.threat:0.0} excede " +
                            $"o limite {request.maxThreat:0.0}."
                    });
                    continue;
                }
                if (request.includeOption != null
                    && !request.includeOption(candidate))
                {
                    result.rejected.Add(new MelhorEstoqueReject
                    {
                        actionCell = actionCell,
                        reason = "Opcao fora da politica de prioridade atual."
                    });
                    continue;
                }
                result.ranking.Add(candidate);
            }
        }

        RemoveDuplicateOptions(result.ranking);
        result.ranking.Sort(Compare);
        result.reachDecision = ResolveByReachCoordinator(
            request,
            result.ranking);
        return result;
    }

    private static bool TryValidateRequest(
        MelhorEstoqueRequest request,
        MelhorEstoqueResult result,
        out UnitData data)
    {
        data = null;
        if (request?.unit == null
            || request.map == null
            || request.terrainDatabase == null
            || !request.unit.TryGetUnitData(out data)
            || data == null
            || !data.isSupplier
            || !HasTransferService(request.unit))
            return false;

        result.actorNeed = StockNeedAssessmentService.Evaluate(
            request.unit,
            request.emulateStockFromUnitData);
        return true;
    }

    private static HashSet<Vector3Int> CollectProspectiveActionCells(
        MelhorEstoqueRequest request,
        UnitData actorData,
        Vector3Int origin)
    {
        var result = new HashSet<Vector3Int> { origin };
        bool adjacent =
            actorData.collectionRange ==
                SupplierRangeMode.Adjacent1Hex
            || actorData.collectionRange ==
                SupplierRangeMode.Hybrid0Or1Hex;
        bool sameCell =
            actorData.collectionRange !=
                SupplierRangeMode.Adjacent1Hex;
        var neighbors = new List<Vector3Int>(6);

        IReadOnlyList<UnitManager> supplierUnits =
            ResolveSupplierUnits(request.map);
        for (int i = 0; i < supplierUnits.Count; i++)
        {
            UnitManager target = supplierUnits[i];
            if (!IsEligibleUnitEndpoint(request.unit, target))
                continue;
            target.TryGetUnitData(out UnitData targetData);
            bool unitSameCell =
                RangeIncludesSameCell(actorData.collectionRange)
                || (targetData != null
                    && RangeIncludesSameCell(
                        targetData.collectionRange));
            bool unitAdjacent =
                RangeIncludesAdjacent(actorData.collectionRange)
                || (targetData != null
                    && RangeIncludesAdjacent(
                        targetData.collectionRange));
            AddMeetingCells(
                request.map,
                Normalize(target.CurrentCellPosition),
                unitSameCell,
                unitAdjacent,
                neighbors,
                result);
        }

        BoardTopologyIndex topology =
            BoardTopologyIndex.GetOrCreateRuntime(
                request.map,
                request.terrainDatabase);
        bool usedTopology = topology != null && topology.IsReady;
        AIDecisionPerf.AddCount("TopologyIndexQueries");
        AIDecisionPerf.AddCount(
            usedTopology ? "TopologyIndexHits" : "TopologyIndexMisses");
        if (usedTopology)
        {
            IReadOnlyList<Vector3Int> cells =
                topology.SupplierConstructionCells;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int indexedCell = Normalize(cells[i]);
                ConstructionManager target =
                    ConstructionOccupancyRules.GetConstructionAtCell(
                        request.map,
                        indexedCell);
                if (!IsEligibleConstructionEndpoint(
                        request.unit, target))
                    continue;
                AddMeetingCells(
                    request.map,
                    Normalize(target.CurrentCellPosition),
                    sameCell,
                    adjacent,
                    neighbors,
                    result);
            }
            AIDecisionPerf.AddCount(
                "MelhorEstoqueIndexedConstructionQueries");
        }
        else
        {
            for (int i = 0;
                 i < ConstructionManager.AllActive.Count;
                 i++)
            {
                ConstructionManager target =
                    ConstructionManager.AllActive[i];
                if (!IsEligibleConstructionEndpoint(
                        request.unit, target))
                    continue;
                AddMeetingCells(
                    request.map,
                    Normalize(target.CurrentCellPosition),
                    sameCell,
                    adjacent,
                    neighbors,
                    result);
            }
        }
        return result;
    }

    private static IReadOnlyList<UnitManager> ResolveSupplierUnits(
        Tilemap map)
    {
        if (Application.isPlaying
            && ConfirmedOccupancyIndex.TryGetFor(
                map,
                out ConfirmedOccupancyIndex occupancy)
            && occupancy != null
            && occupancy.CanServeLiveQueries)
        {
            AIDecisionPerf.AddCount(
                "MelhorEstoqueConfirmedSupplierQueries");
            return occupancy.Suppliers;
        }

        AIDecisionPerf.AddCount(
            "MelhorEstoqueLiveSupplierFallbacks");
        return UnitManager.AllActive;
    }

    private static bool RangeIncludesSameCell(
        SupplierRangeMode range) =>
        range != SupplierRangeMode.Adjacent1Hex;

    private static bool RangeIncludesAdjacent(
        SupplierRangeMode range) =>
        range == SupplierRangeMode.Adjacent1Hex
        || range == SupplierRangeMode.Hybrid0Or1Hex;

    private static void AddMeetingCells(
        Tilemap map,
        Vector3Int endpoint,
        bool sameCell,
        bool adjacent,
        List<Vector3Int> neighborBuffer,
        HashSet<Vector3Int> output)
    {
        if (sameCell)
            output.Add(endpoint);
        if (!adjacent)
            return;

        UnitMovementPathRules.GetImmediateHexNeighbors(
            map, endpoint, neighborBuffer);
        for (int i = 0; i < neighborBuffer.Count; i++)
            output.Add(Normalize(neighborBuffer[i]));
    }

    private static bool IsEligibleUnitEndpoint(
        UnitManager actor,
        UnitManager target)
    {
        if (actor == null || target == null || target == actor
            || target.IsDead
            || !PlayerSlotRelations.AreAllies(actor, target)
            || !target.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isSupplier
            || !HasTransferService(target))
            return false;
        return data.supplierTier == SupplierTier.Hub
            || data.supplierTier == SupplierTier.Receiver;
    }

    private static bool IsEligibleConstructionEndpoint(
        UnitManager actor,
        ConstructionManager target)
    {
        if (actor == null || target == null
            || (int)actor.TeamId != (int)target.TeamId
            || target.CurrentCapturePoints < target.CapturePointsMax
            || !target.TryResolveConstructionData(
                out ConstructionData data)
            || data == null
            || !data.isSupplier)
            return false;
        return data.supplierTier == SupplierTier.Hub
            || data.supplierTier == SupplierTier.Receiver;
    }

    private static AIReachDecisionTier ResolveTier(
        Vector3Int cell,
        Dictionary<Vector3Int, List<Vector3Int>> tacticalPaths,
        Dictionary<Vector3Int, int> operationalCosts,
        bool includeStrategic)
    {
        if (tacticalPaths != null && tacticalPaths.ContainsKey(cell))
            return AIReachDecisionTier.Tactical;
        if (operationalCosts != null
            && operationalCosts.ContainsKey(cell))
            return AIReachDecisionTier.Operational;
        return includeStrategic
            ? AIReachDecisionTier.Strategic
            : AIReachDecisionTier.None;
    }

    private static bool CanActorStopAt(
        MelhorEstoqueRequest request,
        Vector3Int origin,
        Vector3Int actionCell)
    {
        if (actionCell == origin)
            return true;
        List<UnitManager> occupants =
            UnitOccupancyRules.GetUnitsAtCell(
                request.map, actionCell, request.unit);
        return OccupancyResolver.CanEndMove(
            request.unit, actionCell, occupants);
    }

    private static MelhorEstoqueIntent ResolveIntent(
        StockNeedAssessment actorNeed,
        UnitData actorData,
        PodeTransferirOption transfer)
    {
        if (transfer.targetUnit != null
            && transfer.targetUnit.IsEmbarked
            && transfer.targetUnit.EmbarkedTransporter ==
                transfer.supplierUnit)
            return MelhorEstoqueIntent.EmbarkedDistribution;
        if (transfer.flowMode == TransferFlowMode.Recebedor)
        {
            if (actorNeed != null && actorNeed.NeedsStock)
                return MelhorEstoqueIntent.ReplenishSelf;
            if (transfer.targetConstruction != null)
                return MelhorEstoqueIntent.CollectFromConstruction;
            return actorData.supplierTier == SupplierTier.Receiver
                ? MelhorEstoqueIntent.ReplenishSelf
                : MelhorEstoqueIntent.RebalanceHub;
        }
        if (transfer.targetConstruction != null)
            return MelhorEstoqueIntent.SupplyConstruction;
        if (transfer.targetUnit != null
            && transfer.targetUnit.TryGetUnitData(
                out UnitData targetData)
            && targetData != null
            && targetData.supplierTier == SupplierTier.Receiver)
            return MelhorEstoqueIntent.SupplyReceiver;
        return MelhorEstoqueIntent.RebalanceHub;
    }

    private static bool IntentMatches(
        MelhorEstoqueIntent requested,
        MelhorEstoqueIntent resolved) =>
        requested == MelhorEstoqueIntent.Auto
        || requested == resolved;

    private static MelhorEstoqueOption BuildOption(
        MelhorEstoqueRequest request,
        PodeTransferirOption transfer,
        MelhorEstoqueIntent intent,
        AIReachDecisionTier tier,
        Vector3Int origin,
        Vector3Int actionCell,
        Dictionary<Vector3Int, int> routeCosts,
        out string rejectionReason)
    {
        rejectionReason = string.Empty;
        var option = new MelhorEstoqueOption
        {
            prospectiveTransfer = transfer,
            intent = intent,
            tier = tier,
            actionCell = actionCell,
            endpointCell = Normalize(transfer.targetCell),
            cubicDistance = AIActionReachCoordinator.CubicDistance(
                origin, actionCell),
            routeCost = routeCosts != null
                && routeCosts.TryGetValue(
                    actionCell, out int cost)
                    ? cost
                    : -1,
            threat = request.evaluateThreat?.Invoke(actionCell) ?? 0f
        };

        if (transfer.flowMode == TransferFlowMode.Recebedor)
        {
            option.sourceUnit = transfer.targetUnit;
            option.sourceConstruction =
                transfer.targetConstruction;
            option.destinationUnit = request.unit;
            if (option.sourceUnit != null)
            {
                option.sourceStockNeed =
                    StockNeedAssessmentService.Evaluate(
                        option.sourceUnit,
                        request.emulateStockFromUnitData);
            }
            option.stockNeed = StockNeedAssessmentService.Evaluate(
                request.unit,
                request.emulateStockFromUnitData);
        }
        else
        {
            option.sourceUnit = request.unit;
            option.sourceStockNeed =
                StockNeedAssessmentService.Evaluate(
                    request.unit,
                    request.emulateStockFromUnitData);
            option.destinationUnit = transfer.targetUnit;
            option.destinationConstruction =
                transfer.targetConstruction;
            if (transfer.targetUnit != null)
            {
                option.stockNeed =
                    StockNeedAssessmentService.Evaluate(
                        transfer.targetUnit,
                        request.emulateStockFromUnitData);
            }
            else if (transfer.targetConstruction != null)
            {
                option.constructionStockNeed =
                    StockNeedAssessmentService.Evaluate(
                        transfer.targetConstruction);
            }
        }

        if (option.intent == MelhorEstoqueIntent.RebalanceHub
            && option.sourceStockNeed != null
            && option.stockNeed != null
            && option.sourceStockNeed.fillRatio
                <= option.stockNeed.fillRatio + 0.15f)
        {
            // Evita que dois Hubs parcialmente usados fiquem devolvendo a
            // mesma carga um ao outro. Rebalanceamento exige uma diferenca
            // material entre a reserva de quem doa e a de quem recebe.
            rejectionReason =
                "RebalanceHub sem diferenca material de estoque.";
            return null;
        }

        option.estimatedAmount = EstimateAmount(request, option);
        if (option.estimatedAmount <= 0)
        {
            rejectionReason =
                $"Transferencia {intent} sem carga compativel " +
                "ou sem demanda autorizada.";
            return null;
        }
        int urgency = option.stockNeed != null
            ? (int)option.stockNeed.level
            : ResolveConstructionUrgency(option);
        option.score =
            urgency * 100000f
            + option.estimatedAmount * 100f
            - option.cubicDistance * 100f
            - Mathf.Max(0, option.routeCost) * 10f
            - option.threat * 1000f;
        if (option.intent == MelhorEstoqueIntent.ReplenishSelf
            && option.stockNeed != null
            && !option.stockNeed.NeedsStock)
            option.score -= 50000f;

        option.reason =
            $"intent={intent} tier={tier} " +
            $"fluxo={transfer.flowMode} encontro={actionCell} " +
            $"endpoint={option.endpointCell} " +
            $"rota={(option.routeCost >= 0 ? option.routeCost.ToString() : "direcao")} " +
            $"dist={option.cubicDistance} estimado={option.estimatedAmount} " +
            $"urgencia={(StockNeedLevel)urgency} " +
            $"ameaca={option.threat:0.0}";
        return option;
    }

    private static int EstimateAmount(
        MelhorEstoqueRequest request,
        MelhorEstoqueOption option)
    {
        return StockNeedAssessmentService.CollectTransferEstimate(
            option.sourceUnit,
            option.sourceConstruction,
            option.destinationUnit,
            option.destinationConstruction,
            request.emulateStockFromUnitData,
            option.compatibleSupplies);
    }

    private static int ResolveConstructionUrgency(
        MelhorEstoqueOption option)
    {
        if (option?.destinationConstruction == null
            || option.constructionStockNeed == null)
            return (int)StockNeedLevel.Preventive;
        return (int)option.constructionStockNeed.level;
    }

    private static void RemoveDuplicateOptions(
        List<MelhorEstoqueOption> options)
    {
        var seen = new HashSet<string>();
        for (int i = options.Count - 1; i >= 0; i--)
        {
            MelhorEstoqueOption option = options[i];
            string key = BuildKey(option);
            if (!seen.Add(key))
                options.RemoveAt(i);
        }
    }

    private static string BuildKey(MelhorEstoqueOption option)
    {
        PodeTransferirOption transfer =
            option?.prospectiveTransfer;
        return transfer == null
            ? Guid.NewGuid().ToString()
            : $"{(int)transfer.flowMode}:" +
              $"{(transfer.targetUnit != null ? transfer.targetUnit.InstanceId : 0)}:" +
              $"{(transfer.targetConstruction != null ? transfer.targetConstruction.InstanceId : 0)}:" +
              $"{option.actionCell.x}:{option.actionCell.y}";
    }

    private static int Compare(
        MelhorEstoqueOption a,
        MelhorEstoqueOption b)
    {
        int byTier = a.tier.CompareTo(b.tier);
        if (byTier != 0) return byTier;
        int byScore = b.score.CompareTo(a.score);
        if (byScore != 0) return byScore;
        int byDistance =
            a.cubicDistance.CompareTo(b.cubicDistance);
        if (byDistance != 0) return byDistance;
        return a.routeCost.CompareTo(b.routeCost);
    }

    private static AIReachDecisionResult<MelhorEstoqueOption>
        ResolveByReachCoordinator(
            MelhorEstoqueRequest request,
            List<MelhorEstoqueOption> ranking)
    {
        AIReachDecisionStages stages =
            AIReachDecisionStages.Tactical
            | AIReachDecisionStages.Operational;
        if (request.includeStrategic)
            stages |= AIReachDecisionStages.Strategic;

        return AIActionReachCoordinator.Evaluate(
            new AIReachDecisionRequest<MelhorEstoqueOption>
            {
                Context = $"Stock:{request.unit.InstanceId}",
                Policy = new AIReachDecisionPolicy(
                    stages, request.operationalTurns),
                CurrentMovementBudget =
                    Mathf.Max(0, request.tacticalBudget),
                StrategicSearchBudget = int.MaxValue,
                EvaluateTactical = (int budget,
                    out AIReachDecisionCandidate<
                        MelhorEstoqueOption> candidate) =>
                    TryResolveTier(
                        ranking,
                        AIReachDecisionTier.Tactical,
                        out candidate),
                EvaluateOperational = (int budget,
                    out AIReachDecisionCandidate<
                        MelhorEstoqueOption> candidate) =>
                    TryResolveTier(
                        ranking,
                        AIReachDecisionTier.Operational,
                        out candidate),
                EvaluateStrategic = (int budget,
                    out AIReachDecisionCandidate<
                        MelhorEstoqueOption> candidate) =>
                    TryResolveTier(
                        ranking,
                        AIReachDecisionTier.Strategic,
                        out candidate),
                DiagnosticLog = request.diagnosticLog
            });
    }

    private static bool TryResolveTier(
        List<MelhorEstoqueOption> ranking,
        AIReachDecisionTier tier,
        out AIReachDecisionCandidate<MelhorEstoqueOption> candidate)
    {
        candidate = null;
        MelhorEstoqueOption best =
            ranking.Find(option => option.tier == tier);
        if (best == null)
            return false;
        candidate =
            new AIReachDecisionCandidate<MelhorEstoqueOption>
            {
                Value = best,
                ActionCell = best.actionCell,
                TargetCell = best.endpointCell,
                Score = best.score,
                Reason = best.reason
            };
        return true;
    }

    private static bool HasTransferService(UnitManager unit)
    {
        IReadOnlyList<ServiceData> services =
            unit?.GetEmbarkedServices();
        if (services == null)
            return false;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service != null
                && service.serviceType == ServiceType.Transfer)
                return true;
        }
        return false;
    }

    private static Vector3Int Normalize(Vector3Int cell)
    {
        cell.z = 0;
        return cell;
    }

    private static Dictionary<Vector3Int, List<Vector3Int>>
        ClonePaths(
            Dictionary<Vector3Int, List<Vector3Int>> source)
    {
        var clone =
            new Dictionary<Vector3Int, List<Vector3Int>>(
                source?.Count ?? 0);
        if (source == null)
            return clone;
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair
                 in source)
        {
            clone[pair.Key] = pair.Value != null
                ? new List<Vector3Int>(pair.Value)
                : null;
        }
        return clone;
    }
}
