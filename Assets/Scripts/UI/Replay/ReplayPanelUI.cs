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

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button stepBackButton;
    [SerializeField] private Button stepForwardButton;
    [SerializeField] private Button stopButton;

    [Header("Input")]
    [SerializeField] private KeyCode togglePanelKey = KeyCode.F9;

    [SerializeField] private bool isOpen;
    [SerializeField] private bool replaySessionArmed;

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
        isOpen = open;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = open ? 1f : 0f;
            panelCanvasGroup.interactable = open;
            panelCanvasGroup.blocksRaycasts = open;
        }

        if (open && replayManager != null)
            replayManager.SetReplayVision(ReplayVisionMode.Omniscient, TeamId.Neutral);

        RefreshLabels();
    }

    public void CycleObserverTeamPlaceholder()
    {
        if (replayManager == null)
            return;

        TeamId next = replayManager.ObserverTeam;
        if (next < TeamId.Neutral || next >= TeamId.Yellow)
            next = TeamId.Neutral;
        else
            next = (TeamId)((int)next + 1);

        replayManager.SetReplayVision(replayManager.VisionMode, next);
        RefreshLabels();
    }

    private void TryAutoAssignUiBindings()
    {
        if (textReplay == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
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
    }

    private void BindButtonCallbacks()
    {
        startButton?.onClick.AddListener(OnStartClicked);
        stepBackButton?.onClick.AddListener(OnBackClicked);
        playButton?.onClick.AddListener(OnPlayClicked);
        pauseButton?.onClick.AddListener(OnPauseClicked);
        stepForwardButton?.onClick.AddListener(OnForwardClicked);
        stopButton?.onClick.AddListener(OnStopClicked);
    }

    private void UnbindButtonCallbacks()
    {
        startButton?.onClick.RemoveListener(OnStartClicked);
        stepBackButton?.onClick.RemoveListener(OnBackClicked);
        playButton?.onClick.RemoveListener(OnPlayClicked);
        pauseButton?.onClick.RemoveListener(OnPauseClicked);
        stepForwardButton?.onClick.RemoveListener(OnForwardClicked);
        stopButton?.onClick.RemoveListener(OnStopClicked);
    }

    private void OnStartClicked()
    {
        if (replayManager == null)
            return;

        replayManager.StartReplay();
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
        RefreshLabels();
    }

    private void OnPauseClicked()
    {
        if (replayManager == null)
            return;

        replayManager.PausePlayback();
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

    private void RefreshLabels()
    {
        if (replayManager == null)
        {
            SetText(textReplay, "Replay");
            SetText(turnText, "Turno: -");
            SetText(observerText, "Observador: -");
            SetText(visionModeText, "Visao: -");
            SetText(stepText, "Step: -");
            ApplyButtonsState(false, false, false, false, false, false, false);
            return;
        }

        ReplayTurnRecord record = replayManager.CurrentRecord;
        int totalSteps = record != null && record.Steps != null ? record.Steps.Count : 0;
        int executedSteps = Mathf.Clamp(replayManager.CurrentStepIndex + 1, 0, totalSteps);
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
        SetText(stepText, $"Step: {executedSteps}/{totalSteps}");

        bool hasAnyReplayData = totalSteps > 0 || (replayManager.MatchHistory != null && replayManager.MatchHistory.Count > 0);
        bool isReplaying = replayManager.IsReplaying;
        bool isPlaying = replayManager.IsPlaying;
        bool gateOpen = replaySessionArmed;
        bool isBusy = replayManager.IsStepExecutionBusy;
        bool canStepBack = gateOpen && isReplaying && !isPlaying && !isBusy && replayManager.CurrentStepIndex >= 0;
        bool canStepForward = gateOpen && isReplaying && !isPlaying && !isBusy && replayManager.CurrentStepIndex < totalSteps - 1;

        ApplyButtonsState(hasAnyReplayData, gateOpen, isReplaying, isPlaying, canStepBack, canStepForward, isBusy);
    }

    private void ApplyButtonsState(bool hasAnyReplayData, bool gateOpen, bool isReplaying, bool isPlaying, bool canStepBack, bool canStepForward, bool isBusy)
    {
        if (startButton != null)
            startButton.interactable = hasAnyReplayData && !isReplaying;

        if (playButton != null)
            playButton.interactable = hasAnyReplayData && gateOpen && isReplaying && !isPlaying && !isBusy;

        if (pauseButton != null)
            pauseButton.interactable = isReplaying && isPlaying;

        if (stepBackButton != null)
            stepBackButton.interactable = canStepBack;

        if (stepForwardButton != null)
            stepForwardButton.interactable = canStepForward;

        if (stopButton != null)
            stopButton.interactable = gateOpen && isReplaying;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private static string FormatObserverTeamLabel(TeamId teamId)
    {
        if (teamId == TeamId.Neutral)
            return "Neutro";

        return $"Time {TeamUtils.GetName(teamId)}";
    }

    private static string FormatVisionModeLabel(ReplayVisionMode mode, TeamId observerTeam)
    {
        if (mode == ReplayVisionMode.Omniscient)
            return "Neutro";

        if (mode == ReplayVisionMode.TeamFiltered)
        {
            if (observerTeam == TeamId.Neutral)
                return "Neutro";
            return $"Time {TeamUtils.GetName(observerTeam)}";
        }

        return mode.ToString();
    }
}
