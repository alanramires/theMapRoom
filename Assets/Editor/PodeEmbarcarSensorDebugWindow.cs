using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeEmbarcarSensorDebugWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedPassenger;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private SensorMovementMode movementMode = SensorMovementMode.MoveuParado;
    [SerializeField] private int manualRemainingMovement = 0;
    [SerializeField] private bool useManualRemainingMovement = false;

    private readonly List<PodeEmbarcarOption> options = new List<PodeEmbarcarOption>();
    private readonly List<PodeEmbarcarInvalidOption> invalidOptions = new List<PodeEmbarcarInvalidOption>();
    private int selectedOptionIndex = -1;
    private string statusMessage = "Ready.";
    private Vector2 scroll;
    private bool hasSelectedLine;
    private Vector3Int selectedLineStartCell;
    private Vector3Int selectedLineEndCell;
    private Color selectedLineColor = Color.cyan;
    private string selectedLineLabel = string.Empty;

    [MenuItem("Tools/Sensors/Pode Embarcar")]
    public static void OpenWindow()
    {
        GetWindow<PodeEmbarcarSensorDebugWindow>("Pode Embarcar");
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
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sensor Pode Embarcar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Usa a unidade selecionada como passageiro e escaneia transportadores adjacentes (range 1).", MessageType.Info);

        selectedPassenger = (UnitManager)EditorGUILayout.ObjectField("Passageiro", selectedPassenger, typeof(UnitManager), true);
        turnStateManager = (TurnStateManager)EditorGUILayout.ObjectField("TurnStateManager", turnStateManager, typeof(TurnStateManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        movementMode = (SensorMovementMode)EditorGUILayout.EnumPopup("Modo", movementMode);
        useManualRemainingMovement = EditorGUILayout.ToggleLeft("Usar movimento restante manual", useManualRemainingMovement);
        using (new EditorGUI.DisabledScope(!useManualRemainingMovement))
            manualRemainingMovement = EditorGUILayout.IntField("Movimento Restante", Mathf.Max(0, manualRemainingMovement));

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
        EditorGUILayout.LabelField($"Transportadores validos ({options.Count})", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (options.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum transportador valido.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < options.Count; i++)
            {
                PodeEmbarcarOption option = options[i];
                if (option == null)
                    continue;

                EditorGUILayout.BeginVertical("box");
                bool selected = selectedOptionIndex == i;
                if (GUILayout.Toggle(selected, $"{i + 1}. {option.displayLabel}", "Button"))
                {
                    selectedOptionIndex = i;
                    SelectLineForDrawing(option);
                }

                string transporterName = option.transporterUnit != null ? option.transporterUnit.name : "(null)";
                EditorGUILayout.LabelField("Transportador", transporterName);
                EditorGUILayout.LabelField("Slot", option.transporterSlotIndex.ToString());
                EditorGUILayout.LabelField("Custo de Entrada", option.enterCost.ToString());
                EditorGUILayout.LabelField("Movimento Restante", option.remainingMovementBeforeEmbark.ToString());
                if (GUILayout.Button("Desenhar Linha no Scene View"))
                    SelectLineForDrawing(option);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Resultados Invalidos ({invalidOptions.Count})", EditorStyles.boldLabel);
        if (invalidOptions.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma invalidacao registrada nesta simulacao.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < invalidOptions.Count; i++)
                DrawInvalidItem(i, invalidOptions[i]);
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(selectedOptionIndex < 0 || selectedOptionIndex >= options.Count))
        {
            if (GUILayout.Button("Embarcar"))
            {
                PodeEmbarcarOption option = selectedOptionIndex >= 0 && selectedOptionIndex < options.Count ? options[selectedOptionIndex] : null;
                string label = option != null ? option.displayLabel : "(opcao invalida)";
                Debug.Log($"[PodeEmbarcarSensorDebug] Embarcar selecionado: {label}. (Execucao de embarque ainda nao implementada)");
            }
        }
    }

    private void RunSimulation()
    {
        options.Clear();
        invalidOptions.Clear();
        selectedOptionIndex = -1;
        ClearSelectedLine();

        if (selectedPassenger == null)
        {
            statusMessage = "Selecione uma unidade passageira valida.";
            return;
        }

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            statusMessage = "Tilemap base nao encontrado.";
            return;
        }

        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstTerrainDatabaseAsset();
        int remainingMovement = ResolveRemainingMovement(map, db);
        bool canEmbark = PodeEmbarcarSensor.CollectOptions(
            selectedPassenger,
            map,
            db,
            remainingMovement,
            options,
            invalidOptions);

        statusMessage = canEmbark
            ? $"Sensor TRUE. {options.Count} transportador(es) valido(s), {invalidOptions.Count} invalido(s)."
            : $"Sensor FALSE. Nenhum transportador elegivel ({invalidOptions.Count} invalido(s)).";

        if (options.Count > 0)
        {
            selectedOptionIndex = 0;
            SelectLineForDrawing(options[0]);
        }

        Debug.Log($"[PodeEmbarcarSensorDebug] Passageiro={selectedPassenger.name} | canEmbark={canEmbark} | validos={options.Count} | invalidos={invalidOptions.Count}");
        for (int i = 0; i < options.Count; i++)
        {
            PodeEmbarcarOption item = options[i];
            if (item == null)
                continue;

            string transporterName = item.transporterUnit != null ? item.transporterUnit.name : "(null)";
            Debug.Log($"[PodeEmbarcarSensorDebug][VALIDO] {i + 1}. {transporterName} | slot={item.transporterSlotIndex} | custo={item.enterCost} | movRest={item.remainingMovementBeforeEmbark} | {item.displayLabel}");
        }

        for (int i = 0; i < invalidOptions.Count; i++)
        {
            PodeEmbarcarInvalidOption item = invalidOptions[i];
            if (item == null)
                continue;

            string transporterName = item.transporterUnit != null ? item.transporterUnit.name : "(nenhum)";
            Debug.Log($"[PodeEmbarcarSensorDebug][INVALIDO] {i + 1}. cell={item.evaluatedCell.x},{item.evaluatedCell.y} | transp={transporterName} | slot={item.transporterSlotIndex} | custo={item.enterCost} | movRest={item.remainingMovementBeforeEmbark} | motivo={item.reason}");
        }
    }

    private void SelectLineForDrawing(PodeEmbarcarOption option)
    {
        if (option == null || option.sourceUnit == null || option.transporterUnit == null)
        {
            ClearSelectedLine();
            return;
        }

        selectedLineStartCell = option.sourceUnit.CurrentCellPosition;
        selectedLineStartCell.z = 0;
        selectedLineEndCell = option.transporterUnit.CurrentCellPosition;
        selectedLineEndCell.z = 0;
        selectedLineColor = option.enterCost > option.remainingMovementBeforeEmbark ? Color.red : Color.cyan;
        selectedLineLabel = $"Embarque: {option.sourceUnit.name} -> {option.transporterUnit.name} (slot {option.transporterSlotIndex})";
        hasSelectedLine = true;
        SceneView.RepaintAll();
    }

    private void ClearSelectedLine()
    {
        hasSelectedLine = false;
        selectedLineLabel = string.Empty;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!hasSelectedLine)
            return;

        Tilemap map = ResolveDrawTilemap();
        if (map == null)
            return;

        Vector3 start = map.GetCellCenterWorld(selectedLineStartCell);
        Vector3 end = map.GetCellCenterWorld(selectedLineEndCell);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = selectedLineColor;
        Handles.DrawAAPolyLine(4f, start, end);
        Handles.SphereHandleCap(0, start, Quaternion.identity, 0.12f, EventType.Repaint);
        Handles.SphereHandleCap(0, end, Quaternion.identity, 0.12f, EventType.Repaint);

        Vector3 mid = Vector3.Lerp(start, end, 0.5f);
        Handles.Label(mid + new Vector3(0.1f, 0.1f, 0f), selectedLineLabel);
    }

    private Tilemap ResolveDrawTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedPassenger != null && selectedPassenger.BoardTilemap != null)
            return selectedPassenger.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void DrawInvalidItem(int index, PodeEmbarcarInvalidOption item)
    {
        if (item == null)
            return;

        string transporterName = item.transporterUnit != null ? item.transporterUnit.name : "(nenhum)";
        string slotText = item.transporterSlotIndex >= 0 ? item.transporterSlotIndex.ToString() : "-";

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"{index + 1}. invalido", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Hex Avaliado", $"{item.evaluatedCell.x},{item.evaluatedCell.y}");
        EditorGUILayout.LabelField("Transportador", transporterName);
        EditorGUILayout.LabelField("Slot", slotText);
        EditorGUILayout.LabelField("Custo de Entrada", item.enterCost >= 0 ? item.enterCost.ToString() : "-");
        EditorGUILayout.LabelField("Movimento Restante", item.remainingMovementBeforeEmbark.ToString());
        EditorGUILayout.LabelField("Motivo", string.IsNullOrWhiteSpace(item.reason) ? "-" : item.reason);
        EditorGUILayout.EndVertical();
    }

    private int ResolveRemainingMovement(Tilemap map, TerrainDatabase db)
    {
        if (useManualRemainingMovement)
            return Mathf.Max(0, manualRemainingMovement);

        if (selectedPassenger == null)
            return 0;

        int baseMove = Mathf.Max(0, selectedPassenger.GetMovementRange());
        if (turnStateManager == null || turnStateManager.SelectedUnit != selectedPassenger)
            return baseMove;

        TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
        if (state == TurnStateManager.CursorState.MoveuParado)
            return baseMove;

        if (state != TurnStateManager.CursorState.MoveuAndando)
            return baseMove;

        if (!turnStateManager.TryGetCommittedMovementPath(out List<Vector3Int> path, out _, out _) || path == null || path.Count < 2)
            return baseMove;

        int spent = UnitMovementPathRules.CalculateAutonomyCostForPath(
            map,
            selectedPassenger,
            path,
            db);
        return Mathf.Max(0, baseMove - Mathf.Max(0, spent));
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedPassenger != null && selectedPassenger.BoardTilemap != null)
            return selectedPassenger.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void AutoDetectContext()
    {
        if (turnStateManager == null)
            turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();
        if (selectedPassenger == null)
            selectedPassenger = turnStateManager != null ? turnStateManager.SelectedUnit : null;
        if (selectedPassenger == null)
            TryUseCurrentSelection();
        if (overrideTilemap == null)
            overrideTilemap = FindPreferredTilemap();
        if (terrainDatabase == null)
            terrainDatabase = FindFirstTerrainDatabaseAsset();
    }

    private void TryUseCurrentSelection()
    {
        if (Selection.activeGameObject == null)
            return;

        UnitManager unit = Selection.activeGameObject.GetComponent<UnitManager>();
        if (unit == null)
            unit = Selection.activeGameObject.GetComponentInParent<UnitManager>();
        if (unit != null)
            selectedPassenger = unit;
    }

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps == null || maps.Length == 0)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map != null && string.Equals(map.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return maps[0];
    }

    private static TerrainDatabase FindFirstTerrainDatabaseAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TerrainDatabase db = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(path);
            if (db != null)
                return db;
        }

        return null;
    }
}
