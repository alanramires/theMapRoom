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
            if (enemy.SlotIndex == ResolveAISlotKey(aiTeam) || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForSlot(enemy, PlayerSlotId.FromIndex(currentAISlotIndex))) continue;
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
                // Primary is a doctrinal target, not a small tie-break. It must beat
                // objective occupancy bonuses so anti-artillery tanks remove the gun
                // that would otherwise fire on them after the current engagement.
                return 150000f;
            case BazookaTargetPriority.Secondary:
                return 40000f;
            default:
                return 0f;
        }
    }

    private static bool IsBetterAssaultTargetCandidate(
        BazookaTargetPriority preference,
        int eliteLevel,
        float tacticalScore,
        UnitManager currentBest,
        BazookaTargetPriority bestPreference,
        int bestEliteLevel,
        float bestTacticalScore)
    {
        if (currentBest == null)
            return true;
        if (preference != bestPreference)
            return preference > bestPreference;
        if (eliteLevel != bestEliteLevel)
            return eliteLevel > bestEliteLevel;
        return tacticalScore > bestTacticalScore;
    }


}
