using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Supplies/Supply Database", fileName = "SupplyDatabase")]
public class SupplyDatabase : ScriptableObject
{
    [Tooltip("Lista manual dos suprimentos que fazem parte do jogo.")]
    [SerializeField] private List<SupplyData> supplies = new List<SupplyData>();

    private readonly Dictionary<string, SupplyData> byId = new Dictionary<string, SupplyData>();

    public IReadOnlyList<SupplyData> Supplies => supplies;

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

    public bool TryGetById(string id, out SupplyData supply)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            supply = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out supply);
    }

    public bool TryGetFirst(out SupplyData supply)
    {
        for (int i = 0; i < supplies.Count; i++)
        {
            if (supplies[i] != null)
            {
                supply = supplies[i];
                return true;
            }
        }

        supply = null;
        return false;
    }

    private void RebuildLookup()
    {
        byId.Clear();

        for (int i = 0; i < supplies.Count; i++)
        {
            SupplyData data = supplies[i];
            if (data == null || string.IsNullOrWhiteSpace(data.id))
                continue;

            string key = data.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[SupplyDatabase] ID duplicado '{key}' em SupplyData. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, data);
        }
    }
}
