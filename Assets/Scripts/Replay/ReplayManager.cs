using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private ReplayTurnRecord currentRecord;
    [SerializeField] private List<ReplayTurnRecord> matchHistory = new List<ReplayTurnRecord>();
    [SerializeField] private int selectedTurnIndex = -1;
    [SerializeField] private TeamId observerTeam = TeamId.Neutral;
    [SerializeField] private ReplayVisionMode visionMode = ReplayVisionMode.Omniscient;
    [SerializeField] private int currentStepIndex;
    [Header("Playback")]
    [SerializeField] private bool isPlaying;
    [SerializeField] private bool animateCombatOnReplay = true;
    [SerializeField] private bool cinematicModeEnabled = true;
    [SerializeField] private float autoPlayStepInterval = 0.5f;
    [SerializeField] private float autoPlayTimer;
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
        if (autoPlayTimer < Mathf.Max(0.01f, autoPlayStepInterval))
            return;

        autoPlayTimer = 0f;
        if (!StepForward())
            isPlaying = false;
    }
    private bool IsReplayStepExecutionBusy(bool includeCinematicStepRoutine = true)
    {
        TryAutoAssignReferences();

        if (includeCinematicStepRoutine && attackStepExecutionRoutine != null)
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
        if (!isRecording || command == null || currentRecord == null)
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

        int stepCount = currentRecord.Steps != null ? currentRecord.Steps.Count : 0;
        currentStepIndex = stepCount - 1;
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
            visionMode = (int)visionMode
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

        if (data == null)
            return;

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

    public bool StepForward()
    {
        if (!isReplaying || currentRecord == null || currentRecord.Steps == null)
            return false;
        if (IsReplayStepExecutionBusy())
            return false;

        int nextIndex = currentStepIndex + 1;
        if (!ExecuteStepAtIndex(nextIndex, allowCinematic: true, out bool startedAsyncExecution))
            return false;

        if (!startedAsyncExecution)
        {
            ApplyReplayVision();
            if (currentRecord.Steps == null || currentStepIndex >= currentRecord.Steps.Count - 1)
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

        // Historico (turnos antigos): fallback deterministico por reexecucao.
        ReplayLogWarning($"[Replay][StepBackward] using fallback | targetStep={targetIndex} | cachedSnapshots={stepSnapshots.Count}");
        RestoreSnapshot(currentRecord.StartSnapshot);
        currentStepIndex = -1;
        for (int i = 0; i <= targetIndex; i++)
        {
            if (!ExecuteStepAtIndex(i, allowCinematic: false, out _))
                break;
        }

        ApplyReplayVision();
        autoPlayTimer = 0f;
        return true;
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
        if (currentRecord == null || currentRecord.Steps == null || currentRecord.Steps.Count <= 0)
            return;
        if (currentStepIndex >= currentRecord.Steps.Count - 1)
            return;

        isPlaying = true;
        autoPlayTimer = 0f;
    }

    private bool ExecuteStepAtIndex(int index, bool allowCinematic, out bool startedAsyncExecution)
    {
        startedAsyncExecution = false;

        if (currentRecord == null || currentRecord.Steps == null)
            return false;
        if (index < 0 || index >= currentRecord.Steps.Count)
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
            if (currentRecord == null || currentRecord.Steps == null || currentStepIndex >= currentRecord.Steps.Count - 1)
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
                cursorController.SetCell(cursorHex, playMoveSfx: false, adjustCamera: false);
                cursorController.AdjustCameraToCursor();
            }

            if (cinematicEvent.Action == CinematicAction.Confirm)
                turnStateManager?.HandleConfirm();
            else if (cinematicEvent.Action == CinematicAction.AimAction)
                turnStateManager?.HandleAimActionRequested();

            while (IsReplayStepExecutionBusy(includeCinematicStepRoutine: false))
                yield return null;

            float delay = Mathf.Max(0f, cinematicEvent.DelayAfter);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }
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
                manager.gameObject.SetActive(isActive);
        }

        for (int i = 0; i < restoredUnits.Count; i++)
        {
            (UnitManager manager, UnitSaveData saved) = restoredUnits[i];
            if (manager != null && saved != null)
                manager.gameObject.SetActive(saved.isActiveInHierarchy);
        }

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
        delayedBeginTurnRecordingRoutine = null;
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

        Scene activeScene = SceneManager.GetActiveScene();
        EnsureReplayPoolsInitialized();
        List<UnitManager> units = CollectSceneUnitsForReplaySnapshot(activeScene);
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.gameObject.scene != activeScene)
                continue;

            UnitSaveData item = SaveDataMapper.BuildUnitSaveData(unit);
            if (item != null)
                snapshot.Units.Add(item);
        }

        List<ConstructionManager> constructions = CollectSceneConstructionsForReplaySnapshot(activeScene);
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || construction.gameObject.scene != activeScene)
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
                unit.gameObject.SetActive(false);
        }

        IReadOnlyList<ConstructionManager> activeConstructions = ConstructionManager.AllActive;
        for (int i = 0; i < activeConstructions.Count; i++)
        {
            ConstructionManager construction = activeConstructions[i];
            if (construction == null || construction.gameObject.scene != activeScene)
                continue;

            RegisterConstructionInPool(construction);
            if (construction.gameObject.activeSelf)
                construction.gameObject.SetActive(false);
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
        replayUnitPool[unit.InstanceId] = unit;
    }

    private void RegisterConstructionInPool(ConstructionManager construction)
    {
        if (construction == null || construction.InstanceId <= 0)
            return;
        replayConstructionPool[construction.InstanceId] = construction;
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
                unit.gameObject.SetActive(false);
        }

        foreach (KeyValuePair<int, ConstructionManager> kv in replayConstructionPool)
        {
            ConstructionManager construction = kv.Value;
            if (construction == null || construction.gameObject.scene != activeScene)
                continue;
            if (construction.gameObject.activeSelf)
                construction.gameObject.SetActive(false);
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
                unit.gameObject.SetActive(false);

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











































































