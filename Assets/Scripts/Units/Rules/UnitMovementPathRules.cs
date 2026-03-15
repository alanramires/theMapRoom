using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class UnitMovementPathRules
{
    private const int RoadBonusMinBaseMove = 4;
    private const string RailSkillId = "Linha de Trem";

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
        int expandedStateCount = 0;

        PathNodeKey originKey = new PathNodeKey(origin, 0, usedFreeRoadBonusStep: false, roadOnlyUntilBaseMove: true);
        frontier.Enqueue(originKey);
        autonomyCostByState[originKey] = 0;
        cameFrom[originKey] = originKey;

        while (frontier.Count > 0)
        {
            PathNodeKey currentKey = frontier.Dequeue();
            expandedStateCount++;
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
                if (!CanTraverseCell(next, cache, construction, structure, terrainData, hasAnyTile, terrainDatabase != null, unit))
                    continue;
                int movementCostBase = GetAutonomyCostToEnterCell(construction, structure, terrainData, unit, applyOperationalAutonomyModifier: false);
                int autonomyCostToEnter = GetAutonomyCostToEnterCell(construction, structure, terrainData, unit, applyOperationalAutonomyModifier: true);
                bool nextIsRoadBoost = cache.IsRoadBoostCell(next);

                bool useFreeRoadBonusStep =
                    canUseRoadBonus &&
                    !currentKey.usedFreeRoadBonusStep &&
                    currentKey.roadOnlyUntilBaseMove &&
                    currentSteps == maxMovementCost &&
                    nextIsRoadBoost;

                int movementCostToEnter = useFreeRoadBonusStep ? 0 : movementCostBase;
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

        if (PathManager.IsPathfindingDebugLogsEnabled && Application.isPlaying)
        {
            Debug.Log(
                $"[PathBFS] unit={unit.name} maxSteps={maxMovementCost} fuel={maxAutonomyCost} " +
                $"expandedStates={expandedStateCount} visitedStates={autonomyCostByState.Count} " +
                $"reachableHexes={pathsByDestination.Count}");
        }

        return pathsByDestination;
    }

    public static int CalculateAutonomyCostForPath(
        Tilemap terrainTilemap,
        UnitManager unit,
        IReadOnlyList<Vector3Int> path,
        TerrainDatabase terrainDatabase = null,
        bool applyOperationalAutonomyModifier = true)
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
            total += GetAutonomyCostToEnterCell(construction, structure, terrainData, unit, applyOperationalAutonomyModifier);
        }

        return Mathf.Max(0, total);
    }

    public static bool TryGetEnterCellCost(
        Tilemap terrainTilemap,
        UnitManager unit,
        Vector3Int cell,
        TerrainDatabase terrainDatabase,
        bool applyOperationalAutonomyModifier,
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
        if (!CanTraverseCell(cell, null, construction, structure, terrainData, hasAnyTile, terrainDatabase != null, unit))
            return false;

        cost = Mathf.Max(1, GetAutonomyCostToEnterCell(construction, structure, terrainData, unit, applyOperationalAutonomyModifier));
        return true;
    }

    public static bool TryGetEnterCellCost(
        Tilemap terrainTilemap,
        UnitManager unit,
        Vector3Int cell,
        TerrainDatabase terrainDatabase,
        out int cost)
    {
        return TryGetEnterCellCost(terrainTilemap, unit, cell, terrainDatabase, applyOperationalAutonomyModifier: true, out cost);
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
        Vector3Int cell,
        MovementQueryCache cache,
        ConstructionManager construction,
        StructureData structure,
        TerrainTypeData terrainData,
        bool hasAnyTile,
        bool terrainRulesAvailable,
        UnitManager unit)
    {
        if (unit == null)
            return false;

        // Regra especial: unidade com skill de linha de trem ignora desempate de hierarquia
        // e passa no hex se existir ao menos uma estrutura de rota no hex que permita a unidade
        // pelas regras de skill (required/blocked).
        if (UnitHasRailSkill(unit))
        {
            if (cache != null)
                return cache.HasAnyRouteStructureAtCellAllowingUnit(cell, unit);
            return StructureQualifiesAsRailForUnit(structure, unit);
        }

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

        if (cache != null && cache.HasAnyRouteStructureAtCellAllowingUnit(cell, unit))
            return true;

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
                return UnitPassesSkillRules(unit, construction.GetRequiredSkillsToEnter(), construction.GetBlockedSkillsToEnter());
            if (!construction.SupportsLayerMode(currentDomain, currentHeight))
                return false;
            return UnitPassesSkillRules(unit, construction.GetRequiredSkillsToEnter(), construction.GetBlockedSkillsToEnter());
        }

        IReadOnlyList<UnitLayerMode> unitModes = unit.GetAllLayerModes();
        for (int i = 0; i < unitModes.Count; i++)
        {
            UnitLayerMode mode = unitModes[i];
            if (construction.SupportsLayerMode(mode.domain, mode.heightLevel))
                return UnitPassesSkillRules(unit, construction.GetRequiredSkillsToEnter(), construction.GetBlockedSkillsToEnter());
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
                return UnitPassesSkillRules(unit, structure.requiredSkillsToEnter, structure.blockedSkills);
            if (!StructureSupportsMode(structure, currentDomain, currentHeight))
                return false;
            return UnitPassesSkillRules(unit, structure.requiredSkillsToEnter, structure.blockedSkills);
        }

        if (StructureSupportsAdditionalMode(structure, currentDomain, currentHeight))
            return true;

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

        return UnitPassesSkillRules(unit, structure.requiredSkillsToEnter, structure.blockedSkills);
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

        return UnitPassesSkillRules(unit, terrainData.requiredSkillsToEnter, terrainData.blockedSkills);
    }

    private static bool StructureAllowsUnitBySkillRules(StructureData structure, UnitManager unit)
    {
        if (structure == null || unit == null)
            return false;
        return UnitPassesSkillRules(unit, structure.requiredSkillsToEnter, structure.blockedSkills);
    }

    private static bool StructureQualifiesAsRailForUnit(StructureData structure, UnitManager unit)
    {
        if (structure == null || unit == null)
            return false;

        return CanTraverseUsingStructure(structure, unit, unit.GetDomain(), unit.GetHeightLevel());
    }

    private static bool UnitHasRailSkill(UnitManager unit)
    {
        if (unit == null)
            return false;

        if (unit.HasSkillId(RailSkillId))
            return true;

        if (!unit.TryGetUnitData(out UnitData data) || data == null || data.skills == null)
            return false;

        for (int i = 0; i < data.skills.Count; i++)
        {
            if (IsRailSkillDefinition(data.skills[i]))
                return true;
        }

        return false;
    }

    private static bool IsRailSkillDefinition(SkillData skill)
    {
        if (skill == null)
            return false;

        if (string.Equals(skill.id, RailSkillId, System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(skill.displayName, RailSkillId, System.StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(skill.name, RailSkillId, System.StringComparison.OrdinalIgnoreCase);
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
        UnitManager unit,
        bool applyOperationalAutonomyModifier)
    {
        int baseCost;
        if (unit != null && unit.GetDomain() == Domain.Air)
            baseCost = 1;
        else if (construction != null)
            baseCost = GetAutonomyCostWithSkillOverrides(construction.GetBaseMovementCost(), construction.GetSkillCostOverrides(), unit);
        else if (structure != null)
            baseCost = GetAutonomyCostWithSkillOverrides(structure.baseMovementCost, structure.skillCostOverrides, unit);
        else if (terrainData != null)
            baseCost = GetAutonomyCostWithSkillOverrides(terrainData.basicAutonomyCost, terrainData.skillCostOverrides, unit);
        else
            baseCost = 1;

        return OperationalAutonomyRules.ApplyMovementAutonomyCost(unit, baseCost, applyOperationalAutonomyModifier);
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

    private static bool UnitHasAnyBlockedSkill(UnitManager unit, IReadOnlyList<SkillData> blockedSkills)
    {
        if (unit == null || blockedSkills == null || blockedSkills.Count == 0)
            return false;

        for (int i = 0; i < blockedSkills.Count; i++)
        {
            SkillData blocked = blockedSkills[i];
            if (blocked == null)
                continue;
            if (unit.HasSkill(blocked))
                return true;
        }

        return false;
    }

    private static bool StructureSupportsAdditionalMode(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null || structure.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < structure.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = structure.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool UnitPassesSkillRules(UnitManager unit, IReadOnlyList<SkillData> requiredSkills, IReadOnlyList<SkillData> blockedSkills)
    {
        if (UnitHasAnyBlockedSkill(unit, blockedSkills))
            return false;
        return UnitPassesSkillRequirement(unit, requiredSkills);
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
        private readonly Dictionary<Vector3Int, List<UnitManager>> unitsByCell = new Dictionary<Vector3Int, List<UnitManager>>();
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

            for (int i = 0; i < units.Length; i++)
            {
                UnitManager unit = units[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                    continue;
                if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                    continue;

                Vector3Int occupiedCell = unit.CurrentCellPosition;
                occupiedCell.z = 0;
                if (!unitsByCell.TryGetValue(occupiedCell, out List<UnitManager> occupants))
                {
                    occupants = new List<UnitManager>(1);
                    unitsByCell[occupiedCell] = occupants;
                }

                occupants.Add(unit);
            }
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

        public bool HasAnyRouteStructureAtCellAllowingUnit(Vector3Int cell, UnitManager unit)
        {
            cell.z = 0;
            bool found = false;
            for (int i = 0; i < roadNetworks.Length; i++)
            {
                RoadNetworkManager network = roadNetworks[i];
                if (network == null || !network.gameObject.activeInHierarchy)
                    continue;

                Tilemap networkTilemap = network.BoardTilemap;
                if (!IsCompatibleReference(referenceTilemap, networkTilemap))
                    continue;

                StructureDatabase db = network.StructureDatabase;
                IReadOnlyList<StructureData> structures = db != null ? db.Structures : null;
                if (structures == null)
                    continue;

                for (int s = 0; s < structures.Count; s++)
                {
                    StructureData structure = structures[s];
                    if (structure == null)
                        continue;

                    IReadOnlyList<RoadRouteDefinition> routes = db.GetRoadRoutes(structure);
                    if (routes == null)
                        routes = structure.roadRoutes;
                    if (routes == null)
                        continue;

                    for (int r = 0; r < routes.Count; r++)
                    {
                        RoadRouteDefinition route = routes[r];
                        if (route == null || route.cells == null)
                            continue;

                        for (int c = 0; c < route.cells.Count; c++)
                        {
                            Vector3Int routeCell = route.cells[c];
                            routeCell.z = 0;
                            if (routeCell != cell)
                                continue;

                            if (StructureQualifiesAsRailForUnit(structure, unit))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                            break;
                    }

                    if (found)
                        break;
                }

                if (found)
                    break;
            }
            return found;
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
            if (!unitsByCell.TryGetValue(cell, out List<UnitManager> occupants) || occupants == null || occupants.Count == 0)
                return null;

            if (UnitRulesDefinition.IsTotalWarEnabled() && exceptUnit != null)
            {
                UnitManager sameTeam = null;
                UnitManager otherTeam = null;
                for (int i = 0; i < occupants.Count; i++)
                {
                    UnitManager unit = occupants[i];
                    if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit)
                        continue;

                    if (unit.TeamId == exceptUnit.TeamId)
                    {
                        sameTeam = unit;
                        break;
                    }

                    if (otherTeam == null)
                        otherTeam = unit;
                }

                if (sameTeam != null)
                    return sameTeam;
                if (otherTeam != null)
                    return otherTeam;
            }

            for (int i = 0; i < occupants.Count; i++)
            {
                UnitManager unit = occupants[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit)
                    continue;

                Vector3Int occupiedCell = unit.CurrentCellPosition;
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

        private static bool IsUnitOnReferenceMap(UnitManager unit, Tilemap referenceTilemap)
        {
            if (unit == null || referenceTilemap == null)
                return false;
            if (unit.BoardTilemap == null || unit.BoardTilemap != referenceTilemap)
                return false;

            return unit.gameObject.scene == referenceTilemap.gameObject.scene;
        }
    }
}
