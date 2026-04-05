using UnityEngine;

/// <summary>
/// Configuração de comportamento para uma postura de batalha (AIStance).
/// Define identidade, condição de ativação e modificadores que afetam todas as unidades do time.
/// </summary>
[CreateAssetMenu(menuName = "Game/AI/Battle Stance Data", fileName = "BattleStance_")]
public class BattleStanceData : ScriptableObject
{
    [Header("Icon")]
    [Tooltip("Sprite exibido no HUD das unidades enquanto esta postura estiver ativa. Deixe vazio para sem icone (ex: Attack).")]
    public Sprite stanceIcon;

    [Header("Identity")]
    [Tooltip("Identificador unico desta postura. Ex: attack, defend, invasion.")]
    public string battleId;

    [Tooltip("Nome legivel exibido no debug e UI.")]
    public string displayName;

    [Header("Battle Stance Activation")]
    [Tooltip("Tipo da condicao que ativa esta postura.")]
    public PlanConditionType activationType = PlanConditionType.AlwaysActive;

    [Tooltip("Limiar percentual (0–100) usado pela condicao de ativacao.")]
    [Range(0f, 100f)]
    public float threshold = 50f;

    [Header("Stance Behavior")]
    [Tooltip("Suspende objetivo de captura enquanto esta postura estiver ativa. Unidades capturadoras defendem ao inves de avancar.")]
    public bool suspendCaptureObjective = false;

    [Tooltip("Suspende coesao de escolta enquanto esta postura estiver ativa. Escoltas agem de forma independente.")]
    public bool suspendEscortCohesion = false;

    [Tooltip("Raio de engajamento a partir do HQ proprio (hexes). Ativa o modo de defesa quando inimigo entra neste raio. Unidades ignoram inimigos alem dele. 0 = sem limite.")]
    public int hqEngagementRadius = 0;

    [Tooltip("Raio a partir do HQ (hexes) dentro do qual unidades sao convocadas como reforco de defesa. Deve ser >= hqEngagementRadius. 0 = desativado.")]
    public int defenderPullRadius = 0;
}
