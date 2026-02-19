using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PodeMirarTargetOption
{
    [Tooltip("Unidade atacante (origem da acao).")]
    public UnitManager attackerUnit;

    [Tooltip("Unidade alvo desta opcao.")]
    public UnitManager targetUnit;

    [Tooltip("Arma embarcada que pode atingir o alvo.")]
    public WeaponData weapon;

    [Tooltip("Indice da arma na lista embarkedWeapons da unidade.")]
    public int embarkedWeaponIndex;

    [Tooltip("Distancia hex entre atacante e alvo.")]
    public int distance;

    [Tooltip("Texto pronto para listar no menu de alvos.")]
    public string displayLabel;

    [Header("Prioridade de Alvo")]
    [Tooltip("Ordem original de descoberta no sensor (debug).")]
    public int sensorOrder;

    [Tooltip("Se este alvo e preferido para a categoria desta arma (debug).")]
    public bool isPreferredTargetForWeapon;

    [Header("Posicao (prioridade: construcao > estrutura > terreno)")]
    [Tooltip("Descricao da posicao atual do atacante no hex.")]
    public string attackerPositionLabel;

    [Tooltip("Descricao da posicao atual do defensor/alvo no hex.")]
    public string defenderPositionLabel;

    [Tooltip("Hexes intermediarios analisados na linha de tiro (debug).")]
    public List<Vector3Int> lineOfFireIntermediateCells = new List<Vector3Int>();

    [Header("Revide")]
    [Tooltip("Se o defensor consegue revidar este ataque.")]
    public bool defenderCanCounterAttack;

    [Tooltip("Arma escolhida pelo defensor para revide.")]
    public WeaponData defenderCounterWeapon;

    [Tooltip("Indice da arma de revide na lista embarkedWeapons da unidade defensora.")]
    public int defenderCounterEmbarkedWeaponIndex = -1;

    [Tooltip("Distancia usada no revide.")]
    public int defenderCounterDistance;

    [Tooltip("Motivo quando o defensor nao revida.")]
    public string defenderCounterReason;
}
