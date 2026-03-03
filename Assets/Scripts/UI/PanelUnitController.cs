using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelUnitController : MonoBehaviour
{
    private static PanelUnitController instance;

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
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
            panelUnit = FindNamedObject("panel_unit") ?? FindNamedObject("unit_panel");
        if (panelUnit == null)
            panelUnit = gameObject;

        if (textUnit == null)
            textUnit = FindNamedTmpText("text_unit")
                ?? FindNamedTmpText("txt_unit")
                ?? FindNamedTmpText("unit_text");
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
            return $"{unitName} :: Moving";

        string sensor = ResolveSensorName(state);
        if (string.IsNullOrWhiteSpace(sensor))
            return unitName;

        bool isConfirm = IsSensorConfirmPhase(state);
        return isConfirm
            ? $"{unitName} :: {sensor} Confirm"
            : $"{unitName} :: {sensor}";
    }

    private string ResolveSensorName(TurnStateManager.CursorState state)
    {
        switch (state)
        {
            case TurnStateManager.CursorState.Mirando:
                return "Aim";
            case TurnStateManager.CursorState.Capturando:
                return "Capture";
            case TurnStateManager.CursorState.Embarcando:
                return "Embark";
            case TurnStateManager.CursorState.Desembarcando:
                return "Disembark";
            case TurnStateManager.CursorState.Pousando:
                return "Landing";
            case TurnStateManager.CursorState.Fundindo:
                return "Merge";
            case TurnStateManager.CursorState.Suprindo:
                return "Supply";
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
