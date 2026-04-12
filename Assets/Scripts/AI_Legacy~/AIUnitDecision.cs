using UnityEngine;

public struct AIUnitDecision
{
    public bool HasDecision;
    public AIUnitIntent Intent;
    public UnitManager TargetEnemy;
    public Vector3Int ObjectiveCell;
    public string ObjectiveLabel;

    public static AIUnitDecision None => new AIUnitDecision { HasDecision = false };

    public static AIUnitDecision Capture(Vector3Int objectiveCell, string objectiveLabel)
    {
        return new AIUnitDecision
        {
            HasDecision = true,
            Intent = AIUnitIntent.Capture,
            ObjectiveCell = objectiveCell,
            ObjectiveLabel = objectiveLabel ?? string.Empty
        };
    }

    public static AIUnitDecision Engage(UnitManager targetEnemy)
    {
        return new AIUnitDecision
        {
            HasDecision = true,
            Intent = AIUnitIntent.Engage,
            TargetEnemy = targetEnemy
        };
    }

    public static AIUnitDecision Reposition()
    {
        return new AIUnitDecision
        {
            HasDecision = true,
            Intent = AIUnitIntent.Reposition
        };
    }

    public static AIUnitDecision Fallback()
    {
        return new AIUnitDecision
        {
            HasDecision = true,
            Intent = AIUnitIntent.Fallback
        };
    }
}
