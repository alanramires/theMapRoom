using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction TryDropFireSupportConservative(
        UnitManager unit, UnitManager primaryPassenger,
        List<UnitManager> passengers,
        AIWorldSnapshot snapshot, TeamObjectivePlan plan,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        // Use enemy HQ as the "forward" direction reference for the main-line check.
        Vector3Int anchor = snapshot.EnemyHQ != null ? snapshot.EnemyHQ.CurrentCellPosition : fromCell;
        anchor.z = 0;

        float bestScore = float.MinValue;
        Vector3Int bestTransporterCell = fromCell;
        List<Vector3Int> bestTransporterPath = null;
        List<PodeDesembarcarOption> bestSelected = null;

        // Candidate transporter positions: current cell + all non-forward reachable cells.
        var candidateCells = new List<(Vector3Int cell, List<Vector3Int> path)> { (fromCell, null) };
        foreach (var kvp in paths)
        {
            Vector3Int c = kvp.Key; c.z = 0;
            if (c == fromCell || occupied.Contains(c)) continue;
            if (IsLogisticsForwardOfMainLine(unit, snapshot, c, anchor)) continue;
            candidateCells.Add((c, kvp.Value));
        }

        foreach (var (tCell, tPath) in candidateCells)
        {
            List<PodeDesembarcarOption> opts;
            if (tCell == fromCell)
            {
                opts = new List<PodeDesembarcarOption>();
                PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, opts);
            }
            else opts = SimulateDisembarkFromCell(unit, tCell);

            if (opts == null || opts.Count == 0) continue;

            // Score using the conservative metric (allied building, cohesion, threat).
            // Forward disembark cells are skipped.
            float cellBestScore = float.MinValue;
            foreach (PodeDesembarcarOption opt in opts)
            {
                if (opt.passengerUnit != primaryPassenger) continue;
                Vector3Int dc = opt.disembarkCell; dc.z = 0;
                if (IsLogisticsForwardOfMainLine(unit, snapshot, dc, anchor)) continue;
                float score = ScoreConservativeFireSupportDropOff(dc, snapshot);
                if (score > cellBestScore) cellBestScore = score;
            }
            if (cellBestScore <= float.MinValue || cellBestScore <= bestScore) continue;

            bestScore = cellBestScore;
            bestTransporterCell = tCell;
            bestTransporterPath = tPath;
            bestSelected = SelectBestDisembarkPerPassenger(opts, passengers, plan, snapshot);
        }

        if (bestSelected != null)
        {
            PodeDesembarcarOption primaryOpt = bestSelected.Find(o => o.passengerUnit == primaryPassenger);
            Vector3Int dropCell = primaryOpt != null ? primaryOpt.disembarkCell : Vector3Int.zero;
            dropCell.z = 0;
            if (bestTransporterCell == fromCell)
            {
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — desembarca FS #{primaryPassenger.InstanceId} @ {dropCell} score={bestScore:F0}");
                return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, bestSelected);
            }
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — move+desembarca FS #{primaryPassenger.InstanceId} via {bestTransporterCell} @ {dropCell} score={bestScore:F0}");
            return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, bestSelected, bestTransporterCell, bestTransporterPath);
        }

        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — sem drop-off seguro, aguarda");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    // Score a disembark cell for the conservative tow case.
    // Does NOT penalize distance from objective — rewards safety and allied presence.

    private float ScoreConservativeFireSupportDropOff(Vector3Int dc, AIWorldSnapshot snapshot)
    {
        float score = 0f;

        ConstructionManager building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, dc);
        if (building != null && building.TeamId == snapshot.AITeam)
            score += 3000f;

        int allyCount = 0;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally.IsDead || ally.IsEmbarked) continue;
            Vector3Int ac = ally.CurrentCellPosition; ac.z = 0;
            if (SectorManager.HexDistance(ac, dc) <= 3f) allyCount++;
        }
        score += allyCount * 80f;

        score += GetTerrainDpqPontos(dc) * 40f;
        score -= CalculateThreatLevel(dc, snapshot.AITeam) * 120f;

        return score;
    }

    // Temporarily repositions the APC to simCell, collects disembark options from there,
    // then restores the original position. Side effects on occupancy/threat are transient
    // (restored before any other unit decision runs).

}
