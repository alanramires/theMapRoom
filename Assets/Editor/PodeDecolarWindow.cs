using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeDecolarWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    private PodeDecolarReport latestReport;
    private string statusMessage = "Ready.";

    [MenuItem("Tools/Pode Decolar")]
    public static void OpenWindow()
    {
        GetWindow<PodeDecolarWindow>("Pode Decolar");
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sensor Pode Decolar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Avalia decolagem da unidade selecionada antes da escolha de movimento. " +
            "Unidades embarcadas ficam fora deste sensor.",
            MessageType.Info);

        selectedUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade", selectedUnit, typeof(UnitManager), true);
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

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Local de Decolagem", EditorStyles.boldLabel);
        if (latestReport == null)
        {
            EditorGUILayout.HelpBox("Sem simulacao.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Status", latestReport.status ? "valido" : "invalido");
            EditorGUILayout.LabelField("Explicacao", string.IsNullOrWhiteSpace(latestReport.explicacao) ? "-" : latestReport.explicacao);
        }

        EditorGUILayout.Space(8f);
        bool can0 = latestReport != null && latestReport.status && latestReport.canDecolar0Hex;
        bool can1 = latestReport != null && latestReport.status && latestReport.canDecolar1Hex;
        bool canFull = latestReport != null && latestReport.status && latestReport.canDecolarFullMove;
        using (new EditorGUI.DisabledScope(!can0))
        {
            GUILayout.Button("Decolar");
        }
        using (new EditorGUI.DisabledScope(!can1))
        {
            GUILayout.Button("Decolar 1hex");
        }
        using (new EditorGUI.DisabledScope(!canFull))
        {
            GUILayout.Button("Decolar full move");
        }
    }

    private void RunSimulation()
    {
        Tilemap map = ResolveTilemap();
        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstTerrainDatabaseAsset();

        latestReport = PodeDecolarSensor.Evaluate(
            selectedUnit,
            map,
            db);

        statusMessage = latestReport != null
            ? $"Simulacao concluida. Local de decolagem: {(latestReport.status ? "valido" : "invalido")}."
            : "Falha ao executar simulacao.";
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedUnit != null && selectedUnit.BoardTilemap != null)
            return selectedUnit.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void AutoDetectContext()
    {
        if (selectedUnit == null)
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
            selectedUnit = unit;
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
