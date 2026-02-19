using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CombatMatrixWindow : EditorWindow
{
    private const int BaselineDpqIndex = 1; // DPQ_Padrao
    private static readonly DpqPreset[] DpqPresets =
    {
        new DpqPreset("DPQ_Desfavoravel", DPQQualidadeDePosicao.Unfavorable),
        new DpqPreset("DPQ_Padrao", DPQQualidadeDePosicao.Default),
        new DpqPreset("DPQ_Melhorado", DPQQualidadeDePosicao.Improved),
        new DpqPreset("DPQ_Favoravel", DPQQualidadeDePosicao.Favorable),
        new DpqPreset("DPQ_Unico", DPQQualidadeDePosicao.Unique)
    };

    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private WeaponPriorityData weaponPriorityData;
    [SerializeField] private DPQMatchupDatabase dpqMatchupDatabase;
    [SerializeField] private RPSDatabase rpsDatabase;
    [SerializeField] private UnitManager attackerUnit;
    [SerializeField] private UnitManager defenderUnit;
    [SerializeField] private SensorMovementMode movementMode = SensorMovementMode.MoveuParado;
    [SerializeField] private int selectedPairWeaponIndex;
    [SerializeField] private int selectedDefenderCounterIndex;
    [SerializeField] private bool logToConsole;

    private readonly List<PodeMirarTargetOption> sensorValidResults = new List<PodeMirarTargetOption>();
    private readonly List<PodeMirarInvalidOption> sensorInvalidResults = new List<PodeMirarInvalidOption>();
    private readonly List<PodeMirarTargetOption> currentPairOptions = new List<PodeMirarTargetOption>();
    private readonly List<DefenderCounterChoice> currentDefenderCounterChoices = new List<DefenderCounterChoice>();

    private readonly MatrixCellResult[,] matrix = new MatrixCellResult[5, 5];
    private bool matrixReady;
    private Vector2 scroll;
    private string status = "Ready.";
    private string selectedCellTitle = string.Empty;
    private string selectedCellLog = string.Empty;
    private GUIStyle baselineCellButtonStyle;

    [MenuItem("Tools/Matriz de Combate")]
    public static void OpenWindow()
    {
        GetWindow<CombatMatrixWindow>("Matriz de Combate");
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Matriz de Combate", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Forca os 5 DPQs no atacante e defensor, simula as 25 combinacoes e mostra HP restante AxD.", MessageType.Info);

        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        weaponPriorityData = (WeaponPriorityData)EditorGUILayout.ObjectField("Weapon Priority Data", weaponPriorityData, typeof(WeaponPriorityData), false);
        dpqMatchupDatabase = (DPQMatchupDatabase)EditorGUILayout.ObjectField("DPQ Matchup DB", dpqMatchupDatabase, typeof(DPQMatchupDatabase), false);
        rpsDatabase = (RPSDatabase)EditorGUILayout.ObjectField("RPS Database", rpsDatabase, typeof(RPSDatabase), false);
        movementMode = (SensorMovementMode)EditorGUILayout.EnumPopup("Modo Sensor", movementMode);
        logToConsole = EditorGUILayout.ToggleLeft("Log no Console", logToConsole);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Limpar"))
            ClearState();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        attackerUnit = (UnitManager)EditorGUILayout.ObjectField("Atacante", attackerUnit, typeof(UnitManager), true);
        defenderUnit = (UnitManager)EditorGUILayout.ObjectField("Defensor", defenderUnit, typeof(UnitManager), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado como Atacante"))
            TryUseCurrentSelectionAsAttacker();
        if (GUILayout.Button("Usar Selecionado como Defensor"))
            TryUseCurrentSelectionAsDefender();
        EditorGUILayout.EndHorizontal();

        DrawPairWeaponSelector();

        if (GUILayout.Button("Gerar Matriz (25 combinacoes)"))
            GenerateMatrix();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(status, MessageType.None);

        if (matrixReady)
            DrawMatrix();

        if (!string.IsNullOrWhiteSpace(selectedCellLog))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(selectedCellTitle, EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(200f));
            EditorGUILayout.TextArea(selectedCellLog, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawPairWeaponSelector()
    {
        if (currentPairOptions.Count <= 0)
        {
            EditorGUILayout.HelpBox("Sem opcoes validas para o par atual.", MessageType.None);
            selectedPairWeaponIndex = 0;
            selectedDefenderCounterIndex = 0;
            currentDefenderCounterChoices.Clear();
            return;
        }

        string[] labels = new string[currentPairOptions.Count];
        for (int i = 0; i < currentPairOptions.Count; i++)
        {
            PodeMirarTargetOption option = currentPairOptions[i];
            labels[i] = $"{i + 1}. {ResolveWeaponName(option.weapon)} | dist={option.distance}";
        }

        selectedPairWeaponIndex = Mathf.Clamp(selectedPairWeaponIndex, 0, currentPairOptions.Count - 1);
        selectedPairWeaponIndex = EditorGUILayout.Popup("Opcao de Arma (Par)", selectedPairWeaponIndex, labels);

        PodeMirarTargetOption selectedOption = currentPairOptions[selectedPairWeaponIndex];
        RefreshDefenderCounterChoices(selectedOption);

        string[] defenderLabels = new string[currentDefenderCounterChoices.Count];
        for (int i = 0; i < currentDefenderCounterChoices.Count; i++)
            defenderLabels[i] = currentDefenderCounterChoices[i].label;

        selectedDefenderCounterIndex = Mathf.Clamp(selectedDefenderCounterIndex, 0, currentDefenderCounterChoices.Count - 1);
        selectedDefenderCounterIndex = EditorGUILayout.Popup("Arma de Revide (Defensor)", selectedDefenderCounterIndex, defenderLabels);
    }

    private void GenerateMatrix()
    {
        matrixReady = false;
        selectedCellTitle = string.Empty;
        selectedCellLog = string.Empty;
        currentPairOptions.Clear();
        sensorValidResults.Clear();
        sensorInvalidResults.Clear();
        currentDefenderCounterChoices.Clear();

        if (attackerUnit == null || defenderUnit == null)
        {
            status = "Falha: defina atacante e defensor.";
            return;
        }

        if (attackerUnit == defenderUnit)
        {
            status = "Falha: atacante e defensor nao podem ser a mesma unidade.";
            return;
        }

        Tilemap board = ResolveBoardTilemap(attackerUnit, defenderUnit);
        if (board == null)
        {
            status = "Falha: sem Tilemap para rodar o sensor.";
            return;
        }

        PodeMirarSensor.CollectTargets(
            attackerUnit,
            board,
            terrainDatabase,
            movementMode,
            sensorValidResults,
            sensorInvalidResults,
            weaponPriorityData);

        for (int i = 0; i < sensorValidResults.Count; i++)
        {
            PodeMirarTargetOption option = sensorValidResults[i];
            if (option != null && option.attackerUnit == attackerUnit && option.targetUnit == defenderUnit)
                currentPairOptions.Add(option);
        }

        if (currentPairOptions.Count <= 0)
        {
            status = BuildInvalidPairReport(attackerUnit, defenderUnit, sensorInvalidResults);
            if (logToConsole)
                Debug.Log(status);
            return;
        }

        selectedPairWeaponIndex = Mathf.Clamp(selectedPairWeaponIndex, 0, currentPairOptions.Count - 1);
        PodeMirarTargetOption selectedOption = currentPairOptions[selectedPairWeaponIndex];
        RefreshDefenderCounterChoices(selectedOption);
        selectedDefenderCounterIndex = Mathf.Clamp(selectedDefenderCounterIndex, 0, currentDefenderCounterChoices.Count - 1);
        DefenderCounterChoice selectedCounterChoice = currentDefenderCounterChoices[selectedDefenderCounterIndex];

        for (int row = 0; row < DpqPresets.Length; row++)
        {
            for (int col = 0; col < DpqPresets.Length; col++)
                matrix[row, col] = SimulateCell(selectedOption, selectedCounterChoice, DpqPresets[row], DpqPresets[col]);
        }

        matrixReady = true;
        status = $"Matriz pronta para {attackerUnit.name} -> {defenderUnit.name} ({currentPairOptions.Count} opcao(oes) valida(s) no par).";
        status += $" Opcao usada: {selectedPairWeaponIndex + 1} ({ResolveWeaponName(selectedOption.weapon)}).";
        status += $" Revide: {selectedCounterChoice.label}.";
        if (logToConsole)
            Debug.Log(status);
    }

    private void DrawMatrix()
    {
        EnsureStyles();
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("[Atacante \\ Defensor]", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("DPQ", GUILayout.Width(150f));
        for (int col = 0; col < DpqPresets.Length; col++)
            GUILayout.Label(DpqPresets[col].label, EditorStyles.miniBoldLabel, GUILayout.Width(120f));
        EditorGUILayout.EndHorizontal();

        for (int row = 0; row < DpqPresets.Length; row++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(DpqPresets[row].label, EditorStyles.miniBoldLabel, GUILayout.Width(150f));

            for (int col = 0; col < DpqPresets.Length; col++)
            {
                MatrixCellResult cell = matrix[row, col];
                string label = $"{cell.attackerHpAfter}x{cell.defenderHpAfter}";
                bool isBaselineCell = row == BaselineDpqIndex && col == BaselineDpqIndex;
                GUIStyle style = isBaselineCell ? baselineCellButtonStyle : GUI.skin.button;
                Color prevColor = GUI.color;
                if (isBaselineCell)
                    GUI.color = new Color(1f, 0.92f, 0.55f, 1f);

                if (GUILayout.Button(label, style, GUILayout.Width(120f)))
                {
                    selectedCellTitle = $"{DpqPresets[row].label} x {DpqPresets[col].label}";
                    selectedCellLog = cell.log;
                    if (logToConsole)
                        Debug.Log(cell.log);
                }

                if (isBaselineCell)
                    GUI.color = prevColor;
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void EnsureStyles()
    {
        if (baselineCellButtonStyle != null)
            return;

        baselineCellButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold
        };
    }

    private MatrixCellResult SimulateCell(PodeMirarTargetOption option, DefenderCounterChoice counterChoice, DpqPreset forcedAttackerDpq, DpqPreset forcedDefenderDpq)
    {
        UnitManager attacker = option.attackerUnit;
        UnitManager defender = option.targetUnit;
        int attackerHpBefore = Mathf.Max(0, attacker != null ? attacker.CurrentHP : 0);
        int defenderHpBefore = Mathf.Max(0, defender != null ? defender.CurrentHP : 0);

        bool defenderCanCounter = counterChoice.canCounter;
        WeaponData defenderCounterWeapon = counterChoice.weapon;
        int defenderCounterEmbarkedIndex = counterChoice.embarkedWeaponIndex;
        string defenderCounterLabel = counterChoice.label;

        int attackerWeaponPower = option.weapon != null ? Mathf.Max(0, option.weapon.basicAttack) : 0;
        int defenderWeaponPower = defenderCanCounter && defenderCounterWeapon != null
            ? Mathf.Max(0, defenderCounterWeapon.basicAttack)
            : 0;

        GameUnitClass attackerClass = ResolveUnitClass(attacker, out bool attackerClassFromData);
        GameUnitClass defenderClass = ResolveUnitClass(defender, out bool defenderClassFromData);
        WeaponCategory attackerCategory = ResolveWeaponCategory(option.weapon);
        WeaponCategory defenderCategory = ResolveWeaponCategory(defenderCounterWeapon);

        RpsBonusInfo attackerAttackRps = ResolveAttackRps(attackerClass, attackerCategory, defenderClass);
        RpsBonusInfo defenderAttackRps = defenderCanCounter
            ? ResolveAttackRps(defenderClass, defenderCategory, attackerClass)
            : RpsBonusInfo.None;

        int attackerAttackEffective = attackerHpBefore * Mathf.Max(0, attackerWeaponPower + attackerAttackRps.value);
        int defenderAttackEffective = defenderCanCounter
            ? defenderHpBefore * Mathf.Max(0, defenderWeaponPower + defenderAttackRps.value)
            : 0;

        int attackerBaseDefense = GetUnitBaseDefense(attacker);
        int defenderBaseDefense = GetUnitBaseDefense(defender);

        RpsBonusInfo attackerDefenseRps = defenderCanCounter
            ? ResolveDefenseRps(attackerClass, defenderClass, defenderCategory)
            : RpsBonusInfo.None;
        RpsBonusInfo defenderDefenseRps = ResolveDefenseRps(defenderClass, attackerClass, attackerCategory);

        int attackerEffectiveDefense = attackerBaseDefense + forcedAttackerDpq.defenseBonus + attackerDefenseRps.value;
        int defenderEffectiveDefense = defenderBaseDefense + forcedDefenderDpq.defenseBonus + defenderDefenseRps.value;

        DPQCombatOutcome attackerOutcome = DPQCombatOutcome.Neutro;
        DPQCombatOutcome defenderOutcome = DPQCombatOutcome.Neutro;
        if (dpqMatchupDatabase != null)
            dpqMatchupDatabase.Resolve(forcedAttackerDpq.points, forcedDefenderDpq.points, out attackerOutcome, out defenderOutcome);

        int defenderSafeDefense = Mathf.Max(1, defenderEffectiveDefense);
        int attackerSafeDefense = Mathf.Max(1, attackerEffectiveDefense);
        int roundedOnDefender = DPQCombatMath.DivideAndRound(attackerAttackEffective, defenderSafeDefense, attackerOutcome);
        int roundedOnAttacker = defenderCanCounter
            ? DPQCombatMath.DivideAndRound(defenderAttackEffective, attackerSafeDefense, defenderOutcome)
            : 0;

        int appliedOnDefender = Mathf.Max(0, roundedOnDefender);
        int appliedOnAttacker = Mathf.Max(0, roundedOnAttacker);
        int defenderHpAfter = Mathf.Max(0, defenderHpBefore - appliedOnDefender);
        int attackerHpAfter = Mathf.Max(0, attackerHpBefore - appliedOnAttacker);

        StringBuilder log = new StringBuilder(900);
        log.AppendLine("[Matriz de Combate] Simulacao de celula");
        log.AppendLine($"Entrada: {SafeName(attacker)} -> {SafeName(defender)}");
        log.AppendLine($"Classe atacante usada no RPS: {attackerClass} {(attackerClassFromData ? "(UnitData)" : "(fallback)")}");
        log.AppendLine($"Classe defensor usada no RPS: {defenderClass} {(defenderClassFromData ? "(UnitData)" : "(fallback)")}");
        log.AppendLine($"Chave RPS ataque atacante: {attackerClass} + {attackerCategory} -> {defenderClass}");
        log.AppendLine($"Chave RPS ataque defensor: {defenderClass} + {defenderCategory} -> {attackerClass}");
        log.AppendLine($"Opcao selecionada (par): {selectedPairWeaponIndex + 1}/{Mathf.Max(1, currentPairOptions.Count)}");
        log.AppendLine($"Opcao de revide selecionada: {selectedDefenderCounterIndex + 1}/{Mathf.Max(1, currentDefenderCounterChoices.Count)}");
        log.AppendLine($"Indice arma embarcada atacante: {option.embarkedWeaponIndex}");
        log.AppendLine($"DPQ forzado atacante: {forcedAttackerDpq.label} (pontos={forcedAttackerDpq.points}, defesa={forcedAttackerDpq.defenseBonus})");
        log.AppendLine($"DPQ forzado defensor: {forcedDefenderDpq.label} (pontos={forcedDefenderDpq.points}, defesa={forcedDefenderDpq.defenseBonus})");
        log.AppendLine($"Arma atacante: {ResolveWeaponName(option.weapon)}");
        log.AppendLine($"Categoria arma atacante: {ResolveWeaponCategory(option.weapon)}");
        log.AppendLine($"Indice arma embarcada defensor (revide): {defenderCounterEmbarkedIndex}");
        log.AppendLine($"Arma defensor: {ResolveWeaponName(defenderCounterWeapon)}");
        log.AppendLine($"Categoria arma defensor: {ResolveWeaponCategory(defenderCounterWeapon)}");
        log.AppendLine($"Revide: {(defenderCanCounter ? "sim" : "nao")} | origem={defenderCounterLabel}");
        log.AppendLine();
        log.AppendLine("1) Ataque efetivo com RPS");
        log.AppendLine($"- Atacante: HP({attackerHpBefore}) x (Arma({attackerWeaponPower}) + RPSAtaque({FormatSigned(attackerAttackRps.value)})) = {attackerAttackEffective}");
        log.AppendLine($"- Defensor: HP({defenderHpBefore}) x (Arma({defenderWeaponPower}) + RPSAtaque({FormatSigned(defenderAttackRps.value)})) = {defenderAttackEffective}");
        log.AppendLine($"- RPS ataque atacante: {attackerAttackRps.summary}");
        log.AppendLine($"- RPS ataque defensor: {defenderAttackRps.summary}");
        log.AppendLine("2) Defesa efetiva com RPS");
        log.AppendLine($"- Atacante: defesaUnidade({attackerBaseDefense}) + defesaDPQ({forcedAttackerDpq.defenseBonus}) + RPSDefesa({FormatSigned(attackerDefenseRps.value)}) = {attackerEffectiveDefense}");
        log.AppendLine($"- Defensor: defesaUnidade({defenderBaseDefense}) + defesaDPQ({forcedDefenderDpq.defenseBonus}) + RPSDefesa({FormatSigned(defenderDefenseRps.value)}) = {defenderEffectiveDefense}");
        log.AppendLine($"- RPS defesa atacante: {attackerDefenseRps.summary}");
        log.AppendLine($"- RPS defesa defensor: {defenderDefenseRps.summary}");
        log.AppendLine("3) Matchup DPQ");
        log.AppendLine($"- Diferenca: {forcedAttackerDpq.points} - {forcedDefenderDpq.points} = {forcedAttackerDpq.points - forcedDefenderDpq.points}");
        log.AppendLine($"- Outcome atacante: {attackerOutcome}");
        log.AppendLine($"- Outcome defensor: {defenderOutcome}");
        log.AppendLine("4) Resultado");
        log.AppendLine($"- Regra defensor: {BuildRoundingExplanation(attackerAttackEffective, defenderSafeDefense, attackerOutcome, roundedOnDefender)}");
        log.AppendLine($"- Regra atacante: {BuildRoundingExplanation(defenderAttackEffective, attackerSafeDefense, defenderOutcome, roundedOnAttacker)}");
        log.AppendLine($"- Elim no defensor: rounded={roundedOnDefender} -> aplicado={appliedOnDefender}");
        log.AppendLine($"- Elim no atacante: rounded={roundedOnAttacker} -> aplicado={appliedOnAttacker}");
        log.AppendLine($"- HP defensor: {defenderHpBefore} -> {defenderHpAfter}");
        log.AppendLine($"- HP atacante: {attackerHpBefore} -> {attackerHpAfter}");

        return new MatrixCellResult(attackerHpAfter, defenderHpAfter, log.ToString());
    }

    private void AutoDetectContext()
    {
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();
        if (weaponPriorityData == null)
            weaponPriorityData = FindFirstAsset<WeaponPriorityData>();
        if (dpqMatchupDatabase == null)
            dpqMatchupDatabase = FindFirstAsset<DPQMatchupDatabase>();
        if (rpsDatabase == null)
            rpsDatabase = FindFirstAsset<RPSDatabase>();
    }

    private static T FindFirstAsset<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }

    private static UnitManager ResolveSelectedUnit()
    {
        if (Selection.activeGameObject == null)
            return null;

        UnitManager unit = Selection.activeGameObject.GetComponent<UnitManager>();
        if (unit == null)
            unit = Selection.activeGameObject.GetComponentInParent<UnitManager>();
        return unit;
    }

    private void TryUseCurrentSelectionAsAttacker()
    {
        UnitManager selected = ResolveSelectedUnit();
        if (selected != null)
            attackerUnit = selected;
    }

    private void TryUseCurrentSelectionAsDefender()
    {
        UnitManager selected = ResolveSelectedUnit();
        if (selected != null)
            defenderUnit = selected;
    }

    private void ClearState()
    {
        attackerUnit = null;
        defenderUnit = null;
        selectedPairWeaponIndex = 0;
        selectedDefenderCounterIndex = 0;
        currentPairOptions.Clear();
        currentDefenderCounterChoices.Clear();
        sensorValidResults.Clear();
        sensorInvalidResults.Clear();
        matrixReady = false;
        status = "Ready.";
        selectedCellTitle = string.Empty;
        selectedCellLog = string.Empty;
    }

    private static Tilemap ResolveBoardTilemap(UnitManager attacker, UnitManager defender)
    {
        if (attacker != null && attacker.BoardTilemap != null)
            return attacker.BoardTilemap;
        if (defender != null && defender.BoardTilemap != null)
            return defender.BoardTilemap;
        return Object.FindAnyObjectByType<Tilemap>();
    }

    private static int GetUnitBaseDefense(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return data.defense;
        return 0;
    }

    private static GameUnitClass ResolveUnitClass(UnitManager unit, out bool fromData)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
        {
            fromData = true;
            return data.unitClass;
        }

        fromData = false;
        return GameUnitClass.Infantry;
    }

    private static WeaponCategory ResolveWeaponCategory(WeaponData weapon)
    {
        return weapon != null ? weapon.WeaponCategory : WeaponCategory.AntiInfantaria;
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

    private string BuildInvalidPairReport(UnitManager attacker, UnitManager defender, List<PodeMirarInvalidOption> invalidOptions)
    {
        StringBuilder sb = new StringBuilder(500);
        sb.AppendLine("[Matriz de Combate] Par invalido para o sensor");
        sb.AppendLine($"Atacante: {SafeName(attacker)}");
        sb.AppendLine($"Defensor: {SafeName(defender)}");
        sb.AppendLine($"Modo sensor: {movementMode}");

        int reasons = 0;
        for (int i = 0; i < invalidOptions.Count; i++)
        {
            PodeMirarInvalidOption item = invalidOptions[i];
            if (item == null || item.attackerUnit != attacker || item.targetUnit != defender)
                continue;

            reasons++;
            sb.AppendLine($"{reasons}) arma={ResolveWeaponName(item.weapon)} | dist={item.distance} | motivo={item.reason}");
        }

        if (reasons == 0)
            sb.AppendLine("- Sem motivo especifico retornado. Verifique alcance/layer/municao.");

        return sb.ToString();
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

    private static string SafeName(UnitManager unit)
    {
        return unit != null ? unit.name : "(null)";
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

    private void RefreshDefenderCounterChoices(PodeMirarTargetOption selectedOption)
    {
        currentDefenderCounterChoices.Clear();

        if (selectedOption == null)
        {
            currentDefenderCounterChoices.Add(DefenderCounterChoice.NoCounter("Auto (sem opcao)"));
            selectedDefenderCounterIndex = 0;
            return;
        }

        if (selectedOption.defenderCanCounterAttack)
            currentDefenderCounterChoices.Add(DefenderCounterChoice.AutoCounter(selectedOption.defenderCounterWeapon, selectedOption.defenderCounterEmbarkedWeaponIndex));
        else
            currentDefenderCounterChoices.Add(DefenderCounterChoice.NoCounter($"Auto (sem revide: {selectedOption.defenderCounterReason})"));

        UnitManager attacker = selectedOption.attackerUnit;
        UnitManager defender = selectedOption.targetUnit;
        IReadOnlyList<UnitEmbarkedWeapon> defenderWeapons = defender != null ? defender.GetEmbarkedWeapons() : null;

        if (attacker != null && defenderWeapons != null)
        {
            for (int i = 0; i < defenderWeapons.Count; i++)
            {
                if (!TryBuildManualCounterChoice(attacker, defenderWeapons[i], i, selectedOption.distance, out DefenderCounterChoice choice))
                    continue;

                currentDefenderCounterChoices.Add(choice);
            }
        }

        currentDefenderCounterChoices.Add(DefenderCounterChoice.NoCounter("Sem revide (forcado)"));
        selectedDefenderCounterIndex = Mathf.Clamp(selectedDefenderCounterIndex, 0, currentDefenderCounterChoices.Count - 1);
    }

    private static bool TryBuildManualCounterChoice(UnitManager attacker, UnitEmbarkedWeapon embarked, int embarkedIndex, int distance, out DefenderCounterChoice choice)
    {
        choice = default(DefenderCounterChoice);
        if (attacker == null || embarked == null || embarked.weapon == null)
            return false;

        if (distance != 1)
            return false;

        if (embarked.GetRangeMin() != 1)
            return false;

        if (embarked.squadAmmunition <= 0)
            return false;

        if (!embarked.weapon.SupportsOperationOn(attacker.GetDomain(), attacker.GetHeightLevel()))
            return false;

        choice = DefenderCounterChoice.ManualCounter(embarked.weapon, embarkedIndex);
        return true;
    }

    private readonly struct MatrixCellResult
    {
        public readonly int attackerHpAfter;
        public readonly int defenderHpAfter;
        public readonly string log;

        public MatrixCellResult(int attackerHpAfter, int defenderHpAfter, string log)
        {
            this.attackerHpAfter = attackerHpAfter;
            this.defenderHpAfter = defenderHpAfter;
            this.log = log;
        }
    }

    private readonly struct DefenderCounterChoice
    {
        public readonly bool canCounter;
        public readonly WeaponData weapon;
        public readonly int embarkedWeaponIndex;
        public readonly string label;

        public DefenderCounterChoice(bool canCounter, WeaponData weapon, int embarkedWeaponIndex, string label)
        {
            this.canCounter = canCounter;
            this.weapon = weapon;
            this.embarkedWeaponIndex = embarkedWeaponIndex;
            this.label = label;
        }

        public static DefenderCounterChoice AutoCounter(WeaponData weapon, int embarkedIndex)
        {
            return new DefenderCounterChoice(
                true,
                weapon,
                embarkedIndex,
                $"Auto ({ResolveWeaponName(weapon)})");
        }

        public static DefenderCounterChoice ManualCounter(WeaponData weapon, int embarkedIndex)
        {
            return new DefenderCounterChoice(
                true,
                weapon,
                embarkedIndex,
                $"Manual [{embarkedIndex}] {ResolveWeaponName(weapon)}");
        }

        public static DefenderCounterChoice NoCounter(string reason)
        {
            return new DefenderCounterChoice(false, null, -1, reason);
        }
    }

    private readonly struct DpqPreset
    {
        public readonly string label;
        public readonly int points;
        public readonly int defenseBonus;

        public DpqPreset(string label, DPQQualidadeDePosicao quality)
        {
            this.label = label;
            points = DPQData.GetPontosPadrao(quality);
            defenseBonus = DPQData.GetDefesaPadrao(quality);
        }
    }

    private readonly struct RpsBonusInfo
    {
        public static RpsBonusInfo None => new RpsBonusInfo(0, "nao aplicavel");

        public readonly int value;
        public readonly string summary;

        public RpsBonusInfo(int value, string source)
        {
            this.value = value;
            summary = $"{source} | bonus={FormatSigned(value)}";
        }

        public static RpsBonusInfo NoneWithReason(string reason)
        {
            return new RpsBonusInfo(0, $"RPS +0 ({reason})");
        }
    }
}
