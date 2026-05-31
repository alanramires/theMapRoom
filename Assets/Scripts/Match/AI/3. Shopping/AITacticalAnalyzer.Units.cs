using System.Collections.Generic;
using UnityEngine;

// Atribuição de unidades a operações, inferência de fase/coesão e logging.
public partial class AITacticalAnalyzer
{
    private void AssignExistingUnitsToOperations(TeamId team, AIWorldSnapshot snapshot, List<AITacticalNeed> ops)
    {
        var used = new HashSet<int>();
        foreach (AITacticalNeed op in ops)
        {
            foreach (AITacticalSlotNeed slot in op.RequiredSlots)
            {
                UnitManager unit = FindLinkedObjectiveUnit(op.LinkedObjective, slot.Kind, used);
                if (unit == null && CanUseGlobalUnitFill(op))
                    unit = FindAnyUnitForNeed(snapshot, slot.Kind, used);
                if (unit == null)
                    continue;

                slot.Filled = true;
                slot.AssignedUnitId = unit.InstanceId;
                if (!op.AssignedUnitIds.Contains(unit.InstanceId))
                    op.AssignedUnitIds.Add(unit.InstanceId);
                used.Add(unit.InstanceId);
            }
        }
    }

    private void InferCohesionForAllOps(AIWorldSnapshot snapshot, List<AITacticalNeed> ops)
    {
        foreach (AITacticalNeed op in ops)
        {
            if (op == null || !IsCaptureOperation(op))
                continue;

            UnitManager screen = FindBestScreenUnitForOperation(op);
            if (screen == null)
            {
                op.HasScreen = false;
                op.ScreenUnitId = -1;
                op.ScreenDistanceToTarget = -1f;
                op.CohesionReason = "sem screen minimo";
                continue;
            }

            op.HasScreen = true;
            op.ScreenUnitId = screen.InstanceId;
            op.ScreenDistanceToTarget = SectorManager.HexDistance(Normalize(screen.CurrentCellPosition), op.TargetCell);
            op.CohesionReason = $"screen #{screen.InstanceId} dist={op.ScreenDistanceToTarget:F1}";
        }
    }

    private static bool CanUseGlobalUnitFill(AITacticalNeed op)
    {
        return op != null
            && (op.Type == AITacticalNeedType.BaseDefense
                || op.Type == AITacticalNeedType.PreventiveDefense);
    }

    private UnitManager FindLinkedObjectiveUnit(SectorObjective obj, AINeedKind kind, HashSet<int> used)
    {
        if (obj == null || obj.Slots == null)
            return null;
        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled || used.Contains(slot.AssignedUnitId)) continue;
            UnitManager unit = FindActiveUnit(slot.AssignedUnitId);
            if (UnitSatisfiesNeed(unit, kind))
                return unit;
        }
        return null;
    }

    private UnitManager FindAnyUnitForNeed(AIWorldSnapshot snapshot, AINeedKind kind, HashSet<int> used)
    {
        if (snapshot?.MyUnits == null)
            return null;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsUnderRepair || used.Contains(unit.InstanceId)) continue;
            if (UnitSatisfiesNeed(unit, kind))
                return unit;
        }
        return null;
    }

    private static UnitManager FindActiveUnit(int id)
    {
        foreach (UnitManager unit in UnitManager.AllActive)
            if (unit != null && !unit.IsDead && unit.InstanceId == id)
                return unit;
        return null;
    }

    private void InferPhasesForAllOps(AIWorldSnapshot snapshot, List<AITacticalNeed> ops)
    {
        foreach (AITacticalNeed op in ops)
        {
            if (op.LinkedObjective != null && op.LinkedObjective.Status == ObjectiveStatus.Capturing)
            {
                op.Phase = AITacticalNeedPhase.Capturing;
                continue;
            }
            if (op.LinkedObjective != null && op.LinkedObjective.Status == ObjectiveStatus.Defending)
            {
                op.Phase = AITacticalNeedPhase.Holding;
                continue;
            }
            if (HasEnemyNearCell(snapshot, op.TargetCell, 2) && op.AssignedUnitIds.Count > 0)
            {
                op.Phase = AITacticalNeedPhase.Engaging;
                continue;
            }
            if (op.AssignedUnitIds.Count > 0 && DistanceAssignedToTarget(op) > 3)
            {
                op.Phase = AITacticalNeedPhase.Moving;
                continue;
            }
            op.Phase = op.AssignedUnitIds.Count == 0 && HasOpenAnySlot(op)
                ? AITacticalNeedPhase.Forming
                : AITacticalNeedPhase.Holding;
        }
    }

    private void LogOperations(TeamId team, int turn, List<AITacticalNeed> ops)
    {
        foreach (AITacticalNeed op in ops)
            Debug.Log($"[AI Ops][T{turn}][{team}] {op.Type} {op.Sector} pri={op.Priority} phase={op.Phase} urgent={op.IsUrgent} preventive={op.IsPreventive} slots={DescribeSlots(op)} assigned={op.AssignedUnitIds.Count} screen={(op.HasScreen ? op.ScreenUnitId.ToString() : "-")} reason={op.CohesionReason}");
    }

    private static string DescribeSlots(AITacticalNeed op)
    {
        var counts = new Dictionary<AINeedKind, int>();
        foreach (AITacticalSlotNeed slot in op.RequiredSlots)
        {
            counts.TryGetValue(slot.Kind, out int count);
            counts[slot.Kind] = count + 1;
        }

        var parts = new List<string>();
        foreach (KeyValuePair<AINeedKind, int> kv in counts)
            if (kv.Value > 0)
                parts.Add($"{kv.Key}x{kv.Value}");
        return parts.Count > 0 ? string.Join(" ", parts) : "-";
    }

    private static bool HasOpenAnySlot(AITacticalNeed op)
    {
        foreach (AITacticalSlotNeed slot in op.RequiredSlots)
            if (!slot.Filled)
                return true;
        return false;
    }

    private static int DistanceAssignedToTarget(AITacticalNeed op)
    {
        int best = int.MaxValue;
        foreach (int id in op.AssignedUnitIds)
        {
            UnitManager unit = FindActiveUnit(id);
            if (unit == null) continue;
            Vector3Int cell = Normalize(unit.CurrentCellPosition);
            best = Mathf.Min(best, Mathf.RoundToInt(SectorManager.HexDistance(cell, op.TargetCell)));
        }
        return best == int.MaxValue ? 0 : best;
    }

    private static bool IsCaptureOperation(AITacticalNeed op)
    {
        return op != null
            && (op.Type == AITacticalNeedType.GroundCapture
                || op.Type == AITacticalNeedType.AirliftCapture);
    }

    private static UnitManager FindBestScreenUnitForOperation(AITacticalNeed op, int excludedUnitId = -1)
    {
        if (op == null)
            return null;

        UnitManager bestAssault = FindBestAssignedUnitForNeed(op, AINeedKind.Assault, excludedUnitId);
        if (bestAssault != null)
            return bestAssault;

        if (op.Type == AITacticalNeedType.GroundCapture)
            return FindBestAssignedUnitForNeed(op, AINeedKind.Capturer, excludedUnitId);

        return null;
    }

    private static UnitManager FindBestAssignedUnitForNeed(AITacticalNeed op, AINeedKind need, int excludedUnitId = -1)
    {
        UnitManager best = null;
        float bestDist = float.MaxValue;
        foreach (int id in op.AssignedUnitIds)
        {
            if (id == excludedUnitId)
                continue;

            UnitManager unit = FindActiveUnit(id);
            if (!UnitSatisfiesNeed(unit, need))
                continue;

            float dist = SectorManager.HexDistance(Normalize(unit.CurrentCellPosition), op.TargetCell);
            if (best == null || dist < bestDist)
            {
                best = unit;
                bestDist = dist;
            }
        }

        return best;
    }
}
