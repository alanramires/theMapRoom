using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuLoadPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelMenuRoot;
    [SerializeField] private GameObject panelLoadRoot;
    [SerializeField] private MainMenuKeyboardController menuKeyboardController;
    [SerializeField] private SaveGameManager saveGameManager;
    [SerializeField] private MatchMusicAudioManager matchMusicAudioManager;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private Button backButton;

    [Header("Slots")]
    [SerializeField] private Button slotButton1;
    [SerializeField] private Button slotButton2;
    [SerializeField] private Button slotButton3;
    [Header("Transition")]
    [SerializeField] [Range(0f, 2f)] private float menuMusicFadeOutSeconds = 0.3f;

    private int selectedIndex;
    private bool slotButtonsBound;
    private bool isHidden = true;
    private bool loadTransitionInProgress;

    public bool IsOpen => !isHidden && panelLoadRoot != null && panelLoadRoot.activeInHierarchy;

    private void Awake()
    {
        ResolveReferences();
        BindBackButtonIfNeeded();
        BindSlotButtonsIfNeeded();
        SetLoadPanelHidden(true);
    }

    private void Update()
    {
        if (!IsOpen || loadTransitionInProgress)
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
            ConfirmSelection();
            return;
        }

        if (WasCancelPressed())
            CloseLoadPanel();
    }

    public void OpenLoadPanel()
    {
        ResolveReferences();
        RefreshSlotButtonsInteractable();

        SetMenuHidden(true);
        SetLoadPanelHidden(false);

        selectedIndex = FindFirstInteractableButtonIndex();
        SelectCurrentButton(playSfx: false);
        cursorController?.PlayConfirmSfx();
    }

    public void CloseLoadPanel()
    {
        // Evita que o Enter usado no "Voltar" vaze para o menu raiz no mesmo frame.
        UiInputBlocker.SuppressGameplayInputForFrames(1);

        SetLoadPanelHidden(true);
        SetMenuHidden(false);
        menuKeyboardController?.ShowRootMenu();
        cursorController?.PlayCancelSfx();
    }

    public void LoadSlot1() => TryLoadSlot(1);
    public void LoadSlot2() => TryLoadSlot(2);
    public void LoadSlot3() => TryLoadSlot(3);

    private void TryLoadSlot(int slot)
    {
        if (loadTransitionInProgress)
            return;

        if (saveGameManager == null)
            saveGameManager = FindAnyObjectByType<SaveGameManager>();
        if (matchMusicAudioManager == null)
            matchMusicAudioManager = FindAnyObjectByType<MatchMusicAudioManager>();

        if (saveGameManager == null)
        {
            cursorController?.PlayErrorSfx();
            Debug.LogWarning("[MainMenuLoad] SaveGameManager nao encontrado.");
            return;
        }

        StartCoroutine(BeginLoadFlow(slot));
    }

    private void ResolveReferences()
    {
        if (panelMenuRoot == null)
        {
            Transform menu = FindTransformByName("Panel_Menu");
            if (menu != null)
                panelMenuRoot = menu.gameObject;
        }

        if (panelLoadRoot == null)
        {
            Transform load = FindTransformByName("Panel_Load");
            if (load != null)
                panelLoadRoot = load.gameObject;
            else
                panelLoadRoot = gameObject;
        }

        if (menuKeyboardController == null)
            menuKeyboardController = FindAnyObjectByType<MainMenuKeyboardController>();
        if (saveGameManager == null)
            saveGameManager = FindAnyObjectByType<SaveGameManager>();
        if (matchMusicAudioManager == null)
            matchMusicAudioManager = FindAnyObjectByType<MatchMusicAudioManager>();
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
    }

    private void BindBackButtonIfNeeded()
    {
        if (backButton == null)
            return;

        backButton.onClick.RemoveListener(CloseLoadPanel);
        backButton.onClick.AddListener(CloseLoadPanel);
    }

    private void BindSlotButtonsIfNeeded()
    {
        if (slotButtonsBound)
            return;

        if (slotButton1 != null) slotButton1.onClick.AddListener(() => TryLoadSlot(1));
        if (slotButton2 != null) slotButton2.onClick.AddListener(() => TryLoadSlot(2));
        if (slotButton3 != null) slotButton3.onClick.AddListener(() => TryLoadSlot(3));

        slotButtonsBound = true;
    }

    private void MoveSelection(int delta)
    {
        var buttons = GetAvailableSlotButtons();
        if (buttons.Count <= 0)
            return;
        if (delta == 0)
            return;

        int count = buttons.Count;
        int direction = delta > 0 ? 1 : -1;
        int startIndex = Mathf.Clamp(selectedIndex, 0, count - 1);
        int nextIndex = startIndex;

        for (int i = 0; i < count; i++)
        {
            nextIndex += direction;
            while (nextIndex < 0)
                nextIndex += count;
            while (nextIndex >= count)
                nextIndex -= count;

            Button candidate = buttons[nextIndex];
            if (candidate != null && candidate.interactable)
            {
                selectedIndex = nextIndex;
                SelectCurrentButton(playSfx: true);
                return;
            }
        }
    }

    private void SelectCurrentButton(bool playSfx)
    {
        var buttons = GetAvailableSlotButtons();
        if (buttons.Count <= 0)
            return;

        Button b = buttons[Mathf.Clamp(selectedIndex, 0, buttons.Count - 1)];
        if (b == null)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(b.gameObject);

        if (playSfx)
            cursorController?.PlayBeepSfx();
    }

    private void ConfirmSelection()
    {
        var buttons = GetAvailableSlotButtons();
        if (buttons.Count <= 0)
            return;

        Button b = buttons[Mathf.Clamp(selectedIndex, 0, buttons.Count - 1)];
        if (b == null || !b.interactable)
            return;

        b.onClick?.Invoke();
    }

    private void RefreshSlotButtonsInteractable()
    {
        if (saveGameManager == null)
            saveGameManager = FindAnyObjectByType<SaveGameManager>();

        bool hasSave1 = saveGameManager != null && saveGameManager.HasSaveInSlot(1);
        bool hasSave2 = saveGameManager != null && saveGameManager.HasSaveInSlot(2);
        bool hasSave3 = saveGameManager != null && saveGameManager.HasSaveInSlot(3);

        if (slotButton1 != null)
            slotButton1.interactable = hasSave1;
        if (slotButton2 != null)
            slotButton2.interactable = hasSave2;
        if (slotButton3 != null)
            slotButton3.interactable = hasSave3;
    }

    private int FindFirstInteractableButtonIndex()
    {
        var buttons = GetAvailableSlotButtons();
        if (buttons.Count <= 0)
            return 0;

        for (int i = 0; i < buttons.Count; i++)
        {
            Button b = buttons[i];
            if (b != null && b.interactable)
                return i;
        }

        return 0;
    }

    private System.Collections.IEnumerator BeginLoadFlow(int slot)
    {
        loadTransitionInProgress = true;
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        cursorController?.PlayConfirmSfx();

        SetMenuHidden(true);

        if (matchMusicAudioManager != null)
            yield return matchMusicAudioManager.FadeOutAndStop(menuMusicFadeOutSeconds);

        bool started = saveGameManager != null && saveGameManager.BeginLoadFromMainMenuSlot(slot);
        if (!started)
        {
            cursorController?.PlayErrorSfx();
            SetMenuHidden(false);
            SelectCurrentButton(playSfx: false);
            loadTransitionInProgress = false;
            yield break;
        }

        SetLoadPanelHidden(true);
        loadTransitionInProgress = false;
    }

    private List<Button> GetAvailableSlotButtons()
    {
        List<Button> list = new List<Button>(3);
        if (slotButton1 != null) list.Add(slotButton1);
        if (slotButton2 != null) list.Add(slotButton2);
        if (slotButton3 != null) list.Add(slotButton3);
        if (backButton != null) list.Add(backButton);
        return list;
    }

    private void SetMenuHidden(bool hidden)
    {
        if (panelMenuRoot == null)
            return;

        panelMenuRoot.SetActive(!hidden);
    }

    private void SetLoadPanelHidden(bool hidden)
    {
        isHidden = hidden;
        if (panelLoadRoot == null)
            return;

        panelLoadRoot.SetActive(!hidden);
    }

    private static Transform FindTransformByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
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
        if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindInChildrenRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static bool WasUpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
    }

    private static bool WasDownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
    }

    private static bool WasConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
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
