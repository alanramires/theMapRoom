using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WeaponTargetPriorityRule
{
    [Tooltip("Categoria da arma.")]
    public WeaponCategory weaponCategory = WeaponCategory.AntiInfantaria;

    [Tooltip("Classes de alvo preferenciais para esta categoria de arma.")]
    public List<GameUnitClass> preferredTargetClasses = new List<GameUnitClass>();
}

[CreateAssetMenu(menuName = "Game/Weapons/Weapon Priority Data", fileName = "WeaponPriorityData")]
public class WeaponPriorityData : ScriptableObject
{
    [Tooltip("Regras de prioridade de alvo por categoria de arma.")]
    [SerializeField] private List<WeaponTargetPriorityRule> rules = new List<WeaponTargetPriorityRule>();

    public IReadOnlyList<WeaponTargetPriorityRule> Rules => rules;

    public bool IsPreferredTarget(WeaponCategory category, GameUnitClass targetClass)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            WeaponTargetPriorityRule rule = rules[i];
            if (rule == null || rule.weaponCategory != category)
                continue;

            return rule.preferredTargetClasses != null && rule.preferredTargetClasses.Contains(targetClass);
        }

        return false;
    }

    private void OnValidate()
    {
        if (rules == null)
            rules = new List<WeaponTargetPriorityRule>();
    }
}
