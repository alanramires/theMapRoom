using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-300)]
public class PanelMenu : MonoBehaviour
{
    [Header("Menu Buttons")]
    [SerializeField] private Button buttonNew;
    [SerializeField] private Button buttonHotseat;
    [SerializeField] private Button buttonLoad;
    [SerializeField] private Button buttonTutorial;
    [FormerlySerializedAs("buttonSobre")]
    [SerializeField] private Button buttonConfig;
    [SerializeField] private Button buttonAbout;
    [SerializeField] private Button buttonCinematic;
    [SerializeField] private Button buttonFullscreen;
    [SerializeField] private Button buttonSair;
    [SerializeField] private int defaultButtonIndex = 0;
    [SerializeField] private bool wrapSelection = true;

    [Header("Panels")]
    [SerializeField] private GameObject panelNewGameRoot;
    [SerializeField] private GameObject panelTutorialRoot;
    [SerializeField] private GameObject panelConfigRoot;

    [Header("Compass Cursor")]
    [SerializeField] private RectTransform compassCursor;
    [SerializeField] private float compassOffsetX = -80f;
    [SerializeField] private float compassOffsetY = 0f;

    [Header("References")]
    [SerializeField] private CursorController cursorController;
    [SerializeField] private MainMenuLoadPanelController loadPanelController;
    [SerializeField] private MainMenuCinematicController cinematicController;
    [SerializeField] private MainMenuStateController stateController;

    private int currentIndex;
    private bool buttonCallbacksBound;
    private int lastConfirmSfxFrame = -1;
    private bool pendingInitialFocus;
    private bool quitConfirmOpen;
    private bool aboutOpen;
    private int quitConfirmFocusIndex;
    private Vector2 previousUiMove;
    private bool previousUiSubmitPressed;
    private bool previousUiCancelPressed;
    private int ignoreInputUntilFrame = -1;
    private int lastLoadOpenRequestFrame = -1;
    private bool newGameWizardOpen;
    private bool newGameHotSeat;
    private int newGameWizardStep;
    private int newGameWizardFocusIndex;
    private TeamId newGameHumanTeam = TeamId.Green;
    private TeamId newGameAiTeam = TeamId.Red;
    private AIDifficulty newGameDifficulty = AIDifficulty.Iniciante;
    private MatchController.GameSetupPreset newGamePreset = MatchController.GameSetupPreset.GameBoyClassic;

    private static readonly TeamId[] NewGameTeams = { TeamId.Green, TeamId.Red, TeamId.Blue, TeamId.Yellow };
    private readonly struct CampaignDifficultyOption
    {
        public readonly string Label;
        public readonly string Description;
        public readonly AIDifficulty AiDifficulty;
        public readonly MatchController.GameSetupPreset Preset;

        public CampaignDifficultyOption(
            string label,
            string description,
            AIDifficulty aiDifficulty,
            MatchController.GameSetupPreset preset)
        {
            Label = label;
            Description = description;
            AiDifficulty = aiDifficulty;
            Preset = preset;
        }
    }

    private static readonly CampaignDifficultyOption[] CampaignDifficulties =
    {
        new CampaignDifficultyOption(
            "FÁCIL",
            "AI Easy + Game Boy Clássico",
            AIDifficulty.Iniciante,
            MatchController.GameSetupPreset.GameBoyClassic),
        new CampaignDifficultyOption(
            "MÉDIO",
            "AI Normal + Neblina Leve",
            AIDifficulty.Facil,
            MatchController.GameSetupPreset.NeblinaLeve),
        new CampaignDifficultyOption(
            "DIFÍCIL",
            "AI Difícil + Fog of War Total",
            AIDifficulty.Competitiva,
            MatchController.GameSetupPreset.FogOfWarTotal)
    };
    private static readonly MatchController.GameSetupPreset[] NewGamePresets =
    {
        MatchController.GameSetupPreset.GameBoyClassic,
        MatchController.GameSetupPreset.FisicaBasica,
        MatchController.GameSetupPreset.AMontanhaAvacalha,
        MatchController.GameSetupPreset.NeblinaLeve,
        MatchController.GameSetupPreset.FogOfWarTotal
    };

    public int CurrentIndex => currentIndex;
    public bool IsNewGameWizardOpen => newGameWizardOpen;
    public int NewGameWizardFocusIndex => newGameWizardFocusIndex;
    public int NewGameWizardStep => newGameWizardStep;
    public bool IsAboutOpen => aboutOpen;
    public string AboutBody =>
        "Um wargame tático em hexágonos, por turnos, onde nada é definitivo até você confirmar.\n\n" +
        "Mova, mire, planeje e cancele quando quiser. Só quando você diz sim é que a guerra acontece.\n\n" +
        "Comande infantaria, blindados, transportes e apoio de fogo através da neblina da guerra. " +
        "Capture território, sustente sua logística e escolha entre golpes ousados ou avanços cautelosos.\n\n" +
        "Enfrente uma IA com tática própria ou desafie um amigo no mesmo dispositivo em modo hot seat.";

    public string GetNewGameWizardConfirmationSummary()
    {
        string targetMap = newGameHotSeat ? "Hot Seat 1 - Pvp" : "Campanha";
        string humanColor = ColorUtility.ToHtmlStringRGB(TeamUtils.GetColor(newGameHumanTeam));
        string aiColor = ColorUtility.ToHtmlStringRGB(TeamUtils.GetColor(newGameAiTeam));
        return $"MAPA: {targetMap}\n" +
               $"SETUP: {ResolvePresetLabel(newGamePreset)}\n" +
               (newGameHotSeat ? string.Empty : $"DIFICULDADE: {ResolveCampaignDifficultyLabel(newGameDifficulty)}\n") +
               $"JOGADOR 1: <color=#{humanColor}>{ResolveTeamLabel(newGameHumanTeam)}</color>\n" +
               $"JOGADOR 2: <color=#{aiColor}>{ResolveTeamLabel(newGameAiTeam)}</color>{(newGameHotSeat ? string.Empty : " (IA)")}\n\n" +
               $"REGRAS\n{NewGamePanelController.BuildDescricao(newGamePreset)}";
    }

    // Passo 4 = CONFIRMAR PARTIDA. Exposto para o PanelHelper montar os detalhes de confirmacao
    // sem depender do numero magico do passo.
    public bool IsNewGameWizardConfirmStep => newGameWizardOpen && newGameWizardStep == 4;

    public int GetNewGameWizardOptionCount()
    {
        if (!newGameWizardOpen) return 0;
        if (newGameWizardStep == 0) return NewGameTeams.Length + 1;
        if (newGameWizardStep == 1) return NewGameTeams.Length;
        if (newGameWizardStep == 2) return CampaignDifficulties.Length + 1;
        if (newGameWizardStep == 3) return NewGamePresets.Length + 1;
        return 2;
    }

    public string GetNewGameWizardOptionLabel(int index)
    {
        if (newGameWizardStep == 0)
            return index < NewGameTeams.Length ? ResolveTeamLabel(NewGameTeams[index]) : "CANCELAR";
        if (newGameWizardStep == 1)
        {
            List<TeamId> opponents = BuildAvailableOpponentTeams();
            return index < opponents.Count
                ? $"{(newGameHotSeat ? "JOGADOR 2" : "IA")} {ResolveTeamLabel(opponents[index])}"
                : "VOLTAR";
        }
        if (newGameWizardStep == 2)
            return index < CampaignDifficulties.Length ? CampaignDifficulties[index].Label : "VOLTAR";
        if (newGameWizardStep == 3)
            return index < NewGamePresets.Length ? ResolvePresetLabel(NewGamePresets[index]) : "VOLTAR";
        return index == 0 ? "INICIAR JOGO" : "VOLTAR";
    }

    public bool TryGetNewGameWizardOptionColor(int index, out Color color)
    {
        color = Color.white;
        TeamId team;
        if (newGameWizardStep == 0 && index >= 0 && index < NewGameTeams.Length)
            team = NewGameTeams[index];
        else if (newGameWizardStep == 1)
        {
            List<TeamId> opponents = BuildAvailableOpponentTeams();
            if (index < 0 || index >= opponents.Count) return false;
            team = opponents[index];
        }
        else return false;

        color = TeamUtils.GetColor(team);
        return true;
    }

    public bool NavigateNewGameWizard(int direction)
    {
        if (!newGameWizardOpen || direction == 0) return false;
        int count = GetNewGameWizardOptionCount();
        newGameWizardFocusIndex = (newGameWizardFocusIndex + (direction > 0 ? 1 : -1) + count) % count;
        if (newGameWizardStep == 2)
            RefreshNewGameWizardHelper();
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public void InvokeNewGameWizardOption(int index)
    {
        if (!newGameWizardOpen || index < 0 || index >= GetNewGameWizardOptionCount()) return;
        newGameWizardFocusIndex = index;
        if (newGameWizardStep == 0)
        {
            if (index >= NewGameTeams.Length) { CloseNewGameWizard(); return; }
            newGameHumanTeam = NewGameTeams[index];
            newGameWizardStep = 1;
        }
        else if (newGameWizardStep == 1)
        {
            List<TeamId> opponents = BuildAvailableOpponentTeams();
            if (index >= opponents.Count) { newGameWizardStep = 0; }
            else { newGameAiTeam = opponents[index]; newGameWizardStep = newGameHotSeat ? 3 : 2; }
        }
        else if (newGameWizardStep == 2)
        {
            if (index >= CampaignDifficulties.Length) { newGameWizardStep = 1; }
            else
            {
                CampaignDifficultyOption option = CampaignDifficulties[index];
                newGameDifficulty = option.AiDifficulty;
                newGamePreset = option.Preset;
                newGameWizardStep = 4;
            }
        }
        else if (newGameWizardStep == 3)
        {
            if (index >= NewGamePresets.Length) { newGameWizardStep = newGameHotSeat ? 1 : 2; }
            else { newGamePreset = NewGamePresets[index]; newGameWizardStep = 4; }
        }
        else if (index == 0)
        {
            StartConfiguredNewGame();
            return;
        }
        else newGameWizardStep = newGameHotSeat ? 3 : 2;

        newGameWizardFocusIndex = 0;
        RefreshNewGameWizardHelper();
        cursorController?.PlayConfirmSfx();
    }

    public void InvokeFocusedNewGameWizardOption() => InvokeNewGameWizardOption(newGameWizardFocusIndex);

    public void CancelNewGameWizardStep()
    {
        if (!newGameWizardOpen) return;
        if (newGameWizardStep <= 0) CloseNewGameWizard();
        else
        {
            if (newGameWizardStep == 4)
                newGameWizardStep = newGameHotSeat ? 3 : 2;
            else
                newGameWizardStep = newGameHotSeat && newGameWizardStep == 3 ? 1 : newGameWizardStep - 1;
            newGameWizardFocusIndex = 0;
            RefreshNewGameWizardHelper();
            cursorController?.PlayCancelSfx();
        }
    }

    protected virtual void Awake()
    {
        ResolveMenuButtonsIfNeeded();
        EnsureRootLoadButtonReference();

        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (loadPanelController == null)
            loadPanelController = FindLoadPanelControllerIncludingInactive();
        if (cinematicController == null)
            cinematicController = FindAnyObjectByType<MainMenuCinematicController>();
        if (stateController == null)
            stateController = MainMenuStateController.EnsureSceneInstance();
        ResolvePanelReferencesIfNeeded();
        BindFullscreenShortcutIfNeeded();

        BindButtonCallbacksIfNeeded();
        ClampCurrentIndex();
        pendingInitialFocus = true;
    }

    protected virtual void OnEnable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = false;
    }

    protected virtual void OnDisable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
    }

    protected virtual void Update()
    {
        if (PanelRodadaController.IsGameplayInputBlocked)
            return;

        if (stateController != null)
            return;

        EnsureInitialSelectionIfNeeded();

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (stateController != null && !stateController.IsRootMenuInteractiveState)
            return;

        if (cinematicController != null && cinematicController.IsPlaying)
            return;

        // Enquanto o menu principal estiver ativo, bloqueia atalhos de gameplay
        // para impedir vazamento de Enter/Esc para o CursorController.
        UiInputBlocker.SuppressGameplayInputForFrames(1);

        if (Time.frameCount <= ignoreInputUntilFrame)
            return;

        if (IsFocusedOnTextInputControl())
            return;

        ReadMenuInput(
            out bool upPressed,
            out bool downPressed,
            out bool leftPressed,
            out bool rightPressed,
            out bool confirmPressed,
            out bool cancelPressed);

        if (aboutOpen)
        {
            if (confirmPressed || cancelPressed)
                CloseAbout();
            return;
        }

        if (newGameWizardOpen)
        {
            if (upPressed || leftPressed) { NavigateNewGameWizard(-1); return; }
            if (downPressed || rightPressed) { NavigateNewGameWizard(+1); return; }
            if (confirmPressed) { InvokeFocusedNewGameWizardOption(); return; }
            if (cancelPressed) { CancelNewGameWizardStep(); return; }
            return;
        }

        if (quitConfirmOpen)
        {
            if (upPressed || leftPressed)
            {
                NavigateQuitConfirmation(-1);
                return;
            }
            if (downPressed || rightPressed)
            {
                NavigateQuitConfirmation(+1);
                return;
            }
            if (confirmPressed)
            {
                InvokeFocusedQuitConfirmation();
                return;
            }

            if (cancelPressed)
            {
                CancelQuitGame();
                return;
            }

            return;
        }

        if (upPressed || leftPressed)
        {
            Navigate(-1);
            return;
        }

        if (downPressed || rightPressed)
        {
            Navigate(+1);
            return;
        }

        if (confirmPressed)
        {
            ConfirmCurrentSelection();
            return;
        }

        if (cancelPressed)
            CancelToDefault();
    }

    private void EnsureRootLoadButtonReference()
    {
        if (buttonLoad == null || buttonLoad.name == null)
            return;

        string n = buttonLoad.name.ToLowerInvariant();
        if (n.Contains("load1") || n.Contains("load2") || n.Contains("load3"))
            buttonLoad = FindButtonByNames("button_load", "carregar", "load");
    }

    public void ShowRootMenu()
    {
        EnterRootMenu(resetToDefault: true);
    }

    public void EnterRootMenu(bool resetToDefault)
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = false;
            EventSystem.current.SetSelectedGameObject(null);
        }

        previousUiMove = Vector2.zero;
        previousUiSubmitPressed = false;
        previousUiCancelPressed = false;
        ignoreInputUntilFrame = Time.frameCount + 3;
        pendingInitialFocus = false;
        UiInputBlocker.SuppressGameplayInputForFrames(2);

        List<Button> buttons = GetRootButtons();
        if (resetToDefault)
            currentIndex = Mathf.Clamp(defaultButtonIndex, 0, Mathf.Max(0, buttons.Count - 1));
        else
            ClampCurrentIndex();

        SelectCurrentButton(playSfx: false);
    }

    public void ExitRootMenu()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);

        previousUiMove = Vector2.zero;
        previousUiSubmitPressed = false;
        previousUiCancelPressed = false;
        pendingInitialFocus = false;
    }

    public bool Navigate(int delta)
    {
        List<Button> buttons = GetRootButtons();
        if (buttons.Count <= 0 || delta == 0)
            return false;

        int original = currentIndex;
        currentIndex += delta;

        if (wrapSelection)
        {
            int count = buttons.Count;
            while (currentIndex < 0)
                currentIndex += count;
            while (currentIndex >= count)
                currentIndex -= count;
        }
        else
        {
            currentIndex = Mathf.Clamp(currentIndex, 0, buttons.Count - 1);
        }

        if (currentIndex == original)
            return false;

        SelectCurrentButton(playSfx: true);
        return true;
    }

    public void ConfirmCurrentSelection()
    {
        Button button = GetCurrentButton();
        if (button == null || !button.interactable)
            return;

        // "Sair" usa fluxo de confirmacao proprio (PanelDialog).
        // Nao invoca onClick diretamente para evitar listener legado de quit imediato.
        if (buttonSair != null && button == buttonSair)
        {
            OnQuitButtonClicked();
            return;
        }

        if (buttonFullscreen != null && button == buttonFullscreen)
        {
            PlayConfirmSfxOncePerFrame();
            buttonFullscreen.GetComponent<FullscreenShortcutButton>()?.ToggleFullscreen();
            return;
        }

        button.onClick?.Invoke();
    }

    public void CancelToDefault()
    {
        List<Button> buttons = GetRootButtons();
        cursorController?.PlayCancelSfx();
        currentIndex = Mathf.Clamp(defaultButtonIndex, 0, Mathf.Max(0, buttons.Count - 1));
        SelectCurrentButton(playSfx: false);
    }

    public bool IsQuitConfirmationOpen => quitConfirmOpen;
    public int QuitConfirmationFocusIndex => quitConfirmFocusIndex;

    public bool NavigateQuitConfirmation(int direction)
    {
        if (!quitConfirmOpen || direction == 0) return false;
        quitConfirmFocusIndex = (quitConfirmFocusIndex + (direction > 0 ? 1 : -1) + 2) % 2;
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public void InvokeFocusedQuitConfirmation()
    {
        if (!quitConfirmOpen) return;
        if (quitConfirmFocusIndex == 0) ConfirmQuitGame();
        else CancelQuitGame();
    }

    public void ConfirmQuitFromPointer() { quitConfirmFocusIndex = 0; ConfirmQuitGame(); }
    public void CancelQuitFromPointer() { quitConfirmFocusIndex = 1; CancelQuitGame(); }

    public void SetCurrentIndex(int index)
    {
        currentIndex = index;
        ClampCurrentIndex();
        RefreshCompassCursor();
    }

    private void SelectCurrentButton(bool playSfx)
    {
        List<Button> buttons = GetRootButtons();
        if (buttons.Count <= 0)
            return;

        ClampCurrentIndex();
        Button button = buttons[Mathf.Clamp(currentIndex, 0, buttons.Count - 1)];
        if (button == null)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);

        UpdateCompassCursor(button);

        if (playSfx)
            cursorController?.PlayCursorMoveSfx();
    }

    private void UpdateCompassCursor(Button button)
    {
        if (compassCursor == null || button == null)
            return;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect == null)
            return;

        RectTransform parentRect = compassCursor.parent as RectTransform;
        if (parentRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        RectTransform layoutRoot = buttonRect.parent as RectTransform;
        if (layoutRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

        Vector3[] corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        Vector2 localCenter = parentRect.InverseTransformPoint(worldCenter);

        compassCursor.anchoredPosition = new Vector2(
            localCenter.x + compassOffsetX,
            localCenter.y + compassOffsetY);
    }

    private void RefreshCompassCursor()
    {
        Button button = GetCurrentButton();
        if (button != null)
            UpdateCompassCursor(button);
    }

    public void SetCompassCursorVisible(bool visible)
    {
        if (compassCursor != null)
            compassCursor.gameObject.SetActive(visible);
    }

    private Button GetCurrentButton()
    {
        List<Button> buttons = GetRootButtons();
        if (buttons.Count <= 0)
            return null;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject == selected)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        ClampCurrentIndex();
        return buttons[Mathf.Clamp(currentIndex, 0, buttons.Count - 1)];
    }

    private void ClampCurrentIndex()
    {
        List<Button> buttons = GetRootButtons();
        if (buttons.Count <= 0)
        {
            currentIndex = 0;
            return;
        }

        int defaultIndex = Mathf.Clamp(defaultButtonIndex, 0, buttons.Count - 1);
        if (currentIndex < 0 || currentIndex >= buttons.Count)
            currentIndex = defaultIndex;
    }

    private List<Button> GetRootButtons()
    {
        List<Button> list = new List<Button>(9);
        AddIfActive(list, buttonNew);
        AddIfActive(list, buttonHotseat);
        AddIfActive(list, buttonLoad);
        AddIfActive(list, buttonConfig);
        AddIfActive(list, buttonAbout);
        AddIfActive(list, buttonCinematic);
        AddIfActive(list, buttonFullscreen);
        AddIfActive(list, buttonTutorial);
        AddIfActive(list, buttonSair);
        list.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        return list;
    }

    private static void AddIfActive(List<Button> list, Button button)
    {
        if (button != null && button.gameObject.activeInHierarchy)
            list.Add(button);
    }

    private void ResolveMenuButtonsIfNeeded()
    {
        if (buttonNew == null) buttonNew = FindButtonByNames("button_new", "novo", "new");
        if (buttonHotseat == null) buttonHotseat = FindButtonByNames("button_hotseat", "hotseat");
        if (buttonLoad == null) buttonLoad = FindButtonByNames("button_load", "carregar", "load");
        if (buttonTutorial == null) buttonTutorial = FindButtonByNames("button_tutorial", "tutorial");
        if (buttonConfig == null) buttonConfig = FindButtonByNames("button_config", "config");
        if (buttonAbout == null) buttonAbout = FindButtonByNames("button_sobre", "sobre", "about");
        if (buttonCinematic == null) buttonCinematic = FindButtonByNames("button_cinematic", "cinematic", "cinema");
        if (buttonFullscreen == null) buttonFullscreen = FindButtonByNames("button_TelaCheia", "tela_cheia", "fullscreen");
        if (buttonSair == null) buttonSair = FindButtonByNames("button_sair", "sair", "quit", "exit");
        if (buttonSair == null) buttonSair = FindButtonByLabel("sair", "quit", "exit");
    }

    private void ResolvePanelReferencesIfNeeded()
    {
        if (panelNewGameRoot == null)
        {
            Transform t = FindTransformByName("Panel_NewGame");
            if (t != null)
                panelNewGameRoot = t.gameObject;
        }

        if (panelTutorialRoot == null)
        {
            Transform t = FindTransformByName("Panel_Tutorial");
            if (t != null)
                panelTutorialRoot = t.gameObject;
        }

        if (panelConfigRoot == null)
        {
            Transform t = FindTransformByName("Panel_Config");
            if (t != null)
                panelConfigRoot = t.gameObject;
        }
    }

    private void BindFullscreenShortcutIfNeeded()
    {
        if (!Application.isPlaying)
            return;

        Transform shortcut = FindTransformByName("button_TelaCheia");
        if (shortcut == null)
            return;

        if (buttonFullscreen == null)
            buttonFullscreen = shortcut.GetComponent<Button>();
        if (buttonFullscreen == null)
            buttonFullscreen = shortcut.gameObject.AddComponent<Button>();

        Image image = shortcut.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            buttonFullscreen.targetGraphic = image;
        }
        if (shortcut.GetComponent<FullscreenShortcutButton>() == null)
            shortcut.gameObject.AddComponent<FullscreenShortcutButton>();
    }

    private Button FindButtonByNames(params string[] keywords)
    {
        // 1) Prioriza filhos do proprio painel/menu para evitar capturar botoes de outros painéis (ex.: load1/load2/load3).
        Button[] localButtons = GetComponentsInChildren<Button>(includeInactive: true);
        Button localMatch = FindBestButtonMatch(localButtons, keywords, requireRootLoadButton: true);
        if (localMatch != null)
            return localMatch;

        // 2) Fallback global na cena ativa.
        Scene active = SceneManager.GetActiveScene();
        Button[] sceneButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return FindBestButtonMatch(sceneButtons, keywords, requireRootLoadButton: true, activeScene: active);
    }

    private static Button FindBestButtonMatch(Button[] buttons, string[] keywords, bool requireRootLoadButton, Scene? activeScene = null)
    {
        if (buttons == null || buttons.Length == 0)
            return null;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null || b.name == null)
                continue;
            if (activeScene.HasValue && b.gameObject.scene != activeScene.Value)
                continue;

            string n = b.name.ToLowerInvariant();
            bool matched = false;
            for (int k = 0; k < keywords.Length; k++)
            {
                string key = keywords[k];
                if (!string.IsNullOrWhiteSpace(key) && n.Contains(key))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
                continue;

            if (requireRootLoadButton)
            {
                // Evita confundir Button_load (menu raiz) com Button_load1/2/3 (slots).
                if (n.Contains("load1") || n.Contains("load2") || n.Contains("load3"))
                    continue;
                if (n.Contains("del1") || n.Contains("del2") || n.Contains("del3"))
                    continue;
            }

            return b;
        }

        return null;
    }

    private Button FindButtonByLabel(params string[] labelKeywords)
    {
        if (labelKeywords == null || labelKeywords.Length == 0)
            return null;

        Scene active = SceneManager.GetActiveScene();
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null || b.gameObject.scene != active)
                continue;

            string label = ExtractButtonLabelLower(b);
            if (string.IsNullOrWhiteSpace(label))
                continue;

            for (int k = 0; k < labelKeywords.Length; k++)
            {
                string key = labelKeywords[k];
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (label.Contains(key.ToLowerInvariant()))
                    return b;
            }
        }

        return null;
    }

    private static string ExtractButtonLabelLower(Button button)
    {
        if (button == null)
            return string.Empty;

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            return tmp.text.ToLowerInvariant();

        Text legacy = button.GetComponentInChildren<Text>(includeInactive: true);
        if (legacy != null && !string.IsNullOrWhiteSpace(legacy.text))
            return legacy.text.ToLowerInvariant();

        return string.Empty;
    }

    private void BindButtonCallbacksIfNeeded()
    {
        if (buttonCallbacksBound)
            return;

        if (buttonNew != null)
        {
            buttonNew.onClick.RemoveListener(OnNewButtonClicked);
            buttonNew.onClick.AddListener(OnNewButtonClicked);
        }

        if (buttonHotseat != null)
        {
            buttonHotseat.onClick.RemoveListener(OnHotseatButtonClicked);
            buttonHotseat.onClick.AddListener(OnHotseatButtonClicked);
        }

        if (buttonLoad != null)
        {
            buttonLoad.onClick.RemoveListener(OnLoadButtonClicked);
            buttonLoad.onClick.AddListener(OnLoadButtonClicked);
        }

        if (buttonTutorial != null)
        {
            buttonTutorial.onClick.RemoveListener(OnTutorialButtonClicked);
            buttonTutorial.onClick.AddListener(OnTutorialButtonClicked);
        }

        if (buttonAbout != null)
        {
            buttonAbout.onClick.RemoveListener(OnConfigButtonClicked);
            buttonAbout.onClick.AddListener(OnConfigButtonClicked);
        }

        if (buttonCinematic != null)
        {
            buttonCinematic.onClick.RemoveListener(OnCinematicButtonSelected);
            buttonCinematic.onClick.AddListener(OnCinematicButtonSelected);
        }

        if (buttonSair != null)
        {
            // Blindagem contra listener persistente no Inspector que fecha o jogo direto.
            buttonSair.onClick = new Button.ButtonClickedEvent();
            buttonSair.onClick.AddListener(OnQuitButtonClicked);
        }

        buttonCallbacksBound = true;
    }

    private void OnNewButtonClicked()
    {
        if (buttonNew != null)
            SyncCurrentIndexWithButton(buttonNew);

        PlayConfirmSfxOncePerFrame();

        newGameHotSeat = false;
        newGameWizardOpen = true;
        newGameWizardStep = 0;
        newGameWizardFocusIndex = 0;
        newGameHumanTeam = TeamId.Green;
        newGameAiTeam = TeamId.Red;
        newGameDifficulty = AIDifficulty.Iniciante;
        newGamePreset = MatchController.GameSetupPreset.GameBoyClassic;
        RefreshNewGameWizardHelper();
    }

    private void OnHotseatButtonClicked()
    {
        if (buttonHotseat != null)
            SyncCurrentIndexWithButton(buttonHotseat);

        PlayConfirmSfxOncePerFrame();
        newGameHotSeat = true;
        newGameWizardOpen = true;
        newGameWizardStep = 0;
        newGameWizardFocusIndex = 0;
        newGameHumanTeam = TeamId.Green;
        newGameAiTeam = TeamId.Red;
        newGamePreset = MatchController.GameSetupPreset.FogOfWarTotal;
        RefreshNewGameWizardHelper();
    }

    private void RefreshNewGameWizardHelper()
    {
        string title = newGameWizardStep == 0 ? "ESCOLHA SUA COR" :
                       newGameWizardStep == 1 ? (newGameHotSeat ? "ESCOLHA O JOGADOR 2" : "ESCOLHA A IA ADVERSÁRIA") :
                       newGameWizardStep == 2 ? "ESCOLHA A DIFICULDADE" :
                       newGameWizardStep == 3 ? "CONFIGURE O JOGO" : "CONFIRMAR PARTIDA";
        string body = newGameWizardStep == 4
            ? (newGameHotSeat
                ? $"Jogador 1: {ResolveTeamLabel(newGameHumanTeam)}\nJogador 2: {ResolveTeamLabel(newGameAiTeam)}\nRegras: {ResolvePresetLabel(newGamePreset)}"
                : $"Você: {ResolveTeamLabel(newGameHumanTeam)}\nIA adversária: {ResolveTeamLabel(newGameAiTeam)}\nDificuldade: {ResolveCampaignDifficultyLabel(newGameDifficulty)}\nRegras: {ResolvePresetLabel(newGamePreset)}")
            : (newGameWizardStep == 1
                ? (newGameHotSeat ? "Escolha a cor do segundo jogador." : "Slot 1 será controlado pela IA.")
                : newGameWizardStep == 2 && newGameWizardFocusIndex < CampaignDifficulties.Length
                    ? CampaignDifficulties[newGameWizardFocusIndex].Description
                    : string.Empty);
        PanelHelperController.TrySetExternalText(title, body);
    }

    private void CloseNewGameWizard()
    {
        newGameWizardOpen = false;
        newGameWizardStep = 0;
        newGameWizardFocusIndex = 0;
        PanelHelperController.ClearExternalText();
        cursorController?.PlayCancelSfx();
    }

    // Fecha o assistente de novo jogo sem SFX de cancelamento. Usado quando o jogador
    // abre "Carregar Jogo" com o assistente aberto — o painel do assistente (ESCOLHA
    // SUA COR etc.) precisa sumir para nao ficar sobreposto ao painel de load.
    public void CloseNewGameWizardIfOpen()
    {
        if (!newGameWizardOpen)
            return;
        newGameWizardOpen = false;
        newGameWizardStep = 0;
        newGameWizardFocusIndex = 0;
        PanelHelperController.ClearExternalText();
    }

    private List<TeamId> BuildAvailableOpponentTeams()
    {
        List<TeamId> result = new List<TeamId>(3);
        for (int i = 0; i < NewGameTeams.Length; i++)
            if (NewGameTeams[i] != newGameHumanTeam) result.Add(NewGameTeams[i]);
        return result;
    }

    private void StartConfiguredNewGame()
    {
        string target = newGameHotSeat ? "Hot Seat 1 - Pvp" : "Campanha";
        TeamId[] teams = { newGameHumanTeam, newGameAiTeam };
        bool[] isAI = { false, !newGameHotSeat };
        bool[] flipX = { IsTeamFlipped(newGameHumanTeam), IsTeamFlipped(newGameAiTeam) };
        bool[] cmdAuto = { false, !newGameHotSeat };
        SaveGameManager.SetupForNewGame(string.Empty);
        PartidaConfig.Set(2, teams, isAI, flipX, newGamePreset, cmdAuto, target);
        PartidaConfig.SetDifficulty(newGameDifficulty);
        newGameWizardOpen = false;
        PanelHelperController.ClearExternalText();
        SceneManager.LoadScene(target);
    }

    private static bool IsTeamFlipped(TeamId team) => team == TeamId.Red || team == TeamId.Yellow;
    private static string ResolveTeamLabel(TeamId team) => team switch
    {
        TeamId.Green => "VERDE", TeamId.Red => "VERMELHO", TeamId.Blue => "AZUL", TeamId.Yellow => "AMARELO", _ => team.ToString().ToUpperInvariant()
    };
    private static string ResolveCampaignDifficultyLabel(AIDifficulty difficulty) => difficulty switch
    {
        AIDifficulty.Iniciante => "FÁCIL",
        AIDifficulty.Facil => "MÉDIO",
        AIDifficulty.Competitiva => "DIFÍCIL",
        _ => "FÁCIL"
    };
    private static string ResolvePresetLabel(MatchController.GameSetupPreset preset) => preset switch
    {
        MatchController.GameSetupPreset.GameBoyClassic => "GAME BOY CLÁSSICO",
        MatchController.GameSetupPreset.FisicaBasica => "FÍSICA BÁSICA",
        MatchController.GameSetupPreset.AMontanhaAvacalha => "A MONTANHA AVACALHA",
        MatchController.GameSetupPreset.NeblinaLeve => "NEBLINA LEVE",
        _ => "FOG OF WAR TOTAL"
    };

    private void OnLoadButtonClicked()
    {
        if (Time.frameCount == lastLoadOpenRequestFrame)
            return;
        lastLoadOpenRequestFrame = Time.frameCount;

        if (loadPanelController == null)
            loadPanelController = FindLoadPanelControllerIncludingInactive();

        if (buttonLoad != null)
            SyncCurrentIndexWithButton(buttonLoad);

        if (stateController != null)
        {
            stateController.RequestState(MainMenuState.LoadMenu);
            return;
        }

        if (loadPanelController != null)
        {
            loadPanelController.OpenLoadPanel();
            return;
        }

        Debug.LogWarning("[PanelMenu] MainMenuLoadPanelController nao encontrado para abrir panel_load.");
    }

    private void OnTutorialButtonClicked()
    {
        if (buttonTutorial != null)
            SyncCurrentIndexWithButton(buttonTutorial);

        PlayConfirmSfxOncePerFrame();
        if (stateController != null)
        {
            stateController.RequestState(MainMenuState.Tutorial);
            return;
        }

        OpenPanelAndHideMenu(panelTutorialRoot, "Panel_Tutorial");
    }

    private void OnConfigButtonClicked()
    {
        if (buttonAbout != null)
            SyncCurrentIndexWithButton(buttonAbout);

        PlayConfirmSfxOncePerFrame();
        aboutOpen = true;
        PanelHelperController.TrySetExternalText("Sobre o jogo", string.Empty);
        PanelHelperController.SetExternalWideMode(true);
    }

    public void ConfirmAboutFromPointer() => CloseAbout();

    public void CloseAbout()
    {
        if (!aboutOpen)
            return;

        aboutOpen = false;
        PanelHelperController.SetExternalWideMode(false);
        PanelHelperController.ClearExternalText();
        cursorController?.PlayCancelSfx();
    }

    private void OnCinematicButtonSelected()
    {
        if (buttonCinematic != null)
            SyncCurrentIndexWithButton(buttonCinematic);

        PlayConfirmSfxOncePerFrame();

        if (stateController != null)
            stateController.RequestState(MainMenuState.Cinematic);
    }

    private void OnQuitButtonClicked()
    {
        if (buttonSair != null)
            SyncCurrentIndexWithButton(buttonSair);

        if (stateController != null && stateController.CurrentState != MainMenuState.Exit)
        {
            stateController.RequestState(MainMenuState.Exit);
            return;
        }

        OpenQuitConfirmation();
    }

    public void RequestExitConfirmation()
    {
        OpenQuitConfirmation();
    }

    public void CloseExitConfirmationWithoutSfx()
    {
        if (!quitConfirmOpen)
            return;

        quitConfirmOpen = false;
        quitConfirmFocusIndex = 0;
        PanelDialogController.ClearExternalText();
        PanelHelperController.ClearExternalText();
    }

    public void CancelQuitGameFromState()
    {
        CancelQuitGame();
    }

    public void ConfirmQuitGameFromState()
    {
        ConfirmQuitGame();
    }

    private static MainMenuLoadPanelController FindLoadPanelControllerIncludingInactive()
    {
        Scene active = SceneManager.GetActiveScene();
        MainMenuLoadPanelController[] all = FindObjectsByType<MainMenuLoadPanelController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            MainMenuLoadPanelController c = all[i];
            if (c == null)
                continue;
            if (c.gameObject.scene == active)
                return c;
        }

        return null;
    }

    private void HandleButtonInvoked(int buttonIndex)
    {
        List<Button> buttons = GetRootButtons();
        currentIndex = Mathf.Clamp(buttonIndex, 0, Mathf.Max(0, buttons.Count - 1));

        if (EventSystem.current != null)
        {
            Button button = GetCurrentButton();
            if (button != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        PlayConfirmSfxOncePerFrame();
    }

    private void OpenPanelAndHideMenu(GameObject panel, string fallbackPanelName)
    {
        if (panel == null && !string.IsNullOrWhiteSpace(fallbackPanelName))
        {
            Transform found = FindTransformByName(fallbackPanelName);
            if (found != null)
                panel = found.gameObject;
        }

        if (panel == null)
        {
            cursorController?.PlayErrorSfx();
            Debug.LogWarning($"[PanelMenu] Painel '{fallbackPanelName}' nao encontrado.");
            return;
        }

        gameObject.SetActive(false);
        panel.SetActive(true);
    }

    private void OpenQuitConfirmation()
    {
        quitConfirmOpen = true;
        quitConfirmFocusIndex = 0;
        // Evita auto-confirmacao no frame seguinte quando o mesmo submit
        // (mouse/enter/gamepad) que abriu o dialogo ainda esta pressionado.
        ignoreInputUntilFrame = Time.frameCount + 1;
        previousUiSubmitPressed = true;
        PanelDialogController.ClearExternalText();
        PanelHelperController.TrySetExternalText("SAIR", "Sair e voltar para o Windows?");
        cursorController?.PlayBeepSfx();
    }

    private void CancelQuitGame()
    {
        if (!quitConfirmOpen)
            return;

        quitConfirmOpen = false;
        quitConfirmFocusIndex = 0;
        PanelDialogController.ClearExternalText();
        PanelHelperController.ClearExternalText();
        cursorController?.PlayCancelSfx();
    }

    private void ConfirmQuitGame()
    {
        if (!quitConfirmOpen)
            return;

        quitConfirmOpen = false;
        quitConfirmFocusIndex = 0;
        PanelDialogController.ClearExternalText();
        PanelHelperController.ClearExternalText();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static Transform FindTransformByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindInChildrenRecursive(roots[i].transform, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindInChildrenRecursive(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindInChildrenRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private void SyncCurrentIndexWithButton(Button button)
    {
        if (button == null)
            return;

        List<Button> buttons = GetRootButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == button)
            {
                currentIndex = i;
                RefreshCompassCursor();
                return;
            }
        }
    }

    private void PlayConfirmSfxOncePerFrame()
    {
        if (lastConfirmSfxFrame == Time.frameCount)
            return;

        lastConfirmSfxFrame = Time.frameCount;
        cursorController?.PlayConfirmSfx();
    }

    private void EnsureInitialSelectionIfNeeded()
    {
        if (!pendingInitialFocus)
            return;

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (EventSystem.current == null)
            return;

        ShowRootMenu();

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return;

        List<Button> buttons = GetRootButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            Button button = buttons[i];
            if (button != null && button.gameObject == selected)
            {
                pendingInitialFocus = false;
                return;
            }
        }
    }

    private void ReadMenuInput(
        out bool upPressed,
        out bool downPressed,
        out bool leftPressed,
        out bool rightPressed,
        out bool confirmPressed,
        out bool cancelPressed)
    {
        upPressed = false;
        downPressed = false;
        leftPressed = false;
        rightPressed = false;
        confirmPressed = false;
        cancelPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            upPressed = Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame;
            downPressed = Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;
            leftPressed = Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame;
            rightPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
            confirmPressed = Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
            cancelPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        InputSystemUIInputModule module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
        if (module != null)
        {
            Vector2 move = module.move.action != null ? module.move.action.ReadValue<Vector2>() : Vector2.zero;
            bool moveUpNow = move.y > 0.5f;
            bool moveDownNow = move.y < -0.5f;
            bool moveLeftNow = move.x < -0.5f;
            bool moveRightNow = move.x > 0.5f;

            bool moveUpPrev = previousUiMove.y > 0.5f;
            bool moveDownPrev = previousUiMove.y < -0.5f;
            bool moveLeftPrev = previousUiMove.x < -0.5f;
            bool moveRightPrev = previousUiMove.x > 0.5f;

            upPressed |= moveUpNow && !moveUpPrev;
            downPressed |= moveDownNow && !moveDownPrev;
            leftPressed |= moveLeftNow && !moveLeftPrev;
            rightPressed |= moveRightNow && !moveRightPrev;

            bool submitNow = module.submit.action != null && module.submit.action.IsPressed();
            bool cancelNow = module.cancel.action != null && module.cancel.action.IsPressed();
            confirmPressed |= submitNow && !previousUiSubmitPressed;
            cancelPressed |= cancelNow && !previousUiCancelPressed;

            previousUiMove = move;
            previousUiSubmitPressed = submitNow;
            previousUiCancelPressed = cancelNow;
        }
#endif

        upPressed |= Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        downPressed |= Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        leftPressed |= Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        rightPressed |= Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        confirmPressed |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        cancelPressed |= Input.GetKeyDown(KeyCode.Escape);

        // Fire TV / gamepad e clique direito (= ESC) tambem confirmam/cancelam no menu.
        confirmPressed |= RemoteInput.ConfirmDownThisFrame();
        cancelPressed |= RemoteInput.CancelDownThisFrame() || RemoteInput.RightClickCancelDownThisFrame();
    }

    private static bool IsFocusedOnTextInputControl()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            return false;

        GameObject selected = eventSystem.currentSelectedGameObject;
        InputField legacyInput = selected.GetComponentInParent<InputField>();
        if (legacyInput != null && legacyInput.isFocused)
            return true;

        TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
        return tmpInput != null && tmpInput.isFocused;
    }
}
