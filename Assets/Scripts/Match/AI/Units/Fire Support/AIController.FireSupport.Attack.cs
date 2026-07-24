using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Ataque de apoio de fogo: construção de ações de ataque e reposicionamento
    // por tiro bloqueado.
    // -------------------------------------------------------------------------

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
        bool indirectOnly = false,
        bool stationaryOnly = false,
        System.Func<PodeMirarTargetOption, bool> optionFilter = null)
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
        int candidateCells = 0;
        int enemyOptions = 0;
        int optionFiltered = 0;
        int rangeFiltered = 0;
        int attackDecisionBlocked = 0;
        string lastAttackDecisionBlock = "";

        int artilleryMaxRange = indirectOnly ? GetFireSupportMaxWeaponRange(unit) : 0;
        TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);

        foreach (Vector3Int rawCell in EnumerateFireSupportCandidateCells(fromCell, paths, stationary || stationaryOnly))
        {
            candidateCells++;
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell)) continue;
            if (cell != fromCell && IsCellACapturerTarget(cell, capPlan, snapshot.AITeam)) continue;

            SensorMovementMode mode = cell != fromCell
                ? SensorMovementMode.MoveuAndando
                : SensorMovementMode.MoveuParado;

            var targets = new List<PodeMirarTargetOption>();
            WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
            if (!PodeMirarSensor.CollectTargets(
                    unit,
                    boardTilemap,
                    terrainDatabase,
                    mode,
                    targets,
                    weaponPriorityData: weaponPriorityData,
                    dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
                    fromCell: cell))
                continue;

            foreach (PodeMirarTargetOption opt in targets)
            {
                if (opt?.targetUnit == null || opt.targetUnit.SlotIndex == snapshot.AISlotIndex || opt.targetUnit.IsDead) continue;
                enemyOptions++;
                if (optionFilter != null && !optionFilter(opt))
                {
                    optionFiltered++;
                    continue;
                }
                if (artilleryMaxRange > 0 && opt.distance < artilleryMaxRange)
                {
                    rangeFiltered++;
                    continue;
                }
                if (!PassesAttackDecision(unit, opt.targetUnit, cell, defensiveContext, out string attackDecisionReason))
                {
                    attackDecisionBlocked++;
                    lastAttackDecisionBlock = $"{opt.targetUnit.UnitDisplayName}#{opt.targetUnit.InstanceId} via {cell}: {attackDecisionReason}";
                    continue;
                }

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
        {
            reason = $"nenhum tiro: cells={candidateCells} opcoes={enemyOptions} filtro={optionFiltered}"
                + $" range={rangeFiltered} attackDecision={attackDecisionBlocked}"
                + (string.IsNullOrEmpty(lastAttackDecisionBlock)
                    ? ""
                    : $" ultimo=[{lastAttackDecisionBlock}]");
            return false;
        }

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

    private bool TryBuildFireSupportBlockedShotRepositionAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool defensiveContext,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;
        if (IsLongRangeStationary(unit))
            return false;

        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        if (!TryFindBlockedFireSupportTarget(unit, snapshot, fromCell, SensorMovementMode.MoveuParado, weaponPriorityData, out UnitManager blockedTarget, out PodeMirarInvalidOption blockedOption))
            return false;

        Vector3Int targetCell = blockedTarget.CurrentCellPosition;
        targetCell.z = 0;
        bool conservative = IsFireSupportConservative(unit);
        bool preferBestDpq = PreferFireSupportBestDpq(unit);
        int maxRange = GetFireSupportMaxWeaponRange(unit);
        float bestScore = float.MinValue;
        Vector3Int bestCell = fromCell;
        string bestDetails = "";
        TeamObjectivePlan blockedCapPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;
            if (IsCellACapturerTarget(cell, blockedCapPlan, snapshot.AITeam)) continue;

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            if (conservative && !IsFireSupportConservativeCellAllowed(unit, snapshot, cell))
                continue;

            SensorMovementMode mode = SensorMovementMode.MoveuAndando;
            var targets = new List<PodeMirarTargetOption>();
            var invalids = new List<PodeMirarInvalidOption>();
            PodeMirarSensor.CollectTargets(
                unit,
                boardTilemap,
                terrainDatabase,
                mode,
                targets,
                invalids,
                weaponPriorityData: weaponPriorityData,
                dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
                fromCell: cell);

            for (int i = 0; i < targets.Count; i++)
            {
                PodeMirarTargetOption targetOption = targets[i];
                if (targetOption == null || targetOption.targetUnit != blockedTarget)
                    continue;
                if (!PassesAttackDecision(unit, blockedTarget, cell, defensiveContext, out string attackDecisionReason))
                    continue;

                action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, cell, blockedTarget.InstanceId.ToString(), targetCell, paths);
                reason = $"abre tiro imediato via {cell} -> {blockedTarget.UnitDisplayName}#{blockedTarget.InstanceId} {attackDecisionReason}";
                return true;
            }

            PodeMirarInvalidOption sameTargetInvalid = FindInvalidForTarget(invalids, blockedTarget);
            if (sameTargetInvalid == null)
                continue;

            int distance = Mathf.Max(1, Mathf.RoundToInt(SectorManager.HexDistance(cell, targetCell)));
            float rangeFit = GetFireSupportRangeFitScore(unit, blockedTarget, distance, null, weaponPriorityData);
            if (rangeFit <= 0f && !IsBlockedLineReason(sameTargetInvalid.reasonId))
                continue;

            float targetPreference = GetFireSupportTargetPreferenceScore(ResolveFireSupportTargetPreference(unit, blockedTarget));
            float targetValue = 0f;
            if (blockedTarget.TryGetUnitData(out UnitData targetData) && targetData != null)
                targetValue = targetData.cost * 0.75f + targetData.eliteLevel * 2500f;

            float fromDistance = Mathf.Max(1, Mathf.RoundToInt(SectorManager.HexDistance(fromCell, targetCell)));
            float distanceProgress = fromDistance - distance;
            float dpq = GetTerrainDpqPontos(cell);
            int pathCost = GetPathStepCount(paths, cell);
            float cohesion = conservative ? CalculateFireSupportCohesionScore(unit, snapshot, cell) : 0f;
            float idealRangeScore = 0f;
            if (maxRange > 0)
                idealRangeScore = Mathf.Max(0f, 1800f - Mathf.Abs(distance - maxRange) * 450f);

            float score = 12000f
                + targetPreference
                + targetValue
                + rangeFit
                + idealRangeScore
                + distanceProgress * 450f
                + dpq * (preferBestDpq ? 120f : 65f)
                + cohesion
                - threat * (conservative ? 180f : 45f)
                - pathCost * (preferBestDpq ? 24f : 12f);

            if (IsBlockedLineReason(sameTargetInvalid.reasonId))
                score += 3500f;
            if (blockedTarget.GetDomain() == Domain.Air)
                score += 4000f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestDetails = $"alvo={blockedTarget.UnitDisplayName}#{blockedTarget.InstanceId} dist={distance} range={maxRange} reason={sameTargetInvalid.reasonId} block={sameTargetInvalid.blockedCell} score={score:F0}";
            }
        }

        if (bestCell == fromCell)
            return false;

        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestCell, paths);
        reason = $"aproxima alvo bloqueado via {bestCell} ({bestDetails}; origem={blockedOption.reasonId} block={blockedOption.blockedCell})";
        return true;
    }

    private bool TryFindBlockedFireSupportTarget(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        SensorMovementMode mode,
        WeaponPriorityData weaponPriorityData,
        out UnitManager target,
        out PodeMirarInvalidOption option)
    {
        target = null;
        option = null;
        var validTargets = new List<PodeMirarTargetOption>();
        var invalidTargets = new List<PodeMirarInvalidOption>();
        PodeMirarSensor.CollectTargets(
            unit,
            boardTilemap,
            terrainDatabase,
            mode,
            validTargets,
            invalidTargets,
            weaponPriorityData: weaponPriorityData,
            dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
            fromCell: fromCell);

        float bestScore = float.MinValue;
        for (int i = 0; i < invalidTargets.Count; i++)
        {
            PodeMirarInvalidOption invalid = invalidTargets[i];
            UnitManager candidate = invalid != null ? invalid.targetUnit : null;
            if (candidate == null || candidate.SlotIndex == snapshot.AISlotIndex || candidate.IsDead || candidate.IsEmbarked)
                continue;
            if (!IsBlockedLineReason(invalid.reasonId))
                continue;

            float score = GetFireSupportTargetPreferenceScore(ResolveFireSupportTargetPreference(unit, candidate));
            if (candidate.GetDomain() == Domain.Air)
                score += 9000f;
            if (candidate.TryGetUnitData(out UnitData data) && data != null)
                score += data.cost * 0.5f + data.eliteLevel * 2000f;
            score += Mathf.Max(0, 20 - candidate.CurrentHP) * 80f;
            score -= invalid.distance * 50f;
            score -= candidate.InstanceId * 0.001f;

            if (score > bestScore)
            {
                bestScore = score;
                target = candidate;
                option = invalid;
            }
        }

        return target != null;
    }

    private static PodeMirarInvalidOption FindInvalidForTarget(List<PodeMirarInvalidOption> invalids, UnitManager target)
    {
        if (invalids == null || target == null)
            return null;

        PodeMirarInvalidOption best = null;
        for (int i = 0; i < invalids.Count; i++)
        {
            PodeMirarInvalidOption invalid = invalids[i];
            if (invalid == null || invalid.targetUnit != target)
                continue;
            if (IsBlockedLineReason(invalid.reasonId))
                return invalid;
            if (best == null)
                best = invalid;
        }

        return best;
    }

    private static bool IsBlockedLineReason(string reasonId)
    {
        return reasonId == PodeMirarInvalidOption.ReasonIdLdtBlocked
            || reasonId == PodeMirarInvalidOption.ReasonIdLosBlocked;
    }
}
