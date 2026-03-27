using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PanelDialogController : MonoBehaviour
{
    private static PanelDialogController instance;

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private DialogDatabase dialogDatabase;
    [SerializeField] private GameObject panelUnit;
    [SerializeField] private TMP_Text textUnit;
    [SerializeField] private Image unitPreviewImage;
    [SerializeField] [Range(100f, 1200f)] private float shoppingPreviewPanelHeight = 350f;
    [SerializeField] [Range(10f, 72f)] private float shoppingPreviewFontSize = 18f;
    [Header("Dock")]
    [SerializeField] private Vector2 dockedCenterAnchoredPosition = Vector2.zero;

    private string lastText = string.Empty;
    private bool lastPanelVisible;
    private bool lastTextVisible;
    private CanvasGroup selfPanelCanvasGroup;
    private Color lastTextColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
    private bool hasExternalOverrideText;
    private string externalOverrideText = string.Empty;
    private float externalOverrideUntilUnscaledTime = -1f;
    private bool shoppingPreviewMode;
    private Sprite externalPreviewSprite;
    private Color externalPreviewColor = Color.white;
    private RectTransform panelRect;
    private float basePanelHeight = -1f;
    private bool cachedPanelDockDefaults;
    private Vector2 basePanelAnchorMin;
    private Vector2 basePanelAnchorMax;
    private Vector2 basePanelPivot;
    private Vector2 basePanelAnchoredPosition;
    private bool isTemporarilyCentered;
    private RectTransform textRect;
    private float baseTextRectHeight = -1f;
    private bool cachedTextLayoutDefaults;
    private TextWrappingModes baseTextWrappingMode;
    private bool baseRichText;
    private TextOverflowModes baseOverflowMode;
    private bool baseEnableAutoSizing;
    private float baseFontSize = -1f;
    private TextAlignmentOptions baseAlignment = TextAlignmentOptions.TopLeft;
    private bool cachedTextRectDefaults;
    private Vector2 baseTextAnchorMin;
    private Vector2 baseTextAnchorMax;
    private Vector2 baseTextPivot;
    private Vector2 baseTextAnchoredPosition;
    private Vector2 baseTextSizeDelta;

    private void Awake()
    {
        instance = this;
        TryAutoAssignReferences();
        HideAll(force: true);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        TryAutoAssignReferences();
        Refresh(force: false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
        HideAll(force: true);
    }
#endif

    private void TryAutoAssignReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (panelUnit == null)
            panelUnit = FindNamedObject("panel_dialog") ?? FindNamedObject("panel_unit") ?? FindNamedObject("unit_panel");
        if (panelUnit == null)
            panelUnit = gameObject;
        if (panelRect == null && panelUnit != null)
            panelRect = panelUnit.GetComponent<RectTransform>();
        if (basePanelHeight <= 0f && panelRect != null)
            basePanelHeight = Mathf.Max(0f, panelRect.rect.height);
        if (!cachedPanelDockDefaults && panelRect != null)
        {
            basePanelAnchorMin = panelRect.anchorMin;
            basePanelAnchorMax = panelRect.anchorMax;
            basePanelPivot = panelRect.pivot;
            basePanelAnchoredPosition = panelRect.anchoredPosition;
            cachedPanelDockDefaults = true;
        }

        if (textUnit == null)
            textUnit = FindNamedTmpText("text_unit")
                ?? FindNamedTmpText("txt_unit")
                ?? FindNamedTmpText("unit_text")
                ?? FindNamedTmpText("text_dialog");
        if (textRect == null && textUnit != null)
            textRect = textUnit.rectTransform;
        if (!cachedTextLayoutDefaults && textUnit != null)
        {
            baseTextWrappingMode = textUnit.textWrappingMode;
            baseRichText = textUnit.richText;
            baseOverflowMode = textUnit.overflowMode;
            baseEnableAutoSizing = textUnit.enableAutoSizing;
            baseFontSize = textUnit.fontSize;
            baseAlignment = textUnit.alignment;
            baseTextRectHeight = textRect != null ? Mathf.Max(0f, textRect.rect.height) : -1f;
            cachedTextLayoutDefaults = true;
        }
        if (!cachedTextRectDefaults && textRect != null)
        {
            baseTextAnchorMin = textRect.anchorMin;
            baseTextAnchorMax = textRect.anchorMax;
            baseTextPivot = textRect.pivot;
            baseTextAnchoredPosition = textRect.anchoredPosition;
            baseTextSizeDelta = textRect.sizeDelta;
            cachedTextRectDefaults = true;
        }
        if (unitPreviewImage == null)
            unitPreviewImage = FindNamedImage("img_unit")
                ?? FindNamedImage("unit_image")
                ?? FindNamedImage("image_unit")
                ?? FindNamedImage("unit_preview")
                ?? FindNamedImage("preview_unit")
                ?? FindTopLeftPreviewImageCandidate();

#if UNITY_EDITOR
        if (dialogDatabase == null)
            dialogDatabase = FindFirstAssetEditor<DialogDatabase>();
#endif
    }

    private void Refresh(bool force)
    {
        if (hasExternalOverrideText &&
            externalOverrideUntilUnscaledTime > 0f &&
            Time.unscaledTime >= externalOverrideUntilUnscaledTime)
        {
            hasExternalOverrideText = false;
            externalOverrideText = string.Empty;
            externalOverrideUntilUnscaledTime = -1f;
            shoppingPreviewMode = false;
            externalPreviewSprite = null;
            externalPreviewColor = Color.white;
        }

        if (hasExternalOverrideText)
        {
            Color overrideColor = ResolveActiveTeamColor();
            SetVisible(panelVisible: true, textVisible: true, textValue: externalOverrideText, textColor: overrideColor, force: force);
            RefreshPreviewVisual();
            return;
        }

        UnitManager selectedUnit = ResolveSelectedUnit();
        if (selectedUnit == null)
        {
            HideAll(force);
            RefreshPreviewVisual();
            return;
        }

        string unitName = ResolveUnitDisplayName(selectedUnit);
        string nextText = BuildStateText(unitName, turnStateManager.CurrentCursorState);
        Color textColor = ResolveActiveTeamColor();
        SetVisible(panelVisible: true, textVisible: true, textValue: nextText, textColor: textColor, force: force);
        RefreshPreviewVisual();
    }

    private UnitManager ResolveSelectedUnit()
    {
        if (turnStateManager != null && turnStateManager.SelectedUnit != null)
            return turnStateManager.SelectedUnit;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit != null && unit.IsSelected)
                return unit;
        }

        return null;
    }

    private string BuildStateText(string unitName, TurnStateManager.CursorState state)
    {
        if (state == TurnStateManager.CursorState.MoveuAndando || state == TurnStateManager.CursorState.MoveuParado)
        {
            return ResolvePanelMessage(
                "panel_dialog.state.moving",
                "<unit> :: <state>",
                new Dictionary<string, string>
                {
                    { "unit", unitName },
                    { "state", ResolvePanelMessage("panel_dialog.label.moving", "Moving") },
                    { "sensor", string.Empty }
                });
        }

        string sensor = ResolveSensorName(state);
        if (string.IsNullOrWhiteSpace(sensor))
            return unitName;

        bool isConfirm = IsSensorConfirmPhase(state);
        return isConfirm
            ? ResolvePanelMessage(
                "panel_dialog.state.sensor_confirm",
                "<unit> :: <sensor> Confirm",
                new Dictionary<string, string>
                {
                    { "unit", unitName },
                    { "sensor", sensor },
                    { "state", string.Empty }
                })
            : ResolvePanelMessage(
                "panel_dialog.state.sensor",
                "<unit> :: <sensor>",
                new Dictionary<string, string>
                {
                    { "unit", unitName },
                    { "sensor", sensor },
                    { "state", string.Empty }
                });
    }

    private string ResolveSensorName(TurnStateManager.CursorState state)
    {
        switch (state)
        {
            case TurnStateManager.CursorState.Mirando:
                return ResolvePanelMessage("panel_dialog.sensor.aim", "Aim");
            case TurnStateManager.CursorState.Capturando:
                return ResolvePanelMessage("panel_dialog.sensor.capture", "Capture");
            case TurnStateManager.CursorState.Embarcando:
                return ResolvePanelMessage("panel_dialog.sensor.embark", "Embark");
            case TurnStateManager.CursorState.Desembarcando:
                return ResolvePanelMessage("panel_dialog.sensor.disembark", "Disembark");
            case TurnStateManager.CursorState.Pousando:
                return ResolvePanelMessage("panel_dialog.sensor.landing", "Landing");
            case TurnStateManager.CursorState.Fundindo:
                return ResolvePanelMessage("panel_dialog.sensor.merge", "Merge");
            case TurnStateManager.CursorState.Suprindo:
                return ResolvePanelMessage("panel_dialog.sensor.supply", "Supply");
            default:
                return string.Empty;
        }
    }

    private bool IsSensorConfirmPhase(TurnStateManager.CursorState state)
    {
        string step = turnStateManager != null ? turnStateManager.CurrentScannerPromptStepDebug : string.Empty;
        if (string.IsNullOrWhiteSpace(step))
            return false;

        switch (state)
        {
            case TurnStateManager.CursorState.Mirando:
                return step == "MirandoConfirmTarget";
            case TurnStateManager.CursorState.Embarcando:
                return step == "EmbarkConfirmTarget";
            case TurnStateManager.CursorState.Pousando:
                return step == "LandingConfirmOption";
            case TurnStateManager.CursorState.Desembarcando:
                return step == "DisembarkConfirm";
            case TurnStateManager.CursorState.Fundindo:
            case TurnStateManager.CursorState.Suprindo:
                return step == "MergeConfirm";
            default:
                return false;
        }
    }

    private string ResolveUnitDisplayName(UnitManager unit)
    {
        if (unit == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(unit.UnitDisplayName))
            return unit.UnitDisplayName;

        return unit.name;
    }

    private void HideAll(bool force)
    {
        RestoreDockLayoutIfNeeded();
        SetVisible(panelVisible: false, textVisible: false, textValue: string.Empty, textColor: ResolveActiveTeamColor(), force: force);
    }

    private void SetVisible(bool panelVisible, bool textVisible, string textValue, Color textColor, bool force)
    {
        if (panelUnit != null && (force || lastPanelVisible != panelVisible))
            SetPanelVisible(panelVisible);

        if (textUnit != null)
        {
            if (force || lastText != textValue)
                textUnit.text = textValue;

            if (force || lastTextColor != textColor)
                textUnit.color = textColor;

            if (force || lastTextVisible != textVisible || textUnit.gameObject.activeSelf != textVisible)
                textUnit.gameObject.SetActive(textVisible);

            textUnit.enabled = textVisible;
        }

        lastPanelVisible = panelVisible;
        lastTextVisible = textVisible;
        lastText = textValue ?? string.Empty;
        lastTextColor = textColor;
    }

    private Color ResolveActiveTeamColor()
    {
        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        DialogManager dialogManager = DialogManager.Instance;
        if (dialogManager != null)
            return dialogManager.ResolveDialogTextColor(activeTeam);

        if (activeTeam == TeamId.Green || activeTeam == TeamId.Red || activeTeam == TeamId.Blue || activeTeam == TeamId.Yellow)
            return TeamUtils.GetColor(activeTeam);

        return Color.white;
    }

    public static bool TrySetExternalText(string text)
    {
        if (instance == null)
            return false;

        instance.shoppingPreviewMode = false;
        instance.externalPreviewSprite = null;
        instance.externalPreviewColor = Color.white;
        instance.SetExternalText(text);
        return true;
    }

    public static void ClearExternalText()
    {
        if (instance == null)
            return;

        instance.hasExternalOverrideText = false;
        instance.externalOverrideText = string.Empty;
        instance.externalOverrideUntilUnscaledTime = -1f;
        instance.shoppingPreviewMode = false;
        instance.externalPreviewSprite = null;
        instance.externalPreviewColor = Color.white;
    }

    public static bool TrySetTransientText(string text, float durationSeconds = 2.6f)
    {
        if (instance == null)
            return false;

        instance.shoppingPreviewMode = false;
        instance.externalPreviewSprite = null;
        instance.externalPreviewColor = Color.white;
        instance.SetExternalText(text, Mathf.Max(0.05f, durationSeconds), timed: true);
        return true;
    }

    public static bool TrySetShoppingPreview(string text, Sprite previewSprite)
    {
        if (instance == null)
            return false;

        instance.shoppingPreviewMode = true;
        instance.externalPreviewSprite = previewSprite;
        instance.externalPreviewColor = Color.white;
        instance.SetExternalText(text, 0f, timed: false);
        return true;
    }

    public static bool TrySetShoppingPreview(string text, Sprite previewSprite, Color previewColor)
    {
        if (instance == null)
            return false;

        instance.shoppingPreviewMode = true;
        instance.externalPreviewSprite = previewSprite;
        instance.externalPreviewColor = previewColor;
        instance.SetExternalText(text, 0f, timed: false);
        return true;
    }

    public static bool HasActiveExternalText()
    {
        if (instance == null)
            return false;

        if (!instance.hasExternalOverrideText || string.IsNullOrWhiteSpace(instance.externalOverrideText))
            return false;

        if (instance.externalOverrideUntilUnscaledTime > 0f &&
            Time.unscaledTime >= instance.externalOverrideUntilUnscaledTime)
        {
            instance.hasExternalOverrideText = false;
            instance.externalOverrideText = string.Empty;
            instance.externalOverrideUntilUnscaledTime = -1f;
            return false;
        }

        return true;
    }

    public static bool HasActiveFixedExternalText()
    {
        if (instance == null)
            return false;

        return instance.hasExternalOverrideText && instance.externalOverrideUntilUnscaledTime < 0f;
    }

    public static string ResolveDialogMessage(string id, string fallback)
    {
        if (instance == null)
            return fallback ?? string.Empty;

        return instance.ResolvePanelMessage(id, fallback);
    }

    public static string ResolveDialogMessage(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        if (instance == null)
            return ApplyInlineTokens(fallback ?? string.Empty, tokens);

        return instance.ResolvePanelMessage(id, fallback, tokens);
    }

    private void SetExternalText(string text)
    {
        SetExternalText(text, 0f, timed: false);
    }

    private void SetExternalText(string text, float durationSeconds, bool timed)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            hasExternalOverrideText = false;
            externalOverrideText = string.Empty;
            externalOverrideUntilUnscaledTime = -1f;
            return;
        }

        hasExternalOverrideText = true;
        externalOverrideText = text;
        externalOverrideUntilUnscaledTime = timed ? Time.unscaledTime + Mathf.Max(0.05f, durationSeconds) : -1f;
    }

    private void RefreshPreviewVisual()
    {
        if (unitPreviewImage != null)
        {
            bool showPreview = hasExternalOverrideText && shoppingPreviewMode && externalPreviewSprite != null;
            if (showPreview)
            {
                unitPreviewImage.sprite = externalPreviewSprite;
                unitPreviewImage.color = externalPreviewColor;
            }

            unitPreviewImage.enabled = showPreview;
            if (unitPreviewImage.gameObject.activeSelf != showPreview)
                unitPreviewImage.gameObject.SetActive(showPreview);
        }

        if (panelRect == null || basePanelHeight <= 0f)
        {
            RestoreDockLayoutIfNeeded();
            RefreshShoppingTextLayout();
            return;
        }

        float targetHeight = basePanelHeight;
        if (hasExternalOverrideText && shoppingPreviewMode)
            targetHeight = Mathf.Max(350f, shoppingPreviewPanelHeight);

        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        RefreshDockByHelperState(targetHeight);
        RefreshShoppingTextLayout();
    }

    private void RefreshDockByHelperState(float currentPanelHeight)
    {
        bool isExpandedToShoppingSize =
            hasExternalOverrideText &&
            shoppingPreviewMode &&
            currentPanelHeight >= Mathf.Max(350f, shoppingPreviewPanelHeight) - 0.01f;

        bool helperDockedLeft = PanelHelperController.IsDockedCenterLeft();
        bool cursorStillOnRight = PanelHelperController.IsCursorNearOriginalDockRegion();
        bool shouldCenterTemporarily = isExpandedToShoppingSize &&
                                       helperDockedLeft &&
                                       (shoppingPreviewMode || cursorStillOnRight);

        if (shouldCenterTemporarily)
            ApplyTemporaryCenteredDock();
        else
            RestoreDockLayoutIfNeeded();
    }

    private void ApplyTemporaryCenteredDock()
    {
        if (panelRect == null)
            return;

        if (!cachedPanelDockDefaults)
        {
            basePanelAnchorMin = panelRect.anchorMin;
            basePanelAnchorMax = panelRect.anchorMax;
            basePanelPivot = panelRect.pivot;
            basePanelAnchoredPosition = panelRect.anchoredPosition;
            cachedPanelDockDefaults = true;
        }

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = dockedCenterAnchoredPosition;
        isTemporarilyCentered = true;
    }

    private void RestoreDockLayoutIfNeeded()
    {
        if (!isTemporarilyCentered || panelRect == null || !cachedPanelDockDefaults)
            return;

        panelRect.anchorMin = basePanelAnchorMin;
        panelRect.anchorMax = basePanelAnchorMax;
        panelRect.pivot = basePanelPivot;
        panelRect.anchoredPosition = basePanelAnchoredPosition;
        isTemporarilyCentered = false;
    }

    private void RefreshShoppingTextLayout()
    {
        if (textUnit == null)
            return;

        bool shoppingActive = hasExternalOverrideText && shoppingPreviewMode;
        if (shoppingActive)
        {
            textUnit.enableAutoSizing = false;
            textUnit.fontSize = Mathf.Max(10f, shoppingPreviewFontSize);
            textUnit.textWrappingMode = TextWrappingModes.Normal;
            textUnit.overflowMode = TextOverflowModes.Truncate;
            textUnit.richText = false;
            textUnit.alignment = TextAlignmentOptions.TopLeft;
            if (textRect != null && panelRect != null)
            {
                bool previewVisible = unitPreviewImage != null &&
                                      unitPreviewImage.gameObject.activeSelf &&
                                      unitPreviewImage.enabled;
                float previewWidth = previewVisible ? Mathf.Max(0f, unitPreviewImage.rectTransform.rect.width) : 0f;
                float leftInset = previewVisible ? 14f + previewWidth + 10f : 14f;
                float rightInset = 10f;
                float topInset = 10f;
                float bottomInset = 10f;
                float targetWidth = Mathf.Max(24f, panelRect.rect.width - leftInset - rightInset);
                float targetHeight = Mathf.Max(24f, panelRect.rect.height - topInset - bottomInset);

                textRect.anchorMin = new Vector2(0f, 1f);
                textRect.anchorMax = new Vector2(0f, 1f);
                textRect.pivot = new Vector2(0f, 1f);
                textRect.anchoredPosition = new Vector2(leftInset, -topInset);
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
            }
            return;
        }

        if (!cachedTextLayoutDefaults)
            return;

        textUnit.enableAutoSizing = baseEnableAutoSizing;
        if (baseFontSize > 0f)
            textUnit.fontSize = baseFontSize;
        textUnit.textWrappingMode = baseTextWrappingMode;
        textUnit.richText = baseRichText;
        textUnit.overflowMode = baseOverflowMode;
        textUnit.alignment = baseAlignment;
        if (textRect != null && cachedTextRectDefaults)
        {
            textRect.anchorMin = baseTextAnchorMin;
            textRect.anchorMax = baseTextAnchorMax;
            textRect.pivot = baseTextPivot;
            textRect.anchoredPosition = baseTextAnchoredPosition;
            textRect.sizeDelta = baseTextSizeDelta;
        }
        else if (textRect != null && baseTextRectHeight > 0f)
        {
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, baseTextRectHeight);
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelUnit == null)
            return;

        if (panelUnit == gameObject)
        {
            if (selfPanelCanvasGroup == null)
                selfPanelCanvasGroup = panelUnit.GetComponent<CanvasGroup>();
            if (selfPanelCanvasGroup == null)
                selfPanelCanvasGroup = panelUnit.AddComponent<CanvasGroup>();

            selfPanelCanvasGroup.alpha = visible ? 1f : 0f;
            selfPanelCanvasGroup.interactable = false;
            selfPanelCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (panelUnit.activeSelf != visible)
            panelUnit.SetActive(visible);
    }

    private string ResolvePanelMessage(string id, string fallback)
    {
        if (dialogDatabase == null)
            return fallback ?? string.Empty;

        return dialogDatabase.Resolve(id, fallback);
    }

    private string ResolvePanelMessage(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        if (dialogDatabase == null)
            return ApplyInlineTokens(fallback ?? string.Empty, tokens);

        return dialogDatabase.Resolve(id, fallback, tokens);
    }

    private static string ApplyInlineTokens(string template, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template) || tokens == null || tokens.Count == 0)
            return template ?? string.Empty;

        string output = template;
        foreach (KeyValuePair<string, string> pair in tokens)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            string key = pair.Key.Trim();
            string val = pair.Value ?? string.Empty;
            
            output = output.Replace($"<{key}>", val);
            output = output.Replace($"<{key.ToLowerInvariant()}>", val);
            output = output.Replace($"<{key.ToUpperInvariant()}>", val);
            if (key.Length > 0)
            {
                string titleCase = char.ToUpperInvariant(key[0]) + (key.Length > 1 ? key.Substring(1).ToLowerInvariant() : string.Empty);
                output = output.Replace($"<{titleCase}>", val);
            }
        }

        return output;
    }

#if UNITY_EDITOR
    private static T FindFirstAssetEditor<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }
#endif

    private GameObject FindNamedObject(string name)
    {
        Transform local = FindChildRecursive(transform, name);
        if (local != null)
            return local.gameObject;

        GameObject global = GameObject.Find(name);
        return global;
    }

    private TMP_Text FindNamedTmpText(string name)
    {
        Transform local = FindChildRecursive(transform, name);
        if (local != null)
            return local.GetComponent<TMP_Text>();

        GameObject global = GameObject.Find(name);
        return global != null ? global.GetComponent<TMP_Text>() : null;
    }

    private Image FindNamedImage(string name)
    {
        Transform local = FindChildRecursive(transform, name);
        if (local != null)
            return local.GetComponent<Image>();

        GameObject global = GameObject.Find(name);
        return global != null ? global.GetComponent<Image>() : null;
    }

    private Image FindTopLeftPreviewImageCandidate()
    {
        if (panelUnit == null)
            return null;

        Image[] images = panelUnit.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length <= 0)
            return null;

        Image best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < images.Length; i++)
        {
            Image candidate = images[i];
            if (candidate == null)
                continue;
            if (candidate == unitPreviewImage)
                continue;
            if (candidate.transform == panelUnit.transform)
                continue;

            RectTransform rt = candidate.rectTransform;
            if (rt == null)
                continue;

            // Heuristica para o placeholder criado: 100x100 ancorado no canto superior esquerdo.
            float anchorScore = 0f;
            anchorScore -= Mathf.Abs(rt.anchorMin.x - 0f) * 10f;
            anchorScore -= Mathf.Abs(rt.anchorMin.y - 1f) * 10f;
            anchorScore -= Mathf.Abs(rt.anchorMax.x - 0f) * 10f;
            anchorScore -= Mathf.Abs(rt.anchorMax.y - 1f) * 10f;

            float sizeX = Mathf.Max(0f, rt.rect.width);
            float sizeY = Mathf.Max(0f, rt.rect.height);
            float sizeScore = -Mathf.Abs(sizeX - 100f) * 0.2f - Mathf.Abs(sizeY - 100f) * 0.2f;

            float pivotScore = 0f;
            pivotScore -= Mathf.Abs(rt.pivot.x - 0f) * 4f;
            pivotScore -= Mathf.Abs(rt.pivot.y - 1f) * 4f;

            float score = anchorScore + sizeScore + pivotScore;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}


