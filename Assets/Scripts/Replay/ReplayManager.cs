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
    [SerializeField, Range(0f, 2f)] private float replayConfirmVisualDelay = 0.25f;
    [SerializeField] private bool animateCombatOnReplay = true;
    [SerializeField] private bool cinematicModeEnabled = true;
    [SerializeField, HideInInspector] private float autoPlayTimer;
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

    private readonly Dictionary<string, ServiceData> cachedServicesById = new Dictionary<string, ServiceData>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SupplyData> cachedSuppliesById = new Dictionary<string, SupplyData>(StringComparer.OrdinalIgnoreCase);

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
    public ActionStack ActionStack => actionStack;
    public PlayerAction CurrentBuffer => currentBuffer;
    public int CurrentReplayBatchCount => ResolveCurrentReplayBatchCount();

    private void ReplayLog(string message)
    {
        if (enableReplayRuntimeLogs)
            Debug.Log(message);
    }

    private void ReplayLogWarning(string message)
    {
        if (enableReplayRuntimeWarnings)
            Debug.LogWarning(message);
    }

    private void Awake()
    {
        TryAutoAssignReferences();
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
    }

    private IEnumerator ExecuteActionStepFromStack(int index, PlayerAction action, TurnStartSnapshot preActionSnapshot, TurnStartSnapshot postActionSnapshot)
    {
        if (preActionSnapshot != null)
            RestoreSnapshot(preActionSnapshot);

        yield return TryMoveReplayCursorToActionStart(action, preActionSnapshot);

        bool canEmulateAction = action != null && CanReplayActionAsLiveInputs(action.ActionType);
        if (canEmulateAction)
            yield return ExecuteRecordedActionBatch(action, preActionSnapshot);
        else if (postActionSnapshot != null)
            RestoreSnapshot(postActionSnapshot);

        currentStepIndex = index;
        ApplyReplayVision();
        if (currentStepIndex >= ResolveCurrentReplayBatchCount() - 1)
            isPlaying = false;

        actionStepExecutionRoutine = null;
    }

    private IEnumerator TryMoveReplayCursorToActionStart(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        if (cursorController == null)
            yield break;

        if (!TryResolveActionPreExecutionCursorCell(action, preActionSnapshot, out Vector3Int targetCursorCell))
            yield break;

        ReplayLog($"[Replay][CursorTravel] phase=pre-batch current={FormatReplayCell(NormalizeCell(cursorController.CurrentCell))} target={FormatReplayCell(NormalizeCell(targetCursorCell))}");
        yield return MoveCursorToCellWithTravel(targetCursorCell);
    }

    private IEnumerator MoveCursorToCellWithTravel(Vector3Int targetCell)
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

        List<Vector3Int> travelPath = BuildReplayCursorTravelPath(fromCell, toCell);
        if (travelPath == null || travelPath.Count <= 0)
            travelPath = new List<Vector3Int> { toCell };

        ReplayLog($"[Replay][CursorTravel] from={FormatReplayCell(fromCell)} to={FormatReplayCell(toCell)} pathSteps={travelPath.Count}");

        for (int i = 0; i < travelPath.Count; i++)
        {
            Vector3Int stepCell = NormalizeCell(travelPath[i]);
            ReplayLog($"[Replay][CursorTravel] step {i + 1}/{travelPath.Count} -> {FormatReplayCell(stepCell)}");
            cursorController.SetCell(stepCell, playMoveSfx: animateCursorTravelBetweenActions, adjustCamera: false);
            cursorController.TryAdjustCameraToCursor();

            if (animateCursorTravelBetweenActions && cursorTravelStepDelay > 0f)
                yield return new WaitForSecondsRealtime(cursorTravelStepDelay);
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
                return true;
            default:
                return false;
        }
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
            default:
                // Shopping and unknown actions are finalized by restoring post-action snapshot.
                break;
        }

        yield return WaitForReplaySystemsIdle();
    }

    private IEnumerator ExecuteRecordedUnitActionBatch(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        if (cursorController == null)
            yield break;

        bool hasOriginCell = TryResolveRecordedOriginCell(action, preActionSnapshot, out Vector3Int originCell);
        if (hasOriginCell)
        {
            originCell = NormalizeCell(originCell);
            ReplayLog($"[Replay][CursorTravel] phase=unit-batch-origin current={FormatReplayCell(NormalizeCell(cursorController.CurrentCell))} origin={FormatReplayCell(originCell)}");
            yield return MoveCursorToCellWithTravel(originCell);

            ExecuteReplayConfirmInput();
            yield return WaitForReplaySystemsIdle();
        }

        bool hasDestinationCell = TryResolveRecordedDestinationCell(action, hasOriginCell, originCell, out Vector3Int destinationCell);
        if (hasDestinationCell)
        {
            destinationCell = NormalizeCell(destinationCell);
            ReplayLog($"[Replay][CursorTravel] phase=unit-batch-destination current={FormatReplayCell(NormalizeCell(cursorController.CurrentCell))} destination={FormatReplayCell(destinationCell)}");
            if (!hasOriginCell || destinationCell != originCell)
                yield return MoveCursorToCellWithTravel(destinationCell);

            ExecuteReplayConfirmInput();
            yield return WaitForReplaySystemsIdle();
        }

        if (action.SensorAction == SensorActionType.None)
            yield break;

        if (!turnStateManager.HandleAutomatedSensorActionRequested(action.SensorAction))
            yield break;

        yield return WaitForReplaySystemsIdle();

        if (TryResolveRecordedTargetCell(action, out Vector3Int targetCell))
        {
            targetCell = NormalizeCell(targetCell);
            if (NormalizeCell(cursorController.CurrentCell) != targetCell)
                yield return MoveCursorToCellWithTravel(targetCell);
        }

        ExecuteReplayConfirmInput();
        yield return WaitForReplaySystemsIdle();
    }

    private IEnumerator ExecuteRecordedCommandServiceBatch(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        if (cursorController != null && TryResolveRecordedCursorCell(action, preActionSnapshot, out Vector3Int cursorCell))
        {
            yield return MoveCursorToCellWithTravel(NormalizeCell(cursorCell));
        }

        if (turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.CommandService))
        {
            yield return WaitForReplaySystemsIdle();
            ExecuteReplayConfirmInput();
            yield return WaitForReplaySystemsIdle();
        }
    }

    private IEnumerator ExecuteRecordedRemoveUnitBatch(PlayerAction action, TurnStartSnapshot preActionSnapshot)
    {
        if (cursorController != null)
        {
            if (TryResolveRecordedTargetCell(action, out Vector3Int targetCell))
                yield return MoveCursorToCellWithTravel(NormalizeCell(targetCell));
            else if (TryResolveRecordedCursorCell(action, preActionSnapshot, out Vector3Int cursorCell))
                yield return MoveCursorToCellWithTravel(NormalizeCell(cursorCell));
        }

        if (turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.RemoveUnit))
        {
            yield return WaitForReplaySystemsIdle();
            ExecuteReplayConfirmInput();
            yield return WaitForReplaySystemsIdle();
        }
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

    private IEnumerator WaitForReplaySystemsIdle()
    {
        while (IsReplayStepExecutionBusy(includeCinematicStepRoutine: false))
            yield return null;

        if (replayConfirmVisualDelay > 0f)
            yield return new WaitForSecondsRealtime(replayConfirmVisualDelay);
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!isReplaying)
            return;

        if (!isPlaying)
            return;

        if (IsReplayStepExecutionBusy())
            return;

        autoPlayTimer += Mathf.Max(0f, Time.unscaledDeltaTime);
        if (autoPlayTimer < Mathf.Max(0.01f, timeBetweenBatches))
            return;

        autoPlayTimer = 0f;
        if (!StepForward())
            isPlaying = false;
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

        // Replay do turno ativo: entra no estado atual sem reset para o step 0.
        isReplaying = true;
        isRecording = false;
        isPlaying = false;
        autoPlayTimer = 0f;
        EnsureReplayPoolsInitialized();
        RebuildStepSnapshotsForCurrentRecordFromActionSnapshots();

        int batchCount = ResolveCurrentReplayBatchCount();
        currentStepIndex = batchCount - 1;
        cursorController?.PlayBeepSfx();
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

        currentRecord = selected;
        stepSnapshots.Clear();
        RebuildStepSnapshotsForCurrentRecordFromActionSnapshots();
        selectedTurnIndex = turnIndex;
        visionMode = replayVisionMode;
        observerTeam = replayObserverTeam;
        isReplaying = true;
        isRecording = false;
        isPlaying = false;
        autoPlayTimer = 0f;
        currentStepIndex = -1;
        EnsureReplayPoolsInitialized();
        RestoreSnapshot(currentRecord.StartSnapshot);
        cursorController?.PlayBeepSfx();
        ApplyReplayVision();
    }

    public void StopReplay()
    {
        bool wasReplaying = isReplaying;
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
        autoPlayTimer = 0f;
        currentStepIndex = -1;
        if (!shouldPreserveStepSnapshots)
            stepSnapshots.Clear();
        DestroyReplaySpawnedUnits();
        cursorController?.PlayBeepSfx();

        if (wasReplaying && preReplayLiveSnapshot != null)
            RestoreSnapshot(preReplayLiveSnapshot);

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
        autoPlayTimer = 0f;
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
        isReplaying = true;
        isRecording = false;
        isPlaying = false;
        autoPlayTimer = 0f;
        EnsureReplayPoolsInitialized();
        RebuildStepSnapshotsForCurrentRecordFromActionSnapshots();
        currentStepIndex = -1;
        RestoreSnapshot(currentRecord.StartSnapshot);
        cursorController?.PlayBeepSfx();
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

        while (IsReplayStepExecutionBusy())
            yield return null;

        if (!ExecuteStepAtIndex(actionIndex, allowCinematic: true, out bool startedAsync))
            yield break;

        if (startedAsync)
        {
            while (IsReplayStepExecutionBusy())
                yield return null;
        }
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

    public void UpdateCurrentBufferMovement(Vector3Int moveFrom, Vector3Int moveTo, UnitLayerMode layerBefore, UnitLayerMode layerAfter)
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
        currentBuffer.ActionType = PlayerActionType.UnitAction;
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
        currentBuffer = new PlayerAction();
    }

    public void DiscardCurrentBuffer(string reason)
    {
        currentBuffer = new PlayerAction();
    }

    public bool StepForward()
    {
        if (!isReplaying || currentRecord == null)
            return false;
        if (IsReplayStepExecutionBusy())
            return false;

        int nextIndex = currentStepIndex + 1;
        if (!ExecuteStepAtIndex(nextIndex, allowCinematic: true, out bool startedAsyncExecution))
            return false;

        if (!startedAsyncExecution)
        {
            ApplyReplayVision();
            if (currentStepIndex >= ResolveCurrentReplayBatchCount() - 1)
                isPlaying = false;
        }

        return true;
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

        if (targetIndex < 0)
        {
            ReplayLog($"[Replay][StepBackward] using snapshot=start | currentStep={currentStepIndex} -> targetStep=-1");
            RestoreSnapshot(currentRecord.StartSnapshot);
            currentStepIndex = -1;
            ApplyReplayVision();
            autoPlayTimer = 0f;
            return true;
        }

        if (stepSnapshots.TryGetValue(targetIndex, out TurnStartSnapshot stepSnapshot) && stepSnapshot != null)
        {
            ReplayLog($"[Replay][StepBackward] using snapshot=step | targetStep={targetIndex} | cachedSnapshots={stepSnapshots.Count}");
            RestoreSnapshot(stepSnapshot);
            currentStepIndex = targetIndex;
            ApplyReplayVision();
            autoPlayTimer = 0f;
            return true;
        }

        TurnStartSnapshot actionSnapshot = TryResolveSnapshotForCurrentRecordActionIndex(targetIndex, cacheWhenFound: true);
        if (actionSnapshot != null)
        {
            ReplayLog($"[Replay][StepBackward] using snapshot=action | targetStep={targetIndex}");
            RestoreSnapshot(actionSnapshot);
            currentStepIndex = targetIndex;
            ApplyReplayVision();
            autoPlayTimer = 0f;
            return true;
        }

        ReplayLogWarning($"[Replay][StepBackward] snapshot ausente para targetStep={targetIndex} | cachedSnapshots={stepSnapshots.Count}");
        return false;
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
        autoPlayTimer = 0f;
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
        autoPlayTimer = 0f;
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
            && cinematicModeEnabled
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
            context.AnimateCombatPresentation = false;
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

            if (animateCursorTravelBetweenActions && cursorTravelStepDelay > 0f)
                yield return new WaitForSecondsRealtime(cursorTravelStepDelay);


            bool isLastEvent = i == track.Events.Count - 1;
            bool skipTrailingConfirm = isLastEvent && cinematicEvent.Action == CinematicAction.Confirm;

            if (!skipTrailingConfirm && cinematicEvent.Action == CinematicAction.Confirm)
                ExecuteReplayConfirmInput();
            else if (!skipTrailingConfirm && cinematicEvent.Action == CinematicAction.AimAction)
                turnStateManager?.HandleAimActionRequested();

            while (IsReplayStepExecutionBusy(includeCinematicStepRoutine: false))
                yield return null;

            float delay = Mathf.Max(0f, cinematicEvent.DelayAfter);
            if (!skipTrailingConfirm && (cinematicEvent.Action == CinematicAction.Confirm || cinematicEvent.Action == CinematicAction.AimAction))
                delay = Mathf.Max(delay, replayConfirmVisualDelay);

            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }
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

        unitSpawner.EnsureNextIdAbove(maxUnitId);
        constructionSpawner.EnsureNextIdAbove(maxConstructionId);

        MatchStateSaveData matchState = snapshot.MatchState ?? new MatchStateSaveData();
        SaveDataMapper.ApplyMatchStateSaveData(matchController, matchState);
        matchController.SetEconomyEnabled(matchState.economyEnabled);
        matchController.SetCurrentTurn(Mathf.Max(0, matchState.currentTurn));
        matchController.SetActiveTeamId(matchState.activeTeamId);
        SaveDataMapper.ApplyMatchStateSaveData(matchController, matchState);

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
            AnimateCombatPresentation = animateCombatOnReplay,
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
    }
    private void ClearCurrentRuntime()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        EnsureReplayPoolsInitialized();

        DeactivateAllPooledRuntime(activeScene);

        IReadOnlyList<UnitManager> activeUnits = UnitManager.AllActive;
        for (int i = 0; i < activeUnits.Count; i++)
        {
            UnitManager unit = activeUnits[i];
            if (unit == null || unit.gameObject.scene != activeScene)
                continue;

            RegisterUnitInPool(unit);
            if (unit.gameObject.activeSelf)
                SetReplayObjectActive(unit.gameObject, false);
        }

        IReadOnlyList<ConstructionManager> activeConstructions = ConstructionManager.AllActive;
        for (int i = 0; i < activeConstructions.Count; i++)
        {
            ConstructionManager construction = activeConstructions[i];
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

            if (unit.gameObject.activeSelf)
                SetReplayObjectActive(unit.gameObject, false);

            int id = unit.InstanceId;
            if (id > 0 && replayUnitPool.TryGetValue(id, out UnitManager mapped) && mapped == unit)
                replayUnitPool.Remove(id);
        }

        replaySpawnedUnits.Clear();
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
