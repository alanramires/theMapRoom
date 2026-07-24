using System.Collections;
using UnityEngine;

public partial class AIController
{
    private const int FreshCaptureGarrisonEnemyRange = 2;

    private void CommitAIWorldLightAfterAction(TeamId aiTeam, string reason, bool refreshFoW)
    {
        SyncAIUnitCellsFromTransforms();

        if (refreshFoW)
            matchController?.RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.DataOnly);

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        int turnNumber = matchController != null ? matchController.CurrentTurn : 0;
        int removed = InvalidateStaleObjectivesLight(plan, aiTeam, turnNumber);

        Debug.Log($"[AI Commit Light][T{turnNumber}][{TeamUtils.GetName(aiTeam)}] reason={reason} refreshFoW={refreshFoW} removed={removed}");
    }

    private int InvalidateStaleObjectivesLight(TeamObjectivePlan plan, TeamId aiTeam, int turnNumber)
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

                if (ShouldKeepFreshCaptureGarrison(obj.Sector, aiTeam, out Vector3Int guardCell))
                {
                    obj.Status = ObjectiveStatus.Defending;
                    obj.HandoffEligible = false;
                    obj.PreferredHandoffFromUnitId = -1;
                    Debug.Log($"[AI Commit Light][T{turnNumber}][{TeamUtils.GetName(aiTeam)}] guarnicao mantida: {obj.Sector} enemy<=2h guard={guardCell}");
                    continue;
                }
            }

            ClearLightObjectiveSlots(obj, aiTeam);
            ClearObjectiveHUD(obj);
            plan.Objectives.RemoveAt(i);
            removed++;

            Debug.Log($"[AI Commit Light][T{turnNumber}][{TeamUtils.GetName(aiTeam)}] objetivo concluido/removido: {obj.Sector} status={obj.Status}");
        }

        return removed;
    }

    private bool ShouldKeepFreshCaptureGarrison(ConstructionSector sector, TeamId aiTeam, out Vector3Int guardCell)
    {
        guardCell = Vector3Int.zero;
        bool hasOwnedCapturable = false;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;
            if (!construction.IsCapturable || construction.CapturePointsMax <= 0)
                continue;
            if (construction.SlotIndex != ResolveAISlotKey(aiTeam) || construction.CurrentCapturePoints < construction.CapturePointsMax)
                continue;

            hasOwnedCapturable = true;
            Vector3Int cell = construction.CurrentCellPosition; cell.z = 0;
            if (HasNearbyVisibleEnemy(cell, aiTeam, FreshCaptureGarrisonEnemyRange))
            {
                guardCell = cell;
                return true;
            }
        }

        return hasOwnedCapturable
            && TryGetAnySectorInfo(sector, out SectorManager.SectorInfo info)
            && info != null
            && HasNearbyVisibleEnemy(info.RepresentativeCell, aiTeam, FreshCaptureGarrisonEnemyRange);
    }

    private void ClearLightObjectiveSlots(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null || obj.Slots == null)
            return;

        foreach (SlotNeed slot in obj.Slots)
        {
            if (slot == null || !slot.Filled)
                continue;

            UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            unit?.ClearAIAssignedPlan();
            slot.Filled = false;
            slot.AssignedUnitId = -1;
        }
    }

    private IEnumerator CommitAIWorldHeavy(TeamId aiTeam, string reason, bool rebuildPlan = true)
    {
        if (ShouldStopAIForMatchEnd($"ai_commit_start:{reason}"))
            yield break;

        float tHeavy = Time.realtimeSinceStartup;

        float tSync1 = Time.realtimeSinceStartup;
        SyncAIUnitCellsFromTransforms();
        Debug.Log($"[AI Commit Heavy] Sync1: {(Time.realtimeSinceStartup - tSync1) * 1000f:F0}ms");

        float tFoW = Time.realtimeSinceStartup;
        matchController?.RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.DataOnly);
        Debug.Log($"[AI Commit Heavy] RefreshFoW: {(Time.realtimeSinceStartup - tFoW) * 1000f:F0}ms");

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
        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);
        Debug.Log($"[AI Commit Heavy] AIWorldSnapshot.Build: {(Time.realtimeSinceStartup - tSnapshot) * 1000f:F0}ms");

        if (rebuildPlan)
        {
            BuildObjectivePlan(snapshot);
            AITacticalAnalyzer.Instance?.Rebuild(aiTeam, snapshot, ObjectiveManager.GetPlanForTeam(aiTeam));
        }

        Debug.Log($"[AI Commit Heavy][T{snapshot.TurnNumber}][{TeamUtils.GetName(aiTeam)}] reason={reason} units={snapshot.MyUnits.Count} enemies={snapshot.EnemyUnits.Count} total={( Time.realtimeSinceStartup - tHeavy) * 1000f:F0}ms");
    }
}
