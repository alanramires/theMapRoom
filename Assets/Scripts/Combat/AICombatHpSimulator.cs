using UnityEngine;

/// <summary>
/// Simulador de combate consultivo usado pela IA e pelas ferramentas de auditoria.
/// Estima o resultado de um duelo (HP restante de cada lado) usando a mesma formula
/// da Matriz de HP, sem aplicar dano real ao jogo.
/// Pode receber DPQ real das posicoes para aproximar a decisao do combate resolvido.
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
            rpsDatabase, dpqMatchupDatabase,
            attackerDpqPoints: 1,
            defenderDpqPoints: 1,
            attackerDpqDefenseBonus: 0,
            defenderDpqDefenseBonus: 0);
    }

    public static AICombatHpResult Simulate(
        UnitData attacker,
        UnitData defender,
        int attackerCurrentHp,
        int defenderCurrentHp,
        int distance,
        RPSDatabase rpsDatabase,
        DPQMatchupDatabase dpqMatchupDatabase,
        WeaponPriorityData weaponPriorityData,
        int attackerDpqPoints,
        int defenderDpqPoints,
        int attackerDpqDefenseBonus,
        int defenderDpqDefenseBonus)
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
            rpsDatabase, dpqMatchupDatabase,
            attackerDpqPoints,
            defenderDpqPoints,
            attackerDpqDefenseBonus,
            defenderDpqDefenseBonus);
    }

    public static AICombatHpResult SimulateWithWeapons(
        UnitData attacker,
        UnitData defender,
        WeaponData attackWeapon,
        WeaponData counterWeapon,
        int attackerCurrentHp,
        int defenderCurrentHp,
        RPSDatabase rpsDatabase,
        DPQMatchupDatabase dpqMatchupDatabase,
        int attackerDpqPoints,
        int defenderDpqPoints,
        int attackerDpqDefenseBonus,
        int defenderDpqDefenseBonus,
        bool attackerIsGroundedAircraft,
        bool defenderIsGroundedAircraft)
    {
        if (attacker == null || defender == null || attackWeapon == null)
            return AICombatHpResult.Invalid;

        WeaponPick attackPick = new WeaponPick(attackWeapon, -1, true);
        WeaponPick counterPick = counterWeapon != null
            ? new WeaponPick(counterWeapon, -1, true)
            : WeaponPick.None;

        return SimulateCore(
            attacker, defender,
            attackPick, counterPick,
            attackerCurrentHp, defenderCurrentHp,
            rpsDatabase, dpqMatchupDatabase,
            attackerDpqPoints,
            defenderDpqPoints,
            attackerDpqDefenseBonus,
            defenderDpqDefenseBonus,
            attackerIsGroundedAircraft,
            defenderIsGroundedAircraft);
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
        DPQMatchupDatabase dpqMatchupDatabase,
        int attackerDpqPoints,
        int defenderDpqPoints,
        int attackerDpqDefenseBonus,
        int defenderDpqDefenseBonus,
        bool attackerIsGroundedAircraft = false,
        bool defenderIsGroundedAircraft = false)
    {
        // Mesma conta da execucao: fonte unica em CombatFormula.
        CombatFormulaResult f = CombatFormula.Resolve(new CombatFormulaInput
        {
            attacker = attacker,
            defender = defender,
            attackWeapon = attackPick.weapon,
            counterWeapon = counterPick.isValid ? counterPick.weapon : null,
            counterExecuted = counterPick.isValid,
            attackerHp = attackerHpBefore,
            defenderHp = defenderHpBefore,
            attackerDpqPoints = attackerDpqPoints,
            defenderDpqPoints = defenderDpqPoints,
            attackerDpqDefenseBonus = attackerDpqDefenseBonus,
            defenderDpqDefenseBonus = defenderDpqDefenseBonus,
            attackerIsGroundedAircraft = attackerIsGroundedAircraft,
            defenderIsGroundedAircraft = defenderIsGroundedAircraft,
            rpsDatabase = rpsDatabase,
            dpqMatchupDatabase = dpqMatchupDatabase,
            buildExplanation = false
        });

        return new AICombatHpResult(f.attackerHpAfter, f.defenderHpAfter);
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
}
