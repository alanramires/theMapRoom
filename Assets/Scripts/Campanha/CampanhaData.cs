using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Uma campanha dentro de um bloco: Europa, Africa.
///
/// E um RETANGULO do mapa de autoria, dividido em quadrantes — e bloco, campanha
/// e quadrante sao o mesmo gesto em tres escalas, o que faz a mesma ferramenta
/// servir aos tres. Disso sai que a "foto" da campanha nao precisa ser arte: e o
/// retangulo dela, enquadrado pela camera.
/// </summary>
[System.Serializable]
public class CampanhaData : INoDoMapa
{
    [Header("Identidade")]
    public string campanhaId = "campanha";
    public string displayName = "Campanha";
    [TextArea(2, 4)] public string descricao;

    [Header("Retangulo no mapa de autoria")]
    public int originX;
    public int originY;
    [Min(1)] public int width = 1;
    [Min(1)] public int height = 1;

    [Header("Destrave")]
    public List<string> destravadoPor = new List<string>();
    [Tooltip("Campanha final do bloco: so abre com as outras campanhas dele concluidas. Ainda sem efeito em jogo.")]
    [InspectorName("Campanha final do bloco")]
    public bool exigeIrmaos;

    [Header("Quadrantes")]
    public List<QuadranteData> quadrantes = new List<QuadranteData>();

    [Header("Fundo — assado (paisagem da Campanha)")]
    [Tooltip(
        "Terreno do retangulo INTEIRO da campanha, assado pelo Map Helper (\"Assar fundo\"). "
        + "A cena Campanha o pinta por baixo dos quadrantes, apagado: e paisagem, nao e "
        + "jogavel nem selecionavel. Vazio = so os quadrantes aparecem. Row-major, y crescendo.")]
    public List<TileBase> bakedFundo = new List<TileBase>();

    public bool HasFundo => bakedFundo != null && bakedFundo.Count == Mathf.Max(1, width) * Mathf.Max(1, height);

    public TileBase GetFundoTile(int localX, int localY)
    {
        if (!HasFundo || localX < 0 || localY < 0 || localX >= width || localY >= height)
            return null;
        return bakedFundo[(localY * width) + localX];
    }

    [SerializeField, HideInInspector] private int idSerial;

    /// <summary>Identidade estavel. Ver INoDoMapa.IdSerial.</summary>
    public int IdSerial => idSerial;

    string INoDoMapa.Id { get => campanhaId; set => campanhaId = value; }
    int INoDoMapa.IdSerial { get => idSerial; set => idSerial = value; }
    string INoDoMapa.Nome { get => displayName; set => displayName = value; }
    string INoDoMapa.Descricao { get => descricao; set => descricao = value; }
    int INoDoMapa.OriginX { get => originX; set => originX = value; }
    int INoDoMapa.OriginY { get => originY; set => originY = value; }
    int INoDoMapa.Width { get => width; set => width = value; }
    int INoDoMapa.Height { get => height; set => height = value; }
    List<string> INoDoMapa.DestravadoPor => destravadoPor;
    bool INoDoMapa.ExigeIrmaos { get => exigeIrmaos; set => exigeIrmaos = value; }

    public bool TryGetQuadrante(string quadranteId, out QuadranteData quadrante)
    {
        quadrante = null;
        if (string.IsNullOrWhiteSpace(quadranteId) || quadrantes == null)
            return false;

        for (int i = 0; i < quadrantes.Count; i++)
        {
            QuadranteData candidate = quadrantes[i];
            if (candidate == null)
                continue;
            if (!string.Equals(candidate.quadranteId, quadranteId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            quadrante = candidate;
            return true;
        }

        return false;
    }

    public QuadranteData GetOrCreateQuadrante(string quadranteId)
    {
        if (quadrantes == null)
            quadrantes = new List<QuadranteData>();

        if (TryGetQuadrante(quadranteId, out QuadranteData existing))
            return existing;

        QuadranteData created = new QuadranteData
        {
            quadranteId = quadranteId,
            displayName = quadranteId
        };

        quadrantes.Add(created);
        return created;
    }
}
