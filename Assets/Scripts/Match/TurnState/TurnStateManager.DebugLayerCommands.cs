using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum DebugLayerCommand
{
    Landing,
    Takeoff,
    AltitudeLow,
    AltitudeHigh,
    Emerge,
    Submerge
}

public partial class TurnStateManager
{
    private bool debugLayerCommandInProgress;

    public bool TryExecuteLayerCommandFromDebug(DebugLayerCommand command, out string message)
    {
        message = string.Empty;

        if (CurrentCursorState != CursorState.Neutral)
        {
            message = $"Comando de camada exige cursor em Neutral (atual: {CurrentCursorState}).";
            return false;
        }

        if (debugLayerCommandInProgress || IsMovementAnimationRunning())
        {
            message = "Aguarde o fim da animacao de camada atual.";
            return false;
        }

        if (!TryGetUnitUnderCursorForDebug(out UnitManager unit, out Vector3Int cursorCell, out message))
            return false;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : unit.BoardTilemap;
        if (!TryValidateDebugLayerCommand(unit, boardMap, command, out message))
            return false;

        debugLayerCommandInProgress = true;
        StartCoroutine(ExecuteDebugLayerCommand(unit, boardMap, cursorCell, command));
        message = $"Comando {command} iniciado em {FormatMapCellWithZ(cursorCell)}.";
        return true;
    }

    private bool TryValidateDebugLayerCommand(
        UnitManager unit,
        Tilemap boardMap,
        DebugLayerCommand command,
        out string reason)
    {
        reason = string.Empty;
        if (unit == null || boardMap == null || terrainDatabase == null)
        {
            reason = "Contexto de unidade/mapa/terreno invalido.";
            return false;
        }

        if (unit.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode mudar de camada por este comando.";
            return false;
        }

        switch (command)
        {
            case DebugLayerCommand.Landing:
            {
                PodePousarReport report = PodePousarSensor.Evaluate(
                    unit, boardMap, terrainDatabase, SensorMovementMode.MoveuParado,
                    useManualRemainingMovement: false, manualRemainingMovement: 0);
                reason = report != null ? report.explicacao : "Falha ao avaliar pouso.";
                return report != null && report.status;
            }

            case DebugLayerCommand.Takeoff:
            {
                PodeDecolarReport report = PodeDecolarSensor.Evaluate(unit, boardMap, terrainDatabase);
                if (report == null || !report.status)
                {
                    reason = report != null ? report.explicacao : "Falha ao avaliar decolagem.";
                    return false;
                }

                if (!report.canDecolar0Hex && !report.canDecolarFullMove)
                {
                    reason = "Decolagem exige deslocamento de 1 hex; o comando TAKE OFF nao recebe destino.";
                    return false;
                }

                return true;
            }

            case DebugLayerCommand.AltitudeLow:
                return CanChangeAirAltitudeFromDebug(unit, boardMap, HeightLevel.AirLow, out reason);

            case DebugLayerCommand.AltitudeHigh:
                return CanChangeAirAltitudeFromDebug(unit, boardMap, HeightLevel.AirHigh, out reason);

            case DebugLayerCommand.Emerge:
            {
                PodeEmergirReport report = PodeEmergirSensor.Evaluate(unit, boardMap, terrainDatabase);
                reason = report != null ? report.explicacao : "Falha ao avaliar emersao.";
                return report != null && report.status;
            }

            case DebugLayerCommand.Submerge:
            {
                PodeSubmergirReport report = PodeSubmergirSensor.Evaluate(
                    unit,
                    boardMap,
                    terrainDatabase);
                reason = report != null ? report.explicacao : "Falha ao avaliar submersao.";
                return report != null && report.status;
            }
        }

        reason = "Comando de camada desconhecido.";
        return false;
    }

    private static bool CanChangeAirAltitudeFromDebug(
        UnitManager unit,
        Tilemap boardMap,
        HeightLevel targetHeight,
        out string reason)
    {
        PodeMudarAltitudeReport report =
            PodeMudarAltitudeSensor.Evaluate(unit, boardMap, targetHeight);
        reason = report != null ? report.explicacao : "Falha ao avaliar mudanca de altitude.";
        return report != null && report.status;
    }

    private IEnumerator ExecuteDebugLayerCommand(
        UnitManager unit,
        Tilemap boardMap,
        Vector3Int cursorCell,
        DebugLayerCommand command)
    {
        bool applied = false;
        string failureReason = string.Empty;

        try
        {
            if (unit == null)
                yield break;

            if (command == DebugLayerCommand.Landing)
            {
                PlayMovementStartSfx(unit);
                bool startAirHigh = unit.GetHeightLevel() == HeightLevel.AirHigh;
                bool startAirLow = unit.GetHeightLevel() == HeightLevel.AirLow;
                float remainingLandingDuration;
                float beforeGroundDuration = 0f;

                if (startAirHigh)
                {
                    float totalHighToGround = GetEmbarkAirHighToGroundDuration();
                    float highToLowAt = Mathf.Clamp(
                        totalHighToGround * GetEmbarkHighToLowNormalizedTime(), 0f, totalHighToGround);
                    if (highToLowAt > 0f)
                        yield return new WaitForSeconds(highToLowAt);

                    unit.TryStepLayerStateForDebug(-1);
                    beforeGroundDuration = Mathf.Max(0f, totalHighToGround - highToLowAt);
                    remainingLandingDuration = GetEmbarkAirLowToGroundDuration();
                }
                else if (startAirLow)
                {
                    float totalLowToGround = GetEmbarkAirLowToGroundDuration();
                    float lowToGroundAt = Mathf.Clamp(
                        totalLowToGround * GetEmbarkLowToGroundNormalizedTime(), 0f, totalLowToGround);
                    if (lowToGroundAt > 0f)
                        yield return new WaitForSeconds(lowToGroundAt);
                    remainingLandingDuration = Mathf.Max(0f, totalLowToGround - lowToGroundAt);
                }
                else
                {
                    remainingLandingDuration = GetEmbarkForcedLandingDuration();
                }

                if (beforeGroundDuration > 0f)
                    yield return new WaitForSeconds(beforeGroundDuration);

                AircraftOperationRules.ResolveGroundedLayerForCell(
                    unit, boardMap, terrainDatabase, unit.CurrentCellPosition,
                    out Domain landedDomain, out HeightLevel landedHeight);
                applied = landedDomain == Domain.Land && landedHeight == HeightLevel.Surface
                    ? unit.TryStepLayerStateForDebug(-1)
                    : unit.TrySetCurrentLayerMode(landedDomain, landedHeight);
                if (applied)
                {
                    unit.SetAircraftGrounded(true);
                    unit.SetAircraftEmbarkedInCarrier(false);
                    unit.SetAircraftOperationLockTurns(0);

                    float landingFxDuration = animationManager != null
                        ? animationManager.PlayVtolLandingEffect(unit)
                        : 0f;
                    remainingLandingDuration = Mathf.Max(remainingLandingDuration, landingFxDuration);
                    if (remainingLandingDuration > 0f)
                        yield return new WaitForSeconds(remainingLandingDuration);

                    float postLandingDelay = GetEmbarkAfterForcedLandingDelay();
                    if (postLandingDelay > 0f)
                        yield return new WaitForSeconds(postLandingDelay);
                }
                else
                    failureReason = $"Falha ao aplicar pouso em {landedDomain}/{landedHeight}.";
            }
            else if (command == DebugLayerCommand.Takeoff)
            {
                AirOperationTileContext tileContext = AirOperationResolver.ResolveContext(
                    boardMap, terrainDatabase, unit.CurrentCellPosition);
                if (!AirOperationResolver.TryGetTakeoffPlan(
                        unit, tileContext, SensorMovementMode.MoveuParado,
                        out AirTakeoffPlan plan, out failureReason))
                {
                    applied = false;
                }
                else
                {
                    PlayMovementStartSfx(unit);
                    float takeoffFxDuration = animationManager != null
                        ? animationManager.PlayVtolLandingEffect(unit)
                        : 0f;

                    float groundToLowDuration = GetEmbarkAirLowToGroundDuration();
                    if (groundToLowDuration > 0f)
                        yield return new WaitForSeconds(groundToLowDuration);

                    applied = unit.TryStepLayerStateForDebug(+1);
                    if (applied)
                    {
                        unit.SetAircraftGrounded(false);
                        unit.SetAircraftEmbarkedInCarrier(false);
                        unit.SetAircraftOperationLockTurns(0);

                        if (plan.endHeight == HeightLevel.AirHigh)
                        {
                            float lowToHighDuration = GetEmbarkAirHighToGroundDuration()
                                * Mathf.Clamp01(GetEmbarkHighToLowNormalizedTime());
                            if (lowToHighDuration > 0f)
                                yield return new WaitForSeconds(lowToHighDuration);
                            applied = unit.TryStepLayerStateForDebug(+1);
                        }

                        float coveredDuration = groundToLowDuration;
                        float remainingFx = Mathf.Max(0f, takeoffFxDuration - coveredDuration);
                        if (remainingFx > 0f)
                            yield return new WaitForSeconds(remainingFx);
                    }
                    else
                        failureReason = "Falha ao aplicar etapa Air/Low da decolagem.";
                }
            }
            else
            {
                PlayMovementStartSfx(unit);
                float transitionDuration = GetLayerOperationTransitionDuration();
                if (transitionDuration > 0f)
                    yield return new WaitForSeconds(transitionDuration);

                Domain targetDomain = command == DebugLayerCommand.Emerge
                    ? Domain.Naval
                    : command == DebugLayerCommand.Submerge
                        ? Domain.Submarine
                        : Domain.Air;
                HeightLevel targetHeight = command == DebugLayerCommand.Emerge
                    ? HeightLevel.Surface
                    : command == DebugLayerCommand.Submerge
                        ? HeightLevel.Submerged
                        : command == DebugLayerCommand.AltitudeHigh
                            ? HeightLevel.AirHigh
                            : HeightLevel.AirLow;

                if (command == DebugLayerCommand.AltitudeHigh)
                    applied = unit.TryStepLayerStateForDebug(+1);
                else if (command == DebugLayerCommand.AltitudeLow)
                    applied = unit.TryStepLayerStateForDebug(-1);
                else
                    applied = unit.TrySetCurrentLayerMode(targetDomain, targetHeight);
                if (!applied)
                    failureReason = $"Falha ao aplicar {targetDomain}/{targetHeight}.";
                else
                {
                    float postTransitionDelay = GetLayerOperationAfterTransitionDelay();
                    if (postTransitionDelay > 0f)
                        yield return new WaitForSeconds(postTransitionDelay);
                }
            }

            if (applied)
            {
                TryRefreshRuntimeCachesFromDebug(out _);
                cursorController?.PlayDoneSfx();
                RuntimeLog($"[Debug Layer] {command} aplicado em {FormatMapCellWithZ(cursorCell)} sem selecionar a unidade.");
            }
            else
            {
                cursorController?.PlayErrorSfx();
                RuntimeLog($"[Debug Layer] {command} falhou: {failureReason}");
            }
        }
        finally
        {
            debugLayerCommandInProgress = false;
        }
    }
}
