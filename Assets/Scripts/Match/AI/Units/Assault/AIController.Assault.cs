using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int AssaultScoutZoneRadius = 2;
    // Se qualquer slot de Capturador no objetivo tiver DistanceToObjective ≤ esse limiar,
    // o escort entra em "advance mode": prioriza avançar ao objetivo em vez de patrulhar.
    private const int AdvancedCapturerThreshold = 6;
    // Penalidade por congestionamento à frente: vizinhos com custo menor (próximo passo de rota)
    // que estão bloqueados por aliados. Ratio 0..1 × esse peso é subtraído do score.
    private const float ForwardCongestionWeight = 700f;

    // -------------------------------------------------------------------------
    // Assalto Batedor - protege e varre a zona do objetivo de captura atribuido.
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideAssaultAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null || plan == null)
            return null;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Assalto)
            return null;

        SectorObjective assigned = ResolveAssignedAssaultObjective(unit, plan);
        if (assigned == null)
        {
            if (TryFindCriticalHomeDefenseObjectiveForUnit(plan, snapshot.AITeam, unit, unit.CurrentCellPosition, "Assalto Rogue", out SectorObjective rogueCriticalHome))
            {
                Debug.Log($"{TL("Assalto")} {unit.InstanceId} rogue redireciona -> {rogueCriticalHome.Sector}: Base/HQ sob ameaca");
                return DecideAssignedAssaultEscortAction(unit, snapshot, rogueCriticalHome);
            }

            PlayerAction embarkAction = TryDecideAssaultEmbarkAction(unit, snapshot, plan);
            if (embarkAction != null) return embarkAction;
            return DecideRogueAssaultBreakerAction(unit, snapshot, plan);
        }

        if (!IsCriticalHomeDefenseObjective(assigned, snapshot.AITeam)
            && TryFindCriticalHomeDefenseObjectiveForUnit(plan, snapshot.AITeam, unit, unit.CurrentCellPosition, "Assalto", out SectorObjective criticalHome))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} redireciona {assigned.Sector} -> {criticalHome.Sector}: Base/HQ sob ameaca");
            assigned = criticalHome;
        }

        if (IsRallyAssemblyObjective(assigned))
            return DecideRallyAssemblyAssaultAction(unit, snapshot, assigned);

        return DecideAssignedAssaultEscortAction(unit, snapshot, assigned);
    }

    private PlayerAction DecideAssignedAssaultEscortAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Vector3Int scoutAnchorCell = ResolveAssaultEscortCell(assigned, snapshot.AITeam, fromCell);
        int scoutZoneRadius = ResolveAssaultScoutZoneRadius(unit, assigned);

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // Se o escort está no corredor de avanço do capturador, exclui a célula atual
        // do patrol para forçar movimento real e liberar o caminho.
        if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction assignedVacateAction))
            return assignedVacateAction;

        TeamObjectivePlan escortPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        bool inCorridor = escortPlan != null && IsAssaultEscortInCapturerCorridor(unit, fromCell, escortPlan, snapshot.AITeam);
        if (inCorridor)
        {
            occupied.Add(fromCell);
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — cede corredor, força movimento");
        }

        List<UnitManager> threats = CollectAssaultEscortThreats(snapshot.AITeam, scoutAnchorCell, scoutZoneRadius);
        AddAssaultEscortTravelThreats(snapshot.AITeam, fromCell, paths, threats);
        bool defensiveContext = assigned.Status == ObjectiveStatus.Defending;
        if (TryFindAssaultEscortAttack(unit, snapshot, fromCell, scoutAnchorCell, scoutZoneRadius, defensiveContext, paths, occupied, threats,
                out Vector3Int attackCell, out UnitManager attackTarget, out string attackReason))
        {
            Vector3Int targetCell = attackTarget.CurrentCellPosition; targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — ataca via {attackCell} → {attackTarget.UnitDisplayName}#{attackTarget.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                attackTarget.InstanceId.ToString(), targetCell, paths);
        }

        int bestCapturerDist = GetBestCapturerDistanceToObjective(assigned);
        bool escortAdvanceMode = bestCapturerDist >= 0 && bestCapturerDist <= AdvancedCapturerThreshold;
        if (escortAdvanceMode)
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — ADVANCE MODE: capturador mais próximo a {bestCapturerDist}PM de {assigned.Sector}");

        if (escortAdvanceMode
            && TryFindAssaultAdvanceRouteAttack(unit, snapshot, fromCell, scoutAnchorCell, defensiveContext, paths, occupied,
                out Vector3Int routeAttackCell, out UnitManager routeAttackTarget, out string routeAttackReason))
        {
            Vector3Int targetCell = routeAttackTarget.CurrentCellPosition; targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — intercepta via {routeAttackCell} → {routeAttackTarget.UnitDisplayName}#{routeAttackTarget.InstanceId} ({routeAttackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, routeAttackCell,
                routeAttackTarget.InstanceId.ToString(), targetCell, paths);
        }

        List<Vector3Int> suspectCells = CollectSweepSuspectCells(snapshot.AITeam, scoutAnchorCell, scoutZoneRadius);
        if (TryFindAssaultScoutRevealMove(unit, snapshot, fromCell, scoutAnchorCell, scoutZoneRadius, paths, occupied, suspectCells,
                out Vector3Int revealCell, out string revealReason))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — abre FoW via {revealCell} ({revealReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, revealCell, paths);
        }

        Vector3Int coverCell = FindAssaultEscortCoverCell(unit, snapshot, fromCell, scoutAnchorCell, scoutZoneRadius, paths, occupied, threats, bestCapturerDist, out string coverEvaluationLog);
        if (!string.IsNullOrEmpty(coverEvaluationLog))
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — HexEvaluator.Batedor target={scoutAnchorCell} zona={scoutZoneRadius}h advanceMode={escortAdvanceMode} melhorCapt={bestCapturerDist}PM\n{coverEvaluationLog}");
        if (coverCell != fromCell)
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — patrulha via {coverCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, coverCell, paths);
        }

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — mantém patrulha");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private static SectorObjective ResolveAssignedAssaultObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Assalto && slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                    return obj;
        return null;
    }

    // Retorna o menor DistanceToObjective entre os slots de Capturador preenchidos no objetivo.
    // Retorna -1 se nenhum capturador tiver distância conhecida.
    private static int GetBestCapturerDistanceToObjective(SectorObjective obj)
    {
        int best = int.MaxValue;
        foreach (SlotNeed slot in obj.Slots)
        {
            if (slot.Role != UnitRole.Capturador || !slot.Filled || slot.DistanceToObjective < 0) continue;
            if (slot.DistanceToObjective < best) best = slot.DistanceToObjective;
        }
        return best == int.MaxValue ? -1 : best;
    }
}
