using UnityEngine;

[System.Serializable]
public class PodeEmbarcarOption
{
    [Tooltip("Unidade selecionada que pode embarcar.")]
    public UnitManager sourceUnit;

    [Tooltip("Transportador alvo.")]
    public UnitManager transporterUnit;

    [Tooltip("Indice do slot elegivel no UnitData do transportador.")]
    public int transporterSlotIndex;

    [Tooltip("Texto amigavel para debug/UI.")]
    public string displayLabel;

    [Tooltip("Custo para o passageiro entrar no hex do transportador.")]
    public int enterCost;

    [Tooltip("Pontos de movimento restantes antes do embarque.")]
    public int remainingMovementBeforeEmbark;
}
