using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    public ActionSfx HandleConfirm()
    {
        LogStateStep("HandleConfirm");
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
            case CursorState.Pousando:
                return HandleConfirmWhilePousando();
            case CursorState.Embarcando:
                return HandleConfirmWhileEmbarcando();
            case CursorState.Desembarcando:
                return HandleConfirmWhileDesembarcando();
        }

        return ActionSfx.None;
    }

    public ActionSfx HandleCancel()
    {
        LogStateStep("HandleCancel", rollback: true);
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
            case CursorState.Pousando:
                return HandleCancelWhilePousando();
            case CursorState.Embarcando:
                return HandleCancelWhileEmbarcando();
            case CursorState.Desembarcando:
                return HandleCancelWhileDesembarcando();
        }

        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileNeutral()
    {
        LogStateStep("HandleConfirmWhileNeutral");
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

        TryPrepareTemporaryTakeoffStateForSelection(unit, out string takeoffInfo);
        if (!string.IsNullOrWhiteSpace(takeoffInfo))
            Debug.Log($"[Pode Decolar] {takeoffInfo}");

        SetSelectedUnit(unit);
        SetCursorState(CursorState.UnitSelected, "HandleConfirmWhileNeutral: ally selected");
        return ActionSfx.Confirm;
    }

    private ActionSfx HandleConfirmWhileUnitSelected()
    {
        LogStateStep("HandleConfirmWhileUnitSelected");
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
            if (!IsTakeoffMoveDistanceAllowed(0))
            {
                Debug.Log("Decolagem neste contexto nao permite permanecer em 0 hex.");
                return ActionSfx.Error;
            }

            EnterSensorsState(CursorState.MoveuParado);
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
        if (!IsTakeoffMoveDistanceAllowed(path.Count - 1))
        {
            Debug.Log("Distancia invalida para o modo de decolagem disponivel neste contexto.");
            return ActionSfx.Error;
        }

        BeginMovementToSelectedCell(path);
        return ActionSfx.Confirm;
    }

    private bool TryPrepareAutomaticTakeoffForMovement(out string blockReason)
    {
        blockReason = string.Empty;
        if (selectedUnit == null || !selectedUnit.IsAircraftGrounded)
            return true;
        if (hasTemporaryTakeoffSelectionState &&
            (temporaryTakeoffMoveOptions.Contains(0) || temporaryTakeoffMoveOptions.Contains(1)))
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
        LogStateStep("HandleConfirmWhileMoveuAndando");
        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileMoveuParado()
    {
        LogStateStep("HandleConfirmWhileMoveuParado");
        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileMoveuAndando()
    {
        LogStateStep("HandleCancelWhileMoveuAndando", rollback: true);
        Debug.Log(
            $"[Rollback] ESC em fluxo andado (state={cursorState}) | hasCommittedMovement={hasCommittedMovement} | " +
            $"pathCount={committedMovementPath.Count} | selected={(selectedUnit != null ? selectedUnit.name : "(none)")}");

        if (HandleScannerPromptCancel())
        {
            Debug.Log("[Rollback] ESC consumido por submenu/scanner prompt.");
            return ActionSfx.Cancel;
        }

        if (selectedUnit == null)
            return ActionSfx.Cancel;

        if (!hasCommittedMovement || committedMovementPath.Count < 2)
        {
            Debug.Log("[Rollback] Sem caminho comprometido valido. Fallback para UnitSelected.");
            SetCursorState(CursorState.UnitSelected, "HandleCancelWhileMoveuAndando: fallback without committed path", rollback: true);
            ClearSensorResults();
            PaintSelectedUnitMovementRange();
            return ActionSfx.Cancel;
        }

        RestorePreparedFuelCostIfAny();
        if (!BeginRollbackToSelection())
        {
            Debug.Log("[Rollback] Falha ao iniciar animacao de rollback. Fallback para UnitSelected.");
            SetCursorState(CursorState.UnitSelected, "HandleCancelWhileMoveuAndando: rollback animation failed", rollback: true);
            ClearCommittedMovement();
            ClearSensorResults();
            PaintSelectedUnitMovementRange();
        }
        else
        {
            Debug.Log("[Rollback] Animacao de rollback iniciada.");
        }
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleCancelWhileMoveuParado()
    {
        LogStateStep("HandleCancelWhileMoveuParado", rollback: true);
        RestoreForcedLayerAfterRollbackIfNeeded();
        SetCursorState(CursorState.UnitSelected, "HandleCancelWhileMoveuParado", rollback: true);
        ClearSensorResults();
        PaintSelectedUnitMovementRange();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileMirando()
    {
        LogStateStep("HandleConfirmWhileMirando");
        if (TryConfirmScannerAttack())
            return ActionSfx.Confirm;

        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileMirando()
    {
        LogStateStep("HandleCancelWhileMirando", rollback: true);
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitMirandoStateToMovement();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhilePousando()
    {
        LogStateStep("HandleConfirmWhilePousando");
        if (TryConfirmScannerLanding())
            return ActionSfx.Confirm;

        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhilePousando()
    {
        LogStateStep("HandleCancelWhilePousando", rollback: true);
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitLandingStateToMovement();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileEmbarcando()
    {
        LogStateStep("HandleConfirmWhileEmbarcando");
        if (TryConfirmScannerEmbark())
            return ActionSfx.Confirm;

        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileEmbarcando()
    {
        LogStateStep("HandleCancelWhileEmbarcando", rollback: true);
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitEmbarkStateToMovement();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileDesembarcando()
    {
        LogStateStep("HandleConfirmWhileDesembarcando");
        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileDesembarcando()
    {
        LogStateStep("HandleCancelWhileDesembarcando", rollback: true);
        SetCursorState(CursorState.MoveuParado, "HandleCancelWhileDesembarcando", rollback: true);
        RefreshSensorsForCurrentState();
        return ActionSfx.Cancel;
    }
}
