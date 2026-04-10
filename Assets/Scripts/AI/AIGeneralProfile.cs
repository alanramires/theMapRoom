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
    [Tooltip("Perfil de comportamento de IA (avaliacao de postura etc). Se nulo, usa BeginnerAIProfile como fallback.")]
    public AIProfile behaviorProfile;

    [Header("Planner Runtime")]
    [Min(0), Tooltip("Turnos sem progresso antes de considerar plano estagnado para realocacao.")]
    public int stagnationTurns = 2;
    [Min(0), Tooltip("Maximo de planos de setor variaveis ativos simultaneamente.")]
    public int maxVariablePlans = 3;
    [Min(0), Tooltip("Maximo de planos de backup/protect ativos simultaneamente.")]
    public int maxBackupPlans = 0;

    [Header("Planner Gates")]
    [Min(1), Tooltip("Raio minimo em hexes para detectar ameaca proxima ao HQ (usado em HasVisibleEnemyNearSector para setores de base).")]
    public int minimumRangeForDefensePlan = DefaultMinimumRangeForDefensePlan;
}
