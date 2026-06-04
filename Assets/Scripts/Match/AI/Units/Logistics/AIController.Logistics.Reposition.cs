using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Reposicionamento e reabastecimento de logística
    // -------------------------------------------------------------------------

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

        if (TryFindBestToolProgressionCell(
                unit,
                snapshot,
                fromCell,
                targetCell,
                paths,
                occupied,
                ToolProgressionIntent.LogisticsReload,
                out Vector3Int toolReloadCell,
                out ToolProgressionCandidate toolReloadCandidate,
                out string toolReloadReason,
                tacticalScore: (cell, candidate) =>
                {
                    float dist = SectorManager.HexDistance(cell, targetCell);
                    float progress = fromDist - dist;
                    float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                    float dpq = GetTerrainDpqPontos(cell);
                    float score = progress * 650f
                        - dist * 80f
                        + dpq * 40f
                        - threat * 120f
                        - candidate.MoveCost * 8f;

                    if (cell == targetCell)
                        score += 4000f;

                    return score;
                }))
        {
            bool hasReloadProgress = toolReloadCandidate.ToolScore > 0
                || toolReloadCandidate.FirstTurnProgress > 0f
                || toolReloadCandidate.TwoTurnProgress > 0f;
            if (hasReloadProgress || toolReloadCell == targetCell)
            {
                bestCell = toolReloadCell;
                reason = $"toolProgress dest={targetCell} {toolReloadReason}";
                return true;
            }
        }

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
            unit, snapshot, fromCell, fromCell, anchor, serviceCell,
            hasServiceTarget, serviceTarget, baseDefense, preferBestDpq, 0, out string fromDetails);

        Vector3Int progressionTarget = hasServiceTarget ? serviceCell : anchor;
        ToolProgressionIntent progressionIntent = hasServiceTarget
            ? ToolProgressionIntent.LogisticsService
            : ToolProgressionIntent.LogisticsReload;

        if (TryFindBestToolProgressionCell(
                unit,
                snapshot,
                fromCell,
                progressionTarget,
                paths,
                occupied,
                progressionIntent,
                out Vector3Int toolCell,
                out ToolProgressionCandidate toolCandidate,
                out string toolReason,
                allowCell: cell => !(currentBlocksProduction && cell == fromCell),
                tacticalScore: (cell, candidate) =>
                {
                    int pathCost = cell == fromCell ? 0 : candidate.MoveCost;
                    return ScoreLogisticsCell(
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
                        out _);
                }))
        {
            float toolHoldMargin = preferBestDpq ? 35f : 80f;
            if (currentBlocksProduction || toolCandidate.TacticalScore >= fromScore + toolHoldMargin)
            {
                bestCell = toolCell;
                reason = currentBlocksProduction
                    ? $"toolProgress desocupa_produtora {toolReason}"
                    : $"toolProgress hold={fromScore:F0} {toolReason}";
                return true;
            }
        }

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
                unit, snapshot, cell, fromCell, anchor, serviceCell,
                hasServiceTarget, serviceTarget, baseDefense, preferBestDpq, pathCost, out string details);

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
}
