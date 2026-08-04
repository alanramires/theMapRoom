using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MelhorCapturaWindow : EditorWindow
{
    [SerializeField] private UnitManager unit;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private ConstructionSector sectorFilter =
        ConstructionSector.None;
    [SerializeField] private int operationalTurns = 2;
    [SerializeField] private ReachSubStep subStep = ReachSubStep.Terrestre;
    // Mesmo contrato da Hotzone: filtro de sensor nasce DESLIGADO e é opt-in,
    // para quando você quiser ver justamente o que ele corta.
    [SerializeField] private bool applyFogOfWar;
    [SerializeField] private bool skipConstructionsWithCapturer;
    [SerializeField] private bool drawRejected = true;
    [SerializeField] private int listFontSize = 12;

    private MelhorCapturaResult result;
    private MelhorCapturaAlvoScore selected;
    private string status = "Selecione um capturador em campo.";
    private Vector2 scroll;
    private GUIStyle listStyle;
    private MatchController matchController;

    [MenuItem("Tools/Hotzone/Melhor Captura")]
    public static void Open() =>
        GetWindow<MelhorCapturaWindow>("Melhor Captura").Show();

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
            "Melhor Captura", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura: dada UMA unidade, quais construções ela pode " +
            "capturar e em que ordem. Tactical e Operational vêm do envelope; " +
            "fora das duas bandas a distância é cúbica, porque a essa altura " +
            "a resposta útil é 'para que lado'.\n\n" +
            "A permissão é toda do PodeCapturar — a ferramenta não lê skill, " +
            "não compara time e não conhece plano. O setor aqui é só um filtro " +
            "sobre o conjunto de candidatas.\n\n" +
            "A névoa vem desligada. Ligada, ela separa o que pode ser capturado " +
            "agora do objetivo conhecido que ainda precisa ser revelado. O " +
            "segundo continua no ranking como captura da próxima rodada.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        unit = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade", unit, typeof(UnitManager), true);
        tilemap = (Tilemap)EditorGUILayout.ObjectField(
            "Tabuleiro", tilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase,
            typeof(TerrainDatabase), false);
        sectorFilter = ConstructionSectorOrder.Popup(
            "Setor (None = todos)", sectorFilter);
        operationalTurns = Mathf.Max(
            1, EditorGUILayout.IntField(
                "Turnos operacionais", operationalTurns));
        DrawMeasurePopup();
        if (EditorGUI.EndChangeCheck())
        {
            AutoDetect();
            result = null;
            selected = null;
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseSelection(silent: false);
        if (GUILayout.Button("Auto Detect"))
            AutoDetect();
        if (GUILayout.Button("Limpar"))
            ClearEvaluation();
        using (new EditorGUI.DisabledScope(
                   Application.isPlaying || matchController == null))
        {
            if (GUILayout.Button("Cozinhar FOW 0"))
                CookRoundZeroFog();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Filtros do sensor (a ferramenta passa tudo por padrão)",
            EditorStyles.miniBoldLabel);
        applyFogOfWar = EditorGUILayout.Toggle(
            new GUIContent(
                "  Aplicar névoa",
                "No Edit Mode usa o bake manual da rodada 0 para o slot da " +
                "unidade. No runtime usa o snapshot confirmado do mesmo slot."),
            applyFogOfWar);
        skipConstructionsWithCapturer = EditorGUILayout.Toggle(
            new GUIContent(
                "  Descartar capturador no máximo",
                "Só descarta quando existe capturador sobre a construção e " +
                "os pontos atuais já estão no máximo. Abaixo do máximo ela " +
                "está em disputa e continua no ranking para reconquista."),
            skipConstructionsWithCapturer);

        drawRejected = EditorGUILayout.Toggle(
            "Mostrar recusadas na cena", drawRejected);
        listFontSize = EditorGUILayout.IntSlider(
            "Fonte da lista", listFontSize, 9, 22);

        using (new EditorGUI.DisabledScope(
                   unit == null
                   || tilemap == null
                   || terrainDatabase == null))
        {
            if (GUILayout.Button(
                    "Pontuar Construções", GUILayout.Height(30f)))
                Evaluate();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(status, MessageType.None);
        if (result == null)
            return;

        if (!result.hasCaptureSkill)
        {
            EditorGUILayout.HelpBox(
                "Esta unidade não possui a habilitação de capturar " +
                "construções. Quem responde isso é o PodeCapturar — dê a skill " +
                "no UnitData e ela passa a pontuar sem tocar em código.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField(
            $"Tactical {result.CountInTier(MelhorCapturaTier.Tactical)} | " +
            $"Operational {result.CountInTier(MelhorCapturaTier.Operational)} | " +
            $"Fora {result.CountInTier(MelhorCapturaTier.BeyondOperational)}",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Orçamento",
            $"Tactical {result.tacticalBudget} | " +
            $"Operational {result.operationalBudget}");
        EditorGUILayout.LabelField(
            "Candidatas",
            $"{result.candidatesOffered} ofertadas → " +
            $"{result.candidatesVisited} avaliadas");
        int reconquistas = 0;
        for (int i = 0; i < result.ranking.Count; i++)
        {
            if (result.ranking[i].operation
                == PodeCapturarSensor.CaptureOperationType.RecoverAlly)
                reconquistas++;
        }
        EditorGUILayout.LabelField(
            "Operações",
            $"⚔ captura {result.ranking.Count - reconquistas} | " +
            $"♻ reconquista {reconquistas}");
        EditorGUILayout.LabelField(
            "Recusas",
            result.BuildRejectedSummary(),
            EditorStyles.wordWrappedMiniLabel);

        // O filtro NUNCA corta calado. Sem esta linha não dá para separar
        // "o setor tirou" de "o PodeCapturar recusou" — e as duas somem igual.
        if (result.candidatesFilteredOut > 0)
        {
            EditorGUILayout.HelpBox(
                $"O filtro de setor cortou {result.candidatesFilteredOut} " +
                $"construção(ões) antes da avaliação. Elas não estão no " +
                $"ranking nem nas recusas — ponha o setor em None para ver " +
                $"tudo o que a unidade enxerga.",
                MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "Âmbar: alvo vencedor capturável agora. Verde/azul/laranja: Tactical, " +
            "Operational e fora do envelope. Anel branco: já tem capturador " +
            "em cima. Roxo: objetivo conhecido para aproximação; a névoa " +
            "impede capturar nesta rodada. Anel âmbar: o roxo é o vencedor. " +
            "Cinza: recusas, com o motivo no hex.\n" +
            "Clique numa linha para abrir as parcelas e destacá-la na cena.",
            MessageType.None);

        if (result.best != null)
        {
            EditorGUILayout.HelpBox(
                "Saídas do melhor alvo:\n" +
                BuildPositionsText(result.best, numbered: true),
                MessageType.None);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < result.ranking.Count; i++)
        {
            MelhorCapturaAlvoScore alvo = result.ranking[i];
            bool isSelected = selected == alvo;
            string name = alvo.construction != null
                ? alvo.construction.ConstructionDisplayName
                : "?";

            GUI.backgroundColor = isSelected
                ? new Color(1f, 0.8f, 0.2f)
                : Color.white;
            if (GUILayout.Button(
                    $"#{i + 1} [{alvo.tier}] {ResolveOperationTag(alvo.operation)} " +
                    $"{name} {alvo.cell} | custo={alvo.effectiveCost} " +
                    $"turnos={(alvo.turnsToCapture >= 0 ? alvo.turnsToCapture.ToString() : "∞")} " +
                    $"pontos={alvo.displayScore}" +
                    (alvo.blockedByFog ? " | PRÓXIMA RODADA (NÉVOA)" : string.Empty) +
                    (alvo.visibilityContributors.Count > 0
                        ? $" | SPOTTER x{alvo.visibilityContributors.Count}"
                        : string.Empty) +
                    (alvo.capturerOnCell != null ? " | ⛳" : string.Empty),
                    EditorStyles.miniButton))
            {
                selected = alvo;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (!isSelected)
                continue;
            EditorGUILayout.LabelField(
                BuildPositionsText(alvo, numbered: true),
                ResolveListStyle());
            EditorGUILayout.LabelField(alvo.reason, ResolveListStyle());
            EditorGUILayout.ObjectField(
                "   Construção",
                alvo.construction,
                typeof(ConstructionManager),
                true);
            EditorGUILayout.LabelField(
                $"   pontos de captura: {alvo.construction.CurrentCapturePoints}" +
                $"/{alvo.construction.CapturePointsMax} — " +
                $"faltam {alvo.remainingCapturePoints} a {alvo.capturePower}/turno" +
                (alvo.prerequisitePenalty
                    ? " (penalidade de pré-requisito: 50%)"
                    : string.Empty),
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"   setor: {alvo.construction.Sector} | " +
                $"slot dono: {alvo.construction.SlotIndex}",
                EditorStyles.miniLabel);
            if (alvo.capturerOnCell != null)
            {
                EditorGUILayout.ObjectField(
                    "   Capturador em cima",
                    alvo.capturerOnCell,
                    typeof(UnitManager),
                    true);
            }
        }
        if (result.ranking.Count == 0)
        {
            EditorGUILayout.LabelField(
                "Nenhuma construção pontuada.",
                EditorStyles.miniLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Só as medições que a Captura aceita, pelo mesmo predicado que o Build
    /// usa. Oferecer `Artilheiro` aqui devolveria envelope nulo e jogaria tudo
    /// em "fora do envelope", sem dizer por quê.
    /// </summary>
    private void DrawMeasurePopup()
    {
        List<ReachSubStep> valid =
            UnitReachEnvelopeService.GetSubSteps(ReachIntent.Capture, unit);
        if (valid.Count <= 1)
        {
            subStep = valid.Count == 1 ? valid[0] : ReachSubStep.Terrestre;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Medição", ResolveMeasureLabel(subStep));
            return;
        }

        var labels = new string[valid.Count];
        int selected = 0;
        for (int i = 0; i < valid.Count; i++)
        {
            labels[i] = ResolveMeasureLabel(valid[i]);
            if (valid[i] == subStep)
                selected = i;
        }
        selected = EditorGUILayout.Popup("Medição", selected, labels);
        subStep = valid[Mathf.Clamp(selected, 0, valid.Count - 1)];
    }

    private static string ResolveMeasureLabel(ReachSubStep subStep)
    {
        switch (subStep)
        {
            case ReachSubStep.Terrestre:
                return "Geográfico (caminhos)";
            case ReachSubStep.Aereo:
                return "Linear (cúbica)";
            default:
                return subStep.ToString();
        }
    }

    private GUIStyle ResolveListStyle()
    {
        if (listStyle == null)
        {
            listStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true
            };
        }
        listStyle.fontSize = Mathf.Clamp(listFontSize, 9, 22);
        return listStyle;
    }

    private void Evaluate()
    {
        AutoDetect();
        selected = null;
        List<ConstructionManager> constructions = CollectConstructions();
        FogKnowledgeSnapshot fogKnowledge = null;
        string fogReason = string.Empty;
        if (applyFogOfWar)
        {
            if (matchController == null)
            {
                result = null;
                status = "MatchController indisponivel para consultar a nevoa.";
                return;
            }

            PlayerSlotId observerSlot = PlayerSlotId.FromIndex(unit.SlotIndex);
            bool copied = Application.isPlaying
                ? matchController.TryCopyConfirmedFogKnowledgeSnapshotForSlot(
                    observerSlot,
                    tilemap,
                    out fogKnowledge,
                    out fogReason)
                : matchController.TryCopyRoundZeroFogKnowledgeSnapshotForSlot(
                    observerSlot,
                    tilemap,
                    out fogKnowledge,
                    out fogReason);
            if (!copied || fogKnowledge == null)
            {
                result = null;
                status = fogReason;
                return;
            }
        }

        // O setor é FILTRO sobre o conjunto, não conhecimento de plano: o
        // serviço recebe "estas construções", nunca "o setor C".
        ConstructionSector wanted = sectorFilter;
        System.Func<ConstructionManager, bool> sectorGate =
            wanted == ConstructionSector.None
                ? null
                : construction => construction != null
                                  && construction.Sector == wanted;

        result = MelhorCapturaService.Evaluate(new MelhorCapturaRequest
        {
            unit = unit,
            map = tilemap,
            terrainDatabase = terrainDatabase,
            // Zero de propósito: deixa o serviço aplicar a mesma conta que a IA
            // aplica. A ferramenta tem que mostrar o que a IA vê, e um orçamento
            // escolhido aqui faria a janela discordar do jogo.
            tacticalBudget = 0,
            operationalTurns = operationalTurns,
            subStep = subStep,
            constructions = constructions,
            includeConstruction = sectorGate,
            matchController = matchController,
            applyFogOfWar = applyFogOfWar,
            isCellActionVisible = fogKnowledge != null
                ? cell => fogKnowledge.GeographicallyVisibleCells.Contains(cell)
                : null,
            isUnitVisible = fogKnowledge != null
                ? target => fogKnowledge.IsEnemyVisible(target)
                : null,
            resolveVisibilityContributors = fogKnowledge != null
                ? cell => ResolveVisibilityContributors(fogKnowledge, cell)
                : null,
            skipConstructionsWithCapturer = skipConstructionsWithCapturer,
            diagnosticLog = message => status = applyFogOfWar
                ? message + " | " + fogReason
                : message
        });

        SceneView.RepaintAll();
    }

    /// <summary>
    /// Runtime lê o registro vivo; Editor varre a cena.
    ///
    /// Fora do Play Mode o AllActive está vazio, e uma ferramenta que consultar
    /// só ele responde otimista — "nenhuma construção aqui" — sem ter olhado
    /// para nada. Além de montar a lista, sincroniza o registro, porque os
    /// sensores consultados abaixo também o leem.
    /// </summary>
    private List<ConstructionManager> CollectConstructions()
    {
        if (Application.isPlaying)
            return new List<ConstructionManager>(ConstructionManager.AllActive);

        var scene = new List<ConstructionManager>();
        ConstructionManager[] found =
            FindObjectsByType<ConstructionManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject.activeInHierarchy)
                scene.Add(found[i]);
        }

        ConstructionManager.AllActive.Clear();
        for (int i = 0; i < scene.Count; i++)
            ConstructionManager.AllActive.Add(scene[i]);
        return scene;
    }

    private void AutoDetect()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (unit != null && unit.BoardTilemap != null)
            tilemap = unit.BoardTilemap;
        if (terrainDatabase != null)
            return;
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        if (guids.Length > 0)
        {
            terrainDatabase =
                AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }
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
        result = null;
        selected = null;
        status = bakeResult + " O Melhor Captura usara este bake na proxima consulta.";
        SceneView.RepaintAll();
        Debug.Log($"[FoW][RoundZeroBake] {bakeResult}", matchController);
    }

    private void ClearEvaluation()
    {
        result = null;
        selected = null;
        status = unit != null
            ? $"Unidade: {unit.name}. Resultado limpo."
            : "Resultado limpo.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void TryUseSelection(bool silent)
    {
        UnitManager picked =
            Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<UnitManager>()
                : null;
        if (picked == null)
        {
            if (!silent)
                status = "O objeto selecionado não possui UnitManager.";
            return;
        }

        unit = picked;
        result = null;
        AutoDetect();
        status = $"Unidade: {picked.name}.";
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (result == null || unit == null || tilemap == null)
            return;

        // Recusadas primeiro, para os alvos pontuados desenharem por cima.
        if (drawRejected)
        {
            for (int i = 0; i < result.rejected.Count; i++)
            {
                MelhorCapturaReject reject = result.rejected[i];
                Vector3 cell = tilemap.GetCellCenterWorld(reject.cell);
                Handles.color = new Color(0.45f, 0.45f, 0.45f, 0.75f);
                Handles.DrawSolidDisc(
                    cell,
                    Vector3.back,
                    0.18f);
                Handles.Label(
                    cell,
                    ShortenReason(reject.reason),
                    ScoreLabelStyle(Color.white, 10));
            }
        }

        Vector3 from = tilemap.GetCellCenterWorld(result.origin);
        for (int i = 0; i < result.ranking.Count; i++)
        {
            MelhorCapturaAlvoScore alvo = result.ranking[i];
            Vector3 to = tilemap.GetCellCenterWorld(alvo.cell);
            // Clicou na lista, manda na cena: o selecionado assume o destaque
            // do campeão, que é como se compara "por que este e não aquele".
            bool champion = selected != null ? alvo == selected : i == 0;
            Color targetColor = alvo.blockedByFog
                ? new Color(0.72f, 0.32f, 1f, 0.92f)
                : champion
                    ? new Color(1f, 0.75f, 0.05f, 0.95f)
                    : ResolveTierColor(alvo.tier);
            Handles.color = targetColor;
            Handles.DrawSolidDisc(to, Vector3.back, champion ? 0.32f : 0.24f);
            // Roxo preserva o significado "captura futura". O anel âmbar
            // informa que, apesar disso, ele ganhou o ranking e o tracejado.
            if (champion && alvo.blockedByFog)
            {
                Handles.color = new Color(1f, 0.75f, 0.05f, 0.95f);
                Handles.DrawWireDisc(to, Vector3.back, 0.39f);
                Handles.color = targetColor;
            }
            // Anel branco = já tem capturador em cima. O alvo continua na
            // lista; o anel é o fato, não a exclusão.
            if (alvo.capturerOnCell != null)
            {
                Handles.color = Color.white;
                Handles.DrawWireDisc(
                    to, Vector3.back, champion ? 0.38f : 0.30f);
                Handles.color = targetColor;
            }
            // Só o campeão ganha linha: com dezenas de candidatas o resto vira
            // teia e esconde exatamente o que a ferramenta quer mostrar.
            if (champion)
            {
                Handles.DrawDottedLine(from, to, 4f);
                DrawVisibilityContributorLines(alvo, to);
            }
            Handles.Label(
                to,
                champion
                    ? BuildPositionsText(alvo, numbered: false) + "\n" +
                      $"{alvo.displayScore} {ResolveTierTag(alvo.tier)} R{alvo.effectiveCost}"
                    : $"{alvo.displayScore}\n{ResolveTierTag(alvo.tier)} R{alvo.effectiveCost}",
                ScoreLabelStyle(
                    alvo.blockedByFog ? Color.white : Color.black,
                    champion ? 14 : 12));
        }
    }

    private void DrawVisibilityContributorLines(
        MelhorCapturaAlvoScore alvo,
        Vector3 targetWorld)
    {
        if (alvo == null || alvo.visibilityContributors.Count == 0)
            return;

        Handles.color = new Color(1f, 0.8f, 0.1f, 1f);
        for (int i = 0; i < alvo.visibilityContributors.Count; i++)
        {
            UnitManager observer = alvo.visibilityContributors[i];
            if (observer == null || !observer.gameObject.activeInHierarchy)
                continue;

            Vector3Int observerCell = observer.CurrentCellPosition;
            observerCell.z = 0;
            Vector3 observerWorld = tilemap.GetCellCenterWorld(observerCell);
            if (Vector3.Distance(observerWorld, targetWorld) <= 0.0001f)
                continue;

            Handles.DrawDottedLine(observerWorld, targetWorld, 4f);
            Handles.SphereHandleCap(
                0,
                observerWorld,
                Quaternion.identity,
                0.10f,
                EventType.Repaint);
            Handles.Label(
                Vector3.Lerp(observerWorld, targetWorld, 0.5f)
                    + new Vector3(0.08f, -0.08f, 0f),
                $"SPOTTER: {ResolveUnitLabel(observer)}",
                ScoreLabelStyle(new Color(1f, 0.85f, 0.2f)));
        }
    }

    private static IReadOnlyList<UnitManager> ResolveVisibilityContributors(
        FogKnowledgeSnapshot snapshot,
        Vector3Int cell)
    {
        if (snapshot != null
            && snapshot.TryGetVisibilityContributors(
                cell,
                out IReadOnlyList<UnitManager> contributors))
        {
            return contributors;
        }
        return System.Array.Empty<UnitManager>();
    }

    private static string ResolveUnitLabel(UnitManager observer)
    {
        if (observer == null)
            return "?";
        return string.IsNullOrWhiteSpace(observer.UnitDisplayName)
            ? observer.name
            : observer.UnitDisplayName;
    }

    private static string BuildPositionsText(
        MelhorCapturaAlvoScore alvo,
        bool numbered)
    {
        if (alvo == null || alvo.positions.Count == 0)
            return "sem saídas";

        var lines = new List<string>(alvo.positions.Count);
        for (int i = 0; i < alvo.positions.Count; i++)
        {
            MelhorCapturaPosition position = alvo.positions[i];
            if (position == null || string.IsNullOrWhiteSpace(position.text))
                continue;
            lines.Add(numbered
                ? $"{i + 1}. {position.text}"
                : position.text);
        }
        return string.Join("\n", lines);
    }

    /// <summary>Cabe num hex; o motivo inteiro fica na lista da janela.</summary>
    private static string ShortenReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "?";
        if (reason.Contains("desconhecido"))
            return "névoa";
        if (reason.Contains("capturador em cima"))
            return "ocupado";
        if (reason.Contains("nao e capturavel")
            || reason.Contains("não é capturável"))
            return "n/cap";
        if (reason.Contains("Nao ha construcao"))
            return "vazio";
        return reason.Length <= 8 ? reason : reason.Substring(0, 8);
    }

    /// <summary>
    /// Reconquista de aliado tem que saltar aos olhos: é o caso que o ranking
    /// antigo do capturador nem enxergava, porque descartava tudo do próprio
    /// time antes de perguntar ao sensor.
    /// </summary>
    private static string ResolveOperationTag(
        PodeCapturarSensor.CaptureOperationType operation)
    {
        return operation
               == PodeCapturarSensor.CaptureOperationType.RecoverAlly
            ? "♻ RECONQUISTA"
            : "⚔ captura";
    }

    private static string ResolveTierTag(MelhorCapturaTier tier)
    {
        switch (tier)
        {
            case MelhorCapturaTier.Tactical:
                return "T";
            case MelhorCapturaTier.Operational:
                return "O";
            default:
                return "F";
        }
    }

    private static Color ResolveTierColor(MelhorCapturaTier tier)
    {
        switch (tier)
        {
            case MelhorCapturaTier.Tactical:
                return new Color(0.20f, 0.90f, 0.35f, 0.85f);
            case MelhorCapturaTier.Operational:
                return new Color(0.10f, 0.60f, 1f, 0.85f);
            default:
                return new Color(1f, 0.75f, 0.10f, 0.75f);
        }
    }

    private static GUIStyle ScoreLabelStyle(Color color, int fontSize = 12) =>
        new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            normal = { textColor = color }
        };
}
