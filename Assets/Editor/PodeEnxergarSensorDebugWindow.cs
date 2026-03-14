using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeEnxergarSensorDebugWindow : EditorWindow
{
    private sealed class VisibleHexEntry
    {
        public string scenarioLabel;
        public Vector3Int cell;
        public int distance;
        public Domain domain;
        public HeightLevel heightLevel;
        public string layerSource;
        public bool rangeOnlyMode;
        public bool hasDirectLos;
        public float finalReachedEv;
        public float losHeightAtBlockedCell;
        public int blockedCellEv;
        public Vector3Int blockedCell;
        public string blockedLayerLabel;
        public float losHeightAtPassedCell;
        public int passedCellEv;
        public Vector3Int passedCell;
        public string passedLayerLabel;
        public string lineRiseTrace;
    }

    private sealed class VisionScenarioResult
    {
        public string label;
        public Domain domain;
        public HeightLevel heightLevel;
        public int range;
        public bool forcedLayer;
        public bool baseVisionOnly;
        public bool detailsExpanded;
        public bool validListExpanded;
        public bool invalidListExpanded;
        public readonly List<VisibleHexEntry> visibleHexes = new List<VisibleHexEntry>();
        public readonly List<VisibleHexEntry> invalidHexes = new List<VisibleHexEntry>();
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
    [SerializeField] private bool forceVirtualTargetLayer = false;
    [SerializeField] private Domain forcedVirtualTargetDomain = Domain.Land;
    [SerializeField] private HeightLevel forcedVirtualTargetHeight = HeightLevel.Surface;

    private readonly List<VisionScenarioResult> scenarioResults = new List<VisionScenarioResult>();
    private readonly List<VisibleHexEntry> visibleHexes = new List<VisibleHexEntry>();
    private readonly List<VisibleHexEntry> invalidHexes = new List<VisibleHexEntry>();
    private readonly List<SceneLineEntry> sceneLines = new List<SceneLineEntry>();
    private string currentDrawBatchKey = string.Empty;
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
        forceVirtualTargetLayer = EditorGUILayout.ToggleLeft("Forcar camada virtual alvo", forceVirtualTargetLayer);
        if (forceVirtualTargetLayer)
        {
            forcedVirtualTargetDomain = (Domain)EditorGUILayout.EnumPopup("Domain virtual", forcedVirtualTargetDomain);
            forcedVirtualTargetHeight = (HeightLevel)EditorGUILayout.EnumPopup("Height virtual", forcedVirtualTargetHeight);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular"))
            RunSimulation();
        if (GUILayout.Button("Limpar desenho"))
            ClearSelectedLine();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        if (scenarioResults.Count > 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Cenarios de Visao", EditorStyles.boldLabel);
            for (int i = 0; i < scenarioResults.Count; i++)
                DrawScenarioResultCard(i, scenarioResults[i]);
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
        if (!string.IsNullOrWhiteSpace(item.scenarioLabel))
            EditorGUILayout.LabelField("Cenario", item.scenarioLabel);
        EditorGUILayout.LabelField("Virtual Domain/Height", $"{item.domain}/{item.heightLevel}");
        EditorGUILayout.LabelField("Fonte da Camada", item.layerSource);
        if (item.rangeOnlyMode)
        {
            EditorGUILayout.LabelField("LoS", "Range only (AirHigh blockLoS=false)");
        }
        else
        {
            EditorGUILayout.LabelField("LoS direta", item.hasDirectLos ? "sim" : "nao");
            if (!string.IsNullOrWhiteSpace(item.lineRiseTrace))
                EditorGUILayout.LabelField("Subida da linha", item.lineRiseTrace);
            string passedEvText = item.passedCell != Vector3Int.zero
                ? item.losHeightAtPassedCell.ToString("0.00")
                : "-";
            EditorGUILayout.LabelField("EV passou", passedEvText);
            string passedByText = !string.IsNullOrWhiteSpace(item.passedLayerLabel)
                ? item.passedLayerLabel
                : "nenhum bloqueador relevante";
            EditorGUILayout.LabelField("Passou por", passedByText);
            EditorGUILayout.LabelField("EV final (chegou)", item.finalReachedEv.ToString("0.00"));
        }
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
        if (!string.IsNullOrWhiteSpace(item.scenarioLabel))
            EditorGUILayout.LabelField("Cenario", item.scenarioLabel);
        EditorGUILayout.LabelField("Virtual Domain/Height", $"{item.domain}/{item.heightLevel}");
        EditorGUILayout.LabelField("Fonte da Camada", item.layerSource);
        if (item.rangeOnlyMode)
        {
            EditorGUILayout.LabelField("LoS", "Range only (AirHigh blockLoS=false)");
        }
        else
        {
            EditorGUILayout.LabelField("LoS direta", item.hasDirectLos ? "sim" : "nao");
            if (!string.IsNullOrWhiteSpace(item.lineRiseTrace))
                EditorGUILayout.LabelField("Subida da linha", item.lineRiseTrace);
            EditorGUILayout.LabelField("EV na parada", item.losHeightAtBlockedCell.ToString("0.00"));
            EditorGUILayout.LabelField("Tentou ver EV", item.blockedCellEv.ToString());
            if (!string.IsNullOrWhiteSpace(item.blockedLayerLabel))
                EditorGUILayout.LabelField("EV Bloqueador", item.blockedLayerLabel);
        }
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
        scenarioResults.Clear();
        visibleHexes.Clear();
        invalidHexes.Clear();
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

        UnitData observerData = null;
        selectedUnit.TryGetUnitData(out observerData);
        List<UnitVisionException> specializations = new List<UnitVisionException>();
        if (observerData != null && observerData.visionSpecializations != null)
        {
            HashSet<int> dedupe = new HashSet<int>();
            for (int i = 0; i < observerData.visionSpecializations.Count; i++)
            {
                UnitVisionException entry = observerData.visionSpecializations[i];
                if (entry == null)
                    continue;

                int key = ((int)entry.domain * 100) + (int)entry.heightLevel;
                if (!dedupe.Add(key))
                    continue;

                specializations.Add(entry);
            }
        }

        if (forceVirtualTargetLayer)
        {
            int forcedRange = observerData != null
                ? Mathf.Max(0, observerData.ResolveVisionFor(forcedVirtualTargetDomain, forcedVirtualTargetHeight))
                : Mathf.Max(1, selectedUnit.Visao);
            VisionScenarioResult manualScenario = BuildScenarioResult(
                map,
                db,
                enableLos,
                enableSpotter,
                $"Manual {forcedVirtualTargetDomain}/{forcedVirtualTargetHeight}",
                forcedVirtualTargetDomain,
                forcedVirtualTargetHeight,
                forcedRange,
                forceLayer: true,
                preserveObserverLayerRange: false);
            scenarioResults.Add(manualScenario);
        }
        else if (specializations.Count > 0)
        {
            int baseRange = observerData != null
                ? Mathf.Max(1, observerData.visao)
                : Mathf.Max(1, selectedUnit.Visao);
            VisionScenarioResult baseScenario = BuildScenarioResult(
                map,
                db,
                enableLos,
                enableSpotter,
                "Visao Geral (base, sem especializacao)",
                Domain.Land,
                HeightLevel.Surface,
                baseRange,
                forceLayer: false,
                preserveObserverLayerRange: false,
                forcedDetectionRangeOverride: baseRange,
                skipSpecializedTargetLayers: true);
            scenarioResults.Add(baseScenario);

            for (int i = 0; i < specializations.Count; i++)
            {
                UnitVisionException entry = specializations[i];
                if (entry == null)
                    continue;

                int specializedRange = observerData != null
                    ? Mathf.Max(0, observerData.ResolveVisionFor(entry.domain, entry.heightLevel))
                    : Mathf.Max(1, selectedUnit.Visao);
                VisionScenarioResult specializedScenario = BuildScenarioResult(
                    map,
                    db,
                    enableLos,
                    enableSpotter,
                    $"Especializacao {entry.domain}/{entry.heightLevel}",
                    entry.domain,
                    entry.heightLevel,
                    specializedRange,
                    forceLayer: true,
                    preserveObserverLayerRange: false,
                    forcedDetectionRangeOverride: specializedRange,
                    skipSpecializedTargetLayers: false);
                scenarioResults.Add(specializedScenario);
            }
        }
        else
        {
            int baseRange = observerData != null
                ? Mathf.Max(1, observerData.visao)
                : Mathf.Max(1, selectedUnit.Visao);
            VisionScenarioResult defaultScenario = BuildScenarioResult(
                map,
                db,
                enableLos,
                enableSpotter,
                "Basico (camada virtual do mapa)",
                Domain.Land,
                HeightLevel.Surface,
                baseRange,
                forceLayer: false,
                preserveObserverLayerRange: false,
                forcedDetectionRangeOverride: baseRange,
                skipSpecializedTargetLayers: false);
            scenarioResults.Add(defaultScenario);
        }

        if (scenarioResults.Count > 0)
        {
            for (int i = 0; i < scenarioResults.Count; i++)
            {
                VisionScenarioResult scenario = scenarioResults[i];
                if (scenario == null)
                    continue;

                visibleHexes.AddRange(scenario.visibleHexes);
                invalidHexes.AddRange(scenario.invalidHexes);
            }
        }
        SortVisibleHexEntries(visibleHexes);
        SortVisibleHexEntries(invalidHexes);

        statusMessage =
            $"Cenarios: {scenarioResults.Count} | Hexes visiveis (relatorio geral): {visibleHexes.Count} | " +
            $"invalidos: {invalidHexes.Count} | LoS={enableLos} | Spotter={enableSpotter}";

        if (!logToConsole)
            return;

        Debug.Log(
            $"[PodeEnxergarSensorDebug] Unit={selectedUnit.name} | LoS={enableLos} | Spotter={enableSpotter} | " +
            $"Cenarios={scenarioResults.Count}");
        for (int s = 0; s < scenarioResults.Count; s++)
        {
            VisionScenarioResult scenario = scenarioResults[s];
            if (scenario == null)
                continue;

            Debug.Log(
                $"[PodeEnxergarSensorDebug][SCENARIO] {s + 1}. {scenario.label} | layer={scenario.domain}/{scenario.heightLevel} | " +
                $"forced={scenario.forcedLayer} | range={scenario.range} | validos={scenario.visibleHexes.Count} | invalidos={scenario.invalidHexes.Count}");
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
            label = $"{labelPrefix} [hex {targetCell.x},{targetCell.y}]"
        });
        currentDrawBatchKey = string.Empty;
        SceneView.RepaintAll();
    }

    private void DrawAllValidLines()
    {
        if (scenarioResults.Count <= 0)
            return;

        ToggleDrawScenarioBatchLines(0, drawValid: true);
    }

    private void DrawAllInvalidLines()
    {
        if (scenarioResults.Count <= 0)
            return;

        ToggleDrawScenarioBatchLines(0, drawValid: false);
    }

    private void ToggleDrawScenarioBatchLines(int scenarioIndex, bool drawValid)
    {
        if (scenarioIndex < 0 || scenarioIndex >= scenarioResults.Count)
            return;

        VisionScenarioResult scenario = scenarioResults[scenarioIndex];
        if (scenario == null)
            return;

        string key = $"{scenarioIndex}:{(drawValid ? "V" : "I")}";
        if (currentDrawBatchKey == key)
        {
            ClearSelectedLine();
            return;
        }

        DrawAllLinesFromEntries(
            drawValid ? scenario.visibleHexes : scenario.invalidHexes,
            drawValid ? Color.cyan : Color.red,
            drawValid ? $"VAL[{scenarioIndex + 1}]" : $"INV[{scenarioIndex + 1}]");
        currentDrawBatchKey = key;
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
                label = $"{tag} [hex {targetCell.x},{targetCell.y}]"
            });
        }

        SceneView.RepaintAll();
    }

    private void ClearSelectedLine()
    {
        sceneLines.Clear();
        currentDrawBatchKey = string.Empty;
        SceneView.RepaintAll();
    }

    private VisionScenarioResult BuildScenarioResult(
        Tilemap map,
        TerrainDatabase db,
        bool enableLos,
        bool enableSpotter,
        string label,
        Domain domain,
        HeightLevel heightLevel,
        int maxRange,
        bool forceLayer,
        bool preserveObserverLayerRange,
        int forcedDetectionRangeOverride = -1,
        bool skipSpecializedTargetLayers = false)
    {
        VisionScenarioResult result = new VisionScenarioResult
        {
            label = label,
            domain = domain,
            heightLevel = heightLevel,
            range = Mathf.Max(0, maxRange),
            forcedLayer = forceLayer,
            baseVisionOnly = skipSpecializedTargetLayers
        };

        bool rangeOnlyForAirHigh = domain == Domain.Air &&
            heightLevel == HeightLevel.AirHigh &&
            dpqAirHeightConfig != null &&
            dpqAirHeightConfig.TryGetVisionFor(Domain.Air, HeightLevel.AirHigh, out _, out bool airHighBlockLoS) &&
            !airHighBlockLoS;

        HashSet<Vector3Int> localVisibleCells = new HashSet<Vector3Int>();
        PodeDetectarSensor.CollectVisibleCells(
            selectedUnit,
            map,
            db,
            localVisibleCells,
            dpqAirHeightConfig,
            enableLos,
            enableSpotter,
            useOccupantLayerForTarget: false,
            preserveObserverLayerRangeForHexVisibility: preserveObserverLayerRange,
            forceVirtualTargetLayer: forceLayer,
            forcedVirtualTargetDomain: domain,
            forcedVirtualTargetHeight: heightLevel,
            forcedDetectionRangeOverride: forcedDetectionRangeOverride,
            skipSpecializedTargetLayers: skipSpecializedTargetLayers,
            useRangeOnlyForAirHighWhenConfigured: true);

        Dictionary<Vector3Int, int> distances = BuildDistanceMap(
            map,
            selectedUnit.CurrentCellPosition,
            result.range);

        foreach (Vector3Int rawCell in localVisibleCells)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;

            ResolveVirtualLayerForCell(
                map,
                db,
                cell,
                out Domain resolvedDomain,
                out HeightLevel resolvedHeightLevel,
                out string source,
                forceLayer,
                domain,
                heightLevel);
            distances.TryGetValue(cell, out int distance);
            result.visibleHexes.Add(new VisibleHexEntry
            {
                scenarioLabel = result.label,
                cell = cell,
                distance = distance,
                domain = resolvedDomain,
                heightLevel = resolvedHeightLevel,
                layerSource = source
            });
            ApplyLineDebugToEntry(
                result.visibleHexes[result.visibleHexes.Count - 1],
                map,
                db,
                cell,
                enableLos,
                domain,
                heightLevel,
                forceLayer,
                rangeOnlyForAirHigh);
        }

        foreach (KeyValuePair<Vector3Int, int> pair in distances)
        {
            Vector3Int cell = pair.Key;
            cell.z = 0;
            if (pair.Value <= 0)
                continue;
            if (localVisibleCells.Contains(cell))
                continue;

            ResolveVirtualLayerForCell(
                map,
                db,
                cell,
                out Domain resolvedDomain,
                out HeightLevel resolvedHeightLevel,
                out string source,
                forceLayer,
                domain,
                heightLevel);
            result.invalidHexes.Add(new VisibleHexEntry
            {
                scenarioLabel = result.label,
                cell = cell,
                distance = pair.Value,
                domain = resolvedDomain,
                heightLevel = resolvedHeightLevel,
                layerSource = source
            });
            ApplyLineDebugToEntry(
                result.invalidHexes[result.invalidHexes.Count - 1],
                map,
                db,
                cell,
                enableLos,
                domain,
                heightLevel,
                forceLayer,
                rangeOnlyForAirHigh);
        }

        SortVisibleHexEntries(result.visibleHexes);
        SortVisibleHexEntries(result.invalidHexes);
        return result;
    }

    private void ApplyLineDebugToEntry(
        VisibleHexEntry entry,
        Tilemap map,
        TerrainDatabase db,
        Vector3Int targetCell,
        bool enableLos,
        Domain forcedDomain,
        HeightLevel forcedHeight,
        bool forceLayer,
        bool rangeOnlyForAirHigh)
    {
        if (entry == null)
            return;

        entry.rangeOnlyMode = rangeOnlyForAirHigh;
        if (rangeOnlyForAirHigh)
            return;

        Domain? forcedTargetDomain = forceLayer ? forcedDomain : null;
        HeightLevel? forcedTargetHeight = forceLayer ? forcedHeight : null;
        bool hasDirectLos = PodeDetectarSensor.TryGetObservationLineDebug(
            selectedUnit,
            map,
            db,
            targetCell,
            dpqAirHeightConfig,
            enableLos,
            forcedTargetDomain,
            forcedTargetHeight,
            out float finalReachedEv,
            out float losHeightAtBlockedCell,
            out int blockedCellEv,
            out Vector3Int blockedCell,
            out float losHeightAtPassedCell,
            out int passedCellEv,
            out Vector3Int passedCell,
            out List<float> lineRiseHeights);

        entry.hasDirectLos = hasDirectLos;
        entry.finalReachedEv = finalReachedEv;
        entry.losHeightAtBlockedCell = losHeightAtBlockedCell;
        entry.blockedCellEv = blockedCellEv;
        entry.blockedCell = blockedCell;
        entry.blockedLayerLabel = ResolveBlockedLayerLabel(map, db, blockedCell, blockedCellEv);
        entry.losHeightAtPassedCell = losHeightAtPassedCell;
        entry.passedCellEv = passedCellEv;
        entry.passedCell = passedCell;
        entry.passedLayerLabel = ResolveBlockedLayerLabel(map, db, passedCell, passedCellEv);
        entry.lineRiseTrace = FormatLineRiseTrace(lineRiseHeights);
    }

    private static string FormatLineRiseTrace(List<float> heights)
    {
        if (heights == null || heights.Count <= 0)
            return string.Empty;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < heights.Count; i++)
        {
            if (i > 0)
                sb.Append(" -> ");
            sb.Append(heights[i].ToString("0.00"));
        }

        return sb.ToString();
    }

    private static string ResolveBlockedLayerLabel(Tilemap map, TerrainDatabase db, Vector3Int blockedCell, int blockedEv)
    {
        if (blockedCell == Vector3Int.zero)
            return string.Empty;

        blockedCell.z = 0;
        bool hasTerrain = TryResolveTerrainAtCell(map, db, blockedCell, out TerrainTypeData terrain) && terrain != null;
        if (map != null)
        {
            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(map, blockedCell);
            if (construction != null)
            {
                ConstructionDatabase constructionDb = construction.ConstructionDatabase;
                string constructionId = construction.ConstructionId;
                if (constructionDb != null &&
                    !string.IsNullOrWhiteSpace(constructionId) &&
                    constructionDb.TryGetById(constructionId, out ConstructionData constructionData) &&
                    constructionData != null)
                {
                    string constructionName = ResolveEntityLabel(constructionData.displayName, constructionData.id, constructionData.name);
                    int displayEv = hasTerrain &&
                        terrain.TryGetConstructionVisionOverride(constructionData, out int overrideEv, out _)
                        ? Mathf.Max(0, overrideEv)
                        : (hasTerrain ? Mathf.Max(0, terrain.ev) : Mathf.Max(0, blockedEv));
                    return $"{constructionName} (EV: {displayEv})";
                }
            }

            StructureData structure = StructureOccupancyRules.GetStructureAtCell(map, blockedCell);
            if (structure != null)
            {
                string structureName = ResolveEntityLabel(structure.displayName, structure.id, structure.name);
                int displayEv = hasTerrain &&
                    terrain.TryGetStructureVisionOverride(structure, out int overrideEv, out _)
                    ? Mathf.Max(0, overrideEv)
                    : (hasTerrain ? Mathf.Max(0, terrain.ev) : Mathf.Max(0, blockedEv));
                return $"{structureName} (EV: {displayEv})";
            }
        }

        if (hasTerrain)
        {
            string terrainName = ResolveEntityLabel(terrain.displayName, terrain.id, terrain.name);
            return $"{terrainName} (EV: {Mathf.Max(0, terrain.ev)})";
        }

        return $"bloqueador sem nome (EV: {Mathf.Max(0, blockedEv)})";
    }

    private static string ResolveEntityLabel(string displayName, string id, string assetName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName.Trim();
        if (!string.IsNullOrWhiteSpace(id))
            return id.Trim();
        if (!string.IsNullOrWhiteSpace(assetName))
            return assetName.Trim();
        return "sem_nome";
    }

    private static void SortVisibleHexEntries(List<VisibleHexEntry> entries)
    {
        if (entries == null)
            return;

        entries.Sort((a, b) =>
        {
            int distCompare = a.distance.CompareTo(b.distance);
            if (distCompare != 0)
                return distCompare;
            int yCompare = b.cell.y.CompareTo(a.cell.y);
            if (yCompare != 0)
                return yCompare;
            return a.cell.x.CompareTo(b.cell.x);
        });
    }

    private void DrawScenarioResultCard(int index, VisionScenarioResult scenario)
    {
        if (scenario == null)
            return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            $"{index + 1}. {scenario.label}",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Camada virtual", $"{scenario.domain}/{scenario.heightLevel}");
        EditorGUILayout.LabelField("Range aplicado", scenario.range.ToString());
        EditorGUILayout.LabelField("Modo", scenario.baseVisionOnly ? "Visao base (nao especializado)" : "Especializado/forcado");
        EditorGUILayout.LabelField("Hexes visiveis", scenario.visibleHexes.Count.ToString());
        EditorGUILayout.LabelField("Hexes invalidos", scenario.invalidHexes.Count.ToString());
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Desenhar todas as validas"))
            ToggleDrawScenarioBatchLines(index, drawValid: true);
        if (GUILayout.Button("Desenhar todas as invalidas"))
            ToggleDrawScenarioBatchLines(index, drawValid: false);
        EditorGUILayout.EndHorizontal();

        scenario.detailsExpanded = EditorGUILayout.Foldout(
            scenario.detailsExpanded,
            "Relatorio hex a hex (opcional)",
            true);

        if (scenario.detailsExpanded)
        {
            EditorGUI.indentLevel++;

            scenario.validListExpanded = EditorGUILayout.Foldout(
                scenario.validListExpanded,
                $"Validos ({scenario.visibleHexes.Count})",
                true);
            if (scenario.validListExpanded)
            {
                for (int i = 0; i < scenario.visibleHexes.Count; i++)
                    DrawVisibleHexItem(i, scenario.visibleHexes[i]);
            }

            scenario.invalidListExpanded = EditorGUILayout.Foldout(
                scenario.invalidListExpanded,
                $"Invalidos ({scenario.invalidHexes.Count})",
                true);
            if (scenario.invalidListExpanded)
            {
                for (int i = 0; i < scenario.invalidHexes.Count; i++)
                    DrawInvalidHexItem(i, scenario.invalidHexes[i]);
            }

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
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
        out string source,
        bool forceLayer,
        Domain forcedDomain,
        HeightLevel forcedHeight)
    {
        domain = Domain.Land;
        heightLevel = HeightLevel.Surface;
        source = "fallback";

        cell.z = 0;

        if (forceLayer)
        {
            domain = forcedDomain;
            heightLevel = forcedHeight;
            source = "forced";
            return;
        }

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
