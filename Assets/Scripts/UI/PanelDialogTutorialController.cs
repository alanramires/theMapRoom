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
    private bool turnStartGateArmed;
    private bool turnStartGateSatisfied;
    private int currentIndex = -1;
    private int furthestShownIndex = -1;
    private bool waitingGate;
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
    }

    private void OnDisable()
    {
        TutorialManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
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

        int next = currentIndex + 1;
        if (next >= script.Count)
            return;

        TutorialDialogEntry entry = script[next];
        if (entry == null || !entry.waitAllUnitsActed)
            return;

        if (!IsEntryGateBlocked(entry))
        {
            waitingGate = false;
            TryAdvanceToNext();
        }
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
        if (scriptFinished || waitingGate)
            return;

        // Evita que o clique/Enter vaze para o cursor de gameplay no mesmo frame.
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        cursorController?.PlayConfirmSfx();

        // Navegando historico: apenas volta para frente sem reprocessar gates.
        if (currentIndex < furthestShownIndex)
        {
            ShowEntry(currentIndex + 1);
            return;
        }

        TryAdvanceToNext();
    }

    public void OnBackClicked()
    {
        if (scriptFinished || waitingGate || currentIndex <= 0)
            return;

        UiInputBlocker.SuppressGameplayInputForFrames(2);
        cursorController?.PlayCancelSfx();
        ShowEntry(currentIndex - 1);
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

        TutorialDialogEntry entry = script[next];
        if (IsEntryGateBlocked(entry))
        {
            // Gate pendente: esconde o painel e deixa o jogador cumprir a condicao.
            // O gate de turno e edge-triggered: arma agora e so um turno NOVO satisfaz.
            if (entry != null && entry.waitPlayerTurnStart && !turnStartGateArmed)
            {
                turnStartGateArmed = true;
                turnStartGateSatisfied = false;
            }

            waitingGate = true;
            SetPanelVisible(false);
            return;
        }

        turnStartGateArmed = false;
        ShowEntry(next);
    }

    // Uma fala pode combinar gates: objetivo (key/indice), todas-unidades-agiram
    // e inicio de novo turno do jogador. Todos precisam estar satisfeitos.
    private bool IsEntryGateBlocked(TutorialDialogEntry entry)
    {
        if (entry == null)
            return false;

        int waitIndex = ResolveWaitIndex(entry);
        if (waitIndex >= 0 && !completedObjectiveIndices.Contains(waitIndex))
            return true;

        if (entry.waitAllUnitsActed && CountPlayerUnitsToAct() > 0)
            return true;

        if (entry.waitPlayerTurnStart && !turnStartGateSatisfied)
            return true;

        return false;
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
        if (matchController == null || teamId != (int)matchController.GetTeamIdForSlot(0))
            return;

        turnStartGateSatisfied = true;
        if (waitingGate)
        {
            waitingGate = false;
            TryAdvanceToNext();
            return;
        }

        // Auto-avanco com o painel visivel na fronteira: o turno novo comecou e a
        // proxima fala esperava exatamente isso.
        if (script != null && !scriptFinished &&
            currentIndex >= furthestShownIndex && currentIndex + 1 < script.Count)
        {
            TutorialDialogEntry next = script[currentIndex + 1];
            if (next != null && next.waitPlayerTurnStart && !IsEntryGateBlocked(next))
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

        SetPanelVisible(true);

        TutorialDialogEntry entry = script[index];
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

        // O Sargento autorizou: libera o passar a vez (uma vez liberado, nao trava de novo).
        if (entry != null && entry.unlockEndTurn)
            TutorialManager.UnlockEndTurnFromScript();

        // Ordem de marcha: libera mover/manter posicao.
        if (entry != null && entry.unlockMovement)
            TutorialManager.UnlockMovementFromScript();

        // O Sargento deu a ordem: a tarefa entra na task list (idempotente).
        int revealIndex = ResolveRevealIndex(entry);
        if (revealIndex >= 0)
            TutorialManager.RevealObjectiveFromScript(revealIndex);

        if (voiceSource != null)
        {
            voiceSource.Stop();
            if (entry != null && entry.voice != null)
                voiceSource.PlayOneShot(entry.voice);
        }

        RefreshButtons();
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
            bool nextGated = false;
            if (atFrontier && script != null && currentIndex + 1 < script.Count)
                nextGated = IsEntryGateBlocked(script[currentIndex + 1]);

            advanceButton.interactable = !nextGated;
        }
    }

    private void HandleObjectiveCompleted(TutorialObjective objective)
    {
        int index = ResolveObjectiveIndex(objective);
        if (index >= 0)
            completedObjectiveIndices.Add(index);

        if (waitingGate)
        {
            waitingGate = false;
            TryAdvanceToNext();
            return;
        }

        // Avanco automatico: o jogador cumpriu a ordem da fala atual sem clicar
        // Avancar (ex.: deu zoom enquanto o Sargento ainda estava na tela). Se a
        // PROXIMA fala esperava exatamente este objetivo, segue sozinho.
        if (currentIndex >= furthestShownIndex && script != null && currentIndex + 1 < script.Count)
        {
            TutorialDialogEntry next = script[currentIndex + 1];
            if (next != null && index >= 0 && ResolveWaitIndex(next) == index && !IsEntryGateBlocked(next))
            {
                TryAdvanceToNext();
                return;
            }
        }

        RefreshButtons();
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
        if (scriptFinished || waitingGate)
        {
            SetPanelVisible(false);
            return;
        }

        if (script != null && currentIndex >= 0 && currentIndex < script.Count && speechText != null)
        {
            TutorialDialogEntry entry = script[currentIndex];
            speechText.text = FormatSpeechText(entry != null ? entry.text : string.Empty);
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
