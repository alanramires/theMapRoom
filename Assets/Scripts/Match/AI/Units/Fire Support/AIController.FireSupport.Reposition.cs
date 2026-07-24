using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Reposicionamento de apoio de fogo: célula conservadora, max-range e
    // fallback de avanço.
    // -------------------------------------------------------------------------

    private Vector3Int FindConservativeRogueFireSupportCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return fromCell;

        Vector3Int home = snapshot.MyHQ != null ? snapshot.MyHQ.CurrentCellPosition : fromCell;
        home.z = 0;
        float fromHomeDist = SectorManager.HexDistance(fromCell, home);
        float fromScore = ScoreConservativeRogueFireSupportCell(unit, snapshot, fromCell, fromCell, home, 0);

        Vector3Int best = fromCell;
        float bestScore = fromScore;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;
            if (IsCellACapturerTarget(cell, plan, snapshot.AITeam)) continue;

            if (!IsFireSupportConservativeCellAllowed(unit, snapshot, cell))
                continue;

            float homeDist = SectorManager.HexDistance(cell, home);
            if (snapshot.MyHQ != null && homeDist > fromHomeDist + 0.1f)
                continue;

            float score = ScoreConservativeRogueFireSupportCell(
                unit,
                snapshot,
                cell,
                fromCell,
                home,
                GetPathStepCount(paths, cell));

            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        return bestScore >= fromScore + 45f ? best : fromCell;
    }

    private float ScoreConservativeRogueFireSupportCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int fromCell,
        Vector3Int home,
        int pathCost)
    {
        float homeDist = SectorManager.HexDistance(cell, home);
        float threat = CalculateThreatLevel(cell, snapshot.AITeam);
        float cohesion = CalculateFireSupportCohesionScore(unit, snapshot, cell);
        float dpq = GetTerrainDpqPontos(cell);
        float holdBias = cell == fromCell ? 35f : 0f;

        return cohesion
            + dpq * 45f
            - homeDist * 35f
            - threat * 220f
            - pathCost * 20f
            + holdBias;
    }

    private bool TryFindFireSupportRepositionCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int anchor,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason,
        SectorObjective assigned = null,
        bool requireImmediateThreat = false,
        float moveMarginOverride = -1f)
    {
        bestCell = fromCell;
        reason = "";
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        float fromDist = SectorManager.HexDistance(fromCell, anchor);
        float bestScore = float.MinValue;
        bool found = false;
        bool preferMaxRange = PreferFireSupportWeaponMaxRange(unit);
        bool conservative = IsFireSupportConservative(unit);
        bool rallyBacklineRequired = assigned != null
            && assigned.ObjectiveType == AIObjectiveType.RallyAssembly
            && !IsCombatantFireSupport(unit);
        bool backlinePosture = conservative || rallyBacklineRequired;
        Vector3Int rearAnchor = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : anchor;
        rearAnchor.z = 0;
        bool preferBestDpq = PreferFireSupportBestDpq(unit);
        int maxRange = GetFireSupportMaxWeaponRange(unit);
        bool conservativeOffensiveObjective = backlinePosture
            && assigned != null
            && assigned.Status != ObjectiveStatus.Defending
            && assigned.Status != ObjectiveStatus.Complete
            && assigned.Status != ObjectiveStatus.Abandoned;
        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        Dictionary<Vector3Int, int> routeCostToAnchor =
            UnitMovementPathRules.CalculateMovementCostMap(boardTilemap, unit, anchor, 160, terrainDatabase);

        float GetAnchorRouteCost(Vector3Int c) =>
            routeCostToAnchor != null && routeCostToAnchor.TryGetValue(c, out int v)
                ? (float)v
                : float.MaxValue;

        float fromRouteDist = GetAnchorRouteCost(fromCell);
        bool fromRouteFound = fromRouteDist < float.MaxValue
            || TryCalculateFireSupportRouteDistance(unit, fromCell, anchor, out fromRouteDist);
        float fromEffectiveDist = fromRouteFound ? fromRouteDist : fromDist;
        float fromScore = ScoreFireSupportRepositionCell(
            unit, snapshot, fromCell, fromCell, anchor, fromDist, 0,
            preferMaxRange, backlinePosture, preferBestDpq, maxRange, weaponPriorityData, out _);
        Vector3Int bestAdvanceCell = fromCell;
        bool bestAdvanceRouteFound = false;
        float bestAdvanceProgress = 0f;
        float bestAdvanceHexProgress = 0f;
        float bestAdvanceDpq = GetTerrainDpqPontos(fromCell);
        float bestAdvanceThreat = CalculateThreatLevel(fromCell, snapshot.AITeam);
        int bestAdvancePathCost = int.MaxValue;
        bool foundAdvance = false;
        TeamObjectivePlan repositionCapPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        bool shouldAdvanceToAssignedEarly = fromEffectiveDist > Mathf.Max(1, maxRange + 1);
        float moveMargin = moveMarginOverride >= 0f ? moveMarginOverride : 120f;

        if (TryFindBestToolProgressionCell(
                unit,
                snapshot,
                fromCell,
                anchor,
                paths,
                occupied,
                ToolProgressionIntent.FireSupportReposition,
                out Vector3Int toolCell,
                out ToolProgressionCandidate toolCandidate,
                out string toolReason,
                allowCell: cell =>
                {
                    if (IsCellACapturerTarget(cell, repositionCapPlan, snapshot.AITeam))
                        return false;
                    if (backlinePosture && !IsFireSupportConservativeCellAllowed(unit, snapshot, cell))
                        return false;
                    if (conservativeOffensiveObjective && !HasAlliedScreenAheadOfFireSupportCell(unit, snapshot, cell, rearAnchor))
                        return false;
                    if (rallyBacklineRequired
                        && TryScoreBacklineCell(unit, snapshot, cell, rearAnchor, out AIBacklineScore rallyBackline)
                        && (!rallyBackline.InRearSlice || rallyBackline.IsVanguard))
                        return false;

                    float tacticalPressure = CalculateFireSupportTacticalPressureScore(unit, snapshot, cell, weaponPriorityData);
                    if (requireImmediateThreat && tacticalPressure <= 0f)
                        return false;

                    float progress = fromDist - SectorManager.HexDistance(cell, anchor);
                    float rearLine = backlinePosture ? CalculateFireSupportRearLineScore(unit, snapshot, cell, rearAnchor) : 0f;
                    if (backlinePosture && progress > 0f && tacticalPressure <= 0f && rearLine < -350f)
                        return false;

                    return true;
                },
                tacticalScore: (cell, candidate) =>
                {
                    int pathCost = GetPathStepCount(paths, cell);
                    float localScore = ScoreFireSupportRepositionCell(
                        unit,
                        snapshot,
                        cell,
                        fromCell,
                        anchor,
                        fromDist,
                        pathCost,
                        preferMaxRange,
                        backlinePosture,
                        preferBestDpq,
                        maxRange,
                        weaponPriorityData,
                        out _);

                    float progress = fromDist - SectorManager.HexDistance(cell, anchor);
                    float tacticalPressure = CalculateFireSupportTacticalPressureScore(unit, snapshot, cell, weaponPriorityData);
                    float rearLine = backlinePosture ? CalculateFireSupportRearLineScore(unit, snapshot, cell, rearAnchor) : 0f;
                    float dpq = GetTerrainDpqPontos(cell);

                    if (!preferMaxRange && progress < 0f)
                        localScore -= 500f;
                    if (preferBestDpq && dpq <= GetTerrainDpqPontos(fromCell) && pathCost <= 1)
                        localScore -= 250f;

                    return localScore
                        + tacticalPressure * 0.35f
                        + rearLine * 0.15f;
                }))
        {
            float toolHexProgress = fromDist - SectorManager.HexDistance(toolCell, anchor);
            bool offensiveAssignedReposition = assigned != null
                && assigned.Status != ObjectiveStatus.Defending
                && !requireImmediateThreat;
            bool toolHasDistanceProgress = toolHexProgress > 0.1f
                || toolCandidate.FirstTurnProgress > 0.1f
                || (!offensiveAssignedReposition && toolCandidate.TwoTurnProgress > 0.1f);
            bool toolHasProgress = toolHasDistanceProgress
                || (requireImmediateThreat && toolCandidate.ToolScore > 0);
            bool toolBeatsHold = toolCandidate.TacticalScore >= fromScore + moveMargin;
            bool toolPressureMove = requireImmediateThreat && toolCandidate.TacticalScore > fromScore;

            if (toolCell != fromCell
                && toolHasProgress
                && (shouldAdvanceToAssignedEarly || toolBeatsHold || toolPressureMove))
            {
                bestCell = toolCell;
                reason = $"toolProgress {toolReason} hold={fromScore:F0} maxRange={maxRange}";
                return true;
            }
        }

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;
            if (IsCellACapturerTarget(cell, repositionCapPlan, snapshot.AITeam)) continue;
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            if (backlinePosture && !IsFireSupportConservativeCellAllowed(unit, snapshot, cell))
                continue;
            if (conservativeOffensiveObjective && !HasAlliedScreenAheadOfFireSupportCell(unit, snapshot, cell, rearAnchor))
                continue;
            if (rallyBacklineRequired
                && TryScoreBacklineCell(unit, snapshot, cell, rearAnchor, out AIBacklineScore rallyBackline)
                && (!rallyBackline.InRearSlice || rallyBackline.IsVanguard))
                continue;

            float progress = fromDist - SectorManager.HexDistance(cell, anchor);
            float dpq = GetTerrainDpqPontos(cell);
            int pathCost = GetPathStepCount(paths, cell);
            float tacticalPressure = CalculateFireSupportTacticalPressureScore(unit, snapshot, cell, weaponPriorityData);
            float rearLine = backlinePosture ? CalculateFireSupportRearLineScore(unit, snapshot, cell, rearAnchor) : 0f;
            float cellRouteDist = GetAnchorRouteCost(cell);
            bool cellRouteFound = cellRouteDist < float.MaxValue
                || TryCalculateFireSupportRouteDistance(unit, cell, anchor, out cellRouteDist);
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - cellRouteDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            bool advancesByRoute = recoversMissingRoute || routeProgress > 0f;
            if (requireImmediateThreat && tacticalPressure <= 0f)
                continue;
            if (!requireImmediateThreat
                && assigned != null
                && assigned.Status != ObjectiveStatus.Defending
                && progress <= 0.1f
                && routeProgress <= 0.1f
                && !recoversMissingRoute)
                continue;
            if (backlinePosture && progress > 0f && tacticalPressure <= 0f && rearLine < -350f)
                continue;
            if (!requireImmediateThreat && !preferMaxRange && progress < 0f && !advancesByRoute)
                continue;

            float score = ScoreFireSupportRepositionCell(
                unit, snapshot, cell, fromCell, anchor, fromDist, pathCost,
                preferMaxRange, backlinePosture, preferBestDpq, maxRange, weaponPriorityData, out _);
            if (preferBestDpq && dpq <= GetTerrainDpqPontos(fromCell) && pathCost <= 1)
                score -= 250f;

            if (!requireImmediateThreat && (progress > 0f || advancesByRoute))
            {
                float fallbackProgress = advancesByRoute
                    ? recoversMissingRoute ? -cellRouteDist : routeProgress
                    : progress;
                if (!foundAdvance || IsBetterFireSupportAdvanceFallback(
                    advancesByRoute, fallbackProgress, progress, dpq, threat, pathCost,
                    bestAdvanceRouteFound, bestAdvanceProgress, bestAdvanceHexProgress,
                    bestAdvanceDpq, bestAdvanceThreat, bestAdvancePathCost))
                {
                    bestAdvanceCell = cell;
                    bestAdvanceRouteFound = advancesByRoute;
                    bestAdvanceProgress = fallbackProgress;
                    bestAdvanceHexProgress = progress;
                    bestAdvanceDpq = dpq;
                    bestAdvanceThreat = threat;
                    bestAdvancePathCost = pathCost;
                    foundAdvance = true;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                found = true;
            }
        }

        bool enemyNearAnchor = HasNearbyVisibleEnemy(anchor, snapshot.AITeam, defenseEnemyRange + maxRange);
        bool shouldAdvanceToAssigned = fromEffectiveDist > Mathf.Max(1, maxRange + 1);
        bool canUseAdvanceFallback = !backlinePosture;
        if (!requireImmediateThreat
            && canUseAdvanceFallback
            && shouldAdvanceToAssigned
            && foundAdvance
            && bestAdvanceCell != fromCell)
        {
            bestCell = bestAdvanceCell;
            reason = $"advanceRoute forced route={bestAdvanceRouteFound} prog={bestAdvanceProgress:F1} hexProg={bestAdvanceHexProgress:F1} fromRoute={(fromRouteFound ? fromRouteDist.ToString("F1") : "?")} maxRange={maxRange} dpq={bestAdvanceDpq:F1} threat={bestAdvanceThreat:F1} path={bestAdvancePathCost}";
            return true;
        }

        if (!found || bestCell == fromCell)
        {
            if (canUseAdvanceFallback && foundAdvance && (shouldAdvanceToAssigned || enemyNearAnchor))
            {
                bestCell = bestAdvanceCell;
                reason = $"advanceRoute route={bestAdvanceRouteFound} prog={bestAdvanceProgress:F1} hexProg={bestAdvanceHexProgress:F1} fromRoute={(fromRouteFound ? fromRouteDist.ToString("F1") : "?")} maxRange={maxRange} dpq={bestAdvanceDpq:F1} threat={bestAdvanceThreat:F1} path={bestAdvancePathCost}";
                return true;
            }

            return false;
        }

        if (bestScore < fromScore + moveMargin)
        {
            if (canUseAdvanceFallback && foundAdvance && (shouldAdvanceToAssigned || enemyNearAnchor))
            {
                bestCell = bestAdvanceCell;
                reason = $"advanceRoute score={bestScore:F0} hold={fromScore:F0} route={bestAdvanceRouteFound} prog={bestAdvanceProgress:F1} hexProg={bestAdvanceHexProgress:F1} fromRoute={(fromRouteFound ? fromRouteDist.ToString("F1") : "?")} maxRange={maxRange} dpq={bestAdvanceDpq:F1} threat={bestAdvanceThreat:F1} path={bestAdvancePathCost}";
                return true;
            }

            return false;
        }

        ScoreFireSupportRepositionCell(
            unit, snapshot, bestCell, fromCell, anchor, fromDist,
            GetPathStepCount(paths, bestCell), preferMaxRange, backlinePosture,
            preferBestDpq, maxRange, weaponPriorityData, out string scoreDetails);
        reason = $"score={bestScore:F0} hold={fromScore:F0} {scoreDetails}";
        return true;
    }

    private bool TryFindFireSupportMaxRangeThreatCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        if (!PreferFireSupportWeaponMaxRange(unit)
            || unit == null
            || snapshot == null
            || paths == null
            || paths.Count == 0)
            return false;

        int maxRange = GetFireSupportMaxWeaponRange(unit);
        if (maxRange <= 0)
            return false;

        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        bool conservative = IsFireSupportConservative(unit);
        float fromThreat = CalculateThreatLevel(fromCell, snapshot.AITeam);
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        float bestScore = float.MinValue;
        float bestThreat = 0f;
        float bestDpq = 0f;
        float bestPosture = 0f;
        int bestPath = 0;
        string bestPostureReason = "";
        int considered = 0;
        int occupiedRejected = 0;
        int capRejected = 0;
        int noPostureRejected = 0;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            considered++;
            if (occupied != null && occupied.Contains(cell))
            {
                occupiedRejected++;
                continue;
            }

            if (IsCellACapturerTarget(cell, plan, snapshot.AITeam))
            {
                capRejected++;
                continue;
            }

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float posture = ScoreFireSupportMaxRangeSensorPosture(
                unit, snapshot, cell, maxRange, weaponPriorityData, out string postureReason);
            if (posture <= 0f)
            {
                noPostureRejected++;
                continue;
            }

            int pathCost = GetPathStepCount(paths, cell);
            float dpq = GetTerrainDpqPontos(cell);
            float cohesion = conservative ? CalculateFireSupportCohesionScore(unit, snapshot, cell) : 0f;
            float score = posture
                + dpq * 85f
                + cohesion
                - threat * (conservative ? 220f : 70f)
                - pathCost * 18f;
            if (conservative && threat > fromThreat + 0.1f)
                score -= (threat - fromThreat) * 180f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestThreat = threat;
                bestDpq = dpq;
                bestPosture = posture;
                bestPath = pathCost;
                bestPostureReason = postureReason;
            }
        }

        if (bestCell == fromCell)
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} max-range sem candidato: paths={paths.Count} considered={considered} occ={occupiedRejected} cap={capRejected} noPosture={noPostureRejected} fromThreat={fromThreat:F1}");
            return false;
        }

        reason = $"posture={bestPosture:F0} {bestPostureReason} dpq={bestDpq:F1} threat={bestThreat:F1} path={bestPath}";
        return true;
    }

    private float ScoreFireSupportMaxRangeSensorPosture(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        int maxRange,
        WeaponPriorityData weaponPriorityData,
        out string reason)
    {
        reason = "";
        if (unit == null || snapshot == null || maxRange <= 0)
            return 0f;

        var targets = new List<PodeMirarTargetOption>();
        var invalids = new List<PodeMirarInvalidOption>();
        PodeMirarSensor.CollectTargets(
            unit,
            boardTilemap,
            terrainDatabase,
            SensorMovementMode.MoveuAndando,
            targets,
            invalids,
            weaponPriorityData: weaponPriorityData,
            dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
            fromCell: cell);

        float best = 0f;
        UnitManager bestTarget = null;
        string bestKind = "";
        int bestDistance = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            PodeMirarTargetOption opt = targets[i];
            UnitManager target = opt != null ? opt.targetUnit : null;
            if (target == null || target.SlotIndex == snapshot.AISlotIndex || target.IsDead || target.IsEmbarked)
                continue;

            float targetScore = GetFireSupportTargetPreferenceScore(ResolveFireSupportTargetPreference(unit, target));
            float rangeFit = GetFireSupportRangeFitScore(unit, target, opt.distance, opt.weapon, weaponPriorityData);
            if (rangeFit <= 0f)
                continue;

            float idealRange = Mathf.Max(0f, 4200f - Mathf.Abs(opt.distance - maxRange) * 900f);
            float decisionBonus = PassesAttackDecision(unit, target, cell, defensiveContext: false, out _)
                ? 2400f
                : 600f;
            float score = targetScore + rangeFit + idealRange + decisionBonus - target.InstanceId * 0.001f;
            if (score > best)
            {
                best = score;
                bestTarget = target;
                bestKind = "valid";
                bestDistance = opt.distance;
            }
        }

        for (int i = 0; i < invalids.Count; i++)
        {
            PodeMirarInvalidOption invalid = invalids[i];
            UnitManager target = invalid != null ? invalid.targetUnit : null;
            if (target == null || target.SlotIndex == snapshot.AISlotIndex || target.IsDead || target.IsEmbarked)
                continue;

            bool usefulInvalid =
                IsBlockedLineReason(invalid.reasonId)
                || invalid.reasonId == PodeMirarInvalidOption.ReasonIdNoForwardObserver
                || invalid.reasonId == PodeMirarInvalidOption.ReasonIdOutOfRange;
            if (!usefulInvalid)
                continue;

            float rangeFit = GetFireSupportRangeFitScore(unit, target, invalid.distance, invalid.weapon, weaponPriorityData);
            bool nearMaxRange = Mathf.Abs(invalid.distance - maxRange) <= 1;
            if (rangeFit <= 0f && !nearMaxRange && !IsBlockedLineReason(invalid.reasonId))
            {
                if (invalid.reasonId == PodeMirarInvalidOption.ReasonIdOutOfRange && invalid.distance > maxRange)
                {
                    float approachScore = Mathf.Max(1f, 800f - (invalid.distance - maxRange) * 80f);
                    if (approachScore > best)
                    {
                        best = approachScore;
                        bestTarget = target;
                        bestKind = "approach";
                        bestDistance = invalid.distance;
                    }
                }
                continue;
            }

            float targetScore = GetFireSupportTargetPreferenceScore(ResolveFireSupportTargetPreference(unit, target));
            float blockedBonus = IsBlockedLineReason(invalid.reasonId) ? 3200f : 0f;
            float observerBonus = invalid.reasonId == PodeMirarInvalidOption.ReasonIdNoForwardObserver ? 1600f : 0f;
            float rangePosture = Mathf.Max(0f, 3600f - Mathf.Abs(invalid.distance - maxRange) * 850f);
            float score = targetScore + rangeFit + blockedBonus + observerBonus + rangePosture - target.InstanceId * 0.001f;
            if (score > best)
            {
                best = score;
                bestTarget = target;
                bestKind = invalid.reasonId;
                bestDistance = invalid.distance;
            }
        }

        if (bestTarget != null)
            reason = $"target={bestTarget.UnitDisplayName}#{bestTarget.InstanceId} dist={bestDistance} mode={bestKind}";

        return best;
    }

    private static bool IsBetterFireSupportAdvanceFallback(
        bool candidateRouteFound,
        float candidateProgress,
        float candidateHexProgress,
        float candidateDpq,
        float candidateThreat,
        int candidatePathCost,
        bool currentRouteFound,
        float currentProgress,
        float currentHexProgress,
        float currentDpq,
        float currentThreat,
        int currentPathCost)
    {
        const float Epsilon = 0.001f;
        if (candidateRouteFound != currentRouteFound)
            return candidateRouteFound;

        if (Mathf.Abs(candidateProgress - currentProgress) > Epsilon)
            return candidateProgress > currentProgress;

        if (Mathf.Abs(candidateHexProgress - currentHexProgress) > Epsilon)
            return candidateHexProgress > currentHexProgress;

        if (Mathf.Abs(candidateDpq - currentDpq) > Epsilon)
            return candidateDpq > currentDpq;

        if (Mathf.Abs(candidateThreat - currentThreat) > Epsilon)
            return candidateThreat < currentThreat;

        return candidatePathCost < currentPathCost;
    }

    private static bool TryCalculateFireSupportRouteDistance(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell, out float distance)
    {
        distance = 0f;
        fromCell.z = 0;
        targetCell.z = 0;

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
}
