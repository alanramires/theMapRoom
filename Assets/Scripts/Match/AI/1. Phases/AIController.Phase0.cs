using System.Collections;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fase 0: Aguarda serviços automáticos de início de turno
    // -------------------------------------------------------------------------

    private IEnumerator Phase0_WaitForTurnReady()
    {
        MarkAITurnTransition("stage0_start");
        if (ShouldStopAIForMatchEnd("phase0_start"))
            yield break;

        // Um frame para que os handlers de OnActiveTeamChanged das outras systems
        // (supply queue, auto command service) registrem suas coroutines primeiro.
        yield return null;

        // OnActiveTeamChanged e disparado antes de ReleaseUnitsForActiveTeam
        // terminar dentro da mesma troca confirmada de turno. O painel de rodada
        // e apenas apresentacao e nunca participa desta barreira logica.
        if (matchController != null)
        {
            yield return WaitUntilWatched(
                () => matchController == null
                      || !matchController.AreTurnStartEffectsPending,
                "phase0_efeitos_inicio_turno");
            if (ShouldStopAIForMatchEnd(
                    "phase0_apos_efeitos_inicio_turno"))
            {
                yield break;
            }
        }

        MarkAITurnTransition("stage0_turn_effects_ready");
        if (turnStateManager != null)
        {
            yield return WaitUntilWatched(
                () => !turnStateManager.IsAutoCommandServiceBusy,
                "phase0_command_service");
            if (ShouldStopAIForMatchEnd("phase0_apos_command_service"))
                yield break;
            MarkAITurnTransition("stage0_auto_service_idle");
            yield return WaitUntilWatched(
                () => turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral,
                "phase0_neutral");
            if (ShouldStopAIForMatchEnd("phase0_apos_neutral"))
                yield break;
        }

        MarkAITurnTransition("stage0_neutral_ready");
        float batchDelay = GetBatchDelay();
        if (batchDelay > 0f) yield return new WaitForSeconds(batchDelay);

        MarkAITurnTransition("stage0_end");
        Debug.Log($"{TL()} Fase0 concluída.");
    }
}
