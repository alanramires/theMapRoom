using UnityEngine;

[CreateAssetMenu(menuName = "Game/AI/AI Plan Database", fileName = "AIPlanDatabase")]
public class AIPlanDatabase : ScriptableObject
{
    [Header("Fixed Plans")]
    public AIPlanData defensePlan;
    public AIPlanData attackPlan;

    [Header("Dynamic Variable Plans")]
    [Min(0)] public int maxVariablePlans = 3;
    [Min(1), Tooltip("Maximo de unidades de infantaria designadas por plano variavel dinamico.")]
    public int maxUnitsPerVariablePlan = 2;

    public void EnsureDefaults()
    {
        if (maxVariablePlans < 0)
            maxVariablePlans = 0;
        if (maxUnitsPerVariablePlan < 1)
            maxUnitsPerVariablePlan = 1;
    }

    private void OnValidate()
    {
        EnsureDefaults();
    }
}
