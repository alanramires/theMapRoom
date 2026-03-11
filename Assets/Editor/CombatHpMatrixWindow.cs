using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CombatHpMatrixWindow : EditorWindow
{
    private const int HpGridSize = 10;

    [SerializeField] private UnitDatabase unitDatabase;
    [SerializeField] private RPSDatabase rpsDatabase;
    [SerializeField] private DPQMatchupDatabase dpqMatchupDatabase;
    [SerializeField] private WeaponPriorityData weaponPriorityData;
    [SerializeField] private int distance = 1;
    [SerializeField] private int attackerUnitIndex;
    [SerializeField] private int defenderUnitIndex;
    [SerializeField] private int attackerWeaponIndex;
    [SerializeField] private int defenderWeaponIndex;
    [SerializeField] private int selectedAttackerEmbarkedWeaponIndex = -2;
    [SerializeField] private int selectedDefenderEmbarkedWeaponIndex = -2;
    [SerializeField] private bool autoUpdate = true;
    [SerializeField] private bool logToConsole;

    private readonly List<UnitData> units = new List<UnitData>();
    private readonly List<WeaponOption> attackWeaponOptions = new List<WeaponOption>();
    private readonly List<CounterWeaponOption> defenderWeaponOptions = new List<CounterWeaponOption>();
    private readonly MatrixCellResult[,] matrix = new MatrixCellResult[HpGridSize, HpGridSize];

    private bool matrixReady;
    private bool pendingAutoRefresh;
    private Vector2 scroll;
    private string status = "Ready.";
    private string selectedCellTitle = string.Empty;
    private string selectedCellLog = string.Empty;

    [MenuItem("Tools/Combat/Matriz de HP")]
    private static void OpenWindow()
    {
        GetWindow<CombatHpMatrixWindow>("Matriz de HP");
    }

    private void OnEnable()
    {
        AutoDetectContext();
        RefreshUnits();
        RefreshAttackWeaponOptions();
        RefreshDefenderWeaponOptions();
        EditorApplication.projectChanged += OnProjectChanged;
        EditorApplication.update += OnEditorUpdate;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        Undo.postprocessModifications += OnPostprocessModifications;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
        EditorApplication.update -= OnEditorUpdate;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.postprocessModifications -= OnPostprocessModifications;
    }

    private void OnGUI()
    {
        UnitDatabase previousUnitDatabase = unitDatabase;
        int previousDistance = distance;

        EditorGUILayout.LabelField("Matriz de HP (UnitData x UnitData)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("DPQ fixo em padrao (1x1). Linha = HP inicial atacante, coluna = HP inicial defensor.", MessageType.Info);

        unitDatabase = (UnitDatabase)EditorGUILayout.ObjectField("Unit Database", unitDatabase, typeof(UnitDatabase), false);
        rpsDatabase = (RPSDatabase)EditorGUILayout.ObjectField("RPS Database", rpsDatabase, typeof(RPSDatabase), false);
        dpqMatchupDatabase = (DPQMatchupDatabase)EditorGUILayout.ObjectField("DPQ Matchup DB", dpqMatchupDatabase, typeof(DPQMatchupDatabase), false);
        weaponPriorityData = (WeaponPriorityData)EditorGUILayout.ObjectField("Weapon Priority", weaponPriorityData, typeof(WeaponPriorityData), false);
        distance = Mathf.Max(1, EditorGUILayout.IntField("Distancia (hex)", distance));
        autoUpdate = EditorGUILayout.ToggleLeft("Auto Update (quando assets mudarem)", autoUpdate);
        logToConsole = EditorGUILayout.ToggleLeft("Log no Console", logToConsole);

        if (previousUnitDatabase != unitDatabase)
        {
            RefreshUnits();
            RefreshAttackWeaponOptions();
            RefreshDefenderWeaponOptions();
            matrixReady = false;
            selectedCellTitle = string.Empty;
            selectedCellLog = string.Empty;
            status = "Unit Database alterado. Selecione atacante/defensor/arma e gere a matriz.";
        }
        else if (previousDistance != distance)
        {
            RefreshAttackWeaponOptions();
            RefreshDefenderWeaponOptions();
            matrixReady = false;
            selectedCellTitle = string.Empty;
            selectedCellLog = string.Empty;
            status = "Distancia alterada. Armas validas atualizadas.";
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))
        {
            AutoDetectContext();
            RefreshUnits();
            RefreshAttackWeaponOptions();
            RefreshDefenderWeaponOptions();
        }
        if (GUILayout.Button("Limpar"))
            ClearState();
        EditorGUILayout.EndHorizontal();

        if (units.Count <= 0)
        {
            EditorGUILayout.HelpBox("Sem unidades no UnitDatabase.", MessageType.Warning);
        }
        else
        {
            string[] labels = BuildUnitLabels(units);
            attackerUnitIndex = Mathf.Clamp(attackerUnitIndex, 0, units.Count - 1);
            defenderUnitIndex = Mathf.Clamp(defenderUnitIndex, 0, units.Count - 1);

            int previousAttacker = attackerUnitIndex;
            attackerUnitIndex = EditorGUILayout.Popup("Atacante", attackerUnitIndex, labels);
            if (previousAttacker != attackerUnitIndex)
            {
                RefreshAttackWeaponOptions();
                RefreshDefenderWeaponOptions();
            }

            DrawAttackWeaponSelector();

            int previousDefender = defenderUnitIndex;
            defenderUnitIndex = EditorGUILayout.Popup("Defensor", defenderUnitIndex, labels);
            if (previousDefender != defenderUnitIndex)
            {
                RefreshAttackWeaponOptions();
                RefreshDefenderWeaponOptions();
            }

            DrawDefenderWeaponSelector();
        }

        if (GUILayout.Button("Gerar Matriz de HP (10x10)"))
        {
            GenerateMatrix(selectedAttackerEmbarkedWeaponIndex);
        }

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

    private void DrawAttackWeaponSelector()
    {
        if (attackWeaponOptions.Count <= 0)
        {
            EditorGUILayout.HelpBox("Sem arma atacante valida para esse par/distancia.", MessageType.None);
            attackerWeaponIndex = 0;
            selectedAttackerEmbarkedWeaponIndex = -1;
            return;
        }

        UnitData attacker = attackerUnitIndex >= 0 && attackerUnitIndex < units.Count ? units[attackerUnitIndex] : null;
        UnitData defender = defenderUnitIndex >= 0 && defenderUnitIndex < units.Count ? units[defenderUnitIndex] : null;
        WeaponPick autoPick = PickBestAttackWeapon(attacker, defender);

        string[] labels = new string[attackWeaponOptions.Count + 1];
        labels[0] = autoPick.isValid
            ? $"Auto ({ResolveWeaponName(autoPick.weapon)})"
            : "Auto (sem arma valida)";
        for (int i = 0; i < attackWeaponOptions.Count; i++)
        {
            WeaponOption option = attackWeaponOptions[i];
            labels[i + 1] = $"[{option.code}] {ResolveWeaponName(option.weapon)} | range={option.minRange}-{option.maxRange}";
        }

        int popupIndex = 0;
        if (selectedAttackerEmbarkedWeaponIndex >= 0)
        {
            int selectedIndex = ResolveAttackWeaponIndexByEmbarked(selectedAttackerEmbarkedWeaponIndex);
            popupIndex = selectedIndex >= 0 ? selectedIndex + 1 : Mathf.Clamp(attackerWeaponIndex + 1, 1, attackWeaponOptions.Count);
        }

        popupIndex = EditorGUILayout.Popup("Arma do Atacante", popupIndex, labels);
        if (popupIndex <= 0)
        {
            selectedAttackerEmbarkedWeaponIndex = -2;
            if (autoPick.isValid)
                attackerWeaponIndex = Mathf.Max(0, ResolveAttackWeaponIndexByEmbarked(autoPick.embarkedWeaponIndex));
            return;
        }

        attackerWeaponIndex = Mathf.Clamp(popupIndex - 1, 0, attackWeaponOptions.Count - 1);
        selectedAttackerEmbarkedWeaponIndex = attackWeaponOptions[attackerWeaponIndex].embarkedWeaponIndex;
    }

    private void DrawDefenderWeaponSelector()
    {
        RefreshDefenderWeaponOptions();
        if (defenderWeaponOptions.Count <= 0)
        {
            EditorGUILayout.HelpBox("Sem opcao de revide para o defensor nesse par/distancia.", MessageType.None);
            defenderWeaponIndex = 0;
            selectedDefenderEmbarkedWeaponIndex = -1;
            return;
        }

        string[] labels = new string[defenderWeaponOptions.Count];
        for (int i = 0; i < defenderWeaponOptions.Count; i++)
            labels[i] = defenderWeaponOptions[i].label;

        defenderWeaponIndex = Mathf.Clamp(defenderWeaponIndex, 0, defenderWeaponOptions.Count - 1);
        defenderWeaponIndex = EditorGUILayout.Popup("Arma do Defensor (revide)", defenderWeaponIndex, labels);
        selectedDefenderEmbarkedWeaponIndex = defenderWeaponOptions[defenderWeaponIndex].embarkedWeaponIndex;
    }

    private void GenerateMatrix(int forcedEmbarkedWeaponIndex)
    {
        matrixReady = false;
        selectedCellTitle = string.Empty;
        selectedCellLog = string.Empty;

        if (units.Count <= 0)
            RefreshUnits();

        if (units.Count <= 0)
        {
            status = "Falha: UnitDatabase sem unidades validas.";
            return;
        }

        if (attackerUnitIndex < 0 || attackerUnitIndex >= units.Count || defenderUnitIndex < 0 || defenderUnitIndex >= units.Count)
        {
            RefreshUnits();
            if (attackerUnitIndex < 0 || attackerUnitIndex >= units.Count || defenderUnitIndex < 0 || defenderUnitIndex >= units.Count)
            {
                status = "Falha: atacante/defensor invalidos.";
                return;
            }
        }

        if (attackWeaponOptions.Count <= 0 || attackerWeaponIndex < 0 || attackerWeaponIndex >= attackWeaponOptions.Count)
        {
            RefreshAttackWeaponOptions();
            if (attackWeaponOptions.Count <= 0 || attackerWeaponIndex < 0 || attackerWeaponIndex >= attackWeaponOptions.Count)
            {
                status = "Falha: arma atacante invalida para o par/distancia.";
                return;
            }
        }

        UnitData attacker = units[attackerUnitIndex];
        UnitData defender = units[defenderUnitIndex];
        int effectiveWeaponIndex = -1;
        if (forcedEmbarkedWeaponIndex == -2)
        {
            WeaponPick autoAttackPick = PickBestAttackWeapon(attacker, defender);
            effectiveWeaponIndex = autoAttackPick.isValid
                ? ResolveAttackWeaponIndexByEmbarked(autoAttackPick.embarkedWeaponIndex)
                : -1;
        }
        else
        {
            effectiveWeaponIndex = ResolveAttackWeaponIndexByEmbarked(forcedEmbarkedWeaponIndex);
        }

        if (effectiveWeaponIndex < 0)
        {
            if (selectedAttackerEmbarkedWeaponIndex == -2)
            {
                WeaponPick autoAttackPick = PickBestAttackWeapon(attacker, defender);
                effectiveWeaponIndex = autoAttackPick.isValid
                    ? ResolveAttackWeaponIndexByEmbarked(autoAttackPick.embarkedWeaponIndex)
                    : -1;
            }
            else
            {
                effectiveWeaponIndex = ResolveAttackWeaponIndexByEmbarked(selectedAttackerEmbarkedWeaponIndex);
            }
        }
        if (effectiveWeaponIndex < 0 || effectiveWeaponIndex >= attackWeaponOptions.Count)
        {
            status = "Falha: nao foi possivel resolver a arma selecionada.";
            return;
        }

        attackerWeaponIndex = effectiveWeaponIndex;
        WeaponOption attackOption = attackWeaponOptions[effectiveWeaponIndex];
        bool isAutoSelection = forcedEmbarkedWeaponIndex == -2 || selectedAttackerEmbarkedWeaponIndex == -2;
        selectedAttackerEmbarkedWeaponIndex = isAutoSelection
            ? -2
            : attackOption.embarkedWeaponIndex;
        RefreshDefenderWeaponOptions();
        defenderWeaponIndex = Mathf.Clamp(defenderWeaponIndex, 0, Mathf.Max(0, defenderWeaponOptions.Count - 1));
        WeaponPick counterPick = ResolveSelectedDefenderCounterWeapon(defender, attacker, attackOption.weapon);

        for (int row = 0; row < HpGridSize; row++)
        {
            int attackerHpBefore = row + 1;
            for (int col = 0; col < HpGridSize; col++)
            {
                int defenderHpBefore = col + 1;
                matrix[row, col] = SimulateCell(attacker, defender, attackOption, counterPick, attackerHpBefore, defenderHpBefore);
            }
        }

        string counterText = counterPick.isValid
            ? $"{ResolveWeaponName(counterPick.weapon)} [{counterPick.code}]"
            : "sem revide";
        status = $"Matriz pronta: {GetUnitLabel(attacker)} [{attackOption.code}] -> {GetUnitLabel(defender)} | dist={distance} | revide={counterText}.";
        matrixReady = true;
    }

    private void OnProjectChanged()
    {
        RequestAutoRefresh();
    }

    private void OnUndoRedoPerformed()
    {
        RequestAutoRefresh();
    }

    private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
    {
        if (!autoUpdate || !matrixReady || modifications == null || modifications.Length == 0)
            return modifications;

        for (int i = 0; i < modifications.Length; i++)
        {
            Object target = modifications[i].currentValue.target;
            if (target is CombatModifierData
                || target is UnitData
                || target is RPSDatabase
                || target is DPQMatchupDatabase
                || target is WeaponPriorityData)
            {
                pendingAutoRefresh = true;
                break;
            }
        }

        return modifications;
    }

    private void OnEditorUpdate()
    {
        if (!pendingAutoRefresh)
            return;

        pendingAutoRefresh = false;
        if (!autoUpdate || !matrixReady || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        GenerateMatrix(selectedAttackerEmbarkedWeaponIndex);
        Repaint();
    }

    private void RequestAutoRefresh()
    {
        if (!autoUpdate || !matrixReady)
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        pendingAutoRefresh = true;
    }

    private void DrawMatrix()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Linhas = HP atacante inicial | Colunas = HP defensor inicial", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("HP A\\D", GUILayout.Width(70f));
        for (int col = HpGridSize; col >= 1; col--)
            GUILayout.Label(col.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(72f));
        EditorGUILayout.EndHorizontal();

        for (int rowHp = HpGridSize; rowHp >= 1; rowHp--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(rowHp.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(70f));

            for (int colHp = HpGridSize; colHp >= 1; colHp--)
            {
                int row = rowHp - 1;
                int col = colHp - 1;
                MatrixCellResult cell = matrix[row, col];
                string label = $"{cell.attackerHpAfter}x{cell.defenderHpAfter}";
                bool containedByLock = cell.defenderDamageContainedByHpLock || cell.attackerDamageContainedByHpLock;

                Color prevColor = GUI.color;
                if (containedByLock)
                    GUI.color = new Color(1f, 0.92f, 0.45f, 1f);

                if (GUILayout.Button(label, GUILayout.Width(72f)))
                {
                    selectedCellTitle = $"HP A={rowHp} x HP D={colHp}";
                    selectedCellLog = cell.log;
                    if (logToConsole)
                        Debug.Log(cell.log);
                }

                if (containedByLock)
                    GUI.color = prevColor;
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private MatrixCellResult SimulateCell(
        UnitData attacker,
        UnitData defender,
        WeaponOption attackOption,
        WeaponPick counterPick,
        int attackerHpBefore,
        int defenderHpBefore)
    {
        WeaponData attackerWeapon = attackOption.weapon;
        WeaponData defenderWeapon = counterPick.isValid ? counterPick.weapon : null;
        bool counterExecuted = counterPick.isValid;

        DPQCombatOutcome attackerOutcome = DPQCombatOutcome.Neutro;
        DPQCombatOutcome defenderOutcome = DPQCombatOutcome.Neutro;
        if (dpqMatchupDatabase != null)
            dpqMatchupDatabase.Resolve(1, 1, out attackerOutcome, out defenderOutcome);

        int attackerWeaponPower = attackerWeapon != null ? Mathf.Max(0, attackerWeapon.basicAttack) : 0;
        int defenderWeaponPower = counterExecuted && defenderWeapon != null ? Mathf.Max(0, defenderWeapon.basicAttack) : 0;
        WeaponCategory attackerCategory = ResolveWeaponCategory(attackerWeapon);
        WeaponCategory defenderCategory = ResolveWeaponCategory(defenderWeapon);

        int attackerAttackRps = ResolveAttackRps(attacker.unitClass, attackerCategory, defender.unitClass);
        int defenderAttackRps = counterExecuted ? ResolveAttackRps(defender.unitClass, defenderCategory, attacker.unitClass) : 0;
        WeaponCategory defenderCategoryForSkill = counterExecuted ? defenderCategory : attackerCategory;
        SkillModifierSummary attackerSkill = ResolveSkillModifiers(attacker, defender, attackerCategory, defenderCategoryForSkill);
        SkillModifierSummary defenderSkill = ResolveSkillModifiers(defender, attacker, defenderCategoryForSkill, attackerCategory);
        SkillModifierSummary defenderDefenseSkill = ResolveSkillModifiers(defender, attacker, defenderCategoryForSkill, attackerCategory);

        int attackerAttackSkillTotal = attackerSkill.ownerAttack + defenderSkill.opponentAttack;
        int defenderAttackSkillTotal = defenderSkill.ownerAttack + attackerSkill.opponentAttack;
        int attackerDefenseSkillTotal = attackerSkill.ownerDefense + defenderSkill.opponentDefense;
        int defenderDefenseSkillTotal = defenderDefenseSkill.ownerDefense + attackerSkill.opponentDefense;

        int attackerAttackTermRaw = attackerWeaponPower + attackerAttackRps + attackerAttackSkillTotal;
        int defenderAttackTermRaw = defenderWeaponPower + defenderAttackRps + defenderAttackSkillTotal;
        int attackerAttackTermApplied = Mathf.Max(1, attackerAttackTermRaw);
        int defenderAttackTermApplied = counterExecuted ? Mathf.Max(1, defenderAttackTermRaw) : 0;

        int attackerAttackEffective = attackerHpBefore * attackerAttackTermApplied;
        int defenderAttackEffective = counterExecuted ? defenderHpBefore * defenderAttackTermApplied : 0;

        int attackerDefenseRps = counterExecuted ? ResolveDefenseRps(attacker.unitClass, defender.unitClass, defenderCategory) : 0;
        int defenderDefenseRps = ResolveDefenseRps(defender.unitClass, attacker.unitClass, attackerCategory);
        int attackerWoundedPenalty = ResolveWoundedDefensePenalty(attackerHpBefore, attacker.maxHP);
        int defenderWoundedPenalty = ResolveWoundedDefensePenalty(defenderHpBefore, defender.maxHP);
        int attackerEffectiveDefense = attacker.defense + attackerDefenseRps + attackerDefenseSkillTotal + attackerWoundedPenalty;
        int defenderEffectiveDefense = defender.defense + defenderDefenseRps + defenderDefenseSkillTotal + defenderWoundedPenalty;

        int defenderSafeDefense = Mathf.Max(1, defenderEffectiveDefense);
        int attackerSafeDefense = Mathf.Max(1, attackerEffectiveDefense);
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

        StringBuilder log = new StringBuilder(1200);
        log.AppendLine("[Matriz de HP] Simulacao de celula");
        log.AppendLine($"Atacante: {GetUnitLabel(attacker)}");
        log.AppendLine($"Defensor: {GetUnitLabel(defender)}");
        log.AppendLine($"Distancia: {distance}");
        log.AppendLine($"Arma atacante: [{attackOption.code}] {ResolveWeaponName(attackerWeapon)}");
        log.AppendLine($"Arma revide auto: {(counterExecuted ? $"[{counterPick.code}] {ResolveWeaponName(defenderWeapon)}" : "sem revide")}");
        log.AppendLine($"HP atacante inicial: {attackerHpBefore}");
        log.AppendLine($"HP defensor inicial: {defenderHpBefore}");
        log.AppendLine($"Outcome atacante: {attackerOutcome}");
        log.AppendLine($"Outcome defensor: {defenderOutcome}");
        log.AppendLine();
        log.AppendLine("1) Ataque efetivo");
        log.AppendLine($"- Atacante: HP({attackerHpBefore}) x max(1, Arma({attackerWeaponPower}) + RPSAtaque({FormatSigned(attackerAttackRps)}) + EliteSkillAtaque({FormatSigned(attackerAttackSkillTotal)})) = {attackerAttackEffective}");
        log.AppendLine($"- Defensor: HP({defenderHpBefore}) x {(counterExecuted ? "max(1, " : string.Empty)}Arma({defenderWeaponPower}) + RPSAtaque({FormatSigned(defenderAttackRps)}) + EliteSkillAtaque({FormatSigned(defenderAttackSkillTotal)}){(counterExecuted ? ")" : string.Empty)} = {defenderAttackEffective}");
        log.AppendLine("2) Defesa efetiva");
        log.AppendLine($"- Atacante: defesaUnidade({attacker.defense}) + defesaDPQ(0) + RPSDefesa({FormatSigned(attackerDefenseRps)}) + EliteSkillDefesa({FormatSigned(attackerDefenseSkillTotal)}) + UnidadeFerida({FormatSigned(attackerWoundedPenalty)}) = {attackerEffectiveDefense}");
        log.AppendLine($"- Defensor: defesaUnidade({defender.defense}) + defesaDPQ(0) + RPSDefesa({FormatSigned(defenderDefenseRps)}) + EliteSkillDefesa({FormatSigned(defenderDefenseSkillTotal)}) + UnidadeFerida({FormatSigned(defenderWoundedPenalty)}) = {defenderEffectiveDefense}");
        log.AppendLine("3) Resultado");
        log.AppendLine($"- Regra defensor: {BuildRoundingExplanation(attackerAttackEffective, defenderSafeDefense, attackerOutcome, roundedOnDefender)}");
        log.AppendLine($"- Regra atacante: {BuildRoundingExplanation(defenderAttackEffective, attackerSafeDefense, defenderOutcome, roundedOnAttacker)}");
        log.AppendLine($"- Elim no defensor: rounded={roundedOnDefender} -> aplicado={appliedOnDefender} (trava={defenderDamageCapByAttackerHp}, contido pela trava de hp={(defenderDamageContainedByHpLock ? "sim" : "nao")})");
        log.AppendLine($"- Elim no atacante: rounded={roundedOnAttacker} -> aplicado={appliedOnAttacker} (trava={attackerDamageCapByDefenderHp}, contido pela trava de hp={(attackerDamageContainedByHpLock ? "sim" : "nao")})");
        log.AppendLine($"- HP defensor: {defenderHpBefore} -> {defenderHpAfter}");
        log.AppendLine($"- HP atacante: {attackerHpBefore} -> {attackerHpAfter}");

        return new MatrixCellResult(attackerHpAfter, defenderHpAfter, defenderDamageContainedByHpLock, attackerDamageContainedByHpLock, log.ToString());
    }

    private void RefreshUnits()
    {
        units.Clear();
        if (unitDatabase == null || unitDatabase.Units == null)
            return;

        for (int i = 0; i < unitDatabase.Units.Count; i++)
        {
            UnitData unit = unitDatabase.Units[i];
            if (unit == null || string.IsNullOrWhiteSpace(unit.id))
                continue;
            units.Add(unit);
        }

        if (units.Count <= 0)
        {
            attackerUnitIndex = 0;
            defenderUnitIndex = 0;
            return;
        }

        attackerUnitIndex = Mathf.Clamp(attackerUnitIndex, 0, units.Count - 1);
        defenderUnitIndex = Mathf.Clamp(defenderUnitIndex, 0, units.Count - 1);
    }

    private void RefreshAttackWeaponOptions()
    {
        int previousSelectedIndex = attackerWeaponIndex;
        int previousSelectedEmbarkedIndexFromField = selectedAttackerEmbarkedWeaponIndex;
        bool keepAutoSelection = previousSelectedEmbarkedIndexFromField == -2;
        StringBuilder rejectionDetails = new StringBuilder();
        WeaponData previousSelectedWeapon = null;
        int previousSelectedEmbarkedIndex = -1;
        if (attackWeaponOptions.Count > 0
            && attackerWeaponIndex >= 0
            && attackerWeaponIndex < attackWeaponOptions.Count)
        {
            WeaponOption selected = attackWeaponOptions[attackerWeaponIndex];
            previousSelectedWeapon = selected.weapon;
            previousSelectedEmbarkedIndex = selected.embarkedWeaponIndex;
        }

        attackWeaponOptions.Clear();

        if (units.Count <= 0)
            return;
        if (attackerUnitIndex < 0 || attackerUnitIndex >= units.Count || defenderUnitIndex < 0 || defenderUnitIndex >= units.Count)
            return;

        UnitData attacker = units[attackerUnitIndex];
        UnitData defender = units[defenderUnitIndex];
        if (attacker == null || defender == null || attacker.embarkedWeapons == null)
            return;

        for (int i = 0; i < attacker.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = attacker.embarkedWeapons[i];
            if (embarked == null || embarked.weapon == null)
            {
                rejectionDetails.AppendLine($"- arma#{i}: vazia");
                continue;
            }

            string weaponName = ResolveWeaponName(embarked.weapon);

            if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                    embarked,
                    SensorMovementMode.MoveuParado,
                    requireAmmo: true,
                    out int minRange,
                    out int maxRange))
            {
                rejectionDetails.AppendLine($"- {weaponName}#{i}: sem municao ou range invalido ({embarked.GetRangeMin()}-{embarked.GetRangeMax()})");
                continue;
            }

            if (distance < minRange || distance > maxRange)
            {
                rejectionDetails.AppendLine($"- {weaponName}#{i}: fora de alcance (dist={distance}, range={minRange}-{maxRange})");
                continue;
            }
            if (!embarked.weapon.SupportsOperationOn(defender.domain, defender.heightLevel))
            {
                rejectionDetails.AppendLine($"- {weaponName}#{i}: dominio/altura incompativel com alvo ({defender.domain}/{defender.heightLevel})");
                continue;
            }

            attackWeaponOptions.Add(new WeaponOption(embarked.weapon, i, minRange, maxRange));
        }

        if (attackWeaponOptions.Count <= 0)
        {
            attackerWeaponIndex = 0;
            selectedAttackerEmbarkedWeaponIndex = -1;
            string details = rejectionDetails.Length > 0 ? rejectionDetails.ToString().TrimEnd() : "- sem detalhes";
            status = $"Sem arma atacante valida para {GetUnitLabel(attacker)} -> {GetUnitLabel(defender)} (dist={distance}).\n{details}";
            return;
        }

        if (keepAutoSelection)
        {
            UnitData attackerAuto = attackerUnitIndex >= 0 && attackerUnitIndex < units.Count ? units[attackerUnitIndex] : null;
            UnitData defenderAuto = defenderUnitIndex >= 0 && defenderUnitIndex < units.Count ? units[defenderUnitIndex] : null;
            WeaponPick autoPick = PickBestAttackWeapon(attackerAuto, defenderAuto);
            int autoIndex = autoPick.isValid
                ? ResolveAttackWeaponIndexByEmbarked(autoPick.embarkedWeaponIndex)
                : -1;
            attackerWeaponIndex = autoIndex >= 0
                ? autoIndex
                : 0;
            selectedAttackerEmbarkedWeaponIndex = -2;
            return;
        }

        int restoredIndex = -1;

        if (previousSelectedEmbarkedIndexFromField >= 0)
        {
            for (int i = 0; i < attackWeaponOptions.Count; i++)
            {
                if (attackWeaponOptions[i].embarkedWeaponIndex == previousSelectedEmbarkedIndexFromField)
                {
                    restoredIndex = i;
                    break;
                }
            }
        }

        if (restoredIndex < 0)
        {
            for (int i = 0; i < attackWeaponOptions.Count; i++)
            {
                WeaponOption candidate = attackWeaponOptions[i];
                if (candidate.weapon == previousSelectedWeapon && candidate.embarkedWeaponIndex == previousSelectedEmbarkedIndex)
                {
                    restoredIndex = i;
                    break;
                }
            }
        }

        if (restoredIndex >= 0)
        {
            attackerWeaponIndex = restoredIndex;
            if (!keepAutoSelection)
                selectedAttackerEmbarkedWeaponIndex = attackWeaponOptions[restoredIndex].embarkedWeaponIndex;
            return;
        }

        attackerWeaponIndex = Mathf.Clamp(previousSelectedIndex, 0, attackWeaponOptions.Count - 1);
        if (!keepAutoSelection)
            selectedAttackerEmbarkedWeaponIndex = attackWeaponOptions[attackerWeaponIndex].embarkedWeaponIndex;
    }

    private void RefreshDefenderWeaponOptions()
    {
        int previousPopupIndex = defenderWeaponIndex;
        int previousEmbarkedIndex = selectedDefenderEmbarkedWeaponIndex;

        defenderWeaponOptions.Clear();
        if (units.Count <= 0)
            return;
        if (attackerUnitIndex < 0 || attackerUnitIndex >= units.Count || defenderUnitIndex < 0 || defenderUnitIndex >= units.Count)
            return;

        UnitData attacker = units[attackerUnitIndex];
        UnitData defender = units[defenderUnitIndex];
        if (attacker == null || defender == null)
            return;

        WeaponData attackerWeaponContext = null;
        if (attackWeaponOptions.Count > 0 && attackerWeaponIndex >= 0 && attackerWeaponIndex < attackWeaponOptions.Count)
            attackerWeaponContext = attackWeaponOptions[attackerWeaponIndex].weapon;

        WeaponPick autoPick = PickBestCounterWeapon(defender, attacker, distance, attackerWeaponContext);
        string autoLabel = autoPick.isValid
            ? $"Auto ({ResolveWeaponName(autoPick.weapon)})"
            : "Auto (sem revide)";
        defenderWeaponOptions.Add(new CounterWeaponOption(autoPick, -2, autoLabel));

        if (defender.embarkedWeapons != null)
        {
            for (int i = 0; i < defender.embarkedWeapons.Count; i++)
            {
                if (!TryBuildManualCounterWeaponOption(attacker, defender.embarkedWeapons[i], i, distance, out CounterWeaponOption option))
                    continue;
                defenderWeaponOptions.Add(option);
            }
        }

        defenderWeaponOptions.Add(new CounterWeaponOption(WeaponPick.None, -1, "Sem revide (forcado)"));

        int restoredIndex = -1;
        if (previousEmbarkedIndex == -2)
            restoredIndex = 0;
        else
        {
            for (int i = 0; i < defenderWeaponOptions.Count; i++)
            {
                if (defenderWeaponOptions[i].embarkedWeaponIndex == previousEmbarkedIndex)
                {
                    restoredIndex = i;
                    break;
                }
            }
        }

        if (restoredIndex < 0)
            restoredIndex = Mathf.Clamp(previousPopupIndex, 0, defenderWeaponOptions.Count - 1);

        defenderWeaponIndex = restoredIndex;
        selectedDefenderEmbarkedWeaponIndex = defenderWeaponOptions[defenderWeaponIndex].embarkedWeaponIndex;
    }

    private int ResolveAttackWeaponIndexByEmbarked(int embarkedWeaponIndex)
    {
        if (attackWeaponOptions.Count <= 0)
            return -1;

        if (embarkedWeaponIndex >= 0)
        {
            for (int i = 0; i < attackWeaponOptions.Count; i++)
            {
                if (attackWeaponOptions[i].embarkedWeaponIndex == embarkedWeaponIndex)
                    return i;
            }
        }
        return -1;
    }

    private WeaponPick ResolveSelectedDefenderCounterWeapon(UnitData defender, UnitData attacker, WeaponData attackerWeaponContext)
    {
        if (defenderWeaponOptions.Count <= 0)
            return PickBestCounterWeapon(defender, attacker, distance, attackerWeaponContext);

        defenderWeaponIndex = Mathf.Clamp(defenderWeaponIndex, 0, defenderWeaponOptions.Count - 1);
        CounterWeaponOption selected = defenderWeaponOptions[defenderWeaponIndex];
        if (selected.embarkedWeaponIndex == -2)
            return PickBestCounterWeapon(defender, attacker, distance, attackerWeaponContext);

        return selected.pick;
    }

    private WeaponPick PickBestAttackWeapon(UnitData attacker, UnitData defender)
    {
        if (attacker == null || defender == null || attackWeaponOptions.Count <= 0)
            return WeaponPick.None;

        WeaponPick fallback = WeaponPick.None;
        for (int i = 0; i < attackWeaponOptions.Count; i++)
        {
            WeaponOption option = attackWeaponOptions[i];
            if (option.weapon == null)
                continue;

            WeaponPick current = new WeaponPick(option.weapon, option.embarkedWeaponIndex, option.code, true);
            if (!fallback.isValid)
                fallback = current;
            if (PodeMirarSensor.IsPreferredWeaponForTarget(weaponPriorityData, option.weapon, defender.unitClass))
                return current;
        }

        return fallback;
    }

    private WeaponPick PickBestCounterWeapon(UnitData defender, UnitData attacker, int attackRange, WeaponData attackerWeaponContext)
    {
        if (!PodeMirarSensor.TryResolveCounterAttackFromData(
                defender,
                attacker,
                attackRange,
                weaponPriorityData,
                out WeaponData counterWeapon,
                out int counterEmbarkedIndex,
                out _))
        {
            return WeaponPick.None;
        }

        return new WeaponPick(counterWeapon, counterEmbarkedIndex, GetWeaponLabel(counterWeapon, counterEmbarkedIndex), true);
    }

    private static bool TryBuildManualCounterWeaponOption(UnitData attacker, UnitEmbarkedWeapon embarked, int embarkedIndex, int distance, out CounterWeaponOption option)
    {
        option = default(CounterWeaponOption);
        if (attacker == null || embarked == null || embarked.weapon == null)
            return false;
        if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                embarked,
                SensorMovementMode.MoveuParado,
                requireAmmo: true,
                out int minRange,
                out _))
        {
            return false;
        }
        if (distance != 1 || minRange != 1)
            return false;
        if (!embarked.weapon.SupportsOperationOn(attacker.domain, attacker.heightLevel))
            return false;

        WeaponPick pick = new WeaponPick(embarked.weapon, embarkedIndex, GetWeaponLabel(embarked.weapon, embarkedIndex), true);
        string label = $"Manual [{embarkedIndex}] {ResolveWeaponName(embarked.weapon)}";
        option = new CounterWeaponOption(pick, embarkedIndex, label);
        return true;
    }

    private int ResolveAttackRps(GameUnitClass attackerClass, WeaponCategory weaponCategory, GameUnitClass defenderClass)
    {
        if (rpsDatabase == null)
            return 0;
        rpsDatabase.TryResolveAttackBonus(attackerClass, weaponCategory, defenderClass, out int bonus, out _, out _);
        return bonus;
    }

    private int ResolveDefenseRps(GameUnitClass defenderClass, GameUnitClass attackerClass, WeaponCategory weaponCategory)
    {
        if (rpsDatabase == null)
            return 0;
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

    private static int ResolveWoundedDefensePenalty(int currentHp, int maxHp)
    {
        int safeMaxHp = Mathf.Max(1, maxHp);
        int safeCurrentHp = Mathf.Clamp(currentHp, 0, safeMaxHp);
        if (safeCurrentHp >= safeMaxHp)
            return 0;
        if (safeCurrentHp <= 5)
            return -2;
        return -1;
    }

    private static string[] BuildUnitLabels(List<UnitData> data)
    {
        string[] labels = new string[data.Count];
        for (int i = 0; i < data.Count; i++)
            labels[i] = GetUnitLabel(data[i]);
        return labels;
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

    private static WeaponCategory ResolveWeaponCategory(WeaponData weapon)
    {
        return weapon != null ? weapon.WeaponCategory : WeaponCategory.AntiInfantaria;
    }

    private static string BuildRoundingExplanation(int numerator, int denominator, DPQCombatOutcome outcome, int roundedResult)
    {
        if (denominator == 0)
            return "divisao por zero -> 0";
        float raw = (float)numerator / denominator;
        return $"{numerator}/{denominator} = {raw:0.###} | outcome={outcome} -> {roundedResult}";
    }

    private static string FormatSigned(int value)
    {
        return value.ToString("+0;-0;+0");
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

    private void AutoDetectContext()
    {
        if (unitDatabase == null)
            unitDatabase = TryResolveUnitDatabaseFromTurnStateManager();
        if (unitDatabase == null)
            unitDatabase = FindFirstAssetOfType<UnitDatabase>();
        if (rpsDatabase == null)
            rpsDatabase = FindFirstAssetOfType<RPSDatabase>();
        if (dpqMatchupDatabase == null)
            dpqMatchupDatabase = FindFirstAssetOfType<DPQMatchupDatabase>();
        if (weaponPriorityData == null)
            weaponPriorityData = FindFirstAssetOfType<WeaponPriorityData>();
    }

    private static UnitDatabase TryResolveUnitDatabaseFromTurnStateManager()
    {
        TurnStateManager turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();
        if (turnStateManager == null)
            return null;

        SerializedObject turnSo = new SerializedObject(turnStateManager);
        SerializedProperty unitSpawnerProp = turnSo.FindProperty("unitSpawner");
        UnitSpawner unitSpawner = unitSpawnerProp != null ? unitSpawnerProp.objectReferenceValue as UnitSpawner : null;
        if (unitSpawner == null)
            return null;

        SerializedObject spawnerSo = new SerializedObject(unitSpawner);
        SerializedProperty dbProp = spawnerSo.FindProperty("unitDatabase");
        return dbProp != null ? dbProp.objectReferenceValue as UnitDatabase : null;
    }

    private static T FindFirstAssetOfType<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private void ClearState()
    {
        attackerUnitIndex = 0;
        defenderUnitIndex = 0;
        attackerWeaponIndex = 0;
        defenderWeaponIndex = 0;
        selectedAttackerEmbarkedWeaponIndex = -2;
        selectedDefenderEmbarkedWeaponIndex = -2;
        attackWeaponOptions.Clear();
        defenderWeaponOptions.Clear();
        matrixReady = false;
        selectedCellTitle = string.Empty;
        selectedCellLog = string.Empty;
        status = "Ready.";
    }

    private readonly struct WeaponOption
    {
        public readonly WeaponData weapon;
        public readonly int embarkedWeaponIndex;
        public readonly int minRange;
        public readonly int maxRange;
        public readonly string code;

        public WeaponOption(WeaponData weapon, int embarkedWeaponIndex, int minRange, int maxRange)
        {
            this.weapon = weapon;
            this.embarkedWeaponIndex = embarkedWeaponIndex;
            this.minRange = minRange;
            this.maxRange = maxRange;
            code = GetWeaponLabel(weapon, embarkedWeaponIndex);
        }
    }

    private readonly struct WeaponPick
    {
        public static WeaponPick None => new WeaponPick(null, -1, "-", false);

        public readonly WeaponData weapon;
        public readonly int embarkedWeaponIndex;
        public readonly string code;
        public readonly bool isValid;

        public WeaponPick(WeaponData weapon, int embarkedWeaponIndex, string code, bool isValid)
        {
            this.weapon = weapon;
            this.embarkedWeaponIndex = embarkedWeaponIndex;
            this.code = code;
            this.isValid = isValid;
        }
    }

    private readonly struct CounterWeaponOption
    {
        public readonly WeaponPick pick;
        public readonly int embarkedWeaponIndex;
        public readonly string label;

        public CounterWeaponOption(WeaponPick pick, int embarkedWeaponIndex, string label)
        {
            this.pick = pick;
            this.embarkedWeaponIndex = embarkedWeaponIndex;
            this.label = label;
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

    private readonly struct MatrixCellResult
    {
        public readonly int attackerHpAfter;
        public readonly int defenderHpAfter;
        public readonly bool defenderDamageContainedByHpLock;
        public readonly bool attackerDamageContainedByHpLock;
        public readonly string log;

        public MatrixCellResult(
            int attackerHpAfter,
            int defenderHpAfter,
            bool defenderDamageContainedByHpLock,
            bool attackerDamageContainedByHpLock,
            string log)
        {
            this.attackerHpAfter = attackerHpAfter;
            this.defenderHpAfter = defenderHpAfter;
            this.defenderDamageContainedByHpLock = defenderDamageContainedByHpLock;
            this.attackerDamageContainedByHpLock = attackerDamageContainedByHpLock;
            this.log = log;
        }
    }
}
