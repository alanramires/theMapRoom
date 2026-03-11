using System.Collections.Generic;
using UnityEngine;

public sealed class PodeDetectarOption
{
    public UnitManager observerUnit;
    public UnitManager targetUnit;
    public Vector3Int observerCell;
    public Vector3Int targetCell;
    public int distance;
    public Domain targetDomain;
    public HeightLevel targetHeightLevel;
    public int detectionRangeUsed;
    public bool hasDirectLos;
    public bool usedForwardObserver;
    public UnitManager forwardObserverUnit;
    public List<Vector3Int> lineOfSightIntermediateCells = new List<Vector3Int>();
    public Vector3Int blockedCell = Vector3Int.zero;
    public string reason;
}
