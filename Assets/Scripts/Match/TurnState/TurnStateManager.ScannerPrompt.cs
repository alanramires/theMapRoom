using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class TurnStateManager
{
    private const int RemovingUnitConfirmFocusIndex = 0;
    private const int RemovingUnitCancelFocusIndex = 1;
    private int removingUnitFocusIndex = RemovingUnitConfirmFocusIndex;

    public int RemovingUnitFocusIndex => removingUnitFocusIndex;

    public bool NavigateRemovingUnitFocus(int delta)
    {
        if (CurrentCursorState != CursorState.RemovingUnit || delta == 0)
            return false;

        // Wrap: de CONFIRMAR pra cima vai pro CANCELAR e vice-versa (mesma flexibilidade dos demais).
        int total = RemovingUnitCancelFocusIndex - RemovingUnitConfirmFocusIndex + 1;
        int next = (removingUnitFocusIndex + (delta > 0 ? 1 : -1) + total) % total;
        if (next == removingUnitFocusIndex)
            return false;

        removingUnitFocusIndex = next;
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public void SetRemovingUnitFocus(int index)
    {
        if (CurrentCursorState != CursorState.RemovingUnit)
            return;
        removingUnitFocusIndex = Mathf.Clamp(index,
            RemovingUnitConfirmFocusIndex, RemovingUnitCancelFocusIndex);
    }
    private enum ScannerPromptStep
    {
        AwaitingAction = 0,
        MirandoCycleTarget = 1,
        MirandoConfirmTarget = 2,
        EmbarkCycleTarget = 3,
        EmbarkConfirmTarget = 4,
        LandingCycleOption = 5,
        LandingConfirmOption = 6,
        DisembarkPassengerSelect = 7,
        DisembarkLandingSelect = 8,
        DisembarkConfirm = 9,
        MergeParticipantSelect = 10,
        MergeTargetSelect = 11,
        MergeConfirm = 12,
        ThreatLayerTeamSelect = 13
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

    private readonly struct MirandoSelectionEntry
    {
        public readonly bool isValid;
        public readonly PodeMirarTargetOption validOption;
        public readonly PodeMirarInvalidOption invalidOption;

        public MirandoSelectionEntry(PodeMirarTargetOption option)
        {
            isValid = true;
            validOption = option;
            invalidOption = null;
        }

        public MirandoSelectionEntry(PodeMirarInvalidOption option)
        {
            isValid = false;
            validOption = null;
            invalidOption = option;
        }

        public UnitManager AttackerUnit => isValid ? validOption != null ? validOption.attackerUnit : null : invalidOption != null ? invalidOption.attackerUnit : null;
        public UnitManager TargetUnit => isValid ? validOption != null ? validOption.targetUnit : null : invalidOption != null ? invalidOption.targetUnit : null;
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
    private bool mergeExecutionInProgress;
    private bool suppressInitialMirandoAutoFocus;
    private CursorState cursorStateBeforeEmbarcando = CursorState.MoveuParado;
    private CursorState cursorStateBeforePousando = CursorState.MoveuParado;
    private CursorState cursorStateBeforeMirando = CursorState.MoveuParado;
    private CursorState lastLoggedCursorState = (CursorState)(-1);
    private ScannerPromptStep lastLoggedScannerPromptStep = (ScannerPromptStep)(-1);
    private UnitManager lastLoggedSelectedUnit;
    private readonly List<LineRenderer> mirandoPreviewRenderers = new List<LineRenderer>();
    private readonly List<Vector3> mirandoPreviewPathPoints = new List<Vector3>();
    private readonly List<Vector3> mirandoPreviewSegmentPoints = new List<Vector3>();
    private readonly List<MirandoSelectionEntry> cachedMirandoSelectionEntries = new List<MirandoSelectionEntry>();
    private readonly Dictionary<SpriteRenderer, Color> mirandoInvalidTintOriginalColors = new Dictionary<SpriteRenderer, Color>();
    private UnitManager highlightedMirandoTarget;
    private float mirandoPreviewPathLength;
    private float mirandoPreviewHeadDistance;
    private bool mirandoPreviewUseInvalidColor;
    private bool mirandoPreviewSignatureValid;
    private bool aiDebugStepPreviewActive;
    private readonly Color aiDebugStepPreviewColor = new Color(0.1f, 0.45f, 1f, 0.95f);
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
    [Header("Debug Logs & Perf Snapshot (F8)")]
    [SerializeField] private bool enableTurnStateRuntimeLogs = false;
    [Tooltip("Exibe no Console os logs informativos do fluxo de movimento.")]
    [InspectorName("Show Movement Logs")]
    [SerializeField] private bool showMovementLogs = false;
    public bool ShowMovementLogs => showMovementLogs;
    [SerializeField] private bool enableRangeCacheDebugLogs = true;
    [SerializeField] private bool showPerfRangeLine = true;
    [SerializeField] private bool showPerfSensorsLine = true;
    [SerializeField] private bool showPerfSelectionLine = true;
    [SerializeField] private bool showPerfTakeoffPrepLine = true;
    [InspectorName("Show Frame Spike Logs")]
    [SerializeField] private bool showFrameSpikeLogs = false;
    public bool ShowFrameSpikeLogs => showFrameSpikeLogs;
    [Tooltip("Registra automaticamente frames acima deste tempo enquanto Show Frame Spike Logs estiver ligado.")]
    [SerializeField, Min(16f)] private float frameSpikeThresholdMs = 250f;
    private const int PerfFrameWindowSampleCount = 120;
    private readonly float[] perfFrameWindowMs = new float[PerfFrameWindowSampleCount];
    private int perfFrameWindowWriteIndex;
    private int perfFrameSamplesCollected;
    private double perfFrameWindowSumMs;
    private float perfFrameWindowMinMs = float.MaxValue;
    private float perfFrameWindowMaxMs;
    private long perfPreviousManagedBytes = -1L;
    private int perfPreviousGc0Count;
    private int perfPreviousGc1Count;
    private int perfPreviousGc2Count;
    private double perfLastRangeMs;
    private double perfRangeTotalMs;
    private int perfRangeCallCount;
    private double perfLastSensorsMs;
    private double perfSensorsTotalMs;
    private int perfSensorsCallCount;
    private double perfLastSelectionMs;
    private double perfSelectionTotalMs;
    private int perfSelectionCallCount;
    private double perfLastTakeoffPrepMs;
    private double perfTakeoffPrepTotalMs;
    private int perfTakeoffPrepCallCount;
    private bool aiActionOverlaysSuppressed;

    private void Update()
    {
        UpdateAiActionOverlayPresentation();
        RecordFramePerfSample();
        if (PanelRodadaController.IsGameplayInputBlocked)
            return;
        ProcessPerformanceSnapshotHotkeyInput();
        UpdateInspectedHelperAutoDismiss();
        TrackRuntimeDebugLogs();
        ProcessDestroyUnitHotkeyInput();
        ProcessConstructionShoppingInput();
        UpdateShoppingPreviewPersistence();
        ProcessScannerPromptInput();
        ProcessCommandServiceHotkeyInput();
        ProcessPlanningHotkeyInput();
        UpdateMirandoPreviewAnimation();
        UpdateEmbarkPreviewAnimation();
        UpdateMergeQueuePreviewAnimation();
        UpdateSupplyQueuePreviewAnimation();
        UpdateTransferPromptPreview();
    }

    private void UpdateAiActionOverlayPresentation()
    {
        TilemapRenderer rangeRenderer = rangeMapTilemap != null ? rangeMapTilemap.GetComponent<TilemapRenderer>() : null;
        TilemapRenderer lineRenderer = lineOfFireMapTilemap != null ? lineOfFireMapTilemap.GetComponent<TilemapRenderer>() : null;

        if (aiActionOverlaysSuppressed)
        {
            ClearMovementRangeVisualOnly(keepCommittedMovement: false);
            ClearLineOfFireArea();
            ClearCommittedPathVisual();
            aiActionOverlaysSuppressed = false;
        }

        if (rangeRenderer != null)
            rangeRenderer.enabled = true;
        if (lineRenderer != null)
            lineRenderer.enabled = true;
    }

    private bool ShouldSuppressAiActionPreviewLines()
    {
        // A layer FogOfWar cobre as linhas e previews nas celulas ocultas.
        return false;
    }

    private void RecordFramePerfSample()
    {
        float frameMs = Time.unscaledDeltaTime * 1000f;
        if (!float.IsFinite(frameMs) || frameMs <= 0f)
            return;

        if (perfFrameSamplesCollected == PerfFrameWindowSampleCount)
            perfFrameWindowSumMs -= perfFrameWindowMs[perfFrameWindowWriteIndex];
        else
            perfFrameSamplesCollected++;

        perfFrameWindowMs[perfFrameWindowWriteIndex] = frameMs;
        perfFrameWindowSumMs += frameMs;
        perfFrameWindowWriteIndex = (perfFrameWindowWriteIndex + 1) % PerfFrameWindowSampleCount;

        // Janela curta (120): recalcular min/max evita que um spike antigo fique
        // eternamente preso no F8 e torna o snapshot representativo dos frames atuais.
        perfFrameWindowMinMs = float.MaxValue;
        perfFrameWindowMaxMs = 0f;
        for (int i = 0; i < perfFrameSamplesCollected; i++)
        {
            float sampleMs = perfFrameWindowMs[i];
            perfFrameWindowMinMs = Mathf.Min(perfFrameWindowMinMs, sampleMs);
            perfFrameWindowMaxMs = Mathf.Max(perfFrameWindowMaxMs, sampleMs);
        }

        TrackFrameSpike(frameMs);
    }

    private void TrackFrameSpike(float frameMs)
    {
        if (!showFrameSpikeLogs)
            return;

        long managedBytes = System.GC.GetTotalMemory(false);
        int gc0 = System.GC.CollectionCount(0);
        int gc1 = System.GC.CollectionCount(1);
        int gc2 = System.GC.CollectionCount(2);
        long managedDeltaBytes = perfPreviousManagedBytes >= 0L ? managedBytes - perfPreviousManagedBytes : 0L;
        int gc0Delta = gc0 - perfPreviousGc0Count;
        int gc1Delta = gc1 - perfPreviousGc1Count;
        int gc2Delta = gc2 - perfPreviousGc2Count;
        perfPreviousManagedBytes = managedBytes;
        perfPreviousGc0Count = gc0;
        perfPreviousGc1Count = gc1;
        perfPreviousGc2Count = gc2;

        if (frameMs < Mathf.Max(16f, frameSpikeThresholdMs))
            return;

        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        bool replayActive = replayManager != null && replayManager.IsReplaying;
        bool movementAnimating = animationManager != null && animationManager.IsAnimatingMovement;
        bool aiTurn = matchController != null && matchController.IsActiveTeamAI();
        bool aiInputLock = matchController != null && matchController.IsPlayerInputLockedByActiveAI();
        bool turnTransition = matchController != null && matchController.IsTurnTransitionInProgress;
        double managedMb = managedBytes / (1024d * 1024d);
        double managedDeltaMb = managedDeltaBytes / (1024d * 1024d);
#if UNITY_2020_1_OR_NEWER
        double unityAllocatedMb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024d * 1024d);
#else
        double unityAllocatedMb = 0d;
#endif

        Debug.Log(
            $"[FrameSpike] frame={Time.frameCount} duration={frameMs:0.00}ms " +
            $"state={CurrentCursorState} substep={scannerPromptStep} selected={selectedName} " +
            $"boardRev={ThreatRevisionTracker.GlobalBoardRevision} " +
            $"replay={replayActive} aiTurn={aiTurn} aiInputLock={aiInputLock} " +
            $"turnTransition={turnTransition} movementAnimating={movementAnimating} " +
            $"gameplayInputBlocked={PanelRodadaController.IsGameplayInputBlocked} " +
            $"managed={managedMb:0.0}MB managedDelta={managedDeltaMb:+0.0;-0.0;0.0}MB " +
            $"gcDelta=[{gc0Delta},{gc1Delta},{gc2Delta}] unityAlloc={unityAllocatedMb:0.0}MB");
    }

    private void RegisterPerfRangeDuration(double ms)
    {
        if (ms < 0d || double.IsNaN(ms) || double.IsInfinity(ms))
            return;

        perfLastRangeMs = ms;
        perfRangeTotalMs += ms;
        perfRangeCallCount++;
    }

    private void RegisterPerfSensorsDuration(double ms)
    {
        if (ms < 0d || double.IsNaN(ms) || double.IsInfinity(ms))
            return;

        perfLastSensorsMs = ms;
        perfSensorsTotalMs += ms;
        perfSensorsCallCount++;
    }

    private void RegisterPerfSelectionDuration(double ms)
    {
        if (ms < 0d || double.IsNaN(ms) || double.IsInfinity(ms))
            return;

        perfLastSelectionMs = ms;
        perfSelectionTotalMs += ms;
        perfSelectionCallCount++;
    }

    private void RegisterPerfTakeoffPrepDuration(double ms)
    {
        if (ms < 0d || double.IsNaN(ms) || double.IsInfinity(ms))
            return;

        perfLastTakeoffPrepMs = ms;
        perfTakeoffPrepTotalMs += ms;
        perfTakeoffPrepCallCount++;
    }

    private void ProcessPerformanceSnapshotHotkeyInput()
    {
        if (!WasFunctionKeyPressedThisFrame(KeyCode.F8))
            return;
        if (UiInputBlocker.IsTextInputFocused())
            return;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int activeUnits = units != null ? units.Length : 0;
        int embarkedUnits = 0;
        for (int i = 0; i < activeUnits; i++)
        {
            UnitManager unit = units[i];
            if (unit != null && unit.IsEmbarked)
                embarkedUnits++;
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int activeConstructions = constructions != null ? constructions.Length : 0;

        float avgFrameMs = perfFrameSamplesCollected > 0 ? (float)(perfFrameWindowSumMs / perfFrameSamplesCollected) : 0f;
        float avgFps = avgFrameMs > 0.0001f ? (1000f / avgFrameMs) : 0f;
        float minFrameMs = perfFrameWindowMinMs == float.MaxValue ? 0f : perfFrameWindowMinMs;
        float maxFrameMs = perfFrameWindowMaxMs;
        double managedMb = System.GC.GetTotalMemory(false) / (1024d * 1024d);
#if UNITY_2020_1_OR_NEWER
        double unityAllocatedMb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024d * 1024d);
        double unityReservedMb = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024d * 1024d);
        double unityUnusedReservedMb = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong() / (1024d * 1024d);
#else
        double unityAllocatedMb = 0d;
        double unityReservedMb = 0d;
        double unityUnusedReservedMb = 0d;
#endif
        double avgRangeMs = perfRangeCallCount > 0 ? perfRangeTotalMs / perfRangeCallCount : 0d;
        double avgSensorsMs = perfSensorsCallCount > 0 ? perfSensorsTotalMs / perfSensorsCallCount : 0d;
        double avgSelectionMs = perfSelectionCallCount > 0 ? perfSelectionTotalMs / perfSelectionCallCount : 0d;
        double avgTakeoffPrepMs = perfTakeoffPrepCallCount > 0 ? perfTakeoffPrepTotalMs / perfTakeoffPrepCallCount : 0d;

        System.Text.StringBuilder sb = new System.Text.StringBuilder(768);
        sb.AppendLine("[PERF SNAPSHOT F8]");
        sb.AppendLine($"Amostras frame: {perfFrameSamplesCollected}/{PerfFrameWindowSampleCount}");
        sb.AppendLine($"Frame: avg={avgFrameMs:0.00}ms ({avgFps:0.0} FPS) | min={minFrameMs:0.00}ms | max={maxFrameMs:0.00}ms");
        sb.AppendLine($"Unidades ativas: {activeUnits} | embarcadas: {embarkedUnits} | construcoes ativas: {activeConstructions}");
        sb.AppendLine($"Memoria managed: {managedMb:0.0} MB");
        sb.AppendLine($"Memoria Unity: alloc={unityAllocatedMb:0.0} MB | reserved={unityReservedMb:0.0} MB | unusedReserved={unityUnusedReservedMb:0.0} MB");
        if (showPerfRangeLine)
            sb.AppendLine($"PaintSelectedUnitMovementRange: last={perfLastRangeMs:0.00}ms | avg={avgRangeMs:0.00}ms | calls={perfRangeCallCount}");
        if (showPerfSensorsLine)
            sb.AppendLine($"RefreshSensorsForCurrentState: last={perfLastSensorsMs:0.00}ms | avg={avgSensorsMs:0.00}ms | calls={perfSensorsCallCount}");
        if (showPerfSelectionLine)
            sb.AppendLine($"SetSelectedUnit pipeline: last={perfLastSelectionMs:0.00}ms | avg={avgSelectionMs:0.00}ms | calls={perfSelectionCallCount}");
        if (showPerfTakeoffPrepLine)
            sb.AppendLine($"TryPrepareTemporaryTakeoffStateForSelection: last={perfLastTakeoffPrepMs:0.00}ms | avg={avgTakeoffPrepMs:0.00}ms | calls={perfTakeoffPrepCallCount}");
        // O snapshot F8 possui seus próprios toggles Show Perf*. Ele não pode
        // depender de Enable TurnState Runtime Logs, senão as linhas escolhidas
        // no Inspector são silenciosamente descartadas pelo RuntimeLog().
        Debug.Log(sb.ToString());
        PanelDialogController.TrySetTransientText("Perf snapshot logged (F8)", 1.8f);
    }

    private void TrackRuntimeDebugLogs()
    {
        if (!Application.isPlaying || !enableTurnStateRuntimeLogs)
            return;

        bool stateChanged = lastLoggedCursorState != CurrentCursorState;
        bool substepChanged = lastLoggedScannerPromptStep != scannerPromptStep;
        bool selectedChanged = lastLoggedSelectedUnit != selectedUnit;
        if (!stateChanged && !selectedChanged && !substepChanged)
            return;

        ScannerPromptStep previousSubstep = lastLoggedScannerPromptStep;
        lastLoggedCursorState = CurrentCursorState;
        lastLoggedScannerPromptStep = scannerPromptStep;
        lastLoggedSelectedUnit = selectedUnit;
        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        //RuntimeLog($"[TurnState] state={CurrentCursorState} | selected={selectedName}");
        if (substepChanged)
        {
            bool rollback = previousSubstep != (ScannerPromptStep)(-1) && (int)scannerPromptStep < (int)previousSubstep;
            string rollbackTag = rollback ? " [roll back]" : string.Empty;
            RuntimeLog($"[TurnState]{rollbackTag} substep={previousSubstep} -> {scannerPromptStep} | state={CurrentCursorState}");
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
        cachedMirandoSelectionEntries.Clear();
        disembarkPassengerEntries.Clear();
        disembarkQueuedOrders.Clear();
        disembarkLandingOptions.Clear();
        disembarkLandingByCell.Clear();
        disembarkSelectedPassengerIndex = -1;
        disembarkSelectedLandingCellValid = false;
        disembarkLandingAutoEntered = false;
        ResetMergeRuntimeState();
        ResetSupplyRuntimeState();
        ClearPendingTransferPrompt();
        ClearMirandoPreview();
        ClearEmbarkPreview();
    }

    private void ProcessPlanningHotkeyInput()
    {
        if (!WasLetterPressedThisFrame('P'))
            return;
        if (UiInputBlocker.IsTextInputFocused())
            return;
        if (replayManager != null && replayManager.IsReplaying)
            return;

        bool toggled = TryTogglePlanningModeByHotkey();
        if (!toggled)
            return;

        cursorController?.PlayConfirmSfx();
    }

    private void ProcessDestroyUnitHotkeyInput()
    {
        if (replayManager != null && replayManager.IsReplaying)
            return;

        if (!WasLetterPressedThisFrame('U'))
            return;

        if (TutorialManager.IsRemoveUnitBlockedByTutorial)
        {
            TutorialManager.ShowBlockedActionScold(TutorialScoldKind.RemoveUnit);
            cursorController?.PlayErrorSfx();
            return;
        }

        if (UiInputBlocker.IsTextInputFocused())
            return;

        if (CurrentCursorState != CursorState.Neutral)
            return;

        if (IsMovementAnimationRunning())
        {
            RuntimeLog("[Destroy Unit] Aguarde o fim da animacao atual.");
            return;
        }

        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                RuntimeLog($"[Destroy Unit] {reason}");
            return;
        }

        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        if (!CanDestroyUnitTargetForActiveTeam(target, activeTeam, out string ownershipReason))
        {
            RuntimeLog($"[Destroy Unit] {ownershipReason}");
            return;
        }

        string targetName = ResolveDebugUnitName(target);
        PanelDialogController.TrySetExternalText($"Destroy Unit :: {targetName} {FormatMapCellWithZ(cursorCell)} :: Confirm");
        removingUnitFocusIndex = RemovingUnitConfirmFocusIndex;
        Advance(CursorState.RemovingUnit, "ProcessDestroyUnitHotkeyInput");
        cursorController?.PlayConfirmSfx();
        RuntimeLog("[Destroy Unit] Confirmar com Enter | Cancelar com ESC.");
    }

    public bool TryOpenDestroyUnitPromptFromMenu(out string message)
    {
        message = string.Empty;

        if (TutorialManager.IsRemoveUnitBlockedByTutorial)
        {
            TutorialManager.ShowBlockedActionScold(TutorialScoldKind.RemoveUnit);
            return false;
        }

        if (replayManager != null && replayManager.IsReplaying)
        {
            message = "Destroy Unit indisponivel durante replay.";
            return false;
        }

        if (CurrentCursorState != CursorState.Neutral && CurrentCursorState != CursorState.PlayerMenu)
        {
            message = $"Destroy Unit exige cursor em Neutral/PlayerMenu (atual: {CurrentCursorState}).";
            return false;
        }

        if (IsMovementAnimationRunning())
        {
            message = "Destroy Unit indisponivel durante animacao.";
            return false;
        }

        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out string reason))
        {
            message = string.IsNullOrWhiteSpace(reason) ? "Nenhuma unidade valida no cursor." : reason;
            return false;
        }

        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        if (!CanDestroyUnitTargetForActiveTeam(target, activeTeam, out string ownershipReason))
        {
            message = ownershipReason;
            return false;
        }

        string targetName = ResolveDebugUnitName(target);
        PanelDialogController.TrySetExternalText($"Destroy Unit :: {targetName} {FormatMapCellWithZ(cursorCell)} :: Confirm");
        removingUnitFocusIndex = RemovingUnitConfirmFocusIndex;
        Advance(CursorState.RemovingUnit, "TryOpenDestroyUnitPromptFromMenu");
        message = "[Destroy Unit] Confirmar com Enter | Cancelar com ESC.";
        RuntimeLog(message);
        return true;
    }

    private bool TryConfirmRemovingUnit()
    {
        if (CurrentCursorState != CursorState.RemovingUnit)
            return false;
        if (removeUnitExecutionInProgress)
            return true;

        Vector3Int actionCell = cursorController != null ? cursorController.CurrentCell : Vector3Int.zero;
        actionCell.z = 0;
        TeamId actionTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        int actionTurn = matchController != null ? matchController.CurrentTurn : 0;

        UnitManager target = FindUnitAtCell(actionCell);
        if (target == null)
        {
            RuntimeLog($"[Destroy Unit] Nenhuma unidade no cursor {FormatMapCellWithZ(actionCell)}.");
            ExitRemovingUnitStateToNeutral(logCanceled: true);
            return true;
        }

        if (!CanDestroyUnitTargetForActiveTeam(target, actionTeam, out string ownershipReason))
        {
            RuntimeLog($"[Destroy Unit] {ownershipReason}");
            ExitRemovingUnitStateToNeutral(logCanceled: true);
            return true;
        }

        EnterRemovingUnitExecutingState("TryConfirmRemovingUnit");
        removeUnitExecutionInProgress = true;
        StartCoroutine(ExecuteRemoveUnitConfirmationFlow(target, actionCell, actionTurn, actionTeam));
        return true;
    }

    private void EnterRemovingUnitExecutingState(string reason)
    {
        if (CurrentCursorState == CursorState.RemovingUnitExecuting)
            return;

        if (CurrentCursorState != CursorState.RemovingUnit)
            Advance(CursorState.RemovingUnit, $"{reason}: ensure RemovingUnit");

        Advance(CursorState.RemovingUnitExecuting, reason);
    }

    private bool CanDestroyUnitTargetForActiveTeam(UnitManager target, TeamId activeTeam, out string reason)
    {
        reason = string.Empty;
        if (target == null)
        {
            reason = "Unidade alvo invalida.";
            return false;
        }

        if (activeTeam == TeamId.Neutral)
        {
            reason = "Destroy Unit exige um time ativo valido.";
            return false;
        }

        if (target.TeamId != activeTeam)
        {
            reason = $"Voce so pode destruir unidades do time ativo ({activeTeam}). Alvo pertence a {target.TeamId}.";
            return false;
        }

        return true;
    }

    private IEnumerator ExecuteRemoveUnitConfirmationFlow(UnitManager target, Vector3Int actionCell, int actionTurn, TeamId actionTeam)
    {
        try
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                RuntimeLog($"[Destroy Unit] Unidade alvo ausente em {FormatMapCellWithZ(actionCell)}.");
                ExitRemovingUnitStateToNeutral(logCanceled: true);
                yield break;
            }

            int targetUid = target.InstanceId;
            string targetSigla = target.TryGetUnitData(out UnitData targetData) && targetData != null
                ? targetData.apelido
                : "-";
            Vector3 worldPos = target.transform.position;
            target.SetCurrentHP(0);
            target.MarkDead("morto pelo comando destroy unit");
            KillEmbarkedChildrenChain(target);
            yield return ExecuteUnitDeathPresentation(target, actionCell, worldPos, applyStartDelay: false);

            replayManager?.RecordStandaloneAction(new PlayerAction
            {
                ActionType = PlayerActionType.RemoveUnit,
                TurnNumber = actionTurn,
                ActingTeam = actionTeam,
                CursorHex = actionCell,
                HasCursorHex = true,
                TargetHex = actionCell,
                HasTargetHex = true,
                SensorAction = SensorActionType.RemoveUnit,
                Confirmed = true,
                DebugLabel = "RemoveUnit: confirm"
            });
            JogadasManager.EnsureInstance()?.RegistrarDestruir(
                actionTurn,
                (int)actionTeam,
                actionCell.x,
                actionCell.y,
                targetSigla,
                targetUid);

            planningManager?.NotifyUnitVisibilityPossiblyChanged(target);
            ExitRemovingUnitStateToNeutral(logCanceled: false);
            cursorController?.PlayLoadSfx();
        }
        finally
        {
            removeUnitExecutionInProgress = false;
        }
    }

    private void ExitRemovingUnitStateToNeutral(bool logCanceled)
    {
        if (logCanceled)
            RuntimeLog("[Destroy Unit] Cancelado.");
        PanelDialogController.ClearExternalText();
        if (logCanceled && CurrentCursorState == CursorState.RemovingUnit)
            Retreat("ExitRemovingUnitStateToNeutral");
        else
            ExecuteAndReset("ExitRemovingUnitStateToNeutral");
    }

    private bool HandleScannerPromptCancel()
    {
        if (combatExecutionInProgress)
            return true;

        DiscardPendingCombatCinematicTrack();

        if (CurrentCursorState == CursorState.Mirando && scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget)
        {
            if (GetMirandoEntryCount() <= 1)
            {
                ExitMirandoStateToMovement();
                return true;
            }

            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            FocusCurrentMirandoTarget(logDetails: true);
            return true;
        }

        if (CurrentCursorState == CursorState.Embarcando &&
            scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget)
        {
            scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
            embarkConfirmButtonFocus = 0;
            FocusCurrentEmbarkTarget(logDetails: true);
            return true;
        }

        if (CurrentCursorState == CursorState.Embarcando &&
            scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget)
        {
            ExitEmbarkStateToMovement();
            return true;
        }

        if (CurrentCursorState == CursorState.Pousando &&
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

        if (CurrentCursorState == CursorState.Pousando &&
            scannerPromptStep == ScannerPromptStep.LandingCycleOption)
        {
            ExitLandingStateToMovement();
            return true;
        }

        if (CurrentCursorState == CursorState.Desembarcando &&
            scannerPromptStep == ScannerPromptStep.DisembarkConfirm)
        {
            ReturnToDisembarkLandingSelect();
            return true;
        }

        if (CurrentCursorState == CursorState.Desembarcando &&
            scannerPromptStep == ScannerPromptStep.DisembarkLandingSelect)
        {
            if (disembarkLandingAutoEntered)
            {
                ExitDisembarkStateToMovement();
                return true;
            }

            ReturnToDisembarkPassengerSelect();
            return true;
        }

        if (CurrentCursorState == CursorState.Desembarcando &&
            scannerPromptStep == ScannerPromptStep.DisembarkPassengerSelect)
        {
            if (TryUndoLastQueuedDisembarkOrderAndReturnToLanding())
                return true;

            ExitDisembarkStateToMovement();
            return true;
        }

        if (CurrentCursorState == CursorState.Fundindo &&
            scannerPromptStep == ScannerPromptStep.MergeConfirm)
        {
            if (mergeTargetAutoEntered)
            {
                ExitMergeStateToMovement();
                return true;
            }

            ReturnToMergeParticipantSelect();
            return true;
        }

        if (CurrentCursorState == CursorState.Fundindo &&
            scannerPromptStep == ScannerPromptStep.MergeParticipantSelect)
        {
            if (TryUndoLastQueuedMergeOrderAndReturnToTarget())
                return true;

            ExitMergeStateToMovement();
            return true;
        }

        if (CurrentCursorState == CursorState.Suprindo &&
            scannerPromptStep == ScannerPromptStep.MergeConfirm)
        {
            if (supplyTargetAutoEntered)
            {
                ExitSupplyStateToMovement();
                return true;
            }

            ReturnToSupplyCandidateSelect();
            return true;
        }

        if (CurrentCursorState == CursorState.Suprindo &&
            scannerPromptStep == ScannerPromptStep.MergeParticipantSelect)
        {
            if (TryUndoLastQueuedSupplyOrderAndReturnToTarget())
                return true;

            ExitSupplyStateToMovement();
            return true;
        }

        if (scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect)
        {
            ClearEnemyThreatLayersOverlay();
            scannerPromptStep = ScannerPromptStep.AwaitingAction;
            if (CurrentCursorState == CursorState.InspectingHotZone)
                Retreat("HandleScannerPromptCancel: threat hot zone close");
            return true;
        }

        return false;
    }

    private void ProcessScannerPromptInput()
    {
        if (IsMovementAnimationRunning() || embarkExecutionInProgress || landingExecutionInProgress || combatExecutionInProgress || captureExecutionInProgress || mergeExecutionInProgress || supplyExecutionInProgress || transferExecutionInProgress)
            return;

        if (CurrentCursorState == CursorState.Mirando)
            return;

        if (CurrentCursorState != CursorState.Neutral &&
            CurrentCursorState != CursorState.InspectingHotZone &&
            scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect)
        {
            ClearEnemyThreatLayersOverlay();
            scannerPromptStep = ScannerPromptStep.AwaitingAction;
        }

        bool isNeutralLikeInspectState = CurrentCursorState == CursorState.Neutral || CurrentCursorState == CursorState.InspectingHotZone;
        if (isNeutralLikeInspectState)
        {
            if (scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect)
            {
                bool handledThreatLayerInput = false;
                if (WasLetterPressedThisFrame('Z'))
                {
                    handledThreatLayerInput = true;
                    TryCloseThreatLayerHotzone();
                    if (CurrentCursorState == CursorState.InspectingHotZone)
                        Retreat("ProcessScannerPromptInput: hot zone closed by Z");
                    return;
                }

                if (TryReadPressedNumber(out int number))
                {
                    handledThreatLayerInput = true;
                    if (TryApplyThreatLayerSelection(number, out int selectedTeamId))
                    {
                        cursorController?.PlayConfirmSfx();
                        RuntimeLog(PanelDialogController.ResolveDialogMessage(
                            "threat_layers.selected",
                            "Layers de Ameaca: time inspecionado -> <team_name> (<team_id>).",
                            new Dictionary<string, string>
                            {
                                { "team_name", TeamUtils.GetName((TeamId)selectedTeamId) },
                                { "team_id", selectedTeamId.ToString() }
                            }));
                    }
                    else
                    {
                        RuntimeLog(PanelDialogController.ResolveDialogMessage(
                            "threat_layers.invalid",
                            "Layers de Ameaca: time <team_id> nao disponivel nesta partida.",
                            new Dictionary<string, string>
                            {
                                { "team_id", number.ToString() }
                            }));
                    }
                }

                if (CurrentCursorState == CursorState.InspectingHotZone && !handledThreatLayerInput && WasAnyInputPressedThisFrame())
                {
                    TryCloseThreatLayerHotzone();
                    if (CurrentCursorState != CursorState.Neutral)
                        Retreat("ProcessScannerPromptInput: hot zone auto-dismiss by input");
                }
                return;
            }

            if (WasLetterPressedThisFrame('Z'))
            {
                HandleThreatLayersActionRequested();
                return;
            }
            return;
        }

        bool isMovementScannerState = CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado;
        bool isLandingScannerState = CurrentCursorState == CursorState.Pousando;
        bool isEmbarkScannerState = CurrentCursorState == CursorState.Embarcando;
        bool isDisembarkScannerState = CurrentCursorState == CursorState.Desembarcando;
        bool isMergeScannerState = CurrentCursorState == CursorState.Fundindo;
        bool isSupplyScannerState = CurrentCursorState == CursorState.Suprindo;
        if (isDisembarkScannerState)
        {
            ProcessDisembarkPromptInput();
            return;
        }

        if (isMergeScannerState)
        {
            ProcessMergePromptInput();
            return;
        }

        if (isSupplyScannerState)
        {
            ProcessSupplyPromptInput();
            return;
        }

        if (!isMovementScannerState && !isLandingScannerState && !isEmbarkScannerState)
            return;

        if (isMovementScannerState && IsTransferPromptActive())
        {
            ProcessTransferPromptInput();
            return;
        }

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

            if (WasLetterPressedThisFrame('D'))
            {
                HandleDisembarkActionRequested();
                return;
            }

            if (WasLetterPressedThisFrame('C'))
            {
                HandleCaptureActionRequested();
                return;
            }

            if (WasLetterPressedThisFrame('F'))
            {
                HandleMergeActionRequested();
                return;
            }

            if (WasLetterPressedThisFrame('S'))
            {
                HandleSupplyActionRequested();
                return;
            }

            if (WasLetterPressedThisFrame('T'))
            {
                HandleTransferActionRequested();
                return;
            }

            if (WasLetterPressedThisFrame('M'))
            {
                HandleMoveOnlyActionRequested();
                return;
            }

            return;
        }

        if (CurrentCursorState == CursorState.Embarcando &&
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
                        RuntimeLog($"Confirma embarque {shownIndex}? {label}\n(Enter=sim, ESC=voltar para ciclar)");
                    }
                }
            }
        }

        if (CurrentCursorState == CursorState.Pousando &&
            scannerPromptStep == ScannerPromptStep.LandingCycleOption)
        {
            if (TryReadPressedNumber(out int number))
            {
                int index = number - 1;
                PromptLandingOptionConfirmation(index, playConfirmSfx: true);
            }
        }

    }

    public void HandleAimActionRequested(bool automatedSelection = false, UnitManager preferredTarget = null)
    {
        bool canAim = availableSensorActionCodes.Contains('A');
        if (!canAim)
        {
            RuntimeLog("Pode Mirar (\"A\"): nao ha alvos validos agora.");
            LogScannerPanel();
            return;
        }

        BuildMirandoSelectionEntries();
        if (GetMirandoEntryCount() == 0)
        {
            RuntimeLog("Pode Mirar (\"A\"): nao ha alvos para listar.");
            LogScannerPanel();
            return;
        }

        cursorController?.PlayConfirmSfx();
        replayManager?.UpdateCurrentBufferSensorAction(SensorActionType.Attack, "AimActionRequested");
        if (preferredTarget == null)
            FocusFirstOptionForAction('A');
        suppressInitialMirandoAutoFocus = automatedSelection;
        EnterMirandoState();

        if (!automatedSelection)
            OnUnitAimOpened?.Invoke(selectedUnit);

        // O clique em um alvo e apenas uma preferencia de entrada. A lista e a validacao
        // continuam sendo as oficiais do PodeMirar, reconstruidas ao entrar em Mirando.
        if (preferredTarget != null)
            TryEnterMirandoConfirmForTarget(preferredTarget);
    }

    public bool TryInvokeSensorActionFromPointer(char actionCode)
    {
        if ((CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado) ||
            scannerPromptStep != ScannerPromptStep.AwaitingAction)
            return false;

        switch (char.ToUpperInvariant(actionCode))
        {
            case 'A': HandleAimActionRequested(); return true;
            case 'E': HandleEmbarkActionRequested(); return true;
            case 'D': HandleDisembarkActionRequested(); return true;
            case 'C': HandleCaptureActionRequested(); return true;
            case 'F': HandleMergeActionRequested(); return true;
            case 'S': HandleSupplyActionRequested(); return true;
            case 'T': HandleTransferActionRequested(); return true;
            case 'M': HandleMoveOnlyActionRequested(); return true;
            default: return false;
        }
    }

    private static readonly char[] SensorOptionNavigationOrder = { 'A', 'E', 'D', 'C', 'F', 'S', 'T', 'M' };
    private const char SensorOptionCancelCode = '\x1b';
    private char sensorOptionFocusCode;
    public bool SensorOptionCancelFocused => SensorOptionFocusCode == SensorOptionCancelCode;

    public char SensorOptionFocusCode
    {
        get
        {
            EnsureSensorOptionFocusIsValid();
            return sensorOptionFocusCode;
        }
    }

    public bool NavigateSensorOptionFocus(int delta)
    {
        if ((CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado) ||
            scannerPromptStep != ScannerPromptStep.AwaitingAction || delta == 0)
            return false;

        List<char> options = BuildNavigableSensorOptionCodes();
        if (options.Count <= 0)
            return false;

        int current = options.IndexOf(sensorOptionFocusCode);
        if (current < 0)
            current = 0;
        int step = delta > 0 ? 1 : -1;
        sensorOptionFocusCode = options[(current + step + options.Count) % options.Count];
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public void SetSensorOptionFocus(char actionCode)
    {
        char normalized = char.ToUpperInvariant(actionCode);
        if (BuildNavigableSensorOptionCodes().Contains(normalized))
            sensorOptionFocusCode = normalized;
    }

    public bool TryInvokeFocusedSensorOption()
    {
        EnsureSensorOptionFocusIsValid();
        return sensorOptionFocusCode != '\0' && sensorOptionFocusCode != SensorOptionCancelCode &&
               TryInvokeSensorActionFromPointer(sensorOptionFocusCode);
    }

    private void EnsureSensorOptionFocusIsValid()
    {
        List<char> options = BuildNavigableSensorOptionCodes();
        if (options.Count <= 0)
        {
            sensorOptionFocusCode = '\0';
            return;
        }
        if (!options.Contains(sensorOptionFocusCode))
            sensorOptionFocusCode = options[0];
    }

    private List<char> BuildNavigableSensorOptionCodes()
    {
        var options = new List<char>();

        // Capturador: se "Capturar" (C) estiver disponivel, ela vai pro topo tanto na navegacao
        // (setas) quanto no foco pre-selecionado (options[0], resolvido em EnsureSensorOptionFocusIsValid),
        // batendo com a ordem do painel OPÇÕES. Para os demais papeis, ordem padrao.
        bool capturerFirst = IsSelectedUnitPrimaryCapturer() &&
                             availableSensorActionCodes != null &&
                             availableSensorActionCodes.Contains('C');
        if (capturerFirst)
            options.Add('C');

        for (int i = 0; i < SensorOptionNavigationOrder.Length; i++)
        {
            char code = SensorOptionNavigationOrder[i];
            if (capturerFirst && code == 'C')
                continue; // ja adicionado no topo
            if (code == 'M' || (availableSensorActionCodes != null && availableSensorActionCodes.Contains(code)))
                options.Add(code);
        }
        options.Add(SensorOptionCancelCode);
        return options;
    }

    public bool TryInvokeInferredSensorActionByClickingSelectedUnit(Vector3Int clickedCell)
    {
        bool isMovementActionChoice = CurrentCursorState == CursorState.MoveuAndando ||
                                      CurrentCursorState == CursorState.MoveuParado;
        bool isAiming = CurrentCursorState == CursorState.Mirando;
        bool isEmbarking = CurrentCursorState == CursorState.Embarcando;
        if ((!isMovementActionChoice && !isAiming && !isEmbarking) || selectedUnit == null)
            return false;

        clickedCell.z = 0;

        if (isAiming)
            return TryHandleMirandoTargetClick(clickedCell);
        if (isEmbarking)
            return TryHandleEmbarkTargetClick(clickedCell);

        // Durante a escolha de acao pos-movimento, cliques no mapa so podem
        // inferir sensores (inclusive "Apenas Mover" na propria unidade) quando
        // o atalho contextual estiver explicitamente habilitado na partida.
        if (matchController == null || !matchController.AtalhoContextual)
            return false;

        if (scannerPromptStep != ScannerPromptStep.AwaitingAction)
            return false;

        Vector3Int unitCell = selectedUnit.CurrentCellPosition;
        unitCell.z = 0;
        if (clickedCell != unitCell)
            return TryBeginAimAtClickedTarget(clickedCell) ||
                   TryBeginEmbarkAtClickedTarget(clickedCell) ||
                   TryBeginDisembarkAtClickedCell(clickedCell) ||
                   TryBeginMergeAtClickedTarget(clickedCell) ||
                   TryBeginSupplyAtClickedTarget(clickedCell);

        // Prioridades contextuais para um segundo clique na propria unidade.
        // Capturar e uma acao inequivoca do hex atual e tem prioridade sobre M.
        if (availableSensorActionCodes.Contains('C'))
        {
            HandleCaptureActionRequested();
            return true;
        }

        // Para um supridor estacionado sobre uma construcao, o segundo clique na
        // propria unidade expressa a acao contextual Transferir. A disponibilidade
        // e os destinos continuam vindo integralmente do PodeTransferir.
        if (availableSensorActionCodes.Contains('T') && FindConstructionAtCell(unitCell) != null)
        {
            HandleTransferActionRequested();
            return IsTransferPromptActive();
        }

        // As demais acoes contextuais apontam para outro alvo (inimigo, transportador,
        // passageiro etc.). Portanto, depois da prioridade de Capturar, clicar novamente
        // na propria unidade expressa inequivocamente "Apenas Mover", mesmo que existam
        // outros sensores disponiveis no painel.
        HandleMoveOnlyActionRequested();
        return true;
    }

    private bool AllowsContextualPointerConfirmation()
    {
        return matchController != null && matchController.AtalhoContextual;
    }

    private bool TryBeginAimAtClickedTarget(Vector3Int clickedCell)
    {
        if (!availableSensorActionCodes.Contains('A'))
            return false;

        UnitManager target = FindUniqueValidAimTargetAtCell(clickedCell);
        if (target == null)
            return false;

        HandleAimActionRequested(automatedSelection: true, preferredTarget: target);
        return IsMirandoConfirmStep && IsCurrentMirandoTarget(target);
    }

    private bool TryHandleMirandoTargetClick(Vector3Int clickedCell)
    {
        if (combatExecutionInProgress)
            return false;

        UnitManager target = FindUniqueValidAimTargetAtCell(clickedCell);
        if (target == null)
            return false;

        if (scannerPromptStep == ScannerPromptStep.MirandoCycleTarget)
            return TryEnterMirandoConfirmForTarget(target);

        if (scannerPromptStep != ScannerPromptStep.MirandoConfirmTarget ||
            !IsCurrentMirandoTarget(target))
            return false;

        if (!AllowsContextualPointerConfirmation())
            return true;

        // Um segundo clique no mesmo alvo equivale ao Enter da confirmacao. Se CANCELAR
        // estava em foco pelo teclado, o clique explicito no alvo recupera CONFIRMAR.
        mirandoCancelFocused = false;
        mirandoConfirmButtonFocus = 0;
        HandleConfirmWithFeedback();
        return true;
    }

    private UnitManager FindUniqueValidAimTargetAtCell(Vector3Int clickedCell)
    {
        clickedCell.z = 0;
        UnitManager match = null;

        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[i];
            if (option == null || option.attackerUnit != selectedUnit || option.targetUnit == null)
                continue;

            Vector3Int targetCell = option.targetUnit.CurrentCellPosition;
            targetCell.z = 0;
            if (targetCell != clickedCell)
                continue;

            if (match != null && match != option.targetUnit)
                return null;

            match = option.targetUnit;
        }

        return match;
    }

    private bool TryEnterMirandoConfirmForTarget(UnitManager target)
    {
        if (CurrentCursorState != CursorState.Mirando || target == null)
            return false;

        for (int i = 0; i < cachedMirandoSelectionEntries.Count; i++)
        {
            MirandoSelectionEntry entry = cachedMirandoSelectionEntries[i];
            if (!entry.isValid || entry.TargetUnit != target || entry.AttackerUnit != selectedUnit)
                continue;

            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            scannerSelectedTargetIndex = i;
            mirandoCancelFocused = false;
            FocusCurrentMirandoTarget(logDetails: true, moveCursor: true);
            return TryConfirmScannerAttack();
        }

        return false;
    }

    private bool IsCurrentMirandoTarget(UnitManager target)
    {
        return target != null &&
               TryGetCurrentMirandoEntry(out MirandoSelectionEntry entry) &&
               entry.isValid &&
               entry.AttackerUnit == selectedUnit &&
               entry.TargetUnit == target;
    }

    private bool TryBeginEmbarkAtClickedTarget(Vector3Int clickedCell)
    {
        if (!availableSensorActionCodes.Contains('E'))
            return false;

        UnitManager transporter = FindUniqueValidEmbarkTargetAtCell(clickedCell);
        if (transporter == null)
            return false;

        HandleEmbarkActionRequested(transporter);
        return IsEmbarkConfirmStep && IsCurrentEmbarkTarget(transporter);
    }

    private bool TryHandleEmbarkTargetClick(Vector3Int clickedCell)
    {
        if (embarkExecutionInProgress)
            return false;

        UnitManager transporter = FindUniqueValidEmbarkTargetAtCell(clickedCell);
        if (transporter == null)
            return false;

        if (scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget)
            return TryEnterEmbarkConfirmForTarget(transporter);

        if (scannerPromptStep != ScannerPromptStep.EmbarkConfirmTarget ||
            !IsCurrentEmbarkTarget(transporter))
            return false;

        if (!AllowsContextualPointerConfirmation())
            return true;

        embarkCancelFocused = false;
        embarkConfirmButtonFocus = 0;
        HandleConfirmWithFeedback();
        return true;
    }

    private UnitManager FindUniqueValidEmbarkTargetAtCell(Vector3Int clickedCell)
    {
        clickedCell.z = 0;
        UnitManager match = null;
        for (int i = 0; i < cachedPodeEmbarcarTargets.Count; i++)
        {
            PodeEmbarcarOption option = cachedPodeEmbarcarTargets[i];
            if (option == null || (option.sourceUnit != null && option.sourceUnit != selectedUnit) || option.transporterUnit == null)
                continue;
            Vector3Int targetCell = option.transporterUnit.CurrentCellPosition;
            targetCell.z = 0;
            if (targetCell != clickedCell)
                continue;
            if (match != null && match != option.transporterUnit)
                return null;
            match = option.transporterUnit;
        }
        return match;
    }

    private bool TryEnterEmbarkConfirmForTarget(UnitManager transporter)
    {
        if (CurrentCursorState != CursorState.Embarcando || transporter == null)
            return false;
        for (int i = 0; i < cachedPodeEmbarcarTargets.Count; i++)
        {
            PodeEmbarcarOption option = cachedPodeEmbarcarTargets[i];
            if (option == null || (option.sourceUnit != null && option.sourceUnit != selectedUnit) || option.transporterUnit != transporter)
                continue;
            scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
            scannerSelectedEmbarkIndex = i;
            embarkCancelFocused = false;
            embarkConfirmButtonFocus = 0;
            FocusCurrentEmbarkTarget(logDetails: true, moveCursor: true);
            return TryConfirmScannerEmbark();
        }
        return false;
    }

    private bool IsCurrentEmbarkTarget(UnitManager transporter)
    {
        return transporter != null &&
               TryGetSelectedValidEmbarkOption(out PodeEmbarcarOption option, out _) &&
               (option.sourceUnit == null || option.sourceUnit == selectedUnit) &&
               option.transporterUnit == transporter;
    }

    private void HandleMoveOnlyActionRequested()
    {
        // M so finaliza uma acao depois que a FSM ja entrou no fluxo de
        // movimento e sensores. Em UnitSelected, ainda falta confirmar o
        // destino; finalizar ali gera replay sem destino e adianta tutorial.
        if (CurrentCursorState != CursorState.MoveuAndando &&
            CurrentCursorState != CursorState.MoveuParado)
        {
            RuntimeLog($"[Acao] Apenas Mover ignorado fora do fluxo de movimento: state={CurrentCursorState}");
            return;
        }

        // Tutorial: o "M" (apenas mover) tambem respeita a ordem de marcha.
        if (TutorialManager.IsMovementLockedByTutorial)
        {
            TutorialManager.ShowBlockedActionScold(TutorialScoldKind.MovementLocked);
            cursorController?.PlayErrorSfx();
            return;
        }

        // Tutorial (Attack Only): finalizar parado desperdicaria a acao — a ordem e mirar.
        if (TutorialManager.IsFinalizeInPlaceBlockedByTutorial)
        {
            TutorialManager.ShowBlockedActionScold(TutorialScoldKind.AttackOrdered);
            cursorController?.PlayErrorSfx();
            return;
        }

        // Tutorial (Hold Only): o "M" finaliza a unidade onde ela ja esta (nao
        // move ao cursor), entao nunca viola a ordem de segurar posicao — sair da
        // celula ja e barrado no confirm (HandleConfirmWhileUnitSelected).

        bool finished = TryFinalizeSelectedUnitActionFromDebug(completeHeldPosition: true);
        if (finished)
        {
            cursorController?.PlayDoneSfx();
            RuntimeLog("[Acao] Apenas Mover (\"M\") confirmado. Unidade finalizou sem atacar.");
            ResetScannerPromptState();
            return;
        }

        RuntimeLog("[Acao] Apenas Mover (\"M\") indisponivel no estado atual.");
    }

    private void HandleMergeActionRequested()
    {
        bool canMerge = availableSensorActionCodes.Contains('F');
        if (!canMerge)
        {
            string reason = !string.IsNullOrWhiteSpace(cachedPodeFundirReason)
                ? cachedPodeFundirReason
                : "nao ha unidades adjacentes do mesmo tipo.";
            RuntimeLog($"Pode Fundir (\"F\"): {reason}");
            LogScannerPanel();
            return;
        }

        EnsureMergeSensorSnapshot();
        if (cachedPodeFundirTargets == null || cachedPodeFundirTargets.Count <= 0)
        {
            string reason = !string.IsNullOrWhiteSpace(cachedPodeFundirReason)
                ? cachedPodeFundirReason
                : "nao ha candidatos validos para fusao.";
            RuntimeLog($"Pode Fundir (\"F\"): {reason}");
            LogScannerPanel();
            return;
        }

        replayManager?.UpdateCurrentBufferSensorAction(SensorActionType.Merge, "MergeActionRequested");
        EnterMergeStateFromSensors();
    }

    private void HandleLandingSensorRequested()
    {
        if (selectedUnit == null)
            return;
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return;

        BuildLandingOptionsFromCurrentState();

        if (cachedLandingOptions.Count == 0)
        {
            string reason = !string.IsNullOrWhiteSpace(landingOptionUnavailableReason)
                ? landingOptionUnavailableReason
                : "Sem opcoes de mudanca de camada neste contexto.";
            RuntimeLog($"Pode Mudar de Altitude (\"L\"): {reason}");
            LogScannerPanel();
            return;
        }

        cursorStateBeforePousando = CurrentCursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        replayManager?.UpdateCurrentBufferSensorAction(SensorActionType.Land, "LandActionRequested");
        Advance(CursorState.Pousando, "HandleLandingSensorRequested");
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

    private void HandleThreatLayersActionRequested()
    {
        bool totalWarActive = UnitRulesDefinition.IsTotalWarEnabled() || (matchController != null && matchController.EnableTotalWar);
        if (totalWarActive)
        {
            RuntimeLog("Layers de Ameaca (\"Z\"): indisponivel quando Guerra Total estiver ativa.");
            return;
        }

        if (CurrentCursorState != CursorState.Neutral)
        {
            RuntimeLog("Layers de Ameaca (\"Z\"): so disponivel em cursor neutro.");
            return;
        }

        if (!EnterThreatLayerTeamSelection())
        {
            RuntimeLog(PanelDialogController.ResolveDialogMessage(
                "threat_layers.unavailable",
                "Layers de Ameaca (\"Z\"): nenhum time valido para inspecionar."));
            return;
        }

        scannerPromptStep = ScannerPromptStep.ThreatLayerTeamSelect;
        Advance(CursorState.InspectingHotZone, "HandleThreatLayersActionRequested");
        cursorController?.PlayConfirmSfx();
        RuntimeLog(PanelDialogController.ResolveDialogMessage(
            "threat_layers.open",
            "Layers de Ameaca (\"Z\"): selecione o numero do time no helper. ESC para sair."));
    }

    public bool TryChangeAltitudeFromDebug(Domain targetDomain, HeightLevel targetHeight, out string message)
    {
        message = string.Empty;
        if (selectedUnit == null)
        {
            if (CurrentCursorState != CursorState.Neutral)
            {
                message = $"Nenhuma unidade selecionada e estado atual nao permite auto-selecao ({CurrentCursorState}).";
                return false;
            }

            if (!TryGetUnitUnderCursorForDebug(out UnitManager unitUnderCursor, out Vector3Int cursorCell, out message))
                return false;

            SetSelectedUnit(unitUnderCursor);
            Advance(CursorState.UnitSelected, "TryChangeAltitudeFromDebug(auto-select)");
            message = $"Unidade auto-selecionada no cursor {FormatMapCellWithZ(cursorCell)}.";
        }

        bool isMovementScannerState = CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado;
        bool isLandingScannerState = CurrentCursorState == CursorState.Pousando;
        bool isSelectionScannerState = CurrentCursorState == CursorState.UnitSelected;
        if (!isMovementScannerState && !isLandingScannerState && !isSelectionScannerState)
        {
            message = $"Estado atual nao permite mudanca de camada por comando ({CurrentCursorState}).";
            return false;
        }

        BuildLandingOptionsFromCurrentState();
        if (cachedLandingOptions.Count <= 0)
        {
            message = !string.IsNullOrWhiteSpace(landingOptionUnavailableReason)
                ? landingOptionUnavailableReason
                : "Sem opcoes de mudanca de camada neste contexto.";
            return false;
        }

        int optionIndex = -1;
        for (int i = 0; i < cachedLandingOptions.Count; i++)
        {
            LandingOption option = cachedLandingOptions[i];
            if (option.toDomain == targetDomain && option.toHeightLevel == targetHeight)
            {
                optionIndex = i;
                break;
            }
        }

        if (optionIndex < 0)
        {
            message = $"Transicao para {targetDomain}/{targetHeight} nao disponivel agora.";
            return false;
        }

        if (CurrentCursorState != CursorState.Pousando)
        {
            cursorStateBeforePousando = CurrentCursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
            Advance(CursorState.Pousando, "TryChangeAltitudeFromDebug");
            ClearCommittedPathVisual();
        }

        scannerSelectedLandingIndex = optionIndex;
        scannerPromptStep = ScannerPromptStep.LandingConfirmOption;
        LandingOption picked = cachedLandingOptions[optionIndex];
        RuntimeLog($"[LayerOperation][Debug] Confirmado: {picked.fromDomain}/{picked.fromHeightLevel} -> {picked.toDomain}/{picked.toHeightLevel} (action={picked.action})");
        landingExecutionInProgress = true;
        StartCoroutine(ExecuteLandingOptionSequence(picked, consumeAction: false));
        message = $"Mudanca de camada iniciada: {picked.label}.";
        return true;
    }

    private void HandleEmbarkActionRequested(UnitManager preferredTransporter = null)
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return;

        bool hasValid = cachedPodeEmbarcarTargets.Count > 0;
        if (!hasValid)
        {
            RuntimeLog("Pode Embarcar (\"E\"): nao ha transportador valido adjacente.");
            LogScannerPanel();
            return;
        }

        cursorController?.PlayConfirmSfx();
        replayManager?.UpdateCurrentBufferSensorAction(SensorActionType.Embark, "EmbarkActionRequested");
        // Mesma regra do Mirando: ao entrar em um submenu de sensor, oculta o preview de movimento.
        cursorStateBeforeEmbarcando = CurrentCursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        Advance(CursorState.Embarcando, "HandleEmbarkActionRequested");
        ClearCommittedPathVisual();
        scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
        scannerSelectedEmbarkIndex = 0;
        embarkCancelFocused = false;
        embarkConfirmButtonFocus = 0;

        if (preferredTransporter != null)
        {
            TryEnterEmbarkConfirmForTarget(preferredTransporter);
            return;
        }

        if (cachedPodeEmbarcarTargets.Count == 1)
        {
            scannerPromptStep = ScannerPromptStep.EmbarkConfirmTarget;
            FocusCurrentEmbarkTarget(logDetails: false, moveCursor: true);
            if (TryGetSelectedValidEmbarkOption(out PodeEmbarcarOption selected, out int shownIndex))
            {
                string label = !string.IsNullOrWhiteSpace(selected.displayLabel) ? selected.displayLabel : "transportador";
                RuntimeLog($"Confirma embarque {shownIndex}? {label}\n(Enter=sim, ESC=voltar para ciclar)");
            }
            return;
        }

        FocusCurrentEmbarkTarget(logDetails: true);
        LogEmbarkSelectionPanel();
    }

    private void LogLandingSelectionPanel()
    {
        if (cachedLandingOptions.Count <= 0)
        {
            RuntimeLog("[Landing] Sem opcoes.");
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

        RuntimeLog(text);
    }

    private void FocusFirstOptionForAction(char actionCode)
    {
        if (cursorController == null)
            return;

        switch (char.ToUpperInvariant(actionCode))
        {
            case 'A':
            {
                if (GetMirandoEntryCount() <= 0)
                    return;

                MirandoSelectionEntry firstAim = cachedMirandoSelectionEntries[0];
                if (firstAim.TargetUnit == null)
                    return;

                Vector3Int targetCell = firstAim.TargetUnit.CurrentCellPosition;
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
        if (CurrentCursorState != CursorState.Pousando)
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
        RuntimeLog($"[LayerOperation] Confirmado: {picked.fromDomain}/{picked.fromHeightLevel} -> {picked.toDomain}/{picked.toHeightLevel} (action={picked.action})");
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
        RuntimeLog($"Confirma \"{option.label}\"? (Enter=sim, ESC=nao)");
    }

    private System.Collections.IEnumerator ExecuteLandingOptionSequence(LandingOption option, bool consumeAction = true)
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
                        RuntimeLog("[Landing] Falha ao aplicar transicao para Air/Low.");
                        scannerPromptStep = ScannerPromptStep.LandingCycleOption;
                        LogLandingSelectionPanel();
                        yield break;
                    }

                    cursorController?.PlayConfirmSfx();
                    if (TryCompleteLayerOperationAfterExecution(consumeAction))
                        yield break;
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
                    if (TryCompleteLayerOperationAfterExecution(consumeAction))
                        yield break;
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
                        RuntimeLog("[Landing] Opcao Land fora de Air->Land detectada. Aplicando como transicao de camada.");
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
                        if (TryCompleteLayerOperationAfterExecution(consumeAction))
                            yield break;
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
                        RuntimeLog($"[Landing] {reason}");
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

                    // Camada de pouso derivada do hex (hidroaviao pousa na agua
                    // em Naval/Surface com o sprite proprio), nao Land/Surface fixo.
                    Tilemap landingBoardMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
                    Vector3Int landingCell = selectedUnit.CurrentCellPosition;
                    landingCell.z = 0;
                    AircraftOperationRules.ResolveGroundedLayerForCell(
                        selectedUnit, landingBoardMap, terrainDatabase, landingCell,
                        out Domain landedDomain, out HeightLevel landedHeight);
                    if (!selectedUnit.TrySetCurrentLayerMode(landedDomain, landedHeight))
                    {
                        RuntimeLog($"[Landing] Falha ao aplicar pouso ({landedDomain}/{landedHeight}).");
                        scannerPromptStep = ScannerPromptStep.LandingCycleOption;
                        LogLandingSelectionPanel();
                        yield break;
                    }

                    float vtolFxDuration = animationManager != null ? animationManager.PlayVtolLandingEffect(selectedUnit) : 0f;
                    landingDuration = Mathf.Max(landingDuration, vtolFxDuration);
                    if (landingDuration > 0f)
                        yield return new WaitForSeconds(landingDuration);

                    float postLandingDelay = GetEmbarkAfterForcedLandingDelay();
                    if (postLandingDelay > 0f)
                        yield return new WaitForSeconds(postLandingDelay);

                    cursorController?.PlayConfirmSfx();
                    if (TryCompleteLayerOperationAfterExecution(consumeAction))
                        yield break;
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
                    if (TryCompleteLayerOperationAfterExecution(consumeAction))
                        yield break;
                    break;
                }
            }

            if (CurrentCursorState == CursorState.Pousando)
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

    private bool TryCompleteLayerOperationAfterExecution(bool consumeAction)
    {
        if (consumeAction)
        {
            bool finished = TryFinalizeSelectedUnitActionFromDebug();
            if (finished)
            {
                cursorController?.PlayDoneSfx();
                ResetScannerPromptState();
                return true;
            }

            ExitLandingStateToMovement();
            RefreshSensorsForCurrentState();
            return false;
        }

        ExecuteAndReset("ExecuteLandingOptionSequence: debug keep turn reset");
        Advance(CursorState.UnitSelected, "ExecuteLandingOptionSequence: debug keep turn");
        scannerPromptStep = ScannerPromptStep.AwaitingAction;
        ClearSensorResults();
        PaintSelectedUnitMovementRange();
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }

        cursorController?.PlayDoneSfx();
        return false;
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

            if (unit.IsLayerChangeBlockedByForcedLock(mode.domain, mode.heightLevel, out string forcedLockReason))
            {
                if (string.IsNullOrWhiteSpace(unavailableReason))
                    unavailableReason = forcedLockReason;
                continue;
            }

            if (!CanUseLayerModeAtCurrentCell(unit, boardMap, terrainDatabase, unitCell, mode.domain, mode.heightLevel, out string blockReason))
            {
                if (string.IsNullOrWhiteSpace(unavailableReason))
                    unavailableReason = blockReason;
                continue;
            }

            bool isAirToGroundLanding = ShouldUseLandingActionForTransition(currentDomain, currentHeight, mode.domain, mode.heightLevel);
            bool isSubmarineEmerge = currentDomain == Domain.Submarine && currentHeight == HeightLevel.Submerged
                && mode.domain == Domain.Naval && mode.heightLevel == HeightLevel.Surface;

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

            if (isSubmarineEmerge)
            {
                PodeEmergirReport emergirReport = PodeEmergirSensor.Evaluate(unit, boardMap, terrainDatabase);
                if (!emergirReport.status)
                {
                    if (string.IsNullOrWhiteSpace(unavailableReason))
                        unavailableReason = !string.IsNullOrWhiteSpace(emergirReport.explicacao)
                            ? emergirReport.explicacao
                            : "Emersao indisponivel neste hex.";
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

        if (string.IsNullOrWhiteSpace(unavailableReason))
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
        if (!CanEndLayerTransitionAtCurrentCell(unit, boardMap, cell, targetDomain, targetHeight, out reason))
            return false;

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
            if (UnitHasAnyBlockedSkill(unit, construction.GetBlockedSkillsToEnter()))
            {
                reason = "Unidade possui skill bloqueada pela construcao para trocar de camada.";
                return false;
            }

            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        if (structure != null)
        {
            TryResolveTerrainAtCell(boardMap, terrainDb, cell, out TerrainTypeData terrainWithStructure);

            if (!StructureSupportsLayerMode(structure, targetDomain, targetHeight))
            {
                reason = $"Estrutura no hex nao suporta {targetDomain}/{targetHeight}.";
                return false;
            }

            bool usesAdditionalStructureMode = StructureSupportsAdditionalLayerMode(structure, targetDomain, targetHeight);
            if (!usesAdditionalStructureMode && !UnitPassesSkillRequirement(unit, structure.GetRequiredSkillsToEnter(terrainWithStructure)))
            {
                reason = "Unidade nao possui skill exigida pela estrutura para trocar de camada.";
                return false;
            }
            if (UnitHasAnyBlockedSkill(unit, structure.GetBlockedSkillsToEnter(terrainWithStructure)))
            {
                reason = "Unidade possui skill bloqueada pela estrutura para trocar de camada.";
                return false;
            }

            if (terrainWithStructure == null)
            {
                reason = "Terreno do hex nao encontrado para validar camada com estrutura.";
                return false;
            }

            if (!TerrainSupportsLayerMode(terrainWithStructure, targetDomain, targetHeight))
            {
                reason = $"Terreno no hex (com estrutura) nao suporta {targetDomain}/{targetHeight}.";
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
        if (UnitHasAnyBlockedSkill(unit, terrain.blockedSkills))
        {
            reason = "Unidade possui skill bloqueada pelo terreno para trocar de camada.";
            return false;
        }

        return true;
    }

    private static bool CanEndLayerTransitionAtCurrentCell(
        UnitManager unit,
        Tilemap boardMap,
        Vector3Int cell,
        Domain targetDomain,
        HeightLevel targetHeight,
        out string reason)
    {
        reason = string.Empty;
        if (UnitOccupancyRules.CanEndLayerTransitionAtCell(boardMap, cell, unit, targetDomain, targetHeight, out UnitManager blocker))
            return true;

        string blockerName = blocker != null && !string.IsNullOrWhiteSpace(blocker.UnitDisplayName) ? blocker.UnitDisplayName : "aliado";
        reason = $"Camada {targetDomain}/{targetHeight} ocupada por {blockerName}.";
        return false;
    }

    private static bool UnitPassesSkillRequirement(UnitManager unit, IReadOnlyList<SkillData> requiredSkills)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;
        if (unit == null)
            return false;

        bool hasAnyValidRequiredSkill = false;
        for (int i = 0; i < requiredSkills.Count; i++)
        {
            SkillData requiredSkill = requiredSkills[i];
            if (requiredSkill == null)
                continue;

            hasAnyValidRequiredSkill = true;
            if (unit.HasSkill(requiredSkill))
                return true;
        }

        if (!hasAnyValidRequiredSkill)
            return true;

        return false;
    }

    private static bool UnitHasAnyBlockedSkill(UnitManager unit, IReadOnlyList<SkillData> blockedSkills)
    {
        if (unit == null || blockedSkills == null || blockedSkills.Count <= 0)
            return false;

        for (int i = 0; i < blockedSkills.Count; i++)
        {
            SkillData blockedSkill = blockedSkills[i];
            if (blockedSkill == null)
                continue;

            if (unit.HasSkill(blockedSkill))
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

    private static bool StructureSupportsAdditionalLayerMode(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null || structure.aditionalDomainsAllowed == null)
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
                RuntimeLog($"[LayerOperation] {reason}");
                return false;
            }

            return true;
        }

        if (!selectedUnit.TrySetCurrentLayerMode(option.toDomain, option.toHeightLevel))
        {
            RuntimeLog(
                $"[LayerOperation] Falha ao aplicar camada destino {option.toDomain}/{option.toHeightLevel} " +
                $"(atual={beforeDomain}/{beforeHeight}).");
            return false;
        }

        RuntimeLog(
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
        if (CurrentCursorState != CursorState.Embarcando)
            return false;

        if (scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget)
        {
            if (!TryGetSelectedValidEmbarkOption(out PodeEmbarcarOption selected, out int shownIndex))
            {
                RuntimeLog("[Embarque] Selecao de embarque invalida.");
                return true;
            }

            scannerPromptStep = ScannerPromptStep.EmbarkConfirmTarget;
            // Mantem/atualiza a linha de preview durante a fase de confirmacao.
            FocusCurrentEmbarkTarget(logDetails: false, moveCursor: false);
            string label = !string.IsNullOrWhiteSpace(selected.displayLabel) ? selected.displayLabel : "transportador";
            RuntimeLog($"Confirma embarque {shownIndex}? {label}\n(Enter=sim, ESC=voltar para ciclar)");
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

    public bool TryExecuteAutomatedEmbarkReplayTarget(string transporterInstanceId, Vector3Int targetCell)
    {
        if (!TrySelectAutomatedEmbarkReplayTarget(transporterInstanceId, targetCell))
            return false;
        return ConfirmAutomatedEmbarkTarget();
    }

    public bool TrySelectAutomatedEmbarkReplayTarget(string transporterInstanceId, Vector3Int targetCell)
    {
        if (CurrentCursorState != CursorState.Embarcando)
            return false;
        if (cachedPodeEmbarcarTargets == null || cachedPodeEmbarcarTargets.Count <= 0)
            return false;

        int selectedIndex = -1;
        for (int i = 0; i < cachedPodeEmbarcarTargets.Count; i++)
        {
            PodeEmbarcarOption option = cachedPodeEmbarcarTargets[i];
            if (option == null || option.transporterUnit == null)
                continue;

            bool idMatch = !string.IsNullOrWhiteSpace(transporterInstanceId)
                && option.transporterUnit.InstanceId.ToString() == transporterInstanceId;
            Vector3Int optionCell = option.transporterUnit.CurrentCellPosition;
            optionCell.z = 0;
            Vector3Int desiredCell = targetCell;
            desiredCell.z = 0;
            bool cellMatch = optionCell == desiredCell;
            if (!idMatch && !cellMatch)
                continue;

            selectedIndex = i;
            break;
        }

        if (selectedIndex < 0)
            selectedIndex = 0;

        scannerSelectedEmbarkIndex = selectedIndex;
        scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
        FocusCurrentEmbarkTarget(logDetails: true, moveCursor: true);
        return TryConfirmScannerEmbark() && scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget;
    }

    public bool ConfirmAutomatedEmbarkTarget()
    {
        return CurrentCursorState == CursorState.Embarcando &&
               scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget &&
               TryConfirmScannerEmbark();
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
            RuntimeLog("[Embarque] Opcao invalida.");
            scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
            return;
        }

        UnitManager passenger = option.sourceUnit != null ? option.sourceUnit : selectedUnit;
        UnitManager transporter = option.transporterUnit;
        if (passenger == null || transporter == null || passenger != selectedUnit)
        {
            RuntimeLog("[Embarque] Opcao desatualizada para a unidade selecionada.");
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
            // Keep the passenger visually above the transporter during embark.
            passenger.SetTemporarySortingOrder(1000);
            passengerSortingRaised = true;
        }

        Advance(CursorState.EmbarcandoExecuting, "ExecuteEmbarkSequence: begin");

        try
        {
            if (transporter != null)
            {
                transporter.SetTemporarySortingOrder(999);
                transporterSortingRaised = true;
            }

            if (transporter != null && transporter.GetDomain() == Domain.Air)
            {
                AircraftOperationDecision landingDecision = AircraftOperationRules.Evaluate(
                    transporter,
                    movementTilemap,
                    terrainDatabase,
                    SensorMovementMode.MoveuParado);
                if (!landingDecision.available || landingDecision.action != AircraftOperationAction.Land)
                {
                    RuntimeLog(string.IsNullOrWhiteSpace(landingDecision.reason)
                        ? "[Embarque] Transportador aereo sem pouso valido."
                        : $"[Embarque] {landingDecision.reason}");
                    scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
                    Retreat("EmbarcandoExecuting: air landing abort");
                    ExitEmbarkStateToMovement();
                    RefreshSensorsForCurrentState();
                    yield break;
                }

                // Feedback do "forced landing": usa o SFX de movimento da unidade que pousou.
                PlayMovementStartSfx(transporter);
                RuntimeLog("[Embarque] Transportador pousou antes do embarque.");

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
                    RuntimeLog("[Embarque] Falha ao concluir pouso do transportador (Land/Surface).");
                    scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
                    Retreat("EmbarcandoExecuting: layer mode abort");
                    ExitEmbarkStateToMovement();
                    RefreshSensorsForCurrentState();
                    yield break;
                }

                float vtolFxDuration = animationManager != null ? animationManager.PlayVtolLandingEffect(transporter) : 0f;
                landingDuration = Mathf.Max(landingDuration, vtolFxDuration);
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
                RuntimeLog($"Pode Embarcar (\"E\"): {resultMessage}");
                scannerPromptStep = ScannerPromptStep.EmbarkCycleTarget;
                Retreat("EmbarcandoExecuting: embark failed");
                ExitEmbarkStateToMovement();
                RefreshSensorsForCurrentState();
                yield break;
            }

            cursorController?.PlayLoadSfx();
            RuntimeLog(resultMessage);
            yield return ExecutePostEmbarkAirTransporterTakeoff(transporter, movementTilemap);
            if (transporter != null)
                transporter.MarkAsActed();
            ResetScannerPromptState();
        }
        finally
        {
            embarkExecutionInProgress = false;
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

        RecordEmbarkReplayCommand(passenger, transporter, option.transporterSlotIndex);
        OnUnitEmbarked?.Invoke(passenger, transporter);

        string label = !string.IsNullOrWhiteSpace(option.displayLabel) ? option.displayLabel : transporter.name;
        message = $"Embarque concluido em: {label} | custo={embarkCost} | autonomia {fuelBeforeEmbark}->{passenger.CurrentFuel}";
        return true;
    }

    private System.Collections.IEnumerator ExecutePostEmbarkAirTransporterTakeoff(UnitManager transporter, Tilemap boardMap)
    {
        if (transporter == null || boardMap == null)
            yield break;
        if (!transporter.TryGetUnitData(out UnitData data) || data == null || !data.IsAircraft())
            yield break;
        if (transporter.GetDomain() == Domain.Air && !transporter.IsAircraftGrounded)
            yield break;

        PodeDecolarReport report = PodeDecolarSensor.Evaluate(transporter, boardMap, terrainDatabase);
        // O embarque em um transportador aereo possui uma decolagem operacional
        // propria: depois que o passageiro confirma o embarque, o transportador
        // volta ao ar no mesmo hex e na mesma rodada. As opcoes 0/1/full do
        // PodeDecolar descrevem a decolagem iniciada pelo movimento normal da
        // aeronave; usa-las aqui fazia o Hidroaviao permanecer no solo em estrada,
        // onde sua decolagem normal anuncia corrida obrigatoria de 1 hex.
        bool canTakeoffAfterEmbark = report != null && report.status;
        if (!canTakeoffAfterEmbark)
        {
            RuntimeLog(report != null && !string.IsNullOrWhiteSpace(report.explicacao)
                ? $"[Embarque] Transportador permanece no solo apos embarque: {report.explicacao}"
                : "[Embarque] Transportador permanece no solo apos embarque: decolagem indisponivel.");
            yield break;
        }

        if (!AircraftOperationRules.TryApplyOperation(
                transporter,
                boardMap,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                out AircraftOperationDecision takeoffDecision))
        {
            RuntimeLog(string.IsNullOrWhiteSpace(takeoffDecision.reason)
                ? "[Embarque] Falha ao decolar transportador apos embarque."
                : $"[Embarque] Transportador permanece no solo apos embarque: {takeoffDecision.reason}");
            yield break;
        }

        transporter.MarkTookOffRecently();
        PlayMovementStartSfx(transporter);
        RuntimeLog("[Embarque] Transportador decolou apos concluir o embarque.");

        float takeoffFxDuration = animationManager != null ? animationManager.PlayVtolLandingEffect(transporter) : 0f;
        if (takeoffFxDuration > 0f)
            yield return new WaitForSeconds(takeoffFxDuration);
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
            RuntimeLog("Sem opcoes de embarque para listar.");
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
        RuntimeLog(text);
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
            RuntimeLog(
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
            : (invalidOption != null ? $"hex {FormatMapCell(invalidOption.evaluatedCell)}" : "invalido");
        string reason = invalidOption != null && !string.IsNullOrWhiteSpace(invalidOption.reason)
            ? invalidOption.reason
            : "motivo nao informado";
        RuntimeLog(
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
        int total = GetMirandoEntryCount();
        if (total == 0)
        {
            RuntimeLog("Sem alvos para mirar.");
            return;
        }

        int validCount = cachedPodeMirarTargets.Count;
        int invalidCount = Mathf.Max(0, total - validCount);
        string text = $"Alvos de mira: {total} (validos={validCount}, invalidos={invalidCount})\n";
        text += "Mirando: setas alternam entre alvos validos e invalidos\n";
        for (int i = 0; i < total; i++)
        {
            MirandoSelectionEntry entry = cachedMirandoSelectionEntries[i];
            if (entry.isValid)
            {
                PodeMirarTargetOption option = entry.validOption;
                string label = option != null && !string.IsNullOrWhiteSpace(option.displayLabel)
                    ? option.displayLabel
                    : (option != null && option.targetUnit != null ? option.targetUnit.name : "alvo");

                string revide = option != null && option.defenderCanCounterAttack ? "sim" : "nao";
                text += $"{i + 1}. [OK] {label} | revide: {revide}\n";
            }
            else
            {
                PodeMirarInvalidOption invalid = entry.invalidOption;
                string label = invalid != null && invalid.targetUnit != null ? invalid.targetUnit.name : "alvo invalido";
                string reason = invalid != null && !string.IsNullOrWhiteSpace(invalid.reason) ? invalid.reason : "motivo nao informado";
                text += $"{i + 1}. [X] {label} | {reason}\n";
            }
        }

        text += ">> Enter confirma alvo valido | ESC volta";
        RuntimeLog(text);
    }

    private void LogAttackConfirmationPrompt(MirandoSelectionEntry entry, int shownIndex)
    {
        if (entry.isValid)
        {
            PodeMirarTargetOption option = entry.validOption;
            string label = option != null && !string.IsNullOrWhiteSpace(option.displayLabel)
                ? option.displayLabel
                : (option != null && option.targetUnit != null ? option.targetUnit.name : $"alvo {shownIndex}");
            RuntimeLog($"Confirma alvo {shownIndex}? {label}\n(Enter=sim, ESC=voltar para ciclar)");
            return;
        }

        PodeMirarInvalidOption invalid = entry.invalidOption;
        string invalidLabel = invalid != null && invalid.targetUnit != null ? invalid.targetUnit.name : $"alvo {shownIndex}";
        string reason = invalid != null && !string.IsNullOrWhiteSpace(invalid.reason) ? invalid.reason : "motivo nao informado";
        RuntimeLog($"Alvo {shownIndex} invalido: {invalidLabel}\nMotivo: {reason}\n(Enter=toca erro, ESC=voltar para ciclar)");
    }

    private void MoveCursorToTarget(UnitManager targetUnit)
    {
        if (targetUnit == null || cursorController == null)
            return;

        Vector3Int targetCell = targetUnit.CurrentCellPosition;
        targetCell.z = 0;
        cursorController.SetCell(targetCell, playMoveSfx: false);
    }

    private static string ResolveAimInvalidDialogMessage(PodeMirarInvalidOption invalidOption)
    {
        string fallback = invalidOption != null && !string.IsNullOrWhiteSpace(invalidOption.reason)
            ? invalidOption.reason
            : "Aim: alvo invalido";
        string id = invalidOption != null && !string.IsNullOrWhiteSpace(invalidOption.reasonId)
            ? invalidOption.reasonId
            : PodeMirarInvalidOption.ReasonIdGeneric;

        string unitName = string.Empty;
        string domainName = string.Empty;
        string heightName = string.Empty;
        UnitManager attacker = invalidOption != null ? invalidOption.attackerUnit : null;
        if (attacker != null)
        {
            unitName = ResolveUnitRuntimeName(attacker);
            domainName = attacker.GetDomain().ToString();
            heightName = attacker.GetHeightLevel().ToString();
        }

        return PanelDialogController.ResolveDialogMessage(
            id,
            fallback,
            new Dictionary<string, string>
            {
                { "unit", unitName },
                { "domain", domainName },
                { "height", heightName }
            });
    }

    private bool TryConfirmScannerAttack()
    {
        if (CurrentCursorState != CursorState.Mirando)
            return false;
        if (combatExecutionInProgress)
            return true;

        if (scannerPromptStep == ScannerPromptStep.MirandoCycleTarget)
        {
            if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= GetMirandoEntryCount())
                return true;

            MirandoSelectionEntry cycleEntry = cachedMirandoSelectionEntries[scannerSelectedTargetIndex];
            if (!cycleEntry.isValid)
            {
                string reason = cycleEntry.invalidOption != null && !string.IsNullOrWhiteSpace(cycleEntry.invalidOption.reason)
                    ? cycleEntry.invalidOption.reason
                    : "alvo invalido para este ataque.";
                RuntimeLog($"[Mirando] Alvo invalido. {reason}");
                PushPanelUnitMessage(ResolveAimInvalidDialogMessage(cycleEntry.invalidOption), 2.6f);
                cursorController?.PlayErrorSfx();
                return false;
            }

            if (cycleEntry.TargetUnit != null)
                RecordCinematicConfirm(cycleEntry.TargetUnit.CurrentCellPosition);

            EnterMirandoConfirmStep();
            return true;
        }

        if (scannerPromptStep != ScannerPromptStep.MirandoConfirmTarget)
            return false;

        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= GetMirandoEntryCount())
        {
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            scannerSelectedTargetIndex = 0;
            FocusCurrentMirandoTarget(logDetails: true);
            LogTargetSelectionPanel();
            return true;
        }

        MirandoSelectionEntry entry = cachedMirandoSelectionEntries[scannerSelectedTargetIndex];
        if (!entry.isValid)
        {
            string reason = entry.invalidOption != null && !string.IsNullOrWhiteSpace(entry.invalidOption.reason)
                ? entry.invalidOption.reason
                : "alvo invalido para este ataque.";
            RuntimeLog($"[Mirando] Alvo invalido. {reason}");
            PushPanelUnitMessage(ResolveAimInvalidDialogMessage(entry.invalidOption), 2.6f);
            cursorController?.PlayErrorSfx();
            return false;
        }

        PodeMirarTargetOption option = entry.validOption;
        if (option == null || option.attackerUnit == null || option.targetUnit == null)
        {
            RuntimeLog("Falha ao confirmar ataque: opcao invalida.");
            PushPanelUnitMessage("Aim: falha ao confirmar", 2.4f);
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            scannerSelectedTargetIndex = 0;
            FocusCurrentMirandoTarget(logDetails: true);
            LogTargetSelectionPanel();
            return true;
        }

        CombatResolutionResult combat = ResolveCombatFromSelectedOption(option);
        RuntimeLog(combat.trace);
        if (!combat.success)
        {
            RuntimeLog("[Combate] Falha ao resolver combate. Retornando para selecao de alvo.");
            PushPanelUnitMessage("Combate: falha ao resolver", 2.6f);
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            FocusCurrentMirandoTarget(logDetails: true);
            LogTargetSelectionPanel();
            return true;
        }

        if (option.targetUnit != null)
            RecordCinematicConfirm(option.targetUnit.CurrentCellPosition);

        WeaponTrajectoryType trajectory = ResolveSelectedTrajectory(option);
        StartCoroutine(ExecuteConfirmedAttackSequence(option, trajectory, combat));
        return true;
    }

    public bool TryExecuteAutomatedAttackReplayTarget(string targetInstanceId, Vector3Int targetCell)
    {
        if (CurrentCursorState != CursorState.Mirando)
            return false;
        if (GetMirandoEntryCount() <= 0)
            return false;

        int selectedIndex = -1;
        bool hasTargetId = !string.IsNullOrWhiteSpace(targetInstanceId);
        for (int i = 0; i < GetMirandoEntryCount(); i++)
        {
            MirandoSelectionEntry entry = cachedMirandoSelectionEntries[i];
            if (!entry.isValid || entry.validOption == null || entry.validOption.targetUnit == null)
                continue;

            UnitManager target = entry.validOption.targetUnit;
            bool idMatch = hasTargetId && target.InstanceId.ToString() == targetInstanceId;
            if (hasTargetId)
            {
                if (idMatch)
                {
                    selectedIndex = i;
                    break;
                }
                continue;
            }

            Vector3Int optionCell = target.CurrentCellPosition;
            optionCell.z = 0;
            Vector3Int desiredCell = targetCell;
            desiredCell.z = 0;
            bool cellMatch = optionCell == desiredCell;
            if (!cellMatch)
                continue;

            selectedIndex = i;
            break;
        }

        if (selectedIndex < 0)
            return false;

        scannerSelectedTargetIndex = selectedIndex;
        // Semantica visual IA: cursor so foca o alvo final apos a selecao.
        FocusCurrentMirandoTarget(logDetails: false, moveCursor: true);
        EnterMirandoConfirmStep();
        return TryConfirmScannerAttack();
    }

    public bool TrySelectMirandoTargetFromPointer(int index)
    {
        if (CurrentCursorState != CursorState.Mirando || index < 0 || index >= GetMirandoEntryCount())
            return false;
        // Clicar escolhe o alvo e avanca para a confirmacao com feedback de cursor.
        // (HandleConfirmWithFeedback), inclusive o SFX — e so um atalho pra funcao que ja existia.
        scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
        scannerSelectedTargetIndex = index;
        mirandoCancelFocused = false;
        FocusCurrentMirandoTarget(logDetails: true, moveCursor: true);
        HandleConfirmWithFeedback();
        return true;
    }

    private bool mirandoCancelFocused;
    private int mirandoConfirmButtonFocus;
    public bool MirandoCancelFocused => mirandoCancelFocused;
    public int MirandoConfirmButtonFocus => mirandoConfirmButtonFocus;
    public bool IsMirandoConfirmStep => CurrentCursorState == CursorState.Mirando &&
                                        scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget;

    // So o passo de CONFIRMAR ataque usa este helper (alterna CONFIRMAR/CANCELAR). A navegacao de
    // alvos (escolher alvo) vai pelo fluxo normal do cursor em TryResolveMirandoCursorMove.
    public bool NavigateMirandoHelperFocus(int delta)
    {
        if (CurrentCursorState != CursorState.Mirando || delta == 0)
            return false;
        if (scannerPromptStep != ScannerPromptStep.MirandoConfirmTarget)
            return false;

        mirandoConfirmButtonFocus = (mirandoConfirmButtonFocus + (delta > 0 ? 1 : -1) + 2) % 2;
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    private bool embarkCancelFocused;
    private int embarkConfirmButtonFocus;
    public bool EmbarkCancelFocused => embarkCancelFocused;
    public int EmbarkConfirmButtonFocus => embarkConfirmButtonFocus;
    public bool IsEmbarkConfirmStep => CurrentCursorState == CursorState.Embarcando &&
                                       scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget;

    public bool NavigateEmbarkHelperFocus(int delta)
    {
        if (!IsEmbarkConfirmStep || delta == 0)
            return false;
        embarkConfirmButtonFocus = (embarkConfirmButtonFocus + (delta > 0 ? 1 : -1) + 2) % 2;
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public void SetEmbarkConfirmFocus(int index)
    {
        if (IsEmbarkConfirmStep)
            embarkConfirmButtonFocus = Mathf.Clamp(index, 0, 1);
    }

    public bool TrySelectEmbarkTargetFromPointer(int index)
    {
        if (CurrentCursorState != CursorState.Embarcando ||
            index < 0 || index >= cachedPodeEmbarcarTargets.Count)
            return false;
        PodeEmbarcarOption option = cachedPodeEmbarcarTargets[index];
        return option != null && TryEnterEmbarkConfirmForTarget(option.transporterUnit);
    }

    // -------------------------------------------------------------------------
    // API de replay para navegação em listas de sensor (genérica)
    // Usada por: Mirando (ataque). Futuramente: Embark, Supply, Transfer...
    // -------------------------------------------------------------------------

    /// <summary>Quantidade de entradas na lista de alvos do Mirando atual.</summary>
    public int GetMirandoCountForReplay() => GetMirandoEntryCount();

    /// <summary>Índice atualmente selecionado na lista do Mirando.</summary>
    public int GetMirandoCurrentIndexForReplay() => scannerSelectedTargetIndex;

    /// <summary>
    /// Avança um passo na lista do Mirando e foca o novo alvo (highlight visual).
    /// Chame repetidamente com sensorListNavDelay entre chamadas para simular navegação.
    /// </summary>
    public bool StepMirandoForReplay()
    {
        int count = GetMirandoEntryCount();
        if (CurrentCursorState != CursorState.Mirando || count == 0) return false;
        scannerSelectedTargetIndex = (scannerSelectedTargetIndex + 1 + count) % count;
        FocusCurrentMirandoTarget(logDetails: false, moveCursor: true);
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    /// <summary>
    /// Retorna o índice do alvo na lista atual, ou -1 se não encontrado.
    /// Usado pelo replay para saber quantos passos de navegação são necessários.
    /// </summary>
    public int FindMirandoTargetIndexForReplay(string targetInstanceId, Vector3Int targetCell)
    {
        targetCell.z = 0;
        bool hasTargetId = !string.IsNullOrWhiteSpace(targetInstanceId);
        for (int i = 0; i < GetMirandoEntryCount(); i++)
        {
            MirandoSelectionEntry entry = cachedMirandoSelectionEntries[i];
            if (!entry.isValid || entry.validOption == null || entry.validOption.targetUnit == null)
                continue;
            UnitManager target = entry.validOption.targetUnit;
            bool idMatch = hasTargetId && target.InstanceId.ToString() == targetInstanceId;
            if (hasTargetId)
            {
                if (idMatch)
                    return i;
                continue;
            }

            Vector3Int optionCell = target.CurrentCellPosition;
            optionCell.z = 0;
            if (optionCell == targetCell)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Entra no confirm step do alvo atualmente selecionado — exibe a linha de mira/preview.
    /// Chame antes do beforeConfirmDelay.
    /// </summary>
    public void EnterMirandoConfirmStepForReplay() => EnterMirandoConfirmStep();

    /// <summary>
    /// Primeira metade do ataque automatizado: seleciona o alvo e exibe a linha de mira.
    /// Chame ConfirmAutomatedAttackTarget() depois do delay visual desejado.
    /// </summary>
    public bool SelectAutomatedAttackTarget(string targetInstanceId, Vector3Int targetCell)
    {
        if (CurrentCursorState != CursorState.Mirando)
            return false;
        if (GetMirandoEntryCount() <= 0)
            return false;

        int selectedIndex = -1;
        bool hasTargetId = !string.IsNullOrWhiteSpace(targetInstanceId);
        for (int i = 0; i < GetMirandoEntryCount(); i++)
        {
            MirandoSelectionEntry entry = cachedMirandoSelectionEntries[i];
            if (!entry.isValid || entry.validOption == null || entry.validOption.targetUnit == null)
                continue;

            UnitManager target = entry.validOption.targetUnit;
            bool idMatch = hasTargetId && target.InstanceId.ToString() == targetInstanceId;
            if (hasTargetId)
            {
                if (idMatch)
                {
                    selectedIndex = i;
                    break;
                }
                continue;
            }

            Vector3Int optionCell = target.CurrentCellPosition;
            optionCell.z = 0;
            Vector3Int desiredCell = targetCell;
            desiredCell.z = 0;
            bool cellMatch = optionCell == desiredCell;
            if (!cellMatch)
                continue;

            selectedIndex = i;
            break;
        }

        if (selectedIndex < 0)
            return false;

        scannerSelectedTargetIndex = selectedIndex;
        FocusCurrentMirandoTarget(logDetails: false, moveCursor: true);
        EnterMirandoConfirmStep();
        return true;
    }

    /// <summary>
    /// Segunda metade do ataque automatizado: confirma o alvo ja selecionado por SelectAutomatedAttackTarget.
    /// </summary>
    public bool ConfirmAutomatedAttackTarget()
    {
        return TryConfirmScannerAttack();
    }

    private IEnumerator ExecuteConfirmedAttackSequence(
        PodeMirarTargetOption option,
        WeaponTrajectoryType attackerTrajectory,
        CombatResolutionResult combat)
    {
        combatExecutionInProgress = true;
        Advance(CursorState.AttackingExecuting, "ExecuteConfirmedAttackSequence: begin");

        try
        {

        // Esconde a linha de mira antes de iniciar audio/projeteis do combate.
        SetMirandoPreviewVisible(false);
        SetMirandoSpotterPreviewsVisible(false);
        UnitManager attacker = option != null ? option.attackerUnit : null;
        UnitManager defender = option != null ? option.targetUnit : null;
        bool attackerVisibleToDefender = attacker != null && defender != null
            && (matchController == null
                || matchController.IsUnitVisibleForTeamNoCache(attacker, defender.TeamId));
        if (defender != null && matchController != null)
            AIIntelLedger.RecordVisibleContactsForTeam(
                defender.TeamId, matchController.CurrentTurn, matchController);

        float audioDuration = PlayCombatAttackSfx(attackerTrajectory, defender);
        float waitDuration = audioDuration;

        if (attackerTrajectory == WeaponTrajectoryType.Parabolic && animationManager != null && defender != null)
        {
            PanelDialogController.ClearExternalText();
            float effectDuration = animationManager.PlayRangedAttackDefenderEffect(defender, audioDuration);
            waitDuration = Mathf.Max(waitDuration, effectDuration);
        }

        if (waitDuration > 0f)
            yield return new WaitForSeconds(waitDuration);

        int defenderHpBeforeResolution = defender != null ? Mathf.Max(0, defender.CurrentHP) : 0;
        int attackerHpBeforeResolution = attacker != null ? Mathf.Max(0, attacker.CurrentHP) : 0;
        Dictionary<int, int> embarkedHpBeforeById = CaptureEmbarkedHpSnapshot(attacker, defender);
        List<JogadasManager.RuntimeCargoSnapshot> combatCargoSnapshot =
            JogadasManager.CaptureCombatCargoSnapshot(attacker, defender);

        yield return ExecuteCombatProjectileExchange(option, attackerTrajectory, combat.counterExecuted);
        ApplyPostHitForcedLayerEffects(option, combat, attackerHpBeforeResolution, defenderHpBeforeResolution);
        ApplyPostAttackSelfEmergeEffect(combat);
        ApplyPendingCombatHp(combat);

        // Jornal do Comandante — tiro da nevoa: o defensor foi atingido por um
        // atacante que o time dele NAO via. Fog-honesto: registra a vitima, o
        // dano e a celula DELA; a posicao do atacante nunca viaja no evento.
        if (defender != null && attacker != null &&
            defender.TeamId != attacker.TeamId &&
            defender.TeamId != TeamId.Neutral &&
            !attackerVisibleToDefender &&
            matchController != null)
        {
            int fogFireDamage = Mathf.Max(0, defenderHpBeforeResolution - Mathf.Max(0, combat.defenderHpAfter));
            if (fogFireDamage > 0)
            {
                Vector3Int fogFireCell = defender.CurrentCellPosition;
                fogFireCell.z = 0;
                matchController.ReportTurnBriefingEvent(
                    defender.TeamId,
                    MatchController.TurnBriefingCategory.FogFire,
                    ResolveDebugUnitName(defender),
                    $"atingida (−{fogFireDamage} PV) por atacante não identificado",
                    fogFireCell);
            }
        }
        JogadasManager.SetUltimoAtaqueResultado(
            attacker,
            defender,
            attackerHpBeforeResolution,
            defenderHpBeforeResolution,
            option != null && option.weapon != null
                ? option.weapon.WeaponCategory
                : WeaponCategory.AntiInfantaria,
            attackerTrajectory,
            attackerVisibleToDefender,
            combatCargoSnapshot);
        planningManager?.NotifyUnitInvolvedInCombat(attacker);
        planningManager?.NotifyUnitInvolvedInCombat(defender);
        RecordAttackReplayCommand(
            attacker,
            defender,
            attackerHpBeforeResolution,
            defenderHpBeforeResolution,
            embarkedHpBeforeById);
        yield return ExecuteDeathResolutionIfNeeded(combat);

        cursorController?.PlayDoneSfx();
        OnAttackResolved?.Invoke(attacker, defender);
        bool finalized = TryFinalizeSelectedUnitActionFromDebug();
        if (!finalized)
        {
            ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: true);
            if (replayManager != null)
            {
                string attackerId = attacker != null ? attacker.InstanceId.ToString() : "null";
                string defenderId = defender != null ? defender.InstanceId.ToString() : "null";
                replayManager.PromoteCurrentBuffer($"UnitAction: attack {attackerId}->{defenderId}");
            }
        }
        ResetScannerPromptState();

        } // try
        finally
        {
            combatExecutionInProgress = false;
        }
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
        if (cursorController != null)
            return cursorController.PlayCombatAttackSfx(trajectory, 1f);

        return 0f;
    }

    private float PlayWeaponShot(UnitManager shooter, UnitManager target, WeaponData weapon, WeaponTrajectoryType trajectory)
    {
        if (shooter == null || target == null)
            return 0f;

        cursorController?.PlayWeaponFireSfx(weapon, 1f);

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
            if (defenderHpAfter <= 0)
                combat.defenderUnit.MarkDiedBy(combat.attackerUnit);
            ApplyEmbarkedCascadeFromDirectHit(combat.defenderUnit, defenderHpBefore, defenderHpAfter);
        }

        if (combat.attackerUnit != null)
        {
            int attackerHpBefore = Mathf.Max(0, combat.attackerUnit.CurrentHP);
            int attackerHpAfter = Mathf.Max(0, combat.attackerHpAfter);
            combat.attackerUnit.SetCurrentHP(attackerHpAfter);
            if (attackerHpAfter <= 0)
                combat.attackerUnit.MarkDiedBy(combat.defenderUnit);
            ApplyEmbarkedCascadeFromDirectHit(combat.attackerUnit, attackerHpBefore, attackerHpAfter);
        }
    }

    private void ApplyPostHitForcedLayerEffects(
        PodeMirarTargetOption option,
        CombatResolutionResult combat,
        int attackerHpBeforeResolution,
        int defenderHpBeforeResolution)
    {
        if (!combat.success || option == null)
            return;

        UnitManager defender = combat.defenderUnit;
        if (defender != null &&
            combat.defenderHpAfter > 0 &&
            combat.defenderHpAfter < Mathf.Max(0, defenderHpBeforeResolution))
        {
            TryApplyForcedLayerEffectFromWeapon(defender, option.weapon);
        }

        UnitManager attacker = combat.attackerUnit;
        if (combat.counterExecuted &&
            attacker != null &&
            combat.attackerHpAfter > 0 &&
            combat.attackerHpAfter < Mathf.Max(0, attackerHpBeforeResolution))
        {
            TryApplyForcedLayerEffectFromWeapon(attacker, option.defenderCounterWeapon);
        }
    }

    // Unidades como o submarino emergem ao atacar (perdem stealth e ficam
    // travadas na camada revelada por emergeAfterAttackTurns, mesma janela do
    // Layer Force After Hit). So mergulham de volta quando o lock expira e
    // elas se movem de novo. Roda independente de acerto/erro: o gatilho e a
    // propria acao de atacar, nao o resultado do combate.
    private void ApplyPostAttackSelfEmergeEffect(CombatResolutionResult combat)
    {
        if (!combat.success)
            return;

        TryApplySelfEmergeAfterFiring(combat.attackerUnit, combat.attackerHpAfter);
        if (combat.counterExecuted)
            TryApplySelfEmergeAfterFiring(combat.defenderUnit, combat.defenderHpAfter);
    }

    private void TryApplySelfEmergeAfterFiring(UnitManager firingUnit, int hpAfterCombat)
    {
        if (firingUnit == null || firingUnit.IsEmbarked || hpAfterCombat <= 0)
            return;
        if (!firingUnit.TryGetUnitData(out UnitData firingData) || firingData == null || !firingData.emergesToAttack)
            return;

        Domain currentDomain = firingUnit.GetDomain();
        HeightLevel currentHeight = firingUnit.GetHeightLevel();
        Domain revealDomain = firingData.emergeAfterAttackDomain;
        HeightLevel revealHeight = firingData.emergeAfterAttackHeight;
        if (currentDomain == revealDomain && currentHeight == revealHeight)
            return;
        if (!firingUnit.SupportsLayerMode(revealDomain, revealHeight))
            return;

        Tilemap selfEmergeBoardMap = terrainTilemap != null ? terrainTilemap : firingUnit.BoardTilemap;
        Vector3Int selfEmergeCell = firingUnit.CurrentCellPosition;
        selfEmergeCell.z = 0;
        if (!PodeEmergirSensor.CanApplyLayerTransitionAtCell(
                firingUnit, selfEmergeBoardMap, terrainDatabase, selfEmergeCell,
                revealDomain, revealHeight, out string selfEmergeBlockReason))
        {
            // Cobre o revide: o ataque proprio ja e barrado pelo gate do
            // PodeMirarSensor, mas o contra-ataque nao passa por mira.
            ApplyPendingForcedLayerLock(firingUnit, revealDomain, revealHeight, firingData.emergeAfterAttackTurns, selfEmergeBlockReason);
            return;
        }

        if (!firingUnit.TrySetCurrentLayerMode(revealDomain, revealHeight))
            return;

        // Mesmo som de movimento da unidade (por MovementCategory), tocado ao
        // emergir para reforcar que ela mudou de camada, nao so de sprite.
        cursorController?.PlayUnitMovementSfx(firingUnit.GetMovementCategory());

        // Iguala a janela de exposicao da emersao voluntaria (atacar) a da
        // emersao forcada por dano (Layer Force After Hit): sem o lock, o
        // submarino mergulhava de volta no proprio proximo movimento (so 1
        // rodada de exposicao), enquanto ser atingido prendia por 2 rodadas —
        // atacar ficava mais seguro do que ser pego.
        firingUnit.SetForcedLayerLock(revealDomain, revealHeight, firingData.emergeAfterAttackTurns);

        string revealMessage = PanelDialogController.ResolveDialogMessage(
            "layer.revealed.after_attack",
            "<unit> emerge para <domain>/<height> apos atacar (<turns> turno(s)).",
            new Dictionary<string, string>
            {
                { "unit", ResolveDebugUnitName(firingUnit) },
                { "domain", revealDomain.ToString() },
                { "height", revealHeight.ToString() },
                { "turns", firingData.emergeAfterAttackTurns.ToString() }
            });
        PushPanelUnitMessage(revealMessage, 2.6f);
    }

    private void TryApplyForcedLayerEffectFromWeapon(UnitManager target, WeaponData weapon)
    {
        if (target == null || weapon == null || target.IsEmbarked)
            return;

        if (TryApplyForcedEmergeAfterHitFromWeapon(target, weapon))
            return;

        if (weapon.forceOpponentToGoToDomainAfterHit == null || weapon.forceOpponentToGoToDomainAfterHit.Count <= 0)
            return;

        for (int i = 0; i < weapon.forceOpponentToGoToDomainAfterHit.Count; i++)
        {
            WeaponForcedLayerAfterHit effect = weapon.forceOpponentToGoToDomainAfterHit[i];
            if (effect == null)
                continue;

            Domain forcedDomain = effect.domain;
            HeightLevel forcedHeight = effect.heightLevel;
            int turns = Mathf.Max(1, effect.turns);
            if (!target.SupportsLayerMode(forcedDomain, forcedHeight))
                continue;

            Tilemap forcedLayerBoardMap = terrainTilemap != null ? terrainTilemap : target.BoardTilemap;
            Vector3Int forcedLayerCell = target.CurrentCellPosition;
            forcedLayerCell.z = 0;
            if ((target.GetDomain() != forcedDomain || target.GetHeightLevel() != forcedHeight) &&
                !PodeEmergirSensor.CanApplyLayerTransitionAtCell(
                    target, forcedLayerBoardMap, terrainDatabase, forcedLayerCell,
                    forcedDomain, forcedHeight, out string forcedLayerBlockReason))
            {
                ApplyPendingForcedLayerLock(target, forcedDomain, forcedHeight, turns, forcedLayerBlockReason);
                return;
            }

            bool hadPreviousLock = target.TryGetForcedLayerLock(out Domain previousDomain, out HeightLevel previousHeight, out int previousTurns);
            bool previousCountdownStarted = target.LayerLockCountdownStarted;
            target.ClearForcedLayerLock();

            bool moved = target.GetDomain() == forcedDomain && target.GetHeightLevel() == forcedHeight;
            if (!moved)
                moved = target.TrySetCurrentLayerMode(forcedDomain, forcedHeight);

            if (!moved)
            {
                if (hadPreviousLock)
                    target.RestoreLayerLock(previousDomain, previousHeight, previousTurns, previousCountdownStarted);
                continue;
            }

            target.SetForcedLayerLock(forcedDomain, forcedHeight, turns);
            string forcedMessage = PanelDialogController.ResolveDialogMessage(
                "layer.forced.after_hit",
                "<unit> forcada para <domain>/<height> (<turns> turnos).",
                new Dictionary<string, string>
                {
                    { "unit", ResolveDebugUnitName(target) },
                    { "domain", forcedDomain.ToString() },
                    { "height", forcedHeight.ToString() },
                    { "turns", turns.ToString() }
                });
            PushPanelUnitMessage(forcedMessage, 2.6f);
            return;
        }
    }

    private bool TryApplyForcedEmergeAfterHitFromWeapon(UnitManager target, WeaponData weapon)
    {
        if (target == null || weapon == null)
            return false;

        Domain targetDomain = target.GetDomain();
        HeightLevel targetHeight = target.GetHeightLevel();
        if (!weapon.ShouldForceTargetToEmergeAfterHit(targetDomain, targetHeight))
            return false;

        if (!TryResolveForcedEmergeLayerTarget(targetDomain, targetHeight, out Domain forcedDomain, out HeightLevel forcedHeight))
            return false;

        if (!target.SupportsLayerMode(forcedDomain, forcedHeight))
            return false;

        const int forcedTurns = 2;

        Tilemap emergeBoardMap = terrainTilemap != null ? terrainTilemap : target.BoardTilemap;
        Vector3Int emergeCell = target.CurrentCellPosition;
        emergeCell.z = 0;
        if (!PodeEmergirSensor.CanApplyLayerTransitionAtCell(
                target, emergeBoardMap, terrainDatabase, emergeCell,
                forcedDomain, forcedHeight, out string emergeBlockReason))
        {
            // Emersao forcada ilegal no hex (ex.: navio na superficie): a
            // unidade permanece submersa, porem revelada e com o lock pendente.
            ApplyPendingForcedLayerLock(target, forcedDomain, forcedHeight, forcedTurns, emergeBlockReason);
            return true;
        }

        bool hadPreviousLock = target.TryGetForcedLayerLock(out Domain previousDomain, out HeightLevel previousHeight, out int previousTurns);
        bool previousCountdownStarted = target.LayerLockCountdownStarted;
        target.ClearForcedLayerLock();

        bool moved = targetDomain == forcedDomain && targetHeight == forcedHeight;
        if (!moved)
            moved = target.TrySetCurrentLayerMode(forcedDomain, forcedHeight);

        if (!moved)
        {
            if (hadPreviousLock)
                target.RestoreLayerLock(previousDomain, previousHeight, previousTurns, previousCountdownStarted);
            return false;
        }

        target.SetForcedLayerLock(forcedDomain, forcedHeight, forcedTurns);
        string forcedMessage = PanelDialogController.ResolveDialogMessage(
            "layer.forced.after_hit",
            "<unit> forcada para <domain>/<height> (<turns> turnos).",
            new Dictionary<string, string>
            {
                { "unit", ResolveDebugUnitName(target) },
                { "domain", forcedDomain.ToString() },
                { "height", forcedHeight.ToString() },
                { "turns", forcedTurns.ToString() }
            });
        PushPanelUnitMessage(forcedMessage, 2.6f);
        return true;
    }

    // Camada forcada que nao cabe no hex atual vira lock pendente: a unidade
    // permanece na camada de origem, porem revelada (HasPendingForcedLayerLock
    // anula stealth no FoW) e sem contar o tempo do lock. O upkeep do dono
    // (MatchController.TryApplyPendingForcedLayerAtTurnStart) ou o fim do
    // proximo movimento aplica a camada quando o hex permitir.
    private void ApplyPendingForcedLayerLock(UnitManager unit, Domain domain, HeightLevel height, int turns, string blockReason)
    {
        if (unit == null)
            return;

        unit.ClearForcedLayerLock();
        unit.SetForcedLayerLock(domain, height, Mathf.Max(1, turns));

        string pendingMessage = PanelDialogController.ResolveDialogMessage(
            "layer.forced.pending",
            "<unit> revelada: transicao para <domain>/<height> pendente (hex ocupado).",
            new Dictionary<string, string>
            {
                { "unit", ResolveDebugUnitName(unit) },
                { "domain", domain.ToString() },
                { "height", height.ToString() },
                { "turns", turns.ToString() }
            });
        PushPanelUnitMessage(pendingMessage, 2.6f);
        if (showMovementLogs)
            Debug.Log($"[LayerForce] Pendente: {ResolveDebugUnitName(unit)} -> {domain}/{height} ({blockReason})");
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
            // Regra de sobrevida: dano proporcional em embarcados nao mata enquanto
            // o transportador pai continua vivo.
            if (childBefore <= 0)
                continue;

            int propagatedDamage = Mathf.RoundToInt(childBefore * ratio);
            if (propagatedDamage <= 0)
                propagatedDamage = 1;

            int childAfter = Mathf.Max(1, childBefore - propagatedDamage);
            child.SetCurrentHP(childAfter);

            ApplyRatioDamageToEmbarkedRecursive(child, ratio);
        }
    }

    private void KillEntireEmbarkedChain(
        UnitManager root,
        bool detachSelf = true,
        string deathReason = "morto porque o transportador morreu",
        UnitManager killer = null)
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
                KillEntireEmbarkedChain(children[i], detachSelf: true, deathReason: deathReason, killer: killer);
        }

        // MarkDead precisa observar a transicao vivo -> morto para publicar o
        // evento no Jornal do Comandante. SetCurrentHP(0) primeiro sincronizava
        // isDead e fazia MarkDead interpretar a morte como ja registrada.
        root.MarkDead(deathReason, killer);
        root.SetCurrentHP(0);
        
        OnUnitDestroyed?.Invoke(root);

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
            KillEntireEmbarkedChain(children[i], detachSelf: true, deathReason: "morto porque o transportador morreu");
    }

    private IEnumerator ExecuteDeathResolutionIfNeeded(CombatResolutionResult combat)
    {
        List<DeathTarget> deaths = BuildDeathTargets(combat);
        if (deaths.Count == 0)
            yield break;

        for (int i = 0; i < deaths.Count; i++)
        {
            DeathTarget target = deaths[i];
            yield return ExecuteUnitDeathPresentation(target.unit, target.cell, target.worldPos, applyStartDelay: true);
        }
    }

    private IEnumerator ExecuteUnitDeathPresentation(
        UnitManager unit,
        Vector3Int focusCell,
        Vector3 worldPos,
        bool applyStartDelay,
        bool moveCursorFirst = true)
    {
        if (unit == null || !unit.gameObject.activeInHierarchy)
            yield break;

        if (applyStartDelay)
        {
            float deathStartDelay = animationManager != null ? animationManager.CombatDeathStartDelay : 0f;
            if (deathStartDelay > 0f)
                yield return new WaitForSeconds(deathStartDelay);
        }

        if (moveCursorFirst && cursorController != null)
        {
            focusCell.z = 0;
            cursorController.SetCell(focusCell, playMoveSfx: true, adjustCamera: true);
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

        matchController?.NotifyUnitWillBeDisabledForFog(unit);
        unit.gameObject.SetActive(false);

        float explosionDuration = animationManager != null
            ? animationManager.PlayExplosionEffectAt(worldPos)
            : 0f;
        if (explosionDuration > 0f)
            yield return new WaitForSeconds(explosionDuration);
        else
            yield return new WaitForSeconds(0.12f);

        yield return new WaitForSeconds(0.05f);

        OnUnitDestroyed?.Invoke(unit);
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
        bool suppressInitialFocus = suppressInitialMirandoAutoFocus;
        suppressInitialMirandoAutoFocus = false;

        BuildMirandoSelectionEntries();
        if (GetMirandoEntryCount() <= 0)
            return;

        // Ao sair do fluxo de movimento para mirar, oculta o rastro legado do caminho comprometido.
        ClearCommittedPathVisual();

        cursorStateBeforeMirando = CurrentCursorState == CursorState.MoveuAndando
            ? CursorState.MoveuAndando
            : CursorState.MoveuParado;
        Advance(CursorState.Mirando, "EnterMirandoState");
        if (cursorController != null)
            RecordCinematicAimAction(cursorController.CurrentCell);
        scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
        scannerSelectedTargetIndex = 0;
        mirandoCancelFocused = false;
        mirandoConfirmButtonFocus = 0;

        if (GetMirandoEntryCount() <= 1)
        {
            if (GetMirandoEntryCount() == 1 && !suppressInitialFocus)
                FocusCurrentMirandoTarget(logDetails: true);

            if (GetMirandoEntryCount() == 1 &&
                TryGetCurrentMirandoEntry(out MirandoSelectionEntry singleEntry) &&
                singleEntry.isValid)
            {
                EnterMirandoConfirmStep();
            }
            else
            {
                LogTargetSelectionPanel();
            }
            return;
        }

        LogTargetSelectionPanel();
        if (!suppressInitialFocus)
            FocusCurrentMirandoTarget(logDetails: true);
    }

    private void EnterMirandoConfirmStep()
    {
        if (GetMirandoEntryCount() <= 0)
            return;

        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= GetMirandoEntryCount())
            scannerSelectedTargetIndex = 0;

        scannerPromptStep = ScannerPromptStep.MirandoConfirmTarget;
        mirandoCancelFocused = false;
        mirandoConfirmButtonFocus = 0;
        MirandoSelectionEntry picked = cachedMirandoSelectionEntries[scannerSelectedTargetIndex];
        RebuildMirandoPreviewPath(picked);
        SetMirandoPreviewVisible(CurrentCursorState == CursorState.Mirando);
        SetMirandoSpotterPreviewsVisible(CurrentCursorState == CursorState.Mirando && picked.isValid);
        LogAttackConfirmationPrompt(picked, scannerSelectedTargetIndex + 1);
    }

    private void FocusCurrentMirandoTarget(bool logDetails, bool moveCursor = true)
    {
        if (GetMirandoEntryCount() == 0)
        {
            ClearMirandoTargetHighlight();
            SetMirandoPreviewVisible(false);
            SetMirandoSpotterPreviewsVisible(false);
            return;
        }

        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= GetMirandoEntryCount())
            scannerSelectedTargetIndex = 0;

        MirandoSelectionEntry option = cachedMirandoSelectionEntries[scannerSelectedTargetIndex];
        UpdateMirandoTargetHighlight(option.TargetUnit);
        if (moveCursor)
            MoveCursorToTarget(option.TargetUnit);
        RebuildMirandoPreviewPath(option);
        SetMirandoPreviewVisible(CurrentCursorState == CursorState.Mirando);
        SetMirandoSpotterPreviewsVisible(CurrentCursorState == CursorState.Mirando && option.isValid);
        if (logDetails)
            LogCurrentMirandoTarget(option, scannerSelectedTargetIndex + 1, GetMirandoEntryCount());
    }

    private void LogCurrentMirandoTarget(MirandoSelectionEntry entry, int shownIndex, int total)
    {
        if (entry.isValid)
        {
            PodeMirarTargetOption option = entry.validOption;
            if (option == null || option.targetUnit == null)
                return;

            UnitManager target = option.targetUnit;
            string attackWeapon = option.weapon != null ? option.weapon.displayName : "arma";
            string counterText = option.defenderCanCounterAttack ? "sim" : $"nao ({option.defenderCounterReason})";
            string label = !string.IsNullOrWhiteSpace(option.displayLabel) ? option.displayLabel : target.name;
            string evPathText = FormatEvPath(option.lineOfFireEvPath);
            string lineHexesText = FormatHexPath(option.lineOfFireIntermediateCells);

            RuntimeLog(
                $"[Mirando] Alvo {shownIndex}/{total} [VALIDO]\n" +
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
            return;
        }

        PodeMirarInvalidOption invalid = entry.invalidOption;
        if (invalid == null || invalid.targetUnit == null)
            return;

        string weapon = invalid.weapon != null ? invalid.weapon.displayName : "arma";
        string reason = !string.IsNullOrWhiteSpace(invalid.reason) ? invalid.reason : "motivo nao informado";
        string evPathInvalid = FormatEvPath(invalid.lineOfFireEvPath);
        string lineHexesInvalid = FormatHexPath(invalid.lineOfFireIntermediateCells);
        RuntimeLog(
            $"[Mirando] Alvo {shownIndex}/{total} [INVALIDO]\n" +
            $"Unidade: {invalid.targetUnit.name}\n" +
            $"Distancia: {invalid.distance}\n" +
            $"Arma avaliada: {weapon}\n" +
            $"Posicao atacante: {invalid.attackerPositionLabel}\n" +
            $"Posicao defensor: {invalid.defenderPositionLabel}\n" +
            $"EV path: {evPathInvalid}\n" +
            $"Linha (hex intermediario): {lineHexesInvalid}\n" +
            $"Motivo: {reason}\n" +
            "Linha de tiro: CINZA ESCURO\n" +
            "Enter nao confirma este alvo.");
    }

    private int GetMirandoEntryCount()
    {
        return cachedMirandoSelectionEntries.Count;
    }

    private void BuildMirandoSelectionEntries()
    {
        cachedMirandoSelectionEntries.Clear();
        RestoreMirandoInvalidUnitTint();

        HashSet<UnitManager> validTargets = new HashSet<UnitManager>();
        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption valid = cachedPodeMirarTargets[i];
            if (valid == null || valid.targetUnit == null)
                continue;

            cachedMirandoSelectionEntries.Add(new MirandoSelectionEntry(valid));
            validTargets.Add(valid.targetUnit);
        }

        for (int i = 0; i < cachedPodeMirarInvalidTargets.Count; i++)
        {
            PodeMirarInvalidOption invalid = cachedPodeMirarInvalidTargets[i];
            if (invalid == null || invalid.targetUnit == null)
                continue;
            if (validTargets.Contains(invalid.targetUnit))
                continue;

            cachedMirandoSelectionEntries.Add(new MirandoSelectionEntry(invalid));
        }

        SortClockwiseAroundUnit(cachedMirandoSelectionEntries,
            e => e.TargetUnit != null ? e.TargetUnit.CurrentCellPosition : Vector3Int.zero,
            selectedUnit);
        // A ordem circular continua valendo dentro de cada grupo, mas opcoes que podem
        // ser confirmadas sempre aparecem antes das entradas apenas informativas.
        List<MirandoSelectionEntry> groupedEntries = new List<MirandoSelectionEntry>(cachedMirandoSelectionEntries.Count);
        for (int i = 0; i < cachedMirandoSelectionEntries.Count; i++)
            if (cachedMirandoSelectionEntries[i].isValid)
                groupedEntries.Add(cachedMirandoSelectionEntries[i]);
        for (int i = 0; i < cachedMirandoSelectionEntries.Count; i++)
            if (!cachedMirandoSelectionEntries[i].isValid)
                groupedEntries.Add(cachedMirandoSelectionEntries[i]);
        cachedMirandoSelectionEntries.Clear();
        cachedMirandoSelectionEntries.AddRange(groupedEntries);
        ApplyMirandoInvalidUnitTint();
    }

    private void ApplyMirandoInvalidUnitTint()
    {
        for (int i = 0; i < cachedMirandoSelectionEntries.Count; i++)
        {
            MirandoSelectionEntry entry = cachedMirandoSelectionEntries[i];
            if (entry.isValid || entry.TargetUnit == null)
                continue;

            SpriteRenderer[] renderers = entry.TargetUnit.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                SpriteRenderer renderer = renderers[r];
                if (renderer == null)
                    continue;

                if (!mirandoInvalidTintOriginalColors.ContainsKey(renderer))
                    mirandoInvalidTintOriginalColors[renderer] = renderer.color;

                Color baseColor = mirandoInvalidTintOriginalColors[renderer];
                renderer.color = new Color(baseColor.r * 0.35f, baseColor.g * 0.35f, baseColor.b * 0.35f, baseColor.a);
            }
        }
    }

    private void RestoreMirandoInvalidUnitTint()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> pair in mirandoInvalidTintOriginalColors)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value;
        }

        mirandoInvalidTintOriginalColors.Clear();
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
        if (CurrentCursorState != CursorState.Mirando || GetMirandoEntryCount() == 0)
            return false;
        if (scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        int count = GetMirandoEntryCount();
        if (count <= 0)
            return false;

        // Slot virtual CANCELAR ao final (igual ao shopping): total = count + 1. As 4 setas passam
        // por aqui (esquerda/cima = -1, direita/baixo = +1), com wrap, e o CANCELAR entra no loop.
        int total = count + 1;
        int current = mirandoCancelFocused ? count : scannerSelectedTargetIndex;
        int next = (current + step + total) % total;
        if (next == current)
            return false;

        if (next == count)
        {
            // CANCELAR em foco: nao move o cursor no mapa, so destaca na lista do painel.
            mirandoCancelFocused = true;
            cursorController?.PlayCursorMoveSfx();
            return false;
        }

        mirandoCancelFocused = false;
        scannerSelectedTargetIndex = next;
        FocusCurrentMirandoTarget(logDetails: true);

        if (scannerSelectedTargetIndex >= 0 && scannerSelectedTargetIndex < count)
        {
            UnitManager target = cachedMirandoSelectionEntries[scannerSelectedTargetIndex].TargetUnit;
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
        if (CurrentCursorState != CursorState.Embarcando)
            return false;
        if (scannerPromptStep != ScannerPromptStep.EmbarkCycleTarget)
            return false;
        if (cachedPodeEmbarcarTargets.Count == 0)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        int count = cachedPodeEmbarcarTargets.Count;
        int total = count + 1;
        int current = embarkCancelFocused ? count : scannerSelectedEmbarkIndex;
        int next = (current + step + total) % total;
        if (next == count)
        {
            embarkCancelFocused = true;
            cursorController?.PlayCursorMoveSfx();
            return false;
        }

        embarkCancelFocused = false;
        scannerSelectedEmbarkIndex = next;
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
        if (CurrentCursorState != CursorState.Pousando)
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
        return CurrentCursorState == CursorState.Embarcando &&
               (scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget ||
                scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget);
    }

    private bool IsLandingPromptActive()
    {
        return CurrentCursorState == CursorState.Pousando &&
               (scannerPromptStep == ScannerPromptStep.LandingCycleOption ||
                scannerPromptStep == ScannerPromptStep.LandingConfirmOption);
    }

    private SensorMovementMode ResolveLandingMovementMode()
    {
        if (cursorStateBeforePousando == CursorState.MoveuAndando)
            return SensorMovementMode.MoveuAndando;

        return SensorMovementMode.MoveuParado;
    }

    private void ExitLandingStateToMovement()
    {
        if (CurrentCursorState != CursorState.Pousando)
            return;

        Retreat("ExitLandingStateToMovement");
        CursorState targetMovementState = CurrentCursorState;
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
        if (CurrentCursorState != CursorState.Embarcando)
            return;

        Retreat("ExitEmbarkStateToMovement");
        CursorState targetMovementState = CurrentCursorState;
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
        if (CurrentCursorState != CursorState.Mirando)
            return;

        Retreat("ExitMirandoStateToMovement");
        CursorState targetMovementState = CurrentCursorState;
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
        bool isAIDebugStepPreview = aiDebugStepPreviewActive;
        bool isMirandoCycle =
            CurrentCursorState == CursorState.Mirando &&
            scannerPromptStep == ScannerPromptStep.MirandoCycleTarget;

        bool isMirandoConfirm =
            CurrentCursorState == CursorState.Mirando &&
            scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget;

        bool canRenderMirandoPreview =
            isAIDebugStepPreview ||
            (!combatExecutionInProgress &&
            (isMirandoCycle || isMirandoConfirm));

        if (canRenderMirandoPreview && !isAIDebugStepPreview)
            TryRefreshMirandoPreviewPathIfNeeded();

        bool shouldShow =
            canRenderMirandoPreview &&
            mirandoPreviewPathLength > 0.0001f &&
            mirandoPreviewPathPoints.Count >= 2;

        if (!shouldShow)
        {
            SetMirandoPreviewVisible(false);
            if (!isAIDebugStepPreview)
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
        Color previewColor = GetCurrentMirandoPreviewColor();
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

        if (isAIDebugStepPreview)
            SetMirandoSpotterPreviewsVisible(false);
        else
            UpdateMirandoSpotterPreviewAnimation();
    }

    private void RebuildMirandoPreviewPath(MirandoSelectionEntry entry)
    {
        if (entry.isValid)
            RebuildMirandoPreviewPath(entry.validOption);
        else
            RebuildMirandoPreviewPath(entry.invalidOption);
    }

    private void RebuildMirandoPreviewPath(PodeMirarTargetOption option)
    {
        RebuildMirandoPreviewPathInternal(
            option != null ? option.attackerUnit : null,
            option != null ? option.targetUnit : null,
            ResolveSelectedTrajectory(option),
            option,
            useInvalidVisual: false);
    }

    private void RebuildMirandoPreviewPath(PodeMirarInvalidOption option)
    {
        RebuildMirandoPreviewPathInternal(
            option != null ? option.attackerUnit : null,
            option != null ? option.targetUnit : null,
            ResolveSelectedTrajectory(option),
            null,
            useInvalidVisual: true);
    }

    private void RebuildMirandoPreviewPathInternal(
        UnitManager attacker,
        UnitManager target,
        WeaponTrajectoryType trajectory,
        PodeMirarTargetOption validOptionForSpotter,
        bool useInvalidVisual)
    {
        mirandoPreviewPathPoints.Clear();
        mirandoPreviewPathLength = 0f;
        mirandoPreviewHeadDistance = 0f;
        mirandoPreviewSignatureValid = false;
        mirandoPreviewUseInvalidColor = useInvalidVisual;

        if (attacker == null || target == null)
        {
            SetMirandoPreviewVisible(false);
            RebuildMirandoSpotterPreviewPaths(null);
            return;
        }

        Vector3 attackerPos = attacker.transform.position;
        Vector3 targetPos = target.transform.position;
        attackerPos.z = targetPos.z;

        CacheMirandoPreviewSignature(attackerPos, targetPos, trajectory);
        if (trajectory == WeaponTrajectoryType.Parabolic)
            BuildParabolicPath(attackerPos, targetPos, mirandoPreviewPathPoints);
        else
        {
            mirandoPreviewPathPoints.Add(attackerPos);
            mirandoPreviewPathPoints.Add(targetPos);
        }

        mirandoPreviewPathLength = ComputePathLength(mirandoPreviewPathPoints);
        RebuildMirandoSpotterPreviewPaths(validOptionForSpotter);
        if (mirandoPreviewPathLength <= 0.0001f)
            SetMirandoPreviewVisible(false);
    }

    private void TryRefreshMirandoPreviewPathIfNeeded()
    {
        if (!TryGetCurrentMirandoEntry(out MirandoSelectionEntry entry))
            return;

        UnitManager attacker = entry.AttackerUnit;
        UnitManager target = entry.TargetUnit;
        if (attacker == null || target == null)
            return;

        Vector3 from = attacker.transform.position;
        Vector3 to = target.transform.position;
        from.z = to.z;
        WeaponTrajectoryType trajectory = ResolveSelectedTrajectory(entry);
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

        RebuildMirandoPreviewPath(entry);
    }

    private bool TryGetCurrentMirandoEntry(out MirandoSelectionEntry entry)
    {
        entry = default;
        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= GetMirandoEntryCount())
            return false;

        entry = cachedMirandoSelectionEntries[scannerSelectedTargetIndex];
        return true;
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

    private WeaponTrajectoryType ResolveSelectedTrajectory(MirandoSelectionEntry entry)
    {
        return entry.isValid
            ? ResolveSelectedTrajectory(entry.validOption)
            : ResolveSelectedTrajectory(entry.invalidOption);
    }

    private WeaponTrajectoryType ResolveSelectedTrajectory(PodeMirarTargetOption option)
    {
        if (option == null)
            return WeaponTrajectoryType.Straight;

        return ResolveTrajectoryForShot(option.attackerUnit, option.embarkedWeaponIndex, option.weapon);
    }

    private WeaponTrajectoryType ResolveSelectedTrajectory(PodeMirarInvalidOption option)
    {
        if (option == null)
            return WeaponTrajectoryType.Straight;

        return ResolveTrajectoryForShot(option.attackerUnit, option.embarkedWeaponIndex, option.weapon);
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
        const float verticalTieEpsilon = 0.01f;
        float dx = to.x - from.x;
        bool isVerticalTie = Mathf.Abs(dx) <= verticalTieEpsilon;
        Vector2 normal = isVerticalTie
            ? antiClockwiseNormal
            : (dx > 0f ? antiClockwiseNormal : clockwiseNormal);

        float distance = flat.magnitude;
        float maxBend = Mathf.Clamp(GetMirandoParabolaBend(), 0.2f, Mathf.Max(0.2f, distance));
        float horizontalFactor = Mathf.Clamp01(Mathf.Abs(dir.x)); // 1=horizontal, 0=vertical
        float horizontalWeight = Mathf.Pow(horizontalFactor, GetMirandoParabolaHorizontalBendWeight());
        float minBend = Mathf.Clamp(GetMirandoParabolaMinVerticalBend(), 0.01f, 0.3f); // quase reta no vertical
        float bend = Mathf.Lerp(minBend, maxBend, horizontalWeight);
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
            SetMirandoSpotterPreviewsVisible(CurrentCursorState == CursorState.Mirando);
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
        Color baseColor = GetCurrentMirandoPreviewColor();
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
        bool hideAiPresentation =
            matchController != null && matchController.ShouldHideActiveAiActionPresentation();
        bool shouldShow =
            !hideAiPresentation &&
            (CurrentCursorState == CursorState.Embarcando &&
             (scannerPromptStep == ScannerPromptStep.EmbarkCycleTarget || scannerPromptStep == ScannerPromptStep.EmbarkConfirmTarget)) &&
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
        visible = visible && !ShouldSuppressAiActionPreviewLines();
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
        ClearMirandoTargetHighlight();
        mirandoPreviewPathPoints.Clear();
        mirandoPreviewSegmentPoints.Clear();
        mirandoPreviewPathLength = 0f;
        mirandoPreviewHeadDistance = 0f;
        mirandoPreviewUseInvalidColor = false;
        mirandoPreviewSignatureValid = false;
        SetMirandoPreviewVisible(false);
        ClearMirandoSpotterPreviewData();
        SetMirandoSpotterPreviewsVisible(false);
        RestoreMirandoInvalidUnitTint();
    }

    public bool TryShowAIDebugStepPreview(PlayerAction action, out string message)
    {
        message = string.Empty;
        ClearAIDebugStepPreview();

        if (action == null)
        {
            message = "Batch vazio.";
            return false;
        }

        if (cursorController != null && TryResolveAIDebugStepCursorCell(action, out Vector3Int cursorCell))
            cursorController.SetCell(cursorCell, playMoveSfx: false, adjustCamera: true);

        if (!TryResolveAIDebugStepLineCells(action, out Vector3Int fromCell, out Vector3Int toCell))
        {
            message = "Batch sem origem/destino visual para linha azul.";
            return false;
        }

        Tilemap map = terrainTilemap != null ? terrainTilemap : (cursorController != null ? cursorController.BoardTilemap : null);
        Vector3 from = HexCoordinates.GetCellCenterWorld(map, fromCell);
        Vector3 to = HexCoordinates.GetCellCenterWorld(map, toCell);
        from.z = to.z;

        mirandoPreviewPathPoints.Clear();
        mirandoPreviewSegmentPoints.Clear();
        mirandoPreviewPathPoints.Add(from);
        mirandoPreviewPathPoints.Add(to);
        mirandoPreviewPathLength = ComputePathLength(mirandoPreviewPathPoints);
        mirandoPreviewHeadDistance = 0f;
        mirandoPreviewUseInvalidColor = false;
        mirandoPreviewSignatureValid = false;
        aiDebugStepPreviewActive = mirandoPreviewPathLength > 0.0001f;

        SetMirandoPreviewVisible(aiDebugStepPreviewActive);
        SetMirandoSpotterPreviewsVisible(false);

        message = aiDebugStepPreviewActive
            ? $"Linha azul: {FormatMapCellWithZ(fromCell)} -> {FormatMapCellWithZ(toCell)}."
            : "Origem e destino iguais; linha azul nao foi exibida.";
        return aiDebugStepPreviewActive;
    }

    public void ClearAIDebugStepPreview()
    {
        aiDebugStepPreviewActive = false;
        ClearMirandoPreview();
    }

    private static bool TryResolveAIDebugStepCursorCell(PlayerAction action, out Vector3Int cell)
    {
        cell = default;
        if (action == null)
            return false;

        if (action.HasCursorHex)
        {
            cell = action.CursorHex;
            cell.z = 0;
            return true;
        }

        if (action.HasMoveFrom)
        {
            cell = action.MoveFrom;
            cell.z = 0;
            return true;
        }

        if (action.HasTargetHex)
        {
            cell = action.TargetHex;
            cell.z = 0;
            return true;
        }

        return false;
    }

    private static bool TryResolveAIDebugStepLineCells(PlayerAction action, out Vector3Int fromCell, out Vector3Int toCell)
    {
        fromCell = default;
        toCell = default;
        if (action == null)
            return false;

        if (action.ActionType == PlayerActionType.UnitAction &&
            action.SensorAction == SensorActionType.Attack &&
            action.HasMoveTo &&
            action.HasTargetHex)
        {
            // Se a unidade vai se mover antes de atacar → mostra o movimento (MoveFrom → MoveTo).
            // Se fica parada → mostra a linha de ataque (posição atual → inimigo).
            if (action.HasMoveFrom && action.MoveFrom != action.MoveTo)
            {
                fromCell = action.MoveFrom;
                toCell   = action.MoveTo;
            }
            else
            {
                fromCell = action.MoveTo;
                toCell   = action.TargetHex;
            }
            fromCell.z = 0;
            toCell.z   = 0;
            return true;
        }

        if (action.HasMoveFrom && action.HasMoveTo)
        {
            fromCell = action.MoveFrom;
            toCell = action.MoveTo;
            fromCell.z = 0;
            toCell.z = 0;
            return true;
        }

        if (action.HasCursorHex && action.HasTargetHex)
        {
            fromCell = action.CursorHex;
            toCell = action.TargetHex;
            fromCell.z = 0;
            toCell.z = 0;
            return true;
        }

        return false;
    }

    private void UpdateMirandoTargetHighlight(UnitManager target)
    {
        if (highlightedMirandoTarget == target)
            return;

        if (highlightedMirandoTarget != null)
            highlightedMirandoTarget.ClearTemporarySortingOrder();

        highlightedMirandoTarget = target;
        if (highlightedMirandoTarget != null)
            highlightedMirandoTarget.SetTemporarySortingOrder();
    }

    private void ClearMirandoTargetHighlight()
    {
        if (highlightedMirandoTarget == null)
            return;

        highlightedMirandoTarget.ClearTemporarySortingOrder();
        highlightedMirandoTarget = null;
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

    private Color GetCurrentMirandoPreviewColor()
    {
        if (aiDebugStepPreviewActive)
            return aiDebugStepPreviewColor;

        if (mirandoPreviewUseInvalidColor)
            return new Color(0.18f, 0.18f, 0.18f, 0.95f);

        return GetMirandoPreviewColor();
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

    private float GetMergeQueuePreviewMultiplier()
    {
        return animationManager != null ? animationManager.MergeQueuePreviewMultiplier : 0.55f;
    }

    private int GetMergeQueuePreviewSegmentQuantities()
    {
        return animationManager != null ? animationManager.MergeQueuePreviewSegmentQuantities : 1;
    }

    private float GetMergeQueuePreviewSegmentSpeed()
    {
        return animationManager != null ? animationManager.MergeQueuePreviewSegmentSpeed : 3f;
    }

    private float GetMergeQueuePreviewSegmentSpacingMultiplier()
    {
        return animationManager != null ? animationManager.MergeQueuePreviewSegmentSpacingMultiplier : 1f;
    }

    private float GetMergeMoveStepDuration()
    {
        return animationManager != null ? animationManager.MergeMoveStepDuration : 0.20f;
    }

    private float GetMergeCursorHopDelay()
    {
        return animationManager != null ? animationManager.MergeCursorHopDelay : 0.06f;
    }

    private float GetMergeAfterParticipantMoveDelay()
    {
        return animationManager != null ? animationManager.MergeAfterParticipantMoveDelay : 0.10f;
    }

    private float GetMergeAfterParticipantLoadDelay()
    {
        return animationManager != null ? animationManager.MergeAfterParticipantLoadDelay : 0.12f;
    }

    private float GetMirandoParabolaBend()
    {
        return animationManager != null ? animationManager.MirandoParabolaBend : 1.2f;
    }

    private float GetMirandoParabolaMinVerticalBend()
    {
        return animationManager != null ? animationManager.MirandoParabolaMinVerticalBend : 0.05f;
    }

    private float GetMirandoParabolaHorizontalBendWeight()
    {
        return animationManager != null ? animationManager.MirandoParabolaHorizontalBendWeight : 0.85f;
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
        if (TryGetCurrentMirandoEntry(out MirandoSelectionEntry entry))
        {
            UnitManager attacker = entry.AttackerUnit;
            if (attacker != null)
                return attacker.TeamId;
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
            if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame) { number = 0; return true; }
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
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) { number = 0; return true; }
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
        // Durante o turno da IA, bloqueia hotkeys de gameplay manual (U/X).
        if ((char.ToUpperInvariant(letter) == 'U' || char.ToUpperInvariant(letter) == 'X') && IsActiveAiInputLock())
            return false;

        switch (char.ToUpperInvariant(letter))
        {
            case 'A':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.A);
#endif
            case 'C':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.C);
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
            case 'D':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.D);
#endif
            case 'F':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.F);
#endif
            case 'S':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.S);
#endif
            case 'T':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.T);
#endif
            case 'P':
#if ENABLE_INPUT_SYSTEM
                return false; // disabled - próxima versão (era: Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
#else
                return false; // disabled - próxima versão (era: Input.GetKeyDown(KeyCode.P))
#endif
            case 'U':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.U);
#endif
            case 'X':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.X);
#endif
            case 'Z':
#if ENABLE_INPUT_SYSTEM
                return false; // disabled - próxima versão (era: Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
#else
                return false; // disabled - próxima versão (era: Input.GetKeyDown(KeyCode.Z))
#endif
            default:
                return false;
        }
    }

    private static bool IsActiveAiInputLock()
    {
        MatchController match = Object.FindAnyObjectByType<MatchController>();
        return match != null && match.IsPlayerInputLockedByActiveAI();
    }

    private static bool WasFunctionKeyPressedThisFrame(KeyCode key)
    {
        if (key != KeyCode.F8)
            return false;

#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F8);
#endif
    }
}
