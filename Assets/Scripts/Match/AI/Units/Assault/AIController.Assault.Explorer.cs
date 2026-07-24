using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // Returns cells of capturable buildings near the scout zone that are not fully
    // owned by the AI — used as sweep targets without hidden-unit omniscience.
    private List<Vector3Int> CollectSweepSuspectCells(TeamId aiTeam, Vector3Int escortCell, int scoutZoneRadius)
    {
        var suspects = new List<Vector3Int>();
        // Fixed sweep radius — escort stays close to the capturador regardless of unit speed.
        // scoutZoneRadius (dynamic, = movement) is intentionally NOT used here.
        int searchRadius = AssaultScoutZoneRadius + 1;

        foreach (ConstructionManager bldg in ConstructionManager.AllActive)
        {
            if (bldg == null || !bldg.IsCapturable || bldg.CapturePointsMax <= 0) continue;
            if (bldg.SlotIndex == ResolveAISlotKey(aiTeam) && bldg.CurrentCapturePoints >= bldg.CapturePointsMax) continue;

            Vector3Int bc = bldg.CurrentCellPosition; bc.z = 0;
            if (SectorManager.HexDistance(bc, escortCell) > searchRadius) continue;

            suspects.Add(bc);
        }

        suspects.Sort((a, b) =>
            SectorManager.HexDistance(a, escortCell).CompareTo(SectorManager.HexDistance(b, escortCell)));

        return suspects;
    }

    private bool TryFindAssaultScoutRevealMove(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int escortCell,
        int scoutZoneRadius,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        List<Vector3Int> suspectCells,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        if (suspectCells == null || suspectCells.Count == 0) return false;

        bool preferDpq = unit.TryGetUnitData(out UnitData ud) && ud.prioritizeDpqAtBattle;
        float bestScore = float.MinValue;

        foreach (Vector3Int suspectCell in suspectCells)
        {
            var revealCells = new List<Vector3Int>();
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, suspectCell, revealCells);
            foreach (Vector3Int rawCell in revealCells)
            {
                Vector3Int cell = rawCell; cell.z = 0;
                if (cell == fromCell) continue;
                if (!paths.ContainsKey(cell)) continue;
                if (occupied.Contains(cell)) continue;
                if (cell == escortCell) continue;
                if (IsReservedAssaultEscortCaptureCell(cell, snapshot.AITeam)) continue;

                float anchorDist = SectorManager.HexDistance(cell, escortCell);
                float suspectDist = SectorManager.HexDistance(suspectCell, escortCell);
                float terrain = preferDpq ? GetTerrainDpqPontos(cell) : GetTerrainEv(cell);
                float pathCost = GetPathStepCount(paths, cell);
                float score =
                    Mathf.Max(0, scoutZoneRadius + 1 - suspectDist) * 1000f
                    - anchorDist * 120f
                    + terrain * 60f
                    - pathCost * 10f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    reason = $"score={score:F0} suspectDist={suspectDist:F1} zona={anchorDist:F1} terrain={terrain:F1} mov={pathCost:F0}";
                }
            }
        }

        return bestCell != fromCell;
    }
}
