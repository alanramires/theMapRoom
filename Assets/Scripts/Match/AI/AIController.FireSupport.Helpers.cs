using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private static bool IsFireSupportUnit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return data.roles != null && data.roles.Contains(UnitRole.FogoIndireto);
    }

    private static bool IsLongRangeStationary(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.longRangeStationary;
    }

    private static bool PreferFireSupportWeaponMaxRange(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && (data.preferRepositionAtWeaponMaxRange || data.preferArtilleryModeBeforeCombatant);
    }

    private static bool IsArtilleryModeOnly(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.preferArtilleryModeBeforeCombatant;
    }

    // Returns the smallest minRange among all indirect weapons (minRange >= 2) the unit has with ammo.
    // Returns -1 if the unit has no indirect weapon (filter should not apply).
    private static int GetUnitIndirectWeaponMinRange(UnitManager unit)
    {
        if (unit == null) return -1;
        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null) return -1;
        int best = -1;
        foreach (UnitEmbarkedWeapon embarked in weapons)
        {
            if (embarked?.weapon == null || embarked.squadAmmunition <= 0) continue;
            int minR = embarked.GetRangeMin();
            if (minR < 2) continue;
            if (best < 0 || minR < best) best = minR;
        }
        return best;
    }

    private static bool IsFireSupportConservative(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.playConservative;
    }

    private static bool PreferFireSupportBestDpq(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.preferMoveOnBestDPQ;
    }

    private static bool IsFireSupportCloseEnoughToHold(UnitManager unit, Vector3Int cell, Vector3Int anchor)
    {
        int maxRange = GetFireSupportMaxWeaponRange(unit);
        if (maxRange <= 0)
            return true;

        return SectorManager.HexDistance(cell, anchor) <= maxRange + 1f;
    }

    private static int GetFireSupportMaxWeaponRange(UnitManager unit)
    {
        if (unit == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null)
            return 0;

        int best = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null || embarked.squadAmmunition <= 0) continue;
            best = Mathf.Max(best, embarked.GetRangeMax());
        }

        return best;
    }

    private Dictionary<Vector3Int, List<Vector3Int>> BuildFireSupportPaths(UnitManager unit)
    {
        return UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap,
            unit,
            Mathf.Max(0, unit.RemainingMovementPoints),
            terrainDatabase);
    }

    private static SectorObjective ResolveAssignedFireSupportObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        if (unit == null || plan == null) return null;
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.FogoIndireto && slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                    return obj;
        return null;
    }

    private Vector3Int ResolveFireSupportObjectiveAnchor(SectorObjective assigned, TeamId aiTeam, Vector3Int fallback)
    {
        if (assigned == null) return fallback;

        ConstructionManager target = FindCapturableInSector(assigned.Sector, aiTeam, fallback);
        if (target != null)
        {
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            return targetCell;
        }

        if (TryGetAnySectorInfo(assigned.Sector, out SectorManager.SectorInfo info))
        {
            Vector3Int cell = info.RepresentativeCell;
            cell.z = 0;
            return cell;
        }

        return fallback;
    }

    private Vector3Int ResolveRogueFireSupportAnchor(AIWorldSnapshot snapshot, Vector3Int fallback)
    {
        // Rogue fire support always marches toward the enemy HQ when no attack is available.
        if (snapshot != null && snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition;
            hq.z = 0;
            return hq;
        }

        // Fallback: nearest visible enemy unit.
        if (snapshot != null && snapshot.EnemyUnits != null && snapshot.EnemyUnits.Count > 0)
        {
            UnitManager best = null;
            float bestDist = float.MaxValue;
            foreach (UnitManager enemy in snapshot.EnemyUnits)
            {
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
                Vector3Int ec = enemy.CurrentCellPosition;
                ec.z = 0;
                float dist = SectorManager.HexDistance(fallback, ec);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = enemy;
                }
            }

            if (best != null)
            {
                Vector3Int cell = best.CurrentCellPosition;
                cell.z = 0;
                return cell;
            }
        }

        return fallback;
    }

    private bool TryBuildBestFireSupportAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int anchor,
        bool defensiveContext,
        out PlayerAction action,
        out string reason,
        bool indirectOnly = false)
    {
        action = null;
        reason = "";

        if (unit == null || snapshot == null)
            return false;

        bool stationary = IsLongRangeStationary(unit);
        Vector3Int bestCell = fromCell;
        UnitManager bestTarget = null;
        float bestScore = float.MinValue;
        string bestDecision = "";

        // Artillery-mode filter: only allow attacks at the unit's max weapon range.
        // preferArtilleryModeBeforeCombatant means "fire from distance, not close combat."
        int artilleryMaxRange = indirectOnly ? GetFireSupportMaxWeaponRange(unit) : 0;

        foreach (Vector3Int rawCell in EnumerateFireSupportCandidateCells(fromCell, paths, stationary))
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell)) continue;

            SensorMovementMode mode = cell != fromCell
                ? SensorMovementMode.MoveuAndando
                : SensorMovementMode.MoveuParado;

            var targets = new List<PodeMirarTargetOption>();
            WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
            if (!PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase, mode, targets, weaponPriorityData: weaponPriorityData, fromCell: cell))
                continue;

            foreach (PodeMirarTargetOption opt in targets)
            {
                if (opt?.targetUnit == null || opt.targetUnit.TeamId == snapshot.AITeam || opt.targetUnit.IsDead) continue;
                if (artilleryMaxRange > 0 && opt.distance < artilleryMaxRange) continue;
                if (!PassesAttackDecision(unit, opt.targetUnit, cell, defensiveContext, out string attackDecisionReason))
                    continue;

                Vector3Int targetCell = opt.targetUnit.CurrentCellPosition;
                targetCell.z = 0;
                float targetPriority = ScoreFireSupportTarget(unit, opt, cell, targetCell, anchor, weaponPriorityData, out string combatScoreDetails);
                float rangeScore = PreferFireSupportWeaponMaxRange(unit) ? opt.distance * 30f : -opt.distance * 5f;
                float movePenalty = cell == fromCell ? 0f : GetPathStepCount(paths, cell) * 40f;
                float dpq = GetTerrainDpqPontos(cell) * 25f;
                float score = targetPriority + rangeScore + dpq - movePenalty - opt.targetUnit.InstanceId * 0.001f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = opt.targetUnit;
                    bestDecision = $"{attackDecisionReason} {combatScoreDetails}";
                }
            }
        }

        if (bestTarget == null)
            return false;

        Vector3Int bestTargetCell = bestTarget.CurrentCellPosition;
        bestTargetCell.z = 0;
        action = BuildAttackBatch(
            unit,
            snapshot.AITeam,
            fromCell,
            bestCell,
            bestTarget.InstanceId.ToString(),
            bestTargetCell,
            paths);
        reason = $"ataca via {bestCell} -> {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} score={bestScore:F0} {bestDecision}";
        return true;
    }

    private static IEnumerable<Vector3Int> EnumerateFireSupportCandidateCells(
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        bool stationary)
    {
        yield return fromCell;

        if (stationary || paths == null)
            yield break;

        foreach (Vector3Int cell in paths.Keys)
            if (cell != fromCell)
                yield return cell;
    }

    private float ScoreFireSupportTarget(
        UnitManager attacker,
        PodeMirarTargetOption option,
        Vector3Int attackCell,
        Vector3Int targetCell,
        Vector3Int anchor,
        WeaponPriorityData weaponPriorityData,
        out string details)
    {
        details = "";
        UnitManager target = option != null ? option.targetUnit : null;
        if (target == null)
            return 0f;

        float score = 10000f;
        score -= SectorManager.HexDistance(targetCell, anchor) * 500f;
        score += Mathf.Max(0, 20 - target.CurrentHP) * 120f;
        BazookaTargetPriority targetPreference = ResolveFireSupportTargetPreference(attacker, target);
        score += GetFireSupportTargetPreferenceScore(targetPreference);
        if (option.isPreferredTargetForWeapon)
            score += 6500f;
        score += GetFireSupportRangeFitScore(attacker, target, option.distance, option.weapon, weaponPriorityData);

        // Economic value: expensive/elite targets are strategically more valuable to destroy.
        float targetValueScore = 0f;
        if (target.TryGetUnitData(out UnitData targetUnitData) && targetUnitData != null)
        {
            targetValueScore = targetUnitData.cost * 1.5f + targetUnitData.eliteLevel * 5000f;
            score += targetValueScore;
        }

        string simDetails = "";
        if (TrySimulateAttackForAI(attacker, target, attackCell, out AIAttackSimulationSummary sim))
        {
            float damageScore = sim.targetDamage * 3000f;
            float damagePctScore = sim.targetDamagePct * 80f;
            float killScore = sim.result.killGuaranteed ? 12000f : 0f;
            float survivalPenalty = sim.result.attackerSurvives ? 0f : 4000f;
            score += damageScore + damagePctScore + killScore - survivalPenalty;
            simDetails = $" simDmg={sim.targetDamage} dmgPct={sim.targetDamagePct}% kill={sim.result.killGuaranteed} simScore={(damageScore + damagePctScore + killScore - survivalPenalty):F0}";
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetCell);
        float constructionThreatScore = ScoreFireSupportConstructionThreat(target, construction, attacker != null ? attacker.TeamId : TeamId.Neutral);
        score += constructionThreatScore;

        details = $"pref={targetPreference} value={targetValueScore:F0}{simDetails} bldgThreat={constructionThreatScore:F0}";
        return score;
    }

    private float ScoreFireSupportConstructionThreat(UnitManager target, ConstructionManager construction, TeamId aiTeam)
    {
        if (target == null || construction == null || !construction.IsCapturable)
            return 0f;

        bool ownedOrContested = construction.TeamId == aiTeam
            || (construction.TeamId == aiTeam && construction.CurrentCapturePoints < construction.CapturePointsMax);
        bool enemyHeld = construction.TeamId != aiTeam;
        float score = ownedOrContested ? 26000f : enemyHeld ? 12000f : 0f;

        if (target.TryGetUnitData(out UnitData targetData)
            && targetData != null
            && targetData.roles != null
            && targetData.roles.Contains(UnitRole.Capturador))
        {
            score += 9000f;
        }

        score += Mathf.Max(0, target.CurrentHP) * 350f;
        return score;
    }

    private float CalculateFireSupportTacticalPressureScore(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        WeaponPriorityData weaponPriorityData)
    {
        if (unit == null || snapshot == null || snapshot.EnemyUnits == null)
            return 0f;

        float best = 0f;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData enemyData) || enemyData == null) continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            int distance = Mathf.Max(1, Mathf.RoundToInt(SectorManager.HexDistance(cell, enemyCell)));
            float targetScore = GetFireSupportTargetPreferenceScore(ResolveFireSupportTargetPreference(unit, enemy));
            float weaponFit = GetFireSupportRangeFitScore(unit, enemy, distance, null, weaponPriorityData);
            if (weaponFit <= 0f)
                continue;

            float hpScore = Mathf.Max(0, 20 - enemy.CurrentHP) * 45f;
            float score = targetScore + weaponFit + hpScore - enemy.InstanceId * 0.001f;
            if (score > best)
                best = score;
        }

        return best;
    }

    private static BazookaTargetPriority ResolveFireSupportTargetPreference(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return BazookaTargetPriority.Tertiary;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return BazookaTargetPriority.Tertiary;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return BazookaTargetPriority.Tertiary;

        return attackerData.ResolveAiTargetPriorityForTargetClass(targetData.unitClass);
    }

    private static float GetFireSupportTargetPreferenceScore(BazookaTargetPriority priority)
    {
        switch (priority)
        {
            case BazookaTargetPriority.Primary:
                return 18000f;
            case BazookaTargetPriority.Secondary:
                return 8500f;
            default:
                return 0f;
        }
    }

    private static float GetFireSupportRangeFitScore(
        UnitManager attacker,
        UnitManager target,
        int distance,
        WeaponData actualWeapon,
        WeaponPriorityData weaponPriorityData)
    {
        if (attacker == null || target == null)
            return 0f;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return 0f;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = attacker.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count == 0)
            return 0f;

        BazookaTargetPriority targetPreference = ResolveFireSupportTargetPreference(attacker, target);
        bool preferredByUnitData = targetPreference == BazookaTargetPriority.Primary
            || targetPreference == BazookaTargetPriority.Secondary;
        float best = 0f;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null) continue;
            if (actualWeapon != null && embarked.weapon != actualWeapon) continue;
            if (embarked.squadAmmunition <= 0) continue;
            if (!embarked.weapon.SupportsOperationOn(target.GetDomain(), target.GetHeightLevel())) continue;

            int minRange = embarked.GetRangeMin();
            int maxRange = embarked.GetRangeMax();
            if (maxRange <= 0) continue;

            bool preferredWeapon = PodeMirarSensor.IsPreferredWeaponForTarget(weaponPriorityData, embarked.weapon, targetData.unitClass);
            int idealRange = Mathf.Clamp(2, minRange, maxRange);
            bool inRange = distance >= minRange && distance <= maxRange;
            if (actualWeapon == null && !inRange)
                continue;

            float rangeError = distance < minRange
                ? minRange - distance
                : distance > maxRange ? distance - maxRange
                : preferredByUnitData ? 0f : Mathf.Abs(distance - idealRange);
            float inRangeBonus = inRange ? 3500f : 0f;
            float preferredBonus = preferredWeapon ? 6500f : 0f;
            float unitPreferenceInRangeBonus = preferredByUnitData && inRange ? 2500f : 0f;
            float score = preferredBonus + inRangeBonus + unitPreferenceInRangeBonus + Mathf.Max(0f, 2600f - rangeError * 900f);
            if (score > best)
                best = score;
        }

        return best;
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
        bool preferBestDpq = PreferFireSupportBestDpq(unit);
        int maxRange = GetFireSupportMaxWeaponRange(unit);
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
        float fromThreat = CalculateThreatLevel(fromCell, snapshot.AITeam);
        float fromScore = ScoreFireSupportRepositionCell(
            unit,
            snapshot,
            fromCell,
            fromCell,
            anchor,
            fromDist,
            0,
            preferMaxRange,
            conservative,
            preferBestDpq,
            maxRange,
            weaponPriorityData,
            out _);
        Vector3Int bestAdvanceCell = fromCell;
        bool bestAdvanceRouteFound = false;
        float bestAdvanceProgress = 0f;
        float bestAdvanceHexProgress = 0f;
        float bestAdvanceDpq = GetTerrainDpqPontos(fromCell);
        float bestAdvanceThreat = CalculateThreatLevel(fromCell, snapshot.AITeam);
        int bestAdvancePathCost = int.MaxValue;
        bool foundAdvance = false;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            if (conservative && threat > fromThreat + 0.1f)
                continue;

            float progress = fromDist - SectorManager.HexDistance(cell, anchor);
            float dpq = GetTerrainDpqPontos(cell);
            int pathCost = GetPathStepCount(paths, cell);
            float cellRouteDist = GetAnchorRouteCost(cell);
            bool cellRouteFound = cellRouteDist < float.MaxValue
                || TryCalculateFireSupportRouteDistance(unit, cell, anchor, out cellRouteDist);
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - cellRouteDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            bool advancesByRoute = recoversMissingRoute || routeProgress > 0f;
            if (requireImmediateThreat && CalculateFireSupportTacticalPressureScore(unit, snapshot, cell, weaponPriorityData) <= 0f)
                continue;

            float score = ScoreFireSupportRepositionCell(
                unit,
                snapshot,
                cell,
                fromCell,
                anchor,
                fromDist,
                pathCost,
                preferMaxRange,
                conservative,
                preferBestDpq,
                maxRange,
                weaponPriorityData,
                out _);

            if (!preferMaxRange && progress < 0f)
                score -= 500f;
            if (preferBestDpq && dpq <= GetTerrainDpqPontos(fromCell) && pathCost <= 1)
                score -= 250f;

            if (!requireImmediateThreat && (progress > 0f || advancesByRoute))
            {
                float fallbackProgress = advancesByRoute
                    ? recoversMissingRoute ? -cellRouteDist : routeProgress
                    : progress;
                if (!foundAdvance || IsBetterFireSupportAdvanceFallback(
                    advancesByRoute,
                    fallbackProgress,
                    progress,
                    dpq,
                    threat,
                    pathCost,
                    bestAdvanceRouteFound,
                    bestAdvanceProgress,
                    bestAdvanceHexProgress,
                    bestAdvanceDpq,
                    bestAdvanceThreat,
                    bestAdvancePathCost))
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

        float moveMargin = moveMarginOverride >= 0f ? moveMarginOverride : 120f;
        bool enemyNearAnchor = HasNearbyVisibleEnemy(anchor, snapshot.AITeam, defenseEnemyRange + maxRange);
        bool shouldAdvanceToAssigned = fromEffectiveDist > Mathf.Max(1, maxRange + 1);
        if (!requireImmediateThreat
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
            if (foundAdvance && (shouldAdvanceToAssigned || enemyNearAnchor))
            {
                bestCell = bestAdvanceCell;
                reason = $"advanceRoute route={bestAdvanceRouteFound} prog={bestAdvanceProgress:F1} hexProg={bestAdvanceHexProgress:F1} fromRoute={(fromRouteFound ? fromRouteDist.ToString("F1") : "?")} maxRange={maxRange} dpq={bestAdvanceDpq:F1} threat={bestAdvanceThreat:F1} path={bestAdvancePathCost}";
                return true;
            }

            return false;
        }

        if (bestScore < fromScore + moveMargin)
        {
            if (foundAdvance && (shouldAdvanceToAssigned || enemyNearAnchor))
            {
                bestCell = bestAdvanceCell;
                reason = $"advanceRoute score={bestScore:F0} hold={fromScore:F0} route={bestAdvanceRouteFound} prog={bestAdvanceProgress:F1} hexProg={bestAdvanceHexProgress:F1} fromRoute={(fromRouteFound ? fromRouteDist.ToString("F1") : "?")} maxRange={maxRange} dpq={bestAdvanceDpq:F1} threat={bestAdvanceThreat:F1} path={bestAdvancePathCost}";
                return true;
            }

            return false;
        }

        ScoreFireSupportRepositionCell(
            unit,
            snapshot,
            bestCell,
            fromCell,
            anchor,
            fromDist,
            GetPathStepCount(paths, bestCell),
            preferMaxRange,
            conservative,
            preferBestDpq,
            maxRange,
            weaponPriorityData,
            out string scoreDetails);
        reason = $"score={bestScore:F0} hold={fromScore:F0} {scoreDetails}";
        return true;
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

    private float ScoreFireSupportRepositionCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int fromCell,
        Vector3Int anchor,
        float fromDist,
        int pathCost,
        bool preferMaxRange,
        bool conservative,
        bool preferBestDpq,
        int maxRange,
        WeaponPriorityData weaponPriorityData,
        out string details)
    {
        float dist = SectorManager.HexDistance(cell, anchor);
        float progress = fromDist - dist;
        float dpq = GetTerrainDpqPontos(cell);
        float threat = CalculateThreatLevel(cell, snapshot.AITeam);
        float cohesion = conservative ? CalculateFireSupportCohesionScore(unit, snapshot, cell) : 0f;
        float rearLine = conservative ? CalculateFireSupportRearLineScore(unit, snapshot, cell, anchor) : 0f;
        float tacticalPressure = CalculateFireSupportTacticalPressureScore(unit, snapshot, cell, weaponPriorityData);
        if (conservative)
            tacticalPressure = Mathf.Min(tacticalPressure, 3200f);

        float dpqWeight = preferBestDpq ? 95f : 35f;
        float threatWeight = conservative ? 145f : 15f;
        float movementWeight = preferBestDpq ? 18f : 4f;
        float postureScore;

        if (preferMaxRange && maxRange > 0)
        {
            float idealDist = maxRange;
            float rangeError = Mathf.Abs(dist - idealDist);
            postureScore = Mathf.Max(0f, 360f - rangeError * (conservative ? 115f : 90f));

            float overSupportRange = dist - (maxRange + 1f);
            if (overSupportRange > 0f)
                postureScore -= overSupportRange * (conservative ? 260f : 180f);
        }
        else
        {
            postureScore = preferMaxRange
                ? dist * (conservative ? 35f : 50f)
                : progress * (conservative ? 70f : 120f);
        }

        float score = postureScore
            + tacticalPressure
            + dpq * dpqWeight
            + cohesion
            + rearLine
            - threat * threatWeight
            - pathCost * movementWeight;

        if (cell != fromCell && tacticalPressure <= 0f)
            score -= conservative ? 80f : 25f;
        if (conservative && threat > 0f && cell != fromCell)
            score -= threat * 90f;

        details = $"dist={dist:F1} range={maxRange} dpq={dpq:F1} prog={progress:F1} coh={cohesion:F0} rear={rearLine:F0} threat={threat:F1} pressure={tacticalPressure:F0}";
        return score;
    }

    private float CalculateFireSupportCohesionScore(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0f;

        float bestDist = float.MaxValue;
        float sumDist = 0f;
        int count = 0;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked) continue;
            if (IsFireSupportUnit(ally)) continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(cell, allyCell);
            bestDist = Mathf.Min(bestDist, dist);
            sumDist += dist;
            count++;
        }

        if (count == 0)
            return 0f;

        float averageDist = sumDist / count;
        float nearestScore = -Mathf.Abs(bestDist - 2f) * 90f;
        float groupScore = -Mathf.Abs(averageDist - 3.5f) * 35f;
        return nearestScore + groupScore;
    }

    private float CalculateFireSupportRearLineScore(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell, Vector3Int anchor)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0f;

        float allyDistSum = 0f;
        int allyCount = 0;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked) continue;
            if (IsFireSupportUnit(ally)) continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            allyDistSum += SectorManager.HexDistance(allyCell, anchor);
            allyCount++;
        }

        if (allyCount == 0)
            return 0f;

        float allyAverageDist = allyDistSum / allyCount;
        float cellDist = SectorManager.HexDistance(cell, anchor);
        float desiredRearGap = 2f;
        float gap = cellDist - allyAverageDist;

        if (gap >= desiredRearGap)
            return 300f - Mathf.Min(200f, (gap - desiredRearGap) * 35f);

        return -Mathf.Abs(desiredRearGap - gap) * 180f;
    }
}
