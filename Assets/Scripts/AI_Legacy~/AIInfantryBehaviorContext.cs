using UnityEngine;

[System.Obsolete("Legacy type kept only for project file compatibility. Use AIUnitDecisionContext.")]
public struct AIInfantryBehaviorContext
{
    public TeamId Team;
    public AISnapshot Snapshot;
    public UnitManager Unit;
    public UnitManager AssignedEnemy;
    public UnitManager IntelFireTarget;
    public Vector3Int UnitCell;
    public Vector3Int CaptureObjectiveCell;
    public string CaptureObjectiveLabel;
    public bool DefendMode;
    public bool CaptureObjectiveAvailable;
    public bool CaptureNowAvailable;
}
