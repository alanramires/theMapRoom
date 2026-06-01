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
        Debug.Log($"[AI] RunAITurn iniciado para {aiTeam}.");
        if (ShouldStopAIForMatchEnd("turn_start"))
            yield break;

        int activeTurn = matchController != null ? matchController.CurrentTurn : aiTurnNumber;
        bool sameRuntimeTurn = currentAITeam == aiTeam && aiTurnNumber == activeTurn;
        int resumeStage = sameRuntimeTurn ? Mathf.Clamp(currentAIStage, 0, 4) : 0;
        currentAITeam = aiTeam;
        if (resumeStage > 0)
            Debug.Log($"[AI Stage] Retomando turno de {aiTeam} a partir do stage {resumeStage}.");

        if (emulateStage0 && resumeStage <= 0)
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
            Debug.Log(resumeStage > 0
                ? "[AI Stage] Stage 0 ja concluido pelo save."
                : "[AI Stage] Stage 0 desativado por emulacao.");
        }

        yield return CommitAIWorldAfterAction(aiTeam, "turn-start", rebuildPlan: false);

        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);
        aiTurnNumber = snapshot.TurnNumber;
        aiTeamTag    = TeamUtils.GetName(aiTeam).ToUpper();
        Debug.Log($"{TL()} Turno {snapshot.TurnNumber} | Stance: {snapshot.Stance} " +
                  $"| {snapshot.MyUnits.Count} unidades | {snapshot.EnemyUnits.Count} inimigos visíveis " +
                  $"| R$ {snapshot.Budget}");

        if (resumeStage <= 3 && (emulateStage1 || emulateStage2 || emulateStage3))
        {
            currentAIStage = Mathf.Max(1, resumeStage);
            BuildObjectivePlan(snapshot);
            AITacticalAnalyzer.Instance.Rebuild(aiTeam, snapshot, ObjectiveManager.GetPlanForTeam(aiTeam));
        }

        if (emulateStage1 && resumeStage <= 1)
        {
            currentAIStage = 1;
            yield return Phase1_CommandService(snapshot);
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
            yield return Phase2_UnitActions(snapshot);
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
            yield return Phase3_Shopping(snapshot);
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
