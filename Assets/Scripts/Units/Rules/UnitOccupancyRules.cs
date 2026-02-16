using UnityEngine;
using UnityEngine.Tilemaps;

public static class UnitOccupancyRules
{
    public static bool IsUnitCellOccupied(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        cell.z = 0;
        int count = 0;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                continue;

            Vector3Int occupiedCell = unit.BoardTilemap == referenceTilemap
                ? unit.CurrentCellPosition
                : HexCoordinates.WorldToCell(referenceTilemap, unit.transform.position);

            occupiedCell.z = 0;
            if (occupiedCell != cell)
                continue;

            count++;
            if (count >= UnitRulesDefinition.MaxUnitsPerHex)
                return true;
        }

        return false;
    }

    public static UnitManager GetUnitAtCell(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        cell.z = 0;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                continue;

            Vector3Int occupiedCell = unit.BoardTilemap == referenceTilemap
                ? unit.CurrentCellPosition
                : HexCoordinates.WorldToCell(referenceTilemap, unit.transform.position);

            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return unit;
        }

        return null;
    }
}
