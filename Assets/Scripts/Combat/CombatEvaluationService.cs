using UnityEngine;

public enum CombatWeaponSelectionMode
{
    None = 0,
    CanonicalSensorOption = 1,
    LegacyAutomaticFallback = 2
}

public sealed class CombatEvaluationRequest
{
    public UnitManager Attacker;
    public UnitManager Target;
    public Vector3Int AttackCell;
    public PodeMirarTargetOption SensorOption;
    public RPSDatabase RpsDatabase;
    public DPQMatchupDatabase DpqMatchupDatabase;
    public WeaponPriorityData WeaponPriorityData;
    public PositionDpqResult AttackerDpq;
    public PositionDpqResult DefenderDpq;
    public bool DefensiveContext;

    // Compatibility path for existing AI callers. MelhorCombate must keep this
    // false and evaluate only the canonical option returned by PodeMirar.
    public bool AllowLegacyAutomaticWeaponFallback;
}

public readonly struct CombatEvaluationResult
{
    public static CombatEvaluationResult Invalid => default;

    public bool IsValid { get; }
    public AICombatHpSimulator.AICombatHpResult Simulation { get; }
    public CombatWeaponSelectionMode WeaponSelectionMode { get; }
    public PodeMirarTargetOption SensorOption { get; }
    public WeaponData AttackWeapon { get; }
    public int AttackWeaponIndex { get; }
    public WeaponData CounterWeapon { get; }
    public int CounterWeaponIndex { get; }
    public int Distance { get; }
    public int AttackerHpBefore { get; }
    public int TargetHpBefore { get; }
    public int AttackerLoss { get; }
    public int TargetDamage { get; }
    public int AttackerLossPercent { get; }
    public int TargetDamagePercent { get; }
    public PositionDpqResult AttackerDpq { get; }
    public PositionDpqResult DefenderDpq { get; }

    public CombatEvaluationResult(
        AICombatHpSimulator.AICombatHpResult simulation,
        CombatWeaponSelectionMode weaponSelectionMode,
        PodeMirarTargetOption sensorOption,
        WeaponData attackWeapon,
        int attackWeaponIndex,
        WeaponData counterWeapon,
        int counterWeaponIndex,
        int distance,
        int attackerHpBefore,
        int targetHpBefore,
        int attackerLoss,
        int targetDamage,
        int attackerLossPercent,
        int targetDamagePercent,
        PositionDpqResult attackerDpq,
        PositionDpqResult defenderDpq)
    {
        IsValid = simulation.isValid;
        Simulation = simulation;
        WeaponSelectionMode = weaponSelectionMode;
        SensorOption = sensorOption;
        AttackWeapon = attackWeapon;
        AttackWeaponIndex = attackWeaponIndex;
        CounterWeapon = counterWeapon;
        CounterWeaponIndex = counterWeaponIndex;
        Distance = distance;
        AttackerHpBefore = attackerHpBefore;
        TargetHpBefore = targetHpBefore;
        AttackerLoss = attackerLoss;
        TargetDamage = targetDamage;
        AttackerLossPercent = attackerLossPercent;
        TargetDamagePercent = targetDamagePercent;
        AttackerDpq = attackerDpq;
        DefenderDpq = defenderDpq;
    }
}

public readonly struct CombatEvaluationOutcome
{
    public CombatEvaluationResult Combat { get; }
    public AttackDecisionResult AttackDecision { get; }
    public bool HasSimulation => Combat.IsValid;

    public CombatEvaluationOutcome(
        CombatEvaluationResult combat,
        AttackDecisionResult attackDecision)
    {
        Combat = combat;
        AttackDecision = attackDecision;
    }
}

/// <summary>
/// Read-only evaluation of one specific combat. The caller owns legality and
/// candidate discovery; this service simulates the supplied PodeMirar option and
/// applies the UnitData Attack Decision policy without mutating gameplay state.
/// </summary>
public static class CombatEvaluationService
{
    public static CombatEvaluationOutcome Evaluate(CombatEvaluationRequest request)
    {
        if (TryEvaluate(request, out CombatEvaluationResult combat))
        {
            return new CombatEvaluationOutcome(
                combat,
                EvaluateAttackDecision(request, combat));
        }

        return new CombatEvaluationOutcome(
            CombatEvaluationResult.Invalid,
            EvaluateAttackDecision(request));
    }

    public static bool TryEvaluate(
        CombatEvaluationRequest request,
        out CombatEvaluationResult result)
    {
        result = CombatEvaluationResult.Invalid;
        if (!TryGetSimulationContext(request, out UnitData attackerData, out UnitData targetData))
            return false;

        Vector3Int attackCell = request.AttackCell;
        attackCell.z = 0;
        Vector3Int targetCell = request.Target.CurrentCellPosition;
        targetCell.z = 0;
        int distance = Mathf.Max(1, Mathf.RoundToInt(SectorManager.HexDistance(attackCell, targetCell)));

        PodeMirarTargetOption option = IsCanonicalOptionForRequest(request.SensorOption, request)
            ? request.SensorOption
            : null;

        AICombatHpSimulator.AICombatHpResult simulation;
        CombatWeaponSelectionMode selectionMode;
        WeaponData attackWeapon = null;
        int attackWeaponIndex = -1;
        WeaponData counterWeapon = null;
        int counterWeaponIndex = -1;

        int attackerHpBefore = Mathf.Max(0, request.Attacker.CurrentHP);
        int targetHpBefore = Mathf.Max(0, request.Target.CurrentHP);

        if (option != null)
        {
            selectionMode = CombatWeaponSelectionMode.CanonicalSensorOption;
            attackWeapon = option.weapon;
            attackWeaponIndex = option.embarkedWeaponIndex;
            counterWeapon = option.defenderCanCounterAttack
                ? option.defenderCounterWeapon
                : null;
            counterWeaponIndex = option.defenderCanCounterAttack
                ? option.defenderCounterEmbarkedWeaponIndex
                : -1;

            simulation = AICombatHpSimulator.SimulateWithWeapons(
                attackerData,
                targetData,
                attackWeapon,
                counterWeapon,
                attackerHpBefore,
                targetHpBefore,
                request.RpsDatabase,
                request.DpqMatchupDatabase,
                request.AttackerDpq.Points,
                request.DefenderDpq.Points,
                request.AttackerDpq.DefenseBonus,
                request.DefenderDpq.DefenseBonus,
                request.Attacker.IsAircraftGrounded,
                request.Target.IsAircraftGrounded);
        }
        else if (request.AllowLegacyAutomaticWeaponFallback)
        {
            selectionMode = CombatWeaponSelectionMode.LegacyAutomaticFallback;
            simulation = AICombatHpSimulator.Simulate(
                attackerData,
                targetData,
                attackerHpBefore,
                targetHpBefore,
                distance,
                request.RpsDatabase,
                request.DpqMatchupDatabase,
                request.WeaponPriorityData,
                request.AttackerDpq.Points,
                request.DefenderDpq.Points,
                request.AttackerDpq.DefenseBonus,
                request.DefenderDpq.DefenseBonus);
        }
        else
        {
            return false;
        }

        if (!simulation.isValid)
            return false;

        int attackerLoss = Mathf.Max(0, attackerHpBefore - simulation.attackerHpAfter);
        int targetDamage = Mathf.Max(0, targetHpBefore - simulation.defenderHpAfter);
        int attackerLossPercent = attackerHpBefore > 0
            ? Mathf.RoundToInt(attackerLoss * 100f / attackerHpBefore)
            : 0;
        int targetDamagePercent = Mathf.RoundToInt(
            targetDamage * 100f / Mathf.Max(1, targetData.maxHP));

        result = new CombatEvaluationResult(
            simulation,
            selectionMode,
            option,
            attackWeapon,
            attackWeaponIndex,
            counterWeapon,
            counterWeaponIndex,
            distance,
            attackerHpBefore,
            targetHpBefore,
            attackerLoss,
            targetDamage,
            attackerLossPercent,
            targetDamagePercent,
            request.AttackerDpq,
            request.DefenderDpq);
        return true;
    }

    public static AttackDecisionResult EvaluateAttackDecision(CombatEvaluationRequest request)
    {
        if (TryEvaluateAttackDecisionPreconditions(request, out AttackDecisionResult terminal))
            return terminal;

        if (!TryEvaluate(request, out CombatEvaluationResult evaluation))
            return BuildSimulationInvalidResult(request);

        return EvaluateAttackDecision(request, evaluation);
    }

    public static AttackDecisionResult EvaluateAttackDecision(
        CombatEvaluationRequest request,
        CombatEvaluationResult evaluation)
    {
        if (TryEvaluateAttackDecisionPreconditions(request, out AttackDecisionResult terminal))
            return terminal;

        if (!evaluation.IsValid)
            return BuildSimulationInvalidResult(request);

        request.Attacker.TryGetUnitData(out UnitData attackerData);
        AICombatHpSimulator.AICombatHpResult simulation = evaluation.Simulation;
        int hpLossLimit = Mathf.Clamp(
            attackerData.attackAcceptHpLossPercent
            + (request.DefensiveContext ? attackerData.defensiveAttackExtraHpLossPercent : 0),
            0,
            100);
        int eliminationMin = Mathf.Clamp(attackerData.attackEliminationMinPercent, 0, 100);
        int attackerLossPercentOfMax = attackerData.maxHP > 0
            ? Mathf.RoundToInt(evaluation.AttackerLoss * 100f / attackerData.maxHP)
            : evaluation.AttackerLossPercent;

        string summary =
            $"atkDecision hp={evaluation.AttackerHpBefore}->{simulation.attackerHpAfter} "
            + $"loss={evaluation.AttackerLossPercent}%/{hpLossLimit}% "
            + $"dmg={evaluation.TargetDamagePercent}%/{eliminationMin}% "
            + $"target={evaluation.TargetHpBefore}->{simulation.defenderHpAfter} "
            + $"dpq={evaluation.AttackerDpq.Points}/{evaluation.DefenderDpq.Points} "
            + $"def={evaluation.AttackerDpq.DefenseBonus}/{evaluation.DefenderDpq.DefenseBonus} "
            + $"kill={simulation.killGuaranteed} survive={simulation.attackerSurvives}";

        if (attackerData.attackMustSurvive && !simulation.attackerSurvives)
        {
            return BuildAttackDecisionResult(
                AttackDecisionStatus.BlockedMustSurvive,
                false,
                summary + " BLOCK mustSurvive",
                evaluation,
                hpLossLimit,
                eliminationMin);
        }

        if (evaluation.TargetDamage <= 0)
        {
            return BuildAttackDecisionResult(
                AttackDecisionStatus.BlockedNoDamage,
                false,
                summary + " BLOCK noDamage",
                evaluation,
                hpLossLimit,
                eliminationMin);
        }

        if (simulation.killGuaranteed)
        {
            return BuildAttackDecisionResult(
                AttackDecisionStatus.AllowedKill,
                true,
                summary + " OK kill",
                evaluation,
                hpLossLimit,
                eliminationMin);
        }

        if (evaluation.TargetDamagePercent >= eliminationMin
            && evaluation.AttackerLossPercent <= hpLossLimit)
        {
            return BuildAttackDecisionResult(
                AttackDecisionStatus.AllowedDamage,
                true,
                summary + " OK damage",
                evaluation,
                hpLossLimit,
                eliminationMin);
        }

        if (!attackerData.attackMustSurvive)
        {
            return BuildAttackDecisionResult(
                AttackDecisionStatus.AllowedSurvivalNotRequired,
                true,
                summary + " OK noSurviveReq",
                evaluation,
                hpLossLimit,
                eliminationMin);
        }

        if (evaluation.AttackerLossPercent <= hpLossLimit)
        {
            return BuildAttackDecisionResult(
                AttackDecisionStatus.AllowedHpLoss,
                true,
                summary + " OK hpLoss",
                evaluation,
                hpLossLimit,
                eliminationMin);
        }

        if (simulation.attackerSurvives
            && attackerLossPercentOfMax <= hpLossLimit
            && evaluation.TargetDamagePercent >= eliminationMin)
        {
            return BuildAttackDecisionResult(
                AttackDecisionStatus.AllowedHpLossOfMax,
                true,
                summary + " OK hpLossOfMax",
                evaluation,
                hpLossLimit,
                eliminationMin);
        }

        return BuildAttackDecisionResult(
            AttackDecisionStatus.BlockedHpLoss,
            false,
            summary + " BLOCK hpLoss",
            evaluation,
            hpLossLimit,
            eliminationMin);
    }

    public static bool TryEvaluateAttackDecisionPreconditions(
        CombatEvaluationRequest request,
        out AttackDecisionResult result)
    {
        const string defaultReason = "atkDecision=allow";
        if (request == null || request.Attacker == null || request.Target == null)
        {
            result = new AttackDecisionResult(
                AttackDecisionStatus.MissingParticipants,
                true,
                defaultReason);
            return true;
        }

        if (!request.Attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
        {
            result = new AttackDecisionResult(
                AttackDecisionStatus.AttackerDataUnavailable,
                true,
                defaultReason);
            return true;
        }

        if (!attackerData.useAttackDecision)
        {
            result = new AttackDecisionResult(
                AttackDecisionStatus.Disabled,
                true,
                "atkDecision=off");
            return true;
        }

        if (request.DefensiveContext && attackerData.ignoreAttackDecisionWhenDefending)
        {
            result = new AttackDecisionResult(
                AttackDecisionStatus.IgnoredWhileDefending,
                true,
                "atkDecision=defIgnore");
            return true;
        }

        if (!request.Target.TryGetUnitData(out UnitData targetData) || targetData == null)
        {
            result = new AttackDecisionResult(
                AttackDecisionStatus.TargetDataUnavailable,
                true,
                defaultReason);
            return true;
        }

        if (!HasSimulationDependencies(request))
        {
            result = new AttackDecisionResult(
                AttackDecisionStatus.SimulationUnavailable,
                true,
                "atkDecision=simUnavailable");
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryGetSimulationContext(
        CombatEvaluationRequest request,
        out UnitData attackerData,
        out UnitData targetData)
    {
        attackerData = null;
        targetData = null;
        return request != null
            && request.Attacker != null
            && request.Target != null
            && request.Attacker.TryGetUnitData(out attackerData)
            && attackerData != null
            && request.Target.TryGetUnitData(out targetData)
            && targetData != null
            && HasSimulationDependencies(request);
    }

    private static bool HasSimulationDependencies(CombatEvaluationRequest request)
    {
        return request != null
            && request.RpsDatabase != null
            && request.DpqMatchupDatabase != null
            && request.WeaponPriorityData != null;
    }

    private static bool IsCanonicalOptionForRequest(
        PodeMirarTargetOption option,
        CombatEvaluationRequest request)
    {
        return option != null
            && option.weapon != null
            && option.attackerUnit == request.Attacker
            && option.targetUnit == request.Target;
    }

    private static AttackDecisionResult BuildSimulationInvalidResult(
        CombatEvaluationRequest request)
    {
        return new AttackDecisionResult(
            AttackDecisionStatus.SimulationInvalid,
            true,
            "atkDecision=simInvalid",
            attackerDpq: request != null ? request.AttackerDpq : default,
            defenderDpq: request != null ? request.DefenderDpq : default);
    }

    private static AttackDecisionResult BuildAttackDecisionResult(
        AttackDecisionStatus status,
        bool isAllowed,
        string reason,
        CombatEvaluationResult evaluation,
        int hpLossLimit,
        int eliminationMin)
    {
        return new AttackDecisionResult(
            status,
            isAllowed,
            reason,
            hasSimulation: true,
            attackerHpBefore: evaluation.AttackerHpBefore,
            attackerHpAfter: evaluation.Simulation.attackerHpAfter,
            targetHpBefore: evaluation.TargetHpBefore,
            targetHpAfter: evaluation.Simulation.defenderHpAfter,
            attackerLoss: evaluation.AttackerLoss,
            targetDamage: evaluation.TargetDamage,
            attackerLossPercent: evaluation.AttackerLossPercent,
            targetDamagePercent: evaluation.TargetDamagePercent,
            hpLossLimitPercent: hpLossLimit,
            eliminationMinPercent: eliminationMin,
            killGuaranteed: evaluation.Simulation.killGuaranteed,
            attackerSurvives: evaluation.Simulation.attackerSurvives,
            attackerDpq: evaluation.AttackerDpq,
            defenderDpq: evaluation.DefenderDpq);
    }
}
