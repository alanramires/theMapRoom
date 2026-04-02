using UnityEngine;

[CreateAssetMenu(menuName = "Game/AI/AI Plan Database", fileName = "AIPlanDatabase")]
public class AIPlanDatabase : ScriptableObject
{
    [Header("Fixed Plans")]
    public AIPlanData defensePlan;
    public AIPlanData attackPlan;

    [Header("Dynamic Variable Plans")]
    [Min(0)] public int maxVariablePlans = 3;

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
