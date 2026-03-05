using TMPro;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PanelHelperController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CursorController cursorController;
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private HelperDatabase helperDatabase;
    [SerializeField] private GameObject panelHelper;
    [SerializeField] private TMP_Text helperTitle;
    [SerializeField] private TMP_Text helperTxt;

    [Header("Dock")]
    [SerializeField] [Range(0f, 300f)] private float dockEnterProximityPixels = 80f;
    [SerializeField] [Range(0f, 500f)] private float dockExitProximityPixels = 140f;
    [SerializeField] private Vector2 dockedAnchoredPosition = new Vector2(18f, 0f);
    [Header("Layout")]
    [SerializeField] private bool autoExpandHeight = true;
    [SerializeField] [Range(0f, 2000f)] private float minPanelHeight = 0f;
    [SerializeField] [Range(100f, 4000f)] private float maxPanelHeight = 1200f;
    [SerializeField] [Range(0f, 300f)] private float contentVerticalPadding = 24f;

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
    private float cachedBasePanelHeight = -1f;

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

#if UNITY_EDITOR
        if (helperDatabase == null)
            helperDatabase = FindFirstAssetEditor<HelperDatabase>();
#endif
    }

    private void Refresh(bool force)
    {
        if (turnStateManager == null || !turnStateManager.TryBuildHelperPanelData(out TurnStateManager.HelperPanelData data))
        {
            HideAll(force);
            return;
        }

        BuildHelperText(data, out string title, out string body);

        SetVisible(panelVisible: true, title: title, body: body, force: force);
        RefreshDockByCursorProximity();
    }

    private void BuildHelperText(TurnStateManager.HelperPanelData data, out string title, out string body)
    {
        title = string.Empty;
        body = string.Empty;
        if (data == null)
            return;

        switch (data.Kind)
        {
            case TurnStateManager.HelperPanelKind.Shopping:
                title = ResolveMessage("helper.title.shopping", "SHOPPING");
                body = BuildShoppingBody(data);
                return;

            case TurnStateManager.HelperPanelKind.Sensors:
                title = ResolveMessage("helper.title.sensors", "SENSORS");
                body = BuildSensorsBody(data);
                return;

            case TurnStateManager.HelperPanelKind.Disembark:
                title = ResolveMessage("helper.title.disembark", "DISEMBARK");
                body = BuildDisembarkBody(data);
                return;

            case TurnStateManager.HelperPanelKind.Merge:
                title = ResolveMessage("helper.title.merge", "MERGE");
                body = BuildMergeBody(data);
                return;

            default:
                title = string.Empty;
                body = string.Empty;
                return;
        }
    }

    private string BuildShoppingBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.ShoppingLines == null || data.ShoppingLines.Count == 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.ShoppingLines.Count; i++)
        {
            TurnStateManager.HelperShoppingLine line = data.ShoppingLines[i];
            if (line == null)
                continue;

            if (sb.Length > 0)
                sb.AppendLine();

            if (line.cost.HasValue)
            {
                sb.Append(ResolveMessage(
                    "helper.shopping.line.with_cost",
                    "<index> - <unit> ($<valor>)",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "valor", line.cost.Value.ToString() }
                    }));
            }
            else
            {
                sb.Append(ResolveMessage(
                    "helper.shopping.line.no_cost",
                    "<index> - <unit>",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty }
                    }));
            }
        }

        return sb.ToString();
    }

    private string BuildSensorsBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.SensorLines == null || data.SensorLines.Count == 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.SensorLines.Count; i++)
        {
            TurnStateManager.HelperSensorLine line = data.SensorLines[i];
            if (line == null)
                continue;

            string label = ResolveSensorLabel(line.sensorKey);
            string resolvedLineId = line.sensorKey == "move_only"
                ? "helper.sensors.line.move_only"
                : "helper.sensors.line.format";

            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append(ResolveMessage(
                resolvedLineId,
                "<action> - <label>",
                new Dictionary<string, string>
                {
                    { "action", line.actionCode.ToString() },
                    { "label", label }
                }));
        }

        return sb.ToString();
    }

    private string BuildDisembarkBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ResolveMessage("helper.disembark.section.order", "Order"));

        if (data.DisembarkOrderLines != null && data.DisembarkOrderLines.Count > 0)
        {
            for (int i = 0; i < data.DisembarkOrderLines.Count; i++)
            {
                TurnStateManager.HelperDisembarkOrderLine line = data.DisembarkOrderLines[i];
                if (line == null)
                    continue;

                sb.AppendLine(ResolveMessage(
                    "helper.disembark.order.line",
                    "<index> - <unit> (<stats>) -> <terrain>",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty },
                        { "terrain", line.terrainName ?? string.Empty }
                    }));
            }
        }
        else
        {
            sb.AppendLine(ResolveMessage("helper.disembark.order.empty", "0 - (empty)"));
        }

        sb.AppendLine();
        sb.AppendLine(ResolveMessage("helper.disembark.section.select_passenger", "Select Passenger"));

        if (data.DisembarkPassengerLines != null)
        {
            for (int i = 0; i < data.DisembarkPassengerLines.Count; i++)
            {
                TurnStateManager.HelperDisembarkPassengerLine line = data.DisembarkPassengerLines[i];
                if (line == null)
                    continue;

                sb.AppendLine(ResolveMessage(
                    "helper.disembark.passenger.line",
                    "<index> - <unit> (<stats>)",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty }
                    }));
            }
        }

        if (data.HasQueuedDisembarkOrders)
            sb.Append(ResolveMessage("helper.disembark.process_order.line", "0 - Process Order"));

        return sb.ToString().TrimEnd();
    }

    private string BuildMergeBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ResolveMessage("helper.merge.section.queue", "Queue"));

        if (data.MergeQueueLines != null && data.MergeQueueLines.Count > 0)
        {
            for (int i = 0; i < data.MergeQueueLines.Count; i++)
            {
                TurnStateManager.HelperMergeQueueLine line = data.MergeQueueLines[i];
                if (line == null)
                    continue;

                sb.AppendLine(ResolveMessage(
                    "helper.merge.queue.line",
                    "<index> - <unit> (<stats>)",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty }
                    }));
            }
        }
        else
        {
            sb.AppendLine(ResolveMessage("helper.merge.queue.empty", "0 - (empty)"));
        }

        sb.AppendLine();
        sb.AppendLine(ResolveMessage("helper.merge.section.select", "Select Unit"));

        if (data.MergeCandidateLines != null && data.MergeCandidateLines.Count > 0)
        {
            for (int i = 0; i < data.MergeCandidateLines.Count; i++)
            {
                TurnStateManager.HelperMergeCandidateLine line = data.MergeCandidateLines[i];
                if (line == null)
                    continue;

                sb.AppendLine(ResolveMessage(
                    line.isValid ? "helper.merge.candidate.line" : "helper.merge.candidate.invalid",
                    line.isValid ? "<index> - <unit> (<stats>)" : "<color=#8F8F8F><s><index> - <unit> (<stats>)</s></color> <color=#8F8F8F>- <reason></color>",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty },
                        { "reason", string.IsNullOrWhiteSpace(line.invalidReason) ? "invalido" : line.invalidReason }
                    }));
            }
        }
        else
        {
            sb.AppendLine(ResolveMessage("helper.merge.candidate.empty", "(none)"));
        }

        if (data.IsMergeConfirmStep && data.HasSelectedMergeCandidate)
        {
            sb.AppendLine();
            sb.Append(ResolveMessage(
                "helper.merge.confirm.line",
                "Confirm <index> - <unit> (<stats>)",
                new Dictionary<string, string>
                {
                    { "index", data.SelectedMergeCandidateNumber.ToString() },
                    { "unit", data.SelectedMergeCandidateName ?? string.Empty },
                    { "stats", data.SelectedMergeCandidateStats ?? string.Empty }
                }));

            if (!string.IsNullOrWhiteSpace(data.MergeConfirmPreview))
            {
                sb.AppendLine();
                sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
                sb.Append(ResolveMessage(
                    "helper.merge.confirm.preview",
                    "Result: <preview>",
                    new Dictionary<string, string>
                    {
                        { "preview", data.MergeConfirmPreview }
                    }));
            }
        }
        else if (data.MergeQueueLines != null && data.MergeQueueLines.Count > 0)
        {
            sb.AppendLine();
            sb.Append(ResolveMessage(
                "helper.merge.process_order.line",
                "0 - Process Queue | <preview>",
                new Dictionary<string, string>
                {
                    { "preview", data.MergeQueuePreview ?? string.Empty }
                }));
        }

        return sb.ToString().TrimEnd();
    }

    private string ResolveSensorLabel(string sensorKey)
    {
        switch (sensorKey)
        {
            case "aim":
                return ResolveMessage("helper.sensors.label.aim", "Aim");
            case "embark":
                return ResolveMessage("helper.sensors.label.embark", "Embark");
            case "disembark":
                return ResolveMessage("helper.sensors.label.disembark", "Disembark");
            case "capture":
                return ResolveMessage("helper.sensors.label.capture", "Capture");
            case "fuse":
                return ResolveMessage("helper.sensors.label.fuse", "Fuse units");
            case "supply":
                return ResolveMessage("helper.sensors.label.supply", "Supply");
            case "transfer":
                return ResolveMessage("helper.sensors.label.transfer", "Transfer");
            case "layer":
                return ResolveMessage("helper.sensors.label.layer", "Layer");
            case "move_only":
                return ResolveMessage("helper.sensors.label.move_only", "Move Only");
            default:
                return sensorKey ?? string.Empty;
        }
    }

    private string ResolveMessage(string id, string fallback)
    {
        if (helperDatabase == null)
            return fallback ?? string.Empty;

        return helperDatabase.Resolve(id, fallback);
    }

    private string ResolveMessage(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        if (helperDatabase == null)
            return ApplyInlineTokens(fallback ?? string.Empty, tokens);

        return helperDatabase.Resolve(id, fallback, tokens);
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

            output = output.Replace($"<{pair.Key.Trim()}>", pair.Value ?? string.Empty);
        }

        return output;
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
        bool textChanged = force || lastTitle != title || lastBody != body;
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

        RefreshDynamicPanelHeight(panelVisible, textChanged);

        lastPanelVisible = panelVisible;
        lastTitle = title ?? string.Empty;
        lastBody = body ?? string.Empty;
    }

    private void RefreshDynamicPanelHeight(bool panelVisible, bool contentChanged)
    {
        if (helperRect == null)
            return;

        if (cachedBasePanelHeight <= 0f)
            cachedBasePanelHeight = Mathf.Max(0f, helperRect.rect.height);

        if (!autoExpandHeight)
            return;

        if (!panelVisible)
        {
            float resetHeight = cachedBasePanelHeight > 0f ? cachedBasePanelHeight : helperRect.rect.height;
            helperRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, resetHeight);
            return;
        }

        if (!contentChanged)
            return;

        float titleHeight = 0f;
        if (helperTitle != null)
        {
            helperTitle.ForceMeshUpdate();
            titleHeight = Mathf.Max(0f, helperTitle.preferredHeight);
        }

        float bodyHeight = 0f;
        if (helperTxt != null)
        {
            helperTxt.ForceMeshUpdate();
            bodyHeight = Mathf.Max(0f, helperTxt.preferredHeight);
        }

        float baseMin = cachedBasePanelHeight > 0f ? cachedBasePanelHeight : 0f;
        float minHeight = Mathf.Max(minPanelHeight, baseMin);
        float maxHeight = Mathf.Max(minHeight, maxPanelHeight);
        float targetHeight = Mathf.Clamp(titleHeight + bodyHeight + Mathf.Max(0f, contentVerticalPadding), minHeight, maxHeight);
        helperRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
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
        cachedBasePanelHeight = Mathf.Max(0f, helperRect.rect.height);
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
}

