using UnityEngine;

/// <summary>
/// Uma chave de captura e o quanto ela rende NESTA construcao.
///
/// Mesmo formato do TerrainSkillCostOverride: o alvo lista pares
/// (skill, valor). A habilidade continua sendo etiqueta inerte — quem define o
/// que ela abre, e agora tambem o quanto ela rende, e o lugar que a pendura.
///
/// E o que permite um bunker onde "Captura Construcoes" vale 0,8 e um robo vale
/// 1,5, sem nenhuma linha de codigo por construcao.
///
/// Ver docs/manual/01_principios_e_vocabulario.md.
/// </summary>
[System.Serializable]
public class CaptureSkillEfficiency
{
    [Tooltip("Habilidade que abre a captura desta construcao.")]
    public SkillData skill;

    [Tooltip("Multiplicador do poder de captura desta unidade AQUI.\n\n" +
             "1 = normal. 0,5 = metade. 1,5 = uma vez e meia.\n\n" +
             "Nao aceita zero: 'tem a chave e nao consegue' seria confuso — " +
             "para isso, basta nao listar a skill.\n\n" +
             "Multiplica com a penalidade de pre-requisito da construcao, que e " +
             "outra conta: 0,8 aqui com pre-requisito faltando da 0,8 x 0,5 = 0,4.")]
    [Min(0.01f)]
    public float efficiency = 1f;
}
