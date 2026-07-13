using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelRemainingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private TMP_Text textActual;
    [SerializeField] private TMP_Text textMax;
    [SerializeField] private TMP_Text textUnidade;
    [SerializeField] private TMP_Text textCap;
    [SerializeField] private TMP_Text textCamada;
    [SerializeField] private TMP_Text labelCamada;
    [SerializeField] private Button buttonRodada;
    [SerializeField] private Button buttonCamada;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private TurnStateManager turnStateManager;

    private string lastActual = string.Empty;
    private string lastMax = string.Empty;
    private string lastCap = string.Empty;
    private string lastCamada = string.Empty;
    private Color lastColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
    private readonly List<FogOfWarVisionMode> availableVisionModes = new List<FogOfWarVisionMode>();

    private void Awake()
    {
        TryAutoAssignReferences();
        HookRoundButton();
        HookLayerButton();
        Refresh(force: true);
        RefreshCamada(force: true);
    }

    private void Update()
    {
        TryAutoAssignReferences();
        Refresh(force: false);
        RefreshCamada(force: false);
        RefreshRoundButtonInteractability();
        RefreshLayerButton();
    }

    private void OnDestroy()
    {
        if (buttonRodada != null)
            buttonRodada.onClick.RemoveListener(HandleRoundButtonClicked);
        if (buttonCamada != null)
            buttonCamada.onClick.RemoveListener(HandleLayerButtonClicked);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        TryAutoAssignReferences();
        Refresh(force: true);
        RefreshCamada(force: true);
    }
#endif

    private void TryAutoAssignReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (textActual == null)
            textActual = FindNamedTmpText("text_actual");

        if (textMax == null)
            textMax = FindNamedTmpText("text_max");

        if (textUnidade == null)
            textUnidade = FindNamedTmpText("text_unidade");

        if (textCap == null)
            textCap = FindNamedTmpText("text_cap");

        if (textCamada == null)
            textCamada = FindNamedTmpText("text_camada");
        if (labelCamada == null)
            labelCamada = FindNamedTmpText("label_camada");

        if (buttonRodada == null)
        {
            Transform buttonTransform = FindChildRecursive(transform, "button_rodada");
            if (buttonTransform != null)
            {
                buttonRodada = buttonTransform.GetComponent<Button>();
                HookRoundButton();
            }
        }

        if (buttonCamada == null)
        {
            Transform buttonTransform = FindChildRecursive(transform, "button_camada");
            if (buttonTransform != null)
            {
                buttonCamada = buttonTransform.GetComponent<Button>();
                HookLayerButton();
            }
        }
    }

    private void HookLayerButton()
    {
        if (buttonCamada == null)
            return;
        buttonCamada.onClick.RemoveListener(HandleLayerButtonClicked);
        buttonCamada.onClick.AddListener(HandleLayerButtonClicked);
    }

    private void HandleLayerButtonClicked()
    {
        TryAutoAssignReferences();
        if (buttonCamada == null || !buttonCamada.interactable || matchController == null)
            return;
        matchController.CycleFogOfWarVisionMode();
    }

    private void RefreshLayerButton()
    {
        if (buttonCamada == null)
            return;
        int count = matchController != null
            ? matchController.GetAvailableFogOfWarVisionModes(availableVisionModes)
            : 0;
        bool visible = count > 1;
        if (buttonCamada.gameObject.activeSelf != visible)
            buttonCamada.gameObject.SetActive(visible);
        buttonCamada.interactable = visible && matchController != null &&
            !matchController.HasVictoryWinner && !matchController.IsTurnTransitionInProgress &&
            !matchController.IsPlayerInputLockedByActiveAI();
    }

    private void HookRoundButton()
    {
        if (buttonRodada == null)
            return;

        buttonRodada.onClick.RemoveListener(HandleRoundButtonClicked);
        buttonRodada.onClick.AddListener(HandleRoundButtonClicked);
    }

    private void HandleRoundButtonClicked()
    {
        TryAutoAssignReferences();
        if (cursorController == null || turnStateManager == null || !buttonRodada.interactable)
            return;

        if (turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
            return;

        // Botão flutuante de fim de turno → abre a confirmação (prompt de fim de turno),
        // não passa direto. O atalho R continua sendo o caminho rápido.
        if (!cursorController.RequestEndTurnConfirmation())
            cursorController.PlayErrorSfx();
    }

    private void RefreshRoundButtonInteractability()
    {
        if (buttonRodada == null)
            return;

        bool interactable = matchController != null &&
                            turnStateManager != null &&
                            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral &&
                            !matchController.HasVictoryWinner &&
                            !matchController.IsTurnTransitionInProgress &&
                            !matchController.IsPlayerInputLockedByActiveAI() &&
                            !TutorialManager.IsEndTurnLockedByTutorial;
        buttonRodada.interactable = interactable;
    }

    public void SetFogOfWarVisionMode(FogOfWarVisionMode mode)
    {
        TryAutoAssignReferences();
        string label = GetFogOfWarVisionModeLabel(mode);
        if (textCamada != null)
            textCamada.text = label;
        RefreshLayerLabel(mode, label);
        lastCamada = label;
    }

    private void Refresh(bool force)
    {
        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        int totalInField = 0;
        int readyToAct = 0;
        bool shouldCountActiveTeam =
            matchController != null &&
            (activeTeam != TeamId.Neutral || matchController.IncludeNeutralTeam);
        if (shouldCountActiveTeam)
        {
            matchController.GetTeamUnitCounts(activeTeam, out totalInField, out _);
            matchController.GetTeamUnitCounts(activeTeam, out _, out readyToAct, includeEmbarked: false);
        }

        bool hideAiUnitCounts = matchController != null &&
                                matchController.ShouldHideActiveAiActionPresentation();
        string nextActual = hideAiUnitCounts ? "?" : $"{Mathf.Max(0, readyToAct)}";
        string nextMax = hideAiUnitCounts ? "/ ?" : $"/{Mathf.Max(0, totalInField)}";
        string nextCap = matchController != null ? matchController.MaxUnitsPerTeam.ToString() : "0";
        Color teamColor = TeamUtils.GetColor(activeTeam);

        if (!force && nextActual == lastActual && nextMax == lastMax && nextCap == lastCap && teamColor == lastColor)
            return;

        if (textActual != null)
        {
            textActual.text = nextActual;
            textActual.color = teamColor;
        }

        if (textMax != null)
        {
            textMax.text = nextMax;
            textMax.color = teamColor;
        }

        if (textUnidade != null)
            textUnidade.color = teamColor;

        if (textCap != null)
        {
            textCap.text = nextCap;
            textCap.color = teamColor;
        }

        lastActual = nextActual;
        lastMax = nextMax;
        lastCap = nextCap;
        lastColor = teamColor;
    }

    private void RefreshCamada(bool force)
    {
        FogOfWarVisionMode mode = matchController != null
            ? matchController.FogOfWarVisionMode
            : FogOfWarVisionMode.All;
        string label = GetFogOfWarVisionModeLabel(mode);
        if (!force && label == lastCamada)
            return;

        if (textCamada != null)
            textCamada.text = label;
        RefreshLayerLabel(mode, label);
        lastCamada = label;
    }

    private void RefreshLayerLabel(FogOfWarVisionMode mode, string modeLabel)
    {
        if (labelCamada == null)
            return;
        bool visible = mode != FogOfWarVisionMode.All;
        labelCamada.gameObject.SetActive(visible);
        if (visible)
            labelCamada.text = $"Camada: {modeLabel}";
    }

    private static string GetFogOfWarVisionModeLabel(FogOfWarVisionMode mode)
    {
        return mode switch
        {
            FogOfWarVisionMode.Air => "Aérea",
            FogOfWarVisionMode.Surface => "Superfície",
            FogOfWarVisionMode.Sub => "Submarina",
            _ => "Todas"
        };
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
