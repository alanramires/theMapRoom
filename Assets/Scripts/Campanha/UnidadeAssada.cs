using UnityEngine;

/// <summary>
/// Uma unidade que ja esta em campo quando o quadrante abre, em coordenada LOCAL.
///
/// ARTEFATO: sai do bake, e regerada pelo botao, nunca editada a mao. A regra e a
/// mesma das construcoes — "se esta no retangulo, vem como esta". O que decide
/// quem comeca com o que e o DESENHO da cena de autoria, nao uma configuracao a
/// parte: pinte tres blindados da IA no norte do mapa e eles estarao la.
///
/// Um quadrante sem unidades assadas nasce vazio dos dois lados, e a partida
/// comeca comprando — que e como os quatro quadrantes do fixture jogam hoje. A
/// lista existir vazia nao custa nada; o que custa e nao ter onde pintar.
///
/// O DONO E O SLOT. Vale aqui a mesma lei das construcoes: a cor gravada e a da
/// cena de autoria, e as cores da partida sao escolhidas no menu. Assar Azul no
/// slot 0 nao pode fazer a unidade nascer azul numa partida Amarelo x Vermelho.
/// </summary>
[System.Serializable]
public class UnidadeAssada
{
    [Tooltip("Id no UnitDatabase. E por ele que o spawner resolve o prefab.")]
    public string unitId;

    [Tooltip("Coordenada LOCAL do quadrante — ja transladada, (0,0) e o canto.")]
    public int localX;
    public int localY;

    [Tooltip(
        "A cor com que a unidade foi PINTADA na autoria. E registro, nao fonte: quem "
        + "manda no dono e o slotIndex.\n\n"
        + "So vale como cor de verdade quando slotIndex e -1 — unidade de time FIXO, "
        + "que por definicao nao acompanha slot nenhum.")]
    public TeamId teamId = TeamId.Neutral;

    [Tooltip("Slot logico do dono, e a VERDADE sobre de quem a unidade e. -1 = sem slot.")]
    public int slotIndex = -1;

    [Tooltip("So pra log e Inspector; o spawner nao usa.")]
    public string displayName;

    public Vector3Int LocalCell => new Vector3Int(localX, localY, 0);

    public override string ToString()
    {
        return $"{unitId} @ ({localX},{localY}) slot {slotIndex} / {teamId}";
    }
}
