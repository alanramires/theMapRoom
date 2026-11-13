using System.Collections;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fase 4: Passa a vez
    // -------------------------------------------------------------------------

    // Wall time across the turn transition; deltas include scheduling and debug pauses.
    private System.Diagnostics.Stopwatch turnTransitionTimer;
    private long turnTransitionLastMs;

    private void MarkAITurnTransition(string point, bool begin = false, bool finish = false)
    {
        if (begin || turnTransitionTimer == null)
        {
            turnTransitionTimer = System.Diagnostics.Stopwatch.StartNew();
            turnTransitionLastMs = 0;
        }
        long elapsed = turnTransitionTimer.ElapsedMilliseconds;
        int turn = matchController != null ? matchController.CurrentTurn : -1;
        int slot = matchController != null ? matchController.ActiveSlotId.Value : -1;
        Debug.Log($"[AI Turn Transition Timing] T{turn} slot={slot} point={point} " +
            $"total={elapsed}ms delta={elapsed - turnTransitionLastMs}ms " +
            $"debugPaused={isDebugPaused} aiStep={aiStep}");
        turnTransitionLastMs = elapsed;
        if (finish)
        {
            turnTransitionTimer.Stop();
            turnTransitionTimer = null;
        }
    }

    private IEnumerator Phase4_EndTurn()
    {
        MarkAITurnTransition("stage4_start", begin: true);
        Debug.Log($"{TL()} Fase4 — passando a vez.");
        isActive = false;
        if (ShouldStopAIForMatchEnd("phase4_start"))
            yield break;

        yield return WaitUntilWatched(
            () => turnStateManager == null ||
                  turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral,
            "phase4_neutral");
        if (ShouldStopAIForMatchEnd("phase4_apos_neutral"))
            yield break;

        MarkAITurnTransition("stage4_neutral_ready");
        if (replayManager != null)
        {
            TeamId aiTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
            yield return ExecuteAIBatchWithDebugStep(BuildEndTurnBatch(aiTeam));
        }
        else
        {
            MarkAITurnTransition("end_turn_execute");
            matchController?.AdvanceTurnWithTransition();
        }
    }
}
