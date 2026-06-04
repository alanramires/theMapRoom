using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private bool TryBuildRepairLastStandAttack(
        UnitManager unit,
        TeamId aiTeam,
        Vector3Int fromCell,
        ConstructionManager currentConstruction,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (unit == null)
            return false;

        if (currentConstruction != null && currentConstruction.TeamId == aiTeam)
            return false;

        if (HasAnyUnoccupiedRepairMove(fromCell, paths, occupied))
            return false;

        var targets = new List<PodeMirarTargetOption>();
        if (!PodeMirarSensor.CollectTargets(
                unit,
                boardTilemap,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                targets,
                fromCell: fromCell) || targets.Count == 0)
            return false;

        UnitManager bestTarget = null;
        float bestPriority = float.MinValue;
        foreach (PodeMirarTargetOption opt in targets)
        {
            if (opt?.targetUnit == null) continue;
            if (!PassesAttackDecision(unit, opt.targetUnit, fromCell, true, out _)) continue;

            Vector3Int targetCell = opt.targetUnit.CurrentCellPosition;
            targetCell.z = 0;
            float priority = AttackTargetPriority(targetCell, fromCell) * 1000f
                + Mathf.Max(0, 20 - opt.targetUnit.CurrentHP) * 25f
                - opt.distance * 5f
                - opt.targetUnit.InstanceId * 0.001f;

            if (priority > bestPriority)
            {
                bestPriority = priority;
                bestTarget = opt.targetUnit;
            }
        }

        if (bestTarget == null)
            return false;

        Vector3Int bestTargetCell = bestTarget.CurrentCellPosition;
        bestTargetCell.z = 0;
        Debug.Log($"{TL("Repair")} {unit.InstanceId} cercado fora de construcao aliada - ultimo recurso: ataca {bestTarget.UnitDisplayName}#{bestTarget.InstanceId}");
        action = BuildAttackBatch(unit, aiTeam, fromCell, fromCell, bestTarget.InstanceId.ToString(), bestTargetCell, paths);
        return true;
    }


    private static bool HasAnyUnoccupiedRepairMove(
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (paths == null || paths.Count == 0)
            return false;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;

            if (occupied != null && occupied.Contains(cell))
                continue;

            return true;
        }

        return false;
    }


    private bool TryDecideRepairHoldHomeDefense(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamId aiTeam,
        Vector3Int fromCell,
        out PlayerAction action)
    {
        action = null;
        if (!IsRepairUnitInThreatenedOwnHqArea(snapshot, fromCell, aiTeam))
            return false;

        if (HasAttackTargetAtCurrentPos(unit))
        {
            var targets = new List<PodeMirarTargetOption>();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, targets);

            UnitManager bestTarget = null;
            float bestPriority = float.MinValue;
            foreach (PodeMirarTargetOption opt in targets)
            {
                if (opt?.targetUnit == null) continue;
                if (!PassesAttackDecision(unit, opt.targetUnit, fromCell, true, out _)) continue;

                Vector3Int targetCell = opt.targetUnit.CurrentCellPosition;
                targetCell.z = 0;
                float priority = AttackTargetPriority(targetCell, fromCell);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestTarget = opt.targetUnit;
                }
            }

            if (bestTarget != null)
            {
                Vector3Int targetCell = bestTarget.CurrentCellPosition;
                targetCell.z = 0;
                Debug.Log($"{TL("Repair")} {unit.InstanceId} segura base/HQ em {fromCell} sob ameaca - ataca {bestTarget.UnitDisplayName}#{bestTarget.InstanceId}");
                action = BuildAttackBatch(unit, aiTeam, fromCell, fromCell, bestTarget.InstanceId.ToString(), targetCell);
                return true;
            }
        }

        Debug.Log($"{TL("Repair")} {unit.InstanceId} segura base/HQ em {fromCell} sob ameaca");
        action = BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        return true;
    }


    private bool IsRepairUnitInThreatenedOwnHqArea(AIWorldSnapshot snapshot, Vector3Int fromCell, TeamId aiTeam)
    {
        if (snapshot == null || snapshot.MyHQ == null)
            return false;

        fromCell.z = 0;
        Vector3Int hqCell = snapshot.MyHQ.CurrentCellPosition;
        hqCell.z = 0;
        ConstructionSector hqSector = snapshot.MyHQ.Sector;

        ConstructionManager current = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);
        if (current != null
            && current.Sector == hqSector
            && IsHomeDefenseThreatened(hqSector, aiTeam, HomeDefenseThreatRange))
            return true;

        if (SectorManager.HexDistance(fromCell, hqCell) <= HomeDefenseThreatRange
            && IsHomeDefenseThreatened(hqSector, aiTeam, HomeDefenseThreatRange))
            return true;

        return false;
    }


    private bool IsRepairUnitInOwnHomeArea(AIWorldSnapshot snapshot, Vector3Int fromCell, TeamId aiTeam)
    {
        fromCell.z = 0;

        ConstructionManager current = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);
        if (current != null && current.TeamId == aiTeam
            && (current.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(current.Sector)))
            return true;

        if (snapshot?.MyHQ != null)
        {
            Vector3Int hqCell = snapshot.MyHQ.CurrentCellPosition; hqCell.z = 0;
            if (SectorManager.HexDistance(fromCell, hqCell) <= HomeDefenseThreatRange)
                return true;
        }

        return false;
    }


}
