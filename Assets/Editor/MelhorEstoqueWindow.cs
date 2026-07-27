using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MelhorEstoqueWindow : EditorWindow
{
    [SerializeField] private UnitManager unit;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private MelhorEstoqueIntent intent =
        MelhorEstoqueIntent.Auto;
    [SerializeField] private int operationalTurns = 2;
    [SerializeField] private bool includeStrategic;
    [SerializeField] private bool showProbableDirection;
    [SerializeField] private bool showRejected;

    private Tilemap map;
    private MelhorEstoqueResult result;
    private MelhorEstoqueOption selected;
    private MelhorEstoqueOption probableDirection;
    private Vector2 scroll;
    private string status =
        "Selecione uma unidade com serviço Transfer.";

    [MenuItem("Tools/Logistica/Melhor Estoque")]
    public static void Open() =>
        GetWindow<MelhorEstoqueWindow>(
            "Melhor Estoque").Show();

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

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Melhor Estoque", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura da rede de estoque. A varredura nasce na " +
            "unidade escolhida, percorre Tactical → Operational → " +
            "Strategic e simula o Pode Transferir em cada encontro. " +
            "Nenhuma transferência ou movimento é confirmado.",
            MessageType.Info);

        EditorGUILayout.LabelField(
            "Contexto", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        unit = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade", unit, typeof(UnitManager), true);
        terrainDatabase =
            (TerrainDatabase)EditorGUILayout.ObjectField(
                "Terrain Database",
                terrainDatabase,
                typeof(TerrainDatabase),
                false);
        intent = (MelhorEstoqueIntent)EditorGUILayout.EnumPopup(
            new GUIContent(
                "Intenção",
                "Auto compara todos os fluxos válidos. As demais " +
                "opções isolam um trabalho da rede."),
            intent);
        operationalTurns = Mathf.Max(
            1,
            EditorGUILayout.IntField(
                "Turnos operacionais", operationalTurns));
        includeStrategic = EditorGUILayout.Toggle(
            "Incluir Strategic", includeStrategic);
        showProbableDirection = EditorGUILayout.Toggle(
            new GUIContent(
                "Ver direção provável",
                "Quando Strategic não participa da decisão, desenha " +
                "apenas a orientação do melhor endpoint distante."),
            showProbableDirection);
        if (EditorGUI.EndChangeCheck())
            ClearResult();

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
                    "Calcular Melhor Estoque",
                    GUILayout.Height(30f)))
                Calculate();
        }

        EditorGUILayout.HelpBox(status, MessageType.None);
        if (result == null)
            return;

        DrawActorNeed();
        DrawBestDecision();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField(
            $"Encontros válidos ({result.ranking.Count})",
            EditorStyles.boldLabel);
        for (int i = 0; i < result.ranking.Count; i++)
            DrawOption(i, result.ranking[i]);

        showRejected = EditorGUILayout.Foldout(
            showRejected,
            $"Contextos recusados ({result.rejected.Count})",
            true);
        if (showRejected)
        {
            for (int i = 0; i < result.rejected.Count; i++)
            {
                MelhorEstoqueReject reject = result.rejected[i];
                EditorGUILayout.LabelField(
                    $"{reject.actionCell}: {reject.reason}",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawActorNeed()
    {
        StockNeedAssessment need = result.actorNeed;
        if (need == null)
            return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            "Reservas da unidade", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Estado",
            $"{need.level} — {need.totalCurrent}/" +
            $"{need.totalCapacity} (faltam {need.totalMissing})");
        EditorGUILayout.LabelField(
            need.reason, EditorStyles.wordWrappedMiniLabel);
        for (int i = 0; i < need.resources.Count; i++)
        {
            StockResourceNeed resource = need.resources[i];
            string label = resource.supply != null
                && !string.IsNullOrWhiteSpace(
                    resource.supply.displayName)
                    ? resource.supply.displayName
                    : resource.supply != null
                        ? resource.supply.name
                        : "?";
            EditorGUILayout.LabelField(
                $"  {label}: {resource.current}/" +
                $"{resource.capacity} " +
                $"(faltam {resource.missing})",
                EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawBestDecision()
    {
        if (selected == null)
        {
            EditorGUILayout.HelpBox(
                "Nenhum encontro compatível encontrado.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Melhor encontro: {selected.tier} | " +
                $"{FormatFlow(selected)} | " +
                $"ação {selected.actionCell} | " +
                $"estimado {selected.estimatedAmount}",
                MessageType.Info);
        }

        if (showProbableDirection
            && !includeStrategic
            && probableDirection != null)
        {
            EditorGUILayout.HelpBox(
                "Direção provável — diagnóstico, não participa da " +
                $"decisão: {FormatFlow(probableDirection)} em " +
                $"{probableDirection.endpointCell}.",
                MessageType.Info);
        }
    }

    private void DrawOption(
        int index,
        MelhorEstoqueOption option)
    {
        if (option == null)
            return;
        bool active = selected == option;
        GUI.backgroundColor = active
            ? new Color(1f, 0.8f, 0.2f)
            : Color.white;
        if (GUILayout.Button(
                $"#{index + 1} {option.tier} " +
                $"{FormatFlow(option)} | " +
                $"encontro={option.actionCell}",
                EditorStyles.miniButton))
        {
            selected = option;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        if (!active)
            return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            "Intenção", option.intent.ToString());
        EditorGUILayout.LabelField(
            "Origem", ResolveSource(option));
        EditorGUILayout.LabelField(
            "Destino", ResolveDestination(option));
        EditorGUILayout.LabelField(
            "Fluxo final",
            option.prospectiveTransfer != null
                ? option.prospectiveTransfer.flowMode.ToString()
                : "-");
        EditorGUILayout.LabelField(
            "Rota",
            option.routeCost >= 0
                ? $"{option.routeCost} MP"
                : "Strategic: direção por distância cúbica");
        EditorGUILayout.LabelField(
            "Distância cúbica",
            option.cubicDistance.ToString());
        EditorGUILayout.LabelField(
            "Transferência estimada",
            option.estimatedAmount.ToString());
        for (int i = 0;
             i < option.compatibleSupplies.Count;
             i++)
        {
            StockTransferEstimate estimate =
                option.compatibleSupplies[i];
            string supplyName = estimate.supply != null
                && !string.IsNullOrWhiteSpace(
                    estimate.supply.displayName)
                    ? estimate.supply.displayName
                    : estimate.supply != null
                        ? estimate.supply.name
                        : "?";
            string available = estimate.available == int.MaxValue
                ? "∞"
                : estimate.available.ToString();
            EditorGUILayout.LabelField(
                $"  {supplyName}: {estimate.amount} " +
                $"(disp. {available}, pedido " +
                $"{estimate.requested})",
                EditorStyles.miniLabel);
        }
        EditorGUILayout.LabelField(
            "Nota", option.score.ToString("0"));
        if (option.stockNeed != null)
        {
            EditorGUILayout.LabelField(
                "Necessidade atendida",
                option.stockNeed.reason,
                EditorStyles.wordWrappedMiniLabel);
        }
        if (option.constructionStockNeed != null)
        {
            EditorGUILayout.LabelField(
                "Necessidade da construcao",
                option.constructionStockNeed.reason,
                EditorStyles.wordWrappedMiniLabel);
        }
        if (option.sourceStockNeed != null)
        {
            EditorGUILayout.LabelField(
                "Reserva da origem",
                option.sourceStockNeed.reason,
                EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.LabelField(
            option.reason, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void Calculate()
    {
        SyncRegistry();
        AutoDetect();
        result = Evaluate(includeStrategic);
        selected = result.best;
        probableDirection = null;
        if (showProbableDirection && !includeStrategic)
        {
            MelhorEstoqueResult direction = Evaluate(true);
            probableDirection = direction.ranking.Find(
                option =>
                    option.tier == AIReachDecisionTier.Strategic);
        }

        status = selected != null
            ? $"Decisão {selected.tier}: " +
              $"{FormatFlow(selected)}."
            : "Nenhum fluxo compatível no horizonte escolhido.";
        SceneView.RepaintAll();
    }

    private MelhorEstoqueResult Evaluate(bool strategic) =>
        MelhorEstoqueService.Evaluate(
            new MelhorEstoqueRequest
            {
                unit = unit,
                map = map,
                terrainDatabase = terrainDatabase,
                intent = intent,
                tacticalBudget = Mathf.Max(
                    0,
                    Application.isPlaying
                        ? unit.RemainingMovementPoints
                        : unit.MaxMovementPoints),
                operationalTurns = operationalTurns,
                includeStrategic = strategic,
                emulateStockFromUnitData = !Application.isPlaying
            });

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
        AutoDetect();
        ClearResult();
        status = $"Unidade: {picked.name}.";
    }

    private void AutoDetect()
    {
        if (unit != null && unit.BoardTilemap != null)
            map = unit.BoardTilemap;
        if (terrainDatabase != null)
            return;
        string[] guids =
            AssetDatabase.FindAssets("t:TerrainDatabase");
        if (guids.Length > 0)
        {
            terrainDatabase =
                AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    private void ClearResult()
    {
        AutoDetect();
        result = null;
        selected = null;
        probableDirection = null;
        SceneView.RepaintAll();
    }

    private static string FormatFlow(
        MelhorEstoqueOption option) =>
        $"{ResolveSource(option)} → {ResolveDestination(option)}";

    private static string ResolveSource(
        MelhorEstoqueOption option)
    {
        if (option?.sourceUnit != null)
            return option.sourceUnit.name;
        if (option?.sourceConstruction != null)
            return option.sourceConstruction.ConstructionDisplayName;
        return "?";
    }

    private static string ResolveDestination(
        MelhorEstoqueOption option)
    {
        if (option?.destinationUnit != null)
            return option.destinationUnit.name;
        if (option?.destinationConstruction != null)
            return option.destinationConstruction
                .ConstructionDisplayName;
        return "?";
    }

    private static void SyncRegistry()
    {
        if (Application.isPlaying)
            return;
        UnitManager.AllActive.Clear();
        UnitManager[] units =
            FindObjectsByType<UnitManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] != null
                && units[i].gameObject.activeInHierarchy)
                UnitManager.AllActive.Add(units[i]);
        }

        ConstructionManager.AllActive.Clear();
        ConstructionManager[] constructions =
            FindObjectsByType<ConstructionManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            if (constructions[i] != null
                && constructions[i].gameObject.activeInHierarchy)
                ConstructionManager.AllActive.Add(
                    constructions[i]);
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (map == null || unit == null)
            return;

        if (result != null)
        {
            for (int i = 0; i < result.ranking.Count; i++)
            {
                MelhorEstoqueOption option = result.ranking[i];
                Vector3 world =
                    map.GetCellCenterWorld(option.actionCell);
                Handles.color = option == selected
                    ? Color.yellow
                    : ResolveTierColor(option.tier);
                Handles.DrawWireDisc(
                    world,
                    Vector3.forward,
                    option == selected ? 0.42f : 0.25f);
                Handles.Label(
                    world + Vector3.up * 0.3f,
                    $"{option.tier} {option.estimatedAmount}");
            }
        }

        if (selected != null)
            DrawDecision(selected, Color.yellow, dotted: false);
        if (showProbableDirection
            && !includeStrategic
            && probableDirection != null)
        {
            DrawDecision(
                probableDirection,
                new Color(1f, 0.3f, 0.8f),
                dotted: true);
        }
    }

    private void DrawDecision(
        MelhorEstoqueOption option,
        Color color,
        bool dotted)
    {
        Vector3 origin = map.GetCellCenterWorld(
            unit.CurrentCellPosition);
        Vector3 action =
            map.GetCellCenterWorld(option.actionCell);
        Vector3 endpoint =
            map.GetCellCenterWorld(option.endpointCell);
        Handles.color = color;
        if (dotted)
            Handles.DrawDottedLine(origin, endpoint, 6f);
        else
        {
            Handles.DrawAAPolyLine(4f, origin, action);
            Handles.DrawDottedLine(action, endpoint, 4f);
        }
    }

    private static Color ResolveTierColor(
        AIReachDecisionTier tier)
    {
        switch (tier)
        {
            case AIReachDecisionTier.Tactical:
                return Color.green;
            case AIReachDecisionTier.Operational:
                return new Color(0.2f, 0.7f, 1f);
            default:
                return new Color(1f, 0.3f, 0.8f);
        }
    }
}
