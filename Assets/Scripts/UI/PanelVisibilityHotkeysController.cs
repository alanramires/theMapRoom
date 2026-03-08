using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-500)]
public class PanelVisibilityHotkeysController : MonoBehaviour
{
    private enum RetractDirection
    {
        Auto = 0,
        Left = 1,
        Right = 2
    }

    private const string PanelDebugName = "Panel_Debug";
    private const string PanelHotkeysName = "panel_hotkeys";
    private const string PanelTurnName = "Panel_turn";
    private const string HotkeysTitleName = "hotkeys_title";
    private const string HotkeysTextName = "hotkeys_txt";
    private const string HotkeysToggleButtonName = "button_toggle";

    [Header("Manual Bindings (Optional)")]
    [SerializeField] private GameObject panelDebugBinding;
    [SerializeField] private GameObject panelHotkeysBinding;
    [SerializeField] private Button hotkeysToggleButtonBinding;
    [SerializeField] private TMP_Text hotkeysTitleBinding;
    [SerializeField] private TMP_Text hotkeysTextBinding;
    [SerializeField] private bool autoCreateHotkeysIfMissing = true;
    [Header("Retractable Panel")]
    [SerializeField] private bool startHotkeysRetracted = true;
    [SerializeField] private bool useFixedShownAnchoredX = true;
    [SerializeField] private float fixedShownAnchoredX = 0f;
    [SerializeField] private bool useFixedRetractedAnchoredX = true;
    [SerializeField] private float fixedRetractedAnchoredX = -326f;
    [SerializeField] [Min(0f)] private float retractVisibleWidth = 24f;
    [SerializeField] private RetractDirection retractDirection = RetractDirection.Auto;
    [SerializeField] private bool animateRetract = true;
    [SerializeField] [Range(1f, 30f)] private float retractLerpSpeed = 14f;
    [Header("Display")]
    [SerializeField] private KeyCode fullscreenToggleKey = KeyCode.F11;
    [SerializeField] private bool preferExclusiveFullscreen = true;

    private GameObject panelDebug;
    private GameObject panelHotkeys;
    private RectTransform panelHotkeysRect;
    private Button hotkeysToggleButton;
    private TMP_Text hotkeysTitle;
    private TMP_Text hotkeysText;
    private CanvasGroup hotkeysSelfCanvasGroup;
    private bool hotkeysRetractStateInitialized;
    private bool hotkeysIsRetracted;
    private float hotkeysShownAnchoredX;
    private float hotkeysRetractedAnchoredX;
    private float hotkeysTargetAnchoredX;
    private Button hotkeysToggleButtonHookedTarget;
    private bool debugPanelInitializedForScene;
    private int lastInitializedSceneHandle = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBootstrap()
    {
        PanelVisibilityHotkeysController existing = FindAnyObjectByType<PanelVisibilityHotkeysController>();
        if (existing != null)
            return;

        GameObject go = new GameObject(nameof(PanelVisibilityHotkeysController));
        go.AddComponent<PanelVisibilityHotkeysController>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RefreshReferences(forceSceneInit: true);
    }

    private void OnDestroy()
    {
        UnhookHotkeysToggleButton();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        RefreshReferences(forceSceneInit: false);
        bool textInputFocused = UiInputBlocker.IsTextInputFocused();

        if (!textInputFocused && WasDebugTogglePressedThisFrame())
            TogglePanelDebug();

        if (WasHotkeysTogglePressedThisFrame())
            TogglePanelHotkeys();

        if (!textInputFocused && WasFullscreenTogglePressedThisFrame())
            ToggleFullscreenMode();

        UpdateHotkeysRetractAnimation();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshReferences(forceSceneInit: true);
    }

    private void RefreshReferences(bool forceSceneInit)
    {
        Scene active = SceneManager.GetActiveScene();
        if (forceSceneInit || active.handle != lastInitializedSceneHandle)
        {
            debugPanelInitializedForScene = false;
            lastInitializedSceneHandle = active.handle;
            hotkeysRetractStateInitialized = false;
            UnhookHotkeysToggleButton();
        }

        if (panelDebug == null)
            panelDebug = FindSceneObjectByName(PanelDebugName);
        if (panelDebugBinding != null)
            panelDebug = panelDebugBinding;

        if (!debugPanelInitializedForScene && panelDebug != null)
        {
            panelDebug.SetActive(false);
            debugPanelInitializedForScene = true;
        }

        if (panelHotkeys == null)
            panelHotkeys = FindSceneObjectByName(PanelHotkeysName);
        if (panelHotkeysBinding != null)
            panelHotkeys = panelHotkeysBinding;
        if (panelHotkeys != null)
            panelHotkeysRect = panelHotkeys.GetComponent<RectTransform>();
        else
            panelHotkeysRect = null;

        if (hotkeysTitle == null && panelHotkeys != null)
            hotkeysTitle = FindNamedTmpText(panelHotkeys.transform, HotkeysTitleName);
        if (hotkeysTitleBinding != null)
            hotkeysTitle = hotkeysTitleBinding;

        if (panelHotkeys != null && hotkeysText == null)
            hotkeysText = FindNamedTmpText(panelHotkeys.transform, HotkeysTextName) ?? panelHotkeys.GetComponentInChildren<TMP_Text>(true);
        if (hotkeysTextBinding != null)
            hotkeysText = hotkeysTextBinding;

        if (hotkeysToggleButtonBinding != null)
        {
            hotkeysToggleButton = hotkeysToggleButtonBinding;
        }
        else if (panelHotkeys == null)
        {
            hotkeysToggleButton = null;
        }
        else
        {
            // Sempre prioriza o botao de toggle real para evitar bind em botao errado.
            hotkeysToggleButton = FindHotkeysToggleButton(panelHotkeys.transform);
            if (hotkeysToggleButton == null)
                hotkeysToggleButton = FindSceneButtonByAnyName(HotkeysToggleButtonName, "Button_toggle", "buttonToggle", "hotkeys_toggle");
        }

        HookHotkeysToggleButtonIfNeeded();
        EnsureHotkeysToggleButtonRuntimeSetup();
        InitializeHotkeysRetractStateIfNeeded();
    }

    private void TogglePanelDebug()
    {
        if (panelDebug == null)
            panelDebug = FindSceneObjectByName(PanelDebugName);
        if (panelDebug == null)
            return;

        panelDebug.SetActive(!panelDebug.activeSelf);
    }

    private void TogglePanelHotkeys()
    {
        bool createdNow = EnsurePanelHotkeysExists();
        if (panelHotkeys == null)
            return;

        if (createdNow)
            EnsureHotkeysDefaultTexts();

        if (CanUseRetractableHotkeysMode())
        {
            SetHotkeysRetracted(!hotkeysIsRetracted, immediate: false);
            return;
        }

        if (createdNow)
        {
            SetPanelHotkeysVisible(true);
            return;
        }

        SetPanelHotkeysVisible(!IsPanelHotkeysVisible());
    }

    private bool IsPanelHotkeysVisible()
    {
        if (panelHotkeys == null)
            return false;

        if (panelHotkeys == gameObject)
        {
            if (hotkeysSelfCanvasGroup != null)
                return hotkeysSelfCanvasGroup.alpha > 0.001f;
            return true;
        }

        return panelHotkeys.activeSelf;
    }

    private void SetPanelHotkeysVisible(bool visible)
    {
        if (panelHotkeys == null)
            return;

        if (panelHotkeys == gameObject)
        {
            if (hotkeysSelfCanvasGroup == null)
                hotkeysSelfCanvasGroup = panelHotkeys.GetComponent<CanvasGroup>();
            if (hotkeysSelfCanvasGroup == null)
                hotkeysSelfCanvasGroup = panelHotkeys.AddComponent<CanvasGroup>();

            hotkeysSelfCanvasGroup.alpha = visible ? 1f : 0f;
            hotkeysSelfCanvasGroup.interactable = false;
            hotkeysSelfCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (panelHotkeys.activeSelf != visible)
            panelHotkeys.SetActive(visible);
    }

    private bool CanUseRetractableHotkeysMode()
    {
        return panelHotkeysRect != null;
    }

    private void InitializeHotkeysRetractStateIfNeeded()
    {
        if (hotkeysRetractStateInitialized || !CanUseRetractableHotkeysMode())
            return;

        hotkeysShownAnchoredX = panelHotkeysRect.anchoredPosition.x;
        if (useFixedShownAnchoredX)
            hotkeysShownAnchoredX = fixedShownAnchoredX;
        float panelWidth = Mathf.Max(0f, panelHotkeysRect.rect.width);
        float visibleWidth = Mathf.Clamp(retractVisibleWidth, 0f, panelWidth);
        float delta = Mathf.Max(0f, panelWidth - visibleWidth);
        bool retractToLeft = ResolveRetractToLeft(panelHotkeysRect);
        hotkeysRetractedAnchoredX = hotkeysShownAnchoredX + (retractToLeft ? -delta : delta);
        if (useFixedRetractedAnchoredX)
            hotkeysRetractedAnchoredX = fixedRetractedAnchoredX;
        if (Mathf.Abs(hotkeysRetractedAnchoredX - hotkeysShownAnchoredX) <= 0.01f)
            hotkeysShownAnchoredX = 0f;

        hotkeysIsRetracted = startHotkeysRetracted;
        hotkeysTargetAnchoredX = hotkeysIsRetracted ? hotkeysRetractedAnchoredX : hotkeysShownAnchoredX;

        SetHotkeysPanelAnchoredX(hotkeysTargetAnchoredX);
        hotkeysRetractStateInitialized = true;
    }

    private void SetHotkeysRetracted(bool retracted, bool immediate)
    {
        if (!CanUseRetractableHotkeysMode())
        {
            SetPanelHotkeysVisible(!retracted);
            return;
        }

        InitializeHotkeysRetractStateIfNeeded();
        hotkeysIsRetracted = retracted;
        hotkeysTargetAnchoredX = hotkeysIsRetracted ? hotkeysRetractedAnchoredX : hotkeysShownAnchoredX;

        if (immediate || !animateRetract)
            SetHotkeysPanelAnchoredX(hotkeysTargetAnchoredX);
    }

    private void UpdateHotkeysRetractAnimation()
    {
        if (!CanUseRetractableHotkeysMode() || !hotkeysRetractStateInitialized || !animateRetract)
            return;

        float currentX = panelHotkeysRect.anchoredPosition.x;
        float nextX = Mathf.Lerp(currentX, hotkeysTargetAnchoredX, Time.unscaledDeltaTime * retractLerpSpeed);
        if (Mathf.Abs(hotkeysTargetAnchoredX - nextX) <= 0.01f)
            nextX = hotkeysTargetAnchoredX;

        SetHotkeysPanelAnchoredX(nextX);
    }

    private void SetHotkeysPanelAnchoredX(float x)
    {
        if (panelHotkeysRect == null)
            return;

        Vector2 anchored = panelHotkeysRect.anchoredPosition;
        anchored.x = x;
        panelHotkeysRect.anchoredPosition = anchored;
    }

    private void HookHotkeysToggleButtonIfNeeded()
    {
        if (hotkeysToggleButton == null)
            return;

        if (hotkeysToggleButtonHookedTarget != null && hotkeysToggleButtonHookedTarget != hotkeysToggleButton)
            UnhookHotkeysToggleButton();

        if (hotkeysToggleButtonHookedTarget == hotkeysToggleButton)
            return;

        hotkeysToggleButton.onClick.AddListener(TogglePanelHotkeys);
        hotkeysToggleButtonHookedTarget = hotkeysToggleButton;
    }

    private void UnhookHotkeysToggleButton()
    {
        if (hotkeysToggleButtonHookedTarget == null)
            return;

        hotkeysToggleButtonHookedTarget.onClick.RemoveListener(TogglePanelHotkeys);
        hotkeysToggleButtonHookedTarget = null;
    }

    private void EnsureHotkeysToggleButtonRuntimeSetup()
    {
        if (hotkeysToggleButton == null)
            return;

        hotkeysToggleButton.enabled = true;
        hotkeysToggleButton.interactable = true;
        hotkeysToggleButton.transition = Selectable.Transition.None;
        Navigation nav = hotkeysToggleButton.navigation;
        nav.mode = Navigation.Mode.None;
        hotkeysToggleButton.navigation = nav;

        if (hotkeysToggleButton.targetGraphic == null)
        {
            Graphic graphic = hotkeysToggleButton.GetComponent<Graphic>();
            if (graphic == null)
                graphic = hotkeysToggleButton.GetComponentInChildren<Graphic>(true);
            hotkeysToggleButton.targetGraphic = graphic;
        }

        if (hotkeysToggleButton.targetGraphic != null)
            hotkeysToggleButton.targetGraphic.raycastTarget = true;
    }

    private bool ResolveRetractToLeft(RectTransform panelRect)
    {
        switch (retractDirection)
        {
            case RetractDirection.Left:
                return true;
            case RetractDirection.Right:
                return false;
            default:
                // Auto: painel ancorado/pivotado para a esquerda retrai para a esquerda.
                return panelRect != null && panelRect.pivot.x <= 0.5f;
        }
    }

    private bool EnsurePanelHotkeysExists()
    {
        if (panelHotkeys != null)
            return false;
        if (!autoCreateHotkeysIfMissing)
            return false;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return false;

        panelHotkeys = new GameObject(PanelHotkeysName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = panelHotkeys.GetComponent<RectTransform>();
        panelRect.SetParent(canvas.transform, false);
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(260f, 140f);
        panelRect.anchoredPosition = new Vector2(10f, -80f);

        GameObject panelTurn = FindSceneObjectByName(PanelTurnName);
        if (panelTurn != null)
        {
            RectTransform turnRect = panelTurn.GetComponent<RectTransform>();
            if (turnRect != null && turnRect.parent == panelRect.parent)
            {
                float yOffset = Mathf.Abs(turnRect.anchoredPosition.y) + turnRect.rect.height + 8f;
                panelRect.anchoredPosition = new Vector2(turnRect.anchoredPosition.x, -yOffset);
            }
        }

        Image bg = panelHotkeys.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        GameObject textGo = new GameObject("hotkeys_txt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(panelRect, false);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(8f, 8f);
        textRect.offsetMax = new Vector2(-8f, -8f);

        hotkeysText = textGo.GetComponent<TextMeshProUGUI>();
        hotkeysText.fontSize = 16f;
        hotkeysText.color = Color.white;
        hotkeysText.alignment = TextAlignmentOptions.TopLeft;
        hotkeysText.textWrappingMode = TextWrappingModes.NoWrap;
        hotkeysText.text = BuildDefaultHotkeysText();

        EnsureHotkeysDefaultTexts();

        return true;
    }

    private void EnsureHotkeysDefaultTexts()
    {
        if (hotkeysTitle != null && string.IsNullOrWhiteSpace(hotkeysTitle.text))
            hotkeysTitle.text = ResolveDialog("hotkeys.title", "HOTKEYS");
        if (hotkeysText != null && string.IsNullOrWhiteSpace(hotkeysText.text))
            hotkeysText.text = BuildDefaultHotkeysText();
    }

    private static string ResolveDialog(string id, string fallback)
    {
        return PanelDialogController.ResolveDialogMessage(id, fallback);
    }

    private static string BuildDefaultHotkeysText()
    {
        return ResolveDialog(
            "hotkeys.default.list",
            "HOME: Center HQ\n" +
            "N: Mini-Map\n" +
            "Enter: Confirm\n" +
            "Esc: Cancel\n" +
            "Tab: Cycle Units\n" +
            "Shift+Tab: Reverse Cycle Units\n" +
            "F11: Toggle Fullscreen");
    }

    private static TMP_Text FindNamedTmpText(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                return child.GetComponent<TMP_Text>();
        }

        return null;
    }

    private static Button FindNamedButton(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                return child.GetComponent<Button>();
        }

        return null;
    }

    private static Button FindHotkeysToggleButton(Transform panelRoot)
    {
        if (panelRoot == null)
            return null;

        Button direct = FindNamedButton(panelRoot, HotkeysToggleButtonName);
        if (direct != null)
            return direct;

        Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null || b.gameObject == null)
                continue;

            string name = b.gameObject.name;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (name.IndexOf("toggle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return b;
        }

        return null;
    }

    private static Button FindSceneButtonByAnyName(params string[] names)
    {
        if (names == null || names.Length == 0)
            return null;

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null)
                continue;
            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded)
                continue;

            for (int n = 0; n < names.Length; n++)
            {
                string expected = names[n];
                if (string.IsNullOrWhiteSpace(expected))
                    continue;
                if (!string.Equals(t.name, expected, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                Button button = t.GetComponent<Button>();
                if (button != null)
                    return button;
            }
        }

        return null;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null)
                continue;
            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded)
                continue;
            if (string.Equals(t.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }

        return null;
    }

    private static bool WasDebugTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return false;

        return Keyboard.current.quoteKey.wasPressedThisFrame
            || Keyboard.current.semicolonKey.wasPressedThisFrame
            || Keyboard.current.backquoteKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Quote)
            || Input.GetKeyDown(KeyCode.Semicolon)
            || Input.GetKeyDown(KeyCode.BackQuote);
#endif
    }

    private static bool WasHotkeysTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.H);
#else
        return false;
#endif
#else
        return Input.GetKeyDown(KeyCode.H);
#endif
    }

    private bool WasFullscreenTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && WasFunctionKeyPressedThisFrame(fullscreenToggleKey))
            return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(fullscreenToggleKey);
#else
        return false;
#endif
#else
        return Input.GetKeyDown(fullscreenToggleKey);
#endif
    }

    private void ToggleFullscreenMode()
    {
        bool goingFullscreen = !Screen.fullScreen;
        if (!goingFullscreen)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
            PanelDialogController.TrySetTransientText("Fullscreen: OFF", 1.4f);
            return;
        }

        FullScreenMode targetMode = (!Application.isEditor && preferExclusiveFullscreen)
            ? FullScreenMode.ExclusiveFullScreen
            : FullScreenMode.FullScreenWindow;
        Screen.fullScreenMode = targetMode;
        Screen.fullScreen = true;
        PanelDialogController.TrySetTransientText($"Fullscreen: ON ({targetMode})", 1.4f);
    }

#if ENABLE_INPUT_SYSTEM
    private static bool WasFunctionKeyPressedThisFrame(KeyCode keyCode)
    {
        if (Keyboard.current == null)
            return false;

        switch (keyCode)
        {
            case KeyCode.F1: return Keyboard.current.f1Key.wasPressedThisFrame;
            case KeyCode.F2: return Keyboard.current.f2Key.wasPressedThisFrame;
            case KeyCode.F3: return Keyboard.current.f3Key.wasPressedThisFrame;
            case KeyCode.F4: return Keyboard.current.f4Key.wasPressedThisFrame;
            case KeyCode.F5: return Keyboard.current.f5Key.wasPressedThisFrame;
            case KeyCode.F6: return Keyboard.current.f6Key.wasPressedThisFrame;
            case KeyCode.F7: return Keyboard.current.f7Key.wasPressedThisFrame;
            case KeyCode.F8: return Keyboard.current.f8Key.wasPressedThisFrame;
            case KeyCode.F9: return Keyboard.current.f9Key.wasPressedThisFrame;
            case KeyCode.F10: return Keyboard.current.f10Key.wasPressedThisFrame;
            case KeyCode.F11: return Keyboard.current.f11Key.wasPressedThisFrame;
            case KeyCode.F12: return Keyboard.current.f12Key.wasPressedThisFrame;
            default: return false;
        }
    }
#endif
}
