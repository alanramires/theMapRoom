using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class HexCoordinates
{
    public static Vector3 GetCellCenterWorld(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null)
            return Vector3.zero;

        return tilemap.GetCellCenterWorld(cell);
    }

    public static Vector3Int WorldToCell(Tilemap tilemap, Vector3 world)
    {
        if (tilemap == null)
            return Vector3Int.zero;

        return tilemap.WorldToCell(world);
    }

    // Retorna true se cellB estiver a no maximo maxRange passos hexagonais de cellA.
    // Usa BFS com GetImmediateHexNeighbors para respeitar o layout real do tilemap.
    public static bool IsWithinRange(Tilemap tilemap, Vector3Int cellA, Vector3Int cellB, int maxRange)
    {
        if (tilemap == null || maxRange < 0)
            return false;

        cellA.z = 0;
        cellB.z = 0;

        if (cellA == cellB)
            return true;

        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { cellA };
        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        Queue<int> steps = new Queue<int>();
        frontier.Enqueue(cellA);
        steps.Enqueue(0);

        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int currentSteps = steps.Dequeue();

            if (currentSteps >= maxRange)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int n = neighbors[i];
                n.z = 0;
                if (visited.Contains(n))
                    continue;
                visited.Add(n);
                if (n == cellB)
                    return true;
                frontier.Enqueue(n);
                steps.Enqueue(currentSteps + 1);
            }
        }

        return false;
    }
}
