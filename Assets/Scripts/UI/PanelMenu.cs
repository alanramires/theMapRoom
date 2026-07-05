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
    [SerializeField] private Button buttonLoad;
    [SerializeField] private Button buttonTutorial;
    [FormerlySerializedAs("buttonSobre")]
    [SerializeField] private Button buttonConfig;
    [SerializeField] private Button buttonCinematic;
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
    private Vector2 previousUiMove;
    private bool previousUiSubmitPressed;
    private bool previousUiCancelPressed;
    private int ignoreInputUntilFrame = -1;
    private int lastLoadOpenRequestFrame = -1;

    public int CurrentIndex => currentIndex;

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

        if (quitConfirmOpen)
        {
            if (confirmPressed)
            {
                ConfirmQuitGame();
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
        List<Button> list = new List<Button>(6);
        AddIfActive(list, buttonNew);
        AddIfActive(list, buttonLoad);
        AddIfActive(list, buttonTutorial);
        AddIfActive(list, buttonConfig);
        AddIfActive(list, buttonCinematic);
        AddIfActive(list, buttonSair);
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
        if (buttonLoad == null) buttonLoad = FindButtonByNames("button_load", "carregar", "load");
        if (buttonTutorial == null) buttonTutorial = FindButtonByNames("button_tutorial", "tutorial");
        if (buttonConfig == null) buttonConfig = FindButtonByNames("button_config", "button_sobre", "config", "sobre", "about");
        if (buttonCinematic == null) buttonCinematic = FindButtonByNames("button_cinematic", "cinematic", "cinema");
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

        if (buttonConfig != null)
        {
            buttonConfig.onClick.RemoveListener(OnConfigButtonClicked);
            buttonConfig.onClick.AddListener(OnConfigButtonClicked);
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

        TeamId[] teams  = { TeamId.Green, TeamId.Red };
        bool[]   isAI   = { false, true };
        bool[]   flipX  = { false, true };
        bool[]   cmdAuto= { false, true };
        const MatchController.GameSetupPreset preset = MatchController.GameSetupPreset.FogOfWarTotal;
        const string target = "Battle Map 1 - Ground";

        SaveGameManager.SetupForNewGame(string.Empty);
        PartidaConfig.Set(2, teams, isAI, flipX, preset, cmdAuto, target);
        UnityEngine.SceneManagement.SceneManager.LoadScene(target);
    }

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
        if (buttonConfig != null)
            SyncCurrentIndexWithButton(buttonConfig);

        PlayConfirmSfxOncePerFrame();
        if (stateController != null)
        {
            stateController.RequestState(MainMenuState.Config);
            return;
        }

        OpenPanelAndHideMenu(panelConfigRoot, "Panel_Config");
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
        PanelDialogController.ClearExternalText();
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
        // Evita auto-confirmacao no frame seguinte quando o mesmo submit
        // (mouse/enter/gamepad) que abriu o dialogo ainda esta pressionado.
        ignoreInputUntilFrame = Time.frameCount + 1;
        previousUiSubmitPressed = true;
        string text = PanelDialogController.ResolveDialogMessage(
            "dialog.main_menu.quit_confirm",
            "sair para o windows?\nENTER: sim | ESC: nao");
        PanelDialogController.TrySetExternalText(text);
        cursorController?.PlayBeepSfx();
    }

    private void CancelQuitGame()
    {
        if (!quitConfirmOpen)
            return;

        quitConfirmOpen = false;
        PanelDialogController.ClearExternalText();
        cursorController?.PlayCancelSfx();
    }

    private void ConfirmQuitGame()
    {
        if (!quitConfirmOpen)
            return;

        quitConfirmOpen = false;
        PanelDialogController.ClearExternalText();
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
