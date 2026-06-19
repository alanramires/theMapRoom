using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private static bool IsCombatantFireSupport(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.roles != null
            && data.roles.Count > 0
            && data.roles[0] == UnitRole.ArtilheiroCombatente;
    }

    private bool TryDecideCombatantFireSupportTacticalAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int anchor,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool defensiveContext,
        out PlayerAction action)
    {
        action = null;
        if (!IsCombatantFireSupport(unit) || snapshot == null || paths == null)
            return false;

        if (TryBuildBestFireSupportAttack(
                unit, snapshot, fromCell, paths, occupied, anchor, defensiveContext,
                out PlayerAction longRangeAction, out string longRangeReason,
                stationaryOnly: true,
                optionFilter: opt => opt != null && opt.distance > 1))
        {
            Debug.Log($"{TL("ArtilheiroCombatente")} {unit.InstanceId} prioriza tiro distante"
                + $"{FormatCombatantSector(assigned)} - {longRangeReason}");
            action = longRangeAction;
            return true;
        }

        int assaultRadius = ResolveAssaultScoutZoneRadius(unit, assigned);
        List<UnitManager> threats = CollectAssaultEscortThreats(
            snapshot.AITeam, anchor, assaultRadius);
        AddAssaultEscortTravelThreats(snapshot.AITeam, fromCell, paths, threats);
        if (TryFindAssaultEscortAttack(
                unit,
                snapshot,
                fromCell,
                anchor,
                assaultRadius,
                defensiveContext,
                paths,
                occupied,
                threats,
                out Vector3Int assaultCell,
                out UnitManager assaultTarget,
                out string assaultReason))
        {
            Vector3Int targetCell = assaultTarget.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("ArtilheiroCombatente")} {unit.InstanceId} sem tiro distante, modo assalto"
                + $"{FormatCombatantSector(assigned)} via {assaultCell} -> "
                + $"{assaultTarget.UnitDisplayName}#{assaultTarget.InstanceId} ({assaultReason})");
            action = BuildAttackBatch(
                unit,
                snapshot.AITeam,
                fromCell,
                assaultCell,
                assaultTarget.InstanceId.ToString(),
                targetCell,
                paths);
            return true;
        }

        if (TryFindFireSupportMaxRangeThreatCell(
                unit, snapshot, fromCell, paths, occupied,
                out Vector3Int maxRangeCell, out string maxRangeReason))
        {
            Debug.Log($"{TL("ArtilheiroCombatente")} {unit.InstanceId} sem ataque, posiciona max-range"
                + $"{FormatCombatantSector(assigned)} via {maxRangeCell} ({maxRangeReason})");
            action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, maxRangeCell, paths);
            return true;
        }

        return false;
    }

    private static string FormatCombatantSector(SectorObjective assigned)
    {
        return assigned != null ? $" em {assigned.Sector}" : " rogue";
    }
}
