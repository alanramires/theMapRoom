using System.Collections;
using UnityEngine;

/*
    Fase 1: Executa serviços automáticos de comando (CommandService) se estiverem habilitados para a equipe AI.
*/
public partial class AIController
{
    private IEnumerator Phase1_CommandService(AIWorldSnapshot snapshot)
    {
        MarkAITurnTransition("stage1_start");
        if (ShouldStopAIForMatchEnd("phase1_start"))
            yield break;

        Debug.Log($"{TL()} Fase1 — iniciando. replayManager={replayManager != null} turnStateManager={turnStateManager != null}");

        // REPARO ANTES DA CURA. O estado de reparo era decidido so no setup da Fase 2, DEPOIS
        // do Servico do Comando: quem chegava ao turno abaixo do gatilho (soldado a bordo com
        // 2 HP, gatilho 3) era curado +2, chegava a Fase 2 com 4 e "nao estava ferido" — pulava
        // a faixa gatilho..recuperacao inteira (sai so com 8). No embarcado isso decidia se o
        // transporte virava EVAC. Mesma regra (UpdateRepairState), com o HP com que a unidade
        // chegou; a Fase 2 segue avaliando a saida.
        TeamObjectivePlan repairPlan = ObjectiveManager.GetPlanForSlot(
            PlayerSlotId.FromIndex(ResolveAISlotKey(snapshot.AITeam)));
        foreach (UnitManager u in UnitManager.AllActive)
            if (u != null && u.SlotIndex == snapshot.AISlotIndex && !u.IsDead)
                UpdateRepairState(u, repairPlan);

        MarkAITurnTransition("stage1_repair_states_ready");
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
        MarkAITurnTransition("command_service_ready", finish: true);
        yield return ExecuteAIBatchWithDebugStep(BuildCommandServiceBatch(snapshot.AITeam));
        if (ShouldStopAIForMatchEnd("phase1_apos_batch"))
            yield break;
        Debug.Log($"{TL()} Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...");

        if (turnStateManager != null)
            yield return WaitUntilWatched(
                () => !turnStateManager.IsAutoCommandServiceBusy,
                "phase1_command_service");
        if (ShouldStopAIForMatchEnd("phase1_apos_command_service"))
            yield break;

        JogadasManager.EnsureInstance()?.RegistrarServicoComando(snapshot.TurnNumber, snapshot.AISlotIndex);

        float delay = GetBatchDelay();
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        Debug.Log($"{TL()} Fase1 — Serviço do Comando concluído.");
    }
}
