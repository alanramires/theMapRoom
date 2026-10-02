using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Um quadrante: o retangulo recortavel do mapa de campanha, onde se luta.
///
/// NAO confundir com <see cref="ConstructionSector"/> — "setor" ali e rotulo
/// estrategico de uma construcao (Alpha, Bravo, Base0...), consumido pelo planner
/// da IA dentro de UMA partida. Um quadrante CONTEM setores.
///
/// Duas metades com donos diferentes:
///
///   AUTORIA    id, nome e o retangulo (origem + tamanho). Voce desenhou isso.
///   ASSADO     bakedTiles. E ARTEFATO — regerado pelo botao, nunca editado a
///              mao. Se voce editar, o proximo bake apaga.
///
/// O bake guarda TileBase direto em vez de um id de terreno porque o jogo ja
/// resolve terreno a partir do tile (TerrainDatabase.TryGetByPaletteTile). Uma
/// tabela de traducao no meio so criaria uma segunda fonte pra divergir.
/// </summary>
[System.Serializable]
public class QuadranteData : INoDoMapa
{
    [Header("Identidade")]
    public string quadranteId = "Q1";
    public string displayName = "Quadrante";
    [TextArea(2, 4)] public string descricao;

    [Header("Autoria — o retangulo no mapa de autoria")]
    public int originX;
    public int originY;
    [Min(1)] public int width = 18;
    [Min(1)] public int height = 18;

    [Header("Estado")]
    [Tooltip(
        "Em desenvolvimento: o quadrante aparece e pode ser selecionado na Campanha, " +
        "mas o JOGAR recusa — o autor ainda esta montando. Autoral, sobrevive ao bake.")]
    public bool emDesenvolvimento;

    [Header("Destrave")]
    public List<string> destravadoPor = new List<string>();
    [Tooltip("Mapa final da campanha: so abre com os outros quadrantes dela concluidos. Ainda sem efeito em jogo (portao de destrave nao construido).")]
    [InspectorName("Mapa final da campanha")]
    public bool exigeIrmaos;

    [Header("Economia — autoral, sobrevive ao bake")]
    [Tooltip(
        "Caixa inicial por slot. Slot ausente da lista = 0, que e o caso normal: o "
        + "quadrante comeca so com a renda das construcoes.\n\n"
        + "Fica AQUI, e nao entre os campos assados, porque dinheiro nao e espacial — "
        + "e porque quem limpar a secao 'artefato' um dia nao pode levar o numero do "
        + "autor junto.\n\n"
        + "A renda POR RODADA nao mora aqui: e a soma do capturedIncoming das "
        + "construcoes controladas, e o quadrante ja manda nela pelos predios que assa.")]
    public List<EconomiaInicialSlot> economiaInicial = new List<EconomiaInicialSlot>();

    [Header("Eixos da IA — autoral, sobrevive ao bake")]
    [Tooltip(
        "O caminho de pao de cada slot: setores na ordem de avanco, o ultimo e o rally.\n\n"
        + "Vazio para um slot = a IA monta o leque automatico por angulo, como sempre. "
        + "Com eixo autorado, ela segue EXATAMENTE o que esta aqui, e setor fora de "
        + "todos os eixos do slot fica fora de eixo.\n\n"
        + "Sao rotulos (Alpha, Bravo...) deste quadrante — outro quadrante pode repetir "
        + "os mesmos nomes sem conflito.")]
    public List<EixoAutorado> eixos = new List<EixoAutorado>();

    /// <summary>
    /// Eixos autorados do slot, na ordem em que o autor escreveu. Vazio = o slot
    /// usa o leque automatico.
    /// </summary>
    public void CollectEixosDoSlot(int slotIndex, List<EixoAutorado> destino)
    {
        destino.Clear();
        if (eixos == null)
            return;

        for (int i = 0; i < eixos.Count; i++)
        {
            EixoAutorado eixo = eixos[i];
            if (eixo != null && eixo.slotIndex == slotIndex && eixo.IsValid)
                destino.Add(eixo);
        }
    }

    /// <summary>
    /// Caixa inicial do slot, ou 0 se o autor nao declarou nada para ele. Entrada
    /// duplicada vence a primeira — a bancada avisa, aqui so nao explode.
    /// </summary>
    public int GetStartMoneyForSlot(int slotIndex)
    {
        if (slotIndex < 0 || economiaInicial == null)
            return 0;

        for (int i = 0; i < economiaInicial.Count; i++)
        {
            EconomiaInicialSlot entrada = economiaInicial[i];
            if (entrada != null && entrada.slotIndex == slotIndex)
                return Mathf.Max(0, entrada.startMoney);
        }

        return 0;
    }

    [SerializeField, HideInInspector] private int idSerial;

    /// <summary>Identidade estavel. Ver INoDoMapa.IdSerial.</summary>
    public int IdSerial => idSerial;

    string INoDoMapa.Id { get => quadranteId; set => quadranteId = value; }
    int INoDoMapa.IdSerial { get => idSerial; set => idSerial = value; }
    string INoDoMapa.Nome { get => displayName; set => displayName = value; }
    string INoDoMapa.Descricao { get => descricao; set => descricao = value; }
    int INoDoMapa.OriginX { get => originX; set => originX = value; }
    int INoDoMapa.OriginY { get => originY; set => originY = value; }
    int INoDoMapa.Width { get => width; set => width = value; }
    int INoDoMapa.Height { get => height; set => height = value; }
    List<string> INoDoMapa.DestravadoPor => destravadoPor;
    bool INoDoMapa.ExigeIrmaos { get => exigeIrmaos; set => exigeIrmaos = value; }

    [Header("Assado — artefato, nao editar a mao")]
    [Tooltip("Row-major: indice = (y * width) + x. Null significa buraco, e buraco e valido.")]
    public List<TileBase> bakedTiles = new List<TileBase>();
    [Tooltip("Construcoes dentro do retangulo, em coordenada LOCAL.")]
    public List<ConstrucaoAssada> bakedConstrucoes = new List<ConstrucaoAssada>();
    [Tooltip(
        "Camadas decorativas (quebraMar e o que mais vier), uma entrada por Tilemap.\n\n"
        + "Esparsas, ao contrario do terreno: guardam so as celulas marcadas. A camada "
        + "cresce com o MUNDO, nao com o quadrante — cada quadrante leva apenas o que "
        + "cai dentro do seu retangulo, e buraco nao ocupa lugar.")]
    public List<CamadaAssada> bakedCamadas = new List<CamadaAssada>();
    [Tooltip(
        "Trechos de rota (rodovia, ferrovia, rio) em coordenada LOCAL.\n\n"
        + "TRECHOS, nao rotas: recortar uma sequencia ordenada parte ela. Cada pedaco "
        + "contiguo vira uma entrada propria, porque os consumidores leem PARES "
        + "consecutivos — juntar dois pedacos numa lista so criaria uma aresta de "
        + "estrada que o autor nunca desenhou.")]
    public List<RotaAssada> bakedRotas = new List<RotaAssada>();
    [Tooltip(
        "Unidades ja em campo quando o quadrante abre, em coordenada LOCAL.\n\n"
        + "Vazia significa 'os dois lados comecam comprando' — e o caso dos quadrantes "
        + "do fixture hoje. Para dar tropa inicial a alguem, pinte na cena de autoria "
        + "dentro do retangulo e asse: a regra e a mesma das construcoes, 'se esta no "
        + "retangulo, vem como esta'.")]
    public List<UnidadeAssada> bakedUnidades = new List<UnidadeAssada>();

    [Tooltip("De qual cena de autoria este bake saiu. So documentacao.")]
    public string bakedFromScene;
    public long bakedAtUtcTicks;

    public int CellCount => Mathf.Max(0, width) * Mathf.Max(0, height);

    public bool HasBake =>
        width > 0
        && height > 0
        && bakedTiles != null
        && bakedTiles.Count == CellCount;

    /// <summary>Origem no espaco do mapa de campanha. O recorte translada pra (0,0) local.</summary>
    public Vector3Int Origin => new Vector3Int(originX, originY, 0);

    /// <summary>Tile assado na coordenada LOCAL do quadrante. Null = buraco.</summary>
    public TileBase GetBakedTile(int localX, int localY)
    {
        if (!HasBake)
            return null;
        if (localX < 0 || localX >= width || localY < 0 || localY >= height)
            return null;

        return bakedTiles[(localY * width) + localX];
    }

    /// <summary>Celula do mapa de campanha que corresponde a uma celula local.</summary>
    public Vector3Int LocalToCampaign(int localX, int localY)
    {
        return new Vector3Int(originX + localX, originY + localY, 0);
    }

    public bool ContainsCampaignCell(Vector3Int cell)
    {
        return cell.x >= originX && cell.x < originX + width
            && cell.y >= originY && cell.y < originY + height;
    }

    public override string ToString()
    {
        return $"{quadranteId} ({originX},{originY}) {width}x{height}";
    }
}
