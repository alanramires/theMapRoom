using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MelhorDesembarqueWindow : EditorWindow
{
    private enum DebugView { Ambas, MelhorLZ, SpotsPassageiros }

    [SerializeField] private UnitManager transporter;
    [SerializeField] private ConstructionManager forcedTarget;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DebugView view = DebugView.Ambas;
    [SerializeField] private int routeHorizon = 120;
    [SerializeField] private bool hasPickedTargetCell;
    [SerializeField] private Vector3Int pickedTargetCell;
    [SerializeField] private bool hasSecondPickedTargetCell;
    [SerializeField] private Vector3Int secondPickedTargetCell;

    private Tilemap map;
    private readonly List<MelhorDesembarqueLzScore> ranking =
        new List<MelhorDesembarqueLzScore>();
    private MelhorDesembarqueLzScore selected;
    private Vector2 scroll;
    private string status = "Selecione um transportador carregado.";
    private bool pickingTargetCell;
    private bool pickingSecondTargetCell;
    private Vector3Int hoverCell;

    [MenuItem("Tools/Transporte/Melhor LZ de Desembarque")]
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
        UnitManager picked = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<UnitManager>()
            : null;
        if (picked != null)
        {
            transporter = picked;
            AutoDetect();
            Repaint();
        }
    }

    private void AutoDetect()
    {
        if (transporter != null && transporter.BoardTilemap != null)
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
            "Consulta pura. Uma passada de Caminhos Validos do transportador; " +
            "cada LZ simula PodeDesembarcar sem mover a unidade.",
            MessageType.Info);

        EditorGUILayout.LabelField("Contexto", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        transporter = (UnitManager)EditorGUILayout.ObjectField(
            "Transportador", transporter, typeof(UnitManager), true);
        forcedTarget = (ConstructionManager)EditorGUILayout.ObjectField(
            new GUIContent("Alvo forcado (opcional)",
                "Vazio: usa o setor do plano do passageiro ou o capturavel com menor rota valida."),
            forcedTarget, typeof(ConstructionManager), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        view = (DebugView)EditorGUILayout.EnumPopup("Visao", view);
        routeHorizon = Mathf.Max(10, EditorGUILayout.IntField("Horizonte de rota", routeHorizon));
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
        if (GUILayout.Button("Auto Detect"))
        {
            AutoDetect();
            status = map != null ? "Contexto detectado." : "Tilemap nao encontrado.";
        }
        GUI.backgroundColor = pickingTargetCell
            ? new Color(1f, 0.75f, 0.2f)
            : Color.white;
        if (GUILayout.Button(pickingTargetCell ? "Clique no Scene View..." : "Escolher Celula"))
        {
            pickingTargetCell = !pickingTargetCell;
            pickingSecondTargetCell = false;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector3IntField(
                "Celula alvo",
                hasPickedTargetCell ? pickedTargetCell : Vector3Int.zero);
        using (new EditorGUI.DisabledScope(!hasPickedTargetCell))
        {
            if (GUILayout.Button("Limpar", GUILayout.Width(55f)))
            {
                hasPickedTargetCell = false;
                hasSecondPickedTargetCell = false;
                ranking.Clear();
                selected = null;
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector3IntField(
                "Segundo local",
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

        using (new EditorGUI.DisabledScope(!hasPickedTargetCell))
        {
            GUI.backgroundColor = pickingSecondTargetCell
                ? new Color(0.85f, 0.35f, 1f)
                : Color.white;
            if (GUILayout.Button(
                    pickingSecondTargetCell
                        ? "Clique no segundo local..."
                        : "Selecione um segundo local"))
            {
                pickingSecondTargetCell = !pickingSecondTargetCell;
                pickingTargetCell = false;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
        }

        using (new EditorGUI.DisabledScope(transporter == null || map == null))
        {
            if (GUILayout.Button("Calcular Melhor LZ de Desembarque", GUILayout.Height(28f)))
                Calculate();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        if (ranking.Count == 0)
            return;

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

        transporter = picked;
        AutoDetect();
        ranking.Clear();
        selected = null;
        status = $"Transportador selecionado: {transporter.name}.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void Calculate()
    {
        SyncEditorUnitRegistryForSensors();
        ranking.Clear();
        selected = null;
        AutoDetect();
        if (transporter == null || map == null || terrainDatabase == null)
        {
            status = "Contexto incompleto.";
            return;
        }

        MelhorDesembarqueResult result = MelhorDesembarqueService.Evaluate(
            new MelhorDesembarqueRequest
            {
                transporter = transporter,
                map = map,
                terrainDatabase = terrainDatabase,
                movementBudget = transporter.RemainingMovementPoints,
                resolvePassengerTarget = TryResolvePassengerTargetAndRoute
            });
        ranking.AddRange(result.ranking);
        selected = ranking.Count > 0 ? ranking[0] : null;
        status = ranking.Count > 0
            ? $"{ranking.Count} LZ(s). Melhor: {selected.cell}, " +
              $"{selected.delivered} passageiro(s), rota restante {selected.totalRouteCost}."
            : "Nenhum LZ com desembarque e rota comprovados.";
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
        if (hasPickedTargetCell)
        {
            Vector3Int manualTarget =
                ResolveManualTargetForPassenger(passenger);
            return TryRouteTo(
                passenger, from, manualTarget,
                out target, out routeCost);
        }
        if (forcedTarget != null)
            return TryRouteTo(passenger, from, forcedTarget.CurrentCellPosition, out target, out routeCost);

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
        if (from == target)
        {
            cost = 0;
            return true;
        }
        Dictionary<Vector3Int, int> reverse =
            UnitMovementPathRules.CalculateMovementCostMap(
                map, passenger, target, routeHorizon, terrainDatabase);
        return reverse.TryGetValue(from, out cost);
    }

    private Vector3Int ResolveManualTargetForPassenger(UnitManager passenger)
    {
        if (!hasSecondPickedTargetCell
            || transporter == null
            || passenger == null)
            return pickedTargetCell;

        var ordered = new List<UnitTransportSeatRuntime>();
        IReadOnlyList<UnitTransportSeatRuntime> seats =
            transporter.TransportedUnitSlots;
        for (int i = 0; seats != null && i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat?.embarkedUnit != null)
                ordered.Add(seat);
        }
        ordered.Sort((a, b) =>
        {
            int turnA = a.embarkedOnTurn >= 0
                ? a.embarkedOnTurn
                : int.MaxValue;
            int turnB = b.embarkedOnTurn >= 0
                ? b.embarkedOnTurn
                : int.MaxValue;
            int byTurn = turnA.CompareTo(turnB);
            if (byTurn != 0) return byTurn;
            int bySlot = a.slotIndex.CompareTo(b.slotIndex);
            return bySlot != 0
                ? bySlot
                : a.seatIndex.CompareTo(b.seatIndex);
        });

        int passengerIndex = ordered.FindIndex(
            seat => seat.embarkedUnit == passenger);
        return passengerIndex >= 1
            ? secondPickedTargetCell
            : pickedTargetCell;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        HandleTargetCellPicking();
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
                "ALVO 1",
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
                "ALVO 2",
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
            hasSecondPickedTargetCell = false;
        }
        pickingTargetCell = false;
        pickingSecondTargetCell = false;
        forcedTarget = null;
        ranking.Clear();
        selected = null;
        status = selectingSecond
            ? $"Segundo local escolhido: {secondPickedTargetCell}."
            : $"Primeiro local escolhido: {pickedTargetCell}.";
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
}
