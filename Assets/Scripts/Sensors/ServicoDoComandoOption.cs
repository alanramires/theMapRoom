using UnityEngine;
using System.Collections.Generic;

public class ServicoDoComandoOption
{
    public ConstructionManager sourceConstruction;
    public UnitManager sourceSupplierUnit;
    public UnitManager targetUnit;
    public Vector3Int targetCell;
    public bool forceLandBeforeSupply;
    public bool forceTakeoffBeforeSupply;
    public bool forceSurfaceBeforeSupply;
    public Domain plannedServiceDomain;
    public HeightLevel plannedServiceHeight;
    public string displayLabel;
    public List<string> plannedServices = new List<string>();
}
