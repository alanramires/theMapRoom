using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIPlayerController
{
    private static void TryResolveFallbackCaptureTarget(AISnapshot snapshot, AIPlanIntent intent, AIPlanAssignment assignment)
    {
        if (snapshot == null || intent == null || assignment == null)
            return;

        if (!intent.HasCaptureTarget || assignment.Role != AIPlanRole.Capture)
            return;

        var occupied = new HashSet<Vector3Int>();
        for (int i = 0; i < intent.Assignments.Count; i++)
        {
            AIPlanAssignment existing = intent.Assignments[i];
            if (existing == null || !existing.HasPlannedCaptureTarget)
                continue;

            Vector3Int cell = existing.PlannedCaptureCell;
            cell.z = 0;
            occupied.Add(cell);
        }

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable || info.Sector != intent.Sector)
                continue;

            bool ownedFully = info.TeamId == snapshot.AiTeam && info.CapturePoints >= info.CapturePointsMax;
            if (ownedFully)
                continue;

            Vector3Int cell = info.Cell;
            cell.z = 0;
            if (occupied.Contains(cell))
                continue;

            assignment.HasPlannedCaptureTarget = true;
            assignment.PlannedCaptureCell = cell;
            assignment.PlannedCaptureLabel = !string.IsNullOrWhiteSpace(info.DisplayName)
                ? info.DisplayName
                : intent.Sector.ToString();
            return;
        }

        assignment.HasPlannedCaptureTarget = intent.HasCaptureTarget;
        assignment.PlannedCaptureCell = intent.CaptureTargetCell;
        assignment.PlannedCaptureLabel = intent.CaptureTargetLabel;
    }

    private static int CountOutstandingCaptureTargets(AISnapshot snapshot, ConstructionSector sector)
    {
        if (snapshot == null)
            return 0;

        int count = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable)
                continue;
            if (info.Sector != sector)
                continue;

            bool ownedByAi = info.TeamId == snapshot.AiTeam;
            if (!ownedByAi)
                count++;
        }

        return count;
    }

    private static bool IsFriendlyCaptureUnitOccupyingCell(AISnapshot snapshot, UnitManager requester, Vector3Int cell)
    {
        if (snapshot == null || requester == null || snapshot.FriendlyUnits == null)
            return false;

        cell.z = 0;
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager ally = snapshot.FriendlyUnits[i];
            if (ally == null || ally == requester || ally.IsDead || ally.IsEmbarked)
                continue;
            if (ally.TeamId != requester.TeamId)
                continue;
            if (!(ally.TryGetUnitData(out UnitData allyData) && allyData != null
                && allyData.aiUnitProfile != null && allyData.aiUnitProfile.HasSensorInStance(AIStance.Attack, AIUnitSensorKind.Capture)))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            if (allyCell == cell)
                return true;
        }

        return false;
    }

    private static bool HasVisibleEnemyOnCell(AISnapshot snapshot, Vector3Int cell)
    {
        if (snapshot?.VisibleEnemies == null) return false;
        cell.z = 0;
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager e = snapshot.VisibleEnemies[i];
            if (e == null || e.IsDead) continue;
            Vector3Int ec = e.CurrentCellPosition;
            ec.z = 0;
            if (ec == cell) return true;
        }
        return false;
    }

    private static UnitManager GetVisibleEnemyOnCell(AISnapshot snapshot, Vector3Int cell)
    {
        if (snapshot?.VisibleEnemies == null) return null;
        cell.z = 0;
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager e = snapshot.VisibleEnemies[i];
            if (e == null || e.IsDead) continue;
            Vector3Int ec = e.CurrentCellPosition;
            ec.z = 0;
            if (ec == cell) return e;
        }
        return null;
    }

    private static bool IsEnemyInCaptureLane(
        Tilemap board,
        Vector3Int unitCell,
        Vector3Int captureObjectiveCell,
        Vector3Int enemyCell)
    {
        unitCell.z = 0;
        captureObjectiveCell.z = 0;
        enemyCell.z = 0;

        int unitToObjective = GetHexDistance(board, unitCell, captureObjectiveCell, 64);
        int enemyToObjective = GetHexDistance(board, enemyCell, captureObjectiveCell, 64);
        int unitToEnemy = GetHexDistance(board, unitCell, enemyCell, 64);
        if (unitToObjective == int.MaxValue || enemyToObjective == int.MaxValue || unitToEnemy == int.MaxValue)
            return false;

        if (enemyToObjective <= 1)
            return true;

        return unitToEnemy <= unitToObjective + 1 && enemyToObjective <= unitToObjective;
    }

    private bool TryGetPlanCohesionObjective(
        UnitManager unit,
        AISnapshot snapshot,
        AIPlanIntent unitIntent,
        AIPlanAssignment unitAssignment,
        out Vector3Int targetCell,
        out string targetLabel)
    {
        targetCell = unit.CurrentCellPosition;
        targetCell.z = 0;
        targetLabel = unitIntent != null ? unitIntent.Sector.ToString() : "plano";

        if (unit == null || snapshot == null || unitIntent == null || unitAssignment == null)
            return false;
        if (ConstructionSectorHelper.IsBase(unitIntent.Sector))
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        int bestDistance = int.MaxValue;
        bool found = false;

        if (unitIntent.Assignments != null)
        {
            for (int i = 0; i < unitIntent.Assignments.Count; i++)
            {
                AIPlanAssignment asgn = unitIntent.Assignments[i];
                if (asgn == null || asgn.UnitInstanceId == unit.InstanceId)
                    continue;
                if (asgn.Role != AIPlanRole.Capture)
                    continue;

                UnitManager captureUnit = FindUnitById(asgn.UnitInstanceId);
                if (captureUnit == null || captureUnit.IsDead)
                    continue;

                Vector3Int candidate = captureUnit.CurrentCellPosition;
                candidate.z = 0;
                int dist = GetHexDistance(snapshot.BoardTilemap, unitCell, candidate, 64);
                if (dist == int.MaxValue) dist = 64;
                if (dist >= bestDistance) continue;

                bestDistance = dist;
                targetCell = candidate;
                targetLabel = captureUnit.name;
                found = true;
            }
        }

        if (found)
            return true;

        if (unitIntent.Assignments != null)
        {
            for (int i = 0; i < unitIntent.Assignments.Count; i++)
            {
                AIPlanAssignment asgn = unitIntent.Assignments[i];
                if (asgn == null || asgn.UnitInstanceId == unit.InstanceId)
                    continue;
                if (asgn.Role != AIPlanRole.Capture)
                    continue;

                Vector3Int candidate = asgn.HasPlannedCaptureTarget
                    ? asgn.PlannedCaptureCell
                    : (unitIntent.HasCaptureTarget ? unitIntent.CaptureTargetCell : unitCell);
                candidate.z = 0;
                int dist = GetHexDistance(snapshot.BoardTilemap, unitCell, candidate, 64);
                if (dist == int.MaxValue) dist = 64;
                if (dist >= bestDistance) continue;

                bestDistance = dist;
                targetCell = candidate;
                targetLabel = asgn.HasPlannedCaptureTarget && !string.IsNullOrWhiteSpace(asgn.PlannedCaptureLabel)
                    ? asgn.PlannedCaptureLabel
                    : unitIntent.Sector.ToString();
                found = true;
            }
        }

        if (found)
            return true;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != unitIntent.Sector || !info.IsCapturable)
                continue;

            bool aiOwnsFully = info.TeamId == unit.TeamId && info.CapturePoints >= info.CapturePointsMax;
            if (aiOwnsFully)
                continue;

            Vector3Int candidate = info.Cell;
            candidate.z = 0;
            int dist = GetHexDistance(snapshot.BoardTilemap, unitCell, candidate, 64);
            if (dist == int.MaxValue) dist = 64;
            if (dist >= bestDistance) continue;

            bestDistance = dist;
            targetCell = candidate;
            targetLabel = !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : unitIntent.Sector.ToString();
            found = true;
        }

        return found;
    }

    private bool TryGetBestCaptureObjectiveForInfantry(
        UnitManager unit,
        AISnapshot snapshot,
        out ConstructionManager targetConstruction,
        out Vector3Int targetCell,
        out string targetLabel,
        out bool targetIsEnemyTerritory,
        Vector3Int? plannedObjective = null,
        ConstructionSector? plannedSector = null)
    {
        targetConstruction = null;
        targetCell = default;
        targetLabel = string.Empty;
        targetIsEnemyTerritory = false;

        if (unit == null || snapshot == null || snapshot.KnownConstructions == null || snapshot.KnownConstructions.Count == 0)
            return false;

        if (plannedObjective.HasValue)
        {
            Vector3Int planned = plannedObjective.Value;
            planned.z = 0;
            for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
            {
                AIConstructionInfo info = snapshot.KnownConstructions[i];
                if (info == null || info.Source == null) continue;
                Vector3Int cell = info.Cell;
                cell.z = 0;
                if (cell != planned) continue;
                if (!info.IsCapturable || info.CapturePointsMax <= 0) break;
                if (info.TeamId == unit.TeamId && info.CapturePoints >= info.CapturePointsMax) break;
                targetConstruction = info.Source;
                targetCell = cell;
                targetLabel = !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : info.Source.name;
                targetIsEnemyTerritory = info.TeamId != unit.TeamId;
                return true;
            }
        }

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        TeamId myTeam = unit.TeamId;

        int bestCategory = int.MaxValue;
        int bestDistance = int.MaxValue;
        AIConstructionInfo bestInfo = null;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Source == null)
                continue;

            ConstructionManager construction = info.Source;
            if (plannedSector.HasValue && info.Sector != plannedSector.Value)
                continue;
            if (!construction.IsCapturable || construction.CapturePointsMax <= 0)
                continue;

            Vector3Int cell = info.Cell;
            cell.z = 0;

            int category;
            if (info.TeamId == myTeam)
            {
                if (construction.CurrentCapturePoints >= construction.CapturePointsMax)
                    continue;
                category = 3;
            }
            else if (info.IsHq)
            {
                category = 0;
            }
            else
            {
                bool hasEnemyOnCell = HasVisibleEnemyOnCell(snapshot, cell);
                category = hasEnemyOnCell ? 2 : 1;
            }

            int distance = GetHexDistance(snapshot.BoardTilemap, unitCell, cell, 64);
            if (distance == int.MaxValue)
                distance = 64;

            if (bestInfo == null
                || category < bestCategory
                || (category == bestCategory && distance < bestDistance))
            {
                bestCategory = category;
                bestDistance = distance;
                bestInfo = info;
            }
        }

        if (bestInfo == null || bestInfo.Source == null)
            return false;

        targetConstruction = bestInfo.Source;
        targetCell = bestInfo.Cell;
        targetCell.z = 0;
        targetLabel = !string.IsNullOrWhiteSpace(bestInfo.DisplayName) ? bestInfo.DisplayName : bestInfo.Source.name;
        targetIsEnemyTerritory = bestInfo.TeamId != unit.TeamId;
        return true;
    }
}
