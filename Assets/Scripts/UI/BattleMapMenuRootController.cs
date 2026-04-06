using System;
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
        SaveLoad = 7,
        Gerenciar = 8,
        VoltarOptions = 9,
        Destruir = 10,
        Render = 11,
        Sair = 12,
        VoltarGerenciar = 13
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

    [SerializeField] private Button btnMinimapa;
    [SerializeField] private Button btnConfig;
    [SerializeField] private Button btnSaveLoad;
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
    private bool saveLoadPromptOpen;
    private bool exitConfirmOpen;
    private bool pendingOpenOnNextNeutral;
    private bool eventSystemNavStateCaptured;
    private bool previousSendNavigationEvents;
    private int lastConfirmSfxFrame = -1;
    private Vector3Int savedCursorCell;

    public bool IsMenuOpen => menuOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneInstance()
    {
        BattleMapMenuRootController existing = FindAnyObjectByType<BattleMapMenuRootController>();
        if (existing != null)
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

            if (!WasEscapePressedThisFrame())
                return false;

            if (IsAnyTextInputFocusedInUi())
                return false;

            // Se a confirmacao de fim de turno estiver pendente, ESC deve cancelar
            // essa confirmacao no CursorController (nao abrir o menu do jogador).
            if (cursorController != null && cursorController.IsEndTurnConfirmationPending)
                return false;

            if (!CanOpenMenuNow())
            {
                bool aiTurn = matchController != null && matchController.IsPlayerInputLockedByActiveAI();
                if (!aiTurn)
                    return false;

                pendingOpenOnNextNeutral = true;
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

        if (saveLoadPromptOpen)
        {
            if (WasEscapePressedThisFrame())
            {
                saveLoadPromptOpen = false;
                PlayCancelSfx();
                RestoreDefaultDialogForCurrentPanel();
                return true;
            }

            if (WasLetterPressedThisFrame('I'))
            {
                saveLoadPromptOpen = false;
                CloseMenu(restoreCursor: true);
                saveGameManager?.OpenSaveSlotPromptFromMenu();
                return true;
            }

            if (WasLetterPressedThisFrame('O'))
            {
                saveLoadPromptOpen = false;
                CloseMenu(restoreCursor: true);
                saveGameManager?.OpenLoadSlotPromptFromMenu();
                return true;
            }

            return true;
        }

        if (exitConfirmOpen)
        {
            if (WasEscapePressedThisFrame())
            {
                exitConfirmOpen = false;
                PlayCancelSfx();
                RestoreDefaultDialogForCurrentPanel();
                return true;
            }

            if (WasConfirmPressedThisFrame())
            {
                exitConfirmOpen = false;
                PlayConfirmSfxOncePerFrame();
                SceneManager.LoadScene(mainMenuSceneName);
                return true;
            }

            return true;
        }

        if (WasEscapePressedThisFrame())
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
        saveLoadPromptOpen = false;
        exitConfirmOpen = false;
        RestoreUndockedLayout();
        hasLastUndockedScreenRect = false;
        cursorNearUndockedDockRegion = false;
        CaptureAndDisableEventSystemNavigation();
        RefreshButtonInteractability();
        SetPanel(MenuPanel.Menu, resetIndex: true);
        PanelDialogController.ClearExternalText();
    }

    private void RefreshButtonInteractability()
    {
        bool isAiTurn = matchController != null && matchController.IsPlayerInputLockedByActiveAI();
        SetButtonInteractable(btnStatus,   !isAiTurn);
        SetButtonInteractable(btnComando,  !isAiTurn);
        SetButtonInteractable(btnRodada,   !isAiTurn);
        SetButtonInteractable(btnDestruir, !isAiTurn);
        SetButtonInteractable(btnRender,   !isAiTurn);
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void CloseMenu(bool restoreCursor)
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);

        menuOpen = false;
        pendingOpenOnNextNeutral = false;
        saveLoadPromptOpen = false;
        exitConfirmOpen = false;
        turnStateManager?.TryExitPlayerMenuStateToNeutral();
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
        if (menuRoot != null)
            menuRoot.SetActive(false);

        menuOpen = false;
        pendingOpenOnNextNeutral = false;
        saveLoadPromptOpen = false;
        exitConfirmOpen = false;
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
    }

    private void EnsureButtonsCache()
    {
        BindButton(btnStatus, MenuAction.Status);
        BindButton(btnComando, MenuAction.Comando);
        BindButton(btnRodada, MenuAction.Rodada);
        BindButton(btnOpcoes, MenuAction.Opcoes);
        BindButton(btnVoltarMenu, MenuAction.VoltarMenu);

        BindButton(btnMinimapa, MenuAction.Minimapa);
        BindButton(btnConfig, MenuAction.Config);
        BindButton(btnSaveLoad, MenuAction.SaveLoad);
        BindButton(btnGerenciar, MenuAction.Gerenciar);
        BindButton(btnVoltarOptions, MenuAction.VoltarOptions);

        BindButton(btnDestruir, MenuAction.Destruir);
        BindButton(btnRender, MenuAction.Render);
        BindButton(btnSair, MenuAction.Sair);
        BindButton(btnVoltarGerenciar, MenuAction.VoltarGerenciar);

        panelButtons.Clear();
        panelButtons[MenuPanel.Menu] = BuildPanelButtons(btnStatus, btnComando, btnRodada, btnOpcoes, btnVoltarMenu);
        panelButtons[MenuPanel.Options] = BuildPanelButtons(btnMinimapa, btnConfig, btnSaveLoad, btnGerenciar, btnVoltarOptions);
        panelButtons[MenuPanel.Gerenciar] = BuildPanelButtons(btnDestruir, btnRender, btnSair, btnVoltarGerenciar);

    }

    private List<Button> BuildPanelButtons(params Button[] source)
    {
        List<Button> list = new List<Button>();
        for (int i = 0; i < source.Length; i++)
        {
            Button button = source[i];
            if (button != null)
                list.Add(button);
        }

        return list;
    }

    private void BindButton(Button button, MenuAction action)
    {
        if (button == null)
            return;

        if (buttonActions.TryGetValue(button, out MenuAction existing) && existing == action)
            return;

        buttonActions[button] = action;
        button.onClick.AddListener(() => OnButtonClicked(button));
    }

    private void OnButtonClicked(Button button)
    {
        if (button == null || !buttonActions.TryGetValue(button, out MenuAction action))
            return;

        if (action != MenuAction.Minimapa && action != MenuAction.Rodada)
            PlayConfirmSfxOncePerFrame();

        switch (action)
        {
            case MenuAction.Status:
                ShowStatusSummary();
                break;
            case MenuAction.Comando:
                if (!TryCloseMenuForDispatchAndEnsureNeutral())
                    break;
                if (turnStateManager != null && !turnStateManager.TryOpenCommandServiceFromMenu(out string commandMessage))
                    PanelDialogController.TrySetTransientText(commandMessage, 2.4f);
                break;
            case MenuAction.Rodada:
                if (!TryCloseMenuForDispatchAndEnsureNeutral())
                    break;
                if (cursorController == null || !cursorController.TryOpenEndTurnConfirmationFromMenu())
                    cursorController?.PlayErrorSfx();
                break;
            case MenuAction.Opcoes:
                SetPanel(MenuPanel.Options, resetIndex: true);
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
            case MenuAction.SaveLoad:
                saveLoadPromptOpen = true;
                PanelDialogController.TrySetExternalText("Save/Load :: I: salvar | O: carregar | ESC: voltar");
                break;
            case MenuAction.Gerenciar:
                SetPanel(MenuPanel.Gerenciar, resetIndex: true);
                break;
            case MenuAction.VoltarOptions:
                SetPanel(MenuPanel.Menu, resetIndex: false);
                SetPanelSelectionByButton(btnOpcoes);
                break;
            case MenuAction.Destruir:
                if (!TryCloseMenuForDispatchAndEnsureNeutral())
                    break;
                if (turnStateManager != null && !turnStateManager.TryOpenDestroyUnitPromptFromMenu(out string destroyMessage))
                    PanelDialogController.TrySetTransientText(destroyMessage, 2.4f);
                break;
            case MenuAction.Render:
                PanelDialogController.TrySetTransientText("Render: em desenvolvimento.", 2.4f);
                break;
            case MenuAction.Sair:
                exitConfirmOpen = true;
                PanelDialogController.TrySetExternalText("Sair para tela principal?\nENTER: sim | ESC: nao");
                break;
            case MenuAction.VoltarGerenciar:
                SetPanel(MenuPanel.Options, resetIndex: false);
                SetPanelSelectionByButton(btnGerenciar);
                break;
        }
    }

    private void SetPanelSelectionByButton(Button target)
    {
        if (target == null || !panelButtons.TryGetValue(activePanel, out List<Button> list) || list == null || list.Count <= 0)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != target)
                continue;

            currentIndex = i;
            SelectCurrentButton();
            return;
        }
    }

    private void ShowStatusSummary()
    {
        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        int turnNumber = matchController != null ? matchController.CurrentTurn : 0;
        int money = matchController != null ? matchController.GetActualMoney(activeTeam) : 0;
        string message = $"Status da partida\nRodada: {turnNumber}\nTime ativo: {TeamUtils.GetName(activeTeam)}\nTesouro: ${Mathf.Max(0, money)}";
        PanelDialogController.TrySetExternalText(message + "\nESC: voltar");
    }

    private bool TryCloseMenuForDispatchAndEnsureNeutral()
    {
        CloseMenu(restoreCursor: true);
        if (turnStateManager == null)
            return true;

        if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral)
            return true;

        string message = $"Menu do jogador: estado nao normalizado para Neutral (atual: {turnStateManager.CurrentCursorState}).";
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

        if (btnStatus == null) btnStatus = FindButton(panelMenu, "btn_status");
        if (btnComando == null) btnComando = FindButton(panelMenu, "btn_comando");
        if (btnRodada == null) btnRodada = FindButton(panelMenu, "btn_rodada");
        if (btnOpcoes == null) btnOpcoes = FindButton(panelMenu, "btn_opcoes");
        if (btnVoltarMenu == null) btnVoltarMenu = FindButton(panelMenu, "btn_voltar");

        if (btnMinimapa == null) btnMinimapa = FindButton(panelOptions, "btn_minimapa");
        if (btnConfig == null) btnConfig = FindButton(panelOptions, "btn_config");
        if (btnSaveLoad == null) btnSaveLoad = FindButton(panelOptions, "btn_saveLoad");
        if (btnGerenciar == null) btnGerenciar = FindButton(panelOptions, "btn_gerenciar");
        if (btnVoltarOptions == null) btnVoltarOptions = FindButton(panelOptions, "btn_voltar");

        if (btnDestruir == null) btnDestruir = FindButton(panelGerenciar, "btn_destruir");
        if (btnRender == null) btnRender = FindButton(panelGerenciar, "btn_render");
        if (btnSair == null) btnSair = FindButton(panelGerenciar, "btn_sair");
        if (btnVoltarGerenciar == null) btnVoltarGerenciar = FindButton(panelGerenciar, "btn_voltar");

        if (cursorController == null) cursorController = FindInActiveScene<CursorController>();
        if (turnStateManager == null) turnStateManager = FindInActiveScene<TurnStateManager>();
        if (saveGameManager == null) saveGameManager = FindInActiveScene<SaveGameManager>();
        if (cameraController == null) cameraController = FindInActiveScene<CameraController>();
        if (matchController == null) matchController = FindInActiveScene<MatchController>();
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

    private static Button FindButton(GameObject panel, string buttonName)
    {
        if (panel == null || string.IsNullOrWhiteSpace(buttonName))
            return null;

        Transform found = FindChildByName(panel.transform, buttonName);
        return found != null ? found.GetComponent<Button>() : null;
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

    private static bool WasConfirmPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Escape);
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
