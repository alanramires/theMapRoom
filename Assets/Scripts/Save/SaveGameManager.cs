using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

    private sealed class PendingMainMenuLoadRequest
    {
        public int slotIndex;
        public string sceneName;
    }

    private static PendingMainMenuLoadRequest pendingMainMenuLoad;
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
    private sealed class SaveSlotMetadataFile
    {
        public string sceneName;
        public long savedAtUtcTicks;
    }

    private sealed class LoadPreprocessResult
    {
        public string json;
        public bool wasCompressed;
        public int compressedBytes;
        public int uncompressedBytes;
        public string error;
    }

    private bool loadInProgress;
    private bool promptUsingPersistenceState;
    private Coroutine postLoadThreatWarmupRoutine;
    private SlotPromptState promptState;
    private int promptOpenedFrame = -1;
    private int overwritePendingSlot;
    private readonly Dictionary<string, ServiceData> cachedServicesById = new Dictionary<string, ServiceData>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SupplyData> cachedSuppliesById = new Dictionary<string, SupplyData>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        EnsureDefaultSaveDirectoryConfigured();
        TryAutoAssignReferences();
    }

    private void Start()
    {
        TryStartPendingMainMenuLoadForActiveScene();
        ApplyPendingNewGame();
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
        cursorController?.PlayConfirmSfx();
        PanelDialogController.ClearExternalText();
        RefreshPromptHelper();
        LogPromptPerf("save_prompt.total_key_to_helper", PerfNowMs() - promptStartMs, forceWarning: true);
    }

    private void OpenLoadSlotPrompt(double inputStartMs = -1d)
    {
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
        cursorController?.PlayConfirmSfx();
        PanelDialogController.ClearExternalText();
        RefreshPromptHelper();
        LogPromptPerf("load_prompt.total_key_to_helper", PerfNowMs() - promptStartMs, forceWarning: true);
    }

    private void HandlePromptInput()
    {
        if (Time.frameCount <= promptOpenedFrame)
            return;

        if (WasAnySlotNumberPressedThisFrame(out int slotPressed))
        {
            HandleSlotChosen(slotPressed);
            return;
        }

        if (promptState == SlotPromptState.SaveConfirmOverwrite &&
            (WasKeyPressedThisFrame(KeyCode.Return) || WasKeyPressedThisFrame(KeyCode.KeypadEnter)))
        {
            SaveSlot(overwritePendingSlot);
            CancelPrompt(clearDialogOverride: false);
            return;
        }

        if (WasKeyPressedThisFrame(KeyCode.Escape))
        {
            if (promptState == SlotPromptState.SaveConfirmOverwrite)
            {
                promptState = SlotPromptState.SaveSelectSlot;
                overwritePendingSlot = 0;
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
            CancelPrompt(clearDialogOverride: false);
            return;
        }

        if (metadata.exists)
        {
            promptState = SlotPromptState.SaveConfirmOverwrite;
            overwritePendingSlot = normalizedSlot;
            PanelDialogController.TrySetExternalText(BuildOverwriteDialogText(metadata));
            RefreshPromptHelper();
            cursorController?.PlayBeepSfx();
            return;
        }

        SaveSlot(normalizedSlot);
        CancelPrompt(clearDialogOverride: false);
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
            string json = JsonUtility.ToJson(data, false);
            byte[] compressedBytes = CompressJsonToGzipBytes(json);
            string path = ResolveWritableSlotPath(normalizedSlot);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ResolveSaveDirectory());
            File.WriteAllBytes(path, compressedBytes);
            WriteSlotMetadataFile(path, data);
            WriteOrDeleteReplaySidecar(path);
            LogSaveDiagnostics(normalizedSlot, json, compressedBytes);
            cursorController?.PlayLoadSfx();
            string savedText = ResolveDialog(
                "dialog.save_status.success",
                ResolveHelper("helper.save_status.success", "Jogo salvo no slot <slot>"),
                new Dictionary<string, string> { { "slot", normalizedSlot.ToString() } });
            PanelDialogController.TrySetTransientText(savedText, 2.2f);
            Debug.Log($"[SaveGame] Slot {normalizedSlot} salvo em: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveGame] Falha ao salvar: {ex.Message}\n{ex.StackTrace}");
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
            suppressNextLoadConfirmSfx = false;
            Debug.LogError($"[SaveGame] Falha ao trocar para cena '{targetScene}': {ex.Message}");
            return false;
        }
    }

    public void LoadSlot(int slotIndex)
    {
        if (IsPersistenceBlockedByActiveAI(showFeedback: true))
            return;
        if (IsPersistenceBlockedByTurnState(showFeedback: true, allowPersistencePromptState: promptState != SlotPromptState.None))
            return;

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

    private IEnumerator LoadSlotAsync(string path, int normalizedSlot)
    {
        loadInProgress = true;
        IsAnyLoadInProgress = true;
        double asyncStartMs = PerfNowMs();
        LogLoadPerf(normalizedSlot, "load_async.start", asyncStartMs, 0d);
        try
        {
            ShowLoadingIndicator("dialog.load_status.loading_wait", "Carregando jogo, aguarde");
            // Garante pelo menos um frame para o indicador aparecer.
            yield return null;

            double preprocessStartMs = PerfNowMs();
            LogLoadPerf(normalizedSlot, "preprocess.begin", preprocessStartMs, preprocessStartMs - asyncStartMs);
            Task<LoadPreprocessResult> preprocessTask = Task.Run(() => PreprocessLoadData(path));
            while (!preprocessTask.IsCompleted)
                yield return null;
            LogLoadPerf(normalizedSlot, "preprocess.end", preprocessStartMs, PerfNowMs() - asyncStartMs);

            if (preprocessTask.IsFaulted)
            {
                Debug.LogError($"[SaveGame] Falha no preprocess assíncrono: {preprocessTask.Exception?.GetBaseException().Message}");
                cursorController?.PlayErrorSfx();
                PanelDialogController.ClearExternalText();
                yield break;
            }

            LoadPreprocessResult preprocess = preprocessTask.Result;
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
                    $"[SaveGame] Load slot {normalizedSlot}: compressed={preprocess.wasCompressed} " +
                    $"compressedBytes={preprocess.compressedBytes} uncompressedBytes={preprocess.uncompressedBytes}");
            }

            SaveGameData data = null;
            double deserializeStartMs = PerfNowMs();
            LogLoadPerf(normalizedSlot, "deserialize_json.begin", deserializeStartMs, deserializeStartMs - asyncStartMs);
            try
            {
                data = JsonUtility.FromJson<SaveGameData>(preprocess.json);
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
            LogLoadPerf(normalizedSlot, "load_async.end", asyncStartMs, PerfNowMs() - asyncStartMs);
        }
        finally
        {
            // Em casos de erro antes de entrar no LoadRoutine, garante desbloqueio.
            loadInProgress = false;
            IsAnyLoadInProgress = false;
        }
    }

    private static LoadPreprocessResult PreprocessLoadData(string path)
    {
        LoadPreprocessResult result = new LoadPreprocessResult();
        try
        {
            if (!TryReadSaveJson(path, out string json, out bool wasCompressed, out int compressedBytes, out int uncompressedBytes, out string readError))
            {
                result.error = readError;
                return result;
            }

            result.json = json;
            result.wasCompressed = wasCompressed;
            result.compressedBytes = compressedBytes;
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
        string metaPath = ResolveMetaPathForSavePath(path);
        if (File.Exists(metaPath))
            File.Delete(metaPath);
        string replayPath = ResolveReplayPathForSavePath(path);
        if (File.Exists(replayPath))
            File.Delete(replayPath);
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
            turnStateManager.ForceNeutral();
        }

        cursorController?.ClearRuntimeInputLocksAfterLoad();
    }

    private static void LogSaveDiagnostics(int slotIndex, string json, byte[] compressedBytes)
    {
        int uncompressedBytes = string.IsNullOrEmpty(json) ? 0 : Encoding.UTF8.GetByteCount(json);
        float uncompressedKb = uncompressedBytes / 1024f;
        int compressedSizeBytes = compressedBytes != null ? compressedBytes.Length : 0;
        float compressedKb = compressedSizeBytes / 1024f;
        float compressionRatio = uncompressedBytes > 0 ? (float)compressedSizeBytes / uncompressedBytes : 0f;

        Debug.Log(
            $"[SaveGame][Diagnostics] slot={slotIndex} " +
            $"jsonBytes={uncompressedBytes} jsonKB={uncompressedKb:F2} " +
            $"compressedBytes={compressedSizeBytes} compressedKB={compressedKb:F2} compressionRatio={compressionRatio:F3}");
    }

    private static byte[] CompressJsonToGzipBytes(string json)
    {
        string safeJson = json ?? string.Empty;
        byte[] utf8 = Encoding.UTF8.GetBytes(safeJson);
        using (MemoryStream output = new MemoryStream())
        {
            using (GZipStream gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(utf8, 0, utf8.Length);
            }

            return output.ToArray();
        }
    }

    private static bool TryReadSaveJson(
        string path,
        out string json,
        out bool wasCompressed,
        out int compressedBytes,
        out int uncompressedBytes,
        out string error)
    {
        json = string.Empty;
        wasCompressed = false;
        compressedBytes = 0;
        uncompressedBytes = 0;
        error = string.Empty;

        try
        {
            byte[] raw = File.ReadAllBytes(path);
            compressedBytes = raw != null ? raw.Length : 0;
            bool isGzip = raw != null && raw.Length >= 2 && raw[0] == 0x1F && raw[1] == 0x8B;

            if (isGzip)
            {
                wasCompressed = true;
                using (MemoryStream input = new MemoryStream(raw))
                using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
                using (MemoryStream output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    byte[] decompressed = output.ToArray();
                    uncompressedBytes = decompressed.Length;
                    json = Encoding.UTF8.GetString(decompressed);
                    return true;
                }
            }

            json = raw != null ? Encoding.UTF8.GetString(raw) : string.Empty;
            uncompressedBytes = string.IsNullOrEmpty(json) ? 0 : Encoding.UTF8.GetByteCount(json);
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

    private IEnumerator LoadRoutine(SaveGameData data, int loadedSlot)
    {
        loadInProgress = true;
        IsAnyLoadInProgress = true;
        string stage = "init";
        bool coreLoadSucceeded = false;
        bool suppressedFogRefresh = false;
        double routineStartMs = PerfNowMs();
        LogLoadPerf(loadedSlot, "load_routine.start", routineStartMs, 0d);

        if (matchController != null)
        {
            matchController.SuppressFogOfWarRefresh = true;
            suppressedFogRefresh = true;
        }

        // Espera um frame apos destruir para evitar residuos de lookup no mesmo frame.
        stage = "clear-runtime";
        double clearRuntimeStartMs = PerfNowMs();
        LogLoadPerf(loadedSlot, "clear_runtime.begin", clearRuntimeStartMs, clearRuntimeStartMs - routineStartMs);
        ClearCurrentRuntime();
        yield return null;
        LogLoadPerf(loadedSlot, "clear_runtime.end", clearRuntimeStartMs, PerfNowMs() - routineStartMs);

        // Hoisted para ficar acessivel apos o try-catch (necessario para reaplicar flags depois de ForceReapplyActiveTeamWithTurnStart).
        Dictionary<int, UnitManager> unitsById = new Dictionary<int, UnitManager>();

        try
        {
            int maxUnitId = 0;
            int maxConstructionId = 0;

            stage = "spawn-constructions";
            double spawnConstructionsStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "restore_constructions.begin", spawnConstructionsStartMs, spawnConstructionsStartMs - routineStartMs);
            if (data.constructions != null)
            {
                for (int i = 0; i < data.constructions.Count; i++)
                {
                    ConstructionSaveData saved = data.constructions[i];
                    if (saved == null || string.IsNullOrWhiteSpace(saved.constructionId))
                        continue;

                    if (!constructionSpawner.TryGetConstructionData(saved.constructionId, out ConstructionData constructionData) || constructionData == null)
                    {
                        Debug.LogWarning($"[SaveGame] Construcao nao encontrada no DB: {saved.constructionId}");
                        continue;
                    }

                    Vector3 world = new Vector3(saved.worldX, saved.worldY, 0f);
                    GameObject go = constructionSpawner.Spawn(constructionData, (TeamId)saved.teamId, world, Quaternion.identity);
                    if (go == null)
                        continue;

                    ConstructionManager manager = go.GetComponent<ConstructionManager>();
                    if (manager == null)
                        continue;

                    SaveDataMapper.ApplyConstructionSaveData(manager, saved, BuildSiteRuntimeFromSaveData);

                    if (saved.instanceId > maxConstructionId)
                        maxConstructionId = saved.instanceId;
                }
            }
            LogLoadPerf(loadedSlot, "restore_constructions.end", spawnConstructionsStartMs, PerfNowMs() - routineStartMs);
            SectorManager.RequestRebuildFromActiveConstructions("post-restore-constructions");

            stage = "spawn-units";
            double spawnUnitsStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "restore_units.begin", spawnUnitsStartMs, spawnUnitsStartMs - routineStartMs);
            if (data.units != null)
            {
                for (int i = 0; i < data.units.Count; i++)
                {
                    UnitSaveData saved = data.units[i];
                    if (saved == null || string.IsNullOrWhiteSpace(saved.unitId))
                        continue;

                    if (!unitSpawner.TryGetUnitData(saved.unitId, out UnitData unitData) || unitData == null)
                    {
                        Debug.LogWarning($"[SaveGame] Unidade nao encontrada no DB: {saved.unitId}");
                        continue;
                    }

                    Vector3 world = new Vector3(saved.worldX, saved.worldY, 0f);
                    // Load restores exact runtime placement/embark state right after spawn.
                    // Skip occupancy blocking here to avoid false warnings when transporter and passenger share a cell.
                    GameObject go = unitSpawner.Spawn(
                        unitData,
                        (TeamId)saved.teamId,
                        world,
                        Quaternion.identity,
                        enforceSpawnOccupancyRule: false);
                    if (go == null)
                        continue;

                    UnitManager manager = go.GetComponent<UnitManager>();
                    if (manager == null)
                        continue;

                    SaveDataMapper.ApplyUnitSaveData(manager, saved);

                    unitsById[saved.instanceId] = manager;
                    if (saved.instanceId > maxUnitId)
                        maxUnitId = saved.instanceId;
                }
            }
            LogLoadPerf(loadedSlot, "restore_units.end", spawnUnitsStartMs, PerfNowMs() - routineStartMs);

            // Religa passageiros embarcados apos todos os spawns.
            stage = "restore-embarked";
            double restoreEmbarkedStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "restore_embarked.begin", restoreEmbarkedStartMs, restoreEmbarkedStartMs - routineStartMs);
            if (data.units != null)
            {
                for (int i = 0; i < data.units.Count; i++)
                {
                    UnitSaveData saved = data.units[i];
                    if (saved == null || !saved.isEmbarked || saved.transporterInstanceId <= 0)
                        continue;

                    if (!unitsById.TryGetValue(saved.instanceId, out UnitManager passenger) || passenger == null)
                        continue;
                    if (!unitsById.TryGetValue(saved.transporterInstanceId, out UnitManager transporter) || transporter == null)
                        continue;

                    if (!transporter.TryEmbarkPassengerInSlot(passenger, saved.transporterSlotIndex, out string reason) && verboseLogs)
                        Debug.LogWarning($"[SaveGame] Falha embarque {saved.instanceId}->{saved.transporterInstanceId}: {reason}");
                }
            }
            LogLoadPerf(loadedSlot, "restore_embarked.end", restoreEmbarkedStartMs, PerfNowMs() - routineStartMs);

            stage = "sync-ids";
            double syncIdsStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "sync_ids.begin", syncIdsStartMs, syncIdsStartMs - routineStartMs);
            unitSpawner.EnsureNextIdAbove(maxUnitId);
            constructionSpawner.EnsureNextIdAbove(maxConstructionId);
            LogLoadPerf(loadedSlot, "sync_ids.end", syncIdsStartMs, PerfNowMs() - routineStartMs);

            stage = "restore-match";
            double restoreMatchStartMs = PerfNowMs();
            LogLoadPerf(loadedSlot, "restore_match_state.begin", restoreMatchStartMs, restoreMatchStartMs - routineStartMs);
            if (matchController != null)
            {
                RestoreMatchPlayers(data);
                matchController.SetEconomyEnabled(data.economyEnabled);
                matchController.SetCurrentTurn(data.currentTurn);
                matchController.SetActiveTeamIdWithoutTurnStart(data.activeTeamId);
                // Reaplica economia/flip apos SetActiveTeamIdWithoutTurnStart para evitar side effects
                // de credito no inicio do turno sobrescrever o snapshot salvo.
                RestoreMatchPlayers(data);
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
                bool restoredFromCache = matchController.TryRestoreFogRuntimeCacheFromSave(
                    data.fogCacheTeamId,
                    data.fogVisibleContributorsByCell,
                    data.fogUnitVisibilityByCacheIndex);
                if (!restoredFromCache)
                    matchController.RefreshFogOfWarForActiveTeam();
                LogLoadPerf(loadedSlot, "refresh_fog_after_load.end", refreshFogStartMs, PerfNowMs() - routineStartMs);
            }

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

        LogLoadPerf(loadedSlot, "load_routine.end", routineStartMs, PerfNowMs() - routineStartMs);
        loadInProgress = false;
        IsAnyLoadInProgress = false;

        if (coreLoadSucceeded)
        {
            matchController?.ForceReapplyActiveTeamWithTurnStart();

            // ForceReapplyActiveTeamWithTurnStart chama ReleaseUnitsForActiveTeam, que zera hasActed/movementPoints
            // de todas as unidades do time ativo. Reaplica os flags salvos para restaurar o estado correto.
            if (data?.units != null)
            {
                for (int i = 0; i < data.units.Count; i++)
                {
                    UnitSaveData saved = data.units[i];
                    if (saved == null || !unitsById.TryGetValue(saved.instanceId, out UnitManager unit) || unit == null)
                        continue;
                    SaveDataMapper.ApplyUnitTurnFlagsFromSaveData(unit, saved);
                }
            }

            OnAfterLoadSuccess?.Invoke();
        }
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
            includeNeutralTeam = matchState.includeNeutralTeam,
            economyEnabled = matchState.economyEnabled,
            victoryStarsEnabled = matchState.victoryStarsEnabled,
            victoryStarsToWin = matchState.victoryStarsToWin,
            hasVictoryWinner = matchState.hasVictoryWinner,
            victoryWinnerTeamId = matchState.victoryWinnerTeamId,
            players = matchState.players != null ? matchState.players : new List<MatchPlayerSaveData>(),
            victoryStars = matchState.victoryStars != null ? matchState.victoryStars : new List<MatchVictoryStarSaveData>(),
            fogCacheTeamId = int.MinValue,
            fogVisibleContributorsByCell = new List<FogCellContributorSaveData>(),
            fogUnitVisibilityByCacheIndex = new List<FogUnitVisibilitySaveData>()
        };

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;
            if (unit.gameObject.scene != activeScene)
                continue;

            UnitSaveData item = SaveDataMapper.BuildUnitSaveData(unit);
            if (item != null)
                data.units.Add(item);
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
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
            matchController.ExportFogRuntimeCacheForSave(
                out data.fogCacheTeamId,
                data.fogVisibleContributorsByCell,
                data.fogUnitVisibilityByCacheIndex);
        }

        // if (aiPlayerController != null)
        //     data.aiPlannerState = aiPlayerController.BuildPlannerSaveData();

        return data;
    }

    private void RestoreMatchPlayers(SaveGameData data)
    {
        if (data == null)
            return;

        MatchStateSaveData matchState = new MatchStateSaveData
        {
            includeNeutralTeam = data.includeNeutralTeam,
            players = data.players != null ? data.players : new List<MatchPlayerSaveData>(),
            victoryStars = data.victoryStars != null ? data.victoryStars : new List<MatchVictoryStarSaveData>(),
            victoryStarsEnabled = data.victoryStarsEnabled,
            victoryStarsToWin = data.victoryStarsToWin,
            hasVictoryWinner = data.hasVictoryWinner,
            victoryWinnerTeamId = data.victoryWinnerTeamId
        };
        SaveDataMapper.ApplyMatchStateSaveData(matchController, matchState);
    }

    private void ClearCurrentRuntime()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] != null && units[i].gameObject.scene == activeScene)
                Destroy(units[i].gameObject);
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            if (constructions[i] != null && constructions[i].gameObject.scene == activeScene)
                Destroy(constructions[i].gameObject);
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

        string fileName = SanitizeFileName(safeSlot) + ".json";
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
        if (File.Exists(primaryPath) && !IsMetadataSidecarPath(primaryPath) && !IsReplaySidecarPath(primaryPath))
            return primaryPath;

        string saveDir = ResolveSaveDirectory();
        try
        {
            if (Directory.Exists(saveDir))
            {
                string[] candidates = Directory.GetFiles(saveDir, $"*_slot{normalizedSlot}*.json", SearchOption.TopDirectoryOnly);
                if (candidates != null && candidates.Length > 0)
                {
                    string latest = null;
                    DateTime latestWrite = DateTime.MinValue;
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        string current = candidates[i];
                        if (IsMetadataSidecarPath(current) || IsReplaySidecarPath(current))
                            continue;

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

        // Compatibilidade com arquivos antigos em persistentDataPath.
        string legacyStem = string.IsNullOrWhiteSpace(fileNameDefault) ? "quicksave" : fileNameDefault.Trim();
        string legacyName = $"{legacyStem}_slot{normalizedSlot}.json";
        string legacyPath = Path.Combine(Application.persistentDataPath, SanitizeFileName(legacyName));
        if (File.Exists(legacyPath))
            return legacyPath;

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
        string basePath = string.IsNullOrWhiteSpace(customSaveDirectory)
            ? Application.persistentDataPath
            : customSaveDirectory.Trim();

        if (!Path.IsPathRooted(basePath))
            basePath = Path.Combine(Application.persistentDataPath, basePath);

        return basePath;
    }

    private void EnsureDefaultSaveDirectoryConfigured()
    {
        if (string.IsNullOrWhiteSpace(customSaveDirectory))
            customSaveDirectory = Application.persistentDataPath;
    }

    public string GetResolvedSaveDirectory()
    {
        return ResolveSaveDirectory();
    }

    public void SetCustomSaveDirectory(string directoryPath)
    {
        customSaveDirectory = string.IsNullOrWhiteSpace(directoryPath)
            ? Application.persistentDataPath
            : directoryPath.Trim();
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
        string metaPath = ResolveMetaPathForSavePath(metadata.path);
        if (TryReadSlotMetadataFile(metaPath, out SaveSlotMetadataFile metaFile))
        {
            metadata.sceneName = metaFile.sceneName ?? string.Empty;
            if (metaFile.savedAtUtcTicks > 0L)
            {
                DateTime utc = new DateTime(metaFile.savedAtUtcTicks, DateTimeKind.Utc);
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

        try
        {
            if (!TryReadSaveJson(metadata.path, out string json, out _, out _, out _, out string readError))
            {
                if (verboseLogs)
                    Debug.LogWarning($"[SaveGame] Nao foi possivel ler metadados do slot {normalizedSlot}: {readError}");
                metadata.savedAtLocal = fallbackLocalTime;
                if (string.IsNullOrWhiteSpace(metadata.sceneName))
                    metadata.sceneName = "Mapa desconhecido";
                return metadata;
            }

            SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);
            if (data != null)
            {
                metadata.sceneName = data.sceneName ?? string.Empty;
                if (data.savedAtUtcTicks > 0L)
                {
                    DateTime utc = new DateTime(data.savedAtUtcTicks, DateTimeKind.Utc);
                    metadata.savedAtLocal = utc.ToLocalTime();
                }
                else
                {
                    metadata.savedAtLocal = fallbackLocalTime;
                }

                // Compatibilidade: sidecar para evitar desserializacao completa no prompt.
                WriteSlotMetadataFileAtPath(metaPath, data);
            }
            else
            {
                metadata.savedAtLocal = fallbackLocalTime;
            }
        }
        catch (Exception ex)
        {
            metadata.savedAtLocal = fallbackLocalTime;
            if (verboseLogs)
                Debug.LogWarning($"[SaveGame] Nao foi possivel ler metadados do slot {normalizedSlot}: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(metadata.sceneName))
            metadata.sceneName = "Mapa desconhecido";

        return metadata;
    }

    private void WriteSlotMetadataFile(string savePath, SaveGameData data)
    {
        if (string.IsNullOrWhiteSpace(savePath) || data == null)
            return;

        string metaPath = ResolveMetaPathForSavePath(savePath);
        WriteSlotMetadataFileAtPath(metaPath, data);
    }

    private void WriteSlotMetadataFileAtPath(string metaPath, SaveGameData data)
    {
        if (string.IsNullOrWhiteSpace(metaPath) || data == null)
            return;

        try
        {
            SaveSlotMetadataFile metaFile = new SaveSlotMetadataFile
            {
                sceneName = data.sceneName ?? string.Empty,
                savedAtUtcTicks = data.savedAtUtcTicks
            };

            string json = JsonUtility.ToJson(metaFile, false);
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath) ?? ResolveSaveDirectory());
            File.WriteAllText(metaPath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            if (verboseLogs)
                Debug.LogWarning($"[SaveGame] Falha ao escrever metadata sidecar '{metaPath}': {ex.Message}");
        }
    }

    private bool TryReadSlotMetadataFile(string metaPath, out SaveSlotMetadataFile metaFile)
    {
        metaFile = null;
        if (string.IsNullOrWhiteSpace(metaPath) || !File.Exists(metaPath))
            return false;

        try
        {
            string json = File.ReadAllText(metaPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            metaFile = JsonUtility.FromJson<SaveSlotMetadataFile>(json);
            return metaFile != null;
        }
        catch (Exception ex)
        {
            if (verboseLogs)
                Debug.LogWarning($"[SaveGame] Falha ao ler metadata sidecar '{metaPath}': {ex.Message}");
            return false;
        }
    }

    private void WriteOrDeleteReplaySidecar(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            return;

        string replayPath = ResolveReplayPathForSavePath(savePath);
        bool shouldPersistReplay =
            saveReplayData &&
            replayManager != null &&
            replayManager.IsRecording;

        if (!shouldPersistReplay)
        {
            if (File.Exists(replayPath))
                File.Delete(replayPath);
            return;
        }

        try
        {
            ReplaySaveData replayData = replayManager.ExportReplaySaveData();
            string replayJson = JsonUtility.ToJson(replayData, false);
            Directory.CreateDirectory(Path.GetDirectoryName(replayPath) ?? ResolveSaveDirectory());
            File.WriteAllText(replayPath, replayJson, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveGame] Falha ao salvar replay sidecar '{replayPath}': {ex.Message}");
        }
    }

    private static bool IsReplaySidecarPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.EndsWith(".replay", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMetadataSidecarPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveMetaPathForSavePath(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            return Path.Combine(ResolveSaveDirectory(), "slot.meta.json");

        string directory = Path.GetDirectoryName(savePath) ?? ResolveSaveDirectory();
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(savePath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.meta.json");
    }

    private string ResolveReplayPathForSavePath(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            return Path.Combine(ResolveSaveDirectory(), "slot.replay");

        string directory = Path.GetDirectoryName(savePath) ?? ResolveSaveDirectory();
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(savePath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.replay");
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
            LogManager.Warning(GameLogCategory.SaveLoad, message, this);
        else
            LogManager.Info(GameLogCategory.SaveLoad, message, this);
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
            case KeyCode.F12: return Keyboard.current.f12Key.wasPressedThisFrame;
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















