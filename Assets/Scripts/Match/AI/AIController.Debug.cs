using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    /// <summary>
    /// Pausa ou retoma o loop da IA sem cancelar o batch em andamento.
    /// Enquanto pausada, a IA aguarda um comando de "AI RESUME" para retomar 
    /// ou "AI STEP" para executar o pr�ximo batch preparado.
    /// </summary>
    public void SetDebugPaused(bool paused)
    {
        isDebugPaused = paused;
        IsDebugPaused = paused;

        if (!paused)
        {
            debugStepRequest = DebugStepRequest.None;
            ClearDebugStepPreview();
            PanelDialogController.TrySetTransientText("AI RESUME", 1.8f);
        }

        if (showAILogs)
            Debug.Log(paused
                ? "[AI] Pausa de debug solicitada. Aguardando ponto seguro."
                : "[AI] Pausa de debug encerrada. Retomando IA.");

        if (paused)
            PanelDialogController.TrySetExternalText("AI PAUSE\nAguardando AI RESUME ou AI STEP");
    }

    /// <summary>
    /// Pause de JOGADOR: segura o loop da IA enquanto o menu in-game está aberto durante o turno
    /// da IA. Limpo — sem AI STEP/RESUME nem texto externo (isso é do pause de DEV/F10). Retomado
    /// automaticamente quando o menu fecha. Não cancela o batch em andamento (espera ponto seguro).
    /// </summary>
    public void SetPlayerPaused(bool paused)
    {
        if (isPlayerPaused == paused)
            return;
        isPlayerPaused = paused;
        if (showAILogs)
            Debug.Log(paused
                ? "[AI] Pause de jogador (menu in-game aberto). Aguardando fechar o menu."
                : "[AI] Pause de jogador encerrado (menu fechado). Retomando IA.");
    }

    public void RequestDebugStep()
    {
        if (!isDebugPaused)
        {
            Debug.Log("[AI Step] Ignorado: acione AI Pause antes de usar AI Step.");
            PanelDialogController.TrySetTransientText("AI STEP ignorado: use AI PAUSE primeiro", 2.0f);
            return;
        }

        debugStepRequest = debugStepPendingAction != null
            ? DebugStepRequest.Execute
            : DebugStepRequest.Prepare;

        Debug.Log(debugStepRequest == DebugStepRequest.Prepare
            ? "[AI Step] Preparando proximo batch."
            : "[AI Step] Executando batch preparado.");

        PanelDialogController.TrySetTransientText(
            debugStepRequest == DebugStepRequest.Prepare
                ? "AI STEP: preparando batch"
                : "AI STEP: executando batch",
            1.6f);
    }

    public void SetDebugShoppingPaused(bool paused)
    {
        isDebugShoppingPaused = paused;
        IsDebugShoppingPaused = paused;

        Debug.Log(paused
            ? "[AI Shopping] Fase 3 pausada. A IA vai parar antes das compras."
            : "[AI Shopping] Fase 3 liberada. A IA pode entrar nas compras.");

        if (paused)
            PanelDialogController.TrySetTransientText("AI SHOPPING PAUSE", 2.0f);
        else
            PanelDialogController.TrySetTransientText("AI SHOPPING RESUME", 1.8f);
    }

    public bool TryStartDebugStage(int stage, bool resetPlan = false)
    {
        if (stage < 1 || stage > 3)
        {
            Debug.Log($"[AI Stage] Stage invalido: {stage}. Use 1, 2 ou 3.");
            PanelDialogController.TrySetTransientText("AI STAGE invalido: use 1, 2 ou 3", 2.2f);
            return false;
        }

        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (replayManager == null)
            replayManager = FindAnyObjectByType<ReplayManager>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (matchController == null)
        {
            Debug.Log("[AI Stage] MatchController nao encontrado.");
            PanelDialogController.TrySetTransientText("AI STAGE: MatchController nao encontrado", 2.2f);
            return false;
        }

        TeamId aiTeam = matchController.ActiveTeam;
        if (!matchController.IsPlayerAI(aiTeam))
        {
            Debug.Log($"[AI Stage] Time ativo {aiTeam} nao e IA.");
            PanelDialogController.TrySetTransientText($"AI STAGE: time ativo {aiTeam} nao e IA", 2.2f);
            return false;
        }

        if (aiCoroutine != null)
            StopCoroutine(aiCoroutine);

        isActive = true;
        debugStepRequest = DebugStepRequest.None;
        ClearDebugStepPreview();
        aiCoroutine = StartCoroutine(RunAIDebugStage(aiTeam, stage, resetPlan));
        string resetTag = resetPlan ? " (RESET PLANO)" : "";
        Debug.Log($"[AI Stage] Reiniciando IA no stage {stage} para {aiTeam}{resetTag}.");
        PanelDialogController.TrySetTransientText($"AI STAGE {stage}{resetTag}", 2.0f);
        return true;
    }

    // -------------------------------------------------------------------------
    // Fase 1: Serviço do Comando
    // -------------------------------------------------------------------------

    private IEnumerator WaitIfDebugPaused()
    {
        if (ShouldStopAIForMatchEnd("debug_pause_start"))
            yield break;
        if (!isDebugPaused && !isPlayerPaused) yield break;

        if (showAILogs)
            Debug.Log(isPlayerPaused
                ? "[AI] Pausada (menu do jogador aberto) - aguardando fechar o menu."
                : "[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.");
        // Pause de jogador é hold ABSOLUTO (sem AI STEP): só retoma quando o menu fecha. Pause de
        // debug mantém a semântica de STEP. Resume = NÃO player-paused E (NÃO debug-paused OU step).
        yield return new WaitUntil(() =>
            !isPlayerPaused && (!isDebugPaused || debugStepRequest != DebugStepRequest.None));
        if (ShouldStopAIForMatchEnd("debug_pause_end"))
            yield break;
        if (showAILogs && !isDebugPaused && !isPlayerPaused)
            Debug.Log("[AI] Retomando execucao da IA.");
    }

    private IEnumerator WaitIfDebugShoppingPaused()
    {
        if (ShouldStopAIForMatchEnd("debug_shopping_pause_start"))
            yield break;
        if (!isDebugShoppingPaused) yield break;

        Debug.Log($"{TL("Shopping")} Pausado antes da Fase 3 - aguardando 'AI SHOPPING RESUME'.");
        PanelDialogController.TrySetExternalText("AI Shopping pausado\nAI SHOPPING RESUME para liberar compras");
        yield return new WaitUntil(() => !isDebugShoppingPaused);
        if (ShouldStopAIForMatchEnd("debug_shopping_pause_end"))
            yield break;
        PanelDialogController.ClearExternalText();
        Debug.Log($"{TL("Shopping")} Resume recebido - entrando na Fase 3.");
    }

    private float GetBatchDelay()
    {
        if (replayManager != null)
            return Mathf.Max(0f, replayManager.GetEffectiveTimeBetweenBatchesForAutoplay());
        return 0.5f;
    }

    private IEnumerator ExecuteAIBatchWithDebugStep(PlayerAction action)
    {
        if (ShouldStopAIForMatchEnd("batch_start"))
            yield break;
        if (action == null)
            yield break;

        // Pause de jogador: não inicia um batch enquanto o menu in-game do jogador estiver aberto.
        if (isPlayerPaused)
        {
            yield return new WaitUntil(() => !isPlayerPaused);
            if (ShouldStopAIForMatchEnd("batch_player_pause"))
                yield break;
        }

        if (isDebugPaused)
        {
            debugStepPendingAction = action;
            debugStepRequest = DebugStepRequest.None;
            ShowDebugStepPreview(action);

            yield return new WaitUntil(() => !isDebugPaused || debugStepRequest == DebugStepRequest.Execute);
            if (ShouldStopAIForMatchEnd("batch_debug_wait"))
                yield break;
            debugStepRequest = DebugStepRequest.None;
            ClearDebugStepPreview();
        }

        if (replayManager == null)
            yield break;

        aiTurnBatchExecuting = true;
        replayManager.ExecuteLiveAIBatch(action, iaRapida);
        yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);
        aiTurnBatchExecuting = false;
        if (ShouldStopAIForMatchEnd("batch_end"))
            yield break;
        debugStepPendingAction = null;
    }

    public IEnumerator ExecuteTutorialMoveBatch(UnitManager unit, TeamId team, Vector3Int from, Vector3Int to)
    {
        if (unit == null || replayManager == null)
            yield break;

        from.z = 0;
        to.z = 0;
        PlayerAction batch = BuildMoveBatch(unit, team, from, to);
        yield return ExecuteAIBatchWithDebugStep(batch);
    }

    private void ShowDebugStepPreview(PlayerAction action)
    {
        string description = BuildDebugStepDescription(action);
        string previewMessage = string.Empty;
        bool previewShown = turnStateManager != null &&
            turnStateManager.TryShowAIDebugStepPreview(action, out previewMessage);

        Debug.Log($"[AI Step] {description}");
        if (!string.IsNullOrWhiteSpace(previewMessage))
            Debug.Log($"[AI Step] {previewMessage}");

        PanelDialogController.TrySetExternalText(
            $"AI Step\n{description}\nF11: executar | F12: resume");

        if (!previewShown)
            Debug.Log("[AI Step] Preview visual indisponivel para este batch.");
    }

    private void ClearDebugStepPreview()
    {
        debugStepPendingAction = null;
        turnStateManager?.ClearAIDebugStepPreview();
        PanelDialogController.ClearExternalText();
    }

    private string BuildDebugStepDescription(PlayerAction action)
    {
        if (action == null)
            return "batch vazio.";

        string unitLabel = ResolveUnitLabel(action.UnitInstanceId);
        string from = action.HasMoveFrom ? FormatCell(action.MoveFrom) : "origem indefinida";
        string to = action.HasMoveTo ? FormatCell(action.MoveTo) : "destino indefinido";

        switch (action.ActionType)
        {
            case PlayerActionType.CommandService:
                return "servico do comando automatico sera executado.";
            case PlayerActionType.Shopping:
                return $"compra {action.ShoppingUnitTypeId} no hex {FormatCell(action.TargetHex)}.";
            case PlayerActionType.EndTurn:
                return "a IA vai passar a vez.";
            case PlayerActionType.UnitAction:
                switch (action.SensorAction)
                {
                    case SensorActionType.Attack:
                        return $"{unitLabel} vai de {from} ate {to} e vai atacar {action.TargetInstanceId} no hex {FormatCell(action.TargetHex)}.";
                    case SensorActionType.Capture:
                        return $"{unitLabel} vai de {from} ate {to} e vai capturar.";
                    case SensorActionType.Merge:
                        return $"{unitLabel} vai de {from} ate {to} e vai fundir.";
                    case SensorActionType.Supply:
                        return $"{unitLabel} vai de {from} ate {to} e vai suprir.";
                    default:
                        return $"{unitLabel} vai de {from} ate {to}.";
                }
            default:
                return !string.IsNullOrWhiteSpace(action.DebugLabel) ? action.DebugLabel : action.ActionType.ToString();
        }
    }

    private static string FormatCell(Vector3Int cell)
    {
        return $"({cell.x},{cell.y})";
    }

    private static string ResolveUnitLabel(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return "a unidade";

        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null)
                continue;
            if (unit.InstanceId.ToString() == instanceId)
                return $"{unit.UnitDisplayName} #{unit.InstanceId}";
        }

        return $"unidade #{instanceId}";
    }
}
