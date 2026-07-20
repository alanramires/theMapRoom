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
            && TryGetAnySectorInfo(assigned.Sector, out SectorManager.SectorInfo info))
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

        bool preferDpq = IsCombatantFireSupport(unit)
            || (unit.TryGetUnitData(out UnitData escortUd) && escortUd != null && escortUd.prioritizeDpqAtBattle);
        float dpqWeight = preferDpq ? 2000f : 50f;

        float bestScore = float.MinValue;
        BazookaTargetPriority bestPreference = BazookaTargetPriority.Tertiary;
        int bestElite = -1;
        int candidateCells = 0;
        int reservedBlocked = 0;
        int occupiedBlocked = 0;
        int geometryBlocked = 0;
        int attackDecisionBlocked = 0;
        string lastAttackDecisionBlock = "";
        foreach (Vector3Int cell in paths.Keys)
        {
            candidateCells++;
            if (IsReservedAssaultEscortCaptureCell(cell, snapshot.AITeam))
            {
                reservedBlocked++;
                continue;
            }
            if (cell != fromCell && occupied.Contains(cell))
            {
                occupiedBlocked++;
                continue;
            }

            foreach (UnitManager enemy in threats)
            {
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy))
                {
                    geometryBlocked++;
                    continue;
                }
                if (!PassesAttackDecision(unit, enemy, cell, defensiveContext, out string attackDecisionReason))
                {
                    attackDecisionBlocked++;
                    lastAttackDecisionBlock = $"{enemy.UnitDisplayName}#{enemy.InstanceId} via {cell}: {attackDecisionReason}";
                    continue;
                }

                Vector3Int enemyCell = enemy.CurrentCellPosition; enemyCell.z = 0;
                float targetDist = SectorManager.HexDistance(enemyCell, escortCell);
                float coverDist = SectorManager.HexDistance(cell, escortCell);
                float dpq = GetTerrainDpqPontos(cell);
                BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, enemy);
                float targetPreferenceScore = GetAssaultTargetPreferenceScore(targetPreference);
                int targetElite = enemy.TryGetUnitData(out UnitData enemyUd) && enemyUd != null ? Mathf.Max(0, enemyUd.eliteLevel) : 0;
                float score =
                    targetPreferenceScore
                    + targetElite * 4000f
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 1000f
                    + Mathf.Max(0, scoutZoneRadius + 1 - targetDist) * 500f
                    - coverDist * 80f
                    + dpq * dpqWeight
                    - GetPathStepCount(paths, cell) * 5f
                    - enemy.InstanceId * 0.001f;

                if (enemyCell == escortCell)
                    score += 100000f;

                if (IsBetterAssaultTargetCandidate(
                        targetPreference, targetElite, score,
                        bestTarget, bestPreference, bestElite, bestScore))
                {
                    bestScore = score;
                    bestPreference = targetPreference;
                    bestElite = targetElite;
                    bestCell = cell;
                    bestTarget = enemy;
                    reason = $"score={score:F0} pref={targetPreference} elite={targetElite} hp={enemy.CurrentHP} threatDist={targetDist:F1} coverDist={coverDist:F1} dpq={dpq:F1} dpqW={dpqWeight:F0} preferDpq={preferDpq} {attackDecisionReason}";
                }
            }
        }

        if (bestTarget == null)
            reason = $"nenhum assalto: threats={threats.Count} cells={candidateCells}"
                + $" reservado={reservedBlocked} ocupado={occupiedBlocked} geometria={geometryBlocked}"
                + $" attackDecision={attackDecisionBlocked}"
                + (string.IsNullOrEmpty(lastAttackDecisionBlock)
                    ? ""
                    : $" ultimo=[{lastAttackDecisionBlock}]");
        return bestTarget != null;
    }

    private bool TryFindAssaultAdvanceRouteAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int escortCell,
        bool defensiveContext,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        out string reason)
    {
        bestCell = fromCell;
        bestTarget = null;
        reason = "";
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (enemies == null || enemies.Count == 0)
            return false;

        Dictionary<Vector3Int, int> realCostToEscort =
            UnitMovementPathRules.CalculateMovementCostMap(boardTilemap, unit, escortCell, 50, terrainDatabase);
        float fromRouteDist = realCostToEscort != null && realCostToEscort.TryGetValue(fromCell, out int frc)
            ? frc : CalculateAssaultRouteDistance(unit, fromCell, escortCell);
        float fromHexDist = SectorManager.HexDistance(fromCell, escortCell);

        float bestScore = float.MinValue;
        BazookaTargetPriority bestPreference = BazookaTargetPriority.Tertiary;
        int bestElite = -1;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (IsReservedAssaultEscortCaptureCell(cell, snapshot.AITeam)) continue;
            if (cell != fromCell && occupied.Contains(cell)) continue;

            float routeDist = realCostToEscort != null && realCostToEscort.TryGetValue(cell, out int rc)
                ? rc : CalculateAssaultRouteDistance(unit, cell, escortCell);
            float routeProgress = fromRouteDist - routeDist;
            float hexProgress = fromHexDist - SectorManager.HexDistance(cell, escortCell);
            if (cell != fromCell && routeProgress <= 0f && hexProgress <= 0f)
                continue;

            foreach (UnitManager enemy in enemies)
            {
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, cell, defensiveContext, out string attackDecisionReason))
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition; enemyCell.z = 0;
                float enemyDist = SectorManager.HexDistance(cell, enemyCell);
                float targetDist = SectorManager.HexDistance(enemyCell, escortCell);
                float line = CalculateLineProgressTieBreak(fromCell, escortCell, cell);
                float dpq = GetTerrainDpqPontos(cell);
                BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, enemy);
                float targetPreferenceScore = GetAssaultTargetPreferenceScore(targetPreference);
                int targetElite = enemy.TryGetUnitData(out UnitData enemyUd2) && enemyUd2 != null ? Mathf.Max(0, enemyUd2.eliteLevel) : 0;
                float score =
                    targetPreferenceScore
                    + targetElite * 4000f
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 1000f
                    + routeProgress * 1200f
                    + hexProgress * 250f
                    + line * 180f
                    + dpq * 50f
                    - routeDist * 120f
                    - targetDist * 90f
                    - enemyDist * 60f
                    - GetPathStepCount(paths, cell) * 8f
                    - enemy.InstanceId * 0.001f;

                if (IsBetterAssaultTargetCandidate(
                        targetPreference, targetElite, score,
                        bestTarget, bestPreference, bestElite, bestScore))
                {
                    bestScore = score;
                    bestPreference = targetPreference;
                    bestElite = targetElite;
                    bestCell = cell;
                    bestTarget = enemy;
                    reason = $"advanceRoute score={score:F0} pref={targetPreference} elite={targetElite} hp={enemy.CurrentHP} progRota={routeProgress:+0.0;-0.0;0.0} rota={routeDist:F1} alvoZona={targetDist:F1} tiroDist={enemyDist:F1} dpq={dpq:F1} {attackDecisionReason}";
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
        int bestCapturerDist,
        out string evaluationLog)
    {
        // Real MP cost from every cell to escortCell (road/terrain aware, budget=50).
        Dictionary<Vector3Int, int> realCostToEscort =
            UnitMovementPathRules.CalculateMovementCostMap(boardTilemap, unit, escortCell, 50, terrainDatabase);

        Vector3Int bestCell = fromCell;
        var evaluations = new List<AssaultEscortCoverEvaluation>();
        AssaultEscortCoverEvaluation stayEval = ScoreAssaultEscortCover(unit, snapshot, fromCell, fromCell, escortCell, scoutZoneRadius, threats, paths, realCostToEscort, bestCapturerDist, occupied);
        evaluations.Add(stayEval);
        float bestScore = stayEval.score;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (cell != fromCell && occupied.Contains(cell)) continue;
            if (cell == escortCell && IsReservedAssaultEscortCaptureCell(cell, snapshot.AITeam)) continue;

            AssaultEscortCoverEvaluation eval = ScoreAssaultEscortCover(unit, snapshot, fromCell, cell, escortCell, scoutZoneRadius, threats, paths, realCostToEscort, bestCapturerDist, occupied);
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
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> realCostToEscort = null,
        int bestCapturerDist = -1,
        HashSet<Vector3Int> occupied = null)
    {
        // Advance mode: um capturador já está próximo do objetivo — o escort prioriza
        // avançar até lá em vez de patrulhar o anel de zona (que ainda está longe).
        bool advanceMode = bestCapturerDist >= 0 && bestCapturerDist <= AdvancedCapturerThreshold;

        float escortDist = SectorManager.HexDistance(cell, escortCell);
        float fromEscortDist = SectorManager.HexDistance(fromCell, escortCell);
        // Use real terrain/road-aware MP cost when available; fall back to precomputed table.
        float routeDist = realCostToEscort != null && realCostToEscort.TryGetValue(cell, out int rc)
            ? rc : CalculateAssaultRouteDistance(unit, cell, escortCell);
        float fromRouteDist = realCostToEscort != null && realCostToEscort.TryGetValue(fromCell, out int frc)
            ? frc : CalculateAssaultRouteDistance(unit, fromCell, escortCell);
        float routeProgress = fromRouteDist - routeDist;
        float hexProgress = fromEscortDist - escortDist;
        float dpq = GetTerrainDpqPontos(cell);
        float threatPenalty = CalculateThreatLevel(cell, snapshot.AITeam);
        float pathCost = cell == fromCell ? 0f : GetPathStepCount(paths, cell);
        // Em advance mode, suprime o bônus negativo de anel: o escort está longe do objetivo
        // por congestionamento, não por escolha — penalizá-lo faria ele ficar parado ou recuar.
        float scoutRingBonus = advanceMode
            ? 0f
            : CalculateAssaultScoutRingBonus(escortDist, scoutZoneRadius);
        float zonePressureBonus = CalculateAssaultScoutZonePressure(unit, fromCell, cell, threats, out bool canCoverZoneEnemy);
        // Assault escorts are durable — use 40 % of the capturer ThreatWeight so they
        // don't shy away from positions that are dangerous but tactically sound.
        const float AssaultEscortThreatMult = 0.4f;
        float routeProgressWeight = advanceMode ? 900f : 450f;
        float routeDistWeight = advanceMode ? 280f : 180f;
        float score =
            routeProgress * routeProgressWeight
            - routeDist * routeDistWeight
            + hexProgress * 80f
            + dpq * 45f
            + scoutRingBonus
            + zonePressureBonus
            - threatPenalty * ThreatWeight * AssaultEscortThreatMult
            - pathCost * 8f;

        if (escortDist <= 1f) score += 250f;
        if (escortDist <= scoutZoneRadius) score += 100f;
        float routeProgressBonus = advanceMode ? 700f : 350f;
        float routeProgressPenalty = advanceMode ? 1200f : 600f;
        if (routeProgress > 0f) score += routeProgressBonus;
        else if (routeProgress < 0f) score -= routeProgressPenalty;
        // When route distance is flat (all cells same rota), still prefer cells that
        // don't retreat geometrically — avoids seemingly-random lateral/backward moves.
        else if (hexProgress < 0f) score -= 300f;

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
            // Higher weight than capturer (120) so the escort actively positions
            // toward visible threats rather than staying put.
            score -= bestEnemyDist * 300f;
        }

        // Congestionamento à frente: vizinhos com custo de rota menor (= próximo passo rumo ao
        // destino) que estão bloqueados por aliados. Ratio 0..1 penaliza células em beco.
        float congestion = ComputeForwardCongestion(cell, realCostToEscort, occupied);
        score -= congestion * ForwardCongestionWeight;

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
            advanceMode = advanceMode,
            congestion = congestion,
        };
    }

    // Ratio de vizinhos "à frente" (custo de rota menor = mais perto do destino) bloqueados
    // por aliados. 0.0 = caminho livre; 1.0 = todos os próximos passos obstruídos.
    private float ComputeForwardCongestion(
        Vector3Int cell,
        Dictionary<Vector3Int, int> costFromDest,
        HashSet<Vector3Int> occupied)
    {
        if (costFromDest == null || occupied == null) return 0f;
        if (!costFromDest.TryGetValue(cell, out int cellCost) || cellCost <= 0) return 0f;

        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, cell, neighbors);

        int forwardCount = 0;
        int blockedCount = 0;
        foreach (Vector3Int n in neighbors)
        {
            Vector3Int nc = n; nc.z = 0;
            if (!costFromDest.TryGetValue(nc, out int neighborCost)) continue;
            if (neighborCost >= cellCost) continue; // mesmo custo ou mais longe = não é frente
            forwardCount++;
            if (occupied.Contains(nc)) blockedCount++;
        }

        return forwardCount > 0 ? (float)blockedCount / forwardCount : 0f;
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
        bool shownAdvanceHeader = false;
        for (int i = 0; i < evaluations.Count; i++)
        {
            AssaultEscortCoverEvaluation e = evaluations[i];
            if (e.advanceMode && !shownAdvanceHeader)
            {
                sb.AppendLine("  [ADVANCE MODE: anel suprimido, progRota amplificado]");
                shownAdvanceHeader = true;
            }
            string mark = e.isChosen ? "*" : " ";
            string threatDist = e.nearestThreatDistance < float.MaxValue ? e.nearestThreatDistance.ToString("F1") : "-";
            string reserved = e.reservedCapture ? " reservadaCaptura" : "";
            string cover = e.canCoverZoneEnemy ? " cobreInimigoZona" : "";
            string cong = e.congestion > 0f ? $" cong={e.congestion:F2}" : "";
            sb.AppendLine($"  {mark} {e.cell} total={e.score:F0} rota={e.routeDistance:F1} progRota={e.routeProgress:+0.0;-0.0;0.0} zona={e.hexDistance:F1} progHex={e.hexProgress:+0.0;-0.0;0.0} dpq={e.dpq:F1} ameaca={e.threat:F1} mov={e.pathCost:F0} inimigo={threatDist}{cong}{cover}{reserved}");
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
        public float congestion;
        public bool canCoverZoneEnemy;
        public bool reservedCapture;
        public bool isChosen;
        public bool advanceMode;
    }
}
