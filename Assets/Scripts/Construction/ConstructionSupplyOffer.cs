using UnityEngine;

[System.Serializable]
public class ConstructionSupplyOffer
{
    public SupplyData supply;
    [Min(0)] public int quantity = 0;
    // Max dinamico (marca d'agua): maior quantidade que este supply ja teve
    // nesta construcao. Comecou com 40 -> teto 40; doacao elevou a 100 -> teto
    // 100. E o denominador do alerta de estoque (half no threshold, empty no 0).
    [Min(0)] public int peakQuantity = 0;
}
