using UnityEngine;

public enum ConstructionSupplierRangeMode
{
    OverlappingOnly = 0,
    Adjacent1Hex = 1
}

[System.Serializable]
public class ConstructionSupplierResourceCapacity
{
    public SupplyData supply;
    [Min(0)] public int maxCapacity = 0;
}
