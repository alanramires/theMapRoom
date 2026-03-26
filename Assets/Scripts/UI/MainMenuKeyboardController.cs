using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuKeyboardController : MonoBehaviour
{
    [Header("Menu Buttons")]
    [SerializeField] private Button buttonNew;
    [SerializeField] private Button buttonLoad;
    [SerializeField] private Button buttonTutorial;
    [SerializeField] private Button buttonSobre;
    [SerializeField] private Button buttonCinematic;
    [SerializeField] private int defaultButtonIndex = 0;
    [SerializeField] private bool wrapSelection = true;

    [Header("Audio")]
    [SerializeField] private CursorController cursorController;
    [SerializeField] private MainMenuCinematicController cinematicController;
    [SerializeField] private MainMenuLoadPanelController loadPanelController;

    private int currentIndex;
    private bool buttonCallbacksBound;
    private bool loadButtonBoundToLoadPanel;
    private int lastConfirmSfxFrame = -1;

    private void Awake()
    {
        ResolveMenuButtonsIfNeeded();

        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (cinematicController == null)
            cinematicController = GetComponent<MainMenuCinematicController>();
        if (loadPanelController == null)
            loadPanelController = FindLoadPanelControllerIncludingInactive();

        BindButtonCallbacksIfNeeded();
        BindLoadButtonFallbackIfNeeded();
        ClampCurrentIndex();
    }

    private void OnEnable()
    {
        ShowRootMenu();
    }

    private void Update()
    {
        if (!loadButtonBoundToLoadPanel)
        {
            if (loadPanelController == null)
                loadPanelController = FindLoadPanelControllerIncludingInactive();
            BindLoadButtonFallbackIfNeeded();
        }

        if (cinematicController != null && cinematicController.IsPlaying)
            return;
        if (loadPanelController != null && loadPanelController.IsOpen)
            return;

        if (UiInputBlocker.IsTextInputFocused())
            return;

        if (WasUpPressed())
        {
            MoveSelection(-1);
            return;
        }

        if (WasDownPressed())
        {
            MoveSelection(+1);
            return;
        }

        if (WasConfirmPressed())
        {
            ConfirmCurrentSelection();
            return;
        }

        if (WasCancelPressed())
            HandleCancel();
    }

    public void ShowRootMenu()
    {
        ClampCurrentIndex();
        SelectCurrentButton(playSfx: false);
    }

    private void MoveSelection(int delta)
    {
        List<Button> buttons = GetRootButtons();
        if (buttons.Count <= 0)
            return;

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
            return;

        SelectCurrentButton(playSfx: true);
    }

    private void ConfirmCurrentSelection()
    {
        Button button = GetCurrentButton();
        if (button == null || !button.interactable)
            return;

        button.onClick?.Invoke();
    }

    private void HandleCancel()
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
            cursorController?.PlayBeepSfx();
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
        List<Button> list = new List<Button>(5);
        if (buttonNew != null) list.Add(buttonNew);
        if (buttonLoad != null) list.Add(buttonLoad);
        if (buttonTutorial != null) list.Add(buttonTutorial);
        if (buttonSobre != null) list.Add(buttonSobre);
        if (buttonCinematic != null) list.Add(buttonCinematic);
        return list;
    }

    private void ResolveMenuButtonsIfNeeded()
    {
        if (buttonNew == null) buttonNew = FindButtonByNames("button_new", "novo", "new");
        if (buttonLoad == null) buttonLoad = FindButtonByNames("button_load", "carregar", "load");
        if (buttonTutorial == null) buttonTutorial = FindButtonByNames("button_tutorial", "tutorial");
        if (buttonSobre == null) buttonSobre = FindButtonByNames("button_sobre", "sobre", "about");
        if (buttonCinematic == null) buttonCinematic = FindButtonByNames("button_cinematic", "cinematic", "cinema");
    }

    private Button FindButtonByNames(params string[] keywords)
    {
        Button[] buttons = GetComponentsInChildren<Button>(includeInactive: true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null || b.name == null)
                continue;

            string n = b.name.ToLowerInvariant();
            for (int k = 0; k < keywords.Length; k++)
            {
                string key = keywords[k];
                if (!string.IsNullOrWhiteSpace(key) && n.Contains(key))
                    return b;
            }
        }

        return null;
    }

    private void BindButtonCallbacksIfNeeded()
    {
        if (buttonCallbacksBound)
            return;

        List<Button> buttons = GetRootButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            int capturedIndex = i;
            button.onClick.AddListener(() => HandleButtonInvoked(capturedIndex));
        }

        buttonCallbacksBound = true;
    }

    private void BindLoadButtonFallbackIfNeeded()
    {
        if (loadButtonBoundToLoadPanel || buttonLoad == null || loadPanelController == null)
            return;

        buttonLoad.onClick.AddListener(loadPanelController.OpenLoadPanel);
        loadButtonBoundToLoadPanel = true;
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

    private void PlayConfirmSfxOncePerFrame()
    {
        if (lastConfirmSfxFrame == Time.frameCount)
            return;

        lastConfirmSfxFrame = Time.frameCount;
        cursorController?.PlayConfirmSfx();
    }

    private static bool WasUpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
    }

    private static bool WasDownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
    }

    private static bool WasConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }
}
