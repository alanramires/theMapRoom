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

    private string lastText = string.Empty;
    private bool lastPanelVisible;
    private bool lastTextVisible;
    private CanvasGroup selfPanelCanvasGroup;
    private Color lastTextColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
    private bool hasExternalOverrideText;
    private string externalOverrideText = string.Empty;
    private float externalOverrideUntilUnscaledTime = -1f;

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

        if (textUnit == null)
            textUnit = FindNamedTmpText("text_unit")
                ?? FindNamedTmpText("txt_unit")
                ?? FindNamedTmpText("unit_text")
                ?? FindNamedTmpText("text_dialog");

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
        }

        if (hasExternalOverrideText)
        {
            Color overrideColor = ResolveActiveTeamColor();
            SetVisible(panelVisible: true, textVisible: true, textValue: externalOverrideText, textColor: overrideColor, force: force);
            return;
        }

        UnitManager selectedUnit = ResolveSelectedUnit();
        if (selectedUnit == null)
        {
            HideAll(force);
            return;
        }

        string unitName = ResolveUnitDisplayName(selectedUnit);
        string nextText = BuildStateText(unitName, turnStateManager.CurrentCursorState);
        Color textColor = ResolveActiveTeamColor();
        SetVisible(panelVisible: true, textVisible: true, textValue: nextText, textColor: textColor, force: force);
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
        return TeamUtils.GetColor(activeTeam);
    }

    public static bool TrySetExternalText(string text)
    {
        if (instance == null)
            return false;

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
    }

    public static bool TrySetTransientText(string text, float durationSeconds = 2.6f)
    {
        if (instance == null)
            return false;

        instance.SetExternalText(text, Mathf.Max(0.05f, durationSeconds), timed: true);
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

            output = output.Replace($"<{pair.Key.Trim()}>", pair.Value ?? string.Empty);
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


