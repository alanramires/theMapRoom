using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/RPS/RPS Database", fileName = "RPSDatabase")]
public class RPSDatabase : ScriptableObject
{
    [Tooltip("Colecao de tabelas RPS. Ordem da lista define prioridade (primeiro match vence).")]
    [SerializeField] private List<RPSData> tables = new List<RPSData>();

    public IReadOnlyList<RPSData> Tables => tables;

    public bool TryResolveAttackBonus(
        GameUnitClass attackerClass,
        WeaponCategory category,
        GameUnitClass defenderClass,
        out int attackBonus,
        out RPSAttackEntry matchedEntry,
        out RPSData sourceTable)
    {
        for (int i = 0; i < tables.Count; i++)
        {
            RPSData table = tables[i];
            if (table == null)
                continue;

            if (!table.TryResolveAttackBonus(attackerClass, category, defenderClass, out int bonus, out RPSAttackEntry entry))
                continue;

            attackBonus = bonus;
            matchedEntry = entry;
            sourceTable = table;
            return true;
        }

        attackBonus = 0;
        matchedEntry = null;
        sourceTable = null;
        return false;
    }

    public bool TryResolveDefenseBonus(
        GameUnitClass defenderClass,
        GameUnitClass attackerClass,
        WeaponCategory category,
        out int defenseBonus,
        out RPSDefenseEntry matchedEntry,
        out RPSData sourceTable)
    {
        for (int i = 0; i < tables.Count; i++)
        {
            RPSData table = tables[i];
            if (table == null)
                continue;

            if (!table.TryResolveDefenseBonus(defenderClass, attackerClass, category, out int bonus, out RPSDefenseEntry entry))
                continue;

            defenseBonus = bonus;
            matchedEntry = entry;
            sourceTable = table;
            return true;
        }

        defenseBonus = 0;
        matchedEntry = null;
        sourceTable = null;
        return false;
    }

    private void OnValidate()
    {
        if (tables == null)
            tables = new List<RPSData>();
    }
}
