using UnityEngine;

public static class HexOccupancyQuery
{
    public static UnitManager FindUnitAtCell(Vector3Int cell, int preferredTeamId = -1)
    {
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        UnitManager firstMatch = null;
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
            {
                if (preferredTeamId >= 0 && (int)unit.TeamId == preferredTeamId)
                    return unit;

                if (firstMatch == null)
                    firstMatch = unit;
            }
        }

        return firstMatch;
    }
}
