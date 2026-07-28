using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-8500)]
public sealed class BoardTopologyIndex :
    MonoBehaviour,
    ISerializationCallbackReceiver
{
    public const int CurrentTopologyVersion = 1;

    private static readonly List<BoardTopologyIndex> ActiveIndices =
        new List<BoardTopologyIndex>();
    private static readonly IReadOnlyList<Vector3Int> EmptyCells =
        Array.Empty<Vector3Int>();
    private static readonly IReadOnlyList<StructureData> EmptyStructures =
        Array.Empty<StructureData>();

    [Header("Sources")]
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    [Header("Serialized Topology")]
    [SerializeField] private int topologyVersion;
    [SerializeField] private string mapId = string.Empty;
    [SerializeField] private string topologyFingerprint = string.Empty;
    [SerializeField] private List<BoardTopologyCellRecord> cells =
        new List<BoardTopologyCellRecord>();
    [SerializeField] private List<BoardTopologyRouteEdgeRecord> routeEdges =
        new List<BoardTopologyRouteEdgeRecord>();

    [Header("Runtime Validation")]
    [Tooltip(
        "Compara o fingerprint serializado com as fontes uma vez no " +
        "carregamento. Se divergir, usa um índice reconstruído em memória.")]
    [SerializeField] private bool validateFingerprintAtRuntime = true;

    [NonSerialized] private bool hydrated;
    [NonSerialized] private bool runtimeInitialized;
    [NonSerialized] private bool runtimeFallback;

    private readonly Dictionary<Vector3Int, BoardTopologyCellRecord>
        cellByPosition =
            new Dictionary<Vector3Int, BoardTopologyCellRecord>();
    private readonly Dictionary<BoardTopologyEdgeKey, List<StructureData>>
        structuresByRouteEdge =
            new Dictionary<BoardTopologyEdgeKey, List<StructureData>>();
    private readonly List<Vector3Int> indexedCells =
        new List<Vector3Int>();
    private readonly List<Vector3Int> beachCells =
        new List<Vector3Int>();
    private readonly List<Vector3Int> coastalCells =
        new List<Vector3Int>();
    private readonly List<Vector3Int> potentialLandingCells =
        new List<Vector3Int>();
    private readonly List<Vector3Int> potentialEmbarkCells =
        new List<Vector3Int>();
    private readonly List<Vector3Int> potentialDisembarkCells =
        new List<Vector3Int>();
    private readonly List<Vector3Int> supplierConstructionCells =
        new List<Vector3Int>();

    public Tilemap BoardTilemap => boardTilemap;
    public TerrainDatabase TerrainDatabase => terrainDatabase;
    public int TopologyVersion => topologyVersion;
    public string MapId => mapId ?? string.Empty;
    public string TopologyFingerprint =>
        topologyFingerprint ?? string.Empty;
    public bool IsRuntimeFallback => runtimeFallback;
    public bool IsReady =>
        hydrated
        && topologyVersion == CurrentTopologyVersion
        && cellByPosition.Count > 0;
    public int CellCount => cells != null ? cells.Count : 0;
    public int RouteEdgeCount =>
        routeEdges != null ? routeEdges.Count : 0;
    public IReadOnlyList<BoardTopologyCellRecord> Cells => cells;
    public IReadOnlyList<BoardTopologyRouteEdgeRecord> RouteEdges =>
        routeEdges;
    public IReadOnlyList<Vector3Int> IndexedCells
    {
        get
        {
            EnsureHydrated();
            return indexedCells;
        }
    }
    public IReadOnlyList<Vector3Int> BeachCells
    {
        get
        {
            EnsureHydrated();
            return beachCells;
        }
    }
    public IReadOnlyList<Vector3Int> CoastalCells
    {
        get
        {
            EnsureHydrated();
            return coastalCells;
        }
    }
    public IReadOnlyList<Vector3Int> PotentialLandingCells
    {
        get
        {
            EnsureHydrated();
            return potentialLandingCells;
        }
    }
    public IReadOnlyList<Vector3Int> PotentialEmbarkCells
    {
        get
        {
            EnsureHydrated();
            return potentialEmbarkCells;
        }
    }
    public IReadOnlyList<Vector3Int> PotentialDisembarkCells
    {
        get
        {
            EnsureHydrated();
            return potentialDisembarkCells;
        }
    }
    public IReadOnlyList<Vector3Int> SupplierConstructionCells
    {
        get
        {
            EnsureHydrated();
            return supplierConstructionCells;
        }
    }

    private void Awake()
    {
        Register();
        InitializeRuntime();
    }

    private void OnEnable()
    {
        Register();
        if (Application.isPlaying)
            InitializeRuntime();
        else
            Hydrate();
    }

    private void OnDisable()
    {
        ActiveIndices.Remove(this);
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        hydrated = false;
        runtimeInitialized = false;
    }

    public void ConfigureSources(
        Tilemap tilemap,
        TerrainDatabase database)
    {
        boardTilemap = tilemap;
        terrainDatabase = database;
        hydrated = false;
        runtimeInitialized = false;
    }

    public bool AutoResolveSources()
    {
        ResolveSourcesForScene(
            gameObject.scene,
            out Tilemap resolvedTilemap,
            out TerrainDatabase resolvedTerrain);
        if (boardTilemap == null)
            boardTilemap = resolvedTilemap;
        if (terrainDatabase == null)
            terrainDatabase = resolvedTerrain;
        return boardTilemap != null && terrainDatabase != null;
    }

    [ContextMenu("Board Topology/Rebuild Serialized Index")]
    public void RebuildSerializedIndex()
    {
        AutoResolveSources();
        BoardTopologyBuildResult built =
            BoardTopologyIndexBuilder.Build(
                boardTilemap,
                terrainDatabase);
        ApplyBuildResult(built, isRuntimeFallback: false);
        LogValidation(
            built.validation,
            $"Rebuild '{MapId}'");
    }

    [ContextMenu("Board Topology/Validate Serialized Index")]
    public void ValidateAndLog()
    {
        BoardTopologyValidationReport report =
            ValidateAgainstSources();
        LogValidation(report, $"Validate '{MapId}'");
    }

    public BoardTopologyValidationReport ValidateSerializedData()
    {
        var report = new BoardTopologyValidationReport();
        if (topologyVersion != CurrentTopologyVersion)
        {
            report.AddError(
                $"Versão serializada {topologyVersion}; esperada " +
                $"{CurrentTopologyVersion}.");
        }
        if (string.IsNullOrWhiteSpace(mapId))
            report.AddError("Map ID ausente.");
        if (string.IsNullOrWhiteSpace(topologyFingerprint))
            report.AddError("Fingerprint topológico ausente.");
        if (cells == null || cells.Count == 0)
            report.AddError("Índice não possui células.");

        var uniqueCells = new HashSet<Vector3Int>();
        if (cells != null)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                BoardTopologyCellRecord record = cells[i];
                if (record == null)
                {
                    report.AddError(
                        $"Registro de célula nulo no índice {i}.");
                    continue;
                }

                Vector3Int cell = record.cell;
                cell.z = 0;
                if (!uniqueCells.Add(cell))
                {
                    report.AddError(
                        $"Célula duplicada ({cell.x},{cell.y}).");
                }
                if (record.neighbors == null
                    || record.neighbors.Count > 6)
                {
                    report.AddError(
                        $"Célula ({cell.x},{cell.y}) possui uma lista " +
                        "inválida de vizinhos.");
                }
            }
        }

        if (routeEdges != null)
        {
            for (int i = 0; i < routeEdges.Count; i++)
            {
                BoardTopologyRouteEdgeRecord edge = routeEdges[i];
                if (edge == null)
                {
                    report.AddError(
                        $"Segmento de rota nulo no índice {i}.");
                    continue;
                }
                if (!uniqueCells.Contains(edge.EdgeKey.a)
                    || !uniqueCells.Contains(edge.EdgeKey.b))
                {
                    report.AddError(
                        $"Segmento '{edge.routeName}' referencia célula " +
                        "fora do índice.");
                }
            }
        }

        if (cells != null && routeEdges != null
            && !string.IsNullOrWhiteSpace(topologyFingerprint))
        {
            string serializedFingerprint =
                BoardTopologyIndexBuilder.ComputeFingerprint(
                    mapId,
                    cells,
                    routeEdges);
            if (!string.Equals(
                    serializedFingerprint,
                    topologyFingerprint,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    "Fingerprint serializado não corresponde ao conteúdo " +
                    "armazenado no componente.");
            }
        }
        return report;
    }

    public BoardTopologyValidationReport ValidateAgainstSources()
    {
        var report = ValidateSerializedData();
        AutoResolveSources();
        BoardTopologyBuildResult current =
            BoardTopologyIndexBuilder.Build(
                boardTilemap,
                terrainDatabase);
        report.Merge(current.validation);

        if (!string.IsNullOrWhiteSpace(current.fingerprint)
            && !string.Equals(
                current.fingerprint,
                topologyFingerprint,
                StringComparison.Ordinal))
        {
            report.AddError(
                "Índice desatualizado: o fingerprint das fontes atuais " +
                "difere do fingerprint serializado.");
        }
        return report;
    }

    public bool TryGetCell(
        Vector3Int cell,
        out BoardTopologyCellRecord record)
    {
        EnsureHydrated();
        cell.z = 0;
        return cellByPosition.TryGetValue(cell, out record);
    }

    public bool TryGetTerrain(
        Vector3Int cell,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (!TryGetCell(cell, out BoardTopologyCellRecord record))
            return false;
        terrain = record.terrain;
        return terrain != null;
    }

    public bool TryGetStructure(
        Vector3Int cell,
        out StructureData structure)
    {
        structure = null;
        if (!TryGetCell(cell, out BoardTopologyCellRecord record))
            return false;
        structure = record.structure;
        return structure != null;
    }

    public bool TryGetConstruction(
        Vector3Int cell,
        out ConstructionData construction)
    {
        construction = null;
        if (!TryGetCell(cell, out BoardTopologyCellRecord record))
            return false;
        construction = record.construction;
        return construction != null;
    }

    public IReadOnlyList<Vector3Int> GetNeighbors(Vector3Int cell)
    {
        return TryGetCell(cell, out BoardTopologyCellRecord record)
            && record.neighbors != null
                ? record.neighbors
                : EmptyCells;
    }

    public bool HasDeclaredRouteSegment(
        Vector3Int from,
        Vector3Int to)
    {
        EnsureHydrated();
        return structuresByRouteEdge.ContainsKey(
            new BoardTopologyEdgeKey(from, to));
    }

    public bool TryGetRouteStructures(
        Vector3Int from,
        Vector3Int to,
        out IReadOnlyList<StructureData> structures)
    {
        EnsureHydrated();
        if (structuresByRouteEdge.TryGetValue(
                new BoardTopologyEdgeKey(from, to),
                out List<StructureData> found))
        {
            structures = found;
            return true;
        }
        structures = EmptyStructures;
        return false;
    }

    public static bool TryGetFor(
        Tilemap tilemap,
        out BoardTopologyIndex index)
    {
        CleanupRegistry();
        for (int i = 0; i < ActiveIndices.Count; i++)
        {
            BoardTopologyIndex candidate = ActiveIndices[i];
            if (candidate == null)
                continue;
            if (IsCompatibleReference(
                    tilemap,
                    candidate.boardTilemap))
            {
                candidate.InitializeRuntime();
                index = candidate;
                return candidate.IsReady;
            }
        }
        index = null;
        return false;
    }

    public static BoardTopologyIndex GetOrCreateRuntime(
        Tilemap tilemap,
        TerrainDatabase database)
    {
        bool existingReady =
            TryGetFor(tilemap, out BoardTopologyIndex existing);
        if (existingReady || existing != null)
            return existing;
        if (!Application.isPlaying || tilemap == null)
            return null;

        GameObject host = new GameObject(
            "[BoardTopologyIndex Runtime Fallback]");
        host.SetActive(false);
        host.hideFlags = HideFlags.DontSave;
        SceneManager.MoveGameObjectToScene(
            host,
            tilemap.gameObject.scene);
        BoardTopologyIndex created =
            host.AddComponent<BoardTopologyIndex>();
        created.ConfigureSources(tilemap, database);
        host.SetActive(true);
        return created;
    }

    internal static BoardTopologyIndex EnsureForScene(Scene scene)
    {
        CleanupRegistry();
        for (int i = 0; i < ActiveIndices.Count; i++)
        {
            BoardTopologyIndex index = ActiveIndices[i];
            if (index != null && index.gameObject.scene == scene)
            {
                index.InitializeRuntime();
                return index.IsReady ? index : null;
            }
        }

        ResolveSourcesForScene(
            scene,
            out Tilemap tilemap,
            out TerrainDatabase database);
        return GetOrCreateRuntime(tilemap, database);
    }

    private void InitializeRuntime()
    {
        if (runtimeInitialized || !Application.isPlaying)
            return;
        runtimeInitialized = true;
        AutoResolveSources();

        BoardTopologyValidationReport serialized =
            ValidateSerializedData();
        if (!serialized.IsValid)
        {
            RebuildRuntimeFallback(
                "índice serializado ausente ou inválido");
            return;
        }

        Hydrate();
        if (!validateFingerprintAtRuntime)
            return;

        BoardTopologyBuildResult current =
            BoardTopologyIndexBuilder.Build(
                boardTilemap,
                terrainDatabase);
        if (current.validation.IsValid
            && string.Equals(
                current.fingerprint,
                topologyFingerprint,
                StringComparison.Ordinal))
        {
            return;
        }

        ApplyBuildResult(current, isRuntimeFallback: true);
        Debug.LogWarning(
            $"[BoardTopology] Índice de '{MapId}' estava desatualizado. " +
            "Foi reconstruído uma vez em memória; use a ferramenta de " +
            "Editor para persistir o resultado.",
            this);
        LogValidation(
            current.validation,
            $"Runtime fallback '{MapId}'");
    }

    private void RebuildRuntimeFallback(string reason)
    {
        AutoResolveSources();
        BoardTopologyBuildResult built =
            BoardTopologyIndexBuilder.Build(
                boardTilemap,
                terrainDatabase);
        ApplyBuildResult(built, isRuntimeFallback: true);
        if (IsReady)
        {
            Debug.LogWarning(
                $"[BoardTopology] Fallback de runtime para '{MapId}': " +
                $"{reason}. O mapa foi indexado uma única vez no load.",
                this);
        }
        LogValidation(
            built.validation,
            $"Runtime fallback '{MapId}'");
    }

    private void ApplyBuildResult(
        BoardTopologyBuildResult built,
        bool isRuntimeFallback)
    {
        topologyVersion = CurrentTopologyVersion;
        mapId = built != null ? built.mapId : string.Empty;
        topologyFingerprint =
            built != null ? built.fingerprint : string.Empty;
        cells = built != null
            ? built.cells
            : new List<BoardTopologyCellRecord>();
        routeEdges = built != null
            ? built.routeEdges
            : new List<BoardTopologyRouteEdgeRecord>();
        runtimeFallback = isRuntimeFallback;
        hydrated = false;
        Hydrate();
    }

    private void EnsureHydrated()
    {
        if (!hydrated)
            Hydrate();
    }

    private void Hydrate()
    {
        cellByPosition.Clear();
        structuresByRouteEdge.Clear();
        indexedCells.Clear();
        beachCells.Clear();
        coastalCells.Clear();
        potentialLandingCells.Clear();
        potentialEmbarkCells.Clear();
        potentialDisembarkCells.Clear();
        supplierConstructionCells.Clear();

        if (cells != null)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                BoardTopologyCellRecord record = cells[i];
                if (record == null)
                    continue;
                Vector3Int cell = record.cell;
                cell.z = 0;
                if (!cellByPosition.ContainsKey(cell))
                {
                    cellByPosition.Add(cell, record);
                    indexedCells.Add(cell);
                }
                if (record.isBeach)
                    beachCells.Add(cell);
                if (record.isCoastal)
                    coastalCells.Add(cell);
                if (record.isPotentialLandingSurface)
                    potentialLandingCells.Add(cell);
                if (record.isPotentialEmbarkCell)
                    potentialEmbarkCells.Add(cell);
                if (record.isPotentialDisembarkCell)
                    potentialDisembarkCells.Add(cell);
                if (record.construction != null
                    && record.construction.isSupplier
                    && (record.construction.supplierTier
                            == SupplierTier.Hub
                        || record.construction.supplierTier
                            == SupplierTier.Receiver))
                {
                    supplierConstructionCells.Add(cell);
                }
            }
        }

        if (routeEdges != null)
        {
            for (int i = 0; i < routeEdges.Count; i++)
            {
                BoardTopologyRouteEdgeRecord edge = routeEdges[i];
                if (edge == null)
                    continue;
                BoardTopologyEdgeKey key = edge.EdgeKey;
                if (!structuresByRouteEdge.TryGetValue(
                        key,
                        out List<StructureData> structures))
                {
                    structures = new List<StructureData>();
                    structuresByRouteEdge.Add(key, structures);
                }
                if (edge.structure != null
                    && !structures.Contains(edge.structure))
                {
                    structures.Add(edge.structure);
                }
            }
        }

        indexedCells.Sort(CompareLegacyCellTraversal);
        beachCells.Sort(CompareLegacyCellTraversal);
        coastalCells.Sort(CompareLegacyCellTraversal);
        potentialLandingCells.Sort(CompareLegacyCellTraversal);
        potentialEmbarkCells.Sort(CompareLegacyCellTraversal);
        potentialDisembarkCells.Sort(CompareLegacyCellTraversal);
        supplierConstructionCells.Sort(CompareLegacyCellTraversal);
        hydrated = true;
    }

    private static int CompareLegacyCellTraversal(
        Vector3Int left,
        Vector3Int right)
    {
        int z = left.z.CompareTo(right.z);
        if (z != 0)
            return z;
        int y = left.y.CompareTo(right.y);
        if (y != 0)
            return y;
        return left.x.CompareTo(right.x);
    }

    private void Register()
    {
        CleanupRegistry();
        if (!ActiveIndices.Contains(this))
            ActiveIndices.Add(this);
    }

    private static void CleanupRegistry()
    {
        for (int i = ActiveIndices.Count - 1; i >= 0; i--)
        {
            if (ActiveIndices[i] == null)
                ActiveIndices.RemoveAt(i);
        }
    }

    private static bool IsCompatibleReference(
        Tilemap requested,
        Tilemap indexed)
    {
        if (requested == null || indexed == null)
            return requested == indexed;
        if (requested == indexed)
            return true;
        return requested.gameObject.scene
                == indexed.gameObject.scene
            && requested.layoutGrid != null
            && requested.layoutGrid == indexed.layoutGrid;
    }

    private static void ResolveSourcesForScene(
        Scene scene,
        out Tilemap tilemap,
        out TerrainDatabase database)
    {
        tilemap = null;
        database = null;

        TurnStateManager[] turnManagers =
            UnityEngine.Object.FindObjectsByType<TurnStateManager>(
                FindObjectsInactive.Include);
        for (int i = 0; i < turnManagers.Length; i++)
        {
            TurnStateManager manager = turnManagers[i];
            if (manager == null || manager.gameObject.scene != scene)
                continue;
            tilemap = manager.MovementTilemapRef;
            database = manager.TerrainDatabaseRef;
            if (tilemap != null && database != null)
                return;
        }

        RoadNetworkManager[] roadNetworks =
            UnityEngine.Object.FindObjectsByType<RoadNetworkManager>(
                FindObjectsInactive.Include);
        for (int i = 0; i < roadNetworks.Length; i++)
        {
            RoadNetworkManager network = roadNetworks[i];
            if (network == null || network.gameObject.scene != scene)
                continue;
            if (tilemap == null)
                tilemap = network.BoardTilemap;
            if (database == null)
                database = network.TerrainDatabase;
            if (tilemap != null && database != null)
                return;
        }
    }

    private static void LogValidation(
        BoardTopologyValidationReport report,
        string title)
    {
        if (report == null)
            return;
        string message = report.Format(title);
        if (!report.IsValid)
            Debug.LogError(message);
        else if (report.warnings.Count > 0)
            Debug.LogWarning(message);
        else
            Debug.Log(message);
    }
}

internal static class BoardTopologyRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        if (Application.isPlaying)
            BoardTopologyIndex.EnsureForScene(scene);
    }
}
