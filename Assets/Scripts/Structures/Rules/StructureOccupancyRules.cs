using UnityEngine;
using UnityEngine.Tilemaps;

public static class StructureOccupancyRules
{
    public static StructureData GetStructureAtCell(Tilemap referenceTilemap, Vector3Int cell)
    {
        cell.z = 0;

        RoadNetworkManager[] networks = Object.FindObjectsByType<RoadNetworkManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < networks.Length; i++)
        {
            RoadNetworkManager network = networks[i];
            if (network == null || !network.gameObject.activeInHierarchy)
                continue;

            Tilemap networkTilemap = network.BoardTilemap;
            if (!IsCompatibleReference(referenceTilemap, networkTilemap))
                continue;

            if (network.TryGetStructureAtCell(cell, out StructureData structure) && structure != null)
                return structure;
        }

        return null;
    }

    private static bool IsCompatibleReference(Tilemap referenceTilemap, Tilemap networkTilemap)
    {
        if (referenceTilemap == null || networkTilemap == null)
            return true;

        if (referenceTilemap == networkTilemap)
            return true;

        GridLayout referenceGrid = referenceTilemap.layoutGrid;
        GridLayout networkGrid = networkTilemap.layoutGrid;
        if (referenceGrid != null && networkGrid != null && referenceGrid == networkGrid)
            return true;

        return false;
    }
}
