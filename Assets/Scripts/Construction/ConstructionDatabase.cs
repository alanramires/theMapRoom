using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Construction/Construction Database", fileName = "ConstructionDatabase")]
public class ConstructionDatabase : ScriptableObject
{
    [Tooltip("Lista manual das construcoes que realmente fazem parte do jogo/mapa.")]
    [SerializeField] private List<ConstructionData> constructions = new List<ConstructionData>();
    [Tooltip("Construcoes instanciadas neste mapa (layout de campo) centralizadas no proprio catalogo.")]
    [SerializeField] private List<ConstructionFieldEntry> fieldEntries = new List<ConstructionFieldEntry>();

    private readonly Dictionary<string, ConstructionData> byId = new Dictionary<string, ConstructionData>();

    public IReadOnlyList<ConstructionData> Constructions => constructions;
    public IReadOnlyList<ConstructionFieldEntry> FieldEntries => fieldEntries;

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

    public bool TryGetById(string id, out ConstructionData construction)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            construction = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out construction);
    }

    public bool TryGetFirst(out ConstructionData construction)
    {
        for (int i = 0; i < constructions.Count; i++)
        {
            if (constructions[i] != null)
            {
                construction = constructions[i];
                return true;
            }
        }

        construction = null;
        return false;
    }

    private void RebuildLookup()
    {
        byId.Clear();
        if (fieldEntries == null)
            fieldEntries = new List<ConstructionFieldEntry>();

        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionData def = constructions[i];
            if (def == null)
                continue;

            // Id vazio nao resolve por id — mesmo risco de fantasma que a duplicata.
            if (string.IsNullOrWhiteSpace(def.id))
            {
                Debug.LogWarning(
                    $"[ConstructionDatabase] ConstructionData '{def.name}' SEM id — nao resolve por id "
                    + "(risco de sumir no jogo). Defina um id unico.", def);
                continue;
            }

            string key = def.id.Trim();
            // 'def' vai como contexto do log: clicar no aviso pinga o asset FANTASMA no Project.
            // So o primeiro com este id fica indexado; o resto e descartado e some no jogo.
            if (byId.TryGetValue(key, out ConstructionData existing))
            {
                Debug.LogWarning(
                    $"[ConstructionDatabase] ID duplicado '{key}': mantendo '{(existing != null ? existing.name : "?")}', "
                    + $"DESCARTANDO '{def.name}' (vira fantasma no jogo). De ids unicos — "
                    + "Tools > Construction > Verificar IDs duplicados.", def);
                continue;
            }

            byId.Add(key, def);
        }

        for (int i = 0; i < fieldEntries.Count; i++)
        {
            ConstructionFieldEntry entry = fieldEntries[i];
            if (entry == null)
                continue;

            if (entry.initialCapturePoints < -1)
                entry.initialCapturePoints = -1;

            if (entry.constructionConfiguration == null)
                entry.constructionConfiguration = new ConstructionSiteRuntime();
            entry.constructionConfiguration.Sanitize();
        }
    }
}
