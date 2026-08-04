public enum AttackDecisionStatus
{
    Allowed = 0,
    Disabled = 1,
    IgnoredWhileDefending = 2,
    SimulationUnavailable = 3,
    SimulationInvalid = 4,
    AllowedKill = 5,
    AllowedDamage = 6,
    AllowedSurvivalNotRequired = 7,
    AllowedHpLoss = 8,
    AllowedHpLossOfMax = 9,
    BlockedMustSurvive = 10,
    BlockedNoDamage = 11,
    BlockedHpLoss = 12,
    MissingParticipants = 13,
    AttackerDataUnavailable = 14,
    TargetDataUnavailable = 15
}

/// <summary>
/// Structured explanation of the current Attack Decision policy.
/// IsAllowed remains authoritative: unavailable or invalid simulations are
/// explicitly identified by Status but keep the legacy permissive fallback.
/// </summary>
public readonly struct AttackDecisionResult
{
    public readonly AttackDecisionStatus Status;
    public readonly bool IsAllowed;
    public readonly string Reason;
    public readonly bool HasSimulation;
    public readonly int AttackerHpBefore;
    public readonly int AttackerHpAfter;
    public readonly int TargetHpBefore;
    public readonly int TargetHpAfter;
    public readonly int AttackerLoss;
    public readonly int TargetDamage;
    public readonly int AttackerLossPercent;
    public readonly int TargetDamagePercent;
    public readonly int HpLossLimitPercent;
    public readonly int EliminationMinPercent;
    public readonly bool KillGuaranteed;
    public readonly bool AttackerSurvives;
    public readonly PositionDpqResult AttackerDpq;
    public readonly PositionDpqResult DefenderDpq;

    public AttackDecisionResult(
        AttackDecisionStatus status,
        bool isAllowed,
        string reason,
        bool hasSimulation = false,
        int attackerHpBefore = 0,
        int attackerHpAfter = 0,
        int targetHpBefore = 0,
        int targetHpAfter = 0,
        int attackerLoss = 0,
        int targetDamage = 0,
        int attackerLossPercent = 0,
        int targetDamagePercent = 0,
        int hpLossLimitPercent = 0,
        int eliminationMinPercent = 0,
        bool killGuaranteed = false,
        bool attackerSurvives = false,
        PositionDpqResult attackerDpq = default,
        PositionDpqResult defenderDpq = default)
    {
        Status = status;
        IsAllowed = isAllowed;
        Reason = reason ?? string.Empty;
        HasSimulation = hasSimulation;
        AttackerHpBefore = attackerHpBefore;
        AttackerHpAfter = attackerHpAfter;
        TargetHpBefore = targetHpBefore;
        TargetHpAfter = targetHpAfter;
        AttackerLoss = attackerLoss;
        TargetDamage = targetDamage;
        AttackerLossPercent = attackerLossPercent;
        TargetDamagePercent = targetDamagePercent;
        HpLossLimitPercent = hpLossLimitPercent;
        EliminationMinPercent = eliminationMinPercent;
        KillGuaranteed = killGuaranteed;
        AttackerSurvives = attackerSurvives;
        AttackerDpq = attackerDpq;
        DefenderDpq = defenderDpq;
    }
}
