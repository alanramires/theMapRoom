using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private void HandleCaptureActionRequested()
    {
        if (selectedUnit == null)
            return;
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return;

        bool canCapture = availableSensorActionCodes.Contains('C');
        SensorMovementMode movementMode = CurrentCursorState == CursorState.MoveuAndando
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;
        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        string reason = cachedPodeCapturarReason;

        if (!canCapture || !PodeCapturarSensor.TryGetCaptureTarget(
                selectedUnit,
                boardMap,
                movementMode,
                out ConstructionManager target,
                out _,
                out reason,
                matchController))
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                PushPanelUnitMessage(reason, 3.2f);
                cursorController?.PlayErrorSfx();
            }
            RuntimeLog(string.IsNullOrWhiteSpace(reason)
                ? "Pode Capturar (\"C\"): sem alvo de captura valido."
                : $"Pode Capturar (\"C\"): {reason}");
            LogScannerPanel();
            return;
        }

        cursorController?.PlayConfirmSfx();
        replayManager?.UpdateCurrentBufferSensorAction(SensorActionType.Capture, "CaptureActionRequested");
        Advance(CursorState.Capturando, "HandleCaptureActionRequested");
        ClearCommittedPathVisual();
        StartCoroutine(ExecuteCaptureSequence(target, movementMode));
    }

    private IEnumerator ExecuteCaptureSequence(ConstructionManager targetConstruction, SensorMovementMode movementMode)
    {
        captureExecutionInProgress = true;
        Advance(CursorState.CapturandoExecuting, "ExecuteCaptureSequence: begin");

        try
        {
            UnitManager capturer = selectedUnit;
            if (capturer == null || targetConstruction == null)
                yield break;

            int captureDamage = PodeCapturarSensor.GetCapturePower(capturer);
            float hp01 = Mathf.InverseLerp(1f, 10f, Mathf.Clamp(capturer.CurrentHP, 1, 10));
            float capturePitch = Mathf.Lerp(1f, 2f, hp01);
            float preSfxDelay = animationManager != null ? animationManager.CapturePreSfxDelay : 0.12f;
            float postCapturingSfxDelay = animationManager != null ? animationManager.CapturePostCapturingSfxDelay : 0.12f;
            float postDoneSfxDelay = animationManager != null ? animationManager.CapturePostDoneSfxDelay : 0.05f;
            float postCapturedSfxDelay = animationManager != null ? animationManager.CapturePostCapturedSfxDelay : 0.10f;

            if (preSfxDelay > 0f)
                yield return new WaitForSeconds(preSfxDelay);
            cursorController?.PlayCapturingSfx(capturePitch, 1f);
            if (postCapturingSfxDelay > 0f)
                yield return new WaitForSeconds(postCapturingSfxDelay);

            if (!PodeCapturarSensor.TryGetCaptureTarget(
                    capturer,
                    terrainTilemap != null ? terrainTilemap : capturer.BoardTilemap,
                    movementMode,
                    out _,
                    out PodeCapturarSensor.CaptureOperationType operationType,
                    out string operationReason,
                    matchController))
            {
                if (!string.IsNullOrWhiteSpace(operationReason))
                    PushPanelUnitMessage(operationReason, 3.2f);
                RuntimeLog(string.IsNullOrWhiteSpace(operationReason)
                    ? "[Captura] Operacao invalida no momento da execucao."
                    : $"[Captura] Operacao invalida: {operationReason}");
                FinalizeCaptureAction(capturer);
                yield break;
            }

            int before = Mathf.Max(0, targetConstruction.CurrentCapturePoints);
            int safeMax = Mathf.Max(0, targetConstruction.CapturePointsMax);
            int after;
            bool concluded;
            bool captureCompletedForReplay = false;
            TeamId newOwnerForReplay = targetConstruction.TeamId;

            if (operationType == PodeCapturarSensor.CaptureOperationType.RecoverAlly)
            {
                after = Mathf.Min(safeMax, before + captureDamage);
                targetConstruction.SetCurrentCapturePoints(after);
                concluded = after >= safeMax;
                RuntimeLog(
                    $"[Captura] {capturer.name} recuperou {captureDamage} de captura em {targetConstruction.ConstructionDisplayName} " +
                    $"({before} -> {after}).");
            }
            else
            {
                after = Mathf.Max(0, before - captureDamage);
                targetConstruction.SetCurrentCapturePoints(after);
                concluded = after <= 0;
                RuntimeLog(
                    $"[Captura] {capturer.name} causou {captureDamage} de captura em {targetConstruction.ConstructionDisplayName} " +
                    $"({before} -> {after}).");
            }

            // Obs preciso para o log de Jogadas (aqui sabemos o tipo de operação):
            //  RecoverAlly → "reparado"; CaptureEnemy concluído → "capturado"; parcial → "after/max".
            string jogadaObs = operationType == PodeCapturarSensor.CaptureOperationType.RecoverAlly
                ? "reparado"
                : (concluded ? "capturado" : $"{after}/{safeMax}");
            JogadasManager.SetUltimaCapturaObs(capturer.InstanceId, jogadaObs);

            if (concluded)
            {
                cursorController?.PlayCapturedSfx(1f, 1f);
                if (postCapturedSfxDelay > 0f)
                    yield return new WaitForSeconds(postCapturedSfxDelay);

                if (operationType == PodeCapturarSensor.CaptureOperationType.CaptureEnemy)
                {
                    TeamId previousOwnerTeam = targetConstruction.TeamId;
                    targetConstruction.SetTeamId(capturer.TeamId);
                    targetConstruction.SetCurrentCapturePoints(targetConstruction.CapturePointsMax);
                    captureCompletedForReplay = true;
                    newOwnerForReplay = capturer.TeamId;

                    // Jornal do Comandante: o dono anterior perdeu a conquista.
                    // Fog-honesto: e prédio dele — a guarnicao viu quem entrou,
                    // entao o novo dono e nomeado.
                    if (previousOwnerTeam != TeamId.Neutral && previousOwnerTeam != capturer.TeamId && matchController != null)
                    {
                        Vector3Int capturedCell = targetConstruction.CurrentCellPosition;
                        capturedCell.z = 0;
                        matchController.ReportTurnBriefingEvent(
                            previousOwnerTeam,
                            MatchController.TurnBriefingCategory.ConstructionLost,
                            targetConstruction.ConstructionDisplayName,
                            $"capturada por {TeamUtils.GetName(capturer.TeamId)}",
                            capturedCell);
                    }
                    RuntimeLog(
                        $"[Captura] Construcao capturada por {TeamUtils.GetName(capturer.TeamId)}. " +
                        $"Capture resetado para {targetConstruction.CurrentCapturePoints}/{targetConstruction.CapturePointsMax}.");

                    // Captura de QG encerra o jogo para o antigo dono (humano ou IA passam por aqui).
                    matchController?.NotifyConstructionCaptured(targetConstruction, previousOwnerTeam, capturer.TeamId);
                }
                else
                {
                    RuntimeLog(
                        $"[Captura] Construcao aliada recuperada para {targetConstruction.CurrentCapturePoints}/{targetConstruction.CapturePointsMax}.");
                }

                RecordCaptureReplayCommand(
                    capturer,
                    targetConstruction,
                    before,
                    targetConstruction.CurrentCapturePoints,
                    captureCompletedForReplay,
                    newOwnerForReplay);

                FinalizeCaptureAction(capturer);
                yield break;
            }

            cursorController?.PlayDoneSfx();
            if (postDoneSfxDelay > 0f)
                yield return new WaitForSeconds(postDoneSfxDelay);

            RecordCaptureReplayCommand(
                capturer,
                targetConstruction,
                before,
                targetConstruction.CurrentCapturePoints,
                captureCompleted: false,
                newOwner: targetConstruction.TeamId);

            FinalizeCaptureAction(capturer);
        }
        finally
        {
            captureExecutionInProgress = false;
        }
    }

    private void FinalizeCaptureAction(UnitManager capturer)
    {
        bool finalized = TryFinalizeSelectedUnitActionFromDebug();
        if (finalized)
            return;

        if (capturer != null)
            capturer.MarkAsActed();
        ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: true);
    }

}
