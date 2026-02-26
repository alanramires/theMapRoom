using UnityEngine;

public class PodeSuprirOption
{
    public UnitManager supplierUnit;
    public UnitManager targetUnit;
    public Vector3Int targetCell;
    public bool forceLandBeforeSupply;
    public bool forceTakeoffBeforeSupply;
    public bool forceSurfaceBeforeSupply;
    public Domain plannedServiceDomain;
    public HeightLevel plannedServiceHeight;
    public string displayLabel;
}
