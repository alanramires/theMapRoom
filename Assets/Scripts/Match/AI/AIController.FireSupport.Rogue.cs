using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fire Support Rogue - sem slot de plano, pressiona alvo estrategico visivel.
    // -------------------------------------------------------------------------

    private PlayerAction DecideRogueFireSupportAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int anchor = ResolveRogueFireSupportAnchor(snapshot, fromCell);

        if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor, defensiveContext: false, out PlayerAction attackAction, out string attackReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue - {attackReason}");
            return attackAction;
        }

        if (IsLongRangeStationary(unit))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue estacionario @ {fromCell} - sem alvo");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (TryFindFireSupportRepositionCell(unit, snapshot, fromCell, anchor, paths, occupied, out Vector3Int moveCell, out string moveReason, requireImmediateThreat: true))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue reposiciona via {moveCell} alvo={anchor} ({moveReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }
}
