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

        DrawEmbarkedPassengerGrid();

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector3IntField(
                "Hex desejado — vaga 1",
                hasPickedTargetCell ? pickedTargetCell : Vector3Int.zero);
        using (new EditorGUI.DisabledScope(!hasPickedTargetCell))
        {
            if (GUILayout.Button("Limpar", GUILayout.Width(55f)))
            {
                hasPickedTargetCell = false;
                ranking.Clear();
                selected = null;
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector3IntField(
                "Hex desejado — vaga 2",
                hasSecondPickedTargetCell
                    ? secondPickedTargetCell
                    : Vector3Int.zero);
        using (new EditorGUI.DisabledScope(!hasSecondPickedTargetCell))
        {
            if (GUILayout.Button("Limpar", GUILayout.Width(55f)))
            {
                hasSecondPickedTargetCell = false;
                ranking.Clear();
                selected = null;
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();

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
            if (GUILayout.Button(
                    "1) Triagem — locais que ACEITAM entrega",
                    GUILayout.Height(24f)))
                RunDeliverableLocationTriage();
        }
        if (!string.IsNullOrEmpty(triageStatus))
            EditorGUILayout.HelpBox(triageStatus, MessageType.None);

        using (new EditorGUI.DisabledScope(
                   transporter == null
                   || map == null))
        {
            if (GUILayout.Button("Calcular Melhor LZ de Desembarque", GUILayout.Height(28f)))
                Calculate();
        }

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
                    resolvePassengerTarget = TryResolvePassengerTargetAndRoute
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

        if (TryResolveDesignatedCaptureTarget(
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

    private static bool TryResolveDesignatedCaptureTarget(
        UnitManager selectedPassenger,
        out Vector3Int target)
    {
        target = Vector3Int.zero;
        if (selectedPassenger == null
            || !selectedPassenger.AIHasDesignatedCaptureTarget)
            return false;

        int instanceId =
            selectedPassenger.AIDesignatedCaptureTargetInstanceId;
        Vector3Int designatedCell =
            selectedPassenger.AIDesignatedCaptureTargetCell;
        designatedCell.z = 0;
        foreach (ConstructionManager construction
                 in ConstructionManager.AllActive)
        {
            if (construction == null)
                continue;
            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (construction.InstanceId != instanceId
                && cell != designatedCell)
                continue;
            if (construction.TeamId == selectedPassenger.TeamId)
                return false;
            target = cell;
            return true;
        }
        return false;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        HandleTargetCellPicking();
        DrawTriageGizmos();
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
                Color color = lz.routeProgress > 0
                    ? Color.Lerp(new Color(0.3f, 0.85f, 0.3f, 0.65f),
                        new Color(0f, 0.6f, 0f, 0.9f),
                        lz.routeProgress / (float)maxAbsProgress)
                    : lz.routeProgress < 0
                        ? Color.Lerp(new Color(0.9f, 0.45f, 0.1f, 0.65f),
                            new Color(0.8f, 0.1f, 0.1f, 0.9f),
                            -lz.routeProgress / (float)maxAbsProgress)
                        : new Color(0.9f, 0.85f, 0.1f, 0.75f);
                if (lz == ranking[0])
                    color = new Color(1f, 0.75f, 0.05f, 0.95f);
                Handles.color = color;
                Vector3 world = map.GetCellCenterWorld(lz.cell);
                Handles.DrawSolidDisc(world, Vector3.back, lz == ranking[0] ? 0.32f : 0.24f);
                Color textColor = lz == ranking[0] || lz.routeProgress == 0
                    ? Color.black
                    : Color.white;
                Handles.Label(
                    world,
                    $"{lz.displayScore}\n{lz.delivered}p R{lz.totalRouteCost}",
                    ScoreLabelStyle(textColor));
            }
        }

        MelhorDesembarqueLzScore shown = selected ?? ranking[0];
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
        routeFloods = 0;
        routeBudgetExhausted = false;

        triageDeliverable.Clear();
        triageRejected.Clear();
        triageStatus = string.Empty;

        scroll = Vector2.zero;
        status = "Selecione o passageiro; o hex desejado e opcional.";

        SceneView.RepaintAll();
        Repaint();
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
