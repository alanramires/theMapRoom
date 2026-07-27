using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MelhorLocalPousoWindow : EditorWindow
{
    [SerializeField] private UnitManager aircraft;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private int operationalTurns = 2;

    private Tilemap map;
    private MelhorPousoResult result;
    private MelhorPousoOption selected;
    private Vector2 scroll;
    private string status = "Selecione uma aeronave em voo.";

    public static void Open() =>
        GetWindow<MelhorLocalPousoWindow>(
            "Melhor Local para Pouso").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetect();
    }

    private void OnDisable() =>
        SceneView.duringSceneGui -= OnSceneGUI;

    private void OnSelectionChange()
    {
        UseSelection(silent: true);
        Repaint();
    }

    private void AutoDetect()
    {
        if (aircraft != null)
            map = aircraft.BoardTilemap;
        if (terrainDatabase != null)
            return;
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        if (guids.Length > 0)
        {
            terrainDatabase = AssetDatabase.LoadAssetAtPath<
                TerrainDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Melhor Local para Pouso", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura. Varre LZs de pouso a partir da aeronave em " +
            "Tactical e Operational. Cada hex passa pelo PodePousar: " +
            "construção > estrutura + terreno > terreno, com suas skills.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        aircraft = (UnitManager)EditorGUILayout.ObjectField(
            "Aeronave", aircraft, typeof(UnitManager), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase,
            typeof(TerrainDatabase), false);
        operationalTurns = Mathf.Max(1, EditorGUILayout.IntField(
            "Turnos operacionais", operationalTurns));
        if (EditorGUI.EndChangeCheck())
        {
            AutoDetect();
            result = null;
            selected = null;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            UseSelection(silent: false);
        if (GUILayout.Button("Auto Detect"))
            AutoDetect();
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(
                   aircraft == null || map == null || terrainDatabase == null))
        {
            if (GUILayout.Button("Calcular Locais de Pouso",
                    GUILayout.Height(30f)))
                Calculate();
        }

        EditorGUILayout.HelpBox(status, MessageType.None);
        if (result == null)
            return;

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField(
            $"LZs autorizadas ({result.options.Count})",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"Alcance usado: Tactical {result.tacticalBudget} | " +
            $"Operational {result.operationalBudget} | " +
            $"Autonomia atual {result.autonomyRemaining}",
            EditorStyles.wordWrappedMiniLabel);
        for (int i = 0; i < result.options.Count; i++)
        {
            MelhorPousoOption option = result.options[i];
            bool active = option == selected;
            GUI.backgroundColor = active
                ? new Color(1f, .8f, .2f) : Color.white;
            if (GUILayout.Button(
                    $"#{i + 1} {option.tier} LZ={option.cell} | " +
                    $"dist={option.distance} | rota=" +
                    $"{(option.routeCost >= 0 ? option.routeCost.ToString() : "op")}",
                    EditorStyles.miniButton))
            {
                selected = option;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            if (!active)
                continue;

            PodePousarReport landing = option.landing;
            AirOperationTileContext context =
                AirOperationResolver.ResolveContext(map, terrainDatabase,
                    option.cell);
            EditorGUILayout.LabelField(
                $"Camada após pouso: {landing.landingDomain} / " +
                $"{landing.landingHeight}",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                $"Contexto: {context.source} | " +
                $"superfície={context.landingSurface}",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(landing.explicacao,
                EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void UseSelection(bool silent)
    {
        UnitManager picked = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<UnitManager>()
            : null;
        if (picked == null)
        {
            if (!silent)
                status = "O objeto selecionado não possui UnitManager.";
            return;
        }
        aircraft = picked;
        AutoDetect();
        result = null;
        selected = null;
        status = $"Aeronave: {picked.name}.";
    }

    private void Calculate()
    {
        AutoDetect();
        result = MelhorPousoService.Evaluate(new MelhorPousoRequest
        {
            aircraft = aircraft,
            map = map,
            terrainDatabase = terrainDatabase,
            tacticalBudget = Mathf.Max(0, aircraft.RemainingMovementPoints),
            operationalTurns = operationalTurns
        });
        selected = result.best;
        status = selected != null
            ? $"Melhor LZ: {selected.tier} {selected.cell}."
            : "Nenhuma LZ autorizada em Tactical ou Operational.";
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (result == null || map == null)
            return;
        for (int i = 0; i < result.options.Count; i++)
        {
            MelhorPousoOption option = result.options[i];
            Vector3 world = map.GetCellCenterWorld(option.cell);
            Handles.color = option == selected ? Color.yellow
                : option.tier == MelhorPousoTier.Tactical
                    ? Color.green : new Color(.2f, .7f, 1f);
            Handles.DrawWireDisc(world, Vector3.forward,
                option == selected ? .42f : .26f);
            Handles.Label(world + Vector3.up * .27f,
                option.tier == MelhorPousoTier.Tactical ? "T" : "O");
        }
    }
}
