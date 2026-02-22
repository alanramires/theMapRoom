using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodePousarWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedAircraft;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private SensorMovementMode movementMode = SensorMovementMode.MoveuParado;
    [SerializeField] private bool useManualRemainingMovement = false;
    [SerializeField] private int manualRemainingMovement = 0;

    private PodePousarReport latestReport;
    private string statusMessage = "Ready.";

    [MenuItem("Tools/Pode Pousar")]
    public static void OpenWindow()
    {
        GetWindow<PodePousarWindow>("Pode Pousar");
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sensor Pode Pousar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Especializacao do sensor de pouso para aeronaves. Usa a mesma avaliacao de Air Ops do Pode Desembarcar.",
            MessageType.Info);

        selectedAircraft = (UnitManager)EditorGUILayout.ObjectField("Aeronave", selectedAircraft, typeof(UnitManager), true);
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

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Local de Pouso", EditorStyles.boldLabel);
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
        using (new EditorGUI.DisabledScope(true))
        {
            GUILayout.Button("Pousar");
        }
    }

    private void RunSimulation()
    {
        Tilemap map = ResolveTilemap();
        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstTerrainDatabaseAsset();

        latestReport = PodePousarSensor.Evaluate(
            selectedAircraft,
            map,
            db,
            movementMode,
            useManualRemainingMovement,
            manualRemainingMovement);

        statusMessage = latestReport != null
            ? $"Simulacao concluida. Local de pouso: {(latestReport.status ? "valido" : "invalido")}."
            : "Falha ao executar simulacao.";
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedAircraft != null && selectedAircraft.BoardTilemap != null)
            return selectedAircraft.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void AutoDetectContext()
    {
        if (selectedAircraft == null)
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
            selectedAircraft = unit;
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
