using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Combat Modifier Database", fileName = "CombatModifierDatabase")]
public class CombatModifierDatabase : ScriptableObject
{
    [SerializeField] private List<CombatModifierData> modifiers = new List<CombatModifierData>();

    public IReadOnlyList<CombatModifierData> Modifiers => modifiers;
}

