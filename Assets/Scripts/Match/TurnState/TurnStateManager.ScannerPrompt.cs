using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class TurnStateManager
{
    private enum ScannerPromptStep
    {
        AwaitingAction = 0,
        MirandoCycleTarget = 1,
        MirandoConfirmTarget = 2,
        EmbarkCycleTarget = 3,
        EmbarkConfirmTarget = 4,
        LandingCycleOption = 5,
        LandingConfirmOption = 6
    }

    private enum LandingOptionAction
    {
        None = 0,
        DescendToAirLow = 1,
        AscendToAirHigh = 2,
        Land = 3,
        DomainTransition = 4
    }

    private readonly struct LandingOption
    {
        public readonly LandingOptionAction action;
        public readonly string label;
        public readonly Domain fromDomain;
        public readonly HeightLevel fromHeightLevel;
        public readonly Domain toDomain;
        public readonly HeightLevel toHeightLevel;

        public LandingOption(LandingOptionAction action, string label)
        {
            this.action = action;
            this.label = label ?? string.Empty;
            fromDomain = Domain.Land;
            fromHeightLevel = HeightLevel.Surface;
            toDomain = Domain.Land;
            toHeightLevel = HeightLevel.Surface;
        }

        public LandingOption(
            LandingOptionAction action,
            string label,
            Domain fromDomain,
            HeightLevel fromHeightLevel,
            Domain toDomain,
            HeightLevel toHeightLevel)
        {
            this.action = action;
            this.label = label ?? string.Empty;
            this.fromDomain = fromDomain;
            this.fromHeightLevel = fromHeightLevel;
            this.toDomain = toDomain;
            this.toHeightLevel = toHeightLevel;
        }
    }

    private sealed class MirandoSpotterPreviewTrack
    {
        public readonly List<LineRenderer> renderers = new List<LineRenderer>();
        public readonly List<Vector3> pathPoints = new List<Vector3>();
        public readonly List<Vector3> tempSegmentPoints = new List<Vector3>();
        public float pathLength;
        public float headDistance;
    }

    private readonly struct DeathTarget
    {
        public readonly UnitManager unit;
        public readonly Vector3Int cell;
        public readonly Vector3 worldPos;

        public DeathTarget(UnitManager unit, Vector3Int cell, Vector3 worldPos)
        {
            this.unit = unit;
            this.cell = cell;
            this.worldPos = worldPos;
        }
    }

    private ScannerPromptStep scannerPromptStep = ScannerPromptStep.AwaitingAction;
    private int scannerSelectedTargetIndex = -1;
    private int scannerSelectedEmbarkIndex = -1;
    private int scannerSelectedLandingIndex = -1;
    private bool embarkExecutionInProgress;
    private bool landingExecutionInProgress;
    private bool combatExecutionInProgress;
    private CursorState cursorStateBeforeMirando = CursorState.MoveuParado;
    private CursorState cursorStateBeforeEmbarcando = CursorState.MoveuParado;
    private CursorState cursorStateBeforePousando = CursorState.MoveuParado;
    private CursorState lastLoggedCursorState = (CursorState)(-1);
    private ScannerPromptStep lastLoggedScannerPromptStep = (ScannerPromptStep)(-1);
    private UnitManager lastLoggedSelectedUnit;
    private readonly List<LineRenderer> mirandoPreviewRenderers = new List<LineRenderer>();
    private readonly List<Vector3> mirandoPreviewPathPoints = new List<Vector3>();
    private readonly List<Vector3> mirandoPreviewSegmentPoints = new List<Vector3>();
    private float mirandoPreviewPathLength;
    private float mirandoPreviewHeadDistance;
    private bool mirandoPreviewSignatureValid;
    private Vector3 mirandoPreviewLastFrom;
    private Vector3 mirandoPreviewLastTo;
    private WeaponTrajectoryType mirandoPreviewLastTrajectory;
    private float mirandoPreviewLastBend;
    private int mirandoPreviewLastSamples;
    private readonly List<MirandoSpotterPreviewTrack> mirandoSpotterPreviewTracks = new List<MirandoSpotterPreviewTrack>();
    private LineRenderer embarkPreviewRenderer;
    private readonly List<Vector3> embarkPreviewPathPoints = new List<Vector3>();
    private readonly List<Vector3> embarkPreviewSegmentPoints = new List<Vector3>();
    private float embarkPreviewPathLength;
    private float embarkPreviewHeadDistance;
    private Color embarkPreviewColor = Color.white;
    private readonly List<LandingOption> cachedLandingOptions = new List<LandingOption>();
    private string landingOptionUnavailableReason = string.Empty;

    private void Update()
    {
        TrackRuntimeDebugLogs();
        ProcessScannerPromptInput();
        UpdateMirandoPreviewAnimation();
        UpdateEmbarkPreviewAnimation();
    }

    private void TrackRuntimeDebugLogs()
    {
        if (!Application.isPlaying)
            return;

        bool stateChanged = lastLoggedCursorState != cursorState;
        bool substepChanged = lastLoggedScannerPromptStep != scannerPromptStep;
        bool selectedChanged = lastLoggedSelectedUnit != selectedUnit;
        if (!stateChanged && !selectedChanged && !substepChanged)
            return;

        ScannerPromptStep previousSubstep = lastLoggedScannerPromptStep;
        lastLoggedCursorState = cursorState;
        lastLoggedScannerPromptStep = scannerPromptStep;
        lastLoggedSelectedUnit = selectedUnit;
        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        Debug.Log($"[TurnState] state={cursorState} | selected={selectedName}");
        if (substepChanged)
        {
            bool rollback = previousSubstep != (ScannerPromptStep)(-1) && (int)scannerPromptStep < (int)previousSubstep;
            string rollbackTag = rollback ? " [roll back]" : string.Empty;
            Debug.Log($"[TurnState]{rollbackTag} substep={previousSubstep} -> {scannerPromptStep} | state={cursorState}");
        }

    }

    private void ResetScannerPromptState()
    {
        scannerPromptStep = ScannerPromptStep.AwaitingAction;
        scannerSelectedTargetIndex = -1;
        scannerSelectedEmbarkIndex = -1;
        scannerSelectedLandingIndex = -1;
        combatExecutionInProgress = false;
        cachedLandingOptions.Clear();
        ClearMirandoPreview();
        ClearEmbarkPreview();
    }

    private bool HandleScannerPromptCancel()
    {
        if (combatExecutionInProgress)
            return true;

        if (cursorState == CursorState.Mirando && scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget)
        {
            if (cachedPodeMirarTargets.Count <= 1)
            {
                ExitMirandoStateToMovement();
                return true;
            }

            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            FocusCurrentMirandoTarget(logDetails: true);
            return true;
        }

        if (cursorState == CursorState.Embarcando &&
            scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget)
        {
            scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
            FocusCurrentEmbarkTarget(logDetails: true);
            return true;
        }

        if (cursorState == CursorState.Embarcando &&
            scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget)
        {
            ExitEmbarkStateToMovement();
            return true;
        }

        if (cursorState == CursorState.Pousando &&
            scannerPromptStep == ScannerPromptStep.LandingConfirmOption)
        {
            if (cachedLandingOptions.Count <= 1)
            {
                ExitLandingStateToMovement();
                return true;
            }

            scannerPromptStep = ScannerPromptStep.LandingCycleOption;
            LogLandingSelectionPanel();
            return true;
        }

        if (cursorState == CursorState.Pousando &&
            scannerPromptStep == ScannerPromptStep.LandingCycleOption)
        {
            ExitLandingStateToMovement();
            return true;
        }

        return false;
    }

    private void ProcessScannerPromptInput()
    {
        if (IsMovementAnimationRunning() || embarkExecutionInProgress || landingExecutionInProgress || combatExecutionInProgress)
            return;

        if (cursorState == CursorState.Mirando)
            return;

        bool isMovementScannerState = cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado;
        bool isLandingScannerState = cursorState == CursorState.Pousando;
        bool isEmbarkScannerState = cursorState == CursorState.Embarcando;
        if (!isMovementScannerState && !isLandingScannerState && !isEmbarkScannerState)
            return;

        if (scannerPromptStep == ScannerPromptStep.AwaitingAction)
        {
            if (!isMovementScannerState)
                return;

            if (WasLetterPressedThisFrame('A'))
            {
                HandleAimActionRequested();
                return;
            }

            if (WasLetterPressedThisFrame('E'))
            {
                HandleEmbarkActionRequested();
                return;
            }

            if (WasLetterPressedThisFrame('L'))
            {
                HandleLandingSensorRequested();
                return;
            }

            if (WasLetterPressedThisFrame('M'))
            {
                HandleMoveOnlyActionRequested();
                return;
            }

            return;
        }

        if (cursorState == CursorState.Embarcando &&
            (scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget || scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget))
        {
            if (TryReadPressedNumber(out int number))
            {
                int index = number - 1;
                if (index >= 0 && index < GetEmbarkEntryCount())
                {
                    cursorController?.PlayConfirmSfx();
                    scannerSelectedEmbarkIndex = index;
                    scannerPromptStep = ScannerPromptStep.EmbarkConfirmTarget;
                    FocusCurrentEmbarkTarget(logDetails: true);
                    if (TryGetSelectedValidEmbarkOption(out PodeEmbarcarOption selected, out int shownIndex))
                    {
                        string label = !string.IsNullOrWhiteSpace(selected.displayLabel) ? selected.displayLabel : "transportador";
                        Debug.Log($"Confirma embarque {shownIndex}? {label}\n(Enter=sim, ESC=voltar para ciclar)");
                    }
                }
            }
        }

        if (cursorState == CursorState.Pousando &&
            scannerPromptStep == ScannerPromptStep.LandingCycleOption)
        {
            if (TryReadPressedNumber(out int number))
            {
                int index = number - 1;
                PromptLandingOptionConfirmation(index, playConfirmSfx: true);
            }
        }

    }

    private void HandleAimActionRequested()
    {
        bool canAim = availableSensorActionCodes.Contains('A');
        if (!canAim || cachedPodeMirarTargets.Count == 0)
        {
            Debug.Log("Pode Mirar (\"A\"): nao ha alvos validos agora.");
            LogScannerPanel();
            return;
        }

        cursorController?.PlayConfirmSfx();
        FocusFirstOptionForAction('A');
        EnterMirandoState();
    }

    private void HandleMoveOnlyActionRequested()
    {
        bool finished = TryFinalizeSelectedUnitActionFromDebug();
        if (finished)
        {
            cursorController?.PlayDoneSfx();
            Debug.Log("[Acao] Apenas Mover (\"M\") confirmado. Unidade finalizou sem atacar.");
            ResetScannerPromptState();
            return;
        }

        Debug.Log("[Acao] Apenas Mover (\"M\") indisponivel no estado atual.");
    }

    private void HandleLandingSensorRequested()
    {
        if (selectedUnit == null)
            return;
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return;

        BuildLandingOptionsFromCurrentState();

        if (cachedLandingOptions.Count == 0)
        {
            string reason = !string.IsNullOrWhiteSpace(landingOptionUnavailableReason)
                ? landingOptionUnavailableReason
                : "Sem opcoes de mudanca de camada neste contexto.";
            Debug.Log($"Pode Mudar de Altitude (\"L\"): {reason}");
            LogScannerPanel();
            return;
        }

        cursorStateBeforePousando = cursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(CursorState.Pousando, "HandleLandingSensorRequested");
        ClearCommittedPathVisual();
        scannerSelectedLandingIndex = 0;
        if (cachedLandingOptions.Count == 1)
        {
            // Auto-select when there is a single possible landing action.
            PromptLandingOptionConfirmation(0, playConfirmSfx: true);
        }
        else
        {
            scannerPromptStep = ScannerPromptStep.LandingCycleOption;
            cursorController?.PlayConfirmSfx();
            LogLandingSelectionPanel();
        }
    }

    private void HandleEmbarkActionRequested()
    {
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return;

        bool hasValid = cachedPodeEmbarcarTargets.Count > 0;
        if (!hasValid)
        {
            Debug.Log("Pode Embarcar (\"E\"): nao ha transportador valido adjacente.");
            LogScannerPanel();
            return;
        }

        cursorController?.PlayConfirmSfx();
        // Mesma regra do Mirando: ao entrar em um submenu de sensor, oculta o preview de movimento.
        cursorStateBeforeEmbarcando = cursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(CursorState.Embarcando, "HandleEmbarkActionRequested");
        ClearCommittedPathVisual();
        scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
        scannerSelectedEmbarkIndex = 0;
        FocusCurrentEmbarkTarget(logDetails: true);
        LogEmbarkSelectionPanel();
    }

    private void LogLandingSelectionPanel()
    {
        if (cachedLandingOptions.Count <= 0)
        {
            Debug.Log("[Landing] Sem opcoes.");
            return;
        }

        if (scannerSelectedLandingIndex < 0 || scannerSelectedLandingIndex >= cachedLandingOptions.Count)
            scannerSelectedLandingIndex = 0;

        string text = $"Opcoes de Altitude/Camada: {cachedLandingOptions.Count}\n";
        text += "Digite 1..9 ou use setas para selecionar.\n";
        for (int i = 0; i < cachedLandingOptions.Count; i++)
        {
            string marker = i == scannerSelectedLandingIndex ? ">" : " ";
            text += $"{marker} {i + 1}. {cachedLandingOptions[i].label}\n";
        }

        if (scannerPromptStep == ScannerPromptStep.LandingConfirmOption && scannerSelectedLandingIndex >= 0 && scannerSelectedLandingIndex < cachedLandingOptions.Count)
            text += $"Confirma \"{cachedLandingOptions[scannerSelectedLandingIndex].label}\"? (Enter=sim, ESC=nao)\n";
        else
            text += "Enter confirma opcao selecionada | ESC volta\n";

        bool hasLandOption = false;
        for (int i = 0; i < cachedLandingOptions.Count; i++)
        {
            if (cachedLandingOptions[i].action == LandingOptionAction.Land)
            {
                hasLandOption = true;
                break;
            }
        }

        if (!hasLandOption && !string.IsNullOrWhiteSpace(landingOptionUnavailableReason))
            text += $"Mudanca de camada indisponivel: {landingOptionUnavailableReason}\n";

        Debug.Log(text);
    }

    private void FocusFirstOptionForAction(char actionCode)
    {
        if (cursorController == null)
            return;

        switch (char.ToUpperInvariant(actionCode))
        {
            case 'A':
            {
                if (cachedPodeMirarTargets.Count <= 0)
                    return;

                PodeMirarTargetOption firstAim = cachedPodeMirarTargets[0];
                if (firstAim == null || firstAim.targetUnit == null)
                    return;

                Vector3Int targetCell = firstAim.targetUnit.CurrentCellPosition;
                targetCell.z = 0;
                cursorController.SetCell(targetCell, playMoveSfx: false);
                break;
            }
            case 'E':
            {
                if (cachedPodeEmbarcarTargets.Count <= 0)
                    return;

                PodeEmbarcarOption firstEmbark = cachedPodeEmbarcarTargets[0];
                if (firstEmbark == null || firstEmbark.transporterUnit == null)
                    return;

                Vector3Int targetCell = firstEmbark.transporterUnit.CurrentCellPosition;
                targetCell.z = 0;
                cursorController.SetCell(targetCell, playMoveSfx: false);
                break;
            }
        }
    }

    private bool TryConfirmScannerLanding()
    {
        if (cursorState != CursorState.Pousando)
            return false;

        if (scannerPromptStep == ScannerPromptStep.LandingCycleOption)
        {
            if (cachedLandingOptions.Count <= 0)
                return true;

            if (scannerSelectedLandingIndex < 0 || scannerSelectedLandingIndex >= cachedLandingOptions.Count)
                scannerSelectedLandingIndex = 0;

            PromptLandingOptionConfirmation(scannerSelectedLandingIndex, playConfirmSfx: true);
            return true;
        }

        if (scannerPromptStep != ScannerPromptStep.LandingConfirmOption)
            return false;

        if (scannerSelectedLandingIndex < 0 || scannerSelectedLandingIndex >= cachedLandingOptions.Count)
        {
            scannerPromptStep = ScannerPromptStep.LandingCycleOption;
            scannerSelectedLandingIndex = 0;
            LogLandingSelectionPanel();
            return true;
        }

        LandingOption picked = cachedLandingOptions[scannerSelectedLandingIndex];
        Debug.Log($"[LayerOperation] Confirmado: {picked.fromDomain}/{picked.fromHeightLevel} -> {picked.toDomain}/{picked.toHeightLevel} (action={picked.action})");
        landingExecutionInProgress = true;
        StartCoroutine(ExecuteLandingOptionSequence(picked));
        return true;
    }

    private void PromptLandingOptionConfirmation(int index, bool playConfirmSfx)
    {
        if (cachedLandingOptions.Count <= 0)
            return;
        if (index < 0 || index >= cachedLandingOptions.Count)
            return;

        scannerSelectedLandingIndex = index;
        scannerPromptStep = ScannerPromptStep.LandingConfirmOption;
        if (playConfirmSfx)
            cursorController?.PlayConfirmSfx();

        LandingOption option = cachedLandingOptions[scannerSelectedLandingIndex];
        Debug.Log($"Confirma \"{option.label}\"? (Enter=sim, ESC=nao)");
    }

    private System.Collections.IEnumerator ExecuteLandingOptionSequence(LandingOption option)
    {
        try
        {
            if (selectedUnit == null)
            {
                scannerPromptStep = ScannerPromptStep.LandingCycleOption;
                yield break;
            }

            Tilemap boardMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
            switch (option.action)
            {
                case LandingOptionAction.DescendToAirLow:
                {
                    if (!selectedUnit.TrySetCurrentLayerMode(Domain.Air, HeightLevel.AirLow))
                    {
                        Debug.Log("[Landing] Falha ao aplicar transicao para Air/Low.");
                        scannerPromptStep = ScannerPromptStep.LandingCycleOption;
                        LogLandingSelectionPanel();
                        yield break;
                    }

                    cursorController?.PlayConfirmSfx();

                    // Descer de Air/High para Air/Low consome a acao da unidade e encerra turno.
                    bool finished = TryFinalizeSelectedUnitActionFromDebug();
                    if (finished)
                    {
                        cursorController?.PlayDoneSfx();
                        ResetScannerPromptState();
                        yield break;
                    }

                    ExitLandingStateToMovement(rollback: false);
                    RefreshSensorsForCurrentState();
                    break;
                }
                case LandingOptionAction.AscendToAirHigh:
                {
                    PlayMovementStartSfx(selectedUnit);
                    float duration = GetEmbarkAirHighToGroundDuration() * Mathf.Clamp01(GetEmbarkHighToLowNormalizedTime());
                    if (duration > 0f)
                        yield return new WaitForSeconds(duration);
                    selectedUnit.TrySetCurrentLayerMode(Domain.Air, HeightLevel.AirHigh);
                    cursorController?.PlayConfirmSfx();
                    bool ascendFinished = TryFinalizeSelectedUnitActionFromDebug();
                    if (ascendFinished)
                    {
                        cursorController?.PlayDoneSfx();
                        ResetScannerPromptState();
                        yield break;
                    }
                    ExitLandingStateToMovement(rollback: false);
                    RefreshSensorsForCurrentState();
                    break;
                }
                case LandingOptionAction.Land:
                {
                    bool isAirToGroundLanding =
                        option.fromDomain == Domain.Air &&
                        option.toDomain == Domain.Land &&
                        option.toHeightLevel == HeightLevel.Surface;
                    if (!isAirToGroundLanding)
                    {
                        Debug.Log("[Landing] Opcao Land fora de Air->Land detectada. Aplicando como transicao de camada.");
                        PlayMovementStartSfx(selectedUnit);
                        float transitionDuration = GetLayerOperationTransitionDuration();
                        if (transitionDuration > 0f)
                            yield return new WaitForSeconds(transitionDuration);

                        if (!TryApplyDomainTransitionOption(option, boardMap))
                        {
                            scannerPromptStep = ScannerPromptStep.LandingCycleOption;
                            LogLandingSelectionPanel();
                            yield break;
                        }

                        float postTransitionDelay = GetLayerOperationAfterTransitionDelay();
                        if (postTransitionDelay > 0f)
                            yield return new WaitForSeconds(postTransitionDelay);

                        cursorController?.PlayConfirmSfx();
                        bool domainTransitionFinished = TryFinalizeSelectedUnitActionFromDebug();
                        if (domainTransitionFinished)
                        {
                            cursorController?.PlayDoneSfx();
                            ResetScannerPromptState();
                            yield break;
                        }

                        ExitLandingStateToMovement(rollback: false);
                        RefreshSensorsForCurrentState();
                        break;
                    }

                    SensorMovementMode movementMode = ResolveLandingMovementMode();
                    AircraftOperationDecision decision = AircraftOperationRules.Evaluate(
                        selectedUnit,
                        boardMap,
                        terrainDatabase,
                        movementMode);
                    if (!decision.available || decision.action != AircraftOperationAction.Land)
                    {
                        string reason = !string.IsNullOrWhiteSpace(decision.reason) ? decision.reason : "Pouso indisponivel.";
                        Debug.Log($"[Landing] {reason}");
                        scannerPromptStep = ScannerPromptStep.LandingCycleOption;
                        LogLandingSelectionPanel();
                        yield break;
                    }

                    PlayMovementStartSfx(selectedUnit);
                    bool startAirHigh = selectedUnit.GetDomain() == Domain.Air && selectedUnit.GetHeightLevel() == HeightLevel.AirHigh;
                    bool startAirLow = selectedUnit.GetDomain() == Domain.Air && selectedUnit.GetHeightLevel() == HeightLevel.AirLow;

                    // Sequencia temporal igual ao padrao de embarque:
                    // AirHigh -> (tempo normalizado) -> AirLow -> (fim do tempo) -> Land/Surface.
                    float landingDuration;
                    if (startAirHigh)
                    {
                        float totalHighToGround = GetEmbarkAirHighToGroundDuration();
                        float highToLowAt = Mathf.Clamp(totalHighToGround * GetEmbarkHighToLowNormalizedTime(), 0f, totalHighToGround);
                        if (highToLowAt > 0f)
                            yield return new WaitForSeconds(highToLowAt);

                        // Fallback defensivo: se nao existir modo Air/Low, segue para Ground ao fim da janela.
                        selectedUnit.TrySetCurrentLayerMode(Domain.Air, HeightLevel.AirLow);
                        float remainingToGround = Mathf.Max(0f, totalHighToGround - highToLowAt);
                        if (remainingToGround > 0f)
                            yield return new WaitForSeconds(remainingToGround);

                        landingDuration = GetEmbarkAirLowToGroundDuration();
                    }
                    else if (startAirLow)
                    {
                        float totalLowToGround = GetEmbarkAirLowToGroundDuration();
                        float lowToGroundAt = Mathf.Clamp(totalLowToGround * GetEmbarkLowToGroundNormalizedTime(), 0f, totalLowToGround);
                        if (lowToGroundAt > 0f)
                            yield return new WaitForSeconds(lowToGroundAt);
                        landingDuration = Mathf.Max(0f, totalLowToGround - lowToGroundAt);
                    }
                    else
                    {
                        landingDuration = GetEmbarkForcedLandingDuration();
                    }

                    if (!selectedUnit.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface))
                    {
                        Debug.Log("[Landing] Falha ao aplicar pouso (Land/Surface).");
                        scannerPromptStep = ScannerPromptStep.LandingCycleOption;
                        LogLandingSelectionPanel();
                        yield break;
                    }

                    if (selectedUnit.HasSkillId("vtol"))
                    {
                        float vtolFxDuration = animationManager != null ? animationManager.PlayVtolLandingEffect(selectedUnit) : 0f;
                        landingDuration = Mathf.Max(landingDuration, vtolFxDuration);
                    }
                    if (landingDuration > 0f)
                        yield return new WaitForSeconds(landingDuration);

                    float postLandingDelay = GetEmbarkAfterForcedLandingDelay();
                    if (postLandingDelay > 0f)
                        yield return new WaitForSeconds(postLandingDelay);

                    cursorController?.PlayConfirmSfx();
                    bool finished = TryFinalizeSelectedUnitActionFromDebug();
                    if (finished)
                    {
                        cursorController?.PlayDoneSfx();
                        ResetScannerPromptState();
                        yield break;
                    }

                    SetCursorState(CursorState.UnitSelected, "ExecuteLandingOptionSequence: landing without action consume");
                    ClearSensorResults();
                    PaintSelectedUnitMovementRange();
                    if (cursorController != null && selectedUnit != null)
                    {
                        Vector3Int unitCell = selectedUnit.CurrentCellPosition;
                        unitCell.z = 0;
                        cursorController.SetCell(unitCell, playMoveSfx: false);
                    }
                    break;
                }
                case LandingOptionAction.DomainTransition:
                {
                    PlayMovementStartSfx(selectedUnit);
                    float transitionDuration = GetLayerOperationTransitionDuration();
                    if (transitionDuration > 0f)
                        yield return new WaitForSeconds(transitionDuration);

                    if (!TryApplyDomainTransitionOption(option, boardMap))
                    {
                        scannerPromptStep = ScannerPromptStep.LandingCycleOption;
                        LogLandingSelectionPanel();
                        yield break;
                    }

                    float postTransitionDelay = GetLayerOperationAfterTransitionDelay();
                    if (postTransitionDelay > 0f)
                        yield return new WaitForSeconds(postTransitionDelay);

                    cursorController?.PlayConfirmSfx();
                    bool finished = TryFinalizeSelectedUnitActionFromDebug();
                    if (finished)
                    {
                        cursorController?.PlayDoneSfx();
                        ResetScannerPromptState();
                        yield break;
                    }

                    ExitLandingStateToMovement(rollback: false);
                    RefreshSensorsForCurrentState();
                    break;
                }
            }

            if (cursorState == CursorState.Pousando)
            {
                BuildLandingOptionsFromCurrentState();
                scannerPromptStep = ScannerPromptStep.LandingCycleOption;
                LogLandingSelectionPanel();
            }
        }
        finally
        {
            landingExecutionInProgress = false;
        }
    }

    private void BuildLandingOptionsFromCurrentState()
    {
        cachedLandingOptions.Clear();
        landingOptionUnavailableReason = string.Empty;
        scannerSelectedLandingIndex = -1;
        if (selectedUnit == null)
            return;

        SensorMovementMode movementMode = ResolveLandingMovementMode();
        if (TryCollectLayerOperationOptions(selectedUnit, movementMode, cachedLandingOptions, out string reason))
        {
            scannerSelectedLandingIndex = 0;
            return;
        }

        landingOptionUnavailableReason = reason;
    }

    private bool TryCollectLayerOperationOptions(
        UnitManager unit,
        SensorMovementMode movementMode,
        List<LandingOption> output,
        out string unavailableReason)
    {
        unavailableReason = string.Empty;
        if (output == null)
            return false;

        output.Clear();
        if (unit == null)
            return false;

        if (ShouldBlockLayerOperationBecauseTakeoffIsRestricted(unit, out string takeoffRestrictionReason))
        {
            unavailableReason = takeoffRestrictionReason;
            return false;
        }

        IReadOnlyList<UnitLayerMode> modes = unit.GetAllLayerModes();
        if (modes == null || modes.Count <= 1)
        {
            unavailableReason = "Unidade sem camadas alternativas para trocar.";
            return false;
        }

        Domain currentDomain = unit.GetDomain();
        HeightLevel currentHeight = unit.GetHeightLevel();
        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : unit.BoardTilemap;
        Vector3Int unitCell = ResolveLayerOperationCell(unit, movementMode);
        for (int i = 0; i < modes.Count; i++)
        {
            UnitLayerMode mode = modes[i];
            if (mode.domain == currentDomain && mode.heightLevel == currentHeight)
                continue;

            if (!CanUseLayerModeAtCurrentCell(unit, boardMap, terrainDatabase, unitCell, mode.domain, mode.heightLevel, out string blockReason))
            {
                if (string.IsNullOrWhiteSpace(unavailableReason))
                    unavailableReason = blockReason;
                continue;
            }

            bool isAirToGroundLanding = ShouldUseLandingActionForTransition(currentDomain, currentHeight, mode.domain, mode.heightLevel);
            LandingOptionAction action = isAirToGroundLanding
                ? LandingOptionAction.Land
                : LandingOptionAction.DomainTransition;

            if (isAirToGroundLanding)
            {
                AircraftOperationDecision decision = AircraftOperationRules.Evaluate(
                    unit,
                    boardMap,
                    terrainDatabase,
                    movementMode);
                if (!decision.available || decision.action != AircraftOperationAction.Land)
                {
                    if (string.IsNullOrWhiteSpace(unavailableReason))
                        unavailableReason = !string.IsNullOrWhiteSpace(decision.reason)
                            ? decision.reason
                            : "Pouso indisponivel neste hex.";
                    continue;
                }
            }

            output.Add(new LandingOption(
                action,
                BuildLayerOperationLabel(currentDomain, currentHeight, mode.domain, mode.heightLevel),
                currentDomain,
                currentHeight,
                mode.domain,
                mode.heightLevel));
        }

        if (output.Count > 0)
            return true;

        unavailableReason = "Sem transicoes disponiveis para a camada atual.";
        return false;
    }

    private bool ShouldBlockLayerOperationBecauseTakeoffIsRestricted(UnitManager unit, out string reason)
    {
        reason = string.Empty;
        if (unit == null)
            return false;
        if (!hasTemporaryTakeoffSelectionState || temporaryTakeoffUnit != unit)
            return false;
        if (temporaryTakeoffMoveOptions == null || temporaryTakeoffMoveOptions.Count == 0)
            return false;

        bool hasFullTakeoff = temporaryTakeoffMoveOptions.Contains(9);
        if (hasFullTakeoff)
            return false;

        // Regra solicitada: quando Pode Decolar estiver em 0, 1 ou [0,1], L deve ficar indisponivel.
        bool onlyShortTakeoffOptions = true;
        for (int i = 0; i < temporaryTakeoffMoveOptions.Count; i++)
        {
            int option = temporaryTakeoffMoveOptions[i];
            if (option != 0 && option != 1)
            {
                onlyShortTakeoffOptions = false;
                break;
            }
        }

        if (!onlyShortTakeoffOptions)
            return false;

        reason = "L indisponivel: decolagem restrita (0/1).";
        return true;
    }

    private Vector3Int ResolveLayerOperationCell(UnitManager unit, SensorMovementMode movementMode)
    {
        if (unit == null)
            return Vector3Int.zero;

        if (movementMode == SensorMovementMode.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
        {
            Vector3Int committedCell = committedMovementPath[committedMovementPath.Count - 1];
            committedCell.z = 0;
            return committedCell;
        }

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        return cell;
    }

    private static bool CanUseLayerModeAtCurrentCell(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        Domain targetDomain,
        HeightLevel targetHeight,
        out string reason)
    {
        reason = string.Empty;
        if (unit == null || boardMap == null)
        {
            reason = "Contexto de mapa/unidade invalido.";
            return false;
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null)
        {
            if (!construction.SupportsLayerMode(targetDomain, targetHeight))
            {
                reason = $"Construcao no hex nao suporta {targetDomain}/{targetHeight}.";
                return false;
            }

            if (!UnitPassesSkillRequirement(unit, construction.GetRequiredSkillsToEnter()))
            {
                reason = "Unidade nao possui skill exigida pela construcao para trocar de camada.";
                return false;
            }

            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        if (structure != null)
        {
            if (!StructureSupportsLayerMode(structure, targetDomain, targetHeight))
            {
                reason = $"Estrutura no hex nao suporta {targetDomain}/{targetHeight}.";
                return false;
            }

            if (!UnitPassesSkillRequirement(unit, structure.requiredSkillsToEnter))
            {
                reason = "Unidade nao possui skill exigida pela estrutura para trocar de camada.";
                return false;
            }

            if (!TryResolveTerrainAtCell(boardMap, terrainDb, cell, out TerrainTypeData terrainWithStructure) || terrainWithStructure == null)
            {
                reason = "Terreno do hex nao encontrado para validar camada com estrutura.";
                return false;
            }

            if (!TerrainSupportsLayerMode(terrainWithStructure, targetDomain, targetHeight))
            {
                reason = $"Terreno no hex (com estrutura) nao suporta {targetDomain}/{targetHeight}.";
                return false;
            }

            if (!UnitPassesSkillRequirement(unit, terrainWithStructure.requiredSkillsToEnter))
            {
                reason = "Unidade nao possui skill exigida pelo terreno para trocar de camada.";
                return false;
            }

            return true;
        }

        if (!TryResolveTerrainAtCell(boardMap, terrainDb, cell, out TerrainTypeData terrain) || terrain == null)
        {
            reason = "Terreno do hex nao encontrado para validar camada.";
            return false;
        }

        if (!TerrainSupportsLayerMode(terrain, targetDomain, targetHeight))
        {
            reason = $"Terreno no hex nao suporta {targetDomain}/{targetHeight}.";
            return false;
        }

        if (!UnitPassesSkillRequirement(unit, terrain.requiredSkillsToEnter))
        {
            reason = "Unidade nao possui skill exigida pelo terreno para trocar de camada.";
            return false;
        }

        return true;
    }

    private static bool UnitPassesSkillRequirement(UnitManager unit, IReadOnlyList<SkillData> requiredSkills)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;
        if (unit == null)
            return false;

        for (int i = 0; i < requiredSkills.Count; i++)
        {
            SkillData requiredSkill = requiredSkills[i];
            if (requiredSkill == null)
                continue;

            if (unit.HasSkill(requiredSkill))
                return true;
        }

        return false;
    }

    private static bool TerrainSupportsLayerMode(TerrainTypeData terrain, Domain domain, HeightLevel heightLevel)
    {
        if (terrain == null)
            return false;

        if (terrain.domain == domain && terrain.heightLevel == heightLevel)
            return true;

        if (domain == Domain.Air && terrain.alwaysAllowAirDomain)
            return true;

        if (terrain.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < terrain.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrain.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool StructureSupportsLayerMode(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null)
            return false;

        if (structure.domain == domain && structure.heightLevel == heightLevel)
            return true;

        if (domain == Domain.Air && structure.alwaysAllowAirDomain)
            return true;

        if (structure.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < structure.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = structure.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool TryResolveTerrainAtCell(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDb == null)
            return false;

        cell.z = 0;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDb.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
        {
            terrain = byMainTile;
            return true;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase other = map.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDb.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }

    private bool TryApplyDomainTransitionOption(LandingOption option, Tilemap boardMap)
    {
        if (selectedUnit == null)
            return false;

        Domain beforeDomain = selectedUnit.GetDomain();
        HeightLevel beforeHeight = selectedUnit.GetHeightLevel();

        if (option.action == LandingOptionAction.Land &&
            selectedUnit.GetDomain() == Domain.Air &&
            option.toDomain == Domain.Land &&
            option.toHeightLevel == HeightLevel.Surface)
        {
            SensorMovementMode movementMode = ResolveLandingMovementMode();
            if (!AircraftOperationRules.TryApplyOperation(
                    selectedUnit,
                    boardMap,
                    terrainDatabase,
                    movementMode,
                    out AircraftOperationDecision decision))
            {
                string reason = !string.IsNullOrWhiteSpace(decision.reason)
                    ? decision.reason
                    : "Falha ao aplicar pouso.";
                Debug.Log($"[LayerOperation] {reason}");
                return false;
            }

            return true;
        }

        if (!selectedUnit.TrySetCurrentLayerMode(option.toDomain, option.toHeightLevel))
        {
            Debug.Log(
                $"[LayerOperation] Falha ao aplicar camada destino {option.toDomain}/{option.toHeightLevel} " +
                $"(atual={beforeDomain}/{beforeHeight}).");
            return false;
        }

        Debug.Log(
            $"[LayerOperation] Aplicado: {beforeDomain}/{beforeHeight} -> " +
            $"{selectedUnit.GetDomain()}/{selectedUnit.GetHeightLevel()}.");
        return true;
    }

    private static bool ShouldUseLandingActionForTransition(
        Domain fromDomain,
        HeightLevel fromHeightLevel,
        Domain toDomain,
        HeightLevel toHeightLevel)
    {
        return fromDomain == Domain.Air &&
               toDomain == Domain.Land &&
               toHeightLevel == HeightLevel.Surface;
    }

    private static string BuildLayerOperationLabel(
        Domain fromDomain,
        HeightLevel fromHeightLevel,
        Domain toDomain,
        HeightLevel toHeightLevel)
    {
        if (fromDomain == Domain.Air && fromHeightLevel == HeightLevel.AirHigh &&
            toDomain == Domain.Air && toHeightLevel == HeightLevel.AirLow)
            return "Descer para Air/Low";

        if (fromDomain == Domain.Air && fromHeightLevel == HeightLevel.AirLow &&
            toDomain == Domain.Air && toHeightLevel == HeightLevel.AirHigh)
            return "Subir para Air/High";

        if (fromDomain == Domain.Air && toDomain == Domain.Land && toHeightLevel == HeightLevel.Surface)
            return "Pousar";

        return $"Mudar para {toDomain}/{toHeightLevel}";
    }

    private bool TryConfirmScannerEmbark()
    {
        if (cursorState != CursorState.Embarcando)
            return false;

        if (scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget)
        {
            if (!TryGetSelectedValidEmbarkOption(out PodeEmbarcarOption selected, out int shownIndex))
            {
                Debug.Log("[Embarque] Selecao de embarque invalida.");
                return true;
            }

            scannerPromptStep = ScannerPromptStep.EmbarkConfirmTarget;
            // Mantem/atualiza a linha de preview durante a fase de confirmacao.
            FocusCurrentEmbarkTarget(logDetails: false, moveCursor: false);
            string label = !string.IsNullOrWhiteSpace(selected.displayLabel) ? selected.displayLabel : "transportador";
            Debug.Log($"Confirma embarque {shownIndex}? {label}\n(Enter=sim, ESC=voltar para ciclar)");
            return true;
        }

        if (scannerPromptStep != ScannerPromptStep.EmbarkConfirmTarget)
            return false;

        if (!TryGetSelectedValidEmbarkOption(out PodeEmbarcarOption option, out _))
        {
            scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
            FocusCurrentEmbarkTarget(logDetails: true);
            return true;
        }

        StartEmbarkExecutionFlow(option);
        return true;
    }

    private bool TryGetSelectedValidEmbarkOption(out PodeEmbarcarOption option, out int shownIndex)
    {
        option = null;
        shownIndex = scannerSelectedEmbarkIndex + 1;
        if (scannerSelectedEmbarkIndex < 0 || scannerSelectedEmbarkIndex >= cachedPodeEmbarcarTargets.Count)
            return false;

        option = cachedPodeEmbarcarTargets[scannerSelectedEmbarkIndex];
        return option != null;
    }

    private void StartEmbarkExecutionFlow(PodeEmbarcarOption option)
    {
        if (option == null)
        {
            Debug.Log("[Embarque] Opcao invalida.");
            scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
            return;
        }

        UnitManager passenger = option.sourceUnit != null ? option.sourceUnit : selectedUnit;
        UnitManager transporter = option.transporterUnit;
        if (passenger == null || transporter == null || passenger != selectedUnit)
        {
            Debug.Log("[Embarque] Opcao desatualizada para a unidade selecionada.");
            scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
            ExitEmbarkStateToMovement();
            RefreshSensorsForCurrentState();
            return;
        }

        embarkExecutionInProgress = true;
        scannerPromptStep = ScannerPromptStep.EmbarkConfirmTarget;
        StartCoroutine(ExecuteEmbarkSequence(option, passenger, transporter));
    }

    private System.Collections.IEnumerator ExecuteEmbarkSequence(PodeEmbarcarOption option, UnitManager passenger, UnitManager transporter)
    {
        Tilemap movementTilemap = terrainTilemap != null ? terrainTilemap : (passenger != null ? passenger.BoardTilemap : null);
        bool passengerSortingRaised = false;
        bool transporterSortingRaised = false;

        if (passenger != null)
        {
            passenger.SetTemporarySortingOrder();
            passengerSortingRaised = true;
        }

        try
        {
            if (transporter != null && transporter.GetDomain() == Domain.Air)
            {
                transporter.SetTemporarySortingOrder();
                transporterSortingRaised = true;
                AircraftOperationDecision landingDecision = AircraftOperationRules.Evaluate(
                    transporter,
                    movementTilemap,
                    terrainDatabase,
                    SensorMovementMode.MoveuParado);
                if (!landingDecision.available || landingDecision.action != AircraftOperationAction.Land)
                {
                    Debug.Log(string.IsNullOrWhiteSpace(landingDecision.reason)
                        ? "[Embarque] Transportador aereo sem pouso valido."
                        : $"[Embarque] {landingDecision.reason}");
                    embarkExecutionInProgress = false;
                    scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
                    ExitEmbarkStateToMovement();
                    RefreshSensorsForCurrentState();
                    yield break;
                }

                // Feedback do "forced landing": usa o SFX de movimento da unidade que pousou.
                PlayMovementStartSfx(transporter);
                Debug.Log("[Embarque] Transportador pousou antes do embarque.");

                bool transporterStartHigh = transporter.GetDomain() == Domain.Air && transporter.GetHeightLevel() == HeightLevel.AirHigh;
                bool transporterStartLow = transporter.GetDomain() == Domain.Air && transporter.GetHeightLevel() == HeightLevel.AirLow;

                if (transporterStartHigh)
                {
                    float highToLowDuration = GetEmbarkAirHighToGroundDuration() * Mathf.Clamp01(GetEmbarkHighToLowNormalizedTime());
                    if (highToLowDuration > 0f)
                        yield return new WaitForSeconds(highToLowDuration);
                    transporter.TrySetCurrentLayerMode(Domain.Air, HeightLevel.AirLow);
                    transporterStartLow = transporter.GetDomain() == Domain.Air && transporter.GetHeightLevel() == HeightLevel.AirLow;
                }

                float landingDuration = transporterStartLow
                    ? GetEmbarkAirLowToGroundDuration()
                    : GetEmbarkForcedLandingDuration();
                if (!transporter.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface))
                {
                    Debug.Log("[Embarque] Falha ao concluir pouso do transportador (Land/Surface).");
                    embarkExecutionInProgress = false;
                    scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
                    ExitEmbarkStateToMovement();
                    RefreshSensorsForCurrentState();
                    yield break;
                }

                if (transporter.HasSkillId("vtol"))
                {
                    float vtolFxDuration = animationManager != null ? animationManager.PlayVtolLandingEffect(transporter) : 0f;
                    landingDuration = Mathf.Max(landingDuration, vtolFxDuration);
                }
                if (landingDuration > 0f)
                    yield return new WaitForSeconds(landingDuration);

                float postLandingDelay = GetEmbarkAfterForcedLandingDelay();
                if (postLandingDelay > 0f)
                    yield return new WaitForSeconds(postLandingDelay);
            }

            Vector3Int fromCell = passenger != null ? passenger.CurrentCellPosition : Vector3Int.zero;
            fromCell.z = 0;
            Vector3Int toCell = transporter != null ? transporter.CurrentCellPosition : Vector3Int.zero;
            toCell.z = 0;
            bool requiresMovement = fromCell != toCell;
            Domain startDomain = passenger != null ? passenger.GetDomain() : Domain.Land;
            HeightLevel startHeight = passenger != null ? passenger.GetHeightLevel() : HeightLevel.Surface;
            bool startAirHigh = startDomain == Domain.Air && startHeight == HeightLevel.AirHigh;
            bool startAirLow = startDomain == Domain.Air && startHeight == HeightLevel.AirLow;
            ClearEmbarkPreview();

            if (requiresMovement && animationManager != null && passenger != null)
            {
                bool movementFinished = false;
                List<Vector3Int> path = new List<Vector3Int>(2) { fromCell, toCell };
                float selectedStepDuration = startAirHigh
                    ? GetEmbarkAirHighToGroundDuration()
                    : (startAirLow ? GetEmbarkAirLowToGroundDuration() : GetEmbarkDefaultMoveStepDuration());
                float effectiveStepDuration = GetEffectiveEmbarkMoveStepDuration(passenger, selectedStepDuration);

                animationManager.PlayMovement(
                    passenger,
                    movementTilemap,
                    path,
                    playStartSfx: true,
                    onAnimationStart: () => PlayMovementStartSfx(passenger),
                    onAnimationFinished: () => movementFinished = true,
                    onCellReached: null,
                    stepDurationOverride: selectedStepDuration);

                if (startAirHigh)
                {
                    bool lowApplied = false;
                    bool groundApplied = false;
                    float elapsed = 0f;
                    float highToLowAt = Mathf.Clamp(effectiveStepDuration * animationManager.EmbarkHighToLowNormalizedTime, 0f, effectiveStepDuration);
                    float lowToGroundAt = effectiveStepDuration;

                    while (true)
                    {
                        elapsed += Time.deltaTime;
                        if (!lowApplied && elapsed >= highToLowAt)
                        {
                            lowApplied = passenger.TrySetCurrentLayerMode(Domain.Air, HeightLevel.AirLow) || !passenger.SupportsLayerMode(Domain.Air, HeightLevel.AirLow);
                        }

                        if (!groundApplied && elapsed >= lowToGroundAt)
                            groundApplied = passenger.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);

                        if (movementFinished && lowApplied && groundApplied)
                            break;

                        yield return null;
                    }

                    if (!groundApplied)
                        passenger.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);
                }
                else if (startAirLow)
                {
                    bool groundApplied = false;
                    float elapsed = 0f;
                    float lowToGroundAt = Mathf.Clamp(effectiveStepDuration * animationManager.EmbarkLowToGroundNormalizedTime, 0f, effectiveStepDuration);

                    while (true)
                    {
                        elapsed += Time.deltaTime;
                        if (!groundApplied && elapsed >= lowToGroundAt)
                            groundApplied = passenger.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);

                        if (movementFinished && groundApplied)
                            break;

                        yield return null;
                    }

                    if (!groundApplied)
                        passenger.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);
                }
                else
                {
                    while (!movementFinished)
                        yield return null;
                }
            }
            else if (requiresMovement && passenger != null)
            {
                passenger.SetCurrentCellPosition(toCell, enforceFinalOccupancyRule: false);
                float fallbackDuration = startAirHigh
                    ? GetEmbarkAirHighToGroundDuration()
                    : (startAirLow ? GetEmbarkAirLowToGroundDuration() : GetEmbarkDefaultMoveStepDuration());
                if (fallbackDuration > 0f)
                    yield return new WaitForSeconds(fallbackDuration);
                if (startAirHigh || startAirLow)
                    passenger.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);
            }

            int embarkCost = ResolveEmbarkAutonomyCost(option, passenger, transporter);
            int fuelBeforeEmbark = passenger != null ? passenger.CurrentFuel : 0;
            int fuelAfterEmbark = Mathf.Max(0, fuelBeforeEmbark - embarkCost);
            if (passenger != null && fuelAfterEmbark != fuelBeforeEmbark)
                passenger.SetCurrentFuel(fuelAfterEmbark);

            float postEmbarkDelay = GetEmbarkAfterMoveDelay();
            if (postEmbarkDelay > 0f)
                yield return new WaitForSeconds(postEmbarkDelay);

            if (!TryExecuteEmbarkOptionNow(option, embarkCost, fuelBeforeEmbark, out string resultMessage))
            {
                if (passenger != null && passenger.CurrentFuel != fuelBeforeEmbark)
                    passenger.SetCurrentFuel(fuelBeforeEmbark);
                Debug.Log($"Pode Embarcar (\"E\"): {resultMessage}");
                embarkExecutionInProgress = false;
                scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
                ExitEmbarkStateToMovement();
                RefreshSensorsForCurrentState();
                yield break;
            }

            cursorController?.PlayLoadSfx();
            Debug.Log(resultMessage);
            embarkExecutionInProgress = false;
            ResetScannerPromptState();
        }
        finally
        {
            if (passengerSortingRaised && passenger != null)
                passenger.ClearTemporarySortingOrder();
            if (transporterSortingRaised && transporter != null)
                transporter.ClearTemporarySortingOrder();
        }
    }

    private float GetEmbarkForcedLandingDuration()
    {
        return animationManager != null ? animationManager.EmbarkForcedLandingDuration : 0.25f;
    }

    private float GetEmbarkAfterForcedLandingDelay()
    {
        return animationManager != null ? animationManager.EmbarkAfterForcedLandingDelay : 0.10f;
    }

    private float GetEmbarkDefaultMoveStepDuration()
    {
        return animationManager != null ? animationManager.EmbarkDefaultMoveStepDuration : 0.12f;
    }

    private float GetEmbarkAfterMoveDelay()
    {
        return animationManager != null ? animationManager.EmbarkAfterMoveDelay : 0.15f;
    }

    private float GetEmbarkAirHighToGroundDuration()
    {
        return animationManager != null ? animationManager.EmbarkAirHighToGroundDuration : 0.10f;
    }

    private float GetEmbarkAirLowToGroundDuration()
    {
        return animationManager != null ? animationManager.EmbarkAirLowToGroundDuration : 0.05f;
    }

    private float GetLayerOperationTransitionDuration()
    {
        return GetEmbarkDefaultMoveStepDuration();
    }

    private float GetLayerOperationAfterTransitionDelay()
    {
        return GetEmbarkAfterMoveDelay();
    }

    private float GetEmbarkHighToLowNormalizedTime()
    {
        return animationManager != null ? animationManager.EmbarkHighToLowNormalizedTime : 0.50f;
    }

    private float GetEmbarkLowToGroundNormalizedTime()
    {
        return animationManager != null ? animationManager.EmbarkLowToGroundNormalizedTime : 1.00f;
    }

    private float GetEffectiveEmbarkMoveStepDuration(UnitManager passenger, float stepDuration)
    {
        if (animationManager != null)
            return animationManager.GetEffectiveMoveStepDuration(passenger, stepDuration);

        return Mathf.Max(0.04f, stepDuration);
    }

    private int ResolveEmbarkAutonomyCost(PodeEmbarcarOption option, UnitManager passenger, UnitManager transporter)
    {
        int embarkCost = option != null ? Mathf.Max(0, option.enterCost) : 0;
        if (passenger == null || transporter == null)
            return embarkCost;

        Tilemap costTilemap = terrainTilemap != null ? terrainTilemap : passenger.BoardTilemap;
        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;
        if (costTilemap != null && UnitMovementPathRules.TryGetEnterCellCost(
                costTilemap,
                passenger,
                transporterCell,
                terrainDatabase,
                applyOperationalAutonomyModifier: false,
                out int resolvedCost))
        {
            embarkCost = Mathf.Max(0, resolvedCost);
        }

        return embarkCost;
    }

    private bool TryExecuteEmbarkOptionNow(PodeEmbarcarOption option, int embarkCost, int fuelBeforeEmbark, out string message)
    {
        message = "Falha ao executar embarque.";
        if (option == null)
        {
            message = "Opcao de embarque invalida.";
            return false;
        }

        UnitManager passenger = option.sourceUnit != null ? option.sourceUnit : selectedUnit;
        UnitManager transporter = option.transporterUnit;
        if (passenger == null || transporter == null || passenger != selectedUnit)
        {
            message = "Dados de passageiro/transportador invalidos.";
            return false;
        }

        if (!transporter.TryEmbarkPassengerInSlot(passenger, option.transporterSlotIndex, out string embarkReason))
        {
            message = string.IsNullOrWhiteSpace(embarkReason) ? "Transportador sem vaga disponivel." : embarkReason;
            return false;
        }

        bool finished = TryFinalizeSelectedUnitActionFromDebug();
        if (!finished)
        {
            message = "Embarque executado, mas nao foi possivel finalizar a acao da unidade.";
            return false;
        }

        transporter.MarkAsActed();

        string label = !string.IsNullOrWhiteSpace(option.displayLabel) ? option.displayLabel : transporter.name;
        message = $"Embarque concluido em: {label} | custo={embarkCost} | autonomia {fuelBeforeEmbark}->{passenger.CurrentFuel}";
        return true;
    }

    private int GetEmbarkEntryCount()
    {
        return cachedPodeEmbarcarTargets.Count;
    }

    private void LogEmbarkSelectionPanel()
    {
        int total = GetEmbarkEntryCount();
        if (total <= 0)
        {
            Debug.Log("Sem opcoes de embarque para listar.");
            return;
        }

        string text = $"Transportadores validos para embarque: {total}\n";
        text += "Digite 1..9 para selecionar opcao\n";
        for (int i = 0; i < cachedPodeEmbarcarTargets.Count; i++)
        {
            PodeEmbarcarOption option = cachedPodeEmbarcarTargets[i];
            if (option == null)
                continue;

            string label = !string.IsNullOrWhiteSpace(option.displayLabel) ? option.displayLabel : "transportador";
            text += $"{i + 1}. [OK] {label}\n";
        }

        if (cachedPodeEmbarcarInvalidTargets.Count > 0)
            text += $"Invalidos detectados pelo sensor (nao selecionaveis em gameplay): {cachedPodeEmbarcarInvalidTargets.Count}\n";

        text += ">> Enter confirma opcao valida | ESC volta";
        Debug.Log(text);
    }

    private void FocusCurrentEmbarkTarget(bool logDetails, bool moveCursor = true)
    {
        int total = GetEmbarkEntryCount();
        if (total <= 0)
        {
            ClearEmbarkPreview();
            return;
        }

        if (scannerSelectedEmbarkIndex < 0 || scannerSelectedEmbarkIndex >= total)
            scannerSelectedEmbarkIndex = 0;

        if (scannerSelectedEmbarkIndex < 0 || scannerSelectedEmbarkIndex >= cachedPodeEmbarcarTargets.Count)
        {
            ClearEmbarkPreview();
            return;
        }

        PodeEmbarcarOption option = cachedPodeEmbarcarTargets[scannerSelectedEmbarkIndex];
        if (moveCursor && cursorController != null && option != null && option.transporterUnit != null)
        {
            Vector3Int targetCell = option.transporterUnit.CurrentCellPosition;
            targetCell.z = 0;
            cursorController.SetCell(targetCell, playMoveSfx: false);
        }
        DrawEmbarkPreviewForValid(option);
        if (logDetails)
            LogCurrentEmbarkSelection(option, null, scannerSelectedEmbarkIndex + 1, total, isValid: true);
    }

    private void LogCurrentEmbarkSelection(
        PodeEmbarcarOption validOption,
        PodeEmbarcarInvalidOption invalidOption,
        int shownIndex,
        int total,
        bool isValid)
    {
        if (isValid)
        {
            string label = validOption != null && !string.IsNullOrWhiteSpace(validOption.displayLabel)
                ? validOption.displayLabel
                : "transportador";
            int cost = validOption != null ? Mathf.Max(0, validOption.enterCost) : 0;
            Debug.Log(
                $"[Embarque] Opcao {shownIndex}/{total} [VALIDA]\n" +
                $"{label}\n" +
                $"Linha: VERDE\n" +
                $"Custo de autonomia: {cost}\n" +
                "Botao Embarcar: habilitado\n" +
                "Enter confirma. ESC volta.");
            return;
        }

        string transporter = invalidOption != null && invalidOption.transporterUnit != null
            ? invalidOption.transporterUnit.name
            : (invalidOption != null ? $"hex {invalidOption.evaluatedCell.x},{invalidOption.evaluatedCell.y}" : "invalido");
        string reason = invalidOption != null && !string.IsNullOrWhiteSpace(invalidOption.reason)
            ? invalidOption.reason
            : "motivo nao informado";
        Debug.Log(
            $"[Embarque] Opcao {shownIndex}/{total} [INVALIDA]\n" +
            $"{transporter}\n" +
            $"Motivo: {reason}\n" +
            "Linha: VERMELHA\n" +
            "Botao Embarcar: desabilitado");
    }

    private void DrawEmbarkPreviewForValid(PodeEmbarcarOption option)
    {
        if (option == null || selectedUnit == null)
        {
            ClearEmbarkPreview();
            return;
        }

        UnitManager transporter = option.transporterUnit;
        if (transporter == null)
        {
            ClearEmbarkPreview();
            return;
        }

        Vector3 from = selectedUnit.transform.position;
        Vector3 to = transporter.transform.position;
        from.z = to.z;
        Color color = GetMirandoPreviewColor();
        RebuildEmbarkPreviewPath(from, to, color);
    }

    private void DrawEmbarkPreviewForInvalid(PodeEmbarcarInvalidOption invalid)
    {
        if (invalid == null || selectedUnit == null)
        {
            ClearEmbarkPreview();
            return;
        }

        Vector3 from = selectedUnit.transform.position;
        Vector3 to;
        if (invalid.transporterUnit != null)
            to = invalid.transporterUnit.transform.position;
        else
        {
            Tilemap map = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
            Vector3Int cell = invalid.evaluatedCell;
            cell.z = 0;
            to = map != null ? map.GetCellCenterWorld(cell) : from;
        }

        from.z = to.z;
        RebuildEmbarkPreviewPath(from, to, new Color(1f, 0.2f, 0.2f, 0.95f));
    }

    private void LogTargetSelectionPanel()
    {
        if (cachedPodeMirarTargets.Count == 0)
        {
            Debug.Log("Sem alvos validos para mirar.");
            return;
        }

        string text = $"Alvos validos retornados pelo sensor: {cachedPodeMirarTargets.Count}\n";
        text += "Mirando: setas alternam entre alvos validos\n";
        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[i];
            string label = option != null && !string.IsNullOrWhiteSpace(option.displayLabel)
                ? option.displayLabel
                : (option != null && option.targetUnit != null ? option.targetUnit.name : "alvo");

            string revide = option != null && option.defenderCanCounterAttack ? "sim" : "nao";
            text += $"{i + 1}. {label} | revide: {revide}\n";
        }

        text += ">> Enter confirma | ESC volta";
        Debug.Log(text);
    }

    private void LogAttackConfirmationPrompt(PodeMirarTargetOption option, int shownIndex)
    {
        string label = option != null && !string.IsNullOrWhiteSpace(option.displayLabel)
            ? option.displayLabel
            : (option != null && option.targetUnit != null ? option.targetUnit.name : $"alvo {shownIndex}");

        Debug.Log($"Confirma alvo {shownIndex}? {label}\n(Enter=sim, ESC=voltar para ciclar)");
    }

    private void MoveCursorToTarget(PodeMirarTargetOption option)
    {
        if (option == null || option.targetUnit == null || cursorController == null)
            return;

        Vector3Int targetCell = option.targetUnit.CurrentCellPosition;
        targetCell.z = 0;
        cursorController.SetCell(targetCell, playMoveSfx: false);
    }

    private bool TryConfirmScannerAttack()
    {
        if (cursorState != CursorState.Mirando)
            return false;
        if (combatExecutionInProgress)
            return true;

        if (scannerPromptStep == ScannerPromptStep.MirandoCycleTarget)
        {
            if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
                return true;

            EnterMirandoConfirmStep();
            return true;
        }

        if (scannerPromptStep != ScannerPromptStep.MirandoConfirmTarget)
            return false;

        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
        {
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            scannerSelectedTargetIndex = 0;
            FocusCurrentMirandoTarget(logDetails: true);
            LogTargetSelectionPanel();
            return true;
        }

        PodeMirarTargetOption option = cachedPodeMirarTargets[scannerSelectedTargetIndex];
        if (option == null || option.attackerUnit == null || option.targetUnit == null)
        {
            Debug.Log("Falha ao confirmar ataque: opcao invalida.");
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            scannerSelectedTargetIndex = 0;
            FocusCurrentMirandoTarget(logDetails: true);
            LogTargetSelectionPanel();
            return true;
        }

        CombatResolutionResult combat = ResolveCombatFromSelectedOption(option);
        Debug.Log(combat.trace);
        if (!combat.success)
        {
            Debug.Log("[Combate] Falha ao resolver combate. Retornando para selecao de alvo.");
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            FocusCurrentMirandoTarget(logDetails: true);
            LogTargetSelectionPanel();
            return true;
        }

        WeaponTrajectoryType trajectory = ResolveSelectedTrajectory(option);
        StartCoroutine(ExecuteConfirmedAttackSequence(option, trajectory, combat));
        return true;
    }

    private IEnumerator ExecuteConfirmedAttackSequence(
        PodeMirarTargetOption option,
        WeaponTrajectoryType attackerTrajectory,
        CombatResolutionResult combat)
    {
        combatExecutionInProgress = true;
        UnitManager attacker = option != null ? option.attackerUnit : null;
        UnitManager defender = option != null ? option.targetUnit : null;

        float audioDuration = PlayCombatAttackSfx(attackerTrajectory, defender);
        float waitDuration = audioDuration;

        if (attackerTrajectory == WeaponTrajectoryType.Parabolic && animationManager != null && defender != null)
        {
            float effectDuration = animationManager.PlayRangedAttackDefenderEffect(defender, audioDuration);
            waitDuration = Mathf.Max(waitDuration, effectDuration);
        }

        if (waitDuration > 0f)
            yield return new WaitForSeconds(waitDuration);

        yield return ExecuteCombatProjectileExchange(option, attackerTrajectory, combat.counterExecuted);
        ApplyPendingCombatHp(combat);
        yield return ExecuteDeathResolutionIfNeeded(combat);

        combatExecutionInProgress = false;
        cursorController?.PlayDoneSfx();
        if (!TryFinalizeSelectedUnitActionFromDebug())
            ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: true);
        ResetScannerPromptState();
    }

    private IEnumerator ExecuteCombatProjectileExchange(
        PodeMirarTargetOption option,
        WeaponTrajectoryType attackerTrajectory,
        bool counterExecuted)
    {
        if (option == null)
            yield break;

        UnitManager attacker = option.attackerUnit;
        UnitManager defender = option.targetUnit;
        if (attacker == null || defender == null)
            yield break;

        WeaponTrajectoryType counterTrajectory = counterExecuted
            ? ResolveTrajectoryForShot(defender, option.defenderCounterEmbarkedWeaponIndex, option.defenderCounterWeapon)
            : WeaponTrajectoryType.Straight;

        bool canBump =
            counterExecuted &&
            attackerTrajectory == WeaponTrajectoryType.Straight &&
            counterTrajectory == WeaponTrajectoryType.Straight;
        if (canBump && animationManager != null)
        {
            float bumpDuration = animationManager.PlayCombatBumpTogether(attacker, defender);
            if (bumpDuration > 0f)
                yield return new WaitForSeconds(bumpDuration);
        }
        else if (attackerTrajectory == WeaponTrajectoryType.Straight && animationManager != null)
        {
            float bumpDuration = animationManager.PlayCombatBumpTowards(attacker, defender);
            if (bumpDuration > 0f)
                yield return new WaitForSeconds(bumpDuration);
        }

        float attackerShotDuration = PlayWeaponShot(attacker, defender, option.weapon, attackerTrajectory);
        if (attackerShotDuration > 0f)
            yield return new WaitForSeconds(attackerShotDuration);
        float defenderHitFxDuration = animationManager != null ? animationManager.PlayTakingHitEffect(defender) : 0f;
        if (defenderHitFxDuration > 0f)
            yield return new WaitForSeconds(defenderHitFxDuration);

        if (!counterExecuted)
            yield break;

        float counterDelay = animationManager != null ? animationManager.CombatCounterShotDelay : 0.1f;
        if (counterDelay > 0f)
            yield return new WaitForSeconds(counterDelay);

        if (!canBump && counterTrajectory == WeaponTrajectoryType.Straight && animationManager != null)
        {
            float counterBumpDuration = animationManager.PlayCombatBumpTowards(defender, attacker);
            if (counterBumpDuration > 0f)
                yield return new WaitForSeconds(counterBumpDuration);
        }

        float counterShotDuration = PlayWeaponShot(defender, attacker, option.defenderCounterWeapon, counterTrajectory);
        if (counterShotDuration > 0f)
            yield return new WaitForSeconds(counterShotDuration);
        float attackerHitFxDuration = animationManager != null ? animationManager.PlayTakingHitEffect(attacker) : 0f;
        if (attackerHitFxDuration > 0f)
            yield return new WaitForSeconds(attackerHitFxDuration);
    }

    private float PlayCombatAttackSfx(WeaponTrajectoryType trajectory, UnitManager defender)
    {
        AudioClip clip = trajectory == WeaponTrajectoryType.Parabolic ? rangedAttackSfx : meleeAttackSfx;
        if (clip == null)
            return 0f;

        Vector3 worldPos = selectedUnit != null
            ? selectedUnit.transform.position
            : (defender != null ? defender.transform.position : Vector3.zero);
        AudioSource.PlayClipAtPoint(clip, worldPos, Mathf.Clamp01(combatSfxVolume));
        return clip.length;
    }

    private float PlayWeaponShot(UnitManager shooter, UnitManager target, WeaponData weapon, WeaponTrajectoryType trajectory)
    {
        if (shooter == null || target == null)
            return 0f;

        if (weapon != null && weapon.fireSfx != null)
            AudioSource.PlayClipAtPoint(weapon.fireSfx, shooter.transform.position, Mathf.Clamp01(weapon.fireSfxVolume));

        if (animationManager == null)
            return 0f;
        return animationManager.PlayWeaponProjectile(shooter, target, weapon, trajectory);
    }

    private WeaponTrajectoryType ResolveTrajectoryForShot(UnitManager owner, int embarkedWeaponIndex, WeaponData fallbackWeapon)
    {
        if (owner != null)
        {
            IReadOnlyList<UnitEmbarkedWeapon> weapons = owner.GetEmbarkedWeapons();
            if (weapons != null && embarkedWeaponIndex >= 0 && embarkedWeaponIndex < weapons.Count)
            {
                UnitEmbarkedWeapon embarked = weapons[embarkedWeaponIndex];
                if (embarked != null)
                    return embarked.selectedTrajectory;
            }
        }

        if (fallbackWeapon != null && fallbackWeapon.SupportsTrajectory(WeaponTrajectoryType.Parabolic))
            return WeaponTrajectoryType.Parabolic;

        return WeaponTrajectoryType.Straight;
    }

    private void ApplyPendingCombatHp(CombatResolutionResult combat)
    {
        if (!combat.success)
            return;

        if (combat.defenderUnit != null)
        {
            int defenderHpBefore = Mathf.Max(0, combat.defenderUnit.CurrentHP);
            int defenderHpAfter = Mathf.Max(0, combat.defenderHpAfter);
            combat.defenderUnit.SetCurrentHP(defenderHpAfter);
            ApplyEmbarkedCascadeFromDirectHit(combat.defenderUnit, defenderHpBefore, defenderHpAfter);
        }

        if (combat.attackerUnit != null)
        {
            int attackerHpBefore = Mathf.Max(0, combat.attackerUnit.CurrentHP);
            int attackerHpAfter = Mathf.Max(0, combat.attackerHpAfter);
            combat.attackerUnit.SetCurrentHP(attackerHpAfter);
            ApplyEmbarkedCascadeFromDirectHit(combat.attackerUnit, attackerHpBefore, attackerHpAfter);
        }
    }

    private void ApplyEmbarkedCascadeFromDirectHit(UnitManager directlyHitUnit, int hpBefore, int hpAfter)
    {
        if (directlyHitUnit == null)
            return;

        hpBefore = Mathf.Max(0, hpBefore);
        hpAfter = Mathf.Clamp(hpAfter, 0, hpBefore);
        if (hpBefore <= 0)
            return;

        if (hpAfter <= 0)
        {
            // Combatente direto morto: ele nao deve sumir aqui.
            // Somente embarcados (e sub-embarcados) somem sem animacao individual.
            KillEmbarkedChildrenChain(directlyHitUnit);
            return;
        }

        int damageTaken = hpBefore - hpAfter;
        if (damageTaken <= 0)
            return;

        float ratio = Mathf.Clamp01((float)damageTaken / hpBefore);
        ApplyRatioDamageToEmbarkedRecursive(directlyHitUnit, ratio);
    }

    private void ApplyRatioDamageToEmbarkedRecursive(UnitManager transporter, float ratio)
    {
        if (transporter == null || ratio <= 0f)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null || seats.Count == 0)
            return;

        HashSet<UnitManager> processed = new HashSet<UnitManager>();
        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            UnitManager child = seat != null ? seat.embarkedUnit : null;
            if (child == null || !processed.Add(child))
                continue;

            int childBefore = Mathf.Max(0, child.CurrentHP);
            if (childBefore <= 0)
            {
                KillEntireEmbarkedChain(child);
                continue;
            }

            int propagatedDamage = Mathf.RoundToInt(childBefore * ratio);
            if (propagatedDamage <= 0)
                propagatedDamage = 1;

            int childAfter = Mathf.Max(0, childBefore - propagatedDamage);
            child.SetCurrentHP(childAfter);

            if (childAfter <= 0)
            {
                KillEntireEmbarkedChain(child);
                continue;
            }

            ApplyRatioDamageToEmbarkedRecursive(child, ratio);
        }
    }

    private void KillEntireEmbarkedChain(UnitManager root, bool detachSelf = true)
    {
        if (root == null)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = root.TransportedUnitSlots;
        if (seats != null && seats.Count > 0)
        {
            List<UnitManager> children = new List<UnitManager>(seats.Count);
            HashSet<UnitManager> unique = new HashSet<UnitManager>();
            for (int i = 0; i < seats.Count; i++)
            {
                UnitTransportSeatRuntime seat = seats[i];
                UnitManager child = seat != null ? seat.embarkedUnit : null;
                if (child == null || !unique.Add(child))
                    continue;
                children.Add(child);
            }

            for (int i = 0; i < children.Count; i++)
                KillEntireEmbarkedChain(children[i], detachSelf: true);
        }

        root.SetCurrentHP(0);

        if (detachSelf && root.EmbarkedTransporter != null)
            root.EmbarkedTransporter.RemoveEmbarkedPassenger(root);

        if (root.IsEmbarked)
            root.SetEmbarked(false);

        root.gameObject.SetActive(false);
    }

    private void KillEmbarkedChildrenChain(UnitManager transporter)
    {
        if (transporter == null)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null || seats.Count == 0)
            return;

        List<UnitManager> children = new List<UnitManager>(seats.Count);
        HashSet<UnitManager> unique = new HashSet<UnitManager>();
        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            UnitManager child = seat != null ? seat.embarkedUnit : null;
            if (child == null || !unique.Add(child))
                continue;
            children.Add(child);
        }

        for (int i = 0; i < children.Count; i++)
            KillEntireEmbarkedChain(children[i], detachSelf: true);
    }

    private IEnumerator ExecuteDeathResolutionIfNeeded(CombatResolutionResult combat)
    {
        List<DeathTarget> deaths = BuildDeathTargets(combat);
        if (deaths.Count == 0)
            yield break;

        for (int i = 0; i < deaths.Count; i++)
        {
            DeathTarget target = deaths[i];
            UnitManager unit = target.unit;
            if (unit == null)
                continue;

            float deathStartDelay = animationManager != null ? animationManager.CombatDeathStartDelay : 0f;
            if (deathStartDelay > 0f)
                yield return new WaitForSeconds(deathStartDelay);

            if (cursorController != null)
            {
                Vector3Int cell = target.cell;
                cell.z = 0;
                cursorController.SetCell(cell, playMoveSfx: true);
            }

            SpriteRenderer[] renderers = CollectDeathBlinkRenderers(unit);
            if (renderers != null && renderers.Length > 0)
                yield return CoBlinkRenderersFast(renderers);

            if (renderers != null)
            {
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] != null)
                        renderers[r].enabled = false;
                }
            }

            // A unidade deve desaparecer antes da explosao.
            if (unit != null)
                unit.gameObject.SetActive(false);

            float explosionDuration = animationManager != null
                ? animationManager.PlayExplosionEffectAt(target.worldPos)
                : 0f;
            if (explosionDuration > 0f)
                yield return new WaitForSeconds(explosionDuration);
            else
                yield return new WaitForSeconds(0.12f);

            yield return new WaitForSeconds(0.05f);
        }
    }

    private static List<DeathTarget> BuildDeathTargets(CombatResolutionResult combat)
    {
        List<DeathTarget> list = new List<DeathTarget>(2);

        if (combat.attackerUnit != null && combat.attackerHpAfter <= 0 && combat.attackerUnit.gameObject.activeInHierarchy)
        {
            Vector3Int cell = combat.attackerUnit.CurrentCellPosition;
            cell.z = 0;
            list.Add(new DeathTarget(combat.attackerUnit, cell, combat.attackerUnit.transform.position));
        }

        if (combat.defenderUnit != null && combat.defenderHpAfter <= 0 && combat.defenderUnit.gameObject.activeInHierarchy)
        {
            Vector3Int cell = combat.defenderUnit.CurrentCellPosition;
            cell.z = 0;
            list.Add(new DeathTarget(combat.defenderUnit, cell, combat.defenderUnit.transform.position));
        }

        return list;
    }

    private static IEnumerator CoBlinkRenderersFast(SpriteRenderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
            yield break;

        float interval = 0.12f;
        const float minInterval = 0.03f;
        const int blinks = 10;
        bool visible = true;

        for (int i = 0; i < blinks; i++)
        {
            visible = !visible;
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] != null)
                    renderers[r].enabled = visible;
            }

            yield return new WaitForSecondsRealtime(interval);
            interval = Mathf.Max(minInterval, interval * 0.80f);
        }
    }

    private static SpriteRenderer[] CollectDeathBlinkRenderers(UnitManager unit)
    {
        if (unit == null)
            return null;

        SpriteRenderer main = unit.GetMainSpriteRenderer();
        if (main != null)
            return new[] { main };

        return unit.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void EnterMirandoState()
    {
        if (cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado)
            cursorStateBeforeMirando = cursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;

        // Ao sair do fluxo de movimento para mirar, oculta o rastro legado do caminho comprometido.
        ClearCommittedPathVisual();

        SetCursorState(CursorState.Mirando, "EnterMirandoState");
        scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
        scannerSelectedTargetIndex = 0;

        if (cachedPodeMirarTargets.Count <= 1)
        {
            if (cachedPodeMirarTargets.Count == 1)
                FocusCurrentMirandoTarget(logDetails: true);
            EnterMirandoConfirmStep();
            return;
        }

        LogTargetSelectionPanel();
        FocusCurrentMirandoTarget(logDetails: true);
    }

    private void EnterMirandoConfirmStep()
    {
        if (cachedPodeMirarTargets.Count <= 0)
            return;

        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
            scannerSelectedTargetIndex = 0;

        scannerPromptStep = ScannerPromptStep.MirandoConfirmTarget;
        SetMirandoPreviewVisible(false);
        SetMirandoSpotterPreviewsVisible(false);
        PodeMirarTargetOption picked = cachedPodeMirarTargets[scannerSelectedTargetIndex];
        LogAttackConfirmationPrompt(picked, scannerSelectedTargetIndex + 1);
    }

    private void FocusCurrentMirandoTarget(bool logDetails, bool moveCursor = true)
    {
        if (cachedPodeMirarTargets.Count == 0)
        {
            SetMirandoPreviewVisible(false);
            SetMirandoSpotterPreviewsVisible(false);
            return;
        }

        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
            scannerSelectedTargetIndex = 0;

        PodeMirarTargetOption option = cachedPodeMirarTargets[scannerSelectedTargetIndex];
        if (moveCursor)
            MoveCursorToTarget(option);
        RebuildMirandoPreviewPath(option);
        SetMirandoPreviewVisible(cursorState == CursorState.Mirando);
        SetMirandoSpotterPreviewsVisible(cursorState == CursorState.Mirando);
        if (logDetails)
            LogCurrentMirandoTarget(option, scannerSelectedTargetIndex + 1, cachedPodeMirarTargets.Count);
    }

    private void LogCurrentMirandoTarget(PodeMirarTargetOption option, int shownIndex, int total)
    {
        if (option == null || option.targetUnit == null)
            return;

        UnitManager target = option.targetUnit;
        string attackWeapon = option.weapon != null ? option.weapon.displayName : "arma";
        string counterText = option.defenderCanCounterAttack ? "sim" : $"nao ({option.defenderCounterReason})";
        string label = !string.IsNullOrWhiteSpace(option.displayLabel) ? option.displayLabel : target.name;
        string evPathText = FormatEvPath(option.lineOfFireEvPath);
        string lineHexesText = FormatHexPath(option.lineOfFireIntermediateCells);

        Debug.Log(
            $"[Mirando] Alvo {shownIndex}/{total}\n" +
            $"Label: {label}\n" +
            $"Unidade: {target.name}\n" +
            $"Distancia: {option.distance}\n" +
            $"HP: {target.CurrentHP}\n" +
            $"Arma atacante: {attackWeapon}\n" +
            $"Posicao atacante: {option.attackerPositionLabel}\n" +
            $"Posicao defensor: {option.defenderPositionLabel}\n" +
            $"EV path: {evPathText}\n" +
            $"Linha (hex intermediario): {lineHexesText}\n" +
            $"Revide: {counterText}\n" +
            "Use setas para trocar alvo. Enter confirma. ESC volta.");
    }

    private static string FormatHexPath(IReadOnlyList<Vector3Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return "(sem intermediarios)";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append('(');
        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            Vector3Int c = cells[i];
            sb.Append(c.x).Append('/').Append(c.y);
        }
        sb.Append(')');
        return sb.ToString();
    }

    private static string FormatEvPath(IReadOnlyList<float> evPath)
    {
        if (evPath == null || evPath.Count == 0)
            return "(n/a)";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append('(');
        for (int i = 0; i < evPath.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(evPath[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        }
        sb.Append(')');
        return sb.ToString();
    }

    private static int GetMirandoStepFromInput(Vector3Int inputDelta)
    {
        if (inputDelta.x > 0 || inputDelta.y < 0)
            return 1;
        if (inputDelta.x < 0 || inputDelta.y > 0)
            return -1;
        return 0;
    }

    private bool TryResolveMirandoCursorMove(Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = cursorController != null ? cursorController.CurrentCell : Vector3Int.zero;
        if (cursorState != CursorState.Mirando || cachedPodeMirarTargets.Count == 0)
            return false;
        if (scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        int count = cachedPodeMirarTargets.Count;
        if (count <= 1)
            return false;

        scannerSelectedTargetIndex = (scannerSelectedTargetIndex + step + count) % count;
        FocusCurrentMirandoTarget(logDetails: true);

        if (scannerSelectedTargetIndex >= 0 && scannerSelectedTargetIndex < count)
        {
            UnitManager target = cachedPodeMirarTargets[scannerSelectedTargetIndex].targetUnit;
            if (target != null)
            {
                resolvedCell = target.CurrentCellPosition;
                resolvedCell.z = 0;
                return true;
            }
        }

        return false;
    }

    private bool TryResolveEmbarkCursorMove(Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = cursorController != null ? cursorController.CurrentCell : Vector3Int.zero;
        if (cursorState != CursorState.Embarcando)
            return false;
        if (scannerPromptStep != ScannerPromptStep.EmbarkCycleTarget)
            return false;
        if (cachedPodeEmbarcarTargets.Count == 0)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        int count = cachedPodeEmbarcarTargets.Count;
        if (count <= 1)
            return false;

        scannerSelectedEmbarkIndex = (scannerSelectedEmbarkIndex + step + count) % count;
        FocusCurrentEmbarkTarget(logDetails: true);

        if (scannerSelectedEmbarkIndex < 0 || scannerSelectedEmbarkIndex >= count)
            return false;

        PodeEmbarcarOption option = cachedPodeEmbarcarTargets[scannerSelectedEmbarkIndex];
        if (option == null || option.transporterUnit == null)
            return false;

        resolvedCell = option.transporterUnit.CurrentCellPosition;
        resolvedCell.z = 0;
        return true;
    }

    private bool TryResolveLandingCursorMove(Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = cursorController != null ? cursorController.CurrentCell : Vector3Int.zero;
        if (cursorState != CursorState.Pousando)
            return false;
        if (scannerPromptStep != ScannerPromptStep.LandingCycleOption)
            return false;
        if (cachedLandingOptions.Count <= 1)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        int count = cachedLandingOptions.Count;
        scannerSelectedLandingIndex = (scannerSelectedLandingIndex + step + count) % count;
        LogLandingSelectionPanel();
        return true;
    }

    private bool IsEmbarkPromptActive()
    {
        return cursorState == CursorState.Embarcando &&
               (scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget ||
                scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget);
    }

    private bool IsLandingPromptActive()
    {
        return cursorState == CursorState.Pousando &&
               (scannerPromptStep == ScannerPromptStep.LandingCycleOption ||
                scannerPromptStep == ScannerPromptStep.LandingConfirmOption);
    }

    private SensorMovementMode ResolveLandingMovementMode()
    {
        if (cursorStateBeforePousando == CursorState.MoveuAndando)
            return SensorMovementMode.MoveuAndando;

        return SensorMovementMode.MoveuParado;
    }

    private void ExitLandingStateToMovement(bool rollback = true)
    {
        if (cursorState != CursorState.Pousando)
            return;

        CursorState targetMovementState = cursorStateBeforePousando == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(targetMovementState, "ExitLandingStateToMovement", rollback: rollback);
        if (targetMovementState == CursorState.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
            DrawCommittedPathVisual(committedMovementPath);
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }

        scannerPromptStep = ScannerPromptStep.AwaitingAction;
        scannerSelectedLandingIndex = -1;
        cachedLandingOptions.Clear();
        LogScannerPanel();
    }

    private void ExitEmbarkStateToMovement()
    {
        if (cursorState != CursorState.Embarcando)
            return;

        CursorState targetMovementState = cursorStateBeforeEmbarcando == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(targetMovementState, "ExitEmbarkStateToMovement", rollback: true);
        if (targetMovementState == CursorState.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
            DrawCommittedPathVisual(committedMovementPath);
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }

        scannerPromptStep = ScannerPromptStep.AwaitingAction;
        scannerSelectedEmbarkIndex = -1;
        ClearEmbarkPreview();
        LogScannerPanel();
    }

    private void ExitMirandoStateToMovement()
    {
        if (cursorState != CursorState.Mirando)
            return;

        CursorState targetMovementState = cursorStateBeforeMirando == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(targetMovementState, "ExitMirandoStateToMovement", rollback: true);
        if (targetMovementState == CursorState.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
            DrawCommittedPathVisual(committedMovementPath);
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }
        ResetScannerPromptState();
        LogScannerPanel();
    }

    private void UpdateMirandoPreviewAnimation()
    {
        bool isMirandoCycle =
            cursorState == CursorState.Mirando &&
            scannerPromptStep == ScannerPromptStep.MirandoCycleTarget;

        if (isMirandoCycle)
            TryRefreshMirandoPreviewPathIfNeeded();

        bool shouldShow =
            isMirandoCycle &&
            mirandoPreviewPathLength > 0.0001f &&
            mirandoPreviewPathPoints.Count >= 2;

        if (!shouldShow)
        {
            SetMirandoPreviewVisible(false);
            SetMirandoSpotterPreviewsVisible(false);
            return;
        }

        int segmentQuantities = Mathf.Max(1, GetMirandoPreviewSegmentQuantities());
        EnsureMirandoPreviewRenderers(segmentQuantities);
        if (mirandoPreviewRenderers.Count <= 0)
            return;

        float speed = GetMirandoPreviewSpeed();
        float segmentLen = GetMirandoPreviewSegmentLength();
        float cycleLen = mirandoPreviewPathLength + segmentLen;
        mirandoPreviewHeadDistance += speed * Time.deltaTime;
        if (mirandoPreviewHeadDistance > cycleLen)
            mirandoPreviewHeadDistance = 0f;

        float startDist = Mathf.Max(0f, mirandoPreviewHeadDistance - segmentLen);
        float endDist = Mathf.Min(mirandoPreviewHeadDistance, mirandoPreviewPathLength);
        if (endDist <= startDist + 0.0001f)
        {
            SetMirandoPreviewVisible(false);
            return;
        }

        BuildPathSegmentPoints(startDist, endDist, mirandoPreviewSegmentPoints);
        if (mirandoPreviewSegmentPoints.Count < 2)
        {
            SetMirandoPreviewVisible(false);
            return;
        }

        SetMirandoPreviewVisible(true);
        float previewWidth = GetMirandoPreviewWidth();
        Color previewColor = GetMirandoPreviewColor();
        float spacing = cycleLen / segmentQuantities;
        for (int segmentIndex = 0; segmentIndex < segmentQuantities; segmentIndex++)
        {
            LineRenderer renderer = mirandoPreviewRenderers[segmentIndex];
            if (renderer == null)
                continue;

            float segmentHeadDistance = mirandoPreviewHeadDistance - (spacing * segmentIndex);
            while (segmentHeadDistance < 0f)
                segmentHeadDistance += cycleLen;
            while (segmentHeadDistance > cycleLen)
                segmentHeadDistance -= cycleLen;

            float segmentStartDist = Mathf.Max(0f, segmentHeadDistance - segmentLen);
            float segmentEndDist = Mathf.Min(segmentHeadDistance, mirandoPreviewPathLength);
            if (segmentEndDist <= segmentStartDist + 0.0001f)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            BuildPathSegmentPoints(segmentStartDist, segmentEndDist, mirandoPreviewSegmentPoints);
            if (mirandoPreviewSegmentPoints.Count < 2)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            renderer.startWidth = previewWidth;
            renderer.endWidth = previewWidth;
            renderer.startColor = previewColor;
            renderer.endColor = previewColor;
            renderer.positionCount = mirandoPreviewSegmentPoints.Count;
            for (int i = 0; i < mirandoPreviewSegmentPoints.Count; i++)
                renderer.SetPosition(i, mirandoPreviewSegmentPoints[i]);
            renderer.enabled = true;
        }

        UpdateMirandoSpotterPreviewAnimation();
    }

    private void RebuildMirandoPreviewPath(PodeMirarTargetOption option)
    {
        mirandoPreviewPathPoints.Clear();
        mirandoPreviewPathLength = 0f;
        mirandoPreviewHeadDistance = 0f;
        mirandoPreviewSignatureValid = false;

        if (option == null || option.attackerUnit == null || option.targetUnit == null)
        {
            SetMirandoPreviewVisible(false);
            RebuildMirandoSpotterPreviewPaths(null);
            return;
        }

        Vector3 attackerPos = option.attackerUnit.transform.position;
        Vector3 targetPos = option.targetUnit.transform.position;
        attackerPos.z = targetPos.z;

        WeaponTrajectoryType trajectory = ResolveSelectedTrajectory(option);
        CacheMirandoPreviewSignature(attackerPos, targetPos, trajectory);
        if (trajectory == WeaponTrajectoryType.Parabolic)
            BuildParabolicPath(attackerPos, targetPos, mirandoPreviewPathPoints);
        else
        {
            mirandoPreviewPathPoints.Add(attackerPos);
            mirandoPreviewPathPoints.Add(targetPos);
        }

        mirandoPreviewPathLength = ComputePathLength(mirandoPreviewPathPoints);
        RebuildMirandoSpotterPreviewPaths(option);
        if (mirandoPreviewPathLength <= 0.0001f)
            SetMirandoPreviewVisible(false);
    }

    private void TryRefreshMirandoPreviewPathIfNeeded()
    {
        PodeMirarTargetOption option = GetCurrentMirandoOption();
        if (option == null || option.attackerUnit == null || option.targetUnit == null)
            return;

        Vector3 from = option.attackerUnit.transform.position;
        Vector3 to = option.targetUnit.transform.position;
        from.z = to.z;
        WeaponTrajectoryType trajectory = ResolveSelectedTrajectory(option);
        float bend = GetMirandoParabolaBend();
        int samples = GetMirandoParabolaSamples();

        bool changed =
            !mirandoPreviewSignatureValid ||
            from != mirandoPreviewLastFrom ||
            to != mirandoPreviewLastTo ||
            trajectory != mirandoPreviewLastTrajectory ||
            !Mathf.Approximately(bend, mirandoPreviewLastBend) ||
            samples != mirandoPreviewLastSamples;

        if (!changed)
            return;

        RebuildMirandoPreviewPath(option);
    }

    private PodeMirarTargetOption GetCurrentMirandoOption()
    {
        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
            return null;

        return cachedPodeMirarTargets[scannerSelectedTargetIndex];
    }

    private void CacheMirandoPreviewSignature(Vector3 from, Vector3 to, WeaponTrajectoryType trajectory)
    {
        mirandoPreviewSignatureValid = true;
        mirandoPreviewLastFrom = from;
        mirandoPreviewLastTo = to;
        mirandoPreviewLastTrajectory = trajectory;
        mirandoPreviewLastBend = GetMirandoParabolaBend();
        mirandoPreviewLastSamples = GetMirandoParabolaSamples();
    }

    private WeaponTrajectoryType ResolveSelectedTrajectory(PodeMirarTargetOption option)
    {
        if (option == null || option.attackerUnit == null)
            return WeaponTrajectoryType.Straight;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = option.attackerUnit.GetEmbarkedWeapons();
        if (weapons != null && option.embarkedWeaponIndex >= 0 && option.embarkedWeaponIndex < weapons.Count)
        {
            UnitEmbarkedWeapon embarked = weapons[option.embarkedWeaponIndex];
            if (embarked != null)
                return embarked.selectedTrajectory;
        }

        if (option.weapon != null && option.weapon.SupportsTrajectory(WeaponTrajectoryType.Parabolic))
            return WeaponTrajectoryType.Parabolic;

        return WeaponTrajectoryType.Straight;
    }

    private void BuildParabolicPath(Vector3 from, Vector3 to, List<Vector3> output)
    {
        output.Clear();
        Vector2 flat = new Vector2(to.x - from.x, to.y - from.y);
        if (flat.sqrMagnitude <= 0.0001f)
        {
            output.Add(from);
            output.Add(to);
            return;
        }

        Vector2 dir = flat.normalized;
        Vector2 clockwiseNormal = new Vector2(dir.y, -dir.x);
        Vector2 antiClockwiseNormal = new Vector2(-dir.y, dir.x);
        bool targetIsRight = to.x >= from.x;
        Vector2 normal = targetIsRight ? antiClockwiseNormal : clockwiseNormal;

        float distance = flat.magnitude;
        float bend = Mathf.Clamp(GetMirandoParabolaBend(), 0.2f, Mathf.Max(0.2f, distance));
        Vector3 control = (from + to) * 0.5f + new Vector3(normal.x, normal.y, 0f) * bend;

        int samples = GetMirandoParabolaSamples();
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            output.Add(QuadraticBezier(from, control, to, t));
        }
    }

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }

    private static float ComputePathLength(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
            return 0f;

        float length = 0f;
        for (int i = 1; i < points.Count; i++)
            length += Vector3.Distance(points[i - 1], points[i]);
        return length;
    }

    private void BuildPathSegmentPoints(float startDist, float endDist, List<Vector3> output)
    {
        output.Clear();
        if (mirandoPreviewPathPoints.Count < 2)
            return;

        float accumulated = 0f;
        bool addedFirst = false;
        for (int i = 1; i < mirandoPreviewPathPoints.Count; i++)
        {
            Vector3 a = mirandoPreviewPathPoints[i - 1];
            Vector3 b = mirandoPreviewPathPoints[i];
            float segmentLen = Vector3.Distance(a, b);
            if (segmentLen <= 0.0001f)
                continue;

            float segStart = accumulated;
            float segEnd = accumulated + segmentLen;
            if (segEnd < startDist)
            {
                accumulated = segEnd;
                continue;
            }

            if (segStart > endDist)
                break;

            float localStart = Mathf.Clamp01((startDist - segStart) / segmentLen);
            float localEnd = Mathf.Clamp01((endDist - segStart) / segmentLen);
            if (!addedFirst)
            {
                output.Add(Vector3.Lerp(a, b, localStart));
                addedFirst = true;
            }

            output.Add(Vector3.Lerp(a, b, localEnd));
            accumulated = segEnd;
            if (segEnd >= endDist)
                break;
        }
    }

    private void RebuildMirandoSpotterPreviewPaths(PodeMirarTargetOption option)
    {
        ClearMirandoSpotterPreviewData();
        if (option == null || option.targetUnit == null || option.forwardObserverCandidates == null || option.forwardObserverCandidates.Count == 0)
            return;

        HashSet<UnitManager> uniqueObservers = new HashSet<UnitManager>();
        for (int i = 0; i < option.forwardObserverCandidates.Count; i++)
        {
            UnitManager observer = option.forwardObserverCandidates[i];
            if (observer == null || !observer.gameObject.activeInHierarchy)
                continue;
            if (!uniqueObservers.Add(observer))
                continue;

            Vector3 observerPos = observer.transform.position;
            Vector3 targetPos = option.targetUnit.transform.position;
            observerPos.z = targetPos.z;
            if (Vector3.Distance(observerPos, targetPos) <= 0.0001f)
                continue;

            MirandoSpotterPreviewTrack track = EnsureMirandoSpotterPreviewTrack(mirandoSpotterPreviewTracks.Count);
            if (track == null)
                continue;

            track.pathPoints.Clear();
            track.tempSegmentPoints.Clear();
            track.pathPoints.Add(observerPos);
            track.pathPoints.Add(targetPos);
            track.pathLength = ComputePathLength(track.pathPoints);
            track.headDistance = 0f;
        }

        if (mirandoSpotterPreviewTracks.Count > 0)
            SetMirandoSpotterPreviewsVisible(cursorState == CursorState.Mirando);
    }

    private void UpdateMirandoSpotterPreviewAnimation()
    {
        if (mirandoSpotterPreviewTracks.Count == 0)
            return;

        int segmentQuantities = Mathf.Max(1, GetMirandoSpotterSegmentQuantities());
        float spotterMultiplier = GetMirandoSpotterPreviewMultiplier();
        float speed = Mathf.Max(0.2f, GetMirandoSpotterSegmentSpeed());
        float segmentLen = Mathf.Max(0.08f, GetMirandoPreviewSegmentLength() * spotterMultiplier);
        float width = Mathf.Max(0.02f, GetMirandoPreviewWidth() * spotterMultiplier);
        Color baseColor = GetMirandoPreviewColor();
        Color spotterColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(baseColor.a * 0.75f));

        for (int i = 0; i < mirandoSpotterPreviewTracks.Count; i++)
        {
            MirandoSpotterPreviewTrack track = mirandoSpotterPreviewTracks[i];
            if (track == null || track.pathLength <= 0.0001f || track.pathPoints.Count < 2)
            {
                HideMirandoSpotterTrackRenderers(track);
                continue;
            }

            EnsureMirandoSpotterPreviewRenderers(track, i, segmentQuantities);
            if (track.renderers.Count == 0)
                continue;

            float cycleLen = track.pathLength + segmentLen;
            track.headDistance += speed * Time.deltaTime;
            if (track.headDistance > cycleLen)
                track.headDistance = 0f;

            float spacing = cycleLen / segmentQuantities;
            for (int segmentIndex = 0; segmentIndex < segmentQuantities; segmentIndex++)
            {
                if (segmentIndex >= track.renderers.Count)
                    break;

                LineRenderer renderer = track.renderers[segmentIndex];
                if (renderer == null)
                    continue;

                float segmentHeadDistance = track.headDistance - (spacing * segmentIndex);
                while (segmentHeadDistance < 0f)
                    segmentHeadDistance += cycleLen;
                while (segmentHeadDistance > cycleLen)
                    segmentHeadDistance -= cycleLen;

                float startDist = Mathf.Max(0f, segmentHeadDistance - segmentLen);
                float endDist = Mathf.Min(segmentHeadDistance, track.pathLength);
                if (endDist <= startDist + 0.0001f)
                {
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                    continue;
                }

                BuildPathSegmentPointsFrom(track.pathPoints, startDist, endDist, track.tempSegmentPoints);
                if (track.tempSegmentPoints.Count < 2)
                {
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                    continue;
                }

                renderer.startWidth = width;
                renderer.endWidth = width;
                renderer.startColor = spotterColor;
                renderer.endColor = spotterColor;
                renderer.positionCount = track.tempSegmentPoints.Count;
                for (int p = 0; p < track.tempSegmentPoints.Count; p++)
                    renderer.SetPosition(p, track.tempSegmentPoints[p]);
                renderer.enabled = true;
            }

            for (int extra = segmentQuantities; extra < track.renderers.Count; extra++)
            {
                LineRenderer extraRenderer = track.renderers[extra];
                if (extraRenderer == null)
                    continue;
                extraRenderer.positionCount = 0;
                extraRenderer.enabled = false;
            }
        }
    }

    private void BuildPathSegmentPointsFrom(List<Vector3> pathPoints, float startDist, float endDist, List<Vector3> output)
    {
        output.Clear();
        if (pathPoints == null || pathPoints.Count < 2)
            return;

        float accumulated = 0f;
        bool addedFirst = false;
        for (int i = 1; i < pathPoints.Count; i++)
        {
            Vector3 a = pathPoints[i - 1];
            Vector3 b = pathPoints[i];
            float segmentLen = Vector3.Distance(a, b);
            if (segmentLen <= 0.0001f)
                continue;

            float segStart = accumulated;
            float segEnd = accumulated + segmentLen;
            if (segEnd < startDist)
            {
                accumulated = segEnd;
                continue;
            }

            if (segStart > endDist)
                break;

            float localStart = Mathf.Clamp01((startDist - segStart) / segmentLen);
            float localEnd = Mathf.Clamp01((endDist - segStart) / segmentLen);
            if (!addedFirst)
            {
                output.Add(Vector3.Lerp(a, b, localStart));
                addedFirst = true;
            }

            output.Add(Vector3.Lerp(a, b, localEnd));
            accumulated = segEnd;
            if (segEnd >= endDist)
                break;
        }
    }

    private void EnsureMirandoPreviewRenderers(int count)
    {
        int desired = Mathf.Max(1, count);
        while (mirandoPreviewRenderers.Count < desired)
        {
            LineRenderer renderer = CreateMirandoPreviewRenderer(mirandoPreviewRenderers.Count);
            mirandoPreviewRenderers.Add(renderer);
        }
    }

    private LineRenderer CreateMirandoPreviewRenderer(int index)
    {
        string rendererName = index <= 0 ? "MirandoPreviewLine" : $"MirandoPreviewLine_{index + 1}";
        GameObject go = new GameObject(rendererName);
        go.transform.SetParent(transform, false);
        LineRenderer renderer = go.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.numCapVertices = 2;
        renderer.numCornerVertices = 2;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Material previewMaterial = GetMirandoPreviewMaterial();
        renderer.material = previewMaterial != null ? previewMaterial : new Material(Shader.Find("Sprites/Default"));
        int sortingLayerId = GetMirandoPreviewSortingLayerId();
        if (sortingLayerId != 0)
            renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = GetMirandoPreviewSortingOrder();
        renderer.enabled = false;
        return renderer;
    }

    private MirandoSpotterPreviewTrack EnsureMirandoSpotterPreviewTrack(int index)
    {
        while (mirandoSpotterPreviewTracks.Count <= index)
        {
            MirandoSpotterPreviewTrack track = new MirandoSpotterPreviewTrack();
            mirandoSpotterPreviewTracks.Add(track);
        }

        return mirandoSpotterPreviewTracks[index];
    }

    private void EnsureMirandoSpotterPreviewRenderers(MirandoSpotterPreviewTrack track, int trackIndex, int count)
    {
        if (track == null)
            return;

        int desired = Mathf.Max(1, count);
        while (track.renderers.Count < desired)
        {
            LineRenderer renderer = CreateMirandoSpotterPreviewRenderer(trackIndex, track.renderers.Count);
            track.renderers.Add(renderer);
        }
    }

    private LineRenderer CreateMirandoSpotterPreviewRenderer(int trackIndex, int segmentIndex)
    {
        string rendererName = $"MirandoSpotterPreviewLine_{trackIndex + 1}_{segmentIndex + 1}";
        GameObject go = new GameObject(rendererName);
        go.transform.SetParent(transform, false);
        LineRenderer renderer = go.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.numCapVertices = 2;
        renderer.numCornerVertices = 2;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Material previewMaterial = GetMirandoPreviewMaterial();
        renderer.material = previewMaterial != null ? previewMaterial : new Material(Shader.Find("Sprites/Default"));
        int sortingLayerId = GetMirandoPreviewSortingLayerId();
        if (sortingLayerId != 0)
            renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = Mathf.Max(0, GetMirandoPreviewSortingOrder() - 1);
        renderer.enabled = false;
        return renderer;
    }

    private void HideMirandoSpotterTrackRenderers(MirandoSpotterPreviewTrack track)
    {
        if (track == null || track.renderers == null)
            return;

        for (int i = 0; i < track.renderers.Count; i++)
        {
            LineRenderer renderer = track.renderers[i];
            if (renderer == null)
                continue;
            renderer.positionCount = 0;
            renderer.enabled = false;
        }
    }

    private void EnsureEmbarkPreviewRenderer()
    {
        if (embarkPreviewRenderer != null)
            return;

        GameObject go = new GameObject("EmbarkPreviewLine");
        go.transform.SetParent(transform, false);
        embarkPreviewRenderer = go.AddComponent<LineRenderer>();
        embarkPreviewRenderer.useWorldSpace = true;
        embarkPreviewRenderer.textureMode = LineTextureMode.Stretch;
        embarkPreviewRenderer.numCapVertices = 2;
        embarkPreviewRenderer.numCornerVertices = 2;
        embarkPreviewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        embarkPreviewRenderer.receiveShadows = false;
        Material previewMaterial = GetMirandoPreviewMaterial();
        embarkPreviewRenderer.material = previewMaterial != null ? previewMaterial : new Material(Shader.Find("Sprites/Default"));
        int sortingLayerId = GetMirandoPreviewSortingLayerId();
        if (sortingLayerId != 0)
            embarkPreviewRenderer.sortingLayerID = sortingLayerId;
        embarkPreviewRenderer.sortingOrder = GetMirandoPreviewSortingOrder();
        embarkPreviewRenderer.enabled = false;
    }

    private void RebuildEmbarkPreviewPath(Vector3 from, Vector3 to, Color color)
    {
        embarkPreviewPathPoints.Clear();
        embarkPreviewSegmentPoints.Clear();
        embarkPreviewPathLength = 0f;
        embarkPreviewHeadDistance = 0f;

        from.z = to.z;
        embarkPreviewPathPoints.Add(from);
        embarkPreviewPathPoints.Add(to);
        embarkPreviewPathLength = ComputePathLength(embarkPreviewPathPoints);
        embarkPreviewColor = color;
    }

    private void UpdateEmbarkPreviewAnimation()
    {
        bool shouldShow =
            cursorState == CursorState.Embarcando &&
            (scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget || scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget) &&
            embarkPreviewPathLength > 0.0001f &&
            embarkPreviewPathPoints.Count >= 2;
        if (!shouldShow)
        {
            SetEmbarkPreviewVisible(false);
            return;
        }

        EnsureEmbarkPreviewRenderer();
        if (embarkPreviewRenderer == null)
            return;

        float speed = GetMirandoPreviewSpeed();
        float segmentLen = GetMirandoPreviewSegmentLength();
        float cycleLen = embarkPreviewPathLength + segmentLen;
        embarkPreviewHeadDistance += speed * Time.deltaTime;
        if (embarkPreviewHeadDistance > cycleLen)
            embarkPreviewHeadDistance = 0f;

        float startDist = Mathf.Max(0f, embarkPreviewHeadDistance - segmentLen);
        float endDist = Mathf.Min(embarkPreviewHeadDistance, embarkPreviewPathLength);
        if (endDist <= startDist + 0.0001f)
        {
            SetEmbarkPreviewVisible(false);
            return;
        }

        BuildEmbarkPathSegmentPoints(startDist, endDist, embarkPreviewSegmentPoints);
        if (embarkPreviewSegmentPoints.Count < 2)
        {
            SetEmbarkPreviewVisible(false);
            return;
        }

        float width = GetMirandoPreviewWidth();
        embarkPreviewRenderer.startWidth = width;
        embarkPreviewRenderer.endWidth = width;
        embarkPreviewRenderer.startColor = embarkPreviewColor;
        embarkPreviewRenderer.endColor = embarkPreviewColor;
        embarkPreviewRenderer.positionCount = embarkPreviewSegmentPoints.Count;
        for (int i = 0; i < embarkPreviewSegmentPoints.Count; i++)
            embarkPreviewRenderer.SetPosition(i, embarkPreviewSegmentPoints[i]);
        SetEmbarkPreviewVisible(true);
    }

    private void BuildEmbarkPathSegmentPoints(float startDist, float endDist, List<Vector3> output)
    {
        output.Clear();
        if (embarkPreviewPathPoints.Count < 2)
            return;

        float accumulated = 0f;
        bool addedFirst = false;
        for (int i = 1; i < embarkPreviewPathPoints.Count; i++)
        {
            Vector3 a = embarkPreviewPathPoints[i - 1];
            Vector3 b = embarkPreviewPathPoints[i];
            float segmentLen = Vector3.Distance(a, b);
            if (segmentLen <= 0.0001f)
                continue;

            float segStart = accumulated;
            float segEnd = accumulated + segmentLen;
            if (segEnd < startDist)
            {
                accumulated = segEnd;
                continue;
            }

            if (segStart > endDist)
                break;

            float localStart = Mathf.Clamp01((startDist - segStart) / segmentLen);
            float localEnd = Mathf.Clamp01((endDist - segStart) / segmentLen);
            if (!addedFirst)
            {
                output.Add(Vector3.Lerp(a, b, localStart));
                addedFirst = true;
            }

            output.Add(Vector3.Lerp(a, b, localEnd));
            accumulated = segEnd;
            if (segEnd >= endDist)
                break;
        }
    }

    private void ClearEmbarkPreview()
    {
        embarkPreviewPathPoints.Clear();
        embarkPreviewSegmentPoints.Clear();
        embarkPreviewPathLength = 0f;
        embarkPreviewHeadDistance = 0f;
        SetEmbarkPreviewVisible(false);
    }

    private void SetEmbarkPreviewVisible(bool visible)
    {
        if (embarkPreviewRenderer == null)
            return;

        if (!visible)
        {
            embarkPreviewRenderer.positionCount = 0;
            embarkPreviewRenderer.enabled = false;
            return;
        }

        embarkPreviewRenderer.enabled = true;
    }

    private void SetMirandoPreviewVisible(bool visible)
    {
        for (int i = 0; i < mirandoPreviewRenderers.Count; i++)
        {
            LineRenderer renderer = mirandoPreviewRenderers[i];
            if (renderer == null)
                continue;

            if (!visible)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            renderer.enabled = true;
        }
    }

    private void SetMirandoSpotterPreviewsVisible(bool visible)
    {
        for (int i = 0; i < mirandoSpotterPreviewTracks.Count; i++)
        {
            MirandoSpotterPreviewTrack track = mirandoSpotterPreviewTracks[i];
            if (track == null || track.renderers == null || track.renderers.Count == 0)
                continue;

            for (int r = 0; r < track.renderers.Count; r++)
            {
                LineRenderer renderer = track.renderers[r];
                if (renderer == null)
                    continue;

                if (!visible)
                {
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                    continue;
                }

                if (track.pathLength > 0.0001f && track.pathPoints.Count >= 2)
                    renderer.enabled = true;
            }
        }
    }

    private void ClearMirandoSpotterPreviewData()
    {
        for (int i = 0; i < mirandoSpotterPreviewTracks.Count; i++)
        {
            MirandoSpotterPreviewTrack track = mirandoSpotterPreviewTracks[i];
            if (track == null)
                continue;

            track.pathPoints.Clear();
            track.tempSegmentPoints.Clear();
            track.pathLength = 0f;
            track.headDistance = 0f;
            if (track.renderers != null)
            {
                for (int r = 0; r < track.renderers.Count; r++)
                {
                    LineRenderer renderer = track.renderers[r];
                    if (renderer == null)
                        continue;
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                }
            }
        }
    }

    private void ClearMirandoPreview()
    {
        mirandoPreviewPathPoints.Clear();
        mirandoPreviewSegmentPoints.Clear();
        mirandoPreviewPathLength = 0f;
        mirandoPreviewHeadDistance = 0f;
        mirandoPreviewSignatureValid = false;
        SetMirandoPreviewVisible(false);
        ClearMirandoSpotterPreviewData();
        SetMirandoSpotterPreviewsVisible(false);
    }

    private Material GetMirandoPreviewMaterial()
    {
        return animationManager != null ? animationManager.MirandoPreviewMaterial : null;
    }

    private Color GetMirandoPreviewColor()
    {
        Color fallback = animationManager != null ? animationManager.MirandoPreviewColor : new Color(1f, 0.65f, 0.2f, 0.95f);
        TeamId? team = ResolveMirandoAttackerTeam();
        if (!team.HasValue)
            return fallback;

        Color teamColor = TeamUtils.GetColor(team.Value);
        teamColor.a = fallback.a;
        return teamColor;
    }

    private float GetMirandoPreviewWidth()
    {
        return animationManager != null ? animationManager.MirandoPreviewWidth : 0.12f;
    }

    private float GetMirandoPreviewSpeed()
    {
        return animationManager != null ? animationManager.MirandoPreviewSpeed : 3f;
    }

    private float GetMirandoPreviewSegmentLength()
    {
        return animationManager != null ? animationManager.MirandoPreviewSegmentLength : 1.1f;
    }

    private int GetMirandoPreviewSegmentQuantities()
    {
        return animationManager != null ? animationManager.MirandoPreviewSegmentQuantities : 1;
    }

    private float GetMirandoSpotterPreviewMultiplier()
    {
        return animationManager != null ? animationManager.MirandoSpotterPreviewMultiplier : 0.55f;
    }

    private int GetMirandoSpotterSegmentQuantities()
    {
        return animationManager != null ? animationManager.MirandoSpotterSegmentQuantities : 1;
    }

    private float GetMirandoSpotterSegmentSpeed()
    {
        return animationManager != null ? animationManager.MirandoSpotterSegmentSpeed : 3f;
    }

    private float GetMirandoParabolaBend()
    {
        return animationManager != null ? animationManager.MirandoParabolaBend : 1.2f;
    }

    private int GetMirandoParabolaSamples()
    {
        return animationManager != null ? animationManager.MirandoParabolaSamples : 24;
    }

    private int GetMirandoPreviewSortingOrder()
    {
        return animationManager != null ? animationManager.MirandoPreviewSortingOrder : 120;
    }

    private int GetMirandoPreviewSortingLayerId()
    {
        return animationManager != null ? animationManager.MirandoPreviewSortingLayerId : 0;
    }

    private TeamId? ResolveMirandoAttackerTeam()
    {
        if (scannerSelectedTargetIndex >= 0 && scannerSelectedTargetIndex < cachedPodeMirarTargets.Count)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[scannerSelectedTargetIndex];
            if (option != null && option.attackerUnit != null)
                return option.attackerUnit.TeamId;
        }

        if (selectedUnit != null)
            return selectedUnit.TeamId;

        return null;
    }

    private static bool TryReadPressedNumber(out int number)
    {
        number = 0;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) { number = 1; return true; }
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) { number = 2; return true; }
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) { number = 3; return true; }
            if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) { number = 4; return true; }
            if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) { number = 5; return true; }
            if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) { number = 6; return true; }
            if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame) { number = 7; return true; }
            if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame) { number = 8; return true; }
            if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame) { number = 9; return true; }
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { number = 1; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { number = 2; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { number = 3; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { number = 4; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { number = 5; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) { number = 6; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) { number = 7; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) { number = 8; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) { number = 9; return true; }
#endif

        return false;
    }

    private static bool WasLetterPressedThisFrame(char letter)
    {
        switch (char.ToUpperInvariant(letter))
        {
            case 'A':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.A);
#endif
            case 'E':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.E);
#endif
            case 'M':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.M);
#endif
            case 'L':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.L);
#endif
            default:
                return false;
        }
    }
}
