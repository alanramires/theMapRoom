using UnityEngine;

public partial class AIController
{
    private static bool IsRangedAntiAirFireSupport(UnitManager unit)
    {
        return HasPrimaryRole(unit, UnitRole.Antiaereo);
    }

    private static bool IsCombatantAntiAirFireSupport(UnitManager unit)
    {
        return HasPrimaryRole(unit, UnitRole.AntiaereoCombatente);
    }

    private static bool HasPrimaryRole(UnitManager unit, UnitRole role)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.roles != null
            && data.roles.Count > 0
            && data.roles[0] == role;
    }

    private static bool PassesFireSupportRoleTargetFilter(
        UnitManager attacker,
        UnitManager target)
    {
        if (target == null)
            return false;
        if (!IsRangedAntiAirFireSupport(attacker)
            && !IsCombatantAntiAirFireSupport(attacker))
            return true;
        return target.GetDomain() == Domain.Air;
    }

}
