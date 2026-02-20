using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private void PaintLineOfFireArea(SensorMovementMode movementMode)
    {
        ClearLineOfFireArea();
        if (selectedUnit == null)
            return;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        if (boardMap == null)
            return;
        if (lineOfFireMapTilemap == null)
            return;
        if (lineOfFireOverlayTile == null)
        {
            Debug.LogWarning("[TurnStateManager] Defina 'Line Of Fire Overlay Tile' para pintar a area de linha de tiro valida.");
            return;
        }

        HashSet<Vector3Int> validCells = new HashSet<Vector3Int>();
        PodeMirarSensor.CollectValidFireCells(
            selectedUnit,
            boardMap,
            terrainDatabase,
            movementMode,
            validCells,
            dpqAirHeightConfig,
            IsFogOfWarEnabled());

        Color teamColor = TeamUtils.GetColor(selectedUnit.TeamId);
        Color overlayColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(lineOfFireAlpha));
        foreach (Vector3Int cell in validCells)
        {
            if (paintedLineOfFireLookup.Contains(cell))
                continue;

            lineOfFireMapTilemap.SetTile(cell, lineOfFireOverlayTile);
            lineOfFireMapTilemap.SetTileFlags(cell, TileFlags.None);
            lineOfFireMapTilemap.SetColor(cell, overlayColor);
            paintedLineOfFireCells.Add(cell);
            paintedLineOfFireLookup.Add(cell);
        }
    }

    private void ClearLineOfFireArea()
    {
        if (lineOfFireMapTilemap != null)
        {
            for (int i = 0; i < paintedLineOfFireCells.Count; i++)
            {
                Vector3Int cell = paintedLineOfFireCells[i];
                lineOfFireMapTilemap.SetTile(cell, null);
                lineOfFireMapTilemap.SetTileFlags(cell, TileFlags.None);
                lineOfFireMapTilemap.SetColor(cell, Color.white);
            }
        }

        paintedLineOfFireCells.Clear();
        paintedLineOfFireLookup.Clear();
    }

    private Tilemap FindLineOfFireMapTilemap()
    {
        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            string mapName = map.name.ToLowerInvariant();
            if (mapName == "linhadetiromap" || mapName == "lineoffiremap")
                return map;
        }

        return null;
    }
}
