using UnityEngine;

[System.Serializable]
public class UnitEmbarkedSupply
{
    [Tooltip("Suprimento escolhido do catalogo (SupplyData).")]
    public SupplyData supply;

    [Tooltip("Quantidade embarcada deste suprimento na unidade.")]
    [Min(0)]
    public int amount = 0;
}
