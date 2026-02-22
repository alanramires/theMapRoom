using UnityEngine;

[System.Serializable]
public class PodeDesembarcarInvalidOption
{
    [Tooltip("Transportador avaliado.")]
    public UnitManager transporterUnit;

    [Tooltip("Passageiro avaliado.")]
    public UnitManager passengerUnit;

    [Tooltip("Indice do slot no UnitData do transportador.")]
    public int transporterSlotIndex = -1;

    [Tooltip("Indice da vaga dentro do slot.")]
    public int transporterSeatIndex = -1;

    [Tooltip("Hex avaliado para desembarque.")]
    public Vector3Int evaluatedCell;

    [Tooltip("Custo de entrada no hex (quando aplicavel).")]
    public int enterCost = -1;

    [Tooltip("Motivo da invalidacao.")]
    public string reason;
}
