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
                            unit.ClearAIAssignedPlan();
                            break;
                        }
                plan.RogueUnitIds.Remove(unit.InstanceId);
            }
            Debug.Log($"[AI][Repair] {unit.InstanceId} entra em reparo " +
                      $"hp={unit.CurrentHP} fuel={unit.CurrentFuel}/{unit.GetMaxFuel()} " +
                      $"ammo={unit.CurrentAmmo}/{unit.GetMaxAmmo()}");
        }
        else if (unit.IsUnderRepair && !anyTrigger && unit.CurrentHP >= data.repairRecoverHpAbove)
        {
            unit.SetIsUnderRepair(false);
            Debug.Log($"[AI][Repair] {unit.InstanceId} saiu do reparo hp={unit.CurrentHP}");
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

        // Se está sobre captura incompleta → sair e liberar o prédio para outros capturadores
        ConstructionManager currentBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);
        if (currentBldg != null && currentBldg.IsCapturable
            && !(currentBldg.TeamId == aiTeam && currentBldg.CurrentCapturePoints >= currentBldg.CapturePointsMax))
        {
            Vector3Int exitCell = FindRepairExitCell(paths, occupied, fromCell, aiTeam);
            if (exitCell != fromCell)
            {
                Debug.Log($"[AI][Repair] {unit.InstanceId} sai de captura incompleta em {fromCell} → {exitCell}");
                return BuildMoveBatch(unit, aiTeam, fromCell, exitCell, paths);
            }
        }

        // Fusão oportunista: se fuseWhileInRepair e HP < 10, funde com aliado no caminho
        if (unit.TryGetUnitData(out UnitData fuseData) && fuseData.fuseWhileInRepair && unit.CurrentHP < 10)
        {
            var fuseOptions = new List<PodeFundirOption>();
            foreach (Vector3Int cell in paths.Keys)
            {
                if (occupied.Contains(cell)) continue;
                fuseOptions.Clear();
                if (!PodeFundirSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
                    Mathf.Max(0, unit.RemainingMovementPoints), fuseOptions, out _,
                    fromCell: cell))
                    continue;
                foreach (PodeFundirOption opt in fuseOptions)
                {
                    if (opt?.candidateUnit == null) continue;
                    if (opt.candidateUnit.CurrentHP + unit.CurrentHP > 10) continue;
                    Vector3Int candidateCell = opt.candidateCell; candidateCell.z = 0;
                    Debug.Log($"[AI][Repair] {unit.InstanceId} fusão oportunista com " +
                              $"{opt.candidateUnit.InstanceId} hp={unit.CurrentHP}+{opt.candidateUnit.CurrentHP}");
                    return BuildMergeBatch(unit, aiTeam, fromCell, candidateCell, opt.candidateUnit, paths);
                }
            }
        }

        // Navega para a construção aliada mais próxima desocupada
        ConstructionManager repairDest = FindRepairConstruction(fromCell, aiTeam, occupied);
        if (repairDest == null)
        {
            Debug.Log($"[AI][Repair] {unit.InstanceId} sem destino de reparo — conservador");
            return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        }

        Vector3Int destCell = repairDest.CurrentCellPosition; destCell.z = 0;

        if (fromCell == destCell)
        {
            Debug.Log($"[AI][Repair] {unit.InstanceId} aguarda reparo em {fromCell}");
            return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        }

        // Avança para o destino: mínima distância + mínima ameaça
        Vector3Int bestStep = fromCell;
        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied.Contains(cell)) continue;
            float dist  = Vector3Int.Distance(cell, destCell);
            float threat = CalculateThreatLevel(cell, aiTeam);
            float score = -dist * 10f - threat * ThreatWeight;
            if (score > bestScore) { bestScore = score; bestStep = cell; }
        }

        Debug.Log($"[AI][Repair] {unit.InstanceId} marcha para reparo em {destCell} via {bestStep}");
        return BuildMoveBatch(unit, aiTeam, fromCell, bestStep, paths);
    }

    private Vector3Int FindRepairExitCell(
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int fromCell,
        TeamId aiTeam)
    {
        Vector3Int best = fromCell;
        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell || occupied.Contains(cell)) continue;
            // Não sair para outro prédio inimigo/neutro capturável (evita captura acidental)
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (bldg != null && bldg.IsCapturable && bldg.TeamId != aiTeam) continue;
            float threat = CalculateThreatLevel(cell, aiTeam);
            float score  = -threat;
            if (score > bestScore) { bestScore = score; best = cell; }
        }
        return best;
    }

    private static ConstructionManager FindRepairConstruction(Vector3Int fromCell, TeamId aiTeam, HashSet<Vector3Int> occupied)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.TeamId != aiTeam) continue;
            if (c.CurrentCapturePoints < c.CapturePointsMax) continue; // não totalmente controlado
            Vector3Int cc = c.CurrentCellPosition; cc.z = 0;
            if (occupied.Contains(cc)) continue;
            float dist = Vector3Int.Distance(fromCell, cc);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }
        return best;
    }
}
