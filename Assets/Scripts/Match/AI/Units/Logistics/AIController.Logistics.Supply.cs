using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Serviço de suprimento: seleção de alvos, manutenção preventiva,
    // construção de ações de supply e transfer.
    // -------------------------------------------------------------------------

    private UnitManager FindLogisticsServiceTarget(
        UnitManager logistics,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool baseDefense)
    {
        if (logistics == null || snapshot == null || snapshot.MyUnits == null)
            return null;

        bool allowPreventiveMaintenance = IsPreventiveLogisticsAllowed(logistics, snapshot, fromCell, paths, occupied);
        UnitManager best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (ally == null
                || ally == logistics
                || ally.IsDead
                || ally.IsEmbarked
                || ally.TeamId != logistics.TeamId
                || ally.ReceivedSuppliesThisTurn)
                continue;

            bool critical = ally.IsUnderRepair;
            bool reachableCritical = critical && IsReachableLogisticsServiceTarget(logistics, ally, fromCell, paths, occupied);
            if (critical && allowPreventiveMaintenance && !reachableCritical)
                continue;

            bool preventive = allowPreventiveMaintenance && IsPreventiveLogisticsTarget(logistics, ally);
            if (!critical && !preventive)
                continue;

            bool preventiveReachable = preventive && !critical
                && IsReachableLogisticsServiceTarget(logistics, ally, fromCell, paths, occupied);

            Vector3Int cell = ally.CurrentCellPosition;
            cell.z = 0;
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float score = ScoreLogisticsTargetNeed(snapshot, fromCell, ally)
                + threat * (baseDefense ? 120f : 35f)
                - SectorManager.HexDistance(fromCell, cell) * 45f
                - ally.InstanceId * 0.001f;
            if (reachableCritical)
                score += 5000f;
            if (preventiveReachable)
                score += 2500f;

            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
            }
        }

        return best;
    }

    private bool TryBuildLogisticsTransferReceiveAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        var options = new List<PodeTransferirOption>();
        if (!PodeTransferirSensor.CollectOptions(unit, boardTilemap, options, out string sensorReason) || options.Count <= 0)
        {
            reason = sensorReason;
            return false;
        }

        PodeTransferirOption best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < options.Count; i++)
        {
            PodeTransferirOption option = options[i];
            if (option == null || option.flowMode != TransferFlowMode.Recebedor)
                continue;

            Vector3Int cell = option.targetCell;
            cell.z = 0;
            float score = -CalculateThreatLevel(cell, snapshot.AITeam) * 100f
                - SectorManager.HexDistance(fromCell, cell) * 10f
                + (option.targetConstruction != null ? 50f : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                best = option;
            }
        }

        if (best == null)
            return false;

        action = BuildTransferReceiveBatch(unit, snapshot.AITeam, fromCell, fromCell, best, paths);
        reason = $"alvo={best.targetCell} score={bestScore:F0}";
        return true;
    }

    private bool TryBuildLogisticsTransferReceiveActionAtCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int transferCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null)
            return false;

        Vector3Int originalCell = unit.CurrentCellPosition;
        originalCell.z = 0;
        transferCell.z = 0;

        unit.SetCurrentCellPosition(transferCell, enforceFinalOccupancyRule: false);
        try
        {
            var options = new List<PodeTransferirOption>();
            if (!PodeTransferirSensor.CollectOptions(unit, boardTilemap, options, out string sensorReason) || options.Count <= 0)
            {
                reason = sensorReason;
                return false;
            }

            PodeTransferirOption best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < options.Count; i++)
            {
                PodeTransferirOption option = options[i];
                if (option == null || option.flowMode != TransferFlowMode.Recebedor)
                    continue;

                Vector3Int cell = option.targetCell;
                cell.z = 0;
                float score = -CalculateThreatLevel(transferCell, snapshot.AITeam) * 100f
                    - SectorManager.HexDistance(transferCell, cell) * 10f
                    + (option.targetConstruction != null ? 50f : 0f);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = option;
                }
            }

            if (best == null)
            {
                reason = "sem opcao Recebedor apos mover";
                return false;
            }

            action = BuildTransferReceiveBatch(unit, snapshot.AITeam, fromCell, transferCell, best, paths);
            reason = $"aposMover={transferCell} alvo={best.targetCell} score={bestScore:F0}";

            return true;
        }
        finally
        {
            unit.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);
        }
    }

    private bool TryBuildLogisticsSupplyAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool baseDefense,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null || snapshot == null)
            return false;

        int limit = GetLogisticsServiceLimit(unit);
        if (limit <= 0)
            return false;
        bool allowPreventiveMaintenance = IsPreventiveLogisticsAllowed(unit, snapshot, fromCell, paths, occupied);

        var currentOptions = new List<PodeSuprirOption>();
        if (PodeSuprirSensor.CollectOptions(unit, boardTilemap, terrainDatabase, matchController, currentOptions, out _))
        {
            List<UnitManager> currentTargets = PickBestLogisticsSupplyTargets(unit, snapshot, fromCell, currentOptions, limit, allowPreventiveMaintenance);
            if (currentTargets.Count > 0)
            {
                action = BuildSupplyBatch(unit, snapshot.AITeam, fromCell, fromCell, currentTargets, paths);
                reason = allowPreventiveMaintenance ? $"preventivo agora count={currentTargets.Count}" : $"agora count={currentTargets.Count}";
                return true;
            }
        }

        if (paths == null || paths.Count == 0)
            return false;

        Vector3Int bestCell = fromCell;
        List<UnitManager> bestTargets = null;
        float bestScore = float.MinValue;
        Vector3Int anchor = ResolveLogisticsAnchor(snapshot, fromCell);
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            if (!baseDefense && cell != fromCell && IsLogisticsForwardOfMainLine(unit, snapshot, cell, anchor))
                continue;
            if (!IsLogisticsServiceCellAllowed(unit, snapshot, cell))
                continue;

            List<UnitManager> targets = CollectLogisticsTargetsInServiceRange(unit, snapshot, cell, limit, allowPreventiveMaintenance);
            if (targets.Count <= 0)
                continue;

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float dpq = GetTerrainDpqPontos(cell);
            float pairBonus = targets.Count >= 2 ? 1500f : 0f;
            float hpNeed = 0f;
            for (int i = 0; i < targets.Count; i++)
                hpNeed += Mathf.Max(0, 10 - targets[i].CurrentHP) * 70f;

            float rearArea = CalculateLogisticsRearAreaScore(unit, snapshot, cell, anchor);
            float score = targets.Count * 5000f
                + pairBonus
                + hpNeed
                + dpq * 80f
                + rearArea * 0.55f
                - threat * (baseDefense ? 30f : 110f)
                - GetPathStepCount(paths, cell) * 12f
                - cell.GetHashCode() * 0.000001f;

            if (!baseDefense && IsLogisticsForwardOfMainLine(unit, snapshot, cell, anchor))
                score -= 2200f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestTargets = targets;
            }
        }

        if (bestTargets == null || bestTargets.Count <= 0)
            return false;

        action = BuildSupplyBatch(unit, snapshot.AITeam, fromCell, bestCell, bestTargets, paths);
        reason = allowPreventiveMaintenance ? $"preventivo via={bestCell} count={bestTargets.Count} score={bestScore:F0}" : $"via={bestCell} count={bestTargets.Count} score={bestScore:F0}";
        return true;
    }

    private List<UnitManager> PickBestLogisticsSupplyTargets(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int serviceCell,
        List<PodeSuprirOption> options,
        int limit,
        bool allowPreventiveMaintenance)
    {
        var result = new List<UnitManager>();
        if (options == null || limit <= 0)
            return result;

        options.Sort((a, b) =>
        {
            float sa = ScoreLogisticsSupplyOption(snapshot, serviceCell, a);
            float sb = ScoreLogisticsSupplyOption(snapshot, serviceCell, b);
            return sb.CompareTo(sa);
        });

        var seen = new HashSet<int>();
        for (int i = 0; i < options.Count && result.Count < limit; i++)
        {
            UnitManager target = options[i] != null ? options[i].targetUnit : null;
            if (!IsLogisticsServiceTarget(unit, target, allowPreventiveMaintenance))
                continue;
            if (!seen.Add(target.InstanceId))
                continue;
            result.Add(target);
        }

        return result;
    }

    private bool TryBuildTargetedLogisticsSupplyAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        UnitManager serviceTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool baseDefense,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null || snapshot == null || snapshot.MyUnits == null)
            return false;

        int limit = GetLogisticsServiceLimit(unit);
        if (limit <= 0)
            return false;

        bool allowPreventiveMaintenance = IsPreventiveLogisticsAllowed(unit, snapshot, fromCell, paths, occupied);
        if (paths == null || paths.Count == 0)
            return false;

        Vector3Int anchor = ResolveLogisticsAnchor(snapshot, fromCell);
        Vector3Int bestCell = fromCell;
        List<UnitManager> bestTargets = null;
        float bestScore = float.MinValue;
        string bestDetails = "";

        for (int u = 0; u < snapshot.MyUnits.Count; u++)
        {
            UnitManager candidateTarget = snapshot.MyUnits[u];
            if (!IsLogisticsServiceTarget(unit, candidateTarget, allowPreventiveMaintenance))
                continue;

            Vector3Int targetCell = candidateTarget.CurrentCellPosition;
            targetCell.z = 0;
            bool critical = candidateTarget.IsUnderRepair;

            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell != fromCell && occupied != null && occupied.Contains(cell))
                    continue;
                if (!IsLogisticsServiceCellAllowed(unit, snapshot, cell))
                    continue;
                if (!IsInLogisticsServiceRange(unit, cell, candidateTarget))
                    continue;

                List<UnitManager> targets = CollectLogisticsTargetsInServiceRange(unit, snapshot, cell, limit, allowPreventiveMaintenance);
                bool containsTarget = false;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null && targets[i].InstanceId == candidateTarget.InstanceId)
                    {
                        containsTarget = true;
                        break;
                    }
                }
                if (!containsTarget)
                    continue;

                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                float dpq = GetTerrainDpqPontos(cell);
                float rearArea = CalculateLogisticsRearAreaScore(unit, snapshot, cell, anchor);
                float targetNeed = ScoreLogisticsTargetNeed(snapshot, cell, candidateTarget);
                float serviceDist = SectorManager.HexDistance(cell, targetCell);
                int pathCost = GetPathStepCount(paths, cell);
                bool forward = !baseDefense && IsLogisticsForwardOfMainLine(unit, snapshot, cell, anchor);
                bool preferred = serviceTarget != null && candidateTarget.InstanceId == serviceTarget.InstanceId;

                float score = targetNeed
                    + (critical ? 8000f : 0f)
                    + (preferred ? 2500f : 0f)
                    + targets.Count * 1200f
                    + dpq * 80f
                    + rearArea * 0.45f
                    - threat * (baseDefense ? 35f : 120f)
                    - pathCost * 14f
                    - serviceDist * 50f
                    - candidateTarget.InstanceId * 0.001f;

                if (forward)
                    score -= critical ? 900f : 2600f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTargets = targets;
                    bestDetails = $"target={candidateTarget.UnitDisplayName}#{candidateTarget.InstanceId} critical={critical} preferred={preferred} count={targets.Count} need={targetNeed:F0} threat={threat:F1} dpq={dpq:F1} rear={rearArea:F0} path={pathCost} forward={forward}";
                }
            }
        }

        if (bestTargets == null || bestTargets.Count <= 0)
            return false;

        action = BuildSupplyBatch(unit, snapshot.AITeam, fromCell, bestCell, bestTargets, paths);
        reason = $"via={bestCell} score={bestScore:F0} {bestDetails}";
        return true;
    }

    private float ScoreLogisticsSupplyOption(AIWorldSnapshot snapshot, Vector3Int serviceCell, PodeSuprirOption option)
    {
        UnitManager target = option != null ? option.targetUnit : null;
        if (target == null)
            return float.MinValue;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        return ScoreLogisticsTargetNeed(snapshot, serviceCell, target)
            + CalculateThreatLevel(targetCell, snapshot.AITeam) * 35f
            - SectorManager.HexDistance(serviceCell, targetCell) * 10f
            - target.InstanceId * 0.001f;
    }

    private List<UnitManager> CollectLogisticsTargetsInServiceRange(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int serviceCell,
        int limit,
        bool allowPreventiveMaintenance)
    {
        var result = new List<UnitManager>();
        if (unit == null || snapshot == null || snapshot.MyUnits == null || limit <= 0)
            return result;

        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (!IsLogisticsServiceTarget(unit, ally, allowPreventiveMaintenance))
                continue;
            if (!IsInLogisticsServiceRange(unit, serviceCell, ally))
                continue;

            result.Add(ally);
        }

        result.Sort((a, b) =>
        {
            Vector3Int ac = a.CurrentCellPosition; ac.z = 0;
            Vector3Int bc = b.CurrentCellPosition; bc.z = 0;
            float sa = ScoreLogisticsTargetNeed(snapshot, ac, a) + CalculateThreatLevel(ac, snapshot.AITeam) * 35f - a.InstanceId * 0.001f;
            float sb = ScoreLogisticsTargetNeed(snapshot, bc, b) + CalculateThreatLevel(bc, snapshot.AITeam) * 35f - b.InstanceId * 0.001f;
            return sb.CompareTo(sa);
        });

        if (result.Count > limit)
            result.RemoveRange(limit, result.Count - limit);
        return result;
    }

    private static bool IsLogisticsServiceTarget(UnitManager logistics, UnitManager target, bool allowPreventiveMaintenance)
    {
        if (target == null
            || logistics == null
            || target == logistics
            || target.IsDead
            || target.IsEmbarked
            || target.TeamId != logistics.TeamId
            || target.ReceivedSuppliesThisTurn)
            return false;

        if (target.IsUnderRepair)
            return true;

        return allowPreventiveMaintenance && IsPreventiveLogisticsTarget(logistics, target);
    }

    private bool IsPreventiveLogisticsAllowed(
        UnitManager logistics,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (logistics == null
            || !logistics.TryGetUnitData(out UnitData data)
            || data == null
            || !data.aiPreventiveMaintenanceEnabled)
            return false;

        return data.aiPreventiveSupplyCanRunWithUnderRepair
            || !HasReachableCriticalLogisticsTarget(logistics, snapshot, fromCell, paths, occupied);
    }

    private bool HasReachableCriticalLogisticsTarget(
        UnitManager logistics,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (logistics == null || snapshot == null || snapshot.MyUnits == null)
            return false;

        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (ally == null
                || ally == logistics
                || ally.IsDead
                || ally.IsEmbarked
                || ally.TeamId != logistics.TeamId
                || ally.ReceivedSuppliesThisTurn
                || !ally.IsUnderRepair)
                continue;

            if (IsReachableLogisticsServiceTarget(logistics, ally, fromCell, paths, occupied))
                return true;
        }

        return false;
    }

    private bool IsReachableLogisticsServiceTarget(
        UnitManager logistics,
        UnitManager target,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (IsInLogisticsServiceRange(logistics, fromCell, target))
            return IsLogisticsServiceCellAllowed(logistics, null, fromCell);
        if (paths == null || paths.Count == 0)
            return false;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            if (!IsLogisticsServiceCellAllowed(logistics, null, cell))
                continue;
            if (IsInLogisticsServiceRange(logistics, cell, target))
                return true;
        }

        return false;
    }

    private static bool IsPreventiveLogisticsTarget(UnitManager logistics, UnitManager target)
    {
        if (logistics == null || target == null || !logistics.TryGetUnitData(out UnitData data) || data == null)
            return false;

        if (data.aiPreventiveSupplyHpBelowPct > 0)
        {
            int maxHp = Mathf.Max(1, target.GetMaxHP());
            if (target.CurrentHP * 100f / maxHp < data.aiPreventiveSupplyHpBelowPct)
                return true;
        }

        if (data.aiPreventiveSupplyAutonomyBelowPct > 0)
        {
            int maxFuel = Mathf.Max(1, target.GetMaxFuel());
            if (target.CurrentFuel * 100f / maxFuel < data.aiPreventiveSupplyAutonomyBelowPct)
                return true;
        }

        return data.aiPreventiveSupplyWeaponAmmoAtOrBelow > 0
            && HasAnyWeaponAmmoAtOrBelow(target, data.aiPreventiveSupplyWeaponAmmoAtOrBelow);
    }

    private static bool HasAnyWeaponAmmoAtOrBelow(UnitManager unit, int ammoThreshold)
    {
        if (ammoThreshold <= 0 || unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.embarkedWeapons == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = unit.GetEmbarkedWeapons();
        if (runtimeWeapons == null)
            return false;

        int count = Mathf.Min(runtimeWeapons.Count, data.embarkedWeapons.Count);
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = data.embarkedWeapons[i];
            if (runtime == null || baseline == null)
                continue;
            if (baseline.squadAmmunition > 0 && runtime.squadAmmunition <= ammoThreshold)
                return true;
        }

        return false;
    }

    private float ScoreLogisticsTargetNeed(AIWorldSnapshot snapshot, Vector3Int serviceCell, UnitManager target)
    {
        if (target == null)
            return 0f;

        float valueBonus = target.TryGetUnitData(out UnitData vd) && vd != null ? vd.cost / 100f : 0f;

        if (target.IsUnderRepair)
            return 10000f + Mathf.Max(0, target.GetMaxHP() - target.CurrentHP) * 120f + valueBonus;

        float score = valueBonus;
        int maxHp = Mathf.Max(1, target.GetMaxHP());
        int maxFuel = Mathf.Max(1, target.GetMaxFuel());
        score += Mathf.Max(0f, 100f - target.CurrentHP * 100f / maxHp) * 18f;
        score += Mathf.Max(0f, 100f - target.CurrentFuel * 100f / maxFuel) * 10f;
        if (vd != null && HasAnyWeaponAmmoAtOrBelow(target, 1))
            score += 1400f;
        return score;
    }

    private static bool IsInLogisticsServiceRange(UnitManager logistics, Vector3Int serviceCell, UnitManager target)
    {
        if (logistics == null || target == null || !logistics.TryGetUnitData(out UnitData data) || data == null)
            return false;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        float dist = SectorManager.HexDistance(serviceCell, targetCell);
        switch (data.serviceRange)
        {
            case SupplierRangeMode.Hybrid0Or1Hex:
                return dist <= 1f;
            case SupplierRangeMode.Adjacent1Hex:
                return Mathf.Approximately(dist, 1f);
            case SupplierRangeMode.EmbarkedOnly:
                return target.IsEmbarked && target.EmbarkedTransporter == logistics;
            default:
                return false;
        }
    }
}
