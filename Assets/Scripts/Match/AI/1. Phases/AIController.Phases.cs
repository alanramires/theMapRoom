using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Loop principal de fases
    // Cada fase é uma coroutine que executa um estágio específico do turno da IA,
    // com pontos de verificação para pausa e interrupção caso a partida termine.
    // Implementações: AIController.Phase0-4.cs
    // -------------------------------------------------------------------------

    private IEnumerator RunAITurn(TeamId aiTeam)
    {
        float turnStart = Time.realtimeSinceStartup;
        Debug.Log($"[AI] RunAITurn iniciado para {aiTeam}.");
        if (ShouldStopAIForMatchEnd("turn_start"))
            yield break;

        // Devolve um frame para desenhar o indicador antes do planejamento pesado.
        yield return null;

        int activeTurn = matchController != null ? matchController.CurrentTurn : aiTurnNumber;
        bool sameRuntimeTurn = currentAITeam == aiTeam && aiTurnNumber == activeTurn;
        int resumeStage = sameRuntimeTurn ? Mathf.Clamp(currentAIStage, 0, 4) : 0;
        currentAITeam = aiTeam;
        if (resumeStage > 0)
            Debug.Log($"[AI Stage] Retomando turno de {aiTeam} a partir do stage {resumeStage}.");

        if (emulateStage0 && resumeStage <= 0)
        {
            currentAIStage = 0;
            float t0 = Time.realtimeSinceStartup;
            yield return Phase0_WaitForTurnReady();
            Debug.Log($"[AI Perf] Stage0 (wait): {(Time.realtimeSinceStartup - t0) * 1000f:F0}ms");
            if (ShouldStopAIForMatchEnd("apos_stage0"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage0"))
                yield break;
        }
        else
        {
            Debug.Log(resumeStage > 0
                ? "[AI Stage] Stage 0 ja concluido pelo save."
                : "[AI Stage] Stage 0 desativado por emulacao.");

            // Resume path: Stage0 foi pulado mas coroutines do load ainda podem estar em voo.
            // Aguarda as mesmas condições que Stage0 esperaria para que CommitAIWorldHeavy
            // encontre o mundo assentado em vez de um frame sobrecarregado.
            if (resumeStage > 0 && turnStateManager != null)
            {
                yield return WaitForResumeSettleTelemetry();
                if (ShouldStopAIForMatchEnd("resume_apos_command_service"))
                    yield break;
                if (ShouldStopAIForMatchEnd("resume_apos_neutral"))
                    yield break;
            }
        }

        float tCommit = Time.realtimeSinceStartup;
        yield return CommitAIWorldHeavy(aiTeam, "turn-start", rebuildPlan: false);
        Debug.Log($"[AI Perf] CommitAIWorldHeavy: {(Time.realtimeSinceStartup - tCommit) * 1000f:F0}ms");

        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);
        yield return null;
        aiTurnNumber = snapshot.TurnNumber;
        aiTeamTag    = TeamUtils.GetName(aiTeam).ToUpper();
        Debug.Log($"{TL()} Turno {snapshot.TurnNumber} | Stance: {snapshot.Stance} " +
                  $"| {snapshot.MyUnits.Count} unidades | {snapshot.EnemyUnits.Count} inimigos visíveis " +
                  $"| R$ {snapshot.Budget}");

        bool shouldReuseSavedPlan = resumeStage >= 2 && HasUsableSavedObjectivePlan(aiTeam);
        if (shouldReuseSavedPlan)
        {
            Debug.Log($"{TL("Stage")} Retomando stage {resumeStage} com plano salvo; BuildObjectivePlan ignorado.");
            float tAnalyzerResume = Time.realtimeSinceStartup;
            TeamObjectivePlan savedPlan = ObjectiveManager.GetPlanForTeam(aiTeam);
            RefreshRestoredRallyObjectiveStates(savedPlan, snapshot);
            AITacticalAnalyzer.Instance.Rebuild(aiTeam, snapshot, savedPlan);
            Debug.Log($"[AI Perf] TacticalAnalyzer.Rebuild (resume): {(Time.realtimeSinceStartup - tAnalyzerResume) * 1000f:F0}ms");
        }
        else if (resumeStage <= 3 && (emulateStage1 || emulateStage2 || emulateStage3))
        {
            currentAIStage = Mathf.Max(1, resumeStage);
            float tPlan = Time.realtimeSinceStartup;
            BuildObjectivePlan(snapshot);
            Debug.Log($"[AI Perf] BuildObjectivePlan: {(Time.realtimeSinceStartup - tPlan) * 1000f:F0}ms");
            yield return null;
            float tAnalyzer = Time.realtimeSinceStartup;
            AITacticalAnalyzer.Instance.Rebuild(aiTeam, snapshot, ObjectiveManager.GetPlanForTeam(aiTeam));
            Debug.Log($"[AI Perf] TacticalAnalyzer.Rebuild: {(Time.realtimeSinceStartup - tAnalyzer) * 1000f:F0}ms");
            yield return null;
        }

        if (emulateStage1 && resumeStage <= 1)
        {
            currentAIStage = 1;
            float t1 = Time.realtimeSinceStartup;
            yield return Phase1_CommandService(snapshot);
            Debug.Log($"[AI Perf] Stage1 (command): {(Time.realtimeSinceStartup - t1) * 1000f:F0}ms");
            if (ShouldStopAIForMatchEnd("apos_stage1"))
                yield break;
            currentAIStage = 2;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage1"))
                yield break;
        }
        else
        {
            Debug.Log($"{TL("Stage")} Stage 1 {(resumeStage > 1 ? "ja concluido pelo save" : "desativado por emulacao")}.");
        }

        if (emulateStage2 && resumeStage <= 2)
        {
            currentAIStage = 2;
            Debug.Log($"[AI Perf] PRE-Stage2 acumulado: {(Time.realtimeSinceStartup - turnStart) * 1000f:F0}ms");
            float t2 = Time.realtimeSinceStartup;
            yield return Phase2_UnitActions(snapshot);
            Debug.Log($"[AI Perf] Stage2 (actions): {(Time.realtimeSinceStartup - t2) * 1000f:F0}ms");
            if (ShouldStopAIForMatchEnd("apos_stage2"))
                yield break;
            currentAIStage = 3;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage2"))
                yield break;
        }
        else
        {
            Debug.Log($"{TL("Stage")} Stage 2 {(resumeStage > 2 ? "ja concluido pelo save" : "desativado por emulacao")}.");
        }

        if (emulateStage3 && resumeStage <= 3)
        {
            yield return WaitIfDebugShoppingPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_compras"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage3_pre"))
                yield break;
            currentAIStage = 3;
            float t3 = Time.realtimeSinceStartup;
            yield return Phase3_Shopping(snapshot);
            Debug.Log($"[AI Perf] Stage3 (shopping): {(Time.realtimeSinceStartup - t3) * 1000f:F0}ms");
            if (ShouldStopAIForMatchEnd("apos_stage3"))
                yield break;
            currentAIStage = 4;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("apos_pause_stage3"))
                yield break;
        }
        else
        {
            Debug.Log($"{TL("Stage")} Stage 3 {(resumeStage > 3 ? "ja concluido pelo save" : "desativado por emulacao")}.");
        }

        if (emulateStage4 && resumeStage <= 4)
        {
            if (ShouldStopAIForMatchEnd("antes_stage4"))
                yield break;
            currentAIStage = 4;
            float t4 = Time.realtimeSinceStartup;
            yield return Phase4_EndTurn();
            Debug.Log($"[AI Perf] Stage4 (end turn): {(Time.realtimeSinceStartup - t4) * 1000f:F0}ms");
        }
        else
        {
            Debug.Log($"{TL("Stage")} Stage 4 desativado por emulação. Controle liberado sem passar o turno.");
            isActive = false;
        }

        Debug.Log($"[AI Perf] TURNO TOTAL ({aiTeam}): {(Time.realtimeSinceStartup - turnStart) * 1000f:F0}ms");
        currentAIStage = 4;
        currentAITeam = aiTeam;
        aiCoroutine = null;
    }

    private static bool HasUsableSavedObjectivePlan(TeamId aiTeam)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null)
            return false;
        bool hasObjectives = plan.Objectives != null && plan.Objectives.Count > 0;
        bool hasRogues = plan.RogueUnitIds != null && plan.RogueUnitIds.Count > 0;
        return hasObjectives || hasRogues;
    }

    private IEnumerator WaitForResumeSettleTelemetry()
    {
        float tSettle = Time.realtimeSinceStartup;
        if (IsResumeSettleReady())
        {
            Debug.Log($"[AI Perf][ResumeSettle] ready immediately: 0ms | {BuildResumeSettleStateText()}");
            Debug.Log("[AI Perf] Resume settle wait: 0ms");
            yield break;
        }

        yield return null;

        float afterFirstFrame = Time.realtimeSinceStartup;
        Debug.Log($"[AI Perf][ResumeSettle] first frame: {(afterFirstFrame - tSettle) * 1000f:F0}ms | {BuildResumeSettleStateText()}");

        float tCommandService = Time.realtimeSinceStartup;
        float nextCommandServiceLog = tCommandService + 1f;
        while (turnStateManager != null && turnStateManager.IsAutoCommandServiceBusy)
        {
            float now = Time.realtimeSinceStartup;
            if (now >= nextCommandServiceLog)
            {
                Debug.Log($"[AI Perf][ResumeSettle] waiting command service: {(now - tCommandService) * 1000f:F0}ms | {BuildResumeSettleStateText()}");
                nextCommandServiceLog = now + 1f;
            }

            yield return null;
        }

        Debug.Log($"[AI Perf][ResumeSettle] command service wait: {(Time.realtimeSinceStartup - tCommandService) * 1000f:F0}ms | {BuildResumeSettleStateText()}");

        float tNeutral = Time.realtimeSinceStartup;
        float nextNeutralLog = tNeutral + 1f;
        bool loggedNeutralHalfSecond = false;
        while (turnStateManager != null &&
               turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - tNeutral;
            if (!loggedNeutralHalfSecond && elapsed >= 0.5f)
            {
                Debug.Log($"[AI Perf][ResumeSettle] waiting neutral >500ms: {elapsed * 1000f:F0}ms | {BuildResumeSettleStateText()}");
                loggedNeutralHalfSecond = true;
            }

            if (now >= nextNeutralLog)
            {
                Debug.Log($"[AI Perf][ResumeSettle] waiting neutral: {elapsed * 1000f:F0}ms | {BuildResumeSettleStateText()}");
                nextNeutralLog = now + 1f;
            }

            yield return null;
        }

        Debug.Log($"[AI Perf][ResumeSettle] neutral wait: {(Time.realtimeSinceStartup - tNeutral) * 1000f:F0}ms | {BuildResumeSettleStateText()}");
        Debug.Log($"[AI Perf] Resume settle wait: {(Time.realtimeSinceStartup - tSettle) * 1000f:F0}ms");
    }

    private bool IsResumeSettleReady()
    {
        if (turnStateManager == null)
            return true;

        bool replayBusy = replayManager != null && replayManager.IsStepExecutionBusy;
        return !turnStateManager.IsAutoCommandServiceBusy
            && turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral
            && !turnStateManager.IsScannerActionExecutionInProgress
            && !replayBusy;
    }

    private string BuildResumeSettleStateText()
    {
        if (turnStateManager == null)
            return "turnState=null";

        bool replayBusy = replayManager != null && replayManager.IsStepExecutionBusy;
        return $"state={turnStateManager.CurrentCursorState} stack={turnStateManager.CurrentCursorStateStackDebugText} autoCS={turnStateManager.IsAutoCommandServiceBusy} scanner={turnStateManager.IsScannerActionExecutionInProgress} replayBusy={replayBusy}";
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
            AITacticalAnalyzer.Instance.Rebuild(aiTeam, snapshot, ObjectiveManager.GetPlanForTeam(aiTeam));
        }

        if (stage <= 1 && emulateStage1)
        {
            currentAIStage = 1;
            yield return Phase1_CommandService(snapshot);
            if (ShouldStopAIForMatchEnd("debug_apos_stage1"))
                yield break;
            currentAIStage = 2;
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
            currentAIStage = 3;
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
            currentAIStage = 4;
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

        currentAIStage = 4;
        currentAITeam = aiTeam;
        aiCoroutine = null;
    }
}
