using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CombatLargeMatrixWindow : EditorWindow
{
    private const float RowHeaderWidth = 72f;
    private const float CellWidth = 96f;
    private const float CellHeight = 30f;

    [SerializeField] private UnitDatabase unitDatabase;
    [SerializeField] private RPSDatabase rpsDatabase;
    [SerializeField] private DPQMatchupDatabase dpqMatchupDatabase;
    [SerializeField] private WeaponPriorityData weaponPriorityData;
    [SerializeField] private int range = 1;
    [SerializeField] private bool includeCounterAttack = true;
    [SerializeField] private bool includeUnavailableWeapons = true;
    [SerializeField] private bool autoUpdate = false;
    [SerializeField] private float autoUpdateIntervalSeconds = 0.4f;
    [SerializeField] private string outputRelativePath = "docs/COMBAT_MATRIX.csv";

    private bool matrixReady;
    private Vector2 matrixScroll;
    private List<UnitData> cachedUnits = new List<UnitData>();
    private CellView[,] matrixCells;
    private double lastAutoUpdateTime;
    private string lastUpdateLabel = "nunca";

    [MenuItem("Tools/Combat/Grande Matriz de Lutas")]
    private static void Open()
    {
        CombatLargeMatrixWindow window = GetWindow<CombatLargeMatrixWindow>("Grande Matriz");
        window.minSize = new Vector2(780f, 420f);
        window.TryAutoAssignDatabases();
    }

    private void OnEnable()
    {
        TryAutoAssignDatabases();
        EditorApplication.update += EditorTick;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorTick;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Matriz de Combate (UnitData x UnitData)", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        unitDatabase = (UnitDatabase)EditorGUILayout.ObjectField("Unit Database", unitDatabase, typeof(UnitDatabase), false);
        rpsDatabase = (RPSDatabase)EditorGUILayout.ObjectField("RPS Database", rpsDatabase, typeof(RPSDatabase), false);
        dpqMatchupDatabase = (DPQMatchupDatabase)EditorGUILayout.ObjectField("DPQ Matchup DB", dpqMatchupDatabase, typeof(DPQMatchupDatabase), false);
        weaponPriorityData = (WeaponPriorityData)EditorGUILayout.ObjectField("Weapon Priority", weaponPriorityData, typeof(WeaponPriorityData), false);

        range = Mathf.Max(1, EditorGUILayout.IntField("Alcance (hex)", range));
        includeCounterAttack = EditorGUILayout.ToggleLeft("Incluir revide (inv)", includeCounterAttack);
        includeUnavailableWeapons = EditorGUILayout.ToggleLeft("Listar armas indisponiveis (sem muni/layer/range)", includeUnavailableWeapons);
        autoUpdate = EditorGUILayout.ToggleLeft("Auto Update (recalcula ao mudar dados)", autoUpdate);
        autoUpdateIntervalSeconds = Mathf.Clamp(EditorGUILayout.FloatField("Auto Update Interval (s)", autoUpdateIntervalSeconds), 0.1f, 5f);
        outputRelativePath = EditorGUILayout.TextField("Saida CSV", outputRelativePath);

        EditorGUILayout.HelpBox("DPQ da matriz: sempre padrao (atacante=1, defensor=1).", MessageType.Info);
        EditorGUILayout.Space(6f);

        using (new EditorGUI.DisabledScope(unitDatabase == null))
        {
            if (GUILayout.Button("Gerar Matriz (CSV + botoes de atalho)", GUILayout.Height(32f)))
                RebuildMatrix(writeCsv: true);
        }
        EditorGUILayout.LabelField($"Ultima atualizacao: {lastUpdateLabel}", EditorStyles.miniLabel);

        if (!matrixReady || matrixCells == null || cachedUnits.Count == 0)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Resultados (clique em uma celula para abrir a calculadora simples)", EditorStyles.boldLabel);

        matrixScroll = EditorGUILayout.BeginScrollView(matrixScroll, GUILayout.ExpandHeight(true));
        DrawMatrixGrid();
        EditorGUILayout.EndScrollView();
    }

    private void DrawMatrixGrid()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Unidade", EditorStyles.miniBoldLabel, GUILayout.Width(RowHeaderWidth));
        for (int col = 0; col < cachedUnits.Count; col++)
        {
            GUILayout.Label(ShortLabel(cachedUnits[col]), EditorStyles.miniBoldLabel, GUILayout.Width(CellWidth));
        }
        EditorGUILayout.EndHorizontal();

        for (int row = 0; row < cachedUnits.Count; row++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(ShortLabel(cachedUnits[row]), EditorStyles.miniBoldLabel, GUILayout.Width(RowHeaderWidth));

            for (int col = 0; col < cachedUnits.Count; col++)
            {
                CellView cell = matrixCells[row, col];
                string label = ShortCellLabel(cell.summary);
                if (GUILayout.Button(label, GUILayout.Width(CellWidth), GUILayout.Height(CellHeight)))
                {
                    CombatLargePairCalculatorWindow.Open(
                        cachedUnits[row],
                        cachedUnits[col],
                        range,
                        includeCounterAttack,
                        cell.summary,
                        cell.details);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void RebuildMatrix(bool writeCsv)
    {
        matrixReady = false;
        cachedUnits.Clear();
        matrixCells = null;

        if (unitDatabase == null)
        {
            Debug.LogError("[MatrizCombate] UnitDatabase nao definido.");
            return;
        }

        List<UnitData> units = CollectUnits(unitDatabase);
        if (units.Count == 0)
        {
            Debug.LogWarning("[MatrizCombate] UnitDatabase sem unidades validas.");
            return;
        }

        DPQCombatOutcome atkOutcome = DPQCombatOutcome.Neutro;
        DPQCombatOutcome defOutcome = DPQCombatOutcome.Neutro;
        if (dpqMatchupDatabase != null)
            dpqMatchupDatabase.Resolve(1, 1, out atkOutcome, out defOutcome);

        cachedUnits = units;
        matrixCells = new CellView[units.Count, units.Count];

        StringBuilder csv = null;
        if (writeCsv)
        {
            csv = new StringBuilder(1024 * 64);
            csv.Append("Unidade");
            for (int i = 0; i < units.Count; i++)
            {
                csv.Append(';');
                csv.Append(CsvEscape(GetUnitLabel(units[i])));
            }
            csv.AppendLine();
        }

        for (int row = 0; row < units.Count; row++)
        {
            UnitData attacker = units[row];
            if (writeCsv)
                csv.Append(CsvEscape(GetUnitLabel(attacker)));

            for (int col = 0; col < units.Count; col++)
            {
                UnitData defender = units[col];
                CellView cell = BuildCell(attacker, defender, range, includeCounterAttack, includeUnavailableWeapons, atkOutcome, defOutcome);
                matrixCells[row, col] = cell;

                if (writeCsv)
                {
                    csv.Append(';');
                    csv.Append(CsvEscape(cell.summary));
                }
            }

            if (writeCsv)
                csv.AppendLine();
        }

        if (writeCsv)
        {
            string absolutePath = ResolveAbsolutePath(outputRelativePath);
            string dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(absolutePath, csv.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[MatrizCombate] Gerado: {absolutePath}\nUnidades={units.Count} | Range={range} | Revide={(includeCounterAttack ? "on" : "off")} | DPQ=padrao(1x1)");
        }

        matrixReady = true;
        lastUpdateLabel = DateTime.Now.ToString("HH:mm:ss");
    }

    private void EditorTick()
    {
        if (!autoUpdate)
            return;

        if (unitDatabase == null)
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (now - lastAutoUpdateTime < Mathf.Max(0.1f, autoUpdateIntervalSeconds))
            return;

        RebuildMatrix(writeCsv: false);
        lastAutoUpdateTime = now;
        Repaint();
    }

    private void OnInspectorUpdate()
    {
        if (autoUpdate)
            Repaint();
    }

    private CellView BuildCell(
        UnitData attacker,
        UnitData defender,
        int attackRange,
        bool allowCounter,
        bool keepUnavailable,
        DPQCombatOutcome attackerOutcome,
        DPQCombatOutcome defenderOutcome)
    {
        if (attacker == null || defender == null)
            return new CellView("-", "Par invalido.");

        if (attacker.embarkedWeapons == null || attacker.embarkedWeapons.Count == 0)
            return new CellView("-", "Atacante sem armas embarcadas.");

        StringBuilder details = new StringBuilder(512);
        int attackerHp = Mathf.Max(1, attacker.maxHP);
        int defenderHp = Mathf.Max(1, defender.maxHP);

        details.AppendLine($"Atacante: {GetUnitLabel(attacker)}");
        details.AppendLine($"Defensor: {GetUnitLabel(defender)}");
        details.AppendLine($"Range: {attackRange}");
        details.AppendLine($"DPQ outcome atacante: {attackerOutcome}");
        details.AppendLine($"DPQ outcome defensor: {defenderOutcome}");
        details.AppendLine("Regra de elite: modifiers dos dois lados sempre entram no calculo (mesmo sem revide).");
        details.AppendLine("Regra de revide: so aplica dano de retorno quando houver arma valida de revide.");
        details.AppendLine();

        if (!TryPickPreferredAttackWeapon(attacker, defender, attackRange, out UnitEmbarkedWeapon embarked, out int attackerWeaponIndex, out string attackerWeaponReason))
        {
            details.AppendLine($"Ataque indisponivel: {attackerWeaponReason}");
            return new CellView("-", details.ToString());
        }

        string weaponCode = GetWeaponLabel(embarked.weapon, attackerWeaponIndex);
        details.AppendLine($"Arma atacante escolhida (preferencial): [{weaponCode}] {ResolveWeaponName(embarked.weapon)}");

        WeaponPick bestCounter = allowCounter
            ? PickBestCounterWeapon(defender, attacker, attackRange, defenderOutcome, embarked.weapon)
            : WeaponPick.None;

        int damageToDefender = ComputeDamage(
            attackerHp,
            attacker,
            embarked.weapon,
            defender,
            defender.defense,
            attackerOutcome,
            bestCounter.isValid ? bestCounter.weapon : embarked.weapon,
            out DamageBreakdown attackBreakdown);

        int damageToAttacker = 0;
        details.AppendLine($"A->D [{weaponCode}] {ResolveWeaponName(embarked.weapon)} -> dano={damageToDefender}");
        AppendFormulaSection(details, $"{weaponCode} ATAQUE", attackBreakdown);

        if (allowCounter && bestCounter.isValid)
        {
            damageToAttacker = ComputeDamage(
                defenderHp,
                defender,
                bestCounter.weapon,
                attacker,
                attacker.defense,
                defenderOutcome,
                embarked.weapon,
                out DamageBreakdown counterBreakdown);

            details.AppendLine($"D->A [inv {bestCounter.code}] {ResolveWeaponName(bestCounter.weapon)} -> dano={damageToAttacker}");
            details.AppendLine("    criterio revide: preferencial por Weapon Priority; fallback para primeira arma valida.");
            AppendFormulaSection(details, $"{bestCounter.code} REVIDE", counterBreakdown);
        }
        else if (allowCounter)
        {
            details.AppendLine("    inv -");
            details.AppendLine("    inv detalhe: sem dano de retorno, mas modifiers de elite de ambos os lados foram aplicados no ataque principal.");
        }

        int attackerHpAfter = Mathf.Max(0, attackerHp - damageToAttacker);
        int defenderHpAfter = Mathf.Max(0, defenderHp - damageToDefender);
        string defenderWeaponCode = bestCounter.isValid ? bestCounter.code : "-";
        string segment = $"({weaponCode}) {attackerHpAfter} x {defenderHpAfter} ({defenderWeaponCode})";
        return new CellView(segment, details.ToString());
    }

    private WeaponPick PickBestCounterWeapon(UnitData defender, UnitData attacker, int attackRange, DPQCombatOutcome defenderOutcome, WeaponData attackerWeaponContext)
    {
        if (defender == null || attacker == null || defender.embarkedWeapons == null || defender.embarkedWeapons.Count == 0)
            return WeaponPick.None;

        // Regra do resolver/sensor: revide apenas a distancia 1.
        if (attackRange != 1)
            return WeaponPick.None;

        GameUnitClass attackerClass = attacker.unitClass;
        WeaponPick fallback = WeaponPick.None;
        for (int i = 0; i < defender.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = defender.embarkedWeapons[i];
            if (embarked == null || embarked.weapon == null || embarked.squadAmmunition <= 0)
                continue;

            int minRange = Mathf.Max(0, embarked.GetRangeMin());
            // Regra do resolver/sensor: arma de revide precisa ter alcance minimo 1.
            if (minRange != 1)
                continue;

            if (!embarked.weapon.SupportsOperationOn(attacker.domain, attacker.heightLevel))
                continue;

            WeaponPick current = new WeaponPick(embarked.weapon, GetWeaponLabel(embarked.weapon, i), true);
            if (!fallback.isValid)
                fallback = current;
            if (EvaluateWeaponPriority(weaponPriorityData, embarked.weapon.WeaponCategory, attackerClass))
                return current;
        }

        return fallback;
    }

    private static bool EvaluateWeaponPriority(WeaponPriorityData data, WeaponCategory category, GameUnitClass targetClass)
    {
        return data != null && data.IsPreferredTarget(category, targetClass);
    }

    private bool TryPickPreferredAttackWeapon(
        UnitData attacker,
        UnitData defender,
        int attackRange,
        out UnitEmbarkedWeapon picked,
        out int pickedIndex,
        out string reason)
    {
        picked = null;
        pickedIndex = -1;
        reason = "sem arma valida para o alvo/range.";
        if (attacker == null || defender == null || attacker.embarkedWeapons == null || attacker.embarkedWeapons.Count == 0)
            return false;

        UnitEmbarkedWeapon fallback = null;
        int fallbackIndex = -1;
        for (int i = 0; i < attacker.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = attacker.embarkedWeapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;
            if (embarked.squadAmmunition <= 0)
                continue;
            int minRange = Mathf.Max(0, embarked.GetRangeMin());
            int maxRange = Mathf.Max(minRange, embarked.GetRangeMax());
            if (attackRange < minRange || attackRange > maxRange)
                continue;
            if (!embarked.weapon.SupportsOperationOn(defender.domain, defender.heightLevel))
                continue;

            if (fallback == null)
            {
                fallback = embarked;
                fallbackIndex = i;
            }

            if (EvaluateWeaponPriority(weaponPriorityData, embarked.weapon.WeaponCategory, defender.unitClass))
            {
                picked = embarked;
                pickedIndex = i;
                reason = "arma preferencial selecionada.";
                return true;
            }
        }

        if (fallback != null)
        {
            picked = fallback;
            pickedIndex = fallbackIndex;
            reason = "sem preferencial valida; usada primeira arma valida.";
            return true;
        }

        return false;
    }

    private int ComputeDamage(
        int attackerHp,
        UnitData attackerData,
        WeaponData attackerWeapon,
        UnitData defenderData,
        int defenderBaseDefense,
        DPQCombatOutcome outcome,
        WeaponData defenderWeaponContextForSkill)
    {
        return ComputeDamage(
            attackerHp,
            attackerData,
            attackerWeapon,
            defenderData,
            defenderBaseDefense,
            outcome,
            defenderWeaponContextForSkill,
            out _);
    }

    private int ComputeDamage(
        int attackerHp,
        UnitData attackerData,
        WeaponData attackerWeapon,
        UnitData defenderData,
        int defenderBaseDefense,
        DPQCombatOutcome outcome,
        WeaponData defenderWeaponContextForSkill,
        out DamageBreakdown breakdown)
    {
        breakdown = DamageBreakdown.None;
        if (attackerWeapon == null || attackerData == null || defenderData == null)
            return 0;

        GameUnitClass attackerClass = attackerData.unitClass;
        GameUnitClass defenderClass = defenderData.unitClass;

        int attackRps = 0;
        if (rpsDatabase != null)
            rpsDatabase.TryResolveAttackBonus(attackerClass, attackerWeapon.WeaponCategory, defenderClass, out attackRps, out _, out _);

        int defenseRps = 0;
        if (rpsDatabase != null)
            rpsDatabase.TryResolveDefenseBonus(defenderClass, attackerClass, attackerWeapon.WeaponCategory, out defenseRps, out _, out _);

        WeaponCategory opponentWeaponCategory = defenderWeaponContextForSkill != null
            ? defenderWeaponContextForSkill.WeaponCategory
            : attackerWeapon.WeaponCategory;
        SkillModifierSummary attackerSkill = ResolveSkillModifiers(
            attackerData,
            defenderData,
            attackerWeapon.WeaponCategory,
            opponentWeaponCategory);
        SkillModifierSummary defenderSkill = defenderWeaponContextForSkill != null
            ? ResolveSkillModifiers(
                defenderData,
                attackerData,
                defenderWeaponContextForSkill.WeaponCategory,
                attackerWeapon.WeaponCategory)
            : SkillModifierSummary.None;

        int attackSkillTotal = attackerSkill.ownerAttack + defenderSkill.opponentAttack;
        int defenseSkillTotal = defenderSkill.ownerDefense + attackerSkill.opponentDefense;

        int attackEffective = attackerHp * Mathf.Max(0, attackerWeapon.basicAttack + attackRps + attackSkillTotal);
        int defenderEffectiveDefense = Mathf.Max(1, defenderBaseDefense + defenseRps + defenseSkillTotal); // DPQ padrao = defesa 0
        int rounded = DPQCombatMath.DivideAndRound(attackEffective, defenderEffectiveDefense, outcome);
        int damage = Mathf.Max(0, rounded);

        breakdown = new DamageBreakdown(
            attackerHp,
            attackerWeapon.basicAttack,
            attackRps,
            attackerSkill.ownerAttack,
            defenderSkill.opponentAttack,
            attackEffective,
            defenderBaseDefense,
            0,
            defenseRps,
            defenderSkill.ownerDefense,
            attackerSkill.opponentDefense,
            defenderEffectiveDefense,
            outcome,
            damage);

        return damage;
    }

    private static void AppendFormulaSection(StringBuilder details, string sectionLabel, DamageBreakdown breakdown)
    {
        details.AppendLine($"    [{sectionLabel}]");
        details.AppendLine($"    FA atacante = QTD({breakdown.attackerHp}) x (FA arma({breakdown.weaponPower}) + FA RPS({FormatSigned(breakdown.attackRps)}) + FA Elite Owner({FormatSigned(breakdown.attackerOwnerAttack)}) + FA Elite Opponent({FormatSigned(breakdown.defenderOpponentAttack)})) = {breakdown.attackEffective}");
        details.AppendLine($"    FD defensor = FD base({breakdown.defenderBaseDefense}) + FD DPQ({breakdown.dpqDefense}) + FD RPS({FormatSigned(breakdown.defenseRps)}) + FD Elite Owner({FormatSigned(breakdown.defenderOwnerDefense)}) + FD Elite Opponent({FormatSigned(breakdown.attackerOpponentDefense)}) = {breakdown.defenderEffectiveDefense}");
        details.AppendLine($"    Dano = RoundOutcome({breakdown.attackEffective}/{Mathf.Max(1, breakdown.defenderEffectiveDefense)}, {breakdown.outcome}) => {breakdown.damageApplied}");
    }

    private static string FormatSigned(int value)
    {
        return value.ToString("+0;-0;+0");
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

        int ownerAttack = 0;
        int ownerDefense = 0;
        int opponentAttack = 0;
        int opponentDefense = 0;

        for (int i = 0; i < ownerData.combatModifiers.Count; i++)
        {
            CombatModifierData modifier = ownerData.combatModifiers[i];
            if (modifier == null)
                continue;

            if (!modifier.TryGetCombatRpsModifiers(
                ownerElite,
                ownerWeaponCategory,
                opponentWeaponCategory,
                opponentClass,
                opponentElite,
                out int ownerAtkMod,
                out int ownerDefMod,
                out int opponentAtkMod,
                out int opponentDefMod,
                out _))
            {
                continue;
            }

            ownerAttack += ownerAtkMod;
            ownerDefense += ownerDefMod;
            opponentAttack += opponentAtkMod;
            opponentDefense += opponentDefMod;
        }

        return new SkillModifierSummary(ownerAttack, ownerDefense, opponentAttack, opponentDefense);
    }

    private static List<UnitData> CollectUnits(UnitDatabase db)
    {
        List<UnitData> result = new List<UnitData>();
        if (db == null || db.Units == null)
            return result;

        for (int i = 0; i < db.Units.Count; i++)
        {
            UnitData unit = db.Units[i];
            if (unit == null || string.IsNullOrWhiteSpace(unit.id))
                continue;
            result.Add(unit);
        }

        return result;
    }

    private void TryAutoAssignDatabases()
    {
        if (unitDatabase == null)
            unitDatabase = FindFirstAssetOfType<UnitDatabase>();
        if (rpsDatabase == null)
            rpsDatabase = FindFirstAssetOfType<RPSDatabase>();
        if (dpqMatchupDatabase == null)
            dpqMatchupDatabase = FindFirstAssetOfType<DPQMatchupDatabase>();
        if (weaponPriorityData == null)
            weaponPriorityData = FindFirstAssetOfType<WeaponPriorityData>();
    }

    private static T FindFirstAssetOfType<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static string GetUnitLabel(UnitData unit)
    {
        if (unit == null)
            return "(null)";
        if (!string.IsNullOrWhiteSpace(unit.apelido))
            return unit.apelido.Trim();
        if (!string.IsNullOrWhiteSpace(unit.displayName))
            return unit.displayName.Trim();
        return !string.IsNullOrWhiteSpace(unit.id) ? unit.id.Trim() : unit.name;
    }

    private static string ResolveWeaponName(WeaponData weapon)
    {
        if (weapon == null)
            return "(sem arma)";
        if (!string.IsNullOrWhiteSpace(weapon.displayName))
            return weapon.displayName;
        if (!string.IsNullOrWhiteSpace(weapon.id))
            return weapon.id;
        return weapon.name;
    }

    private static string GetWeaponCode(int index)
    {
        if (index == 0) return "P";
        if (index == 1) return "S";
        if (index == 2) return "T";
        return $"W{index + 1}";
    }

    private static string GetWeaponLabel(WeaponData weapon, int index)
    {
        if (weapon != null && !string.IsNullOrWhiteSpace(weapon.apelido))
            return weapon.apelido.Trim();
        return GetWeaponCode(index);
    }

    private static string ShortLabel(UnitData unit)
    {
        string text = GetUnitLabel(unit);
        if (text.Length <= 12)
            return text;
        return text.Substring(0, 12) + "...";
    }

    private static string ShortCellLabel(string value)
    {
        string text = string.IsNullOrWhiteSpace(value) ? "-" : value;
        if (text.Length <= 24)
            return text;
        return text.Substring(0, 24) + "...";
    }

    private static string ResolveAbsolutePath(string relativeOrAbsolutePath)
    {
        string text = string.IsNullOrWhiteSpace(relativeOrAbsolutePath) ? "docs/COMBAT_MATRIX.csv" : relativeOrAbsolutePath.Trim();
        if (Path.IsPathRooted(text))
            return text;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, text.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
    }

    private static string CsvEscape(string value)
    {
        string text = value ?? string.Empty;
        if (!text.Contains(";") && !text.Contains("\"") && !text.Contains("\n") && !text.Contains("\r"))
            return text;

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private readonly struct WeaponPick
    {
        public static WeaponPick None => new WeaponPick(null, "-", false);

        public readonly WeaponData weapon;
        public readonly string code;
        public readonly bool isValid;

        public WeaponPick(WeaponData weapon, string code, bool isValid)
        {
            this.weapon = weapon;
            this.code = code;
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

    private readonly struct DamageBreakdown
    {
        public static DamageBreakdown None => new DamageBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, DPQCombatOutcome.Neutro, 0);

        public readonly int attackerHp;
        public readonly int weaponPower;
        public readonly int attackRps;
        public readonly int attackerOwnerAttack;
        public readonly int defenderOpponentAttack;
        public readonly int attackEffective;
        public readonly int defenderBaseDefense;
        public readonly int dpqDefense;
        public readonly int defenseRps;
        public readonly int defenderOwnerDefense;
        public readonly int attackerOpponentDefense;
        public readonly int defenderEffectiveDefense;
        public readonly DPQCombatOutcome outcome;
        public readonly int damageApplied;

        public DamageBreakdown(
            int attackerHp,
            int weaponPower,
            int attackRps,
            int attackerOwnerAttack,
            int defenderOpponentAttack,
            int attackEffective,
            int defenderBaseDefense,
            int dpqDefense,
            int defenseRps,
            int defenderOwnerDefense,
            int attackerOpponentDefense,
            int defenderEffectiveDefense,
            DPQCombatOutcome outcome,
            int damageApplied)
        {
            this.attackerHp = attackerHp;
            this.weaponPower = weaponPower;
            this.attackRps = attackRps;
            this.attackerOwnerAttack = attackerOwnerAttack;
            this.defenderOpponentAttack = defenderOpponentAttack;
            this.attackEffective = attackEffective;
            this.defenderBaseDefense = defenderBaseDefense;
            this.dpqDefense = dpqDefense;
            this.defenseRps = defenseRps;
            this.defenderOwnerDefense = defenderOwnerDefense;
            this.attackerOpponentDefense = attackerOpponentDefense;
            this.defenderEffectiveDefense = defenderEffectiveDefense;
            this.outcome = outcome;
            this.damageApplied = damageApplied;
        }
    }

    private readonly struct CellView
    {
        public readonly string summary;
        public readonly string details;

        public CellView(string summary, string details)
        {
            this.summary = summary;
            this.details = details;
        }
    }
}
