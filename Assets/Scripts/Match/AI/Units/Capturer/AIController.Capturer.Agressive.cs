using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int AggressiveCapturerEngagementRadius = 3;

    private bool TryDecideAggressiveCapturerAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || assigned == null || paths == null)
            return false;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.CapturadorAgressivo)
            return false;

        List<UnitManager> threats = CollectAssaultEscortThreats(
            snapshot.AITeam, targetCell, AggressiveCapturerEngagementRadius);
        AddAssaultEscortTravelThreats(snapshot.AITeam, fromCell, paths, threats);
        if (!TryFindAssaultEscortAttack(
                unit,
                snapshot,
                fromCell,
                targetCell,
                AggressiveCapturerEngagementRadius,
                assigned.Status == ObjectiveStatus.Defending,
                paths,
                occupied,
                threats,
                out Vector3Int attackCell,
                out UnitManager attackTarget,
                out string attackReason))
            return false;

        Vector3Int enemyCell = attackTarget.CurrentCellPosition;
        enemyCell.z = 0;
        Debug.Log($"{TL("CapturadorAgressivo")} {unit.InstanceId} abre caminho para {assigned.Sector} "
            + $"via {attackCell} -> {attackTarget.UnitDisplayName}#{attackTarget.InstanceId} ({attackReason})");
        action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
            attackTarget.InstanceId.ToString(), enemyCell, paths);
        return true;
    }
}
