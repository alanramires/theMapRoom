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
        if (vacatePaths != null && vacatePaths.Count > 0)
        {
            HashSet<Vector3Int> vacateOccupied = BuildOccupied(unit);
            if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, vacatePaths, vacateOccupied, out PlayerAction vacateAction))
                return vacateAction;
        }

        PlayerAction repairAction = TryDecideRepairAction(unit, snapshot, plan);
        if (repairAction != null) return repairAction;

        SectorObjective assigned = ResolveAssignedFireSupportObjective(unit, plan);
        if (assigned == null)
            return DecideRogueFireSupportAction(unit, snapshot);

        if (assigned.Status == ObjectiveStatus.Defending)
            return DecideFireSupportDefenderAction(unit, snapshot, assigned);

        return DecideAssignedFireSupportAction(unit, snapshot, assigned);
    }

    private PlayerAction DecideAssignedFireSupportAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
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

        if (TryFindFireSupportRepositionCell(unit, snapshot, fromCell, anchor, paths, occupied, out Vector3Int moveCell, out string moveReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} reposiciona para apoiar {assigned.Sector} via {moveCell} ({moveReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        Debug.Log($"{TL("FireSupport")} {unit.InstanceId} aguarda apoio {assigned.Sector}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }
}
