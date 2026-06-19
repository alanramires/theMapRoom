using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction DecideCombatantAntiAirFireSupportAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        SectorObjective assigned = ResolveAssignedAssaultObjective(unit, plan)
            ?? ResolveAssignedFireSupportObjective(unit, plan);
        if (TryFindGroundAntiAirAttack(
                unit, snapshot, fromCell, paths, occupied,
                out Vector3Int attackCell, out UnitManager target, out string attackReason))
        {
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("AntiaereoCombatente")} {unit.InstanceId} intercepta via {attackCell}"
                + $" -> {target.UnitDisplayName}#{target.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                target.InstanceId.ToString(), targetCell, paths);
        }

        if (TryFindGroundAntiAirEscortGroupMove(
                unit, snapshot, assigned, fromCell, paths, occupied,
                out Vector3Int escortCell, out string escortReason))
        {
            if (escortCell != fromCell)
            {
                Debug.Log($"{TL("AntiaereoCombatente")} {unit.InstanceId} escolta"
                    + $"{FormatAntiAirSector(assigned)} via {escortCell} ({escortReason})");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, escortCell, paths);
            }

            Debug.Log($"{TL("AntiaereoCombatente")} {unit.InstanceId} mantem escolta"
                + $"{FormatAntiAirSector(assigned)} ({escortReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (TryFindGroundAntiAirCohesionMove(
                unit, snapshot, fromCell, paths, occupied,
                out Vector3Int moveCell, out string moveReason)
            && moveCell != fromCell)
        {
            Debug.Log($"{TL("AntiaereoCombatente")} {unit.InstanceId} recompõe cobertura via"
                + $" {moveCell} ({moveReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        Debug.Log($"{TL("AntiaereoCombatente")} {unit.InstanceId} sem alvo aereo - segura cobertura");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }
}
