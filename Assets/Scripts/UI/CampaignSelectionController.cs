using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Selecao runtime do quadrante. A cena de autoria nunca e carregada: o mapa de
/// campanha e reconstruido como mosaico dos bakes guardados no MundoData.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class CampaignSelectionController : MonoBehaviour
{
    private sealed class QuadrantEntry
    {
        public BlocoData Bloco;
        public CampanhaData Campanha;
        public QuadranteData Quadrante;
    }

    private sealed class ConstructionPreviewEntry
    {
        public int QuadrantIndex;
        public SpriteRenderer Renderer;
        public Color BaseColor;
    }

    [Header("Data")]
    [SerializeField] private MundoData mundo;
    [SerializeField] private ConstructionDatabase constructionDatabase;
    [SerializeField] private StructureDatabase structureDatabase;
    [SerializeField] private UnitDatabase unitDatabase;
    [SerializeField] private string battleSceneName = "Batalha";

    [Header("Scene References")]
    [SerializeField] private Tilemap worldTilemap;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private MatchController matchController;
    [SerializeField] private AIController aiController;

    [Header("Quadrant Presentation")]
    [Tooltip("Multiplicador aplicado ao terreno dos quadrantes que nao estao em foco.")]
    [SerializeField] private Color unfocusedQuadrantTint = new Color(0.62f, 0.66f, 0.70f, 1f);
    [Tooltip("Brilho de um territorio conquistado quando ele nao esta em foco.")]
    [Range(0f, 1f)]
    [SerializeField] private float unfocusedWinnerBrightness = 0.82f;
    [Tooltip("Brilho das construcoes assadas em um quadrante ainda neutro e fora de foco.")]
    [Range(0f, 1f)]
    [SerializeField] private float unfocusedConstructionBrightness = 0.62f;

    private readonly List<QuadrantEntry> quadrants = new List<QuadrantEntry>();
    private readonly List<ConstructionPreviewEntry> constructionPreviews = new List<ConstructionPreviewEntry>();
    private Transform constructionPreviewRoot;
    private sealed class MapDetailPreview
    {
        public readonly HashSet<int> Quadrants = new HashSet<int>();
        public SpriteRenderer Renderer;
        public Tilemap Map;
        public Vector3Int Cell;
        public Color BaseColor;
    }

    private readonly List<MapDetailPreview> mapDetails = new List<MapDetailPreview>();
    private Transform roadPreviewRoot;
    private Transform unitPreviewRoot;
    private QuadrantEntry hovered;
    private QuadrantEntry pending;
    private int selectedQuadrantIndex = -1;
    private int confirmationFocusIndex;
    private bool confirmationOpen;
    private bool launching;
    private bool selectionInputArmed;
    private bool confirmationSubmitArmed;
    private int sceneOpenedFrame;
    private int confirmationOpenedFrame;
    private GameObject campaignMenuRoot;
    private GameObject campaignMenuPanel;
    private bool campaignMenuOpen;
    private GameObject lastCampaignMenuSelection;
    private SaveGameManager campaignSaveManager;
    private bool waitingForPersistence;
    private bool campaignStatusOpen;
    private AIDifficulty? loadedDifficulty;
    private string persistenceFeedback;
    private CampanhaManager campanhaManager;
    private Color appliedUnfocusedQuadrantTint;
    private float appliedUnfocusedWinnerBrightness = -1f;
    private float appliedUnfocusedConstructionBrightness = -1f;

    public bool IsConfirmationOpen => confirmationOpen;
    public bool IsCampaignMenuOpen => campaignMenuOpen;
    public int ConfirmationFocusIndex => confirmationFocusIndex;

    private void Awake()
    {
        campanhaManager = GetComponent<CampanhaManager>();
        if (campanhaManager == null)
            foreach (CampanhaManager manager in FindObjectsByType<CampanhaManager>(FindObjectsSortMode.None))
                if (manager.gameObject.scene == gameObject.scene)
                {
                    campanhaManager = manager;
                    break;
                }
        ResolveReferences();
        DisableGameplayFogPresentation();
        // O mosaico nasce antes do Awake do MatchController. Aplica o contrato
        // confirmado no menu antes de resolver as cores dos slots dos predios.
        matchController?.EnsurePartidaConfigApplied();
        BuildWorldMosaic();
    }

    private void Start()
    {
        ResolveReferences();
        InitializeCampaignMenu();
        if (cursorController != null)
            cursorController.enabled = false;
        FocusInitialQuadrant();
        RefreshHoveredQuadrant(force: true);

        // A cena pode ser carregada no mesmo frame do clique/Enter usado em INICIAR.
        // Antes de aceitar uma selecao, espera esse comando ser completamente solto.
        sceneOpenedFrame = Time.frameCount;
        selectionInputArmed = false;
        UiInputBlocker.SuppressGameplayInputForFrames(2);
    }

    private void Update()
    {
        if (launching)
            return;

        if (waitingForPersistence || SaveGameManager.IsAnyLoadInProgress ||
            (campaignSaveManager != null && campaignSaveManager.IsPersistencePromptActive))
        {
            UiInputBlocker.SuppressGameplayInputForFrames(1);
            if (!SaveGameManager.IsAnyLoadInProgress && campaignSaveManager != null &&
                !campaignSaveManager.IsPersistencePromptActive)
            {
                waitingForPersistence = false;
                RefreshHoveredQuadrant(force: true);
                // O prompt ja toca o som de cancelar, salvar ou carregar.
                SetCampaignMenuOpen(true, playSound: false);
                if (!string.IsNullOrEmpty(persistenceFeedback))
                {
                    PanelHelperController.TrySetExternalText(PanelMessage.Helper("helper.campaign.title"), persistenceFeedback);
                    persistenceFeedback = null;
                }
            }
            return;
        }
        if (campaignStatusOpen)
        {
            UiInputBlocker.SuppressGameplayInputForFrames(1);
            if (WasCancelPressedThisFrame() || RemoteInput.RightClickCancelDownThisFrame())
            {
                campaignStatusOpen = false;
                RefreshHoveredQuadrant(force: true);
                SetCampaignMenuOpen(true, playSound: false);
                cursorController?.PlayCancelSfx();
            }
            return;
        }

        if (!selectionInputArmed)
        {
            UiInputBlocker.SuppressGameplayInputForFrames(1);
            if (Time.frameCount <= sceneOpenedFrame + 1 || IsSubmitHeldNow())
                return;

            selectionInputArmed = true;
            return;
        }

        if (confirmationOpen)
        {
            // O painel de confirmacao tem prioridade sobre o cursor de gameplay.
            UiInputBlocker.SuppressGameplayInputForFrames(1);

            if (WasPreviousPressedThisFrame())
            {
                NavigateConfirmation(-1);
                return;
            }

            if (WasNextPressedThisFrame())
            {
                NavigateConfirmation(+1);
                return;
            }

            if (WasCancelPressedThisFrame() || RemoteInput.RightClickCancelDownThisFrame())
            {
                CancelConfirmation();
                return;
            }

            if (!confirmationSubmitArmed)
            {
                if (Time.frameCount > confirmationOpenedFrame && !IsSubmitHeldNow())
                    confirmationSubmitArmed = true;
                return;
            }

            if (WasSubmitPressedThisFrame())
                InvokeConfirmationOption(confirmationFocusIndex);

            return;
        }

        if (campaignMenuOpen)
        {
            UiInputBlocker.SuppressGameplayInputForFrames(1);
            if (WasCancelPressedThisFrame() || RemoteInput.RightClickCancelDownThisFrame())
                SetCampaignMenuOpen(false);
            // No celular nao ha ESC: toque fora do painel fecha, como o clique fora
            // no menu da batalha.
            else if (TryGetMapTapThisFrame(out Vector2 outsideTap) && IsTapOutsideCampaignMenu(outsideTap))
            {
                SetCampaignMenuOpen(false);
                cursorController?.PlayCancelSfx();
            }
            return;
        }

        if (WasCancelPressedThisFrame())
        {
            SetCampaignMenuOpen(true);
            return;
        }

        // Toque/clique no mapa: o primeiro seleciona, o segundo no mesmo quadrante
        // confirma (o mesmo par de toques do cursor da batalha). Antes o quadrante
        // so andava por setas, e no celular nao havia como escolher nada.
        if (TryGetMapTapThisFrame(out Vector2 tapScreen) && HandleMapTap(tapScreen))
            return;

        if (WasQuadrantDirectionPressedThisFrame(out Vector2 direction))
        {
            MoveQuadrantSelection(direction);
            return;
        }

        if (!WasSubmitPressedThisFrame())
            return;

        // Enter seleciona provisoriamente. Impede que o mesmo Enter vaze para o
        // TurnStateManager que existe na cena-base de Campanha.
        UiInputBlocker.SuppressGameplayInputForFrames(1);
        OpenConfirmation();
    }

    private void LateUpdate()
    {
        // Ajustes no Inspector tambem precisam repintar, mesmo com o menu aberto.
        if (appliedUnfocusedQuadrantTint != unfocusedQuadrantTint ||
            appliedUnfocusedWinnerBrightness != unfocusedWinnerBrightness ||
            appliedUnfocusedConstructionBrightness != unfocusedConstructionBrightness)
            RefreshQuadrantPresentation();

        // O EventSystem navega depois do Update deste controller (-10000).
        // Observa o foco efetivo para incluir teclado/controle e repeticao de seta.
        if (!campaignMenuOpen || campaignMenuPanel == null) return;
        var events = UnityEngine.EventSystems.EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected == null || !selected.transform.IsChildOf(campaignMenuPanel.transform)) return;
        var button = selected.GetComponent<UnityEngine.UI.Button>();
        if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()) return;
        if (selected == lastCampaignMenuSelection) return;
        lastCampaignMenuSelection = selected;
        cursorController?.PlayCursorMoveSfx();
    }

    private void InitializeCampaignMenu()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (!string.Equals(candidate.name, "MenuRoot", StringComparison.OrdinalIgnoreCase)) continue;
            campaignMenuRoot = candidate.gameObject;
            foreach (Transform child in candidate.GetComponentsInChildren<Transform>(true))
                if (string.Equals(child.name, "Panel_campanha", StringComparison.OrdinalIgnoreCase))
                    campaignMenuPanel = child.gameObject;
            break;
        }
        if (campaignMenuRoot == null || campaignMenuPanel == null)
        {
            Debug.LogWarning("[Campanha] MenuRoot ou Panel_campanha nao encontrado.", this);
            return;
        }
        campaignMenuRoot.SetActive(false);
        campaignSaveManager = FindAnyObjectByType<SaveGameManager>();
        foreach (var button in campaignMenuPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            string action = button.name.ToLowerInvariant();
            button.onClick.AddListener(() => InvokeCampaignMenuAction(action));
        }
    }

    private void InvokeCampaignMenuAction(string action)
    {
        if (!campaignMenuOpen || waitingForPersistence || SaveGameManager.IsAnyLoadInProgress) return;
        switch (action)
        {
            case "button_save":
            case "button_load":
                if (campaignSaveManager == null) { cursorController?.PlayErrorSfx(); return; }
                persistenceFeedback = null;
                SetCampaignMenuOpen(false, playSound: false);
                if (action == "button_save") campaignSaveManager.OpenSaveSlotPromptFromMenu();
                else campaignSaveManager.OpenLoadSlotPromptFromMenu();
                waitingForPersistence = campaignSaveManager.IsPersistencePromptActive;
                if (!waitingForPersistence) SetCampaignMenuOpen(true, playSound: false);
                break;
            case "button_situacao":
                SetCampaignMenuOpen(false, playSound: false);
                cursorController?.PlayConfirmSfx();
                campaignStatusOpen = true;
                int conquered = 0;
                for (int i = 0; i < quadrants.Count; i++)
                    if (TryGetQuadrantOwner(i, out _)) conquered++;
                PanelHelperController.TrySetExternalText(PanelMessage.Helper("helper.campaign.status_title"),
                    PanelMessage.Helper("helper.campaign.status", ("world", mundo.displayName), ("total", quadrants.Count), ("conquered", conquered)));
                break;
            case "button_minimapa":
                SetCampaignMenuOpen(false, playSound: false);
                // A camera toca o beep da alternancia do minimapa.
                FindAnyObjectByType<CameraController>()?.ToggleQuickZoomFromMenu();
                break;
            case "button_voltar":
                launching = true;
                PanelHelperController.ClearExternalText();
                SceneManager.LoadScene("Tela de Entrada");
                break;
        }
    }

    public CampaignSelectionSaveData CaptureSelectionForSave()
    {
        var ids = new List<int>(); var flips = new List<bool>(); var ai = new List<bool>();
        matchController.ExportPlayersState(ids, flips, ai, new List<int>(), new List<int>(), new List<int>(), new List<bool>());
        var data = new CampaignSelectionSaveData
        {
            mundoId = mundo.mundoId, teams = new TeamId[ids.Count], isAI = ai.ToArray(), flipX = flips.ToArray(),
            commandAutomatic = new bool[ids.Count], preset = matchController.GameSetup, difficulty = ResolveDifficulty()
        };
        for (int i = 0; i < ids.Count; i++)
        {
            data.teams[i] = (TeamId)ids[i];
            data.commandAutomatic[i] = matchController.IsPlayerCommandServiceAutomatic(PlayerSlotId.FromIndex(i));
        }
        if (selectedQuadrantIndex >= 0 && selectedQuadrantIndex < quadrants.Count)
        {
            var entry = quadrants[selectedQuadrantIndex];
            data.campanhaId = entry.Campanha.campanhaId;
            data.quadranteId = entry.Quadrante.quadranteId;
            data.quadranteSerial = entry.Quadrante.IdSerial;
        }
        return data;
    }

    public bool RestoreSelectionFromSave(SaveGameData save)
    {
        CampaignSelectionSaveData data = save.campaignSelection;
        if (data == null || mundo == null || data.mundoId != mundo.mundoId ||
            data.teams == null || data.teams.Length < 2 || data.teams.Length > 4)
        {
            Debug.LogWarning("[Campanha] Save sem contrato de seleção válido ou de outro mundo.", this);
            SetPersistenceFeedback(PanelMessage.Helper("helper.campaign.incompatible_save"));
            return false;
        }
        int focus = quadrants.FindIndex(entry => data.quadranteSerial > 0
            ? entry.Quadrante.IdSerial == data.quadranteSerial
            : entry.Campanha.campanhaId == data.campanhaId && entry.Quadrante.quadranteId == data.quadranteId);
        if (focus < 0)
        {
            SetPersistenceFeedback(PanelMessage.Helper("helper.campaign.missing_quadrant"));
            return false;
        }
        PartidaConfig.Set(data.teams.Length, data.teams, data.isAI, data.flipX, data.preset,
            data.commandAutomatic, gameObject.scene.name);
        matchController.EnsurePartidaConfigApplied();
        loadedDifficulty = data.difficulty;
        CampaignProgressStore.ImportSnapshot(save.campaignProgress);
        confirmationOpen = false;
        pending = null;
        BuildWorldMosaic();
        SelectQuadrant(focus, playMoveSfx: false, adjustCamera: false);
        return true;
    }

    public void SetPersistenceFeedback(string message)
    {
        persistenceFeedback = message;
        PanelHelperController.TrySetExternalText(PanelMessage.Helper("helper.campaign.title"), message);
    }

    public void RestoreFocusedQuadrantHelper()
    {
        persistenceFeedback = null;
        RefreshHoveredQuadrant(force: true);
    }

    public bool TryToggleCampaignMenuFromShortcut()
    {
        if (waitingForPersistence || SaveGameManager.IsAnyLoadInProgress ||
            (campaignSaveManager != null && campaignSaveManager.IsPersistencePromptActive)) return false;
        if (campaignStatusOpen)
        {
            campaignStatusOpen = false;
            RefreshHoveredQuadrant(force: true);
            SetCampaignMenuOpen(true, playSound: false);
            cursorController?.PlayCancelSfx();
            return true;
        }
        if (launching || !selectionInputArmed || campaignMenuRoot == null || campaignMenuPanel == null)
            return false;
        if (confirmationOpen)
        {
            CancelConfirmation();
            return true;
        }
        SetCampaignMenuOpen(!campaignMenuOpen);
        return true;
    }

    private void SetCampaignMenuOpen(bool open, bool playSound = true)
    {
        if (campaignMenuRoot == null || campaignMenuPanel == null) return;
        campaignMenuOpen = open;
        if (open)
        {
            foreach (Transform child in campaignMenuRoot.transform)
                child.gameObject.SetActive(child.gameObject == campaignMenuPanel);
            campaignMenuPanel.SetActive(true);
        }
        campaignMenuRoot.SetActive(open);
        if (!open)
            RestoreFocusedQuadrantHelper();
        lastCampaignMenuSelection = null;
        UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null)
        {
            if (open)
            {
                // A Campanha tambem pode iniciar diretamente pelo Editor, sem
                // passar pelos controllers de entrada que habilitam a UI.
                events.sendNavigationEvents = true;
#if ENABLE_INPUT_SYSTEM
                var module = events.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (module != null)
                {
                    if (module.actionsAsset == null)
                        module.AssignDefaultActions();
                    module.enabled = true;
                    module.move?.action?.Enable();
                    module.submit?.action?.Enable();
                    module.cancel?.action?.Enable();
                    module.point?.action?.Enable();
                    module.leftClick?.action?.Enable();
                    module.rightClick?.action?.Enable();
                    module.scrollWheel?.action?.Enable();
                }
#endif
            }
            var first = open ? campaignMenuPanel.GetComponentInChildren<UnityEngine.UI.Button>() : null;
            events.SetSelectedGameObject(first != null ? first.gameObject : null);
            lastCampaignMenuSelection = events.currentSelectedGameObject;
        }
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        BattleMapMenuRootController.SuppressMenuOpenForCurrentFrame();
        if (playSound)
        {
            if (open) cursorController?.PlayConfirmSfx();
            else cursorController?.PlayCancelSfx();
        }
    }

    public void NavigateConfirmation(int direction)
    {
        if (!confirmationOpen || direction == 0)
            return;

        confirmationFocusIndex = (confirmationFocusIndex + (direction > 0 ? 1 : -1) + 2) % 2;
        cursorController?.PlayCursorMoveSfx();
    }

    public void InvokeConfirmationOption(int index)
    {
        if (!confirmationOpen)
            return;

        confirmationFocusIndex = Mathf.Clamp(index, 0, 1);
        if (confirmationFocusIndex == 0)
        {
            // Tambem protege o callback do botao criado dinamicamente no PanelHelper:
            // um submit residual nunca pode comprometer a escolha provisoria.
            if (!confirmationSubmitArmed)
            {
                if (Time.frameCount <= confirmationOpenedFrame || IsSubmitHeldNow())
                    return;
                confirmationSubmitArmed = true;
            }

            LaunchSelectedQuadrant();
            return;
        }

        CancelConfirmation();
    }

    public string GetConfirmationSummary()
    {
        if (pending == null)
            return string.Empty;

        QuadranteData q = pending.Quadrante;
        int constructionCount = q.bakedConstrucoes != null ? q.bakedConstrucoes.Count : 0;
        return PanelMessage.Helper("helper.campaign.confirm", ("block", pending.Bloco.displayName), ("campaign", pending.Campanha.displayName), ("quadrant", q.displayName), ("width", q.width), ("height", q.height), ("buildings", constructionCount), ("bake", PanelMessage.Helper(q.HasBake ? "helper.campaign.bake_ready" : "helper.campaign.bake_unavailable")), ("development", q.emDesenvolvimento ? PanelMessage.Helper("helper.campaign.development_suffix") : string.Empty));
    }

    private void OpenConfirmation()
    {
        RefreshHoveredQuadrant(force: false);
        if (hovered == null)
            return;

        pending = hovered;
        confirmationOpen = true;
        confirmationFocusIndex = 0;
        confirmationSubmitArmed = false;
        confirmationOpenedFrame = Time.frameCount;
        cursorController?.PlayConfirmSfx();
        PanelHelperController.TrySetExternalText(PanelMessage.Helper("helper.campaign.confirm_title"), string.Empty);
    }

    private void CancelConfirmation()
    {
        if (!confirmationOpen)
            return;

        confirmationOpen = false;
        confirmationFocusIndex = 0;
        confirmationSubmitArmed = false;
        pending = null;
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        cursorController?.PlayCancelSfx();
        RefreshHoveredQuadrant(force: true);
    }

    private void LaunchSelectedQuadrant()
    {
        if (pending == null || launching)
            return;

        if (!pending.Quadrante.HasBake)
        {
            PanelHelperController.TrySetExternalText(
                PanelMessage.Helper("helper.campaign.unavailable_title"),
                PanelMessage.Helper("helper.campaign.no_bake"));
            cursorController?.PlayCancelSfx();
            confirmationOpen = false;
            pending = null;
            return;
        }

        // Em desenvolvimento: selecionavel, mas nao abre. Marcado na bancada
        // (Tools > Utils > Map Helper), enquanto o autor monta o quadrante.
        if (pending.Quadrante.emDesenvolvimento)
        {
            PanelHelperController.TrySetExternalText(
                PanelMessage.Helper("helper.campaign.development_title"),
                PanelMessage.Helper("helper.campaign.development_body"));
            cursorController?.PlayErrorSfx();
            confirmationOpen = false;
            pending = null;
            return;
        }

        ResolveReferences();
        if (matchController == null)
        {
            PanelHelperController.TrySetExternalText(
                PanelMessage.Helper("helper.campaign.error_title"),
                PanelMessage.Helper("helper.campaign.missing_controller"));
            cursorController?.PlayCancelSfx();
            return;
        }

        List<int> teamIds = new List<int>();
        List<bool> flipXs = new List<bool>();
        List<bool> isAIs = new List<bool>();
        List<int> startMoneys = new List<int>();
        List<int> actualMoneys = new List<int>();
        List<int> incomePerTurns = new List<int>();
        List<bool> startMoneyApplied = new List<bool>();
        matchController.ExportPlayersState(
            teamIds,
            flipXs,
            isAIs,
            startMoneys,
            actualMoneys,
            incomePerTurns,
            startMoneyApplied);

        if (teamIds.Count < 2)
        {
            PanelHelperController.TrySetExternalText(
                PanelMessage.Helper("helper.campaign.error_title"),
                PanelMessage.Helper("helper.campaign.missing_players"));
            cursorController?.PlayCancelSfx();
            return;
        }

        TeamId[] teams = new TeamId[teamIds.Count];
        bool[] commandsAutomatic = new bool[teamIds.Count];
        for (int i = 0; i < teamIds.Count; i++)
        {
            teams[i] = (TeamId)teamIds[i];
            commandsAutomatic[i] = matchController.IsPlayerCommandServiceAutomatic(PlayerSlotId.FromIndex(i));
        }

        PartidaConfig.Set(
            teamIds.Count,
            teams,
            isAIs.ToArray(),
            flipXs.ToArray(),
            matchController.GameSetup,
            commandsAutomatic,
            battleSceneName);
        PartidaConfig.SetDifficulty(ResolveDifficulty());
        PartidaConfig.SetQuadrante(pending.Campanha.campanhaId, pending.Quadrante.quadranteId);

        launching = true;
        PanelHelperController.ClearExternalText();
        Debug.Log(
            $"[Campanha] JOGAR confirmado para '{pending.Campanha.campanhaId}/{pending.Quadrante.quadranteId}'. Abrindo '{battleSceneName}'.",
            this);
        StartCoroutine(LoadBattleAfterDoneSfx());
    }

    // O mesmo fecho do contrato da Tela de Entrada (PanelMenu.StartConfiguredNewGameAfterSfx):
    // toca o "done" e so troca de cena quando ele termina — o AudioSource mora no cursor
    // desta cena, e um LoadScene imediato cortaria o som. `launching` ja bloqueia input e
    // reentrada durante a espera.
    private System.Collections.IEnumerator LoadBattleAfterDoneSfx()
    {
        cursorController?.PlayDoneSfx();
        float soundDuration = cursorController != null ? cursorController.GetDoneSfxDuration() : 0f;
        // Tempo real: a cena de campanha pode estar com o tempo de jogo pausado.
        if (soundDuration > 0f)
            yield return new WaitForSecondsRealtime(soundDuration);

        SceneManager.LoadScene(battleSceneName);
    }

    private AIDifficulty ResolveDifficulty()
    {
        if (loadedDifficulty.HasValue) return loadedDifficulty.Value;
        if (aiController != null)
            return aiController.AppliedDifficulty;

        switch (matchController.GameSetup)
        {
            case MatchController.GameSetupPreset.GameBoyClassic:
                return AIDifficulty.Facil;
            case MatchController.GameSetupPreset.NeblinaLeve:
                return AIDifficulty.Medio;
            default:
                return AIDifficulty.Dificil;
        }
    }

    private void ResolveReferences()
    {
        if (worldTilemap == null)
        {
            Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null && string.Equals(maps[i].name, "TileMap", StringComparison.OrdinalIgnoreCase))
                {
                    worldTilemap = maps[i];
                    break;
                }
            }
        }

        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (aiController == null)
            aiController = FindAnyObjectByType<AIController>();

        if (constructionDatabase == null)
        {
            ConstructionSpawner[] spawners =
                FindObjectsByType<ConstructionSpawner>(FindObjectsInactive.Include);
            for (int i = 0; i < spawners.Length; i++)
            {
                if (spawners[i] != null && spawners[i].ConstructionDatabase != null)
                {
                    constructionDatabase = spawners[i].ConstructionDatabase;
                    break;
                }
            }
        }
    }

    private void BuildWorldMosaic()
    {
        quadrants.Clear();
        if (mundo == null || worldTilemap == null)
        {
            Debug.LogError("[Campanha] MundoData ou TileMap não configurado.", this);
            return;
        }

        if (mundo.blocos != null)
        {
            for (int b = 0; b < mundo.blocos.Count; b++)
            {
                BlocoData bloco = mundo.blocos[b];
                if (bloco?.campanhas == null)
                    continue;

                for (int c = 0; c < bloco.campanhas.Count; c++)
                {
                    CampanhaData campanha = bloco.campanhas[c];
                    if (campanha?.quadrantes == null)
                        continue;

                    for (int q = 0; q < campanha.quadrantes.Count; q++)
                    {
                        QuadranteData quadrante = campanha.quadrantes[q];
                        if (quadrante == null)
                            continue;

                        quadrants.Add(new QuadrantEntry
                        {
                            Bloco = bloco,
                            Campanha = campanha,
                            Quadrante = quadrante
                        });
                    }
                }
            }
        }

        quadrants.Sort((left, right) =>
            string.CompareOrdinal(left.Quadrante.quadranteId, right.Quadrante.quadranteId));

        worldTilemap.ClearAllTiles();
        int painted = 0;
        for (int i = 0; i < quadrants.Count; i++)
        {
            QuadranteData q = quadrants[i].Quadrante;
            if (!q.HasBake)
                continue;

            for (int localY = 0; localY < q.height; localY++)
            {
                for (int localX = 0; localX < q.width; localX++)
                {
                    TileBase tile = q.GetBakedTile(localX, localY);
                    if (tile == null)
                        continue;

                    worldTilemap.SetTile(
                        new Vector3Int(q.originX + localX, q.originY + localY, 0),
                        tile);
                    painted++;
                }
            }
        }

        int constructionPreviews = BuildConstructionPreviews();
        BuildMapDetails(out int decorationCount, out int roadSegmentCount);
        int unitCount = BuildUnitPreviews();
        RefreshQuadrantPresentation();
        worldTilemap.CompressBounds();
        FrameWorldInCamera();
        Debug.Log(
            $"[Campanha] Mosaico '{mundo.displayName}' construído: {quadrants.Count} quadrantes, " +
            $"{painted} tiles, {constructionPreviews} construções visuais, " +
            $"{decorationCount} enfeites, {roadSegmentCount} segmentos de estrada, {unitCount} unidades visuais.",
            this);
    }

    // Apenas apresentacao: nao registra rotas no RoadNetworkManager nem altera o bake.
    private void BuildMapDetails(out int decorationCount, out int roadSegmentCount)
    {
        foreach (MapDetailPreview detail in mapDetails)
            if (detail.Map != null) detail.Map.SetTile(detail.Cell, null);
        mapDetails.Clear();
        if (roadPreviewRoot != null)
        {
            roadPreviewRoot.gameObject.SetActive(false);
            Destroy(roadPreviewRoot.gameObject);
        }
        roadPreviewRoot = new GameObject("Campaign Road Previews").transform;
        roadPreviewRoot.SetParent(transform, false);
        decorationCount = roadSegmentCount = 0;

        var layers = new Dictionary<string, Tilemap>(StringComparer.Ordinal);
        Transform grid = worldTilemap.layoutGrid != null ? worldTilemap.layoutGrid.transform : worldTilemap.transform.parent;
        foreach (Tilemap map in grid.GetComponentsInChildren<Tilemap>(true))
            if (map != worldTilemap) layers.TryAdd(map.name, map);
        var clearedLayers = new HashSet<Tilemap>();
        var marks = new Dictionary<(Tilemap, Vector3Int), MapDetailPreview>();
        var edges = new Dictionary<(string, Vector3Int, Vector3Int), MapDetailPreview>();
        var missingStructures = new HashSet<string>();

        for (int i = 0; i < quadrants.Count; i++)
        {
            QuadranteData q = quadrants[i].Quadrante;
            if (!q.HasBake) continue;
            if (q.bakedCamadas != null)
            foreach (CamadaAssada layer in q.bakedCamadas)
            {
                if (layer == null || string.IsNullOrWhiteSpace(layer.tilemapName) || layer.marcas == null) continue;
                if (!layers.TryGetValue(layer.tilemapName, out Tilemap map))
                {
                    var go = new GameObject(layer.tilemapName, typeof(Tilemap), typeof(TilemapRenderer));
                    go.transform.SetParent(grid, false);
                    map = go.GetComponent<Tilemap>();
                    map.tileAnchor = worldTilemap.tileAnchor;
                    go.GetComponent<TilemapRenderer>().sortingLayerName = "Estruturas";
                    layers.Add(layer.tilemapName, map);
                }
                if (clearedLayers.Add(map)) map.ClearAllTiles();
                map.gameObject.SetActive(true);
                foreach (CamadaAssada.Marca mark in layer.marcas)
                {
                    if (mark.tile == null || !IsInsideQuadrant(q, mark.localX, mark.localY)) continue;
                    Vector3Int cell = new Vector3Int(q.originX + mark.localX, q.originY + mark.localY, 0);
                    if (marks.TryGetValue((map, cell), out MapDetailPreview shared))
                    {
                        shared.Quadrants.Add(i);
                        continue;
                    }
                    map.SetTile(cell, mark.tile);
                    map.SetTileFlags(cell, TileFlags.None);
                    map.SetTransformMatrix(cell, layer.GetMatriz(mark.transformIndex));
                    map.SetColor(cell, mark.cor);
                    var detail = new MapDetailPreview { Map = map, Cell = cell, BaseColor = mark.cor };
                    detail.Quadrants.Add(i);
                    marks.Add((map, cell), detail);
                    mapDetails.Add(detail);
                    decorationCount++;
                }
            }

            if (q.bakedRotas == null) continue;
            foreach (RotaAssada route in q.bakedRotas)
            {
                if (route?.celulas == null || route.celulas.Count < 2) continue;
                if (structureDatabase == null ||
                    !structureDatabase.TryGetById(route.structureId, out StructureData structure) ||
                    structure == null || structure.roadSegmentSprite == null)
                {
                    if (missingStructures.Add(route.structureId ?? string.Empty))
                        Debug.LogWarning($"[Campanha] Estrada '{route.structureId}' sem catalogo ou sprite visual.", this);
                    continue;
                }
                for (int c = 0; c < route.celulas.Count - 1; c++)
                {
                    Vector3Int a = route.celulas[c], b = route.celulas[c + 1];
                    // Nunca filtra e cola a sequencia: um par invalido e simplesmente ignorado.
                    if (!IsInsideQuadrant(q, a.x, a.y) || !IsInsideQuadrant(q, b.x, b.y)) continue;
                    a = new Vector3Int(q.originX + a.x, q.originY + a.y, 0);
                    b = new Vector3Int(q.originX + b.x, q.originY + b.y, 0);
                    if (a == b || !worldTilemap.HasTile(a) || !worldTilemap.HasTile(b)) continue;
                    var key = a.x < b.x || (a.x == b.x && a.y < b.y)
                        ? (route.structureId, a, b) : (route.structureId, b, a);
                    if (edges.TryGetValue(key, out MapDetailPreview shared))
                    {
                        shared.Quadrants.Add(i);
                        continue;
                    }
                    Vector3 from = worldTilemap.GetCellCenterWorld(a), to = worldTilemap.GetCellCenterWorld(b);
                    Vector3 delta = to - from;
                    var go = new GameObject($"{route.structureId} {a} - {b}");
                    go.transform.SetParent(roadPreviewRoot, false);
                    go.transform.position = (from + to) * 0.5f;
                    go.transform.rotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, delta));
                    SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = structure.roadSegmentSprite;
                    renderer.color = structure.roadColor;
                    renderer.sortingLayerName = "Estruturas";
                    renderer.sortingOrder = 20;
                    Vector3 size = renderer.sprite.bounds.size;
                    go.transform.localScale = new Vector3(
                        Mathf.Clamp(structure.roadWidth, 0.03f, 0.6f) / Mathf.Max(0.0001f, size.x),
                        (delta.magnitude + Mathf.Clamp(structure.segmentOverlap, 0f, 0.3f)) / Mathf.Max(0.0001f, size.y), 1);
                    var detail = new MapDetailPreview { Renderer = renderer, BaseColor = structure.roadColor };
                    detail.Quadrants.Add(i);
                    edges.Add(key, detail);
                    mapDetails.Add(detail);
                    roadSegmentCount++;
                }
            }
        }
    }

    private static bool IsInsideQuadrant(QuadranteData q, int x, int y)
        => x >= 0 && y >= 0 && x < q.width && y < q.height;

    /// <summary>
    /// Representa as construcoes assadas no mapa de selecao sem instanciar o
    /// prefab jogavel. Estes objetos possuem apenas SpriteRenderer: nao entram em
    /// captura, renda, ocupacao, FOW, IA nem em qualquer cache da partida.
    /// </summary>
    private int BuildConstructionPreviews()
    {
        ResetConstructionPreviewRoot();

        if (constructionDatabase == null)
        {
            Debug.LogWarning(
                "[Campanha] ConstructionDatabase nao configurado; o mosaico sera exibido sem construcoes.",
                this);
            return 0;
        }

        var occupiedCells = new HashSet<Vector3Int>();
        var missingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int built = 0;

        for (int i = 0; i < quadrants.Count; i++)
        {
            QuadranteData q = quadrants[i].Quadrante;
            if (!q.HasBake || q.bakedConstrucoes == null)
                continue;

            for (int c = 0; c < q.bakedConstrucoes.Count; c++)
            {
                ConstrucaoAssada baked = q.bakedConstrucoes[c];
                if (baked == null || string.IsNullOrWhiteSpace(baked.constructionId))
                    continue;

                if (baked.localX < 0 || baked.localX >= q.width ||
                    baked.localY < 0 || baked.localY >= q.height)
                {
                    Debug.LogWarning(
                        $"[Campanha] Construcao assada '{baked}' fora do retangulo de '{q.quadranteId}'. " +
                        "Ela nao sera exibida; asse o quadrante novamente.",
                        this);
                    continue;
                }

                Vector3Int globalCell = new Vector3Int(
                    q.originX + baked.localX,
                    q.originY + baked.localY,
                    0);

                if (!worldTilemap.HasTile(globalCell))
                {
                    Debug.LogWarning(
                        $"[Campanha] Construcao '{baked.constructionId}' em {globalCell} nao possui terreno " +
                        "no mosaico e nao sera exibida.",
                        this);
                    continue;
                }

                if (!constructionDatabase.TryGetById(baked.constructionId, out ConstructionData data))
                {
                    if (missingIds.Add(baked.constructionId))
                    {
                        Debug.LogWarning(
                            $"[Campanha] Construcao '{baked.constructionId}' nao existe no catalogo visual.",
                            this);
                    }
                    continue;
                }

                // O slot define o dono; a cor do bake so vale para conteudo sem slot.
                TeamId ownerTeam = baked.slotIndex >= 0 && matchController != null
                    ? matchController.GetTeamIdForSlot(baked.slotIndex)
                    : baked.teamId;
                Sprite sprite = TeamUtils.GetTeamSprite(data, ownerTeam);
                if (sprite == null)
                {
                    if (missingIds.Add(baked.constructionId + "#sprite"))
                    {
                        Debug.LogWarning(
                            $"[Campanha] Construcao '{baked.constructionId}' nao possui sprite para exibir.",
                            data);
                    }
                    continue;
                }

                // Quadrantes podem se sobrepor na autoria. No mosaico global uma
                // mesma celula continua comportando uma unica construcao visual.
                // So reserva a celula depois que id e sprite realmente resolveram.
                if (!occupiedCells.Add(globalCell))
                {
                    Debug.LogWarning(
                        $"[Campanha] Mais de uma construcao assada ocupa {globalCell}; " +
                        $"mantendo a primeira e ignorando '{baked.constructionId}'.",
                        this);
                    continue;
                }

                var preview = new GameObject($"{baked.constructionId} @ {globalCell.x},{globalCell.y}");
                preview.transform.SetParent(constructionPreviewRoot, worldPositionStays: false);
                preview.transform.position = worldTilemap.GetCellCenterWorld(globalCell);

                SpriteRenderer renderer = preview.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                Color baseColor = TeamUtils.GetColor(ownerTeam);
                renderer.color = baseColor;
                renderer.flipX = TeamUtils.ShouldFlipX(ownerTeam);
                renderer.sortingLayerName = "Construcao";
                renderer.sortingOrder = 5;
                constructionPreviews.Add(new ConstructionPreviewEntry
                {
                    QuadrantIndex = i,
                    Renderer = renderer,
                    BaseColor = baseColor
                });
                built++;
            }
        }

        return built;
    }

    private int BuildUnitPreviews()
    {
        if (unitPreviewRoot != null)
        {
            unitPreviewRoot.gameObject.SetActive(false);
            Destroy(unitPreviewRoot.gameObject);
        }
        unitPreviewRoot = new GameObject("Campaign Unit Previews").transform;
        unitPreviewRoot.SetParent(constructionPreviewRoot.parent, false);
        int built = 0;
        var previews = new Dictionary<string, MapDetailPreview>();
        for (int i = 0; i < quadrants.Count; i++)
        {
            QuadranteData q = quadrants[i].Quadrante;
            if (!q.HasBake || q.bakedUnidades == null) continue;
            foreach (UnidadeAssada baked in q.bakedUnidades)
            {
                if (baked == null) continue;
                if (baked.localX < 0 || baked.localX >= q.width ||
                    baked.localY < 0 || baked.localY >= q.height) continue;
                Vector3Int cell = new Vector3Int(q.originX + baked.localX, q.originY + baked.localY, 0);
                if (!worldTilemap.HasTile(cell)) continue;
                if (unitDatabase == null || !unitDatabase.TryGetById(baked.unitId, out UnitData data))
                {
                    Debug.LogWarning($"[Campanha] Unidade '{baked.unitId}' sem catalogo visual.", this);
                    continue;
                }
                TeamId team = baked.slotIndex >= 0 && matchController != null
                    ? matchController.GetTeamIdForSlot(baked.slotIndex) : baked.teamId;
                Sprite sprite = TeamUtils.GetTeamSprite(data, team);
                if (sprite == null)
                {
                    Debug.LogWarning($"[Campanha] Unidade '{baked.unitId}' sem sprite.", data);
                    continue;
                }
                string key = $"{cell}:{baked.unitId}:{baked.slotIndex}:{team}";
                if (previews.TryGetValue(key, out MapDetailPreview existing))
                {
                    existing.Quadrants.Add(i);
                    continue;
                }
                // Somente imagem: nao instancia UnitManager nem registra ocupacao/sensores.
                var visual = new GameObject($"{baked.unitId} @ {cell.x},{cell.y}");
                visual.transform.SetParent(unitPreviewRoot, false);
                visual.transform.position = worldTilemap.GetCellCenterWorld(cell);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = TeamUtils.GetColor(team);
                renderer.flipX = matchController != null
                    ? matchController.GetTeamFlipX(team) : TeamUtils.ShouldFlipX(team);
                renderer.sortingLayerName = "Unidade";
                renderer.sortingOrder = 10;
                var preview = new MapDetailPreview { Renderer = renderer, BaseColor = renderer.color };
                preview.Quadrants.Add(i);
                previews.Add(key, preview);
                mapDetails.Add(preview);
                built++;
            }
        }
        return built;
    }

    private void ResetConstructionPreviewRoot()
    {
        constructionPreviews.Clear();

        if (constructionPreviewRoot != null)
        {
            constructionPreviewRoot.gameObject.SetActive(false);
            Destroy(constructionPreviewRoot.gameObject);
        }

        var root = new GameObject("Campaign Construction Previews");
        Transform parent = worldTilemap != null && worldTilemap.layoutGrid != null
            ? worldTilemap.layoutGrid.transform
            : transform;
        root.transform.SetParent(parent, worldPositionStays: false);
        constructionPreviewRoot = root.transform;
    }

    private void FocusInitialQuadrant()
    {
        if (quadrants.Count <= 0)
            return;

        SelectQuadrant(0, playMoveSfx: false, adjustCamera: false);
    }

    private void MoveQuadrantSelection(Vector2 direction)
    {
        if (quadrants.Count <= 0)
            return;

        if (selectedQuadrantIndex < 0 || selectedQuadrantIndex >= quadrants.Count)
        {
            SelectQuadrant(0, playMoveSfx: true, adjustCamera: true);
            return;
        }

        direction = direction.normalized;
        Vector2 origin = GetQuadrantCenter(quadrants[selectedQuadrantIndex].Quadrante);
        int bestIndex = -1;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < quadrants.Count; i++)
        {
            if (i == selectedQuadrantIndex)
                continue;

            Vector2 delta = GetQuadrantCenter(quadrants[i].Quadrante) - origin;
            float forward = Vector2.Dot(delta, direction);
            if (forward <= 0.01f)
                continue;

            float lateral = Mathf.Abs(direction.x * delta.y - direction.y * delta.x);
            float score = lateral * 4f + forward;
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        // Sem vizinho nessa direcao: o cursor permanece no limite do mapa de quadrantes.
        if (bestIndex >= 0)
            SelectQuadrant(bestIndex, playMoveSfx: true, adjustCamera: true);
    }

    private void SelectQuadrant(int index, bool playMoveSfx, bool adjustCamera)
    {
        if (index < 0 || index >= quadrants.Count)
            return;

        selectedQuadrantIndex = index;
        hovered = quadrants[index];
        RefreshQuadrantPresentation();
        PositionCursorOnQuadrant(hovered.Quadrante, adjustCamera);
        if (playMoveSfx)
            cursorController?.PlayCursorMoveSfx();
        RefreshHoveredQuadrant(force: true);
    }

    /// <summary>
    /// O retangulo assado e a propria mascara do quadrante: foco e dominio sao
    /// apenas cor de apresentacao e nunca alteram o MundoData nem o bake.
    /// </summary>
    private void RefreshQuadrantPresentation()
    {
        if (worldTilemap == null)
            return;

        appliedUnfocusedQuadrantTint = unfocusedQuadrantTint;
        appliedUnfocusedWinnerBrightness = unfocusedWinnerBrightness;
        appliedUnfocusedConstructionBrightness = unfocusedConstructionBrightness;

        for (int i = 0; i < quadrants.Count; i++)
        {
            QuadranteData q = quadrants[i].Quadrante;
            if (q == null || !q.HasBake)
                continue;

            Color tint = ResolveQuadrantTerrainTint(i);
            for (int localY = 0; localY < q.height; localY++)
            {
                for (int localX = 0; localX < q.width; localX++)
                {
                    Vector3Int cell = new Vector3Int(q.originX + localX, q.originY + localY, 0);
                    if (!worldTilemap.HasTile(cell))
                        continue;

                    // Tiles de autoria podem vir com LockColor. O tint desta cena
                    // e runtime e precisa ser livre sem tocar no asset compartilhado.
                    worldTilemap.SetTileFlags(cell, TileFlags.None);
                    worldTilemap.SetColor(cell, tint);
                }
            }
        }

        for (int i = 0; i < constructionPreviews.Count; i++)
        {
            ConstructionPreviewEntry preview = constructionPreviews[i];
            if (preview?.Renderer == null)
                continue;

            bool focused = preview.QuadrantIndex == selectedQuadrantIndex;
            bool hasOwner = TryGetQuadrantOwner(preview.QuadrantIndex, out _);
            float brightness = focused
                ? 1f
                : hasOwner ? unfocusedWinnerBrightness : unfocusedConstructionBrightness;
            preview.Renderer.color = ScaleRgb(preview.BaseColor, brightness);
        }

        foreach (MapDetailPreview detail in mapDetails)
        {
            Color color = detail.Quadrants.Contains(selectedQuadrantIndex)
                ? detail.BaseColor : ScaleRgb(detail.BaseColor, unfocusedConstructionBrightness);
            if (detail.Renderer != null) detail.Renderer.color = color;
            if (detail.Map != null) detail.Map.SetColor(detail.Cell, color);
        }
    }

    public void RefreshCampaignProgressPresentation() => RefreshQuadrantPresentation();

    /// <summary>
    /// Um dono por quadrante, na MESMA ordem e sobre a MESMA lista que o
    /// GetWonSectorCounts percorre. Slot invalido = ninguem tomou ainda.
    ///
    /// Compartilhar a lista nao e detalhe: os quadradinhos e o contador mostram a
    /// mesma coisa de dois jeitos, e se lessem fontes diferentes poderiam
    /// discordar na tela sem ninguem entender por que.
    ///
    /// Reusa a lista recebida para nao alocar a cada refresh.
    /// </summary>
    public void GetQuadrantOwners(List<PlayerSlotId> destino)
    {
        if (destino == null)
            return;

        destino.Clear();
        if (mundo == null)
            return;

        foreach (QuadrantEntry entry in quadrants)
        {
            destino.Add(
                CampaignProgressStore.TryGetOwner(
                    mundo.mundoId,
                    entry.Campanha.campanhaId,
                    entry.Quadrante.quadranteId,
                    out PlayerSlotId owner)
                    ? owner
                    : PlayerSlotId.Invalid);
        }
    }

    public void GetWonSectorCounts(out int slot0, out int slot1, out int total)
    {
        slot0 = slot1 = 0;
        total = quadrants.Count;
        if (mundo == null) return;
        foreach (QuadrantEntry entry in quadrants)
        {
            if (!CampaignProgressStore.TryGetOwner(mundo.mundoId,
                entry.Campanha.campanhaId, entry.Quadrante.quadranteId, out PlayerSlotId owner))
                continue;
            if (owner.Value == 0) slot0++;
            else if (owner.Value == 1) slot1++;
        }
    }

    private Color ResolveQuadrantTerrainTint(int index)
    {
        bool focused = index == selectedQuadrantIndex;
        if (!TryGetQuadrantOwner(index, out TeamId owner))
            return focused ? Color.white : unfocusedQuadrantTint;

        float strength = campanhaManager != null ? campanhaManager.WinnerTintStrength : 0.7f;
        Color winnerTint = Color.Lerp(Color.white, TeamUtils.GetColor(owner), strength);
        return focused ? winnerTint : ScaleRgb(winnerTint, unfocusedWinnerBrightness);
    }

    /// <summary>
    /// O progresso guarda o SLOT que tomou o quadrante; a cor sai do slot agora,
    /// contra as cores DESTA sessao. Quem venceu como Amarelo ontem e joga de
    /// Vermelho hoje ve o seu quadrante em vermelho — que e o certo: a cor
    /// responde "quem sou eu nesta partida", nao "que cor eu era naquela".
    /// </summary>
    private bool TryGetQuadrantOwner(int index, out TeamId owner)
    {
        owner = TeamId.Neutral;
        if (mundo == null || index < 0 || index >= quadrants.Count)
            return false;

        QuadrantEntry entry = quadrants[index];
        if (!CampaignProgressStore.TryGetOwner(
                mundo.mundoId,
                entry.Campanha.campanhaId,
                entry.Quadrante.quadranteId,
                out PlayerSlotId ownerSlot))
        {
            return false;
        }

        if (matchController == null)
            return false;

        owner = matchController.GetTeamIdForSlot(ownerSlot.Value);
        return owner != TeamId.Neutral;
    }

    private static Color ScaleRgb(Color color, float scale)
    {
        scale = Mathf.Clamp01(scale);
        return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
    }

    private void PositionCursorOnQuadrant(QuadranteData q, bool adjustCamera)
    {
        if (q == null || cursorController == null || worldTilemap == null)
            return;

        if (TryFindQuadrantFocusCell(q, out Vector3Int cell) &&
            cursorController.SetCell(cell, playMoveSfx: false, adjustCamera: adjustCamera))
            return;

        // Quadrantes sem bake continuam selecionaveis e aparecem como indisponiveis.
        // Como nao existe tile valido para SetCell, posiciona apenas a apresentacao.
        Vector3Int centerCell = new Vector3Int(q.originX + q.width / 2, q.originY + q.height / 2, 0);
        cursorController.transform.position = worldTilemap.GetCellCenterWorld(centerCell);
        if (adjustCamera)
            cursorController.TryAdjustCameraToCursor();
    }

    // --- Toque e clique no mapa ------------------------------------------------
    // Mesma regra do CursorController (WasPrimaryTouchTapReleasedThisFrame): no
    // toque, vale o RELEASE sem arrasto e com um dedo so; um press imediato
    // confundiria toque com arrasto.
    private const float MapTapMaxTravelPixels = 24f;
    // Navegador de celular gera um clique de mouse sintetico depois do toque. Sem
    // esta janela, um toque so selecionava E confirmava o quadrante.
    private const float MouseIgnoreAfterTouchSeconds = 0.6f;

    private bool mapTouchTracking;
    private bool mapTouchSuppressed;
    private Vector2 mapTouchDownScreen;
    private float lastTouchActivityTime = -10f;

    private bool TryGetMapTapThisFrame(out Vector2 screen)
    {
        screen = default;
#if ENABLE_INPUT_SYSTEM
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            var primary = touchscreen.primaryTouch;
            if (primary.press.isPressed || primary.press.wasReleasedThisFrame)
                lastTouchActivityTime = Time.unscaledTime;

            if (primary.press.wasPressedThisFrame)
            {
                mapTouchTracking = true;
                mapTouchSuppressed = false;
                mapTouchDownScreen = primary.position.ReadValue();
            }

            if (mapTouchTracking)
            {
                int active = 0;
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                    if (touches[i].press.isPressed)
                        active++;
                if (active >= 2)
                    mapTouchSuppressed = true;

                float dpiScale = Screen.dpi > 0f ? Mathf.Max(1f, Screen.dpi / 160f) : 1f;
                if (Vector2.Distance(primary.position.ReadValue(), mapTouchDownScreen) >
                    MapTapMaxTravelPixels * dpiScale)
                    mapTouchSuppressed = true;
            }

            if (primary.press.wasReleasedThisFrame)
            {
                bool isTap = mapTouchTracking && !mapTouchSuppressed;
                mapTouchTracking = false;
                mapTouchSuppressed = false;
                if (isTap)
                {
                    screen = mapTouchDownScreen;
                    return true;
                }
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            Time.unscaledTime - lastTouchActivityTime > MouseIgnoreAfterTouchSeconds)
        {
            screen = Mouse.current.position.ReadValue();
            return true;
        }
        return false;
#else
        if (Input.GetMouseButtonDown(0))
        {
            screen = Input.mousePosition;
            return true;
        }
        return false;
#endif
    }

    private bool HandleMapTap(Vector2 screen)
    {
        if (IsScreenPointOverUi(screen) || worldTilemap == null)
            return false;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        Vector3Int cell = worldTilemap.WorldToCell(world);
        cell.z = 0;

        int index = FindQuadrantAtCell(cell);
        if (index < 0)
            return false;

        if (index == selectedQuadrantIndex)
        {
            UiInputBlocker.SuppressGameplayInputForFrames(1);
            OpenConfirmation();
        }
        else
        {
            SelectQuadrant(index, playMoveSfx: true, adjustCamera: false);
        }
        return true;
    }

    // Quadrantes vizinhos podem dividir a faixa da borda: vence o de centro mais
    // proximo da celula tocada.
    private int FindQuadrantAtCell(Vector3Int cell)
    {
        int best = -1;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < quadrants.Count; i++)
        {
            QuadranteData q = quadrants[i].Quadrante;
            if (q == null)
                continue;
            if (cell.x < q.originX || cell.x >= q.originX + q.width ||
                cell.y < q.originY || cell.y >= q.originY + q.height)
                continue;

            float distance = Vector2.Distance(GetQuadrantCenter(q), new Vector2(cell.x + 0.5f, cell.y + 0.5f));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    private static readonly List<RaycastResult> uiRaycastBuffer = new List<RaycastResult>();

    // Fora = no mapa (nenhuma UI) ou no fundo do proprio menu. Um toque em outro
    // botao NAO conta: o toque que abriu o menu solta em cima do botao de abrir, e
    // fecharia o menu no mesmo gesto.
    private bool IsTapOutsideCampaignMenu(Vector2 screen)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || campaignMenuPanel == null)
            return false;

        uiRaycastBuffer.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screen }, uiRaycastBuffer);
        if (uiRaycastBuffer.Count == 0)
            return true;

        Transform panel = campaignMenuPanel.transform;
        for (int i = 0; i < uiRaycastBuffer.Count; i++)
        {
            GameObject hit = uiRaycastBuffer[i].gameObject;
            if (hit != null && hit.transform.IsChildOf(panel))
                return false;
        }

        GameObject top = uiRaycastBuffer[0].gameObject;
        return campaignMenuRoot != null && top != null && top.transform.IsChildOf(campaignMenuRoot.transform);
    }

    private static bool IsScreenPointOverUi(Vector2 screen)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        uiRaycastBuffer.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screen }, uiRaycastBuffer);
        return uiRaycastBuffer.Count > 0;
    }

    private static Vector2 GetQuadrantCenter(QuadranteData q)
    {
        return new Vector2(q.originX + q.width * 0.5f, q.originY + q.height * 0.5f);
    }

    private bool TryFindQuadrantFocusCell(QuadranteData q, out Vector3Int cell)
    {
        cell = new Vector3Int(q.originX + q.width / 2, q.originY + q.height / 2, 0);
        if (worldTilemap != null && worldTilemap.HasTile(cell))
            return true;

        for (int y = 0; y < q.height; y++)
        {
            for (int x = 0; x < q.width; x++)
            {
                cell = new Vector3Int(q.originX + x, q.originY + y, 0);
                if (worldTilemap != null && worldTilemap.HasTile(cell))
                    return true;
            }
        }

        return false;
    }

    private void RefreshHoveredQuadrant(bool force)
    {
        QuadrantEntry next = selectedQuadrantIndex >= 0 && selectedQuadrantIndex < quadrants.Count
            ? quadrants[selectedQuadrantIndex]
            : null;
        if (!force && ReferenceEquals(next, hovered))
            return;

        hovered = next;
        if (hovered == null)
        {
            PanelHelperController.TrySetExternalText(
                mundo != null ? mundo.displayName.ToUpperInvariant() : PanelMessage.Helper("helper.campaign.title"),
                PanelMessage.Helper("helper.campaign.navigate"));
            return;
        }

        QuadranteData q = hovered.Quadrante;
        // RODADAS, nao "turnos": o currentTurn do MatchController so incrementa
        // em CloseRoundAndAdvanceToFirstPlayer, ou seja quando o indice de jogador
        // DA A VOLTA. Passar a vez de um jogador ao outro nao mexe nele.
        //
        // O codigo chama isso de "turno" em todo lugar (HUD da batalha, cortina de
        // privacidade), mas o autor chama de RODADA e reserva "turno" para a jogada
        // individual. Aqui vale o vocabulario do autor, porque este numero e a
        // MARCA: o jogador compara ao rejogar, sem tela nenhuma por perto para dar
        // contexto.
        string bake = q.HasBake ? string.Empty : PanelMessage.Helper("helper.campaign.no_bake_badge");
        if (q.emDesenvolvimento)
            bake += PanelMessage.Helper("helper.campaign.development_badge");
        string result = PanelMessage.Helper("helper.campaign.no_result");
        if (mundo != null && CampaignProgressStore.TryGetResult(
            mundo.mundoId, hovered.Campanha.campanhaId, q.quadranteId,
            out PlayerSlotId winner, out int turn, out string reason))
        {
            TeamId team = matchController != null
                ? matchController.GetTeamIdForSlot(winner.Value) : TeamId.Neutral;
            string winnerName = PanelMessage.Helper("helper.campaign.player", ("number", winner.Value + 1));
            if (team != TeamId.Neutral)
                winnerName += $" ({TeamUtils.GetName(team)})";
            string color = ColorUtility.ToHtmlStringRGB(TeamUtils.GetColor(team));
            // O MOTIVO separa vitoria jogada de vitoria por setup: "exercito
            // eliminado" na rodada 2 e o sintoma de quadrante sem tropa nem caixa.
            result = PanelMessage.Helper("helper.campaign.result", ("color", color), ("winner", winnerName), ("round", turn), ("reason", CampaignProgressStore.DescreverMotivo(reason).ToUpperInvariant()));
        }
        PanelHelperController.TrySetExternalText(
            hovered.Bloco.displayName.ToUpperInvariant(),
            PanelMessage.Helper("helper.campaign.quadrant", ("campaign", hovered.Campanha.displayName), ("quadrant", q.displayName), ("description", q.descricao), ("availability", bake), ("result", result)));
    }

    private void FrameWorldInCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || worldTilemap == null || worldTilemap.cellBounds.size.x <= 0)
            return;

        Bounds localBounds = worldTilemap.localBounds;
        Vector3 center = worldTilemap.transform.TransformPoint(localBounds.center);
        Vector3 size = worldTilemap.transform.TransformVector(localBounds.size);
        float aspect = Mathf.Max(0.1f, cam.aspect);
        float halfHeight = Mathf.Abs(size.y) * 0.5f;
        float halfWidthByAspect = Mathf.Abs(size.x) * 0.5f / aspect;
        cam.orthographicSize = Mathf.Max(1f, Mathf.Max(halfHeight, halfWidthByAspect) + 0.75f);
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
    }

    private void DisableGameplayFogPresentation()
    {
        FogOfWarController[] controllers =
            FindObjectsByType<FogOfWarController>(FindObjectsInactive.Include);
        for (int i = 0; i < controllers.Length; i++)
            if (controllers[i] != null)
                controllers[i].gameObject.SetActive(false);

        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && string.Equals(maps[i].name, "FogOfWar", StringComparison.OrdinalIgnoreCase))
                maps[i].gameObject.SetActive(false);
        }
    }

    private static bool WasSubmitPressedThisFrame()
    {
        if (RemoteInput.ConfirmDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool IsSubmitHeldNow()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed))
            return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) ||
               Input.GetKey(KeyCode.JoystickButton0) || Input.GetMouseButton(0);
#else
        return false;
#endif
    }

    private static bool WasCancelPressedThisFrame()
    {
        if (RemoteInput.CancelDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private static bool WasPreviousPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame ||
             Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) ||
               Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A);
    }

    private static bool WasNextPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame ||
             Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
               Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D);
    }

    private static bool WasQuadrantDirectionPressedThisFrame(out Vector2 direction)
    {
        direction = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
                direction += Vector2.up;
            if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
                direction += Vector2.down;
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                direction += Vector2.left;
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                direction += Vector2.right;
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.up.wasPressedThisFrame || Gamepad.current.leftStick.up.wasPressedThisFrame)
                direction += Vector2.up;
            if (Gamepad.current.dpad.down.wasPressedThisFrame || Gamepad.current.leftStick.down.wasPressedThisFrame)
                direction += Vector2.down;
            if (Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.leftStick.left.wasPressedThisFrame)
                direction += Vector2.left;
            if (Gamepad.current.dpad.right.wasPressedThisFrame || Gamepad.current.leftStick.right.wasPressedThisFrame)
                direction += Vector2.right;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) direction += Vector2.up;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) direction += Vector2.down;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) direction += Vector2.left;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) direction += Vector2.right;
#endif
        return direction.sqrMagnitude > 0.01f;
    }
}
