using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Consulta pura da cobertura estrutural de Vigilancia Aerea.
///
/// A geometria depende apenas do mapa, da topologia, da celula candidata e do
/// perfil do observador. Unidades, contatos e FOW nao participam da chave nem
/// sao publicados por esta consulta. A sobreposicao aliada e calculada sobre o
/// resultado imutavel do cache.
/// </summary>
public static class AirSurveillanceCoverageService
{
    private const int MaxStructuralCacheEntries = 4096;

    public readonly struct Result
    {
        public readonly int AirLow;
        public readonly int AirHigh;
        public readonly int MarginalAirLow;
        public readonly int MarginalAirHigh;
        public readonly int UnexploredMarginalAirHigh;
        public readonly bool DetectsLowStealth;
        public readonly bool DetectsHighStealth;
        public readonly float Score;

        public int VisibleCells => AirLow + AirHigh;

        public Result(
            int airLow,
            int airHigh,
            int marginalAirLow,
            int marginalAirHigh,
            int unexploredMarginalAirHigh,
            bool detectsLowStealth,
            bool detectsHighStealth,
            float score)
        {
            AirLow = airLow;
            AirHigh = airHigh;
            MarginalAirLow = marginalAirLow;
            MarginalAirHigh = marginalAirHigh;
            UnexploredMarginalAirHigh =
                unexploredMarginalAirHigh;
            DetectsLowStealth = detectsLowStealth;
            DetectsHighStealth = detectsHighStealth;
            Score = score;
        }
    }

    private sealed class StructuralCoverage
    {
        public readonly Vector3Int[] AirLow;
        public readonly Vector3Int[] AirHigh;

        public StructuralCoverage(
            List<Vector3Int> airLow,
            List<Vector3Int> airHigh)
        {
            AirLow = airLow != null
                ? airLow.ToArray()
                : Array.Empty<Vector3Int>();
            AirHigh = airHigh != null
                ? airHigh.ToArray()
                : Array.Empty<Vector3Int>();
        }
    }

    private readonly struct StructuralCacheKey :
        IEquatable<StructuralCacheKey>
    {
        private readonly int mapObjectId;
        private readonly int terrainDatabaseObjectId;
        private readonly int airConfigObjectId;
        private readonly int unitDataObjectId;
        private readonly int cellX;
        private readonly int cellY;
        private readonly int topologyVersion;
        private readonly string topologyFingerprint;
        private readonly int airLowVision;
        private readonly int airHighVision;
        private readonly int airLowLosPolicy;
        private readonly int airHighLosPolicy;
        private readonly int observerDomain;
        private readonly int observerHeight;
        private readonly bool enableLos;

        public StructuralCacheKey(
            UnitManager observer,
            UnitData data,
            Vector3Int observerCell,
            Tilemap map,
            TerrainDatabase terrainDatabase,
            DPQAirHeightConfig airConfig,
            bool enableLos,
            BoardTopologyIndex topology)
        {
            mapObjectId = ResolveObjectId(map);
            terrainDatabaseObjectId =
                ResolveObjectId(terrainDatabase);
            airConfigObjectId = ResolveObjectId(airConfig);
            unitDataObjectId = ResolveObjectId(data);
            cellX = observerCell.x;
            cellY = observerCell.y;
            topologyVersion =
                topology != null ? topology.TopologyVersion : 0;
            topologyFingerprint =
                topology != null
                    ? topology.TopologyFingerprint
                    : string.Empty;
            airLowVision = data != null
                ? data.ResolveVisionFor(
                    Domain.Air,
                    HeightLevel.AirLow)
                : Mathf.Max(1, observer != null ? observer.Visao : 1);
            airHighVision = data != null
                ? data.ResolveVisionFor(
                    Domain.Air,
                    HeightLevel.AirHigh)
                : Mathf.Max(1, observer != null ? observer.Visao : 1);
            airLowLosPolicy = data != null
                ? (int)data.ResolveLosPolicyFor(
                    Domain.Air,
                    HeightLevel.AirLow)
                : (int)LosPolicy.InheritGlobal;
            airHighLosPolicy = data != null
                ? (int)data.ResolveLosPolicyFor(
                    Domain.Air,
                    HeightLevel.AirHigh)
                : (int)LosPolicy.InheritGlobal;
            observerDomain =
                observer != null ? (int)observer.GetDomain() : -1;
            observerHeight =
                observer != null ? (int)observer.GetHeightLevel() : -1;
            this.enableLos = enableLos;
        }

        public bool Equals(StructuralCacheKey other)
        {
            return mapObjectId == other.mapObjectId
                && terrainDatabaseObjectId
                    == other.terrainDatabaseObjectId
                && airConfigObjectId == other.airConfigObjectId
                && unitDataObjectId == other.unitDataObjectId
                && cellX == other.cellX
                && cellY == other.cellY
                && topologyVersion == other.topologyVersion
                && string.Equals(
                    topologyFingerprint,
                    other.topologyFingerprint,
                    StringComparison.Ordinal)
                && airLowVision == other.airLowVision
                && airHighVision == other.airHighVision
                && airLowLosPolicy == other.airLowLosPolicy
                && airHighLosPolicy == other.airHighLosPolicy
                && observerDomain == other.observerDomain
                && observerHeight == other.observerHeight
                && enableLos == other.enableLos;
        }

        public override bool Equals(object obj)
        {
            return obj is StructuralCacheKey other
                && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + mapObjectId;
                hash = hash * 31 + terrainDatabaseObjectId;
                hash = hash * 31 + airConfigObjectId;
                hash = hash * 31 + unitDataObjectId;
                hash = hash * 31 + cellX;
                hash = hash * 31 + cellY;
                hash = hash * 31 + topologyVersion;
                hash = hash * 31
                    + StringComparer.Ordinal.GetHashCode(
                        topologyFingerprint ?? string.Empty);
                hash = hash * 31 + airLowVision;
                hash = hash * 31 + airHighVision;
                hash = hash * 31 + airLowLosPolicy;
                hash = hash * 31 + airHighLosPolicy;
                hash = hash * 31 + observerDomain;
                hash = hash * 31 + observerHeight;
                hash = hash * 31 + (enableLos ? 1 : 0);
                return hash;
            }
        }
    }

    private static readonly Dictionary<
        StructuralCacheKey,
        StructuralCoverage> StructuralCache =
        new Dictionary<StructuralCacheKey, StructuralCoverage>();
    private static readonly Queue<StructuralCacheKey>
        StructuralCacheInsertionOrder =
            new Queue<StructuralCacheKey>();

    public static Result Evaluate(
        UnitManager observer,
        Vector3Int observerCell,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig airConfig,
        bool enableLos,
        ISet<Vector3Int> alliedAirLow = null,
        ISet<Vector3Int> alliedAirHigh = null,
        Func<Vector3Int, bool> isExplored = null)
    {
        using var perf = new AIDecisionPerfScope(
            observer,
            "airSurveillancePreciseCoverage");
        StructuralCoverage coverage = GetStructuralCoverage(
            observer,
            observerCell,
            map,
            terrainDatabase,
            airConfig,
            enableLos);

        int marginalLow = CountMarginal(
            coverage.AirLow,
            alliedAirLow);
        int marginalHigh = CountMarginal(
            coverage.AirHigh,
            alliedAirHigh);
        int unexploredMarginalHigh = 0;
        if (isExplored != null)
        {
            for (int i = 0; i < coverage.AirHigh.Length; i++)
            {
                Vector3Int cell = coverage.AirHigh[i];
                if ((alliedAirHigh == null
                        || !alliedAirHigh.Contains(cell))
                    && !isExplored(cell))
                {
                    unexploredMarginalHigh++;
                }
            }
        }

        UnitData data = null;
        if (observer != null)
            observer.TryGetUnitData(out data);
        bool detectsLowStealth = data != null
            && data.HasStealthDetectionFor(
                Domain.Air,
                HeightLevel.AirLow);
        bool detectsHighStealth = data != null
            && data.HasStealthDetectionFor(
                Domain.Air,
                HeightLevel.AirHigh);

        int overlapLow = Mathf.Max(
            0,
            coverage.AirLow.Length - marginalLow);
        int overlapHigh = Mathf.Max(
            0,
            coverage.AirHigh.Length - marginalHigh);

        // A utilidade pertence ao time, nao ao sensor isolado. Uma celula nova
        // recebe quase todo o valor; cobertura ja fornecida por um aliado vale
        // apenas uma pequena redundancia operacional. O bonus stealth tambem
        // incide principalmente onde o observador acrescenta cobertura.
        float score =
            marginalLow * 3f
            + marginalHigh * 3f
            + unexploredMarginalHigh * 25f
            + overlapLow * 1.5f
            + overlapHigh
            + (detectsLowStealth
                ? marginalLow * 2f
                : 0f)
            + (detectsHighStealth
                ? marginalHigh * 2f
                : 0f);

        return new Result(
            coverage.AirLow.Length,
            coverage.AirHigh.Length,
            marginalLow,
            marginalHigh,
            unexploredMarginalHigh,
            detectsLowStealth,
            detectsHighStealth,
            score);
    }

    public static void AppendStructuralCoverage(
        UnitManager observer,
        Vector3Int observerCell,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig airConfig,
        bool enableLos,
        ISet<Vector3Int> airLow,
        ISet<Vector3Int> airHigh)
    {
        StructuralCoverage coverage = GetStructuralCoverage(
            observer,
            observerCell,
            map,
            terrainDatabase,
            airConfig,
            enableLos);
        Append(coverage.AirLow, airLow);
        Append(coverage.AirHigh, airHigh);
    }

    public static void ClearStructuralCache()
    {
        StructuralCache.Clear();
        StructuralCacheInsertionOrder.Clear();
    }

    private static StructuralCoverage GetStructuralCoverage(
        UnitManager observer,
        Vector3Int observerCell,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig airConfig,
        bool enableLos)
    {
        if (observer == null || map == null)
            return new StructuralCoverage(null, null);

        observerCell.z = 0;
        observer.TryGetUnitData(out UnitData data);
        BoardTopologyIndex.TryGetFor(
            map,
            out BoardTopologyIndex topology);
        var key = new StructuralCacheKey(
            observer,
            data,
            observerCell,
            map,
            terrainDatabase,
            airConfig,
            enableLos,
            topology);

        if (StructuralCache.TryGetValue(
                key,
                out StructuralCoverage cached))
        {
            AIDecisionPerf.AddCount(
                "AirSurveillanceCoverageCacheHits");
            return cached;
        }

        AIDecisionPerf.AddCount(
            "AirSurveillanceCoverageCacheMisses");
        var airLow = new List<Vector3Int>();
        var airHigh = new List<Vector3Int>();
        // Politica do papel Vigilancia Aerea: EWACS e Radar Movel escolhem
        // postura pelo ganho primario em Air/High. Air/Low e secundario e nao
        // justifica uma segunda passada cara para cada hex candidato. Isto
        // afeta somente o ranking puro; o commit do tabuleiro continua
        // recalculando e publicando o FOW oficial de todas as camadas.
        AIDecisionPerf.AddCount(
            "AirSurveillanceCoverageAirLowSkippedByPolicy");
        PodeDetectarSensor.CollectVisibleAirCellsAt(
            observer,
            observerCell,
            map,
            terrainDatabase,
            airHigh,
            HeightLevel.AirHigh,
            airConfig,
            enableLos);

        var built = new StructuralCoverage(airLow, airHigh);
        Store(key, built);
        AIDecisionPerf.AddCount(
            "AirSurveillanceCoverageCacheStores");
        AIDecisionPerf.AddCount(
            "AirSurveillanceCoverageCellsBuilt",
            built.AirLow.Length + built.AirHigh.Length);
        return built;
    }

    private static void Store(
        StructuralCacheKey key,
        StructuralCoverage coverage)
    {
        while (StructuralCache.Count >= MaxStructuralCacheEntries
            && StructuralCacheInsertionOrder.Count > 0)
        {
            StructuralCacheKey oldest =
                StructuralCacheInsertionOrder.Dequeue();
            StructuralCache.Remove(oldest);
        }

        StructuralCache[key] = coverage;
        StructuralCacheInsertionOrder.Enqueue(key);
    }

    private static int CountMarginal(
        Vector3Int[] cells,
        ISet<Vector3Int> alliedCoverage)
    {
        if (alliedCoverage == null)
            return cells != null ? cells.Length : 0;

        int marginal = 0;
        for (int i = 0; cells != null && i < cells.Length; i++)
        {
            if (!alliedCoverage.Contains(cells[i]))
                marginal++;
        }
        return marginal;
    }

    private static void Append(
        Vector3Int[] source,
        ISet<Vector3Int> target)
    {
        if (source == null || target == null)
            return;
        for (int i = 0; i < source.Length; i++)
            target.Add(source[i]);
    }

    private static int ResolveObjectId(UnityEngine.Object value)
    {
        return value != null
            ? value.GetEntityId().GetHashCode()
            : 0;
    }
}
