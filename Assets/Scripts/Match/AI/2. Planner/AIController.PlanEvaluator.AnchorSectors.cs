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

    // Entrada de inspeção (HUD/editor): setor-âncora do time, sua célula e se está seguro.
    public struct AnchorInspection
    {
        public ConstructionSector Sector;
        public Vector3Int Cell;
        public bool Held;
    }

    // Fonte única dos anchors do time: percorre ConstructionManager.IsAnchorSector filtrando
    // pelos slots de HQ do time. Usado pelo planner (BuildAnchorPlanContext) e pela inspeção.
    private static IEnumerable<(ConstructionManager anchor, ConstructionSector sector, Vector3Int cell, int slot, bool held)>
        EnumerateOwnAnchors(TeamId aiTeam)
    {
        HashSet<int> ownSlots = CollectOwnHQSlots(aiTeam);
        if (ownSlots.Count == 0)
            yield break;

        foreach (ConstructionManager anchor in ConstructionManager.AllActive)
        {
            if (anchor == null || !anchor.IsAnchorSector || anchor.Sector == ConstructionSector.None)
                continue;
            if (!ownSlots.Contains(anchor.AnchorSectorSlotIndex))
                continue;

            Vector3Int cell = anchor.CurrentCellPosition; cell.z = 0;
            bool held = TryGetAnySectorInfo(anchor.Sector, out SectorManager.SectorInfo info)
                && info.IsFullyControlled
                && info.ControllingTeam == aiTeam;
            yield return (anchor, anchor.Sector, cell, anchor.AnchorSectorSlotIndex, held);
        }
    }

    // Para a janela Shopping Pressure (seção "# Base guard / âncora").
    public static List<AnchorInspection> GetOwnAnchorsForInspection(TeamId aiTeam)
    {
        var list = new List<AnchorInspection>();
        foreach (var a in EnumerateOwnAnchors(aiTeam))
            list.Add(new AnchorInspection { Sector = a.sector, Cell = a.cell, Held = a.held });
        return list;
    }

    private static AIAnchorPlanContext BuildAnchorPlanContext(TeamId aiTeam, int turnNumber)
    {
        AIAnchorPlanContext context = new AIAnchorPlanContext
        {
            OwnAnchorSectors = new HashSet<ConstructionSector>()
        };

        foreach (var a in EnumerateOwnAnchors(aiTeam))
        {
            context.OwnAnchorSectors.Add(a.sector);
            context.AnchorCount++;
            Debug.Log($"[AI Anchor][T{turnNumber}][{aiTeam}] {a.sector} via {a.anchor.name} slot={a.slot} held={a.held}");
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
        return CountOpenAnchorCapturerSlots(plan, context) > 0;
    }

    // Quantas vagas de capturador as âncoras ainda precisam preencher. A reserva deve segurar
    // só esse tanto de capturadores; o excedente fica livre para os demais objetivos.
    private static int CountOpenAnchorCapturerSlots(TeamObjectivePlan plan, AIAnchorPlanContext context)
    {
        if (plan == null || context.OwnAnchorSectors == null || context.OwnAnchorSectors.Count == 0)
            return 0;

        int count = 0;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || !IsOwnAnchorSector(context, obj.Sector))
                continue;
            if (obj.Status == ObjectiveStatus.Defending || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                continue;

            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Capturador && !slot.Filled)
                    count++;
        }

        return count;
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
