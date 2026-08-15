using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// O mundo. UM asset, UMA cena de autoria — o globo inteiro desenhado de uma vez.
///
///   MUNDO
///    └─ BLOCO          Europeu, America do Norte, Russia     ← o jogador escolhe
///        └─ CAMPANHA       Europa, Africa
///            └─ QUADRANTE      Inglaterra, Franca...         ← aqui se luta
///
/// Uma cena so porque tudo encosta em tudo: Europa faz fronteira com Africa E com
/// a Russia. Nao se desenha fronteira continua entre dois arquivos, e e a
/// continuidade que faz o mapa acender por regiao conforme se conquista.
///
/// O asset existe porque em RUNTIME a cena de autoria nao esta carregada: a
/// Batalha nao tem como ler um tilemap que nao existe na memoria. Este arquivo e
/// a ponte entre o que foi desenhado e o que e jogado.
/// </summary>
[CreateAssetMenu(menuName = "Game/Campanha/Mundo", fileName = "Mundo")]
public class MundoData : ScriptableObject
{
    [Header("Identidade")]
    public string mundoId = "mundo";
    public string displayName = "Mundo";
    [TextArea(2, 5)] public string descricao;
    public Sprite foto;

    [Tooltip("Cena de autoria onde este mundo e desenhado. Documentacao — nao e carregada em runtime.")]
    public string authoringSceneName;

    [Header("Blocos")]
    public List<BlocoData> blocos = new List<BlocoData>();

    // ─────────────────────────────────────────────────────────────── busca ──

    public bool TryGetBloco(string blocoId, out BlocoData bloco)
    {
        bloco = null;
        if (string.IsNullOrWhiteSpace(blocoId) || blocos == null)
            return false;

        for (int i = 0; i < blocos.Count; i++)
        {
            BlocoData candidate = blocos[i];
            if (candidate == null)
                continue;
            if (!string.Equals(candidate.blocoId, blocoId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            bloco = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Acha a campanha em QUALQUER bloco. O endereco nao carrega o bloco de
    /// proposito: ids sao unicos no mundo (a ferramenta valida isso), e assim
    /// mover uma campanha de bloco nao invalida save nenhum.
    /// </summary>
    public bool TryGetCampanha(string campanhaId, out BlocoData bloco, out CampanhaData campanha)
    {
        bloco = null;
        campanha = null;
        if (string.IsNullOrWhiteSpace(campanhaId) || blocos == null)
            return false;

        for (int i = 0; i < blocos.Count; i++)
        {
            BlocoData candidate = blocos[i];
            if (candidate == null)
                continue;
            if (!candidate.TryGetCampanha(campanhaId, out campanha))
                continue;

            bloco = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolve o endereco (campanha, quadrante) — o mesmo par que, mais pra
    /// frente, vem do save e MANDA no que a Batalha pinta.
    /// </summary>
    public bool TryGetQuadrante(
        string campanhaId,
        string quadranteId,
        out BlocoData bloco,
        out CampanhaData campanha,
        out QuadranteData quadrante)
    {
        quadrante = null;
        if (!TryGetCampanha(campanhaId, out bloco, out campanha))
            return false;

        return campanha.TryGetQuadrante(quadranteId, out quadrante);
    }

    public BlocoData GetOrCreateBloco(string blocoId)
    {
        if (blocos == null)
            blocos = new List<BlocoData>();

        if (TryGetBloco(blocoId, out BlocoData existing))
            return existing;

        BlocoData created = new BlocoData
        {
            blocoId = blocoId,
            displayName = blocoId
        };

        blocos.Add(created);
        return created;
    }

    // ─────────────────────────────────────────────────────── enumeracao ──

    /// <summary>Todas as campanhas do mundo, de todos os blocos.</summary>
    public IEnumerable<CampanhaData> AllCampanhas()
    {
        if (blocos == null)
            yield break;

        for (int i = 0; i < blocos.Count; i++)
        {
            BlocoData b = blocos[i];
            if (b?.campanhas == null)
                continue;

            for (int j = 0; j < b.campanhas.Count; j++)
            {
                if (b.campanhas[j] != null)
                    yield return b.campanhas[j];
            }
        }
    }

    /// <summary>Todos os quadrantes do mundo, de todas as campanhas.</summary>
    public IEnumerable<QuadranteData> AllQuadrantes()
    {
        foreach (CampanhaData c in AllCampanhas())
        {
            if (c.quadrantes == null)
                continue;

            for (int i = 0; i < c.quadrantes.Count; i++)
            {
                if (c.quadrantes[i] != null)
                    yield return c.quadrantes[i];
            }
        }
    }
}
