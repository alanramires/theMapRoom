using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catalogo de construcoes: diz o que uma construcao E. Irmao do UnitDatabase e do
/// TerrainDatabase.
///
/// NAO carrega layout, de proposito. Ele ja carregou — o campo fieldEntries dizia
/// "esta construcao esta nesta celula, deste dono", e era isso que obrigava a
/// existir um catalogo POR MAPA (eram sete). Layout e da cena; catalogo e
/// compartilhado e agrupado por conteudo ("basico", "com naval"), como o
/// UnitDatabase sempre foi.
///
/// O campo tinha ZERO leitores em runtime. Quem o alimentava era o
/// ConstructionPainterWindow, espelhando no catalogo o que ja plantava na cena —
/// o proprio nome do flag, persistToFieldDatabase, dizia quem era a fonte.
///
/// Sob o modelo de campanha isso quebraria de vez: a cena de Batalha e UMA so
/// para todos os quadrantes de todos os mundos, e um catalogo por mapa nao cabe
/// nela. Layout de quadrante mora em QuadranteData.bakedConstrucoes.
/// </summary>
[CreateAssetMenu(menuName = "Game/Construction/Construction Database", fileName = "ConstructionDatabase")]
public class ConstructionDatabase : ScriptableObject
{
    [Tooltip("Lista manual das construcoes que realmente fazem parte do jogo.")]
    [SerializeField] private List<ConstructionData> constructions = new List<ConstructionData>();

    private readonly Dictionary<string, ConstructionData> byId = new Dictionary<string, ConstructionData>();

    public IReadOnlyList<ConstructionData> Constructions => constructions;

    private void OnEnable()
    {
        RebuildLookup();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildLookup();
    }
#endif

    public bool TryGetById(string id, out ConstructionData construction)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            construction = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out construction);
    }

    public bool TryGetFirst(out ConstructionData construction)
    {
        for (int i = 0; i < constructions.Count; i++)
        {
            if (constructions[i] != null)
            {
                construction = constructions[i];
                return true;
            }
        }

        construction = null;
        return false;
    }

    private void RebuildLookup()
    {
        byId.Clear();

        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionData def = constructions[i];
            if (def == null)
                continue;

            // Id vazio nao resolve por id — mesmo risco de fantasma que a duplicata.
            if (string.IsNullOrWhiteSpace(def.id))
            {
                Debug.LogWarning(
                    $"[ConstructionDatabase] ConstructionData '{def.name}' SEM id — nao resolve por id "
                    + "(risco de sumir no jogo). Defina um id unico.", def);
                continue;
            }

            string key = def.id.Trim();
            // 'def' vai como contexto do log: clicar no aviso pinga o asset FANTASMA no Project.
            // So o primeiro com este id fica indexado; o resto e descartado e some no jogo.
            if (byId.TryGetValue(key, out ConstructionData existing))
            {
                Debug.LogWarning(
                    $"[ConstructionDatabase] ID duplicado '{key}': mantendo '{(existing != null ? existing.name : "?")}', "
                    + $"DESCARTANDO '{def.name}' (vira fantasma no jogo). De ids unicos — "
                    + "Tools > Construction > Verificar IDs duplicados.", def);
                continue;
            }

            byId.Add(key, def);
        }
    }
}
