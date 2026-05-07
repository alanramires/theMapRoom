using UnityEngine;

public partial class AIController
{
    private bool PassesAttackDecision(
        UnitManager attacker,
        UnitManager target,
        Vector3Int attackCell,
        bool defensiveContext,
        out string reason)
    {
        reason = "atkDecision=allow";
        if (attacker == null || target == null)
            return true;

        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return true;
        if (!attackerData.useAttackDecision)
        {
            reason = "atkDecision=off";
            return true;
        }
        if (defensiveContext && attackerData.ignoreAttackDecisionWhenDefending)
        {
            reason = "atkDecision=defIgnore";
            return true;
        }
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return true;
        if (turnStateManager == null
            || turnStateManager.RpsDatabaseRef == null
            || turnStateManager.DpqMatchupDatabaseRef == null
            || turnStateManager.WeaponPriorityDataRef == null)
        {
            reason = "atkDecision=simUnavailable";
            return true;
        }

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        int distance = Mathf.Max(1, Mathf.RoundToInt(SectorManager.HexDistance(attackCell, targetCell)));
        PositionDpqForAttackDecision attackerDpq = ResolveDpqForAttackDecision(attackCell);
        PositionDpqForAttackDecision defenderDpq = ResolveDpqForAttackDecision(targetCell);
        AICombatHpSimulator.AICombatHpResult sim = AICombatHpSimulator.Simulate(
            attackerData,
            targetData,
            Mathf.Max(0, attacker.CurrentHP),
            Mathf.Max(0, target.CurrentHP),
            distance,
            turnStateManager.RpsDatabaseRef,
            turnStateManager.DpqMatchupDatabaseRef,
            turnStateManager.WeaponPriorityDataRef,
            attackerDpq.points,
            defenderDpq.points,
            attackerDpq.defenseBonus,
            defenderDpq.defenseBonus);

        if (!sim.isValid)
        {
            reason = "atkDecision=simInvalid";
            return true;
        }

        int attackerHpBefore = Mathf.Max(0, attacker.CurrentHP);
        int targetHpBefore = Mathf.Max(0, target.CurrentHP);
        int attackerLoss = Mathf.Max(0, attackerHpBefore - sim.attackerHpAfter);
        int targetDamage = Mathf.Max(0, targetHpBefore - sim.defenderHpAfter);
        int attackerLossPct = attackerHpBefore > 0
            ? Mathf.RoundToInt(attackerLoss * 100f / attackerHpBefore)
            : 0;
        int targetDamagePct = Mathf.Max(1, targetData.maxHP) > 0
            ? Mathf.RoundToInt(targetDamage * 100f / Mathf.Max(1, targetData.maxHP))
            : 0;
        int hpLossLimit = Mathf.Clamp(attackerData.attackAcceptHpLossPercent
            + (defensiveContext ? attackerData.defensiveAttackExtraHpLossPercent : 0), 0, 100);
        int eliminationMin = Mathf.Clamp(attackerData.attackEliminationMinPercent, 0, 100);

        string summary = $"atkDecision hp={attackerHpBefore}->{sim.attackerHpAfter} loss={attackerLossPct}%/{hpLossLimit}% dmg={targetDamagePct}%/{eliminationMin}% target={targetHpBefore}->{sim.defenderHpAfter} dpq={attackerDpq.points}/{defenderDpq.points} def={attackerDpq.defenseBonus}/{defenderDpq.defenseBonus} kill={sim.killGuaranteed} survive={sim.attackerSurvives}";

        if (attackerData.attackMustSurvive && !sim.attackerSurvives)
        {
            reason = summary + " BLOCK mustSurvive";
            return false;
        }
        if (targetDamage <= 0)
        {
            reason = summary + " BLOCK noDamage";
            return false;
        }
        if (sim.killGuaranteed)
        {
            reason = summary + " OK kill";
            return true;
        }
        if (targetDamagePct >= eliminationMin && attackerLossPct <= hpLossLimit)
        {
            reason = summary + " OK damage";
            return true;
        }
        if (!attackerData.attackMustSurvive)
        {
            reason = summary + " OK noSurviveReq";
            return true;
        }
        if (attackerLossPct <= hpLossLimit)
        {
            reason = summary + " OK hpLoss";
            return true;
        }

        reason = summary + " BLOCK hpLoss";
        return false;
    }
}
