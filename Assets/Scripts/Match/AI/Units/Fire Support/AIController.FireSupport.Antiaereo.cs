using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private static bool IsRangedAntiAirFireSupport(UnitManager unit)
    {
        return HasPrimaryRole(unit, UnitRole.Antiaereo);
    }

    private static bool IsCombatantAntiAirFireSupport(UnitManager unit)
    {
        return HasPrimaryRole(unit, UnitRole.AntiaereoCombatente);
    }

    private static bool HasPrimaryRole(UnitManager unit, UnitRole role)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.roles != null
            && data.roles.Count > 0
            && data.roles[0] == role;
    }

    private PlayerAction TryDecideAntiAirFireSupportAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        if (IsCombatantAntiAirFireSupport(unit))
            return DecideCombatantAntiAirFireSupportAction(unit, snapshot, plan);
        if (!IsRangedAntiAirFireSupport(unit))
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        SectorObjective assigned = ResolveAssignedFireSupportObjective(unit, plan);
        Vector3Int anchor = assigned != null
            ? ResolveFireSupportObjectiveAnchor(assigned, snapshot.AITeam, fromCell)
            : snapshot.MyHQ != null ? snapshot.MyHQ.CurrentCellPosition : fromCell;
        anchor.z = 0;

        if (TryBuildBestFireSupportAttack(
                unit,
                snapshot,
                fromCell,
                paths,
                occupied,
                anchor,
                assigned != null && assigned.Status == ObjectiveStatus.Defending,
                out PlayerAction attackAction,
                out string attackReason,
                optionFilter: IsAirTargetOption))
        {
            Debug.Log($"{TL("Antiaereo")} {unit.InstanceId} controla espaco aereo"
                + $"{FormatAntiAirSector(assigned)} - {attackReason}");
            return attackAction;
        }

        if (TryFindFireSupportMaxRangeThreatCell(
                unit, snapshot, fromCell, paths, occupied,
                out Vector3Int maxRangeCell, out string maxRangeReason))
        {
            Debug.Log($"{TL("Antiaereo")} {unit.InstanceId} reposiciona cobertura max-range"
                + $"{FormatAntiAirSector(assigned)} via {maxRangeCell} ({maxRangeReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, maxRangeCell, paths);
        }

        if (assigned != null
            && TryFindFireSupportRepositionCell(
                unit, snapshot, fromCell, anchor, paths, occupied,
                out Vector3Int supportCell, out string supportReason, assigned: assigned))
        {
            Debug.Log($"{TL("Antiaereo")} {unit.InstanceId} ajusta cobertura"
                + $"{FormatAntiAirSector(assigned)} via {supportCell} ({supportReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, supportCell, paths);
        }

        Debug.Log($"{TL("Antiaereo")} {unit.InstanceId} mantem cobertura"
            + FormatAntiAirSector(assigned));
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private static bool IsAirTargetOption(PodeMirarTargetOption option)
    {
        return option != null
            && option.targetUnit != null
            && option.targetUnit.GetDomain() == Domain.Air;
    }

    private static string FormatAntiAirSector(SectorObjective assigned)
    {
        return assigned != null ? $" em {assigned.Sector}" : " na reserva";
    }
}
