using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Painel de roteiro do tutorial (panel_dialog_tutorial): retrato do Sargento,
/// area de texto e botoes avancar/voltar, ancorado center-bottom.
/// Le TutorialData.script do tutorial ativo. Falas com waitObjectiveIndex >= 0
/// escondem o painel ate o objetivo completar (gate); ao completar, o painel
/// reaparece sozinho na fala seguinte. Voltar navega o historico ja exibido.
/// </summary>
public class PanelDialogTutorialController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text speechText;
    [SerializeField] private Button advanceButton;
    [SerializeField] private Button backButton;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Bronca")]
    [Tooltip("Retrato do Sargento (Image). Auto-resolve por nome 'image_retrato' se vazio.")]
    [SerializeField] private Image portraitImage;
    [Tooltip("Sprite de expressao de bronca (opcional). Troca o retrato durante a mensagem de acao bloqueada e volta ao normal depois.")]
    [SerializeField] private Sprite scoldPortraitSprite;

    private static PanelDialogTutorialController activeInstance;

    private List<TutorialDialogEntry> script;
    private readonly HashSet<int> completedObjectiveIndices = new HashSet<int>();
    private readonly HashSet<int> executedSpawnEntries = new HashSet<int>();
    private readonly HashSet<int> executedStatEntries = new HashSet<int>();
    private Coroutine scoldRoutine;
    private Sprite normalPortraitSprite;
    // Gate de "novo turno": conta os inicios de turno (jogador e inimigo em separado)
    // e carimba o valor quando cada fala da fronteira aparece — o gate so satisfaz
    // com turno INICIADO DEPOIS da fala, imune a eventos antigos/da carga da cena.
    private int playerTurnStartCount;
    private int turnStartCountAtFrontierShow;
    private int enemyTurnStartCount;
    private int enemyTurnCountAtFrontierShow;
    // Gate "abriu o Mirar": rearmado sempre que uma fala com advance=AimOpened
    // chega na fronteira — so satisfaz com um Mirar DEPOIS da fala aparecer.
    private bool aimOpenedAtFrontier;
    private int currentIndex = -1;
    private int furthestShownIndex = -1;
    private bool scriptFinished;

    private void Awake()
    {
        activeInstance = this;
        ResolveReferences();
        BindButtons();
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    private void OnEnable()
    {
        TutorialManager.OnObjectiveCompleted += HandleObjectiveCompleted;
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        TurnStateManager.OnUnitAimOpened += HandleAimOpened;
    }

    private void OnDisable()
    {
        TutorialManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        TurnStateManager.OnUnitAimOpened -= HandleAimOpened;
    }

    private void Update()
    {
        // Gate por estado (nao ha evento): "todas as unidades do jogador agiram".
        // Roda tanto com o painel escondido no gate quanto visivel na fronteira —
        // o jogador pode cumprir a ordem sem nunca ter clicado Avancar.
        if (script == null || scriptFinished)
            return;
        if (currentIndex < furthestShownIndex)
            return;

        if (currentIndex < 0 || currentIndex >= script.Count)
            return;

        TutorialDialogEntry entry = script[currentIndex];
        if (entry == null || entry.advance != TutorialAdvanceCondition.AllUnitsActed)
            return;

        if (!IsAdvanceBlocked(entry))
            TryAdvanceToNext();
    }

    private void Start()
    {
        ResolveReferences();

        TutorialData tutorial = matchController != null ? matchController.ActiveTutorial : null;
        if (tutorial == null || tutorial.script == null || tutorial.script.Count <= 0)
        {
            SetPanelVisible(false);
            scriptFinished = true;
            return;
        }

        script = tutorial.script;
        tutorial.MigrateLegacyDialogFlow();
        TryAdvanceToNext();
    }

    private void ResolveReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (tutorialManager == null)
            tutorialManager = FindAnyObjectByType<TutorialManager>();
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();

        if (panelRoot == null)
        {
            Transform t = FindTransformByName("Panel_Dialog_Tutorial");
            if (t != null)
                panelRoot = t.gameObject;
            else
                panelRoot = gameObject;
        }

        if (speechText == null)
            speechText = FindComponentByName<TMP_Text>("text_fala");
        if (advanceButton == null)
            advanceButton = FindComponentByName<Button>("button_avancar");
        if (backButton == null)
            backButton = FindComponentByName<Button>("button_voltar");
        if (portraitImage == null)
            portraitImage = FindComponentByName<Image>("image_retrato");
        if (voiceSource == null && panelRoot != null)
            voiceSource = panelRoot.GetComponent<AudioSource>();

        if (panelCanvasGroup == null && panelRoot != null)
        {
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }
    }

    private void BindButtons()
    {
        if (advanceButton != null)
        {
            advanceButton.onClick.RemoveListener(OnAdvanceClicked);
            advanceButton.onClick.AddListener(OnAdvanceClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackClicked);
            backButton.onClick.AddListener(OnBackClicked);
        }
    }

    public void OnAdvanceClicked()
    {
        if (scriptFinished)
            return;

        // Evita que o clique/Enter vaze para o cursor de gameplay no mesmo frame.
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        cursorController?.PlayConfirmSfx();

        // Navegando historico: apenas volta para frente sem reprocessar gates.
        // Falas mudas nao entram no historico visivel — pula, sem passar da fronteira.
        if (currentIndex < furthestShownIndex)
        {
            int target = FindNonMuteIndex(currentIndex + 1, +1);
            if (target < 0 || target > furthestShownIndex)
                target = furthestShownIndex;
            ShowEntry(target);
            return;
        }

        if (currentIndex >= 0 && currentIndex < script.Count && IsAdvanceBlocked(script[currentIndex]))
            return;

        TryAdvanceToNext();
    }

    public void OnBackClicked()
    {
        if (scriptFinished || currentIndex <= 0)
            return;

        UiInputBlocker.SuppressGameplayInputForFrames(2);
        cursorController?.PlayCancelSfx();

        int previous = FindNonMuteIndex(currentIndex - 1, -1);
        if (previous >= 0)
            ShowEntry(previous);
    }

    // Procura a partir de start, na direcao dada, a primeira fala com texto.
    // Retorna -1 se so houver falas mudas no caminho.
    private int FindNonMuteIndex(int start, int direction)
    {
        if (script == null)
            return -1;

        for (int i = start; i >= 0 && i < script.Count; i += direction)
        {
            TutorialDialogEntry entry = script[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.text))
                return i;
        }

        return -1;
    }

    private void TryAdvanceToNext()
    {
        if (script == null)
            return;

        int next = currentIndex + 1;
        if (next >= script.Count)
        {
            // Roteiro concluido: painel sai de cena.
            scriptFinished = true;
            SetPanelVisible(false);
            return;
        }

        ShowEntry(next);
    }

    // Uma fala pode combinar gates: objetivo (key/indice), todas-unidades-agiram
    // e inicio de novo turno do jogador. Todos precisam estar satisfeitos.
    private bool IsAdvanceBlocked(TutorialDialogEntry entry)
    {
        if (entry == null)
            return false;

        switch (entry.advance)
        {
            case TutorialAdvanceCondition.ObjectiveCompleted:
                int objectiveIndex = ResolveObjectiveKey(entry.objectiveKey);
                return objectiveIndex < 0 || !completedObjectiveIndices.Contains(objectiveIndex);
            case TutorialAdvanceCondition.AllUnitsActed:
                return CountPlayerUnitsToAct() > 0;
            case TutorialAdvanceCondition.PlayerTurnStarted:
                return playerTurnStartCount <= turnStartCountAtFrontierShow;
            case TutorialAdvanceCondition.EnemyTurnStarted:
                return enemyTurnStartCount <= enemyTurnCountAtFrontierShow;
            case TutorialAdvanceCondition.AimOpened:
                return !aimOpenedAtFrontier;
            default:
                return false;
        }
    }

    private int CountPlayerUnitsToAct()
    {
        if (matchController == null)
            return 0;

        TeamId playerTeam = matchController.GetTeamIdForSlot(0);
        int count = 0;
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (unit.TeamId != playerTeam || unit.HasActed)
                continue;
            count++;
        }

        return count;
    }

    private void HandleActiveTeamChanged(int teamId)
    {
        if (matchController == null)
            return;

        bool playerTurn = teamId == (int)matchController.GetTeamIdForSlot(0);
        TutorialAdvanceCondition satisfied;
        if (playerTurn)
        {
            playerTurnStartCount++;
            satisfied = TutorialAdvanceCondition.PlayerTurnStarted;
        }
        else
        {
            enemyTurnStartCount++;
            satisfied = TutorialAdvanceCondition.EnemyTurnStarted;
        }

        // Auto-avanco na fronteira: o turno novo comecou e a fala atual esperava
        // exatamente isso (inclui falas mudas seguradas em gate de turno).
        if (script != null && !scriptFinished &&
            currentIndex >= furthestShownIndex && currentIndex >= 0 && currentIndex < script.Count)
        {
            TutorialDialogEntry current = script[currentIndex];
            if (current != null && current.advance == satisfied && !IsAdvanceBlocked(current))
                TryAdvanceToNext();
        }
    }

    private void ShowEntry(int index)
    {
        if (script == null || index < 0 || index >= script.Count)
            return;

        currentIndex = index;
        if (index > furthestShownIndex)
            furthestShownIndex = index;

        TutorialDialogEntry entry = script[index];

        // Fala muda (texto vazio): direcao de cena — executa os comandos sem abrir
        // o balao (ex.: spawn do inimigo + pan durante o turno da IA).
        bool mute = entry == null || string.IsNullOrWhiteSpace(entry.text);
        SetPanelVisible(!mute);

        if (index >= furthestShownIndex && entry != null)
        {
            if (entry.advance == TutorialAdvanceCondition.PlayerTurnStarted)
                turnStartCountAtFrontierShow = playerTurnStartCount;
            else if (entry.advance == TutorialAdvanceCondition.EnemyTurnStarted)
                enemyTurnCountAtFrontierShow = enemyTurnStartCount;
            else if (entry.advance == TutorialAdvanceCondition.AimOpened)
                aimOpenedAtFrontier = false;
        }
        if (speechText != null)
            speechText.text = FormatSpeechText(entry != null ? entry.text : string.Empty);

        // Spawns amarrados a fala executam uma unica vez (voltar no historico nao duplica).
        if (entry != null && !string.IsNullOrWhiteSpace(entry.spawnCommand) && !executedSpawnEntries.Contains(index))
        {
            executedSpawnEntries.Add(index);
            if (tutorialManager == null)
                tutorialManager = FindAnyObjectByType<TutorialManager>();
            tutorialManager?.ExecuteSpawnCommands(entry.spawnCommand);
        }

        // Ajustes de status amarrados a fala (demonstracoes do Sargento) — uma unica vez.
        if (entry != null && !string.IsNullOrWhiteSpace(entry.statCommand) && !executedStatEntries.Contains(index))
        {
            executedStatEntries.Add(index);
            if (tutorialManager == null)
                tutorialManager = FindAnyObjectByType<TutorialManager>();
            tutorialManager?.ExecuteStatCommands(entry.statCommand);
        }

        // Muda o estado persistente do passar a vez quando a fala pede isso.
        if (entry != null)
            TutorialManager.ApplyEndTurnEffectFromScript(entry.turn);

        // Estado do movimento (Locked / Hold Only / Unlocked) definido pela fala.
        if (entry != null)
            TutorialManager.ApplyMovementEffectFromScript(entry.movement);

        // LEGADO: ordem de marcha via bool antigo.
        if (entry != null && entry.unlockMovement)
            TutorialManager.UnlockMovementFromScript();

        // O Sargento deu a ordem: a tarefa entra na task list (idempotente).
        int revealIndex = entry != null && entry.revealObjective
            ? ResolveObjectiveKey(entry.objectiveKey)
            : -1;
        if (revealIndex >= 0)
            TutorialManager.RevealObjectiveFromScript(revealIndex);

        if (voiceSource != null)
        {
            voiceSource.Stop();
            if (entry != null && entry.voice != null)
                voiceSource.PlayOneShot(entry.voice);
        }

        RefreshButtons();

        // Fala muda sem gate proprio: executa e segue direto para a proxima.
        if (mute && entry != null && entry.advance == TutorialAdvanceCondition.Immediate)
            TryAdvanceToNext();
    }

    private void RefreshButtons()
    {
        if (backButton != null)
            backButton.interactable = currentIndex > 0;

        if (advanceButton != null)
        {
            // No fim do historico com gate pendente na proxima fala, o avancar
            // fica desabilitado — o resto do fluxo esconde o painel de qualquer forma.
            bool atFrontier = currentIndex >= furthestShownIndex;
            bool blocked = atFrontier && script != null && currentIndex >= 0 &&
                currentIndex < script.Count && IsAdvanceBlocked(script[currentIndex]);

            advanceButton.interactable = !blocked;
        }
    }

    private void HandleObjectiveCompleted(TutorialObjective objective)
    {
        int index = ResolveObjectiveIndex(objective);
        if (index >= 0)
            completedObjectiveIndices.Add(index);

        // Avanco automatico: o jogador cumpriu a ordem da fala atual sem clicar
        // Avancar (ex.: deu zoom enquanto o Sargento ainda estava na tela). Se a
        // PROXIMA fala esperava exatamente este objetivo, segue sozinho.
        if (currentIndex >= furthestShownIndex && script != null && currentIndex >= 0 && currentIndex < script.Count)
        {
            TutorialDialogEntry current = script[currentIndex];
            if (current != null && current.advance == TutorialAdvanceCondition.ObjectiveCompleted &&
                index >= 0 && ResolveObjectiveKey(current.objectiveKey) == index && !IsAdvanceBlocked(current))
            {
                TryAdvanceToNext();
                return;
            }
        }

        RefreshButtons();
    }

    // Jogador abriu o Mirar: satisfaz o gate AimOpened e avanca sozinho se a fala
    // da fronteira esperava exatamente isso (mesmo padrao do objetivo completado).
    private void HandleAimOpened(UnitManager unit)
    {
        if (matchController == null ||
            matchController.ActiveTeamId != (int)matchController.GetTeamIdForSlot(0))
            return;

        aimOpenedAtFrontier = true;

        if (script != null && !scriptFinished &&
            currentIndex >= furthestShownIndex && currentIndex >= 0 && currentIndex < script.Count)
        {
            TutorialDialogEntry current = script[currentIndex];
            if (current != null && current.advance == TutorialAdvanceCondition.AimOpened && !IsAdvanceBlocked(current))
                TryAdvanceToNext();
        }
    }

    private int ResolveObjectiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return -1;

        int index = ResolveKeyToIndex(key);
        if (index < 0)
            Debug.LogError($"[PanelDialogTutorial] objectiveKey '{key}' nao existe nos objectives do tutorial ativo.");
        return index;
    }

    // Key (hist_Y_XX) tem precedencia sobre o indice legado. Key inexistente e erro
    // barulhento no Console — falha silenciosa em gate ja nos custou caro.
    private int ResolveWaitIndex(TutorialDialogEntry entry)
    {
        if (entry == null)
            return -1;

        if (!string.IsNullOrWhiteSpace(entry.waitObjectiveKey))
        {
            int index = ResolveKeyToIndex(entry.waitObjectiveKey);
            if (index < 0)
                Debug.LogError($"[PanelDialogTutorial] waitObjectiveKey '{entry.waitObjectiveKey}' nao existe nos objectives do tutorial ativo.");
            return index;
        }

        return entry.waitObjectiveIndex;
    }

    private int ResolveRevealIndex(TutorialDialogEntry entry)
    {
        if (entry == null)
            return -1;

        if (!string.IsNullOrWhiteSpace(entry.revealObjectiveKey))
        {
            int index = ResolveKeyToIndex(entry.revealObjectiveKey);
            if (index < 0)
                Debug.LogError($"[PanelDialogTutorial] revealObjectiveKey '{entry.revealObjectiveKey}' nao existe nos objectives do tutorial ativo.");
            return index;
        }

        return entry.revealObjectiveIndex;
    }

    private int ResolveKeyToIndex(string key)
    {
        TutorialData tutorial = matchController != null ? matchController.ActiveTutorial : null;
        return tutorial != null ? tutorial.FindObjectiveIndexByKey(key) : -1;
    }

    private int ResolveObjectiveIndex(TutorialObjective objective)
    {
        TutorialData tutorial = matchController != null ? matchController.ActiveTutorial : null;
        if (tutorial == null || tutorial.objectives == null || objective == null)
            return -1;

        return tutorial.objectives.IndexOf(objective);
    }

    // ── Bronca do Sargento (mensagem de acao bloqueada) ─────────────────────
    // O panel_dialog fica atras deste painel, entao mensagens de bloqueio no
    // tutorial viram fala transiente do proprio Sargento: o balao troca o texto
    // (aparecendo, se estava escondido num gate), segura alguns segundos e restaura.

    public static void ShowBlockedActionMessage(string text, AudioClip voice = null)
    {
        if (activeInstance != null && activeInstance.TryShowScold(text, voice))
            return;

        // Sem painel de tutorial na cena: cai no panel_dialog normal.
        PanelDialogController.TrySetTransientText(text, 2.6f);
    }

    private bool TryShowScold(string text, AudioClip voice)
    {
        if (script == null || speechText == null || string.IsNullOrWhiteSpace(text))
            return false;

        if (scoldRoutine != null)
            StopCoroutine(scoldRoutine);

        // Com voz gravada, o balao segura ate a fala terminar.
        float duration = 2.6f;
        if (voice != null)
            duration = Mathf.Max(duration, voice.length + 0.4f);

        scoldRoutine = StartCoroutine(RunScold(text, voice, duration));
        return true;
    }

    private IEnumerator RunScold(string text, AudioClip voice, float duration)
    {
        SetPanelVisible(true);
        speechText.text = FormatSpeechText(text);
        ApplyScoldPortrait(true);

        if (voiceSource != null)
        {
            voiceSource.Stop();
            if (voice != null)
                voiceSource.PlayOneShot(voice);
        }

        yield return new WaitForSeconds(duration);
        scoldRoutine = null;
        RestoreAfterScold();
    }

    private void RestoreAfterScold()
    {
        ApplyScoldPortrait(false);

        // Volta ao estado que o roteiro pedia antes da bronca.
        if (scriptFinished)
        {
            SetPanelVisible(false);
            return;
        }

        if (script != null && currentIndex >= 0 && currentIndex < script.Count && speechText != null)
        {
            TutorialDialogEntry entry = script[currentIndex];
            speechText.text = FormatSpeechText(entry != null ? entry.text : string.Empty);

            // Fala muda em espera: a bronca abriu o painel; volta a esconder.
            if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                SetPanelVisible(false);
        }
    }

    // Markup leve do roteiro, sem exigir tags do TMP dentro do asset:
    // [ordem]...[/ordem]     = o que FAZER (amarelo + negrito)
    // [enfase]...[/enfase]   = o que FIXAR (laranja, conceito que o Sargento realca)
    // Cores puras (realce visual de elementos da UI, sem negrito):
    // [azul], [amarelo], [vermelho]
    private static string FormatSpeechText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("[ordem]", "<color=#FFD84D><b>")
            .Replace("[/ordem]", "</b></color>")
            .Replace("[enfase]", "<color=#FF8C3B>")
            .Replace("[/enfase]", "</color>")
            .Replace("[azul]", "<color=#5CB3FF>")
            .Replace("[/azul]", "</color>")
            .Replace("[amarelo]", "<color=#FFD84D>")
            .Replace("[/amarelo]", "</color>")
            .Replace("[vermelho]", "<color=#FF5C5C>")
            .Replace("[/vermelho]", "</color>");
    }

    private void ApplyScoldPortrait(bool scolding)
    {
        if (portraitImage == null || scoldPortraitSprite == null)
            return;

        if (scolding)
        {
            if (normalPortraitSprite == null)
                normalPortraitSprite = portraitImage.sprite;
            portraitImage.sprite = scoldPortraitSprite;
        }
        else if (normalPortraitSprite != null)
        {
            portraitImage.sprite = normalPortraitSprite;
        }
    }

    private void SetPanelVisible(bool visible)
    {
        // CanvasGroup em vez de SetActive: se este componente estiver no proprio
        // painel, desativar o GameObject mataria a escuta de OnObjectiveCompleted
        // e o gate nunca abriria.
        if (panelCanvasGroup == null)
            ResolveReferences();

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }
        else if (panelRoot != null && panelRoot != gameObject && panelRoot.activeSelf != visible)
        {
            panelRoot.SetActive(visible);
        }
    }

    private T FindComponentByName<T>(string name) where T : Component
    {
        Transform root = panelRoot != null ? panelRoot.transform : transform;
        Transform found = FindInChildrenRecursive(root, name);
        return found != null ? found.GetComponent<T>() : null;
    }

    private static Transform FindTransformByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindInChildrenRecursive(roots[i].transform, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindInChildrenRecursive(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindInChildrenRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
