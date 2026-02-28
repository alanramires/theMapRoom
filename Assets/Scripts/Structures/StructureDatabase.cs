using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StructureRoadRouteBucket
{
    [Tooltip("Estrutura dona destas rotas no contexto deste catalogo/mapa.")]
    public StructureData structure;

    [Tooltip("Rotas desta estrutura neste catalogo.")]
    public List<RoadRouteDefinition> routes = new List<RoadRouteDefinition>();
}

[CreateAssetMenu(menuName = "Game/Structures/Structure Database", fileName = "StructureDatabase")]
public class StructureDatabase : ScriptableObject
{
    [Tooltip("Lista manual de estruturas do jogo.")]
    [SerializeField] private List<StructureData> structures = new List<StructureData>();
    [Tooltip("Rotas por estrutura neste catalogo/mapa. Centraliza layout de rotas por tabuleiro.")]
    [SerializeField] private List<StructureRoadRouteBucket> roadRoutesByStructure = new List<StructureRoadRouteBucket>();

    private readonly Dictionary<string, StructureData> byId = new Dictionary<string, StructureData>();
    private readonly Dictionary<StructureData, int> indexByStructure = new Dictionary<StructureData, int>();
    private readonly Dictionary<StructureData, List<RoadRouteDefinition>> routesByStructure = new Dictionary<StructureData, List<RoadRouteDefinition>>();

    public IReadOnlyList<StructureData> Structures => structures;
    public IReadOnlyList<StructureRoadRouteBucket> RoadRoutesByStructure => roadRoutesByStructure;

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
        routesByStructure.Clear();

        if (roadRoutesByStructure == null)
            roadRoutesByStructure = new List<StructureRoadRouteBucket>();

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

        for (int i = roadRoutesByStructure.Count - 1; i >= 0; i--)
        {
            StructureRoadRouteBucket bucket = roadRoutesByStructure[i];
            if (bucket == null || bucket.structure == null)
            {
                roadRoutesByStructure.RemoveAt(i);
                continue;
            }

            if (bucket.routes == null)
                bucket.routes = new List<RoadRouteDefinition>();

            if (!routesByStructure.ContainsKey(bucket.structure))
                routesByStructure.Add(bucket.structure, bucket.routes);
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

    public IReadOnlyList<RoadRouteDefinition> GetRoadRoutes(StructureData structure)
    {
        if (structure == null)
            return null;

        if (routesByStructure.Count == 0)
            RebuildLookup();

        if (routesByStructure.TryGetValue(structure, out List<RoadRouteDefinition> routes))
            return routes;

        return null;
    }

    public List<RoadRouteDefinition> GetOrCreateRoadRoutes(StructureData structure)
    {
        if (structure == null)
            return null;

        if (routesByStructure.Count == 0)
            RebuildLookup();

        if (routesByStructure.TryGetValue(structure, out List<RoadRouteDefinition> existing) && existing != null)
            return existing;

        if (roadRoutesByStructure == null)
            roadRoutesByStructure = new List<StructureRoadRouteBucket>();

        var bucket = new StructureRoadRouteBucket
        {
            structure = structure,
            routes = new List<RoadRouteDefinition>()
        };

        roadRoutesByStructure.Add(bucket);
        routesByStructure[structure] = bucket.routes;
        return bucket.routes;
    }

    public bool HasAnyRoadRoutesForStructure(StructureData structure)
    {
        IReadOnlyList<RoadRouteDefinition> routes = GetRoadRoutes(structure);
        return routes != null && routes.Count > 0;
    }

    public void ResetRoadRoutes()
    {
        if (roadRoutesByStructure == null)
            roadRoutesByStructure = new List<StructureRoadRouteBucket>();
        else
            roadRoutesByStructure.Clear();

        routesByStructure.Clear();
    }
}
