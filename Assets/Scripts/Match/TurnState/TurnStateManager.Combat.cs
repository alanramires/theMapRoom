using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private readonly struct CombatResolutionResult
    {
        public readonly bool success;
        public readonly string trace;

        public CombatResolutionResult(bool success, string trace)
        {
            this.success = success;
            this.trace = trace;
        }
    }

    private CombatResolutionResult ResolveCombatFromSelectedOption(PodeMirarTargetOption option)
    {
        StringBuilder trace = new StringBuilder(1024);
        trace.AppendLine("[Combate] Resolve (RPS + DPQ)");

        if (option == null)
        {
            trace.AppendLine("1) Falha: opcao nula.");
            return new CombatResolutionResult(false, trace.ToString());
        }

        UnitManager attacker = option.attackerUnit;
        UnitManager defender = option.targetUnit;
        if (attacker == null || defender == null)
        {
            trace.AppendLine("1) Falha: atacante ou defensor nulo.");
            return new CombatResolutionResult(false, trace.ToString());
        }

        string attackWeaponName = ResolveWeaponName(option.weapon, "arma");
        string counterWeaponName = ResolveWeaponName(option.defenderCounterWeapon, "-");
        int attackerHpBefore = Mathf.Max(0, attacker.CurrentHP);
        int defenderHpBefore = Mathf.Max(0, defender.CurrentHP);

        trace.AppendLine("1) Entrada");
        trace.AppendLine($"- Atacante: {attacker.name}");
        trace.AppendLine($"- Defensor: {defender.name}");
        trace.AppendLine($"- Indice arma embarcada atacante: {option.embarkedWeaponIndex}");
        trace.AppendLine($"- Arma atacante: {attackWeaponName}");
        trace.AppendLine($"- Categoria arma atacante: {ResolveWeaponCategory(option.weapon)}");
        trace.AppendLine($"- Indice arma embarcada defensor (revide): {option.defenderCounterEmbarkedWeaponIndex}");
        trace.AppendLine($"- Arma revide: {counterWeaponName}");
        trace.AppendLine($"- Categoria arma revide: {ResolveWeaponCategory(option.defenderCounterWeapon)}");
        trace.AppendLine($"- Distancia: {option.distance}");
        trace.AppendLine($"- Posicao atacante: {SafeText(option.attackerPositionLabel)}");
        trace.AppendLine($"- Posicao defensor: {SafeText(option.defenderPositionLabel)}");
        trace.AppendLine($"- Revide previsto: {(option.defenderCanCounterAttack ? "sim" : "nao")}");
        trace.AppendLine($"- HP atacante antes: {attackerHpBefore}");
        trace.AppendLine($"- HP defensor antes: {defenderHpBefore}");

        bool hasAttackerAmmoBefore = TryGetEmbarkedAmmo(attacker, option.embarkedWeaponIndex, out int attackerAmmoBefore);
        bool hasDefenderAmmoBefore = TryGetEmbarkedAmmo(defender, option.defenderCounterEmbarkedWeaponIndex, out int defenderAmmoBefore);
        trace.AppendLine("2) Snapshot");
        trace.AppendLine($"- Muni atacante antes: {(hasAttackerAmmoBefore ? attackerAmmoBefore.ToString() : "indisponivel")}");
        trace.AppendLine($"- Muni defensor antes: {(hasDefenderAmmoBefore ? defenderAmmoBefore.ToString() : "indisponivel")}");

        bool attackerConsumed = attacker.TryConsumeEmbarkedWeaponAmmo(option.embarkedWeaponIndex, 1);
        trace.AppendLine("3) Consumo atacante");
        trace.AppendLine($"- Gasto implicito: 1");
        trace.AppendLine($"- Resultado: {(attackerConsumed ? "ok" : "falhou")}");
        if (!attackerConsumed)
        {
            trace.AppendLine("4) Encerrado: combate nao resolvido (falha ao consumir municao do atacante).");
            return new CombatResolutionResult(false, trace.ToString());
        }

        bool defenderConsumed = false;
        string counterReason = option.defenderCounterReason;
        trace.AppendLine("4) Revide");
        if (option.defenderCanCounterAttack && option.defenderCounterEmbarkedWeaponIndex >= 0)
        {
            defenderConsumed = defender.TryConsumeEmbarkedWeaponAmmo(option.defenderCounterEmbarkedWeaponIndex, 1);
            trace.AppendLine($"- Arma revide: {counterWeaponName}");
            trace.AppendLine($"- Gasto implicito: 1");
            trace.AppendLine($"- Resultado: {(defenderConsumed ? "ok" : "falhou ao consumir municao")}");
        }
        else
        {
            trace.AppendLine("- Sem revide.");
            trace.AppendLine($"- Motivo: {SafeText(counterReason)}");
        }

        bool counterExecuted = option.defenderCanCounterAttack && defenderConsumed;
        GameUnitClass attackerClass = ResolveUnitClass(attacker);
        GameUnitClass defenderClass = ResolveUnitClass(defender);
        WeaponCategory attackerWeaponCategory = ResolveWeaponCategory(option.weapon);
        WeaponCategory defenderWeaponCategory = ResolveWeaponCategory(option.defenderCounterWeapon);

        int attackerWeaponPower = option.weapon != null ? Mathf.Max(0, option.weapon.basicAttack) : 0;
        int defenderWeaponPower = counterExecuted && option.defenderCounterWeapon != null
            ? Mathf.Max(0, option.defenderCounterWeapon.basicAttack)
            : 0;

        RpsBonusInfo attackerAttackRps = ResolveAttackRps(attackerClass, attackerWeaponCategory, defenderClass);
        RpsBonusInfo defenderAttackRps = counterExecuted
            ? ResolveAttackRps(defenderClass, defenderWeaponCategory, attackerClass)
            : RpsBonusInfo.None;

        int attackerAttackEffective = attackerHpBefore * Mathf.Max(0, attackerWeaponPower + attackerAttackRps.value);
        int defenderAttackEffective = counterExecuted
            ? defenderHpBefore * Mathf.Max(0, defenderWeaponPower + defenderAttackRps.value)
            : 0;

        trace.AppendLine("5) Forca de ataque efetiva");
        trace.AppendLine($"- Atacante: HP({attackerHpBefore}) x (Arma({attackerWeaponPower}) + RPSAtaque({FormatSigned(attackerAttackRps.value)})) = {attackerAttackEffective}");
        trace.AppendLine($"- Defensor: HP({defenderHpBefore}) x (Arma({defenderWeaponPower}) + RPSAtaque({FormatSigned(defenderAttackRps.value)})) = {defenderAttackEffective}");
        trace.AppendLine($"- Detalhe RPS ataque atacante: {attackerAttackRps.summary}");
        trace.AppendLine($"- Detalhe RPS ataque defensor: {defenderAttackRps.summary}");

        PositionDpqInfo attackerDpq = ResolveDpqAtUnitPosition(attacker, option.attackerPositionLabel);
        PositionDpqInfo defenderDpq = ResolveDpqAtUnitPosition(defender, option.defenderPositionLabel);
        trace.AppendLine("6) DPQ da posicao");
        trace.AppendLine($"- Atacante: {attackerDpq.name} ({attackerDpq.source}) | defesa={attackerDpq.defenseBonus} | pontos={attackerDpq.points}");
        trace.AppendLine($"- Defensor: {defenderDpq.name} ({defenderDpq.source}) | defesa={defenderDpq.defenseBonus} | pontos={defenderDpq.points}");

        int attackerBaseDefense = GetUnitBaseDefense(attacker);
        int defenderBaseDefense = GetUnitBaseDefense(defender);

        RpsBonusInfo attackerDefenseRps = counterExecuted
            ? ResolveDefenseRps(attackerClass, defenderClass, defenderWeaponCategory)
            : RpsBonusInfo.None;
        RpsBonusInfo defenderDefenseRps = ResolveDefenseRps(defenderClass, attackerClass, attackerWeaponCategory);

        int attackerEffectiveDefense = attackerBaseDefense + attackerDpq.defenseBonus + attackerDefenseRps.value;
        int defenderEffectiveDefense = defenderBaseDefense + defenderDpq.defenseBonus + defenderDefenseRps.value;

        trace.AppendLine("7) Forca de defesa efetiva");
        trace.AppendLine($"- Atacante: defesaUnidade({attackerBaseDefense}) + defesaDPQ({attackerDpq.defenseBonus}) + RPSDefesa({FormatSigned(attackerDefenseRps.value)}) = {attackerEffectiveDefense}");
        trace.AppendLine($"- Defensor: defesaUnidade({defenderBaseDefense}) + defesaDPQ({defenderDpq.defenseBonus}) + RPSDefesa({FormatSigned(defenderDefenseRps.value)}) = {defenderEffectiveDefense}");
        trace.AppendLine($"- Detalhe RPS defesa atacante: {attackerDefenseRps.summary}");
        trace.AppendLine($"- Detalhe RPS defesa defensor: {defenderDefenseRps.summary}");

        int dpqDifference = attackerDpq.points - defenderDpq.points;
        DPQCombatOutcome attackerOutcome = DPQCombatOutcome.Neutro;
        DPQCombatOutcome defenderOutcome = DPQCombatOutcome.Neutro;
        if (dpqMatchupDatabase != null)
            dpqMatchupDatabase.Resolve(attackerDpq.points, defenderDpq.points, out attackerOutcome, out defenderOutcome);

        trace.AppendLine("8) Matchup DPQ");
        trace.AppendLine($"- Diferenca: {attackerDpq.points} - {defenderDpq.points} = {dpqDifference}");
        trace.AppendLine($"- Outcome atacante: {attackerOutcome}");
        trace.AppendLine($"- Outcome defensor: {defenderOutcome}");

        int defenderSafeDefense = Mathf.Max(1, defenderEffectiveDefense);
        int attackerSafeDefense = Mathf.Max(1, attackerEffectiveDefense);
        float rawOnDefender = (float)attackerAttackEffective / defenderSafeDefense;
        float rawOnAttacker = counterExecuted ? (float)defenderAttackEffective / attackerSafeDefense : 0f;

        trace.AppendLine("9) Eliminacao (conta bruta)");
        trace.AppendLine($"- Tipo: {(counterExecuted ? "simultanea" : "unilateral")}");
        trace.AppendLine($"- No defensor: {attackerAttackEffective} / {defenderSafeDefense} = {rawOnDefender:0.###}");
        trace.AppendLine($"- No atacante: {defenderAttackEffective} / {attackerSafeDefense} = {rawOnAttacker:0.###}");

        int roundedOnDefender = DPQCombatMath.DivideAndRound(attackerAttackEffective, defenderSafeDefense, attackerOutcome);
        int roundedOnAttacker = counterExecuted
            ? DPQCombatMath.DivideAndRound(defenderAttackEffective, attackerSafeDefense, defenderOutcome)
            : 0;

        int appliedOnDefender = Mathf.Max(0, roundedOnDefender);
        int appliedOnAttacker = Mathf.Max(0, roundedOnAttacker);

        int defenderHpAfter = Mathf.Max(0, defenderHpBefore - appliedOnDefender);
        int attackerHpAfter = Mathf.Max(0, attackerHpBefore - appliedOnAttacker);
        defender.SetCurrentHP(defenderHpAfter);
        attacker.SetCurrentHP(attackerHpAfter);

        trace.AppendLine("10) Arredondamento + Aplicacao");
        trace.AppendLine($"- Regra defensor: {BuildRoundingExplanation(attackerAttackEffective, defenderSafeDefense, attackerOutcome, roundedOnDefender)}");
        trace.AppendLine($"- Regra atacante: {BuildRoundingExplanation(defenderAttackEffective, attackerSafeDefense, defenderOutcome, roundedOnAttacker)}");
        trace.AppendLine($"- Elim no defensor: rounded={roundedOnDefender} -> aplicado={appliedOnDefender}");
        trace.AppendLine($"- Elim no atacante: rounded={roundedOnAttacker} -> aplicado={appliedOnAttacker}");
        trace.AppendLine($"- HP defensor: {defenderHpBefore} -> {defenderHpAfter}");
        trace.AppendLine($"- HP atacante: {attackerHpBefore} -> {attackerHpAfter}");

        bool hasAttackerAmmoAfter = TryGetEmbarkedAmmo(attacker, option.embarkedWeaponIndex, out int attackerAmmoAfter);
        bool hasDefenderAmmoAfter = TryGetEmbarkedAmmo(defender, option.defenderCounterEmbarkedWeaponIndex, out int defenderAmmoAfter);
        trace.AppendLine("11) Saida");
        trace.AppendLine($"- Muni atacante depois: {(hasAttackerAmmoAfter ? attackerAmmoAfter.ToString() : "indisponivel")}");
        trace.AppendLine($"- Muni defensor depois: {(hasDefenderAmmoAfter ? defenderAmmoAfter.ToString() : "indisponivel")}");
        trace.AppendLine($"- Revide executado: {(counterExecuted ? "sim" : "nao")}");

        return new CombatResolutionResult(true, trace.ToString());
    }

    private PositionDpqInfo ResolveDpqAtUnitPosition(UnitManager unit, string sensorPositionLabel)
    {
        PositionDpqInfo info = new PositionDpqInfo
        {
            source = !string.IsNullOrWhiteSpace(sensorPositionLabel) ? sensorPositionLabel : "-",
            name = "DPQ: (nenhum)",
            defenseBonus = 0,
            points = 0
        };

        if (unit == null)
            return info;

        Tilemap referenceTilemap = unit.BoardTilemap;
        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(referenceTilemap, cell);
        if (construction != null && TryGetConstructionDpq(construction, out DPQData constructionDpq))
            return BuildDpqInfo(constructionDpq, $"Construcao: {ResolveConstructionName(construction)}");

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(referenceTilemap, cell);
        if (structure != null && structure.dpqData != null)
            return BuildDpqInfo(structure.dpqData, $"Estrutura: {ResolveStructureName(structure)}");

        if (TryResolveTerrainAtCell(referenceTilemap, terrainDatabase, cell, out TerrainTypeData terrain) && terrain != null && terrain.dpqData != null)
            return BuildDpqInfo(terrain.dpqData, $"Terreno: {ResolveTerrainName(terrain)}");

        return info;
    }

    private RpsBonusInfo ResolveAttackRps(GameUnitClass attackerClass, WeaponCategory category, GameUnitClass defenderClass)
    {
        if (rpsDatabase == null)
            return RpsBonusInfo.NoneWithReason("sem RPSDatabase");

        if (rpsDatabase.TryResolveAttackBonus(attackerClass, category, defenderClass, out int bonus, out RPSAttackEntry entry, out _))
        {
            string text = entry != null && !string.IsNullOrWhiteSpace(entry.RpsAttackText)
                ? entry.RpsAttackText
                : $"RPS Ataque {FormatSigned(bonus)}";
            return new RpsBonusInfo(bonus, text);
        }

        return RpsBonusInfo.NoneWithReason("sem match");
    }

    private RpsBonusInfo ResolveDefenseRps(GameUnitClass defenderClass, GameUnitClass attackerClass, WeaponCategory category)
    {
        if (rpsDatabase == null)
            return RpsBonusInfo.NoneWithReason("sem RPSDatabase");

        if (rpsDatabase.TryResolveDefenseBonus(defenderClass, attackerClass, category, out int bonus, out RPSDefenseEntry entry, out _))
        {
            string text = entry != null && !string.IsNullOrWhiteSpace(entry.RpsDefenseText)
                ? entry.RpsDefenseText
                : $"RPS Defesa {FormatSigned(bonus)}";
            return new RpsBonusInfo(bonus, text);
        }

        return RpsBonusInfo.NoneWithReason("sem match");
    }

    private static bool TryGetConstructionDpq(ConstructionManager construction, out DPQData dpq)
    {
        dpq = null;
        if (construction == null)
            return false;

        ConstructionDatabase db = construction.ConstructionDatabase;
        string id = construction.ConstructionId;
        if (db == null || string.IsNullOrWhiteSpace(id))
            return false;

        if (!db.TryGetById(id, out ConstructionData data) || data == null)
            return false;

        dpq = data.dpqData;
        return dpq != null;
    }

    private static PositionDpqInfo BuildDpqInfo(DPQData dpq, string source)
    {
        if (dpq == null)
        {
            return new PositionDpqInfo
            {
                source = source,
                name = "DPQ: (nenhum)",
                defenseBonus = 0,
                points = 0
            };
        }

        string dpqName = !string.IsNullOrWhiteSpace(dpq.nome) ? dpq.nome : (!string.IsNullOrWhiteSpace(dpq.id) ? dpq.id : dpq.name);
        return new PositionDpqInfo
        {
            source = source,
            name = dpqName,
            defenseBonus = dpq.DefesaBonus,
            points = dpq.Pontos
        };
    }

    private static bool TryResolveTerrainAtCell(Tilemap terrainTilemap, TerrainDatabase terrainDb, Vector3Int cell, out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDb == null)
            return false;

        cell.z = 0;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDb.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
        {
            terrain = byMainTile;
            return true;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase other = map.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDb.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }

    private static string ResolveConstructionName(ConstructionManager construction)
    {
        if (construction == null)
            return "(null)";
        if (!string.IsNullOrWhiteSpace(construction.ConstructionDisplayName))
            return construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(construction.ConstructionId))
            return construction.ConstructionId;
        return construction.name;
    }

    private static string ResolveStructureName(StructureData structure)
    {
        if (structure == null)
            return "(null)";
        if (!string.IsNullOrWhiteSpace(structure.displayName))
            return structure.displayName;
        if (!string.IsNullOrWhiteSpace(structure.id))
            return structure.id;
        return structure.name;
    }

    private static string ResolveTerrainName(TerrainTypeData terrain)
    {
        if (terrain == null)
            return "(null)";
        if (!string.IsNullOrWhiteSpace(terrain.displayName))
            return terrain.displayName;
        if (!string.IsNullOrWhiteSpace(terrain.id))
            return terrain.id;
        return terrain.name;
    }

    private static int GetUnitBaseDefense(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return data.defense;
        return 0;
    }

    private static GameUnitClass ResolveUnitClass(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return data.unitClass;
        return GameUnitClass.Infantry;
    }

    private static WeaponCategory ResolveWeaponCategory(WeaponData weapon)
    {
        return weapon != null ? weapon.WeaponCategory : WeaponCategory.AntiInfantaria;
    }

    private static bool TryGetEmbarkedAmmo(UnitManager unit, int embarkedWeaponIndex, out int ammo)
    {
        ammo = 0;
        if (unit == null || embarkedWeaponIndex < 0)
            return false;

        System.Collections.Generic.IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null || embarkedWeaponIndex >= weapons.Count)
            return false;

        UnitEmbarkedWeapon embarked = weapons[embarkedWeaponIndex];
        if (embarked == null)
            return false;

        ammo = embarked.squadAmmunition;
        return true;
    }

    private static string ResolveWeaponName(WeaponData weapon, string fallback)
    {
        if (weapon == null)
            return fallback;

        if (!string.IsNullOrWhiteSpace(weapon.displayName))
            return weapon.displayName;

        return !string.IsNullOrWhiteSpace(weapon.name) ? weapon.name : fallback;
    }

    private static string SafeText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string BuildRoundingExplanation(int numerator, int denominator, DPQCombatOutcome outcome, int roundedResult)
    {
        if (denominator == 0)
            return "divisao por zero -> 0";

        float raw = (float)numerator / denominator;
        string rawText = raw.ToString("0.###");
        return $"{numerator}/{denominator} = {rawText} | outcome={outcome} -> {roundedResult}";
    }

    private static string FormatSigned(int value)
    {
        return value.ToString("+0;-0;+0");
    }

    private struct PositionDpqInfo
    {
        public string source;
        public string name;
        public int defenseBonus;
        public int points;
    }

    private readonly struct RpsBonusInfo
    {
        public static RpsBonusInfo None => new RpsBonusInfo(0, "nao aplicavel");

        public readonly int value;
        public readonly string summary;

        public RpsBonusInfo(int value, string sourceLabel)
        {
            this.value = value;
            summary = $"{sourceLabel} | bonus={FormatSigned(value)}";
        }

        public static RpsBonusInfo NoneWithReason(string reason)
        {
            return new RpsBonusInfo(0, $"RPS +0 ({reason})");
        }
    }
}
