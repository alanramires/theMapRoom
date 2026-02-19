using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/DPQ/DPQ Database", fileName = "DPQDatabase")]
public class DPQDatabase : ScriptableObject
{
    [Tooltip("Lista manual de DPQs do jogo.")]
    [SerializeField] private List<DPQData> items = new List<DPQData>();

    private readonly Dictionary<string, DPQData> byId = new Dictionary<string, DPQData>();

    public IReadOnlyList<DPQData> Items => items;

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

    public bool TryGetById(string id, out DPQData item)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            item = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out item);
    }

    public bool TryGetFirst(out DPQData item)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
            {
                item = items[i];
                return true;
            }
        }

        item = null;
        return false;
    }

    private void RebuildLookup()
    {
        byId.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            DPQData data = items[i];
            if (data == null || string.IsNullOrWhiteSpace(data.id))
                continue;

            string key = data.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[DPQDatabase] ID duplicado '{key}' em DPQData. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, data);
        }
    }
}
