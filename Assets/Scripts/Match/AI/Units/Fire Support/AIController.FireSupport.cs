using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fire Support - entrada principal
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideFireSupportAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!IsFireSupportUnit(unit)) return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> vacatePaths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> vacateOccupied = BuildOccupied(unit);
        if (vacatePaths != null && vacatePaths.Count > 0)
        {
            if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, vacatePaths, vacateOccupied, out PlayerAction vacateAction))
                return vacateAction;
        }

        TeamObjectivePlan capBlockPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        if (capBlockPlan != null && IsCellACapturerTarget(fromCell, capBlockPlan, snapshot.AITeam))
        {
            Vector3Int vacateCell = FindFireSupportCapturerVacateCell(unit, snapshot, fromCell, capBlockPlan, vacatePaths ?? BuildFireSupportPaths(unit), vacateOccupied);
            if (vacateCell != fromCell)
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} bloqueia capturador em {fromCell} — vacate via {vacateCell}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, vacateCell, vacatePaths);
            }
        }

        PlayerAction repairAction = TryDecideRepairAction(unit, snapshot, plan);
        if (repairAction != null) return repairAction;

        SectorObjective assigned = ResolveAssignedFireSupportObjective(unit, plan);
        if (assigned == null)
        {
            if (TryDecideRallyAssemblyFireSupportAction(unit, snapshot, plan, fromCell, vacatePaths, vacateOccupied, out PlayerAction rallyAction))
                return rallyAction;

            PlayerAction embarkAction = TryDecideFireSupportEmbarkAction(unit, snapshot, plan);
            if (embarkAction != null) return embarkAction;
            return DecideRogueFireSupportAction(unit, snapshot);
        }

        if (assigned.Status == ObjectiveStatus.Defending)
            return DecideFireSupportDefenderAction(unit, snapshot, assigned);

        return DecideAssignedFireSupportAction(unit, snapshot, plan, assigned);
    }

    private PlayerAction TryDecideFireSupportAttackOnlyAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!IsFireSupportUnit(unit) || unit == null || snapshot == null)
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        SectorObjective assigned = ResolveAssignedFireSupportObjective(unit, plan);
        if (assigned == null)
            assigned = ResolveAssignedAssaultObjective(unit, plan);

        Vector3Int anchor = assigned != null
            ? ResolveFireSupportObjectiveAnchor(assigned, snapshot.AITeam, fromCell)
            : ResolveRogueFireSupportAnchor(snapshot, fromCell);

        if (IsArtilleryModeOnly(unit)
            && TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                assigned != null && assigned.Status == ObjectiveStatus.Defending,
                out PlayerAction indirectAction, out string indirectReason, indirectOnly: true))
        {
            string sector = assigned != null ? assigned.Sector.ToString() : "rogue";
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} hibrido prioriza tiro indireto {sector} - {indirectReason}");
            return indirectAction;
        }

        if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
            assigned != null && assigned.Status == ObjectiveStatus.Defending,
            out PlayerAction attackAction, out string attackReason))
        {
            string sector = assigned != null ? assigned.Sector.ToString() : "rogue";
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} hibrido prioriza tiro {sector} - {attackReason}");
            return attackAction;
        }

        return null;
    }

    private PlayerAction DecideAssignedFireSupportAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int anchor = ResolveFireSupportObjectiveAnchor(assigned, snapshot.AITeam, fromCell);

        if (IsArtilleryModeOnly(unit)
            && TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                assigned.Status == ObjectiveStatus.Defending, out PlayerAction stationaryAttackAction,
                out string stationaryAttackReason, stationaryOnly: true))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} apoia {assigned.Sector} (modo artilharia) - {stationaryAttackReason}");
            return stationaryAttackAction;
        }

        if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor, assigned.Status == ObjectiveStatus.Defending, out PlayerAction attackAction, out string attackReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} apoia {assigned.Sector} - {attackReason}");
            return attackAction;
        }

        // Missing screen is a planning signal now; rendezvous/repositioning decides the movement.
        if (assigned.Status != ObjectiveStatus.Defending
            && AITacticalAnalyzer.Instance != null
            && !AITacticalAnalyzer.Instance.IsFireSupportScreenedForObjective(unit, snapshot.AITeam, assigned, out _, out string screenReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} {assigned.Sector}: screen ausente, segue reposicionamento/rendezvous ({screenReason})");
        }

        if (TryBuildFireSupportBlockedShotRepositionAction(unit, snapshot, fromCell, paths, occupied, assigned.Status == ObjectiveStatus.Defending, out PlayerAction blockedShotAction, out string blockedShotReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} reposiciona para linha de tiro {assigned.Sector} - {blockedShotReason}");
            return blockedShotAction;
        }

        PlayerAction embarkAction = TryDecideFireSupportEmbarkAction(unit, snapshot, plan, assigned);
        if (embarkAction != null) return embarkAction;

        if (TryRogueFireSupportKnownTargetRangeStep(unit, snapshot, fromCell, paths, occupied,
                assigned,
                anchor,
                out Vector3Int assignedRangeStepCell, out string assignedRangeStepReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} reposiciona para por alvo na mira {assigned.Sector} via {assignedRangeStepCell} ({assignedRangeStepReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, assignedRangeStepCell, paths);
        }

        Vector3Int supportAnchor = anchor;
        string supportAnchorReason = null;
        if (assigned.Status != ObjectiveStatus.Defending
            && TryResolveFireSupportLiveSupportAnchor(unit, snapshot, assigned, anchor, out Vector3Int liveAnchor, out supportAnchorReason))
        {
            supportAnchor = liveAnchor;
        }

        if (TryFindFireSupportRepositionCell(unit, snapshot, fromCell, supportAnchor, paths, occupied,
            out Vector3Int moveCell, out string moveReason, assigned: assigned))
        {
            string anchorText = supportAnchorReason != null ? $" anchor={supportAnchor} {supportAnchorReason}; " : "";
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} reposiciona para apoiar {assigned.Sector} via {moveCell} ({anchorText}{moveReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        if (TryFindFireSupportMaxRangeThreatCell(unit, snapshot, fromCell, paths, occupied, out Vector3Int maxRangeCell, out string maxRangeReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} postura max-range {assigned.Sector} via {maxRangeCell} ({maxRangeReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, maxRangeCell, paths);
        }

        PlayerAction rendezvousAction = TryFireSupportRendezvousAction(unit, snapshot, assigned, fromCell, paths, occupied);
        if (rendezvousAction != null) return rendezvousAction;

        PlayerAction cohesionAction = TryFireSupportCohesionFallbackAction(unit, snapshot, assigned, fromCell, supportAnchor, paths, occupied);
        if (cohesionAction != null) return cohesionAction;

        Debug.Log($"{TL("FireSupport")} {unit.InstanceId} aguarda apoio {assigned.Sector}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    // Rendezvous: move em direção ao aliado do plano mais próximo quando não há ação de artilharia.
    private PlayerAction TryFireSupportRendezvousAction(
        UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned,
        Vector3Int fromCell, Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied)
    {
        if (paths == null || paths.Count == 0 || assigned == null) return null;
        if (PreferFireSupportWeaponMaxRange(unit) || IsFireSupportConservative(unit))
            return null;

        UnitManager rendezvousTarget = null;
        float bestDist = float.MaxValue;

        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!slot.Filled || slot.AssignedUnitId == unit.InstanceId) continue;
            UnitManager ally = FindActiveUnit(slot.AssignedUnitId, snapshot.AITeam);
            if (ally == null || ally.IsDead || ally.IsEmbarked) continue;
            if (IsBacklineSupportUnit(ally)) continue;
            Vector3Int allyCell = ally.CurrentCellPosition; allyCell.z = 0;
            float dist = SectorManager.HexDistance(fromCell, allyCell);
            if (dist < bestDist) { bestDist = dist; rendezvousTarget = ally; }
        }

        if (rendezvousTarget == null || bestDist <= 1f) return null;

        Vector3Int targetCell = rendezvousTarget.CurrentCellPosition; targetCell.z = 0;
        TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        float fromThreat = CalculateThreatLevel(fromCell, snapshot.AITeam);

        // Try progression tool first — avoids backward movement toward rendezvous target.
        if (TryFindBestToolProgressionCell(
                unit,
                snapshot,
                fromCell,
                targetCell,
                paths,
                occupied,
                ToolProgressionIntent.FireSupportRendezvous,
                out Vector3Int toolCell,
                out ToolProgressionCandidate toolCandidate,
                out string toolReason,
                allowCell: cell => !IsCellACapturerTarget(cell, capPlan, snapshot.AITeam))
            && toolCell != fromCell
            && (toolCandidate.ToolScore > 0 || toolCandidate.FirstTurnProgress > 0f || toolCandidate.TwoTurnProgress > 0f)
            && CalculateThreatLevel(toolCell, snapshot.AITeam) <= fromThreat + 0.1f)
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rendezvous {assigned.Sector} → #{rendezvousTarget.InstanceId} via {toolCell} (progressão {toolReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, toolCell, paths);
        }

        // Fallback: pressure move (no tool progress available).
        // Only accept if it actually closes distance to the rendezvous target.
        Vector3Int moveCell = FindAssaultPressureMove(unit, snapshot, fromCell, targetCell, paths, occupied, out _);
        if (moveCell == fromCell) return null;
        if (SectorManager.HexDistance(moveCell, targetCell) >= SectorManager.HexDistance(fromCell, targetCell))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rendezvous {assigned.Sector} fallback sem progresso via {moveCell}, fica parado");
            return null;
        }
        if (IsCellACapturerTarget(moveCell, capPlan, snapshot.AITeam))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rendezvous {assigned.Sector} evita predio reservado {moveCell} - sem alternativa");
            return null;
        }
        if (CalculateThreatLevel(moveCell, snapshot.AITeam) > fromThreat + 0.1f)
            return null;

        Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rendezvous {assigned.Sector} → #{rendezvousTarget.InstanceId} via {moveCell} (fallback)");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
    }

    private PlayerAction TryFireSupportCohesionFallbackAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int anchor,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (unit == null || snapshot == null || assigned == null || paths == null || paths.Count == 0)
            return null;
        if (!IsFireSupportConservative(unit) && !PreferFireSupportWeaponMaxRange(unit))
            return null;

        TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        float fromThreat = CalculateThreatLevel(fromCell, snapshot.AITeam);
        float fromAnchorDist = SectorManager.HexDistance(fromCell, anchor);
        float fromCohesion = CalculateFireSupportCohesionScore(unit, snapshot, fromCell);
        float fromNearestAlly = FindNearestNonFireSupportAllyDistance(unit, snapshot, fromCell);
        bool conservativeOffensiveObjective = IsFireSupportConservative(unit)
            && assigned.Status != ObjectiveStatus.Defending
            && assigned.Status != ObjectiveStatus.Complete
            && assigned.Status != ObjectiveStatus.Abandoned;

        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        string bestReason = "";

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;
            if (IsCellACapturerTarget(cell, capPlan, snapshot.AITeam)) continue;
            if (!IsFireSupportConservativeCellAllowed(unit, snapshot, cell)) continue;

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            if (threat > fromThreat + 0.1f)
                continue;

            float cellAnchorDist = SectorManager.HexDistance(cell, anchor);
            bool advancesToObjective = cellAnchorDist < fromAnchorDist - 0.5f;
            if (conservativeOffensiveObjective
                && advancesToObjective
                && !HasAlliedScreenAheadOfFireSupportCell(unit, snapshot, cell, anchor))
                continue;

            float nearestAlly = FindNearestNonFireSupportAllyDistance(unit, snapshot, cell);
            if (nearestAlly >= float.MaxValue)
                continue;

            float cohesion = CalculateFireSupportCohesionScore(unit, snapshot, cell);
            float cohesionGain = cohesion - fromCohesion;
            float allyGain = fromNearestAlly < float.MaxValue ? fromNearestAlly - nearestAlly : 0f;
            if (conservativeOffensiveObjective && cellAnchorDist > fromAnchorDist + 0.1f)
                continue;

            float rearBias = conservativeOffensiveObjective
                ? 0f
                : Mathf.Max(0f, cellAnchorDist - fromAnchorDist) * 35f;
            float pathCost = GetPathStepCount(paths, cell);
            float score = cohesionGain
                + allyGain * 180f
                + rearBias
                + GetTerrainDpqPontos(cell) * 18f
                - threat * 120f
                - pathCost * 8f;

            if (allyGain <= 0.1f && cohesionGain < 45f)
                continue;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestReason = $"allyGain={allyGain:F1} cohGain={cohesionGain:F0} threat={threat:F1}";
            }
        }

        if (bestCell == fromCell || bestScore < 35f)
            return null;

        Debug.Log($"{TL("FireSupport")} {unit.InstanceId} reagrupa/cohesion {assigned.Sector} via {bestCell} anchor={anchor} ({bestReason} score={bestScore:F0})");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestCell, paths);
    }

    private static float FindNearestNonFireSupportAllyDistance(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return float.MaxValue;

        float best = float.MaxValue;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (IsBacklineSupportUnit(ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            best = Mathf.Min(best, SectorManager.HexDistance(cell, allyCell));
        }

        return best;
    }

    private bool TryDecideRallyAssemblyFireSupportAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (!TryResolveRallyInfluence(plan, snapshot.AITeam, fromCell, includeGoGreen: false, out AIRallyInfluence rally)
            || !rally.Active
            || !IsRallyAssemblingState(rally.State))
            return false;

        if (paths == null || paths.Count == 0)
            return false;

        if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, rally.Anchor,
                defensiveContext: true, out PlayerAction attackAction, out string attackReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rally {rally.Sector} cobre montagem - {attackReason}");
            action = attackAction;
            return true;
        }

        if (TryFindFireSupportRepositionCell(unit, snapshot, fromCell, rally.Anchor, paths, occupied,
                out Vector3Int moveCell, out string moveReason, assigned: null, moveMarginOverride: 45f))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rally {rally.Sector} monta retaguarda via {moveCell} ({rally.Reason}; {moveReason})");
            action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
            return true;
        }

        if (SectorManager.HexDistance(fromCell, rally.Anchor) <= rally.SupportRadius + 1f)
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rally {rally.Sector} segura cobertura @ {fromCell} ({rally.Reason})");
            action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
            return true;
        }

        return false;
    }
}
