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
    private const int MaxVictoryStarsGoal = 12;
    public static event Action<int> OnActiveTeamChanged;
    public static event Action<UnitManager> OnUnitActedStateChanged;
    public static event Action OnFogOfWarUpdated;
    public static event Action OnBeforeAdvanceTurn;
    // Disparado quando a configuracao de slots de time muda (ex: TeamId de um slot alterado no editor).
    public static event Action OnSlotConfigChanged;
    public static event Action<TeamId> OnTeamDefeated;

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

    private readonly struct FogCollectPerfEntry
    {
        public readonly string unitName;
        public readonly double collectMs;
        public readonly int visibleCellCount;

        public FogCollectPerfEntry(string unitName, double collectMs, int visibleCellCount)
        {
            this.unitName = unitName ?? string.Empty;
            this.collectMs = Math.Max(0d, collectMs);
            this.visibleCellCount = Math.Max(0, visibleCellCount);
        }
    }

    public sealed class FogUnitContributorDebugInfo
    {
        public UnitManager targetUnit;
        public bool isVisibleForActiveTeam;
        public readonly List<UnitManager> contributors = new List<UnitManager>();
    }


    // Override manual da orientacao (flipX) de um slot. Auto segue o calculo
    // automatico por posicao do HQ; Normal/Espelhado forcam o valor.
    public enum FlipXOverrideMode
    {
        Auto = 0,
        Normal = 1,
        Espelhado = 2
    }

    [System.Serializable]
    private struct PlayerEntry
    {
        public TeamId teamId;
        [HideInInspector] public bool flipX;
        public FlipXOverrideMode flipXOverride;
        public bool isAI;
        public bool commandServiceAutomatic;
        public bool defeated;
        [Min(0)] public int startMoney;
        [Min(0)] public int actualMoney;
        [Min(0)] public int incomePerTurn;
        [SerializeField, HideInInspector] public bool startMoneyApplied;
    }

    [System.Serializable]
    private struct TeamVictoryEntry
    {
        public TeamId teamId;
        [Min(0)] public int stars;
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
        new PlayerEntry { teamId = TeamId.Green, flipX = false, defeated = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false },
        new PlayerEntry { teamId = TeamId.Red, flipX = true, defeated = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false },
        new PlayerEntry { teamId = TeamId.Blue, flipX = false, defeated = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false },
        new PlayerEntry { teamId = TeamId.Yellow, flipX = true, defeated = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false }
    };
    [SerializeField] private bool includeNeutralTeam = false;
    [SerializeField] private bool economyEnabled = true;
    // Placeholder para futura pintura de visibilidade no mapa (nao governa regras de combate no momento).
    [SerializeField, HideInInspector] private bool fogOfWar = true;
    [Tooltip("Calcula automaticamente o flipX de cada slot com base na posicao do HQ em relacao ao centro do mapa.")]
    [SerializeField] private bool autoFlipXFromHqPositions = true;
    [Header("Gameplay Setup")]
    [SerializeField] private GameSetupPreset gameSetup = GameSetupPreset.FogOfWarTotal;
    [SerializeField] private bool enableLdtValidation = true;
    [SerializeField] private bool enableLosValidation = true;
    [SerializeField] private bool enableSpotter = true;
    [SerializeField] private bool enableStealthValidation = true;
    [SerializeField] private bool enableTotalWar = true;
    [Tooltip("Permite inferir acoes contextuais a partir de cliques no mapa.")]
    [SerializeField] private bool atalhoContextual = false;
    [FormerlySerializedAs("passarTurnoSemConfirmacao")]
    [Tooltip("Se ativo, o atalho R abre a confirmacao/validacao do panel_helper. Se desativado, R encerra o turno diretamente.")]
    [SerializeField] private bool atalhoRPassarTurnoUsaConfirmacao = false;
    [Tooltip("Se false, o time com 0 unidades nao e eliminado automaticamente. Util para testes sem unidades.")]
    [SerializeField] private bool allowDefeatForZeroUnits = true;
    [Tooltip("Se true, capturar o QG (isPlayerHeadQuarter) de um time o elimina imediatamente e pode encerrar a partida.")]
    [SerializeField] private bool allowDefeatForHeadQuarterCapture = true;
    [SerializeField, Min(1)] private int maxUnitsPerTeam = 40;
    [Header("Tutorial")]
    [Tooltip("Tutorial ativo desta partida. Nulo = sandbox/campanha sem tutorial guiado.")]
    [SerializeField] private TutorialData activeTutorial;
    [SerializeField] private AutonomyDatabase autonomyDatabase;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private TurnStateManager turnStateManager;
    [FormerlySerializedAs("helpManager")]
    [SerializeField] private DialogManager dialogManager;

    public DialogManager DialogManager => dialogManager;
    [Header("Turn Transition")]
    [SerializeField] private MatchMusicAudioManager matchMusicAudioManager;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPreDelay = 0.1f;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPostDelay = 0f;
    [Header("Victory Stars")]
    [SerializeField] private bool enableVictoryStars = true;
    [SerializeField] [Range(1, MaxVictoryStarsGoal)] private int victoryStarsToWin = 5;
    [SerializeField] private bool freezeTurnAdvanceAfterVictory = true;
    [SerializeField] private List<TeamVictoryEntry> victoryStarsByTeam = new List<TeamVictoryEntry>();
    [SerializeField, HideInInspector] private bool hasVictoryWinner;
    [SerializeField, HideInInspector] private TeamId victoryWinnerTeam = TeamId.Neutral;
    [Header("Fog Of War")]
    [SerializeField] private FogOfWarController fogOfWarController;
    [SerializeField] private Tilemap fogOfWarTilemap;
    [SerializeField] private TileBase fogOfWarOverlayTile;
    [SerializeField] private TerrainDatabase fogOfWarTerrainDatabase;
    [SerializeField] private DPQAirHeightConfig fogOfWarDpqAirHeightConfig;
    [SerializeField, HideInInspector] [Range(0f, 1f)] private float fogOfWarAlpha = 0.65f;
    [SerializeField] private FogOfWarVisionMode fogOfWarVisionMode = FogOfWarVisionMode.All;
    [Header("Victory Overlay")]
    [SerializeField] private bool showVictoryOverlay = true;
    [SerializeField] private Tilemap victoryOverlayTilemap;
    [SerializeField] private TileBase victoryOverlayTile;
    [SerializeField] [Range(0f, 1f)] private float victoryOverlayAlpha = 1f;
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
    [System.NonSerialized] private readonly HashSet<Vector3Int> fogDisplayVisibleCellsBuffer = new HashSet<Vector3Int>();
    [System.NonSerialized] private readonly Dictionary<int, FogOfWarUnitCacheEntry> fogVisibleCellsByUnit = new Dictionary<int, FogOfWarUnitCacheEntry>();
    [System.NonSerialized] private readonly Dictionary<Vector3Int, int> fogVisibleContributorsByCell = new Dictionary<Vector3Int, int>();
    [System.NonSerialized] private readonly Dictionary<int, bool> fogUnitVisibilityByCacheIndex = new Dictionary<int, bool>();
    [System.NonSerialized] private readonly HashSet<Vector3Int> fogUnitVisibleScratchBuffer = new HashSet<Vector3Int>();
    [System.NonSerialized] private PanelRemainingController fogVisionPanelRemaining;
    [System.NonSerialized] private bool fogSortingLayerValidated;
    [System.NonSerialized] private int fogCachedTeamId = int.MinValue;
    [System.NonSerialized] private bool fogOverlayInitialized;
    [System.NonSerialized] private bool initialStealthDetectionBootstrapped;
    [System.NonSerialized] private bool debugFogOfWarEnabled = true;
    [System.NonSerialized] private bool debugFogOfWarPartial;
    [System.NonSerialized] private int fogPresentationGameplayTeamId = int.MinValue;
    [System.NonSerialized] private float runtimeConstructionIncomeRefreshTimer;
    [System.NonSerialized] private readonly HashSet<Vector3Int> victoryOverlayActiveCells = new HashSet<Vector3Int>();
    [System.NonSerialized] private int cachedVictoryOverlaySignature;
    [System.NonSerialized] private int cachedVictoryOverlayCount;
    [System.NonSerialized] private int cachedVictoryOverlaySettingsSignature;
    [System.NonSerialized] private Tilemap lastVictoryOverlayTilemap;
#if UNITY_EDITOR
    [System.NonSerialized] private bool pendingVictoryOverlayRefreshInEditor;
    [System.NonSerialized] private bool pendingFogOfWarClearInEditor;
#endif
    [Header("Debug")]
    [SerializeField] private bool enableFogSourceDebugLogs = false;
    [SerializeField] private bool enableFogStepPerfLogs = false;
    [SerializeField] private bool enableFogValidationLogs = false;
    [SerializeField] private bool enableSensorsRuntimeLogs = false;
    [SerializeField] private bool enablePodeMirarSensorLogs = false;
    [SerializeField] private bool enablePodeEmbarcarSensorLogs = false;
    [SerializeField] private bool enablePodeDesembarcarSensorLogs = false;
    [SerializeField] private bool enablePodeCapturarSensorLogs = false;
    [SerializeField] private bool enablePodeFundirSensorLogs = false;
    [SerializeField] private bool enablePodeSuprirSensorLogs = false;
    [SerializeField] private bool enablePodeTransferirSensorLogs = false;
    [SerializeField] private bool enableServicoDoComandoSensorLogs = false;
    [SerializeField] private bool enablePodePousarSensorLogs = false;
    [SerializeField] private bool enablePodeDecolarSensorLogs = false;
    [SerializeField] private bool enablePodeEmergirSensorLogs = false;
    [SerializeField] private bool enableAindaMeVeRuntimeLogs = false;
    [SerializeField] private bool enablePodeDetectarRuntimeLogs = false;
    [SerializeField] private bool enablePodeEnxergarRuntimeLogs = false;
    [SerializeField] private bool enableTurnPerfLogs = true;
    [SerializeField] [Range(1, 8)] private int fogStepPerfTopUnits = 3;
    public bool SuppressFogOfWarRefresh { get; set; } = false;

    private bool ShouldLogPodeEnxergarRuntime => enableFogSourceDebugLogs || enablePodeEnxergarRuntimeLogs;
    private bool ShouldLogAindaMeVeRuntime => enableSensorsRuntimeLogs || enableAindaMeVeRuntimeLogs;
    private bool ShouldLogPodeDetectarRuntime => enableSensorsRuntimeLogs || enablePodeDetectarRuntimeLogs;

    private static double TurnPerfNowMs()
    {
        return Time.realtimeSinceStartupAsDouble * 1000d;
    }

    private void TurnPerfLog(string stage, double startMs)
    {
        if (!enableTurnPerfLogs)
            return;

        double elapsed = TurnPerfNowMs() - startMs;
        Debug.Log($"[TurnPerf] etapa={stage} ms={elapsed:F3}");
    }

    public int CurrentTurn => currentTurn;
    public int ActiveTeamId => activeTeamId;
    public bool AtalhoRPassarTurnoUsaConfirmacao => atalhoRPassarTurnoUsaConfirmacao;
    public float LegacyFogOfWarAlpha => Mathf.Clamp01(fogOfWarAlpha);
    public int VisualContrastActiveTeamId => fogPresentationGameplayTeamId != int.MinValue
        ? fogPresentationGameplayTeamId
        : activeTeamId;
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

    // Retorna o TeamId do slot indicado. slotIndex -1 = Neutral. Fora do range = Neutral.
    public TeamId GetTeamIdForSlot(int slotIndex)
    {
        if (slotIndex < 0 || players == null || slotIndex >= players.Count)
            return TeamId.Neutral;
        return players[slotIndex].teamId;
    }

    public bool TryGetFirstAITeam(out TeamId team)
    {
        team = TeamId.Neutral;
        if (players == null)
            return false;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].isAI)
                continue;
            team = players[i].teamId;
            return team != TeamId.Neutral;
        }
        return false;
    }

    public bool TryGetFirstHumanTeam(out TeamId team)
    {
        team = TeamId.Neutral;
        if (players == null)
            return false;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry player = players[i];
            if (player.isAI || player.defeated || player.teamId == TeamId.Neutral)
                continue;

            team = player.teamId;
            return true;
        }

        return false;
    }

    public int GetSlotIndexForTeam(TeamId teamId)
    {
        if (teamId == TeamId.Neutral || players == null)
            return -1;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].teamId == teamId)
                return i;
        }

        return -1;
    }

    // Retorna quantos slots de jogador existem (excluindo Neutral).
    public int SlotCount => players != null ? players.Count : 0;
    public bool EconomyEnabled => economyEnabled;
    public GameSetupPreset GameSetup => gameSetup;
    /// <summary>
    /// Getter: true se pelo menos um jogador tem Command Service Automatic ativo.
    /// Setter: aplica o valor a todos os slots de jogador (usado pelo PartidaConfig).
    /// Para controle por time, use IsPlayerCommandServiceAutomatic / SetPlayerCommandServiceAutomatic.
    /// </summary>
    public bool CommandServiceAutomatic
    {
        get
        {
            if (players == null) return false;
            foreach (var p in players)
                if (p.commandServiceAutomatic) return true;
            return false;
        }
        set
        {
            if (players == null) return;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerEntry e = players[i];
                e.commandServiceAutomatic = value;
                players[i] = e;
            }
        }
    }

    public bool IsPlayerCommandServiceAutomatic(TeamId teamId)
    {
        if (players == null) return false;
        for (int i = 0; i < players.Count; i++)
            if (players[i].teamId == teamId)
                return players[i].commandServiceAutomatic;
        return false;
    }

    public void SetPlayerCommandServiceAutomatic(TeamId teamId, bool value)
    {
        if (players == null) return;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].teamId != teamId) continue;
            PlayerEntry e = players[i];
            e.commandServiceAutomatic = value;
            players[i] = e;
            return;
        }
    }
    public bool EnableLdtValidation => enableLdtValidation;
    public bool EnableLosValidation => enableLosValidation;
    public bool EnableSpotter => enableSpotter;
    public bool EnableStealthValidation => enableStealthValidation;
    public bool EnableTotalWar => enableTotalWar;
    public bool AtalhoContextual => atalhoContextual;
    // Tutorial desliga o atalho contextual no inicio da aula (o jogador pode religar
    // nas preferencias) — clique inferindo acao driblaria as travas do roteiro.
    public void SetAtalhoContextual(bool value) => atalhoContextual = value;
    public bool EnableSensorsRuntimeLogs => enableSensorsRuntimeLogs;
    public bool EnablePodeMirarSensorLogs => enableSensorsRuntimeLogs || enablePodeMirarSensorLogs;
    public bool EnablePodeEmbarcarSensorLogs => enableSensorsRuntimeLogs || enablePodeEmbarcarSensorLogs;
    public bool EnablePodeDesembarcarSensorLogs => enableSensorsRuntimeLogs || enablePodeDesembarcarSensorLogs;
    public bool EnablePodeCapturarSensorLogs => enableSensorsRuntimeLogs || enablePodeCapturarSensorLogs;
    public bool EnablePodeFundirSensorLogs => enableSensorsRuntimeLogs || enablePodeFundirSensorLogs;
    public bool EnablePodeSuprirSensorLogs => enableSensorsRuntimeLogs || enablePodeSuprirSensorLogs;
    public bool EnablePodeTransferirSensorLogs => enableSensorsRuntimeLogs || enablePodeTransferirSensorLogs;
    public bool EnableServicoDoComandoSensorLogs => enableSensorsRuntimeLogs || enableServicoDoComandoSensorLogs;
    public bool EnablePodePousarSensorLogs => enableSensorsRuntimeLogs || enablePodePousarSensorLogs;
    public bool EnablePodeDecolarSensorLogs => enableSensorsRuntimeLogs || enablePodeDecolarSensorLogs;
    public bool EnablePodeEmergirSensorLogs => enableSensorsRuntimeLogs || enablePodeEmergirSensorLogs;
    public TutorialData ActiveTutorial => activeTutorial;
    public bool IsTutorialMode => activeTutorial != null;
    public TerrainDatabase TerrainDatabaseRef => ResolveFogTerrainDatabase();
    public bool IsFogOfWarDebugEnabled => debugFogOfWarEnabled;
    public bool IsFogOfWarDebugPartial => debugFogOfWarPartial;
    public FogOfWarVisionMode FogOfWarVisionMode => fogOfWarVisionMode;
    public int MaxUnitsPerTeam => Mathf.Max(1, maxUnitsPerTeam);
    public AutonomyDatabase AutonomyDatabase => autonomyDatabase;
    public int ActivePlayerListIndex => activePlayerListIndex;
    public bool IsTurnTransitionInProgress => advanceTurnTransitionRoutine != null;
    public bool EnableVictoryStars => enableVictoryStars;
    public int VictoryStarsToWin => ClampVictoryStarsGoal(victoryStarsToWin);
    public bool HasVictoryWinner => hasVictoryWinner;
    public TeamId VictoryWinnerTeam => victoryWinnerTeam;
    private Coroutine advanceTurnTransitionRoutine;

    public int GetVictoryStars(TeamId team)
    {
        int index = FindVictoryEntryIndex(team);
        if (index < 0)
            return 0;

        return Mathf.Max(0, victoryStarsByTeam[index].stars);
    }

    public void GetVictoryControlForTeam(TeamId team, out int controlled, out int total)
    {
        controlled = 0;
        total = 0;

        if (team == TeamId.Neutral)
            return;
        if (FindPlayerIndexByTeam(team) < 0)
            return;

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if (!construction.IsVictoryBuilding)
                continue;

            total++;
            if (construction.TeamId == team)
                controlled++;
        }
    }

    public int GetProjectedVictoryStarsGain(TeamId team)
    {
        if (!enableVictoryStars)
            return 0;
        if (team == TeamId.Neutral)
            return 0;
        if (FindPlayerIndexByTeam(team) < 0)
            return 0;

        GetVictoryControlForTeam(team, out int controlled, out int total);
        if (total <= 0)
            return 0;

        int majorityThreshold = (total / 2) + 1;
        return controlled >= majorityThreshold ? 1 : 0;
    }

    public int GetActualMoney(TeamId team)
    {
        int playerIndex = FindPlayerEconomyIndex(team);
        if (playerIndex < 0)
            return 0;

        return Mathf.Max(0, players[playerIndex].actualMoney);
    }

    public int GetStartMoney(TeamId team)
    {
        int playerIndex = FindPlayerEconomyIndex(team);
        if (playerIndex < 0)
            return 0;

        return Mathf.Max(0, players[playerIndex].startMoney);
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
        List<bool> isAIs,
        List<int> startMoneys,
        List<int> actualMoneys,
        List<int> incomePerTurns,
        List<bool> startMoneyAppliedFlags)
    {
        if (teamIds == null || flipXs == null || isAIs == null || startMoneys == null || actualMoneys == null || incomePerTurns == null || startMoneyAppliedFlags == null)
            return;

        teamIds.Clear();
        flipXs.Clear();
        isAIs.Clear();
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
            isAIs.Add(entry.isAI);
            startMoneys.Add(Mathf.Max(0, entry.startMoney));
            actualMoneys.Add(Mathf.Max(0, entry.actualMoney));
            incomePerTurns.Add(Mathf.Max(0, entry.incomePerTurn));
            startMoneyAppliedFlags.Add(entry.startMoneyApplied);
        }
    }

    public void ImportPlayersState(
        IList<int> teamIds,
        IList<bool> flipXs,
        IList<bool> isAIs,
        IList<int> startMoneys,
        IList<int> actualMoneys,
        IList<int> incomePerTurns,
        IList<bool> startMoneyAppliedFlags,
        bool includeNeutral)
    {
        includeNeutralTeam = includeNeutral;
        // O override de flipX pertence ao slot logico da cena (como a economia):
        // preserva o valor autorado ao reconstruir a lista.
        List<FlipXOverrideMode> previousOverrides = new List<FlipXOverrideMode>();
        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
                previousOverrides.Add(players[i].flipXOverride);
        }

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
                flipXOverride = i < previousOverrides.Count ? previousOverrides[i] : FlipXOverrideMode.Auto,
                isAI = GetValueOrDefault(isAIs, i, false),
                startMoney = Mathf.Max(0, GetValueOrDefault(startMoneys, i, 0)),
                actualMoney = Mathf.Max(0, GetValueOrDefault(actualMoneys, i, 0)),
                incomePerTurn = Mathf.Max(0, GetValueOrDefault(incomePerTurns, i, 0)),
                startMoneyApplied = GetValueOrDefault(startMoneyAppliedFlags, i, false)
            };
            players.Add(entry);
        }

        NormalizePlayersList();
        NormalizeVictoryStars();
        SyncActivePlayerIndexFromActiveTeam();
        ApplyTeamFlipSettingsToSceneObjects();
    }

    public void ExportVictoryStarsState(
        List<int> teamIds,
        List<int> stars,
        out bool enabled,
        out int starsToWin,
        out bool winnerDefined,
        out int winnerTeamId)
    {
        enabled = enableVictoryStars;
        starsToWin = ClampVictoryStarsGoal(victoryStarsToWin);
        winnerDefined = hasVictoryWinner;
        winnerTeamId = (int)victoryWinnerTeam;

        if (teamIds == null || stars == null)
            return;

        teamIds.Clear();
        stars.Clear();
        for (int i = 0; i < victoryStarsByTeam.Count; i++)
        {
            TeamVictoryEntry entry = victoryStarsByTeam[i];
            if (entry.teamId == TeamId.Neutral)
                continue;

            teamIds.Add((int)entry.teamId);
            stars.Add(Mathf.Max(0, entry.stars));
        }
    }

    public void ImportVictoryStarsState(
        IList<int> teamIds,
        IList<int> stars,
        bool enabled,
        int starsToWin,
        bool winnerDefined,
        int winnerTeamId)
    {
        enableVictoryStars = enabled;
        victoryStarsToWin = ClampVictoryStarsGoal(starsToWin);
        hasVictoryWinner = winnerDefined;
        victoryWinnerTeam = ClampToTeamId(winnerTeamId);

        victoryStarsByTeam.Clear();
        int count = teamIds != null ? teamIds.Count : 0;
        for (int i = 0; i < count; i++)
        {
            TeamId team = ClampToTeamId(teamIds[i]);
            if (team == TeamId.Neutral)
                continue;

            int value = stars != null && i < stars.Count ? Mathf.Max(0, stars[i]) : 0;
            victoryStarsByTeam.Add(new TeamVictoryEntry { teamId = team, stars = value });
        }

        NormalizeVictoryStars();
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

    public bool GetSlotFlipX(int slotIndex)
    {
        if (players == null || slotIndex < 0 || slotIndex >= players.Count)
            return false;

        return players[slotIndex].flipX;
    }

    private void OnEnable()
    {
        TurnStateManager.OnUnitDestroyed += HandleUnitDestroyed;
    }

    private void OnDisable()
    {
        TurnStateManager.OnUnitDestroyed -= HandleUnitDestroyed;
    }

    private void Awake()
    {
        if (PartidaConfig.HasPending)
        {
            PartidaConfig.Apply(this);
            PartidaConfig.Clear();
        }
        if (PartidaConfig.TryConsumeTutorialPlayerTeam(out TeamId tutorialPlayerTeam))
            ApplyTutorialPlayerTeamChoice(tutorialPlayerTeam);
        ApplyGameSetupPreset();
        SyncThreatRevisionFlags();
        NormalizeState();
        TryRefreshIncomeFromConstructions(markDirtyInEditor: false);
        TryAutoAssignCursorController();
        TryAutoAssignTurnStateManager();
        TryAutoAssignTurnTransitionReferences();
        TryAutoAssignVictoryOverlayReferences();
        TryRefreshVictoryOverlayFromConstructions(markDirtyInEditor: false);
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
        {
            FindAnyObjectByType<ReplayManager>()?.CleanupReplayArtifactsForMatchStart();
            RecomputeTeamFlips();
            ResetUnfundedStartMoneyFlagsForFreshMatch();
            ApplyActiveTeamIfChanged(force: true);
            // Hard-code de observacao: em partidas AI vs AI, mantenha a apresentacao
            // de debug equivalente ao comando `fow partial` para acompanharmos ambos
            // os times em suas proprias perspectivas.
            if (AreAllPlayerSlotsAI())
                SetFogOfWarDebugPartial();
            TryAutoAssignTurnTransitionReferences();
            matchMusicAudioManager?.PrepareForMatchStart(forceRestartPlayback: true);
            
            // Garante que o painel de fim de jogo comece oculto
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "Panel_endGame" && go.scene.name != null)
                {
                    go.SetActive(false);
                    break;
                }
            }
        }
        TryBootstrapInitialStealthDetection();
        RunTurnStartStillObservedForActiveTeamStealthUnits();
    }

    // Recolore o slot 0 (jogador) com a cor escolhida na Tela de Entrada para o tutorial.
    // Se outro slot ja usa a cor escolhida, os dois slots trocam de cor entre si.
    // Economia, flip e isAI pertencem ao slot logico e nao se movem (mesma regra do
    // PartidaConfig.Apply). So tem efeito visivel em cenas cujas unidades/construcoes
    // usam slotIndex (>= 0); conteudo com time fixo (slotIndex -1) nao acompanha.
    private void ApplyTutorialPlayerTeamChoice(TeamId chosenTeam)
    {
        if (chosenTeam == TeamId.Neutral)
            return;

        List<int> teamIds = new List<int>();
        List<bool> flipXs = new List<bool>();
        List<bool> isAIs = new List<bool>();
        List<int> startMoneys = new List<int>();
        List<int> actualMoneys = new List<int>();
        List<int> incomePerTurns = new List<int>();
        List<bool> startMoneyApplied = new List<bool>();
        ExportPlayersState(teamIds, flipXs, isAIs, startMoneys, actualMoneys, incomePerTurns, startMoneyApplied);

        if (teamIds.Count <= 0 || teamIds[0] == (int)chosenTeam)
            return;

        int previousSlot0Team = teamIds[0];
        for (int i = 1; i < teamIds.Count; i++)
        {
            if (teamIds[i] == (int)chosenTeam)
            {
                teamIds[i] = previousSlot0Team;
                break;
            }
        }
        teamIds[0] = (int)chosenTeam;

        ImportPlayersState(teamIds, flipXs, isAIs, startMoneys, actualMoneys, incomePerTurns, startMoneyApplied, false);
        SetActiveTeamIdWithoutTurnStart(teamIds[0]);
    }

    private bool AreAllPlayerSlotsAI()
    {
        if (players == null)
            return false;

        int participatingSlots = 0;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry player = players[i];
            if (player.teamId == TeamId.Neutral)
                continue;

            participatingSlots++;
            if (!player.isAI)
                return false;
        }

        return participatingSlots >= 2;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            SyncThreatRevisionFlags();
            // Flip editado DURANTE o Play (auto/override) precisa refletir na hora
            // nas unidades em campo — sem isso o early-return engolia a mudanca.
            RecomputeTeamFlips();
            return;
        }

        ApplyGameSetupPreset();
        SyncThreatRevisionFlags();
        NormalizeState();
        TryRefreshIncomeFromConstructions(markDirtyInEditor: true);
        TryAutoAssignCursorController();
        TryAutoAssignTurnStateManager();
        TryAutoAssignTurnTransitionReferences();
        TryAutoAssignVictoryOverlayReferences();
        ScheduleVictoryOverlayRefreshInEditor();
        if (enableTotalWar)
            TryAutoAssignFogOfWarReferences();
        ScheduleFogOfWarClearInEditor();
        RecomputeTeamFlips();
        ApplyActiveTeamIfChanged(force: false);
        ApplyTeamFlipSettingsToSceneObjects();
        OnSlotConfigChanged?.Invoke();
    }
#endif

#if UNITY_EDITOR
    private void ScheduleVictoryOverlayRefreshInEditor()
    {
        if (Application.isPlaying)
            return;

        if (pendingVictoryOverlayRefreshInEditor)
            return;

        pendingVictoryOverlayRefreshInEditor = true;
        EditorApplication.delayCall += ExecuteDelayedVictoryOverlayRefreshInEditor;
    }

    private void ExecuteDelayedVictoryOverlayRefreshInEditor()
    {
        if (this == null)
            return;

        pendingVictoryOverlayRefreshInEditor = false;
        if (Application.isPlaying)
            return;

        TryRefreshVictoryOverlayFromConstructions(markDirtyInEditor: true);
    }

    private void ScheduleFogOfWarClearInEditor()
    {
        if (Application.isPlaying)
            return;

        if (pendingFogOfWarClearInEditor)
            return;

        pendingFogOfWarClearInEditor = true;
        EditorApplication.delayCall += ExecuteDelayedFogOfWarClearInEditor;
    }

    private void ExecuteDelayedFogOfWarClearInEditor()
    {
        if (this == null)
            return;

        pendingFogOfWarClearInEditor = false;
        if (Application.isPlaying || fogOfWarTilemap == null)
            return;

        fogOfWarTilemap.ClearAllTiles();
    }
#else
    private void ScheduleVictoryOverlayRefreshInEditor()
    {
    }

    private void ScheduleFogOfWarClearInEditor()
    {
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
                TryRefreshVictoryOverlayFromConstructions(markDirtyInEditor: false);
            }
        }
        else if (continuousEditorRefresh)
        {
            TryRefreshIncomeFromConstructions(markDirtyInEditor: true);
            ScheduleVictoryOverlayRefreshInEditor();
        }

        SyncThreatRevisionFlags();

        if (!Application.isPlaying)
            return;

        TryAutoAssignCursorController();
        TryAutoAssignTurnStateManager();
        TryAutoAssignTurnTransitionReferences();
        if (enableTotalWar)
            TryAutoAssignFogOfWarReferences();
        HandleFogOfWarVisionModeHotkey();
        ApplyActiveTeamIfChanged(force: false);
        EnsureFogOfWarRuntimeInitialized();
    }

    private void EnsureFogOfWarRuntimeInitialized()
    {
        if (!debugFogOfWarEnabled || !enableTotalWar || fogOfWarTilemap == null)
            return;
        if (activeTeamId < 0 && !includeNeutralTeam)
            return;
        int expectedVisualTeamId = activeTeamId;
        if (ShouldUseHumanFogPresentation(out TeamId presentationTeam))
            expectedVisualTeamId = (int)presentationTeam;
        if (fogCachedTeamId == expectedVisualTeamId && fogOverlayInitialized)
            return;

        // Domain reloads durante o Play Mode limpam o cache nao serializado, mas o
        // tilemap visual sobrevive. Reconstrua-o mesmo sem troca de time.
        RefreshFogOfWarForActiveTeam();
    }

    public void SetCurrentTurn(int turn)
    {
        currentTurn = Mathf.Max(0, turn);
    }

    public void CycleFogOfWarVisionMode()
    {
        SetFogOfWarVisionMode(GetNextAvailableFogOfWarVisionMode(fogOfWarVisionMode));
    }

    public void SetFogOfWarVisionMode(FogOfWarVisionMode mode)
    {
        if (!IsFogOfWarVisionModeAvailable(mode))
            mode = FogOfWarVisionMode.All;
        UpdateFogOfWarVisionModePanel(mode);
        if (fogOfWarVisionMode == mode)
            return;

        fogOfWarVisionMode = mode;
        if (Application.isPlaying && debugFogOfWarEnabled && enableTotalWar)
            RefreshFogOfWarForActiveTeam();
        Debug.Log($"[FogOfWar] VisionMode={fogOfWarVisionMode}");
    }

    private void HandleFogOfWarVisionModeHotkey()
    {
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return;
        if (UiInputBlocker.IsTextInputFocused())
            return;
        if (Input.GetKeyDown(KeyCode.L))
            CycleFogOfWarVisionMode();
    }

    private FogOfWarVisionMode GetNextAvailableFogOfWarVisionMode(FogOfWarVisionMode current)
    {
        FogOfWarVisionMode candidate = current;
        for (int i = 0; i < 4; i++)
        {
            candidate = GetNextFogOfWarVisionMode(candidate);
            if (IsFogOfWarVisionModeAvailable(candidate))
                return candidate;
        }

        return FogOfWarVisionMode.All;
    }

    private static FogOfWarVisionMode GetNextFogOfWarVisionMode(FogOfWarVisionMode mode)
    {
        return mode switch
        {
            FogOfWarVisionMode.All => FogOfWarVisionMode.Air,
            FogOfWarVisionMode.Air => FogOfWarVisionMode.Surface,
            FogOfWarVisionMode.Surface => FogOfWarVisionMode.Sub,
            _ => FogOfWarVisionMode.All
        };
    }

    private bool IsFogOfWarVisionModeAvailable(FogOfWarVisionMode mode)
    {
        if (mode == FogOfWarVisionMode.All)
            return true;

        bool hasAirport = MapHasConstructionFacility(requireAirport: true);
        bool hasHarbor = MapHasConstructionFacility(requireAirport: false);
        return mode switch
        {
            FogOfWarVisionMode.Air => hasAirport,
            FogOfWarVisionMode.Surface => hasAirport || hasHarbor,
            FogOfWarVisionMode.Sub => hasHarbor,
            _ => true
        };
    }

    private bool MapHasConstructionFacility(bool requireAirport)
    {
        List<ConstructionManager> constructions = ConstructionManager.AllActive;
        for (int i = constructions.Count - 1; i >= 0; i--)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
            {
                constructions.RemoveAt(i);
                continue;
            }

            if (!construction.gameObject.activeInHierarchy)
                continue;
            if (construction.gameObject.scene != gameObject.scene)
                continue;
            if (!construction.TryResolveConstructionData(out ConstructionData data) || data == null)
                continue;

            if (requireAirport ? data.isAirport : data.isHarbor)
                return true;
        }

        return false;
    }

    private void UpdateFogOfWarVisionModePanel(FogOfWarVisionMode mode)
    {
        PanelRemainingController panel = FindPanelRemainingController();
        if (panel != null)
            panel.SetFogOfWarVisionMode(mode);
    }

    private PanelRemainingController FindPanelRemainingController()
    {
        if (fogVisionPanelRemaining != null && fogVisionPanelRemaining.gameObject.activeInHierarchy)
            return fogVisionPanelRemaining;

        GameObject panelObject = GameObject.Find("Panel_remaining");
        if (panelObject != null)
            fogVisionPanelRemaining = panelObject.GetComponent<PanelRemainingController>()
                ?? panelObject.GetComponentInChildren<PanelRemainingController>(true);

        if (fogVisionPanelRemaining == null)
            fogVisionPanelRemaining = FindAnyObjectByType<PanelRemainingController>();

        return fogVisionPanelRemaining;
    }

    public void SetGameSetupPreset(GameSetupPreset preset)
    {
        gameSetup = preset;
        ApplyGameSetupPreset();
        if (gameSetup == GameSetupPreset.FogOfWarTotal)
        {
            // Hard guard: the Total FoW preset must always start with FoW enabled.
            fogOfWar = true;
            debugFogOfWarEnabled = true;
        }
        else
        {
            fogOfWar = false;
            ResetFogOfWarRuntime(clearTilemap: true);
            ShowAllUnitsIgnoringFog();

            ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < constructions.Length; i++)
                constructions[i]?.RefreshRuntimeVisualState(force: true);
        }
        fogSortingLayerValidated = false;
        TryAutoAssignFogOfWarReferences();
        ValidateFogOfWarSortingLayer();
        SyncThreatRevisionFlags();
    }

    public void SetPlayerIsAI(TeamId teamId, bool isAI)
    {
        if (players == null)
            return;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].teamId == teamId)
            {
                PlayerEntry e = players[i];
                e.isAI = isAI;
                players[i] = e;
                return;
            }
        }
    }

    public bool IsPlayerAI(TeamId teamId)
    {
        if (players == null)
            return false;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].teamId == teamId)
                return players[i].isAI;
        }
        return false;
    }

    // Verifica se o time ATUALMENTE ativo e IA, usando o slot index diretamente.
    // Mais robusto que IsPlayerAI(TeamId) pois nao depende de lookup por TeamId.
    public bool IsActiveTeamAI()
    {
        if (players == null || activePlayerListIndex < 0 || activePlayerListIndex >= players.Count)
            return false;
        return players[activePlayerListIndex].isAI;
    }

    // Bloqueio central de input humano durante o turno de um time controlado por IA.
    public bool IsPlayerInputLockedByActiveAI()
    {
        return Application.isPlaying && IsActiveTeamAI() && !AIController.IsDebugPaused;
    }

    public bool IsTeamDefeated(TeamId team)
    {
        int index = FindPlayerEconomyIndex(team);
        if (index < 0 || players == null || index >= players.Count)
            return false;
        return players[index].defeated;
    }

    public void SetActiveTeamId(int teamId)
    {
        activeTeamId = Mathf.Clamp(teamId, -1, 3);
        SyncActivePlayerIndexFromActiveTeam();
        ApplyActiveTeamIfChanged(force: false);
    }

    // Usado apos load: garante que OnActiveTeamChanged dispare mesmo que o time ativo seja o mesmo de antes.
    // Usa applyTurnStartEffects: false para nao reprocessar economia/upkeep que ja foram restaurados do save.
    public void ForceReapplyActiveTeam()
    {
        appliedActiveTeamId = int.MinValue;
        ApplyActiveTeamIfChanged(force: false, applyTurnStartEffects: false);
    }

    // Versao para load: aplica efeitos de inicio de turno.
    public void ForceReapplyActiveTeamWithTurnStart()
    {
        appliedActiveTeamId = int.MinValue;
        ApplyActiveTeamIfChanged(force: false, applyTurnStartEffects: true);
    }

    // Debug: troca o time ativo sem aplicar efeitos de inicio de turno
    // (economia/upkeep/reset acted), mas atualiza musica/FoW/cursor/UI.
    public void SetActiveTeamIdWithoutTurnStart(int teamId)
    {
        activeTeamId = Mathf.Clamp(teamId, -1, 3);
        SyncActivePlayerIndexFromActiveTeam();
        ApplyActiveTeamIfChanged(force: false, applyTurnStartEffects: false);
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
            int aliveNext = FindNextAlivePlayerIndex(activePlayerListIndex);
            if (aliveNext >= 0)
            {
                bool wrapped = aliveNext <= activePlayerListIndex;
                if (wrapped && includeNeutralTeam && HasAnyNeutralUnitsInField())
                {
                    SetNeutralActiveTeam();
                    return;
                }

                SetActivePlayerByIndex(aliveNext);
                return;
            }

            if (includeNeutralTeam && HasAnyNeutralUnitsInField())
            {
                SetNeutralActiveTeam();
                return;
            }

            int firstAlive = FindNextAlivePlayerIndex(-1);
            if (firstAlive >= 0)
                SetActivePlayerByIndex(firstAlive, forceApply: true);
            return;
        }

        int nextAliveFromNeutral = FindNextAlivePlayerIndex(-1);
        if (nextAliveFromNeutral >= 0)
            SetActivePlayerByIndex(nextAliveFromNeutral, forceApply: true);
    }

    // Avanca para o proximo membro da lista. So incrementa currentTurn ao "fechar ciclo".
    public void AdvanceTurn()
    {
        if (freezeTurnAdvanceAfterVictory && hasVictoryWinner)
            return;
        if (Application.isPlaying)
            OnBeforeAdvanceTurn?.Invoke();
        if (players.Count == 0)
        {
            if (includeNeutralTeam && HasAnyNeutralUnitsInField())
            {
                pendingTurnStartUpkeep = true;
                pendingTurnStartEconomy = true;
                SetNeutralActiveTeam();
            }
            CloseRoundAndAdvanceToFirstPlayer();
            return;
        }

        if (CountAlivePlayers() <= 1)
        {
            TryDeclareLastStandingWinner();
            return;
        }

        // Caso padrao: estamos em um player da lista.
        if (activePlayerListIndex >= 0)
        {
            int nextAlive = FindNextAlivePlayerIndex(activePlayerListIndex);
            if (nextAlive >= 0)
            {
                pendingTurnStartUpkeep = true;
                pendingTurnStartEconomy = true;
                bool wrapped = nextAlive <= activePlayerListIndex;
                if (wrapped && includeNeutralTeam && HasAnyNeutralUnitsInField())
                {
                    SetNeutralActiveTeam();
                    return;
                }
                if (wrapped)
                    CloseRoundAndAdvanceToFirstPlayer();
                SetActivePlayerByIndex(nextAlive, forceApply: wrapped);
                return;
            }

            // Saiu da lista: vai para neutral se estiver habilitado.
            if (includeNeutralTeam && HasAnyNeutralUnitsInField())
            {
                pendingTurnStartUpkeep = true;
                pendingTurnStartEconomy = true;
                SetNeutralActiveTeam();
                return;
            }

            // Sem neutral: tenta declarar vencedor por ultimo sobrevivente.
            TryDeclareLastStandingWinner();
            return;
        }

        // Estavamos em neutral (ou fora da lista): fecha ciclo de turno e volta para o primeiro player.
        int firstAlivePlayer = FindNextAlivePlayerIndex(-1);
        if (firstAlivePlayer >= 0)
        {
            CloseRoundAndAdvanceToFirstPlayer();
            pendingTurnStartUpkeep = true;
            pendingTurnStartEconomy = true;
            SetActivePlayerByIndex(firstAlivePlayer, forceApply: true);
            return;
        }

        TryDeclareLastStandingWinner();
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
        double transitionStartMs = TurnPerfNowMs();
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
        float preDelay = turnStateManager != null ? turnStateManager.AdvanceTurnPreDelay : advanceTurnPreDelay;
        if (preDelay > 0f)
            yield return new WaitForSeconds(preDelay);

        double advanceTurnStartMs = TurnPerfNowMs();
        AdvanceTurn();
        TurnPerfLog("AdvanceTurn", advanceTurnStartMs);

        float postDelay = turnStateManager != null ? turnStateManager.AdvanceTurnPostDelay : advanceTurnPostDelay;
        if (postDelay > 0f)
            yield return new WaitForSeconds(postDelay);

        if (hasVictoryWinner && matchMusicAudioManager != null)
        {
            matchMusicAudioManager.StopForTurnTransition();
            matchMusicAudioManager.EndTurnTransition();
        }
        else if (wasMusicPlaying && !wasPausedByUser && matchMusicAudioManager != null)
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
        TurnPerfLog("AdvanceTurnTransitionRoutine.Total", transitionStartMs);
    }

    private void CloseRoundAndAdvanceToFirstPlayer()
    {
        currentTurn = Mathf.Max(0, currentTurn + 1);
    }

    private void EvaluateVictoryStarsAtTurnStartForActiveTeam(List<ConstructionManager> constructions = null)
    {
        if (!enableVictoryStars)
            return;
        if (hasVictoryWinner)
            return;
        if (players == null || players.Count <= 0 || activeTeamId < 0)
            return;

        NormalizeVictoryStars();
        if (victoryStarsByTeam == null || victoryStarsByTeam.Count <= 0)
            return;

        TeamId activeTeam = ClampToTeamId(activeTeamId);
        if (activeTeam == TeamId.Neutral)
            return;
        if (FindPlayerIndexByTeam(activeTeam) < 0)
            return;

        constructions ??= GetActiveConstructionsOnScene();
        int totalVictoryBuildings = 0;
        int activeTeamControlledVictoryBuildings = 0;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if (!construction.IsVictoryBuilding)
                continue;

            totalVictoryBuildings++;
            if (construction.TeamId == activeTeam)
                activeTeamControlledVictoryBuildings++;
        }

        if (totalVictoryBuildings <= 0)
            return;

        int majorityThreshold = (totalVictoryBuildings / 2) + 1;
        if (activeTeamControlledVictoryBuildings < majorityThreshold)
            return;

        int winnerEntryIndex = FindVictoryEntryIndex(activeTeam);
        if (winnerEntryIndex < 0)
            return;

        TeamVictoryEntry winnerEntry = victoryStarsByTeam[winnerEntryIndex];
        winnerEntry.stars = Mathf.Max(0, winnerEntry.stars + 1);
        victoryStarsByTeam[winnerEntryIndex] = winnerEntry;

        int goal = ClampVictoryStarsGoal(victoryStarsToWin);
        Debug.Log($"[VictoryStars] +1 estrela para {TeamUtils.GetName(activeTeam)} ({winnerEntry.stars}/{goal}) | dominio {activeTeamControlledVictoryBuildings}/{totalVictoryBuildings}.");
        if (winnerEntry.stars < goal)
            return;

        hasVictoryWinner = true;
        victoryWinnerTeam = activeTeam;
        HandleVictoryAestheticPresentation(activeTeam, TeamId.Neutral, VictoryReason.VictoryStars);
    }

    public void DeclareTutorialVictory(TutorialData tutorial = null)
    {
        if (hasVictoryWinner) return;

        TeamId winnerTeam = GetTeamIdForSlot(0);
        hasVictoryWinner = true;
        victoryWinnerTeam = winnerTeam;

        Debug.Log($"[Victory] Tutorial concluido: vitoria do {TeamUtils.GetName(winnerTeam)}.");

        // Parar musica permanentemente
        if (matchMusicAudioManager != null)
            matchMusicAudioManager.StopPlaybackPermanently();

        // Tocar victory SFX
        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null)
            cursor.PlayVictorySfx();

        // Mesmo Panel_vitoria da partida normal, com motivo do tutorial
        // (customizavel pelo victoryDialog.message do TutorialData).
        string motivo = (tutorial != null && tutorial.victoryDialog != null && !string.IsNullOrWhiteSpace(tutorial.victoryDialog.message))
            ? tutorial.victoryDialog.message
            : "TREINAMENTO CONCLUÍDO";
        string descricao = $"TIME {ColorizeTeamName(winnerTeam)} — {motivo}";
        ShowVictoryPanel("VITÓRIA!", TeamUtils.GetColor(winnerTeam), descricao);
    }

    private void DeclareDefeat()
    {
        if (hasVictoryWinner) return;

        hasVictoryWinner = true;
        
        // Parar musica permanentemente
        if (matchMusicAudioManager != null)
        {
            matchMusicAudioManager.StopPlaybackPermanently(); 
        }
        
        // Tocar defeat SFX
        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null)
        {
            cursor.PlayDefeatSfx();
        }

        PanelDialogController.TrySetExternalText("DERROTA! VOCÊ FICOU SEM UNIDADES.");

        // Busca o painel
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "Panel_endGame" && go.scene.name != null)
            {
                go.SetActive(true);
                // Busca todos os textos para atualizar titulo e descricao
                var texts = go.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var txt in texts)
                {
                    if (txt.name == "text_endgame")
                    {
                        txt.text = "DERROTA!";
                    }
                    else if (txt.name == "text_descrição" || txt.name == "text_description")
                    {
                        txt.text = "Ficou sem unidades";
                    }
                }
                break;
            }
        }
    }

    private int CountAlivePlayers()
    {
        if (players == null || players.Count == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].defeated)
                count++;
        }
        return count;
    }

    private int FindNextAlivePlayerIndex(int fromIndex)
    {
        if (players == null || players.Count == 0)
            return -1;

        int count = players.Count;
        int start = Mathf.Clamp(fromIndex + 1, 0, count);
        for (int step = 0; step < count; step++)
        {
            int idx = (start + step) % count;
            if (!players[idx].defeated)
                return idx;
        }
        return -1;
    }

    private bool TryDefeatTeamIfZeroUnits(TeamId team)
    {
        int playerIndex = FindPlayerEconomyIndex(team);
        if (playerIndex < 0 || players == null || playerIndex >= players.Count)
            return false;
        if (players[playerIndex].defeated)
            return false;

        List<UnitManager> allUnits = GetActiveUnitsOnScene();
        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitManager unit = allUnits[i];
            if (unit != null && unit.TeamId == team)
                return false;
        }

        PlayerEntry entry = players[playerIndex];
        entry.defeated = true;
        players[playerIndex] = entry;

        NeutralizeConstructionsOwnedByTeam(team);
        Debug.Log($"[Match] Team {TeamUtils.GetName(team)} derrotado (0 unidades). Construcoes neutralizadas.");
        OnTeamDefeated?.Invoke(team);
        return true;
    }

    // Chamado do ponto unico de conclusao de captura (ExecuteCaptureSequence), valido tanto para o
    // jogador humano quanto para a IA (que captura pelo mesmo caminho via Automation ->
    // HandleCaptureActionRequested). Se o alvo for um QG, o antigo dono e eliminado na hora e o
    // capturador vence imediatamente (mesmo com outros jogadores/IA ainda em jogo).
    public void NotifyConstructionCaptured(ConstructionManager construction, TeamId previousOwner, TeamId newOwner)
    {
        if (!Application.isPlaying || hasVictoryWinner)
            return;
        if (!allowDefeatForHeadQuarterCapture)
            return;
        if (construction == null || !IsHeadQuarterConstruction(construction))
            return;
        if (previousOwner == TeamId.Neutral || previousOwner == newOwner)
            return;

        if (!MarkTeamDefeated(previousOwner, "QG capturado"))
            return;

        Debug.Log($"[Match] QG de {TeamUtils.GetName(previousOwner)} capturado por {TeamUtils.GetName(newOwner)}. Time eliminado.");
        // O primeiro a capturar um QG vence na hora, mesmo com outros jogadores em jogo.
        DeclareEliminationVictory(newOwner, previousOwner, VictoryReason.HeadQuarterCaptured);
    }

    // Marca um time como derrotado por qualquer condicao (QG capturado, rendicao, etc.): neutraliza
    // suas construcoes e dispara OnTeamDefeated. A checagem de 0 unidades tem seu proprio caminho
    // (TryDefeatTeamIfZeroUnits) por causa do pre-requisito de contagem de unidades.
    private bool MarkTeamDefeated(TeamId team, string reasonLabel)
    {
        int playerIndex = FindPlayerEconomyIndex(team);
        if (playerIndex < 0 || players == null || playerIndex >= players.Count)
            return false;
        if (players[playerIndex].defeated)
            return false;

        PlayerEntry entry = players[playerIndex];
        entry.defeated = true;
        players[playerIndex] = entry;

        NeutralizeConstructionsOwnedByTeam(team);
        Debug.Log($"[Match] Team {TeamUtils.GetName(team)} derrotado ({reasonLabel}). Construcoes neutralizadas.");
        OnTeamDefeated?.Invoke(team);
        return true;
    }

    private void NeutralizeConstructionsOwnedByTeam(TeamId defeatedTeam)
    {
        List<ConstructionManager> constructions = GetActiveConstructionsOnScene();
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || construction.TeamId != defeatedTeam)
                continue;

            construction.SetTeamId(TeamId.Neutral);
            construction.SetCurrentCapturePoints(construction.CapturePointsMax);
        }
    }

    private bool TryDeclareLastStandingWinner()
    {
        if (hasVictoryWinner)
            return true;

        int aliveCount = CountAlivePlayers();
        if (aliveCount != 1)
            return false;

        TeamId winner = TeamId.Neutral;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].defeated)
            {
                winner = players[i].teamId;
                break;
            }
        }
        if (winner == TeamId.Neutral)
            return false;

        hasVictoryWinner = true;
        victoryWinnerTeam = winner;
        HandleVictoryAestheticPresentation(winner, TeamId.Neutral, VictoryReason.ArmyEliminated);
        return true;
    }

    // Regra: o PRIMEIRO a eliminar um jogador (capturando o QG ou destruindo o exercito por
    // inteiro) vence na hora, mesmo que ainda restem outros jogadores/IA. O vencedor e o time
    // que executou a eliminacao.
    private bool DeclareEliminationVictory(TeamId winnerTeam, TeamId defeatedTeam, VictoryReason reason)
    {
        if (hasVictoryWinner)
            return true;
        if (winnerTeam == TeamId.Neutral || winnerTeam == defeatedTeam)
            return false;

        hasVictoryWinner = true;
        victoryWinnerTeam = winnerTeam;
        HandleVictoryAestheticPresentation(winnerTeam, defeatedTeam, reason);
        return true;
    }

    // Atribui a eliminacao por 0 unidades a quem estava agindo (time ativo). Se nao der para
    // atribuir (ex.: o time comeca o turno ja sem unidades), cai no primeiro oponente vivo.
    private TeamId ResolveEliminatorTeamFor(TeamId defeatedTeam)
    {
        TeamId active = ClampToTeamId(activeTeamId);
        if (active != TeamId.Neutral && active != defeatedTeam && !IsTeamDefeated(active))
            return active;

        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].defeated && players[i].teamId != defeatedTeam && players[i].teamId != TeamId.Neutral)
                    return players[i].teamId;
            }
        }
        return TeamId.Neutral;
    }

    // Time controlado por humano (nao IA, nao neutro).
    private bool IsHumanTeam(TeamId team)
    {
        return team != TeamId.Neutral && !IsPlayerAI(team);
    }

    private bool AnyHumanPlayerExists()
    {
        if (players == null)
            return false;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].isAI && players[i].teamId != TeamId.Neutral)
                return true;
        }
        return false;
    }

    private void HandleUnitDestroyed(UnitManager unit)
    {
        if (!Application.isPlaying || hasVictoryWinner || currentTurn < 2)
            return;

        if (unit == null)
            return;
        if (!allowDefeatForZeroUnits)
            return;
        if (unit.TeamId == TeamId.Neutral)
            return;

        TeamId defeatedTeam = unit.TeamId;
        if (TryDefeatTeamIfZeroUnits(defeatedTeam))
        {
            // O primeiro a destruir por completo o exercito de um jogador vence na hora.
            DeclareEliminationVictory(ResolveEliminatorTeamFor(defeatedTeam), defeatedTeam, VictoryReason.ArmyEliminated);
        }
    }

    private void NormalizeState()
    {
        if (gameSetup == GameSetupPreset.FogOfWarTotal)
            fogOfWar = true;
        debugFogOfWarEnabled = fogOfWar;
        currentTurn = Mathf.Max(0, currentTurn);
        activeTeamId = Mathf.Clamp(activeTeamId, -1, 3);
        maxUnitsPerTeam = Mathf.Max(1, maxUnitsPerTeam);
        NormalizePlayersList();
        NormalizeVictoryStars();
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
            entry.defeated = entry.defeated && entry.teamId != TeamId.Neutral;
            players[i] = entry;
        }
    }

    private void ResetUnfundedStartMoneyFlagsForFreshMatch()
    {
        if (!Application.isPlaying || currentTurn > 1 || players == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry entry = players[i];
            if (!entry.startMoneyApplied)
                continue;
            if (entry.startMoney <= 0 || entry.actualMoney > 0)
                continue;

            entry.startMoneyApplied = false;
            players[i] = entry;
        }
    }

    private void NormalizeVictoryStars()
    {
        if (victoryStarsByTeam == null)
            victoryStarsByTeam = new List<TeamVictoryEntry>();

        victoryStarsToWin = ClampVictoryStarsGoal(victoryStarsToWin);

        for (int i = victoryStarsByTeam.Count - 1; i >= 0; i--)
        {
            TeamVictoryEntry entry = victoryStarsByTeam[i];
            if (entry.teamId == TeamId.Neutral || FindPlayerIndexByTeam(entry.teamId) < 0)
            {
                victoryStarsByTeam.RemoveAt(i);
                continue;
            }

            entry.stars = Mathf.Max(0, entry.stars);
            victoryStarsByTeam[i] = entry;
        }

        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                TeamId team = players[i].teamId;
                if (team == TeamId.Neutral)
                    continue;
                if (FindVictoryEntryIndex(team) >= 0)
                    continue;

                victoryStarsByTeam.Add(new TeamVictoryEntry
                {
                    teamId = team,
                    stars = 0
                });
            }
        }

        if (hasVictoryWinner)
        {
            if (victoryWinnerTeam == TeamId.Neutral || FindPlayerIndexByTeam(victoryWinnerTeam) < 0)
            {
                hasVictoryWinner = false;
                victoryWinnerTeam = TeamId.Neutral;
            }
        }
    }

    private int FindVictoryEntryIndex(TeamId team)
    {
        if (victoryStarsByTeam == null)
            return -1;

        for (int i = 0; i < victoryStarsByTeam.Count; i++)
        {
            if (victoryStarsByTeam[i].teamId == team)
                return i;
        }

        return -1;
    }

    private static int ClampVictoryStarsGoal(int value)
    {
        return Mathf.Clamp(value, 1, MaxVictoryStarsGoal);
    }

    private enum VictoryReason
    {
        HeadQuarterCaptured,
        ArmyEliminated,
        Surrender,
        VictoryStars
    }

    private void HandleVictoryAestheticPresentation(TeamId winnerTeam, TeamId defeatedTeam, VictoryReason reason)
    {
        Debug.Log($"[Victory] Vitoria de {TeamUtils.GetName(winnerTeam)} " +
                  $"({reason}{(defeatedTeam != TeamId.Neutral ? $" | derrotado: {TeamUtils.GetName(defeatedTeam)}" : string.Empty)}).");

        TryAutoAssignTurnTransitionReferences();
        if (matchMusicAudioManager != null)
            matchMusicAudioManager.StopPlaybackPermanently();

        // Resultado do ponto de vista do humano local: se o vencedor for humano -> VITORIA;
        // se quem venceu foi IA e existe humano na partida -> aquele humano perdeu (DERROTA).
        bool humanLost = !IsHumanTeam(winnerTeam) && AnyHumanPlayerExists();

        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (humanLost)
            cursor?.PlayDefeatSfx();
        else
            cursor?.PlayVictorySfx();

        Color winnerColor = TeamUtils.GetColor(winnerTeam);
        string coloredWinner = $"TIME {ColorizeTeamName(winnerTeam)}";

        // O motivo cita o time derrotado pintado com a cor dele (mesmo texto para vitoria/derrota;
        // o que muda e o titulo VITORIA!/DERROTA!).
        string descricao = coloredWinner;
        if (defeatedTeam != TeamId.Neutral)
        {
            string coloredLoser = ColorizeTeamName(defeatedTeam);
            string motivo;
            switch (reason)
            {
                case VictoryReason.HeadQuarterCaptured: motivo = $"QG {coloredLoser} CAPTURADO"; break;
                case VictoryReason.Surrender:           motivo = $"RENDIÇÃO DO TIME {coloredLoser}"; break;
                default:                                motivo = $"EXÉRCITO {coloredLoser} DERROTADO"; break;
            }
            descricao = $"{coloredWinner} — {motivo}";
        }
        else if (reason == VictoryReason.VictoryStars)
        {
            descricao = $"{coloredWinner} — PONTOS DE VITÓRIA";
        }

        // Titulo: cor do vencedor na vitoria; cor do time derrotado (o proprio humano) na derrota.
        string titulo = humanLost ? "DERROTA!" : "VITÓRIA!";
        Color tituloColor = humanLost
            ? (defeatedTeam != TeamId.Neutral ? TeamUtils.GetColor(defeatedTeam) : new Color(0.85f, 0.25f, 0.25f))
            : winnerColor;

        ShowVictoryPanel(titulo, tituloColor, descricao);
    }

    // Ativa o Panel_vitoria da cena (mesmo desativado) e preenche titulo/descricao.
    // Ponto unico usado pela vitoria de partida e pela vitoria de tutorial.
    private void ShowVictoryPanel(string titulo, Color tituloColor, string descricao)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "Panel_vitoria" && go.scene.IsValid())
            {
                go.SetActive(true);
                foreach (TMPro.TMP_Text t in go.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: true))
                {
                    if (t.name == "text_descricao")
                    {
                        t.richText = true;
                        t.text = descricao;
                    }
                    else if (t.name == "text_vitoria")
                    {
                        t.text = titulo;
                        t.color = tituloColor;
                    }
                }
                return;
            }
        }

        Debug.LogWarning("[Victory] Panel_vitoria nao encontrado na cena — a vitoria foi declarada sem painel visual.");
    }

    // Nome do time em maiusculas, pintado com a cor do time via rich text do TMP.
    private static string ColorizeTeamName(TeamId team)
    {
        string name = (TeamUtils.GetName(team) ?? string.Empty).ToUpperInvariant();
        string hex = ColorUtility.ToHtmlStringRGB(TeamUtils.GetColor(team));
        return $"<color=#{hex}>{name}</color>";
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
        if (players[index].defeated)
        {
            int aliveIndex = FindNextAlivePlayerIndex(index - 1);
            if (aliveIndex < 0)
                return;
            index = aliveIndex;
        }
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

    private void ApplyActiveTeamIfChanged(bool force, bool applyTurnStartEffects = true)
    {
        if (!force && appliedActiveTeamId == activeTeamId)
            return;

        if (activeTeamId >= 0)
        {
            TeamId requestedTeam = ClampToTeamId(activeTeamId);
            if (IsTeamDefeated(requestedTeam))
            {
                int aliveIndex = FindNextAlivePlayerIndex(-1);
                if (aliveIndex >= 0)
                {
                    activePlayerListIndex = aliveIndex;
                    activeTeamId = (int)players[aliveIndex].teamId;
                }
                else
                {
                    TryDeclareLastStandingWinner();
                    return;
                }
            }
        }

        double totalStartMs = TurnPerfNowMs();
        appliedActiveTeamId = activeTeamId;

        double stageStartMs = TurnPerfNowMs();
        if (Application.isPlaying)
        {
            UnitManager.ResetActiveTeamChangedPerfCounters();
            ConstructionManager.ResetActiveTeamChangedPerfCounters();
            OnActiveTeamChanged?.Invoke(activeTeamId);
            if (enableTurnPerfLogs)
            {
                UnitManager.GetActiveTeamChangedPerfCounters(out int unitHandlerCount, out double unitHandlerMs);
                ConstructionManager.GetActiveTeamChangedPerfCounters(out int constructionHandlerCount, out double constructionHandlerMs);
                Debug.Log($"[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count={unitHandlerCount} ms={unitHandlerMs:F3}");
                Debug.Log($"[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count={constructionHandlerCount} ms={constructionHandlerMs:F3}");
            }
        }
        TurnPerfLog("ApplyActiveTeam.OnActiveTeamChanged", stageStartMs);

        stageStartMs = TurnPerfNowMs();
        TeleportCursorToActiveTeamHeadQuarterSilently();
        TurnPerfLog("ApplyActiveTeam.TeleportCursorToHQ", stageStartMs);

        List<ConstructionManager> activeConstructions = GetActiveConstructionsOnScene();

        if (applyTurnStartEffects)
        {
            stageStartMs = TurnPerfNowMs();
            ReleaseUnitsForActiveTeam(activeConstructions);
            TurnPerfLog("ApplyActiveTeam.ReleaseUnitsForActiveTeam", stageStartMs);
        }
        else
            pendingTurnStartAutonomyHelperEntries = null;

        stageStartMs = TurnPerfNowMs();
        if (!debugFogOfWarEnabled)
        {
            ResetFogOfWarRuntime(clearTilemap: true);
            ShowAllUnitsIgnoringFog();
            FlushTurnStartAutonomyHelper();
            TurnPerfLog("ApplyActiveTeam.FogDisabled", stageStartMs);
            TurnPerfLog("ApplyActiveTeam.Total", totalStartMs);
            return;
        }

        if (enableTotalWar)
        {
            if (Application.isPlaying)
            {
                RefreshFogOfWarForActiveTeam();
                RefreshRuntimeUnitFogVisibility();
                RunTurnStartStillObservedForActiveTeamStealthUnits();
            }
            else
            {
                ResetFogOfWarRuntime(clearTilemap: true);
            }
        }
        else
        {
            ResetFogOfWarRuntime(clearTilemap: true);
            RefreshRuntimeUnitFogVisibility();
        }
        TurnPerfLog("ApplyActiveTeam.FogAndVisibility", stageStartMs);

        stageStartMs = TurnPerfNowMs();
        FlushTurnStartAutonomyHelper();
        TurnPerfLog("ApplyActiveTeam.FlushTurnStartAutonomyHelper", stageStartMs);
        TurnPerfLog("ApplyActiveTeam.Total", totalStartMs);
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

    // PONTO UNICO de recomputacao do flip: auto (se habilitado) + overrides por slot
    // + aplicacao nos objetos em campo. Chame ISTO, nunca AutoCompute direto —
    // auto sem overrides ja sobrescreveu escolha manual uma vez (ConstructionSpawner).
    public void RecomputeTeamFlips()
    {
        if (autoFlipXFromHqPositions)
            AutoComputeFlipXFromHqPositions();
        ApplyFlipXOverridesToPlayers();
        ApplyTeamFlipSettingsToSceneObjects();
    }

    // Calcula flipX de cada slot comparando a posicao X do HQ com o centro do mapa.
    // HQ a direita do centro => flipX true. A esquerda => flipX false.
    // PRIVADO de proposito: sozinho ele ignora os overrides — use RecomputeTeamFlips().
    private void AutoComputeFlipXFromHqPositions()
    {
        if (players == null || players.Count == 0)
            return;

        float mapCenterX = GetMapCenterWorldX();
        ConstructionManager[] managers = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < players.Count; i++)
        {
            Vector3? hqPos = FindHqWorldPositionForSlot(managers, i);
            if (!hqPos.HasValue)
                continue;

            PlayerEntry entry = players[i];
            entry.flipX = hqPos.Value.x > mapCenterX;
            players[i] = entry;
        }
    }

    // Aplica o override manual de orientacao por slot por cima do calculo automatico
    // (ou do valor serializado, se o auto estiver desligado). Auto = nao mexe.
    public void ApplyFlipXOverridesToPlayers()
    {
        if (players == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry entry = players[i];
            if (entry.flipXOverride == FlipXOverrideMode.Auto)
                continue;

            entry.flipX = entry.flipXOverride == FlipXOverrideMode.Espelhado;
            players[i] = entry;
        }
    }

    private float GetMapCenterWorldX()
    {
        if (fogOfWarTilemap != null)
        {
            Bounds local = fogOfWarTilemap.localBounds;
            return fogOfWarTilemap.transform.TransformPoint(local.center).x;
        }
        return 0f;
    }

    private static Vector3? FindHqWorldPositionForSlot(ConstructionManager[] managers, int slotIdx)
    {
        for (int i = 0; i < managers.Length; i++)
        {
            ConstructionManager cm = managers[i];
            if (cm == null || !cm.IsPlayerHeadQuarter)
                continue;
            if (cm.SlotIndex == slotIdx)
                return cm.transform.position;
        }
        return null;
    }

    private List<UnitManager> GetActiveUnitsOnScene()
    {
        List<UnitManager> all = UnitManager.AllActive;
        if (all == null || all.Count == 0)
            return new List<UnitManager>();

        Scene activeScene = gameObject.scene;
        List<UnitManager> result = new List<UnitManager>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            UnitManager unit = all[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if (unit.gameObject.scene != activeScene)
                continue;
            result.Add(unit);
        }

        return result;
    }

    private List<ConstructionManager> GetActiveConstructionsOnScene()
    {
        List<ConstructionManager> all = ConstructionManager.AllActive;
        if (all == null || all.Count == 0)
            return new List<ConstructionManager>();

        Scene activeScene = gameObject.scene;
        List<ConstructionManager> result = new List<ConstructionManager>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            ConstructionManager construction = all[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if (construction.gameObject.scene != activeScene)
                continue;
            result.Add(construction);
        }

        return result;
    }

    private void ReleaseUnitsForActiveTeam(List<ConstructionManager> activeConstructions = null)
    {
        if (!Application.isPlaying)
            return;
        if (activeTeamId < 0 && !includeNeutralTeam)
            return;

        double stageStartMs = TurnPerfNowMs();
        activeConstructions ??= GetActiveConstructionsOnScene();
        EvaluateVictoryStarsAtTurnStartForActiveTeam(activeConstructions);
        
        // Checagem de derrota por 0 unidades: marca o time como defeated.
        if (allowDefeatForZeroUnits && currentTurn >= 2 && activeTeamId >= 0)
        {
            TeamId activeTeam = ClampToTeamId(activeTeamId);
            if (TryDefeatTeamIfZeroUnits(activeTeam))
            {
                DeclareEliminationVictory(ResolveEliminatorTeamFor(activeTeam), activeTeam, VictoryReason.ArmyEliminated);
                return;
            }
        }

        TurnPerfLog("ReleaseUnits.EvaluateVictoryStars", stageStartMs);

        stageStartMs = TurnPerfNowMs();
        ApplyEconomyAtTurnStartForActiveTeam(activeConstructions);
        TurnPerfLog("ReleaseUnits.ApplyEconomy", stageStartMs);

        List<TurnStateManager.TurnStartAutonomyUpkeepEntry> turnStartAutonomyEntries = null;
        turnStartUnitsMarkedForFuelDepletionDeath.Clear();
        List<UnitManager> units = GetActiveUnitsOnScene();

        stageStartMs = TurnPerfNowMs();
        for (int i = 0; i < units.Count; i++)
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
                    if (afterFuel <= 0)
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

            unit.ResetForTeamTurnStart();

            IReadOnlyList<UnitTransportSeatRuntime> seats = unit.TransportedUnitSlots;
            if (seats != null)
            {
                for (int s = 0; s < seats.Count; s++)
                {
                    UnitTransportSeatRuntime seat = seats[s];
                    UnitManager passenger = seat != null ? seat.embarkedUnit : null;
                    if (passenger == null || !passenger.IsEmbarked)
                        continue;
                    if ((int)passenger.TeamId != activeTeamId)
                        continue;
                    passenger.ResetForTeamTurnStart();
                }
            }
        }
        TurnPerfLog("ReleaseUnits.IterateUnits", stageStartMs);

        pendingTurnStartAutonomyHelperEntries = turnStartAutonomyEntries;
        TryAutoAssignTurnStateManager();
        stageStartMs = TurnPerfNowMs();
        turnStateManager?.EnqueueTurnStartFuelDepletionDeaths(turnStartUnitsMarkedForFuelDepletionDeath);
        if (turnStartUnitsMarkedForFuelDepletionDeath.Count <= 0)
            turnStateManager?.TryExecuteTurnStartRallyQueueIfIdle();
        TurnPerfLog("ReleaseUnits.EnqueueFuelDepletionDeaths", stageStartMs);

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

    private void RecalculateIncomePerTurnForAllPlayers(List<ConstructionManager> constructions = null)
    {
        if (players == null || players.Count == 0)
            return;

        constructions ??= GetActiveConstructionsOnScene();
        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry entry = players[i];
            int income = 0;
            for (int c = 0; c < constructions.Count; c++)
            {
                ConstructionManager construction = constructions[c];
                if (construction == null)
                    continue;
                if (construction.TeamId != entry.teamId)
                    continue;

                income += ResolveConstructionIncomeForPlayer(entry, construction);
            }

            entry.incomePerTurn = Mathf.Max(0, income);
            players[i] = entry;
        }
    }

    private static int ResolveConstructionIncomeForPlayer(PlayerEntry player, ConstructionManager construction)
    {
        if (construction == null)
            return 0;

        int baseIncome = Mathf.Max(0, construction.CapturedIncoming);
        bool easyAiEconomy = player.isAI &&
                             AIController.Instance != null &&
                             AIController.Instance.EasyMode;
        if (!easyAiEconomy)
            return baseIncome;

        if (construction.TryResolveConstructionData(out ConstructionData data) &&
            data != null && data.isCity)
            return baseIncome;

        return baseIncome / 3;
    }

    private void ApplyEconomyAtTurnStartForActiveTeam(List<ConstructionManager> constructions = null)
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

        RecalculateIncomePerTurnForAllPlayers(constructions);

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
        unchecked
        {
            signature = (signature * 31) +
                        (AIController.Instance != null && AIController.Instance.EasyMode ? 1 : 0);
        }
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
        if (fogOfWarController == null)
            fogOfWarController = FindAnyObjectByType<FogOfWarController>();
        fogOfWarController?.InitializeAlphaFromLegacy(fogOfWarAlpha);

        if (fogOfWarTilemap == null)
            fogOfWarTilemap = FindTilemapByName("FogOfWar");

        if (fogOfWarTerrainDatabase == null)
            fogOfWarTerrainDatabase = ResolveFogTerrainDatabase();
        if (fogOfWarDpqAirHeightConfig == null)
            fogOfWarDpqAirHeightConfig = ResolveFogDpqAirHeightConfig();
    }

    private void TryAutoAssignVictoryOverlayReferences()
    {
        if (victoryOverlayTilemap != null)
            return;

        victoryOverlayTilemap = FindTilemapByName("VictoryOverlay")
            ?? FindTilemapByName("Victory")
            ?? FindTilemapByName("TileMapVictory");
    }

    private void TryRefreshVictoryOverlayFromConstructions(bool markDirtyInEditor)
    {
        int settingsSignature = BuildVictoryOverlaySettingsSignature();

        if (lastVictoryOverlayTilemap != null && lastVictoryOverlayTilemap != victoryOverlayTilemap)
            ClearVictoryOverlayOnTilemap(lastVictoryOverlayTilemap, markDirtyInEditor);
        lastVictoryOverlayTilemap = victoryOverlayTilemap;

        if (!showVictoryOverlay || victoryOverlayTilemap == null || victoryOverlayTile == null)
        {
            if (victoryOverlayActiveCells.Count > 0)
                ClearVictoryOverlay(markDirtyInEditor);
            cachedVictoryOverlaySignature = 0;
            cachedVictoryOverlayCount = 0;
            cachedVictoryOverlaySettingsSignature = settingsSignature;
            return;
        }

        ComputeVictoryOverlaySignature(out int signature, out int count);
        if (settingsSignature == cachedVictoryOverlaySettingsSignature &&
            signature == cachedVictoryOverlaySignature &&
            count == cachedVictoryOverlayCount)
        {
            return;
        }

        cachedVictoryOverlaySignature = signature;
        cachedVictoryOverlayCount = count;
        cachedVictoryOverlaySettingsSignature = settingsSignature;
        ApplyVictoryOverlayFromConstructions(markDirtyInEditor);
    }

    private int BuildVictoryOverlaySettingsSignature()
    {
        unchecked
        {
            int signature = 17;
            signature = (signature * 31) + (showVictoryOverlay ? 1 : 0);
            signature = (signature * 31) + (victoryOverlayTilemap != null ? victoryOverlayTilemap.GetEntityId().GetHashCode() : 0);
            signature = (signature * 31) + (victoryOverlayTile != null ? victoryOverlayTile.GetEntityId().GetHashCode() : 0);
            signature = (signature * 31) + Mathf.RoundToInt(Mathf.Clamp01(victoryOverlayAlpha) * 1000f);
            return signature;
        }
    }

    private void ComputeVictoryOverlaySignature(out int signature, out int count)
    {
        signature = 17;
        count = 0;
        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy || !construction.IsVictoryBuilding)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;

            unchecked
            {
                signature = (signature * 31) + construction.InstanceId;
                signature = (signature * 31) + cell.x;
                signature = (signature * 31) + cell.y;
            }
            count++;
        }
    }

    private void ApplyVictoryOverlayFromConstructions(bool markDirtyInEditor)
    {
        if (victoryOverlayTilemap == null)
            return;

        victoryOverlayTilemap.ClearAllTiles();
        victoryOverlayActiveCells.Clear();

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        
        Color overlayColor = Color.white;
        overlayColor.a = Mathf.Clamp01(victoryOverlayAlpha);

        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy || !construction.IsVictoryBuilding)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            
            victoryOverlayTilemap.SetTile(cell, victoryOverlayTile);
            victoryOverlayTilemap.SetTileFlags(cell, TileFlags.None);
            victoryOverlayTilemap.SetColor(cell, overlayColor);
            
            victoryOverlayActiveCells.Add(cell);
        }

#if UNITY_EDITOR
        if (markDirtyInEditor && !Application.isPlaying)
            EditorUtility.SetDirty(victoryOverlayTilemap);
#endif
    }

    private void ClearVictoryOverlay(bool markDirtyInEditor)
    {
        if (victoryOverlayTilemap == null)
            return;

        ClearVictoryOverlayOnTilemap(victoryOverlayTilemap, markDirtyInEditor);
    }

    private void ClearVictoryOverlayOnTilemap(Tilemap targetTilemap, bool markDirtyInEditor)
    {
        if (targetTilemap == null)
            return;

        targetTilemap.ClearAllTiles();
        victoryOverlayActiveCells.Clear();
#if UNITY_EDITOR
        if (markDirtyInEditor && !Application.isPlaying)
            EditorUtility.SetDirty(targetTilemap);
#endif
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

    public void RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode mode = FogOfWarRefreshMode.FullVisual)
    {
        if (!ShouldUseHumanFogPresentation(out TeamId presentationTeam))
        {
            RefreshFogOfWarForCurrentTeamInternal(mode);
            return;
        }

        int gameplayTeamId = activeTeamId;
        int gameplayPlayerIndex = activePlayerListIndex;
        try
        {
            fogPresentationGameplayTeamId = gameplayTeamId;
            // Mantem a percepção da AI atualizada para decisões e memória.
            RefreshFogOfWarForCurrentTeamInternal(FogOfWarRefreshMode.DataOnly);

            // A apresentação permanece sempre sob os sensores do jogador humano.
            activeTeamId = (int)presentationTeam;
            activePlayerListIndex = GetSlotIndexForTeam(presentationTeam);
            RefreshFogOfWarForCurrentTeamInternal(FogOfWarRefreshMode.FullVisual);
        }
        finally
        {
            activeTeamId = gameplayTeamId;
            activePlayerListIndex = gameplayPlayerIndex;
            fogPresentationGameplayTeamId = int.MinValue;
            // O refresh visual usa temporariamente o time humano. Reaplica a layer
            // depois de restaurar o turno real. Em Total FoW a nevoa fica na layer
            // FogOfWar durante toda a partida (a oclusao e sempre do overlay).
            ValidateFogOfWarSortingLayer();
        }
    }

    private void RefreshFogOfWarForCurrentTeamInternal(FogOfWarRefreshMode mode)
    {
        PodeDetectarSensor.ClearRefreshScopedTerrainCache();
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

        if (ShouldLogPodeEnxergarRuntime)
        {
            Debug.Log(
                $"[FoW][Context] activeTeam={activeTeamId} " +
                $"controllerScene={gameObject.scene.name} " +
                $"fogScene={(fogOfWarTilemap != null ? fogOfWarTilemap.gameObject.scene.name : "-")} " +
                $"boardMap={boardMap.name} boardScene={boardMap.gameObject.scene.name}");
        }

        ResetFogOfWarRuntime(clearTilemap: false);
        InitializeFogRuntimeData(boardMap);
        if (!fogOverlayInitialized)
            return;

        double refreshStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        if (enableFogStepPerfLogs)
            PodeDetectarSensor.ResetFogDebugCounters();
        double collectTotalMs = 0d;
        int collectUnitsMeasured = 0;
        int collectVisibleCellsTotal = 0;
        List<FogCollectPerfEntry> topCollectEntries = enableFogStepPerfLogs
            ? new List<FogCollectPerfEntry>(Mathf.Clamp(fogStepPerfTopUnits, 1, 8))
            : null;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int unitsIncluded = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
            {
                if (ShouldLogPodeEnxergarRuntime && unit != null)
                    Debug.Log($"[FoW][Unit][Skip] {unit.name} reason=inactive_or_embarked");
                continue;
            }
            if ((int)unit.TeamId != activeTeamId)
            {
                if (ShouldLogPodeEnxergarRuntime)
                    Debug.Log($"[FoW][Unit][Skip] {unit.name} reason=other_team team={(int)unit.TeamId}");
                continue;
            }
            if (!IsUnitOnBoard(unit, boardMap))
            {
                if (ShouldLogPodeEnxergarRuntime)
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
            if (ShouldLogPodeEnxergarRuntime)
            {
                Debug.Log(
                    $"[FoW][Unit][Use] {unit.name} team={(int)unit.TeamId} " +
                    $"unitMap={unit.BoardTilemap.name} unitScene={unit.gameObject.scene.name}");
            }

            UpdateFogVisibilityForUnit(
                unit,
                boardMap,
                out double collectMs,
                out int visibleCellsCollected,
                out bool collectExecuted,
                updateVisual: false);
            if (collectExecuted)
            {
                collectTotalMs += collectMs;
                collectUnitsMeasured++;
                collectVisibleCellsTotal += visibleCellsCollected;
                if (topCollectEntries != null)
                    RegisterFogCollectTopEntry(topCollectEntries, unit, collectMs, visibleCellsCollected, Mathf.Clamp(fogStepPerfTopUnits, 1, 8));
            }
        }

        if (ShouldLogPodeEnxergarRuntime)
            Debug.Log($"[FoW][Unit][Summary] total={units.Length} included={unitsIncluded}");

        double constructionVisionStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        int constructionsIncluded = ApplyFriendlyConstructionVision(boardMap, updateVisual: false);
        double constructionVisionMs = enableFogStepPerfLogs
            ? (Time.realtimeSinceStartupAsDouble - constructionVisionStartMs) * 1000d
            : 0d;
        if (mode == FogOfWarRefreshMode.FullVisual)
            RefreshRuntimeUnitFogVisibility();
        if (Application.isPlaying && activeTeamId >= 0
            && Enum.IsDefined(typeof(TeamId), activeTeamId))
            AIIntelLedger.RecordVisibleContactsForTeam(
                (TeamId)activeTeamId, currentTurn, this);
        if (mode == FogOfWarRefreshMode.FullVisual)
        {
            RenderFogOverlayFromRuntimeCache(boardMap);
            if (Application.isPlaying)
                OnFogOfWarUpdated?.Invoke();
        }

        if (enableFogStepPerfLogs)
        {
            double refreshTotalMs = (Time.realtimeSinceStartupAsDouble - refreshStartMs) * 1000d;
            double collectAvgMs = collectUnitsMeasured > 0 ? collectTotalMs / collectUnitsMeasured : 0d;
            string dominant = collectTotalMs >= constructionVisionMs
                ? "CollectVisibleCells"
                : "ApplyFriendlyConstructionVision";
            Debug.Log(
                $"[FoW][Perf] total={refreshTotalMs:F3}ms | " +
                $"collect.total={collectTotalMs:F3}ms collect.avg/unit={collectAvgMs:F3}ms collect.units={collectUnitsMeasured} collect.cells={collectVisibleCellsTotal} | " +
                $"constructionVision={constructionVisionMs:F3}ms constructions={constructionsIncluded} | dominant={dominant}");
            PodeDetectarSensor.GetFogDebugCounters(
                out int cacheHits,
                out int cacheMisses,
                out int poolRents,
                out int poolReleases,
                out int fragataCollectWorkspaceRents,
                out int fragataCollectWorkspaceReleases);
            Debug.Log($"[FoW][Cache] hits={cacheHits} misses={cacheMisses}");
            Debug.Log(
                $"[FoW][Pool] rents={poolRents} releases={poolReleases} " +
                $"fragataCollect.rents={fragataCollectWorkspaceRents} fragataCollect.releases={fragataCollectWorkspaceReleases}");

            if (topCollectEntries != null && topCollectEntries.Count > 0)
            {
                for (int i = 0; i < topCollectEntries.Count; i++)
                {
                    FogCollectPerfEntry entry = topCollectEntries[i];
                    Debug.Log($"[FoW][Perf][CollectTop{(i + 1)}] unit={entry.unitName} ms={entry.collectMs:F3} cells={entry.visibleCellCount}");
                }
            }
        }
    }


    public void RefreshFogOfWarForTeam(TeamId observerTeamId)
    {
        int previousActiveTeamId = activeTeamId;
        try
        {
            activeTeamId = Mathf.Clamp((int)observerTeamId, -1, 3);
            RefreshFogOfWarForActiveTeam();
        }
        finally
        {
            activeTeamId = previousActiveTeamId;
        }
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

        UpdateFogVisibilityForUnit(unit, boardMap, out _, out _, out _);
        RefreshRuntimeUnitFogVisibility();
        AIIntelLedger.RecordVisibleContactsForTeam(ActiveTeam, currentTurn, this);
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

        if (ShouldLogAindaMeVeRuntime)
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
        if (ShouldLogPodeDetectarRuntime)
        {
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
        if (ShouldLogPodeDetectarRuntime || ShouldLogAindaMeVeRuntime)
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
        if (ShouldLogAindaMeVeRuntime)
        {
            Debug.Log(
                $"[AindaMeVe][Runtime] target={actedUnit.name} team={(int)actedUnit.TeamId} " +
                $"hadRevealBefore={hadRevealBefore} observedNow={isObservedNow} observerRadius={observerRadius} observerTeams={teamsObservedLabel}");
        }
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
            if (ShouldLogAindaMeVeRuntime)
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

    public bool IsUnitVisibleForTeam(UnitManager unit, TeamId observerTeam)
    {
        if (!debugFogOfWarEnabled)
            return true;

        if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
            return false;

        if (unit.TeamId == observerTeam)
            return true;

        if (!enableTotalWar)
            return true;

        if ((int)observerTeam == activeTeamId)
        {
            int cacheIndex = ResolveFogCacheIndex(unit);
            if (fogCachedTeamId == activeTeamId &&
                fogUnitVisibilityByCacheIndex.TryGetValue(cacheIndex, out bool cachedVisible))
            {
                return cachedVisible;
            }
        }

        return ComputeIsUnitVisibleForTeamWithoutCache(unit, observerTeam);
    }

    public bool IsUnitVisibleForTeamNoCache(UnitManager unit, TeamId observerTeam)
    {
        if (!debugFogOfWarEnabled)
            return true;

        if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
            return false;

        if (unit.TeamId == observerTeam)
            return true;

        if (!enableTotalWar)
            return true;

        return ComputeIsUnitVisibleForTeamWithoutCache(unit, observerTeam);
    }

    private bool ComputeIsUnitVisibleForTeamWithoutCache(UnitManager unit, TeamId observerTeam)
    {
        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return false;

        if (IsUnitOnFriendlyConstruction(unit, observerTeam, boardMap))
            return true;

        bool enforceStealthValidation = enableStealthValidation && !unit.HasFiredThisTurn;
        return PodeDetectarSensor.IsTargetObservedByTeamWithoutForwardObserver(
            unit,
            (int)observerTeam,
            boardMap,
            ResolveFogTerrainDatabase(),
            ResolveFogDpqAirHeightConfig(),
            enableLosValidation,
            enforceStealthValidation);
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

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return false;

        if (Enum.IsDefined(typeof(TeamId), activeTeamId)
            && IsUnitOnFriendlyConstruction(unit, (TeamId)activeTeamId, boardMap))
        {
            return true;
        }

        bool enforceStealthValidation = enableStealthValidation && !unit.HasFiredThisTurn;
        return PodeDetectarSensor.IsTargetObservedByTeamWithoutForwardObserver(
            unit,
            activeTeamId,
            boardMap,
            ResolveFogTerrainDatabase(),
            ResolveFogDpqAirHeightConfig(),
            enableLosValidation,
            enforceStealthValidation);
    }

    public void DeclareSurrenderDefeat()
    {
        if (hasVictoryWinner)
            return;

        // Rendicao e mais uma condicao de derrota: entra no mesmo ciclo de eliminacao/vitoria.
        // Quem se rende e o jogador ativo (dono do menu). Ele e marcado como derrotado e o vencedor
        // vira o primeiro oponente vivo — a apresentacao unica mostra DERROTA! para o humano.
        TeamId surrenderingTeam = ClampToTeamId(activeTeamId);
        if (surrenderingTeam != TeamId.Neutral)
            MarkTeamDefeated(surrenderingTeam, "rendicao");

        TeamId winner = ResolveEliminatorTeamFor(surrenderingTeam);
        if (DeclareEliminationVictory(winner, surrenderingTeam, VictoryReason.Surrender))
            return;

        // Sem oponente vivo para coroar (config atipica): encerra como derrota simples.
        hasVictoryWinner = true;
        victoryWinnerTeam = TeamId.Neutral;
        HandleVictoryAestheticPresentation(TeamId.Neutral, surrenderingTeam, VictoryReason.Surrender);
    }

    private bool IsUnitOnFriendlyConstruction(UnitManager unit, TeamId observerTeam, Tilemap boardMap)
    {
        if (unit == null || boardMap == null || observerTeam == TeamId.Neutral)
            return false;
        if (!IsUnitOnBoard(unit, boardMap))
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        List<ConstructionManager> constructions = ConstructionManager.AllActive;
        for (int i = constructions.Count - 1; i >= 0; i--)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if (!IsConstructionOwnedByTeam(construction, observerTeam))
                continue;
            if (construction.BoardTilemap != boardMap)
                continue;
            if (construction.gameObject.scene != boardMap.gameObject.scene)
                continue;

            Vector3Int constructionCell = construction.CurrentCellPosition;
            constructionCell.z = 0;
            if (constructionCell == unitCell)
                return true;
        }

        return false;
    }

    private bool IsConstructionOwnedByActivePlayer(ConstructionManager construction)
    {
        if (construction == null)
            return false;
        if (activePlayerListIndex >= 0 && construction.SlotIndex >= 0)
            return construction.SlotIndex == activePlayerListIndex;
        return (int)construction.TeamId == activeTeamId;
    }

    private bool IsConstructionOwnedByTeam(ConstructionManager construction, TeamId observerTeam)
    {
        if (construction == null || observerTeam == TeamId.Neutral)
            return false;

        int observerSlot = GetSlotIndexForTeam(observerTeam);
        if (observerSlot >= 0 && construction.SlotIndex >= 0)
            return construction.SlotIndex == observerSlot;
        return construction.TeamId == observerTeam;
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

    public bool IsCellVisibleInFogPresentation(Vector3Int cell)
    {
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return true;

        int expectedTeamId = activeTeamId;
        if (ShouldUseHumanFogPresentation(out TeamId presentationTeam))
            expectedTeamId = (int)presentationTeam;
        if (fogCachedTeamId != expectedTeamId)
            return false;

        cell.z = 0;
        return fogVisibleContributorsByCell.TryGetValue(cell, out int contributors) && contributors > 0;
    }

    public bool ShouldHideActiveAiActionPresentation()
    {
        return ShouldUseHumanFogPresentation(out _);
    }

    private bool ShouldUseHumanFogPresentation(out TeamId presentationTeam)
    {
        presentationTeam = TeamId.Neutral;
        if (!Application.isPlaying || !debugFogOfWarEnabled || debugFogOfWarPartial)
            return false;
        if (!enableTotalWar || gameSetup != GameSetupPreset.FogOfWarTotal || activeTeamId < 0)
            return false;
        if (!Enum.IsDefined(typeof(TeamId), activeTeamId) || !IsPlayerAI((TeamId)activeTeamId))
            return false;

        return TryGetFirstHumanTeam(out presentationTeam);
    }

    public void ExportFogRuntimeCacheForSave(
        out int cachedTeamId,
        List<FogCellContributorSaveData> visibleContributorsByCell,
        List<FogUnitVisibilitySaveData> unitVisibilityByCacheIndex)
    {
        cachedTeamId = fogCachedTeamId;

        if (visibleContributorsByCell != null)
        {
            visibleContributorsByCell.Clear();
            foreach (var kv in fogVisibleContributorsByCell)
            {
                if (kv.Value <= 0)
                    continue;

                visibleContributorsByCell.Add(new FogCellContributorSaveData
                {
                    x = kv.Key.x,
                    y = kv.Key.y,
                    z = kv.Key.z,
                    contributors = kv.Value
                });
            }
        }

        if (unitVisibilityByCacheIndex != null)
        {
            unitVisibilityByCacheIndex.Clear();
            foreach (var kv in fogUnitVisibilityByCacheIndex)
            {
                unitVisibilityByCacheIndex.Add(new FogUnitVisibilitySaveData
                {
                    cacheIndex = kv.Key,
                    isVisible = kv.Value
                });
            }
        }
    }

    public bool TryRestoreFogRuntimeCacheFromSave(
        int cachedTeamId,
        List<FogCellContributorSaveData> visibleContributorsByCell,
        List<FogUnitVisibilitySaveData> unitVisibilityByCacheIndex)
    {
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return false;
        if (cachedTeamId != activeTeamId)
            return false;
        bool hasCellContributors = visibleContributorsByCell != null && visibleContributorsByCell.Count > 0;
        bool hasUnitVisibility = unitVisibilityByCacheIndex != null && unitVisibilityByCacheIndex.Count > 0;
        if (!hasCellContributors && !hasUnitVisibility)
            return false;
        if (fogOfWarTilemap == null)
            return false;

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return false;

        ValidateFogOfWarSortingLayer();
        ResetFogOfWarRuntime(clearTilemap: false);
        InitializeFogOverlay(boardMap);
        if (!fogOverlayInitialized)
            return false;

        fogCachedTeamId = cachedTeamId;

        if (visibleContributorsByCell != null)
        {
            for (int i = 0; i < visibleContributorsByCell.Count; i++)
            {
                FogCellContributorSaveData entry = visibleContributorsByCell[i];
                if (entry == null)
                    continue;

                int contributors = Mathf.Max(0, entry.contributors);
                if (contributors <= 0)
                    continue;

                Vector3Int cell = new Vector3Int(entry.x, entry.y, entry.z);
                fogVisibleContributorsByCell[cell] = contributors;
                fogOfWarTilemap.SetTile(cell, null);
            }
        }

        if (unitVisibilityByCacheIndex != null)
        {
            for (int i = 0; i < unitVisibilityByCacheIndex.Count; i++)
            {
                FogUnitVisibilitySaveData entry = unitVisibilityByCacheIndex[i];
                if (entry == null)
                    continue;

                fogUnitVisibilityByCacheIndex[entry.cacheIndex] = entry.isVisible;
            }
        }

        ApplyRuntimeUnitFogVisibilityFromCache(boardMap);
        if (Application.isPlaying)
            OnFogOfWarUpdated?.Invoke();
        return true;
    }

    private void ApplyRuntimeUnitFogVisibilityFromCache(Tilemap boardMap)
    {
        bool fogOverlayOwnsWorldOcclusion = UsesFogOverlayForWorldOcclusion();
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if (boardMap != null && !IsUnitOnBoard(unit, boardMap))
                continue;

            int cacheIndex = ResolveFogCacheIndex(unit);
            bool visible = (int)unit.TeamId == activeTeamId;
            if (!visible)
            {
                if (!fogUnitVisibilityByCacheIndex.TryGetValue(cacheIndex, out visible))
                    visible = false;
            }

            unit.SetFogOfWarVisibility(ResolveFogRenderVisibility(unit, visible, fogOverlayOwnsWorldOcclusion));
        }
    }

    public void BuildFogUnitContributorDebugSnapshot(List<FogUnitContributorDebugInfo> output)
    {
        if (output == null)
            return;
        output.Clear();

        if (!Application.isPlaying)
            return;

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<UnitManager> observers = new List<UnitManager>(64);
        List<UnitManager> targets = new List<UnitManager>(64);
        Dictionary<int, UnitManager> unitsById = new Dictionary<int, UnitManager>(128);

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if (!IsUnitOnBoard(unit, boardMap))
                continue;

            int cacheId = ResolveFogCacheIndex(unit);
            unitsById[cacheId] = unit;
            if ((int)unit.TeamId == activeTeamId)
                observers.Add(unit);
            else
                targets.Add(unit);
        }

        Dictionary<int, HashSet<int>> contributorIdsByTarget = new Dictionary<int, HashSet<int>>();
        List<PodeDetectarOption> detectedStealth = new List<PodeDetectarOption>();
        List<PodeDetectarOption> undetectedStealth = new List<PodeDetectarOption>();
        List<PodeDetectarOption> spottedCandidates = new List<PodeDetectarOption>();
        List<PodeDetectarOption> blockedByLos = new List<PodeDetectarOption>();

        for (int i = 0; i < observers.Count; i++)
        {
            UnitManager observer = observers[i];
            if (observer == null)
                continue;

            PodeDetectarSensor.CollectDetection(
                observer,
                boardMap,
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

            int observerId = ResolveFogCacheIndex(observer);
            RegisterContributorOptions(detectedStealth, observerId, contributorIdsByTarget);
            RegisterContributorOptions(spottedCandidates, observerId, contributorIdsByTarget);
        }

        for (int i = 0; i < targets.Count; i++)
        {
            UnitManager target = targets[i];
            if (target == null)
                continue;

            FogUnitContributorDebugInfo info = new FogUnitContributorDebugInfo
            {
                targetUnit = target,
                isVisibleForActiveTeam = IsUnitVisibleForActiveTeam(target)
            };

            int targetId = ResolveFogCacheIndex(target);
            if (contributorIdsByTarget.TryGetValue(targetId, out HashSet<int> contributorIds))
            {
                foreach (int contributorId in contributorIds)
                {
                    if (unitsById.TryGetValue(contributorId, out UnitManager contributor) && contributor != null)
                        info.contributors.Add(contributor);
                }
            }

            output.Add(info);
        }

        output.Sort((a, b) =>
        {
            int visibleCompare = b.isVisibleForActiveTeam.CompareTo(a.isVisibleForActiveTeam);
            if (visibleCompare != 0)
                return visibleCompare;
            int countCompare = b.contributors.Count.CompareTo(a.contributors.Count);
            if (countCompare != 0)
                return countCompare;

            string aName = a.targetUnit != null ? a.targetUnit.UnitDisplayName : string.Empty;
            string bName = b.targetUnit != null ? b.targetUnit.UnitDisplayName : string.Empty;
            return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void RegisterContributorOptions(
        List<PodeDetectarOption> options,
        int observerId,
        Dictionary<int, HashSet<int>> contributorIdsByTarget)
    {
        if (options == null || contributorIdsByTarget == null || observerId == 0)
            return;

        for (int i = 0; i < options.Count; i++)
        {
            PodeDetectarOption option = options[i];
            UnitManager target = option != null ? option.targetUnit : null;
            if (target == null)
                continue;

            int targetId = target.InstanceId > 0 ? target.InstanceId : target.GetEntityId().GetHashCode();
            if (!contributorIdsByTarget.TryGetValue(targetId, out HashSet<int> contributors))
            {
                contributors = new HashSet<int>();
                contributorIdsByTarget[targetId] = contributors;
            }

            contributors.Add(observerId);
        }
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
        if (fogOfWarTilemap == null)
            return;

        TilemapRenderer renderer = fogOfWarTilemap.GetComponent<TilemapRenderer>();
        if (renderer == null)
            return;

        bool coverWorldPresentation = UsesFogOverlayForWorldOcclusion();
        string expectedLayer = coverWorldPresentation ? "FogOfWar" : "SFX";
        string currentLayer = SortingLayer.IDToName(renderer.sortingLayerID);
        if (!string.Equals(currentLayer, expectedLayer, StringComparison.OrdinalIgnoreCase))
        {
            renderer.sortingLayerName = expectedLayer;
            renderer.sortingOrder = 0;
            currentLayer = SortingLayer.IDToName(renderer.sortingLayerID);
        }

        bool playerTurn = activeTeamId >= 0
            && Enum.IsDefined(typeof(TeamId), activeTeamId)
            && !IsPlayerAI((TeamId)activeTeamId);
        cursorController?.ApplyFogOfWarSorting(playerTurn);
        turnStateManager?.ApplyMovementRangeFogOfWarSorting(playerTurn);

        if (fogSortingLayerValidated)
            return;
        fogSortingLayerValidated = true;
        if (!enableFogValidationLogs)
            return;

        if (!string.Equals(currentLayer, expectedLayer, StringComparison.OrdinalIgnoreCase))
            Debug.LogWarning($"[FogOfWar] Sorting layer atual = {currentLayer}. Esperado = {expectedLayer}.");
        else
            Debug.Log($"[FogOfWar] Sorting layer validada em {expectedLayer}.");
    }

    private bool UsesFogOverlayForWorldOcclusion()
    {
        // Em Total FoW a nevoa preta cobre o mundo na sorting layer FogOfWar durante
        // TODA a partida (turno humano incluso). Enquanto ela for opaca e cobrir o
        // mundo, e ela quem faz a oclusao visual: os renderers de unidade/HUD ficam
        // ligados e a unidade so "surge" pela propria animacao ao cruzar a fronteira
        // revelada. Assim nao ocorre o aparecimento magico de religar renderers ja na
        // posicao final apos o refresh de FoW.
        //
        // O hardcode de SetSpriteVisible/SetHudVisible continua como fallback para
        // nevoa parcial/transparente, modos sem overlay opaco, E para o caso em que a
        // celula da unidade esta revelada (sem tile de nevoa) mas a unidade continua
        // logicamente invisivel (ex.: terreno revelado por raio de visao, mas unidade
        // nao spottada/stealth/sem LoS). Ali o overlay NAO cobre o mundo, entao a
        // oclusao precisa vir do hide individual -> ver ResolveFogRenderVisibility.
        // A visibilidade logica (fogUnitVisibilityByCache / IsUnitVisibleForTeam)
        // segue valendo para selecao, sensores e regras, independente disto.
        //
        // NOTE: independente de qual time esta ativo. A perspectiva apresentada e
        // decidida por ShouldUseHumanFogPresentation; a posse da oclusao nao.
        if (!Application.isPlaying || !debugFogOfWarEnabled || debugFogOfWarPartial)
            return false;
        if (!enableTotalWar || gameSetup != GameSetupPreset.FogOfWarTotal)
            return false;
        return fogOfWarTilemap != null;
    }

    // Resolve o estado final do renderer sob Total FoW.
    //
    // - Se o PodeDetectar disser que a unidade e visivel para o observador
    //   (logicallyVisible), renderiza: o sensor e a fonte de verdade da deteccao.
    // - Caso contrario, so mantem o renderer ligado quando o overlay opaco de fato
    //   COBRE a celula da unidade. Ai a nevoa preta em world-space faz a oclusao e o
    //   renderer pode ficar ligado para a animacao de travessia da fronteira funcionar
    //   sem o "religa renderer na posicao final" (teleporte).
    //
    // Em celula revelada (sem tile de nevoa) o overlay nao cobre nada, entao a unidade
    // invisivel volta a ser ocultada pelo hide individual do sensor -- evitando que ela
    // apareca flutuando em terreno revelado mas nao spottado (raio de visao vs spot).
    //
    // A cobertura da celula usa a API oficial do proprio FoW (mesma que decide quais
    // tiles pretos sao limpos), sem logica paralela.
    private bool ResolveFogRenderVisibility(UnitManager unit, bool logicallyVisible, bool fogOverlayOwnsWorldOcclusion)
    {
        if (logicallyVisible)
            return true;
        if (!fogOverlayOwnsWorldOcclusion || unit == null)
            return false;

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        // Coberta pela nevoa = nao visivel na apresentacao atual.
        return !IsCellVisibleInFogPresentation(cell);
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
        fogDisplayVisibleCellsBuffer.Clear();
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
        Color fogColor = new Color(0f, 0f, 0f, ResolveFogOfWarAlpha());
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

    // Coleta células do board e marca overlay como inicializado, sem escrever no tilemap.
    // Usado em DataOnly e como fase de dados do FullVisual.
    private void InitializeFogRuntimeData(Tilemap boardMap)
    {
        fogBoardCellsBuffer.Clear();
        CollectBoardCells(boardMap, fogBoardCellsBuffer);
        if (fogBoardCellsBuffer.Count <= 0)
        {
            fogOverlayInitialized = false;
            return;
        }
        fogCachedTeamId = activeTeamId;
        fogOverlayInitialized = true;
    }

    // Desenha o overlay de névoa a partir do cache já calculado (fogVisibleContributorsByCell).
    // Deve ser chamado apenas após todos os UpdateFogVisibilityForUnit do turno terem rodado.
    private void RenderFogOverlayFromRuntimeCache(Tilemap boardMap)
    {
        bool useDisplayFilter = fogOfWarVisionMode != FogOfWarVisionMode.All;
        if (useDisplayFilter)
            BuildFogDisplayVisibleCellsForMode(boardMap, fogOfWarVisionMode, fogDisplayVisibleCellsBuffer);

        fogOfWarTilemap.ClearAllTiles();
        Color fogColor = new Color(0f, 0f, 0f, ResolveFogOfWarAlpha());
        for (int i = 0; i < fogBoardCellsBuffer.Count; i++)
        {
            Vector3Int cell = fogBoardCellsBuffer[i];
            bool visible = useDisplayFilter
                ? fogDisplayVisibleCellsBuffer.Contains(cell)
                : fogVisibleContributorsByCell.ContainsKey(cell);
            if (visible) continue;
            TileBase tile = ResolveFogTileForCell(boardMap, cell);
            if (tile == null) continue;
            fogOfWarTilemap.SetTile(cell, tile);
            fogOfWarTilemap.SetTileFlags(cell, TileFlags.None);
            fogOfWarTilemap.SetColor(cell, fogColor);
        }
    }

    private void BuildFogDisplayVisibleCellsForMode(
        Tilemap boardMap,
        FogOfWarVisionMode mode,
        HashSet<Vector3Int> output)
    {
        if (output == null)
            return;

        output.Clear();
        if (boardMap == null || mode == FogOfWarVisionMode.All)
            return;

        TerrainDatabase terrainDatabase = ResolveFogTerrainDatabase();
        DPQAirHeightConfig dpqConfig = ResolveFogDpqAirHeightConfig();
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if ((int)unit.TeamId != activeTeamId)
                continue;
            if (!IsUnitOnBoard(unit, boardMap))
                continue;

            switch (mode)
            {
                case FogOfWarVisionMode.Air:
                    AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig, Domain.Air, HeightLevel.AirLow, output);
                    AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig, Domain.Air, HeightLevel.AirHigh, output);
                    break;
                case FogOfWarVisionMode.Surface:
                    AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig, Domain.Land, HeightLevel.Surface, output);
                    AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig, Domain.Naval, HeightLevel.Surface, output);
                    break;
                case FogOfWarVisionMode.Sub:
                    AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig, Domain.Submarine, HeightLevel.Submerged, output);
                    break;
            }
        }

        // Construcoes aliadas fornecem vigilancia local em todas as camadas:
        // somente o proprio hex, sem ampliar alcance nem atuar como spotter.
        AddFriendlyConstructionDisplayCells(boardMap, output);
    }

    private void AddFogLayerVisibleCellsForUnit(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqConfig,
        Domain targetDomain,
        HeightLevel targetHeight,
        HashSet<Vector3Int> output)
    {
        PodeDetectarSensor.CollectVisibleCells(
            unit,
            boardMap,
            terrainDatabase,
            output,
            dpqConfig,
            enableLosValidation,
            enableSpotter: false,
            useOccupantLayerForTarget: false,
            preserveObserverLayerRangeForHexVisibility: false,
            forceVirtualTargetLayer: true,
            forcedVirtualTargetDomain: targetDomain,
            forcedVirtualTargetHeight: targetHeight,
            useRangeOnlyForAirHighWhenConfigured: true);
    }

    private void AddFriendlyConstructionDisplayCells(Tilemap boardMap, HashSet<Vector3Int> output)
    {
        if (boardMap == null || output == null || activeTeamId < 0)
            return;

        List<ConstructionManager> constructions = ConstructionManager.AllActive;
        for (int i = constructions.Count - 1; i >= 0; i--)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
            {
                constructions.RemoveAt(i);
                continue;
            }
            if (!construction.gameObject.activeInHierarchy)
                continue;
            if (!IsConstructionOwnedByActivePlayer(construction))
                continue;

            Tilemap constructionMap = construction.BoardTilemap;
            if (constructionMap == null && construction.gameObject.scene == boardMap.gameObject.scene)
            {
                construction.SetBoardTilemap(boardMap);
                constructionMap = construction.BoardTilemap;
            }

            if (constructionMap != boardMap || construction.gameObject.scene != boardMap.gameObject.scene)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (boardMap.GetTile(cell) != null)
                output.Add(cell);
        }
    }

    private void UpdateFogVisibilityForUnit(
        UnitManager unit,
        Tilemap boardMap,
        out double collectMs,
        out int visibleCellsCollected,
        out bool collectExecuted,
        bool updateVisual = true)
    {
        collectMs = 0d;
        visibleCellsCollected = 0;
        collectExecuted = false;

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
                ApplyFogContribution(cell, -1, boardMap, updateVisual);
            cacheEntry.visibleCells.Clear();
        }

        if (!unit.gameObject.activeInHierarchy || unit.IsEmbarked || (int)unit.TeamId != activeTeamId)
        {
            cacheEntry.key = nextKey;
            return;
        }

        fogUnitVisibleScratchBuffer.Clear();
        double collectStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        PodeDetectarSensor.CollectVisibleCellsForFogOfWar(
            unit,
            boardMap,
            ResolveFogTerrainDatabase(),
            fogUnitVisibleScratchBuffer,
            ResolveFogDpqAirHeightConfig(),
            enableLosValidation);
        collectExecuted = true;
        visibleCellsCollected = fogUnitVisibleScratchBuffer.Count;
        if (enableFogStepPerfLogs)
            collectMs = (Time.realtimeSinceStartupAsDouble - collectStartMs) * 1000d;

        foreach (Vector3Int cell in fogUnitVisibleScratchBuffer)
        {
            cacheEntry.visibleCells.Add(cell);
            ApplyFogContribution(cell, +1, boardMap, updateVisual);
        }

        cacheEntry.key = nextKey;
        if (updateVisual && fogOfWarVisionMode != FogOfWarVisionMode.All)
            RenderFogOverlayFromRuntimeCache(boardMap);
    }

    public void NotifyUnitWillBeDisabledForFog(UnitManager unit)
    {
        if (!Application.isPlaying || unit == null)
            return;
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return;
        if (fogOfWarTilemap == null)
            return;

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return;

        int cacheIndex = ResolveFogCacheIndex(unit);
        if (fogVisibleCellsByUnit.TryGetValue(cacheIndex, out FogOfWarUnitCacheEntry cacheEntry) &&
            cacheEntry != null &&
            cacheEntry.visibleCells.Count > 0)
        {
            foreach (Vector3Int cell in cacheEntry.visibleCells)
                ApplyFogContribution(cell, -1, boardMap);
            cacheEntry.visibleCells.Clear();
            fogVisibleCellsByUnit.Remove(cacheIndex);
        }

        fogUnitVisibilityByCacheIndex[cacheIndex] = false;
        RefreshRuntimeUnitFogVisibility();
        if (fogOfWarVisionMode != FogOfWarVisionMode.All)
            RenderFogOverlayFromRuntimeCache(boardMap);
        OnFogOfWarUpdated?.Invoke();
    }

    private void ApplyFogContribution(Vector3Int cell, int delta, Tilemap boardMap, bool updateVisual = true)
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

        if (!updateVisual)
            return;

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
            fogOfWarTilemap.SetColor(cell, new Color(0f, 0f, 0f, ResolveFogOfWarAlpha()));
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

    private int ApplyFriendlyConstructionVision(Tilemap boardMap, bool updateVisual = true)
    {
        if (boardMap == null || activeTeamId < 0)
            return 0;

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
            bool ownedByActivePlayer = IsConstructionOwnedByActivePlayer(construction);
            bool alwaysVisibleHeadQuarter = construction.IsPlayerHeadQuarter;
            if (!ownedByActivePlayer && !alwaysVisibleHeadQuarter)
            {
                if (ShouldLogPodeEnxergarRuntime)
                    Debug.Log($"[FoW][Construction][Skip] {construction?.name} reason=other_team team={(int)construction.TeamId}");
                continue;
            }
            if (ownedByActivePlayer)
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
                if (ShouldLogPodeEnxergarRuntime)
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
                if (ShouldLogPodeEnxergarRuntime)
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

            // O QG e um marco global do tabuleiro: todos conhecem seu hex.
            // Apenas o dono recebe o restante do raio de visao configurado.
            if (!ownedByActivePlayer)
            {
                constructionsIncluded++;
                ApplyFogContribution(cell, +1, boardMap, updateVisual);
                if (ShouldLogPodeEnxergarRuntime)
                {
                    Debug.Log($"[FoW][Construction][Use] {construction.name} cell={cell.x},{cell.y} reason=global_hq");
                }
                continue;
            }

            int visionRange = 0;
            if (construction.TryResolveConstructionData(out ConstructionData constructionData) &&
                constructionData != null)
            {
                visionRange = Mathf.Max(0, constructionData.visao);
            }

            constructionsIncluded++;
            if (ShouldLogPodeEnxergarRuntime)
                Debug.Log($"[FoW][Construction][Use] {construction.name} cell={cell.x},{cell.y} vision={visionRange}");

            HashSet<Vector3Int> visibleCells = BuildCellsInRadius(boardMap, cell, visionRange);
            foreach (Vector3Int visibleCell in visibleCells)
                ApplyFogContribution(visibleCell, +1, boardMap, updateVisual);
        }

        if (ShouldLogPodeEnxergarRuntime)
        {
            Debug.Log(
                $"[FoW][Construction][Temp] allActive={constructions.Count} " +
                $"activeTeamCandidates={activeTeamCandidates} included={constructionsIncluded} activeTeam={activeTeamId}");
        }

        if (ShouldLogPodeEnxergarRuntime)
            Debug.Log($"[FoW][Construction][Summary] total={constructions.Count} included={constructionsIncluded}");

        return constructionsIncluded;
    }

    private static void RegisterFogCollectTopEntry(
        List<FogCollectPerfEntry> topEntries,
        UnitManager unit,
        double collectMs,
        int visibleCellsCollected,
        int topN)
    {
        if (topEntries == null || topN <= 0)
            return;
        if (unit == null)
            return;

        string unitName = !string.IsNullOrWhiteSpace(unit.UnitDisplayName) ? unit.UnitDisplayName : unit.name;
        FogCollectPerfEntry candidate = new FogCollectPerfEntry(unitName, collectMs, visibleCellsCollected);

        int insertIndex = topEntries.Count;
        for (int i = 0; i < topEntries.Count; i++)
        {
            if (candidate.collectMs > topEntries[i].collectMs)
            {
                insertIndex = i;
                break;
            }
        }

        if (insertIndex >= topN && topEntries.Count >= topN)
            return;

        if (insertIndex >= topEntries.Count)
            topEntries.Add(candidate);
        else
            topEntries.Insert(insertIndex, candidate);

        while (topEntries.Count > topN)
            topEntries.RemoveAt(topEntries.Count - 1);
    }

    private void RefreshRuntimeUnitFogVisibility()
    {
        if (!debugFogOfWarEnabled || !enableTotalWar || gameSetup != GameSetupPreset.FogOfWarTotal)
        {
            ShowAllUnitsIgnoringFog();
            return;
        }

        TeamId observerTeam = ActiveTeam;
        bool useHumanPresentation = ShouldUseHumanFogPresentation(out TeamId presentationTeam);
        bool fogOverlayOwnsWorldOcclusion = UsesFogOverlayForWorldOcclusion();
        if (useHumanPresentation)
            observerTeam = presentationTeam;

        List<UnitManager> units = UnitManager.AllActive;
        fogUnitVisibilityByCacheIndex.Clear();
        Tilemap boardMap = ResolveFogBoardTilemap();
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if (boardMap != null && !IsUnitOnBoard(unit, boardMap))
                continue;

            bool visible = unit.TeamId == observerTeam
                || ComputeIsUnitVisibleForTeamWithoutCache(unit, observerTeam);
            fogUnitVisibilityByCacheIndex[ResolveFogCacheIndex(unit)] = visible;
            unit.SetFogOfWarVisibility(ResolveFogRenderVisibility(unit, visible, fogOverlayOwnsWorldOcclusion));
        }
    }

    public void ApplyConservativeFogVisibilityForLoading()
    {
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        fogUnitVisibilityByCacheIndex.Clear();
        Tilemap boardMap = ResolveFogBoardTilemap();
        bool useConservativeFog = debugFogOfWarEnabled && enableTotalWar && activeTeamId >= 0;
        bool fogOverlayOwnsWorldOcclusion = UsesFogOverlayForWorldOcclusion();

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if (boardMap != null && !IsUnitOnBoard(unit, boardMap))
                continue;

            bool visible = !useConservativeFog || (int)unit.TeamId == activeTeamId;
            fogUnitVisibilityByCacheIndex[ResolveFogCacheIndex(unit)] = visible;
            unit.SetFogOfWarVisibility(ResolveFogRenderVisibility(unit, visible, fogOverlayOwnsWorldOcclusion));
        }
    }

    public void SetFogOfWarDebugEnabled(bool enabled)
    {
        bool modeChanged = debugFogOfWarPartial;
        debugFogOfWarPartial = false;
        if (debugFogOfWarEnabled == enabled && !modeChanged)
            return;

        debugFogOfWarEnabled = enabled;
        fogSortingLayerValidated = false;
        ValidateFogOfWarSortingLayer();
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

    public void SetFogOfWarAlphaPercent(int alphaPercent)
    {
        int clampedPercent = Mathf.Clamp(alphaPercent, 0, 100);
        TryAutoAssignFogOfWarReferences();
        if (fogOfWarController != null)
            fogOfWarController.SetAlphaPercent(clampedPercent);
        else
            fogOfWarAlpha = clampedPercent / 100f;
        if (fogOfWarTilemap == null)
            return;

        Color fogColor = new Color(0f, 0f, 0f, ResolveFogOfWarAlpha());
        BoundsInt bounds = fogOfWarTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (fogOfWarTilemap.HasTile(cell))
                fogOfWarTilemap.SetColor(cell, fogColor);
        }
    }

    private float ResolveFogOfWarAlpha()
    {
        return fogOfWarController != null
            ? fogOfWarController.FogOfWarAlpha
            : Mathf.Clamp01(fogOfWarAlpha);
    }

    public void SetFogOfWarDebugPartial()
    {
        bool modeChanged = !debugFogOfWarEnabled || !debugFogOfWarPartial;
        debugFogOfWarEnabled = true;
        debugFogOfWarPartial = true;
        fogSortingLayerValidated = false;
        ValidateFogOfWarSortingLayer();
        if (!modeChanged)
            return;

        if (enableTotalWar)
            RefreshFogOfWarForActiveTeam();
        else
            ResetFogOfWarRuntime(clearTilemap: true);

        RefreshRuntimeUnitFogVisibility();
        Debug.Log("[Debug Command] FoW PARTIAL (debug): exibindo a perspectiva do time ativo.");
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
        return unit.GetEntityId().GetHashCode();
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
            hash = (hash * 31) + (boardMap != null ? boardMap.GetEntityId().GetHashCode() : 0);
            TerrainDatabase fogTerrainDb = ResolveFogTerrainDatabase();
            DPQAirHeightConfig fogAirConfig = ResolveFogDpqAirHeightConfig();
            hash = (hash * 31) + (fogTerrainDb != null ? fogTerrainDb.GetEntityId().GetHashCode() : 0);
            hash = (hash * 31) + (fogAirConfig != null ? fogAirConfig.GetEntityId().GetHashCode() : 0);
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

        if (activeTeamId == (int)TeamId.Neutral && includeNeutralTeam)
        {
            if (TryResolveTeamHeadQuarterCell(activeTeamId, out Vector3Int neutralHeadQuarterCell))
            {
                cursorController.SetCell(neutralHeadQuarterCell, playMoveSfx: false);
                return;
            }

            if (TryTeleportCursorToNearestUnitForActiveTeam(preferReadyUnits: true))
                return;
        }

        if (!TeamAnchorResolver.TryResolveAnchorCell(activeTeamId, out Vector3Int anchorCell))
        {
            if (activeTeamId == (int)TeamId.Neutral && includeNeutralTeam)
                TryTeleportCursorToNearestUnitForActiveTeam(preferReadyUnits: true);
            return;
        }

        cursorController.SetCell(anchorCell, playMoveSfx: false);
    }

    private bool TryTeleportCursorToNearestUnitForActiveTeam(bool preferReadyUnits)
    {
        if (cursorController == null)
            return false;

        List<UnitManager> units = UnitManager.AllActive;
        if (units == null || units.Count == 0)
            return false;

        Vector3Int origin = cursorController.CurrentCell;
        origin.z = 0;
        bool foundPreferred = false;
        Vector3Int bestPreferredCell = origin;
        float bestPreferredDistanceSqr = float.MaxValue;
        bool foundFallback = false;
        Vector3Int bestFallbackCell = origin;
        float bestFallbackDistanceSqr = float.MaxValue;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if ((int)unit.TeamId != activeTeamId)
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;

            float dx = cell.x - origin.x;
            float dy = cell.y - origin.y;
            float distanceSqr = (dx * dx) + (dy * dy);

            if (!foundFallback || distanceSqr < bestFallbackDistanceSqr)
            {
                foundFallback = true;
                bestFallbackDistanceSqr = distanceSqr;
                bestFallbackCell = cell;
            }

            bool isPreferred = !preferReadyUnits || !unit.HasActed;
            if (isPreferred && (!foundPreferred || distanceSqr < bestPreferredDistanceSqr))
            {
                foundPreferred = true;
                bestPreferredDistanceSqr = distanceSqr;
                bestPreferredCell = cell;
            }
        }

        if (foundPreferred)
        {
            cursorController.SetCell(bestPreferredCell, playMoveSfx: false);
            return true;
        }

        if (foundFallback)
        {
            cursorController.SetCell(bestFallbackCell, playMoveSfx: false);
            return true;
        }

        return false;
    }

    private static bool TryResolveTeamHeadQuarterCell(int teamId, out Vector3Int cell)
    {
        cell = Vector3Int.zero;
        if (teamId < 0)
            return false;

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        ConstructionManager bestHq = null;
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if ((int)construction.TeamId != teamId)
                continue;
            if (!IsHeadQuarterConstruction(construction))
                continue;

            if (bestHq == null || construction.InstanceId < bestHq.InstanceId)
                bestHq = construction;
        }

        if (bestHq == null)
            return false;

        cell = bestHq.CurrentCellPosition;
        cell.z = 0;
        return true;
    }

    public void DeclareTutorialDefeat(TutorialData tutorial, string reason = "")
    {
        if (tutorial == null || hasVictoryWinner)
            return;

        hasVictoryWinner = true; // Marca como encerrado
        victoryWinnerTeam = TeamId.Neutral; 

        Debug.Log($"[MatchController] Tutorial '{tutorial.id}' FALHOU! Derrota decretada. Razao: {reason}");

        // Para musica permanentemente
        if (matchMusicAudioManager != null)
        {
            matchMusicAudioManager.StopPlaybackPermanently(); 
        }

        // Tocar defeat SFX
        if (cursorController != null)
        {
            cursorController.PlayDefeatSfx();
        }

        // Busca o painel pelo nome (como no DeclareDefeat original)
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "Panel_endGame" && go.scene.name != null)
            {
                go.SetActive(true);
                // Busca todos os textos para atualizar titulo e descricao
                var texts = go.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var txt in texts)
                {
                    if (txt.name == "text_endgame")
                    {
                        txt.text = "DERROTA!";
                    }
                    else if (txt.name == "text_descrição" || txt.name == "text_description" || txt.name == "txt_descricao")
                    {
                        txt.text = reason;
                    }
                }
                break;
            }
        }
    }

    private static bool IsHeadQuarterConstruction(ConstructionManager construction)
    {
        if (construction == null)
            return false;

        if (construction.IsPlayerHeadQuarter)
            return true;

        string constructionId = construction.ConstructionId;
        if (!string.IsNullOrWhiteSpace(constructionId) &&
            string.Equals(constructionId.Trim(), "hq", StringComparison.OrdinalIgnoreCase))
            return true;

        string displayName = construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(displayName) &&
            displayName.IndexOf("hq", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}













