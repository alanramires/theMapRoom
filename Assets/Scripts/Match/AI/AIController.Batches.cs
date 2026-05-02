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

            SensorAction          = SensorActionType.Shopping,

            ShoppingSelectedIndex = order.SelectedIndex,

            ShoppingUnitTypeId    = unitId,

            Confirmed             = true,

            DebugLabel            = $"AI Shopping: {order.UnitToBuy.name} @ {cell}",

        };

    }
}
