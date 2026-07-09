using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AutomataData", menuName = "Game/Automation/Automata Data")]
public class AutomataData : ScriptableObject
{
    [Header("Identity")]
    public string id;

    [Tooltip("Token da unidade para casar (id/display/apelido/nome). Ex: APC, SD, CANHAO.")]
    public string unitToken;

    [Tooltip("Time que usa esta regra.")]
    public TeamId teamId = TeamId.Red;

    [Header("Behavior")]
    [Tooltip("Se verdadeiro, tenta atacar antes de mover.")]
    public bool preferAttack = true;

    [Tooltip("Se verdadeiro, faz fallback para M quando nao conseguir atacar.")]
    public bool fallbackMove = true;

    [Header("Movement")]
    [Tooltip("Se verdadeiro, a unidade avanca em direcao ao hex alvo a cada turno, usando o movimento real (custos de terreno, ocupacao), antes de atacar/finalizar.")]
    public bool moveTowardsTarget = false;

    [Tooltip("Hex alvo do avanco (z ignorado).")]
    public Vector3Int moveTargetCell;

    [Tooltip("Para de avancar quando a distancia hex ate o alvo for menor ou igual a este valor. Ex.: 1 = parar adjacente ao alvo.")]
    [Min(0)] public int stopDistance = 0;

    [Header("Scope")]
    [Tooltip("Se vazio, vale para qualquer tutorial. Se preenchido, so vale para os TutorialData listados.")]
    public List<TutorialData> tutorials = new List<TutorialData>();
}
