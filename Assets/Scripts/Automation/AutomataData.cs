using System.Collections.Generic;
using UnityEngine;

// Criterio de escolha de alvo do automata — burro por escolha: nada de score
// tatico, o designer decide o temperamento da unidade no asset.
public enum AutomataTargetPreference
{
    [InspectorName("Spawn Order (default)")]
    SpawnOrder = 0,
    [InspectorName("Less HP")]
    LessHp = 1,
    [InspectorName("More HP")]
    MoreHp = 2,
    [InspectorName("Random")]
    Random = 3
}

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
    [Tooltip("Guarnicao: a unidade NUNCA se move no proprio turno. So age quando ha alvo no alcance (ataca parada, se Prefer Attack); sem alvo, nem e selecionada — turno silencioso, sem passeio de cursor. Tem precedencia sobre Move Towards Target. Comandos scriptados do roteiro ('move ...') continuam valendo.")]
    public bool stationary = false;

    [Tooltip("Se verdadeiro, tenta atacar antes de mover.")]
    public bool preferAttack = true;

    [Tooltip("Com mais de um alvo valido: ordem de spawn (fila do sensor, padrao), menor HP, maior HP ou aleatorio.")]
    public AutomataTargetPreference targetPreference = AutomataTargetPreference.SpawnOrder;

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
