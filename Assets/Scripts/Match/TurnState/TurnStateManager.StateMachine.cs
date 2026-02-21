using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    public ActionSfx HandleConfirm()
    {
        if (IsMovementAnimationRunning())
            return ActionSfx.None;

        switch (cursorState)
        {
            case CursorState.Neutral:
                return HandleConfirmWhileNeutral();
            case CursorState.UnitSelected:
                return HandleConfirmWhileUnitSelected();
            case CursorState.MoveuAndando:
                return HandleConfirmWhileMoveuAndando();
            case CursorState.MoveuParado:
                return HandleConfirmWhileMoveuParado();
            case CursorState.Mirando:
                return HandleConfirmWhileMirando();
        }

        return ActionSfx.None;
    }

    public ActionSfx HandleCancel()
    {
        if (IsMovementAnimationRunning())
            return ActionSfx.None;

        switch (cursorState)
        {
            case CursorState.Neutral:
                return ActionSfx.None;
            case CursorState.UnitSelected:
                ClearSelectionAndReturnToNeutral();
                return ActionSfx.Cancel;
            case CursorState.MoveuAndando:
                return HandleCancelWhileMoveuAndando();
            case CursorState.MoveuParado:
                return HandleCancelWhileMoveuParado();
            case CursorState.Mirando:
                return HandleCancelWhileMirando();
        }

        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileNeutral()
    {
        if (cursorController == null)
            return ActionSfx.None;

        Vector3Int cursorCell = cursorController.CurrentCell;
        UnitManager unit = FindUnitAtCell(cursorCell);
        if (unit == null)
            return ActionSfx.None;

        int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;
        bool isAlly = (int)unit.TeamId == activeTeam;
        if (!isAlly)
        {
            Debug.Log($"debug: inspecionando inimigo (unit={unit.name}, unitTeam={(int)unit.TeamId}, activeTeam={activeTeam}, hasActed={unit.HasActed})");
            return ActionSfx.Confirm;
        }

        if (unit.HasActed)
        {
            Debug.Log($"debug: inspecionando aliado que ja agiu (unit={unit.name}, unitTeam={(int)unit.TeamId}, activeTeam={activeTeam}, hasActed={unit.HasActed})");
            return ActionSfx.Confirm;
        }

        SetSelectedUnit(unit);
        cursorState = CursorState.UnitSelected;
        return ActionSfx.Confirm;
    }

    private ActionSfx HandleConfirmWhileUnitSelected()
    {
        if (cursorController == null || selectedUnit == null)
            return ActionSfx.None;

        Vector3Int cursorCell = cursorController.CurrentCell;
        UnitManager unit = FindUnitAtCell(cursorCell);
        if (unit != null && unit != selectedUnit)
        {
            Debug.Log("unidade selecionada, escolha um local valido para movimento");
            return ActionSfx.Error;
        }

        Vector3Int currentUnitCell = selectedUnit.CurrentCellPosition;
        currentUnitCell.z = 0;
        if (cursorCell == currentUnitCell)
        {
            cursorState = CursorState.MoveuParado;
            RefreshSensorsForCurrentState();
            Debug.Log("moveu no mesmo lugar");
            return ActionSfx.Confirm;
        }

        if (selectedUnit.IsAircraftGrounded)
        {
            if (!TryPrepareAutomaticTakeoffForMovement(out string takeoffBlockReason))
            {
                if (!string.IsNullOrWhiteSpace(takeoffBlockReason))
                    Debug.Log(takeoffBlockReason);
                return ActionSfx.Error;
            }
        }

        if (!paintedRangeLookup.Contains(cursorCell))
            return ActionSfx.Error;

        if (!TryGetSelectedUnitPath(cursorCell, out List<Vector3Int> path))
            return ActionSfx.Error;

        BeginMovementToSelectedCell(path);
        return ActionSfx.Confirm;
    }

    private bool TryPrepareAutomaticTakeoffForMovement(out string blockReason)
    {
        blockReason = string.Empty;
        if (selectedUnit == null || !selectedUnit.IsAircraftGrounded)
            return true;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        AircraftOperationDecision decision = AircraftOperationRules.Evaluate(
            selectedUnit,
            boardMap,
            terrainDatabase,
            SensorMovementMode.MoveuParado);
        if (!decision.available || decision.action != AircraftOperationAction.Takeoff)
        {
            blockReason = string.IsNullOrWhiteSpace(decision.reason)
                ? "Aeronave em solo sem decolagem valida neste hex."
                : decision.reason;
            return false;
        }

        if (decision.consumesAction)
        {
            blockReason = "Decolagem neste hex consome acao. Use \"E\" para decolar parado.";
            return false;
        }

        if (!AircraftOperationRules.TryApplyOperation(
                selectedUnit,
                boardMap,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                out _))
        {
            blockReason = "Falha ao preparar decolagem automatica.";
            return false;
        }

        PaintSelectedUnitMovementRange();
        return true;
    }

    private ActionSfx HandleConfirmWhileMoveuAndando()
    {
        if (TryConfirmScannerAttack())
            return ActionSfx.Confirm;

        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileMoveuParado()
    {
        if (TryConfirmScannerAttack())
            return ActionSfx.Confirm;

        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileMoveuAndando()
    {
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        if (selectedUnit == null || !hasCommittedMovement || committedMovementPath.Count < 2)
            return ActionSfx.Cancel;

        RestorePreparedFuelCostIfAny();
        BeginRollbackToSelection();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleCancelWhileMoveuParado()
    {
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        cursorState = CursorState.UnitSelected;
        ClearSensorResults();
        PaintSelectedUnitMovementRange();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileMirando()
    {
        if (TryConfirmScannerAttack())
            return ActionSfx.Confirm;

        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileMirando()
    {
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitMirandoStateToMovement();
        return ActionSfx.Cancel;
    }
}
