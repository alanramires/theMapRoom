using UnityEngine;

[System.Serializable]
public class TerrainSkillCostOverride
{
    [Tooltip("Skill que altera o custo de autonomia neste terreno.")]
    public SkillData skill;

    [Tooltip("Novo custo de autonomia para entrar no hex quando a unidade tiver a skill.")]
    [Min(1)]
    public int autonomyCost = 1;
}
