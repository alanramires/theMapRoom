using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PodeArremeterWindow : EditorWindow
{
    [SerializeField] private UnitManager aircraft;
    [SerializeField] private Tilemap map;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private AirGoAroundOperation operation = AirGoAroundOperation.Supply;
    [SerializeField] private bool wasAirborneBeforeOperation = true;
    [SerializeField] private bool landedForOperation = true;
    [SerializeField] private int fuelBeforeOperation = 1;
    [SerializeField] private bool operationExplicitlyAllowsGoAround = true;

    private PodeArremeterReport report;

    [MenuItem("Tools/Operações Aéreas/Pode Arremeter")]
    public static void Open()
    {
        GetWindow<PodeArremeterWindow>("Pode Arremeter").Show();
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
        EditorGUILayout.LabelField("Pode Arremeter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura do retorno ao voo após uma operação no mesmo hex. " +
            "O snapshot representa o estado anterior ao pouso e ao serviço.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        aircraft = (UnitManager)EditorGUILayout.ObjectField(
            "Aeronave", aircraft, typeof(UnitManager), true);
        map = (Tilemap)EditorGUILayout.ObjectField(
            "Tilemap", map, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        operation = (AirGoAroundOperation)EditorGUILayout.EnumPopup(
            "Operação", operation);
        wasAirborneBeforeOperation = EditorGUILayout.Toggle(
            "Estava no ar antes", wasAirborneBeforeOperation);
        landedForOperation = EditorGUILayout.Toggle(
            "Pousou para a operação", landedForOperation);
        fuelBeforeOperation = Mathf.Max(0, EditorGUILayout.IntField(
            "Combustível anterior", fuelBeforeOperation));
        operationExplicitlyAllowsGoAround = EditorGUILayout.Toggle(
            "Operação autoriza", operationExplicitlyAllowsGoAround);
        if (EditorGUI.EndChangeCheck())
            report = null;

        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();

        using (new EditorGUI.DisabledScope(
                   aircraft == null || map == null || terrainDatabase == null))
        {
            if (GUILayout.Button("Verificar Arremetida", GUILayout.Height(28f)))
            {
                report = PodeArremeterSensor.Evaluate(
                    aircraft,
                    map,
                    terrainDatabase,
                    operation,
                    wasAirborneBeforeOperation,
                    landedForOperation,
                    fuelBeforeOperation,
                    operationExplicitlyAllowsGoAround);
            }
        }

        if (report != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                report.status
                    ? $"PODE ARREMETER\n{report.explicacao}"
                    : $"NÃO PODE ARREMETER\n{report.explicacao}",
                report.status ? MessageType.Info : MessageType.Warning);
        }
    }

    private void TryUseCurrentSelection()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return;

        UnitManager unit = selected.GetComponent<UnitManager>();
        if (unit == null)
            unit = selected.GetComponentInParent<UnitManager>();
        if (unit == null)
            return;

        aircraft = unit;
        fuelBeforeOperation = Mathf.Max(0, unit.CurrentFuel);
        AutoDetect();
        report = null;
    }

    private void AutoDetect()
    {
        if (map == null && aircraft != null)
            map = aircraft.BoardTilemap;
        if (terrainDatabase == null)
            terrainDatabase = FindFirstTerrainDatabaseAsset();
    }

    private static TerrainDatabase FindFirstTerrainDatabaseAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            TerrainDatabase database = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (database != null)
                return database;
        }
        return null;
    }
}
