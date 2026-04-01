using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/AI/AI Plan Database", fileName = "AIPlanDatabase")]
public class AIPlanDatabase : ScriptableObject
{
    [Header("Fixed Plans")]
    public AIPlanData defensePlan;
    public AIPlanData attackPlan;

    [Header("Variable Plans")]
    [Min(0)] public int maxVariablePlans = 3;
    public List<AIPlanData> variablePlans = new List<AIPlanData>();

    public void EnsureDefaults()
    {
        if (maxVariablePlans < 0)
            maxVariablePlans = 0;
        if (variablePlans == null)
            variablePlans = new List<AIPlanData>();

        for (int i = variablePlans.Count - 1; i >= 0; i--)
        {
            if (variablePlans[i] == null)
                variablePlans.RemoveAt(i);
        }
    }

    private void OnValidate()
    {
        EnsureDefaults();
    }
}