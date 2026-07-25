using UnityEngine;

[System.Serializable]
public class UnitTransportSeatRuntime
{
    [Tooltip("Indice do slot em UnitData.transportSlots.")]
    public int slotIndex = -1;

    [Tooltip("ID de debug do slot.")]
    public string slotId = "slot";

    [Tooltip("Indice da vaga dentro do slot.")]
    public int seatIndex;

    [Tooltip("Unidade embarcada nesta vaga. Null = vaga livre.")]
    public UnitManager embarkedUnit;

    [Tooltip("Turno confirmado em que a unidade ocupou esta vaga. -1 = vaga livre/legado.")]
    public int embarkedOnTurn = -1;
}
