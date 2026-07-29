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
        public bool RouteFound;
        public float RouteDistance;
        public float RouteProgress;
        public float LineDeviation;
        public float Threat;
        public float Dpq;
        public float FinalScore;
        // Contribuição ponderada de cada termo no toolScore bruto (para o breakdown da janela).
        public float RawTwoTurnTerm;
        public float RawFirstTurnTerm;
        public float RawLineTerm;
        public float RawMoveTerm;
        // Rotas para destaque interativo no mapa.
        public List<Vector3Int> FirstTurnPath;   // origem → célula (turno 1)
        public Vector3Int SecondTurnStop;         // melhor parada do turno 2
        public List<Vector3Int> SecondTurnPath;   // célula → melhor parada (turno 2)
        public bool HasSecondTurn;
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
    [SerializeField] private bool apcEmptyIgnoresThreat;

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
    // Inspeção de gasto: clique numa célula alcançável para ver o PM passo a passo.
    private Dictionary<Vector3Int, List<Vector3Int>> reachablePaths;
    private Dictionary<Vector3Int, int> reachableCostMap;
    private Vector3Int selectedReachableCell;
    private bool hasSelectedReachableCell;
    private List<string> reachableStepLines;
    private string reachableSummary = string.Empty;
    private int reachableBudget;

    // Distância até Destino
    private int distanceCost = -1;
    private Vector3Int lastDest;
    private bool hasDistResult;
    private Dictionary<Vector3Int, int> distanceCostMap;

    // Progressão de Rota
    private Dictionary<Vector3Int, int> progressMap;
    private Dictionary<Vector3Int, ProgressDebugCandidate> progressCandidateMap;
    private List<KeyValuePair<Vector3Int, ProgressDebugCandidate>> progressRanking;
    private Vector2 routesScroll;
    private Vector3Int selectedProgressCell;
    private bool hasSelectedProgressCell;
    private const float LeftColumnWidth = 380f;
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
        EditorGUILayout.BeginHorizontal();
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(LeftColumnWidth));

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
        {
            EditorGUILayout.LabelField($"{reachableCells.Count} hexes alcançáveis de {lastOrigin} com PM={movementPoints}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("  Clique numa bolinha verde na Scene View para ver o gasto de PM até ela.", EditorStyles.miniLabel);
        }

        if (hasSelectedReachableCell)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Gasto de PM", EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(reachableSummary))
                EditorGUILayout.HelpBox(reachableSummary, MessageType.None);
            if (reachableStepLines != null)
            {
                for (int i = 0; i < reachableStepLines.Count; i++)
                    EditorGUILayout.LabelField("  " + reachableStepLines[i], EditorStyles.miniLabel);
            }
            if (GUILayout.Button("Limpar seleção do gasto", EditorStyles.miniButton))
            {
                hasSelectedReachableCell = false;
                reachableStepLines = null;
                reachableSummary = string.Empty;
                SceneView.RepaintAll();
            }
        }

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
        apcEmptyIgnoresThreat = EditorGUILayout.Toggle("APC vazio (ignora prudência)", apcEmptyIgnoresThreat);
        EditorGUILayout.LabelField("  Zera o termo threat (APC vazio que vira assalto). Logística mantém prudência.", EditorStyles.miniLabel);

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

        // ── Coluna direita: rotas interativas ──
        routesScroll = EditorGUILayout.BeginScrollView(routesScroll);
        DrawProgressRoutesColumn();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawProgressRoutesColumn()
    {
        EditorGUILayout.LabelField("Rotas Calculadas", EditorStyles.boldLabel);

        if (!hasProgressResult || progressRanking == null || progressRanking.Count == 0)
        {
            EditorGUILayout.HelpBox("Calcule a Progressão (coluna à esquerda) para listar as rotas de cada célula alcançável.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Clique numa rota para destacá-la no mapa:", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Verde = turno 1   •   Azul = turno 2 provável", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("RAW = toolScore bruto (×10/×2/…)   •   FIN = fórmula do intent (×1000/×220/…)", EditorStyles.miniLabel);
        EditorGUILayout.Space(2f);

        if (hasSelectedProgressCell && GUILayout.Button("Limpar seleção", EditorStyles.miniButton))
        {
            hasSelectedProgressCell = false;
            SceneView.RepaintAll();
        }

        int limit = Mathf.Min(40, progressRanking.Count);
        for (int i = 0; i < limit; i++)
        {
            KeyValuePair<Vector3Int, ProgressDebugCandidate> kv = progressRanking[i];
            Vector3Int cell = kv.Key;
            ProgressDebugCandidate cand = kv.Value;
            bool selected = hasSelectedProgressCell && selectedProgressCell == cell;

            string turn2 = cand.HasSecondTurn ? $" → ({cand.SecondTurnStop.x},{cand.SecondTurnStop.y})" : "";
            string rowLabel = $"#{i + 1}  ({cell.x},{cell.y}){turn2}   nota {FormatCandidateNota(cand)}   tool {cand.ToolScore}";

            GUI.backgroundColor = selected ? new Color(0.4f, 1f, 0.45f) : Color.white;
            if (GUILayout.Button(rowLabel, EditorStyles.miniButton))
            {
                if (selected)
                {
                    hasSelectedProgressCell = false;
                }
                else
                {
                    selectedProgressCell = cell;
                    hasSelectedProgressCell = true;
                }
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (selected)
                EditorGUILayout.HelpBox(BuildBreakdownLine(cell, cand), MessageType.None);
        }

        if (progressRanking.Count > limit)
            EditorGUILayout.LabelField($"  … +{progressRanking.Count - limit} células omitidas.", EditorStyles.miniLabel);
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
        progressRanking = null;
        hasSelectedProgressCell = false;
        hasWaypointResult = false;
        statusMessage = string.Empty;

        ClearReachableInspection();

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
            // Guarda o caminho de cada célula e o custo acumulado para a inspeção
            // por clique. Nada aqui muda a regra: sao os mesmos resultados que a
            // ferramenta ja calculava e descartava.
            reachablePaths = new Dictionary<Vector3Int, List<Vector3Int>>(paths.Count);
            foreach (var kv in paths)
                reachablePaths[kv.Key] = kv.Value != null ? new List<Vector3Int>(kv.Value) : null;
            reachableCostMap = UnitMovementPathRules.CalculateMovementCostMap(
                tilemap, unit, origin, movementPoints, terrainDatabase);
            reachableBudget = movementPoints;
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
        progressRanking = null;
        hasSelectedProgressCell = false;
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
        progressRanking = null;
        hasSelectedProgressCell = false;
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
            progressOriginCost = Mathf.CeilToInt(RouteDistanceOrHexDebug(unit, origin, dest));
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
                    rawScore = CalculateReachableProgressScore(unit, origin, dest, cell, paths[cell]);
                    candidate = BuildOneTurnProgressCandidate(unit, origin, dest, cell, paths[cell], costMap);
                }
                else
                {
                    rawScore = CalculateTwoTurnProgressScore(unit, origin, dest, cell, paths[cell], costMap, out candidate);
                }

                candidate.FinalScore = progressIntent == ProgressDebugIntent.Bruto
                    ? rawScore
                    : ScoreProgressIntent(progressIntent, candidate, apcEmptyIgnoresThreat ? 0f : 1f);

                progressCandidateMap[cell] = candidate;
                progressMap[cell] = progressIntent == ProgressDebugIntent.Bruto
                    ? rawScore
                    : Mathf.RoundToInt(candidate.FinalScore / 1000f);
            }

            hasProgressResult = true;
        });

        BuildProgressRanking();

        SceneView.RepaintAll();
        Repaint();
    }

    private void BuildProgressRanking()
    {
        progressRanking = null;
        if (progressCandidateMap == null || progressCandidateMap.Count == 0)
            return;

        var list = new List<KeyValuePair<Vector3Int, ProgressDebugCandidate>>(progressCandidateMap);
        list.Sort((a, b) => b.Value.FinalScore.CompareTo(a.Value.FinalScore));
        progressRanking = list;
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
        progressRanking = null;
        hasSelectedProgressCell = false;
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

    private void ClearReachableInspection()
    {
        reachablePaths = null;
        reachableCostMap = null;
        hasSelectedReachableCell = false;
        reachableStepLines = null;
        reachableSummary = string.Empty;
    }

    // Monta o extrato de PM da célula clicada, passo a passo, como o jogo cobraria.
    // Só leitura: os custos vêm do mesmo mapa que o cálculo já produziu.
    private void BuildReachableBreakdown(Vector3Int cell)
    {
        reachableStepLines = new List<string>();
        reachableSummary = string.Empty;
        if (reachablePaths == null || !reachablePaths.TryGetValue(cell, out List<Vector3Int> path) || path == null)
        {
            reachableSummary = "Sem caminho registrado para esta célula.";
            return;
        }

        int total = reachableCostMap != null && reachableCostMap.TryGetValue(cell, out int t) ? t : -1;

        int previous = 0;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3Int step = path[i]; step.z = 0;
            int cumulative = reachableCostMap != null && reachableCostMap.TryGetValue(step, out int c) ? c : -1;
            string delta = cumulative >= 0 ? (cumulative - previous).ToString("+0;-0;0") : "?";
            string running = cumulative >= 0 ? $"{cumulative}/{reachableBudget}" : "?";
            reachableStepLines.Add(
                $"{i}. ({step.x},{step.y})  {delta} PM  →  {running}   {DescribeCellSurface(step)}");
            if (cumulative >= 0)
                previous = cumulative;
        }

        bool roadBonus = false;
        RunWithTempUnit(lastOrigin, unit =>
        {
            roadBonus = UnitMovementPathRules.DidUseRoadFullMoveBonus(tilemap, unit, path, terrainDatabase);
        });

        reachableSummary =
            $"({cell.x},{cell.y})  total {(total >= 0 ? total.ToString() : "?")} de {reachableBudget} PM  |  " +
            $"{Mathf.Max(0, path.Count - 1)} passo(s)  |  bônus de estrada: {(roadBonus ? "SIM (passo grátis)" : "não")}";
    }

    // Só apresentação: mesma precedência que o motor usa para nomear o hex.
    private string DescribeCellSurface(Vector3Int cell)
    {
        if (tilemap == null)
            return string.Empty;

        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell);
        if (construction != null && construction.TryResolveConstructionData(out ConstructionData cd) && cd != null)
            return $"[{cd.displayName}]";

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(tilemap, cell);
        TerrainTypeData terrain = ResolveTerrainForDisplay(cell);
        if (structure != null)
            return $"[{structure.displayName}{(terrain != null ? " / " + terrain.displayName : string.Empty)}]";

        return terrain != null ? $"[{terrain.displayName}]" : "[?]";
    }

    private TerrainTypeData ResolveTerrainForDisplay(Vector3Int cell)
    {
        if (tilemap == null || terrainDatabase == null)
            return null;

        TileBase tile = tilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data))
            return data;

        GridLayout grid = tilemap.layoutGrid;
        if (grid == null)
            return null;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == null || maps[i] == tilemap)
                continue;
            TileBase other = maps[i].GetTile(cell);
            if (other != null && terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData otherData))
                return otherData;
        }

        return null;
    }

    private void ClearAll()
    {
        ClearReachableInspection();
        reachableCells = null;
        hasDistResult = false;
        distanceCostMap = null;
        hasProgressResult = false;
        progressMap = null;
        progressCandidateMap = null;
        progressRanking = null;
        hasSelectedProgressCell = false;
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
        float originDistance = RouteDistanceOrHexDebug(tempUnit, origin, dest);
        float bestDistanceAfterNextMove = RouteDistanceOrHexDebug(tempUnit, firstStop, dest);
        // Real MP cost (road-bonus aware); fall back to path waypoint count if costMap absent.
        int firstMoveCost = costMap != null && costMap.TryGetValue(firstStop, out int c)
            ? c
            : (firstPath != null ? Mathf.Max(0, firstPath.Count - 1) : 0);

        Vector3Int bestNextStop = firstStop;
        List<Vector3Int> bestNextPath = null;
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

                float nextDistance = RouteDistanceOrHexDebug(tempUnit, nextStop, dest);
                if (nextDistance < bestDistanceAfterNextMove)
                {
                    bestDistanceAfterNextMove = nextDistance;
                    bestNextStop = nextStop; bestNextStop.z = 0;
                    bestNextPath = nextPaths.TryGetValue(nextStop, out List<Vector3Int> np) ? np : null;
                }
            }
        }
        finally
        {
            tempUnit.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);
        }

        float twoTurnProgress = originDistance - bestDistanceAfterNextMove;
        // Tiebreaker: how close firstStop itself is to dest — credits road cells that cost the
        // same MP as a shorter non-road path but land closer to the objective by real route cost.
        float firstTurnProgress = originDistance - RouteDistanceOrHexDebug(tempUnit, firstStop, dest);
        float lineDeviation = DistanceFromHexLine(firstStop, origin, dest);

        // Progressão = aproximar do alvo. 1T (avanço já no turno 1) é termo principal;
        // 2T mantém a viabilidade de chegar em 2 turnos; line entra suave; mv NÃO penaliza
        // (gastar movimento avançando é o objetivo, não defeito).
        float score =
            twoTurnProgress * 10f
            + firstTurnProgress * 6f
            - lineDeviation * 1f;

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
        candidate.RawTwoTurnTerm = twoTurnProgress * 10f;
        candidate.RawFirstTurnTerm = firstTurnProgress * 6f;
        candidate.RawLineTerm = -lineDeviation * 1f;
        candidate.RawMoveTerm = 0f;
        candidate.SecondTurnStop = bestNextStop;
        candidate.SecondTurnPath = bestNextPath;
        candidate.HasSecondTurn = bestNextPath != null && bestNextStop != firstStop;

        return Mathf.RoundToInt(score);
    }

    private static int CalculateReachableProgressScore(
        UnitManager tempUnit,
        Vector3Int origin,
        Vector3Int dest,
        Vector3Int cell,
        IReadOnlyList<Vector3Int> validPath)
    {
        float originDistance = RouteDistanceOrHexDebug(tempUnit, origin, dest);
        float cellDistance = RouteDistanceOrHexDebug(tempUnit, cell, dest);
        float hexProgress = originDistance - cellDistance;
        float lineDeviation = DistanceFromHexLine(cell, origin, dest);

        float score =
            hexProgress * 10f
            - lineDeviation * 1f;

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
        float originDistance = RouteDistanceOrHexDebug(tempUnit, origin, dest);
        float firstTurnProgress = originDistance - RouteDistanceOrHexDebug(tempUnit, cell, dest);
        float lineDeviation = DistanceFromHexLine(cell, origin, dest);
        int moveCost = costMap != null && costMap.TryGetValue(cell, out int c)
            ? c
            : (path != null ? Mathf.Max(0, path.Count - 1) : 0);
        float rawScore = firstTurnProgress * 10f - lineDeviation * 1f;

        ProgressDebugCandidate candidate = BuildProgressCandidate(
            tempUnit,
            origin,
            dest,
            cell,
            path,
            Mathf.RoundToInt(rawScore),
            RouteDistanceOrHexDebug(tempUnit, cell, dest),
            moveCost,
            firstTurnProgress,
            firstTurnProgress,
            lineDeviation);
        candidate.RawTwoTurnTerm = 0f;
        candidate.RawFirstTurnTerm = firstTurnProgress * 10f;
        candidate.RawLineTerm = -lineDeviation * 1f;
        candidate.RawMoveTerm = 0f;
        return candidate;
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

        // Route-aware progress (mirrors AIController.ProgressionSelector): origin→dest minus cell→dest,
        // measured by the unit's real movement template (HexDistance only for air / no route).
        bool originRouteFound = TryCalculateRouteDistanceDebug(tempUnit, origin, dest, out float originRouteDistance);
        bool cellRouteFound = TryCalculateRouteDistanceDebug(tempUnit, cell, dest, out float cellRouteDistance);
        float routeProgress = originRouteFound && cellRouteFound ? originRouteDistance - cellRouteDistance : 0f;

        return new ProgressDebugCandidate
        {
            ToolScore = toolScore,
            NextDistance = nextDistance,
            MoveCost = moveCost,
            RoadBonus = roadBonus,
            FirstTurnProgress = firstTurnProgress,
            TwoTurnProgress = twoTurnProgress,
            RouteFound = cellRouteFound,
            RouteDistance = cellRouteFound ? cellRouteDistance : float.MaxValue,
            RouteProgress = routeProgress,
            LineDeviation = lineDeviation,
            Threat = CalculateDebugThreatLevel(cell, ActiveTeam()),
            Dpq = GetDebugTerrainDpqPontos(cell),
            FirstTurnPath = path != null ? new List<Vector3Int>(path) : null
        };
    }

    // Mirror of AIController.TryCalculateRouteDistance / CalculateRouteDistanceOrHex.
    // Keeps the tool's progression score on the same metric the runtime AI uses:
    // real movement cost via the unit's UnitData template, HexDistance only for air / no route.
    private static bool TryCalculateRouteDistanceDebug(
        UnitManager unit, Vector3Int fromCell, Vector3Int targetCell, out float distance)
    {
        distance = 0f;
        fromCell.z = 0;
        targetCell.z = 0;

        if (unit != null && unit.GetDomain() == Domain.Air)
        {
            distance = SectorManager.HexDistance(fromCell, targetCell);
            return true;
        }

        if (unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && SectorManager.TryGetLandMovementDistance(fromCell, targetCell, unitData, out int unitCost))
        {
            distance = unitCost;
            return true;
        }

        if (SectorManager.TryGetLandMovementDistance(fromCell, targetCell, out int fallbackCost))
        {
            distance = fallbackCost;
            return true;
        }

        return false;
    }

    private static float RouteDistanceOrHexDebug(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell)
    {
        return TryCalculateRouteDistanceDebug(unit, fromCell, targetCell, out float routeDistance)
            ? routeDistance
            : SectorManager.HexDistance(fromCell, targetCell);
    }

    private static float ScoreProgressIntent(ProgressDebugIntent intent, ProgressDebugCandidate candidate, float threatScale)
        => ScoreProgressIntent(intent, candidate, threatScale, out _);

    private static float ScoreProgressIntent(ProgressDebugIntent intent, ProgressDebugCandidate candidate, float threatScale, out string breakdown)
    {
        // base = toolScore (já contém 2T/1T/line) amplificado. Os termos de progresso NÃO são
        // re-somados aqui (eram contagem dupla); mv foi removido. Só road/dpq/threat/route entram à parte.
        float baseTerm = candidate.ToolScore * 1000f;
        float roadTerm = candidate.RoadBonus ? 650f : 0f;

        float dpqWeight = 0f;
        float threatWeight = 0f;
        float routeProgTerm = 0f;
        float routeDistTerm = 0f;
        switch (intent)
        {
            case ProgressDebugIntent.FireSupportReposition:
            case ProgressDebugIntent.FireSupportRendezvous:
                dpqWeight = 35f; threatWeight = 80f;
                break;
            case ProgressDebugIntent.LogisticsService:
            case ProgressDebugIntent.LogisticsReload:
                dpqWeight = 30f; threatWeight = 120f;
                break;
            case ProgressDebugIntent.RepairReturn:
                threatWeight = 60f;
                break;
            case ProgressDebugIntent.TransportDelivery:
            case ProgressDebugIntent.TransportRendezvous:
                threatWeight = 50f;
                break;
            case ProgressDebugIntent.ObserverSpot:
                dpqWeight = 45f; threatWeight = 90f;
                break;
            case ProgressDebugIntent.AssaultPressure:
                if (candidate.RouteFound)
                {
                    routeProgTerm = candidate.RouteProgress * 2500f;
                    routeDistTerm = -candidate.RouteDistance * 80f;
                }
                dpqWeight = 20f; threatWeight = 35f;
                break;
            case ProgressDebugIntent.CaptureAdvance:
            default:
                dpqWeight = 20f; threatWeight = 35f;
                break;
        }

        float dpqTerm = candidate.Dpq * dpqWeight;
        float threatTerm = -candidate.Threat * threatWeight * threatScale;
        float score = baseTerm + roadTerm + dpqTerm + threatTerm + routeProgTerm + routeDistTerm;

        string assaultPart = intent == ProgressDebugIntent.AssaultPressure && candidate.RouteFound
            ? $" routeP {Sg(routeProgTerm)} routeD {Sg(routeDistTerm)}"
            : "";
        breakdown =
            $"FIN[base {baseTerm:F0} road {Sg(roadTerm)} dpq {Sg(dpqTerm)} " +
            $"threat {Sg(threatTerm)}{assaultPart} = {score:F0}]";

        return score;
    }

    private static string Sg(float value) => value.ToString("+0.#;-0.#;0");

    private string FormatCandidateNota(ProgressDebugCandidate c)
    {
        int mapVal = progressIntent == ProgressDebugIntent.Bruto
            ? Mathf.RoundToInt(c.FinalScore)
            : Mathf.RoundToInt(c.FinalScore / 1000f);
        return (mapVal >= 0 ? "+" : "") + FormatRouteScore(mapVal);
    }

    private string BuildBreakdownLine(Vector3Int cell, ProgressDebugCandidate c)
    {
        string disp = FormatCandidateNota(c);
        string raw =
            $"RAW[2T {Sg(c.RawTwoTurnTerm)} 1T {Sg(c.RawFirstTurnTerm)} line {Sg(c.RawLineTerm)} " +
            $"= tool {c.ToolScore}]  next={c.NextDistance:F1} moveCost={c.MoveCost} road={c.RoadBonus}";

        if (progressIntent == ProgressDebugIntent.Bruto)
            return $"({cell.x},{cell.y}) {disp}   {raw}";

        ScoreProgressIntent(progressIntent, c, apcEmptyIgnoresThreat ? 0f : 1f, out string fin);
        return $"({cell.x},{cell.y}) {disp}   {raw}   {fin}";
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
        HandleReachableClickInput();

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

            // Célula clicada: caminho destacado + PM acumulado em cada passo.
            if (hasSelectedReachableCell
                && reachablePaths != null
                && reachablePaths.TryGetValue(selectedReachableCell, out List<Vector3Int> selectedPath))
            {
                DrawPathHighlight(selectedPath, new Color(1f, 0.55f, 0.1f, 0.95f), 0.18f, 6f);

                var stepStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    normal = { textColor = Color.white }
                };

                if (selectedPath != null)
                {
                    for (int i = 1; i < selectedPath.Count; i++)
                    {
                        Vector3Int step = selectedPath[i]; step.z = 0;
                        int cumulative = reachableCostMap != null && reachableCostMap.TryGetValue(step, out int c) ? c : -1;
                        Handles.Label(
                            tilemap.GetCellCenterWorld(step) + Vector3.up * 0.02f,
                            cumulative >= 0 ? cumulative.ToString() : "?",
                            stepStyle);
                    }
                }

                Vector3 sel = tilemap.GetCellCenterWorld(selectedReachableCell);
                Handles.color = new Color(1f, 0.55f, 0.1f, 0.95f);
                Handles.DrawWireDisc(sel, Vector3.back, 0.36f);
                Handles.Label(sel + Vector3.up * 0.42f, reachableSummary,
                    new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(1f, 0.6f, 0.15f) } });
            }
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

            // Rota selecionada: verde = turno 1, azul = turno 2 provável.
            if (hasSelectedProgressCell
                && progressCandidateMap != null
                && progressCandidateMap.TryGetValue(selectedProgressCell, out ProgressDebugCandidate selected))
            {
                DrawSelectedRouteHighlight(selected);
            }
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

    private void DrawSelectedRouteHighlight(ProgressDebugCandidate candidate)
    {
        // Turno 2 primeiro (azul, por baixo), depois turno 1 (verde, por cima).
        if (candidate.HasSecondTurn)
        {
            DrawPathHighlight(candidate.SecondTurnPath, new Color(0.15f, 0.55f, 1f, 0.95f), 0.16f, 5f);
            Vector3 sw = tilemap.GetCellCenterWorld(candidate.SecondTurnStop);
            Handles.color = new Color(0.15f, 0.55f, 1f, 0.95f);
            Handles.DrawWireDisc(sw, Vector3.back, 0.34f);
            Handles.Label(sw + Vector3.up * 0.40f, "2",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.1f, 0.4f, 1f) } });
        }

        DrawPathHighlight(candidate.FirstTurnPath, new Color(0.2f, 0.95f, 0.25f, 0.95f), 0.2f, 6f);
        Vector3 fw = tilemap.GetCellCenterWorld(selectedProgressCell);
        Handles.color = new Color(0.2f, 0.95f, 0.25f, 0.95f);
        Handles.DrawWireDisc(fw, Vector3.back, 0.36f);
        Handles.Label(fw + Vector3.up * 0.40f, "1",
            new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.1f, 0.6f, 0.1f) } });
    }

    private void DrawPathHighlight(IReadOnlyList<Vector3Int> path, Color color, float discRadius, float lineWidth)
    {
        if (path == null || path.Count == 0 || tilemap == null)
            return;

        var points = new Vector3[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            Vector3Int cell = path[i]; cell.z = 0;
            points[i] = tilemap.GetCellCenterWorld(cell);
        }

        Handles.color = color;
        if (points.Length >= 2)
            Handles.DrawAAPolyLine(lineWidth, points);
        foreach (Vector3 p in points)
            Handles.DrawSolidDisc(p, Vector3.back, discRadius);
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

    // Clique numa bolinha verde = inspecionar o gasto até ela. O clique só é
    // consumido quando cai numa célula alcançavel, para não roubar a seleção
    // normal da Scene View no resto do mapa.
    private void HandleReachableClickInput()
    {
        if (reachableCells == null || reachableCells.Count == 0)
            return;
        if (pickingOrigin || pickingDestination || pickingWaypoint)
            return;

        Event e = Event.current;

        if (e.type == EventType.Layout)
        {
            Vector3Int over = ScreenToCell(e.mousePosition); over.z = 0;
            if (reachableCells.Contains(over))
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            return;
        }

        if (e.type != EventType.MouseDown || e.button != 0 || e.alt)
            return;

        Vector3Int cell = ScreenToCell(e.mousePosition); cell.z = 0;
        if (!reachableCells.Contains(cell))
            return;

        if (hasSelectedReachableCell && selectedReachableCell == cell)
        {
            hasSelectedReachableCell = false;
            reachableStepLines = null;
            reachableSummary = string.Empty;
        }
        else
        {
            selectedReachableCell = cell;
            hasSelectedReachableCell = true;
            BuildReachableBreakdown(cell);
        }

        e.Use();
        Repaint();
        SceneView.RepaintAll();
    }

    private Vector3Int ScreenToCell(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        float t = ray.direction.z != 0f ? -ray.origin.z / ray.direction.z : 0f;
        return tilemap.WorldToCell(ray.origin + ray.direction * t);
    }
}
