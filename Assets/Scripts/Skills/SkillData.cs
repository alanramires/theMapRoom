using UnityEngine;
using UnityEngine.Serialization;

public enum SkillEliteComparisonMode
{
    Ignore = 0,
    AttackerGreater = 1,
    DefenderGreater = 2,
    Different = 3,
    Equal = 4
}

[CreateAssetMenu(menuName = "Game/Skills/Skill Data", fileName = "SkillData_")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico da skill (ex.: guerrilha, alpino, off-road).")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string displayName;

    [TextArea]
    public string description;

    [Header("Combat RPS Modifier")]
    [Tooltip("Habilita modificador adicional sobre o RPS base durante combate.")]
    public bool enableCombatRpsModifier = false;

    [Tooltip("Se ativo, exige classe especifica para a unidade dona da skill.")]
    public bool filterOwnerClass = false;

    [Tooltip("Classe exigida para a unidade dona da skill quando o filtro estiver ativo.")]
    public GameUnitClass requiredOwnerClass = GameUnitClass.Jet;

    [Tooltip("Se ativo, exige classe especifica para a unidade oponente.")]
    public bool filterOpponentClass = false;

    [Tooltip("Classe exigida para a unidade oponente quando o filtro estiver ativo.")]
    public GameUnitClass requiredOpponentClass = GameUnitClass.Jet;

    [Tooltip("Se ativo, exige categoria especifica da arma usada pela unidade dona da skill.")]
    public bool filterWeaponCategory = false;

    [Tooltip("Categoria de arma exigida para aplicar a skill.")]
    public WeaponCategory requiredWeaponCategory = WeaponCategory.AntiAerea;

    [Tooltip("Se ativo, exige que owner e opponent tenham a mesma classe de unidade.")]
    public bool requireSameUnitClass = false;

    [Tooltip("Regra de comparacao entre elite do owner (atacante da skill) e opponent.")]
    public SkillEliteComparisonMode eliteComparison = SkillEliteComparisonMode.Ignore;

    [Tooltip("Diferenca minima absoluta de elite para considerar a comparacao acima.")]
    [Min(0)] public int minEliteDifference = 1;

    [Tooltip("Bonus aplicado ao RPS de ataque do owner quando a skill ativa.")]
    [FormerlySerializedAs("attackRpsModifier")]
    public int ownerAttackRpsModifier = 0;

    [Tooltip("Bonus aplicado ao RPS de defesa do owner quando a skill ativa.")]
    [FormerlySerializedAs("defenseRpsModifier")]
    public int ownerDefenseRpsModifier = 0;

    [Tooltip("Bonus aplicado ao RPS de ataque do opponent quando a skill ativa.")]
    public int opponentAttackRpsModifier = 0;

    [Tooltip("Bonus aplicado ao RPS de defesa do opponent quando a skill ativa.")]
    public int opponentDefenseRpsModifier = 0;

    public bool TryGetCombatRpsModifiers(
        GameUnitClass ownerClass,
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
        reason = "skill sem efeito";

        if (!enableCombatRpsModifier)
        {
            reason = "modificador de combate desativado";
            return false;
        }

        if (filterOwnerClass && ownerClass != requiredOwnerClass)
        {
            reason = "classe do owner nao atende";
            return false;
        }

        if (filterOpponentClass && opponentClass != requiredOpponentClass)
        {
            reason = "classe do opponent nao atende";
            return false;
        }

        if (filterWeaponCategory && ownerWeaponCategory != requiredWeaponCategory)
        {
            reason = "categoria de arma nao atende";
            return false;
        }

        if (requireSameUnitClass && ownerClass != opponentClass)
        {
            reason = "classes diferentes";
            return false;
        }

        int diff = ownerEliteLevel - opponentEliteLevel;
        int absDiff = Mathf.Abs(diff);
        bool passesElite = true;
        switch (eliteComparison)
        {
            case SkillEliteComparisonMode.Ignore:
                passesElite = true;
                break;
            case SkillEliteComparisonMode.AttackerGreater:
                passesElite = diff >= minEliteDifference;
                break;
            case SkillEliteComparisonMode.DefenderGreater:
                passesElite = -diff >= minEliteDifference;
                break;
            case SkillEliteComparisonMode.Different:
                passesElite = absDiff >= minEliteDifference;
                break;
            case SkillEliteComparisonMode.Equal:
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
