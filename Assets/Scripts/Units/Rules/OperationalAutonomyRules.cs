using UnityEngine;

public static class OperationalAutonomyRules
{
    public static bool HasOperationalAutonomy(UnitManager unit)
    {
        if (unit == null)
            return false;

        if (!unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        return data.autonomyData != null;
    }

    public static int ApplyMovementAutonomyCost(UnitManager unit, int baseCost, bool applyModifier)
    {
        int safeBase = Mathf.Max(1, baseCost);
        if (!applyModifier || !HasOperationalAutonomy(unit))
            return safeBase;

        AutonomyData profile = ResolveProfile(unit, database: null);
        if (profile == null)
            return safeBase;

        int multiplier = Mathf.Max(1, profile.movementAutonomyMultiplier);
        return Mathf.Max(1, safeBase * multiplier);
    }

    public static int GetTurnStartAutonomyUpkeep(UnitManager unit, AutonomyDatabase database)
    {
        if (unit == null || !HasOperationalAutonomy(unit))
            return 0;

        AutonomyData profile = ResolveProfile(unit, database);
        if (profile == null)
            return 0;

        int upkeep = Mathf.Max(0, profile.turnStartUpkeep);
        if (upkeep <= 0)
            return 0;

        if (profile.isAircraft)
        {
            HeightLevel height = unit.GetHeightLevel();
            bool inAirLayer = height == HeightLevel.AirLow || height == HeightLevel.AirHigh;
            if (!inAirLayer)
                return 0;
        }

        if (!profile.AppliesTurnStartUpkeep(unit.GetDomain(), unit.GetHeightLevel()))
            return 0;

        return upkeep;
    }

    private static AutonomyData ResolveProfile(UnitManager unit, AutonomyDatabase database)
    {
        if (unit == null)
            return null;

        if (!unit.TryGetUnitData(out UnitData data) || data == null)
            return null;

        AutonomyData assigned = data.autonomyData;
        if (assigned == null)
            return null;

        if (database == null)
            return assigned;

        return database.TryResolve(assigned, out AutonomyData resolved) ? resolved : null;
    }
}
