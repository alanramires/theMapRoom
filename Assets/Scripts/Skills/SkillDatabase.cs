using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/Skill Database", fileName = "SkillDatabase")]
public class SkillDatabase : ScriptableObject
{
    [Tooltip("Lista manual das skills disponiveis no jogo.")]
    [SerializeField] private List<SkillData> skills = new List<SkillData>();

    private readonly Dictionary<string, SkillData> byId = new Dictionary<string, SkillData>();

    public IReadOnlyList<SkillData> Skills => skills;

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

    public bool TryGetById(string id, out SkillData skill)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            skill = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out skill);
    }

    public bool TryGetFirst(out SkillData skill)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] != null)
            {
                skill = skills[i];
                return true;
            }
        }

        skill = null;
        return false;
    }

    private void RebuildLookup()
    {
        byId.Clear();

        for (int i = 0; i < skills.Count; i++)
        {
            SkillData def = skills[i];
            if (def == null || string.IsNullOrWhiteSpace(def.id))
                continue;

            string key = def.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[SkillDatabase] ID duplicado '{key}' em SkillData. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, def);
        }
    }
}
