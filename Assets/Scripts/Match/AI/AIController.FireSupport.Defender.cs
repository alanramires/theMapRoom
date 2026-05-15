using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fire Support Defensor - cobre setor/base ja controlado.
    // -------------------------------------------------------------------------

    private PlayerAction DecideFireSupportDefenderAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int anchor = ResolveFireSupportObjectiveAnchor(assigned, snapshot.AITeam, fromCell);

        if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor, defensiveContext: true, out PlayerAction attackAction, out string attackReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} defesa {assigned.Sector} - {attackReason}");
            return attackAction;
        }

        // Embark on adjacent transport (e.g. supply truck tow) to reach better position.
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        PlayerAction embarkAction = TryDecideAssaultEmbarkAction(unit, snapshot, plan);
        if (embarkAction != null) return embarkAction;

        if (IsLongRangeStationary(unit) && IsFireSupportCloseEnoughToHold(unit, fromCell, anchor))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} defesa estacionaria {assigned.Sector} @ {fromCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (TryFindFireSupportRepositionCell(unit, snapshot, fromCell, anchor, paths, occupied, out Vector3Int moveCell, out string moveReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} ajusta defesa {assigned.Sector} via {moveCell} ({moveReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }
}
