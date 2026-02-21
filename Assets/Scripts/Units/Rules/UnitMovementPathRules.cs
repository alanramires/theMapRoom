using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class UnitMovementPathRules
{
    private const int RoadBonusMinBaseMove = 4;

    public static Dictionary<Vector3Int, List<Vector3Int>> CalcularCaminhosValidos(
        Tilemap terrainTilemap,
        UnitManager unit,
        int maxSteps,
        TerrainDatabase terrainDatabase = null)
    {
        Dictionary<Vector3Int, List<Vector3Int>> pathsByDestination = new Dictionary<Vector3Int, List<Vector3Int>>();
        if (terrainTilemap == null || unit == null || maxSteps < 0)
            return pathsByDestination;

        // Garante que o estado de camada usado na validacao vem da instancia em campo.
        unit.SyncLayerStateFromData(forceNativeDefault: false);
        int maxMovementCost = Mathf.Max(0, maxSteps);
        int maxAutonomyCost = Mathf.Max(0, unit.CurrentFuel);
        bool canUseRoadBonus = CanUseRoadFullMoveBonus(unit, maxMovementCost);

        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;
        MovementQueryCache cache = new MovementQueryCache(terrainTilemap, terrainDatabase);

        Queue<PathNodeKey> frontier = new Queue<PathNodeKey>();
        Dictionary<PathNodeKey, int> autonomyCostByState = new Dictionary<PathNodeKey, int>();
        Dictionary<PathNodeKey, PathNodeKey> cameFrom = new Dictionary<PathNodeKey, PathNodeKey>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        PathNodeKey originKey = new PathNodeKey(origin, 0, usedFreeRoadBonusStep: false, roadOnlyUntilBaseMove: true);
        frontier.Enqueue(originKey);
        autonomyCostByState[originKey] = 0;
        cameFrom[originKey] = originKey;

        while (frontier.Count > 0)
        {
            PathNodeKey currentKey = frontier.Dequeue();
            Vector3Int current = currentKey.cell;
            int currentSteps = currentKey.steps;
            int currentAutonomyCost = autonomyCostByState[currentKey];

            GetImmediateHexNeighbors(terrainTilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                ConstructionManager construction = cache.GetConstructionAtCell(next);
                StructureData structure = cache.GetStructureAtCell(next);
                TerrainTypeData terrainData = cache.ResolveTerrainAtCell(next);
                bool hasAnyTile = cache.HasAnyPaintedTileAtCell(next);
                if (!CanTraverseCell(construction, structure, terrainData, hasAnyTile, terrainDatabase != null, unit))
                    continue;
                int autonomyCostToEnter = GetAutonomyCostToEnterCell(construction, structure, terrainData, unit);
                bool nextIsRoadBoost = cache.IsRoadBoostCell(next);

                bool useFreeRoadBonusStep =
                    canUseRoadBonus &&
                    !currentKey.usedFreeRoadBonusStep &&
                    currentKey.roadOnlyUntilBaseMove &&
                    currentSteps == maxMovementCost &&
                    nextIsRoadBoost;

                int movementCostToEnter = useFreeRoadBonusStep ? 0 : autonomyCostToEnter;
                int autonomyCostDelta = useFreeRoadBonusStep ? 0 : autonomyCostToEnter;
                int nextStep = currentSteps + movementCostToEnter;
                if (nextStep > maxMovementCost)
                    continue;
                bool nextRoadOnlyUntilBaseMove = currentKey.roadOnlyUntilBaseMove;
                if (currentSteps < maxMovementCost && !nextIsRoadBoost)
                    nextRoadOnlyUntilBaseMove = false;

                PathNodeKey nextKey = new PathNodeKey(
                    next,
                    nextStep,
                    usedFreeRoadBonusStep: currentKey.usedFreeRoadBonusStep || useFreeRoadBonusStep,
                    roadOnlyUntilBaseMove: nextRoadOnlyUntilBaseMove);
                int totalAutonomyCost = currentAutonomyCost + autonomyCostDelta;
                if (totalAutonomyCost > maxAutonomyCost)
                    continue;

                UnitManager blocker = cache.GetUnitAtCell(next, unit);
                if (blocker != null)
                    blocker.SyncLayerStateFromData(forceNativeDefault: false);
                if (!UnitRulesDefinition.CanPassThrough(unit, blocker))
                    continue;

                if (autonomyCostByState.TryGetValue(nextKey, out int knownCost) && knownCost <= totalAutonomyCost)
                    continue;

                autonomyCostByState[nextKey] = totalAutonomyCost;
                cameFrom[nextKey] = currentKey;
                frontier.Enqueue(nextKey);
            }
        }

        Dictionary<Vector3Int, PathNodeKey> bestStateByDestination = new Dictionary<Vector3Int, PathNodeKey>();
        foreach (KeyValuePair<PathNodeKey, int> pair in autonomyCostByState)
        {
            PathNodeKey candidateState = pair.Key;
            int candidateCost = pair.Value;

            if (!bestStateByDestination.TryGetValue(candidateState.cell, out PathNodeKey currentBest))
            {
                bestStateByDestination[candidateState.cell] = candidateState;
                continue;
            }

            int currentBestCost = autonomyCostByState[currentBest];
            if (candidateCost < currentBestCost || (candidateCost == currentBestCost && candidateState.steps < currentBest.steps))
                bestStateByDestination[candidateState.cell] = candidateState;
        }

        foreach (KeyValuePair<Vector3Int, PathNodeKey> pair in bestStateByDestination)
            pathsByDestination[pair.Key] = BuildPath(originKey, pair.Value, cameFrom);

        return pathsByDestination;
    }

    public static int CalculateAutonomyCostForPath(
        Tilemap terrainTilemap,
        UnitManager unit,
        IReadOnlyList<Vector3Int> path,
        TerrainDatabase terrainDatabase = null)
    {
        if (terrainTilemap == null || unit == null || path == null || path.Count < 2)
            return 0;

        MovementQueryCache cache = new MovementQueryCache(terrainTilemap, terrainDatabase);
        int baseMove = Mathf.Max(0, unit.GetMovementRange());
        bool canUseRoadBonus = CanUseRoadFullMoveBonus(unit, baseMove);
        bool freeRoadStepGranted = false;
        int freeRoadStepIndex = -1;

        if (canUseRoadBonus && path.Count > baseMove + 1)
        {
            bool fullMoveWasOnRoad = true;
            for (int i = 1; i <= baseMove; i++)
            {
                Vector3Int roadCell = path[i];
                roadCell.z = 0;
                if (!cache.IsRoadBoostCell(roadCell))
                {
                    fullMoveWasOnRoad = false;
                    break;
                }
            }

            if (fullMoveWasOnRoad)
            {
                Vector3Int bonusCell = path[baseMove + 1];
                bonusCell.z = 0;
                if (cache.IsRoadBoostCell(bonusCell))
                    freeRoadStepIndex = baseMove + 1;
            }
        }

        int total = 0;
        for (int i = 1; i < path.Count; i++)
        {
            if (!freeRoadStepGranted && freeRoadStepIndex == i)
            {
                freeRoadStepGranted = true;
                continue;
            }

            Vector3Int cell = path[i];
            cell.z = 0;

            ConstructionManager construction = cache.GetConstructionAtCell(cell);
            StructureData structure = cache.GetStructureAtCell(cell);
            TerrainTypeData terrainData = cache.ResolveTerrainAtCell(cell);
            total += GetAutonomyCostToEnterCell(construction, structure, terrainData, unit);
        }

        return Mathf.Max(0, total);
    }

    public static bool TryGetEnterCellCost(
        Tilemap terrainTilemap,
        UnitManager unit,
        Vector3Int cell,
        TerrainDatabase terrainDatabase,
        out int cost)
    {
        cost = 0;
        if (terrainTilemap == null || unit == null)
            return false;

        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(terrainTilemap, cell);
        StructureData structure = StructureOccupancyRules.GetStructureAtCell(terrainTilemap, cell);
        TerrainTypeData terrainData = ResolveTerrainAtCell(terrainTilemap, terrainDatabase, cell);
        bool hasAnyTile = HasAnyPaintedTileAtCell(terrainTilemap, cell);
        if (!CanTraverseCell(construction, structure, terrainData, hasAnyTile, terrainDatabase != null, unit))
            return false;

        cost = Mathf.Max(1, GetAutonomyCostToEnterCell(construction, structure, terrainData, unit));
        return true;
    }

    private static bool CanUseRoadFullMoveBonus(UnitManager unit, int baseMove)
    {
        if (unit == null)
            return false;

        if (baseMove < RoadBonusMinBaseMove)
            return false;

        return unit.GetDomain() == Domain.Land && unit.GetHeightLevel() == HeightLevel.Surface;
    }

    private static bool CanTraverseCell(
        ConstructionManager construction,
        StructureData structure,
        TerrainTypeData terrainData,
        bool hasAnyTile,
        bool terrainRulesAvailable,
        UnitManager unit)
    {
        if (unit == null)
            return false;

        Domain currentDomain = unit.GetDomain();
        HeightLevel currentHeight = unit.GetHeightLevel();
        bool isAirUnit = currentDomain == Domain.Air;

        // Para unidades aereas: tenta construcao/estrutura, mas se nao permitir ar faz fallback
        // para o terreno base (em vez de bloquear completamente por sobrescrita).
        if (isAirUnit)
        {
            if (construction != null && CanTraverseUsingConstruction(construction, unit, currentDomain, currentHeight))
                return true;

            if (structure != null && CanTraverseUsingStructure(structure, unit, currentDomain, currentHeight))
                return true;

            if (terrainData == null)
                return !terrainRulesAvailable && hasAnyTile;

            return CanTraverseUsingTerrain(terrainData, unit, currentDomain, currentHeight);
        }

        if (construction != null)
            return CanTraverseUsingConstruction(construction, unit, currentDomain, currentHeight);

        if (structure != null)
            return CanTraverseUsingStructure(structure, unit, currentDomain, currentHeight);

        if (terrainData == null)
            return !terrainRulesAvailable && hasAnyTile;

        return CanTraverseUsingTerrain(terrainData, unit, currentDomain, currentHeight);
    }

    private static bool CanTraverseUsingConstruction(
        ConstructionManager construction,
        UnitManager unit,
        Domain currentDomain,
        HeightLevel currentHeight)
    {
        if (construction == null || unit == null)
            return false;

        if (currentDomain == Domain.Air)
        {
            if (construction.AllowsAirDomain())
                return UnitPassesSkillRequirement(unit, construction.GetRequiredSkillsToEnter());
            if (!construction.SupportsLayerMode(currentDomain, currentHeight))
                return false;
            return UnitPassesSkillRequirement(unit, construction.GetRequiredSkillsToEnter());
        }

        IReadOnlyList<UnitLayerMode> unitModes = unit.GetAllLayerModes();
        for (int i = 0; i < unitModes.Count; i++)
        {
            UnitLayerMode mode = unitModes[i];
            if (construction.SupportsLayerMode(mode.domain, mode.heightLevel))
                return UnitPassesSkillRequirement(unit, construction.GetRequiredSkillsToEnter());
        }

        return false;
    }

    private static bool CanTraverseUsingStructure(
        StructureData structure,
        UnitManager unit,
        Domain currentDomain,
        HeightLevel currentHeight)
    {
        if (structure == null || unit == null)
            return false;

        if (currentDomain == Domain.Air)
        {
            if (structure.alwaysAllowAirDomain)
                return UnitPassesSkillRequirement(unit, structure.requiredSkillsToEnter);
            if (!StructureSupportsMode(structure, currentDomain, currentHeight))
                return false;
            return UnitPassesSkillRequirement(unit, structure.requiredSkillsToEnter);
        }

        IReadOnlyList<UnitLayerMode> unitModes = unit.GetAllLayerModes();
        bool supportsAnyMode = false;
        for (int i = 0; i < unitModes.Count; i++)
        {
            UnitLayerMode mode = unitModes[i];
            if (StructureSupportsMode(structure, mode.domain, mode.heightLevel))
            {
                supportsAnyMode = true;
                break;
            }
        }

        if (!supportsAnyMode)
            return false;

        return UnitPassesSkillRequirement(unit, structure.requiredSkillsToEnter);
    }

    private static bool CanTraverseUsingTerrain(
        TerrainTypeData terrainData,
        UnitManager unit,
        Domain currentDomain,
        HeightLevel currentHeight)
    {
        if (terrainData == null || unit == null)
            return false;

        if (currentDomain == Domain.Air)
        {
            if (terrainData.alwaysAllowAirDomain)
                return true;
            return TerrainSupportsMode(terrainData, currentDomain, currentHeight);
        }

        IReadOnlyList<UnitLayerMode> unitModes = unit.GetAllLayerModes();
        bool supportsAnyMode = false;
        for (int i = 0; i < unitModes.Count; i++)
        {
            UnitLayerMode mode = unitModes[i];
            if (TerrainSupportsMode(terrainData, mode.domain, mode.heightLevel))
            {
                supportsAnyMode = true;
                break;
            }
        }

        if (!supportsAnyMode)
            return false;

        if (terrainData.requiredSkillsToEnter == null || terrainData.requiredSkillsToEnter.Count == 0)
            return true;

        return UnitHasAnyRequiredSkill(unit, terrainData.requiredSkillsToEnter);
    }

    private static bool TerrainSupportsMode(TerrainTypeData terrainData, Domain domain, HeightLevel heightLevel)
    {
        if (terrainData == null)
            return false;

        if (terrainData.domain == domain && terrainData.heightLevel == heightLevel)
            return true;

        if (terrainData.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < terrainData.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrainData.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool StructureSupportsMode(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null)
            return false;

        if (structure.domain == domain && structure.heightLevel == heightLevel)
            return true;

        if (structure.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < structure.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = structure.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static int GetAutonomyCostToEnterCell(
        ConstructionManager construction,
        StructureData structure,
        TerrainTypeData terrainData,
        UnitManager unit)
    {
        if (unit != null && unit.GetDomain() == Domain.Air)
            return 1;

        if (construction != null)
            return GetAutonomyCostWithSkillOverrides(construction.GetBaseMovementCost(), construction.GetSkillCostOverrides(), unit);

        if (structure != null)
            return GetAutonomyCostWithSkillOverrides(structure.baseMovementCost, structure.skillCostOverrides, unit);

        if (terrainData != null)
            return GetAutonomyCostWithSkillOverrides(terrainData.basicAutonomyCost, terrainData.skillCostOverrides, unit);

        return 1;
    }

    private static int GetAutonomyCostWithSkillOverrides(
        int baseCost,
        IReadOnlyList<TerrainSkillCostOverride> overrides,
        UnitManager unit)
    {
        int safeBase = Mathf.Max(1, baseCost);
        if (unit == null || overrides == null)
            return safeBase;

        for (int i = 0; i < overrides.Count; i++)
        {
            TerrainSkillCostOverride entry = overrides[i];
            if (entry == null || entry.skill == null)
                continue;

            if (unit.HasSkill(entry.skill))
                return Mathf.Max(1, entry.autonomyCost);
        }

        return safeBase;
    }

    private static bool UnitHasAnyRequiredSkill(UnitManager unit, IReadOnlyList<SkillData> requiredSkills)
    {
        if (unit == null || requiredSkills == null || requiredSkills.Count == 0)
            return false;

        for (int i = 0; i < requiredSkills.Count; i++)
        {
            SkillData requiredSkill = requiredSkills[i];
            if (requiredSkill == null)
                continue;

            if (unit.HasSkill(requiredSkill))
                return true;
        }

        return false;
    }

    private static bool UnitPassesSkillRequirement(UnitManager unit, IReadOnlyList<SkillData> requiredSkills)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;

        return UnitHasAnyRequiredSkill(unit, requiredSkills);
    }

    private static TerrainTypeData ResolveTerrainAtCell(Tilemap terrainTilemap, TerrainDatabase terrainDatabase, Vector3Int cell)
    {
        if (terrainTilemap == null || terrainDatabase == null)
            return null;

        cell.z = 0;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
            return byMainTile;

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return null;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase other = map.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
                return byGridTile;
        }

        return null;
    }

    private static bool HasAnyPaintedTileAtCell(Tilemap terrainTilemap, Vector3Int cell)
    {
        if (terrainTilemap == null)
            return false;

        cell.z = 0;
        if (terrainTilemap.GetTile(cell) != null)
            return true;

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            if (map.GetTile(cell) != null)
                return true;
        }

        return false;
    }

    public static void GetImmediateHexNeighbors(Tilemap terrainTilemap, Vector3Int cell, List<Vector3Int> output)
    {
        output.Clear();
        if (terrainTilemap == null)
            return;

        Vector3 centerWorld = terrainTilemap.GetCellCenterWorld(cell);
        List<CellDistance> candidates = new List<CellDistance>(24);

        // Busca local para capturar os 6 vizinhos de um hex, respeitando o offset real do Tilemap.
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                Vector3Int candidate = new Vector3Int(cell.x + dx, cell.y + dy, 0);
                Vector3 world = terrainTilemap.GetCellCenterWorld(candidate);
                float distance = Vector2.Distance(centerWorld, world);
                if (distance <= 0.0001f)
                    continue;

                candidates.Add(new CellDistance(candidate, distance));
            }
        }

        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

        int count = Mathf.Min(6, candidates.Count);
        for (int i = 0; i < count; i++)
            output.Add(candidates[i].cell);
    }

    private static List<Vector3Int> BuildPath(PathNodeKey origin, PathNodeKey destination, Dictionary<PathNodeKey, PathNodeKey> cameFrom)
    {
        List<Vector3Int> reversedPath = new List<Vector3Int>();
        if (!cameFrom.ContainsKey(destination))
            return reversedPath;

        PathNodeKey current = destination;
        reversedPath.Add(current.cell);

        while (!current.Equals(origin))
        {
            current = cameFrom[current];
            reversedPath.Add(current.cell);
        }

        reversedPath.Reverse();
        return reversedPath;
    }

    private readonly struct PathNodeKey : System.IEquatable<PathNodeKey>
    {
        public readonly Vector3Int cell;
        public readonly int steps;
        public readonly bool usedFreeRoadBonusStep;
        public readonly bool roadOnlyUntilBaseMove;

        public PathNodeKey(
            Vector3Int cell,
            int steps,
            bool usedFreeRoadBonusStep,
            bool roadOnlyUntilBaseMove)
        {
            this.cell = new Vector3Int(cell.x, cell.y, 0);
            this.steps = steps;
            this.usedFreeRoadBonusStep = usedFreeRoadBonusStep;
            this.roadOnlyUntilBaseMove = roadOnlyUntilBaseMove;
        }

        public bool Equals(PathNodeKey other)
        {
            return cell == other.cell
                   && steps == other.steps
                   && usedFreeRoadBonusStep == other.usedFreeRoadBonusStep
                   && roadOnlyUntilBaseMove == other.roadOnlyUntilBaseMove;
        }

        public override bool Equals(object obj)
        {
            return obj is PathNodeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (cell.GetHashCode() * 397) ^ steps;
                hash = (hash * 397) ^ (usedFreeRoadBonusStep ? 1 : 0);
                hash = (hash * 397) ^ (roadOnlyUntilBaseMove ? 1 : 0);
                return hash;
            }
        }
    }

    private struct CellDistance
    {
        public readonly Vector3Int cell;
        public readonly float distance;

        public CellDistance(Vector3Int cell, float distance)
        {
            this.cell = cell;
            this.distance = distance;
        }
    }

    private sealed class MovementQueryCache
    {
        private readonly Tilemap referenceTilemap;
        private readonly TerrainDatabase terrainDatabase;
        private readonly Tilemap[] gridTilemaps;
        private readonly UnitManager[] units;
        private readonly ConstructionManager[] constructions;
        private readonly RoadNetworkManager[] roadNetworks;
        private readonly Dictionary<Vector3Int, ConstructionManager> constructionByCell = new Dictionary<Vector3Int, ConstructionManager>();
        private readonly Dictionary<Vector3Int, StructureData> structureByCell = new Dictionary<Vector3Int, StructureData>();
        private readonly Dictionary<Vector3Int, TerrainTypeData> terrainByCell = new Dictionary<Vector3Int, TerrainTypeData>();
        private readonly HashSet<Vector3Int> terrainMisses = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector3Int, bool> hasAnyTileByCell = new Dictionary<Vector3Int, bool>();

        public MovementQueryCache(Tilemap referenceTilemap, TerrainDatabase terrainDatabase)
        {
            this.referenceTilemap = referenceTilemap;
            this.terrainDatabase = terrainDatabase;

            if (referenceTilemap != null && referenceTilemap.layoutGrid != null)
                gridTilemaps = referenceTilemap.layoutGrid.GetComponentsInChildren<Tilemap>(includeInactive: true);
            else
                gridTilemaps = System.Array.Empty<Tilemap>();

            units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            roadNetworks = Object.FindObjectsByType<RoadNetworkManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        public ConstructionManager GetConstructionAtCell(Vector3Int cell)
        {
            cell.z = 0;
            if (constructionByCell.TryGetValue(cell, out ConstructionManager cachedConstruction))
                return cachedConstruction;

            ConstructionManager found = null;
            for (int i = 0; i < constructions.Length; i++)
            {
                ConstructionManager construction = constructions[i];
                if (construction == null || !construction.gameObject.activeInHierarchy)
                    continue;

                Vector3Int occupiedCell = construction.BoardTilemap == referenceTilemap
                    ? construction.CurrentCellPosition
                    : HexCoordinates.WorldToCell(referenceTilemap, construction.transform.position);

                occupiedCell.z = 0;
                if (occupiedCell != cell)
                    continue;

                found = construction;
                break;
            }

            constructionByCell[cell] = found;
            return found;
        }

        public StructureData GetStructureAtCell(Vector3Int cell)
        {
            cell.z = 0;
            if (structureByCell.TryGetValue(cell, out StructureData cachedStructure))
                return cachedStructure;

            StructureData found = null;
            for (int i = 0; i < roadNetworks.Length; i++)
            {
                RoadNetworkManager network = roadNetworks[i];
                if (network == null || !network.gameObject.activeInHierarchy)
                    continue;

                Tilemap networkTilemap = network.BoardTilemap;
                if (!IsCompatibleReference(referenceTilemap, networkTilemap))
                    continue;

                if (network.TryGetStructureAtCell(cell, out StructureData structure) && structure != null)
                {
                    found = structure;
                    break;
                }
            }

            structureByCell[cell] = found;
            return found;
        }

        public bool IsRoadBoostCell(Vector3Int cell)
        {
            StructureData structure = GetStructureAtCell(cell);
            return structure != null && structure.roadBoost;
        }

        public TerrainTypeData ResolveTerrainAtCell(Vector3Int cell)
        {
            if (referenceTilemap == null || terrainDatabase == null)
                return null;

            cell.z = 0;
            if (terrainByCell.TryGetValue(cell, out TerrainTypeData cachedTerrain))
                return cachedTerrain;
            if (terrainMisses.Contains(cell))
                return null;

            TileBase tile = referenceTilemap.GetTile(cell);
            if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
            {
                terrainByCell[cell] = byMainTile;
                return byMainTile;
            }

            for (int i = 0; i < gridTilemaps.Length; i++)
            {
                Tilemap map = gridTilemaps[i];
                if (map == null)
                    continue;

                TileBase other = map.GetTile(cell);
                if (other == null)
                    continue;

                if (terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
                {
                    terrainByCell[cell] = byGridTile;
                    return byGridTile;
                }
            }

            terrainMisses.Add(cell);
            return null;
        }

        public bool HasAnyPaintedTileAtCell(Vector3Int cell)
        {
            if (referenceTilemap == null)
                return false;

            cell.z = 0;
            if (hasAnyTileByCell.TryGetValue(cell, out bool cached))
                return cached;

            bool hasAny = referenceTilemap.GetTile(cell) != null;
            if (!hasAny)
            {
                for (int i = 0; i < gridTilemaps.Length; i++)
                {
                    Tilemap map = gridTilemaps[i];
                    if (map == null)
                        continue;

                    if (map.GetTile(cell) != null)
                    {
                        hasAny = true;
                        break;
                    }
                }
            }

            hasAnyTileByCell[cell] = hasAny;
            return hasAny;
        }

        public UnitManager GetUnitAtCell(Vector3Int cell, UnitManager exceptUnit = null)
        {
            cell.z = 0;
            for (int i = 0; i < units.Length; i++)
            {
                UnitManager unit = units[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                    continue;

                Vector3Int occupiedCell = unit.BoardTilemap == referenceTilemap
                    ? unit.CurrentCellPosition
                    : HexCoordinates.WorldToCell(referenceTilemap, unit.transform.position);

                occupiedCell.z = 0;
                if (occupiedCell == cell)
                    return unit;
            }

            return null;
        }

        private static bool IsCompatibleReference(Tilemap referenceTilemap, Tilemap networkTilemap)
        {
            if (referenceTilemap == null || networkTilemap == null)
                return true;

            if (referenceTilemap == networkTilemap)
                return true;

            GridLayout referenceGrid = referenceTilemap.layoutGrid;
            GridLayout networkGrid = networkTilemap.layoutGrid;
            if (referenceGrid != null && networkGrid != null && referenceGrid == networkGrid)
                return true;

            return false;
        }
    }
}
