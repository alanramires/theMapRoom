using UnityEngine;

public enum TransferFlowMode
{
    Fornecimento = 0,
    Recebedor = 1
}

public class PodeTransferirOption
{
    public UnitManager supplierUnit;
    public UnitManager targetUnit;
    public ConstructionManager targetConstruction;
    public Vector3Int targetCell;
    public TransferFlowMode flowMode;
    public string displayLabel;
}
