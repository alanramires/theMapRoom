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

        if (unit.IsEmbarked)
            return 0;

        AutonomyData profile = ResolveProfile(unit, database);
        if (profile == null)
            return 0;

        int upkeep = Mathf.Max(0, profile.turnStartUpkeep);
        if (upkeep <= 0)
            return 0;

        if (!profile.AppliesTurnStartUpkeep(unit.GetDomain(), unit.GetHeightLevel()))
            return 0;

        if (profile.isAircraft && IsAircraftLandedOnValidConstruction(unit))
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

    private static bool IsAircraftLandedOnValidConstruction(UnitManager unit)
    {
        if (unit == null || !unit.IsAircraftGrounded)
            return false;

        if (unit.BoardTilemap == null)
            return false;

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(unit.BoardTilemap, cell);
        if (construction == null || !construction.TryResolveConstructionData(out ConstructionData constructionData) || constructionData == null)
            return false;

        if (!constructionData.allowAircraftTakeoffAndLanding)
            return false;

        if (!UnitSatisfiesRequiredSkills(unit, constructionData.requiredLandingSkillRules, constructionData.requireAtLeastOneLandingSkill))
            return false;

        return true;
    }

    private static bool UnitSatisfiesRequiredSkills(
        UnitManager unit,
        System.Collections.Generic.IReadOnlyList<ConstructionLandingSkillRule> requiredSkills,
        bool requireAtLeastOneSkill)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;
        if (unit == null)
            return false;

        if (requireAtLeastOneSkill)
        {
            for (int i = 0; i < requiredSkills.Count; i++)
            {
                ConstructionLandingSkillRule required = requiredSkills[i];
                SkillData requiredSkill = required != null ? required.skill : null;
                if (requiredSkill == null)
                    continue;
                if (unit.HasSkill(requiredSkill))
                    return true;
            }

            return false;
        }

        for (int i = 0; i < requiredSkills.Count; i++)
        {
            ConstructionLandingSkillRule required = requiredSkills[i];
            SkillData requiredSkill = required != null ? required.skill : null;
            if (requiredSkill == null)
                continue;
            if (!unit.HasSkill(requiredSkill))
                return false;
        }

        return true;
    }
}
