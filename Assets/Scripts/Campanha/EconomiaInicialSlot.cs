using UnityEngine;

/// <summary>
/// Caixa inicial de UM slot num quadrante.
///
/// AUTORAL, NAO ASSADO. Ao contrario de tudo que mora sob o header "Assado", isto
/// e digitado a mao e sobrevive ao bake — porque dinheiro nao e espacial. A tropa
/// inicial vem do bake porque ela ESTA no retangulo, e vale a regra "se esta no
/// retangulo, vem como esta". Cem mil no bolso nao esta em lugar nenhum da cena:
/// e uma afirmacao sobre a partida.
///
/// ⚠️ Se um dia isto for movido para junto dos campos assados, o proximo autor que
/// limpar a secao "artefato" apaga o numero junto, sem erro nenhum.
///
/// A RENDA POR RODADA NAO MORA AQUI, e nao deve passar a morar. Ela e a soma do
/// capturedIncoming das construcoes que o slot controla, recalculada a cada
/// inicio de turno por RecalculateIncomePerTurnForAllPlayers — que faz ATRIBUICAO,
/// nao soma. Um valor declarado seria apagado no primeiro recalculo, sem erro; e
/// se nao fosse apagado, capturar cidade deixaria de valer alguma coisa.
///
/// O quadrante ja manda na renda por rodada: pelos predios que ele assa.
/// </summary>
[System.Serializable]
public class EconomiaInicialSlot
{
    [Tooltip("Slot logico do dono. O dinheiro e do SLOT, nunca da cor — as cores sao escolhidas no menu.")]
    public int slotIndex = -1;

    [Tooltip(
        "Caixa que este slot ganha UMA vez, no primeiro inicio de turno dele, somada "
        + "a renda das construcoes. Quase sempre 0: o normal e comecar so com a renda.")]
    [Min(0)]
    public int startMoney;

    public override string ToString() => $"slot {slotIndex}: {startMoney}";
}
