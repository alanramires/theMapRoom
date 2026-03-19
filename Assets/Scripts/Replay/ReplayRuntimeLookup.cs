using UnityEngine;

public static class ReplayRuntimeLookup
{
    public static UnitManager FindUnitByInstanceId(string rawInstanceId)
    {
        if (string.IsNullOrWhiteSpace(rawInstanceId))
            return null;
        if (!int.TryParse(rawInstanceId, out int id))
            return null;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit != null && unit.InstanceId == id)
                return unit;
        }

        return null;
    }

    public static ConstructionManager FindConstructionByInstanceId(string rawInstanceId)
    {
        if (string.IsNullOrWhiteSpace(rawInstanceId))
            return null;
        if (!int.TryParse(rawInstanceId, out int id))
            return null;

        ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction != null && construction.InstanceId == id)
                return construction;
        }

        return null;
    }

    public static ConstructionManager FindConstructionById(string constructionId)
    {
        if (string.IsNullOrWhiteSpace(constructionId))
            return null;

        ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
                continue;

            if (string.Equals(construction.ConstructionId, constructionId, System.StringComparison.OrdinalIgnoreCase))
                return construction;
        }

        return null;
    }
}
