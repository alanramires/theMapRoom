using UnityEngine;

[System.Serializable]
public class PodeEmbarcarInvalidOption
{
    [Tooltip("Unidade passageira selecionada.")]
    public UnitManager sourceUnit;

    [Tooltip("Unidade adjacente avaliada como possivel transportador.")]
    public UnitManager transporterUnit;

    [Tooltip("Hex adjacente avaliado.")]
    public Vector3Int evaluatedCell;

    [Tooltip("Indice do slot avaliado (quando aplicavel).")]
    public int transporterSlotIndex = -1;

    [Tooltip("Motivo da invalidacao.")]
    public string reason;

    [Tooltip("Custo para entrar no hex do transportador (quando aplicavel).")]
    public int enterCost = -1;

    [Tooltip("Movimento restante do passageiro no momento da avaliacao.")]
    public int remainingMovementBeforeEmbark;
}
