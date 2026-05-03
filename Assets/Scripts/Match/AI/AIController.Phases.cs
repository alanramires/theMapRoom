using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Loop principal de fases
    // -------------------------------------------------------------------------

    private IEnumerator RunAITurn(TeamId aiTeam)
    {
        Debug.Log($"[AI] RunAITurn iniciado para {aiTeam}.");
        currentAITeam = aiTeam;
        currentAIStage = 0;
        yield return Phase0_WaitForTurnReady();
        yield return WaitIfDebugPaused();

        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);
        aiTurnNumber = snapshot.TurnNumber;
        aiTeamTag    = TeamUtils.GetName(aiTeam).ToUpper();
        Debug.Log($"{TL()} Turno {snapshot.TurnNumber} | Stance: {snapshot.Stance} " +
                  $"| {snapshot.MyUnits.Count} unidades | {snapshot.EnemyUnits.Count} inimigos visíveis " +
                  $"| R$ {snapshot.Budget}");

        currentAIStage = 1;
        BuildObjectivePlan(snapshot);

        yield return Phase1_CommandService(snapshot);
        yield return WaitIfDebugPaused();
        currentAIStage = 2;
        yield return Phase2_UnitActions(snapshot);
        yield return WaitIfDebugPaused();
        yield return WaitIfDebugShoppingPaused();
        yield return WaitIfDebugPaused();
        currentAIStage = 3;
        yield return Phase3_Shopping(snapshot);
        yield return WaitIfDebugPaused();
        currentAIStage = 4;
        yield return Phase4_EndTurn();

        currentAIStage = 0;
        currentAITeam = TeamId.Neutral;
        aiCoroutine = null;
    }

    private IEnumerator RunAIDebugStage(TeamId aiTeam, int stage, bool resetPlan = false)
    {
        currentAITeam = aiTeam;
        currentAIStage = Mathf.Clamp(stage, 1, 3);
        yield return WaitIfDebugPaused();
        yield return new WaitUntil(() => replayManager == null || !replayManager.IsStepExecutionBusy);
        yield return new WaitUntil(() =>
            turnStateManager == null ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);

        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);
        aiTurnNumber = snapshot.TurnNumber;
        aiTeamTag    = TeamUtils.GetName(aiTeam).ToUpper();
        Debug.Log($"{TL("Stage")} Inicio debug stage {stage} | Stance: {snapshot.Stance} " +
                  $"| {snapshot.MyUnits.Count} unidades | {snapshot.EnemyUnits.Count} inimigos visiveis " +
                  $"| R$ {snapshot.Budget}");

        if (resetPlan)
        {
            TeamObjectivePlan existing = ObjectiveManager.GetOrCreatePlanForTeam(aiTeam);
            foreach (SectorObjective obj in existing.Objectives)
                ClearObjectiveHUD(obj);
            ObjectiveManager.ClearPlanForTeam(aiTeam);
            Debug.Log($"{TL("Stage")} Plano resetado antes do BuildObjectivePlan.");
        }

        currentAIStage = 1;
        BuildObjectivePlan(snapshot);

        if (stage <= 1)
        {
            currentAIStage = 1;
            yield return Phase1_CommandService(snapshot);
            yield return WaitIfDebugPaused();
        }

        if (stage <= 2)
        {
            currentAIStage = 2;
            yield return Phase2_UnitActions(snapshot);
            yield return WaitIfDebugPaused();
        }

        currentAIStage = 2;
        yield return WaitIfDebugShoppingPaused();
        yield return WaitIfDebugPaused();
        currentAIStage = 3;
        yield return Phase3_Shopping(snapshot);
        yield return WaitIfDebugPaused();
        currentAIStage = 4;
        yield return Phase4_EndTurn();

        currentAIStage = 0;
        currentAITeam = TeamId.Neutral;
        aiCoroutine = null;
    }

    // -------------------------------------------------------------------------
    // Fase 0: Aguarda serviços automáticos de início de turno
    // -------------------------------------------------------------------------

    private IEnumerator Phase0_WaitForTurnReady()
    {
        // Um frame para que os handlers de OnActiveTeamChanged das outras systems
        // (supply queue, auto command service) registrem suas coroutines primeiro.
        yield return null;

        if (turnStateManager != null)
        {
            yield return new WaitUntil(() => !turnStateManager.IsAutoCommandServiceBusy);
            yield return new WaitUntil(() =>
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);
        }

        float batchDelay = GetBatchDelay();
        if (batchDelay > 0f) yield return new WaitForSeconds(batchDelay);

        Debug.Log($"{TL()} Fase0 concluída.");
    }

    private IEnumerator Phase1_CommandService(AIWorldSnapshot snapshot)
    {
        Debug.Log($"{TL()} Fase1 — iniciando. replayManager={replayManager != null} turnStateManager={turnStateManager != null}");

        if (replayManager == null)
        {
            Debug.LogWarning($"{TL()} Fase1 — replayManager é null, abortando.");
            yield break;
        }

        if (matchController == null || !matchController.IsPlayerCommandServiceAutomatic(snapshot.AITeam))
        {
            Debug.Log($"{TL()} Fase1 — commandServiceAutomatic=false, pulando.");
            yield break;
        }

        Debug.Log($"{TL()} Fase1 — enviando batch CommandService.");
        yield return ExecuteAIBatchWithDebugStep(BuildCommandServiceBatch(snapshot.AITeam));
        Debug.Log($"{TL()} Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...");

        if (turnStateManager != null)
            yield return new WaitUntil(() => !turnStateManager.IsAutoCommandServiceBusy);

        float delay = GetBatchDelay();
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        Debug.Log($"{TL()} Fase1 — Serviço do Comando concluído.");
    }

    // -------------------------------------------------------------------------
    // Fase 2: Ações de unidades
    // -------------------------------------------------------------------------

    private IEnumerator Phase2_UnitActions(AIWorldSnapshot snapshot)
    {
        TeamId aiTeam = snapshot.AITeam;

        List<UnitManager> initial = GetAvailableUnits(aiTeam);
        if (initial.Count == 0)
        {
            Debug.Log($"{TL()} Fase 2 — sem unidades em campo, pulando.");
            yield break;
        }

        Debug.Log($"{TL()} Fase2 — iniciando ações.");
        plannedDestinations.Clear();

        while (isActive)
        {
            yield return WaitIfDebugPaused();

            List<UnitManager> available = GetAvailableUnits(aiTeam);
            if (available.Count == 0) break;

            // Reconstrói a foto do mundo após cada batch — hexes ocupados mudam
            AIWorldSnapshot current = AIWorldSnapshot.Build(aiTeam, matchController);

            // Ordena iniciativa por grupo (menor = age primeiro):
            // 0 = vacater handoff / blocker com inimigo adjacente
            // 1 = IsUnderRepair sobre construção capturável  OU  no corredor de avanço
            //     (mais perto do objetivo do que o capturador designado) — libera o hex antes
            //     do capturador avaliar suas opções de ataque/avanço.
            // 2 = objetivo normal  3 = rogue/sem objetivo
            // 4 = IsUnderRepair fora de corredor (ex: floresta no meio do nada) — age por último
            TeamObjectivePlan activePlan = ObjectiveManager.GetPlanForTeam(aiTeam);

            // Pre-pass: atualiza estado de reparo antes do sort para que IsUnderRepair
            // esteja correto quando GetInitiativeGroup classificar cada unidade.
            foreach (UnitManager u in available) UpdateRepairState(u, activePlan);

            available.Sort((a, b) =>
            {
                int groupA = GetInitiativeGroup(a, activePlan, aiTeam);
                int groupB = GetInitiativeGroup(b, activePlan, aiTeam);

                // Blocker cross-group: B fisicamente no target de A → B age primeiro para desocupar
                if (activePlan != null)
                {
                    Vector3Int? aTarget = GetAssignedTargetCell(a, activePlan);
                    if (aTarget.HasValue)
                    {
                        Vector3Int bCell = b.CurrentCellPosition; bCell.z = 0;
                        if (bCell == aTarget.Value) return 1;
                    }
                    Vector3Int? bTarget = GetAssignedTargetCell(b, activePlan);
                    if (bTarget.HasValue)
                    {
                        Vector3Int aCell = a.CurrentCellPosition; aCell.z = 0;
                        if (aCell == bTarget.Value) return -1;
                    }
                }

                if (groupA != groupB) return groupA.CompareTo(groupB);

                // Dentro do grupo 2: prioridade do objetivo (pri=1 = age primeiro)
                if (groupA == 2 && activePlan != null)
                {
                    SectorObjective objA = ResolveAssignedObjective(a, activePlan);
                    SectorObjective objB = ResolveAssignedObjective(b, activePlan);
                    if (objA == null && objB == null) return 0;
                    if (objA == null) return 1;
                    if (objB == null) return -1;
                    return objA.Priority.CompareTo(objB.Priority);
                }

                return 0;
            });

            UnitManager unit = available[0];
            PlayerAction action = DecideUnitAction(unit, current);

            if (action == null)
            {
                Debug.LogWarning($"[AI] Sem decisão para {unit.InstanceId} — marcando como agida.");
                unit.MarkAsActed();
                continue;
            }

            // Registra destino para que unidades subsequentes não colidam
            if (action.HasMoveTo && action.MoveTo != action.MoveFrom)
            {
                Vector3Int dest = action.MoveTo; dest.z = 0;
                plannedDestinations.Add(dest);
            }

            // Recalcula FoW apenas quando algo que altera visibilidade ocorreu:
            // movimento (nova posição = novo cone de visão) ou ataque (inimigo pode
            // ter morrido, liberando LOS para células antes bloqueadas).
            bool unitMoved    = action.HasMoveTo && action.MoveTo != action.MoveFrom;
            bool unitAttacked = !string.IsNullOrEmpty(action.TargetInstanceId);
            yield return ExecuteAIBatchWithDebugStep(action);
            yield return WaitIfDebugPaused();

            if (unitMoved || unitAttacked)
                matchController?.RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.DataOnly);

            float delay = GetBatchDelay();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        Debug.Log($"{TL()} Fase2 concluída.");
    }

    // -------------------------------------------------------------------------
    // Fase 3: Compras
    // -------------------------------------------------------------------------

    private IEnumerator Phase3_Shopping(AIWorldSnapshot snapshot)
    {
        Debug.Log($"{TL()} Fase3 — compras.");

        // Reconstrói snapshot para refletir o saldo atual pós-ações
        AIWorldSnapshot freshSnap = AIWorldSnapshot.Build(snapshot.AITeam, matchController);
        List<AIShoppingPlanner.ShoppingOrder> orders = AIShoppingPlanner.Decide(freshSnap);

        foreach (AIShoppingPlanner.ShoppingOrder order in orders)
        {
            if (!isActive) break;
            yield return WaitIfDebugPaused();
            yield return WaitIfDebugShoppingPaused();
            yield return WaitIfDebugPaused();

            PlayerAction batch = BuildShoppingBatch(snapshot.AITeam, order);
            Debug.Log($"{TL("Shopping")} {order.UnitToBuy.name} @ {order.Building.CurrentCellPosition}");

            yield return ExecuteAIBatchWithDebugStep(batch);
            yield return WaitIfDebugPaused();

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

    // -------------------------------------------------------------------------
    // Fase 4: Passa a vez
    // -------------------------------------------------------------------------

    private IEnumerator Phase4_EndTurn()
    {
        Debug.Log($"{TL()} Fase4 — passando a vez.");
        isActive = false;

        yield return new WaitUntil(() =>
            turnStateManager == null ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);

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
