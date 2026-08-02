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
        Mobilidade,
        // Projecao INVERTIDA: nao pergunta "ate onde eu chego", pergunta "de
        // onde eu chego no objetivo". Por isso exige hex de referencia.
        // Tactical = largada boa (chega no mesmo turno). Operational = largada
        // degradada (chega no turno seguinte), que existe para o caso raro de
        // nao haver hex livre no verde.
        Desembarque
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
    // "E se ela estivesse ali?" — calcula a partir de outro hex sem mover nada.
    // É o que responde a projeção invertida do desembarque (teleporta o
    // passageiro para cima do objetivo e a banda dele vira a zona de largada) e
    // o teste da unidade fantasma da doutrina de fogo indireto.
    [SerializeField] private bool useOriginOverride;
    [SerializeField] private Vector3Int originOverride;
    // Seleção de hex pela cena, mesmo padrão da janela de Retaguarda.
    private bool pickingOrigin;
    private Vector3Int hoverCell;
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

    [MenuItem("Tools/Hotzone/Hotzone")]
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

        DrawOriginOverride();

        intentMode = (IntentMode)EditorGUILayout.EnumPopup(
            "Intenção", intentMode);

        // Medição é parâmetro de entrada, igual à intenção. Só aparecem as
        // válidas para a intenção escolhida — a árvore do contrato.
        DrawSubStepPopup();

        // O envelope fica junto de intenção e medição: os três são a pergunta.
        using (new EditorGUI.DisabledScope(!paintReach))
        {
            viewMode = (ViewMode)EditorGUILayout.EnumPopup(
                "Envelope (Tático/Oper)", viewMode);
        }

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
            $"{ResolveIntentLabel()} / {ResolveSubStepLabel(subStep)}\n" +
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
        if (tilemap == null)
            tilemap = ResolveBoardTilemap();
        AutoDetectAssets();
        Repaint();
    }

    /// <summary>
    /// O tabuleiro NÃO é "o primeiro Tilemap da cena".
    ///
    /// A cena tem vários — AmeacaMap e outros overlays —, e pegar o primeiro
    /// escolhia um mapa de calor no lugar do board. Quem sabe qual é o board são
    /// os objetos que já operam sobre ele; a busca cega fica como último
    /// recurso, e ainda avisa.
    /// </summary>
    private Tilemap ResolveBoardTilemap()
    {
        if (unit != null && unit.BoardTilemap != null)
            return unit.BoardTilemap;

        foreach (UnitManager sceneUnit
                 in FindObjectsByType<UnitManager>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sceneUnit != null && sceneUnit.BoardTilemap != null)
                return sceneUnit.BoardTilemap;
        }

        foreach (ConstructionManager construction
                 in FindObjectsByType<ConstructionManager>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (construction != null && construction.BoardTilemap != null)
                return construction.BoardTilemap;
        }

        foreach (BoardTopologyIndex topology
                 in FindObjectsByType<BoardTopologyIndex>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (topology != null && topology.BoardTilemap != null)
                return topology.BoardTilemap;
        }

        Tilemap fallback = FindFirstObjectByType<Tilemap>();
        if (fallback != null)
        {
            Debug.LogWarning(
                $"[Hotzone] Tabuleiro resolvido por busca cega: '{fallback.name}'. " +
                "Nenhuma unidade, construção ou índice de topologia da cena " +
                "declarou o board — confira se é o mapa certo.");
        }
        return fallback;
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
                OriginOverride = ResolveOriginOverride(),
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
            // Regra geral: vermelho é o ANEL EXTERNO — o que a arma alcança
            // além de onde a unidade pisa.
            //
            // Artilheiro é a exceção, porque a banda dele é da ARMA e não do
            // movimento: o verde já vem sem a zona morta e sem o hex da própria
            // unidade, e o vermelho SOBRESCREVE marcando o que ela realmente
            // atinge. Num obus 3-4 o tático é {3, 4} — 0, 1 e 2 ficam vazios,
            // porque a peça não age em nenhum dos três.
            if (subStep == ReachSubStep.Artilheiro)
                tacticalAction.UnionWith(profile.Tactical.ActionCells);
            else
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
            $"\nMedição: {(profile.Tactical != null ? ResolveSubStepLabel(profile.Tactical.SubStep) : "—")}" +
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

    /// <summary>
    /// "E se ela estivesse ali?" — origem alternativa, sem mover nada.
    ///
    /// É o que a projeção invertida do desembarque pede: teleporta o passageiro
    /// para cima do objetivo e o VERDE resultante é a zona de largada. Serve
    /// também para o teste da unidade fantasma do fogo indireto.
    /// </summary>
    private void DrawOriginOverride()
    {
        useOriginOverride = EditorGUILayout.ToggleLeft(
            "Usar hex de referência",
            useOriginOverride);

        using (new EditorGUI.DisabledScope(!useOriginOverride))
        {
            EditorGUILayout.BeginHorizontal();
            Vector3Int edited = EditorGUILayout.Vector3IntField(
                "Hex de referência", originOverride);
            edited.z = 0;
            originOverride = edited;

            GUI.backgroundColor = pickingOrigin ? Color.red : Color.white;
            if (GUILayout.Button(
                    pickingOrigin ? "Clique no mapa..." : "Escolher no mapa",
                    GUILayout.Width(130f)))
            {
                pickingOrigin = !pickingOrigin;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Usar hex da unidade") && unit != null)
            {
                Vector3Int cell = unit.CurrentCellPosition;
                cell.z = 0;
                originOverride = cell;
            }
        }

        if (useOriginOverride)
        {
            EditorGUILayout.HelpBox(
                "A unidade NÃO é movida: a banda é calculada como se ela " +
                "estivesse no hex de referência. " +
                "Desembarque: escolha o passageiro em Unidade e clique no hex " +
                "do prédio pretendido — o VERDE é a zona de largada boa (ele " +
                "chega no mesmo turno) e o AZUL é a zona de largada degradada " +
                "(chega no turno seguinte), para o caso raro de não haver " +
                "hex livre no verde.",
                MessageType.None);
        }
    }

    private Vector3Int? ResolveOriginOverride()
    {
        if (!useOriginOverride)
            return null;
        Vector3Int cell = originOverride;
        cell.z = 0;
        return cell;
    }

    /// <summary>
    /// Mesmo padrão da janela de Retaguarda: clique esquerdo escolhe, direito
    /// ou ESC cancela.
    /// </summary>
    private void HandleOriginPicking()
    {
        if (!pickingOrigin)
            return;

        Event e = Event.current;
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            hoverCell = ScreenToCell(e.mousePosition);
            HandleUtility.Repaint();
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Vector3Int picked = ScreenToCell(e.mousePosition);
            picked.z = 0;
            originOverride = picked;
            useOriginOverride = true;
            pickingOrigin = false;
            e.Use();
            Repaint();
            return;
        }

        if ((e.type == EventType.MouseDown && e.button == 1)
            || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
        {
            pickingOrigin = false;
            e.Use();
            Repaint();
        }
    }

    private void DrawOriginPicker()
    {
        Handles.color = Color.yellow;
        Vector3 hoverWorld = tilemap.GetCellCenterWorld(hoverCell);
        Handles.DrawWireDisc(hoverWorld, Vector3.back, 0.35f);
        Handles.Label(
            hoverWorld + Vector3.up * 0.42f, $"Referência {hoverCell}");
    }

    private Vector3Int ScreenToCell(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        float t = ray.direction.z != 0f
            ? -ray.origin.z / ray.direction.z
            : 0f;
        return tilemap.WorldToCell(ray.origin + ray.direction * t);
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
                EditorGUILayout.TextField("Medição", ResolveSubStepLabel(subStep));
            return;
        }

        var labels = new string[valid.Count];
        int selected = 0;
        for (int i = 0; i < valid.Count; i++)
        {
            labels[i] = ResolveSubStepLabel(valid[i]);
            if (valid[i] == subStep)
                selected = i;
        }

        selected = EditorGUILayout.Popup("Medição", selected, labels);
        subStep = valid[Mathf.Clamp(selected, 0, valid.Count - 1)];
    }

    /// <summary>
    /// A medição é COMO se mede, não o que a unidade é. Os nomes do enum ainda
    /// dizem domínio (Terrestre/Aereo) porque o rename do tipo é fase própria;
    /// o rótulo já fala a língua certa.
    ///
    /// `Artilheiro` fica de fora do par: ele não é geometria, é "não move e a
    /// banda é da arma". Chamá-lo de linear seria mentira — a cúbica dele vem do
    /// BuildArtilleryBand, que só existe sob a intenção Combate.
    /// </summary>
    private static string ResolveSubStepLabel(ReachSubStep subStep)
    {
        switch (subStep)
        {
            case ReachSubStep.Terrestre:
                return "Geográfico (caminhos)";
            case ReachSubStep.Aereo:
                return "Linear (cúbica)";
            case ReachSubStep.Artilheiro:
                return "Artilheiro (parado, banda da arma)";
            default:
                return subStep.ToString();
        }
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
            case IntentMode.Desembarque: return ReachIntent.Mobility;
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

            case IntentMode.Desembarque:
                // ReachIntent.Disembark ainda nao existe. A ferramenta compoe a
                // resposta com o que existe: alcance de movimento do PASSAGEIRO
                // projetado a partir da referencia. Quando a intencao propria
                // nascer (com a regra do +1MP), so este mapeamento muda.
                intent = ReachIntent.Mobility;
                if (!useOriginOverride)
                {
                    status =
                        "Desembarque precisa do hex de referência: escolha o " +
                        "prédio pretendido no mapa.";
                    return false;
                }
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
            case IntentMode.Desembarque:
                return "Desembarque";
            default:
                return "Combate";
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (tilemap == null)
            return;

        // A escolha do hex acontece mesmo sem resultado calculado: é ela que
        // define O QUE calcular.
        HandleOriginPicking();
        if (pickingOrigin)
            DrawOriginPicker();

        if (!hasResult)
            return;

        if (!paintReach)
            return;

        if (viewMode == ViewMode.Todas
            || viewMode == ViewMode.Operational)
        {
            // Operational é SEMPRE azul. No artilheiro o teto não é orçamento
            // de MP — é o dobro do alcance da arma, que já está no custo.
            DrawCells(
                operationalMovement,
                new Color(0.10f, 0.55f, 1f, 0.25f),
                operationalCosts,
                subStep == ReachSubStep.Artilheiro
                    ? ResolveMaxCost(operationalCosts)
                    : AIActionReachCoordinator.ResolveOperationalBudget(unit));
        }
        if (viewMode == ViewMode.Todas
            || viewMode == ViewMode.Tactical)
        {
            // Verde = onde ele PARA (e quanto custou chegar).
            // No artilheiro, verde = os anéis que a arma cobre: ele não pisa
            // ali, alcança dali. Sem o hex dele e sem a zona morta.
            DrawCells(
                tacticalMovement,
                new Color(0.15f, 1f, 0.35f, 0.32f),
                tacticalCosts,
                subStep == ReachSubStep.Artilheiro
                    ? ResolveMaxCost(tacticalCosts)
                    : unit != null ? Mathf.Max(0, unit.MaxMovementPoints) : 0);
            // Vermelho = onde ele SÓ ALCANÇA com a arma, não pisa.
            // No artilheiro, vermelho = o tiro real, sobrescrevendo o verde.
            DrawCells(
                tacticalAction,
                new Color(0.90f, 0.10f, 0.10f, 0.40f),
                null);
        }
    }

    /// <summary>
    /// Maior custo publicado, usado como teto do degradê quando a banda não é
    /// de movimento — caso do artilheiro, em que o custo é distância cúbica e o
    /// teto é o próprio raio da banda.
    /// </summary>
    private static int ResolveMaxCost(Dictionary<Vector3Int, int> costs)
    {
        int max = 0;
        if (costs == null)
            return max;
        foreach (KeyValuePair<Vector3Int, int> pair in costs)
            max = Mathf.Max(max, pair.Value);
        return max;
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
