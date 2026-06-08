using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction TryDropFireSupportConservative(
        UnitManager unit, UnitManager primaryPassenger,
        List<UnitManager> passengers,
        AIWorldSnapshot snapshot, TeamObjectivePlan plan,
        Vector3Int fromCell,
        Vector3Int progressionMoveTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        // Use enemy HQ as the "forward" direction reference for the main-line check.
        Vector3Int anchor = snapshot.EnemyHQ != null ? snapshot.EnemyHQ.CurrentCellPosition : fromCell;
        anchor.z = 0;
        Vector3Int deliveryTarget;
        if (!TryResolveCourierPassengerTarget(primaryPassenger, plan, snapshot, Vector3Int.zero, fromCell, out deliveryTarget))
            deliveryTarget = anchor;
        deliveryTarget.z = 0;

        if (TryResolveTowObjectiveDeliveryCell(snapshot.AITeam, deliveryTarget, progressionMoveTarget, out Vector3Int objectiveDeliveryCell)
            && TryBuildFireSupportObjectiveDrop(
                unit,
                primaryPassenger,
                passengers,
                snapshot,
                fromCell,
                objectiveDeliveryCell,
                paths,
                occupied,
                out PlayerAction objectiveDrop,
                out string objectiveDropReason))
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier TOW — entrega FS #{primaryPassenger.InstanceId} no objetivo ({objectiveDropReason})");
            return objectiveDrop;
        }

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

        if (TryBuildEmergencyFireSupportDrop(
                unit,
                primaryPassenger,
                passengers,
                snapshot,
                plan,
                fromCell,
                paths,
                occupied,
                out PlayerAction emergencyDrop,
                out string emergencyReason))
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — drop-off nao seguro, libera FS #{primaryPassenger.InstanceId} ({emergencyReason})");
            return emergencyDrop;
        }

        if (TryFindConservativeTowCourierRendezvousCell(
                unit,
                primaryPassenger,
                snapshot,
                plan,
                fromCell,
                anchor,
                paths,
                occupied,
                out Vector3Int rendezvousCell,
                out string rendezvousDetails))
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — sem drop-off seguro, rendezvous via {rendezvousCell} {rendezvousDetails}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, rendezvousCell, paths);
        }

        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — sem drop-off seguro/rendezvous util, aguarda");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private bool TryBuildFireSupportObjectiveDrop(
        UnitManager unit,
        UnitManager primaryPassenger,
        List<UnitManager> passengers,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int objectiveTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null || primaryPassenger == null || snapshot == null || passengers == null || passengers.Count == 0)
            return false;

        float bestScore = float.MinValue;
        Vector3Int bestTransporterCell = fromCell;
        List<Vector3Int> bestPath = null;
        PodeDesembarcarOption bestOption = null;
        objectiveTarget.z = 0;

        var candidates = new List<Vector3Int> { fromCell };
        if (paths != null)
        {
            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell == fromCell)
                    continue;
                candidates.Add(cell);
            }
        }

        foreach (Vector3Int rawCell in candidates)
        {
            Vector3Int transporterCell = rawCell;
            transporterCell.z = 0;
            if (transporterCell != fromCell && occupied != null && occupied.Contains(transporterCell))
                continue;
            if (transporterCell != fromCell && IsNonTeamConstruction(transporterCell, snapshot.AITeam))
                continue;

            List<PodeDesembarcarOption> opts;
            if (transporterCell == fromCell)
            {
                opts = new List<PodeDesembarcarOption>();
                PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, opts);
            }
            else
                opts = SimulateDisembarkFromCell(unit, transporterCell);

            if (opts == null || opts.Count == 0)
                continue;

            for (int i = 0; i < opts.Count; i++)
            {
                PodeDesembarcarOption opt = opts[i];
                if (opt == null || opt.passengerUnit != primaryPassenger)
                    continue;

                Vector3Int dropCell = opt.disembarkCell;
                dropCell.z = 0;

                float dropDist = SectorManager.HexDistance(dropCell, objectiveTarget);
                float truckDist = SectorManager.HexDistance(transporterCell, objectiveTarget);

                // The towed gun is delivered to the objective; the courier itself must stop adjacent.
                if (dropDist > 0.5f)
                    continue;
                if (truckDist < 0.5f || truckDist > 1.5f)
                    continue;

                int pathCost = transporterCell == fromCell ? 0 : GetPathStepCount(paths, transporterCell);
                float threat = CalculateThreatLevel(dropCell, snapshot.AITeam);
                float dpq = GetTerrainDpqPontos(dropCell);
                float score = 50000f
                    + 12000f
                    - threat * 110f
                    - pathCost * 18f
                    + dpq * 90f
                    - Mathf.Abs(truckDist - 1f) * 850f;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestTransporterCell = transporterCell;
                bestOption = opt;
                if (paths != null)
                    paths.TryGetValue(transporterCell, out bestPath);
            }
        }

        if (bestOption == null)
            return false;

        Vector3Int bestDrop = bestOption.disembarkCell;
        bestDrop.z = 0;
        float bestDropDist = SectorManager.HexDistance(bestDrop, objectiveTarget);
        float bestTruckDist = SectorManager.HexDistance(bestTransporterCell, objectiveTarget);
        reason = $"truck={bestTransporterCell} drop={bestDrop} target={objectiveTarget} dropDist={bestDropDist:F1} truckDist={bestTruckDist:F1} score={bestScore:F0}";

        var selected = new List<PodeDesembarcarOption> { bestOption };
        if (bestTransporterCell == fromCell)
            action = BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
        else
            action = BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected, bestTransporterCell, bestPath);

        return true;
    }

    private bool TryResolveTowObjectiveDeliveryCell(TeamId aiTeam, Vector3Int deliveryTarget, Vector3Int progressionMoveTarget, out Vector3Int objectiveCell)
    {
        deliveryTarget.z = 0;
        progressionMoveTarget.z = 0;
        objectiveCell = Vector3Int.zero;

        if (IsTowObjectiveDeliveryCell(progressionMoveTarget, aiTeam))
        {
            objectiveCell = progressionMoveTarget;
            return true;
        }

        if (IsTowObjectiveDeliveryCell(deliveryTarget, aiTeam))
        {
            objectiveCell = deliveryTarget;
            return true;
        }

        return false;
    }

    private bool IsTowObjectiveDeliveryCell(Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;
        if (cell == Vector3Int.zero)
            return false;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction == null)
            return false;
        if (construction.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(construction.Sector))
            return false;
        return true;
    }

    private bool TryBuildEmergencyFireSupportDrop(
        UnitManager unit,
        UnitManager primaryPassenger,
        List<UnitManager> passengers,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null || primaryPassenger == null || snapshot == null || passengers == null || passengers.Count == 0)
            return false;

        Vector3Int target;
        if (!TryResolveCourierPassengerTarget(primaryPassenger, plan, snapshot, Vector3Int.zero, fromCell, out target))
            target = snapshot.EnemyHQ != null ? snapshot.EnemyHQ.CurrentCellPosition : fromCell;
        target.z = 0;

        float bestScore = float.MinValue;
        Vector3Int bestTransporterCell = fromCell;
        List<Vector3Int> bestPath = null;
        List<PodeDesembarcarOption> bestSelected = null;
        PodeDesembarcarOption bestPrimary = null;

        var candidateCells = new List<Vector3Int> { fromCell };
        if (paths != null)
        {
            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell == fromCell) continue;
                candidateCells.Add(cell);
            }
        }

        foreach (Vector3Int rawCell in candidateCells)
        {
            Vector3Int tCell = rawCell;
            tCell.z = 0;
            if (tCell != fromCell && occupied != null && occupied.Contains(tCell))
                continue;
            if (tCell != fromCell && IsNonTeamConstruction(tCell, snapshot.AITeam))
                continue;

            List<PodeDesembarcarOption> opts;
            if (tCell == fromCell)
            {
                opts = new List<PodeDesembarcarOption>();
                PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, opts);
            }
            else
                opts = SimulateDisembarkFromCell(unit, tCell);

            if (opts == null || opts.Count == 0)
                continue;

            List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(opts, passengers, plan, snapshot);
            PodeDesembarcarOption primaryOpt = selected.Find(o => o.passengerUnit == primaryPassenger);
            if (primaryOpt == null)
                continue;

            Vector3Int dropCell = primaryOpt.disembarkCell;
            dropCell.z = 0;
            int pathCost = tCell == fromCell ? 0 : GetPathStepCount(paths, tCell);
            float score = ScoreEmergencyFireSupportDrop(primaryPassenger, snapshot, fromCell, tCell, dropCell, target, pathCost, out _);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestTransporterCell = tCell;
            bestSelected = selected;
            bestPrimary = primaryOpt;
            if (paths != null)
                paths.TryGetValue(tCell, out bestPath);
        }

        if (bestSelected == null || bestPrimary == null)
            return false;

        Vector3Int bestDrop = bestPrimary.disembarkCell;
        bestDrop.z = 0;
        ScoreEmergencyFireSupportDrop(primaryPassenger, snapshot, fromCell, bestTransporterCell, bestDrop, target,
            bestTransporterCell == fromCell ? 0 : GetPathStepCount(paths, bestTransporterCell), out string details);
        reason = $"via={bestTransporterCell} dc={bestDrop} target={target} score={bestScore:F0} {details}";

        if (bestTransporterCell == fromCell)
            action = BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, bestSelected);
        else
            action = BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, bestSelected, bestTransporterCell, bestPath);

        return true;
    }

    private float ScoreEmergencyFireSupportDrop(
        UnitManager primaryPassenger,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int transporterCell,
        Vector3Int dropCell,
        Vector3Int target,
        int pathCost,
        out string details)
    {
        float threat = CalculateThreatLevel(dropCell, snapshot.AITeam);
        float dpq = GetTerrainDpqPontos(dropCell);
        float fromDist = SectorManager.HexDistance(fromCell, target);
        float dropDist = SectorManager.HexDistance(dropCell, target);
        float progress = fromDist - dropDist;
        float cohesion = CalculateFireSupportCohesionScore(primaryPassenger, snapshot, dropCell);
        int alliesNear = CountEmergencyFireSupportAllies(primaryPassenger, snapshot, dropCell);

        float score = 4500f
            + progress * 420f
            + dpq * 90f
            + cohesion * 0.2f
            + alliesNear * 360f
            - threat * 85f
            - pathCost * 18f
            - dropDist * 12f;

        if (transporterCell != fromCell)
            score += 450f;
        if (alliesNear == 0 && threat > 0f)
            score -= 650f;

        details = $"prog={progress:F1} dist={dropDist:F1} dpq={dpq:F1} threat={threat:F1} allies3={alliesNear} path={pathCost}";
        return score;
    }

    private int CountEmergencyFireSupportAllies(UnitManager primaryPassenger, AIWorldSnapshot snapshot, Vector3Int dropCell)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == primaryPassenger || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            if (SectorManager.HexDistance(dropCell, allyCell) <= 3f)
                count++;
        }

        return count;
    }

    private bool TryFindConservativeTowCourierRendezvousCell(
        UnitManager unit,
        UnitManager primaryPassenger,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        Vector3Int mainLineAnchor,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string details)
    {
        bestCell = fromCell;
        details = "";
        if (unit == null || primaryPassenger == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        Vector3Int target;
        if (!TryResolveCourierPassengerTarget(primaryPassenger, plan, snapshot, Vector3Int.zero, fromCell, out target))
            target = mainLineAnchor;
        target.z = 0;
        mainLineAnchor.z = 0;
        fromCell.z = 0;

        float fromScore = ScoreConservativeTowCourierRendezvousCell(
            unit, primaryPassenger, snapshot, fromCell, fromCell, target, mainLineAnchor, 0, out string fromDetails);
        float bestScore = fromScore;
        string bestDetails = fromDetails;

        foreach (var kvp in paths)
        {
            Vector3Int cell = kvp.Key;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;
            if (IsLogisticsForwardOfMainLine(unit, snapshot, cell, mainLineAnchor)) continue;
            if (!IsFireSupportConservativeCellAllowed(primaryPassenger, snapshot, cell)) continue;

            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (construction != null && construction.TeamId != snapshot.AITeam)
                continue;

            int pathCost = GetPathStepCount(paths, cell);
            float score = ScoreConservativeTowCourierRendezvousCell(
                unit, primaryPassenger, snapshot, cell, fromCell, target, mainLineAnchor, pathCost, out string cellDetails);

            if (score <= bestScore + 80f)
                continue;

            bestScore = score;
            bestCell = cell;
            bestDetails = cellDetails;
        }

        if (bestCell == fromCell)
            return false;

        details = $"score={bestScore:F0} from={fromScore:F0} {bestDetails}";
        return true;
    }

    private float ScoreConservativeTowCourierRendezvousCell(
        UnitManager unit,
        UnitManager primaryPassenger,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int fromCell,
        Vector3Int target,
        Vector3Int mainLineAnchor,
        int pathCost,
        out string details)
    {
        float dpq = GetTerrainDpqPontos(cell);
        float threat = CalculateThreatLevel(cell, snapshot.AITeam);
        float fromDist = SectorManager.HexDistance(fromCell, target);
        float targetDist = SectorManager.HexDistance(cell, target);
        float progress = fromDist - targetDist;
        float anchorDist = SectorManager.HexDistance(cell, mainLineAnchor);
        float cohesion = CalculateFireSupportCohesionScore(primaryPassenger, snapshot, cell);
        float rearLine = CalculateFireSupportRearLineScore(primaryPassenger, snapshot, cell, mainLineAnchor);
        int supportCount = CountTowCourierFrontlineSupport(unit, primaryPassenger, snapshot, cell, target, out float nearestSupport);

        float supportScore = supportCount * 520f;
        if (nearestSupport < float.MaxValue)
            supportScore += Mathf.Max(0f, 3f - nearestSupport) * 180f;

        float score = dpq * 70f
            + progress * 680f
            + supportScore
            + cohesion * 0.35f
            + rearLine * 0.25f
            - threat * 145f
            - pathCost * 18f
            - Mathf.Abs(anchorDist - 4f) * 18f;

        if (supportCount > 0 && nearestSupport <= 3f)
            score += 850f;
        if (progress > 0f)
            score += 500f;
        if (progress <= 0f && supportCount == 0)
            score -= 2200f;

        details = $"target={target} prog={progress:F1} allies3={supportCount} near={nearestSupport:F1} dpq={dpq:F1} threat={threat:F1} coh={cohesion:F0} rear={rearLine:F0} path={pathCost}";
        return score;
    }

    private int CountTowCourierFrontlineSupport(
        UnitManager unit,
        UnitManager primaryPassenger,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int target,
        out float nearestSupport)
    {
        nearestSupport = float.MaxValue;
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        float cellTargetDist = SectorManager.HexDistance(cell, target);
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally == primaryPassenger || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (IsPrimaryLogisticsUnit(ally) || IsFireSupportUnit(ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(cell, allyCell);
            if (dist > 3f)
                continue;

            nearestSupport = Mathf.Min(nearestSupport, dist);
            float allyTargetDist = SectorManager.HexDistance(allyCell, target);
            if (allyTargetDist <= cellTargetDist + 3f)
                count++;
        }

        return count;
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
