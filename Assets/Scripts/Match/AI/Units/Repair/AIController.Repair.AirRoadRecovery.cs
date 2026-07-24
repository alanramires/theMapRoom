using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private bool TryDecideAircraftRoadRecoveryFallback(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || unit.GetAircraftType() == AircraftType.None || paths == null || paths.Count == 0)
            return false;

        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        string bestReason = string.Empty;
        bool found = false;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;

            if (!IsAircraftRoadRecoveryCell(unit, cell, out string roadReason))
                continue;

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float distFromPlane = SectorManager.HexDistance(fromCell, cell);
            float logisticsScore = ScoreAircraftRoadRecoveryLogisticsSupport(unit, snapshot, cell, out string logisticsReason);
            float currentBonus = cell == fromCell ? 300f : 0f;
            int pathCost = GetPathStepCount(paths, cell);

            float score =
                5000f
                + logisticsScore
                + currentBonus
                - threat * ThreatWeight * 1.2f
                - distFromPlane * 55f
                - pathCost * 10f
                - cell.GetHashCode() * 0.000001f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestReason = $"{roadReason} {logisticsReason} threat={threat:F1} dist={distFromPlane:F1} path={pathCost}";
                found = true;
            }
        }

        if (!found)
            return false;

        Debug.Log($"{TL("Repair")} {unit.InstanceId} sem pista disponivel — aguarda resgate sobre estrada {bestCell} ({bestReason} score={bestScore:F0})");
        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestCell, paths);
        return true;
    }

    private bool IsAircraftRoadRecoveryCell(UnitManager unit, Vector3Int cell, out string reason)
    {
        reason = string.Empty;
        if (unit == null || boardTilemap == null || terrainDatabase == null)
        {
            reason = "contexto invalido";
            return false;
        }

        cell.z = 0;
        AirOperationTileContext context = AirOperationResolver.ResolveContext(boardTilemap, terrainDatabase, cell);
        if (context.source != AirOperationRuleSource.Structure || context.landingSurface != LandingSurface.RoadRunway)
        {
            reason = "nao e road-runway";
            return false;
        }

        AirLandingEvaluation landing = AirOperationResolver.EvaluateLanding(unit, context, SensorMovementMode.MoveuParado);
        if (!landing.allowed)
        {
            reason = $"pouso road negado: {landing.reason}";
            return false;
        }

        AirTakeoffEvaluation takeoff = AirOperationResolver.EvaluateTakeoff(unit, context, SensorMovementMode.MoveuParado);
        if (!takeoff.allowed)
        {
            reason = $"decolagem road negada: {takeoff.reason}";
            return false;
        }

        reason = $"road-runway takeoff={takeoff.plan.procedure}";
        return true;
    }

    private float ScoreAircraftRoadRecoveryLogisticsSupport(
        UnitManager aircraft,
        AIWorldSnapshot snapshot,
        Vector3Int roadCell,
        out string reason)
    {
        reason = "sem caminhao proximo";
        if (aircraft == null || snapshot == null || snapshot.MyUnits == null)
            return -600f;

        float best = -600f;
        UnitManager bestLogistics = null;
        float bestDist = float.MaxValue;
        bool bestInServiceRange = false;
        Vector3Int originalCell = aircraft.CurrentCellPosition;
        originalCell.z = 0;

        aircraft.SetCurrentCellPosition(roadCell, enforceFinalOccupancyRule: false);
        try
        {
            for (int i = 0; i < snapshot.MyUnits.Count; i++)
            {
                UnitManager logistics = snapshot.MyUnits[i];
                if (!IsPrimaryLogisticsUnit(logistics)
                    || logistics == aircraft
                    || logistics.IsDead
                    || logistics.IsEmbarked
                    || logistics.SlotIndex != aircraft.SlotIndex)
                {
                    continue;
                }

                Vector3Int logisticsCell = logistics.CurrentCellPosition;
                logisticsCell.z = 0;
                float dist = SectorManager.HexDistance(logisticsCell, roadCell);
                bool inServiceRange = IsInLogisticsServiceRange(logistics, logisticsCell, aircraft);
                float score = 1200f - dist * 140f;

                if (inServiceRange)
                    score += 3500f;
                else if (dist <= 2f)
                    score += 1800f;
                else if (dist <= 4f)
                    score += 850f;

                if (score > best)
                {
                    best = score;
                    bestLogistics = logistics;
                    bestDist = dist;
                    bestInServiceRange = inServiceRange;
                }
            }
        }
        finally
        {
            aircraft.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);
        }

        if (bestLogistics != null)
        {
            reason = $"logistica=#{bestLogistics.InstanceId} dist={bestDist:F1} inRange={bestInServiceRange}";
            return best;
        }

        return best;
    }
}
