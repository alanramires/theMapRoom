using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fase 3: Compras
    // -------------------------------------------------------------------------

    // Wall time: includes coroutine scheduling and any pause before the first review.
    // Stops before approval, so reading the AI Step panel is never timed.
    private System.Diagnostics.Stopwatch shoppingFirstDecisionTimer;
    private long shoppingCommitMs, shoppingSnapshotMs, shoppingDecideMs;
    private int shoppingTimingSlot, shoppingTimingTurn;

    private void MarkFirstShoppingDecisionReady(PlayerAction action, string decision, bool reviewReady)
    {
        if (shoppingFirstDecisionTimer == null || action == null ||
            action.ActionType != PlayerActionType.Shopping) return;
        long total = (shoppingFirstDecisionTimer?.ElapsedMilliseconds ?? 0L);
        shoppingFirstDecisionTimer.Stop();
        shoppingFirstDecisionTimer = null;
        long remaining = total - shoppingCommitMs - shoppingSnapshotMs - shoppingDecideMs;
        if (aiStep) Debug.Log($"[AI Shopping Timing] T{shoppingTimingTurn} slot={shoppingTimingSlot} first_decision_ready " +
            $"total={total}ms commit={shoppingCommitMs}ms snapshot={shoppingSnapshotMs}ms " +
            $"decide={shoppingDecideMs}ms preparation_scheduling_pauses={remaining}ms " +
            $"aiStep={reviewReady} decision={decision}");
    }

    private IEnumerator Phase3_Shopping(AIWorldSnapshot snapshot)
    {
        shoppingTimingSlot = snapshot.AISlotIndex;
        shoppingTimingTurn = matchController != null ? matchController.CurrentTurn : -1;
        shoppingCommitMs = shoppingSnapshotMs = shoppingDecideMs = 0;
        shoppingFirstDecisionTimer = aiStep ? System.Diagnostics.Stopwatch.StartNew() : null;
        if (aiStep)
            Debug.Log($"[AI Shopping Timing] T{shoppingTimingTurn} slot={shoppingTimingSlot} stage3_start");
        try
        {
            if (ShouldStopAIForMatchEnd("phase3_start"))
                yield break;

            Debug.Log($"{TL()} Fase3 — compras.");

            PlayerSlotId shoppingSlot = PlayerSlotId.FromIndex(snapshot.AISlotIndex);

            // Teto de unidades do time: mesmo predicado que a compra usa em
            // TurnStateManager.TryPurchaseShoppingUnitByIndex. Sem este gate a IA abria
            // o menu, tentava comprar e colhia um LogError com o batch revertido.
            if (matchController != null &&
                matchController.HasReachedMaxUnitsForSlot(shoppingSlot))
            {
                Debug.Log(
                    $"{TL("Shopping")} Teto de unidades atingido " +
                    $"({matchController.MaxUnitsPerTeam}) — nao ha o que comprar.");
                Debug.Log($"{TL()} Fase3 concluída.");
                yield break;
            }

            long timingCheckpoint = (shoppingFirstDecisionTimer?.ElapsedMilliseconds ?? 0L);
            yield return CommitAIWorldHeavy(
                PlayerSlotId.FromIndex(snapshot.AISlotIndex),
                "phase3:pre-shopping");
            shoppingCommitMs = (shoppingFirstDecisionTimer?.ElapsedMilliseconds ?? 0L) - timingCheckpoint;
            timingCheckpoint = (shoppingFirstDecisionTimer?.ElapsedMilliseconds ?? 0L);

            // Reconstrói snapshot para refletir o saldo atual pós-ações
            AIWorldSnapshot freshSnap = AIWorldSnapshot.Build(
                PlayerSlotId.FromIndex(snapshot.AISlotIndex),
                matchController);
            shoppingSnapshotMs = (shoppingFirstDecisionTimer?.ElapsedMilliseconds ?? 0L) - timingCheckpoint;
            if (ShouldStopAIForMatchEnd("phase3_apos_snapshot"))
                yield break;

            timingCheckpoint = (shoppingFirstDecisionTimer?.ElapsedMilliseconds ?? 0L);
            List<AIShoppingPlanner.ShoppingOrder> orders = DecideShoppingForReview(freshSnap);
            shoppingDecideMs = (shoppingFirstDecisionTimer?.ElapsedMilliseconds ?? 0L) - timingCheckpoint;
            // A Phase3 NAO mexe em currentAIStage: ele fica em 3 durante todo o shopping e só vira 4
            // quando a fase termina (em RunAITurn, apos Phase3_Shopping). Assim um save/load no meio
            // das compras RETOMA a Phase3 e compra o que falta — Decide e deficit-aware (unidades ja
            // compradas contam e reduzem a demanda; o orcamento ja vem reduzido), entao nao recompra
            // por cima nem estoura caixa.

            foreach (AIShoppingPlanner.ShoppingOrder order in orders)
            {
                if (!isActive || ShouldStopAIForMatchEnd("phase3_loop")) break;

                // Recheca por ordem, nao so na entrada da fase: cada compra bem
                // sucedida adiciona uma unidade, e uma lista de N ordens pode cruzar o
                // teto na ordem k. O gate de entrada nao cobre esse caso.
                if (matchController != null &&
                    matchController.HasReachedMaxUnitsForSlot(shoppingSlot))
                {
                    Debug.Log(
                        $"{TL("Shopping")} Teto de unidades atingido " +
                        $"({matchController.MaxUnitsPerTeam}) — ordens restantes canceladas.");
                    break;
                }

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

                // Segurança: fecha o menu de shopping se ficou aberto (compra falhou).
                // ANTES da pausa: o menu do jogador so abre em Neutral, e uma pausa com o
                // shopping da IA aberto segurava a IA esperando um menu que nunca abria.
                if (turnStateManager != null &&
                    turnStateManager.CurrentCursorState == TurnStateManager.CursorState.ShoppingAndServices)
                {
                    Debug.LogWarning($"{TL("Shopping")} Menu ficou aberto — fechando.");
                    turnStateManager.HandleCancel();
                }

                yield return WaitIfDebugPaused();
                if (ShouldStopAIForMatchEnd("phase3_apos_pause_batch"))
                    yield break;

                float delay = GetBatchDelay();
                if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            }

            Debug.Log($"{TL()} Fase3 concluída.");
        }
        finally
        {
            if (shoppingFirstDecisionTimer != null)
            {
                if (aiStep) Debug.Log($"[AI Shopping Timing] T{shoppingTimingTurn} slot={shoppingTimingSlot} " +
                    $"ended_without_first_decision total={(shoppingFirstDecisionTimer?.ElapsedMilliseconds ?? 0L)}ms");
                shoppingFirstDecisionTimer.Stop();
                shoppingFirstDecisionTimer = null;
            }
        }
    }
}
