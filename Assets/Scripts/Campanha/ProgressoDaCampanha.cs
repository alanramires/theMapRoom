using System.Collections.Generic;

/// <summary>
/// Responde "isto esta concluido?" nos tres niveis do mapa, e conta o progresso.
///
///   quadrante concluido = o ULTIMO vencedor registrado nele e o meu slot
///   campanha  concluida = todos os quadrantes dela concluidos
///   bloco     concluido = todas as campanhas dele concluidas
///   jogo zerado         = todos os blocos concluidos
///
/// NADA DISTO E GRAVADO alem do quadrante. Um campo "campanhaConcluida" seria um
/// segundo lugar da verdade, e divergiria na primeira vez que alguem rejogasse e
/// perdesse — o que e permitido e comum, porque o registro do quadrante e o
/// retrato da ULTIMA tentativa, nao da melhor. Aqui tudo e recalculado na hora.
///
/// A consequencia aceita de proposito: o progresso ANDA PARA TRAS. Da para estar
/// em 4/4 e voltar para 3/4 rejogando e perdendo, e nao existe ponto sem retorno.
/// Nao existe nenhum if(campanhaConcluida) em lugar nenhum porque nao existe o
/// campo para consultar: existe uma contagem, e ela responde toda vez que alguem
/// pergunta.
///
/// SERVICO BURRO. Recebe o no e o slot, devolve o fato. Nao sabe de quem e o
/// ponto de vista, nao procura MatchController, nao decide nada — quem chama
/// resolve o slot (TryGetSingleActiveLocalHumanSlot) e decide o que fazer com a
/// resposta. Intersecao, ranking e politica sao de quem consome.
///
/// O QUE ESTE ARQUIVO NAO FAZ: o portao de destrave (Liberado). Ele e a recursao
/// que DESCE — bloco libera campanha, que libera quadrante — e precisa de
/// navegacao de pai e de irmaos, que o MundoData nao expoe hoje. Concluido so
/// SOBE, e subir e de graca porque as listas de filhos ja existem.
/// </summary>
public static class ProgressoDaCampanha
{
    /// <summary>
    /// O quadrante e do slot? Sem registro = false, e "sem registro" nao e empate:
    /// e "ninguem tomou ainda".
    /// </summary>
    public static bool Concluido(
        MundoData mundo,
        CampanhaData campanha,
        QuadranteData quadrante,
        PlayerSlotId slot)
    {
        if (mundo == null || campanha == null || quadrante == null || !slot.IsValid)
            return false;

        if (!CampaignProgressStore.TryGetOwner(
                mundo.mundoId,
                campanha.campanhaId,
                quadrante.quadranteId,
                out PlayerSlotId dono))
        {
            return false;
        }

        return dono == slot;
    }

    /// <summary>
    /// Um dono por quadrante da campanha, NA ORDEM DA LISTA do asset. Slot
    /// invalido = ninguem tomou ainda.
    ///
    /// Ao contrario do ContarConquistados, isto nao pergunta "de quem?" — devolve
    /// o dono de CADA um, que e o que uma tela precisa para desenhar um simbolo
    /// por quadrante.
    ///
    /// A ORDEM E A DO AUTOR. O asset guarda os quadrantes numa lista, e e ela que
    /// sai daqui — sem ordenar, sem inventar criterio. Num mundo cujo layout e
    /// caotico nao existe ordem espacial que sirva, entao quem consome NAO deve
    /// prometer posicao: o mapa e quem mostra onde cada um fica.
    ///
    /// Reusa a lista recebida para nao alocar a cada frame.
    /// </summary>
    public static void ColetarDonos(
        MundoData mundo,
        CampanhaData campanha,
        List<PlayerSlotId> destino)
    {
        if (destino == null)
            return;

        destino.Clear();
        if (mundo == null || campanha?.quadrantes == null)
            return;

        for (int i = 0; i < campanha.quadrantes.Count; i++)
        {
            QuadranteData quadrante = campanha.quadrantes[i];
            if (quadrante == null)
                continue;

            destino.Add(
                CampaignProgressStore.TryGetOwner(
                    mundo.mundoId,
                    campanha.campanhaId,
                    quadrante.quadranteId,
                    out PlayerSlotId dono)
                    ? dono
                    : PlayerSlotId.Invalid);
        }
    }

    /// <summary>
    /// Quantos quadrantes da campanha sao do slot. E o numerador do "1/4".
    /// </summary>
    public static int ContarConquistados(MundoData mundo, CampanhaData campanha, PlayerSlotId slot)
    {
        if (campanha?.quadrantes == null)
            return 0;

        int total = 0;
        for (int i = 0; i < campanha.quadrantes.Count; i++)
        {
            if (Concluido(mundo, campanha, campanha.quadrantes[i], slot))
                total++;
        }

        return total;
    }

    /// <summary>
    /// O denominador do "1/4". Conta TODO quadrante da campanha, inclusive os sem
    /// bake.
    ///
    /// ⚠️ Isso e deliberado, e tem uma consequencia que vale saber: um quadrante
    /// sem bake nao pode ser jogado, logo nao pode ser concluido, logo a campanha
    /// que o contem nunca fecha. Excluir os sem bake esconderia um erro de autoria
    /// atras de um 4/4 que mente. A bancada ja avisa quem esta sem bake; aqui a
    /// conta so nao finge que ele nao existe.
    /// </summary>
    public static int ContarQuadrantes(CampanhaData campanha)
    {
        return campanha?.quadrantes != null ? campanha.quadrantes.Count : 0;
    }

    /// <summary>
    /// Campanha concluida: todos os quadrantes dela sao do slot.
    ///
    /// Campanha vazia devolve false — "nao tem nada para conquistar" nao e
    /// "conquistei tudo", e um mundo mal autorado nao deve zerar sozinho.
    /// </summary>
    public static bool Concluida(MundoData mundo, CampanhaData campanha, PlayerSlotId slot)
    {
        int total = ContarQuadrantes(campanha);
        if (total <= 0)
            return false;

        return ContarConquistados(mundo, campanha, slot) == total;
    }

    /// <summary>Bloco concluido: todas as campanhas dele concluidas.</summary>
    public static bool Concluido(MundoData mundo, BlocoData bloco, PlayerSlotId slot)
    {
        if (bloco?.campanhas == null || bloco.campanhas.Count == 0)
            return false;

        for (int i = 0; i < bloco.campanhas.Count; i++)
        {
            if (!Concluida(mundo, bloco.campanhas[i], slot))
                return false;
        }

        return true;
    }

    /// <summary>Jogo zerado: todos os blocos concluidos.</summary>
    public static bool JogoZerado(MundoData mundo, PlayerSlotId slot)
    {
        if (mundo?.blocos == null || mundo.blocos.Count == 0)
            return false;

        for (int i = 0; i < mundo.blocos.Count; i++)
        {
            if (!Concluido(mundo, mundo.blocos[i], slot))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Conveniencia para tela: "CONQUISTADOS 1/4" da campanha indicada.
    ///
    /// Devolve string vazia quando nao ha campanha — a tela decide se mostra nada
    /// ou um tracinho, que e decisao de apresentacao e nao deste servico.
    /// </summary>
    public static string FormatarProgresso(MundoData mundo, CampanhaData campanha, PlayerSlotId slot)
    {
        int total = ContarQuadrantes(campanha);
        if (total <= 0)
            return string.Empty;

        return $"{ContarConquistados(mundo, campanha, slot)}/{total}";
    }
}
