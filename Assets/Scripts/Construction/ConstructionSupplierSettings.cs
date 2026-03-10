using UnityEngine;
using UnityEngine.Serialization;

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
    [Tooltip("Quando ligado, esta construcao oferece supply infinito para este item.")]
    public bool hasInfiniteSupply = false;

    [Tooltip("Capacidade maxima deste supply quando nao for infinito.")]
    [FormerlySerializedAs("capacity")]
    [Min(0)] public int maxCapacity = 0;

    public void Sanitize()
    {
        // Migracao legado: -1 representava infinito.
        if (maxCapacity < 0)
        {
            hasInfiniteSupply = true;
            maxCapacity = 0;
        }
        else
        {
            maxCapacity = Mathf.Max(0, maxCapacity);
        }
    }

    public bool IsInfinite()
    {
        return hasInfiniteSupply || maxCapacity < 0;
    }
}
