using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fase 3: Compras
    // -------------------------------------------------------------------------

    private IEnumerator Phase3_Shopping(AIWorldSnapshot snapshot)
    {
        if (ShouldStopAIForMatchEnd("phase3_start"))
            yield break;

        Debug.Log($"{TL()} Fase3 — compras.");

        // Reconstrói snapshot para refletir o saldo atual pós-ações
        AIWorldSnapshot freshSnap = AIWorldSnapshot.Build(snapshot.AITeam, matchController);
        if (ShouldStopAIForMatchEnd("phase3_apos_snapshot"))
            yield break;

        // Reavalia plano/operações com inimigos revelados durante a Fase 2.
        BuildObjectivePlan(freshSnap);
        AITacticalAnalyzer.Instance.Rebuild(snapshot.AITeam, freshSnap, ObjectiveManager.GetPlanForTeam(snapshot.AITeam));

        List<AIShoppingPlanner.ShoppingOrder> orders = AIShoppingPlanner.Decide(freshSnap);

        foreach (AIShoppingPlanner.ShoppingOrder order in orders)
        {
            if (!isActive || ShouldStopAIForMatchEnd("phase3_loop")) break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase3_apos_pause_loop"))
                yield break;
            yield return WaitIfDebugShoppingPaused();
            if (ShouldStopAIForMatchEnd("phase3_apos_pause_shop"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase3_apos_pause_pre_batch"))
                yield break;

            PlayerAction batch = BuildShoppingBatch(snapshot.AITeam, order);
            Debug.Log($"{TL("Shopping")} {order.UnitToBuy.name} @ {order.Building.CurrentCellPosition}");

            yield return ExecuteAIBatchWithDebugStep(batch);
            if (ShouldStopAIForMatchEnd("phase3_apos_batch"))
                yield break;

            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase3_apos_pause_batch"))
                yield break;

            // Segurança: fecha o menu de shopping se ficou aberto (compra falhou)
            if (turnStateManager != null &&
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.ShoppingAndServices)
            {
                Debug.LogWarning($"{TL("Shopping")} Menu ficou aberto — fechando.");
                turnStateManager.HandleCancel();
            }

            float delay = GetBatchDelay();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        Debug.Log($"{TL()} Fase3 concluída.");
    }
}
