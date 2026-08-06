using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Pontuação e seleção de setores: prioridade, gating, preempção e sorting.
    // -------------------------------------------------------------------------

    private static int CalculateSectorPriority(SectorManager.SectorInfo info, TeamId aiTeam, AIStance stance = AIStance.Tactical)
    {
        float distToAI = info.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
        if (distToAI == float.MaxValue) return 0;

        SectorManager.SectorRiskLevel risk = info.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));

        int riskBonus;
        switch (risk)
        {
            case SectorManager.SectorRiskLevel.Safe:   riskBonus = 40; break;
            case SectorManager.SectorRiskLevel.Low:    riskBonus = 30; break;
            case SectorManager.SectorRiskLevel.Medium: riskBonus = 20; break;
            case SectorManager.SectorRiskLevel.High:   riskBonus = 10; break;
            default:                                   riskBonus =  0; break;
        }

        int distPenalty   = Mathf.RoundToInt(distToAI);
        int disputedBonus = info.IsDisputed ? 15 : 0;

        int buildingValueBonus = 0;
        foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
        {
            if (c.Source == null || c.OwnerTeam == aiTeam) continue;
            if (c.Source.IsPlayerHeadQuarter)
            {
                int hqVal = stance == AIStance.Defensive
                    ? Mathf.RoundToInt(100 * 0.2f)
                    : 100;
                buildingValueBonus += hqVal;
            }
            else if (c.Source.CanProduceUnits)
                buildingValueBonus += 30;
            else if (c.Source.CapturedIncoming > 0)
                buildingValueBonus += 15;
        }
        buildingValueBonus = Mathf.Clamp(buildingValueBonus, 0, 80);

        int stanceBonus = 0;
        switch (stance)
        {
            case AIStance.Defensive:
                stanceBonus += Mathf.RoundToInt(1f / (distToAI + 1f) * 30f);
                if (risk >= SectorManager.SectorRiskLevel.High)
                    stanceBonus -= 30;
                break;
            case AIStance.Offensive:
                if (risk >= SectorManager.SectorRiskLevel.High)
                    stanceBonus += 20;
                else if (risk <= SectorManager.SectorRiskLevel.Low)
                    stanceBonus -= 10;
                break;
        }

        return riskBonus - distPenalty + disputedBonus + buildingValueBonus + stanceBonus;
    }

    private void SortPlanObjectivesByStrategicPriority(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        bool priorityNumberAscending)
    {
        if (plan == null) return;

        plan.Objectives.Sort((a, b) =>
        {
            bool aCritical = IsCriticalHomeDefenseObjective(a, aiTeam);
            bool bCritical = IsCriticalHomeDefenseObjective(b, aiTeam);
            if (aCritical != bCritical) return aCritical ? -1 : 1;

            bool aDefending = a.Status == ObjectiveStatus.Defending;
            bool bDefending = b.Status == ObjectiveStatus.Defending;
            if (aDefending != bDefending) return aDefending ? 1 : -1;

            return priorityNumberAscending
                ? a.Priority.CompareTo(b.Priority)
                : b.Priority.CompareTo(a.Priority);
        });

        for (int i = 0; i < plan.Objectives.Count; i++)
            plan.Objectives[i].Priority = i + 1;
    }

    private static float GetDistanceToAssignedTarget(UnitManager unit, TeamId aiTeam, TeamObjectivePlan plan)
    {
        if (plan == null) return float.MaxValue;
        if (plan.RogueUnitIds.Contains(unit.InstanceId)) return float.MaxValue;

        foreach (SectorObjective obj in plan.Objectives)
        {
            bool assigned = false;
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && slot.AssignedUnitId == unit.InstanceId) { assigned = true; break; }
            if (!assigned) continue;

            ConstructionManager target = FindCapturableInSector(obj.Sector, aiTeam);
            if (target == null) return 0f;

            Vector3Int uCell = unit.CurrentCellPosition; uCell.z = 0;
            Vector3Int tCell = target.CurrentCellPosition; tCell.z = 0;
            return SectorManager.HexDistance(uCell, tCell);
        }

        return float.MaxValue;
    }

    private static bool ShouldDelayEnemyNaturalOpening(
        SectorManager.SectorInfo info,
        TeamId aiTeam,
        AIWorldSnapshot snapshot,
        AIIntelReport intel,
        AIMacroTerritoryContext macro)
    {
        if (info == null || snapshot == null)
            return false;
        if (macro.Phase != AIMacroTerritoryPhase.EarlyExpansion)
            return false;
        if (AISectorIntentAnalyzer.ClassifyRelation(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)), info) != AISectorRelation.EnemyNatural)
            return false;
        if (info.IsDisputed || info.HasPartialCapture)
            return false;

        AISectorIntel sectorIntel = FindIntelForSector(intel, info.Sector);
        if (sectorIntel != null
            && (sectorIntel.capturePressure > 0f
                || sectorIntel.damageTaken > 0f
                || sectorIntel.landingPressure > 0f
                || sectorIntel.enemyPresence >= 2f
                || sectorIntel.hotScore >= 10f))
            return false;

        return true;
    }

    private static bool ShouldDelayEnemyNaturalByFrontier(
        SectorManager.SectorInfo info,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out ConstructionSector requiredRearSector)
    {
        requiredRearSector = ConstructionSector.None;
        if (info == null)
            return false;
        if (AISectorIntentAnalyzer.ClassifyRelation(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)), info) != AISectorRelation.EnemyNatural)
            return false;

        if (TryGetRequiredCampaignPredecessor(info.Sector, aiTeam, out ConstructionSector campaignRear))
        {
            requiredRearSector = campaignRear;
            return !TryGetCampaignPredecessor(info.Sector, plan, aiTeam, out _, out _);
        }

        if (!TryGetSuggestedRearNeighborTowardHQ(info, aiTeam, out SectorManager.SectorInfo rearInfo))
            return false;

        requiredRearSector = rearInfo.Sector;
        return !HasAvailableRearNeighborTowardHQ(info, plan, aiTeam, out _);
    }

    private static bool HasAvailableRearNeighborTowardHQ(
        SectorManager.SectorInfo info,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out ConstructionSector rearSector)
    {
        rearSector = ConstructionSector.None;
        if (info == null)
            return false;

        if (TryGetCampaignPredecessor(info.Sector, plan, aiTeam, out ConstructionSector campaignRear, out _))
        {
            rearSector = campaignRear;
            return true;
        }

        float myHQDist = info.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
        float bestDist = float.MinValue;
        if (TryRearNeighborAvailable(info.ClosestNeighbor1, myHQDist, plan, aiTeam, ref bestDist, out ConstructionSector rear1))
            rearSector = rear1;
        if (TryRearNeighborAvailable(info.ClosestNeighbor2, myHQDist, plan, aiTeam, ref bestDist, out ConstructionSector rear2))
            rearSector = rear2;

        return rearSector != ConstructionSector.None;
    }

    private static bool TryRearNeighborAvailable(
        ConstructionSector sector,
        float currentHQDistance,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        ref float bestDist,
        out ConstructionSector rearSector)
    {
        rearSector = ConstructionSector.None;
        if (sector == ConstructionSector.None || !SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo rearInfo))
            return false;

        float rearDist = rearInfo.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
        if (rearDist >= currentHQDistance || rearDist <= bestDist)
            return false;

        bool available = IsOwnedDefensibleSector(rearInfo, aiTeam);
        if (!available)
        {
            SectorObjective rearObjective = plan != null ? plan.GetObjectiveForSector(rearInfo.Sector) : null;
            available = rearObjective != null
                && rearObjective.Status != ObjectiveStatus.Complete
                && rearObjective.Status != ObjectiveStatus.Abandoned
                && rearObjective.Status != ObjectiveStatus.Defending;
        }

        if (!available)
            return false;

        bestDist = rearDist;
        rearSector = rearInfo.Sector;
        return true;
    }

    private static bool TryGetSuggestedRearNeighborTowardHQ(
        SectorManager.SectorInfo info,
        TeamId aiTeam,
        out SectorManager.SectorInfo rearInfo)
    {
        rearInfo = null;
        if (info == null)
            return false;

        float myHQDist = info.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
        float bestDist = float.MinValue;

        if (info.ClosestNeighbor1 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor1, out SectorManager.SectorInfo n1))
        {
            float d = n1.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
            if (d < myHQDist && d > bestDist)
            {
                rearInfo = n1;
                bestDist = d;
            }
        }

        if (info.ClosestNeighbor2 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor2, out SectorManager.SectorInfo n2))
        {
            float d = n2.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
            if (d < myHQDist && d > bestDist)
            {
                rearInfo = n2;
                bestDist = d;
            }
        }

        return rearInfo != null;
    }

    private void PruneDelayedEnemyNaturalObjectives(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        AIWorldSnapshot snapshot,
        AIIntelReport intel,
        AIMacroTerritoryContext macro)
    {
        if (plan == null || plan.Objectives == null)
            return;

        for (int i = plan.Objectives.Count - 1; i >= 0; i--)
        {
            SectorObjective obj = plan.Objectives[i];
            if (obj == null
                || obj.Status == ObjectiveStatus.Defending
                || obj.Status == ObjectiveStatus.Complete
                || obj.Status == ObjectiveStatus.Abandoned)
                continue;
            if (ConstructionSectorHelper.IsBase(obj.Sector))
                continue;
            if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
                continue;
            bool delayedByOpening = ShouldDelayEnemyNaturalOpening(info, aiTeam, snapshot, intel, macro);
            bool delayedByFrontier = ShouldDelayEnemyNaturalByFrontier(info, plan, aiTeam, out ConstructionSector rearSector);
            if (!delayedByOpening && !delayedByFrontier)
                continue;

            ClearObjectiveHUD(obj);
            plan.Objectives.RemoveAt(i);
            string reason = delayedByFrontier
                ? $"fronteira exige {rearSector} antes"
                : "EarlyExpansion EnemyNatural sem pressao local";
            Debug.Log($"{TL("Plan")} objetivo adiado: {obj.Sector} {reason}");
        }
    }

    private static bool ShouldDelayEnemyBaseOpening(
        SectorManager.SectorInfo info,
        TeamId aiTeam,
        AIIntelReport intel,
        AIMacroTerritoryContext macro,
        bool hasPartialOwnCapture)
    {
        if (info == null)
            return false;
        if (macro.Phase != AIMacroTerritoryPhase.EarlyExpansion)
            return false;
        if (!ConstructionSectorHelper.IsBase(info.Sector))
            return false;
        if (FindHQTeamInSector(info.Sector) == aiTeam)
            return false;
        if (hasPartialOwnCapture || info.IsDisputed || info.HasPartialCapture)
            return false;

        AISectorIntel sectorIntel = FindIntelForSector(intel, info.Sector);
        if (sectorIntel != null
            && (sectorIntel.capturePressure > 0f
                || sectorIntel.damageTaken > 0f
                || sectorIntel.landingPressure > 0f
                || sectorIntel.enemyPresence >= 3f
                || sectorIntel.hotScore >= 12f))
            return false;

        return true;
    }

    private bool TryPreemptLowUrgencyObjectiveForHotCandidate(
        TeamObjectivePlan plan,
        SectorObjective candidate,
        TeamId aiTeam,
        AIIntelReport intel,
        AIRallyPlanContext rallyContext,
        int turn,
        int maxObj,
        out SectorObjective removed)
    {
        removed = null;
        if (plan == null || candidate == null || !IsPreemptiveHotCandidate(candidate, intel, rallyContext))
            return false;

        SectorObjective best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < plan.Objectives.Count; i++)
        {
            SectorObjective current = plan.Objectives[i];
            if (!IsLowUrgencyPreemptableObjective(current, aiTeam, intel))
                continue;

            float score = ScorePreemptableObjective(current, aiTeam, intel, turn);
            if (best == null || score > bestScore)
            {
                best = current;
                bestScore = score;
            }
        }

        if (best == null)
            return false;

        ClearObjectiveHUD(best);
        plan.Objectives.Remove(best);
        removed = best;
        Debug.Log($"{TL("Plan")} cap preempt ({maxObj}): {candidate.Sector} hot substitui {best.Sector} score={bestScore:F0}");
        return true;
    }

    private static bool IsPreemptiveHotCandidate(SectorObjective candidate, AIIntelReport intel, AIRallyPlanContext rallyContext)
    {
        AISectorIntel entry = FindIntelForSector(intel, candidate.Sector);
        if (entry == null)
            return false;

        return entry.hotScore >= 10f
            || entry.capturePressure >= 4f
            || entry.damageTaken >= 4f
            || entry.landingPressure >= 4f;
    }

    private static bool IsLowUrgencyPreemptableObjective(SectorObjective obj, TeamId aiTeam, AIIntelReport intel)
    {
        if (obj == null || obj.Status == ObjectiveStatus.Defending || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
            return false;
        if (ConstructionSectorHelper.IsBase(obj.Sector))
            return false;
        if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
            return false;
        if (info.ControllingTeam != TeamId.Neutral)
            return false;
        if (info.HasPartialCapture || info.IsDisputed)
            return false;
        if (AISectorIntentAnalyzer.ClassifyRelation(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)), info) != AISectorRelation.OwnNatural)
            return false;
        if (IsHotPlanIntelSector(FindIntelForSector(intel, obj.Sector)))
            return false;

        return true;
    }

    private static float ScorePreemptableObjective(SectorObjective obj, TeamId aiTeam, AIIntelReport intel, int turn)
    {
        float score = obj != null ? obj.Priority * 100f : 0f;
        if (obj == null)
            return score;

        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            score += Mathf.Clamp(20f - info.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))), 0f, 20f);
            score -= info.GetRiskRatioFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) * 50f;
        }

        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled) continue;
            UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (unit == null) continue;
            if (unit.HasActed) score -= 30f;
            score -= Mathf.Clamp(unit.CurrentHP, 0, 10);
        }

        if (IsRecentlyCapturedSector(aiTeam, obj.Sector, turn))
            score -= 100f;

        return score;
    }
}
