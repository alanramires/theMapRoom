using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CaminhosValidosWindow : EditorWindow
{
    private enum ProgressDebugIntent
    {
        Bruto,
        CaptureAdvance,
        AssaultPressure,
        FireSupportReposition,
        FireSupportRendezvous,
        LogisticsService,
        LogisticsReload,
        RepairReturn,
        TransportDelivery,
        TransportRendezvous,
        ObserverSpot
    }

    private struct ProgressDebugCandidate
    {
        public int ToolScore;
        public float NextDistance;
        public int MoveCost;
        public bool RoadBonus;
        public float FirstTurnProgress;
        public float TwoTurnProgress;
        public float LineDeviation;
        public float Threat;
        public float Dpq;
        public float FinalScore;
    }

    [SerializeField] private UnitData unitData;
    [SerializeField] private UnitDatabase unitDatabase;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private bool calculateAircraftAsAirborne = true;

    [SerializeField] private Vector3Int originHex;
    [SerializeField] private int movementPoints = 3;
    [SerializeField] private Vector3Int destinationHex;
    [SerializeField] private Vector3Int waypointHex;
    [SerializeField] private int progressHorizonTurns = 2;
    [SerializeField] private ProgressDebugIntent progressIntent = ProgressDebugIntent.TransportDelivery;
    [SerializeField] private bool progressIntentInitialized;

    [SerializeField] private TeamId simulationTeam = TeamId.Green;

    private bool useSceneUnit;
    private UnitManager sceneUnit;

    private static readonly FieldInfo s_teamIdField =
        typeof(UnitManager).GetField("teamId", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo s_unitDatabaseField =
        typeof(UnitManager).GetField("unitDatabase", BindingFlags.NonPublic | BindingFlags.Instance);

    private bool pickingOrigin;
    private bool pickingDestination;
    private bool pickingWaypoint;
    private Vector3Int hoverCell;

    // Caminhos Alcançáveis
    private HashSet<Vector3Int> reachableCells;
    private Vector3Int lastOrigin;

    // Distância até Destino
    private int distanceCost = -1;
    private Vector3Int lastDest;
    private bool hasDistResult;
    private Dictionary<Vector3Int, int> distanceCostMap;

    // Progressão de Rota
    private Dictionary<Vector3Int, int> progressMap;
    private Dictionary<Vector3Int, ProgressDebugCandidate> progressCandidateMap;
    private bool hasProgressResult;
    private Vector3Int lastProgressOrigin;
    private Vector3Int lastProgressDest;
    private int progressOriginCost;

    // Passando Por
    private bool hasWaypointResult;
    private Vector3Int lastWayOrigin, lastWayWaypoint, lastWayDest;
    private int waypointLeg1Cost = -1;   // A → B (terreno)
    private int waypointLeg2Cost = -1;   // B → C (terreno)
    private Dictionary<Vector3Int, int> waypointCostFromDest;   // custo terreno de C para cada célula
    private HashSet<Vector3Int> waypointReachable;              // alcançáveis de A no turno (com ocupação)

    private string statusMessage = string.Empty;
    private Vector2 scroll;

    [MenuItem("Tools/Transporte/Caminhos Válidos")]
    public static void Open() => GetWindow<CaminhosValidosWindow>("Caminhos Válidos").Show();

    private void OnEnable()
    {
        if (!progressIntentInitialized)
        {
            progressIntent = ProgressDebugIntent.TransportDelivery;
            progressIntentInitialized = true;
        }

        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetectContext();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        pickingOrigin = false;
        pickingDestination = false;
        pickingWaypoint = false;
    }

    private void OnSelectionChange()
    {
        if (!useSceneUnit) return;
        RefreshSceneUnit();
        Repaint();
    }

    private void RefreshSceneUnit()
    {
        sceneUnit = null;
        if (Selection.activeGameObject != null)
            sceneUnit = Selection.activeGameObject.GetComponent<UnitManager>();
    }

    private static readonly FieldInfo s_spawnerUnitDatabaseField =
        typeof(UnitSpawner).GetField("unitDatabase", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo s_spawnerTilemapField =
        typeof(UnitSpawner).GetField("boardTilemap", BindingFlags.NonPublic | BindingFlags.Instance);

    private void AutoDetectContext()
    {
        if (tilemap == null)
        {
            UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
            if (spawner != null)
                tilemap = s_spawnerTilemapField?.GetValue(spawner) as Tilemap;
            if (tilemap == null) tilemap = FindFirstObjectByType<Tilemap>();
        }
        if (terrainDatabase == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
            if (guids.Length > 0)
                terrainDatabase = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (unitDatabase == null)
        {
            UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
            if (spawner != null)
                unitDatabase = s_spawnerUnitDatabaseField?.GetValue(spawner) as UnitDatabase;
            if (unitDatabase == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:UnitDatabase");
                if (guids.Length > 0)
                    unitDatabase = AssetDatabase.LoadAssetAtPath<UnitDatabase>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
    }

    private UnitData ActiveUnitData()
    {
        if (useSceneUnit && sceneUnit != null)
        {
            sceneUnit.TryGetUnitData(out UnitData d);
            return d;
        }
        return unitData;
    }

    private bool HasValidUnit() => ActiveUnitData() != null && tilemap != null;

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        // ── Unidade ──
        EditorGUILayout.LabelField("Unidade", EditorStyles.boldLabel);

        bool newUseScene = EditorGUILayout.Toggle("Usar unidade selecionada", useSceneUnit);
        if (newUseScene != useSceneUnit)
        {
            useSceneUnit = newUseScene;
            if (useSceneUnit) RefreshSceneUnit();
        }

        if (useSceneUnit)
        {
            if (sceneUnit != null)
            {
                UnitData d = ActiveUnitData();
                string info = d != null
                    ? $"  {sceneUnit.name}  mov={d.movement}  PM restantes={sceneUnit.RemainingMovementPoints}  {sceneUnit.CurrentCellPosition}  layer={sceneUnit.GetDomain()}/{sceneUnit.GetHeightLevel()}  calc={DescribeEffectiveCalculationLayer(d)}  db={DescribeActiveUnitDatabase(d)}"
                    : $"  {sceneUnit.name}  (sem UnitData)";
                EditorGUILayout.LabelField(info, EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("→ Origem", GUILayout.Width(90)))
                {
                    Vector3Int p = sceneUnit.CurrentCellPosition; p.z = 0;
                    originHex = p;
                }
                EditorGUI.BeginDisabledGroup(d == null || sceneUnit.RemainingMovementPoints <= 0);
                if (GUILayout.Button("PM atuais", GUILayout.Width(90)))
                    movementPoints = Mathf.Max(1, sceneUnit.RemainingMovementPoints);
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(d == null);
                if (GUILayout.Button("PM máx", GUILayout.Width(90)))
                    movementPoints = d != null ? Mathf.Max(1, d.movement) : movementPoints;
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Selecione uma unidade na cena (Hierarchy ou Scene View).", MessageType.Info);
            }
        }
        else
        {
            unitData = (UnitData)EditorGUILayout.ObjectField("UnitData", unitData, typeof(UnitData), false);
            if (unitData != null)
                EditorGUILayout.LabelField($"  mov={unitData.movement}  classe={unitData.unitClass}", EditorStyles.miniLabel);
            simulationTeam = (TeamId)EditorGUILayout.EnumPopup("Time (simulação)", simulationTeam);
        }

        tilemap         = (Tilemap)EditorGUILayout.ObjectField("Tilemap", tilemap, typeof(Tilemap), true);
        unitDatabase    = (UnitDatabase)EditorGUILayout.ObjectField("Unit Database", unitDatabase, typeof(UnitDatabase), false);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        calculateAircraftAsAirborne = EditorGUILayout.Toggle("Aeronave calcula em voo", calculateAircraftAsAirborne);

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Limpar Resultados", GUILayout.Height(22)))
            ClearAll();

        EditorGUILayout.Space(8f);

        // ── Caminhos Alcançáveis ──
        EditorGUILayout.LabelField("Caminhos Alcançáveis", EditorStyles.boldLabel);
        DrawHexField("Origem", ref originHex, ref pickingOrigin, ref pickingDestination, Color.yellow);
        movementPoints = EditorGUILayout.IntSlider("Pontos de Movimento", movementPoints, 1, 99);

        EditorGUI.BeginDisabledGroup(!HasValidUnit());
        if (GUILayout.Button("Calcular Caminhos", GUILayout.Height(26)))
            RunPathCalculation();
        EditorGUI.EndDisabledGroup();

        if (reachableCells != null)
            EditorGUILayout.LabelField($"{reachableCells.Count} hexes alcançáveis de {lastOrigin} com PM={movementPoints}", EditorStyles.miniLabel);

        EditorGUILayout.Space(8f);

        // ── Distância até Destino ──
        EditorGUILayout.LabelField("Distância até Destino", EditorStyles.boldLabel);
        DrawHexField("Destino", ref destinationHex, ref pickingDestination, ref pickingOrigin, Color.cyan);

        EditorGUI.BeginDisabledGroup(!HasValidUnit());
        if (GUILayout.Button("Calcular Distância", GUILayout.Height(26)))
            RunDistanceCalculation();
        EditorGUI.EndDisabledGroup();

        if (hasDistResult)
        {
            string r = distanceCost >= 0
                ? $"{lastOrigin} -> {lastDest} = {distanceCost} PM"
                : $"{lastDest} inacessível a partir de {lastOrigin}";
            EditorGUILayout.LabelField(r, EditorStyles.boldLabel);
        }

        EditorGUILayout.Space(8f);

        // ── Progressão de Rota ──
        EditorGUILayout.LabelField("Progressão de Rota", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  Progresso de rota do AI por célula alcançável (respeita ocupação por aliados).", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("  Verde = aproxima do destino | Vermelho = afasta | Amarelo = neutro", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("  Usa Origem A (amarelo) e Destino B (ciano) definidos acima.", EditorStyles.miniLabel);
        progressHorizonTurns = EditorGUILayout.IntSlider("Horizonte", progressHorizonTurns, 1, 2);
        progressIntent = (ProgressDebugIntent)EditorGUILayout.EnumPopup("Intent runtime", progressIntent);
        EditorGUILayout.LabelField(progressIntent == ProgressDebugIntent.Bruto
            ? "  Exibe toolScore bruto da progressao em dois turnos."
            : "  Exibe FinalScore/1000 com a formula runtime do intent selecionado.", EditorStyles.miniLabel);

        EditorGUI.BeginDisabledGroup(!HasValidUnit());
        if (GUILayout.Button("Calcular Progressão", GUILayout.Height(26)))
            RunProgressCalculation();
        EditorGUI.EndDisabledGroup();

        if (hasProgressResult)
        {
            string pr = progressOriginCost >= 0
                ? $"Origem ({lastProgressOrigin}) -> Destino ({lastProgressDest}) = {progressOriginCost} PM base"
                : $"Destino ({lastProgressDest}) inacessível a partir de {lastProgressOrigin}";
            EditorGUILayout.LabelField(pr, EditorStyles.boldLabel);
        }

        EditorGUILayout.Space(8f);

        // ── Passando Por ──
        /*
        // Passando Por removido: a progressao agora cobre a analise de duas rodadas.
        EditorGUILayout.LabelField("  Rota A → B → C: compara custo via waypoint com a rota direta.", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("  Gradiente = PM restantes até C (terreno, sem ocupação).", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("  Azul = células alcançáveis de A no turno atual (com ocupação).", EditorStyles.miniLabel);

        // Via (B) — waypoint picker (cancela os outros dois)
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        waypointHex = EditorGUILayout.Vector3IntField("Via (B)", waypointHex);
        if (EditorGUI.EndChangeCheck()) { hasWaypointResult = false; SceneView.RepaintAll(); }
        GUI.backgroundColor = pickingWaypoint ? Color.magenta : Color.white;
        if (GUILayout.Button(pickingWaypoint ? "■" : "↖", GUILayout.Width(28)))
        {
            pickingWaypoint = !pickingWaypoint;
            if (pickingWaypoint) { pickingOrigin = false; pickingDestination = false; }
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"  Destino (C) = {destinationHex}  |  Origem (A) = {originHex}", EditorStyles.miniLabel);

        EditorGUI.BeginDisabledGroup(!HasValidUnit());
        if (GUILayout.Button("Calcular Via B", GUILayout.Height(26)))
            RunWaypointCalculation();
        EditorGUI.EndDisabledGroup();

        if (hasWaypointResult)
        {
            string leg1 = waypointLeg1Cost >= 0 ? $"{waypointLeg1Cost} PM" : "?";
            string leg2 = waypointLeg2Cost >= 0 ? $"{waypointLeg2Cost} PM" : "?";
            string total = (waypointLeg1Cost >= 0 && waypointLeg2Cost >= 0)
                ? $"{waypointLeg1Cost + waypointLeg2Cost} PM"
                : "?";
            EditorGUILayout.LabelField($"A→B = {leg1}  |  B→C = {leg2}  |  Total = {total}", EditorStyles.boldLabel);
        }

        */

        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, MessageType.Error);

        if (!HasValidUnit())
            EditorGUILayout.HelpBox("UnitData (ou unidade selecionada) e Tilemap são obrigatórios.", MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private void DrawHexField(string label, ref Vector3Int hex,
        ref bool myPicking, ref bool otherPicking, Color pickColor)
    {
        EditorGUILayout.BeginHorizontal();
        hex = EditorGUILayout.Vector3IntField(label, hex);
        GUI.backgroundColor = myPicking ? pickColor : Color.white;
        if (GUILayout.Button(myPicking ? "■" : "↖", GUILayout.Width(28)))
        {
            myPicking = !myPicking;
            if (myPicking) { otherPicking = false; pickingWaypoint = false; }
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // ── Cálculos ─────────────────────────────────────────────────────────────

    private void RunPathCalculation()
    {
        reachableCells = null;
        hasDistResult = false;
        distanceCostMap = null;
        hasProgressResult = false;
        progressMap = null;
        progressCandidateMap = null;
        hasWaypointResult = false;
        statusMessage = string.Empty;

        Vector3Int origin = originHex; origin.z = 0;
        RunWithTempUnit(origin, unit =>
        {
            var paths = UnitMovementPathRules.CalcularCaminhosValidos(tilemap, unit, movementPoints, terrainDatabase);
            reachableCells = new HashSet<Vector3Int>();
            foreach (Vector3Int cell in paths.Keys)
            {
                if (CanUseAsDebugStopCell(unit, cell, origin))
                    reachableCells.Add(cell);
            }
            lastOrigin = origin;
        });

        SceneView.RepaintAll();
        Repaint();
    }

    private void RunDistanceCalculation()
    {
        hasDistResult = false;
        reachableCells = null;
        hasProgressResult = false;
        progressMap = null;
        progressCandidateMap = null;
        hasWaypointResult = false;
        statusMessage = string.Empty;

        Vector3Int origin = originHex; origin.z = 0;
        Vector3Int dest   = destinationHex; dest.z = 0;
        RunWithTempUnit(origin, unit =>
        {
            var costMap = UnitMovementPathRules.CalculateMovementCostMap(tilemap, unit, origin, 99, terrainDatabase);
            distanceCostMap = costMap;
            distanceCost = costMap.TryGetValue(dest, out int c) ? c : -1;
            lastOrigin = origin;
            lastDest = dest;
            hasDistResult = true;
        });

        SceneView.RepaintAll();
        Repaint();
    }

    private void RunProgressCalculation()
    {
        hasProgressResult = false;
        progressMap = null;
        progressCandidateMap = null;
        reachableCells = null;
        hasDistResult = false;
        distanceCostMap = null;
        hasWaypointResult = false;
        statusMessage = string.Empty;

        Vector3Int origin = originHex; origin.z = 0;
        Vector3Int dest   = destinationHex; dest.z = 0;
        // Células ocupadas por aliados: o unit pode passar mas não pode parar lá.
        var alliedOccupied = new HashSet<Vector3Int>();
        TeamId activeTeam = ActiveTeam();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != activeTeam || u.IsDead) continue;
            Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
            if (uc == origin) continue; // a própria unidade sai da origem
            alliedOccupied.Add(uc);
        }

        RunWithTempUnit(origin, unit =>
        {
            var paths = UnitMovementPathRules.CalcularCaminhosValidos(tilemap, unit, movementPoints, terrainDatabase);
            // costMap dá o custo real em PM para cada célula, considerando bônus de estrada.
            var costMap = UnitMovementPathRules.CalculateMovementCostMap(tilemap, unit, origin, movementPoints, terrainDatabase);
            progressOriginCost = Mathf.CeilToInt(SectorManager.HexDistance(origin, dest));
            lastProgressOrigin = origin;
            lastProgressDest   = dest;

            progressMap = new Dictionary<Vector3Int, int>();
            progressCandidateMap = new Dictionary<Vector3Int, ProgressDebugCandidate>();
            var debugStopOrigin = origin;
            int horizon = Mathf.Clamp(progressHorizonTurns, 1, 2);
            foreach (Vector3Int cell in paths.Keys)
            {
                if (!CanUseAsDebugStopCell(unit, cell, debugStopOrigin)) continue;
                if (alliedOccupied.Contains(cell)) continue; // não pode parar aqui
                ProgressDebugCandidate candidate;
                int rawScore;
                if (horizon <= 1)
                {
                    rawScore = CalculateReachableProgressScore(origin, dest, cell, paths[cell]);
                    candidate = BuildOneTurnProgressCandidate(unit, origin, dest, cell, paths[cell], costMap);
                }
                else
                {
                    rawScore = CalculateTwoTurnProgressScore(unit, origin, dest, cell, paths[cell], costMap, out candidate);
                }

                candidate.FinalScore = progressIntent == ProgressDebugIntent.Bruto
                    ? rawScore
                    : ScoreProgressIntent(progressIntent, candidate);

                progressCandidateMap[cell] = candidate;
                progressMap[cell] = progressIntent == ProgressDebugIntent.Bruto
                    ? rawScore
                    : Mathf.RoundToInt(candidate.FinalScore / 1000f);
            }

            hasProgressResult = true;
        });

        SceneView.RepaintAll();
        Repaint();
    }

    private void RunWaypointCalculation()
    {
        hasWaypointResult = false;
        reachableCells = null;
        hasDistResult = false;
        distanceCostMap = null;
        hasProgressResult = false;
        progressMap = null;
        progressCandidateMap = null;
        statusMessage = string.Empty;

        Vector3Int origin  = originHex;      origin.z = 0;
        Vector3Int waypoint = waypointHex;   waypoint.z = 0;
        Vector3Int dest    = destinationHex; dest.z = 0;

        RunWithTempUnit(origin, unit =>
        {
            // Leg 1: custo terreno de A até B (ignora ocupação — mostra custo ideal da rota)
            var costFromOrigin = UnitMovementPathRules.CalculateMovementCostMap(tilemap, unit, origin, 99, terrainDatabase);
            waypointLeg1Cost = costFromOrigin.TryGetValue(waypoint, out int l1) ? l1 : -1;

            // Leg 2: custo terreno de B até C — usa BFS de C, lê o custo em B
            waypointCostFromDest = UnitMovementPathRules.CalculateMovementCostMap(tilemap, unit, dest, 99, terrainDatabase);
            waypointLeg2Cost = waypointCostFromDest.TryGetValue(waypoint, out int l2) ? l2 : -1;

            // Células alcançáveis de A no turno atual (respeita ocupação — mostra congestionamento real)
            var reachPaths = UnitMovementPathRules.CalcularCaminhosValidos(tilemap, unit, movementPoints, terrainDatabase);
            waypointReachable = new HashSet<Vector3Int>();
            foreach (Vector3Int cell in reachPaths.Keys)
            {
                if (CanUseAsDebugStopCell(unit, cell, origin))
                    waypointReachable.Add(cell);
            }

            lastWayOrigin   = origin;
            lastWayWaypoint = waypoint;
            lastWayDest     = dest;
            hasWaypointResult = true;
        });

        SceneView.RepaintAll();
        Repaint();
    }

    private void ClearAll()
    {
        reachableCells = null;
        hasDistResult = false;
        distanceCostMap = null;
        hasProgressResult = false;
        progressMap = null;
        progressCandidateMap = null;
        hasWaypointResult = false;
        waypointCostFromDest = null;
        waypointReachable = null;
        statusMessage = string.Empty;
        SceneView.RepaintAll();
        Repaint();
    }

    private TeamId ActiveTeam() => useSceneUnit && sceneUnit != null ? sceneUnit.TeamId : simulationTeam;

    private void RunWithTempUnit(Vector3Int atCell, System.Action<UnitManager> action)
    {
        UnitData data = ActiveUnitData();
        if (data == null) { statusMessage = "Sem UnitData."; return; }

        var go = new GameObject("__CaminhosValidosTemp__");
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var unit = go.AddComponent<UnitManager>();
            UnitDatabase db = ActiveUnitDatabase(data);
            if (db != null)
                s_unitDatabaseField?.SetValue(unit, db);
            unit.Apply(data);
            unit.SetAutonomia(9999, refillCurrentFuel: true);
            s_teamIdField?.SetValue(unit, ActiveTeam());
            unit.SetCurrentCellPosition(atCell, enforceFinalOccupancyRule: false);
            PrepareTempUnitLayer(unit, data);
            action(unit);
        }
        catch (System.Exception e) { statusMessage = e.Message; }
        finally { DestroyImmediate(go); }
    }

    private bool CanUseAsDebugStopCell(UnitManager mover, Vector3Int cell, Vector3Int origin)
    {
        if (mover == null)
            return false;

        cell.z = 0;
        origin.z = 0;
        if (cell == origin)
            return true;

        HeightBand moverBand = OccupancyResolver.GetHeightBand(mover);
        if (moverBand != HeightBand.Blocking)
            return true;

        foreach (UnitManager occupant in UnitManager.AllActive)
        {
            if (occupant == null || occupant.IsDead || occupant.IsEmbarked)
                continue;
            if (useSceneUnit && sceneUnit != null && occupant == sceneUnit)
                continue;

            Vector3Int occupantCell = occupant.CurrentCellPosition;
            occupantCell.z = 0;
            if (occupantCell != cell)
                continue;

            occupant.SyncLayerStateFromData(forceNativeDefault: false);
            if (OccupancyResolver.GetHeightBand(occupant) != moverBand)
                continue;

            // Same blocking band + same team: allied units can never share a blocking hex.
            if (occupant.TeamId == mover.TeamId)
                return false;

            // Same blocking band + enemy: only stoppable in TW mode.
            if (!OccupancyResolver.IsLayerAwareRulesActive)
                return false;
        }

        return true;
    }

    private UnitDatabase ActiveUnitDatabase(UnitData data)
    {
        if (useSceneUnit && sceneUnit != null)
        {
            UnitDatabase sceneDb = s_unitDatabaseField?.GetValue(sceneUnit) as UnitDatabase;
            if (DatabaseContainsExactData(sceneDb, data))
                return sceneDb;
        }

        if (DatabaseContainsExactData(unitDatabase, data))
            return unitDatabase;

        UnitDatabase exactDb = FindUnitDatabaseFor(data, requireExactAsset: true);
        if (exactDb != null)
            return exactDb;

        if (useSceneUnit && sceneUnit != null)
        {
            UnitDatabase sceneDb = s_unitDatabaseField?.GetValue(sceneUnit) as UnitDatabase;
            if (DatabaseContainsCompatibleData(sceneDb, data))
                return sceneDb;
        }

        if (DatabaseContainsCompatibleData(unitDatabase, data))
            return unitDatabase;

        return FindUnitDatabaseFor(data, requireExactAsset: false);
    }

    private static bool DatabaseContainsExactData(UnitDatabase db, UnitData data)
    {
        if (db == null || data == null || string.IsNullOrWhiteSpace(data.id))
            return false;

        return db.TryGetById(data.id, out UnitData resolved) && resolved == data;
    }

    private static bool DatabaseContainsCompatibleData(UnitDatabase db, UnitData data)
    {
        if (db == null || data == null || string.IsNullOrWhiteSpace(data.id))
            return false;
        if (!db.TryGetById(data.id, out UnitData resolved) || resolved == null)
            return false;
        if (resolved == data)
            return true;

        return resolved.unitClass == data.unitClass
            && resolved.IsAircraft() == data.IsAircraft()
            && (resolved.domain == data.domain || resolved.IsAircraft());
    }

    private static UnitDatabase FindUnitDatabaseFor(UnitData data, bool requireExactAsset)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.id))
            return null;

        string[] guids = AssetDatabase.FindAssets("t:UnitDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            UnitDatabase db = AssetDatabase.LoadAssetAtPath<UnitDatabase>(AssetDatabase.GUIDToAssetPath(guids[i]));
            bool contains = requireExactAsset
                ? DatabaseContainsExactData(db, data)
                : DatabaseContainsCompatibleData(db, data);
            if (contains)
                return db;
        }

        return null;
    }

    private void PrepareTempUnitLayer(UnitManager unit, UnitData data)
    {
        if (unit == null || data == null)
            return;

        bool shouldCalculateAirborne = calculateAircraftAsAirborne && data.IsAircraft();

        if (useSceneUnit && sceneUnit != null && !shouldCalculateAirborne)
            unit.ForceLayerStateForDebug(sceneUnit.GetDomain(), sceneUnit.GetHeightLevel());

        if (!shouldCalculateAirborne)
            return;

        HeightLevel airHeight = ResolveSceneOrDataAirHeight(data);
        unit.ForceLayerStateForDebug(Domain.Air, airHeight);
        unit.SetAircraftGrounded(false);
    }

    private string DescribeEffectiveCalculationLayer(UnitData data)
    {
        if (data == null)
            return "-";
        if (calculateAircraftAsAirborne && data.IsAircraft())
            return $"Air/{ResolveSceneOrDataAirHeight(data)}";
        if (useSceneUnit && sceneUnit != null)
            return $"{sceneUnit.GetDomain()}/{sceneUnit.GetHeightLevel()}";
        return $"{data.domain}/{data.heightLevel}";
    }

    private string DescribeActiveUnitDatabase(UnitData data)
    {
        UnitDatabase db = ActiveUnitDatabase(data);
        return db != null ? db.name : "-";
    }

    private HeightLevel ResolveSceneOrDataAirHeight(UnitData data)
    {
        if (useSceneUnit && sceneUnit != null && sceneUnit.GetDomain() == Domain.Air)
            return sceneUnit.GetHeightLevel() == HeightLevel.AirHigh ? HeightLevel.AirHigh : HeightLevel.AirLow;

        return ResolveDataPreferredAirHeight(data);
    }

    private static HeightLevel ResolveDataPreferredAirHeight(UnitData data)
    {
        if (data == null)
            return HeightLevel.AirLow;
        if (data.preferredAirHeight == HeightLevel.AirHigh)
            return HeightLevel.AirHigh;
        return HeightLevel.AirLow;
    }

    private static int CalculateRouteProgressScore(
        Vector3Int origin,
        Vector3Int dest,
        Vector3Int cell,
        int originCostFromDest,
        int cellCostFromDest)
    {
        int routeProgress = originCostFromDest - cellCostFromDest;
        float hexProgress = SectorManager.HexDistance(origin, dest) - SectorManager.HexDistance(cell, dest);
        float lineDeviation = DistanceFromHexLine(cell, origin, dest);

        float score =
            routeProgress * 10f
            + hexProgress * 2f
            - lineDeviation * 3f;

        return Mathf.RoundToInt(score);
    }

    private int CalculateTwoTurnProgressScore(
        UnitManager tempUnit,
        Vector3Int origin,
        Vector3Int dest,
        Vector3Int firstStop,
        IReadOnlyList<Vector3Int> firstPath,
        Dictionary<Vector3Int, int> costMap,
        out ProgressDebugCandidate candidate)
    {
        float originDistance = SectorManager.HexDistance(origin, dest);
        float bestDistanceAfterNextMove = SectorManager.HexDistance(firstStop, dest);
        // Real MP cost (road-bonus aware); fall back to path waypoint count if costMap absent.
        int firstMoveCost = costMap != null && costMap.TryGetValue(firstStop, out int c)
            ? c
            : (firstPath != null ? Mathf.Max(0, firstPath.Count - 1) : 0);

        Vector3Int originalCell = tempUnit.CurrentCellPosition;
        originalCell.z = 0;
        try
        {
            tempUnit.SetCurrentCellPosition(firstStop, enforceFinalOccupancyRule: false);
            var nextPaths = UnitMovementPathRules.CalcularCaminhosValidos(tilemap, tempUnit, movementPoints, terrainDatabase);
            foreach (Vector3Int nextStop in nextPaths.Keys)
            {
                if (!CanUseAsDebugStopCell(tempUnit, nextStop, firstStop))
                    continue;

                float nextDistance = SectorManager.HexDistance(nextStop, dest);
                if (nextDistance < bestDistanceAfterNextMove)
                    bestDistanceAfterNextMove = nextDistance;
            }
        }
        finally
        {
            tempUnit.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);
        }

        float twoTurnProgress = originDistance - bestDistanceAfterNextMove;
        // Tiebreaker: how close firstStop itself is to dest — credits road cells that cost the
        // same MP as a shorter non-road path but land geometrically closer to the objective.
        float firstTurnProgress = originDistance - SectorManager.HexDistance(firstStop, dest);
        float lineDeviation = DistanceFromHexLine(firstStop, origin, dest);

        float score =
            twoTurnProgress * 10f
            + firstTurnProgress * 2f
            - lineDeviation * 2f
            - firstMoveCost * 0.5f;

        candidate = BuildProgressCandidate(
            tempUnit,
            origin,
            dest,
            firstStop,
            firstPath,
            Mathf.RoundToInt(score),
            bestDistanceAfterNextMove,
            firstMoveCost,
            firstTurnProgress,
            twoTurnProgress,
            lineDeviation);

        return Mathf.RoundToInt(score);
    }

    private static int CalculateReachableProgressScore(
        Vector3Int origin,
        Vector3Int dest,
        Vector3Int cell,
        IReadOnlyList<Vector3Int> validPath)
    {
        float originDistance = SectorManager.HexDistance(origin, dest);
        float cellDistance = SectorManager.HexDistance(cell, dest);
        float hexProgress = originDistance - cellDistance;
        float lineDeviation = DistanceFromHexLine(cell, origin, dest);
        int pathSteps = validPath != null ? Mathf.Max(0, validPath.Count - 1) : 0;

        float score =
            hexProgress * 10f
            - lineDeviation * 3f
            - pathSteps * 0.5f;

        return Mathf.RoundToInt(score);
    }

    private ProgressDebugCandidate BuildOneTurnProgressCandidate(
        UnitManager tempUnit,
        Vector3Int origin,
        Vector3Int dest,
        Vector3Int cell,
        IReadOnlyList<Vector3Int> path,
        Dictionary<Vector3Int, int> costMap)
    {
        float originDistance = SectorManager.HexDistance(origin, dest);
        float firstTurnProgress = originDistance - SectorManager.HexDistance(cell, dest);
        float lineDeviation = DistanceFromHexLine(cell, origin, dest);
        int moveCost = costMap != null && costMap.TryGetValue(cell, out int c)
            ? c
            : (path != null ? Mathf.Max(0, path.Count - 1) : 0);
        float rawScore = firstTurnProgress * 10f - lineDeviation * 3f - moveCost * 0.5f;

        return BuildProgressCandidate(
            tempUnit,
            origin,
            dest,
            cell,
            path,
            Mathf.RoundToInt(rawScore),
            SectorManager.HexDistance(cell, dest),
            moveCost,
            firstTurnProgress,
            firstTurnProgress,
            lineDeviation);
    }

    private ProgressDebugCandidate BuildProgressCandidate(
        UnitManager tempUnit,
        Vector3Int origin,
        Vector3Int dest,
        Vector3Int cell,
        IReadOnlyList<Vector3Int> path,
        int toolScore,
        float nextDistance,
        int moveCost,
        float firstTurnProgress,
        float twoTurnProgress,
        float lineDeviation)
    {
        bool roadBonus = path != null
            && UnitMovementPathRules.DidUseRoadFullMoveBonus(tilemap, tempUnit, path, terrainDatabase);

        return new ProgressDebugCandidate
        {
            ToolScore = toolScore,
            NextDistance = nextDistance,
            MoveCost = moveCost,
            RoadBonus = roadBonus,
            FirstTurnProgress = firstTurnProgress,
            TwoTurnProgress = twoTurnProgress,
            LineDeviation = lineDeviation,
            Threat = CalculateDebugThreatLevel(cell, ActiveTeam()),
            Dpq = GetDebugTerrainDpqPontos(cell)
        };
    }

    private static float ScoreProgressIntent(ProgressDebugIntent intent, ProgressDebugCandidate candidate)
    {
        float score = candidate.ToolScore * 1000f
            + candidate.TwoTurnProgress * 220f
            + Mathf.Max(0f, candidate.FirstTurnProgress) * 90f
            - candidate.LineDeviation * 80f
            - candidate.MoveCost * 18f
            + (candidate.RoadBonus ? 650f : 0f);

        switch (intent)
        {
            case ProgressDebugIntent.FireSupportReposition:
            case ProgressDebugIntent.FireSupportRendezvous:
                score += candidate.Dpq * 35f;
                score -= candidate.Threat * 80f;
                break;
            case ProgressDebugIntent.LogisticsService:
            case ProgressDebugIntent.LogisticsReload:
                score += candidate.Dpq * 30f;
                score -= candidate.Threat * 120f;
                break;
            case ProgressDebugIntent.RepairReturn:
                score -= candidate.Threat * 60f;
                break;
            case ProgressDebugIntent.TransportDelivery:
            case ProgressDebugIntent.TransportRendezvous:
                score -= candidate.Threat * 50f;
                break;
            case ProgressDebugIntent.ObserverSpot:
                score += candidate.Dpq * 45f;
                score -= candidate.Threat * 90f;
                break;
            case ProgressDebugIntent.AssaultPressure:
            case ProgressDebugIntent.CaptureAdvance:
            default:
                score += candidate.Dpq * 20f;
                score -= candidate.Threat * 35f;
                break;
        }

        return score;
    }

    private float CalculateDebugThreatLevel(Vector3Int cell, TeamId aiTeam)
    {
        const int threatRadius = 3;
        float threat = 0f;
        cell.z = 0;
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked)
                continue;

            Vector3Int ec = enemy.CurrentCellPosition;
            ec.z = 0;
            float dist = SectorManager.HexDistance(cell, ec);
            if (dist <= threatRadius)
                threat += (threatRadius - dist + 1f) * 10f;
        }

        return threat;
    }

    private float GetDebugTerrainDpqPontos(Vector3Int cell)
    {
        if (tilemap == null || terrainDatabase == null)
            return 0f;

        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell);
        if (construction != null
            && construction.TryResolveConstructionData(out ConstructionData constructionData)
            && constructionData != null
            && constructionData.dpqData != null)
            return constructionData.dpqData.Pontos;

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(tilemap, cell);
        if (structure != null && structure.dpqData != null)
            return structure.dpqData.Pontos;

        TileBase tile = tilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data) && data?.dpqData != null)
            return data.dpqData.Pontos;

        GridLayout grid = tilemap.layoutGrid;
        if (grid != null)
        {
            Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
            for (int i = 0; i < maps.Length; i++)
            {
                Tilemap map = maps[i];
                if (map == null || map == tilemap)
                    continue;

                TileBase other = map.GetTile(cell);
                if (other != null && terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData otherData) && otherData?.dpqData != null)
                    return otherData.dpqData.Pontos;
            }
        }

        return 0f;
    }

    private static string FormatRouteScore(int score)
    {
        if (score % 10 == 0)
            return (score / 10).ToString();
        return (score / 10f).ToString("0.0");
    }

    private static float DistanceFromHexLine(Vector3Int cell, Vector3Int lineStart, Vector3Int lineEnd)
    {
        Vector2 p = new Vector2(cell.x, cell.y);
        Vector2 a = new Vector2(lineStart.x, lineStart.y);
        Vector2 b = new Vector2(lineEnd.x, lineEnd.y);
        Vector2 ab = b - a;
        float abLenSq = ab.sqrMagnitude;
        if (abLenSq <= 0.0001f)
            return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
        Vector2 projection = a + ab * t;
        return Vector2.Distance(p, projection);
    }

    // ── Scene GUI ─────────────────────────────────────────────────────────────

    private void OnSceneGUI(SceneView _)
    {
        if (tilemap == null) return;
        HandlePickingInput();

        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            normal = { textColor = Color.white }
        };

        // Caminhos alcançáveis
        if (reachableCells != null)
        {
            Handles.color = new Color(0.1f, 0.9f, 0.2f, 0.5f);
            foreach (Vector3Int cell in reachableCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.22f);

            Handles.color = new Color(1f, 0.85f, 0f, 0.9f);
            Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(lastOrigin), Vector3.back, 0.28f);
        }

        // Distância até destino
        if (hasDistResult && distanceCostMap != null)
        {
            int maxCost = distanceCost > 0 ? distanceCost : 1;

            foreach (var kv in distanceCostMap)
            {
                Vector3Int cell = kv.Key;
                int cost = kv.Value;
                if (cell == lastDest || cell == lastOrigin) continue;

                float t = Mathf.Clamp01((float)cost / maxCost);
                Color c = Color.Lerp(new Color(0.1f, 0.85f, 0.1f, 0.55f), new Color(0.85f, 0.3f, 0.1f, 0.55f), t);
                Handles.color = c;
                Vector3 cw = tilemap.GetCellCenterWorld(cell);
                Handles.DrawSolidDisc(cw, Vector3.back, 0.20f);
                Handles.Label(cw + Vector3.up * 0.01f, cost.ToString(), labelStyle);
            }

            Handles.color = new Color(1f, 0.85f, 0f, 0.9f);
            Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(lastOrigin), Vector3.back, 0.28f);

            Handles.color = new Color(1f, 0.15f, 0.15f, 0.9f);
            Vector3 dw = tilemap.GetCellCenterWorld(lastDest);
            Handles.DrawSolidDisc(dw, Vector3.back, 0.28f);
            Handles.Label(dw + Vector3.up * 0.38f,
                distanceCost >= 0 ? distanceCost + " PM" : "?",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.red } });
        }

        // Progressão de rota
        if (hasProgressResult && progressMap != null)
        {
            int maxProgress = 1;
            int minProgress = 0;
            foreach (var kv in progressMap)
            {
                if (kv.Value == int.MinValue) continue;
                if (kv.Value > maxProgress) maxProgress = kv.Value;
                if (kv.Value < minProgress) minProgress = kv.Value;
            }

            var progStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            foreach (var kv in progressMap)
            {
                Vector3Int cell = kv.Key;
                int prog = kv.Value;
                Vector3 cw = tilemap.GetCellCenterWorld(cell);

                Color col;
                string lbl;
                if (prog == int.MinValue)
                {
                    col = new Color(0.3f, 0.3f, 0.3f, 0.4f);
                    lbl = "?";
                }
                else if (prog > 0)
                {
                    float t = Mathf.Clamp01((float)prog / maxProgress);
                    col = Color.Lerp(new Color(0.3f, 0.85f, 0.3f, 0.55f), new Color(0.0f, 0.6f, 0.0f, 0.8f), t);
                    lbl = "+" + FormatRouteScore(prog);
                }
                else if (prog == 0)
                {
                    col = new Color(0.9f, 0.85f, 0.1f, 0.6f);
                    lbl = "0";
                }
                else
                {
                    float t = minProgress != 0 ? Mathf.Clamp01((float)(-prog) / (-minProgress)) : 1f;
                    col = Color.Lerp(new Color(0.85f, 0.4f, 0.1f, 0.55f), new Color(0.8f, 0.1f, 0.1f, 0.8f), t);
                    lbl = "-" + FormatRouteScore(-prog);
                }

                Handles.color = col;
                Handles.DrawSolidDisc(cw, Vector3.back, 0.22f);
                Handles.Label(cw + Vector3.up * 0.01f, lbl, progStyle);
            }

            Handles.color = new Color(1f, 0.85f, 0f, 0.95f);
            Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(lastProgressOrigin), Vector3.back, 0.30f);
            Handles.Label(tilemap.GetCellCenterWorld(lastProgressOrigin) + Vector3.up * 0.38f, "A",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.black } });

            Handles.color = new Color(0.1f, 0.8f, 1f, 0.95f);
            Vector3 dw = tilemap.GetCellCenterWorld(lastProgressDest);
            Handles.DrawSolidDisc(dw, Vector3.back, 0.30f);
            Handles.Label(dw + Vector3.up * 0.38f, "B",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.black } });
        }

        // Passando Por
        if (hasWaypointResult)
        {
            // Gradiente de custo até C (quanto menor = mais verde = mais perto do destino)
            if (waypointCostFromDest != null)
            {
                int maxCost = 1;
                foreach (var kv in waypointCostFromDest)
                    if (kv.Value > maxCost) maxCost = kv.Value;

                foreach (var kv in waypointCostFromDest)
                {
                    Vector3Int cell = kv.Key; cell.z = 0;
                    if (cell == lastWayDest || cell == lastWayOrigin || cell == lastWayWaypoint) continue;
                    float t = Mathf.Clamp01((float)kv.Value / maxCost);
                    Color c = Color.Lerp(new Color(0.1f, 0.85f, 0.1f, 0.50f), new Color(0.85f, 0.3f, 0.1f, 0.50f), t);
                    Handles.color = c;
                    Vector3 cw = tilemap.GetCellCenterWorld(cell);
                    Handles.DrawSolidDisc(cw, Vector3.back, 0.20f);
                    Handles.Label(cw + Vector3.up * 0.01f, kv.Value.ToString(), labelStyle);
                }
            }

            // Azul = células alcançáveis de A no turno atual (com ocupação)
            if (waypointReachable != null)
            {
                Handles.color = new Color(0.2f, 0.5f, 1f, 0.45f);
                foreach (Vector3Int cell in waypointReachable)
                    Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.26f);
            }

            // A = amarelo
            Handles.color = new Color(1f, 0.85f, 0f, 0.95f);
            Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(lastWayOrigin), Vector3.back, 0.30f);
            Handles.Label(tilemap.GetCellCenterWorld(lastWayOrigin) + Vector3.up * 0.38f, "A",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.black } });

            // B = magenta, mostra custo leg1+leg2
            Handles.color = new Color(1f, 0.2f, 1f, 0.95f);
            Vector3 bw = tilemap.GetCellCenterWorld(lastWayWaypoint);
            Handles.DrawSolidDisc(bw, Vector3.back, 0.30f);
            string totalStr = (waypointLeg1Cost >= 0 && waypointLeg2Cost >= 0)
                ? $"B  {waypointLeg1Cost}+{waypointLeg2Cost}={waypointLeg1Cost + waypointLeg2Cost}PM"
                : "B (?)";
            Handles.Label(bw + Vector3.up * 0.38f, totalStr,
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.magenta } });

            // C = ciano
            Handles.color = new Color(0.1f, 0.8f, 1f, 0.95f);
            Vector3 dw = tilemap.GetCellCenterWorld(lastWayDest);
            Handles.DrawSolidDisc(dw, Vector3.back, 0.30f);
            Handles.Label(dw + Vector3.up * 0.38f, "C",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.cyan } });
        }

        // Hover ao marcar
        if (pickingOrigin || pickingDestination || pickingWaypoint)
        {
            Color col = pickingOrigin ? Color.yellow : pickingWaypoint ? Color.magenta : Color.cyan;
            string pickerName = pickingOrigin ? "Origem" : pickingWaypoint ? "Via (B)" : "Destino";
            Handles.color = col;
            Vector3 hw = tilemap.GetCellCenterWorld(hoverCell);
            Handles.DrawWireDisc(hw, Vector3.back, 0.35f);
            Handles.Label(hw + Vector3.up * 0.42f,
                pickerName + " " + hoverCell,
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = col } });
        }
    }

    private void HandlePickingInput()
    {
        if (!pickingOrigin && !pickingDestination && !pickingWaypoint) return;

        Event e = Event.current;

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            hoverCell = ScreenToCell(e.mousePosition);
            HandleUtility.Repaint();
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Vector3Int picked = ScreenToCell(e.mousePosition); picked.z = 0;
            if (pickingOrigin)          { originHex = picked;      pickingOrigin = false; }
            else if (pickingDestination){ destinationHex = picked;  pickingDestination = false; }
            else if (pickingWaypoint)   { waypointHex = picked;     pickingWaypoint = false; }
            e.Use();
            Repaint();
        }

        if ((e.type == EventType.MouseDown && e.button == 1) ||
            (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
        {
            pickingOrigin = pickingDestination = pickingWaypoint = false;
            e.Use();
            Repaint();
        }

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    private Vector3Int ScreenToCell(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        float t = ray.direction.z != 0f ? -ray.origin.z / ray.direction.z : 0f;
        return tilemap.WorldToCell(ray.origin + ray.direction * t);
    }
}
