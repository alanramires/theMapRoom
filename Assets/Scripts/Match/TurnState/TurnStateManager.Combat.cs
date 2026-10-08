using System.Text;
using System.Collections.Generic;
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

        // A formula precisa das duas fichas; conferir antes de gastar municao.
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null ||
            !defender.TryGetUnitData(out UnitData defenderData) || defenderData == null)
        {
            trace.AppendLine("1) Falha: atacante ou defensor sem UnitData.");
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

        if (attackerData.IsWeaponUseBlockedAt(attacker.GetDomain(), attacker.GetHeightLevel()))
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

        if (!TryGetEmbarkedWeapon(attacker, option.embarkedWeaponIndex, out UnitEmbarkedWeapon attackerEmbarkedWeapon) ||
            !attackerEmbarkedWeapon.CanFireAtLayer(attacker.GetDomain(), attacker.GetHeightLevel()))
        {
            trace.AppendLine("1) Falha: arma do atacante indisponivel para a camada atual da unidade.");
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
            defenderData.IsWeaponUseBlockedAt(defender.GetDomain(), defender.GetHeightLevel());
        trace.AppendLine("4) Revide");
        if (option.defenderCanCounterAttack &&
            option.defenderCounterEmbarkedWeaponIndex >= 0 &&
            !defenderCounterBlockedByEmbarked &&
            !defenderCounterBlockedByLayer)
        {
            bool defenderWeaponAllowedAtCurrentLayer =
                TryGetEmbarkedWeapon(defender, option.defenderCounterEmbarkedWeaponIndex, out UnitEmbarkedWeapon defenderEmbarkedWeapon) &&
                defenderEmbarkedWeapon.CanFireAtLayer(defender.GetDomain(), defender.GetHeightLevel());

            if (!defenderWeaponAllowedAtCurrentLayer)
            {
                defenderConsumed = false;
                counterReason = "Camada atual da unidade incompativel com esta arma.";
                trace.AppendLine($"- Arma revide: {counterWeaponName}");
                trace.AppendLine("- Resultado: falhou (camada atual nao permite disparo da arma).");
            }
            else
            {
                defenderConsumed = defender.TryConsumeEmbarkedWeaponAmmo(option.defenderCounterEmbarkedWeaponIndex, 1);
                trace.AppendLine($"- Arma revide: {counterWeaponName}");
                trace.AppendLine("- Gasto implicito: 1");
                trace.AppendLine($"- Resultado: {(defenderConsumed ? "ok" : "falhou ao consumir municao")}");
            }
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

        // Quem revida tambem disparou: o revide custa ocultacao igual ao ataque.
        // Simetrico ao submarino, que ja emerge ao revidar (ApplyPostAttackSelfEmergeEffect).
        // Fisicamente: a aeronave furtiva abre o compartimento de armas para atirar, e a
        // cavidade quebra o desenho anguloso que devolvia a onda de radar para longe.
        if (counterExecuted)
        {
            defender.MarkAsFired();
            trace.AppendLine("- Marcador de disparo runtime do revide: ativo (hasFiredThisTurn=true).");
        }

        GameUnitClass attackerClass = ResolveUnitClass(attacker);
        GameUnitClass defenderClass = ResolveUnitClass(defender);
        int attackerEliteLevel = ResolveEliteLevel(attacker);
        int defenderEliteLevel = ResolveEliteLevel(defender);

        trace.AppendLine($"- Classe atacante: {attackerClass} | EliteLevel: {attackerEliteLevel}");
        trace.AppendLine($"- Classe defensor: {defenderClass} | EliteLevel: {defenderEliteLevel}");

        PositionDpqInfo attackerDpq = ResolveDpqAtUnitPosition(attacker, option.attackerPositionLabel);
        PositionDpqInfo defenderDpq = ResolveDpqAtUnitPosition(defender, option.defenderPositionLabel);
        bool defenderIsGroundedAircraft = IsGroundedAircraft(defender);
        bool attackerIsGroundedAircraft = IsGroundedAircraft(attacker);

        // A conta e a mesma da previsao da IA: fonte unica em CombatFormula.
        CombatFormulaResult f = CombatFormula.Resolve(new CombatFormulaInput
        {
            attacker = attackerData,
            defender = defenderData,
            attackWeapon = option.weapon,
            counterWeapon = option.defenderCounterWeapon,
            counterExecuted = counterExecuted,
            attackerHp = attackerHpBefore,
            defenderHp = defenderHpBefore,
            attackerDpqPoints = attackerDpq.points,
            defenderDpqPoints = defenderDpq.points,
            attackerDpqDefenseBonus = attackerDpq.defenseBonus,
            defenderDpqDefenseBonus = defenderDpq.defenseBonus,
            attackerIsGroundedAircraft = attackerIsGroundedAircraft,
            defenderIsGroundedAircraft = defenderIsGroundedAircraft,
            rpsDatabase = rpsDatabase,
            dpqMatchupDatabase = dpqMatchupDatabase,
            buildExplanation = true
        });

        RpsBonusInfo attackerAttackRps = ToRpsInfo(f.attackerAttackRps, "RPS Ataque");
        RpsBonusInfo defenderAttackRps = ToRpsInfo(f.defenderAttackRps, "RPS Ataque");
        RpsBonusInfo attackerDefenseRps = ToRpsInfo(f.attackerDefenseRps, "RPS Defesa");
        RpsBonusInfo defenderDefenseRps = ToRpsInfo(f.defenderDefenseRps, "RPS Defesa");
        SkillRpsBonusInfo attackerSkillRps = ToSkillInfo(f.attackerSkill);
        SkillRpsBonusInfo defenderSkillRps = ToSkillInfo(f.defenderSkill);

        trace.AppendLine("5) Forca de ataque efetiva");
        trace.AppendLine($"- Atacante: HP({attackerHpBefore}) x max(1, Arma({f.attackerWeaponPower}) + RPSAtaqueBase({FormatSigned(f.attackerAttackRpsApplied)}) + EliteSkillAtaqueProprio({FormatSigned(attackerSkillRps.ownerAttackValue)}) + EliteSkillAtaqueRecebido({FormatSigned(defenderSkillRps.opponentAttackValue)})) = {f.attackerAttackEffective} (termo bruto={f.attackerAttackTermRaw}, aplicado={f.attackerAttackTermApplied})");
        trace.AppendLine($"- Defensor: HP({defenderHpBefore}) x {(counterExecuted ? "max(1, " : string.Empty)}Arma({f.defenderWeaponPower}) + RPSAtaqueBase({FormatSigned(f.defenderAttackRpsApplied)}) + EliteSkillAtaqueProprio({FormatSigned(defenderSkillRps.ownerAttackValue)}) + EliteSkillAtaqueRecebido({FormatSigned(attackerSkillRps.opponentAttackValue)}){(counterExecuted ? ")" : string.Empty)} = {f.defenderAttackEffective} (termo bruto={f.defenderAttackTermRaw}, aplicado={f.defenderAttackTermApplied})");
        if (defenderIsGroundedAircraft && f.attackerAttackRpsApplied != f.attackerAttackRps.value)
            trace.AppendLine($"- Regra grounded aplicada no ataque: RPS atacante {FormatSigned(f.attackerAttackRps.value)} -> {FormatSigned(f.attackerAttackRpsApplied)}.");
        if (counterExecuted && attackerIsGroundedAircraft && f.defenderAttackRpsApplied != f.defenderAttackRps.value)
            trace.AppendLine($"- Regra grounded aplicada no revide: RPS defensor {FormatSigned(f.defenderAttackRps.value)} -> {FormatSigned(f.defenderAttackRpsApplied)}.");
        trace.AppendLine($"- Detalhe RPS ataque atacante: {attackerAttackRps.summary}");
        trace.AppendLine($"- Detalhe RPS ataque defensor: {defenderAttackRps.summary}");
        trace.AppendLine($"- ELITE SKILL ataque atacante: proprio={FormatSigned(attackerSkillRps.ownerAttackValue)} | recebido={FormatSigned(defenderSkillRps.opponentAttackValue)} | total={FormatSigned(f.attackerAttackSkillTotal)}");
        trace.AppendLine($"- ELITE SKILL ataque defensor: proprio={FormatSigned(defenderSkillRps.ownerAttackValue)} | recebido={FormatSigned(attackerSkillRps.opponentAttackValue)} | total={FormatSigned(f.defenderAttackSkillTotal)}");
        trace.AppendLine($"- Detalhe skill lado atacante: {attackerSkillRps.summary}");
        trace.AppendLine($"- Detalhe skill lado defensor: {defenderSkillRps.summary}");
        trace.AppendLine($"- Detalhe skill defesa do defensor (vs arma atacante): {defenderSkillRps.summary}");

        trace.AppendLine("6) DPQ da posicao");
        trace.AppendLine($"- Atacante: {attackerDpq.name} ({attackerDpq.source}) | defesa={attackerDpq.defenseBonus} | pontos={attackerDpq.points}");
        trace.AppendLine($"- Defensor: {defenderDpq.name} ({defenderDpq.source}) | defesa={defenderDpq.defenseBonus} | pontos={defenderDpq.points}");

        trace.AppendLine("7) Forca de defesa efetiva");
        trace.AppendLine($"- Atacante: defesaUnidade({f.attackerBaseDefense}) + defesaDPQ({attackerDpq.defenseBonus}) + RPSDefesaBase({FormatSigned(f.attackerDefenseRps.value)}) + EliteSkillDefesaProprio({FormatSigned(attackerSkillRps.ownerDefenseValue)}) + EliteSkillDefesaRecebido({FormatSigned(defenderSkillRps.opponentDefenseValue)}) + UnidadeFerida({FormatSigned(f.attackerWoundedPenalty)}) = {f.attackerEffectiveDefense}");
        trace.AppendLine($"- Defensor: defesaUnidade({f.defenderBaseDefense}) + defesaDPQ({defenderDpq.defenseBonus}) + RPSDefesaBase({FormatSigned(f.defenderDefenseRps.value)}) + EliteSkillDefesaProprio({FormatSigned(defenderSkillRps.ownerDefenseValue)}) + EliteSkillDefesaRecebido({FormatSigned(attackerSkillRps.opponentDefenseValue)}) + UnidadeFerida({FormatSigned(f.defenderWoundedPenalty)}) = {f.defenderEffectiveDefense}");
        trace.AppendLine($"- Detalhe RPS defesa atacante: {attackerDefenseRps.summary}");
        trace.AppendLine($"- Detalhe RPS defesa defensor: {defenderDefenseRps.summary}");
        trace.AppendLine($"- ELITE SKILL defesa atacante: proprio={FormatSigned(attackerSkillRps.ownerDefenseValue)} | recebido={FormatSigned(defenderSkillRps.opponentDefenseValue)} | total={FormatSigned(f.attackerDefenseSkillTotal)}");
        trace.AppendLine($"- ELITE SKILL defesa defensor: proprio={FormatSigned(defenderSkillRps.ownerDefenseValue)} | recebido={FormatSigned(attackerSkillRps.opponentDefenseValue)} | total={FormatSigned(f.defenderDefenseSkillTotal)}");

        trace.AppendLine("8) Matchup DPQ");
        trace.AppendLine($"- Diferenca: {attackerDpq.points} - {defenderDpq.points} = {attackerDpq.points - defenderDpq.points}");
        trace.AppendLine($"- Outcome atacante: {f.attackerOutcome}");
        trace.AppendLine($"- Outcome defensor: {f.defenderOutcome}");

        float rawOnDefender = (float)f.attackerAttackEffective / f.defenderSafeDefense;
        float rawOnAttacker = counterExecuted ? (float)f.defenderAttackEffective / f.attackerSafeDefense : 0f;
        trace.AppendLine("9) Eliminacao (conta bruta)");
        trace.AppendLine($"- Tipo: {(counterExecuted ? "simultanea" : "unilateral")}");
        trace.AppendLine($"- No defensor: {f.attackerAttackEffective} / {f.defenderSafeDefense} = {rawOnDefender:0.###}");
        trace.AppendLine($"- No atacante: {f.defenderAttackEffective} / {f.attackerSafeDefense} = {rawOnAttacker:0.###}");

        bool defenderDamageContainedByHpLock = f.defenderDamageContainedByHpLock;
        bool attackerDamageContainedByHpLock = f.attackerDamageContainedByHpLock;
        int defenderHpAfter = f.defenderHpAfter;
        int attackerHpAfter = f.attackerHpAfter;

        trace.AppendLine("10) Arredondamento + Aplicacao (postergada)");
        trace.AppendLine($"- Regra defensor: {BuildRoundingExplanation(f.attackerAttackEffective, f.defenderSafeDefense, f.attackerOutcome, f.roundedOnDefender)}");
        trace.AppendLine($"- Regra atacante: {BuildRoundingExplanation(f.defenderAttackEffective, f.attackerSafeDefense, f.defenderOutcome, f.roundedOnAttacker)}");
        trace.AppendLine($"- Elim no defensor: rounded={f.roundedOnDefender} -> aplicado={f.appliedOnDefender} (trava={Mathf.Max(0, attackerHpBefore)}, contido pela trava de hp={(defenderDamageContainedByHpLock ? "sim" : "nao")})");
        trace.AppendLine($"- Elim no atacante: rounded={f.roundedOnAttacker} -> aplicado={f.appliedOnAttacker} (trava={Mathf.Max(0, defenderHpBefore)}, contido pela trava de hp={(attackerDamageContainedByHpLock ? "sim" : "nao")})");
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

        // Fonte unica: a previsao da IA le o mesmo resolvedor.
        if (PositionDpqResolver.TryResolveData(
                unit,
                unit.CurrentCellPosition,
                unit.BoardTilemap,
                terrainDatabase,
                dpqAirHeightConfig,
                out DPQData dpq,
                out string source))
        {
            return BuildDpqInfo(dpq, source);
        }

        return info;
    }

    private static RpsBonusInfo ToRpsInfo(CombatRpsLookup lookup, string fallbackLabel)
    {
        if (!lookup.applicable)
            return RpsBonusInfo.None;
        if (!lookup.hasDatabase)
            return RpsBonusInfo.NoneWithReason("sem RPSDatabase");
        if (!lookup.matched)
            return RpsBonusInfo.NoneWithReason("sem match");

        string text = !string.IsNullOrWhiteSpace(lookup.entryText)
            ? lookup.entryText
            : $"{fallbackLabel} {FormatSigned(lookup.value)}";
        return new RpsBonusInfo(lookup.value, text);
    }

    private static SkillRpsBonusInfo ToSkillInfo(CombatModifierSummary resolved)
    {
        if (resolved.appliedCount <= 0)
            return SkillRpsBonusInfo.NoneWithReason(resolved.reason);

        return new SkillRpsBonusInfo(
            resolved.ownerAttack,
            resolved.ownerDefense,
            resolved.opponentAttack,
            resolved.opponentDefense,
            $"modifiersAplicados={resolved.appliedCount} | {resolved.reason}");
    }

    private static bool IsGroundedAircraft(UnitManager unit)
    {
        if (unit == null || !unit.IsAircraftGrounded)
            return false;

        return unit.TryGetUnitData(out UnitData data) && data != null && data.IsAircraft();
    }

    private static bool TryGetConstructionDpq(ConstructionManager construction, out DPQData dpq)
    {
        dpq = null;
        if (construction == null || construction.IsForwardObserverSpot)
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

    private static bool TryGetEmbarkedWeapon(UnitManager unit, int embarkedWeaponIndex, out UnitEmbarkedWeapon weapon)
    {
        weapon = null;
        if (unit == null || embarkedWeaponIndex < 0)
            return false;

        System.Collections.Generic.IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null || embarkedWeaponIndex >= weapons.Count)
            return false;

        weapon = weapons[embarkedWeaponIndex];
        return weapon != null;
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

    private void RecordAttackReplayCommand(
        UnitManager attacker,
        UnitManager defender,
        int attackerHpBefore,
        int defenderHpBefore,
        Dictionary<int, int> embarkedHpBeforeById)
    {
        if (replayManager == null || attacker == null || defender == null)
            return;

        replayManager.UpdateCurrentBufferSensorAction(SensorActionType.Attack, "AttackConfirm");
        replayManager.UpdateCurrentBufferTarget(defender, null, defender.CurrentCellPosition, "AttackTargetConfirm");
    }

    private Dictionary<int, int> CaptureEmbarkedHpSnapshot(UnitManager rootA, UnitManager rootB)
    {
        Dictionary<int, int> hpById = new Dictionary<int, int>();
        CollectEmbarkedUnitHpRecursive(rootA, hpById);
        if (rootB != null && rootB != rootA)
            CollectEmbarkedUnitHpRecursive(rootB, hpById);
        return hpById;
    }

    private static void CollectEmbarkedUnitHpRecursive(UnitManager transporter, Dictionary<int, int> hpById)
    {
        if (transporter == null || hpById == null)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null || seats.Count <= 0)
            return;

        HashSet<int> processed = new HashSet<int>();
        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            UnitManager child = seat != null ? seat.embarkedUnit : null;
            if (child == null || child.InstanceId <= 0 || !processed.Add(child.InstanceId))
                continue;

            hpById[child.InstanceId] = Mathf.Max(0, child.CurrentHP);
            CollectEmbarkedUnitHpRecursive(child, hpById);
        }
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


