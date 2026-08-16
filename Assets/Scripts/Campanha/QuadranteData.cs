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

    [Header("Destrave")]
    public List<string> destravadoPor = new List<string>();
    [Tooltip("Exige todos os quadrantes irmaos concluidos. E o 'last map' da campanha.")]
    public bool exigeIrmaos;

    string INoDoMapa.Id { get => quadranteId; set => quadranteId = value; }
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
