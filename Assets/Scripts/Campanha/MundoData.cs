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

    [Header("Camadas decorativas")]
    [Tooltip(
        "Nomes de Tilemap, no mesmo Grid do tabuleiro, que o bake copia junto com o "
        + "terreno. Sao ENFEITE — sem regra, sem custo, fora de sensor.\n\n"
        + "⚠️ 'quebraMar' NAO e rotulo livre: e o nome que a NEVOA fotografa. O "
        + "FoW_tile guarda memoria de tres coisas — hexagono, construcao e quebraMar "
        + "— e o nome esta fixo em MatchController.RenderFogBreakwaterMemory.\n\n"
        + "Renomear a camada faz o enfeite SUMIR sob a nevoa (aparece quando visivel, "
        + "some quando so explorado), sem erro nenhum. Camada com outro nome e "
        + "copiada pelo recorte mas nao e fotografada.")]
    public List<string> camadasDecorativas = new List<string> { "quebraMar" };

    // NAO existe catalogo aqui, de proposito. Catalogo diz o que uma coisa E, e
    // isso nao e do mundo: um QG e um QG em qualquer lugar. A cena aponta pro
    // catalogo compartilhado, como o UnitDatabase sempre fez.

    [Header("Identidade estavel")]
    [SerializeField, HideInInspector] private int proximoSerial = 1;

    [Header("Blocos")]
    public List<BlocoData> blocos = new List<BlocoData>();

    /// <summary>
    /// Proximo serial livre. SO SOBE — e essa e a regra inteira.
    ///
    /// O UnitSpawner recalcula o contador dele a partir do maior id em uso
    /// (SetNextIdAfterMax), e la isso e correto: os ids morrem com a partida.
    /// Aqui seria bug. O arquivo de progresso e o grafo de destrave sobrevivem ao
    /// no — apagar o ultimo quadrante e criar outro devolveria o serial do morto,
    /// e a marca de um lugar grudaria noutro, sem erro nenhum.
    ///
    /// Serial queimado fica queimado. E barato: um int por mundo, e o mundo inteiro
    /// tem dezenas de nos, nao milhoes.
    /// </summary>
    public int GerarSerial()
    {
        if (proximoSerial < 1)
            proximoSerial = 1;

        int serial = proximoSerial;
        proximoSerial++;
        return serial;
    }

    /// <summary>
    /// Da serial a quem ainda nao tem, e devolve quantos deu.
    ///
    /// Existe pros nos criados ANTES do serial existir — e para o dia em que um no
    /// vier de um copiar/colar, que duplica o campo junto. Confere duplicata: dois
    /// nos com o mesmo serial e pior que nenhum serial, porque o segundo herdaria
    /// silenciosamente o progresso do primeiro.
    /// </summary>
    public int RepararSeriais(out int duplicados)
    {
        duplicados = 0;
        int dados = 0;
        HashSet<int> vistos = new HashSet<int>();

        foreach (INoDoMapa no in TodosOsNos())
        {
            if (no.IdSerial <= 0)
            {
                no.IdSerial = GerarSerial();
                dados++;
                continue;
            }

            if (!vistos.Add(no.IdSerial))
            {
                // Duplicata: o segundo perde o serial e ganha um novo. O primeiro
                // fica com o original, entao o progresso ja gravado continua valendo
                // pra ele — e o clone e que vira um lugar novo, que e o que ele e.
                no.IdSerial = GerarSerial();
                vistos.Add(no.IdSerial);
                duplicados++;
                dados++;
                continue;
            }

            // O contador nunca pode ficar atras do que ja existe no asset, senao a
            // proxima criacao repetiria um serial vivo.
            if (proximoSerial <= no.IdSerial)
                proximoSerial = no.IdSerial + 1;
        }

        return dados;
    }

    /// <summary>Os tres niveis, na ordem em que aninham.</summary>
    public IEnumerable<INoDoMapa> TodosOsNos()
    {
        if (blocos == null)
            yield break;

        for (int i = 0; i < blocos.Count; i++)
        {
            BlocoData b = blocos[i];
            if (b == null)
                continue;

            yield return b;

            if (b.campanhas == null)
                continue;

            for (int j = 0; j < b.campanhas.Count; j++)
            {
                CampanhaData c = b.campanhas[j];
                if (c == null)
                    continue;

                yield return c;

                if (c.quadrantes == null)
                    continue;

                for (int k = 0; k < c.quadrantes.Count; k++)
                {
                    if (c.quadrantes[k] != null)
                        yield return c.quadrantes[k];
                }
            }
        }
    }

    /// <summary>Acha qualquer no do mundo pelo serial — o endereco que nao muda.</summary>
    public bool TryGetPorSerial(int idSerial, out INoDoMapa no)
    {
        no = null;
        if (idSerial <= 0)
            return false;

        foreach (INoDoMapa candidato in TodosOsNos())
        {
            if (candidato.IdSerial != idSerial)
                continue;

            no = candidato;
            return true;
        }

        return false;
    }

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
