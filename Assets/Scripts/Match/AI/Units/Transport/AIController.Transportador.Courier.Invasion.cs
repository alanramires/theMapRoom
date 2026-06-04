using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private bool IsTransportInvasionDelivery(
        UnitManager passenger,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int target)
    {
        if (passenger == null || snapshot == null)
            return false;

        SectorObjective objective = ResolveAnyAssignedObjective(passenger, plan);
        if (objective != null)
        {
            if (objective.ObjectiveType == AIObjectiveType.InvasionAttack)
                return true;
            if (ConstructionSectorHelper.IsBase(objective.Sector)
                && !IsCriticalHomeDefenseObjective(objective, snapshot.AITeam))
                return true;
        }

        target.z = 0;
        ConstructionManager building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, target);
        if (building != null
            && building.TeamId != snapshot.AITeam
            && (building.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(building.Sector)))
            return true;

        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition;
            hq.z = 0;
            if (SectorManager.HexDistance(target, hq) <= 4f)
                return true;
        }

        return false;
    }


    private bool IsTransportInvasionCourierCellAllowed(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int target)
    {
        if (cell == transporter.CurrentCellPosition)
            return true;

        return TryGetTransportScreenMetrics(transporter, snapshot, cell, target,
            out float gap, out _, out _)
            && gap >= 0.75f;
    }


    private bool IsTransportInvasionDropAllowed(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int transporterCell,
        Vector3Int dropCell,
        Vector3Int target)
    {
        if (!IsTransportInvasionCourierCellAllowed(transporter, snapshot, transporterCell, target))
            return false;

        return TryGetTransportScreenMetrics(transporter, snapshot, dropCell, target,
            out float gap, out _, out _)
            && gap >= 0.25f;
    }


    private Vector3Int FindTransportInvasionRendezvousCell(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int target,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out string reason)
    {
        Vector3Int best = fromCell;
        float bestScore = float.MinValue;
        reason = "sem screen";

        if (TryFindBestToolProgressionCell(
                transporter,
                snapshot,
                fromCell,
                target,
                paths,
                occupied,
                ToolProgressionIntent.TransportRendezvous,
                out Vector3Int toolCell,
                out ToolProgressionCandidate toolCandidate,
                out string toolReason,
                allowCell: cell =>
                {
                    if (IsNonTeamConstruction(cell, snapshot.AITeam))
                        return false;
                    if (!TryGetTransportScreenMetrics(transporter, snapshot, cell, target,
                            out float gap, out _, out _))
                        return false;
                    return gap >= 0.75f;
                },
                tacticalScore: (cell, candidate) =>
                {
                    if (!TryGetTransportScreenMetrics(transporter, snapshot, cell, target,
                            out float gap, out float nearestScreenDist, out float cellDist))
                        return -100000f;

                    float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                    float dpq = GetTerrainDpqPontos(cell);
                    float idealGap = 2f;
                    return 3000f
                        - Mathf.Abs(gap - idealGap) * 420f
                        - threat * 90f
                        - candidate.MoveCost * 12f
                        - cellDist * 4f
                        + dpq * 25f
                        - nearestScreenDist * 2f;
                }))
        {
            best = toolCell;
            reason = $"toolProgress {toolReason}";
            return best;
        }

        var candidates = new List<Vector3Int> { fromCell };
        if (paths != null)
            candidates.AddRange(paths.Keys);

        foreach (Vector3Int rawCell in candidates)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            if (cell != fromCell && IsNonTeamConstruction(cell, snapshot.AITeam))
                continue;
            if (!TryGetTransportScreenMetrics(transporter, snapshot, cell, target,
                    out float gap, out float nearestScreenDist, out float cellDist))
                continue;
            if (gap < 0.75f)
                continue;

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            int pathCost = cell == fromCell ? 0 : GetPathStepCount(paths, cell);
            float dpq = GetTerrainDpqPontos(cell);
            float idealGap = 2f;
            float score = 3000f
                - Mathf.Abs(gap - idealGap) * 420f
                - threat * 90f
                - pathCost * 12f
                - cellDist * 4f
                + dpq * 25f
                + (cell == fromCell ? 120f : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
                reason = $"gap={gap:F1} screenDist={nearestScreenDist:F1} cellDist={cellDist:F1} threat={threat:F1} path={pathCost} score={score:F0}";
            }
        }

        return best;
    }


    private bool TryGetTransportScreenMetrics(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int target,
        out float gap,
        out float nearestScreenDist,
        out float cellDist)
    {
        gap = 0f;
        nearestScreenDist = float.MaxValue;
        cell.z = 0;
        target.z = 0;
        cellDist = SectorManager.HexDistance(cell, target);

        if (snapshot == null || snapshot.MyUnits == null)
            return false;

        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (!IsTransportInvasionScreenUnit(ally, transporter))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(allyCell, target);
            if (dist < nearestScreenDist)
                nearestScreenDist = dist;
        }

        if (nearestScreenDist >= float.MaxValue)
            return false;

        gap = cellDist - nearestScreenDist;
        return true;
    }


    private static bool IsTransportInvasionScreenUnit(UnitManager ally, UnitManager transporter)
    {
        if (ally == null || ally == transporter || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
            return false;
        if (!ally.TryGetUnitData(out UnitData data) || data == null || data.roles == null)
            return false;

        return data.roles.Contains(UnitRole.Assalto)
            || data.roles.Contains(UnitRole.Capturador);
    }

    // -------------------------------------------------------------------------
    // Conservative tow: drop FireSupport at best safe rear-area position
    // -------------------------------------------------------------------------


}
