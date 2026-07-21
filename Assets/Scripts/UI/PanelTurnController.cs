using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelTurnController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private TMP_Text textTurn;

    [Header("Match statistics")]
    [SerializeField] private GameObject panelEstatisticas;
    [SerializeField] private TMP_Text slot0Count;
    [SerializeField] private TMP_Text slot1Count;
    [SerializeField] private Image slot0Bar;
    [SerializeField] private Image neutralBar;
    [SerializeField] private Image slot1Bar;

    private string lastValue = string.Empty;
    private Color lastColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
    private bool statsInitialized;
    private bool wasNeutral;
    private int lastStatsTurn = -1;

    private void Awake()
    {
        TryAutoAssignReferences();
        RefreshTurn(force: true);
        RefreshStatsIfConfirmed(force: true);
    }

    private void Update()
    {
        TryAutoAssignReferences();
        RefreshTurn(force: false);
        RefreshStatsIfConfirmed(force: false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
        RefreshTurn(force: true);
    }
#endif

    private void TryAutoAssignReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (textTurn == null)
            textTurn = FindNamedTmpText("text_turn");

        if (panelEstatisticas == null)
            panelEstatisticas = FindNamedChild("panel_estatisticas")?.gameObject;
        if (slot0Count == null)
            slot0Count = FindNamedTmpText("slot0_count");
        if (slot1Count == null)
            slot1Count = FindNamedTmpText("slot1_count");
        if (slot0Bar == null)
            slot0Bar = FindNamedImage("slot0_bar");
        if (neutralBar == null)
            neutralBar = FindNamedImage("slot_bar");
        if (slot1Bar == null)
            slot1Bar = FindNamedImage("slot1_bar");
    }

    private void RefreshTurn(bool force)
    {
        int turn = matchController != null ? Mathf.Max(0, matchController.CurrentTurn) : 0;
        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;

        string next = $"Turno {turn}";
        Color color = TeamUtils.GetColor(activeTeam);
        if (!force && next == lastValue && color == lastColor)
            return;

        if (textTurn != null)
        {
            textTurn.text = next;
            textTurn.color = color;
        }

        lastValue = next;
        lastColor = color;
    }

    private void RefreshStatsIfConfirmed(bool force)
    {
        if (!Application.isPlaying)
            return;

        bool isNeutral = turnStateManager == null ||
                         turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral;
        int turn = matchController != null ? matchController.CurrentTurn : -1;
        bool enteredNeutral = isNeutral && !wasNeutral;
        wasNeutral = isNeutral;

        UpdateStatsVisibility();
        if (!isNeutral || panelEstatisticas == null || !panelEstatisticas.activeSelf)
            return;
        if (!force && statsInitialized && !enteredNeutral && turn == lastStatsTurn)
            return;

        RefreshConfirmedStats();
        statsInitialized = true;
        lastStatsTurn = turn;
    }

    private void UpdateStatsVisibility()
    {
        if (panelEstatisticas == null)
            return;

        bool hasTwoSlots = matchController != null && matchController.SlotCount == 2;
        bool aiVersusAi = hasTwoSlots &&
                          matchController.IsPlayerAI(matchController.GetTeamIdForSlot(0)) &&
                          matchController.IsPlayerAI(matchController.GetTeamIdForSlot(1));
        bool fogOfWarTotal = matchController != null &&
                             matchController.GameSetup == MatchController.GameSetupPreset.FogOfWarTotal &&
                             matchController.IsFogOfWarDebugEnabled;
        // Mapa de tutorial: o painel de turno fica só com o contador. Placar de unidades/território
        // não tem sentido num mapa roteirizado e ainda compete com o passo a passo pela atenção.
        bool tutorial = matchController != null && matchController.IsTutorialMode;
        bool shouldShow = !tutorial && hasTwoSlots && (!fogOfWarTotal || aiVersusAi);

        if (panelEstatisticas.activeSelf != shouldShow)
        {
            panelEstatisticas.SetActive(shouldShow);
            if (shouldShow)
                statsInitialized = false;
        }
    }

    private void RefreshConfirmedStats()
    {
        if (matchController == null)
            return;

        TeamId team0 = matchController.GetTeamIdForSlot(0);
        TeamId team1 = matchController.GetTeamIdForSlot(1);
        MatchStatsManager statsManager = MatchStatsManager.Instance ?? MatchStatsManager.EnsureInstance();

        statsManager.TryGetStats(team0, out SlotMatchStats stats0);
        statsManager.TryGetStats(team1, out SlotMatchStats stats1);

        int units0 = stats0 != null ? Mathf.Max(0, stats0.currentUnits) : 0;
        int units1 = stats1 != null ? Mathf.Max(0, stats1.currentUnits) : 0;
        float percent0 = stats0 != null ? Mathf.Clamp(stats0.territoryControlPercent, 0f, 100f) : 0f;
        float percent1 = stats1 != null ? Mathf.Clamp(stats1.territoryControlPercent, 0f, 100f) : 0f;

        // Em partida de dois lados sem nenhuma construção neutra, toda a barra pertence a um dos
        // dois times. Pontos já retirados do dono por captura parcial migram para o adversário,
        // mesmo se o cache de ocupação não conseguiu identificar o capturador naquele frame.
        // Isso evita uma faixa cinza falsa entre os lados durante a captura de prédio inimigo.
        if (!HasNeutralCapturableConstruction()
            && stats0 != null && stats1 != null
            && stats0.totalCapturePointsMax > 0)
        {
            int unclaimedFromTeam0 = Mathf.Max(0,
                stats0.contestedOwnedCapturePoints - stats1.contestingCapturePoints);
            int unclaimedFromTeam1 = Mathf.Max(0,
                stats1.contestedOwnedCapturePoints - stats0.contestingCapturePoints);
            float totalPoints = stats0.totalCapturePointsMax;
            percent0 = Mathf.Clamp((stats0.controlledCapturePoints + unclaimedFromTeam1)
                * 100f / totalPoints, 0f, 100f);
            percent1 = Mathf.Clamp((stats1.controlledCapturePoints + unclaimedFromTeam0)
                * 100f / totalPoints, 0f, 100f);
        }

        // Arredondamentos e territórios contestados podem fazer a soma passar de 100.
        // Normalizamos apenas a apresentação; a fonte estatística permanece intacta.
        float ownedTotal = percent0 + percent1;
        if (ownedTotal > 100f)
        {
            percent0 = percent0 * 100f / ownedTotal;
            percent1 = percent1 * 100f / ownedTotal;
        }
        Color color0 = TeamUtils.GetColor(team0);
        Color color1 = TeamUtils.GetColor(team1);
        if (slot0Count != null)
        {
            slot0Count.text = units0.ToString();
            slot0Count.color = color0;
        }
        if (slot1Count != null)
        {
            slot1Count.text = units1.ToString();
            slot1Count.color = color1;
        }
        if (slot0Bar != null) slot0Bar.color = color0;
        if (slot1Bar != null) slot1Bar.color = color1;
        if (neutralBar != null) neutralBar.color = Color.gray;

        // A barra neutra e o fundo integral. As barras dos slots ficam por cima:
        // slot 0 cresce da esquerda para a direita; slot 1, da direita para a esquerda.
        if (neutralBar != null)
            neutralBar.transform.SetAsFirstSibling();
        SetHorizontalSegment(neutralBar, 0f, 1f);
        SetHorizontalSegment(slot0Bar, 0f, percent0 / 100f);
        SetHorizontalSegment(slot1Bar, 1f - percent1 / 100f, 1f);
    }

    private static bool HasNeutralCapturableConstruction()
    {
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
            if (construction != null
                && construction.IsCapturable
                && construction.CapturePointsMax > 0
                && construction.TeamId == TeamId.Neutral)
                return true;
        return false;
    }

    private static void SetHorizontalSegment(Image image, float min, float max)
    {
        if (image == null)
            return;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(Mathf.Clamp01(min), 0.5f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(max), 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 10f);
    }

    private TMP_Text FindNamedTmpText(string name)
    {
        Transform local = FindNamedChild(name);
        if (local != null)
            return local.GetComponent<TMP_Text>();

        GameObject global = GameObject.Find(name);
        return global != null ? global.GetComponent<TMP_Text>() : null;
    }

    private Image FindNamedImage(string name)
    {
        Transform local = FindNamedChild(name);
        return local != null ? local.GetComponent<Image>() : null;
    }

    private Transform FindNamedChild(string name)
    {
        return FindChildRecursive(transform, name);
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && string.Equals(child.name.Trim(), childName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
