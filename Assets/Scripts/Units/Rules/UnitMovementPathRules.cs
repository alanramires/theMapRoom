using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class UnitMovementPathRules
{
    public static Dictionary<Vector3Int, List<Vector3Int>> CalcularCaminhosValidos(
        Tilemap terrainTilemap,
        UnitManager unit,
        int maxSteps,
        TerrainDatabase terrainDatabase = null)
    {
        Dictionary<Vector3Int, List<Vector3Int>> pathsByDestination = new Dictionary<Vector3Int, List<Vector3Int>>();
        if (terrainTilemap == null || unit == null || maxSteps < 0)
            return pathsByDestination;

        // Garante que o estado de camada usado na validacao vem da instancia em campo.
        unit.SyncLayerStateFromData(forceNativeDefault: false);

        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;
        if (terrainTilemap.GetTile(origin) == null)
            return pathsByDestination;

        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        Dictionary<Vector3Int, int> distance = new Dictionary<Vector3Int, int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        frontier.Enqueue(origin);
        distance[origin] = 0;
        cameFrom[origin] = origin;

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int nextStep = distance[current] + 1;
            if (nextStep > maxSteps)
                continue;

            GetImmediateHexNeighbors(terrainTilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                if (distance.ContainsKey(next))
                    continue;
                TileBase nextTile = terrainTilemap.GetTile(next);
                if (nextTile == null)
                    continue;
                ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(terrainTilemap, next);
                if (!CanTraverseCell(terrainDatabase, nextTile, construction, unit))
                    continue;

                UnitManager blocker = UnitOccupancyRules.GetUnitAtCell(terrainTilemap, next, unit);
                if (blocker != null)
                    blocker.SyncLayerStateFromData(forceNativeDefault: false);
                if (!UnitRulesDefinition.CanPassThrough(unit, blocker))
                    continue;

                distance[next] = nextStep;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        foreach (KeyValuePair<Vector3Int, int> pair in distance)
            pathsByDestination[pair.Key] = BuildPath(origin, pair.Key, cameFrom);

        return pathsByDestination;
    }

    private static bool CanTraverseCell(
        TerrainDatabase terrainDatabase,
        TileBase terrainTile,
        ConstructionManager construction,
        UnitManager unit)
    {
        if (unit == null || terrainTile == null)
            return false;

        Domain currentDomain = unit.GetDomain();
        HeightLevel currentHeight = unit.GetHeightLevel();
        if (construction != null)
            return CanTraverseUsingConstruction(construction, unit, currentDomain, currentHeight);

        if (terrainDatabase == null)
            return true;
        if (!terrainDatabase.TryGetByPaletteTile(terrainTile, out TerrainTypeData terrainData) || terrainData == null)
            return false;

        return CanTraverseUsingTerrain(terrainData, unit, currentDomain, currentHeight);
    }

    private static bool CanTraverseUsingConstruction(
        ConstructionManager construction,
        UnitManager unit,
        Domain currentDomain,
        HeightLevel currentHeight)
    {
        if (construction == null || unit == null)
            return false;

        if (currentDomain == Domain.Air)
        {
            if (construction.AllowsAirDomain())
                return true;
            return construction.SupportsLayerMode(currentDomain, currentHeight);
        }

        IReadOnlyList<UnitLayerMode> unitModes = unit.GetAllLayerModes();
        for (int i = 0; i < unitModes.Count; i++)
        {
            UnitLayerMode mode = unitModes[i];
            if (construction.SupportsLayerMode(mode.domain, mode.heightLevel))
                return true;
        }

        return false;
    }

    private static bool CanTraverseUsingTerrain(
        TerrainTypeData terrainData,
        UnitManager unit,
        Domain currentDomain,
        HeightLevel currentHeight)
    {
        if (terrainData == null || unit == null)
            return false;

        if (currentDomain == Domain.Air)
        {
            if (terrainData.alwaysAllowAirDomain)
                return true;
            return TerrainSupportsMode(terrainData, currentDomain, currentHeight);
        }

        IReadOnlyList<UnitLayerMode> unitModes = unit.GetAllLayerModes();
        for (int i = 0; i < unitModes.Count; i++)
        {
            UnitLayerMode mode = unitModes[i];
            if (TerrainSupportsMode(terrainData, mode.domain, mode.heightLevel))
                return true;
        }

        return false;
    }

    private static bool TerrainSupportsMode(TerrainTypeData terrainData, Domain domain, HeightLevel heightLevel)
    {
        if (terrainData == null)
            return false;

        if (terrainData.domain == domain && terrainData.heightLevel == heightLevel)
            return true;

        if (terrainData.additionalLayerModes == null)
            return false;

        for (int i = 0; i < terrainData.additionalLayerModes.Count; i++)
        {
            TerrainLayerMode mode = terrainData.additionalLayerModes[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    public static void GetImmediateHexNeighbors(Tilemap terrainTilemap, Vector3Int cell, List<Vector3Int> output)
    {
        output.Clear();
        if (terrainTilemap == null)
            return;

        Vector3 centerWorld = terrainTilemap.GetCellCenterWorld(cell);
        List<CellDistance> candidates = new List<CellDistance>(24);

        // Busca local para capturar os 6 vizinhos de um hex, respeitando o offset real do Tilemap.
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                Vector3Int candidate = new Vector3Int(cell.x + dx, cell.y + dy, 0);
                Vector3 world = terrainTilemap.GetCellCenterWorld(candidate);
                float distance = Vector2.Distance(centerWorld, world);
                if (distance <= 0.0001f)
                    continue;

                candidates.Add(new CellDistance(candidate, distance));
            }
        }

        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

        int count = Mathf.Min(6, candidates.Count);
        for (int i = 0; i < count; i++)
            output.Add(candidates[i].cell);
    }

    private static List<Vector3Int> BuildPath(Vector3Int origin, Vector3Int destination, Dictionary<Vector3Int, Vector3Int> cameFrom)
    {
        List<Vector3Int> reversedPath = new List<Vector3Int>();
        if (!cameFrom.ContainsKey(destination))
            return reversedPath;

        Vector3Int current = destination;
        reversedPath.Add(current);

        while (current != origin)
        {
            current = cameFrom[current];
            reversedPath.Add(current);
        }

        reversedPath.Reverse();
        return reversedPath;
    }

    private struct CellDistance
    {
        public readonly Vector3Int cell;
        public readonly float distance;

        public CellDistance(Vector3Int cell, float distance)
        {
            this.cell = cell;
            this.distance = distance;
        }
    }
}
