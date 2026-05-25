using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------

    // Construtores de PlayerAction

    // -------------------------------------------------------------------------

    private PlayerAction BuildMoveBatch(UnitManager unit, TeamId team, Vector3Int from, Vector3Int to,

        Dictionary<Vector3Int, List<Vector3Int>> paths = null)

    {

        List<Vector3Int> movementPath = null;

        paths?.TryGetValue(to, out movementPath);

        return new PlayerAction

        {

            IsAIGenerated  = true,

            ActionType     = PlayerActionType.UnitAction,

            ActingTeam     = team,

            TurnNumber     = matchController != null ? matchController.CurrentTurn : 0,

            CursorHex      = from, HasCursorHex = true,

            UnitInstanceId = unit.InstanceId.ToString(),

            MoveFrom       = from, HasMoveFrom = true,

            MoveTo         = to,   HasMoveTo   = true,

            SensorAction   = SensorActionType.None,

            MovementPath   = movementPath,

            DebugLabel     = $"AI Move {unit.InstanceId} → {to}",

        };

    }

    private PlayerAction BuildCaptureBatch(UnitManager unit, TeamId team, Vector3Int from, Vector3Int to,

        Dictionary<Vector3Int, List<Vector3Int>> paths = null)

    {

        List<Vector3Int> movementPath = null;

        paths?.TryGetValue(to, out movementPath);

        return new PlayerAction

        {

            IsAIGenerated  = true,

            ActionType     = PlayerActionType.UnitAction,

            ActingTeam     = team,

            TurnNumber     = matchController != null ? matchController.CurrentTurn : 0,

            CursorHex      = from, HasCursorHex = true,

            UnitInstanceId = unit.InstanceId.ToString(),

            MoveFrom       = from, HasMoveFrom = true,

            MoveTo         = to,   HasMoveTo   = true,

            SensorAction   = SensorActionType.Capture,

            MovementPath   = movementPath,

            DebugLabel     = $"AI Capture {unit.InstanceId} @ {to}",

        };

    }

    private PlayerAction BuildAttackBatch(UnitManager unit, TeamId team,

        Vector3Int from, Vector3Int to, string targetId, Vector3Int targetCell,

        Dictionary<Vector3Int, List<Vector3Int>> paths = null)

    {

        List<Vector3Int> movementPath = null;

        paths?.TryGetValue(to, out movementPath);

        return new PlayerAction

        {

            IsAIGenerated   = true,

            ActionType      = PlayerActionType.UnitAction,

            ActingTeam      = team,

            TurnNumber      = matchController != null ? matchController.CurrentTurn : 0,

            CursorHex       = from, HasCursorHex = true,

            UnitInstanceId  = unit.InstanceId.ToString(),

            MoveFrom        = from, HasMoveFrom = true,

            MoveTo          = to,   HasMoveTo   = true,

            SensorAction    = SensorActionType.Attack,

            MovementPath    = movementPath,

            TargetInstanceId = targetId,

            TargetHex       = targetCell, HasTargetHex = true,

            DebugLabel      = $"AI Attack {unit.InstanceId} → {targetId} @ {targetCell}",

        };

    }

    private PlayerAction BuildMergeBatch(UnitManager unit, TeamId team,

        Vector3Int from, Vector3Int to, UnitManager target,

        Dictionary<Vector3Int, List<Vector3Int>> paths = null)

    {

        List<Vector3Int> movementPath = null;

        paths?.TryGetValue(to, out movementPath);

        Vector3Int targetCell = target.CurrentCellPosition; targetCell.z = 0;

        var action = new PlayerAction

        {

            IsAIGenerated    = true,

            ActionType       = PlayerActionType.UnitAction,

            ActingTeam       = team,

            TurnNumber       = matchController != null ? matchController.CurrentTurn : 0,

            CursorHex        = from, HasCursorHex = true,

            UnitInstanceId   = unit.InstanceId.ToString(),

            MoveFrom         = from, HasMoveFrom = true,

            MoveTo           = to,   HasMoveTo   = true,

            SensorAction     = SensorActionType.Merge,

            MovementPath     = movementPath,

            DebugLabel       = $"AI Merge {unit.InstanceId} → {target.InstanceId}",

        };

        action.SubSteps.Add(new PlayerActionSubStep

        {

            Label            = "AIFuse",

            TargetInstanceId = target.InstanceId.ToString(),

            TargetHex        = targetCell,

            HasTargetHex     = true,

        });

        return action;

    }

    private PlayerAction BuildSupplyBatch(
        UnitManager supplier,
        TeamId team,
        Vector3Int from,
        Vector3Int to,
        List<UnitManager> targets,
        Dictionary<Vector3Int, List<Vector3Int>> paths = null)
    {
        List<Vector3Int> movementPath = null;
        paths?.TryGetValue(to, out movementPath);

        var action = new PlayerAction
        {
            IsAIGenerated = true,
            ActionType = PlayerActionType.UnitAction,
            ActingTeam = team,
            TurnNumber = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex = from,
            HasCursorHex = true,
            UnitInstanceId = supplier.InstanceId.ToString(),
            MoveFrom = from,
            HasMoveFrom = true,
            MoveTo = to,
            HasMoveTo = true,
            SensorAction = SensorActionType.Supply,
            MovementPath = movementPath,
            DebugLabel = $"AI Supply {supplier.InstanceId} ({(targets != null ? targets.Count : 0)} alvo(s)) via {to}",
        };

        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                UnitManager target = targets[i];
                if (target == null)
                    continue;

                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;
                action.SubSteps.Add(new PlayerActionSubStep
                {
                    Label = "SupplyQueueConfirm",
                    TargetInstanceId = target.InstanceId.ToString(),
                    TargetHex = targetCell,
                    HasTargetHex = true,
                });
            }
        }

        return action;
    }

    private PlayerAction BuildTransferReceiveBatch(
        UnitManager receiver,
        TeamId team,
        Vector3Int from,
        Vector3Int to,
        PodeTransferirOption option,
        Dictionary<Vector3Int, List<Vector3Int>> paths = null)
    {
        List<Vector3Int> movementPath = null;
        paths?.TryGetValue(to, out movementPath);

        Vector3Int targetCell = option != null ? option.targetCell : to;
        targetCell.z = 0;

        var action = new PlayerAction
        {
            IsAIGenerated = true,
            ActionType = PlayerActionType.UnitAction,
            ActingTeam = team,
            TurnNumber = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex = from,
            HasCursorHex = true,
            UnitInstanceId = receiver.InstanceId.ToString(),
            MoveFrom = from,
            HasMoveFrom = true,
            MoveTo = to,
            HasMoveTo = true,
            SensorAction = SensorActionType.Transfer,
            MovementPath = movementPath,
            DebugLabel = $"AI TransferReceive {receiver.InstanceId} via {to}",
        };

        action.SubSteps.Add(new PlayerActionSubStep
        {
            Label = "TransferTargetConfirm",
            TargetInstanceId = option != null && option.targetUnit != null ? option.targetUnit.InstanceId.ToString() : null,
            TargetConstructionId = option != null && option.targetConstruction != null ? option.targetConstruction.InstanceId.ToString() : null,
            TargetHex = targetCell,
            HasTargetHex = true,
        });

        return action;
    }

    private PlayerAction BuildEmbarcarBatch(
        UnitManager passenger,
        TeamId team,
        Vector3Int from,
        UnitManager transporter,
        int slotIndex,
        Dictionary<Vector3Int, List<Vector3Int>> paths = null)
    {
        Vector3Int transporterCell = transporter.CurrentCellPosition; transporterCell.z = 0;
        List<Vector3Int> movementPath = null;
        paths?.TryGetValue(transporterCell, out movementPath);

        // MoveTo = hex onde a unidade PARA para embarcar (adjacente ao transporter, nunca sobre ele).
        // Percorre o path de trás pra frente e usa o último cell que não é o transporterCell.
        // Caso sem path (adjacente, ficar parado): from.
        Vector3Int moveTo = from;
        if (movementPath != null)
        {
            for (int i = movementPath.Count - 1; i >= 0; i--)
            {
                Vector3Int c = movementPath[i]; c.z = 0;
                if (c != transporterCell) { moveTo = c; break; }
            }
        }

        return new PlayerAction
        {
            IsAIGenerated    = true,
            ActionType       = PlayerActionType.UnitAction,
            ActingTeam       = team,
            TurnNumber       = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex        = from, HasCursorHex = true,
            UnitInstanceId   = passenger.InstanceId.ToString(),
            MoveFrom         = from, HasMoveFrom = true,
            MoveTo           = moveTo, HasMoveTo = true,
            SensorAction     = SensorActionType.Embark,
            TargetInstanceId = transporter.InstanceId.ToString(),
            TargetHex        = transporterCell, HasTargetHex = true,
            MovementPath     = movementPath,
            DebugLabel       = $"AI Embark {passenger.InstanceId} → {transporter.InstanceId} (slot {slotIndex})",
        };
    }

    // O transportador não se move ao desembarcar: o sensor é avaliado da posição atual
    // e os SubSteps distribuem cada passageiro para um hex adjacente livre.
    private PlayerAction BuildDesembarcarBatch(
        UnitManager transporter,
        TeamId team,
        Vector3Int from,
        List<PodeDesembarcarOption> disembarkOrders)
    {
        var action = new PlayerAction
        {
            IsAIGenerated  = true,
            ActionType     = PlayerActionType.UnitAction,
            ActingTeam     = team,
            TurnNumber     = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex      = from, HasCursorHex = true,
            UnitInstanceId = transporter.InstanceId.ToString(),
            MoveFrom       = from, HasMoveFrom = true,
            MoveTo         = from, HasMoveTo   = true,
            SensorAction   = SensorActionType.Disembark,
            DebugLabel     = $"AI Disembark ← {transporter.InstanceId} ({disembarkOrders.Count} passageiro(s))",
        };
        foreach (PodeDesembarcarOption order in disembarkOrders)
        {
            Vector3Int targetCell = order.disembarkCell; targetCell.z = 0;
            action.SubSteps.Add(new PlayerActionSubStep
            {
                Label            = "AIDisembark",
                TargetInstanceId = order.passengerUnit.InstanceId.ToString(),
                TargetHex        = targetCell,
                HasTargetHex     = true,
            });
        }
        return action;
    }

    private PlayerAction BuildDesembarcarBatch(
        UnitManager transporter,
        TeamId team,
        Vector3Int from,
        List<PodeDesembarcarOption> disembarkOrders,
        Vector3Int moveTo,
        List<Vector3Int> movementPath)
    {
        var action = new PlayerAction
        {
            IsAIGenerated  = true,
            ActionType     = PlayerActionType.UnitAction,
            ActingTeam     = team,
            TurnNumber     = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex      = from, HasCursorHex = true,
            UnitInstanceId = transporter.InstanceId.ToString(),
            MoveFrom       = from, HasMoveFrom = true,
            MoveTo         = moveTo, HasMoveTo  = true,
            SensorAction   = SensorActionType.Disembark,
            MovementPath   = movementPath,
            DebugLabel     = $"AI Disembark ← {transporter.InstanceId} ({disembarkOrders.Count} passageiro(s)) via {moveTo}",
        };
        foreach (PodeDesembarcarOption order in disembarkOrders)
        {
            Vector3Int targetCell = order.disembarkCell; targetCell.z = 0;
            action.SubSteps.Add(new PlayerActionSubStep
            {
                Label            = "AIDisembark",
                TargetInstanceId = order.passengerUnit.InstanceId.ToString(),
                TargetHex        = targetCell,
                HasTargetHex     = true,
            });
        }
        return action;
    }

    private PlayerAction BuildEndTurnBatch(TeamId team)

    {

        return new PlayerAction

        {

            IsAIGenerated = true,

            ActionType    = PlayerActionType.EndTurn,

            ActingTeam    = team,

            TurnNumber    = matchController != null ? matchController.CurrentTurn : 0,

            DebugLabel    = "AI EndTurn",

        };

    }

    private PlayerAction BuildCommandServiceBatch(TeamId team)

    {

        return new PlayerAction

        {

            IsAIGenerated = true,

            ActionType    = PlayerActionType.CommandService,

            ActingTeam    = team,

            TurnNumber    = matchController != null ? matchController.CurrentTurn : 0,

            SensorAction  = SensorActionType.CommandService,

            Confirmed     = true,

            DebugLabel    = "AI CommandService",

        };

    }

    private PlayerAction BuildShoppingBatch(TeamId team, AIShoppingPlanner.ShoppingOrder order)

    {

        Vector3Int cell = order.Building.CurrentCellPosition; cell.z = 0;

        string unitId = !string.IsNullOrWhiteSpace(order.UnitToBuy.id)

            ? order.UnitToBuy.id

            : order.UnitToBuy.name;

        return new PlayerAction

        {

            IsAIGenerated         = true,

            ActionType            = PlayerActionType.Shopping,

            ActingTeam            = team,

            TurnNumber            = matchController != null ? matchController.CurrentTurn : 0,

            CursorHex             = cell, HasCursorHex = true,

            TargetHex             = cell,

            TargetConstructionId   = order.Building != null ? order.Building.InstanceId.ToString() : null,

            SensorAction          = SensorActionType.Shopping,

            ShoppingSelectedIndex = order.SelectedIndex,

            ShoppingUnitTypeId    = unitId,

            Confirmed             = true,

            DebugLabel            = $"AI Shopping: {order.UnitToBuy.name} @ {cell}",

        };

    }
}
