using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Pesos de scoring do capturador (Fases 1-3 — calibrar na Fase 4)
    // -------------------------------------------------------------------------

    private const float CaptureProximityBase  = 500f;
    private const float DpqWeight             = 200f;
    private const float ThreatWeight          = 50f;
    private const float AttackHexBonus        = 800f;
    private const float SafetyThresholdFactor = 0f;
    private const int   ThreatRadius          = 3;

    // -------------------------------------------------------------------------
    // Entrada principal
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideCapturerAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        PlayerAction repairAction = TryDecideRepairAction(unit, snapshot, plan);
        if (repairAction != null) return repairAction;

        SectorObjective assigned = ResolveAssignedObjective(unit, plan);

        if (assigned == null)
        {
            if (!plan.RogueUnitIds.Contains(unit.InstanceId)) return null;
            if (snapshot.EnemyHQ == null) return null;
            return DecideRogueCapturerAction(unit, snapshot);
        }

        return DecideAssignedCapturerAction(unit, snapshot, assigned);
    }


    // -------------------------------------------------------------------------
    // Capturador com plano — captura o setor atribuído, defende após conquista
    // -------------------------------------------------------------------------

    private PlayerAction DecideAssignedCapturerAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        ConstructionManager target = FindCapturableInSector(assigned.Sector, snapshot.AITeam, fromCell);

        // (C) Setor ja conquistado - modo Defensor
        if (target == null)
            return DecideCapturerDefenderAction(unit, snapshot, assigned, fromCell);

        Vector3Int targetCell = target.CurrentCellPosition; targetCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        if (TryDecideCapturerSpearheadArrival(unit, snapshot, assigned, fromCell, targetCell, paths, occupied, out PlayerAction spearheadAction))
            return spearheadAction;

        if (TryDecideCapturerPursuerCurrent(unit, snapshot, assigned, fromCell, targetCell, paths, occupied, out PlayerAction pursuerAction))
            return pursuerAction;

        if (TryFindOpportunisticCapture(unit, paths, occupied, targetCell, out Vector3Int opCell, excludeCurrentCell: true))
        {
            if (ShouldReserveOpportunisticCaptureForCloserUnit(unit, snapshot.AITeam, opCell, paths, out UnitManager reservedFor))
            {
                Debug.Log($"{TL("Oportunista")} {unit.InstanceId} cede captura oportunista @ {opCell} para {reservedFor.InstanceId}");
            }
            else
            {
                return DecideCapturerOpportunistAction(unit, snapshot, assigned, fromCell, opCell, paths);
            }
        }

        if (TryDecideCapturerExplorer(unit, snapshot, assigned, fromCell, targetCell, paths, occupied, out PlayerAction explorerAction))
            return explorerAction;

        // Scoring: avança pelo melhor hex (PontaLanca) — ataca defensor visível (Perseguidor)
        float fromDist = SectorManager.HexDistance(fromCell, targetCell);

        UnitManager defender    = HexOccupancyQuery.FindUnitAtCell(targetCell);
        MatchController mcDef   = GetMatchController();
        bool defenderVisible    = defender != null
            && defender.TeamId != snapshot.AITeam
            && (mcDef == null || mcDef.IsUnitVisibleForTeam(defender, snapshot.AITeam));

        Vector3Int bestMove       = fromCell;
        float      bestScore     = float.MinValue;
        float      bestSectorTie = float.MinValue;
        float      bestHqTie     = float.MinValue;
        bool       canAdvance    = false;

        Vector3Int attackMove       = fromCell;
        float      attackScore     = float.MinValue;
        float      attackSectorTie = float.MinValue;
        float      attackHqTie     = float.MinValue;
        bool       hasAttackHex    = false;

        bool preferDpqMove     = unit.TryGetUnitData(out UnitData moveUd) && moveUd.preferMoveOnBestDPQ;
        bool preferDpqAtBattle = unit.TryGetUnitData(out UnitData dpqUd)  && dpqUd.prioritizeDpqAtBattle;
        bool conservative      = unit.TryGetUnitData(out UnitData consUd)  && consUd.playConservative;

        var scoringLog = showAIUnitHUD ? new System.Text.StringBuilder() : null;
        scoringLog?.AppendLine($"{TL("Score")} Unit{unit.InstanceId} → {assigned.Sector} (fromDist={fromDist:F1} dpqMove={preferDpqMove} dpqBattle={preferDpqAtBattle} conservative={conservative})");

        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied.Contains(cell)) continue;
            if (SectorManager.HexDistance(cell, targetCell) >= fromDist) continue;

            float threat    = conservative ? CalculateThreatLevel(cell, snapshot.AITeam) : 0f;
            float dist      = SectorManager.HexDistance(cell, targetCell);
            float prox      = (1f / (dist + 1f)) * CaptureProximityBase;
            float dpq       = preferDpqMove ? GetTerrainDpqPontos(cell) * DpqWeight : 0f;
            float moveCost  = paths[cell].Count;
            float score     = prox - moveCost + dpq - threat * ThreatWeight;
            float sectorTie = -dist;
            float hqDist    = CalculateEnemyHqDistance(cell, snapshot, unit);
            float hqTie     = CalculateEnemyHqTieBreak(hqDist);

            string hqDistText = hqDist < float.MaxValue ? hqDist.ToString("F1") : "?";
            scoringLog?.AppendLine($"  {cell} dist={dist:F1} prox={prox:F0} mv={moveCost:F0} dpq={dpq:F0} thr={threat:F0} secTie={sectorTie:F1} hq={hqDistText} hqTie={hqTie:F1} -> {score:F0}");

            if (IsBetterScore(score, sectorTie, hqTie, bestScore, bestSectorTie, bestHqTie))
            {
                bestScore     = score;
                bestSectorTie = sectorTie;
                bestHqTie     = hqTie;
                bestMove      = cell;
                canAdvance    = true;
            }

            if (defenderVisible && score >= SafetyThresholdFactor
                && CanAttackTargetFrom(fromCell, cell, unit, defender))
            {
                float attackDpq = (preferDpqAtBattle && !preferDpqMove)
                    ? GetTerrainDpqPontos(cell) * DpqWeight
                    : 0f;
                float aScore = score + AttackHexBonus + attackDpq;
                if (IsBetterScore(aScore, sectorTie, hqTie, attackScore, attackSectorTie, attackHqTie))
                {
                    attackScore      = aScore;
                    attackSectorTie  = sectorTie;
                    attackHqTie      = hqTie;
                    attackMove       = cell;
                    hasAttackHex     = true;
                }
            }
        }

        if (hasAttackHex)
        {
            assigned.Status = ObjectiveStatus.Pursuing;
            Vector3Int defCell = defender.CurrentCellPosition; defCell.z = 0;
            Debug.Log($"{TL("Perseguidor")} {unit.InstanceId} move+ataca defensor de {assigned.Sector} via {attackMove}");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackMove,
                defender.InstanceId.ToString(), defCell, paths);
        }

        if (scoringLog != null) Debug.Log(scoringLog.ToString());
        if (!canAdvance)
        {
            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(targetCell);
            if (occupant != null && occupant.TeamId == snapshot.AITeam)
            {
                Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} aguarda {assigned.Sector} — aliado {occupant.InstanceId} ocupa o alvo");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);
            }
            return null;
        }

        SensorMovementMode advanceMode = bestMove != fromCell
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;
        var advanceBuffer = new List<PodeMirarTargetOption>();
        if (PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                advanceMode, advanceBuffer, fromCell: bestMove)
            && advanceBuffer.Count > 0)
        {
            UnitManager bestTarget    = null;
            float       bestPriority  = float.MinValue;
            foreach (PodeMirarTargetOption opt in advanceBuffer)
            {
                if (opt?.targetUnit == null) continue;
                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                if (SectorManager.HexDistance(tc, targetCell) > DefenseEnemyRange) continue;
                float priority = AttackTargetPriorityPursuer(tc, targetCell);
                if (priority > bestPriority) { bestPriority = priority; bestTarget = opt.targetUnit; }
            }
            if (bestTarget != null)
            {
                assigned.Status = ObjectiveStatus.Pursuing;
                Vector3Int btCell = bestTarget.CurrentCellPosition; btCell.z = 0;
                Debug.Log($"{TL("Perseguidor")} {unit.InstanceId} move+ataca inimigo via {bestMove} → {bestTarget.UnitDisplayName}#{bestTarget.InstanceId}");
                return BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestMove,
                    bestTarget.InstanceId.ToString(), btCell, paths);
            }
        }

        assigned.Status = ObjectiveStatus.Pursuing;
        float bestHqDist = CalculateEnemyHqDistance(bestMove, snapshot, unit);
        string bestHqText = bestHqDist < float.MaxValue ? bestHqDist.ToString("F1") : "?";
        Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} avança para {assigned.Sector} via {bestMove} (score={bestScore:F0}, secTie={bestSectorTie:F1}, hq={bestHqText}, hqTie={bestHqTie:F1})");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestMove, paths);
    }
}
