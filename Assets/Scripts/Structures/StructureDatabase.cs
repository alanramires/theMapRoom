using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Balde de rotas por estrutura. Mora AQUI por historia, mas quem o usa hoje e so
/// o RoadNetworkManager, na CENA — que e o tier certo pra layout.
/// </summary>
[System.Serializable]
public class StructureRoadRouteBucket
{
    [Tooltip("Estrutura dona destas rotas nesta cena.")]
    public StructureData structure;

    [Tooltip("Rotas desta estrutura nesta cena.")]
    public List<RoadRouteDefinition> routes = new List<RoadRouteDefinition>();
}

/// <summary>
/// Catalogo de estruturas: diz o que uma estrutura E. Irmao do UnitDatabase,
/// ConstructionDatabase e TerrainDatabase.
///
/// NAO carrega layout, de proposito. Ele ja carregou — 93 rotas espalhadas por 16
/// catalogos, um por mapa. Layout de estrada mora na CENA (RoadNetworkManager) e,
/// no modelo de campanha, no bake do quadrante.
///
/// Isso quebrava o teste de aceitacao do CLAUDE.md ("duplique uma cena e ela nasce
/// vazia") e, sob a cena de Batalha unica, quebraria de vez: um catalogo por mapa
/// nao cabe numa cena que serve todos os quadrantes de todos os mundos.
/// </summary>
[CreateAssetMenu(menuName = "Game/Structures/Structure Database", fileName = "StructureDatabase")]
public class StructureDatabase : ScriptableObject
{
    [Tooltip("Lista manual de estruturas do jogo.")]
    [SerializeField] private List<StructureData> structures = new List<StructureData>();

    private readonly Dictionary<string, StructureData> byId = new Dictionary<string, StructureData>();
    private readonly Dictionary<StructureData, int> indexByStructure = new Dictionary<StructureData, int>();

    public IReadOnlyList<StructureData> Structures => structures;

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

    public bool TryGetById(string id, out StructureData structure)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            structure = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out structure);
    }

    private void RebuildLookup()
    {
        byId.Clear();
        indexByStructure.Clear();

        for (int i = 0; i < structures.Count; i++)
        {
            StructureData data = structures[i];
            if (data == null)
                continue;

            if (!indexByStructure.ContainsKey(data))
                indexByStructure.Add(data, i);

            if (string.IsNullOrWhiteSpace(data.id))
                continue;

            string key = data.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[StructureDatabase] ID duplicado '{key}' em StructureData. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, data);
        }
    }

    // > 0: a vence b | < 0: b vence a | 0: empate.
    public int ComparePriority(StructureData a, StructureData b)
    {
        if (a == b)
            return 0;
        if (a == null)
            return -1;
        if (b == null)
            return 1;

        int byPriority = a.priorityOrder.CompareTo(b.priorityOrder);
        if (byPriority != 0)
            return byPriority;

        int indexA = GetStructureIndex(a);
        int indexB = GetStructureIndex(b);
        // Empate: mantem ordem da lista (primeiro da lista vence).
        return indexB.CompareTo(indexA);
    }

    private int GetStructureIndex(StructureData structure)
    {
        if (structure == null)
            return int.MaxValue;

        if (indexByStructure.Count == 0)
            RebuildLookup();

        if (indexByStructure.TryGetValue(structure, out int index))
            return index;

        return int.MaxValue;
    }

}
