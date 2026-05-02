using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Modo de reparo
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideRepairAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        UpdateRepairState(unit, plan);
        return unit.IsUnderRepair ? DecideUnderRepairAction(unit, snapshot) : null;
    }

    private void UpdateRepairState(UnitManager unit, TeamObjectivePlan plan)
    {
        if (!unit.TryGetUnitData(out UnitData data)) return;

        bool anyTrigger = EvaluateRepairTriggers(unit, data);

        if (!unit.IsUnderRepair && anyTrigger)
        {
            unit.SetIsUnderRepair(true);
            // Libera o slot do objetivo para reatribuição imediata
            if (plan != null)
            {
                foreach (SectorObjective obj in plan.Objectives)
                    foreach (SlotNeed slot in obj.Slots)
                        if (slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                        {
                            slot.Filled = false;
                            slot.AssignedUnitId = -1;
                            break;
                        }
                plan.RogueUnitIds.Remove(unit.InstanceId);
            }
            unit.SetAIMaintenanceActive(true);
            Debug.Log($"{TL("Repair")} {unit.InstanceId} entra em reparo " +
                      $"hp={unit.CurrentHP} fuel={unit.CurrentFuel}/{unit.GetMaxFuel()} " +
                      $"ammo={unit.CurrentAmmo}/{unit.GetMaxAmmo()}");
        }
        else if (unit.IsUnderRepair && !anyTrigger && unit.CurrentHP >= data.repairRecoverHpAbove)
        {
            unit.SetIsUnderRepair(false);
            unit.SetAIMaintenanceActive(false);
            Debug.Log($"{TL("Repair")} {unit.InstanceId} saiu do reparo hp={unit.CurrentHP}");
        }
    }

    private static bool EvaluateRepairTriggers(UnitManager unit, UnitData data)
    {
        if (data.repairTriggerHpBelow > 0 && unit.CurrentHP <= data.repairTriggerHpBelow)
            return true;
        if (data.repairTriggerAutonomyPct > 0 &&
            unit.CurrentFuel * 100f / unit.GetMaxFuel() <= data.repairTriggerAutonomyPct)
            return true;
        if (data.repairTriggerAmmoEnabled &&
            unit.CurrentAmmo * 100f / unit.GetMaxAmmo() <= data.repairTriggerAmmoPct)
            return true;
        return false;
    }

    private PlayerAction DecideUnderRepairAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        TeamId aiTeam = snapshot.AITeam;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);

        // 1. Prédio conquistado: verifica segurança e presença de substituto
        ConstructionManager currentBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);
        if (currentBldg != null && currentBldg.IsCapturable
            && currentBldg.TeamId == aiTeam && currentBldg.CurrentCapturePoints >= currentBldg.CapturePointsMax)
        {
            bool safe = !HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange);
            if (safe)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} aguarda reparo em {fromCell} (conquistado, setor seguro)");
                return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
            }

            // Com ameaça: só sai se houver aliado saudável próximo que pode substituir
            bool hasReplacement = false;
            foreach (UnitManager ally in UnitManager.AllActive)
            {
                if (ally == unit || ally.TeamId != aiTeam || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair) continue;
                Vector3Int ac = ally.CurrentCellPosition; ac.z = 0;
                if (SectorManager.HexDistance(ac, fromCell) <= DefenseEnemyRange) { hasReplacement = true; break; }
            }
            if (!hasReplacement)
            {
                // Sem substituto: defende o prédio enquanto aguarda reparo
                if (HasAttackTargetAtCurrentPos(unit))
                {
                    var defBuf = new List<PodeMirarTargetOption>();
                    PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                        SensorMovementMode.MoveuParado, defBuf);
                    UnitManager defTarget = null; float defPri = float.MinValue;
                    foreach (PodeMirarTargetOption opt in defBuf)
                    {
                        if (opt?.targetUnit == null) continue;
                        Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                        float p = AttackTargetPriority(tc, fromCell);
                        if (p > defPri) { defPri = p; defTarget = opt.targetUnit; }
                    }
                    if (defTarget != null)
                    {
                        Vector3Int dtc = defTarget.CurrentCellPosition; dtc.z = 0;
                        Debug.Log($"{TL("Repair")} {unit.InstanceId} segura {fromCell} sem substituto — ataca {defTarget.UnitDisplayName}#{defTarget.InstanceId}");
                        return BuildAttackBatch(unit, aiTeam, fromCell, fromCell, defTarget.InstanceId.ToString(), dtc);
                    }
                }
                Debug.Log($"{TL("Repair")} {unit.InstanceId} segura {fromCell} sem substituto");
                return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
            }
        }

        // 2. Fusão: libera o hex e recupera a unidade ao mesmo tempo
        // Scoring: candidato em repCell defensivo (+20) > em prédio (+10) > campo (0); desempate por HP combinado
        if (unit.TryGetUnitData(out UnitData fuseData) && fuseData.fuseWhileInRepair)
        {
            var defensiveRepCells = new HashSet<Vector3Int>();
            TeamObjectivePlan fusePlan = ObjectiveManager.GetPlanForTeam(aiTeam);
            if (fusePlan != null)
                foreach (SectorObjective obj in fusePlan.Objectives)
                {
                    if (obj.Status != ObjectiveStatus.Defending) continue;
                    if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo di)) continue;
                    Vector3Int rc = di.RepresentativeCell; rc.z = 0;
                    defensiveRepCells.Add(rc);
                }

            int totalMovement = Mathf.Max(0, unit.RemainingMovementPoints);
            var fuseOptions = new List<PodeFundirOption>();
            Vector3Int bestFuseCell = Vector3Int.zero;
            PodeFundirOption bestFuseOpt = null;
            float bestFuseScore = float.MinValue;

            foreach (Vector3Int cell in paths.Keys)
            {
                if (occupied.Contains(cell)) continue;

                List<Vector3Int> pathToCell = paths[cell];
                int costToCell = pathToCell != null && pathToCell.Count > 0
                    ? Mathf.Max(0, UnitMovementPathRules.CalculateAutonomyCostForPath(
                        boardTilemap, unit, pathToCell, terrainDatabase,
                        applyOperationalAutonomyModifier: false))
                    : 0;
                int remainingAfterMove = Mathf.Max(0, totalMovement - costToCell);

                fuseOptions.Clear();
                bool canFuse = PodeFundirSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
                    remainingAfterMove, fuseOptions, out _, fromCell: cell);
                Debug.Log($"[Repair] fusão de {cell} mov={remainingAfterMove} canFuse={canFuse} opts={fuseOptions.Count}");
                if (!canFuse) continue;

                foreach (PodeFundirOption opt in fuseOptions)
                {
                    if (opt?.candidateUnit == null) continue;
                    if (opt.candidateUnit.CurrentHP + unit.CurrentHP > 10)
                    {
                        Debug.Log($"[Repair] skip fusão {opt.candidateUnit.InstanceId} hp={unit.CurrentHP}+{opt.candidateUnit.CurrentHP}>10");
                        continue;
                    }
                    Vector3Int cc = opt.candidateUnit.CurrentCellPosition; cc.z = 0;
                    float score = 0f;
                    if (defensiveRepCells.Contains(cc)) score += 20f;
                    else
                    {
                        ConstructionManager candBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cc);
                        if (candBldg != null && candBldg.IsCapturable) score += 10f;
                    }
                    score += opt.candidateUnit.CurrentHP + unit.CurrentHP;

                    if (score > bestFuseScore) { bestFuseScore = score; bestFuseOpt = opt; bestFuseCell = cell; }
                }
            }

            if (bestFuseOpt != null)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} fusão oportunista com " +
                          $"{bestFuseOpt.candidateUnit.InstanceId} hp={unit.CurrentHP}+{bestFuseOpt.candidateUnit.CurrentHP}" +
                          $" via {bestFuseCell} (score={bestFuseScore:F0})");
                return BuildMergeBatch(unit, aiTeam, fromCell, bestFuseCell, bestFuseOpt.candidateUnit, paths);
            }
        }

        // 3. Marcha para a construção aliada mais próxima desocupada (não defensiva)
        // Exclui: célula atual + repCells de objetivos defensivos ativos
        var occupiedForRepair = new HashSet<Vector3Int>(occupied) { fromCell };
        TeamObjectivePlan repPlan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (repPlan != null)
            foreach (SectorObjective obj in repPlan.Objectives)
            {
                if (obj.Status != ObjectiveStatus.Defending) continue;
                if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
                Vector3Int rc = defInfo.RepresentativeCell; rc.z = 0;
                occupiedForRepair.Add(rc);
            }

        ConstructionManager repairDest = FindRepairConstruction(fromCell, aiTeam, occupiedForRepair);
        if (repairDest == null)
        {
            Debug.Log($"{TL("Repair")} {unit.InstanceId} sem destino de reparo — conservador");
            return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        }

        Vector3Int destCell = repairDest.CurrentCellPosition; destCell.z = 0;

        if (fromCell == destCell)
        {
            Debug.Log($"{TL("Repair")} {unit.InstanceId} aguarda reparo em {fromCell}");
            return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        }

        // Avança para o destino: mínima distância + mínima ameaça
        Vector3Int bestStep = fromCell;
        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied.Contains(cell)) continue;
            float dist   = Vector3Int.Distance(cell, destCell);
            float threat = CalculateThreatLevel(cell, aiTeam);
            float score  = -dist * 10f - threat * ThreatWeight;
            if (score > bestScore) { bestScore = score; bestStep = cell; }
        }

        Debug.Log($"{TL("Repair")} {unit.InstanceId} marcha para reparo em {destCell} via {bestStep}");
        return BuildMoveBatch(unit, aiTeam, fromCell, bestStep, paths);
    }

    private static ConstructionManager FindRepairConstruction(Vector3Int fromCell, TeamId aiTeam, HashSet<Vector3Int> occupied)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            Vector3Int cc = c.CurrentCellPosition; cc.z = 0;
            float dist = Vector3Int.Distance(fromCell, cc);
            if (c.TeamId != aiTeam)
            {
                Debug.Log($"[Repair] skip {cc} team={c.TeamId} (need {aiTeam}) dist={dist:F1}");
                continue;
            }
            if (c.CurrentCapturePoints < c.CapturePointsMax)
            {
                Debug.Log($"[Repair] skip {cc} cap={c.CurrentCapturePoints}/{c.CapturePointsMax} (incompleto) dist={dist:F1}");
                continue;
            }
            if (occupied.Contains(cc))
            {
                Debug.Log($"[Repair] skip {cc} ocupado dist={dist:F1}");
                continue;
            }
            if (dist < bestDist) { bestDist = dist; best = c; }
        }
        if (best != null)
        {
            Vector3Int bc = best.CurrentCellPosition; bc.z = 0;
            Debug.Log($"[Repair] destino selecionado {bc} dist={bestDist:F1}");
        }
        return best;
    }
}
