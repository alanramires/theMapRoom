using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SaveGameManager : MonoBehaviour
{
    // Disparado apos o load ser concluido com sucesso (independente do time ativo).
    public static event Action OnAfterLoadSuccess;
    public static bool IsAnyLoadInProgress { get; private set; }
    public static bool HasPendingMainMenuLoadRequest => mainMenuLoadTransitionActive || pendingMainMenuLoad != null;

    private sealed class PendingMainMenuLoadRequest
    {
        public int slotIndex;
        public string sceneName;
    }

    private static PendingMainMenuLoadRequest pendingMainMenuLoad;
    private static bool mainMenuLoadTransitionActive;
    private static bool suppressNextLoadConfirmSfx;
    private static string pendingNewGameSaveDirectory;

    
    [SerializeField] private UnitSpawner unitSpawner;
    
    [SerializeField] private ConstructionSpawner constructionSpawner;
    
    [SerializeField] private MatchController matchController;
    
    [SerializeField] private TurnStateManager turnStateManager;
    
    [SerializeField] private AnimationManager animationManager;
    
    [SerializeField] private CursorController cursorController;
    
    [SerializeField] private ReplayManager replayManager;
    
    [SerializeField] private PlanningManager planningManager;

    [SerializeField] private AIController aiController;
    [SerializeField] private PanelRodadaController panelRodada;
    [SerializeField] private MatchMusicAudioManager matchMusicAudioManager;
    
    // [SerializeField] private AIPlayerController aiPlayerController;

    [Header("Quick Save/Load")]
    [SerializeField] private bool enableHotkeys = true;
    [SerializeField] private KeyCode quickSaveKey = KeyCode.I;
    [SerializeField] private KeyCode quickLoadKey = KeyCode.O;
    [FormerlySerializedAs("slotName")]
    [SerializeField] private string fileNameDefault = "<Map>_<date>_<hour>";
    [SerializeField] private bool useSceneSpecificSlot = false;
    [Header("Save Path")]
    [Tooltip("Diretorio atual de save (editavel). Se vazio, usa Application.persistentDataPath.")]
    [SerializeField] private string customSaveDirectory = string.Empty;
    [SerializeField] private bool blockCrossSceneLoad = true;
    [SerializeField] private bool verboseLogs = true;
    [Tooltip("Exibe no Console traces detalhados de entrada nos fluxos de Save/Load.")]
    [InspectorName("Show SaveLoad Logs")]
    [SerializeField] private bool showSaveLoadLogs;
    [SerializeField] private bool forceLoadWhenBusy = true;

    [Header("Replay")]
    [SerializeField] private bool saveReplayData = true;
    [Header("Load Performance")]
    [SerializeField] private bool enableThreatCacheWarmupOnLoad = false;
    [SerializeField] private bool enableLoadPerfLogs = true;
    [Header("Prompt Performance")]
    [SerializeField] private bool enablePromptPerfLogs = false;
    [SerializeField] [Range(0f, 1000f)] private float promptPerfWarnThresholdMs = 50f;

    private enum SlotPromptState
    {
        None = 0,
        SaveSelectSlot = 1,
        SaveConfirmOverwrite = 2,
        LoadSelectSlot = 3
    }

    private sealed class SaveSlotMetadata
    {
        public int slotIndex;
        public bool exists;
        public string sceneName;
        public DateTime savedAtLocal;
        public string path;
    }

    [Serializable]
    private sealed class SaveContainerManifest
    {
        public int containerVersion = 1;
        public int saveVersion;
        public string sceneName;
        public long savedAtUtcTicks;
        public bool hasReplay;
        public bool hasJogadas;
        // Hash canonico do estado no momento do save (MatchStateHasher).
        // Saves antigos: vazio. Futuro pacote de turno do multiplayer viaja
        // com este campo como detector de desync.
        public string stateHash;
    }

    private sealed class LoadPreprocessResult
    {
        public string manifestJson;
        public string json;
        public string replayJson;
        public string jogadasJson;
        public int containerBytes;
        public int uncompressedBytes;
        public string error;
    }

    private const string SaveContainerExtension = ".tmrsave";
    private const string SaveContainerManifestEntry = "manifest.json";
    private const string SaveContainerGameEntry = "game.json";
    private const string SaveContainerReplayEntry = "replay.json";
    private const string SaveContainerJogadasEntry = "jogadas.json";

    private bool loadInProgress;
    private bool lastLoadRoutineSucceeded;
    private bool promptUsingPersistenceState;
    private Coroutine postLoadThreatWarmupRoutine;
    private SlotPromptState promptState;
    private int promptOpenedFrame = -1;
    private int overwritePendingSlot;
#if UNITY_WEBGL && !UNITY_EDITOR
    private bool webGLStorageReady;
    private bool webGLSaveSyncInProgress;
    private int pendingWebGLSaveSlot;
    private string pendingWebGLSavePath;
#endif
    private readonly Dictionary<string, ServiceData> cachedServicesById = new Dictionary<string, ServiceData>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SupplyData> cachedSuppliesById = new Dictionary<string, SupplyData>(StringComparer.OrdinalIgnoreCase);

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void SyncFilesToIndexedDB(string objectName, string callbackMethod);
    [DllImport("__Internal")] private static extern void LoadFilesFromIndexedDB(string objectName, string callbackMethod);
#endif

    private void Awake()
    {
        AIIntelLedger.Clear();
        EnsureDefaultSaveDirectoryConfigured();
        TryAutoAssignReferences();
#if UNITY_WEBGL && !UNITY_EDITOR
        webGLStorageReady = false;
        LoadFilesFromIndexedDB(gameObject.name, nameof(OnWebGLInitSyncComplete));
#endif
    }

    private void OnWebGLInitSyncComplete(string status)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        webGLStorageReady = true;
        if (!string.IsNullOrWhiteSpace(status) && status.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase))
            Debug.LogWarning($"[SaveGame] WebGL: falha ao carregar IndexedDB ({status}). Saves locais podem aparecer vazios nesta sessao.");
        else
            Debug.Log("[SaveGame] WebGL: IndexedDB sincronizado e pronto.");

        TryStartPendingMainMenuLoadForActiveScene();
#endif
    }

    private void BeginWebGLSyncAfterWrite(int slotIndex, string path)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        webGLStorageReady = false;
        webGLSaveSyncInProgress = true;
        pendingWebGLSaveSlot = slotIndex;
        pendingWebGLSavePath = path;
        SyncFilesToIndexedDB(gameObject.name, nameof(OnWebGLSaveSyncComplete));
#endif
    }

    private void OnWebGLSaveSyncComplete(string status)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        int slot = pendingWebGLSaveSlot;
        string path = pendingWebGLSavePath;
        pendingWebGLSaveSlot = 0;
        pendingWebGLSavePath = string.Empty;
        webGLSaveSyncInProgress = false;
        webGLStorageReady = true;

        if (!string.IsNullOrWhiteSpace(status) && status.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase))
        {
            cursorController?.PlayErrorSfx();
            string errorText = ResolveDialog(
                "dialog.save_status.webgl_sync_failed",
                "Falha ao persistir o save no navegador. Tente salvar novamente.");
            PanelDialogController.TrySetTransientText(errorText, 3.2f);
            Debug.LogError($"[SaveGame] WebGL: falha ao sincronizar IndexedDB para slot {slot}: {status}");
            return;
        }

        cursorController?.PlayLoadSfx();
        string savedText = ResolveDialog(
            "dialog.save_status.success_webgl",
            ResolveHelper("helper.save_status.success_webgl", "Jogo salvo no navegador no slot <slot>"),
            new Dictionary<string, string> { { "slot", slot.ToString() } });
        PanelDialogController.TrySetTransientText(savedText, 2.2f);
        Debug.Log($"[SaveGame] WebGL: Slot {slot} persistido no IndexedDB: {path}");
#endif
    }

    private void Start()
    {
        ApplyPendingNewGame();
#if UNITY_WEBGL && !UNITY_EDITOR
        if (webGLStorageReady)
            TryStartPendingMainMenuLoadForActiveScene();
#else
        TryStartPendingMainMenuLoadForActiveScene();
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureDefaultSaveDirectoryConfigured();
    }
#endif

    private void OnDisable()
    {
        if (postLoadThreatWarmupRoutine != null)
        {
            StopCoroutine(postLoadThreatWarmupRoutine);
            postLoadThreatWarmupRoutine = null;
        }

        CancelPrompt(clearDialogOverride: true);
        ExitPersistenceStateForPromptIfNeeded();
    }

    private void Update()
    {
        // O panel_rodada e uma barreira total. SaveGameManager le I/O/Esc
        // diretamente, fora do CursorController, portanto precisa respeitar o
        // gate antes ate de processar/cancelar um prompt ja aberto.
        if (PanelRodadaController.IsGameplayInputBlocked)
            return;

        if (!Application.isPlaying || !enableHotkeys || loadInProgress)
            return;

        if (IsPersistenceBlockedByActiveAI())
            return;

        if (promptState == SlotPromptState.None && IsPersistenceBlockedByTurnState())
            return;

        if (promptState != SlotPromptState.None)
        {
            UiInputBlocker.SuppressGameplayInputForFrames(1);
            HandlePromptInput();
            return;
        }

        if (UiInputBlocker.IsTextInputFocused())
            return;

        if (WasKeyPressedThisFrame(quickSaveKey))
        {
            OpenSaveSlotPrompt(PerfNowMs());
            return;
        }

        if (WasKeyPressedThisFrame(quickLoadKey))
        {
            OpenLoadSlotPrompt(PerfNowMs());
            return;
        }
    }

    [ContextMenu("Save Quick Slot")]
    public void Save()
    {
        if (PanelRodadaController.IsGameplayInputBlocked)
            return;
        if (IsPersistenceBlockedByActiveAI(showFeedback: true))
            return;
        if (IsPersistenceBlockedByTurnState(showFeedback: true))
            return;
        SaveSlot(1);
    }

    [ContextMenu("Save Slot 1")]
    public void SaveSlot1() => SaveSlot(1);

    [ContextMenu("Save Slot 2")]
    public void SaveSlot2() => SaveSlot(2);

    [ContextMenu("Save Slot 3")]
    public void SaveSlot3() => SaveSlot(3);

    public void OpenSaveSlotPromptFromMenu()
    {
        if (PanelRodadaController.IsGameplayInputBlocked)
            return;

        if (!EnterSavingStateForPersistencePrompt())
            return;

        if (IsPersistenceBlockedByActiveAI(showFeedback: true))
        {
            ExitPersistenceStateForPromptIfNeeded();
            return;
        }
        OpenSaveSlotPrompt(PerfNowMs());
    }

    public void OpenLoadSlotPromptFromMenu()
    {
        if (PanelRodadaController.IsGameplayInputBlocked)
            return;

        if (!EnterLoadingStateForPersistencePrompt())
            return;

        if (IsPersistenceBlockedByActiveAI(showFeedback: true))
        {
            ExitPersistenceStateForPromptIfNeeded();
            return;
        }
        OpenLoadSlotPrompt(PerfNowMs());
    }

    private void OpenSaveSlotPrompt(double inputStartMs = -1d)
    {
        if (!IsWebGLStorageAvailable(showFeedback: true))
            return;

        double promptStartMs = inputStartMs >= 0d ? inputStartMs : PerfNowMs();
        double refsStartMs = PerfNowMs();
        TryAutoAssignReferences();
        LogPromptPerf("save_prompt.auto_assign_refs", PerfNowMs() - refsStartMs);

        if (!EnterSavingStateForPersistencePrompt())
            return;

        if (IsPersistenceBlockedByTurnState(showFeedback: true, allowPersistencePromptState: true))
            return;

        if (TryBlockAircraftFuelDepletionPersistence(
                "[Save] Bloqueado: fila de aeronaves caindo em execucao",
                "dialog.save_load.blocked_aircraft_depletion",
                "Save/Load bloqueado: aguarde a fila de aeronaves caindo finalizar"))
            return;

        if (TryBlockReplayPersistence(
                "[Save] Bloqueado: replay ativo",
                "dialog.replay.save_disabled",
                "Replay :: save desativado durante replay"))
            return;

        if (TryBlockPlanningSavePersistence(
                "[Save] Bloqueado: planning ativo",
                "dialog.planning.save_blocked",
                "Save bloqueado durante Planning: saia do modo P antes de salvar"))
            return;

        promptState = SlotPromptState.SaveSelectSlot;
        promptOpenedFrame = Time.frameCount;
        overwritePendingSlot = 0;
        ResetPersistencePromptFocus();
        cursorController?.PlayConfirmSfx();
        PanelDialogController.ClearExternalText();
        RefreshPromptHelper();
        LogPromptPerf("save_prompt.total_key_to_helper", PerfNowMs() - promptStartMs, forceWarning: true);
    }

    private void OpenLoadSlotPrompt(double inputStartMs = -1d)
    {
        if (!IsWebGLStorageAvailable(showFeedback: true))
            return;

        double promptStartMs = inputStartMs >= 0d ? inputStartMs : PerfNowMs();
        double refsStartMs = PerfNowMs();
        TryAutoAssignReferences();
        LogPromptPerf("load_prompt.auto_assign_refs", PerfNowMs() - refsStartMs);

        if (!EnterLoadingStateForPersistencePrompt())
            return;

        if (IsPersistenceBlockedByTurnState(showFeedback: true, allowPersistencePromptState: true))
            return;

        if (TryBlockAircraftFuelDepletionPersistence(
                "[Load] Bloqueado: fila de aeronaves caindo em execucao",
                "dialog.save_load.blocked_aircraft_depletion",
                "Save/Load bloqueado: aguarde a fila de aeronaves caindo finalizar"))
            return;

        if (TryBlockReplayPersistence(
                "[Load] Bloqueado: replay ativo",
                "dialog.replay.load_disabled",
                "Replay :: load desativado durante replay"))
            return;

        promptState = SlotPromptState.LoadSelectSlot;
        promptOpenedFrame = Time.frameCount;
        overwritePendingSlot = 0;
        ResetPersistencePromptFocus();
        cursorController?.PlayConfirmSfx();
        PanelDialogController.ClearExternalText();
        RefreshPromptHelper();
        LogPromptPerf("load_prompt.total_key_to_helper", PerfNowMs() - promptStartMs, forceWarning: true);
    }

    private void HandlePromptInput()
    {
        if (Time.frameCount <= promptOpenedFrame)
            return;

        if (WasKeyPressedThisFrame(KeyCode.UpArrow))
        {
            NavigatePersistencePromptFocus(-1);
            return;
        }

        if (WasKeyPressedThisFrame(KeyCode.DownArrow))
        {
            NavigatePersistencePromptFocus(+1);
            return;
        }

        // Numeros continuam escolhendo o slot direto (atalho), independente do foco.
        if (WasAnySlotNumberPressedThisFrame(out int slotPressed))
        {
            HandleSlotChosen(slotPressed);
            return;
        }

        // Enter aciona o botao em foco (slot destacado, CANCELAR, CONFIRMAR ou VOLTAR).
        if (WasKeyPressedThisFrame(KeyCode.Return) || WasKeyPressedThisFrame(KeyCode.KeypadEnter))
        {
            ConfirmPersistencePromptFocused();
            return;
        }

        if (WasPersistencePromptCancelPressedThisFrame())
        {
            if (promptState == SlotPromptState.SaveConfirmOverwrite)
            {
                promptState = SlotPromptState.SaveSelectSlot;
                overwritePendingSlot = 0;
                ResetPersistencePromptFocus();
                PanelDialogController.ClearExternalText();
                RefreshPromptHelper();
                cursorController?.PlayBeepSfx();
            }
            else
            {
                CancelPrompt(clearDialogOverride: true);
                BattleMapMenuRootController.SuppressMenuOpenForCurrentFrame();
                cursorController?.PlayCancelSfx();
            }
        }
    }

    private bool WasPersistencePromptCancelPressedThisFrame()
    {
        return WasKeyPressedThisFrame(KeyCode.Escape) ||
               RemoteInput.CancelDownThisFrame() ||
               RemoteInput.RightClickCancelDownThisFrame();
    }

    private void HandleSlotChosen(int slotIndex)
    {
        int normalizedSlot = NormalizeSlot(slotIndex);
        SaveSlotMetadata metadata = ReadSlotMetadata(normalizedSlot);
        if (promptState == SlotPromptState.LoadSelectSlot)
        {
            if (!metadata.exists)
            {
                cursorController?.PlayErrorSfx();
                string text = ResolveDialog(
                    "dialog.load_prompt.slot_empty",
                    "Nao e possivel carregar: slot <slot> vazio.",
                    new Dictionary<string, string> { { "slot", normalizedSlot.ToString() } });
                PanelDialogController.TrySetTransientText(text, 2.4f);
                return;
            }

            LoadSlot(normalizedSlot);
            CompletePromptAfterConfirmedPersistence();
            return;
        }

        if (metadata.exists)
        {
            promptState = SlotPromptState.SaveConfirmOverwrite;
            overwritePendingSlot = normalizedSlot;
            ResetPersistencePromptFocus();
            PanelDialogController.TrySetExternalText(BuildOverwriteDialogText(metadata));
            RefreshPromptHelper();
            cursorController?.PlayBeepSfx();
            return;
        }

        SaveSlot(normalizedSlot);
        CompletePromptAfterConfirmedPersistence();
    }

    public bool IsPersistenceSlotSelectionActive =>
        promptState == SlotPromptState.SaveSelectSlot || promptState == SlotPromptState.LoadSelectSlot;

    public bool IsPersistenceOverwriteConfirmationActive =>
        promptState == SlotPromptState.SaveConfirmOverwrite;

    // Foco de teclado no prompt de save/load, na MESMA ordem dos botoes do PanelHelper:
    // selecao de slot = [slot1, slot2, slot3, CANCELAR]; sobrescrita = [CONFIRMAR, VOLTAR].
    private int persistencePromptFocusIndex;

    public int PersistencePromptFocusIndex => persistencePromptFocusIndex;

    private int GetPersistencePromptButtonCount()
    {
        if (IsPersistenceOverwriteConfirmationActive)
            return 2;
        if (IsPersistenceSlotSelectionActive)
            return 4;
        return 0;
    }

    private void ResetPersistencePromptFocus()
    {
        persistencePromptFocusIndex = 0;
    }

    private void NavigatePersistencePromptFocus(int delta)
    {
        int count = GetPersistencePromptButtonCount();
        if (count <= 0 || delta == 0)
            return;

        // Wrap: do primeiro item pra cima vai pro ultimo (CANCELAR) e vice-versa.
        int next = (persistencePromptFocusIndex + (delta > 0 ? 1 : -1) + count) % count;
        if (next == persistencePromptFocusIndex)
            return;

        persistencePromptFocusIndex = next;
        cursorController?.PlayCursorMoveSfx();
    }

    // Enter no prompt aciona o botao em foco, casando com o onClick de cada botao do PanelHelper.
    private void ConfirmPersistencePromptFocused()
    {
        if (IsPersistenceOverwriteConfirmationActive)
        {
            if (persistencePromptFocusIndex == 0)
                SaveSlot(overwritePendingSlot);
            else
            {
                promptState = SlotPromptState.SaveSelectSlot;
                overwritePendingSlot = 0;
                ResetPersistencePromptFocus();
                PanelDialogController.ClearExternalText();
                RefreshPromptHelper();
                cursorController?.PlayBeepSfx();
                return;
            }
            CompletePromptAfterConfirmedPersistence();
            return;
        }

        if (!IsPersistenceSlotSelectionActive)
            return;

        if (persistencePromptFocusIndex >= 0 && persistencePromptFocusIndex <= 2)
            HandleSlotChosen(persistencePromptFocusIndex + 1);
        else
        {
            CancelPrompt(clearDialogOverride: true);
            BattleMapMenuRootController.SuppressMenuOpenForCurrentFrame();
            cursorController?.PlayCancelSfx();
        }
    }

    public string GetPersistenceSlotButtonLabel(int slotIndex)
    {
        return BuildSlotDisplayLine(ReadSlotMetadata(NormalizeSlot(slotIndex)));
    }

    public bool TryChoosePersistenceSlotFromPointer(int slotIndex)
    {
        if (!IsPersistenceSlotSelectionActive)
            return false;
        HandleSlotChosen(slotIndex);
        return true;
    }

    public bool TryConfirmPersistenceOverwriteFromPointer()
    {
        if (!IsPersistenceOverwriteConfirmationActive)
            return false;
        SaveSlot(overwritePendingSlot);
        CompletePromptAfterConfirmedPersistence();
        return true;
    }

    public bool TryCancelPersistencePromptFromPointer()
    {
        if (promptState == SlotPromptState.None)
            return false;

        if (promptState == SlotPromptState.SaveConfirmOverwrite)
        {
            promptState = SlotPromptState.SaveSelectSlot;
            overwritePendingSlot = 0;
            ResetPersistencePromptFocus();
            PanelDialogController.ClearExternalText();
            RefreshPromptHelper();
            cursorController?.PlayBeepSfx();
        }
        else
        {
            CancelPrompt(clearDialogOverride: true);
            BattleMapMenuRootController.SuppressMenuOpenForCurrentFrame();
            cursorController?.PlayCancelSfx();
        }
        return true;
    }

    private void CompletePromptAfterConfirmedPersistence()
    {
        promptState = SlotPromptState.None;
        promptOpenedFrame = -1;
        overwritePendingSlot = 0;
        PanelHelperController.ClearExternalText();
        ExitPersistenceStateToNeutralAfterConfirmedPersistence();
        BattleMapMenuRootController.SuppressMenuOpenForCurrentFrame();
    }

    private void CancelPrompt(bool clearDialogOverride)
    {
        if (promptState == SlotPromptState.None)
        {
            promptOpenedFrame = -1;
            if (clearDialogOverride)
                PanelDialogController.ClearExternalText();
            ExitPersistenceStateForPromptIfNeeded();
            return;
        }

        promptState = SlotPromptState.None;
        promptOpenedFrame = -1;
        overwritePendingSlot = 0;
        PanelHelperController.ClearExternalText();
        if (clearDialogOverride)
            PanelDialogController.ClearExternalText();
        ExitPersistenceStateForPromptIfNeeded();
    }

    private void RefreshPromptHelper()
    {
        double startMs = PerfNowMs();
        if (promptState == SlotPromptState.None)
        {
            PanelHelperController.ClearExternalText();
            return;
        }

        string title = promptState == SlotPromptState.LoadSelectSlot
            ? ResolveHelper("helper.load_prompt.title", "LOAD")
            : ResolveHelper("helper.save_prompt.title", "SAVE");
        string body = BuildPromptBody();
        PanelHelperController.TrySetExternalText(title, body);
        LogPromptPerf($"prompt_helper.refresh.{promptState}", PerfNowMs() - startMs);
    }

    private string BuildPromptBody()
    {
        double buildStartMs = PerfNowMs();
        StringBuilder sb = new StringBuilder();
        string header = promptState == SlotPromptState.LoadSelectSlot
            ? ResolveHelper("helper.load_prompt.header", "carregar jogo")
            : ResolveHelper("helper.save_prompt.header", "salvar em qual slot");
        sb.AppendLine(header);
        for (int slot = 1; slot <= 3; slot++)
        {
            double slotReadStartMs = PerfNowMs();
            SaveSlotMetadata metadata = ReadSlotMetadata(slot);
            LogPromptPerf($"prompt_helper.read_slot_metadata.slot_{slot}", PerfNowMs() - slotReadStartMs);
            sb.AppendLine(BuildSlotDisplayLine(metadata));
        }

        if (promptState == SlotPromptState.SaveConfirmOverwrite)
        {
            sb.Append(ResolveHelper("helper.save_prompt.footer.overwrite", "ENTER: confirmar sobrescrita | ESC: voltar"));
        }
        else if (promptState == SlotPromptState.LoadSelectSlot)
        {
            sb.Append(ResolveHelper("helper.load_prompt.footer.select", "ESC: cancelar"));
        }
        else
        {
            sb.Append(ResolveHelper("helper.save_prompt.footer.select", "ESC: cancelar"));
        }

        LogPromptPerf("prompt_helper.build_body_total", PerfNowMs() - buildStartMs);
        return sb.ToString();
    }

    private string BuildSlotDisplayLine(SaveSlotMetadata metadata)
    {
        if (metadata == null)
            return "-";

        if (!metadata.exists)
        {
            return ResolveHelper(
                "helper.slot.line.empty",
                "<slot>:",
                new Dictionary<string, string> { { "slot", metadata.slotIndex.ToString() } });
        }

        string scene = string.IsNullOrWhiteSpace(metadata.sceneName) ? "Mapa desconhecido" : metadata.sceneName.Trim();
        string date = metadata.savedAtLocal.ToString("dd-MM-yy HH'h'mm");
        return ResolveHelper(
            "helper.slot.line.filled",
            "<slot>:  <scene> - <date>",
            new Dictionary<string, string>
            {
                { "slot", metadata.slotIndex.ToString() },
                { "scene", scene },
                { "date", date }
            });
    }

    private string BuildOverwriteDialogText(SaveSlotMetadata metadata)
    {
        string slotLine = BuildSlotDisplayLine(metadata);
        return ResolveDialog(
            "dialog.save_prompt.overwrite_confirm",
            "<slot_line>\nSobrescrever este save?\nENTER: confirmar | ESC: voltar",
            new Dictionary<string, string> { { "slot_line", slotLine } });
    }

    private bool WasAnySlotNumberPressedThisFrame(out int slotIndex)
    {
        if (WasKeyPressedThisFrame(KeyCode.Alpha1) || WasKeyPressedThisFrame(KeyCode.Keypad1))
        {
            slotIndex = 1;
            return true;
        }

        if (WasKeyPressedThisFrame(KeyCode.Alpha2) || WasKeyPressedThisFrame(KeyCode.Keypad2))
        {
            slotIndex = 2;
            return true;
        }

        if (WasKeyPressedThisFrame(KeyCode.Alpha3) || WasKeyPressedThisFrame(KeyCode.Keypad3))
        {
            slotIndex = 3;
            return true;
        }

        slotIndex = 0;
        return false;
    }

    public void SaveSlot(int slotIndex)
    {
        if (!IsWebGLStorageAvailable(showFeedback: true))
            return;

        if (IsPersistenceBlockedByActiveAI(showFeedback: true))
            return;
        if (IsPersistenceBlockedByTurnState(showFeedback: true, allowPersistencePromptState: promptState != SlotPromptState.None))
            return;

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SaveGame] Save funciona apenas em Play Mode.");
            return;
        }

        if (TryBlockAircraftFuelDepletionPersistence(
                "[Save] Bloqueado: fila de aeronaves caindo em execucao",
                "dialog.save_load.blocked_aircraft_depletion",
                "Save/Load bloqueado: aguarde a fila de aeronaves caindo finalizar"))
            return;

        if (TryBlockReplayPersistence(
                "[Save] Bloqueado: replay ativo",
                "dialog.replay.save_disabled",
                "Replay :: save desativado durante replay"))
            return;

        if (TryBlockPlanningSavePersistence(
                "[Save] Bloqueado: planning ativo",
                "dialog.planning.save_blocked",
                "Save bloqueado durante Planning: saia do modo P antes de salvar"))
            return;

        int normalizedSlot = NormalizeSlot(slotIndex);
        try
        {
            TryAutoAssignReferences();
            if (matchController != null && matchController.EnableTotalWar && turnStateManager != null)
            {
                turnStateManager.ClearThreatLayerHotzoneCache();
                if (verboseLogs)
                    Debug.Log("[SaveGame] Skip saving hotzone cache: Total War ativo.");
            }

            cursorController?.PlayConfirmSfx();

            SaveGameData data = BuildSaveData();
            // ComputeHash tambem canonicaliza as listas (SortCanonical), entao o
            // JSON persistido abaixo sai em ordem canonica — dois saves do mesmo
            // estado geram bytes identicos. O hash logado e a ferramenta de
            // validacao round-trip: salvar -> carregar -> salvar deve repetir o
            // hash; divergencia = campo se perdendo no load. E a fundacao do
            // anti-desync do multiplayer (docs/ideias_futuras_multiplayer.md).
            string stateHash = MatchStateHasher.ComputeHash(data);
            if (showSaveLoadLogs)
                Debug.Log($"[SaveGame] state_hash={stateHash}");
            string json = JsonUtility.ToJson(data, false);
            string replayJson = BuildReplayJsonForSave();
            string jogadasJson = BuildJogadasJsonForSave();
            string path = ResolveWritableSlotPath(normalizedSlot);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ResolveSaveDirectory());
            WriteSaveContainerAtomic(path, data, json, replayJson, jogadasJson, stateHash);
            if (showSaveLoadLogs)
                LogSaveDiagnostics(normalizedSlot, json, new FileInfo(path).Length);
#if UNITY_WEBGL && !UNITY_EDITOR
            string syncingText = ResolveDialog(
                "dialog.save_status.webgl_syncing",
                "Salvando no navegador...");
            PanelDialogController.TrySetTransientText(syncingText, 2.2f);
            BeginWebGLSyncAfterWrite(normalizedSlot, path);
#else
            cursorController?.PlayLoadSfx();
            string savedText = ResolveDialog(
                "dialog.save_status.success",
                ResolveHelper("helper.save_status.success", "Jogo salvo no slot <slot>"),
                new Dictionary<string, string> { { "slot", normalizedSlot.ToString() } });
            PanelDialogController.TrySetTransientText(savedText, 2.2f);
            if (showSaveLoadLogs)
                Debug.Log($"[SaveGame] Slot {normalizedSlot} salvo em: {path}");
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveGame] Falha ao salvar: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Hash canonico do estado ATUAL da cena (sem gravar nada). Fluxo de
    // validacao round-trip manual: salvar (hash sai no log) -> carregar ->
    // rodar "state hash" no debug -> comparar. Divergiu = campo se perdendo
    // no load.
    public string ComputeCurrentStateHash()
    {
        SaveGameData data = BuildSaveData();
        return MatchStateHasher.ComputeHash(data);
    }

    // Diagnostico do round-trip: grava o JSON canonico do estado atual num
    // arquivo. Rodar apos o save e apos o load e diffar os dois arquivos
    // aponta exatamente o campo que diverge quando os hashes nao batem.
    public bool TryDumpCanonicalStateToFile(out string path)
    {
        path = string.Empty;
        try
        {
            SaveGameData data = BuildSaveData();
            string json = MatchStateHasher.BuildCanonicalJson(data);
            if (string.IsNullOrEmpty(json))
                return false;

            path = Path.Combine(Application.persistentDataPath, $"state_dump_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveGame] Falha no state dump: {ex.Message}");
            return false;
        }
    }

    [ContextMenu("Load Quick Slot")]
    public void Load()
    {
        LoadSlot(1);
    }

    [ContextMenu("Load Slot 1")]
    public void LoadSlot1() => LoadSlot(1);

    [ContextMenu("Load Slot 2")]
    public void LoadSlot2() => LoadSlot(2);

    [ContextMenu("Load Slot 3")]
    public void LoadSlot3() => LoadSlot(3);

    public bool HasSaveInSlot(int slotIndex)
    {
        if (!IsWebGLStorageAvailable(showFeedback: false))
            return false;

        int normalizedSlot = NormalizeSlot(slotIndex);
        string path = ResolveReadableSlotPath(normalizedSlot);
        return File.Exists(path);
    }

    public bool TryGetSlotSceneName(int slotIndex, out string sceneName)
    {
        sceneName = string.Empty;
        int normalizedSlot = NormalizeSlot(slotIndex);
        SaveSlotMetadata metadata = ReadSlotMetadata(normalizedSlot);
        if (metadata == null || !metadata.exists)
            return false;

        string value = string.IsNullOrWhiteSpace(metadata.sceneName) ? string.Empty : metadata.sceneName.Trim();
        if (string.Equals(value, "Mapa desconhecido", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        sceneName = value;
        return true;
    }

    public bool BeginLoadFromMainMenuSlot(int slotIndex)
    {
        if (!IsWebGLStorageAvailable(showFeedback: true))
            return false;

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SaveGame] MainMenu load funciona apenas em Play Mode.");
            return false;
        }

        int normalizedSlot = NormalizeSlot(slotIndex);
        SaveSlotMetadata metadata = ReadSlotMetadata(normalizedSlot);
        if (metadata == null || !metadata.exists)
        {
            Debug.LogWarning($"[SaveGame] MainMenu load bloqueado: slot {normalizedSlot} vazio.");
            return false;
        }

        string targetScene = string.IsNullOrWhiteSpace(metadata.sceneName) ? string.Empty : metadata.sceneName.Trim();
        if (string.IsNullOrWhiteSpace(targetScene) || string.Equals(targetScene, "Mapa desconhecido", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[SaveGame] MainMenu load bloqueado: slot {normalizedSlot} sem sceneName valido.");
            return false;
        }

        pendingMainMenuLoad = new PendingMainMenuLoadRequest
        {
            slotIndex = normalizedSlot,
            sceneName = targetScene
        };
        mainMenuLoadTransitionActive = true;
        suppressNextLoadConfirmSfx = true;

        string currentScene = SceneManager.GetActiveScene().name;
        if (string.Equals(currentScene, targetScene, StringComparison.Ordinal))
        {
            if (verboseLogs)
                Debug.Log($"[SaveGame] MainMenu load: slot {normalizedSlot} na mesma cena '{targetScene}'.");
            StartCoroutine(LoadPendingMainMenuSlotNextFrame(normalizedSlot, targetScene));
            return true;
        }

        try
        {
            if (verboseLogs)
                Debug.Log($"[SaveGame] MainMenu load: trocando para cena '{targetScene}' para carregar slot {normalizedSlot}.");
            SceneManager.LoadScene(targetScene);
            return true;
        }
        catch (Exception ex)
        {
            pendingMainMenuLoad = null;
            mainMenuLoadTransitionActive = false;
            suppressNextLoadConfirmSfx = false;
            Debug.LogError($"[SaveGame] Falha ao trocar para cena '{targetScene}': {ex.Message}");
            return false;
        }
    }

    public void LoadSlot(int slotIndex)
    {
        if (!IsWebGLStorageAvailable(showFeedback: true))
            return;

        // Ao carregar pela Tela de Entrada, a cena-base existe apenas como suporte
        // temporario ate o snapshot ser restaurado. Em Hot Seat, o gate de troca de
        // jogador tambem bloqueia input e compartilha a mesma consulta usada pelo
        // turno da IA; nao deixe esse estado transitorio impedir o load solicitado.
        bool isPendingMainMenuLoad = HasPendingMainMenuLoadRequest;
        if (!isPendingMainMenuLoad && IsPersistenceBlockedByActiveAI(showFeedback: true))
            return;
        if (IsPersistenceBlockedByTurnState(showFeedback: true, allowPersistencePromptState: promptState != SlotPromptState.None))
            return;

        if (showSaveLoadLogs)
            Debug.Log($"[TRACE][SaveGameManager.LoadSlot] slotIndex={slotIndex}\n{Environment.StackTrace}");

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SaveGame] Load funciona apenas em Play Mode.");
            return;
        }

        if (TryBlockAircraftFuelDepletionPersistence(
                "[Load] Bloqueado: fila de aeronaves caindo em execucao",
                "dialog.save_load.blocked_aircraft_depletion",
                "Save/Load bloqueado: aguarde a fila de aeronaves caindo finalizar"))
            return;

        if (TryBlockReplayPersistence(
                "[Load] Bloqueado: replay ativo",
                "dialog.replay.load_disabled",
                "Replay :: load desativado durante replay"))
            return;

        if (loadInProgress)
            return;

        if (suppressNextLoadConfirmSfx)
            suppressNextLoadConfirmSfx = false;
        else
            cursorController?.PlayConfirmSfx();
        TryAutoAssignReferences();
        if (unitSpawner == null || constructionSpawner == null)
        {
            Debug.LogError("[SaveGame] UnitSpawner/ConstructionSpawner nao encontrados na cena.");
            return;
        }
        int normalizedSlot = NormalizeSlot(slotIndex);
        string path = ResolveReadableSlotPath(normalizedSlot);
        if (!File.Exists(path))
        {
            cursorController?.PlayErrorSfx();
            Debug.LogWarning($"[SaveGame] Slot {normalizedSlot} sem savegame: {path}");
            return;
        }

        if (!CanLoadNow(out string reason))
        {
            if (!forceLoadWhenBusy)
            {
                Debug.LogWarning($"[SaveGame] Load bloqueado: {reason}");
                return;
            }

            Debug.LogWarning($"[SaveGame] Load fora do estado ideal ({reason}). Forcando carregamento.");
        }

        StartCoroutine(LoadSlotAsync(path, normalizedSlot));
    }

    private bool IsPersistenceBlockedByActiveAI(bool showFeedback = false)
    {
        TryAutoAssignReferences();
        bool playerMenuScoped = turnStateManager != null &&
            (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.PlayerMenu ||
             turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Saving ||
             turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Loading);
        if (playerMenuScoped)
            return false;

        if (matchController == null || !matchController.IsPlayerInputLockedByActiveAI())
            return false;

        if (showFeedback)
        {
            cursorController?.PlayErrorSfx();
            PanelDialogController.TrySetTransientText("Turno da IA em execucao: save/load bloqueado.", 2.4f);
        }

        return true;
    }

    private bool IsWebGLStorageAvailable(bool showFeedback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (webGLStorageReady && !webGLSaveSyncInProgress)
            return true;

        if (showFeedback)
        {
            cursorController?.PlayErrorSfx();
            string message = webGLSaveSyncInProgress
                ? ResolveDialog(
                    "dialog.save_load.webgl_sync_pending",
                    "Save em andamento: aguarde o navegador confirmar.")
                : ResolveDialog(
                    "dialog.save_load.webgl_storage_loading",
                    "Carregando saves do navegador...");
            PanelDialogController.TrySetTransientText(message, 2.4f);
        }

        if (verboseLogs)
            Debug.LogWarning($"[SaveGame] WebGL storage indisponivel: ready={webGLStorageReady} sync={webGLSaveSyncInProgress}");
        return false;
#else
        return true;
#endif
    }

    private bool IsPersistenceBlockedByTurnState(bool showFeedback = false, bool allowPersistencePromptState = false)
    {
        TryAutoAssignReferences();
        if (turnStateManager == null)
            return false;

        TurnStateManager.CursorState state = turnStateManager.CurrentCursorState;
        bool blocked =
            state == TurnStateManager.CursorState.PlayerMenu ||
            state == TurnStateManager.CursorState.CommandService ||
            state == TurnStateManager.CursorState.CommandServiceExecuting ||
            state == TurnStateManager.CursorState.RemovingUnit ||
            state == TurnStateManager.CursorState.RemovingUnitExecuting ||
            state == TurnStateManager.CursorState.EndingTurn ||
            state == TurnStateManager.CursorState.EndingTurnExecuting ||
            ((state == TurnStateManager.CursorState.Saving || state == TurnStateManager.CursorState.Loading) && !allowPersistencePromptState);
        if (!blocked)
            return false;

        if (showFeedback)
        {
            cursorController?.PlayErrorSfx();
            PanelDialogController.TrySetTransientText($"Save/Load bloqueado em {state}: volte ao Neutral.", 2.4f);
        }

        if (showFeedback)
            Debug.LogWarning($"[SaveGame] Save/Load bloqueado: estado atual {state}; volte ao Neutral.");
        return true;
    }

    private bool EnterSavingStateForPersistencePrompt()
    {
        if (turnStateManager == null)
            return true;

        if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Saving)
        {
            promptUsingPersistenceState = true;
            return true;
        }

        if (turnStateManager.TryEnterSavingState(out string message))
        {
            promptUsingPersistenceState = true;
            return true;
        }

        cursorController?.PlayErrorSfx();
        PanelDialogController.TrySetTransientText(
            string.IsNullOrWhiteSpace(message) ? "Save/Load bloqueado no estado atual." : message,
            2.4f);
        return false;
    }

    private bool EnterLoadingStateForPersistencePrompt()
    {
        if (turnStateManager == null)
            return true;

        if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Loading)
        {
            promptUsingPersistenceState = true;
            return true;
        }

        if (turnStateManager.TryEnterLoadingState(out string message))
        {
            promptUsingPersistenceState = true;
            return true;
        }

        cursorController?.PlayErrorSfx();
        PanelDialogController.TrySetTransientText(
            string.IsNullOrWhiteSpace(message) ? "Save/Load bloqueado no estado atual." : message,
            2.4f);
        return false;
    }

    private void ExitPersistenceStateForPromptIfNeeded()
    {
        if (!promptUsingPersistenceState)
            return;

        promptUsingPersistenceState = false;
        turnStateManager?.TryExitPersistencePromptState();
    }

    private void ExitPersistenceStateToNeutralAfterConfirmedPersistence()
    {
        if (!promptUsingPersistenceState)
            return;

        promptUsingPersistenceState = false;
        turnStateManager?.ForceNeutral();
    }

    private IEnumerator LoadSlotAsync(string path, int normalizedSlot)
    {
        loadInProgress = true;
        IsAnyLoadInProgress = true;
        lastLoadRoutineSucceeded = false;
        if (panelRodada == null)
            panelRodada = FindAnyObjectByType<PanelRodadaController>(FindObjectsInactive.Include);
        if (matchMusicAudioManager == null)
            matchMusicAudioManager = FindAnyObjectByType<MatchMusicAudioManager>();
        if (matchMusicAudioManager != null)
        {
            matchMusicAudioManager.BeginTurnTransition();
            matchMusicAudioManager.StopForTurnTransition();
        }
        panelRodada?.BeginLoadingPresentation();
        bool loadingPresentationReleased = false;
        double asyncStartMs = PerfNowMs();
        LogLoadPerf(normalizedSlot, "load_async.start", asyncStartMs, 0d);
        try
        {
            ShowLoadingIndicator("dialog.load_status.loading_wait", "Carregando jogo, aguarde");
            // Garante pelo menos um frame para o indicador aparecer.
            yield return null;

            double preprocessStartMs = PerfNowMs();
            LogLoadPerf(normalizedSlot, "preprocess.begin", preprocessStartMs, preprocessStartMs - asyncStartMs);
            LoadPreprocessResult preprocess;
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL normalmente roda sem workers. Task.Run pode nunca executar e
            // deixar o indicador preso antes do restore comecar. O syncfs inicial
            // ja trouxe o arquivo persistente para o MEMFS.
            preprocess = PreprocessLoadData(path);
            yield return null;
#else
            Task<LoadPreprocessResult> preprocessTask = Task.Run(() => PreprocessLoadData(path));
            while (!preprocessTask.IsCompleted)
                yield return null;
            if (preprocessTask.IsFaulted)
            {
                Debug.LogError($"[SaveGame] Falha no preprocess assíncrono: {preprocessTask.Exception?.GetBaseException().Message}");
                cursorController?.PlayErrorSfx();
                PanelDialogController.ClearExternalText();
                yield break;
            }

            preprocess = preprocessTask.Result;
#endif
            LogLoadPerf(normalizedSlot, "preprocess.end", preprocessStartMs, PerfNowMs() - asyncStartMs);
            if (!string.IsNullOrWhiteSpace(preprocess.error))
            {
                Debug.LogError($"[SaveGame] Falha ao preprocessar save: {preprocess.error}");
                cursorController?.PlayErrorSfx();
                PanelDialogController.ClearExternalText();
                yield break;
            }

            if (verboseLogs)
            {
                Debug.Log(
                    $"[SaveGame] Load slot {normalizedSlot}: containerBytes={preprocess.containerBytes} " +
                    $"gameJsonBytes={preprocess.uncompressedBytes}");
            }

            SaveGameData data = null;
            double deserializeStartMs = PerfNowMs();
            LogLoadPerf(normalizedSlot, "deserialize_json.begin", deserializeStartMs, deserializeStartMs - asyncStartMs);
            try
            {
                SaveContainerManifest manifest = JsonUtility.FromJson<SaveContainerManifest>(preprocess.manifestJson);
                if (manifest == null || manifest.containerVersion != 1)
                    throw new InvalidDataException("Versao de container ausente ou nao suportada.");

                data = JsonUtility.FromJson<SaveGameData>(preprocess.json);
                MigrateFogObserverSlotIdentity(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGame] Falha ao desserializar save no main thread: {ex.Message}");
                cursorController?.PlayErrorSfx();
                PanelDialogController.ClearExternalText();
                yield break;
            }
            LogLoadPerf(normalizedSlot, "deserialize_json.end", deserializeStartMs, PerfNowMs() - asyncStartMs);

            if (data == null)
            {
                Debug.LogError("[SaveGame] Falha ao desserializar save.");
                cursorController?.PlayErrorSfx();
                PanelDialogController.ClearExternalText();
                yield break;
            }

            panelRodada?.SetLoadingTeam((TeamId)data.activeTeamId, data.currentTurn);

            string currentScene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrWhiteSpace(data.sceneName) && !string.Equals(data.sceneName, currentScene, StringComparison.Ordinal))
            {
                if (blockCrossSceneLoad)
                {
                    cursorController?.PlayErrorSfx();
                    Debug.LogWarning($"[SaveGame] Load bloqueado: save da cena '{data.sceneName}', cena atual '{currentScene}'.");
                    PanelDialogController.ClearExternalText();
                    yield break;
                }

                Debug.LogWarning($"[SaveGame] Save foi criado na cena '{data.sceneName}', cena atual: '{currentScene}'.");
            }

            double prepareStartMs = PerfNowMs();
            LogLoadPerf(normalizedSlot, "prepare_runtime.begin", prepareStartMs, prepareStartMs - asyncStartMs);
            PrepareRuntimeForLoad();
            LogLoadPerf(normalizedSlot, "prepare_runtime.end", prepareStartMs, PerfNowMs() - asyncStartMs);

            // LoadRoutine controla loadInProgress e encerra o indicador no fim.
            yield return StartCoroutine(LoadRoutine(data, normalizedSlot));

            double replayRestoreStartMs = PerfNowMs();
            LogLoadPerf(normalizedSlot, "restore_replay.begin", replayRestoreStartMs, replayRestoreStartMs - asyncStartMs);
            RestoreReplayFromContainer(preprocess.replayJson);
            LogLoadPerf(normalizedSlot, "restore_replay.end", replayRestoreStartMs, PerfNowMs() - asyncStartMs);

            double jogadasRestoreStartMs = PerfNowMs();
            LogLoadPerf(normalizedSlot, "restore_jogadas.begin", jogadasRestoreStartMs, jogadasRestoreStartMs - asyncStartMs);
            RestoreJogadasFromContainer(preprocess.jogadasJson);
            LogLoadPerf(normalizedSlot, "restore_jogadas.end", jogadasRestoreStartMs, PerfNowMs() - asyncStartMs);
            if (lastLoadRoutineSucceeded && panelRodada != null)
            {
                int playerNumber = matchController != null ? matchController.ActivePlayerListIndex + 1 : 1;
                double presentationStartMs = PerfNowMs();
                LogLoadPerf(normalizedSlot, "turn_presentation.begin", presentationStartMs, presentationStartMs - asyncStartMs);
                yield return panelRodada.ReleaseLoadingPresentation(
                    (TeamId)data.activeTeamId,
                    Mathf.Max(1, playerNumber),
                    data.currentTurn,
                    () => LogLoadPerf(
                        normalizedSlot,
                        "turn_button.ready",
                        presentationStartMs,
                        PerfNowMs() - asyncStartMs));
                LogLoadPerf(normalizedSlot, "turn_button.confirmed", presentationStartMs, PerfNowMs() - asyncStartMs);
                matchController?.ReleaseHotSeatGateAfterLoad();
                loadingPresentationReleased = true;
                matchMusicAudioManager?.PrepareForMatchStart(forceRestartPlayback: true);
            }
            LogLoadPerf(normalizedSlot, "load_async.end", asyncStartMs, PerfNowMs() - asyncStartMs);
        }
        finally
        {
            if (!loadingPresentationReleased && panelRodada != null && panelRodada.IsPresenting)
                panelRodada.CancelLoadingPresentation();
            if (!loadingPresentationReleased)
                matchMusicAudioManager?.EndTurnTransition();
            // Em casos de erro antes de entrar no LoadRoutine, garante desbloqueio.
            loadInProgress = false;
            IsAnyLoadInProgress = false;
            mainMenuLoadTransitionActive = false;
        }
    }

    private static LoadPreprocessResult PreprocessLoadData(string path)
    {
        LoadPreprocessResult result = new LoadPreprocessResult();
        try
        {
            if (!TryReadSaveContainer(
                    path,
                    out string manifestJson,
                    out string json,
                    out string replayJson,
                    out string jogadasJson,
                    out int containerBytes,
                    out int uncompressedBytes,
                    out string readError))
            {
                result.error = readError;
                return result;
            }

            result.manifestJson = manifestJson;
            result.json = json;
            result.replayJson = replayJson;
            result.jogadasJson = jogadasJson;
            result.containerBytes = containerBytes;
            result.uncompressedBytes = uncompressedBytes;
            if (string.IsNullOrWhiteSpace(json))
                result.error = "JSON do save vazio apos leitura/decompress.";
        }
        catch (Exception ex)
        {
            result.error = ex.Message;
        }

        return result;
    }

    private void ShowLoadingIndicator(string dialogId, string fallback)
    {
        string text = ResolveDialog(dialogId, fallback);
        PanelDialogController.TrySetExternalText(text);
    }

    [ContextMenu("Clear Slot 1")]
    public void ClearSlot1() => ClearSlot(1);

    [ContextMenu("Clear Slot 2")]
    public void ClearSlot2() => ClearSlot(2);

    [ContextMenu("Clear Slot 3")]
    public void ClearSlot3() => ClearSlot(3);

    public void ClearSlot(int slotIndex)
    {
        int normalizedSlot = NormalizeSlot(slotIndex);
        string path = ResolveReadableSlotPath(normalizedSlot);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveGame] Slot {normalizedSlot} ja esta vazio.");
            return;
        }

        File.Delete(path);
        DeleteIfExists(path + ".tmp");
        DeleteIfExists(path + ".bak");
        Debug.Log($"[SaveGame] Slot {normalizedSlot} limpo: {path}");
    }

    private void PrepareRuntimeForLoad()
    {
        // Load pode ser disparado no meio de subfluxos (embarque/suprir/fundir etc).
        // Antes de restaurar snapshot, limpa qualquer estado transiente pendente.
        animationManager?.StopCurrentMovement();
        if (turnStateManager != null)
        {
            turnStateManager.StopAllCoroutines();
            turnStateManager.ResetCommandServiceReplayTransientState();
            turnStateManager.ForceNeutral();
        }

        cursorController?.ClearRuntimeInputLocksAfterLoad();
    }

    private static void LogSaveDiagnostics(int slotIndex, string json, long containerSizeBytes)
    {
        int uncompressedBytes = string.IsNullOrEmpty(json) ? 0 : Encoding.UTF8.GetByteCount(json);
        float uncompressedKb = uncompressedBytes / 1024f;
        float containerKb = containerSizeBytes / 1024f;
        float compressionRatio = uncompressedBytes > 0 ? (float)containerSizeBytes / uncompressedBytes : 0f;

        Debug.Log(
            $"[SaveGame][Diagnostics] slot={slotIndex} " +
            $"jsonBytes={uncompressedBytes} jsonKB={uncompressedKb:F2} " +
            $"containerBytes={containerSizeBytes} containerKB={containerKb:F2} compressionRatio={compressionRatio:F3}");
    }

    private static bool TryReadSaveContainer(
        string path,
        out string manifestJson,
        out string gameJson,
        out string replayJson,
        out string jogadasJson,
        out int containerBytes,
        out int uncompressedBytes,
        out string error)
    {
        manifestJson = string.Empty;
        gameJson = string.Empty;
        replayJson = string.Empty;
        jogadasJson = string.Empty;
        containerBytes = 0;
        uncompressedBytes = 0;
        error = string.Empty;

        try
        {
            containerBytes = checked((int)new FileInfo(path).Length);
            using (FileStream stream = File.OpenRead(path))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
            {
                manifestJson = ReadRequiredContainerEntry(archive, SaveContainerManifestEntry);
                gameJson = ReadRequiredContainerEntry(archive, SaveContainerGameEntry);
                replayJson = ReadOptionalContainerEntry(archive, SaveContainerReplayEntry);
                jogadasJson = ReadOptionalContainerEntry(archive, SaveContainerJogadasEntry);
            }

            uncompressedBytes = string.IsNullOrEmpty(gameJson) ? 0 : Encoding.UTF8.GetByteCount(gameJson);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void TryStartPendingMainMenuLoadForActiveScene()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!webGLStorageReady)
            return;
#endif
        if (pendingMainMenuLoad == null)
            return;

        string currentScene = SceneManager.GetActiveScene().name;
        if (!string.Equals(currentScene, pendingMainMenuLoad.sceneName, StringComparison.Ordinal))
            return;

        int pendingSlot = pendingMainMenuLoad.slotIndex;
        string pendingScene = pendingMainMenuLoad.sceneName;
        pendingMainMenuLoad = null;
        StartCoroutine(LoadPendingMainMenuSlotNextFrame(pendingSlot, pendingScene));
    }

    private IEnumerator LoadPendingMainMenuSlotNextFrame(int slotIndex, string sceneName)
    {
        // Aguarda 1 frame para garantir inicializacao dos managers da cena destino.
        yield return null;

        if (verboseLogs)
            Debug.Log($"[SaveGame] MainMenu pending load: slot {slotIndex} na cena '{sceneName}'.");

        LoadSlot(slotIndex);
    }

    private IEnumerator RestoreConstructionsBatched(
        SaveGameData data,
        Dictionary<int, ConstructionManager> existingConstructionsById,
        HashSet<ConstructionManager> restoredConstructions,
        Action<int> setMaxId,
        Action<string> setError)
    {
        const int batchSize = int.MaxValue;
        int maxId = 0;
        int count = data?.constructions?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            bool failed = false;
            try
            {
                ConstructionSaveData saved = data.constructions[i];
                if (saved != null && !string.IsNullOrWhiteSpace(saved.constructionId))
                {
                    if (!constructionSpawner.TryGetConstructionData(saved.constructionId, out ConstructionData constructionData) || constructionData == null)
                    {
                        Debug.LogWarning($"[SaveGame] Construcao nao encontrada no DB: {saved.constructionId}");
                    }
                    else
                    {
                        ConstructionManager manager = null;
                        if (existingConstructionsById != null
                            && saved.instanceId > 0
                            && existingConstructionsById.TryGetValue(saved.instanceId, out ConstructionManager existing)
                            && existing != null
                            && string.Equals(existing.ConstructionId, saved.constructionId, StringComparison.OrdinalIgnoreCase))
                        {
                            manager = existing;
                        }

                        if (manager == null)
                        {
                            Vector3 world = new Vector3(saved.worldX, saved.worldY, 0f);
                            GameObject go = constructionSpawner.Spawn(constructionData, (TeamId)saved.teamId, world, Quaternion.identity);
                            manager = go != null ? go.GetComponent<ConstructionManager>() : null;
                        }

                        if (manager != null)
                        {
                            if (!manager.gameObject.activeSelf)
                                manager.gameObject.SetActive(true);
                            SaveDataMapper.ApplyConstructionSaveData(
                                manager,
                                saved,
                                BuildSiteRuntimeFromSaveData,
                                constructionData);
                            restoredConstructions?.Add(manager);
                            maxId = Mathf.Max(maxId, saved.instanceId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                setError?.Invoke(ex.Message);
                failed = true;
            }
            if (failed)
                yield break;

            if ((i + 1) % batchSize == 0)
                yield return null;
        }
        setMaxId?.Invoke(maxId);
        yield break;
    }

    private IEnumerator RestoreUnitsBatched(
        SaveGameData data,
        Dictionary<int, UnitManager> unitsById,
        Action<int> setMaxId,
        Action<string> setError)
    {
        const int batchSize = int.MaxValue;
        int maxId = 0;
        int count = data?.units?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            bool failed = false;
            try
            {
                UnitSaveData saved = data.units[i];
                if (saved != null && !string.IsNullOrWhiteSpace(saved.unitId))
                {
                    if (!unitSpawner.TryGetUnitData(saved.unitId, out UnitData unitData) || unitData == null)
                    {
                        Debug.LogWarning($"[SaveGame] Unidade nao encontrada no DB: {saved.unitId}");
                    }
                    else
                    {
                        Vector3 world = new Vector3(saved.worldX, saved.worldY, 0f);
                        GameObject go = unitSpawner.Spawn(
                            unitData,
                            (TeamId)saved.teamId,
                            world,
                            Quaternion.identity,
                            enforceSpawnOccupancyRule: false);
                        UnitManager manager = go != null ? go.GetComponent<UnitManager>() : null;
                        if (manager != null)
                        {
                            SaveDataMapper.ApplyUnitSaveData(manager, saved);
                            unitsById[saved.instanceId] = manager;
                            maxId = Mathf.Max(maxId, saved.instanceId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                setError?.Invoke(ex.Message);
                failed = true;
            }
            if (failed)
                yield break;

            if ((i + 1) % batchSize == 0)
                yield return null;
        }
        setMaxId?.Invoke(maxId);
        yield break;
    }

    private IEnumerator RestoreEmbarkedUnitsBatched(
        SaveGameData data,
        Dictionary<int, UnitManager> unitsById,
        Action<string> setError)
    {
        const int batchSize = int.MaxValue;
        int count = data?.units?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            bool failed = false;
            try
            {
                UnitSaveData saved = data.units[i];
                if (saved != null && saved.isEmbarked && saved.transporterInstanceId > 0 &&
                    unitsById.TryGetValue(saved.instanceId, out UnitManager passenger) && passenger != null &&
                    unitsById.TryGetValue(saved.transporterInstanceId, out UnitManager transporter) && transporter != null &&
                    !transporter.TryEmbarkPassengerInSlot(passenger, saved.transporterSlotIndex, out string reason) && verboseLogs)
                {
                    Debug.LogWarning($"[SaveGame] Falha embarque {saved.instanceId}->{saved.transporterInstanceId}: {reason}");
                }
            }
            catch (Exception ex)
            {
                setError?.Invoke(ex.Message);
                failed = true;
            }
            if (failed)
                yield break;

            if ((i + 1) % batchSize == 0)
                yield return null;
        }
        yield break;
    }

    private IEnumerator LoadRoutine(SaveGameData data, int loadedSlot)
    {
        loadInProgress = true;
        IsAnyLoadInProgress = true;
        string stage = "init";
        bool coreLoadSucceeded = false;
        lastLoadRoutineSucceeded = false;
        bool suppressedFogRefresh = false;
        double routineStartMs = PerfNowMs();
        LogLoadPerf(loadedSlot, "load_routine.start", routineStartMs, 0d);

        if (matchController != null)
        {
            matchController.SuppressFogOfWarRefresh = true;
            suppressedFogRefresh = true;
        }

        Dictionary<int, ConstructionManager> existingConstructionsById = CollectSceneConstructionsById();
        HashSet<ConstructionManager> restoredConstructions = new HashSet<ConstructionManager>();

        // Unidades sao reconstruidas; construcoes do mapa preservam sua identidade e referencias.
        stage = "clear-runtime";
        double clearRuntimeStartMs = PerfNowMs();
        LogLoadPerf(loadedSlot, "clear_runtime.begin", clearRuntimeStartMs, clearRuntimeStartMs - routineStartMs);
        ClearCurrentRuntime(preserveConstructions: true);
        yield return null;
        LogLoadPerf(loadedSlot, "clear_runtime.end", clearRuntimeStartMs, PerfNowMs() - routineStartMs);

        // Restaura os slots antes dos spawns. UnitSpawner/ConstructionSpawner
        // resolvem slotIndex a partir do TeamId; com a cena-base ainda Green/Red,
        // saves Yellow/Blue perderiam o vinculo e virariam objetos sem slot.
        stage = "restore-match-slots-before-spawn";
        if (matchController != null)
            RestoreMatchPlayers(data);

        // Mantem lookup estavel durante todas as etapas de restauracao do snapshot.
        Dictionary<int, UnitManager> unitsById = new Dictionary<int, UnitManager>();

        int maxUnitId = 0;
        int maxConstructionId = 0;
        string batchedRestoreError = string.Empty;

        stage = "spawn-constructions";
        double spawnConstructionsStartMs = PerfNowMs();
        LogLoadPerf(loadedSlot, "restore_constructions.begin", spawnConstructionsStartMs, spawnConstructionsStartMs - routineStartMs);
        yield return RestoreConstructionsBatched(
            data,
            existingConstructionsById,
            restoredConstructions,
            value => maxConstructionId = value,
            error => batchedRestoreError = error);
        DestroyConstructionsNotInSnapshot(existingConstructionsById, restoredConstructions);
        LogLoadPerf(loadedSlot, "restore_constructions.end", spawnConstructionsStartMs, PerfNowMs() - routineStartMs);
        SectorManager.RequestRebuildFromActiveConstructions("post-restore-constructions");

        if (string.IsNullOrEmpty(batchedRestoreError))
        {
            stage = "spawn-units";
            double spawnUnitsStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "restore_units.begin", spawnUnitsStartMs, spawnUnitsStartMs - routineStartMs);
            yield return RestoreUnitsBatched(data, unitsById, value => maxUnitId = value, error => batchedRestoreError = error);
            LogLoadPerf(loadedSlot, "restore_units.end", spawnUnitsStartMs, PerfNowMs() - routineStartMs);
        }

        if (string.IsNullOrEmpty(batchedRestoreError))
        {
            stage = "restore-embarked";
            double restoreEmbarkedStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "restore_embarked.begin", restoreEmbarkedStartMs, restoreEmbarkedStartMs - routineStartMs);
            yield return RestoreEmbarkedUnitsBatched(data, unitsById, error => batchedRestoreError = error);
            LogLoadPerf(loadedSlot, "restore_embarked.end", restoreEmbarkedStartMs, PerfNowMs() - routineStartMs);
        }

        if (!string.IsNullOrEmpty(batchedRestoreError))
        {
            Debug.LogError($"[SaveGame] Falha no load (etapa: {stage}): {batchedRestoreError}");
        }
        else try
        {

            stage = "sync-ids";
            double syncIdsStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "sync_ids.begin", syncIdsStartMs, syncIdsStartMs - routineStartMs);
            unitSpawner.SetNextIdAfterMax(maxUnitId);
            constructionSpawner.SetNextIdAfterMax(maxConstructionId);
            LogLoadPerf(loadedSlot, "sync_ids.end", syncIdsStartMs, PerfNowMs() - routineStartMs);

            stage = "restore-match";
            double restoreMatchStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "restore_match_state.begin", restoreMatchStartMs, restoreMatchStartMs - routineStartMs);
            if (matchController != null)
            {
                RestoreMatchPlayers(data);
                matchController.SetEconomyEnabled(data.economyEnabled);
                matchController.SetCurrentTurn(data.currentTurn);
                PlayerSlotId restoredActiveSlot = PlayerSlotId.FromIndex(data.activeSlotIndex);
                if (matchController.IsValidPlayerSlot(restoredActiveSlot))
                    matchController.SetActiveSlotWithoutTurnStart(restoredActiveSlot);
                else
                    matchController.SetActiveTeamIdWithoutTurnStart(data.activeTeamId);
                // Reaplica economia/flip apos SetActiveTeamIdWithoutTurnStart para evitar side effects
                // de credito no inicio do turno sobrescrever o snapshot salvo.
                RestoreMatchPlayers(data);
                matchController.RegisterCurrentlyOwnedBuildings();
            }
            LogLoadPerf(loadedSlot, "restore_match_state.end", restoreMatchStartMs, PerfNowMs() - routineStartMs);

            // Reaplica estado de acted apos MatchController liberar equipe ativa.
            stage = "restore-unit-flags";
            double restoreUnitFlagsStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "restore_unit_flags.begin", restoreUnitFlagsStartMs, restoreUnitFlagsStartMs - routineStartMs);
            if (data.units != null)
            {
                for (int i = 0; i < data.units.Count; i++)
                {
                    UnitSaveData saved = data.units[i];
                    if (saved == null || !unitsById.TryGetValue(saved.instanceId, out UnitManager unit) || unit == null)
                        continue;

                    SaveDataMapper.ApplyUnitTurnFlagsFromSaveData(unit, saved);
                }
            }
            LogLoadPerf(loadedSlot, "restore_unit_flags.end", restoreUnitFlagsStartMs, PerfNowMs() - routineStartMs);

            stage = "restore-unit-active-states";
            RestoreSavedUnitActiveStates(data, unitsById);

            stage = "restore-turn-briefing-ledger";
            matchController?.RestoreTurnBriefingLedger(data.turnBriefingEvents);
            turnStateManager?.RestoreTurnBriefingReportSaveData(
                data.turnBriefingReportLines,
                data.activeSlotIndex >= 0 ? data.activeSlotIndex : matchController.ActiveSlotId.Value);

            stage = "restore-ai-objective-plans";
            AIIntelLedger.Restore(data.aiIntelLedgers);
            if (data.aiObjectivePlans != null && data.aiObjectivePlans.Count > 0)
            {
                ObjectiveManager.RestoreSaveData(data.aiObjectivePlans);
            }
            else
            {
                ObjectiveManager.RestoreSaveData(null);
                ClearLoadedAIAssignmentBadges(unitsById);
            }

            if (aiController != null)
            {
                if (data.aiDifficultySaved)
                    aiController.RestoreDifficultyFromSave(
                        data.aiEasyMode, data.aiHardMode,
                        data.aiConscriptionWhenLosing
                            || (data.version < 10 && data.aiHardMode && !data.aiConscriptionDoctrine),
                        data.aiConscriptionDoctrine);
                aiController.MassacrePhaseActive = data.aiMassacrePhase;
                TeamId restoredAiTeam = (TeamId)data.aiRuntimeTeamId;
                PlayerSlotId restoredAiSlot = PlayerSlotId.FromIndex(data.aiRuntimeSlotIndex);
                if (matchController != null && !matchController.IsValidPlayerSlot(restoredAiSlot))
                {
                    if (matchController.TryGetUniqueSlotForTeam(restoredAiTeam, out PlayerSlotId migratedSlot))
                        restoredAiSlot = migratedSlot;
                    else if (matchController.IsActiveTeamAI())
                        restoredAiSlot = matchController.ActiveSlotId;
                }
                if (matchController != null && matchController.IsValidPlayerSlot(restoredAiSlot))
                    restoredAiTeam = matchController.GetVisualTeamForSlot(restoredAiSlot);
                aiController.RestoreAIRuntimeState(
                    data.aiRuntimeActive,
                    restoredAiSlot,
                    restoredAiTeam,
                    data.aiRuntimeTurnNumber,
                    data.aiRuntimeStage);
            }

            stage = "restore-ai-planner";
            double restoreAiPlannerStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "restore_ai_planner.begin", restoreAiPlannerStartMs, restoreAiPlannerStartMs - routineStartMs);
            /*if (aiPlayerController != null)
            {
                aiPlayerController.RestorePlannerSaveData(data.aiPlannerState);
                aiPlayerController.DebugLogRestoredPlannerSnapshots("post-restore");
            }*/
            LogLoadPerf(loadedSlot, "restore_ai_planner.end", restoreAiPlannerStartMs, PerfNowMs() - routineStartMs);

            stage = "apply-conservative-fog-visibility";
            double conservativeFogStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "apply_conservative_fog.begin", conservativeFogStartMs, conservativeFogStartMs - routineStartMs);
            matchController?.ApplyConservativeFogVisibilityForLoading();
            LogLoadPerf(loadedSlot, "apply_conservative_fog.end", conservativeFogStartMs, PerfNowMs() - routineStartMs);

            coreLoadSucceeded = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveGame] Falha no load (etapa: {stage}): {ex.Message}\n{ex.StackTrace}");
        }

        if (coreLoadSucceeded)
        {
            double planningImportStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "planning_import.begin", planningImportStartMs, planningImportStartMs - routineStartMs);
            if (planningManager != null)
                planningManager.ImportPlanningData(data.planningConfig, data.rallyPoints, data.rallyAssignments);
            LogLoadPerf(loadedSlot, "planning_import.end", planningImportStartMs, PerfNowMs() - routineStartMs);

            stage = "reset-runtime-input";
            double resetInputStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "reset_runtime_input.begin", resetInputStartMs, resetInputStartMs - routineStartMs);
            turnStateManager?.ForceNeutral();
            foreach (UnitManager restoredUnit in unitsById.Values)
                restoredUnit?.EndTemporaryFogTraversalVisual();
            cursorController?.ClearRuntimeInputLocksAfterLoad();
            cursorController?.SnapToCurrentCell();
            PanelDialogController.ClearExternalText();
            LogLoadPerf(loadedSlot, "reset_runtime_input.end", resetInputStartMs, PerfNowMs() - routineStartMs);

            if (matchController != null)
            {
                // Ensure spawned constructions/units finished enable-cycle and static lists are up-to-date.
                double refreshFogStartMs = PerfNowMs();
                LogLoadPerf(loadedSlot, "refresh_fog_after_load.begin", refreshFogStartMs, refreshFogStartMs - routineStartMs);
                yield return null;
                matchController.SuppressFogOfWarRefresh = false;
                suppressedFogRefresh = false;
                // O cache salvo e apenas uma fotografia de runtime e pode ficar
                // incompatível com unidades/construcoes reidratadas. Recalcula o
                // snapshot confirmado para nunca combinar unidade visivel com hex
                // apenas conhecido depois do load.
                matchController.RefreshFogOfWarForActiveTeam();
                LogLoadPerf(loadedSlot, "refresh_fog_after_load.end", refreshFogStartMs, PerfNowMs() - routineStartMs);

                // Etapa 5/6: o cold refresh continua sendo a verdade. A fotografia
                // salva e somente comparada com o resultado recalculado; nunca e
                // aplicada ao runtime neste ponto.
                if (data.fogSourceContributions != null && data.fogSourceContributions.Count > 0)
                {
                    double verifyFogCacheStartMs = PerfNowMs();
                    LogLoadPerf(loadedSlot, "verify_fog_cache.begin", verifyFogCacheStartMs, verifyFogCacheStartMs - routineStartMs);
                    matchController.VerifyFogSourceContributionsFromSave(data.fogSourceContributions);
                    LogLoadPerf(loadedSlot, "verify_fog_cache.end", verifyFogCacheStartMs, PerfNowMs() - routineStartMs);
                }
            }

            // A ocupacao visual depende da visibilidade final da unidade. Recalcular somente
            // depois do FOW e do planejamento evita manter a barra da construcao sob a unidade.
            stage = "refresh-construction-occupancy";
            RefreshConstructionOccupancyAfterLoad(unitsById);

            cursorController?.PlayBeepSfx();
            if (verboseLogs)
                Debug.Log($"[SaveGame] Load concluido: {data.units?.Count ?? 0} unidades, {data.constructions?.Count ?? 0} construcoes.");
            string loadedText = ResolveDialog(
                "dialog.load_status.success",
                ResolveHelper("helper.load_status.success", "Jogo do slot <slot> carregado"),
                new Dictionary<string, string> { { "slot", loadedSlot.ToString() } });
            PanelDialogController.TrySetTransientText(loadedText, 2.2f);
            SchedulePostLoadThreatWarmup();
        }

        if (suppressedFogRefresh && matchController != null)
            matchController.SuppressFogOfWarRefresh = false;

        if (coreLoadSucceeded)
        {
            // O time ativo e seus efeitos visuais ja foram aplicados por
            // SetActiveTeamIdWithoutTurnStart, e as flags vieram do snapshot.
            // Nao simule um novo inicio de turno aqui: isso zera o estado salvo,
            // redispara FOW/eventos globais e exige uma segunda restauracao inteira.
            double afterLoadEventsStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "after_load_events.begin", afterLoadEventsStartMs, afterLoadEventsStartMs - routineStartMs);
            OnAfterLoadSuccess?.Invoke();
            LogLoadPerf(loadedSlot, "after_load_events.end", afterLoadEventsStartMs, PerfNowMs() - routineStartMs);
            lastLoadRoutineSucceeded = true;
        }
        LogLoadPerf(loadedSlot, "load_routine.end", routineStartMs, PerfNowMs() - routineStartMs);
    }

    private void SchedulePostLoadThreatWarmup()
    {
        if (turnStateManager == null)
            return;

        if (postLoadThreatWarmupRoutine != null)
            StopCoroutine(postLoadThreatWarmupRoutine);

        if (!enableThreatCacheWarmupOnLoad)
        {
            postLoadThreatWarmupRoutine = null;
            if (verboseLogs)
                Debug.Log("[SaveGame] Warm-up de hotzone no load desativado.");
            return;
        }

        postLoadThreatWarmupRoutine = StartCoroutine(PostLoadThreatWarmupRoutine());
    }

    private IEnumerator PostLoadThreatWarmupRoutine()
    {
        // Deixa o primeiro frame renderizar apos o load antes de aquecer o cache.
        yield return null;

        if (turnStateManager == null)
        {
            postLoadThreatWarmupRoutine = null;
            yield break;
        }

        bool skipHotzoneWarmup = matchController != null && matchController.EnableTotalWar;
        if (skipHotzoneWarmup)
        {
            if (verboseLogs)
                Debug.Log("[SaveGame] Skip loading hotzone cache: Total War ativo.");
            postLoadThreatWarmupRoutine = null;
            yield break;
        }

        yield return turnStateManager.WarmUpThreatCacheFromScene((processed, total) =>
        {
            if (!verboseLogs)
                return;

            if (total <= 0 || processed == 0 || processed == total || processed % 10 == 0)
                Debug.Log($"[SaveGame] Warm-up hotzone cache (post-load): {processed}/{total}");
        });

        postLoadThreatWarmupRoutine = null;
    }

    private SaveGameData BuildSaveData()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        MatchStateSaveData matchState = SaveDataMapper.BuildMatchStateSaveData(matchController);
        SaveGameData data = new SaveGameData
        {
            sceneName = activeScene.name,
            savedAtUtcTicks = DateTime.UtcNow.Ticks,
            currentTurn = matchState.currentTurn,
            activeTeamId = matchState.activeTeamId,
            activeSlotIndex = matchState.activeSlotIndex,
            includeNeutralTeam = matchState.includeNeutralTeam,
            economyEnabled = matchState.economyEnabled,
            victoryStarsEnabled = matchState.victoryStarsEnabled,
            victoryStarsToWin = matchState.victoryStarsToWin,
            hasVictoryWinner = matchState.hasVictoryWinner,
            victoryWinnerTeamId = matchState.victoryWinnerTeamId,
            victoryWinnerSlotIndex = matchState.victoryWinnerSlotIndex,
            players = matchState.players != null ? matchState.players : new List<MatchPlayerSaveData>(),
            capturedBuildingHistory = new List<TeamCapturedBuildingSaveData>(),
            victoryStars = matchState.victoryStars != null ? matchState.victoryStars : new List<MatchVictoryStarSaveData>(),
            fogObserverSlotIndex = int.MinValue,
            fogCacheTeamId = int.MinValue,
            fogVisibleContributorsByCell = new List<FogCellContributorSaveData>(),
            fogUnitVisibilityByCacheIndex = new List<FogUnitVisibilitySaveData>(),
            fogSourceContributions = new List<FogSourceContributionSaveData>(),
            fogExploredCellsBySlot = new List<TeamExploredCellsSaveData>(),
            fogExploredCellsByTeam = new List<TeamExploredCellsSaveData>(),
            fogConstructionMemory = new List<FogConstructionMemorySaveData>(),
            aiObjectivePlans = ObjectiveManager.BuildSaveData(),
            aiRuntimeActive = aiController != null && aiController.IsAIRuntimeActive,
            aiRuntimeSlotIndex = aiController != null ? aiController.CurrentAISlotId.Value : -1,
            aiRuntimeTeamId = aiController != null ? (int)aiController.CurrentAITeam : (int)TeamId.Neutral,
            aiRuntimeTurnNumber = aiController != null ? aiController.CurrentAITurnNumber : 0,
            aiRuntimeStage = aiController != null ? aiController.CurrentAIStage : 0,
            aiIntelLedgers = AIIntelLedger.BuildSaveData()
        };

        if (aiController != null)
        {
            aiController.CaptureDifficultyForSave(
                out bool aiEasy, out bool aiHard,
                out bool aiConscriptionWhenLosing, out bool aiConscriptionAlways);
            data.aiDifficultySaved = true;
            data.aiEasyMode = aiEasy;
            data.aiHardMode = aiHard;
            data.aiConscriptionWhenLosing = aiConscriptionWhenLosing;
            data.aiConscriptionDoctrine = aiConscriptionAlways;
            data.aiMassacrePhase = aiController.MassacrePhaseActive;
        }

        // Jornal do Comandante: eventos pendentes entre turnos sao estado.
        if (matchController != null && matchController.TurnBriefingLedger != null)
            data.turnBriefingEvents.AddRange(matchController.TurnBriefingLedger);
        if (turnStateManager != null)
            data.turnBriefingReportLines.AddRange(turnStateManager.BuildTurnBriefingReportSaveData());

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if (unit.gameObject.scene != activeScene)
                continue;

            UnitSaveData item = SaveDataMapper.BuildUnitSaveData(unit);
            if (item != null)
                data.units.Add(item);
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
                continue;
            if (construction.gameObject.scene != activeScene)
                continue;

            ConstructionSaveData item = SaveDataMapper.BuildConstructionSaveData(construction);
            if (item != null)
                data.constructions.Add(item);
        }

        if (planningManager != null)
        {
            planningManager.ExportPlanningData(out PlanningConfigSaveData planningConfig, out List<RallyPointSaveData> planningPoints, out List<RallyAssignmentSaveData> planningAssignments);
            data.planningConfig = planningConfig;
            data.rallyPoints = planningPoints;
            data.rallyAssignments = planningAssignments;
        }

        if (matchController != null)
        {
            matchController.ExportCapturedBuildingHistory(data.capturedBuildingHistory);
            matchController.ExportFogExplorationMemory(data.fogExploredCellsBySlot);
            matchController.ExportFogConstructionMemory(data.fogConstructionMemory);
            matchController.ExportFogRuntimeCacheForSave(
                out data.fogObserverSlotIndex,
                data.fogVisibleContributorsByCell,
                data.fogUnitVisibilityByCacheIndex);
            matchController.ExportFogSourceContributionsForSave(data.fogSourceContributions);
        }

        // if (aiPlayerController != null)
        //     data.aiPlannerState = aiPlayerController.BuildPlannerSaveData();

        return data;
    }

    private static void MigrateFogObserverSlotIdentity(SaveGameData data)
    {
        if (data == null)
            return;

        // O campo legado tinha nome de TeamId, mas ExportFogRuntimeCacheForSave
        // sempre gravou ActiveSlotId.Value. Copiar diretamente preserva dois slots
        // que compartilham a mesma cor; jamais migrar este valor via TeamId.
        if (data.fogObserverSlotIndex < 0 && data.fogCacheTeamId >= 0)
            data.fogObserverSlotIndex = data.fogCacheTeamId;
        data.fogCacheTeamId = int.MinValue;

        if ((data.fogExploredCellsBySlot == null || data.fogExploredCellsBySlot.Count == 0) &&
            data.fogExploredCellsByTeam != null && data.fogExploredCellsByTeam.Count > 0)
        {
            data.fogExploredCellsBySlot = data.fogExploredCellsByTeam;
        }

        // Depois da migração, apenas o campo por slot participa do estado em memória
        // e de futuros hashes/saves.
        data.fogExploredCellsByTeam = new List<TeamExploredCellsSaveData>();
        data.fogSourceContributions ??= new List<FogSourceContributionSaveData>();
    }

    private void RestoreMatchPlayers(SaveGameData data)
    {
        if (data == null)
            return;

        MatchStateSaveData matchState = new MatchStateSaveData
        {
            includeNeutralTeam = data.includeNeutralTeam,
            activeSlotIndex = data.activeSlotIndex,
            players = data.players != null ? data.players : new List<MatchPlayerSaveData>(),
            victoryStars = data.victoryStars != null ? data.victoryStars : new List<MatchVictoryStarSaveData>(),
            victoryStarsEnabled = data.victoryStarsEnabled,
            victoryStarsToWin = data.victoryStarsToWin,
            hasVictoryWinner = data.hasVictoryWinner,
            victoryWinnerTeamId = data.victoryWinnerTeamId,
            victoryWinnerSlotIndex = data.victoryWinnerSlotIndex
        };
        SaveDataMapper.ApplyMatchStateSaveData(matchController, matchState);
        matchController?.ImportCapturedBuildingHistory(data.capturedBuildingHistory);
        matchController?.ImportFogExplorationMemory(data.fogExploredCellsBySlot);
        matchController?.ImportFogConstructionMemory(data.fogConstructionMemory);
    }

    private static void ClearLoadedAIAssignmentBadges(Dictionary<int, UnitManager> unitsById)
    {
        if (unitsById == null)
            return;

        foreach (KeyValuePair<int, UnitManager> pair in unitsById)
        {
            UnitManager unit = pair.Value;
            if (unit != null)
                unit.ClearAIAssignedPlan();
        }
    }

    private static void RestoreSavedUnitActiveStates(SaveGameData data, Dictionary<int, UnitManager> unitsById)
    {
        if (data?.units == null || unitsById == null)
            return;

        for (int i = 0; i < data.units.Count; i++)
        {
            UnitSaveData saved = data.units[i];
            if (saved == null || !unitsById.TryGetValue(saved.instanceId, out UnitManager unit) || unit == null)
                continue;

            if (unit.gameObject.activeSelf != saved.isActiveInHierarchy)
                unit.gameObject.SetActive(saved.isActiveInHierarchy);
        }
    }

    private static void RefreshConstructionOccupancyAfterLoad(Dictionary<int, UnitManager> unitsById)
    {
        if (unitsById != null)
        {
            foreach (UnitManager unit in unitsById.Values)
            {
                if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsDead || unit.IsEmbarked)
                    continue;

                Vector3Int cell = unit.CurrentCellPosition;
                cell.z = 0;
                UnitOccupancyRules.NotifyUnitOccupancyChanged(unit, cell, cell);
            }
        }

        Scene activeScene = SceneManager.GetActiveScene();
        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction != null && construction.gameObject.scene == activeScene)
                construction.RefreshRuntimeVisualState(force: true);
        }
    }

    private static Dictionary<int, ConstructionManager> CollectSceneConstructionsById()
    {
        Dictionary<int, ConstructionManager> result = new Dictionary<int, ConstructionManager>();
        Scene activeScene = SceneManager.GetActiveScene();
        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || construction.gameObject.scene != activeScene || construction.InstanceId <= 0)
                continue;

            if (!result.ContainsKey(construction.InstanceId))
                result.Add(construction.InstanceId, construction);
        }

        return result;
    }

    private static void DestroyConstructionsNotInSnapshot(
        Dictionary<int, ConstructionManager> existingConstructionsById,
        HashSet<ConstructionManager> restoredConstructions)
    {
        if (existingConstructionsById == null)
            return;

        foreach (ConstructionManager construction in existingConstructionsById.Values)
        {
            if (construction != null && (restoredConstructions == null || !restoredConstructions.Contains(construction)))
                Destroy(construction.gameObject);
        }
    }

    private void ClearCurrentRuntime(bool preserveConstructions = false)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] != null && units[i].gameObject.scene == activeScene)
                Destroy(units[i].gameObject);
        }

        if (!preserveConstructions)
        {
            ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < constructions.Length; i++)
            {
                if (constructions[i] != null && constructions[i].gameObject.scene == activeScene)
                    Destroy(constructions[i].gameObject);
            }
        }
    }

    private bool CanLoadNow(out string reason)
    {
        reason = string.Empty;

        if (turnStateManager != null &&
            turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral &&
            turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Loading)
        {
            reason = $"cursor em {turnStateManager.CurrentCursorState}; volte ao estado Neutral.";
            return false;
        }

        if (animationManager != null && animationManager.IsAnimatingMovement)
        {
            reason = "animacao em progresso.";
            return false;
        }

        return true;
    }

    private bool TryBlockReplayPersistence(string logMessage, string dialogId, string dialogFallback)
    {
        if (replayManager == null || !replayManager.IsReplaying)
            return false;

        cursorController?.PlayErrorSfx();
        CancelPrompt(clearDialogOverride: false);
        string dialog = ResolveDialog(dialogId, dialogFallback);
        PanelDialogController.TrySetTransientText(dialog, 2.8f);
        Debug.LogWarning(logMessage);
        return true;
    }

    private bool TryBlockPlanningSavePersistence(string logMessage, string dialogId, string dialogFallback)
    {
        bool planningActive =
            (planningManager != null && planningManager.IsPlanningModeActive)
            || (turnStateManager != null && turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Planning);
        if (!planningActive)
            return false;

        cursorController?.PlayErrorSfx();
        CancelPrompt(clearDialogOverride: false);
        string dialog = ResolveDialog(dialogId, dialogFallback);
        PanelDialogController.TrySetTransientText(dialog, 2.8f);
        Debug.LogWarning(logMessage);
        return true;
    }

    private bool TryBlockAircraftFuelDepletionPersistence(string logMessage, string dialogId, string dialogFallback)
    {
        if (turnStateManager == null || !turnStateManager.IsTurnStartFuelDepletionExecutionInProgress)
            return false;

        cursorController?.PlayErrorSfx();
        CancelPrompt(clearDialogOverride: false);
        string dialog = ResolveDialog(dialogId, dialogFallback);
        PanelDialogController.TrySetTransientText(dialog, 2.8f);
        Debug.LogWarning(logMessage);
        return true;
    }

    private string GetSlotPathFromTemplate(int slotIndex)
    {
        int normalizedSlot = NormalizeSlot(slotIndex);
        string safeSlot = ResolveFileNameDefaultTemplate();
        safeSlot = $"{safeSlot}_slot{normalizedSlot}";
        if (useSceneSpecificSlot)
        {
            Scene scene = SceneManager.GetActiveScene();
            string sceneName = string.IsNullOrWhiteSpace(scene.name) ? "Scene" : scene.name.Trim();
            string sceneIdentity = !string.IsNullOrWhiteSpace(scene.path) ? scene.path : sceneName;
            string sceneHash = ComputeShortStableHash(sceneIdentity);
            safeSlot = $"{safeSlot}_{sceneName}_{sceneHash}";
        }

        string fileName = SanitizeFileName(safeSlot) + SaveContainerExtension;
        return Path.Combine(ResolveSaveDirectory(), fileName);
    }

    private string ResolveWritableSlotPath(int slotIndex)
    {
        string existing = ResolveReadableSlotPath(slotIndex);
        if (File.Exists(existing))
            return existing;

        return GetSlotPathFromTemplate(slotIndex);
    }

    private string ResolveReadableSlotPath(int slotIndex)
    {
        int normalizedSlot = NormalizeSlot(slotIndex);
        string primaryPath = GetSlotPathFromTemplate(normalizedSlot);
        if (File.Exists(primaryPath))
            return primaryPath;

        string saveDir = ResolveSaveDirectory();
        try
        {
            if (Directory.Exists(saveDir))
            {
                string[] candidates = Directory.GetFiles(saveDir, $"*_slot{normalizedSlot}*{SaveContainerExtension}", SearchOption.TopDirectoryOnly);
                if (candidates != null && candidates.Length > 0)
                {
                    string latest = null;
                    DateTime latestWrite = DateTime.MinValue;
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        string current = candidates[i];
                        DateTime currentWrite = File.GetLastWriteTimeUtc(current);
                        if (latest == null || currentWrite > latestWrite)
                        {
                            latest = current;
                            latestWrite = currentWrite;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(latest))
                        return latest;
                }
            }
        }
        catch (Exception ex)
        {
            if (verboseLogs)
                Debug.LogWarning($"[SaveGame] Falha ao listar saves do slot {normalizedSlot}: {ex.Message}");
        }

        return primaryPath;
    }

    private string ResolveFileNameDefaultTemplate()
    {
        string template = string.IsNullOrWhiteSpace(fileNameDefault) ? "<Map>_<date>_<hour>" : fileNameDefault.Trim();
        Scene activeScene = SceneManager.GetActiveScene();
        string mapName = string.IsNullOrWhiteSpace(activeScene.name) ? "Map" : activeScene.name.Trim();
        DateTime localNow = DateTime.Now;
        string dateTag = localNow.ToString("yyyy-MM-dd");
        string hourTag = localNow.ToString("HH-mm");

        return template
            .Replace("<Map>", mapName)
            .Replace("<date>", dateTag)
            .Replace("<hour>", hourTag);
    }

    private string ResolveSaveDirectory()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string basePath = Application.persistentDataPath;
#else
        string basePath = string.IsNullOrWhiteSpace(customSaveDirectory)
            ? Application.persistentDataPath
            : customSaveDirectory.Trim();
#endif

        if (!Path.IsPathRooted(basePath))
            basePath = Path.Combine(Application.persistentDataPath, basePath);

        try
        {
            Directory.CreateDirectory(basePath);
            return basePath;
        }
        catch
        {
            Debug.LogWarning($"[SaveGame] Diretorio customizado inacessivel: '{basePath}'. Usando persistentDataPath.");
            return Application.persistentDataPath;
        }
    }

    private void EnsureDefaultSaveDirectoryConfigured()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        customSaveDirectory = Application.persistentDataPath;
#else
        if (string.IsNullOrWhiteSpace(customSaveDirectory))
            customSaveDirectory = Application.persistentDataPath;
#endif
    }

    public string GetResolvedSaveDirectory()
    {
        return ResolveSaveDirectory();
    }

    public void SetCustomSaveDirectory(string directoryPath)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        customSaveDirectory = Application.persistentDataPath;
#else
        customSaveDirectory = string.IsNullOrWhiteSpace(directoryPath)
            ? Application.persistentDataPath
            : directoryPath.Trim();
#endif
    }

    [ContextMenu("Log Save Directory")]
    public void LogSaveDirectory()
    {
        Debug.Log($"[SaveGame] Diretorio atual de save: {ResolveSaveDirectory()}");
    }

    /// <summary>
    /// Chamado por NewGamePanelController antes de LoadScene.
    /// Transporta o diretório de save e ativa auto-save na cena de destino.
    /// </summary>
    public static void SetupForNewGame(string saveDirectory)
    {
        pendingNewGameSaveDirectory = string.IsNullOrWhiteSpace(saveDirectory)
            ? string.Empty
            : saveDirectory.Trim();
    }

    private void ApplyPendingNewGame()
    {
        if (pendingNewGameSaveDirectory == null)
            return;

        if (!string.IsNullOrWhiteSpace(pendingNewGameSaveDirectory))
            SetCustomSaveDirectory(pendingNewGameSaveDirectory);

        pendingNewGameSaveDirectory = null;
        ResetUnitSpawnerNextIdForNewGame();
    }

    private void ResetUnitSpawnerNextIdForNewGame()
    {
        if (unitSpawner == null)
            unitSpawner = FindAnyObjectByType<UnitSpawner>();

        if (unitSpawner == null)
            return;

        int maxUnitId = unitSpawner.ResetNextIdAfterSceneUnits();

        if (verboseLogs)
            Debug.Log($"[SaveGame] NewGame: UnitSpawner ajustado apos maxUnitId={maxUnitId}.");
    }

    private static string ResolveHelper(string id, string fallback)
    {
        return PanelHelperController.ResolveHelperMessage(id, fallback);
    }

    private static string ResolveHelper(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        return PanelHelperController.ResolveHelperMessage(id, fallback, tokens);
    }

    private static string ResolveDialog(string id, string fallback)
    {
        return PanelDialogController.ResolveDialogMessage(id, fallback);
    }

    private static string ResolveDialog(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        return PanelDialogController.ResolveDialogMessage(id, fallback, tokens);
    }

    private SaveSlotMetadata ReadSlotMetadata(int slotIndex)
    {
        int normalizedSlot = NormalizeSlot(slotIndex);
        SaveSlotMetadata metadata = new SaveSlotMetadata
        {
            slotIndex = normalizedSlot,
            exists = false,
            sceneName = string.Empty,
            savedAtLocal = DateTime.Now,
            path = ResolveReadableSlotPath(normalizedSlot)
        };

        if (!File.Exists(metadata.path))
            return metadata;

        metadata.exists = true;

        DateTime fallbackLocalTime = File.GetLastWriteTime(metadata.path);
        if (TryReadContainerManifest(metadata.path, out SaveContainerManifest manifest, out string manifestError))
        {
            metadata.sceneName = manifest.sceneName ?? string.Empty;
            if (manifest.savedAtUtcTicks > 0L)
            {
                DateTime utc = new DateTime(manifest.savedAtUtcTicks, DateTimeKind.Utc);
                metadata.savedAtLocal = utc.ToLocalTime();
            }
            else
            {
                metadata.savedAtLocal = fallbackLocalTime;
            }

            if (string.IsNullOrWhiteSpace(metadata.sceneName))
                metadata.sceneName = "Mapa desconhecido";
            return metadata;
        }

        metadata.savedAtLocal = fallbackLocalTime;
        if (verboseLogs)
            Debug.LogWarning($"[SaveGame] Nao foi possivel ler manifesto do slot {normalizedSlot}: {manifestError}");

        if (string.IsNullOrWhiteSpace(metadata.sceneName))
            metadata.sceneName = "Mapa desconhecido";

        return metadata;
    }

    private string BuildReplayJsonForSave()
    {
        bool shouldPersistReplay =
            saveReplayData &&
            replayManager != null &&
            replayManager.IsRecording;
        if (!shouldPersistReplay)
            return string.Empty;

        ReplaySaveData replayData = replayManager.ExportReplaySaveData();
        return replayData != null ? JsonUtility.ToJson(replayData, false) : string.Empty;
    }

    private static string BuildJogadasJsonForSave()
    {
        JogadasManager manager = JogadasManager.EnsureInstance();
        return JsonUtility.ToJson(manager.log ?? new JogadasLog(), false);
    }

    private static void WriteSaveContainerAtomic(
        string savePath,
        SaveGameData data,
        string gameJson,
        string replayJson,
        string jogadasJson,
        string stateHash = null)
    {
        string tempPath = savePath + ".tmp";
        string backupPath = savePath + ".bak";
        DeleteIfExists(tempPath);
        DeleteIfExists(backupPath);
        try
        {
            SaveContainerManifest manifest = new SaveContainerManifest
            {
                saveVersion = data != null ? data.version : 0,
                sceneName = data?.sceneName ?? string.Empty,
                savedAtUtcTicks = data?.savedAtUtcTicks ?? 0L,
                hasReplay = !string.IsNullOrWhiteSpace(replayJson),
                hasJogadas = !string.IsNullOrWhiteSpace(jogadasJson),
                stateHash = stateHash ?? string.Empty
            };

            using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                WriteContainerEntry(archive, SaveContainerManifestEntry, JsonUtility.ToJson(manifest, false));
                WriteContainerEntry(archive, SaveContainerGameEntry, gameJson);
                if (manifest.hasReplay)
                    WriteContainerEntry(archive, SaveContainerReplayEntry, replayJson);
                if (manifest.hasJogadas)
                    WriteContainerEntry(archive, SaveContainerJogadasEntry, jogadasJson);
            }

            if (!TryReadContainerManifest(tempPath, out _, out string manifestError))
                throw new InvalidDataException($"Container temporario invalido: {manifestError}");

            using (FileStream validationStream = File.OpenRead(tempPath))
            using (ZipArchive validationArchive = new ZipArchive(validationStream, ZipArchiveMode.Read, leaveOpen: false))
                ReadRequiredContainerEntry(validationArchive, SaveContainerGameEntry);

            ReplaceFileTransactional(tempPath, savePath, backupPath);
        }
        finally
        {
            DeleteIfExists(tempPath);
            DeleteIfExists(backupPath);
        }
    }

    private static void ReplaceFileTransactional(string tempPath, string savePath, string backupPath)
    {
        if (!File.Exists(savePath))
        {
            File.Move(tempPath, savePath);
            return;
        }

        try
        {
            File.Replace(tempPath, savePath, backupPath);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException || ex is IOException)
        {
            File.Move(savePath, backupPath);
            try
            {
                File.Move(tempPath, savePath);
                File.Delete(backupPath);
            }
            catch
            {
                if (!File.Exists(savePath) && File.Exists(backupPath))
                    File.Move(backupPath, savePath);
                throw;
            }
        }
    }

    private static void WriteContainerEntry(ZipArchive archive, string entryName, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
        using (Stream stream = entry.Open())
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            writer.Write(content ?? string.Empty);
    }

    private static string ReadRequiredContainerEntry(ZipArchive archive, string entryName)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryName);
        if (entry == null)
            throw new InvalidDataException($"Entrada obrigatoria ausente: {entryName}");

        string content = ReadContainerEntry(entry);
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidDataException($"Entrada obrigatoria vazia: {entryName}");
        return content;
    }

    private static string ReadOptionalContainerEntry(ZipArchive archive, string entryName)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryName);
        return entry != null ? ReadContainerEntry(entry) : string.Empty;
    }

    private static string ReadContainerEntry(ZipArchiveEntry entry)
    {
        using (Stream stream = entry.Open())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
            return reader.ReadToEnd();
    }

    private static bool TryReadContainerManifest(
        string savePath,
        out SaveContainerManifest manifest,
        out string error)
    {
        manifest = null;
        error = string.Empty;
        try
        {
            using (FileStream stream = File.OpenRead(savePath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
            {
                string json = ReadRequiredContainerEntry(archive, SaveContainerManifestEntry);
                manifest = JsonUtility.FromJson<SaveContainerManifest>(json);
            }

            if (manifest == null || manifest.containerVersion != 1)
                throw new InvalidDataException("Versao de container ausente ou nao suportada.");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            manifest = null;
            return false;
        }
    }

    private void RestoreReplayFromContainer(string replayJson)
    {
        if (replayManager == null)
            return;

        try
        {
            ReplaySaveData restored = string.IsNullOrWhiteSpace(replayJson)
                ? null
                : JsonUtility.FromJson<ReplaySaveData>(replayJson);
            replayManager.ImportReplaySaveData(restored);
            replayManager.BeginTurnRecording();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveGame] Falha ao restaurar replay do container: {ex.Message}");
            replayManager.ImportReplaySaveData(null);
            replayManager.BeginTurnRecording();
        }
    }

    private static void RestoreJogadasFromContainer(string jogadasJson)
    {
        JogadasManager manager = JogadasManager.EnsureInstance();
        try
        {
            JogadasLog restored = string.IsNullOrWhiteSpace(jogadasJson)
                ? null
                : JsonUtility.FromJson<JogadasLog>(jogadasJson);
            manager.log = restored ?? new JogadasLog();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveGame] Falha ao restaurar jogadas do container: {ex.Message}");
            manager.log = new JogadasLog();
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            File.Delete(path);
    }

    private static string SanitizeFileName(string raw)
    {
        string input = string.IsNullOrWhiteSpace(raw) ? "save_slot" : raw.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder sb = new StringBuilder(input.Length);
        for (int i = 0; i < input.Length; i++)
        {
            char ch = input[i];
            bool isInvalid = false;
            for (int j = 0; j < invalid.Length; j++)
            {
                if (invalid[j] == ch)
                {
                    isInvalid = true;
                    break;
                }
            }

            sb.Append(isInvalid ? '_' : ch);
        }

        string sanitized = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "save_slot" : sanitized;
    }

    private static int NormalizeSlot(int slotIndex)
    {
        return Mathf.Clamp(slotIndex, 1, 3);
    }

    private static string ComputeShortStableHash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "00000000";

        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = sha.ComputeHash(bytes);
            StringBuilder sb = new StringBuilder(8);
            for (int i = 0; i < 4; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    private void TryAutoAssignReferences()
    {
        if (unitSpawner == null)
            unitSpawner = FindInActiveScene<UnitSpawner>();
        if (constructionSpawner == null)
            constructionSpawner = FindInActiveScene<ConstructionSpawner>();
        if (matchController == null)
            matchController = FindInActiveScene<MatchController>();
        if (turnStateManager == null)
            turnStateManager = FindInActiveScene<TurnStateManager>();
        if (animationManager == null)
            animationManager = FindInActiveScene<AnimationManager>();
        if (cursorController == null)
            cursorController = FindInActiveScene<CursorController>();
        
        if (planningManager == null)
            planningManager = FindInActiveScene<PlanningManager>();
        if (aiController == null)
            aiController = FindInActiveScene<AIController>();
        
        // if (aiPlayerController == null)
        //     aiPlayerController = FindInActiveScene<AIPlayerController>();
    }

    private static double PerfNowMs()
    {
        return Time.realtimeSinceStartupAsDouble * 1000d;
    }

    private void LogLoadPerf(int slot, string stage, double stageStartMs, double totalElapsedMs)
    {
        if (!enableLoadPerfLogs)
            return;

        double stageElapsedMs = Math.Max(0d, PerfNowMs() - stageStartMs);
        double totalMs = Math.Max(0d, totalElapsedMs);
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Debug.Log(
            $"[SaveGame][LoadPerf][{timestamp}] slot={slot} stage={stage} " +
            $"stageMs={stageElapsedMs:F2} totalMs={totalMs:F2}");
    }

    private void LogPromptPerf(string stage, double elapsedMs, bool forceWarning = false)
    {
        if (!enablePromptPerfLogs)
            return;

        string message = $"[SaveGame][PromptPerf] stage={stage} ms={Math.Max(0d, elapsedMs):F2}";
        if (forceWarning || elapsedMs >= promptPerfWarnThresholdMs)
            Debug.LogWarning(message, this);
        else
            Debug.Log(message, this);
    }

    private static T FindInActiveScene<T>() where T : Component
    {
        Scene activeScene = SceneManager.GetActiveScene();
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            T candidate = all[i];
            if (candidate == null)
                continue;
            if (candidate.gameObject.scene == activeScene)
                return candidate;
        }

        return null;
    }

    private bool WasKeyPressedThisFrame(KeyCode key)
    {
        if (key == KeyCode.F12)
            return false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return false;

        switch (key)
        {
            case KeyCode.A: return Keyboard.current.aKey.wasPressedThisFrame;
            case KeyCode.B: return Keyboard.current.bKey.wasPressedThisFrame;
            case KeyCode.C: return Keyboard.current.cKey.wasPressedThisFrame;
            case KeyCode.D: return Keyboard.current.dKey.wasPressedThisFrame;
            case KeyCode.E: return Keyboard.current.eKey.wasPressedThisFrame;
            case KeyCode.F: return Keyboard.current.fKey.wasPressedThisFrame;
            case KeyCode.G: return Keyboard.current.gKey.wasPressedThisFrame;
            case KeyCode.H: return Keyboard.current.hKey.wasPressedThisFrame;
            case KeyCode.I: return Keyboard.current.iKey.wasPressedThisFrame;
            case KeyCode.J: return Keyboard.current.jKey.wasPressedThisFrame;
            case KeyCode.K: return Keyboard.current.kKey.wasPressedThisFrame;
            case KeyCode.L: return Keyboard.current.lKey.wasPressedThisFrame;
            case KeyCode.M: return Keyboard.current.mKey.wasPressedThisFrame;
            case KeyCode.N: return Keyboard.current.nKey.wasPressedThisFrame;
            case KeyCode.O: return Keyboard.current.oKey.wasPressedThisFrame;
            case KeyCode.P: return Keyboard.current.pKey.wasPressedThisFrame;
            case KeyCode.Q: return Keyboard.current.qKey.wasPressedThisFrame;
            case KeyCode.R: return Keyboard.current.rKey.wasPressedThisFrame;
            case KeyCode.S: return Keyboard.current.sKey.wasPressedThisFrame;
            case KeyCode.T: return Keyboard.current.tKey.wasPressedThisFrame;
            case KeyCode.U: return Keyboard.current.uKey.wasPressedThisFrame;
            case KeyCode.V: return Keyboard.current.vKey.wasPressedThisFrame;
            case KeyCode.W: return Keyboard.current.wKey.wasPressedThisFrame;
            case KeyCode.X: return Keyboard.current.xKey.wasPressedThisFrame;
            case KeyCode.Y: return Keyboard.current.yKey.wasPressedThisFrame;
            case KeyCode.Z: return Keyboard.current.zKey.wasPressedThisFrame;
            case KeyCode.Alpha0: return Keyboard.current.digit0Key.wasPressedThisFrame;
            case KeyCode.Alpha1: return Keyboard.current.digit1Key.wasPressedThisFrame;
            case KeyCode.Alpha2: return Keyboard.current.digit2Key.wasPressedThisFrame;
            case KeyCode.Alpha3: return Keyboard.current.digit3Key.wasPressedThisFrame;
            case KeyCode.Alpha4: return Keyboard.current.digit4Key.wasPressedThisFrame;
            case KeyCode.Alpha5: return Keyboard.current.digit5Key.wasPressedThisFrame;
            case KeyCode.Alpha6: return Keyboard.current.digit6Key.wasPressedThisFrame;
            case KeyCode.Alpha7: return Keyboard.current.digit7Key.wasPressedThisFrame;
            case KeyCode.Alpha8: return Keyboard.current.digit8Key.wasPressedThisFrame;
            case KeyCode.Alpha9: return Keyboard.current.digit9Key.wasPressedThisFrame;
            case KeyCode.Keypad0: return Keyboard.current.numpad0Key.wasPressedThisFrame;
            case KeyCode.Keypad1: return Keyboard.current.numpad1Key.wasPressedThisFrame;
            case KeyCode.Keypad2: return Keyboard.current.numpad2Key.wasPressedThisFrame;
            case KeyCode.Keypad3: return Keyboard.current.numpad3Key.wasPressedThisFrame;
            case KeyCode.Keypad4: return Keyboard.current.numpad4Key.wasPressedThisFrame;
            case KeyCode.Keypad5: return Keyboard.current.numpad5Key.wasPressedThisFrame;
            case KeyCode.Keypad6: return Keyboard.current.numpad6Key.wasPressedThisFrame;
            case KeyCode.Keypad7: return Keyboard.current.numpad7Key.wasPressedThisFrame;
            case KeyCode.Keypad8: return Keyboard.current.numpad8Key.wasPressedThisFrame;
            case KeyCode.Keypad9: return Keyboard.current.numpad9Key.wasPressedThisFrame;
            case KeyCode.Space: return Keyboard.current.spaceKey.wasPressedThisFrame;
            case KeyCode.UpArrow: return Keyboard.current.upArrowKey.wasPressedThisFrame;
            case KeyCode.DownArrow: return Keyboard.current.downArrowKey.wasPressedThisFrame;
            case KeyCode.Return: return Keyboard.current.enterKey.wasPressedThisFrame;
            case KeyCode.KeypadEnter: return Keyboard.current.numpadEnterKey.wasPressedThisFrame;
            case KeyCode.Escape: return Keyboard.current.escapeKey.wasPressedThisFrame;
            case KeyCode.Tab: return Keyboard.current.tabKey.wasPressedThisFrame;
            case KeyCode.F1: return Keyboard.current.f1Key.wasPressedThisFrame;
            case KeyCode.F2: return Keyboard.current.f2Key.wasPressedThisFrame;
            case KeyCode.F3: return Keyboard.current.f3Key.wasPressedThisFrame;
            case KeyCode.F4: return Keyboard.current.f4Key.wasPressedThisFrame;
            case KeyCode.F5: return Keyboard.current.f5Key.wasPressedThisFrame;
            case KeyCode.F6: return Keyboard.current.f6Key.wasPressedThisFrame;
            case KeyCode.F7: return Keyboard.current.f7Key.wasPressedThisFrame;
            case KeyCode.F8: return Keyboard.current.f8Key.wasPressedThisFrame;
            case KeyCode.F9: return Keyboard.current.f9Key.wasPressedThisFrame;
            case KeyCode.F10: return Keyboard.current.f10Key.wasPressedThisFrame;
            case KeyCode.F11: return Keyboard.current.f11Key.wasPressedThisFrame;
            case KeyCode.F12: return false; // reserved — AI Resume (DebugManager)
            default:
                return false;
        }
#else
        return Input.GetKeyDown(key);
#endif
    }

    private ConstructionSiteRuntime BuildSiteRuntimeFromSaveData(ConstructionSiteRuntimeSaveData saved)
    {
        return SaveDataMapper.BuildConstructionSiteRuntimeFromSaveData(
            saved,
            ResolveUnitById,
            ResolveServiceById,
            ResolveSupplyById);
    }

    private UnitData ResolveUnitById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || unitSpawner == null)
            return null;

        return unitSpawner.TryGetUnitData(id, out UnitData unit) ? unit : null;
    }

    private ServiceData ResolveServiceById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (cachedServicesById.TryGetValue(id, out ServiceData cached) && cached != null)
            return cached;

        ServiceData[] loaded = Resources.FindObjectsOfTypeAll<ServiceData>();
        for (int i = 0; i < loaded.Length; i++)
        {
            ServiceData service = loaded[i];
            if (service == null || string.IsNullOrWhiteSpace(service.id))
                continue;
            if (!cachedServicesById.ContainsKey(service.id))
                cachedServicesById[service.id] = service;
        }

        cachedServicesById.TryGetValue(id, out ServiceData resolved);
        return resolved;
    }

    private SupplyData ResolveSupplyById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (cachedSuppliesById.TryGetValue(id, out SupplyData cached) && cached != null)
            return cached;

        SupplyData[] loaded = Resources.FindObjectsOfTypeAll<SupplyData>();
        for (int i = 0; i < loaded.Length; i++)
        {
            SupplyData supply = loaded[i];
            if (supply == null || string.IsNullOrWhiteSpace(supply.id))
                continue;
            if (!cachedSuppliesById.ContainsKey(supply.id))
                cachedSuppliesById[supply.id] = supply;
        }

        cachedSuppliesById.TryGetValue(id, out SupplyData resolved);
        return resolved;
    }
}
