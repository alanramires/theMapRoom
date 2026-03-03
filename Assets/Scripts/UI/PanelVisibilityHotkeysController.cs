using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-500)]
public class PanelVisibilityHotkeysController : MonoBehaviour
{
    private const string PanelDebugName = "Panel_Debug";
    private const string PanelHotkeysName = "panel_hotkeys";
    private const string PanelTurnName = "Panel_turn";
    private const string HotkeysTitleName = "hotkeys_title";
    private const string HotkeysTextName = "hotkeys_txt";

    [Header("Manual Bindings (Optional)")]
    [SerializeField] private GameObject panelDebugBinding;
    [SerializeField] private GameObject panelHotkeysBinding;
    [SerializeField] private TMP_Text hotkeysTitleBinding;
    [SerializeField] private TMP_Text hotkeysTextBinding;
    [SerializeField] private bool autoCreateHotkeysIfMissing = true;

    private GameObject panelDebug;
    private GameObject panelHotkeys;
    private TMP_Text hotkeysTitle;
    private TMP_Text hotkeysText;
    private CanvasGroup hotkeysSelfCanvasGroup;
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
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        if (UiInputBlocker.IsTextInputFocused())
            return;

        RefreshReferences(forceSceneInit: false);

        if (WasDebugTogglePressedThisFrame())
            TogglePanelDebug();

        if (WasHotkeysTogglePressedThisFrame())
            TogglePanelHotkeys();
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

        if (hotkeysTitle == null && panelHotkeys != null)
            hotkeysTitle = FindNamedTmpText(panelHotkeys.transform, HotkeysTitleName);
        if (hotkeysTitleBinding != null)
            hotkeysTitle = hotkeysTitleBinding;

        if (panelHotkeys != null && hotkeysText == null)
            hotkeysText = FindNamedTmpText(panelHotkeys.transform, HotkeysTextName) ?? panelHotkeys.GetComponentInChildren<TMP_Text>(true);
        if (hotkeysTextBinding != null)
            hotkeysText = hotkeysTextBinding;
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
        {
            EnsureHotkeysDefaultTexts();
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
        hotkeysText.text =
            "HOME: Center HQ\n" +
            "N: Mini-Map\n" +
            "Enter: Confirm\n" +
            "Esc: Cancel\n" +
            "Tab: Cycle Units\n" +
            "Shift+Tab: Reverse Cycle Units";

        EnsureHotkeysDefaultTexts();

        return true;
    }

    private void EnsureHotkeysDefaultTexts()
    {
        if (hotkeysTitle != null && string.IsNullOrWhiteSpace(hotkeysTitle.text))
            hotkeysTitle.text = "HOTKEYS";
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
        return Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.H);
#endif
    }
}
