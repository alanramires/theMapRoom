using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/UI/Helper Database", fileName = "Helper Database")]
public class HelperDatabase : ScriptableObject
{
    [SerializeField] private List<HelperData> messages = new List<HelperData>();
    private readonly Dictionary<string, HelperData> byId = new Dictionary<string, HelperData>();

    public IReadOnlyList<HelperData> Messages => messages;

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

    public string Resolve(string id, string fallback)
    {
        if (TryGetById(id, out HelperData data) && !string.IsNullOrWhiteSpace(data.message))
            return data.message;

        return fallback ?? string.Empty;
    }

    public string Resolve(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        string template = Resolve(id, fallback);
        if (string.IsNullOrEmpty(template) || tokens == null || tokens.Count == 0)
            return template;

        string output = template;
        foreach (KeyValuePair<string, string> pair in tokens)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            output = output.Replace($"<{pair.Key.Trim()}>", pair.Value ?? string.Empty);
        }

        return output;
    }

    public bool TryGetById(string id, out HelperData data)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            data = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out data);
    }

    private void RebuildLookup()
    {
        byId.Clear();

        for (int i = 0; i < messages.Count; i++)
        {
            HelperData data = messages[i];
            if (data == null || string.IsNullOrWhiteSpace(data.id))
                continue;

            string key = data.id.Trim();
            if (byId.ContainsKey(key))
                continue;

            byId.Add(key, data);
        }
    }
}

