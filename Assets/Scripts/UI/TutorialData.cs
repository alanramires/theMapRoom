using UnityEngine;
using UnityEngine.Serialization;
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

// Acao bloqueada que dispara bronca do Sargento no tutorial.
public enum TutorialScoldKind
{
    EndTurnLocked = 0,
    CommandService = 1,
    RemoveUnit = 2,
    Surrender = 3,
    StatusSummary = 4,
    MovementLocked = 5,
    HoldPosition = 6,
    AttackOrdered = 7
}

public enum TutorialEndTurnEffect
{
    [InspectorName("No Effect (default)")]
    NoEffect = 0,
    Locked = 1,
    Unlocked = 2
}

public enum TutorialAdvanceCondition
{
    [InspectorName("Immediate (default)")]
    Immediate = 0,
    [InspectorName("Objective Completed")]
    ObjectiveCompleted = 1,
    [InspectorName("All Units Acted")]
    AllUnitsActed = 2,
    [InspectorName("Player Turn Started")]
    PlayerTurnStarted = 3,
    [InspectorName("Enemy Turn Started")]
    EnemyTurnStarted = 4,
    [InspectorName("Aim Opened (Mirar)")]
    AimOpened = 5
}

// Estado persistente do movimento do jogador definido pelo roteiro.
// HoldOnly permite confirmar na PROPRIA celula (manter posicao / atacar parado),
// mas bloqueia sair dela — "ninguem desce do morro sem ordem".
public enum TutorialMovementEffect
{
    [InspectorName("No Effect (default)")]
    NoEffect = 0,
    [InspectorName("Locked (nem mover, nem manter)")]
    Locked = 1,
    [InspectorName("Hold Only (manter/atacar sim, sair nao)")]
    HoldOnly = 2,
    [InspectorName("Unlocked")]
    Unlocked = 3,
    [InspectorName("Attack Only (mirar sim, finalizar parado nao)")]
    AttackOnly = 4
}

[System.Serializable]
public class TutorialScoldEntry
{
    [TextArea(2, 5)]
    [Tooltip("Texto da bronca. Vazio = usa o texto padrao do codigo.")]
    public string text;

    [Tooltip("Voz gravada da bronca (opcional). Toca junto com o balao.")]
    public AudioClip voice;
}

[System.Serializable]
public class TutorialDialogEntry
{
    [Tooltip("Define quando esta fala avanca para a seguinte.")]
    public TutorialAdvanceCondition advance;

    [Tooltip("Objetivo usado por Objective Completed e/ou revelado por esta fala.")]
    public string objectiveKey;

    [Tooltip("Revela Objective Key quando esta fala aparece.")]
    public bool revealObjective;

    [HideInInspector]
    [Tooltip("Se preenchido, esta fala so aparece depois que o objetivo com esta KEY (ex.: hist_1_04) completar. Tem precedencia sobre waitObjectiveIndex.")]
    public string waitObjectiveKey;

    [HideInInspector]
    [Tooltip("LEGADO (prefira waitObjectiveKey). Se >= 0, esta fala so aparece depois que o objetivo neste INDICE completar. -1 = segue na sequencia.")]
    public int waitObjectiveIndex = -1;

    [HideInInspector]
    [Tooltip("Gate extra: esta fala so aparece quando TODAS as unidades do jogador ja agiram (ex.: logo apos executar o movimento ordenado).")]
    public bool waitAllUnitsActed;

    [HideInInspector]
    [Tooltip("Gate extra: esta fala so aparece quando um NOVO turno do jogador comecar (depois da IA jogar).")]
    public bool waitPlayerTurnStart;

    [TextArea(2, 10)]
    [Tooltip("Texto da fala. VAZIO = fala muda: executa os comandos (spawn/stat/turn/movement) sem abrir o balao — util para direcao de cena no turno inimigo (ex.: spawn + pan).")]
    public string text;

    [Tooltip("Narracao gravada da fala (opcional). Toca ao exibir.")]
    public AudioClip voice;

    [Tooltip("Spawns executados quando esta fala aparece (uma unica vez). Formato: 'slot0 SD 1,3' ou '1 SD 5,6'. Opcoes apos as coordenadas: 'acted' (nasce ja agiu), 'name=Ryan' (renomeia, _ vira espaco), 'cursor' (move o cursor ate a unidade). Multiplos separados por ';'. Ex.: slot0 SD 1,3 name=Ryan cursor")]
    public string spawnCommand;

    [Tooltip("Ajustes executados quando esta fala aparece (uma unica vez). Formatos: 'NOME stat=valor' (hp/fuel/ammo); 'wake 1,3' / 'wake Ryan' (reativa unidade); 'show Bandeira' / 'hide Bandeira' / 'show 5,4' (isVisible de construcao); 'pan Bandeira' / 'pan Ryan' / 'pan 5,4' (desliza SO a camera, cursor intocado); 'cursor Ryan' / 'cursor Ramelle' / 'cursor 3,1' (move apenas o cursor, sem selecionar); 'zoom 1.5' (orthographicSize exato). Multiplos separados por ';'. Ex.: pan Bandeira; cursor Bandeira; zoom 1.5")]
    public string statCommand;

    [FormerlySerializedAs("unlockEndTurn")]
    [Tooltip("No Effect mantem o estado atual. Locked trava o passar a vez a partir desta fala. Unlocked libera novamente ate outra fala mudar o estado.")]
    public TutorialEndTurnEffect turn;

    [Tooltip("Estado do movimento a partir desta fala. No Effect mantem. Locked trava tudo. Hold Only libera manter posicao/atacar parado mas impede sair da celula. Unlocked libera. Se QUALQUER fala usar Unlocked (ou o legado unlockMovement), a cena comeca travada.")]
    public TutorialMovementEffect movement;

    [HideInInspector]
    [Tooltip("LEGADO (prefira movement=Unlocked). Se true, mover/manter posicao e liberado a partir desta fala (ordem de marcha).")]
    public bool unlockMovement;

    [HideInInspector]
    [Tooltip("Se preenchido, revela o objetivo com esta KEY (ex.: hist_1_04) quando esta fala aparece. Tem precedencia sobre revealObjectiveIndex. Se QUALQUER fala do roteiro usar reveal (key ou indice), o manager para de revelar tarefas sozinho e o painel comeca vazio.")]
    public string revealObjectiveKey;

    [HideInInspector]
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

    [SerializeField, HideInInspector] private bool dialogFlowMigrated;

    public void MigrateLegacyDialogFlow()
    {
        if (dialogFlowMigrated || script == null)
            return;

        for (int i = 0; i < script.Count; i++)
        {
            TutorialDialogEntry entry = script[i];
            if (entry == null)
                continue;

            if (!string.IsNullOrWhiteSpace(entry.revealObjectiveKey))
            {
                entry.objectiveKey = entry.revealObjectiveKey;
                entry.revealObjective = true;
            }

            if (i + 1 >= script.Count || script[i + 1] == null)
                continue;

            TutorialDialogEntry next = script[i + 1];
            if (!string.IsNullOrWhiteSpace(next.waitObjectiveKey))
            {
                entry.advance = TutorialAdvanceCondition.ObjectiveCompleted;
                entry.objectiveKey = next.waitObjectiveKey;
            }
            else if (next.waitObjectiveIndex >= 0)
            {
                entry.advance = TutorialAdvanceCondition.ObjectiveCompleted;
                entry.objectiveKey = ResolveLegacyObjectiveKey(next.waitObjectiveIndex);
            }
            else if (next.waitAllUnitsActed)
            {
                entry.advance = TutorialAdvanceCondition.AllUnitsActed;
            }
            else if (next.waitPlayerTurnStart)
            {
                entry.advance = TutorialAdvanceCondition.PlayerTurnStarted;
            }
        }

        dialogFlowMigrated = true;
    }

    private string ResolveLegacyObjectiveKey(int index)
    {
        if (objectives == null || index < 0 || index >= objectives.Count || objectives[index] == null)
            return string.Empty;
        return objectives[index].key;
    }

    private void OnValidate()
    {
        MigrateLegacyDialogFlow();
    }

    [Header("Broncas do Sargento (acoes bloqueadas)")]
    [Tooltip("Bronca ao tentar passar a vez antes da ordem.")]
    public TutorialScoldEntry scoldEndTurnLocked = new TutorialScoldEntry();

    [Tooltip("Bronca ao tentar o Servico do Comando (Reabastecer, X).")]
    public TutorialScoldEntry scoldCommandService = new TutorialScoldEntry();

    [Tooltip("Bronca ao tentar dispensar/destruir unidade (U).")]
    public TutorialScoldEntry scoldRemoveUnit = new TutorialScoldEntry();

    [Tooltip("Bronca ao tentar render-se.")]
    public TutorialScoldEntry scoldSurrender = new TutorialScoldEntry();

    [Tooltip("Bronca ao tentar abrir a Situacao (estatisticas).")]
    public TutorialScoldEntry scoldStatusSummary = new TutorialScoldEntry();

    [Tooltip("Bronca ao tentar mover/manter posicao antes da ordem de marcha (movement=Locked).")]
    public TutorialScoldEntry scoldMovementLocked = new TutorialScoldEntry();

    [Tooltip("Bronca ao tentar SAIR da celula sob ordem de segurar posicao (movement=Hold Only). Manter posicao/atacar parado continua livre.")]
    public TutorialScoldEntry scoldHoldPosition = new TutorialScoldEntry();

    [Tooltip("Bronca ao tentar finalizar parado ('apenas mover'/M) sob ordem de ataque (movement=Attack Only). O caminho liberado e Mirar.")]
    public TutorialScoldEntry scoldAttackOrder = new TutorialScoldEntry();

    public TutorialScoldEntry GetScold(TutorialScoldKind kind)
    {
        switch (kind)
        {
            case TutorialScoldKind.EndTurnLocked: return scoldEndTurnLocked;
            case TutorialScoldKind.CommandService: return scoldCommandService;
            case TutorialScoldKind.RemoveUnit: return scoldRemoveUnit;
            case TutorialScoldKind.Surrender: return scoldSurrender;
            case TutorialScoldKind.StatusSummary: return scoldStatusSummary;
            case TutorialScoldKind.MovementLocked: return scoldMovementLocked;
            case TutorialScoldKind.HoldPosition: return scoldHoldPosition;
            case TutorialScoldKind.AttackOrdered: return scoldAttackOrder;
            default: return null;
        }
    }

    [Header("Figurantes")]
    [Tooltip("Unidades que amanhecem 'ja agiram' em todo turno do jogador (tokens separados por ';'). Ex.: Mathias; Dias — ficam na fila sem nunca poder agir.")]
    public string alwaysActedUnits;

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
