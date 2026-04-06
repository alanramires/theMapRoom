using System;
using UnityEngine;

public enum PlanConditionType
{
    AlwaysActive = 0,
    SectorNotControlledByAI = 1,
    SectorPartiallyControlledByAI = 2,
    EnemyUnitsVisibleInSector = 3,
    FriendlyStrengthBelowPercent = 4,
    /// <summary>Verdadeiro quando a IA controla mais de X% de todas as construcoes capturaveis no mapa.</summary>
    TotalConstructionsControlledAbovePercent = 5,
    /// <summary>Verdadeiro quando ha inimigo visivel dentro do raio de defesa do HQ proprio.</summary>
    EnemyUnitsNearHQ = 6,
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
