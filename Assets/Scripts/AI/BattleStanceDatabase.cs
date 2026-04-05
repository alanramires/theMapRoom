using UnityEngine;

/// <summary>
/// Banco de posturas de batalha (AIStance).
/// Define o comportamento modificador para cada postura do time: ataque, defesa e invasão.
/// </summary>
[CreateAssetMenu(menuName = "Game/AI/Battle Stance Database", fileName = "BattleStanceDatabase")]
public class BattleStanceDatabase : ScriptableObject
{
    [Header("Posturas")]
    public BattleStanceData attackStance;
    public BattleStanceData defendStance;
    public BattleStanceData invasionStance;

    public BattleStanceData GetStanceData(AIStance stance)
    {
        switch (stance)
        {
            case AIStance.Defend:   return defendStance;
            case AIStance.Invasion: return invasionStance;
            default:                return attackStance;
        }
    }
}
