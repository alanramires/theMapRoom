using System.Collections.Generic;
using UnityEngine;

public class DialogManager : MonoBehaviour
{
    private static DialogManager instance;
    public static DialogManager Instance => instance;

    [Header("Settings")]
    [SerializeField] private bool contextHelpEnabled = true;
    [SerializeField] [Range(0.2f, 5f)] private float hoverHelpDelay = 1.0f;
    [SerializeField] [Range(1f, 10f)] private float dialogHelpDuration = 3.0f;
    [Header("Text Color Override")]
    [SerializeField] private bool useTeamColor = true;
    [SerializeField] private Color fallbackDialogTextColor = Color.white;

    public bool ContextHelpEnabled => contextHelpEnabled;
    public float HoverHelpDelay => hoverHelpDelay;
    public bool UseTeamColor => useTeamColor;
    public Color FallbackDialogTextColor => EnsureValidReadableColor(fallbackDialogTextColor);

    [Header("References")]
    [SerializeField] private MatchController matchController;

    private readonly Dictionary<int, HashSet<HelpHintId>> learnedHintsBySlot = new Dictionary<int, HashSet<HelpHintId>>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;

        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
    }

    private void OnEnable()
    {
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
    }

    private void OnDisable()
    {
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
    }

    private void OnValidate()
    {
        fallbackDialogTextColor = EnsureValidReadableColor(fallbackDialogTextColor);
    }

    private void HandleActiveTeamChanged(int teamId)
    {
        ClearLearnedHints();
    }

    public void MarkHintLearned(PlayerSlotId slotId, HelpHintId hintId)
    {
        if (!slotId.IsValid)
            return;

        int slotIndex = slotId.Value;
        if (!learnedHintsBySlot.TryGetValue(slotIndex, out HashSet<HelpHintId> hints))
        {
            hints = new HashSet<HelpHintId>();
            learnedHintsBySlot[slotIndex] = hints;
        }

        hints.Add(hintId);
    }

    public void ClearLearnedHints()
    {
        learnedHintsBySlot.Clear();
    }

    public bool HasLearnedHint(PlayerSlotId slotId, HelpHintId hintId)
    {
        if (!slotId.IsValid)
            return true;

        if (learnedHintsBySlot.TryGetValue(slotId.Value, out HashSet<HelpHintId> hints))
            return hints.Contains(hintId);

        return false;
    }

    public void TryShowHint(PlayerSlotId slotId, HelpHintId hintId, string buildingOrUnitName)
    {
        if (!contextHelpEnabled)
            return;

        if (matchController != null && matchController.HasVictoryWinner)
            return;

        if (PanelDialogController.HasActiveFixedExternalText())
            return;

        if (HasLearnedHint(slotId, hintId))
            return;

        string dialogId = GetDialogIdForHint(hintId);
        string baseText = PanelDialogController.ResolveDialogMessage(
            dialogId,
            GetFallbackText(hintId, buildingOrUnitName),
            new Dictionary<string, string>
            {
                { "construction", buildingOrUnitName },
                { "unit", buildingOrUnitName }
            });

        if (!string.IsNullOrEmpty(baseText))
            PanelDialogController.TrySetTransientText(baseText, dialogHelpDuration);
    }

    private static string GetDialogIdForHint(HelpHintId hintId)
    {
        switch (hintId)
        {
            case HelpHintId.Produce: return "hint.produce";
            case HelpHintId.Inspect: return "hint.inspect";
            case HelpHintId.Act: return "hint.act";
            case HelpHintId.Construction: return "hint.construction";
            default: return string.Empty;
        }
    }

    private static string GetFallbackText(HelpHintId hintId, string entityName)
    {
        return entityName;
    }

    public Color ResolveDialogTextColor(TeamId team)
    {
        if (useTeamColor && IsRealTeam(team))
        {
            Color teamColor = TeamUtils.GetColor(team);
            if (IsColorValid(teamColor))
                return teamColor;
        }

        return EnsureValidReadableColor(fallbackDialogTextColor);
    }

    private static bool IsRealTeam(TeamId team)
    {
        return team == TeamId.Green ||
               team == TeamId.Red ||
               team == TeamId.Blue ||
               team == TeamId.Yellow;
    }

    private static bool IsColorValid(Color color)
    {
        return !float.IsNaN(color.r) && !float.IsInfinity(color.r) &&
               !float.IsNaN(color.g) && !float.IsInfinity(color.g) &&
               !float.IsNaN(color.b) && !float.IsInfinity(color.b) &&
               !float.IsNaN(color.a) && !float.IsInfinity(color.a);
    }

    private static Color EnsureValidReadableColor(Color color)
    {
        if (!IsColorValid(color))
            return Color.white;

        // Se vier totalmente transparente, evita "sumir" na UI.
        if (color.a <= 0.001f)
            return Color.white;

        return color;
    }
}
