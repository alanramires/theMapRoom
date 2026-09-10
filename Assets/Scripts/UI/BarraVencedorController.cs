using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Um quadradinho por quadrante da campanha, pintado com a cor de quem o tomou.
///
/// SUBSTITUI A BARRA DE PROPORCAO, e o motivo e a forma casar com o dado: barra e
/// continua e pede que o jogador interprete area; quadrante e objeto contavel.
/// Quatro quadrados SAO quatro mapas, e o denominador para de precisar de
/// explicacao porque esta desenhado.
///
/// NAO E ESPACIAL, de proposito. A ordem e a da lista que chega — hoje ordenada
/// por quadranteId — e a navegacao do mapa e por DIRECAO, entao as duas nao
/// concordam. Prometer posicao aqui ensinaria uma relacao falsa; quem mostra onde
/// cada quadrante fica e o mapa, que ja pinta posse por cor. Aqui e so "quantos de
/// quantos".
///
/// Por isso tambem nao ha destaque do quadrante em foco: ele pularia de forma
/// aparentemente aleatoria enquanto o jogador anda pelo mapa.
///
/// COMPONENTE BURRO. Recebe a lista de donos e desenha. Quem sabe quais sao os
/// quadrantes e de quem eles sao e quem chama — ver ProgressoDaCampanha.ColetarDonos.
///
/// COMO LIGAR: ponha este componente no 'bar_vencedor', com UM filho Image
/// desativado servindo de molde (por padrao chamado 'quadrado_modelo') e um
/// Horizontal Layout Group no pai. Os quadrados sao clonados do molde, entao a
/// quantidade vem do mundo e nao da cena: uma campanha de sete quadrantes
/// funciona sem tocar no prefab.
/// </summary>
public class BarraVencedorController : MonoBehaviour
{
    [Header("Molde")]
    [Tooltip("Image desativada que serve de forma para os quadrados. Se vazio, procura por nome entre os filhos.")]
    [SerializeField] private Image quadradoModelo;
    [SerializeField] private string nomeDoModelo = "quadrado_modelo";

    [Header("Cores")]
    [Tooltip(
        "Quadrante que ninguem tomou ainda. NAO e empate nem neutro do jogo: e "
        + "ausencia de registro.\n\n"
        + "Se um dia virar moldura vazia em vez de preenchimento, e aqui que sai — "
        + "o resto do componente nao muda.")]
    [SerializeField] private Color corNaoJogado = new Color(1f, 1f, 1f, 0.18f);

    private readonly List<Image> quadrados = new List<Image>();
    private MatchController matchController;

    /// <summary>
    /// Desenha um quadrado por entrada. Slot invalido = ainda nao jogado.
    ///
    /// A COR SAI DO SLOT, nunca de constante: GetTeamIdForSlot resolve contra as
    /// cores DESTA sessao, as mesmas que o tint do mapa usa. Sem isso, um jogador
    /// que trocasse de cor veria o quadradinho discordar do mapa.
    /// </summary>
    public void Mostrar(IReadOnlyList<PlayerSlotId> donos)
    {
        if (!ResolverModelo())
            return;

        int total = donos != null ? donos.Count : 0;
        GarantirQuantidade(total);

        for (int i = 0; i < quadrados.Count; i++)
        {
            Image quadrado = quadrados[i];
            if (i >= total)
            {
                quadrado.gameObject.SetActive(false);
                continue;
            }

            quadrado.gameObject.SetActive(true);
            quadrado.color = ResolverCor(donos[i]);
        }
    }

    private Color ResolverCor(PlayerSlotId dono)
    {
        if (!dono.IsValid)
            return corNaoJogado;

        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (matchController == null)
            return corNaoJogado;

        TeamId time = matchController.GetTeamIdForSlot(dono.Value);
        return time == TeamId.Neutral ? corNaoJogado : TeamUtils.GetColor(time);
    }

    private bool ResolverModelo()
    {
        if (quadradoModelo != null)
            return true;

        foreach (Image candidato in GetComponentsInChildren<Image>(includeInactive: true))
        {
            if (candidato == null || candidato.gameObject == gameObject)
                continue;

            if (string.Equals(candidato.name, nomeDoModelo, System.StringComparison.OrdinalIgnoreCase))
            {
                quadradoModelo = candidato;
                return true;
            }
        }

        Debug.LogWarning(
            $"[BarraVencedor] Molde '{nomeDoModelo}' nao encontrado em '{name}'. "
            + "Crie um filho Image com esse nome, desativado, para servir de forma.",
            this);
        return false;
    }

    /// <summary>
    /// Clona ate ter quadrados suficientes. Nunca destroi: sobra e desativada e
    /// reaproveitada na proxima campanha, entao trocar de campanha nao gera lixo
    /// nem custa instanciacao.
    /// </summary>
    private void GarantirQuantidade(int total)
    {
        while (quadrados.Count < total)
        {
            Image novo = Instantiate(quadradoModelo, quadradoModelo.transform.parent);
            novo.name = $"{nomeDoModelo}_{quadrados.Count}";
            novo.transform.SetAsLastSibling();
            quadrados.Add(novo);
        }

        // O molde fica sempre desativado e fora da conta: ele e forma, nao dado.
        if (quadradoModelo.gameObject.activeSelf)
            quadradoModelo.gameObject.SetActive(false);
    }
}
