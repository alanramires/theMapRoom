using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class PanelMoneyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private TMP_Text textMoney;
    [SerializeField] private Text legacyTextMoney;
    [SerializeField] private TMP_Text textUpdate;
    [SerializeField] private Text legacyTextUpdate;

    [Header("Display")]
    [SerializeField] private string prefix = "$ ";
    [SerializeField] private Color moneyGainColor = new Color(0.35f, 1f, 0.35f, 1f);
    [SerializeField] private Color moneyLossColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] [Range(0f, 5f)] private float moneyUpdateVisibleSeconds = 1.0f;

    private string lastRenderedValue = string.Empty;
    private Color lastRenderedColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
    private readonly Dictionary<TeamId, int> knownMoneyByTeam = new Dictionary<TeamId, int>();
    private Coroutine moneyUpdateRoutine;

    private void Awake()
    {
        TryAutoAssignReferences();
        SeedKnownMoneySnapshot();
        SetUpdateTextVisible(false);
        RefreshMoneyText(force: true);
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

        if (legacyTextMoney == null)
            legacyTextMoney = FindMoneyLegacyTextByName();

        if (textUpdate == null)
            textUpdate = FindMoneyUpdateTextByName();

        if (legacyTextUpdate == null)
            legacyTextUpdate = FindMoneyUpdateLegacyTextByName();

        if (textMoney == null)
            textMoney = GetComponentInChildren<TMP_Text>(true);

        if (legacyTextMoney == null)
            legacyTextMoney = GetComponentInChildren<Text>(true);
    }

    private TMP_Text FindMoneyTextByName()
    {
        Transform named = FindChildRecursive(transform, "text_money");
        if (named == null)
            return null;
        return named.GetComponent<TMP_Text>();
    }

    private Text FindMoneyLegacyTextByName()
    {
        Transform named = FindChildRecursive(transform, "text_money");
        if (named == null)
            return null;
        return named.GetComponent<Text>();
    }

    private TMP_Text FindMoneyUpdateTextByName()
    {
        Transform named = FindChildRecursive(transform, "text_update");
        if (named == null)
            return null;
        return named.GetComponent<TMP_Text>();
    }

    private Text FindMoneyUpdateLegacyTextByName()
    {
        Transform named = FindChildRecursive(transform, "text_update");
        if (named == null)
            return null;
        return named.GetComponent<Text>();
    }

    private void RefreshMoneyText(bool force)
    {
        TeamId activeTeam = TeamId.Neutral;
        int money = 0;
        if (matchController != null)
        {
            activeTeam = matchController.ActiveTeam;
            if (activeTeam != TeamId.Neutral)
                money = Mathf.Max(0, matchController.GetActualMoney(activeTeam));
        }

        string nextValue = $"{prefix}{FormatWithThousandsDots(money)}";
        Color nextColor = ResolveTextColor(activeTeam);

        if (!force && nextValue == lastRenderedValue && nextColor == lastRenderedColor)
            return;

        ApplyTextValue(nextValue);
        ApplyTextColor(nextColor);
        lastRenderedValue = nextValue;
        lastRenderedColor = nextColor;
    }

    private Color ResolveTextColor(TeamId activeTeam)
    {
        return Color.white;
    }

    private void ApplyTextValue(string value)
    {
        if (textMoney != null)
            textMoney.text = value;

        if (legacyTextMoney != null)
            legacyTextMoney.text = value;
    }

    private void ApplyTextColor(Color value)
    {
        if (textMoney != null)
            textMoney.color = value;

        if (legacyTextMoney != null)
            legacyTextMoney.color = value;
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
            ShowMoneyDelta(delta);

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

    private void ShowMoneyDelta(int delta)
    {
        InterruptMoneyUpdateFade();

        string sign = delta > 0 ? "+" : "-";
        int absDelta = Mathf.Abs(delta);
        string text = $"{sign}$ {FormatWithThousandsDots(absDelta)}";
        Color color = delta > 0 ? moneyGainColor : moneyLossColor;

        if (textUpdate != null)
        {
            textUpdate.text = text;
            textUpdate.color = color;
            textUpdate.enabled = true;
        }

        if (legacyTextUpdate != null)
        {
            legacyTextUpdate.text = text;
            legacyTextUpdate.color = color;
            legacyTextUpdate.enabled = true;
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
        if (legacyTextUpdate != null)
            legacyTextUpdate.color = withAlpha;
    }

    private void SetUpdateTextVisible(bool visible)
    {
        if (!visible)
            ApplyUpdateTextColorWithAlpha(moneyGainColor, 0f);

        if (textUpdate != null)
            textUpdate.enabled = visible;
        if (legacyTextUpdate != null)
            legacyTextUpdate.enabled = visible;
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
