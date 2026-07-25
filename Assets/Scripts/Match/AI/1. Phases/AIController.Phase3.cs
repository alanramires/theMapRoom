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

        yield return CommitAIWorldHeavy(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex),
            "phase3:pre-shopping");

        // Reconstrói snapshot para refletir o saldo atual pós-ações
        AIWorldSnapshot freshSnap = AIWorldSnapshot.Build(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex),
            matchController);
        if (ShouldStopAIForMatchEnd("phase3_apos_snapshot"))
            yield break;

        List<AIShoppingPlanner.ShoppingOrder> orders = AIShoppingPlanner.Decide(freshSnap);
        // A Phase3 NAO mexe em currentAIStage: ele fica em 3 durante todo o shopping e só vira 4
        // quando a fase termina (em RunAITurn, apos Phase3_Shopping). Assim um save/load no meio
        // das compras RETOMA a Phase3 e compra o que falta — Decide e deficit-aware (unidades ja
        // compradas contam e reduzem a demanda; o orcamento ja vem reduzido), entao nao recompra
        // por cima nem estoura caixa.

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
