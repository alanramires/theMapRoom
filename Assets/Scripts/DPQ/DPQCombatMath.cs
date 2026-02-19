using UnityEngine;

public static class DPQCombatMath
{
    public static int RoundWithOutcome(float value, DPQCombatOutcome outcome)
    {
        bool isInteger = Mathf.Abs(value - Mathf.Round(value)) < 0.0001f;

        switch (outcome)
        {
            case DPQCombatOutcome.Vantagem:
            {
                int rounded = Mathf.CeilToInt(value);
                return isInteger ? rounded + 1 : rounded;
            }
            case DPQCombatOutcome.Desvantagem:
            {
                int rounded = Mathf.FloorToInt(value);
                return isInteger ? rounded - 1 : rounded;
            }
            default:
                return Mathf.RoundToInt(value);
        }
    }

    public static int DivideAndRound(int numerator, int denominator, DPQCombatOutcome outcome)
    {
        if (denominator == 0)
            return 0;

        float raw = (float)numerator / denominator;
        return RoundWithOutcome(raw, outcome);
    }
}

