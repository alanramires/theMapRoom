using UnityEngine;

public partial class TurnStateManager
{
    public bool TryResolveCursorMove(Vector3Int currentCell, Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = currentCell + inputDelta;
        resolvedCell.z = 0;

        if (cursorState != CursorState.UnitSelected)
            return true;

        if (paintedRangeLookup.Count == 0)
            return false;

        if (selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            if (resolvedCell == unitCell)
                return true;
        }

        if (paintedRangeLookup.Contains(resolvedCell))
            return true;

        return HexPathResolver.TryResolveDirectionalFallback(
            terrainTilemap,
            paintedRangeLookup,
            currentCell,
            resolvedCell,
            out resolvedCell);
    }

    private UnitManager FindUnitAtCell(Vector3Int cell)
    {
        return HexOccupancyQuery.FindUnitAtCell(cell);
    }
}
