using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Cache compartilhado das duas ondas de movimento. Somente snapshots
/// confirmados podem ser publicados ou consultados; resultados entregues aos
/// consumidores sao copias, portanto nenhuma alteracao local contamina a
/// entrada armazenada.
/// </summary>
public static class MovementReachCache
{
    private const int MaxEntries = 96;
    private const int MaxCellWeight = 120000;

    private enum QueryKind : byte
    {
        ValidPaths = 1,
        MovementCosts = 2
    }

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        public readonly QueryKind kind;
        public readonly int mapObjectId;
        public readonly int terrainDatabaseObjectId;
        public readonly int unitObjectId;
        public readonly int unitInstanceId;
        public readonly Vector3Int origin;
        public readonly int budget;
        public readonly int currentFuel;
        public readonly int movementProfileHash;
        public readonly Domain domain;
        public readonly HeightLevel height;
        public readonly TeamId team;
        public readonly int slotIndex;
        public readonly bool isEmbarked;
        public readonly bool layerAwareOccupancy;
        public readonly bool totalWar;
        public readonly int occupancyRevision;
        public readonly int topologyVersion;
        public readonly string topologyFingerprint;

        public CacheKey(
            QueryKind kind,
            Tilemap map,
            TerrainDatabase terrainDatabase,
            UnitManager unit,
            Vector3Int origin,
            int budget,
            int movementProfileHash,
            int occupancyRevision,
            BoardTopologyIndex topology)
        {
            origin.z = 0;
            this.kind = kind;
            mapObjectId = map.GetEntityId().GetHashCode();
            terrainDatabaseObjectId =
                terrainDatabase != null
                    ? terrainDatabase.GetEntityId().GetHashCode()
                    : 0;
            unitObjectId = unit.GetEntityId().GetHashCode();
            unitInstanceId = unit.InstanceId;
            this.origin = origin;
            this.budget = budget;
            currentFuel = Mathf.Max(0, unit.CurrentFuel);
            this.movementProfileHash = movementProfileHash;
            domain = unit.GetDomain();
            height = unit.GetHeightLevel();
            team = unit.TeamId;
            slotIndex = unit.SlotIndex;
            isEmbarked = unit.IsEmbarked;
            layerAwareOccupancy =
                OccupancyResolver.IsLayerAwareRulesActive;
            totalWar = UnitRulesDefinition.IsTotalWarEnabled();
            this.occupancyRevision = occupancyRevision;
            topologyVersion = topology.TopologyVersion;
            topologyFingerprint =
                topology.TopologyFingerprint ?? string.Empty;
        }

        public bool Equals(CacheKey other)
        {
            return kind == other.kind
                && mapObjectId == other.mapObjectId
                && terrainDatabaseObjectId
                    == other.terrainDatabaseObjectId
                && unitObjectId == other.unitObjectId
                && unitInstanceId == other.unitInstanceId
                && origin == other.origin
                && budget == other.budget
                && currentFuel == other.currentFuel
                && movementProfileHash == other.movementProfileHash
                && domain == other.domain
                && height == other.height
                && team == other.team
                && slotIndex == other.slotIndex
                && isEmbarked == other.isEmbarked
                && layerAwareOccupancy
                    == other.layerAwareOccupancy
                && totalWar == other.totalWar
                && occupancyRevision == other.occupancyRevision
                && topologyVersion == other.topologyVersion
                && string.Equals(
                    topologyFingerprint,
                    other.topologyFingerprint,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)kind;
                hash = (hash * 397) ^ mapObjectId;
                hash = (hash * 397) ^ terrainDatabaseObjectId;
                hash = (hash * 397) ^ unitObjectId;
                hash = (hash * 397) ^ unitInstanceId;
                hash = (hash * 397) ^ origin.GetHashCode();
                hash = (hash * 397) ^ budget;
                hash = (hash * 397) ^ currentFuel;
                hash = (hash * 397) ^ movementProfileHash;
                hash = (hash * 397) ^ (int)domain;
                hash = (hash * 397) ^ (int)height;
                hash = (hash * 397) ^ (int)team;
                hash = (hash * 397) ^ slotIndex;
                hash = (hash * 397) ^ (isEmbarked ? 1 : 0);
                hash = (hash * 397)
                    ^ (layerAwareOccupancy ? 1 : 0);
                hash = (hash * 397) ^ (totalWar ? 1 : 0);
                hash = (hash * 397) ^ occupancyRevision;
                hash = (hash * 397) ^ topologyVersion;
                hash = (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(
                        topologyFingerprint ?? string.Empty);
                return hash;
            }
        }
    }

    private sealed class CacheEntry
    {
        public Dictionary<Vector3Int, List<Vector3Int>> paths;
        public Dictionary<Vector3Int, int> costs;
        public LinkedListNode<CacheKey> lruNode;
        public int cellWeight;
    }

    private static readonly Dictionary<CacheKey, CacheEntry> Entries =
        new Dictionary<CacheKey, CacheEntry>();
    private static readonly LinkedList<CacheKey> Lru =
        new LinkedList<CacheKey>();
    private static int totalCellWeight;

    public static bool TryGetValidPaths(
        Tilemap map,
        UnitManager unit,
        int budget,
        TerrainDatabase terrainDatabase,
        out Dictionary<Vector3Int, List<Vector3Int>> result)
    {
        result = null;
        if (!TryBuildKey(
                QueryKind.ValidPaths,
                map,
                unit,
                unit != null
                    ? unit.CurrentCellPosition
                    : Vector3Int.zero,
                budget,
                terrainDatabase,
                out CacheKey key))
        {
            AIDecisionPerf.AddCount("MovementCacheBypasses");
            return false;
        }

        if (!Entries.TryGetValue(key, out CacheEntry entry)
            || entry.paths == null)
            return false;

        Touch(entry);
        result = ClonePaths(entry.paths);
        AIDecisionPerf.AddCount("MovementCacheHits");
        AIDecisionPerf.AddCount("ValidPathCacheHits");
        return true;
    }

    public static void StoreValidPaths(
        Tilemap map,
        UnitManager unit,
        int budget,
        TerrainDatabase terrainDatabase,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        if (paths == null
            || !TryBuildKey(
                QueryKind.ValidPaths,
                map,
                unit,
                unit != null
                    ? unit.CurrentCellPosition
                    : Vector3Int.zero,
                budget,
                terrainDatabase,
                out CacheKey key))
        {
            return;
        }

        Dictionary<Vector3Int, List<Vector3Int>> stored =
            ClonePaths(paths);
        int weight = EstimatePathWeight(stored);
        Store(
            key,
            new CacheEntry
            {
                paths = stored,
                cellWeight = weight
            });
    }

    public static bool TryGetMovementCosts(
        Tilemap map,
        UnitManager unit,
        Vector3Int origin,
        int budget,
        TerrainDatabase terrainDatabase,
        out Dictionary<Vector3Int, int> result)
    {
        result = null;
        if (!TryBuildKey(
                QueryKind.MovementCosts,
                map,
                unit,
                origin,
                budget,
                terrainDatabase,
                out CacheKey key))
        {
            AIDecisionPerf.AddCount("MovementCacheBypasses");
            return false;
        }

        if (!Entries.TryGetValue(key, out CacheEntry entry)
            || entry.costs == null)
            return false;

        Touch(entry);
        result = new Dictionary<Vector3Int, int>(entry.costs);
        AIDecisionPerf.AddCount("MovementCacheHits");
        AIDecisionPerf.AddCount("MovementCostCacheHits");
        return true;
    }

    public static void StoreMovementCosts(
        Tilemap map,
        UnitManager unit,
        Vector3Int origin,
        int budget,
        TerrainDatabase terrainDatabase,
        Dictionary<Vector3Int, int> costs)
    {
        if (costs == null
            || !TryBuildKey(
                QueryKind.MovementCosts,
                map,
                unit,
                origin,
                budget,
                terrainDatabase,
                out CacheKey key))
        {
            return;
        }

        var stored = new Dictionary<Vector3Int, int>(costs);
        Store(
            key,
            new CacheEntry
            {
                costs = stored,
                cellWeight = Mathf.Max(1, stored.Count)
            });
    }

    public static void InvalidateForMap(Tilemap map)
    {
        if (map == null || Entries.Count == 0)
            return;

        int mapObjectId = map.GetEntityId().GetHashCode();
        var stale = new List<CacheKey>();
        foreach (KeyValuePair<CacheKey, CacheEntry> pair in Entries)
        {
            if (pair.Key.mapObjectId == mapObjectId)
                stale.Add(pair.Key);
        }

        for (int i = 0; i < stale.Count; i++)
            Remove(stale[i]);
        if (stale.Count > 0)
            AIDecisionPerf.AddCount(
                "MovementCacheInvalidatedEntries",
                stale.Count);
    }

    public static void ClearAll()
    {
        int count = Entries.Count;
        Entries.Clear();
        Lru.Clear();
        totalCellWeight = 0;
        if (count > 0)
            AIDecisionPerf.AddCount(
                "MovementCacheInvalidatedEntries",
                count);
    }

    private static bool TryBuildKey(
        QueryKind kind,
        Tilemap map,
        UnitManager unit,
        Vector3Int origin,
        int budget,
        TerrainDatabase terrainDatabase,
        out CacheKey key)
    {
        key = default;
        if (!Application.isPlaying
            || map == null
            || unit == null
            || budget < 0
            || !BoardTopologyIndex.TryGetFor(
                map,
                out BoardTopologyIndex topology)
            || topology == null
            || !topology.IsReady
            || !ConfirmedOccupancyIndex.TryGetFor(
                map,
                out ConfirmedOccupancyIndex occupancy)
            || occupancy == null
            || !occupancy.CanServeLiveQueries
            || !occupancy.TryGetRecord(
                unit,
                out ConfirmedUnitOccupancyRecord confirmed))
        {
            return false;
        }

        Vector3Int liveCell = unit.CurrentCellPosition;
        liveCell.z = 0;
        if (confirmed.cell != liveCell
            || confirmed.domain != unit.GetDomain()
            || confirmed.height != unit.GetHeightLevel()
            || confirmed.slotIndex != unit.SlotIndex
            || confirmed.team != unit.TeamId
            || confirmed.isEmbarked != unit.IsEmbarked)
        {
            return false;
        }

        key = new CacheKey(
            kind,
            map,
            terrainDatabase,
            unit,
            origin,
            budget,
            BuildMovementProfileHash(unit),
            occupancy.ConfirmedRevision,
            topology);
        return true;
    }

    private static int BuildMovementProfileHash(UnitManager unit)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + unit.GetMovementRange();
            hash = (hash * 31) + (int)unit.GetMovementCategory();

            IReadOnlyList<UnitLayerMode> modes =
                unit.GetAllLayerModes();
            for (int i = 0; i < modes.Count; i++)
            {
                hash = (hash * 31) + (int)modes[i].domain;
                hash = (hash * 31) + (int)modes[i].heightLevel;
            }

            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                return hash;

            hash = (hash * 31)
                + data.GetEntityId().GetHashCode();
            if (data.autonomyData != null)
            {
                hash = (hash * 31)
                    + data.autonomyData.GetEntityId().GetHashCode();
                hash = (hash * 31)
                    + data.autonomyData.movementAutonomyMultiplier;
            }

            if (data.skills == null)
                return hash;
            for (int i = 0; i < data.skills.Count; i++)
            {
                SkillData skill = data.skills[i];
                if (skill == null)
                    continue;
                hash = (hash * 31)
                    + skill.GetEntityId().GetHashCode();
                hash = (hash * 31)
                    + StringComparer.OrdinalIgnoreCase.GetHashCode(
                        skill.id ?? string.Empty);
            }
            return hash;
        }
    }

    private static void Store(CacheKey key, CacheEntry entry)
    {
        if (entry == null || entry.cellWeight <= 0)
            return;
        if (entry.cellWeight > MaxCellWeight)
        {
            AIDecisionPerf.AddCount(
                "MovementCacheOversizedSkips");
            return;
        }

        if (Entries.ContainsKey(key))
            Remove(key);

        entry.lruNode = Lru.AddFirst(key);
        Entries[key] = entry;
        totalCellWeight += entry.cellWeight;
        AIDecisionPerf.AddCount("MovementCacheStores");

        while (Entries.Count > MaxEntries
            || totalCellWeight > MaxCellWeight)
        {
            LinkedListNode<CacheKey> oldest = Lru.Last;
            if (oldest == null)
                break;
            Remove(oldest.Value);
            AIDecisionPerf.AddCount("MovementCacheEvictions");
        }
    }

    private static void Touch(CacheEntry entry)
    {
        if (entry?.lruNode == null
            || entry.lruNode.List != Lru
            || entry.lruNode == Lru.First)
        {
            return;
        }

        Lru.Remove(entry.lruNode);
        Lru.AddFirst(entry.lruNode);
    }

    private static void Remove(CacheKey key)
    {
        if (!Entries.TryGetValue(key, out CacheEntry entry))
            return;
        Entries.Remove(key);
        if (entry.lruNode != null && entry.lruNode.List == Lru)
            Lru.Remove(entry.lruNode);
        totalCellWeight = Mathf.Max(
            0,
            totalCellWeight - Mathf.Max(0, entry.cellWeight));
    }

    private static Dictionary<Vector3Int, List<Vector3Int>>
        ClonePaths(
            IReadOnlyDictionary<Vector3Int, List<Vector3Int>> source)
    {
        var clone =
            new Dictionary<Vector3Int, List<Vector3Int>>(source.Count);
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair
                 in source)
        {
            clone[pair.Key] = pair.Value != null
                ? new List<Vector3Int>(pair.Value)
                : new List<Vector3Int>();
        }
        return clone;
    }

    private static int EstimatePathWeight(
        IReadOnlyDictionary<Vector3Int, List<Vector3Int>> paths)
    {
        int weight = Mathf.Max(1, paths.Count);
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair
                 in paths)
        {
            if (pair.Value != null)
                weight += pair.Value.Count;
        }
        return weight;
    }
}
