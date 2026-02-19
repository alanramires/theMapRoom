using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DPQDifferenceRule
{
    [Tooltip("Diferenca minima (DPQ atacante - DPQ defensor).")]
    public int minDifference;

    [Tooltip("Diferenca maxima (DPQ atacante - DPQ defensor).")]
    public int maxDifference;

    [Tooltip("Resultado para o atacante dentro deste intervalo.")]
    public DPQCombatOutcome atacante = DPQCombatOutcome.Neutro;

    [Tooltip("Resultado para o defensor dentro deste intervalo.")]
    public DPQCombatOutcome defensor = DPQCombatOutcome.Neutro;

    public bool Matches(int difference)
    {
        return difference >= minDifference && difference <= maxDifference;
    }
}

[CreateAssetMenu(menuName = "Game/DPQ/DPQ Matchup Database", fileName = "DPQMatchupDatabase")]
public class DPQMatchupDatabase : ScriptableObject
{
    [Tooltip("Tabela configuravel de diferenca de DPQ para atacante e defensor.")]
    [SerializeField] private List<DPQDifferenceRule> rules = new List<DPQDifferenceRule>();

    [Header("Fallback")]
    [Tooltip("Resultado do atacante quando nenhuma regra casar.")]
    [SerializeField] private DPQCombatOutcome fallbackAtacante = DPQCombatOutcome.Neutro;

    [Tooltip("Resultado do defensor quando nenhuma regra casar.")]
    [SerializeField] private DPQCombatOutcome fallbackDefensor = DPQCombatOutcome.Neutro;

    public IReadOnlyList<DPQDifferenceRule> Rules => rules;

    public void Resolve(int attackerDpqPoints, int defenderDpqPoints, out DPQCombatOutcome atacante, out DPQCombatOutcome defensor)
    {
        ResolveByDifference(attackerDpqPoints - defenderDpqPoints, out atacante, out defensor);
    }

    public void ResolveByDifference(int difference, out DPQCombatOutcome atacante, out DPQCombatOutcome defensor)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            DPQDifferenceRule rule = rules[i];
            if (rule == null)
                continue;

            if (rule.Matches(difference))
            {
                atacante = rule.atacante;
                defensor = rule.defensor;
                return;
            }
        }

        atacante = fallbackAtacante;
        defensor = fallbackDefensor;
    }

    public void ResetToSuggestedDefaults()
    {
        rules.Clear();

        rules.Add(new DPQDifferenceRule
        {
            minDifference = 2,
            maxDifference = int.MaxValue,
            atacante = DPQCombatOutcome.Vantagem,
            defensor = DPQCombatOutcome.Desvantagem
        });

        rules.Add(new DPQDifferenceRule
        {
            minDifference = 0,
            maxDifference = 1,
            atacante = DPQCombatOutcome.Vantagem,
            defensor = DPQCombatOutcome.Neutro
        });

        rules.Add(new DPQDifferenceRule
        {
            minDifference = -1,
            maxDifference = -1,
            atacante = DPQCombatOutcome.Neutro,
            defensor = DPQCombatOutcome.Neutro
        });

        rules.Add(new DPQDifferenceRule
        {
            minDifference = int.MinValue,
            maxDifference = -2,
            atacante = DPQCombatOutcome.Desvantagem,
            defensor = DPQCombatOutcome.Vantagem
        });
    }

    private void OnEnable()
    {
        if (rules.Count == 0)
            ResetToSuggestedDefaults();
    }

#if UNITY_EDITOR
    [ContextMenu("Resetar Para Tabela Sugerida")]
    private void ResetFromContextMenu()
    {
        ResetToSuggestedDefaults();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

