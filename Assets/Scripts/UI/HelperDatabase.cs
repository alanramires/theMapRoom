using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/UI/Helper Database", fileName = "Helper Database")]
public class HelperDatabase : ScriptableObject
{
    [SerializeField] private List<HelperData> messages = new List<HelperData>();
    private readonly Dictionary<string, HelperData> byId = new Dictionary<string, HelperData>();

    private readonly HashSet<string> missingIds = new HashSet<string>();

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
        if (TryGetById(id, out HelperData data))
        {
            string message = data.LocalizedMessage;
            if (!string.IsNullOrWhiteSpace(message))
                return message;
        }

        if (!string.IsNullOrWhiteSpace(id) && missingIds.Add(id))
            Debug.LogWarning($"[HelperDatabase] Mensagem ausente ou vazia: {id}", this);
        return fallback ?? string.Empty;
    }

    public string Resolve(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        return MessageTemplate.Apply(Resolve(id, fallback), tokens);
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
        missingIds.Clear();

        for (int i = 0; i < messages.Count; i++)
        {
            HelperData data = messages[i];
            if (data == null || string.IsNullOrWhiteSpace(data.id))
                continue;

            string key = data.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[HelperDatabase] ID duplicado: {key}. Mantendo a primeira mensagem.", this);
                continue;
            }

            byId.Add(key, data);
        }
    }
}

