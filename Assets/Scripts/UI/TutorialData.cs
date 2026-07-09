using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TutorialObjective
{
    public string id;

    [Tooltip("Identificador unico da tarefa para gates/reveals do roteiro. Padrao: hist_Y_XX (ex.: hist_1_04 = tarefa 04 da Historia 1). O campo 'id' continua sendo o TIPO de evento (UNIT_AT_HEX, END_TURN...).")]
    public string key;

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
    [Tooltip("Se preenchido, esta fala so aparece depois que o objetivo com esta KEY (ex.: hist_1_04) completar. Tem precedencia sobre waitObjectiveIndex.")]
    public string waitObjectiveKey;

    [Tooltip("LEGADO (prefira waitObjectiveKey). Se >= 0, esta fala so aparece depois que o objetivo neste INDICE completar. -1 = segue na sequencia.")]
    public int waitObjectiveIndex = -1;

    [TextArea(2, 10)]
    public string text;

    [Tooltip("Narracao gravada da fala (opcional). Toca ao exibir.")]
    public AudioClip voice;

    [Tooltip("Spawns executados quando esta fala aparece (uma unica vez). Formato: 'slot0 SD 1,3' ou '1 SD 5,6'. Opcoes apos as coordenadas: 'acted' (nasce ja agiu), 'name=Ryan' (renomeia, _ vira espaco), 'cursor' (move o cursor ate a unidade). Multiplos separados por ';'. Ex.: slot0 SD 1,3 name=Ryan cursor")]
    public string spawnCommand;

    [Tooltip("Ajustes de status executados quando esta fala aparece (uma unica vez). Formato: 'NOME stat=valor' com stats hp, fuel e ammo; NOME casa por nome/apelido/id da unidade. Multiplos separados por ';'. Ex.: Mathias hp=4; Dias fuel=15")]
    public string statCommand;

    [Tooltip("Se true, o passar a vez (R, panel_remaining, menu) e liberado a partir desta fala. Se QUALQUER fala do roteiro tiver esta flag, a cena comeca com o passar a vez travado.")]
    public bool unlockEndTurn;

    [Tooltip("Se preenchido, revela o objetivo com esta KEY (ex.: hist_1_04) quando esta fala aparece. Tem precedencia sobre revealObjectiveIndex. Se QUALQUER fala do roteiro usar reveal (key ou indice), o manager para de revelar tarefas sozinho e o painel comeca vazio.")]
    public string revealObjectiveKey;

    [Tooltip("LEGADO (prefira revealObjectiveKey). Se >= 0, revela o objetivo neste INDICE quando esta fala aparece.")]
    public int revealObjectiveIndex = -1;
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

    [Header("Bloqueios")]
    [Tooltip("Bloqueia o Servico do Comando (Reabastecer, atalho X) durante este tutorial.")]
    public bool blockCommandService;

    [Tooltip("Bloqueia dispensar/destruir unidades (atalho U e menu Gerenciar) durante este tutorial.")]
    public bool blockRemoveUnit;

    [Tooltip("Bloqueia render-se durante este tutorial.")]
    public bool blockSurrender;

    [Tooltip("Bloqueia a Situacao (estatisticas de combate do menu) durante este tutorial.")]
    public bool blockStatusSummary;

    [Header("Victory")]
    [Tooltip("Dialogo exibido ao completar todos os objetivos.")]
    public DialogData victoryDialog;

    // Resolve a key unica (hist_Y_XX) para o indice na lista de objectives. -1 se nao achar.
    public int FindObjectiveIndexByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || objectives == null)
            return -1;

        string trimmed = key.Trim();
        for (int i = 0; i < objectives.Count; i++)
        {
            TutorialObjective obj = objectives[i];
            if (obj != null && !string.IsNullOrWhiteSpace(obj.key) &&
                string.Equals(obj.key.Trim(), trimmed, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
