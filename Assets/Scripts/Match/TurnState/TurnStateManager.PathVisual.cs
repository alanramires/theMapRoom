using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private void DrawCommittedPathVisual(List<Vector3Int> path)
    {
        if (pathManager == null)
            return;

        Tilemap tilemap = terrainTilemap != null ? terrainTilemap : (selectedUnit != null ? selectedUnit.BoardTilemap : null);
        if (tilemap == null)
            return;

        TeamId? teamId = selectedUnit != null ? selectedUnit.TeamId : null;
        pathManager.DrawCommittedPath(path, tilemap, teamId);
    }

    private void ClearCommittedPathVisual()
    {
        pathManager?.ClearCommittedPath();
    }
}
