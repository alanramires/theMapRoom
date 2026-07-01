using UnityEngine;

/// <summary>
/// Avaliacao compartilhada de um matchup para UI, ferramentas e Shopping.
/// Converte a simulacao real de combate numa nota estavel de cobertura (0..1).
/// </summary>
public static class UnitCounterEvaluator
{
    public readonly struct Evaluation
    {
        public readonly bool IsValid;
        public readonly int Distance;
        public readonly WeaponCategory WeaponCategory;
        public readonly int Dealt;
        public readonly int Received;
        public readonly bool Survives;
        public readonly bool Kill;
        public readonly int TradeScore;
        public readonly int NetValue;
        public readonly int Ttk;
        public readonly float Coverage;
        public readonly string Verdict;

        public Evaluation(bool valid, int distance, WeaponCategory weaponCategory,
            int dealt, int received, bool survives, bool kill, int tradeScore,
            int netValue, int ttk, float coverage, string verdict)
        {
            IsValid = valid;
            Distance = distance;
            WeaponCategory = weaponCategory;
            Dealt = dealt;
            Received = received;
            Survives = survives;
            Kill = kill;
            TradeScore = tradeScore;
            NetValue = netValue;
            Ttk = ttk;
            Coverage = coverage;
            Verdict = verdict;
        }

        public static Evaluation Invalid => default;
    }

    public static Evaluation EvaluateBestAuto(UnitData attacker, UnitData defender,
        RPSDatabase rps, DPQMatchupDatabase dpq, WeaponPriorityData priorities)
    {
        if (attacker == null || defender == null)
            return Evaluation.Invalid;

        Evaluation best = Evaluation.Invalid;
        int maxRange = ResolveMaxRange(attacker);
        for (int distance = 1; distance <= maxRange; distance++)
        {
            AICombatHpSimulator.AICombatHpResult result = AICombatHpSimulator.Simulate(
                attacker, defender, Mathf.Max(1, attacker.maxHP), Mathf.Max(1, defender.maxHP),
                distance, rps, dpq, priorities);
            if (!result.isValid || !TryResolveAutoWeapon(attacker, defender, distance, priorities, out WeaponData weapon))
                continue;

            Evaluation candidate = FromSimulation(attacker, defender, Mathf.Max(1, attacker.maxHP),
                distance, weapon.WeaponCategory, result);
            if (!best.IsValid
                || candidate.Coverage > best.Coverage
                || (Mathf.Approximately(candidate.Coverage, best.Coverage) && candidate.Dealt > best.Dealt)
                || (Mathf.Approximately(candidate.Coverage, best.Coverage) && candidate.Dealt == best.Dealt
                    && candidate.Distance > best.Distance))
                best = candidate;
        }
        return best;
    }

    public static Evaluation FromSimulation(UnitData attacker, UnitData defender, int attackerHp,
        int distance, WeaponCategory weaponCategory, AICombatHpSimulator.AICombatHpResult result)
    {
        if (attacker == null || defender == null || !result.isValid)
            return Evaluation.Invalid;

        int atkHp = Mathf.Max(1, attackerHp);
        int defHp = Mathf.Max(1, defender.maxHP);
        int dealt = Mathf.Clamp(defHp - result.defenderHpAfter, 0, defHp);
        int received = Mathf.Clamp(atkHp - result.attackerHpAfter, 0, atkHp);
        float valueDestroyed = Mathf.Max(1, defender.cost) * (dealt / (float)defHp);
        float valueLost = Mathf.Max(1, attacker.cost) * (received / (float)atkHp);
        int trade = TradeScore(valueDestroyed, valueLost);
        int netValue = Mathf.RoundToInt(valueDestroyed - valueLost);
        string verdict = Verdict(dealt, received, result.attackerSurvives, trade, valueDestroyed - valueLost);
        return new Evaluation(true, distance, weaponCategory, dealt, received,
            result.attackerSurvives, result.killGuaranteed, trade, netValue,
            dealt > 0 ? Mathf.CeilToInt(defHp / (float)dealt) : -1,
            NoteFromVerdict(verdict), verdict);
    }

    public static float NoteFromVerdict(string verdict)
    {
        switch (verdict)
        {
            case "COUNTER NATURAL": return 1f;
            case "Counter (custo)": return 0.75f;
            case "Forte": return 0.60f;
            case "Troca boa": return 0.40f;
            case "Neutro": return 0.20f;
            default: return 0f;
        }
    }

    public static string Verdict(int dealt, int received, bool survives, int tradeScore, float netValue)
    {
        if (dealt >= 8 && survives) return "COUNTER NATURAL";
        if (tradeScore >= 3 && dealt >= 3 && survives) return "Counter (custo)";
        if (dealt >= 5) return "Forte";
        if (netValue > 0f) return "Troca boa";
        if (received > dealt) return "Desvantagem";
        return "Neutro";
    }

    public static int TradeScore(float valueDestroyed, float valueLost)
    {
        if (valueLost <= 0.01f)
            return valueDestroyed > 0.01f ? 5 : 0;
        float ratio = valueDestroyed / valueLost;
        if (ratio >= 4f) return 5;
        if (ratio >= 2f) return 3;
        if (ratio >= 1.25f) return 1;
        if (ratio > 0.8f) return 0;
        if (ratio > 0.5f) return -1;
        if (ratio > 0.25f) return -3;
        return -5;
    }

    private static int ResolveMaxRange(UnitData attacker)
    {
        int max = 1;
        if (attacker.embarkedWeapons == null)
            return max;
        foreach (UnitEmbarkedWeapon embarked in attacker.embarkedWeapons)
            if (embarked != null && PodeMirarSensor.TryResolveWeaponRangeCandidate(
                    embarked, SensorMovementMode.MoveuParado, false, out _, out int range))
                max = Mathf.Max(max, range);
        return max;
    }

    private static bool TryResolveAutoWeapon(UnitData attacker, UnitData defender, int distance,
        WeaponPriorityData priorities, out WeaponData resolved)
    {
        resolved = null;
        if (attacker.embarkedWeapons == null)
            return false;
        WeaponData fallback = null;
        foreach (UnitEmbarkedWeapon embarked in attacker.embarkedWeapons)
        {
            if (embarked == null || embarked.weapon == null
                || !PodeMirarSensor.TryResolveWeaponRangeCandidate(embarked,
                    SensorMovementMode.MoveuParado, false, out int min, out int max)
                || distance < min || distance > max
                || !embarked.weapon.SupportsOperationOn(defender.domain, defender.heightLevel))
                continue;
            if (fallback == null) fallback = embarked.weapon;
            if (PodeMirarSensor.IsPreferredWeaponForTarget(priorities, embarked.weapon, defender.unitClass))
            {
                resolved = embarked.weapon;
                return true;
            }
        }
        resolved = fallback;
        return resolved != null;
    }
}
