using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeCapturarSensorDebugWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private SensorMovementMode movementMode = SensorMovementMode.MoveuParado;

    private ConstructionManager targetConstruction;
    private bool canCapture;
    private PodeCapturarSensor.CaptureOperationType operationType = PodeCapturarSensor.CaptureOperationType.None;
    private string sensorReason = "Ready.";
    private string statusMessage = "Ready.";

    private bool hasSelectedMarker;
    private Vector3Int selectedMarkerCell;
    private Color selectedMarkerColor = Color.yellow;
    private string selectedMarkerLabel = string.Empty;

    [MenuItem("Tools/Sensors/Pode Capturar")]
    public static void OpenWindow()
    {
        GetWindow<PodeCapturarSensorDebugWindow>("Pode Capturar");
    }

    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearSelectedMarker();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sensor Pode Capturar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Regras:\n" +
            "1) Apenas infantaria\n" +
            "2) Moveu Parado/Andando\n" +
            "3) Unidade em construcao inimiga/neutra captura\n" +
            "4) Unidade em construcao aliada danificada recupera",
            MessageType.Info);

        selectedUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade", selectedUnit, typeof(UnitManager), true);
        turnStateManager = (TurnStateManager)EditorGUILayout.ObjectField("TurnStateManager", turnStateManager, typeof(TurnStateManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        movementMode = (SensorMovementMode)EditorGUILayout.EnumPopup("Modo", movementMode);

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
        if (!string.IsNullOrWhiteSpace(sensorReason))
            EditorGUILayout.HelpBox($"Sensor: {sensorReason}", canCapture ? MessageType.Info : MessageType.Warning);

        DrawSimulationResult();

        using (new EditorGUI.DisabledScope(!canCapture || targetConstruction == null))
        {
            if (GUILayout.Button("Capturar (Debug)"))
                ExecuteDebugCapture();
        }
    }

    private void DrawSimulationResult()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Resultado", EditorStyles.boldLabel);

        if (selectedUnit == null)
        {
            EditorGUILayout.HelpBox("Selecione uma unidade para simular.", MessageType.Info);
            return;
        }

        string unitName = selectedUnit != null ? selectedUnit.name : "(null)";
        EditorGUILayout.LabelField("Unidade", unitName);
        EditorGUILayout.LabelField("HP Atual", selectedUnit.CurrentHP.ToString());
        EditorGUILayout.LabelField("Team", $"{TeamUtils.GetName(selectedUnit.TeamId)} ({(int)selectedUnit.TeamId})");
        EditorGUILayout.LabelField("Modo", movementMode.ToString());
        EditorGUILayout.LabelField("Pode Capturar", canCapture ? "SIM" : "NAO");
        EditorGUILayout.LabelField("Operacao", operationType.ToString());

        if (targetConstruction != null)
        {
            string cName = !string.IsNullOrWhiteSpace(targetConstruction.ConstructionDisplayName)
                ? targetConstruction.ConstructionDisplayName
                : targetConstruction.name;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Construcao Alvo", cName);
            EditorGUILayout.LabelField("Team", $"{TeamUtils.GetName(targetConstruction.TeamId)} ({(int)targetConstruction.TeamId})");
            EditorGUILayout.LabelField("Capture", $"{targetConstruction.CurrentCapturePoints}/{targetConstruction.CapturePointsMax}");
            EditorGUILayout.LabelField("Dano de Captura", Mathf.Max(0, selectedUnit.CurrentHP).ToString());
        }
    }

    private void RunSimulation()
    {
        targetConstruction = null;
        canCapture = false;
        operationType = PodeCapturarSensor.CaptureOperationType.None;
        sensorReason = string.Empty;
        ClearSelectedMarker();

        if (selectedUnit == null)
        {
            statusMessage = "Selecione uma unidade valida.";
            return;
        }

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            statusMessage = "Tilemap base nao encontrado.";
            return;
        }

        canCapture = PodeCapturarSensor.TryGetCaptureTarget(
            selectedUnit,
            map,
            movementMode,
            out targetConstruction,
            out operationType,
            out sensorReason);

        if (canCapture && targetConstruction != null)
        {
            Vector3Int cell = targetConstruction.CurrentCellPosition;
            cell.z = 0;
            SetSelectedMarker(cell, Color.yellow, "Captura valida");
            statusMessage = operationType == PodeCapturarSensor.CaptureOperationType.RecoverAlly
                ? "Sensor TRUE. Recuperacao de base aliada disponivel."
                : "Sensor TRUE. Captura disponivel.";
        }
        else
        {
            statusMessage = "Sensor FALSE. Captura indisponivel.";
        }

        Debug.Log(
            $"[PodeCapturarSensorDebug] unit={(selectedUnit != null ? selectedUnit.name : "(null)")} | " +
            $"canCapture={canCapture} | op={operationType} | reason={sensorReason}");
    }

    private void ExecuteDebugCapture()
    {
        if (!canCapture || selectedUnit == null || targetConstruction == null)
        {
            statusMessage = "Nao ha captura valida para executar.";
            return;
        }

        Undo.RecordObject(selectedUnit, "Pode Capturar (Debug)");
        Undo.RecordObject(targetConstruction, "Pode Capturar (Debug)");

        int captureDamage = Mathf.Max(0, selectedUnit.CurrentHP);
        int before = Mathf.Max(0, targetConstruction.CurrentCapturePoints);
        int safeMax = Mathf.Max(0, targetConstruction.CapturePointsMax);
        int after = before;
        bool concluded = false;
        if (operationType == PodeCapturarSensor.CaptureOperationType.RecoverAlly)
        {
            after = Mathf.Min(safeMax, before + captureDamage);
            concluded = after >= safeMax;
        }
        else
        {
            after = Mathf.Max(0, before - captureDamage);
            concluded = after <= 0;
        }

        targetConstruction.SetCurrentCapturePoints(after);

        if (operationType == PodeCapturarSensor.CaptureOperationType.CaptureEnemy && concluded)
        {
            targetConstruction.SetTeamId(selectedUnit.TeamId);
            targetConstruction.SetCurrentCapturePoints(targetConstruction.CapturePointsMax);
        }

        selectedUnit.MarkAsActed();

        EditorUtility.SetDirty(selectedUnit);
        EditorUtility.SetDirty(targetConstruction);
        if (selectedUnit.gameObject != null && selectedUnit.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(selectedUnit.gameObject.scene);

        string cName = !string.IsNullOrWhiteSpace(targetConstruction.ConstructionDisplayName)
            ? targetConstruction.ConstructionDisplayName
            : targetConstruction.name;
        if (operationType == PodeCapturarSensor.CaptureOperationType.RecoverAlly)
        {
            statusMessage = concluded
                ? $"Recuperacao concluida: {cName} voltou ao maximo ({after}/{safeMax})."
                : $"Recuperacao aplicada: {cName} {before}->{after}.";
        }
        else
        {
            statusMessage = concluded
                ? $"Captura concluida: {cName} agora e do time {TeamUtils.GetName(selectedUnit.TeamId)}."
                : $"Captura aplicada: {cName} {before}->{after}.";
        }

        RunSimulation();
    }

    private void TryUseCurrentSelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            statusMessage = "Selecione uma unidade na hierarquia.";
            return;
        }

        UnitManager unit = go.GetComponent<UnitManager>();
        if (unit == null)
        {
            statusMessage = "GameObject selecionado nao possui UnitManager.";
            return;
        }

        selectedUnit = unit;
        statusMessage = $"Unidade selecionada: {unit.name}.";
        Repaint();
    }

    private void AutoDetectContext()
    {
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (selectedUnit == null && turnStateManager != null)
            selectedUnit = turnStateManager.SelectedUnit;

        if (turnStateManager != null)
        {
            TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
            if (state == TurnStateManager.CursorState.MoveuAndando)
                movementMode = SensorMovementMode.MoveuAndando;
            else
                movementMode = SensorMovementMode.MoveuParado;
        }

        statusMessage = "Contexto detectado.";
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedUnit != null && selectedUnit.BoardTilemap != null)
            return selectedUnit.BoardTilemap;

        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
            return cursor.BoardTilemap;

        return null;
    }

    private void SetSelectedMarker(Vector3Int cell, Color color, string label)
    {
        selectedMarkerCell = cell;
        selectedMarkerCell.z = 0;
        selectedMarkerColor = color;
        selectedMarkerLabel = label;
        hasSelectedMarker = true;
        SceneView.RepaintAll();
    }

    private void ClearSelectedMarker()
    {
        hasSelectedMarker = false;
        selectedMarkerLabel = string.Empty;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!hasSelectedMarker)
            return;

        Tilemap map = ResolveTilemap();
        if (map == null)
            return;

        Vector3 center = map.GetCellCenterWorld(selectedMarkerCell);
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = selectedMarkerColor;
        float radius = HandleUtility.GetHandleSize(center) * 0.2f;
        Handles.DrawWireDisc(center, Vector3.forward, radius);
        Handles.Label(center + new Vector3(0.1f, 0.1f, 0f), selectedMarkerLabel);
    }
}
