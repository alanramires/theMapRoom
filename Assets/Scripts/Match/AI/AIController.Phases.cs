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
        if (ShouldStopAIForMatchEnd("turn_start"))
            yield break;

        currentAITeam = aiTeam;
        if (emulateStage0)
        {
            currentAIStage = 0;
            yield return Phase0_WaitForTurnReady();
            if (ShouldStopAIForMatchEnd("apos_stage0"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage0"))
                yield break;
        }
        else
        {
            Debug.Log("[AI Stage] Stage 0 desativado por emulação.");
        }

        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);
        aiTurnNumber = snapshot.TurnNumber;
        aiTeamTag    = TeamUtils.GetName(aiTeam).ToUpper();
        Debug.Log($"{TL()} Turno {snapshot.TurnNumber} | Stance: {snapshot.Stance} " +
                  $"| {snapshot.MyUnits.Count} unidades | {snapshot.EnemyUnits.Count} inimigos visíveis " +
                  $"| R$ {snapshot.Budget}");

        if (emulateStage1 || emulateStage2 || emulateStage3)
        {
            currentAIStage = 1;
            BuildObjectivePlan(snapshot);
        }

        if (emulateStage1)
        {
            currentAIStage = 1;
            yield return Phase1_CommandService(snapshot);
            if (ShouldStopAIForMatchEnd("apos_stage1"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage1"))
                yield break;
        }
        else
        {
            Debug.Log($"{TL("Stage")} Stage 1 desativado por emulação.");
        }

        if (emulateStage2)
        {
            currentAIStage = 2;
            yield return Phase2_UnitActions(snapshot);
            if (ShouldStopAIForMatchEnd("apos_stage2"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage2"))
                yield break;
        }
        else
        {
            Debug.Log($"{TL("Stage")} Stage 2 desativado por emulação.");
        }

        if (emulateStage3)
        {
            yield return WaitIfDebugShoppingPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_compras"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage3_pre"))
                yield break;
            currentAIStage = 3;
            yield return Phase3_Shopping(snapshot);
            if (ShouldStopAIForMatchEnd("apos_stage3"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage3"))
                yield break;
        }
        else
        {
            Debug.Log($"{TL("Stage")} Stage 3 desativado por emulação.");
        }

        if (emulateStage4)
        {
            if (ShouldStopAIForMatchEnd("antes_stage4"))
                yield break;
            currentAIStage = 4;
            yield return Phase4_EndTurn();
        }
        else
        {
            Debug.Log($"{TL("Stage")} Stage 4 desativado por emulação. Controle liberado sem passar o turno.");
            isActive = false;
        }

        currentAIStage = 0;
        currentAITeam = TeamId.Neutral;
        aiCoroutine = null;
    }

    private IEnumerator RunAIDebugStage(TeamId aiTeam, int stage, bool resetPlan = false)
    {
        if (ShouldStopAIForMatchEnd("debug_stage_start"))
            yield break;

        currentAITeam = aiTeam;
        currentAIStage = Mathf.Clamp(stage, 1, 3);
        yield return WaitIfDebugPaused();
        if (ShouldStopAIForMatchEnd("debug_apos_pause_inicial"))
            yield break;
        yield return new WaitUntil(() => replayManager == null || !replayManager.IsStepExecutionBusy);
        if (ShouldStopAIForMatchEnd("debug_apos_replay_busy"))
            yield break;
        yield return new WaitUntil(() =>
            turnStateManager == null ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);
        if (ShouldStopAIForMatchEnd("debug_apos_neutral"))
            yield break;

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

        if (stage <= 3 && (emulateStage1 || emulateStage2 || emulateStage3))
        {
            currentAIStage = 1;
            BuildObjectivePlan(snapshot);
        }

        if (stage <= 1 && emulateStage1)
        {
            currentAIStage = 1;
            yield return Phase1_CommandService(snapshot);
            if (ShouldStopAIForMatchEnd("debug_apos_stage1"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("debug_apos_pause_stage1"))
                yield break;
        }
        else if (stage <= 1)
        {
            Debug.Log($"{TL("Stage")} Stage 1 desativado por emulação.");
        }

        if (stage <= 2 && emulateStage2)
        {
            currentAIStage = 2;
            yield return Phase2_UnitActions(snapshot);
            if (ShouldStopAIForMatchEnd("debug_apos_stage2"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("debug_apos_pause_stage2"))
                yield break;
        }
        else if (stage <= 2)
        {
            Debug.Log($"{TL("Stage")} Stage 2 desativado por emulação.");
        }

        if (stage <= 3 && emulateStage3)
        {
            currentAIStage = 2;
            yield return WaitIfDebugShoppingPaused();
            if (ShouldStopAIForMatchEnd("debug_apos_pause_compras"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("debug_apos_pause_stage3_pre"))
                yield break;
            currentAIStage = 3;
            yield return Phase3_Shopping(snapshot);
            if (ShouldStopAIForMatchEnd("debug_apos_stage3"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("debug_apos_pause_stage3"))
                yield break;
        }
        else if (stage <= 3)
        {
            Debug.Log($"{TL("Stage")} Stage 3 desativado por emulação.");
        }

        if (emulateStage4)
        {
            if (ShouldStopAIForMatchEnd("debug_antes_stage4"))
                yield break;
            currentAIStage = 4;
            yield return Phase4_EndTurn();
        }
        else
        {
            Debug.Log($"{TL("Stage")} Stage 4 desativado por emulação. Controle liberado sem passar o turno.");
            isActive = false;
        }

        currentAIStage = 0;
        currentAITeam = TeamId.Neutral;
        aiCoroutine = null;
    }

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

        if (matchController == null || !matchController.IsPlayerCommandServiceAutomatic(snapshot.AITeam))
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

        float delay = GetBatchDelay();
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        Debug.Log($"{TL()} Fase1 — Serviço do Comando concluído.");
    }

    // -------------------------------------------------------------------------
    // Fase 2: Ações de unidades
    // -------------------------------------------------------------------------

    private IEnumerator Phase2_UnitActions(AIWorldSnapshot snapshot)
    {
        if (ShouldStopAIForMatchEnd("phase2_start"))
            yield break;

        TeamId aiTeam = snapshot.AITeam;

        List<UnitManager> initial = GetAvailableUnits(aiTeam);
        if (initial.Count == 0)
        {
            Debug.Log($"{TL()} Fase 2 — sem unidades em campo, pulando.");
            yield break;
        }

        Debug.Log($"{TL()} Fase2 — iniciando ações.");
        plannedDestinations.Clear();
        var deferredUnitIds = new HashSet<int>();
        Dictionary<int, int> prevGroupCache = null;

        while (isActive && !IsMatchEnded())
        {
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase2_loop_apos_pause"))
                yield break;

            List<UnitManager> available = GetAvailableUnits(aiTeam);
            if (available.Count == 0) break;
            if (deferredUnitIds.Count > 0)
            {
                available.RemoveAll(u => u != null && deferredUnitIds.Contains(u.InstanceId));
                if (available.Count == 0)
                {
                    deferredUnitIds.Clear();
                    available = GetAvailableUnits(aiTeam);
                    if (available.Count == 0) break;
                }
            }

            // Reconstrói a foto do mundo após cada batch — hexes ocupados mudam
            // BuildLight omite campos não usados pelos handlers (MyUnits, EnemyUnits,
            // OccupiedCells, Stance), reduzindo custo de ~50 iterações por unidade.
            AIWorldSnapshot current = AIWorldSnapshot.BuildLight(aiTeam, matchController);

            // Ordena iniciativa por grupo (menor = age primeiro):
            // 0 = vacater handoff / blocker com inimigo adjacente
            // 1 = helicoptero
            // 2 = unidade ativa liberando corredor/posicionamento
            // 3 = objetivo normal  4 = rogue/sem objetivo
            // 5 = IsUnderRepair / manutencao - age por ultimo
            TeamObjectivePlan activePlan = ObjectiveManager.GetPlanForTeam(aiTeam);
            InvalidateStaleThreatObjectives(activePlan, aiTeam);

            // Pre-pass: atualiza estado de reparo antes do sort para que IsUnderRepair
            // esteja correto quando GetInitiativeGroup classificar cada unidade.
            foreach (UnitManager u in available) UpdateRepairState(u, activePlan);

            // Pre-computa grupos uma vez por unidade (evita O(N log N) chamadas no comparador).
            var groupCache = new Dictionary<int, int>(available.Count);
            foreach (UnitManager u in available)
                groupCache[u.InstanceId] = GetInitiativeGroup(u, activePlan, aiTeam);

            // Dirty flag: grupos podem mudar após cada ação (captura concluída, reparo, etc.).
            // Só re-sort quando ao menos um grupo mudou em relação à iteração anterior.
            bool needsSort = true;
            if (!needsSort)
            {
                foreach (UnitManager u in available)
                {
                    if (!prevGroupCache.TryGetValue(u.InstanceId, out int prev) || prev != groupCache[u.InstanceId])
                    {
                        needsSort = true;
                        break;
                    }
                }
            }

            if (needsSort)
            {
                available.Sort((a, b) =>
                {
                    int groupA = groupCache[a.InstanceId];
                    int groupB = groupCache[b.InstanceId];

                    if (groupA != groupB) return groupA.CompareTo(groupB);

                    // Dentro do grupo 0: blocker (IsBlockingCaptureTarget) age antes de vacater/outros
                    if (groupA == 0 && activePlan != null)
                    {
                        bool blockerA = IsBlockingCaptureTarget(a, activePlan, aiTeam);
                        bool blockerB = IsBlockingCaptureTarget(b, activePlan, aiTeam);
                        if (blockerA != blockerB) return blockerA ? -1 : 1;
                    }

                    // Dentro do grupo 3: prioridade do objetivo (pri=1 = age primeiro)
                    if (groupA == 3 && activePlan != null)
                    {
                        SectorObjective objA = ResolveAnyAssignedObjective(a, activePlan);
                        SectorObjective objB = ResolveAnyAssignedObjective(b, activePlan);
                        if (objA == null && objB == null) return b.CurrentHP.CompareTo(a.CurrentHP);
                        if (objA == null) return 1;
                        if (objB == null) return -1;

                        int cmp = objA.Priority.CompareTo(objB.Priority);
                        if (cmp != 0) return cmp;

                        return b.CurrentHP.CompareTo(a.CurrentHP);
                    }

                    int initiativeCmp = CompareUnitInitiative(a, b);
                    return initiativeCmp != 0 ? initiativeCmp : b.CurrentHP.CompareTo(a.CurrentHP);
                });
            }

            prevGroupCache = groupCache;

            // LOG: ordem de iniciativa apos o sort real.
            {
                var initLog = new System.Text.StringBuilder();
                initLog.AppendLine($"{TL()} Fase2 iniciativa ({available.Count} unidades):");
                foreach (UnitManager u in available)
                {
                    int g  = groupCache[u.InstanceId];
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    Vector3Int? tgt = GetAssignedTargetCell(u, activePlan);
                    string tgtStr = tgt.HasValue ? tgt.Value.ToString() : "null";
                    initLog.AppendLine($"  [grp={g}] {FormatInitiativeUnitName(u)} @ {uc} target={tgtStr}");
                }
                Debug.Log(initLog.ToString());
            }

            UnitManager unit = available[0];
            PlayerAction action = DecideUnitAction(unit, current);

            if (IsNoOpUnitAction(action) && ShouldDeferIdleAssaultForSectorCapturer(unit, activePlan, aiTeam))
            {
                SectorObjective obj = ResolveAssignedAssaultObjective(unit, activePlan);
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — batedor {unit.InstanceId} cede vez para capturador de {obj.Sector}");
                continue;
            }

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
            if (ShouldStopAIForMatchEnd("phase2_apos_batch"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase2_apos_pause_batch"))
                yield break;

            if (unitMoved || unitAttacked)
            {
                matchController?.RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.DataOnly);
            }

            float delay = GetBatchDelay();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        Debug.Log($"{TL()} Fase2 concluída.");
    }

    private static bool IsNoOpUnitAction(PlayerAction action)
    {
        if (action == null) return false;
        if (!string.IsNullOrEmpty(action.TargetInstanceId)) return false;
        if (!string.IsNullOrEmpty(action.TargetConstructionId)) return false;
        if (!action.HasMoveTo || !action.HasMoveFrom) return false;

        Vector3Int from = action.MoveFrom; from.z = 0;
        Vector3Int to = action.MoveTo; to.z = 0;
        return from == to;
    }

    private bool ShouldDeferIdleAssaultForSectorCapturer(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (unit == null || plan == null) return false;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Assalto)
            return false;

        SectorObjective assaultObjective = ResolveAssignedAssaultObjective(unit, plan);
        if (assaultObjective == null) return false;

        foreach (SlotNeed slot in assaultObjective.Slots)
        {
            if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (capturer != null && !capturer.HasActed)
                return true;
        }

        return false;
    }

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
