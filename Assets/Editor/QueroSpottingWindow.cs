using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// RASCUNHO do QueroSpotting — o lado de quem PEDE olho. É o par do Melhor
/// Spotting (docs/implementar_melhor_spotting.md, ainda não implementado),
/// como o QueroCarona é par do Melhor Embarque:
///
///   QueroCarona    ↔  MelhorEmbarque     "preciso ir"      ↔ "eu levo"
///   QueroSpotting  ↔  MelhorSpotting     "preciso de olho" ↔ "eu ilumino"
///
/// A pergunta desta tela: "a faixa de fogo desta peça está cega na frente
/// dela?"
///
///   faixa     = Tactical da ARMA (UnitReachEnvelopeService, subetapa
///               Artilheiro): os anéis que alguma arma cobre, sem zona morta
///               nem o próprio hex
///   vanguarda = faixa no cone estreito apontado para a âncora
///   flancos   = faixa entre o cone da vanguarda e o cone dos flancos
///   retaguarda= o resto da faixa; fica fora do papel
///   cega      = sem cobertura de sensor do slot
///
/// SÓ A VANGUARDA PENDURA O PAPEL. Flanco cego entra com peso menor
/// (ObjectiveWeights do Melhor Spotting), mas sozinho não gera pedido: senão o
/// spotter que acende seis células de campo aberto no flanco "cumpre" o papel
/// e a massa atrás da serra continua escura. A pergunta "vem alguém me pegar
/// pelo lado?" é de segurança, com outra faixa (operacional), e não é esta.
///
/// A ÂNCORA É O PONTO EM DISCUSSÃO. "Honesta" usa só o que o slot pode saber:
/// contatos que ele detecta agora, senão o HQ inimigo, senão os prédios
/// capturáveis não aliados — geografia que o jogador viu antes do mapa
/// escurecer. "Massa real" é a trapaça da janela Retaguarda: média da posição
/// verdadeira de todo inimigo. As duas ficam lado a lado de propósito: se só a
/// massa real acha o alvo, o papel estava apontando para onde a IA não podia
/// saber.
///
/// COBERTURA É PROXY. Usa SensorCoveredCells da fotografia de FOW (visão das
/// unidades do slot + o próprio hex das construções dele). Para alvo comum,
/// ser visto é ser detectado; contra furtivo não. A cobertura de DETECÇÃO por
/// célula ainda não existe (degrau 1 da escada).
///
/// Não move nada, não altera FOW, não toca no bake da rodada 0. Regra, âncora
/// e cone ainda mudam; só sobem para serviço quando pararem de mudar.
/// </summary>
public sealed class QueroSpottingWindow : EditorWindow
{
    private enum AnchorSource
    {
        Honesta = 0,
        MassaReal = 1,
        Manual = 2
    }

    private enum KnowledgeSource
    {
        FotografiaTemporaria = 0,
        BakeDaRodadaZero = 1
    }

    private sealed class Evaluation
    {
        public Vector3Int Origin;
        public Vector3Int Anchor;
        public string AnchorLabel;
        public float FlankWeight;
        public readonly HashSet<Vector3Int> Band = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> Vanguard = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> Flank = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> BlindVanguard = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> CoveredVanguard = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> BlindFlank = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> CoveredFlank = new HashSet<Vector3Int>();
        public int KnownContacts;
        public string KnowledgeLabel;

        public bool HasCheatComparison;
        public Vector3Int CheatAnchor;
        public string CheatAnchorLabel;
        public readonly HashSet<Vector3Int> CheatBlindVanguard = new HashSet<Vector3Int>();

        public bool HangsPaper => BlindVanguard.Count > 0;

        public float Urgency => Vanguard.Count > 0
            ? (float)BlindVanguard.Count / Vanguard.Count
            : 0f;

        /// <summary>O que o Melhor Spotting receberia como peso total do papel.</summary>
        public float WeightedBlind => BlindVanguard.Count + BlindFlank.Count * FlankWeight;
    }

    // Cores só desta tela, nomeadas na legenda. Nenhuma herda significado de
    // outra janela: verde/azul/vermelho são do Hotzone e ficam de fora.
    // Cheio = vanguarda; vazado = flanco (a mesma coisa, valendo menos).
    private static readonly Color RearColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
    private static readonly Color CoveredColor = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color BlindColor = new Color(1f, 0.55f, 0.05f, 0.9f);
    private static readonly Color AnchorColor = new Color(1f, 0.92f, 0.2f, 1f);
    private static readonly Color CheatAnchorColor = new Color(0.85f, 0.2f, 0.85f, 1f);

    [SerializeField] private UnitManager unit;
    [SerializeField] private Tilemap overrideMap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private AnchorSource anchorSource = AnchorSource.Honesta;
    [SerializeField] private Vector3Int manualAnchor;
    [SerializeField] private KnowledgeSource knowledgeSource = KnowledgeSource.FotografiaTemporaria;
    [SerializeField] private bool enableLos = true;
    [SerializeField] private float vanguardHalfAngle = 50f;
    [SerializeField] private float flankHalfAngle = 90f;
    [SerializeField] private float flankWeight = 0.3f;
    [SerializeField] private bool compareWithRealMass = true;
    [SerializeField] private bool showRear = true;
    [SerializeField] private bool showCovered = true;
    [SerializeField] private bool showBlindVanguard = true;
    [SerializeField] private bool showBlindFlank = true;

    private Tilemap resolvedMap;
    private Evaluation result;
    private bool pickingAnchor;
    private bool blindListExpanded;
    private Vector2 scroll;
    private string status = "Selecione uma peça de fogo em campo.";

    [MenuItem("Tools/Hotzone/Quero Spotting")]
    public static void Open() =>
        GetWindow<QueroSpottingWindow>("Quero Spotting").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetectContext();
    }

    private void OnDisable() =>
        SceneView.duringSceneGui -= OnSceneGUI;

    private void OnSelectionChange() => Repaint();

    // ------------------------------------------------------------------
    // GUI
    // ------------------------------------------------------------------

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Quero Spotting", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Rascunho. Pergunta se a faixa de fogo da peça está cega na frente "
            + "dela. Faixa = Tactical da arma; frente = faixa no cone da âncora; "
            + "cega = frente sem cobertura do slot. A cega é o papel que iria "
            + "para o quadro de missões. Não move nada nem altera FOW ou bake.",
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

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Âncora da frente", EditorStyles.boldLabel);
        anchorSource = (AnchorSource)EditorGUILayout.EnumPopup(
            new GUIContent(
                "Fonte",
                "Honesta: contatos detectados agora, senão HQ inimigo, senão "
                + "prédios capturáveis não aliados. Massa real: posição verdadeira "
                + "de todo inimigo (trapaça da Retaguarda). Manual: hex clicado."),
            anchorSource);
        if (anchorSource == AnchorSource.Manual)
        {
            EditorGUILayout.BeginHorizontal();
            manualAnchor = EditorGUILayout.Vector3IntField("Hex", manualAnchor);
            GUI.backgroundColor = pickingAnchor ? Color.red : Color.white;
            if (GUILayout.Button(pickingAnchor ? "X" : "<", GUILayout.Width(28f)))
            {
                pickingAnchor = !pickingAnchor;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        vanguardHalfAngle = EditorGUILayout.Slider(
            new GUIContent(
                "Vanguarda — meia abertura (°)",
                "Cone estreito apontado para a âncora. Só ele pendura o papel."),
            vanguardHalfAngle, 10f, 180f);
        flankHalfAngle = EditorGUILayout.Slider(
            new GUIContent(
                "Flancos — até (°)",
                "Do fim da vanguarda até aqui é flanco. 90 = meio-plano. "
                + "O que passa daqui é retaguarda e fica fora do papel."),
            Mathf.Max(flankHalfAngle, vanguardHalfAngle), vanguardHalfAngle, 180f);
        flankWeight = EditorGUILayout.Slider(
            new GUIContent(
                "Peso do flanco",
                "Quanto uma célula de flanco cega vale no papel, contra 1 da "
                + "vanguarda. Vira ObjectiveWeights do Melhor Spotting."),
            flankWeight, 0f, 1f);
        using (new EditorGUI.DisabledScope(anchorSource == AnchorSource.MassaReal))
        {
            compareWithRealMass = EditorGUILayout.ToggleLeft(
                "Comparar com a massa real (trapaça)", compareWithRealMass);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Conhecimento do slot", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            knowledgeSource = (KnowledgeSource)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Fonte (Edit Mode)",
                    "Fotografia temporária: cozinha agora, a partir da cena, sem "
                    + "gravar nada. Bake da rodada 0: lê o que o botão Cozinhar FOW 0 "
                    + "das outras janelas gravou. No Play Mode é sempre o FOW "
                    + "confirmado do slot."),
                knowledgeSource);
            enableLos = EditorGUILayout.ToggleLeft("Validar linha de visão", enableLos);
        }
        if (EditorGUI.EndChangeCheck())
            ClearResult();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Camadas", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        showBlindVanguard = EditorGUILayout.ToggleLeft("Laranja cheio — vanguarda cega (pendura o papel)", showBlindVanguard);
        showBlindFlank = EditorGUILayout.ToggleLeft("Laranja vazado — flanco cego (peso menor)", showBlindFlank);
        showCovered = EditorGUILayout.ToggleLeft("Branco — coberta (cheio vanguarda, vazado flanco)", showCovered);
        showRear = EditorGUILayout.ToggleLeft("Cinza — retaguarda da faixa (fora do papel)", showRear);
        EditorGUILayout.LabelField(
            " ",
            "Linha amarela: âncora usada. Linha lilás: massa real, para comparar.",
            EditorStyles.miniLabel);
        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        EditorGUILayout.EndHorizontal();
        using (new EditorGUI.DisabledScope(unit == null))
        {
            if (GUILayout.Button("Avaliar se Quer Spotting", GUILayout.Height(30f)))
                Evaluate();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(status, MessageType.None);
        DrawResult();
        EditorGUILayout.EndScrollView();
    }

    private void DrawResult()
    {
        if (result == null)
            return;

        EditorGUILayout.HelpBox(
            result.HangsPaper
                ? $"SIM — PENDURA O PAPEL: {result.BlindVanguard.Count} célula(s) cega(s) na vanguarda"
                : result.BlindFlank.Count > 0
                    ? $"NÃO — vanguarda coberta; {result.BlindFlank.Count} de flanco cegas não bastam"
                    : "NÃO — vanguarda e flancos cobertos",
            result.HangsPaper ? MessageType.Warning : MessageType.Info);

        EditorGUILayout.LabelField("Origem", Format(result.Origin));
        EditorGUILayout.LabelField("Âncora", $"{Format(result.Anchor)} — {result.AnchorLabel}");
        EditorGUILayout.LabelField("Conhecimento", result.KnowledgeLabel, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Contatos detectados", result.KnownContacts.ToString());
        EditorGUILayout.LabelField("Faixa da arma", $"{result.Band.Count} células");
        EditorGUILayout.LabelField(
            "Vanguarda",
            $"{result.Vanguard.Count} — coberta {result.CoveredVanguard.Count}, cega {result.BlindVanguard.Count}");
        EditorGUILayout.LabelField(
            "Flancos",
            $"{result.Flank.Count} — coberta {result.CoveredFlank.Count}, cega {result.BlindFlank.Count}");
        EditorGUILayout.LabelField(
            "Retaguarda (fora)",
            $"{result.Band.Count - result.Vanguard.Count - result.Flank.Count} células");
        EditorGUILayout.LabelField("Urgência (cega / vanguarda)", $"{result.Urgency:P0}");
        EditorGUILayout.LabelField(
            "Peso do papel",
            $"{result.WeightedBlind:0.#} = {result.BlindVanguard.Count} × 1 + {result.BlindFlank.Count} × {result.FlankWeight:0.##}");

        if (result.HasCheatComparison)
        {
            int shared = 0;
            foreach (Vector3Int cell in result.BlindVanguard)
                if (result.CheatBlindVanguard.Contains(cell))
                    shared++;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Comparação com a massa real (vanguarda)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Âncora trapaceira", $"{Format(result.CheatAnchor)} — {result.CheatAnchorLabel}");
            EditorGUILayout.LabelField("Vanguarda cega pela massa real", $"{result.CheatBlindVanguard.Count} células");
            EditorGUILayout.LabelField(
                "Em comum",
                $"{shared} — só honesta {result.BlindVanguard.Count - shared}, só trapaça {result.CheatBlindVanguard.Count - shared}");
        }

        if (result.BlindVanguard.Count + result.BlindFlank.Count == 0)
            return;
        blindListExpanded = EditorGUILayout.Foldout(
            blindListExpanded, "Células do papel (ObjectiveCells e peso)", true);
        if (!blindListExpanded)
            return;
        EditorGUI.indentLevel++;
        DrawCellList(result.BlindVanguard, "vanguarda, peso 1");
        DrawCellList(result.BlindFlank, $"flanco, peso {result.FlankWeight:0.##}");
        EditorGUI.indentLevel--;
    }

    private void DrawCellList(HashSet<Vector3Int> cells, string label)
    {
        var ordered = new List<Vector3Int>(cells);
        ordered.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        foreach (Vector3Int cell in ordered)
            EditorGUILayout.LabelField(
                Format(cell),
                $"{label} — distância {AIActionReachCoordinator.CubicDistance(result.Origin, cell)}");
    }

    // ------------------------------------------------------------------
    // Avaliação
    // ------------------------------------------------------------------

    private void Evaluate()
    {
        AutoDetectContext();
        result = null;
        if (unit == null)
        {
            status = "Selecione uma unidade.";
            return;
        }
        resolvedMap = overrideMap != null ? overrideMap : unit.BoardTilemap;
        if (resolvedMap == null || terrainDatabase == null)
        {
            status = "Tilemap e Terrain Database são obrigatórios.";
            return;
        }

        var evaluation = new Evaluation();
        evaluation.Origin = unit.CurrentCellPosition;
        evaluation.Origin.z = 0;

        // 1. Faixa: a mesma autoridade do Hotzone. Nada de anel recalculado aqui.
        UnitReachEnvelope envelope = UnitReachEnvelopeService.Build(new UnitReachRequest
        {
            Unit = unit,
            BoardMap = resolvedMap,
            TerrainDatabase = terrainDatabase,
            DpqAirHeightConfig = dpqAirHeightConfig,
            Intent = ReachIntent.Combat,
            Band = ReachBand.Tactical,
            SubStep = ReachSubStep.Artilheiro,
            EnableLos = enableLos
        });
        if (envelope == null)
        {
            status = "Sem faixa de arma: a unidade não tem arma com munição, ou "
                + "a subetapa Artilheiro não se aplica a ela.";
            return;
        }
        foreach (Vector3Int raw in envelope.MovementCells)
        {
            Vector3Int cell = raw;
            cell.z = 0;
            evaluation.Band.Add(cell);
        }

        // 2. Conhecimento do slot.
        FogKnowledgeSnapshot knowledge = ResolveKnowledge(out string knowledgeReason);
        if (knowledge == null)
        {
            status = "Sem fotografia de FOW do slot: " + knowledgeReason;
            return;
        }
        evaluation.KnowledgeLabel = knowledgeReason;
        evaluation.KnownContacts = knowledge.VisibleEnemyUnits.Count;

        // 3. Âncora e frente.
        if (!TryResolveAnchor(anchorSource, knowledge, out evaluation.Anchor, out evaluation.AnchorLabel))
        {
            status = "Sem âncora: " + evaluation.AnchorLabel;
            return;
        }
        evaluation.FlankWeight = flankWeight;
        if (!Classify(evaluation.Origin, evaluation.Anchor, evaluation.Band, evaluation.Vanguard, evaluation.Flank))
        {
            status = "A âncora caiu no hex da própria unidade; não há direção de frente.";
            return;
        }
        SplitByCoverage(knowledge, evaluation.Vanguard, evaluation.CoveredVanguard, evaluation.BlindVanguard);
        SplitByCoverage(knowledge, evaluation.Flank, evaluation.CoveredFlank, evaluation.BlindFlank);

        // 4. Comparação com a trapaça, na mesma faixa e no mesmo conhecimento.
        //    Só a vanguarda: é ela que decide se o papel existe.
        if (compareWithRealMass
            && anchorSource != AnchorSource.MassaReal
            && TryResolveAnchor(AnchorSource.MassaReal, knowledge, out evaluation.CheatAnchor, out evaluation.CheatAnchorLabel))
        {
            var cheatVanguard = new HashSet<Vector3Int>();
            if (Classify(evaluation.Origin, evaluation.CheatAnchor, evaluation.Band, cheatVanguard, null))
            {
                SplitByCoverage(knowledge, cheatVanguard, null, evaluation.CheatBlindVanguard);
                evaluation.HasCheatComparison = true;
            }
        }

        result = evaluation;
        status = result.HangsPaper
            ? $"{unit.name}: pendura papel com {result.BlindVanguard.Count} célula(s) cega(s) na vanguarda."
            : $"{unit.name}: vanguarda coberta, nenhum papel.";
        Repaint();
        SceneView.RepaintAll();
    }

    private FogKnowledgeSnapshot ResolveKnowledge(out string reason)
    {
        reason = string.Empty;
        PlayerSlotId slot = PlayerSlotId.FromIndex(unit.SlotIndex);
        FogKnowledgeSnapshot knowledge;

        if (Application.isPlaying)
        {
            if (matchController == null)
            {
                reason = "MatchController indisponível.";
                return null;
            }
            bool copied = matchController.TryCopyConfirmedFogKnowledgeSnapshotForSlot(
                slot, resolvedMap, out knowledge, out reason);
            reason = "FOW confirmado do slot. " + reason;
            return copied ? knowledge : null;
        }

        if (knowledgeSource == KnowledgeSource.BakeDaRodadaZero)
        {
            if (matchController == null)
            {
                reason = "MatchController indisponível para ler o bake.";
                return null;
            }
            bool copied = matchController.TryCopyRoundZeroFogKnowledgeSnapshotForSlot(
                slot, resolvedMap, out knowledge, out reason);
            reason = "bake da rodada 0. " + reason;
            return copied ? knowledge : null;
        }

        bool cooked = FogKnowledgeSnapshotBuilder.TryBuild(
            new FogKnowledgeBuildRequest
            {
                ObserverSlot = slot,
                BoardMap = resolvedMap,
                TerrainDatabase = terrainDatabase,
                DpqAirHeightConfig = dpqAirHeightConfig,
                EnableLos = enableLos,
                EnableStealth = true
            },
            out knowledge,
            out reason);
        reason = "fotografia temporária (bake intocado). " + reason;
        return cooked ? knowledge : null;
    }

    private bool TryResolveAnchor(
        AnchorSource source,
        FogKnowledgeSnapshot knowledge,
        out Vector3Int anchor,
        out string label)
    {
        anchor = default;
        var points = new List<Vector3Int>();

        switch (source)
        {
            case AnchorSource.Manual:
                anchor = manualAnchor;
                anchor.z = 0;
                label = "manual";
                return true;

            case AnchorSource.MassaReal:
                foreach (UnitManager enemy in FindObjectsByType<UnitManager>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (IsEnemyUnitOnBoard(enemy))
                        points.Add(enemy.CurrentCellPosition);
                }
                label = $"massa real ({points.Count} inimigos, inclusive os que o slot não vê)";
                break;

            default:
                // Honesta, em degraus. O primeiro que tiver ponto responde.
                foreach (UnitManager enemy in knowledge.VisibleEnemyUnits)
                {
                    if (IsEnemyUnitOnBoard(enemy))
                        points.Add(enemy.CurrentCellPosition);
                }
                if (points.Count > 0)
                {
                    label = $"contatos detectados agora ({points.Count})";
                    break;
                }

                ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (ConstructionManager construction in constructions)
                {
                    if (construction != null
                        && construction.IsPlayerHeadQuarter
                        && construction.TeamId != unit.TeamId
                        && construction.TeamId != TeamId.Neutral)
                        points.Add(construction.CurrentCellPosition);
                }
                if (points.Count > 0)
                {
                    label = "HQ inimigo (geografia)";
                    break;
                }

                foreach (ConstructionManager construction in constructions)
                {
                    if (construction != null
                        && construction.IsCapturable
                        && construction.TeamId != unit.TeamId)
                        points.Add(construction.CurrentCellPosition);
                }
                label = $"prédios capturáveis não aliados ({points.Count}, geografia)";
                break;
        }

        if (points.Count == 0)
        {
            label += " — nenhum ponto";
            return false;
        }

        // Média no espaço do mundo, não nas coordenadas odd-r: média de offset
        // cisalha meia célula quando as linhas têm paridades diferentes.
        Vector3 accumulated = Vector3.zero;
        foreach (Vector3Int point in points)
        {
            Vector3Int cell = point;
            cell.z = 0;
            accumulated += resolvedMap.GetCellCenterWorld(cell);
        }
        anchor = resolvedMap.WorldToCell(accumulated / points.Count);
        anchor.z = 0;
        return true;
    }

    private bool IsEnemyUnitOnBoard(UnitManager other) =>
        other != null
        && !other.IsDead
        && !other.IsEmbarked
        && other.TeamId != unit.TeamId
        && other.TeamId != TeamId.Neutral;

    /// <summary>
    /// Separa a faixa em vanguarda e flancos pelo ângulo em relação à âncora.
    /// O que sobra é retaguarda e não entra em conjunto nenhum.
    /// </summary>
    private bool Classify(
        Vector3Int origin,
        Vector3Int anchor,
        HashSet<Vector3Int> band,
        HashSet<Vector3Int> vanguard,
        HashSet<Vector3Int> flank)
    {
        Vector2 originWorld = resolvedMap.GetCellCenterWorld(origin);
        Vector2 forward = (Vector2)resolvedMap.GetCellCenterWorld(anchor) - originWorld;
        if (forward.sqrMagnitude <= 0.0001f)
            return false;
        forward.Normalize();

        float flankLimit = Mathf.Max(flankHalfAngle, vanguardHalfAngle);
        foreach (Vector3Int cell in band)
        {
            Vector2 relative = (Vector2)resolvedMap.GetCellCenterWorld(cell) - originWorld;
            if (relative.sqrMagnitude <= 0.0001f)
                continue;
            float angle = Vector2.Angle(relative, forward);
            if (angle <= vanguardHalfAngle + 0.05f)
                vanguard.Add(cell);
            else if (angle <= flankLimit + 0.05f)
                flank?.Add(cell);
        }
        return true;
    }

    private static void SplitByCoverage(
        FogKnowledgeSnapshot knowledge,
        HashSet<Vector3Int> front,
        HashSet<Vector3Int> covered,
        HashSet<Vector3Int> blind)
    {
        foreach (Vector3Int cell in front)
        {
            if (knowledge.SensorCoveredCells.Contains(cell))
                covered?.Add(cell);
            else
                blind.Add(cell);
        }
    }

    // ------------------------------------------------------------------
    // Scene View
    // ------------------------------------------------------------------

    private void OnSceneGUI(SceneView sceneView)
    {
        HandleAnchorPicking();
        if (result == null || resolvedMap == null)
            return;

        float radius = Mathf.Max(0.05f, resolvedMap.cellSize.x * 0.2f);
        if (showRear)
        {
            Handles.color = RearColor;
            foreach (Vector3Int cell in result.Band)
                if (!result.Vanguard.Contains(cell) && !result.Flank.Contains(cell))
                    Handles.DrawSolidDisc(resolvedMap.GetCellCenterWorld(cell), Vector3.forward, radius * 0.6f);
        }
        if (showCovered)
        {
            Handles.color = CoveredColor;
            DrawDiscs(result.CoveredVanguard, radius, solid: true);
            DrawDiscs(result.CoveredFlank, radius, solid: false);
        }
        if (showBlindFlank)
        {
            Handles.color = BlindColor;
            DrawDiscs(result.BlindFlank, radius, solid: false);
        }
        if (showBlindVanguard)
        {
            Handles.color = BlindColor;
            DrawDiscs(result.BlindVanguard, radius, solid: true);
        }

        Vector3 originWorld = resolvedMap.GetCellCenterWorld(result.Origin);
        Vector3 anchorWorld = resolvedMap.GetCellCenterWorld(result.Anchor);
        Handles.color = AnchorColor;
        Handles.DrawDottedLine(originWorld, anchorWorld, 4f);
        Handles.DrawWireDisc(anchorWorld, Vector3.forward, radius * 1.6f);
        Handles.Label(anchorWorld, $"âncora: {result.AnchorLabel}");

        if (result.HasCheatComparison)
        {
            Vector3 cheatWorld = resolvedMap.GetCellCenterWorld(result.CheatAnchor);
            Handles.color = CheatAnchorColor;
            Handles.DrawDottedLine(originWorld, cheatWorld, 4f);
            Handles.DrawWireDisc(cheatWorld, Vector3.forward, radius * 1.6f);
            Handles.Label(cheatWorld, "massa real");
        }

        Handles.Label(
            originWorld,
            result.HangsPaper
                ? $"QUERO SPOTTING: {result.BlindVanguard.Count} cegas na vanguarda ({result.Urgency:P0})"
                : "vanguarda coberta");
    }

    private void DrawDiscs(HashSet<Vector3Int> cells, float radius, bool solid)
    {
        foreach (Vector3Int cell in cells)
        {
            Vector3 center = resolvedMap.GetCellCenterWorld(cell);
            if (solid)
                Handles.DrawSolidDisc(center, Vector3.forward, radius);
            else
                Handles.DrawWireDisc(center, Vector3.forward, radius, 3f);
        }
    }

    private void HandleAnchorPicking()
    {
        if (!pickingAnchor)
            return;
        Tilemap map = resolvedMap != null
            ? resolvedMap
            : overrideMap != null
                ? overrideMap
                : unit != null ? unit.BoardTilemap : null;
        if (map == null)
            return;

        Event evt = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        var plane = new Plane(Vector3.forward, map.transform.position);
        Vector3Int hover = manualAnchor;
        if (plane.Raycast(ray, out float enter))
        {
            hover = map.WorldToCell(ray.GetPoint(enter));
            hover.z = 0;
            Handles.color = AnchorColor;
            Handles.DrawWireDisc(map.GetCellCenterWorld(hover), Vector3.forward, 0.31f);
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (evt.type != EventType.MouseDown || evt.button != 0 || evt.alt)
            return;

        manualAnchor = hover;
        pickingAnchor = false;
        ClearResult();
        evt.Use();
        Repaint();
    }

    // ------------------------------------------------------------------
    // Contexto
    // ------------------------------------------------------------------

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
        if (overrideMap == null && unit != null)
            overrideMap = unit.BoardTilemap;
    }

    private void TryUseSelection()
    {
        GameObject selectedObject = Selection.activeGameObject;
        UnitManager found = selectedObject != null
            ? selectedObject.GetComponentInParent<UnitManager>()
            : null;
        if (found == null)
        {
            status = "O objeto selecionado não possui UnitManager.";
            return;
        }
        unit = found;
        ClearResult();
        AutoDetectContext();
        status = $"Unidade: {found.name}.";
    }

    private void ClearResult()
    {
        result = null;
        SceneView.RepaintAll();
    }

    private static T FindFirstAsset<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        return guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]))
            : null;
    }

    private static string Format(Vector3Int cell) => $"({cell.x},{cell.y})";
}
