using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class TurnStateManager
{
    private Coroutine turnStartRallyExecutionRoutine;

    public bool TryTogglePlanningModeByHotkey()
    {
        TryAutoAssignReferences();
        if (planningManager == null)
            return false;

        if (CurrentCursorState == CursorState.Planning)
        {
            ExitPlanningStateToNeutral(rollback: true);
            return true;
        }

        if (CurrentCursorState != CursorState.Neutral)
            return false;

        if (!planningManager.TryEnterPlanningMode(out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                PanelDialogController.TrySetTransientText(reason, 2.0f);
            return false;
        }

        Advance(CursorState.Planning, "TryTogglePlanningModeByHotkey: enter");
        PanelDialogController.TrySetExternalText("Planning ativo: clique no mapa para destino / unidade. P ou ESC para sair.");
        return true;
    }

    private void ExitPlanningStateToNeutral(bool rollback)
    {
        planningManager?.ExitPlanningMode();
        PanelDialogController.ClearExternalText();
        if (rollback)
            Retreat("ExitPlanningStateToNeutral");
        else
            ExecuteAndReset("ExitPlanningStateToNeutral");
    }

    public bool TryExecuteTurnStartRallyQueueIfIdle()
    {
        TryAutoAssignReferences();
        if (!Application.isPlaying)
            return false;
        if (planningManager == null)
            return false;
        if (turnStartFuelDepletionExecutionInProgress)
            return false;
        if (turnStartRallyExecutionInProgress || turnStartRallyExecutionRoutine != null)
            return false;

        PlayerSlotId slot = matchController != null ? matchController.ActiveSlotId : PlayerSlotId.Invalid;
        if (!planningManager.HasActiveAssignmentsForSlot(slot))
            return false;

        replayManager?.BeginTurnRecording();
        turnStartRallyExecutionRoutine = StartCoroutine(ExecuteTurnStartRallyQueueRoutine());
        return true;
    }

    private IEnumerator ExecuteTurnStartRallyQueueRoutine()
    {
        if (turnStartRallyExecutionInProgress)
            yield break;

        turnStartRallyExecutionInProgress = true;
        EnterTurnStartRallyCursorState();

        try
        {
            PlayerSlotId slot = matchController != null ? matchController.ActiveSlotId : PlayerSlotId.Invalid;
            if (planningManager != null)
                yield return planningManager.ExecuteTurnStartRallyPhase(slot);
        }
        finally
        {
            turnStartRallyExecutionInProgress = false;
            turnStartRallyExecutionRoutine = null;
            ExitTurnStartRallyCursorState();
        }
    }

    private void EnterTurnStartRallyCursorState()
    {
        if (CurrentCursorState == CursorState.TurnStartRallyQueue)
            return;

        Advance(CursorState.TurnStartRallyQueue, "TurnStartRallyQueue: begin");
    }

    private void ExitTurnStartRallyCursorState()
    {
        if (CurrentCursorState != CursorState.TurnStartRallyQueue)
            return;

        ExecuteAndReset("TurnStartRallyQueue: completed");
    }

    public IEnumerator ExecutePlanningMoveOnlyAlongPath(
        UnitManager unit,
        List<Vector3Int> path,
        string debugLabel,
        Action<bool, int> onFinished)
    {
        bool success = false;
        int movedHexes = 0;

        try
        {
            if (unit == null || path == null || path.Count < 2)
                yield break;
            if (CurrentCursorState != CursorState.TurnStartRallyQueue && CurrentCursorState != CursorState.AircraftFuelDepletionQueue)
                yield break;
            if (IsMovementAnimationRunning())
                yield break;

            if (selectedUnit != null)
                ClearSelectionForTurnStartFuelDepletionQueue();

            Vector3Int originCell = path[0];
            originCell.z = 0;
            Vector3Int destinationCell = path[path.Count - 1];
            destinationCell.z = 0;

            cursorController?.SetCell(originCell, playMoveSfx: false, adjustCamera: false);
            cursorController?.TryAdjustCameraToCursor();

            replayManager?.EnsureCurrentUnitActionBuffer(unit, originCell);

            SetSelectedUnit(unit);
            Advance(CursorState.UnitSelected, "ExecutePlanningMoveOnlyAlongPath: select");

            BeginMovementToSelectedCell(path);

            while (animationManager != null && animationManager.IsAnimatingMovement)
                yield return null;

            int guard = 0;
            while (guard++ < 120)
            {
                if (CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado)
                    break;
                yield return null;
            }

            if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
                yield break;

            HandleMoveOnlyActionRequested();

            guard = 0;
            while (guard++ < 180)
            {
                if (CurrentCursorState == CursorState.Neutral)
                    break;
                yield return null;
            }

            Vector3Int postCell = unit.CurrentCellPosition;
            postCell.z = 0;
            movedHexes = Mathf.Max(0, path.Count - 1);
            success = postCell == destinationCell && CurrentCursorState == CursorState.Neutral;
            if (!success)
            {
                replayManager?.DiscardCurrentBuffer("Planning move failed");
                if (CurrentCursorState != CursorState.Neutral)
                    ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: false);
            }
        }
        finally
        {
            onFinished?.Invoke(success, movedHexes);
        }
    }
}

