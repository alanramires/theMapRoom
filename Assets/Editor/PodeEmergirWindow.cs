using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Consulta pura de "pode emergir?" (Submarine/Submerged -> Naval/Surface).
// Usa o PodeEmergirSensor, exatamente como o comando EMERGE do runtime
// (TurnStateManager.TryValidateDebugLayerCommand). Nada e reimplementado aqui.
public sealed class PodeEmergirWindow : EditorWindow
{
    [SerializeField] private UnitManager submarine;
    [SerializeField] private Tilemap map;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private bool hasDestination;
    [SerializeField] private Vector3Int destination;

    private bool pickingDestination;
    private Vector3Int hoverCell;
    private PodeEmergirReport report;

    [MenuItem("Tools/Operações Navais/Pode Emergir")]
    public static void Open()
    {
        GetWindow<PodeEmergirWindow>("Pode Emergir").Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetect();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSelectionChange()
    {
        TryUseCurrentSelection();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Pode Emergir", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura: simula a emersão no hex escolhido e restaura " +
            "imediatamente a posição da unidade. Nenhuma ação é confirmada.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        submarine = (UnitManager)EditorGUILayout.ObjectField(
            "Submarino", submarine, typeof(UnitManager), true);
        map = (Tilemap)EditorGUILayout.ObjectField(
            "Tilemap", map, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        if (EditorGUI.EndChangeCheck())
            ClearResult();

        NavalOpsWindowGui.DrawUnitPickerRow(TryUseCurrentSelection, ref pickingDestination);

        NavalOpsWindowGui.DrawEvaluatedCell(submarine, hasDestination, destination);

        using (new EditorGUI.DisabledScope(!hasDestination))
        {
            if (GUILayout.Button("Usar o Próprio Hex da Unidade"))
            {
                hasDestination = false;
                pickingDestination = false;
                ClearResult();
                SceneView.RepaintAll();
            }
        }

        NavalOpsWindowGui.DrawCurrentLayer(submarine);

        using (new EditorGUI.DisabledScope(
                   submarine == null || map == null || terrainDatabase == null))
        {
            if (GUILayout.Button("Verificar Emersão", GUILayout.Height(28f)))
                Evaluate();
        }

        EditorGUILayout.Space(6f);
        if (report == null)
            return;

        EditorGUILayout.HelpBox(
            report.status
                ? $"PODE EMERGIR\n{report.explicacao}"
                : $"NÃO PODE EMERGIR\n{report.explicacao}",
            report.status ? MessageType.Info : MessageType.Warning);

        if (report.status)
            EditorGUILayout.LabelField("Camada após emergir", "Naval / Surface");
    }

    private void Evaluate()
    {
        AutoDetect();
        if (submarine == null || map == null || terrainDatabase == null)
            return;

        Vector3Int testCell =
            hasDestination ? destination : submarine.CurrentCellPosition;
        testCell.z = 0;

        // Consulta por hex: o sensor recebe a celula, ninguem e deslocado.
        report = PodeEmergirSensor.Evaluate(submarine, map, terrainDatabase, testCell);

        Repaint();
        SceneView.RepaintAll();
    }

    private void TryUseCurrentSelection()
    {
        UnitManager unit = NavalOpsWindowGui.ResolveSelectedUnit();
        if (unit == null)
            return;

        submarine = unit;
        AutoDetect();
        ClearResult();
    }

    private void AutoDetect()
    {
        NavalOpsWindowGui.AutoDetectContext(
            submarine, ref map, ref terrainDatabase);
    }

    private void ClearResult()
    {
        report = null;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!NavalOpsWindowGui.TryPickCellInScene(
                map, ref pickingDestination, ref hoverCell, "Emersão", out Vector3Int picked))
            return;

        destination = picked;
        hasDestination = true;
        ClearResult();
        Repaint();
    }
}
