using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class KeyboardManager : MonoBehaviour
{
    private static KeyboardManager activeInstance;

    [Header("References")]
    [SerializeField] private CursorController cursorController;
    [SerializeField] private PanelMenu panelMenu;
    [SerializeField] private MainMenuCinematicController cinematicController;
    [SerializeField] private MainMenuLoadPanelController loadPanelController;

    private Vector2 previousUiMove;
    private bool previousUiSubmitPressed;
    private bool previousUiCancelPressed;

    public static KeyboardManager EnsureSceneInstance()
    {
        Scene active = SceneManager.GetActiveScene();
        KeyboardManager[] all = FindObjectsByType<KeyboardManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            KeyboardManager km = all[i];
            if (km == null)
                continue;
            if (km.gameObject.scene != active)
                continue;
            if (IsUnderMenuPanel(km.transform))
                continue;
            return km;
        }

        GameObject managers = FindManagersRoot(active);
        if (managers == null)
            managers = new GameObject("Managers");

        GameObject host = new GameObject("Keyboard Manager");
        host.transform.SetParent(managers.transform, worldPositionStays: false);
        return host.AddComponent<KeyboardManager>();
    }

    private void Awake()
    {
        // Arquitetura antiga (gerenciador global) desativada.
        // O controle de teclado agora e feito por painel.
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;

        enabled = false;
    }

    private void OnEnable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
    }

    private void OnDisable()
    {
        if (activeInstance == this)
            activeInstance = null;

        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
    }

    private void Update()
    {
        // Intencionalmente vazio.
    }

    private void ResolveReferences()
    {
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (panelMenu == null)
            panelMenu = FindAnyObjectByType<PanelMenu>();
        if (cinematicController == null)
            cinematicController = FindAnyObjectByType<MainMenuCinematicController>();
        if (loadPanelController == null)
            loadPanelController = FindLoadPanelControllerIncludingInactive();
    }

    private void EnsureControllerHostOutsidePanels()
    {
        if (!IsUnderMenuPanel(transform))
            return;

        KeyboardManager ensured = EnsureSceneInstance();
        if (ensured != null && ensured != this)
            Destroy(this);
    }

    private static bool IsUnderMenuPanel(Transform t)
    {
        Transform cur = t;
        while (cur != null)
        {
            string n = cur.name;
            if (n.Equals("Panel_Menu", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Panel_Load", System.StringComparison.OrdinalIgnoreCase))
                return true;
            cur = cur.parent;
        }

        return false;
    }

    private static GameObject FindManagersRoot(Scene active)
    {
        GameObject[] roots = active.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && root.name.Equals("Managers", System.StringComparison.OrdinalIgnoreCase))
                return root;
        }

        return null;
    }

    private enum NavDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    private bool TryMoveSelectionByDirection(NavDirection direction, bool loadPanelOpen)
    {
        if (EventSystem.current == null)
            return false;

        // No Panel_Load, usa somente a navegacao explicita da grade
        // para evitar pular itens por conta do auto-navigation do Unity.
        if (loadPanelOpen && loadPanelController != null)
        {
            return direction switch
            {
                NavDirection.Up => loadPanelController.NavigateVertical(-1),
                NavDirection.Down => loadPanelController.NavigateVertical(+1),
                NavDirection.Left => loadPanelController.NavigateHorizontal(-1),
                NavDirection.Right => loadPanelController.NavigateHorizontal(+1),
                _ => false
            };
        }

        Selectable current = ResolveCurrentSelectable();
        Selectable next = FindNeighbor(current, direction);

        if (next != null && next.IsInteractable())
        {
            EventSystem.current.SetSelectedGameObject(next.gameObject);
            return true;
        }

        return false;
    }

    private static Selectable ResolveCurrentSelectable()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            return null;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        Selectable selectable = selected.GetComponent<Selectable>();
        if (selectable != null)
            return selectable;

        return selected.GetComponentInParent<Selectable>();
    }

    private static Selectable FindNeighbor(Selectable current, NavDirection direction)
    {
        if (current == null)
            return null;

        return direction switch
        {
            NavDirection.Up => current.FindSelectableOnUp(),
            NavDirection.Down => current.FindSelectableOnDown(),
            NavDirection.Left => current.FindSelectableOnLeft(),
            NavDirection.Right => current.FindSelectableOnRight(),
            _ => null
        };
    }

    private static void ConfirmSelectedButtonFromEventSystem()
    {
        if (EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return;

        Button button = selected.GetComponent<Button>();
        if (button == null || !button.interactable)
            return;

        button.onClick?.Invoke();
    }

    private void ConfirmRootMenuSelection()
    {
        if (panelMenu != null)
        {
            panelMenu.ConfirmCurrentSelection();
            return;
        }

        ConfirmSelectedButtonFromEventSystem();
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
