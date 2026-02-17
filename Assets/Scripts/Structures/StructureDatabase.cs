using System.Collections.Generic;
using UnityEngine;

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
