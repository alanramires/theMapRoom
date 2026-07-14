using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeDetectarSensor
{
    private const int MaxCollectVisibleCellsCacheEntries = 1024;

    // Margem de rasante da LoS: um obstaculo so bloqueia se seu topo sobe ACIMA
    // da linha de visao por mais que essa folga. Sem isso, um obstaculo cujo topo
    // fica praticamente no nivel da linha (empate de float/geometria) bloqueia ou
    // nao de forma imprevisivel dependendo da direcao. Ex.: floresta EV 1 com a
    // linha a ~1.0 deve ser enxergada por cima.
    private const float LosGrazeEpsilon = 0.05f;

    private readonly struct CollectVisibleCellsCacheKey : IEquatable<CollectVisibleCellsCacheKey>
    {
        public readonly int observerInstanceId;
        public readonly int observerCellX;
        public readonly int observerCellY;
        public readonly int observerTeamId;
        public readonly int boardMapInstanceId;
        public readonly int terrainDatabaseInstanceId;
        public readonly int dpqConfigInstanceId;
        public readonly int maxRange;
        public readonly int observerLayerRangeFloor;
        public readonly int forcedDetectionRangeOverride;
        public readonly Domain forcedVirtualTargetDomain;
        public readonly HeightLevel forcedVirtualTargetHeight;
        public readonly bool enableLosValidation;
        public readonly bool enableSpotter;
        public readonly bool useOccupantLayerForTarget;
        public readonly bool preserveObserverLayerRangeForHexVisibility;
        public readonly bool forceVirtualTargetLayer;
        public readonly bool skipSpecializedTargetLayers;
        public readonly bool useRangeOnlyForAirHighWhenConfigured;
        public readonly int globalBoardRevision;
        public readonly int teamObserverRevision;

        public CollectVisibleCellsCacheKey(
            UnitManager observer,
            Vector3Int observerCell,
            Tilemap boardMap,
            TerrainDatabase terrainDatabase,
            DPQAirHeightConfig dpqAirHeightConfig,
            int maxRange,
            int observerLayerRangeFloor,
            bool enableLosValidation,
            bool enableSpotter,
            bool useOccupantLayerForTarget,
            bool preserveObserverLayerRangeForHexVisibility,
            bool forceVirtualTargetLayer,
            Domain forcedVirtualTargetDomain,
            HeightLevel forcedVirtualTargetHeight,
            int forcedDetectionRangeOverride,
            bool skipSpecializedTargetLayers,
            bool useRangeOnlyForAirHighWhenConfigured,
            int globalBoardRevision,
            int teamObserverRevision)
        {
            observerInstanceId = ResolveUnitCacheInstanceId(observer);
            observerCellX = observerCell.x;
            observerCellY = observerCell.y;
            observerTeamId = observer != null ? (int)observer.TeamId : -1;
            boardMapInstanceId = boardMap != null ? boardMap.GetEntityId().GetHashCode() : 0;
            terrainDatabaseInstanceId = terrainDatabase != null ? terrainDatabase.GetEntityId().GetHashCode() : 0;
            dpqConfigInstanceId = dpqAirHeightConfig != null ? dpqAirHeightConfig.GetEntityId().GetHashCode() : 0;
            this.maxRange = maxRange;
            this.observerLayerRangeFloor = observerLayerRangeFloor;
            this.forcedDetectionRangeOverride = forcedDetectionRangeOverride;
            this.forcedVirtualTargetDomain = forcedVirtualTargetDomain;
            this.forcedVirtualTargetHeight = forcedVirtualTargetHeight;
            this.enableLosValidation = enableLosValidation;
            this.enableSpotter = enableSpotter;
            this.useOccupantLayerForTarget = useOccupantLayerForTarget;
            this.preserveObserverLayerRangeForHexVisibility = preserveObserverLayerRangeForHexVisibility;
            this.forceVirtualTargetLayer = forceVirtualTargetLayer;
            this.skipSpecializedTargetLayers = skipSpecializedTargetLayers;
            this.useRangeOnlyForAirHighWhenConfigured = useRangeOnlyForAirHighWhenConfigured;
            this.globalBoardRevision = globalBoardRevision;
            this.teamObserverRevision = teamObserverRevision;
        }

        public bool Equals(CollectVisibleCellsCacheKey other)
        {
            return observerInstanceId == other.observerInstanceId
                && observerCellX == other.observerCellX
                && observerCellY == other.observerCellY
                && observerTeamId == other.observerTeamId
                && boardMapInstanceId == other.boardMapInstanceId
                && terrainDatabaseInstanceId == other.terrainDatabaseInstanceId
                && dpqConfigInstanceId == other.dpqConfigInstanceId
                && maxRange == other.maxRange
                && observerLayerRangeFloor == other.observerLayerRangeFloor
                && forcedDetectionRangeOverride == other.forcedDetectionRangeOverride
                && forcedVirtualTargetDomain == other.forcedVirtualTargetDomain
                && forcedVirtualTargetHeight == other.forcedVirtualTargetHeight
                && enableLosValidation == other.enableLosValidation
                && enableSpotter == other.enableSpotter
                && useOccupantLayerForTarget == other.useOccupantLayerForTarget
                && preserveObserverLayerRangeForHexVisibility == other.preserveObserverLayerRangeForHexVisibility
                && forceVirtualTargetLayer == other.forceVirtualTargetLayer
                && skipSpecializedTargetLayers == other.skipSpecializedTargetLayers
                && useRangeOnlyForAirHighWhenConfigured == other.useRangeOnlyForAirHighWhenConfigured
                && globalBoardRevision == other.globalBoardRevision
                && teamObserverRevision == other.teamObserverRevision;
        }

        public override bool Equals(object obj)
        {
            return obj is CollectVisibleCellsCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + observerInstanceId;
                hash = (hash * 31) + observerCellX;
                hash = (hash * 31) + observerCellY;
                hash = (hash * 31) + observerTeamId;
                hash = (hash * 31) + boardMapInstanceId;
                hash = (hash * 31) + terrainDatabaseInstanceId;
                hash = (hash * 31) + dpqConfigInstanceId;
                hash = (hash * 31) + maxRange;
                hash = (hash * 31) + observerLayerRangeFloor;
                hash = (hash * 31) + forcedDetectionRangeOverride;
                hash = (hash * 31) + (int)forcedVirtualTargetDomain;
                hash = (hash * 31) + (int)forcedVirtualTargetHeight;
                hash = (hash * 31) + (enableLosValidation ? 1 : 0);
                hash = (hash * 31) + (enableSpotter ? 1 : 0);
                hash = (hash * 31) + (useOccupantLayerForTarget ? 1 : 0);
                hash = (hash * 31) + (preserveObserverLayerRangeForHexVisibility ? 1 : 0);
                hash = (hash * 31) + (forceVirtualTargetLayer ? 1 : 0);
                hash = (hash * 31) + (skipSpecializedTargetLayers ? 1 : 0);
                hash = (hash * 31) + (useRangeOnlyForAirHighWhenConfigured ? 1 : 0);
                hash = (hash * 31) + globalBoardRevision;
                hash = (hash * 31) + teamObserverRevision;
                return hash;
            }
        }
    }

    private sealed class DistanceMapWorkspace
    {
        public readonly Dictionary<Vector3Int, int> distances = new Dictionary<Vector3Int, int>();
        public readonly Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        public readonly List<Vector3Int> neighbors = new List<Vector3Int>(6);
    }

    private readonly struct CubeCoord
    {
        public readonly float x;
        public readonly float y;
        public readonly float z;

        public CubeCoord(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    private readonly struct TerrainCellCacheKey : IEquatable<TerrainCellCacheKey>
    {
        public readonly int tilemapInstanceId;
        public readonly int x;
        public readonly int y;

        public TerrainCellCacheKey(int tilemapInstanceId, int x, int y)
        {
            this.tilemapInstanceId = tilemapInstanceId;
            this.x = x;
            this.y = y;
        }

        public bool Equals(TerrainCellCacheKey other)
        {
            return tilemapInstanceId == other.tilemapInstanceId &&
                x == other.x &&
                y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is TerrainCellCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + tilemapInstanceId;
                hash = (hash * 31) + x;
                hash = (hash * 31) + y;
                return hash;
            }
        }
    }

    private readonly struct LosCacheKey : IEquatable<LosCacheKey>
    {
        public readonly int boardMapInstanceId;
        public readonly int observerInstanceId;
        public readonly int observerCellX;
        public readonly int observerCellY;
        public readonly int targetCellX;
        public readonly int targetCellY;
        public readonly int detectionRange;
        public readonly Domain targetDomain;
        public readonly HeightLevel targetHeight;
        public readonly int terrainDatabaseInstanceId;
        public readonly int dpqConfigInstanceId;

        public LosCacheKey(
            Tilemap boardMap,
            UnitManager observer,
            Vector3Int observerCell,
            Vector3Int targetCell,
            int detectionRange,
            Domain targetDomain,
            HeightLevel targetHeight,
            TerrainDatabase terrainDatabase,
            DPQAirHeightConfig dpqAirHeightConfig)
        {
            boardMapInstanceId = boardMap != null ? boardMap.GetEntityId().GetHashCode() : 0;
            observerInstanceId = ResolveUnitCacheInstanceId(observer);
            observerCellX = observerCell.x;
            observerCellY = observerCell.y;
            targetCellX = targetCell.x;
            targetCellY = targetCell.y;
            this.detectionRange = detectionRange;
            this.targetDomain = targetDomain;
            this.targetHeight = targetHeight;
            terrainDatabaseInstanceId = terrainDatabase != null ? terrainDatabase.GetEntityId().GetHashCode() : 0;
            dpqConfigInstanceId = dpqAirHeightConfig != null ? dpqAirHeightConfig.GetEntityId().GetHashCode() : 0;
        }

        public bool Equals(LosCacheKey other)
        {
            return boardMapInstanceId == other.boardMapInstanceId &&
                observerInstanceId == other.observerInstanceId &&
                observerCellX == other.observerCellX &&
                observerCellY == other.observerCellY &&
                targetCellX == other.targetCellX &&
                targetCellY == other.targetCellY &&
                detectionRange == other.detectionRange &&
                targetDomain == other.targetDomain &&
                targetHeight == other.targetHeight &&
                terrainDatabaseInstanceId == other.terrainDatabaseInstanceId &&
                dpqConfigInstanceId == other.dpqConfigInstanceId;
        }

        public override bool Equals(object obj)
        {
            return obj is LosCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + boardMapInstanceId;
                hash = (hash * 31) + observerInstanceId;
                hash = (hash * 31) + observerCellX;
                hash = (hash * 31) + observerCellY;
                hash = (hash * 31) + targetCellX;
                hash = (hash * 31) + targetCellY;
                hash = (hash * 31) + detectionRange;
                hash = (hash * 31) + (int)targetDomain;
                hash = (hash * 31) + (int)targetHeight;
                hash = (hash * 31) + terrainDatabaseInstanceId;
                hash = (hash * 31) + dpqConfigInstanceId;
                return hash;
            }
        }
    }

    private static readonly Stack<DistanceMapWorkspace> distanceMapWorkspacePool = new Stack<DistanceMapWorkspace>(8);
    private static readonly Dictionary<int, Tilemap[]> gridTilemapsCache = new Dictionary<int, Tilemap[]>();
    private static readonly Dictionary<int, bool> tilemapOddRowOffsetCache = new Dictionary<int, bool>();
    private static readonly Dictionary<TerrainCellCacheKey, TerrainTypeData> terrainCacheForRefresh = new Dictionary<TerrainCellCacheKey, TerrainTypeData>(4096);
    private static readonly Dictionary<LosCacheKey, bool> losCacheForRefresh = new Dictionary<LosCacheKey, bool>(8192);
    private static readonly Dictionary<CollectVisibleCellsCacheKey, List<Vector3Int>> collectVisibleCellsCache = new Dictionary<CollectVisibleCellsCacheKey, List<Vector3Int>>(128);
    private static readonly List<Vector3Int> collectVisibleCellsScratch = new List<Vector3Int>(128);
    private static int collectVisibleCellsCacheRevision = int.MinValue;
    private static bool useLegacyLoSLerp = false;
    private static int debugCacheHits;
    private static int debugCacheMisses;
    private static int debugPoolRents;
    private static int debugPoolReleases;
    private static int debugFragataCollectWorkspaceRents;
    private static int debugFragataCollectWorkspaceReleases;

    public static void ResetFogDebugCounters()
    {
        debugCacheHits = 0;
        debugCacheMisses = 0;
        debugPoolRents = 0;
        debugPoolReleases = 0;
        debugFragataCollectWorkspaceRents = 0;
        debugFragataCollectWorkspaceReleases = 0;
    }

    public static void GetFogDebugCounters(
        out int cacheHits,
        out int cacheMisses,
        out int poolRents,
        out int poolReleases,
        out int fragataCollectWorkspaceRents,
        out int fragataCollectWorkspaceReleases)
    {
        cacheHits = debugCacheHits;
        cacheMisses = debugCacheMisses;
        poolRents = debugPoolRents;
        poolReleases = debugPoolReleases;
        fragataCollectWorkspaceRents = debugFragataCollectWorkspaceRents;
        fragataCollectWorkspaceReleases = debugFragataCollectWorkspaceReleases;
    }

    public static bool UseLegacyLoSLerp
    {
        get => useLegacyLoSLerp;
        set => useLegacyLoSLerp = value;
    }

    public static void ClearRefreshScopedTerrainCache()
    {
        terrainCacheForRefresh.Clear();
        losCacheForRefresh.Clear();
    }

    public static bool IsTargetObservedByTeam(
        UnitManager target,
        int viewerTeamId,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        bool enableSpotter = true,
        bool enableStealthValidation = true)
    {
        if (target == null || !target.gameObject.activeInHierarchy || target.IsEmbarked)
            return false;

        if ((int)target.TeamId == viewerTeamId)
            return true;

        Tilemap boardMap = map != null ? map : target.BoardTilemap;
        if (boardMap == null)
            return false;
        if (!IsUnitOnBoard(target, boardMap))
            return false;

        IReadOnlyList<UnitManager> units = GetUnitsForSensorQueries();
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager observer = units[i];
            if (observer == null || !observer.gameObject.activeInHierarchy || observer.IsEmbarked)
                continue;
            if ((int)observer.TeamId != viewerTeamId)
                continue;
            if (!IsUnitOnBoard(observer, boardMap))
                continue;

            if (CanObserverObserveTarget(
                    observer,
                    target,
                    boardMap,
                    terrainDatabase,
                    dpqAirHeightConfig,
                    enableLosValidation,
                    enableSpotter,
                    enableStealthValidation))
            {
                return true;
            }
        }

        return false;
    }

    // Uso de visibilidade direta para FoW/UI local: sem observador avancado.
    public static bool IsTargetObservedByTeamWithoutForwardObserver(
        UnitManager target,
        int viewerTeamId,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        bool enableStealthValidation = true)
    {
        return IsTargetObservedByTeam(
            target,
            viewerTeamId,
            map,
            terrainDatabase,
            dpqAirHeightConfig,
            enableLosValidation,
            enableSpotter: false,
            enableStealthValidation);
    }

    public static void CollectVisibleCells(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        ICollection<Vector3Int> visibleCellsOutput,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        bool enableSpotter = true,
        bool useOccupantLayerForTarget = true,
        bool preserveObserverLayerRangeForHexVisibility = false,
        bool forceVirtualTargetLayer = false,
        Domain forcedVirtualTargetDomain = Domain.Land,
        HeightLevel forcedVirtualTargetHeight = HeightLevel.Surface,
        int forcedDetectionRangeOverride = -1,
        bool skipSpecializedTargetLayers = false,
        bool useRangeOnlyForAirHighWhenConfigured = false)
    {
        if (visibleCellsOutput == null)
            return;

        if (observer == null || observer.IsEmbarked)
            return;


        Tilemap boardMap = map != null ? map : observer.BoardTilemap;
        if (boardMap == null)
            return;

        UnitData observerData = null;
        observer.TryGetUnitData(out observerData);
        Domain observerDomain = observer.GetDomain();
        HeightLevel observerHeight = observer.GetHeightLevel();
        if (forceVirtualTargetLayer && observerData != null &&
            observerData.ResolveVisionFor(forcedVirtualTargetDomain, forcedVirtualTargetHeight) <= 0)
        {
            return;
        }
        int observerLayerRangeFloor = preserveObserverLayerRangeForHexVisibility
            ? ResolveDetectionRange(observer, observerData, null, observerDomain, observerHeight)
            : 0;

        int maxRange = forcedDetectionRangeOverride >= 0
            ? Mathf.Max(0, forcedDetectionRangeOverride)
            : ResolveObserverMaxVisionRange(observerData, observer);
        if (maxRange <= 0)
            return;

        Vector3Int observerCell = observer.CurrentCellPosition;
        observerCell.z = 0;
        int globalBoardRevision = ThreatRevisionTracker.GlobalBoardRevision;
        int teamObserverRevision = observer != null ? ThreatRevisionTracker.GetTeamObserverRevision(observer.TeamId) : 0;
        InvalidateCollectVisibleCellsCacheIfNeeded(globalBoardRevision);

        CollectVisibleCellsCacheKey cacheKey = new CollectVisibleCellsCacheKey(
            observer,
            observerCell,
            boardMap,
            terrainDatabase,
            dpqAirHeightConfig,
            maxRange,
            observerLayerRangeFloor,
            enableLosValidation,
            enableSpotter,
            useOccupantLayerForTarget,
            preserveObserverLayerRangeForHexVisibility,
            forceVirtualTargetLayer,
            forcedVirtualTargetDomain,
            forcedVirtualTargetHeight,
            forcedDetectionRangeOverride,
            skipSpecializedTargetLayers,
            useRangeOnlyForAirHighWhenConfigured,
            globalBoardRevision,
            teamObserverRevision);

        if (TryAppendCollectVisibleCellsFromCache(cacheKey, visibleCellsOutput))
            return;

        bool isFragataCollect = IsDebugFragataObserver(observer);
        collectVisibleCellsScratch.Clear();
        collectVisibleCellsScratch.Add(observerCell);
        DistanceMapWorkspace workspace = RentDistanceMapWorkspace();
        DistanceMapWorkspace aquaticWorkspace = null;
        if (isFragataCollect)
            debugFragataCollectWorkspaceRents++;
        try
        {
            BuildDistanceMapInto(boardMap, observerCell, maxRange, workspace);
            // O modo agregado (All) consulta a camada nativa do terreno primeiro.
            // Mar costuma ser Naval/Surface com Submarine/Submerged adicional; portanto,
            // a especializacao submarina precisa conservar sua propria distancia conectada
            // e nao pode herdar a distancia hexagonal reta da superficie atravessando praia.
            if (!forceVirtualTargetLayer &&
                preserveObserverLayerRangeForHexVisibility &&
                terrainDatabase != null &&
                ResolveDetectionRange(
                    observer,
                    observerData,
                    null,
                    Domain.Submarine,
                    HeightLevel.Submerged) > 0)
            {
                aquaticWorkspace = RentDistanceMapWorkspace();
                BuildDistanceMapInto(
                    boardMap,
                    observerCell,
                    maxRange,
                    aquaticWorkspace,
                    c => IsAquaticCellForSubmergedDetection(boardMap, terrainDatabase, c));
            }

            foreach (KeyValuePair<Vector3Int, int> pair in workspace.distances)
            {
                Vector3Int cell = pair.Key;
                int distance = pair.Value;
                if (distance <= 0)
                    continue;
                Domain targetDomain;
                HeightLevel targetHeight;
                if (forceVirtualTargetLayer)
                {
                    targetDomain = forcedVirtualTargetDomain;
                    targetHeight = forcedVirtualTargetHeight;

                    // Uma camada virtual representa um filtro de exibicao, nao uma
                    // conversao do terreno. Naval/Surface, por exemplo, so pode
                    // contribuir em celulas que realmente aceitam Naval/Surface.
                    // Ar permanece independente do terreno abaixo.
                    if (targetDomain != Domain.Air &&
                        !CellSupportsObservationLayer(
                            boardMap,
                            terrainDatabase,
                            cell,
                            targetDomain,
                            targetHeight))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!TryResolveObservationTargetLayer(
                            boardMap,
                            terrainDatabase,
                            cell,
                            out targetDomain,
                            out targetHeight,
                            useOccupantLayerForTarget))
                    {
                        continue;
                    }
                }

                if (skipSpecializedTargetLayers &&
                    HasVisionSpecializationForLayer(observerData, targetDomain, targetHeight))
                {
                    continue;
                }

                int effectiveDistance = distance;
                bool useAquaticDistance = ShouldUseAquaticDistanceForDetection(targetDomain, targetHeight) && terrainDatabase != null;
                if (useAquaticDistance)
                {
                    if (aquaticWorkspace == null)
                    {
                        aquaticWorkspace = RentDistanceMapWorkspace();
                        BuildDistanceMapInto(
                            boardMap,
                            observerCell,
                            maxRange,
                            aquaticWorkspace,
                            c => IsAquaticCellForSubmergedDetection(boardMap, terrainDatabase, c));
                    }

                    if (!aquaticWorkspace.distances.TryGetValue(cell, out effectiveDistance))
                        continue;
                }

                if (!forceVirtualTargetLayer && preserveObserverLayerRangeForHexVisibility)
                {
                    if (CanObserveCellByAnyObserverVisionLayer(
                            observer,
                            observerData,
                            boardMap,
                            terrainDatabase,
                            dpqAirHeightConfig,
                            observerCell,
                            cell,
                            effectiveDistance,
                            targetDomain,
                            targetHeight,
                            enableLosValidation,
                            enableSpotter,
                            useRangeOnlyForAirHighWhenConfigured,
                            aquaticWorkspace))
                    {
                        collectVisibleCellsScratch.Add(cell);
                    }

                    continue;
                }

                int detectionRange = forcedDetectionRangeOverride >= 0
                    ? Mathf.Max(0, forcedDetectionRangeOverride)
                    : ResolveDetectionRange(observer, observerData, null, targetDomain, targetHeight);
                if (preserveObserverLayerRangeForHexVisibility && observerLayerRangeFloor > detectionRange)
                    detectionRange = observerLayerRangeFloor;
                if (effectiveDistance > detectionRange)
                    continue;

                bool effectiveLosValidation = ResolveEffectiveLosValidation(observerData, targetDomain, targetHeight, enableLosValidation);
                bool bypassLosByPolicy = !effectiveLosValidation;
                bool skipLosForCurrentTarget = observer.GetDomain() == Domain.Air &&
                    useRangeOnlyForAirHighWhenConfigured &&
                    IsAirHighRangeOnlyVision(dpqAirHeightConfig, targetDomain, targetHeight);
                bool hasDirectLos = skipLosForCurrentTarget || HasValidStraightObservationLine(
                    boardMap,
                    terrainDatabase,
                    observerCell,
                    cell,
                    observer,
                    null,
                    dpqAirHeightConfig,
                    out _,
                    out _,
                    out _,
                    enableLosValidation: true,
                    forcedTargetDomain: forceVirtualTargetLayer ? forcedVirtualTargetDomain : null,
                    forcedTargetHeightLevel: forceVirtualTargetLayer ? forcedVirtualTargetHeight : null);

                bool hasObservation = hasDirectLos || bypassLosByPolicy;
                if (!hasObservation)
                {
                    if (enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight))
                    {
                        hasObservation = TryFindForwardObserverForVirtualCell(
                            observer,
                            cell,
                            boardMap,
                            terrainDatabase,
                            dpqAirHeightConfig,
                            enableLosValidation: true);
                    }
                }

                if (hasObservation)
                    collectVisibleCellsScratch.Add(cell);
            }
        }
        finally
        {
            if (aquaticWorkspace != null)
                ReleaseDistanceMapWorkspace(aquaticWorkspace);
            ReleaseDistanceMapWorkspace(workspace);
            if (isFragataCollect)
                debugFragataCollectWorkspaceReleases++;
        }

        for (int i = 0; i < collectVisibleCellsScratch.Count; i++)
            visibleCellsOutput.Add(collectVisibleCellsScratch[i]);
        StoreCollectVisibleCellsInCache(cacheKey, collectVisibleCellsScratch);
    }

    // FoW individual: abre hexes apenas por visao direta da propria unidade.
    public static void CollectVisibleCellsForFogOfWar(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        ICollection<Vector3Int> visibleCellsOutput,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true)
    {
        CollectVisibleCells(
            observer,
            map,
            terrainDatabase,
            visibleCellsOutput,
            dpqAirHeightConfig,
            enableLosValidation,
            enableSpotter: false,
            useOccupantLayerForTarget: false,
            preserveObserverLayerRangeForHexVisibility: true,
            useRangeOnlyForAirHighWhenConfigured: true);
    }

    private static bool CanObserveCellByAnyObserverVisionLayer(
        UnitManager observer,
        UnitData observerData,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        Vector3Int observerCell,
        Vector3Int targetCell,
        int distance,
        Domain resolvedTargetDomain,
        HeightLevel resolvedTargetHeight,
        bool enableLosValidation,
        bool enableSpotter,
        bool useRangeOnlyForAirHighWhenConfigured,
        DistanceMapWorkspace aquaticWorkspace)
    {
        if (CanObserveCellWithLayer(
                observer,
                observerData,
                boardMap,
                terrainDatabase,
                dpqAirHeightConfig,
                observerCell,
                targetCell,
                distance,
                resolvedTargetDomain,
                resolvedTargetHeight,
                enableLosValidation,
                enableSpotter,
                useRangeOnlyForAirHighWhenConfigured))
        {
            return true;
        }

        if (observerData == null || observerData.visionSpecializations == null || observerData.visionSpecializations.Count <= 0)
            return false;


        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < observerData.visionSpecializations.Count; i++)
        {
            UnitVisionException specialization = observerData.visionSpecializations[i];
            if (specialization == null)
                continue;

            Domain domain = specialization.domain;
            HeightLevel height = specialization.heightLevel;
            int key = ((int)domain * 100) + (int)height;
            if (!seen.Add(key))
                continue;
            if (domain == resolvedTargetDomain && height == resolvedTargetHeight)
                continue;

            // Só permite cruzar tipos de terreno dentro da família aquática (Naval ↔ Submarine).
            // Impede que visão sub/submerged revele hexes de terra ou ar.
            bool specIsAquatic = domain == Domain.Submarine || domain == Domain.Naval;
            bool terrainIsAquatic = resolvedTargetDomain == Domain.Submarine || resolvedTargetDomain == Domain.Naval;
            if (specIsAquatic != terrainIsAquatic)
                continue;

            // Uma especializacao alternativa so pode contribuir se o proprio
            // TerrainTypeData do hex suportar essa camada. Sem isso, o alcance
            // Submarine/Submerged:7 era reaplicado sobre praia Naval/Surface e
            // fazia o modo All enxergar alem do Surface:3.
            if (!TryResolveTerrainAtCell(boardMap, terrainDatabase, targetCell, out TerrainTypeData targetTerrain)
                || !TerrainSupportsLayerMode(targetTerrain, domain, height))
            {
                continue;
            }

            int specializationDistance = distance;
            if (domain == Domain.Submarine && height == HeightLevel.Submerged)
            {
                if (aquaticWorkspace == null ||
                    !aquaticWorkspace.distances.TryGetValue(targetCell, out specializationDistance))
                {
                    continue;
                }
            }

            if (CanObserveCellWithLayer(
                    observer,
                    observerData,
                    boardMap,
                    terrainDatabase,
                    dpqAirHeightConfig,
                    observerCell,
                    targetCell,
                    specializationDistance,
                    domain,
                    height,
                    enableLosValidation,
                    enableSpotter,
                    useRangeOnlyForAirHighWhenConfigured))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanObserveCellWithLayer(
        UnitManager observer,
        UnitData observerData,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        Vector3Int observerCell,
        Vector3Int targetCell,
        int distance,
        Domain targetDomain,
        HeightLevel targetHeight,
        bool enableLosValidation,
        bool enableSpotter,
        bool useRangeOnlyForAirHighWhenConfigured)
    {
        int detectionRange = ResolveDetectionRange(observer, observerData, null, targetDomain, targetHeight);
        if (distance > detectionRange)
            return false;
        if (distance <= 1)
            return true;

        bool effectiveLosValidation = ResolveEffectiveLosValidation(observerData, targetDomain, targetHeight, enableLosValidation);
        bool bypassLosByPolicy = !effectiveLosValidation;
        bool skipLosForCurrentTarget = observer != null && observer.GetDomain() == Domain.Air &&
            useRangeOnlyForAirHighWhenConfigured &&
            IsAirHighRangeOnlyVision(dpqAirHeightConfig, targetDomain, targetHeight);
        bool hasDirectLos = skipLosForCurrentTarget ||
            TryGetDirectLosCachedForRefresh(
                boardMap,
                terrainDatabase,
                observerCell,
                targetCell,
                observer,
                dpqAirHeightConfig,
                targetDomain,
                targetHeight,
                detectionRange);

        if (hasDirectLos || bypassLosByPolicy)
            return true;

        if (enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight))
        {
            return TryFindForwardObserverForVirtualCell(
                observer,
                targetCell,
                boardMap,
                terrainDatabase,
                dpqAirHeightConfig,
                enableLosValidation: true);
        }

        return false;
    }

    private static bool HasVisionSpecializationForLayer(UnitData observerData, Domain targetDomain, HeightLevel targetHeight)
    {
        if (observerData == null || observerData.visionSpecializations == null)
            return false;

        for (int i = 0; i < observerData.visionSpecializations.Count; i++)
        {
            UnitVisionException entry = observerData.visionSpecializations[i];
            if (entry == null)
                continue;
            if (entry.domain == targetDomain && (entry.allHeights || entry.heightLevel == targetHeight))
                return true;
        }

        return false;
    }

    private static bool IsAirHighRangeOnlyVision(DPQAirHeightConfig dpqAirHeightConfig, Domain targetDomain, HeightLevel targetHeight)
    {
        if (targetDomain != Domain.Air || targetHeight != HeightLevel.AirHigh)
            return false;
        if (dpqAirHeightConfig == null)
            return false;
        if (!dpqAirHeightConfig.TryGetVisionFor(targetDomain, targetHeight, out _, out bool blockLoS))
            return false;

        return !blockLoS;
    }

    public static bool CollectDetection(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        List<PodeDetectarOption> detectedStealthOutput,
        List<PodeDetectarOption> undetectedStealthOutput,
        List<PodeDetectarOption> spottedCandidatesOutput,
        List<PodeDetectarOption> inRangeButLosBlockedOutput,
        out string reason,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        bool enableSpotter = true,
        bool enableStealthValidation = true)
    {
        reason = string.Empty;
        if (detectedStealthOutput == null || undetectedStealthOutput == null || spottedCandidatesOutput == null || inRangeButLosBlockedOutput == null)
        {
            reason = "Listas de output nao podem ser nulas.";
            return false;
        }

        detectedStealthOutput.Clear();
        undetectedStealthOutput.Clear();
        spottedCandidatesOutput.Clear();
        inRangeButLosBlockedOutput.Clear();

        if (observer == null)
        {
            reason = "Selecione uma unidade observadora.";
            return false;
        }

        if (observer.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode detectar.";
            return false;
        }

        Tilemap boardMap = map != null ? map : observer.BoardTilemap;
        if (boardMap == null)
        {
            reason = "Tilemap indisponivel para o scan.";
            return false;
        }
        if (!IsUnitOnBoard(observer, boardMap))
        {
            reason = "Observador fora do tilemap selecionado.";
            return false;
        }

        UnitData observerData = null;
        observer.TryGetUnitData(out observerData);

        int maxRange = ResolveObserverMaxVisionRange(observerData, observer);
        if (maxRange <= 0)
        {
            reason = "Observador sem alcance de visao valido.";
            return false;
        }

        Vector3Int observerCell = observer.CurrentCellPosition;
        observerCell.z = 0;
        DistanceMapWorkspace detectWorkspace = RentDistanceMapWorkspace();
        DistanceMapWorkspace aquaticDetectWorkspace = null;
        try
        {
            BuildDistanceMapInto(boardMap, observerCell, maxRange, detectWorkspace);
            Dictionary<Vector3Int, int> defaultDistanceMap = detectWorkspace.distances;

            IReadOnlyList<UnitManager> units = GetUnitsForSensorQueries();
            for (int i = 0; i < units.Count; i++)
            {
                UnitManager target = units[i];
                if (!IsEnemyTargetCandidate(observer, target, boardMap))
                    continue;

                UnitData targetData = null;
                target.TryGetUnitData(out targetData);
                bool isStealthTarget = targetData != null && targetData.IsStealthUnit(target.GetDomain(), target.GetHeightLevel());

                Domain targetDomain = target.GetDomain();
                HeightLevel targetHeight = target.GetHeightLevel();
                bool useAquaticDistance = ShouldUseAquaticDistanceForDetection(targetDomain, targetHeight) && terrainDatabase != null;
                Dictionary<Vector3Int, int> distanceMap = defaultDistanceMap;
                if (useAquaticDistance)
                {
                    if (aquaticDetectWorkspace == null)
                    {
                        aquaticDetectWorkspace = RentDistanceMapWorkspace();
                        BuildDistanceMapInto(
                            boardMap,
                            observerCell,
                            maxRange,
                            aquaticDetectWorkspace,
                            cell => IsAquaticCellForSubmergedDetection(boardMap, terrainDatabase, cell));
                    }

                    distanceMap = aquaticDetectWorkspace.distances;
                }

                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;
                if (!distanceMap.TryGetValue(targetCell, out int distance))
                    continue;

                int detectionRange = ResolveDetectionRange(observer, observerData, target, targetDomain, targetHeight);
                if (distance > detectionRange)
                    continue;

                List<Vector3Int> lineCells = new List<Vector3Int>();
                Vector3Int blockedCell = Vector3Int.zero;
                bool effectiveLosValidation = ResolveEffectiveLosValidation(observerData, targetDomain, targetHeight, enableLosValidation);
                bool bypassLosByPolicy = !effectiveLosValidation;
                bool skipLosForCurrentTarget = observer.GetDomain() == Domain.Air &&
                    IsAirHighRangeOnlyVision(dpqAirHeightConfig, targetDomain, targetHeight);
                bool hasDirectLos = skipLosForCurrentTarget || HasValidStraightObservationLine(
                    boardMap,
                    terrainDatabase,
                    observerCell,
                    targetCell,
                    observer,
                    target,
                    dpqAirHeightConfig,
                    out lineCells,
                    out _,
                    out blockedCell,
                    enableLosValidation: true);

                bool usedForwardObserver = false;
                UnitManager forwardObserver = null;
                bool canUseForwardObserver = enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight);
                if (!hasDirectLos)
                {
                    if (canUseForwardObserver)
                    {
                        List<UnitManager> forwardObservers = CollectForwardObserversForTarget(
                            observer,
                            target,
                            boardMap,
                            terrainDatabase,
                            dpqAirHeightConfig,
                            enableLosValidation: true);
                        if (forwardObservers.Count > 0)
                        {
                            usedForwardObserver = true;
                            forwardObserver = forwardObservers[0];
                        }
                        else if (HasControlledConstructionObserverForTarget(
                                     observer, target, boardMap, terrainDatabase, dpqAirHeightConfig, enableLosValidation: true))
                        {
                            usedForwardObserver = true;
                        }
                    }
                }

                bool hasObservation = hasDirectLos || bypassLosByPolicy || usedForwardObserver;
                if (!hasObservation)
                {
                    string rangeContext = useAquaticDistance ? " (distancia aquatica)" : string.Empty;
                    if (isStealthTarget)
                    {
                        undetectedStealthOutput.Add(new PodeDetectarOption
                        {
                            observerUnit = observer,
                            targetUnit = target,
                            observerCell = observerCell,
                            targetCell = targetCell,
                            distance = distance,
                            targetDomain = targetDomain,
                            targetHeightLevel = targetHeight,
                            detectionRangeUsed = detectionRange,
                            hasDirectLos = false,
                            usedForwardObserver = false,
                            forwardObserverUnit = null,
                            lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                            blockedCell = blockedCell,
                            reason = $"Furtiva no alcance{rangeContext}, mas nao detectada por falta de LOS."
                        });
                    }

                    inRangeButLosBlockedOutput.Add(new PodeDetectarOption
                    {
                        observerUnit = observer,
                        targetUnit = target,
                        observerCell = observerCell,
                        targetCell = targetCell,
                        distance = distance,
                        targetDomain = targetDomain,
                        targetHeightLevel = targetHeight,
                        detectionRangeUsed = detectionRange,
                        hasDirectLos = false,
                        usedForwardObserver = false,
                        forwardObserverUnit = null,
                        lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                        blockedCell = blockedCell,
                        reason = $"No alcance{rangeContext}, mas sem LOS."
                    });
                    continue;
                }

                if (isStealthTarget)
                {
                    if (!enableStealthValidation)
                    {
                        detectedStealthOutput.Add(new PodeDetectarOption
                        {
                            observerUnit = observer,
                            targetUnit = target,
                            observerCell = observerCell,
                            targetCell = targetCell,
                            distance = distance,
                            targetDomain = targetDomain,
                            targetHeightLevel = targetHeight,
                            detectionRangeUsed = detectionRange,
                            hasDirectLos = hasDirectLos,
                            usedForwardObserver = usedForwardObserver,
                            forwardObserverUnit = forwardObserver,
                            lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                            blockedCell = blockedCell,
                            reason = "Detectado (Stealth validation desativada no Game Setup)."
                        });
                        continue;
                    }

                    bool canDetectStealth = usedForwardObserver ||
                        (observerData != null && observerData.CanDetectStealthFor(targetDomain, targetHeight, targetData));
                    if (!canDetectStealth)
                    {
                        undetectedStealthOutput.Add(new PodeDetectarOption
                        {
                            observerUnit = observer,
                            targetUnit = target,
                            observerCell = observerCell,
                            targetCell = targetCell,
                            distance = distance,
                            targetDomain = targetDomain,
                            targetHeightLevel = targetHeight,
                            detectionRangeUsed = detectionRange,
                            hasDirectLos = hasDirectLos,
                            usedForwardObserver = usedForwardObserver,
                            forwardObserverUnit = forwardObserver,
                            lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                            blockedCell = blockedCell,
                            reason = "Furtiva no alcance/visao, mas sem especializacao de deteccao stealth."
                        });
                        continue;
                    }

                    string stealthDetectionReason = ResolveStealthDetectionReason(
                        observerData,
                        targetData,
                        targetDomain,
                        targetHeight);
                    string rangeContext = useAquaticDistance ? " com distancia aquatica" : string.Empty;
                    string observationModeReason = bypassLosByPolicy
                        ? $"com LOS ignorada pela policy da visao especializada{rangeContext}"
                        : usedForwardObserver
                        ? $"via observador avancado{rangeContext}"
                        : $"com LOS direta{rangeContext}";
                    string detectedReason = string.IsNullOrWhiteSpace(stealthDetectionReason)
                        ? $"Detectado stealth {observationModeReason}."
                        : $"{stealthDetectionReason} ({observationModeReason}).";

                    detectedStealthOutput.Add(new PodeDetectarOption
                    {
                        observerUnit = observer,
                        targetUnit = target,
                        observerCell = observerCell,
                        targetCell = targetCell,
                        distance = distance,
                        targetDomain = targetDomain,
                        targetHeightLevel = targetHeight,
                        detectionRangeUsed = detectionRange,
                        hasDirectLos = hasDirectLos,
                        usedForwardObserver = usedForwardObserver,
                        forwardObserverUnit = forwardObserver,
                        lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                        blockedCell = blockedCell,
                        reason = detectedReason
                    });
                    continue;
                }

                spottedCandidatesOutput.Add(new PodeDetectarOption
                {
                    observerUnit = observer,
                    targetUnit = target,
                    observerCell = observerCell,
                    targetCell = targetCell,
                    distance = distance,
                    targetDomain = targetDomain,
                    targetHeightLevel = targetHeight,
                    detectionRangeUsed = detectionRange,
                    hasDirectLos = hasDirectLos,
                    usedForwardObserver = usedForwardObserver,
                    forwardObserverUnit = forwardObserver,
                    lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                    blockedCell = blockedCell,
                    reason = useAquaticDistance
                        ? (usedForwardObserver ? "Avistado via observador avancado com distancia aquatica." : "Avistado com LOS direta e distancia aquatica.")
                        : (usedForwardObserver ? "Avistado via observador avancado." : "Avistado com LOS direta.")
                });
            }
        }
        finally
        {
            if (aquaticDetectWorkspace != null)
                ReleaseDistanceMapWorkspace(aquaticDetectWorkspace);
            ReleaseDistanceMapWorkspace(detectWorkspace);
        }

        reason = $"FurtivasDetectadas={detectedStealthOutput.Count} | FurtivasNaoDetectadas={undetectedStealthOutput.Count} | Avistadas={spottedCandidatesOutput.Count} | SemLOS={inRangeButLosBlockedOutput.Count}";

        return detectedStealthOutput.Count > 0 || spottedCandidatesOutput.Count > 0;
    }

    public static bool TryGetObservationLineDebug(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int targetCell,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation,
        Domain? forcedTargetDomain,
        HeightLevel? forcedTargetHeightLevel,
        out float finalReachedEv,
        out float losHeightAtBlockedCell,
        out float blockedCellEv,
        out Vector3Int blockedCell,
        out float losHeightAtStrongestPassedCell,
        out float strongestPassedCellEv,
        out Vector3Int strongestPassedCell,
        out List<float> lineRiseHeights)
    {
        finalReachedEv = 0f;
        losHeightAtBlockedCell = 0f;
        blockedCellEv = 0f;
        blockedCell = Vector3Int.zero;
        losHeightAtStrongestPassedCell = 0f;
        strongestPassedCellEv = 0f;
        strongestPassedCell = Vector3Int.zero;
        lineRiseHeights = new List<float>();

        if (observer == null)
            return false;

        Tilemap boardMap = map != null ? map : observer.BoardTilemap;
        if (boardMap == null)
            return false;

        Vector3Int originCell = observer.CurrentCellPosition;
        originCell.z = 0;
        targetCell.z = 0;

        bool hasDirectLos = HasValidStraightObservationLine(
            boardMap,
            terrainDatabase,
            originCell,
            targetCell,
            observer,
            null,
            dpqAirHeightConfig,
            out List<Vector3Int> intermediateCells,
            out List<float> evPath,
            out blockedCell,
            enableLosValidation: enableLosValidation,
            forcedTargetDomain: forcedTargetDomain,
            forcedTargetHeightLevel: forcedTargetHeightLevel);

        if (evPath != null && evPath.Count > 0)
        {
            finalReachedEv = evPath[evPath.Count - 1];
            lineRiseHeights.AddRange(evPath);
        }
        losHeightAtBlockedCell = finalReachedEv;

        if (blockedCell != Vector3Int.zero && intermediateCells != null && evPath != null)
        {
            int blockedIndex = intermediateCells.IndexOf(blockedCell);
            if (blockedIndex >= 0)
            {
                int evPathIndex = blockedIndex + 1; // +1 because index 0 is origin EV.
                if (evPathIndex >= 0 && evPathIndex < evPath.Count)
                    losHeightAtBlockedCell = evPath[evPathIndex];
            }

            if (TryResolveCellVision(
                    boardMap,
                    terrainDatabase,
                    blockedCell,
                    null,
                    dpqAirHeightConfig,
                    out float resolvedBlockedEv,
                    out _,
                    forcedDomain: forcedTargetDomain,
                    forcedHeightLevel: forcedTargetHeightLevel))
            {
                blockedCellEv = resolvedBlockedEv;
            }
        }

        if (intermediateCells != null && evPath != null)
        {
            for (int i = 0; i < intermediateCells.Count; i++)
            {
                Vector3Int cell = intermediateCells[i];
                int evPathIndex = i + 1; // index 0 is origin EV
                if (evPathIndex < 0 || evPathIndex >= evPath.Count)
                    continue;

                if (!TryResolveCellVision(
                        boardMap,
                        terrainDatabase,
                        cell,
                        null,
                        dpqAirHeightConfig,
                        out float cellEv,
                        out bool cellBlocksLoS,
                        forcedDomain: forcedTargetDomain,
                        forcedHeightLevel: forcedTargetHeightLevel))
                {
                    continue;
                }

                if (cellEv <= 0)
                    continue;

                float losHeightAtCell = evPath[evPathIndex];
                if (cellEv > losHeightAtCell + LosGrazeEpsilon)
                    continue;

                if (cellEv > strongestPassedCellEv)
                {
                    strongestPassedCellEv = cellEv;
                    strongestPassedCell = cell;
                    losHeightAtStrongestPassedCell = losHeightAtCell;
                }
            }
        }

        return hasDirectLos;
    }

    private static int ResolveObserverMaxVisionRange(UnitData observerData, UnitManager observer)
    {
        int maxRange = observerData != null ? Mathf.Max(1, observerData.visao) : Mathf.Max(1, observer.Visao);
        if (observerData == null || observerData.visionSpecializations == null)
            return maxRange;

        for (int i = 0; i < observerData.visionSpecializations.Count; i++)
        {
            UnitVisionException entry = observerData.visionSpecializations[i];
            if (entry == null)
                continue;

            int value = Mathf.Max(0, entry.vision);
            if (value > maxRange)
                maxRange = value;
        }

        return maxRange;
    }

    private static int ResolveDetectionRange(
        UnitManager observer,
        UnitData observerData,
        UnitManager target,
        Domain targetDomain,
        HeightLevel targetHeight)
    {
        if (observerData != null)
        {
            return Mathf.Max(0, observerData.ResolveVisionFor(targetDomain, targetHeight));
        }
        if (observer != null)
            return Mathf.Max(1, observer.Visao);

        return 1;
    }

    private static bool ShouldUseForwardObserverRule(Domain domain, HeightLevel heightLevel)
    {
        return (domain == Domain.Land && heightLevel == HeightLevel.Surface) ||
            (domain == Domain.Naval && heightLevel == HeightLevel.Surface) ||
            (domain == Domain.Submarine && heightLevel == HeightLevel.Submerged);
    }

    private static List<UnitManager> CollectForwardObserversForTarget(
        UnitManager observer,
        UnitManager target,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation)
    {
        List<UnitManager> observers = new List<UnitManager>();
        if (observer == null || target == null || map == null)
            return observers;
        if (!IsUnitOnBoard(observer, map) || !IsUnitOnBoard(target, map))
            return observers;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        int maxObserverRange = GetTeamMaxObservationRangeHexes(observer, target, map);
        DistanceMapWorkspace observersWorkspace = RentDistanceMapWorkspace();
        try
        {
            BuildDistanceMapInto(map, targetCell, maxObserverRange, observersWorkspace);
            Dictionary<Vector3Int, int> localAroundTarget = observersWorkspace.distances;
            if (localAroundTarget.Count == 0)
                return observers;

            IReadOnlyList<UnitManager> units = GetUnitsForSensorQueries();
            for (int i = 0; i < units.Count; i++)
            {
                UnitManager ally = units[i];
                if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                    continue;
                if (ally.TeamId != observer.TeamId)
                    continue;
                if (ally == observer)
                    continue;
                if (!IsUnitOnBoard(ally, map))
                    continue;

                Vector3Int allyCell = ally.CurrentCellPosition;
                allyCell.z = 0;
                if (!localAroundTarget.TryGetValue(allyCell, out int allyDistanceToTarget))
                    continue;

            int allyObservationRange = GetObservationRangeHexes(ally, target);
            if (allyDistanceToTarget > allyObservationRange)
                continue;

            if (!CanForwardObserverDetectTarget(ally, target))
                continue;

                if (!HasValidStraightObservationLine(
                        map,
                        terrainDatabase,
                        allyCell,
                        targetCell,
                        ally,
                        target,
                        dpqAirHeightConfig,
                        out _,
                        out _,
                        out _,
                        enableLosValidation))
                {
                    continue;
                }

                if (!observers.Contains(ally))
                    observers.Add(ally);
            }
        }
        finally
        {
            ReleaseDistanceMapWorkspace(observersWorkspace);
        }

        return observers;
    }

    private static bool CanForwardObserverDetectTarget(UnitManager observer, UnitManager target)
    {
        if (observer == null || target == null)
            return false;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null ||
            !targetData.IsStealthUnit(target.GetDomain(), target.GetHeightLevel()))
            return true;
        if (!observer.TryGetUnitData(out UnitData observerData) || observerData == null)
            return false;
        return observerData.CanDetectStealthFor(target.GetDomain(), target.GetHeightLevel(), targetData);
    }

    private static bool HasControlledConstructionObserverForTarget(
        UnitManager observer,
        UnitManager target,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation)
    {
        if (observer == null || target == null || map == null)
            return false;
        if (target.TryGetUnitData(out UnitData targetData) && targetData != null &&
            targetData.IsStealthUnit(target.GetDomain(), target.GetHeightLevel()))
            return false;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        IReadOnlyList<ConstructionManager> constructions = ConstructionManager.AllActive;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if (construction.TeamId == TeamId.Neutral || construction.TeamId != observer.TeamId)
                continue;
            if (construction.BoardTilemap != map || !construction.TryResolveConstructionData(out ConstructionData data) || data == null)
                continue;

            Vector3Int constructionCell = construction.CurrentCellPosition;
            constructionCell.z = 0;
            int range = Mathf.Max(0, data.visao);
            DistanceMapWorkspace workspace = RentDistanceMapWorkspace();
            try
            {
                BuildDistanceMapInto(map, constructionCell, range, workspace);
                if (!workspace.distances.TryGetValue(targetCell, out int distance) || distance > range)
                    continue;
                if (distance == 0 || HasValidStraightObservationLine(
                        map, terrainDatabase, constructionCell, targetCell, null, target,
                        dpqAirHeightConfig, out _, out _, out _, enableLosValidation))
                    return true;
            }
            finally
            {
                ReleaseDistanceMapWorkspace(workspace);
            }
        }

        return false;
    }

    private static int GetObservationRangeHexes(UnitManager unit, UnitManager target)
    {
        if (target != null && unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return Mathf.Max(0, data.ResolveVisionFor(target.GetDomain(), target.GetHeightLevel()));
        if (unit != null)
            return Mathf.Max(1, unit.Visao);

        return 1;
    }

    private static int GetObservationRangeHexes(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return Mathf.Max(0, data.ResolveVisionFor(unit.GetDomain(), unit.GetHeightLevel()));
        if (unit != null)
            return Mathf.Max(1, unit.Visao);

        return 1;
    }

    private static int GetTeamMaxObservationRangeHexes(UnitManager referenceUnit, UnitManager target, Tilemap boardMap)
    {
        if (referenceUnit == null || boardMap == null)
            return 1;

        int maxRange = GetObservationRangeHexes(referenceUnit, target);
        IReadOnlyList<UnitManager> units = GetUnitsForSensorQueries();
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != referenceUnit.TeamId)
                continue;
            if (!IsUnitOnBoard(ally, boardMap))
                continue;

            int allyRange = GetObservationRangeHexes(ally, target);
            if (allyRange > maxRange)
                maxRange = allyRange;
        }

        return Mathf.Max(1, maxRange);
    }

    private static int GetTeamMaxObservationRangeHexes(UnitManager referenceUnit, Tilemap boardMap)
    {
        if (referenceUnit == null || boardMap == null)
            return 1;

        int maxRange = GetObservationRangeHexes(referenceUnit);
        IReadOnlyList<UnitManager> units = GetUnitsForSensorQueries();
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != referenceUnit.TeamId)
                continue;
            if (!IsUnitOnBoard(ally, boardMap))
                continue;

            int allyRange = GetObservationRangeHexes(ally);
            if (allyRange > maxRange)
                maxRange = allyRange;
        }

        return Mathf.Max(1, maxRange);
    }

    private static bool TryFindForwardObserverForVirtualCell(
        UnitManager observer,
        Vector3Int targetCell,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation)
    {
        if (observer == null || map == null)
            return false;
        if (!IsUnitOnBoard(observer, map))
            return false;

        targetCell.z = 0;
        int maxObserverRange = GetTeamMaxObservationRangeHexes(observer, map);
        DistanceMapWorkspace observerWorkspace = RentDistanceMapWorkspace();
        try
        {
            BuildDistanceMapInto(map, targetCell, maxObserverRange, observerWorkspace);
            Dictionary<Vector3Int, int> localAroundTarget = observerWorkspace.distances;
            if (localAroundTarget.Count == 0)
                return false;

            IReadOnlyList<UnitManager> units = GetUnitsForSensorQueries();
            for (int i = 0; i < units.Count; i++)
            {
                UnitManager ally = units[i];
                if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                    continue;
                if (ally.TeamId != observer.TeamId)
                    continue;
                if (!IsUnitOnBoard(ally, map))
                    continue;

                Vector3Int allyCell = ally.CurrentCellPosition;
                allyCell.z = 0;
                if (!localAroundTarget.TryGetValue(allyCell, out int allyDistanceToTarget))
                    continue;

                int allyObservationRange = GetObservationRangeHexes(ally);
                if (allyDistanceToTarget > allyObservationRange)
                    continue;

                if (HasValidStraightObservationLine(
                        map,
                        terrainDatabase,
                        allyCell,
                        targetCell,
                        ally,
                        null,
                        dpqAirHeightConfig,
                        out _,
                        out _,
                        out _,
                        enableLosValidation))
                {
                    return true;
                }
            }
        }
        finally
        {
            ReleaseDistanceMapWorkspace(observerWorkspace);
        }

        return false;
    }

    private static bool IsEnemyTargetCandidate(UnitManager observer, UnitManager target, Tilemap boardMap)
    {
        if (observer == null || target == null)
            return false;
        if (target == observer)
            return false;
        if (!target.gameObject.activeInHierarchy || target.IsEmbarked)
            return false;
        if (!IsUnitOnBoard(observer, boardMap) || !IsUnitOnBoard(target, boardMap))
            return false;

        return observer.TeamId != target.TeamId;
    }

    private static IReadOnlyList<UnitManager> GetUnitsForSensorQueries()
    {
        if (UnitManager.AllActive != null && UnitManager.AllActive.Count > 0)
            return UnitManager.AllActive;

        UnitManager[] fallback = UnityEngine.Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return fallback ?? System.Array.Empty<UnitManager>();
    }

    private static bool ResolveEffectiveLosValidation(
        UnitData observerData,
        Domain targetDomain,
        HeightLevel targetHeight,
        bool enableLosValidationGlobal)
    {
        if (observerData == null)
            return enableLosValidationGlobal;

        return observerData.ResolveLosValidationFor(targetDomain, targetHeight, enableLosValidationGlobal);
    }

    private static bool ShouldUseAquaticDistanceForDetection(Domain targetDomain, HeightLevel targetHeight)
    {
        return targetDomain == Domain.Submarine && targetHeight == HeightLevel.Submerged;
    }

    private static bool IsAquaticCellForSubmergedDetection(Tilemap map, TerrainDatabase terrainDatabase, Vector3Int cell)
    {
        if (map == null || terrainDatabase == null)
            return true;

        if (!TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
            return false;

        // Fundo do mar nao e sinonimo de todo terreno Naval/Surface. Praia, por
        // exemplo, aceita navegacao na superficie mas nao Submarine/Submerged.
        // A propria lista data-driven do terreno decide se o pulso submarino
        // pode atravessar e revelar a celula.
        return TerrainSupportsLayerMode(terrain, Domain.Submarine, HeightLevel.Submerged);
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

    private static bool CellSupportsObservationLayer(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        Domain domain,
        HeightLevel height)
    {
        if (TryResolveConstructionAtCell(map, cell, out ConstructionData construction) &&
            construction != null &&
            SupportsLayerMode(
                construction.domain,
                construction.heightLevel,
                construction.aditionalDomainsAllowed,
                domain,
                height))
        {
            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(map, cell);
        if (structure != null &&
            SupportsLayerMode(
                structure.domain,
                structure.heightLevel,
                structure.aditionalDomainsAllowed,
                domain,
                height))
        {
            return true;
        }

        return TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) &&
            TerrainSupportsLayerMode(terrain, domain, height);
    }

    private static bool SupportsLayerMode(
        Domain nativeDomain,
        HeightLevel nativeHeight,
        IReadOnlyList<TerrainLayerMode> additionalModes,
        Domain domain,
        HeightLevel height)
    {
        if (nativeDomain == domain && nativeHeight == height)
            return true;

        if (additionalModes == null)
            return false;

        for (int i = 0; i < additionalModes.Count; i++)
        {
            TerrainLayerMode mode = additionalModes[i];
            if (mode.domain == domain && mode.heightLevel == height)
                return true;
        }

        return false;
    }

    private static bool CanObserverObserveTarget(
        UnitManager observer,
        UnitManager target,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation,
        bool enableSpotter,
        bool enableStealthValidation)
    {
        if (observer == null || target == null || boardMap == null)
            return false;
        if (!IsUnitOnBoard(observer, boardMap) || !IsUnitOnBoard(target, boardMap))
            return false;

        UnitData observerData = null;
        observer.TryGetUnitData(out observerData);
        UnitData targetData = null;
        target.TryGetUnitData(out targetData);

        Vector3Int observerCell = observer.CurrentCellPosition;
        observerCell.z = 0;
        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;

        Domain targetDomain = target.GetDomain();
        HeightLevel targetHeight = target.GetHeightLevel();
        int detectionRange = ResolveDetectionRange(observer, observerData, target, targetDomain, targetHeight);
        bool useAquaticDistance = ShouldUseAquaticDistanceForDetection(targetDomain, targetHeight) && terrainDatabase != null;

        DistanceMapWorkspace observeWorkspace = RentDistanceMapWorkspace();
        int distance;
        try
        {
            BuildDistanceMapInto(
                boardMap,
                observerCell,
                detectionRange,
                observeWorkspace,
                useAquaticDistance
                    ? cell => IsAquaticCellForSubmergedDetection(boardMap, terrainDatabase, cell)
                    : null);
            Dictionary<Vector3Int, int> distanceMap = observeWorkspace.distances;
            if (!distanceMap.TryGetValue(targetCell, out distance))
                return false;
            if (distance > detectionRange)
                return false;
        }
        finally
        {
            ReleaseDistanceMapWorkspace(observeWorkspace);
        }

        bool effectiveLosValidation = ResolveEffectiveLosValidation(observerData, targetDomain, targetHeight, enableLosValidation);
        bool bypassLosByPolicy = !effectiveLosValidation;
        bool skipLosForCurrentTarget = observer.GetDomain() == Domain.Air &&
            IsAirHighRangeOnlyVision(dpqAirHeightConfig, targetDomain, targetHeight);
        bool hasDirectLos = skipLosForCurrentTarget || HasValidStraightObservationLine(
            boardMap,
            terrainDatabase,
            observerCell,
            targetCell,
            observer,
            target,
            dpqAirHeightConfig,
            out _,
            out _,
            out _,
            enableLosValidation: true);

        bool usedForwardObserver = false;
        if (!hasDirectLos)
        {
            if (enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight))
            {
                List<UnitManager> forwardObservers = CollectForwardObserversForTarget(
                    observer,
                    target,
                    boardMap,
                    terrainDatabase,
                    dpqAirHeightConfig,
                    enableLosValidation: true);
                usedForwardObserver = forwardObservers.Count > 0 ||
                    HasControlledConstructionObserverForTarget(
                        observer, target, boardMap, terrainDatabase, dpqAirHeightConfig, enableLosValidation: true);
            }
        }

        bool hasObservation = hasDirectLos || bypassLosByPolicy || usedForwardObserver;
        if (!hasObservation)
            return false;

        bool isStealthTarget = targetData != null && targetData.IsStealthUnit(targetDomain, targetHeight);
        if (!isStealthTarget || !enableStealthValidation)
            return true;

        if (usedForwardObserver)
            return true;

        return observerData != null && observerData.CanDetectStealthFor(targetDomain, targetHeight, targetData);
    }

    private static bool IsUnitOnBoard(UnitManager unit, Tilemap boardMap)
    {
        if (unit == null || boardMap == null)
            return false;

        if (unit.BoardTilemap == null || unit.BoardTilemap != boardMap)
            return false;

        return unit.gameObject.scene == boardMap.gameObject.scene;
    }

    private static string ResolveStealthDetectionReason(
        UnitData observerData,
        UnitData targetData,
        Domain targetDomain,
        HeightLevel targetHeight)
    {
        if (observerData == null)
            return string.Empty;

        UnitVisionException specialization = FindVisionSpecialization(observerData, targetDomain, targetHeight);
        if (specialization == null)
            return string.Empty;

        if (targetData == null || specialization.detectUnitsWithFollowingSkills == null || specialization.detectUnitsWithFollowingSkills.Count == 0)
            return string.Empty;

        List<SkillData> targetStealthSkills = targetData.ResolveStealthSkillsForDetection(targetDomain, targetHeight);
        if (targetStealthSkills == null || targetStealthSkills.Count == 0)
            return string.Empty;

        if (!TryGetFirstMatchingSkill(specialization.detectUnitsWithFollowingSkills, targetStealthSkills, out SkillData matchedSkill))
            return string.Empty;

        string skillName = ResolveSkillName(matchedSkill);
        return string.IsNullOrWhiteSpace(skillName)
            ? "Detectado via skill da visao especializada"
            : $"Detectado via skill '{skillName}' da visao especializada";
    }

    private static UnitVisionException FindVisionSpecialization(UnitData observerData, Domain targetDomain, HeightLevel targetHeight)
    {
        if (observerData == null || observerData.visionSpecializations == null)
            return null;

        for (int i = 0; i < observerData.visionSpecializations.Count; i++)
        {
            UnitVisionException entry = observerData.visionSpecializations[i];
            if (entry == null)
                continue;
            if (entry.domain != targetDomain || entry.heightLevel != targetHeight)
                continue;

            return entry;
        }

        return null;
    }

    private static bool TryGetFirstMatchingSkill(
        List<SkillData> detectorSkills,
        List<SkillData> targetSkills,
        out SkillData matchedSkill)
    {
        matchedSkill = null;
        if (detectorSkills == null || targetSkills == null)
            return false;

        for (int i = 0; i < detectorSkills.Count; i++)
        {
            SkillData detectorSkill = detectorSkills[i];
            if (detectorSkill == null)
                continue;

            string detectorId = string.IsNullOrWhiteSpace(detectorSkill.id) ? string.Empty : detectorSkill.id.Trim();
            for (int j = 0; j < targetSkills.Count; j++)
            {
                SkillData targetSkill = targetSkills[j];
                if (targetSkill == null)
                    continue;

                if (ReferenceEquals(detectorSkill, targetSkill))
                {
                    matchedSkill = detectorSkill;
                    return true;
                }

                string targetId = string.IsNullOrWhiteSpace(targetSkill.id) ? string.Empty : targetSkill.id.Trim();
                if (detectorId.Length > 0 && targetId.Length > 0 &&
                    string.Equals(detectorId, targetId, System.StringComparison.OrdinalIgnoreCase))
                {
                    matchedSkill = detectorSkill;
                    return true;
                }
            }
        }

        return false;
    }

    private static string ResolveSkillName(SkillData skill)
    {
        if (skill == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(skill.displayName))
            return skill.displayName.Trim();
        if (!string.IsNullOrWhiteSpace(skill.id))
            return skill.id.Trim();
        return skill.name;
    }

    private static bool TryGetDirectLosCachedForRefresh(
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int observerCell,
        Vector3Int targetCell,
        UnitManager observer,
        DPQAirHeightConfig dpqAirHeightConfig,
        Domain targetDomain,
        HeightLevel targetHeight,
        int detectionRange)
    {
        LosCacheKey cacheKey = new LosCacheKey(
            boardMap,
            observer,
            observerCell,
            targetCell,
            detectionRange,
            targetDomain,
            targetHeight,
            terrainDatabase,
            dpqAirHeightConfig);

        if (losCacheForRefresh.TryGetValue(cacheKey, out bool cachedLos))
            return cachedLos;

        bool hasDirectLos = HasValidStraightObservationLine(
            boardMap,
            terrainDatabase,
            observerCell,
            targetCell,
            observer,
            null,
            dpqAirHeightConfig,
            out _,
            out _,
            out _,
            enableLosValidation: true,
            forcedTargetDomain: targetDomain,
            forcedTargetHeightLevel: targetHeight);
        losCacheForRefresh[cacheKey] = hasDirectLos;
        return hasDirectLos;
    }

    private static bool TryResolveObservationTargetLayer(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out Domain domain,
        out HeightLevel height,
        bool useOccupantLayerForTarget = true)
    {
        domain = Domain.Land;
        height = HeightLevel.Surface;

        if (useOccupantLayerForTarget)
        {
            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(cell);
            if (occupant != null)
            {
                domain = occupant.GetDomain();
                height = occupant.GetHeightLevel();
                return true;
            }
        }

        if (!TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
            return true;

        if (TryResolveConstructionAtCell(map, cell, out ConstructionData constructionData) && constructionData != null)
        {
            domain = constructionData.domain;
            height = constructionData.heightLevel;
            return true;
        }

        StructureData structureData = StructureOccupancyRules.GetStructureAtCell(map, cell);
        if (structureData != null)
        {
            domain = structureData.domain;
            height = structureData.heightLevel;
            return true;
        }

        domain = terrain.domain;
        height = terrain.heightLevel;
        return true;
    }

    private static bool HasValidStraightObservationLine(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int originCell,
        Vector3Int targetCell,
        UnitManager observer,
        UnitManager target,
        DPQAirHeightConfig dpqAirHeightConfig,
        out List<Vector3Int> intermediateCells,
        out List<float> evPath,
        out Vector3Int blockedCell,
        bool enableLosValidation,
        Domain? forcedTargetDomain = null,
        HeightLevel? forcedTargetHeightLevel = null)
    {
        intermediateCells = new List<Vector3Int>();
        evPath = new List<float>();
        blockedCell = Vector3Int.zero;
        if (tilemap == null)
            return false;

        if (!TryResolveCellVision(
                tilemap,
                terrainDatabase,
                originCell,
                observer,
                dpqAirHeightConfig,
                out float originEv,
                out _))
        {
            originEv = 0;
        }
        originEv = ResolveOriginEvForLos(tilemap, terrainDatabase, originCell, observer, dpqAirHeightConfig, originEv);

        if (!forcedTargetDomain.HasValue &&
            !forcedTargetHeightLevel.HasValue &&
            target == null &&
            TryResolveObservationTargetLayer(
                tilemap,
                terrainDatabase,
                targetCell,
                out Domain resolvedTargetDomain,
                out HeightLevel resolvedTargetHeightLevel,
                useOccupantLayerForTarget: false))
        {
            forcedTargetDomain = resolvedTargetDomain;
            forcedTargetHeightLevel = resolvedTargetHeightLevel;
        }

        if (!TryResolveCellVision(
                tilemap,
                terrainDatabase,
                targetCell,
                target,
                dpqAirHeightConfig,
                out float targetEv,
                out _,
                forcedDomain: forcedTargetDomain,
                forcedHeightLevel: forcedTargetHeightLevel))
        {
            targetEv = 0;
        }

        evPath.Add(originEv);
        List<Vector3Int> crossedCells = GetIntermediateCellsByCellLerp(tilemap, originCell, targetCell);
        intermediateCells.AddRange(crossedCells);

        // A altura da linha em cada hex cruzado usa a distancia REAL projetada do
        // centro do hex sobre a reta origem->alvo, nao o indice na lista. Sem isso,
        // a duplicacao de hexes na fronteira entre dois hexagonos distorce o "t":
        // dois hexes empatados na mesma distancia ganhariam limiares diferentes.
        Vector2 losOriginWorld2 = ToWorld2(tilemap.GetCellCenterWorld(originCell));
        Vector2 losTargetWorld2 = ToWorld2(tilemap.GetCellCenterWorld(targetCell));
        Vector2 losDir = losTargetWorld2 - losOriginWorld2;
        float losLenSq = Vector2.Dot(losDir, losDir);

        for (int i = 0; i < crossedCells.Count; i++)
        {
            Vector3Int cell = crossedCells[i];
            float t = losLenSq > 0.0001f
                ? Mathf.Clamp01(Vector2.Dot(ToWorld2(tilemap.GetCellCenterWorld(cell)) - losOriginWorld2, losDir) / losLenSq)
                : (i + 1f) / (crossedCells.Count + 1f);
            float losHeightAtCell = Mathf.Lerp(originEv, targetEv, t);
            evPath.Add(losHeightAtCell);

            if (!enableLosValidation)
                continue;

            if (!TryResolveCellVision(
                    tilemap,
                    terrainDatabase,
                    cell,
                    null,
                    dpqAirHeightConfig,
                    out float cellEv,
                    out bool cellBlocksLoS,
                    forcedDomain: forcedTargetDomain,
                    forcedHeightLevel: forcedTargetHeightLevel))
            {
                continue;
            }

            if (!cellBlocksLoS || cellEv <= 0)
                continue;

            if (cellEv > losHeightAtCell + LosGrazeEpsilon)
            {
                blockedCell = cell;
                return false;
            }
        }

        evPath.Add(targetEv);
        return true;
    }

    private static float ResolveOriginEvForLos(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int originCell,
        UnitManager observer,
        DPQAirHeightConfig dpqAirHeightConfig,
        float fallbackEv)
    {
        if (observer == null)
            return Mathf.Max(0f, fallbackEv);

        Domain domain = observer.GetDomain();
        HeightLevel height = observer.GetHeightLevel();
        if (domain == Domain.Air)
        {
            if (dpqAirHeightConfig != null &&
                dpqAirHeightConfig.TryGetVisionFor(domain, height, out int airEv, out _))
            {
                return Mathf.Max(0f, airEv);
            }

            return Mathf.Max(0f, fallbackEv);
        }

        originCell.z = 0;
        if (tilemap != null &&
            terrainDatabase != null &&
            TryResolveTerrainAtCell(tilemap, terrainDatabase, originCell, out TerrainTypeData originTerrain) &&
            originTerrain != null &&
            originTerrain.shooterInheritsTerrainEv)
        {
            return originTerrain.ResolveShooterInheritedEv();
        }

        return 0;
    }

    private static bool TryResolveCellVision(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        UnitManager occupantUnit,
        DPQAirHeightConfig dpqAirHeightConfig,
        out float ev,
        out bool blockLoS,
        Domain? forcedDomain = null,
        HeightLevel? forcedHeightLevel = null)
    {
        ev = 0f;
        blockLoS = true;
        if (!TryResolveTerrainAtCell(tilemap, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
            return false;

        TryResolveConstructionAtCell(tilemap, cell, out ConstructionData constructionData);
        StructureData structureData = StructureOccupancyRules.GetStructureAtCell(tilemap, cell);

        Domain domain = Domain.Land;
        HeightLevel height = HeightLevel.Surface;
        if (forcedDomain.HasValue && forcedHeightLevel.HasValue)
        {
            domain = forcedDomain.Value;
            height = forcedHeightLevel.Value;
        }
        else if (occupantUnit != null)
        {
            domain = occupantUnit.GetDomain();
            height = occupantUnit.GetHeightLevel();
        }
        else
        {
            TryResolveObservationTargetLayer(
                tilemap,
                terrainDatabase,
                cell,
                out domain,
                out height,
                useOccupantLayerForTarget: false);
        }

        TerrainVisionResolver.Resolve(
            terrain,
            domain,
            height,
            dpqAirHeightConfig,
            constructionData,
            structureData,
            out ev,
            out blockLoS);

        return true;
    }

    private static bool TryResolveConstructionAtCell(Tilemap tilemap, Vector3Int cell, out ConstructionData constructionData)
    {
        constructionData = null;
        if (tilemap == null)
            return false;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell);
        if (construction == null)
            return false;

        ConstructionDatabase db = construction.ConstructionDatabase;
        string id = construction.ConstructionId;
        if (db == null || string.IsNullOrWhiteSpace(id))
            return false;

        if (!db.TryGetById(id, out ConstructionData data) || data == null)
            return false;

        constructionData = data;
        return true;
    }

    private static bool TryResolveTerrainAtCell(Tilemap terrainTilemap, TerrainDatabase terrainDatabase, Vector3Int cell, out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDatabase == null)
            return false;

        cell.z = 0;
        TerrainCellCacheKey cacheKey = new TerrainCellCacheKey(terrainTilemap.GetEntityId().GetHashCode(), cell.x, cell.y);
        if (terrainCacheForRefresh.TryGetValue(cacheKey, out TerrainTypeData cachedTerrain))
        {
            terrain = cachedTerrain;
            return terrain != null;
        }

        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
        {
            terrain = byMainTile;
            terrainCacheForRefresh[cacheKey] = terrain;
            return true;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = GetCachedTilemapsForGrid(grid);
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
                terrainCacheForRefresh[cacheKey] = terrain;
                return true;
            }
        }

        terrainCacheForRefresh[cacheKey] = null;
        return false;
    }

    private static Tilemap[] GetCachedTilemapsForGrid(GridLayout grid)
    {
        if (grid == null)
            return Array.Empty<Tilemap>();

        int gridId = grid.GetEntityId().GetHashCode();
        if (gridTilemapsCache.TryGetValue(gridId, out Tilemap[] cached) &&
            cached != null &&
            cached.Length > 0)
        {
            return cached;
        }

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        gridTilemapsCache[gridId] = maps ?? Array.Empty<Tilemap>();
        return gridTilemapsCache[gridId];
    }

    private static List<Vector3Int> GetIntermediateCellsByCellLerp(Tilemap tilemap, Vector3Int originCell, Vector3Int targetCell)
    {
        // Mantem o mesmo algoritmo robusto do PodeMirar:
        // supersampling + expansao apenas em fronteira ambigua.
        // O traçado por cube-line pode escolher um unico caminho em diagonais/ties
        // e deixar passar casos de bloqueio por relevo entre hexes.
        if (useLegacyLoSLerp)
            return GetIntermediateCellsByCellLerpLegacy(tilemap, originCell, targetCell);

        List<Vector3Int> cells = new List<Vector3Int>();

        originCell.z = 0;
        targetCell.z = 0;
        if (tilemap == null)
            return cells;

        Vector3 originWorld = tilemap.GetCellCenterWorld(originCell);
        Vector3 targetWorld = tilemap.GetCellCenterWorld(targetCell);
        Vector2 originWorld2 = new Vector2(originWorld.x, originWorld.y);
        Vector2 targetWorld2 = new Vector2(targetWorld.x, targetWorld.y);
        float neighborStep = 1f;
        List<Vector3Int> originNeighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, originCell, originNeighbors);
        if (originNeighbors.Count > 0)
        {
            Vector3 n = tilemap.GetCellCenterWorld(originNeighbors[0]);
            neighborStep = Vector2.Distance(originWorld2, new Vector2(n.x, n.y));
            if (neighborStep <= 0.0001f)
                neighborStep = 1f;
        }

        float worldDistance = Vector2.Distance(originWorld2, targetWorld2);
        if (worldDistance <= 0.0001f)
            return cells;

        int approxHexes = Mathf.Max(1, Mathf.CeilToInt(worldDistance / Mathf.Max(0.0001f, neighborStep)));
        if (approxHexes <= 1)
            return cells;

        float borderEpsilon = Mathf.Max(0.01f, neighborStep * 0.08f);
        int sampleCount = approxHexes * 10;
        if (sampleCount <= 1)
            sampleCount = approxHexes * 6;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        List<Vector3Int> centerNeighbors = new List<Vector3Int>(6);
        for (int i = 1; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector2 sample2 = Vector2.Lerp(originWorld2, targetWorld2, t);

            Vector3Int centerCell = tilemap.WorldToCell(new Vector3(sample2.x, sample2.y, 0f));
            centerCell.z = 0;
            if (centerCell != originCell && centerCell != targetCell && seen.Add(centerCell))
                cells.Add(centerCell);

            Vector2 centerWorld2 = ToWorld2(tilemap.GetCellCenterWorld(centerCell));
            float distToCenter = Vector2.Distance(sample2, centerWorld2);

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, centerCell, centerNeighbors);
            for (int n = 0; n < centerNeighbors.Count; n++)
            {
                Vector3Int neighborCell = centerNeighbors[n];
                neighborCell.z = 0;
                if (neighborCell == originCell || neighborCell == targetCell)
                    continue;

                Vector2 neighborWorld2 = ToWorld2(tilemap.GetCellCenterWorld(neighborCell));
                float distToNeighbor = Vector2.Distance(sample2, neighborWorld2);
                if (Mathf.Abs(distToCenter - distToNeighbor) > borderEpsilon)
                    continue;

                if (seen.Add(neighborCell))
                    cells.Add(neighborCell);
            }
        }

        return cells;
    }

    private static List<Vector3Int> GetIntermediateCellsByCellLerpLegacy(Tilemap tilemap, Vector3Int originCell, Vector3Int targetCell)
    {
        List<Vector3Int> cells = new List<Vector3Int>();

        originCell.z = 0;
        targetCell.z = 0;
        if (tilemap == null)
            return cells;

        Vector3 originWorld = tilemap.GetCellCenterWorld(originCell);
        Vector3 targetWorld = tilemap.GetCellCenterWorld(targetCell);
        Vector2 originWorld2 = new Vector2(originWorld.x, originWorld.y);
        Vector2 targetWorld2 = new Vector2(targetWorld.x, targetWorld.y);
        float neighborStep = 1f;
        List<Vector3Int> originNeighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, originCell, originNeighbors);
        if (originNeighbors.Count > 0)
        {
            Vector3 n = tilemap.GetCellCenterWorld(originNeighbors[0]);
            neighborStep = Vector2.Distance(originWorld2, new Vector2(n.x, n.y));
            if (neighborStep <= 0.0001f)
                neighborStep = 1f;
        }

        float worldDistance = Vector2.Distance(originWorld2, targetWorld2);
        if (worldDistance <= 0.0001f)
            return cells;

        int approxHexes = Mathf.Max(1, Mathf.CeilToInt(worldDistance / Mathf.Max(0.0001f, neighborStep)));
        if (approxHexes <= 1)
            return cells;

        float borderEpsilon = Mathf.Max(0.01f, neighborStep * 0.08f);
        int sampleCount = approxHexes * 10;
        if (sampleCount <= 1)
            sampleCount = approxHexes * 6;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        List<Vector3Int> centerNeighbors = new List<Vector3Int>(6);
        for (int i = 1; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector2 sample2 = Vector2.Lerp(originWorld2, targetWorld2, t);

            Vector3Int centerCell = tilemap.WorldToCell(new Vector3(sample2.x, sample2.y, 0f));
            centerCell.z = 0;
            if (centerCell != originCell && centerCell != targetCell && seen.Add(centerCell))
                cells.Add(centerCell);

            Vector2 centerWorld2 = ToWorld2(tilemap.GetCellCenterWorld(centerCell));
            float distToCenter = Vector2.Distance(sample2, centerWorld2);

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, centerCell, centerNeighbors);
            for (int n = 0; n < centerNeighbors.Count; n++)
            {
                Vector3Int neighborCell = centerNeighbors[n];
                neighborCell.z = 0;
                if (neighborCell == originCell || neighborCell == targetCell)
                    continue;

                Vector2 neighborWorld2 = ToWorld2(tilemap.GetCellCenterWorld(neighborCell));
                float distToNeighbor = Vector2.Distance(sample2, neighborWorld2);
                if (Mathf.Abs(distToCenter - distToNeighbor) > borderEpsilon)
                    continue;

                if (seen.Add(neighborCell))
                    cells.Add(neighborCell);
            }
        }

        return cells;
    }

    private static List<Vector3Int> GetIntermediateCellsByCubeLine(Tilemap tilemap, Vector3Int originCell, Vector3Int targetCell)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        if (tilemap == null)
            return cells;

        originCell.z = 0;
        targetCell.z = 0;
        if (originCell == targetCell)
            return cells;

        if (!TryResolveOddRowOffset(tilemap, out bool oddRowOffset))
            return null;

        CubeCoord originCube = OffsetToCube(originCell, oddRowOffset);
        CubeCoord targetCube = OffsetToCube(targetCell, oddRowOffset);
        int steps = CubeDistance(originCube, targetCube);
        if (steps <= 1)
            return cells;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        for (int i = 1; i < steps; i++)
        {
            float t = i / (float)steps;
            CubeCoord lerped = CubeLerp(originCube, targetCube, t);
            CubeCoord rounded = CubeRound(lerped);
            Vector3Int cell = CubeToOffset(rounded, oddRowOffset);
            cell.z = 0;
            if (cell == originCell || cell == targetCell)
                continue;
            if (seen.Add(cell))
                cells.Add(cell);
        }

        return cells;
    }

    private static Vector2 ToWorld2(Vector3 world)
    {
        return new Vector2(world.x, world.y);
    }

    private static bool TryResolveOddRowOffset(Tilemap tilemap, out bool oddRowOffset)
    {
        oddRowOffset = true;
        if (tilemap == null)
            return false;

        int tilemapId = tilemap.GetEntityId().GetHashCode();
        if (tilemapOddRowOffsetCache.TryGetValue(tilemapId, out bool cached))
        {
            oddRowOffset = cached;
            return true;
        }

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, Vector3Int.zero, neighbors);
        if (neighbors.Count <= 0)
            return false;

        int oddScore = 0;
        int evenScore = 0;
        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int n = neighbors[i];
            if (IsExpectedNeighborForOddRowOffsetEvenRow(n))
                oddScore++;
            if (IsExpectedNeighborForEvenRowOffsetEvenRow(n))
                evenScore++;
        }

        oddRowOffset = oddScore >= evenScore;
        tilemapOddRowOffsetCache[tilemapId] = oddRowOffset;
        return true;
    }

    private static bool IsExpectedNeighborForOddRowOffsetEvenRow(Vector3Int cell)
    {
        return (cell.x == 1 && cell.y == 0) ||
               (cell.x == -1 && cell.y == 0) ||
               (cell.x == 0 && cell.y == -1) ||
               (cell.x == -1 && cell.y == -1) ||
               (cell.x == 0 && cell.y == 1) ||
               (cell.x == -1 && cell.y == 1);
    }

    private static bool IsExpectedNeighborForEvenRowOffsetEvenRow(Vector3Int cell)
    {
        return (cell.x == 1 && cell.y == 0) ||
               (cell.x == -1 && cell.y == 0) ||
               (cell.x == 1 && cell.y == -1) ||
               (cell.x == 0 && cell.y == -1) ||
               (cell.x == 1 && cell.y == 1) ||
               (cell.x == 0 && cell.y == 1);
    }

    private static CubeCoord OffsetToCube(Vector3Int cell, bool oddRowOffset)
    {
        int row = cell.y;
        int rowParity = Mathf.Abs(row) & 1;
        float q = oddRowOffset
            ? cell.x - ((row - rowParity) / 2f)
            : cell.x - ((row + rowParity) / 2f);
        float r = row;
        float x = q;
        float z = r;
        float y = -x - z;
        return new CubeCoord(x, y, z);
    }

    private static Vector3Int CubeToOffset(CubeCoord cube, bool oddRowOffset)
    {
        int q = Mathf.RoundToInt(cube.x);
        int r = Mathf.RoundToInt(cube.z);
        int rowParity = Mathf.Abs(r) & 1;
        int col = oddRowOffset
            ? q + ((r - rowParity) / 2)
            : q + ((r + rowParity) / 2);
        return new Vector3Int(col, r, 0);
    }

    private static int CubeDistance(CubeCoord a, CubeCoord b)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dy = Mathf.Abs(a.y - b.y);
        float dz = Mathf.Abs(a.z - b.z);
        return Mathf.RoundToInt(Mathf.Max(dx, Mathf.Max(dy, dz)));
    }

    private static CubeCoord CubeLerp(CubeCoord a, CubeCoord b, float t)
    {
        return new CubeCoord(
            Mathf.Lerp(a.x, b.x, t),
            Mathf.Lerp(a.y, b.y, t),
            Mathf.Lerp(a.z, b.z, t));
    }

    private static CubeCoord CubeRound(CubeCoord fractional)
    {
        float rx = Mathf.Round(fractional.x);
        float ry = Mathf.Round(fractional.y);
        float rz = Mathf.Round(fractional.z);

        float xDiff = Mathf.Abs(rx - fractional.x);
        float yDiff = Mathf.Abs(ry - fractional.y);
        float zDiff = Mathf.Abs(rz - fractional.z);

        if (xDiff > yDiff && xDiff > zDiff)
            rx = -ry - rz;
        else if (yDiff > zDiff)
            ry = -rx - rz;
        else
            rz = -rx - ry;

        return new CubeCoord(rx, ry, rz);
    }

    private static int ResolveUnitCacheInstanceId(UnitManager unit)
    {
        if (unit == null)
            return 0;
        int instanceId = unit.InstanceId;
        if (instanceId > 0)
            return instanceId;
        return unit.GetEntityId().GetHashCode();
    }

    private static void InvalidateCollectVisibleCellsCacheIfNeeded(int globalBoardRevision)
    {
        if (collectVisibleCellsCacheRevision == globalBoardRevision)
            return;

        collectVisibleCellsCacheRevision = globalBoardRevision;
        collectVisibleCellsCache.Clear();
    }

    private static bool TryAppendCollectVisibleCellsFromCache(CollectVisibleCellsCacheKey key, ICollection<Vector3Int> output)
    {
        if (!collectVisibleCellsCache.TryGetValue(key, out List<Vector3Int> cachedCells) ||
            cachedCells == null ||
            cachedCells.Count <= 0)
        {
            debugCacheMisses++;
            return false;
        }

        debugCacheHits++;
        for (int i = 0; i < cachedCells.Count; i++)
            output.Add(cachedCells[i]);
        return true;
    }

    private static void StoreCollectVisibleCellsInCache(CollectVisibleCellsCacheKey key, List<Vector3Int> sourceCells)
    {
        if (sourceCells == null || sourceCells.Count <= 0)
            return;
        if (collectVisibleCellsCache.Count >= MaxCollectVisibleCellsCacheEntries)
            collectVisibleCellsCache.Clear();

        collectVisibleCellsCache[key] = new List<Vector3Int>(sourceCells);
    }

    private static DistanceMapWorkspace RentDistanceMapWorkspace()
    {
        debugPoolRents++;
        if (distanceMapWorkspacePool.Count > 0)
            return distanceMapWorkspacePool.Pop();
        return new DistanceMapWorkspace();
    }

    private static void ReleaseDistanceMapWorkspace(DistanceMapWorkspace workspace)
    {
        if (workspace == null)
            return;

        debugPoolReleases++;
        workspace.distances.Clear();
        workspace.frontier.Clear();
        workspace.neighbors.Clear();
        distanceMapWorkspacePool.Push(workspace);
    }

    private static void BuildDistanceMapInto(
        Tilemap tilemap,
        Vector3Int origin,
        int maxRange,
        DistanceMapWorkspace workspace,
        System.Func<Vector3Int, bool> passableCellFilter = null)
    {
        if (workspace == null)
            return;

        workspace.distances.Clear();
        workspace.frontier.Clear();
        workspace.neighbors.Clear();

        if (tilemap == null || maxRange < 0)
            return;

        origin.z = 0;
        workspace.distances[origin] = 0;
        workspace.frontier.Enqueue(origin);

        while (workspace.frontier.Count > 0)
        {
            Vector3Int current = workspace.frontier.Dequeue();
            int currentDistance = workspace.distances[current];
            if (currentDistance >= maxRange)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, workspace.neighbors);
            for (int i = 0; i < workspace.neighbors.Count; i++)
            {
                Vector3Int next = workspace.neighbors[i];
                next.z = 0;
                if (workspace.distances.ContainsKey(next))
                    continue;
                if (passableCellFilter != null && !passableCellFilter(next))
                    continue;

                int nextDistance = currentDistance + 1;
                if (nextDistance > maxRange)
                    continue;

                workspace.distances[next] = nextDistance;
                workspace.frontier.Enqueue(next);
            }
        }
    }

    private static bool IsDebugFragataObserver(UnitManager observer)
    {
        if (observer == null)
            return false;

        string displayName = observer.UnitDisplayName;
        if (!string.IsNullOrWhiteSpace(displayName) &&
            displayName.IndexOf("fragata", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (observer.TryGetUnitData(out UnitData data) &&
            data != null &&
            !string.IsNullOrWhiteSpace(data.id) &&
            data.id.IndexOf("fragata", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return observer.name.IndexOf("fragata", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
