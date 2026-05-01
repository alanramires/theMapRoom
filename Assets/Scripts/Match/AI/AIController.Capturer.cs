using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
    // Capturador Rogue — captura HQ e construções no caminho, engaja para abrir passagem
    // -------------------------------------------------------------------------

    private PlayerAction DecideRogueCapturerAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        Vector3Int from   = unit.CurrentCellPosition; from.z = 0;
        Vector3Int target = snapshot.EnemyHQ.CurrentCellPosition; target.z = 0;

        // Rogue abre caminho: ataca diretamente — não delega ao HexEvaluator (que evitaria o combate)
        if (HasAttackTargetAtCurrentPos(unit))
        {
            var stayTargets = new List<PodeMirarTargetOption>();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, stayTargets);
            UnitManager stayBest = PickBestRogueTarget(stayTargets, snapshot.AITeam);
            if (stayBest != null)
            {
                Vector3Int stCell = stayBest.CurrentCellPosition; stCell.z = 0;
                Debug.Log($"{TL("Rogue")} {unit.InstanceId} ataca {stayBest.UnitDisplayName}#{stayBest.InstanceId} da posição atual");
                return BuildAttackBatch(unit, snapshot.AITeam, from, from,
                    stayBest.InstanceId.ToString(), stCell);
            }
        }

        if (HasEnemyInEngageRadius(unit, from, snapshot.AITeam))
        {
            Dictionary<Vector3Int, List<Vector3Int>> engagePaths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
            HashSet<Vector3Int> engageOccupied = BuildOccupied(unit);

            // Captura oportunista tem prioridade sobre o combate: prédio disponível é mais
            // valioso do que eliminar um inimigo que não bloqueia o caminho.
            if (TryFindOpportunisticCapture(unit, engagePaths, engageOccupied, target, out Vector3Int engageOpCell))
            {
                Debug.Log($"{TL("Rogue")} {unit.InstanceId} captura oportunista (inimigos no raio) @ {engageOpCell}");
                return BuildCaptureBatch(unit, snapshot.AITeam, from, engageOpCell, engagePaths);
            }

            // Sem captura disponível → abre caminho por combate
            var engageBuffer = new List<PodeMirarTargetOption>();
            foreach (Vector3Int cell in engagePaths.Keys)
            {
                if (engageOccupied.Contains(cell)) continue;
                engageBuffer.Clear();
                PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuAndando, engageBuffer, fromCell: cell);
                UnitManager candidate = PickBestRogueTarget(engageBuffer, snapshot.AITeam);
                if (candidate != null)
                {
                    Vector3Int btCell = candidate.CurrentCellPosition; btCell.z = 0;
                    Debug.Log($"{TL("Rogue")} {unit.InstanceId} move+ataca {candidate.UnitDisplayName}#{candidate.InstanceId} via {cell}");
                    return BuildAttackBatch(unit, snapshot.AITeam, from, cell,
                        candidate.InstanceId.ToString(), btCell, engagePaths);
                }
            }

            return null; // inimigos próximos, sem captura nem ataque → HexEvaluator
        }

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, from, from);

        // HQ alcançável → captura ou entra
        if (paths.ContainsKey(target) && !occupied.Contains(target))
        {
            if (SimulateCaptureSensor(unit, target, out _))
                return BuildCaptureBatch(unit, snapshot.AITeam, from, target, paths);
            return BuildMoveBatch(unit, snapshot.AITeam, from, target, paths);
        }

        // Captura oportunista: qualquer prédio capturável no caminho ao HQ
        if (TryFindOpportunisticCapture(unit, paths, occupied, target, out Vector3Int opCell))
        {
            Debug.Log($"{TL("Rogue")} {unit.InstanceId} captura oportunista @ {opCell}");
            return BuildCaptureBatch(unit, snapshot.AITeam, from, opCell, paths);
        }

        // FoW passinho: HQ tem ocupante invisível → sobe no DPQ mais elevado adjacente
        {
            UnitManager hqOccupant = HexOccupancyQuery.FindUnitAtCell(target);
            if (hqOccupant != null && hqOccupant.TeamId != snapshot.AITeam)
            {
                MatchController mc = GetMatchController();
                if (mc == null || !mc.IsUnitVisibleForTeam(hqOccupant, snapshot.AITeam))
                {
                    if (TryFindBestLoSCell(unit, paths, occupied, target, out Vector3Int dpqCell))
                    {
                        Debug.Log($"{TL("FoW")} {unit.InstanceId} DPQ para revelar HQ via {dpqCell} (ev={GetTerrainEv(dpqCell):F0})");
                        return BuildMoveBatch(unit, snapshot.AITeam, from, dpqCell, paths);
                    }
                }
            }
        }

        // Avança para o hex mais próximo do HQ
        Vector3Int best     = from;
        float      bestDist = SectorManager.HexDistance(from, target);
        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied.Contains(cell)) continue;
            float dist = SectorManager.HexDistance(cell, target);
            if (IsBetterRogueAdvance(from, target, cell, dist, best, bestDist))
            {
                bestDist = dist;
                best = cell;
            }
        }

        Debug.Log($"{TL("Rogue")} {unit.InstanceId} marcha para HQ inimigo via {best}");
        return BuildMoveBatch(unit, snapshot.AITeam, from, best, paths);
    }

    // -------------------------------------------------------------------------
    // Capturador com plano — captura o setor atribuído, defende após conquista
    // -------------------------------------------------------------------------

    private PlayerAction DecideAssignedCapturerAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        ConstructionManager target = FindCapturableInSector(assigned.Sector, snapshot.AITeam, fromCell);

        // (C) Setor já conquistado: defende se houver inimigos, aguarda caso contrário
        if (target == null)
        {
            assigned.Status = ObjectiveStatus.Complete;
            if (HasEnemyInEngageRadius(unit, fromCell, snapshot.AITeam)) return null; // HexEvaluator defende
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);         // aguarda reatribuição
        }

        Vector3Int targetCell = target.CurrentCellPosition; targetCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // Já está no hex alvo
        if (fromCell == targetCell)
        {
            if (SimulateCaptureSensor(unit, targetCell, out _))
            {
                assigned.Status = ObjectiveStatus.Capturing;
                Debug.Log($"{TL("Obj")} {unit.InstanceId} captura {assigned.Sector} @ {targetCell}");
                return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, targetCell);
            }
            assigned.Status = ObjectiveStatus.Complete;
            return null;
        }

        // Alvo alcançável neste turno
        if (paths.ContainsKey(targetCell) && !occupied.Contains(targetCell))
        {
            if (SimulateCaptureSensor(unit, targetCell, out _))
            {
                assigned.Status = ObjectiveStatus.Capturing;
                Debug.Log($"{TL("Obj")} {unit.InstanceId} alcança e captura {assigned.Sector} @ {targetCell}");
                return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, targetCell, paths);
            }
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, targetCell, paths);
        }

        // Auto-defesa: inimigo em alcance de tiro direto → ataca sem delegar ao HexEvaluator.
        // HexEvaluator tem viés de avanço para o próximo objetivo e ignora ameaças ao setor atual.
        if (HasAttackTargetAtCurrentPos(unit))
        {
            var stayTargets = new List<PodeMirarTargetOption>();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, stayTargets);
            UnitManager bestStayTarget = null;
            float       bestPriority   = float.MinValue;
            foreach (PodeMirarTargetOption opt in stayTargets)
            {
                if (opt?.targetUnit == null) continue;
                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                float priority = AttackTargetPriority(tc, targetCell);
                if (priority > bestPriority) { bestPriority = priority; bestStayTarget = opt.targetUnit; }
            }
            if (bestStayTarget != null)
            {
                // Reposicionamento antes de atacar se:
                // (a) prioritizeDpqAtBattle — prefere terreno elevado; ou
                // (b) está fisicamente sobre o alvo de outro capturador designado — deve liberar o hex.
                TeamObjectivePlan activePlanDef = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
                bool mustVacate = activePlanDef != null && IsBlockingCaptureTarget(unit, activePlanDef, snapshot.AITeam);
                bool dpqPref    = unit.TryGetUnitData(out UnitData udDef) && udDef.prioritizeDpqAtBattle;

                if (mustVacate || dpqPref)
                {
                    Vector3Int ec = bestStayTarget.CurrentCellPosition; ec.z = 0;
                    if (TryFindBestLoSCell(unit, paths, occupied, ec, out Vector3Int combatCell)
                        && combatCell != fromCell)
                    {
                        string reason = mustVacate && dpqPref ? "vacate+DPQ" : mustVacate ? "vacate" : "DPQ";
                        Debug.Log($"{TL("Obj")} {unit.InstanceId} [{reason}] reposiciona @ {combatCell} — ataca {bestStayTarget.UnitDisplayName}#{bestStayTarget.InstanceId} @ {ec}");
                        return BuildAttackBatch(unit, snapshot.AITeam, fromCell, combatCell,
                            bestStayTarget.InstanceId.ToString(), ec);
                    }
                }

                Vector3Int stCell = bestStayTarget.CurrentCellPosition; stCell.z = 0;
                Debug.Log($"{TL("Obj")} {unit.InstanceId} defende {assigned.Sector} — ataca {bestStayTarget.UnitDisplayName}#{bestStayTarget.InstanceId} @ {stCell}");
                return BuildAttackBatch(unit, snapshot.AITeam, fromCell, fromCell,
                    bestStayTarget.InstanceId.ToString(), stCell);
            }
            return null; // sensor retornou true mas CollectTargets não encontrou alvo válido
        }

        // (B) Captura oportunista: prédio capturável no caminho ao objetivo atribuído
        // excludeCurrentCell=true: após handoff, unit ainda está no setor anterior — não voltar a capturá-lo
        // Não captura oportunisticamente se outro capturador já foi designado para aquele setor —
        // evita roubar o alvo de uma unidade que age depois por ter objetivo de menor prioridade.
        if (TryFindOpportunisticCapture(unit, paths, occupied, targetCell, out Vector3Int opCell, excludeCurrentCell: true))
        {
            bool hasDesignated = false;
            ConstructionManager opBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, opCell);
            TeamObjectivePlan activePlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
            if (opBldg != null && activePlan != null)
            {
                SectorObjective opObj = activePlan.GetObjectiveForSector(opBldg.Sector);
                if (opObj != null)
                    foreach (SlotNeed slot in opObj.Slots)
                    {
                        if (!slot.Filled || slot.AssignedUnitId == unit.InstanceId) continue;
                        UnitManager designated = null;
                        foreach (UnitManager u in UnitManager.AllActive)
                            if (u.InstanceId == slot.AssignedUnitId) { designated = u; break; }
                        if (designated != null)
                        {
                            Dictionary<Vector3Int, List<Vector3Int>> dPaths =
                                UnitMovementPathRules.CalcularCaminhosValidos(
                                    boardTilemap, designated,
                                    Mathf.Max(0, designated.RemainingMovementPoints),
                                    terrainDatabase);
                            if (dPaths != null && dPaths.ContainsKey(opCell))
                                hasDesignated = true;
                        }
                        break;
                    }
            }
            if (!hasDesignated)
            {
                Debug.Log($"{TL("Obj")} {unit.InstanceId} captura oportunista @ {opCell} → {assigned.Sector}");
                return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, opCell, paths);
            }
        }

        // (D) FoW passinho: ocupante invisível no alvo → sobe no DPQ mais elevado adjacente
        {
            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(targetCell);
            if (occupant != null && occupant.TeamId != snapshot.AITeam)
            {
                MatchController mc = GetMatchController();
                if (mc == null || !mc.IsUnitVisibleForTeam(occupant, snapshot.AITeam))
                {
                    if (TryFindBestLoSCell(unit, paths, occupied, targetCell, out Vector3Int dpqCell))
                    {
                        assigned.Status = ObjectiveStatus.Pursuing;
                        Debug.Log($"{TL("FoW")} {unit.InstanceId} DPQ para revelar {assigned.Sector} via {dpqCell} (ev={GetTerrainEv(dpqCell):F0})");
                        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, dpqCell, paths);
                    }
                }
            }
        }

        // Scoring: escolhe o melhor hex de avanço por captureProximity + DPQ - ameaça.
        // Fase 3: se defensor visível ocupa o alvo, prioriza hex de onde é possível atacá-lo.
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
            if (SectorManager.HexDistance(cell, targetCell) >= fromDist) continue; // só avanço

            float threat    = conservative ? CalculateThreatLevel(cell, snapshot.AITeam) : 0f;
            float dist      = SectorManager.HexDistance(cell, targetCell);
            float prox      = (1f / (dist + 1f)) * CaptureProximityBase;
            float dpq       = preferDpqMove ? GetTerrainDpqPontos(cell) * DpqWeight : 0f;
            // moveCost: custo de movimento da unidade até este candidato.
            // Desempata células equidistantes do alvo: prefere a que está na direção direta.
            float moveCost  = paths[cell].Count;
            float score     = prox - moveCost + dpq - threat * ThreatWeight;
            float sectorTie = -dist; // desempate 1: prefere célula mais próxima do setor
            float hqDist    = CalculateEnemyHqDistance(cell, snapshot, unit);
            float hqTie     = CalculateEnemyHqTieBreak(hqDist); // desempate 2: prefere mais próximo ao HQ

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
                // prioritizeDpqAtBattle: favorece hex de ataque com DPQ mais alto.
                // Evita double-count quando preferDpqMove já incluiu DPQ no score base.
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
            Debug.Log($"{TL("Obj")} {unit.InstanceId} move+ataca defensor de {assigned.Sector} via {attackMove}");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackMove,
                defender.InstanceId.ToString(), defCell, paths);
        }

        if (scoringLog != null) Debug.Log(scoringLog.ToString());
        if (!canAdvance)
        {
            // Alvo ocupado por aliado: aguarda adjacente em vez de ceder ao HexEvaluator
            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(targetCell);
            if (occupant != null && occupant.TeamId == snapshot.AITeam)
            {
                Debug.Log($"{TL("Obj")} {unit.InstanceId} aguarda {assigned.Sector} — aliado {occupant.InstanceId} ocupa o alvo");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);
            }
            return null;
        }

        // Após escolher bestMove, tenta atacar qualquer inimigo alcançável a partir dele.
        // Prioriza defensor do prédio alvo; aceita qualquer alvo no caminho.
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
                float priority = AttackTargetPriority(tc, targetCell);
                if (priority > bestPriority) { bestPriority = priority; bestTarget = opt.targetUnit; }
            }
            if (bestTarget != null)
            {
                assigned.Status = ObjectiveStatus.Pursuing;
                Vector3Int btCell = bestTarget.CurrentCellPosition; btCell.z = 0;
                Debug.Log($"{TL("Obj")} {unit.InstanceId} move+ataca inimigo via {bestMove} → {bestTarget.UnitDisplayName}#{bestTarget.InstanceId}");
                return BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestMove,
                    bestTarget.InstanceId.ToString(), btCell, paths);
            }
        }

        assigned.Status = ObjectiveStatus.Pursuing;
        float bestHqDist = CalculateEnemyHqDistance(bestMove, snapshot, unit);
        string bestHqText = bestHqDist < float.MaxValue ? bestHqDist.ToString("F1") : "?";
        Debug.Log($"{TL("Obj")} {unit.InstanceId} avança para {assigned.Sector} via {bestMove} (score={bestScore:F0}, secTie={bestSectorTie:F1}, hq={bestHqText}, hqTie={bestHqTie:F1})");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestMove, paths);
    }

    // -------------------------------------------------------------------------
    // Helpers de combate
    // -------------------------------------------------------------------------

    // 3 = defensor do prédio objetivo  2 = inimigo em qualquer construção  1 = terreno aberto
    private float AttackTargetPriority(Vector3Int targetUnitCell, Vector3Int captureTargetCell)
    {
        if (targetUnitCell == captureTargetCell) return 3f;
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetUnitCell);
        return bldg != null ? 2f : 1f;
    }

    private bool HasAttackTargetAtCurrentPos(UnitManager unit)
    {
        var targets = new List<PodeMirarTargetOption>();
        return PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
            SensorMovementMode.MoveuParado, targets) && targets.Count > 0;
    }

    // Escolhe o melhor alvo para o rogue: capturadores ativos em prédios têm prioridade máxima,
    // desempate por HP mais baixo (mais fácil de eliminar).
    private UnitManager PickBestRogueTarget(List<PodeMirarTargetOption> options, TeamId aiTeam)
    {
        UnitManager best = null;
        float bestPriority = float.MinValue;
        foreach (PodeMirarTargetOption opt in options)
        {
            if (opt?.targetUnit == null || opt.targetUnit.TeamId == aiTeam) continue;
            float priority = 10f - opt.targetUnit.CurrentHP;

            // Inimigo em prédio capturável → ameaça direta ao objetivo, prioridade máxima
            Vector3Int ec = opt.targetUnit.CurrentCellPosition; ec.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, ec);
            if (bldg != null && bldg.IsCapturable
                && !(bldg.TeamId == aiTeam && bldg.CurrentCapturePoints >= bldg.CapturePointsMax))
                priority += 1000f;

            if (priority > bestPriority) { bestPriority = priority; best = opt.targetUnit; }
        }
        return best;
    }

    private static bool HasEnemyInEngageRadius(UnitManager unit, Vector3Int fromCell, TeamId aiTeam)
    {
        int radius = Mathf.Max(0, unit.RemainingMovementPoints) + 1;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (Vector3Int.Distance(fromCell, ec) <= radius) return true;
        }
        return false;
    }

    // Inimigo visível dentro do raio de movimento+1 que está mais perto do alvo do que a unidade
    // (bloqueador de rota) → deve-se lutar, não rotear pelo QG.
    private static bool HasEnemyBlockingPath(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell, TeamId aiTeam)
    {
        float unitDist   = Vector3Int.Distance(fromCell, targetCell);
        int engageRadius = Mathf.Max(0, unit.RemainingMovementPoints) + 1;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (Vector3Int.Distance(fromCell, ec) > engageRadius) continue;
            if (Vector3Int.Distance(ec, targetCell) < unitDist) return true;
        }
        return false;
    }

    // Inimigo visível no hex alvo ou num dos seus adjacentes (ameaça direta ao objetivo).
    private bool HasEnemyNearCell(Vector3Int cell, TeamId aiTeam)
    {
        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, cell, neighbors);
        var nearCells = new HashSet<Vector3Int>(neighbors) { cell };

        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (nearCells.Contains(ec)) return true;
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Helpers de captura
    // -------------------------------------------------------------------------

    // Captura oportunista: primeiro prédio capturável alcançável, excluindo excludeCell.
    // excludeCurrentCell=true: ignora fromCell (usado após handoff — não re-capturar o setor abandonado).
    private bool TryFindOpportunisticCapture(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int excludeCell,
        out Vector3Int captureCell,
        bool excludeCurrentCell = false)
    {
        captureCell = Vector3Int.zero;
        Vector3Int currentCell = unit.CurrentCellPosition; currentCell.z = 0;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied.Contains(cell) || cell == excludeCell) continue;
            if (excludeCurrentCell && cell == currentCell) continue;
            if (!SimulateCaptureSensor(unit, cell, out _)) continue;
            captureCell = cell;
            return true;
        }
        return false;
    }

    // FoW passinho: entre os hexes adjacentes ao alvo alcançáveis, prefere maior DPQ (prioritizeDpqAtBattle=true) ou maior EV.
    private bool TryFindBestLoSCell(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int targetCell,
        out Vector3Int bestCell)
    {
        bool preferDpq = unit.TryGetUnitData(out UnitData ud) && ud.prioritizeDpqAtBattle;

        bestCell = Vector3Int.zero;
        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, targetCell, neighbors);

        bool found = false;
        float bestScore = float.MinValue;
        foreach (Vector3Int n in neighbors)
        {
            Vector3Int nc = n; nc.z = 0;
            if (!paths.ContainsKey(nc) || occupied.Contains(nc)) continue;
            float score = preferDpq ? GetTerrainDpqPontos(nc) : GetTerrainEv(nc);
            if (!found || score > bestScore) { bestScore = score; bestCell = nc; found = true; }
        }
        return found;
    }

    // Fase 2: ameaça local por inimigos visíveis dentro do raio ThreatRadius.
    private float CalculateThreatLevel(Vector3Int cell, TeamId aiTeam)
    {
        float threat = 0f;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            float dist = Vector3Int.Distance(cell, ec);
            if (dist <= ThreatRadius)
                threat += (ThreatRadius - dist + 1f) * 10f;
        }
        return threat;
    }

    // Fase 3: verifica se a unidade consegue atacar o alvo a partir de toCell.
    private bool CanAttackTargetFrom(Vector3Int fromCell, Vector3Int toCell,
        UnitManager unit, UnitManager target)
    {
        SensorMovementMode mode = toCell != fromCell
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;

        var targets = new List<PodeMirarTargetOption>();
        bool hasAny = PodeMirarSensor.CollectTargets(
            unit, boardTilemap, terrainDatabase, mode, targets, fromCell: toCell);

        if (!hasAny) return false;
        foreach (PodeMirarTargetOption opt in targets)
            if (opt?.targetUnit == target) return true;
        return false;
    }

    private float GetTerrainEv(Vector3Int cell)
    {
        if (boardTilemap == null || terrainDatabase == null) return 0f;
        cell.z = 0;
        TileBase tile = boardTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data) && data != null)
            return data.ev;
        return 0f;
    }

    private float GetTerrainDpqPontos(Vector3Int cell)
    {
        if (boardTilemap == null || terrainDatabase == null) return 0f;
        cell.z = 0;
        TileBase tile = boardTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data) && data?.dpqData != null)
            return data.dpqData.Pontos;
        return 0f;
    }

    private static bool IsBetterScore(float score, float sectorTie, float hqTie,
                                       float bestScore, float bestSectorTie, float bestHqTie)
    {
        const float epsilon = 0.001f;
        if (score > bestScore + epsilon) return true;
        if (Mathf.Abs(score - bestScore) > epsilon) return false;
        // desempate 1: setor atribuído (menor distância ao alvo do setor)
        if (sectorTie > bestSectorTie + epsilon) return true;
        if (Mathf.Abs(sectorTie - bestSectorTie) > epsilon) return false;
        // desempate 2: HQ inimigo
        return hqTie > bestHqTie + epsilon;
    }

    private static float CalculateEnemyHqDistance(Vector3Int cell, AIWorldSnapshot snapshot, UnitManager unit)
    {
        if (snapshot == null || snapshot.EnemyHQ == null)
            return float.MaxValue;

        Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition;
        hq.z = 0;
        cell.z = 0;

        if (unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && SectorManager.TryGetLandMovementDistance(cell, hq, unitData, out int movementCost))
        {
            return movementCost;
        }

        if (SectorManager.TryGetLandMovementDistance(cell, hq, out int fallbackCost))
            return fallbackCost;

        return SectorManager.HexDistance(cell, hq);
    }

    private static float CalculateEnemyHqTieBreak(float hqDistance)
    {
        return hqDistance < float.MaxValue ? -hqDistance : 0f;
    }

    private static bool IsBetterRogueAdvance(Vector3Int from, Vector3Int target, Vector3Int candidate, float candidateHexDist, Vector3Int currentBest, float bestHexDist)
    {
        const float epsilon = 0.001f;
        if (candidateHexDist < bestHexDist - epsilon)
            return true;
        if (candidateHexDist > bestHexDist + epsilon)
            return false;

        float candidateLine = CalculateLineProgressTieBreak(from, target, candidate);
        float bestLine = CalculateLineProgressTieBreak(from, target, currentBest);
        if (candidateLine > bestLine + epsilon)
            return true;
        if (candidateLine < bestLine - epsilon)
            return false;

        return Vector3Int.Distance(candidate, target) < Vector3Int.Distance(currentBest, target) - epsilon;
    }

    private static float CalculateLineProgressTieBreak(Vector3Int from, Vector3Int target, Vector3Int candidate)
    {
        Vector2 origin = new Vector2(from.x, from.y);
        Vector2 goal = new Vector2(target.x, target.y);
        Vector2 point = new Vector2(candidate.x, candidate.y);
        Vector2 direction = goal - origin;
        float lengthSq = direction.sqrMagnitude;
        if (lengthSq <= 0.001f)
            return 0f;

        Vector2 advanced = point - origin;
        float projection = Vector2.Dot(advanced, direction.normalized);
        float lateral = Mathf.Abs(direction.x * advanced.y - direction.y * advanced.x) / Mathf.Sqrt(lengthSq);
        return projection - lateral * 0.25f;
    }

    private static SectorObjective ResolveAssignedObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && slot.AssignedUnitId == unit.InstanceId) return obj;
        return null;
    }

    // Replica PodeCapturarSensor em um hex simulado sem mover a unidade.
    private bool SimulateCaptureSensor(UnitManager unit, Vector3Int simulatedCell,
        out ConstructionManager targetConstruction)
    {
        targetConstruction = null;
        if (!unit.TryGetUnitData(out UnitData data)) return false;
        if (data.roles == null || !data.roles.Contains(UnitRole.Capturador)) return false;
        if (unit.TeamId == TeamId.Neutral) return false;

        ConstructionManager c = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, simulatedCell);
        if (c == null || !c.IsCapturable || c.CapturePointsMax <= 0) return false;
        if (c.TeamId == unit.TeamId && c.CurrentCapturePoints >= c.CapturePointsMax) return false;

        targetConstruction = c;
        return true;
    }

    private static ConstructionManager FindCapturableInSector(ConstructionSector sector, TeamId aiTeam, Vector3Int? unitPos = null)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.Sector != sector || !c.IsCapturable) continue;
            if (c.TeamId == aiTeam && c.CurrentCapturePoints >= c.CapturePointsMax) continue;

            if (unitPos == null) return c;

            Vector3Int tc = c.CurrentCellPosition; tc.z = 0;
            float dist = Vector3Int.Distance(unitPos.Value, tc);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }

        return best;
    }

    private static List<UnitManager> GetAvailableCapturers(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (data.roles != null && data.roles.Contains(UnitRole.Capturador))
                list.Add(u);
        }
        return list;
    }
}
