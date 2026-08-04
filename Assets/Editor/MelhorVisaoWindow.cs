using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MelhorVisaoWindow : EditorWindow
{
    private enum LayerMode
    {
        PrincipalDaFicha = 0,
        All = 1,
        CamadaEspecifica = 2
    }

    private enum CoverageView
    {
        Selecionado = 0,
        Melhor = 1,
        Nenhum = 2
    }

    [SerializeField] private UnitManager unit;
    [SerializeField] private Tilemap overrideMap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private MatchController matchController;
    [SerializeField] private LayerMode layerMode;
    [SerializeField] private Domain manualDomain = Domain.Land;
    [SerializeField] private HeightLevel manualHeight = HeightLevel.Surface;
    [SerializeField] private bool useFullTurnMovement = true;
    [SerializeField] private bool useRuntimeContext = true;
    [SerializeField] private bool usePerceptionSnapshot = true;
    [SerializeField] private bool treatBakeAsExploration;
    [SerializeField] private bool validateFinalOccupancy = true;
    [SerializeField] private bool enableLos = true;
    [SerializeField] private CoverageView coverageView;
    [SerializeField] private List<Vector3Int> focusCells = new List<Vector3Int>();

    // Politica explicita desta ferramenta. O servico nao possui default.
    [SerializeField] private float visibleWeight = 0.25f;
    [SerializeField] private float marginalWeight = 3f;
    [SerializeField] private float unexploredMarginalWeight = 12f;
    [SerializeField] private float recoveredWeight = 6f;
    [SerializeField] private float retainedUniqueWeight = 2f;
    [SerializeField] private float overlapWeight = 0.1f;
    [SerializeField] private float lostUniqueWeight = -5f;
    [SerializeField] private float focusWeight = 10f;
    [SerializeField] private float lostFocusedWeight = -15f;
    [SerializeField] private float movementCostWeight = -0.25f;
    [SerializeField] private bool scoringExpanded;

    private MelhorVisaoResult result;
    private MelhorVisaoCellScore selected;
    private Tilemap resolvedMap;
    private Vector2 scroll;
    private string status =
        "Selecione uma unidade e calcule a melhor posicao de visao.";
    private bool pickingFocus;
    private Vector3Int hoverCell;

    [MenuItem("Tools/Hotzone/Melhor Visão")]
    public static void Open() =>
        GetWindow<MelhorVisaoWindow>("Melhor Visão").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetectContext();
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
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Melhor Visão", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Audita uma unidade a partir de cada hex da hotzone Mobility. "
            + "A consulta usa PodeDetectar com origem virtual: nao move a unidade, "
            + "nao abre FOW e nao altera o tabuleiro. No Edit Mode o calculo e "
            + "estrutural; no Play Mode pode comparar cobertura aliada, conhecidos "
            + "e explorados confirmados.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        unit = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade", unit, typeof(UnitManager), true);
        overrideMap = (Tilemap)EditorGUILayout.ObjectField(
            "Tilemap (opcional)", overrideMap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        dpqAirHeightConfig = (DPQAirHeightConfig)EditorGUILayout.ObjectField(
            "DPQ Air Height", dpqAirHeightConfig, typeof(DPQAirHeightConfig), false);
        layerMode = (LayerMode)EditorGUILayout.EnumPopup(
            "Camada da consulta", layerMode);
        if (layerMode == LayerMode.CamadaEspecifica)
        {
            manualDomain = (Domain)EditorGUILayout.EnumPopup(
                "Domain", manualDomain);
            manualHeight = (HeightLevel)EditorGUILayout.EnumPopup(
                "Height", manualHeight);
        }

        useFullTurnMovement = EditorGUILayout.ToggleLeft(
            "Movimento potencial (turno cheio)", useFullTurnMovement);
        useRuntimeContext = EditorGUILayout.ToggleLeft(
            "Contexto runtime: aliados + explorado", useRuntimeContext);
        usePerceptionSnapshot = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "Consumir FOW confirmado / bake da rodada 0",
                "Le a cobertura aliada das contribuicoes ja fotografadas em vez "
                + "de rodar os sensores de cada aliado outra vez. Sem "
                + "fotografia, o calculo estrutural bruto continua valendo."),
            usePerceptionSnapshot);
        using (new EditorGUI.DisabledScope(!usePerceptionSnapshot))
        {
            EditorGUI.indentLevel++;
            treatBakeAsExploration = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Rodada 0: explorado = conhecido agora",
                    "So para o bake manual, onde nao existe historico anterior. "
                    + "No runtime a exploracao real do MatchController tem "
                    + "precedencia e este campo e ignorado."),
                treatBakeAsExploration);
            EditorGUI.indentLevel--;
        }
        validateFinalOccupancy = EditorGUILayout.ToggleLeft(
            "Validar ocupacao final", validateFinalOccupancy);
        enableLos = EditorGUILayout.ToggleLeft(
            "Validar linha de visao", enableLos);
        coverageView = (CoverageView)EditorGUILayout.EnumPopup(
            "Cobertura detalhada", coverageView);
        if (EditorGUI.EndChangeCheck())
            ClearResult();

        DrawResolvedLayer();
        DrawScoringPolicy();
        DrawActions();

        EditorGUILayout.Space(5f);
        EditorGUILayout.HelpBox(status, MessageType.None);
        DrawResult();
        EditorGUILayout.EndScrollView();
    }

    private void DrawResolvedLayer()
    {
        VisionCoverageLayer layer = ResolveLayer();
        EditorGUILayout.LabelField("Camada resolvida", layer.Label);
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return;

        List<VisionCoverageLayer> auditable =
            VisionCoverageLayerResolver.CollectAuditableLayers(data);
        var labels = new string[auditable.Count];
        for (int i = 0; i < auditable.Count; i++)
            labels[i] = auditable[i].Label;
        EditorGUILayout.LabelField(
            new GUIContent(
                "Camadas auditaveis",
                "All mais as especializacoes declaradas; allHeights se expande."),
            string.Join(", ", labels));
    }

    private void DrawScoringPolicy()
    {
        scoringExpanded = EditorGUILayout.Foldout(
            scoringExpanded,
            "Politica de pontuacao da ferramenta",
            true);
        if (!scoringExpanded)
            return;

        EditorGUI.indentLevel++;
        visibleWeight = EditorGUILayout.FloatField("Visivel", visibleWeight);
        marginalWeight = EditorGUILayout.FloatField("Marginal / ganho bruto", marginalWeight);
        unexploredMarginalWeight = EditorGUILayout.FloatField("Nao explorado", unexploredMarginalWeight);
        recoveredWeight = EditorGUILayout.FloatField("Recuperado", recoveredWeight);
        retainedUniqueWeight = EditorGUILayout.FloatField("Unico mantido", retainedUniqueWeight);
        overlapWeight = EditorGUILayout.FloatField("Sobreposicao", overlapWeight);
        lostUniqueWeight = EditorGUILayout.FloatField("Unico perdido", lostUniqueWeight);
        focusWeight = EditorGUILayout.FloatField("Foco revelado", focusWeight);
        lostFocusedWeight = EditorGUILayout.FloatField("Foco perdido", lostFocusedWeight);
        movementCostWeight = EditorGUILayout.FloatField("Custo de movimento", movementCostWeight);
        EditorGUI.indentLevel--;
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Calcular"))
            Calculate();
        using (new EditorGUI.DisabledScope(
                   Application.isPlaying || matchController == null))
        {
            if (GUILayout.Button("Cozinhar FOW 0"))
                CookRoundZeroFog();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(pickingFocus ? "Clique no Scene View..." : "Adicionar hex de foco"))
        {
            pickingFocus = !pickingFocus;
            SceneView.RepaintAll();
        }
        using (new EditorGUI.DisabledScope(focusCells.Count == 0))
        {
            if (GUILayout.Button($"Limpar foco ({focusCells.Count})"))
            {
                focusCells.Clear();
                ClearResult();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawResult()
    {
        if (result == null)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Ranking", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Diagnostico", result.Diagnostic ?? "-");
        EditorGUILayout.LabelField(
            "Contexto",
            IsRuntimeContextActive()
                ? "Runtime confirmado por camada"
                : "Edit/raw: ganho e perda relativos a origem");
        EditorGUILayout.LabelField(
            "Cobertura aliada",
            result.PerceptionSource == MelhorVisaoPerceptionSource.Snapshot
                ? "Fotografia (contribuicoes por hex)"
                : "Estrutural (sensores dos aliados recalculados)");
        if (!string.IsNullOrEmpty(result.PerceptionDiagnostic))
            EditorGUILayout.LabelField(" ", result.PerceptionDiagnostic);

        int shown = Mathf.Min(result.Ranking.Count, 80);
        for (int i = 0; i < shown; i++)
        {
            MelhorVisaoCellScore entry = result.Ranking[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{i + 1}. {entry.Cell.x},{entry.Cell.y}  score {entry.DisplayScore}",
                entry == result.Best
                    ? EditorStyles.boldLabel
                    : EditorStyles.label);
            if (GUILayout.Button("Ver", GUILayout.Width(48f)))
            {
                selected = entry;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
            VisionCoverageEvaluation e = entry.Evaluation;
            EditorGUILayout.LabelField(
                $"Δ origem {entry.DeltaFromOrigin:+0.##;-0.##;0} | MP {entry.MovementCost} | "
                + $"vis {e.VisibleCount} | ganho {e.GainedFromOriginCells.Count} | "
                + $"perde {e.LostUniqueCells.Count} | foco {e.FocusRevealedCells.Count}");
            if (entry == selected)
                EditorGUILayout.HelpBox(entry.Reason, MessageType.None);
            EditorGUILayout.EndVertical();
        }
    }

    private void Calculate()
    {
        AutoDetectContext();
        if (unit == null)
        {
            status = "Selecione uma unidade.";
            return;
        }
        resolvedMap = overrideMap != null ? overrideMap : unit.BoardTilemap;
        if (resolvedMap == null)
            resolvedMap = FindPreferredTilemap();
        if (resolvedMap == null || terrainDatabase == null)
        {
            status = "Tilemap e Terrain Database sao obrigatorios.";
            return;
        }

        bool runtime = IsRuntimeContextActive();
        int movementBudget = ResolveMovementBudget();
        var focus = new HashSet<Vector3Int>(focusCells);
        VisionCoverageLayer layer = ResolveLayer();
        bool effectiveLos = runtime && matchController != null
            ? matchController.EnableLosValidation
            : enableLos;
        Func<Vector3Int, bool> isExplored = runtime && matchController != null
            ? cell => matchController.IsCellExploredBySlot(
                PlayerSlotId.FromIndex(unit.SlotIndex), cell)
            : null;
        FogKnowledgeSnapshot fogKnowledge = ResolvePerceptionSnapshot(
            out string fogReason);

        result = MelhorVisaoService.Evaluate(new MelhorVisaoRequest
        {
            Unit = unit,
            Map = resolvedMap,
            TerrainDatabase = terrainDatabase,
            DpqAirHeightConfig = dpqAirHeightConfig,
            Layer = layer,
            ScoringPolicy = BuildPolicy(),
            FocusCells = focus,
            IsExplored = isExplored,
            PerceptionSnapshot = fogKnowledge,
            TreatSnapshotKnowledgeAsExploration = treatBakeAsExploration,
            MovementBudget = movementBudget,
            EnableLos = effectiveLos,
            IncludeAlliedCoverage = runtime || fogKnowledge != null,
            ValidateFinalOccupancy = validateFinalOccupancy
        });
        selected = result.Best;
        status = result.Ranking.Count > 0
            ? $"{result.Ranking.Count} hexes avaliados em {layer.Label}. "
                + $"Melhor: {result.Best.Cell.x},{result.Best.Cell.y}."
            : result.Diagnostic;
        if (!string.IsNullOrEmpty(fogReason))
            status += $"\nFOW: {fogReason}";
        Repaint();
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Runtime copia o snapshot confirmado do slot; Edit Mode le o bake manual.
    /// Nenhum dos dois cozinha nada aqui: o bake so muda no botao.
    /// </summary>
    private FogKnowledgeSnapshot ResolvePerceptionSnapshot(out string reason)
    {
        reason = string.Empty;
        if (!usePerceptionSnapshot)
            return null;
        if (matchController == null)
        {
            reason = "MatchController indisponivel; calculo estrutural bruto.";
            return null;
        }

        PlayerSlotId observerSlot = PlayerSlotId.FromIndex(unit.SlotIndex);
        FogKnowledgeSnapshot knowledge;
        bool copied = Application.isPlaying
            ? matchController.TryCopyConfirmedFogKnowledgeSnapshotForSlot(
                observerSlot,
                resolvedMap,
                out knowledge,
                out reason)
            : matchController.TryCopyRoundZeroFogKnowledgeSnapshotForSlot(
                observerSlot,
                resolvedMap,
                out knowledge,
                out reason);
        return copied ? knowledge : null;
    }

    private void CookRoundZeroFog()
    {
        if (Application.isPlaying)
        {
            status = "O FOW da rodada 0 so pode ser cozido no Edit Mode.";
            return;
        }
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (matchController == null)
        {
            status = "MatchController nao encontrado na Scene.";
            return;
        }

        Undo.RecordObject(matchController, "Cozinhar FOW da Rodada 0");
        if (!matchController.TryCookRoundZeroFogForAllSlots(out string bakeResult))
        {
            status = bakeResult;
            Debug.LogError($"[FoW][RoundZeroBake] {bakeResult}", matchController);
            return;
        }

        EditorUtility.SetDirty(matchController);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            matchController.gameObject.scene);
        ClearResult();
        status = bakeResult
            + " O Melhor Visao usara este bake na proxima consulta.";
        Debug.Log($"[FoW][RoundZeroBake] {bakeResult}", matchController);
    }

    private VisionCoverageScoringPolicy BuildPolicy() =>
        new VisionCoverageScoringPolicy(
            visibleWeight,
            marginalWeight,
            unexploredMarginalWeight,
            recoveredWeight,
            retainedUniqueWeight,
            overlapWeight,
            lostUniqueWeight,
            focusWeight,
            lostFocusedWeight,
            movementCostWeight);

    private VisionCoverageLayer ResolveLayer()
    {
        if (layerMode == LayerMode.All)
            return VisionCoverageLayer.All;
        if (layerMode == LayerMode.CamadaEspecifica)
            return VisionCoverageLayer.Specific(manualDomain, manualHeight);
        return VisionCoverageLayerResolver.ResolvePrincipal(unit);
    }

    private int ResolveMovementBudget()
    {
        if (unit == null)
            return 0;
        if (useFullTurnMovement || !Application.isPlaying)
            return unit.MaxMovementPoints;
        if (unit.HasActed)
            return 0;
        return unit.RemainingMovementPoints;
    }

    private bool IsRuntimeContextActive() =>
        Application.isPlaying
        && useRuntimeContext
        && matchController != null;

    private void OnSceneGUI(SceneView sceneView)
    {
        HandleFocusPicking();
        DrawFocusCells();
        if (resolvedMap == null || result == null || result.Ranking.Count == 0)
            return;

        float maxAbsDelta = 1f;
        for (int i = 0; i < result.Ranking.Count; i++)
            maxAbsDelta = Mathf.Max(
                maxAbsDelta,
                Mathf.Abs(result.Ranking[i].DeltaFromOrigin));

        for (int i = 0; i < result.Ranking.Count; i++)
        {
            MelhorVisaoCellScore entry = result.Ranking[i];
            Color color = ResolveCandidateColor(entry, maxAbsDelta);
            Vector3 world = resolvedMap.GetCellCenterWorld(entry.Cell);
            Handles.color = color;
            float radius = entry == result.Best ? 0.31f : 0.23f;
            Handles.DrawSolidDisc(world, Vector3.back, radius);
            Handles.Label(
                world,
                $"{entry.DisplayScore}\n+{entry.Evaluation.GainedFromOriginCells.Count}"
                + $"/-{entry.Evaluation.LostUniqueCells.Count}",
                ScoreLabelStyle(
                    entry == result.Best
                        ? Color.black
                        : Color.white));
        }

        if (result.Origin != null)
        {
            Handles.color = new Color(0.2f, 0.55f, 1f, 1f);
            Handles.DrawWireDisc(
                resolvedMap.GetCellCenterWorld(result.Origin.Cell),
                Vector3.back,
                result.Origin == result.Best ? 0.37f : 0.29f);
        }

        HandleCandidateClick();

        MelhorVisaoCellScore shown = ResolveShownCoverage();
        if (shown != null)
            DrawCoverage(shown);
    }

    private void HandleCandidateClick()
    {
        Event evt = Event.current;
        if (evt.type != EventType.MouseDown
            || evt.button != 0
            || evt.alt
            || result == null)
        {
            return;
        }

        Vector2 mouse = evt.mousePosition;
        MelhorVisaoCellScore nearest = null;
        float nearestPixels = 18f;
        for (int i = 0; i < result.Ranking.Count; i++)
        {
            MelhorVisaoCellScore candidate = result.Ranking[i];
            Vector2 guiPoint = HandleUtility.WorldToGUIPoint(
                resolvedMap.GetCellCenterWorld(candidate.Cell));
            float distance = Vector2.Distance(mouse, guiPoint);
            if (distance >= nearestPixels)
                continue;
            nearestPixels = distance;
            nearest = candidate;
        }
        if (nearest == null)
            return;

        selected = nearest;
        evt.Use();
        Repaint();
        SceneView.RepaintAll();
    }

    private MelhorVisaoCellScore ResolveShownCoverage()
    {
        switch (coverageView)
        {
            case CoverageView.Melhor:
                return result?.Best;
            case CoverageView.Nenhum:
                return null;
            default:
                return selected ?? result?.Best;
        }
    }

    private void DrawCoverage(MelhorVisaoCellScore score)
    {
        VisionCoverageEvaluation e = score.Evaluation;
        foreach (Vector3Int cell in score.Coverage.VisibleCells)
        {
            Color color;
            if (e.FocusRevealedCells.Contains(cell))
                color = new Color(0.9f, 0.1f, 1f, 0.34f);
            else if (e.UnexploredMarginalCells.Contains(cell))
                color = new Color(0f, 0.9f, 1f, 0.28f);
            else if (e.RecoveredCells.Contains(cell))
                color = new Color(0.15f, 0.45f, 1f, 0.28f);
            else if (e.MarginalCells.Contains(cell))
                color = new Color(0.1f, 0.9f, 0.25f, 0.22f);
            else
                color = new Color(0.65f, 0.65f, 0.7f, 0.14f);
            Handles.color = color;
            Handles.DrawSolidDisc(
                resolvedMap.GetCellCenterWorld(cell),
                Vector3.back,
                0.13f);
        }
        Handles.color = new Color(1f, 0.15f, 0.05f, 0.9f);
        foreach (Vector3Int cell in e.LostUniqueCells)
        {
            Handles.DrawWireDisc(
                resolvedMap.GetCellCenterWorld(cell),
                Vector3.back,
                0.18f);
        }
    }

    private Color ResolveCandidateColor(
        MelhorVisaoCellScore entry,
        float maxAbsDelta)
    {
        if (entry == result.Best)
            return new Color(1f, 0.72f, 0.05f, 0.96f);
        if (entry == result.Origin)
            return new Color(0.25f, 0.55f, 1f, 0.9f);
        if (entry.DeltaFromOrigin > 0.01f)
            return Color.Lerp(
                new Color(0.35f, 0.85f, 0.35f, 0.75f),
                new Color(0f, 0.55f, 0.1f, 0.95f),
                entry.DeltaFromOrigin / maxAbsDelta);
        if (entry.DeltaFromOrigin < -0.01f)
            return Color.Lerp(
                new Color(0.95f, 0.5f, 0.08f, 0.75f),
                new Color(0.8f, 0.05f, 0.05f, 0.95f),
                -entry.DeltaFromOrigin / maxAbsDelta);
        return new Color(0.9f, 0.82f, 0.1f, 0.8f);
    }

    private void HandleFocusPicking()
    {
        if (!pickingFocus)
            return;
        Tilemap map = resolvedMap != null
            ? resolvedMap
            : overrideMap != null
                ? overrideMap
                : unit != null ? unit.BoardTilemap : FindPreferredTilemap();
        if (map == null)
            return;

        Event evt = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        Plane plane = new Plane(Vector3.forward, map.transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            hoverCell = map.WorldToCell(ray.GetPoint(enter));
            hoverCell.z = 0;
            Handles.color = new Color(1f, 0.1f, 1f, 0.95f);
            Handles.DrawWireDisc(
                map.GetCellCenterWorld(hoverCell), Vector3.back, 0.31f);
        }

        HandleUtility.AddDefaultControl(
            GUIUtility.GetControlID(FocusType.Passive));
        if (evt.type != EventType.MouseDown
            || evt.button != 0
            || evt.alt)
        {
            return;
        }

        int index = focusCells.IndexOf(hoverCell);
        if (index >= 0)
            focusCells.RemoveAt(index);
        else
            focusCells.Add(hoverCell);
        pickingFocus = false;
        ClearResult();
        evt.Use();
        Repaint();
        SceneView.RepaintAll();
    }

    private void DrawFocusCells()
    {
        Tilemap map = resolvedMap != null
            ? resolvedMap
            : overrideMap != null
                ? overrideMap
                : unit != null ? unit.BoardTilemap : null;
        if (map == null)
            return;
        Handles.color = new Color(1f, 0.1f, 1f, 0.95f);
        for (int i = 0; i < focusCells.Count; i++)
        {
            Handles.DrawWireDisc(
                map.GetCellCenterWorld(focusCells[i]),
                Vector3.back,
                0.29f);
        }
    }

    private void TryUseSelection()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
            return;
        UnitManager found = selectedObject.GetComponent<UnitManager>();
        if (found == null)
            found = selectedObject.GetComponentInParent<UnitManager>();
        if (found == null)
            return;
        unit = found;
        ClearResult();
        AutoDetectContext();
    }

    private void AutoDetectContext()
    {
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (terrainDatabase == null && turnStateManager != null)
            terrainDatabase = turnStateManager.TerrainDatabaseRef;
        if (terrainDatabase == null && matchController != null)
            terrainDatabase = matchController.TerrainDatabaseRef;
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();
        if (dpqAirHeightConfig == null && turnStateManager != null)
            dpqAirHeightConfig = turnStateManager.DpqAirHeightConfigRef;
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAsset<DPQAirHeightConfig>();
        if (unit != null && unit.BoardTilemap != null)
        {
            resolvedMap = unit.BoardTilemap;
            if (overrideMap == null)
                overrideMap = unit.BoardTilemap;
        }
        if (overrideMap == null)
            overrideMap = FindPreferredTilemap();
    }

    private void ClearResult()
    {
        result = null;
        selected = null;
        SceneView.RepaintAll();
    }

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap[] maps = FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include);
        if (maps == null || maps.Length == 0)
            return null;
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null
                && string.Equals(
                    maps[i].name,
                    "TileMap",
                    StringComparison.OrdinalIgnoreCase))
            {
                return maps[i];
            }
        }
        return maps[0];
    }

    private static T FindFirstAsset<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        for (int i = 0; i < guids.Length; i++)
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (asset != null)
                return asset;
        }
        return null;
    }

    private static GUIStyle ScoreLabelStyle(Color color) =>
        new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = color }
        };
}
