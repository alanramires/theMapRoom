using UnityEngine;

public partial class TurnStateManager
{
    public bool HandleAutomatedSensorActionRequested(SensorActionType action)
    {
        switch (action)
        {
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