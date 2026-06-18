using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fire Support Rogue - sem slot de plano, pressiona alvo estrategico visivel.
    // -------------------------------------------------------------------------

    private PlayerAction DecideRogueFireSupportAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int anchor = ResolveRogueFireSupportAnchor(snapshot, fromCell);
        bool artilleryOnly = IsArtilleryModeOnly(unit);

        // Artillery mode: prefer max-range fire, then close-range (combatant), then reposition.
        // "preferArtilleryModeBeforeCombatant" means the order of preference, not exclusivity.
        // Normal mode: attack immediately if any target is available.
        if (artilleryOnly)
        {
            if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                    defensiveContext: false, out PlayerAction indirectAction, out string indirectReason, indirectOnly: true))
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue - {indirectReason}");
                return indirectAction;
            }
            // No max-range target — try close-range (combatant) before repositioning.
            if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                    defensiveContext: false, out PlayerAction combatAction, out string combatReason))
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue (combatente) - {combatReason}");
                return combatAction;
            }
        }
        else
        {
            if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                    defensiveContext: false, out PlayerAction attackAction, out string attackReason))
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue - {attackReason}");
                return attackAction;
            }
        }

        // Stationary fire support: hold position when the active front is within firing range.
        // Prevents artillery bought from lateral factories from drifting toward HQ when idle.
        if (IsLongRangeStationary(unit))
        {
            int maxRange = GetFireSupportMaxWeaponRange(unit);
            int holdRange = maxRange + 2;
            bool nearFront = false;

            List<UnitManager> visibleEnemies = snapshot != null ? CollectVisibleAssaultEnemies(snapshot.AITeam) : null;
            if (visibleEnemies != null)
                foreach (UnitManager enemy in visibleEnemies)
                {
                    if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
                    Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                    if (SectorManager.HexDistance(fromCell, ec) <= holdRange) { nearFront = true; break; }
                }

            if (!nearFront && snapshot?.EnemyBuildings != null)
                foreach (ConstructionManager bldg in snapshot.EnemyBuildings)
                {
                    if (bldg == null) continue;
                    Vector3Int bc = bldg.CurrentCellPosition; bc.z = 0;
                    if (SectorManager.HexDistance(fromCell, bc) <= holdRange) { nearFront = true; break; }
                }

            if (nearFront)
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue estacionario @ {fromCell} — frente a ≤{holdRange}h, segura");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
            }
        }

        PlayerAction rogueRendezvous = TryRogueFireSupportRendezvousAction(unit, snapshot, fromCell, paths, occupied);
        if (rogueRendezvous != null)
            return rogueRendezvous;

        if (TryRogueFireSupportKnownTargetRangeStep(unit, snapshot, fromCell, paths, occupied,
                null,
                fromCell,
                out Vector3Int rangeStepCell, out string rangeStepReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue aproxima alvo conhecido via {rangeStepCell} ({rangeStepReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, rangeStepCell, paths);
        }

        if (IsFireSupportConservative(unit))
        {
            Vector3Int conservativeCell = FindConservativeRogueFireSupportCell(unit, snapshot, fromCell, paths, occupied);
            if (conservativeCell != fromCell)
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue conservador reagrupa via {conservativeCell}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, conservativeCell, paths);
            }

            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue conservador segura @ {fromCell} - sem alvo");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (IsLongRangeStationary(unit) && IsFireSupportCloseEnoughToHold(unit, fromCell, anchor))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue estacionario @ {fromCell} - sem alvo");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        // Artillery mode: use zero margin so any improvement toward ideal range triggers a move.
        // This handles the case where the standard margin (120) blocks small-but-necessary adjustments
        // (e.g. backing up from 1h to 2h when maxRange=2, improvement ~86pts < 120).
        float repoMargin = artilleryOnly ? 0f : -1f;
        if (TryFindFireSupportRepositionCell(unit, snapshot, fromCell, anchor, paths, occupied,
                out Vector3Int moveCell, out string moveReason, moveMarginOverride: repoMargin))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue reposiciona via {moveCell} alvo={anchor} ({moveReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        // Artillery mode truly stuck — allow direct fire as absolute last resort.
        if (artilleryOnly && TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                defensiveContext: false, out PlayerAction fallbackAction, out string fallbackReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue (direto fallback) - {fallbackReason}");
            return fallbackAction;
        }

        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private bool TryRogueFireSupportKnownTargetRangeStep(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        SectorObjective assigned,
        Vector3Int objectiveAnchor,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = null;

        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;
        if (!PreferFireSupportWeaponMaxRange(unit) && !IsFireSupportConservative(unit))
            return false;

        int maxRange = GetFireSupportMaxWeaponRange(unit);
        if (maxRange <= 0)
            return false;

        int movementPoints = Mathf.Max(0, unit.RemainingMovementPoints);
        Dictionary<Vector3Int, int> movementCostMap =
            UnitMovementPathRules.CalculateMovementCostMap(
                boardTilemap,
                unit,
                fromCell,
                movementPoints,
                terrainDatabase);
        int minRange = Mathf.Max(0, GetUnitIndirectWeaponMinRange(unit));
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        bool conservativeOffensiveObjective = IsFireSupportConservative(unit)
            && assigned != null
            && assigned.Status != ObjectiveStatus.Defending
            && assigned.Status != ObjectiveStatus.Complete
            && assigned.Status != ObjectiveStatus.Abandoned;
        float bestScore = float.MinValue;
        UnitManager bestTarget = null;
        float bestDist = 0f;
        List<UnitManager> visibleEnemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        int candidateCount = 0;
        int sensorTargetCount = 0;
        int geometricTargetCount = 0;
        int conservativeBlocked = 0;
        int screenBlocked = 0;
        int occupiedBlocked = 0;
        int capturerTargetBlocked = 0;
        int highThreatCandidates = 0;
        int rangeTooClose = 0;
        int rangeTooFar = 0;
        float closestRangeMiss = float.MaxValue;
        Vector3Int closestRangeCell = fromCell;
        Vector3Int closestEnemyCell = fromCell;
        UnitManager closestRangeEnemy = null;
        int closestRangeDistance = 0;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell))
            {
                occupiedBlocked++;
                continue;
            }
            if (IsCellACapturerTarget(cell, plan, snapshot.AITeam))
            {
                capturerTargetBlocked++;
                continue;
            }
            if (!IsFireSupportConservativeCellAllowed(unit, snapshot, cell))
            {
                conservativeBlocked++;
                continue;
            }
            if (conservativeOffensiveObjective && !HasAlliedScreenAheadOfFireSupportCell(unit, snapshot, cell, objectiveAnchor))
            {
                screenBlocked++;
                continue;
            }

            candidateCount++;

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            if (threat >= 8f)
                highThreatCandidates++;

            int pathCost = ResolveFireSupportMovementCost(cell, paths, movementCostMap, movementPoints);
            float dpq = GetTerrainDpqPontos(cell);
            float cohesion = IsFireSupportConservative(unit) ? CalculateFireSupportCohesionScore(unit, snapshot, cell) : 0f;

            var sensorTargets = new List<PodeMirarTargetOption>();
            if (PodeMirarSensor.CollectTargets(
                    unit,
                    boardTilemap,
                    terrainDatabase,
                    SensorMovementMode.MoveuAndando,
                    sensorTargets,
                    weaponPriorityData: weaponPriorityData,
                    dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
                    fromCell: cell))
            {
                foreach (PodeMirarTargetOption opt in sensorTargets)
                {
                    UnitManager enemy = opt?.targetUnit;
                    if (enemy == null || enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked)
                        continue;

                    sensorTargetCount++;
                    ScoreRogueFireSupportRangeStepCandidate(
                        unit,
                        enemy,
                        cell,
                        opt.distance,
                        opt.weapon,
                        weaponPriorityData,
                        maxRange,
                        dpq,
                        cohesion,
                        threat,
                        pathCost,
                        passAttackBonus: PassesAttackDecision(unit, enemy, cell, defensiveContext: false, out _),
                        ref bestScore,
                        ref bestCell,
                        ref bestTarget,
                        ref bestDist);
                }
            }

            if (visibleEnemies == null)
                continue;

            foreach (UnitManager enemy in visibleEnemies)
            {
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked)
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                int dist = Mathf.Max(1, Mathf.RoundToInt(SectorManager.HexDistance(cell, enemyCell)));
                if (dist < minRange || dist > maxRange)
                {
                    if (dist < minRange)
                        rangeTooClose++;
                    else
                        rangeTooFar++;

                    float miss = dist < minRange ? minRange - dist : dist - maxRange;
                    if (miss < closestRangeMiss)
                    {
                        closestRangeMiss = miss;
                        closestRangeCell = cell;
                        closestEnemyCell = enemyCell;
                        closestRangeEnemy = enemy;
                        closestRangeDistance = dist;
                    }
                    continue;
                }

                geometricTargetCount++;
                ScoreRogueFireSupportRangeStepCandidate(
                    unit,
                    enemy,
                    cell,
                    dist,
                    null,
                    weaponPriorityData,
                    maxRange,
                    dpq,
                    cohesion,
                    threat,
                    pathCost,
                    passAttackBonus: false,
                    ref bestScore,
                    ref bestCell,
                    ref bestTarget,
                    ref bestDist);
            }
        }

        if (bestCell == fromCell || bestTarget == null)
        {
            int visibleEnemyCount = visibleEnemies != null ? visibleEnemies.Count : 0;
            string closestText = closestRangeEnemy != null
                ? $" closest={closestRangeEnemy.UnitDisplayName}#{closestRangeEnemy.InstanceId}@{closestEnemyCell} via {closestRangeCell} dist={closestRangeDistance}"
                : " closest=-";
            string context = assigned != null ? assigned.Sector.ToString() : "rogue";
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} {context} range-step scan: sem célula cand={candidateCount} visibleEnemies={visibleEnemyCount} sensorTargets={sensorTargetCount} geomTargets={geometricTargetCount} tooClose={rangeTooClose} tooFar={rangeTooFar} blockedOcc={occupiedBlocked} blockedCapTarget={capturerTargetBlocked} blockedConservative={conservativeBlocked} blockedScreen={screenBlocked} highThreat={highThreatCandidates} range={minRange}-{maxRange}{closestText}");
            return false;
        }

        reason = $"alvo={bestTarget.UnitDisplayName}#{bestTarget.InstanceId} dist={bestDist:F0}/{maxRange} score={bestScore:F0}";
        return true;
    }

    private static int ResolveFireSupportMovementCost(
        Vector3Int cell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> movementCostMap,
        int movementPoints)
    {
        cell.z = 0;
        if (movementCostMap != null && movementCostMap.TryGetValue(cell, out int cost))
            return Mathf.Max(0, cost);

        if (paths != null && paths.ContainsKey(cell))
            return Mathf.Max(0, movementPoints);

        return 0;
    }

    private void ScoreRogueFireSupportRangeStepCandidate(
        UnitManager unit,
        UnitManager enemy,
        Vector3Int cell,
        float distance,
        WeaponData weapon,
        WeaponPriorityData weaponPriorityData,
        int maxRange,
        float dpq,
        float cohesion,
        float threat,
        int pathCost,
        bool passAttackBonus,
        ref float bestScore,
        ref Vector3Int bestCell,
        ref UnitManager bestTarget,
        ref float bestDist)
    {
        float targetPreference = GetFireSupportTargetPreferenceScore(ResolveFireSupportTargetPreference(unit, enemy));
        float rangeFit = GetFireSupportRangeFitScore(unit, enemy, Mathf.RoundToInt(distance), weapon, weaponPriorityData);
        if (rangeFit <= 0f)
            return;

        float maxRangeFit = PreferFireSupportWeaponMaxRange(unit)
            ? Mathf.Max(0f, 1200f - Mathf.Abs(distance - maxRange) * 300f)
            : 0f;

        float score = 10000f
            + targetPreference
            + rangeFit
            + maxRangeFit
            + (passAttackBonus ? 2500f : 0f)
            + dpq * 75f
            + cohesion * 0.25f
            - threat * 300f
            - pathCost * 45f
            - enemy.InstanceId * 0.001f;

        if (score > bestScore)
        {
            bestScore = score;
            bestCell = cell;
            bestTarget = enemy;
            bestDist = distance;
        }
    }

    private PlayerAction TryRogueFireSupportRendezvousAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return null;

        if (!TryResolveRogueFireSupportFrontAnchor(unit, snapshot, fromCell, out Vector3Int anchor, out string anchorReason))
            return null;

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        int maxRange = GetFireSupportMaxWeaponRange(unit);
        if (maxRange <= 0)
            return null;

        bool preferMaxRange = PreferFireSupportWeaponMaxRange(unit);
        bool rearLinePosture = preferMaxRange || IsFireSupportConservative(unit) || IsArtilleryModeOnly(unit);
        if (!rearLinePosture)
            return null;

        bool preferBestDpq = PreferFireSupportBestDpq(unit);
        float fromDist = SectorManager.HexDistance(fromCell, anchor);
        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        int minRange = Mathf.Max(0, GetUnitIndirectWeaponMinRange(unit));

        Vector3Int best = fromCell;
        float bestScore = float.MinValue;
        string bestDetails = null;
        int bestToolProgress = int.MinValue;
        float bestNextDistance = float.MaxValue;
        bool bestInRange = fromDist >= minRange && fromDist <= maxRange;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell; cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;
            if (IsCellACapturerTarget(cell, plan, snapshot.AITeam)) continue;
            if (!IsFireSupportConservativeCellAllowed(unit, snapshot, cell)) continue;

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);

            float dist = SectorManager.HexDistance(cell, anchor);
            bool hasScreenAhead = HasAlliedScreenAheadOfFireSupportCell(unit, snapshot, cell, anchor);

            if (!TryScoreToolRouteProgression(
                    unit,
                    fromCell,
                    anchor,
                    cell,
                    paths[cell],
                    occupied,
                    out int toolProgress,
                    out float nextDistance,
                    out int pathMoveCost))
            {
                continue;
            }

            bool roadBonus = UnitMovementPathRules.DidUseRoadFullMoveBonus(boardTilemap, unit, paths[cell], terrainDatabase);

            ScoreFireSupportRepositionCell(
                unit,
                snapshot,
                cell,
                fromCell,
                anchor,
                fromDist,
                pathMoveCost,
                preferMaxRange: true,
                conservative: rearLinePosture,
                preferBestDpq,
                maxRange,
                weaponPriorityData,
                out string details);

            bool inWeaponRange = dist >= minRange && dist <= maxRange;

            float score = toolProgress * 1000f;
            if (inWeaponRange)
                score += 25000f;
            else if (dist > maxRange)
                score -= (dist - maxRange) * 5000f;

            if (roadBonus)
                score += 500f;
            if (!hasScreenAhead)
                score -= inWeaponRange ? 250f : 1200f;

            score -= threat * 250f;
            score += GetTerrainDpqPontos(cell) * 20f;

            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
                bestDetails = $"{details} toolProgress={toolProgress} nextDist={nextDistance:F1} inRange={inWeaponRange} screen={hasScreenAhead} moveCost={pathMoveCost} roadBonus={roadBonus}";
                bestToolProgress = toolProgress;
                bestNextDistance = nextDistance;
                bestInRange = inWeaponRange;
            }
        }

        if (best == fromCell || (bestToolProgress <= 0 && !bestInRange))
            return null;

        Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue tool-progress rendezvous via {best} anchor={anchor} ({anchorReason}; toolProgress={bestToolProgress} nextDist={bestNextDistance:F1} inRange={bestInRange}; {bestDetails})");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, best, paths);
    }

    private int GetFireSupportPathMovementCost(UnitManager unit, IReadOnlyList<Vector3Int> path)
    {
        if (unit == null || path == null)
            return int.MaxValue;

        return UnitMovementPathRules.CalculateAutonomyCostForPath(
            boardTilemap,
            unit,
            path,
            terrainDatabase,
            applyOperationalAutonomyModifier: false);
    }

    private bool TryResolveRogueFireSupportFrontAnchor(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        out Vector3Int anchor,
        out string reason)
    {
        anchor = Vector3Int.zero;
        reason = null;
        if (unit == null || snapshot == null)
            return false;

        float bestScore = float.MinValue;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        if (plan != null && plan.Objectives != null)
        {
            foreach (SectorObjective obj in plan.Objectives)
            {
                if (obj == null || obj.Slots == null) continue;
                if (obj.Status == ObjectiveStatus.Defending || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                    continue;

                Vector3Int objAnchor = ResolveFireSupportObjectiveAnchor(obj, snapshot.AITeam, fromCell);
                objAnchor.z = 0;

                int screenCount = 0;
                float nearestScreenDist = float.MaxValue;
                foreach (SlotNeed slot in obj.Slots)
                {
                    if (slot == null || !slot.Filled || slot.AssignedUnitId < 0) continue;
                    UnitManager ally = FindActiveUnit(slot.AssignedUnitId, snapshot.AITeam);
                    if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked) continue;
                    if (IsBacklineSupportUnit(ally)) continue;

                    Vector3Int allyCell = ally.CurrentCellPosition; allyCell.z = 0;
                    screenCount++;
                    nearestScreenDist = Mathf.Min(nearestScreenDist, SectorManager.HexDistance(allyCell, objAnchor));
                }

                if (screenCount <= 0)
                    continue;

                float priorityBonus = Mathf.Max(0f, 12f - obj.Priority) * 55f;
                float typeBonus = obj.ObjectiveType == AIObjectiveType.RallyAssembly ? 180f :
                    obj.ObjectiveType == AIObjectiveType.InvasionAttack ? 120f : 0f;
                float score = screenCount * 520f
                    + priorityBonus
                    + typeBonus
                    - SectorManager.HexDistance(fromCell, objAnchor) * 10f
                    - nearestScreenDist * 30f;

                if (score > bestScore)
                {
                    bestScore = score;
                    anchor = objAnchor;
                    reason = $"plano {obj.Sector} {obj.ObjectiveType} screen={screenCount}";
                }
            }
        }

        if (bestScore > float.MinValue)
            return true;

        if (snapshot.MyUnits == null)
            return false;

        Vector3Int home = snapshot.MyHQ != null ? snapshot.MyHQ.CurrentCellPosition : fromCell;
        home.z = 0;
        float fromHomeDist = SectorManager.HexDistance(fromCell, home);

        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (IsBacklineSupportUnit(ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition; allyCell.z = 0;
            float allyHomeDist = SectorManager.HexDistance(allyCell, home);
            float advance = allyHomeDist - fromHomeDist;
            if (snapshot.MyHQ != null && advance < 1.5f)
                continue;

            float unitDist = SectorManager.HexDistance(fromCell, allyCell);
            if (unitDist <= 2f)
                continue;

            float score = advance * 120f - unitDist * 12f + Mathf.Max(0f, 8f - unitDist) * 15f;
            if (score > bestScore)
            {
                bestScore = score;
                anchor = allyCell;
                reason = $"aliado avancado #{ally.InstanceId}";
            }
        }

        return bestScore > float.MinValue;
    }

    private bool HasAlliedScreenAheadOfFireSupportCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int anchor)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return false;

        float cellToAnchor = SectorManager.HexDistance(cell, anchor);
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (IsBacklineSupportUnit(ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition; allyCell.z = 0;
            float allyToAnchor = SectorManager.HexDistance(allyCell, anchor);
            if (allyToAnchor + 0.5f >= cellToAnchor)
                continue;

            if (SectorManager.HexDistance(allyCell, cell) <= 2f)
                return true;
        }

        return false;
    }
}
