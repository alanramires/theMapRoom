using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public sealed class FogRoundZeroDetectionBake
{
    public UnitManager target;
    public List<UnitManager> contributors = new List<UnitManager>();
}

/// <summary>
/// Fotografia persistente da rodada zero. Ela so muda quando o autor aciona
/// explicitamente o cozimento no MatchController.
/// </summary>
[Serializable]
public sealed class FogRoundZeroSlotBake
{
    public const int CurrentFormatVersion = 1;

    public int formatVersion = CurrentFormatVersion;
    public int observerSlotIndex = -1;
    public int sourceHash;
    public int sourceCacheFormat;
    public int sourceCacheConfigHash;
    public bool enableLos;
    public bool enableStealth;
    public string cookedAtUtc;
    public Tilemap boardMap;
    public List<FogSourceContributionSaveData> sourceContributions =
        new List<FogSourceContributionSaveData>();
    public List<Vector3Int> geographicallyVisibleCells =
        new List<Vector3Int>();
    public List<Vector3Int> sensorCoveredCells = new List<Vector3Int>();
    public List<Vector3Int> knownCells = new List<Vector3Int>();
    public List<Vector3Int> geographicOnlyCells = new List<Vector3Int>();
    public List<UnitManager> visibleEnemyUnits = new List<UnitManager>();
    public List<FogRoundZeroDetectionBake> detectionByTarget =
        new List<FogRoundZeroDetectionBake>();
    public List<UnitManager> constructionDetectedTargets =
        new List<UnitManager>();
}

/// <summary>
/// Fotografia instantanea e somente consultiva do conhecimento de um slot.
/// Nao representa memoria historica: KnownCells significa conhecido agora.
/// </summary>
public sealed class FogKnowledgeSnapshot
{
    public PlayerSlotId ObserverSlot { get; }
    public Tilemap BoardMap { get; }
    public int SourceHash { get; internal set; }

    public readonly HashSet<Vector3Int> GeographicallyVisibleCells =
        new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> SensorCoveredCells =
        new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> KnownCells =
        new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> GeographicOnlyCells =
        new HashSet<Vector3Int>();
    public readonly List<UnitManager> VisibleEnemyUnits =
        new List<UnitManager>();
    public readonly Dictionary<UnitManager, List<UnitManager>>
        DetectionContributorsByTarget =
            new Dictionary<UnitManager, List<UnitManager>>();
    public readonly Dictionary<Vector3Int, List<UnitManager>>
        VisibilityContributorsByCell =
            new Dictionary<Vector3Int, List<UnitManager>>();
    public readonly HashSet<UnitManager> ConstructionDetectedTargets =
        new HashSet<UnitManager>();

    public FogKnowledgeSnapshot(PlayerSlotId observerSlot, Tilemap boardMap)
    {
        ObserverSlot = observerSlot;
        BoardMap = boardMap;
    }

    public bool IsEnemyVisible(UnitManager target) =>
        target != null && VisibleEnemyUnits.Contains(target);

    public bool TryGetDetectionContributors(
        UnitManager target,
        out IReadOnlyList<UnitManager> contributors)
    {
        if (target != null &&
            DetectionContributorsByTarget.TryGetValue(
                target,
                out List<UnitManager> found))
        {
            contributors = found;
            return found.Count > 0;
        }

        contributors = Array.Empty<UnitManager>();
        return false;
    }

    public bool TryGetVisibilityContributors(
        Vector3Int cell,
        out IReadOnlyList<UnitManager> contributors)
    {
        cell.z = 0;
        if (VisibilityContributorsByCell.TryGetValue(
                cell,
                out List<UnitManager> found))
        {
            contributors = found;
            return found.Count > 0;
        }

        contributors = Array.Empty<UnitManager>();
        return false;
    }

    internal void AddVisibilityContributor(
        Vector3Int cell,
        UnitManager contributor)
    {
        if (contributor == null)
            return;
        cell.z = 0;
        if (!VisibilityContributorsByCell.TryGetValue(
                cell,
                out List<UnitManager> contributors))
        {
            contributors = new List<UnitManager>();
            VisibilityContributorsByCell[cell] = contributors;
        }
        if (!contributors.Contains(contributor))
            contributors.Add(contributor);
    }
}

public sealed class FogKnowledgeBuildRequest
{
    public PlayerSlotId ObserverSlot = PlayerSlotId.Invalid;
    public Tilemap BoardMap;
    public TerrainDatabase TerrainDatabase;
    public DPQAirHeightConfig DpqAirHeightConfig;
    public bool EnableLos = true;
    public bool EnableStealth = true;
    public IReadOnlyList<UnitManager> Units;
    public IReadOnlyList<ConstructionManager> Constructions;
}

/// <summary>
/// Cozinha a mesma fotografia instantanea que alimenta o FOW, sem publicar
/// estado runtime, trocar slot ativo, pintar tilemap ou registrar inteligencia.
/// </summary>
public static class FogKnowledgeSnapshotBuilder
{
    public static bool TryBuild(
        FogKnowledgeBuildRequest request,
        out FogKnowledgeSnapshot snapshot,
        out string reason)
    {
        snapshot = null;
        reason = string.Empty;
        if (request == null || !request.ObserverSlot.IsValid)
        {
            reason = "Slot observador invalido.";
            return false;
        }
        if (request.BoardMap == null)
        {
            reason = "Tilemap do tabuleiro indisponivel.";
            return false;
        }
        if (request.TerrainDatabase == null)
        {
            reason = "Terrain Database indisponivel.";
            return false;
        }

        IReadOnlyList<UnitManager> units = request.Units ?? CollectUnits();
        IReadOnlyList<ConstructionManager> constructions =
            request.Constructions ?? CollectConstructions();
        PodeDetectarSensor.ClearRefreshScopedTerrainCache();
        snapshot = new FogKnowledgeSnapshot(
            request.ObserverSlot,
            request.BoardMap)
        {
            SourceHash = ComputeSourceHash(request, units, constructions)
        };

        var friendlyObservers = new List<UnitManager>();
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager observer = units[i];
            if (!IsEligibleFriendlyObserver(
                    observer,
                    request.ObserverSlot,
                    request.BoardMap))
            {
                continue;
            }

            friendlyObservers.Add(observer);
            var visible = new HashSet<Vector3Int>();
            PodeDetectarSensor.CollectVisibleCellsForFogOfWar(
                observer,
                request.BoardMap,
                request.TerrainDatabase,
                visible,
                request.DpqAirHeightConfig,
                request.EnableLos);
            snapshot.GeographicallyVisibleCells.UnionWith(visible);
            snapshot.SensorCoveredCells.UnionWith(visible);
            snapshot.KnownCells.UnionWith(visible);
            foreach (Vector3Int cell in visible)
                snapshot.AddVisibilityContributor(cell, observer);
            // DETECCAO NAO REVELA FOW. O conhecimento aereo especializado
            // (EWACS) fazia aeronave aparecer, e era despejado aqui dentro do
            // mesmo balde que pinta terreno. O bake segue o runtime: quem
            // revela hexagono e o campo visao, pelo PodeEnxergar acima.
        }

        AddConstructionKnowledge(request, constructions, snapshot);
        CollectVisibleEnemies(
            request,
            units,
            constructions,
            friendlyObservers,
            snapshot);

        snapshot.GeographicOnlyCells.UnionWith(
            snapshot.GeographicallyVisibleCells);
        snapshot.GeographicOnlyCells.ExceptWith(snapshot.SensorCoveredCells);
        snapshot.VisibleEnemyUnits.Sort(CompareUnits);
        reason =
            $"slot={request.ObserverSlot.Value} " +
            $"observers={friendlyObservers.Count} " +
            $"known={snapshot.KnownCells.Count} " +
            $"visibleEnemies={snapshot.VisibleEnemyUnits.Count}";
        return true;
    }

    public static int ComputeSourceHash(FogKnowledgeBuildRequest request)
    {
        if (request == null)
            return 0;
        return ComputeSourceHash(
            request,
            request.Units ?? CollectUnits(),
            request.Constructions ?? CollectConstructions());
    }

    private static int ComputeSourceHash(
        FogKnowledgeBuildRequest request,
        IReadOnlyList<UnitManager> units,
        IReadOnlyList<ConstructionManager> constructions)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + request.ObserverSlot.Value;
            hash = hash * 31 + (request.BoardMap != null
                ? request.BoardMap.GetEntityId().GetHashCode()
                : 0);
            hash = hash * 31 + (request.TerrainDatabase != null
                ? request.TerrainDatabase.GetEntityId().GetHashCode()
                : 0);
            hash = hash * 31 + (request.DpqAirHeightConfig != null
                ? request.DpqAirHeightConfig.GetEntityId().GetHashCode()
                : 0);
            hash = hash * 31 + (request.EnableLos ? 1 : 0);
            hash = hash * 31 + (request.EnableStealth ? 1 : 0);

            for (int i = 0; i < units.Count; i++)
            {
                UnitManager unit = units[i];
                if (unit == null)
                    continue;
                Vector3Int cell = unit.CurrentCellPosition;
                hash = hash * 31 + unit.GetEntityId().GetHashCode();
                hash = hash * 31 + unit.SlotIndex;
                hash = hash * 31 + cell.x;
                hash = hash * 31 + cell.y;
                hash = hash * 31 + (int)unit.GetDomain();
                hash = hash * 31 + (int)unit.GetHeightLevel();
                hash = hash * 31 + (unit.gameObject.activeInHierarchy ? 1 : 0);
                hash = hash * 31 + (unit.IsEmbarked ? 1 : 0);
                hash = hash * 31 + (unit.IsDead ? 1 : 0);
                hash = hash * 31 + (unit.HasFiredThisTurn ? 1 : 0);
                hash = hash * 31 + (unit.HasPendingForcedLayerLock ? 1 : 0);
                if (unit.TryGetUnitData(out UnitData data) && data != null)
                    hash = hash * 31 + data.GetEntityId().GetHashCode();
            }

            for (int i = 0; i < constructions.Count; i++)
            {
                ConstructionManager construction = constructions[i];
                if (construction == null)
                    continue;
                Vector3Int cell = construction.CurrentCellPosition;
                hash = hash * 31 + construction.GetEntityId().GetHashCode();
                hash = hash * 31 + construction.SlotIndex;
                hash = hash * 31 + cell.x;
                hash = hash * 31 + cell.y;
                hash = hash * 31 +
                    (construction.gameObject.activeInHierarchy ? 1 : 0);
                hash = hash * 31 +
                    (construction.IsPlayerHeadQuarter ? 1 : 0);
                if (construction.TryResolveConstructionData(
                        out ConstructionData data) && data != null)
                {
                    hash = hash * 31 + data.GetEntityId().GetHashCode();
                    hash = hash * 31 + data.visao;
                }
            }

            return hash;
        }
    }

    private static void AddSpecializedAirKnowledge(
        FogKnowledgeBuildRequest request,
        UnitManager observer,
        HashSet<Vector3Int> output)
    {
        if (!observer.TryGetUnitData(out UnitData data) || data == null ||
            data.visionSpecializations == null)
        {
            return;
        }

        bool airLowAdded = false;
        bool airHighAdded = false;
        for (int i = 0; i < data.visionSpecializations.Count; i++)
        {
            UnitVisionException specialization = data.visionSpecializations[i];
            if (specialization == null || specialization.domain != Domain.Air)
                continue;

            HeightLevel height = specialization.heightLevel;
            if (height == HeightLevel.AirLow && airLowAdded)
                continue;
            if (height == HeightLevel.AirHigh && airHighAdded)
                continue;
            if (height != HeightLevel.AirLow && height != HeightLevel.AirHigh)
                continue;

            PodeDetectarSensor.CollectVisibleCells(
                observer,
                request.BoardMap,
                request.TerrainDatabase,
                output,
                request.DpqAirHeightConfig,
                request.EnableLos,
                enableSpotter: false,
                useOccupantLayerForTarget: false,
                preserveObserverLayerRangeForHexVisibility: false,
                forceVirtualTargetLayer: true,
                forcedVirtualTargetDomain: Domain.Air,
                forcedVirtualTargetHeight: height,
                useRangeOnlyForAirHighWhenConfigured: true);

            if (height == HeightLevel.AirLow)
                airLowAdded = true;
            else
                airHighAdded = true;
        }
    }

    private static void AddConstructionKnowledge(
        FogKnowledgeBuildRequest request,
        IReadOnlyList<ConstructionManager> constructions,
        FogKnowledgeSnapshot snapshot)
    {
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (!IsConstructionOnBoard(construction, request.BoardMap))
                continue;

            bool owned = construction.SlotIndex == request.ObserverSlot.Value;
            if (!owned && !construction.IsPlayerHeadQuarter)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (!request.BoardMap.HasTile(cell))
                continue;

            if (!owned)
            {
                snapshot.GeographicallyVisibleCells.Add(cell);
                snapshot.KnownCells.Add(cell);
                continue;
            }

            int range = 0;
            if (construction.TryResolveConstructionData(
                    out ConstructionData data) && data != null)
            {
                range = Mathf.Max(0, data.visao);
            }

            HashSet<Vector3Int> cells = BuildCellsInRadius(
                request.BoardMap,
                cell,
                range);
            snapshot.GeographicallyVisibleCells.UnionWith(cells);
            snapshot.KnownCells.UnionWith(cells);
            snapshot.SensorCoveredCells.Add(cell);
        }
    }

    private static void CollectVisibleEnemies(
        FogKnowledgeBuildRequest request,
        IReadOnlyList<UnitManager> units,
        IReadOnlyList<ConstructionManager> constructions,
        IReadOnlyList<UnitManager> friendlyObservers,
        FogKnowledgeSnapshot snapshot)
    {
        var visibleSet = new HashSet<UnitManager>();
        var detectedStealth = new List<PodeDetectarOption>();
        var undetectedStealth = new List<PodeDetectarOption>();
        var spotted = new List<PodeDetectarOption>();
        var blocked = new List<PodeDetectarOption>();

        for (int i = 0; i < friendlyObservers.Count; i++)
        {
            UnitManager observer = friendlyObservers[i];
            PodeDetectarSensor.CollectDetection(
                observer,
                request.BoardMap,
                request.TerrainDatabase,
                detectedStealth,
                undetectedStealth,
                spotted,
                blocked,
                out _,
                request.DpqAirHeightConfig,
                request.EnableLos,
                enableSpotter: false,
                enableStealthValidation: request.EnableStealth);
            RegisterDetectedOptions(
                observer,
                detectedStealth,
                visibleSet,
                snapshot);
            RegisterDetectedOptions(observer, spotted, visibleSet, snapshot);
        }

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager target = units[i];
            if (!IsEligibleEnemyTarget(
                    target,
                    request.ObserverSlot,
                    request.BoardMap))
            {
                continue;
            }

            if (IsOnFriendlyConstruction(
                    target,
                    request.ObserverSlot,
                    request.BoardMap,
                    constructions))
            {
                visibleSet.Add(target);
                snapshot.ConstructionDetectedTargets.Add(target);
                continue;
            }

            bool stealthTemporarilyRevealed =
                target.HasFiredThisTurn || target.HasPendingForcedLayerLock;
            if (!visibleSet.Contains(target) && stealthTemporarilyRevealed &&
                PodeDetectarSensor.IsTargetObservedByTeamWithoutForwardObserver(
                    target,
                    request.ObserverSlot.Value,
                    request.BoardMap,
                    request.TerrainDatabase,
                    request.DpqAirHeightConfig,
                    request.EnableLos,
                    enableStealthValidation: false))
            {
                visibleSet.Add(target);
            }
        }

        snapshot.VisibleEnemyUnits.AddRange(visibleSet);
    }

    private static void RegisterDetectedOptions(
        UnitManager observer,
        List<PodeDetectarOption> options,
        HashSet<UnitManager> visibleSet,
        FogKnowledgeSnapshot snapshot)
    {
        for (int i = 0; i < options.Count; i++)
        {
            UnitManager target = options[i]?.targetUnit;
            if (target == null)
                continue;
            visibleSet.Add(target);
            if (!snapshot.DetectionContributorsByTarget.TryGetValue(
                    target,
                    out List<UnitManager> contributors))
            {
                contributors = new List<UnitManager>();
                snapshot.DetectionContributorsByTarget[target] = contributors;
            }
            if (observer != null && !contributors.Contains(observer))
                contributors.Add(observer);
        }
    }

    private static bool IsEligibleFriendlyObserver(
        UnitManager unit,
        PlayerSlotId observerSlot,
        Tilemap boardMap)
    {
        return unit != null && unit.gameObject.activeInHierarchy &&
            !unit.IsEmbarked && !unit.IsDead &&
            unit.SlotIndex == observerSlot.Value &&
            IsUnitOnBoard(unit, boardMap);
    }

    private static bool IsEligibleEnemyTarget(
        UnitManager unit,
        PlayerSlotId observerSlot,
        Tilemap boardMap)
    {
        return unit != null && unit.gameObject.activeInHierarchy &&
            !unit.IsEmbarked && !unit.IsDead &&
            PlayerSlotRelations.AreEnemies(observerSlot.Value, unit.SlotIndex) &&
            IsUnitOnBoard(unit, boardMap);
    }

    private static bool IsUnitOnBoard(UnitManager unit, Tilemap boardMap)
    {
        return unit != null && boardMap != null &&
            unit.BoardTilemap == boardMap &&
            unit.gameObject.scene == boardMap.gameObject.scene;
    }

    private static bool IsConstructionOnBoard(
        ConstructionManager construction,
        Tilemap boardMap)
    {
        if (construction == null || boardMap == null ||
            !construction.gameObject.activeInHierarchy ||
            construction.gameObject.scene != boardMap.gameObject.scene)
        {
            return false;
        }

        return construction.BoardTilemap == null ||
            construction.BoardTilemap == boardMap;
    }

    private static bool IsOnFriendlyConstruction(
        UnitManager target,
        PlayerSlotId observerSlot,
        Tilemap boardMap,
        IReadOnlyList<ConstructionManager> constructions)
    {
        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (!IsConstructionOnBoard(construction, boardMap) ||
                construction.SlotIndex != observerSlot.Value)
            {
                continue;
            }
            Vector3Int constructionCell = construction.CurrentCellPosition;
            constructionCell.z = 0;
            if (constructionCell == targetCell)
                return true;
        }
        return false;
    }

    private static HashSet<Vector3Int> BuildCellsInRadius(
        Tilemap map,
        Vector3Int origin,
        int radius)
    {
        var visited = new HashSet<Vector3Int>();
        if (map == null || radius < 0)
            return visited;

        origin.z = 0;
        var queue = new Queue<Vector3Int>();
        var distance = new Dictionary<Vector3Int, int>();
        var neighbors = new List<Vector3Int>(6);
        queue.Enqueue(origin);
        visited.Add(origin);
        distance[origin] = 0;
        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int currentDistance = distance[current];
            if (currentDistance >= radius)
                continue;

            neighbors.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(
                map,
                current,
                neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (!map.HasTile(next) || !visited.Add(next))
                    continue;
                distance[next] = currentDistance + 1;
                queue.Enqueue(next);
            }
        }
        return visited;
    }

    private static IReadOnlyList<UnitManager> CollectUnits()
    {
        if (Application.isPlaying && UnitManager.AllActive.Count > 0)
            return UnitManager.AllActive;
        return UnityEngine.Object.FindObjectsByType<UnitManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
    }

    private static IReadOnlyList<ConstructionManager> CollectConstructions()
    {
        if (Application.isPlaying && ConstructionManager.AllActive.Count > 0)
            return ConstructionManager.AllActive;
        return UnityEngine.Object.FindObjectsByType<ConstructionManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
    }

    private static int CompareUnits(UnitManager a, UnitManager b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;
        int slot = a.SlotIndex.CompareTo(b.SlotIndex);
        if (slot != 0)
            return slot;
        return a.GetEntityId().GetHashCode().CompareTo(
            b.GetEntityId().GetHashCode());
    }
}
