using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class HotzoneWindow : EditorWindow
{
    // Espelha ReachIntent. A janela nao interpreta a intencao: apenas escolhe
    // qual delas pedir ao servico unificado.
    private enum IntentMode
    {
        Combate,
        Logistica,
        Transferencia,
        Fusao,
        Embarque,
        Captura,
        Mobilidade
    }

    private enum ViewMode
    {
        Todas,
        Tactical,
        Operational
    }

    [SerializeField] private UnitManager unit;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private IntentMode intentMode = IntentMode.Combate;
    [SerializeField] private ViewMode viewMode = ViewMode.Todas;
    [SerializeField] private ReachSubStep subStep = ReachSubStep.Terrestre;
    [SerializeField] private bool showCellCosts;
    [SerializeField] private bool paintReach = true;
    // A ferramenta PASSA TUDO. Aplicar FoW e descartar o que a unidade não
    // deveria ver é função da IA e dos sensores Pode* na hora da decisão, não
    // da visualização. Estes três ficam desligados e são opt-in, para quando
    // você quiser ver justamente o que cada filtro corta.
    [SerializeField] private bool enableLdt;
    [SerializeField] private bool enableLos;
    [SerializeField] private bool enableSpotter;
    [SerializeField] private int statusFontSize = 15;
    [SerializeField] private int sceneCostFontSize = 18;

    private GUIStyle readoutStyle;
    private GUIStyle sceneCostStyle;

    private readonly HashSet<Vector3Int> tactical =
        new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> tacticalMovement =
        new HashSet<Vector3Int>();
    // Só o que a unidade alcança SEM pisar (tiro, fusão, embarque). Captura
    // nunca entra aqui: ela acontece no hex de parada.
    private readonly HashSet<Vector3Int> tacticalAction =
        new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> operationalMovement =
        new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, int> tacticalCosts =
        new Dictionary<Vector3Int, int>();
    private readonly Dictionary<Vector3Int, ReachOrigin> tacticalOrigins =
        new Dictionary<Vector3Int, ReachOrigin>();
    private readonly HashSet<Vector3Int> operational =
        new HashSet<Vector3Int>();

    private readonly Dictionary<Vector3Int, int> operationalCosts =
        new Dictionary<Vector3Int, int>();

    private string status = "Selecione uma unidade e calcule.";
    private bool hasResult;

    [MenuItem("Tools/Utils/Hotzone")]
    public static void Open()
    {
        GetWindow<HotzoneWindow>("Hotzone").Show();
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
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Hotzone — Tactical / Operational",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "A Hotzone só devolve resposta materializável: movimento, ação, " +
            "custo e origem da ação, nas bandas Tactical e Operational.\n\n" +
            "Intenção decide O QUE se materializa; cada uma delega a um sensor " +
            "Pode*. Combate expande por alcance de arma (o tiro não custa MP). " +
            "Fusão e embarque expandem com custo. Captura e Mobilidade " +
            "devolvem só alcance — cruzar com objetivo é da IA.\n\n" +
            "Não existe banda estratégica. Objetivo fora dessas duas bandas é " +
            "a IA perguntando 'que direção sigo ou preciso de carona?', e ela " +
            "consulta a mobilidade só para o alvo que escolheu.",
            MessageType.Info);

        unit = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade", unit, typeof(UnitManager), true);
        tilemap = (Tilemap)EditorGUILayout.ObjectField(
            "Tabuleiro", tilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database",
            terrainDatabase,
            typeof(TerrainDatabase),
            false);
        dpqAirHeightConfig = (DPQAirHeightConfig)EditorGUILayout.ObjectField(
            "DPQ Air Height",
            dpqAirHeightConfig,
            typeof(DPQAirHeightConfig),
            false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            UseSelected();
        if (GUILayout.Button("Auto Detect"))
            AutoDetect();
        EditorGUILayout.EndHorizontal();

        intentMode = (IntentMode)EditorGUILayout.EnumPopup(
            "Intenção", intentMode);

        // Subetapa é parâmetro de entrada, igual à intenção. Só aparecem as
        // válidas para a intenção escolhida — a árvore do contrato.
        DrawSubStepPopup();
        paintReach = EditorGUILayout.Toggle(
            "Pintar alcance (terreno)", paintReach);

        EditorGUILayout.LabelField(
            "Filtros do sensor (a ferramenta passa tudo por padrão)",
            EditorStyles.miniBoldLabel);
        enableLdt = EditorGUILayout.Toggle("  Aplicar LDT", enableLdt);
        enableLos = EditorGUILayout.Toggle("  Aplicar LoS", enableLos);
        enableSpotter = EditorGUILayout.Toggle(
            "  Exigir observação", enableSpotter);
        using (new EditorGUI.DisabledScope(!paintReach))
        {
            viewMode = (ViewMode)EditorGUILayout.EnumPopup(
                "Modalidade", viewMode);
            showCellCosts = EditorGUILayout.Toggle(
                "Mostrar custos por célula", showCellCosts);
        }
        statusFontSize = EditorGUILayout.IntSlider(
            "Fonte do painel", statusFontSize, 10, 28);
        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(!paintReach || !showCellCosts))
        {
            sceneCostFontSize = EditorGUILayout.IntSlider(
                "Fonte do custo (cena)", sceneCostFontSize, 8, 40);
        }
        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        using (new EditorGUI.DisabledScope(
                   unit == null || tilemap == null))
        {
            if (GUILayout.Button("Calcular Hotzone", GUILayout.Height(30f)))
                Calculate();
        }

        EditorGUILayout.Space(5f);
        DrawReadout(status);
        if (!hasResult)
            return;

        DrawReadout(
            $"Tactical: {tactical.Count} células " +
            $"(movimento {tacticalMovement.Count} + ação {tacticalAction.Count})\n" +
            $"Operational: {operational.Count} células\n\n" +
            $"{ResolveIntentLabel()} / {subStep}\n" +
            "VERDE = onde para (rótulo: custo/MP restante)\n" +
            "VERMELHO = onde só alcança com a arma, não pisa\n" +
            "AZUL = Operational (turno seguinte)");
    }

    // Texto selecionável (dá para copiar o funil para o chat/relatório) e com
    // fonte controlada pelo slider: o diagnóstico é longo e ilegível no
    // miniLabel padrão do Editor.
    private void DrawReadout(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (readoutStyle == null)
        {
            readoutStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = false
            };
        }
        readoutStyle.fontSize = Mathf.Clamp(statusFontSize, 10, 28);

        float width = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 30f);
        float height = readoutStyle.CalcHeight(new GUIContent(text), width);
        EditorGUILayout.SelectableLabel(
            text,
            readoutStyle,
            GUILayout.Height(height),
            GUILayout.ExpandWidth(true));
    }

    private void UseSelected()
    {
        GameObject selected = Selection.activeGameObject;
        UnitManager selectedUnit =
            selected != null
                ? selected.GetComponent<UnitManager>()
                : null;
        if (selectedUnit == null)
        {
            status = "A seleção atual não contém UnitManager.";
            return;
        }

        unit = selectedUnit;
        if (unit.BoardTilemap != null)
            tilemap = unit.BoardTilemap;
        AutoDetectAssets();
        status = $"Unidade selecionada: {unit.name}.";
        hasResult = false;
        SceneView.RepaintAll();
    }

    private void AutoDetect()
    {
        UseSelectedIfAvailable();
        if (tilemap == null && unit != null)
            tilemap = unit.BoardTilemap;
        if (tilemap == null)
        {
            UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
            if (spawner != null)
                tilemap = spawner.GetComponentInChildren<Tilemap>();
            if (tilemap == null)
                tilemap = FindFirstObjectByType<Tilemap>();
        }
        AutoDetectAssets();
        Repaint();
    }

    private void UseSelectedIfAvailable()
    {
        GameObject selected = Selection.activeGameObject;
        UnitManager selectedUnit =
            selected != null
                ? selected.GetComponent<UnitManager>()
                : null;
        if (selectedUnit != null)
            unit = selectedUnit;
    }

    private void AutoDetectAssets()
    {
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAsset<DPQAirHeightConfig>();
    }

    private static T FindFirstAsset<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        return guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<T>(
                AssetDatabase.GUIDToAssetPath(guids[0]))
            : null;
    }

    private void Calculate()
    {
        tactical.Clear();
        tacticalMovement.Clear();
        tacticalAction.Clear();
        operationalMovement.Clear();
        tacticalCosts.Clear();
        tacticalOrigins.Clear();
        operational.Clear();
        operationalCosts.Clear();

        if (unit == null || tilemap == null)
        {
            status = "Unidade ou tabuleiro ausente.";
            hasResult = false;
            return;
        }

        if (!unit.TryGetUnitData(out UnitData unitData)
            || unitData == null)
        {
            status = "UnitData ausente.";
            hasResult = false;
            return;
        }

        if (!TryResolveIntent(unitData, out ReachIntent intent))
        {
            hasResult = false;
            return;
        }

        // A janela nao calcula nada: pede as duas bandas materializaveis ao
        // servico unificado e apenas pinta o resultado.
        UnitReachProfile profile =
            UnitReachEnvelopeService.BuildProfile(new UnitReachRequest
            {
                Unit = unit,
                BoardMap = tilemap,
                TerrainDatabase = terrainDatabase,
                Intent = intent,
                SubStep = subStep,
                Band = ReachBand.Tactical,
                MovementBudget = Mathf.Max(0, unit.MaxMovementPoints),
                IncludeMovementCosts = true,
                // Filtro de camada é predicado de supridor: só vale para
                // logística e transferência. O serviço também ignora nos
                // demais casos, mas ser explícito aqui evita reincidência.
                FilterByOperationDomain =
                    intent == ReachIntent.Service
                    || intent == ReachIntent.Transfer,
                MovementMode = UnitThreatEnvelopeMovement.Potential,
                DpqAirHeightConfig = dpqAirHeightConfig,
                EnableLdt = enableLdt,
                EnableLos = enableLos,
                EnableSpotter = enableSpotter
            });

        // Movimento e ação são camadas DIFERENTES e precisam de cores
        // diferentes: "onde eu paro" não é "onde eu alcanço". Era isso que a
        // tela verde única escondia.
        if (profile.Tactical != null)
        {
            tacticalMovement.UnionWith(profile.Tactical.MovementCells);
            tacticalAction.UnionWith(profile.Tactical.OuterCells);
            tactical.UnionWith(profile.Tactical.ActionCells);
            tactical.UnionWith(profile.Tactical.MovementCells);

            foreach (KeyValuePair<Vector3Int, int> pair
                     in profile.Tactical.CostByCell)
            {
                Vector3Int costCell = pair.Key;
                costCell.z = 0;
                tacticalCosts[costCell] = pair.Value;
            }

            foreach (KeyValuePair<Vector3Int, ReachOrigin> pair
                     in profile.Tactical.OriginByActionCell)
            {
                Vector3Int actionCell = pair.Key;
                actionCell.z = 0;
                tacticalOrigins[actionCell] = pair.Value;
            }
        }

        int operationalBudget =
            AIActionReachCoordinator.ResolveOperationalBudget(unit);
        if (profile.Operational != null)
        {
            foreach (KeyValuePair<Vector3Int, int> pair
                     in profile.Operational.CostByCell)
            {
                Vector3Int costCell = pair.Key;
                costCell.z = 0;
                operationalCosts[costCell] = pair.Value;
            }

            // O ALCANCE do Operational é o que decide se o capturador recusa
            // carona ("chego em 2 rodadas"). É tudo azul, sem subdivisão.
            foreach (Vector3Int rawCell in profile.Operational.MovementCells)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (tactical.Contains(cell))
                    continue;
                operationalMovement.Add(cell);
                operational.Add(cell);
            }
        }

        string geometry =
            AIActionReachCoordinator.UsesCubicSectorReach(unit)
                ? "cúbica (aeronáutica)"
                : "geográfica (caminhos e custos)";
        status =
            $"{unit.name}: intenção={ResolveIntentLabel()}; " +
            $"Tactical={tactical.Count}; " +
            $"Operational={operational.Count}, orçamento={operationalBudget}, " +
            $"regra={geometry}." +
            $"\nSubetapa: {(profile.Tactical != null ? profile.Tactical.SubStep.ToString() : "—")}" +
            $"\nFunil Tactical: {ResolveDiagnostic(profile.Tactical)}" +
            $"\nFunil Operational: {ResolveDiagnostic(profile.Operational)}" +
            $"\nOrigem={unit.CurrentCellPosition} " +
            $"MP={unit.RemainingMovementPoints}/{unit.MaxMovementPoints}" +
            ResolveOriginReport();
        hasResult = true;
        SceneView.RepaintAll();
    }

    private string ResolveOriginReport()
    {
        var report = new System.Text.StringBuilder();

        // Pares (de onde, com quanto sobrando) para as intenções que alcançam
        // um vizinho. Em combate são dezenas e viraria ruído.
        if (tacticalAction.Count > 0 && tacticalAction.Count <= 8)
        {
            report.Append("\nAlcança o vizinho a partir de:");
            foreach (Vector3Int cell in tacticalAction)
            {
                if (!tacticalOrigins.TryGetValue(cell, out ReachOrigin origin))
                    continue;
                report.Append(
                    $"\n  {cell} ← parar em {origin.FromCell} " +
                    $"(sobra {origin.RemainingMovement}, entrada custa {origin.EnterCost})");
            }
        }

        return report.ToString();
    }

    private static string ResolveDiagnostic(UnitReachEnvelope envelope)
    {
        if (envelope == null)
        {
            return "envelope nulo — subetapa inválida para esta unidade, " +
                   "capacidade ausente, ou unidade/tabuleiro sem contexto";
        }
        return string.IsNullOrEmpty(envelope.Diagnostic)
            ? "sem funil (intenção resolvida por sensor de combate)"
            : envelope.Diagnostic;
    }

    private void DrawSubStepPopup()
    {
        ReachIntent intent = ResolveIntentForPopup();
        // Filtrado pela unidade também: Aereo não aparece para quem não é
        // isAircraft.
        IReadOnlyList<ReachSubStep> valid =
            UnitReachEnvelopeService.GetSubSteps(intent, unit);

        if (valid.Count <= 1)
        {
            subStep = valid.Count == 1 ? valid[0] : ReachSubStep.Terrestre;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.EnumPopup("Subetapa", subStep);
            return;
        }

        var labels = new string[valid.Count];
        int selected = 0;
        for (int i = 0; i < valid.Count; i++)
        {
            labels[i] = valid[i].ToString();
            if (valid[i] == subStep)
                selected = i;
        }

        selected = EditorGUILayout.Popup("Subetapa", selected, labels);
        subStep = valid[Mathf.Clamp(selected, 0, valid.Count - 1)];
    }

    private ReachIntent ResolveIntentForPopup()
    {
        switch (intentMode)
        {
            case IntentMode.Logistica: return ReachIntent.Service;
            case IntentMode.Transferencia: return ReachIntent.Transfer;
            case IntentMode.Fusao: return ReachIntent.Fusion;
            case IntentMode.Embarque: return ReachIntent.Embark;
            case IntentMode.Captura: return ReachIntent.Capture;
            case IntentMode.Mobilidade: return ReachIntent.Mobility;
            default: return ReachIntent.Combat;
        }
    }

    private bool TryResolveIntent(UnitData unitData, out ReachIntent intent)
    {
        switch (intentMode)
        {
            case IntentMode.Logistica:
                intent = ReachIntent.Service;
                if (!unitData.isSupplier
                    || unitData.supplierServiceProfile
                        == SupplierServiceProfile.StockTransfer)
                {
                    status =
                        "A unidade não oferece atendimento logístico de campo.";
                    return false;
                }
                return true;

            case IntentMode.Transferencia:
                intent = ReachIntent.Transfer;
                if (!unitData.isSupplier
                    || unitData.supplierTier != SupplierTier.Hub)
                {
                    status = "A unidade não é Hub de transferência.";
                    return false;
                }
                return true;

            case IntentMode.Embarque:
                intent = ReachIntent.Embark;
                if (unit.IsEmbarked)
                {
                    status = "A unidade já está embarcada.";
                    return false;
                }
                return true;

            case IntentMode.Fusao:
                intent = ReachIntent.Fusion;
                return true;

            case IntentMode.Captura:
                intent = ReachIntent.Capture;
                if (!PodeCapturarSensor.HasCaptureConstructionSkill(unit))
                {
                    status = "A unidade não possui skill de captura.";
                    return false;
                }
                return true;

            case IntentMode.Mobilidade:
                intent = ReachIntent.Mobility;
                return true;

            default:
                intent = ReachIntent.Combat;
                return true;
        }
    }

    private string ResolveIntentLabel()
    {
        switch (intentMode)
        {
            case IntentMode.Logistica:
                return "Logística";
            case IntentMode.Transferencia:
                return "Transferência";
            case IntentMode.Embarque:
                return "Embarque";
            case IntentMode.Captura:
                return "Captura";
            case IntentMode.Mobilidade:
                return "Mobilidade";
            case IntentMode.Fusao:
                return "Fusão";
            default:
                return "Combate";
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!hasResult || tilemap == null)
            return;

        if (!paintReach)
            return;

        if (viewMode == ViewMode.Todas
            || viewMode == ViewMode.Operational)
        {
            // Operational é SEMPRE azul.
            DrawCells(
                operationalMovement,
                new Color(0.10f, 0.55f, 1f, 0.25f),
                operationalCosts,
                AIActionReachCoordinator.ResolveOperationalBudget(unit));
        }
        if (viewMode == ViewMode.Todas
            || viewMode == ViewMode.Tactical)
        {
            // Verde = onde ele PARA (e quanto custou chegar).
            DrawCells(
                tacticalMovement,
                new Color(0.15f, 1f, 0.35f, 0.32f),
                tacticalCosts,
                unit != null ? Mathf.Max(0, unit.MaxMovementPoints) : 0);
            // Vermelho = onde ele SÓ ALCANÇA com a arma, não pisa.
            DrawCells(
                tacticalAction,
                new Color(0.90f, 0.10f, 0.10f, 0.40f),
                null);
        }
    }

    private void DrawCells(
        IEnumerable<Vector3Int> cells,
        Color color,
        Dictionary<Vector3Int, int> costs,
        int budget = 0)
    {
        Color previous = Handles.color;
        Handles.color = color;
        foreach (Vector3Int cell in cells)
        {
            Vector3 world = tilemap.GetCellCenterWorld(cell);
            float radius = Mathf.Max(
                0.15f,
                Mathf.Min(
                    Mathf.Abs(tilemap.cellSize.x),
                    Mathf.Abs(tilemap.cellSize.y)) * 0.42f);
            Handles.DrawSolidDisc(world, Vector3.forward, radius);
            if (showCellCosts
                && costs != null
                && costs.TryGetValue(cell, out int cost))
            {
                // "custo acumulado / MP que sobra" — a conta que explica por
                // que a unidade para naquele hex.
                Handles.Label(
                    world,
                    budget > 0
                        ? $"{cost}/{Mathf.Max(0, budget - cost)}"
                        : cost.ToString(),
                    ResolveSceneCostStyle());
            }
        }
        Handles.color = previous;
    }

    // O custo desenhado na cena vinha em miniBoldLabel (~9px), ilegível sobre
    // o sprite da unidade. Fonte controlada pelo slider e branco sólido, que
    // é a única cor que sobrevive tanto ao disco verde quanto ao azul.
    private GUIStyle ResolveSceneCostStyle()
    {
        if (sceneCostStyle == null)
        {
            sceneCostStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }
        sceneCostStyle.fontSize = Mathf.Clamp(sceneCostFontSize, 8, 40);
        sceneCostStyle.normal.textColor = Color.white;
        return sceneCostStyle;
    }
}
