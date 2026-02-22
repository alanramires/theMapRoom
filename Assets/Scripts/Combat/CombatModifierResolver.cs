using System.Text;
using UnityEngine;

public readonly struct CombatModifierSummary
{
    public readonly int ownerAttack;
    public readonly int ownerDefense;
    public readonly int opponentAttack;
    public readonly int opponentDefense;
    public readonly int appliedCount;
    public readonly string reason;

    public CombatModifierSummary(
        int ownerAttack,
        int ownerDefense,
        int opponentAttack,
        int opponentDefense,
        int appliedCount,
        string reason)
    {
        this.ownerAttack = ownerAttack;
        this.ownerDefense = ownerDefense;
        this.opponentAttack = opponentAttack;
        this.opponentDefense = opponentDefense;
        this.appliedCount = appliedCount;
        this.reason = reason ?? string.Empty;
    }
}

public static class CombatModifierResolver
{
    public static CombatModifierSummary Resolve(UnitManager ownerUnit, UnitManager opponentUnit, WeaponCategory ownerWeaponCategory)
    {
        if (ownerUnit == null)
            return new CombatModifierSummary(0, 0, 0, 0, 0, "owner nulo");

        if (!ownerUnit.TryGetUnitData(out UnitData ownerData) || ownerData == null)
            return new CombatModifierSummary(0, 0, 0, 0, 0, "owner sem UnitData");

        if (ownerData.combatModifiers == null || ownerData.combatModifiers.Count == 0)
            return new CombatModifierSummary(0, 0, 0, 0, 0, "owner sem combat modifiers");

        int ownerElite = Mathf.Max(0, ownerData.eliteLevel);

        UnitData opponentData = null;
        if (opponentUnit != null)
            opponentUnit.TryGetUnitData(out opponentData);

        GameUnitClass opponentClass = opponentData != null ? opponentData.unitClass : GameUnitClass.Infantry;
        int opponentElite = opponentData != null ? Mathf.Max(0, opponentData.eliteLevel) : 0;

        int totalOwnerAttack = 0;
        int totalOwnerDefense = 0;
        int totalOpponentAttack = 0;
        int totalOpponentDefense = 0;
        int appliedCount = 0;
        StringBuilder summary = new StringBuilder();

        for (int i = 0; i < ownerData.combatModifiers.Count; i++)
        {
            CombatModifierData modifier = ownerData.combatModifiers[i];
            if (modifier == null)
                continue;

            if (!modifier.TryGetCombatRpsModifiers(
                ownerElite,
                ownerWeaponCategory,
                opponentClass,
                opponentElite,
                out int ownerAtkMod,
                out int ownerDefMod,
                out int opponentAtkMod,
                out int opponentDefMod,
                out string reason))
            {
                continue;
            }

            totalOwnerAttack += ownerAtkMod;
            totalOwnerDefense += ownerDefMod;
            totalOpponentAttack += opponentAtkMod;
            totalOpponentDefense += opponentDefMod;
            appliedCount++;

            if (summary.Length > 0)
                summary.Append(" || ");

            string modifierName = !string.IsNullOrWhiteSpace(modifier.displayName)
                ? modifier.displayName
                : (!string.IsNullOrWhiteSpace(modifier.id) ? modifier.id : modifier.name);
            summary.Append(modifierName);
            summary.Append(": ownAtk ");
            summary.Append(ownerAtkMod);
            summary.Append(", ownDef ");
            summary.Append(ownerDefMod);
            summary.Append(", oppAtk ");
            summary.Append(opponentAtkMod);
            summary.Append(", oppDef ");
            summary.Append(opponentDefMod);
            summary.Append(" (");
            summary.Append(reason);
            summary.Append(")");
        }

        if (appliedCount == 0)
            return new CombatModifierSummary(0, 0, 0, 0, 0, "sem combat modifier aplicavel");

        return new CombatModifierSummary(
            totalOwnerAttack,
            totalOwnerDefense,
            totalOpponentAttack,
            totalOpponentDefense,
            appliedCount,
            summary.ToString());
    }
}
