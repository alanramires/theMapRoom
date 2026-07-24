using System.Collections;
using UnityEngine;

/*
    Fase 1: Executa serviços automáticos de comando (CommandService) se estiverem habilitados para a equipe AI.
*/
public partial class AIController
{
    private IEnumerator Phase1_CommandService(AIWorldSnapshot snapshot)
    {
        if (ShouldStopAIForMatchEnd("phase1_start"))
            yield break;

        Debug.Log($"{TL()} Fase1 — iniciando. replayManager={replayManager != null} turnStateManager={turnStateManager != null}");

        if (replayManager == null)
        {
            Debug.LogWarning($"{TL()} Fase1 — replayManager é null, abortando.");
            yield break;
        }

        PlayerSlotId aiSlot = PlayerSlotId.FromIndex(snapshot.AISlotIndex);
        if (matchController == null || !matchController.IsPlayerCommandServiceAutomatic(aiSlot))
        {
            Debug.Log($"{TL()} Fase1 — commandServiceAutomatic=false, pulando.");
            yield break;
        }

        Debug.Log($"{TL()} Fase1 — enviando batch CommandService.");
        yield return ExecuteAIBatchWithDebugStep(BuildCommandServiceBatch(snapshot.AITeam));
        if (ShouldStopAIForMatchEnd("phase1_apos_batch"))
            yield break;
        Debug.Log($"{TL()} Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...");

        if (turnStateManager != null)
            yield return new WaitUntil(() => !turnStateManager.IsAutoCommandServiceBusy);
        if (ShouldStopAIForMatchEnd("phase1_apos_command_service"))
            yield break;

        JogadasManager.EnsureInstance()?.RegistrarServicoComando(snapshot.TurnNumber, snapshot.AISlotIndex);

        float delay = GetBatchDelay();
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        Debug.Log($"{TL()} Fase1 — Serviço do Comando concluído.");
    }
}
