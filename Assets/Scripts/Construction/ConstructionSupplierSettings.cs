using UnityEngine;

public enum ConstructionSupplierRangeMode
{
    OverlappingOnly = 0,
    Adjacent1Hex = 1,
    Hybrid0Or1Hex = 2
}

[System.Serializable]
public class ConstructionSupplierResourceCapacity
{
    public SupplyData supply;
    [Tooltip("Capacidade maxima deste supply. Use -1 para infinito.")]
    [Min(-1)] public int maxCapacity = 0;
}
