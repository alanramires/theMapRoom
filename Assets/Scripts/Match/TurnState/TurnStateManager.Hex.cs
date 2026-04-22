using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    public bool TryResolveCursorMove(Vector3Int currentCell, Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = currentCell + inputDelta;
        resolvedCell.z = 0;

        if (IsBoardCursorMovementLockedByCurrentState())
        {
            resolvedCell = currentCell;
            return false;
        }

        if (IsInspectingState())
        {
            if (resolvedCell != currentCell)
                ExitInspectStateToNeutral();
            return true;
        }

        if (CurrentCursorState == CursorState.ShoppingAndServices)
        {
            TryResolveShoppingCursorMove(currentCell, inputDelta);
            resolvedCell = currentCell;
            return false;
        }

        if (CurrentCursorState == CursorState.Capturando || CurrentCursorState == CursorState.CapturandoExecuting)
        {
            resolvedCell = currentCell;
            return false;
        }

        if (CurrentCursorState == CursorState.AircraftFuelDepletionQueue || CurrentCursorState == CursorState.TurnStartRallyQueue)
        {
            resolvedCell = currentCell;
            return false;
        }

        if (CurrentCursorState == CursorState.Mirando)
            return TryResolveMirandoCursorMove(inputDelta, out resolvedCell);

        if (CurrentCursorState == CursorState.Desembarcando)
            return TryResolveDisembarkCursorMove(currentCell, inputDelta, out resolvedCell);

        if (CurrentCursorState == CursorState.Fundindo)
            return TryResolveMergeCursorMove(currentCell, inputDelta, out resolvedCell);

        if (CurrentCursorState == CursorState.Suprindo)
            return TryResolveSupplyCursorMove(currentCell, inputDelta, out resolvedCell);

        if (CurrentCursorState == CursorState.Pousando)
        {
            if (IsLandingPromptActive())
                return TryResolveLandingCursorMove(inputDelta, out resolvedCell);
            return false;
        }

        if (CurrentCursorState == CursorState.Embarcando)
        {
            if (IsEmbarkPromptActive())
                return TryResolveEmbarkCursorMove(inputDelta, out resolvedCell);
            return false;
        }

        if (CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado)
        {
            if (IsTransferSelectionStepActive())
                return TryResolveTransferCursorMove(currentCell, inputDelta, out resolvedCell);

            if (IsLandingPromptActive())
                return TryResolveLandingCursorMove(inputDelta, out resolvedCell);

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

        if (CurrentCursorState != CursorState.UnitSelected)
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

    private bool IsBoardCursorMovementLockedByCurrentState()
    {
        switch (CurrentCursorState)
        {
            case CursorState.PlayerMenu:
            case CursorState.CommandService:
            case CursorState.CommandServiceExecuting:
            case CursorState.RemovingUnit:
            case CursorState.RemovingUnitExecuting:
            case CursorState.EndingTurn:
            case CursorState.EndingTurnExecuting:
            case CursorState.Saving:
            case CursorState.Loading:
            case CursorState.SuprindoExecuting:
            case CursorState.FundindoExecuting:
            case CursorState.DesembarcandoExecuting:
            case CursorState.EmbarcandoExecuting:
            case CursorState.AttackingExecuting:
                return true;
            default:
                return false;
        }
    }

    private UnitManager FindUnitAtCell(Vector3Int cell)
    {
        int preferredTeamId = matchController != null ? matchController.ActiveTeamId : -1;
        UnitManager unit = HexOccupancyQuery.FindUnitAtCell(cell, preferredTeamId);
        if (unit == null)
            return null;

        if (matchController != null && !matchController.IsUnitVisibleForActiveTeam(unit))
            return null;

        return unit;
    }

    private ConstructionManager FindConstructionAtCell(Vector3Int cell)
    {
        Tilemap referenceTilemap = terrainTilemap != null
            ? terrainTilemap
            : (cursorController != null ? cursorController.BoardTilemap : null);

        if (referenceTilemap == null)
            return null;

        return ConstructionOccupancyRules.GetConstructionAtCell(referenceTilemap, cell);
    }

    private void SortClockwiseAroundUnit<T>(List<T> list, System.Func<T, Vector3Int> getCell, UnitManager unit)
    {
        if (list.Count <= 1 || terrainTilemap == null || unit == null)
            return;
        Vector3Int centerCell = unit.CurrentCellPosition;
        centerCell.z = 0;
        Vector3 worldCenter = HexCoordinates.GetCellCenterWorld(terrainTilemap, centerCell);
        list.Sort((a, b) =>
        {
            Vector3Int ca = getCell(a); ca.z = 0;
            Vector3Int cb = getCell(b); cb.z = 0;
            Vector3 wa = HexCoordinates.GetCellCenterWorld(terrainTilemap, ca);
            Vector3 wb = HexCoordinates.GetCellCenterWorld(terrainTilemap, cb);
            float fa = Mathf.Atan2(wa.x - worldCenter.x, wa.y - worldCenter.y);
            float fb = Mathf.Atan2(wb.x - worldCenter.x, wb.y - worldCenter.y);
            if (fa < 0) fa += 2 * Mathf.PI;
            if (fb < 0) fb += 2 * Mathf.PI;
            return fa.CompareTo(fb);
        });
    }
}


