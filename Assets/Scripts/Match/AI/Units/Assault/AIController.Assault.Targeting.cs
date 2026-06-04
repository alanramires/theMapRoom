using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private List<UnitManager> CollectVisibleAssaultEnemies(TeamId aiTeam)
    {
        var enemies = new List<UnitManager>();
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            enemies.Add(enemy);
        }
        return enemies;
    }


    private static BazookaTargetPriority ResolveAssaultTargetPreference(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return BazookaTargetPriority.Tertiary;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return BazookaTargetPriority.Tertiary;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return BazookaTargetPriority.Tertiary;

        return attackerData.ResolveAiTargetPriorityForTargetClass(targetData.unitClass);
    }


    private static float GetAssaultTargetPreferenceScore(BazookaTargetPriority priority)
    {
        switch (priority)
        {
            case BazookaTargetPriority.Primary:
                return 30000f;
            case BazookaTargetPriority.Secondary:
                return 15000f;
            default:
                return 0f;
        }
    }


}
