using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeDesembarcarSensorDebugWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedTransporter;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    private readonly List<PodeDesembarcarOption> options = new List<PodeDesembarcarOption>();
    private readonly List<PodeDesembarcarInvalidOption> invalidOptions = new List<PodeDesembarcarInvalidOption>();
    private PodeDesembarcarReport latestReport;
    private int selectedOptionIndex = -1;
    private int selectedInvalidOptionIndex = -1;
    private string statusMessage = "Ready.";
    private Vector2 scroll;
    private bool hasSelectedLine;
    private Vector3Int selectedLineStartCell;
    private Vector3Int selectedLineEndCell;
    private Color selectedLineColor = Color.cyan;
    private string selectedLineLabel = string.Empty;

    [MenuItem("Tools/Sensors/Pode Desembarcar")]
    public static void OpenWindow()
    {
        GetWindow<PodeDesembarcarSensorDebugWindow>("Pode Desembarcar");
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
        EditorGUILayout.LabelField("Sensor Pode Desembarcar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Escaneia hexagonos vizinhos para desembarque usando as regras novas:\n" +
            "1) Allowed Disembark Terrain When Transporter At (hex atual do transportador)\n" +
            "2) Passengers Can Disembark And Goes To (hex destino do passageiro)\n" +
            "3) Terrain + Structure com isBlocked (bloqueio explicito por par).",
            MessageType.Info);

        selectedTransporter = (UnitManager)EditorGUILayout.ObjectField("Transportador", selectedTransporter, typeof(UnitManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular"))
            RunSimulation();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        DrawActiveRuleSnapshot();
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        if (hasSelectedLine)
            EditorGUILayout.HelpBox($"Linha selecionada: {selectedLineLabel}", MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawLandingSection();
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Locais validos de desembarque ({options.Count})", EditorStyles.boldLabel);
        if (options.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum local valido para desembarque.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < options.Count; i++)
                DrawValidItem(i, options[i]);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Locais invalidos de desembarque ({invalidOptions.Count})", EditorStyles.boldLabel);
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

        using (new EditorGUI.DisabledScope(true))
        {
            GUILayout.Button("Desembarcar (placeholder)");
        }
    }

    private void RunSimulation()
    {
        options.Clear();
        invalidOptions.Clear();
        selectedOptionIndex = -1;
        selectedInvalidOptionIndex = -1;
        ClearSelectedLine();

        if (selectedTransporter == null)
        {
            statusMessage = "Selecione um transportador valido.";
            return;
        }

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            statusMessage = "Tilemap base nao encontrado.";
            return;
        }

        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstTerrainDatabaseAsset();
        latestReport = PodeDesembarcarSensor.CollectReport(
            selectedTransporter,
            map,
            db);
        bool canDisembark = false;
        if (latestReport != null)
        {
            options.Clear();
            invalidOptions.Clear();
            if (latestReport.locaisValidosDeDesembarque != null)
                options.AddRange(latestReport.locaisValidosDeDesembarque);
            if (latestReport.locaisInvalidosDeDesembarque != null)
                invalidOptions.AddRange(latestReport.locaisInvalidosDeDesembarque);
            canDisembark = latestReport.canDisembark;
        }

        statusMessage = canDisembark
            ? $"Sensor TRUE. {options.Count} local(is) valido(s), {invalidOptions.Count} invalido(s)."
            : $"Sensor FALSE. Nenhum local elegivel ({invalidOptions.Count} invalido(s)).";

        if (options.Count > 0)
        {
            selectedOptionIndex = 0;
            SelectLineForDrawing(options[0]);
        }

        Debug.Log($"[PodeDesembarcarSensorDebug] Transportador={selectedTransporter.name} | canDisembark={canDisembark} | validos={options.Count} | invalidos={invalidOptions.Count}");
        for (int i = 0; i < options.Count; i++)
        {
            PodeDesembarcarOption item = options[i];
            if (item == null)
                continue;

            string passengerName = item.passengerUnit != null ? item.passengerUnit.name : "(null)";
            Debug.Log($"[PodeDesembarcarSensorDebug][VALIDO] {i + 1}. passageiro={passengerName} | slot={item.transporterSlotIndex}/{item.transporterSeatIndex} | cell={item.disembarkCell.x},{item.disembarkCell.y} | custo={item.enterCost} | {item.displayLabel}");
        }

        for (int i = 0; i < invalidOptions.Count; i++)
        {
            PodeDesembarcarInvalidOption item = invalidOptions[i];
            if (item == null)
                continue;

            string passengerName = item.passengerUnit != null ? item.passengerUnit.name : "(nenhum)";
            Debug.Log($"[PodeDesembarcarSensorDebug][INVALIDO] {i + 1}. passageiro={passengerName} | slot={item.transporterSlotIndex}/{item.transporterSeatIndex} | cell={item.evaluatedCell.x},{item.evaluatedCell.y} | enterCost={item.enterCost} | motivo={item.reason}");
        }
    }

    private void DrawValidItem(int index, PodeDesembarcarOption item)
    {
        if (item == null)
            return;

        string passengerName = item.passengerUnit != null ? item.passengerUnit.name : "(null)";
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"{index + 1}. valido", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Passageiro", passengerName);
        EditorGUILayout.LabelField("Slot/Vaga", $"{item.transporterSlotIndex}/{item.transporterSeatIndex}");
        EditorGUILayout.LabelField("Hex destino", $"{item.disembarkCell.x},{item.disembarkCell.y}");
        EditorGUILayout.LabelField("Custo", item.enterCost.ToString());
        EditorGUILayout.LabelField("Detalhe", string.IsNullOrWhiteSpace(item.displayLabel) ? "-" : item.displayLabel);
        if (GUILayout.Button("Desenhar Linha no Scene View"))
        {
            selectedOptionIndex = index;
            selectedInvalidOptionIndex = -1;
            SelectLineForDrawing(item);
        }
        EditorGUILayout.EndVertical();
    }

    private void SelectLineForDrawing(PodeDesembarcarOption option)
    {
        if (option == null || option.transporterUnit == null)
        {
            ClearSelectedLine();
            return;
        }

        selectedLineStartCell = option.transporterUnit.CurrentCellPosition;
        selectedLineStartCell.z = 0;
        selectedLineEndCell = option.disembarkCell;
        selectedLineEndCell.z = 0;
        selectedLineColor = Color.green;
        string passengerName = option.passengerUnit != null ? option.passengerUnit.name : "passageiro";
        selectedLineLabel = $"VAL: {passengerName} -> {selectedLineEndCell.x},{selectedLineEndCell.y}";
        hasSelectedLine = true;
        SceneView.RepaintAll();
    }

    private void SelectLineForDrawing(PodeDesembarcarInvalidOption option)
    {
        if (option == null || selectedTransporter == null)
        {
            ClearSelectedLine();
            return;
        }

        selectedLineStartCell = selectedTransporter.CurrentCellPosition;
        selectedLineStartCell.z = 0;
        selectedLineEndCell = option.evaluatedCell;
        selectedLineEndCell.z = 0;
        selectedLineColor = Color.red;
        string passengerName = option.passengerUnit != null ? option.passengerUnit.name : "passageiro";
        selectedLineLabel = $"INV: {passengerName} -> {selectedLineEndCell.x},{selectedLineEndCell.y}";
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

        Tilemap map = ResolveTilemap();
        if (map == null)
            return;

        Vector3 start = map.GetCellCenterWorld(selectedLineStartCell);
        Vector3 end = map.GetCellCenterWorld(selectedLineEndCell);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = selectedLineColor;
        Handles.DrawAAPolyLine(4f, start, end);
        Handles.SphereHandleCap(0, start, Quaternion.identity, 0.15f, EventType.Repaint);
        Handles.SphereHandleCap(0, end, Quaternion.identity, 0.15f, EventType.Repaint);

        Vector3 mid = Vector3.Lerp(start, end, 0.5f);
        Handles.Label(mid + new Vector3(0.1f, 0.1f, 0f), selectedLineLabel);
    }

    private void DrawInvalidItem(int index, PodeDesembarcarInvalidOption item)
    {
        if (item == null)
            return;

        string passengerName = item.passengerUnit != null ? item.passengerUnit.name : "(nenhum)";
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"{index + 1}. invalido", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Passageiro", passengerName);
        EditorGUILayout.LabelField("Slot/Vaga", $"{item.transporterSlotIndex}/{item.transporterSeatIndex}");
        EditorGUILayout.LabelField("Hex avaliado", $"{item.evaluatedCell.x},{item.evaluatedCell.y}");
        EditorGUILayout.LabelField("Custo", item.enterCost >= 0 ? item.enterCost.ToString() : "-");
        EditorGUILayout.LabelField("Motivo", string.IsNullOrWhiteSpace(item.reason) ? "-" : item.reason);
        if (GUILayout.Button("Desenhar Linha no Scene View"))
        {
            selectedInvalidOptionIndex = index;
            selectedOptionIndex = -1;
            SelectLineForDrawing(item);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawActiveRuleSnapshot()
    {
        if (selectedTransporter == null || !selectedTransporter.TryGetUnitData(out UnitData data) || data == null)
            return;

        int blockedAtCurrentHex = CountBlockedPairs(data.allowedDisembarkWhenTransporterAtTerrainStructures);
        int blockedAtDestination = CountBlockedPairs(data.passengersCanDisembarkAndGoesToTerrainStructures);
        string snapshot =
            $"Transporter At: terrain={SafeCount(data.allowedDisembarkWhenTransporterAtTerrains)}, " +
            $"terrain+structure={SafeCount(data.allowedDisembarkWhenTransporterAtTerrainStructures)} (blocked={blockedAtCurrentHex}), " +
            $"constructions={SafeCount(data.allowedDisembarkWhenTransporterAtConstructions)}\n" +
            $"Passenger Goes To: terrain={SafeCount(data.passengersCanDisembarkAndGoesToTerrains)}, " +
            $"terrain+structure={SafeCount(data.passengersCanDisembarkAndGoesToTerrainStructures)} (blocked={blockedAtDestination}), " +
            $"constructions={SafeCount(data.passengersCanDisembarkAndGoesToConstructions)}";

        EditorGUILayout.HelpBox(snapshot, MessageType.None);
    }

    private static int SafeCount<T>(List<T> list)
    {
        return list != null ? list.Count : 0;
    }

    private static int CountBlockedPairs(List<TransportStructureTerrainRule> list)
    {
        if (list == null || list.Count == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            TransportStructureTerrainRule item = list[i];
            if (item != null && item.isBlocked)
                count++;
        }

        return count;
    }

    private void DrawLandingSection()
    {
        EditorGUILayout.LabelField("Local de Pouso", EditorStyles.boldLabel);
        if (latestReport == null || latestReport.localDePouso == null)
        {
            EditorGUILayout.HelpBox("Sem avaliacao de pouso nesta simulacao.", MessageType.Info);
            return;
        }

        var landing = latestReport.localDePouso;
        EditorGUILayout.LabelField("Status", landing.isValid ? "valido" : "invalido");
        EditorGUILayout.LabelField("Explicacao", string.IsNullOrWhiteSpace(landing.explanation) ? "-" : landing.explanation);
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedTransporter != null && selectedTransporter.BoardTilemap != null)
            return selectedTransporter.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void AutoDetectContext()
    {
        if (selectedTransporter == null)
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
            selectedTransporter = unit;
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
