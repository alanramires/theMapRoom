using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class HexPathResolver
{
    public static bool TryResolveDirectionalFallback(
        Tilemap terrainTilemap,
        HashSet<Vector3Int> allowedCells,
        Vector3Int currentCell,
        Vector3Int primaryTarget,
        out Vector3Int resolvedCell)
    {
        resolvedCell = primaryTarget;
        if (terrainTilemap == null)
            return false;

        Vector3 currentWorld = terrainTilemap.GetCellCenterWorld(currentCell);
        Vector3 targetWorld = terrainTilemap.GetCellCenterWorld(primaryTarget);
        Vector2 desiredDir = (targetWorld - currentWorld);
        if (desiredDir.sqrMagnitude < 0.0001f)
            return false;

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(terrainTilemap, currentCell, neighbors);

        float bestScore = float.NegativeInfinity;
        Vector3Int bestCell = primaryTarget;
        bool found = false;

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int candidate = neighbors[i];
            if (!allowedCells.Contains(candidate))
                continue;

            Vector3 candidateWorld = terrainTilemap.GetCellCenterWorld(candidate);
            Vector2 candidateDir = (candidateWorld - currentWorld);
            float dot = Vector2.Dot(desiredDir.normalized, candidateDir.normalized);
            if (dot <= 0f)
                continue;

            float distPenalty = Vector2.Distance(targetWorld, candidateWorld) * 0.01f;
            float score = dot - distPenalty;
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestCell = candidate;
            found = true;
        }

        if (!found)
            return false;

        resolvedCell = bestCell;
        return true;
    }
}
