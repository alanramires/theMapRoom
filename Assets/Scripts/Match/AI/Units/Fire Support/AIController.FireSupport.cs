using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fire Support - entrada principal
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideFireSupportAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!IsFireSupportUnit(unit)) return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> vacatePaths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> vacateOccupied = BuildOccupied(unit);
        if (vacatePaths != null && vacatePaths.Count > 0)
        {
            if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, vacatePaths, vacateOccupied, out PlayerAction vacateAction))
                return vacateAction;
        }

        TeamObjectivePlan capBlockPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        if (capBlockPlan != null && IsCellACapturerTarget(fromCell, capBlockPlan, snapshot.AITeam))
        {
            Vector3Int vacateCell = FindFireSupportCapturerVacateCell(unit, snapshot, fromCell, capBlockPlan, vacatePaths ?? BuildFireSupportPaths(unit), vacateOccupied);
            if (vacateCell != fromCell)
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} bloqueia capturador em {fromCell} — vacate via {vacateCell}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, vacateCell, vacatePaths);
            }
        }

        PlayerAction repairAction = TryDecideRepairAction(unit, snapshot, plan);
        if (repairAction != null) return repairAction;

        SectorObjective assigned = ResolveAssignedFireSupportObjective(unit, plan);
        if (assigned == null)
        {
            PlayerAction embarkAction = TryDecideAssaultEmbarkAction(unit, snapshot, plan);
            if (embarkAction != null) return embarkAction;
            return DecideRogueFireSupportAction(unit, snapshot);
        }

        if (assigned.Status == ObjectiveStatus.Defending)
            return DecideFireSupportDefenderAction(unit, snapshot, assigned);

        return DecideAssignedFireSupportAction(unit, snapshot, plan, assigned);
    }

    private PlayerAction DecideAssignedFireSupportAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int anchor = ResolveFireSupportObjectiveAnchor(assigned, snapshot.AITeam, fromCell);

        if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor, assigned.Status == ObjectiveStatus.Defending, out PlayerAction attackAction, out string attackReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} apoia {assigned.Sector} - {attackReason}");
            return attackAction;
        }

        // Adjacent supply truck takes priority over walking — faster delivery to objective.
        if (assigned.Status != ObjectiveStatus.Defending
            && AITacticalAnalyzer.Instance != null
            && !AITacticalAnalyzer.Instance.IsFireSupportScreenedForObjective(unit, snapshot.AITeam, assigned, out AITacticalNeed op, out string screenReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} segura {assigned.Sector}: task force sem screen ({screenReason})");
            PlayerAction rendezvousEarlyAction = TryFireSupportRendezvousAction(unit, snapshot, assigned, fromCell, paths, occupied);
            if (rendezvousEarlyAction != null) return rendezvousEarlyAction;
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (TryBuildFireSupportBlockedShotRepositionAction(unit, snapshot, fromCell, paths, occupied, assigned.Status == ObjectiveStatus.Defending, out PlayerAction blockedShotAction, out string blockedShotReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} reposiciona para linha de tiro {assigned.Sector} - {blockedShotReason}");
            return blockedShotAction;
        }

        PlayerAction embarkAction = TryDecideAssaultEmbarkAction(unit, snapshot, plan);
        if (embarkAction != null) return embarkAction;

        if (TryFindFireSupportRepositionCell(unit, snapshot, fromCell, anchor, paths, occupied, out Vector3Int moveCell, out string moveReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} reposiciona para apoiar {assigned.Sector} via {moveCell} ({moveReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        if (TryFindFireSupportMaxRangeThreatCell(unit, snapshot, fromCell, paths, occupied, out Vector3Int maxRangeCell, out string maxRangeReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} postura max-range {assigned.Sector} via {maxRangeCell} ({maxRangeReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, maxRangeCell, paths);
        }

        PlayerAction rendezvousAction = TryFireSupportRendezvousAction(unit, snapshot, assigned, fromCell, paths, occupied);
        if (rendezvousAction != null) return rendezvousAction;

        Debug.Log($"{TL("FireSupport")} {unit.InstanceId} aguarda apoio {assigned.Sector}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    // Rendezvous: move em direção ao aliado do plano mais próximo quando não há ação de artilharia.
    private PlayerAction TryFireSupportRendezvousAction(
        UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned,
        Vector3Int fromCell, Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied)
    {
        if (paths == null || paths.Count == 0 || assigned == null) return null;
        if (PreferFireSupportWeaponMaxRange(unit) || IsFireSupportConservative(unit))
            return null;

        UnitManager rendezvousTarget = null;
        float bestDist = float.MaxValue;

        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!slot.Filled || slot.AssignedUnitId == unit.InstanceId) continue;
            UnitManager ally = FindActiveUnit(slot.AssignedUnitId, snapshot.AITeam);
            if (ally == null || ally.IsDead || ally.IsEmbarked) continue;
            if (IsFireSupportUnit(ally)) continue;
            Vector3Int allyCell = ally.CurrentCellPosition; allyCell.z = 0;
            float dist = SectorManager.HexDistance(fromCell, allyCell);
            if (dist < bestDist) { bestDist = dist; rendezvousTarget = ally; }
        }

        if (rendezvousTarget == null || bestDist <= 1f) return null;

        Vector3Int targetCell = rendezvousTarget.CurrentCellPosition; targetCell.z = 0;
        Vector3Int moveCell = FindAssaultPressureMove(unit, snapshot, fromCell, targetCell, paths, occupied);
        if (moveCell == fromCell) return null;
        if (CalculateThreatLevel(moveCell, snapshot.AITeam) > CalculateThreatLevel(fromCell, snapshot.AITeam) + 0.1f)
            return null;

        Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rendezvous {assigned.Sector} → #{rendezvousTarget.InstanceId} via {moveCell}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
    }
}
