using System.Text;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PanelMoneyController : MonoBehaviour
{
    private struct PendingMoneyUpdate
    {
        public TeamId team;
        public int resultingMoney;
        public string label;
        public int delta;
    }

    private static PanelMoneyController instance;
    private static readonly List<PendingMoneyUpdate> pendingMoneyUpdates = new List<PendingMoneyUpdate>();

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private TMP_Text textMoney;
    [SerializeField] private TMP_Text textUpdate;
    [SerializeField] private TMP_Text textIncoming;

    [Header("Display")]
    [SerializeField] private string prefix = "$ ";
    [SerializeField] private Color moneyGainColor = new Color(0.35f, 1f, 0.35f, 1f);
    [SerializeField] private Color moneyLossColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] [Range(0f, 5f)] private float moneyUpdateVisibleSeconds = 1.0f;

    private string lastRenderedValue = string.Empty;
    private Color lastRenderedColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
    private string lastRenderedIncoming = string.Empty;
    private readonly Dictionary<TeamId, int> knownMoneyByTeam = new Dictionary<TeamId, int>();
    private Coroutine moneyUpdateRoutine;

    public static void PushContextualUpdate(TeamId team, int resultingMoney, string label, int delta)
    {
        if (instance == null)
        {
            pendingMoneyUpdates.Add(new PendingMoneyUpdate
            {
                team = team,
                resultingMoney = resultingMoney,
                label = label,
                delta = delta
            });
            return;
        }

        instance.PushContextualUpdateInternal(team, resultingMoney, label, delta);
    }

    private void Awake()
    {
        instance = this;
        TryAutoAssignReferences();
        SeedKnownMoneySnapshot();
        SetUpdateTextVisible(false);
        RefreshMoneyText(force: true);
        ConsumePendingMoneyUpdates();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void ConsumePendingMoneyUpdates()
    {
        if (pendingMoneyUpdates.Count <= 0)
            return;

        for (int i = 0; i < pendingMoneyUpdates.Count; i++)
        {
            PendingMoneyUpdate pending = pendingMoneyUpdates[i];
            PushContextualUpdateInternal(pending.team, pending.resultingMoney, pending.label, pending.delta);
        }

        pendingMoneyUpdates.Clear();
    }

    private void Update()
    {
        TryAutoAssignReferences();
        TryEmitMoneyDeltaForActiveTeam();
        RefreshMoneyText(force: false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
        RefreshMoneyText(force: true);
    }
#endif

    private void TryAutoAssignReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (animationManager == null)
            animationManager = FindAnyObjectByType<AnimationManager>();

        if (textMoney == null)
            textMoney = FindMoneyTextByName();

        if (textUpdate == null)
            textUpdate = FindMoneyUpdateTextByName();
        if (textIncoming == null)
            textIncoming = FindMoneyIncomingTextByName();

        if (textMoney == null)
            textMoney = GetComponentInChildren<TMP_Text>(true);
    }

    private TMP_Text FindMoneyTextByName()
    {
        Transform named = FindChildRecursive(transform, "text_money");
        if (named == null)
            return null;
        return named.GetComponent<TMP_Text>();
    }

    private TMP_Text FindMoneyUpdateTextByName()
    {
        Transform named = FindChildRecursive(transform, "text_update");
        if (named == null)
            return null;
        return named.GetComponent<TMP_Text>();
    }

    private TMP_Text FindMoneyIncomingTextByName()
    {
        Transform named = FindChildRecursive(transform, "text_incoming");
        if (named == null)
            return null;
        return named.GetComponent<TMP_Text>();
    }

    private void RefreshMoneyText(bool force)
    {
        TeamId activeTeam = TeamId.Neutral;
        int money = 0;
        int incoming = 0;
        if (matchController != null)
        {
            activeTeam = matchController.ActiveTeam;
            if (activeTeam != TeamId.Neutral)
            {
                money = Mathf.Max(0, matchController.GetActualMoney(activeTeam));
                incoming = Mathf.Max(0, matchController.GetIncomePerTurn(activeTeam));
            }
        }

        string nextValue = $"{prefix}{FormatWithThousandsDots(money)}";
        string nextIncoming = $"{incoming} / turn";
        Color nextColor = ResolveTextColor(activeTeam);

        if (!force && nextValue == lastRenderedValue && nextColor == lastRenderedColor && nextIncoming == lastRenderedIncoming)
            return;

        ApplyTextValue(nextValue);
        ApplyIncomingTextValue(nextIncoming);
        ApplyTextColor(nextColor);
        lastRenderedValue = nextValue;
        lastRenderedColor = nextColor;
        lastRenderedIncoming = nextIncoming;
    }

    private Color ResolveTextColor(TeamId activeTeam)
    {
        return Color.white;
    }

    private void ApplyTextValue(string value)
    {
        if (textMoney != null)
            textMoney.text = value;
    }

    private void ApplyTextColor(Color value)
    {
        if (textMoney != null)
            textMoney.color = value;
    }

    private void ApplyIncomingTextValue(string value)
    {
        if (textIncoming != null)
            textIncoming.text = value;
    }

    private void TryEmitMoneyDeltaForActiveTeam()
    {
        if (matchController == null)
            return;

        TeamId activeTeam = matchController.ActiveTeam;
        if (activeTeam == TeamId.Neutral)
            return;

        int currentMoney = Mathf.Max(0, matchController.GetActualMoney(activeTeam));
        if (!knownMoneyByTeam.TryGetValue(activeTeam, out int previousMoney))
        {
            knownMoneyByTeam[activeTeam] = currentMoney;
            return;
        }

        int delta = currentMoney - previousMoney;
        if (delta != 0)
            ShowMoneyUpdate(string.Empty, delta);

        knownMoneyByTeam[activeTeam] = currentMoney;
    }

    private void SeedKnownMoneySnapshot()
    {
        knownMoneyByTeam.Clear();
        if (matchController == null)
            return;

        for (int i = (int)TeamId.Green; i <= (int)TeamId.Yellow; i++)
        {
            TeamId team = (TeamId)i;
            knownMoneyByTeam[team] = Mathf.Max(0, matchController.GetActualMoney(team));
        }
    }

    private void PushContextualUpdateInternal(TeamId team, int resultingMoney, string label, int delta)
    {
        TeamId clampedTeam = team;
        if (clampedTeam < TeamId.Neutral || clampedTeam > TeamId.Yellow)
            clampedTeam = TeamId.Neutral;

        knownMoneyByTeam[clampedTeam] = Mathf.Max(0, resultingMoney);

        if (matchController == null || matchController.ActiveTeam != clampedTeam)
            return;
        if (delta == 0)
            return;

        ShowMoneyUpdate(label, delta);
    }

    private void ShowMoneyUpdate(string label, int delta)
    {
        InterruptMoneyUpdateFade();

        string sign = delta > 0 ? "+" : "-";
        int absDelta = Mathf.Abs(delta);
        string amount = $"{sign}$ {FormatWithThousandsDots(absDelta)}";
        string text = string.IsNullOrWhiteSpace(label)
            ? amount
            : $"{label} {amount}";
        Color color = delta > 0 ? moneyGainColor : moneyLossColor;

        if (textUpdate != null)
        {
            textUpdate.text = text;
            textUpdate.color = color;
            textUpdate.enabled = true;
        }

        SetUpdateTextVisible(true);
        moneyUpdateRoutine = StartCoroutine(FadeOutMoneyUpdateRoutine(color));
    }

    private void InterruptMoneyUpdateFade()
    {
        if (moneyUpdateRoutine == null)
            return;

        StopCoroutine(moneyUpdateRoutine);
        moneyUpdateRoutine = null;
    }

    private IEnumerator FadeOutMoneyUpdateRoutine(Color baseColor)
    {
        float wait = Mathf.Max(0f, moneyUpdateVisibleSeconds);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        float fadeDuration = GetMoneyUpdateFadeDuration();
        if (fadeDuration <= 0f)
        {
            SetUpdateTextVisible(false);
            moneyUpdateRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float a = Mathf.Lerp(baseColor.a, 0f, t);
            ApplyUpdateTextColorWithAlpha(baseColor, a);
            yield return null;
        }

        SetUpdateTextVisible(false);
        moneyUpdateRoutine = null;
    }

    private float GetMoneyUpdateFadeDuration()
    {
        if (animationManager != null)
            return animationManager.MoneyUpdateFadeDuration;

        return 1.2f;
    }

    private void ApplyUpdateTextColorWithAlpha(Color baseColor, float alpha)
    {
        Color withAlpha = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        if (textUpdate != null)
            textUpdate.color = withAlpha;
    }

    private void SetUpdateTextVisible(bool visible)
    {
        if (!visible)
            ApplyUpdateTextColorWithAlpha(moneyGainColor, 0f);

        if (textUpdate != null)
            textUpdate.enabled = visible;
    }

    private static string FormatWithThousandsDots(int value)
    {
        int safe = Mathf.Max(0, value);
        string digits = safe.ToString();
        if (digits.Length <= 3)
            return digits;

        StringBuilder sb = new StringBuilder(digits.Length + (digits.Length / 3));
        int firstGroupLen = digits.Length % 3;
        if (firstGroupLen == 0)
            firstGroupLen = 3;

        sb.Append(digits, 0, firstGroupLen);
        for (int i = firstGroupLen; i < digits.Length; i += 3)
        {
            sb.Append('.');
            sb.Append(digits, i, 3);
        }

        return sb.ToString();
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
