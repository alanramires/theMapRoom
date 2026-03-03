using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelHelperController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CursorController cursorController;
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private GameObject panelHelper;
    [SerializeField] private TMP_Text helperTitle;
    [SerializeField] private TMP_Text helperTxt;

    [Header("Dock")]
    [SerializeField] [Range(0f, 300f)] private float dockEnterProximityPixels = 80f;
    [SerializeField] [Range(0f, 500f)] private float dockExitProximityPixels = 140f;
    [SerializeField] private Vector2 dockedAnchoredPosition = new Vector2(18f, 0f);

    private string lastTitle = string.Empty;
    private string lastBody = string.Empty;
    private bool lastPanelVisible;
    private CanvasGroup selfPanelCanvasGroup;
    private RectTransform helperRect;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalPivot;
    private Vector2 originalAnchoredPosition;
    private bool layoutCached;
    private bool isDockedCenterLeft;
    private bool hasLastUndockedScreenRect;
    private Rect lastUndockedScreenRect;
    private Color lastHelperTxtColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);

    private void Awake()
    {
        TryAutoAssignReferences();
        CacheOriginalLayoutIfNeeded();
        HideAll(force: true);
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
        CacheOriginalLayoutIfNeeded();
        HideAll(force: true);
    }
#endif

    private void TryAutoAssignReferences()
    {
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (panelHelper == null)
            panelHelper = FindNamedObject("panel_helper") ?? FindNamedObject("Panel_helper") ?? FindNamedObject("Panel_Helper");
        if (panelHelper == null)
            panelHelper = gameObject;

        if (helperTitle == null)
            helperTitle = FindNamedTmpText("helper_title");
        if (helperTxt == null)
            helperTxt = FindNamedTmpText("helper_txt");

        if (helperRect == null && panelHelper != null)
            helperRect = panelHelper.GetComponent<RectTransform>();
    }

    private void Refresh(bool force)
    {
        if (turnStateManager == null || !turnStateManager.TryBuildHelperPanel(out string title, out string body))
        {
            HideAll(force);
            return;
        }

        SetVisible(panelVisible: true, title: title, body: body, force: force);
        RefreshDockByCursorProximity();
    }

    private void HideAll(bool force)
    {
        if (isDockedCenterLeft)
            RestoreOriginalLayout();
        hasLastUndockedScreenRect = false;
        SetVisible(panelVisible: false, title: string.Empty, body: string.Empty, force: force);
    }

    private void SetVisible(bool panelVisible, string title, string body, bool force)
    {
        if (force || panelVisible != lastPanelVisible)
            SetPanelVisible(panelVisible);

        if (helperTitle != null)
        {
            if (force || lastTitle != title)
                helperTitle.text = title ?? string.Empty;
            helperTitle.enabled = panelVisible;
        }

        if (helperTxt != null)
        {
            if (force || lastBody != body)
                helperTxt.text = body ?? string.Empty;
            Color txtColor = ResolveActiveTeamColor();
            if (force || txtColor != lastHelperTxtColor)
            {
                helperTxt.color = txtColor;
                lastHelperTxtColor = txtColor;
            }
            helperTxt.enabled = panelVisible;
        }

        lastPanelVisible = panelVisible;
        lastTitle = title ?? string.Empty;
        lastBody = body ?? string.Empty;
    }

    private Color ResolveActiveTeamColor()
    {
        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        return TeamUtils.GetColor(activeTeam);
    }

    private void RefreshDockByCursorProximity()
    {
        if (helperRect == null || cursorController == null || panelHelper == null)
            return;
        if (!panelHelper.activeInHierarchy)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 cursorWorld = cursorController.transform.position;
        Vector3 cursorScreen = cam.WorldToScreenPoint(cursorWorld);
        if (cursorScreen.z < 0f)
            return;

        if (!isDockedCenterLeft)
        {
            Rect panelScreenRect = GetScreenRect(helperRect);
            if (panelScreenRect.width <= 0f || panelScreenRect.height <= 0f)
                return;

            lastUndockedScreenRect = panelScreenRect;
            hasLastUndockedScreenRect = true;

            if (IsNearRect(panelScreenRect, cursorScreen, dockEnterProximityPixels))
                ApplyDockCenterLeft();
            return;
        }

        if (!hasLastUndockedScreenRect)
            return;

        if (!IsNearRect(lastUndockedScreenRect, cursorScreen, dockExitProximityPixels))
            RestoreOriginalLayout();
    }

    private static bool IsNearRect(Rect rect, Vector3 screenPoint, float marginPixels)
    {
        float margin = Mathf.Max(0f, marginPixels);
        Rect expanded = new Rect(
            rect.xMin - margin,
            rect.yMin - margin,
            rect.width + margin * 2f,
            rect.height + margin * 2f);

        return expanded.Contains(new Vector2(screenPoint.x, screenPoint.y));
    }

    private void CacheOriginalLayoutIfNeeded()
    {
        if (layoutCached || helperRect == null)
            return;

        originalAnchorMin = helperRect.anchorMin;
        originalAnchorMax = helperRect.anchorMax;
        originalPivot = helperRect.pivot;
        originalAnchoredPosition = helperRect.anchoredPosition;
        layoutCached = true;
    }

    private void ApplyDockCenterLeft()
    {
        if (helperRect == null)
            return;

        CacheOriginalLayoutIfNeeded();
        helperRect.anchorMin = new Vector2(0f, 0.5f);
        helperRect.anchorMax = new Vector2(0f, 0.5f);
        helperRect.pivot = new Vector2(0f, 0.5f);
        helperRect.anchoredPosition = dockedAnchoredPosition;
        isDockedCenterLeft = true;
    }

    private void RestoreOriginalLayout()
    {
        if (helperRect == null || !layoutCached)
            return;

        helperRect.anchorMin = originalAnchorMin;
        helperRect.anchorMax = originalAnchorMax;
        helperRect.pivot = originalPivot;
        helperRect.anchoredPosition = originalAnchoredPosition;
        isDockedCenterLeft = false;
    }

    private static Rect GetScreenRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < 4; i++)
        {
            Vector3 c = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.x > maxX) maxX = c.x;
            if (c.y > maxY) maxY = c.y;
        }

        if (!float.IsFinite(minX) || !float.IsFinite(minY) || !float.IsFinite(maxX) || !float.IsFinite(maxY))
            return new Rect();

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelHelper == null)
            return;

        if (panelHelper == gameObject)
        {
            if (selfPanelCanvasGroup == null)
                selfPanelCanvasGroup = panelHelper.GetComponent<CanvasGroup>();
            if (selfPanelCanvasGroup == null)
                selfPanelCanvasGroup = panelHelper.AddComponent<CanvasGroup>();

            selfPanelCanvasGroup.alpha = visible ? 1f : 0f;
            selfPanelCanvasGroup.interactable = false;
            selfPanelCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (panelHelper.activeSelf != visible)
            panelHelper.SetActive(visible);
    }

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
