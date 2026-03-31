using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

[DefaultExecutionOrder(-350)]
public class MainMenuStateController : MonoBehaviour
{
    private static MainMenuStateController activeInstance;

    [Header("State")]
    [SerializeField] private MainMenuState initialState = MainMenuState.Neutral;
    [SerializeField] private int neutralDelayFrames = 2;
    [SerializeField] private bool stateLog = false;

    [Header("Panels")]
    [SerializeField] private GameObject panelMenuRoot;
    [SerializeField] private CanvasGroup panelMenuCanvasGroup;
    [SerializeField] private GameObject panelNewGameRoot;
    [SerializeField] private GameObject panelLoadRoot;
    [SerializeField] private GameObject panelTutorialRoot;
    [SerializeField] private GameObject panelConfigRoot;
    [SerializeField] private RectTransform sharedMenuContainer;

    [Header("References")]
    [SerializeField] private PanelMenu panelMenu;
    [SerializeField] private MainMenuLoadPanelController loadPanelController;
    [SerializeField] private MainMenuCinematicController cinematicController;

    private MainMenuState currentState;
    private int enteredNeutralFrame = -1;
    private int previousRootMenuIndex = -1;
    private int ignoreInputUntilFrame = -1;
    private Vector2 previousUiMove;
    private bool previousUiSubmitPressed;
    private bool previousUiCancelPressed;

    public MainMenuState CurrentState => currentState;
    public bool IsRootMenuInteractiveState => currentState == MainMenuState.RootMenu || currentState == MainMenuState.Exit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        MainMenuStateController controller = EnsureSceneInstance();
        if (controller != null && controller.stateLog)
            Debug.Log($"[MainMenuState] Bootstrap scene '{SceneManager.GetActiveScene().name}'");
    }

    public static MainMenuStateController EnsureSceneInstance()
    {
        Scene active = SceneManager.GetActiveScene();

        MainMenuStateController[] all = FindObjectsByType<MainMenuStateController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            MainMenuStateController controller = all[i];
            if (controller == null)
                continue;
            if (controller.gameObject.scene != active)
                continue;
            return controller;
        }

        GameObject managers = FindManagersRoot(active);
        if (managers == null)
            managers = new GameObject("Managers");

        GameObject host = new GameObject("Main Menu State Controller");
        host.transform.SetParent(managers.transform, worldPositionStays: false);
        return host.AddComponent<MainMenuStateController>();
    }

    private void Awake()
    {
        if (stateLog) Debug.Log($"[MainMenuState] Awake on '{gameObject.name}'");

        if (activeInstance != null && activeInstance != this)
        {
            Destroy(this);
            return;
        }

        activeInstance = this;
        ResolveReferences();
        ResetUiInputTracking();
        ChangeState(initialState);
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    private void Update()
    {
        ResolveReferences();
        RouteInputForCurrentState();
    }

    public void RequestState(MainMenuState nextState)
    {
        if (nextState == currentState)
            return;

        ChangeState(nextState);
    }

    private void ChangeState(MainMenuState nextState)
    {
        if (stateLog) Debug.Log($"[MainMenuState] {currentState} -> {nextState}");
        ExitCurrentState();
        currentState = nextState;
        EnterCurrentState();
    }

    private void ExitCurrentState()
    {
        switch (currentState)
        {
            case MainMenuState.RootMenu:
            case MainMenuState.Exit:
                previousRootMenuIndex = panelMenu != null ? panelMenu.CurrentIndex : previousRootMenuIndex;
                panelMenu?.ExitRootMenu();
                panelMenu?.CloseExitConfirmationWithoutSfx();
                break;

            case MainMenuState.LoadMenu:
                loadPanelController?.ExitLoadMenu(stateDriven: true);
                break;

            case MainMenuState.Cinematic:
                cinematicController?.ExitToNeutral();
                break;
        }
    }

    private void EnterCurrentState()
    {
        ignoreInputUntilFrame = Time.frameCount + 1;
        ResetUiInputTrackingFromCurrentInput();

        switch (currentState)
        {
            case MainMenuState.Neutral:
                EnterNeutral();
                break;

            case MainMenuState.RootMenu:
                EnterRootMenu(resetToDefault: previousRootMenuIndex < 0);
                break;

            case MainMenuState.NewGame:
                EnterSimplePanelState(panelNewGameRoot);
                break;

            case MainMenuState.LoadMenu:
                EnterLoadMenu();
                break;

            case MainMenuState.Tutorial:
                EnterSimplePanelState(panelTutorialRoot);
                break;

            case MainMenuState.Config:
                EnterSimplePanelState(panelConfigRoot);
                break;

            case MainMenuState.Cinematic:
                EnterCinematic();
                break;

            case MainMenuState.Exit:
                EnterRootMenu(resetToDefault: false);
                panelMenu?.RequestExitConfirmation();
                break;
        }
    }

    private void EnterNeutral()
    {
        EnsureSharedMenuContainerVisible();
        enteredNeutralFrame = Time.frameCount;
        SetPanelMenuVisible(false);
        SetPanelActive(panelNewGameRoot, false);
        SetPanelActive(panelLoadRoot, false);
        SetPanelActive(panelTutorialRoot, false);
        SetPanelActive(panelConfigRoot, false);

        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = false;
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void EnterRootMenu(bool resetToDefault)
    {
        EnsureSharedMenuContainerVisible();
        SetPanelActive(panelNewGameRoot, false);
        SetPanelActive(panelLoadRoot, false);
        SetPanelActive(panelTutorialRoot, false);
        SetPanelActive(panelConfigRoot, false);
        SetPanelMenuVisible(true);

        if (!resetToDefault && previousRootMenuIndex >= 0)
            panelMenu?.SetCurrentIndex(previousRootMenuIndex);

        panelMenu?.EnterRootMenu(resetToDefault);
        previousRootMenuIndex = panelMenu != null ? panelMenu.CurrentIndex : previousRootMenuIndex;
    }

    private void EnterSimplePanelState(GameObject targetPanel)
    {
        EnsureSharedMenuContainerVisible();
        SetPanelMenuVisible(false);
        SetPanelActive(panelLoadRoot, false);
        SetPanelActive(panelNewGameRoot, targetPanel == panelNewGameRoot);
        SetPanelActive(panelTutorialRoot, targetPanel == panelTutorialRoot);
        SetPanelActive(panelConfigRoot, targetPanel == panelConfigRoot);

        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
    }

    private void EnterLoadMenu()
    {
        EnsureSharedMenuContainerVisible();
        SetPanelMenuVisible(false);
        SetPanelActive(panelNewGameRoot, false);
        SetPanelActive(panelTutorialRoot, false);
        SetPanelActive(panelConfigRoot, false);
        SetPanelActive(panelLoadRoot, true);

        if (loadPanelController != null)
            loadPanelController.EnterLoadMenu();
    }

    private void EnterCinematic()
    {
        EnsureSharedMenuContainerVisible();
        SetPanelMenuVisible(false);
        SetPanelActive(panelNewGameRoot, false);
        SetPanelActive(panelLoadRoot, false);
        SetPanelActive(panelTutorialRoot, false);
        SetPanelActive(panelConfigRoot, false);

        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = false;
            EventSystem.current.SetSelectedGameObject(null);
        }

        cinematicController?.EnterCinematic();
    }

    private void RouteInputForCurrentState()
    {
        switch (currentState)
        {
            case MainMenuState.Neutral:
                if (CanAdvanceFromNeutral())
                    ChangeState(MainMenuState.RootMenu);
                break;

            case MainMenuState.RootMenu:
                RouteRootMenuInput(allowExitConfirmation: false);
                break;

            case MainMenuState.Exit:
                RouteRootMenuInput(allowExitConfirmation: true);
                break;

            case MainMenuState.LoadMenu:
                RouteLoadMenuInput();
                break;

            case MainMenuState.Cinematic:
                if (cinematicController != null && !cinematicController.IsPlaying)
                {
                    ChangeState(MainMenuState.Neutral);
                    return;
                }

                if (WasCancelPressed())
                    ChangeState(MainMenuState.Neutral);
                break;
        }
    }

    private void RouteRootMenuInput(bool allowExitConfirmation)
    {
        if (panelMenu == null)
            return;

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

        if (allowExitConfirmation && panelMenu.IsQuitConfirmationOpen)
        {
            if (confirmPressed)
            {
                panelMenu.ConfirmQuitGameFromState();
                return;
            }

            if (cancelPressed)
            {
                panelMenu.CancelQuitGameFromState();
                ChangeState(MainMenuState.Neutral);
            }

            return;
        }

        if (upPressed || leftPressed)
        {
            panelMenu.Navigate(-1);
            previousRootMenuIndex = panelMenu.CurrentIndex;
            return;
        }

        if (downPressed || rightPressed)
        {
            panelMenu.Navigate(+1);
            previousRootMenuIndex = panelMenu.CurrentIndex;
            return;
        }

        if (confirmPressed)
        {
            panelMenu.ConfirmCurrentSelection();
            previousRootMenuIndex = panelMenu.CurrentIndex;
            return;
        }

        if (cancelPressed)
        {
            panelMenu.CancelToDefault();
            previousRootMenuIndex = panelMenu.CurrentIndex;
        }
    }

    private void RouteLoadMenuInput()
    {
        if (loadPanelController == null || !loadPanelController.IsOpen)
            return;

        UiInputBlocker.SuppressGameplayInputForFrames(1);
        if (Time.frameCount <= ignoreInputUntilFrame)
            return;
        if (IsAnyTextInputFocusedInUi())
            return;

        if (loadPanelController.IsDeleteConfirmationOpen)
        {
            if (WasConfirmPressed())
            {
                loadPanelController.ConfirmDeleteFromKeyboard();
                return;
            }

            if (WasCancelPressed())
            {
                loadPanelController.CancelDeleteFromKeyboard();
                return;
            }

            return;
        }

        if (WasUpPressed())
        {
            loadPanelController.NavigateVertical(-1);
            return;
        }

        if (WasLeftPressed())
        {
            loadPanelController.NavigateHorizontal(-1);
            return;
        }

        if (WasDownPressed())
        {
            loadPanelController.NavigateVertical(+1);
            return;
        }

        if (WasRightPressed())
        {
            loadPanelController.NavigateHorizontal(+1);
            return;
        }

        if (WasConfirmPressed())
        {
            UiInputBlocker.SuppressGameplayInputForFrames(2);
            loadPanelController.ConfirmCurrentSelection();
            return;
        }

        if (WasCancelPressed())
            ChangeState(MainMenuState.RootMenu);
    }

    private bool CanAdvanceFromNeutral()
    {
        if (enteredNeutralFrame < 0)
            return false;

        if (Time.frameCount < enteredNeutralFrame + Mathf.Max(1, neutralDelayFrames))
            return false;

        if (cinematicController != null && cinematicController.IsPlaying)
            return false;

        return true;
    }

    private void ResolveReferences()
    {
        if (panelMenu == null)
            panelMenu = FindInActiveScene<PanelMenu>();
        if (loadPanelController == null)
            loadPanelController = FindInActiveScene<MainMenuLoadPanelController>();
        if (cinematicController == null)
            cinematicController = FindInActiveScene<MainMenuCinematicController>();

        if (panelMenuRoot == null)
        {
            Transform t = FindTransformByName("Panel_Menu");
            if (t != null)
                panelMenuRoot = t.gameObject;
        }

        if (panelMenuCanvasGroup == null && panelMenuRoot != null)
            panelMenuCanvasGroup = panelMenuRoot.GetComponent<CanvasGroup>();

        if (panelNewGameRoot == null)
        {
            Transform t = FindTransformByName("Panel_NewGame");
            if (t != null)
                panelNewGameRoot = t.gameObject;
        }

        if (panelLoadRoot == null)
        {
            Transform t = FindTransformByName("Panel_Load");
            if (t != null)
                panelLoadRoot = t.gameObject;
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

        if (sharedMenuContainer == null)
            sharedMenuContainer = ResolveSharedMenuContainer();
    }

    private RectTransform ResolveSharedMenuContainer()
    {
        Transform menuParent = panelMenuRoot != null ? panelMenuRoot.transform.parent : null;
        Transform loadParent = panelLoadRoot != null ? panelLoadRoot.transform.parent : null;

        if (menuParent != null && loadParent != null && menuParent == loadParent)
            return menuParent as RectTransform;

        if (menuParent is RectTransform menuRect)
            return menuRect;

        if (loadParent is RectTransform loadRect)
            return loadRect;

        return null;
    }

    private void EnsureSharedMenuContainerVisible()
    {
        if (sharedMenuContainer == null)
            sharedMenuContainer = ResolveSharedMenuContainer();

        if (sharedMenuContainer == null)
            return;

        if (sharedMenuContainer.localScale == Vector3.zero)
        {
            sharedMenuContainer.localScale = Vector3.one;
            Debug.Log($"[MainMenuState] Restored shared menu container scale on '{sharedMenuContainer.name}'");
        }
    }

    private void SetPanelMenuVisible(bool visible)
    {
        if (panelMenuCanvasGroup != null)
        {
            panelMenuCanvasGroup.alpha = visible ? 1f : 0f;
            panelMenuCanvasGroup.interactable = visible;
            panelMenuCanvasGroup.blocksRaycasts = visible;
            return;
        }

        SetPanelActive(panelMenuRoot, visible);
    }

    private static void SetPanelActive(GameObject panel, bool visible)
    {
        if (panel != null && panel.activeSelf != visible)
            panel.SetActive(visible);
    }

    private void ResetUiInputTracking()
    {
        previousUiMove = Vector2.zero;
        previousUiSubmitPressed = false;
        previousUiCancelPressed = false;
    }

    private void ResetUiInputTrackingFromCurrentInput()
    {
        previousUiMove = ReadCurrentUiMove();
        previousUiSubmitPressed = IsSubmitPressedNow();
        previousUiCancelPressed = IsCancelPressedNow();
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

    private static Vector2 ReadCurrentUiMove()
    {
#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
        if (module != null && module.move.action != null)
            return module.move.action.ReadValue<Vector2>();
#endif
        return Vector2.zero;
    }

    private static bool IsSubmitPressedNow()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed))
            return true;

        InputSystemUIInputModule module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
        if (module != null && module.submit.action != null && module.submit.action.IsPressed())
            return true;
#endif
        return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
    }

    private static bool IsCancelPressedNow()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.isPressed)
            return true;

        InputSystemUIInputModule module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
        if (module != null && module.cancel.action != null && module.cancel.action.IsPressed())
            return true;
#endif
        return Input.GetKey(KeyCode.Escape);
    }

    private static bool IsFocusedOnTextInputControl()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            return false;

        GameObject selected = eventSystem.currentSelectedGameObject;
        return selected.GetComponentInParent<TMPro.TMP_InputField>()?.isFocused == true ||
               selected.GetComponentInParent<UnityEngine.UI.InputField>()?.isFocused == true;
    }

    private static bool IsAnyTextInputFocusedInUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null)
            {
                if (selected.GetComponentInParent<UnityEngine.UI.InputField>()?.isFocused == true)
                    return true;
                if (selected.GetComponentInParent<TMPro.TMP_InputField>()?.isFocused == true)
                    return true;
            }
        }

        TMPro.TMP_InputField[] tmpInputs = FindObjectsByType<TMPro.TMP_InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < tmpInputs.Length; i++)
        {
            TMPro.TMP_InputField field = tmpInputs[i];
            if (field != null && field.isActiveAndEnabled && field.isFocused)
                return true;
        }

        UnityEngine.UI.InputField[] legacyInputs = FindObjectsByType<UnityEngine.UI.InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < legacyInputs.Length; i++)
        {
            UnityEngine.UI.InputField field = legacyInputs[i];
            if (field != null && field.isActiveAndEnabled && field.isFocused)
                return true;
        }

        return false;
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

    private static T FindInActiveScene<T>() where T : Component
    {
        Scene active = SceneManager.GetActiveScene();
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            T item = all[i];
            if (item == null)
                continue;
            if (item.gameObject.scene == active)
                return item;
        }

        return null;
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

    private static Transform FindTransformByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform match = FindInChildrenRecursive(roots[i].transform, objectName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static Transform FindInChildrenRecursive(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindInChildrenRecursive(root.GetChild(i), objectName);
            if (match != null)
                return match;
        }

        return null;
    }
}
