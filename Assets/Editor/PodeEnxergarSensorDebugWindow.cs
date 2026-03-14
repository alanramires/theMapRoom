using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeEnxergarSensorDebugWindow : EditorWindow
{
    private enum DrawBatchMode
    {
        None,
        Valid,
        Invalid
    }

    private sealed class VisibleHexEntry
    {
        public Vector3Int cell;
        public int distance;
        public Domain domain;
        public HeightLevel heightLevel;
        public string layerSource;
    }

    private sealed class SceneLineEntry
    {
        public Vector3 startWorld;
        public Vector3 endWorld;
        public Color color;
        public string label;
    }

    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private MatchController matchController;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private bool useGameplaySensorContext = true;
    [SerializeField] private bool logToConsole = true;

    private readonly List<VisibleHexEntry> visibleHexes = new List<VisibleHexEntry>();
    private readonly List<VisibleHexEntry> invalidHexes = new List<VisibleHexEntry>();
    private readonly HashSet<Vector3Int> visibleCellsBuffer = new HashSet<Vector3Int>();
    private readonly List<SceneLineEntry> sceneLines = new List<SceneLineEntry>();
    private DrawBatchMode currentDrawBatchMode = DrawBatchMode.None;
    private Vector2 windowScroll;
    private string statusMessage = "Ready.";

    [MenuItem("Tools/Sensors/Pode Enxergar")]
    public static void OpenWindow()
    {
        GetWindow<PodeEnxergarSensorDebugWindow>("Pode Enxergar");
    }

    [MenuItem("Tools/Sensor/Pode Enxergar")]
    public static void OpenWindowAlias()
    {
        OpenWindow();
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
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);

        EditorGUILayout.LabelField("Sensor Pode Enxergar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Irmao do Pode Detectar focado em visibilidade de hex. " +
            "Em vez de procurar unidades reais, resolve uma unidade virtual por celula visivel.",
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
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Desenhar todas as validas"))
            DrawAllValidLines();
        if (GUILayout.Button("Desenhar todas as invalidas"))
            DrawAllInvalidLines();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"Hexes Visiveis ({visibleHexes.Count})", EditorStyles.boldLabel);
        if (visibleHexes.Count <= 0)
        {
            EditorGUILayout.HelpBox("Nenhum hex visivel.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < visibleHexes.Count; i++)
                DrawVisibleHexItem(i, visibleHexes[i]);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"Hexes Invalidos ({invalidHexes.Count})", EditorStyles.boldLabel);
        if (invalidHexes.Count <= 0)
        {
            EditorGUILayout.HelpBox("Nenhum hex invalido no alcance.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < invalidHexes.Count; i++)
                DrawInvalidHexItem(i, invalidHexes[i]);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawVisibleHexItem(int index, VisibleHexEntry item)
    {
        if (item == null)
            return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{index + 1}. Hex {item.cell.x},{item.cell.y}", EditorStyles.boldLabel);
        if (GUILayout.Button("Desenhar Linha", GUILayout.Width(110f)))
            SelectLineForDrawing(item, Color.cyan, $"VAL {item.cell.x},{item.cell.y}");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Distancia", item.distance.ToString());
        EditorGUILayout.LabelField("Virtual Domain/Height", $"{item.domain}/{item.heightLevel}");
        EditorGUILayout.LabelField("Fonte da Camada", item.layerSource);
        EditorGUILayout.EndVertical();
    }

    private void DrawInvalidHexItem(int index, VisibleHexEntry item)
    {
        if (item == null)
            return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{index + 1}. Hex {item.cell.x},{item.cell.y}", EditorStyles.boldLabel);
        if (GUILayout.Button("Desenhar Linha", GUILayout.Width(110f)))
            SelectLineForDrawing(item, Color.red, $"INV {item.cell.x},{item.cell.y}");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Distancia", item.distance.ToString());
        EditorGUILayout.LabelField("Virtual Domain/Height", $"{item.domain}/{item.heightLevel}");
        EditorGUILayout.LabelField("Fonte da Camada", item.layerSource);
        EditorGUILayout.EndVertical();
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
        visibleHexes.Clear();
        invalidHexes.Clear();
        visibleCellsBuffer.Clear();
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
        if (useGameplaySensorContext && matchController != null)
        {
            enableLos = matchController.EnableLosValidation;
            enableSpotter = matchController.EnableSpotter;
        }

        PodeDetectarSensor.CollectVisibleCells(
            selectedUnit,
            map,
            db,
            visibleCellsBuffer,
            dpqAirHeightConfig,
            enableLos,
            enableSpotter,
            useOccupantLayerForTarget: false,
            preserveObserverLayerRangeForHexVisibility: true);

        Dictionary<Vector3Int, int> distances = BuildDistanceMap(
            map,
            selectedUnit.CurrentCellPosition,
            ResolveMaxVisionRange(selectedUnit));

        foreach (Vector3Int cell in visibleCellsBuffer)
        {
            ResolveVirtualLayerForCell(map, db, cell, out Domain domain, out HeightLevel heightLevel, out string source);
            Vector3Int normalizedCell = cell;
            normalizedCell.z = 0;
            distances.TryGetValue(normalizedCell, out int distance);
            visibleHexes.Add(new VisibleHexEntry
            {
                cell = normalizedCell,
                distance = distance,
                domain = domain,
                heightLevel = heightLevel,
                layerSource = source
            });
        }

        foreach (KeyValuePair<Vector3Int, int> pair in distances)
        {
            Vector3Int cell = pair.Key;
            cell.z = 0;
            if (pair.Value <= 0)
                continue;
            if (visibleCellsBuffer.Contains(cell))
                continue;

            ResolveVirtualLayerForCell(map, db, cell, out Domain domain, out HeightLevel heightLevel, out string source);
            invalidHexes.Add(new VisibleHexEntry
            {
                cell = cell,
                distance = pair.Value,
                domain = domain,
                heightLevel = heightLevel,
                layerSource = source
            });
        }

        visibleHexes.Sort((a, b) =>
        {
            int distCompare = a.distance.CompareTo(b.distance);
            if (distCompare != 0)
                return distCompare;
            int yCompare = b.cell.y.CompareTo(a.cell.y);
            if (yCompare != 0)
                return yCompare;
            return a.cell.x.CompareTo(b.cell.x);
        });
        invalidHexes.Sort((a, b) =>
        {
            int distCompare = a.distance.CompareTo(b.distance);
            if (distCompare != 0)
                return distCompare;
            int yCompare = b.cell.y.CompareTo(a.cell.y);
            if (yCompare != 0)
                return yCompare;
            return a.cell.x.CompareTo(b.cell.x);
        });

        statusMessage = $"Hexes visiveis: {visibleHexes.Count} | invalidos: {invalidHexes.Count} | LoS={enableLos} | Spotter={enableSpotter}";

        if (!logToConsole)
            return;

        Debug.Log($"[PodeEnxergarSensorDebug] Unit={selectedUnit.name} | LoS={enableLos} | Spotter={enableSpotter} | HexesVisiveis={visibleHexes.Count} | HexesInvalidos={invalidHexes.Count}");
        for (int i = 0; i < visibleHexes.Count; i++)
        {
            VisibleHexEntry item = visibleHexes[i];
            Debug.Log(
                $"[PodeEnxergarSensorDebug][HEX] {i + 1}. cell={item.cell.x},{item.cell.y} | dist={item.distance} | " +
                $"virtualLayer={item.domain}/{item.heightLevel} | source={item.layerSource}");
        }
        for (int i = 0; i < invalidHexes.Count; i++)
        {
            VisibleHexEntry item = invalidHexes[i];
            Debug.Log(
                $"[PodeEnxergarSensorDebug][INVALIDO] {i + 1}. cell={item.cell.x},{item.cell.y} | dist={item.distance} | " +
                $"virtualLayer={item.domain}/{item.heightLevel} | source={item.layerSource}");
        }
    }

    private void SelectLineForDrawing(VisibleHexEntry item, Color color, string labelPrefix)
    {
        if (item == null || selectedUnit == null)
            return;

        Tilemap map = ResolveBoardTilemapForSimulation();
        if (map == null)
            return;

        Vector3Int originCell = selectedUnit.CurrentCellPosition;
        originCell.z = 0;
        Vector3Int targetCell = item.cell;
        targetCell.z = 0;

        sceneLines.Clear();
        sceneLines.Add(new SceneLineEntry
        {
            startWorld = map.GetCellCenterWorld(originCell),
            endWorld = map.GetCellCenterWorld(targetCell),
            color = color,
            label = $"{labelPrefix} ({targetCell.x},{targetCell.y})"
        });
        currentDrawBatchMode = DrawBatchMode.None;
        SceneView.RepaintAll();
    }

    private void DrawAllValidLines()
    {
        if (currentDrawBatchMode == DrawBatchMode.Valid)
        {
            ClearSelectedLine();
            return;
        }

        DrawAllLinesFromEntries(visibleHexes, Color.cyan, "VAL");
        currentDrawBatchMode = DrawBatchMode.Valid;
    }

    private void DrawAllInvalidLines()
    {
        if (currentDrawBatchMode == DrawBatchMode.Invalid)
        {
            ClearSelectedLine();
            return;
        }

        DrawAllLinesFromEntries(invalidHexes, Color.red, "INV");
        currentDrawBatchMode = DrawBatchMode.Invalid;
    }

    private void DrawAllLinesFromEntries(List<VisibleHexEntry> entries, Color color, string tag)
    {
        if (selectedUnit == null)
            return;

        Tilemap map = ResolveBoardTilemapForSimulation();
        if (map == null)
            return;

        sceneLines.Clear();
        Vector3Int originCell = selectedUnit.CurrentCellPosition;
        originCell.z = 0;
        Vector3 originWorld = map.GetCellCenterWorld(originCell);
        for (int i = 0; i < entries.Count; i++)
        {
            VisibleHexEntry item = entries[i];
            if (item == null)
                continue;

            Vector3Int targetCell = item.cell;
            targetCell.z = 0;
            sceneLines.Add(new SceneLineEntry
            {
                startWorld = originWorld,
                endWorld = map.GetCellCenterWorld(targetCell),
                color = color,
                label = $"{tag} ({targetCell.x},{targetCell.y})"
            });
        }

        SceneView.RepaintAll();
    }

    private void ClearSelectedLine()
    {
        sceneLines.Clear();
        currentDrawBatchMode = DrawBatchMode.None;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (sceneLines.Count <= 0)
            return;

        for (int i = 0; i < sceneLines.Count; i++)
        {
            SceneLineEntry entry = sceneLines[i];
            if (entry == null)
                continue;

            Handles.color = entry.color;
            Handles.DrawAAPolyLine(3f, entry.startWorld, entry.endWorld);
            Vector3 mid = Vector3.Lerp(entry.startWorld, entry.endWorld, 0.5f);
            Handles.Label(mid + Vector3.up * 0.16f, entry.label);
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

    private static int ResolveMaxVisionRange(UnitManager observer)
    {
        if (observer == null)
            return 1;

        if (observer.TryGetUnitData(out UnitData observerData) && observerData != null)
        {
            int maxRange = Mathf.Max(1, observerData.visao);
            if (observerData.visionSpecializations != null)
            {
                for (int i = 0; i < observerData.visionSpecializations.Count; i++)
                {
                    UnitVisionException entry = observerData.visionSpecializations[i];
                    if (entry == null)
                        continue;
                    maxRange = Mathf.Max(maxRange, Mathf.Max(0, entry.vision));
                }
            }

            return Mathf.Max(1, maxRange);
        }

        return Mathf.Max(1, observer.Visao);
    }

    private static Dictionary<Vector3Int, int> BuildDistanceMap(Tilemap tilemap, Vector3Int origin, int maxRange)
    {
        Dictionary<Vector3Int, int> distances = new Dictionary<Vector3Int, int>();
        if (tilemap == null || maxRange < 0)
            return distances;

        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        origin.z = 0;
        distances[origin] = 0;
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int currentDistance = distances[current];
            if (currentDistance >= maxRange)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (distances.ContainsKey(next))
                    continue;

                int nextDistance = currentDistance + 1;
                if (nextDistance > maxRange)
                    continue;

                distances[next] = nextDistance;
                frontier.Enqueue(next);
            }
        }

        return distances;
    }

    private static void ResolveVirtualLayerForCell(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out Domain domain,
        out HeightLevel heightLevel,
        out string source)
    {
        domain = Domain.Land;
        heightLevel = HeightLevel.Surface;
        source = "fallback";

        cell.z = 0;

        if (tilemap != null)
        {
            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell);
            if (construction != null)
            {
                ConstructionDatabase db = construction.ConstructionDatabase;
                string id = construction.ConstructionId;
                if (db != null && !string.IsNullOrWhiteSpace(id) && db.TryGetById(id, out ConstructionData constructionData) && constructionData != null)
                {
                    domain = constructionData.domain;
                    heightLevel = constructionData.heightLevel;
                    source = "construction";
                    return;
                }
            }

            StructureData structure = StructureOccupancyRules.GetStructureAtCell(tilemap, cell);
            if (structure != null)
            {
                domain = structure.domain;
                heightLevel = structure.heightLevel;
                source = "structure";
                return;
            }
        }

        if (TryResolveTerrainAtCell(tilemap, terrainDatabase, cell, out TerrainTypeData terrain) && terrain != null)
        {
            domain = terrain.domain;
            heightLevel = terrain.heightLevel;
            source = "terrain";
        }
    }

    private static bool TryResolveTerrainAtCell(Tilemap terrainTilemap, TerrainDatabase terrainDatabase, Vector3Int cell, out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDatabase == null)
            return false;

        cell.z = 0;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
        {
            terrain = byMainTile;
            return true;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap scan = maps[i];
            if (scan == null)
                continue;

            TileBase other = scan.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }
}
