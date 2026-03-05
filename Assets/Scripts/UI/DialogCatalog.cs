using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogMessageEntry
{
    [SerializeField] private string id;
    [SerializeField] [TextArea(1, 3)] private string condition;
    [SerializeField] [TextArea(1, 6)] private string message;

    public string Id => id;
    public string Condition => condition;
    public string Message => message;
}

[CreateAssetMenu(menuName = "Game/UI/Dialog Catalog", fileName = "Dialog Catalog")]
public class DialogCatalog : ScriptableObject
{
    [SerializeField] private List<DialogMessageEntry> entries = new List<DialogMessageEntry>();

    private readonly Dictionary<string, string> index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private bool isIndexDirty = true;

    public string Resolve(string id, string fallback)
    {
        if (string.IsNullOrWhiteSpace(id))
            return fallback ?? string.Empty;

        RebuildIndexIfNeeded();
        if (index.TryGetValue(id.Trim(), out string value) && !string.IsNullOrWhiteSpace(value))
            return value;

        return fallback ?? string.Empty;
    }

    public string Resolve(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        string template = Resolve(id, fallback);
        if (tokens == null || tokens.Count == 0 || string.IsNullOrEmpty(template))
            return template;

        string output = template;
        foreach (KeyValuePair<string, string> pair in tokens)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            string token = $"<{pair.Key.Trim()}>";
            output = output.Replace(token, pair.Value ?? string.Empty);
        }

        return output;
    }

    private void RebuildIndexIfNeeded()
    {
        if (!isIndexDirty)
            return;

        index.Clear();
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                DialogMessageEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    continue;

                string key = entry.Id.Trim();
                if (!index.ContainsKey(key))
                    index.Add(key, entry.Message ?? string.Empty);
            }
        }

        isIndexDirty = false;
    }

    private void OnValidate()
    {
        isIndexDirty = true;
    }
}

