using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Consulta pura de "pode submergir?" (Naval/Surface -> Submarine/Submerged).
// A janela apenas monta o contexto e apresenta o relatorio do sensor autoritativo.
public sealed class PodeSubmergirWindow : EditorWindow
{
    [SerializeField] private UnitManager ship;
    [SerializeField] private Tilemap map;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private bool hasDestination;
    [SerializeField] private Vector3Int destination;

    private bool pickingDestination;
    private Vector3Int hoverCell;
    private bool hasResult;
    private bool canSubmerge;
    private string explanation;

    [MenuItem("Tools/Operações Navais/Pode Submergir")]
    public static void Open()
    {
        GetWindow<PodeSubmergirWindow>("Pode Submergir").Show();
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
        EditorGUILayout.LabelField("Pode Submergir", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura: simula a submersão no hex escolhido e restaura " +
            "imediatamente a posição da unidade. Nenhuma ação é confirmada.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        ship = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade", ship, typeof(UnitManager), true);
        map = (Tilemap)EditorGUILayout.ObjectField(
            "Tilemap", map, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        if (EditorGUI.EndChangeCheck())
            ClearResult();

        NavalOpsWindowGui.DrawUnitPickerRow(TryUseCurrentSelection, ref pickingDestination);

        NavalOpsWindowGui.DrawEvaluatedCell(ship, hasDestination, destination);

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

        NavalOpsWindowGui.DrawCurrentLayer(ship);

        using (new EditorGUI.DisabledScope(
                   ship == null || map == null || terrainDatabase == null))
        {
            if (GUILayout.Button("Verificar Submersão", GUILayout.Height(28f)))
                Evaluate();
        }

        EditorGUILayout.Space(6f);
        if (!hasResult)
            return;

        EditorGUILayout.HelpBox(
            canSubmerge
                ? $"PODE SUBMERGIR\n{explanation}"
                : $"NÃO PODE SUBMERGIR\n{explanation}",
            canSubmerge ? MessageType.Info : MessageType.Warning);

        if (canSubmerge)
            EditorGUILayout.LabelField("Camada após submergir", "Submarine / Submerged");
    }

    private void Evaluate()
    {
        AutoDetect();
        if (ship == null || map == null || terrainDatabase == null)
            return;

        Vector3Int testCell = hasDestination ? destination : ship.CurrentCellPosition;
        testCell.z = 0;

        PodeSubmergirReport report = PodeSubmergirSensor.Evaluate(
            ship,
            map,
            terrainDatabase,
            testCell);
        canSubmerge = report != null && report.status;
        explanation = report != null ? report.explicacao : "PodeSubmergir sem resultado.";
        hasResult = true;

        Repaint();
        SceneView.RepaintAll();
    }

    private void TryUseCurrentSelection()
    {
        UnitManager unit = NavalOpsWindowGui.ResolveSelectedUnit();
        if (unit == null)
            return;

        ship = unit;
        AutoDetect();
        ClearResult();
    }

    private void AutoDetect()
    {
        NavalOpsWindowGui.AutoDetectContext(ship, ref map, ref terrainDatabase);
    }

    private void ClearResult()
    {
        hasResult = false;
        canSubmerge = false;
        explanation = string.Empty;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!NavalOpsWindowGui.TryPickCellInScene(
                map, ref pickingDestination, ref hoverCell, "Submersão", out Vector3Int picked))
            return;

        destination = picked;
        hasDestination = true;
        ClearResult();
        Repaint();
    }
}
