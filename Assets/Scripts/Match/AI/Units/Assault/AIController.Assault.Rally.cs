using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int RallyAssemblyAssaultRadius = 2;

    private PlayerAction DecideRallyAssemblyAssaultAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Vector3Int rallyAnchor = ResolveRallyAssemblyAnchor(assigned, snapshot.AITeam, fromCell);
        int rallyRadius = ResolveRallyAssemblyAssaultRadius(unit);

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction vacateAction))
            return vacateAction;

        List<UnitManager> threats = CollectAssaultEscortThreats(snapshot.AITeam, rallyAnchor, rallyRadius);
        bool localDefenseContext = true;
        if (TryFindAssaultEscortAttack(unit, snapshot, fromCell, rallyAnchor, rallyRadius, localDefenseContext, paths, occupied, threats,
                out Vector3Int attackCell, out UnitManager attackTarget, out string attackReason))
        {
            Vector3Int targetCell = attackTarget.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - protege perimetro via {attackCell} -> {attackTarget.UnitDisplayName}#{attackTarget.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                attackTarget.InstanceId.ToString(), targetCell, paths);
        }

        List<Vector3Int> suspectCells = CollectSweepSuspectCells(snapshot.AITeam, rallyAnchor, rallyRadius);
        if (TryFindAssaultScoutRevealMove(unit, snapshot, fromCell, rallyAnchor, rallyRadius, paths, occupied, suspectCells,
                out Vector3Int revealCell, out string revealReason))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - varre perimetro via {revealCell} ({revealReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, revealCell, paths);
        }

        Vector3Int coverCell = FindAssaultEscortCoverCell(
            unit,
            snapshot,
            fromCell,
            rallyAnchor,
            rallyRadius,
            paths,
            occupied,
            threats,
            bestCapturerDist: -1,
            out string coverEvaluationLog);

        if (!string.IsNullOrEmpty(coverEvaluationLog))
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - perimetro target={rallyAnchor} raio={rallyRadius}h\n{coverEvaluationLog}");

        if (coverCell != fromCell)
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - monta perimetro via {coverCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, coverCell, paths);
        }

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - segura perimetro @ {fromCell}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private int ResolveRallyAssemblyAssaultRadius(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return Mathf.Max(RallyAssemblyAssaultRadius, Mathf.Min(3, data.movement));

        return RallyAssemblyAssaultRadius;
    }
}
