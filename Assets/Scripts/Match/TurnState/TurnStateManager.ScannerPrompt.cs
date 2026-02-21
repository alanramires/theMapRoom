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
        EmbarkConfirmTarget = 4
    }

    private ScannerPromptStep scannerPromptStep = ScannerPromptStep.AwaitingAction;
    private int scannerSelectedTargetIndex = -1;
    private int scannerSelectedEmbarkIndex = -1;
    private bool scannerSelectedEmbarkIsValid;
    private bool embarkExecutionInProgress;
    private CursorState cursorStateBeforeMirando = CursorState.MoveuParado;
    private CursorState lastLoggedCursorState = (CursorState)(-1);
    private UnitManager lastLoggedSelectedUnit;
    private LineRenderer mirandoPreviewRenderer;
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
    private LineRenderer embarkPreviewRenderer;
    private readonly List<Vector3> embarkPreviewPathPoints = new List<Vector3>();
    private readonly List<Vector3> embarkPreviewSegmentPoints = new List<Vector3>();
    private float embarkPreviewPathLength;
    private float embarkPreviewHeadDistance;
    private Color embarkPreviewColor = Color.white;
    [SerializeField] private bool enableUnitSelectedLayerPreviewHotkeys = true;

    private void Update()
    {
        TrackRuntimeDebugLogs();
        ProcessUnitSelectedLayerPreviewInput();
        ProcessScannerPromptInput();
        UpdateMirandoPreviewAnimation();
        UpdateEmbarkPreviewAnimation();
    }

    private void ProcessUnitSelectedLayerPreviewInput()
    {
        if (!enableUnitSelectedLayerPreviewHotkeys || !Application.isPlaying)
            return;
        if (IsMovementAnimationRunning())
            return;
        if (cursorState != CursorState.UnitSelected || selectedUnit == null)
            return;

        int delta = 0;
        if (WasLetterPressedThisFrame('D'))
            delta = -1;
        else if (WasLetterPressedThisFrame('S'))
            delta = +1;

        if (delta == 0)
            return;

        if (!TryGetNextLayerModeForSelectedUnit(delta, out UnitLayerMode targetMode, out int shownIndex, out int totalModes))
            return;

        cursorController?.PlayConfirmSfx();
        selectedUnit.TrySetCurrentLayerMode(targetMode.domain, targetMode.heightLevel);
        selectedUnit.PullCellFromTransform();
        if (cursorController != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }
        ClearSensorResults();
        PaintSelectedUnitMovementRange();
        Debug.Log($"[Layer Preview] {selectedUnit.name}: {targetMode.domain}/{targetMode.heightLevel} ({shownIndex}/{totalModes})");
    }

    private bool TryGetNextLayerModeForSelectedUnit(int delta, out UnitLayerMode targetMode, out int shownIndex, out int totalModes)
    {
        targetMode = default(UnitLayerMode);
        shownIndex = 0;
        totalModes = 0;
        if (selectedUnit == null)
            return false;

        List<UnitLayerMode> orderedModes = BuildOrderedLayerModesForSelectedUnit(selectedUnit);
        totalModes = orderedModes.Count;
        if (totalModes <= 1)
            return false;

        UnitLayerMode currentMode = selectedUnit.GetCurrentLayerMode();
        int currentIndex = 0;
        for (int i = 0; i < orderedModes.Count; i++)
        {
            if (orderedModes[i].domain == currentMode.domain && orderedModes[i].heightLevel == currentMode.heightLevel)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = currentIndex + delta;
        if (nextIndex < 0 || nextIndex >= orderedModes.Count)
            return false;

        targetMode = orderedModes[nextIndex];
        shownIndex = nextIndex + 1;
        return true;
    }

    private static List<UnitLayerMode> BuildOrderedLayerModesForSelectedUnit(UnitManager unit)
    {
        List<UnitLayerMode> ordered = new List<UnitLayerMode>();
        if (unit == null)
            return ordered;

        IReadOnlyList<UnitLayerMode> modes = unit.GetAllLayerModes();
        if (modes == null)
            return ordered;

        for (int i = 0; i < modes.Count; i++)
            ordered.Add(modes[i]);

        ordered.Sort((a, b) =>
        {
            int byHeight = ((int)a.heightLevel).CompareTo((int)b.heightLevel);
            if (byHeight != 0)
                return byHeight;
            return ((int)a.domain).CompareTo((int)b.domain);
        });

        return ordered;
    }

    private void TrackRuntimeDebugLogs()
    {
        if (!Application.isPlaying)
            return;

        bool stateChanged = lastLoggedCursorState != cursorState;
        bool selectedChanged = lastLoggedSelectedUnit != selectedUnit;
        if (!stateChanged && !selectedChanged)
            return;

        lastLoggedCursorState = cursorState;
        lastLoggedSelectedUnit = selectedUnit;
        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        Debug.Log($"[TurnState] state={cursorState} | selected={selectedName}");

        if ((cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado) && selectedUnit != null)
            LogScannerPanel();
    }

    private void ResetScannerPromptState()
    {
        scannerPromptStep = ScannerPromptStep.AwaitingAction;
        scannerSelectedTargetIndex = -1;
        scannerSelectedEmbarkIndex = -1;
        scannerSelectedEmbarkIsValid = false;
        ClearMirandoPreview();
        ClearEmbarkPreview();
    }

    private bool HandleScannerPromptCancel()
    {
        if (cursorState == CursorState.Mirando && scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget)
        {
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            FocusCurrentMirandoTarget(logDetails: true);
            return true;
        }

        if ((cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado) &&
            scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget)
        {
            scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
            FocusCurrentEmbarkTarget(logDetails: true);
            return true;
        }

        if ((cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado) &&
            scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget)
        {
            ResetScannerPromptState();
            if (cursorState == CursorState.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
                DrawCommittedPathVisual(committedMovementPath);
            if (cursorController != null && selectedUnit != null)
            {
                Vector3Int unitCell = selectedUnit.CurrentCellPosition;
                unitCell.z = 0;
                cursorController.SetCell(unitCell, playMoveSfx: false);
            }
            LogScannerPanel();
            return true;
        }

        return false;
    }

    private void ProcessScannerPromptInput()
    {
        if (IsMovementAnimationRunning() || embarkExecutionInProgress)
            return;

        if (cursorState == CursorState.Mirando)
            return;

        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return;

        if (scannerPromptStep == ScannerPromptStep.AwaitingAction)
        {
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
                HandleAircraftOperationRequested();
                return;
            }

            if (WasLetterPressedThisFrame('M'))
            {
                HandleMoveOnlyActionRequested();
                return;
            }

            return;
        }

        if (scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget || scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget)
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

    private void HandleAircraftOperationRequested()
    {
        SensorMovementMode movementMode = cursorState == CursorState.MoveuAndando
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : (selectedUnit != null ? selectedUnit.BoardTilemap : null);
        if (!AircraftOperationRules.TryApplyOperation(selectedUnit, boardMap, terrainDatabase, movementMode, out AircraftOperationDecision decision))
        {
            string reason = !string.IsNullOrWhiteSpace(decision.reason) ? decision.reason : "Sem operacao aerea valida.";
            Debug.Log($"Operacao Aerea (\"L\"): {reason}");
            return;
        }

        cursorController?.PlayConfirmSfx();
        Debug.Log($"[Operacao Aerea] {decision.label} executado.");

        if (decision.consumesAction)
        {
            bool finished = TryFinalizeSelectedUnitActionFromDebug();
            if (finished)
            {
                cursorController?.PlayDoneSfx();
                ResetScannerPromptState();
                return;
            }
        }

        cursorState = CursorState.UnitSelected;
        ClearSensorResults();
        PaintSelectedUnitMovementRange();
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }
    }

    private void HandleEmbarkActionRequested()
    {
        bool hasValid = cachedPodeEmbarcarTargets.Count > 0;
        if (!hasValid)
        {
            Debug.Log("Pode Embarcar (\"E\"): nao ha transportador valido adjacente.");
            LogScannerPanel();
            return;
        }

        cursorController?.PlayConfirmSfx();
        // Mesma regra do Mirando: ao entrar em um submenu de sensor, oculta o preview de movimento.
        ClearCommittedPathVisual();
        scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
        scannerSelectedEmbarkIndex = 0;
        FocusCurrentEmbarkTarget(logDetails: true);
        LogEmbarkSelectionPanel();
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

    private bool TryConfirmScannerEmbark()
    {
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
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
                if (!AircraftOperationRules.TryApplyOperation(
                        transporter,
                        movementTilemap,
                        terrainDatabase,
                        SensorMovementMode.MoveuParado,
                        out AircraftOperationDecision landingDecision) ||
                    landingDecision.action != AircraftOperationAction.Land)
                {
                    Debug.Log(string.IsNullOrWhiteSpace(landingDecision.reason)
                        ? "[Embarque] Transportador aereo sem pouso valido."
                        : $"[Embarque] {landingDecision.reason}");
                    embarkExecutionInProgress = false;
                    scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
                    RefreshSensorsForCurrentState();
                    yield break;
                }

                // Feedback do "forced landing": usa o SFX de movimento da unidade que pousou.
                PlayMovementStartSfx(transporter);
                Debug.Log("[Embarque] Transportador pousou antes do embarque.");

                float landingDuration = GetEmbarkForcedLandingDuration();
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

        scannerSelectedEmbarkIsValid = true;
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

        if (scannerPromptStep == ScannerPromptStep.MirandoCycleTarget)
        {
            if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
                return true;

            scannerPromptStep = ScannerPromptStep.MirandoConfirmTarget;
            PodeMirarTargetOption picked = cachedPodeMirarTargets[scannerSelectedTargetIndex];
            LogAttackConfirmationPrompt(picked, scannerSelectedTargetIndex + 1);
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

        cursorController?.PlayDoneSfx();
        TryFinalizeSelectedUnitActionFromDebug();
        ResetScannerPromptState();
        return true;
    }

    private void EnterMirandoState()
    {
        if (cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado)
            cursorStateBeforeMirando = cursorState;

        // Ao sair do fluxo de movimento para mirar, oculta o rastro legado do caminho comprometido.
        ClearCommittedPathVisual();

        cursorState = CursorState.Mirando;
        scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
        scannerSelectedTargetIndex = 0;

        LogTargetSelectionPanel();
        FocusCurrentMirandoTarget(logDetails: true);
    }

    private void FocusCurrentMirandoTarget(bool logDetails, bool moveCursor = true)
    {
        if (cachedPodeMirarTargets.Count == 0)
        {
            SetMirandoPreviewVisible(false);
            return;
        }

        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
            scannerSelectedTargetIndex = 0;

        PodeMirarTargetOption option = cachedPodeMirarTargets[scannerSelectedTargetIndex];
        if (moveCursor)
            MoveCursorToTarget(option);
        RebuildMirandoPreviewPath(option);
        SetMirandoPreviewVisible(cursorState == CursorState.Mirando);
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

        Debug.Log(
            $"[Mirando] Alvo {shownIndex}/{total}\n" +
            $"Label: {label}\n" +
            $"Unidade: {target.name}\n" +
            $"HP: {target.CurrentHP}\n" +
            $"Arma atacante: {attackWeapon}\n" +
            $"Revide: {counterText}\n" +
            "Use setas para trocar alvo. Enter confirma. ESC volta.");
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
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
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

    private bool IsEmbarkPromptActive()
    {
        return scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget ||
               scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget;
    }

    private void ExitMirandoStateToMovement()
    {
        if (cursorState != CursorState.Mirando)
            return;

        cursorState = cursorStateBeforeMirando == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        if (cursorState == CursorState.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
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
        if (cursorState == CursorState.Mirando)
            TryRefreshMirandoPreviewPathIfNeeded();

        bool shouldShow =
            cursorState == CursorState.Mirando &&
            mirandoPreviewPathLength > 0.0001f &&
            mirandoPreviewPathPoints.Count >= 2;

        if (!shouldShow)
        {
            SetMirandoPreviewVisible(false);
            return;
        }

        EnsureMirandoPreviewRenderer();
        if (mirandoPreviewRenderer == null)
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
        mirandoPreviewRenderer.startWidth = previewWidth;
        mirandoPreviewRenderer.endWidth = previewWidth;
        mirandoPreviewRenderer.startColor = previewColor;
        mirandoPreviewRenderer.endColor = previewColor;
        mirandoPreviewRenderer.positionCount = mirandoPreviewSegmentPoints.Count;
        for (int i = 0; i < mirandoPreviewSegmentPoints.Count; i++)
            mirandoPreviewRenderer.SetPosition(i, mirandoPreviewSegmentPoints[i]);
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

    private void EnsureMirandoPreviewRenderer()
    {
        if (mirandoPreviewRenderer != null)
            return;

        GameObject go = new GameObject("MirandoPreviewLine");
        go.transform.SetParent(transform, false);
        mirandoPreviewRenderer = go.AddComponent<LineRenderer>();
        mirandoPreviewRenderer.useWorldSpace = true;
        mirandoPreviewRenderer.textureMode = LineTextureMode.Stretch;
        mirandoPreviewRenderer.numCapVertices = 2;
        mirandoPreviewRenderer.numCornerVertices = 2;
        mirandoPreviewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mirandoPreviewRenderer.receiveShadows = false;
        Material previewMaterial = GetMirandoPreviewMaterial();
        mirandoPreviewRenderer.material = previewMaterial != null ? previewMaterial : new Material(Shader.Find("Sprites/Default"));
        int sortingLayerId = GetMirandoPreviewSortingLayerId();
        if (sortingLayerId != 0)
            mirandoPreviewRenderer.sortingLayerID = sortingLayerId;
        mirandoPreviewRenderer.sortingOrder = GetMirandoPreviewSortingOrder();
        mirandoPreviewRenderer.enabled = false;
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
        if (mirandoPreviewRenderer == null)
            return;

        if (!visible)
        {
            mirandoPreviewRenderer.positionCount = 0;
            mirandoPreviewRenderer.enabled = false;
            return;
        }

        mirandoPreviewRenderer.enabled = true;
    }

    private void ClearMirandoPreview()
    {
        mirandoPreviewPathPoints.Clear();
        mirandoPreviewSegmentPoints.Clear();
        mirandoPreviewPathLength = 0f;
        mirandoPreviewHeadDistance = 0f;
        mirandoPreviewSignatureValid = false;
        SetMirandoPreviewVisible(false);
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
            case 'D':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.D);
#endif
            case 'S':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.S);
#endif
            default:
                return false;
        }
    }
}
