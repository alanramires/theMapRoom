using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-290)]
public class MainMenuTutorialPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelMenuRoot;
    [SerializeField] private GameObject panelTutorialRoot;
    [SerializeField] private PanelMenu panelMenu;
    [SerializeField] private MainMenuStateController stateController;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private Button backButton;

    [Header("Historias")]
    [SerializeField] private Button storyButton1;
    [SerializeField] private Button storyButton2;
    [SerializeField] private Button storyButton3;
    [SerializeField] private Button storyButton4;
    [SerializeField] private Button storyButton5;

    [Tooltip("Nome exato da cena de cada historia (mesmo indice dos botoes). Vazio desabilita o botao.")]
    [SerializeField] private string[] storySceneNames =
    {
        "História 1 - Aprendendo a Atirar",
        "História 2 - A Arma certa",
        "História 3 - Resgate Off Road",
        "História 4 - Sem Combustivel",
        "História 5 - Defenda a Ponte",
    };

    [Tooltip("Pergunta a cor do jogador (ESCOLHA SUA COR) antes de carregar a historia. So tem efeito em cenas com unidades ligadas por slotIndex.")]
    [SerializeField] private bool askPlayerColor = true;

    private static readonly TeamId[] TutorialTeams = { TeamId.Green, TeamId.Red, TeamId.Blue, TeamId.Yellow };

    private int selectedIndex;
    private bool storyButtonsBound;
    private bool isHidden = true;
    private bool loadTransitionInProgress;
    private int panelOpenedFrame = -1;
    private int lastOpenFrame = -1;
    private int lastCloseFrame = -1;
    private bool colorStepOpen;
    private int colorStepFocusIndex;
    private int pendingStoryIndex = -1;

    public bool IsOpen => !isHidden && panelTutorialRoot != null && panelTutorialRoot.activeInHierarchy;
    public bool IsColorStepOpen => colorStepOpen;
    public int ColorStepFocusIndex => colorStepFocusIndex;

    private void Awake()
    {
        ResolveReferences();
        BindBackButtonIfNeeded();
        BindStoryButtonsIfNeeded();
        RefreshStoryButtonsInteractable();
        SetTutorialPanelHidden(true);
    }

    private void OnEnable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = false;

        ResolveReferences();
        RefreshStoryButtonsInteractable();
    }

    private void OnDisable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
    }

    private void Update()
    {
        // Com MainMenuStateController presente, o roteamento de teclado e feito por ele
        // (RouteTutorialMenuInput). Este Update e apenas o fallback standalone.
        if (stateController != null)
            return;

        if (!IsOpen || loadTransitionInProgress)
            return;

        UiInputBlocker.SuppressGameplayInputForFrames(1);

        if (IsAnyTextInputFocusedInUi())
            return;

        if (WasUpPressed() || WasLeftPressed())
        {
            Navigate(-1);
            return;
        }

        if (WasDownPressed() || WasRightPressed())
        {
            Navigate(+1);
            return;
        }

        if (WasConfirmPressed())
        {
            UiInputBlocker.SuppressGameplayInputForFrames(2);
            ConfirmCurrentSelection();
            return;
        }

        if (WasCancelPressed())
            CloseTutorialPanel();
    }

    public void OpenTutorialPanel()
    {
        EnterTutorialMenu();
    }

    public void EnterTutorialMenu()
    {
        if (Time.frameCount == lastOpenFrame)
            return;
        lastOpenFrame = Time.frameCount;

        ResolveReferences();
        panelMenu?.CloseNewGameWizardIfOpen();
        RefreshStoryButtonsInteractable();
        SetTutorialPanelHidden(false);

        panelOpenedFrame = Time.frameCount;

        selectedIndex = FindFirstInteractableButtonIndex();
        SelectCurrentButton(playSfx: false);
        cursorController?.PlayConfirmSfx();
    }

    public void CloseTutorialPanel()
    {
        if (Time.frameCount == lastCloseFrame)
            return;
        lastCloseFrame = Time.frameCount;

        if (!IsOpen)
            return;

        // Esc/Voltar com o passo de cor aberto so fecha o passo de cor.
        if (colorStepOpen)
        {
            CloseColorStep(playCancelSfx: true);
            return;
        }

        if (stateController != null && stateController.CurrentState == MainMenuState.Tutorial)
        {
            stateController.RequestState(MainMenuState.RootMenu);
            cursorController?.PlayCancelSfx();
            return;
        }

        // Evita que o Enter usado no "Voltar" vaze para o menu raiz no mesmo frame.
        UiInputBlocker.SuppressGameplayInputForFrames(4);
        SetTutorialPanelHidden(true);
        SetMenuHidden(false);
        panelMenu?.ShowRootMenu();
        cursorController?.PlayCancelSfx();
    }

    public void ExitTutorialMenu(bool stateDriven = false)
    {
        CloseColorStep(playCancelSfx: false);
        SetTutorialPanelHidden(true);
    }

    public bool Navigate(int direction)
    {
        if (colorStepOpen)
        {
            if (direction == 0)
                return false;
            int count = GetColorStepOptionCount();
            colorStepFocusIndex = (colorStepFocusIndex + (direction > 0 ? 1 : -1) + count) % count;
            cursorController?.PlayCursorMoveSfx();
            return true;
        }

        return MoveSelection(direction, playSfx: true);
    }

    public void ConfirmCurrentSelection()
    {
        if (colorStepOpen)
        {
            InvokeColorStepOption(colorStepFocusIndex);
            return;
        }

        ConfirmSelection();
    }

    // ── Passo de cor (ESCOLHA SUA COR, renderizado no panel_helper) ────────

    public int GetColorStepOptionCount() => TutorialTeams.Length + 1;

    public string GetColorStepOptionLabel(int index)
    {
        if (index >= 0 && index < TutorialTeams.Length)
            return ResolveTeamLabel(TutorialTeams[index]);
        return "VOLTAR";
    }

    public bool TryGetColorStepOptionColor(int index, out Color color)
    {
        color = Color.white;
        if (index < 0 || index >= TutorialTeams.Length)
            return false;
        color = TeamUtils.GetColor(TutorialTeams[index]);
        return true;
    }

    public void InvokeColorStepOption(int index)
    {
        if (!colorStepOpen || index < 0 || index >= GetColorStepOptionCount())
            return;

        colorStepFocusIndex = index;
        if (index >= TutorialTeams.Length)
        {
            CloseColorStep(playCancelSfx: true);
            return;
        }

        TryLoadStory(pendingStoryIndex, TutorialTeams[index]);
    }

    private void OpenColorStep(int storyIndex)
    {
        pendingStoryIndex = storyIndex;
        colorStepOpen = true;
        colorStepFocusIndex = 0;
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        string storyLabel = GetStorySceneName(storyIndex);
        PanelHelperController.TrySetExternalText("ESCOLHA SUA COR", storyLabel ?? string.Empty);
        cursorController?.PlayConfirmSfx();
    }

    private void CloseColorStep(bool playCancelSfx)
    {
        if (!colorStepOpen)
            return;

        colorStepOpen = false;
        colorStepFocusIndex = 0;
        pendingStoryIndex = -1;
        PanelHelperController.ClearExternalText();
        if (playCancelSfx)
            cursorController?.PlayCancelSfx();
    }

    private void OnStoryButtonClicked(int index)
    {
        // Protecao contra listener indevido: o clique que abriu o painel nao pode
        // disparar a historia no mesmo frame.
        if (Time.frameCount == panelOpenedFrame)
            return;

        if (!IsOpen || loadTransitionInProgress || colorStepOpen)
            return;

        Button expectedButton = GetStoryButton(index);
        if (expectedButton == null || !expectedButton.gameObject.activeInHierarchy)
            return;

        string sceneName = GetStorySceneName(index);
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            cursorController?.PlayErrorSfx();
            Debug.LogWarning($"[MainMenuTutorial] Cena '{sceneName}' nao encontrada no build settings.");
            return;
        }

        if (askPlayerColor)
        {
            OpenColorStep(index);
            return;
        }

        TryLoadStory(index, TeamId.Neutral);
    }

    private void TryLoadStory(int index, TeamId playerTeam)
    {
        if (!IsOpen || loadTransitionInProgress)
            return;

        string sceneName = GetStorySceneName(index);
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            cursorController?.PlayErrorSfx();
            Debug.LogWarning($"[MainMenuTutorial] Cena '{sceneName}' nao encontrada no build settings.");
            return;
        }

        loadTransitionInProgress = true;
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        cursorController?.PlayConfirmSfx();
        PanelHelperController.ClearExternalText();

        // Mesma higiene do NewGame: reseta os IDs do spawner sem mexer no diretorio de save.
        SaveGameManager.SetupForNewGame(string.Empty);
        if (playerTeam != TeamId.Neutral)
            PartidaConfig.SetTutorialPlayerTeam(playerTeam);
        SceneManager.LoadScene(sceneName);
    }

    private static string ResolveTeamLabel(TeamId team) => team switch
    {
        TeamId.Green => "VERDE", TeamId.Red => "VERMELHO", TeamId.Blue => "AZUL", TeamId.Yellow => "AMARELO", _ => team.ToString().ToUpperInvariant()
    };

    private void ResolveReferences()
    {
        if (panelMenuRoot == null)
        {
            Transform menu = FindTransformByName("Panel_Menu");
            if (menu != null)
                panelMenuRoot = menu.gameObject;
        }

        if (panelTutorialRoot == null)
        {
            Transform tutorial = FindTransformByName("Panel_Tutorial");
            if (tutorial != null)
                panelTutorialRoot = tutorial.gameObject;
            else
                panelTutorialRoot = gameObject;
        }

        if (panelMenu == null)
            panelMenu = FindAnyObjectByType<PanelMenu>();
        if (stateController == null)
            stateController = MainMenuStateController.EnsureSceneInstance();
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();

        if (storyButton1 == null) storyButton1 = FindButtonByName("button_historia1");
        if (storyButton2 == null) storyButton2 = FindButtonByName("button_historia2");
        if (storyButton3 == null) storyButton3 = FindButtonByName("button_historia3");
        if (storyButton4 == null) storyButton4 = FindButtonByName("button_historia4");
        if (storyButton5 == null) storyButton5 = FindButtonByName("button_historia5");
        if (backButton == null && panelTutorialRoot != null)
        {
            Transform back = FindInChildrenRecursive(panelTutorialRoot.transform, "button_voltar");
            if (back != null)
                backButton = back.GetComponent<Button>();
        }
    }

    private void BindBackButtonIfNeeded()
    {
        if (backButton == null)
            return;

        backButton.onClick.RemoveListener(CloseTutorialPanel);
        backButton.onClick.AddListener(CloseTutorialPanel);
    }

    private void BindStoryButtonsIfNeeded()
    {
        if (storyButtonsBound)
            return;

        if (storyButton1 != null) storyButton1.onClick.AddListener(() => OnStoryButtonClicked(1));
        if (storyButton2 != null) storyButton2.onClick.AddListener(() => OnStoryButtonClicked(2));
        if (storyButton3 != null) storyButton3.onClick.AddListener(() => OnStoryButtonClicked(3));
        if (storyButton4 != null) storyButton4.onClick.AddListener(() => OnStoryButtonClicked(4));
        if (storyButton5 != null) storyButton5.onClick.AddListener(() => OnStoryButtonClicked(5));

        storyButtonsBound = true;
    }

    private void RefreshStoryButtonsInteractable()
    {
        for (int i = 1; i <= 5; i++)
        {
            Button button = GetStoryButton(i);
            if (button == null)
                continue;

            string sceneName = GetStorySceneName(i);
            button.interactable = !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
        }
    }

    private Button GetStoryButton(int index)
    {
        return index switch
        {
            1 => storyButton1,
            2 => storyButton2,
            3 => storyButton3,
            4 => storyButton4,
            5 => storyButton5,
            _ => null
        };
    }

    private string GetStorySceneName(int index)
    {
        if (storySceneNames == null || index < 1 || index > storySceneNames.Length)
            return null;
        return storySceneNames[index - 1];
    }

    private bool MoveSelection(int delta, bool playSfx)
    {
        var buttons = GetAvailableButtons();
        if (buttons.Count <= 0 || delta == 0)
            return false;

        int count = buttons.Count;
        int direction = delta > 0 ? 1 : -1;
        int startIndex = Mathf.Clamp(selectedIndex, 0, count - 1);
        int nextIndex = startIndex;

        for (int i = 0; i < count; i++)
        {
            nextIndex += direction;
            while (nextIndex < 0)
                nextIndex += count;
            while (nextIndex >= count)
                nextIndex -= count;

            Button candidate = buttons[nextIndex];
            if (candidate != null && candidate.interactable)
            {
                selectedIndex = nextIndex;
                SelectCurrentButton(playSfx);
                return true;
            }
        }

        return false;
    }

    private void SelectCurrentButton(bool playSfx)
    {
        var buttons = GetAvailableButtons();
        if (buttons.Count <= 0)
            return;

        Button b = buttons[Mathf.Clamp(selectedIndex, 0, buttons.Count - 1)];
        if (b == null)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(b.gameObject);

        if (playSfx)
            cursorController?.PlayCursorMoveSfx();
    }

    private void ConfirmSelection()
    {
        var buttons = GetAvailableButtons();
        if (buttons.Count <= 0)
            return;

        Button b = buttons[Mathf.Clamp(selectedIndex, 0, buttons.Count - 1)];
        if (b == null || !b.interactable)
            return;

        b.onClick?.Invoke();
    }

    private int FindFirstInteractableButtonIndex()
    {
        var buttons = GetAvailableButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            Button b = buttons[i];
            if (b != null && b.interactable)
                return i;
        }

        return 0;
    }

    private List<Button> GetAvailableButtons()
    {
        List<Button> list = new List<Button>(6);
        if (storyButton1 != null) list.Add(storyButton1);
        if (storyButton2 != null) list.Add(storyButton2);
        if (storyButton3 != null) list.Add(storyButton3);
        if (storyButton4 != null) list.Add(storyButton4);
        if (storyButton5 != null) list.Add(storyButton5);
        if (backButton != null) list.Add(backButton);
        return list;
    }

    private void SetMenuHidden(bool hidden)
    {
        if (panelMenuRoot == null)
            return;

        panelMenuRoot.SetActive(!hidden);
    }

    private void SetTutorialPanelHidden(bool hidden)
    {
        isHidden = hidden;
        if (panelTutorialRoot == null)
            return;

        panelTutorialRoot.SetActive(!hidden);
        panelMenu?.SetCompassCursorVisible(hidden);
    }

    private static Transform FindTransformByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindInChildrenRecursive(roots[i].transform, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private Button FindButtonByName(string name)
    {
        Transform target = FindTransformByName(name);
        if (target == null)
            return null;
        return target.GetComponent<Button>();
    }

    private static Transform FindInChildrenRecursive(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindInChildrenRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static bool WasUpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
    }

    private static bool WasDownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
    }

    private static bool WasLeftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
    }

    private static bool WasRightPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
    }

    private static bool WasConfirmPressed()
    {
        if (RemoteInput.ConfirmDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool WasCancelPressed()
    {
        if (RemoteInput.CancelDownThisFrame() || RemoteInput.RightClickCancelDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private static bool IsAnyTextInputFocusedInUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null)
            {
                InputField legacyInput = selected.GetComponentInParent<InputField>();
                if (legacyInput != null && legacyInput.isFocused)
                    return true;

                TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
                if (tmpInput != null && tmpInput.isFocused)
                    return true;
            }
        }

        return false;
    }
}
