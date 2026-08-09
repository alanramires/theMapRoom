using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif


/// <summary>
/// Interpreta a linha natural de praias do mapa e lhe da identidade militar.
///
/// A identidade nasce da cadeia percorrida, nao de centroide ou raio. Cada
/// componente desconectado comeca uma praia nova. Dentro de uma faixa longa,
/// a caminhada comeca numa extremidade deterministica e consome no maximo a
/// extensao configurada; a primeira celula seguinte inicia outro nome.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-340)]
[DisallowMultipleComponent]
[AddComponentMenu("The Map Room/Beach Manager")]
public sealed class BeachManager : MonoBehaviour, ISerializationCallbackReceiver
{
    private const int CurrentBuildAlgorithmVersion = 3;

    [Serializable]
    public sealed class BeachInfo
    {
        [SerializeField] private string beachId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Vector3Int startCell;
        [SerializeField] private Vector3Int endCell;
        [FormerlySerializedAs("representativeCell")]
        [SerializeField] private Vector3Int beachRepCell;
        [SerializeField] private int chainExtent;
        [SerializeField] private int connectedComponentIndex;
        [SerializeField] private List<Vector3Int> cells =
            new List<Vector3Int>();

        public string BeachId => beachId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public Vector3Int StartCell => startCell;
        public Vector3Int EndCell => endCell;
        /// <summary>
        /// Celula representativa no meio da cadeia desta praia. Serve para
        /// rotulo, camera e ranking macro; nao define pertencimento.
        /// </summary>
        public Vector3Int BeachRepCell => beachRepCell;
        public int ChainExtent => chainExtent;
        public int ConnectedComponentIndex => connectedComponentIndex;
        public int CellCount => cells != null ? cells.Count : 0;
        public IReadOnlyList<Vector3Int> Cells => cells;

        internal BeachInfo(
            Vector3Int start,
            Vector3Int end,
            Vector3Int representative,
            int extent,
            int componentIndex,
            List<Vector3Int> valueCells)
        {
            start.z = 0;
            end.z = 0;
            representative.z = 0;
            startCell = start;
            endCell = end;
            beachRepCell = representative;
            chainExtent = Mathf.Max(0, extent);
            connectedComponentIndex = Mathf.Max(0, componentIndex);
            cells = valueCells ?? new List<Vector3Int>();
        }

        internal void ApplyIdentity(string id, string name)
        {
            beachId = id ?? string.Empty;
            displayName = name ?? string.Empty;
        }
    }

    private sealed class StripGrowth
    {
        public readonly List<Vector3Int> Cells = new List<Vector3Int>();
        public readonly Dictionary<Vector3Int, Vector3Int> ParentByCell =
            new Dictionary<Vector3Int, Vector3Int>();
        public Vector3Int EndCell;
        public Vector3Int BeachRepCell;
        public int Extent;
    }

    // Joint Army/Navy spelling alphabet (1941), conhecido como "Able Baker".
    private static readonly string[] AmericanMilitaryAlphabet =
    {
        "Able", "Baker", "Charlie", "Dog", "Easy", "Fox",
        "George", "How", "Item", "Jig", "King", "Love", "Mike",
        "Nan", "Oboe", "Peter", "Queen", "Roger", "Sugar", "Tare",
        "Uncle", "Victor", "William", "X-ray", "Yoke", "Zebra"
    };

    // Cada cena e um mapa. Um singleton global permitiria que uma consulta
    // feita durante carregamento aditivo recebesse as praias de outra cena.
    // A identidade do catalogo, portanto, e o handle da Scene que o contem.
    private static readonly Dictionary<ulong, BeachManager> InstancesByScene =
        new Dictionary<ulong, BeachManager>();
    private static readonly IReadOnlyList<BeachInfo> EmptyBeaches =
        Array.Empty<BeachInfo>();

    [Header("Sources")]
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    [Header("Military Beach Size")]
    [Tooltip("TerrainTypeData que representa praia neste mapa.")]
    [SerializeField] private TerrainTypeData beachTerrainType;
    [Tooltip(
        "Extensao maxima percorrida pela cadeia de praia antes de iniciar " +
        "outro nome. Soldado: 3 MP; Operational: 6.")]
    [Range(1, 24)]
    [FormerlySerializedAs("maximumOperationalRadius")]
    [SerializeField] private int maximumConnectedStripLength = 6;

    [Header("Debug")]
    [SerializeField] private bool beachLog;
    [FormerlySerializedAs("drawBeachLabelsInScene")]
    [SerializeField] private bool paintBeachStripsInScene;
    [Tooltip("Tamanho das iniciais desenhadas sobre as praias na Scene View.")]
    [Range(8, 48)]
    [SerializeField] private int beachLabelFontSize = 18;
    [SerializeField, HideInInspector] private string sourceTopologyFingerprint =
        string.Empty;
    [FormerlySerializedAs("builtOperationalRadius")]
    [SerializeField, HideInInspector] private int builtConnectedStripLength;
    [SerializeField, HideInInspector] private TerrainTypeData builtBeachTerrainType;
    [SerializeField, HideInInspector] private int builtAlgorithmVersion;
    [SerializeField, HideInInspector] private List<BeachInfo> beaches =
        new List<BeachInfo>();

    [NonSerialized] private bool hydrated;
    private readonly Dictionary<Vector3Int, BeachInfo> beachByCell =
        new Dictionary<Vector3Int, BeachInfo>();
    private readonly Dictionary<string, BeachInfo> beachById =
        new Dictionary<string, BeachInfo>(StringComparer.Ordinal);

    public static BeachManager Instance => EnsureInstance(
        SceneManager.GetActiveScene(),
        createAtRuntime: true);
    public Tilemap BoardTilemap => boardTilemap;
    public TerrainDatabase TerrainDatabase => terrainDatabase;
    public TerrainTypeData BeachTerrainType => beachTerrainType;
    public int MaximumConnectedStripLength =>
        Mathf.Max(1, maximumConnectedStripLength);
    public bool PaintBeachStripsInScene => paintBeachStripsInScene;
    public int BeachLabelFontSize => Mathf.Clamp(beachLabelFontSize, 8, 48);
    public IReadOnlyList<BeachInfo> Beaches
    {
        get
        {
            EnsureCurrent();
            return beaches;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Tilemap[] tilemaps =
            FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null
                || tilemap.gameObject.scene != activeScene)
            {
                continue;
            }
            EnsureInstance(activeScene, createAtRuntime: true);
            return;
        }
    }

    public static BeachManager GetOrCreate(
        Tilemap tilemap = null,
        TerrainDatabase database = null)
    {
        Scene targetScene = tilemap != null
            ? tilemap.gameObject.scene
            : SceneManager.GetActiveScene();
        return GetOrCreateForScene(targetScene, tilemap, database);
    }

    public static BeachManager GetOrCreateForScene(
        Scene targetScene,
        Tilemap tilemap = null,
        TerrainDatabase database = null)
    {
        BeachManager manager = EnsureInstance(
            targetScene,
            createAtRuntime: true);
        if (manager == null)
            return null;

        manager.ConfigureSources(tilemap, database);
        manager.EnsureCurrent();
        return manager;
    }

    public static IReadOnlyList<BeachInfo> GetAllBeachInfos(
        Tilemap tilemap = null,
        TerrainDatabase database = null)
    {
        BeachManager manager = GetOrCreate(tilemap, database);
        return manager != null ? manager.Beaches : EmptyBeaches;
    }

    public static bool TryGetBeachAtCell(
        Vector3Int cell,
        out BeachInfo beach,
        Tilemap tilemap = null,
        TerrainDatabase database = null)
    {
        beach = null;
        BeachManager manager = GetOrCreate(tilemap, database);
        return manager != null && manager.TryGetAtCell(cell, out beach);
    }

    public bool TryGetAtCell(Vector3Int cell, out BeachInfo beach)
    {
        EnsureCurrent();
        cell.z = 0;
        return beachByCell.TryGetValue(cell, out beach);
    }

    public bool TryGetById(string beachId, out BeachInfo beach)
    {
        EnsureCurrent();
        if (string.IsNullOrWhiteSpace(beachId))
        {
            beach = null;
            return false;
        }
        return beachById.TryGetValue(beachId, out beach);
    }

    public void ConfigureSources(
        Tilemap tilemap,
        TerrainDatabase database)
    {
        if (tilemap != null
            && gameObject.scene.IsValid()
            && tilemap.gameObject.scene != gameObject.scene)
        {
            if (beachLog)
            {
                Debug.LogError(
                    $"[BeachManager] recusou Tilemap da cena " +
                    $"'{tilemap.gameObject.scene.name}': este catalogo " +
                    $"pertence a '{gameObject.scene.name}'.",
                    this);
            }
            return;
        }

        bool changed = false;
        if (tilemap != null && tilemap != boardTilemap)
        {
            boardTilemap = tilemap;
            changed = true;
        }
        if (database != null && database != terrainDatabase)
        {
            terrainDatabase = database;
            changed = true;
        }
        if (changed)
            sourceTopologyFingerprint = string.Empty;
    }

    [ContextMenu("Rebuild Military Beaches")]
    public void RebuildMilitaryBeaches()
    {
        Rebuild("manual", forceLog: true);
    }

    private static BeachManager EnsureInstance(
        Scene targetScene,
        bool createAtRuntime)
    {
        if (!targetScene.IsValid())
            targetScene = SceneManager.GetActiveScene();

        ulong sceneHandle = GetSceneKey(targetScene);
        if (InstancesByScene.TryGetValue(
                sceneHandle,
                out BeachManager registered))
        {
            if (registered != null
                && registered.gameObject.scene == targetScene)
            {
                return registered;
            }
            InstancesByScene.Remove(sceneHandle);
        }

        BeachManager[] candidates =
            FindObjectsByType<BeachManager>(FindObjectsInactive.Include);
        for (int i = 0; i < candidates.Length; i++)
        {
            BeachManager existing = candidates[i];
            if (existing == null
                || existing.gameObject.scene != targetScene)
                continue;
            InstancesByScene[sceneHandle] = existing;
            return existing;
        }

        // Consultas de Inspector nao devem sujar a cena criando objetos.
        if (!Application.isPlaying || !createAtRuntime)
            return null;

        GameObject host = new GameObject(nameof(BeachManager));
        if (targetScene.IsValid()
            && targetScene.isLoaded
            && host.scene != targetScene)
        {
            SceneManager.MoveGameObjectToScene(host, targetScene);
        }
        BeachManager created = host.AddComponent<BeachManager>();
        InstancesByScene[GetSceneKey(created.gameObject.scene)] = created;
        return created;
    }

    private static ulong GetSceneKey(Scene scene) =>
        scene.handle.GetRawData();

    private void Awake()
    {
        ulong sceneHandle = GetSceneKey(gameObject.scene);
        if (InstancesByScene.TryGetValue(
                sceneHandle,
                out BeachManager existing)
            && existing != null
            && existing != this)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                Debug.LogError(
                    $"[BeachManager] mais de um catalogo na cena " +
                    $"'{gameObject.scene.name}'. Mantenha somente um.",
                    this);
            return;
        }
        InstancesByScene[sceneHandle] = this;
    }

    private void OnEnable()
    {
        ulong sceneHandle = GetSceneKey(gameObject.scene);
        if (InstancesByScene.TryGetValue(
                sceneHandle,
                out BeachManager existing)
            && existing != null
            && existing != this)
        {
            return;
        }
        InstancesByScene[sceneHandle] = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Hydrate();
        EnsureCurrent();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        ulong sceneHandle = GetSceneKey(gameObject.scene);
        if (InstancesByScene.TryGetValue(
                sceneHandle,
                out BeachManager registered)
            && registered == this)
        {
            InstancesByScene.Remove(sceneHandle);
        }
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        hydrated = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maximumConnectedStripLength =
            Mathf.Clamp(maximumConnectedStripLength, 1, 24);
        beachLabelFontSize = beachLabelFontSize <= 0
            ? 18
            : Mathf.Clamp(beachLabelFontSize, 8, 48);
        if (Application.isPlaying)
            return;
        Rebuild("on-validate");
        EditorApplication.QueuePlayerLoopUpdate();
    }
#endif

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (gameObject.scene != scene)
            return;
        Rebuild($"scene-loaded:{scene.name}");
    }

    private void EnsureCurrent()
    {
        if (!hydrated)
            Hydrate();
        TryResolveTopology(out BoardTopologyIndex topology);
        if (boardTilemap == null)
            AutoResolveSourcesFromScene();
        if (boardTilemap == null)
            return;

        int length = MaximumConnectedStripLength;
        if (builtAlgorithmVersion == CurrentBuildAlgorithmVersion
            && builtConnectedStripLength == length
            && builtBeachTerrainType == beachTerrainType
            && !string.IsNullOrWhiteSpace(sourceTopologyFingerprint))
        {
            return;
        }
        RebuildFromSources(topology, "source-changed");
    }

    private void Rebuild(string reason, bool forceLog = false)
    {
        TryResolveTopology(out BoardTopologyIndex topology);
        if (boardTilemap == null)
            AutoResolveSourcesFromScene();
        if (boardTilemap == null)
        {
            beaches.Clear();
            sourceTopologyFingerprint = string.Empty;
            builtConnectedStripLength = MaximumConnectedStripLength;
            builtBeachTerrainType = beachTerrainType;
            builtAlgorithmVersion = CurrentBuildAlgorithmVersion;
            Hydrate();
            if (beachLog || forceLog)
            {
                Debug.LogWarning(
                    $"[BeachManager] sem Board Tilemap ({reason}).",
                    this);
            }
            return;
        }
        RebuildFromSources(topology, reason, forceLog);
    }

    private void RebuildFromSources(
        BoardTopologyIndex topology,
        string reason,
        bool forceLog = false)
    {
        beaches.Clear();
        int maximumLength = MaximumConnectedStripLength;
        List<Vector3Int> configuredBeachCells =
            CollectConfiguredBeachCells(
                boardTilemap,
                beachTerrainType,
                out int compatibleTilemapCount);
        List<List<Vector3Int>> components =
            DiscoverConnectedBeachComponents(
                boardTilemap,
                configuredBeachCells);

        for (int componentIndex = 0;
             componentIndex < components.Count;
             componentIndex++)
        {
            beaches.AddRange(PartitionCoastalChain(
                boardTilemap,
                components[componentIndex],
                maximumLength,
                componentIndex));
        }

        // A ordem produzida e a ordem da caminhada: componente mais baixo no
        // mapa primeiro; dentro dele, Able -> Baker -> Charlie ao longo da costa.
        for (int i = 0; i < beaches.Count; i++)
        {
            BeachInfo beach = beaches[i];
            beach.ApplyIdentity(
                BuildStableBeachId(
                    ResolveMapId(topology, boardTilemap),
                    beach.Cells),
                BuildBeachName(i));
        }

        sourceTopologyFingerprint = BuildBeachSourceFingerprint(
            ResolveMapId(topology, boardTilemap),
            beachTerrainType != null ? beachTerrainType.paletteTile : null,
            configuredBeachCells);
        builtConnectedStripLength = maximumLength;
        builtBeachTerrainType = beachTerrainType;
        builtAlgorithmVersion = CurrentBuildAlgorithmVersion;
        Hydrate();

        if (beachLog || forceLog)
        {
            string terrainLabel = beachTerrainType != null
                ? (!string.IsNullOrWhiteSpace(beachTerrainType.displayName)
                    ? beachTerrainType.displayName
                    : beachTerrainType.name)
                : "<nao configurado>";
            string paletteLabel = beachTerrainType != null
                && beachTerrainType.paletteTile != null
                    ? beachTerrainType.paletteTile.name
                    : "<ausente>";
            Debug.Log(
                $"[BeachManager] rebuild reason={reason ?? "none"} " +
                $"source={(topology != null ? "topology+tilemap" : "tilemap")} " +
                $"terrain={terrainLabel} palette={paletteLabel} " +
                $"tilemaps={compatibleTilemapCount} " +
                $"componentes={components.Count} praias={beaches.Count} " +
                $"extensao={maximumLength} cells={configuredBeachCells.Count}",
                this);
            if (forceLog && beaches.Count > 0)
            {
                var summaries = new List<string>(beaches.Count);
                for (int i = 0; i < beaches.Count; i++)
                {
                    BeachInfo beach = beaches[i];
                    summaries.Add(
                        $"{beach.DisplayName}[hexes={beach.CellCount}," +
                        $"len={beach.ChainExtent},rep={beach.BeachRepCell}]");
                }
                Debug.Log(
                    $"[BeachManager] {string.Join(" | ", summaries)}",
                    this);
            }
        }
    }

    private bool TryResolveTopology(out BoardTopologyIndex topology)
    {
        topology = null;
        if (boardTilemap != null
            && BoardTopologyIndex.TryGetFor(boardTilemap, out topology)
            && topology != null)
        {
            if (terrainDatabase == null)
                terrainDatabase = topology.TerrainDatabase;
            return true;
        }

        BoardTopologyIndex[] indices =
            FindObjectsByType<BoardTopologyIndex>(FindObjectsInactive.Include);
        for (int i = 0; i < indices.Length; i++)
        {
            BoardTopologyIndex candidate = indices[i];
            if (candidate == null
                || candidate.gameObject.scene != gameObject.scene)
            {
                continue;
            }
            topology = candidate;
            boardTilemap = candidate.BoardTilemap;
            terrainDatabase = candidate.TerrainDatabase;
            return true;
        }

        if (boardTilemap == null || terrainDatabase == null)
            AutoResolveSourcesFromScene();
        topology = BoardTopologyIndex.GetOrCreateRuntime(
            boardTilemap,
            terrainDatabase);
        return topology != null && topology.IsReady;
    }

    private void AutoResolveSourcesFromScene()
    {
        TurnStateManager[] managers =
            FindObjectsByType<TurnStateManager>(FindObjectsInactive.Include);
        for (int i = 0; i < managers.Length; i++)
        {
            TurnStateManager manager = managers[i];
            if (manager == null
                || manager.gameObject.scene != gameObject.scene)
            {
                continue;
            }
            if (boardTilemap == null)
                boardTilemap = manager.MovementTilemapRef;
            if (terrainDatabase == null)
                terrainDatabase = manager.TerrainDatabaseRef;
            if (boardTilemap != null && terrainDatabase != null)
                return;
        }
    }

    private static List<Vector3Int> CollectConfiguredBeachCells(
        Tilemap boardTilemap,
        TerrainTypeData configuredTerrain,
        out int compatibleTilemapCount)
    {
        var result = new List<Vector3Int>();
        compatibleTilemapCount = 0;
        if (boardTilemap == null
            || configuredTerrain == null
            || configuredTerrain.paletteTile == null)
        {
            return result;
        }

        List<Tilemap> tilemaps = CollectCompatibleTilemaps(boardTilemap);
        compatibleTilemapCount = tilemaps.Count;
        var uniqueCells = new HashSet<Vector3Int>();
        TileBase beachPaletteTile = configuredTerrain.paletteTile;
        for (int i = 0; i < tilemaps.Count; i++)
        {
            Tilemap map = tilemaps[i];
            if (map == null)
                continue;

            foreach (Vector3Int rawCell in map.cellBounds.allPositionsWithin)
            {
                if (map.GetTile(rawCell) != beachPaletteTile)
                    continue;
                Vector3Int cell = rawCell;
                cell.z = 0;
                uniqueCells.Add(cell);
            }
        }

        result.AddRange(uniqueCells);
        result.Sort(CompareCells);
        return result;
    }

    private static List<Tilemap> CollectCompatibleTilemaps(
        Tilemap boardTilemap)
    {
        var maps = new List<Tilemap> { boardTilemap };
        GridLayout grid = boardTilemap.layoutGrid;
        if (grid == null)
            return maps;

        Tilemap[] candidates =
            grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < candidates.Length; i++)
        {
            Tilemap map = candidates[i];
            if (map == null
                || map == boardTilemap
                || map.gameObject.scene != boardTilemap.gameObject.scene)
            {
                continue;
            }
            maps.Add(map);
        }
        return maps;
    }

    private static List<List<Vector3Int>> DiscoverConnectedBeachComponents(
        Tilemap boardTilemap,
        IReadOnlyList<Vector3Int> source)
    {
        var remaining = new HashSet<Vector3Int>();
        for (int i = 0; i < source.Count; i++)
        {
            Vector3Int cell = source[i];
            cell.z = 0;
            remaining.Add(cell);
        }

        var components = new List<List<Vector3Int>>();
        var queue = new Queue<Vector3Int>();
        var neighbors = new List<Vector3Int>(6);
        while (remaining.Count > 0)
        {
            Vector3Int seed = FindFirstCell(remaining);
            var component = new List<Vector3Int>();
            queue.Enqueue(seed);
            remaining.Remove(seed);
            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                component.Add(current);
                UnitMovementPathRules.GetImmediateHexNeighbors(
                    boardTilemap,
                    current,
                    neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Vector3Int neighbor = neighbors[i];
                    neighbor.z = 0;
                    if (remaining.Remove(neighbor))
                        queue.Enqueue(neighbor);
                }
            }
            component.Sort(CompareCells);
            components.Add(component);
        }
        components.Sort((left, right) =>
            CompareCells(left[0], right[0]));
        return components;
    }

    private static List<BeachInfo> PartitionCoastalChain(
        Tilemap boardTilemap,
        List<Vector3Int> component,
        int maximumLength,
        int componentIndex)
    {
        var result = new List<BeachInfo>();
        if (component == null || component.Count == 0)
            return result;

        var remaining = new HashSet<Vector3Int>(component);
        var pendingStarts = new List<Vector3Int>();
        var neighbors = new List<Vector3Int>(6);
        pendingStarts.Add(FindNaturalChainStart(boardTilemap, remaining));

        while (remaining.Count > 0)
        {
            Vector3Int start = TakeNextPendingStart(
                boardTilemap,
                remaining,
                pendingStarts);
            StripGrowth strip = GrowConnectedStrip(
                boardTilemap,
                remaining,
                start,
                maximumLength);

            for (int i = 0; i < strip.Cells.Count; i++)
                remaining.Remove(strip.Cells[i]);

            // As primeiras celulas ainda nao consumidas, adjacentes a esta
            // faixa, sao os inicios naturais das faixas seguintes.
            for (int i = 0; i < strip.Cells.Count; i++)
            {
                UnitMovementPathRules.GetImmediateHexNeighbors(
                    boardTilemap,
                    strip.Cells[i],
                    neighbors);
                for (int n = 0; n < neighbors.Count; n++)
                {
                    Vector3Int neighbor = neighbors[n];
                    neighbor.z = 0;
                    if (remaining.Contains(neighbor)
                        && !pendingStarts.Contains(neighbor))
                    {
                        pendingStarts.Add(neighbor);
                    }
                }
            }
            pendingStarts.Sort(CompareCells);

            result.Add(new BeachInfo(
                start,
                strip.EndCell,
                strip.BeachRepCell,
                strip.Extent,
                componentIndex,
                strip.Cells));
        }
        return result;
    }

    private static Vector3Int TakeNextPendingStart(
        Tilemap boardTilemap,
        HashSet<Vector3Int> remaining,
        List<Vector3Int> pendingStarts)
    {
        while (pendingStarts.Count > 0)
        {
            Vector3Int candidate = pendingStarts[0];
            pendingStarts.RemoveAt(0);
            if (remaining.Contains(candidate))
                return candidate;
        }
        return FindNaturalChainStart(boardTilemap, remaining);
    }

    private static Vector3Int FindNaturalChainStart(
        Tilemap boardTilemap,
        HashSet<Vector3Int> cells)
    {
        bool found = false;
        Vector3Int best = Vector3Int.zero;
        int bestDegree = int.MaxValue;
        var neighbors = new List<Vector3Int>(6);
        foreach (Vector3Int rawCell in cells)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            int degree = 0;
            UnitMovementPathRules.GetImmediateHexNeighbors(
                boardTilemap,
                cell,
                neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int neighbor = neighbors[i];
                neighbor.z = 0;
                if (cells.Contains(neighbor))
                    degree++;
            }
            if (!found
                || degree < bestDegree
                || (degree == bestDegree && CompareCells(cell, best) < 0))
            {
                best = cell;
                bestDegree = degree;
                found = true;
            }
        }
        return best;
    }

    private static StripGrowth GrowConnectedStrip(
        Tilemap boardTilemap,
        HashSet<Vector3Int> allowed,
        Vector3Int start,
        int maximumLength)
    {
        var growth = new StripGrowth();
        var distances = new Dictionary<Vector3Int, int>
        {
            [start] = 0
        };
        var queue = new Queue<Vector3Int>();
        queue.Enqueue(start);
        growth.EndCell = start;
        growth.BeachRepCell = start;
        var neighbors = new List<Vector3Int>(6);

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int distance = distances[current];
            growth.Cells.Add(current);
            if (distance > growth.Extent
                || (distance == growth.Extent
                    && CompareCells(current, growth.EndCell) < 0))
            {
                growth.Extent = distance;
                growth.EndCell = current;
            }
            if (distance >= maximumLength)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(
                boardTilemap,
                current,
                neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int neighbor = neighbors[i];
                neighbor.z = 0;
                if (!allowed.Contains(neighbor)
                    || distances.ContainsKey(neighbor))
                {
                    continue;
                }
                distances[neighbor] = distance + 1;
                growth.ParentByCell[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        // Celula de rotulo no meio do caminho efetivamente percorrido. Ela e
        // apenas representativa; nao participa da regra de identidade.
        Vector3Int representative = growth.EndCell;
        int middleDistance = growth.Extent / 2;
        while (distances.TryGetValue(representative, out int distance)
               && distance > middleDistance
               && growth.ParentByCell.TryGetValue(
                   representative,
                   out Vector3Int parent))
        {
            representative = parent;
        }
        growth.BeachRepCell = representative;
        return growth;
    }

    private void Hydrate()
    {
        beachByCell.Clear();
        beachById.Clear();
        if (beaches == null)
            beaches = new List<BeachInfo>();
        for (int i = 0; i < beaches.Count; i++)
        {
            BeachInfo beach = beaches[i];
            if (beach == null)
                continue;
            if (!string.IsNullOrWhiteSpace(beach.BeachId))
                beachById[beach.BeachId] = beach;
            IReadOnlyList<Vector3Int> cells = beach.Cells;
            if (cells == null)
                continue;
            for (int c = 0; c < cells.Count; c++)
            {
                Vector3Int cell = cells[c];
                cell.z = 0;
                beachByCell[cell] = beach;
            }
        }
        hydrated = true;
    }

    private static Vector3Int FindFirstCell(HashSet<Vector3Int> cells)
    {
        bool found = false;
        Vector3Int best = Vector3Int.zero;
        foreach (Vector3Int raw in cells)
        {
            Vector3Int cell = raw;
            cell.z = 0;
            if (!found || CompareCells(cell, best) < 0)
            {
                best = cell;
                found = true;
            }
        }
        return best;
    }

    private static int CompareCells(Vector3Int left, Vector3Int right)
    {
        int y = left.y.CompareTo(right.y);
        if (y != 0)
            return y;
        int x = left.x.CompareTo(right.x);
        if (x != 0)
            return x;
        return left.z.CompareTo(right.z);
    }

    private static string BuildBeachName(int index)
    {
        int alphabetIndex = Mathf.Abs(index) % AmericanMilitaryAlphabet.Length;
        int cycle = Mathf.Abs(index) / AmericanMilitaryAlphabet.Length + 1;
        string word = AmericanMilitaryAlphabet[alphabetIndex];
        return cycle == 1
            ? $"{word} Beach"
            : $"{word}-{cycle} Beach";
    }

    private static string ResolveMapId(
        BoardTopologyIndex topology,
        Tilemap boardTilemap)
    {
        if (topology != null
            && !string.IsNullOrWhiteSpace(topology.MapId))
        {
            return topology.MapId;
        }
        if (boardTilemap == null)
            return "beach-map";

        Scene scene = boardTilemap.gameObject.scene;
        string sceneId = !string.IsNullOrWhiteSpace(scene.path)
            ? scene.path
            : scene.name;
        return $"{sceneId}::{boardTilemap.name}";
    }

    private static string BuildBeachSourceFingerprint(
        string mapId,
        TileBase paletteTile,
        IReadOnlyList<Vector3Int> sourceCells)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;

        AddStableTextToHash(mapId, ref hash, prime);
        AddStableTextToHash(
            paletteTile != null ? paletteTile.name : string.Empty,
            ref hash,
            prime);
        for (int i = 0; i < sourceCells.Count; i++)
        {
            Vector3Int cell = sourceCells[i];
            hash ^= unchecked((uint)cell.x);
            hash *= prime;
            hash ^= unchecked((uint)cell.y);
            hash *= prime;
        }
        return $"beach-source-{hash:x16}";
    }

    private static void AddStableTextToHash(
        string value,
        ref ulong hash,
        ulong prime)
    {
        string source = value ?? string.Empty;
        for (int i = 0; i < source.Length; i++)
        {
            hash ^= source[i];
            hash *= prime;
        }
    }

    private static string BuildStableBeachId(
        string mapId,
        IReadOnlyList<Vector3Int> sourceCells)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        string source = mapId ?? string.Empty;
        for (int i = 0; i < source.Length; i++)
        {
            hash ^= source[i];
            hash *= prime;
        }

        var cells = new List<Vector3Int>(sourceCells.Count);
        for (int i = 0; i < sourceCells.Count; i++)
            cells.Add(sourceCells[i]);
        cells.Sort(CompareCells);
        for (int i = 0; i < cells.Count; i++)
        {
            hash ^= unchecked((uint)cells[i].x);
            hash *= prime;
            hash ^= unchecked((uint)cells[i].y);
            hash *= prime;
        }
        return $"beach-{hash:x16}";
    }
}
