using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class TurnStateManager
{
    public bool HandleAutomatedSensorActionRequested(SensorActionType action)
    {
        switch (action)
        {
            case SensorActionType.None:
                return HandleAutomatedMoveOnlyActionRequested();
            case SensorActionType.Attack:
                HandleAimActionRequested();
                return true;
            case SensorActionType.Embark:
                HandleEmbarkActionRequested();
                return true;
            case SensorActionType.Disembark:
                HandleDisembarkActionRequested();
                return true;
            case SensorActionType.Capture:
                HandleCaptureActionRequested();
                return true;
            case SensorActionType.Merge:
                HandleMergeActionRequested();
                return true;
            case SensorActionType.Supply:
                HandleSupplyActionRequested();
                return true;
            case SensorActionType.Transfer:
                HandleTransferActionRequested();
                return true;
            case SensorActionType.Land:
                HandleLandingSensorRequested();
                return true;
            case SensorActionType.CommandService:
                return HandleAutomatedCommandServiceRequested();
            case SensorActionType.RemoveUnit:
                return HandleAutomatedRemoveUnitRequested();
            case SensorActionType.Shopping:
                return false;
            default:
                return false;
        }
    }

    public bool HandleAutomatedMoveOnlyActionRequested()
    {
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return false;

        HandleMoveOnlyActionRequested();
        return cursorState == CursorState.Neutral;
    }

    public bool TryAutomatedSelectUnitAndEnterMoveuParado(UnitManager unit)
    {
        if (unit == null || cursorController == null)
            return false;
        if (cursorState != CursorState.Neutral)
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        cursorController.SetCell(unitCell, playMoveSfx: false);

        // Confirm #1: seleciona unidade aliada.
        HandleConfirm();
        if (selectedUnit != unit)
            return false;

        // Confirm #2 no mesmo hex: entra em MoveuParado (sensores habilitados).
        cursorController.SetCell(unitCell, playMoveSfx: false);
        HandleConfirm();
        return cursorState == CursorState.MoveuParado || cursorState == CursorState.MoveuAndando;
    }

    public bool TryExecuteAutomatedAttackFirstTarget()
    {
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return false;
        if (!HandleAutomatedSensorActionRequested(SensorActionType.Attack))
            return false;

        if (cachedPodeMirarTargets == null || cachedPodeMirarTargets.Count <= 0)
        {
            HandleCancel();
            return false;
        }

        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[i];
            if (option == null || option.targetUnit == null)
                continue;

            UnitManager target = option.targetUnit;
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            if (TryExecuteAutomatedAttackReplayTarget(target.InstanceId.ToString(), targetCell))
                return true;
        }

        HandleCancel();
        return false;
    }

    public bool HasAutomatedAttackAvailable()
    {
        return availableSensorActionCodes != null && availableSensorActionCodes.Contains('A');
    }

    public bool HasAutomatedMoveAvailable()
    {
        return cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado;
    }

    public IEnumerator WaitUntilAutomatedNeutralReady(float timeoutSeconds)
    {
        float endTime = Time.time + Mathf.Max(0.2f, timeoutSeconds);
        while (Time.time < endTime)
        {
            if (cursorState == CursorState.Neutral && !IsScannerActionExecutionInProgress && !IsMovementAnimationRunning())
                yield break;

            yield return null;
        }
    }

    public IEnumerator MoveCursorToCellWithAutomatedTravel(Vector3Int targetCell, float stepDelay = -1f)
    {
        float resolvedStepDelay = stepDelay >= 0f
            ? stepDelay
            : (replayManager != null
                ? Mathf.Max(0f, replayManager.GetEffectiveCursorTravelStepDelayForRuntimeMotion())
                : 0.08f);

        yield return MoveCursorToCellLikeReplayAtTurnStart(targetCell, resolvedStepDelay);
    }

    public float GetAutomatedPreSelectDelay()
    {
        return animationManager != null ? animationManager.TurnStartFuelDeathCursorFocusDelay : 0.20f;
    }

    public float GetAutomatedBetweenUnitsDelay()
    {
        return animationManager != null ? animationManager.TurnStartFuelDeathBetweenKillsDelay : 0.15f;
    }

    public bool HandleAutomatedCommandServiceRequested()
    {
        if (cursorState != CursorState.Neutral)
            return false;

        TryCloseThreatLayerHotzone();
        if (!TryPreviewCommandServiceOrder(out _, emitLogs: false))
            return false;

        SetCursorState(CursorState.CommandService, "HandleAutomatedCommandServiceRequested");
        return true;
    }

    public bool HandleAutomatedRemoveUnitRequested()
    {
        if (cursorState != CursorState.Neutral)
            return false;

        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out _))
            return false;

        string targetName = ResolveDebugUnitName(target);
        PanelDialogController.TrySetExternalText($"Destroy Unit :: {targetName} {FormatMapCellWithZ(cursorCell)} :: Confirm");
        SetCursorState(CursorState.RemovingUnit, "HandleAutomatedRemoveUnitRequested");
        return true;
    }
}
