using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AlguemMeVeDebugWindow : EditorWindow
{
    private sealed class ObserverHit
    {
        public UnitManager observer;
        public PodeDetectarOption option;
        public string bucket;
    }

    [SerializeField] private UnitManager targetUnit;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private MatchController matchController;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private bool useGameplaySensorContext = true;
    [SerializeField] private bool logToConsole = true;

    private readonly List<ObserverHit> detectedByEnemies = new List<ObserverHit>();
    private readonly List<ObserverHit> undetectedStealthByEnemies = new List<ObserverHit>();
    private readonly List<ObserverHit> blockedByLos = new List<ObserverHit>();
    private readonly HashSet<UnitManager> forcedDetectedIndicatorUnits = new HashSet<UnitManager>();

    // Contexto do relatorio da reta — o mesmo tabuleiro e a mesma base que a
    // varredura usou.
    private Tilemap reportTilemap;
    private TerrainDatabase reportTerrainDatabase;
    private readonly List<ObservationLineReportRow> reportRows = new List<ObservationLineReportRow>();
    private bool hasSelectedLine;
    private Vector3 selectedLineStartWorld;
    private Vector3 selectedLineEndWorld;
    private readonly List<Vector3> selectedLineWorldPoints = new List<Vector3>();
    private Color selectedLineColor = Color.green;
    private string selectedLineLabel = string.Empty;
    private Vector2 windowScroll;
    private string statusMessage = "Ready.";

    [MenuItem("Tools/FoW/Alguem me vê")]
    public static void OpenWindow()
    {
        GetWindow<AlguemMeVeDebugWindow>("Alguem me vê");
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
        EditorGUILayout.LabelField("Sensor Alguem me vê", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Relatorio orientado ao alvo: quais inimigos detectam esta unidade, quais falham por stealth e quais falham por LOS.",
            MessageType.Info);

        targetUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade alvo", targetUnit, typeof(UnitManager), true);
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
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        EditorGUILayout.Space(6f);
        DrawObserverList("Inimigos que me detectam", detectedByEnemies, MessageType.Info);
        EditorGUILayout.Space(6f);
        DrawObserverList("Inimigos que me veem mas falham no stealth", undetectedStealthByEnemies, MessageType.Warning);
        EditorGUILayout.Space(6f);
        DrawObserverList("Inimigos no alcance sem LOS", blockedByLos, MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private void DrawObserverList(string title, List<ObserverHit> items, MessageType emptyType)
    {
        EditorGUILayout.LabelField($"{title} ({items.Count})", EditorStyles.boldLabel);
        if (items.Count <= 0)
        {
            EditorGUILayout.HelpBox("Nenhum item.", emptyType);
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ObserverHit hit = items[i];
            if (hit == null || hit.observer == null || hit.option == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{i + 1}. {hit.observer.name}", EditorStyles.boldLabel);
            if (GUILayout.Button("Desenhar Linha", GUILayout.Width(110f)))
                SelectLineForDrawing(hit);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Bucket", hit.bucket ?? string.Empty);
            EditorGUILayout.LabelField("Hex alvo", $"{hit.option.targetCell.x},{hit.option.targetCell.y}");
            EditorGUILayout.LabelField("Distancia", $"{hit.option.distance} / alcance {hit.option.detectionRangeUsed}");
            EditorGUILayout.LabelField("Camada", $"{hit.option.targetDomain}/{hit.option.targetHeightLevel}");
            // Mesmo relatorio das janelas do Pode Enxergar e do Pode Detectar.
            // Esta pergunta e a inversa — quem me ve — mas a reta e a mesma.
            ObservationLineReport.Build(hit.option.lineProfile, reportTilemap, reportTerrainDatabase, reportRows);
            for (int r = 0; r < reportRows.Count; r++)
                EditorGUILayout.LabelField(reportRows[r].label, reportRows[r].value);
            if (!string.IsNullOrWhiteSpace(hit.option.reason))
                EditorGUILayout.LabelField("Obs", hit.option.reason);
            EditorGUILayout.EndVertical();
        }
    }

    private void TryUseCurrentSelection()
    {
        if (Selection.activeGameObject == null)
            return;

        UnitManager unit = Selection.activeGameObject.GetComponent<UnitManager>();
        if (unit == null)
            unit = Selection.activeGameObject.GetComponentInParent<UnitManager>();
        if (unit != null)
            targetUnit = unit;
    }

    private void RunSimulation()
    {
        detectedByEnemies.Clear();
        undetectedStealthByEnemies.Clear();
        blockedByLos.Clear();
        ClearForcedDetectedIndicators();
        ClearSelectedLine();

        if (targetUnit == null)
        {
            statusMessage = "Selecione uma unidade alvo valida.";
            return;
        }

        Tilemap map = ResolveBoardTilemapForSimulation();
        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstAsset<TerrainDatabase>();
        reportTilemap = map;
        reportTerrainDatabase = db;
        bool enableLos = true;
        bool enableSpotter = true;
        bool enableStealth = true;
        if (useGameplaySensorContext && matchController != null)
        {
            enableLos = matchController.EnableLosValidation;
            enableSpotter = matchController.EnableSpotter;
            enableStealth = matchController.EnableStealthValidation;
        }

        IReadOnlyList<UnitManager> allUnits = GetUnitsForDebugQueries();
        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitManager observer = allUnits[i];
            if (!IsEnemyObserverCandidate(observer))
                continue;

            List<PodeDetectarOption> detectedStealth = new List<PodeDetectarOption>();
            List<PodeDetectarOption> undetectedStealth = new List<PodeDetectarOption>();
            List<PodeDetectarOption> spotted = new List<PodeDetectarOption>();
            List<PodeDetectarOption> blocked = new List<PodeDetectarOption>();
            PodeDetectarSensor.CollectDetection(
                observer,
                map,
                db,
                detectedStealth,
                undetectedStealth,
                spotted,
                blocked,
                out _,
                dpqAirHeightConfig,
                enableLos,
                enableSpotter,
                enableStealth);

            AppendHitsForTarget(observer, detectedStealth, "Furtiva detectada", detectedByEnemies);
            AppendHitsForTarget(observer, spotted, "Avistada", detectedByEnemies);
            AppendHitsForTarget(observer, undetectedStealth, "Furtiva nao detectada", undetectedStealthByEnemies);
            AppendHitsForTarget(observer, blocked, "Sem LOS", blockedByLos);
        }

        statusMessage =
            $"Detectam={detectedByEnemies.Count} | FalhaStealth={undetectedStealthByEnemies.Count} | SemLOS={blockedByLos.Count} | " +
            $"LoS={enableLos} Spotter={enableSpotter} Stealth={enableStealth}";

        ApplyForcedDetectedIndicatorForTarget();

        if (!logToConsole)
            return;

        Debug.Log($"[AlguemMeVeDebug] target={targetUnit.name} | {statusMessage}");
    }

    private void ApplyForcedDetectedIndicatorForTarget()
    {
        if (targetUnit == null)
            return;

        bool shouldShow = detectedByEnemies.Count > 0;
        UnitHudController hud = ResolveOwnUnitHud(targetUnit);
        if (hud == null)
            return;

        if (shouldShow)
        {
            hud.SetDetectedIndicatorVisible(true);
            forcedDetectedIndicatorUnits.Add(targetUnit);
            statusMessage += " | Olhinho ativo no alvo.";
        }
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

    private void SelectLineForDrawing(ObserverHit hit)
    {
        if (hit == null || hit.option == null)
            return;

        Tilemap map = ResolveBoardTilemapForSimulation();
        Vector3 startWorld = hit.option.observerUnit != null ? hit.option.observerUnit.transform.position : Vector3.zero;
        Vector3 endWorld = hit.option.targetUnit != null ? hit.option.targetUnit.transform.position : Vector3.zero;

        selectedLineWorldPoints.Clear();
        if (map != null)
        {
            Vector3Int startCell = hit.option.observerCell;
            Vector3Int endCell = hit.option.targetCell;
            startCell.z = 0;
            endCell.z = 0;
            startWorld = map.GetCellCenterWorld(startCell);
            endWorld = map.GetCellCenterWorld(endCell);

            if (IsSubSubmergedLayer(hit.option) &&
                TryBuildAquaticCellPath(map, startCell, endCell, out List<Vector3Int> cellPath))
            {
                for (int i = 0; i < cellPath.Count; i++)
                    selectedLineWorldPoints.Add(map.GetCellCenterWorld(cellPath[i]));
            }
        }

        hasSelectedLine = true;
        selectedLineStartWorld = startWorld;
        selectedLineEndWorld = endWorld;
        selectedLineColor = ResolveLineColorForBucket(hit.bucket);
        selectedLineLabel = $"{hit.observer.name} -> {hit.option.targetUnit.name}";
        SceneView.RepaintAll();
    }

    private static Color ResolveLineColorForBucket(string bucket)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            return Color.green;

        string normalized = bucket.Trim().ToLowerInvariant();
        if (normalized.Contains("sem los") || normalized.Contains("nao detectada") || normalized.Contains("não detectada"))
            return Color.red;

        return Color.green;
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

    private bool IsEnemyObserverCandidate(UnitManager observer)
    {
        if (observer == null || targetUnit == null)
            return false;
        if (!observer.gameObject.activeInHierarchy || observer.IsEmbarked)
            return false;
        if (observer == targetUnit)
            return false;
        return observer.TeamId != targetUnit.TeamId;
    }

    private void AppendHitsForTarget(UnitManager observer, List<PodeDetectarOption> source, string bucket, List<ObserverHit> output)
    {
        if (source == null || output == null || targetUnit == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            PodeDetectarOption item = source[i];
            if (item == null || item.targetUnit != targetUnit)
                continue;

            output.Add(new ObserverHit
            {
                observer = observer,
                option = item,
                bucket = bucket
            });
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

        if (targetUnit != null && targetUnit.BoardTilemap != null)
            return targetUnit.BoardTilemap;

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

    private static IReadOnlyList<UnitManager> GetUnitsForDebugQueries()
    {
        if (UnitManager.AllActive != null && UnitManager.AllActive.Count > 0)
            return UnitManager.AllActive;

        UnitManager[] fallback = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return fallback ?? System.Array.Empty<UnitManager>();
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
