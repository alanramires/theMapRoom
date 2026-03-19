using System;
using UnityEngine;

[Serializable]
public class MoveUnitReplayCommand : IReplayCommand
{
    public string UnitInstanceId;
    public Vector3Int OriginCell;
    public Vector3Int TargetCell;
    public UnitLayerMode LayerBefore;
    public UnitLayerMode LayerAfter;
    public int FuelBefore;
    public int FuelAfter;
    public string debugLabel;

    public string DebugLabel => string.IsNullOrWhiteSpace(debugLabel)
        ? $"Move Unit {UnitInstanceId} from ({OriginCell.x},{OriginCell.y}) to ({TargetCell.x},{TargetCell.y}) layer {LayerBefore.domain}/{LayerBefore.heightLevel}->{LayerAfter.domain}/{LayerAfter.heightLevel}"
        : debugLabel;

    public ReplayStepType StepType => ReplayStepType.MoveUnit;

    public void Execute(ReplayExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(UnitInstanceId))
            return;

        if (!int.TryParse(UnitInstanceId, out int parsedId))
            return;

        UnitManager[] units = UnityEngine.Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        UnitManager target = null;
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;

            if (unit.InstanceId == parsedId)
            {
                target = unit;
                break;
            }
        }

        if (target == null)
            return;

        if (!target.gameObject.activeSelf)
            target.gameObject.SetActive(true);

        Vector3Int resolvedTarget = TargetCell;
        resolvedTarget.z = 0;
        target.SetCurrentCellPosition(resolvedTarget, enforceFinalOccupancyRule: false);
        if (target.BoardTilemap != null)
            target.SetCurrentPosition(HexCoordinates.GetCellCenterWorld(target.BoardTilemap, resolvedTarget));

        target.TrySetCurrentLayerMode(LayerAfter.domain, LayerAfter.heightLevel);
        target.SetCurrentFuel(FuelAfter);
        CursorController cursor = UnityEngine.Object.FindAnyObjectByType<CursorController>();
        cursor?.SetCell(resolvedTarget, playMoveSfx: false, adjustCamera: true);

    }
}

