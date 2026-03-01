using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private void HandleCaptureActionRequested()
    {
        if (selectedUnit == null)
            return;
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return;

        bool canCapture = availableSensorActionCodes.Contains('C');
        SensorMovementMode movementMode = cursorState == CursorState.MoveuAndando
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
                out reason))
        {
            Debug.Log(string.IsNullOrWhiteSpace(reason)
                ? "Pode Capturar (\"C\"): sem alvo de captura valido."
                : $"Pode Capturar (\"C\"): {reason}");
            LogScannerPanel();
            return;
        }

        cursorController?.PlayConfirmSfx();
        SetCursorState(CursorState.Capturando, "HandleCaptureActionRequested");
        ClearCommittedPathVisual();
        StartCoroutine(ExecuteCaptureSequence(target));
    }

    private IEnumerator ExecuteCaptureSequence(ConstructionManager targetConstruction)
    {
        captureExecutionInProgress = true;

        UnitManager capturer = selectedUnit;
        if (capturer == null || targetConstruction == null)
        {
            captureExecutionInProgress = false;
            yield break;
        }

        int captureDamage = Mathf.Max(0, capturer.CurrentHP);
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
                cursorState == CursorState.MoveuAndando ? SensorMovementMode.MoveuAndando : SensorMovementMode.MoveuParado,
                out _,
                out PodeCapturarSensor.CaptureOperationType operationType,
                out string operationReason))
        {
            Debug.Log(string.IsNullOrWhiteSpace(operationReason)
                ? "[Captura] Operacao invalida no momento da execucao."
                : $"[Captura] Operacao invalida: {operationReason}");
            FinalizeCaptureAction(capturer);
            captureExecutionInProgress = false;
            yield break;
        }

        int before = Mathf.Max(0, targetConstruction.CurrentCapturePoints);
        int safeMax = Mathf.Max(0, targetConstruction.CapturePointsMax);
        int after;
        bool concluded;
        if (operationType == PodeCapturarSensor.CaptureOperationType.RecoverAlly)
        {
            after = Mathf.Min(safeMax, before + captureDamage);
            targetConstruction.SetCurrentCapturePoints(after);
            concluded = after >= safeMax;
            Debug.Log(
                $"[Captura] {capturer.name} recuperou {captureDamage} de captura em {targetConstruction.ConstructionDisplayName} " +
                $"({before} -> {after}).");
        }
        else
        {
            after = Mathf.Max(0, before - captureDamage);
            targetConstruction.SetCurrentCapturePoints(after);
            concluded = after <= 0;
            Debug.Log(
                $"[Captura] {capturer.name} causou {captureDamage} de captura em {targetConstruction.ConstructionDisplayName} " +
                $"({before} -> {after}).");
        }

        if (concluded)
        {
            cursorController?.PlayCapturedSfx(1f, 1f);
            if (postCapturedSfxDelay > 0f)
                yield return new WaitForSeconds(postCapturedSfxDelay);

            if (operationType == PodeCapturarSensor.CaptureOperationType.CaptureEnemy)
            {
                targetConstruction.SetTeamId(capturer.TeamId);
                targetConstruction.SetCurrentCapturePoints(targetConstruction.CapturePointsMax);
                Debug.Log(
                    $"[Captura] Construcao capturada por {TeamUtils.GetName(capturer.TeamId)}. " +
                    $"Capture resetado para {targetConstruction.CurrentCapturePoints}/{targetConstruction.CapturePointsMax}.");
            }
            else
            {
                Debug.Log(
                    $"[Captura] Construcao aliada recuperada para {targetConstruction.CurrentCapturePoints}/{targetConstruction.CapturePointsMax}.");
            }

            FinalizeCaptureAction(capturer);
            captureExecutionInProgress = false;
            yield break;
        }

        cursorController?.PlayDoneSfx();
        if (postDoneSfxDelay > 0f)
            yield return new WaitForSeconds(postDoneSfxDelay);
        FinalizeCaptureAction(capturer);
        captureExecutionInProgress = false;
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
