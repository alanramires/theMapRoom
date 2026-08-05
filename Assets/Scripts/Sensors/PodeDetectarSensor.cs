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
            int globalBoardRevision,
            int teamObserverRevision)
        {
            observerInstanceId = ResolveUnitCacheInstanceId(observer);
            observerCellX = observerCell.x;
            observerCellY = observerCell.y;
            observerTeamId = observer != null ? observer.SlotIndex : -1;
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
    // Os caches de terreno, construcao e estrutura mudaram de casa: agora vivem
    // no ObservationCellService, junto do codigo que os preenche. Eles nao eram
    // do PodeDetectar â€” eram fato de tabuleiro que o PodeEnxergar tambem
    // precisa. A medicao que os criou continua valendo: GetConstructionAtCell e
    // GetStructureAtCell varrem a cena a cada chamada, ~312us e ~300us, e
    // somavam 68ms dos 106ms de um collect.
    private static readonly Dictionary<LosCacheKey, bool> losCacheForRefresh = new Dictionary<LosCacheKey, bool>(8192);
    private static readonly Dictionary<CollectVisibleCellsCacheKey, List<Vector3Int>> collectVisibleCellsCache = new Dictionary<CollectVisibleCellsCacheKey, List<Vector3Int>>(128);
    private static readonly List<Vector3Int> collectVisibleCellsScratch = new List<Vector3Int>(128);
    private static int collectVisibleCellsCacheRevision = int.MinValue;
    private static int debugCacheHits;
    private static int debugCacheMisses;
    private static int debugPoolRents;
    private static int debugPoolReleases;
    private static int debugFragataCollectWorkspaceRents;
    private static int debugFragataCollectWorkspaceReleases;

    // Decomposicao do custo interno de CollectVisibleCells. A saida (26-31 celulas)
    // nao diz o trabalho feito: maxRange e o MAXIMO entre as camadas de visao, e o
    // mapa de distancias cobre esse raio inteiro antes de qualquer filtro.
    private static int debugCollectRuns;
    private static int debugCollectDistanceCells;
    private static int debugCollectMaxRange;
    private static int debugCollectLayerChecks;
    private static int debugCollectSpecializationChecks;
    private static int debugCollectLosCalls;
    private static int debugCollectLosHits;
    private static int debugCollectAquaticMaps;
    private static double debugCollectLosMs;
    private static int debugCollectCellVisionCalls;
    private static double debugCollectConstructionMs;
    private static double debugCollectStructureMs;
    // Os contadores do traçado mudaram de casa junto com o traçado: agora vivem
    // no ObservationLineService, e este sensor apenas os reporta.

    public static void ResetFogDebugCounters()
    {
        debugCacheHits = 0;
        debugCacheMisses = 0;
        debugPoolRents = 0;
        debugPoolReleases = 0;
        debugFragataCollectWorkspaceRents = 0;
        debugFragataCollectWorkspaceReleases = 0;
        debugCollectRuns = 0;
        debugCollectDistanceCells = 0;
        debugCollectMaxRange = 0;
        debugCollectLayerChecks = 0;
        debugCollectSpecializationChecks = 0;
        debugCollectLosCalls = 0;
        debugCollectLosHits = 0;
        debugCollectAquaticMaps = 0;
        debugCollectLosMs = 0d;
        ObservationCellService.ResetCounters();
        ObservationLineService.ResetCounters();
    }

    public static void GetCollectDebugCounters(
        out int runs,
        out int distanceCells,
        out int maxRange,
        out int layerChecks,
        out int specializationChecks,
        out int losCalls,
        out int losHits,
        out int aquaticMaps)
    {
        runs = debugCollectRuns;
        distanceCells = debugCollectDistanceCells;
        maxRange = debugCollectMaxRange;
        layerChecks = debugCollectLayerChecks;
        specializationChecks = debugCollectSpecializationChecks;
        losCalls = debugCollectLosCalls;
        losHits = debugCollectLosHits;
        aquaticMaps = debugCollectAquaticMaps;
    }

    public static void GetCollectLosDebugCounters(
        out double losMs,
        out int cellVisionCalls,
        out double constructionMs,
        out double structureMs,
        out double lerpMs,
        out int lerpCells)
    {
        losMs = debugCollectLosMs;
        cellVisionCalls = ObservationCellService.CellVisionCalls;
        constructionMs = ObservationCellService.ConstructionMs;
        structureMs = ObservationCellService.StructureMs;
        lerpMs = ObservationLineService.LerpMs;
        lerpCells = ObservationLineService.LerpCells;
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

    public static void ClearRefreshScopedTerrainCache()
    {
        losCacheForRefresh.Clear();
        ObservationCellService.ClearRefreshScopedCaches();
    }

    public static bool IsTargetObservedByTeam(
        UnitManager target,
        int viewerSlotIndex,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        bool enableSpotter = true,
        bool enableStealthValidation = true)
    {
        if (target == null || !target.gameObject.activeInHierarchy || target.IsEmbarked)
            return false;

        if (target.SlotIndex == viewerSlotIndex)
            return true;

        Tilemap boardMap = map != null ? map : target.BoardTilemap;
        if (boardMap == null)
            return false;
        if (!IsUnitOnBoard(target, boardMap))
            return false;

        // FONTE DE VERDADE: se o PodeDetectar coletou o alvo, ele aparece no
        // tabuleiro. Ponto.
        //
        // Antes daqui existiam DUAS implementacoes da mesma pergunta — esta
        // varredura par-a-par e a coleta por observador que as ferramentas
        // auditam. Elas podiam discordar, e discordavam: a janela mostrava um
        // caca detectado por chave de Stealth com LOS direta e o jogo nao o
        // desenhava. Uma pergunta, uma implementacao.
        IReadOnlyList<UnitManager> units = GetUnitsForSensorQueries();
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager observer = units[i];
            if (observer == null || !observer.gameObject.activeInHierarchy || observer.IsEmbarked)
                continue;
            if (observer.SlotIndex != viewerSlotIndex)
                continue;
            if (!IsUnitOnBoard(observer, boardMap))
                continue;

            observedTeamDetectedStealth.Clear();
            observedTeamUndetectedStealth.Clear();
            observedTeamSpotted.Clear();
            observedTeamBlocked.Clear();
            CollectDetection(
                observer,
                boardMap,
                terrainDatabase,
                observedTeamDetectedStealth,
                observedTeamUndetectedStealth,
                observedTeamSpotted,
                observedTeamBlocked,
                out _,
                dpqAirHeightConfig,
                enableLosValidation,
                enableSpotter,
                enableStealthValidation);

            if (ContainsDetectedTarget(observedTeamDetectedStealth, target) ||
                ContainsDetectedTarget(observedTeamSpotted, target))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly List<PodeDetectarOption> observedTeamDetectedStealth =
        new List<PodeDetectarOption>(32);
    private static readonly List<PodeDetectarOption> observedTeamUndetectedStealth =
        new List<PodeDetectarOption>(32);
    private static readonly List<PodeDetectarOption> observedTeamSpotted =
        new List<PodeDetectarOption>(32);
    private static readonly List<PodeDetectarOption> observedTeamBlocked =
        new List<PodeDetectarOption>(32);

    private static bool ContainsDetectedTarget(
        List<PodeDetectarOption> options,
        UnitManager target)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] != null && options[i].targetUnit == target)
                return true;
        }
        return false;
    }

    // Uso de visibilidade direta para FoW/UI local: sem observador avancado.
    public static bool IsTargetObservedByTeamWithoutForwardObserver(
        UnitManager target,
        int viewerSlotIndex,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        bool enableStealthValidation = true)
    {
        return IsTargetObservedByTeam(
            target,
            viewerSlotIndex,
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
        Vector3Int? virtualObserverCell = null)
    {
        if (visibleCellsOutput == null)
            return;

        if (observer == null
            || (observer.IsEmbarked
                && !virtualObserverCell.HasValue))
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

        Vector3Int observerCell = virtualObserverCell
            ?? observer.CurrentCellPosition;
        observerCell.z = 0;
        int globalBoardRevision = ThreatRevisionTracker.GlobalBoardRevision;
        int teamObserverRevision = observer != null
            ? ThreatRevisionTracker.GetSlotObserverRevision(PlayerSlotId.FromIndex(observer.SlotIndex))
            : 0;
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
            globalBoardRevision,
            teamObserverRevision);

        if (TryAppendCollectVisibleCellsFromCache(cacheKey, visibleCellsOutput))
            return;

        bool isFragataCollect = IsDebugFragataObserver(observer);
        collectVisibleCellsScratch.Clear();
        collectVisibleCellsScratch.Add(observerCell);
        DistanceMapWorkspace workspace = RentDistanceMapWorkspace();
        // O mapa de propagacao pertence a UMA camada de alvo: ele anda pelas
        // celulas que aquela camada aceita. Guardamos qual camada esta
        // carregada porque, desde que Propagated deixou de ser privilegio do
        // submarino, uma ficha pode declarar propagacao em mais de uma camada —
        // e entregar a distancia pela agua a um alvo de superficie seria erro
        // silencioso, nao excecao.
        DistanceMapWorkspace aquaticWorkspace = null;
        bool propagationMapReady = false;
        Domain propagationDomain = Domain.Land;
        HeightLevel propagationHeight = HeightLevel.Surface;
        if (isFragataCollect)
            debugFragataCollectWorkspaceRents++;
        try
        {
            BuildDistanceMapInto(boardMap, observerCell, maxRange, workspace);
            debugCollectRuns++;
            debugCollectDistanceCells += workspace.distances.Count;
            if (maxRange > debugCollectMaxRange)
                debugCollectMaxRange = maxRange;
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
                debugCollectAquaticMaps++;
                BuildDistanceMapInto(
                    boardMap,
                    observerCell,
                    maxRange,
                    aquaticWorkspace,
                    c => IsCellPassableForPropagation(
                        boardMap,
                        terrainDatabase,
                        c,
                        Domain.Submarine,
                        HeightLevel.Submerged));
                propagationMapReady = true;
                propagationDomain = Domain.Submarine;
                propagationHeight = HeightLevel.Submerged;
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
                bool useAquaticDistance = ShouldUsePropagatedDistance(observerData, targetDomain, targetHeight) && terrainDatabase != null;
                if (useAquaticDistance)
                {
                    // Remonta quando a camada pedida nao e a que esta carregada.
                    // BuildDistanceMapInto limpa o workspace, entao reusar o
                    // mesmo aluguel e seguro e evita devolver e pegar de novo.
                    if (!propagationMapReady ||
                        propagationDomain != targetDomain ||
                        propagationHeight != targetHeight)
                    {
                        if (aquaticWorkspace == null)
                            aquaticWorkspace = RentDistanceMapWorkspace();
                        debugCollectAquaticMaps++;
                        BuildDistanceMapInto(
                            boardMap,
                            observerCell,
                            maxRange,
                            aquaticWorkspace,
                            c => IsCellPassableForPropagation(
                                boardMap,
                                terrainDatabase,
                                c,
                                targetDomain,
                                targetHeight));
                        propagationMapReady = true;
                        propagationDomain = targetDomain;
                        propagationHeight = targetHeight;
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

                bool effectiveLosValidation = ResolveEffectiveLosValidation(
                    observerData,
                    targetDomain,
                    targetHeight,
                    enableLosValidation);
                bool bypassLosByPolicy = !effectiveLosValidation;
                // A linha SEMPRE e validada. Nao existe camada que dispense a
                // reta: o alvo alto atras de relevo pode ficar escondido de quem
                // esta atras da floresta, porque a linha ascendente ainda nao
                // subiu o bastante naquele ponto.
                bool hasDirectLos = HasValidStraightObservationLine(
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

    /// <summary>
    /// Consulta pura da cobertura de uma camada aerea a partir de uma celula
    /// candidata. Nao move a unidade e nao publica FOW, contatos ou deteccao.
    /// Usa exatamente as regras de alcance e LoS da apresentacao por camada.
    /// </summary>
    public static void CollectVisibleAirCellsAt(
        UnitManager observer,
        Vector3Int observerCell,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        ICollection<Vector3Int> visibleCellsOutput,
        HeightLevel targetHeight,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true)
    {
        if (targetHeight != HeightLevel.AirLow
            && targetHeight != HeightLevel.AirHigh)
        {
            return;
        }

        observerCell.z = 0;
        CollectVisibleCells(
            observer,
            map,
            terrainDatabase,
            visibleCellsOutput,
            dpqAirHeightConfig,
            enableLosValidation,
            enableSpotter: false,
            useOccupantLayerForTarget: false,
            preserveObserverLayerRangeForHexVisibility: false,
            forceVirtualTargetLayer: true,
            forcedVirtualTargetDomain: Domain.Air,
            forcedVirtualTargetHeight: targetHeight,
            virtualObserverCell: observerCell);
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
        // Pergunta de HEXES â€” delegada ao PodeEnxergar, que e quem responde por
        // ela desde o split. Visao padrao da ficha, reta pura, uma passada por
        // superficie, e a lista de Detect Specializations invisivel.
        //
        // O que sai daqui: preserveObserverLayerRangeForHexVisibility levava a
        // CanObserveCellByAnyObserverVisionLayer, que aceitava a celula se
        // QUALQUER camada do observador a alcancasse. Com o cruzamento da
        // familia aquatica, a entrada "Submarine/Submerged 7" do submarino
        // revelava mar no alcance de cacar submarino. Ele revela pelo
        // periscopio, que e a visao padrao.
        PodeEnxergarSensor.CollectKnownTerrainCells(
            observer,
            map,
            terrainDatabase,
            visibleCellsOutput,
            dpqAirHeightConfig,
            enableLosValidation);
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
                enableSpotter))
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

            // SÃ³ permite cruzar tipos de terreno dentro da famÃ­lia aquÃ¡tica (Naval â†” Submarine).
            // Impede que visÃ£o sub/submerged revele hexes de terra ou ar.
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

            debugCollectSpecializationChecks++;
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
                    enableSpotter))
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
        bool enableSpotter)
    {
        debugCollectLayerChecks++;
        int detectionRange = ResolveDetectionRange(observer, observerData, null, targetDomain, targetHeight);
        if (distance > detectionRange)
            return false;
        if (distance <= 1)
            return true;

        bool effectiveLosValidation = ResolveEffectiveLosValidation(observerData, targetDomain, targetHeight, enableLosValidation);
        bool bypassLosByPolicy = !effectiveLosValidation;
        // A linha SEMPRE e validada, inclusive contra alvo aereo. Nao existe
        // camada que dispense a reta: propagacao contorna obstaculo no plano do
        // meio, e linha ascendente nao tem do que desviar — nao ha montanha
        // flutuante.
        bool hasDirectLos = TryGetDirectLosCachedForRefresh(
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
        bool detectPropagationReady = false;
        Domain detectPropagationDomain = Domain.Land;
        HeightLevel detectPropagationHeight = HeightLevel.Surface;
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
                bool useAquaticDistance = ShouldUsePropagatedDistance(observerData, targetDomain, targetHeight) && terrainDatabase != null;
                Dictionary<Vector3Int, int> distanceMap = defaultDistanceMap;
                if (useAquaticDistance)
                {
                    // Mesmo cuidado do coletor de celulas: o mapa de propagacao
                    // pertence a UMA camada, e a ficha pode declarar Propagated
                    // em mais de uma. Entregar a distancia pela agua a um alvo
                    // de superficie seria erro silencioso.
                    if (!detectPropagationReady ||
                        detectPropagationDomain != targetDomain ||
                        detectPropagationHeight != targetHeight)
                    {
                        if (aquaticDetectWorkspace == null)
                            aquaticDetectWorkspace = RentDistanceMapWorkspace();
                        BuildDistanceMapInto(
                            boardMap,
                            observerCell,
                            maxRange,
                            aquaticDetectWorkspace,
                            cell => IsCellPassableForPropagation(boardMap, terrainDatabase, cell, targetDomain, targetHeight));
                        detectPropagationReady = true;
                        detectPropagationDomain = targetDomain;
                        detectPropagationHeight = targetHeight;
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

                // Um perfil por candidato, preenchido pelo proprio traçado. Ele
                // substitui as duas listas cruas que este laco copiava por
                // opcao: a resposta e o relatorio dela sao o mesmo objeto.
                ObservationLineProfile lineProfile = new ObservationLineProfile();
                bool effectiveLosValidation = ResolveEffectiveLosValidation(observerData, targetDomain, targetHeight, enableLosValidation);
                bool bypassLosByPolicy = !effectiveLosValidation;
                // Este pulo estava VIVO: observador aereo contra alvo em AirHigh
                // nao tracava linha nenhuma, e sem o gate de parametro que os
                // outros dois tinham. Caca contra caca era so alcance.
                //
                // A linha e sempre validada. Propagacao contorna obstaculo no
                // plano do meio; linha ascendente nao tem do que desviar.
                bool hasDirectLos = HasValidStraightObservationLine(
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
                    enableLosValidation: true,
                    profile: lineProfile);

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
                            lineProfile = lineProfile,
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
                        lineProfile = lineProfile,
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
                            lineProfile = lineProfile,
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
                            lineProfile = lineProfile,
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
                        lineProfile = lineProfile,
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
                    lineProfile = lineProfile,
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
                if (!PlayerSlotRelations.AreAllies(ally, observer))
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
            if (!PlayerSlotRelations.AreAllies(observer.SlotIndex, construction.SlotIndex))
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
            if (!PlayerSlotRelations.AreAllies(ally, referenceUnit))
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
            if (!PlayerSlotRelations.AreAllies(ally, referenceUnit))
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
                if (!PlayerSlotRelations.AreAllies(ally, observer))
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

        return PlayerSlotRelations.AreEnemies(observer, target);
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

    /// <summary>
    /// Propagacao pelo meio em vez de reta. Quem decide e a FICHA â€” o
    /// DetectionMethod declarado para aquela camada de alvo.
    ///
    /// Antes disso, toda deteccao de alvo submerso propagava, tivesse a ficha
    /// declarado ou nao, e nenhuma outra camada podia. As tres unidades que
    /// caÃ§am submerso hoje estao todas em Propagated, entao nenhuma ficha muda
    /// de comportamento; o que muda e quem manda, e que agora qualquer camada
    /// pode declarar propagacao.
    /// </summary>
    private static bool ShouldUsePropagatedDistance(
        UnitData observerData,
        Domain targetDomain,
        HeightLevel targetHeight)
    {
        // Sem privilegio de camada. Quem propaga e quem a ficha declarou como
        // Propagated, e o MEIO sai da propria camada do alvo: a propagacao anda
        // pelas celulas que aquela camada aceita.
        //
        // E por isso que um "detect land/surface 5 propagate" inventa um
        // megafone sem precisar de codigo novo â€” o som contorna o morro pelas
        // celulas de superficie, do mesmo jeito que o sonar contorna a
        // peninsula pelas celulas de agua.
        return observerData != null
            && observerData.ResolveDetectionMethodFor(targetDomain, targetHeight)
                == DetectionMethod.Propagated;
    }

    /// <summary>
    /// Por onde a propagacao anda: pelas celulas que a CAMADA DO ALVO aceita.
    /// Agua conectada para submerso, superficie para um sensor de superficie, e
    /// assim por diante â€” a regra e a mesma, o meio e que muda.
    /// </summary>
    private static bool IsCellPassableForPropagation(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        Domain targetDomain,
        HeightLevel targetHeight)
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

        StructureData structure = ResolveStructureAtCellCachedForRefresh(map, cell);
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

        debugCollectLosCalls++;
        if (losCacheForRefresh.TryGetValue(cacheKey, out bool cachedLos))
        {
            debugCollectLosHits++;
            return cachedLos;
        }

        double losStartMs = Time.realtimeSinceStartupAsDouble;
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
        debugCollectLosMs +=
            (Time.realtimeSinceStartupAsDouble - losStartMs) * 1000d;
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
        return ObservationCellService.TryResolveObservationLayer(
            map,
            terrainDatabase,
            cell,
            out domain,
            out height,
            useOccupantLayerForTarget);
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
        HeightLevel? forcedTargetHeightLevel = null,
        ObservationLineProfile profile = null)
    {
        return ObservationLineService.TryTrace(
            tilemap, terrainDatabase, originCell, targetCell, observer, target,
            dpqAirHeightConfig, out intermediateCells, out evPath, out blockedCell,
            enableLosValidation, forcedTargetDomain, forcedTargetHeightLevel,
            OriginEvRule.InheritTerrain, profile);
    }

    private static float ResolveOriginEvForLos(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int originCell,
        UnitManager observer,
        DPQAirHeightConfig dpqAirHeightConfig,
        float fallbackEv)
    {
        return ObservationLineService.ResolveOriginEv(
            tilemap, terrainDatabase, originCell, observer, dpqAirHeightConfig, fallbackEv);
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
        // Fato de celula: uma fonte so, o ObservationCellService.
        return ObservationCellService.TryResolveCellVision(
            tilemap,
            terrainDatabase,
            cell,
            occupantUnit,
            dpqAirHeightConfig,
            out ev,
            out blockLoS,
            forcedDomain,
            forcedHeightLevel);
    }

    private static bool TryResolveConstructionAtCell(Tilemap tilemap, Vector3Int cell, out ConstructionData constructionData)
    {
        return ObservationCellService.TryResolveConstruction(
            tilemap,
            cell,
            out constructionData);
    }

    private static StructureData ResolveStructureAtCellCachedForRefresh(Tilemap tilemap, Vector3Int cell)
    {
        return ObservationCellService.ResolveStructure(tilemap, cell);
    }

    private static bool TryResolveTerrainAtCell(Tilemap terrainTilemap, TerrainDatabase terrainDatabase, Vector3Int cell, out TerrainTypeData terrain)
    {
        return ObservationCellService.TryResolveTerrain(
            terrainTilemap,
            terrainDatabase,
            cell,
            out terrain);
    }

    private static Tilemap[] GetCachedTilemapsForGrid(GridLayout grid)
    {
        return ObservationCellService.GetCachedTilemapsForGrid(grid);
    }

    private static List<Vector3Int> GetIntermediateCellsByCellLerp(Tilemap tilemap, Vector3Int originCell, Vector3Int targetCell)
    {
        return ObservationLineService.GetIntermediateCellsByCellLerp(tilemap, originCell, targetCell);
    }

    private static Vector2 ToWorld2(Vector3 world)
    {
        return HexGridGeometry.ToWorld2(world);
    }

    private static bool TryResolveOddRowOffset(Tilemap tilemap, out bool oddRowOffset)
    {
        return HexGridGeometry.TryResolveOddRowOffset(tilemap, out oddRowOffset);
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
