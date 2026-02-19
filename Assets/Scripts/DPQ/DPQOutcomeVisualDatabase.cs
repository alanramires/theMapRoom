using UnityEngine;

[CreateAssetMenu(menuName = "Game/DPQ/DPQ Outcome Visual Database", fileName = "DPQOutcomeVisualDatabase")]
public class DPQOutcomeVisualDatabase : ScriptableObject
{
    [Header("Sprites")]
    [Tooltip("Seta para resultado de vantagem.")]
    [SerializeField] private Sprite vantagemSprite;

    [Tooltip("Seta para resultado neutro.")]
    [SerializeField] private Sprite neutroSprite;

    [Tooltip("Seta para resultado de desvantagem.")]
    [SerializeField] private Sprite desvantagemSprite;

    public Sprite GetSprite(DPQCombatOutcome outcome)
    {
        switch (outcome)
        {
            case DPQCombatOutcome.Vantagem: return vantagemSprite;
            case DPQCombatOutcome.Desvantagem: return desvantagemSprite;
            default: return neutroSprite;
        }
    }
}

