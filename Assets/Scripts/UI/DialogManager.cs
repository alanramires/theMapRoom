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

    public bool ContextHelpEnabled => contextHelpEnabled;
    public float HoverHelpDelay => hoverHelpDelay;

    [Header("References")]
    [SerializeField] private MatchController matchController;

    private readonly Dictionary<TeamId, HashSet<HelpHintId>> learnedHints = new Dictionary<TeamId, HashSet<HelpHintId>>();

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

    private void HandleActiveTeamChanged(int teamId)
    {
        ClearLearnedHints();
    }

    public void MarkHintLearned(TeamId team, HelpHintId hintId)
    {
        if (team == TeamId.Neutral || (int)team < 1)
            return;

        if (!learnedHints.TryGetValue(team, out HashSet<HelpHintId> hints))
        {
            hints = new HashSet<HelpHintId>();
            learnedHints[team] = hints;
        }

        hints.Add(hintId);
    }

    public void ClearLearnedHints()
    {
        learnedHints.Clear();
    }

    public bool HasLearnedHint(TeamId team, HelpHintId hintId)
    {
        if (team == TeamId.Neutral)
            return true;

        if (learnedHints.TryGetValue(team, out HashSet<HelpHintId> hints))
            return hints.Contains(hintId);

        return false;
    }

    public void TryShowHint(TeamId team, HelpHintId hintId, string buildingOrUnitName)
    {
        if (!contextHelpEnabled)
            return;

        if (matchController != null && matchController.HasVictoryWinner)
            return;

        if (PanelDialogController.HasActiveFixedExternalText())
            return;

        if (HasLearnedHint(team, hintId))
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
}
