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

    [Tooltip("Enum AIStance correspondente a esta postura. Usado pelo sistema de comportamento por unidade.")]
    public AIStance stanceType;

    [Header("Battle Stance Activation")]
    [Tooltip("Tipo da condicao que ativa esta postura.")]
    public PlanConditionType activationType = PlanConditionType.AlwaysActive;

    [Tooltip("Limiar percentual (0–100) usado pela condicao de ativacao.")]
    [Range(0f, 100f)]
    public float threshold = 50f;

}
