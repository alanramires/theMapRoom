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

        // Se está sobre o objetivo de captura de outro setor com capturador ativo,
        // marca o próprio hex como ocupado para forçar saída no scoring.
        TeamObjectivePlan selfPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        bool onOtherTarget = selfPlan != null && IsOtherAssignedCapturerTarget(fromCell, unit, assigned, selfPlan, snapshot.AITeam);
        if (onOtherTarget)
        {
            occupied.Add(fromCell);
            Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} — sobre objetivo alheio {fromCell}, cedendo hex");
        }

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

        // Coleta todos os inimigos visíveis dentro de fromDist do objetivo (fonte: AllActive + FoW)
        MatchController mcDef = GetMatchController();
        var nearbyEnemies = new List<UnitManager>();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mcDef != null && !mcDef.IsUnitVisibleForTeam(enemy, snapshot.AITeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, targetCell) > fromDist) continue;
            nearbyEnemies.Add(enemy);
        }
        nearbyEnemies.Sort((a, b) =>
        {
            Vector3Int ca = a.CurrentCellPosition; ca.z = 0;
            Vector3Int cb = b.CurrentCellPosition; cb.z = 0;
            return SectorManager.HexDistance(ca, targetCell)
                .CompareTo(SectorManager.HexDistance(cb, targetCell));
        });

        Vector3Int bestMove       = fromCell;
        float      bestScore     = float.MinValue;
        float      bestSectorTie = float.MinValue;
        float      bestHqTie     = float.MinValue;
        bool       canAdvance    = false;

        Vector3Int attackMove       = fromCell;
        UnitManager attackTarget    = null;
        float      attackScore     = float.MinValue;
        float      attackPriority  = float.MinValue;
        float      attackDpqTie    = float.MinValue;
        float      attackSectorTie = float.MinValue;
        float      attackHqTie     = float.MinValue;
        bool       hasAttackHex    = false;

        bool preferDpqMove     = unit.TryGetUnitData(out UnitData moveUd) && moveUd.preferMoveOnBestDPQ;
        bool preferDpqAtBattle = unit.TryGetUnitData(out UnitData dpqUd)  && dpqUd.prioritizeDpqAtBattle;
        bool conservative      = unit.TryGetUnitData(out UnitData consUd)  && consUd.playConservative;

        var scoringLog = showAIUnitHUD ? new System.Text.StringBuilder() : null;
        scoringLog?.AppendLine($"{TL("Score")} Unit{unit.InstanceId} → {assigned.Sector} (fromDist={fromDist:F1} dpqMove={preferDpqMove} dpqBattle={preferDpqAtBattle} conservative={conservative})");
        AppendMissingDpqReachabilityDiagnostics(scoringLog, unit, paths, targetCell);

        foreach (Vector3Int cell in paths.Keys)
        {
            float dpqPontos = GetTerrainDpqPontos(cell);
            if (occupied.Contains(cell))
            {
                scoringLog?.AppendLine($"  {cell} SKIP occupied dpqPts={dpqPontos:F1}");
                continue;
            }
            float dist     = SectorManager.HexDistance(cell, targetCell);
            bool  advances = dist < fromDist;
            // prioritizeDpqAtBattle: considera qualquer célula alcançável como
            // origem de tiro; ela só vence se realmente conseguir atacar.
            bool  eligibleForAttack = advances || preferDpqAtBattle;

            float threat    = conservative ? CalculateThreatLevel(cell, snapshot.AITeam) : 0f;
            float prox      = (1f / (dist + 1f)) * CaptureProximityBase;
            float dpq       = preferDpqMove ? dpqPontos * DpqWeight : 0f;
            float moveCost  = paths[cell].Count;
            float score     = prox - moveCost + dpq - threat * ThreatWeight;
            float sectorTie = -dist;
            float hqDist    = CalculateEnemyHqDistance(cell, snapshot, unit);
            float hqTie     = CalculateEnemyHqTieBreak(hqDist);

            string hqDistText = hqDist < float.MaxValue ? hqDist.ToString("F1") : "?";
            // dpqPontos sempre exibido (independente de preferDpqMove) para diagnóstico
            scoringLog?.AppendLine($"  {cell} dist={dist:F1} prox={prox:F0} mv={moveCost:F0} dpqPts={dpqPontos:F1} dpq={dpq:F0} thr={threat:F0} secTie={sectorTie:F1} hq={hqDistText} hqTie={hqTie:F1} -> {score:F0}");

            // bestMove: só células que avançam em direção ao objetivo
            if (advances && IsBetterScore(score, sectorTie, hqTie, bestScore, bestSectorTie, bestHqTie))
            {
                TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
                if (IsOtherAssignedCapturerTarget(cell, unit, assigned, capPlan, snapshot.AITeam))
                    scoringLog?.AppendLine($"    ↳ SKIP bestMove: hex de captura de outro setor");
                else
                {
                    bestScore     = score;
                    bestSectorTie = sectorTie;
                    bestHqTie     = hqTie;
                    bestMove      = cell;
                    canAdvance    = true;
                }
            }

            if (!advances && !eligibleForAttack) continue;

            if (eligibleForAttack && score >= SafetyThresholdFactor && nearbyEnemies.Count > 0)
            {
                foreach (UnitManager nearbyEnemy in nearbyEnemies)
                {
                    if (!CanAttackTargetFrom(fromCell, cell, unit, nearbyEnemy)) continue;
                    bool attackAllowed = PassesAttackDecision(unit, nearbyEnemy, cell, assigned.Status == ObjectiveStatus.Defending, out string attackDecisionReason);
                    if (!attackAllowed)
                    {
                        scoringLog?.AppendLine($"    ↳ ATK {nearbyEnemy.UnitDisplayName}#{nearbyEnemy.InstanceId} BLOCK {attackDecisionReason}");
                        continue;
                    }
                    Vector3Int enemyCell = nearbyEnemy.CurrentCellPosition; enemyCell.z = 0;
                    float targetPriority = AttackTargetPriorityPursuer(enemyCell, targetCell);
                    float objectiveBonus = enemyCell == targetCell ? 100000f : 0f;
                    float attackDpq = preferDpqAtBattle ? dpqPontos : 0f;
                    float aScore    = objectiveBonus + targetPriority * 1000f + score + AttackHexBonus;
                    bool  isNewBest = IsBetterAttackCandidate(
                        preferDpqAtBattle,
                        targetPriority,
                        attackDpq,
                        aScore,
                        sectorTie,
                        hqTie,
                        attackPriority,
                        attackDpqTie,
                        attackScore,
                        attackSectorTie,
                        attackHqTie);
                    scoringLog?.AppendLine($"    ↳ ATK {nearbyEnemy.UnitDisplayName}#{nearbyEnemy.InstanceId} pri={targetPriority:F1} objBonus={objectiveBonus:F0} atkDpqPts={attackDpq:F1} aScore={aScore:F0} {attackDecisionReason}{(isNewBest ? " ★" : "")}");
                    if (isNewBest)
                    {
                        attackScore      = aScore;
                        attackPriority   = targetPriority;
                        attackDpqTie     = attackDpq;
                        attackSectorTie  = sectorTie;
                        attackHqTie      = hqTie;
                        attackMove       = cell;
                        attackTarget     = nearbyEnemy;
                        hasAttackHex     = true;
                    }
                }
            }
        }

        if (scoringLog != null) Debug.Log(scoringLog.ToString());
        if (hasAttackHex && attackTarget != null)
        {
            assigned.Status = ObjectiveStatus.Pursuing;
            Vector3Int atCell = attackTarget.CurrentCellPosition; atCell.z = 0;
            string targetRole = atCell == targetCell ? "defensor do objetivo" : "inimigo";
            Debug.Log($"{TL("Perseguidor")} {unit.InstanceId} move+ataca {targetRole} via {attackMove} → {attackTarget.UnitDisplayName}#{attackTarget.InstanceId}");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackMove,
                attackTarget.InstanceId.ToString(), atCell, paths);
        }
        if (!canAdvance)
        {
            // Está bloqueado mas sobre o objetivo de outro capturador — tenta ceder qualquer hex livre
            if (onOtherTarget)
            {
                foreach (Vector3Int cell in paths.Keys)
                {
                    if (cell == fromCell || occupied.Contains(cell)) continue;
                    Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} cede objetivo alheio {fromCell} → {cell}");
                    return BuildMoveBatch(unit, snapshot.AITeam, fromCell, cell, paths);
                }
            }
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
                if (SectorManager.HexDistance(tc, targetCell) > fromDist) continue;
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
        UnitManager advOccupant = HexOccupancyQuery.FindUnitAtCell(targetCell);
        MatchController mcAdv   = GetMatchController();
        bool hiddenOccupant = advOccupant != null
            && advOccupant.TeamId != snapshot.AITeam
            && (mcAdv == null || !mcAdv.IsUnitVisibleForTeam(advOccupant, snapshot.AITeam));
        bool sectorInContest = HasNearbyVisibleEnemy(targetCell, snapshot.AITeam, DefenseEnemyRange);
        string advTag = hiddenOccupant ? "Explorador" : sectorInContest ? "Perseguidor" : "PontaLanca";
        Debug.Log($"{TL(advTag)} {unit.InstanceId} avança para {assigned.Sector} via {bestMove} (score={bestScore:F0}, secTie={bestSectorTie:F1}, hq={bestHqText}, hqTie={bestHqTie:F1})");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestMove, paths);
    }

    // Retorna true se `cell` é o alvo de captura de OUTRO setor com capturador ativo designado.
    // Usado para evitar que um capturador avance para o objetivo alheio e bloqueie seu designado.
    private bool IsOtherAssignedCapturerTarget(Vector3Int cell, UnitManager unit, SectorObjective ownObjective, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null) return false;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == ownObjective) continue;
            if (obj.Status == ObjectiveStatus.Defending) continue;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt == null) continue;
            Vector3Int tgtCell = tgt.CurrentCellPosition; tgtCell.z = 0;
            if (tgtCell != cell) continue;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
                UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (capturer != null) return true;
            }
        }
        return false;
    }
}
