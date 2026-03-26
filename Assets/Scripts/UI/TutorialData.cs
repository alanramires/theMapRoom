using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TutorialObjective
{
    public string id;
    public string parameters;
    public string description;
    public bool startHidden = false;
    public bool isVisible = true;
    public bool isCompleted = false;
    public bool isOptional = false;
    public bool isDefeatCondition = false;
    public bool hasFailed = false;
}

[CreateAssetMenu(fileName = "Novo TutorialData", menuName = "Game/Tutorial/Tutorial Data")]
public class TutorialData : ScriptableObject
{
    public string id;

    [Tooltip("Texto descritivo / Sobre o tutorial")]
    [TextArea(3, 10)]
    public string description;

    [Tooltip("Lista de objetivos deste tutorial")]
    public List<TutorialObjective> objectives = new List<TutorialObjective>();

    [Header("Victory")]
    [Tooltip("Dialogo exibido ao completar todos os objetivos.")]
    public DialogData victoryDialog;
}
