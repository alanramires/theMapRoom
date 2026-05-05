using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private List<UnitManager> CollectHiddenAssaultEscortThreats(TeamId aiTeam, Vector3Int escortCell, int scoutZoneRadius)
    {
        var threats = new List<UnitManager>();
        MatchController mc = GetMatchController();
        if (mc == null) return threats;

        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;

            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, escortCell) <= scoutZoneRadius)
                threats.Add(enemy);
        }

        threats.Sort((a, b) =>
        {
            Vector3Int ac = a.CurrentCellPosition; ac.z = 0;
            Vector3Int bc = b.CurrentCellPosition; bc.z = 0;
            int distCmp = SectorManager.HexDistance(ac, escortCell).CompareTo(SectorManager.HexDistance(bc, escortCell));
            if (distCmp != 0) return distCmp;
            return a.CurrentHP.CompareTo(b.CurrentHP);
        });
        return threats;
    }

    private bool TryFindAssaultScoutRevealMove(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int escortCell,
        int scoutZoneRadius,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        List<UnitManager> hiddenThreats,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        out string reason)
    {
        bestCell = fromCell;
        bestTarget = null;
        reason = "";
        if (hiddenThreats == null || hiddenThreats.Count == 0)
            return false;

        bool preferDpq = unit.TryGetUnitData(out UnitData ud) && ud.prioritizeDpqAtBattle;
        float bestScore = float.MinValue;

        foreach (UnitManager hidden in hiddenThreats)
        {
            if (hidden == null || hidden.IsDead || hidden.IsEmbarked) continue;
            Vector3Int hiddenCell = hidden.CurrentCellPosition; hiddenCell.z = 0;

            var revealCells = new List<Vector3Int>();
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, hiddenCell, revealCells);
            foreach (Vector3Int rawCell in revealCells)
            {
                Vector3Int cell = rawCell; cell.z = 0;
                if (cell == fromCell) continue;
                if (!paths.ContainsKey(cell)) continue;
                if (occupied.Contains(cell)) continue;
                if (cell == escortCell) continue;
                if (IsReservedAssaultEscortCaptureCell(cell, snapshot.AITeam)) continue;

                float anchorDist = SectorManager.HexDistance(cell, escortCell);
                float hiddenDist = SectorManager.HexDistance(hiddenCell, escortCell);
                float terrain = preferDpq ? GetTerrainDpqPontos(cell) : GetTerrainEv(cell);
                float pathCost = GetPathStepCount(paths, cell);
                float score =
                    Mathf.Max(0, scoutZoneRadius + 1 - hiddenDist) * 1000f
                    - anchorDist * 120f
                    + terrain * 60f
                    - pathCost * 10f
                    - hidden.CurrentHP * 2f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = hidden;
                    reason = $"score={score:F0} hiddenDist={hiddenDist:F1} zona={anchorDist:F1} terrain={terrain:F1} mov={pathCost:F0}";
                }
            }
        }

        return bestTarget != null && bestCell != fromCell;
    }
}
