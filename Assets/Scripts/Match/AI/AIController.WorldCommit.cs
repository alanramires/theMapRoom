using System.Collections;
using UnityEngine;

public partial class AIController
{
    private const int FreshCaptureGarrisonEnemyRange = 2;

    private void CommitAIWorldLightAfterAction(
        PlayerSlotId aiSlot,
        string reason,
        bool ensureFogAfterConstructionChange)
    {
        TeamId aiTeam = matchController != null
            ? matchController.GetVisualTeamForSlot(aiSlot)
            : currentAITeam;
        SyncAIUnitCellsFromTransforms();

        FogPlanningSnapshotBarrierResult fogBarrier =
            FogPlanningSnapshotBarrierResult.Unavailable;
        if (ensureFogAfterConstructionChange && matchController != null)
        {
            fogBarrier =
                matchController.EnsureConfirmedFogGameplaySnapshotForSlot(aiSlot);
        }

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(aiSlot);
        int turnNumber = matchController != null ? matchController.CurrentTurn : 0;
        int removed = InvalidateStaleObjectivesLight(
            plan,
            aiSlot,
            aiTeam,
            turnNumber);

        Debug.Log(
            $"[AI Commit Light][T{turnNumber}][slot={aiSlot.Value}][{TeamUtils.GetName(aiTeam)}] " +
            $"reason={reason} fogBarrier={fogBarrier} removed={removed}");
    }

    private int InvalidateStaleObjectivesLight(
        TeamObjectivePlan plan,
        PlayerSlotId aiSlot,
        TeamId aiTeam,
        int turnNumber)
    {
        if (plan == null || plan.Objectives == null)
            return 0;

        int removed = 0;
        for (int i = plan.Objectives.Count - 1; i >= 0; i--)
        {
            SectorObjective obj = plan.Objectives[i];
            if (obj == null)
                continue;

            if (obj.Status == ObjectiveStatus.Defending)
                continue;

            if (obj.ObjectiveType == AIObjectiveType.RallyAssembly)
                continue;

            if (FindCapturableInSector(obj.Sector, aiTeam) != null)
                continue;

            if (IsCaptureProgressStatus(obj.Status))
            {
                RememberRecentlyCapturedSector(aiTeam, obj.Sector, turnNumber);

                if (ShouldKeepFreshCaptureGarrison(
                        obj.Sector,
                        aiSlot,
                        out Vector3Int guardCell))
                {
                    obj.Status = ObjectiveStatus.Defending;
                    obj.HandoffEligible = false;
                    obj.PreferredHandoffFromUnitId = -1;
                    Debug.Log($"[AI Commit Light][T{turnNumber}][{TeamUtils.GetName(aiTeam)}] guarnicao mantida: {obj.Sector} enemy<=2h guard={guardCell}");
                    continue;
                }
            }

            ClearLightObjectiveSlots(obj, aiSlot);
            ClearObjectiveHUD(obj);
            plan.Objectives.RemoveAt(i);
            removed++;

            Debug.Log($"[AI Commit Light][T{turnNumber}][{TeamUtils.GetName(aiTeam)}] objetivo concluido/removido: {obj.Sector} status={obj.Status}");
        }

        return removed;
    }

    private bool ShouldKeepFreshCaptureGarrison(
        ConstructionSector sector,
        PlayerSlotId aiSlot,
        out Vector3Int guardCell)
    {
        guardCell = Vector3Int.zero;
        bool hasOwnedCapturable = false;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;
            if (!construction.IsCapturable || construction.CapturePointsMax <= 0)
                continue;
            if (construction.SlotIndex != aiSlot.Value ||
                construction.CurrentCapturePoints < construction.CapturePointsMax)
                continue;

            hasOwnedCapturable = true;
            Vector3Int cell = construction.CurrentCellPosition; cell.z = 0;
            if (HasNearbyVisibleEnemyForSlot(
                    cell,
                    aiSlot,
                    FreshCaptureGarrisonEnemyRange))
            {
                guardCell = cell;
                return true;
            }
        }

        return hasOwnedCapturable
            && TryGetAnySectorInfo(sector, out SectorManager.SectorInfo info)
            && info != null
            && HasNearbyVisibleEnemyForSlot(
                info.RepresentativeCell,
                aiSlot,
                FreshCaptureGarrisonEnemyRange);
    }

    private bool HasNearbyVisibleEnemyForSlot(
        Vector3Int cell,
        PlayerSlotId aiSlot,
        int range)
    {
        MatchController match = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null ||
                enemy.SlotIndex == aiSlot.Value ||
                enemy.IsDead ||
                enemy.IsEmbarked)
            {
                continue;
            }

            if (match != null && !match.IsUnitVisibleForSlot(enemy, aiSlot))
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (SectorManager.HexDistance(enemyCell, cell) <= range)
                return true;
        }
        return false;
    }

    private void ClearLightObjectiveSlots(SectorObjective obj, PlayerSlotId aiSlot)
    {
        if (obj == null || obj.Slots == null)
            return;

        foreach (SlotNeed slot in obj.Slots)
        {
            if (slot == null || !slot.Filled)
                continue;

            UnitManager unit = FindActiveUnitForSlot(slot.AssignedUnitId, aiSlot);
            unit?.ClearAIAssignedPlan();
            slot.Filled = false;
            slot.AssignedUnitId = -1;
        }
    }

    private static UnitManager FindActiveUnitForSlot(
        int instanceId,
        PlayerSlotId slot)
    {
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit != null &&
                unit.InstanceId == instanceId &&
                unit.SlotIndex == slot.Value &&
                !unit.IsDead)
            {
                return unit;
            }
        }
        return null;
    }

    private IEnumerator CommitAIWorldHeavy(
        PlayerSlotId aiSlot,
        string reason,
        bool rebuildPlan = true)
    {
        TeamId aiTeam = matchController != null
            ? matchController.GetVisualTeamForSlot(aiSlot)
            : currentAITeam;
        if (ShouldStopAIForMatchEnd($"ai_commit_start:{reason}"))
            yield break;

        float tHeavy = Time.realtimeSinceStartup;

        float tSync1 = Time.realtimeSinceStartup;
        SyncAIUnitCellsFromTransforms();
        Debug.Log($"[AI Commit Heavy] Sync1: {(Time.realtimeSinceStartup - tSync1) * 1000f:F0}ms");

        float tFoW = Time.realtimeSinceStartup;
        FogPlanningSnapshotBarrierResult fogBarrier = matchController != null
            ? matchController.EnsureConfirmedFogGameplaySnapshotForSlot(aiSlot)
            : FogPlanningSnapshotBarrierResult.Unavailable;
        Debug.Log(
            $"[AI Commit Heavy] FogBarrier: {(Time.realtimeSinceStartup - tFoW) * 1000f:F0}ms " +
            $"slot={aiSlot.Value} result={fogBarrier}");

        SectorManager.RequestRebuildFromActiveConstructions($"ai-commit:{reason}");

        float tYield = Time.realtimeSinceStartup;
        // SectorManager rebuilds on the next frame in play mode. Wait for that
        // barrier so the next AI decision uses the same consolidated world a load does.
        yield return null;
        Debug.Log($"[AI Commit Heavy] yield+SectorRebuild: {(Time.realtimeSinceStartup - tYield) * 1000f:F0}ms");

        float tSync2 = Time.realtimeSinceStartup;
        SyncAIUnitCellsFromTransforms();
        Debug.Log($"[AI Commit Heavy] Sync2: {(Time.realtimeSinceStartup - tSync2) * 1000f:F0}ms");

        float tSnapshot = Time.realtimeSinceStartup;
        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(
            aiSlot,
            matchController);
        Debug.Log($"[AI Commit Heavy] AIWorldSnapshot.Build: {(Time.realtimeSinceStartup - tSnapshot) * 1000f:F0}ms");

        if (rebuildPlan)
        {
            BuildObjectivePlan(snapshot);
            AITacticalAnalyzer.Instance?.Rebuild(
                snapshot,
                ObjectiveManager.GetPlanForSlot(aiSlot));
        }

        Debug.Log(
            $"[AI Commit Heavy][T{snapshot.TurnNumber}][slot={aiSlot.Value}]" +
            $"[{TeamUtils.GetName(aiTeam)}] reason={reason} units={snapshot.MyUnits.Count} " +
            $"enemies={snapshot.EnemyUnits.Count} total={(Time.realtimeSinceStartup - tHeavy) * 1000f:F0}ms");
    }
}
