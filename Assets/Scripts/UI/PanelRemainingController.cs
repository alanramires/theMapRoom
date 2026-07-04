using TMPro;
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
    [SerializeField] private Button buttonRodada;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private TurnStateManager turnStateManager;

    private string lastActual = string.Empty;
    private string lastMax = string.Empty;
    private string lastCap = string.Empty;
    private string lastCamada = string.Empty;
    private Color lastColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);

    private void Awake()
    {
        TryAutoAssignReferences();
        HookRoundButton();
        Refresh(force: true);
        RefreshCamada(force: true);
    }

    private void Update()
    {
        TryAutoAssignReferences();
        Refresh(force: false);
        RefreshCamada(force: false);
        RefreshRoundButtonInteractability();
    }

    private void OnDestroy()
    {
        if (buttonRodada != null)
            buttonRodada.onClick.RemoveListener(HandleRoundButtonClicked);
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

        if (buttonRodada == null)
        {
            Transform buttonTransform = FindChildRecursive(transform, "button_rodada");
            if (buttonTransform != null)
            {
                buttonRodada = buttonTransform.GetComponent<Button>();
                HookRoundButton();
            }
        }
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

        if (!cursorController.TryExecuteEndTurnFromMenu())
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
                            !matchController.IsPlayerInputLockedByActiveAI();
        buttonRodada.interactable = interactable;
    }

    public void SetFogOfWarVisionMode(FogOfWarVisionMode mode)
    {
        TryAutoAssignReferences();
        string label = GetFogOfWarVisionModeLabel(mode);
        if (textCamada != null)
            textCamada.text = label;
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

        string nextActual = $"{Mathf.Max(0, readyToAct)}";
        string nextMax = $"/{Mathf.Max(0, totalInField)}";
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
        lastCamada = label;
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
