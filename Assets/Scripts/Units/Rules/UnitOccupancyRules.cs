using UnityEngine;
using UnityEngine.Tilemaps;

public static class UnitOccupancyRules
{
    public static bool IsUnitCellOccupied(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        if (UnitRulesDefinition.IsTotalWarEnabled() && exceptUnit != null)
            return IsUnitCellOccupiedForTeam(referenceTilemap, cell, exceptUnit.TeamId, exceptUnit);

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

    public static bool IsUnitCellOccupiedForTeam(Tilemap referenceTilemap, Vector3Int cell, TeamId teamId, UnitManager exceptUnit = null)
    {
        cell.z = 0;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                continue;
            if (unit.TeamId != teamId)
                continue;

            Vector3Int occupiedCell = unit.BoardTilemap == referenceTilemap
                ? unit.CurrentCellPosition
                : HexCoordinates.WorldToCell(referenceTilemap, unit.transform.position);

            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return true;
        }

        return false;
    }

    public static UnitManager GetUnitAtCell(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        cell.z = 0;

        if (UnitRulesDefinition.IsTotalWarEnabled() && exceptUnit != null)
        {
            // Prioriza retornar bloqueador do mesmo time para evitar empilhamento
            // quando coexistencia entre times diferentes for permitida.
            UnitManager sameTeam = null;
            UnitManager otherTeam = null;

            UnitManager[] totalWarUnits = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < totalWarUnits.Length; i++)
            {
                UnitManager unit = totalWarUnits[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                    continue;

                Vector3Int occupiedCell = unit.BoardTilemap == referenceTilemap
                    ? unit.CurrentCellPosition
                    : HexCoordinates.WorldToCell(referenceTilemap, unit.transform.position);

                occupiedCell.z = 0;
                if (occupiedCell != cell)
                    continue;

                if (unit.TeamId == exceptUnit.TeamId)
                {
                    sameTeam = unit;
                    break;
                }

                if (otherTeam == null)
                    otherTeam = unit;
            }

            if (sameTeam != null)
                return sameTeam;
            if (otherTeam != null)
                return otherTeam;
        }

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
