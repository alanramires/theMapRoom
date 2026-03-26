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

    [Header("Scope")]
    [Tooltip("Se vazio, vale para qualquer tutorial. Se preenchido, so vale para os TutorialData listados.")]
    public List<TutorialData> tutorials = new List<TutorialData>();
}
