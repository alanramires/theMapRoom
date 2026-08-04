using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MelhorCombateWindow : EditorWindow
{
    private enum QueryMode
    {
        Parado = 0,
        MoverEAtacar = 1,
        Hibrido = 2,
        AutoDaFicha = 3
    }

    [SerializeField] private UnitManager unit;
    [SerializeField] private Tilemap overrideMap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private RPSDatabase rpsDatabase;
    [SerializeField] private DPQMatchupDatabase dpqMatchupDatabase;
    [SerializeField] private WeaponPriorityData weaponPriorityData;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private QueryMode mode = QueryMode.AutoDaFicha;
    [SerializeField] private ReachSubStep mobileSubStep = ReachSubStep.Terrestre;
    [SerializeField] private bool defensiveContext;
    [SerializeField] private bool applyRuntimeFog = true;
    [SerializeField] private bool recalculateExperimentalKnowledge;
    [SerializeField] private bool enableLdt = true;
    [SerializeField] private bool enableLos = true;
    [SerializeField] private bool enableSpotter = true;
    [SerializeField] private bool enableStealth = true;
    [SerializeField] private bool drawEmptyOrigins;
    [SerializeField] private bool drawSelectedSensorRejections = true;
    [SerializeField] private int listFontSize = 11;
    [SerializeField] private bool stationaryExpanded = true;
    [SerializeField] private bool mobileExpanded = true;
    [SerializeField] private bool databasesExpanded;
    [SerializeField] private bool sensorRulesExpanded;

    private MelhorCombateResult result;
    private MelhorCombateCellResult selectedCell;
    private MelhorCombateCandidate selectedCandidate;
    private Tilemap resolvedMap;
    private TurnStateManager turnStateManager;
    private MatchController matchController;
    private FogKnowledgeSnapshot knowledgeSnapshot;
    private bool knowledgeSnapshotFromRuntime;
    private string knowledgeSnapshotDiagnostic = string.Empty;
    private Vector2 scroll;
    private string status =
        "Selecione uma unidade e calcule os combates possíveis.";
    private GUIStyle wrappedStyle;
    private readonly List<MelhorCombateCellResult> orderedStationaryCells =
        new List<MelhorCombateCellResult>();
    private readonly List<MelhorCombateCellResult> orderedMobileCells =
        new List<MelhorCombateCellResult>();
    private readonly Dictionary<MelhorCombateCellResult, int> rankByCell =
        new Dictionary<MelhorCombateCellResult, int>();
    private readonly HashSet<UnitManager> simulatedDetectedTargets =
        new HashSet<UnitManager>();
    private readonly HashSet<UnitManager> simulatedSelfDetectedTargets =
        new HashSet<UnitManager>();
    private readonly HashSet<UnitManager> simulatedVisibilityRejections =
        new HashSet<UnitManager>();
    private readonly Dictionary<UnitManager, List<UnitManager>> simulatedObserversByTarget =
        new Dictionary<UnitManager, List<UnitManager>>();
    private readonly Dictionary<UnitManager, List<UnitManager>> perceptionContributorsByTarget =
        new Dictionary<UnitManager, List<UnitManager>>();
    private readonly HashSet<UnitManager> constructionDetectedTargets =
        new HashSet<UnitManager>();
    private bool preMovementVisibilityAvailable;

    [MenuItem("Tools/Hotzone/Melhor Combate")]
    public static void Open() =>
        GetWindow<MelhorCombateWindow>("Melhor Combate").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetectContext();
        TryUseSelection(silent: true);
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
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Melhor Combate", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Auditoria consultiva de UMA unidade. Parado pergunta quem ela "
            + "atinge da posição atual; Mover e atacar testa o PodeMirar em "
            + "cada origem alcançável; Híbrido mantém os dois rankings "
            + "separados. A ferramenta não move peças, não consome munição, "
            + "não aplica dano e não altera FOW.\n\n"
            + "A arma exibida é a primeira opção executável do PodeMirar. Se "
            + "a roofgun estiver vazia, ela aparece nas recusas e o canhão é "
            + "simulado — mesmo quando isso produz um combate ruim.",
            MessageType.Info);

        DrawContext();
        DrawActions();

        EditorGUILayout.Space(5f);
        EditorGUILayout.HelpBox(status, MessageType.None);
        DrawResult();
        EditorGUILayout.EndScrollView();
    }

    private void DrawContext()
    {
        EditorGUI.BeginChangeCheck();
        unit = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade", unit, typeof(UnitManager), true);
        overrideMap = (Tilemap)EditorGUILayout.ObjectField(
            new GUIContent(
                "Tilemap (opcional)",
                "Vazio usa o TileMap da unidade ou o TileMap padrão da cena."),
            overrideMap,
            typeof(Tilemap),
            true);
        mode = (QueryMode)EditorGUILayout.EnumPopup("Modo", mode);
        if (IncludesMobile(mode))
            DrawMobileMeasurePopup();
        defensiveContext = EditorGUILayout.Toggle(
            new GUIContent(
                "Contexto defensivo",
                "Aplica a tolerância defensiva do Attack Decision."),
            defensiveContext);

        databasesExpanded = EditorGUILayout.Foldout(
            databasesExpanded,
            "Bases de combate",
            true);
        if (databasesExpanded)
        {
            EditorGUI.indentLevel++;
            terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
                "Terrain Database",
                terrainDatabase,
                typeof(TerrainDatabase),
                false);
            rpsDatabase = (RPSDatabase)EditorGUILayout.ObjectField(
                "RPS Database",
                rpsDatabase,
                typeof(RPSDatabase),
                false);
            dpqMatchupDatabase = (DPQMatchupDatabase)EditorGUILayout.ObjectField(
                "DPQ Matchup",
                dpqMatchupDatabase,
                typeof(DPQMatchupDatabase),
                false);
            weaponPriorityData = (WeaponPriorityData)EditorGUILayout.ObjectField(
                "Weapon Priority",
                weaponPriorityData,
                typeof(WeaponPriorityData),
                false);
            dpqAirHeightConfig = (DPQAirHeightConfig)EditorGUILayout.ObjectField(
                "DPQ Air Height",
                dpqAirHeightConfig,
                typeof(DPQAirHeightConfig),
                false);
            EditorGUI.indentLevel--;
        }

        sensorRulesExpanded = EditorGUILayout.Foldout(
            sensorRulesExpanded,
            "Regras do PodeMirar",
            true);
        if (sensorRulesExpanded)
        {
            EditorGUI.indentLevel++;
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                applyRuntimeFog = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Aplicar FOW runtime",
                        "No Edit Mode a ferramenta usa o bake manual da rodada 0."),
                    applyRuntimeFog);
            }
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                recalculateExperimentalKnowledge = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Experimento: recalcular FOW",
                        "Desligado usa o bake persistido no MatchController. " +
                        "Ligado recalcula uma fotografia temporaria somente ao " +
                        "apertar Calcular e nao altera o bake da Scene."),
                    recalculateExperimentalKnowledge);
            }
            enableLdt = EditorGUILayout.Toggle("Validar LdT", enableLdt);
            enableLos = EditorGUILayout.Toggle("Validar LoS", enableLos);
            enableSpotter = EditorGUILayout.Toggle("Exigir spotter", enableSpotter);
            enableStealth = EditorGUILayout.Toggle("Validar stealth", enableStealth);
            drawEmptyOrigins = EditorGUILayout.Toggle(
                "Desenhar origens sem alvo", drawEmptyOrigins);
            drawSelectedSensorRejections = EditorGUILayout.Toggle(
                "Recusas da origem selecionada", drawSelectedSensorRejections);
            listFontSize = EditorGUILayout.IntSlider(
                "Fonte da lista", listFontSize, 9, 20);
            EditorGUI.indentLevel--;
        }

        if (EditorGUI.EndChangeCheck())
        {
            ResolveMap();
            ClearResult();
        }

        DrawResolvedState();
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseSelection(silent: false);
        if (GUILayout.Button("Auto Detect"))
        {
            AutoDetectContext();
            status = "Contexto detectado.";
        }
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        using (new EditorGUI.DisabledScope(
                   Application.isPlaying || matchController == null))
        {
            if (GUILayout.Button("Cozinhar FOW 0"))
                CookRoundZeroFog();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(
                   unit == null
                   || ResolveMap() == null
                   || terrainDatabase == null))
        {
            if (GUILayout.Button("Calcular Melhor Combate", GUILayout.Height(30f)))
                Calculate();
        }
        if (GUILayout.Button("Limpar", GUILayout.Width(90f), GUILayout.Height(30f)))
        {
            ClearResult();
            status = "Resultado limpo.";
        }
        EditorGUILayout.EndHorizontal();
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
        InvalidateKnowledgeSnapshot();
        status = bakeResult + " O Melhor Combate usara este bake no proximo calculo.";
        Debug.Log($"[FoW][RoundZeroBake] {bakeResult}", matchController);
    }

    private void DrawResolvedState()
    {
        EditorGUILayout.LabelField(
            "Contexto da cena",
            Application.isPlaying
                ? applyRuntimeFog
                    ? "Runtime com FOW confirmado"
                    : "Runtime usando o bake manual da rodada 0"
                : recalculateExperimentalKnowledge
                    ? "Scene:Edit com fotografia experimental temporaria"
                    : "Scene:Edit usando o bake manual da rodada 0");

        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        int loaded = 0;
        int empty = 0;
        if (weapons != null)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                UnitEmbarkedWeapon weapon = weapons[i];
                if (weapon?.weapon == null)
                    continue;
                if (weapon.squadAmmunition > 0)
                    loaded++;
                else
                    empty++;
            }
        }

        EditorGUILayout.LabelField(
            "Estado serializado",
            $"HP {unit.CurrentHP}/{data.maxHP} | combustível {unit.CurrentFuel} | "
            + $"MP {unit.RemainingMovementPoints}/{unit.MaxMovementPoints} | "
            + $"armas {loaded} carregadas, {empty} vazias");
        EditorGUILayout.LabelField(
            "Preferências",
            $"alvo por classe | DPQ={(data.prioritizeDpqAtBattle ? "sim" : "não")} | "
            + $"alcance máximo={(data.preferRepositionAtWeaponMaxRange ? "sim" : "não")} | "
            + $"artilheiro primeiro={(data.preferArtilleryModeBeforeCombatant ? "sim" : "não")}",
            EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawMobileMeasurePopup()
    {
        List<ReachSubStep> supported =
            UnitReachEnvelopeService.GetSubSteps(ReachIntent.Combat, unit);
        supported.Remove(ReachSubStep.Artilheiro);

        if (supported.Count == 0)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Movimento", "Sem subetapa móvel válida");
            return;
        }

        if (!supported.Contains(mobileSubStep))
            mobileSubStep = ResolveDefaultMobileSubStep(supported);

        var labels = new string[supported.Count];
        int selected = 0;
        for (int i = 0; i < supported.Count; i++)
        {
            labels[i] = ResolveSubStepLabel(supported[i]);
            if (supported[i] == mobileSubStep)
                selected = i;
        }

        selected = EditorGUILayout.Popup("Movimento", selected, labels);
        mobileSubStep = supported[Mathf.Clamp(selected, 0, supported.Count - 1)];
    }

    private void Calculate()
    {
        resolvedMap = ResolveMap();
        selectedCell = null;
        selectedCandidate = null;
        if (unit == null || resolvedMap == null || terrainDatabase == null)
        {
            status = "Unidade, Tilemap e Terrain Database são obrigatórios.";
            return;
        }

        if (!TryPrepareKnowledgeSnapshot(
                out FogKnowledgeSnapshot knowledge,
                out string knowledgeReason))
        {
            ClearResult();
            status = knowledgeReason;
            return;
        }

        Predicate<UnitManager> preMovementFilter =
            PrepareVisibilityFromKnowledge(knowledge);
        PodeMirarPerceptionSnapshot perception =
            BuildPerceptionSnapshot();

        result = MelhorCombateService.Evaluate(new MelhorCombateRequest
        {
            Unit = unit,
            BoardMap = resolvedMap,
            TerrainDatabase = terrainDatabase,
            RpsDatabase = rpsDatabase,
            DpqMatchupDatabase = dpqMatchupDatabase,
            WeaponPriorityData = weaponPriorityData,
            DpqAirHeightConfig = dpqAirHeightConfig,
            Mode = ResolveServiceMode(mode),
            MobileSubStep = mobileSubStep,
            MovementBudget = 0,
            DefensiveContext = defensiveContext,
            EnableLdt = enableLdt,
            EnableLos = enableLos,
            EnableSpotter = enableSpotter,
            EnableStealth = enableStealth,
            RespectTotalWarVisibility = false,
            TargetCandidates = knowledge.VisibleEnemyUnits,
            PreMovementTargetFilter = preMovementFilter,
            PerceptionSnapshot = perception
        });

        RebuildCellRanks();
        SelectInitialResult();
        int unavailable = CountAdmission(CombatAdmissionTier.Unavailable);
        int blocked = CountAdmission(CombatAdmissionTier.Blocked);
        int allowed = CountAdmission(CombatAdmissionTier.Allowed);
        string fogSourceLabel = knowledgeSnapshotFromRuntime
            ? "runtime confirmado"
            : !Application.isPlaying && recalculateExperimentalKnowledge
                ? "experimento temporario"
                : "rodada 0 manual";
        status =
            $"{allowed} admitidos | {blocked} bloqueados | {unavailable} indisponíveis. "
            + (preMovementVisibilityAvailable
                ? $"Conhecimento do slot: {simulatedDetectedTargets.Count} contato(s), "
                    + $"{simulatedVisibilityRejections.Count} descartado(s) do modo movel. "
                : string.Empty)
            + $"FOW={fogSourceLabel}. "
            + result.Diagnostic;
        Repaint();
        SceneView.RepaintAll();
    }

    private bool TryPrepareKnowledgeSnapshot(
        out FogKnowledgeSnapshot snapshot,
        out string reason)
    {
        snapshot = null;
        reason = string.Empty;
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        PlayerSlotId observerSlot = PlayerSlotId.FromIndex(unit.SlotIndex);
        if (Application.isPlaying && applyRuntimeFog)
        {
            if (matchController == null)
            {
                reason = "MatchController indisponível: a ferramenta não "
                    + "recalcula FOW como fallback em runtime.";
                return false;
            }

            if (!matchController.TryCopyConfirmedFogKnowledgeSnapshotForSlot(
                    observerSlot,
                    resolvedMap,
                    out snapshot,
                    out reason))
            {
                reason += " Aguarde a publicação em Neutral; nenhum sensor "
                    + "foi recalculado.";
                return false;
            }

            knowledgeSnapshot = snapshot;
            knowledgeSnapshotFromRuntime = true;
            knowledgeSnapshotDiagnostic = reason;
            return true;
        }

        if (!Application.isPlaying && recalculateExperimentalKnowledge)
        {
            bool cooked = FogKnowledgeSnapshotBuilder.TryBuild(
                CreateFallbackKnowledgeRequest(observerSlot),
                out snapshot,
                out reason);
            if (!cooked || snapshot == null)
                return false;

            knowledgeSnapshot = snapshot;
            knowledgeSnapshotFromRuntime = false;
            knowledgeSnapshotDiagnostic =
                "fotografia experimental temporaria; o bake da rodada 0 nao foi alterado. " +
                reason;
            return true;
        }

        if (matchController == null)
        {
            reason =
                "MatchController indisponivel. O Melhor Combate nao cozinha " +
                "automaticamente o FOW da Scene.";
            return false;
        }
        if (!matchController.TryCopyRoundZeroFogKnowledgeSnapshotForSlot(
                observerSlot,
                resolvedMap,
                out snapshot,
                out reason))
        {
            return false;
        }

        knowledgeSnapshot = snapshot;
        knowledgeSnapshotFromRuntime = false;
        knowledgeSnapshotDiagnostic = reason;
        return true;
    }

    private FogKnowledgeBuildRequest CreateFallbackKnowledgeRequest(
        PlayerSlotId observerSlot)
    {
        return new FogKnowledgeBuildRequest
        {
            ObserverSlot = observerSlot,
            BoardMap = resolvedMap,
            TerrainDatabase = terrainDatabase,
            DpqAirHeightConfig = dpqAirHeightConfig,
            EnableLos = enableLos,
            EnableStealth = enableStealth
        };
    }

    private Predicate<UnitManager> PrepareVisibilityFromKnowledge(
        FogKnowledgeSnapshot snapshot)
    {
        simulatedDetectedTargets.Clear();
        simulatedSelfDetectedTargets.Clear();
        simulatedVisibilityRejections.Clear();
        simulatedObserversByTarget.Clear();
        perceptionContributorsByTarget.Clear();
        constructionDetectedTargets.Clear();
        preMovementVisibilityAvailable = false;
        if (snapshot == null)
            return null;

        for (int i = 0; i < snapshot.VisibleEnemyUnits.Count; i++)
        {
            UnitManager target = snapshot.VisibleEnemyUnits[i];
            if (target != null)
                simulatedDetectedTargets.Add(target);
        }

        foreach (KeyValuePair<UnitManager, List<UnitManager>> pair in
                 snapshot.DetectionContributorsByTarget)
        {
            UnitManager target = pair.Key;
            List<UnitManager> observers = pair.Value;
            if (target == null || observers == null)
                continue;
            for (int i = 0; i < observers.Count; i++)
            {
                UnitManager observer = observers[i];
                if (observer == null)
                    continue;
                RegisterPerceptionContributor(target, observer);
                if (observer == unit)
                    simulatedSelfDetectedTargets.Add(target);
                else
                    RegisterDetectionObserver(target, observer);
            }
        }
        constructionDetectedTargets.UnionWith(
            snapshot.ConstructionDetectedTargets);

        // Runtime ja publicou os contatos e o FOW ja guardou quais fontes
        // abriram cada hex. Cruza as duas fotografias uma vez; nenhuma origem
        // hipotetica precisa chamar os sensores de percepcao novamente.
        for (int i = 0; i < snapshot.VisibleEnemyUnits.Count; i++)
        {
            UnitManager target = snapshot.VisibleEnemyUnits[i];
            if (target == null
                || perceptionContributorsByTarget.ContainsKey(target))
            {
                continue;
            }

            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            if (!snapshot.TryGetVisibilityContributors(
                    targetCell,
                    out IReadOnlyList<UnitManager> contributors))
            {
                continue;
            }

            for (int contributorIndex = 0;
                 contributorIndex < contributors.Count;
                 contributorIndex++)
            {
                UnitManager observer = contributors[contributorIndex];
                if (!CanSnapshotContributorDetectTarget(observer, target))
                    continue;
                RegisterPerceptionContributor(target, observer);
                if (observer == unit)
                    simulatedSelfDetectedTargets.Add(target);
                else
                    RegisterDetectionObserver(target, observer);
            }
        }

        // No Scene:Edit podemos mostrar os descartados porque todas as pecas
        // fazem parte do tapete de auditoria. Em runtime isto revelaria objetos
        // ocultos e, portanto, e expressamente proibido.
        if (!Application.isPlaying)
        {
            UnitManager[] allUnits = UnityEngine.Object.FindObjectsByType<UnitManager>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < allUnits.Length; i++)
            {
                UnitManager target = allUnits[i];
                if (target != null && !target.IsDead && !target.IsEmbarked &&
                    PlayerSlotRelations.AreEnemies(unit, target) &&
                    target.BoardTilemap == resolvedMap &&
                    !simulatedDetectedTargets.Contains(target))
                {
                    simulatedVisibilityRejections.Add(target);
                }
            }
        }

        preMovementVisibilityAvailable = true;
        return FilterByPreMovementVisibility;
    }

    private bool FilterByPreMovementVisibility(UnitManager target)
    {
        if (target == null || simulatedDetectedTargets.Contains(target))
            return true;

        simulatedVisibilityRejections.Add(target);
        return false;
    }

    private void RegisterDetectionObserver(UnitManager target, UnitManager observer)
    {
        if (target == null || observer == null)
            return;
        if (!simulatedObserversByTarget.TryGetValue(target, out List<UnitManager> observers))
        {
            observers = new List<UnitManager>();
            simulatedObserversByTarget[target] = observers;
        }
        if (!observers.Contains(observer))
            observers.Add(observer);
    }

    private void RegisterPerceptionContributor(
        UnitManager target,
        UnitManager observer)
    {
        if (target == null || observer == null)
            return;
        if (!perceptionContributorsByTarget.TryGetValue(
                target,
                out List<UnitManager> contributors))
        {
            contributors = new List<UnitManager>();
            perceptionContributorsByTarget[target] = contributors;
        }
        if (!contributors.Contains(observer))
            contributors.Add(observer);
    }

    private PodeMirarPerceptionSnapshot BuildPerceptionSnapshot()
    {
        return new PodeMirarPerceptionSnapshot
        {
            IsTargetDetected = target =>
                target != null && simulatedDetectedTargets.Contains(target),
            ResolveDetectionContributors = target =>
                target != null
                && perceptionContributorsByTarget.TryGetValue(
                    target,
                    out List<UnitManager> contributors)
                    ? contributors
                    : System.Array.Empty<UnitManager>(),
            IsObservedByConstruction = target =>
                target != null && constructionDetectedTargets.Contains(target)
        };
    }

    /// <summary>
    /// A geometria/LoS ja esta embutida na contribuicao por fonte. Para stealth
    /// sobra apenas a capacidade data-driven da ficha do observador.
    /// </summary>
    private static bool CanSnapshotContributorDetectTarget(
        UnitManager observer,
        UnitManager target)
    {
        if (observer == null || target == null)
            return false;
        if (target.HasFiredThisTurn || target.HasPendingForcedLayerLock)
            return true;
        if (!target.TryGetUnitData(out UnitData targetData)
            || targetData == null
            || !targetData.IsStealthUnit(
                target.GetDomain(),
                target.GetHeightLevel()))
        {
            return true;
        }
        if (!observer.TryGetUnitData(out UnitData observerData)
            || observerData == null)
        {
            return false;
        }
        return observerData.CanDetectStealthFor(
            target.GetDomain(),
            target.GetHeightLevel(),
            targetData);
    }

    private void DrawResult()
    {
        if (result == null)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Resultado", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Diagnóstico", result.Diagnostic, WrappedStyle());
        EditorGUILayout.LabelField(
            "Orçamento de movimento", result.MovementBudget.ToString());
        EditorGUILayout.LabelField(
            "Auto da ficha",
            result.PreferredMode == MelhorCombateCandidateMode.Stationary
                ? "tenta Parado primeiro"
                : "tenta Mover e atacar primeiro");
        EditorGUILayout.LabelField(
            "Legenda",
            "Âmbar: melhor escolhido | verde: admitido | vermelho: bloqueado | "
            + "magenta: simulação indisponível | cinza: origem sem combate | "
            + "tracejado âmbar: spotter de tiro → alvo | "
            + "tracejado ciano: quem detectou → alvo",
            EditorStyles.wordWrappedMiniLabel);

        DrawSimulatedVisibilityReport();

        if (result.StationaryCells.Count > 0)
        {
            stationaryExpanded = EditorGUILayout.Foldout(
                stationaryExpanded,
                $"Parado — {result.StationaryRanking.Count} combate(s)",
                true);
            if (stationaryExpanded)
                DrawCellRanking(MelhorCombateCandidateMode.Stationary);
        }

        if (result.MobileCells.Count > 0)
        {
            mobileExpanded = EditorGUILayout.Foldout(
                mobileExpanded,
                $"Mover e atacar — {result.MobileRanking.Count} combate(s) em "
                + $"{result.MobileCells.Count} origem(ns)",
                true);
            if (mobileExpanded)
                DrawCellRanking(MelhorCombateCandidateMode.MoveAndAttack);
        }

        if (result.StationaryCells.Count == 0 && result.MobileCells.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Nenhuma origem foi produzida. Confira a subetapa de movimento, "
                + "munição e armamento da unidade.",
                MessageType.Warning);
        }

        DrawSelectedDetails();
    }

    private void DrawSimulatedVisibilityReport()
    {
        if (!preMovementVisibilityAvailable)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Conhecimento do slot",
            EditorStyles.miniBoldLabel);
        string snapshotLabel = knowledgeSnapshotFromRuntime
            ? "Snapshot confirmado do runtime"
            : !Application.isPlaying && recalculateExperimentalKnowledge
                ? "FOW experimental temporario"
                : "FOW manual da rodada 0";
        EditorGUILayout.HelpBox(
            $"{snapshotLabel}: "
            + $"{knowledgeSnapshot?.KnownCells.Count ?? 0} hex(es) conhecido(s), "
            + $"{simulatedDetectedTargets.Count} contato(s) inimigo(s). "
            + "A mesma lista alimenta todas as origens; o PodeMirar não "
            + "recalcula percepção. " + knowledgeSnapshotDiagnostic,
            MessageType.None);

        if (simulatedVisibilityRejections.Count == 0)
            return;

        var rejected = new List<UnitManager>(simulatedVisibilityRejections);
        rejected.Sort((a, b) => string.CompareOrdinal(
            ResolveUnitLabel(a),
            ResolveUnitLabel(b)));
        for (int i = 0; i < rejected.Count; i++)
        {
            UnitManager target = rejected[i];
            EditorGUILayout.LabelField(
                $"• {ResolveUnitLabel(target)} descartado antes do ranking: "
                + "o slot não possuía este contato no snapshot cozido "
                + $"(LoS={(enableLos ? "ligada" : "desligada")}, "
                + $"stealth={(enableStealth ? "ligado" : "desligado")}).",
                WrappedStyle());
        }
    }

    private void DrawCellRanking(MelhorCombateCandidateMode candidateMode)
    {
        List<MelhorCombateCellResult> ordered = candidateMode
            == MelhorCombateCandidateMode.Stationary
            ? orderedStationaryCells
            : orderedMobileCells;
        int shown = Mathf.Min(ordered.Count, 120);
        for (int i = 0; i < shown; i++)
        {
            MelhorCombateCellResult cell = ordered[i];
            MelhorCombateCandidate best = cell.Best;
            bool isSelected = cell == selectedCell;
            bool isOverallBest = best != null && best == result.Best;
            GUI.backgroundColor = isSelected
                ? new Color(1f, 0.78f, 0.18f)
                : Color.white;

            string label = BuildCellLabel(i + 1, cell, best, isOverallBest);
            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                selectedCell = cell;
                selectedCandidate = best;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
        }

        if (ordered.Count > shown)
        {
            EditorGUILayout.LabelField(
                $"Mostrando {shown} de {ordered.Count} origens.",
                EditorStyles.miniLabel);
        }
    }

    private void DrawSelectedDetails()
    {
        if (selectedCell == null)
            return;

        EditorGUILayout.Space(7f);
        EditorGUILayout.LabelField(
            $"Origem selecionada {selectedCell.Cell.x},{selectedCell.Cell.y}",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Movimento",
            $"modo={ResolveCandidateModeLabel(selectedCell.Mode)} | "
            + $"custo={selectedCell.MovementCost} | restante={selectedCell.RemainingMovement}");

        if (selectedCell.Candidates.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Nenhum combate válido do PodeMirar nesta origem. As recusas "
                + "do sensor abaixo explicam as armas/alvos examinados.",
                MessageType.Warning);
            DrawSensorRejections();
            return;
        }

        EditorGUILayout.LabelField("Combates nesta origem", EditorStyles.miniBoldLabel);
        for (int i = 0; i < selectedCell.Candidates.Count; i++)
        {
            MelhorCombateCandidate candidate = selectedCell.Candidates[i];
            bool picked = candidate == selectedCandidate;
            GUI.backgroundColor = picked
                ? new Color(1f, 0.78f, 0.18f)
                : Color.white;
            if (GUILayout.Button(
                    $"#{i + 1} {ResolveAdmissionTag(candidate.RankKey.Admission)} "
                    + $"{ResolveUnitLabel(candidate.Target)} | "
                    + $"{ResolveWeaponLabel(candidate.SensorOption?.weapon)} | "
                    + BuildMilitarySummary(candidate),
                    EditorStyles.miniButton))
            {
                selectedCandidate = candidate;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
        }

        MelhorCombateCandidate selected = selectedCandidate ?? selectedCell.Best;
        if (selected != null)
            DrawCandidateDetails(selected);
        DrawSensorRejections();
    }

    private void DrawCandidateDetails(MelhorCombateCandidate candidate)
    {
        PodeMirarTargetOption option = candidate.SensorOption;
        AttackDecisionResult decision = candidate.Evaluation.AttackDecision;
        CombatEvaluationResult combat = candidate.Evaluation.Combat;

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            $"{ResolveAdmissionTag(candidate.RankKey.Admission)} — {decision.Status}",
            EditorStyles.boldLabel);
        EditorGUILayout.ObjectField(
            "Alvo", candidate.Target, typeof(UnitManager), true);
        EditorGUILayout.ObjectField(
            "Arma canônica",
            option != null ? option.weapon : null,
            typeof(WeaponData),
            false);
        EditorGUILayout.LabelField(
            "Execução",
            candidate.IsCanonicalSensorOption
                ? $"CANÔNICA do PodeMirar — slot W{option.embarkedWeaponIndex + 1}"
                : "Opção canônica indisponível");
        EditorGUILayout.LabelField(
            "Munição atual",
            TryGetWeaponAmmo(unit, option != null ? option.embarkedWeaponIndex : -1, out int ammo)
                ? ammo.ToString()
                : "-");
        EditorGUILayout.LabelField(
            "Distância / preferência de alcance",
            option != null
                ? $"{option.distance} hex | desvio={candidate.RankKey.RangeDistanceFromPreferred}"
                : "-");
        EditorGUILayout.LabelField(
            "Preferência de alvo",
            candidate.RankKey.TargetPreference.ToString());
        EditorGUILayout.LabelField(
            "DPQ",
            $"atacante {decision.AttackerDpq.Points}/def {decision.AttackerDpq.DefenseBonus} | "
            + $"alvo {decision.DefenderDpq.Points}/def {decision.DefenderDpq.DefenseBonus}");

        if (candidate.Evaluation.HasSimulation)
        {
            EditorGUILayout.LabelField(
                "HP previsto",
                $"atacante {combat.AttackerHpBefore} → {combat.Simulation.attackerHpAfter} "
                + $"(-{combat.AttackerLoss}, {combat.AttackerLossPercent}%) | "
                + $"alvo {combat.TargetHpBefore} → {combat.Simulation.defenderHpAfter} "
                + $"(-{combat.TargetDamage}, {combat.TargetDamagePercent}%)");
            EditorGUILayout.LabelField(
                "Resultado militar",
                $"kill={(combat.Simulation.killGuaranteed ? "sim" : "não")} | "
                + $"sobrevive={(combat.Simulation.attackerSurvives ? "sim" : "não")} | "
                + $"troca={candidate.RankKey.TradeBalancePercent:+#;-#;0}%");
            EditorGUILayout.ObjectField(
                "Arma de revide",
                combat.CounterWeapon,
                typeof(WeaponData),
                false);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Simulação indisponível. A opção do PodeMirar continua visível, "
                + "mas não recebe uma promessa de HP.",
                MessageType.Warning);
        }

        EditorGUILayout.LabelField(
            "Attack Decision",
            decision.Reason,
            WrappedStyle());
        if (option != null)
        {
            EditorGUILayout.LabelField(
                "Posições",
                $"atacante={option.attackerPositionLabel} | alvo={option.defenderPositionLabel}",
                WrappedStyle());
            EditorGUILayout.LabelField(
                "Spotter",
                option.usedForwardObserver
                    ? $"{ResolveUnitLabel(option.forwardObserverUnit)} — {option.forwardObserverReason}"
                    : "não usado",
                WrappedStyle());
            EditorGUILayout.LabelField(
                "Detectado por",
                ResolveDetectionObserversLabel(option.targetUnit),
                WrappedStyle());
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawSensorRejections()
    {
        if (result == null || selectedCell == null)
            return;

        var matching = new List<MelhorCombateSensorRejection>();
        for (int i = 0; i < result.SensorRejections.Count; i++)
        {
            MelhorCombateSensorRejection rejection = result.SensorRejections[i];
            if (rejection != null
                && rejection.Mode == selectedCell.Mode
                && rejection.FromCell == selectedCell.Cell)
            {
                matching.Add(rejection);
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            $"Recusas do PodeMirar nesta origem ({matching.Count})",
            EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(BuildActiveValidationSummary(), MessageType.None);
        if (matching.Count == 0)
        {
            EditorGUILayout.LabelField("Nenhuma recusa registrada.", EditorStyles.miniLabel);
            if (Application.isPlaying && applyRuntimeFog)
            {
                EditorGUILayout.HelpBox(
                    "Com o FOW runtime ligado, alvos fora da visibilidade "
                    + "confirmada são omitidos antes de o PodeMirar produzir "
                    + "recusas nominais.",
                    MessageType.Info);
            }
            return;
        }

        UnitManager selectedTarget = selectedCandidate != null
            ? selectedCandidate.Target
            : null;
        matching.Sort((a, b) =>
        {
            bool aSame = a.InvalidOption != null && a.InvalidOption.targetUnit == selectedTarget;
            bool bSame = b.InvalidOption != null && b.InvalidOption.targetUnit == selectedTarget;
            int same = bSame.CompareTo(aSame);
            return same != 0
                ? same
                : string.CompareOrdinal(
                    a.InvalidOption?.reasonId,
                    b.InvalidOption?.reasonId);
        });

        int shown = Mathf.Min(matching.Count, 50);
        for (int i = 0; i < shown; i++)
        {
            PodeMirarInvalidOption invalid = matching[i].InvalidOption;
            if (invalid == null)
                continue;
            EditorGUILayout.LabelField(
                $"• {BuildRejectionExplanation(invalid)}",
                WrappedStyle());
        }
    }

    private string BuildActiveValidationSummary()
    {
        string fow = !Application.isPlaying
            ? "não aplicado no Scene:Edit"
            : applyRuntimeFog
                ? "ligado (snapshot confirmado)"
                : "desligado";
        string teamVisibility = preMovementVisibilityAvailable
            ? knowledgeSnapshotFromRuntime
                ? "snapshot confirmado do slot"
                : !Application.isPlaying && recalculateExperimentalKnowledge
                    ? "fotografia experimental temporaria"
                    : "bake manual da rodada 0"
            : "indisponível";
        return "Filtros desta consulta: "
            + $"FOW {fow} | "
            + $"conhecimento de alvos {teamVisibility} | "
            + $"LdT {(enableLdt ? "ligada" : "desligada")} | "
            + $"LoS {(enableLos ? "ligada" : "desligada")} | "
            + $"spotter {(enableSpotter ? "exigido" : "não exigido")} | "
            + $"stealth {(enableStealth ? "validado" : "ignorado")}.";
    }

    private string BuildRejectionExplanation(PodeMirarInvalidOption invalid)
    {
        string target = ResolveUnitLabel(invalid.targetUnit);
        string weapon = ResolveWeaponLabel(invalid.weapon);
        string weaponSlot = invalid.embarkedWeaponIndex >= 0
            ? $" W{invalid.embarkedWeaponIndex + 1}"
            : string.Empty;
        string subject = $"{target} com {weapon}{weaponSlot}";

        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdLosBlocked)
        {
            string blockedCell = invalid.blockedCell != Vector3Int.zero
                ? $" no hex {invalid.blockedCell.x},{invalid.blockedCell.y}"
                : string.Empty;
            return $"Validar LoS ligado: {subject} foi descartado porque a "
                + $"linha de visada do tiro reto está bloqueada{blockedCell}. "
                + "Spotter não permite atravessar esse bloqueio físico.";
        }

        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdLdtBlocked)
        {
            return $"Validar LdT ligado: {subject} foi descartado porque a "
                + "trajetória não é válida nos domínios/hexes atravessados.";
        }

        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdNoForwardObserver)
        {
            return $"Exigir spotter ligado: {subject} foi descartado porque o "
                + "atacante não confirmou o alvo sozinho e nenhum observador "
                + "avançado válido o confirmou.";
        }

        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdStealth)
        {
            return $"Validar stealth ligado: {subject} foi descartado porque "
                + "o alvo não foi detectado pelas regras aplicadas ao ataque.";
        }

        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdOutOfRange)
            return $"Alcance: {subject} foi descartado. {invalid.reason}";

        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdNoAmmo)
            return $"Munição: {subject} foi descartado porque a arma está vazia.";

        return $"{subject} foi descartado: {invalid.reason}";
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (result == null || resolvedMap == null)
            return;

        DrawCells(orderedStationaryCells);
        DrawCells(orderedMobileCells);
        DrawSimulatedVisibilityRejections();
        DrawSelectedCombatLine();
        if (drawSelectedSensorRejections)
            DrawSelectedRejections();
        HandleCellClick();
    }

    private void DrawSimulatedVisibilityRejections()
    {
        foreach (UnitManager target in simulatedVisibilityRejections)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                continue;

            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            Vector3 world = resolvedMap.GetCellCenterWorld(targetCell);
            Handles.color = new Color(0.62f, 0.28f, 0.72f, 0.92f);
            Handles.DrawWireDisc(world, Vector3.back, 0.31f);
            Handles.Label(
                world + new Vector3(0f, 0.24f, 0f),
                "fora do conhecimento do slot",
                ScoreLabelStyle(new Color(0.92f, 0.72f, 1f)));
        }
    }

    private void DrawCells(List<MelhorCombateCellResult> cells)
    {
        if (cells == null)
            return;
        for (int i = cells.Count - 1; i >= 0; i--)
        {
            MelhorCombateCellResult cell = cells[i];
            if (cell == null || (!drawEmptyOrigins && cell.Best == null))
                continue;

            bool selected = cell == selectedCell;
            bool champion = cell.Best != null && cell.Best == result.Best;
            Vector3 world = resolvedMap.GetCellCenterWorld(cell.Cell);
            Color color = ResolveCellColor(cell, champion);
            Handles.color = color;
            float radius = champion ? 0.34f : selected ? 0.30f : cell.Best != null ? 0.23f : 0.13f;
            if (cell.Best != null)
                Handles.DrawSolidDisc(world, Vector3.back, radius);
            else
                Handles.DrawWireDisc(world, Vector3.back, radius);

            if (cell.Mode == MelhorCombateCandidateMode.Stationary)
            {
                Handles.color = new Color(0.25f, 0.65f, 1f, 0.95f);
                Handles.DrawWireDisc(world, Vector3.back, radius + 0.07f);
            }
            if (selected)
            {
                Handles.color = Color.white;
                Handles.DrawWireDisc(world, Vector3.back, radius + 0.04f);
            }

            string label = cell.Best != null
                ? $"#{ResolveCellRank(cell)}\n{BuildSceneScore(cell.Best)}"
                : "–";
            Handles.Label(
                world,
                label,
                ScoreLabelStyle(champion ? Color.black : Color.white));
        }
    }

    private void DrawSelectedCombatLine()
    {
        MelhorCombateCandidate candidate = selectedCandidate
            ?? selectedCell?.Best
            ?? result?.Best;
        if (candidate?.Target == null)
            return;

        Vector3 from = resolvedMap.GetCellCenterWorld(candidate.FromCell);
        Vector3Int targetCell = candidate.Target.CurrentCellPosition;
        targetCell.z = 0;
        Vector3 to = resolvedMap.GetCellCenterWorld(targetCell);
        Handles.color = ResolveAdmissionColor(candidate.RankKey.Admission);
        Handles.DrawAAPolyLine(4f, from, to);
        Handles.DrawWireDisc(to, Vector3.back, 0.32f);
        Handles.Label(
            Vector3.Lerp(from, to, 0.55f),
            $"{ResolveWeaponLabel(candidate.SensorOption?.weapon)}\n{BuildMilitarySummary(candidate)}",
            ScoreLabelStyle(Color.white));

        DrawSelectedDetectionObserverLines(candidate.SensorOption, to);
        DrawSelectedSpotterLines(candidate.SensorOption, to);
    }

    private void DrawSelectedDetectionObserverLines(
        PodeMirarTargetOption option,
        Vector3 targetWorld)
    {
        if (option?.targetUnit == null
            || !simulatedObserversByTarget.TryGetValue(
                option.targetUnit,
                out List<UnitManager> observers))
        {
            return;
        }

        for (int i = 0; i < observers.Count; i++)
        {
            UnitManager observer = observers[i];
            if (observer == null || !observer.gameObject.activeInHierarchy)
                continue;
            if (IsFireSpotterObserver(option, observer))
                continue;

            Vector3Int observerCell = observer.CurrentCellPosition;
            observerCell.z = 0;
            Vector3 observerWorld = resolvedMap.GetCellCenterWorld(observerCell);
            if (Vector3.Distance(observerWorld, targetWorld) <= 0.0001f)
                continue;

            Handles.color = new Color(0.1f, 0.9f, 1f, 0.95f);
            Handles.DrawDottedLine(observerWorld, targetWorld, 5f);
            Handles.SphereHandleCap(
                0,
                observerWorld,
                Quaternion.identity,
                0.09f,
                EventType.Repaint);
            Handles.Label(
                Vector3.Lerp(observerWorld, targetWorld, 0.42f)
                    + new Vector3(-0.08f, 0.08f, 0f),
                $"DETECTOU: {ResolveUnitLabel(observer)}",
                ScoreLabelStyle(new Color(0.3f, 0.95f, 1f)));
        }
    }

    private static bool IsFireSpotterObserver(
        PodeMirarTargetOption option,
        UnitManager observer)
    {
        if (option == null || observer == null || !option.usedForwardObserver)
            return false;
        if (observer == option.forwardObserverUnit)
            return true;
        return option.forwardObserverCandidates != null
            && option.forwardObserverCandidates.Contains(observer);
    }

    private void DrawSelectedSpotterLines(
        PodeMirarTargetOption option,
        Vector3 targetWorld)
    {
        if (!enableSpotter || option == null || !option.usedForwardObserver)
            return;

        var observers = new HashSet<UnitManager>();
        if (option.forwardObserverCandidates != null)
        {
            for (int i = 0; i < option.forwardObserverCandidates.Count; i++)
            {
                UnitManager candidate = option.forwardObserverCandidates[i];
                if (candidate != null && candidate.gameObject.activeInHierarchy)
                    observers.Add(candidate);
            }
        }

        if (option.forwardObserverUnit != null
            && option.forwardObserverUnit.gameObject.activeInHierarchy)
        {
            observers.Add(option.forwardObserverUnit);
        }

        Handles.color = new Color(1f, 0.8f, 0.1f, 1f);
        foreach (UnitManager observer in observers)
        {
            Vector3Int observerCell = observer.CurrentCellPosition;
            observerCell.z = 0;
            Vector3 observerWorld = resolvedMap.GetCellCenterWorld(observerCell);
            if (Vector3.Distance(observerWorld, targetWorld) <= 0.0001f)
                continue;

            Handles.DrawDottedLine(observerWorld, targetWorld, 4f);
            Handles.SphereHandleCap(
                0,
                observerWorld,
                Quaternion.identity,
                0.10f,
                EventType.Repaint);

            bool selectedBySensor = observer == option.forwardObserverUnit;
            string prefix = selectedBySensor ? "SPOTTER" : "OBS";
            Handles.Label(
                Vector3.Lerp(observerWorld, targetWorld, 0.5f)
                    + new Vector3(0.08f, -0.08f, 0f),
                $"{prefix}: {ResolveUnitLabel(observer)}",
                ScoreLabelStyle(new Color(1f, 0.85f, 0.2f)));
        }
    }

    private void DrawSelectedRejections()
    {
        if (selectedCell == null)
            return;
        int drawn = 0;
        for (int i = 0; i < result.SensorRejections.Count && drawn < 30; i++)
        {
            MelhorCombateSensorRejection rejection = result.SensorRejections[i];
            PodeMirarInvalidOption invalid = rejection?.InvalidOption;
            if (invalid?.targetUnit == null
                || rejection.Mode != selectedCell.Mode
                || rejection.FromCell != selectedCell.Cell)
            {
                continue;
            }

            Vector3Int targetCell = invalid.targetUnit.CurrentCellPosition;
            targetCell.z = 0;
            Vector3 world = resolvedMap.GetCellCenterWorld(targetCell);
            Handles.color = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            Handles.DrawWireDisc(world, Vector3.back, 0.19f + drawn * 0.002f);
            if (drawn < 8)
            {
                Handles.Label(
                    world,
                    ShortReason(invalid),
                    ScoreLabelStyle(new Color(0.85f, 0.85f, 0.85f)));
            }
            drawn++;
        }
    }

    private void HandleCellClick()
    {
        Event evt = Event.current;
        if (evt.type != EventType.MouseDown || evt.button != 0 || evt.alt)
            return;

        MelhorCombateCellResult nearest = null;
        float nearestPixels = 18f;
        FindNearestCell(result.StationaryCells, evt.mousePosition, ref nearest, ref nearestPixels);
        FindNearestCell(result.MobileCells, evt.mousePosition, ref nearest, ref nearestPixels);
        if (nearest == null)
            return;

        selectedCell = nearest;
        selectedCandidate = nearest.Best;
        evt.Use();
        Repaint();
        SceneView.RepaintAll();
    }

    private void FindNearestCell(
        List<MelhorCombateCellResult> cells,
        Vector2 mouse,
        ref MelhorCombateCellResult nearest,
        ref float nearestPixels)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            MelhorCombateCellResult cell = cells[i];
            if (cell == null || (!drawEmptyOrigins && cell.Best == null))
                continue;
            Vector2 gui = HandleUtility.WorldToGUIPoint(
                resolvedMap.GetCellCenterWorld(cell.Cell));
            float distance = Vector2.Distance(mouse, gui);
            if (distance >= nearestPixels)
                continue;
            nearestPixels = distance;
            nearest = cell;
        }
    }

    private void SelectInitialResult()
    {
        if (result == null)
            return;
        selectedCandidate = result.Best;
        selectedCell = FindCellForCandidate(result.StationaryCells, selectedCandidate)
            ?? FindCellForCandidate(result.MobileCells, selectedCandidate)
            ?? FirstCellWithCombat(result.StationaryCells)
            ?? FirstCellWithCombat(result.MobileCells)
            ?? FirstCell(result.StationaryCells)
            ?? FirstCell(result.MobileCells);
        if (selectedCandidate == null && selectedCell != null)
            selectedCandidate = selectedCell.Best;
    }

    private int CountAdmission(CombatAdmissionTier tier)
    {
        if (result == null)
            return 0;
        int count = 0;
        CountAdmission(result.StationaryRanking, tier, ref count);
        CountAdmission(result.MobileRanking, tier, ref count);
        return count;
    }

    private static void CountAdmission(
        List<MelhorCombateCandidate> ranking,
        CombatAdmissionTier tier,
        ref int count)
    {
        for (int i = 0; i < ranking.Count; i++)
        {
            if (ranking[i] != null && ranking[i].RankKey.Admission == tier)
                count++;
        }
    }

    private int ResolveCellRank(MelhorCombateCellResult wanted)
    {
        return wanted != null && rankByCell.TryGetValue(wanted, out int rank)
            ? rank
            : 0;
    }

    private void RebuildCellRanks()
    {
        orderedStationaryCells.Clear();
        orderedMobileCells.Clear();
        rankByCell.Clear();
        if (result == null)
            return;

        orderedStationaryCells.AddRange(result.StationaryCells);
        orderedMobileCells.AddRange(result.MobileCells);
        orderedStationaryCells.Sort(CompareCellsByBest);
        orderedMobileCells.Sort(CompareCellsByBest);
        RegisterRanks(orderedStationaryCells);
        RegisterRanks(orderedMobileCells);
    }

    private void RegisterRanks(List<MelhorCombateCellResult> ordered)
    {
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i] != null)
                rankByCell[ordered[i]] = i + 1;
        }
    }

    private static int CompareCellsByBest(
        MelhorCombateCellResult a,
        MelhorCombateCellResult b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;
        int combat = MelhorCombateService.CompareCandidates(a.Best, b.Best);
        if (combat != 0)
            return combat;
        int y = a.Cell.y.CompareTo(b.Cell.y);
        return y != 0 ? y : a.Cell.x.CompareTo(b.Cell.x);
    }

    private string BuildCellLabel(
        int rank,
        MelhorCombateCellResult cell,
        MelhorCombateCandidate best,
        bool overallBest)
    {
        string prefix = overallBest ? "★ " : string.Empty;
        if (best == null)
        {
            return $"{prefix}#{rank} {cell.Cell.x},{cell.Cell.y} | "
                + $"MP {cell.MovementCost} | sem combate";
        }

        return $"{prefix}#{rank} {cell.Cell.x},{cell.Cell.y} | "
            + $"{ResolveAdmissionTag(best.RankKey.Admission)} | "
            + $"{ResolveUnitLabel(best.Target)} | "
            + $"{ResolveWeaponLabel(best.SensorOption?.weapon)} | "
            + $"{BuildMilitarySummary(best)} | MP {cell.MovementCost}";
    }

    private static string BuildMilitarySummary(MelhorCombateCandidate candidate)
    {
        if (candidate == null || !candidate.Evaluation.HasSimulation)
            return "sim?";
        return $"{(candidate.RankKey.KillGuaranteed ? "K" : "-")}/"
            + $"{(candidate.RankKey.AttackerSurvives ? "S" : "X")} "
            + $"dano {candidate.RankKey.TargetDamagePercent}% "
            + $"perda {candidate.RankKey.AttackerLossPercent}% "
            + $"Δ{candidate.RankKey.TradeBalancePercent:+#;-#;0}";
    }

    private static string BuildSceneScore(MelhorCombateCandidate candidate)
    {
        if (candidate == null)
            return "–";
        if (!candidate.Evaluation.HasSimulation)
            return "SIM?";
        return $"{ResolveAdmissionShort(candidate.RankKey.Admission)} "
            + $"{(candidate.RankKey.KillGuaranteed ? "K" : "-")}"
            + $"{(candidate.RankKey.AttackerSurvives ? "S" : "X")} "
            + $"Δ{candidate.RankKey.TradeBalancePercent:+#;-#;0}";
    }

    private Color ResolveCellColor(MelhorCombateCellResult cell, bool champion)
    {
        if (champion)
            return new Color(1f, 0.72f, 0.05f, 0.97f);
        if (cell.Best == null)
            return new Color(0.48f, 0.48f, 0.48f, 0.65f);
        return ResolveAdmissionColor(cell.Best.RankKey.Admission);
    }

    private static Color ResolveAdmissionColor(CombatAdmissionTier admission)
    {
        switch (admission)
        {
            case CombatAdmissionTier.Allowed:
                return new Color(0.12f, 0.82f, 0.28f, 0.92f);
            case CombatAdmissionTier.Blocked:
                return new Color(0.92f, 0.16f, 0.12f, 0.92f);
            default:
                return new Color(0.85f, 0.18f, 0.92f, 0.90f);
        }
    }

    private static string ResolveAdmissionTag(CombatAdmissionTier admission)
    {
        switch (admission)
        {
            case CombatAdmissionTier.Allowed:
                return "ADMITIDO";
            case CombatAdmissionTier.Blocked:
                return "BLOQUEADO";
            default:
                return "INDISPONÍVEL";
        }
    }

    private static string ResolveAdmissionShort(CombatAdmissionTier admission)
    {
        switch (admission)
        {
            case CombatAdmissionTier.Allowed:
                return "A";
            case CombatAdmissionTier.Blocked:
                return "B";
            default:
                return "?";
        }
    }

    private string ResolveDetectionObserversLabel(UnitManager target)
    {
        if (target != null
            && simulatedObserversByTarget.TryGetValue(
                target,
                out List<UnitManager> observers)
            && observers.Count > 0)
        {
            var labels = new List<string>(observers.Count);
            for (int i = 0; i < observers.Count; i++)
            {
                UnitManager observer = observers[i];
                if (observer != null)
                    labels.Add(ResolveUnitLabel(observer));
            }
            if (labels.Count > 0)
                return string.Join(", ", labels);
        }

        if (Application.isPlaying && applyRuntimeFog)
            return "FOW confirmado — fonte não reconstruída";
        if (target != null && constructionDetectedTargets.Contains(target))
            return "construção aliada no próprio hex";
        if (target != null && simulatedSelfDetectedTargets.Contains(target))
            return "visão própria — nenhum aliado adicional";
        return "não registrado";
    }

    private static string ResolveUnitLabel(UnitManager manager)
    {
        if (manager == null)
            return "?";
        if (!string.IsNullOrWhiteSpace(manager.UnitDisplayName))
            return manager.UnitDisplayName;
        if (!string.IsNullOrWhiteSpace(manager.UnitId))
            return manager.UnitId;
        return manager.name;
    }

    private static string ResolveWeaponLabel(WeaponData weapon)
    {
        if (weapon == null)
            return "sem arma";
        return !string.IsNullOrWhiteSpace(weapon.displayName)
            ? weapon.displayName
            : weapon.name;
    }

    private static string ResolveCandidateModeLabel(MelhorCombateCandidateMode value)
    {
        return value == MelhorCombateCandidateMode.Stationary
            ? "Parado"
            : "Mover e atacar";
    }

    private static string ShortReason(PodeMirarInvalidOption invalid)
    {
        if (invalid == null)
            return "?";
        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdNoAmmo)
            return "sem munição";
        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdOutOfRange)
            return "fora do alcance";
        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdLosBlocked)
            return "LoS bloqueada";
        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdLdtBlocked)
            return "LdT bloqueada";
        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdNoForwardObserver)
            return "sem spotter";
        if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdStealth)
            return "não detectado";
        return string.IsNullOrWhiteSpace(invalid.reason)
            ? "inválido"
            : invalid.reason.Length <= 12
                ? invalid.reason
                : invalid.reason.Substring(0, 12);
    }

    private static bool TryGetWeaponAmmo(UnitManager owner, int index, out int ammo)
    {
        ammo = 0;
        if (owner == null || index < 0)
            return false;
        IReadOnlyList<UnitEmbarkedWeapon> weapons = owner.GetEmbarkedWeapons();
        if (weapons == null || index >= weapons.Count || weapons[index] == null)
            return false;
        ammo = weapons[index].squadAmmunition;
        return true;
    }

    private void AutoDetectContext()
    {
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (turnStateManager != null)
        {
            if (terrainDatabase == null)
                terrainDatabase = turnStateManager.TerrainDatabaseRef;
            if (rpsDatabase == null)
                rpsDatabase = turnStateManager.RpsDatabaseRef;
            if (dpqMatchupDatabase == null)
                dpqMatchupDatabase = turnStateManager.DpqMatchupDatabaseRef;
            if (weaponPriorityData == null)
                weaponPriorityData = turnStateManager.WeaponPriorityDataRef;
            if (dpqAirHeightConfig == null)
                dpqAirHeightConfig = turnStateManager.DpqAirHeightConfigRef;
        }

        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();
        if (rpsDatabase == null)
            rpsDatabase = FindFirstAsset<RPSDatabase>();
        if (dpqMatchupDatabase == null)
            dpqMatchupDatabase = FindFirstAsset<DPQMatchupDatabase>();
        if (weaponPriorityData == null)
            weaponPriorityData = FindFirstAsset<WeaponPriorityData>();
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAsset<DPQAirHeightConfig>();
        ResolveMap();
    }

    private void TryUseSelection(bool silent)
    {
        GameObject selectedObject = Selection.activeGameObject;
        UnitManager found = selectedObject != null
            ? selectedObject.GetComponent<UnitManager>()
            : null;
        if (found == null && selectedObject != null)
            found = selectedObject.GetComponentInParent<UnitManager>();
        if (found == null)
        {
            if (!silent)
                status = "O objeto selecionado não possui UnitManager.";
            return;
        }

        if (unit == found)
            return;
        unit = found;
        if (unit.BoardTilemap != null)
            overrideMap = unit.BoardTilemap;
        ResolveDefaultMobileSubStepForUnit();
        ClearResult();
        status = $"Unidade: {ResolveUnitLabel(unit)}.";
    }

    private Tilemap ResolveMap()
    {
        if (overrideMap != null)
        {
            resolvedMap = overrideMap;
            return resolvedMap;
        }
        if (unit != null && unit.BoardTilemap != null)
        {
            resolvedMap = unit.BoardTilemap;
            return resolvedMap;
        }
        resolvedMap = FindPreferredTilemap();
        return resolvedMap;
    }

    private void ResolveDefaultMobileSubStepForUnit()
    {
        List<ReachSubStep> supported =
            UnitReachEnvelopeService.GetSubSteps(ReachIntent.Combat, unit);
        supported.Remove(ReachSubStep.Artilheiro);
        if (supported.Count > 0)
            mobileSubStep = ResolveDefaultMobileSubStep(supported);
    }

    private ReachSubStep ResolveDefaultMobileSubStep(List<ReachSubStep> supported)
    {
        ReachSubStep preferred = unit != null
            && AIActionReachCoordinator.UsesCubicSectorReach(unit)
            ? ReachSubStep.Aereo
            : ReachSubStep.Terrestre;
        return supported.Contains(preferred) ? preferred : supported[0];
    }

    private void ClearResult()
    {
        result = null;
        selectedCell = null;
        selectedCandidate = null;
        orderedStationaryCells.Clear();
        orderedMobileCells.Clear();
        rankByCell.Clear();
        simulatedDetectedTargets.Clear();
        simulatedSelfDetectedTargets.Clear();
        simulatedVisibilityRejections.Clear();
        simulatedObserversByTarget.Clear();
        perceptionContributorsByTarget.Clear();
        constructionDetectedTargets.Clear();
        preMovementVisibilityAvailable = false;
        SceneView.RepaintAll();
    }

    private void InvalidateKnowledgeSnapshot()
    {
        knowledgeSnapshot = null;
        knowledgeSnapshotFromRuntime = false;
        knowledgeSnapshotDiagnostic = string.Empty;
        ClearResult();
    }

    private GUIStyle WrappedStyle()
    {
        if (wrappedStyle == null)
        {
            wrappedStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true
            };
        }
        wrappedStyle.fontSize = Mathf.Clamp(listFontSize, 9, 20);
        return wrappedStyle;
    }

    private static MelhorCombateMode ResolveServiceMode(QueryMode value)
    {
        switch (value)
        {
            case QueryMode.Parado:
                return MelhorCombateMode.Stationary;
            case QueryMode.MoverEAtacar:
                return MelhorCombateMode.MoveAndAttack;
            case QueryMode.Hibrido:
                return MelhorCombateMode.Hybrid;
            default:
                return MelhorCombateMode.AutoFromUnitData;
        }
    }

    private static bool IncludesMobile(QueryMode value) =>
        value != QueryMode.Parado;

    private static string ResolveSubStepLabel(ReachSubStep value)
    {
        switch (value)
        {
            case ReachSubStep.Aereo:
                return "Aéreo — distância cúbica";
            case ReachSubStep.Terrestre:
                return "Terrestre — caminhos e custos";
            default:
                return value.ToString();
        }
    }

    private static MelhorCombateCellResult FindCellForCandidate(
        List<MelhorCombateCellResult> cells,
        MelhorCombateCandidate candidate)
    {
        if (candidate == null)
            return null;
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null && cells[i].Candidates.Contains(candidate))
                return cells[i];
        }
        return null;
    }

    private static MelhorCombateCellResult FirstCellWithCombat(
        List<MelhorCombateCellResult> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i]?.Best != null)
                return cells[i];
        }
        return null;
    }

    private static MelhorCombateCellResult FirstCell(
        List<MelhorCombateCellResult> cells) =>
        cells != null && cells.Count > 0 ? cells[0] : null;

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        if (maps == null || maps.Length == 0)
            return null;
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null
                && string.Equals(maps[i].name, "TileMap", StringComparison.OrdinalIgnoreCase))
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
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = color }
        };
}
