using UnityEngine;

public enum CombatEliteComparisonMode
{
    [InspectorName("Ignore")]
    Ignore = 0,
    [InspectorName("Owner > Opponent")]
    AttackerGreater = 1,
    [InspectorName("Owner < Opponent")]
    DefenderGreater = 2,
    [InspectorName("Owner != Opponent")]
    Different = 3,
    [InspectorName("Owner == Opponent")]
    Equal = 4,
    [InspectorName("Owner <= Opponent")]
    OwnerLessOrEqual = 5,
    [InspectorName("Owner >= Opponent")]
    OwnerGreaterOrEqual = 6
}

public enum CombatModifierType
{
    Attack = 0,
    Defense = 1
}

[CreateAssetMenu(menuName = "Game/Combat/Combat Modifier Data", fileName = "CombatModifier_")]
public class CombatModifierData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    [Tooltip("Define se o filtro/categoria de arma desta elite e avaliado no contexto de ataque do owner ou defesa contra a arma recebida.")]
    public CombatModifierType modifierType = CombatModifierType.Attack;

    [Header("Filters")]
    [Tooltip("Classe alvo do oponente para este modificador.")]
    public GameUnitClass requiredOpponentClass = GameUnitClass.Jet;
    [Tooltip("Categoria de arma exigida para este modificador.")]
    public WeaponCategory requiredWeaponCategory = WeaponCategory.AntiAerea;

    [Header("Elite Comparison (Owner vs Opponent)")]
    [Tooltip("Comparacao entre elite de quem possui o modifier (Owner) e elite do oponente (Opponent).")]
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
        WeaponCategory opponentWeaponCategory,
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

        WeaponCategory categoryToCheck = modifierType == CombatModifierType.Defense
            ? opponentWeaponCategory
            : ownerWeaponCategory;

        if (categoryToCheck != requiredWeaponCategory)
        {
            reason = "categoria de arma nao atende";
            return false;
        }

        int diff = ownerEliteLevel - opponentEliteLevel;
        int absDiff = Mathf.Abs(diff);
        bool bothZeroElite = ownerEliteLevel == 0 && opponentEliteLevel == 0;
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
                // House rule: comparativo de igualdade nao ativa quando ambos estao em elite 0.
                passesElite = absDiff == 0 && !bothZeroElite;
                break;
            case CombatEliteComparisonMode.OwnerLessOrEqual:
                passesElite = (diff == 0 && !bothZeroElite) || -diff >= minEliteDifference;
                break;
            case CombatEliteComparisonMode.OwnerGreaterOrEqual:
                passesElite = (diff == 0 && !bothZeroElite) || diff >= minEliteDifference;
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
