using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Autonomy/Autonomy Database", fileName = "AutonomyDatabase")]
public class AutonomyDatabase : ScriptableObject
{
    [SerializeField] private List<AutonomyData> autonomies = new List<AutonomyData>();
    private readonly Dictionary<string, AutonomyData> byId = new Dictionary<string, AutonomyData>();

    public IReadOnlyList<AutonomyData> Autonomies => autonomies;

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

    public bool TryResolve(AutonomyData reference, out AutonomyData resolved)
    {
        resolved = null;
        if (reference == null)
            return false;

        if (autonomies != null)
        {
            for (int i = 0; i < autonomies.Count; i++)
            {
                if (autonomies[i] == reference)
                {
                    resolved = autonomies[i];
                    return true;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(reference.id))
            return false;

        return TryGetById(reference.id, out resolved);
    }

    public bool TryGetById(string id, out AutonomyData autonomy)
    {
        autonomy = null;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out autonomy);
    }

    private void RebuildLookup()
    {
        byId.Clear();
        if (autonomies == null)
            return;

        for (int i = 0; i < autonomies.Count; i++)
        {
            AutonomyData data = autonomies[i];
            if (data == null || string.IsNullOrWhiteSpace(data.id))
                continue;

            string key = data.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[AutonomyDatabase] ID duplicado '{key}'. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, data);
        }
    }
}
