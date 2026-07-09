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
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text speechText;
    [SerializeField] private Button advanceButton;
    [SerializeField] private Button backButton;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    private List<TutorialDialogEntry> script;
    private readonly HashSet<int> completedObjectiveIndices = new HashSet<int>();
    private int currentIndex = -1;
    private int furthestShownIndex = -1;
    private bool waitingGate;
    private bool scriptFinished;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
    }

    private void OnEnable()
    {
        TutorialManager.OnObjectiveCompleted += HandleObjectiveCompleted;
    }

    private void OnDisable()
    {
        TutorialManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
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
        if (entry != null &&
            entry.waitObjectiveIndex >= 0 &&
            !completedObjectiveIndices.Contains(entry.waitObjectiveIndex))
        {
            // Gate pendente: esconde o painel e deixa o jogador cumprir a tarefa.
            waitingGate = true;
            SetPanelVisible(false);
            return;
        }

        ShowEntry(next);
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
            speechText.text = entry != null ? entry.text : string.Empty;

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
            {
                TutorialDialogEntry next = script[currentIndex + 1];
                nextGated = next != null &&
                            next.waitObjectiveIndex >= 0 &&
                            !completedObjectiveIndices.Contains(next.waitObjectiveIndex);
            }

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
        }
        else
        {
            RefreshButtons();
        }
    }

    private int ResolveObjectiveIndex(TutorialObjective objective)
    {
        TutorialData tutorial = matchController != null ? matchController.ActiveTutorial : null;
        if (tutorial == null || tutorial.objectives == null || objective == null)
            return -1;

        return tutorial.objectives.IndexOf(objective);
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
