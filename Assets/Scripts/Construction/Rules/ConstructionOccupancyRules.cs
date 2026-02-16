using UnityEngine;
using UnityEngine.Tilemaps;

public static class ConstructionOccupancyRules
{
    public static ConstructionManager GetConstructionAtCell(Tilemap referenceTilemap, Vector3Int cell)
    {
        cell.z = 0;

        ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;

            Vector3Int occupiedCell = construction.BoardTilemap == referenceTilemap
                ? construction.CurrentCellPosition
                : HexCoordinates.WorldToCell(referenceTilemap, construction.transform.position);

            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return construction;
        }

        return null;
    }
}
