using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MatchController : MonoBehaviour
{
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
    [SerializeField, Min(1)] private int maxUnitsPerTeam = 40;
    [SerializeField] private AutonomyDatabase autonomyDatabase;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private TurnStateManager turnStateManager;
    [Header("Turn Transition")]
    [SerializeField] private MatchMusicAudioManager matchMusicAudioManager;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPreDelay = 0.5f;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPostDelay = 0.2f;
    [SerializeField] private int activePlayerListIndex = 0;
    [SerializeField, HideInInspector] private int appliedActiveTeamId = int.MinValue;
    [SerializeField, HideInInspector] private bool pendingTurnStartUpkeep;
    [SerializeField, HideInInspector] private bool pendingTurnStartEconomy = true;
    [SerializeField, HideInInspector] private int cachedConstructionIncomeSignature;
    [SerializeField, HideInInspector] private int cachedConstructionIncomeCount;
    [System.NonSerialized] private readonly List<TeamId> playersView = new List<TeamId>();
    [System.NonSerialized] private List<TurnStateManager.TurnStartAutonomyUpkeepEntry> pendingTurnStartAutonomyHelperEntries;
    [System.NonSerialized] private readonly List<UnitManager> turnStartUnitsMarkedForFuelDepletionDeath = new List<UnitManager>();

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
        if (teamId == TeamId.Neutral)
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
        ApplyActiveTeamIfChanged(force: true);
        ApplyTeamFlipSettingsToSceneObjects();
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
        ApplyActiveTeamIfChanged(force: false);
        ApplyTeamFlipSettingsToSceneObjects();
    }
#endif

    private void Update()
    {
        TryRefreshIncomeFromConstructions(markDirtyInEditor: !Application.isPlaying);
        SyncThreatRevisionFlags();

        if (!Application.isPlaying)
            return;

        TryAutoAssignCursorController();
        TryAutoAssignTurnStateManager();
        TryAutoAssignTurnTransitionReferences();
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
            if (includeNeutralTeam)
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

            if (includeNeutralTeam)
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
            if (includeNeutralTeam)
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
            if (includeNeutralTeam)
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
                break;
            case GameSetupPreset.FisicaBasica:
                enableLdtValidation = true;
                enableLosValidation = false;
                enableSpotter = false;
                enableStealthValidation = false;
                break;
            case GameSetupPreset.AMontanhaAvacalha:
                enableLdtValidation = true;
                enableLosValidation = true;
                enableSpotter = false;
                enableStealthValidation = false;
                break;
            case GameSetupPreset.NeblinaLeve:
                enableLdtValidation = true;
                enableLosValidation = true;
                enableSpotter = true;
                enableStealthValidation = false;
                break;
            case GameSetupPreset.FogOfWarTotal:
            default:
                enableLdtValidation = true;
                enableLosValidation = true;
                enableSpotter = true;
                enableStealthValidation = true;
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
        TeleportCursorToActiveTeamHeadQuarterSilently();
        ReleaseUnitsForActiveTeam();
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
        if (activeTeamId < 0)
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

    private void PlayAdvanceTurnSfx()
    {
        cursorController?.PlayEndingTurnSfx(1f);
    }

    private void TeleportCursorToActiveTeamHeadQuarterSilently()
    {
        if (!Application.isPlaying)
            return;
        if (activeTeamId < 0)
            return;
        if (cursorController == null)
            return;

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        ConstructionManager bestHq = null;
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager c = constructions[i];
            if (c == null || !c.gameObject.activeInHierarchy)
                continue;
            if (!IsHeadQuarterConstruction(c))
                continue;
            if ((int)c.TeamId != activeTeamId)
                continue;

            if (bestHq == null || c.InstanceId < bestHq.InstanceId)
                bestHq = c;
        }

        if (bestHq == null)
            return;

        Vector3Int hqCell = bestHq.CurrentCellPosition;
        hqCell.z = 0;
        cursorController.SetCell(hqCell, playMoveSfx: false);
    }

    private static bool IsHeadQuarterConstruction(ConstructionManager construction)
    {
        if (construction == null)
            return false;

        if (construction.IsPlayerHeadQuarter)
            return true;

        string constructionId = construction.ConstructionId;
        if (!string.IsNullOrWhiteSpace(constructionId) &&
            string.Equals(constructionId.Trim(), "hq", System.StringComparison.OrdinalIgnoreCase))
            return true;

        string displayName = construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(displayName) &&
            displayName.IndexOf("hq", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
