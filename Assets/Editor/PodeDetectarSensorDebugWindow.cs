using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeDetectarSensorDebugWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private MatchController matchController;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private bool useGameplaySensorContext = true;
    [SerializeField] private bool logToConsole = true;

    private readonly List<PodeDetectarOption> detectedStealth = new List<PodeDetectarOption>();
    private readonly List<PodeDetectarOption> undetectedStealth = new List<PodeDetectarOption>();
    private readonly List<PodeDetectarOption> spottedCandidates = new List<PodeDetectarOption>();
    private readonly List<PodeDetectarOption> inRangeButLosBlocked = new List<PodeDetectarOption>();
    private readonly HashSet<UnitManager> forcedDetectedIndicatorUnits = new HashSet<UnitManager>();
    private Vector2 windowScroll;
    private string statusMessage = "Ready.";
    private bool hasSelectedLine;
    private Vector3 selectedLineStartWorld;
    private Vector3 selectedLineEndWorld;
    private Color selectedLineColor = Color.green;
    private string selectedLineLabel = string.Empty;

    [MenuItem("Tools/Sensors/Pode Detectar")]
    public static void OpenWindow()
    {
        GetWindow<PodeDetectarSensorDebugWindow>("Pode Detectar");
    }

    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearSelectedLine();
        ClearForcedDetectedIndicators();
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);

        EditorGUILayout.LabelField("Sensor Pode Detectar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scan de proximidade por visao/specialization + LOS. " +
            "Unidades stealth so entram em \"furtivas detectadas\" quando o observador tiver especializacao para detectar stealth naquele dominio/altura.",
            MessageType.Info);

        selectedUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade", selectedUnit, typeof(UnitManager), true);
        turnStateManager = (TurnStateManager)EditorGUILayout.ObjectField("TurnStateManager", turnStateManager, typeof(TurnStateManager), true);
        matchController = (MatchController)EditorGUILayout.ObjectField("MatchController", matchController, typeof(MatchController), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        dpqAirHeightConfig = (DPQAirHeightConfig)EditorGUILayout.ObjectField("DPQ Air Height", dpqAirHeightConfig, typeof(DPQAirHeightConfig), false);
        useGameplaySensorContext = EditorGUILayout.ToggleLeft("Usar contexto do gameplay (MatchController)", useGameplaySensorContext);
        logToConsole = EditorGUILayout.ToggleLeft("Log no Console", logToConsole);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular"))
            RunSimulation();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        EditorGUILayout.Space(6f);
        DrawOptionList("Unidades furtivas detectadas", detectedStealth, MessageType.Info, Color.green);
        EditorGUILayout.Space(6f);
        DrawOptionList("Unidades furtivas nao detectadas", undetectedStealth, MessageType.Warning, Color.red);
        EditorGUILayout.Space(6f);
        DrawOptionList("Candidatos avistados", spottedCandidates, MessageType.None, Color.green);
        EditorGUILayout.Space(6f);
        DrawOptionList("Candidatos no alcance mas nao detectados por LOS", inRangeButLosBlocked, MessageType.Warning, Color.red);

        EditorGUILayout.EndScrollView();
    }

    private void DrawOptionList(string title, List<PodeDetectarOption> items, MessageType emptyMessageType, Color lineColor)
    {
        EditorGUILayout.LabelField($"{title} ({items.Count})", EditorStyles.boldLabel);
        if (items.Count <= 0)
        {
            EditorGUILayout.HelpBox("Nenhum item.", emptyMessageType);
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            PodeDetectarOption item = items[i];
            if (item == null || item.targetUnit == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{i + 1}. {item.targetUnit.name}", EditorStyles.boldLabel);
            if (GUILayout.Button("Desenhar Linha", GUILayout.Width(110f)))
                SelectLineForDrawing(item, lineColor, $"{title}: {item.targetUnit.name}");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Hex", $"{item.targetCell.x},{item.targetCell.y}");
            EditorGUILayout.LabelField("Distancia", $"{item.distance} / alcance {item.detectionRangeUsed}");
            EditorGUILayout.LabelField("Camada", $"{item.targetDomain}/{item.targetHeightLevel}");
            EditorGUILayout.LabelField("LOS direta", item.hasDirectLos ? "SIM" : "NAO");
            if (item.usedForwardObserver)
            {
                string observerName = item.forwardObserverUnit != null ? item.forwardObserverUnit.name : "(desconhecido)";
                EditorGUILayout.LabelField("Observador avancado", observerName);
            }
            if (item.blockedCell != Vector3Int.zero)
                EditorGUILayout.LabelField("Bloqueio LOS", $"{item.blockedCell.x},{item.blockedCell.y}");
            if (!string.IsNullOrWhiteSpace(item.reason))
                EditorGUILayout.LabelField("Obs", item.reason);
            EditorGUILayout.EndVertical();
        }
    }

    private void TryUseCurrentSelection()
    {
        if (Selection.activeGameObject == null)
            return;

        UnitManager unit = Selection.activeGameObject.GetComponent<UnitManager>();
        if (unit == null)
            unit = Selection.activeGameObject.GetComponentInParent<UnitManager>();
        if (unit != null)
            selectedUnit = unit;
    }

    private void RunSimulation()
    {
        detectedStealth.Clear();
        undetectedStealth.Clear();
        spottedCandidates.Clear();
        inRangeButLosBlocked.Clear();
        ClearForcedDetectedIndicators();
        ClearSelectedLine();

        if (selectedUnit == null)
        {
            statusMessage = "Selecione uma unidade valida.";
            return;
        }

        Tilemap map = ResolveBoardTilemapForSimulation();
        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstAsset<TerrainDatabase>();
        bool enableLos = true;
        bool enableSpotter = true;
        bool enableStealth = true;
        if (useGameplaySensorContext && matchController != null)
        {
            enableLos = matchController.EnableLosValidation;
            enableSpotter = matchController.EnableSpotter;
            enableStealth = matchController.EnableStealthValidation;
        }

        bool ok = PodeDetectarSensor.CollectDetection(
            selectedUnit,
            map,
            db,
            detectedStealth,
            undetectedStealth,
            spottedCandidates,
            inRangeButLosBlocked,
            out string reason,
            dpqAirHeightConfig,
            enableLos,
            enableSpotter,
            enableStealth);

        statusMessage = ok
            ? $"Sensor TRUE. {reason}"
            : $"Sensor FALSE. {reason}";

        ApplyForcedDetectedIndicatorsForLayerVisibleStealthUnits();

        if (!logToConsole)
            return;

        Debug.Log($"[PodeDetectarSensorDebug] Unit={selectedUnit.name} | GameSetup(LoS={enableLos},Spotter={enableSpotter},Stealth={enableStealth}) | {reason}");
        LogOptionList("FURTIVAS", detectedStealth);
        LogOptionList("FURTIVAS_NAO_DETECTADAS", undetectedStealth);
        LogOptionList("AVISTADAS", spottedCandidates);
        LogOptionList("SEM_LOS", inRangeButLosBlocked);
    }

    private void ApplyForcedDetectedIndicatorsForLayerVisibleStealthUnits()
    {
        int marked = 0;
        for (int i = 0; i < detectedStealth.Count; i++)
        {
            PodeDetectarOption option = detectedStealth[i];
            UnitManager target = option != null ? option.targetUnit : null;
            if (target == null)
                continue;

            UnitHudController hud = ResolveOwnUnitHud(target);
            if (hud == null)
                continue;

            hud.SetDetectedIndicatorVisible(true);
            if (forcedDetectedIndicatorUnits.Add(target))
                marked++;
        }

        for (int i = 0; i < spottedCandidates.Count; i++)
        {
            PodeDetectarOption option = spottedCandidates[i];
            UnitManager target = option != null ? option.targetUnit : null;
            if (target == null)
                continue;

            if (!target.TryGetUnitData(out UnitData unitData) || unitData == null)
                continue;

            bool hasAnyStealthConfigured = unitData.ResolveStealthSkillsForDetection().Count > 0;
            bool stealthActiveAtCurrentLayer = unitData.IsStealthUnit(target.GetDomain(), target.GetHeightLevel());
            if (!hasAnyStealthConfigured || stealthActiveAtCurrentLayer)
                continue;

            UnitHudController hud = ResolveOwnUnitHud(target);
            if (hud == null)
                continue;

            hud.SetDetectedIndicatorVisible(true);
            if (forcedDetectedIndicatorUnits.Add(target))
                marked++;
        }

        if (marked > 0)
            statusMessage += $" | Olhinho ativo em {marked} unidade(s) detectadas nesta simulacao.";
    }

    private void ClearForcedDetectedIndicators()
    {
        foreach (UnitManager unit in forcedDetectedIndicatorUnits)
        {
            if (unit == null)
                continue;

            UnitHudController hud = ResolveOwnUnitHud(unit);
            if (hud == null)
                continue;

            hud.SetDetectedIndicatorVisible(false);
        }

        forcedDetectedIndicatorUnits.Clear();
    }

    private static UnitHudController ResolveOwnUnitHud(UnitManager unit)
    {
        if (unit == null)
            return null;

        UnitHudController[] candidates = unit.GetComponentsInChildren<UnitHudController>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            UnitHudController candidate = candidates[i];
            if (candidate == null)
                continue;

            UnitManager owner = candidate.GetComponentInParent<UnitManager>();
            if (owner == unit)
                return candidate;
        }

        return null;
    }

    private void SelectLineForDrawing(PodeDetectarOption item, Color lineColor, string label)
    {
        if (item == null || item.targetUnit == null)
            return;

        Tilemap map = overrideTilemap != null ? overrideTilemap : (selectedUnit != null ? selectedUnit.BoardTilemap : item.targetUnit.BoardTilemap);
        Vector3 startWorld = item.observerUnit != null ? item.observerUnit.transform.position : Vector3.zero;
        Vector3 endWorld = item.targetUnit.transform.position;
        if (map != null)
        {
            Vector3Int startCell = item.observerCell;
            Vector3Int endCell = item.targetCell;
            startCell.z = 0;
            endCell.z = 0;
            startWorld = map.GetCellCenterWorld(startCell);
            endWorld = map.GetCellCenterWorld(endCell);
        }

        hasSelectedLine = true;
        selectedLineStartWorld = startWorld;
        selectedLineEndWorld = endWorld;
        selectedLineColor = lineColor;
        selectedLineLabel = label;
        SceneView.RepaintAll();
    }

    private void ClearSelectedLine()
    {
        hasSelectedLine = false;
        selectedLineLabel = string.Empty;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!hasSelectedLine)
            return;

        Handles.color = selectedLineColor;
        Handles.DrawAAPolyLine(4f, selectedLineStartWorld, selectedLineEndWorld);
        Vector3 mid = Vector3.Lerp(selectedLineStartWorld, selectedLineEndWorld, 0.5f);
        Handles.Label(mid + Vector3.up * 0.2f, selectedLineLabel);
    }

    private static void LogOptionList(string tag, List<PodeDetectarOption> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            PodeDetectarOption item = items[i];
            if (item == null || item.targetUnit == null)
                continue;

            string observerName = item.observerUnit != null ? item.observerUnit.name : "(null)";
            string targetName = item.targetUnit.name;
            string forwardObserverName = item.forwardObserverUnit != null ? item.forwardObserverUnit.name : "-";
            Debug.Log(
                $"[PodeDetectarSensorDebug][{tag}] {i + 1}. {observerName} -> {targetName} | " +
                $"dist={item.distance}/{item.detectionRangeUsed} | layer={item.targetDomain}/{item.targetHeightLevel} | " +
                $"losDireta={(item.hasDirectLos ? "sim" : "nao")} | forwardObserver={forwardObserverName} | motivo={item.reason}");
        }
    }

    private void AutoDetectContext()
    {
        if (turnStateManager == null)
            turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();
        if (matchController == null)
            matchController = Object.FindAnyObjectByType<MatchController>();
        if (overrideTilemap == null)
            overrideTilemap = FindPreferredTilemap();
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAsset<DPQAirHeightConfig>();
    }

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap board = FindTilemapByName("TileMap");
        if (board != null)
            return board;

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps == null || maps.Length == 0)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;
            if (string.Equals(map.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return maps[0];
    }

    private Tilemap ResolveBoardTilemapForSimulation()
    {
        if (overrideTilemap != null)
            return overrideTilemap;

        if (useGameplaySensorContext)
        {
            Tilemap gameplayMap = ResolveGameplayTerrainTilemap();
            if (gameplayMap != null)
                return gameplayMap;
        }

        if (selectedUnit != null && selectedUnit.BoardTilemap != null)
            return selectedUnit.BoardTilemap;

        return FindPreferredTilemap();
    }

    private Tilemap ResolveGameplayTerrainTilemap()
    {
        if (turnStateManager == null)
            turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();
        if (turnStateManager == null)
            return null;

        SerializedObject so = new SerializedObject(turnStateManager);
        SerializedProperty terrainProp = so.FindProperty("terrainTilemap");
        return terrainProp != null ? terrainProp.objectReferenceValue as Tilemap : null;
    }

    private static Tilemap FindTilemapByName(string expectedName)
    {
        if (string.IsNullOrWhiteSpace(expectedName))
            return null;

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            if (string.Equals(map.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return null;
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
}
