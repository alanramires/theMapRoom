using System.Collections.Generic;
using UnityEngine;

public enum SupplierTier
{
    Hub = 0,
    Receiver = 1,
    SelfSupplier = 2
}

public enum SupplierRangeMode
{
    EmbarkedOnly = 0,
    Adjacent1Hex = 1
}

[System.Serializable]
public struct SupplierOperationDomain
{
    public Domain domain;
    public HeightLevel heightLevel;

    public SupplierOperationDomain(Domain domain, HeightLevel heightLevel)
    {
        this.domain = domain;
        this.heightLevel = heightLevel;
    }
}

[System.Serializable]
public class SupplierEmbarkedSupplyCapacity
{
    public SupplyData supply;
    [Min(0)] public int maxCapacity = 0;
}
