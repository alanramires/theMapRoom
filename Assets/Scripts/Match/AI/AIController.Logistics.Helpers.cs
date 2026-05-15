using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private static bool IsPrimaryLogisticsUnit(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.roles != null
            && data.roles.Count > 0
            && data.roles.Contains(UnitRole.Logistica);
    }

    private static bool PreferLogisticsBestDpq(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.preferMoveOnBestDPQ;
    }

    private Dictionary<Vector3Int, List<Vector3Int>> BuildLogisticsPaths(UnitManager unit)
    {
        return UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap,
            unit,
            Mathf.Max(0, unit.RemainingMovementPoints),
            terrainDatabase);
    }

    private bool IsLogisticsBaseDefenseEmergency(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        if (snapshot.Stance == AIStance.Defensive)
            return true;

        if (snapshot.MyBuildings == null)
            return false;

        for (int i = 0; i < snapshot.MyBuildings.Count; i++)
        {
            ConstructionManager building = snapshot.MyBuildings[i];
            if (building == null)
                continue;

            bool strategic = building.IsPlayerHeadQuarter || building.CanProduceUnitsForTeam(snapshot.AITeam);
            if (!strategic)
                continue;

            if (IsHomeDefenseThreatened(building.Sector, snapshot.AITeam, HomeDefenseThreatRange))
                return true;
        }

        return false;
    }

    private Vector3Int ResolveLogisticsAnchor(AIWorldSnapshot snapshot, Vector3Int fallback)
    {
        if (snapshot == null)
            return fallback;

        ConstructionManager best = null;
        float bestScore = float.MinValue;
        if (snapshot.MyBuildings != null)
        {
            for (int i = 0; i < snapshot.MyBuildings.Count; i++)
            {
                ConstructionManager building = snapshot.MyBuildings[i];
                if (building == null)
                    continue;

                bool strategic = building.IsPlayerHeadQuarter || building.CanProduceUnitsForTeam(snapshot.AITeam);
                if (!strategic)
                    continue;

                Vector3Int cell = building.CurrentCellPosition;
                cell.z = 0;
                float score = -SectorManager.HexDistance(fallback, cell);
                if (building.IsPlayerHeadQuarter)
                    score += 8f;
                if (building.CanProduceUnitsForTeam(snapshot.AITeam))
                    score += 3f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = building;
                }
            }
        }

        if (best != null)
        {
            Vector3Int cell = best.CurrentCellPosition;
            cell.z = 0;
            return cell;
        }

        if (snapshot.MyHQ != null)
        {
            Vector3Int hq = snapshot.MyHQ.CurrentCellPosition;
            hq.z = 0;
            return hq;
        }

        return fallback;
    }

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

            Vector3Int cell = ally.CurrentCellPosition;
            cell.z = 0;
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float score = ScoreLogisticsTargetNeed(snapshot, fromCell, ally)
                + threat * (baseDefense ? 120f : 35f)
                - SectorManager.HexDistance(fromCell, cell) * 45f
                - ally.InstanceId * 0.001f;
            if (reachableCritical)
                score += 5000f;

            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
            }
        }

        return best;
    }

    private bool LogisticsHasEmptyCargoProduct(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.supplierResources == null)
            return false;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null)
            return false;

        int count = Mathf.Min(resources.Count, data.supplierResources.Count);
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedSupply baseline = data.supplierResources[i];
            UnitEmbarkedSupply runtime = resources[i];
            if (baseline == null || baseline.supply == null || baseline.amount <= 0)
                continue;
            if (runtime == null || runtime.amount <= 0)
                return true;
        }

        return false;
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
            if (!baseDefense && threat > 0f)
                continue;

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

    private int GetLogisticsServiceLimit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return 0;
        return Mathf.Max(0, data.maxUnitsServedPerTurn);
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
            Vector3Int ac = a.CurrentCellPosition;
            ac.z = 0;
            Vector3Int bc = b.CurrentCellPosition;
            bc.z = 0;
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

    private bool IsLogisticsServiceCellAllowed(UnitManager logistics, AIWorldSnapshot snapshot, Vector3Int cell)
    {
        if (logistics == null
            || !logistics.TryGetUnitData(out UnitData data)
            || data == null
            || !data.playConservative
            || data.aiConservativeSupplyAvoidEnemyRange <= 0)
            return true;

        TeamId aiTeam = snapshot != null ? snapshot.AITeam : logistics.TeamId;
        return !HasNearbyVisibleEnemy(cell, aiTeam, data.aiConservativeSupplyAvoidEnemyRange);
    }

    private bool TryFindLogisticsReloadCell(
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
        if (paths == null || paths.Count == 0 || snapshot == null || snapshot.MyBuildings == null)
            return false;

        ConstructionManager target = FindSafeLogisticsReloadConstruction(snapshot, fromCell);
        if (target == null)
            return false;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        float fromDist = SectorManager.HexDistance(fromCell, targetCell);
        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;

            float dist = SectorManager.HexDistance(cell, targetCell);
            float progress = fromDist - dist;
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float dpq = GetTerrainDpqPontos(cell);
            float score = progress * 650f
                - dist * 80f
                + dpq * 40f
                - threat * 120f
                - GetPathStepCount(paths, cell) * 8f;

            if (cell == targetCell)
                score += 4000f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        if (bestCell == fromCell && fromDist > 0f)
            return false;

        reason = $"dest={targetCell} dist={SectorManager.HexDistance(bestCell, targetCell):F1} score={bestScore:F0}";
        return true;
    }

    private ConstructionManager FindSafeLogisticsReloadConstruction(AIWorldSnapshot snapshot, Vector3Int fromCell)
    {
        ConstructionManager best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < snapshot.MyBuildings.Count; i++)
        {
            ConstructionManager building = snapshot.MyBuildings[i];
            if (building == null || building.TeamId != snapshot.AITeam)
                continue;
            if (building.CurrentCapturePoints < building.CapturePointsMax)
                continue;
            if (!IsLogisticsReloadConstruction(building))
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            if (threat > 0f)
                continue;

            float score = -SectorManager.HexDistance(fromCell, cell) * 100f
                + (building.IsPlayerHeadQuarter ? 250f : 0f)
                + (building.CanProduceUnitsForTeam(snapshot.AITeam) ? 100f : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                best = building;
            }
        }

        return best;
    }

    private static bool IsLogisticsReloadConstruction(ConstructionManager building)
    {
        if (building == null)
            return false;
        if (building.IsPlayerHeadQuarter)
            return true;
        if (building.TryResolveConstructionData(out ConstructionData data) && data != null && data.isSupplier && data.supplierTier == SupplierTier.Hub)
            return true;
        return building.CanProduceUnits;
    }

    private bool TryFindLogisticsRepositionCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int anchor,
        UnitManager serviceTarget,
        bool baseDefense,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        bool preferBestDpq = PreferLogisticsBestDpq(unit);
        Vector3Int serviceCell = Vector3Int.zero;
        bool hasServiceTarget = serviceTarget != null;
        if (hasServiceTarget)
        {
            serviceCell = serviceTarget.CurrentCellPosition;
            serviceCell.z = 0;
        }
        bool currentBlocksProduction = IsLogisticsBlockingProduction(snapshot, fromCell);

        float fromScore = ScoreLogisticsCell(
            unit,
            snapshot,
            fromCell,
            fromCell,
            anchor,
            serviceCell,
            hasServiceTarget,
            serviceTarget,
            baseDefense,
            preferBestDpq,
            0,
            out string fromDetails);

        float bestScore = float.MinValue;
        string bestDetails = "";
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            if (currentBlocksProduction && cell == fromCell)
                continue;

            int pathCost = cell == fromCell ? 0 : GetPathStepCount(paths, cell);
            float score = ScoreLogisticsCell(
                unit,
                snapshot,
                cell,
                fromCell,
                anchor,
                serviceCell,
                hasServiceTarget,
                serviceTarget,
                baseDefense,
                preferBestDpq,
                pathCost,
                out string details);

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestDetails = details;
            }
        }

        if (currentBlocksProduction)
        {
            if (bestCell != fromCell && bestScore > float.MinValue)
            {
                reason = $"desocupa_produtora score={bestScore:F0} {bestDetails}";
                return true;
            }

            reason = $"bloqueia_produtora_sem_saida holdScore={fromScore:F0} {fromDetails}";
            return false;
        }

        float moveMargin = preferBestDpq ? 35f : 80f;
        if (bestCell == fromCell || bestScore < fromScore + moveMargin)
        {
            reason = $"holdScore={fromScore:F0} {fromDetails}";
            return false;
        }

        reason = $"score={bestScore:F0} hold={fromScore:F0} {bestDetails}";
        return true;
    }

    private float ScoreLogisticsCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int fromCell,
        Vector3Int anchor,
        Vector3Int serviceCell,
        bool hasServiceTarget,
        UnitManager serviceTarget,
        bool baseDefense,
        bool preferBestDpq,
        int pathCost,
        out string details)
    {
        float dpq = GetTerrainDpqPontos(cell);
        float threat = CalculateThreatLevel(cell, snapshot.AITeam);
        float distToAnchor = SectorManager.HexDistance(cell, anchor);
        float currentDistToAnchor = SectorManager.HexDistance(fromCell, anchor);
        float anchorProgress = currentDistToAnchor - distToAnchor;
        float cohesion = CalculateFireSupportCohesionScore(unit, snapshot, cell);
        float rearArea = CalculateLogisticsRearAreaScore(unit, snapshot, cell, anchor);
        float serviceProgress = 0f;
        float serviceDist = 0f;
        float serviceNeed = 0f;
        bool serviceInRange = false;

        if (hasServiceTarget)
        {
            float currentServiceDist = SectorManager.HexDistance(fromCell, serviceCell);
            serviceDist = SectorManager.HexDistance(cell, serviceCell);
            serviceProgress = currentServiceDist - serviceDist;
            serviceNeed = ScoreLogisticsTargetNeed(snapshot, cell, serviceTarget);
            serviceInRange = IsInLogisticsServiceRange(unit, cell, serviceTarget);
        }

        float dpqWeight = preferBestDpq ? 125f : 70f;
        float threatWeight = baseDefense ? 38f : 125f;
        float pathWeight = preferBestDpq ? 8f : 18f;
        float score = dpq * dpqWeight
            + cohesion * 0.35f
            + rearArea
            - threat * threatWeight
            - pathCost * pathWeight;

        if (hasServiceTarget)
        {
            score += serviceProgress * (baseDefense ? 420f : 520f);
            score += Mathf.Min(2200f, serviceNeed * 0.45f);
            score -= serviceDist * 45f;
            if (serviceProgress > 0f)
                score += 700f;
            if (serviceInRange)
            {
                if (IsLogisticsServiceCellAllowed(unit, snapshot, cell))
                    score += 4500f;
                else
                    score -= 5000f;
            }
        }
        else
            score += anchorProgress * 85f - Mathf.Abs(distToAnchor - 2f) * 25f;

        if (!baseDefense && IsLogisticsForwardOfMainLine(unit, snapshot, cell, anchor))
            score -= 2600f;

        if (!baseDefense && threat > 0f && cell != fromCell)
            score -= 450f;

        if (preferBestDpq && cell != fromCell && dpq <= GetTerrainDpqPontos(fromCell))
            score -= 90f;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        bool blocksProduction = construction != null && construction.CanProduceUnitsForTeam(snapshot.AITeam);
        if (blocksProduction)
            score -= cell == fromCell ? 220f : 900f;

        string serviceDetails = hasServiceTarget && serviceTarget != null
            ? $" service={serviceTarget.UnitDisplayName}#{serviceTarget.InstanceId} serviceDist={serviceDist:F1} serviceNeed={serviceNeed:F0} serviceRange={serviceInRange}"
            : "";
        details = $"baseDef={baseDefense} dpq={dpq:F1} threat={threat:F1} rear={rearArea:F0} coh={cohesion:F0} " +
                  $"anchorDist={distToAnchor:F1} serviceProg={serviceProgress:F1}{serviceDetails} path={pathCost}";
        return score;
    }

    private bool IsLogisticsBlockingProduction(AIWorldSnapshot snapshot, Vector3Int cell)
    {
        if (snapshot == null)
            return false;

        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return construction != null && construction.CanProduceUnitsForTeam(snapshot.AITeam);
    }

    private float CalculateLogisticsRearAreaScore(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell, Vector3Int anchor)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0f;

        cell.z = 0;
        anchor.z = 0;
        float cellDist = SectorManager.HexDistance(cell, anchor);
        float allyDistSum = 0f;
        float frontDist = 0f;
        float rearGuardDist = float.MaxValue;
        int allyCount = 0;

        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (IsPrimaryLogisticsUnit(ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(allyCell, anchor);
            allyDistSum += dist;
            frontDist = Mathf.Max(frontDist, dist);
            rearGuardDist = Mathf.Min(rearGuardDist, dist);
            allyCount++;
        }

        float score = ScoreLogisticsOwnedArea(snapshot, cell);
        if (allyCount == 0)
            return score - Mathf.Abs(cellDist - 2f) * 65f;

        float averageDist = allyDistSum / allyCount;
        float desiredDist = Mathf.Clamp(rearGuardDist - 1f, 1f, Mathf.Max(1f, averageDist - 1f));
        score += 900f - Mathf.Abs(cellDist - desiredDist) * 95f;

        if (cellDist > rearGuardDist + 1f)
            score -= (cellDist - rearGuardDist - 1f) * 520f;
        if (cellDist > averageDist - 0.5f)
            score -= (cellDist - averageDist + 0.5f) * 260f;
        if (cellDist > frontDist - 1f)
            score -= 1800f;

        return score;
    }

    private float ScoreLogisticsOwnedArea(AIWorldSnapshot snapshot, Vector3Int cell)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return 0f;

        float best = 0f;
        for (int i = 0; i < snapshot.MyBuildings.Count; i++)
        {
            ConstructionManager building = snapshot.MyBuildings[i];
            if (building == null || building.TeamId != snapshot.AITeam)
                continue;
            if (building.IsCapturable && building.CurrentCapturePoints < building.CapturePointsMax)
                continue;

            Vector3Int buildingCell = building.CurrentCellPosition;
            buildingCell.z = 0;
            float dist = SectorManager.HexDistance(cell, buildingCell);
            float value = Mathf.Max(0f, 3f - dist) * 180f;
            if (building.IsPlayerHeadQuarter)
                value += Mathf.Max(0f, 5f - dist) * 80f;
            if (building.CanProduceUnitsForTeam(snapshot.AITeam))
                value += Mathf.Max(0f, 4f - dist) * 65f;
            best = Mathf.Max(best, value);
        }

        return best;
    }

    private bool IsLogisticsForwardOfMainLine(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell, Vector3Int anchor)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return false;

        cell.z = 0;
        anchor.z = 0;
        float cellDist = SectorManager.HexDistance(cell, anchor);
        float allyDistSum = 0f;
        float rearGuardDist = float.MaxValue;
        int allyCount = 0;

        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (IsPrimaryLogisticsUnit(ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(allyCell, anchor);
            allyDistSum += dist;
            rearGuardDist = Mathf.Min(rearGuardDist, dist);
            allyCount++;
        }

        if (allyCount == 0)
            return false;

        float averageDist = allyDistSum / allyCount;
        float forwardLimit = Mathf.Min(averageDist + 0.5f, rearGuardDist + 2f);
        return cellDist > forwardLimit;
    }
}
