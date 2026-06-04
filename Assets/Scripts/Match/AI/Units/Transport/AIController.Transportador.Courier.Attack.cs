using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private bool TryFindTransportCourierAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int primaryTarget,
        out Vector3Int bestCell,
        out UnitManager bestTarget)
    {
        bestCell = fromCell;
        bestTarget = null;

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (enemies == null || enemies.Count == 0) return false;

        float fromDistToTarget = SectorManager.HexDistance(fromCell, primaryTarget);

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied.Contains(cell)) continue;
            if (SectorManager.HexDistance(cell, primaryTarget) > fromDistToTarget + 2f) continue;

            foreach (UnitManager enemy in enemies)
            {
                if (enemy.CurrentHP > 2) continue;
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, cell, false, out _)) continue;

                bestCell = cell;
                bestTarget = enemy;
                return true;
            }
        }
        return false;
    }

}
