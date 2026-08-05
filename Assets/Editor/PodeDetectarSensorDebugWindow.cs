using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeDetectarSensorDebugWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private MatchController matchController;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private bool useGameplaySensorContext = true;
    [SerializeField] private bool logToConsole = true;

    private readonly List<PodeDetectarOption> detectedStealth = new List<PodeDetectarOption>();
    private readonly List<PodeDetectarOption> undetectedStealth = new List<PodeDetectarOption>();
    private readonly List<PodeDetectarOption> spottedCandidates = new List<PodeDetectarOption>();
    private readonly List<PodeDetectarOption> inRangeButLosBlocked = new List<PodeDetectarOption>();
    private readonly HashSet<UnitManager> forcedDetectedIndicatorUnits = new HashSet<UnitManager>();
    private Vector2 windowScroll;
    private string statusMessage = "Ready.";
    private bool hasSelectedLine;
    private Vector3 selectedLineStartWorld;
    private Vector3 selectedLineEndWorld;
    private readonly List<Vector3> selectedLineWorldPoints = new List<Vector3>();
    private Color selectedLineColor = Color.green;
    private string selectedLineLabel = string.Empty;

    [MenuItem("Tools/FoW/Pode Detectar")]
    public static void OpenWindow()
    {
        GetWindow<PodeDetectarSensorDebugWindow>("Pode Detectar");
    }

    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearSelectedLine();
        ClearForcedDetectedIndicators();
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);

        EditorGUILayout.LabelField("Sensor Pode Detectar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scan de proximidade por visao/specialization + LOS. " +
            "Unidades stealth so entram em \"furtivas detectadas\" quando o observador tiver especializacao para detectar stealth naquele dominio/altura.",
            MessageType.Info);

        selectedUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade", selectedUnit, typeof(UnitManager), true);
        turnStateManager = (TurnStateManager)EditorGUILayout.ObjectField("TurnStateManager", turnStateManager, typeof(TurnStateManager), true);
        matchController = (MatchController)EditorGUILayout.ObjectField("MatchController", matchController, typeof(MatchController), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        dpqAirHeightConfig = (DPQAirHeightConfig)EditorGUILayout.ObjectField("DPQ Air Height", dpqAirHeightConfig, typeof(DPQAirHeightConfig), false);
        useGameplaySensorContext = EditorGUILayout.ToggleLeft("Usar contexto do gameplay (MatchController)", useGameplaySensorContext);
        logToConsole = EditorGUILayout.ToggleLeft("Log no Console", logToConsole);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular"))
            RunSimulation();
        if (GUILayout.Button("Limpar"))
            ClearResults();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        EditorGUILayout.Space(6f);
        DrawOptionList("Unidades furtivas detectadas", detectedStealth, MessageType.Info, Color.green);
        EditorGUILayout.Space(6f);
        DrawOptionList("Unidades furtivas nao detectadas", undetectedStealth, MessageType.Warning, Color.red);
        EditorGUILayout.Space(6f);
        DrawOptionList("Candidatos avistados", spottedCandidates, MessageType.None, Color.green);
        EditorGUILayout.Space(6f);
        DrawOptionList("Candidatos no alcance mas nao detectados por LOS", inRangeButLosBlocked, MessageType.Warning, Color.red);

        EditorGUILayout.EndScrollView();
    }

    private void DrawOptionList(string title, List<PodeDetectarOption> items, MessageType emptyMessageType, Color lineColor)
    {
        EditorGUILayout.LabelField($"{title} ({items.Count})", EditorStyles.boldLabel);
        if (items.Count <= 0)
        {
            EditorGUILayout.HelpBox("Nenhum item.", emptyMessageType);
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            PodeDetectarOption item = items[i];
            if (item == null || item.targetUnit == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{i + 1}. {item.targetUnit.name}", EditorStyles.boldLabel);
            if (GUILayout.Button("Desenhar Linha", GUILayout.Width(110f)))
                SelectLineForDrawing(item, lineColor, $"{title}: {item.targetUnit.name}");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Hex", $"{item.targetCell.x},{item.targetCell.y}");
            EditorGUILayout.LabelField("Distancia", $"{item.distance} / alcance {item.detectionRangeUsed}");
            EditorGUILayout.LabelField("Camada", $"{item.targetDomain}/{item.targetHeightLevel}");
            EditorGUILayout.LabelField("LOS direta", item.hasDirectLos ? "SIM" : "NAO");
            DrawLineProfile(item);
            if (item.usedForwardObserver)
            {
                string observerName = item.forwardObserverUnit != null ? item.forwardObserverUnit.name : "(desconhecido)";
                EditorGUILayout.LabelField("Observador avancado", observerName);
            }
            if (item.blockedCell != Vector3Int.zero)
                EditorGUILayout.LabelField("Bloqueio LOS", $"{item.blockedCell.x},{item.blockedCell.y}");
            if (!string.IsNullOrWhiteSpace(item.reason))
                EditorGUILayout.LabelField("Obs", item.reason);
            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// A viagem da linha, do EV de origem ao EV do alvo, com a altura em cada
    /// hex cruzado. Vem do mesmo tracado que decidiu a deteccao — nao e um
    /// calculo paralelo da janela, que foi como ferramenta e jogo ja
    /// discordaram uma vez.
    /// </summary>
    private static void DrawLineProfile(PodeDetectarOption item)
    {
        if (item == null || item.lineOfSightEvPath == null || item.lineOfSightEvPath.Count == 0)
            return;

        float origin = item.lineOriginEv;
        float target = item.lineTargetEv;
        string direction =
            Mathf.Abs(target - origin) < 0.001f ? "nivelada"
            : target > origin ? "ascendente"
            : "descendente";

        EditorGUILayout.LabelField(
            "Viagem da linha",
            $"{direction}  {origin:0.##} -> {target:0.##}");

        List<float> path = item.lineOfSightEvPath;
        int shown = Mathf.Min(path.Count, 12);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < shown; i++)
        {
            if (i > 0)
                sb.Append(" > ");
            sb.Append(path[i].ToString("0.##"));
        }
        if (path.Count > shown)
            sb.Append(" ...");

        EditorGUILayout.LabelField("Altura por hex", sb.ToString());
    }

    private void TryUseCurrentSelection()
    {
        if (Selection.activeGameObject == null)
            return;

        UnitManager unit = Selection.activeGameObject.GetComponent<UnitManager>();
        if (unit == null)
            unit = Selection.activeGameObject.GetComponentInParent<UnitManager>();
        if (unit != null)
            selectedUnit = unit;
    }

    private void RunSimulation()
    {
        detectedStealth.Clear();
        undetectedStealth.Clear();
        spottedCandidates.Clear();
        inRangeButLosBlocked.Clear();
        ClearForcedDetectedIndicators();
        ClearSelectedLine();

        if (selectedUnit == null)
        {
            statusMessage = "Selecione uma unidade valida.";
            return;
        }

        Tilemap map = ResolveBoardTilemapForSimulation();
        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstAsset<TerrainDatabase>();
        bool enableLos = true;
        bool enableSpotter = true;
        bool enableStealth = true;
        if (useGameplaySensorContext && matchController != null)
        {
            enableLos = matchController.EnableLosValidation;
            enableSpotter = matchController.EnableSpotter;
            enableStealth = matchController.EnableStealthValidation;
        }

        bool ok = PodeDetectarSensor.CollectDetection(
            selectedUnit,
            map,
            db,
            detectedStealth,
            undetectedStealth,
            spottedCandidates,
            inRangeButLosBlocked,
            out string reason,
            dpqAirHeightConfig,
            enableLos,
            enableSpotter,
            enableStealth);

        string candidateSummary = BuildCandidateDebugSummary(selectedUnit, map);
        statusMessage = ok
            ? $"Sensor TRUE. {reason} | {candidateSummary}"
            : $"Sensor FALSE. {reason} | {candidateSummary}";

        ApplyForcedDetectedIndicatorsForLayerVisibleStealthUnits();

        if (!logToConsole)
            return;

        Debug.Log($"[PodeDetectarSensorDebug] Unit={selectedUnit.name} | GameSetup(LoS={enableLos},Spotter={enableSpotter},Stealth={enableStealth}) | {reason} | {candidateSummary}");
        LogOptionList("FURTIVAS", detectedStealth);
        LogOptionList("FURTIVAS_NAO_DETECTADAS", undetectedStealth);
        LogOptionList("AVISTADAS", spottedCandidates);
        LogOptionList("SEM_LOS", inRangeButLosBlocked);
    }

    private static string BuildCandidateDebugSummary(UnitManager observer, Tilemap boardMap)
    {
        if (observer == null)
            return "Diag: observer=null";
        if (boardMap == null)
            return "Diag: boardMap=null";

        IReadOnlyList<UnitManager> units = GetUnitsForDebugQueries();
        int total = 0;
        int enemyTeam = 0;
        int enemyTeamOnMap = 0;
        int enemyEligible = 0;
        int enemyFilteredEmbarked = 0;
        int enemyFilteredMap = 0;
        int enemyFilteredScene = 0;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager target = units[i];
            if (target == null || target == observer || !target.gameObject.activeInHierarchy)
                continue;

            total++;
            if (target.TeamId == observer.TeamId)
                continue;

            enemyTeam++;
            if (target.IsEmbarked)
            {
                enemyFilteredEmbarked++;
                continue;
            }

            if (target.BoardTilemap != boardMap)
            {
                enemyFilteredMap++;
                continue;
            }

            if (target.gameObject.scene != boardMap.gameObject.scene)
            {
                enemyFilteredScene++;
                continue;
            }

            enemyTeamOnMap++;
            enemyEligible++;
        }

        return $"Diag: total={total} enemyTeam={enemyTeam} enemyEligible={enemyEligible} enemyOnMap={enemyTeamOnMap} dropEmbarked={enemyFilteredEmbarked} dropMap={enemyFilteredMap} dropScene={enemyFilteredScene}";
    }

    private static IReadOnlyList<UnitManager> GetUnitsForDebugQueries()
    {
        if (UnitManager.AllActive != null && UnitManager.AllActive.Count > 0)
            return UnitManager.AllActive;

        UnitManager[] fallback = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return fallback ?? System.Array.Empty<UnitManager>();
    }

    private void ApplyForcedDetectedIndicatorsForLayerVisibleStealthUnits()
    {
        int marked = 0;
        for (int i = 0; i < detectedStealth.Count; i++)
        {
            PodeDetectarOption option = detectedStealth[i];
            UnitManager target = option != null ? option.targetUnit : null;
            if (target == null)
                continue;

            UnitHudController hud = ResolveOwnUnitHud(target);
            if (hud == null)
                continue;

            hud.SetDetectedIndicatorVisible(true);
            if (forcedDetectedIndicatorUnits.Add(target))
                marked++;
        }

        for (int i = 0; i < spottedCandidates.Count; i++)
        {
            PodeDetectarOption option = spottedCandidates[i];
            UnitManager target = option != null ? option.targetUnit : null;
            if (target == null)
                continue;

            if (!target.TryGetUnitData(out UnitData unitData) || unitData == null)
                continue;

            bool hasAnyStealthConfigured = unitData.ResolveStealthSkillsForDetection().Count > 0;
            bool stealthActiveAtCurrentLayer = unitData.IsStealthUnit(target.GetDomain(), target.GetHeightLevel());
            if (!hasAnyStealthConfigured || stealthActiveAtCurrentLayer)
                continue;

            UnitHudController hud = ResolveOwnUnitHud(target);
            if (hud == null)
                continue;

            hud.SetDetectedIndicatorVisible(true);
            if (forcedDetectedIndicatorUnits.Add(target))
                marked++;
        }

        if (marked > 0)
            statusMessage += $" | Olhinho ativo em {marked} unidade(s) detectadas nesta simulacao.";
    }

    private void ClearForcedDetectedIndicators()
    {
        foreach (UnitManager unit in forcedDetectedIndicatorUnits)
        {
            if (unit == null)
                continue;

            UnitHudController hud = ResolveOwnUnitHud(unit);
            if (hud == null)
                continue;

            hud.SetDetectedIndicatorVisible(false);
        }

        forcedDetectedIndicatorUnits.Clear();
    }

    private static UnitHudController ResolveOwnUnitHud(UnitManager unit)
    {
        if (unit == null)
            return null;

        UnitHudController[] candidates = unit.GetComponentsInChildren<UnitHudController>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            UnitHudController candidate = candidates[i];
            if (candidate == null)
                continue;

            UnitManager owner = candidate.GetComponentInParent<UnitManager>();
            if (owner == unit)
                return candidate;
        }

        return null;
    }

    private void SelectLineForDrawing(PodeDetectarOption item, Color lineColor, string label)
    {
        if (item == null || item.targetUnit == null)
            return;

        Tilemap map = overrideTilemap != null ? overrideTilemap : (selectedUnit != null ? selectedUnit.BoardTilemap : item.targetUnit.BoardTilemap);
        Vector3 startWorld = item.observerUnit != null ? item.observerUnit.transform.position : Vector3.zero;
        Vector3 endWorld = item.targetUnit.transform.position;
        selectedLineWorldPoints.Clear();
        if (map != null)
        {
            Vector3Int startCell = item.observerCell;
            Vector3Int endCell = item.targetCell;
            startCell.z = 0;
            endCell.z = 0;
            startWorld = map.GetCellCenterWorld(startCell);
            endWorld = map.GetCellCenterWorld(endCell);

            if (IsSubSubmergedLayer(item) &&
                TryBuildAquaticCellPath(map, startCell, endCell, out List<Vector3Int> cellPath))
            {
                for (int i = 0; i < cellPath.Count; i++)
                    selectedLineWorldPoints.Add(map.GetCellCenterWorld(cellPath[i]));
            }
        }

        hasSelectedLine = true;
        selectedLineStartWorld = startWorld;
        selectedLineEndWorld = endWorld;
        selectedLineColor = lineColor;
        selectedLineLabel = label;
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Devolve a janela ao estado vazio: listas, indicadores forcados no Scene
    /// View e a linha desenhada. Nao mexe na unidade nem no contexto — limpar
    /// resultado nao e recomecar a configuracao.
    /// </summary>
    private void ClearResults()
    {
        detectedStealth.Clear();
        undetectedStealth.Clear();
        spottedCandidates.Clear();
        inRangeButLosBlocked.Clear();
        ClearForcedDetectedIndicators();
        ClearSelectedLine();
        statusMessage = "Limpo.";
        SceneView.RepaintAll();
        Repaint();
    }

    private void ClearSelectedLine()
    {
        hasSelectedLine = false;
        selectedLineWorldPoints.Clear();
        selectedLineLabel = string.Empty;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!hasSelectedLine)
            return;

        Handles.color = selectedLineColor;
        if (selectedLineWorldPoints.Count >= 2)
            Handles.DrawAAPolyLine(4f, selectedLineWorldPoints.ToArray());
        else
            Handles.DrawAAPolyLine(4f, selectedLineStartWorld, selectedLineEndWorld);

        Vector3 mid = selectedLineWorldPoints.Count >= 2
            ? selectedLineWorldPoints[selectedLineWorldPoints.Count / 2]
            : Vector3.Lerp(selectedLineStartWorld, selectedLineEndWorld, 0.5f);
        Handles.Label(mid + Vector3.up * 0.2f, selectedLineLabel);
    }

    private bool TryBuildAquaticCellPath(Tilemap map, Vector3Int startCell, Vector3Int endCell, out List<Vector3Int> path)
    {
        path = new List<Vector3Int>();
        if (map == null)
            return false;

        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstAsset<TerrainDatabase>();
        if (db == null)
            return false;

        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> parent = new Dictionary<Vector3Int, Vector3Int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        startCell.z = 0;
        endCell.z = 0;
        frontier.Enqueue(startCell);
        visited.Add(startCell);

        bool found = false;
        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            if (current == endCell)
            {
                found = true;
                break;
            }

            UnitMovementPathRules.GetImmediateHexNeighbors(map, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (visited.Contains(next))
                    continue;
                if (!IsAquaticCellForSubmergedDetection(map, db, next))
                    continue;

                visited.Add(next);
                parent[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!found)
            return false;

        Vector3Int step = endCell;
        path.Add(step);
        while (step != startCell)
        {
            if (!parent.TryGetValue(step, out Vector3Int prev))
                return false;
            step = prev;
            path.Add(step);
        }

        path.Reverse();
        return path.Count >= 2;
    }

    private static bool IsSubSubmergedLayer(PodeDetectarOption option)
    {
        if (option == null)
            return false;

        return option.targetDomain == Domain.Submarine && option.targetHeightLevel == HeightLevel.Submerged;
    }

    private static bool IsAquaticCellForSubmergedDetection(Tilemap map, TerrainDatabase db, Vector3Int cell)
    {
        if (map == null || db == null)
            return false;

        if (!TryResolveTerrainAtCell(map, db, cell, out TerrainTypeData terrain) || terrain == null)
            return false;

        return TerrainSupportsLayerMode(terrain, Domain.Submarine, HeightLevel.Submerged) ||
            TerrainSupportsLayerMode(terrain, Domain.Naval, HeightLevel.Surface);
    }

    private static bool TerrainSupportsLayerMode(TerrainTypeData terrain, Domain domain, HeightLevel height)
    {
        if (terrain == null)
            return false;

        if (terrain.domain == domain && terrain.heightLevel == height)
            return true;

        if (terrain.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < terrain.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrain.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == height)
                return true;
        }

        return false;
    }

    private static bool TryResolveTerrainAtCell(Tilemap map, TerrainDatabase db, Vector3Int cell, out TerrainTypeData terrain)
    {
        terrain = null;
        if (map == null || db == null)
            return false;

        cell.z = 0;
        TileBase tile = map.GetTile(cell);
        if (tile != null && db.TryGetByPaletteTile(tile, out TerrainTypeData byMain) && byMain != null)
        {
            terrain = byMain;
            return true;
        }

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap candidate = maps[i];
            if (candidate == null || candidate == map)
                continue;
            if (candidate.layoutGrid != map.layoutGrid)
                continue;
            if (candidate.gameObject.scene != map.gameObject.scene)
                continue;

            TileBase other = candidate.GetTile(cell);
            if (other == null)
                continue;
            if (db.TryGetByPaletteTile(other, out TerrainTypeData byGrid) && byGrid != null)
            {
                terrain = byGrid;
                return true;
            }
        }

        return false;
    }

    private static void LogOptionList(string tag, List<PodeDetectarOption> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            PodeDetectarOption item = items[i];
            if (item == null || item.targetUnit == null)
                continue;

            string observerName = item.observerUnit != null ? item.observerUnit.name : "(null)";
            string targetName = item.targetUnit.name;
            string forwardObserverName = item.forwardObserverUnit != null ? item.forwardObserverUnit.name : "-";
            Debug.Log(
                $"[PodeDetectarSensorDebug][{tag}] {i + 1}. {observerName} -> {targetName} | " +
                $"dist={item.distance}/{item.detectionRangeUsed} | layer={item.targetDomain}/{item.targetHeightLevel} | " +
                $"losDireta={(item.hasDirectLos ? "sim" : "nao")} | forwardObserver={forwardObserverName} | motivo={item.reason}");
        }
    }

    private void AutoDetectContext()
    {
        if (turnStateManager == null)
            turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();
        if (matchController == null)
            matchController = Object.FindAnyObjectByType<MatchController>();
        if (overrideTilemap == null)
            overrideTilemap = FindPreferredTilemap();
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAsset<DPQAirHeightConfig>();
    }

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap board = FindTilemapByName("TileMap");
        if (board != null)
            return board;

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps == null || maps.Length == 0)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;
            if (string.Equals(map.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return maps[0];
    }

    private Tilemap ResolveBoardTilemapForSimulation()
    {
        if (overrideTilemap != null)
            return overrideTilemap;

        if (useGameplaySensorContext)
        {
            Tilemap gameplayMap = ResolveGameplayTerrainTilemap();
            if (gameplayMap != null)
                return gameplayMap;
        }

        if (selectedUnit != null && selectedUnit.BoardTilemap != null)
            return selectedUnit.BoardTilemap;

        return FindPreferredTilemap();
    }

    private Tilemap ResolveGameplayTerrainTilemap()
    {
        if (turnStateManager == null)
            turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();
        if (turnStateManager == null)
            return null;

        SerializedObject so = new SerializedObject(turnStateManager);
        SerializedProperty terrainProp = so.FindProperty("terrainTilemap");
        return terrainProp != null ? terrainProp.objectReferenceValue as Tilemap : null;
    }

    private static Tilemap FindTilemapByName(string expectedName)
    {
        if (string.IsNullOrWhiteSpace(expectedName))
            return null;

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            if (string.Equals(map.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return null;
    }

    private static T FindFirstAsset<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }
}
