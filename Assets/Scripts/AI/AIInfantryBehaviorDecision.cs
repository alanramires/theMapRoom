using UnityEngine;

[System.Obsolete("Legacy type kept only for project file compatibility. Use AIUnitDecision.")]
public struct AIInfantryBehaviorDecision
{
    public bool hasDecision;
    public AIInfantryIntent intent;
    public UnitManager targetEnemy;
    public Vector3Int objectiveCell;
    public string objectiveLabel;

    public static AIInfantryBehaviorDecision None => new AIInfantryBehaviorDecision { hasDecision = false };
}
