using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private readonly struct CombatResolutionResult
    {
        public readonly bool success;
        public readonly bool counterExecuted;
        public readonly bool defenderDamageContainedByHpLock;
        public readonly bool attackerDamageContainedByHpLock;
        public readonly UnitManager attackerUnit;
        public readonly UnitManager defenderUnit;
        public readonly int attackerHpAfter;
        public readonly int defenderHpAfter;
        public readonly string trace;

        public CombatResolutionResult(
            bool success,
            bool counterExecuted,
            bool defenderDamageContainedByHpLock,
            bool attackerDamageContainedByHpLock,
            UnitManager attackerUnit,
            UnitManager defenderUnit,
            int attackerHpAfter,
            int defenderHpAfter,
            string trace)
        {
            this.success = success;
            this.counterExecuted = counterExecuted;
            this.defenderDamageContainedByHpLock = defenderDamageContainedByHpLock;
            this.attackerDamageContainedByHpLock = attackerDamageContainedByHpLock;
            this.attackerUnit = attackerUnit;
            this.defenderUnit = defenderUnit;
            this.attackerHpAfter = attackerHpAfter;
            this.defenderHpAfter = defenderHpAfter;
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
            return new CombatResolutionResult(false, false, false, false, null, null, 0, 0, trace.ToString());
        }

        UnitManager attacker = option.attackerUnit;
        UnitManager defender = option.targetUnit;
        if (attacker == null || defender == null)
        {
            trace.AppendLine("1) Falha: atacante ou defensor nulo.");
            return new CombatResolutionResult(false, false, false, false, attacker, defender, 0, 0, trace.ToString());
        }

        if (defender.IsEmbarked)
        {
            trace.AppendLine("1) Falha: defensor embarcado nao pode ser alvejado diretamente.");
            return new CombatResolutionResult(
                false,
                false,
                false,
                false,
                attacker,
                defender,
                Mathf.Max(0, attacker.CurrentHP),
                Mathf.Max(0, defender.CurrentHP),
                trace.ToString());
        }

        if (attacker.TryGetUnitData(out UnitData attackerData) &&
            attackerData != null &&
            attackerData.IsWeaponUseBlockedAt(attacker.GetDomain(), attacker.GetHeightLevel()))
        {
            trace.AppendLine("1) Falha: camada atual do atacante bloqueia uso de armas.");
            return new CombatResolutionResult(
                false,
                false,
                false,
                false,
                attacker,
                defender,
                Mathf.Max(0, attacker.CurrentHP),
                Mathf.Max(0, defender.CurrentHP),
                trace.ToString());
        }

        string attackWeaponName = ResolveWeaponName(option.weapon, "arma");
        string counterWeaponName = ResolveWeaponName(option.defenderCounterWeapon, "-");
        int attackerHpBefore = Mathf.Max(0, attacker.CurrentHP);
        int defenderHpBefore = Mathf.Max(0, defender.CurrentHP);

        trace.AppendLine("1) Entrada");
        trace.AppendLine($"- Atacante: {attacker.name}");
        trace.AppendLine($"- Defensor: {defender.name}");
        trace.AppendLine($"- DB RPS: {(rpsDatabase != null ? "ok" : "null")}");
        trace.AppendLine($"- DB DPQ Matchup: {(dpqMatchupDatabase != null ? "ok" : "null")}");
        trace.AppendLine($"- DB DPQ Air Height: {(dpqAirHeightConfig != null ? "ok" : "null")}");
        trace.AppendLine($"- DB Terrain: {(terrainDatabase != null ? "ok" : "null")}");
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
            return new CombatResolutionResult(false, false, false, false, attacker, defender, attackerHpBefore, defenderHpBefore, trace.ToString());
        }

        attacker.MarkAsFired();
        trace.AppendLine("- Marcador de disparo runtime: ativo (hasFiredThisTurn=true).");

        bool defenderConsumed = false;
        string counterReason = option.defenderCounterReason;
        bool defenderCounterBlockedByEmbarked = defender.IsEmbarked;
        bool defenderCounterBlockedByLayer =
            defender.TryGetUnitData(out UnitData defenderData) &&
            defenderData != null &&
            defenderData.IsWeaponUseBlockedAt(defender.GetDomain(), defender.GetHeightLevel());
        trace.AppendLine("4) Revide");
        if (option.defenderCanCounterAttack &&
            option.defenderCounterEmbarkedWeaponIndex >= 0 &&
            !defenderCounterBlockedByEmbarked &&
            !defenderCounterBlockedByLayer)
        {
            defenderConsumed = defender.TryConsumeEmbarkedWeaponAmmo(option.defenderCounterEmbarkedWeaponIndex, 1);
            trace.AppendLine($"- Arma revide: {counterWeaponName}");
            trace.AppendLine($"- Gasto implicito: 1");
            trace.AppendLine($"- Resultado: {(defenderConsumed ? "ok" : "falhou ao consumir municao")}");
        }
        else
        {
            if (defenderCounterBlockedByEmbarked)
                counterReason = "Defensor embarcado nao pode revidar.";
            else if (defenderCounterBlockedByLayer)
                counterReason = $"{defender.name} nao atira quando em {defender.GetDomain()}/{defender.GetHeightLevel()}";

            trace.AppendLine("- Sem revide.");
            trace.AppendLine($"- Motivo: {SafeText(counterReason)}");
        }

        bool counterExecuted =
            option.defenderCanCounterAttack &&
            !defenderCounterBlockedByEmbarked &&
            !defenderCounterBlockedByLayer &&
            defenderConsumed;
        GameUnitClass attackerClass = ResolveUnitClass(attacker);
        GameUnitClass defenderClass = ResolveUnitClass(defender);
        int attackerEliteLevel = ResolveEliteLevel(attacker);
        int defenderEliteLevel = ResolveEliteLevel(defender);
        WeaponCategory attackerWeaponCategory = ResolveWeaponCategory(option.weapon);
        WeaponCategory defenderWeaponCategory = ResolveWeaponCategory(option.defenderCounterWeapon);

        trace.AppendLine($"- Classe atacante: {attackerClass} | EliteLevel: {attackerEliteLevel}");
        trace.AppendLine($"- Classe defensor: {defenderClass} | EliteLevel: {defenderEliteLevel}");

        int attackerWeaponPower = option.weapon != null ? Mathf.Max(0, option.weapon.basicAttack) : 0;
        int defenderWeaponPower = counterExecuted && option.defenderCounterWeapon != null
            ? Mathf.Max(0, option.defenderCounterWeapon.basicAttack)
            : 0;

        RpsBonusInfo attackerAttackRps = ResolveAttackRps(attackerClass, attackerWeaponCategory, defenderClass);
        RpsBonusInfo defenderAttackRps = counterExecuted
            ? ResolveAttackRps(defenderClass, defenderWeaponCategory, attackerClass)
            : RpsBonusInfo.None;
        bool defenderIsGroundedAircraft = IsGroundedAircraft(defender);
        bool attackerIsGroundedAircraft = IsGroundedAircraft(attacker);
        int attackerAttackRpsBaseValue = attackerAttackRps.value;
        int defenderAttackRpsBaseValue = defenderAttackRps.value;
        int attackerAttackRpsAppliedValue = defenderIsGroundedAircraft
            ? Mathf.Max(0, attackerAttackRpsBaseValue)
            : attackerAttackRpsBaseValue;
        int defenderAttackRpsAppliedValue = counterExecuted && attackerIsGroundedAircraft
            ? Mathf.Max(0, defenderAttackRpsBaseValue)
            : defenderAttackRpsBaseValue;
        WeaponCategory defenderWeaponCategoryForSkill = counterExecuted ? defenderWeaponCategory : attackerWeaponCategory;
        SkillRpsBonusInfo attackerSkillRps = ResolveSkillRps(
            attacker,
            defender,
            attackerWeaponCategory,
            defenderWeaponCategoryForSkill);
        SkillRpsBonusInfo defenderSkillRps = ResolveSkillRps(
            defender,
            attacker,
            defenderWeaponCategoryForSkill,
            attackerWeaponCategory);
        // Defesa do alvo deve considerar modifiers do proprio defensor mesmo sem revide,
        // usando a categoria da arma que ele esta recebendo.
        SkillRpsBonusInfo defenderDefenseSkillRps = ResolveSkillRps(
            defender,
            attacker,
            defenderWeaponCategoryForSkill,
            attackerWeaponCategory);

        int attackerAttackSkillTotal = attackerSkillRps.ownerAttackValue + defenderSkillRps.opponentAttackValue;
        int defenderAttackSkillTotal = defenderSkillRps.ownerAttackValue + attackerSkillRps.opponentAttackValue;
        int attackerDefenseSkillTotal = attackerSkillRps.ownerDefenseValue + defenderSkillRps.opponentDefenseValue;
        int defenderDefenseSkillTotal = defenderDefenseSkillRps.ownerDefenseValue + attackerSkillRps.opponentDefenseValue;

        int attackerTotalAttackRps = attackerAttackRpsAppliedValue + attackerAttackSkillTotal;
        int defenderTotalAttackRps = defenderAttackRpsAppliedValue + defenderAttackSkillTotal;

        int attackerAttackTermRaw = attackerWeaponPower + attackerTotalAttackRps;
        int attackerAttackTermApplied = Mathf.Max(1, attackerAttackTermRaw); // Disparo valido: piso de FA = 1.
        int defenderAttackTermRaw = defenderWeaponPower + defenderTotalAttackRps;
        int defenderAttackTermApplied = counterExecuted
            ? Mathf.Max(1, defenderAttackTermRaw) // Revide valido: piso de FA = 1.
            : 0;

        int attackerAttackEffective = attackerHpBefore * attackerAttackTermApplied;
        int defenderAttackEffective = counterExecuted
            ? defenderHpBefore * defenderAttackTermApplied
            : 0;

        trace.AppendLine("5) Forca de ataque efetiva");
        trace.AppendLine($"- Atacante: HP({attackerHpBefore}) x max(1, Arma({attackerWeaponPower}) + RPSAtaqueBase({FormatSigned(attackerAttackRpsAppliedValue)}) + EliteSkillAtaqueProprio({FormatSigned(attackerSkillRps.ownerAttackValue)}) + EliteSkillAtaqueRecebido({FormatSigned(defenderSkillRps.opponentAttackValue)})) = {attackerAttackEffective} (termo bruto={attackerAttackTermRaw}, aplicado={attackerAttackTermApplied})");
        trace.AppendLine($"- Defensor: HP({defenderHpBefore}) x {(counterExecuted ? "max(1, " : string.Empty)}Arma({defenderWeaponPower}) + RPSAtaqueBase({FormatSigned(defenderAttackRpsAppliedValue)}) + EliteSkillAtaqueProprio({FormatSigned(defenderSkillRps.ownerAttackValue)}) + EliteSkillAtaqueRecebido({FormatSigned(attackerSkillRps.opponentAttackValue)}){(counterExecuted ? ")" : string.Empty)} = {defenderAttackEffective} (termo bruto={defenderAttackTermRaw}, aplicado={defenderAttackTermApplied})");
        if (defenderIsGroundedAircraft && attackerAttackRpsAppliedValue != attackerAttackRpsBaseValue)
            trace.AppendLine($"- Regra grounded aplicada no ataque: RPS atacante {FormatSigned(attackerAttackRpsBaseValue)} -> {FormatSigned(attackerAttackRpsAppliedValue)}.");
        if (counterExecuted && attackerIsGroundedAircraft && defenderAttackRpsAppliedValue != defenderAttackRpsBaseValue)
            trace.AppendLine($"- Regra grounded aplicada no revide: RPS defensor {FormatSigned(defenderAttackRpsBaseValue)} -> {FormatSigned(defenderAttackRpsAppliedValue)}.");
        trace.AppendLine($"- Detalhe RPS ataque atacante: {attackerAttackRps.summary}");
        trace.AppendLine($"- Detalhe RPS ataque defensor: {defenderAttackRps.summary}");
        trace.AppendLine($"- ELITE SKILL ataque atacante: proprio={FormatSigned(attackerSkillRps.ownerAttackValue)} | recebido={FormatSigned(defenderSkillRps.opponentAttackValue)} | total={FormatSigned(attackerAttackSkillTotal)}");
        trace.AppendLine($"- ELITE SKILL ataque defensor: proprio={FormatSigned(defenderSkillRps.ownerAttackValue)} | recebido={FormatSigned(attackerSkillRps.opponentAttackValue)} | total={FormatSigned(defenderAttackSkillTotal)}");
        trace.AppendLine($"- Detalhe skill lado atacante: {attackerSkillRps.summary}");
        trace.AppendLine($"- Detalhe skill lado defensor: {defenderSkillRps.summary}");
        trace.AppendLine($"- Detalhe skill defesa do defensor (vs arma atacante): {defenderDefenseSkillRps.summary}");

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
        int attackerWoundedPenalty = ResolveWoundedDefensePenalty(attacker);
        int defenderWoundedPenalty = ResolveWoundedDefensePenalty(defender);
        int attackerEffectiveDefense = attackerBaseDefense + attackerDpq.defenseBonus + attackerDefenseRps.value + attackerDefenseSkillTotal + attackerWoundedPenalty;
        int defenderEffectiveDefense = defenderBaseDefense + defenderDpq.defenseBonus + defenderDefenseRps.value + defenderDefenseSkillTotal + defenderWoundedPenalty;

        trace.AppendLine("7) Forca de defesa efetiva");
        trace.AppendLine($"- Atacante: defesaUnidade({attackerBaseDefense}) + defesaDPQ({attackerDpq.defenseBonus}) + RPSDefesaBase({FormatSigned(attackerDefenseRps.value)}) + EliteSkillDefesaProprio({FormatSigned(attackerSkillRps.ownerDefenseValue)}) + EliteSkillDefesaRecebido({FormatSigned(defenderSkillRps.opponentDefenseValue)}) + UnidadeFerida({FormatSigned(attackerWoundedPenalty)}) = {attackerEffectiveDefense}");
        trace.AppendLine($"- Defensor: defesaUnidade({defenderBaseDefense}) + defesaDPQ({defenderDpq.defenseBonus}) + RPSDefesaBase({FormatSigned(defenderDefenseRps.value)}) + EliteSkillDefesaProprio({FormatSigned(defenderDefenseSkillRps.ownerDefenseValue)}) + EliteSkillDefesaRecebido({FormatSigned(attackerSkillRps.opponentDefenseValue)}) + UnidadeFerida({FormatSigned(defenderWoundedPenalty)}) = {defenderEffectiveDefense}");
        trace.AppendLine($"- Detalhe RPS defesa atacante: {attackerDefenseRps.summary}");
        trace.AppendLine($"- Detalhe RPS defesa defensor: {defenderDefenseRps.summary}");
        trace.AppendLine($"- ELITE SKILL defesa atacante: proprio={FormatSigned(attackerSkillRps.ownerDefenseValue)} | recebido={FormatSigned(defenderSkillRps.opponentDefenseValue)} | total={FormatSigned(attackerDefenseSkillTotal)}");
        trace.AppendLine($"- ELITE SKILL defesa defensor: proprio={FormatSigned(defenderDefenseSkillRps.ownerDefenseValue)} | recebido={FormatSigned(attackerSkillRps.opponentDefenseValue)} | total={FormatSigned(defenderDefenseSkillTotal)}");

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
        int defenderDamageCapByAttackerHp = Mathf.Max(0, attackerHpBefore);
        int attackerDamageCapByDefenderHp = Mathf.Max(0, defenderHpBefore);
        bool defenderDamageContainedByHpLock = appliedOnDefender > defenderDamageCapByAttackerHp;
        bool attackerDamageContainedByHpLock = appliedOnAttacker > attackerDamageCapByDefenderHp;
        appliedOnDefender = Mathf.Min(appliedOnDefender, defenderDamageCapByAttackerHp);
        appliedOnAttacker = Mathf.Min(appliedOnAttacker, attackerDamageCapByDefenderHp);

        int defenderHpAfter = Mathf.Max(0, defenderHpBefore - appliedOnDefender);
        int attackerHpAfter = Mathf.Max(0, attackerHpBefore - appliedOnAttacker);

        trace.AppendLine("10) Arredondamento + Aplicacao (postergada)");
        trace.AppendLine($"- Regra defensor: {BuildRoundingExplanation(attackerAttackEffective, defenderSafeDefense, attackerOutcome, roundedOnDefender)}");
        trace.AppendLine($"- Regra atacante: {BuildRoundingExplanation(defenderAttackEffective, attackerSafeDefense, defenderOutcome, roundedOnAttacker)}");
        trace.AppendLine($"- Elim no defensor: rounded={roundedOnDefender} -> aplicado={appliedOnDefender} (trava={defenderDamageCapByAttackerHp}, contido pela trava de hp={(defenderDamageContainedByHpLock ? "sim" : "nao")})");
        trace.AppendLine($"- Elim no atacante: rounded={roundedOnAttacker} -> aplicado={appliedOnAttacker} (trava={attackerDamageCapByDefenderHp}, contido pela trava de hp={(attackerDamageContainedByHpLock ? "sim" : "nao")})");
        trace.AppendLine($"- HP defensor (pendente): {defenderHpBefore} -> {defenderHpAfter}");
        trace.AppendLine($"- HP atacante (pendente): {attackerHpBefore} -> {attackerHpAfter}");

        bool hasAttackerAmmoAfter = TryGetEmbarkedAmmo(attacker, option.embarkedWeaponIndex, out int attackerAmmoAfter);
        bool hasDefenderAmmoAfter = TryGetEmbarkedAmmo(defender, option.defenderCounterEmbarkedWeaponIndex, out int defenderAmmoAfter);
        trace.AppendLine("11) Saida");
        trace.AppendLine($"- Muni atacante depois: {(hasAttackerAmmoAfter ? attackerAmmoAfter.ToString() : "indisponivel")}");
        trace.AppendLine($"- Muni defensor depois: {(hasDefenderAmmoAfter ? defenderAmmoAfter.ToString() : "indisponivel")}");
        trace.AppendLine($"- Revide executado: {(counterExecuted ? "sim" : "nao")}");
        trace.AppendLine($"- Contido pela trava de hp (no defensor): {(defenderDamageContainedByHpLock ? "sim" : "nao")}");
        trace.AppendLine($"- Contido pela trava de hp (no atacante): {(attackerDamageContainedByHpLock ? "sim" : "nao")}");

        return new CombatResolutionResult(
            true,
            counterExecuted,
            defenderDamageContainedByHpLock,
            attackerDamageContainedByHpLock,
            attacker,
            defender,
            attackerHpAfter,
            defenderHpAfter,
            trace.ToString());
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
        Domain activeDomain = unit.GetDomain();
        HeightLevel activeHeight = unit.GetHeightLevel();

        if (activeDomain == Domain.Air
            && dpqAirHeightConfig != null
            && dpqAirHeightConfig.TryGetFor(activeDomain, activeHeight, out DPQData airDpq)
            && airDpq != null)
        {
            return BuildDpqInfo(airDpq, $"Camada ativa: {activeDomain}/{activeHeight}");
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(referenceTilemap, cell);
        if (construction != null
            && ConstructionSupportsLayer(construction, activeDomain, activeHeight)
            && TryGetConstructionDpq(construction, out DPQData constructionDpq))
        {
            return BuildDpqInfo(constructionDpq, $"Construcao: {ResolveConstructionName(construction)}");
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(referenceTilemap, cell);
        if (structure != null
            && StructureSupportsLayer(structure, activeDomain, activeHeight)
            && structure.dpqData != null)
        {
            return BuildDpqInfo(structure.dpqData, $"Estrutura: {ResolveStructureName(structure)}");
        }

        if (TryResolveTerrainAtCellForLayer(referenceTilemap, terrainDatabase, cell, activeDomain, activeHeight, out TerrainTypeData terrain)
            && terrain != null
            && terrain.dpqData != null)
        {
            return BuildDpqInfo(terrain.dpqData, $"Terreno: {ResolveTerrainName(terrain)}");
        }

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

    private static bool IsGroundedAircraft(UnitManager unit)
    {
        if (unit == null || !unit.IsAircraftGrounded)
            return false;

        return unit.TryGetUnitData(out UnitData data) && data != null && data.IsAircraft();
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

    private SkillRpsBonusInfo ResolveSkillRps(
        UnitManager ownerUnit,
        UnitManager opponentUnit,
        WeaponCategory ownerWeaponCategory,
        WeaponCategory opponentWeaponCategory)
    {
        CombatModifierSummary resolved = CombatModifierResolver.Resolve(ownerUnit, opponentUnit, ownerWeaponCategory, opponentWeaponCategory);
        if (resolved.appliedCount <= 0)
            return SkillRpsBonusInfo.NoneWithReason(resolved.reason);

        return new SkillRpsBonusInfo(
            resolved.ownerAttack,
            resolved.ownerDefense,
            resolved.opponentAttack,
            resolved.opponentDefense,
            $"modifiersAplicados={resolved.appliedCount} | {resolved.reason}");
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

    private static bool TryResolveTerrainAtCellForLayer(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        Domain activeDomain,
        HeightLevel activeHeight,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDb == null)
            return false;

        cell.z = 0;
        TerrainTypeData fallback = null;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDb.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
        {
            if (TerrainSupportsLayer(byMainTile, activeDomain, activeHeight))
            {
                terrain = byMainTile;
                return true;
            }

            fallback = byMainTile;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
        {
            terrain = fallback;
            return terrain != null;
        }

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
                if (fallback == null)
                    fallback = byGridTile;

                if (TerrainSupportsLayer(byGridTile, activeDomain, activeHeight))
                {
                    terrain = byGridTile;
                    return true;
                }
            }
        }

        terrain = fallback;
        return terrain != null;
    }

    private static bool TerrainSupportsLayer(TerrainTypeData terrain, Domain domain, HeightLevel heightLevel)
    {
        if (terrain == null)
            return false;
        if (terrain.domain == domain && terrain.heightLevel == heightLevel)
            return true;
        if (domain == Domain.Air && terrain.alwaysAllowAirDomain)
            return true;
        if (terrain.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < terrain.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrain.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool StructureSupportsLayer(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null)
            return false;
        if (structure.domain == domain && structure.heightLevel == heightLevel)
            return true;
        if (domain == Domain.Air && structure.alwaysAllowAirDomain)
            return true;
        if (structure.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < structure.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = structure.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool ConstructionSupportsLayer(ConstructionManager construction, Domain domain, HeightLevel heightLevel)
    {
        if (construction == null)
            return false;
        if (construction.SupportsLayerMode(domain, heightLevel))
            return true;
        return domain == Domain.Air && construction.AllowsAirDomain();
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

    private static int ResolveWoundedDefensePenalty(UnitManager unit)
    {
        if (unit == null)
            return 0;

        int maxHp = Mathf.Max(1, unit.GetMaxHP());
        int currentHp = Mathf.Clamp(unit.CurrentHP, 0, maxHp);
        if (currentHp >= maxHp)
            return 0;
        if (currentHp <= 5)
            return -2;
        return -1;
    }

    private static GameUnitClass ResolveUnitClass(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return data.unitClass;
        return GameUnitClass.Infantry;
    }

    private static int ResolveEliteLevel(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return Mathf.Max(0, data.eliteLevel);
        return 0;
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

    private readonly struct SkillRpsBonusInfo
    {
        public static SkillRpsBonusInfo None => new SkillRpsBonusInfo(0, 0, 0, 0, "nao aplicavel");

        public readonly int ownerAttackValue;
        public readonly int ownerDefenseValue;
        public readonly int opponentAttackValue;
        public readonly int opponentDefenseValue;
        public readonly string summary;

        public SkillRpsBonusInfo(
            int ownerAttackValue,
            int ownerDefenseValue,
            int opponentAttackValue,
            int opponentDefenseValue,
            string sourceLabel)
        {
            this.ownerAttackValue = ownerAttackValue;
            this.ownerDefenseValue = ownerDefenseValue;
            this.opponentAttackValue = opponentAttackValue;
            this.opponentDefenseValue = opponentDefenseValue;
            summary = $"{sourceLabel} | ownAtk={FormatSigned(ownerAttackValue)} ownDef={FormatSigned(ownerDefenseValue)} oppAtk={FormatSigned(opponentAttackValue)} oppDef={FormatSigned(opponentDefenseValue)}";
        }

        public static SkillRpsBonusInfo NoneWithReason(string reason)
        {
            return new SkillRpsBonusInfo(0, 0, 0, 0, $"RPS Skill +0/+0/+0/+0 ({reason})");
        }
    }
}
