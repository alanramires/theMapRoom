using UnityEngine;

/// <summary>
/// Simulador de combate para uso exclusivo da IA.
/// Estima o resultado de um duelo (HP restante de cada lado) usando a mesma formula
/// da Matriz de HP, sem aplicar dano real ao jogo.
/// DPQ fixo em padrao (1x1) - serve como norteador para tomada de decisao.
/// </summary>
public static class AICombatHpSimulator
{
    // ---- Resultado publico ----

    public readonly struct AICombatHpResult
    {
        public readonly bool isValid;
        public readonly int attackerHpAfter;
        public readonly int defenderHpAfter;
        public readonly bool killGuaranteed;   // defenderHpAfter == 0
        public readonly bool attackerSurvives; // attackerHpAfter > 0

        public AICombatHpResult(int attackerHpAfter, int defenderHpAfter)
        {
            isValid = true;
            this.attackerHpAfter = attackerHpAfter;
            this.defenderHpAfter = defenderHpAfter;
            killGuaranteed = defenderHpAfter <= 0;
            attackerSurvives = attackerHpAfter > 0;
        }

        public static AICombatHpResult Invalid => default;
    }

    // ---- Structs internos ----

    private readonly struct WeaponPick
    {
        public static WeaponPick None => new WeaponPick(null, -1, false);
        public readonly WeaponData weapon;
        public readonly int embarkedWeaponIndex;
        public readonly bool isValid;

        public WeaponPick(WeaponData weapon, int embarkedWeaponIndex, bool isValid)
        {
            this.weapon = weapon;
            this.embarkedWeaponIndex = embarkedWeaponIndex;
            this.isValid = isValid;
        }
    }

    private readonly struct SkillModifierSummary
    {
        public static SkillModifierSummary None => new SkillModifierSummary(0, 0, 0, 0);
        public readonly int ownerAttack;
        public readonly int ownerDefense;
        public readonly int opponentAttack;
        public readonly int opponentDefense;

        public SkillModifierSummary(int ownerAttack, int ownerDefense, int opponentAttack, int opponentDefense)
        {
            this.ownerAttack = ownerAttack;
            this.ownerDefense = ownerDefense;
            this.opponentAttack = opponentAttack;
            this.opponentDefense = opponentDefense;
        }
    }

    // ---- API publica ----

    /// <summary>
    /// Simula o duelo com o HP maximo das unidades (sem ferimentos previos).
    /// </summary>
    public static AICombatHpResult Simulate(
        UnitData attacker,
        UnitData defender,
        int distance,
        RPSDatabase rpsDatabase,
        DPQMatchupDatabase dpqMatchupDatabase,
        WeaponPriorityData weaponPriorityData)
    {
        if (attacker == null || defender == null || distance <= 0)
            return AICombatHpResult.Invalid;

        return Simulate(attacker, defender, attacker.maxHP, defender.maxHP,
            distance, rpsDatabase, dpqMatchupDatabase, weaponPriorityData);
    }

    /// <summary>
    /// Simula o duelo com HP customizado (unidades ja feridas).
    /// </summary>
    public static AICombatHpResult Simulate(
        UnitData attacker,
        UnitData defender,
        int attackerCurrentHp,
        int defenderCurrentHp,
        int distance,
        RPSDatabase rpsDatabase,
        DPQMatchupDatabase dpqMatchupDatabase,
        WeaponPriorityData weaponPriorityData)
    {
        if (attacker == null || defender == null || distance <= 0)
            return AICombatHpResult.Invalid;

        WeaponPick attackPick = PickBestAttackWeapon(attacker, defender, distance, weaponPriorityData);
        if (!attackPick.isValid)
            return AICombatHpResult.Invalid;

        WeaponPick counterPick = PickBestCounterWeapon(defender, attacker, distance, weaponPriorityData);

        return SimulateCore(
            attacker, defender,
            attackPick, counterPick,
            attackerCurrentHp, defenderCurrentHp,
            rpsDatabase, dpqMatchupDatabase);
    }

    // ---- Nucleo da simulacao ----

    private static AICombatHpResult SimulateCore(
        UnitData attacker,
        UnitData defender,
        WeaponPick attackPick,
        WeaponPick counterPick,
        int attackerHpBefore,
        int defenderHpBefore,
        RPSDatabase rpsDatabase,
        DPQMatchupDatabase dpqMatchupDatabase)
    {
        WeaponData attackerWeapon = attackPick.weapon;
        WeaponData defenderWeapon = counterPick.isValid ? counterPick.weapon : null;
        bool counterExecuted = counterPick.isValid;

        DPQCombatOutcome attackerOutcome = DPQCombatOutcome.Neutro;
        DPQCombatOutcome defenderOutcome = DPQCombatOutcome.Neutro;
        if (dpqMatchupDatabase != null)
            dpqMatchupDatabase.Resolve(1, 1, out attackerOutcome, out defenderOutcome);

        int attackerWeaponPower = attackerWeapon != null ? Mathf.Max(0, attackerWeapon.basicAttack) : 0;
        int defenderWeaponPower = counterExecuted && defenderWeapon != null ? Mathf.Max(0, defenderWeapon.basicAttack) : 0;

        WeaponCategory attackerCategory = attackerWeapon != null ? attackerWeapon.WeaponCategory : WeaponCategory.AntiInfantaria;
        WeaponCategory defenderCategory = defenderWeapon != null ? defenderWeapon.WeaponCategory : WeaponCategory.AntiInfantaria;
        WeaponCategory defenderCategoryForSkill = counterExecuted ? defenderCategory : attackerCategory;

        int attackerAttackRps = ResolveAttackRps(attacker.unitClass, attackerCategory, defender.unitClass, rpsDatabase);
        int defenderAttackRps = counterExecuted ? ResolveAttackRps(defender.unitClass, defenderCategory, attacker.unitClass, rpsDatabase) : 0;

        SkillModifierSummary attackerSkill = ResolveSkillModifiers(attacker, defender, attackerCategory, defenderCategoryForSkill);
        SkillModifierSummary defenderSkill = ResolveSkillModifiers(defender, attacker, defenderCategoryForSkill, attackerCategory);

        int attackerAttackSkillTotal = attackerSkill.ownerAttack + defenderSkill.opponentAttack;
        int defenderAttackSkillTotal = defenderSkill.ownerAttack + attackerSkill.opponentAttack;
        int attackerDefenseSkillTotal = attackerSkill.ownerDefense + defenderSkill.opponentDefense;
        int defenderDefenseSkillTotal = defenderSkill.ownerDefense + attackerSkill.opponentDefense;

        int attackerAttackTermApplied = Mathf.Max(1, attackerWeaponPower + attackerAttackRps + attackerAttackSkillTotal);
        int defenderAttackTermApplied = counterExecuted ? Mathf.Max(1, defenderWeaponPower + defenderAttackRps + defenderAttackSkillTotal) : 0;

        int attackerAttackEffective = attackerHpBefore * attackerAttackTermApplied;
        int defenderAttackEffective = counterExecuted ? defenderHpBefore * defenderAttackTermApplied : 0;

        int attackerDefenseRps = counterExecuted ? ResolveDefenseRps(attacker.unitClass, defender.unitClass, defenderCategory, rpsDatabase) : 0;
        int defenderDefenseRps = ResolveDefenseRps(defender.unitClass, attacker.unitClass, attackerCategory, rpsDatabase);

        int attackerWoundedPenalty = ResolveWoundedDefensePenalty(attackerHpBefore, attacker.maxHP);
        int defenderWoundedPenalty = ResolveWoundedDefensePenalty(defenderHpBefore, defender.maxHP);

        int attackerEffectiveDefense = attacker.defense + attackerDefenseRps + attackerDefenseSkillTotal + attackerWoundedPenalty;
        int defenderEffectiveDefense = defender.defense + defenderDefenseRps + defenderDefenseSkillTotal + defenderWoundedPenalty;

        int roundedOnDefender = DPQCombatMath.DivideAndRound(
            attackerAttackEffective, Mathf.Max(1, defenderEffectiveDefense), attackerOutcome);
        int roundedOnAttacker = counterExecuted
            ? DPQCombatMath.DivideAndRound(defenderAttackEffective, Mathf.Max(1, attackerEffectiveDefense), defenderOutcome)
            : 0;

        // HP lock: dano maximo limitado pelo HP do oponente
        int appliedOnDefender = Mathf.Min(Mathf.Max(0, roundedOnDefender), Mathf.Max(0, attackerHpBefore));
        int appliedOnAttacker = Mathf.Min(Mathf.Max(0, roundedOnAttacker), Mathf.Max(0, defenderHpBefore));

        return new AICombatHpResult(
            Mathf.Max(0, attackerHpBefore - appliedOnAttacker),
            Mathf.Max(0, defenderHpBefore - appliedOnDefender));
    }

    // ---- Selecao de armas ----

    private static WeaponPick PickBestAttackWeapon(UnitData attacker, UnitData defender, int distance, WeaponPriorityData weaponPriorityData)
    {
        if (attacker == null || defender == null || attacker.embarkedWeapons == null)
            return WeaponPick.None;

        WeaponPick fallback = WeaponPick.None;
        for (int i = 0; i < attacker.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = attacker.embarkedWeapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;

            if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                    embarked, SensorMovementMode.MoveuParado, requireAmmo: false,
                    out int minRange, out int maxRange))
                continue;

            if (distance < minRange || distance > maxRange)
                continue;

            if (!embarked.weapon.SupportsOperationOn(defender.domain, defender.heightLevel))
                continue;

            WeaponPick current = new WeaponPick(embarked.weapon, i, true);
            if (!fallback.isValid)
                fallback = current;

            if (PodeMirarSensor.IsPreferredWeaponForTarget(weaponPriorityData, embarked.weapon, defender.unitClass))
                return current;
        }

        return fallback;
    }

    private static WeaponPick PickBestCounterWeapon(UnitData defender, UnitData attacker, int distance, WeaponPriorityData weaponPriorityData)
    {
        if (!PodeMirarSensor.TryResolveCounterAttackFromData(
                defender, attacker, distance, weaponPriorityData,
                out WeaponData counterWeapon, out int counterEmbarkedIndex, out _))
            return WeaponPick.None;

        return new WeaponPick(counterWeapon, counterEmbarkedIndex, true);
    }

    // ---- Helpers de formula ----

    private static int ResolveAttackRps(GameUnitClass attackerClass, WeaponCategory weaponCategory, GameUnitClass defenderClass, RPSDatabase rpsDatabase)
    {
        if (rpsDatabase == null) return 0;
        rpsDatabase.TryResolveAttackBonus(attackerClass, weaponCategory, defenderClass, out int bonus, out _, out _);
        return bonus;
    }

    private static int ResolveDefenseRps(GameUnitClass defenderClass, GameUnitClass attackerClass, WeaponCategory weaponCategory, RPSDatabase rpsDatabase)
    {
        if (rpsDatabase == null) return 0;
        rpsDatabase.TryResolveDefenseBonus(defenderClass, attackerClass, weaponCategory, out int bonus, out _, out _);
        return bonus;
    }

    private static SkillModifierSummary ResolveSkillModifiers(
        UnitData ownerData,
        UnitData opponentData,
        WeaponCategory ownerWeaponCategory,
        WeaponCategory opponentWeaponCategory)
    {
        if (ownerData == null || ownerData.combatModifiers == null || ownerData.combatModifiers.Count == 0)
            return SkillModifierSummary.None;

        int ownerElite = Mathf.Max(0, ownerData.eliteLevel);
        GameUnitClass opponentClass = opponentData != null ? opponentData.unitClass : GameUnitClass.Infantry;
        int opponentElite = opponentData != null ? Mathf.Max(0, opponentData.eliteLevel) : 0;

        int ownerAttack = 0, ownerDefense = 0, opponentAttack = 0, opponentDefense = 0;

        for (int i = 0; i < ownerData.combatModifiers.Count; i++)
        {
            CombatModifierData modifier = ownerData.combatModifiers[i];
            if (modifier == null) continue;

            if (!modifier.TryGetCombatRpsModifiers(
                    ownerElite, ownerWeaponCategory, opponentWeaponCategory,
                    opponentClass, opponentElite,
                    out int ownerAtkMod, out int ownerDefMod,
                    out int opponentAtkMod, out int opponentDefMod, out _))
                continue;

            ownerAttack += ownerAtkMod;
            ownerDefense += ownerDefMod;
            opponentAttack += opponentAtkMod;
            opponentDefense += opponentDefMod;
        }

        return new SkillModifierSummary(ownerAttack, ownerDefense, opponentAttack, opponentDefense);
    }

    private static int ResolveWoundedDefensePenalty(int currentHp, int maxHp)
    {
        int safeMaxHp = Mathf.Max(1, maxHp);
        int safeCurrentHp = Mathf.Clamp(currentHp, 0, safeMaxHp);
        if (safeCurrentHp >= safeMaxHp) return 0;
        if (safeCurrentHp <= 5) return -2;
        return -1;
    }
}
