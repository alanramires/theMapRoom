using UnityEngine;

[System.Serializable]
public class PodeDesembarcarOption
{
    [Tooltip("Transportador selecionado.")]
    public UnitManager transporterUnit;

    [Tooltip("Passageiro que pode desembarcar.")]
    public UnitManager passengerUnit;

    [Tooltip("Indice do slot no UnitData do transportador.")]
    public int transporterSlotIndex = -1;

    [Tooltip("Indice da vaga dentro do slot.")]
    public int transporterSeatIndex = -1;

    [Tooltip("Hex de destino para o desembarque.")]
    public Vector3Int disembarkCell;

    [Tooltip("Custo de desembarque (fixo para este sensor).")]
    public int disembarkCost = 1;

    [Tooltip("Custo de entrada no hex calculado pelas regras de movimento (sem multiplicador de autonomia).")]
    public int enterCost = 1;

    [Tooltip("Texto amigavel para debug/UI.")]
    public string displayLabel;
}
