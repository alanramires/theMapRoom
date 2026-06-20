using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("The Map Room/Construction/Construction Hud Controller")]
[DisallowMultipleComponent]
public class ConstructionHudController : MonoBehaviour
{
    [Header("Captured")]
    [SerializeField] private Transform capturedContainer;
    [SerializeField] private Image capturedBarImage;
    [SerializeField] private SpriteRenderer capturedBarRenderer;
    [SerializeField] private Image capturedFillImage;
    [SerializeField] private SpriteRenderer capturedFillRenderer;
    [SerializeField] private TMP_Text capturedText;
    [SerializeField] private bool hideWhenFull = false;
    [SerializeField] private bool capturedTintWithTeamColor = false;
    [SerializeField] private Color capturedDefaultColor = new Color(0.8235295f, 0.4117647f, 0.1176471f, 1f);
    [SerializeField] private Color captured50PercentColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color captured25PercentColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Flag Capture")]
    [SerializeField] private Transform flagIcon;
    [SerializeField] private TMP_Text flagText;
    [SerializeField] private Color flagThreatOutlineColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] [Range(0f, 1f)] private float flagThreatOutlineAlphaMin = 0.12f;
    [SerializeField] [Range(0f, 1f)] private float flagThreatOutlineAlphaMax = 0.42f;
    [SerializeField] [Range(0.5f, 3f)] private float flagThreatPulseMinDuration = 0.8f;
    [SerializeField] [Range(0.5f, 3f)] private float flagThreatPulseMaxDuration = 1.2f;
    [SerializeField] private Vector2 flagThreatOutlineDistance = new Vector2(1f, -1f);

    [Header("Sector Badge")]
    [SerializeField] private TMP_Text sectorBadge;

    [Header("Rally")]
    [SerializeField] private Image rallyTrafficImage;
    [SerializeField] private Sprite rallyOffSprite;
    [SerializeField] private Sprite rallyRedSprite;
    [SerializeField] private Sprite rallyYellowSprite;
    [SerializeField] private Sprite rallyGreenSprite;

    [Header("Sorting")]
    [SerializeField] private bool applyHudSorting = true;
    [SerializeField] private string hudSortingLayerName = "SFX";
    [SerializeField] private int hudSortingOrder = 60;

    private Outline flagThreatOutline;
    private bool shouldShowFlagThreatOutline;
    private float flagThreatPulseTimer;
    private float flagThreatPulseDuration = 1f;
    private Coroutine flagThreatPulseRoutine;

    private void Awake()
    {
        AutoAssignReferences();
        EnsureFlagThreatOutline();
        EnsureCaptureVisualOrder();
        ApplySorting();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignReferences();
        EnsureFlagThreatOutline();
        EnsureCaptureVisualOrder();
        ApplySorting();
    }
#endif

    public void RefreshBindings()
    {
        AutoAssignReferences();
        EnsureFlagThreatOutline();
        EnsureCaptureVisualOrder();
        ApplySorting();
    }

    public void Apply(
        int currentCapture,
        int maxCapture,
        bool isCapturable,
        TeamId ownerTeam,
        bool hideCaptureBarBecauseOccupied,
        bool showFlagThreatOutline = false,
        bool isRallyPoint = false,
        bool rallyOwnedByRallyOwner = false,
        AIRallyAssemblyState rallyState = AIRallyAssemblyState.None)
    {
        AutoAssignReferences();
        EnsureFlagThreatOutline();
        EnsureCaptureVisualOrder();

        int safeMax = Mathf.Max(0, maxCapture);
        int clampedCurrent = Mathf.Clamp(currentCapture, 0, safeMax);
        float ratio = safeMax > 0 ? Mathf.Clamp01((float)clampedCurrent / safeMax) : 0f;

        bool showContainer = isCapturable && !hideCaptureBarBecauseOccupied && (!hideWhenFull || clampedCurrent < safeMax);
        if (capturedContainer != null && capturedContainer.gameObject.activeSelf != showContainer)
            capturedContainer.gameObject.SetActive(showContainer);

        if (capturedBarImage != null)
        {
            if (capturedBarImage.gameObject.activeSelf != showContainer)
                capturedBarImage.gameObject.SetActive(showContainer);
            capturedBarImage.enabled = showContainer;
        }

        if (capturedBarRenderer != null)
        {
            if (capturedBarRenderer.gameObject.activeSelf != showContainer)
                capturedBarRenderer.gameObject.SetActive(showContainer);
            capturedBarRenderer.enabled = showContainer;
        }

        if (capturedText != null)
        {
            capturedText.text = $"{clampedCurrent}/{safeMax}";
            if (capturedText.gameObject.activeSelf != showContainer)
                capturedText.gameObject.SetActive(showContainer);
            capturedText.enabled = showContainer;
        }

        Color capturedColor = GetCapturedColorByRatio(ratio, ownerTeam);

        if (capturedFillImage != null)
        {
            capturedFillImage.fillAmount = ratio;
            capturedFillImage.color = capturedColor;
            if (capturedFillImage.gameObject.activeSelf != showContainer)
                capturedFillImage.gameObject.SetActive(showContainer);
            capturedFillImage.enabled = showContainer;
        }

        if (capturedFillRenderer != null)
        {
            Vector3 scale = capturedFillRenderer.transform.localScale;
            scale.x = Mathf.Max(0.001f, ratio);
            capturedFillRenderer.transform.localScale = scale;
            capturedFillRenderer.color = capturedColor;
            if (capturedFillRenderer.gameObject.activeSelf != showContainer)
                capturedFillRenderer.gameObject.SetActive(showContainer);
            capturedFillRenderer.enabled = showContainer;
        }

        bool showFlagIcon = clampedCurrent != safeMax;
        bool showFlagText = showFlagIcon && hideCaptureBarBecauseOccupied;

        if (flagIcon != null && flagIcon.gameObject.activeSelf != showFlagIcon)
            flagIcon.gameObject.SetActive(showFlagIcon);

        shouldShowFlagThreatOutline = showFlagIcon && showFlagThreatOutline;
        if (!shouldShowFlagThreatOutline)
            ResetFlagThreatOutlineVisual();
        UpdateFlagThreatPulseRoutineState();

        if (flagText != null)
        {
            flagText.text = $"{clampedCurrent}";
            if (flagText.gameObject.activeSelf != showFlagText)
                flagText.gameObject.SetActive(showFlagText);
            flagText.enabled = showFlagText;
        }

        ApplyRallyTrafficLight(isRallyPoint, rallyOwnedByRallyOwner, rallyState);
    }

    public void ApplySectorBadge(bool showAIHUD, bool isOccupied, ConstructionSector sector, bool isFakeBuilding)
    {
        if (sectorBadge == null)
            AutoAssignReferences();
        if (sectorBadge == null)
            return;

        bool show = showAIHUD
            && !isOccupied
            && !isFakeBuilding
            && sector != ConstructionSector.None
            && !ConstructionSectorHelper.IsBase(sector);
        if (sectorBadge.gameObject.activeSelf != show)
            sectorBadge.gameObject.SetActive(show);

        if (show)
        {
            string name = sector.ToString();
            sectorBadge.text = name.Length > 0 ? name[0].ToString().ToUpper() : string.Empty;
        }
    }

    public void ApplyRallyTrafficLight(bool isRallyPoint, bool ownedByRallyOwner, AIRallyAssemblyState rallyState)
    {
        if (rallyTrafficImage == null)
            AutoAssignReferences();
        if (rallyTrafficImage == null)
            return;

        bool show = isRallyPoint;
        if (rallyTrafficImage.gameObject.activeSelf != show)
            rallyTrafficImage.gameObject.SetActive(show);
        rallyTrafficImage.enabled = show;
        if (!show)
            return;

        Sprite target = ResolveRallyTrafficSprite(ownedByRallyOwner, rallyState);
        if (target != null)
            rallyTrafficImage.sprite = target;
    }

    private void OnDisable()
    {
        StopFlagThreatPulseRoutine();
        ResetFlagThreatOutlineVisual();
    }

    private void AutoAssignReferences()
    {
        Transform explicitCaptureBar = FindChildRecursive(transform, "capture_bar")
            ?? FindChildRecursive(transform, "captured_container")
            ?? FindChildRecursive(transform, "capture_container");
        Transform explicitCaptureFill = FindChildRecursive(transform, "capture")
            ?? FindChildRecursive(transform, "captured");
        Transform explicitCaptureText = FindChildRecursive(transform, "capture_text")
            ?? FindChildRecursive(transform, "captured_text");

        if (explicitCaptureBar != null)
            capturedContainer = explicitCaptureBar;
        else if (!IsChildOfThisHud(capturedContainer))
            capturedContainer = FindChildRecursive(transform, "captured_container") ?? FindChildRecursive(transform, "capture_container");

        if (!IsChildOfThisHud(capturedBarImage != null ? capturedBarImage.transform : null))
        {
            if (explicitCaptureBar != null)
                capturedBarImage = explicitCaptureBar.GetComponent<Image>();
            if (capturedBarImage == null && capturedContainer != null)
                capturedBarImage = capturedContainer.GetComponent<Image>();
        }

        if (!IsChildOfThisHud(capturedBarRenderer != null ? capturedBarRenderer.transform : null))
        {
            if (explicitCaptureBar != null)
                capturedBarRenderer = explicitCaptureBar.GetComponent<SpriteRenderer>();
            if (capturedBarRenderer == null && capturedContainer != null)
                capturedBarRenderer = capturedContainer.GetComponent<SpriteRenderer>();
        }

        if (explicitCaptureFill != null)
        {
            capturedFillImage = explicitCaptureFill.GetComponent<Image>();
            capturedFillRenderer = explicitCaptureFill.GetComponent<SpriteRenderer>();
        }

        if (!IsChildOfThisHud(capturedFillImage != null ? capturedFillImage.transform : null))
        {
            Transform captured = FindChildRecursive(transform, "captured") ?? FindChildRecursive(transform, "capture");
            if (captured == null && capturedContainer != null)
                captured = capturedContainer;
            if (captured != null)
                capturedFillImage = captured.GetComponent<Image>();
            if (capturedFillImage == null && capturedContainer != null)
                capturedFillImage = capturedContainer.GetComponentInChildren<Image>(includeInactive: true);
        }

        if (!IsChildOfThisHud(capturedFillRenderer != null ? capturedFillRenderer.transform : null))
        {
            Transform captured = FindChildRecursive(transform, "captured") ?? FindChildRecursive(transform, "capture");
            if (captured == null && capturedContainer != null)
                captured = capturedContainer;
            if (captured != null)
                capturedFillRenderer = captured.GetComponent<SpriteRenderer>();
            if (capturedFillRenderer == null && capturedContainer != null)
                capturedFillRenderer = capturedContainer.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
        }

        if (explicitCaptureText != null)
            capturedText = explicitCaptureText.GetComponent<TMP_Text>();

        if (!IsChildOfThisHud(capturedText != null ? capturedText.transform : null))
        {
            Transform text = FindChildRecursive(transform, "captured_text") ?? FindChildRecursive(transform, "capture_text");
            if (text != null)
                capturedText = text.GetComponent<TMP_Text>();
            if (capturedText == null && capturedContainer != null)
                capturedText = capturedContainer.GetComponentInChildren<TMP_Text>(includeInactive: true);
        }

        if (!IsChildOfThisHud(flagIcon))
            flagIcon = FindChildRecursive(transform, "flag_icon");

        if (!IsChildOfThisHud(flagText != null ? flagText.transform : null))
        {
            Transform flagTextTransform = FindChildRecursive(transform, "flag_text");
            if (flagTextTransform != null)
                flagText = flagTextTransform.GetComponent<TMP_Text>();
        }

        if (!IsChildOfThisHud(sectorBadge != null ? sectorBadge.transform : null))
        {
            Transform badgeTransform = FindChildRecursive(transform, "text_badge");
            if (badgeTransform != null)
                sectorBadge = badgeTransform.GetComponent<TMP_Text>();
        }
        if (sectorBadge == null && Application.isPlaying)
            sectorBadge = CreateRuntimeSectorBadge();

        if (!IsChildOfThisHud(rallyTrafficImage != null ? rallyTrafficImage.transform : null))
        {
            Transform rallyTransform = FindChildRecursive(transform, "rally");
            if (rallyTransform != null)
                rallyTrafficImage = rallyTransform.GetComponent<Image>();
        }

        if (rallyTrafficImage == null && Application.isPlaying)
            rallyTrafficImage = CreateRuntimeRallyTrafficImage();

        if (rallyTrafficImage != null && rallyOffSprite == null)
            rallyOffSprite = rallyTrafficImage.sprite;

        // Auto-cura referências baked quebradas: instâncias antigas (e o próprio prefab,
        // antes da textura virar Single mode) guardam um sub-sprite que não existe mais,
        // que o Unity resolve como null e renderiza como um quadrado branco. Se o sprite
        // serializado de "off" é válido, restaura-o na Image.
        if (rallyTrafficImage != null && rallyTrafficImage.sprite == null && rallyOffSprite != null)
            rallyTrafficImage.sprite = rallyOffSprite;
    }

    private Sprite ResolveRallyTrafficSprite(bool ownedByRallyOwner, AIRallyAssemblyState rallyState)
    {
        if (!ownedByRallyOwner)
            return rallyOffSprite != null ? rallyOffSprite : (rallyTrafficImage != null ? rallyTrafficImage.sprite : null);

        switch (rallyState)
        {
            case AIRallyAssemblyState.GoGreen:
                return rallyGreenSprite != null ? rallyGreenSprite : rallyOffSprite;
            case AIRallyAssemblyState.Assembling:
            case AIRallyAssemblyState.Ready:
                return rallyYellowSprite != null ? rallyYellowSprite : rallyOffSprite;
            case AIRallyAssemblyState.WaitHold:
                return rallyRedSprite != null ? rallyRedSprite : rallyOffSprite;
            default:
                return rallyRedSprite != null ? rallyRedSprite : rallyOffSprite;
        }
    }

    private Image CreateRuntimeRallyTrafficImage()
    {
        Transform parent = FindChildRecursive(transform, "Canvas") ?? transform;
        GameObject rallyGo = new GameObject("rally", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rallyGo.transform.SetParent(parent, false);

        RectTransform rect = rallyGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(36.2f, 17.3f);
        rect.sizeDelta = new Vector2(12f, 24f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = new Vector3(1f, 1f, 10f);

        Image image = rallyGo.GetComponent<Image>();
        image.sprite = rallyOffSprite;
        image.raycastTarget = false;
        image.enabled = false;
        rallyGo.SetActive(false);
        return image;
    }

    private TMP_Text CreateRuntimeSectorBadge()
    {
        GameObject badgeGo = new GameObject("text_badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        badgeGo.transform.SetParent(transform, false);

        RectTransform rect = badgeGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0.4f, -31.7f);
        rect.sizeDelta = new Vector2(50f, 15f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        TextMeshProUGUI text = badgeGo.GetComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 12f;
        text.raycastTarget = false;
        text.enabled = false;
        badgeGo.SetActive(false);
        return text;
    }

    private void EnsureFlagThreatOutline()
    {
        if (flagIcon == null)
        {
            flagThreatOutline = null;
            return;
        }

        if (flagThreatOutline == null || flagThreatOutline.transform != flagIcon)
            flagThreatOutline = flagIcon.GetComponent<Outline>();

        if (flagThreatOutline == null)
            flagThreatOutline = flagIcon.gameObject.AddComponent<Outline>();

        flagThreatOutline.effectDistance = flagThreatOutlineDistance;
        flagThreatOutline.useGraphicAlpha = true;
        if (!shouldShowFlagThreatOutline)
            flagThreatOutline.enabled = false;
    }

    private void RefreshFlagThreatOutlinePulse()
    {
        if (!shouldShowFlagThreatOutline || flagThreatOutline == null || flagIcon == null || !flagIcon.gameObject.activeInHierarchy)
        {
            ResetFlagThreatOutlineVisual();
            return;
        }

        if (flagThreatPulseDuration <= 0.0001f)
            flagThreatPulseDuration = UnityEngine.Random.Range(flagThreatPulseMinDuration, flagThreatPulseMaxDuration);

        flagThreatPulseTimer += Time.deltaTime;
        while (flagThreatPulseTimer >= flagThreatPulseDuration)
        {
            flagThreatPulseTimer -= flagThreatPulseDuration;
            flagThreatPulseDuration = UnityEngine.Random.Range(flagThreatPulseMinDuration, flagThreatPulseMaxDuration);
        }

        float t = Mathf.PingPong(flagThreatPulseTimer / Mathf.Max(0.001f, flagThreatPulseDuration), 1f);
        float alpha = Mathf.Lerp(flagThreatOutlineAlphaMin, flagThreatOutlineAlphaMax, t);
        Color c = flagThreatOutlineColor;
        c.a = alpha;

        flagThreatOutline.effectColor = c;
        if (!flagThreatOutline.enabled)
            flagThreatOutline.enabled = true;
    }

    private void UpdateFlagThreatPulseRoutineState()
    {
        if (shouldShowFlagThreatOutline)
        {
            if (flagThreatPulseRoutine == null)
                flagThreatPulseRoutine = StartCoroutine(FlagThreatPulseRoutine());
            return;
        }

        StopFlagThreatPulseRoutine();
    }

    private void StopFlagThreatPulseRoutine()
    {
        if (flagThreatPulseRoutine == null)
            return;

        StopCoroutine(flagThreatPulseRoutine);
        flagThreatPulseRoutine = null;
    }

    private System.Collections.IEnumerator FlagThreatPulseRoutine()
    {
        while (shouldShowFlagThreatOutline && isActiveAndEnabled)
        {
            RefreshFlagThreatOutlinePulse();
            yield return null;
        }

        flagThreatPulseRoutine = null;
    }

    private void ResetFlagThreatOutlineVisual()
    {
        flagThreatPulseTimer = 0f;
        flagThreatPulseDuration = Mathf.Max(0.01f, UnityEngine.Random.Range(flagThreatPulseMinDuration, flagThreatPulseMaxDuration));

        if (flagThreatOutline == null)
            return;

        flagThreatOutline.enabled = false;
    }

    private void EnsureCaptureVisualOrder()
    {
        RectTransform bar = capturedContainer as RectTransform;
        RectTransform fill = capturedFillImage != null ? capturedFillImage.rectTransform : null;
        RectTransform textRect = capturedText != null ? capturedText.rectTransform : null;
        if (bar == null || fill == null || textRect == null)
            return;

        Transform parent = bar.parent;
        if (parent == null || fill.parent != parent || textRect.parent != parent)
            return;

        int first = Mathf.Min(bar.GetSiblingIndex(), Mathf.Min(fill.GetSiblingIndex(), textRect.GetSiblingIndex()));
        bar.SetSiblingIndex(first);
        fill.SetSiblingIndex(first + 1);
        textRect.SetSiblingIndex(first + 2);
    }

    private bool IsChildOfThisHud(Transform candidate)
    {
        return candidate != null && candidate.IsChildOf(transform);
    }

    private void ApplySorting()
    {
        if (!applyHudSorting)
            return;

        int layerId = SortingLayer.NameToID(hudSortingLayerName);
        if (layerId == 0 && hudSortingLayerName != "Default")
            layerId = SortingLayer.NameToID("Default");

        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingLayerID = layerId;
            canvas.sortingOrder = hudSortingOrder;
        }
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private Color GetCapturedColorByRatio(float ratio, TeamId ownerTeam)
    {
        if (capturedTintWithTeamColor)
            return TeamUtils.GetColor(ownerTeam);

        if (ratio <= 0.25f)
            return captured25PercentColor;

        if (ratio <= 0.5f)
            return captured50PercentColor;

        return capturedDefaultColor;
    }
}
