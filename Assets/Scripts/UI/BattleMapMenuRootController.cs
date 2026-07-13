using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-200)]
public class BattleMapMenuRootController : MonoBehaviour
{
    private enum MenuPanel
    {
        Menu = 0,
        Options = 1,
        Gerenciar = 2
    }

    private enum MenuAction
    {
        Status = 0,
        Comando = 1,
        Rodada = 2,
        Opcoes = 3,
        VoltarMenu = 4,
        Minimapa = 5,
        Config = 6,
        Save = 7,
        Load = 8,
        Gerenciar = 9,
        VoltarOptions = 10,
        Destruir = 11,
        Render = 12,
        Sair = 13,
        VoltarGerenciar = 14,
        Camada = 15
    }

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "Tela de Entrada";

    [Header("Dock")]
    [SerializeField] [Range(0f, 300f)] private float dockEnterProximityPixels = 80f;
    [SerializeField] [Range(0f, 500f)] private float dockExitProximityPixels = 140f;
    [SerializeField] private Vector2 dockedAnchoredPosition = new Vector2(18f, 0f);

    [Header("Optional References")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject panelMenu;
    [SerializeField] private GameObject panelOptions;
    [SerializeField] private GameObject panelGerenciar;

    [SerializeField] private Button btnStatus;
    [SerializeField] private Button btnComando;
    [SerializeField] private Button btnRodada;
    [SerializeField] private Button btnOpcoes;
    [SerializeField] private Button btnVoltarMenu;
    [SerializeField] private Button btnCamada;

    [SerializeField] private Button btnMinimapa;
    [SerializeField] private Button btnConfig;
    [SerializeField] private Button btnSave;
    [SerializeField] private Button btnLoad;
    [SerializeField] private Button btnGerenciar;
    [SerializeField] private Button btnVoltarOptions;

    [SerializeField] private Button btnDestruir;
    [SerializeField] private Button btnRender;
    [SerializeField] private Button btnSair;
    [SerializeField] private Button btnVoltarGerenciar;

    private CursorController cursorController;
    private TurnStateManager turnStateManager;
    private SaveGameManager saveGameManager;
    private CameraController cameraController;
    private MatchController matchController;
    private ReplayManager replayManager;
    private RectTransform menuRootRect;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalPivot;
    private Vector2 originalAnchoredPosition;
    private bool dockLayoutCached;
    private bool isDockedCenterLeft;
    private bool hasLastUndockedScreenRect;
    private Rect lastUndockedScreenRect;
    private bool cursorNearUndockedDockRegion;

    private readonly Dictionary<MenuPanel, List<Button>> panelButtons = new Dictionary<MenuPanel, List<Button>>();
    private readonly Dictionary<Button, MenuAction> buttonActions = new Dictionary<Button, MenuAction>();
    private MenuPanel activePanel = MenuPanel.Menu;
    private int currentIndex;
    private bool menuInitialized;
    private bool menuOpen;
    private bool exitConfirmOpen;
    private int exitConfirmFocusIndex;
    private bool surrenderConfirmOpen;
    private bool layerSelectionOpen;
    private readonly List<FogOfWarVisionMode> layerSelectionModes = new List<FogOfWarVisionMode>();
    private int surrenderConfirmFocusIndex;
    private CanvasGroup modalMenuCanvasGroup;
    private float modalMenuPreviousAlpha = 1f;
    private bool modalMenuPreviousInteractable = true;
    private bool modalMenuPreviousBlocksRaycasts = true;
    private bool menuHiddenForModalPrompt;
    public bool IsExitConfirmationOpen => exitConfirmOpen;
    public int ExitConfirmationFocusIndex => exitConfirmFocusIndex;
    public bool IsSurrenderConfirmationOpen => surrenderConfirmOpen;
    public bool IsLayerSelectionOpen => layerSelectionOpen;
    public int LayerSelectionFocusIndex => 0;

    public int GetLayerSelectionOptionCount() => layerSelectionModes.Count + 1;

    public string GetLayerSelectionOptionLabel(int index)
    {
        if (index >= 0 && index < layerSelectionModes.Count)
            return layerSelectionModes[index] switch
            {
                FogOfWarVisionMode.Air => "AÉREA",
                FogOfWarVisionMode.Surface => "SUPERFÍCIE",
                FogOfWarVisionMode.Sub => "SUBMARINA",
                _ => "TODAS"
            };
        return "CANCELAR";
    }

    public void InvokeLayerSelectionOption(int index)
    {
        if (!layerSelectionOpen)
            return;
        if (index >= 0 && index < layerSelectionModes.Count)
            matchController?.SetFogOfWarVisionMode(layerSelectionModes[index]);
        layerSelectionOpen = false;
        layerSelectionModes.Clear();
        PanelHelperController.ClearExternalText();
        turnStateManager?.TryExitPlayerMenuStateToNeutral();
        UiInputBlocker.SuppressGameplayInputForFrames(1);
    }
    public int SurrenderConfirmationFocusIndex => surrenderConfirmFocusIndex;

    // Confirmacao de fim de turno (helper panel clicavel, mesmo tratamento de Render-se/Sair).
    // O backing e o estado EndingTurn do TurnStateManager (compartilhado por humano e IA);
    // aqui so expomos para o PanelHelperController montar os botoes e o clique invoca-los.
    public bool IsEndTurnConfirmationOpen =>
        turnStateManager != null && turnStateManager.CurrentCursorState == TurnStateManager.CursorState.EndingTurn;
    public int EndTurnConfirmationFocusIndex => 0;

    public void InvokeEndTurnConfirmationOption(int index)
    {
        if (cursorController == null)
            return;
        if (index == 0)
            cursorController.ConfirmEndTurnFromPointer();
        else
            cursorController.CancelEndTurnFromPointer();
    }

    public bool NavigateSurrenderConfirmation(int direction)
    {
        if (!surrenderConfirmOpen || direction == 0) return false;
        surrenderConfirmFocusIndex = (surrenderConfirmFocusIndex + (direction > 0 ? 1 : -1) + 2) % 2;
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public void InvokeSurrenderConfirmationOption(int index)
    {
        if (!surrenderConfirmOpen || index < 0 || index > 1) return;
        surrenderConfirmFocusIndex = index;
        if (index == 0)
        {
            surrenderConfirmOpen = false;
            PanelHelperController.ClearExternalText();
            CloseMenu(restoreCursor: false);
            matchController?.DeclareSurrenderDefeat();
        }
        else
            CancelSurrenderConfirmation();
    }

    public void CancelSurrenderConfirmation()
    {
        if (!surrenderConfirmOpen) return;
        surrenderConfirmOpen = false;
        surrenderConfirmFocusIndex = 0;
        RestoreMenuAfterModalPrompt();
        PanelHelperController.ClearExternalText();
        PlayCancelSfx();
        RestoreDefaultDialogForCurrentPanel();
    }

    public bool NavigateExitConfirmation(int direction)
    {
        if (!exitConfirmOpen || direction == 0) return false;
        exitConfirmFocusIndex = (exitConfirmFocusIndex + (direction > 0 ? 1 : -1) + 3) % 3;
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public void InvokeExitConfirmationOption(int index)
    {
        if (!exitConfirmOpen || index < 0 || index > 2) return;
        exitConfirmFocusIndex = index;
        if (index == 0)
        {
            exitConfirmOpen = false;
            PanelHelperController.ClearExternalText();
            PlayConfirmSfxOncePerFrame();
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else if (index == 1)
        {
            exitConfirmOpen = false;
            PanelHelperController.ClearExternalText();
            PlayConfirmSfxOncePerFrame();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        else
            CancelExitConfirmation();
    }

    public void InvokeFocusedExitConfirmationOption() => InvokeExitConfirmationOption(exitConfirmFocusIndex);

    public void CancelExitConfirmation()
    {
        if (!exitConfirmOpen) return;
        exitConfirmOpen = false;
        exitConfirmFocusIndex = 0;
        RestoreMenuAfterModalPrompt();
        PanelHelperController.ClearExternalText();
        PlayCancelSfx();
        RestoreDefaultDialogForCurrentPanel();
    }
    private bool pendingOpenOnNextNeutral;
    private bool eventSystemNavStateCaptured;
    private bool previousSendNavigationEvents;
    private int lastConfirmSfxFrame = -1;
    private Vector3Int savedCursorCell;
    private Coroutine restoreSelectionRoutine;
    private int restoredFromStateStackFrame = -1;
    private static int suppressMenuOpenFrame = -1;

    public bool IsMenuOpen => menuOpen;

    public bool TryToggleMenuFromShortcut()
    {
        TryAutoAssignReferences();
        EnsureButtonsCache();

        if (menuRoot == null)
            return false;

        if (menuOpen)
        {
            CloseMenu(restoreCursor: true);
            PlayCancelSfx();
            return true;
        }

        if (turnStateManager == null)
            return false;

        if (turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
            turnStateManager.ForceNeutral();

        if (!CanOpenMenuNow())
        {
            cursorController?.PlayErrorSfx();
            return false;
        }

        OpenMenu();
        if (!menuOpen)
            return false;

        PlayConfirmSfxOncePerFrame();
        return true;
    }

    public static bool TryRestoreMenuFromStateStack(TurnStateManager.CursorState exitedState = TurnStateManager.CursorState.Neutral)
    {
        bool restored = false;
        BattleMapMenuRootController[] controllers = FindObjectsByType<BattleMapMenuRootController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            BattleMapMenuRootController controller = controllers[i];
            if (controller != null && controller.RestoreMenuFromStateStack(exitedState))
                restored = true;
        }

        return restored;
    }

    public static void SuppressMenuOpenForCurrentFrame()
    {
        suppressMenuOpenFrame = Time.frameCount;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneInstance()
    {
        BattleMapMenuRootController[] existing = FindObjectsByType<BattleMapMenuRootController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (existing != null && existing.Length > 0)
            return;

        GameObject go = new GameObject("BattleMapMenuRootController");
        go.AddComponent<BattleMapMenuRootController>();
    }

    private void Awake()
    {
        TryAutoAssignReferences();
        EnsureButtonsCache();
        ForceCloseMenuState();
    }

    public bool TryHandleMenuInput(CursorController cursor, TurnStateManager stateManager)
    {
        cursorController = cursorController != null ? cursorController : cursor;
        turnStateManager = turnStateManager != null ? turnStateManager : stateManager;
        TryAutoAssignReferences();
        EnsureButtonsCache();

        if (menuRoot == null)
            return false;

        if (!menuOpen)
        {
            if (pendingOpenOnNextNeutral)
            {
                if (CanOpenMenuNow())
                {
                    OpenMenu();
                    pendingOpenOnNextNeutral = false;
                    PlayConfirmSfxOncePerFrame();
                    return true;
                }

                // Mantem o pedido pendente ate o proximo estado neutro.
                UiInputBlocker.SuppressGameplayInputForFrames(1);
                return true;
            }

            bool cancelShortcut = WasCancelShortcutPressedThisFrame();
            bool rightClickCancel = cursorController != null && cursorController.WasRightClickCancelTapThisFrame;
            if (!cancelShortcut && !rightClickCancel)
                return false;

            if (suppressMenuOpenFrame == Time.frameCount)
                return true;

            if (IsAnyTextInputFocusedInUi())
                return false;

            // Se a confirmacao de fim de turno estiver pendente, ESC deve cancelar
            // essa confirmacao no CursorController (nao abrir o menu do jogador).
            if (cursorController != null && cursorController.IsEndTurnConfirmationPending)
                return false;

            bool aiTurn = matchController != null && matchController.IsPlayerInputLockedByActiveAI();

            // Durante o turno da IA, o clique direito NUNCA abre nem agenda o menu — mesmo sendo o
            // equivalente do ESC. So o ESC/Backspace pode pausar a simulacao e abrir. Sem esta guarda,
            // um Rclick numa janela Neutral entre batches da IA (CanOpenMenuNow() == true) cairia
            // direto no OpenMenu() logo abaixo.
            if (aiTurn && !cancelShortcut)
                return false;

            if (!CanOpenMenuNow())
            {
                if (!aiTurn)
                    return false;

                pendingOpenOnNextNeutral = true;
                // Pausa a IA imediatamente (ponto seguro, igual ao F10): ela termina o batch atual e
                // para antes do proximo. Sem isso a IA continuaria iniciando batches e o menu so abriria
                // numa janela curta entre eles. O resume acontece ao fechar o menu (TryExitPlayerMenuStateToNeutral).
                AIController.Instance?.SetPlayerPaused(true);
                PanelDialogController.TrySetTransientText("Pausa da simulacao solicitada. Abrindo menu no proximo Neutral.", 2.4f);
                cursorController?.PlayBeepSfx();
                return true;
            }

            OpenMenu();
            PlayConfirmSfxOncePerFrame();
            return true;
        }

        // Enquanto o menu estiver aberto, bloqueia atalhos/gameplay de outros sistemas.
        UiInputBlocker.SuppressGameplayInputForFrames(1);
        RefreshDockByCursorProximity();

        if (restoredFromStateStackFrame == Time.frameCount)
            return true;

        if (!exitConfirmOpen && !surrenderConfirmOpen && WasPrimaryPointerPressedThisFrame(out Vector2 pointerPosition) &&
            !GetScreenRect(menuRootRect).Contains(pointerPosition) &&
            !IsPointerOverMenuShortcut(pointerPosition))
        {
            CloseMenu(restoreCursor: true);
            PlayCancelSfx();
            return true;
        }

        if (exitConfirmOpen)
        {
            if (WasUpPressedThisFrame() || WasLeftPressedThisFrame())
            {
                NavigateExitConfirmation(-1);
                return true;
            }
            if (WasDownPressedThisFrame() || WasRightPressedThisFrame())
            {
                NavigateExitConfirmation(+1);
                return true;
            }
            if (WasCancelRequestedThisFrame())
            {
                CancelExitConfirmation();
                return true;
            }

            if (WasConfirmPressedThisFrame())
            {
                InvokeFocusedExitConfirmationOption();
                return true;
            }

            return true;
        }

        if (surrenderConfirmOpen)
        {
            if (WasUpPressedThisFrame() || WasLeftPressedThisFrame()) { NavigateSurrenderConfirmation(-1); return true; }
            if (WasDownPressedThisFrame() || WasRightPressedThisFrame()) { NavigateSurrenderConfirmation(+1); return true; }
            if (WasCancelRequestedThisFrame()) { CancelSurrenderConfirmation(); return true; }
            if (WasConfirmPressedThisFrame()) { InvokeSurrenderConfirmationOption(surrenderConfirmFocusIndex); return true; }
            return true;
        }

        if (WasCancelRequestedThisFrame())
        {
            HandleBackByEsc();
            PlayCancelSfx();
            return true;
        }

        if (WasUpPressedThisFrame())
        {
            Navigate(-1);
            return true;
        }

        if (WasDownPressedThisFrame())
        {
            Navigate(+1);
            return true;
        }

        if (WasConfirmPressedThisFrame())
        {
            TriggerCurrentButton();
            return true;
        }

        return true;
    }

    // -------------------------------------------------------------------------
    // AI API — métodos públicos para o AIController emular o menu sem bloqueios
    // -------------------------------------------------------------------------

    /// <summary>
    /// Abre o menu in-game como a IA (sem desabilitar botões pelo turno da IA).
    /// </summary>
    public bool TryOpenMenuFromAI()
    {
        TryAutoAssignReferences();
        EnsureButtonsCache();
        if (menuRoot == null)
        {
            Debug.Log("[AI][Menu] TryOpenMenuFromAI → false (menuRoot == null)");
            return false;
        }
        if (menuOpen)
        {
            Debug.Log("[AI][Menu] TryOpenMenuFromAI → true (já estava aberto)");
            return true;
        }
        if (turnStateManager == null || !turnStateManager.TryEnterPlayerMenuState())
        {
            Debug.Log($"[AI][Menu] TryOpenMenuFromAI → false (TryEnterPlayerMenuState falhou | turnStateManager={turnStateManager != null} | cursorState={turnStateManager?.CurrentCursorState})");
            return false;
        }

        if (cursorController != null) savedCursorCell = cursorController.CurrentCell;
        menuRoot.SetActive(true);
        menuOpen = true;
        pendingOpenOnNextNeutral = false;
        exitConfirmOpen = false;
        surrenderConfirmOpen = false;
        RestoreMenuAfterModalPrompt();
        RestoreUndockedLayout();
        hasLastUndockedScreenRect = false;
        cursorNearUndockedDockRegion = false;
        CaptureAndDisableEventSystemNavigation();
        // Intencionalmente omite RefreshButtonInteractability: a IA pode selecionar qualquer botão.
        SetPanel(MenuPanel.Menu, resetIndex: true);
        PanelDialogController.ClearExternalText();
        PlayConfirmSfxOncePerFrame();
        Debug.Log("[AI][Menu] TryOpenMenuFromAI → true (menu aberto com sucesso)");
        return true;
    }

    /// <summary>
    /// Navega um passo na lista (ignora interatividade — a IA não é bloqueada por isso).
    /// </summary>
    public void NavigateMenuStepForAI(int delta)
    {
        if (!panelButtons.TryGetValue(activePanel, out List<Button> list) || list.Count <= 0) return;
        int count = list.Count;
        currentIndex = ((currentIndex + delta) % count + count) % count;
        SelectCurrentButton();
        cursorController?.PlayCursorMoveSfx();
    }

    /// <summary>True se o botão atualmente selecionado é o "Comando" (Reabastecer).</summary>
    public bool IsComandoButtonSelected
    {
        get
        {
            if (btnComando == null) return false;
            if (!panelButtons.TryGetValue(activePanel, out List<Button> list)) return false;
            return currentIndex >= 0 && currentIndex < list.Count && list[currentIndex] == btnComando;
        }
    }

    /// <summary>True se o botão atualmente selecionado é o "Rodada" (Passar a Vez).</summary>
    public bool IsRodadaButtonSelected
    {
        get
        {
            if (btnRodada == null) return false;
            if (!panelButtons.TryGetValue(activePanel, out List<Button> list)) return false;
            return currentIndex >= 0 && currentIndex < list.Count && list[currentIndex] == btnRodada;
        }
    }

    /// <summary>
    /// Fecha o menu e aciona Passar a Vez sem checar interatividade do botão.
    /// </summary>
    public bool TryTriggerRodadaForAI()
    {
        if (!TryCloseMenuForEndTurnDispatch()) return false;
        if (cursorController == null) return false;
        return cursorController.TryExecuteEndTurnFromMenu();
    }

    /// <summary>
    /// Fecha o menu e ABRE a confirmação de Passar a Vez (estado EndingTurn, com o
    /// helper panel de confirmação) sem executar. A IA no modo visível usa isto para
    /// confirmar como um humano faria, em vez de passar a vez direto.
    /// </summary>
    public bool TryOpenRodadaConfirmationForAI()
    {
        if (!TryCloseMenuForEndTurnDispatch()) return false;
        if (turnStateManager == null) return false;
        // O fechamento do menu pode deixar o cursor em PlayerMenu; a confirmação exige Neutral.
        turnStateManager.TryExitPlayerMenuStateToNeutral();
        return turnStateManager.TryOpenEndingTurnConfirmation(out _);
    }

    /// <summary>
    /// Fecha o menu e aciona o Serviço do Comando sem checar interatividade do botão.
    /// Retorna false se não há alvos ou se o estado é inválido.
    /// </summary>
    public bool TryTriggerComandoForAI()
    {
        if (!TryCloseMenuForCommandServiceDispatch()) return false;
        if (turnStateManager == null) return false;
        if (!turnStateManager.TryOpenCommandServiceFromMenu(out string message))
        {
            turnStateManager.TryExitPlayerMenuStateToNeutral();
            PanelDialogController.TrySetTransientText(message, 2.4f);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Fecha o menu sem passar a vez (para uso da IA quando precisa cancelar a abertura).
    /// </summary>
    public void CloseMenuFromAI()
    {
        CloseMenu(restoreCursor: true);
    }

    // -------------------------------------------------------------------------

    private void OpenMenu()
    {
        if (menuRoot == null)
            return;
        if (turnStateManager == null || !turnStateManager.TryEnterPlayerMenuState())
            return;

        if (cursorController != null)
            savedCursorCell = cursorController.CurrentCell;

        menuRoot.SetActive(true);
        menuOpen = true;
        pendingOpenOnNextNeutral = false;
        exitConfirmOpen = false;
        surrenderConfirmOpen = false;
        RestoreMenuAfterModalPrompt();
        RestoreUndockedLayout();
        hasLastUndockedScreenRect = false;
        cursorNearUndockedDockRegion = false;
        CaptureAndDisableEventSystemNavigation();
        RefreshButtonInteractability();
        SetPanel(MenuPanel.Menu, resetIndex: true);
        // Na primeiríssima abertura os botões estão sendo ativados neste frame: o Selectable só aplica
        // a cor de "selecionado" no frame seguinte. Reaplica a seleção 1 frame depois (como faz o
        // RestoreMenuFromStateStack) para o "Situação" já vir destacado logo na primeira vez.
        ScheduleRestoreSelectionNextFrame();
        PanelDialogController.ClearExternalText();
        ApplyPlayerMenuAiPauseIfNeeded();
    }

    // Pause de JOGADOR: enquanto o menu in-game do jogador está aberto durante o turno da IA, segura
    // o loop da IA (limpo, sem AI STEP do F10). Retomado em TryExitPlayerMenuStateToNeutral. Só pausa
    // no turno da IA — durante o turno do jogador a IA nem está rodando.
    private void ApplyPlayerMenuAiPauseIfNeeded()
    {
        if (matchController != null && matchController.IsPlayerInputLockedByActiveAI())
            AIController.Instance?.SetPlayerPaused(true);
    }

    private bool RestoreMenuFromStateStack(TurnStateManager.CursorState exitedState)
    {
        TryAutoAssignReferences();
        EnsureButtonsCache();

        if (menuRoot == null || turnStateManager == null)
            return false;
        if (turnStateManager.CurrentCursorState != TurnStateManager.CursorState.PlayerMenu)
            return false;

        if (cursorController != null)
            savedCursorCell = cursorController.CurrentCell;

        menuRoot.SetActive(true);
        menuOpen = true;
        pendingOpenOnNextNeutral = false;
        exitConfirmOpen = false;
        surrenderConfirmOpen = false;
        RestoreMenuAfterModalPrompt();
        RestoreUndockedLayout();
        hasLastUndockedScreenRect = false;
        cursorNearUndockedDockRegion = false;
        CaptureAndDisableEventSystemNavigation();
        RefreshButtonInteractability();
        SetPanel(activePanel, resetIndex: false);
        RestoreSelectionForExitedState(exitedState);
        SelectCurrentButton();
        ScheduleRestoreSelectionNextFrame();
        PanelDialogController.ClearExternalText();
        restoredFromStateStackFrame = Time.frameCount;
        // Menu reaberto (ex.: voltando de save/load) durante o turno da IA → mantém a IA pausada.
        ApplyPlayerMenuAiPauseIfNeeded();
        return true;
    }

    private void RestoreSelectionForExitedState(TurnStateManager.CursorState exitedState)
    {
        switch (exitedState)
        {
            case TurnStateManager.CursorState.CommandService:
                SetPanel(MenuPanel.Menu, resetIndex: false);
                SetPanelSelectionByButton(btnComando);
                break;
            case TurnStateManager.CursorState.RemovingUnit:
                SetPanel(MenuPanel.Gerenciar, resetIndex: false);
                SetPanelSelectionByButton(btnDestruir);
                break;
            case TurnStateManager.CursorState.Saving:
                SetPanel(MenuPanel.Options, resetIndex: false);
                SetPanelSelectionByButton(btnSave);
                break;
            case TurnStateManager.CursorState.Loading:
                SetPanel(MenuPanel.Options, resetIndex: false);
                SetPanelSelectionByButton(btnLoad);
                break;
        }
    }

    private void RefreshButtonInteractability()
    {
        bool isAiTurn = matchController != null && matchController.IsPlayerInputLockedByActiveAI();
        SetButtonInteractable(btnStatus,   !isAiTurn && !TutorialManager.IsStatusSummaryBlockedByTutorial);
        SetButtonInteractable(btnComando,  !isAiTurn && !TutorialManager.IsCommandServiceBlockedByTutorial);
        SetButtonInteractable(btnRodada,   !isAiTurn && !TutorialManager.IsEndTurnLockedByTutorial);
        SetButtonInteractable(btnDestruir, !isAiTurn && !TutorialManager.IsRemoveUnitBlockedByTutorial);
        SetButtonInteractable(btnRender,   !isAiTurn && !TutorialManager.IsSurrenderBlockedByTutorial);
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void CloseMenu(bool restoreCursor)
    {
        CloseMenu(restoreCursor, exitPlayerMenuState: true);
    }

    private void CloseMenu(bool restoreCursor, bool exitPlayerMenuState)
    {
        RestoreMenuAfterModalPrompt();
        if (menuRoot != null)
            menuRoot.SetActive(false);

        menuOpen = false;
        pendingOpenOnNextNeutral = false;
        exitConfirmOpen = false;
        surrenderConfirmOpen = false;
        if (exitPlayerMenuState)
        {
            turnStateManager?.TryExitPlayerMenuStateToNeutral();
        }
        RestoreUndockedLayout();
        hasLastUndockedScreenRect = false;
        cursorNearUndockedDockRegion = false;
        PanelDialogController.ClearExternalText();
        RestoreEventSystemNavigation();

        if (restoreCursor && cursorController != null)
            cursorController.SetCell(savedCursorCell, playMoveSfx: false, adjustCamera: true);
    }

    private void ForceCloseMenuState()
    {
        RestoreMenuAfterModalPrompt();
        if (menuRoot != null)
            menuRoot.SetActive(false);

        menuOpen = false;
        pendingOpenOnNextNeutral = false;
        exitConfirmOpen = false;
        surrenderConfirmOpen = false;
    }

    private void HandleBackByEsc()
    {
        if (activePanel == MenuPanel.Menu)
        {
            CloseMenu(restoreCursor: true);
            return;
        }

        if (activePanel == MenuPanel.Options)
        {
            SetPanel(MenuPanel.Menu, resetIndex: false);
            SetPanelSelectionByButton(btnOpcoes);
            return;
        }

        SetPanel(MenuPanel.Options, resetIndex: false);
        SetPanelSelectionByButton(btnGerenciar);
    }

    private void Navigate(int delta)
    {
        if (!panelButtons.TryGetValue(activePanel, out List<Button> list) || list.Count <= 0)
            return;

        int original = currentIndex;
        int count = list.Count;
        int next = currentIndex;

        for (int i = 0; i < count; i++)
        {
            next = (next + delta + count) % count;
            if (next == original)
                return; // voltou ao inicio sem achar nenhum habilitado
            Button candidate = list[next];
            if (candidate != null && candidate.interactable)
                break;
        }

        if (next == original)
            return;

        currentIndex = next;
        SelectCurrentButton();
        cursorController?.PlayCursorMoveSfx();
    }

    private void TriggerCurrentButton()
    {
        if (!panelButtons.TryGetValue(activePanel, out List<Button> list) || list.Count <= 0)
            return;

        currentIndex = Mathf.Clamp(currentIndex, 0, list.Count - 1);
        Button button = list[currentIndex];
        if (button == null || !button.interactable)
            return;

        button.onClick?.Invoke();
    }

    private void SetPanel(MenuPanel panel, bool resetIndex)
    {
        activePanel = panel;
        if (panelMenu != null) panelMenu.SetActive(panel == MenuPanel.Menu);
        if (panelOptions != null) panelOptions.SetActive(panel == MenuPanel.Options);
        if (panelGerenciar != null) panelGerenciar.SetActive(panel == MenuPanel.Gerenciar);

        if (!panelButtons.TryGetValue(activePanel, out List<Button> list) || list.Count <= 0)
        {
            currentIndex = 0;
            return;
        }

        if (resetIndex || currentIndex < 0 || currentIndex >= list.Count)
        {
            currentIndex = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].interactable) { currentIndex = i; break; }
            }
        }

        SelectCurrentButton();
    }

    private void SelectCurrentButton()
    {
        if (!panelButtons.TryGetValue(activePanel, out List<Button> list) || list.Count <= 0)
            return;

        currentIndex = Mathf.Clamp(currentIndex, 0, list.Count - 1);
        Button button = list[currentIndex];
        if (button == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(button.gameObject);
        button.Select();
    }

    private void ScheduleRestoreSelectionNextFrame()
    {
        if (!isActiveAndEnabled)
            return;

        if (restoreSelectionRoutine != null)
            StopCoroutine(restoreSelectionRoutine);
        restoreSelectionRoutine = StartCoroutine(RestoreSelectionNextFrame());
    }

    private IEnumerator RestoreSelectionNextFrame()
    {
        yield return null;
        restoreSelectionRoutine = null;
        if (!menuOpen)
            yield break;

        SelectCurrentButton();
    }

    private void EnsureButtonsCache()
    {
        BindButton(btnStatus, MenuAction.Status);
        BindButton(btnComando, MenuAction.Comando);
        BindButton(btnRodada, MenuAction.Rodada);
        BindButton(btnOpcoes, MenuAction.Opcoes);
        BindButton(btnVoltarMenu, MenuAction.VoltarMenu);
        BindButton(btnCamada, MenuAction.Camada);

        BindButton(btnMinimapa, MenuAction.Minimapa);
        BindButton(btnConfig, MenuAction.Config);
        BindButton(btnSave, MenuAction.Save);
        BindButton(btnLoad, MenuAction.Load);
        BindButton(btnGerenciar, MenuAction.Gerenciar);
        BindButton(btnVoltarOptions, MenuAction.VoltarOptions);

        BindButton(btnDestruir, MenuAction.Destruir);
        BindButton(btnRender, MenuAction.Render);
        BindButton(btnSair, MenuAction.Sair);
        BindButton(btnVoltarGerenciar, MenuAction.VoltarGerenciar);

        panelButtons.Clear();
        RefreshLayerButtonAvailability();
        panelButtons[MenuPanel.Menu] = BuildPanelButtonsFromLayout(panelMenu, btnStatus, btnComando, btnRodada, btnCamada, btnOpcoes, btnVoltarMenu);
        panelButtons[MenuPanel.Options] = BuildPanelButtonsFromLayout(panelOptions, btnMinimapa, btnConfig, btnSave, btnLoad, btnGerenciar, btnVoltarOptions);
        panelButtons[MenuPanel.Gerenciar] = BuildPanelButtonsFromLayout(panelGerenciar, btnDestruir, btnRender, btnSair, btnVoltarGerenciar);

    }

    private List<Button> BuildPanelButtons(params Button[] source)
    {
        List<Button> list = new List<Button>();
        for (int i = 0; i < source.Length; i++)
        {
            Button button = source[i];
            if (button != null && !list.Contains(button))
                list.Add(button);
        }

        return list;
    }

    private List<Button> BuildPanelButtonsFromLayout(GameObject panel, params Button[] fallbackButtons)
    {
        List<Button> list = new List<Button>();

        if (panel != null)
        {
            Button[] panelCandidates = panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < panelCandidates.Length; i++)
            {
                Button button = panelCandidates[i];
                if (IsNavigablePanelButton(panel, button) && !list.Contains(button))
                    list.Add(button);
            }

            list.Sort(CompareButtonsByVisualOrder);
        }

        for (int i = 0; i < fallbackButtons.Length; i++)
        {
            Button button = fallbackButtons[i];
            if (IsNavigablePanelButton(panel, button) && !list.Contains(button))
                list.Add(button);
        }

        return list;
    }

    private static bool IsNavigablePanelButton(GameObject panel, Button button)
    {
        if (panel == null || button == null)
            return false;
        if (!IsInsidePanelWithoutCrossingNestedPanel(panel.transform, button.transform))
            return false;
        if (!button.gameObject.activeSelf)
            return false;

        RectTransform rect = GetButtonVisualRectTransform(button);
        if (rect != null && (rect.rect.width <= 1f || rect.rect.height <= 1f))
            return false;

        return true;
    }

    private static bool IsInsidePanelWithoutCrossingNestedPanel(Transform panel, Transform child)
    {
        if (panel == null || child == null)
            return false;

        Transform current = child;
        while (current != null)
        {
            if (current == panel)
                return true;

            current = current.parent;
            if (current != null && current != panel && IsMenuPanelTransform(current))
                return false;
        }

        return false;
    }

    private static bool IsMenuPanelTransform(Transform transform)
    {
        if (transform == null)
            return false;

        string name = transform.name;
        return string.Equals(name, "panel_menu", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "panel_options", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "panel_opcoes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "panel_opções", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "panel_gerenciar", StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareButtonsByVisualOrder(Button a, Button b)
    {
        Vector3 aCenter = GetButtonWorldCenter(a);
        Vector3 bCenter = GetButtonWorldCenter(b);

        float yDelta = bCenter.y - aCenter.y;
        if (Mathf.Abs(yDelta) > 0.01f)
            return yDelta > 0f ? 1 : -1;

        float xDelta = aCenter.x - bCenter.x;
        if (Mathf.Abs(xDelta) > 0.01f)
            return xDelta > 0f ? 1 : -1;

        int siblingA = a != null ? a.transform.GetSiblingIndex() : 0;
        int siblingB = b != null ? b.transform.GetSiblingIndex() : 0;
        return siblingA.CompareTo(siblingB);
    }

    private static Vector3 GetButtonWorldCenter(Button button)
    {
        if (button == null)
            return Vector3.zero;

        RectTransform rect = GetButtonVisualRectTransform(button);
        if (rect == null)
            return button.transform.position;

        return rect.TransformPoint(rect.rect.center);
    }

    private static RectTransform GetButtonVisualRectTransform(Button button)
    {
        if (button == null)
            return null;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (HasUsableRect(rect))
            return rect;

        if (button.targetGraphic != null && HasUsableRect(button.targetGraphic.rectTransform))
            return button.targetGraphic.rectTransform;

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic != null && HasUsableRect(graphic.rectTransform))
                return graphic.rectTransform;
        }

        return rect;
    }

    private static bool HasUsableRect(RectTransform rect)
    {
        return rect != null && rect.rect.width > 1f && rect.rect.height > 1f;
    }

    private void BindButton(Button button, MenuAction action)
    {
        if (button == null)
            return;

        if (buttonActions.TryGetValue(button, out MenuAction existing) && existing == action)
            return;

        bool alreadyTracked = buttonActions.ContainsKey(button);
        buttonActions[button] = action;
        if (alreadyTracked)
            return;

        button.onClick.AddListener(() => OnButtonClicked(button));
    }

    private static bool TryInferActionFromButton(MenuPanel panel, Button button, out MenuAction action)
    {
        action = default;
        if (button == null)
            return false;

        string name = button.name.ToLowerInvariant();
        switch (panel)
        {
            case MenuPanel.Menu:
                if (name.Contains("status")) { action = MenuAction.Status; return true; }
                if (name.Contains("comando")) { action = MenuAction.Comando; return true; }
                if (name.Contains("rodada")) { action = MenuAction.Rodada; return true; }
                if (name.Contains("camada") || name.Contains("layer")) { action = MenuAction.Camada; return true; }
                if (name.Contains("opcoes") || name.Contains("opções")) { action = MenuAction.Opcoes; return true; }
                if (name.Contains("voltar")) { action = MenuAction.VoltarMenu; return true; }
                break;
            case MenuPanel.Options:
                if (name.Contains("minimapa") || name.Contains("minimap")) { action = MenuAction.Minimapa; return true; }
                if (name.Contains("config")) { action = MenuAction.Config; return true; }
                if (name.Contains("save")) { action = MenuAction.Save; return true; }
                if (name.Contains("load")) { action = MenuAction.Load; return true; }
                if (name.Contains("gerenciar")) { action = MenuAction.Gerenciar; return true; }
                if (name.Contains("voltar")) { action = MenuAction.VoltarOptions; return true; }
                break;
            case MenuPanel.Gerenciar:
                if (name.Contains("destruir")) { action = MenuAction.Destruir; return true; }
                if (name.Contains("render")) { action = MenuAction.Render; return true; }
                if (name.Contains("sair")) { action = MenuAction.Sair; return true; }
                if (name.Contains("voltar")) { action = MenuAction.VoltarGerenciar; return true; }
                break;
        }

        return false;
    }

    private void OnButtonClicked(Button button)
    {
        if (button == null || !buttonActions.TryGetValue(button, out MenuAction action))
            return;

        SyncSelectionFromClickedButton(button);

        if (action != MenuAction.Comando &&
            action != MenuAction.Minimapa &&
            action != MenuAction.Rodada &&
            action != MenuAction.Save &&
            action != MenuAction.Load)
            PlayConfirmSfxOncePerFrame();

        switch (action)
        {
            case MenuAction.Status:
                if (TutorialManager.IsStatusSummaryBlockedByTutorial)
                {
                    TutorialManager.ShowBlockedActionScold(TutorialScoldKind.StatusSummary);
                    cursorController?.PlayErrorSfx();
                    break;
                }
                ShowStatusSummary();
                break;
            case MenuAction.Comando:
                if (!TryCloseMenuForCommandServiceDispatch())
                    break;
                if (turnStateManager != null && !turnStateManager.TryOpenCommandServiceFromMenu(out string commandMessage))
                {
                    turnStateManager.TryExitPlayerMenuStateToNeutral();
                    PanelDialogController.TrySetTransientText(commandMessage, 2.4f);
                }
                break;
            case MenuAction.Rodada:
                if (!TryCloseMenuForEndTurnDispatch())
                    break;
                // Botão deliberado do menu → abre a confirmação (prompt de fim de turno), não
                // passa direto. O atalho R é o caminho rápido; os botões pedem confirmação.
                turnStateManager?.TryExitPlayerMenuStateToNeutral();
                if (cursorController == null || !cursorController.RequestEndTurnConfirmation())
                    cursorController?.PlayErrorSfx();
                break;
            case MenuAction.Camada:
                if (!TryCloseMenuForSaveLoadDispatch() || matchController == null)
                    break;
                layerSelectionModes.Clear();
                matchController.GetAvailableFogOfWarVisionModes(layerSelectionModes);
                if (layerSelectionModes.Count <= 1)
                {
                    turnStateManager?.TryExitPlayerMenuStateToNeutral();
                    break;
                }
                layerSelectionOpen = true;
                PanelDialogController.ClearExternalText();
                PanelHelperController.TrySetExternalText("CAMADA (LAYER)", "Escolha a visualização:");
                break;
            case MenuAction.Opcoes:
                SetPanel(MenuPanel.Options, resetIndex: true);
                ScheduleRestoreSelectionNextFrame();
                break;
            case MenuAction.VoltarMenu:
                CloseMenu(restoreCursor: true);
                break;
            case MenuAction.Minimapa:
                if (!TryCloseMenuForDispatchAndEnsureNeutral())
                    break;
                cameraController?.ToggleQuickZoomFromMenu();
                break;
            case MenuAction.Config:
                PanelDialogController.TrySetTransientText("Config de partida: em desenvolvimento.", 2.4f);
                break;
            case MenuAction.Save:
                if (!TryCloseMenuForSaveLoadDispatch())
                    break;
                saveGameManager?.OpenSaveSlotPromptFromMenu();
                break;
            case MenuAction.Load:
                if (!TryCloseMenuForSaveLoadDispatch())
                    break;
                saveGameManager?.OpenLoadSlotPromptFromMenu();
                break;
            case MenuAction.Gerenciar:
                SetPanel(MenuPanel.Gerenciar, resetIndex: true);
                ScheduleRestoreSelectionNextFrame();
                break;
            case MenuAction.VoltarOptions:
                SetPanel(MenuPanel.Menu, resetIndex: false);
                SetPanelSelectionByButton(btnOpcoes);
                ScheduleRestoreSelectionNextFrame();
                break;
            case MenuAction.Destruir:
                if (!TryCloseMenuForRemoveUnitDispatch())
                    break;
                if (turnStateManager != null && !turnStateManager.TryOpenDestroyUnitPromptFromMenu(out string destroyMessage))
                {
                    turnStateManager.TryExitPlayerMenuStateToNeutral();
                    PanelDialogController.TrySetTransientText(destroyMessage, 2.4f);
                }
                break;
            case MenuAction.Render:
                if (TutorialManager.IsSurrenderBlockedByTutorial)
                {
                    TutorialManager.ShowBlockedActionScold(TutorialScoldKind.Surrender);
                    cursorController?.PlayErrorSfx();
                    break;
                }
                surrenderConfirmOpen = true;
                surrenderConfirmFocusIndex = 0;
                HideMenuForModalPrompt();
                PanelDialogController.ClearExternalText();
                PanelHelperController.TrySetExternalText("RENDER-SE", "Confirmar rendição? A partida será perdida.");
                break;
            case MenuAction.Sair:
                exitConfirmOpen = true;
                exitConfirmFocusIndex = 0;
                HideMenuForModalPrompt();
                PanelDialogController.ClearExternalText();
                PanelHelperController.TrySetExternalText("SAIR DA PARTIDA", "Escolha o destino:");
                break;
            case MenuAction.VoltarGerenciar:
                SetPanel(MenuPanel.Options, resetIndex: false);
                SetPanelSelectionByButton(btnGerenciar);
                ScheduleRestoreSelectionNextFrame();
                break;
        }
    }

    private void HideMenuForModalPrompt()
    {
        if (menuRoot == null || menuHiddenForModalPrompt)
            return;
        modalMenuCanvasGroup = menuRoot.GetComponent<CanvasGroup>();
        if (modalMenuCanvasGroup == null)
            modalMenuCanvasGroup = menuRoot.AddComponent<CanvasGroup>();
        modalMenuPreviousAlpha = modalMenuCanvasGroup.alpha;
        modalMenuPreviousInteractable = modalMenuCanvasGroup.interactable;
        modalMenuPreviousBlocksRaycasts = modalMenuCanvasGroup.blocksRaycasts;
        modalMenuCanvasGroup.alpha = 0f;
        modalMenuCanvasGroup.interactable = false;
        modalMenuCanvasGroup.blocksRaycasts = false;
        menuHiddenForModalPrompt = true;
    }

    private void RestoreMenuAfterModalPrompt()
    {
        if (!menuHiddenForModalPrompt)
            return;
        if (modalMenuCanvasGroup != null)
        {
            modalMenuCanvasGroup.alpha = modalMenuPreviousAlpha;
            modalMenuCanvasGroup.interactable = modalMenuPreviousInteractable;
            modalMenuCanvasGroup.blocksRaycasts = modalMenuPreviousBlocksRaycasts;
        }
        menuHiddenForModalPrompt = false;
        SelectCurrentButton();
        ScheduleRestoreSelectionNextFrame();
    }

    private bool SetPanelSelectionByButton(Button target)
    {
        if (target == null || !panelButtons.TryGetValue(activePanel, out List<Button> list) || list == null || list.Count <= 0)
            return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != target)
                continue;

            currentIndex = i;
            SelectCurrentButton();
            return true;
        }

        return false;
    }

    private void SyncSelectionFromClickedButton(Button clickedButton)
    {
        if (clickedButton == null)
            return;

        foreach (KeyValuePair<MenuPanel, List<Button>> pair in panelButtons)
        {
            List<Button> list = pair.Value;
            if (list == null)
                continue;

            int index = list.IndexOf(clickedButton);
            if (index < 0)
                continue;

            activePanel = pair.Key;
            currentIndex = index;
            return;
        }
    }

    private void ShowStatusSummary()
    {
        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        int turnNumber = matchController != null ? matchController.CurrentTurn : 0;
        int money = matchController != null ? matchController.GetActualMoney(activeTeam) : 0;
        string treasury = matchController != null && matchController.ShouldHideActiveAiActionPresentation()
            ? "----"
            : Mathf.Max(0, money).ToString();
        string message = $"Status da partida\nRodada: {turnNumber}\nTime ativo: {TeamUtils.GetName(activeTeam)}\nTesouro: ${treasury}";
        PanelDialogController.TrySetExternalText(message + "\nESC: voltar");
    }

    private bool TryCloseMenuForDispatchAndEnsureNeutral()
    {
        CloseMenu(restoreCursor: true);
        if (turnStateManager == null)
            return true;

        if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.PlayerMenu)
            return true;

        string message = $"Menu do jogador: estado nao normalizado para Neutral (atual: {turnStateManager.CurrentCursorState}).";
        PanelDialogController.TrySetTransientText(message, 2.8f);
        cursorController?.PlayErrorSfx();
        return false;
    }

    private bool TryCloseMenuForCommandServiceDispatch()
    {
        CloseMenu(restoreCursor: true, exitPlayerMenuState: false);
        if (turnStateManager == null)
            return true;

        TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
        if (state == TurnStateManager.CursorState.PlayerMenu ||
            state == TurnStateManager.CursorState.Neutral)
            return true;

        string message = $"Menu do jogador: estado invalido para Servico do Comando (atual: {state}).";
        PanelDialogController.TrySetTransientText(message, 2.8f);
        cursorController?.PlayErrorSfx();
        return false;
    }

    private bool TryCloseMenuForRemoveUnitDispatch()
    {
        CloseMenu(restoreCursor: true, exitPlayerMenuState: false);
        if (turnStateManager == null)
            return true;

        TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
        if (state == TurnStateManager.CursorState.PlayerMenu ||
            state == TurnStateManager.CursorState.Neutral)
            return true;

        string message = $"Menu do jogador: estado invalido para Destroy Unit (atual: {state}).";
        PanelDialogController.TrySetTransientText(message, 2.8f);
        cursorController?.PlayErrorSfx();
        return false;
    }

    private bool TryCloseMenuForEndTurnDispatch()
    {
        CloseMenu(restoreCursor: true, exitPlayerMenuState: false);
        if (turnStateManager == null)
            return true;

        TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
        if (state == TurnStateManager.CursorState.PlayerMenu ||
            state == TurnStateManager.CursorState.Neutral)
            return true;

        string message = $"Menu do jogador: estado invalido para Passar a Vez (atual: {state}).";
        PanelDialogController.TrySetTransientText(message, 2.8f);
        cursorController?.PlayErrorSfx();
        return false;
    }

    private bool TryCloseMenuForSaveLoadDispatch()
    {
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        CloseMenu(restoreCursor: true, exitPlayerMenuState: false);
        if (turnStateManager == null)
            return true;

        TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
        if (state == TurnStateManager.CursorState.PlayerMenu ||
            state == TurnStateManager.CursorState.Neutral)
            return true;

        string message = $"Menu do jogador: estado invalido para Save/Load (atual: {state}).";
        PanelDialogController.TrySetTransientText(message, 2.8f);
        cursorController?.PlayErrorSfx();
        return false;
    }

    private void RestoreDefaultDialogForCurrentPanel()
    {
        PanelDialogController.ClearExternalText();
    }

    private bool CanOpenMenuNow()
    {
        if (turnStateManager == null)
            return false;

        if (turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
            return false;

        if (turnStateManager.IsScannerActionExecutionInProgress)
            return false;

        // Espera o batch da IA terminar de verdade (animacao de movimento, step routines e scanner),
        // mesmo guarda usado pelo F10 em ExecuteAIBatchWithDebugStep. Sem isso o menu abriria no meio
        // de um batch de movimento simples, quando o cursor volta a Neutral mas a animacao ainda roda.
        if (replayManager != null && replayManager.IsStepExecutionBusy)
            return false;

        return true;
    }

    private void PlayConfirmSfxOncePerFrame()
    {
        if (lastConfirmSfxFrame == Time.frameCount)
            return;

        lastConfirmSfxFrame = Time.frameCount;
        cursorController?.PlayConfirmSfx();
    }

    private void PlayCancelSfx()
    {
        cursorController?.PlayCancelSfx();
    }

    private void CaptureAndDisableEventSystemNavigation()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        previousSendNavigationEvents = eventSystem.sendNavigationEvents;
        eventSystemNavStateCaptured = true;
        eventSystem.sendNavigationEvents = false;
        eventSystem.SetSelectedGameObject(null);
    }

    private void RestoreEventSystemNavigation()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        if (eventSystemNavStateCaptured)
            eventSystem.sendNavigationEvents = previousSendNavigationEvents;

        eventSystemNavStateCaptured = false;
        eventSystem.SetSelectedGameObject(null);
    }

    private void TryAutoAssignReferences()
    {
        if (menuRoot == null)
            menuRoot = FindSceneObjectByName("menuRoot");
        if (panelMenu == null)
            panelMenu = FindChildByName(menuRoot != null ? menuRoot.transform : null, "panel_menu")?.gameObject;
        if (panelOptions == null)
            panelOptions = FindChildByName(menuRoot != null ? menuRoot.transform : null, "panel_options")?.gameObject;
        if (panelGerenciar == null)
            panelGerenciar = FindChildByName(menuRoot != null ? menuRoot.transform : null, "panel_gerenciar")?.gameObject;

        if (menuRoot != null && !menuInitialized)
        {
            if (panelMenu != null) panelMenu.SetActive(true);
            if (panelOptions != null) panelOptions.SetActive(false);
            if (panelGerenciar != null) panelGerenciar.SetActive(false);
            menuRoot.SetActive(false);
            menuInitialized = true;
        }

        if (menuRootRect == null && menuRoot != null)
            menuRootRect = menuRoot.GetComponent<RectTransform>();
        CacheOriginalDockLayoutIfNeeded();

        if (btnStatus == null) btnStatus = FindButtonByNames(panelMenu, "btn_status", "button_status");
        if (btnComando == null) btnComando = FindButtonByNames(panelMenu, "btn_comando", "button_comando");
        if (btnRodada == null) btnRodada = FindButtonByNames(panelMenu, "btn_rodada", "button_rodada");
        if (btnOpcoes == null) btnOpcoes = FindButtonByNames(panelMenu, "btn_opcoes", "button_opcoes", "button_opções");
        if (btnVoltarMenu == null) btnVoltarMenu = FindButtonByNames(panelMenu, "btn_voltar", "button_voltar");
        if (btnCamada == null) btnCamada = FindButtonByNames(panelMenu, "btn_camada", "button_camada", "button_layer");

        if (btnMinimapa == null) btnMinimapa = FindButtonByNames(panelOptions, "btn_minimapa", "button_minimapa", "button_miniMapa");
        if (btnConfig == null) btnConfig = FindButtonByNames(panelOptions, "btn_config", "button_config");
        if (btnSave == null) btnSave = FindButtonByNames(panelOptions, "button_save", "btn_save");
        if (btnLoad == null) btnLoad = FindButtonByNames(panelOptions, "button_load", "btn_load");
        if (btnGerenciar == null) btnGerenciar = FindButtonByNames(panelOptions, "btn_gerenciar", "button_gerenciar");
        if (btnVoltarOptions == null) btnVoltarOptions = FindButtonByNames(panelOptions, "btn_voltar", "button_voltar");

        if (btnDestruir == null) btnDestruir = FindButtonByNames(panelGerenciar, "btn_destruir", "button_destruir");
        if (btnRender == null) btnRender = FindButtonByNames(panelGerenciar, "btn_render", "button_render");
        if (btnSair == null) btnSair = FindButtonByNames(panelGerenciar, "btn_sair", "button_sair");
        if (btnVoltarGerenciar == null) btnVoltarGerenciar = FindButtonByNames(panelGerenciar, "btn_voltar", "button_voltar");

        if (cursorController == null) cursorController = FindInActiveScene<CursorController>();
        if (turnStateManager == null) turnStateManager = FindInActiveScene<TurnStateManager>();
        if (saveGameManager == null) saveGameManager = FindInActiveScene<SaveGameManager>();
        if (cameraController == null) cameraController = FindInActiveScene<CameraController>();
        if (matchController == null) matchController = FindInActiveScene<MatchController>();
        if (replayManager == null) replayManager = FindInActiveScene<ReplayManager>();
    }

    private void RefreshLayerButtonAvailability()
    {
        if (btnCamada == null)
            return;
        List<FogOfWarVisionMode> modes = new List<FogOfWarVisionMode>();
        bool available = matchController != null && matchController.GetAvailableFogOfWarVisionModes(modes) > 1;
        if (btnCamada.gameObject.activeSelf != available)
            btnCamada.gameObject.SetActive(available);
        btnCamada.interactable = available;
    }

    private void CacheOriginalDockLayoutIfNeeded()
    {
        if (dockLayoutCached || menuRootRect == null)
            return;

        originalAnchorMin = menuRootRect.anchorMin;
        originalAnchorMax = menuRootRect.anchorMax;
        originalPivot = menuRootRect.pivot;
        originalAnchoredPosition = menuRootRect.anchoredPosition;
        dockLayoutCached = true;
    }

    private void RefreshDockByCursorProximity()
    {
        if (!menuOpen || menuRootRect == null || cursorController == null || menuRoot == null)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }

        Vector3 cursorWorld = cursorController.transform.position;
        Vector3 cursorScreen = cam.WorldToScreenPoint(cursorWorld);
        if (cursorScreen.z < 0f)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }

        if (!isDockedCenterLeft)
        {
            RectTransform dockReference = GetDockReferenceRectTransform();
            Rect panelScreenRect = GetScreenRect(dockReference != null ? dockReference : menuRootRect);
            if (panelScreenRect.width <= 0f || panelScreenRect.height <= 0f)
            {
                cursorNearUndockedDockRegion = false;
                return;
            }

            lastUndockedScreenRect = panelScreenRect;
            hasLastUndockedScreenRect = true;
            cursorNearUndockedDockRegion = IsNearRect(panelScreenRect, cursorScreen, dockEnterProximityPixels);
            if (cursorNearUndockedDockRegion)
                ApplyDockCenterLeft();
            return;
        }

        if (!hasLastUndockedScreenRect)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }

        cursorNearUndockedDockRegion = IsNearRect(lastUndockedScreenRect, cursorScreen, dockExitProximityPixels);
        if (!cursorNearUndockedDockRegion)
            RestoreUndockedLayout();
    }

    private RectTransform GetDockReferenceRectTransform()
    {
        if (activePanel == MenuPanel.Menu && panelMenu != null)
            return panelMenu.GetComponent<RectTransform>();

        if (activePanel == MenuPanel.Options && panelOptions != null)
            return panelOptions.GetComponent<RectTransform>();

        if (activePanel == MenuPanel.Gerenciar && panelGerenciar != null)
            return panelGerenciar.GetComponent<RectTransform>();

        return null;
    }

    private void ApplyDockCenterLeft()
    {
        if (menuRootRect == null)
            return;

        CacheOriginalDockLayoutIfNeeded();
        menuRootRect.anchorMin = new Vector2(0f, 0.5f);
        menuRootRect.anchorMax = new Vector2(0f, 0.5f);
        menuRootRect.pivot = new Vector2(0f, 0.5f);
        menuRootRect.anchoredPosition = dockedAnchoredPosition;
        isDockedCenterLeft = true;
    }

    private void RestoreUndockedLayout()
    {
        if (menuRootRect == null || !dockLayoutCached)
            return;

        menuRootRect.anchorMin = originalAnchorMin;
        menuRootRect.anchorMax = originalAnchorMax;
        menuRootRect.pivot = originalPivot;
        menuRootRect.anchoredPosition = originalAnchoredPosition;
        isDockedCenterLeft = false;
    }

    private static bool IsNearRect(Rect rect, Vector3 screenPoint, float marginPixels)
    {
        Rect expanded = new Rect(
            rect.xMin - marginPixels,
            rect.yMin - marginPixels,
            rect.width + marginPixels * 2f,
            rect.height + marginPixels * 2f);
        return expanded.Contains(new Vector2(screenPoint.x, screenPoint.y));
    }

    private static Rect GetScreenRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return Rect.zero;

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            Vector3 c = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.x > maxX) maxX = c.x;
            if (c.y > maxY) maxY = c.y;
        }

        if (minX == float.MaxValue || minY == float.MaxValue || maxX == float.MinValue || maxY == float.MinValue)
            return Rect.zero;

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static bool WasPrimaryPointerPressedThisFrame(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif
        return false;
    }

    private static bool IsPointerOverMenuShortcut(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        PointerEventData pointerData = new PointerEventData(eventSystem) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);
        for (int i = 0; i < results.Count; i++)
        {
            GameObject target = results[i].gameObject;
            if (target != null && target.GetComponentInParent<MenuShortcutButton>() != null)
                return true;
        }

        return false;
    }

    private static Button FindButton(GameObject panel, string buttonName)
    {
        if (panel == null || string.IsNullOrWhiteSpace(buttonName))
            return null;

        Transform found = FindChildByName(panel.transform, buttonName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private static Button FindButtonByNames(GameObject panel, params string[] buttonNames)
    {
        if (panel == null || buttonNames == null)
            return null;

        for (int i = 0; i < buttonNames.Length; i++)
        {
            Button button = FindButton(panel, buttonNames[i]);
            if (button != null)
                return button;
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Scene active = SceneManager.GetActiveScene();
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null)
                continue;
            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded || t.gameObject.scene != active)
                continue;
            if (string.Equals(t.name, objectName, StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }

        return null;
    }

    private static T FindInActiveScene<T>() where T : Component
    {
        Scene active = SceneManager.GetActiveScene();
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            T candidate = all[i];
            if (candidate != null && candidate.gameObject.scene == active)
                return candidate;
        }

        return null;
    }

    private static bool WasUpPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
    }

    private static bool WasDownPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
    }

    private static bool WasLeftPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
    }

    private static bool WasRightPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
    }

    private static bool WasConfirmPressedThisFrame()
    {
        if (RemoteInput.ConfirmDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    // Cancelar (ESC/Backspace), pausa de simulacao (P) OU clique direito curto
    // (o CursorController trata direito=ESC). Durante a IA, P usa o player pause:
    // termina o batch atual e abre o menu somente no proximo ponto seguro.
    private bool WasCancelRequestedThisFrame()
    {
        return WasCancelShortcutPressedThisFrame() ||
               (cursorController != null && cursorController.WasRightClickCancelTapThisFrame);
    }

    private static bool WasCancelShortcutPressedThisFrame()
    {
        if (RemoteInput.CancelDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.escapeKey.wasPressedThisFrame ||
                   Keyboard.current.backspaceKey.wasPressedThisFrame ||
                   Keyboard.current.pKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.Escape) ||
               Input.GetKeyDown(KeyCode.Backspace) ||
               Input.GetKeyDown(KeyCode.P);
    }

    private static bool WasLetterPressedThisFrame(char letter)
    {
        char normalized = char.ToUpperInvariant(letter);
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (normalized == 'I') return Keyboard.current.iKey.wasPressedThisFrame;
            if (normalized == 'O') return Keyboard.current.oKey.wasPressedThisFrame;
        }
#endif
        if (normalized == 'I') return Input.GetKeyDown(KeyCode.I);
        if (normalized == 'O') return Input.GetKeyDown(KeyCode.O);
        return false;
    }

    private static bool IsAnyTextInputFocusedInUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null)
            {
                InputField legacy = selected.GetComponentInParent<InputField>();
                if (legacy != null && legacy.isFocused)
                    return true;

                TMP_InputField tmp = selected.GetComponentInParent<TMP_InputField>();
                if (tmp != null && tmp.isFocused)
                    return true;
            }
        }

        TMP_InputField[] tmpInputs = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < tmpInputs.Length; i++)
        {
            TMP_InputField field = tmpInputs[i];
            if (field != null && field.isActiveAndEnabled && field.isFocused)
                return true;
        }

        InputField[] legacyInputs = FindObjectsByType<InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < legacyInputs.Length; i++)
        {
            InputField field = legacyInputs[i];
            if (field != null && field.isActiveAndEnabled && field.isFocused)
                return true;
        }

        return false;
    }
}
