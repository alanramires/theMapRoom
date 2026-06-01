using System.Collections;
using UnityEngine;

public partial class AIController
{
    private IEnumerator CommitAIWorldAfterAction(TeamId aiTeam, string reason, bool rebuildPlan = true)
    {
        if (ShouldStopAIForMatchEnd($"ai_commit_start:{reason}"))
            yield break;

        SyncAIUnitCellsFromTransforms();
        matchController?.RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.DataOnly);
        SectorManager.RequestRebuildFromActiveConstructions($"ai-commit:{reason}");

        // SectorManager rebuilds on the next frame in play mode. Wait for that
        // barrier so the next AI decision uses the same consolidated world a load does.
        yield return null;

        SyncAIUnitCellsFromTransforms();
        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);

        if (rebuildPlan)
        {
            BuildObjectivePlan(snapshot);
            AITacticalAnalyzer.Instance?.Rebuild(aiTeam, snapshot, ObjectiveManager.GetPlanForTeam(aiTeam));
        }

        Debug.Log($"[AI Commit][T{snapshot.TurnNumber}][{TeamUtils.GetName(aiTeam)}] reason={reason} units={snapshot.MyUnits.Count} enemies={snapshot.EnemyUnits.Count}");
    }
}
