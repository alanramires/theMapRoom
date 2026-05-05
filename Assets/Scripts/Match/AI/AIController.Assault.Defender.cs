using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private Vector3Int ResolveAssaultEscortCell(SectorObjective assigned, TeamId aiTeam, Vector3Int fromCell)
    {
        ConstructionManager target = assigned != null
            ? FindCapturableInSector(assigned.Sector, aiTeam, fromCell)
            : null;
        if (target != null)
        {
            Vector3Int tc = target.CurrentCellPosition; tc.z = 0;
            return tc;
        }

        if (assigned != null
            && SectorManager.TryGetSectorInfo(assigned.Sector, out SectorManager.SectorInfo info))
        {
            Vector3Int rc = info.RepresentativeCell; rc.z = 0;
            return rc;
        }

        return fromCell;
    }

    private int ResolveAssaultScoutZoneRadius(UnitManager unit, SectorObjective assigned)
    {
        int movement = unit != null ? Mathf.Max(0, unit.RemainingMovementPoints) : 0;
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            movement = Mathf.Max(movement, data.movement);

        return Mathf.Max(AssaultScoutZoneRadius, movement);
    }

    private List<UnitManager> CollectAssaultEscortThreats(TeamId aiTeam, Vector3Int escortCell, int scoutZoneRadius)
    {
        var threats = new List<UnitManager>();
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;

            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, escortCell) <= scoutZoneRadius)
                threats.Add(enemy);
        }
        return threats;
    }

    private void AddAssaultEscortTravelThreats(
        TeamId aiTeam,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        List<UnitManager> threats)
    {
        if (paths == null || threats == null) return;

        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            if (threats.Contains(enemy)) continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition; enemyCell.z = 0;
            if (SectorManager.HexDistance(fromCell, enemyCell) <= ThreatRadius)
            {
                threats.Add(enemy);
                continue;
            }

            foreach (Vector3Int cell in paths.Keys)
            {
                if (SectorManager.HexDistance(cell, enemyCell) <= ThreatRadius)
                {
                    threats.Add(enemy);
                    break;
                }
            }
        }
    }

    private bool TryFindAssaultEscortAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int escortCell,
        int scoutZoneRadius,
        bool defensiveContext,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        List<UnitManager> threats,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        out string reason)
    {
        bestCell = fromCell;
        bestTarget = null;
        reason = "";
        if (threats == null || threats.Count == 0)
            return false;

        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (IsReservedAssaultEscortCaptureCell(cell, snapshot.AITeam)) continue;
            if (cell != fromCell && occupied.Contains(cell)) continue;

            foreach (UnitManager enemy in threats)
            {
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, cell, defensiveContext, out string attackDecisionReason))
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition; enemyCell.z = 0;
                float targetDist = SectorManager.HexDistance(enemyCell, escortCell);
                float coverDist = SectorManager.HexDistance(cell, escortCell);
                float dpq = GetTerrainDpqPontos(cell);
                BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, enemy);
                float targetPreferenceScore = GetAssaultTargetPreferenceScore(targetPreference);
                float score =
                    targetPreferenceScore
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 1000f
                    + Mathf.Max(0, scoutZoneRadius + 1 - targetDist) * 500f
                    - coverDist * 80f
                    + dpq * 50f
                    - GetPathStepCount(paths, cell) * 5f
                    - enemy.InstanceId * 0.001f;

                if (enemyCell == escortCell)
                    score += 100000f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = enemy;
                    reason = $"score={score:F0} pref={targetPreference} hp={enemy.CurrentHP} threatDist={targetDist:F1} coverDist={coverDist:F1} dpq={dpq:F1} {attackDecisionReason}";
                }
            }
        }

        return bestTarget != null;
    }

    private Vector3Int FindAssaultEscortCoverCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int escortCell,
        int scoutZoneRadius,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        List<UnitManager> threats,
        out string evaluationLog)
    {
        Vector3Int bestCell = fromCell;
        var evaluations = new List<AssaultEscortCoverEvaluation>();
        AssaultEscortCoverEvaluation stayEval = ScoreAssaultEscortCover(unit, snapshot, fromCell, fromCell, escortCell, scoutZoneRadius, threats, paths);
        evaluations.Add(stayEval);
        float bestScore = stayEval.score;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (cell != fromCell && occupied.Contains(cell)) continue;
            if (cell == escortCell && IsReservedAssaultEscortCaptureCell(cell, snapshot.AITeam)) continue;

            AssaultEscortCoverEvaluation eval = ScoreAssaultEscortCover(unit, snapshot, fromCell, cell, escortCell, scoutZoneRadius, threats, paths);
            evaluations.Add(eval);
            float score = eval.score;
            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        if (bestCell == fromCell && stayEval.routeDistance > scoutZoneRadius)
        {
            float bestProgress = 0f;
            float bestProgressScore = float.MinValue;
            for (int i = 0; i < evaluations.Count; i++)
            {
                AssaultEscortCoverEvaluation eval = evaluations[i];
                if (eval.cell == fromCell) continue;
                if (eval.reservedCapture) continue;
                if (eval.routeProgress <= 0f && eval.hexProgress <= 0f) continue;

                float progress = eval.routeProgress > 0f ? eval.routeProgress : eval.hexProgress;
                bool better = progress > bestProgress
                    || (Mathf.Approximately(progress, bestProgress) && eval.score > bestProgressScore);
                if (!better) continue;

                bestProgress = progress;
                bestProgressScore = eval.score;
                bestCell = eval.cell;
            }
        }

        for (int i = 0; i < evaluations.Count; i++)
        {
            AssaultEscortCoverEvaluation eval = evaluations[i];
            eval.isChosen = eval.cell == bestCell;
            evaluations[i] = eval;
        }
        evaluations.Sort((a, b) =>
        {
            int scoreCompare = b.score.CompareTo(a.score);
            if (scoreCompare != 0) return scoreCompare;
            int routeCompare = a.routeDistance.CompareTo(b.routeDistance);
            if (routeCompare != 0) return routeCompare;
            return a.pathCost.CompareTo(b.pathCost);
        });
        evaluationLog = FormatAssaultEscortCoverEvaluationLog(evaluations);
        return bestCell;
    }

    private AssaultEscortCoverEvaluation ScoreAssaultEscortCover(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int cell,
        Vector3Int escortCell,
        int scoutZoneRadius,
        List<UnitManager> threats,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        float escortDist = SectorManager.HexDistance(cell, escortCell);
        float fromEscortDist = SectorManager.HexDistance(fromCell, escortCell);
        float routeDist = CalculateAssaultRouteDistance(unit, cell, escortCell);
        float fromRouteDist = CalculateAssaultRouteDistance(unit, fromCell, escortCell);
        float routeProgress = fromRouteDist - routeDist;
        float hexProgress = fromEscortDist - escortDist;
        float dpq = GetTerrainDpqPontos(cell);
        float threatPenalty = CalculateThreatLevel(cell, snapshot.AITeam);
        float pathCost = cell == fromCell ? 0f : GetPathStepCount(paths, cell);
        float scoutRingBonus = CalculateAssaultScoutRingBonus(escortDist, scoutZoneRadius);
        float zonePressureBonus = CalculateAssaultScoutZonePressure(unit, fromCell, cell, threats, out bool canCoverZoneEnemy);
        float score =
            routeProgress * 450f
            - routeDist * 180f
            + hexProgress * 80f
            + dpq * 45f
            + scoutRingBonus
            + zonePressureBonus
            - threatPenalty * ThreatWeight
            - pathCost * 8f;

        if (escortDist <= 1f) score += 250f;
        if (escortDist <= scoutZoneRadius) score += 100f;
        if (routeProgress > 0f) score += 350f;
        else if (routeProgress < 0f) score -= 600f;

        bool reservedCapture = IsReservedAssaultEscortCaptureCell(cell, snapshot.AITeam);
        if (reservedCapture)
            score -= 100000f;

        float bestEnemyDist = float.MaxValue;
        if (threats != null && threats.Count > 0)
        {
            foreach (UnitManager enemy in threats)
            {
                Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(cell, ec);
                if (d < bestEnemyDist) bestEnemyDist = d;
            }
            score -= bestEnemyDist * 120f;
        }

        return new AssaultEscortCoverEvaluation
        {
            cell = cell,
            score = score,
            routeDistance = routeDist,
            routeProgress = routeProgress,
            hexDistance = escortDist,
            hexProgress = hexProgress,
            dpq = dpq,
            threat = threatPenalty,
            pathCost = pathCost,
            nearestThreatDistance = bestEnemyDist,
            canCoverZoneEnemy = canCoverZoneEnemy,
            reservedCapture = reservedCapture,
        };
    }

    private static float CalculateAssaultScoutRingBonus(float anchorDistance, int scoutZoneRadius)
    {
        if (anchorDistance <= 0f)
            return -500f;
        int safeRadius = Mathf.Max(AssaultScoutZoneRadius, scoutZoneRadius);
        if (anchorDistance <= AssaultScoutZoneRadius)
            return 900f;
        if (anchorDistance <= safeRadius)
            return 650f - (anchorDistance - AssaultScoutZoneRadius) * 60f;
        return -Mathf.Min(600f, (anchorDistance - safeRadius) * 180f);
    }

    private float CalculateAssaultScoutZonePressure(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int cell,
        List<UnitManager> threats,
        out bool canCoverZoneEnemy)
    {
        canCoverZoneEnemy = false;
        if (threats == null || threats.Count == 0)
            return 0f;

        float nearestEnemyDist = float.MaxValue;
        foreach (UnitManager enemy in threats)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            float d = SectorManager.HexDistance(cell, ec);
            if (d < nearestEnemyDist) nearestEnemyDist = d;
            if (!canCoverZoneEnemy && CanAttackTargetFrom(fromCell, cell, unit, enemy))
                canCoverZoneEnemy = true;
        }

        float pressure = nearestEnemyDist < float.MaxValue
            ? Mathf.Max(0f, 6f - nearestEnemyDist) * 260f
            : 0f;
        if (canCoverZoneEnemy)
            pressure += 6000f;
        return pressure;
    }

    private bool IsReservedAssaultEscortCaptureCell(Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction == null || !construction.IsCapturable || construction.CapturePointsMax <= 0)
            return false;

        return construction.TeamId != aiTeam
            || construction.CurrentCapturePoints < construction.CapturePointsMax;
    }

    private float CalculateAssaultRouteDistance(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell)
    {
        fromCell.z = 0;
        targetCell.z = 0;
        if (unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && SectorManager.TryGetLandMovementDistance(fromCell, targetCell, unitData, out int unitCost))
            return unitCost;

        if (SectorManager.TryGetLandMovementDistance(fromCell, targetCell, out int fallbackCost))
            return fallbackCost;

        return SectorManager.HexDistance(fromCell, targetCell);
    }

    private static string FormatAssaultEscortCoverEvaluationLog(List<AssaultEscortCoverEvaluation> evaluations)
    {
        if (evaluations == null || evaluations.Count == 0)
            return "";

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < evaluations.Count; i++)
        {
            AssaultEscortCoverEvaluation e = evaluations[i];
            string mark = e.isChosen ? "*" : " ";
            string threatDist = e.nearestThreatDistance < float.MaxValue ? e.nearestThreatDistance.ToString("F1") : "-";
            string reserved = e.reservedCapture ? " reservadaCaptura" : "";
            string cover = e.canCoverZoneEnemy ? " cobreInimigoZona" : "";
            sb.AppendLine($"  {mark} {e.cell} total={e.score:F0} rota={e.routeDistance:F1} progRota={e.routeProgress:+0.0;-0.0;0.0} zona={e.hexDistance:F1} progHex={e.hexProgress:+0.0;-0.0;0.0} dpq={e.dpq:F1} ameaca={e.threat:F1} mov={e.pathCost:F0} inimigo={threatDist}{cover}{reserved}");
        }
        return sb.ToString();
    }

    private struct AssaultEscortCoverEvaluation
    {
        public Vector3Int cell;
        public float score;
        public float routeDistance;
        public float routeProgress;
        public float hexDistance;
        public float hexProgress;
        public float dpq;
        public float threat;
        public float pathCost;
        public float nearestThreatDistance;
        public bool canCoverZoneEnemy;
        public bool reservedCapture;
        public bool isChosen;
    }
}
