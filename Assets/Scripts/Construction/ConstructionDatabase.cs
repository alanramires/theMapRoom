using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Construction/Construction Database", fileName = "ConstructionDatabase")]
public class ConstructionDatabase : ScriptableObject
{
    [Tooltip("Lista manual das construcoes que realmente fazem parte do jogo/mapa.")]
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
            if (def == null || string.IsNullOrWhiteSpace(def.id))
                continue;

            string key = def.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[ConstructionDatabase] ID duplicado '{key}' em ConstructionData. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, def);
        }
    }
}
