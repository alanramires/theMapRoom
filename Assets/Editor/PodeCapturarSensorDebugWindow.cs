using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeCapturarSensorDebugWindow : EditorWindow
{
    private enum EvaluationMode
    {
        RuntimeStrict = 0,
        SceneManual = 1
    }

    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private EvaluationMode evaluationMode = EvaluationMode.SceneManual;
    [SerializeField] private SensorMovementMode movementMode = SensorMovementMode.MoveuParado;

    private ConstructionManager targetConstruction;
    private bool canCapture;
    private PodeCapturarSensor.CaptureOperationType operationType = PodeCapturarSensor.CaptureOperationType.None;
    private string sensorReason = "Ready.";
    private string statusMessage = "Ready.";
    private bool runtimeCanCapture;
    private ConstructionManager runtimeTargetConstruction;
    private PodeCapturarSensor.CaptureOperationType runtimeOperationType = PodeCapturarSensor.CaptureOperationType.None;
    private string runtimeReason = string.Empty;
    private bool sceneCanCapture;
    private ConstructionManager sceneTargetConstruction;
    private PodeCapturarSensor.CaptureOperationType sceneOperationType = PodeCapturarSensor.CaptureOperationType.None;
    private string sceneReason = string.Empty;

    private bool hasSelectedMarker;
    private Vector3Int selectedMarkerCell;
    private Color selectedMarkerColor = Color.yellow;
    private string selectedMarkerLabel = string.Empty;
    private Vector2 windowScroll;

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
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
        EditorGUILayout.LabelField("Sensor Pode Capturar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Regras:\n" +
            "1) Training > Skills exige uma skill com Can Capture Constructions ativo\n" +
            "2) Runtime: exige estado real Moveu Parado/Andando e acao C disponivel\n" +
            "3) Scene Manual: usa o sensor puro no modo escolhido, sem fingir estado do TurnState\n" +
            "4) Unidade em construcao inimiga/neutra captura; aliada danificada recupera",
            MessageType.Info);

        selectedUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade", selectedUnit, typeof(UnitManager), true);
        turnStateManager = (TurnStateManager)EditorGUILayout.ObjectField("TurnStateManager", turnStateManager, typeof(TurnStateManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", overrideTilemap, typeof(Tilemap), true);
        evaluationMode = (EvaluationMode)EditorGUILayout.EnumPopup("Avaliacao", evaluationMode);
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
        EditorGUILayout.EndScrollView();
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
        EditorGUILayout.LabelField("Avaliacao Ativa", evaluationMode.ToString());
        EditorGUILayout.LabelField("Modo Manual", movementMode.ToString());
        EditorGUILayout.LabelField("Pode Capturar", canCapture ? "SIM" : "NAO");
        EditorGUILayout.LabelField("Operacao", operationType.ToString());

        DrawDiagnosticBlock(
            "Runtime",
            runtimeCanCapture,
            runtimeOperationType,
            runtimeReason,
            runtimeTargetConstruction);
        DrawDiagnosticBlock(
            "Scene Manual",
            sceneCanCapture,
            sceneOperationType,
            sceneReason,
            sceneTargetConstruction);
    }

    private void DrawDiagnosticBlock(
        string label,
        bool canRun,
        PodeCapturarSensor.CaptureOperationType opType,
        string reason,
        ConstructionManager construction)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Pode Capturar", canRun ? "SIM" : "NAO");
        EditorGUILayout.LabelField("Operacao", opType.ToString());
        if (!string.IsNullOrWhiteSpace(reason))
            EditorGUILayout.LabelField("Motivo", reason);

        if (construction != null)
        {
            string cName = !string.IsNullOrWhiteSpace(construction.ConstructionDisplayName)
                ? construction.ConstructionDisplayName
                : construction.name;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Construcao Alvo", cName);
            EditorGUILayout.LabelField("Team", $"{TeamUtils.GetName(construction.TeamId)} ({(int)construction.TeamId})");
            EditorGUILayout.LabelField("Capture", $"{construction.CurrentCapturePoints}/{construction.CapturePointsMax}");
            EditorGUILayout.LabelField("Dano de Captura", Mathf.Max(0, selectedUnit.CurrentHP).ToString());
        }
    }

    private void RunSimulation()
    {
        ClearSimulationResult();
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

        EvaluateRuntime(map);
        EvaluateSceneManual(map);

        if (evaluationMode == EvaluationMode.RuntimeStrict)
        {
            canCapture = runtimeCanCapture;
            targetConstruction = runtimeTargetConstruction;
            operationType = runtimeOperationType;
            sensorReason = runtimeReason;
        }
        else
        {
            canCapture = sceneCanCapture;
            targetConstruction = sceneTargetConstruction;
            operationType = sceneOperationType;
            sensorReason = sceneReason;
        }

        if (canCapture && targetConstruction != null)
        {
            Vector3Int cell = targetConstruction.CurrentCellPosition;
            cell.z = 0;
            SetSelectedMarker(cell, Color.yellow, evaluationMode == EvaluationMode.RuntimeStrict ? "Captura valida (Runtime)" : "Captura valida (Scene)");
            statusMessage = BuildSuccessStatusMessage();
        }
        else
        {
            statusMessage = evaluationMode == EvaluationMode.RuntimeStrict
                ? "Runtime FALSE. Captura indisponivel no estado atual."
                : "Scene Manual FALSE. Captura indisponivel.";
        }

        Debug.Log(
            $"[PodeCapturarSensorDebug] unit={(selectedUnit != null ? selectedUnit.name : "(null)")} | " +
            $"runtime={runtimeCanCapture}/{runtimeOperationType} reason={runtimeReason} | " +
            $"scene={sceneCanCapture}/{sceneOperationType} reason={sceneReason}");
    }

    private void EvaluateRuntime(Tilemap map)
    {
        runtimeTargetConstruction = null;
        runtimeCanCapture = false;
        runtimeOperationType = PodeCapturarSensor.CaptureOperationType.None;
        runtimeReason = string.Empty;

        if (turnStateManager == null)
        {
            runtimeReason = "TurnStateManager nao encontrado.";
            return;
        }

        if (turnStateManager.SelectedUnit != selectedUnit)
        {
            runtimeReason = "A unidade precisa ser a SelectedUnit real do TurnStateManager.";
            return;
        }

        TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
        if (state != TurnStateManager.CursorState.MoveuAndando && state != TurnStateManager.CursorState.MoveuParado)
        {
            runtimeReason = $"Estado atual invalido para captura: {state}.";
            return;
        }

        if (!turnStateManager.AvailableSensorActionCodes.Contains('C'))
        {
            runtimeTargetConstruction = turnStateManager.CachedPodeCapturarConstruction;
            runtimeReason = string.IsNullOrWhiteSpace(turnStateManager.CachedPodeCapturarReason)
                ? "Acao C nao esta disponivel."
                : turnStateManager.CachedPodeCapturarReason;
            return;
        }

        SensorMovementMode runtimeMovementMode = state == TurnStateManager.CursorState.MoveuAndando
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;
        runtimeCanCapture = PodeCapturarSensor.TryGetCaptureTarget(
            selectedUnit,
            map,
            runtimeMovementMode,
            out runtimeTargetConstruction,
            out runtimeOperationType,
            out runtimeReason);
    }

    private void EvaluateSceneManual(Tilemap map)
    {
        sceneTargetConstruction = null;
        sceneCanCapture = PodeCapturarSensor.TryGetCaptureTarget(
            selectedUnit,
            map,
            movementMode,
            out sceneTargetConstruction,
            out sceneOperationType,
            out sceneReason);
    }

    private string BuildSuccessStatusMessage()
    {
        if (evaluationMode == EvaluationMode.RuntimeStrict)
        {
            return operationType == PodeCapturarSensor.CaptureOperationType.RecoverAlly
                ? "Runtime TRUE. Recuperacao de base aliada disponivel."
                : "Runtime TRUE. Captura disponivel.";
        }

        return operationType == PodeCapturarSensor.CaptureOperationType.RecoverAlly
            ? "Scene Manual TRUE. Recuperacao de base aliada disponivel."
            : "Scene Manual TRUE. Captura disponivel.";
    }

    private void ExecuteDebugCapture()
    {
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

        if (!TryRevalidateSelectedEvaluation(map, out ConstructionManager validatedConstruction, out PodeCapturarSensor.CaptureOperationType validatedOperation, out string validatedReason))
        {
            statusMessage = string.IsNullOrWhiteSpace(validatedReason)
                ? "Nao ha captura valida para executar."
                : validatedReason;
            RunSimulation();
            return;
        }

        Undo.RecordObject(selectedUnit, "Pode Capturar (Debug)");
        Undo.RecordObject(validatedConstruction, "Pode Capturar (Debug)");

        int captureDamage = Mathf.Max(0, selectedUnit.CurrentHP);
        int before = Mathf.Max(0, validatedConstruction.CurrentCapturePoints);
        int safeMax = Mathf.Max(0, validatedConstruction.CapturePointsMax);
        int after = before;
        bool concluded = false;
        if (validatedOperation == PodeCapturarSensor.CaptureOperationType.RecoverAlly)
        {
            after = Mathf.Min(safeMax, before + captureDamage);
            concluded = after >= safeMax;
        }
        else
        {
            after = Mathf.Max(0, before - captureDamage);
            concluded = after <= 0;
        }

        validatedConstruction.SetCurrentCapturePoints(after);

        if (validatedOperation == PodeCapturarSensor.CaptureOperationType.CaptureEnemy && concluded)
        {
            validatedConstruction.SetTeamId(selectedUnit.TeamId);
            validatedConstruction.SetCurrentCapturePoints(validatedConstruction.CapturePointsMax);
        }

        selectedUnit.MarkAsActed();

        EditorUtility.SetDirty(selectedUnit);
        EditorUtility.SetDirty(validatedConstruction);
        if (selectedUnit.gameObject != null && selectedUnit.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(selectedUnit.gameObject.scene);

        string cName = !string.IsNullOrWhiteSpace(validatedConstruction.ConstructionDisplayName)
            ? validatedConstruction.ConstructionDisplayName
            : validatedConstruction.name;
        if (validatedOperation == PodeCapturarSensor.CaptureOperationType.RecoverAlly)
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

    private bool TryRevalidateSelectedEvaluation(
        Tilemap map,
        out ConstructionManager validatedConstruction,
        out PodeCapturarSensor.CaptureOperationType validatedOperation,
        out string validatedReason)
    {
        validatedConstruction = null;
        validatedOperation = PodeCapturarSensor.CaptureOperationType.None;
        validatedReason = string.Empty;

        if (evaluationMode == EvaluationMode.RuntimeStrict)
        {
            if (turnStateManager == null)
            {
                validatedReason = "TurnStateManager nao encontrado.";
                return false;
            }

            if (turnStateManager.SelectedUnit != selectedUnit)
            {
                validatedReason = "A unidade nao e a SelectedUnit atual do runtime.";
                return false;
            }

            TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
            if (state != TurnStateManager.CursorState.MoveuAndando && state != TurnStateManager.CursorState.MoveuParado)
            {
                validatedReason = $"Estado atual invalido para captura: {state}.";
                return false;
            }

            if (!turnStateManager.AvailableSensorActionCodes.Contains('C'))
            {
                validatedReason = string.IsNullOrWhiteSpace(turnStateManager.CachedPodeCapturarReason)
                    ? "Acao C nao esta disponivel."
                    : turnStateManager.CachedPodeCapturarReason;
                return false;
            }

            SensorMovementMode runtimeMovementMode = state == TurnStateManager.CursorState.MoveuAndando
                ? SensorMovementMode.MoveuAndando
                : SensorMovementMode.MoveuParado;
            return PodeCapturarSensor.TryGetCaptureTarget(
                selectedUnit,
                map,
                runtimeMovementMode,
                out validatedConstruction,
                out validatedOperation,
                out validatedReason);
        }

        return PodeCapturarSensor.TryGetCaptureTarget(
            selectedUnit,
            map,
            movementMode,
            out validatedConstruction,
            out validatedOperation,
            out validatedReason);
    }

    // A janela nao elege mais "a skill de captura".
    //
    // Ela mantinha um campo `requiredCaptureSkill` e o preenchia varrendo os
    // assets atras do primeiro com `canCaptureConstructions` — ordem de GUID.
    // Com duas skills de captura no projeto, o jogo aceitava as duas e a janela
    // exigia uma; ela reprovava o que o jogo aprovava.
    //
    // Agora a pergunta e a mesma do jogo: quem diz quem captura e a construcao,
    // em requiredSkillsToCapture, e o PodeCapturar responde por ela.

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

        if (overrideTilemap == null)
            overrideTilemap = ResolveTilemap();

        if (turnStateManager != null)
        {
            TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
            if (state == TurnStateManager.CursorState.MoveuAndando)
                movementMode = SensorMovementMode.MoveuAndando;
            else if (state == TurnStateManager.CursorState.MoveuParado)
                movementMode = SensorMovementMode.MoveuParado;
        }

        statusMessage = "Contexto detectado.";
    }

    private void ClearSimulationResult()
    {
        targetConstruction = null;
        canCapture = false;
        operationType = PodeCapturarSensor.CaptureOperationType.None;
        sensorReason = string.Empty;
        runtimeCanCapture = false;
        runtimeTargetConstruction = null;
        runtimeOperationType = PodeCapturarSensor.CaptureOperationType.None;
        runtimeReason = string.Empty;
        sceneCanCapture = false;
        sceneTargetConstruction = null;
        sceneOperationType = PodeCapturarSensor.CaptureOperationType.None;
        sceneReason = string.Empty;
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
