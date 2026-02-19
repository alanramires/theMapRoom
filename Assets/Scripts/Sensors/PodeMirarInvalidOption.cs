using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PodeMirarInvalidOption
{
    [Tooltip("Unidade atacante (origem da acao).")]
    public UnitManager attackerUnit;

    [Tooltip("Unidade alvo que foi descartada.")]
    public UnitManager targetUnit;

    [Tooltip("Arma avaliada para este alvo.")]
    public WeaponData weapon;

    [Tooltip("Indice da arma avaliada na unidade atacante.")]
    public int embarkedWeaponIndex;

    [Tooltip("Distancia hex entre atacante e alvo.")]
    public int distance;

    [Tooltip("Motivo de invalidacao.")]
    public string reason;

    [Tooltip("Hex intermediario bloqueador (quando aplicavel).")]
    public Vector3Int blockedCell;

    [Tooltip("Hexes intermediarios analisados na linha de tiro (debug).")]
    public List<Vector3Int> lineOfFireIntermediateCells = new List<Vector3Int>();
}

