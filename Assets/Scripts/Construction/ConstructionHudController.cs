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
    [SerializeField] private Image capturedFillImage;
    [SerializeField] private SpriteRenderer capturedFillRenderer;
    [SerializeField] private TMP_Text capturedText;
    [SerializeField] private bool hideWhenFull = false;
    [SerializeField] private bool capturedTintWithTeamColor = false;
    [SerializeField] private Color capturedDefaultColor = new Color(0.8235295f, 0.4117647f, 0.1176471f, 1f);
    [SerializeField] private Color captured50PercentColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color captured25PercentColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Flames")]
    [SerializeField] private Transform fire1;
    [SerializeField] private Transform fire2;
    [SerializeField] private Transform fire3;
    [SerializeField] [Range(0f, 1f)] private float flamesWhenNotCapturableAlpha = 0f;
    [SerializeField] [Range(0.1f, 8f)] private float flamesSpeed = 2.2f;
    [SerializeField] [Range(0f, 0.25f)] private float flamesScaleAmplitude = 0.08f;
    [SerializeField] [Range(0f, 0.3f)] private float flamesPulseAmplitude = 0.12f;

    [Header("Sorting")]
    [SerializeField] private bool applyHudSorting = true;
    [SerializeField] private string hudSortingLayerName = "SFX";
    [SerializeField] private int hudSortingOrder = 60;

    private float flameTime;
    private Transform[] flameTransforms;
    private Vector3[] flameBaseScales;
    private SpriteRenderer[] flameSpriteRenderers;
    private Image[] flameImages;
    private float[] flameBaseAlphas;
    private bool flameCacheInitialized;

    private void Awake()
    {
        AutoAssignReferences();
        EnsureFlameCaches();
        ApplySorting();
    }

    private void LateUpdate()
    {
        AnimateFlames();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignReferences();
        EnsureFlameCaches();
        ApplySorting();
    }
#endif

    public void RefreshBindings()
    {
        AutoAssignReferences();
        EnsureFlameCaches();
        ApplySorting();
    }

    public void Apply(int currentCapture, int maxCapture, bool isCapturable, TeamId ownerTeam, bool hideCaptureBarBecauseOccupied)
    {
        int safeMax = Mathf.Max(0, maxCapture);
        int clampedCurrent = Mathf.Clamp(currentCapture, 0, safeMax);
        float ratio = safeMax > 0 ? Mathf.Clamp01((float)clampedCurrent / safeMax) : 0f;

        bool showContainer = isCapturable && !hideCaptureBarBecauseOccupied && (!hideWhenFull || clampedCurrent < safeMax);
        if (capturedContainer != null && capturedContainer.gameObject.activeSelf != showContainer)
            capturedContainer.gameObject.SetActive(showContainer);

        if (capturedText != null)
        {
            capturedText.text = $"{clampedCurrent}/{safeMax}";
            capturedText.enabled = showContainer;
        }

        Color capturedColor = GetCapturedColorByRatio(ratio, ownerTeam);

        if (capturedFillImage != null)
        {
            capturedFillImage.fillAmount = ratio;
            capturedFillImage.color = capturedColor;
            capturedFillImage.enabled = showContainer;
        }

        if (capturedFillRenderer != null)
        {
            Vector3 s = capturedFillRenderer.transform.localScale;
            s.x = Mathf.Max(0.001f, ratio);
            capturedFillRenderer.transform.localScale = s;
            capturedFillRenderer.color = capturedColor;
            capturedFillRenderer.enabled = showContainer;
        }

        float alpha1 = 0f;
        float alpha2 = 0f;
        float alpha3 = 0f;
        if (isCapturable && safeMax > 0 && clampedCurrent < safeMax)
        {
            // Qualquer discrepancia de captura ja exibe pelo menos 1 fogo.
            alpha1 = 1f;
            if (ratio < 0.50f) alpha2 = 1f;
            if (ratio < 0.25f) alpha3 = 1f;
        }
        else if (!isCapturable)
        {
            float fallback = Mathf.Clamp01(flamesWhenNotCapturableAlpha);
            alpha1 = fallback;
            alpha2 = fallback;
            alpha3 = fallback;
        }

        ApplyFlamesBaseAlpha(alpha1, alpha2, alpha3);
    }

    private void AnimateFlames()
    {
        EnsureFlameCaches();
        if (flameTransforms == null || flameTransforms.Length == 0)
            return;

        flameTime += Time.deltaTime * Mathf.Max(0.1f, flamesSpeed);

        for (int i = 0; i < flameTransforms.Length; i++)
        {
            Transform flame = flameTransforms[i];
            if (flame == null)
                continue;

            float phase = flameTime + (i * 1.1f);
            float scalePulse = 1f + (Mathf.Sin(phase) * flamesScaleAmplitude);
            Vector3 baseScale = i < flameBaseScales.Length ? flameBaseScales[i] : Vector3.one;
            Vector3 scaled = baseScale * scalePulse;
            if (!IsFinite(scaled))
                scaled = SafeScale(baseScale);
            flame.localScale = scaled;

            float alphaPulse = 1f + (Mathf.Sin(phase * 1.3f) * flamesPulseAmplitude);
            alphaPulse = Mathf.Clamp(alphaPulse, 0f, 1.2f);

            if (i < flameSpriteRenderers.Length && flameSpriteRenderers[i] != null)
            {
                Color c = flameSpriteRenderers[i].color;
                float baseAlpha = (flameBaseAlphas != null && i < flameBaseAlphas.Length) ? flameBaseAlphas[i] : c.a;
                c.a = Mathf.Clamp01(baseAlpha * alphaPulse);
                flameSpriteRenderers[i].color = c;
            }

            if (i < flameImages.Length && flameImages[i] != null)
            {
                Color c = flameImages[i].color;
                float baseAlpha = (flameBaseAlphas != null && i < flameBaseAlphas.Length) ? flameBaseAlphas[i] : c.a;
                c.a = Mathf.Clamp01(baseAlpha * alphaPulse);
                flameImages[i].color = c;
            }
        }
    }

    private void ApplyFlamesBaseAlpha(float alpha1, float alpha2, float alpha3)
    {
        EnsureFlameCaches();
        float[] alphas = { Mathf.Clamp01(alpha1), Mathf.Clamp01(alpha2), Mathf.Clamp01(alpha3) };
        for (int i = 0; i < flameSpriteRenderers.Length; i++)
        {
            SpriteRenderer r = flameSpriteRenderers[i];
            if (r == null)
                continue;
            Color c = r.color;
            c.a = i < alphas.Length ? alphas[i] : 0f;
            r.color = c;
            if (flameBaseAlphas != null && i < flameBaseAlphas.Length)
                flameBaseAlphas[i] = c.a;
        }

        for (int i = 0; i < flameImages.Length; i++)
        {
            Image img = flameImages[i];
            if (img == null)
                continue;
            Color c = img.color;
            c.a = i < alphas.Length ? alphas[i] : 0f;
            img.color = c;
            if (flameBaseAlphas != null && i < flameBaseAlphas.Length)
                flameBaseAlphas[i] = c.a;
        }
    }

    private void EnsureFlameCaches()
    {
        Transform f1 = fire1;
        Transform f2 = fire2;
        Transform f3 = fire3;
        bool sameRefs =
            flameTransforms != null &&
            flameTransforms.Length == 3 &&
            flameTransforms[0] == f1 &&
            flameTransforms[1] == f2 &&
            flameTransforms[2] == f3;
        if (flameCacheInitialized && sameRefs)
            return;

        flameTransforms = new[] { f1, f2, f3 };
        flameBaseScales = new Vector3[3];
        flameSpriteRenderers = new SpriteRenderer[3];
        flameImages = new Image[3];
        flameBaseAlphas = new float[3];

        for (int i = 0; i < flameTransforms.Length; i++)
        {
            Transform flame = flameTransforms[i];
            if (flame == null)
            {
                flameBaseScales[i] = Vector3.one;
                continue;
            }

            flameBaseScales[i] = SafeScale(flame.localScale);
            flameSpriteRenderers[i] = flame.GetComponent<SpriteRenderer>();
            flameImages[i] = flame.GetComponent<Image>();
            float spriteAlpha = flameSpriteRenderers[i] != null ? flameSpriteRenderers[i].color.a : 1f;
            float imageAlpha = flameImages[i] != null ? flameImages[i].color.a : 1f;
            flameBaseAlphas[i] = Mathf.Clamp01(Mathf.Max(spriteAlpha, imageAlpha));
        }

        flameCacheInitialized = true;
    }

    private void AutoAssignReferences()
    {
        if (!IsChildOfThisHud(capturedContainer))
            capturedContainer = FindChildRecursive(transform, "captured_container") ?? FindChildRecursive(transform, "capture_container");

        if (!IsChildOfThisHud(capturedFillImage != null ? capturedFillImage.transform : null))
        {
            Transform captured = FindChildRecursive(transform, "captured") ?? FindChildRecursive(transform, "capture");
            if (captured != null)
                capturedFillImage = captured.GetComponent<Image>();
        }

        if (!IsChildOfThisHud(capturedFillRenderer != null ? capturedFillRenderer.transform : null))
        {
            Transform captured = FindChildRecursive(transform, "captured") ?? FindChildRecursive(transform, "capture");
            if (captured != null)
                capturedFillRenderer = captured.GetComponent<SpriteRenderer>();
        }

        if (!IsChildOfThisHud(capturedText != null ? capturedText.transform : null))
        {
            Transform text = FindChildRecursive(transform, "captured_text") ?? FindChildRecursive(transform, "capture_text");
            if (text != null)
                capturedText = text.GetComponent<TMP_Text>();
        }

        if (!IsChildOfThisHud(fire1))
            fire1 = FindChildRecursive(transform, "fire1");
        if (!IsChildOfThisHud(fire2))
            fire2 = FindChildRecursive(transform, "fire2");
        if (!IsChildOfThisHud(fire3))
            fire3 = FindChildRecursive(transform, "fire3");
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

    private static bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    private static Vector3 SafeScale(Vector3 candidate)
    {
        Vector3 v = candidate;
        if (!float.IsFinite(v.x) || Mathf.Abs(v.x) > 1000f) v.x = 1f;
        if (!float.IsFinite(v.y) || Mathf.Abs(v.y) > 1000f) v.y = 1f;
        if (!float.IsFinite(v.z) || Mathf.Abs(v.z) > 1000f) v.z = 1f;
        return v;
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
