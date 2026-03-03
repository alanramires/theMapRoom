using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class UnitHudController : MonoBehaviour
{
    private static readonly Color ForcedFuelDefaultOrange = new Color(0.8235295f, 0.4117647f, 0.1176471f, 1f);

    [System.Serializable]
    public class PipGroup
    {
        public List<SpriteRenderer> worldPips = new List<SpriteRenderer>();
        public List<Image> uiPips = new List<Image>();
        public bool tintWithTeamColor = false;
        public Color activeColor = Color.white;
        public Color inactiveColor = new Color(1f, 1f, 1f, 0.2f);
    }

    [Header("HP")]
    [SerializeField] private Image hpStateImage;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Sprite heartSprite;
    [SerializeField] private Sprite halfHeartSprite;
    [SerializeField] private Sprite emptyHeartSprite;

    [Header("Ammo")]
    [SerializeField] private PipGroup ammoPips = new PipGroup();

    [Header("Fuel")]
    [SerializeField] private SpriteRenderer fuelFillRenderer;
    [SerializeField] private Image fuelFillImage;
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private bool fuelTintWithTeamColor = false;
    [SerializeField] private Color fuelDefaultColor = new Color(0.8235295f, 0.4117647f, 0.1176471f, 1f);
    [FormerlySerializedAs("fuel75PercentColor")]
    [SerializeField] private Color fuel50PercentColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color fuel25PercentColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float fuelMinScaleX = 0.05f;
    [SerializeField] private bool hideFuelWhenFull = false;

    [Header("Weapon Ammo UI")]
    [SerializeField] private Transform wContainer;
    [SerializeField] private SpriteRenderer wFillRenderer;
    [SerializeField] private Image wFillImage;
    [SerializeField] private TMP_Text wText;
    [SerializeField] private Transform w1Container;
    [SerializeField] private SpriteRenderer w1FillRenderer;
    [SerializeField] private Image w1FillImage;
    [SerializeField] private TMP_Text w1Text;
    [SerializeField] private Transform w2Container;
    [SerializeField] private SpriteRenderer w2FillRenderer;
    [SerializeField] private Image w2FillImage;
    [SerializeField] private TMP_Text w2Text;
    [SerializeField] private float weaponAmmoMinScaleX = 0f;

    [Header("Altitude")]
    [SerializeField] private SpriteRenderer altitudeRenderer;
    [SerializeField] private Image altitudeImage;
    [SerializeField] private Sprite altitudeHighSprite;
    [SerializeField] private Sprite altitudeLowSprite;
    [SerializeField] private Sprite altitudeSubmergedSprite;
    [SerializeField] private Transform transportIndicatorRoot;

    [Header("Sorting")]
    [SerializeField] private bool applyHudSorting = true;
    [SerializeField] private string hudSortingLayerName = "SFX";
    [SerializeField] private int hudSortingOrder = 50;
    [SerializeField] private bool followParentSpriteSortingLayer = true;
    [SerializeField] private int orderAboveParentSprite = 100;
    [SerializeField] private int worldHudOrderOffsetPerElement = 1;
    private UnitManager ownerUnit;
    private bool wBaseScaleCached;
    private bool w1BaseScaleCached;
    private bool w2BaseScaleCached;
    private Vector3 wBaseScale = Vector3.one;
    private Vector3 w1BaseScale = Vector3.one;
    private Vector3 w2BaseScale = Vector3.one;

    private void Awake()
    {
        EnforceFuelDefaultColor();
        AutoAssignCommonReferences();
        DisableLegacyLockVisuals();
        ApplySorting();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnforceFuelDefaultColor();
        AutoAssignCommonReferences();
        DisableLegacyLockVisuals();
        ApplySorting();
    }
#endif

    public void RefreshBindings()
    {
        EnforceFuelDefaultColor();
        AutoAssignCommonReferences();
        DisableLegacyLockVisuals();
        ApplySorting();
    }

    public void Apply(
        int currentHP,
        int maxHP,
        int currentAmmo,
        int maxAmmo,
        int currentFuel,
        int maxFuel,
        Color teamColor,
        Domain domain,
        HeightLevel heightLevel,
        bool showTransportIndicator)
    {
        if (altitudeImage == null && altitudeRenderer == null)
            AutoAssignCommonReferences();

        RefreshHp(currentHP, maxHP);
        RefreshPips(ammoPips, currentAmmo, maxAmmo, teamColor);
        RefreshFuel(currentFuel, maxFuel, teamColor);
        RefreshWeaponAmmoVisuals();
        RefreshAltitude(domain, heightLevel);
        RefreshTransportIndicator(showTransportIndicator);
    }

    private static void RefreshPips(PipGroup group, int current, int max, Color teamColor)
    {
        if (group == null)
            return;

        int worldCount = group.worldPips != null ? group.worldPips.Count : 0;
        int uiCount = group.uiPips != null ? group.uiPips.Count : 0;
        int slotCount = Mathf.Max(worldCount, uiCount);
        if (slotCount == 0)
            return;

        int safeMax = Mathf.Max(1, max);
        float ratio = Mathf.Clamp01((float)Mathf.Max(0, current) / safeMax);
        int activeCount = Mathf.RoundToInt(ratio * slotCount);

        for (int i = 0; i < worldCount; i++)
        {
            SpriteRenderer pip = group.worldPips[i];
            if (pip == null)
                continue;

            bool active = i < activeCount;
            pip.enabled = active || group.inactiveColor.a > 0.001f;
            if (!pip.enabled)
                continue;

            if (active)
                pip.color = group.tintWithTeamColor ? teamColor : group.activeColor;
            else
                pip.color = group.inactiveColor;
        }

        for (int i = 0; i < uiCount; i++)
        {
            Image pip = group.uiPips[i];
            if (pip == null)
                continue;

            bool active = i < activeCount;
            pip.enabled = active || group.inactiveColor.a > 0.001f;
            if (!pip.enabled)
                continue;

            if (active)
                pip.color = group.tintWithTeamColor ? teamColor : group.activeColor;
            else
                pip.color = group.inactiveColor;
        }
    }

    private void RefreshFuel(int currentFuel, int maxFuel, Color teamColor)
    {
        int safeMax = Mathf.Max(1, maxFuel);
        float ratio = Mathf.Clamp01((float)Mathf.Max(0, currentFuel) / safeMax);
        Color c = GetFuelColorByRatio(ratio, teamColor);

        if (fuelText != null)
            fuelText.text = $"{Mathf.Max(0, currentFuel)}/{safeMax}";

        if (fuelFillImage != null)
        {
            fuelFillImage.fillAmount = ratio;
            fuelFillImage.color = c;
            fuelFillImage.enabled = !(hideFuelWhenFull && ratio >= 0.999f);
        }

        if (fuelFillRenderer == null)
            return;

        Vector3 scale = fuelFillRenderer.transform.localScale;
        scale.x = Mathf.Max(fuelMinScaleX, ratio);
        fuelFillRenderer.transform.localScale = scale;

        fuelFillRenderer.color = c;
        fuelFillRenderer.enabled = !(hideFuelWhenFull && ratio >= 0.999f);
    }

    private Color GetFuelColorByRatio(float ratio, Color teamColor)
    {
        if (fuelTintWithTeamColor)
            return teamColor;

        if (ratio <= 0.25f)
            return fuel25PercentColor;

        if (ratio <= 0.5f)
            return fuel50PercentColor;

        return fuelDefaultColor;
    }

    private void EnforceFuelDefaultColor()
    {
        fuelDefaultColor = ForcedFuelDefaultOrange;
    }

    private void RefreshHp(int currentHP, int maxHP)
    {
        if (hpText != null)
            hpText.text = $"{Mathf.Max(0, currentHP)}";

        if (hpStateImage == null)
            return;

        Sprite target;
        if (currentHP >= 5)
            target = halfHeartSprite;
        else if (currentHP >= 1)
            target = emptyHeartSprite;
        else
            target = emptyHeartSprite;

        if (currentHP >= 9)
            target = heartSprite;

        // Fallbacks if alguma sprite nao foi setada no inspector.
        if (target == null)
            target = heartSprite != null ? heartSprite : hpStateImage.sprite;

        hpStateImage.sprite = target;
        hpStateImage.enabled = hpStateImage.sprite != null;
    }

    private void AutoAssignCommonReferences()
    {
        if (applyHudSorting)
        {
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingLayerName = hudSortingLayerName;
                canvas.sortingOrder = hudSortingOrder;
            }
        }

        if (hpStateImage == null)
        {
            Transform hpT = FindChildRecursive(transform, "hp");
            if (hpT != null)
                hpStateImage = hpT.GetComponent<Image>();
        }

        if (hpText == null)
        {
            Transform hpTextT = FindChildRecursive(transform, "hp_text");
            if (hpTextT != null)
                hpText = hpTextT.GetComponent<TMP_Text>();
        }

        if (fuelFillImage == null)
        {
            Transform fuelT = FindChildRecursive(transform, "fuel");
            if (fuelT != null)
                fuelFillImage = fuelT.GetComponent<Image>();
        }

        if (fuelFillRenderer == null)
        {
            Transform fuelT = FindChildRecursive(transform, "fuel");
            if (fuelT != null)
                fuelFillRenderer = fuelT.GetComponent<SpriteRenderer>();
        }

        if (fuelText == null)
        {
            Transform fuelTextT = FindChildRecursive(transform, "fuel_text");
            if (fuelTextT != null)
                fuelText = fuelTextT.GetComponent<TMP_Text>();
        }

        if (wContainer == null)
            wContainer = FindChildRecursive(transform, "w_container");
        if (wFillRenderer == null)
        {
            Transform wFillT = FindChildRecursive(transform, "w");
            if (wFillT != null)
                wFillRenderer = wFillT.GetComponent<SpriteRenderer>();
        }
        if (wFillImage == null)
        {
            Transform wFillT = FindChildRecursive(transform, "w");
            if (wFillT != null)
                wFillImage = wFillT.GetComponent<Image>();
        }
        if (wText == null)
        {
            Transform wTextT = FindChildRecursive(transform, "w_text");
            if (wTextT != null)
                wText = wTextT.GetComponent<TMP_Text>();
        }

        if (w1Container == null)
            w1Container = FindChildRecursive(transform, "w1_container");
        if (w1FillRenderer == null)
        {
            Transform w1FillT = FindChildRecursive(transform, "w1");
            if (w1FillT != null)
                w1FillRenderer = w1FillT.GetComponent<SpriteRenderer>();
        }
        if (w1FillImage == null)
        {
            Transform w1FillT = FindChildRecursive(transform, "w1");
            if (w1FillT != null)
                w1FillImage = w1FillT.GetComponent<Image>();
        }
        if (w1Text == null)
        {
            Transform w1TextT = FindChildRecursive(transform, "w1_text");
            if (w1TextT != null)
                w1Text = w1TextT.GetComponent<TMP_Text>();
        }

        if (w2Container == null)
            w2Container = FindChildRecursive(transform, "w2_container");
        if (w2FillRenderer == null)
        {
            Transform w2FillT = FindChildRecursive(transform, "w2");
            if (w2FillT != null)
                w2FillRenderer = w2FillT.GetComponent<SpriteRenderer>();
        }
        if (w2FillImage == null)
        {
            Transform w2FillT = FindChildRecursive(transform, "w2");
            if (w2FillT != null)
                w2FillImage = w2FillT.GetComponent<Image>();
        }
        if (w2Text == null)
        {
            Transform w2TextT = FindChildRecursive(transform, "w2_text");
            if (w2TextT != null)
                w2Text = w2TextT.GetComponent<TMP_Text>();
        }

        Transform altitudeT = FindChildRecursive(transform, "altitude");
        if (altitudeT != null)
        {
            if (altitudeImage == null)
                altitudeImage = altitudeT.GetComponent<Image>();
            if (altitudeRenderer == null)
                altitudeRenderer = altitudeT.GetComponent<SpriteRenderer>();
        }

        if (transportIndicatorRoot == null)
        {
            transportIndicatorRoot = FindChildRecursive(transform, "transporte");
            if (transportIndicatorRoot == null)
                transportIndicatorRoot = FindChildRecursive(transform, "transport");
        }

        if (altitudeHighSprite == null)
            altitudeHighSprite = FindSpriteByName("high altitude");
        if (altitudeLowSprite == null)
            altitudeLowSprite = FindSpriteByName("low altitude");
        if (altitudeSubmergedSprite == null)
            altitudeSubmergedSprite = FindSpriteByName("submerged");

    }

    private void RefreshWeaponAmmoVisuals()
    {
        if (ownerUnit == null)
            ownerUnit = GetComponentInParent<UnitManager>();

        int weaponCount = GetConfiguredWeaponAmmoInfo(
            out int current0, out int max0,
            out int current1, out int max1);

        if (weaponCount <= 0)
        {
            SetWeaponSlotVisible(wContainer, wFillRenderer, wFillImage, wText, false);
            SetWeaponSlotVisible(w1Container, w1FillRenderer, w1FillImage, w1Text, false);
            SetWeaponSlotVisible(w2Container, w2FillRenderer, w2FillImage, w2Text, false);
            return;
        }

        if (weaponCount == 1)
        {
            SetWeaponSlotVisible(wContainer, wFillRenderer, wFillImage, wText, true);
            SetWeaponSlotVisible(w1Container, w1FillRenderer, w1FillImage, w1Text, false);
            SetWeaponSlotVisible(w2Container, w2FillRenderer, w2FillImage, w2Text, false);
            ApplyWeaponSlotAmmo(current0, max0, wFillRenderer, wFillImage, wText, ref wBaseScaleCached, ref wBaseScale);
            return;
        }

        SetWeaponSlotVisible(wContainer, wFillRenderer, wFillImage, wText, false);
        SetWeaponSlotVisible(w1Container, w1FillRenderer, w1FillImage, w1Text, true);
        SetWeaponSlotVisible(w2Container, w2FillRenderer, w2FillImage, w2Text, true);
        ApplyWeaponSlotAmmo(current0, max0, w1FillRenderer, w1FillImage, w1Text, ref w1BaseScaleCached, ref w1BaseScale);
        ApplyWeaponSlotAmmo(current1, max1, w2FillRenderer, w2FillImage, w2Text, ref w2BaseScaleCached, ref w2BaseScale);
    }

    private int GetConfiguredWeaponAmmoInfo(
        out int current0, out int max0,
        out int current1, out int max1)
    {
        current0 = 0;
        max0 = 0;
        current1 = 0;
        max1 = 0;

        if (ownerUnit == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> runtime = ownerUnit.GetEmbarkedWeapons();
        if (runtime == null || runtime.Count <= 0)
            return 0;

        List<UnitEmbarkedWeapon> baseline = null;
        if (ownerUnit.TryGetUnitData(out UnitData data) && data != null)
            baseline = data.embarkedWeapons;

        int maxEntries = Mathf.Max(runtime.Count, baseline != null ? baseline.Count : 0);
        int configured = 0;
        for (int i = 0; i < maxEntries; i++)
        {
            UnitEmbarkedWeapon runtimeWeapon = i < runtime.Count ? runtime[i] : null;
            UnitEmbarkedWeapon baselineWeapon = (baseline != null && i < baseline.Count) ? baseline[i] : null;
            bool hasWeapon = (runtimeWeapon != null && runtimeWeapon.weapon != null) ||
                             (baselineWeapon != null && baselineWeapon.weapon != null);
            if (!hasWeapon)
                continue;

            int current = runtimeWeapon != null ? Mathf.Max(0, runtimeWeapon.squadAmmunition) : 0;
            int max = baselineWeapon != null ? Mathf.Max(0, baselineWeapon.squadAmmunition) : current;
            max = Mathf.Max(max, current);

            if (configured == 0)
            {
                current0 = current;
                max0 = max;
            }
            else if (configured == 1)
            {
                current1 = current;
                max1 = max;
            }

            configured++;
            if (configured >= 2)
                break;
        }

        return configured;
    }

    private void SetWeaponSlotVisible(Transform container, SpriteRenderer fillRenderer, Image fillImage, TMP_Text text, bool visible)
    {
        if (container != null)
        {
            if (container.gameObject.activeSelf != visible)
                container.gameObject.SetActive(visible);
            return;
        }

        if (fillRenderer != null)
            fillRenderer.enabled = visible;
        if (fillImage != null)
            fillImage.enabled = visible;
        if (text != null)
            text.enabled = visible;
    }

    private void ApplyWeaponSlotAmmo(
        int currentAmmo,
        int maxAmmo,
        SpriteRenderer fillRenderer,
        Image fillImage,
        TMP_Text text,
        ref bool baseScaleCached,
        ref Vector3 baseScale)
    {
        int safeCurrent = Mathf.Max(0, currentAmmo);
        int safeMax = Mathf.Max(1, maxAmmo);
        float ratio = Mathf.Clamp01((float)safeCurrent / safeMax);

        if (text != null)
            text.text = $"{safeCurrent}";

        if (fillImage != null)
            fillImage.fillAmount = ratio;

        if (fillRenderer != null)
        {
            if (!baseScaleCached)
            {
                baseScale = fillRenderer.transform.localScale;
                baseScaleCached = true;
            }

            Vector3 scale = baseScale;
            float baseX = Mathf.Abs(baseScale.x) <= 0.0001f ? 1f : baseScale.x;
            scale.x = Mathf.Sign(baseX) * Mathf.Max(weaponAmmoMinScaleX, Mathf.Abs(baseX) * ratio);
            fillRenderer.transform.localScale = scale;
        }
    }

    private void RefreshAltitude(Domain domain, HeightLevel heightLevel)
    {
        bool shouldShow = true;
        Sprite target = null;

        if (heightLevel == HeightLevel.AirHigh)
            target = altitudeHighSprite;
        else if (heightLevel == HeightLevel.AirLow)
            target = altitudeLowSprite;
        else if (heightLevel == HeightLevel.Submerged || domain == Domain.Submarine)
            target = altitudeSubmergedSprite;
        else if (domain == Domain.Land && heightLevel == HeightLevel.Surface)
            shouldShow = false;
        else
            shouldShow = false;

        if (altitudeImage != null)
        {
            altitudeImage.sprite = target;
            altitudeImage.enabled = shouldShow && target != null;
        }

        if (altitudeRenderer != null)
        {
            altitudeRenderer.sprite = target;
            altitudeRenderer.enabled = shouldShow && target != null;
        }
    }

    private void RefreshTransportIndicator(bool show)
    {
        if (transportIndicatorRoot == null)
            return;

        if (transportIndicatorRoot.gameObject.activeSelf != show)
            transportIndicatorRoot.gameObject.SetActive(show);
    }

    private void DisableLegacyLockVisuals()
    {
        Transform lockT = FindChildRecursive(transform, "ActedLock");
        if (lockT == null)
            lockT = FindChildRecursive(transform, "Cadeado");

        if (lockT == null)
            return;

        SpriteRenderer lockRenderer = lockT.GetComponent<SpriteRenderer>();
        if (lockRenderer != null)
            lockRenderer.enabled = false;

        Image lockImage = lockT.GetComponent<Image>();
        if (lockImage != null)
            lockImage.enabled = false;

        if (lockT.gameObject.activeSelf)
            lockT.gameObject.SetActive(false);
    }

    private void ApplySorting()
    {
        if (!applyHudSorting)
            return;

        int order = hudSortingOrder;
        int layerId = SortingLayer.NameToID(hudSortingLayerName);
        if (layerId == 0 && hudSortingLayerName != "Default")
        {
            // Fallback to default if layer name is invalid.
            layerId = SortingLayer.NameToID("Default");
        }

        if (followParentSpriteSortingLayer)
        {
            SpriteRenderer parentSprite = GetComponentInParent<SpriteRenderer>();
            if (parentSprite != null)
            {
                layerId = parentSprite.sortingLayerID;
                int minAboveParent = parentSprite.sortingOrder + Mathf.Max(1, orderAboveParentSprite);
                order = Mathf.Max(order, minAboveParent);
            }
        }

        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingLayerID = layerId;
            canvas.sortingOrder = order;
        }

        if (ammoPips != null && ammoPips.worldPips != null)
        {
            for (int i = 0; i < ammoPips.worldPips.Count; i++)
            {
                ApplySortingToRenderer(ammoPips.worldPips[i], layerId, order);
                order += worldHudOrderOffsetPerElement;
            }
        }

        ApplySortingToRenderer(fuelFillRenderer, layerId, order);
    }

    private static void ApplySortingToRenderer(SpriteRenderer renderer, int sortingLayerId, int sortingOrder)
    {
        if (renderer == null)
            return;

        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;
        string target = string.IsNullOrWhiteSpace(childName) ? string.Empty : childName.Trim();
        if (target.Length == 0)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            string current = child != null && !string.IsNullOrWhiteSpace(child.name) ? child.name.Trim() : string.Empty;
            if (string.Equals(current, target, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, target);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static Sprite FindSpriteByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string normalized = name.Trim().ToLowerInvariant();
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null || string.IsNullOrWhiteSpace(sprite.name))
                continue;

            if (sprite.name.Trim().ToLowerInvariant() == normalized)
                return sprite;
        }

        return null;
    }
}
