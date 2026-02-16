using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Units/Unit Database", fileName = "UnitDatabase")]
public class UnitDatabase : ScriptableObject
{
    [Tooltip("Lista manual das unidades que realmente fazem parte do jogo/mapa.")]
    [SerializeField] private List<UnitData> units = new List<UnitData>();

    private readonly Dictionary<string, UnitData> byId = new Dictionary<string, UnitData>();

    public IReadOnlyList<UnitData> Units => units;

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

    public bool TryGetById(string id, out UnitData unit)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            unit = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out unit);
    }

    public bool TryGetFirst(out UnitData unit)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null)
            {
                unit = units[i];
                return true;
            }
        }

        unit = null;
        return false;
    }

    private void RebuildLookup()
    {
        byId.Clear();

        for (int i = 0; i < units.Count; i++)
        {
            UnitData def = units[i];
            if (def == null || string.IsNullOrWhiteSpace(def.id))
                continue;

            string key = def.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[UnitDatabase] ID duplicado '{key}' em UnitData. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, def);
        }
    }
}
