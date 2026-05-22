using UnityEngine;

public static class HexOccupancyQuery
{
    public static bool HasEnemyUnitAtCellLayer(UnitManager referenceUnit)
    {
        if (referenceUnit == null)
            return false;

        Vector3Int cell = referenceUnit.CurrentCellPosition;
        cell.z = 0;
        return HasEnemyUnitAtCellLayer(
            cell,
            referenceUnit.TeamId,
            referenceUnit.GetDomain(),
            referenceUnit.GetHeightLevel(),
            referenceUnit);
    }

    public static bool HasEnemyUnitAtCellLayer(
        Vector3Int cell,
        TeamId friendlyTeam,
        Domain domain,
        HeightLevel heightLevel,
        UnitManager ignoredUnit = null)
    {
        cell.z = 0;
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit == ignoredUnit || !unit.gameObject.activeInHierarchy || unit.IsEmbarked || unit.IsDead)
                continue;
            if (unit.TeamId == friendlyTeam)
                continue;
            if (unit.GetDomain() != domain || unit.GetHeightLevel() != heightLevel)
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return true;
        }

        return false;
    }

    public static UnitManager FindUnitAtCell(Vector3Int cell, int preferredTeamId = -1)
    {
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        UnitManager firstMatch = null;
        UnitManager preferredReadyMatch = null;
        UnitManager preferredActedMatch = null;

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked || unit.IsDead)
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
            {
                if (preferredTeamId >= 0 && (int)unit.TeamId == preferredTeamId)
                {
                    if (!unit.HasActed)
                    {
                        if (preferredReadyMatch == null)
                            preferredReadyMatch = unit;
                    }
                    else if (preferredActedMatch == null)
                    {
                        preferredActedMatch = unit;
                    }

                    continue;
                }

                if (firstMatch == null)
                    firstMatch = unit;
            }
        }

        if (preferredReadyMatch != null)
            return preferredReadyMatch;
        if (preferredActedMatch != null)
            return preferredActedMatch;

        return firstMatch;
    }
}
