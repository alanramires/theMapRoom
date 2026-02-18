using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Services/Service Database", fileName = "ServiceDatabase")]
public class ServiceDatabase : ScriptableObject
{
    [SerializeField] private List<ServiceData> services = new List<ServiceData>();
    private readonly Dictionary<string, ServiceData> byId = new Dictionary<string, ServiceData>();

    public IReadOnlyList<ServiceData> Services => services;

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

    public bool TryGetById(string id, out ServiceData service)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            service = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out service);
    }

    private void RebuildLookup()
    {
        byId.Clear();
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData data = services[i];
            if (data == null || string.IsNullOrWhiteSpace(data.id))
                continue;

            string key = data.id.Trim();
            if (byId.ContainsKey(key))
                continue;

            byId.Add(key, data);
        }
    }
}
