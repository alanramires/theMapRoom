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

[System.Serializable]
public class TutorialDialogEntry
{
    [Tooltip("Se >= 0, esta fala so aparece depois que o objetivo neste INDICE da lista de objectives completar. -1 = segue na sequencia pelo botao avancar.")]
    public int waitObjectiveIndex = -1;

    [TextArea(2, 10)]
    public string text;

    [Tooltip("Narracao gravada da fala (opcional). Toca ao exibir.")]
    public AudioClip voice;
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

    [Header("Roteiro")]
    [Tooltip("Falas do panel_dialog_tutorial, em ordem. Gates por waitObjectiveIndex pausam o roteiro ate a tarefa completar.")]
    public List<TutorialDialogEntry> script = new List<TutorialDialogEntry>();

    [Header("Victory")]
    [Tooltip("Dialogo exibido ao completar todos os objetivos.")]
    public DialogData victoryDialog;
}
