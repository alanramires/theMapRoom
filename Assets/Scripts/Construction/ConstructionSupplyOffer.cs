using UnityEngine;

[System.Serializable]
public class ConstructionSupplyOffer
{
    public SupplyData supply;
    [Min(0)] public int quantity = 0;
}
