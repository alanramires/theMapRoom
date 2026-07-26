using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PodeSubmergirRapidamenteWindow : EditorWindow
{
    [SerializeField] private UnitManager submarine;
    [SerializeField] private Tilemap map;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private RapidSubmergeOperation operation =
        RapidSubmergeOperation.FutureExplicitOperation;
    [SerializeField] private bool wasSubmergedBeforeOperation = true;
    [SerializeField] private bool surfacedForOperation = true;
    [SerializeField] private bool operationExplicitlyAllowsRapidSubmerge;

    private PodeSubmergirRapidamenteReport report;

    [MenuItem("Tools/Operações Navais/Pode Submergir Rapidamente")]
    public static void Open()
    {
        GetWindow<PodeSubmergirRapidamenteWindow>(
            "Pode Submergir Rapidamente").Show();
    }

    private void OnEnable()
    {
        AutoDetect();
    }

    private void OnSelectionChange()
    {
        TryUseCurrentSelection();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Pode Submergir Rapidamente", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura do retorno à submersão após uma operação no mesmo hex.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        submarine = (UnitManager)EditorGUILayout.ObjectField(
            "Submarino", submarine, typeof(UnitManager), true);
        map = (Tilemap)EditorGUILayout.ObjectField(
            "Tilemap", map, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        operation = (RapidSubmergeOperation)EditorGUILayout.EnumPopup(
            "Operação", operation);
        wasSubmergedBeforeOperation = EditorGUILayout.Toggle(
            "Estava submerso antes", wasSubmergedBeforeOperation);
        surfacedForOperation = EditorGUILayout.Toggle(
            "Emergiu para a operação", surfacedForOperation);
        operationExplicitlyAllowsRapidSubmerge = EditorGUILayout.Toggle(
            "Operação autoriza", operationExplicitlyAllowsRapidSubmerge);
        if (EditorGUI.EndChangeCheck())
            report = null;

        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();

        using (new EditorGUI.DisabledScope(
                   submarine == null || map == null || terrainDatabase == null))
        {
            if (GUILayout.Button(
                    "Verificar Submersão Rápida", GUILayout.Height(28f)))
            {
                report = PodeSubmergirRapidamenteSensor.Evaluate(
                    submarine,
                    map,
                    terrainDatabase,
                    operation,
                    wasSubmergedBeforeOperation,
                    surfacedForOperation,
                    operationExplicitlyAllowsRapidSubmerge);
            }
        }

        if (report != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                report.status
                    ? $"PODE SUBMERGIR RAPIDAMENTE\n{report.explicacao}"
                    : $"NÃO PODE SUBMERGIR RAPIDAMENTE\n{report.explicacao}",
                report.status ? MessageType.Info : MessageType.Warning);
        }
    }

    private void TryUseCurrentSelection()
    {
        UnitManager unit = NavalOpsWindowGui.ResolveSelectedUnit();
        if (unit == null)
            return;

        submarine = unit;
        AutoDetect();
        report = null;
    }

    private void AutoDetect()
    {
        NavalOpsWindowGui.AutoDetectContext(
            submarine, ref map, ref terrainDatabase);
    }
}
