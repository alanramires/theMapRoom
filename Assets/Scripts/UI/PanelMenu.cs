using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("References")]
    [SerializeField] private CursorController cursorController;
    [SerializeField] private MainMenuLoadPanelController loadPanelController;

    private int currentIndex;
    private bool buttonCallbacksBound;
    private int lastConfirmSfxFrame = -1;
    private bool pendingInitialFocus;
    private bool quitConfirmOpen;

    protected virtual void Awake()
    {
        ResolveMenuButtonsIfNeeded();
        EnsureRootLoadButtonReference();

        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (loadPanelController == null)
            loadPanelController = FindLoadPanelControllerIncludingInactive();
        ResolvePanelReferencesIfNeeded();

        BindButtonCallbacksIfNeeded();
        ClampCurrentIndex();
        pendingInitialFocus = true;
    }

    protected virtual void OnEnable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = false;

        pendingInitialFocus = true;
        ShowRootMenu();
    }

    protected virtual void OnDisable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
    }

    protected virtual void Update()
    {
        EnsureInitialSelectionIfNeeded();

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (loadPanelController != null && loadPanelController.IsOpen)
            return;

        if (IsAnyTextInputFocusedInUi())
            return;

        if (quitConfirmOpen)
        {
            if (WasConfirmPressed())
            {
                ConfirmQuitGame();
                return;
            }

            if (WasCancelPressed())
            {
                CancelQuitGame();
                return;
            }

            return;
        }

        if (WasUpPressed() || WasLeftPressed())
        {
            Navigate(-1);
            return;
        }

        if (WasDownPressed() || WasRightPressed())
        {
            Navigate(+1);
            return;
        }

        if (WasConfirmPressed())
        {
            ConfirmCurrentSelection();
            return;
        }

        if (WasCancelPressed())
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
        ClampCurrentIndex();
        SelectCurrentButton(playSfx: false);
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

        button.onClick?.Invoke();
    }

    public void CancelToDefault()
    {
        List<Button> buttons = GetRootButtons();
        cursorController?.PlayCancelSfx();
        currentIndex = Mathf.Clamp(defaultButtonIndex, 0, Mathf.Max(0, buttons.Count - 1));
        SelectCurrentButton(playSfx: false);
    }

    private void SelectCurrentButton(bool playSfx)
    {
        Button button = GetCurrentButton();
        if (button == null)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);

        if (playSfx)
            cursorController?.PlayCursorMoveSfx();
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
        if (buttonNew != null) list.Add(buttonNew);
        if (buttonLoad != null) list.Add(buttonLoad);
        if (buttonTutorial != null) list.Add(buttonTutorial);
        if (buttonConfig != null) list.Add(buttonConfig);
        if (buttonCinematic != null) list.Add(buttonCinematic);
        if (buttonSair != null) list.Add(buttonSair);
        return list;
    }

    private void ResolveMenuButtonsIfNeeded()
    {
        if (buttonNew == null) buttonNew = FindButtonByNames("button_new", "novo", "new");
        if (buttonLoad == null) buttonLoad = FindButtonByNames("button_load", "carregar", "load");
        if (buttonTutorial == null) buttonTutorial = FindButtonByNames("button_tutorial", "tutorial");
        if (buttonConfig == null) buttonConfig = FindButtonByNames("button_config", "button_sobre", "config", "sobre", "about");
        if (buttonCinematic == null) buttonCinematic = FindButtonByNames("button_cinematic", "cinematic", "cinema");
        if (buttonSair == null) buttonSair = FindButtonByNames("button_sair", "sair", "quit", "exit");
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
            buttonSair.onClick.RemoveListener(OnQuitButtonClicked);
            buttonSair.onClick.AddListener(OnQuitButtonClicked);
        }

        buttonCallbacksBound = true;
    }

    private void OnNewButtonClicked()
    {
        if (buttonNew != null)
            SyncCurrentIndexWithButton(buttonNew);

        PlayConfirmSfxOncePerFrame();
        OpenPanelAndHideMenu(panelNewGameRoot, "Panel_NewGame");
    }

    private void OnLoadButtonClicked()
    {
        if (loadPanelController == null)
            loadPanelController = FindLoadPanelControllerIncludingInactive();

        if (buttonLoad != null)
            SyncCurrentIndexWithButton(buttonLoad);

        PlayConfirmSfxOncePerFrame();
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
        OpenPanelAndHideMenu(panelTutorialRoot, "Panel_Tutorial");
    }

    private void OnConfigButtonClicked()
    {
        if (buttonConfig != null)
            SyncCurrentIndexWithButton(buttonConfig);

        PlayConfirmSfxOncePerFrame();
        OpenPanelAndHideMenu(panelConfigRoot, "Panel_Config");
    }

    private void OnCinematicButtonSelected()
    {
        if (buttonCinematic != null)
            SyncCurrentIndexWithButton(buttonCinematic);

        PlayConfirmSfxOncePerFrame();
    }

    private void OnQuitButtonClicked()
    {
        if (buttonSair != null)
            SyncCurrentIndexWithButton(buttonSair);

        PlayConfirmSfxOncePerFrame();
        OpenQuitConfirmation();
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

    private static bool WasUpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
    }

    private static bool WasDownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
    }

    private static bool WasLeftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
    }

    private static bool WasRightPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
    }

    private static bool WasConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private static bool IsAnyTextInputFocusedInUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null)
            {
                InputField legacyInput = selected.GetComponentInParent<InputField>();
                if (legacyInput != null && legacyInput.isFocused)
                    return true;

                TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
                if (tmpInput != null && tmpInput.isFocused)
                    return true;
            }
        }

        TMP_InputField[] tmpInputs = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < tmpInputs.Length; i++)
        {
            TMP_InputField field = tmpInputs[i];
            if (field != null && field.isActiveAndEnabled && field.isFocused)
                return true;
        }

        InputField[] legacyInputs = FindObjectsByType<InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < legacyInputs.Length; i++)
        {
            InputField field = legacyInputs[i];
            if (field != null && field.isActiveAndEnabled && field.isFocused)
                return true;
        }

        return false;
    }
}
