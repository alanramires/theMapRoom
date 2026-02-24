using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeDesembarcarSensorDebugWindow : EditorWindow
{
    private sealed class DebugDisembarkOrder
    {
        public UnitManager passenger;
        public int slotIndex;
        public int seatIndex;
        public Vector3Int targetCell;
        public string label;
    }

    [SerializeField] private UnitManager selectedTransporter;
    [SerializeField] private UnitManager selectedPassenger;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private TurnStateManager selectedTurnStateManager;

    private readonly List<PodeDesembarcarOption> options = new List<PodeDesembarcarOption>();
    private readonly List<PodeDesembarcarInvalidOption> invalidOptions = new List<PodeDesembarcarInvalidOption>();
    private readonly List<DebugDisembarkOrder> debugOrders = new List<DebugDisembarkOrder>();
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
    private string disembarkMessage = "Nenhuma ordem em fila.";
    private readonly List<UnitManager> embarkedPassengers = new List<UnitManager>();
    private readonly List<string> embarkedPassengerLabels = new List<string>();
    private UnitManager cachedPassengerTransporter;
    private int selectedPassengerIndex = -1;

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
        RefreshEmbarkedPassengersIfNeeded();
        DrawPassengerSelector();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular"))
            RunSimulation();
        if (GUILayout.Button("Limpar Ordens"))
            ClearDebugOrders("Fila limpa.");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        DrawActiveRuleSnapshot();
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        if (hasSelectedLine)
            EditorGUILayout.HelpBox($"Linha selecionada: {selectedLineLabel}", MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawDebugDisembarkOrderSection();
        EditorGUILayout.Space(8f);
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
    }

    private void RunSimulation()
    {
        RefreshEmbarkedPassengersIfNeeded(force: true);
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
        if (!TryResolveSelectedPassengerForSimulation(out UnitManager passengerToSimulate))
            return;

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
            {
                for (int i = 0; i < latestReport.locaisValidosDeDesembarque.Count; i++)
                {
                    PodeDesembarcarOption item = latestReport.locaisValidosDeDesembarque[i];
                    if (item == null || item.passengerUnit != passengerToSimulate)
                        continue;
                    if (IsCellReservedByOtherPassenger(item.disembarkCell, passengerToSimulate))
                    {
                        invalidOptions.Add(new PodeDesembarcarInvalidOption
                        {
                            passengerUnit = item.passengerUnit,
                            transporterSlotIndex = item.transporterSlotIndex,
                            transporterSeatIndex = item.transporterSeatIndex,
                            evaluatedCell = item.disembarkCell,
                            enterCost = item.enterCost,
                            reason = "Hex reservado por ordem de desembarque ja definida."
                        });
                        continue;
                    }

                    options.Add(item);
                }
            }
            if (latestReport.locaisInvalidosDeDesembarque != null)
            {
                for (int i = 0; i < latestReport.locaisInvalidosDeDesembarque.Count; i++)
                {
                    PodeDesembarcarInvalidOption item = latestReport.locaisInvalidosDeDesembarque[i];
                    if (item == null || item.passengerUnit != passengerToSimulate)
                        continue;
                    invalidOptions.Add(item);
                }
            }
            canDisembark = options.Count > 0;
        }

        statusMessage = canDisembark
            ? $"Sensor TRUE para {passengerToSimulate.name}. {options.Count} local(is) valido(s), {invalidOptions.Count} invalido(s)."
            : $"Sensor FALSE para {passengerToSimulate.name}. Nenhum local elegivel ({invalidOptions.Count} invalido(s)).";

        if (options.Count > 0)
        {
            selectedOptionIndex = 0;
            SelectLineForDrawing(options[0]);
        }

        Debug.Log($"[PodeDesembarcarSensorDebug] Transportador={selectedTransporter.name} | passageiro={passengerToSimulate.name} | canDisembark={canDisembark} | validos={options.Count} | invalidos={invalidOptions.Count}");
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
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Desenhar Linha no Scene View"))
        {
            selectedOptionIndex = index;
            selectedInvalidOptionIndex = -1;
            SelectLineForDrawing(item);
        }
        if (GUILayout.Button("Adicionar na Ordem"))
            TryAddDebugOrder(item);
        EditorGUILayout.EndHorizontal();
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

    private void DrawDebugDisembarkOrderSection()
    {
        EditorGUILayout.LabelField($"Fila de Desembarque (Debug) ({debugOrders.Count})", EditorStyles.boldLabel);
        if (debugOrders.Count == 0)
        {
            EditorGUILayout.HelpBox("Fila vazia. Clique em \"Adicionar na Ordem\" em um local valido.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < debugOrders.Count; i++)
            {
                DebugDisembarkOrder order = debugOrders[i];
                if (order == null)
                    continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{i + 1}. {order.label}");
                EditorGUILayout.LabelField("Slot/Vaga", $"{order.slotIndex}/{order.seatIndex}");
                EditorGUILayout.LabelField("Hex destino", $"{order.targetCell.x},{order.targetCell.y}");
                if (GUILayout.Button("Remover Ordem"))
                {
                    debugOrders.RemoveAt(i);
                    disembarkMessage = "Ordem removida.";
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Gerar 1 ordem por passageiro"))
            BuildAutoOrdersFromOptions();
        using (new EditorGUI.DisabledScope(selectedTransporter == null || debugOrders.Count == 0))
        {
            if (GUILayout.Button("Desembarcar (DEBUG SKIP)"))
                ExecuteDebugDisembarkSkip();
        }
        EditorGUILayout.EndHorizontal();
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
        if (selectedTurnStateManager == null)
            selectedTurnStateManager = FindTurnStateManagerInScene();
        RefreshEmbarkedPassengersIfNeeded();
    }

    private void TryUseCurrentSelection()
    {
        if (Selection.activeGameObject == null)
            return;

        UnitManager unit = Selection.activeGameObject.GetComponent<UnitManager>();
        if (unit == null)
            unit = Selection.activeGameObject.GetComponentInParent<UnitManager>();
        if (unit != null)
        {
            selectedTransporter = unit;
            RefreshEmbarkedPassengersIfNeeded(force: true);
        }
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

    private static TurnStateManager FindTurnStateManagerInScene()
    {
        TurnStateManager[] managers = Object.FindObjectsByType<TurnStateManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (managers == null || managers.Length == 0)
            return null;
        return managers[0];
    }

    private void TryAddDebugOrder(PodeDesembarcarOption option)
    {
        if (option == null || option.passengerUnit == null)
        {
            disembarkMessage = "Opcao invalida para fila.";
            return;
        }

        if (IsPassengerAlreadyQueued(option.transporterSlotIndex, option.transporterSeatIndex))
        {
            disembarkMessage = $"Passageiro ja esta na fila (slot {option.transporterSlotIndex}/{option.transporterSeatIndex}).";
            return;
        }

        Vector3Int targetCell = option.disembarkCell;
        targetCell.z = 0;
        if (IsTargetCellAlreadyQueued(targetCell))
        {
            disembarkMessage = $"Hex {targetCell.x},{targetCell.y} ja reservado por outra ordem.";
            return;
        }

        string passengerName = option.passengerUnit != null ? option.passengerUnit.name : "passageiro";
        debugOrders.Add(new DebugDisembarkOrder
        {
            passenger = option.passengerUnit,
            slotIndex = option.transporterSlotIndex,
            seatIndex = option.transporterSeatIndex,
            targetCell = targetCell,
            label = $"{passengerName} -> ({targetCell.x},{targetCell.y})"
        });
        disembarkMessage = $"Ordem adicionada: {passengerName}.";

        options.Clear();
        invalidOptions.Clear();
        selectedOptionIndex = -1;
        selectedInvalidOptionIndex = -1;
        ClearSelectedLine();
    }

    private void ClearDebugOrders(string message)
    {
        debugOrders.Clear();
        disembarkMessage = message;
    }

    private void BuildAutoOrdersFromOptions()
    {
        ClearDebugOrders("Fila reconstruida automaticamente.");
        for (int i = 0; i < options.Count; i++)
        {
            PodeDesembarcarOption option = options[i];
            if (option == null || option.passengerUnit == null)
                continue;
            if (IsPassengerAlreadyQueued(option.transporterSlotIndex, option.transporterSeatIndex))
                continue;

            Vector3Int target = option.disembarkCell;
            target.z = 0;
            if (IsTargetCellAlreadyQueued(target))
                continue;

            TryAddDebugOrder(option);
        }
    }

    private bool IsPassengerAlreadyQueued(int slotIndex, int seatIndex)
    {
        for (int i = 0; i < debugOrders.Count; i++)
        {
            DebugDisembarkOrder order = debugOrders[i];
            if (order == null)
                continue;
            if (order.slotIndex == slotIndex && order.seatIndex == seatIndex)
                return true;
        }

        return false;
    }

    private bool IsPassengerAlreadyQueued(UnitManager passenger)
    {
        if (passenger == null)
            return false;

        for (int i = 0; i < debugOrders.Count; i++)
        {
            DebugDisembarkOrder order = debugOrders[i];
            if (order == null || order.passenger == null)
                continue;
            if (order.passenger == passenger)
                return true;
        }

        return false;
    }

    private bool IsTargetCellAlreadyQueued(Vector3Int cell)
    {
        cell.z = 0;
        for (int i = 0; i < debugOrders.Count; i++)
        {
            DebugDisembarkOrder order = debugOrders[i];
            if (order == null)
                continue;

            Vector3Int queued = order.targetCell;
            queued.z = 0;
            if (queued == cell)
                return true;
        }

        return false;
    }

    private bool IsCellReservedByOtherPassenger(Vector3Int cell, UnitManager passenger)
    {
        cell.z = 0;
        for (int i = 0; i < debugOrders.Count; i++)
        {
            DebugDisembarkOrder order = debugOrders[i];
            if (order == null || order.passenger == null)
                continue;
            if (order.passenger == passenger)
                continue;

            Vector3Int reserved = order.targetCell;
            reserved.z = 0;
            if (reserved == cell)
                return true;
        }

        return false;
    }

    private void ExecuteDebugDisembarkSkip()
    {
        if (selectedTransporter == null || debugOrders.Count == 0)
        {
            disembarkMessage = "Nada para desembarcar.";
            return;
        }

        int successCount = 0;
        for (int i = 0; i < debugOrders.Count; i++)
        {
            DebugDisembarkOrder order = debugOrders[i];
            if (order == null)
                continue;

            if (!selectedTransporter.TryDisembarkPassengerFromSeat(order.slotIndex, order.seatIndex, out UnitManager passenger, out string reason))
            {
                Debug.LogWarning($"[PodeDesembarcarSensorDebug][EXEC] Falha slot={order.slotIndex}/{order.seatIndex}: {reason}");
                continue;
            }

            if (passenger == null)
                continue;

            Vector3Int target = order.targetCell;
            target.z = 0;
            passenger.SetCurrentCellPosition(target, enforceFinalOccupancyRule: true);
            passenger.MarkAsActed();
            successCount++;
            Debug.Log($"[PodeDesembarcarSensorDebug][EXEC] {passenger.name} -> ({target.x},{target.y})");
        }

        if (successCount > 0)
            selectedTransporter.MarkAsActed();

        if (Application.isPlaying &&
            selectedTurnStateManager != null &&
            selectedTurnStateManager.SelectedUnit == selectedTransporter)
        {
            selectedTurnStateManager.TryFinalizeSelectedUnitActionFromDebug();
        }

        disembarkMessage = successCount > 0
            ? $"Desembarque debug concluido. {successCount} unidade(s) desembarcada(s)."
            : "Nenhuma unidade foi desembarcada.";
        debugOrders.Clear();
        RunSimulation();
    }

    private void DrawPassengerSelector()
    {
        EditorGUILayout.LabelField("Passageiros Embarcados", EditorStyles.boldLabel);
        if (selectedTransporter == null)
        {
            EditorGUILayout.HelpBox("Selecione um transportador para listar passageiros.", MessageType.Info);
            return;
        }

        if (embarkedPassengers.Count == 0)
        {
            EditorGUILayout.HelpBox("Transportador sem passageiros embarcados.", MessageType.Info);
            return;
        }

        List<int> selectableIndices = new List<int>();
        List<string> selectableLabels = new List<string>();
        for (int i = 0; i < embarkedPassengers.Count; i++)
        {
            UnitManager passenger = embarkedPassengers[i];
            if (passenger == null || IsPassengerAlreadyQueued(passenger))
                continue;

            selectableIndices.Add(i);
            selectableLabels.Add(embarkedPassengerLabels[i]);
        }

        if (selectableIndices.Count == 0)
        {
            selectedPassenger = null;
            selectedPassengerIndex = -1;
            EditorGUILayout.HelpBox("Todos os passageiros embarcados ja estao na fila de ordens.", MessageType.Info);
            return;
        }

        int currentPopupIndex = 0;
        if (selectedPassenger != null)
        {
            int selectedEmbarkedIndex = embarkedPassengers.IndexOf(selectedPassenger);
            int mapped = selectableIndices.IndexOf(selectedEmbarkedIndex);
            if (mapped >= 0)
                currentPopupIndex = mapped;
        }

        int newPopupIndex = EditorGUILayout.Popup("Passageiro p/ Simular", currentPopupIndex, selectableLabels.ToArray());
        if (newPopupIndex < 0 || newPopupIndex >= selectableIndices.Count)
            newPopupIndex = 0;

        int newEmbarkedIndex = selectableIndices[newPopupIndex];
        if (newEmbarkedIndex != selectedPassengerIndex)
        {
            selectedPassengerIndex = newEmbarkedIndex;
            selectedPassenger = embarkedPassengers[newEmbarkedIndex];
        }

        string selectedLabel = selectedPassenger != null ? selectedPassenger.name : "(nenhum)";
        EditorGUILayout.LabelField("Selecionado", selectedLabel);
    }

    private void RefreshEmbarkedPassengersIfNeeded(bool force = false)
    {
        if (!force && cachedPassengerTransporter == selectedTransporter)
            return;

        UnitManager previousSelectedPassenger = selectedPassenger;
        bool transporterChanged = cachedPassengerTransporter != selectedTransporter;
        if (transporterChanged && debugOrders.Count > 0)
            ClearDebugOrders("Transportador alterado. Fila limpa.");

        cachedPassengerTransporter = selectedTransporter;
        embarkedPassengers.Clear();
        embarkedPassengerLabels.Clear();
        selectedPassenger = null;
        selectedPassengerIndex = -1;

        if (selectedTransporter == null)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = selectedTransporter.TransportedUnitSlots;
        if (seats == null)
            return;

        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat == null || seat.embarkedUnit == null || !seat.embarkedUnit.IsEmbarked)
                continue;

            embarkedPassengers.Add(seat.embarkedUnit);
            embarkedPassengerLabels.Add(seat.embarkedUnit.name);
        }

        if (embarkedPassengers.Count > 0)
        {
            int preservedIndex = previousSelectedPassenger != null
                ? embarkedPassengers.IndexOf(previousSelectedPassenger)
                : -1;
            if (preservedIndex < 0)
                preservedIndex = 0;

            selectedPassengerIndex = preservedIndex;
            selectedPassenger = embarkedPassengers[preservedIndex];
        }
    }

    private bool TryResolveSelectedPassengerForSimulation(out UnitManager passenger)
    {
        passenger = null;
        RefreshEmbarkedPassengersIfNeeded();
        if (embarkedPassengers.Count <= 0)
        {
            statusMessage = "Transportador sem passageiros embarcados.";
            return false;
        }

        if (selectedPassenger == null || !embarkedPassengers.Contains(selectedPassenger))
        {
            if (embarkedPassengers.Count == 1)
            {
                selectedPassenger = embarkedPassengers[0];
                selectedPassengerIndex = 0;
            }
            else
            {
                statusMessage = "Escolha o passageiro na lista para rodar a simulacao.";
                return false;
            }
        }

        if (IsPassengerAlreadyQueued(selectedPassenger))
        {
            statusMessage = "Passageiro selecionado ja tem ordem. Escolha outro.";
            return false;
        }

        passenger = selectedPassenger;
        return passenger != null;
    }
}
