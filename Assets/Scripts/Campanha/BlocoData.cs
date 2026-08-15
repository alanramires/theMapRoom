using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Um bloco do mundo: Europeu, America do Norte, Russia.
///
/// E o nivel que o jogador escolhe primeiro, e o que trava os outros — "America
/// do Norte e Russia so abrem quando o Europeu terminar". Contem campanhas.
///
/// Inline no MundoData de proposito: bloco pertence a um mundo so e nao e
/// compartilhado. Asset separado so criaria a chance de existir bloco fora da
/// lista, que e o modo de falha em que nada e achado e ninguem entende por que.
/// </summary>
[System.Serializable]
public class BlocoData : INoDoMapa
{
    [Header("Identidade")]
    public string blocoId = "bloco";
    public string displayName = "Bloco";
    [TextArea(2, 4)] public string descricao;

    [Header("Retangulo no mapa de autoria")]
    public int originX;
    public int originY;
    [Min(1)] public int width = 1;
    [Min(1)] public int height = 1;

    [Header("Destrave")]
    public List<string> destravadoPor = new List<string>();
    [Tooltip("Exige todos os blocos irmaos concluidos. O caso 'last map', um nivel acima.")]
    public bool exigeIrmaos;

    [Header("Campanhas")]
    public List<CampanhaData> campanhas = new List<CampanhaData>();

    string INoDoMapa.Id { get => blocoId; set => blocoId = value; }
    string INoDoMapa.Nome { get => displayName; set => displayName = value; }
    int INoDoMapa.OriginX { get => originX; set => originX = value; }
    int INoDoMapa.OriginY { get => originY; set => originY = value; }
    int INoDoMapa.Width { get => width; set => width = value; }
    int INoDoMapa.Height { get => height; set => height = value; }
    List<string> INoDoMapa.DestravadoPor => destravadoPor;
    bool INoDoMapa.ExigeIrmaos { get => exigeIrmaos; set => exigeIrmaos = value; }

    public bool TryGetCampanha(string campanhaId, out CampanhaData campanha)
    {
        campanha = null;
        if (string.IsNullOrWhiteSpace(campanhaId) || campanhas == null)
            return false;

        for (int i = 0; i < campanhas.Count; i++)
        {
            CampanhaData candidate = campanhas[i];
            if (candidate == null)
                continue;
            if (!string.Equals(candidate.campanhaId, campanhaId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            campanha = candidate;
            return true;
        }

        return false;
    }

    public CampanhaData GetOrCreateCampanha(string campanhaId)
    {
        if (campanhas == null)
            campanhas = new List<CampanhaData>();

        if (TryGetCampanha(campanhaId, out CampanhaData existing))
            return existing;

        CampanhaData created = new CampanhaData
        {
            campanhaId = campanhaId,
            displayName = campanhaId
        };

        campanhas.Add(created);
        return created;
    }
}
