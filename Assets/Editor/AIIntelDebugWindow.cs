using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AIIntelDebugWindow : EditorWindow
{
    // ── inner types ────────────────────────────────────────────────────────────
    private sealed class ConstructionIntel
    {
        public ConstructionManager construction;
        public int distanceFromNearestAlly = -1;
    }

    private sealed class SectorIntel
    {
        public ConstructionSector sector;
        public readonly List<ConstructionIntel> entries = new List<ConstructionIntel>();
        public ConstructionIntel representative;
        public int sectorDistance = -1;
        public SectorManager.SectorInfo sectorInfo;
    }

    // ── sector colors (mesma ordem ordinal do enum: Alpha=0 … Tango=18, BaseTeam=19) ──
    private static readonly Color[] SectorColors = new Color[]
    {
        new Color(0.30f, 0.60f, 1.00f), // Alpha
        new Color(0.20f, 0.75f, 0.35f), // Bravo
        new Color(1.00f, 0.55f, 0.10f), // Charlie
        new Color(0.85f, 0.20f, 0.20f), // Delta
        new Color(0.75f, 0.35f, 0.90f), // Echo
        new Color(0.15f, 0.80f, 0.85f), // Foxtrot
        new Color(1.00f, 0.85f, 0.10f), // Golf
        new Color(0.95f, 0.40f, 0.65f), // Hotel
        new Color(0.48f, 0.48f, 0.95f), // India
        new Color(0.40f, 0.85f, 0.55f), // Juliet
        new Color(0.85f, 0.65f, 0.25f), // Kilo
        new Color(0.65f, 0.30f, 0.80f), // Lima
        new Color(0.25f, 0.70f, 0.95f), // Mike
        new Color(0.90f, 0.45f, 0.35f), // November
        new Color(0.55f, 0.70f, 0.25f), // Oscar
        new Color(0.95f, 0.70f, 0.45f), // Papa
        new Color(0.30f, 0.60f, 0.55f), // Quebec
        new Color(0.85f, 0.35f, 0.50f), // Romeo
        new Color(0.70f, 0.70f, 0.70f), // Tango
        new Color(0.95f, 0.95f, 0.95f), // BaseTeam (índice 19)
    };

    private static Color GetSectorColor(ConstructionSector sector)
    {
        int idx = sector == ConstructionSector.BaseTeam ? 19 : (int)sector;
        return idx >= 0 && idx < SectorColors.Length ? SectorColors[idx] : Color.white;
    }

    // ── context ────────────────────────────────────────────────────────────────
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private AIPlanDatabase planDatabase;
    [SerializeField] private bool useGameplaySensorContext = true;
    [SerializeField] private bool logToConsole = true;

    // ── foldouts ───────────────────────────────────────────────────────────────
    private bool foldActivePlayer = true;
    private bool foldAlliedUnits  = true;
    private bool foldEnemyUnits   = true;
    private bool foldBaseTeam     = true;
    private bool foldAllConstr    = true;
    private bool foldSectors      = true;
    private bool foldPlans        = true;

    // ── results ────────────────────────────────────────────────────────────────
    private readonly List<UnitManager>       alliedUnits       = new List<UnitManager>();
    private readonly List<UnitManager>       visibleEnemyUnits = new List<UnitManager>();
    private readonly List<ConstructionIntel> baseTeamConstr    = new List<ConstructionIntel>(); // sector == BaseTeam, sorted by TeamId
    private readonly List<ConstructionIntel> allSortedConstr   = new List<ConstructionIntel>(); // todos exceto BaseTeam, sorted by sector+dist
    private readonly List<SectorIntel>       sectorIntel       = new List<SectorIntel>();       // exceto BaseTeam

    // ── sector scene overlay ───────────────────────────────────────────────────
    private readonly HashSet<ConstructionSector> highlightedSectors = new HashSet<ConstructionSector>();
    private const float SectorCircleRadius = 1.5f;

    private string statusMessage     = "Pronto.";
    private string activePlayerBlock = string.Empty;
    private Vector2 scroll;

    // ── menu ───────────────────────────────────────────────────────────────────
    [MenuItem("Tools/AI/AI Intel")]
    public static void OpenWindow() => GetWindow<AIIntelDebugWindow>("AI Intel");

    // ── lifecycle ──────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        highlightedSectors.Clear();
        SceneView.RepaintAll();
    }

    // ── scene overlay ──────────────────────────────────────────────────────────
    private void OnSceneGUI(SceneView sceneView)
    {
        if (highlightedSectors.Count == 0) return;

        for (int s = 0; s < sectorIntel.Count; s++)
        {
            SectorIntel si = sectorIntel[s];
            if (!highlightedSectors.Contains(si.sector)) continue;

            Color c = GetSectorColor(si.sector);
            Handles.color = new Color(c.r, c.g, c.b, 0.85f);

            for (int i = 0; i < si.entries.Count; i++)
            {
                ConstructionManager cm = si.entries[i]?.construction;
                if (cm == null) continue;
                Vector3 pos = cm.transform.position;
                pos.z = 0f;
                Handles.DrawWireDisc(pos, Vector3.forward, SectorCircleRadius);
            }
        }

        // Plan SceneGUI: linhas de unidades designadas até centroide do setor
        if (Application.isPlaying)
        {
            AIPlayerController aiController = Object.FindAnyObjectByType<AIPlayerController>();
            IReadOnlyList<AIPlanIntent> activePlans = aiController?.CurrentTurnPlans;
            if (activePlans != null)
            {
                for (int p = 0; p < activePlans.Count; p++)
                {
                    AIPlanIntent intent = activePlans[p];
                    if (intent?.Assignments == null || intent.Assignments.Count == 0) continue;

                    Color planColor = GetSectorColor(intent.Sector);
                    Vector3 targetPos = intent.HasCaptureTarget
                        ? new Vector3(intent.CaptureTargetCell.x + 0.5f, intent.CaptureTargetCell.y + 0.5f, 0f)
                        : ComputeSectorCentroidWorld(intent.Sector);

                    Handles.color = new Color(planColor.r, planColor.g, planColor.b, 0.7f);
                    for (int a = 0; a < intent.Assignments.Count; a++)
                    {
                        UnitManager u = FindUnitByInstanceId(intent.Assignments[a].UnitInstanceId);
                        if (u == null) continue;
                        Vector3 from = u.transform.position;
                        from.z = 0f;
                        Handles.DrawLine(from, targetPos);
                    }
                }
            }
        }

        // BaseTeam highlight separado (se marcado)
        if (highlightedSectors.Contains(ConstructionSector.BaseTeam))
        {
            Color c = GetSectorColor(ConstructionSector.BaseTeam);
            Handles.color = new Color(c.r, c.g, c.b, 0.85f);
            for (int i = 0; i < baseTeamConstr.Count; i++)
            {
                ConstructionManager cm = baseTeamConstr[i]?.construction;
                if (cm == null) continue;
                Vector3 pos = cm.transform.position;
                pos.z = 0f;
                Handles.DrawWireDisc(pos, Vector3.forward, SectorCircleRadius);
            }
        }
    }

    // ── GUI ────────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("AI Intel", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Intel do jogador da vez. Todas as construções são conhecidas (posição, dono, captura, setor, distância). " +
            "Unidades inimigas só aparecem se visíveis pelo FoW. Ocupantes de construções são desconhecidos.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        DrawContextFields();

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect")) AutoDetectContext();
        if (GUILayout.Button("Simular"))      RunSimulation();
        if (GUILayout.Button("Refresh FOW"))  TryRefreshFow();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        if (matchController == null)
        {
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space(6f);
        DrawActivePlayerSection();

        EditorGUILayout.Space(6f);
        DrawUnitSection("Unidades Aliadas", alliedUnits, ref foldAlliedUnits, Color.green);

        EditorGUILayout.Space(6f);
        DrawUnitSection("Unidades Inimigas Visíveis", visibleEnemyUnits, ref foldEnemyUnits, Color.red);

        EditorGUILayout.Space(6f);
        DrawBaseTeamSection();

        EditorGUILayout.Space(6f);
        DrawAllConstructionsSection();

        EditorGUILayout.Space(6f);
        DrawSectorsSection();

        EditorGUILayout.Space(6f);
        DrawPlansSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawContextFields()
    {
        matchController    = (MatchController)EditorGUILayout.ObjectField("MatchController",   matchController,    typeof(MatchController),    true);
        turnStateManager   = (TurnStateManager)EditorGUILayout.ObjectField("TurnStateManager", turnStateManager,   typeof(TurnStateManager),   true);
        overrideTilemap    = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)",         overrideTilemap,    typeof(Tilemap),             true);
        terrainDatabase    = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database",   terrainDatabase,    typeof(TerrainDatabase),    false);
        dpqAirHeightConfig = (DPQAirHeightConfig)EditorGUILayout.ObjectField("DPQ Air Height", dpqAirHeightConfig, typeof(DPQAirHeightConfig), false);
        planDatabase       = (AIPlanDatabase)EditorGUILayout.ObjectField("Plan Database",       planDatabase,       typeof(AIPlanDatabase),     false);
        useGameplaySensorContext = EditorGUILayout.ToggleLeft("Usar contexto do gameplay (MatchController)", useGameplaySensorContext);
        logToConsole             = EditorGUILayout.ToggleLeft("Log no Console", logToConsole);
    }

    // ── simulation ─────────────────────────────────────────────────────────────
    private void RunSimulation()
    {
        alliedUnits.Clear();
        visibleEnemyUnits.Clear();
        baseTeamConstr.Clear();
        allSortedConstr.Clear();
        sectorIntel.Clear();
        activePlayerBlock = string.Empty;

        if (matchController == null) { statusMessage = "MatchController não encontrado."; return; }

        TeamId activeTeam = matchController.ActiveTeam;
        bool totalWar  = !useGameplaySensorContext || matchController.EnableTotalWar;
        bool losOn     = !useGameplaySensorContext || matchController.EnableLosValidation;
        bool spotterOn = !useGameplaySensorContext || matchController.EnableSpotter;
        bool stealthOn = !useGameplaySensorContext || matchController.EnableStealthValidation;

        activePlayerBlock = BuildActivePlayerText(activeTeam, totalWar, losOn, spotterOn, stealthOn);

        // ── unidades ───────────────────────────────────────────────────────────
        IReadOnlyList<UnitManager> units = GetAllUnits();
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager u = units[i];
            if (u == null || !u.gameObject.activeInHierarchy || u.IsEmbarked) continue;
            if (u.TeamId == activeTeam)
                alliedUnits.Add(u);
            else if (IsUnitVisibleForTeam(u, activeTeam, totalWar, losOn, spotterOn, stealthOn))
                visibleEnemyUnits.Add(u);
        }

        // ── construções: TODAS são conhecidas ──────────────────────────────────
        Tilemap map = ResolveBoardTilemap();
        IReadOnlyList<ConstructionManager> constructions = GetAllConstructions();
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager c = constructions[i];
            if (c == null || !c.gameObject.activeInHierarchy) continue;

            ConstructionIntel intel = new ConstructionIntel
            {
                construction            = c,
                distanceFromNearestAlly = ComputeNearestAllyDistance(c.CurrentCellPosition, alliedUnits, map)
            };

            if (c.Sector == ConstructionSector.BaseTeam)
                baseTeamConstr.Add(intel);
            else
                allSortedConstr.Add(intel);
        }

        // Base Team: ordena por TeamId
        baseTeamConstr.Sort((a, b) =>
            ((int)a.construction.TeamId).CompareTo((int)b.construction.TeamId));

        // Todas as Construções: ordena por setor (ordinal) depois por distância
        allSortedConstr.Sort((a, b) =>
        {
            int cmpSector = ((int)a.construction.Sector).CompareTo((int)b.construction.Sector);
            if (cmpSector != 0) return cmpSector;
            return a.distanceFromNearestAlly.CompareTo(b.distanceFromNearestAlly);
        });

        // ── setores (exceto BaseTeam) ──────────────────────────────────────────
        BuildSectorIntel(map);

        statusMessage =
            $"Time: {TeamUtils.GetName(activeTeam)} | Turno {matchController.CurrentTurn} | " +
            $"Aliados={alliedUnits.Count} InimVis={visibleEnemyUnits.Count} | " +
            $"BaseTeam={baseTeamConstr.Count} OutrasConstr={allSortedConstr.Count} Setores={sectorIntel.Count}";

        if (logToConsole)
        {
            Debug.Log($"[AIIntel] {statusMessage}");
            LogUnitList("ALIADOS",          alliedUnits);
            LogUnitList("INIMIGOS_VISIVEIS", visibleEnemyUnits);
            LogConstructionList("BASE_TEAM",  baseTeamConstr);
            LogConstructionList("CONSTRUCOES", allSortedConstr);
            LogSectors();
        }
    }

    // ── sector builder ─────────────────────────────────────────────────────────
    private void BuildSectorIntel(Tilemap map)
    {
        sectorIntel.Clear();

        IReadOnlyList<SectorManager.SectorInfo> sectors = SectorManager.GetAllSectorInfos();
        if (sectors == null || sectors.Count == 0)
            return;

        Dictionary<int, ConstructionIntel> intelByInstanceId = new Dictionary<int, ConstructionIntel>();
        for (int i = 0; i < allSortedConstr.Count; i++)
        {
            ConstructionIntel intel = allSortedConstr[i];
            if (intel?.construction == null)
                continue;

            intelByInstanceId[intel.construction.InstanceId] = intel;
        }

        for (int i = 0; i < sectors.Count; i++)
        {
            SectorManager.SectorInfo source = sectors[i];
            if (source == null)
                continue;

            SectorIntel si = new SectorIntel
            {
                sector = source.Sector,
                sectorInfo = source
            };

            IReadOnlyList<SectorManager.SectorConstructionInfo> constructions = source.Constructions;
            for (int j = 0; j < constructions.Count; j++)
            {
                SectorManager.SectorConstructionInfo entry = constructions[j];
                if (entry == null)
                    continue;

                if (!intelByInstanceId.TryGetValue(entry.InstanceId, out ConstructionIntel intel))
                {
                    ConstructionManager construction = entry.Source;
                    intel = new ConstructionIntel
                    {
                        construction = construction,
                        distanceFromNearestAlly = construction != null
                            ? ComputeNearestAllyDistance(construction.CurrentCellPosition, alliedUnits, map)
                            : -1
                    };
                }

                si.entries.Add(intel);

                if (source.RepresentativeConstruction != null
                    && intel?.construction != null
                    && intel.construction.InstanceId == source.RepresentativeConstruction.InstanceId)
                {
                    si.representative = intel;
                }
            }

            if (si.representative == null && si.entries.Count > 0)
                si.representative = si.entries[0];

            si.sectorDistance = si.representative?.distanceFromNearestAlly ?? -1;
            sectorIntel.Add(si);
        }
    }

    // ── draw sections ──────────────────────────────────────────────────────────
    private void DrawActivePlayerSection()
    {
        foldActivePlayer = EditorGUILayout.Foldout(foldActivePlayer, "Jogador da Vez", true, EditorStyles.foldoutHeader);
        if (!foldActivePlayer) return;

        EditorGUI.indentLevel++;
        if (string.IsNullOrEmpty(activePlayerBlock))
        {
            EditorGUILayout.HelpBox("Rode a simulação.", MessageType.None);
        }
        else
        {
            TeamId activeTeam = matchController.ActiveTeam;
            Color tc = TeamUtils.GetColor(activeTeam);
            EditorGUILayout.LabelField($"■  {TeamUtils.GetName(activeTeam)}",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = tc } });
            EditorGUILayout.HelpBox(activePlayerBlock, MessageType.None);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawUnitSection(string title, List<UnitManager> units, ref bool foldout, Color lineColor)
    {
        foldout = EditorGUILayout.Foldout(foldout, $"{title}  ({units.Count})", true, EditorStyles.foldoutHeader);
        if (!foldout) return;

        EditorGUI.indentLevel++;
        if (units.Count == 0) { EditorGUILayout.HelpBox("Nenhuma unidade.", MessageType.None); EditorGUI.indentLevel--; return; }

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager u = units[i];
            if (u == null) continue;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            Color prev = GUI.color; GUI.color = lineColor;
            EditorGUILayout.LabelField($"{i + 1}. {u.UnitDisplayName}", EditorStyles.boldLabel);
            GUI.color = prev;
            if (GUILayout.Button("Sel", GUILayout.Width(36f))) Selection.activeGameObject = u.gameObject;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Time",    TeamUtils.GetName(u.TeamId));
            EditorGUILayout.LabelField("Hex",     FormatCell(u.CurrentCellPosition));
            EditorGUILayout.LabelField("Domínio", $"{u.GetDomain()} / {u.GetHeightLevel()}");
            EditorGUILayout.LabelField("Agiu",    u.HasActed ? "SIM" : "NÃO");
            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    private void DrawBaseTeamSection()
    {
        Color btColor = GetSectorColor(ConstructionSector.BaseTeam);
        bool isHighlighted = highlightedSectors.Contains(ConstructionSector.BaseTeam);

        // header com botão de highlight
        EditorGUILayout.BeginHorizontal();
        foldBaseTeam = EditorGUILayout.Foldout(foldBaseTeam,
            $"Base Team  ({baseTeamConstr.Count})", true, EditorStyles.foldoutHeader);
        DrawSectorHighlightButton(ConstructionSector.BaseTeam, isHighlighted, btColor);
        EditorGUILayout.EndHorizontal();

        if (!foldBaseTeam) return;

        EditorGUI.indentLevel++;
        if (baseTeamConstr.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma construção com setor BaseTeam.", MessageType.None);
            EditorGUI.indentLevel--;
            return;
        }

        TeamId? lastTeam = null;
        for (int i = 0; i < baseTeamConstr.Count; i++)
        {
            ConstructionIntel ci = baseTeamConstr[i];
            if (ci?.construction == null) continue;
            ConstructionManager c = ci.construction;

            // separador de time
            if (lastTeam != c.TeamId)
            {
                lastTeam = c.TeamId;
                Color tc = TeamUtils.GetColor(c.TeamId);
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField($"── {TeamUtils.GetName(c.TeamId)} ──",
                    new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = tc } });
            }

            DrawConstructionIntelRow(ci, i);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawAllConstructionsSection()
    {
        foldAllConstr = EditorGUILayout.Foldout(foldAllConstr,
            $"Todas as Construções  ({allSortedConstr.Count})", true, EditorStyles.foldoutHeader);
        if (!foldAllConstr) return;

        EditorGUI.indentLevel++;
        if (allSortedConstr.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma construção (exceto BaseTeam).", MessageType.None);
            EditorGUI.indentLevel--;
            return;
        }

        ConstructionSector? lastSector = null;
        for (int i = 0; i < allSortedConstr.Count; i++)
        {
            ConstructionIntel ci = allSortedConstr[i];
            if (ci?.construction == null) continue;
            ConstructionSector s = ci.construction.Sector;

            // separador de setor com cor
            if (lastSector != s)
            {
                lastSector = s;
                Color sc = GetSectorColor(s);
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField($"── {s} ──",
                    new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = sc } });
            }

            DrawConstructionIntelRow(ci, i);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawConstructionIntelRow(ConstructionIntel ci, int index)
    {
        ConstructionManager c = ci.construction;
        Color teamColor = TeamUtils.GetColor(c.TeamId);
        GUIStyle bold   = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = teamColor } };
        string flags    = BuildFlags(c);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{index + 1}. {c.ConstructionDisplayName}{flags}", bold);
        if (GUILayout.Button("Sel", GUILayout.Width(36f))) Selection.activeGameObject = c.gameObject;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Dono",  TeamUtils.GetName(c.TeamId));
        EditorGUILayout.LabelField("Hex",   FormatCell(c.CurrentCellPosition));
        if (c.IsCapturable)
            EditorGUILayout.LabelField("Captura",
                $"{c.CurrentCapturePoints} / {c.CapturePointsMax}  ({CapturePercent(c)}%)");

        string distStr = ci.distanceFromNearestAlly >= 0
            ? $"{ci.distanceFromNearestAlly} hex" : "—";
        EditorGUILayout.LabelField("Dist. aliado", distStr);
        EditorGUILayout.EndVertical();
    }

    private void DrawSectorsSection()
    {
        foldSectors = EditorGUILayout.Foldout(foldSectors,
            $"Setores  ({sectorIntel.Count})", true, EditorStyles.foldoutHeader);
        if (!foldSectors) return;

        EditorGUI.indentLevel++;
        if (sectorIntel.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum setor identificado.", MessageType.None);
            EditorGUI.indentLevel--;
            return;
        }

        for (int i = 0; i < sectorIntel.Count; i++)
        {
            SectorIntel si = sectorIntel[i];
            Color sc = GetSectorColor(si.sector);
            bool isHighlighted = highlightedSectors.Contains(si.sector);

            EditorGUILayout.BeginVertical("box");

            // header: nome + dist + botão de highlight
            EditorGUILayout.BeginHorizontal();
            string distStr = si.sectorDistance >= 0 ? $"  {si.sectorDistance} hex" : "  —";
            EditorGUILayout.LabelField($"{si.sector}{distStr}",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = sc } });
            DrawSectorHighlightButton(si.sector, isHighlighted, sc);
            EditorGUILayout.EndHorizontal();

            SectorManager.SectorInfo sectorInfo = si.sectorInfo;
            if (sectorInfo != null)
            {
                EditorGUILayout.LabelField(
                    $"Status: {sectorInfo.StatusText} | controleTotal={sectorInfo.IsFullyControlled} | disputa={sectorInfo.IsDisputed}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"Captura: {sectorInfo.TotalCurrentCapturePoints}/{sectorInfo.TotalCapturePointsMax} | dono={TeamUtils.GetName(sectorInfo.ControllingTeam)}",
                    EditorStyles.miniLabel);
            }

            // representante
            if (si.representative?.construction != null)
            {
                ConstructionManager rep = si.representative.construction;
                EditorGUILayout.LabelField(
                    $"★ rep: {rep.ConstructionDisplayName}  [{FormatCell(rep.CurrentCellPosition)}]  ({TeamUtils.GetName(rep.TeamId)})",
                    EditorStyles.miniLabel);
            }

            // participantes
            for (int j = 0; j < si.entries.Count; j++)
            {
                ConstructionManager c = si.entries[j].construction;
                if (c == null) continue;
                bool isRep = si.representative?.construction == c;
                Color entryColor = isRep ? TeamUtils.GetColor(c.TeamId) : Color.gray;
                GUIStyle style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = entryColor } };
                string captStr = c.IsCapturable ? $"  cap={c.CurrentCapturePoints}/{c.CapturePointsMax}" : string.Empty;
                int dist = si.entries[j].distanceFromNearestAlly;
                string dStr = dist >= 0 ? $"  d={dist}" : string.Empty;
                EditorGUILayout.LabelField(
                    $"  {(isRep ? "★" : "·")} {c.ConstructionDisplayName}{BuildFlags(c)}{captStr}{dStr}  [{TeamUtils.GetName(c.TeamId)}]",
                    style);
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    // ── plans section ──────────────────────────────────────────────────────────
    private void DrawPlansSection()
    {
        // Tenta encontrar o AIPlayerController na cena para ler os planos do turno
        AIPlayerController aiController = Object.FindAnyObjectByType<AIPlayerController>();
        IReadOnlyList<AIPlanIntent> activePlans = aiController?.CurrentTurnPlans;

        int planCount = activePlans != null ? activePlans.Count : 0;
        string header = planDatabase != null
            ? $"Planos Ativos  ({planCount} / {CountConfiguredPlans(planDatabase)} configurados)"
            : $"Planos Ativos  ({planCount})";

        foldPlans = EditorGUILayout.Foldout(foldPlans, header, true, EditorStyles.foldoutHeader);
        if (!foldPlans) return;

        EditorGUI.indentLevel++;

        if (planDatabase == null)
        {
            EditorGUILayout.HelpBox("Plan Database não configurado.", MessageType.None);
            EditorGUI.indentLevel--;
            return;
        }

        if (!Application.isPlaying || activePlans == null || activePlans.Count == 0)
        {
            // Modo editoria / sem planos ativos: mostra configuração do banco
            DrawPlanDatabaseConfig(planDatabase);
            EditorGUI.indentLevel--;
            return;
        }

        // Runtime: mostra os planos ativos com unidades designadas
        for (int i = 0; i < activePlans.Count; i++)
        {
            AIPlanIntent intent = activePlans[i];
            if (intent == null) continue;

            Color planColor = GetSectorColor(intent.Sector);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = planColor } };
            string planName = !string.IsNullOrWhiteSpace(intent.DisplayName)
                ? intent.DisplayName
                : (intent.Plan != null ? intent.Plan.displayName : "(plano dinamico)");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(
                $"{i + 1}. {planName}  [{intent.Sector}]",
                headerStyle);

            if (intent.HasCaptureTarget)
                EditorGUILayout.LabelField($"  Alvo: {intent.CaptureTargetLabel}  [{intent.CaptureTargetCell.x},{intent.CaptureTargetCell.y}]",
                    EditorStyles.miniLabel);

            if (intent.SectorEnemy != null && !intent.SectorEnemy.IsDead)
                EditorGUILayout.LabelField($"  Inimigo no setor: {intent.SectorEnemy.UnitDisplayName}",
                    EditorStyles.miniLabel);

            if (intent.Assignments.Count == 0)
            {
                EditorGUILayout.LabelField("  (sem unidades designadas)", EditorStyles.miniLabel);
            }
            else
            {
                for (int a = 0; a < intent.Assignments.Count; a++)
                {
                    AIPlanAssignment asgn = intent.Assignments[a];
                    UnitManager u = FindUnitByInstanceId(asgn.UnitInstanceId);
                    string unitName = u != null ? u.UnitDisplayName : $"#{asgn.UnitInstanceId}";
                    string role = asgn.Role.ToDebugLabel();
                    EditorGUILayout.LabelField($"  · [{role}] {unitName}", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUI.indentLevel--;
    }

    private static void DrawPlanDatabaseConfig(AIPlanDatabase db)
    {
        db.EnsureDefaults();

        void DrawPlanRow(AIPlanData plan, string tag)
        {
            if (plan == null) return;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"[{tag}] {plan.displayName}  [{plan.targetSector}]", EditorStyles.boldLabel);
            int cc = plan.activationConditions != null ? plan.activationConditions.Count : 0;
            EditorGUILayout.LabelField($"  Condições: {(cc == 0 ? "AlwaysActive" : cc.ToString())}", EditorStyles.miniLabel);
            int pc = plan.participants != null ? plan.participants.Count : 0;
            EditorGUILayout.LabelField($"  Participantes: {pc}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        DrawPlanRow(db.defensePlan, "Defesa");
        DrawPlanRow(db.attackPlan,  "Ataque");
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("[Variaveis dinamicos] Runtime", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"  Max planos por turno: {db.maxVariablePlans}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("  Composicao: heuristica por setor (INF/ARM/ART/APC)", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    private static int CountConfiguredPlans(AIPlanDatabase db)
    {
        if (db == null) return 0;
        int n = 0;
        if (db.defensePlan != null) n++;
        if (db.attackPlan  != null) n++;
        n += db.maxVariablePlans;
        return n;
    }

    private static UnitManager FindUnitByInstanceId(int instanceId)
    {
        if (UnitManager.AllActive == null) return null;
        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager u = UnitManager.AllActive[i];
            if (u != null && u.InstanceId == instanceId) return u;
        }
        return null;
    }

    // Botão de toggle de highlight para um setor — inline, sem quebrar layout
    private void DrawSectorHighlightButton(ConstructionSector sector, bool isHighlighted, Color sectorColor)
    {
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = isHighlighted ? sectorColor : Color.gray * 0.6f;
        string label = isHighlighted ? "○ Limpar" : "● Colorir";
        if (GUILayout.Button(label, GUILayout.Width(78f)))
        {
            if (isHighlighted) highlightedSectors.Remove(sector);
            else               highlightedSectors.Add(sector);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = prevBg;
    }

    // ── visibility ─────────────────────────────────────────────────────────────
    // Usa o sensor diretamente (sem cache FOW) para garantir resultado correto
    // independente do estado do cache do MatchController.
    private bool IsUnitVisibleForTeam(UnitManager target, TeamId observer,
        bool totalWar, bool losOn, bool spotterOn, bool stealthOn)
    {
        if (!totalWar) return true;

        Tilemap map = ResolveBoardTilemap();
        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstAsset<TerrainDatabase>();

        // Stealth: unidade que atirou neste turno é sempre detectável
        bool enforceStealthValidation = stealthOn && !target.HasFiredThisTurn;

        return PodeDetectarSensor.IsTargetObservedByTeam(
            target,
            (int)observer,
            map,
            db,
            dpqAirHeightConfig,
            losOn,
            spotterOn,
            enforceStealthValidation);
    }

    // ── distance ───────────────────────────────────────────────────────────────
    private static int ComputeNearestAllyDistance(Vector3Int target, List<UnitManager> allies, Tilemap map)
    {
        if (allies == null || allies.Count == 0) return -1;
        int best = int.MaxValue;
        for (int i = 0; i < allies.Count; i++)
        {
            if (allies[i] == null) continue;
            int d = HexDistance(map, allies[i].CurrentCellPosition, target, 128);
            if (d < best) best = d;
        }
        return best == int.MaxValue ? -1 : best;
    }

    private static int HexDistance(Tilemap tilemap, Vector3Int from, Vector3Int to, int maxSteps)
    {
        from.z = 0; to.z = 0;
        if (from == to) return 0;
        if (tilemap == null) return Mathf.RoundToInt((to - from).magnitude);

        HashSet<Vector3Int> visited  = new HashSet<Vector3Int> { from };
        Queue<Vector3Int>   frontier = new Queue<Vector3Int>();
        Queue<int>          depth    = new Queue<int>();
        frontier.Enqueue(from); depth.Enqueue(0);
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        int safeMax = Mathf.Clamp(maxSteps, 1, 256);

        while (frontier.Count > 0)
        {
            Vector3Int cur = frontier.Dequeue();
            int d = depth.Dequeue();
            if (d >= safeMax) continue;
            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, cur, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int n = neighbors[i]; n.z = 0;
                if (visited.Contains(n)) continue;
                if (n == to) return d + 1;
                visited.Add(n); frontier.Enqueue(n); depth.Enqueue(d + 1);
            }
        }
        return maxSteps;
    }

    // ── FOW ────────────────────────────────────────────────────────────────────
    private void TryRefreshFow()
    {
        if (matchController == null) { statusMessage = "MatchController não encontrado."; return; }
        matchController.RefreshFogOfWarForActiveTeam();
        statusMessage = $"FOW atualizado para {TeamUtils.GetName(matchController.ActiveTeam)}.";
    }

    // ── helpers ────────────────────────────────────────────────────────────────
    private static Vector3 ComputeSectorCentroidWorld(ConstructionSector sector)
    {
        IReadOnlyList<ConstructionManager> all = GetAllConstructions();
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < all.Count; i++)
        {
            ConstructionManager c = all[i];
            if (c == null || c.Sector != sector) continue;
            sum += c.transform.position;
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }

    private static string BuildFlags(ConstructionManager c)
    {
        string f = string.Empty;
        if (c.IsPlayerHeadQuarter) f += " [QG]";
        if (c.IsVictoryBuilding)   f += " [VIT]";
        return f;
    }

    private static int CapturePercent(ConstructionManager c) =>
        c.CapturePointsMax > 0 ? Mathf.RoundToInt(c.CurrentCapturePoints * 100f / c.CapturePointsMax) : 0;

    private static string FormatCell(Vector3Int cell) => $"{cell.x},{cell.y}";

    private string BuildActivePlayerText(TeamId team, bool totalWar, bool los, bool spotter, bool stealth)
    {
        bool   isAI     = matchController.IsActiveTeamAI();
        bool   defeated = matchController.IsTeamDefeated(team);
        string money    = matchController.EconomyEnabled
            ? $"  Dinheiro: {matchController.GetActualMoney(team)}" : string.Empty;
        return
            $"Turno: {matchController.CurrentTurn}  |  {(isAI ? "IA" : "Humano")}" +
            $"{(defeated ? "  [DERROTADO]" : string.Empty)}\n" +
            $"GameSetup: {matchController.GameSetup}{money}\n" +
            $"TotalWar={totalWar}  LoS={los}  Spotter={spotter}  Stealth={stealth}";
    }

    // ── context ────────────────────────────────────────────────────────────────
    private void AutoDetectContext()
    {
        if (matchController    == null) matchController    = Object.FindAnyObjectByType<MatchController>();
        if (turnStateManager   == null) turnStateManager   = Object.FindAnyObjectByType<TurnStateManager>();
        if (overrideTilemap    == null) overrideTilemap    = FindPreferredTilemap();
        if (terrainDatabase    == null) terrainDatabase    = FindFirstAsset<TerrainDatabase>();
        if (dpqAirHeightConfig == null) dpqAirHeightConfig = FindFirstAsset<DPQAirHeightConfig>();
    }

    private Tilemap ResolveBoardTilemap()
    {
        if (overrideTilemap != null) return overrideTilemap;
        if (useGameplaySensorContext) { Tilemap gp = ResolveGameplayTerrainTilemap(); if (gp != null) return gp; }
        return FindPreferredTilemap();
    }

    private Tilemap ResolveGameplayTerrainTilemap()
    {
        if (turnStateManager == null) turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();
        if (turnStateManager == null) return null;
        SerializedObject so = new SerializedObject(turnStateManager);
        SerializedProperty p = so.FindProperty("terrainTilemap");
        return p?.objectReferenceValue as Tilemap;
    }

    private static IReadOnlyList<UnitManager> GetAllUnits()
    {
        if (UnitManager.AllActive != null && UnitManager.AllActive.Count > 0) return UnitManager.AllActive;
        return Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
               ?? System.Array.Empty<UnitManager>();
    }

    private static IReadOnlyList<ConstructionManager> GetAllConstructions()
    {
        if (ConstructionManager.AllActive != null && ConstructionManager.AllActive.Count > 0)
            return ConstructionManager.AllActive;
        return Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
               ?? System.Array.Empty<ConstructionManager>();
    }

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap t = FindTilemapByName("TileMap");
        if (t != null) return t;
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps == null || maps.Length == 0) return null;
        for (int i = 0; i < maps.Length; i++)
            if (maps[i] != null && string.Equals(maps[i].name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                return maps[i];
        return maps[0];
    }

    private static Tilemap FindTilemapByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
            if (maps[i] != null && string.Equals(maps[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                return maps[i];
        return null;
    }

    private static T FindFirstAsset<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0) return null;
        for (int i = 0; i < guids.Length; i++)
        {
            T a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (a != null) return a;
        }
        return null;
    }

    // ── console logging ────────────────────────────────────────────────────────
    private static void LogUnitList(string tag, List<UnitManager> units)
    {
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager u = units[i];
            if (u == null) continue;
            Debug.Log($"[AIIntel][{tag}] {i + 1}. {u.UnitDisplayName} | " +
                      $"team={TeamUtils.GetName(u.TeamId)} hex={FormatCell(u.CurrentCellPosition)} " +
                      $"dom={u.GetDomain()}/{u.GetHeightLevel()} agiu={u.HasActed}");
        }
    }

    private static void LogConstructionList(string tag, List<ConstructionIntel> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            ConstructionIntel ci = list[i];
            if (ci?.construction == null) continue;
            ConstructionManager c = ci.construction;
            Debug.Log($"[AIIntel][{tag}] {i + 1}. {c.ConstructionDisplayName}{BuildFlags(c)} | " +
                      $"setor={c.Sector} dono={TeamUtils.GetName(c.TeamId)} " +
                      $"hex={FormatCell(c.CurrentCellPosition)} " +
                      $"cap={c.CurrentCapturePoints}/{c.CapturePointsMax} dist={ci.distanceFromNearestAlly}");
        }
    }

    private void LogSectors()
    {
        for (int i = 0; i < sectorIntel.Count; i++)
        {
            SectorIntel si = sectorIntel[i];
            string rep = si.representative?.construction?.ConstructionDisplayName ?? "(nenhum)";
            string status = si.sectorInfo != null ? si.sectorInfo.StatusText : "(sem-status)";
            string capture = si.sectorInfo != null ? $"{si.sectorInfo.TotalCurrentCapturePoints}/{si.sectorInfo.TotalCapturePointsMax}" : "0/0";
            Debug.Log($"[AIIntel][SETOR] {si.sector} | qtd={si.entries.Count} rep={rep} dist={si.sectorDistance} status={status} capture={capture}");
        }
    }
}

