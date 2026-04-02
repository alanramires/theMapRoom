using System;
using UnityEngine;

public enum PlanConditionType
{
    AlwaysActive = 0,
    SectorNotControlledByAI = 1,
    SectorPartiallyControlledByAI = 2,
    EnemyUnitsVisibleInSector = 3,
    FriendlyStrengthBelowPercent = 4,
}

[Serializable]
public class PlanCondition
{
    [Tooltip("Tipo da condição de ativação.")]
    public PlanConditionType type = PlanConditionType.AlwaysActive;

    [Tooltip("Limiar percentual (0–100). Usado em FriendlyStrengthBelowPercent.")]
    [Range(0f, 100f)]
    public float threshold = 50f;
}
