using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Helpers de combate e posicionamento de ataque (capturadores)
    // -------------------------------------------------------------------------

    // 3 = defensor do prédio objetivo  2 = inimigo em qualquer construção  1 = terreno aberto
    private float AttackTargetPriority(Vector3Int targetUnitCell, Vector3Int captureTargetCell)
    {
        if (targetUnitCell == captureTargetCell) return 3f;
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetUnitCell);
        return bldg != null ? 2f : 1f;
    }

    // Perseguidor: prioriza inimigos mais próximos ao objetivo a capturar
    private float AttackTargetPriorityPursuer(Vector3Int targetUnitCell, Vector3Int captureTargetCell)
    {
        if (targetUnitCell == captureTargetCell) return 3f;
        float dist = SectorManager.HexDistance(targetUnitCell, captureTargetCell);
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetUnitCell);
        float bldgBonus = bldg != null ? 0.5f : 0f;
        return 2f - dist * 0.1f + bldgBonus;
    }

    private static BazookaTargetPriority ResolveCapturerTargetPreference(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return BazookaTargetPriority.Tertiary;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return BazookaTargetPriority.Tertiary;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return BazookaTargetPriority.Tertiary;

        return attackerData.ResolveAiTargetPriorityForTargetClass(targetData.unitClass);
    }

    private static float GetCapturerTargetPreferenceScore(BazookaTargetPriority priority)
    {
        switch (priority)
        {
            case BazookaTargetPriority.Primary:
                return 30000f;
            case BazookaTargetPriority.Secondary:
                return 15000f;
            default:
                return 0f;
        }
    }

    private static float GetCapturerTargetPreferenceTie(BazookaTargetPriority priority)
    {
        switch (priority)
        {
            case BazookaTargetPriority.Primary:
                return 20f;
            case BazookaTargetPriority.Secondary:
                return 10f;
            default:
                return 0f;
        }
    }

    private UnitManager PickBestRogueTarget(List<PodeMirarTargetOption> options, TeamId aiTeam)
    {
        return PickBestRogueTarget(options, aiTeam, null, default, false, out _);
    }

    private UnitManager PickBestRogueTarget(
        List<PodeMirarTargetOption> options,
        TeamId aiTeam,
        UnitManager attacker,
        Vector3Int attackCell,
        bool defensiveContext,
        out string attackDecisionReason)
    {
        attackDecisionReason = "";
        UnitManager best = null;
        float bestPriority = float.MinValue;
        foreach (PodeMirarTargetOption opt in options)
        {
            if (opt?.targetUnit == null || opt.targetUnit.TeamId == aiTeam) continue;
            string decisionReason = "";
            if (attacker != null
                && !PassesAttackDecision(attacker, opt.targetUnit, attackCell, defensiveContext, out decisionReason))
            {
                continue;
            }
            float priority = 10f - opt.targetUnit.CurrentHP;

            Vector3Int ec = opt.targetUnit.CurrentCellPosition; ec.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, ec);
            if (bldg != null && bldg.IsCapturable
                && !(bldg.TeamId == aiTeam && bldg.CurrentCapturePoints >= bldg.CapturePointsMax))
                priority += 1000f;

            if (priority > bestPriority)
            {
                bestPriority = priority;
                best = opt.targetUnit;
                attackDecisionReason = attacker != null ? decisionReason : "";
            }
        }
        return best;
    }

    private static bool HasEnemyInEngageRadius(UnitManager unit, Vector3Int fromCell, TeamId aiTeam)
    {
        int radius = Mathf.Max(0, unit.RemainingMovementPoints) + 1;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (Vector3Int.Distance(fromCell, ec) <= radius) return true;
        }
        return false;
    }

    private static bool HasEnemyBlockingPath(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell, TeamId aiTeam)
    {
        float unitDist   = Vector3Int.Distance(fromCell, targetCell);
        int engageRadius = Mathf.Max(0, unit.RemainingMovementPoints) + 1;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (Vector3Int.Distance(fromCell, ec) > engageRadius) continue;
            if (Vector3Int.Distance(ec, targetCell) < unitDist) return true;
        }
        return false;
    }

    private bool TryFindBestLoSCell(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int targetCell,
        out Vector3Int bestCell)
    {
        bool preferDpq = unit.TryGetUnitData(out UnitData ud) && ud.prioritizeDpqAtBattle;

        bestCell = Vector3Int.zero;
        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, targetCell, neighbors);

        bool found = false;
        float bestScore = float.MinValue;
        foreach (Vector3Int n in neighbors)
        {
            Vector3Int nc = n; nc.z = 0;
            if (!paths.ContainsKey(nc) || occupied.Contains(nc)) continue;
            float score = preferDpq ? GetTerrainDpqPontos(nc) : GetTerrainEv(nc);
            if (!found || score > bestScore) { bestScore = score; bestCell = nc; found = true; }
        }
        return found;
    }

    private bool CanAttackTargetFrom(Vector3Int fromCell, Vector3Int toCell,
        UnitManager unit, UnitManager target)
    {
        SensorMovementMode mode = toCell != fromCell
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;

        var targets = new List<PodeMirarTargetOption>();
        bool hasAny = PodeMirarSensor.CollectTargets(
            unit,
            boardTilemap,
            terrainDatabase,
            mode,
            targets,
            dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
            fromCell: toCell);

        if (!hasAny) return false;
        foreach (PodeMirarTargetOption opt in targets)
            if (opt?.targetUnit == target) return true;
        return false;
    }

    private bool TryFindBetterDpqAttackCellForTarget(
        UnitManager unit,
        TeamId aiTeam,
        Vector3Int fromCell,
        UnitManager target,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        if (unit == null || target == null || paths == null || paths.Count == 0)
            return false;

        float currentDpq = GetTerrainDpqPontos(fromCell);
        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;
            if (!CanAttackTargetFrom(fromCell, cell, unit, target)) continue;
            if (!PassesAttackDecision(unit, target, cell, false, out string attackDecisionReason)) continue;

            float dpq = GetTerrainDpqPontos(cell);
            if (dpq <= currentDpq + 0.01f) continue;

            int pathCost = GetPathStepCount(paths, cell);
            float threat = CalculateThreatLevel(cell, aiTeam);
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            float score =
                (dpq - currentDpq) * 10000f
                - pathCost * 25f
                - threat * 5f
                - SectorManager.HexDistance(cell, targetCell) * 10f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                reason = $"dpq {currentDpq:F1}->{dpq:F1} path={pathCost} threat={threat:F1} score={score:F0} {attackDecisionReason}";
            }
        }

        return bestCell != fromCell;
    }

    private bool TryDecideCapturerDefensiveOpportunityAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || assigned == null || paths == null || paths.Count == 0)
            return false;
        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null)
            return false;

        bool defensiveCombatant = unitData.aiPurchaseMode == AIPurchaseMode.Defensive
            || unitData.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Armored) != BazookaTargetPriority.Tertiary;
        bool defensiveContext = snapshot.Stance == AIStance.Defensive
            || assigned.Status == ObjectiveStatus.Defending
            || defensiveCombatant;
        if (!defensiveContext)
            return false;

        MatchController mc = GetMatchController();
        UnitManager bestTarget = null;
        Vector3Int bestAttackCell = fromCell;
        float bestScore = float.MinValue;
        string bestReason = "";

        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked)
                continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, snapshot.AITeam))
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            BazookaTargetPriority targetPreference = ResolveCapturerTargetPreference(unit, enemy);

            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (occupied != null && occupied.Contains(cell))
                    continue;
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy))
                    continue;
                if (!PassesAttackDecision(unit, enemy, cell, true, out string attackDecisionReason))
                    continue;

                float dpq = GetTerrainDpqPontos(cell);
                int pathCost = GetPathStepCount(paths, cell);
                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                float score =
                    GetCapturerTargetPreferenceScore(targetPreference)
                    + AttackTargetPriorityPursuer(enemyCell, fromCell) * 3000f
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 250f
                    + dpq * 800f
                    - pathCost * 35f
                    - threat * 80f
                    - SectorManager.HexDistance(cell, enemyCell) * 25f;

                if (cell == fromCell)
                    score += 500f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                    bestAttackCell = cell;
                    bestReason = $"score={score:F0} pref={targetPreference} hp={enemy.CurrentHP} dpq={dpq:F1} threat={threat:F1} {attackDecisionReason}";
                }
            }
        }

        if (bestTarget == null)
            return false;

        Vector3Int targetCell = bestTarget.CurrentCellPosition;
        targetCell.z = 0;
        Debug.Log($"{TL("Capturador")} {unit.InstanceId} defesa oportunista: ataca {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} via {bestAttackCell} ({bestReason})");
        action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestAttackCell,
            bestTarget.InstanceId.ToString(), targetCell, paths);
        return true;
    }

    private bool TryDecideCapturerOwnedBuildingDefenseBeforeEmbark(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        MatchController mc = GetMatchController();
        bool preferDpq = unit.TryGetUnitData(out UnitData unitData) && unitData != null && unitData.prioritizeDpqAtBattle;

        UnitManager bestTarget = null;
        Vector3Int bestAttackCell = fromCell;
        float bestScore = float.MinValue;
        float bestDpq = float.MinValue;
        float bestTargetPrefScore = float.MinValue;
        string bestReason = "";

        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked)
                continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, snapshot.AITeam))
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            ConstructionManager threatenedBuilding = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, enemyCell);
            if (threatenedBuilding == null
                || !threatenedBuilding.IsCapturable
                || threatenedBuilding.TeamId != snapshot.AITeam)
                continue;

            bool contested = threatenedBuilding.CurrentCapturePoints < threatenedBuilding.CapturePointsMax;
            float captureLoss = Mathf.Max(0, threatenedBuilding.CapturePointsMax - threatenedBuilding.CurrentCapturePoints);
            BazookaTargetPriority targetPreference = ResolveCapturerTargetPreference(unit, enemy);
            float targetPrefScore = GetCapturerTargetPreferenceScore(targetPreference);

            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int attackCell = rawCell;
                attackCell.z = 0;
                if (attackCell != fromCell && occupied != null && occupied.Contains(attackCell))
                    continue;
                if (!CanAttackTargetFrom(fromCell, attackCell, unit, enemy))
                    continue;
                if (!PassesAttackDecision(unit, enemy, attackCell, true, out string attackDecisionReason))
                    continue;

                float dpq = GetTerrainDpqPontos(attackCell);
                float threat = CalculateThreatLevel(attackCell, snapshot.AITeam);
                int pathCost = GetPathStepCount(paths, attackCell);
                float score =
                    45000f
                    + (contested ? 15000f : 7000f)
                    + captureLoss * 500f
                    + targetPrefScore
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 500f
                    + dpq * 400f
                    - threat * 80f
                    - pathCost * 25f
                    - SectorManager.HexDistance(attackCell, enemyCell) * 20f
                    - enemy.InstanceId * 0.001f;

                if (attackCell == fromCell)
                    score += 650f;

                if (IsBetterAttackCandidate(preferDpq, targetPrefScore, dpq, score, 0f, 0f,
                        bestTargetPrefScore, bestDpq, bestScore, 0f, 0f))
                {
                    bestScore = score;
                    bestDpq = dpq;
                    bestTargetPrefScore = targetPrefScore;
                    bestTarget = enemy;
                    bestAttackCell = attackCell;
                    bestReason = $"score={score:F0} predio={threatenedBuilding.Sector} contested={contested} capLoss={captureLoss:F0} pref={targetPreference} hp={enemy.CurrentHP} dpq={dpq:F1} threat={threat:F1} path={pathCost} preferDpq={preferDpq} {attackDecisionReason}";
                }
            }
        }

        if (bestTarget == null)
            return false;

        Vector3Int targetCell = bestTarget.CurrentCellPosition;
        targetCell.z = 0;
        Debug.Log($"{TL("Capturador")} {unit.InstanceId} defende predio aliado antes de embarcar: ataca {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} via {bestAttackCell} ({bestReason})");
        action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestAttackCell,
            bestTarget.InstanceId.ToString(), targetCell, paths);
        return true;
    }

    private void AppendMissingDpqReachabilityDiagnostics(
        System.Text.StringBuilder log,
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int targetCell)
    {
        if (log == null || unit == null || boardTilemap == null)
            return;

        var cells = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, targetCell, cells);

        foreach (Vector3Int rawCell in cells)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (paths != null && paths.ContainsKey(cell))
                continue;

            float dpq = GetTerrainDpqPontos(cell);
            if (dpq <= 0f)
                continue;

            bool canEnter = UnitMovementPathRules.TryGetEnterCellCost(
                boardTilemap,
                unit,
                cell,
                terrainDatabase,
                applyOperationalAutonomyModifier: true,
                out int enterCost);

            string occupant = DescribeAnyUnitAtCellForDiagnostics(cell);
            string route = DescribeReachabilityFromKnownPaths(unit, paths, cell, enterCost, canEnter);
            log.AppendLine($"  {cell} MISS notReachable dpqPts={dpq:F1} enter={(canEnter ? enterCost.ToString() : "no")} {route} occ={occupant}");
        }
    }

    private void AppendSpecificReachabilityDiagnostics(
        System.Text.StringBuilder log,
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        params Vector3Int[] cells)
    {
        if (log == null || unit == null || cells == null)
            return;

        for (int i = 0; i < cells.Length; i++)
        {
            Vector3Int cell = cells[i];
            cell.z = 0;
            if (paths != null && paths.ContainsKey(cell))
                continue;

            bool canEnter = UnitMovementPathRules.TryGetEnterCellCost(
                boardTilemap,
                unit,
                cell,
                terrainDatabase,
                applyOperationalAutonomyModifier: true,
                out int enterCost);

            string occupant = DescribeAnyUnitAtCellForDiagnostics(cell);
            string route = DescribeReachabilityFromKnownPaths(unit, paths, cell, enterCost, canEnter);
            log.AppendLine($"  {cell} MISS probe dpqPts={GetTerrainDpqPontos(cell):F1} enter={(canEnter ? enterCost.ToString() : "no")} {route} occ={occupant}");
        }
    }

    private string DescribeReachabilityFromKnownPaths(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int blockedCell,
        int enterCost,
        bool canEnter)
    {
        if (unit == null || paths == null || boardTilemap == null)
            return "route=?";

        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;
        int maxMove = Mathf.Max(0, unit.RemainingMovementPoints);
        int maxFuel = Mathf.Max(0, unit.CurrentFuel);

        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, blockedCell, neighbors);

        Vector3Int bestNeighbor = Vector3Int.zero;
        int bestSteps = int.MaxValue;
        int bestPathLen = int.MaxValue;
        string bestOcc = "-";

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int neighbor = neighbors[i];
            neighbor.z = 0;
            if (paths.TryGetValue(neighbor, out List<Vector3Int> path) && path != null)
            {
                int steps = Mathf.Max(0, path.Count - 1);
                if (steps < bestSteps)
                {
                    bestSteps = steps;
                    bestPathLen = path.Count;
                    bestNeighbor = neighbor;
                }
            }
            else if (bestOcc == "-")
            {
                string occ = DescribeAnyUnitAtCellForDiagnostics(neighbor);
                if (occ != "-")
                    bestOcc = $"{neighbor}:{occ}";
            }
        }

        if (bestSteps == int.MaxValue)
            return $"route=noReachableNeighbor maxMove={maxMove} fuel={maxFuel} nearOcc={bestOcc}";

        int estimatedTotal = canEnter ? bestSteps + Mathf.Max(1, enterCost) : int.MaxValue;
        string totalText = canEnter ? estimatedTotal.ToString() : "?";
        return $"route=via {bestNeighbor} pathLen={bestPathLen} stepApprox={bestSteps}+{(canEnter ? enterCost.ToString() : "?")}={totalText} maxMove={maxMove} fuel={maxFuel}";
    }

    private string DescribeAnyUnitAtCellForDiagnostics(Vector3Int cell)
    {
        cell.z = 0;
        UnitManager found = null;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;

            Vector3Int uc = unit.CurrentCellPosition;
            uc.z = 0;
            if (uc != cell)
                continue;

            found = unit;
            break;
        }

        if (found == null)
            return "-";

        Tilemap map = found.BoardTilemap != null ? found.BoardTilemap : boardTilemap;
        Vector3Int worldCell = map != null
            ? HexCoordinates.WorldToCell(map, found.transform.position)
            : found.CurrentCellPosition;
        worldCell.z = 0;
        Vector3Int stateCell = found.CurrentCellPosition;
        stateCell.z = 0;
        bool stale = worldCell != stateCell;

        return $"{found.UnitDisplayName}#{found.InstanceId}/team={found.TeamId}/dead={found.IsDead}/emb={found.IsEmbarked}/acted={found.HasActed}/state={stateCell}/world={worldCell}/stale={stale}";
    }

    private float GetTerrainEv(Vector3Int cell)
    {
        if (boardTilemap == null || terrainDatabase == null) return 0f;
        cell.z = 0;
        TileBase tile = boardTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data) && data != null)
            return data.ev;
        return 0f;
    }

    private float GetTerrainDpqPontos(Vector3Int cell)
    {
        if (boardTilemap == null || terrainDatabase == null) return 0f;
        cell.z = 0;

        PositionDpqForAttackDecision dpq = ResolveDpqForAttackDecision(cell);
        return dpq.points;
    }

    private PositionDpqForAttackDecision ResolveDpqForAttackDecision(Vector3Int cell)
    {
        cell.z = 0;

        if (boardTilemap == null || terrainDatabase == null)
            return PositionDpqForAttackDecision.None;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction != null
            && construction.TryResolveConstructionData(out ConstructionData constructionData)
            && constructionData != null
            && constructionData.dpqData != null)
        {
            return new PositionDpqForAttackDecision(constructionData.dpqData.Pontos, constructionData.dpqData.DefesaBonus);
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardTilemap, cell);
        if (structure != null && structure.dpqData != null)
            return new PositionDpqForAttackDecision(structure.dpqData.Pontos, structure.dpqData.DefesaBonus);

        TileBase tile = boardTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data) && data?.dpqData != null)
            return new PositionDpqForAttackDecision(data.dpqData.Pontos, data.dpqData.DefesaBonus);

        GridLayout grid = boardTilemap.layoutGrid;
        if (grid != null)
        {
            Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
            for (int i = 0; i < maps.Length; i++)
            {
                Tilemap map = maps[i];
                if (map == null || map == boardTilemap)
                    continue;

                TileBase other = map.GetTile(cell);
                if (other != null && terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData otherData) && otherData?.dpqData != null)
                    return new PositionDpqForAttackDecision(otherData.dpqData.Pontos, otherData.dpqData.DefesaBonus);
            }
        }

        return PositionDpqForAttackDecision.None;
    }

    private PositionDpqForAttackDecision ResolveDpqForAttackDecision(UnitManager unit, Vector3Int cell)
    {
        if (unit != null
            && unit.GetDomain() == Domain.Air
            && turnStateManager != null
            && turnStateManager.DpqAirHeightConfigRef != null
            && turnStateManager.DpqAirHeightConfigRef.TryGetFor(unit.GetDomain(), unit.GetHeightLevel(), out DPQData airDpq)
            && airDpq != null)
        {
            return new PositionDpqForAttackDecision(airDpq.Pontos, airDpq.DefesaBonus);
        }

        return ResolveDpqForAttackDecision(cell);
    }

    private readonly struct PositionDpqForAttackDecision
    {
        public static PositionDpqForAttackDecision None => new PositionDpqForAttackDecision(0, 0);

        public readonly int points;
        public readonly int defenseBonus;

        public PositionDpqForAttackDecision(int points, int defenseBonus)
        {
            this.points = Mathf.Max(0, points);
            this.defenseBonus = defenseBonus;
        }
    }

    private static bool IsBetterAttackCandidate(
        bool preferDpqAtBattle,
        float targetPriority,
        float attackDpq,
        float score,
        float sectorTie,
        float hqTie,
        float bestTargetPriority,
        float bestAttackDpq,
        float bestScore,
        float bestSectorTie,
        float bestHqTie)
    {
        const float epsilon = 0.001f;

        if (preferDpqAtBattle)
        {
            if (targetPriority > bestTargetPriority + epsilon) return true;
            if (Mathf.Abs(targetPriority - bestTargetPriority) > epsilon) return false;

            if (attackDpq > bestAttackDpq + epsilon) return true;
            if (Mathf.Abs(attackDpq - bestAttackDpq) > epsilon) return false;
        }

        return IsBetterScore(score, sectorTie, hqTie, bestScore, bestSectorTie, bestHqTie);
    }

    private static bool IsBetterScore(float score, float sectorTie, float hqTie,
                                       float bestScore, float bestSectorTie, float bestHqTie)
    {
        const float epsilon = 0.001f;
        if (score > bestScore + epsilon) return true;
        if (Mathf.Abs(score - bestScore) > epsilon) return false;
        if (sectorTie > bestSectorTie + epsilon) return true;
        if (Mathf.Abs(sectorTie - bestSectorTie) > epsilon) return false;
        return hqTie > bestHqTie + epsilon;
    }

    private static float CalculateEnemyHqDistance(Vector3Int cell, AIWorldSnapshot snapshot, UnitManager unit)
    {
        if (snapshot == null || snapshot.EnemyHQ == null)
            return float.MaxValue;

        Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition;
        hq.z = 0;
        cell.z = 0;

        if (unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && SectorManager.TryGetLandMovementDistance(cell, hq, unitData, out int movementCost))
        {
            return movementCost;
        }

        if (SectorManager.TryGetLandMovementDistance(cell, hq, out int fallbackCost))
            return fallbackCost;

        return SectorManager.HexDistance(cell, hq);
    }

    private static bool TryCalculateRouteDistance(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell, out float distance)
    {
        distance = 0f;
        fromCell.z = 0;
        targetCell.z = 0;

        if (unit != null && unit.GetDomain() == Domain.Air)
        {
            distance = SectorManager.HexDistance(fromCell, targetCell);
            return true;
        }

        if (unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && SectorManager.TryGetLandMovementDistance(fromCell, targetCell, unitData, out int unitCost))
        {
            distance = unitCost;
            return true;
        }

        if (SectorManager.TryGetLandMovementDistance(fromCell, targetCell, out int fallbackCost))
        {
            distance = fallbackCost;
            return true;
        }

        return false;
    }

    private static float CalculateRouteDistanceOrHex(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell)
    {
        return TryCalculateRouteDistance(unit, fromCell, targetCell, out float routeDistance)
            ? routeDistance
            : SectorManager.HexDistance(fromCell, targetCell);
    }

    private static float CalculateEnemyHqTieBreak(float hqDistance)
    {
        return hqDistance < float.MaxValue ? -hqDistance : 0f;
    }

    private static bool IsBetterRogueAdvance(Vector3Int from, Vector3Int target, Vector3Int candidate, float candidateHexDist, Vector3Int currentBest, float bestHexDist)
    {
        const float epsilon = 0.001f;
        if (candidateHexDist < bestHexDist - epsilon)
            return true;
        if (candidateHexDist > bestHexDist + epsilon)
            return false;

        float candidateLine = CalculateLineProgressTieBreak(from, target, candidate);
        float bestLine = CalculateLineProgressTieBreak(from, target, currentBest);
        if (candidateLine > bestLine + epsilon)
            return true;
        if (candidateLine < bestLine - epsilon)
            return false;

        return Vector3Int.Distance(candidate, target) < Vector3Int.Distance(currentBest, target) - epsilon;
    }

    private static float CalculateLineProgressTieBreak(Vector3Int from, Vector3Int target, Vector3Int candidate)
    {
        Vector2 origin = new Vector2(from.x, from.y);
        Vector2 goal = new Vector2(target.x, target.y);
        Vector2 point = new Vector2(candidate.x, candidate.y);
        Vector2 direction = goal - origin;
        float lengthSq = direction.sqrMagnitude;
        if (lengthSq <= 0.001f)
            return 0f;

        Vector2 advanced = point - origin;
        float projection = Vector2.Dot(advanced, direction.normalized);
        float lateral = Mathf.Abs(direction.x * advanced.y - direction.y * advanced.x) / Mathf.Sqrt(lengthSq);
        return projection - lateral * 0.25f;
    }
}
