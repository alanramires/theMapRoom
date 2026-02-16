using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private void PaintSelectedUnitMovementRange()
    {
        ClearMovementRange(keepCommittedMovement: true);
        if (selectedUnit == null)
            return;

        if (rangeMapTilemap == null || terrainTilemap == null)
            return;
        if (rangeOverlayTile == null)
        {
            Debug.LogWarning("[TurnStateManager] Defina 'Range Overlay Tile' (hex simples) para pintar o alcance.");
            return;
        }

        int radius = GetAvailableMovementSteps(selectedUnit);
        if (radius < 0)
            return;

        Color teamColor = TeamUtils.GetColor(selectedUnit.TeamId);
        Color overlayColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(movementRangeAlpha));
        Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
            terrainTilemap,
            selectedUnit,
            radius,
            terrainDatabase);
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in validPaths)
        {
            Vector3Int cell = pair.Key;
            if (terrainTilemap.GetTile(cell) == null)
                continue;

            rangeMapTilemap.SetTile(cell, rangeOverlayTile);
            rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
            rangeMapTilemap.SetColor(cell, overlayColor);
            paintedRangeCells.Add(cell);
            paintedRangeLookup.Add(cell);
            movementPathsByCell[cell] = pair.Value;
        }
    }

    private void ClearMovementRange()
    {
        ClearMovementRange(keepCommittedMovement: false);
    }

    private void ClearMovementRange(bool keepCommittedMovement)
    {
        if (rangeMapTilemap != null)
        {
            for (int i = 0; i < paintedRangeCells.Count; i++)
            {
                Vector3Int cell = paintedRangeCells[i];
                rangeMapTilemap.SetTile(cell, null);
                rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
                rangeMapTilemap.SetColor(cell, Color.white);
            }
        }

        paintedRangeCells.Clear();
        paintedRangeLookup.Clear();
        movementPathsByCell.Clear();

        if (!keepCommittedMovement)
            ClearCommittedMovement();
    }

    private Tilemap FindRangeMapTilemap()
    {
        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map != null && map.name.ToLowerInvariant() == "rangemap")
                return map;
        }

        return null;
    }
}
