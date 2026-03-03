using TMPro;
using UnityEngine;

public class PanelTurnController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private TMP_Text textTurn;

    private string lastValue = string.Empty;
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

        if (textTurn == null)
            textTurn = FindNamedTmpText("text_turn");
    }

    private void Refresh(bool force)
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
