using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// Catalogo de papeis (autor, 2026-11-11, D3). O que um rotulo (UnitRole) VALE mora aqui,
// num asset, e nao em switch no codigo: familia (comportamento), Forca (grupo de combate),
// participacao em batalha, "conta como" (a leitura do Shopping) e "satisfaz tambem" (quais
// pedidos este rotulo atende). "Se um dia eu quiser que cacas sejam dissuasao e so eu
// mesmo mudar a flag."
//
// E a regra "a skill e uma chave" aplicada aos papeis: o nome nao faz nada; o catalogo
// diz o que ele e. Um unico asset, em Assets/DB/Resources, editado por Tools > Role
// Families. Sem asset nao ha fallback para tabela em codigo: erro alto.
// =====================================================================================
[System.Serializable]
public class UnitRoleCatalogEntry
{
    public UnitRole role;
    public UnitRoleFamily familia;
    public UnitForce forca;
    public UnitBattleParticipation participacao;
    [Tooltip("Como o Shopping CONTA uma ficha cuja essencia e este rotulo. None = o proprio rotulo.")]
    public UnitRole contaComo;
    [Tooltip("Pedidos (vagas/demandas) que este rotulo atende alem dele mesmo.")]
    public List<UnitRole> satisfazTambem = new List<UnitRole>();
}

[CreateAssetMenu(menuName = "Map Room/Unit Role Catalog", fileName = "UnitRoleCatalog")]
public class UnitRoleCatalog : ScriptableObject
{
    public const string ResourceName = "UnitRoleCatalog";

    [SerializeField] private List<UnitRoleCatalogEntry> entries = new List<UnitRoleCatalogEntry>();
    public IReadOnlyList<UnitRoleCatalogEntry> Entries => entries;

    private Dictionary<UnitRole, UnitRoleCatalogEntry> index;

    public UnitRoleCatalogEntry Get(UnitRole role)
    {
        if (index == null)
        {
            index = new Dictionary<UnitRole, UnitRoleCatalogEntry>();
            foreach (UnitRoleCatalogEntry e in entries)
                if (e != null && !index.ContainsKey(e.role)) index[e.role] = e;
        }
        return index.TryGetValue(role, out UnitRoleCatalogEntry entry) ? entry : null;
    }

    // Editor: garante a linha (papel novo no enum ainda sem linha no asset).
    public UnitRoleCatalogEntry GetOrAdd(UnitRole role)
    {
        UnitRoleCatalogEntry entry = Get(role);
        if (entry != null) return entry;
        entry = new UnitRoleCatalogEntry { role = role, contaComo = role };
        entries.Add(entry);
        Invalidate();
        return entry;
    }

    public void Invalidate() => index = null;

    private static UnitRoleCatalog active;
    private static bool missingReported;

    public static UnitRoleCatalog Active
    {
        get
        {
            if (active != null) return active;
            active = Resources.Load<UnitRoleCatalog>(ResourceName);
#if UNITY_EDITOR
            if (active == null)
                foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:UnitRoleCatalog"))
                {
                    active = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitRoleCatalog>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (active != null) break;
                }
#endif
            if (active == null && !missingReported)
            {
                missingReported = true;
                Debug.LogError($"[UnitRoleCatalog] asset '{ResourceName}' ausente em Resources. Todo papel vale " +
                               "Nenhuma/None ate o catalogo existir (tools/ai_test/criar_catalogo_papeis.cs o cria).");
            }
            return active;
        }
    }

    // Fixtures: trocam o catalogo por um clone em memoria e restauram depois.
    public static void SetActiveForTests(UnitRoleCatalog catalog)
    {
        active = catalog;
        if (catalog != null) catalog.Invalidate();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; missingReported = false; }
}
