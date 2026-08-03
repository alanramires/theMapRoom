using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/Skill Data", fileName = "SkillData_")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico da skill (ex.: vtol, stovl, aircraft landing).")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string displayName;

    [TextArea]
    public string description;

    // UMA HABILIDADE E UMA CHAVE, NAO UM PODER.
    //
    // Esta classe carrega IDENTIDADE e nada mais. Nenhum campo aqui pode
    // conceder capacidade — o nome na ficha nao faz nada sozinho, e quem define
    // o que a etiqueta abre e o ALVO: a montanha diz "so entra quem for
    // alpino", a construcao diz quem a captura, em
    // ConstructionData.requiredSkillsToCapture.
    //
    // Houve aqui um `canCaptureConstructions` — a unica excecao do projeto, a
    // skill declarando o proprio poder. Por causa dele a etiqueta nao podia ser
    // renomeada livremente. Saiu na v7.0.2.
    //
    // O teste antes de acrescentar qualquer campo: o designer consegue
    // renomear esta skill para qualquer coisa e tudo continua funcionando? Se
    // nao, o poder esta no lugar errado.
    //
    // Ver docs/manual/01_principios_e_vocabulario.md.
}
