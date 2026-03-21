using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ReplayPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ReplayManager replayManager;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Texts")]
    [SerializeField] private TMP_Text textReplay;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text observerText;
    [SerializeField] private TMP_Text visionModeText;
    [SerializeField] private TMP_Text stepText;
    [SerializeField] private TMP_Text startConfigText;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button stepBackButton;
    [SerializeField] private Button stepForwardButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Toggle fastReplayModeToggle;

    [Header("Input")]
    [SerializeField] private KeyCode togglePanelKey = KeyCode.F9;

    [SerializeField] private bool isOpen;
    [SerializeField] private bool replaySessionArmed;

    [Header("Start Selection")]
    [SerializeField] private ReplayStartMode replayStartMode = ReplayStartMode.FromCurrentTop;
    [SerializeField] [Range(-1, 3)] private int specificTeam = -1;
    [SerializeField] [Min(0)] private int specificTurn = 0;

    [Header("View Selection")]
    [SerializeField] [Range(-1, 3)] private int viewUnderSpecificTeam = -1;

    private void OnValidate()
    {
        specificTeam = Mathf.Clamp(specificTeam, -1, 3);
        viewUnderSpecificTeam = Mathf.Clamp(viewUnderSpecificTeam, -1, 3);
        specificTurn = Mathf.Max(0, specificTurn);
    }

    private void Awake()
    {
        if (replayManager == null)
            replayManager = FindAnyObjectByType<ReplayManager>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        TryAutoAssignUiBindings();
        BindButtonCallbacks();
        SetPanelOpen(false);
    }

    private void OnDestroy()
    {
        UnbindButtonCallbacks();
    }

    private void OnDisable()
    {
        TryAutoPauseReplayOnPanelClose(showDialog: false);
    }

    private void Update()
    {
        if (WasKeyPressedThisFrame(togglePanelKey))
            TogglePanel();

        if (isOpen)
            RefreshLabels();
    }

    private static bool WasKeyPressedThisFrame(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return false;

        switch (key)
        {
            case KeyCode.F9:
                return Keyboard.current.f9Key.wasPressedThisFrame;
            case KeyCode.RightBracket:
                return Keyboard.current.rightBracketKey.wasPressedThisFrame;
            case KeyCode.LeftBracket:
                return Keyboard.current.leftBracketKey.wasPressedThisFrame;
            case KeyCode.Backslash:
                return Keyboard.current.backslashKey.wasPressedThisFrame;
        }

        return false;
#else
        return Input.GetKeyDown(key);
#endif
    }

    public void TogglePanel()
    {
        SetPanelOpen(!isOpen);
    }

    public void SetPanelOpen(bool open)
    {
        bool wasOpen = isOpen;
        isOpen = open;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = open ? 1f : 0f;
            panelCanvasGroup.interactable = open;
            panelCanvasGroup.blocksRaycasts = open;
        }

        if (open)
            ApplyReplayViewSelection();
        else if (wasOpen)
            TryAutoPauseReplayOnPanelClose(showDialog: true);

        RefreshLabels();
    }

    public void CycleObserverTeamPlaceholder()
    {
        CycleViewUnderSpecificTeam();
    }
    public void CycleReplayStartMode()
    {
        int next = (int)replayStartMode + 1;
        if (next > (int)ReplayStartMode.FromSpecificTurnTeam)
            next = (int)ReplayStartMode.FromBeginning;
        replayStartMode = (ReplayStartMode)next;
        RefreshLabels();
    }

    public void CycleSpecificTeam()
    {
        specificTeam++;
        if (specificTeam > 3)
            specificTeam = -1;
        RefreshLabels();
    }

    public void IncreaseSpecificTurn()
    {
        specificTurn = Mathf.Max(0, specificTurn + 1);
        RefreshLabels();
    }

    public void DecreaseSpecificTurn()
    {
        specificTurn = Mathf.Max(0, specificTurn - 1);
        RefreshLabels();
    }

    public void SetReplaySpecificSelection(int turn, int team)
    {
        specificTurn = Mathf.Max(0, turn);
        specificTeam = Mathf.Clamp(team, -1, 3);
        RefreshLabels();
    }

    public void CycleViewUnderSpecificTeam()
    {
        viewUnderSpecificTeam++;
        if (viewUnderSpecificTeam > 3)
            viewUnderSpecificTeam = -1;

        ApplyReplayViewSelection();
        RefreshLabels();
    }

    public void SetReplayViewUnderSpecificTeam(int team)
    {
        viewUnderSpecificTeam = Mathf.Clamp(team, -1, 3);
        ApplyReplayViewSelection();
        RefreshLabels();
    }

    private void ApplyReplayViewSelection()
    {
        if (replayManager == null)
            return;

        if (viewUnderSpecificTeam < 0)
        {
            replayManager.SetReplayVision(ReplayVisionMode.Omniscient, TeamId.Neutral);
            return;
        }

        TeamId team = (TeamId)Mathf.Clamp(viewUnderSpecificTeam, 0, 3);
        replayManager.SetReplayVision(ReplayVisionMode.TeamFiltered, team);
    }

    private void TryAutoAssignUiBindings()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (textReplay == null)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                    continue;

                string name = text.name != null ? text.name.ToLowerInvariant() : string.Empty;
                if (name.Contains("text_replay") || name == "replay" || name.Contains("replay_status"))
                {
                    textReplay = text;
                    break;
                }
            }
        }

        if (startConfigText == null)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || ReferenceEquals(text, textReplay))
                    continue;

                string name = text.name != null ? text.name.ToLowerInvariant() : string.Empty;
                if (name.Contains("start_mode") || name.Contains("replay_start") || name.Contains("start_config"))
                {
                    startConfigText = text;
                    break;
                }
            }
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            string name = button.name != null ? button.name.ToLowerInvariant() : string.Empty;
            if (startButton == null && name.Contains("start"))
                startButton = button;
            else if (stopButton == null && name.Contains("stop"))
                stopButton = button;
            else if (pauseButton == null && name.Contains("pause"))
                pauseButton = button;
            else if (playButton == null && name.Contains("play"))
                playButton = button;
            else if (stepBackButton == null && (name.Contains("back") || name.Contains("prev")))
                stepBackButton = button;
            else if (stepForwardButton == null && (name.Contains("fwd") || name.Contains("forward") || name.Contains("next")))
                stepForwardButton = button;
        }

        if (fastReplayModeToggle == null)
        {
            Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                Toggle toggle = toggles[i];
                if (toggle == null)
                    continue;

                string name = toggle.name != null ? toggle.name.ToLowerInvariant() : string.Empty;
                if (name.Contains("fast") && name.Contains("replay"))
                {
                    fastReplayModeToggle = toggle;
                    break;
                }
            }
        }
    }

    private void BindButtonCallbacks()
    {
        startButton?.onClick.AddListener(OnStartClicked);
        stepBackButton?.onClick.AddListener(OnBackClicked);
        playButton?.onClick.AddListener(OnPlayClicked);
        pauseButton?.onClick.AddListener(OnPauseClicked);
        stepForwardButton?.onClick.AddListener(OnForwardClicked);
        stopButton?.onClick.AddListener(OnStopClicked);
        fastReplayModeToggle?.onValueChanged.AddListener(OnFastReplayModeToggleChanged);
    }

    private void UnbindButtonCallbacks()
    {
        startButton?.onClick.RemoveListener(OnStartClicked);
        stepBackButton?.onClick.RemoveListener(OnBackClicked);
        playButton?.onClick.RemoveListener(OnPlayClicked);
        pauseButton?.onClick.RemoveListener(OnPauseClicked);
        stepForwardButton?.onClick.RemoveListener(OnForwardClicked);
        stopButton?.onClick.RemoveListener(OnStopClicked);
        fastReplayModeToggle?.onValueChanged.RemoveListener(OnFastReplayModeToggleChanged);
    }

    private void OnStartClicked()
    {
        if (replayManager == null)
            return;

        bool started = false;
        ApplyReplayViewSelection();
        TeamId specificTeamId = (TeamId)Mathf.Clamp(specificTeam, (int)TeamId.Neutral, (int)TeamId.Yellow);

        switch (replayStartMode)
        {
            case ReplayStartMode.FromBeginning:
                started = replayManager.StartReplayFromCurrentRecordBeginning(replayManager.VisionMode, replayManager.ObserverTeam)
                          || replayManager.StartReplayFromBeginning(replayManager.VisionMode, replayManager.ObserverTeam);
                break;
            case ReplayStartMode.FromSpecificTurnTeam:
                if (specificTeamId == TeamId.Neutral)
                    started = replayManager.StartReplayFromTurn(specificTurn, replayManager.VisionMode, replayManager.ObserverTeam);
                else
                    started = replayManager.StartReplayFromTurnAndTeam(specificTurn, specificTeamId, replayManager.VisionMode, replayManager.ObserverTeam);
                break;
            case ReplayStartMode.FromCurrentTop:
            default:
                started = replayManager.StartReplayFromLatestSnapshot(replayManager.VisionMode, replayManager.ObserverTeam);
                break;
        }

        if (!started)
        {
            replaySessionArmed = false;
            RefreshLabels();
            return;
        }

        replaySessionArmed = replayManager.IsReplaying;
        replayManager.PausePlayback();
        RefreshLabels();
    }

    private void OnBackClicked()
    {
        if (replayManager == null || !replayManager.IsReplaying || !replaySessionArmed)
            return;

        replayManager.PausePlayback();
        replayManager.StepBackward();
        RefreshLabels();
    }

    private void OnPlayClicked()
    {
        if (replayManager == null || !replayManager.IsReplaying || !replaySessionArmed)
            return;

        replayManager.ResumePlayback();
        if (replayManager.IsPlaying)
            ShowReplayDialog("dialog.replay.autoplay_on", "replay auto play ligado");
        RefreshLabels();
    }

    private void OnPauseClicked()
    {
        if (replayManager == null)
            return;

        bool wasPlaying = replayManager.IsReplaying && replayManager.IsPlaying;
        replayManager.PausePlayback();
        if (wasPlaying)
            ShowReplayDialog("dialog.replay.autoplay_off", "replay auto play desligado");
        RefreshLabels();
    }

    private void OnForwardClicked()
    {
        if (replayManager == null || !replayManager.IsReplaying || !replaySessionArmed)
            return;

        replayManager.PausePlayback();
        replayManager.StepForward();
        RefreshLabels();
    }

    private void OnStopClicked()
    {
        if (replayManager == null)
            return;

        replayManager.StopReplay();
        replaySessionArmed = false;
        RefreshLabels();
    }

    private void OnFastReplayModeToggleChanged(bool enabled)
    {
        if (replayManager == null)
            return;

        replayManager.SetFastReplayMode(enabled);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (replayManager == null)
        {
            if (fastReplayModeToggle != null)
            {
                fastReplayModeToggle.SetIsOnWithoutNotify(false);
                fastReplayModeToggle.interactable = false;
            }

            SetText(textReplay, "Replay");
            SetText(turnText, "Turno: -");
            SetText(observerText, "Observador: -");
            SetText(visionModeText, "Visao: -");
            SetText(stepText, "Step: -");
            SetText(startConfigText, BuildReplayStartConfigLabel());
            ApplyButtonsState(false, false, false, false, false, false, false, false);
            return;
        }

        ReplayTurnRecord record = replayManager.CurrentRecord;
        int totalBatches = replayManager.CurrentReplayBatchCount;
        int totalSnapshots = Mathf.Max(1, totalBatches + 1);
        int currentSnapshotIndex = replayManager.IsReplaying
            ? Mathf.Clamp(replayManager.CurrentStepIndex + 2, 1, totalSnapshots)
            : totalSnapshots;
        int turnLabel = record != null ? record.TurnNumber : -1;

        string replayStateLabel;
        if (!replayManager.IsReplaying)
        {
            replaySessionArmed = false;
            replayStateLabel = "Replay";
        }
        else if (replayManager.IsPlaying)
            replayStateLabel = "Replay Ativado";
        else
            replayStateLabel = "Replay Pausado";

        SetText(textReplay, replayStateLabel);
        SetText(turnText, turnLabel >= 0 ? $"Turno: {turnLabel}" : "Turno: -");
        SetText(observerText, $"Observador: {FormatObserverTeamLabel(replayManager.ObserverTeam)}");
        SetText(visionModeText, $"Visao: {FormatVisionModeLabel(replayManager.VisionMode, replayManager.ObserverTeam)}");
        SetText(stepText, $"Step: {currentSnapshotIndex}/{totalSnapshots}");
        SetText(startConfigText, BuildReplayStartConfigLabel());

        if (fastReplayModeToggle != null)
        {
            fastReplayModeToggle.SetIsOnWithoutNotify(replayManager.FastReplayMode);
            fastReplayModeToggle.interactable = true;
        }

        bool hasReplayBatches = totalBatches > 0;
        bool hasReplayHistory = replayManager.MatchHistory != null && replayManager.MatchHistory.Count > 0;
        bool hasReplayData = hasReplayBatches || hasReplayHistory;
        bool isReplaying = replayManager.IsReplaying;
        bool isPlaying = replayManager.IsPlaying;
        bool gateOpen = replaySessionArmed;
        bool isBusy = replayManager.IsStepExecutionBusy;
        bool canStepBack = gateOpen && isReplaying && !isPlaying && !isBusy && replayManager.CurrentStepIndex >= 0;
        bool canStepForward = gateOpen && isReplaying && !isPlaying && !isBusy && totalBatches > 0 && replayManager.CurrentStepIndex < totalBatches - 1;

        ApplyButtonsState(hasReplayData, hasReplayBatches, gateOpen, isReplaying, isPlaying, canStepBack, canStepForward, isBusy);
    }

    private void ApplyButtonsState(bool hasReplayData, bool hasReplayBatches, bool gateOpen, bool isReplaying, bool isPlaying, bool canStepBack, bool canStepForward, bool isBusy)
    {
        if (startButton != null)
            startButton.interactable = hasReplayData && !isReplaying;

        if (playButton != null)
            playButton.interactable = hasReplayBatches && gateOpen && isReplaying && !isPlaying && !isBusy;

        if (pauseButton != null)
            pauseButton.interactable = isReplaying && (isPlaying || isBusy);

        if (stepBackButton != null)
            stepBackButton.interactable = canStepBack;

        if (stepForwardButton != null)
            stepForwardButton.interactable = canStepForward;

        if (stopButton != null)
            stopButton.interactable = gateOpen && isReplaying;
    }

    private string BuildReplayStartConfigLabel()
    {
        switch (replayStartMode)
        {
            case ReplayStartMode.FromBeginning:
                return "Inicio: snapshot 0 (inicio do jogo/load)";
            case ReplayStartMode.FromSpecificTurnTeam:
                TeamId team = (TeamId)Mathf.Clamp(specificTeam, (int)TeamId.Neutral, (int)TeamId.Yellow);
                string teamLabel = team == TeamId.Neutral
                    ? "qualquer time"
                    : $"Time {(int)team} ({TeamUtils.GetName(team)})";
                return $"Inicio: especifico | {teamLabel} | Turno {Mathf.Max(0, specificTurn)}";
            default:
                return "Inicio: atual (ultimo snapshot da pilha)";
        }
    }
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private static string FormatObserverTeamLabel(TeamId teamId)
    {
        if (teamId == TeamId.Neutral)
            return "Qualquer time";

        return $"Time {TeamUtils.GetName(teamId)}";
    }

    private static string FormatVisionModeLabel(ReplayVisionMode mode, TeamId observerTeam)
    {
        if (mode == ReplayVisionMode.Omniscient)
            return "Omnisciente";

        if (mode == ReplayVisionMode.TeamFiltered)
        {
            if (observerTeam == TeamId.Neutral)
                return "Qualquer time";
            return $"Time {TeamUtils.GetName(observerTeam)}";
        }

        return mode.ToString();
    }

    private void TryAutoPauseReplayOnPanelClose(bool showDialog)
    {
        if (replayManager == null)
            return;
        if (!replayManager.IsReplaying || !replayManager.IsPlaying)
            return;

        replayManager.PausePlayback();

        if (showDialog)
            ShowReplayDialog("dialog.replay.paused", "replay pausado");
    }

    private static void ShowReplayDialog(string id, string fallback)
    {
        string text = PanelDialogController.ResolveDialogMessage(id, fallback);
        PanelDialogController.TrySetTransientText(text, 2.2f);
    }
}










