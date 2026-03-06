using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PodeMirarInvalidOption
{
    public const string ReasonIdGeneric = "aim.invalid.target";
    public const string ReasonIdOutOfRange = "aim.invalid.out_of_range";
    public const string ReasonIdNoAmmo = "aim.invalid.no_ammo";
    public const string ReasonIdLayer = "aim.invalid.layer";
    public const string ReasonIdLdtBlocked = "aim.invalid.ldt_blocked";
    public const string ReasonIdLosBlocked = "aim.invalid.los_blocked";
    public const string ReasonIdNoForwardObserver = "aim.invalid.no_forward_observer";
    public const string ReasonIdStealth = "aim.invalid.stealth";

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

    [Header("Posicao (prioridade: construcao > estrutura > terreno)")]
    [Tooltip("Descricao da posicao atual do atacante no hex.")]
    public string attackerPositionLabel;

    [Tooltip("Descricao da posicao atual do defensor/alvo no hex.")]
    public string defenderPositionLabel;

    [Tooltip("Motivo de invalidacao.")]
    public string reason;

    [Tooltip("ID de mensagem para dialog database (i18n/customizacao).")]
    public string reasonId;

    [Tooltip("Hex intermediario bloqueador (quando aplicavel).")]
    public Vector3Int blockedCell;

    [Tooltip("Hexes intermediarios analisados na linha de tiro (debug).")]
    public List<Vector3Int> lineOfFireIntermediateCells = new List<Vector3Int>();

    [Tooltip("Perfil EV da linha (origem -> intermediarios -> alvo), para debug de LoS.")]
    public List<float> lineOfFireEvPath = new List<float>();
}
