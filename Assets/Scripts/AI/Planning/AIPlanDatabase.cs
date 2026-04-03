using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/AI/AI Plan Database", fileName = "AIPlanDatabase")]
public class AIPlanDatabase : ScriptableObject
{
    [Header("Fixed Plans")]
    public AIPlanData defensePlan;
    public AIPlanData attackPlan;

    [Header("Active Sector Plans Budget")]
    [Min(0)] public int maxVariablePlans = 3;

    public AIPlanData GetPlanByKey(string planKey)
    {
        if (string.IsNullOrWhiteSpace(planKey))
            return null;

        const string fixedPrefix = "fixed:";
        string normalized = planKey.Trim();
        if (!normalized.StartsWith(fixedPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        string stableId = normalized.Substring(fixedPrefix.Length);
        if (string.IsNullOrWhiteSpace(stableId))
            return null;

        AIPlanData[] candidates = new AIPlanData[] { defensePlan, attackPlan };
        for (int i = 0; i < candidates.Length; i++)
        {
            AIPlanData candidate = candidates[i];
            if (candidate == null)
                continue;

            string candidateStableId = !string.IsNullOrWhiteSpace(candidate.planId)
                ? candidate.planId
                : (!string.IsNullOrWhiteSpace(candidate.displayName) ? candidate.displayName : candidate.name);

            if (string.Equals(candidateStableId, stableId, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    public void EnsureDefaults()
    {
        if (maxVariablePlans < 0)
            maxVariablePlans = 0;
    }

    private void OnValidate()
    {
        EnsureDefaults();
    }
}
