using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class UnitMovementPathRules
{
    private const int RoadBonusMinBaseMove = 4;

    private enum TraversalSource
    {
        None = 0,
        Construction = 1,
        Structure = 2,
        Terrain = 3
    }

    private enum StructureTraversalMode
    {
        None = 0,
        NativeStructure = 1,
        TerrainPassage = 2
    }

    private readonly struct TraversalDecision
    {
        public readonly TraversalSource source;
        public readonly StructureData structure;

        public TraversalDecision(
            TraversalSource source,
            StructureData structure = null)
        {
            this.source = source;
            this.structure = structure;
        }
    }

    /// <summary>
    /// <paramref name="originOverride"/> calcula a onda a partir de OUTRA
    /// celula que nao a atual da unidade: "e se ela estivesse ali?".
    ///
    /// E o que sustenta a projecao invertida do desembarque — teleporta o
    /// passageiro para cima do objetivo e a banda dele vira a zona de largada —
    /// e o teste da unidade fantasma que a doutrina de fogo indireto pede.
    /// Nao move nada: so troca o ponto de partida da busca.
    /// </summary>
    public static Dictionary<Vector3Int, List<Vector3Int>> CalcularCaminhosValidos(
        Tilemap terrainTilemap,
        UnitManager unit,
        int maxSteps,
        TerrainDatabase terrainDatabase = null,
        Vector3Int? originOverride = null)
    {
        using var perf = new AIDecisionPerfScope(unit, "validPaths");
        Dictionary<Vector3Int, List<Vector3Int>> pathsByDestination = new Dictionary<Vector3Int, List<Vector3Int>>();
        if (terrainTilemap == null || unit == null || maxSteps < 0)
            return pathsByDestination;

        // A chave precisa observar a camada runtime sincronizada. O cache so
        // aceita o snapshot quando ela ainda coincide com a ocupacao
        // confirmada; durante previews ou rollback pendente a consulta cai no
        // calculo normal e nao publica resultado.
        unit.SyncLayerStateFromData(forceNativeDefault: false);
        if (MovementReachCache.TryGetValidPaths(
                terrainTilemap,
                unit,
                maxSteps,
                terrainDatabase,
                out Dictionary<Vector3Int, List<Vector3Int>> cachedPaths,
                originOverride))
        {
            return cachedPaths;
        }

        AIDecisionPerf.AddCount("MovementWavesBuilt");
        AIDecisionPerf.AddCount("MovementCacheMisses");
        AIDecisionPerf.AddCount("ValidPathWaves");

        int maxMovementCost = Mathf.Max(0, maxSteps);
        int maxAutonomyCost = Mathf.Max(0, unit.CurrentFuel);
        bool canUseRoadBonus = CanUseRoadFullMoveBonus(unit, maxMovementCost);

        Vector3Int origin = originOverride ?? unit.CurrentCellPosition;
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
                if (!TryResolveTraversal(
                        next,
                        cache,
                        construction,
                        structure,
                        terrainData,
                        hasAnyTile,
                        terrainDatabase != null,
                        unit,
                        current,
                        out TraversalDecision traversal))
                    continue;
                int movementCostBase = GetAutonomyCostToEnterCell(
                    construction,
                    terrainData,
                    unit,
                    applyOperationalAutonomyModifier: false,
                    traversal);
                int autonomyCostToEnter = GetAutonomyCostToEnterCell(
                    construction,
                    terrainData,
                    unit,
                    applyOperationalAutonomyModifier: true,
                    traversal);
                bool nextIsRoadBoost =
                    traversal.source == TraversalSource.Structure
                    && cache.IsRoadBoostEdge(
                        current,
                        next,
                        unit);

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

                bool blockedByOccupant = false;
                IReadOnlyList<UnitManager> blockers = cache.GetUnitsAtCell(next, unit);
                for (int blockerIndex = 0; blockerIndex < blockers.Count; blockerIndex++)
                {
                    UnitManager blocker = blockers[blockerIndex];
                    if (blocker == null)
                        continue;

                    blocker.SyncLayerStateFromData(forceNativeDefault: false);
                    HeightBand moverBand = OccupancyResolver.GetHeightBand(unit);
                    HeightBand blockerBand = OccupancyResolver.GetHeightBand(blocker);
                    bool canPassThrough = OccupancyResolver.CanPassThrough(unit, blocker, next);

                    if (PathManager.IsPathfindingDebugLogsEnabled && Application.isPlaying)
                    {
                        Debug.Log(
                            $"[PathBFS][OccupancyDebug] cell=({next.x},{next.y},{next.z}) " +
                            $"mover={unit.name} moverTeam={(int)unit.TeamId} moverBand={moverBand} " +
                            $"blocker={blocker.name} blockerTeam={(int)blocker.TeamId} blockerBand={blockerBand} " +
                            $"canPassThrough={canPassThrough}");
                    }

                    if (!canPassThrough)
                    {
                        blockedByOccupant = true;
                        break;
                    }
                }

                if (blockedByOccupant)
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

        AIDecisionPerf.AddCount("CellsVisited", expandedStateCount);
        AIDecisionPerf.AddCount("PathStatesExpanded", expandedStateCount);
        AIDecisionPerf.AddCount(
            "ReachableCellsProduced",
            pathsByDestination.Count);

        if (PathManager.IsPathfindingDebugLogsEnabled && Application.isPlaying)
        {
            Debug.Log(
                $"[PathBFS] unit={unit.name} maxSteps={maxMovementCost} fuel={maxAutonomyCost} " +
                $"expandedStates={expandedStateCount} visitedStates={autonomyCostByState.Count} " +
                $"reachableHexes={pathsByDestination.Count}");
        }

        MovementReachCache.StoreValidPaths(
            terrainTilemap,
            unit,
            maxSteps,
            terrainDatabase,
            pathsByDestination,
            originOverride);
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
                if (!cache.IsRoadBoostEdge(
                        path[i - 1],
                        path[i],
                        unit))
                {
                    fullMoveWasOnRoad = false;
                    break;
                }
            }

            if (fullMoveWasOnRoad)
            {
                if (cache.IsRoadBoostEdge(
                        path[baseMove],
                        path[baseMove + 1],
                        unit))
                {
                    freeRoadStepIndex = baseMove + 1;
                }
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
            Vector3Int previousCell = path[i - 1];
            previousCell.z = 0;
            bool hasAnyTile = cache.HasAnyPaintedTileAtCell(cell);
            TraversalDecision traversal;
            if (!TryResolveTraversal(
                    cell,
                    cache,
                    construction,
                    structure,
                    terrainData,
                    hasAnyTile,
                    terrainDatabase != null,
                    unit,
                    previousCell,
                    out traversal))
            {
                traversal = ResolveDefaultTraversalDecision(
                    construction,
                    structure,
                    terrainData);
            }
            total += GetAutonomyCostToEnterCell(
                construction,
                terrainData,
                unit,
                applyOperationalAutonomyModifier,
                traversal);
        }

        return Mathf.Max(0, total);
    }

    public static bool DidUseRoadFullMoveBonus(
        Tilemap terrainTilemap,
        UnitManager unit,
        IReadOnlyList<Vector3Int> path,
        TerrainDatabase terrainDatabase = null)
    {
        if (terrainTilemap == null || unit == null || path == null || path.Count < 2)
            return false;

        MovementQueryCache cache = new MovementQueryCache(terrainTilemap, terrainDatabase);
        int baseMove = Mathf.Max(0, unit.GetMovementRange());
        bool canUseRoadBonus = CanUseRoadFullMoveBonus(unit, baseMove);
        if (!canUseRoadBonus)
            return false;

        if (path.Count <= baseMove + 1)
            return false;

        for (int i = 1; i <= baseMove; i++)
        {
            if (!cache.IsRoadBoostEdge(
                    path[i - 1],
                    path[i],
                    unit))
            {
                return false;
            }
        }

        return cache.IsRoadBoostEdge(
            path[baseMove],
            path[baseMove + 1],
            unit);
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
        if (!TryResolveTraversal(
                cell,
                null,
                construction,
                structure,
                terrainData,
                hasAnyTile,
                terrainDatabase != null,
                unit,
                previousCell: null,
                out TraversalDecision traversal))
            return false;

        cost = Mathf.Max(
            1,
            GetAutonomyCostToEnterCell(
                construction,
                terrainData,
                unit,
                applyOperationalAutonomyModifier,
                traversal));
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

    // Returns the real MP cost (using this unit's movement rules) from startCell to every
    // reachable cell within maxSteps. Used by AI to compare candidates by true travel cost
    // instead of unit-agnostic hex distance.
    /// <summary>
    /// Malha de custo de N TURNOS, com o teto de MP reiniciando a cada turno.
    ///
    /// MP nao acumula entre turnos: um soldado de 3 MP faz 3+3, nunca 6. Na
    /// montanha de custo 2 ele entra em UMA por turno, porque depois da
    /// primeira sobra 1 e a segunda pede 2. E um hex que custa mais do que o
    /// teto de um turno e intransponivel para sempre — e BLOQUEIA o corredor
    /// atras dele.
    ///
    /// Numa unica passada: a chave de relaxamento e o par
    /// (turnos usados, MP gasto no turno corrente), minimizado
    /// lexicograficamente. Reusa o mesmo TryResolveTraversal com previousCell
    /// de <see cref="CalculateMovementCostMap"/>, entao as regras de travessia
    /// e transicao de camada respondem identico.
    ///
    /// Devolve o custo ACUMULADO por celula, no mesmo formato do mapa de um
    /// turno so; <paramref name="turnsByCell"/> diz em quantos turnos cada
    /// celula e alcancada (1 = nesta rodada).
    /// </summary>
    public static Dictionary<Vector3Int, int> CalculateTurnChainedCostMap(
        Tilemap terrainTilemap,
        UnitManager unit,
        Vector3Int startCell,
        int firstTurnBudget,
        int laterTurnBudget,
        int turns,
        TerrainDatabase terrainDatabase,
        out Dictionary<Vector3Int, int> turnsByCell)
    {
        using var perf = new AIDecisionPerfScope(unit, "turnChainedCostMap");
        var costByCell = new Dictionary<Vector3Int, int>();
        turnsByCell = new Dictionary<Vector3Int, int>();
        int totalTurns = Mathf.Max(1, turns);
        int firstBudget = Mathf.Max(0, firstTurnBudget);
        int laterBudget = Mathf.Max(0, laterTurnBudget);
        if (terrainTilemap == null || unit == null)
            return costByCell;
        if (firstBudget <= 0 && laterBudget <= 0)
            return costByCell;

        unit.SyncLayerStateFromData(forceNativeDefault: false);
        Vector3Int origin = startCell;
        origin.z = 0;

        // Estado por celula: melhor (turno, gasto no turno). Menor turno vence;
        // empatando, menor gasto no turno corrente, porque sobra mais MP para
        // seguir adiante dentro do mesmo turno.
        var bestTurn = new Dictionary<Vector3Int, int>();
        var bestSpent = new Dictionary<Vector3Int, int>();
        bestTurn[origin] = 1;
        bestSpent[origin] = 0;
        costByCell[origin] = 0;
        turnsByCell[origin] = 1;

        MovementQueryCache cache =
            new MovementQueryCache(terrainTilemap, terrainDatabase);
        var frontier = new Queue<Vector3Int>();
        frontier.Enqueue(origin);
        var neighbors = new List<Vector3Int>(6);
        int expandedCellCount = 0;

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int currentTurn = bestTurn[current];
            int currentSpent = bestSpent[current];
            int currentCost = costByCell[current];
            expandedCellCount++;

            GetImmediateHexNeighbors(terrainTilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                ConstructionManager construction = cache.GetConstructionAtCell(next);
                StructureData structure = cache.GetStructureAtCell(next);
                TerrainTypeData terrainData = cache.ResolveTerrainAtCell(next);
                bool hasAnyTile = cache.HasAnyPaintedTileAtCell(next);
                if (!TryResolveTraversal(
                        next,
                        cache,
                        construction,
                        structure,
                        terrainData,
                        hasAnyTile,
                        terrainDatabase != null,
                        unit,
                        current,
                        out TraversalDecision traversal))
                    continue;

                int moveCost = Mathf.Max(
                    1,
                    GetAutonomyCostToEnterCell(
                        construction,
                        terrainData,
                        unit,
                        applyOperationalAutonomyModifier: false,
                        traversal));

                int turn = currentTurn;
                int spent = currentSpent + moveCost;
                int budget = turn == 1 ? firstBudget : laterBudget;
                if (spent > budget)
                {
                    // Nao cabe no turno corrente: vira o turno e recomeca o
                    // teto. O hex ainda precisa caber num turno INTEIRO — senao
                    // e intransponivel, por mais turnos que passem.
                    turn++;
                    spent = moveCost;
                    if (turn > totalTurns || spent > laterBudget)
                        continue;
                }

                if (bestTurn.TryGetValue(next, out int knownTurn)
                    && (knownTurn < turn
                        || (knownTurn == turn && bestSpent[next] <= spent)))
                {
                    continue;
                }

                bestTurn[next] = turn;
                bestSpent[next] = spent;
                costByCell[next] = currentCost + moveCost;
                turnsByCell[next] = turn;
                frontier.Enqueue(next);
            }
        }

        AIDecisionPerf.AddCount("CellsVisited", expandedCellCount);
        AIDecisionPerf.AddCount("TurnChainedCellsExpanded", expandedCellCount);
        AIDecisionPerf.AddCount("ReachableCellsProduced", costByCell.Count);
        return costByCell;
    }

    public static Dictionary<Vector3Int, int> CalculateMovementCostMap(
        Tilemap terrainTilemap,
        UnitManager unit,
        Vector3Int startCell,
        int maxSteps,
        TerrainDatabase terrainDatabase = null)
    {
        using var perf = new AIDecisionPerfScope(
            unit,
            "movementCostMap");
        var costByCell = new Dictionary<Vector3Int, int>();
        if (terrainTilemap == null || unit == null || maxSteps < 0)
            return costByCell;

        unit.SyncLayerStateFromData(forceNativeDefault: false);
        Vector3Int origin = startCell;
        origin.z = 0;
        if (MovementReachCache.TryGetMovementCosts(
                terrainTilemap,
                unit,
                origin,
                maxSteps,
                terrainDatabase,
                out Dictionary<Vector3Int, int> cachedCosts))
        {
            return cachedCosts;
        }

        AIDecisionPerf.AddCount("MovementWavesBuilt");
        AIDecisionPerf.AddCount("MovementCacheMisses");
        AIDecisionPerf.AddCount("MovementCostWaves");

        costByCell[origin] = 0;

        MovementQueryCache cache = new MovementQueryCache(terrainTilemap, terrainDatabase);
        var frontier = new Queue<(Vector3Int cell, int steps)>();
        frontier.Enqueue((origin, 0));
        var neighbors = new List<Vector3Int>(6);
        int expandedCellCount = 0;

        while (frontier.Count > 0)
        {
            var (current, currentSteps) = frontier.Dequeue();
            if (costByCell.TryGetValue(current, out int recorded) && recorded < currentSteps) continue;
            expandedCellCount++;

            GetImmediateHexNeighbors(terrainTilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                ConstructionManager construction = cache.GetConstructionAtCell(next);
                StructureData structure = cache.GetStructureAtCell(next);
                TerrainTypeData terrainData = cache.ResolveTerrainAtCell(next);
                bool hasAnyTile = cache.HasAnyPaintedTileAtCell(next);
                if (!TryResolveTraversal(
                        next,
                        cache,
                        construction,
                        structure,
                        terrainData,
                        hasAnyTile,
                        terrainDatabase != null,
                        unit,
                        current,
                        out TraversalDecision traversal))
                    continue;

                int moveCost = Mathf.Max(
                    1,
                    GetAutonomyCostToEnterCell(
                        construction,
                        terrainData,
                        unit,
                        applyOperationalAutonomyModifier: false,
                        traversal));
                int nextSteps = currentSteps + moveCost;
                if (nextSteps > maxSteps) continue;

                if (costByCell.TryGetValue(next, out int knownCost) && knownCost <= nextSteps) continue;
                costByCell[next] = nextSteps;
                frontier.Enqueue((next, nextSteps));
            }
        }

        AIDecisionPerf.AddCount("CellsVisited", expandedCellCount);
        AIDecisionPerf.AddCount(
            "MovementCostCellsExpanded",
            expandedCellCount);
        AIDecisionPerf.AddCount(
            "ReachableCellsProduced",
            costByCell.Count);
        MovementReachCache.StoreMovementCosts(
            terrainTilemap,
            unit,
            origin,
            maxSteps,
            terrainDatabase,
            costByCell);
        return costByCell;
    }

    private static bool CanUseRoadFullMoveBonus(UnitManager unit, int baseMove)
    {
        if (unit == null)
            return false;

        if (baseMove < RoadBonusMinBaseMove)
            return false;

        return unit.GetDomain() == Domain.Land && unit.GetHeightLevel() == HeightLevel.Surface;
    }

    private static bool TryResolveTraversal(
        Vector3Int cell,
        MovementQueryCache cache,
        ConstructionManager construction,
        StructureData structure,
        TerrainTypeData terrainData,
        bool hasAnyTile,
        bool terrainRulesAvailable,
        UnitManager unit,
        Vector3Int? previousCell,
        out TraversalDecision decision)
    {
        decision = default;
        if (unit == null)
            return false;

        // Regra de terreno ausente nao e fallback de gameplay. Quando existe
        // TerrainDatabase, todo hex consultado precisa resolver um
        // TerrainTypeData antes que construcao ou estrutura possam governa-lo.
        if (terrainRulesAvailable && terrainData == null)
            return false;

        Domain currentDomain = unit.GetDomain();
        HeightLevel currentHeight = unit.GetHeightLevel();
        bool isAirUnit = currentDomain == Domain.Air;
        bool constructionInheritsTerrain =
            construction != null
            && construction.InheritsTerrainRulesOn(terrainData);

        if (isAirUnit)
        {
            if (constructionInheritsTerrain)
            {
                if (!CanTraverseUsingTerrain(
                        terrainData,
                        unit,
                        currentDomain,
                        currentHeight))
                {
                    return false;
                }

                decision = new TraversalDecision(
                    TraversalSource.Terrain);
                return true;
            }

            if (construction != null
                && CanTraverseUsingConstruction(
                    construction,
                    terrainData,
                    unit,
                    currentDomain,
                    currentHeight))
            {
                decision = new TraversalDecision(
                    TraversalSource.Construction);
                return true;
            }

            if (structure != null
                && CanTraverseUsingStructure(
                    structure,
                    terrainData,
                    unit,
                    currentDomain,
                    currentHeight))
            {
                decision = new TraversalDecision(
                    TraversalSource.Structure,
                    structure);
                return true;
            }

            if (terrainData == null)
            {
                if (terrainRulesAvailable || !hasAnyTile)
                    return false;
                decision = new TraversalDecision(TraversalSource.Terrain);
                return true;
            }

            if (!CanTraverseUsingTerrain(
                    terrainData,
                    unit,
                    currentDomain,
                    currentHeight))
            {
                return false;
            }

            decision = new TraversalDecision(TraversalSource.Terrain);
            return true;
        }

        // Em cruzamentos, a aresta escolhe qual representante de rede esta
        // sendo usado. O par Estrutura+Terreno desse representante governa
        // skills e custo; o no compartilhado, sozinho, nao mistura as redes.
        if (previousCell.HasValue && cache != null)
        {
            bool routeAllows = cache
                .TryGetConnectedRouteStructureAllowingUnit(
                    previousCell.Value,
                    cell,
                    unit,
                    RouteNetworkType.None,
                    terrainData,
                    out StructureData connectedRoute,
                    out bool hasDeclaredRouteEdge);
            bool constructionInheritsStructure =
                construction != null
                && construction.InheritsStructureRulesOn(
                    terrainData);
            if (constructionInheritsStructure
                && hasDeclaredRouteEdge)
            {
                // Neste terreno, a construcao deixa a aresta estrutural
                // conectada governar integralmente. Se a estrutura recusar a
                // unidade, nao ha fallback para as regras da construcao.
                if (!routeAllows
                    || !TryResolveStructureTraversal(
                        connectedRoute,
                        terrainData,
                        unit,
                        currentDomain,
                        currentHeight,
                        out StructureTraversalMode inheritedMode))
                {
                    return false;
                }

                decision = inheritedMode
                    == StructureTraversalMode.TerrainPassage
                    ? new TraversalDecision(
                        TraversalSource.Terrain)
                    : new TraversalDecision(
                        TraversalSource.Structure,
                        connectedRoute);
                return true;
            }

            if (constructionInheritsTerrain)
            {
                if (!CanTraverseUsingTerrain(
                        terrainData,
                        unit,
                        currentDomain,
                        currentHeight))
                {
                    return false;
                }

                decision = new TraversalDecision(
                    TraversalSource.Terrain);
                return true;
            }

            if (routeAllows)
            {
                if (construction != null)
                {
                    if (!CanTraverseUsingConstruction(
                            construction,
                            terrainData,
                            unit,
                            currentDomain,
                            currentHeight))
                    {
                        return false;
                    }

                    decision = new TraversalDecision(
                        TraversalSource.Construction);
                }
                else
                {
                    if (!TryResolveStructureTraversal(
                            connectedRoute,
                            terrainData,
                            unit,
                            currentDomain,
                            currentHeight,
                            out StructureTraversalMode routeMode))
                    {
                        return false;
                    }

                    decision = routeMode
                        == StructureTraversalMode.TerrainPassage
                        ? new TraversalDecision(
                            TraversalSource.Terrain)
                        : new TraversalDecision(
                            TraversalSource.Structure,
                            connectedRoute);
                }
                return true;
            }
        }

        if (constructionInheritsTerrain)
        {
            if (!CanTraverseUsingTerrain(
                    terrainData,
                    unit,
                    currentDomain,
                    currentHeight))
            {
                return false;
            }

            decision = new TraversalDecision(
                TraversalSource.Terrain);
            return true;
        }

        bool routeGateFailed = structure != null
            && previousCell.HasValue
            && cache != null
            && structure.ExigeRotaDeclaradaEm(terrainData)
            && !cache.TryGetConnectedRouteStructureAllowingUnit(
                previousCell.Value,
                cell,
                unit,
                structure.routeNetworkType,
                terrainData,
                out _,
                out _);

        bool routeGateBlocks = routeGateFailed
            && (construction == null
                || structure.exigeEstruturaNaConstrucao);

        if (construction != null && !routeGateBlocks)
        {
            if (!CanTraverseUsingConstruction(
                    construction,
                    terrainData,
                    unit,
                    currentDomain,
                    currentHeight))
            {
                return false;
            }

            decision = new TraversalDecision(
                TraversalSource.Construction);
            return true;
        }

        if (structure != null)
        {
            if (!TryResolveStructureTraversal(
                    structure,
                    terrainData,
                    unit,
                    currentDomain,
                    currentHeight,
                    out StructureTraversalMode structureMode))
            {
                // A estrutura existe fisicamente neste hex. Se ela nao oferece
                // nem a camada nativa nem uma camada adicional compativel, o
                // terreno de baixo nao atravessa a estrutura (ponte sem vao =
                // dique/barragem para unidades navais).
                return false;
            }

            if (structureMode
                == StructureTraversalMode.TerrainPassage)
            {
                // Dominio adicional e permissao de passagem, nao uso do
                // conves/estrutura. O terreno conserva suas regras e custo.
                decision = new TraversalDecision(
                    TraversalSource.Terrain);
                return true;
            }

            bool crossingRouteOutsideDeclaredEdge =
                structure.routeNetworkType != RouteNetworkType.None
                && previousCell.HasValue
                && cache != null;
            if (crossingRouteOutsideDeclaredEdge
                || routeGateBlocks)
            {
                // Cruzar a infraestrutura fora da aresta declarada continua
                // sujeito ao par Estrutura+Terreno, mas exige que o terreno
                // subjacente tambem aceite a unidade. Isso deixa um caminhao
                // cruzar trilho+planicie e impede um trem de "deslizar" pela
                // mesma planicie sem conexao ferroviaria.
                if (!CanTraverseAcrossStructureUsingTerrainRules(
                        structure,
                        terrainData,
                        unit,
                        currentDomain,
                        currentHeight))
                {
                    return false;
                }
            }

            decision = new TraversalDecision(
                TraversalSource.Structure,
                structure);
            return true;
        }

        // Sem estrutura, vale somente o terreno.
        if (terrainData == null)
        {
            if (terrainRulesAvailable || !hasAnyTile)
                return false;
            decision = new TraversalDecision(TraversalSource.Terrain);
            return true;
        }

        if (!CanTraverseUsingTerrain(
                terrainData,
                unit,
                currentDomain,
                currentHeight))
        {
            return false;
        }

        decision = new TraversalDecision(TraversalSource.Terrain);
        return true;
    }

    private static bool CanTraverseUsingConstruction(
        ConstructionManager construction,
        TerrainTypeData terrainData,
        UnitManager unit,
        Domain currentDomain,
        HeightLevel currentHeight)
    {
        if (construction == null || unit == null)
            return false;

        if (currentDomain == Domain.Air)
        {
            if (construction.AllowsAirDomain())
            {
                return UnitPassesSkillRules(
                    unit,
                    construction.GetRequiredSkillsToEnter(terrainData),
                    construction.GetBlockedSkillsToEnter(terrainData));
            }
            if (!construction.SupportsLayerMode(currentDomain, currentHeight))
                return false;
            return UnitPassesSkillRules(
                unit,
                construction.GetRequiredSkillsToEnter(terrainData),
                construction.GetBlockedSkillsToEnter(terrainData));
        }

        IReadOnlyList<UnitLayerMode> unitModes = unit.GetAllLayerModes();
        for (int i = 0; i < unitModes.Count; i++)
        {
            UnitLayerMode mode = unitModes[i];
            if (construction.SupportsLayerMode(mode.domain, mode.heightLevel))
            {
                return UnitPassesSkillRules(
                    unit,
                    construction.GetRequiredSkillsToEnter(terrainData),
                    construction.GetBlockedSkillsToEnter(terrainData));
            }
        }

        return false;
    }

    private static bool CanTraverseUsingStructure(
        StructureData structure,
        TerrainTypeData terrainData,
        UnitManager unit,
        Domain currentDomain,
        HeightLevel currentHeight)
    {
        return TryResolveStructureTraversal(
            structure,
            terrainData,
            unit,
            currentDomain,
            currentHeight,
            out _);
    }

    private static bool TryResolveStructureTraversal(
        StructureData structure,
        TerrainTypeData terrainData,
        UnitManager unit,
        Domain currentDomain,
        HeightLevel currentHeight,
        out StructureTraversalMode mode)
    {
        mode = StructureTraversalMode.None;
        if (structure == null || unit == null)
            return false;

        // Veto do par Estrutura+Terreno vem primeiro: se o par proibiu a camada, nao ha
        // concessao que valha — nem o dominio nativo, nem o adicional, nem o terreno base.
        if (structure.IsLayerBlockedAt(terrainData, currentDomain, currentHeight))
            return false;

        if (currentDomain == Domain.Air)
        {
            if (structure.alwaysAllowAirDomain)
            {
                mode = StructureTraversalMode.NativeStructure;
                return UnitPassesSkillRules(
                    unit,
                    structure.GetRequiredSkillsToEnter(terrainData),
                    structure.GetBlockedSkillsToEnter(terrainData));
            }
            if (!StructureSupportsMode(structure, currentDomain, currentHeight))
                return false;
            mode = StructureTraversalMode.NativeStructure;
            return UnitPassesSkillRules(
                unit,
                structure.GetRequiredSkillsToEnter(terrainData),
                structure.GetBlockedSkillsToEnter(terrainData));
        }

        // Camada ADICIONAL da estrutura = atravessar por outro andar que nao o dela, tipicamente
        // passar POR BAIXO (navio e submarino sob a ponte). Duas correcoes em relacao a tratar
        // isso como travessia normal:
        //
        // 1) O terreno base precisa suportar a camada. Uma ponte concede naval/submerso porque
        //    ha MAR embaixo; apoiada em planicie ou montanha ela nao cria agua, e sem esta
        //    checagem um submarino navegaria por baixo de um campo.
        //
        // 2) As skills da estrutura governam quem anda POR CIMA, nao quem passa por baixo. A
        //    ponte ferroviaria exige a skill de trilho para o traçado superior; aplicar isso ao
        //    vao afundava a regra em quem so queria cruzar o rio — era por isso que o navio
        //    passava sob a ponte rodoviaria e nao sob a ferroviaria.
        if (structure.domain == currentDomain
            && structure.heightLevel == currentHeight)
        {
            if (!UnitPassesSkillRules(
                    unit,
                    structure.GetRequiredSkillsToEnter(terrainData),
                    structure.GetBlockedSkillsToEnter(terrainData)))
            {
                return false;
            }

            mode = StructureTraversalMode.NativeStructure;
            return true;
        }

        if (StructureSupportsAdditionalMode(
                structure,
                currentDomain,
                currentHeight))
        {
            if (!CanTraverseUsingTerrainLayerMode(
                    terrainData,
                    unit,
                    currentDomain,
                    currentHeight))
            {
                return false;
            }

            mode = StructureTraversalMode.TerrainPassage;
            return true;
        }

        IReadOnlyList<UnitLayerMode> unitModes = unit.GetAllLayerModes();
        for (int i = 0; i < unitModes.Count; i++)
        {
            UnitLayerMode unitMode = unitModes[i];
            if (structure.domain == unitMode.domain
                && structure.heightLevel == unitMode.heightLevel)
            {
                if (!UnitPassesSkillRules(
                        unit,
                        structure.GetRequiredSkillsToEnter(terrainData),
                        structure.GetBlockedSkillsToEnter(terrainData)))
                {
                    return false;
                }

                mode = StructureTraversalMode.NativeStructure;
                return true;
            }
        }

        for (int i = 0; i < unitModes.Count; i++)
        {
            UnitLayerMode unitMode = unitModes[i];
            if (!StructureSupportsAdditionalMode(
                    structure,
                    unitMode.domain,
                    unitMode.heightLevel))
            {
                continue;
            }
            if (structure.IsLayerBlockedAt(
                    terrainData,
                    unitMode.domain,
                    unitMode.heightLevel))
            {
                continue;
            }
            if (!CanTraverseUsingTerrainLayerMode(
                    terrainData,
                    unit,
                    unitMode.domain,
                    unitMode.heightLevel))
            {
                continue;
            }

            mode = StructureTraversalMode.TerrainPassage;
            return true;
        }

        return false;
    }

    private static bool CanTraverseAcrossStructureUsingTerrainRules(
        StructureData structure,
        TerrainTypeData terrainData,
        UnitManager unit,
        Domain currentDomain,
        HeightLevel currentHeight)
    {
        if (structure == null || unit == null)
            return false;
        if (structure.IsLayerBlockedAt(
                terrainData,
                currentDomain,
                currentHeight))
        {
            return false;
        }
        if (!CanTraverseUsingTerrain(
                terrainData,
                unit,
                currentDomain,
                currentHeight))
        {
            return false;
        }

        return UnitPassesSkillRules(
            unit,
            structure.GetRequiredSkillsToEnter(terrainData),
            structure.GetBlockedSkillsToEnter(terrainData));
    }

    private static bool CanTraverseUsingTerrainLayerMode(
        TerrainTypeData terrainData,
        UnitManager unit,
        Domain domain,
        HeightLevel height)
    {
        if (terrainData == null || unit == null)
            return false;
        if (!TerrainSupportsMode(terrainData, domain, height))
            return false;

        return UnitPassesSkillRules(
            unit,
            terrainData.requiredSkillsToEnter,
            terrainData.blockedSkills);
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

    private static bool StructureQualifiesForRouteNetwork(
        StructureData structure,
        UnitManager unit,
        RouteNetworkType requiredNetwork,
        TerrainTypeData terrainData)
    {
        if (structure == null || unit == null)
            return false;
        if (requiredNetwork != RouteNetworkType.None
            && structure.routeNetworkType != requiredNetwork)
        {
            return false;
        }

        return CanTraverseUsingStructure(
            structure,
            terrainData,
            unit,
            unit.GetDomain(),
            unit.GetHeightLevel());
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
        TerrainTypeData terrainData,
        UnitManager unit,
        bool applyOperationalAutonomyModifier,
        TraversalDecision traversal)
    {
        int baseCost;
        if (unit != null && unit.GetDomain() == Domain.Air)
            baseCost = 1;
        else if (traversal.source == TraversalSource.Construction
            && construction != null)
        {
            baseCost = GetAutonomyCostWithSkillOverrides(
                construction.GetBaseMovementCost(),
                construction.GetSkillCostOverrides(terrainData),
                unit);
        }
        else if (traversal.source == TraversalSource.Structure
            && traversal.structure != null)
        {
            // Uso nativo da estrutura: o par Estrutura+Terreno vence quando
            // configurado; se nao houver override no par, cai no global da
            // estrutura. Overrides do terreno so participam quando a decisao
            // de travessia e Terrain (ex.: navio sob uma ponte).
            StructureData structure = traversal.structure;
            baseCost = GetAutonomyCostWithSkillOverrides(
                structure.baseMovementCost,
                structure.GetSkillCostOverrides(terrainData),
                unit);
        }
        else if (traversal.source == TraversalSource.Terrain
            && terrainData != null)
        {
            baseCost = GetAutonomyCostWithSkillOverrides(terrainData.basicAutonomyCost, terrainData.skillCostOverrides, unit);
        }
        else
            baseCost = 1;

        return OperationalAutonomyRules.ApplyMovementAutonomyCost(unit, baseCost, applyOperationalAutonomyModifier);
    }

    private static TraversalDecision ResolveDefaultTraversalDecision(
        ConstructionManager construction,
        StructureData structure,
        TerrainTypeData terrainData)
    {
        if (construction != null)
        {
            if (construction.InheritsTerrainRulesOn(terrainData)
                && terrainData != null)
            {
                return new TraversalDecision(
                    TraversalSource.Terrain);
            }
            return new TraversalDecision(TraversalSource.Construction);
        }
        if (structure != null)
        {
            return new TraversalDecision(
                TraversalSource.Structure,
                structure);
        }
        return terrainData != null
            ? new TraversalDecision(TraversalSource.Terrain)
            : default;
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

        bool hasAnyValidRequiredSkill = false;
        for (int i = 0; i < requiredSkills.Count; i++)
        {
            SkillData requiredSkill = requiredSkills[i];
            if (requiredSkill == null)
                continue;

            hasAnyValidRequiredSkill = true;
            if (unit.HasSkill(requiredSkill))
                return true;
        }

        if (!hasAnyValidRequiredSkill)
            return true;

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

    // A vizinhanca imediata de um hex depende apenas do layout do Grid, nunca do
    // conteudo do Tilemap: esta funcao nao consulta HasTile. O resultado tambem e
    // invariante a translacao/rotacao do GameObject, porque so a ORDENACAO das
    // distancias relativas importa. Por isso a memoizacao vale pela vida do Tilemap.
    //
    // Sem o cache, cada chamada custava 25 GetCellCenterWorld (interop nativo),
    // uma alocacao de lista e um Sort. Com ~62 call sites -- BFS de movimento,
    // supersampling de LoS, cost maps -- isso dominava o custo de um turno.
    private static readonly Dictionary<HexGeometryCellKey, Vector3Int[]> immediateHexNeighborsCache =
        new Dictionary<HexGeometryCellKey, Vector3Int[]>(8192);
    private static readonly List<CellDistance> immediateHexNeighborScratch =
        new List<CellDistance>(24);
    private static readonly System.Comparison<CellDistance> immediateHexNeighborComparison =
        (a, b) => a.distance.CompareTo(b.distance);

    // Escape hatch para quem alterar o layout do Grid em runtime ou quiser
    // liberar a memoria de mapas ja descarregados.
    public static void ClearHexGeometryCaches()
    {
        immediateHexNeighborsCache.Clear();
    }

    // O Tilemap de uma cena descarregada nunca volta: sem isto as entradas dela
    // ficariam residentes a cada troca de mapa. Reaquecer custa uma passada,
    // entao descartar tudo e preferivel a varrer o dicionario por instancia.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterHexGeometryCacheInvalidation()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloadedClearHexGeometry;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloadedClearHexGeometry;
    }

    private static void OnSceneUnloadedClearHexGeometry(
        UnityEngine.SceneManagement.Scene scene)
    {
        ClearHexGeometryCaches();
    }

    public static void GetImmediateHexNeighbors(Tilemap terrainTilemap, Vector3Int cell, List<Vector3Int> output)
    {
        output.Clear();
        if (terrainTilemap == null)
            return;

        HexGeometryCellKey cacheKey = new HexGeometryCellKey(
            terrainTilemap.GetEntityId().GetHashCode(),
            cell.x,
            cell.y);
        if (immediateHexNeighborsCache.TryGetValue(cacheKey, out Vector3Int[] cachedNeighbors))
        {
            // Copia para a lista do chamador: o array do cache nunca escapa,
            // senao um caller que ordena/remove corromperia todos os demais.
            for (int i = 0; i < cachedNeighbors.Length; i++)
                output.Add(cachedNeighbors[i]);
            return;
        }

        Vector3 centerWorld = terrainTilemap.GetCellCenterWorld(cell);
        List<CellDistance> candidates = immediateHexNeighborScratch;
        candidates.Clear();

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

        candidates.Sort(immediateHexNeighborComparison);

        int count = Mathf.Min(6, candidates.Count);
        Vector3Int[] neighbors = new Vector3Int[count];
        for (int i = 0; i < count; i++)
        {
            neighbors[i] = candidates[i].cell;
            output.Add(neighbors[i]);
        }

        candidates.Clear();
        immediateHexNeighborsCache[cacheKey] = neighbors;
    }

    public static bool HasTraversableRouteIgnoringUnits(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDatabase,
        UnitManager unit,
        Vector3Int origin,
        Vector3Int destination,
        int maxExpanded = 200000)
    {
        if (terrainTilemap == null || unit == null)
            return false;

        origin.z = 0;
        destination.z = 0;
        if (origin == destination)
            return true;

        var cache = new MovementQueryCache(
            terrainTilemap,
            terrainDatabase);
        var queue = new Queue<Vector3Int>();
        var visited = new HashSet<Vector3Int>();
        var neighbors = new List<Vector3Int>(6);
        queue.Enqueue(origin);
        visited.Add(origin);

        int expanded = 0;
        int safeMaxExpanded = Mathf.Max(1, maxExpanded);
        while (queue.Count > 0
            && expanded++ < safeMaxExpanded)
        {
            Vector3Int current = queue.Dequeue();
            GetImmediateHexNeighbors(
                terrainTilemap,
                current,
                neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (visited.Contains(next))
                    continue;

                ConstructionManager construction =
                    cache.GetConstructionAtCell(next);
                StructureData structure =
                    cache.GetStructureAtCell(next);
                TerrainTypeData terrainData =
                    cache.ResolveTerrainAtCell(next);
                bool hasAnyTile =
                    cache.HasAnyPaintedTileAtCell(next);
                if (!TryResolveTraversal(
                        next,
                        cache,
                        construction,
                        structure,
                        terrainData,
                        hasAnyTile,
                        terrainDatabase != null,
                        unit,
                        current,
                        out _))
                {
                    continue;
                }

                if (next == destination)
                    return true;

                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return false;
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

    // Chave da memoizacao geometrica: identidade do Tilemap + celula no plano.
    private readonly struct HexGeometryCellKey : System.IEquatable<HexGeometryCellKey>
    {
        private readonly int tilemapInstanceId;
        private readonly int cellX;
        private readonly int cellY;

        public HexGeometryCellKey(int tilemapInstanceId, int cellX, int cellY)
        {
            this.tilemapInstanceId = tilemapInstanceId;
            this.cellX = cellX;
            this.cellY = cellY;
        }

        public bool Equals(HexGeometryCellKey other)
        {
            return tilemapInstanceId == other.tilemapInstanceId
                && cellX == other.cellX
                && cellY == other.cellY;
        }

        public override bool Equals(object obj)
        {
            return obj is HexGeometryCellKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + tilemapInstanceId;
                hash = (hash * 31) + cellX;
                hash = (hash * 31) + cellY;
                return hash;
            }
        }
    }

    private sealed class MovementQueryCache
    {
        private static readonly IReadOnlyList<UnitManager> EmptyUnits = System.Array.Empty<UnitManager>();
        private readonly Tilemap referenceTilemap;
        private readonly TerrainDatabase terrainDatabase;
        private readonly BoardTopologyIndex topology;
        private readonly bool topologyTerrainCompatible;
        private readonly Tilemap[] gridTilemaps;
        private readonly RoadNetworkManager[] roadNetworks;
        private readonly Dictionary<Vector3Int, List<UnitManager>> unitsByCell = new Dictionary<Vector3Int, List<UnitManager>>();
        private readonly Dictionary<Vector3Int, ConstructionManager> constructionByCell = new Dictionary<Vector3Int, ConstructionManager>();
        private readonly Dictionary<Vector3Int, StructureData> structureByCell = new Dictionary<Vector3Int, StructureData>();
        private readonly Dictionary<Vector3Int, List<StructureData>> routeStructuresByCell = new Dictionary<Vector3Int, List<StructureData>>();
        private readonly Dictionary<Vector3Int, TerrainTypeData> terrainByCell = new Dictionary<Vector3Int, TerrainTypeData>();
        private readonly HashSet<Vector3Int> terrainMisses = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector3Int, bool> hasAnyTileByCell = new Dictionary<Vector3Int, bool>();
        private readonly Dictionary<(Vector3Int from, Vector3Int to, RouteNetworkType network), StructureData>
            routeStructureByEdge =
                new Dictionary<(Vector3Int from, Vector3Int to, RouteNetworkType network), StructureData>();
        private readonly Dictionary<(Vector3Int from, Vector3Int to, RouteNetworkType network), bool>
            routeStructureMisses =
                new Dictionary<(Vector3Int from, Vector3Int to, RouteNetworkType network), bool>();

        public MovementQueryCache(Tilemap referenceTilemap, TerrainDatabase terrainDatabase)
        {
            this.referenceTilemap = referenceTilemap;
            this.terrainDatabase = terrainDatabase;
            AIDecisionPerf.AddCount("MovementQueryCachesBuilt");

            BoardTopologyIndex.TryGetFor(
                referenceTilemap,
                out BoardTopologyIndex resolvedTopology);
            topology = resolvedTopology;
            topologyTerrainCompatible =
                topology != null
                && terrainDatabase != null
                && topology.TerrainDatabase == terrainDatabase;

            if (referenceTilemap != null && referenceTilemap.layoutGrid != null)
                gridTilemaps = referenceTilemap.layoutGrid.GetComponentsInChildren<Tilemap>(includeInactive: true);
            else
                gridTilemaps = System.Array.Empty<Tilemap>();

            bool useConfirmedIndices =
                ConfirmedOccupancyIndex.TryGetFor(
                    referenceTilemap,
                    out ConfirmedOccupancyIndex occupancy)
                && occupancy != null
                && occupancy.CanServeLiveQueries;
            if (useConfirmedIndices)
            {
                IReadOnlyList<UnitManager> confirmedUnits =
                    occupancy.BoardUnits;
                for (int i = 0; i < confirmedUnits.Count; i++)
                    IndexUnit(confirmedUnits[i]);
                AIDecisionPerf.AddCount(
                    "MovementQueryConfirmedOccupancyUses");
            }
            else
            {
                UnitManager[] liveUnits =
                    Object.FindObjectsByType<UnitManager>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                for (int i = 0; i < liveUnits.Length; i++)
                    IndexUnit(liveUnits[i]);
                AIDecisionPerf.AddCount(
                    "MovementQueryLiveOccupancyFallbacks");
            }

            IReadOnlyList<ConstructionManager> activeConstructions =
                ConstructionManager.AllActive;
            if (useConfirmedIndices
                && activeConstructions != null
                && activeConstructions.Count > 0)
            {
                for (int i = 0;
                     i < activeConstructions.Count;
                     i++)
                {
                    IndexConstruction(activeConstructions[i]);
                }
            }
            else
            {
                ConstructionManager[] liveConstructions =
                    Object.FindObjectsByType<ConstructionManager>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                for (int i = 0; i < liveConstructions.Length; i++)
                    IndexConstruction(liveConstructions[i]);
            }

            roadNetworks = topology != null
                ? System.Array.Empty<RoadNetworkManager>()
                : Object.FindObjectsByType<RoadNetworkManager>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            if (topology != null)
                IndexTopologyRouteStructures();
        }

        public ConstructionManager GetConstructionAtCell(Vector3Int cell)
        {
            cell.z = 0;
            return constructionByCell.TryGetValue(
                    cell,
                    out ConstructionManager construction)
                ? construction
                : null;
        }

        public StructureData GetStructureAtCell(Vector3Int cell)
        {
            cell.z = 0;
            if (structureByCell.TryGetValue(cell, out StructureData cachedStructure))
                return cachedStructure;

            StructureData found = null;
            if (topology != null
                && topology.TryGetStructure(
                    cell,
                    out StructureData indexedStructure))
            {
                found = indexedStructure;
            }

            for (int i = 0;
                 found == null && i < roadNetworks.Length;
                 i++)
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

        public bool IsRoadBoostEdge(
            Vector3Int fromCell,
            Vector3Int toCell,
            UnitManager unit)
        {
            toCell.z = 0;
            TerrainTypeData terrain =
                ResolveTerrainAtCell(toCell);
            return TryGetConnectedRouteStructureAllowingUnit(
                    fromCell,
                    toCell,
                    unit,
                    RouteNetworkType.Asfaltado,
                    terrain,
                    out StructureData structure,
                    out _)
                && structure != null
                && structure.IsRoadBoostEnabled(terrain);
        }

        public bool TryGetAnyRouteStructureAtCellAllowingUnit(
            Vector3Int cell,
            UnitManager unit,
            RouteNetworkType requiredNetwork,
            TerrainTypeData terrainData,
            out StructureData matchedStructure)
        {
            matchedStructure = null;
            cell.z = 0;
            if (routeStructuresByCell.TryGetValue(
                    cell,
                    out List<StructureData> indexedStructures))
            {
                for (int i = 0; i < indexedStructures.Count; i++)
                {
                    StructureData candidate = indexedStructures[i];
                    if (!StructureQualifiesForRouteNetwork(
                            candidate,
                            unit,
                            requiredNetwork,
                            terrainData))
                    {
                        continue;
                    }

                    if (IsBetterRouteStructure(
                            candidate,
                            matchedStructure))
                    {
                        matchedStructure = candidate;
                    }
                }
                return matchedStructure != null;
            }

            for (int i = 0; i < roadNetworks.Length; i++)
            {
                RoadNetworkManager network = roadNetworks[i];
                if (network == null
                    || !network.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Tilemap networkTilemap = network.BoardTilemap;
                if (!IsCompatibleReference(
                        referenceTilemap,
                        networkTilemap))
                {
                    continue;
                }

                StructureDatabase db = network.StructureDatabase;
                IReadOnlyList<StructureData> structures =
                    db != null ? db.Structures : null;
                if (structures == null)
                    continue;

                for (int s = 0; s < structures.Count; s++)
                {
                    StructureData structure = structures[s];
                    if (!StructureQualifiesForRouteNetwork(
                            structure,
                            unit,
                            requiredNetwork,
                            terrainData))
                    {
                        continue;
                    }

                    IReadOnlyList<RoadRouteDefinition> routes =
                        db.GetRoadRoutes(structure);
                    if (routes == null)
                        routes = structure.roadRoutes;
                    if (routes == null)
                        continue;

                    bool containsCell = false;
                    for (int r = 0; r < routes.Count; r++)
                    {
                        RoadRouteDefinition route = routes[r];
                        if (route == null || route.cells == null)
                            continue;

                        for (int c = 0; c < route.cells.Count; c++)
                        {
                            Vector3Int routeCell = route.cells[c];
                            routeCell.z = 0;
                            if (routeCell == cell)
                            {
                                containsCell = true;
                                break;
                            }
                        }

                        if (containsCell)
                            break;
                    }

                    if (containsCell
                        && IsBetterRouteStructure(
                            structure,
                            matchedStructure))
                    {
                        matchedStructure = structure;
                    }
                }
            }
            return matchedStructure != null;
        }

        public bool TryGetConnectedRouteStructureAllowingUnit(
            Vector3Int fromCell,
            Vector3Int toCell,
            UnitManager unit,
            RouteNetworkType requiredNetwork,
            TerrainTypeData destinationTerrain,
            out StructureData matchedStructure,
            out bool hasDeclaredRouteEdge)
        {
            matchedStructure = null;
            hasDeclaredRouteEdge = false;
            fromCell.z = 0;
            toCell.z = 0;
            if (fromCell == toCell)
                return false;

            var edge = (
                from: fromCell,
                to: toCell,
                network: requiredNetwork);
            if (routeStructureByEdge.TryGetValue(
                    edge,
                    out matchedStructure))
            {
                hasDeclaredRouteEdge = true;
                return matchedStructure != null;
            }
            if (routeStructureMisses.TryGetValue(
                    edge,
                    out hasDeclaredRouteEdge))
            {
                return false;
            }

            if (topology != null)
            {
                if (topology.TryGetRouteStructures(
                        fromCell,
                        toCell,
                        out IReadOnlyList<StructureData>
                            indexedStructures))
                {
                    for (int i = 0;
                         i < indexedStructures.Count;
                         i++)
                    {
                        StructureData candidate =
                            indexedStructures[i];
                        if (candidate == null
                            || (requiredNetwork != RouteNetworkType.None
                                && candidate.routeNetworkType
                                    != requiredNetwork))
                        {
                            continue;
                        }

                        hasDeclaredRouteEdge = true;
                        if (!StructureQualifiesForRouteNetwork(
                                candidate,
                                unit,
                                requiredNetwork,
                                destinationTerrain))
                        {
                            continue;
                        }

                        if (IsBetterRouteStructure(
                                candidate,
                                matchedStructure))
                        {
                            matchedStructure = candidate;
                        }
                    }
                }

                CacheRouteStructure(
                    fromCell,
                    toCell,
                    requiredNetwork,
                    matchedStructure,
                    hasDeclaredRouteEdge);
                return matchedStructure != null;
            }

            for (int i = 0; i < roadNetworks.Length; i++)
            {
                RoadNetworkManager network = roadNetworks[i];
                if (network == null
                    || !network.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Tilemap networkTilemap = network.BoardTilemap;
                if (!IsCompatibleReference(
                        referenceTilemap,
                        networkTilemap))
                {
                    continue;
                }

                StructureDatabase db = network.StructureDatabase;
                IReadOnlyList<StructureData> structures =
                    db != null ? db.Structures : null;
                if (structures == null)
                    continue;

                for (int s = 0; s < structures.Count; s++)
                {
                    StructureData structure = structures[s];
                    if (structure == null
                        || (requiredNetwork != RouteNetworkType.None
                            && structure.routeNetworkType
                                != requiredNetwork))
                    {
                        continue;
                    }

                    IReadOnlyList<RoadRouteDefinition> routes =
                        db.GetRoadRoutes(structure);
                    if (routes == null)
                        routes = structure.roadRoutes;
                    if (routes == null)
                        continue;

                    bool containsEdge = false;
                    for (int r = 0; r < routes.Count; r++)
                    {
                        RoadRouteDefinition route = routes[r];
                        if (route == null
                            || route.cells == null
                            || route.cells.Count < 2)
                        {
                            continue;
                        }

                        for (int c = 1;
                             c < route.cells.Count;
                             c++)
                        {
                            Vector3Int a = route.cells[c - 1];
                            Vector3Int b = route.cells[c];
                            a.z = 0;
                            b.z = 0;

                            if ((a == fromCell && b == toCell)
                                || (a == toCell
                                    && b == fromCell))
                            {
                                containsEdge = true;
                                break;
                            }
                        }

                        if (containsEdge)
                            break;
                    }

                    if (containsEdge)
                        hasDeclaredRouteEdge = true;

                    if (containsEdge
                        && StructureQualifiesForRouteNetwork(
                            structure,
                            unit,
                            requiredNetwork,
                            destinationTerrain)
                        && IsBetterRouteStructure(
                            structure,
                            matchedStructure))
                    {
                        matchedStructure = structure;
                    }
                }
            }

            CacheRouteStructure(
                fromCell,
                toCell,
                requiredNetwork,
                matchedStructure,
                hasDeclaredRouteEdge);
            return matchedStructure != null;
        }

        private void CacheRouteStructure(
            Vector3Int fromCell,
            Vector3Int toCell,
            RouteNetworkType network,
            StructureData structure,
            bool hasDeclaredRouteEdge)
        {
            var forward = (
                from: fromCell,
                to: toCell,
                network);
            if (structure != null)
            {
                routeStructureByEdge[forward] = structure;
                routeStructureMisses.Remove(forward);
                return;
            }

            routeStructureMisses[forward] = hasDeclaredRouteEdge;
        }

        private static bool IsBetterRouteStructure(
            StructureData candidate,
            StructureData current)
        {
            if (candidate == null)
                return false;
            if (current == null)
                return true;
            if (candidate.priorityOrder != current.priorityOrder)
            {
                return candidate.priorityOrder
                    > current.priorityOrder;
            }

            return string.CompareOrdinal(
                    candidate.id ?? string.Empty,
                    current.id ?? string.Empty)
                < 0;
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

            if (topologyTerrainCompatible
                && topology.TryGetTerrain(
                    cell,
                    out TerrainTypeData indexedTerrain))
            {
                terrainByCell[cell] = indexedTerrain;
                return indexedTerrain;
            }

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

            if (topology != null)
            {
                bool indexed = topology.TryGetCell(
                        cell,
                        out BoardTopologyCellRecord record)
                    && record != null
                    && record.hasAnyPaintedTile;
                hasAnyTileByCell[cell] = indexed;
                return indexed;
            }

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
                    if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsDead)
                        continue;

                    if (PlayerSlotRelations.AreAllies(unit, exceptUnit))
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
                if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsDead)
                    continue;

                Vector3Int occupiedCell = unit.CurrentCellPosition;
                occupiedCell.z = 0;
                if (occupiedCell == cell)
                    return unit;
            }

            return null;
        }

        public IReadOnlyList<UnitManager> GetUnitsAtCell(Vector3Int cell, UnitManager exceptUnit = null)
        {
            cell.z = 0;
            if (!unitsByCell.TryGetValue(cell, out List<UnitManager> occupants) || occupants == null || occupants.Count == 0)
                return EmptyUnits;

            if (exceptUnit == null)
                return occupants;

            List<UnitManager> filtered = null;
            for (int i = 0; i < occupants.Count; i++)
            {
                UnitManager unit = occupants[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsDead || unit.IsEmbarked)
                    continue;

                Vector3Int occupiedCell = unit.CurrentCellPosition;
                occupiedCell.z = 0;
                if (occupiedCell != cell)
                    continue;

                if (filtered == null)
                    filtered = new List<UnitManager>(occupants.Count);
                filtered.Add(unit);
            }

            return filtered ?? EmptyUnits;
        }

        private void IndexUnit(UnitManager unit)
        {
            if (unit == null
                || !unit.gameObject.activeInHierarchy
                || unit.IsEmbarked
                || unit.IsDead
                || !IsUnitOnReferenceMap(
                    unit,
                    referenceTilemap))
            {
                return;
            }

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (!unitsByCell.TryGetValue(
                    occupiedCell,
                    out List<UnitManager> occupants))
            {
                occupants = new List<UnitManager>(1);
                unitsByCell[occupiedCell] = occupants;
            }
            occupants.Add(unit);
        }

        private void IndexConstruction(
            ConstructionManager construction)
        {
            if (construction == null
                || !construction.gameObject.activeInHierarchy)
            {
                return;
            }

            Vector3Int occupiedCell =
                construction.BoardTilemap == referenceTilemap
                    ? construction.CurrentCellPosition
                    : HexCoordinates.WorldToCell(
                        referenceTilemap,
                        construction.transform.position);
            occupiedCell.z = 0;
            if (constructionByCell.ContainsKey(occupiedCell))
                return;

            // Preserva a semantica historica da primeira construcao
            // encontrada: uma construcao fake ocupa a entrada com null.
            constructionByCell[occupiedCell] =
                construction.IsFakeBuilding
                    ? null
                    : construction;
        }

        private void IndexTopologyRouteStructures()
        {
            IReadOnlyList<BoardTopologyRouteEdgeRecord> edges =
                topology.RouteEdges;
            if (edges == null)
                return;

            for (int i = 0; i < edges.Count; i++)
            {
                BoardTopologyRouteEdgeRecord edge = edges[i];
                if (edge == null || edge.structure == null)
                    continue;
                AddRouteStructure(edge.from, edge.structure);
                AddRouteStructure(edge.to, edge.structure);
            }
        }

        private void AddRouteStructure(
            Vector3Int cell,
            StructureData structure)
        {
            cell.z = 0;
            if (!routeStructuresByCell.TryGetValue(
                    cell,
                    out List<StructureData> structures))
            {
                structures = new List<StructureData>(1);
                routeStructuresByCell[cell] = structures;
            }
            if (!structures.Contains(structure))
                structures.Add(structure);
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
