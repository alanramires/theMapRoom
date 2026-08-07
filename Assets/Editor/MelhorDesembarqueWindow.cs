using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MelhorDesembarqueWindow : EditorWindow
{
    private enum DebugView { Ambas, MelhorLZ, SpotsPassageiros }

    [SerializeField] private UnitManager passenger;
    [SerializeField] private UnitManager transporter;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DebugView view = DebugView.Ambas;
    [SerializeField] private int routeHorizon = 120;
    [SerializeField] private bool hasPickedTargetCell;
    [SerializeField] private Vector3Int pickedTargetCell;
    [SerializeField] private bool hasSecondPickedTargetCell;
    [SerializeField] private Vector3Int secondPickedTargetCell;

    private Tilemap map;
    // Uma inundação de tabuleiro por (passageiro, alvo). O mapa reverso sai do
    // ALVO e vale para todas as células de largada de uma vez — recalculá-lo
    // por LZ e por spot era o que travava a janela. O runtime já faz isso em
    // GetOrBuildDisembarkPassengerRoute; aqui fora não havia nada, porque o
    // MovementReachCache se desliga sozinho fora do Play Mode
    // (TryBuildKey exige Application.isPlaying).
    private readonly Dictionary<(int passenger, Vector3Int target),
        Dictionary<Vector3Int, int>> routeCache =
        new Dictionary<(int, Vector3Int), Dictionary<Vector3Int, int>>();
    // Teto duro: uma ferramenta de diagnóstico pode devolver resultado parcial,
    // mas não pode pendurar o editor. Fica exposto porque quem conhece o mapa
    // sabe quantos alvos ele tem.
    // Nevoa: mesmo par do Melhor Captura. Sem isto a bancada respondia com o
    // tabuleiro inteiro enquanto o runtime recusava tudo por preto — duas
    // ferramentas dando respostas opostas sobre a mesma cena.
    // SEM NEVOA NESTA JANELA, de proposito.
    //
    //   "no fundo a ferramenta devolve o mapa alheio a fow e a AI courier
    //    decide, ne?" (autor, 2026-08-07)
    //
    // E a divisao de camadas do projeto: a bancada responde GEOMETRIA —
    // onde daria para entregar, quanto o passageiro andaria, qual a banda.
    // Conhecimento e decisao sao do organizador. Tentar prever com nevoa
    // aqui nunca ia bater com o jogo: o commit e que revela, entao a
    // bancada sempre olharia com a informacao de ANTES do passo.
    // Teto de rota restante do passageiro, o mesmo que o runtime chama de
    // dropOffRange. Sem ele a bancada ranqueava LZ com o passageiro a 15 hexes
    // do alvo — resposta que o jogo nunca daria. -1 = automatico (Operational
    // do passageiro), como o courier faz.
    [SerializeField] private int dropOffRangeOverride = -1;
    // LZ recusadas pelo teto de rota. Guardadas de proposito: filtro mudo e o
    // inimigo de ferramenta de diagnostico — some do desenho e some da
    // explicacao junto. O runtime ignora estas celulas; a bancada MOSTRA que as
    // ignorou, e por que.
    private readonly List<Vector3Int> rejectedByRange = new List<Vector3Int>();
    [SerializeField] private bool showRejectedByRange = true;
    [SerializeField] private MatchController matchController;
    [SerializeField] private int maxRouteFloods = 256;
    private int routeFloods;
    private bool routeBudgetExhausted;
    private readonly List<MelhorDesembarqueLzScore> ranking =
        new List<MelhorDesembarqueLzScore>();
    private MelhorDesembarqueLzScore selected;
    private Vector2 scroll;
    private string status =
        "Selecione o passageiro; o hex desejado e opcional.";
    private bool pickingTargetCell;
    private bool pickingSecondTargetCell;
    private Vector3Int hoverCell;

    [MenuItem("Tools/Hotzone/Melhor LZ de Desembarque")]
    public static void Open() =>
        GetWindow<MelhorDesembarqueWindow>("Melhor LZ de Desembarque").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetect();
    }

    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    private void OnSelectionChange()
    {
        Repaint();
    }

    private void AutoDetect()
    {
        if (passenger != null)
        {
            if (passenger.IsEmbarked
                && passenger.EmbarkedTransporter != null)
                transporter = passenger.EmbarkedTransporter;
            if (passenger.BoardTilemap != null)
                map = passenger.BoardTilemap;
        }
        if (map == null
            && transporter != null
            && transporter.BoardTilemap != null)
            map = transporter.BoardTilemap;
        if (terrainDatabase == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
            if (guids.Length > 0)
                terrainDatabase = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Melhor LZ de Desembarque", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura centrada no passageiro. O transportador e inferido " +
            "da carga embarcada. Sem hex desejado, respeita a intencao atual " +
            "do passageiro; com hex informado, procura a melhor LZ para " +
            "entrega-lo naquele destino. Cada LZ simula PodeDesembarcar sem " +
            "mover nenhuma unidade.",
            MessageType.Info);

        EditorGUILayout.LabelField("Contexto", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        passenger = (UnitManager)EditorGUILayout.ObjectField(
            "Passageiro", passenger, typeof(UnitManager), true);
        transporter = (UnitManager)EditorGUILayout.ObjectField(
            new GUIContent(
                "Transportador",
                "Inferido automaticamente quando o passageiro esta embarcado."),
            transporter, typeof(UnitManager), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        view = (DebugView)EditorGUILayout.EnumPopup("Visao", view);
        routeHorizon = Mathf.Max(10, EditorGUILayout.IntField("Horizonte de rota", routeHorizon));
        maxRouteFloods = Mathf.Max(
            16,
            EditorGUILayout.IntField(
                new GUIContent(
                    "Teto de mapas de rota",
                    "Um mapa por (passageiro, alvo). Atingido o teto, o " +
                    "ranking sai parcial em vez de pendurar o editor."),
                maxRouteFloods));
        if (EditorGUI.EndChangeCheck())
        {
            AutoDetect();
            ranking.Clear();
            selected = null;
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();
        if (GUILayout.Button("Usar como Transportador"))
            TryUseCurrentSelectionAsTransporter();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectRuntimeSelection();
        if (GUILayout.Button("Limpar", GUILayout.Width(70f)))
            ClearAll();
        EditorGUILayout.EndHorizontal();

        dropOffRangeOverride = EditorGUILayout.IntField(
            new GUIContent(
                "Teto de rota (drop range)",
                "Quanto o passageiro ainda pode ter que andar DEPOIS de "
                + "largado. -1 usa o automatico: Operational do passageiro "
                + "(MaxMovementPoints x 2), que e o que o courier usa."),
            dropOffRangeOverride);
        showRejectedByRange = EditorGUILayout.Toggle(
            new GUIContent(
                "Mostrar recusadas pelo teto",
                "Anéis vermelhos: LZs que o passageiro alcançaria, mas com "
                + "rota restante acima do teto. O runtime também as ignora — "
                + "aqui elas aparecem para explicar por que o ranking encolheu."),
            showRejectedByRange);

        DrawEmbarkedPassengerGrid();

        DrawDesiredHexRow(
            "Hex desejado — vaga 1", 0,
            ref hasPickedTargetCell, ref pickedTargetCell);
        DrawDesiredHexRow(
            "Hex desejado — vaga 2", 1,
            ref hasSecondPickedTargetCell, ref secondPickedTargetCell);

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(transporter == null))
        {
            GUI.backgroundColor = pickingTargetCell
                ? new Color(1f, 0.75f, 0.2f)
                : Color.white;
            if (GUILayout.Button(
                    pickingTargetCell
                        ? "Escolhendo destino da vaga 1..."
                        : "Escolher destino da vaga 1"))
            {
                pickingTargetCell = !pickingTargetCell;
                pickingSecondTargetCell = false;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = pickingSecondTargetCell
                ? new Color(0.85f, 0.35f, 1f)
                : Color.white;
            if (GUILayout.Button(
                    pickingSecondTargetCell
                        ? "Escolhendo destino da vaga 2..."
                        : "Escolher destino da vaga 2"))
            {
                pickingSecondTargetCell = !pickingSecondTargetCell;
                pickingTargetCell = false;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(
                   transporter == null
                   || map == null))
        {
            if (GUILayout.Button("Calcular Melhor LZ de Desembarque", GUILayout.Height(28f)))
                Calculate();
        }

        DrawValidationsSection();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        if (ranking.Count == 0)
            return;
        EditorGUILayout.HelpBox(
            "Amarelo: LZ vencedora do transportador. " +
            "Verde/laranja/vermelho: demais LZs do ranking. " +
            "Azul/ciano: hex onde cada passageiro desembarca para a LZ selecionada.",
            MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < ranking.Count; i++)
        {
            MelhorDesembarqueLzScore lz = ranking[i];
            bool isSelected = selected == lz;
            GUI.backgroundColor = isSelected
                ? new Color(1f, 0.8f, 0.2f)
                : Color.white;
            if (GUILayout.Button(
                    $"#{i + 1} LZ {lz.cell} | pax={lz.delivered} " +
                    $"rota={lz.totalRouteCost} move={lz.moveCost} pontos={lz.displayScore}",
                    EditorStyles.miniButton))
            {
                selected = lz;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (!isSelected)
                continue;
            EditorGUILayout.LabelField(lz.reason, EditorStyles.miniLabel);
            foreach (MelhorDesembarqueSpotScore spot in lz.spots)
            {
                EditorGUILayout.LabelField(
                    $"  {spot.option.passengerUnit.name} -> {spot.option.disembarkCell} " +
                    $"alvo={spot.target} rota={spot.routeCost}",
                    EditorStyles.miniLabel);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawEmbarkedPassengerGrid()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Passageiros embarcados",
            EditorStyles.boldLabel);

        IReadOnlyList<UnitTransportSeatRuntime> seats =
            transporter != null
                ? transporter.TransportedUnitSlots
                : null;
        var embarkedSeats = new List<UnitTransportSeatRuntime>();
        for (int i = 0; seats != null && i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat?.embarkedUnit != null
                && seat.embarkedUnit.IsEmbarked)
                embarkedSeats.Add(seat);
        }
        embarkedSeats.Sort((a, b) =>
        {
            int byTurn = ResolveEmbarkedTurn(a)
                .CompareTo(ResolveEmbarkedTurn(b));
            if (byTurn != 0)
                return byTurn;
            int bySlot = a.slotIndex.CompareTo(b.slotIndex);
            return bySlot != 0
                ? bySlot
                : a.seatIndex.CompareTo(b.seatIndex);
        });

        if (embarkedSeats.Count == 0)
        {
            EditorGUILayout.HelpBox(
                transporter == null
                    ? "Selecione um transportador para listar sua carga."
                    : "Este transportador nao possui passageiros embarcados.",
                MessageType.None);
            return;
        }

        GUI.backgroundColor = passenger == null
            ? new Color(1f, 0.8f, 0.2f)
            : Color.white;
        if (GUILayout.Button(
                "Toda a carga — simular desembarque conjunto",
                GUILayout.Height(28f)))
        {
            passenger = null;
            ranking.Clear();
            selected = null;
            status =
                "Toda a carga sera avaliada em conjunto; vaga 1 e vaga 2 " +
                "usam seus respectivos hexes desejados.";
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        int columns = Mathf.Max(
            1,
            Mathf.FloorToInt(
                Mathf.Max(180f, position.width - 24f) / 190f));
        for (int i = 0; i < embarkedSeats.Count; i += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < columns; c++)
            {
                int index = i + c;
                if (index >= embarkedSeats.Count)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }

                UnitTransportSeatRuntime seat = embarkedSeats[index];
                UnitManager embarked = seat.embarkedUnit;
                bool active = passenger == embarked;
                GUI.backgroundColor = active
                    ? new Color(0.25f, 0.9f, 1f)
                    : Color.white;
                // Verbo + coordenada. O id do predio saiu junto com o filtro de
                // captura: o transportador nao pergunta O QUE a carga vai fazer.
                string designation =
                    embarked.AIHasDesignatedMission
                        ? $"\n{embarked.AIDesignatedMissionIntent} " +
                          $"{embarked.AIDesignatedMissionTargetCell}"
                        : "\nSem destino designado";
                if (GUILayout.Button(
                        $"Destino {index + 1} | " +
                        $"{embarked.UnitDisplayName} #{embarked.InstanceId}" +
                        $"\nVaga fisica {seat.slotIndex + 1}" +
                        designation,
                        GUILayout.MinWidth(170f),
                        GUILayout.Height(54f)))
                {
                    passenger = embarked;
                    AutoDetect();
                    ranking.Clear();
                    selected = null;
                    status =
                        $"Passageiro embarcado: {embarked.name}.";
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void TryUseCurrentSelection()
    {
        UnitManager picked = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<UnitManager>()
            : null;
        if (picked == null)
        {
            status = "O GameObject selecionado nao possui UnitManager.";
            return;
        }

        passenger = picked;
        AutoDetect();
        ranking.Clear();
        selected = null;
        status = passenger.IsEmbarked && transporter != null
            ? $"Passageiro selecionado: {passenger.name}; " +
              $"transportador: {transporter.name}."
            : $"Passageiro selecionado: {passenger.name}; " +
              "nao esta embarcado.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void TryUseCurrentSelectionAsTransporter()
    {
        UnitManager picked = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<UnitManager>()
            : null;
        if (picked == null)
        {
            status =
                "O GameObject selecionado nao possui UnitManager.";
            return;
        }

        transporter = picked;
        if (passenger != null
            && (!passenger.IsEmbarked
                || passenger.EmbarkedTransporter != transporter))
            passenger = null;
        AutoDetect();
        ranking.Clear();
        selected = null;
        status =
            $"Transportador selecionado: {transporter.name}. " +
            "Escolha um de seus passageiros.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void AutoDetectRuntimeSelection()
    {
        UnitManager runtimeUnit = null;
        string runtimeSource = "";
        if (Application.isPlaying)
        {
            TurnStateManager turnState =
                FindAnyObjectByType<TurnStateManager>();
            runtimeUnit = turnState != null
                ? turnState.SelectedUnit
                : null;
            runtimeSource = "TurnStateManager.SelectedUnit";
            if (runtimeUnit == null)
            {
                AIController ai =
                    FindAnyObjectByType<AIController>();
                if (ai != null
                    && ai.TryGetDebugStepPendingUnit(
                        out UnitManager pending))
                {
                    runtimeUnit = pending;
                    runtimeSource = "batch preparado pelo F11";
                }
            }
        }

        if (runtimeUnit != null)
        {
            if (runtimeUnit.IsEmbarked
                && runtimeUnit.EmbarkedTransporter != null)
            {
                passenger = runtimeUnit;
                transporter = runtimeUnit.EmbarkedTransporter;
            }
            else if (TryResolveFirstEmbarkedPassenger(
                         runtimeUnit,
                         out UnitManager detectedPassenger))
            {
                transporter = runtimeUnit;
                passenger = detectedPassenger;
            }
            else
            {
                status =
                    $"{runtimeUnit.name} ({runtimeSource}) nao e " +
                    "passageiro embarcado nem transportador carregado.";
                return;
            }

            AutoDetect();
            ranking.Clear();
            selected = null;
            Selection.activeGameObject = passenger.gameObject;
            EditorGUIUtility.PingObject(passenger.gameObject);
            SceneView.FrameLastActiveSceneView();
            SceneView.RepaintAll();
            status =
                $"Passageiro runtime ({runtimeSource}): " +
                $"{passenger.name}; transportador: {transporter.name}.";
            return;
        }

        AutoDetect();
        status = Application.isPlaying
            ? "A IA nao possui batch runtime detectavel neste instante."
            : "Contexto detectado. Fora do Play Mode, use os botoes de selecao.";
    }

    private static bool TryResolveFirstEmbarkedPassenger(
        UnitManager candidateTransporter,
        out UnitManager detectedPassenger)
    {
        detectedPassenger = null;
        IReadOnlyList<UnitTransportSeatRuntime> seats =
            candidateTransporter != null
                ? candidateTransporter.TransportedUnitSlots
                : null;
        UnitTransportSeatRuntime oldestSeat = null;
        for (int i = 0; seats != null && i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat?.embarkedUnit == null
                || !seat.embarkedUnit.IsEmbarked)
                continue;
            if (oldestSeat == null
                || ResolveEmbarkedTurn(seat)
                    < ResolveEmbarkedTurn(oldestSeat))
                oldestSeat = seat;
        }

        detectedPassenger = oldestSeat?.embarkedUnit;
        return detectedPassenger != null;
    }

    private static int ResolveEmbarkedTurn(
        UnitTransportSeatRuntime seat) =>
        seat != null && seat.embarkedOnTurn >= 0
            ? seat.embarkedOnTurn
            : int.MaxValue;

    private void Calculate()
    {
        SyncEditorUnitRegistryForSensors();
        ranking.Clear();
        selected = null;
        rejectedByRange.Clear();
        // O memo vale por cálculo: entre um clique e outro o tabuleiro pode ter
        // mudado, e uma rota velha seria uma resposta provisória disfarçada de
        // confirmada.
        routeCache.Clear();
        routeFloods = 0;
        routeBudgetExhausted = false;
        AutoDetect();
        if (transporter == null
            || map == null
            || terrainDatabase == null)
        {
            status = "Contexto incompleto.";
            return;
        }
        if (passenger != null
            && (!passenger.IsEmbarked
                || passenger.EmbarkedTransporter != transporter))
        {
            status =
                "O passageiro selecionado nao esta embarcado neste transportador.";
            return;
        }

        FogKnowledgeSnapshot fogKnowledge = null;

        MelhorDesembarqueResult result;
        try
        {
            result = MelhorDesembarqueService.Evaluate(
                new MelhorDesembarqueRequest
                {
                    transporter = transporter,
                    passengerFilter = passenger,
                    map = map,
                    terrainDatabase = terrainDatabase,
                    movementBudget = transporter.RemainingMovementPoints,
                    resolvePassengerTarget = TryResolvePassengerTargetAndRoute,
                    // A MESMA regra do runtime (IsConfirmedVisibleOrExploredCellForAI):
                    // o transportador pode terminar em terreno visivel OU ja
                    // explorado; preto nao. Sem este gate a bancada aprovava LZ
                    // que o jogo recusa — duas ferramentas discordando da mesma
                    // cena, que e o pior resultado possivel para um diagnostico.
                    allowTransporterCell = BuildTransporterCellGate(fogKnowledge),
                    // O ponto de QUEDA tambem respeita o conhecimento quando a
                    // nevoa esta ligada. Sem isto a janela desenhava o
                    // passageiro caindo em cima do objetivo mesmo com o
                    // objetivo no escuro — e a diferenca entre as duas leituras
                    // e justamente o que se quer ver.
                    allowDisembarkCell = BuildTransporterCellGate(fogKnowledge)
                });
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        ranking.AddRange(result.ranking);
        selected = ranking.Count > 0 ? ranking[0] : null;
        status = ranking.Count > 0
            ? $"{ranking.Count} LZ(s). Melhor: {selected.cell}, " +
              $"{selected.delivered} passageiro(s), rota restante {selected.totalRouteCost}. " +
              $"({routeFloods} mapa(s) de rota)"
            : $"Nenhum LZ com desembarque e rota comprovados. " +
              $"({routeFloods} mapa(s) de rota)";
        if (routeBudgetExhausted)
            status +=
                $" ATENÇÃO: teto de {maxRouteFloods} mapas de rota atingido; " +
                "o ranking está PARCIAL. Reduza o horizonte de rota ou fixe " +
                "um hex desejado para restringir os alvos avaliados.";
        SceneView.RepaintAll();
    }

    private static void SyncEditorUnitRegistryForSensors()
    {
        if (Application.isPlaying)
            return;

        UnitManager.AllActive.Clear();
        UnitManager[] units = FindObjectsByType<UnitManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit != null && unit.gameObject.activeInHierarchy)
                UnitManager.AllActive.Add(unit);
        }
    }

    private bool TryResolvePassengerTargetAndRoute(
        UnitManager passenger,
        Vector3Int from,
        out Vector3Int target,
        out int routeCost)
    {
        bool ok = TryResolvePassengerTargetAndRouteUncapped(
            passenger, from, out target, out routeCost);

        // TETO DE ROTA RESTANTE — o mesmo corte que o courier aplica.
        //
        // Sem ele a bancada ranqueava LZ com o passageiro ainda a 15 hexes do
        // alvo, e o vencedor da janela nao era o vencedor do jogo. O runtime
        // passa dropOffRange como maxRemainingRouteCost e recusa qualquer rota
        // acima dele (AIController.MelhorDesembarque.cs:163).
        if (ok && routeCost > ResolveBenchDropOffRange())
        {
            Vector3Int rejected = from;
            rejected.z = 0;
            if (!rejectedByRange.Contains(rejected))
                rejectedByRange.Add(rejected);
            return false;
        }
        return ok;
    }

    private bool TryResolvePassengerTargetAndRouteUncapped(
        UnitManager passenger,
        Vector3Int from,
        out Vector3Int target,
        out int routeCost)
    {
        target = Vector3Int.zero;
        routeCost = int.MaxValue;
        if (TryResolveManualTargetForPassenger(
                passenger,
                out Vector3Int manualTarget))
        {
            return TryRouteTo(
                passenger, from, manualTarget,
                out target, out routeCost);
        }

        if (TryResolveDesignatedMissionTarget(
                passenger,
                out Vector3Int designatedTarget)
            && TryRouteTo(
                passenger,
                from,
                designatedTarget,
                out target,
                out routeCost))
            return true;

        string planName = passenger.AIAssignedPlanName;
        ConstructionManager best = null;
        int bestCost = int.MaxValue;
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.TeamId == passenger.TeamId)
                continue;
            if (!string.IsNullOrWhiteSpace(planName)
                && !string.Equals(construction.Sector.ToString(), planName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!TryRouteTo(passenger, from, construction.CurrentCellPosition, out _, out int cost))
                continue;
            if (cost < bestCost)
            {
                best = construction;
                bestCost = cost;
            }
        }

        // Se o setor persistido nao resolveu uma construcao, cai para qualquer
        // capturavel inimigo com rota comprovada.
        if (best == null && !string.IsNullOrWhiteSpace(planName))
        {
            foreach (ConstructionManager construction in ConstructionManager.AllActive)
            {
                if (construction == null || construction.TeamId == passenger.TeamId)
                    continue;
                if (!TryRouteTo(passenger, from, construction.CurrentCellPosition, out _, out int cost))
                    continue;
                if (cost < bestCost) { best = construction; bestCost = cost; }
            }
        }

        if (best == null)
            return false;
        target = best.CurrentCellPosition;
        target.z = 0;
        routeCost = bestCost;
        return true;
    }

    private bool TryRouteTo(
        UnitManager passenger,
        Vector3Int from,
        Vector3Int rawTarget,
        out Vector3Int target,
        out int cost)
    {
        target = rawTarget;
        target.z = 0;
        from.z = 0;
        cost = int.MaxValue;
        if (from == target)
        {
            cost = 0;
            return true;
        }
        Dictionary<Vector3Int, int> reverse =
            GetOrBuildReverseRoute(passenger, target);
        return reverse != null && reverse.TryGetValue(from, out cost);
    }

    /// <summary>
    /// O mapa reverso depende só de (passageiro, alvo) — nunca da LZ nem do
    /// spot de onde a pergunta veio. Memorizar por esse par troca
    /// "LZs × spots × construções" inundações por uma por par.
    /// </summary>
    private Dictionary<Vector3Int, int> GetOrBuildReverseRoute(
        UnitManager routePassenger,
        Vector3Int target)
    {
        var key = (
            routePassenger != null ? routePassenger.InstanceId : 0,
            target);
        if (routeCache.TryGetValue(
                key,
                out Dictionary<Vector3Int, int> cached))
            return cached;

        if (routeFloods >= maxRouteFloods)
        {
            routeBudgetExhausted = true;
            return null;
        }

        routeFloods++;
        EditorUtility.DisplayProgressBar(
            "Melhor LZ de Desembarque",
            $"Mapa de rota {routeFloods}/{maxRouteFloods} — alvo {target}",
            routeFloods / (float)maxRouteFloods);
        Dictionary<Vector3Int, int> built =
            UnitMovementPathRules.CalculateMovementCostMap(
                map, routePassenger, target, routeHorizon, terrainDatabase);
        routeCache[key] = built;
        return built;
    }

    private bool TryResolveManualTargetForPassenger(
        UnitManager selectedPassenger,
        out Vector3Int manualTarget)
    {
        manualTarget = Vector3Int.zero;
        if (transporter == null || selectedPassenger == null)
            return false;

        var ordered = new List<UnitTransportSeatRuntime>();
        IReadOnlyList<UnitTransportSeatRuntime> seats =
            transporter.TransportedUnitSlots;
        for (int i = 0; seats != null && i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat?.embarkedUnit != null
                && seat.embarkedUnit.IsEmbarked)
                ordered.Add(seat);
        }
        ordered.Sort((a, b) =>
        {
            int byTurn = ResolveEmbarkedTurn(a)
                .CompareTo(ResolveEmbarkedTurn(b));
            if (byTurn != 0)
                return byTurn;
            int bySlot = a.slotIndex.CompareTo(b.slotIndex);
            return bySlot != 0
                ? bySlot
                : a.seatIndex.CompareTo(b.seatIndex);
        });

        int index = ordered.FindIndex(
            seat => seat.embarkedUnit == selectedPassenger);
        if (index == 0 && hasPickedTargetCell)
        {
            manualTarget = pickedTargetCell;
            return true;
        }
        if (index == 1 && hasSecondPickedTargetCell)
        {
            manualTarget = secondPickedTargetCell;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Destino declarado do passageiro — SO A COORDENADA.
    ///
    /// <para>"O transportador e alheio a missao da carga. Nao importa se ela vai
    /// capturar, fazer pressao, etc. Ele so quer saber da coordenada."
    /// (autor, 2026-08-07)</para>
    ///
    /// <para>A versao anterior fazia tres coisas que nao sao da conta do
    /// transporte: exigia o verbo <c>Capture</c>, procurava uma construcao que
    /// casasse com o id ou a celula, e ainda checava o DONO dessa construcao.
    /// Com <c>Mission Intent = Pressure</c> e alvo (20,6) declarado no
    /// Inspector, a janela dizia "Sem destino designado" — porque nenhuma das
    /// tres condicoes tinha a ver com a pergunta que estava sendo feita.</para>
    /// </summary>
    private static bool TryResolveDesignatedMissionTarget(
        UnitManager selectedPassenger,
        out Vector3Int target)
    {
        target = Vector3Int.zero;
        if (selectedPassenger == null
            || !selectedPassenger.AIHasDesignatedMission)
            return false;

        target = selectedPassenger.AIDesignatedMissionTargetCell;
        target.z = 0;
        return true;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        HandleTargetCellPicking();
        DrawTriageGizmos();
        DrawDeliveryBandGizmos();
        if (map == null || ranking.Count == 0)
            return;

        int maxDelivered = 1;
        int maxAbsProgress = 1;
        foreach (MelhorDesembarqueLzScore lz in ranking)
        {
            maxDelivered = Mathf.Max(maxDelivered, lz.delivered);
            maxAbsProgress = Mathf.Max(maxAbsProgress, Mathf.Abs(lz.routeProgress));
        }

        if (view != DebugView.SpotsPassageiros)
        {
            foreach (MelhorDesembarqueLzScore lz in ranking)
            {
                // Paleta com CINCO significados e cinco cores que nao brigam.
                //
                // Antes a rampa negativa comecava em LARANJA, o neutro era
                // AMARELO e o vencedor era OURO — tres sentidos dentro da mesma
                // faixa de matiz, num overlay que se le de relance. O laranja
                // ja e do vencedor; a rampa do "pior" sai dele.
                //
                // E o neutro perdeu a cor de propósito: routeProgress == 0 e
                // "nao tenho opiniao", nao um alerta. Cinza diz isso; amarelo
                // saturado competia com o que importa.
                //
                //   verde     aproxima do alvo
                //   cinza     indiferente
                //   vermelho  afasta
                //   ouro      o vencedor do ranking
                //   ciano     preview de onde o passageiro cai
                Color color = lz.routeProgress > 0
                    ? Color.Lerp(new Color(0.35f, 0.85f, 0.35f, 0.6f),
                        new Color(0f, 0.6f, 0f, 0.9f),
                        lz.routeProgress / (float)maxAbsProgress)
                    : lz.routeProgress < 0
                        ? Color.Lerp(new Color(1f, 0.45f, 0.45f, 0.6f),
                            new Color(0.65f, 0f, 0f, 0.9f),
                            -lz.routeProgress / (float)maxAbsProgress)
                        : new Color(0.62f, 0.62f, 0.62f, 0.45f);
                if (lz == ranking[0])
                    color = new Color(1f, 0.75f, 0.05f, 0.95f);
                Handles.color = color;
                Vector3 world = map.GetCellCenterWorld(lz.cell);
                Handles.DrawSolidDisc(world, Vector3.back, lz == ranking[0] ? 0.32f : 0.24f);
                // O neutro deixou de ser amarelo claro, entao texto preto nele
                // virou ilegivel. So o vencedor (ouro) ainda pede preto.
                Color textColor = lz == ranking[0]
                    ? Color.black
                    : Color.white;
                Handles.Label(
                    world,
                    $"{lz.displayScore}\n{lz.delivered}p R{lz.totalRouteCost}",
                    ScoreLabelStyle(textColor));
            }
        }

        MelhorDesembarqueLzScore shown = selected ?? ranking[0];
        // REGRA SIMPLES, e ela e a doutrina desta janela:
        //
        //   sem nevoa   LZ + desembarque EM CIMA do objetivo
        //   com nevoa   LZ + desembarque em terreno VISIVEL proximo do objetivo
        //
        // A bolinha nao some com nevoa — ela MUDA DE LUGAR, e e essa diferenca
        // que a janela existe para mostrar. Quem faz a bolinha andar e o
        // allowDisembarkCell la na request: com nevoa ligada, so celula
        // conhecida serve de ponto de queda.
        if (view != DebugView.MelhorLZ && shown != null)
        {
            for (int i = 0; i < shown.spots.Count; i++)
            {
                MelhorDesembarqueSpotScore spot = shown.spots[i];
                Vector3 world = map.GetCellCenterWorld(spot.option.disembarkCell);
                Color color = new Color(0.05f, 0.85f, 1f, 0.9f);
                Handles.color = color;
                Handles.DrawSolidDisc(world, Vector3.back, 0.20f);
                Handles.Label(
                    world,
                    $"#{spot.option.passengerUnit.InstanceId}\nR{spot.routeCost}",
                    ScoreLabelStyle(Color.black, 12));
                Handles.DrawDottedLine(
                    world, map.GetCellCenterWorld(spot.target), 5f);
            }
        }

        if (hasPickedTargetCell)
        {
            Color targetColor = new Color(1f, 0.15f, 0.15f, 0.95f);
            Handles.color = targetColor;
            Vector3 targetWorld = map.GetCellCenterWorld(pickedTargetCell);
            Handles.DrawWireDisc(targetWorld, Vector3.back, 0.34f);
            Handles.Label(
                targetWorld + Vector3.up * 0.36f,
                "ALVO VAGA 1",
                LabelStyle(targetColor));
        }
        if (hasSecondPickedTargetCell)
        {
            Color targetColor =
                new Color(0.85f, 0.15f, 1f, 0.95f);
            Handles.color = targetColor;
            Vector3 targetWorld =
                map.GetCellCenterWorld(secondPickedTargetCell);
            Handles.DrawWireDisc(
                targetWorld, Vector3.back, 0.34f);
            Handles.Label(
                targetWorld + Vector3.up * 0.36f,
                "ALVO VAGA 2",
                LabelStyle(targetColor));
        }
    }

    private void HandleTargetCellPicking()
    {
        if ((!pickingTargetCell && !pickingSecondTargetCell)
            || map == null)
            return;

        Event evt = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        Plane plane = new Plane(Vector3.forward, map.transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 world = ray.GetPoint(enter);
            hoverCell = map.WorldToCell(world);
            hoverCell.z = 0;
            Handles.color = pickingSecondTargetCell
                ? new Color(0.85f, 0.15f, 1f, 0.95f)
                : new Color(1f, 0.75f, 0.15f, 0.95f);
            Handles.DrawWireDisc(map.GetCellCenterWorld(hoverCell), Vector3.back, 0.32f);
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (evt.type != EventType.MouseDown || evt.button != 0 || evt.alt)
            return;

        bool selectingSecond = pickingSecondTargetCell;
        if (selectingSecond)
        {
            secondPickedTargetCell = hoverCell;
            secondPickedTargetCell.z = 0;
            hasSecondPickedTargetCell = true;
        }
        else
        {
            pickedTargetCell = hoverCell;
            pickedTargetCell.z = 0;
            hasPickedTargetCell = true;
        }
        pickingTargetCell = false;
        pickingSecondTargetCell = false;
        ranking.Clear();
        selected = null;
        status = selectingSecond
            ? $"Hex desejado da vaga 2: {secondPickedTargetCell}."
            : $"Hex desejado da vaga 1: {pickedTargetCell}.";
        evt.Use();
        Repaint();
        SceneView.RepaintAll();
    }

    private static GUIStyle LabelStyle(Color color) =>
        new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = color }
        };

    private static GUIStyle ScoreLabelStyle(Color color, int fontSize = 14) =>
        new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            normal = { textColor = color }
        };

    // =====================================================================
    // TRIAGEM DE LOCAIS ENTREGAVEIS
    //
    // Pergunta, para cada construcao registrada no mapa: "se o transportador
    // ESTIVESSE aqui, ele conseguiria desembarcar um passageiro?"
    //
    // Nao teleporta nada — PodeDesembarcarSensor.CollectOptionsFromCell ja
    // aceita uma celula hipotetica, entao a consulta e pura e nao mexe no
    // tabuleiro.
    //
    // Por que isto e a PRIMEIRA triagem, e nao um filtro no fim: desembarque
    // e sempre no hex ADJACENTE (nao existe desembarque de movimento zero no
    // mesmo hex). Entao uma construcao cujos vizinhos todos recusam o
    // passageiro nao e "dificil de entregar" — ela e INENTREGAVEL por
    // transporte, e so pode ser alcancada a pe. Gastar ranking, rota e
    // envelope com ela e trabalho jogado fora.
    //
    // Duas construcoes lado a lado se aceitam mutuamente: de cima de uma, a
    // outra e vizinha e vale como destino. Uma construcao cercada de terreno
    // que o passageiro nao entra nao aceita ninguem.
    // =====================================================================
    [SerializeField] private string triageStatus = string.Empty;
    private readonly List<Vector3Int> triageDeliverable = new List<Vector3Int>();
    private readonly List<Vector3Int> triageRejected = new List<Vector3Int>();

    /// <summary>
    /// Zera a bancada inteira: contexto, resultados, triagem, caches e modos de
    /// selecao. Existe porque estado sobrando entre dois cenarios e a forma mais
    /// facil de ler um resultado como se fosse de outro tabuleiro — e a janela
    /// guarda cache de rota, que sobrevive a troca de unidade.
    /// </summary>
    /// <summary>
    /// Copia o conhecimento de nevoa do slot do TRANSPORTADOR — e do
    /// transportador de proposito: e ele quem precisa terminar o movimento numa
    /// celula legal. O passageiro nao escolhe onde o casco para.
    /// </summary>
    /// <summary>
    /// Portao de celula do transportador, espelhando o runtime.
    ///
    /// <para>Em Play chama os MESMOS metodos do MatchController que a IA chama —
    /// paridade garantida, nao aproximada. Em Edit Mode nao existe estado
    /// corrente, entao usa o bake da rodada 0: <c>KnownCells</c> e o que aquele
    /// slot ja conhecia quando o tabuleiro foi cozido.</para>
    ///
    /// <para>Sem nevoa aplicada devolve <c>null</c>, e o servico deixa passar
    /// tudo — util para ver a zona teorica, enganoso para prever a IA.</para>
    /// </summary>
    /// <summary>
    /// Mesmo teto que o courier aplica: Operational do passageiro. A bancada
    /// existe para prever a IA — sem este corte ela ranqueava LZ que o jogo
    /// recusa, e o vencedor da janela nao era o vencedor do jogo.
    /// </summary>
    private int ResolveBenchDropOffRange()
    {
        if (dropOffRangeOverride >= 0)
            return dropOffRangeOverride;
        UnitManager reference = passenger ?? ResolvePassengerAtSeat(0);
        int tactical = reference != null
            ? Mathf.Max(1, reference.MaxMovementPoints)
            : 3;
        return tactical * 2;
    }

    private System.Func<Vector3Int, bool> BuildTransporterCellGate(
        FogKnowledgeSnapshot fogKnowledge)
    {
        // Sempre nulo: a bancada nao filtra por conhecimento.
        {
            return null;
        }

        if (Application.isPlaying && matchController != null)
            return cell =>
            {
                cell.z = 0;
                return matchController.IsCellVisibleForActiveTeam(cell)
                    || matchController.IsCellExploredBySlot(
                        matchController.ActiveSlotId, cell);
            };

        if (fogKnowledge == null)
            return null;

        return cell =>
        {
            cell.z = 0;
            return fogKnowledge.KnownCells.Contains(cell)
                || fogKnowledge.GeographicallyVisibleCells.Contains(cell);
        };
    }

    private bool TryCopyFogKnowledge(out FogKnowledgeSnapshot fogKnowledge)
    {
        fogKnowledge = null;
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (matchController == null)
        {
            status = "MatchController indisponivel para consultar a nevoa.";
            return false;
        }

        PlayerSlotId observerSlot = PlayerSlotId.FromIndex(transporter.SlotIndex);
        bool copied = Application.isPlaying
            ? matchController.TryCopyConfirmedFogKnowledgeSnapshotForSlot(
                observerSlot, map, out fogKnowledge, out string reason)
            : matchController.TryCopyRoundZeroFogKnowledgeSnapshotForSlot(
                observerSlot, map, out fogKnowledge, out reason);
        if (!copied || fogKnowledge == null)
        {
            status = reason;
            return false;
        }
        return true;
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
        ranking.Clear();
        selected = null;
        status = bakeResult
            + " O Melhor Desembarque usara este bake com 'Aplicar névoa' ligado.";
        SceneView.RepaintAll();
        Repaint();
    }

    private void ClearAll()
    {
        passenger = null;
        transporter = null;

        hasPickedTargetCell = false;
        pickedTargetCell = Vector3Int.zero;
        hasSecondPickedTargetCell = false;
        secondPickedTargetCell = Vector3Int.zero;
        pickingTargetCell = false;
        pickingSecondTargetCell = false;

        ranking.Clear();
        selected = null;
        routeCache.Clear();
        rejectedByRange.Clear();
        routeFloods = 0;
        routeBudgetExhausted = false;

        triageDeliverable.Clear();
        triageRejected.Clear();
        triageStatus = string.Empty;
        bandTactical.Clear();
        bandOperational.Clear();
        bandFogged.Clear();
        bandStatus = string.Empty;
        hasAnchor = false;
        anchorStatus = string.Empty;

        scroll = Vector2.zero;
        status = "Selecione o passageiro; o hex desejado e opcional.";

        SceneView.RepaintAll();
        Repaint();
    }

    /// <summary>
    /// O hex desejado ESPELHA a missao do passageiro daquela vaga.
    ///
    /// <para>"No hex desejado tem que auto-espelhar a missao do cara. Se ele
    /// realmente nao tinha destino — 'ah, eu pedi taxi porque estava a toa' —
    /// entao o transportador usa o Melhor LZ para achar um para ele."
    /// (autor, 2026-08-07)</para>
    ///
    /// <para>Tres estados, e o rotulo diz qual e qual:
    /// <b>(missao)</b> o passageiro declarou destino e a bancada obedece;
    /// <b>(manual)</b> alguem escolheu um hex a mao, e a escolha vence a missao
    /// — e para isso que o modo de picking existe; <b>(livre)</b> ninguem sabe
    /// para onde, e o ranking procura o melhor destino sozinho.</para>
    ///
    /// <para>O pick manual NAO sobrescreve a missao da unidade: ele so manda
    /// nesta consulta. Limpar volta para o espelho.</para>
    /// </summary>
    private void DrawDesiredHexRow(
        string label,
        int seatIndex,
        ref bool hasManualPick,
        ref Vector3Int manualCell)
    {
        UnitManager seatPassenger = ResolvePassengerAtSeat(seatIndex);
        Vector3Int missionCell = Vector3Int.zero;
        bool hasMission =
            !hasManualPick
            && TryResolveDesignatedMissionTarget(
                seatPassenger, out missionCell);

        string origin = hasManualPick
            ? " (manual)"
            : hasMission ? " (missão)" : " (livre)";

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector3IntField(
                label + origin,
                hasManualPick
                    ? manualCell
                    : hasMission ? missionCell : Vector3Int.zero);
        using (new EditorGUI.DisabledScope(!hasManualPick))
        {
            if (GUILayout.Button("Limpar", GUILayout.Width(55f)))
            {
                hasManualPick = false;
                manualCell = Vector3Int.zero;
                ranking.Clear();
                selected = null;
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private UnitManager ResolvePassengerAtSeat(int seatIndex)
    {
        if (transporter == null)
            return null;
        // Mesma ordem da grade de vagas: so embarcados, ordenados por turno de
        // embarque e depois por slot. Se divergir da grade, o rotulo mostra a
        // missao de um passageiro e o calculo usa a de outro.
        IReadOnlyList<UnitTransportSeatRuntime> seats =
            transporter.TransportedUnitSlots;
        if (seats == null)
            return null;

        var embarked = new List<UnitTransportSeatRuntime>();
        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat?.embarkedUnit != null && seat.embarkedUnit.IsEmbarked)
                embarked.Add(seat);
        }
        embarked.Sort((a, b) =>
        {
            int byTurn = ResolveEmbarkedTurn(a).CompareTo(ResolveEmbarkedTurn(b));
            return byTurn != 0
                ? byTurn
                : a.slotIndex.CompareTo(b.slotIndex);
        });

        if (seatIndex < 0 || seatIndex >= embarked.Count)
            return null;
        return embarked[seatIndex].embarkedUnit;
    }

    // =====================================================================
    // VALIDACOES — perguntas que se responde ANTES de acreditar no ranking.
    //
    // Ficam depois do calculo de proposito: cada uma checa uma premissa que o
    // ranking assume em silencio. Quando o ranking devolve resultado estranho,
    // a ordem de investigacao e descer por aqui, nao reler a nota.
    // =====================================================================
    private void DrawValidationsSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Validações", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(transporter == null || map == null))
        {
            if (GUILayout.Button(
                    "1) Triagem — locais que ACEITAM entrega",
                    GUILayout.Height(22f)))
                RunDeliverableLocationTriage();
        }
        if (!string.IsNullOrEmpty(triageStatus))
            EditorGUILayout.HelpBox(triageStatus, MessageType.None);

        bool bandsPainted = bandTactical.Count > 0
            || bandOperational.Count > 0
            || bandFogged.Count > 0;
        using (new EditorGUI.DisabledScope(map == null))
        {
            // Alterna: o overlay tapa o ranking, e olhar um DEPOIS do outro no
            // mesmo tabuleiro e a comparacao que interessa.
            if (GUILayout.Button(
                    bandsPainted
                        ? "2) Apagar Tactical/Operational da entrega"
                        : "2) Pintar Tactical/Operational da entrega",
                    GUILayout.Height(22f)))
            {
                if (bandsPainted)
                    ClearDeliveryBands();
                else
                    PaintDeliveryBands();
            }
        }
        if (!string.IsNullOrEmpty(bandStatus))
            EditorGUILayout.HelpBox(bandStatus, MessageType.None);

        using (new EditorGUI.DisabledScope(
                   transporter == null || passenger == null || map == null))
        {
            if (GUILayout.Button(
                    "3) Âncora do courier — para onde ele vai",
                    GUILayout.Height(22f)))
                ResolveCourierAnchor();
        }
        if (!string.IsNullOrEmpty(anchorStatus))
            EditorGUILayout.HelpBox(anchorStatus, MessageType.None);
    }

    /// <summary>
    /// A ancora e ATRIBUICAO DA IA, nao da ferramenta — por isso esta janela
    /// nao a recalcula: chama o mesmo
    /// <c>AIController.TryResolveDeliveryZoneAnchor</c> que o courier chama, com
    /// o mesmo predicado de nevoa. Reimplementar aqui daria duas respostas para
    /// a mesma pergunta, que e o defeito que a nevoa e o teto de rota ja
    /// expuseram duas vezes hoje.
    /// </summary>
    private void ResolveCourierAnchor()
    {
        hasAnchor = false;
        anchorStatus = string.Empty;

        if (!TryResolveEffectiveDeliveryTarget(out Vector3Int targetCell))
        {
            anchorStatus =
                "Sem destino: o passageiro nao tem missao e nenhum hex foi "
                + "escolhido a mao.";
            SceneView.RepaintAll();
            return;
        }

        FogKnowledgeSnapshot fog = null;

        Vector3Int from = transporter.CurrentCellPosition;
        from.z = 0;

        hasAnchor = AIController.TryResolveDeliveryZoneAnchor(
            map,
            terrainDatabase,
            transporter,
            passenger,
            targetCell,
            from,
            BuildTransporterCellGate(fog),
            out anchorCell,
            out int walkCost,
            out bool isTactical,
            out string mode);

        anchorStatus = hasAnchor
            ? mode == "avancar"
                ? $"AVANÇAR → {anchorCell} (magenta). Nenhuma celula CONHECIDA "
                  + $"na zona de {targetCell}: entra no escuro na direcao dela."
                : $"ENTREGA → {anchorCell} (magenta), "
                  + $"{(isTactical ? "Tactical" : "Operational")}; o passageiro "
                  + $"anda {walkCost} de la ate {targetCell}."
            : $"Nenhuma ancora para {targetCell}: nem zona conhecida nem "
              + "fronteira alcancavel.";

        Debug.Log($"[AncoraCourier] {anchorStatus}");
        SceneView.RepaintAll();
        Repaint();
    }

    // =====================================================================
    // BANDAS DA ENTREGA
    //
    // Emula o teleporte: poe o PASSAGEIRO no destino e pergunta de onde ele
    // chegaria ali sozinho. E a zona de entrega — "reverse: teleport the unit
    // onto the target, that area is the drop zone" (CLAUDE.md).
    //
    // Nao move nada. CalculateMovementCostMap e um flood reverso a partir do
    // alvo com o movimento do passageiro; o teleporte e figura de linguagem.
    //
    // Cores: as do projeto, verde = Tactical (uma rodada), azul = Operational
    // (duas). Nao inventar cor nova nem reusar uma que ja tem outro sentido.
    // =====================================================================
    private readonly List<Vector3Int> bandTactical = new List<Vector3Int>();
    private readonly List<Vector3Int> bandOperational = new List<Vector3Int>();
    // Celulas DA ZONA que o transportador nao conhece. Geometricamente serviriam
    // de LZ; hoje nao servem, porque o casco nao pode terminar movimento no
    // preto. E a previsao que o autor pediu: "onde ele vai entrar na nevoa e
    // nao fara a entrega".
    private readonly List<Vector3Int> bandFogged = new List<Vector3Int>();
    // Ancora do courier: PARA ONDE ele vai este turno. Nao e calculada aqui —
    // vem do mesmo metodo que o jogo chama.
    private bool hasAnchor;
    private Vector3Int anchorCell;
    [SerializeField] private string anchorStatus = string.Empty;
    [SerializeField] private string bandStatus = string.Empty;

    private void ClearDeliveryBands()
    {
        bandTactical.Clear();
        bandOperational.Clear();
        bandFogged.Clear();
        bandStatus = string.Empty;
        SceneView.RepaintAll();
        Repaint();
    }

    private void PaintDeliveryBands()
    {
        bandTactical.Clear();
        bandOperational.Clear();
        bandFogged.Clear();
        bandStatus = string.Empty;

        if (passenger == null || map == null || terrainDatabase == null)
        {
            bandStatus = "Sem passageiro, tilemap ou Terrain Database.";
            return;
        }

        if (!TryResolveEffectiveDeliveryTarget(out Vector3Int targetCell))
        {
            bandStatus =
                "Sem destino: o passageiro nao tem missao e nenhum hex foi "
                + "escolhido a mao. Escolha um destino para pintar a zona.";
            return;
        }

        int tactical = Mathf.Max(1, passenger.MaxMovementPoints);
        int operational = tactical * 2;

        Dictionary<Vector3Int, int> reach =
            UnitMovementPathRules.CalculateMovementCostMap(
                map, passenger, targetCell, operational, terrainDatabase);
        if (reach == null || reach.Count == 0)
        {
            bandStatus =
                $"Alvo {targetCell}: o passageiro nao alcanca NENHUMA celula a "
                + "pe a partir dali. Entrega impossivel por transporte.";
            SceneView.RepaintAll();
            return;
        }

        // A zona e geometria do PASSAGEIRO; a usabilidade e conhecimento do
        // TRANSPORTADOR — e o casco que precisa terminar o movimento ali. Por
        // isso o filtro de nevoa e o do transportador, o mesmo que o runtime usa.
        FogKnowledgeSnapshot bandFog = null;
        System.Func<Vector3Int, bool> knows = BuildTransporterCellGate(bandFog);

        foreach (KeyValuePair<Vector3Int, int> entry in reach)
        {
            Vector3Int cell = entry.Key;
            cell.z = 0;
            if (knows != null && !knows(cell))
            {
                bandFogged.Add(cell);
                continue;
            }
            if (entry.Value <= tactical)
                bandTactical.Add(cell);
            else
                bandOperational.Add(cell);
        }

        int zoneTotal =
            bandTactical.Count + bandOperational.Count + bandFogged.Count;
        bandStatus =
            $"Alvo {targetCell} · Tactical={tactical} ({bandTactical.Count} verde) "
            + $"· Operational={operational} ({bandOperational.Count} azul)";
        bandStatus += knows == null
            ? " · névoa DESLIGADA: a zona teórica inteira. Ligue para ver o que "
              + "ele pode mesmo usar hoje."
            : $" · {bandFogged.Count} de {zoneTotal} NO ESCURO (amarelo) — "
              + "geometricamente serviriam, mas o casco não pode terminar "
              + "movimento no preto, então não há entrega ali hoje.";
        Debug.Log($"[BandasEntrega] {bandStatus}");
        SceneView.RepaintAll();
        Repaint();
    }

    /// <summary>Destino efetivo: pick manual vence, senao a missao da vaga 1.</summary>
    private bool TryResolveEffectiveDeliveryTarget(out Vector3Int targetCell)
    {
        if (hasPickedTargetCell)
        {
            targetCell = pickedTargetCell;
            targetCell.z = 0;
            return true;
        }
        return TryResolveDesignatedMissionTarget(passenger, out targetCell);
    }

    private void DrawDeliveryBandGizmos()
    {
        if (map == null)
            return;
        DrawBand(bandTactical, new Color(0.25f, 1f, 0.35f, 0.55f));
        DrawBand(bandOperational, new Color(0.35f, 0.6f, 1f, 0.45f));

        // Anel, nao disco: elas NAO sao candidatas. O contorno diz "olhei e
        // descartei", que e diferente de "nunca existiu".
        if (hasAnchor)
        {
            Vector3 world = map.GetCellCenterWorld(anchorCell);
            Handles.color = new Color(1f, 0.2f, 1f, 0.95f);
            Handles.DrawSolidDisc(world, Vector3.forward, 0.20f);
            Handles.DrawWireDisc(world, Vector3.forward, 0.44f);
            Handles.DrawWireDisc(world, Vector3.forward, 0.40f);
        }

        if (showRejectedByRange)
        {
            Handles.color = new Color(1f, 0.3f, 0.25f, 0.8f);
            for (int i = 0; i < rejectedByRange.Count; i++)
                Handles.DrawWireDisc(
                    map.GetCellCenterWorld(rejectedByRange[i]),
                    Vector3.forward, 0.26f);
        }
    }

    private void DrawBand(List<Vector3Int> cells, Color color)
    {
        Handles.color = color;
        for (int i = 0; i < cells.Count; i++)
            Handles.DrawSolidDisc(
                map.GetCellCenterWorld(cells[i]), Vector3.forward, 0.30f);
    }

    private void RunDeliverableLocationTriage()
    {
        triageDeliverable.Clear();
        triageRejected.Clear();
        triageStatus = string.Empty;

        if (transporter == null || map == null)
        {
            triageStatus = "Sem transportador ou tilemap.";
            return;
        }
        if (transporter.IsEmbarked)
        {
            triageStatus =
                "Transportador esta embarcado; a consulta exige casco solto.";
            return;
        }

        TerrainDatabase terrain = terrainDatabase;
        if (terrain == null)
        {
            triageStatus = "Sem Terrain Database.";
            return;
        }

        var options = new List<PodeDesembarcarOption>();
        var seen = new HashSet<Vector3Int>();
        int scanned = 0;

        for (int i = 0; i < ConstructionManager.AllActive.Count; i++)
        {
            ConstructionManager construction = ConstructionManager.AllActive[i];
            if (construction == null)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (!seen.Add(cell))
                continue;
            scanned++;

            bool ok = PodeDesembarcarSensor.CollectOptionsFromCell(
                          transporter,
                          cell,
                          map,
                          terrain,
                          options,
                          out string reason)
                      && options.Count > 0;

            if (ok)
                triageDeliverable.Add(cell);
            else
            {
                triageRejected.Add(cell);
                if (triageRejected.Count <= 8)
                    Debug.Log(
                        $"[TriagemLZ] {cell} REJEITADO — so alcancavel a pe. "
                        + $"motivo={reason}");
            }
        }

        triageStatus =
            $"{scanned} construcao(oes) no mapa · "
            + $"{triageDeliverable.Count} ACEITAM entrega · "
            + $"{triageRejected.Count} so a pe. "
            + "Verde na Scene View = aceita. Vermelho = descartar antes de "
            + "qualquer ranking.";

        Debug.Log("[TriagemLZ] " + triageStatus);
        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawTriageGizmos()
    {
        if (map == null)
            return;
        for (int i = 0; i < triageDeliverable.Count; i++)
            DrawTriageCell(triageDeliverable[i], new Color(0.2f, 1f, 0.3f, 0.9f));
        for (int i = 0; i < triageRejected.Count; i++)
            DrawTriageCell(triageRejected[i], new Color(1f, 0.25f, 0.2f, 0.9f));
    }

    private void DrawTriageCell(Vector3Int cell, Color color)
    {
        Vector3 center = map.GetCellCenterWorld(cell);
        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.forward, 0.42f);
        Handles.DrawWireDisc(center, Vector3.forward, 0.36f);
    }
}
