using UnityEngine;

/// <summary>
/// Entrada da formula de combate. Tudo ja resolvido pelo chamador: quem atira com
/// que arma, se o revide acontece, HP e DPQ de cada lado.
/// </summary>
public struct CombatFormulaInput
{
    public UnitData attacker;
    public UnitData defender;
    public WeaponData attackWeapon;
    // So conta quando counterExecuted. Quem decide o revide e o chamador
    // (execucao: municao e camada; previsao: opcao do PodeMirar).
    public WeaponData counterWeapon;
    public bool counterExecuted;
    public int attackerHp;
    public int defenderHp;
    public int attackerDpqPoints;
    public int defenderDpqPoints;
    public int attackerDpqDefenseBonus;
    public int defenderDpqDefenseBonus;
    public bool attackerIsGroundedAircraft;
    public bool defenderIsGroundedAircraft;
    public RPSDatabase rpsDatabase;
    public DPQMatchupDatabase dpqMatchupDatabase;
    // Monta os textos de skill para o trace. A previsao da IA deixa desligado.
    public bool buildExplanation;
}

/// <summary>Uma consulta a tabela RPS, com o que o trace precisa para se explicar.</summary>
public struct CombatRpsLookup
{
    public bool applicable;   // false: nao se aplica (ex.: lado sem revide)
    public bool hasDatabase;
    public bool matched;
    public int value;
    public string entryText;
}

/// <summary>Resultado da formula, com todos os termos intermediarios.</summary>
public struct CombatFormulaResult
{
    public WeaponCategory attackerWeaponCategory;
    public WeaponCategory defenderWeaponCategory;

    // Forca de ataque
    public int attackerWeaponPower;
    public int defenderWeaponPower;
    public CombatRpsLookup attackerAttackRps;
    public CombatRpsLookup defenderAttackRps;
    public int attackerAttackRpsApplied;
    public int defenderAttackRpsApplied;
    public CombatModifierSummary attackerSkill;
    public CombatModifierSummary defenderSkill;
    public int attackerAttackSkillTotal;
    public int defenderAttackSkillTotal;
    public int attackerAttackTermRaw;
    public int attackerAttackTermApplied;
    public int defenderAttackTermRaw;
    public int defenderAttackTermApplied;
    public int attackerAttackEffective;
    public int defenderAttackEffective;

    // Forca de defesa
    public int attackerBaseDefense;
    public int defenderBaseDefense;
    public CombatRpsLookup attackerDefenseRps;
    public CombatRpsLookup defenderDefenseRps;
    public int attackerDefenseSkillTotal;
    public int defenderDefenseSkillTotal;
    public int attackerWoundedPenalty;
    public int defenderWoundedPenalty;
    public int attackerEffectiveDefense;
    public int defenderEffectiveDefense;

    // Matchup DPQ e eliminacao
    public DPQCombatOutcome attackerOutcome;
    public DPQCombatOutcome defenderOutcome;
    public int defenderSafeDefense;
    public int attackerSafeDefense;
    public int roundedOnDefender;
    public int roundedOnAttacker;
    public int appliedOnDefender;
    public int appliedOnAttacker;
    public bool defenderDamageContainedByHpLock;
    public bool attackerDamageContainedByHpLock;
    public int attackerHpAfter;
    public int defenderHpAfter;
}

/// <summary>
/// Formula do combate (RPS + DPQ), fonte unica. A execucao (TurnStateManager.Combat)
/// e a previsao (AICombatHpSimulator) chamam aqui. Pura: nao gasta municao, nao
/// marca disparo, nao aplica dano — so conta.
/// </summary>
public static class CombatFormula
{
    public static CombatFormulaResult Resolve(in CombatFormulaInput input)
    {
        CombatFormulaResult r = default;
        UnitData attacker = input.attacker;
        UnitData defender = input.defender;
        bool counter = input.counterExecuted;
        int attackerHp = input.attackerHp;
        int defenderHp = input.defenderHp;

        GameUnitClass attackerClass = attacker != null ? attacker.unitClass : GameUnitClass.Infantry;
        GameUnitClass defenderClass = defender != null ? defender.unitClass : GameUnitClass.Infantry;

        r.attackerWeaponCategory = input.attackWeapon != null ? input.attackWeapon.WeaponCategory : WeaponCategory.AntiInfantaria;
        r.defenderWeaponCategory = input.counterWeapon != null ? input.counterWeapon.WeaponCategory : WeaponCategory.AntiInfantaria;

        // 1) Forca de ataque
        r.attackerWeaponPower = input.attackWeapon != null ? Mathf.Max(0, input.attackWeapon.basicAttack) : 0;
        r.defenderWeaponPower = counter && input.counterWeapon != null ? Mathf.Max(0, input.counterWeapon.basicAttack) : 0;

        r.attackerAttackRps = LookupAttackRps(input.rpsDatabase, true, attackerClass, r.attackerWeaponCategory, defenderClass);
        r.defenderAttackRps = LookupAttackRps(input.rpsDatabase, counter, defenderClass, r.defenderWeaponCategory, attackerClass);

        // Aeronave pousada: o RPS de quem atira nela nao pode ser negativo.
        r.attackerAttackRpsApplied = input.defenderIsGroundedAircraft
            ? Mathf.Max(0, r.attackerAttackRps.value)
            : r.attackerAttackRps.value;
        r.defenderAttackRpsApplied = counter && input.attackerIsGroundedAircraft
            ? Mathf.Max(0, r.defenderAttackRps.value)
            : r.defenderAttackRps.value;

        // Sem revide, o defensor ainda "recebe" a categoria da arma do atacante
        // para os modifiers de defesa dele.
        WeaponCategory defenderCategoryForSkill = counter ? r.defenderWeaponCategory : r.attackerWeaponCategory;
        r.attackerSkill = CombatModifierResolver.Resolve(attacker, defender, r.attackerWeaponCategory, defenderCategoryForSkill, input.buildExplanation);
        r.defenderSkill = CombatModifierResolver.Resolve(defender, attacker, defenderCategoryForSkill, r.attackerWeaponCategory, input.buildExplanation);

        r.attackerAttackSkillTotal = r.attackerSkill.ownerAttack + r.defenderSkill.opponentAttack;
        r.defenderAttackSkillTotal = r.defenderSkill.ownerAttack + r.attackerSkill.opponentAttack;
        r.attackerDefenseSkillTotal = r.attackerSkill.ownerDefense + r.defenderSkill.opponentDefense;
        r.defenderDefenseSkillTotal = r.defenderSkill.ownerDefense + r.attackerSkill.opponentDefense;

        r.attackerAttackTermRaw = r.attackerWeaponPower + r.attackerAttackRpsApplied + r.attackerAttackSkillTotal;
        r.attackerAttackTermApplied = Mathf.Max(1, r.attackerAttackTermRaw); // Disparo valido: piso de FA = 1.
        r.defenderAttackTermRaw = r.defenderWeaponPower + r.defenderAttackRpsApplied + r.defenderAttackSkillTotal;
        r.defenderAttackTermApplied = counter ? Mathf.Max(1, r.defenderAttackTermRaw) : 0; // Revide valido: piso 1.

        r.attackerAttackEffective = attackerHp * r.attackerAttackTermApplied;
        r.defenderAttackEffective = counter ? defenderHp * r.defenderAttackTermApplied : 0;

        // 2) Forca de defesa
        r.attackerBaseDefense = attacker != null ? attacker.defense : 0;
        r.defenderBaseDefense = defender != null ? defender.defense : 0;
        r.attackerDefenseRps = LookupDefenseRps(input.rpsDatabase, counter, attackerClass, defenderClass, r.defenderWeaponCategory);
        r.defenderDefenseRps = LookupDefenseRps(input.rpsDatabase, true, defenderClass, attackerClass, r.attackerWeaponCategory);
        r.attackerWoundedPenalty = ResolveWoundedDefensePenalty(attackerHp, attacker != null ? attacker.maxHP : 1);
        r.defenderWoundedPenalty = ResolveWoundedDefensePenalty(defenderHp, defender != null ? defender.maxHP : 1);

        r.attackerEffectiveDefense = r.attackerBaseDefense + input.attackerDpqDefenseBonus + r.attackerDefenseRps.value
            + r.attackerDefenseSkillTotal + r.attackerWoundedPenalty;
        r.defenderEffectiveDefense = r.defenderBaseDefense + input.defenderDpqDefenseBonus + r.defenderDefenseRps.value
            + r.defenderDefenseSkillTotal + r.defenderWoundedPenalty;

        // 3) Matchup DPQ
        r.attackerOutcome = DPQCombatOutcome.Neutro;
        r.defenderOutcome = DPQCombatOutcome.Neutro;
        if (input.dpqMatchupDatabase != null)
            input.dpqMatchupDatabase.Resolve(input.attackerDpqPoints, input.defenderDpqPoints, out r.attackerOutcome, out r.defenderOutcome);

        // 4) Eliminacao, arredondamento e trava de HP
        r.defenderSafeDefense = Mathf.Max(1, r.defenderEffectiveDefense);
        r.attackerSafeDefense = Mathf.Max(1, r.attackerEffectiveDefense);
        r.roundedOnDefender = DPQCombatMath.DivideAndRound(r.attackerAttackEffective, r.defenderSafeDefense, r.attackerOutcome);
        r.roundedOnAttacker = counter
            ? DPQCombatMath.DivideAndRound(r.defenderAttackEffective, r.attackerSafeDefense, r.defenderOutcome)
            : 0;

        // Trava de HP: ninguem tira mais do que o proprio HP de quem atira.
        int appliedOnDefender = Mathf.Max(0, r.roundedOnDefender);
        int appliedOnAttacker = Mathf.Max(0, r.roundedOnAttacker);
        int defenderDamageCap = Mathf.Max(0, attackerHp);
        int attackerDamageCap = Mathf.Max(0, defenderHp);
        r.defenderDamageContainedByHpLock = appliedOnDefender > defenderDamageCap;
        r.attackerDamageContainedByHpLock = appliedOnAttacker > attackerDamageCap;
        r.appliedOnDefender = Mathf.Min(appliedOnDefender, defenderDamageCap);
        r.appliedOnAttacker = Mathf.Min(appliedOnAttacker, attackerDamageCap);

        r.defenderHpAfter = Mathf.Max(0, defenderHp - r.appliedOnDefender);
        r.attackerHpAfter = Mathf.Max(0, attackerHp - r.appliedOnAttacker);
        return r;
    }

    public static int ResolveWoundedDefensePenalty(int currentHp, int maxHp)
    {
        int safeMaxHp = Mathf.Max(1, maxHp);
        int safeCurrentHp = Mathf.Clamp(currentHp, 0, safeMaxHp);
        if (safeCurrentHp >= safeMaxHp)
            return 0;
        if (safeCurrentHp <= 5)
            return -2;
        return -1;
    }

    private static CombatRpsLookup LookupAttackRps(
        RPSDatabase rps,
        bool applicable,
        GameUnitClass attackerClass,
        WeaponCategory category,
        GameUnitClass defenderClass)
    {
        CombatRpsLookup lookup = new CombatRpsLookup { applicable = applicable, hasDatabase = rps != null };
        if (!applicable || rps == null)
            return lookup;

        if (rps.TryResolveAttackBonus(attackerClass, category, defenderClass, out int bonus, out RPSAttackEntry entry, out _))
        {
            lookup.matched = true;
            lookup.value = bonus;
            lookup.entryText = entry != null ? entry.RpsAttackText : null;
        }

        return lookup;
    }

    private static CombatRpsLookup LookupDefenseRps(
        RPSDatabase rps,
        bool applicable,
        GameUnitClass defenderClass,
        GameUnitClass attackerClass,
        WeaponCategory category)
    {
        CombatRpsLookup lookup = new CombatRpsLookup { applicable = applicable, hasDatabase = rps != null };
        if (!applicable || rps == null)
            return lookup;

        if (rps.TryResolveDefenseBonus(defenderClass, attackerClass, category, out int bonus, out RPSDefenseEntry entry, out _))
        {
            lookup.matched = true;
            lookup.value = bonus;
            lookup.entryText = entry != null ? entry.RpsDefenseText : null;
        }

        return lookup;
    }
}
