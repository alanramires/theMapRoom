using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Construction/Construction Field Database", fileName = "ConstructionFieldDatabase")]
public class ConstructionFieldDatabase : ScriptableObject
{
    [Tooltip("Entradas de construcoes em campo para este mapa/cenario.")]
    [SerializeField] private List<ConstructionFieldEntry> entries = new List<ConstructionFieldEntry>();

    public IReadOnlyList<ConstructionFieldEntry> Entries => entries;

    private void OnEnable()
    {
        SanitizeEntries();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SanitizeEntries();
    }
#endif

    private void SanitizeEntries()
    {
        if (entries == null)
            entries = new List<ConstructionFieldEntry>();

        for (int i = 0; i < entries.Count; i++)
        {
            ConstructionFieldEntry entry = entries[i];
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
