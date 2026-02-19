using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RPSAttackEntry
{
    [Header("Chave: Ataque")]
    [Tooltip("Classe da unidade atacante.")]
    public GameUnitClass unitClass = GameUnitClass.Infantry;

    [Tooltip("Categoria da arma usada no ataque.")]
    public WeaponCategory weaponCategory = WeaponCategory.AntiInfantaria;

    [Tooltip("Classe da unidade alvo.")]
    public GameUnitClass targetClass = GameUnitClass.Infantry;

    [Tooltip("Bonus de ataque para esta combinacao.")]
    public int attackBonus = 0;

    [TextArea]
    [Tooltip("Anotacoes livres para esta entrada.")]
    public string notes;

    [TextArea]
    [Tooltip("Texto consolidado da regra de ataque (gerado automaticamente).")]
    [SerializeField] private string rpsAttackText;

    public string RpsAttackText => rpsAttackText;

    public bool Matches(GameUnitClass attackerClass, WeaponCategory category, GameUnitClass defenderClass)
    {
        return unitClass == attackerClass
            && weaponCategory == category
            && targetClass == defenderClass;
    }

    public void RefreshComputedText()
    {
        string bonus = attackBonus.ToString("+0;-0;+0");
        rpsAttackText = $"{unitClass} com {weaponCategory} ataca {targetClass} com RPS Ataque {bonus}";
    }
}

[System.Serializable]
public class RPSDefenseEntry
{
    [Header("Chave: Defesa")]
    [Tooltip("Classe da unidade que esta defendendo (alvo).")]
    public GameUnitClass targetClass = GameUnitClass.Infantry;

    [Tooltip("Classe da unidade atacante.")]
    public GameUnitClass unitClass = GameUnitClass.Infantry;

    [Tooltip("Categoria da arma usada no ataque.")]
    public WeaponCategory weaponCategory = WeaponCategory.AntiInfantaria;

    [Tooltip("Bonus de defesa para esta combinacao.")]
    public int defenseBonus = 0;

    [TextArea]
    [Tooltip("Anotacoes livres para esta entrada.")]
    public string notes;

    [TextArea]
    [Tooltip("Texto consolidado da regra de defesa (gerado automaticamente).")]
    [SerializeField] private string rpsDefenseText;

    public string RpsDefenseText => rpsDefenseText;

    public bool Matches(GameUnitClass defenderClass, GameUnitClass attackerClass, WeaponCategory category)
    {
        return targetClass == defenderClass
            && unitClass == attackerClass
            && weaponCategory == category;
    }

    public void RefreshComputedText()
    {
        string bonus = defenseBonus.ToString("+0;-0;+0");
        rpsDefenseText = $"{targetClass} se defende de {unitClass} com {weaponCategory} com RPS Defesa {bonus}";
    }
}

[System.Serializable]
public class RPSMapEntry
{
    [Tooltip("Regra de ataque da entrada.")]
    public RPSAttackEntry ataque = new RPSAttackEntry();

    [Tooltip("Regra de defesa da entrada.")]
    public RPSDefenseEntry defesa = new RPSDefenseEntry();

    public void RefreshComputedTexts()
    {
        if (ataque == null)
            ataque = new RPSAttackEntry();
        if (defesa == null)
            defesa = new RPSDefenseEntry();

        ataque.RefreshComputedText();
        defesa.RefreshComputedText();
    }
}

[CreateAssetMenu(menuName = "Game/RPS/RPS Data", fileName = "RPSData")]
public class RPSData : ScriptableObject
{
    [Tooltip("Mapa RPS. Cada entrada contem dois elementos: ataque e defesa.")]
    [SerializeField] private List<RPSMapEntry> entries = new List<RPSMapEntry>();

    public IReadOnlyList<RPSMapEntry> Entries => entries;

    public bool TryResolveAttackBonus(
        GameUnitClass attackerClass,
        WeaponCategory category,
        GameUnitClass defenderClass,
        out int attackBonus,
        out RPSAttackEntry matchedEntry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            RPSMapEntry entry = entries[i];
            if (entry == null || entry.ataque == null)
                continue;

            if (!entry.ataque.Matches(attackerClass, category, defenderClass))
                continue;

            attackBonus = entry.ataque.attackBonus;
            matchedEntry = entry.ataque;
            return true;
        }

        attackBonus = 0;
        matchedEntry = null;
        return false;
    }

    public bool TryResolveDefenseBonus(
        GameUnitClass defenderClass,
        GameUnitClass attackerClass,
        WeaponCategory category,
        out int defenseBonus,
        out RPSDefenseEntry matchedEntry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            RPSMapEntry entry = entries[i];
            if (entry == null || entry.defesa == null)
                continue;

            if (!entry.defesa.Matches(defenderClass, attackerClass, category))
                continue;

            defenseBonus = entry.defesa.defenseBonus;
            matchedEntry = entry.defesa;
            return true;
        }

        defenseBonus = 0;
        matchedEntry = null;
        return false;
    }

    private void OnValidate()
    {
        if (entries == null)
            entries = new List<RPSMapEntry>();

        for (int i = 0; i < entries.Count; i++)
        {
            RPSMapEntry entry = entries[i];
            if (entry == null)
            {
                entries[i] = new RPSMapEntry();
                entry = entries[i];
            }

            entry.RefreshComputedTexts();
        }
    }
}
