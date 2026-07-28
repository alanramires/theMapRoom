using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class QueroCaronaAereaWindow : EditorWindow
{
    [SerializeField] private UnitManager aircraft;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private int operationalTurns = 2;
    [SerializeField] private bool useMissionFocus;
    [SerializeField] private Vector3Int missionFocus;
    [SerializeField] private float minimumMissionGain = 2f;
    [SerializeField] private bool acceptOnlyCompatibleRecovery = true;
    [SerializeField] private float maximumRecoveryRegression = 2f;

    private Tilemap map;
    private QueroCaronaAereaResult result;
    private string status =
        "Selecione Interceptador, Ataque Aereo ou Vigilancia Aerea.";
    private Vector2 scroll;

    [MenuItem("Tools/Operações Aéreas/Quero Carona Aérea")]
    public static void Open() =>
        GetWindow<QueroCaronaAereaWindow>("Quero Carona Aérea").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetect();
    }

    private void OnDisable() =>
        SceneView.duringSceneGui -= OnSceneGUI;

    private void OnSelectionChange()
    {
        UseSelection(true);
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Quero Carona Aérea", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura para Interceptador, Ataque Aéreo e Vigilância Aérea. Recuperação aceita " +
            "plataforma por emergência; em situação normal, um foco de missão testa " +
            "se o convés realmente melhora o rebasing. Slots e skills vêm do PodePousar.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        aircraft = (UnitManager)EditorGUILayout.ObjectField(
            "Aeronave", aircraft, typeof(UnitManager), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        operationalTurns = Mathf.Max(1, EditorGUILayout.IntField(
            "Turnos operacionais", operationalTurns));
        useMissionFocus = EditorGUILayout.Toggle("Usar foco de missão", useMissionFocus);
        if (useMissionFocus)
        {
            missionFocus = EditorGUILayout.Vector3IntField("Hex da missão", missionFocus);
            minimumMissionGain = Mathf.Max(
                0.1f,
                EditorGUILayout.FloatField(
                    "Ganho mínimo (hex)",
                    minimumMissionGain));
            acceptOnlyCompatibleRecovery = EditorGUILayout.Toggle(
                "Aceitar recuperação única",
                acceptOnlyCompatibleRecovery);
            if (acceptOnlyCompatibleRecovery)
            {
                maximumRecoveryRegression = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        "Regressão máxima",
                        maximumRecoveryRegression));
            }
        }
        if (EditorGUI.EndChangeCheck())
        {
            AutoDetect();
            result = null;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado")) UseSelection(false);
        if (GUILayout.Button("Auto Detect")) AutoDetect();
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(
                   aircraft == null || map == null || terrainDatabase == null))
        {
            if (GUILayout.Button("Avaliar se Quer Carona Aérea", GUILayout.Height(30f)))
                Evaluate();
        }

        EditorGUILayout.HelpBox(status, MessageType.None);
        if (result == null) return;

        EditorGUILayout.HelpBox(
            result.wantsRide ? "SIM — ACEITA PLATAFORMA" : "NÃO — NÃO PEDE PLATAFORMA",
            result.wantsRide ? MessageType.Info : MessageType.Warning);
        EditorGUILayout.LabelField("Resultado", result.reason, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Emergência", result.isEmergency ? "Sim" : "Não");
        EditorGUILayout.LabelField(
            "Papel",
            result.isAirSurveillanceRole
                ? "Vigilância Aérea"
                : result.isAirCombatRole
                    ? "Combate Aéreo"
                    : "Não suportado");
        EditorGUILayout.LabelField(
            "Recuperação única",
            result.isOnlyCompatibleRecovery ? "Sim" : "Não");
        if (useMissionFocus && result.bestPlatform != null)
        {
            EditorGUILayout.LabelField(
                "Missão",
                $"{result.currentMissionDistance:0.#} -> " +
                $"{result.platformMissionDistance:0.#} " +
                $"(ganho {result.missionDistanceGain:0.#})");
        }
        EditorGUILayout.LabelField("Reparo", string.IsNullOrWhiteSpace(result.repairEvaluation)
            ? "Não avaliado." : result.repairEvaluation, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Alcance", $"Tactical {result.tacticalBudget} | Operational {result.operationalBudget}");

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField($"Plataformas compatíveis ({result.platformCount})", EditorStyles.boldLabel);
        for (int i = 0; i < result.platforms.Count; i++)
        {
            MelhorPousoOption option = result.platforms[i];
            string name = option.platform != null ? option.platform.UnitDisplayName : "?";
            EditorGUILayout.LabelField(
                $"#{i + 1} {option.tier} {name} @ {option.cell} | slot {option.platformSlotIndex} | dist={option.distance}",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(option.platformReason, EditorStyles.miniLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void Evaluate()
    {
        AutoDetect();
        result = QueroCaronaAereaService.Evaluate(new QueroCaronaAereaRequest
        {
            aircraft = aircraft,
            map = map,
            terrainDatabase = terrainDatabase,
            operationalTurns = operationalTurns,
            emulateUnderRepairFromUnitData = !Application.isPlaying,
            hasMissionFocus = useMissionFocus,
            missionFocus = missionFocus,
            minimumMissionDistanceGain = minimumMissionGain,
            acceptPlatformWhenOnlyRecovery =
                acceptOnlyCompatibleRecovery,
            maximumMissionRegressionForRecovery =
                maximumRecoveryRegression
        });
        status = result.reason;
        SceneView.RepaintAll();
    }

    private void AutoDetect()
    {
        if (aircraft != null) map = aircraft.BoardTilemap;
        if (terrainDatabase != null) return;
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        if (guids.Length > 0)
            terrainDatabase = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private void UseSelection(bool silent)
    {
        UnitManager picked = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<UnitManager>() : null;
        if (picked == null)
        {
            if (!silent) status = "O objeto selecionado não possui UnitManager.";
            return;
        }
        aircraft = picked;
        result = null;
        AutoDetect();
        status = $"Aeronave: {picked.name}.";
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (result == null || aircraft == null || map == null) return;
        Vector3 from = map.GetCellCenterWorld(aircraft.CurrentCellPosition);
        for (int i = 0; i < result.platforms.Count; i++)
        {
            MelhorPousoOption option = result.platforms[i];
            Vector3 to = map.GetCellCenterWorld(option.cell);
            Handles.color = option == result.bestPlatform ? Color.cyan : Color.gray;
            Handles.DrawDottedLine(from, to, 4f);
            Handles.DrawWireDisc(to, Vector3.forward, .3f);
        }
    }
}
