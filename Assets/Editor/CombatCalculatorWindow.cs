using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CombatCalculatorWindow : EditorWindow
{
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private WeaponPriorityData weaponPriorityData;
    [SerializeField] private DPQMatchupDatabase dpqMatchupDatabase;
    [SerializeField] private RPSDatabase rpsDatabase;
    [SerializeField] private UnitManager attackerUnit;
    [SerializeField] private UnitManager defenderUnit;
    [SerializeField] private SensorMovementMode movementMode = SensorMovementMode.MoveuParado;
    [SerializeField] private int selectedPairWeaponIndex;
    [SerializeField] private bool logToConsole = true;

    private Vector2 scroll;
    private string report = "Ready.";
    private readonly List<PodeMirarTargetOption> sensorValidResults = new List<PodeMirarTargetOption>();
    private readonly List<PodeMirarInvalidOption> sensorInvalidResults = new List<PodeMirarInvalidOption>();
    private readonly List<PodeMirarTargetOption> currentPairOptions = new List<PodeMirarTargetOption>();

    [MenuItem("Tools/Calcular Combate")]
    public static void OpenWindow()
    {
        GetWindow<CombatCalculatorWindow>("Calcular Combate");
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Calcular Combate (Rascunho)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Selecione atacante e defensor, rode o sensor Pode Mirar para o par e simule os calculos iniciais.", MessageType.Info);

        turnStateManager = (TurnStateManager)EditorGUILayout.ObjectField("TurnStateManager", turnStateManager, typeof(TurnStateManager), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        weaponPriorityData = (WeaponPriorityData)EditorGUILayout.ObjectField("Weapon Priority Data", weaponPriorityData, typeof(WeaponPriorityData), false);
        dpqMatchupDatabase = (DPQMatchupDatabase)EditorGUILayout.ObjectField("DPQ Matchup DB", dpqMatchupDatabase, typeof(DPQMatchupDatabase), false);
        rpsDatabase = (RPSDatabase)EditorGUILayout.ObjectField("RPS Database", rpsDatabase, typeof(RPSDatabase), false);
        movementMode = (SensorMovementMode)EditorGUILayout.EnumPopup("Modo Sensor", movementMode);
        logToConsole = EditorGUILayout.ToggleLeft("Log no Console", logToConsole);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Limpar Selecao"))
            ClearPairSelection();
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

        if (GUILayout.Button("Rodar Sensor + Simular Par"))
            RunSensorAndSimulatePair();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Relatorio", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void DrawPairWeaponSelector()
    {
        if (currentPairOptions.Count <= 0)
        {
            EditorGUILayout.HelpBox("Sem opcoes validas carregadas para o par atual.", MessageType.None);
            selectedPairWeaponIndex = 0;
            return;
        }

        string[] labels = new string[currentPairOptions.Count];
        for (int i = 0; i < currentPairOptions.Count; i++)
        {
            PodeMirarTargetOption option = currentPairOptions[i];
            string weaponLabel = option != null ? ResolveWeaponName(option.weapon) : "(sem arma)";
            labels[i] = $"{i + 1}. {weaponLabel} | dist={option.distance}";
        }

        selectedPairWeaponIndex = Mathf.Clamp(selectedPairWeaponIndex, 0, currentPairOptions.Count - 1);
        selectedPairWeaponIndex = EditorGUILayout.Popup("Opcao de Arma (Par)", selectedPairWeaponIndex, labels);
    }

    private void RunSensorAndSimulatePair()
    {
        currentPairOptions.Clear();
        sensorValidResults.Clear();
        sensorInvalidResults.Clear();

        if (attackerUnit == null || defenderUnit == null)
        {
            report = "Falha: defina atacante e defensor.";
            return;
        }

        if (attackerUnit == defenderUnit)
        {
            report = "Falha: atacante e defensor nao podem ser a mesma unidade.";
            return;
        }

        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();

        Tilemap board = ResolveBoardTilemap(attackerUnit, defenderUnit);
        if (board == null)
        {
            report = "Falha: nao foi possivel resolver Tilemap para rodar o sensor.";
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
            if (option == null)
                continue;
            if (option.attackerUnit == attackerUnit && option.targetUnit == defenderUnit)
                currentPairOptions.Add(option);
        }

        if (currentPairOptions.Count <= 0)
        {
            report = BuildInvalidPairReport(attackerUnit, defenderUnit, sensorInvalidResults);
            selectedPairWeaponIndex = 0;
            if (logToConsole)
                Debug.Log(report);
            return;
        }

        selectedPairWeaponIndex = Mathf.Clamp(selectedPairWeaponIndex, 0, currentPairOptions.Count - 1);
        PodeMirarTargetOption chosen = currentPairOptions[selectedPairWeaponIndex];
        report = BuildCombatDraftReport(chosen);
        if (logToConsole)
            Debug.Log(report);
    }

    private string BuildCombatDraftReport(PodeMirarTargetOption option)
    {
        UnitManager attacker = option.attackerUnit;
        UnitManager defender = option.targetUnit;
        if (attacker == null || defender == null)
            return "Falha: atacante/defensor invalidos na opcao.";

        StringBuilder sb = new StringBuilder(1024);
        sb.AppendLine("[Combate] Calculo inicial (rascunho)");
        sb.AppendLine($"Entrada: {SafeName(attacker)} -> {SafeName(defender)}");
        sb.AppendLine($"Opcao selecionada (par): {selectedPairWeaponIndex + 1}/{Mathf.Max(1, currentPairOptions.Count)}");
        sb.AppendLine($"Indice arma embarcada atacante: {option.embarkedWeaponIndex}");
        sb.AppendLine($"Arma atacante: {ResolveWeaponName(option.weapon)}");
        sb.AppendLine($"Categoria arma atacante: {ResolveWeaponCategory(option.weapon)}");
        sb.AppendLine($"Revide previsto: {(option.defenderCanCounterAttack ? "sim" : "nao")}");
        sb.AppendLine($"Indice arma embarcada defensor (revide): {option.defenderCounterEmbarkedWeaponIndex}");
        sb.AppendLine($"Arma revide: {ResolveWeaponName(option.defenderCounterWeapon)}");
        sb.AppendLine($"Categoria arma revide: {ResolveWeaponCategory(option.defenderCounterWeapon)}");
        sb.AppendLine($"Distancia: {option.distance}");

        int attackerHp = Mathf.Max(0, attacker.CurrentHP);
        int defenderHp = Mathf.Max(0, defender.CurrentHP);
        int attackerWeaponPower = option.weapon != null ? Mathf.Max(0, option.weapon.basicAttack) : 0;
        int defenderWeaponPower = option.defenderCanCounterAttack && option.defenderCounterWeapon != null
            ? Mathf.Max(0, option.defenderCounterWeapon.basicAttack)
            : 0;
        GameUnitClass attackerClass = ResolveUnitClass(attacker, out bool attackerClassFromData);
        GameUnitClass defenderClass = ResolveUnitClass(defender, out bool defenderClassFromData);
        WeaponCategory attackerWeaponCategory = ResolveWeaponCategory(option.weapon);
        WeaponCategory defenderWeaponCategory = ResolveWeaponCategory(option.defenderCounterWeapon);
        sb.AppendLine($"Classe atacante usada no RPS: {attackerClass} {(attackerClassFromData ? "(UnitData)" : "(fallback)")}");
        sb.AppendLine($"Classe defensor usada no RPS: {defenderClass} {(defenderClassFromData ? "(UnitData)" : "(fallback)")}");
        sb.AppendLine($"Chave RPS ataque atacante: {attackerClass} + {attackerWeaponCategory} -> {defenderClass}");
        sb.AppendLine($"Chave RPS ataque defensor: {defenderClass} + {defenderWeaponCategory} -> {attackerClass}");
        sb.AppendLine();

        RPSBonusInfo attackerAttackRps = ResolveAttackRps(attackerClass, attackerWeaponCategory, defenderClass);
        RPSBonusInfo defenderAttackRps = option.defenderCanCounterAttack
            ? ResolveAttackRps(defenderClass, defenderWeaponCategory, attackerClass)
            : RPSBonusInfo.None;

        int attackerAttackEffective = attackerHp * (attackerWeaponPower + attackerAttackRps.bonusValue);
        int defenderAttackEffective = option.defenderCanCounterAttack
            ? defenderHp * (defenderWeaponPower + defenderAttackRps.bonusValue)
            : 0;

        sb.AppendLine("1) Forca de ataque efetiva");
        sb.AppendLine($"- Atacante: HP({attackerHp}) x (Arma({attackerWeaponPower}) + RPSAtaque({FormatSigned(attackerAttackRps.bonusValue)})) = {attackerAttackEffective}");
        sb.AppendLine($"- Defensor: HP({defenderHp}) x (Arma({defenderWeaponPower}) + RPSAtaque({FormatSigned(defenderAttackRps.bonusValue)})) = {defenderAttackEffective}");
        sb.AppendLine($"- Detalhe RPS ataque atacante: {attackerAttackRps.summary}");
        sb.AppendLine($"- Detalhe RPS ataque defensor: {defenderAttackRps.summary}");

        PositionDpqInfo attackerDpq = ResolveDpqAtUnitPosition(attacker, option.attackerPositionLabel);
        PositionDpqInfo defenderDpq = ResolveDpqAtUnitPosition(defender, option.defenderPositionLabel);
        sb.AppendLine("2) DPQ da posicao");
        sb.AppendLine($"- Atacante: {attackerDpq.displayName} ({attackerDpq.sourceLabel})");
        sb.AppendLine($"- Defensor: {defenderDpq.displayName} ({defenderDpq.sourceLabel})");

        sb.AppendLine("3) Bonus de defesa e pontos de posicao (DPQ)");
        sb.AppendLine($"- Atacante: defesaBonus={attackerDpq.defenseBonus} | pontos={attackerDpq.points}");
        sb.AppendLine($"- Defensor: defesaBonus={defenderDpq.defenseBonus} | pontos={defenderDpq.points}");

        int attackerBaseDefense = GetUnitBaseDefense(attacker);
        int defenderBaseDefense = GetUnitBaseDefense(defender);
        RPSBonusInfo attackerDefenseRps = option.defenderCanCounterAttack
            ? ResolveDefenseRps(attackerClass, defenderClass, defenderWeaponCategory)
            : RPSBonusInfo.None;
        RPSBonusInfo defenderDefenseRps = ResolveDefenseRps(defenderClass, attackerClass, attackerWeaponCategory);

        int attackerEffectiveDefense = attackerBaseDefense + attackerDpq.defenseBonus + attackerDefenseRps.bonusValue;
        int defenderEffectiveDefense = defenderBaseDefense + defenderDpq.defenseBonus + defenderDefenseRps.bonusValue;
        sb.AppendLine("4) Forca de defesa efetiva");
        sb.AppendLine($"- Atacante: defesaUnidade({attackerBaseDefense}) + defesaDPQ({attackerDpq.defenseBonus}) + RPSDefesa({FormatSigned(attackerDefenseRps.bonusValue)}) = {attackerEffectiveDefense}");
        sb.AppendLine($"- Defensor: defesaUnidade({defenderBaseDefense}) + defesaDPQ({defenderDpq.defenseBonus}) + RPSDefesa({FormatSigned(defenderDefenseRps.bonusValue)}) = {defenderEffectiveDefense}");
        sb.AppendLine($"- Detalhe RPS defesa atacante: {attackerDefenseRps.summary}");
        sb.AppendLine($"- Detalhe RPS defesa defensor: {defenderDefenseRps.summary}");

        int dpqDifference = attackerDpq.points - defenderDpq.points;
        DPQCombatOutcome attackerOutcome = DPQCombatOutcome.Neutro;
        DPQCombatOutcome defenderOutcome = DPQCombatOutcome.Neutro;
        if (dpqMatchupDatabase != null)
            dpqMatchupDatabase.Resolve(attackerDpq.points, defenderDpq.points, out attackerOutcome, out defenderOutcome);

        sb.AppendLine("5) Matchup DPQ (Tabela)");
        sb.AppendLine($"- Diferenca (atacante - defensor): {attackerDpq.points} - {defenderDpq.points} = {dpqDifference}");
        sb.AppendLine($"- Outcome atacante: {attackerOutcome}");
        sb.AppendLine($"- Outcome defensor: {defenderOutcome}");

        int defenderSafeDefense = Mathf.Max(1, defenderEffectiveDefense);
        int attackerSafeDefense = Mathf.Max(1, attackerEffectiveDefense);
        float rawEliminationOnDefender = defenderSafeDefense > 0 ? (float)attackerAttackEffective / defenderSafeDefense : 0f;
        float rawEliminationOnAttacker = option.defenderCanCounterAttack && attackerSafeDefense > 0
            ? (float)defenderAttackEffective / attackerSafeDefense
            : 0f;

        sb.AppendLine("6) Primeira conta de eliminacao");
        sb.AppendLine($"- Tipo: {(option.defenderCanCounterAttack ? "simultanea" : "unilateral")}");
        sb.AppendLine($"- No defensor (bruto): {attackerAttackEffective} / {defenderSafeDefense} = {rawEliminationOnDefender:0.###}");
        sb.AppendLine($"- No atacante (bruto): {defenderAttackEffective} / {attackerSafeDefense} = {rawEliminationOnAttacker:0.###}");

        int roundedOnDefender = DPQCombatMath.DivideAndRound(attackerAttackEffective, defenderSafeDefense, attackerOutcome);
        int roundedOnAttacker = option.defenderCanCounterAttack
            ? DPQCombatMath.DivideAndRound(defenderAttackEffective, attackerSafeDefense, defenderOutcome)
            : 0;

        int appliedOnDefender = Mathf.Max(0, roundedOnDefender);
        int appliedOnAttacker = Mathf.Max(0, roundedOnAttacker);
        int projectedDefenderHp = Mathf.Max(0, defenderHp - appliedOnDefender);
        int projectedAttackerHp = Mathf.Max(0, attackerHp - appliedOnAttacker);

        sb.AppendLine("7) Arredondamentos e aplicacao inicial");
        sb.AppendLine($"- Regra defensor: {BuildRoundingExplanation(attackerAttackEffective, defenderSafeDefense, attackerOutcome, roundedOnDefender)}");
        sb.AppendLine($"- Regra atacante: {BuildRoundingExplanation(defenderAttackEffective, attackerSafeDefense, defenderOutcome, roundedOnAttacker)}");
        sb.AppendLine($"- Elim no defensor: rounded={roundedOnDefender} -> aplicado={appliedOnDefender}");
        sb.AppendLine($"- Elim no atacante: rounded={roundedOnAttacker} -> aplicado={appliedOnAttacker}");
        sb.AppendLine($"- HP projetado defensor: {defenderHp} -> {projectedDefenderHp}");
        sb.AppendLine($"- HP projetado atacante: {attackerHp} -> {projectedAttackerHp}");

        return sb.ToString();
    }

    private PositionDpqInfo ResolveDpqAtUnitPosition(UnitManager unit, string sensorPositionLabel)
    {
        PositionDpqInfo info = new PositionDpqInfo
        {
            sourceLabel = !string.IsNullOrWhiteSpace(sensorPositionLabel) ? sensorPositionLabel : "-",
            displayName = "DPQ: (nenhum)",
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

    private static PositionDpqInfo BuildDpqInfo(DPQData dpq, string source)
    {
        if (dpq == null)
        {
            return new PositionDpqInfo
            {
                sourceLabel = source,
                displayName = "DPQ: (nenhum)",
                defenseBonus = 0,
                points = 0
            };
        }

        string dpqName = !string.IsNullOrWhiteSpace(dpq.nome) ? dpq.nome : (!string.IsNullOrWhiteSpace(dpq.id) ? dpq.id : dpq.name);
        return new PositionDpqInfo
        {
            sourceLabel = source,
            displayName = dpqName,
            defenseBonus = dpq.DefesaBonus,
            points = dpq.Pontos
        };
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

    private static int GetUnitBaseDefense(UnitManager unit)
    {
        if (unit == null)
            return 0;

        if (unit.TryGetUnitData(out UnitData data) && data != null)
            return data.defense;

        return 0;
    }

    private void AutoDetectContext()
    {
        if (turnStateManager == null)
            turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();

        if (terrainDatabase == null && turnStateManager != null)
        {
            SerializedObject so = new SerializedObject(turnStateManager);
            terrainDatabase = so.FindProperty("terrainDatabase")?.objectReferenceValue as TerrainDatabase;
            weaponPriorityData = so.FindProperty("weaponPriorityData")?.objectReferenceValue as WeaponPriorityData;
        }

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

    private GameUnitClass ResolveUnitClass(UnitManager unit, out bool fromData)
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

    private RPSBonusInfo ResolveAttackRps(GameUnitClass attackerClass, WeaponCategory category, GameUnitClass defenderClass)
    {
        if (rpsDatabase == null)
            return RPSBonusInfo.NoneWithReason("sem RPSDatabase");

        if (rpsDatabase.TryResolveAttackBonus(attackerClass, category, defenderClass, out int bonus, out RPSAttackEntry entry, out _))
        {
            string text = entry != null && !string.IsNullOrWhiteSpace(entry.RpsAttackText)
                ? entry.RpsAttackText
                : $"RPS Ataque {FormatSigned(bonus)}";
            return new RPSBonusInfo(bonus, text);
        }

        return RPSBonusInfo.NoneWithReason("sem match");
    }

    private RPSBonusInfo ResolveDefenseRps(GameUnitClass defenderClass, GameUnitClass attackerClass, WeaponCategory category)
    {
        if (rpsDatabase == null)
            return RPSBonusInfo.NoneWithReason("sem RPSDatabase");

        if (rpsDatabase.TryResolveDefenseBonus(defenderClass, attackerClass, category, out int bonus, out RPSDefenseEntry entry, out _))
        {
            string text = entry != null && !string.IsNullOrWhiteSpace(entry.RpsDefenseText)
                ? entry.RpsDefenseText
                : $"RPS Defesa {FormatSigned(bonus)}";
            return new RPSBonusInfo(bonus, text);
        }

        return RPSBonusInfo.NoneWithReason("sem match");
    }

    private static Tilemap ResolveBoardTilemap(UnitManager attacker, UnitManager defender)
    {
        if (attacker != null && attacker.BoardTilemap != null)
            return attacker.BoardTilemap;
        if (defender != null && defender.BoardTilemap != null)
            return defender.BoardTilemap;
        return Object.FindAnyObjectByType<Tilemap>();
    }

    private string BuildInvalidPairReport(UnitManager attacker, UnitManager defender, List<PodeMirarInvalidOption> invalidOptions)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("[Combate] Par invalido para o sensor");
        sb.AppendLine($"Atacante: {SafeName(attacker)}");
        sb.AppendLine($"Defensor: {SafeName(defender)}");
        sb.AppendLine($"Modo sensor: {movementMode}");

        List<PodeMirarInvalidOption> pairInvalid = new List<PodeMirarInvalidOption>();
        if (invalidOptions != null)
        {
            for (int i = 0; i < invalidOptions.Count; i++)
            {
                PodeMirarInvalidOption item = invalidOptions[i];
                if (item == null)
                    continue;
                if (item.attackerUnit == attacker && item.targetUnit == defender)
                    pairInvalid.Add(item);
            }
        }

        if (pairInvalid.Count == 0)
        {
            sb.AppendLine("- Sem motivo especifico no retorno invalido.");
            sb.AppendLine("- Possiveis causas: fora de alcance, alvo nao encontrado no range global, ou sem arma candidata.");
            return sb.ToString();
        }

        sb.AppendLine($"- Motivos encontrados: {pairInvalid.Count}");
        for (int i = 0; i < pairInvalid.Count; i++)
        {
            PodeMirarInvalidOption item = pairInvalid[i];
            sb.AppendLine($"  {i + 1}) arma={ResolveWeaponName(item.weapon)} | dist={item.distance} | motivo={item.reason}");
        }

        return sb.ToString();
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

    private static UnitManager ResolveSelectedUnit()
    {
        if (Selection.activeGameObject == null)
            return null;

        UnitManager unit = Selection.activeGameObject.GetComponent<UnitManager>();
        if (unit == null)
            unit = Selection.activeGameObject.GetComponentInParent<UnitManager>();
        return unit;
    }

    private void ClearPairSelection()
    {
        attackerUnit = null;
        defenderUnit = null;
        selectedPairWeaponIndex = 0;
        currentPairOptions.Clear();
        sensorValidResults.Clear();
        sensorInvalidResults.Clear();
        report = "Ready.";
    }

    private struct PositionDpqInfo
    {
        public string sourceLabel;
        public string displayName;
        public int defenseBonus;
        public int points;

        public PositionDpqInfo(string sourceLabel, string displayName, int defenseBonus, int points)
        {
            this.sourceLabel = sourceLabel;
            this.displayName = displayName;
            this.defenseBonus = defenseBonus;
            this.points = points;
        }
    }

    private readonly struct RPSBonusInfo
    {
        public static RPSBonusInfo None => new RPSBonusInfo(0, "nao aplicavel");

        public readonly int bonusValue;
        public readonly string summary;

        public RPSBonusInfo(int bonusValue, string sourceLabel)
        {
            this.bonusValue = bonusValue;
            summary = $"{sourceLabel} | bonus={FormatSigned(bonusValue)}";
        }

        public static RPSBonusInfo NoneWithReason(string reason)
        {
            return new RPSBonusInfo(0, $"RPS +0 ({reason})");
        }
    }
}
