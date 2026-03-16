using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MatchController : MonoBehaviour
{
    public static event Action<int> OnActiveTeamChanged;
    public static event Action<UnitManager> OnUnitActedStateChanged;
    public static event Action OnFogOfWarUpdated;

    private readonly struct FogOfWarUnitCacheKey : IEquatable<FogOfWarUnitCacheKey>
    {
        public readonly int snapshotHash;
        public readonly int globalBoardRevision;
        public readonly int teamObserverRevision;
        public readonly int sensorFlagsHash;

        public FogOfWarUnitCacheKey(int snapshotHash, int globalBoardRevision, int teamObserverRevision, int sensorFlagsHash)
        {
            this.snapshotHash = snapshotHash;
            this.globalBoardRevision = globalBoardRevision;
            this.teamObserverRevision = teamObserverRevision;
            this.sensorFlagsHash = sensorFlagsHash;
        }

        public bool Equals(FogOfWarUnitCacheKey other)
        {
            return snapshotHash == other.snapshotHash
                && globalBoardRevision == other.globalBoardRevision
                && teamObserverRevision == other.teamObserverRevision
                && sensorFlagsHash == other.sensorFlagsHash;
        }
    }

    private sealed class FogOfWarUnitCacheEntry
    {
        public FogOfWarUnitCacheKey key;
        public readonly HashSet<Vector3Int> visibleCells = new HashSet<Vector3Int>();
    }


    [System.Serializable]
    private struct PlayerEntry
    {
        public TeamId teamId;
        public bool flipX;
        [Min(0)] public int startMoney;
        [Min(0)] public int actualMoney;
        [Min(0)] public int incomePerTurn;
        [SerializeField, HideInInspector] public bool startMoneyApplied;
    }

    public enum GameSetupPreset
    {
        GameBoyClassic = 0,
        FisicaBasica = 1,
        AMontanhaAvacalha = 2,
        NeblinaLeve = 3,
        FogOfWarTotal = 4
    }

    [Header("Match State (MVP)")]
    [SerializeField] private int currentTurn = 0;
    [SerializeField] private int activeTeamId = (int)TeamId.Green;
    [FormerlySerializedAs("playerEconomy")]
    [SerializeField] private List<PlayerEntry> players = new List<PlayerEntry>
    {
        new PlayerEntry { teamId = TeamId.Green, flipX = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false },
        new PlayerEntry { teamId = TeamId.Red, flipX = true, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false },
        new PlayerEntry { teamId = TeamId.Blue, flipX = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false },
        new PlayerEntry { teamId = TeamId.Yellow, flipX = true, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false }
    };
    [SerializeField] private bool includeNeutralTeam = false;
    [SerializeField] private bool economyEnabled = true;
    // Placeholder para futura pintura de visibilidade no mapa (nao governa regras de combate no momento).
    [SerializeField, HideInInspector] private bool fogOfWar = true;
    [Header("Gameplay Setup")]
    [SerializeField] private GameSetupPreset gameSetup = GameSetupPreset.FogOfWarTotal;
    [SerializeField] private bool enableLdtValidation = true;
    [SerializeField] private bool enableLosValidation = true;
    [SerializeField] private bool enableSpotter = true;
    [SerializeField] private bool enableStealthValidation = true;
    [SerializeField] private bool enableTotalWar = true;
    [SerializeField, Min(1)] private int maxUnitsPerTeam = 40;
    [SerializeField] private AutonomyDatabase autonomyDatabase;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private TurnStateManager turnStateManager;
    [Header("Turn Transition")]
    [SerializeField] private MatchMusicAudioManager matchMusicAudioManager;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPreDelay = 0.5f;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPostDelay = 0.2f;
    [Header("Fog Of War")]
    [SerializeField] private Tilemap fogOfWarTilemap;
    [SerializeField] private TileBase fogOfWarOverlayTile;
    [SerializeField] private TerrainDatabase fogOfWarTerrainDatabase;
    [SerializeField] private DPQAirHeightConfig fogOfWarDpqAirHeightConfig;
    [SerializeField] [Range(0f, 1f)] private float fogOfWarAlpha = 0.65f;
    [SerializeField] private int activePlayerListIndex = 0;
    [SerializeField, HideInInspector] private int appliedActiveTeamId = int.MinValue;
    [SerializeField, HideInInspector] private bool pendingTurnStartUpkeep;
    [SerializeField, HideInInspector] private bool pendingTurnStartEconomy = true;
    [SerializeField, HideInInspector] private int cachedConstructionIncomeSignature;
    [SerializeField, HideInInspector] private int cachedConstructionIncomeCount;
    [Header("Runtime Perf")]
    [SerializeField] [Range(0.05f, 2f)] private float constructionIncomeRefreshIntervalSeconds = 0.35f;
    [Header("Editor")]
    [SerializeField] private bool continuousEditorRefresh = false;
    [System.NonSerialized] private readonly List<TeamId> playersView = new List<TeamId>();
    [System.NonSerialized] private List<TurnStateManager.TurnStartAutonomyUpkeepEntry> pendingTurnStartAutonomyHelperEntries;
    [System.NonSerialized] private readonly List<UnitManager> turnStartUnitsMarkedForFuelDepletionDeath = new List<UnitManager>();
    [System.NonSerialized] private readonly List<Vector3Int> fogBoardCellsBuffer = new List<Vector3Int>(1024);
    [System.NonSerialized] private readonly HashSet<Vector3Int> fogVisibleCellsBuffer = new HashSet<Vector3Int>();
    [System.NonSerialized] private readonly Dictionary<int, FogOfWarUnitCacheEntry> fogVisibleCellsByUnit = new Dictionary<int, FogOfWarUnitCacheEntry>();
    [System.NonSerialized] private readonly Dictionary<Vector3Int, int> fogVisibleContributorsByCell = new Dictionary<Vector3Int, int>();
    [System.NonSerialized] private readonly Dictionary<int, bool> fogUnitVisibilityByCacheIndex = new Dictionary<int, bool>();
    [System.NonSerialized] private readonly HashSet<Vector3Int> fogUnitVisibleScratchBuffer = new HashSet<Vector3Int>();
    [System.NonSerialized] private bool fogSortingLayerValidated;
    [System.NonSerialized] private int fogCachedTeamId = int.MinValue;
    [System.NonSerialized] private bool fogOverlayInitialized;
    [System.NonSerialized] private bool initialStealthDetectionBootstrapped;
    [System.NonSerialized] private bool debugFogOfWarEnabled = true;
    [System.NonSerialized] private float runtimeConstructionIncomeRefreshTimer;
    [Header("Debug")]
    [SerializeField] private bool enableFogSourceDebugLogs = false;
    public bool SuppressFogOfWarRefresh { get; set; } = false;

    public int CurrentTurn => currentTurn;
    public int ActiveTeamId => activeTeamId;
    public TeamId ActiveTeam => ClampToTeamId(activeTeamId);
    public IReadOnlyList<TeamId> Players
    {
        get
        {
            playersView.Clear();
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                    playersView.Add(players[i].teamId);
            }

            return playersView;
        }
    }
    public bool IncludeNeutralTeam => includeNeutralTeam;
    public bool EconomyEnabled => economyEnabled;
    public GameSetupPreset GameSetup => gameSetup;
    public bool EnableLdtValidation => enableLdtValidation;
    public bool EnableLosValidation => enableLosValidation;
    public bool EnableSpotter => enableSpotter;
    public bool EnableStealthValidation => enableStealthValidation;
    public bool EnableTotalWar => enableTotalWar;
    public bool IsFogOfWarDebugEnabled => debugFogOfWarEnabled;
    public int MaxUnitsPerTeam => Mathf.Max(1, maxUnitsPerTeam);
    public AutonomyDatabase AutonomyDatabase => autonomyDatabase;
    public int ActivePlayerListIndex => activePlayerListIndex;
    public bool IsTurnTransitionInProgress => advanceTurnTransitionRoutine != null;
    private Coroutine advanceTurnTransitionRoutine;

    public int GetActualMoney(TeamId team)
    {
        int playerIndex = FindPlayerEconomyIndex(team);
        if (playerIndex < 0)
            return 0;

        return Mathf.Max(0, players[playerIndex].actualMoney);
    }

    public int GetIncomePerTurn(TeamId team)
    {
        int playerIndex = FindPlayerEconomyIndex(team);
        if (playerIndex < 0)
            return 0;

        return Mathf.Max(0, players[playerIndex].incomePerTurn);
    }

    public bool TrySpendActualMoney(TeamId team, int amount, out int remainingMoney)
    {
        remainingMoney = 0;
        int spend = Mathf.Max(0, amount);
        int playerIndex = FindPlayerEconomyIndex(team);
        if (playerIndex < 0)
            return false;

        PlayerEntry entry = players[playerIndex];
        int current = Mathf.Max(0, entry.actualMoney);
        if (current < spend)
        {
            remainingMoney = current;
            return false;
        }

        entry.actualMoney = current - spend;
        players[playerIndex] = entry;
        remainingMoney = entry.actualMoney;
        return true;
    }

    public void SetEconomyEnabled(bool enabled)
    {
        economyEnabled = enabled;
    }

    public int ResolveEconomyCost(int baseCost)
    {
        return economyEnabled ? Mathf.Max(0, baseCost) : 0;
    }

    public bool TrySetActualMoney(TeamId team, int value)
    {
        int playerIndex = FindPlayerEconomyIndex(team);
        if (playerIndex < 0)
            return false;

        PlayerEntry entry = players[playerIndex];
        entry.actualMoney = Mathf.Max(0, value);
        players[playerIndex] = entry;
        return true;
    }

    public bool TrySetActualMoneyFirstPlayer(int value, out TeamId team)
    {
        team = TeamId.Neutral;
        if (players == null || players.Count == 0)
            return false;

        PlayerEntry entry = players[0];
        entry.actualMoney = Mathf.Max(0, value);
        players[0] = entry;
        team = entry.teamId;
        return true;
    }

    public void GetTeamUnitCounts(TeamId teamId, out int totalInField, out int readyToAct, bool includeEmbarked = true)
    {
        totalInField = 0;
        readyToAct = 0;
        if (teamId == TeamId.Neutral && !includeNeutralTeam)
            return;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;
            if (unit.TeamId != teamId)
                continue;
            if (!includeEmbarked && unit.IsEmbarked)
                continue;

            totalInField++;
            if (!unit.HasActed)
                readyToAct++;
        }
    }

    public bool HasReachedMaxUnitsPerTeam(TeamId teamId)
    {
        GetTeamUnitCounts(teamId, out int totalInField, out _);
        return totalInField >= MaxUnitsPerTeam;
    }

    public void ExportPlayersState(
        List<int> teamIds,
        List<bool> flipXs,
        List<int> startMoneys,
        List<int> actualMoneys,
        List<int> incomePerTurns,
        List<bool> startMoneyAppliedFlags)
    {
        if (teamIds == null || flipXs == null || startMoneys == null || actualMoneys == null || incomePerTurns == null || startMoneyAppliedFlags == null)
            return;

        teamIds.Clear();
        flipXs.Clear();
        startMoneys.Clear();
        actualMoneys.Clear();
        incomePerTurns.Clear();
        startMoneyAppliedFlags.Clear();

        if (players == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry entry = players[i];
            teamIds.Add((int)entry.teamId);
            flipXs.Add(entry.flipX);
            startMoneys.Add(Mathf.Max(0, entry.startMoney));
            actualMoneys.Add(Mathf.Max(0, entry.actualMoney));
            incomePerTurns.Add(Mathf.Max(0, entry.incomePerTurn));
            startMoneyAppliedFlags.Add(entry.startMoneyApplied);
        }
    }

    public void ImportPlayersState(
        IList<int> teamIds,
        IList<bool> flipXs,
        IList<int> startMoneys,
        IList<int> actualMoneys,
        IList<int> incomePerTurns,
        IList<bool> startMoneyAppliedFlags,
        bool includeNeutral)
    {
        includeNeutralTeam = includeNeutral;
        if (players == null)
            players = new List<PlayerEntry>();
        else
            players.Clear();

        int count = teamIds != null ? teamIds.Count : 0;
        for (int i = 0; i < count; i++)
        {
            TeamId team = ClampToTeamId(teamIds[i]);
            if (team == TeamId.Neutral)
                continue;

            PlayerEntry entry = new PlayerEntry
            {
                teamId = team,
                flipX = GetValueOrDefault(flipXs, i, GetDefaultFlipX(team)),
                startMoney = Mathf.Max(0, GetValueOrDefault(startMoneys, i, 0)),
                actualMoney = Mathf.Max(0, GetValueOrDefault(actualMoneys, i, 0)),
                incomePerTurn = Mathf.Max(0, GetValueOrDefault(incomePerTurns, i, 0)),
                startMoneyApplied = GetValueOrDefault(startMoneyAppliedFlags, i, false)
            };
            players.Add(entry);
        }

        NormalizePlayersList();
        SyncActivePlayerIndexFromActiveTeam();
        ApplyTeamFlipSettingsToSceneObjects();
    }

    public void RefreshIncomeFromConstructionsNow()
    {
        ComputeConstructionIncomeSignature(out int signature, out int count);
        cachedConstructionIncomeSignature = signature;
        cachedConstructionIncomeCount = count;
        RecalculateIncomePerTurnForAllPlayers();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(this);
#endif
    }

    public bool GetTeamFlipX(TeamId teamId)
    {
        if (teamId == TeamId.Neutral)
            return false;

        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].teamId == teamId)
                    return players[i].flipX;
            }
        }

        return GetDefaultFlipX(teamId);
    }

    private void Awake()
    {
        ApplyGameSetupPreset();
        SyncThreatRevisionFlags();
        NormalizeState();
        TryRefreshIncomeFromConstructions(markDirtyInEditor: false);
        TryAutoAssignCursorController();
        TryAutoAssignTurnStateManager();
        TryAutoAssignTurnTransitionReferences();
        if (enableTotalWar)
            TryAutoAssignFogOfWarReferences();
        if (Application.isPlaying)
        {
            // Delay first team apply/FoW refresh to Start so all scene objects had OnEnable.
            appliedActiveTeamId = activeTeamId;
        }
        else
        {
            ApplyActiveTeamIfChanged(force: true);
        }
        ApplyTeamFlipSettingsToSceneObjects();
    }

    private void Start()
    {
        if (Application.isPlaying)
            ApplyActiveTeamIfChanged(force: true);
        TryBootstrapInitialStealthDetection();
        RunTurnStartStillObservedForActiveTeamStealthUnits();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyGameSetupPreset();
        SyncThreatRevisionFlags();
        NormalizeState();
        TryRefreshIncomeFromConstructions(markDirtyInEditor: true);
        TryAutoAssignCursorController();
        TryAutoAssignTurnStateManager();
        TryAutoAssignTurnTransitionReferences();
        if (enableTotalWar)
            TryAutoAssignFogOfWarReferences();
        ApplyActiveTeamIfChanged(force: false);
        ApplyTeamFlipSettingsToSceneObjects();
    }
#endif

    private void Update()
    {
        if (Application.isPlaying)
        {
            runtimeConstructionIncomeRefreshTimer += Mathf.Max(0f, Time.unscaledDeltaTime);
            if (runtimeConstructionIncomeRefreshTimer >= Mathf.Max(0.05f, constructionIncomeRefreshIntervalSeconds))
            {
                runtimeConstructionIncomeRefreshTimer = 0f;
                TryRefreshIncomeFromConstructions(markDirtyInEditor: false);
            }
        }
        else if (continuousEditorRefresh)
        {
            TryRefreshIncomeFromConstructions(markDirtyInEditor: true);
        }

        SyncThreatRevisionFlags();

        if (!Application.isPlaying)
            return;

        TryAutoAssignCursorController();
        TryAutoAssignTurnStateManager();
        TryAutoAssignTurnTransitionReferences();
        if (enableTotalWar)
            TryAutoAssignFogOfWarReferences();
        ApplyActiveTeamIfChanged(force: false);
    }

    public void SetCurrentTurn(int turn)
    {
        currentTurn = Mathf.Max(0, turn);
    }

    public void SetGameSetupPreset(GameSetupPreset preset)
    {
        gameSetup = preset;
        ApplyGameSetupPreset();
        SyncThreatRevisionFlags();
    }

    public void SetActiveTeamId(int teamId)
    {
        activeTeamId = Mathf.Clamp(teamId, -1, 3);
        SyncActivePlayerIndexFromActiveTeam();
        ApplyActiveTeamIfChanged(force: false);
    }

    // Debug: avanca apenas o cursor de team sem alterar currentTurn.
    public void AdvanceTeam()
    {
        if (players.Count == 0)
        {
            if (includeNeutralTeam && HasAnyNeutralUnitsInField())
                SetNeutralActiveTeam();
            return;
        }

        if (activePlayerListIndex >= 0)
        {
            int next = activePlayerListIndex + 1;
            if (next < players.Count)
            {
                SetActivePlayerByIndex(next);
                return;
            }

            if (includeNeutralTeam && HasAnyNeutralUnitsInField())
            {
                SetNeutralActiveTeam();
                return;
            }

            SetActivePlayerByIndex(0, forceApply: true);
            return;
        }

        SetActivePlayerByIndex(0, forceApply: true);
    }

    // Avanca para o proximo membro da lista. So incrementa currentTurn ao "fechar ciclo".
    public void AdvanceTurn()
    {
        if (players.Count == 0)
        {
            if (includeNeutralTeam && HasAnyNeutralUnitsInField())
                SetNeutralActiveTeam();
            currentTurn = Mathf.Max(0, currentTurn + 1);
            return;
        }

        // Caso padrao: estamos em um player da lista.
        if (activePlayerListIndex >= 0)
        {
            int next = activePlayerListIndex + 1;
            if (next < players.Count)
            {
                pendingTurnStartUpkeep = true;
                pendingTurnStartEconomy = true;
                SetActivePlayerByIndex(next);
                return;
            }

            // Saiu da lista: vai para neutral se estiver habilitado.
            if (includeNeutralTeam && HasAnyNeutralUnitsInField())
            {
                SetNeutralActiveTeam();
                return;
            }

            // Sem neutral: fecha ciclo de turno.
            currentTurn = Mathf.Max(0, currentTurn + 1);
            pendingTurnStartUpkeep = true;
            pendingTurnStartEconomy = true;
            SetActivePlayerByIndex(0, forceApply: true);
            return;
        }

        // Estavamos em neutral (ou fora da lista): fecha ciclo de turno e volta para o primeiro player.
        currentTurn = Mathf.Max(0, currentTurn + 1);
        pendingTurnStartUpkeep = true;
        pendingTurnStartEconomy = true;
        SetActivePlayerByIndex(0, forceApply: true);
    }

    public void AdvanceTurnWithTransition()
    {
        if (!Application.isPlaying)
        {
            AdvanceTurn();
            return;
        }

        if (advanceTurnTransitionRoutine != null)
            StopCoroutine(advanceTurnTransitionRoutine);

        advanceTurnTransitionRoutine = StartCoroutine(AdvanceTurnTransitionRoutine());
    }

    private IEnumerator AdvanceTurnTransitionRoutine()
    {
        bool wasMusicPlaying = matchMusicAudioManager != null && matchMusicAudioManager.IsPlaying;
        bool wasPausedByUser = matchMusicAudioManager != null && matchMusicAudioManager.IsPausedByUser;
        bool usePauseResume = matchMusicAudioManager != null && matchMusicAudioManager.IsFreeMode;
        if (matchMusicAudioManager != null)
            matchMusicAudioManager.BeginTurnTransition();

        if (wasMusicPlaying && matchMusicAudioManager != null)
        {
            if (usePauseResume)
                matchMusicAudioManager.PauseForTurnTransition();
            else
                matchMusicAudioManager.StopForTurnTransition();
        }

        PlayAdvanceTurnSfx();
        if (advanceTurnPreDelay > 0f)
            yield return new WaitForSeconds(advanceTurnPreDelay);

        AdvanceTurn();

        if (advanceTurnPostDelay > 0f)
            yield return new WaitForSeconds(advanceTurnPostDelay);

        if (wasMusicPlaying && !wasPausedByUser && matchMusicAudioManager != null)
        {
            if (usePauseResume)
                matchMusicAudioManager.ResumeAfterTurnTransition();
            else
                matchMusicAudioManager.RestartCurrentModePlayback();
        }
        else if (matchMusicAudioManager != null)
        {
            matchMusicAudioManager.EndTurnTransition();
        }

        advanceTurnTransitionRoutine = null;
    }

    private void NormalizeState()
    {
        currentTurn = Mathf.Max(0, currentTurn);
        activeTeamId = Mathf.Clamp(activeTeamId, -1, 3);
        maxUnitsPerTeam = Mathf.Max(1, maxUnitsPerTeam);
        NormalizePlayersList();
        RecalculateIncomePerTurnForAllPlayers();
        SyncActivePlayerIndexFromActiveTeam();

        if (players.Count == 0)
        {
            activePlayerListIndex = -1;
            if (includeNeutralTeam || activeTeamId < -1 || activeTeamId > 3)
                activeTeamId = (int)TeamId.Neutral;
            return;
        }

        if (activeTeamId == (int)TeamId.Neutral)
        {
            if (!includeNeutralTeam)
                SetActivePlayerByIndex(0);
            return;
        }

        if (activePlayerListIndex < 0)
            SetActivePlayerByIndex(0);
    }

    private void ApplyGameSetupPreset()
    {
        switch (gameSetup)
        {
            case GameSetupPreset.GameBoyClassic:
                enableLdtValidation = false;
                enableLosValidation = false;
                enableSpotter = false;
                enableStealthValidation = false;
                enableTotalWar = false;
                break;
            case GameSetupPreset.FisicaBasica:
                enableLdtValidation = true;
                enableLosValidation = false;
                enableSpotter = false;
                enableStealthValidation = false;
                enableTotalWar = false;
                break;
            case GameSetupPreset.AMontanhaAvacalha:
                enableLdtValidation = true;
                enableLosValidation = true;
                enableSpotter = false;
                enableStealthValidation = false;
                enableTotalWar = false;
                break;
            case GameSetupPreset.NeblinaLeve:
                enableLdtValidation = true;
                enableLosValidation = true;
                enableSpotter = true;
                enableStealthValidation = false;
                enableTotalWar = false;
                break;
            case GameSetupPreset.FogOfWarTotal:
            default:
                enableLdtValidation = true;
                enableLosValidation = true;
                enableSpotter = true;
                enableStealthValidation = true;
                enableTotalWar = true;
                break;
        }

        SyncThreatRevisionFlags();
    }

    private void SyncThreatRevisionFlags()
    {
        ThreatRevisionTracker.SetMatchFlags(enableLdtValidation, enableLosValidation, enableSpotter);
    }

    private static TeamId ClampToTeamId(int value)
    {
        if (value < -1)
            value = -1;
        if (value > 3)
            value = 3;
        return (TeamId)value;
    }

    private void NormalizePlayersList()
    {
        if (players == null)
            players = new List<PlayerEntry>();

        for (int i = players.Count - 1; i >= 0; i--)
        {
            PlayerEntry entry = players[i];
            if (entry.teamId == TeamId.Neutral)
            {
                players.RemoveAt(i);
                continue;
            }

            entry.startMoney = Mathf.Max(0, entry.startMoney);
            entry.actualMoney = Mathf.Max(0, entry.actualMoney);
            entry.incomePerTurn = Mathf.Max(0, entry.incomePerTurn);
            players[i] = entry;
        }
    }

    private void SyncActivePlayerIndexFromActiveTeam()
    {
        if (players == null || players.Count == 0 || activeTeamId == (int)TeamId.Neutral)
        {
            activePlayerListIndex = -1;
            return;
        }

        TeamId activeTeam = ClampToTeamId(activeTeamId);
        activePlayerListIndex = FindPlayerIndexByTeam(activeTeam);
    }

    private void SetActivePlayerByIndex(int index, bool forceApply = false)
    {
        if (players == null || players.Count == 0)
            return;

        index = Mathf.Clamp(index, 0, players.Count - 1);
        activePlayerListIndex = index;
        activeTeamId = (int)players[index].teamId;
        ApplyActiveTeamIfChanged(force: forceApply);
    }

    private void SetNeutralActiveTeam()
    {
        activePlayerListIndex = -1;
        activeTeamId = (int)TeamId.Neutral;
        ApplyActiveTeamIfChanged(force: false);
    }

    private void ApplyActiveTeamIfChanged(bool force)
    {
        if (!force && appliedActiveTeamId == activeTeamId)
            return;

        appliedActiveTeamId = activeTeamId;
        if (Application.isPlaying)
            OnActiveTeamChanged?.Invoke(activeTeamId);
        TeleportCursorToActiveTeamHeadQuarterSilently();
        ReleaseUnitsForActiveTeam();
        if (!debugFogOfWarEnabled)
        {
            ResetFogOfWarRuntime(clearTilemap: true);
            ShowAllUnitsIgnoringFog();
            FlushTurnStartAutonomyHelper();
            return;
        }

        if (enableTotalWar)
        {
            RefreshFogOfWarForActiveTeam();
            RefreshRuntimeUnitFogVisibility();
            RunTurnStartStillObservedForActiveTeamStealthUnits();
        }
        else
        {
            ResetFogOfWarRuntime(clearTilemap: true);
            RefreshRuntimeUnitFogVisibility();
        }
        FlushTurnStartAutonomyHelper();
    }

    private void ApplyTeamFlipSettingsToSceneObjects()
    {
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;

            unit.ApplyTeamVisualFlipX(GetTeamFlipX(unit.TeamId));
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
                continue;

            construction.ApplyTeamVisualFlipX(GetTeamFlipX(construction.TeamId));
        }
    }

    private void ReleaseUnitsForActiveTeam()
    {
        if (!Application.isPlaying)
            return;
        if (activeTeamId < 0 && !includeNeutralTeam)
            return;

        ApplyEconomyAtTurnStartForActiveTeam();

        List<TurnStateManager.TurnStartAutonomyUpkeepEntry> turnStartAutonomyEntries = null;
        turnStartUnitsMarkedForFuelDepletionDeath.Clear();
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if ((int)unit.TeamId != activeTeamId)
                continue;

            unit.ConsumeForcedLayerLockTurn();

            if (pendingTurnStartUpkeep)
            {
                int turnStartUpkeep = OperationalAutonomyRules.GetTurnStartAutonomyUpkeep(unit, autonomyDatabase);
                if (turnStartUpkeep > 0)
                {
                    int beforeFuel = Mathf.Max(0, unit.CurrentFuel);
                    int afterFuel = Mathf.Max(0, beforeFuel - turnStartUpkeep);
                    int consumed = Mathf.Max(0, beforeFuel - afterFuel);
                    unit.SetCurrentFuel(afterFuel);
                    bool markedForFuelDepletionDeath = false;
                    if (beforeFuel > 0 && afterFuel <= 0)
                    {
                        bool isAirUnitInFlight =
                            unit.GetAircraftType() != AircraftType.None &&
                            !unit.IsAircraftGrounded &&
                            !unit.IsEmbarked;
                        if (isAirUnitInFlight && unit.gameObject.activeInHierarchy)
                        {
                            turnStartUnitsMarkedForFuelDepletionDeath.Add(unit);
                            markedForFuelDepletionDeath = true;
                        }
                    }

                    bool isAircraftUnit = unit.TryGetUnitData(out UnitData unitDataAtUpkeep)
                        && unitDataAtUpkeep != null
                        && unitDataAtUpkeep.IsAircraft();

                    if ((consumed > 0 && isAircraftUnit) || markedForFuelDepletionDeath)
                    {
                        turnStartAutonomyEntries ??= new List<TurnStateManager.TurnStartAutonomyUpkeepEntry>();
                        Vector3Int cell = unit.CurrentCellPosition;
                        cell.z = 0;
                        turnStartAutonomyEntries.Add(new TurnStateManager.TurnStartAutonomyUpkeepEntry(
                            ResolveRuntimeUnitName(unit),
                            cell,
                            consumed,
                            beforeFuel,
                            afterFuel));
                    }
                }
            }

            unit.ResetActed();
            unit.ClearReceivedSuppliesThisTurn();
        }

        pendingTurnStartAutonomyHelperEntries = turnStartAutonomyEntries;
        TryAutoAssignTurnStateManager();
        turnStateManager?.EnqueueTurnStartFuelDepletionDeaths(turnStartUnitsMarkedForFuelDepletionDeath);

        pendingTurnStartUpkeep = false;
    }

    private void FlushTurnStartAutonomyHelper()
    {
        TryAutoAssignTurnStateManager();
        turnStateManager?.ShowTurnStartAutonomyUpkeepHelper(pendingTurnStartAutonomyHelperEntries);
        pendingTurnStartAutonomyHelperEntries = null;
    }

    private int FindPlayerEconomyIndex(TeamId teamId)
    {
        if (players == null)
            return -1;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].teamId == teamId)
                return i;
        }

        return -1;
    }

    private void RecalculateIncomePerTurnForAllPlayers()
    {
        if (players == null || players.Count == 0)
            return;

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry entry = players[i];
            int income = 0;
            for (int c = 0; c < constructions.Length; c++)
            {
                ConstructionManager construction = constructions[c];
                if (construction == null)
                    continue;
                if (construction.TeamId != entry.teamId)
                    continue;

                income += Mathf.Max(0, construction.CapturedIncoming);
            }

            entry.incomePerTurn = Mathf.Max(0, income);
            players[i] = entry;
        }
    }

    private void ApplyEconomyAtTurnStartForActiveTeam()
    {
        if (!pendingTurnStartEconomy)
            return;
        if (players == null || players.Count == 0)
        {
            pendingTurnStartEconomy = false;
            return;
        }

        TeamId team = ClampToTeamId(activeTeamId);
        int playerIndex = FindPlayerEconomyIndex(team);
        if (playerIndex < 0)
        {
            pendingTurnStartEconomy = false;
            return;
        }

        RecalculateIncomePerTurnForAllPlayers();

        PlayerEntry entry = players[playerIndex];
        int credit = Mathf.Max(0, entry.incomePerTurn);
        if (!entry.startMoneyApplied)
        {
            credit += Mathf.Max(0, entry.startMoney);
            entry.startMoneyApplied = true;
        }

        if (credit > 0)
        {
            entry.actualMoney = Mathf.Max(0, entry.actualMoney + credit);
            PanelMoneyController.PushContextualUpdate(team, entry.actualMoney, "Incoming", credit);
        }

        players[playerIndex] = entry;
        pendingTurnStartEconomy = false;
    }

    private int FindPlayerIndexByTeam(TeamId team)
    {
        if (players == null || players.Count == 0)
            return -1;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].teamId == team)
                return i;
        }

        return -1;
    }

    private static bool GetDefaultFlipX(TeamId teamId)
    {
        return teamId == TeamId.Red || teamId == TeamId.Yellow;
    }

    private void TryRefreshIncomeFromConstructions(bool markDirtyInEditor)
    {
        ComputeConstructionIncomeSignature(out int signature, out int count);
        if (signature == cachedConstructionIncomeSignature && count == cachedConstructionIncomeCount)
            return;

        cachedConstructionIncomeSignature = signature;
        cachedConstructionIncomeCount = count;
        RecalculateIncomePerTurnForAllPlayers();

#if UNITY_EDITOR
        if (markDirtyInEditor && !Application.isPlaying)
            EditorUtility.SetDirty(this);
#endif
    }

    private static void ComputeConstructionIncomeSignature(out int signature, out int count)
    {
        signature = 17;
        count = 0;
        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
                continue;

            unchecked
            {
                signature = (signature * 31) + (int)construction.TeamId;
                signature = (signature * 31) + Mathf.Max(0, construction.CapturedIncoming);
                signature = (signature * 31) + construction.InstanceId;
            }

            count++;
        }
    }

    private static T GetValueOrDefault<T>(IList<T> list, int index, T defaultValue)
    {
        if (list == null || index < 0 || index >= list.Count)
            return defaultValue;
        return list[index];
    }

    private void TryAutoAssignCursorController()
    {
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
    }

    private void TryAutoAssignTurnStateManager()
    {
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();
    }

    private static string ResolveRuntimeUnitName(UnitManager unit)
    {
        if (unit == null)
            return "Unidade";
        if (!string.IsNullOrWhiteSpace(unit.UnitDisplayName))
            return unit.UnitDisplayName;
        if (!string.IsNullOrWhiteSpace(unit.UnitId))
            return unit.UnitId;
        return unit.name;
    }

    private void TryAutoAssignTurnTransitionReferences()
    {
        if (matchMusicAudioManager == null)
            matchMusicAudioManager = FindAnyObjectByType<MatchMusicAudioManager>();
    }

    private void TryAutoAssignFogOfWarReferences()
    {
        if (fogOfWarTilemap == null)
            fogOfWarTilemap = FindTilemapByName("FogOfWar");

        if (fogOfWarTerrainDatabase == null)
            fogOfWarTerrainDatabase = ResolveFogTerrainDatabase();
        if (fogOfWarDpqAirHeightConfig == null)
            fogOfWarDpqAirHeightConfig = ResolveFogDpqAirHeightConfig();
    }

    private static Tilemap FindTilemapByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null)
                continue;
            if (string.Equals(tilemap.name, targetName, StringComparison.OrdinalIgnoreCase))
                return tilemap;
        }

        return null;
    }

    public void RefreshFogOfWarForActiveTeam()
    {
        if (SuppressFogOfWarRefresh)
            return;

        if (!enableTotalWar)
            return;

        if (fogOfWarTilemap == null)
            return;
        if (activeTeamId < 0 && !includeNeutralTeam)
            return;

        ValidateFogOfWarSortingLayer();

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return;

        if (enableFogSourceDebugLogs)
        {
            Debug.Log(
                $"[FoW][Context] activeTeam={activeTeamId} " +
                $"controllerScene={gameObject.scene.name} " +
                $"fogScene={(fogOfWarTilemap != null ? fogOfWarTilemap.gameObject.scene.name : "-")} " +
                $"boardMap={boardMap.name} boardScene={boardMap.gameObject.scene.name}");
        }

        ResetFogOfWarRuntime(clearTilemap: false);
        InitializeFogOverlay(boardMap);
        if (!fogOverlayInitialized)
            return;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int unitsIncluded = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
            {
                if (enableFogSourceDebugLogs && unit != null)
                    Debug.Log($"[FoW][Unit][Skip] {unit.name} reason=inactive_or_embarked");
                continue;
            }
            if ((int)unit.TeamId != activeTeamId)
            {
                if (enableFogSourceDebugLogs)
                    Debug.Log($"[FoW][Unit][Skip] {unit.name} reason=other_team team={(int)unit.TeamId}");
                continue;
            }
            if (!IsUnitOnBoard(unit, boardMap))
            {
                if (enableFogSourceDebugLogs)
                {
                    string unitMap = unit.BoardTilemap != null ? unit.BoardTilemap.name : "-";
                    string unitScene = unit.gameObject.scene.name;
                    Debug.Log(
                        $"[FoW][Unit][Skip] {unit.name} reason=other_board_or_scene " +
                        $"unitMap={unitMap} unitScene={unitScene} " +
                        $"boardMap={boardMap.name} boardScene={boardMap.gameObject.scene.name}");
                }
                continue;
            }

            unitsIncluded++;
            if (enableFogSourceDebugLogs)
            {
                Debug.Log(
                    $"[FoW][Unit][Use] {unit.name} team={(int)unit.TeamId} " +
                    $"unitMap={unit.BoardTilemap.name} unitScene={unit.gameObject.scene.name}");
            }

            UpdateFogVisibilityForUnit(unit, boardMap);
        }

        if (enableFogSourceDebugLogs)
            Debug.Log($"[FoW][Unit][Summary] total={units.Length} included={unitsIncluded}");

        ApplyFriendlyConstructionVision(boardMap);
        RefreshRuntimeUnitFogVisibility();
        if (Application.isPlaying)
            OnFogOfWarUpdated?.Invoke();
    }

    public void NotifyUnitReachedHasAct(UnitManager unit)
    {
        if (!Application.isPlaying)
            return;
        if (unit != null)
            OnUnitActedStateChanged?.Invoke(unit);
        if (SuppressFogOfWarRefresh)
            return;
        if (!debugFogOfWarEnabled)
            return;
        if (!enableTotalWar)
            return;
        if (unit == null || !unit.gameObject.activeInHierarchy)
            return;
        if (!unit.HasActed)
            return;
        if (activeTeamId < 0)
            return;
        if ((int)unit.TeamId != activeTeamId)
            return;

        if (fogOfWarTilemap == null)
            TryAutoAssignFogOfWarReferences();
        if (fogOfWarTilemap == null)
            return;

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return;

        ValidateFogOfWarSortingLayer();
        if (fogCachedTeamId != activeTeamId || !fogOverlayInitialized)
        {
            RefreshFogOfWarForActiveTeam();
            TryPlaySkillDetectionSfxForActedUnit(unit, boardMap);
            TryRefreshDetectedPersistenceForActedUnit(unit, boardMap);
            return;
        }

        UpdateFogVisibilityForUnit(unit, boardMap);
        RefreshRuntimeUnitFogVisibility();
        TryPlaySkillDetectionSfxForActedUnit(unit, boardMap);
        TryRefreshDetectedPersistenceForActedUnit(unit, boardMap);
        OnFogOfWarUpdated?.Invoke();
    }

    private void RunTurnStartStillObservedForActiveTeamStealthUnits()
    {
        if (!Application.isPlaying || !debugFogOfWarEnabled || !enableTotalWar)
            return;
        if (!initialStealthDetectionBootstrapped)
            return;
        if (activeTeamId < 0)
            return;

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int scannedStealthUnits = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if ((int)unit.TeamId != activeTeamId)
                continue;
            if (!IsUnitOnBoard(unit, boardMap))
                continue;
            if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null)
                continue;
            if (!unitData.IsStealthUnit())
                continue;

            scannedStealthUnits++;
            TryRefreshDetectedPersistenceForActedUnit(unit, boardMap, allowWithoutHasAct: true);
        }

        Debug.Log($"[AindaMeVe][TurnStart] team={activeTeamId} scannedStealthUnits={scannedStealthUnits}");
    }

    private void TryPlaySkillDetectionSfxForActedUnit(
        UnitManager observer,
        Tilemap boardMap,
        bool allowSkillSfx = true)
    {
        if (observer == null)
            return;
        if (!observer.HasActed)
            return;
        if (!observer.TryGetUnitData(out UnitData observerData) || observerData == null)
            return;
        bool canPlaySkillSfx = allowSkillSfx && cursorController != null && observer.HasActed;

        Tilemap map = boardMap != null ? boardMap : ResolveFogBoardTilemap();
        if (map == null)
            return;

        List<PodeDetectarOption> detectedStealth = new List<PodeDetectarOption>();
        List<PodeDetectarOption> undetectedStealth = new List<PodeDetectarOption>();
        List<PodeDetectarOption> spottedCandidates = new List<PodeDetectarOption>();
        List<PodeDetectarOption> blockedByLos = new List<PodeDetectarOption>();

        PodeDetectarSensor.CollectDetection(
            observer,
            map,
            ResolveFogTerrainDatabase(),
            detectedStealth,
            undetectedStealth,
            spottedCandidates,
            blockedByLos,
            out _,
            ResolveFogDpqAirHeightConfig(),
            enableLosValidation,
            enableSpotter: false,
            enableStealthValidation);

        int observerTeamId = (int)observer.TeamId;
        Debug.Log(
            $"[PodeDetectar][Runtime] observer={observer.name} team={observerTeamId} " +
            $"detectedStealth={detectedStealth.Count} undetectedStealth={undetectedStealth.Count} " +
            $"spotted={spottedCandidates.Count} blockedLos={blockedByLos.Count}");
        for (int i = 0; i < detectedStealth.Count; i++)
        {
            PodeDetectarOption option = detectedStealth[i];
            if (option == null || option.targetUnit == null)
                continue;

            string reason = string.IsNullOrWhiteSpace(option.reason) ? "-" : option.reason;
            Debug.Log(
                $"[PodeDetectar][Runtime][Detected] observer={observer.name} -> target={option.targetUnit.name} " +
                $"layer={option.targetDomain}/{option.targetHeightLevel} reason={reason}");
        }

        bool appliedReveal = false;
        bool playedSkillSfx = false;
        HashSet<UnitManager> updatedTargets = new HashSet<UnitManager>();
        for (int i = 0; i < detectedStealth.Count; i++)
        {
            PodeDetectarOption option = detectedStealth[i];
            if (option == null || option.targetUnit == null)
                continue;
            if (!option.targetUnit.TryGetUnitData(out UnitData targetData) || targetData == null)
                continue;
            if (targetData.IsStealthUnit() && updatedTargets.Add(option.targetUnit))
            {
                // Gameplay runtime: qualquer unidade stealth-capable detectada por PodeDetectar
                // deve receber o marcador de "observada por time".
                RegisterStealthRevealFromDetection(observer, option.targetUnit);
                appliedReveal = true;
            }

            if (!TryResolveSkillMatchedDetectorSkill(observerData, targetData, option.targetDomain, option.targetHeightLevel, out SkillData matchedDetectorSkill))
                continue;
            if (matchedDetectorSkill == null)
                continue;

            if (!canPlaySkillSfx || playedSkillSfx || !IsSubmarineLikeDetectionTarget(option))
                continue;

            cursorController.TryPlaySkillSfx(matchedDetectorSkill, 1f);
            playedSkillSfx = true;
        }

        if (!playedSkillSfx &&
            canPlaySkillSfx &&
            IsSubmarineLikeObserver(observer))
        {
            bool hasAnyDetection = detectedStealth.Count > 0 || spottedCandidates.Count > 0;
            if (hasAnyDetection)
            {
                // Fallback: em deteccoes de submarino para alvos de superficie (ex.: fragata),
                // toca o sonar mesmo quando nao houver match de skill por stealth-target.
                if (TryResolveSonarSkill(observerData, out SkillData sonarSkill) && sonarSkill != null)
                    playedSkillSfx = cursorController.TryPlaySkillSfx(sonarSkill, 1f);

                if (!playedSkillSfx)
                    playedSkillSfx = cursorController.TryPlayUnitSkillSfx(observer, 1f);
            }
        }

        // Quando a unidade stealth-capable estiver fora da camada stealth ativa, ela pode entrar
        // como spottedCandidates. Ainda assim precisa receber olhinho ao ser observada.
        for (int i = 0; i < spottedCandidates.Count; i++)
        {
            PodeDetectarOption option = spottedCandidates[i];
            if (option == null || option.targetUnit == null)
                continue;
            if (!option.targetUnit.TryGetUnitData(out UnitData targetData) || targetData == null)
                continue;
            if (!targetData.IsStealthUnit())
                continue;
            if (!updatedTargets.Add(option.targetUnit))
                continue;

            RegisterStealthRevealFromDetection(observer, option.targetUnit);
            appliedReveal = true;
        }

        if (appliedReveal)
            RefreshRuntimeUnitFogVisibility();
    }

    private void TryBootstrapInitialStealthDetection()
    {
        if (!Application.isPlaying || initialStealthDetectionBootstrapped)
            return;
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return;

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int observersProcessed = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager observer = units[i];
            if (observer == null || !observer.gameObject.activeInHierarchy || observer.IsEmbarked)
                continue;
            if (!IsUnitOnBoard(observer, boardMap))
                continue;
            observersProcessed++;

            // 0) Start do jogo: roda PodeDetectar para cada unidade.
            TryPlaySkillDetectionSfxForActedUnit(observer, boardMap, allowSkillSfx: false);
            // 0) Start do jogo: roda AlguemMeVe para cada unidade.
            TryRefreshDetectedPersistenceForActedUnit(observer, boardMap, allowWithoutHasAct: true);
        }

        RefreshRuntimeUnitFogVisibility();

        initialStealthDetectionBootstrapped = true;
        Debug.Log($"[Sensors][Bootstrap] unitsProcessed={observersProcessed}");
    }

    private void RegisterStealthRevealFromDetection(UnitManager observer, UnitManager target)
    {
        if (observer == null || target == null)
            return;

        int detectorTeamId = (int)observer.TeamId;
        target.RegisterStealthReveal(detectorTeamId);
        target.AddCurrentlyObservedByTeam(detectorTeamId);
        target.RefreshRuntimeVisualState();
    }

    private static bool IsSubmarineLikeDetectionTarget(PodeDetectarOption option)
    {
        if (option == null)
            return false;

        return option.targetDomain == Domain.Submarine || option.targetHeightLevel == HeightLevel.Submerged;
    }

    private static bool IsSubmarineLikeObserver(UnitManager observer)
    {
        if (observer == null)
            return false;

        return observer.GetDomain() == Domain.Submarine || observer.GetHeightLevel() == HeightLevel.Submerged;
    }

    private static bool TryResolveSonarSkill(UnitData observerData, out SkillData sonarSkill)
    {
        sonarSkill = null;
        if (observerData == null || observerData.skills == null || observerData.skills.Count == 0)
            return false;

        for (int i = 0; i < observerData.skills.Count; i++)
        {
            SkillData skill = observerData.skills[i];
            if (skill == null)
                continue;

            string id = string.IsNullOrWhiteSpace(skill.id) ? string.Empty : skill.id.Trim();
            string display = string.IsNullOrWhiteSpace(skill.displayName) ? string.Empty : skill.displayName.Trim();
            string name = string.IsNullOrWhiteSpace(skill.name) ? string.Empty : skill.name.Trim();

            if (id.IndexOf("sonar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                display.IndexOf("sonar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("sonar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sonarSkill = skill;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveSkillMatchedDetectorSkill(
        UnitData observerData,
        UnitData targetData,
        Domain targetDomain,
        HeightLevel targetHeightLevel,
        out SkillData matchedDetectorSkill)
    {
        matchedDetectorSkill = null;
        if (observerData == null || targetData == null || observerData.visionSpecializations == null || observerData.visionSpecializations.Count == 0)
            return false;

        UnitVisionException match = null;
        for (int i = 0; i < observerData.visionSpecializations.Count; i++)
        {
            UnitVisionException entry = observerData.visionSpecializations[i];
            if (entry == null)
                continue;
            if (entry.domain != targetDomain || entry.heightLevel != targetHeightLevel)
                continue;

            match = entry;
            break;
        }

        if (match == null || match.detectUnitsWithFollowingSkills == null || match.detectUnitsWithFollowingSkills.Count == 0)
            return false;

        List<SkillData> targetStealthSkills = targetData.ResolveStealthSkillsForDetection(targetDomain, targetHeightLevel);
        if (targetStealthSkills == null || targetStealthSkills.Count == 0)
            return false;

        for (int i = 0; i < match.detectUnitsWithFollowingSkills.Count; i++)
        {
            SkillData detectorSkill = match.detectUnitsWithFollowingSkills[i];
            if (detectorSkill == null)
                continue;
            if (!ContainsSkill(targetStealthSkills, detectorSkill))
                continue;

            matchedDetectorSkill = detectorSkill;
            return true;
        }

        return false;
    }

    private static bool ContainsSkill(List<SkillData> haystack, SkillData needle)
    {
        if (haystack == null || needle == null)
            return false;

        string needleId = string.IsNullOrWhiteSpace(needle.id) ? string.Empty : needle.id.Trim();
        for (int i = 0; i < haystack.Count; i++)
        {
            SkillData current = haystack[i];
            if (current == null)
                continue;
            if (ReferenceEquals(current, needle))
                return true;

            string currentId = string.IsNullOrWhiteSpace(current.id) ? string.Empty : current.id.Trim();
            if (needleId.Length > 0 && currentId.Length > 0 &&
                string.Equals(needleId, currentId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void TryRefreshDetectedPersistenceForActedUnit(UnitManager actedUnit, Tilemap boardMap, bool allowWithoutHasAct = false)
    {
        if (actedUnit == null || !actedUnit.gameObject.activeInHierarchy || actedUnit.IsEmbarked)
            return;
        if (!allowWithoutHasAct && !actedUnit.HasActed)
            return;
        if (!actedUnit.TryGetUnitData(out UnitData actedData) || actedData == null)
            return;
        if (!actedData.IsStealthUnit())
            return;

        Tilemap map = boardMap != null ? boardMap : ResolveFogBoardTilemap();
        if (map == null)
            return;

        bool hadRevealBefore = HasAnyActiveEnemyReveal(actedUnit);
        HashSet<int> observerTeamIds = new HashSet<int>();
        int observerRadius = ResolveMaxEnemyObservationRadiusForTarget(actedUnit);
        bool isObservedNow = CollectObserverEnemyTeamsWithinRadius(actedUnit, map, observerRadius, observerTeamIds);
        string teamsObservedLabel = observerTeamIds.Count > 0
            ? string.Join(",", observerTeamIds)
            : "-";
        Debug.Log(
            $"[AindaMeVe][Runtime] target={actedUnit.name} team={(int)actedUnit.TeamId} " +
            $"hadRevealBefore={hadRevealBefore} observedNow={isObservedNow} observerRadius={observerRadius} observerTeams={teamsObservedLabel}");
        if (isObservedNow)
        {
            bool observedTeamsChanged = actedUnit.SyncCurrentlyObservedByTeams(observerTeamIds);
            if (observedTeamsChanged)
            {
                actedUnit.RefreshRuntimeVisualState();
                RefreshRuntimeUnitFogVisibility();
            }

            return;
        }

        bool observedTeamsCleared = actedUnit.ClearCurrentlyObservedByTeams();
        if (hadRevealBefore)
        {
            actedUnit.ClearStealthRevealState();
            actedUnit.RefreshRuntimeVisualState();
            RefreshRuntimeUnitFogVisibility();
            Debug.Log($"[AindaMeVe][Runtime][Clear] target={actedUnit.name} -> nenhum inimigo detectando.");
            return;
        }

        if (observedTeamsCleared)
            actedUnit.RefreshRuntimeVisualState();
    }

    private int ResolveMaxEnemyObservationRadiusForTarget(UnitManager target)
    {
        const int MaxObservationScanRadius = 7;

        if (target == null)
            return 1;

        Tilemap boardMap = target.BoardTilemap != null
            ? target.BoardTilemap
            : ResolveFogBoardTilemap();
        if (boardMap == null)
            return 1;

        Domain targetDomain = target.GetDomain();
        HeightLevel targetHeight = target.GetHeightLevel();
        int targetTeamId = (int)target.TeamId;

        int maxRange = 1;
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager observer = units[i];
            if (observer == null || observer == target || !observer.gameObject.activeInHierarchy || observer.IsEmbarked)
                continue;
            if ((int)observer.TeamId == targetTeamId)
                continue;
            if (!IsUnitOnBoard(observer, boardMap))
                continue;

            int observerRange = observer.Visao;
            if (observer.TryGetUnitData(out UnitData observerData) && observerData != null)
                observerRange = Mathf.Max(0, observerData.ResolveVisionFor(targetDomain, targetHeight));

            if (observerRange > maxRange)
                maxRange = observerRange;
        }

        maxRange = Mathf.Clamp(maxRange, 1, MaxObservationScanRadius);
        return maxRange;
    }

    private bool HasAnyActiveEnemyReveal(UnitManager target)
    {
        if (target == null)
            return false;

        int ownerTeamId = (int)target.TeamId;
        for (int teamId = -1; teamId <= 3; teamId++)
        {
            if (teamId == ownerTeamId)
                continue;
            if (target.IsStealthRevealedForTeam(teamId, currentTurn))
                return true;
        }

        return false;
    }

    private bool CollectObserverEnemyTeamsWithinRadius(UnitManager target, Tilemap map, int radius, HashSet<int> observerTeamIds)
    {
        if (observerTeamIds == null)
            return false;
        observerTeamIds.Clear();
        if (target == null || map == null || radius < 0)
            return false;

        Vector3Int center = target.CurrentCellPosition;
        center.z = 0;
        HashSet<Vector3Int> cellsInRadius = BuildCellsInRadius(map, center, radius);
        if (cellsInRadius.Count <= 0)
            return false;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager observer = units[i];
            if (observer == null || observer == target || !observer.gameObject.activeInHierarchy || observer.IsEmbarked)
                continue;
            if (observer.TeamId == target.TeamId)
                continue;
            if (!IsUnitOnBoard(observer, map))
                continue;

            Vector3Int observerCell = observer.CurrentCellPosition;
            observerCell.z = 0;
            if (!cellsInRadius.Contains(observerCell))
                continue;

            int observerTeamId = (int)observer.TeamId;
            if (observerTeamId < -1 || observerTeamId > 3)
                continue;

            bool enforceStealthValidation = enableStealthValidation && !target.HasFiredThisTurn;
            bool canObserveTarget = PodeDetectarSensor.IsTargetObservedByTeam(
                target,
                observerTeamId,
                map,
                ResolveFogTerrainDatabase(),
                ResolveFogDpqAirHeightConfig(),
                enableLosValidation,
                enableSpotter: false,
                enforceStealthValidation);
            if (!canObserveTarget)
                continue;

            observerTeamIds.Add(observerTeamId);
        }

        return observerTeamIds.Count > 0;
    }

    private static HashSet<Vector3Int> BuildCellsInRadius(Tilemap map, Vector3Int origin, int radius)
    {
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        if (map == null || radius < 0)
            return visited;

        origin.z = 0;
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Dictionary<Vector3Int, int> distance = new Dictionary<Vector3Int, int>();
        queue.Enqueue(origin);
        visited.Add(origin);
        distance[origin] = 0;

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int currentDistance = distance[current];
            if (currentDistance >= radius)
                continue;

            neighbors.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(map, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (visited.Contains(next))
                    continue;

                visited.Add(next);
                distance[next] = currentDistance + 1;
                queue.Enqueue(next);
            }
        }

        return visited;
    }

    public bool IsUnitVisibleForActiveTeam(UnitManager unit)
    {
        if (!debugFogOfWarEnabled)
            return true;

        if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
            return false;

        int cacheIndex = ResolveFogCacheIndex(unit);
        if (fogCachedTeamId == activeTeamId &&
            fogUnitVisibilityByCacheIndex.TryGetValue(cacheIndex, out bool cachedVisible))
        {
            return cachedVisible;
        }

        return ComputeIsUnitVisibleForActiveTeam(unit);
    }

    private bool ComputeIsUnitVisibleForActiveTeam(UnitManager unit)
    {
        if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
            return false;

        TeamId unitTeam = unit.TeamId;
        if ((int)unitTeam == activeTeamId)
            return true;

        if (!enableTotalWar)
            return true;

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        if (!IsCellVisibleForActiveTeam(cell))
            return false;

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return false;

        bool enforceStealthValidation = enableStealthValidation && !unit.HasFiredThisTurn;
        return PodeDetectarSensor.IsTargetObservedByTeam(
            unit,
            activeTeamId,
            boardMap,
            ResolveFogTerrainDatabase(),
            ResolveFogDpqAirHeightConfig(),
            enableLosValidation,
            enableSpotter: false,
            enforceStealthValidation);
    }

    public bool IsCellVisibleForActiveTeam(Vector3Int cell)
    {
        if (!debugFogOfWarEnabled)
            return true;
        if (!enableTotalWar)
            return true;
        if (fogCachedTeamId != activeTeamId)
            return false;

        cell.z = 0;
        return fogVisibleContributorsByCell.TryGetValue(cell, out int contributors) && contributors > 0;
    }

    private static bool HasAnyNeutralUnitsInField()
    {
        List<UnitManager> units = UnitManager.AllActive;
        if (units == null || units.Count == 0)
            return false;

        for (int i = units.Count - 1; i >= 0; i--)
        {
            UnitManager unit = units[i];
            if (unit == null)
            {
                units.RemoveAt(i);
                continue;
            }

            if (!unit.gameObject.activeInHierarchy)
                continue;

            if (unit.TeamId == TeamId.Neutral)
                return true;
        }

        return false;
    }

    private void ValidateFogOfWarSortingLayer()
    {
        if (fogSortingLayerValidated || fogOfWarTilemap == null)
            return;

        fogSortingLayerValidated = true;
        TilemapRenderer renderer = fogOfWarTilemap.GetComponent<TilemapRenderer>();
        if (renderer == null)
            return;

        const string expectedLayer = "SFX";
        string currentLayer = SortingLayer.IDToName(renderer.sortingLayerID);
        if (!string.Equals(currentLayer, expectedLayer, StringComparison.OrdinalIgnoreCase))
            Debug.LogWarning($"[FogOfWar] Sorting layer atual = {currentLayer}. Esperado = {expectedLayer}.");
        else
            Debug.Log("[FogOfWar] Sorting layer validada em SFX.");
    }

    private Tilemap ResolveFogBoardTilemap()
    {
        Scene contextScene = fogOfWarTilemap != null
            ? fogOfWarTilemap.gameObject.scene
            : gameObject.scene;

        if (cursorController != null && cursorController.BoardTilemap != null)
        {
            Tilemap cursorMap = cursorController.BoardTilemap;
            if (cursorMap.gameObject.scene == contextScene)
                return cursorMap;
        }

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.BoardTilemap == null)
                continue;
            if (unit.gameObject.scene != contextScene)
                continue;

            return unit.BoardTilemap;
        }

        if (fogOfWarTilemap != null)
        {
            for (int i = 0; i < units.Length; i++)
            {
                UnitManager unit = units[i];
                if (unit == null || unit.BoardTilemap == null)
                    continue;
                if (unit.gameObject.scene != contextScene)
                    continue;

                return unit.BoardTilemap;
            }
        }

        return null;
    }

    private static void CollectBoardCells(Tilemap boardMap, List<Vector3Int> output)
    {
        if (boardMap == null || output == null)
            return;

        BoundsInt bounds = boardMap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (boardMap.HasTile(cell))
                    output.Add(cell);
            }
        }
    }

    private void ResetFogOfWarRuntime(bool clearTilemap)
    {
        fogBoardCellsBuffer.Clear();
        fogVisibleCellsBuffer.Clear();
        fogVisibleCellsByUnit.Clear();
        fogVisibleContributorsByCell.Clear();
        fogUnitVisibilityByCacheIndex.Clear();
        fogUnitVisibleScratchBuffer.Clear();
        fogCachedTeamId = int.MinValue;
        fogOverlayInitialized = false;
        if (clearTilemap && fogOfWarTilemap != null)
            fogOfWarTilemap.ClearAllTiles();
    }

    private void InitializeFogOverlay(Tilemap boardMap)
    {
        fogBoardCellsBuffer.Clear();
        CollectBoardCells(boardMap, fogBoardCellsBuffer);
        if (fogBoardCellsBuffer.Count <= 0)
        {
            fogOfWarTilemap.ClearAllTiles();
            fogOverlayInitialized = false;
            return;
        }

        fogOfWarTilemap.ClearAllTiles();
        Color fogColor = new Color(0f, 0f, 0f, Mathf.Clamp01(fogOfWarAlpha));
        for (int i = 0; i < fogBoardCellsBuffer.Count; i++)
        {
            Vector3Int cell = fogBoardCellsBuffer[i];
            TileBase tile = ResolveFogTileForCell(boardMap, cell);
            if (tile == null)
                continue;

            fogOfWarTilemap.SetTile(cell, tile);
            fogOfWarTilemap.SetTileFlags(cell, TileFlags.None);
            fogOfWarTilemap.SetColor(cell, fogColor);
        }

        fogCachedTeamId = activeTeamId;
        fogOverlayInitialized = true;
    }

    private void UpdateFogVisibilityForUnit(UnitManager unit, Tilemap boardMap)
    {
        if (unit == null)
            return;

        int cacheIndex = ResolveFogCacheIndex(unit);
        FogOfWarUnitCacheKey nextKey = BuildFogUnitCacheKey(unit, boardMap);
        if (fogVisibleCellsByUnit.TryGetValue(cacheIndex, out FogOfWarUnitCacheEntry cacheEntry) &&
            cacheEntry != null &&
            cacheEntry.key.Equals(nextKey))
        {
            return;
        }

        if (cacheEntry == null)
        {
            cacheEntry = new FogOfWarUnitCacheEntry();
            fogVisibleCellsByUnit[cacheIndex] = cacheEntry;
        }

        if (cacheEntry.visibleCells.Count > 0)
        {
            foreach (Vector3Int cell in cacheEntry.visibleCells)
                ApplyFogContribution(cell, -1, boardMap);
            cacheEntry.visibleCells.Clear();
        }

        if (!unit.gameObject.activeInHierarchy || unit.IsEmbarked || (int)unit.TeamId != activeTeamId)
        {
            cacheEntry.key = nextKey;
            return;
        }

        fogUnitVisibleScratchBuffer.Clear();
        PodeDetectarSensor.CollectVisibleCells(
            unit,
            boardMap,
            ResolveFogTerrainDatabase(),
            fogUnitVisibleScratchBuffer,
            ResolveFogDpqAirHeightConfig(),
            enableLosValidation,
            enableSpotter: false,
            useOccupantLayerForTarget: false,
            preserveObserverLayerRangeForHexVisibility: true,
            useRangeOnlyForAirHighWhenConfigured: true);

        foreach (Vector3Int cell in fogUnitVisibleScratchBuffer)
        {
            cacheEntry.visibleCells.Add(cell);
            ApplyFogContribution(cell, +1, boardMap);
        }

        cacheEntry.key = nextKey;
    }

    private void ApplyFogContribution(Vector3Int cell, int delta, Tilemap boardMap)
    {
        if (delta == 0)
            return;

        if (!fogVisibleContributorsByCell.TryGetValue(cell, out int current))
            current = 0;

        int next = Mathf.Max(0, current + delta);
        if (next == current)
            return;

        if (next <= 0)
            fogVisibleContributorsByCell.Remove(cell);
        else
            fogVisibleContributorsByCell[cell] = next;

        if (current <= 0 && next > 0)
        {
            fogOfWarTilemap.SetTile(cell, null);
            return;
        }

        if (current > 0 && next <= 0)
        {
            TileBase tile = ResolveFogTileForCell(boardMap, cell);
            if (tile == null)
                return;

            fogOfWarTilemap.SetTile(cell, tile);
            fogOfWarTilemap.SetTileFlags(cell, TileFlags.None);
            fogOfWarTilemap.SetColor(cell, new Color(0f, 0f, 0f, Mathf.Clamp01(fogOfWarAlpha)));
        }
    }

    private TileBase ResolveFogTileForCell(Tilemap boardMap, Vector3Int cell)
    {
        if (fogOfWarOverlayTile != null)
            return fogOfWarOverlayTile;
        if (boardMap == null)
            return null;
        return boardMap.GetTile(cell);
    }

    private void ApplyFriendlyConstructionVision(Tilemap boardMap)
    {
        if (boardMap == null || activeTeamId < 0)
            return;

        List<ConstructionManager> constructions = ConstructionManager.AllActive;
        int constructionsIncluded = 0;
        int activeTeamCandidates = 0;
        for (int i = constructions.Count - 1; i >= 0; i--)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
            {
                constructions.RemoveAt(i);
                continue;
            }
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if ((int)construction.TeamId != activeTeamId)
            {
                if (enableFogSourceDebugLogs)
                    Debug.Log($"[FoW][Construction][Skip] {construction?.name} reason=other_team team={(int)construction.TeamId}");
                continue;
            }
            activeTeamCandidates++;
            Tilemap constructionMap = construction.BoardTilemap;
            if (constructionMap == null && construction.gameObject.scene == boardMap.gameObject.scene)
            {
                // Event-driven construction refresh removed the old periodic path that
                // indirectly auto-bound missing board refs. Ensure FoW can still resolve.
                construction.SetBoardTilemap(boardMap);
                constructionMap = construction.BoardTilemap;
            }

            if (constructionMap == null || constructionMap != boardMap)
            {
                if (enableFogSourceDebugLogs)
                {
                    string cMap = constructionMap != null ? constructionMap.name : "-";
                    Debug.Log(
                        $"[FoW][Construction][Skip] {construction.name} reason=other_board " +
                        $"constructionMap={cMap} boardMap={boardMap.name}");
                }
                continue;
            }
            if (construction.gameObject.scene != boardMap.gameObject.scene)
            {
                if (enableFogSourceDebugLogs)
                {
                    Debug.Log(
                        $"[FoW][Construction][Skip] {construction.name} reason=other_scene " +
                        $"constructionScene={construction.gameObject.scene.name} boardScene={boardMap.gameObject.scene.name}");
                }
                continue;
            }

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (boardMap.GetTile(cell) == null)
                continue;

            constructionsIncluded++;
            if (enableFogSourceDebugLogs)
                Debug.Log($"[FoW][Construction][Use] {construction.name} cell={cell.x},{cell.y}");
            ApplyFogContribution(cell, +1, boardMap);
        }

        Debug.Log(
            $"[FoW][Construction][Temp] allActive={constructions.Count} " +
            $"activeTeamCandidates={activeTeamCandidates} included={constructionsIncluded} activeTeam={activeTeamId}");

        if (enableFogSourceDebugLogs)
            Debug.Log($"[FoW][Construction][Summary] total={constructions.Count} included={constructionsIncluded}");
    }

    private void RefreshRuntimeUnitFogVisibility()
    {
        if (!debugFogOfWarEnabled)
        {
            ShowAllUnitsIgnoringFog();
            return;
        }

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        fogUnitVisibilityByCacheIndex.Clear();
        Tilemap boardMap = ResolveFogBoardTilemap();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if (boardMap != null && !IsUnitOnBoard(unit, boardMap))
                continue;

            bool visible = ComputeIsUnitVisibleForActiveTeam(unit);
            fogUnitVisibilityByCacheIndex[ResolveFogCacheIndex(unit)] = visible;
            unit.SetFogOfWarVisibility(visible);
        }
    }

    public void SetFogOfWarDebugEnabled(bool enabled)
    {
        if (debugFogOfWarEnabled == enabled)
            return;

        debugFogOfWarEnabled = enabled;
        if (!enabled)
        {
            ResetFogOfWarRuntime(clearTilemap: true);
            ShowAllUnitsIgnoringFog();
            Debug.Log("[Debug Command] FoW OFF (debug).");
            return;
        }

        if (enableTotalWar)
            RefreshFogOfWarForActiveTeam();
        else
            ResetFogOfWarRuntime(clearTilemap: true);

        RefreshRuntimeUnitFogVisibility();
        Debug.Log("[Debug Command] FoW ON (debug).");
    }

    private void ShowAllUnitsIgnoringFog()
    {
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        fogUnitVisibilityByCacheIndex.Clear();
        Tilemap boardMap = ResolveFogBoardTilemap();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if (boardMap != null && !IsUnitOnBoard(unit, boardMap))
                continue;

            fogUnitVisibilityByCacheIndex[ResolveFogCacheIndex(unit)] = true;
            unit.SetFogOfWarVisibility(true);
        }
    }

    private static bool IsUnitOnBoard(UnitManager unit, Tilemap boardMap)
    {
        if (unit == null || boardMap == null)
            return false;

        if (unit.BoardTilemap == null || unit.BoardTilemap != boardMap)
            return false;

        return unit.gameObject.scene == boardMap.gameObject.scene;
    }

    private static int ResolveFogCacheIndex(UnitManager unit)
    {
        if (unit == null)
            return 0;

        int instanceId = unit.InstanceId;
        if (instanceId > 0)
            return instanceId;
        return unit.GetInstanceID();
    }

    private FogOfWarUnitCacheKey BuildFogUnitCacheKey(UnitManager unit, Tilemap boardMap)
    {
        int snapshotHash = BuildFogUnitSnapshotHash(unit, boardMap);
        int globalBoardRevision = ThreatRevisionTracker.GlobalBoardRevision;
        int teamObserverRevision = ThreatRevisionTracker.GetTeamObserverRevision(activeTeamId);
        int sensorFlagsHash = BuildFogSensorFlagsHash(enableLosValidation);
        return new FogOfWarUnitCacheKey(snapshotHash, globalBoardRevision, teamObserverRevision, sensorFlagsHash);
    }

    private int BuildFogUnitSnapshotHash(UnitManager unit, Tilemap boardMap)
    {
        unchecked
        {
            if (unit == null)
                return 0;

            int hash = 17;
            Vector3Int cell = unit.CurrentCellPosition;
            hash = (hash * 31) + cell.x;
            hash = (hash * 31) + cell.y;
            hash = (hash * 31) + (int)unit.TeamId;
            hash = (hash * 31) + (int)unit.GetDomain();
            hash = (hash * 31) + (int)unit.GetHeightLevel();
            hash = (hash * 31) + (unit.IsEmbarked ? 1 : 0);
            hash = (hash * 31) + Mathf.Max(1, unit.Visao);
            hash = (hash * 31) + (boardMap != null ? boardMap.GetInstanceID() : 0);
            TerrainDatabase fogTerrainDb = ResolveFogTerrainDatabase();
            DPQAirHeightConfig fogAirConfig = ResolveFogDpqAirHeightConfig();
            hash = (hash * 31) + (fogTerrainDb != null ? fogTerrainDb.GetInstanceID() : 0);
            hash = (hash * 31) + (fogAirConfig != null ? fogAirConfig.GetInstanceID() : 0);
            return hash;
        }
    }

    private TerrainDatabase ResolveFogTerrainDatabase()
    {
        if (fogOfWarTerrainDatabase != null)
            return fogOfWarTerrainDatabase;

        if (turnStateManager != null && turnStateManager.TerrainDatabaseRef != null)
        {
            fogOfWarTerrainDatabase = turnStateManager.TerrainDatabaseRef;
            return fogOfWarTerrainDatabase;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TerrainDatabase db = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(path);
            if (db != null)
            {
                fogOfWarTerrainDatabase = db;
                return fogOfWarTerrainDatabase;
            }
        }
#endif

        return fogOfWarTerrainDatabase;
    }

    private DPQAirHeightConfig ResolveFogDpqAirHeightConfig()
    {
        if (fogOfWarDpqAirHeightConfig != null)
            return fogOfWarDpqAirHeightConfig;

        if (turnStateManager != null && turnStateManager.DpqAirHeightConfigRef != null)
        {
            fogOfWarDpqAirHeightConfig = turnStateManager.DpqAirHeightConfigRef;
            return fogOfWarDpqAirHeightConfig;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:DPQAirHeightConfig");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DPQAirHeightConfig config = AssetDatabase.LoadAssetAtPath<DPQAirHeightConfig>(path);
            if (config != null)
            {
                fogOfWarDpqAirHeightConfig = config;
                return fogOfWarDpqAirHeightConfig;
            }
        }
#endif

        return fogOfWarDpqAirHeightConfig;
    }

    private static int BuildFogSensorFlagsHash(bool enableLos)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (enableLos ? 1 : 0);
            return hash;
        }
    }

    private void PlayAdvanceTurnSfx()
    {
        cursorController?.PlayEndingTurnSfx(1f);
    }

    private void TeleportCursorToActiveTeamHeadQuarterSilently()
    {
        if (!Application.isPlaying)
            return;
        if (activeTeamId < 0 && !includeNeutralTeam)
            return;
        if (cursorController == null)
            return;

        if (!TeamAnchorResolver.TryResolveAnchorCell(activeTeamId, out Vector3Int anchorCell))
        {
            // Fallback para turno neutro sem HQ: teleporta para a unidade neutra mais proxima.
            if (activeTeamId == (int)TeamId.Neutral && includeNeutralTeam)
                TryTeleportCursorToNearestReadyUnitForActiveTeam();
            return;
        }

        cursorController.SetCell(anchorCell, playMoveSfx: false);
    }

    private void TryTeleportCursorToNearestReadyUnitForActiveTeam()
    {
        if (cursorController == null)
            return;

        List<UnitManager> units = UnitManager.AllActive;
        if (units == null || units.Count == 0)
            return;

        Vector3Int origin = cursorController.CurrentCell;
        origin.z = 0;
        bool found = false;
        Vector3Int bestCell = origin;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked || unit.HasActed)
                continue;
            if ((int)unit.TeamId != activeTeamId)
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;

            float dx = cell.x - origin.x;
            float dy = cell.y - origin.y;
            float distanceSqr = (dx * dx) + (dy * dy);
            if (!found || distanceSqr < bestDistanceSqr)
            {
                found = true;
                bestDistanceSqr = distanceSqr;
                bestCell = cell;
            }
        }

        if (found)
            cursorController.SetCell(bestCell, playMoveSfx: false);
    }
}
