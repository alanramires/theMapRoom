using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuLoadPanelController : MonoBehaviour
{
    private const string UnknownMapSlotLabelFormat = "#{0} game nao encontrado";
    private const string DeleteConfirmDialogId = "dialog.load_prompt.delete_confirm";
    private const string DeleteSuccessDialogId = "dialog.load_prompt.delete_success";

    [Header("References")]
    [SerializeField] private GameObject panelMenuRoot;
    [SerializeField] private GameObject panelLoadRoot;
    [SerializeField] private PanelMenu panelMenu;
    [SerializeField] private SaveGameManager saveGameManager;
    [SerializeField] private MatchMusicAudioManager matchMusicAudioManager;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private Button backButton;

    [Header("Slots")]
    [SerializeField] private Button slotButton1;
    [SerializeField] private Button slotButton2;
    [SerializeField] private Button slotButton3;
    [SerializeField] private Button deleteButton1;
    [SerializeField] private Button deleteButton2;
    [SerializeField] private Button deleteButton3;

    private int selectedIndex;
    private bool slotButtonsBound;
    private bool isHidden = true;
    private bool loadTransitionInProgress;
    private bool deleteConfirmOpen;
    private int deleteConfirmSlot;
    private int panelOpenedFrame = -1;

    public bool IsOpen => !isHidden && panelLoadRoot != null && panelLoadRoot.activeInHierarchy;
    public bool IsDeleteConfirmationOpen => deleteConfirmOpen;

    private void Awake()
    {
        ResolveReferences();
        BindBackButtonIfNeeded();
        BindSlotButtonsIfNeeded();
        RefreshSlotButtonsInteractable();
        SetLoadPanelHidden(true);
    }

    private void OnEnable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = false;

        ResolveReferences();
        RefreshSlotButtonsInteractable();
    }

    private void OnDisable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
    }

    private void Update()
    {
        if (!IsOpen || loadTransitionInProgress)
            return;

        // Enquanto o Panel_Load estiver aberto, bloqueia atalhos de gameplay
        // (ex.: Enter vazando para CursorController e tocando SFX extra).
        UiInputBlocker.SuppressGameplayInputForFrames(1);

        if (IsAnyTextInputFocusedInUi())
            return;

        if (deleteConfirmOpen)
        {
            if (WasConfirmPressed())
            {
                ConfirmDeleteSlot();
                return;
            }

            if (WasCancelPressed())
            {
                CancelDeleteConfirmation(playCancelSfx: true);
                return;
            }

            return;
        }

        if (WasUpPressed())
        {
            MoveSelectionVertical(-1, playSfx: true);
            return;
        }

        if (WasLeftPressed())
        {
            MoveSelectionHorizontal(-1, playSfx: true);
            return;
        }

        if (WasDownPressed())
        {
            MoveSelectionVertical(+1, playSfx: true);
            return;
        }

        if (WasRightPressed())
        {
            MoveSelectionHorizontal(+1, playSfx: true);
            return;
        }

        if (WasConfirmPressed())
        {
            // Evita vazamento do Enter para o CursorController (som de gameplay no mesmo frame).
            UiInputBlocker.SuppressGameplayInputForFrames(2);
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
        CancelDeleteConfirmation(playCancelSfx: false);

        SetMenuHidden(true);
        SetLoadPanelHidden(false);
        panelOpenedFrame = Time.frameCount;

        selectedIndex = FindFirstInteractableButtonIndex();
        SelectCurrentButton(playSfx: false);
        cursorController?.PlayConfirmSfx();
    }

    public void CloseLoadPanel()
    {
        // Evita que o Enter usado no "Voltar" vaze para o menu raiz no mesmo frame.
        UiInputBlocker.SuppressGameplayInputForFrames(1);
        CancelDeleteConfirmation(playCancelSfx: false);
        RefreshSlotButtonsInteractable();

        SetLoadPanelHidden(true);
        SetMenuHidden(false);
        panelMenu?.ShowRootMenu();
        cursorController?.PlayCancelSfx();
    }

    public void LoadSlot1() => TryLoadSlot(1);
    public void LoadSlot2() => TryLoadSlot(2);
    public void LoadSlot3() => TryLoadSlot(3);
    public void DeleteSlot1() => OpenDeleteConfirmation(1);
    public void DeleteSlot2() => OpenDeleteConfirmation(2);
    public void DeleteSlot3() => OpenDeleteConfirmation(3);
    public bool NavigateVertical(int direction) => MoveSelectionVertical(direction, playSfx: false);
    public bool NavigateHorizontal(int direction) => MoveSelectionHorizontal(direction, playSfx: false);
    public void ConfirmDeleteFromKeyboard() => ConfirmDeleteSlot();
    public void CancelDeleteFromKeyboard() => CancelDeleteConfirmation(playCancelSfx: true);

    private void TryLoadSlot(int slot)
    {
        Debug.Log($"[TRACE][MainMenuLoadPanelController.TryLoadSlot] slot={slot} isOpen={IsOpen}\n{Environment.StackTrace}");

        // Protecao contra listener indevido no Button_load do menu raiz:
        // se o clique que abriu o panel_load tambem chamar TryLoadSlot no mesmo frame,
        // ignora para nao carregar direto.
        if (Time.frameCount == panelOpenedFrame)
            return;

        // Blindagem: se algum botao fora do Panel_Load estiver ligado por engano
        // em LoadSlotX no Inspector, ignora para nao disparar load direto.
        if (!IsOpen)
            return;

        Button expectedButton = slot switch
        {
            1 => slotButton1,
            2 => slotButton2,
            3 => slotButton3,
            _ => null
        };

        if (expectedButton == null || !expectedButton.gameObject.activeInHierarchy)
            return;

        if (loadTransitionInProgress || deleteConfirmOpen)
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

        if (panelMenu == null)
            panelMenu = FindAnyObjectByType<PanelMenu>();
        if (saveGameManager == null)
            saveGameManager = FindAnyObjectByType<SaveGameManager>();
        if (matchMusicAudioManager == null)
            matchMusicAudioManager = FindAnyObjectByType<MatchMusicAudioManager>();
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();

        if (deleteButton1 == null)
            deleteButton1 = FindButtonByName("button_del1");
        if (deleteButton2 == null)
            deleteButton2 = FindButtonByName("button_del2");
        if (deleteButton3 == null)
            deleteButton3 = FindButtonByName("button_del3");
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
        if (deleteButton1 != null) deleteButton1.onClick.AddListener(() => OpenDeleteConfirmation(1));
        if (deleteButton2 != null) deleteButton2.onClick.AddListener(() => OpenDeleteConfirmation(2));
        if (deleteButton3 != null) deleteButton3.onClick.AddListener(() => OpenDeleteConfirmation(3));

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

    private bool MoveSelectionVertical(int direction, bool playSfx)
    {
        if (direction == 0)
            return false;

        Button current = GetCurrentSelectedButton();
        if (!TryGetGridPosition(current, out int row, out int col))
        {
            selectedIndex = FindFirstInteractableButtonIndex();
            SelectCurrentButton(playSfx);
            return true;
        }

        const int totalRows = 4;
        int nextRow = row;
        for (int i = 0; i < totalRows; i++)
        {
            nextRow += direction > 0 ? 1 : -1;
            while (nextRow < 0)
                nextRow += totalRows;
            while (nextRow >= totalRows)
                nextRow -= totalRows;

            Button candidate = ResolveGridButton(nextRow, col);
            if (candidate == null || !candidate.interactable)
            {
                candidate = ResolveGridButton(nextRow, col == 0 ? 1 : 0);
            }

            if (candidate != null && candidate.interactable)
            {
                SelectButton(candidate, playSfx);
                return true;
            }
        }

        return false;
    }

    private bool MoveSelectionHorizontal(int direction, bool playSfx)
    {
        if (direction == 0)
            return false;

        Button current = GetCurrentSelectedButton();
        if (!TryGetGridPosition(current, out int row, out int col))
        {
            selectedIndex = FindFirstInteractableButtonIndex();
            SelectCurrentButton(playSfx);
            return true;
        }

        if (row >= 3)
        {
            Button back = ResolveGridButton(3, 0);
            if (back != null && back.interactable)
            {
                SelectButton(back, playSfx);
                return true;
            }

            return false;
        }

        int nextCol = direction > 0 ? 1 : 0;
        if (nextCol == col)
            return false;

        Button candidate = ResolveGridButton(row, nextCol);
        if (candidate == null || !candidate.interactable)
        {
            Button fallback = FindHorizontalNeighborFromSelectable(current, direction);
            if (fallback != null && fallback.interactable)
            {
                SelectButton(fallback, playSfx);
                return true;
            }

            return false;
        }

        SelectButton(candidate, playSfx);
        return true;
    }

    private void SelectCurrentButton(bool playSfx)
    {
        var buttons = GetAvailableSlotButtons();
        if (buttons.Count <= 0)
            return;

        Button b = buttons[Mathf.Clamp(selectedIndex, 0, buttons.Count - 1)];
        if (b == null)
            return;

        SelectButton(b, playSfx);
    }

    private void SelectButton(Button button, bool playSfx)
    {
        if (button == null)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);

        List<Button> buttons = GetAvailableSlotButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == button)
            {
                selectedIndex = i;
                break;
            }
        }

        if (playSfx)
            cursorController?.PlayCursorMoveSfx();
    }

    private Button GetCurrentSelectedButton()
    {
        List<Button> buttons = GetAvailableSlotButtons();
        if (buttons.Count <= 0)
            return null;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            for (int i = 0; i < buttons.Count; i++)
            {
                Button b = buttons[i];
                if (b != null && b.gameObject == selected)
                {
                    selectedIndex = i;
                    return b;
                }
            }
        }

        return buttons[Mathf.Clamp(selectedIndex, 0, buttons.Count - 1)];
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
        if (deleteButton1 != null)
            deleteButton1.interactable = hasSave1;
        if (deleteButton2 != null)
            deleteButton2.interactable = hasSave2;
        if (deleteButton3 != null)
            deleteButton3.interactable = hasSave3;

        RefreshSlotButtonLabels(hasSave1, hasSave2, hasSave3);
    }

    private void RefreshSlotButtonLabels(bool hasSave1, bool hasSave2, bool hasSave3)
    {
        UpdateSlotButtonLabel(slotButton1, 1, hasSave1);
        UpdateSlotButtonLabel(slotButton2, 2, hasSave2);
        UpdateSlotButtonLabel(slotButton3, 3, hasSave3);
    }

    private void UpdateSlotButtonLabel(Button button, int slotIndex, bool hasSave)
    {
        if (button == null)
            return;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        if (label == null)
            return;

        if (!hasSave || saveGameManager == null)
        {
            label.text = $"#{slotIndex} Jogo";
            return;
        }

        if (saveGameManager.TryGetSlotSceneName(slotIndex, out string sceneName))
        {
            label.text = $"#{slotIndex} {sceneName}";
            return;
        }

        label.text = string.Format(UnknownMapSlotLabelFormat, slotIndex);
    }

    private bool TryGetGridPosition(Button button, out int row, out int col)
    {
        row = -1;
        col = -1;
        if (button == null)
            return false;

        if (button == slotButton1) { row = 0; col = 0; return true; }
        if (button == deleteButton1) { row = 0; col = 1; return true; }
        if (button == slotButton2) { row = 1; col = 0; return true; }
        if (button == deleteButton2) { row = 1; col = 1; return true; }
        if (button == slotButton3) { row = 2; col = 0; return true; }
        if (button == deleteButton3) { row = 2; col = 1; return true; }
        if (button == backButton) { row = 3; col = 0; return true; }

        return false;
    }

    private Button ResolveGridButton(int row, int col)
    {
        switch (row)
        {
            case 0: return col == 0 ? slotButton1 : deleteButton1;
            case 1: return col == 0 ? slotButton2 : deleteButton2;
            case 2: return col == 0 ? slotButton3 : deleteButton3;
            case 3: return backButton;
            default: return null;
        }
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
        CancelDeleteConfirmation(playCancelSfx: false);
        cursorController?.PlayConfirmSfx();

        SetMenuHidden(true);

        if (matchMusicAudioManager != null)
            matchMusicAudioManager.SetRuntimeVolumeOverride(matchMusicAudioManager.MenuLoadTransitionMusicVolume);

        bool started = saveGameManager != null && saveGameManager.BeginLoadFromMainMenuSlot(slot);
        if (!started)
        {
            if (matchMusicAudioManager != null)
                matchMusicAudioManager.ClearRuntimeVolumeOverride();
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
        List<Button> list = new List<Button>(7);
        if (slotButton1 != null) list.Add(slotButton1);
        if (slotButton2 != null) list.Add(slotButton2);
        if (slotButton3 != null) list.Add(slotButton3);
        if (deleteButton1 != null) list.Add(deleteButton1);
        if (deleteButton2 != null) list.Add(deleteButton2);
        if (deleteButton3 != null) list.Add(deleteButton3);
        if (backButton != null) list.Add(backButton);
        return list;
    }

    private void OpenDeleteConfirmation(int slot)
    {
        // Blindagem: evita abrir confirmacao de delete fora do Panel_Load.
        if (!IsOpen)
            return;

        if (loadTransitionInProgress)
            return;

        if (deleteConfirmOpen)
            return;

        if (saveGameManager == null)
            saveGameManager = FindAnyObjectByType<SaveGameManager>();

        if (saveGameManager == null)
        {
            cursorController?.PlayErrorSfx();
            return;
        }

        bool hasSave = saveGameManager.HasSaveInSlot(slot);
        if (!hasSave)
        {
            cursorController?.PlayErrorSfx();
            return;
        }

        string sceneName = saveGameManager.TryGetSlotSceneName(slot, out string scene) ? scene : "game nao encontrado";
        string fallback = $"#{slot} {sceneName}\nDeletar este save?\nENTER: confirmar | ESC: voltar";
        string text = PanelDialogController.ResolveDialogMessage(
            DeleteConfirmDialogId,
            fallback,
            new Dictionary<string, string>
            {
                { "slot", slot.ToString() },
                { "scene", sceneName }
            });

        deleteConfirmOpen = true;
        deleteConfirmSlot = slot;
        // Enter/click de UI nao deve disparar feedback paralelo de gameplay.
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        PanelDialogController.TrySetExternalText(text);
        cursorController?.PlayBeepSfx();
    }

    private void ConfirmDeleteSlot()
    {
        if (!deleteConfirmOpen)
            return;

        if (saveGameManager == null)
            saveGameManager = FindAnyObjectByType<SaveGameManager>();

        int slot = deleteConfirmSlot;
        deleteConfirmOpen = false;
        deleteConfirmSlot = 0;
        PanelDialogController.ClearExternalText();

        if (saveGameManager == null)
        {
            cursorController?.PlayErrorSfx();
            return;
        }

        saveGameManager.ClearSlot(slot);
        RefreshSlotButtonsInteractable();
        selectedIndex = FindFirstInteractableButtonIndex();
        SelectCurrentButton(playSfx: false);

        string fallback = $"Slot #{slot} apagado.";
        string deletedText = PanelDialogController.ResolveDialogMessage(
            DeleteSuccessDialogId,
            fallback,
            new Dictionary<string, string> { { "slot", slot.ToString() } });
        PanelDialogController.TrySetTransientText(deletedText, 1.8f);
        cursorController?.PlayConfirmSfx();
    }

    private void CancelDeleteConfirmation(bool playCancelSfx)
    {
        if (!deleteConfirmOpen)
            return;

        deleteConfirmOpen = false;
        deleteConfirmSlot = 0;
        PanelDialogController.ClearExternalText();
        if (playCancelSfx)
            cursorController?.PlayCancelSfx();
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

    private Button FindButtonByName(string name)
    {
        Transform target = FindTransformByName(name);
        if (target == null)
            return null;
        return target.GetComponent<Button>();
    }

    private static Button FindHorizontalNeighborFromSelectable(Button current, int direction)
    {
        if (current == null)
            return null;

        Selectable next = direction > 0 ? current.FindSelectableOnRight() : current.FindSelectableOnLeft();
        int guard = 0;
        while (next != null && guard < 16)
        {
            guard++;
            if (next is Button button)
                return button;

            next = direction > 0 ? next.FindSelectableOnRight() : next.FindSelectableOnLeft();
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

