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
            "A névoa vem desligada: ela não é regra de captura, é recorte do " +
            "que o time enxerga, e cruzar isso com o alcance é trabalho da IA. " +
            "Ligada, a consulta deixa de responder 'vale a pena ir descobrir " +
            "aquilo?', porque o alvo some antes de receber nota.",
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
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Filtros do sensor (a ferramenta passa tudo por padrão)",
            EditorStyles.miniBoldLabel);
        applyFogOfWar = EditorGUILayout.Toggle(
            "  Aplicar névoa", applyFogOfWar);
        skipConstructionsWithCapturer = EditorGUILayout.Toggle(
            new GUIContent(
                "  Descartar com capturador",
                "Desligado, a construção continua pontuada e o ocupante sai " +
                "reportado na linha. A IA já é atraída pelo capturador que " +
                "chegou — o fato basta, o veredito é dela."),
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
            "Âmbar: o alvo vencedor. Verde/azul/laranja: Tactical, " +
            "Operational e fora do envelope. Anel branco: já tem capturador " +
            "em cima. Cinza: recusada, com o motivo no hex.\n" +
            "Clique numa linha para abrir as parcelas e destacá-la na cena.",
            MessageType.None);

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
                    (alvo.capturerOnCell != null ? " | ⛳" : string.Empty),
                    EditorStyles.miniButton))
            {
                selected = alvo;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (!isSelected)
                continue;
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
            tacticalBudget = Mathf.Max(0, unit.MaxMovementPoints),
            operationalTurns = operationalTurns,
            subStep = subStep,
            constructions = constructions,
            includeConstruction = sectorGate,
            matchController = Application.isPlaying
                ? FindFirstObjectByType<MatchController>()
                : null,
            applyFogOfWar = applyFogOfWar,
            skipConstructionsWithCapturer = skipConstructionsWithCapturer,
            diagnosticLog = message => status = message
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

        // Recusadas primeiro, para o campeão desenhar por cima delas.
        if (drawRejected)
        {
            Handles.color = new Color(0.45f, 0.45f, 0.45f, 0.75f);
            for (int i = 0; i < result.rejected.Count; i++)
            {
                MelhorCapturaReject reject = result.rejected[i];
                Vector3 cell = tilemap.GetCellCenterWorld(reject.cell);
                Handles.DrawSolidDisc(cell, Vector3.back, 0.18f);
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
            Handles.color = champion
                ? new Color(1f, 0.75f, 0.05f, 0.95f)
                : ResolveTierColor(alvo.tier);
            Handles.DrawSolidDisc(to, Vector3.back, champion ? 0.32f : 0.24f);
            // Anel branco = já tem capturador em cima. O alvo continua na
            // lista; o anel é o fato, não a exclusão.
            if (alvo.capturerOnCell != null)
            {
                Handles.color = Color.white;
                Handles.DrawWireDisc(
                    to, Vector3.back, champion ? 0.38f : 0.30f);
                Handles.color = champion
                    ? new Color(1f, 0.75f, 0.05f, 0.95f)
                    : ResolveTierColor(alvo.tier);
            }
            // Só o campeão ganha linha: com dezenas de candidatas o resto vira
            // teia e esconde exatamente o que a ferramenta quer mostrar.
            if (champion)
                Handles.DrawDottedLine(from, to, 4f);
            Handles.Label(
                to,
                $"{alvo.displayScore}\n{ResolveTierTag(alvo.tier)} R{alvo.effectiveCost}",
                ScoreLabelStyle(Color.black, champion ? 14 : 12));
        }
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
