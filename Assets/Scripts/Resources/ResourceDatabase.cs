using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Resources/Resource Database", fileName = "ResourceDatabase")]
public class ResourceDatabase : ScriptableObject
{
    [Tooltip("Lista manual de recursos do jogo.")]
    [SerializeField] private List<ResourceData> resources = new List<ResourceData>();

    private readonly Dictionary<string, ResourceData> byId = new Dictionary<string, ResourceData>();

    public IReadOnlyList<ResourceData> Resources => resources;

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

    public bool TryGetById(string id, out ResourceData resource)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            resource = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out resource);
    }

    public bool TryGetFirst(out ResourceData resource)
    {
        for (int i = 0; i < resources.Count; i++)
        {
            if (resources[i] != null)
            {
                resource = resources[i];
                return true;
            }
        }

        resource = null;
        return false;
    }

    private void RebuildLookup()
    {
        byId.Clear();

        for (int i = 0; i < resources.Count; i++)
        {
            ResourceData data = resources[i];
            if (data == null || string.IsNullOrWhiteSpace(data.id))
                continue;

            string key = data.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[ResourceDatabase] ID duplicado '{key}' em ResourceData. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, data);
        }
    }
}
