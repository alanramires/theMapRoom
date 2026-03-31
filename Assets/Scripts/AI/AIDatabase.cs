using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/AI/AI Database", fileName = "AIDatabase")]
public class AIDatabase : ScriptableObject
{
    [Tooltip("Catalogo oficial de perfis de IA disponiveis para os generais.")]
    public List<AIGeneralProfile> profiles = new List<AIGeneralProfile>();

    public bool ContainsProfile(AIGeneralProfile profile)
    {
        if (profile == null || profiles == null)
            return false;

        for (int i = 0; i < profiles.Count; i++)
        {
            if (profiles[i] == profile)
                return true;
        }

        return false;
    }

    public AIData ResolveFor(AIStance stance, AIGeneralProfile profile)
    {
        if (!ContainsProfile(profile))
            return null;

        if (profile.aiData == null)
            return null;

        return profile.aiData;
    }

    // Compatibilidade com chamadas antigas. Nao escolhe perfil automaticamente.
    public AIData ResolveFor(AIStance stance)
    {
        return null;
    }
}
