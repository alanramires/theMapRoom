using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum UnitThreatEnvelopeMovement
{
    Potential = 0,
    CurrentTurn = 1
}

public sealed class UnitThreatEnvelope
{
    public readonly Dictionary<Vector3Int, List<Vector3Int>> PathsByDestination;
    public readonly HashSet<Vector3Int> MovementCells;
    public readonly HashSet<Vector3Int> AttackableCells;
    public readonly List<Vector3Int> RangeCells;
    public readonly List<Vector3Int> LineCells;

    internal UnitThreatEnvelope(
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> movementCells,
        HashSet<Vector3Int> attackableCells,
        HashSet<Vector3Int> lineCells)
    {
        PathsByDestination = paths ?? new Dictionary<Vector3Int, List<Vector3Int>>();
        MovementCells = movementCells ?? new HashSet<Vector3Int>();
        AttackableCells = attackableCells ?? new HashSet<Vector3Int>();
        RangeCells = new List<Vector3Int>(MovementCells);
        LineCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>();
    }

    public bool CanThreaten(Vector3Int cell)
    {
        cell.z = 0;
        return AttackableCells.Contains(cell);
    }
}

/// <summary>
/// Shared source of truth for the hotzone shown to players and consumed by AI.
/// It calculates movement plus every cell that can be fired upon after a legal move.
/// </summary>
public static class UnitThreatEnvelopeService
{
    private enum ThreatProfile
    {
        None,
        Movement,
        DistanceStatic,
        Hybrid
    }

    private sealed class CacheEntry
    {
        public int KeyHash;
        public UnitThreatEnvelope Envelope;
    }

    private static readonly Dictionary<long, CacheEntry> Cache = new Dictionary<long, CacheEntry>();

    /// <summary>
    /// Constroi a hotzone de uma ferramenta de servico a partir dos mesmos
    /// destinos de movimento legais usados pela hotzone de combate. Nao usa
    /// distancia-hexa a partir da origem: terreno, estradas, ocupacao e custos
    /// ja estao materializados em paths.
    /// </summary>
    public static UnitThreatEnvelope BuildServiceEnvelope(
        UnitManager unit,
        Tilemap boardMap,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        SupplierRangeMode serviceRange)
    {
        var normalizedPaths = paths ??
            new Dictionary<Vector3Int, List<Vector3Int>>();
        var movementCells = new HashSet<Vector3Int>();
        var serviceableCells = new HashSet<Vector3Int>();
        var lineCells = new HashSet<Vector3Int>();
        Tilemap map = boardMap != null ? boardMap : unit != null ? unit.BoardTilemap : null;
        var neighbors = new List<Vector3Int>(6);

        foreach (Vector3Int rawCell in normalizedPaths.Keys)
        {
            Vector3Int moveCell = rawCell;
            moveCell.z = 0;
            if (map != null && map.GetTile(moveCell) == null)
                continue;

            movementCells.Add(moveCell);
            if (serviceRange == SupplierRangeMode.Hybrid0Or1Hex)
                serviceableCells.Add(moveCell);

            if (serviceRange != SupplierRangeMode.Adjacent1Hex &&
                serviceRange != SupplierRangeMode.Hybrid0Or1Hex)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(map, moveCell, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int targetCell = neighbors[i];
                targetCell.z = 0;
                if (map == null || map.GetTile(targetCell) != null)
                    serviceableCells.Add(targetCell);
            }
        }

        lineCells.UnionWith(serviceableCells);
        lineCells.ExceptWith(movementCells);
        return new UnitThreatEnvelope(
            normalizedPaths,
            movementCells,
            serviceableCells,
            lineCells);
    }

    /// <summary>
    /// Hotzone especializada de fusao. O alvo nao fica a "+1 gratis" do
    /// movimento: a unidade precisa conservar MP suficiente para entrar no
    /// hex ocupado pelo receptor. Assim, o envelope perde 1 em planicie, 2 em
    /// floresta/montanha, ou o custo efetivo resolvido pelas skills da unidade.
    /// </summary>
    public static UnitThreatEnvelope BuildFusionEnvelope(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        var normalizedPaths = paths ?? new Dictionary<Vector3Int, List<Vector3Int>>();
        var movementCells = new HashSet<Vector3Int>();
        var fusionCells = new HashSet<Vector3Int>();
        var lineCells = new HashSet<Vector3Int>();
        Tilemap map = boardMap != null ? boardMap : unit != null ? unit.BoardTilemap : null;
        if (unit == null || map == null)
            return new UnitThreatEnvelope(normalizedPaths, movementCells, fusionCells, lineCells);

        int totalMovement = Mathf.Max(0, unit.RemainingMovementPoints);
        var neighbors = new List<Vector3Int>(6);
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in normalizedPaths)
        {
            Vector3Int moveCell = pair.Key;
            moveCell.z = 0;
            if (map.GetTile(moveCell) == null)
                continue;

            int moveCost = pair.Value != null && pair.Value.Count > 0
                ? Mathf.Max(0, UnitMovementPathRules.CalculateAutonomyCostForPath(
                    map,
                    unit,
                    pair.Value,
                    terrainDatabase,
                    applyOperationalAutonomyModifier: false))
                : 0;
            int remaining = Mathf.Max(0, totalMovement - moveCost);
            movementCells.Add(moveCell);
            if (remaining <= 0)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(map, moveCell, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int targetCell = neighbors[i];
                targetCell.z = 0;
                if (map.GetTile(targetCell) == null)
                    continue;
                if (!PodeFundirSensor.TryResolveMergeEnterCost(
                        map,
                        terrainDatabase,
                        unit,
                        targetCell,
                        out int enterCost,
                        out _))
                {
                    continue;
                }
                if (remaining >= Mathf.Max(1, enterCost))
                    fusionCells.Add(targetCell);
            }
        }

        lineCells.UnionWith(fusionCells);
        lineCells.ExceptWith(movementCells);
        return new UnitThreatEnvelope(
            normalizedPaths,
            movementCells,
            fusionCells,
            lineCells);
    }

    public static void ClearCache()
    {
        Cache.Clear();
    }

    public static bool TryGet(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        UnitThreatEnvelopeMovement movementMode,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLdt,
        bool enableLos,
        bool enableSpotter,
        out UnitThreatEnvelope envelope,
        out bool cacheHit)
    {
        envelope = null;
        cacheHit = false;
        if (unit == null)
            return false;

        Tilemap map = boardMap != null ? boardMap : unit.BoardTilemap;
        if (map == null)
            return false;

        ThreatProfile profile = ResolveProfile(unit);
        if (profile == ThreatProfile.None)
            return false;

        int movementSteps = movementMode == UnitThreatEnvelopeMovement.CurrentTurn
            ? Mathf.Max(0, unit.RemainingMovementPoints)
            : Mathf.Max(0, unit.MaxMovementPoints);
        long cacheIndex = ((long)ResolveUnitIndex(unit) << 2) | (uint)movementMode;
        int keyHash = BuildKeyHash(unit, map, movementSteps, enableLdt, enableLos, enableSpotter);
        if (Cache.TryGetValue(cacheIndex, out CacheEntry existing)
            && existing != null
            && existing.KeyHash == keyHash
            && existing.Envelope != null)
        {
            envelope = existing.Envelope;
            cacheHit = true;
            return true;
        }

        bool includeStatic = profile == ThreatProfile.DistanceStatic || profile == ThreatProfile.Hybrid;
        bool includeMovement = profile == ThreatProfile.Movement || profile == ThreatProfile.Hybrid;
        var staticThreat = new HashSet<Vector3Int>();
        var mobileThreat = new HashSet<Vector3Int>();
        var movementCells = new HashSet<Vector3Int>();
        var paths = new Dictionary<Vector3Int, List<Vector3Int>>();

        if (includeStatic)
        {
            PodeMirarSensor.CollectValidFireCellsFromOrigin(
                unit, map, terrainDatabase, SensorMovementMode.MoveuParado,
                unit.CurrentCellPosition, staticThreat, dpqAirHeightConfig,
                enableLdt, enableLos, enableSpotter);
            RemoveCellsOutsideBoard(map, staticThreat);
        }

        if (includeMovement)
        {
            paths = UnitMovementPathRules.CalcularCaminhosValidos(
                map, unit, movementSteps, terrainDatabase);
            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (map.GetTile(cell) != null)
                    movementCells.Add(cell);
            }

            Vector3Int origin = unit.CurrentCellPosition;
            origin.z = 0;
            if (map.GetTile(origin) != null)
                movementCells.Add(origin);

            var localThreat = new HashSet<Vector3Int>();
            foreach (Vector3Int moveCell in movementCells)
            {
                // Nao pular celulas de movimento que caiam dentro do staticThreat: uma
                // coisa e a celula estar no alcance parado, outra e tudo que se atinge
                // DE LA ja estar coberto. Numa hibrida (min 1 / max 2) o tiro pos-movimento
                // colapsa para alcance 1, entao a partir de uma celula na borda do alcance
                // parado ela alcanca alvos que o tiro parado nao alcanca. Pular aqui abria
                // um buraco no envelope e o CanThreaten barrava o ataque antes do sensor:
                // a artilheira combatente parava ao lado do alvo sem atirar.
                localThreat.Clear();
                PodeMirarSensor.CollectValidFireCellsFromOrigin(
                    unit, map, terrainDatabase, SensorMovementMode.MoveuAndando,
                    moveCell, localThreat, dpqAirHeightConfig,
                    enableLdt, enableLos, enableSpotter);
                foreach (Vector3Int rawTarget in localThreat)
                {
                    Vector3Int target = rawTarget;
                    target.z = 0;
                    if (map.GetTile(target) != null)
                        mobileThreat.Add(target);
                }
            }
        }

        var attackable = new HashSet<Vector3Int>(movementCells);
        attackable.UnionWith(staticThreat);
        attackable.UnionWith(mobileThreat);

        var lineCells = new HashSet<Vector3Int>(staticThreat);
        lineCells.UnionWith(mobileThreat);
        lineCells.ExceptWith(movementCells);

        envelope = new UnitThreatEnvelope(paths, movementCells, attackable, lineCells);
        Cache[cacheIndex] = new CacheEntry { KeyHash = keyHash, Envelope = envelope };
        return true;
    }

    private static void RemoveCellsOutsideBoard(Tilemap map, HashSet<Vector3Int> cells)
    {
        cells.RemoveWhere(cell => map.GetTile(cell) == null);
    }

    private static ThreatProfile ResolveProfile(UnitManager unit)
    {
        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit != null ? unit.GetEmbarkedWeapons() : null;
        if (weapons == null || weapons.Count == 0)
            return ThreatProfile.None;

        bool hasOne = false;
        bool hasLong = false;
        bool hasHybrid = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null || embarked.squadAmmunition <= 0)
                continue;
            if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                    embarked,
                    SensorMovementMode.MoveuParado,
                    requireAmmo: false,
                    out int min,
                    out int max))
                continue;
            hasOne |= min == 1 && max == 1;
            hasLong |= max > 1;
            hasHybrid |= min == 1 && max > 1;
        }

        if (hasHybrid || (hasOne && hasLong)) return ThreatProfile.Hybrid;
        if (hasLong) return ThreatProfile.DistanceStatic;
        if (hasOne) return ThreatProfile.Movement;
        return ThreatProfile.None;
    }

    private static int ResolveUnitIndex(UnitManager unit)
    {
        return unit.InstanceId > 0 ? unit.InstanceId : unit.GetEntityId().GetHashCode();
    }

    private static int BuildKeyHash(
        UnitManager unit,
        Tilemap map,
        int movementSteps,
        bool enableLdt,
        bool enableLos,
        bool enableSpotter)
    {
        unchecked
        {
            int hash = 17;
            Vector3Int cell = unit.CurrentCellPosition;
            hash = hash * 31 + cell.x;
            hash = hash * 31 + cell.y;
            hash = hash * 31 + movementSteps;
            hash = hash * 31 + unit.CurrentFuel;
            hash = hash * 31 + (int)unit.GetDomain();
            hash = hash * 31 + (int)unit.GetHeightLevel();
            hash = hash * 31 + (unit.IsAircraftGrounded ? 1 : 0);
            hash = hash * 31 + map.GetEntityId().GetHashCode();
            hash = hash * 31 + ThreatRevisionTracker.GlobalBoardRevision;
            hash = hash * 31 + ThreatRevisionTracker.GetSlotObserverRevision(PlayerSlotId.FromIndex(unit.SlotIndex));
            hash = hash * 31 + ThreatRevisionTracker.MatchFlagsHash;
            hash = hash * 31 + (enableLdt ? 1 : 0);
            hash = hash * 31 + (enableLos ? 1 : 0);
            hash = hash * 31 + (enableSpotter ? 1 : 0);

            IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
            int count = weapons != null ? weapons.Count : 0;
            hash = hash * 31 + count;
            for (int i = 0; i < count; i++)
            {
                UnitEmbarkedWeapon embarked = weapons[i];
                if (embarked == null)
                {
                    hash = hash * 31 + 7;
                    continue;
                }
                hash = hash * 31 + (embarked.weapon != null ? embarked.weapon.GetEntityId().GetHashCode() : 0);
                hash = hash * 31 + embarked.squadAmmunition;
                hash = hash * 31 + (int)embarked.selectedTrajectory;
            }
            return hash;
        }
    }
}
