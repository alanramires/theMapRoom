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

    [Header("Sorting")]
    [SerializeField] private bool applyHudSorting = true;
    [SerializeField] private string hudSortingLayerName = "SFX";
    [SerializeField] private int hudSortingOrder = 60;

    private Outline flagThreatOutline;
    private bool shouldShowFlagThreatOutline;
    private float flagThreatPulseTimer;
    private float flagThreatPulseDuration = 1f;

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

    private void Update()
    {
        RefreshFlagThreatOutlinePulse();
    }

    public void Apply(int currentCapture, int maxCapture, bool isCapturable, TeamId ownerTeam, bool hideCaptureBarBecauseOccupied, bool showFlagThreatOutline = false)
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

        if (flagText != null)
        {
            flagText.text = $"{clampedCurrent}";
            if (flagText.gameObject.activeSelf != showFlagText)
                flagText.gameObject.SetActive(showFlagText);
            flagText.enabled = showFlagText;
        }
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
