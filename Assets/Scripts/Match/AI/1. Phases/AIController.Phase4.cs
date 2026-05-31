using System.Collections;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fase 4: Passa a vez
    // -------------------------------------------------------------------------

    private IEnumerator Phase4_EndTurn()
    {
        Debug.Log($"{TL()} Fase4 — passando a vez.");
        isActive = false;
        if (ShouldStopAIForMatchEnd("phase4_start"))
            yield break;

        yield return new WaitUntil(() =>
            turnStateManager == null ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);
        if (ShouldStopAIForMatchEnd("phase4_apos_neutral"))
            yield break;

        if (replayManager != null)
        {
            TeamId aiTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
            yield return ExecuteAIBatchWithDebugStep(BuildEndTurnBatch(aiTeam));
        }
        else
        {
            matchController?.AdvanceTurnWithTransition();
        }
    }
}
