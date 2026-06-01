using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int AnchorSectorRecoveryPriorityBonus = 90;
    private const int AnchorSectorEarlyPriorityBonus = 45;
    private const int AnchorSectorDefaultPriorityBonus = 20;

    private struct AIAnchorPlanContext
    {
        public HashSet<ConstructionSector> OwnAnchorSectors;
        public int AnchorCount;
    }

    private static AIAnchorPlanContext BuildAnchorPlanContext(TeamId aiTeam, int turnNumber)
    {
        AIAnchorPlanContext context = new AIAnchorPlanContext
        {
            OwnAnchorSectors = new HashSet<ConstructionSector>()
        };

        HashSet<int> ownSlots = CollectOwnHQSlots(aiTeam);
        if (ownSlots.Count == 0)
            return context;

        foreach (ConstructionManager anchor in ConstructionManager.AllActive)
        {
            if (anchor == null || !anchor.IsAnchorSector)
                continue;

            if (anchor.Sector == ConstructionSector.None)
            {
                Debug.Log($"[AI Anchor][T{turnNumber}][{aiTeam}] ignorado {anchor.name}: anchor sem setor");
                continue;
            }

            int anchorSlot = anchor.AnchorSectorSlotIndex;
            if (!ownSlots.Contains(anchorSlot))
                continue;

            context.OwnAnchorSectors.Add(anchor.Sector);
            context.AnchorCount++;

            bool held = TryGetAnySectorInfo(anchor.Sector, out SectorManager.SectorInfo info)
                && info.IsFullyControlled
                && info.ControllingTeam == aiTeam;
            Debug.Log($"[AI Anchor][T{turnNumber}][{aiTeam}] {anchor.Sector} via {anchor.name} slot={anchorSlot} held={held}");
        }

        return context;
    }

    private static HashSet<int> CollectOwnHQSlots(TeamId aiTeam)
    {
        HashSet<int> slots = new HashSet<int>();
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || !construction.IsPlayerHeadQuarter)
                continue;
            if (construction.TeamId != aiTeam)
                continue;
            if (construction.SlotIndex < 0)
                continue;

            slots.Add(construction.SlotIndex);
        }

        return slots;
    }

    private static bool IsOwnAnchorSector(AIAnchorPlanContext context, ConstructionSector sector)
    {
        return context.OwnAnchorSectors != null && context.OwnAnchorSectors.Contains(sector);
    }

    private static int GetAnchorSectorPriorityBonus(AIAnchorPlanContext context, ConstructionSector sector, AIMacroTerritoryPhase phase)
    {
        if (!IsOwnAnchorSector(context, sector))
            return 0;

        switch (phase)
        {
            case AIMacroTerritoryPhase.Collapsing:
                return AnchorSectorRecoveryPriorityBonus;
            case AIMacroTerritoryPhase.EarlyExpansion:
                return AnchorSectorEarlyPriorityBonus;
            default:
                return AnchorSectorDefaultPriorityBonus;
        }
    }

    private static bool ShouldReserveCapturersForAnchors(AIAnchorPlanContext context, AIMacroTerritoryPhase phase)
    {
        return context.OwnAnchorSectors != null
            && context.OwnAnchorSectors.Count > 0
            && (phase == AIMacroTerritoryPhase.Collapsing || phase == AIMacroTerritoryPhase.EarlyExpansion);
    }

    private static bool HasOpenAnchorCapturerNeed(TeamObjectivePlan plan, AIAnchorPlanContext context)
    {
        if (plan == null || context.OwnAnchorSectors == null || context.OwnAnchorSectors.Count == 0)
            return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || !IsOwnAnchorSector(context, obj.Sector))
                continue;
            if (obj.Status == ObjectiveStatus.Defending || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                continue;

            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Capturador && !slot.Filled)
                    return true;
        }

        return false;
    }

    private void ReleaseNonAnchorCapturersForAnchorNeed(TeamObjectivePlan plan, TeamId aiTeam, AIAnchorPlanContext context, AIMacroTerritoryPhase phase, int turnNumber)
    {
        if (!ShouldReserveCapturersForAnchors(context, phase) || !HasOpenAnchorCapturerNeed(plan, context))
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || IsOwnAnchorSector(context, obj.Sector))
                continue;
            if (obj.Status == ObjectiveStatus.Defending)
                continue;

            bool releasedAny = false;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot.Role != UnitRole.Capturador || !slot.Filled)
                    continue;

                UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                unit?.ClearAIAssignedPlan();
                slot.Filled = false;
                slot.AssignedUnitId = -1;
                releasedAny = true;
            }

            if (!releasedAny)
                continue;

            if (obj.Status == ObjectiveStatus.Pursuing || obj.Status == ObjectiveStatus.Capturing)
                obj.Status = ObjectiveStatus.Pending;

            Debug.Log($"[AI Anchor][T{turnNumber}][{aiTeam}] libera capturador de {obj.Sector}: anchor aberto tem prioridade");
        }
    }
}
