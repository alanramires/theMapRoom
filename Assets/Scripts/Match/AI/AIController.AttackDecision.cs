using UnityEngine;

public partial class AIController
{
    private readonly struct AIAttackSimulationSummary
    {
        public readonly AICombatHpSimulator.AICombatHpResult result;
        public readonly int attackerHpBefore;
        public readonly int targetHpBefore;
        public readonly int attackerLoss;
        public readonly int targetDamage;
        public readonly int attackerLossPct;
        public readonly int targetDamagePct;

        public AIAttackSimulationSummary(
            AICombatHpSimulator.AICombatHpResult result,
            int attackerHpBefore,
            int targetHpBefore,
            int attackerLoss,
            int targetDamage,
            int attackerLossPct,
            int targetDamagePct)
        {
            this.result = result;
            this.attackerHpBefore = attackerHpBefore;
            this.targetHpBefore = targetHpBefore;
            this.attackerLoss = attackerLoss;
            this.targetDamage = targetDamage;
            this.attackerLossPct = attackerLossPct;
            this.targetDamagePct = targetDamagePct;
        }
    }

    private bool TrySimulateAttackForAI(
        UnitManager attacker,
        UnitManager target,
        Vector3Int attackCell,
        out AIAttackSimulationSummary summary)
    {
        summary = default;
        if (attacker == null || target == null)
            return false;

        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return false;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return false;
        if (turnStateManager == null
            || turnStateManager.RpsDatabaseRef == null
            || turnStateManager.DpqMatchupDatabaseRef == null
            || turnStateManager.WeaponPriorityDataRef == null)
            return false;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        int distance = Mathf.Max(1, Mathf.RoundToInt(SectorManager.HexDistance(attackCell, targetCell)));
        PositionDpqForAttackDecision attackerDpq = ResolveDpqForAttackDecision(attacker, attackCell);
        PositionDpqForAttackDecision defenderDpq = ResolveDpqForAttackDecision(target, targetCell);
        AICombatHpSimulator.AICombatHpResult sim;
        if (TryFindAttackDecisionOption(attacker, target, attackCell, out PodeMirarTargetOption option))
        {
            WeaponData counterWeapon = option.defenderCanCounterAttack
                ? option.defenderCounterWeapon
                : null;
            sim = AICombatHpSimulator.SimulateWithWeapons(
                attackerData,
                targetData,
                option.weapon,
                counterWeapon,
                Mathf.Max(0, attacker.CurrentHP),
                Mathf.Max(0, target.CurrentHP),
                turnStateManager.RpsDatabaseRef,
                turnStateManager.DpqMatchupDatabaseRef,
                attackerDpq.points,
                defenderDpq.points,
                attackerDpq.defenseBonus,
                defenderDpq.defenseBonus,
                attacker.IsAircraftGrounded,
                target.IsAircraftGrounded);
        }
        else
        {
            sim = AICombatHpSimulator.Simulate(
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
        }

        if (!sim.isValid)
            return false;

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

        summary = new AIAttackSimulationSummary(
            sim,
            attackerHpBefore,
            targetHpBefore,
            attackerLoss,
            targetDamage,
            attackerLossPct,
            targetDamagePct);
        return true;
    }

    private bool TryFindAttackDecisionOption(
        UnitManager attacker,
        UnitManager target,
        Vector3Int attackCell,
        out PodeMirarTargetOption option)
    {
        option = null;
        if (attacker == null || target == null)
            return false;

        Vector3Int fromCell = attacker.CurrentCellPosition;
        fromCell.z = 0;
        attackCell.z = 0;
        SensorMovementMode mode = attackCell != fromCell
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;

        var targets = new System.Collections.Generic.List<PodeMirarTargetOption>();
        bool hasAny = PodeMirarSensor.CollectTargets(
            attacker,
            boardTilemap,
            terrainDatabase,
            mode,
            targets,
            weaponPriorityData: turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null,
            dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
            fromCell: attackCell);

        if (!hasAny || targets.Count == 0)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            PodeMirarTargetOption candidate = targets[i];
            if (candidate == null || candidate.targetUnit != target)
                continue;

            option = candidate;
            return option.weapon != null;
        }

        return false;
    }

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

        PositionDpqForAttackDecision attackerDpq = ResolveDpqForAttackDecision(attacker, attackCell);
        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        PositionDpqForAttackDecision defenderDpq = ResolveDpqForAttackDecision(target, targetCell);
        if (!TrySimulateAttackForAI(attacker, target, attackCell, out AIAttackSimulationSummary simSummary))
        {
            reason = "atkDecision=simInvalid";
            return true;
        }

        AICombatHpSimulator.AICombatHpResult sim = simSummary.result;
        int attackerHpBefore = simSummary.attackerHpBefore;
        int targetHpBefore = simSummary.targetHpBefore;
        int attackerLossPct = simSummary.attackerLossPct;
        int targetDamage = simSummary.targetDamage;
        int targetDamagePct = simSummary.targetDamagePct;
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
