using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private static bool IsPrimaryStockUnit(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.roles != null
            && data.roles.Count > 0
            && data.roles[0] == UnitRole.Estoque
            && HasStockTransferCapability(unit, data);
    }

    private static bool HasStockTransferCapability(
        UnitManager unit,
        UnitData data = null)
    {
        if (unit == null)
            return false;
        if (data == null
            && !unit.TryGetUnitData(out data))
            return false;
        if (data == null
            || !data.isSupplier
            || (data.supplierTier != SupplierTier.Hub
                && data.supplierTier != SupplierTier.Receiver))
            return false;

        IReadOnlyList<ServiceData> runtimeServices =
            unit.GetEmbarkedServices();
        for (int i = 0;
             runtimeServices != null && i < runtimeServices.Count;
             i++)
        {
            ServiceData service = runtimeServices[i];
            if (service != null
                && service.serviceType == ServiceType.Transfer)
                return true;
        }

        if (data.supplierServicesProvided == null)
            return false;
        for (int i = 0;
             i < data.supplierServicesProvided.Count;
             i++)
        {
            ServiceData service =
                data.supplierServicesProvided[i];
            if (service != null
                && service.serviceType == ServiceType.Transfer)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Papel Estoque e branch de transportadores-hub. Transporte ja tentou
    /// Hospital, EVAC, Supply e Pickup antes deste ponto; Logistica chama a
    /// mesma materializacao internamente depois do atendimento de campo.
    /// </summary>
    private PlayerAction TryDecideStockAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null)
            return null;

        bool primaryStock = IsPrimaryStockUnit(unit);
        bool transportHybrid =
            IsPrimaryTransportRole(unit)
            && HasStockTransferCapability(unit);
        if (!primaryStock && !transportHybrid)
            return null;

        if (primaryStock)
        {
            PlayerAction repairAction =
                TryDecideRepairAction(unit, snapshot, plan);
            if (repairAction != null)
                return repairAction;
        }

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap,
                unit,
                Mathf.Max(0, unit.RemainingMovementPoints),
                terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (TryBuildStockNetworkAction(
                unit,
                snapshot,
                fromCell,
                paths,
                occupied,
                out PlayerAction action,
                out string reason))
        {
            Debug.Log(
                $"{TL("Stock")} {unit.InstanceId} " +
                $"circula estoque — {reason}");
            return action;
        }

        Debug.Log(
            $"{TL("Stock")} {unit.InstanceId} " +
            $"sem operacao de estoque Tactical/Operational — {reason}");
        if (!primaryStock)
            return null;

        return BuildMoveBatch(
            unit,
            snapshot.AITeam,
            fromCell,
            fromCell,
            paths);
    }

    private bool TryBuildStockNetworkAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action,
        out string reason,
        bool allowStrategicDirection = false)
    {
        action = null;
        reason = string.Empty;
        if (unit == null
            || snapshot == null
            || boardTilemap == null
            || terrainDatabase == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || !HasStockTransferCapability(unit, data))
        {
            reason = "capacidade Transfer indisponivel";
            return false;
        }

        fromCell.z = 0;
        bool avoidThreat = data.playConservative;
        StockNeedAssessment actorNeed =
            StockNeedAssessmentService.Evaluate(unit);
        MelhorEstoqueIntent requestedIntent =
            actorNeed != null
            && actorNeed.level >= StockNeedLevel.Operational
                ? MelhorEstoqueIntent.ReplenishSelf
                : MelhorEstoqueIntent.Auto;
        MelhorEstoqueResult result =
            MelhorEstoqueService.Evaluate(
                new MelhorEstoqueRequest
                {
                    unit = unit,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    intent = requestedIntent,
                    tacticalBudget = Mathf.Max(
                        0, unit.RemainingMovementPoints),
                    operationalTurns = 2,
                    includeStrategic = allowStrategicDirection,
                    emulateStockFromUnitData = false,
                    maxThreat = avoidThreat
                        ? 0f
                        : float.PositiveInfinity,
                    evaluateThreat = cell =>
                        CalculateThreatLevel(
                            cell, snapshot.AITeam),
                    diagnosticLog = line => Debug.Log(line)
                });

        if (result?.reachDecision == null
            || !result.reachDecision.Found
            || result.reachDecision.Decision?.Value == null)
        {
            reason =
                $"MelhorEstoque {requestedIntent} sem encontro " +
                $"(rejeitados={result?.rejected.Count ?? 0})";
            return false;
        }

        MelhorEstoqueOption stock =
            result.reachDecision.Decision.Value;
        PodeTransferirOption transfer =
            stock.prospectiveTransfer;
        if (transfer == null)
        {
            reason = "MelhorEstoque sem opcao do PodeTransferir";
            return false;
        }

        Vector3Int rendezvous = stock.actionCell;
        rendezvous.z = 0;
        if (result.reachDecision.Tier ==
            AIReachDecisionTier.Tactical)
        {
            if (rendezvous != fromCell
                && (paths == null
                    || !paths.ContainsKey(rendezvous)))
            {
                reason =
                    $"encontro Tactical {rendezvous} fora dos " +
                    "caminhos atuais";
                return false;
            }

            action = BuildStockTransferBatch(
                unit,
                snapshot.AITeam,
                fromCell,
                rendezvous,
                transfer,
                stock.estimatedAmount,
                paths);
            reason =
                $"{stock.intent} Tactical {stock.reason}";
            return true;
        }

        AIReachDecisionTier reachTier =
            result.reachDecision.Tier;
        if (reachTier != AIReachDecisionTier.Operational
            && (!allowStrategicDirection
                || reachTier != AIReachDecisionTier.Strategic))
        {
            reason =
                $"tier {reachTier} nao materializado";
            return false;
        }

        if (!TryChooseStockProgressCell(
                unit,
                snapshot,
                fromCell,
                rendezvous,
                paths,
                occupied,
                avoidThreat,
                ToolProgressionIntent.StockNetwork,
                out Vector3Int progressCell,
                out string progressReason))
        {
            reason =
                $"encontro Operational={rendezvous}, " +
                "mas sem progressao valida";
            return false;
        }

        action = BuildMoveBatch(
            unit,
            snapshot.AITeam,
            fromCell,
            progressCell,
            paths);
        reason =
            $"{stock.intent} {reachTier} encontro={rendezvous} " +
            $"{progressReason} {stock.reason}";
        return true;
    }

    private PlayerAction BuildStockTransferBatch(
        UnitManager actor,
        TeamId team,
        Vector3Int from,
        Vector3Int to,
        PodeTransferirOption option,
        int estimatedAmount,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        List<Vector3Int> movementPath = null;
        paths?.TryGetValue(to, out movementPath);

        Vector3Int targetCell =
            option != null ? option.targetCell : to;
        targetCell.z = 0;
        TransferFlowMode flow = option != null
            ? option.flowMode
            : TransferFlowMode.Recebedor;
        int donationPercent =
            ResolveStockTransferDonationPercent(
                actor,
                option,
                estimatedAmount);
        var action = new PlayerAction
        {
            IsAIGenerated = true,
            ActionType = PlayerActionType.UnitAction,
            ActingTeam = team,
            TurnNumber = matchController != null
                ? matchController.CurrentTurn
                : 0,
            CursorHex = from,
            HasCursorHex = true,
            UnitInstanceId = actor.InstanceId.ToString(),
            MoveFrom = from,
            HasMoveFrom = true,
            MoveTo = to,
            HasMoveTo = true,
            SensorAction = SensorActionType.Transfer,
            MovementPath = movementPath,
            DebugLabel =
                $"AI StockTransfer {actor.InstanceId} " +
                $"{flow} via {to}"
        };
        action.SubSteps.Add(new PlayerActionSubStep
        {
            Label = flow == TransferFlowMode.Fornecimento
                ? $"TransferSupply:{donationPercent}:TargetConfirm"
                : "TransferReceiveTargetConfirm",
            TargetInstanceId =
                option != null && option.targetUnit != null
                    ? option.targetUnit.InstanceId.ToString()
                    : null,
            TargetConstructionId =
                option != null
                && option.targetConstruction != null
                    ? option.targetConstruction.InstanceId.ToString()
                    : null,
            TargetHex = targetCell,
            HasTargetHex = true
        });
        return action;
    }

    private static int ResolveStockTransferDonationPercent(
        UnitManager actor,
        PodeTransferirOption option,
        int estimatedAmount)
    {
        if (actor == null
            || option == null
            || option.flowMode != TransferFlowMode.Fornecimento
            || option.targetConstruction == null)
            return 100;

        int available =
            StockNeedAssessmentService.GetTotalCurrentStock(actor);
        if (available <= 0 || estimatedAmount <= 0)
            return 100;

        int requestedPercent = Mathf.CeilToInt(
            estimatedAmount * 100f / available);
        if (requestedPercent <= 25)
            return 25;
        if (requestedPercent <= 50)
            return 50;
        if (requestedPercent <= 75)
            return 75;
        return 100;
    }
}
