using System.Collections;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fase 0: Aguarda serviços automáticos de início de turno
    // -------------------------------------------------------------------------

    private IEnumerator Phase0_WaitForTurnReady()
    {
        if (ShouldStopAIForMatchEnd("phase0_start"))
            yield break;

        // Um frame para que os handlers de OnActiveTeamChanged das outras systems
        // (supply queue, auto command service) registrem suas coroutines primeiro.
        yield return null;

        if (turnStateManager != null)
        {
            yield return new WaitUntil(() => !turnStateManager.IsAutoCommandServiceBusy);
            if (ShouldStopAIForMatchEnd("phase0_apos_command_service"))
                yield break;
            yield return new WaitUntil(() =>
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);
            if (ShouldStopAIForMatchEnd("phase0_apos_neutral"))
                yield break;
        }

        float batchDelay = GetBatchDelay();
        if (batchDelay > 0f) yield return new WaitForSeconds(batchDelay);

        Debug.Log($"{TL()} Fase0 concluída.");
    }
}
