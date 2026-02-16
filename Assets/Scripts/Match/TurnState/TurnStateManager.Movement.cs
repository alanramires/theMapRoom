using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private void BeginMovementToSelectedCell(List<Vector3Int> path)
    {
        if (selectedUnit == null || path == null || path.Count < 2)
            return;

        committedMovementPath.Clear();
        committedMovementPath.AddRange(path);
        committedOriginCell = path[0];
        committedDestinationCell = path[path.Count - 1];
        hasCommittedMovement = true;

        if (animationManager == null)
            return;

        Tilemap movementTilemap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        List<Vector3Int> walkedTrail = new List<Vector3Int> { path[0] };
        DrawCommittedPathVisual(walkedTrail);
        animationManager.PlayMovement(
            selectedUnit,
            movementTilemap,
            path,
            playStartSfx: true,
            onAnimationStart: () => PlayMovementStartSfx(selectedUnit),
            onAnimationFinished: () => HandleMovementAnimationCompleted(CursorState.MoveuAndando),
            onCellReached: cell =>
            {
                walkedTrail.Add(cell);
                DrawCommittedPathVisual(walkedTrail);
            });
    }

    private void BeginRollbackToSelection()
    {
        if (selectedUnit == null || !TryGetCommittedMovementPath(out List<Vector3Int> committedPath, out Vector3Int originCell, out Vector3Int destinationCell))
            return;

        Vector3Int currentCell = selectedUnit.CurrentCellPosition;
        currentCell.z = 0;
        if (currentCell != destinationCell)
            return;

        List<Vector3Int> reversePath = new List<Vector3Int>(committedPath);
        reversePath.Reverse();
        reversePath[reversePath.Count - 1] = originCell;

        if (animationManager == null)
            return;

        Tilemap movementTilemap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        ClearCommittedPathVisual();
        animationManager.PlayMovement(
            selectedUnit,
            movementTilemap,
            reversePath,
            playStartSfx: false,
            onAnimationStart: null,
            onAnimationFinished: () => HandleMovementAnimationCompleted(CursorState.UnitSelected),
            onCellReached: null);
    }

    private void HandleMovementAnimationCompleted(CursorState onCompleteState)
    {
        if (selectedUnit != null)
            animationManager?.ApplySelectionVisual(selectedUnit);

        if (onCompleteState == CursorState.UnitSelected)
            ClearCommittedMovement();
        else if (onCompleteState == CursorState.MoveuAndando)
        {
            PrepareFuelCostForCommittedPath();
            DrawCommittedPathVisual(committedMovementPath);
            Debug.Log($"moveu para {committedDestinationCell.x},{committedDestinationCell.y}");
        }

        cursorState = onCompleteState;
    }

    private void PlayMovementStartSfx(UnitManager unit)
    {
        if (unit == null || cursorController == null)
            return;

        cursorController.PlayUnitMovementSfx(unit.GetMovementCategory());
    }
}
