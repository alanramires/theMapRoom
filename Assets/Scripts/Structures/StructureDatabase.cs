using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Structures/Structure Database", fileName = "StructureDatabase")]
public class StructureDatabase : ScriptableObject
{
    [Tooltip("Lista manual de estruturas do jogo.")]
    [SerializeField] private List<StructureData> structures = new List<StructureData>();

    private readonly Dictionary<string, StructureData> byId = new Dictionary<string, StructureData>();

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

        for (int i = 0; i < structures.Count; i++)
        {
            StructureData data = structures[i];
            if (data == null || string.IsNullOrWhiteSpace(data.id))
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
}
