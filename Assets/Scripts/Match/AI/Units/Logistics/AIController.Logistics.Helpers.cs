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
