using UnityEngine;

public enum CombatEliteComparisonMode
{
    Ignore = 0,
    AttackerGreater = 1,
    DefenderGreater = 2,
    Different = 3,
    Equal = 4
}

[CreateAssetMenu(menuName = "Game/Combat/Combat Modifier Data", fileName = "CombatModifier_")]
public class CombatModifierData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [TextArea] public string description;

    [Header("Filters")]
    [Tooltip("Classe alvo do oponente para este modificador.")]
    public GameUnitClass requiredOpponentClass = GameUnitClass.Jet;
    [Tooltip("Categoria de arma exigida para este modificador.")]
    public WeaponCategory requiredWeaponCategory = WeaponCategory.AntiAerea;

    [Header("Elite Comparison")]
    public CombatEliteComparisonMode eliteComparison = CombatEliteComparisonMode.AttackerGreater;
    [Min(0)] public int minEliteDifference = 1;

    [Header("RPS Modifiers")]
    public int ownerAttackRpsModifier = 0;
    public int ownerDefenseRpsModifier = 0;
    public int opponentAttackRpsModifier = 0;
    public int opponentDefenseRpsModifier = 0;

    public bool TryGetCombatRpsModifiers(
        int ownerEliteLevel,
        WeaponCategory ownerWeaponCategory,
        GameUnitClass opponentClass,
        int opponentEliteLevel,
        out int ownerAttackModifier,
        out int ownerDefenseModifier,
        out int opponentAttackModifier,
        out int opponentDefenseModifier,
        out string reason)
    {
        ownerAttackModifier = 0;
        ownerDefenseModifier = 0;
        opponentAttackModifier = 0;
        opponentDefenseModifier = 0;
        reason = "modifier sem efeito";

        if (opponentClass != requiredOpponentClass)
        {
            reason = "classe do opponent nao atende";
            return false;
        }

        if (ownerWeaponCategory != requiredWeaponCategory)
        {
            reason = "categoria de arma nao atende";
            return false;
        }

        int diff = ownerEliteLevel - opponentEliteLevel;
        int absDiff = Mathf.Abs(diff);
        bool passesElite = true;
        switch (eliteComparison)
        {
            case CombatEliteComparisonMode.Ignore:
                passesElite = true;
                break;
            case CombatEliteComparisonMode.AttackerGreater:
                passesElite = diff >= minEliteDifference;
                break;
            case CombatEliteComparisonMode.DefenderGreater:
                passesElite = -diff >= minEliteDifference;
                break;
            case CombatEliteComparisonMode.Different:
                passesElite = absDiff >= minEliteDifference;
                break;
            case CombatEliteComparisonMode.Equal:
                passesElite = absDiff == 0;
                break;
        }

        if (!passesElite)
        {
            reason = "comparacao de elite nao atende";
            return false;
        }

        ownerAttackModifier = ownerAttackRpsModifier;
        ownerDefenseModifier = ownerDefenseRpsModifier;
        opponentAttackModifier = opponentAttackRpsModifier;
        opponentDefenseModifier = opponentDefenseRpsModifier;
        reason = "ok";
        return true;
    }
}
