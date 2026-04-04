using UnityEngine;

[CreateAssetMenu(menuName = "Game/AI/AI Profile", fileName = "AIProfile")]
public class AIGeneralProfile : ScriptableObject
{
    public const int DefaultMinimumRangeForDefensePlan = 5;
    public const float DefaultMinimumConstructionControlledForFinalAttackPlan = 0.5f;

    public string profileName = "General";
    [Tooltip("Time preferido para este general. Neutral = sem vinculacao.")]
    public TeamId preferedTeamAssignment = TeamId.Neutral;
    [Tooltip("Dados de composicao/compra usados por este general.")]
    public AIData aiData;

    [Header("Planner Runtime")]
    [Min(1), Tooltip("Raio maximo para puxar unidades para Defender HQ no modo DEFESA.")]
    public int defensePullRadius = 6;
    [Min(0), Tooltip("Maximo de unidades realocadas por turno para Invadir HQ no modo ATAQUE.")]
    public int maxAttackReassignPerTurn = 2;
    [Min(0), Tooltip("Turnos sem progresso antes de considerar plano estagnado para realocacao.")]
    public int stagnationTurns = 2;
    [Min(0), Tooltip("Maximo de planos de setor variaveis ativos simultaneamente.")]
    public int maxVariablePlans = 3;

    [Header("Planner Gates")]
    [Min(1), Tooltip("Raio minimo em hexes para ativar o plano fixo de defesa do HQ.")]
    public int minimumRangeForDefensePlan = DefaultMinimumRangeForDefensePlan;
    [Range(0f, 1f), Tooltip("Percentual minimo de construcoes capturaveis controladas para ativar o plano fixo de ataque final.")]
    public float minimumConstructionControlledForFinalAttackPlan = DefaultMinimumConstructionControlledForFinalAttackPlan;
}
