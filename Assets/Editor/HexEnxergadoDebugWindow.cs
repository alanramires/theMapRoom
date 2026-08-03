using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HexEnxergadoDebugWindow : EditorWindow
{
    private sealed class ObserverEntry
    {
        public Object source;
        public string sourceType;
        public string sourceName;
        public TeamId team;
        public string layer;
        public Vector3Int cell;
        public int distance;
    }

    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private Vector3Int selectedCell;
    [SerializeField] private bool hasSelectedCell;
    [SerializeField] private bool autoRefresh = true;
    [SerializeField] private bool enableLos = true;
    [SerializeField] private bool enableSpotter = true;
    [SerializeField] private FogOfWarVisionMode visionMode = FogOfWarVisionMode.All;
    // Terminal burro: varre observadores de TODOS os times por padrao. "Quem
    // enxerga este hex" e pergunta naturalmente bilateral — restringir a um time
    // esconde metade da resposta, que costuma ser a metade interessante.
    [SerializeField] private bool restrictToActiveTeam = false;
    [SerializeField] private int settingsVersion;
    private const int CurrentSettingsVersion = 1;

    private readonly List<ObserverEntry> observers = new List<ObserverEntry>();
    private readonly HashSet<Vector3Int> visibleCells = new HashSet<Vector3Int>();
    private Vector2 scroll;
    private double nextRefreshTime;

    [MenuItem("Tools/FoW/Hex Enxergado")]
    public static void OpenWindow()
    {
        GetWindow<HexEnxergadoDebugWindow>("Hex Enxergado");
    }

    private void OnEnable()
    {
        MigrateSettings();
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // Ver PodeEnxergarSensorDebugWindow: [SerializeField] em EditorWindow
    // persiste entre sessoes, entao o default novo nao alcanca janela ja aberta.
    private void MigrateSettings()
    {
        if (settingsVersion >= CurrentSettingsVersion)
            return;

        restrictToActiveTeam = false;
        settingsVersion = CurrentSettingsVersion;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void Update()
    {
        if (!autoRefresh || !hasSelectedCell || EditorApplication.timeSinceStartup < nextRefreshTime)
            return;
        nextRefreshTime = EditorApplication.timeSinceStartup + 0.35d;
        ScanObservers();
        Repaint();
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Hex Enxergado", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Clique em um hex na Scene View para listar todas as unidades que enxergam essa célula usando as regras do PodeEnxergar.", MessageType.Info);

        boardTilemap = (Tilemap)EditorGUILayout.ObjectField("Tabuleiro", boardTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        dpqAirHeightConfig = (DPQAirHeightConfig)EditorGUILayout.ObjectField("DPQ Air Height", dpqAirHeightConfig, typeof(DPQAirHeightConfig), false);
        enableLos = EditorGUILayout.ToggleLeft("Validar linha de visão", enableLos);
        enableSpotter = EditorGUILayout.ToggleLeft("Considerar spotter", enableSpotter);
        visionMode = (FogOfWarVisionMode)EditorGUILayout.EnumPopup("Camada consultada", visionMode);
        restrictToActiveTeam = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "Restringir ao time ativo",
                "Desmarcado (padrao): varre observadores de qualquer time. "
                + "Marque apenas para reproduzir a visao de uma partida em andamento."),
            restrictToActiveTeam);
        autoRefresh = EditorGUILayout.ToggleLeft("Atualizar automaticamente", autoRefresh);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect")) AutoDetectContext();
        using (new EditorGUI.DisabledScope(!hasSelectedCell))
            if (GUILayout.Button("Atualizar")) ScanObservers();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Hex selecionado", hasSelectedCell ? selectedCell.ToString() : "Nenhum");
        if (!hasSelectedCell)
            return;

        EditorGUILayout.LabelField($"Enxergado por: {observers.Count}", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < observers.Count; i++)
        {
            ObserverEntry entry = observers[i];
            if (entry.source == null) continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.ObjectField(entry.sourceName, entry.source, typeof(Object), true);
            EditorGUILayout.LabelField("Fonte", entry.sourceType);
            EditorGUILayout.LabelField("Time", entry.team.ToString());
            EditorGUILayout.LabelField("Hex / distância", $"{entry.cell} / {entry.distance}");
            if (!string.IsNullOrEmpty(entry.layer)) EditorGUILayout.LabelField("Camada", entry.layer);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (boardTilemap == null)
            AutoDetectContext();
        if (boardTilemap == null)
            return;

        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            Plane plane = new Plane(boardTilemap.transform.forward, boardTilemap.transform.position);
            if (plane.Raycast(ray, out float enter))
            {
                selectedCell = boardTilemap.WorldToCell(ray.GetPoint(enter));
                selectedCell.z = 0;
                hasSelectedCell = true;
                ScanObservers();
                Repaint();
                evt.Use();
            }
        }

        if (!hasSelectedCell)
            return;
        Vector3 center = boardTilemap.GetCellCenterWorld(selectedCell);
        float radius = Mathf.Max(0.2f, boardTilemap.cellSize.x * 0.48f);
        Handles.color = observers.Count > 0 ? new Color(0.2f, 1f, 0.35f, 0.95f) : new Color(1f, 0.25f, 0.2f, 0.95f);
        Handles.DrawWireDisc(center, boardTilemap.transform.forward, radius);
        Handles.Label(center + Vector3.up * radius, $"{selectedCell}  ({observers.Count})");
    }

    private void ScanObservers()
    {
        observers.Clear();
        if (!hasSelectedCell || boardTilemap == null)
            return;

        MatchController match = FindAnyObjectByType<MatchController>();
        TeamId activeTeam = match != null ? match.ActiveTeam : TeamId.Neutral;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if (restrictToActiveTeam && activeTeam != TeamId.Neutral && unit.TeamId != activeTeam)
                continue;
            if (unit.BoardTilemap != null && unit.BoardTilemap != boardTilemap)
                continue;

            string matchedLayer = CollectForSelectedMode(unit);
            if (string.IsNullOrEmpty(matchedLayer))
                continue;

            Vector3Int observerCell = unit.CurrentCellPosition;
            observerCell.z = 0;
            observers.Add(new ObserverEntry
            {
                source = unit,
                sourceType = "Unidade",
                sourceName = unit.name,
                team = unit.TeamId,
                layer = matchedLayer,
                cell = observerCell,
                distance = HexDistanceOddR(observerCell, selectedCell)
            });
        }
        List<ConstructionManager> constructions = ConstructionManager.AllActive;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy) continue;
            if (construction.BoardTilemap != null && construction.BoardTilemap != boardTilemap) continue;
            // Mesma trava que as unidades tinham, e esta o toggle nao cobria: a
            // visao da construcao so era avaliada para o time ativo. Agora o
            // filtro e o mesmo, e desmarcado avalia a visao de qualquer time.
            bool evaluateVision = !restrictToActiveTeam
                || activeTeam == TeamId.Neutral
                || construction.TeamId == activeTeam;
            bool owned = evaluateVision;
            if (!owned && !construction.IsPlayerHeadQuarter) continue;
            Vector3Int sourceCell = construction.CurrentCellPosition;
            sourceCell.z = 0;
            int distance = HexDistanceOddR(sourceCell, selectedCell);
            int range = 0;
            if (owned && construction.TryResolveConstructionData(out ConstructionData data) && data != null)
                range = Mathf.Max(0, data.visao);
            if (owned ? distance > range : selectedCell != sourceCell) continue;
            observers.Add(new ObserverEntry
            {
                source = construction,
                sourceType = owned ? "Construção" : "QG global",
                sourceName = construction.name,
                team = construction.TeamId,
                layer = string.Empty,
                cell = sourceCell,
                distance = distance
            });
        }

        observers.Sort((a, b) => a.distance != b.distance
            ? a.distance.CompareTo(b.distance)
            : string.Compare(a.sourceName, b.sourceName, System.StringComparison.OrdinalIgnoreCase));
    }

    private string CollectForSelectedMode(UnitManager unit)
    {
        if (visionMode == FogOfWarVisionMode.All)
        {
            List<string> matches = new List<string>();
            if (CollectUnitCells(unit, true, Domain.Air, HeightLevel.AirLow)) matches.Add("AirLow");
            if (CollectUnitCells(unit, true, Domain.Air, HeightLevel.AirHigh)) matches.Add("AirHigh");
            if (CollectUnitCells(unit, true, Domain.Land, HeightLevel.Surface)) matches.Add("Land/Surface");
            if (CollectUnitCells(unit, true, Domain.Naval, HeightLevel.Surface)) matches.Add("Naval/Surface");
            if (CollectUnitCells(unit, true, Domain.Submarine, HeightLevel.Submerged)) matches.Add("Submarine/Submerged");
            return matches.Count > 0 ? string.Join(" + ", matches) : string.Empty;
        }

        if (visionMode == FogOfWarVisionMode.Air)
        {
            bool low = CollectUnitCells(unit, true, Domain.Air, HeightLevel.AirLow);
            bool high = CollectUnitCells(unit, true, Domain.Air, HeightLevel.AirHigh);
            return low && high ? "AirLow + AirHigh" : low ? "AirLow" : high ? "AirHigh" : string.Empty;
        }
        if (visionMode == FogOfWarVisionMode.Surface)
        {
            bool land = CollectUnitCells(unit, true, Domain.Land, HeightLevel.Surface);
            bool naval = CollectUnitCells(unit, true, Domain.Naval, HeightLevel.Surface);
            return land && naval ? "Land + Naval / Surface" : land ? "Land / Surface" : naval ? "Naval / Surface" : string.Empty;
        }
        return CollectUnitCells(unit, true, Domain.Submarine, HeightLevel.Submerged)
            ? "Submarine / Submerged"
            : string.Empty;
    }

    private bool CollectUnitCells(UnitManager unit, bool forceLayer, Domain domain, HeightLevel height)
    {
        visibleCells.Clear();
        PodeDetectarSensor.CollectVisibleCells(unit, boardTilemap, terrainDatabase, visibleCells,
            dpqAirHeightConfig, enableLos, forceLayer ? false : enableSpotter,
            useOccupantLayerForTarget: !forceLayer,
            preserveObserverLayerRangeForHexVisibility: false,
            forceVirtualTargetLayer: forceLayer,
            forcedVirtualTargetDomain: domain,
            forcedVirtualTargetHeight: height,
            useRangeOnlyForAirHighWhenConfigured: forceLayer);
        return visibleCells.Contains(selectedCell);
    }

    private void AutoDetectContext()
    {
        if (boardTilemap == null)
        {
            TurnStateManager state = FindAnyObjectByType<TurnStateManager>();
            if (state != null)
                boardTilemap = state.GetComponentInChildren<Tilemap>();
            UnitManager unit = FindAnyObjectByType<UnitManager>();
            if (boardTilemap == null && unit != null)
                boardTilemap = unit.BoardTilemap;
        }
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAsset<DPQAirHeightConfig>();
    }

    private static T FindFirstAsset<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static int HexDistanceOddR(Vector3Int a, Vector3Int b)
    {
        int aq = a.x - (a.y - (a.y & 1)) / 2;
        int ar = a.y;
        int bq = b.x - (b.y - (b.y & 1)) / 2;
        int br = b.y;
        int dx = aq - bq;
        int dz = ar - br;
        int dy = -dx - dz;
        return (Mathf.Abs(dx) + Mathf.Abs(dy) + Mathf.Abs(dz)) / 2;
    }
}
