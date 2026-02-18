using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapons/Weapon Database", fileName = "WeaponDatabase")]
public class WeaponDatabase : ScriptableObject
{
    [Tooltip("Lista manual das armas que fazem parte do jogo.")]
    [SerializeField] private List<WeaponData> weapons = new List<WeaponData>();

    private readonly Dictionary<string, WeaponData> byId = new Dictionary<string, WeaponData>();

    public IReadOnlyList<WeaponData> Weapons => weapons;

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

    public bool TryGetById(string id, out WeaponData weapon)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            weapon = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out weapon);
    }

    public bool TryGetFirst(out WeaponData weapon)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null)
            {
                weapon = weapons[i];
                return true;
            }
        }

        weapon = null;
        return false;
    }

    private void RebuildLookup()
    {
        byId.Clear();

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponData data = weapons[i];
            if (data == null || string.IsNullOrWhiteSpace(data.id))
                continue;

            string key = data.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[WeaponDatabase] ID duplicado '{key}' em WeaponData. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, data);
        }
    }
}
