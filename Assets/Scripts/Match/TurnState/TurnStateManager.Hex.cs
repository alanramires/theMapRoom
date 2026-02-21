using UnityEngine;

public partial class TurnStateManager
{
    public bool TryResolveCursorMove(Vector3Int currentCell, Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = currentCell + inputDelta;
        resolvedCell.z = 0;

        if (cursorState == CursorState.Mirando)
            return TryResolveMirandoCursorMove(inputDelta, out resolvedCell);

        if (cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado)
        {
            if (IsEmbarkPromptActive())
                return TryResolveEmbarkCursorMove(inputDelta, out resolvedCell);

            if (TryResolveEmbarkCursorMove(inputDelta, out resolvedCell))
                return true;

            if (selectedUnit == null)
                return false;

            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;

            // Mantem o cursor ancorado na unidade; evita deslocamento livre nesses estados.
            if (currentCell != unitCell)
            {
                resolvedCell = unitCell;
                return true;
            }

            return false;
        }

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
