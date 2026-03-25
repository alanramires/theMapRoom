using UnityEngine;
using System.Collections.Generic;

public class HelpManager : MonoBehaviour
{
    private static HelpManager instance;
    public static HelpManager Instance => instance;

    [Header("Tutorial")]
    [Tooltip("Selecione um TutorialData opcional para esta fase. Se nulo, trata-se de uma partida normal.")]
    [SerializeField] private TutorialData activeTutorial;

    public TutorialData ActiveTutorial => activeTutorial;
    public bool IsTutorialMode => activeTutorial != null;

    [Header("Settings")]
    [SerializeField] private bool contextHelpEnabled = true;
    [SerializeField] [Range(0.2f, 5f)] private float hoverHelpDelay = 1.0f;
    [SerializeField] [Range(1f, 10f)] private float dialogHelpDuration = 3.0f;

    public bool ContextHelpEnabled => contextHelpEnabled;
    public float HoverHelpDelay => hoverHelpDelay;

    [Header("References")]
    [SerializeField] private MatchController matchController;

    private Dictionary<TeamId, HashSet<HelpHintId>> learnedHints = new Dictionary<TeamId, HashSet<HelpHintId>>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
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
        if (team == TeamId.Neutral || (int)team < 1) return;

        if (!learnedHints.TryGetValue(team, out var hints))
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
        if (team == TeamId.Neutral) return true;

        if (learnedHints.TryGetValue(team, out var hints))
        {
            return hints.Contains(hintId);
        }
        return false;
    }

    public void TryShowHint(TeamId team, HelpHintId hintId, string buildingOrUnitName)
    {
        if (!contextHelpEnabled) return;

        // Se a partida acabou, não mostra mais hints de hover
        if (matchController != null && matchController.HasVictoryWinner)
        {
            return;
        }

        // Se houver um diálogo FIXO (não temporário) ativo, não sobrescrever com o hover
        if (PanelDialogController.HasActiveFixedExternalText())
        {
            return;
        }

        if (HasLearnedHint(team, hintId)) return;

        // 1. Sempre busca a mensagem base do Dialog Database (ou fallback hardcoded)
        string dialogId = GetDialogIdForHint(hintId);
        string baseText = PanelDialogController.ResolveDialogMessage(
            dialogId, 
            GetFallbackText(hintId, buildingOrUnitName), 
            new Dictionary<string, string> {
                { "construction", buildingOrUnitName },
                { "unit", buildingOrUnitName }
            });

        // 2. Se estiver em tutorial, tenta enriquecer com o Objetivo correspondente
        string prefix = string.Empty;
        if (IsTutorialMode && activeTutorial.objectives != null)
        {
            for (int i = 0; i < activeTutorial.objectives.Count; i++)
            {
                var obj = activeTutorial.objectives[i];
                if (!obj.isCompleted && obj.hintId == hintId)
                {
                    prefix = obj.description;
                    break;
                }
            }
        }

        string finalText = !string.IsNullOrWhiteSpace(prefix) 
            ? $"{prefix} :: {baseText}" 
            : baseText;

        if (!string.IsNullOrEmpty(finalText))
        {
            PanelDialogController.TrySetTransientText(finalText, dialogHelpDuration);
        }
    }

    private string GetDialogIdForHint(HelpHintId hintId)
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

    private string GetFallbackText(HelpHintId hintId, string entityName)
    {
        // Fallback ultra-simples: se o DB falhar, mostra apenas o nome da entidade.
        // O padrão real agora virá dos assets Hint Act/Inspect/Produce.asset
        return entityName;
    }
}
