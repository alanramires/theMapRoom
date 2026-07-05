using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Stopwatch = System.Diagnostics.Stopwatch;

public class ReplayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private UnitSpawner unitSpawner;
    [SerializeField] private ConstructionSpawner constructionSpawner;
    [SerializeField] private FogOfWarController fogOfWarController;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private AutomatedPlayer automatedPlayer;

    [Header("Runtime")]
    [SerializeField] private bool isRecording;
    [SerializeField] private bool isReplaying;
    [SerializeField] private ActionStack actionStack = new ActionStack();
    [SerializeField] private PlayerAction currentBuffer = new PlayerAction();
    [SerializeField] private ReplayTurnRecord currentRecord;
    [SerializeField] private List<ReplayTurnRecord> matchHistory = new List<ReplayTurnRecord>();
    [SerializeField] private int selectedTurnIndex = -1;
    [SerializeField] private TeamId observerTeam = TeamId.Neutral;
    [SerializeField] private ReplayVisionMode visionMode = ReplayVisionMode.Omniscient;
    [SerializeField] private int currentStepIndex;
    [Header("Playback")]
    [SerializeField] private bool isPlaying;
    [FormerlySerializedAs("autoPlayStepInterval")]
    [SerializeField, Range(0.01f, 5f), Tooltip("Time between action batches (seconds).") ] private float timeBetweenBatches = 0.5f;
    [SerializeField] private bool animateCursorTravelBetweenActions = true;
    [SerializeField, Range(0.01f, 1f)] private float cursorTravelStepDelay = 0.15f;
    [SerializeField, Range(0f, 1f), Tooltip("Delay between sensor substeps during replay automation (seconds).") ] private float sensorSubstepDelay = 0.08f;
    [SerializeField, Range(0f, 2f), Tooltip("Pausa apos selecionar a unidade antes de comecar a mover o cursor ao destino (seconds).") ] private float unitSelectionHoldDelay = 0.3f;
    [SerializeField, Range(0f, 2f), Tooltip("Pausa entre selecionar uma acao de sensor (alvo visivel, linha de mira, etc.) e o confirma final. Aplicado em todos os sensores (ataque, captura, embarque...) (seconds).") ] private float beforeConfirmDelay = 0.15f;
    [SerializeField, Range(0f, 1f), Tooltip("Pausa entre cada item navegado em uma lista de sensor (alvos de ataque, destinos de embarque, etc.) durante replay automatizado (seconds).") ] private float sensorListNavDelay = 0.12f;
    [SerializeField, Range(0f, 2f)] private float replayConfirmVisualDelay = 0.25f;
    [SerializeField, Range(0.5f, 8f), Tooltip("Tempo minimo de exibicao das mensagens de transicao do replay (segundos).") ] private float replayTransitionMinDisplaySeconds = 3f;
    [SerializeField, Range(0.01f, 1f), Tooltip("Delay between shopping menu items during replay automation (seconds).") ] private float shoppingNavDelay = 0.15f;
    [SerializeField, Range(0f, 1f), Tooltip("Pausa após o menu de compras abrir, antes de navegar ou confirmar a seleção (seconds).") ] private float shoppingMenuOpenDelay = 0.25f;
    [SerializeField, Range(0f, 1f), Tooltip("Pausa entre cada passo de navegação no menu do jogador (abre ESC, navega ate Reabastecer, etc.) durante execução da IA (seconds).") ] private float playerMenuStepDelay = 0.2f;
    [SerializeField, Tooltip("Quando ligado, remove delays artificiais do replay e usa teleporte do cursor entre cells.")] private bool fastReplayMode = false;
    [Header("Telemetry")]
    [SerializeField] private bool snapshotTelemetryEnabled = true;
    [SerializeField] private int snapshotTelemetryLogEvery = 20;
    [SerializeField] private bool snapshotTelemetryVerboseContext = false;
    [Header("Debug Logs")]
    [SerializeField] private bool enableReplayRuntimeLogs = false;
    [SerializeField] private bool enableReplayRuntimeWarnings = true;
    private long snapshotTelemetryCount;
    private double snapshotTelemetryTotalMs;
    private double snapshotTelemetryMinMs = double.MaxValue;
    private double snapshotTelemetryMaxMs;
    private TurnStartSnapshot preReplayLiveSnapshot;
    private ReplayTurnRecord preReplayRecordingRecord;
    private bool preReplayWasRecording;
    private Coroutine delayedBeginTurnRecordingRoutine;
    private readonly Dictionary<int, UnitManager> replayUnitPool = new Dictionary<int, UnitManager>();
    private readonly Dictionary<int, ConstructionManager> replayConstructionPool = new Dictionary<int, ConstructionManager>();
    private readonly HashSet<UnitManager> replaySpawnedUnits = new HashSet<UnitManager>();
    private readonly Dictionary<int, TurnStartSnapshot> stepSnapshots = new Dictionary<int, TurnStartSnapshot>();
    private bool replayPoolsInitialized;
    private int replayPoolSceneHandle = -1;
    private Coroutine restoreFogRefreshRoutine;
    private Coroutine attackStepExecutionRoutine;
    private Coroutine actionStepExecutionRoutine;
    private Coroutine autoplayAdvanceRetryRoutine;
    private Coroutine replayTransitionFeedbackRoutine;
    private string replayTransitionFeedbackText = string.Empty;
    private float replayTransitionFeedbackStartedAt;
    private bool replayBatchAbortRequested;

    private readonly Dictionary<string, ServiceData> cachedServicesById = new Dictionary<string, ServiceData>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SupplyData> cachedSuppliesById = new Dictionary<string, SupplyData>(StringComparer.OrdinalIgnoreCase);
    private BattleMapMenuRootController cachedBattleMapMenu;

    public bool IsRecording => isRecording;
    public bool IsReplaying => isReplaying;
    public ReplayTurnRecord CurrentRecord => currentRecord;
    public IReadOnlyList<ReplayTurnRecord> MatchHistory => matchHistory;
    public int SelectedTurnIndex => selectedTurnIndex;
    public TeamId ObserverTeam => observerTeam;
    public ReplayVisionMode VisionMode => visionMode;
    public int CurrentStepIndex => currentStepIndex;
    public bool IsPlaying => isPlaying;
    public bool IsPaused => !isPlaying;
    public bool IsStepExecutionBusy => IsReplayStepExecutionBusy();
    public bool FastReplayMode => fastReplayMode;
    public ActionStack ActionStack => actionStack;
    public PlayerAction CurrentBuffer => currentBuffer;
    public int CurrentReplayBatchCount => ResolveCurrentReplayBatchCount();

    private void ReplayLog(string message)
    {
        if (enableReplayRuntimeLogs)
            LogManager.Info(GameLogCategory.Replay, message, this);
    }

    private void ReplayLogWarning(string message)
    {
        if (enableReplayRuntimeWarnings)
            LogManager.Warning(GameLogCategory.Replay, message, this);
    }

    public float GetEffectiveTimeBetweenBatchesForAutoplay()
    {
        return GetEffectiveTimeBetweenBatches();
    }

    public float GetEffectiveCursorTravelStepDelayForRuntimeMotion()
    {
        return GetEffectiveCursorTravelStepDelay();
    }

    public float GetEffectiveShoppingNavDelayForRuntimeMotion()
    {
        return GetEffectiveShoppingNavDelay();
    }

    public float GetEffectiveReplayConfirmVisualDelayForRuntimeMotion()
    {
        return GetEffectiveReplayConfirmVisualDelay();
    }

    public string GetReplayStepExecutionBusyReason()
    {
        TryAutoAssignReferences();

        bool actionStepRoutineBusy = attackStepExecutionRoutine != null || actionStepExecutionRoutine != null;
        bool movementBusy = animationManager != null && animationManager.IsAnimatingMovement;
        bool scannerBusy = turnStateManager != null && turnStateManager.IsScannerActionExecutionInProgress;

        List<string> reasons = new List<string>(3);
        if (actionStepRoutineBusy)
            reasons.Add("actionStepRoutine");
        if (movementBusy)
            reasons.Add("IsAnimatingMovement");
        if (scannerBusy)
            reasons.Add("IsScannerActionExecutionInProgress");

        return reasons.Count > 0 ? string.Join(", ", reasons) : "none";
    }
    private void Awake()
    {
        TryAutoAssignReferences();
    }

    public void CleanupReplayArtifactsForMatchStart()
    {
        if (isReplaying)
            return;

        DestroyReplaySpawnedUnits();
        DestroyOrphanReplayUnitClonesInActiveScene();
        replayUnitPool.Clear();
        replayConstructionPool.Clear();
        replayPoolsInitialized = false;
        replayPoolSceneHandle = -1;
    }

    private void OnEnable()
    {
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        MatchController.OnBeforeAdvanceTurn += HandleBeforeAdvanceTurn;
    }

    private void OnDisable()
    {
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        MatchController.OnBeforeAdvanceTurn -= HandleBeforeAdvanceTurn;

        if (delayedBeginTurnRecordingRoutine != null)
        {
            StopCoroutine(delayedBeginTurnRecordingRoutine);
            delayedBeginTurnRecordingRoutine = null;
        }

        if (attackStepExecutionRoutine != null)
        {
            StopCoroutine(attackStepExecutionRoutine);
            attackStepExecutionRoutine = null;
        }

        if (actionStepExecutionRoutine != null)
        {
            StopCoroutine(actionStepExecutionRoutine);
            actionStepExecutionRoutine = null;
        }

        StopAutoplayAdvanceRetryRoutine();
        automatedPlayer?.StopPlaying();
    }

    private IEnumerator ExecuteActionStepFromStack(int index, PlayerAction action, TurnStartSnapshot preActionSnapshot, TurnStartSnapshot postActionSnapshot)
    {
        replayBatchAbortRequested = false;

        bool isSequentialNextBatch = index == currentStepIndex + 1;
        if (preActionSnapshot != null && (!fastReplayMode || !isSequentialNextBatch))
            RestoreSnapshot(preActionSnapshot);

        yield return TryMoveReplayCursorToActionStart(action, preActionSnapshot);

        bool canEmulateAction = action != null && CanReplayActionAsLiveInputs(action.ActionType);
        if (canEmulateAction)
        {
            yield return ExecuteRecordedActionBatch(action, preActionSnapshot);
            if (ShouldApplyPostSnapshotAfterLiveEmulation(action) && postActionSnapshot != null)
                RestoreSnapshot(postActionSnapshot);
        }
        else if (postActionSnapshot != null)
            RestoreSnapshot(postActionSnapshot);

        if (replayBatchAbortRequested)
        {
            replayBatchAbortRequested = false;
            actionStepExecutionRoutine = null;
            yield break;
        }

        currentStepIndex = index;
        ApplyReplayVision();
        bool reachedLastBatch = currentStepIndex >= ResolveCurrentReplayBatchCount() - 1;
        if (reachedLastBatch)
        {
            isPlaying = false;
            StopAutoplayAdvanceRetryRoutine();
            automatedPlayer?.StopPlaying();
        }

        actionStepExecutionRoutine = null;

        // Fallback robusto: se o autoplay estiver ativo, encadeia o proximo batch
        // mesmo quando nenhum listener externo consumir o evento de neutral.
        if (!reachedLastBatch && isPlaying)
            RequestAutoplayAdvance("step-finished");
    }

    private IEnumerator TryMoveReplayCursorToActionStart(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        if (cursorController == null)
            yield break;
        if (action != null &&
            (action.ActionType == PlayerActionType.UnitAction
             || action.ActionType == PlayerActionType.Shopping
             || action.ActionType == PlayerActionType.CommandService
             || action.ActionType == PlayerActionType.RemoveUnit))
            yield break;

        if (!TryResolveActionPreExecutionCursorCell(action, preActionSnapshot, out Vector3Int targetCursorCell))
            yield break;

        ReplayLog($"[Replay][CursorTravel] phase=pre-batch current={FormatReplayCell(NormalizeCell(cursorController.CurrentCell))} target={FormatReplayCell(NormalizeCell(targetCursorCell))}");
        yield return MoveCursorToCellWithTravel(targetCursorCell);
    }

    private IEnumerator MoveCursorToCellWithTravel(Vector3Int targetCell, List<Vector3Int> precomputedPath = null)
    {
        if (cursorController == null)
            yield break;

        Vector3Int fromCell = NormalizeCell(cursorController.CurrentCell);
        Vector3Int toCell = NormalizeCell(targetCell);
        if (fromCell == toCell)
        {
            ReplayLog($"[Replay][CursorTravel] skipped from==to {FormatReplayCell(fromCell)}");
            yield break;
        }

        // Usa o caminho real de movimento quando disponível (ex: gerado pela IA via CalcularCaminhosValidos).
        // Isso garante que o cursor segue o mesmo trajeto que a unidade percorrerá,
        // sem atravessar hexes inválidos ou unidades inimigas.
        List<Vector3Int> travelPath = (precomputedPath != null && precomputedPath.Count > 0)
            ? precomputedPath
            : BuildReplayCursorTravelPath(fromCell, toCell);
        if (travelPath == null || travelPath.Count <= 0)
            travelPath = new List<Vector3Int> { toCell };

        ReplayLog($"[Replay][CursorTravel] from={FormatReplayCell(fromCell)} to={FormatReplayCell(toCell)} pathSteps={travelPath.Count}");

        for (int i = 0; i < travelPath.Count; i++)
        {
            Vector3Int stepCell = NormalizeCell(travelPath[i]);
            ReplayLog($"[Replay][CursorTravel] step {i + 1}/{travelPath.Count} -> {FormatReplayCell(stepCell)}");
            cursorController.SetCell(stepCell, playMoveSfx: animateCursorTravelBetweenActions, adjustCamera: false);
            cursorController.TryAdjustCameraToCursor();

            if (animateCursorTravelBetweenActions)
            {
                float delay = GetEffectiveCursorTravelStepDelay();
                if (delay > 0f)
                    yield return new WaitForSecondsRealtime(delay);
                else
                    yield return null;
            }
        }
    }

    private List<Vector3Int> BuildReplayCursorTravelPath(Vector3Int fromCell, Vector3Int toCell)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        fromCell = NormalizeCell(fromCell);
        toCell = NormalizeCell(toCell);

        if (fromCell == toCell)
            return path;

        var board = cursorController != null ? cursorController.BoardTilemap : null;
        if (board == null)
        {
            path.Add(toCell);
            return path;
        }

        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        queue.Enqueue(fromCell);
        visited.Add(fromCell);

        bool found = false;
        int guard = 0;
        const int maxVisited = 8192;
        while (queue.Count > 0 && guard++ < maxVisited)
        {
            Vector3Int current = queue.Dequeue();
            if (current == toCell)
            {
                found = true;
                break;
            }

            neighbors.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(board, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = NormalizeCell(neighbors[i]);
                if (!visited.Add(next))
                    continue;

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!found)
        {
            path.Add(toCell);
            return path;
        }

        List<Vector3Int> reversed = new List<Vector3Int>();
        Vector3Int walk = toCell;
        while (walk != fromCell)
        {
            reversed.Add(walk);
            if (!cameFrom.TryGetValue(walk, out Vector3Int prev))
            {
                reversed.Clear();
                reversed.Add(toCell);
                break;
            }

            walk = prev;
        }

        for (int i = reversed.Count - 1; i >= 0; i--)
            path.Add(reversed[i]);

        return path;
    }

    private static bool CanReplayActionAsLiveInputs(PlayerActionType actionType)
    {
        switch (actionType)
        {
            case PlayerActionType.UnitAction:
            case PlayerActionType.CommandService:
            case PlayerActionType.RemoveUnit:
            case PlayerActionType.Shopping:
            case PlayerActionType.EndTurn:
                return true;

            default:
                return false;
        }
    }

    private static bool ShouldApplyPostSnapshotAfterLiveEmulation(PlayerAction action)
    {
        if (action == null)
            return false;

        // RemoveUnit can drift in runtime (target resolution / async death sequencing).
        // Reapply recorded post-action snapshot to keep replay deterministic.
        return action.ActionType == PlayerActionType.RemoveUnit;
    }

    private IEnumerator ExecuteRecordedActionBatch(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        if (action == null)
            yield break;

        if (turnStateManager == null)
            yield break;

        switch (action.ActionType)
        {
            case PlayerActionType.UnitAction:
                yield return ExecuteRecordedUnitActionBatch(action, preActionSnapshot);
                break;
            case PlayerActionType.CommandService:
                yield return ExecuteRecordedCommandServiceBatch(action, preActionSnapshot);
                break;
            case PlayerActionType.RemoveUnit:
                yield return ExecuteRecordedRemoveUnitBatch(action, preActionSnapshot);
                break;
            case PlayerActionType.Shopping:
                yield return ExecuteRecordedShoppingBatch(action, preActionSnapshot);
                break;
            case PlayerActionType.EndTurn:
                yield return ExecuteRecordedEndTurnBatch(action, preActionSnapshot);
                break;
            default:
                break;
        }

        yield return null;
    }

    private IEnumerator ExecuteRecordedUnitActionBatch(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        if (cursorController == null)
            yield break;

        bool awaitedSensorsReadyAfterMove = false;

        bool hasOriginCell = TryResolveRecordedOriginCell(action, preActionSnapshot, out Vector3Int originCell);
        if (hasOriginCell)
        {
            originCell = NormalizeCell(originCell);
            ReplayLog($"[Replay][CursorTravel] phase=unit-batch-origin current={FormatReplayCell(NormalizeCell(cursorController.CurrentCell))} origin={FormatReplayCell(originCell)}");
            yield return MoveCursorToCellWithTravel(originCell);

            if (!ValidateReplayOriginUnitBeforeConfirm(action, originCell, out string mismatchDetails))
            {
                AbortReplayBatchDueToError(
                    "dialog.replay.error",
                    "replay <erro>",
                    mismatchDetails,
                    3.2f);
                yield break;
            }

            bool selectedById = false;
            if (!string.IsNullOrWhiteSpace(action.UnitInstanceId) && turnStateManager != null)
                selectedById = turnStateManager.TryAutomatedSelectUnitByInstanceId(action.UnitInstanceId, originCell);

            if (!selectedById)
                ExecuteReplayConfirmInput();
            else
                PlayReplayActionFeedback(TurnStateManager.ActionSfx.Confirm);

            yield return null;

            float selectionHold = GetEffectiveUnitSelectionHoldDelay();
            if (selectionHold > 0f)
                yield return new WaitForSecondsRealtime(selectionHold);
        }

        bool hasDestinationCell = TryResolveRecordedDestinationCell(action, hasOriginCell, originCell, out Vector3Int destinationCell);
        if (hasDestinationCell)
        {
            destinationCell = NormalizeCell(destinationCell);
            ReplayLog($"[Replay][CursorTravel] phase=unit-batch-destination current={FormatReplayCell(NormalizeCell(cursorController.CurrentCell))} destination={FormatReplayCell(destinationCell)}");
            if (!hasOriginCell || destinationCell != originCell)
                yield return MoveCursorToCellWithTravel(destinationCell, action.MovementPath);
            ExecuteReplayConfirmInput();
            yield return null;
            yield return WaitForSensorsReadyAfterMovementConfirmEvent();
            awaitedSensorsReadyAfterMove = true;
        }
        if (action.SensorAction == SensorActionType.None)
        {
            if (!awaitedSensorsReadyAfterMove)
                yield return WaitForSensorsReadyAfterMovementConfirmEvent();

            if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral)
                yield break;

            bool moveOnlyHandled;
            if (IsLiveAIPresentationMode)
            {
                yield return WaitForAIPresentationStage();
                yield return NavigateSensorMenuForAIPresentation(SensorActionType.None);
                moveOnlyHandled = turnStateManager.SensorOptionFocusCode == 'M' &&
                                  turnStateManager.TryInvokeFocusedSensorOption();
            }
            else
            {
                moveOnlyHandled = turnStateManager.HandleAutomatedMoveOnlyActionRequested();
            }

            if (!moveOnlyHandled)
            {
                AbortReplayBatchDueToError(
                    "dialog.replay.error",
                    "replay <erro>",
                    "MoveOnly falhou ao finalizar no ciclo de sensores",
                    3.2f);
                yield break;
            }

            yield return WaitForCursorReturnedToNeutralEvent();
            yield break;
        }

        // No modo de apresentação, sustenta o estado pós-movimento por pelo menos
        // um frame para o panel_helper exibir OPÇÕES antes da IA escolher o sensor.
        if (IsLiveAIPresentationMode)
            yield return WaitForAIPresentationStage();

        bool sensorActionHandled;
        if (IsLiveAIPresentationMode && TryGetSensorMenuCode(action.SensorAction, out char sensorMenuCode))
        {
            yield return NavigateSensorMenuForAIPresentation(action.SensorAction);
            sensorActionHandled = turnStateManager.SensorOptionFocusCode == sensorMenuCode &&
                                  turnStateManager.TryInvokeFocusedSensorOption();
        }
        else
        {
            sensorActionHandled = turnStateManager.HandleAutomatedSensorActionRequested(action.SensorAction);
        }

        if (!sensorActionHandled)
            yield break;

        yield return WaitForSensorSubstepDelay();

        bool handledBySpecificSensorRoutine = true;
        switch (action.SensorAction)
        {
            case SensorActionType.Disembark:
                yield return ExecuteRecordedDisembarkSubsteps(action);
                break;
            case SensorActionType.Merge:
                yield return ExecuteRecordedMergeSubsteps(action);
                break;
            case SensorActionType.Supply:
                yield return ExecuteRecordedSupplySubsteps(action);
                break;
            case SensorActionType.Transfer:
                yield return ExecuteRecordedTransferSubsteps(action);
                break;
            case SensorActionType.Embark:
                yield return ExecuteRecordedEmbarkSubsteps(action);
                break;
            case SensorActionType.Attack:
                yield return ExecuteRecordedAttackSubsteps(action);
                break;
            case SensorActionType.Capture:
                yield return ExecuteRecordedCaptureAction(action);
                break;
            case SensorActionType.Land:
                yield return ExecuteRecordedLandAction(action);
                break;
            default:
                handledBySpecificSensorRoutine = false;
                break;
        }

        if (!handledBySpecificSensorRoutine)
        {
            if (TryResolveRecordedTargetCell(action, out Vector3Int targetCell))
            {
                targetCell = NormalizeCell(targetCell);
                if (NormalizeCell(cursorController.CurrentCell) != targetCell)
                    yield return MoveCursorToCellWithTravel(targetCell);
            }

            ExecuteReplayConfirmInput();
            yield return null;
        }

        if (turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
            yield return WaitForCursorReturnedToNeutralEvent();
    }
    private IEnumerator WaitForSensorsReadyAfterMovementConfirmEvent()
    {
        if (turnStateManager == null)
            yield break;

        bool sensorsReady = false;
        bool cursorReturnedToNeutral = turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral;
        ReplayLog($"[Replay][Listener] subscribe OnSensorsReady + OnCursorReturnedToNeutral (wait movement->sensor) initialState={turnStateManager.CurrentCursorState}");

        void HandleSensorsReady()
        {
            sensorsReady = true;
            ReplayLog("[Replay][Listener] received OnSensorsReady");
        }

        void HandleCursorNeutral()
        {
            cursorReturnedToNeutral = true;
            ReplayLog("[Replay][Listener] received OnCursorReturnedToNeutral (fallback while waiting sensors)");
        }

        TurnStateManager.OnSensorsReady += HandleSensorsReady;
        CursorController.OnCursorReturnedToNeutral += HandleCursorNeutral;
        try
        {
            // Fallback para evitar corrida caso o evento tenha disparado antes da assinatura.
            if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.MoveuAndando ||
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.MoveuParado)
            {
                sensorsReady = true;
            }

            while ((isReplaying || isLiveAIBatchExecution) && !replayBatchAbortRequested && !sensorsReady && !cursorReturnedToNeutral)
                yield return null;

            ReplayLog($"[Replay][Listener] wait movement->sensor finished sensorsReady={sensorsReady} cursorNeutralFallback={cursorReturnedToNeutral} state={turnStateManager.CurrentCursorState}");
        }
        finally
        {
            TurnStateManager.OnSensorsReady -= HandleSensorsReady;
            CursorController.OnCursorReturnedToNeutral -= HandleCursorNeutral;
            ReplayLog("[Replay][Listener] unsubscribe OnSensorsReady + OnCursorReturnedToNeutral (wait movement->sensor)");
        }
    }

    private IEnumerator WaitForCursorReturnedToNeutralEvent()
    {
        if (turnStateManager == null)
            yield break;

        bool cursorReturnedToNeutral = turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral;
        ReplayLog($"[Replay][Listener] subscribe OnCursorReturnedToNeutral (wait neutral) initialState={turnStateManager.CurrentCursorState}");

        void HandleCursorNeutral()
        {
            cursorReturnedToNeutral = true;
            ReplayLog("[Replay][Listener] received OnCursorReturnedToNeutral");
        }

        CursorController.OnCursorReturnedToNeutral += HandleCursorNeutral;
        try
        {
            while (true)
            {
                if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral)
                    cursorReturnedToNeutral = true;

                bool scannerBusy = turnStateManager.IsScannerActionExecutionInProgress;
                bool movementBusy = animationManager != null && animationManager.IsAnimatingMovement;
                bool aborted = replayBatchAbortRequested;

                if (cursorReturnedToNeutral)
                    break;
                if (aborted)
                    break;

                // Avoid race: replay can be marked inactive one frame before scanner execution
                // finishes and returns cursor to Neutral (notably in merge flows).
                if (!isReplaying && !isLiveAIBatchExecution && !scannerBusy && !movementBusy)
                    break;

                yield return null;
            }

            bool scannerBusyAtExit = turnStateManager.IsScannerActionExecutionInProgress;
            bool movementBusyAtExit = animationManager != null && animationManager.IsAnimatingMovement;
            ReplayLog(
                $"[Replay][Listener] wait neutral finished cursorNeutral={cursorReturnedToNeutral} state={turnStateManager.CurrentCursorState} " +
                $"isReplaying={isReplaying} aborted={replayBatchAbortRequested} scannerBusy={scannerBusyAtExit} movementBusy={movementBusyAtExit}");
        }
        finally
        {
            CursorController.OnCursorReturnedToNeutral -= HandleCursorNeutral;
            ReplayLog("[Replay][Listener] unsubscribe OnCursorReturnedToNeutral (wait neutral)");
        }
    }
    private IEnumerable<PlayerActionSubStep> EnumerateRecordedTargetSubsteps(PlayerAction action)
    {
        if (action == null)
            yield break;

        bool yieldedFromList = false;
        if (action.SubSteps != null)
        {
            for (int i = 0; i < action.SubSteps.Count; i++)
            {
                PlayerActionSubStep step = action.SubSteps[i];
                if (step == null)
                    continue;

                bool hasTargetData = step.HasTargetHex ||
                                     !string.IsNullOrWhiteSpace(step.TargetInstanceId) ||
                                     !string.IsNullOrWhiteSpace(step.TargetConstructionId);
                if (!hasTargetData)
                    continue;

                yieldedFromList = true;
                yield return step;
            }
        }

        bool actionHasTargetData = action.HasTargetHex ||
                                   !string.IsNullOrWhiteSpace(action.TargetInstanceId) ||
                                   !string.IsNullOrWhiteSpace(action.TargetConstructionId);
        if (!yieldedFromList && actionHasTargetData)
        {
            yield return new PlayerActionSubStep
            {
                Label = action.SubStepLabel,
                TargetInstanceId = action.TargetInstanceId,
                TargetConstructionId = action.TargetConstructionId,
                TargetHex = action.TargetHex,
                HasTargetHex = true
            };
        }
    }
    private IEnumerator ExecuteRecordedDisembarkSubsteps(PlayerAction action)
    {
        if (turnStateManager == null)
            yield break;

        bool executedAny = false;
        foreach (PlayerActionSubStep step in EnumerateRecordedTargetSubsteps(action))
        {
            if (step == null || !step.HasTargetHex)
                continue;

            Vector3Int targetCell = NormalizeCell(step.TargetHex);
            if (cursorController != null && NormalizeCell(cursorController.CurrentCell) != targetCell)
                yield return MoveCursorToCellWithTravel(targetCell);

            bool queued;
            if (IsLiveAIPresentationMode)
            {
                bool passengerSelected = turnStateManager.TrySelectAutomatedDisembarkPassengerForPresentation(step.TargetInstanceId);
                if (passengerSelected)
                    yield return WaitForAIPresentationStage();

                bool landingSelected = passengerSelected &&
                    turnStateManager.TrySelectAutomatedDisembarkLandingForPresentation(targetCell);
                if (landingSelected)
                    yield return WaitForAIPresentationStage();

                queued = landingSelected &&
                    turnStateManager.ConfirmAutomatedDisembarkOrderForPresentation();
            }
            else
            {
                queued = turnStateManager.TryQueueAutomatedDisembarkReplayOrder(step.TargetInstanceId, targetCell);
            }
            if (!queued)
            {
                ExecuteReplayConfirmInput();
                yield return WaitForSensorSubstepDelay();
                ExecuteReplayConfirmInput();
                yield return WaitForSensorSubstepDelay();
            }
            else
            {
                yield return WaitForSensorSubstepDelay();
            }

            executedAny = true;
        }

        if (!executedAny)
            yield return ExecuteDoubleConfirmFallback();

        if (!turnStateManager.TryStartAutomatedDisembarkReplayExecution() &&
            !turnStateManager.IsDisembarkExecutionInProgress &&
            turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
            yield return ExecuteDoubleConfirmFallback();

        yield return WaitForSensorSubstepDelay();
    }
    private IEnumerator ExecuteRecordedMergeSubsteps(PlayerAction action)
    {
        if (turnStateManager == null)
            yield break;

        bool hasAnyQueueLabel = false;
        if (action != null && action.SubSteps != null)
        {
            for (int i = 0; i < action.SubSteps.Count; i++)
            {
                PlayerActionSubStep candidate = action.SubSteps[i];
                if (candidate != null
                    && !string.IsNullOrWhiteSpace(candidate.Label)
                    && candidate.Label.IndexOf("QueueConfirm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasAnyQueueLabel = true;
                    break;
                }
            }
        }

        bool executedAny = false;
        foreach (PlayerActionSubStep step in EnumerateRecordedTargetSubsteps(action))
        {
            if (step == null || string.IsNullOrWhiteSpace(step.TargetInstanceId))
                continue;

            // Merge replay must only queue participant confirmations.
            if (hasAnyQueueLabel)
            {
                if (string.IsNullOrWhiteSpace(step.Label)
                    || step.Label.IndexOf("QueueConfirm", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }
            else if (!string.IsNullOrWhiteSpace(step.Label)
                     && step.Label.IndexOf("TargetConfirm", StringComparison.OrdinalIgnoreCase) >= 0
                     && !string.IsNullOrWhiteSpace(action != null ? action.TargetInstanceId : null)
                     && string.Equals(step.TargetInstanceId, action.TargetInstanceId, StringComparison.Ordinal))
            {
                // Legacy guard: ignore receiver-target confirmation when no queue labels exist.
                continue;
            }

            // Ciclar pela lista até o candidato escolhido, emulando navegação humana com cursor.mp3
            int mergeTargetIndex = turnStateManager.FindMergeTargetIndexForReplay(step.TargetInstanceId);
            if (mergeTargetIndex >= 0)
            {
                int guard = 0;
                while (turnStateManager.GetMergeCurrentIndexForReplay() != mergeTargetIndex && guard++ < 64)
                {
                    turnStateManager.StepMergeForReplay();
                    float navDelay = GetEffectiveSensorListNavDelay();
                    if (navDelay > 0f)
                        yield return new WaitForSecondsRealtime(navDelay);
                }
            }

            if (!turnStateManager.TryQueueAutomatedMergeReplayOrder(step.TargetInstanceId))
            {
                ReplayLogWarning($"[Replay][Merge] Could not queue merge participant id={step.TargetInstanceId} label={step.Label ?? "(null)"}");
                continue;
            }

            yield return WaitForSensorSubstepDelay();
            executedAny = true;
        }

        if (!executedAny)
        {
            AbortReplayBatchDueToError(
                "dialog.replay.error",
                "replay <erro>",
                "Merge replay sem participantes validos na fila",
                3.2f);
            yield break;
        }

        bool startedMergeExecution = turnStateManager.TryStartAutomatedMergeReplayExecution();
        if (!startedMergeExecution)
        {
            bool mergeStillRunning = turnStateManager.IsScannerActionExecutionInProgress;
            bool stillInMergeState = turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Fundindo;
            bool alreadyNeutral = turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral;

            if (!mergeStillRunning && !stillInMergeState && !alreadyNeutral)
            {
                AbortReplayBatchDueToError(
                    "dialog.replay.error",
                    "replay <erro>",
                    "Merge replay nao conseguiu iniciar execucao da fila",
                    3.2f);
                yield break;
            }

            ReplayLogWarning($"[Replay][Merge] start ignored: execution may have been auto-started by queue flow (state={turnStateManager.CurrentCursorState}, scannerBusy={turnStateManager.IsScannerActionExecutionInProgress}).");
        }

        yield return WaitForSensorSubstepDelay();
    }
    private IEnumerator ExecuteRecordedSupplySubsteps(PlayerAction action)
    {
        if (turnStateManager == null)
            yield break;

        bool executedAny = false;
        foreach (PlayerActionSubStep step in EnumerateRecordedTargetSubsteps(action))
        {
            if (step == null || string.IsNullOrWhiteSpace(step.TargetInstanceId))
                continue;

            if (!turnStateManager.TryQueueAutomatedSupplyReplayOrder(step.TargetInstanceId))
                yield return ExecuteDoubleConfirmFallback();
            else
                yield return WaitForSensorSubstepDelay();

            executedAny = true;
        }

        if (!executedAny)
            yield return ExecuteDoubleConfirmFallback();

        if (!turnStateManager.TryStartAutomatedSupplyReplayExecution())
            yield return ExecuteDoubleConfirmFallback();

        yield return WaitForSensorSubstepDelay();
    }

    private IEnumerator ExecuteRecordedTransferSubsteps(PlayerAction action)
    {
        if (turnStateManager == null)
            yield break;

        bool executedAny = false;
        foreach (PlayerActionSubStep step in EnumerateRecordedTargetSubsteps(action))
        {
            if (step == null || !step.HasTargetHex)
                continue;

            Vector3Int targetCell = NormalizeCell(step.TargetHex);
            if (cursorController != null && NormalizeCell(cursorController.CurrentCell) != targetCell)
                yield return MoveCursorToCellWithTravel(targetCell);

            if (!turnStateManager.TryExecuteAutomatedTransferReplayOrder(step.TargetInstanceId, targetCell))
                yield return ExecuteDoubleConfirmFallback();
            else
                yield return WaitForSensorSubstepDelay();

            executedAny = true;
        }

        if (!executedAny)
            yield return ExecuteDoubleConfirmFallback();
    }

    private IEnumerator ExecuteRecordedEmbarkSubsteps(PlayerAction action)
    {
        if (turnStateManager == null)
            yield break;

        bool executedAny = false;
        foreach (PlayerActionSubStep step in EnumerateRecordedTargetSubsteps(action))
        {
            if (step == null || !step.HasTargetHex)
                continue;

            Vector3Int targetCell = NormalizeCell(step.TargetHex);
            if (cursorController != null && NormalizeCell(cursorController.CurrentCell) != targetCell)
                yield return MoveCursorToCellWithTravel(targetCell);

            bool embarked;
            if (IsLiveAIPresentationMode)
            {
                bool selected = turnStateManager.TrySelectAutomatedEmbarkReplayTarget(step.TargetInstanceId, targetCell);
                if (selected)
                    yield return WaitForAIPresentationStage();
                embarked = selected && turnStateManager.ConfirmAutomatedEmbarkTarget();
            }
            else
            {
                embarked = turnStateManager.TryExecuteAutomatedEmbarkReplayTarget(step.TargetInstanceId, targetCell);
            }

            if (!embarked)
                yield return ExecuteDoubleConfirmFallback();
            else
                yield return WaitForSensorSubstepDelay();

            executedAny = true;
        }

        if (!executedAny)
            yield return ExecuteDoubleConfirmFallback();
    }

    private IEnumerator ExecuteRecordedAttackSubsteps(PlayerAction action)
    {
        if (turnStateManager == null)
            yield break;

        bool executedAny = false;
        foreach (PlayerActionSubStep step in EnumerateRecordedTargetSubsteps(action))
        {
            if (step == null || !step.HasTargetHex)
                continue;

            Vector3Int targetCell = NormalizeCell(step.TargetHex);
            if (cursorController != null && NormalizeCell(cursorController.CurrentCell) != targetCell)
                yield return MoveCursorToCellWithTravel(targetCell);

            int targetIndex = turnStateManager.FindMirandoTargetIndexForReplay(step.TargetInstanceId, targetCell);
            if (targetIndex < 0)
            {
                yield return ExecuteDoubleConfirmFallback();
            }
            else
            {
                // Navega pela lista até o alvo, um passo por vez
                int guard = 0;
                while (turnStateManager.GetMirandoCurrentIndexForReplay() != targetIndex && guard++ < 64)
                {
                    turnStateManager.StepMirandoForReplay();
                    float navDelay = GetEffectiveSensorListNavDelay();
                    if (navDelay > 0f)
                        yield return new WaitForSecondsRealtime(navDelay);
                }

                // Entra no confirm step — exibe linha de mira/preview
                // Equivale ao primeiro confirm humano (MirandoCycleTarget → MirandoConfirmTarget)
                turnStateManager.EnterMirandoConfirmStepForReplay();
                cursorController?.PlayConfirmSfx();

                // Pausa para o jogador ver o preview antes de confirmar
                float previewDelay = GetEffectiveBeforeConfirmDelay();
                if (previewDelay > 0f)
                    yield return new WaitForSecondsRealtime(previewDelay);

                turnStateManager.ConfirmAutomatedAttackTarget();
                yield return WaitForSensorSubstepDelay();
            }

            executedAny = true;
        }

        if (!executedAny)
            yield return ExecuteDoubleConfirmFallback();
    }

    private IEnumerator ExecuteDoubleConfirmFallback()
    {
        ExecuteReplayConfirmInput();
        yield return WaitForSensorSubstepDelay();
        ExecuteReplayConfirmInput();
        yield return WaitForSensorSubstepDelay();
    }

    private IEnumerator ExecuteRecordedCaptureAction(PlayerAction action)
    {
        // Capture starts execution immediately when requested; no extra confirm required.
        yield return WaitForSensorSubstepDelay();
    }

    private IEnumerator ExecuteRecordedLandAction(PlayerAction action)
    {
        bool executedAny = false;
        foreach (PlayerActionSubStep step in EnumerateRecordedTargetSubsteps(action))
        {
            if (step != null && step.HasTargetHex && cursorController != null)
            {
                Vector3Int targetCell = NormalizeCell(step.TargetHex);
                if (NormalizeCell(cursorController.CurrentCell) != targetCell)
                    yield return MoveCursorToCellWithTravel(targetCell);
            }

            ExecuteReplayConfirmInput();
            yield return WaitForSensorSubstepDelay();
            executedAny = true;
        }

        if (!executedAny)
        {
            ExecuteReplayConfirmInput();
            yield return WaitForSensorSubstepDelay();
        }
    }
    private IEnumerator ExecuteRecordedShoppingBatch(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        if (turnStateManager == null || cursorController == null)
            yield break;

        if (TryResolveRecordedCursorCell(action, preActionSnapshot, out Vector3Int cursorCell))
            yield return MoveCursorToCellWithTravel(NormalizeCell(cursorCell));

        bool shoppingOpened = false;
        Vector3Int normalizedCursorCell = NormalizeCell(cursorCell);
        if (action != null
            && action.IsAIGenerated
            && action.SensorAction == SensorActionType.Shopping
            && TryResolveShoppingConstruction(action, normalizedCursorCell, out ConstructionManager shoppingConstruction))
        {
            shoppingOpened = turnStateManager.TryAutomatedEnterShoppingAtConstruction(shoppingConstruction);
        }

        if (!shoppingOpened)
            ExecuteReplayConfirmInput();
        yield return null;

        if (turnStateManager.CurrentCursorState != TurnStateManager.CursorState.ShoppingAndServices)
        {
            ReplayLogWarning("[Replay][Shopping] Shopping menu did not open after confirm.");
            yield break;
        }

        // Pausa após abrir o menu — mostra o cursor pousando na seleção antes de navegar/confirmar.
        float menuOpenHold = GetEffectiveShoppingMenuOpenDelay();
        if (menuOpenHold > 0f)
            yield return new WaitForSecondsRealtime(menuOpenHold);

        int targetIndex = Mathf.Max(0, action != null ? action.ShoppingSelectedIndex : 0);
        int guard = 0;
        const int maxGuard = 256;

        while (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.ShoppingAndServices)
        {
            int currentIndex = turnStateManager.GetShoppingSelectedIndexForReplay();
            if (currentIndex >= targetIndex)
                break;

            if (guard++ >= maxGuard)
            {
                ReplayLogWarning($"[Replay][Shopping] Navigation guard reached while moving to index {targetIndex}.");
                break;
            }

            bool moved = turnStateManager.TryResolveShoppingCursorMoveForReplay(Vector3Int.right);
            if (!moved)
                moved = turnStateManager.TryResolveShoppingCursorMoveForReplay(new Vector3Int(0, -1, 0));

            if (!moved)
            {
                ReplayLogWarning($"[Replay][Shopping] Could not advance selection (current={currentIndex}, target={targetIndex}).");
                break;
            }

            float navDelay = GetEffectiveShoppingNavDelay();
            if (navDelay > 0f)
                yield return new WaitForSecondsRealtime(navDelay);

            yield return null;
        }

        if (!string.IsNullOrWhiteSpace(action != null ? action.ShoppingUnitTypeId : null))
        {
            string selectedUnitTypeId = turnStateManager.GetShoppingSelectedUnitTypeIdForReplay();
            if (!string.Equals(selectedUnitTypeId, action.ShoppingUnitTypeId, StringComparison.OrdinalIgnoreCase))
            {
                ReplayLogWarning(
                    $"[Replay][Shopping] Unit type mismatch at selected index. recorded={action.ShoppingUnitTypeId} selected={selectedUnitTypeId ?? "(null)"}");
            }
        }

        if (!turnStateManager.TryConfirmSelectedShoppingOptionForReplay())
        {
            ReplayLogWarning("[Replay][Shopping] TryConfirmSelectedShoppingOptionForReplay failed, falling back to confirm input.");
            ExecuteReplayConfirmInput();
        }

        yield return null;
    }

    private static bool TryResolveShoppingConstruction(PlayerAction action, Vector3Int cursorCell, out ConstructionManager construction)
    {
        construction = null;
        cursorCell.z = 0;

        int targetId = 0;
        bool hasTargetId = action != null
            && !string.IsNullOrWhiteSpace(action.TargetConstructionId)
            && int.TryParse(action.TargetConstructionId, out targetId);

        List<ConstructionManager> constructions = ConstructionManager.AllActive;
        if (constructions == null)
            return false;

        if (hasTargetId)
        {
            for (int i = 0; i < constructions.Count; i++)
            {
                ConstructionManager candidate = constructions[i];
                if (candidate == null) continue;
                if (candidate.InstanceId == targetId)
                {
                    construction = candidate;
                    return true;
                }
            }
        }

        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager candidate = constructions[i];
            if (candidate == null) continue;
            Vector3Int cell = candidate.CurrentCellPosition;
            cell.z = 0;
            if (cell == cursorCell)
            {
                construction = candidate;
                return true;
            }
        }

        return false;
    }

    private IEnumerator ExecuteRecordedCommandServiceBatch(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        Debug.Log("[Replay][CommandService] ExecuteRecordedCommandServiceBatch iniciado.");

        // A IA rapida equivale ao atalho X: abre o preview diretamente, sem depender
        // da selecao visual do menu, que nao estabiliza com delay zero.
        if (IsLiveAIFastMode)
        {
            string commandMessage = string.Empty;
            if (turnStateManager == null ||
                !turnStateManager.TryOpenCommandServiceFromMenu(out commandMessage))
            {
                if (!string.IsNullOrWhiteSpace(commandMessage))
                    ReplayLogWarning($"[Replay][CommandService] Atalho X recusado: {commandMessage}");
                yield break;
            }

            yield return null;
            ExecuteReplayConfirmInput();
            yield return null;
            yield break;
        }

        // Passo 1: "ESC" — abre o menu do jogador
        BattleMapMenuRootController menu = GetBattleMapMenu();
        Debug.Log($"[Replay][CommandService] BattleMapMenuRootController encontrado: {menu != null}");
        if (menu == null || !menu.TryOpenMenuFromAI())
        {
            ReplayLogWarning("[Replay][CommandService] Menu do jogador indisponível — abortando.");
            yield break;
        }

        float stepDelay = GetEffectivePlayerMenuStepDelay();

        if (stepDelay > 0f) yield return new WaitForSecondsRealtime(stepDelay);

        // Passo 2: "Cursor Down" — navega até o botão Reabastecer (Comando)
        int guard = 0;
        while (!menu.IsComandoButtonSelected && guard++ < 10)
        {
            menu.NavigateMenuStepForAI(+1);
            if (stepDelay > 0f) yield return new WaitForSecondsRealtime(stepDelay);
        }

        if (!menu.IsComandoButtonSelected)
        {
            ReplayLogWarning("[Replay][CommandService] Botão Comando não encontrado no menu — abortando.");
            menu.CloseMenuFromAI();
            yield break;
        }

        if (stepDelay > 0f) yield return new WaitForSecondsRealtime(stepDelay);

        // Passo 3: "Enter" — aciona o Serviço do Comando (fecha menu, abre preview)
        if (!menu.TryTriggerComandoForAI())
        {
            // Sem candidatos — encerra silenciosamente (sem error dialog)
            yield break;
        }

        yield return null;

        // Passo 4: "Enter" — confirma a execução do serviço
        ExecuteReplayConfirmInput();
        yield return null;
    }

    private IEnumerator ExecuteRecordedEndTurnBatch(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        Debug.Log("[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.");

        // A IA rapida equivale ao atalho R confirmado: passa a vez diretamente a
        // partir de Neutral, sem abrir e navegar o menu do jogador.
        if (IsLiveAIFastMode)
        {
            if (cursorController == null || !cursorController.TryExecuteEndTurnFromMenu())
                ReplayLogWarning("[Replay][EndTurn] Atalho R recusado.");
            yield return null;
            yield break;
        }

        BattleMapMenuRootController menu = GetBattleMapMenu();
        if (menu == null || !menu.TryOpenMenuFromAI())
        {
            ReplayLogWarning("[Replay][EndTurn] Menu do jogador indisponível — abortando.");
            yield break;
        }

        float stepDelay = GetEffectivePlayerMenuStepDelay();
        if (stepDelay > 0f) yield return new WaitForSecondsRealtime(stepDelay);

        // Navega até btnRodada: ESC abriu no índice 0 (Status), 2× DOWN chega em Rodada
        int guard = 0;
        while (!menu.IsRodadaButtonSelected && guard++ < 10)
        {
            menu.NavigateMenuStepForAI(+1);
            if (stepDelay > 0f) yield return new WaitForSecondsRealtime(stepDelay);
        }

        if (!menu.IsRodadaButtonSelected)
        {
            ReplayLogWarning("[Replay][EndTurn] Botão Rodada não encontrado — abortando.");
            menu.CloseMenuFromAI();
            yield break;
        }

        if (stepDelay > 0f) yield return new WaitForSecondsRealtime(stepDelay);

        menu.TryTriggerRodadaForAI();
        yield return null;
    }

    private IEnumerator ExecuteRecordedRemoveUnitBatch(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        if (turnStateManager != null && turnStateManager.TryStartAutomatedFuelDepletionReplayQueue(action))
        {
            yield return WaitForRemoveUnitReplayIdle();
            yield break;
        }

        bool executedAny = false;
        foreach (PlayerActionSubStep step in EnumerateRecordedTargetSubsteps(action))
        {
            if (step == null)
                continue;

            if (cursorController != null)
            {
                if (step.HasTargetHex)
                    yield return MoveCursorToCellWithTravel(NormalizeCell(step.TargetHex));
                else if (TryResolveRecordedCursorCell(action, preActionSnapshot, out Vector3Int cursorCellFromAction))
                    yield return MoveCursorToCellWithTravel(NormalizeCell(cursorCellFromAction));
            }

            if (!turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.RemoveUnit))
            {
                turnStateManager.ForceNeutral();
                yield return null;
                if (!turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.RemoveUnit))
                {
                    AbortReplayBatchDueToError(
                        "dialog.replay.error",
                        "replay <erro>",
                        "RemoveUnit replay nao conseguiu entrar em estado de confirmacao",
                        3.2f);
                    yield break;
                }
            }

            yield return null;
            ExecuteReplayConfirmInput();
            yield return null;
            yield return WaitForRemoveUnitReplayIdle();

            executedAny = true;
        }

        if (executedAny)
            yield break;

        if (cursorController != null)
        {
            if (TryResolveRecordedTargetCell(action, out Vector3Int targetCell))
                yield return MoveCursorToCellWithTravel(NormalizeCell(targetCell));
            else if (TryResolveRecordedCursorCell(action, preActionSnapshot, out Vector3Int cursorCell))
                yield return MoveCursorToCellWithTravel(NormalizeCell(cursorCell));
        }

        if (!turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.RemoveUnit))
        {
            turnStateManager.ForceNeutral();
            yield return null;
            if (!turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.RemoveUnit))
            {
                AbortReplayBatchDueToError(
                    "dialog.replay.error",
                    "replay <erro>",
                    "RemoveUnit replay nao conseguiu entrar em estado de confirmacao",
                    3.2f);
                yield break;
            }
        }

        yield return null;
        ExecuteReplayConfirmInput();
        yield return null;
        yield return WaitForRemoveUnitReplayIdle();
    }

    private IEnumerator WaitForRemoveUnitReplayIdle()
    {
        if (turnStateManager == null)
            yield break;

        while (isReplaying && !replayBatchAbortRequested)
        {
            bool cursorNeutral = turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral;
            bool scannerIdle = !turnStateManager.IsScannerActionExecutionInProgress;
            bool movementIdle = animationManager == null || !animationManager.IsAnimatingMovement;
            if (cursorNeutral && scannerIdle && movementIdle)
                break;

            yield return null;
        }

        yield return null;
    }

    private static bool IsRecordedCellPresent(Vector3Int cell, bool explicitFlag)
    {
        return explicitFlag || cell != Vector3Int.zero;
    }

    private static bool TryResolveCellFromSnapshotByUnitId(TurnStartSnapshot snapshot, string unitInstanceId, out Vector3Int cell)
    {
        cell = Vector3Int.zero;
        if (snapshot == null || snapshot.Units == null || snapshot.Units.Count <= 0)
            return false;

        if (!int.TryParse(unitInstanceId, out int unitId) || unitId <= 0)
            return false;

        for (int i = 0; i < snapshot.Units.Count; i++)
        {
            UnitSaveData unit = snapshot.Units[i];
            if (unit == null || unit.instanceId != unitId)
                continue;

            cell = new Vector3Int(unit.cellX, unit.cellY, 0);
            return true;
        }

        return false;
    }

    private bool TryResolveRecordedOriginCell(PlayerAction action, TurnStartSnapshot preActionSnapshot, out Vector3Int cell)
    {
        cell = Vector3Int.zero;
        if (action == null)
            return false;

        // Special-case: turn-start fuel depletion queue records Target/Cursor as the
        // last removal, but replay pre-travel must start from the first queued target
        // to preserve visual/runtime order.
        if (string.Equals(action.SubStepLabel, "TurnStartFuelDepletionQueue", StringComparison.OrdinalIgnoreCase)
            && action.SubSteps != null
            && action.SubSteps.Count > 0)
        {
            for (int i = 0; i < action.SubSteps.Count; i++)
            {
                PlayerActionSubStep subStep = action.SubSteps[i];
                if (subStep != null && IsRecordedCellPresent(subStep.TargetHex, subStep.HasTargetHex))
                {
                    cell = subStep.TargetHex;
                    return true;
                }
            }
        }

        if (IsRecordedCellPresent(action.MoveFrom, action.HasMoveFrom))
        {
            cell = action.MoveFrom;
            return true;
        }

        if (IsRecordedCellPresent(action.CursorHex, action.HasCursorHex))
        {
            cell = action.CursorHex;
            return true;
        }

        return TryResolveCellFromSnapshotByUnitId(preActionSnapshot, action.UnitInstanceId, out cell);
    }

    private bool TryResolveRecordedDestinationCell(PlayerAction action, bool hasOriginCell, Vector3Int originCell, out Vector3Int cell)
    {
        cell = Vector3Int.zero;
        if (action == null)
            return false;

        if (IsRecordedCellPresent(action.MoveTo, action.HasMoveTo))
        {
            cell = action.MoveTo;
            return true;
        }

        if (hasOriginCell)
        {
            cell = originCell;
            return true;
        }

        return false;
    }

    private bool TryResolveRecordedCursorCell(PlayerAction action, TurnStartSnapshot preActionSnapshot, out Vector3Int cell)
    {
        if (TryResolveRecordedOriginCell(action, preActionSnapshot, out cell))
            return true;

        if (preActionSnapshot != null && preActionSnapshot.HasCursorCell)
        {
            cell = preActionSnapshot.CursorCell;
            return true;
        }

        return false;
    }

    private bool TryResolveRecordedTargetCell(PlayerAction action, out Vector3Int cell)
    {
        cell = Vector3Int.zero;
        if (action == null)
            return false;

        if (IsRecordedCellPresent(action.TargetHex, action.HasTargetHex))
        {
            cell = action.TargetHex;
            return true;
        }

        return false;
    }
    private bool ValidateReplayOriginUnitBeforeConfirm(PlayerAction action, Vector3Int originCell, out string mismatchDetails)
    {
        mismatchDetails = string.Empty;
        if (action == null || string.IsNullOrWhiteSpace(action.UnitInstanceId))
            return true;

        if (!int.TryParse(action.UnitInstanceId, out int expectedInstanceId))
        {
            mismatchDetails = $"UnitInstanceId invalido no replay: '{action.UnitInstanceId}'";
            ReplayLogWarning($"[Replay] {mismatchDetails}");
            return false;
        }

        UnitManager foundUnit = FindReplayUnitByInstanceId(expectedInstanceId);
        if (foundUnit != null)
        {
            Vector3Int foundCell = foundUnit.CurrentCellPosition;
            foundCell.z = 0;
            if (foundCell == originCell)
                return true;
        }

        int preferredTeamId = matchController != null ? matchController.ActiveTeamId : -1;
        UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(originCell, preferredTeamId);
        int foundInstanceId = occupant != null ? occupant.InstanceId : -1;
        if (occupant != null && foundInstanceId == expectedInstanceId)
            return true;

        string foundLabel = occupant != null ? foundInstanceId.ToString() : "none";
        mismatchDetails = $"UnitInstanceId divergiu - esperado {expectedInstanceId}, encontrado {foundLabel}";
        ReplayLogWarning($"[Replay] {mismatchDetails}");
        return false;
    }

    private static UnitManager FindReplayUnitByInstanceId(int instanceId)
    {
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit != null && unit.InstanceId == instanceId)
                return unit;
        }

        return null;
    }

    private void AbortReplayBatchDueToError(string dialogId, string fallbackTemplate, string errorText, float dialogDurationSeconds)
    {
        string safeError = string.IsNullOrWhiteSpace(errorText) ? "erro desconhecido" : errorText.Trim();
        isPlaying = false;
        replayBatchAbortRequested = true;

        string message = PanelDialogController.ResolveDialogMessage(
            dialogId,
            fallbackTemplate,
            new Dictionary<string, string> { { "erro", safeError } });
        PanelDialogController.TrySetTransientText(message, Mathf.Max(0.5f, dialogDurationSeconds));
    }
    private void ExecuteReplayConfirmInput()
    {
        if (turnStateManager == null)
            return;

        TurnStateManager.ActionSfx feedback = turnStateManager.HandleConfirm();
        PlayReplayActionFeedback(feedback);
    }

    private void PlayReplayActionFeedback(TurnStateManager.ActionSfx feedback)
    {
        if (cursorController == null)
            return;

        switch (feedback)
        {
            case TurnStateManager.ActionSfx.Confirm:
                cursorController.PlayConfirmSfx();
                break;
            case TurnStateManager.ActionSfx.Cancel:
                cursorController.PlayCancelSfx();
                break;
            case TurnStateManager.ActionSfx.Error:
                cursorController.PlayErrorSfx();
                break;
        }
    }
    private void Update()
    {
    }


    private bool IsReplayStepExecutionBusy(bool includeCinematicStepRoutine = true)
    {
        TryAutoAssignReferences();

        if (includeCinematicStepRoutine && (attackStepExecutionRoutine != null || actionStepExecutionRoutine != null))
            return true;

        if (animationManager != null && animationManager.IsAnimatingMovement)
            return true;

        if (turnStateManager != null && turnStateManager.IsScannerActionExecutionInProgress)
            return true;

        return false;
    }

    public void BeginTurnRecording()
    {
        if (!Application.isPlaying || isReplaying)
            return;

        TryAutoAssignReferences();

        // Prevent accidental snapshot#0 overwrite when BeginTurnRecording is invoked again
        // for the same runtime turn/team (spurious team-change notifications).
        if (isRecording && currentRecord != null && currentRecord.StartSnapshot != null && DoesCurrentRecordMatchRuntimeTurn())
            return;

        // If replay was loaded from save for this exact runtime turn/team, preserve snapshot#0 and continue recording.
        if (currentRecord != null && currentRecord.StartSnapshot != null && DoesCurrentRecordMatchRuntimeTurn())
        {
            if (currentRecord.Steps == null)
                currentRecord.Steps = new List<ReplayStep>();

            currentStepIndex = -1;
            RebuildStepSnapshotsForCurrentRecordFromActionSnapshots();
            isRecording = true;
            return;
        }

        TurnStartSnapshot snapshot = BuildTurnStartSnapshot("BeginTurnRecording");
        currentRecord = new ReplayTurnRecord
        {
            TurnNumber = snapshot != null ? snapshot.TurnNumber : 0,
            ActingTeam = snapshot != null ? snapshot.ActiveTeam : TeamId.Neutral,
            StartSnapshot = snapshot,
            Steps = new List<ReplayStep>()
        };

        currentStepIndex = -1;
        stepSnapshots.Clear();
        isRecording = true;
    }

    public void RecordCommand(IReplayCommand command)
    {
        if (isReplaying || !isRecording || command == null || currentRecord == null)
            return;

        if (currentRecord.Steps == null)
            currentRecord.Steps = new List<ReplayStep>();

        ReplayStep step = new ReplayStep
        {
            StepIndex = currentRecord.Steps.Count,
            StepType = command.StepType,
            Command = command,
            DebugLabel = command.DebugLabel
        };

        currentRecord.Steps.Add(step);
        stepSnapshots[step.StepIndex] = BuildTurnStartSnapshot("RecordCommand.PostStep");
    }

    public void EndTurnRecording()
    {
        if (!isRecording)
            return;

        if (currentRecord != null)
        {
            if (matchHistory == null)
                matchHistory = new List<ReplayTurnRecord>();
            matchHistory.Add(currentRecord);
            selectedTurnIndex = matchHistory.Count - 1;
        }

        stepSnapshots.Clear();
        isRecording = false;
    }
    public void StartReplay()
    {
        if (!isReplaying)
        {
            preReplayLiveSnapshot = BuildTurnStartSnapshot("StartReplay.LiveSnapshot");
            preReplayRecordingRecord = currentRecord;
            preReplayWasRecording = isRecording;
        }

        if (currentRecord == null || currentRecord.StartSnapshot == null)
            return;

        BeginReplayTransitionFeedback("dialog.replay.loading", "Replay iniciando (aguarde)");

        // Replay do turno ativo: entra no estado atual sem reset para o step 0.
        isReplaying = true;
        isRecording = false;
        isPlaying = false;
        EnsureReplayPoolsInitialized();
        RebuildStepSnapshotsForCurrentRecordFromActionSnapshots();

        int batchCount = ResolveCurrentReplayBatchCount();
        currentStepIndex = batchCount - 1;
        ApplyReplayVision();
    }

    public void StartReplay(int turnIndex, ReplayVisionMode replayVisionMode, TeamId replayObserverTeam)
    {
        if (!isReplaying)
        {
            preReplayLiveSnapshot = BuildTurnStartSnapshot("StartReplay.LiveSnapshot");
            preReplayRecordingRecord = currentRecord;
            preReplayWasRecording = isRecording;
        }
        if (matchHistory == null || turnIndex < 0 || turnIndex >= matchHistory.Count)
            return;

        ReplayTurnRecord selected = matchHistory[turnIndex];
        if (selected == null || selected.StartSnapshot == null)
            return;

        BeginReplayTransitionFeedback("dialog.replay.loading", "Replay iniciando (aguarde)");

        currentRecord = selected;
        stepSnapshots.Clear();
        RebuildStepSnapshotsForCurrentRecordFromActionSnapshots();
        selectedTurnIndex = turnIndex;
        visionMode = replayVisionMode;
        observerTeam = replayObserverTeam;
        isReplaying = true;
        isRecording = false;
        isPlaying = false;
        currentStepIndex = -1;
        EnsureReplayPoolsInitialized();
        RestoreSnapshot(currentRecord.StartSnapshot);
        ApplyReplayVision();
    }

    public void StopReplay()
    {
        bool wasReplaying = isReplaying;
        if (wasReplaying)
            BeginReplayTransitionFeedback("dialog.replay.ending_wait", "Replay finalizando - voltando a partida");

        if (attackStepExecutionRoutine != null)
        {
            StopCoroutine(attackStepExecutionRoutine);
            attackStepExecutionRoutine = null;
        }

        if (actionStepExecutionRoutine != null)
        {
            StopCoroutine(actionStepExecutionRoutine);
            actionStepExecutionRoutine = null;
        }
        bool shouldPreserveStepSnapshots =
            preReplayWasRecording
            && preReplayRecordingRecord != null
            && ReferenceEquals(currentRecord, preReplayRecordingRecord);

        isReplaying = false;
        isPlaying = false;
        StopAutoplayAdvanceRetryRoutine();
        automatedPlayer?.StopPlaying();
        currentStepIndex = -1;
        if (!shouldPreserveStepSnapshots)
            stepSnapshots.Clear();
        DestroyReplaySpawnedUnits();

        if (wasReplaying && preReplayLiveSnapshot != null)
        {
            RestoreSnapshot(preReplayLiveSnapshot);
            cursorController?.TryAdjustCameraToCursor();
        }

        preReplayLiveSnapshot = null;

        if (preReplayWasRecording && preReplayRecordingRecord != null && preReplayRecordingRecord.StartSnapshot != null)
        {
            currentRecord = preReplayRecordingRecord;
            isRecording = true;
        }
        else if (!isRecording)
        {
            BeginTurnRecording();
        }

        preReplayRecordingRecord = null;
        preReplayWasRecording = false;
    }

    public ReplaySaveData ExportReplaySaveData()
    {
        ReplaySaveData data = new ReplaySaveData
        {
            selectedTurnIndex = selectedTurnIndex,
            observerTeamId = (int)observerTeam,
            visionMode = (int)visionMode,
            actionStack = actionStack ?? new ActionStack()
        };

        if (matchHistory != null)
        {
            for (int i = 0; i < matchHistory.Count; i++)
            {
                ReplayTurnRecordSaveData record = BuildTurnRecordSaveData(matchHistory[i]);
                if (record != null)
                    data.matchHistory.Add(record);
            }
        }

        data.hasCurrentRecord = currentRecord != null && currentRecord.StartSnapshot != null;
        if (data.hasCurrentRecord)
            data.currentRecord = BuildTurnRecordSaveData(currentRecord);

        return data;
    }

    public void ImportReplaySaveData(ReplaySaveData data)
    {
        if (matchHistory == null)
            matchHistory = new List<ReplayTurnRecord>();
        else
            matchHistory.Clear();

        currentRecord = null;
        selectedTurnIndex = -1;
        currentStepIndex = -1;
        isReplaying = false;
        isPlaying = false;
        isRecording = false;
        preReplayLiveSnapshot = null;
        stepSnapshots.Clear();
        actionStack = new ActionStack();
        currentBuffer = new PlayerAction();

        if (data == null)
            return;

        if (data.actionStack != null)
            actionStack = data.actionStack;

        if (data.matchHistory != null)
        {
            for (int i = 0; i < data.matchHistory.Count; i++)
            {
                ReplayTurnRecord record = BuildTurnRecordFromSaveData(data.matchHistory[i]);
                if (record != null)
                    matchHistory.Add(record);
            }
        }

        if (data.hasCurrentRecord && data.currentRecord != null)
            currentRecord = BuildTurnRecordFromSaveData(data.currentRecord);

        int maxIndex = matchHistory.Count - 1;
        selectedTurnIndex = Mathf.Clamp(data.selectedTurnIndex, -1, maxIndex);
        observerTeam = (TeamId)data.observerTeamId;
        visionMode = (ReplayVisionMode)data.visionMode;
    }

    public void ClearReplayHistory()
    {
        ImportReplaySaveData(null);
    }

    public void SetReplayVision(ReplayVisionMode replayVisionMode, TeamId replayObserverTeam)
    {
        visionMode = replayVisionMode;
        observerTeam = replayObserverTeam;
        if (isReplaying)
            ApplyReplayVision();
    }

    public bool StartReplayFromCurrentRecordBeginning(ReplayVisionMode replayVisionMode, TeamId replayObserverTeam)
    {
        if (currentRecord == null || currentRecord.StartSnapshot == null)
            return false;

        if (!isReplaying)
        {
            preReplayLiveSnapshot = BuildTurnStartSnapshot("StartReplay.LiveSnapshot");
            preReplayRecordingRecord = currentRecord;
            preReplayWasRecording = isRecording;
        }

        visionMode = replayVisionMode;
        observerTeam = replayObserverTeam;
        BeginReplayTransitionFeedback("dialog.replay.loading", "Replay iniciando (aguarde)");
        isReplaying = true;
        isRecording = false;
        isPlaying = false;
        EnsureReplayPoolsInitialized();
        RebuildStepSnapshotsForCurrentRecordFromActionSnapshots();
        currentStepIndex = -1;
        RestoreSnapshot(currentRecord.StartSnapshot);
        ApplyReplayVision();
        return isReplaying;
    }

    public bool StartReplayFromBeginning(ReplayVisionMode replayVisionMode, TeamId replayObserverTeam)
    {
        if (StartReplayFromCurrentRecordBeginning(replayVisionMode, replayObserverTeam))
            return true;

        if (matchHistory == null || matchHistory.Count <= 0)
            return false;

        StartReplay(0, replayVisionMode, replayObserverTeam);
        return isReplaying;
    }

    public bool StartReplayFromTurn(int turnNumber, ReplayVisionMode replayVisionMode, TeamId replayObserverTeam)
    {
        if (matchHistory == null || matchHistory.Count <= 0)
            return false;

        for (int i = 0; i < matchHistory.Count; i++)
        {
            ReplayTurnRecord record = matchHistory[i];
            if (record == null)
                continue;
            if (record.TurnNumber != turnNumber)
                continue;

            StartReplay(i, replayVisionMode, replayObserverTeam);
            return isReplaying;
        }

        // Fallback: allow direct index selection from the panel.
        if (turnNumber >= 0 && turnNumber < matchHistory.Count)
        {
            StartReplay(turnNumber, replayVisionMode, replayObserverTeam);
            return isReplaying;
        }

        return false;
    }
    public bool StartReplayFromTurnAndTeam(int turnNumber, TeamId actingTeam, ReplayVisionMode replayVisionMode, TeamId replayObserverTeam)
    {
        if (matchHistory == null || matchHistory.Count <= 0)
            return false;

        for (int i = 0; i < matchHistory.Count; i++)
        {
            ReplayTurnRecord record = matchHistory[i];
            if (record == null)
                continue;
            if (record.TurnNumber != turnNumber || record.ActingTeam != actingTeam)
                continue;

            StartReplay(i, replayVisionMode, replayObserverTeam);
            return isReplaying;
        }

        // Fallback: allow selecting by index + team when panel uses index semantics.
        if (turnNumber >= 0 && turnNumber < matchHistory.Count)
        {
            ReplayTurnRecord record = matchHistory[turnNumber];
            if (record != null && record.ActingTeam == actingTeam)
            {
                StartReplay(turnNumber, replayVisionMode, replayObserverTeam);
                return isReplaying;
            }
        }

        return false;
    }
    public bool StartReplayFromLatestSnapshot(ReplayVisionMode replayVisionMode, TeamId replayObserverTeam)
    {
        if (currentRecord == null || currentRecord.StartSnapshot == null)
            return false;

        visionMode = replayVisionMode;
        observerTeam = replayObserverTeam;
        StartReplay();
        return isReplaying;
    }

    public int ResolveActionIndexForTurn(int turnNumber, TeamId actingTeam)
    {
        if (currentRecord != null && currentRecord.TurnNumber == turnNumber && currentRecord.ActingTeam == actingTeam)
            return 0;

        if (matchHistory == null)
            return 0;

        for (int i = 0; i < matchHistory.Count; i++)
        {
            ReplayTurnRecord record = matchHistory[i];
            if (record == null)
                continue;
            if (record.TurnNumber != turnNumber || record.ActingTeam != actingTeam)
                continue;

            currentRecord = record;
            selectedTurnIndex = i;
            return 0;
        }

        return 0;
    }

    public IEnumerator ExecuteActionFromAutomatedPlayer(int actionIndex)
    {
        if (currentRecord == null || ResolveCurrentReplayBatchCount() <= 0)
            yield break;

        if (!isReplaying)
            StartReplay();

        if (!ExecuteStepAtIndex(actionIndex, allowCinematic: true, out bool startedAsync))
            yield break;

        if (startedAsync)
            yield return null;
        else
        {
            ApplyReplayVision();
        }
    }

    public void EnsureCurrentUnitActionBuffer(UnitManager unit, Vector3Int cursorHex)
    {
        if (isReplaying)
            return;

        if (currentBuffer == null)
            currentBuffer = new PlayerAction();

        currentBuffer.ActionType = PlayerActionType.UnitAction;
        currentBuffer.CursorHex = cursorHex;
        currentBuffer.HasCursorHex = true;
        currentBuffer.MoveFrom = cursorHex;
        currentBuffer.HasMoveFrom = true;

        if (unit != null)
        {
            currentBuffer.UnitInstanceId = unit.InstanceId.ToString();
            currentBuffer.ActingTeam = unit.TeamId;
        }
        else if (matchController != null)
        {
            currentBuffer.ActingTeam = matchController.ActiveTeam;
        }

        if (matchController != null)
            currentBuffer.TurnNumber = matchController.CurrentTurn;
    }

    public void UpdateCurrentBufferMovement(Vector3Int moveFrom, Vector3Int moveTo, UnitLayerMode layerBefore, UnitLayerMode layerAfter, List<Vector3Int> movementPath = null)
    {
        if (isReplaying)
            return;

        currentBuffer.MoveFrom = moveFrom;
        currentBuffer.HasMoveFrom = true;
        currentBuffer.MoveTo = moveTo;
        currentBuffer.HasMoveTo = true;
        currentBuffer.LayerBefore = layerBefore;
        currentBuffer.LayerAfter = layerAfter;
        currentBuffer.ActionType = PlayerActionType.UnitAction;
        currentBuffer.MovementPath = movementPath;
    }

    public void UpdateCurrentBufferSensorAction(SensorActionType sensorAction, string subStepLabel = null)
    {
        if (isReplaying)
            return;

        currentBuffer.SensorAction = sensorAction;
        if (!string.IsNullOrWhiteSpace(subStepLabel))
            currentBuffer.SubStepLabel = subStepLabel;
        currentBuffer.ActionType = PlayerActionType.UnitAction;
    }

    public void UpdateCurrentBufferTarget(UnitManager targetUnit, ConstructionManager targetConstruction, Vector3Int targetHex, string subStepLabel = null)
    {
        if (isReplaying)
            return;

        currentBuffer.TargetInstanceId = targetUnit != null ? targetUnit.InstanceId.ToString() : null;
        currentBuffer.TargetConstructionId = targetConstruction != null ? targetConstruction.InstanceId.ToString() : null;
        currentBuffer.TargetHex = targetHex;
        currentBuffer.HasTargetHex = true;
        if (!string.IsNullOrWhiteSpace(subStepLabel))
            currentBuffer.SubStepLabel = subStepLabel;
        if (ShouldAppendCurrentBufferSubStep(subStepLabel))
            AppendCurrentBufferSubStep(subStepLabel);
        currentBuffer.ActionType = PlayerActionType.UnitAction;
    }

    private bool ShouldAppendCurrentBufferSubStep(string label)
    {
        if (currentBuffer == null)
            return false;
        if (string.IsNullOrWhiteSpace(label))
            return false;

        switch (currentBuffer.SensorAction)
        {
            case SensorActionType.Merge:
            case SensorActionType.Supply:
                return label.IndexOf("QueueConfirm", StringComparison.OrdinalIgnoreCase) >= 0;
            default:
                return label.IndexOf("TargetConfirm", StringComparison.OrdinalIgnoreCase) >= 0
                       || label.IndexOf("QueueConfirm", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
    private void AppendCurrentBufferSubStep(string label)
    {
        if (currentBuffer == null)
            return;

        if (currentBuffer.SubSteps == null)
            currentBuffer.SubSteps = new List<PlayerActionSubStep>();

        PlayerActionSubStep step = new PlayerActionSubStep
        {
            Label = label,
            TargetInstanceId = currentBuffer.TargetInstanceId,
            TargetConstructionId = currentBuffer.TargetConstructionId,
            TargetHex = currentBuffer.TargetHex,
            HasTargetHex = currentBuffer.HasTargetHex
        };

        int lastIndex = currentBuffer.SubSteps.Count - 1;
        if (lastIndex >= 0)
        {
            PlayerActionSubStep last = currentBuffer.SubSteps[lastIndex];
            bool sameTargetHex = (!step.HasTargetHex && !last.HasTargetHex) || (step.HasTargetHex && last.HasTargetHex && step.TargetHex == last.TargetHex);
            bool duplicate =
                string.Equals(last.Label, step.Label, StringComparison.Ordinal)
                && string.Equals(last.TargetInstanceId, step.TargetInstanceId, StringComparison.Ordinal)
                && string.Equals(last.TargetConstructionId, step.TargetConstructionId, StringComparison.Ordinal)
                && sameTargetHex;
            if (duplicate)
                return;
        }

        currentBuffer.SubSteps.Add(step);
    }
    public void RecordStandaloneAction(PlayerAction action)
    {
        if (isReplaying || action == null)
            return;

        if (actionStack == null)
            actionStack = new ActionStack();

        if (!action.HasCursorHex && (action.CursorHex != Vector3Int.zero || !string.IsNullOrWhiteSpace(action.UnitInstanceId) || !string.IsNullOrWhiteSpace(action.TargetInstanceId) || !string.IsNullOrWhiteSpace(action.TargetConstructionId)))
            action.HasCursorHex = true;

        if (!action.HasMoveFrom && action.MoveFrom != Vector3Int.zero)
            action.HasMoveFrom = true;

        if (!action.HasMoveTo && action.MoveTo != Vector3Int.zero)
            action.HasMoveTo = true;

        if (!action.HasTargetHex && (action.TargetHex != Vector3Int.zero || !string.IsNullOrWhiteSpace(action.TargetInstanceId) || !string.IsNullOrWhiteSpace(action.TargetConstructionId)))
            action.HasTargetHex = true;

        actionStack.Actions.Add(action);

        if (!isRecording || currentRecord == null || currentRecord.StartSnapshot == null)
            return;

        if (!BelongsToCurrentRecord(action))
            return;

        int actionIndex = ResolveCurrentRecordActionCount() - 1;
        if (actionIndex >= 0)
        {
            TurnStartSnapshot postActionSnapshot = BuildTurnStartSnapshot("RecordStandaloneAction.PostStep");
            action.Snapshot = postActionSnapshot;
            if (postActionSnapshot != null)
                stepSnapshots[actionIndex] = postActionSnapshot;
        }
    }

    public void PromoteCurrentBuffer(string debugLabel)
    {
        if (isReplaying)
            return;

        if (currentBuffer == null)
            currentBuffer = new PlayerAction();

        currentBuffer.Confirmed = true;
        currentBuffer.DebugLabel = string.IsNullOrWhiteSpace(debugLabel) ? currentBuffer.DebugLabel : debugLabel;

        if (currentBuffer.TurnNumber == 0 && matchController != null)
            currentBuffer.TurnNumber = matchController.CurrentTurn;
        if (matchController != null)
            currentBuffer.ActingTeam = matchController.ActiveTeam;

        RecordStandaloneAction(currentBuffer);

        if (!currentBuffer.IsAIGenerated && !isLiveAIBatchExecution)
            JogadasManager.RegistrarPlayerAction(currentBuffer);

        currentBuffer = new PlayerAction();
    }

    public void DiscardCurrentBuffer(string reason)
    {
        currentBuffer = new PlayerAction();
    }

    public bool StepForward()
    {
        if (!isReplaying || currentRecord == null || currentRecord.StartSnapshot == null)
            return false;
        if (IsReplayStepExecutionBusy())
            return false;

        int batchCount = ResolveCurrentReplayBatchCount();
        if (batchCount <= 0)
            return false;

        int targetIndex = currentStepIndex + 1;
        if (targetIndex >= batchCount)
            return false;

        isPlaying = false;
        return NavigateToSnapshotIndex(targetIndex);
    }


    public bool StepBackward()
    {
        if (!isReplaying || currentRecord == null || currentRecord.StartSnapshot == null)
            return false;
        if (IsReplayStepExecutionBusy())
            return false;

        if (currentStepIndex < 0)
            return false;

        isPlaying = false;

        int targetIndex = currentStepIndex - 1;
        return NavigateToSnapshotIndex(targetIndex);
    }
    public void TogglePlayPause()
    {
        if (!isReplaying)
            return;

        if (isPlaying)
            PausePlayback();
        else
            ResumePlayback();
    }

    public void PausePlayback()
    {
        isPlaying = false;
        StopAutoplayAdvanceRetryRoutine();
        automatedPlayer?.Pause();
    }

    public void ResumePlayback()
    {
        if (!isReplaying)
            return;
        if (IsReplayStepExecutionBusy())
            return;

        int batchCount = ResolveCurrentReplayBatchCount();
        if (batchCount <= 0)
            return;
        if (currentStepIndex >= batchCount - 1)
            return;

        isPlaying = true;
        TryAutoAssignReferences();

        if (automatedPlayer != null)
        {
            automatedPlayer.StartPlaying();
            return;
        }

        // Fallback de seguranca: dispara o primeiro batch mesmo sem referencia do AutomatedPlayer.
        ExecuteNextReplayBatch();
    }

    public void SetFastReplayMode(bool enabled)
    {
        fastReplayMode = enabled;
    }

    private bool isLiveAIBatchExecution = false;
    private bool liveAIFastMode = true;
    private bool IsFastPresentation => fastReplayMode || (isLiveAIBatchExecution && liveAIFastMode);
    private bool IsLiveAIPresentationMode => isLiveAIBatchExecution && !liveAIFastMode;
    private bool IsLiveAIFastMode => isLiveAIBatchExecution && liveAIFastMode;

    public void ExecuteLiveAIBatch(PlayerAction action, bool fastAI = true)
    {
        if (action == null || actionStepExecutionRoutine != null)
            return;

        liveAIFastMode = fastAI;
        actionStepExecutionRoutine = StartCoroutine(ExecuteLiveAIBatchRoutine(action));
    }

    private IEnumerator ExecuteLiveAIBatchRoutine(PlayerAction action)
    {
        replayBatchAbortRequested = false;
        isLiveAIBatchExecution = true;
        try
        {
            bool canEmulateAction = action != null && CanReplayActionAsLiveInputs(action.ActionType);
            if (canEmulateAction)
                yield return ExecuteRecordedActionBatch(action, null);
        }
        finally
        {
            actionStepExecutionRoutine = null;
            isLiveAIBatchExecution = false;
            liveAIFastMode = true;
        }
    }

    public bool ExecuteNextReplayBatch()
    {
        if (!isReplaying || !isPlaying || currentRecord == null)
            return false;
        if (IsReplayStepExecutionBusy())
            return false;

        int nextIndex = currentStepIndex + 1;
        if (!ExecuteStepAtIndex(nextIndex, allowCinematic: true, out bool startedAsyncExecution))
            return false;

        if (!startedAsyncExecution)
        {
            ApplyReplayVision();
            bool reachedLastBatch = currentStepIndex >= ResolveCurrentReplayBatchCount() - 1;
            if (reachedLastBatch)
                isPlaying = false;
            else if (isPlaying)
                RequestAutoplayAdvance("step-finished-sync");
        }

        return true;
    }


    private void RequestAutoplayAdvance(string source)
    {
        if (!isReplaying || !isPlaying)
            return;

        if (autoplayAdvanceRetryRoutine != null)
            return;

        autoplayAdvanceRetryRoutine = StartCoroutine(AutoplayAdvanceWhenReady(source));
    }

    private IEnumerator AutoplayAdvanceWhenReady(string source)
    {
        while (isReplaying && isPlaying &&
               (IsReplayStepExecutionBusy() ||
                (turnStateManager != null && turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)))
        {
            yield return null;
        }

        float delay = GetEffectiveTimeBetweenBatches();
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        bool started = false;
        if (isReplaying && isPlaying)
            started = ExecuteNextReplayBatch();

        ReplayLog($"[Replay][Autoplay] {source} advance started={started} currentStep={currentStepIndex}");
        autoplayAdvanceRetryRoutine = null;
    }

    private void StopAutoplayAdvanceRetryRoutine()
    {
        if (autoplayAdvanceRetryRoutine == null)
            return;

        StopCoroutine(autoplayAdvanceRetryRoutine);
        autoplayAdvanceRetryRoutine = null;
    }
    private bool NavigateToSnapshotIndex(int targetIndex)
    {
        if (currentRecord == null || currentRecord.StartSnapshot == null)
            return false;

        // Manual snapshot navigation must never keep autoplay side-effects alive.
        isPlaying = false;
        StopAutoplayAdvanceRetryRoutine();
        automatedPlayer?.Pause();

        if (targetIndex < 0)
        {
            RestoreSnapshot(currentRecord.StartSnapshot);
            currentStepIndex = -1;
            ApplyReplayVision();
                return true;
        }

        if (stepSnapshots.TryGetValue(targetIndex, out TurnStartSnapshot stepSnapshot) && stepSnapshot != null)
        {
            RestoreSnapshot(stepSnapshot);
            currentStepIndex = targetIndex;
            ApplyReplayVision();
                return true;
        }

        TurnStartSnapshot actionSnapshot = TryResolveSnapshotForCurrentRecordActionIndex(targetIndex, cacheWhenFound: true);
        if (actionSnapshot != null)
        {
            RestoreSnapshot(actionSnapshot);
            currentStepIndex = targetIndex;
            ApplyReplayVision();
                return true;
        }

        ReplayLogWarning($"[Replay][SnapshotNav] snapshot ausente para targetStep={targetIndex} | cachedSnapshots={stepSnapshots.Count}");
        return false;
    }

    private bool ExecuteStepAtIndex(int index, bool allowCinematic, out bool startedAsyncExecution)
    {
        startedAsyncExecution = false;

        int replayBatchCount = ResolveCurrentReplayBatchCount();
        if (currentRecord == null || replayBatchCount <= 0)
            return false;
        if (index < 0 || index >= replayBatchCount)
            return false;

        PlayerAction action = TryResolveCurrentRecordActionByIndex(index);
        if (action != null)
        {
            TurnStartSnapshot postActionSnapshot = TryResolveSnapshotForCurrentRecordActionIndex(index, cacheWhenFound: true);
            if (allowCinematic)
            {
                TurnStartSnapshot preActionSnapshot = TryResolvePreActionSnapshotForCurrentRecordActionIndex(index);
                actionStepExecutionRoutine = StartCoroutine(ExecuteActionStepFromStack(index, action, preActionSnapshot, postActionSnapshot));
                startedAsyncExecution = true;
                return true;
            }

            if (postActionSnapshot != null)
            {
                RestoreSnapshot(postActionSnapshot);
                currentStepIndex = index;
                return true;
            }

            return false;
        }

        bool hasStepCommands = currentRecord.Steps != null && currentRecord.Steps.Count > 0;
        if (!hasStepCommands)
            return false;

        ReplayStep step = currentRecord.Steps[index];
        if (step == null || step.Command == null)
            return false;

        if (step.StepType != ReplayStepType.MoveUnit
            && step.StepType != ReplayStepType.Attack
            && step.StepType != ReplayStepType.BuyUnit
            && step.StepType != ReplayStepType.Capture
            && step.StepType != ReplayStepType.Embark
            && step.StepType != ReplayStepType.Disembark
            && step.StepType != ReplayStepType.Merge
            && step.StepType != ReplayStepType.Supply)
            return false;

        if (allowCinematic
            && step.StepType == ReplayStepType.Attack
            && step.Command is AttackReplayCommand attackCommand
            && attackCommand.CinematicTrack != null
            && attackCommand.CinematicTrack.Events != null
            && attackCommand.CinematicTrack.Events.Count > 0)
        {
            attackStepExecutionRoutine = StartCoroutine(ExecuteAttackStepWithCinematic(index, step, attackCommand));
            startedAsyncExecution = true;
            return true;
        }

        ReplayExecutionContext context = BuildExecutionContext();
        step.Command.Execute(context);
        currentStepIndex = index;
        return true;
    }

    private IEnumerator ExecuteAttackStepWithCinematic(int index, ReplayStep step, AttackReplayCommand attackCommand)
    {
        yield return PlayCinematicTrack(attackCommand != null ? attackCommand.CinematicTrack : null);

        if (step != null && step.Command != null)
        {
            ReplayExecutionContext context = BuildExecutionContext();
            // Durante cinematica, o combate ja foi apresentado pelo FSM.
            step.Command.Execute(context);
            currentStepIndex = index;
            ApplyReplayVision();
            if (currentStepIndex >= ResolveCurrentReplayBatchCount() - 1)
                isPlaying = false;
        }

        attackStepExecutionRoutine = null;
    }

    public IEnumerator PlayCinematicTrack(CinematicTrack track)
    {
        TryAutoAssignReferences();
        if (track == null || track.Events == null || track.Events.Count <= 0)
            yield break;

        for (int i = 0; i < track.Events.Count; i++)
        {
            CinematicEvent cinematicEvent = track.Events[i];
            if (cinematicEvent == null)
                continue;

            Vector3Int cursorHex = cinematicEvent.CursorHex;
            cursorHex.z = 0;
            if (cursorController != null)
            {
                cursorController.SetCell(cursorHex, playMoveSfx: animateCursorTravelBetweenActions, adjustCamera: false);
                cursorController.TryAdjustCameraToCursor();
            }

            if (!IsFastPresentation && animateCursorTravelBetweenActions && cursorTravelStepDelay > 0f)
                yield return new WaitForSecondsRealtime(cursorTravelStepDelay);


            bool isLastEvent = i == track.Events.Count - 1;
            bool skipTrailingConfirm = isLastEvent && cinematicEvent.Action == CinematicAction.Confirm;

            if (!skipTrailingConfirm && cinematicEvent.Action == CinematicAction.Confirm)
                ExecuteReplayConfirmInput();
            else if (!skipTrailingConfirm && cinematicEvent.Action == CinematicAction.AimAction)
                turnStateManager?.HandleAimActionRequested();

            yield return null;

            float delay = IsFastPresentation ? 0f : Mathf.Max(0f, cinematicEvent.DelayAfter);
            if (!IsFastPresentation && !skipTrailingConfirm && (cinematicEvent.Action == CinematicAction.Confirm || cinematicEvent.Action == CinematicAction.AimAction))
                delay = Mathf.Max(delay, replayConfirmVisualDelay);

            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }
    }
    private float GetEffectiveCursorTravelStepDelay()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0f, cursorTravelStepDelay);
    }

    private float GetEffectiveShoppingNavDelay()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0f, shoppingNavDelay);
    }

    private float GetEffectiveShoppingMenuOpenDelay()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0f, shoppingMenuOpenDelay);
    }

    private float GetEffectivePlayerMenuStepDelay()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0f, playerMenuStepDelay);
    }

    private BattleMapMenuRootController GetBattleMapMenu()
    {
        if (cachedBattleMapMenu != null) return cachedBattleMapMenu;
        BattleMapMenuRootController[] all = FindObjectsByType<BattleMapMenuRootController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null) { cachedBattleMapMenu = all[i]; break; }
        }
        return cachedBattleMapMenu;
    }
    private float GetEffectiveSensorSubstepDelay()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0f, sensorSubstepDelay);
    }

    private float GetEffectiveUnitSelectionHoldDelay()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0f, unitSelectionHoldDelay);
    }

    private float GetEffectiveBeforeConfirmDelay()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0f, beforeConfirmDelay);
    }

    private float GetEffectiveSensorListNavDelay()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0f, sensorListNavDelay);
    }

    private IEnumerator WaitForSensorSubstepDelay()
    {
        float delay = GetEffectiveSensorSubstepDelay();
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
        else
            yield return null;
    }

    private IEnumerator WaitForAIPresentationStage()
    {
        // Garante ao menos um frame para o panel_helper observar o novo estado, mesmo
        // quando o delay configurado estiver em zero.
        yield return null;
        float delay = GetEffectiveBeforeConfirmDelay();
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
    }

    private IEnumerator NavigateSensorMenuForAIPresentation(SensorActionType action)
    {
        if (turnStateManager == null || !TryGetSensorMenuCode(action, out char targetCode))
            yield break;

        const int navigationGuard = 16;
        int steps = 0;
        while (turnStateManager.SensorOptionFocusCode != targetCode && steps < navigationGuard)
        {
            if (!turnStateManager.NavigateSensorOptionFocus(+1))
                yield break;

            steps++;
            yield return null;

            float delay = GetEffectiveSensorListNavDelay();
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }
    }

    private static bool TryGetSensorMenuCode(SensorActionType action, out char code)
    {
        switch (action)
        {
            case SensorActionType.None: code = 'M'; return true;
            case SensorActionType.Attack: code = 'A'; return true;
            case SensorActionType.Embark: code = 'E'; return true;
            case SensorActionType.Disembark: code = 'D'; return true;
            case SensorActionType.Capture: code = 'C'; return true;
            case SensorActionType.Merge: code = 'F'; return true;
            case SensorActionType.Supply: code = 'S'; return true;
            case SensorActionType.Transfer: code = 'T'; return true;
            default:
                code = '\0';
                return false;
        }
    }
    private float GetEffectiveReplayConfirmVisualDelay()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0f, replayConfirmVisualDelay);
    }

    private float GetEffectiveTimeBetweenBatches()
    {
        return IsFastPresentation ? 0f : Mathf.Max(0.01f, timeBetweenBatches);
    }

    private int ResolveCurrentReplayBatchCount()
    {
        if (currentRecord == null)
            return 0;

        int actionCount = ResolveCurrentRecordActionCount();
        if (actionCount > 0)
            return actionCount;

        int stepCount = currentRecord.Steps != null ? currentRecord.Steps.Count : 0;
        return stepCount;
    }

    private int ResolveCurrentRecordActionCount()
    {
        if (currentRecord == null || actionStack == null || actionStack.Actions == null || actionStack.Actions.Count <= 0)
            return 0;

        int count = 0;
        for (int i = 0; i < actionStack.Actions.Count; i++)
        {
            PlayerAction action = actionStack.Actions[i];
            if (BelongsToCurrentRecord(action))
                count++;
        }

        return count;
    }

    private PlayerAction TryResolveCurrentRecordActionByIndex(int actionIndex)
    {
        if (actionIndex < 0 || currentRecord == null || actionStack == null || actionStack.Actions == null)
            return null;

        int currentActionIndex = 0;
        for (int i = 0; i < actionStack.Actions.Count; i++)
        {
            PlayerAction action = actionStack.Actions[i];
            if (!BelongsToCurrentRecord(action))
                continue;

            if (currentActionIndex == actionIndex)
                return action;

            currentActionIndex++;
        }

        return null;
    }

    private void RebuildStepSnapshotsForCurrentRecordFromActionSnapshots()
    {
        stepSnapshots.Clear();

        if (currentRecord == null || actionStack == null || actionStack.Actions == null)
            return;

        int actionIndex = 0;
        for (int i = 0; i < actionStack.Actions.Count; i++)
        {
            PlayerAction action = actionStack.Actions[i];
            if (!BelongsToCurrentRecord(action))
                continue;

            if (action != null && action.Snapshot != null)
                stepSnapshots[actionIndex] = action.Snapshot;

            actionIndex++;
        }
    }

    private TurnStartSnapshot TryResolveSnapshotForCurrentRecordActionIndex(int actionIndex, bool cacheWhenFound)
    {
        if (actionIndex < 0 || currentRecord == null)
            return null;
        if (stepSnapshots.TryGetValue(actionIndex, out TurnStartSnapshot cachedSnapshot) && cachedSnapshot != null)
            return cachedSnapshot;

        PlayerAction action = TryResolveCurrentRecordActionByIndex(actionIndex);
        TurnStartSnapshot snapshot = action != null ? action.Snapshot : null;
        if (cacheWhenFound && snapshot != null)
            stepSnapshots[actionIndex] = snapshot;
        return snapshot;
    }

    private TurnStartSnapshot TryResolvePreActionSnapshotForCurrentRecordActionIndex(int actionIndex)
    {
        if (currentRecord == null)
            return null;

        if (actionIndex <= 0)
            return currentRecord.StartSnapshot;

        return TryResolveSnapshotForCurrentRecordActionIndex(actionIndex - 1, cacheWhenFound: true);
    }

    private bool TryResolveActionPreExecutionCursorCell(PlayerAction action, TurnStartSnapshot snapshot, out Vector3Int cursorCell)
    {
        cursorCell = Vector3Int.zero;
        if (action == null)
            return false;

        if (TryResolveRecordedOriginCell(action, snapshot, out cursorCell))
        {
            cursorCell = NormalizeCell(cursorCell);
            return true;
        }

        if (snapshot != null && snapshot.HasCursorCell)
        {
            cursorCell = NormalizeCell(snapshot.CursorCell);
            return true;
        }

        return false;
    }

    private static Vector3Int NormalizeCell(Vector3Int cell)
    {
        cell.z = 0;
        return cell;
    }

    private bool BelongsToCurrentRecord(PlayerAction action)
    {
        if (action == null || currentRecord == null)
            return false;

        return action.TurnNumber == currentRecord.TurnNumber
               && action.ActingTeam == currentRecord.ActingTeam;
    }
    public void RestoreSnapshot(TurnStartSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        TryAutoAssignReferences();
        if (unitSpawner == null || constructionSpawner == null || matchController == null)
            return;

        turnStateManager?.ResetCommandServiceReplayTransientState();

        EnsureReplayPoolsInitialized();
        ClearCurrentRuntime();
        ResetEmbarkStateInPools(SceneManager.GetActiveScene());

        Dictionary<int, UnitManager> unitsById = new Dictionary<int, UnitManager>();
        List<(ConstructionManager manager, bool isActive)> restoredConstructions = new List<(ConstructionManager, bool)>();
        List<(UnitManager manager, UnitSaveData saved)> restoredUnits = new List<(UnitManager, UnitSaveData)>();
        int maxUnitId = 0;
        int maxConstructionId = 0;

        if (snapshot.Constructions != null)
        {
            for (int i = 0; i < snapshot.Constructions.Count; i++)
            {
                ConstructionSaveData saved = snapshot.Constructions[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.constructionId))
                    continue;

                ConstructionManager manager = null;
                bool reused = saved.instanceId > 0
                    && replayConstructionPool.TryGetValue(saved.instanceId, out manager)
                    && manager != null
                    && manager.gameObject.scene == SceneManager.GetActiveScene();

                if (!reused)
                {
                    if (!constructionSpawner.TryGetConstructionData(saved.constructionId, out ConstructionData constructionData) || constructionData == null)
                        continue;

                    Vector3 world = new Vector3(saved.worldX, saved.worldY, 0f);
                    GameObject go = constructionSpawner.Spawn(constructionData, (TeamId)saved.teamId, world, Quaternion.identity);
                    if (go == null)
                        continue;

                    manager = go.GetComponent<ConstructionManager>();
                    if (manager == null)
                        continue;
                }

                if (!manager.gameObject.activeSelf)
                    manager.gameObject.SetActive(true);

                SaveDataMapper.ApplyConstructionSaveData(manager, saved, BuildSiteRuntimeFromSaveData);
                RegisterConstructionInPool(manager);
                restoredConstructions.Add((manager, saved.isActiveInHierarchy));
                if (saved.instanceId > maxConstructionId)
                    maxConstructionId = saved.instanceId;
            }
        }

        if (snapshot.Units != null)
        {
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                UnitSaveData saved = snapshot.Units[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.unitId))
                    continue;

                UnitManager manager = null;
                bool reused = saved.instanceId > 0
                    && replayUnitPool.TryGetValue(saved.instanceId, out manager)
                    && manager != null
                    && manager.gameObject.scene == SceneManager.GetActiveScene();

                if (!reused)
                {
                    if (!unitSpawner.TryGetUnitData(saved.unitId, out UnitData unitData) || unitData == null)
                        continue;

                    Vector3 world = new Vector3(saved.worldX, saved.worldY, 0f);
                    GameObject go = unitSpawner.Spawn(unitData, (TeamId)saved.teamId, world, Quaternion.identity);
                    if (go == null)
                        continue;

                    manager = go.GetComponent<UnitManager>();
                    if (manager == null)
                        continue;
                }

                if (!manager.gameObject.activeSelf)
                    manager.gameObject.SetActive(true);

                SaveDataMapper.ApplyUnitSaveData(manager, saved);
                RegisterUnitInPool(manager);
                if (!reused && isReplaying && saved.instanceId > 0)
                    replaySpawnedUnits.Add(manager);
                unitsById[saved.instanceId] = manager;
                restoredUnits.Add((manager, saved));

                if (saved.instanceId > maxUnitId)
                    maxUnitId = saved.instanceId;
            }
        }

        if (snapshot.Units != null)
        {
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                UnitSaveData saved = snapshot.Units[i];
                if (saved == null || !saved.isEmbarked || saved.transporterInstanceId <= 0)
                    continue;

                if (!unitsById.TryGetValue(saved.instanceId, out UnitManager passenger) || passenger == null)
                    continue;
                if (!unitsById.TryGetValue(saved.transporterInstanceId, out UnitManager transporter) || transporter == null)
                    continue;

                transporter.TryEmbarkPassengerInSlot(passenger, saved.transporterSlotIndex, out _);
            }
        }

        unitSpawner.SetNextIdAfterMax(maxUnitId);
        constructionSpawner.SetNextIdAfterMax(maxConstructionId);

        MatchStateSaveData matchState = snapshot.MatchState ?? new MatchStateSaveData();
        SaveDataMapper.ApplyMatchStateSaveData(matchController, matchState);
        matchController.SetEconomyEnabled(matchState.economyEnabled);
        matchController.SetCurrentTurn(Mathf.Max(0, matchState.currentTurn));
        matchController.SetActiveTeamId(matchState.activeTeamId);
        SaveDataMapper.ApplyMatchStateSaveData(matchController, matchState);
        // Evita drift visual de renda ao navegar snapshots (captura/reversao/avance).
        matchController.RefreshIncomeFromConstructionsNow();

        for (int i = 0; i < restoredUnits.Count; i++)
        {
            (UnitManager manager, UnitSaveData saved) = restoredUnits[i];
            if (manager == null || saved == null)
                continue;

            SaveDataMapper.ApplyUnitTurnFlagsFromSaveData(manager, saved);
        }

        for (int i = 0; i < restoredConstructions.Count; i++)
        {
            (ConstructionManager manager, bool isActive) = restoredConstructions[i];
            if (manager != null)
                SetReplayObjectActive(manager.gameObject, isActive);
        }

        for (int i = 0; i < restoredUnits.Count; i++)
        {
            (UnitManager manager, UnitSaveData saved) = restoredUnits[i];
            if (manager != null && saved != null)
                SetReplayObjectActive(manager.gameObject, saved.isActiveInHierarchy);
        }

        if (snapshot.HasCursorCell && cursorController != null)
            cursorController.SetCell(NormalizeCell(snapshot.CursorCell), playMoveSfx: false, adjustCamera: false);

        QueueFogRefreshForNextFrame();
    }

    private ReplayExecutionContext BuildExecutionContext()
    {
        TryAutoAssignReferences();

        return new ReplayExecutionContext
        {
            MatchController = matchController,
            UnitManager = null,
            ConstructionManager = null,
            FogOfWarController = fogOfWarController,
            AnimationManager = animationManager,
            CursorController = cursorController,
            IsReplayMode = isReplaying,
            VisionMode = visionMode,
            ObserverTeam = observerTeam
        };
    }

    private bool DoesCurrentRecordMatchRuntimeTurn()
    {
        if (currentRecord == null || currentRecord.StartSnapshot == null || matchController == null)
            return false;

        int runtimeTurn = matchController.CurrentTurn;
        TeamId runtimeTeam = matchController.ActiveTeam;

        int recordedTurn = currentRecord.StartSnapshot.TurnNumber > 0
            ? currentRecord.StartSnapshot.TurnNumber
            : currentRecord.TurnNumber;

        TeamId recordedTeam = currentRecord.StartSnapshot.ActiveTeam;
        if (recordedTeam == TeamId.Neutral && currentRecord.ActingTeam != TeamId.Neutral)
            recordedTeam = currentRecord.ActingTeam;

        return recordedTurn == runtimeTurn && recordedTeam == runtimeTeam;
    }

    private bool IsReadyForTurnStartSnapshot(bool requireInitializedTurn)
    {
        if (!Application.isPlaying || isReplaying)
            return false;

        TryAutoAssignReferences();

        if (matchController == null)
            return false;

        if (requireInitializedTurn && matchController.CurrentTurn <= 0)
            return false;

        if (turnStateManager != null && turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
            return false;

        if (animationManager != null && animationManager.IsAnimatingMovement)
            return false;

        if (turnStateManager != null && turnStateManager.IsScannerActionExecutionInProgress)
            return false;

        return true;
    }
    private void HandleActiveTeamChanged(int _)
    {
        if (!isActiveAndEnabled)
            return;

        if (delayedBeginTurnRecordingRoutine != null)
            StopCoroutine(delayedBeginTurnRecordingRoutine);

        delayedBeginTurnRecordingRoutine = StartCoroutine(BeginTurnRecordingNextFrame());
    }

    private IEnumerator BeginTurnRecordingNextFrame()
    {
        yield return null;

        bool hasLoadedStartSnapshot = currentRecord != null
            && currentRecord.StartSnapshot != null
            && DoesCurrentRecordMatchRuntimeTurn();
        bool requireInitializedTurn = !hasLoadedStartSnapshot;

        const float maxWaitSeconds = 15f;
        float waitedSeconds = 0f;
        while (isActiveAndEnabled && !IsReadyForTurnStartSnapshot(requireInitializedTurn))
        {
            yield return null;
            waitedSeconds += Mathf.Max(0f, Time.unscaledDeltaTime);
            if (waitedSeconds >= maxWaitSeconds)
            {
                ReplayLogWarning("[Replay] Timeout waiting for neutral/startup readiness before BeginTurnRecording.");
                break;
            }
        }

        delayedBeginTurnRecordingRoutine = null;

        if (!isActiveAndEnabled)
            yield break;

        BeginTurnRecording();
    }

    private void HandleBeforeAdvanceTurn()
    {
        EndTurnRecording();
    }

    private TurnStartSnapshot BuildTurnStartSnapshot(string reason = null)
    {
        Stopwatch stopwatch = null;
        if (snapshotTelemetryEnabled)
            stopwatch = Stopwatch.StartNew();

        TryAutoAssignReferences();
        // Garante coerencia entre ownership de construcoes e income salvo no snapshot.
        matchController?.RefreshIncomeFromConstructionsNow();

        MatchStateSaveData matchState = SaveDataMapper.BuildMatchStateSaveData(matchController);
        TurnStartSnapshot snapshot = new TurnStartSnapshot
        {
            TurnNumber = matchController != null ? matchController.CurrentTurn : 0,
            ActiveTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral,
            MatchState = matchState
        };

        if (cursorController != null)
        {
            Vector3Int cursorCell = cursorController.CurrentCell;
            cursorCell.z = 0;
            snapshot.CursorCell = cursorCell;
            snapshot.HasCursorCell = true;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        HashSet<int> unitIdsInSnapshot = new HashSet<int>();

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;
            if (unit.gameObject.scene != activeScene)
                continue;

            UnitSaveData item = SaveDataMapper.BuildUnitSaveData(unit);
            if (item != null)
            {
                snapshot.Units.Add(item);
                if (item.instanceId > 0)
                    unitIdsInSnapshot.Add(item.instanceId);
            }

            AppendEmbarkedPassengersRecursive(unit, snapshot.Units, unitIdsInSnapshot, activeScene);
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if (construction.gameObject.scene != activeScene)
                continue;

            ConstructionSaveData item = SaveDataMapper.BuildConstructionSaveData(construction);
            if (item != null)
                snapshot.Constructions.Add(item);
        }

        if (snapshotTelemetryEnabled && stopwatch != null)
        {
            stopwatch.Stop();
            double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            snapshotTelemetryCount++;
            snapshotTelemetryTotalMs += elapsedMs;
            if (elapsedMs < snapshotTelemetryMinMs)
                snapshotTelemetryMinMs = elapsedMs;
            if (elapsedMs > snapshotTelemetryMaxMs)
                snapshotTelemetryMaxMs = elapsedMs;

            int logEvery = Mathf.Max(1, snapshotTelemetryLogEvery);
            if (snapshotTelemetryCount % logEvery == 0)
            {
                double avg = snapshotTelemetryCount > 0 ? snapshotTelemetryTotalMs / snapshotTelemetryCount : 0d;
                string context = snapshotTelemetryVerboseContext
                    ? $" | reason={reason ?? "(none)"}"
                    : string.Empty;
                ReplayLog($"[Replay][SnapshotTelemetry] count={snapshotTelemetryCount} avg={avg:F2}ms min={snapshotTelemetryMinMs:F2}ms max={snapshotTelemetryMaxMs:F2}ms{context}");
            }
        }

        return snapshot;
    }
    private void AppendEmbarkedPassengersRecursive(
        UnitManager transporter,
        List<UnitSaveData> targetUnits,
        HashSet<int> knownInstanceIds,
        Scene activeScene)
    {
        if (transporter == null || targetUnits == null || knownInstanceIds == null)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null || seats.Count <= 0)
            return;

        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            UnitManager passenger = seat != null ? seat.embarkedUnit : null;
            if (passenger == null)
                continue;
            if (passenger.gameObject.scene != activeScene)
                continue;

            int id = passenger.InstanceId;
            if (id <= 0 || !knownInstanceIds.Add(id))
            {
                AppendEmbarkedPassengersRecursive(passenger, targetUnits, knownInstanceIds, activeScene);
                continue;
            }

            UnitSaveData saved = SaveDataMapper.BuildUnitSaveData(passenger);
            if (saved != null)
                targetUnits.Add(saved);

            AppendEmbarkedPassengersRecursive(passenger, targetUnits, knownInstanceIds, activeScene);
        }
    }

    private static string FormatReplayCell(Vector3Int cell)
    {
        return $"({cell.x},{cell.y},{cell.z})";
    }

    private static void SetReplayObjectActive(GameObject go, bool active)
    {
        if (go == null || go.activeSelf == active)
            return;

#if UNITY_EDITOR
        if (!active)
        {
            GameObject selectedGo = Selection.activeGameObject;
            if (selectedGo != null && (selectedGo == go || selectedGo.transform.IsChildOf(go.transform) || go.transform.IsChildOf(selectedGo.transform)))
                Selection.activeObject = null;
        }
#endif

        go.SetActive(active);
    }

    private void TryAutoAssignReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (unitSpawner == null)
            unitSpawner = FindAnyObjectByType<UnitSpawner>();
        if (constructionSpawner == null)
            constructionSpawner = FindAnyObjectByType<ConstructionSpawner>();
        if (fogOfWarController == null)
            fogOfWarController = FindAnyObjectByType<FogOfWarController>();
        if (animationManager == null)
            animationManager = FindAnyObjectByType<AnimationManager>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (automatedPlayer == null)
            automatedPlayer = FindAnyObjectByType<AutomatedPlayer>();
    }
    private void ClearCurrentRuntime()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        EnsureReplayPoolsInitialized();

        DeactivateAllPooledRuntime(activeScene);

        // Hard reset da cena: AllActive pode perder objetos em estados transitorios
        // (ex.: recem-spawnados no mesmo frame). Varremos tudo da cena ativa.
        UnitManager[] allSceneUnits = Resources.FindObjectsOfTypeAll<UnitManager>();
        for (int i = 0; i < allSceneUnits.Length; i++)
        {
            UnitManager unit = allSceneUnits[i];
            if (unit == null || unit.gameObject.scene != activeScene)
                continue;

            RegisterUnitInPool(unit);
            if (unit.gameObject.activeSelf)
                SetReplayObjectActive(unit.gameObject, false);
        }

        ConstructionManager[] allSceneConstructions = Resources.FindObjectsOfTypeAll<ConstructionManager>();
        for (int i = 0; i < allSceneConstructions.Length; i++)
        {
            ConstructionManager construction = allSceneConstructions[i];
            if (construction == null || construction.gameObject.scene != activeScene)
                continue;

            RegisterConstructionInPool(construction);
            if (construction.gameObject.activeSelf)
                SetReplayObjectActive(construction.gameObject, false);
        }
    }
    private void ApplyReplayVision()
    {
        if (!isReplaying)
            return;

        if (visionMode == ReplayVisionMode.Omniscient)
        {
            IReadOnlyList<UnitManager> units = UnitManager.AllActive;
            for (int i = 0; i < units.Count; i++)
            {
                UnitManager unit = units[i];
                if (unit == null)
                    continue;

                unit.SetFogOfWarVisibility(true);
            }

            return;
        }

        fogOfWarController?.RefreshFogOfWarForTeam(observerTeam);
    }

    private void EnsureReplayPoolsInitialized()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (replayPoolsInitialized && replayPoolSceneHandle == activeScene.handle)
            return;

        replayUnitPool.Clear();
        replayConstructionPool.Clear();
        replaySpawnedUnits.Clear();

        IReadOnlyList<UnitManager> activeUnits = UnitManager.AllActive;
        for (int i = 0; i < activeUnits.Count; i++)
        {
            UnitManager unit = activeUnits[i];
            if (unit == null || unit.gameObject.scene != activeScene)
                continue;
            if (unit.InstanceId > 0)
                replayUnitPool[unit.InstanceId] = unit;
        }

        IReadOnlyList<ConstructionManager> activeConstructions = ConstructionManager.AllActive;
        for (int i = 0; i < activeConstructions.Count; i++)
        {
            ConstructionManager construction = activeConstructions[i];
            if (construction == null || construction.gameObject.scene != activeScene)
                continue;
            if (construction.InstanceId > 0)
                replayConstructionPool[construction.InstanceId] = construction;
        }

        UnitManager[] allUnits = Resources.FindObjectsOfTypeAll<UnitManager>();
        for (int i = 0; i < allUnits.Length; i++)
        {
            UnitManager unit = allUnits[i];
            if (unit == null || unit.gameObject.scene != activeScene)
                continue;
            if (unit.InstanceId > 0)
                replayUnitPool[unit.InstanceId] = unit;
        }

        ConstructionManager[] allConstructions = Resources.FindObjectsOfTypeAll<ConstructionManager>();
        for (int i = 0; i < allConstructions.Length; i++)
        {
            ConstructionManager construction = allConstructions[i];
            if (construction == null || construction.gameObject.scene != activeScene)
                continue;
            if (construction.InstanceId > 0)
                replayConstructionPool[construction.InstanceId] = construction;
        }

        replayPoolsInitialized = true;
        replayPoolSceneHandle = activeScene.handle;
    }

    private void RegisterUnitInPool(UnitManager unit)
    {
        if (unit == null || unit.InstanceId <= 0)
            return;
        RemoveStaleUnitPoolMappings(unit, unit.InstanceId);
        replayUnitPool[unit.InstanceId] = unit;
    }

    private void RegisterConstructionInPool(ConstructionManager construction)
    {
        if (construction == null || construction.InstanceId <= 0)
            return;
        RemoveStaleConstructionPoolMappings(construction, construction.InstanceId);
        replayConstructionPool[construction.InstanceId] = construction;
    }

    private void ResetEmbarkStateInPools(Scene activeScene)
    {
        HashSet<UnitManager> processed = new HashSet<UnitManager>();
        foreach (KeyValuePair<int, UnitManager> kv in replayUnitPool)
        {
            UnitManager unit = kv.Value;
            if (unit == null || unit.gameObject.scene != activeScene || !processed.Add(unit))
                continue;

            if (unit.IsEmbarked || unit.EmbarkedTransporter != null)
                unit.SetEmbarked(false);

            IReadOnlyList<UnitTransportSeatRuntime> seats = unit.TransportedUnitSlots;
            if (seats == null || seats.Count <= 0)
                continue;

            for (int i = 0; i < seats.Count; i++)
            {
                UnitTransportSeatRuntime seat = seats[i];
                UnitManager passenger = seat != null ? seat.embarkedUnit : null;
                if (passenger == null)
                    continue;

                if (!passenger.IsEmbarked || passenger.EmbarkedTransporter != unit)
                    unit.RemoveEmbarkedPassenger(passenger);
            }
        }
    }

    private void RemoveStaleUnitPoolMappings(UnitManager unit, int keepInstanceId)
    {
        if (unit == null || replayUnitPool.Count <= 0)
            return;

        List<int> staleKeys = null;
        foreach (KeyValuePair<int, UnitManager> kv in replayUnitPool)
        {
            if (kv.Value != unit || kv.Key == keepInstanceId)
                continue;

            if (staleKeys == null)
                staleKeys = new List<int>();

            staleKeys.Add(kv.Key);
        }

        if (staleKeys == null)
            return;

        for (int i = 0; i < staleKeys.Count; i++)
            replayUnitPool.Remove(staleKeys[i]);
    }

    private void RemoveStaleConstructionPoolMappings(ConstructionManager construction, int keepInstanceId)
    {
        if (construction == null || replayConstructionPool.Count <= 0)
            return;

        List<int> staleKeys = null;
        foreach (KeyValuePair<int, ConstructionManager> kv in replayConstructionPool)
        {
            if (kv.Value != construction || kv.Key == keepInstanceId)
                continue;

            if (staleKeys == null)
                staleKeys = new List<int>();

            staleKeys.Add(kv.Key);
        }

        if (staleKeys == null)
            return;

        for (int i = 0; i < staleKeys.Count; i++)
            replayConstructionPool.Remove(staleKeys[i]);
    }

    private List<UnitManager> CollectSceneUnitsForReplaySnapshot(Scene activeScene)
    {
        List<UnitManager> list = new List<UnitManager>(replayUnitPool.Count + UnitManager.AllActive.Count);
        HashSet<int> seen = new HashSet<int>();

        foreach (KeyValuePair<int, UnitManager> kv in replayUnitPool)
        {
            UnitManager unit = kv.Value;
            if (unit == null || unit.gameObject.scene != activeScene)
                continue;
            list.Add(unit);
            if (kv.Key > 0)
                seen.Add(kv.Key);
        }

        IReadOnlyList<UnitManager> active = UnitManager.AllActive;
        for (int i = 0; i < active.Count; i++)
        {
            UnitManager unit = active[i];
            if (unit == null || unit.gameObject.scene != activeScene)
                continue;
            int id = unit.InstanceId;
            if (id > 0 && seen.Contains(id))
                continue;
            list.Add(unit);
            if (id > 0)
            {
                seen.Add(id);
                replayUnitPool[id] = unit;
            }
        }

        return list;
    }

    private List<ConstructionManager> CollectSceneConstructionsForReplaySnapshot(Scene activeScene)
    {
        List<ConstructionManager> list = new List<ConstructionManager>(replayConstructionPool.Count + ConstructionManager.AllActive.Count);
        HashSet<int> seen = new HashSet<int>();

        foreach (KeyValuePair<int, ConstructionManager> kv in replayConstructionPool)
        {
            ConstructionManager construction = kv.Value;
            if (construction == null || construction.gameObject.scene != activeScene)
                continue;
            list.Add(construction);
            if (kv.Key > 0)
                seen.Add(kv.Key);
        }

        IReadOnlyList<ConstructionManager> active = ConstructionManager.AllActive;
        for (int i = 0; i < active.Count; i++)
        {
            ConstructionManager construction = active[i];
            if (construction == null || construction.gameObject.scene != activeScene)
                continue;
            int id = construction.InstanceId;
            if (id > 0 && seen.Contains(id))
                continue;
            list.Add(construction);
            if (id > 0)
            {
                seen.Add(id);
                replayConstructionPool[id] = construction;
            }
        }

        return list;
    }

    private void DeactivateAllPooledRuntime(Scene activeScene)
    {
        foreach (KeyValuePair<int, UnitManager> kv in replayUnitPool)
        {
            UnitManager unit = kv.Value;
            if (unit == null || unit.gameObject.scene != activeScene)
                continue;
            if (unit.gameObject.activeSelf)
                SetReplayObjectActive(unit.gameObject, false);
        }

        foreach (KeyValuePair<int, ConstructionManager> kv in replayConstructionPool)
        {
            ConstructionManager construction = kv.Value;
            if (construction == null || construction.gameObject.scene != activeScene)
                continue;
            if (construction.gameObject.activeSelf)
                SetReplayObjectActive(construction.gameObject, false);
        }
    }

    private void DestroyReplaySpawnedUnits()
    {
        if (replaySpawnedUnits.Count <= 0)
            return;

        List<UnitManager> tracked = new List<UnitManager>(replaySpawnedUnits);
        for (int i = 0; i < tracked.Count; i++)
        {
            UnitManager unit = tracked[i];
            if (unit == null)
                continue;

            int id = unit.InstanceId;
            if (id > 0 && replayUnitPool.TryGetValue(id, out UnitManager mapped) && mapped == unit)
                replayUnitPool.Remove(id);

            if (Application.isPlaying)
                Destroy(unit.gameObject);
            else
                DestroyImmediate(unit.gameObject);
        }

        replaySpawnedUnits.Clear();
    }

    private void DestroyOrphanReplayUnitClonesInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        UnitManager[] allUnits = Resources.FindObjectsOfTypeAll<UnitManager>();
        for (int i = 0; i < allUnits.Length; i++)
        {
            UnitManager unit = allUnits[i];
            if (unit == null || unit.gameObject == null || unit.gameObject.scene != activeScene)
                continue;

            string objectName = unit.gameObject.name;
            if (!string.Equals(objectName, "unit(Clone)", StringComparison.Ordinal))
                continue;

            if (Application.isPlaying)
                Destroy(unit.gameObject);
            else
                DestroyImmediate(unit.gameObject);
        }
    }
    private void QueueFogRefreshForNextFrame()
    {
        if (restoreFogRefreshRoutine != null)
        {
            StopCoroutine(restoreFogRefreshRoutine);
            restoreFogRefreshRoutine = null;
        }

        restoreFogRefreshRoutine = StartCoroutine(RefreshFogVisualNextFrame());
    }

    private IEnumerator RefreshFogVisualNextFrame()
    {
        yield return null;

        matchController?.RefreshFogOfWarForActiveTeam();
        fogOfWarController?.RebuildSnapshot();
        restoreFogRefreshRoutine = null;
    }

    private void BeginReplayTransitionFeedback(string dialogId, string fallback)
    {
        string text = PanelDialogController.ResolveDialogMessage(dialogId, fallback);
        replayTransitionFeedbackText = text;
        replayTransitionFeedbackStartedAt = Time.realtimeSinceStartup;
        PanelDialogController.TrySetExternalText(text);
        PanelDialogController.TrySetTransientText(text, Mathf.Max(0.5f, replayTransitionMinDisplaySeconds));

        if (replayTransitionFeedbackRoutine != null)
        {
            StopCoroutine(replayTransitionFeedbackRoutine);
            replayTransitionFeedbackRoutine = null;
        }

        replayTransitionFeedbackRoutine = StartCoroutine(FinalizeReplayTransitionFeedback());
    }

    private IEnumerator FinalizeReplayTransitionFeedback()
    {
        yield return null;

        while (true)
        {
            bool busy = restoreFogRefreshRoutine != null || IsReplayStepExecutionBusy(includeCinematicStepRoutine: false);
            float elapsed = Time.realtimeSinceStartup - replayTransitionFeedbackStartedAt;
            bool minDisplayElapsed = elapsed >= Mathf.Max(0.5f, replayTransitionMinDisplaySeconds);
            if (!busy && minDisplayElapsed)
                break;

            if (!string.IsNullOrWhiteSpace(replayTransitionFeedbackText))
                PanelDialogController.TrySetExternalText(replayTransitionFeedbackText);
            yield return null;
        }

        PanelDialogController.ClearExternalText();
        replayTransitionFeedbackText = string.Empty;
        cursorController?.PlayBeepSfx();
        replayTransitionFeedbackRoutine = null;
    }

    private static ReplayTurnRecordSaveData BuildTurnRecordSaveData(ReplayTurnRecord record)
    {
        if (record == null || record.StartSnapshot == null)
            return null;

        ReplayTurnRecordSaveData saveData = new ReplayTurnRecordSaveData
        {
            turnNumber = record.TurnNumber,
            actingTeamId = (int)record.ActingTeam,
            startSnapshot = record.StartSnapshot
        };

        if (record.Steps != null)
        {
            for (int i = 0; i < record.Steps.Count; i++)
            {
                ReplayStepSaveData step = BuildStepSaveData(record.Steps[i]);
                if (step != null)
                    saveData.steps.Add(step);
            }
        }

        return saveData;
    }

    private static ReplayTurnRecord BuildTurnRecordFromSaveData(ReplayTurnRecordSaveData saveData)
    {
        if (saveData == null || saveData.startSnapshot == null)
            return null;

        ReplayTurnRecord record = new ReplayTurnRecord
        {
            TurnNumber = saveData.turnNumber,
            ActingTeam = (TeamId)saveData.actingTeamId,
            StartSnapshot = saveData.startSnapshot,
            Steps = new List<ReplayStep>()
        };

        if (saveData.steps != null)
        {
            for (int i = 0; i < saveData.steps.Count; i++)
            {
                ReplayStep step = BuildStepFromSaveData(saveData.steps[i]);
                if (step != null)
                    record.Steps.Add(step);
            }
        }

        return record;
    }

    private static ReplayStepSaveData BuildStepSaveData(ReplayStep step)
    {
        if (step == null)
            return null;

        ReplayStepSaveData saveData = new ReplayStepSaveData
        {
            stepIndex = step.StepIndex,
            stepType = (int)step.StepType,
            debugLabel = step.DebugLabel
        };

        if (step.Command != null)
            saveData.commandJson = JsonUtility.ToJson(step.Command);

        return saveData;
    }

    private static ReplayStep BuildStepFromSaveData(ReplayStepSaveData saveData)
    {
        if (saveData == null)
            return null;

        ReplayStepType stepType = (ReplayStepType)saveData.stepType;
        IReplayCommand command = BuildCommandFromSaveData(stepType, saveData.commandJson);
        if (command == null)
            return null;

        return new ReplayStep
        {
            StepIndex = saveData.stepIndex,
            StepType = stepType,
            Command = command,
            DebugLabel = string.IsNullOrWhiteSpace(saveData.debugLabel) ? command.DebugLabel : saveData.debugLabel
        };
    }

    private static IReplayCommand BuildCommandFromSaveData(ReplayStepType stepType, string commandJson)
    {
        if (string.IsNullOrWhiteSpace(commandJson))
            return null;

        switch (stepType)
        {
            case ReplayStepType.MoveUnit:
                return JsonUtility.FromJson<MoveUnitReplayCommand>(commandJson);
            case ReplayStepType.Attack:
                return JsonUtility.FromJson<AttackReplayCommand>(commandJson);
            case ReplayStepType.BuyUnit:
                return JsonUtility.FromJson<BuyUnitReplayCommand>(commandJson);
            case ReplayStepType.Capture:
                return JsonUtility.FromJson<CaptureReplayCommand>(commandJson);
            case ReplayStepType.Embark:
                return JsonUtility.FromJson<EmbarkReplayCommand>(commandJson);
            case ReplayStepType.Disembark:
                return JsonUtility.FromJson<DisembarkReplayCommand>(commandJson);
            case ReplayStepType.Merge:
                return JsonUtility.FromJson<MergeReplayCommand>(commandJson);
            case ReplayStepType.Supply:
                return JsonUtility.FromJson<SupplyReplayCommand>(commandJson);
            default:
                return null;
        }
    }

    private UnitData ResolveUnitById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || unitSpawner == null)
            return null;

        return unitSpawner.TryGetUnitData(id, out UnitData unit) ? unit : null;
    }

    private ConstructionSiteRuntime BuildSiteRuntimeFromSaveData(ConstructionSiteRuntimeSaveData saved)
    {
        return SaveDataMapper.BuildConstructionSiteRuntimeFromSaveData(
            saved,
            ResolveUnitById,
            ResolveServiceById,
            ResolveSupplyById);
    }

    private ServiceData ResolveServiceById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (cachedServicesById.TryGetValue(id, out ServiceData cached) && cached != null)
            return cached;

        ServiceData[] loaded = Resources.FindObjectsOfTypeAll<ServiceData>();
        for (int i = 0; i < loaded.Length; i++)
        {
            ServiceData service = loaded[i];
            if (service == null || string.IsNullOrWhiteSpace(service.id))
                continue;
            if (!cachedServicesById.ContainsKey(service.id))
                cachedServicesById[service.id] = service;
        }

        cachedServicesById.TryGetValue(id, out ServiceData resolved);
        return resolved;
    }

    private SupplyData ResolveSupplyById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (cachedSuppliesById.TryGetValue(id, out SupplyData cached) && cached != null)
            return cached;

        SupplyData[] loaded = Resources.FindObjectsOfTypeAll<SupplyData>();
        for (int i = 0; i < loaded.Length; i++)
        {
            SupplyData supply = loaded[i];
            if (supply == null || string.IsNullOrWhiteSpace(supply.id))
                continue;
            if (!cachedSuppliesById.ContainsKey(supply.id))
                cachedSuppliesById[supply.id] = supply;
        }

        cachedSuppliesById.TryGetValue(id, out SupplyData resolved);
        return resolved;
    }
}









































