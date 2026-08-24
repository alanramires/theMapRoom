using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Export/import de TERRENO puro, em JSON, pra uma IA externa desenhar mapa.
///
/// SO TERRENO. Nada de construcao, estrada, estrutura ou enfeite — hexagono por
/// hexagono e mais nada. Terreno e a unica coisa que faz round-trip PURO, e o
/// motivo esta no proprio catalogo:
///
///     exportar   tile -> TerrainDatabase.TryGetByPaletteTile -> TerrainTypeData.id
///     importar   id   -> TerrainDatabase.TryGetById          -> paletteTile
///
/// Nao existe tabela de traducao aqui, de proposito: o paletteTile JA e o
/// dicionario. Inventar um mapa de nomes no meio criaria uma segunda fonte de
/// verdade pra divergir da primeira — a mesma razao que fez o bake guardar
/// TileBase direto em vez de um id (ver QuadranteData).
///
/// O VOCABULARIO NAO E FIXO NO CODIGO. Sai do TerrainDatabase da cena, entao uma
/// sexta paleta amanha entra sozinha, sem tocar aqui. Hoje sao cinco: plains,
/// beach, sea, forest, mountain.
///
/// ─── O QUE O FORMATO PRECISA ENSINAR ────────────────────────────────────────
///
/// O tabuleiro e HEXAGONAL com offset odd-r: linhas de y IMPAR sao deslocadas
/// meia celula pra direita. Uma IA que trate o texto como grade quadrada produz
/// costa serrilhada e rio partido — e o mapa CARREGA sem um erro sequer, so joga
/// errado. Por isso a regra e a lista de vizinhos vao escritas DENTRO do arquivo.
///
/// (Convencao verificada contra o tracado da rodovia da cena de autoria, cujas
/// celulas consecutivas sao adjacentes por definicao: odd-r bate em 33 de 33
/// pares, even-r em 24.)
/// </summary>
public static class MapaTerrenoJson
{
    public const string Formato = "the-map-room.terreno.v1";

    /// <summary>Buraco: celula do retangulo sem tile nenhum. E valido.</summary>
    public const char CharVazio = '-';

    [System.Serializable]
    public class Entrada
    {
        public string simbolo;
        public string terreno;
        public string nome;
    }

    [System.Serializable]
    public class Documento
    {
        public string formato = Formato;
        public string leiaPrimeiro;
        public string grid;
        public string vizinhos;
        public string ordemDasLinhas;
        public string vazio;

        public int origemX;
        public int origemY;
        public int largura;
        public int altura;

        public Entrada[] legenda;
        public string[] linhas;
    }

    // ─────────────────────────────────────────────────────────── legenda ──

    /// <summary>
    /// Simbolo por terreno, derivado do ID — nao de uma lista fixa. Primeira letra
    /// livre do id; se colidir, tenta as seguintes, depois maiuscula, depois digito.
    /// As cinco de hoje nao colidem (plains beach sea forest mountain -> p b s f m),
    /// mas uma sexta paleta tem de entrar sem editar codigo.
    /// </summary>
    public static Dictionary<char, TerrainTypeData> ConstruirLegenda(
        TerrainDatabase catalogo,
        out List<Entrada> entradas)
    {
        Dictionary<char, TerrainTypeData> porSimbolo = new Dictionary<char, TerrainTypeData>();
        entradas = new List<Entrada>();

        if (catalogo == null || catalogo.Terrains == null)
            return porSimbolo;

        for (int i = 0; i < catalogo.Terrains.Count; i++)
        {
            TerrainTypeData t = catalogo.Terrains[i];
            if (t == null || string.IsNullOrWhiteSpace(t.id))
                continue;

            char simbolo = EscolherSimbolo(t.id, porSimbolo);
            porSimbolo[simbolo] = t;
            entradas.Add(new Entrada
            {
                simbolo = simbolo.ToString(),
                terreno = t.id.Trim(),
                nome = t.displayName
            });
        }

        return porSimbolo;
    }

    private static char EscolherSimbolo(string id, Dictionary<char, TerrainTypeData> usados)
    {
        string limpo = id.Trim().ToLowerInvariant();

        for (int i = 0; i < limpo.Length; i++)
        {
            char c = limpo[i];
            if (c >= 'a' && c <= 'z' && c != CharVazio && !usados.ContainsKey(c))
                return c;
        }

        for (char c = 'A'; c <= 'Z'; c++)
        {
            if (!usados.ContainsKey(c))
                return c;
        }

        for (char c = '0'; c <= '9'; c++)
        {
            if (!usados.ContainsKey(c))
                return c;
        }

        return '?';
    }

    // ───────────────────────────────────────────────────────── exportar ──

    public static string Exportar(
        Tilemap tabuleiro,
        TerrainDatabase catalogo,
        int origemX,
        int origemY,
        int largura,
        int altura,
        out int desconhecidos)
    {
        desconhecidos = 0;
        if (tabuleiro == null || catalogo == null)
            return null;

        Dictionary<char, TerrainTypeData> porSimbolo =
            ConstruirLegenda(catalogo, out List<Entrada> entradas);

        Dictionary<TerrainTypeData, char> porTerreno = new Dictionary<TerrainTypeData, char>();
        foreach (KeyValuePair<char, TerrainTypeData> par in porSimbolo)
            porTerreno[par.Value] = par.Key;

        largura = Mathf.Max(1, largura);
        altura = Mathf.Max(1, altura);

        // DE CIMA PARA BAIXO: a primeira linha do array e o MAIOR y. E como se le
        // um mapa, e inverter aqui espelharia o mundo sem avisar ninguem.
        string[] linhas = new string[altura];
        StringBuilder sb = new StringBuilder(largura);

        for (int linha = 0; linha < altura; linha++)
        {
            int y = origemY + altura - 1 - linha;
            sb.Length = 0;

            for (int localX = 0; localX < largura; localX++)
            {
                TileBase tile = tabuleiro.GetTile(new Vector3Int(origemX + localX, y, 0));
                if (tile == null)
                {
                    sb.Append(CharVazio);
                    continue;
                }

                if (catalogo.TryGetByPaletteTile(tile, out TerrainTypeData terreno)
                    && porTerreno.TryGetValue(terreno, out char simbolo))
                {
                    sb.Append(simbolo);
                    continue;
                }

                // Tile pintado que o catalogo nao reconhece. Vira buraco no texto,
                // mas e CONTADO e reportado: reimportar isso apagaria o hex, e
                // apagar calado e pior desfecho que recusar.
                desconhecidos++;
                sb.Append(CharVazio);
            }

            linhas[linha] = sb.ToString();
        }

        Documento doc = new Documento
        {
            leiaPrimeiro =
                "Mapa de terreno de um jogo de estrategia em hexagonos. Edite APENAS o array "
                + "'linhas': um caractere por hexagono, usando somente os simbolos da 'legenda'. "
                + "Nao mude largura, altura nem a quantidade de linhas — cada linha tem de ter "
                + "exatamente 'largura' caracteres, e tem de haver exatamente 'altura' linhas.",
            grid =
                "Hexagonos ponta-para-cima, offset odd-r: as linhas de y IMPAR sao deslocadas "
                + "MEIA CELULA para a DIREITA. Isto NAO e uma grade quadrada. Tratar como grade "
                + "quadrada produz costa serrilhada e rio partido, e o mapa carrega sem erro "
                + "nenhum — so fica errado.",
            vizinhos =
                "Os 6 vizinhos de (x,y) dependem da PARIDADE de y. "
                + "y par:   (x-1,y) (x+1,y) (x-1,y-1) (x,y-1) (x-1,y+1) (x,y+1). "
                + "y impar: (x-1,y) (x+1,y) (x,y-1) (x+1,y-1) (x,y+1) (x+1,y+1).",
            ordemDasLinhas =
                "De cima para baixo: linhas[0] e a fileira do MAIOR y (o topo do mapa) e a "
                + "ultima linha e y = origemY. Dentro de cada linha, o caractere de indice 0 e "
                + "x = origemX, crescendo para a direita.",
            vazio =
                "'" + CharVazio + "' e buraco: hexagono que nao existe. E valido e faz parte do "
                + "desenho — use para recortar o formato da costa ou da ilha.",
            origemX = origemX,
            origemY = origemY,
            largura = largura,
            altura = altura,
            legenda = entradas.ToArray(),
            linhas = linhas
        };

        return JsonUtility.ToJson(doc, prettyPrint: true);
    }

    // ───────────────────────────────────────────────────────── importar ──

    public class Resultado
    {
        public bool ok;
        public string erro;
        public Documento documento;
        public int pintados;
        public int buracos;
    }

    /// <summary>
    /// Le e VALIDA por inteiro antes de tocar no tilemap. Aplicar meio mapa e so
    /// entao falhar deixaria a cena num estado que ninguem pediu — e que so o Undo
    /// desfaz, se o autor perceber a tempo.
    /// </summary>
    public static Resultado Interpretar(string json, TerrainDatabase catalogo)
    {
        Resultado r = new Resultado();

        if (string.IsNullOrWhiteSpace(json))
        {
            r.erro = "JSON vazio.";
            return r;
        }

        if (catalogo == null)
        {
            r.erro = "Sem TerrainDatabase: nao ha como resolver simbolo em terreno.";
            return r;
        }

        Documento doc;
        try
        {
            doc = JsonUtility.FromJson<Documento>(json);
        }
        catch (System.Exception e)
        {
            r.erro = "JSON invalido: " + e.Message;
            return r;
        }

        if (doc == null)
        {
            r.erro = "JSON invalido: nao virou documento.";
            return r;
        }

        if (doc.formato != Formato)
        {
            r.erro = "Formato '" + doc.formato + "' — esperado '" + Formato + "'.";
            return r;
        }

        if (doc.largura <= 0 || doc.altura <= 0)
        {
            r.erro = "Tamanho invalido: " + doc.largura + "x" + doc.altura + ".";
            return r;
        }

        int quantasLinhas = doc.linhas == null ? 0 : doc.linhas.Length;
        if (quantasLinhas != doc.altura)
        {
            r.erro =
                "O documento diz altura " + doc.altura + ", mas tem "
                + quantasLinhas + " linha(s).";
            return r;
        }

        for (int i = 0; i < doc.linhas.Length; i++)
        {
            string linha = doc.linhas[i];
            int tamanho = linha == null ? 0 : linha.Length;
            if (tamanho != doc.largura)
            {
                r.erro =
                    "linhas[" + i + "] tem " + tamanho + " caractere(s), mas a largura e "
                    + doc.largura + ". Toda linha tem de ter exatamente a largura.";
                return r;
            }
        }

        // A LEGENDA DO ARQUIVO NAO MANDA — o catalogo manda.
        //
        // Um documento pode voltar com um terreno que foi renomeado ou removido
        // desde a exportacao. Resolver pelo campo 'terreno' (o id) contra o catalogo
        // ATUAL e o que impede um arquivo velho de pintar a coisa errada calado.
        Dictionary<char, TerrainTypeData> porSimbolo = new Dictionary<char, TerrainTypeData>();
        if (doc.legenda != null)
        {
            for (int i = 0; i < doc.legenda.Length; i++)
            {
                Entrada e = doc.legenda[i];
                if (e == null || string.IsNullOrEmpty(e.simbolo))
                    continue;

                if (!catalogo.TryGetById(e.terreno, out TerrainTypeData terreno) || terreno == null)
                {
                    r.erro =
                        "A legenda usa o terreno '" + e.terreno + "' (simbolo '" + e.simbolo
                        + "'), que nao existe no catalogo desta cena.";
                    return r;
                }

                if (terreno.paletteTile == null)
                {
                    r.erro =
                        "O terreno '" + e.terreno + "' nao tem paletteTile — nao ha o que pintar.";
                    return r;
                }

                porSimbolo[e.simbolo[0]] = terreno;
            }
        }

        // Todo simbolo usado tem de estar na legenda. Sem esta checagem, um caractere
        // digitado por engano viraria buraco em silencio — e apagar hex calado e o
        // pior desfecho possivel de um import.
        HashSet<char> ausentes = new HashSet<char>();
        for (int i = 0; i < doc.linhas.Length; i++)
        {
            string linha = doc.linhas[i];
            for (int c = 0; c < linha.Length; c++)
            {
                char simbolo = linha[c];
                if (simbolo == CharVazio || porSimbolo.ContainsKey(simbolo))
                    continue;

                ausentes.Add(simbolo);
            }
        }

        if (ausentes.Count > 0)
        {
            r.erro =
                "Simbolo(s) fora da legenda: " + string.Join(" ", ausentes)
                + ". Legais: " + string.Join(" ", porSimbolo.Keys)
                + " e '" + CharVazio + "' (buraco).";
            return r;
        }

        r.ok = true;
        r.documento = doc;
        return r;
    }

    /// <summary>Aplica um documento JA validado. Chame Interpretar antes.</summary>
    public static void Aplicar(
        Resultado validado,
        Tilemap tabuleiro,
        TerrainDatabase catalogo,
        int origemX,
        int origemY)
    {
        Documento doc = validado.documento;
        Dictionary<char, TerrainTypeData> porSimbolo = new Dictionary<char, TerrainTypeData>();

        for (int i = 0; i < doc.legenda.Length; i++)
        {
            Entrada e = doc.legenda[i];
            if (e == null || string.IsNullOrEmpty(e.simbolo))
                continue;
            if (catalogo.TryGetById(e.terreno, out TerrainTypeData terreno))
                porSimbolo[e.simbolo[0]] = terreno;
        }

        validado.pintados = 0;
        validado.buracos = 0;

        for (int linha = 0; linha < doc.altura; linha++)
        {
            int y = origemY + doc.altura - 1 - linha;
            string texto = doc.linhas[linha];

            for (int localX = 0; localX < doc.largura; localX++)
            {
                Vector3Int cell = new Vector3Int(origemX + localX, y, 0);
                char simbolo = texto[localX];

                if (simbolo == CharVazio)
                {
                    tabuleiro.SetTile(cell, null);
                    validado.buracos++;
                    continue;
                }

                tabuleiro.SetTile(cell, porSimbolo[simbolo].paletteTile);
                validado.pintados++;
            }
        }
    }
}
