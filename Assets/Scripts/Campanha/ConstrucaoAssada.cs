using UnityEngine;

/// <summary>
/// Uma construcao dentro do retangulo de um quadrante, em coordenada LOCAL.
///
/// ARTEFATO: sai do bake, e regerada pelo botao, nunca editada a mao. O que vale
/// e o que esta pintado na cena de autoria — "se esta no retangulo, vem como
/// esta", a mesma regra das unidades. Nao existe flag de "tem QG": a escolha e o
/// desenho.
///
/// Guarda o dono junto porque um QG neutro nao seria QG de ninguem — mas o dono e
/// o SLOT, nao a cor. A cor da autoria e so registro: quem joga escolhe as duas
/// cores no menu, e assar Azul no slot 0 nao pode fazer o QG nascer Azul numa
/// partida Amarelo contra Vermelho.
/// </summary>
[System.Serializable]
public class ConstrucaoAssada
{
    [Tooltip("Id no ConstructionDatabase. E por ele que o spawner resolve o prefab.")]
    public string constructionId;

    [Tooltip("Coordenada LOCAL do quadrante — ja transladada, (0,0) e o canto.")]
    public int localX;
    public int localY;

    [Tooltip(
        "A cor com que a peca foi PINTADA na autoria. E registro, nao fonte: quem manda "
        + "no dono e o slotIndex, porque as cores da partida sao escolhidas no menu e nao "
        + "tem relacao com as da cena de autoria.\n\n"
        + "So vale como cor de verdade quando slotIndex e -1 — conteudo de time FIXO, que "
        + "por definicao nao acompanha slot nenhum.")]
    public TeamId teamId = TeamId.Neutral;

    [Tooltip("Slot logico do dono, e a VERDADE sobre de quem a peca e. -1 = sem slot.")]
    public int slotIndex = -1;

    [Tooltip(
        "Rotulo estrategico consumido pelo planner da IA. SEM ele a construcao nasce em "
        + "ConstructionSector.Alpha (o default do enum NAO e None) e o plano degenera em silencio.")]
    public ConstructionSector sector = ConstructionSector.None;

    [Tooltip("Ancora de setor — o planner usa pra escolher para onde o eixo avanca.")]
    public bool isAnchorSector;

    [Tooltip("Pontos de captura iniciais. -1 usa o maximo da configuracao do tipo.")]
    public int initialCapturePoints = -1;

    [Tooltip(
        "Configuracao DESTA instancia: o que ela vende, que servicos oferece, se e QG, "
        + "se e capturavel. A cena de autoria e a lei — uma fabrica leve que NAO vende "
        + "radar movel tem de nascer sem radar movel.\n\n"
        + "Sem isto o spawn cai na configuracao do TIPO, e toda fabrica do mapa vira "
        + "igual: a customizacao por instancia some sem erro nenhum.")]
    public ConstructionSiteRuntime siteRuntime = new ConstructionSiteRuntime();

    [Tooltip("So pra log e Inspector; o spawner nao usa.")]
    public string displayName;

    public Vector3Int LocalCell => new Vector3Int(localX, localY, 0);

    public override string ToString()
    {
        return $"{constructionId} @ ({localX},{localY}) slot {slotIndex} / {teamId}";
    }
}
