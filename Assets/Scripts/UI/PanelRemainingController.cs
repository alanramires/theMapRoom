using TMPro;
using UnityEngine;

public class PanelRemainingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private TMP_Text textActual;
    [SerializeField] private TMP_Text textMax;
    [SerializeField] private TMP_Text textUnidade;

    private string lastActual = string.Empty;
    private string lastMax = string.Empty;
    private Color lastColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);

    private void Awake()
    {
        TryAutoAssignReferences();
        Refresh(force: true);
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
        Refresh(force: true);
    }
#endif

    private void TryAutoAssignReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (textActual == null)
            textActual = FindNamedTmpText("text_actual");

        if (textMax == null)
            textMax = FindNamedTmpText("text_max");

        if (textUnidade == null)
            textUnidade = FindNamedTmpText("text_unidade");
    }

    private void Refresh(bool force)
    {
        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        int totalInField = 0;
        int readyToAct = 0;
        if (matchController != null && activeTeam != TeamId.Neutral)
            matchController.GetTeamUnitCounts(activeTeam, out totalInField, out readyToAct);

        string nextActual = $"{Mathf.Max(0, readyToAct)}";
        string nextMax = $"/{Mathf.Max(0, totalInField):D2}";
        Color teamColor = TeamUtils.GetColor(activeTeam);

        if (!force && nextActual == lastActual && nextMax == lastMax && teamColor == lastColor)
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

        lastActual = nextActual;
        lastMax = nextMax;
        lastColor = teamColor;
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
