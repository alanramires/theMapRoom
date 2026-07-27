using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class QueroCaronaWindow : EditorWindow
{
    [SerializeField] private UnitManager unit;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private QueroCaronaContext context =
        QueroCaronaContext.RogueOuRebelde;
    [SerializeField] private ConstructionSector plannedSector =
        ConstructionSector.None;
    [SerializeField] private int operationalTurns = 2;

    private Tilemap map;
    private QueroCaronaResult result;
    private string status = "Selecione uma unidade em campo.";

    [MenuItem("Tools/Transporte/Quero Carona")]
    public static void Open() =>
        GetWindow<QueroCaronaWindow>("Quero Carona").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetect();
    }

    private void OnDisable() =>
        SceneView.duringSceneGui -= OnSceneGUI;

    private void OnSelectionChange()
    {
        TryUseSelection(silent: true);
        Repaint();
    }

    private void AutoDetect()
    {
        if (unit != null)
            map = unit.BoardTilemap;
        if (terrainDatabase == null)
        {
            string[] guids =
                AssetDatabase.FindAssets("t:TerrainDatabase");
            if (guids.Length > 0)
            {
                terrainDatabase =
                    AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
        if (unit != null
            && context == QueroCaronaContext.ComPlano
            && plannedSector == ConstructionSector.None
            && Enum.TryParse(
                unit.AIAssignedPlanName,
                true,
                out ConstructionSector parsed))
            plannedSector = parsed;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Quero Carona", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Estimativa de necessidade do passageiro. É o contrapeso do " +
            "Melhor LZ de Embarque: verifica se a unidade já consegue cumprir sua " +
            "rota em Tactical ou Operational. SIM não cria ordem nem supera " +
            "prioridades do papel da unidade.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        unit = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade", unit, typeof(UnitManager), true);
        terrainDatabase =
            (TerrainDatabase)EditorGUILayout.ObjectField(
                "Terrain Database", terrainDatabase,
                typeof(TerrainDatabase), false);
        context = (QueroCaronaContext)EditorGUILayout.EnumPopup(
            "Situação", context);
        if (context == QueroCaronaContext.ComPlano)
        {
            plannedSector =
                (ConstructionSector)EditorGUILayout.EnumPopup(
                    "Setor do plano", plannedSector);
        }
        operationalTurns = Mathf.Max(
            1, EditorGUILayout.IntField(
                "Turnos operacionais", operationalTurns));
        if (EditorGUI.EndChangeCheck())
        {
            AutoDetect();
            result = null;
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseSelection(silent: false);
        if (GUILayout.Button("Auto Detect"))
        {
            AutoDetect();
            status = map != null
                ? "Contexto detectado."
                : "Tilemap não encontrado.";
        }
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(
                   unit == null
                   || map == null
                   || terrainDatabase == null))
        {
            if (GUILayout.Button(
                    "Avaliar se Quer Carona",
                    GUILayout.Height(30f)))
                Evaluate();
        }

        if (result == null)
        {
            EditorGUILayout.HelpBox(status, MessageType.None);
            return;
        }

        EditorGUILayout.HelpBox(
            result.wantsRide
                ? "SIM — ACEITA CARONA"
                : "NÃO — RECUSA CARONA",
            result.wantsRide
                ? MessageType.Info
                : MessageType.Warning);
        EditorGUILayout.LabelField(
            "Resultado", result.reason,
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField(
            "Estimativa", result.isEstimate ? "Sim" : "Não");
        EditorGUILayout.LabelField(
            "Emergência",
            result.isEmergency
                ? result.isUnderRepairRuntime
                    ? "Sim — IsUnderRepair runtime"
                    : "Sim — emulado por AI Behavior"
                : "Não");
        EditorGUILayout.LabelField(
            "Avaliação de reparo",
            string.IsNullOrWhiteSpace(result.repairEvaluation)
                ? "Não avaliada."
                : result.repairEvaluation,
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField(
            "Tipo", result.isInfantry
                ? "Infantaria"
                : "Outra unidade");
        EditorGUILayout.LabelField(
            "Envelope encontrado", result.reach.ToString());
        EditorGUILayout.LabelField(
            "Tactical", result.tacticalBudget.ToString());
        EditorGUILayout.LabelField(
            "Operational", result.operationalBudget.ToString());
        EditorGUILayout.LabelField(
            "Custo de rota",
            result.routeCost < int.MaxValue
                ? result.routeCost.ToString()
                : "fora do alcance");
        EditorGUILayout.Vector3IntField(
            "Alvo avaliado", result.evaluatedTarget);
        EditorGUILayout.ObjectField(
            "Prédio avaliado",
            result.evaluatedConstruction,
            typeof(ConstructionManager),
            true);
        EditorGUILayout.LabelField(
            "Nota de necessidade",
            result.rideNeedScore.ToString());
    }

    private void Evaluate()
    {
        SyncRegistry();
        AutoDetect();
        result = QueroCaronaService.Evaluate(
            new QueroCaronaRequest
            {
                unit = unit,
                map = map,
                terrainDatabase = terrainDatabase,
                context = context,
                plannedSector = plannedSector,
                operationalTurns = operationalTurns,
                emulateUnderRepairFromUnitData =
                    !Application.isPlaying
            });
        status = result.reason;
        SceneView.RepaintAll();
    }

    private void TryUseSelection(bool silent)
    {
        UnitManager picked =
            Selection.activeGameObject != null
                ? Selection.activeGameObject
                    .GetComponent<UnitManager>()
                : null;
        if (picked == null)
        {
            if (!silent)
                status =
                    "O objeto selecionado não possui UnitManager.";
            return;
        }
        unit = picked;
        result = null;
        AutoDetect();
        status = $"Unidade: {picked.name}.";
        SceneView.RepaintAll();
    }

    private static void SyncRegistry()
    {
        if (Application.isPlaying)
            return;
        ConstructionManager.AllActive.Clear();
        ConstructionManager[] constructions =
            FindObjectsByType<ConstructionManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            if (constructions[i] != null
                && constructions[i].gameObject.activeInHierarchy)
                ConstructionManager.AllActive.Add(constructions[i]);
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (result == null
            || unit == null
            || map == null
            || result.evaluatedTarget == Vector3Int.zero)
            return;
        Vector3 from =
            map.GetCellCenterWorld(unit.CurrentCellPosition);
        Vector3 to =
            map.GetCellCenterWorld(result.evaluatedTarget);
        Handles.color = result.wantsRide
            ? Color.cyan
            : Color.yellow;
        Handles.DrawDottedLine(from, to, 5f);
        Handles.DrawWireDisc(
            to, Vector3.forward, 0.4f);
        Handles.Label(
            Vector3.Lerp(from, to, 0.5f),
            result.wantsRide
                ? "quer carona"
                : $"vai sozinho ({result.reach})");
    }
}
