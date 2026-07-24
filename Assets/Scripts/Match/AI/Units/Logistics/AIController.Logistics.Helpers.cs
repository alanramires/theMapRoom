using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Helpers de logística: propriedades de unidade, âncora, posicionamento
    // e métricas de área controlada.
    // -------------------------------------------------------------------------

    private static bool IsPrimaryLogisticsUnit(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && UnitRoleCompatibility.CanSatisfy(data, UnitRole.Logistica);
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

    private bool ShouldRestockLogisticsUnit(UnitManager unit, out string reason)
    {
        reason = "";
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.supplierResources == null)
            return false;
        if (!data.isSupplier)
            return false;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null)
            return false;

        int count = data.supplierResources.Count;
        bool emptySupplyTriggersRestock = data.restockWhenAnyRuntimeSupplyEmpty || !HasAnyRestockThresholdConfigured(data);
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedSupply baseline = data.supplierResources[i];
            if (baseline == null || baseline.supply == null || baseline.amount <= 0)
                continue;

            int runtimeAmount = GetRuntimeSupplyAmount(resources, baseline.supply);
            int threshold = ResolveRestockThresholdForSupply(data, baseline.supply);
            bool coreRestockSupply = threshold > 0 || IsCoreRestockSupply(baseline.supply);
            if (runtimeAmount <= 0 && (coreRestockSupply || emptySupplyTriggersRestock))
            {
                reason = $"restock vazio {GetSupplyDebugName(baseline.supply)}";
                return true;
            }

            if (threshold <= 0)
                continue;

            float pct = runtimeAmount * 100f / baseline.amount;
            if (pct <= threshold)
            {
                reason = $"restock {GetSupplyDebugName(baseline.supply)} {pct:F0}%<={threshold}%";
                return true;
            }
        }

        reason = BuildRestockStockSummary(data, resources);
        return false;
    }

    private static int GetRuntimeSupplyAmount(IReadOnlyList<UnitEmbarkedSupply> resources, SupplyData supply)
    {
        if (resources == null || supply == null)
            return 0;

        bool foundExact = false;
        int exactTotal = 0;
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || !IsSameSupply(entry.supply, supply))
                continue;

            foundExact = true;
            exactTotal += Mathf.Max(0, entry.amount);
        }

        if (foundExact)
            return exactTotal;

        int kind = ResolveRestockSupplyKind(supply);
        if (kind == 0)
            return 0;

        int kindTotal = 0;
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || ResolveRestockSupplyKind(entry.supply) != kind)
                continue;

            kindTotal += Mathf.Max(0, entry.amount);
        }

        return kindTotal;
    }

    private static bool IsSameSupply(SupplyData a, SupplyData b)
    {
        if (a == null || b == null)
            return false;
        if (ReferenceEquals(a, b))
            return true;
        if (!string.IsNullOrWhiteSpace(a.id) && !string.IsNullOrWhiteSpace(b.id))
            return a.id == b.id;
        return a.name == b.name;
    }

    private static string BuildRestockStockSummary(UnitData data, IReadOnlyList<UnitEmbarkedSupply> resources)
    {
        if (data == null || data.supplierResources == null)
            return "restockCheck sem UnitData";

        string summary = "restockCheck ok";
        for (int i = 0; i < data.supplierResources.Count; i++)
        {
            UnitEmbarkedSupply baseline = data.supplierResources[i];
            if (baseline == null || baseline.supply == null || baseline.amount <= 0)
                continue;

            int kind = ResolveRestockSupplyKind(baseline.supply);
            if (kind == 0)
                continue;

            int runtimeAmount = GetRuntimeSupplyAmount(resources, baseline.supply);
            summary += $" {GetSupplyDebugName(baseline.supply)}={runtimeAmount}/{baseline.amount}";
        }

        return summary;
    }

    private static bool IsCoreRestockSupply(SupplyData supply)
    {
        return ResolveRestockSupplyKind(supply) != 0;
    }

    private static bool HasAnyRestockThresholdConfigured(UnitData data)
    {
        return data != null
            && (data.restockTriggerGallonPct > 0
                || data.restockTriggerAmmoBoxPct > 0
                || data.restockTriggerToolsPct > 0);
    }

    private static int ResolveRestockThresholdForSupply(UnitData data, SupplyData supply)
    {
        if (data == null || supply == null)
            return 0;

        switch (ResolveRestockSupplyKind(supply))
        {
            case 1: return data.restockTriggerGallonPct;
            case 2: return data.restockTriggerAmmoBoxPct;
            case 3: return data.restockTriggerToolsPct;
            default: return 0;
        }
    }

    private static int ResolveRestockSupplyKind(SupplyData supply)
    {
        if (supply == null)
            return 0;

        string key = ((supply.id ?? "") + " " + (supply.displayName ?? "") + " " + supply.name).ToLowerInvariant();
        if (key.Contains("gasolina") || key.Contains("gala") || key.Contains("fuel"))
            return 1;
        if (key.Contains("municao") || key.Contains("muni") || key.Contains("ammo"))
            return 2;
        if (key.Contains("pecas") || key.Contains("peca") || key.Contains("tool") || key.Contains("part"))
            return 3;

        return 0;
    }

    private static string GetSupplyDebugName(SupplyData supply)
    {
        if (supply == null)
            return "(supply)";
        if (!string.IsNullOrWhiteSpace(supply.displayName))
            return supply.displayName;
        if (!string.IsNullOrWhiteSpace(supply.id))
            return supply.id;
        return supply.name;
    }

    private int GetLogisticsServiceLimit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return 0;
        return Mathf.Max(0, data.maxUnitsServedPerTurn);
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

    // Returns terrain-aware movement cost for unit to walk from fromCell to toCell.
    // Explores up to maxBudget MP; returns maxBudget + 1 when not reachable within that budget.
    private int TerrainCostToCell(UnitManager unit, Vector3Int fromCell, Vector3Int toCell, int maxBudget)
    {
        if (maxBudget <= 0) return maxBudget + 1;
        var costMap = UnitMovementPathRules.CalculateMovementCostMap(
            boardTilemap, unit, fromCell, maxBudget, terrainDatabase);
        return costMap != null && costMap.TryGetValue(toCell, out int cost) ? cost : maxBudget + 1;
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

    private bool IsLogisticsBlockingProduction(AIWorldSnapshot snapshot, Vector3Int cell)
    {
        if (snapshot == null)
            return false;

        return IsLogisticsProductionCell(snapshot.AITeam, cell);
    }

    private bool IsLogisticsProductionCell(TeamId aiTeam, Vector3Int cell)
    {

        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return construction != null && construction.CanProduceUnitsForTeam(aiTeam);
    }

    private bool HasReachableNonProductionLogisticsCell(
        UnitManager logistics,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (logistics == null || snapshot == null || paths == null)
            return false;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;
            if (IsLogisticsProductionCell(snapshot.AITeam, cell))
                continue;
            if (!IsLogisticsServiceCellAllowed(logistics, snapshot, cell))
                continue;
            return true;
        }

        return false;
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
            if (building == null || building.SlotIndex != snapshot.AISlotIndex)
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
