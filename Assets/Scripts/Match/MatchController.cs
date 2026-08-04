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

/// <summary>
/// Identidade lógica de um participante da partida.
/// Não representa cor ou facção visual; isso continua sendo responsabilidade de TeamId.
/// </summary>
[Serializable]
public readonly struct PlayerSlotId : IEquatable<PlayerSlotId>, IComparable<PlayerSlotId>
{
    public const int InvalidValue = -1;

    public static PlayerSlotId Invalid => new PlayerSlotId(InvalidValue);

    public int Value { get; }
    public bool IsValid => Value >= 0;

    private PlayerSlotId(int value)
    {
        Value = value;
    }

    public static PlayerSlotId FromIndex(int slotIndex)
    {
        return slotIndex >= 0 ? new PlayerSlotId(slotIndex) : Invalid;
    }

    public bool Equals(PlayerSlotId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is PlayerSlotId other && Equals(other);
    public override int GetHashCode() => Value;
    public int CompareTo(PlayerSlotId other) => Value.CompareTo(other.Value);
    public override string ToString() => IsValid ? $"Slot {Value}" : "Slot inválido";

    public static bool operator ==(PlayerSlotId left, PlayerSlotId right) => left.Equals(right);
    public static bool operator !=(PlayerSlotId left, PlayerSlotId right) => !left.Equals(right);
}

/// <summary>
/// Relações de propriedade entre participantes. Slot inválido representa mundo/neutro:
/// não é aliado nem inimigo de um participante.
/// </summary>
public static class PlayerSlotRelations
{
    public static bool IsPlayerOwned(int slotIndex) => slotIndex >= 0;

    public static bool AreAllies(int firstSlotIndex, int secondSlotIndex) =>
        firstSlotIndex >= 0 && secondSlotIndex >= 0 && firstSlotIndex == secondSlotIndex;

    public static bool AreEnemies(int firstSlotIndex, int secondSlotIndex) =>
        firstSlotIndex >= 0 && secondSlotIndex >= 0 && firstSlotIndex != secondSlotIndex;

    public static bool AreAllies(UnitManager first, UnitManager second) =>
        first != null && second != null && AreAllies(first.SlotIndex, second.SlotIndex);

    public static bool AreEnemies(UnitManager first, UnitManager second) =>
        first != null && second != null && AreEnemies(first.SlotIndex, second.SlotIndex);

    public static bool AreAllies(UnitManager unit, ConstructionManager construction) =>
        unit != null && construction != null && AreAllies(unit.SlotIndex, construction.SlotIndex);

    public static bool AreEnemies(UnitManager unit, ConstructionManager construction) =>
        unit != null && construction != null && AreEnemies(unit.SlotIndex, construction.SlotIndex);
}

[Flags]
public enum CommittedBoardChangeKind
{
    None = 0,
    UnitActed = 1 << 0,
    UnitSpawned = 1 << 1,
    UnitRemoved = 1 << 2,
    MultiUnitChanged = 1 << 3
}

public enum FogPlanningSnapshotBarrierResult
{
    Unavailable = 0,
    ReusedAndReconciled = 1,
    FullFallback = 2,
    RejectedOutsideNeutral = 3
}

/// <summary>
/// Descreve somente mutacoes ja comprometidas do tabuleiro. O delta pode ser
/// acumulado enquanto a FSM conclui a apresentacao, mas so pode ser consumido
/// depois do retorno a Neutral.
/// </summary>
public sealed class CommittedBoardDelta
{
    private readonly List<UnitManager> changedUnits = new List<UnitManager>();
    private readonly HashSet<UnitManager> changedUnitSet = new HashSet<UnitManager>();
    private readonly HashSet<UnitManager> unitsRequiringHasActed = new HashSet<UnitManager>();
    private readonly HashSet<Vector3Int> changedCells = new HashSet<Vector3Int>();

    public CommittedBoardChangeKind ChangeKind { get; private set; }
    public IReadOnlyList<UnitManager> ChangedUnits => changedUnits;
    public IReadOnlyCollection<Vector3Int> ChangedCells => changedCells;
    public bool RequireFullFogRefresh { get; private set; }
    public bool RequireSourceReconciliation { get; private set; }
    public bool IsEmpty =>
        ChangeKind == CommittedBoardChangeKind.None &&
        changedUnits.Count == 0 &&
        !RequireFullFogRefresh &&
        !RequireSourceReconciliation;

    public void AddUnit(
        UnitManager unit,
        CommittedBoardChangeKind changeKind,
        bool requireHasActed = false)
    {
        ChangeKind |= changeKind;
        if (unit == null)
            return;

        if (changedUnitSet.Add(unit))
            changedUnits.Add(unit);
        if (requireHasActed)
            unitsRequiringHasActed.Add(unit);

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        changedCells.Add(cell);
    }

    public void RequireFullRefresh(CommittedBoardChangeKind changeKind)
    {
        ChangeKind |= changeKind;
        RequireFullFogRefresh = true;
    }

    public void RequireReconciliation(CommittedBoardChangeKind changeKind)
    {
        ChangeKind |= changeKind;
        RequireSourceReconciliation = true;
    }

    public void AddChangedCell(Vector3Int cell)
    {
        cell.z = 0;
        changedCells.Add(cell);
    }

    public void MergeFrom(CommittedBoardDelta other)
    {
        if (other == null || other.IsEmpty)
            return;

        ChangeKind |= other.ChangeKind;
        RequireFullFogRefresh |= other.RequireFullFogRefresh;
        RequireSourceReconciliation |= other.RequireSourceReconciliation;

        for (int i = 0; i < other.changedUnits.Count; i++)
        {
            UnitManager unit = other.changedUnits[i];
            if (unit != null && changedUnitSet.Add(unit))
                changedUnits.Add(unit);
            if (unit != null && other.unitsRequiringHasActed.Contains(unit))
                unitsRequiringHasActed.Add(unit);
        }

        foreach (Vector3Int cell in other.changedCells)
            changedCells.Add(cell);
    }

    public bool RequiresHasActed(UnitManager unit)
    {
        return unit != null && unitsRequiringHasActed.Contains(unit);
    }
}

public class MatchController : MonoBehaviour
{
    private const int MaxVictoryStarsGoal = 12;
    private const int FogSourceCacheFormatVersion = 1;
    public static event Action<PlayerSlotId, PlayerSlotId> OnActiveSlotChanged;
    // Compatibilidade temporária: novos sistemas devem assinar OnActiveSlotChanged.
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

    private enum FogContributionSourceType
    {
        Unit = 1,
        Construction = 2
    }

    private readonly struct FogContributionSourceId : IEquatable<FogContributionSourceId>
    {
        public readonly FogContributionSourceType type;
        public readonly int instanceId;

        public FogContributionSourceId(FogContributionSourceType type, int instanceId)
        {
            this.type = type;
            this.instanceId = instanceId;
        }

        public bool Equals(FogContributionSourceId other) =>
            type == other.type && instanceId == other.instanceId;

        public override bool Equals(object obj) =>
            obj is FogContributionSourceId other && Equals(other);

        public override int GetHashCode() => ((int)type * 397) ^ instanceId;
    }

    private sealed class FogSourceContributionCacheEntry
    {
        // Apenas unidades usam a chave incremental nesta etapa. Construcoes entram
        // pelo full refresh, mas ja compartilham a mesma representacao por fonte.
        public FogOfWarUnitCacheKey unitCacheKey;
        public int sourceStateHash;
        public readonly HashSet<Vector3Int> geographicCells = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> sensorCells = new HashSet<Vector3Int>();
    }

    private readonly struct FogSpecializedViewCacheKey : IEquatable<FogSpecializedViewCacheKey>
    {
        public readonly int unitIndex;
        public readonly int snapshotHash;
        public readonly int sensorFlagsHash;
        public readonly Domain domain;
        public readonly HeightLevel height;

        public FogSpecializedViewCacheKey(int unitIndex, int snapshotHash, int sensorFlagsHash, Domain domain, HeightLevel height)
        {
            this.unitIndex = unitIndex;
            this.snapshotHash = snapshotHash;
            this.sensorFlagsHash = sensorFlagsHash;
            this.domain = domain;
            this.height = height;
        }

        public bool Equals(FogSpecializedViewCacheKey other) =>
            unitIndex == other.unitIndex && snapshotHash == other.snapshotHash &&
            sensorFlagsHash == other.sensorFlagsHash && domain == other.domain && height == other.height;

        public override bool Equals(object obj) => obj is FogSpecializedViewCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = unitIndex;
                hash = (hash * 397) ^ snapshotHash;
                hash = (hash * 397) ^ sensorFlagsHash;
                hash = (hash * 397) ^ (int)domain;
                return (hash * 397) ^ (int)height;
            }
        }
    }

    private readonly struct FogCollectPerfEntry
    {
        public readonly string unitName;
        // UnitDisplayName e o nome do TIPO ("APC"), insuficiente para achar a
        // instancia na cena. objectName carrega o formato Tipo_T#_U#.
        public readonly string objectName;
        public readonly int slotIndex;
        public readonly Vector3Int cell;
        public readonly double collectMs;
        public readonly int visibleCellCount;

        public FogCollectPerfEntry(
            string unitName,
            string objectName,
            int slotIndex,
            Vector3Int cell,
            double collectMs,
            int visibleCellCount)
        {
            this.unitName = unitName ?? string.Empty;
            this.objectName = objectName ?? string.Empty;
            this.slotIndex = slotIndex;
            this.cell = cell;
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
        [SerializeField, HideInInspector] public bool isRebelRuntime;
        [Tooltip("Este slot humano pertence a esta maquina/tela. Ignorado para AI.")]
        public bool isLocal;
        [SerializeField, HideInInspector] public bool localityConfigured;
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
        [SerializeField, HideInInspector] public int slotIndex;
        public TeamId teamId;
        [Min(0)] public int stars;
    }

    [System.Serializable]
    private sealed class TeamCapturedBuildingHistory
    {
        public int slotIndex = -1;
        public TeamId teamId;
        public List<string> buildingKeys = new List<string>();
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
        new PlayerEntry { teamId = TeamId.Green, flipX = false, isLocal = true, localityConfigured = true, defeated = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false },
        new PlayerEntry { teamId = TeamId.Red, flipX = true, isLocal = true, localityConfigured = true, defeated = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false },
        new PlayerEntry { teamId = TeamId.Blue, flipX = false, isLocal = true, localityConfigured = true, defeated = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false },
        new PlayerEntry { teamId = TeamId.Yellow, flipX = true, isLocal = true, localityConfigured = true, defeated = false, startMoney = 0, actualMoney = 0, incomePerTurn = 0, startMoneyApplied = false }
    };
    [SerializeField] private bool includeNeutralTeam = false;
    [SerializeField, HideInInspector] private List<TeamCapturedBuildingHistory> capturedBuildingHistory = new List<TeamCapturedBuildingHistory>();
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
    [SerializeField] private PanelRodadaController panelRodada;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPreDelay = 0.1f;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPostDelay = 0f;
    [Header("Victory Stars")]
    [SerializeField] private bool enableVictoryStars = true;
    [SerializeField] [Range(1, MaxVictoryStarsGoal)] private int victoryStarsToWin = 5;
    [SerializeField] private bool freezeTurnAdvanceAfterVictory = true;
    [SerializeField] private List<TeamVictoryEntry> victoryStarsByTeam = new List<TeamVictoryEntry>();
    [SerializeField, HideInInspector] private bool hasVictoryWinner;
    [SerializeField, HideInInspector] private TeamId victoryWinnerTeam = TeamId.Neutral;
    [SerializeField, HideInInspector] private int victoryWinnerSlotIndex = -1;
    [Header("Fog Of War")]
    [SerializeField] private FogOfWarController fogOfWarController;
    [SerializeField] private Tilemap fogOfWarTilemap;
    [SerializeField] private Tilemap fogOfWarMemoryTilemap;
    [SerializeField] private Tilemap fogOfWarBreakwaterMemoryTilemap;
    [SerializeField] private TileBase fogOfWarOverlayTile;
    [SerializeField] private TerrainDatabase fogOfWarTerrainDatabase;
    [SerializeField] private DPQAirHeightConfig fogOfWarDpqAirHeightConfig;
    [SerializeField, HideInInspector] [Range(0f, 1f)] private float fogOfWarAlpha = 0.65f;
    [SerializeField] private FogOfWarVisionMode fogOfWarVisionMode = FogOfWarVisionMode.All;
    [SerializeField, HideInInspector] private List<FogRoundZeroSlotBake> fogRoundZeroBakes =
        new List<FogRoundZeroSlotBake>();
    [System.NonSerialized] private readonly Dictionary<int, FogOfWarVisionMode> fogVisionModeByPlayerIndex = new Dictionary<int, FogOfWarVisionMode>();
    [Header("Victory Overlay")]
    [SerializeField] private bool showVictoryOverlay = true;
    [SerializeField] private Tilemap victoryOverlayTilemap;
    [SerializeField] private TileBase victoryOverlayTile;
    [SerializeField] [Range(0f, 1f)] private float victoryOverlayAlpha = 1f;
    [SerializeField] private int activePlayerListIndex = 0;
    [SerializeField, HideInInspector] private int appliedActivePlayerListIndex = int.MinValue;
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
    [System.NonSerialized] private readonly HashSet<Vector3Int> fogRenderedVisibleCellsBuffer = new HashSet<Vector3Int>();
    [System.NonSerialized] private bool fogRenderedVisibleCellsValid;
    [System.NonSerialized] private readonly Dictionary<FogContributionSourceId, FogSourceContributionCacheEntry> fogContributionsBySource =
        new Dictionary<FogContributionSourceId, FogSourceContributionCacheEntry>();
    [System.NonSerialized] private readonly Dictionary<FogSpecializedViewCacheKey, HashSet<Vector3Int>> fogSpecializedViewCellsByUnit = new Dictionary<FogSpecializedViewCacheKey, HashSet<Vector3Int>>();
    // Canais distintos: revelar o mapa nao implica detectar um ocupante.
    // Unidades contribuem nos dois; construcoes revelam o raio geograficamente,
    // mas somente o proprio hex possui cobertura de deteccao da construcao.
    [System.NonSerialized] private readonly Dictionary<Vector3Int, int> fogGeographicContributorsByCell = new Dictionary<Vector3Int, int>();
    [System.NonSerialized] private readonly Dictionary<Vector3Int, int> fogSensorContributorsByCell = new Dictionary<Vector3Int, int>();
    [System.NonSerialized] private readonly Dictionary<int, bool> fogUnitVisibilityByCacheIndex = new Dictionary<int, bool>();
    [System.NonSerialized] private readonly HashSet<Vector3Int> fogUnitVisibleScratchBuffer = new HashSet<Vector3Int>();
    private sealed class FogSlotGameplaySnapshot
    {
        public readonly HashSet<Vector3Int> geographicallyVisibleCells = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> sensorCoveredCells = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> knownCells = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> geographicOnlyCells = new HashSet<Vector3Int>();
        public readonly Dictionary<int, bool> unitVisibility = new Dictionary<int, bool>();
    }
    private readonly struct FogUpdateContext
    {
        public readonly PlayerSlotId gameplaySlot;
        public readonly PlayerSlotId observerSlot;
        public readonly PlayerSlotId presentationSlot;
        public readonly bool publishGameplayData;
        public readonly bool publishVisuals;
        public readonly bool recordExplorationMemory;
        public readonly bool recordIntel;

        public FogUpdateContext(
            PlayerSlotId gameplaySlot,
            PlayerSlotId observerSlot,
            PlayerSlotId presentationSlot,
            bool publishGameplayData,
            bool publishVisuals,
            bool recordExplorationMemory,
            bool recordIntel)
        {
            this.gameplaySlot = gameplaySlot;
            this.observerSlot = observerSlot;
            this.presentationSlot = presentationSlot;
            this.publishGameplayData = publishGameplayData;
            this.publishVisuals = publishVisuals;
            this.recordExplorationMemory = recordExplorationMemory;
            this.recordIntel = recordIntel;
        }
    }
    private readonly struct FogObserverScopeState
    {
        public readonly int activeTeamId;
        public readonly int activePlayerListIndex;
        public readonly int presentationGameplayTeamId;
        public readonly FogUpdateContext? updateContext;

        public FogObserverScopeState(
            int activeTeamId,
            int activePlayerListIndex,
            int presentationGameplayTeamId,
            FogUpdateContext? updateContext)
        {
            this.activeTeamId = activeTeamId;
            this.activePlayerListIndex = activePlayerListIndex;
            this.presentationGameplayTeamId = presentationGameplayTeamId;
            this.updateContext = updateContext;
        }
    }
    private sealed class FogSlotContributionRuntime
    {
        public readonly Dictionary<FogContributionSourceId, FogSourceContributionCacheEntry> sources =
            new Dictionary<FogContributionSourceId, FogSourceContributionCacheEntry>();
        public readonly Dictionary<Vector3Int, int> geographicContributors =
            new Dictionary<Vector3Int, int>();
        public readonly Dictionary<Vector3Int, int> sensorContributors =
            new Dictionary<Vector3Int, int>();
    }
    private sealed class FogConstructionMemoryEntry
    {
        public ConstructionData data;
        public TeamId knownOwner;
        public bool flipX;
    }
    [System.NonSerialized] private readonly Dictionary<int, FogSlotGameplaySnapshot> fogGameplaySnapshotsBySlot =
        new Dictionary<int, FogSlotGameplaySnapshot>();
    [System.NonSerialized] private readonly Dictionary<int, FogSlotContributionRuntime> fogContributionRuntimeBySlot =
        new Dictionary<int, FogSlotContributionRuntime>();
    [System.NonSerialized] private readonly Dictionary<int, HashSet<Vector3Int>> fogExploredCellsBySlot =
        new Dictionary<int, HashSet<Vector3Int>>();
    [System.NonSerialized] private readonly Dictionary<int, Dictionary<Vector3Int, FogConstructionMemoryEntry>> fogConstructionMemoryBySlot =
        new Dictionary<int, Dictionary<Vector3Int, FogConstructionMemoryEntry>>();
    [System.NonSerialized] private readonly List<SpriteRenderer> fogConstructionMemoryRenderers =
        new List<SpriteRenderer>();
    [System.NonSerialized] private readonly List<SpriteRenderer> fogStructureMemoryRenderers =
        new List<SpriteRenderer>();
    [System.NonSerialized] private PanelRemainingController fogVisionPanelRemaining;
    [System.NonSerialized] private bool fogSortingLayerValidated;
    [System.NonSerialized] private int fogCachedObserverSlotIndex = int.MinValue;
    [System.NonSerialized] private string lastFogWriteBarrierWarning;
    [System.NonSerialized] private bool fogOverlayInitialized;
    [System.NonSerialized] private int fogRenderedObserverSlotIndex = int.MinValue;
    [System.NonSerialized] private bool initialStealthDetectionBootstrapped;
    [System.NonSerialized] private CommittedBoardDelta pendingCommittedBoardDelta;
    [System.NonSerialized] private bool debugFogOfWarEnabled = true;
    [System.NonSerialized] private bool debugFogOfWarPartial;
    [System.NonSerialized] private bool debugPanelRodadaEnabled = true;
    [System.NonSerialized] private int fogPresentationGameplayTeamId = int.MinValue;
    [System.NonSerialized] private FogUpdateContext? activeFogUpdateContext;
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

    // Orcamento de CPU por frame do warm de FoW. Antes era UMA fonte por frame,
    // e com o custo por fonte em ~10ms o warm passava a maior parte do tempo
    // apenas esperando 63 frames chegarem (medido: cpu=678ms de 4335ms totais).
    //
    // A troca e direta: orcamento maior = menos frames, cada um mais longo.
    // Com ~48ms de custo de frame alheio ao warm e F fontes por frame, o total
    // aproximado e (63/F) * (48 + 10*F) ms -- 1 fonte/frame da ~3,6s; 4 da ~1,4s;
    // 8 da ~1,0s, ao preco de engasgos de ~126ms. 40ms e o meio: ~4 fontes por
    // frame. Aumente se preferir terminar antes, diminua se sentir o engasgo.
    [SerializeField] [Range(8f, 120f)] private float fogWarmupFrameBudgetMs = 40f;
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
    public PlayerSlotId ActiveSlotId => GetPlayerSlotId(activePlayerListIndex);
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

    public PlayerSlotId GetPlayerSlotId(int slotIndex)
    {
        return IsValidPlayerSlotIndex(slotIndex)
            ? PlayerSlotId.FromIndex(slotIndex)
            : PlayerSlotId.Invalid;
    }

    public bool IsValidPlayerSlot(PlayerSlotId slotId)
    {
        return slotId.IsValid && IsValidPlayerSlotIndex(slotId.Value);
    }

    public bool IsValidPlayerSlotIndex(int slotIndex)
    {
        return players != null && slotIndex >= 0 && slotIndex < players.Count;
    }

    /// <summary>
    /// Resolve apenas a aparência configurada para o participante.
    /// TeamId não deve ser usado como identidade ou propriedade.
    /// </summary>
    public TeamId GetVisualTeamForSlot(PlayerSlotId slotId)
    {
        return IsValidPlayerSlot(slotId)
            ? players[slotId.Value].teamId
            : TeamId.Neutral;
    }

    public bool AreAllies(PlayerSlotId first, PlayerSlotId second)
    {
        return IsValidPlayerSlot(first)
            && IsValidPlayerSlot(second)
            && first == second;
    }

    public bool AreEnemies(PlayerSlotId first, PlayerSlotId second)
    {
        return IsValidPlayerSlot(first)
            && IsValidPlayerSlot(second)
            && first != second;
    }

    public bool IsOwnedBySlot(UnitManager unit, PlayerSlotId ownerSlot)
    {
        return unit != null
            && IsValidPlayerSlot(ownerSlot)
            && unit.SlotIndex == ownerSlot.Value;
    }

    public bool IsOwnedBySlot(ConstructionManager construction, PlayerSlotId ownerSlot)
    {
        return construction != null
            && IsValidPlayerSlot(ownerSlot)
            && construction.SlotIndex == ownerSlot.Value;
    }

    public PlayerSlotId GetOwnerSlot(UnitManager unit)
    {
        return unit != null ? GetPlayerSlotId(unit.SlotIndex) : PlayerSlotId.Invalid;
    }

    public PlayerSlotId GetOwnerSlot(ConstructionManager construction)
    {
        return construction != null ? GetPlayerSlotId(construction.SlotIndex) : PlayerSlotId.Invalid;
    }

    /// <summary>
    /// Compatibilidade para dados legados. Falha se nenhuma ou mais de uma vaga
    /// usar a mesma aparência, evitando escolher silenciosamente o primeiro slot.
    /// </summary>
    public bool TryGetUniqueSlotForTeam(TeamId visualTeam, out PlayerSlotId slotId)
    {
        slotId = PlayerSlotId.Invalid;
        if (visualTeam == TeamId.Neutral || players == null)
            return false;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].teamId != visualTeam)
                continue;

            if (slotId.IsValid)
            {
                slotId = PlayerSlotId.Invalid;
                return false;
            }

            slotId = PlayerSlotId.FromIndex(i);
        }

        return slotId.IsValid;
    }

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

    public bool IsPlayerCommandServiceAutomatic(PlayerSlotId slotId)
    {
        return IsValidPlayerSlot(slotId) && players[slotId.Value].commandServiceAutomatic;
    }

    public void SetPlayerCommandServiceAutomatic(PlayerSlotId slotId, bool value)
    {
        if (!IsValidPlayerSlot(slotId))
            return;
        PlayerEntry entry = players[slotId.Value];
        entry.commandServiceAutomatic = value;
        players[slotId.Value] = entry;
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
    public bool IsTurnTransitionInProgress => hotSeatGateActive || advanceTurnTransitionRoutine != null;
    public bool IsHotSeatGateActive => hotSeatGateActive;
    public bool AreTurnStartEffectsPending =>
        pendingTurnStartUpkeep
        || pendingTurnStartEconomy;
    public bool IsFogCacheWarmupInProgress =>
        fogCacheWarmupRoutine != null;
    public bool IsTurnBoardReady =>
        !AreTurnStartEffectsPending &&
        !IsFogCacheWarmupInProgress &&
        (turnStateManager == null ||
            (!turnStateManager.IsAutoCommandServiceBusy &&
             !turnStateManager.IsScannerActionExecutionInProgress &&
             turnStateManager.CurrentCursorState ==
                TurnStateManager.CursorState.Neutral));
    public bool IsTurnBoardReadyForHumanConfirmation() =>
        !IsActiveTeamAI() && IsTurnBoardReady;
    public bool IsTurnPanelPresentationEnabled =>
        debugPanelRodadaEnabled &&
        !DebugManager.IsPanelRodadaDisabledForHotSeat();

    // O load iniciado pela Tela de Entrada substitui a inicializacao normal da
    // cena. Depois que o snapshot foi restaurado, libera o gate provisorio de
    // input criado no Awake; a apresentacao do Panel_Rodada e opcional.
    public void ReleaseHotSeatGateAfterLoad()
    {
        hotSeatGateActive = false;
    }

    public void PrepareFogCachesForTurnPresentation()
    {
        // Saves atuais normalmente restauram o cache por observador. Para
        // snapshots antigos ou incompletos, inicia o complemento ainda atrás do
        // loading, antes que o botão de turno consulte IsTurnBoardReady.
        ScheduleInactiveAiFogCacheWarmup();
    }

    public void CancelFogCacheWarmupForLoad()
    {
        // Invalida tambem uma execucao agendada no mesmo frame do load.
        // A geracao impede que o warmup antigo retome quando a supressao acabar.
        fogCacheWarmupGeneration++;
        if (fogCacheWarmupRoutine != null)
        {
            StopCoroutine(fogCacheWarmupRoutine);
            fogCacheWarmupRoutine = null;
        }
    }
    public bool EnableVictoryStars => enableVictoryStars;
    public int VictoryStarsToWin => ClampVictoryStarsGoal(victoryStarsToWin);
    public bool HasVictoryWinner => hasVictoryWinner;
    public TeamId VictoryWinnerTeam => victoryWinnerTeam;
    public PlayerSlotId VictoryWinnerSlotId => PlayerSlotId.FromIndex(victoryWinnerSlotIndex);
    private Coroutine advanceTurnTransitionRoutine;
    private Coroutine fogCacheWarmupRoutine;
    private int fogCacheWarmupGeneration;

    // Marcado por ImportCursorCell, consumido pelo teleport de virada de turno.
    [System.NonSerialized] private bool suppressNextHeadQuarterCursorFocus;

    // Decomposicao do custo por fonte aquecida. O warm reporta so um total, e
    // 203ms/fonte contra 96-145ms de collect medido deixa ~60-100ms sem dono:
    // pode ser o clone do runtime, o CollectBoardCells ou o proprio collect.
    [System.NonSerialized] private double fogWarmupActivateMs;
    [System.NonSerialized] private double fogWarmupWorkMs;
    [System.NonSerialized] private double fogWarmupStoreMs;
    [System.NonSerialized] private double fogWarmupRestoreMs;
    [System.NonSerialized] private int fogWarmupClonedSources;
    private bool hotSeatGateActive;

    public int GetVictoryStars(PlayerSlotId slotId)
    {
        int index = FindVictoryEntryIndex(slotId.Value);
        if (index < 0)
            return 0;

        return Mathf.Max(0, victoryStarsByTeam[index].stars);
    }

    public void GetVictoryControlForSlot(PlayerSlotId slotId, out int controlled, out int total)
    {
        controlled = 0;
        total = 0;

        if (!IsValidPlayerSlot(slotId))
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
            if (construction.SlotIndex == slotId.Value)
                controlled++;
        }
    }

    public int GetProjectedVictoryStarsGain(PlayerSlotId slotId)
    {
        if (!enableVictoryStars)
            return 0;
        if (!IsValidPlayerSlot(slotId))
            return 0;

        GetVictoryControlForSlot(slotId, out int controlled, out int total);
        if (total <= 0)
            return 0;

        int majorityThreshold = (total / 2) + 1;
        return controlled >= majorityThreshold ? 1 : 0;
    }

    public int GetActualMoney(PlayerSlotId slotId) =>
        IsValidPlayerSlot(slotId) ? Mathf.Max(0, players[slotId.Value].actualMoney) : 0;

    public int GetStartMoney(PlayerSlotId slotId) =>
        IsValidPlayerSlot(slotId) ? Mathf.Max(0, players[slotId.Value].startMoney) : 0;

    public int GetIncomePerTurn(PlayerSlotId slotId) =>
        IsValidPlayerSlot(slotId) ? Mathf.Max(0, players[slotId.Value].incomePerTurn) : 0;

    public bool TrySpendActualMoney(PlayerSlotId slotId, int amount, out int remainingMoney)
    {
        remainingMoney = 0;
        int spend = Mathf.Max(0, amount);
        if (!IsValidPlayerSlot(slotId))
            return false;

        PlayerEntry entry = players[slotId.Value];
        int current = Mathf.Max(0, entry.actualMoney);
        if (current < spend)
        {
            remainingMoney = current;
            return false;
        }

        entry.actualMoney = current - spend;
        players[slotId.Value] = entry;
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

    public bool TrySetActualMoney(PlayerSlotId slotId, int value)
    {
        if (!IsValidPlayerSlot(slotId))
            return false;
        PlayerEntry entry = players[slotId.Value];
        entry.actualMoney = Mathf.Max(0, value);
        players[slotId.Value] = entry;
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

    public void GetSlotUnitCounts(int slotIndex, out int totalInField, out int readyToAct, bool includeEmbarked = true)
    {
        totalInField = 0;
        readyToAct = 0;
        if (players == null || slotIndex < 0 || slotIndex >= players.Count)
            return;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;
            if (unit.SlotIndex != slotIndex)
                continue;
            if (!includeEmbarked && unit.IsEmbarked)
                continue;

            totalInField++;
            if (!unit.HasActed)
                readyToAct++;
        }
    }

    public bool HasReachedMaxUnitsForSlot(PlayerSlotId slotId)
    {
        GetSlotUnitCounts(slotId.Value, out int totalInField, out _);
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
                isLocal = !GetValueOrDefault(isAIs, i, false),
                localityConfigured = true,
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
        List<int> slotIndices,
        List<int> stars,
        out bool enabled,
        out int starsToWin,
        out bool winnerDefined,
        out int winnerSlotIndex)
    {
        enabled = enableVictoryStars;
        starsToWin = ClampVictoryStarsGoal(victoryStarsToWin);
        winnerDefined = hasVictoryWinner;
        winnerSlotIndex = victoryWinnerSlotIndex;

        if (slotIndices == null || stars == null)
            return;

        slotIndices.Clear();
        stars.Clear();
        for (int i = 0; i < victoryStarsByTeam.Count; i++)
        {
            TeamVictoryEntry entry = victoryStarsByTeam[i];
            if (entry.teamId == TeamId.Neutral)
                continue;

            slotIndices.Add(entry.slotIndex);
            stars.Add(Mathf.Max(0, entry.stars));
        }
    }

    public void ImportVictoryStarsState(
        IList<int> slotIndices,
        IList<int> stars,
        bool enabled,
        int starsToWin,
        bool winnerDefined,
        int winnerSlotIndex)
    {
        enableVictoryStars = enabled;
        victoryStarsToWin = ClampVictoryStarsGoal(starsToWin);
        hasVictoryWinner = winnerDefined;
        victoryWinnerSlotIndex = winnerSlotIndex;
        victoryWinnerTeam = winnerSlotIndex >= 0 && players != null && winnerSlotIndex < players.Count
            ? players[winnerSlotIndex].teamId
            : TeamId.Neutral;

        victoryStarsByTeam.Clear();
        int count = slotIndices != null ? slotIndices.Count : 0;
        for (int i = 0; i < count; i++)
        {
            int slotIndex = slotIndices[i];
            if (players == null || slotIndex < 0 || slotIndex >= players.Count)
                continue;
            TeamId team = players[slotIndex].teamId;

            int value = stars != null && i < stars.Count ? Mathf.Max(0, stars[i]) : 0;
            victoryStarsByTeam.Add(new TeamVictoryEntry { slotIndex = slotIndex, teamId = team, stars = value });
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

    public void RefreshPlayerRuntimeFlagsNow()
    {
        RecalculatePlayerRuntimeFlags(GetActiveConstructionsOnScene());
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
        fogVisionModeByPlayerIndex.Clear();
        fogOfWarVisionMode = FogOfWarVisionMode.All;
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
        hotSeatGateActive = Application.isPlaying && IsHotSeatPrivacyRequired();
        TryRefreshIncomeFromConstructions(markDirtyInEditor: false);
        TryAutoAssignCursorController();
        TryAutoAssignTurnStateManager();
        TryAutoAssignTurnTransitionReferences();
        ValidateFogOfWarSortingLayer();
        TryAutoAssignVictoryOverlayReferences();
        TryRefreshVictoryOverlayFromConstructions(markDirtyInEditor: false);
        if (enableTotalWar)
            TryAutoAssignFogOfWarReferences();
        if (Application.isPlaying)
        {
            // Delay first team apply/FoW refresh to Start so all scene objects had OnEnable.
            appliedActivePlayerListIndex = activePlayerListIndex;
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
            StartCoroutine(InitializeMatchAfterHotSeatGate());
        else
        {
            TryBootstrapInitialStealthDetection();
            RunTurnStartStillObservedForActiveTeamStealthUnits();
        }
    }

    private IEnumerator InitializeMatchAfterHotSeatGate()
    {
        // Ao entrar numa cena para carregar um save do menu, o SaveGameManager
        // assume o PanelRodada e restaura o snapshot. Nao inicializa uma partida
        // nova em paralelo, pois isso aplicaria turno/FOW/camera antes do load.
        if (SaveGameManager.HasPendingMainMenuLoadRequest)
            yield break;

        if (capturedBuildingHistory == null)
            capturedBuildingHistory = new List<TeamCapturedBuildingHistory>();
        else
            capturedBuildingHistory.Clear();
        RegisterCurrentlyOwnedBuildings();

        // O painel e somente uma cortina visual/de input. A verdade confirmada do
        // turno e inicializada por ApplyActiveTeamIfChanged mesmo quando a
        // apresentacao estiver desativada ou aguardando confirmacao.
        if (panelRodada == null)
            panelRodada = FindAnyObjectByType<PanelRodadaController>(FindObjectsInactive.Include);
        bool requiresLocalPrivacy = IsHotSeatPrivacyRequired();
        bool useHotSeatPanel =
            requiresLocalPrivacy &&
            panelRodada != null &&
            IsTurnPanelPresentationEnabled;
        bool useFogWarmupLoading =
            useHotSeatPanel &&
            debugFogOfWarEnabled &&
            enableTotalWar;
        if (useFogWarmupLoading)
        {
            // O loading nao reproduz o video: ele pode ficar estatico enquanto
            // o main thread constroi os primeiros caches FOW dos slots AI.
            panelRodada.BeginLoadingPresentation();
            panelRodada.SetLoadingTeam(ActiveTeam, currentTurn);
            yield return null;
        }
        else if (useHotSeatPanel)
        {
            panelRodada.CoverImmediatelyForPrivateTurnTransition();
            // Permite que a cortina seja realmente renderizada antes do primeiro
            // ApplyActiveTeam/FOW da cena.
            yield return null;
        }
        else if (panelRodada != null &&
            (panelRodada.IsPresenting || panelRodada.gameObject.activeInHierarchy))
            panelRodada.CancelLoadingPresentation();

        FindAnyObjectByType<ReplayManager>()?.CleanupReplayArtifactsForMatchStart();
        RecomputeTeamFlips();
        ResetUnfundedStartMoneyFlagsForFreshMatch();
        RestoreRoundZeroFogBakesForRuntime();
        ApplyActiveTeamIfChanged(force: true);
        // Neste ponto todos os objetos da cena ja passaram por OnEnable.
        // Reaplica SFX nos presets sem FOW Total caso o cursor ainda nao
        // estivesse disponivel durante o Awake/ApplyGameSetupPreset.
        ValidateFogOfWarSortingLayer();
        // AI vs AI possui um observador, nao uma perspectiva de debug parcial.
        // Mantem o mesmo FOW Total usado pelo jogador humano; apenas a apresentacao
        // temporaria da acao ativa e promovida acima da nevoa.
        if (AreAllPlayerSlotsAI())
            SetFogOfWarDebugEnabled(true);
        TryAutoAssignTurnTransitionReferences();

        if (useHotSeatPanel)
        {
            if (ShouldUseHotSeatPrivacyCurtain())
            {
                panelRodada.ShowPrivacyCurtain(
                    ActiveTeam,
                    currentTurn,
                    IsActiveTeamAI());
            }
            else
            {
                if (useFogWarmupLoading)
                {
                    // ReleaseLoadingPresentation mantem o texto de loading e so
                    // inicia video/botao depois da barreira de prontidao.
                    yield return panelRodada.ReleaseLoadingPresentation(
                        ActiveTeam,
                        activePlayerListIndex + 1,
                        currentTurn,
                        isBoardReady: IsTurnBoardReadyForHumanConfirmation);
                }
                else
                {
                    yield return panelRodada.Apresentar(
                        ActiveTeam,
                        activePlayerListIndex + 1,
                        currentTurn,
                        IsTurnBoardReadyForHumanConfirmation);
                }
            }
        }

        hotSeatGateActive = false;
        matchMusicAudioManager?.PrepareForMatchStart(forceRestartPlayback: true);

        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "Panel_endGame" && go.scene.name != null)
            {
                go.SetActive(false);
                break;
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
        int expectedObserverSlot = ActiveSlotId.Value;
        if (TryResolveFogPresentationSlot(out PlayerSlotId presentationSlot))
            expectedObserverSlot = presentationSlot.Value;
        if (fogCachedObserverSlotIndex == expectedObserverSlot && fogOverlayInitialized)
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
        if (activePlayerListIndex >= 0)
            fogVisionModeByPlayerIndex[activePlayerListIndex] = mode;
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

    public bool IsFogOfWarVisionModeAvailable(FogOfWarVisionMode mode)
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

    public void SetPlayerIsAI(PlayerSlotId slotId, bool isAI)
    {
        if (!IsValidPlayerSlot(slotId))
            return;
        PlayerEntry entry = players[slotId.Value];
        bool wasAI = entry.isAI;
        entry.isAI = isAI;
        if (isAI)
            entry.isLocal = false;
        else if (wasAI)
            entry.isLocal = true;
        entry.localityConfigured = true;
        players[slotId.Value] = entry;
    }

    public bool IsPlayerAI(PlayerSlotId slotId) =>
        IsValidPlayerSlot(slotId) && players[slotId.Value].isAI;

    public bool IsSlotRebel(PlayerSlotId slotId)
    {
        if (!IsValidPlayerSlot(slotId))
            return false;

        // Mantem a consulta correta mesmo quando chamada imediatamente apos load,
        // captura ou edicao, antes do proximo refresh periodico da economia.
        RecalculatePlayerRuntimeFlags(GetActiveConstructionsOnScene());
        return players[slotId.Value].isRebelRuntime;
    }

    public void SetPlayerIsLocal(PlayerSlotId slotId, bool isLocal)
    {
        if (!IsValidPlayerSlot(slotId))
            return;

        PlayerEntry entry = players[slotId.Value];
        entry.isLocal = !entry.isAI && isLocal;
        entry.localityConfigured = true;
        players[slotId.Value] = entry;
    }

    public bool IsPlayerLocal(PlayerSlotId slotId) =>
        IsValidPlayerSlot(slotId) &&
        !players[slotId.Value].isAI &&
        players[slotId.Value].isLocal;

    public bool TryExportCursorCell(out Vector3Int cell)
    {
        cell = default;
        if (cursorController == null)
            return false;

        cell = cursorController.CurrentCell;
        cell.z = 0;
        return true;
    }

    /// <summary>
    /// Restaura o cursor de um save e cancela o enquadramento de QG que viria em
    /// seguida — o load tem uma posicao autoritativa, e o teleport de virada de
    /// turno a sobrescreveria.
    ///
    /// A celula do cursor e estado logico e sempre restaura. Mover a CAMERA para
    /// la e apresentacao, e obedece a mesma politica do enquadramento de QG: num
    /// save carregado no turno de uma AI (ou de um humano remoto) sob sigilo, a
    /// posicao volta mas a camera nao vai atras revelar onde ela estava.
    /// </summary>
    public void ImportCursorCell(Vector3Int cell)
    {
        suppressNextHeadQuarterCursorFocus = true;
        if (cursorController == null)
            return;

        cell.z = 0;
        if (!cursorController.SetCell(cell, playMoveSfx: false, adjustCamera: false))
            return;

        if (ShouldFocusCameraOnActiveHeadQuarter())
            cursorController.FocusCameraOnCursor(instant: true);
    }

    public int CountActiveLocalHumanPlayers()
    {
        int count = 0;
        if (players == null)
            return count;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry player = players[i];
            if (!player.defeated && !player.isAI && player.isLocal)
                count++;
        }
        return count;
    }

    public bool IsHotSeatPrivacyRequired() =>
        CountActiveLocalHumanPlayers() >= 2;

    public bool ShouldUseHotSeatPrivacyCurtain() =>
        IsTurnPanelPresentationEnabled &&
        IsHotSeatPrivacyRequired() &&
        (!IsValidPlayerSlot(ActiveSlotId) || !IsPlayerLocal(ActiveSlotId));

    public bool TryGetSingleActiveLocalHumanSlot(out PlayerSlotId slot)
    {
        slot = PlayerSlotId.Invalid;
        if (players == null)
            return false;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry player = players[i];
            if (player.defeated || player.isAI || !player.isLocal)
                continue;
            if (slot.IsValid)
            {
                slot = PlayerSlotId.Invalid;
                return false;
            }
            slot = PlayerSlotId.FromIndex(i);
        }
        return slot.IsValid;
    }

    // Verifica se o time ATUALMENTE ativo e IA, usando o slot index diretamente.
    // Usa a identidade real do participante, sem lookup por cor.
    public bool IsActiveTeamAI()
    {
        if (players == null || activePlayerListIndex < 0 || activePlayerListIndex >= players.Count)
            return false;
        return players[activePlayerListIndex].isAI;
    }

    // Bloqueio central de input humano durante o turno de um time controlado por IA.
    public bool IsPlayerInputLockedByActiveAI()
    {
        return Application.isPlaying && (hotSeatGateActive || (IsActiveTeamAI() && !AIController.IsDebugPaused));
    }

    public int GetAvailableFogOfWarVisionModes(List<FogOfWarVisionMode> output)
    {
        if (output == null)
            return 0;

        output.Clear();
        FogOfWarVisionMode[] modes =
        {
            FogOfWarVisionMode.All,
            FogOfWarVisionMode.Air,
            FogOfWarVisionMode.Surface,
            FogOfWarVisionMode.Sub
        };
        for (int i = 0; i < modes.Length; i++)
            if (IsFogOfWarVisionModeAvailable(modes[i]))
                output.Add(modes[i]);
        return output.Count;
    }

    public bool AreAllPlayersHuman()
    {
        if (players == null || players.Count < 2) return false;
        for (int i = 0; i < players.Count; i++)
            if (players[i].isAI) return false;
        return true;
    }

    public bool IsSlotDefeated(PlayerSlotId slotId) =>
        IsValidPlayerSlot(slotId) && players[slotId.Value].defeated;

    public void SetActiveTeamId(int teamId)
    {
        activeTeamId = Mathf.Clamp(teamId, -1, 3);
        SyncActivePlayerIndexFromActiveTeam();
        ApplyActiveTeamIfChanged(force: false);
    }

    public bool SetActiveSlot(PlayerSlotId slotId)
    {
        if (!IsValidPlayerSlot(slotId))
            return false;

        SetActivePlayerByIndex(slotId.Value);
        return true;
    }

    public bool SetActiveSlotWithoutTurnStart(PlayerSlotId slotId)
    {
        if (!IsValidPlayerSlot(slotId))
            return false;

        activePlayerListIndex = slotId.Value;
        activeTeamId = (int)players[slotId.Value].teamId;
        ApplyActiveTeamIfChanged(force: false, applyTurnStartEffects: false);
        return true;
    }

    // Usado apos load: garante que OnActiveTeamChanged dispare mesmo que o time ativo seja o mesmo de antes.
    // Usa applyTurnStartEffects: false para nao reprocessar economia/upkeep que ja foram restaurados do save.
    public void ForceReapplyActiveTeam()
    {
        appliedActivePlayerListIndex = int.MinValue;
        appliedActiveTeamId = int.MinValue;
        ApplyActiveTeamIfChanged(force: false, applyTurnStartEffects: false);
    }

    // Versao para load: aplica efeitos de inicio de turno.
    public void ForceReapplyActiveTeamWithTurnStart()
    {
        appliedActivePlayerListIndex = int.MinValue;
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
        // Defesa contra qualquer preview de movimento que tenha sido interrompido:
        // nenhuma unidade pode carregar a layer temporaria de FoW para outro turno.
        UnitManager[] unitsBeforeTurnAdvance = FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < unitsBeforeTurnAdvance.Length; i++)
            unitsBeforeTurnAdvance[i]?.EndTemporaryFogTraversalVisual();
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

        if (panelRodada == null)
            panelRodada = FindAnyObjectByType<PanelRodadaController>(FindObjectsInactive.Include);

        // O painel preto sobe antes de trocar time/FoW/camera, preservando o hot seat.
        bool useHotSeatPanel =
            IsHotSeatPrivacyRequired() &&
            panelRodada != null &&
            IsTurnPanelPresentationEnabled;
        if (!useHotSeatPanel &&
            panelRodada != null &&
            panelRodada.IsPresenting)
            panelRodada.CancelLoadingPresentation();
        if (useHotSeatPanel)
        {
            panelRodada.CoverImmediatelyForPrivateTurnTransition();
            // CoverImmediately atualiza o Canvas, mas sem devolver um frame ao
            // renderer o custo sincrono de AdvanceTurn ainda pode acontecer antes
            // de a cortina chegar à tela. O painel continua sendo apenas cobertura:
            // a transicao e a IA nao dependem dele para funcionar.
            yield return null;
        }

        double advanceTurnStartMs = TurnPerfNowMs();
        AdvanceTurn();
        TurnPerfLog("AdvanceTurn", advanceTurnStartMs);

        if (useHotSeatPanel && !hasVictoryWinner)
        {
            if (ShouldUseHotSeatPrivacyCurtain())
            {
                panelRodada.ShowPrivacyCurtain(
                    ActiveTeam,
                    currentTurn,
                    IsActiveTeamAI());
                Debug.Log(
                    $"[PrivacyCurtain] hold activeSlot={ActiveSlotId.Value} " +
                    $"localHumans={CountActiveLocalHumanPlayers()}");
            }
            else
            {
                yield return panelRodada.Apresentar(
                    ActiveTeam,
                    activePlayerListIndex + 1,
                    currentTurn,
                    IsTurnBoardReadyForHumanConfirmation);
                Debug.Log(
                    $"[PrivacyCurtain] exit confirmedSlot={ActiveSlotId.Value}");
            }
        }

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

        PlayerSlotId activeSlot = ActiveSlotId;
        if (!IsValidPlayerSlot(activeSlot))
            return;
        TeamId activeTeam = GetVisualTeamForSlot(activeSlot);

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
            if (construction.SlotIndex == activeSlot.Value)
                activeTeamControlledVictoryBuildings++;
        }

        if (totalVictoryBuildings <= 0)
            return;

        int majorityThreshold = (totalVictoryBuildings / 2) + 1;
        if (activeTeamControlledVictoryBuildings < majorityThreshold)
            return;

        int winnerEntryIndex = FindVictoryEntryIndex(activeSlot.Value);
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
        victoryWinnerSlotIndex = activeSlot.Value;
        victoryWinnerTeam = activeTeam;
        HandleVictoryAestheticPresentation(activeTeam, TeamId.Neutral, VictoryReason.VictoryStars);
    }

    public void DeclareTutorialVictory(TutorialData tutorial = null)
    {
        if (hasVictoryWinner) return;

        TeamId winnerTeam = GetTeamIdForSlot(0);
        hasVictoryWinner = true;
        victoryWinnerSlotIndex = 0;
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

    private bool TryDefeatSlotIfZeroUnits(PlayerSlotId slotId)
    {
        if (!IsValidPlayerSlot(slotId))
            return false;
        int playerIndex = slotId.Value;
        TeamId team = players[playerIndex].teamId;
        if (players[playerIndex].defeated)
            return false;
        // Derrota por 0 unidades so vale para slots com QG. Uma faccao rebelde (sem QG) some do
        // tabuleiro sem encerrar a partida e sem neutralizar o que ja tinha capturado.
        if (ConstructionManager.IsHeadQuarterlessTeam(team))
            return false;

        List<UnitManager> allUnits = GetActiveUnitsOnScene();
        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitManager unit = allUnits[i];
            if (unit != null && unit.SlotIndex == playerIndex)
                return false;
        }

        PlayerEntry entry = players[playerIndex];
        entry.defeated = true;
        players[playerIndex] = entry;

        NeutralizeConstructionsOwnedBySlot(slotId);
        Debug.Log($"[Match] Slot {playerIndex} ({TeamUtils.GetName(team)}) derrotado (0 unidades). Construcoes neutralizadas.");
        OnTeamDefeated?.Invoke(team);
        return true;
    }

    // Chamado do ponto unico de conclusao de captura (ExecuteCaptureSequence), valido tanto para o
    // jogador humano quanto para a IA (que captura pelo mesmo caminho via Automation ->
    // HandleCaptureActionRequested). Se o alvo for um QG, o antigo dono e eliminado na hora e o
    // capturador vence imediatamente (mesmo com outros jogadores/IA ainda em jogo).
    public void NotifyConstructionCaptured(ConstructionManager construction, int previousOwnerSlot, int newOwnerSlot, TeamId previousOwner, TeamId newOwner)
    {
        if (Application.isPlaying && construction != null && newOwnerSlot >= 0 && previousOwnerSlot != newOwnerSlot &&
            construction.TryResolveConstructionData(out ConstructionData capturedData))
        {
            RegisterCapturedBuilding(PlayerSlotId.FromIndex(newOwnerSlot), capturedData);
        }

        if (!Application.isPlaying || hasVictoryWinner)
            return;
        if (!allowDefeatForHeadQuarterCapture)
            return;
        if (construction == null || !IsHeadQuarterConstruction(construction))
            return;
        if (previousOwnerSlot < 0 || previousOwnerSlot == newOwnerSlot)
            return;

        if (!MarkSlotDefeated(PlayerSlotId.FromIndex(previousOwnerSlot), "QG capturado"))
            return;

        Debug.Log($"[Match] QG de {TeamUtils.GetName(previousOwner)} capturado por {TeamUtils.GetName(newOwner)}. Time eliminado.");
        // O primeiro a capturar um QG vence na hora, mesmo com outros jogadores em jogo.
        DeclareEliminationVictory(
            PlayerSlotId.FromIndex(newOwnerSlot),
            PlayerSlotId.FromIndex(previousOwnerSlot),
            VictoryReason.HeadQuarterCaptured);
    }

    public bool HasCapturedBuilding(PlayerSlotId slotId, ConstructionData building)
    {
        string key = ResolveProgressionBuildingKey(building);
        if (!IsValidPlayerSlot(slotId) || string.IsNullOrWhiteSpace(key))
            return false;

        for (int i = 0; capturedBuildingHistory != null && i < capturedBuildingHistory.Count; i++)
        {
            TeamCapturedBuildingHistory entry = capturedBuildingHistory[i];
            if (entry == null || entry.slotIndex != slotId.Value || entry.buildingKeys == null)
                continue;
            for (int k = 0; k < entry.buildingKeys.Count; k++)
                if (string.Equals(entry.buildingKeys[k], key, StringComparison.OrdinalIgnoreCase))
                    return true;
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager owned = constructions[i];
            if (owned == null || owned.SlotIndex != slotId.Value || !owned.TryResolveConstructionData(out ConstructionData ownedData))
                continue;
            if (!string.Equals(ResolveProgressionBuildingKey(ownedData), key, StringComparison.OrdinalIgnoreCase))
                continue;
            RegisterCapturedBuilding(slotId, ownedData);
            return true;
        }
        return false;
    }

    public void RegisterCurrentlyOwnedBuildings()
    {
        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || construction.TeamId == TeamId.Neutral ||
                !construction.TryResolveConstructionData(out ConstructionData data))
            {
                continue;
            }

            RegisterCapturedBuilding(PlayerSlotId.FromIndex(construction.SlotIndex), data);
        }
    }

    private void RegisterCapturedBuilding(TeamId team, ConstructionData building)
    {
        string key = ResolveProgressionBuildingKey(building);
        if (team == TeamId.Neutral || string.IsNullOrWhiteSpace(key))
            return;
        if (capturedBuildingHistory == null)
            capturedBuildingHistory = new List<TeamCapturedBuildingHistory>();

        TeamCapturedBuildingHistory teamHistory = null;
        for (int i = 0; i < capturedBuildingHistory.Count; i++)
        {
            if (capturedBuildingHistory[i] != null && capturedBuildingHistory[i].teamId == team)
            {
                teamHistory = capturedBuildingHistory[i];
                break;
            }
        }

        if (teamHistory == null)
        {
            teamHistory = new TeamCapturedBuildingHistory { teamId = team };
            capturedBuildingHistory.Add(teamHistory);
        }
        if (teamHistory.buildingKeys == null)
            teamHistory.buildingKeys = new List<string>();
        for (int i = 0; i < teamHistory.buildingKeys.Count; i++)
            if (string.Equals(teamHistory.buildingKeys[i], key, StringComparison.OrdinalIgnoreCase))
                return;
        teamHistory.buildingKeys.Add(key);
    }

    private void RegisterCapturedBuilding(PlayerSlotId slotId, ConstructionData building)
    {
        string key = ResolveProgressionBuildingKey(building);
        if (!IsValidPlayerSlot(slotId) || string.IsNullOrWhiteSpace(key))
            return;
        capturedBuildingHistory ??= new List<TeamCapturedBuildingHistory>();

        TeamCapturedBuildingHistory history = null;
        for (int i = 0; i < capturedBuildingHistory.Count; i++)
            if (capturedBuildingHistory[i] != null && capturedBuildingHistory[i].slotIndex == slotId.Value)
            {
                history = capturedBuildingHistory[i];
                break;
            }

        if (history == null)
        {
            history = new TeamCapturedBuildingHistory
            {
                slotIndex = slotId.Value,
                teamId = GetVisualTeamForSlot(slotId)
            };
            capturedBuildingHistory.Add(history);
        }
        history.buildingKeys ??= new List<string>();
        for (int i = 0; i < history.buildingKeys.Count; i++)
            if (string.Equals(history.buildingKeys[i], key, StringComparison.OrdinalIgnoreCase))
                return;
        history.buildingKeys.Add(key);
    }

    public void ExportCapturedBuildingHistory(List<TeamCapturedBuildingSaveData> destination)
    {
        if (destination == null)
            return;
        destination.Clear();
        if (capturedBuildingHistory == null)
            return;
        for (int i = 0; i < capturedBuildingHistory.Count; i++)
        {
            TeamCapturedBuildingHistory source = capturedBuildingHistory[i];
            if (source == null)
                continue;
            destination.Add(new TeamCapturedBuildingSaveData
            {
                slotIndex = source.slotIndex,
                teamId = (int)source.teamId,
                buildingKeys = source.buildingKeys != null ? new List<string>(source.buildingKeys) : new List<string>()
            });
        }
    }

    public void ImportCapturedBuildingHistory(IList<TeamCapturedBuildingSaveData> source)
    {
        if (capturedBuildingHistory == null)
            capturedBuildingHistory = new List<TeamCapturedBuildingHistory>();
        else
            capturedBuildingHistory.Clear();
        if (source == null)
            return;
        for (int i = 0; i < source.Count; i++)
        {
            TeamCapturedBuildingSaveData saved = source[i];
            if (saved == null || !Enum.IsDefined(typeof(TeamId), saved.teamId) || (TeamId)saved.teamId == TeamId.Neutral)
                continue;
            capturedBuildingHistory.Add(new TeamCapturedBuildingHistory
            {
                slotIndex = saved.slotIndex >= 0
                    ? saved.slotIndex
                    : (TryGetUniqueSlotForTeam((TeamId)saved.teamId, out PlayerSlotId migratedSlot) ? migratedSlot.Value : -1),
                teamId = (TeamId)saved.teamId,
                buildingKeys = saved.buildingKeys != null ? new List<string>(saved.buildingKeys) : new List<string>()
            });
        }
    }

    private static string ResolveProgressionBuildingKey(ConstructionData building)
    {
        return building != null ? building.name.Trim() : string.Empty;
    }

    private static string ResolveProgressionBuildingName(ConstructionData building)
    {
        if (building == null)
            return "a construcao necessaria";
        return !string.IsNullOrWhiteSpace(building.displayName) ? building.displayName.Trim() : building.name;
    }

    // Marca um time como derrotado por qualquer condicao (QG capturado, rendicao, etc.): neutraliza
    // suas construcoes e dispara OnTeamDefeated. A checagem de 0 unidades tem seu proprio caminho
    // (TryDefeatTeamIfZeroUnits) por causa do pre-requisito de contagem de unidades.
    private bool MarkSlotDefeated(PlayerSlotId slotId, string reasonLabel)
    {
        if (!IsValidPlayerSlot(slotId))
            return false;
        int playerIndex = slotId.Value;
        TeamId team = players[playerIndex].teamId;
        if (players[playerIndex].defeated)
            return false;

        PlayerEntry entry = players[playerIndex];
        entry.defeated = true;
        players[playerIndex] = entry;

        NeutralizeConstructionsOwnedBySlot(slotId);
        Debug.Log($"[Match] Slot {playerIndex} ({TeamUtils.GetName(team)}) derrotado ({reasonLabel}). Construcoes neutralizadas.");
        OnTeamDefeated?.Invoke(team);
        return true;
    }

    private void NeutralizeConstructionsOwnedBySlot(PlayerSlotId defeatedSlot)
    {
        List<ConstructionManager> constructions = GetActiveConstructionsOnScene();
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || construction.SlotIndex != defeatedSlot.Value)
                continue;

            construction.SetOwnerSlot(-1);
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

        int winnerSlot = -1;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].defeated)
            {
                winnerSlot = i;
                break;
            }
        }
        if (winnerSlot < 0)
            return false;

        hasVictoryWinner = true;
        victoryWinnerSlotIndex = winnerSlot;
        TeamId winner = players[winnerSlot].teamId;
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
        victoryWinnerSlotIndex = TryGetUniqueSlotForTeam(winnerTeam, out PlayerSlotId winnerSlot)
            ? winnerSlot.Value
            : -1;
        victoryWinnerTeam = winnerTeam;
        HandleVictoryAestheticPresentation(winnerTeam, defeatedTeam, reason);
        return true;
    }

    // Time controlado por humano (nao IA, nao neutro).
    private bool IsHumanSlot(PlayerSlotId slotId)
    {
        return IsValidPlayerSlot(slotId) && !IsPlayerAI(slotId);
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

        PlayerSlotId defeatedSlot = PlayerSlotId.FromIndex(unit.SlotIndex);
        if (TryDefeatSlotIfZeroUnits(defeatedSlot))
        {
            // O primeiro a destruir por completo o exercito de um jogador vence na hora.
            PlayerSlotId winnerSlot = ActiveSlotId != defeatedSlot ? ActiveSlotId : ResolveFirstAliveOpponentSlot(defeatedSlot);
            DeclareEliminationVictory(winnerSlot, defeatedSlot, VictoryReason.ArmyEliminated);
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
            if (!entry.localityConfigured)
            {
                // Cenas anteriores a isLocal: todo humano era implicitamente local.
                entry.isLocal = !entry.isAI;
                entry.localityConfigured = true;
            }
            if (entry.isAI)
                entry.isLocal = false;
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
        List<TeamVictoryEntry> normalized = new List<TeamVictoryEntry>();
        if (players != null)
        {
            for (int slotIndex = 0; slotIndex < players.Count; slotIndex++)
            {
                int stars = 0;
                int existingIndex = FindVictoryEntryIndex(slotIndex);
                if (existingIndex >= 0)
                    stars = Mathf.Max(0, victoryStarsByTeam[existingIndex].stars);
                else
                {
                    for (int legacyIndex = 0; legacyIndex < victoryStarsByTeam.Count; legacyIndex++)
                    {
                        TeamVictoryEntry legacy = victoryStarsByTeam[legacyIndex];
                        if (legacy.slotIndex < 0 && legacy.teamId == players[slotIndex].teamId)
                        {
                            stars = Mathf.Max(0, legacy.stars);
                            break;
                        }
                    }
                }

                normalized.Add(new TeamVictoryEntry
                {
                    slotIndex = slotIndex,
                    teamId = players[slotIndex].teamId,
                    stars = stars
                });
            }
        }
        victoryStarsByTeam = normalized;

        if (hasVictoryWinner)
        {
            if (victoryWinnerSlotIndex < 0 || players == null || victoryWinnerSlotIndex >= players.Count)
            {
                if (TryGetUniqueSlotForTeam(victoryWinnerTeam, out PlayerSlotId migratedWinner))
                    victoryWinnerSlotIndex = migratedWinner.Value;
                else
                {
                    hasVictoryWinner = false;
                    victoryWinnerTeam = TeamId.Neutral;
                    victoryWinnerSlotIndex = -1;
                }
            }
        }
    }

    private int FindVictoryEntryIndex(int slotIndex)
    {
        if (victoryStarsByTeam == null)
            return -1;

        for (int i = 0; i < victoryStarsByTeam.Count; i++)
        {
            if (victoryStarsByTeam[i].slotIndex == slotIndex)
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
        bool humanLost = (winnerTeam == TeamId.Neutral ||
                          !IsHumanSlot(PlayerSlotId.FromIndex(victoryWinnerSlotIndex))) &&
                         AnyHumanPlayerExists();

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
        fogOfWarVisionMode = fogVisionModeByPlayerIndex.TryGetValue(index, out FogOfWarVisionMode savedMode)
            && IsFogOfWarVisionModeAvailable(savedMode)
            ? savedMode
            : FogOfWarVisionMode.All;
        UpdateFogOfWarVisionModePanel(fogOfWarVisionMode);
        ApplyActiveTeamIfChanged(force: forceApply);
    }

    private void SetNeutralActiveTeam()
    {
        activePlayerListIndex = -1;
        activeTeamId = (int)TeamId.Neutral;
        fogOfWarVisionMode = FogOfWarVisionMode.All;
        UpdateFogOfWarVisionModePanel(fogOfWarVisionMode);
        ApplyActiveTeamIfChanged(force: false);
    }

    private void ApplyActiveTeamIfChanged(bool force, bool applyTurnStartEffects = true)
    {
        if (!force && appliedActivePlayerListIndex == activePlayerListIndex)
            return;

        if (activePlayerListIndex >= 0)
        {
            if (!IsValidPlayerSlotIndex(activePlayerListIndex) || players[activePlayerListIndex].defeated)
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
        PlayerSlotId previousSlot = GetPlayerSlotId(appliedActivePlayerListIndex);
        PlayerSlotId nextSlot = GetPlayerSlotId(activePlayerListIndex);
        appliedActivePlayerListIndex = activePlayerListIndex;
        appliedActiveTeamId = activeTeamId;

        double stageStartMs = TurnPerfNowMs();
        if (Application.isPlaying)
        {
            UnitManager.ResetActiveTeamChangedPerfCounters();
            ConstructionManager.ResetActiveTeamChangedPerfCounters();
            OnActiveSlotChanged?.Invoke(previousSlot, nextSlot);
            OnActiveTeamChanged?.Invoke(activeTeamId);
            if (enableTurnPerfLogs)
            {
                UnitManager.GetActiveTeamChangedPerfCounters(out int unitHandlerCount, out double unitHandlerMs);
                ConstructionManager.GetActiveTeamChangedPerfCounters(out int constructionHandlerCount, out double constructionHandlerMs);
                Debug.Log($"[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count={unitHandlerCount} ms={unitHandlerMs:F3}");
                Debug.Log($"[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count={constructionHandlerCount} ms={constructionHandlerMs:F3}");
            }
        }
        TurnPerfLog("ApplyActiveTeam.OnActiveSlotChanged", stageStartMs);

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
        if (SuppressFogOfWarRefresh)
        {
            // Durante o load, a troca de slot ainda precisa notificar os
            // sistemas e atualizar o cursor, mas o snapshot salvo continua
            // sendo a autoridade do FOW. Recalcular aqui seria provisório e
            // seria descartado poucos instantes depois pela restauração.
            TurnPerfLog("ApplyActiveTeam.FogAndVisibility.Suppressed", stageStartMs);
        }
        else if (!debugFogOfWarEnabled)
        {
            ResetFogOfWarRuntime(clearTilemap: true);
            ShowAllUnitsIgnoringFog();
            FlushTurnStartAutonomyHelper();
            if (applyTurnStartEffects)
                turnStateManager?.StartPendingTurnStartQueues();
            TurnPerfLog("ApplyActiveTeam.FogDisabled", stageStartMs);
            TurnPerfLog("ApplyActiveTeam.Total", totalStartMs);
            return;
        }

        else if (enableTotalWar)
        {
            if (Application.isPlaying)
            {
                if (!TryRefreshFogOfWarForTurnStartIncremental())
                {
                    if (enableFogStepPerfLogs)
                        Debug.Log($"[FoW][TurnStartCache] slot={ActiveSlotId.Value} fallback=full");
                    RefreshFogOfWarForActiveTeam();
                }
                if (!ShouldUseHotSeatPrivacyCurtain())
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
            if (!ShouldUseHotSeatPrivacyCurtain())
                RefreshRuntimeUnitFogVisibility();
        }
        if (!SuppressFogOfWarRefresh)
            TurnPerfLog("ApplyActiveTeam.FogAndVisibility", stageStartMs);

        stageStartMs = TurnPerfNowMs();
        FlushTurnStartAutonomyHelper();
        TurnPerfLog("ApplyActiveTeam.FlushTurnStartAutonomyHelper", stageStartMs);

        // Filas de inicio de turno so arrancam AQUI, com o FoW ja publicado para o
        // slot certo. Antes elas subiam dentro do upkeep e a guarda de Neutral
        // rejeitava o refresh que viria em seguida.
        if (applyTurnStartEffects)
            turnStateManager?.StartPendingTurnStartQueues();

        TurnPerfLog("ApplyActiveTeam.Total", totalStartMs);

        // O primeiro snapshot de um slot AI nao deve ser construído na troca de
        // turno. Durante um turno humano neutro, aquece somente as contribuicoes
        // internas da IA, sem publicar FOW, memoria, intel ou visuais.
        ScheduleInactiveAiFogCacheWarmup();
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

    // Emersao/camada forcada pendente (ver TurnStateManager.ApplyPendingForcedLayerLock):
    // no inicio do turno do dono tenta aplicar a camada travada, caso o hex tenha
    // liberado (ex.: navio saiu de cima do submarino). Retorna true quando aplicou
    // agora — nesse turno o tempo do lock ainda nao conta.
    private bool TryApplyPendingForcedLayerAtTurnStart(UnitManager unit)
    {
        if (unit == null || unit.IsEmbarked || !unit.HasPendingForcedLayerLock)
            return false;
        if (!unit.TryGetForcedLayerLock(out Domain lockDomain, out HeightLevel lockHeight, out _))
            return false;

        Tilemap boardMap = unit.BoardTilemap != null ? unit.BoardTilemap : ResolveFogBoardTilemap();
        if (boardMap == null)
            return false;

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        if (!LayerTransitionRules.CanUseLayerModeAtCell(
                unit, boardMap, ResolveFogTerrainDatabase(), cell, lockDomain, lockHeight, out _))
        {
            return false;
        }

        if (!unit.TrySetCurrentLayerMode(lockDomain, lockHeight))
            return false;

        if (turnStateManager != null && turnStateManager.ShowMovementLogs)
            Debug.Log($"[LayerForce] Upkeep aplicou camada pendente: {unit.name} -> {lockDomain}/{lockHeight} em ({cell.x},{cell.y})");
        // Jornal do Comandante: o dono precisa saber que a camada mudou sozinha.
        ReportTurnBriefingEvent(
            PlayerSlotId.FromIndex(unit.SlotIndex),
            TurnBriefingCategory.ForcedSurfaceApplied,
            ResolveRuntimeUnitName(unit),
            $"camada aplicada automaticamente: {lockDomain}/{lockHeight}",
            cell);
        return true;
    }

    private void ReleaseUnitsForActiveTeam(List<ConstructionManager> activeConstructions = null)
    {
        if (!Application.isPlaying)
            return;
        PlayerSlotId activeSlot = ActiveSlotId;
        if (!activeSlot.IsValid && !includeNeutralTeam)
            return;

        double stageStartMs = TurnPerfNowMs();
        activeConstructions ??= GetActiveConstructionsOnScene();
        EvaluateVictoryStarsAtTurnStartForActiveTeam(activeConstructions);
        
        // Checagem de derrota por 0 unidades: marca o time como defeated.
        if (allowDefeatForZeroUnits && currentTurn >= 2 && activeTeamId >= 0)
        {
            if (TryDefeatSlotIfZeroUnits(activeSlot))
            {
                DeclareEliminationVictory(ResolveFirstAliveOpponentSlot(activeSlot), activeSlot, VictoryReason.ArmyEliminated);
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
            if (activeSlot.IsValid
                ? !IsOwnedBySlot(unit, activeSlot)
                : unit.TeamId != TeamId.Neutral)
                continue;

            // Lock pendente (ex.: emersao forcada adiada por hex ocupado) tenta
            // aplicar agora que o hex pode ter liberado. No turno em que aplica,
            // o tempo do lock ainda nao conta — a janela de exposicao comeca
            // com a unidade de fato na camada forcada.
            bool pendingForcedLayerAppliedNow = TryApplyPendingForcedLayerAtTurnStart(unit);
            if (!pendingForcedLayerAppliedNow)
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
                            // Marca para a fila de resolucao por combustivel. E a
                            // FILA (TurnStateManager) que decide pousar-ou-cair por
                            // unidade, com o cursor varrendo e apresentando cada
                            // caso: pouso de emergencia quando o hex permite (regra
                            // do sensor PodePousar) ou queda como antes.
                            turnStartUnitsMarkedForFuelDepletionDeath.Add(unit);
                            markedForFuelDepletionDeath = true;

                            // Jornal do Comandante: pre-avalia o desfecho (mesma
                            // regra deterministica que a fila usara neste mesmo
                            // frame de estado) para o briefing do turno JA conter
                            // a linha — a fila roda depois do flush do relatorio.
                            Vector3Int fuelCell = unit.CurrentCellPosition;
                            fuelCell.z = 0;
                            TryAutoAssignTurnStateManager();
                            bool willLand = turnStateManager != null && turnStateManager.CanEmergencyLandAtTurnStart(unit);
                            ReportTurnBriefingEvent(
                                PlayerSlotId.FromIndex(unit.SlotIndex),
                                willLand ? TurnBriefingCategory.EmergencyLanding : TurnBriefingCategory.FuelCrash,
                                ResolveRuntimeUnitName(unit),
                                willLand ? "pousou sem combustível — reabasteça ou remova" : "perdida por exaustão de combustível",
                                fuelCell);
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
                        SpriteRenderer upkeepRenderer = unit.GetMainSpriteRenderer();
                        turnStartAutonomyEntries.Add(new TurnStateManager.TurnStartAutonomyUpkeepEntry(
                            ResolveRuntimeUnitName(unit),
                            cell,
                            consumed,
                            beforeFuel,
                            afterFuel,
                            unit.GetMaxFuel(),
                            upkeepRenderer != null ? upkeepRenderer.sprite : null,
                            TeamUtils.GetColor(unit.TeamId)));
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
                    if (activeSlot.IsValid
                        ? !IsOwnedBySlot(passenger, activeSlot)
                        : passenger.TeamId != TeamId.Neutral)
                        continue;
                    passenger.ResetForTeamTurnStart();
                }
            }
        }
        TurnPerfLog("ReleaseUnits.IterateUnits", stageStartMs);

        pendingTurnStartAutonomyHelperEntries = turnStartAutonomyEntries;
        TryAutoAssignTurnStateManager();
        stageStartMs = TurnPerfNowMs();
        // Apenas enfileira. O arranque e de ApplyActiveTeam, DEPOIS do estagio de
        // FoW: as filas entram em cursor state proprio e, fora de Neutral, o
        // refresh de inicio de turno e rejeitado — a apresentacao ficaria travada
        // no observador do turno anterior enquanto a fila roda na tela.
        turnStateManager?.EnqueueTurnStartFuelDepletionDeaths(
            turnStartUnitsMarkedForFuelDepletionDeath, startImmediately: false);
        TurnPerfLog("ReleaseUnits.EnqueueFuelDepletionDeaths", stageStartMs);

        pendingTurnStartUpkeep = false;
    }

    // ------------------------------------------------------------------
    // Jornal do Comandante — ledger de eventos entre turnos.
    // Eventos registrados DURANTE os turnos alheios (contato perdido, tiro da
    // nevoa, conquista perdida...) acumulam aqui por time destinatario e sao
    // drenados no inicio do turno dele, virando linhas do relatorio do
    // panel_helper (junto do consumo em voo). Regra de ouro: quem registra
    // decide fog-honesto o que o destinatario tem direito de saber.
    // ------------------------------------------------------------------
    public enum TurnBriefingCategory
    {
        ContactLost = 0,
        FogFire = 1,
        ConstructionLost = 2,
        CaptureInProgress = 3,
        EmergencyLanding = 4,
        FuelCrash = 5,
        ForcedSurfaceApplied = 6,
        NewContact = 7,
        SupplyDepleted = 8
    }

    [System.NonSerialized] private readonly List<TurnBriefingEventSaveData> turnBriefingLedger = new List<TurnBriefingEventSaveData>();

    public IReadOnlyList<TurnBriefingEventSaveData> TurnBriefingLedger => turnBriefingLedger;

    public void RestoreTurnBriefingLedger(List<TurnBriefingEventSaveData> events)
    {
        turnBriefingLedger.Clear();
        if (events != null)
            turnBriefingLedger.AddRange(events);
    }

    public void ReportTurnBriefingEvent(
        PlayerSlotId targetSlot,
        TurnBriefingCategory category,
        string subjectName,
        string detail,
        Vector3Int cell)
    {
        if (!Application.isPlaying)
            return;
        if (!IsValidPlayerSlot(targetSlot))
            return;
        if (IsPlayerAI(targetSlot))
            return;

        cell.z = 0;
        turnBriefingLedger.Add(new TurnBriefingEventSaveData
        {
            slotIndex = targetSlot.Value,
            teamId = (int)GetVisualTeamForSlot(targetSlot),
            category = (int)category,
            subjectName = subjectName ?? string.Empty,
            detail = detail ?? string.Empty,
            cellX = cell.x,
            cellY = cell.y,
            turnNumber = currentTurn
        });
    }

    // Tiers do Jornal do Comandante (0=Critico, 1=Atencao, 2=Informativo):
    // Critico   = perda consumada ou golpe recebido (unidade, cidade, queda, tiro da nevoa);
    // Atencao   = ameaca em curso ou escassez (captura parcial, estoque zerado, autonomia critica);
    // Informativo = ganho de intel / auto-ajuste (novo contato, emersao, pouso).
    public enum TurnBriefingSeverity { Critical = 0, Warning = 1, Info = 2 }

    public static TurnBriefingSeverity ResolveBriefingSeverity(TurnBriefingCategory category)
    {
        switch (category)
        {
            case TurnBriefingCategory.ContactLost:
            case TurnBriefingCategory.ConstructionLost:
            case TurnBriefingCategory.FuelCrash:
            case TurnBriefingCategory.FogFire:
                return TurnBriefingSeverity.Critical;
            case TurnBriefingCategory.CaptureInProgress:
            case TurnBriefingCategory.SupplyDepleted:
                return TurnBriefingSeverity.Warning;
            default:
                return TurnBriefingSeverity.Info;
        }
    }

    private static string ResolveBriefingCategoryLabel(TurnBriefingCategory category)
    {
        switch (category)
        {
            case TurnBriefingCategory.ContactLost: return "CONTATO PERDIDO";
            case TurnBriefingCategory.FogFire: return "TIRO DA NÉVOA";
            case TurnBriefingCategory.ConstructionLost: return "CONQUISTA PERDIDA";
            case TurnBriefingCategory.CaptureInProgress: return "SOB CAPTURA";
            case TurnBriefingCategory.EmergencyLanding: return "POUSO DE EMERGÊNCIA";
            case TurnBriefingCategory.FuelCrash: return "QUEDA (COMBUSTÍVEL)";
            case TurnBriefingCategory.ForcedSurfaceApplied: return "EMERSÃO AUTOMÁTICA";
            case TurnBriefingCategory.NewContact: return "NOVO CONTATO";
            case TurnBriefingCategory.SupplyDepleted: return "ESTOQUE ZERADO";
            default: return "EVENTO";
        }
    }

    // Drena o ledger do time ativo + varreduras de ESTADO (estoques zerados,
    // capturas parciais) em linhas prontas para o relatorio.
    private List<TurnStateManager.HelperTurnStartAutonomyLine> BuildTurnBriefingLinesForActiveTeam()
    {
        var lines = new List<TurnStateManager.HelperTurnStartAutonomyLine>();
        if (activeTeamId < 0)
            return lines;

        // 1) Eventos do ledger destinados ao time ativo (drena removendo).
        var drained = new List<TurnBriefingEventSaveData>();
        for (int i = turnBriefingLedger.Count - 1; i >= 0; i--)
        {
            TurnBriefingEventSaveData evt = turnBriefingLedger[i];
            if (evt == null)
            {
                turnBriefingLedger.RemoveAt(i);
                continue;
            }
            int eventSlotIndex = evt.slotIndex;
            if (!IsValidPlayerSlotIndex(eventSlotIndex) &&
                TryGetUniqueSlotForTeam((TeamId)evt.teamId, out PlayerSlotId migratedSlot))
                eventSlotIndex = migratedSlot.Value;
            if (eventSlotIndex != ActiveSlotId.Value)
                continue;
            drained.Add(evt);
            turnBriefingLedger.RemoveAt(i);
        }
        // Removidos de tras pra frente: restaura ordem cronologica.
        drained.Reverse();
        // Categoria mais critica primeiro, cronologia dentro da categoria.
        drained.Sort((a, b) => a.category != b.category
            ? a.category.CompareTo(b.category)
            : a.turnNumber.CompareTo(b.turnNumber));

        for (int i = 0; i < drained.Count; i++)
        {
            TurnBriefingEventSaveData evt = drained[i];
            var cell = new Vector3Int(evt.cellX, evt.cellY, 0);
            TurnBriefingCategory category = (TurnBriefingCategory)evt.category;
            string label = ResolveBriefingCategoryLabel(category);
            string body = string.IsNullOrWhiteSpace(evt.detail)
                ? evt.subjectName
                : $"{evt.subjectName}\n{evt.detail}";
            lines.Add(new TurnStateManager.HelperTurnStartAutonomyLine
            {
                unitName = evt.subjectName,
                cell = cell,
                customText = $"{label}\n{body}\n({cell.x},{cell.y}) — T{evt.turnNumber}",
                severityTier = (int)ResolveBriefingSeverity(category)
            });
        }

        // 2) Varreduras de estado do proprio time (lembretes persistentes).
        List<ConstructionManager> constructions = ConstructionManager.AllActive;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if (construction.SlotIndex != ActiveSlotId.Value)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            string name = construction.ConstructionDisplayName;

            // Captura parcial em andamento contra voce (e seu predio: voce sabe).
            if (construction.IsCapturable &&
                construction.CurrentCapturePoints < construction.CapturePointsMax)
            {
                lines.Add(new TurnStateManager.HelperTurnStartAutonomyLine
                {
                    unitName = name,
                    cell = cell,
                    customText = $"{ResolveBriefingCategoryLabel(TurnBriefingCategory.CaptureInProgress)}\n{name} ({construction.CurrentCapturePoints}/{construction.CapturePointsMax})\n({cell.x},{cell.y})",
                    severityTier = (int)ResolveBriefingSeverity(TurnBriefingCategory.CaptureInProgress)
                });
            }

            // Estoques zerados (supply nao-infinito com oferta runtime em 0).
            if (construction.CanProvideSupplies && !construction.HasInfiniteSuppliesOverride)
            {
                IReadOnlyList<ConstructionSupplyOffer> offers = construction.OfferedSupplies;
                for (int o = 0; o < offers.Count; o++)
                {
                    ConstructionSupplyOffer offer = offers[o];
                    if (offer == null || offer.supply == null)
                        continue;
                    if (offer.quantity > 0 || construction.HasInfiniteSuppliesFor(offer.supply))
                        continue;

                    lines.Add(new TurnStateManager.HelperTurnStartAutonomyLine
                    {
                        unitName = name,
                        cell = cell,
                        customText = $"{ResolveBriefingCategoryLabel(TurnBriefingCategory.SupplyDepleted)}\n{name}: sem {offer.supply.displayName}\n({cell.x},{cell.y})",
                        severityTier = (int)ResolveBriefingSeverity(TurnBriefingCategory.SupplyDepleted)
                    });
                }
            }
        }

        return lines;
    }

    private void FlushTurnStartAutonomyHelper()
    {
        TryAutoAssignTurnStateManager();
        if (IsValidPlayerSlot(ActiveSlotId) && IsPlayerAI(ActiveSlotId))
        {
            DiscardTurnBriefingEventsForSlot(ActiveSlotId);
            pendingTurnStartAutonomyHelperEntries = null;
            // Limpa também um relatório humano que ainda estivesse aberto ou
            // reabrível. A AI recebe o upkeep, mas não a interface de intel.
            turnStateManager?.ShowTurnStartBriefing(null, null);
            return;
        }

        List<TurnStateManager.HelperTurnStartAutonomyLine> briefingLines = BuildTurnBriefingLinesForActiveTeam();
        turnStateManager?.ShowTurnStartBriefing(pendingTurnStartAutonomyHelperEntries, briefingLines);
        pendingTurnStartAutonomyHelperEntries = null;
    }

    private void DiscardTurnBriefingEventsForSlot(PlayerSlotId slot)
    {
        if (!IsValidPlayerSlot(slot))
            return;

        for (int i = turnBriefingLedger.Count - 1; i >= 0; i--)
        {
            TurnBriefingEventSaveData evt = turnBriefingLedger[i];
            if (evt == null)
            {
                turnBriefingLedger.RemoveAt(i);
                continue;
            }

            int eventSlotIndex = evt.slotIndex;
            if (!IsValidPlayerSlotIndex(eventSlotIndex) &&
                Enum.IsDefined(typeof(TeamId), evt.teamId) &&
                TryGetUniqueSlotForTeam((TeamId)evt.teamId, out PlayerSlotId migratedSlot))
            {
                eventSlotIndex = migratedSlot.Value;
            }

            if (eventSlotIndex == slot.Value)
                turnBriefingLedger.RemoveAt(i);
        }
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
        RecalculatePlayerRuntimeFlags(constructions);
        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry entry = players[i];
            int income = 0;
            for (int c = 0; c < constructions.Count; c++)
            {
                ConstructionManager construction = constructions[c];
                if (construction == null)
                    continue;
                if (construction.SlotIndex != i)
                    continue;

                income += ResolveConstructionIncomeForPlayer(entry, construction);
            }

            entry.incomePerTurn = Mathf.Max(0, income);
            players[i] = entry;
        }
    }

    private void RecalculatePlayerRuntimeFlags(List<ConstructionManager> constructions)
    {
        if (players == null)
            return;

        bool anyOwnedHeadQuarter = false;
        var slotsWithHeadQuarter = new HashSet<int>();
        if (constructions != null)
        {
            for (int i = 0; i < constructions.Count; i++)
            {
                ConstructionManager construction = constructions[i];
                if (construction == null || !construction.IsPlayerHeadQuarter)
                    continue;
                int ownerSlot = construction.SlotIndex;
                if (!IsValidPlayerSlotIndex(ownerSlot))
                    continue;

                anyOwnedHeadQuarter = true;
                slotsWithHeadQuarter.Add(ownerSlot);
            }
        }

        for (int i = 0; i < players.Count; i++)
        {
            PlayerEntry entry = players[i];
            entry.isRebelRuntime =
                anyOwnedHeadQuarter &&
                entry.teamId != TeamId.Neutral &&
                !entry.defeated &&
                !slotsWithHeadQuarter.Contains(i);
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

        int playerIndex = ActiveSlotId.Value;
        if (!IsValidPlayerSlotIndex(playerIndex))
        {
            pendingTurnStartEconomy = false;
            return;
        }

        RecalculateIncomePerTurnForAllPlayers(constructions);

        PlayerEntry entry = players[playerIndex];
        TeamId team = entry.teamId;
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
        if (Application.isPlaying)
            EnsureFogOfWarMemoryTilemap();

        if (fogOfWarTerrainDatabase == null)
            fogOfWarTerrainDatabase = ResolveFogTerrainDatabase();
        if (fogOfWarDpqAirHeightConfig == null)
            fogOfWarDpqAirHeightConfig = ResolveFogDpqAirHeightConfig();
    }

    private void EnsureFogOfWarMemoryTilemap()
    {
        if (fogOfWarTilemap == null)
            return;
        if (fogOfWarMemoryTilemap != null)
        {
            EnsureFogBreakwaterMemoryTilemap();
            return;
        }

        fogOfWarMemoryTilemap = FindTilemapByName("FogOfWarTile");
        if (fogOfWarMemoryTilemap != null)
        {
            EnsureFogBreakwaterMemoryTilemap();
            return;
        }
        if (!Application.isPlaying)
            return;

        GameObject memoryObject = new GameObject("FogOfWarTile", typeof(Tilemap), typeof(TilemapRenderer));
        Transform sourceTransform = fogOfWarTilemap.transform;
        Transform memoryTransform = memoryObject.transform;
        memoryTransform.SetParent(sourceTransform.parent, false);
        memoryTransform.localPosition = sourceTransform.localPosition;
        memoryTransform.localRotation = sourceTransform.localRotation;
        memoryTransform.localScale = sourceTransform.localScale;

        fogOfWarMemoryTilemap = memoryObject.GetComponent<Tilemap>();
        fogOfWarMemoryTilemap.tileAnchor = fogOfWarTilemap.tileAnchor;
        fogOfWarMemoryTilemap.orientation = fogOfWarTilemap.orientation;
        fogOfWarMemoryTilemap.orientationMatrix = fogOfWarTilemap.orientationMatrix;
        fogOfWarMemoryTilemap.color = Color.white;

        TilemapRenderer sourceRenderer = fogOfWarTilemap.GetComponent<TilemapRenderer>();
        TilemapRenderer memoryRenderer = memoryObject.GetComponent<TilemapRenderer>();
        if (sourceRenderer != null)
        {
            memoryRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            memoryRenderer.mode = sourceRenderer.mode;
        }
        memoryRenderer.sortingLayerName = "FogOfWarTile";
        memoryRenderer.sortingOrder = 0;

        EnsureFogBreakwaterMemoryTilemap();
    }

    private void EnsureFogBreakwaterMemoryTilemap()
    {
        if (fogOfWarTilemap == null)
            return;

        Tilemap boardMap = ResolveFogBoardTilemap();
        Tilemap source = FindTilemapByNameOnBoard("quebraMar", boardMap);
        if (fogOfWarBreakwaterMemoryTilemap == null)
            fogOfWarBreakwaterMemoryTilemap = FindTilemapByNameOnBoard("FogOfWarBreakwaterTile", boardMap);
        if (fogOfWarBreakwaterMemoryTilemap == null)
        {
            GameObject memoryObject = new GameObject("FogOfWarBreakwaterTile", typeof(Tilemap), typeof(TilemapRenderer));
            fogOfWarBreakwaterMemoryTilemap = memoryObject.GetComponent<Tilemap>();
        }

        Tilemap layoutSource = source != null ? source : fogOfWarTilemap;
        Transform sourceTransform = layoutSource.transform;
        Transform memoryTransform = fogOfWarBreakwaterMemoryTilemap.transform;
        memoryTransform.SetParent(sourceTransform.parent, false);
        memoryTransform.localPosition = sourceTransform.localPosition;
        memoryTransform.localRotation = sourceTransform.localRotation;
        memoryTransform.localScale = sourceTransform.localScale;

        fogOfWarBreakwaterMemoryTilemap.tileAnchor = layoutSource.tileAnchor;
        fogOfWarBreakwaterMemoryTilemap.orientation = layoutSource.orientation;
        fogOfWarBreakwaterMemoryTilemap.orientationMatrix = layoutSource.orientationMatrix;
        fogOfWarBreakwaterMemoryTilemap.color = layoutSource.color;

        TilemapRenderer renderer = fogOfWarBreakwaterMemoryTilemap.GetComponent<TilemapRenderer>();
        TilemapRenderer sourceRenderer = layoutSource.GetComponent<TilemapRenderer>();
        if (sourceRenderer != null)
        {
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.mode = sourceRenderer.mode;
            renderer.sortOrder = sourceRenderer.sortOrder;
        }
        renderer.sortingLayerName = "FogOfWarTile";
        renderer.sortingOrder = 1;
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

    private static Tilemap FindTilemapByNameOnBoard(string targetName, Tilemap boardMap)
    {
        if (string.IsNullOrWhiteSpace(targetName) || boardMap == null)
            return null;

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null
                || tilemap.gameObject.scene != boardMap.gameObject.scene
                || tilemap.layoutGrid != boardMap.layoutGrid)
                continue;
            if (string.Equals(tilemap.name, targetName, StringComparison.OrdinalIgnoreCase))
                return tilemap;
        }
        return null;
    }

    public void RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode mode = FogOfWarRefreshMode.FullVisual)
    {
        PlayerSlotId gameplaySlot = ActiveSlotId;
        if (ShouldUseHotSeatPrivacyCurtain())
        {
            ExecuteFogRefreshContext(
                CreateFogUpdateContext(
                    gameplaySlot,
                    gameplaySlot,
                    gameplaySlot,
                    mode));
            return;
        }
        PlayerSlotId presentationSlot = TryResolveFogPresentationSlot(out PlayerSlotId resolvedPresentationSlot)
            ? resolvedPresentationSlot
            : gameplaySlot;
        if (presentationSlot == gameplaySlot)
        {
            ExecuteFogRefreshContext(
                CreateFogUpdateContext(gameplaySlot, gameplaySlot, presentationSlot, mode));
            return;
        }

        ExecuteFogRefreshContext(
            CreateFogUpdateContext(
                gameplaySlot,
                gameplaySlot,
                presentationSlot,
                FogOfWarRefreshMode.DataOnly));
        ExecuteFogRefreshContext(
            CreateFogUpdateContext(
                gameplaySlot,
                presentationSlot,
                presentationSlot,
                FogOfWarRefreshMode.FullVisual));
    }

    private FogUpdateContext CreateFogUpdateContext(
        PlayerSlotId gameplaySlot,
        PlayerSlotId observerSlot,
        PlayerSlotId presentationSlot,
        FogOfWarRefreshMode mode)
    {
        bool publishVisuals =
            mode == FogOfWarRefreshMode.FullVisual &&
            observerSlot == presentationSlot;
        return new FogUpdateContext(
            gameplaySlot,
            observerSlot,
            presentationSlot,
            publishGameplayData: true,
            publishVisuals,
            recordExplorationMemory: true,
            recordIntel: Application.isPlaying);
    }

    private void ExecuteFogRefreshContext(FogUpdateContext context)
    {
        if (!IsValidPlayerSlot(context.gameplaySlot) ||
            !IsValidPlayerSlot(context.observerSlot) ||
            (context.publishVisuals && !IsValidPlayerSlot(context.presentationSlot)))
        {
            return;
        }

        FogObserverScopeState previous = EnterFogObserverScope(context);
        try
        {
            RefreshFogOfWarForCurrentTeamInternal(context);
        }
        finally
        {
            ExitFogObserverScope(previous);
        }
    }

    // Ponte de compatibilidade: coletores legados ainda consultam ActiveSlotId.
    // A troca temporaria fica confinada aqui; a politica pertence ao contexto.
    private FogObserverScopeState EnterFogObserverScope(FogUpdateContext context)
    {
        FogObserverScopeState previous = new FogObserverScopeState(
            activeTeamId,
            activePlayerListIndex,
            fogPresentationGameplayTeamId,
            activeFogUpdateContext);
        activeFogUpdateContext = context;
        activePlayerListIndex = context.observerSlot.Value;
        activeTeamId = (int)GetVisualTeamForSlot(context.observerSlot);
        fogPresentationGameplayTeamId =
            context.publishVisuals && context.gameplaySlot != context.observerSlot
                ? (int)GetVisualTeamForSlot(context.gameplaySlot)
                : int.MinValue;
        return previous;
    }

    private void ExitFogObserverScope(FogObserverScopeState previous)
    {
        activeTeamId = previous.activeTeamId;
        activePlayerListIndex = previous.activePlayerListIndex;
        fogPresentationGameplayTeamId = previous.presentationGameplayTeamId;
        activeFogUpdateContext = previous.updateContext;
        ValidateFogOfWarSortingLayer();
    }

    private void RefreshFogOfWarForCurrentTeamInternal(FogUpdateContext context)
    {
        PodeDetectarSensor.ClearRefreshScopedTerrainCache();
        if (SuppressFogOfWarRefresh)
            return;

        if (!enableTotalWar)
            return;

        if (fogOfWarTilemap == null)
            return;
        PlayerSlotId observerSlot = context.observerSlot;
        if (!IsValidPlayerSlot(observerSlot))
            return;
        if (ActiveSlotId != observerSlot)
        {
            if (enableFogValidationLogs)
                Debug.LogWarning("[FoW][Context] observer_scope_mismatch");
            return;
        }

        ValidateFogOfWarSortingLayer();

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return;

        if (ShouldLogPodeEnxergarRuntime)
        {
            Debug.Log(
                $"[FoW][Context] gameplaySlot={context.gameplaySlot.Value} " +
                $"observerSlot={context.observerSlot.Value} presentationSlot={context.presentationSlot.Value} " +
                $"publishData={context.publishGameplayData} publishVisuals={context.publishVisuals} " +
                $"recordMemory={context.recordExplorationMemory} recordIntel={context.recordIntel} " +
                $"activeTeam={activeTeamId} " +
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

        double unitScanStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        double unitScanMs = enableFogStepPerfLogs
            ? (Time.realtimeSinceStartupAsDouble - unitScanStartMs) * 1000d
            : 0d;
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
            if (unit.SlotIndex != observerSlot.Value)
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
        // Estas seis etapas respondiam por ~550ms de um refresh de 695ms sem
        // nenhum timer: o collect levava a culpa por ser o unico medido.
        double stepStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        double publishMs = 0d;
        double unitVisibilityMs = 0d;
        double intelMs = 0d;
        double renderMs = 0d;
        double callbacksMs = 0d;
        double storeMs = 0d;

        if (context.publishGameplayData)
        {
            PublishFogGameplaySnapshot(
                observerSlot.Value,
                boardMap,
                units,
                context.recordExplorationMemory);
        }
        if (enableFogStepPerfLogs)
        {
            publishMs = (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;
            stepStartMs = Time.realtimeSinceStartupAsDouble;
        }

        if (context.publishVisuals)
            RefreshRuntimeUnitFogVisibility();
        if (enableFogStepPerfLogs)
        {
            unitVisibilityMs = (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;
            stepStartMs = Time.realtimeSinceStartupAsDouble;
        }

        if (context.recordIntel)
            AIIntelLedger.RecordVisibleContactsForSlot(observerSlot, currentTurn, this);
        if (enableFogStepPerfLogs)
        {
            intelMs = (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;
            stepStartMs = Time.realtimeSinceStartupAsDouble;
        }

        if (context.publishVisuals)
        {
            RenderFogOverlayFromRuntimeCache(boardMap);
            if (enableFogStepPerfLogs)
            {
                renderMs = (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;
                stepStartMs = Time.realtimeSinceStartupAsDouble;
            }
            if (Application.isPlaying)
            {
                OnFogOfWarUpdated?.Invoke();
            }
            if (enableFogStepPerfLogs)
            {
                callbacksMs = (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;
                stepStartMs = Time.realtimeSinceStartupAsDouble;
            }
        }

        StoreFogContributionRuntimeForSlot(observerSlot);
        if (enableFogStepPerfLogs)
            storeMs = (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;

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
            double stepsMeasuredMs =
                unitScanMs + collectTotalMs + constructionVisionMs + publishMs +
                unitVisibilityMs + intelMs + renderMs + callbacksMs + storeMs;
            Debug.Log(
                $"[FoW][Perf][Steps] unitScan={unitScanMs:F3}ms publish={publishMs:F3}ms " +
                $"unitVisibility={unitVisibilityMs:F3}ms intel={intelMs:F3}ms " +
                $"render={renderMs:F3}ms callbacks={callbacksMs:F3}ms store={storeMs:F3}ms | " +
                $"boardCells={fogBoardCellsBuffer.Count} unitsScanned={units.Length} " +
                $"unaccounted={(refreshTotalMs - stepsMeasuredMs):F3}ms");
            PodeDetectarSensor.GetFogDebugCounters(
                out int cacheHits,
                out int cacheMisses,
                out int poolRents,
                out int poolReleases,
                out int fragataCollectWorkspaceRents,
                out int fragataCollectWorkspaceReleases);
            Debug.Log($"[FoW][Cache] hits={cacheHits} misses={cacheMisses}");
            PodeDetectarSensor.GetCollectDebugCounters(
                out int collectRuns,
                out int collectDistanceCells,
                out int collectMaxRange,
                out int collectLayerChecks,
                out int collectSpecChecks,
                out int collectLosCalls,
                out int collectLosHits,
                out int collectAquaticMaps);
            Debug.Log(
                $"[FoW][Perf][Collect] runs={collectRuns} maxRange={collectMaxRange} " +
                $"distanceCells={collectDistanceCells} " +
                $"outCells={collectVisibleCellsTotal} " +
                $"layerChecks={collectLayerChecks} specChecks={collectSpecChecks} " +
                $"los.calls={collectLosCalls} los.hits={collectLosHits} " +
                $"los.misses={(collectLosCalls - collectLosHits)} " +
                $"aquaticMaps={collectAquaticMaps}");
            PodeDetectarSensor.GetCollectLosDebugCounters(
                out double losMs,
                out int cellVisionCalls,
                out double constructionMs,
                out double structureMs,
                out double lerpMs,
                out int lerpCells);
            Debug.Log(
                $"[FoW][Perf][Los] los.ms={losMs:F3} " +
                $"cellVision.calls={cellVisionCalls} " +
                $"construction.ms={constructionMs:F3} " +
                $"structure.ms={structureMs:F3} " +
                $"lerp.ms={lerpMs:F3} lerp.cells={lerpCells} | " +
                $"collect.total={collectTotalMs:F3}ms " +
                $"outsideLos={(collectTotalMs - losMs):F3}ms");
            Debug.Log(
                $"[FoW][Coverage] geographic={fogGeographicContributorsByCell.Count} " +
                $"sensor={fogSensorContributorsByCell.Count} " +
                $"geographicOnly={CountFogGeographicOnlyCells()} " +
                $"sources={fogContributionsBySource.Count} " +
                $"unitSources={CountFogContributionSources(FogContributionSourceType.Unit)} " +
                $"constructionSources={CountFogContributionSources(FogContributionSourceType.Construction)}");
            Debug.Log(
                $"[FoW][Pool] rents={poolRents} releases={poolReleases} " +
                $"fragataCollect.rents={fragataCollectWorkspaceRents} fragataCollect.releases={fragataCollectWorkspaceReleases}");

            if (topCollectEntries != null && topCollectEntries.Count > 0)
            {
                for (int i = 0; i < topCollectEntries.Count; i++)
                {
                    FogCollectPerfEntry entry = topCollectEntries[i];
                    Debug.Log(
                        $"[FoW][Perf][CollectTop{(i + 1)}] unit={entry.unitName} " +
                        $"object={entry.objectName} slot={entry.slotIndex} " +
                        $"cell=({entry.cell.x},{entry.cell.y}) " +
                        $"ms={entry.collectMs:F3} cells={entry.visibleCellCount}");
                }
            }
        }
    }

    private bool TryRefreshFogOfWarForTurnStartIncremental()
    {
        if (!Application.isPlaying || !debugFogOfWarEnabled || !enableTotalWar)
            return false;
        if (turnStateManager != null &&
            turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        {
            return false;
        }

        Tilemap boardMap = ResolveFogBoardTilemap();
        PlayerSlotId gameplaySlot = ActiveSlotId;
        if (boardMap == null || !IsValidPlayerSlot(gameplaySlot))
            return false;

        if (ShouldUseHotSeatPrivacyCurtain())
        {
            FogUpdateContext privacyContext = CreateFogUpdateContext(
                gameplaySlot,
                gameplaySlot,
                gameplaySlot,
                FogOfWarRefreshMode.FullVisual);
            if (!TrySynchronizeFogRuntimeAtTurnStart(privacyContext, boardMap))
                return false;
            OnFogOfWarUpdated?.Invoke();
            return true;
        }

        PlayerSlotId presentationSlot = TryResolveFogPresentationSlot(out PlayerSlotId resolvedPresentationSlot)
            ? resolvedPresentationSlot
            : gameplaySlot;
        bool splitPresentation = presentationSlot != gameplaySlot;
        FogUpdateContext gameplayContext = CreateFogUpdateContext(
            gameplaySlot,
            gameplaySlot,
            presentationSlot,
            splitPresentation ? FogOfWarRefreshMode.DataOnly : FogOfWarRefreshMode.FullVisual);
        if (!TrySynchronizeFogRuntimeAtTurnStart(gameplayContext, boardMap))
            return false;

        if (splitPresentation)
        {
            FogUpdateContext presentationContext = CreateFogUpdateContext(
                gameplaySlot,
                presentationSlot,
                presentationSlot,
                FogOfWarRefreshMode.FullVisual);
            if (!TrySynchronizeFogRuntimeAtTurnStart(presentationContext, boardMap))
                return false;
        }

        OnFogOfWarUpdated?.Invoke();
        return true;
    }

    public FogPlanningSnapshotBarrierResult EnsureConfirmedFogGameplaySnapshotForSlot(
        PlayerSlotId observerSlot)
    {
        if (!Application.isPlaying || !debugFogOfWarEnabled || !enableTotalWar ||
            !IsValidPlayerSlot(observerSlot))
        {
            return FogPlanningSnapshotBarrierResult.Unavailable;
        }

        if (turnStateManager != null &&
            turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        {
            Debug.LogWarning(
                $"[FoW][PlanningBarrier] slot={observerSlot.Value} rejected=outside-neutral");
            return FogPlanningSnapshotBarrierResult.RejectedOutsideNeutral;
        }

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return FogPlanningSnapshotBarrierResult.Unavailable;

        PlayerSlotId presentationSlot =
            TryResolveFogPresentationSlot(out PlayerSlotId resolvedPresentationSlot)
                ? resolvedPresentationSlot
                : observerSlot;
        FogUpdateContext context = CreateFogUpdateContext(
            observerSlot,
            observerSlot,
            presentationSlot,
            FogOfWarRefreshMode.DataOnly);
        PlayerSlotId runtimeOwnerBefore =
            PlayerSlotId.FromIndex(fogCachedObserverSlotIndex);

        double startMs = Time.realtimeSinceStartupAsDouble;
        if (TrySynchronizeFogRuntimeAtTurnStart(context, boardMap))
        {
            RestoreFogRuntimeOwnerAfterPlanningBarrier(
                observerSlot,
                presentationSlot,
                runtimeOwnerBefore,
                boardMap);
            if (enableFogStepPerfLogs)
            {
                Debug.Log(
                    $"[FoW][PlanningBarrier] slot={observerSlot.Value} " +
                    $"result=reused total={(Time.realtimeSinceStartupAsDouble - startMs) * 1000d:F3}ms");
            }
            return FogPlanningSnapshotBarrierResult.ReusedAndReconciled;
        }

        ExecuteFogRefreshContext(context);
        RestoreFogRuntimeOwnerAfterPlanningBarrier(
            observerSlot,
            presentationSlot,
            runtimeOwnerBefore,
            boardMap);
        if (enableFogStepPerfLogs)
        {
            Debug.Log(
                $"[FoW][PlanningBarrier] slot={observerSlot.Value} " +
                $"result=full-fallback total={(Time.realtimeSinceStartupAsDouble - startMs) * 1000d:F3}ms");
        }
        return FogPlanningSnapshotBarrierResult.FullFallback;
    }

    private void RestoreFogRuntimeOwnerAfterPlanningBarrier(
        PlayerSlotId observerSlot,
        PlayerSlotId presentationSlot,
        PlayerSlotId runtimeOwnerBefore,
        Tilemap boardMap)
    {
        PlayerSlotId targetRuntimeSlot = IsValidPlayerSlot(runtimeOwnerBefore)
            ? runtimeOwnerBefore
            : presentationSlot;
        if (observerSlot == targetRuntimeSlot ||
            !IsValidPlayerSlot(targetRuntimeSlot) ||
            boardMap == null)
        {
            return;
        }

        if (!TryActivateFogContributionRuntimeForSlot(
                targetRuntimeSlot,
                boardMap) &&
            enableFogValidationLogs)
        {
            Debug.LogWarning(
                $"[FoW][PlanningBarrier] presentation-runtime-missing " +
                $"slot={targetRuntimeSlot.Value}; visual tilemaps preservados");
        }
    }

    private bool TrySynchronizeFogRuntimeAtTurnStart(
        FogUpdateContext context,
        Tilemap boardMap)
    {
        FogObserverScopeState previous = EnterFogObserverScope(context);
        try
        {
            if (!TryActivateFogContributionRuntimeForSlot(context.observerSlot, boardMap))
            {
                if (enableFogStepPerfLogs)
                    Debug.Log($"[FoW][TurnStartCache] slot={context.observerSlot.Value} activated=false");
                return false;
            }

            double startMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
            List<UnitManager> eligibleUnits = new List<UnitManager>();
            HashSet<FogContributionSourceId> eligibleUnitSources = new HashSet<FogContributionSourceId>();
            List<UnitManager> activeUnits = UnitManager.AllActive;
            for (int i = 0; i < activeUnits.Count; i++)
            {
                UnitManager unit = activeUnits[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked ||
                    unit.SlotIndex != context.observerSlot.Value || !IsUnitOnBoard(unit, boardMap))
                {
                    continue;
                }

                FogContributionSourceId sourceId = ResolveFogContributionSourceId(unit);
                if (!eligibleUnitSources.Add(sourceId))
                    return false;
                eligibleUnits.Add(unit);
            }

            int removedUnits = 0;
            int removedConstructions = 0;
            List<FogContributionSourceId> staleSources = new List<FogContributionSourceId>();
            foreach (KeyValuePair<FogContributionSourceId, FogSourceContributionCacheEntry> pair
                     in fogContributionsBySource)
            {
                if (pair.Key.type == FogContributionSourceType.Unit &&
                    !eligibleUnitSources.Contains(pair.Key))
                {
                    staleSources.Add(pair.Key);
                    removedUnits++;
                }
                else if (pair.Key.type == FogContributionSourceType.Construction)
                {
                    // Construcoes sao baratas e propriedade pode ter mudado durante
                    // outro turno. Reconstrua somente este subconjunto de fontes.
                    staleSources.Add(pair.Key);
                    removedConstructions++;
                }
            }
            for (int i = 0; i < staleSources.Count; i++)
            {
                FogContributionSourceId sourceId = staleSources[i];
                if (fogContributionsBySource.TryGetValue(
                        sourceId,
                        out FogSourceContributionCacheEntry staleEntry))
                {
                    RemoveFogSourceContributions(staleEntry, boardMap, updateVisual: false);
                }
                fogContributionsBySource.Remove(sourceId);
            }

            int changedUnits = 0;
            int unchangedUnits = 0;
            int collectedCells = 0;
            for (int i = 0; i < eligibleUnits.Count; i++)
            {
                UnitManager unit = eligibleUnits[i];
                FogContributionSourceId sourceId = ResolveFogContributionSourceId(unit);
                int sourceStateHash = BuildFogUnitSourceStateHash(unit);
                if (fogContributionsBySource.TryGetValue(
                        sourceId,
                        out FogSourceContributionCacheEntry existing) &&
                    existing != null &&
                    existing.sourceStateHash == sourceStateHash)
                {
                    // Revisoes globais podem ter avancado por acoes inimigas sem
                    // alterar esta fonte. Rebaseie a chave sobre o estado confirmado.
                    existing.unitCacheKey = BuildFogUnitCacheKey(unit, boardMap);
                    unchangedUnits++;
                    continue;
                }

                UpdateFogVisibilityForUnit(
                    unit,
                    boardMap,
                    out _,
                    out int visibleCellsCollected,
                    out _,
                    updateVisual: false);
                collectedCells += visibleCellsCollected;
                changedUnits++;
            }

            int constructions = ApplyFriendlyConstructionVision(boardMap, updateVisual: false);
            UnitManager[] snapshotUnits = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude);
            if (context.publishGameplayData)
            {
                PublishFogGameplaySnapshot(
                    context.observerSlot.Value,
                    boardMap,
                    snapshotUnits,
                    context.recordExplorationMemory);
            }
            StoreFogContributionRuntimeForSlot(context.observerSlot);
            if (context.recordIntel)
                AIIntelLedger.RecordVisibleContactsForSlot(context.observerSlot, currentTurn, this);
            if (context.publishVisuals)
            {
                RefreshRuntimeUnitFogVisibility();
                RenderFogOverlayFromRuntimeCache(boardMap);
            }

            if (enableFogStepPerfLogs)
            {
                double totalMs = (Time.realtimeSinceStartupAsDouble - startMs) * 1000d;
                Debug.Log(
                    $"[FoW][TurnStartCache] slot={context.observerSlot.Value} activated=true " +
                    $"units.changed={changedUnits} units.unchanged={unchangedUnits} units.removed={removedUnits} " +
                    $"cells.collected={collectedCells} constructions={constructions} " +
                    $"constructions.removed={removedConstructions} total={totalMs:F3}ms");
            }
            return true;
        }
        finally
        {
            ExitFogObserverScope(previous);
        }
    }

    private void ScheduleInactiveAiFogCacheWarmup()
    {
        if (!Application.isPlaying ||
            !debugFogOfWarEnabled ||
            !enableTotalWar ||
            SuppressFogOfWarRefresh ||
            !IsValidPlayerSlot(ActiveSlotId) ||
            IsPlayerAI(ActiveSlotId))
        {
            return;
        }

        if (fogCacheWarmupRoutine != null)
        {
            StopCoroutine(fogCacheWarmupRoutine);
            fogCacheWarmupRoutine = null;
        }

        int warmupGeneration = ++fogCacheWarmupGeneration;
        fogCacheWarmupRoutine = StartCoroutine(
            WarmInactiveAiFogCachesAcrossFrames(
                ActiveSlotId,
                warmupGeneration));
    }

    private IEnumerator WarmInactiveAiFogCachesAcrossFrames(
        PlayerSlotId hostSlot,
        int warmupGeneration)
    {
        double totalStartMs = enableFogStepPerfLogs
            ? Time.realtimeSinceStartupAsDouble
            : 0d;
        int warmedSources = 0;
        int warmedSlots = 0;
        int warmedFrames = 0;
        fogWarmupActivateMs = 0d;
        fogWarmupWorkMs = 0d;
        fogWarmupStoreMs = 0d;
        fogWarmupRestoreMs = 0d;
        fogWarmupClonedSources = 0;

        try
        {
            // Nunca alonga o mesmo frame que acabou de publicar o FOW humano.
            yield return null;

            // Um load pode começar depois que esta coroutine foi agendada.
            // Nesse caso o snapshot salvo é a autoridade; não reconstrua em
            // paralelo caches que serão restaurados ao fim da carga.
            if (SuppressFogOfWarRefresh ||
                warmupGeneration != fogCacheWarmupGeneration)
                yield break;

            Tilemap boardMap = ResolveFogBoardTilemap();
            if (boardMap == null || players == null || players.Count <= 1)
                yield break;
            PodeDetectarSensor.ClearRefreshScopedTerrainCache();

            for (int offset = 1; offset < players.Count; offset++)
            {
                int slotIndex = (hostSlot.Value + offset) % players.Count;
                PlayerSlotId observerSlot = PlayerSlotId.FromIndex(slotIndex);
                if (!IsValidPlayerSlot(observerSlot) ||
                    players[slotIndex].defeated ||
                    !IsPlayerAI(observerSlot))
                {
                    continue;
                }

                List<UnitManager> unitsToWarm = new List<UnitManager>();
                List<UnitManager> activeUnits = UnitManager.AllActive;
                for (int i = 0; i < activeUnits.Count; i++)
                {
                    UnitManager unit = activeUnits[i];
                    if (unit == null ||
                        !unit.gameObject.activeInHierarchy ||
                        unit.IsEmbarked ||
                        unit.SlotIndex != observerSlot.Value ||
                        !IsUnitOnBoard(unit, boardMap))
                    {
                        continue;
                    }

                    FogContributionSourceId sourceId =
                        ResolveFogContributionSourceId(unit);
                    int sourceStateHash = BuildFogUnitSourceStateHash(unit);
                    if (fogContributionRuntimeBySlot.TryGetValue(
                            observerSlot.Value,
                            out FogSlotContributionRuntime stored) &&
                        stored != null &&
                        stored.sources.TryGetValue(
                            sourceId,
                            out FogSourceContributionCacheEntry existing) &&
                        existing != null &&
                        existing.sourceStateHash == sourceStateHash)
                    {
                        continue;
                    }

                    unitsToWarm.Add(unit);
                }

                double frameStartMs = Time.realtimeSinceStartupAsDouble;
                for (int i = 0; i < unitsToWarm.Count; i++)
                {
                    bool waitedForNeutral = false;
                    while (IsValidPlayerSlot(hostSlot) &&
                           ActiveSlotId == hostSlot &&
                           !SuppressFogOfWarRefresh &&
                           warmupGeneration == fogCacheWarmupGeneration &&
                           turnStateManager != null &&
                           turnStateManager.CurrentCursorState !=
                               TurnStateManager.CursorState.Neutral)
                    {
                        yield return null;
                        waitedForNeutral = true;
                    }

                    // Esperar pelo Neutral tambem consome frames: o orcamento
                    // vale para o frame atual, nao para o que ficou para tras.
                    if (waitedForNeutral)
                        frameStartMs = Time.realtimeSinceStartupAsDouble;

                    // A troca de turno venceu a corrida. O cache parcial permanece
                    // valido e o reconciliador de TurnStart completa apenas o resto.
                    if (!IsValidPlayerSlot(hostSlot) ||
                        ActiveSlotId != hostSlot ||
                        SuppressFogOfWarRefresh ||
                        warmupGeneration != fogCacheWarmupGeneration ||
                        IsPlayerAI(ActiveSlotId))
                    {
                        yield break;
                    }

                    UnitManager unit = unitsToWarm[i];
                    if (unit != null &&
                        unit.gameObject.activeInHierarchy &&
                        !unit.IsEmbarked &&
                        unit.SlotIndex == observerSlot.Value &&
                        IsUnitOnBoard(unit, boardMap) &&
                        TryWarmFogContributionSourceForInactiveSlot(
                            observerSlot,
                            hostSlot,
                            unit,
                            boardMap))
                    {
                        warmedSources++;
                    }

                    // Orcamento de CPU por frame, nao uma fonte por frame. O
                    // objetivo original -- nao entregar ao jogador uma parede de
                    // varios segundos durante Passar Turno -- e o mesmo; o que
                    // mudou e que uma fonte deixou de custar ~108ms e passou a
                    // custar ~10ms, e ceder o frame a cada uma delas fazia o warm
                    // esperar mais do que trabalhar.
                    if ((Time.realtimeSinceStartupAsDouble - frameStartMs) * 1000d >=
                        fogWarmupFrameBudgetMs)
                    {
                        yield return null;
                        warmedFrames++;
                        frameStartMs = Time.realtimeSinceStartupAsDouble;
                    }
                }

                if (!IsValidPlayerSlot(hostSlot) ||
                    ActiveSlotId != hostSlot ||
                    SuppressFogOfWarRefresh ||
                    warmupGeneration != fogCacheWarmupGeneration ||
                    IsPlayerAI(ActiveSlotId))
                {
                    yield break;
                }

                if (TryFinalizeFogCacheWarmupForInactiveSlot(
                        observerSlot,
                        hostSlot,
                        boardMap))
                {
                    warmedSlots++;
                }
                yield return null;
            }
        }
        finally
        {
            if (enableFogStepPerfLogs)
            {
                double totalMs =
                    (Time.realtimeSinceStartupAsDouble - totalStartMs) * 1000d;
                Debug.Log(
                    $"[FoW][Warmup] host={hostSlot.Value} " +
                    $"slots={warmedSlots} sources={warmedSources} total={totalMs:F3}ms");
                double cpuMs =
                    fogWarmupActivateMs + fogWarmupWorkMs +
                    fogWarmupStoreMs + fogWarmupRestoreMs;
                Debug.Log(
                    $"[FoW][Warmup][Steps] activate={fogWarmupActivateMs:F1}ms " +
                    $"work={fogWarmupWorkMs:F1}ms store={fogWarmupStoreMs:F1}ms " +
                    $"restore={fogWarmupRestoreMs:F1}ms | cpu={cpuMs:F1}ms " +
                    $"clonedSources={fogWarmupClonedSources} " +
                    $"frames={warmedFrames} budget={fogWarmupFrameBudgetMs:F0}ms " +
                    $"idleBetweenFrames={(totalMs - cpuMs):F1}ms");
            }
            if (warmupGeneration == fogCacheWarmupGeneration)
                fogCacheWarmupRoutine = null;
        }
    }

    private bool TryWarmFogContributionSourceForInactiveSlot(
        PlayerSlotId observerSlot,
        PlayerSlotId hostSlot,
        UnitManager unit,
        Tilemap boardMap)
    {
        if (unit == null ||
            boardMap == null ||
            ActiveSlotId != hostSlot ||
            fogCachedObserverSlotIndex != hostSlot.Value ||
            !fogContributionRuntimeBySlot.ContainsKey(hostSlot.Value))
        {
            return false;
        }

        PlayerSlotId runtimeOwnerBefore =
            PlayerSlotId.FromIndex(fogCachedObserverSlotIndex);
        FogUpdateContext warmupContext = new FogUpdateContext(
            observerSlot,
            observerSlot,
            observerSlot,
            publishGameplayData: false,
            publishVisuals: false,
            recordExplorationMemory: false,
            recordIntel: false);
        FogObserverScopeState previous = EnterFogObserverScope(warmupContext);
        bool warmed = false;
        double stepStartMs = Time.realtimeSinceStartupAsDouble;
        try
        {
            // Quantas fontes esta ativacao vai clonar. Cresce a cada unidade
            // aquecida: e a assinatura do O(n^2), se ele existir.
            if (fogContributionRuntimeBySlot.TryGetValue(
                    observerSlot.Value,
                    out FogSlotContributionRuntime pendingRuntime) &&
                pendingRuntime != null)
            {
                fogWarmupClonedSources += pendingRuntime.sources.Count;
            }

            if (!TryActivateFogContributionRuntimeForSlot(
                    observerSlot,
                    boardMap))
            {
                // Inicializa somente o canal de contribuicoes do observador.
                // ResetFogOfWarRuntime tambem invalidaria buffers da apresentacao
                // humana, embora nenhum visual seja publicado por este contexto.
                fogContributionsBySource.Clear();
                fogGeographicContributorsByCell.Clear();
                fogSensorContributorsByCell.Clear();
                InitializeFogRuntimeData(boardMap);
            }
            fogWarmupActivateMs +=
                (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;
            stepStartMs = Time.realtimeSinceStartupAsDouble;

            if (!fogOverlayInitialized ||
                fogCachedObserverSlotIndex != observerSlot.Value)
            {
                return false;
            }

            FogContributionSourceId sourceId =
                ResolveFogContributionSourceId(unit);
            int sourceStateHash = BuildFogUnitSourceStateHash(unit);
            if (fogContributionsBySource.TryGetValue(
                    sourceId,
                    out FogSourceContributionCacheEntry existing) &&
                existing != null &&
                existing.sourceStateHash == sourceStateHash)
            {
                existing.unitCacheKey = BuildFogUnitCacheKey(unit, boardMap);
            }
            else
            {
                UpdateFogVisibilityForUnit(
                    unit,
                    boardMap,
                    out _,
                    out _,
                    out _,
                    updateVisual: false);
                warmed = true;
            }

            fogWarmupWorkMs +=
                (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;
            stepStartMs = Time.realtimeSinceStartupAsDouble;

            StoreFogContributionRuntimeForSlot(observerSlot);
            fogWarmupStoreMs +=
                (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;
        }
        finally
        {
            stepStartMs = Time.realtimeSinceStartupAsDouble;
            ExitFogObserverScope(previous);
            TryActivateFogContributionRuntimeForSlot(
                runtimeOwnerBefore,
                boardMap);
            fogWarmupRestoreMs +=
                (Time.realtimeSinceStartupAsDouble - stepStartMs) * 1000d;
        }

        return warmed;
    }

    private bool TryFinalizeFogCacheWarmupForInactiveSlot(
        PlayerSlotId observerSlot,
        PlayerSlotId hostSlot,
        Tilemap boardMap)
    {
        if (boardMap == null ||
            ActiveSlotId != hostSlot ||
            fogCachedObserverSlotIndex != hostSlot.Value)
        {
            return false;
        }

        PlayerSlotId runtimeOwnerBefore =
            PlayerSlotId.FromIndex(fogCachedObserverSlotIndex);
        FogUpdateContext warmupContext = new FogUpdateContext(
            observerSlot,
            observerSlot,
            observerSlot,
            publishGameplayData: false,
            publishVisuals: false,
            recordExplorationMemory: false,
            recordIntel: false);
        bool synchronized;
        try
        {
            synchronized =
                TrySynchronizeFogRuntimeAtTurnStart(warmupContext, boardMap);
        }
        finally
        {
            TryActivateFogContributionRuntimeForSlot(
                runtimeOwnerBefore,
                boardMap);
        }
        return synchronized;
    }

    private int CountFogGeographicOnlyCells()
    {
        int count = 0;
        foreach (KeyValuePair<Vector3Int, int> entry in fogGeographicContributorsByCell)
        {
            if (entry.Value > 0 &&
                (!fogSensorContributorsByCell.TryGetValue(entry.Key, out int sensors) || sensors <= 0))
            {
                count++;
            }
        }
        return count;
    }

    private int CountFogContributionSources(FogContributionSourceType type)
    {
        int count = 0;
        foreach (FogContributionSourceId sourceId in fogContributionsBySource.Keys)
        {
            if (sourceId.type == type)
                count++;
        }
        return count;
    }


    public void RefreshFogOfWarForSlot(PlayerSlotId observerSlot)
    {
        if (!IsValidPlayerSlot(observerSlot))
            return;
        PlayerSlotId gameplaySlot = IsValidPlayerSlot(ActiveSlotId)
            ? ActiveSlotId
            : observerSlot;
        ExecuteFogRefreshContext(
            CreateFogUpdateContext(
                gameplaySlot,
                observerSlot,
                observerSlot,
                FogOfWarRefreshMode.FullVisual));
    }
    public void NotifyUnitReachedHasAct(UnitManager unit)
    {
        ProcessCommittedUnitFog(
            unit,
            raiseActedEvent: true,
            requireHasActed: true,
            requireFullRefresh: false,
            CommittedBoardChangeKind.UnitActed);
    }

    // Spawn de UMA unidade (compra): a unidade e do time ativo e so SOMA visao — nenhuma outra
    // unidade muda. Usa o delta incremental (como o movimento), que adiciona a visao da nova
    // unidade e republica o snapshot de deteccao, em vez de recolher TODAS. Sem isto, um spawn
    // custava um refresh completo O(unidades) (~4s em mapa com muitos aereos de visao grande),
    // porque a chave do cache de visao inclui o globalBoardRevision que o spawn incrementa. Rede
    // de seguranca: se o cache nao estiver pronto p/ o time ativo, o proprio caminho incremental
    // cai em full (ver ProcessCommittedUnitFog). Multi-unidade enumerada,
    // como desembarque, tambem segue pelo delta incremental.
    public void NotifyCommittedUnitSpawnedForFog(UnitManager unit)
    {
        ProcessCommittedUnitFog(
            unit,
            raiseActedEvent: false,
            requireHasActed: false,
            requireFullRefresh: false,
            CommittedBoardChangeKind.UnitSpawned);
    }

    public void NotifyCommittedMultiUnitBoardChangeForFog(
        UnitManager contextUnit,
        IReadOnlyList<UnitManager> changedUnits)
    {
        if (!Application.isPlaying
            || SuppressFogOfWarRefresh
            || !debugFogOfWarEnabled
            || !enableTotalWar
            || activeTeamId < 0)
        {
            return;
        }

        CommittedBoardDelta delta = new CommittedBoardDelta();
        if (contextUnit != null
            && contextUnit.gameObject.activeInHierarchy
            && contextUnit.SlotIndex == ActiveSlotId.Value)
        {
            delta.AddUnit(
                contextUnit,
                CommittedBoardChangeKind.MultiUnitChanged);
        }

        if (changedUnits != null)
        {
            for (int i = 0; i < changedUnits.Count; i++)
            {
                UnitManager changed = changedUnits[i];
                if (changed == null
                    || !changed.gameObject.activeInHierarchy
                    || changed.SlotIndex != ActiveSlotId.Value)
                {
                    continue;
                }

                delta.AddUnit(
                    changed,
                    CommittedBoardChangeKind.MultiUnitChanged);
            }
        }

        if (turnStateManager != null
            && turnStateManager.TryGetCommittedMovementPath(
                out _,
                out Vector3Int originCell,
                out Vector3Int destinationCell))
        {
            delta.AddChangedCell(originCell);
            delta.AddChangedCell(destinationCell);
        }

        // Multi-unidade nao significa full refresh. No desembarque, cada
        // passageiro passa de embarcado (sem fonte) para uma fonte confirmada
        // independente; o transportador apenas atualiza a propria fonte.
        // O delta fica pendente ate Neutral e entao atualiza somente essas
        // contribuicoes.
        SubmitCommittedBoardDelta(delta);
    }

    private void ProcessCommittedUnitFog(
        UnitManager unit,
        bool raiseActedEvent,
        bool requireHasActed,
        bool requireFullRefresh,
        CommittedBoardChangeKind changeKind)
    {
        if (!Application.isPlaying)
            return;
        if (raiseActedEvent && unit != null)
            OnUnitActedStateChanged?.Invoke(unit);
        if (SuppressFogOfWarRefresh)
            return;
        if (!debugFogOfWarEnabled)
            return;
        if (!enableTotalWar)
            return;
        if (unit == null || !unit.gameObject.activeInHierarchy)
            return;
        if (requireHasActed && !unit.HasActed)
            return;
        if (activeTeamId < 0)
            return;
        if (unit.SlotIndex != ActiveSlotId.Value)
            return;

        CommittedBoardDelta delta = new CommittedBoardDelta();
        delta.AddUnit(unit, changeKind, requireHasActed);
        if (turnStateManager != null &&
            turnStateManager.TryGetCommittedMovementPath(
                out _,
                out Vector3Int originCell,
                out Vector3Int destinationCell))
        {
            delta.AddChangedCell(originCell);
            delta.AddChangedCell(destinationCell);
        }
        if (requireFullRefresh)
            delta.RequireFullRefresh(changeKind);
        SubmitCommittedBoardDelta(delta);
    }

    private void SubmitCommittedBoardDelta(CommittedBoardDelta delta)
    {
        if (delta == null || delta.IsEmpty)
            return;

        // O delta ja descreve uma mutacao comprometida, mas sua publicacao
        // definitiva continua bloqueada ate a FSM retornar a Neutral.
        if (turnStateManager != null && turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        {
            pendingCommittedBoardDelta ??= new CommittedBoardDelta();
            pendingCommittedBoardDelta.MergeFrom(delta);
            return;
        }

        ApplyCommittedBoardDelta(delta);
    }

    private void ApplyCommittedBoardDelta(CommittedBoardDelta delta)
    {
        if (delta == null || delta.IsEmpty)
            return;

        if (enableFogStepPerfLogs)
        {
            Debug.Log(
                $"[FoW][CommittedDelta] kinds={delta.ChangeKind} " +
                $"units={delta.ChangedUnits.Count} cells={delta.ChangedCells.Count} " +
                $"full={delta.RequireFullFogRefresh} reconcile={delta.RequireSourceReconciliation}");
        }

        if (fogOfWarTilemap == null)
            TryAutoAssignFogOfWarReferences();
        if (fogOfWarTilemap == null)
            return;

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return;

        ValidateFogOfWarSortingLayer();
        // Refresh completo: reservado para mutacoes cujo delta nao consegue
        // enumerar as fontes afetadas. Spawn de UMA unidade (compra) e
        // movimento usam o delta incremental abaixo — a unidade nova/movida so soma/atualiza a
        // propria visao, e o snapshot de deteccao e republicado, mantendo overlay e consultas de
        // LOS consistentes sem varrer o time inteiro.
        UnitManager contextUnit = null;
        for (int i = 0; i < delta.ChangedUnits.Count; i++)
        {
            UnitManager candidate = delta.ChangedUnits[i];
            if (candidate != null)
            {
                contextUnit = candidate;
                break;
            }
        }

        if (delta.RequireFullFogRefresh)
        {
            RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.FullVisual);
            TryPlaySkillDetectionSfxForActedUnit(contextUnit, boardMap);
            TryRefreshDetectedPersistenceForActedUnit(contextUnit, boardMap);
            return;
        }

        if (delta.RequireSourceReconciliation)
        {
            if (!TryRefreshFogOfWarForTurnStartIncremental())
                RefreshFogOfWarForActiveTeam();
            return;
        }

        for (int i = 0; i < delta.ChangedUnits.Count; i++)
        {
            UnitManager unit = delta.ChangedUnits[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;
            if (delta.RequiresHasActed(unit) && !unit.HasActed)
                continue;
            HashSet<Vector3Int> affectedTargetCells =
                new HashSet<Vector3Int>(delta.ChangedCells);
            ApplyCommittedUnitFog(unit, boardMap, affectedTargetCells);
        }
    }

    private void ApplyCommittedUnitFog(
        UnitManager unit,
        Tilemap boardMap,
        HashSet<Vector3Int> affectedTargetCells)
    {
        if ((fogCachedObserverSlotIndex != ActiveSlotId.Value || !fogOverlayInitialized) &&
            !TryActivateFogContributionRuntimeForSlot(ActiveSlotId, boardMap))
        {
            RefreshFogOfWarForActiveTeam();
            TryPlaySkillDetectionSfxForActedUnit(unit, boardMap);
            TryRefreshDetectedPersistenceForActedUnit(unit, boardMap);
            return;
        }

        double incrementalStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        double stageStartMs = incrementalStartMs;
        PlayerSlotId gameplaySlot = ActiveSlotId;
        PlayerSlotId presentationSlot = TryResolveFogPresentationSlot(out PlayerSlotId resolvedPresentationSlot)
            ? resolvedPresentationSlot
            : gameplaySlot;
        bool splitPresentation = presentationSlot != gameplaySlot;
        FogUpdateContext gameplayContext = CreateFogUpdateContext(
            gameplaySlot,
            gameplaySlot,
            presentationSlot,
            splitPresentation ? FogOfWarRefreshMode.DataOnly : FogOfWarRefreshMode.FullVisual);
        HashSet<Vector3Int> presentationTargetCells =
            new HashSet<Vector3Int>(affectedTargetCells);
        FogObserverScopeState incrementalPrevious = EnterFogObserverScope(gameplayContext);
        try
        {
            UpdateFogVisibilityForUnit(
            unit,
            boardMap,
            out double collectMs,
            out int visibleCellsCollected,
            out bool collectExecuted,
            updateVisual: gameplayContext.publishVisuals,
            affectedTargetCells);
        double updateCacheMs = enableFogStepPerfLogs ? (Time.realtimeSinceStartupAsDouble - stageStartMs) * 1000d : 0d;
        UnitManager[] snapshotUnits = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude);
        // MarkAsActed e o ponto de compromisso da acao. A uniao especializada do modo ALL
        // so pode revelar o novo ponto de observacao agora, nunca ao fim do movimento provisório.
        stageStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        if (gameplayContext.publishVisuals && fogOfWarVisionMode == FogOfWarVisionMode.All)
            RenderFogOverlayFromRuntimeCache(boardMap);
        double renderOverlayMs = enableFogStepPerfLogs ? (Time.realtimeSinceStartupAsDouble - stageStartMs) * 1000d : 0d;
        stageStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        if (gameplayContext.publishGameplayData)
        {
            // affectedTargetCells: null de proposito. No ponto de compromisso o
            // snapshot publica a visibilidade de TODAS as unidades, nao so das
            // que estao em celula que mudou de revelacao.
            //
            // E deste snapshot que ApplyFogDetectedContactPresentation le para
            // decidir o contato cinza sobre o preto. Com o filtro por celula, um
            // caca detectado por radar — que nao revela terreno — nunca entrava
            // no publish e ficava invisivel no tabuleiro mesmo com o PodeDetectar
            // afirmando que o viu.
            PublishFogGameplaySnapshot(
                gameplayContext.observerSlot.Value,
                boardMap,
                snapshotUnits,
                gameplayContext.recordExplorationMemory,
                affectedTargetCells: null);
        }
        StoreFogContributionRuntimeForSlot(gameplaySlot);
        if (gameplayContext.publishVisuals)
        {
            // Ponto de compromisso: aqui o tabuleiro confirmado e reconstruido, e
            // a visibilidade de unidade precisa ser recalculada INTEIRA.
            //
            // O delta por celulas afetadas nao serve mais. Ele filtrava as
            // unidades por "a celula dela esta no conjunto que mudou de
            // revelacao", e isso so cobria a deteccao enquanto revelar hexagono
            // e detectar unidade eram a mesma coisa. Depois da separacao, um
            // radar que detecta a 7 sem revelar terreno nao poe a celula do alvo
            // no conjunto — e o inimigo ficava com o valor velho para sempre.
            //
            // A lentidao que motivou o delta era refresh cheio a CADA PASSO do
            // movimento provisorio; isto aqui roda uma vez por acao comprometida.
            RefreshRuntimeUnitFogVisibility();
        }
        double runtimeVisibilityMs = enableFogStepPerfLogs ? (Time.realtimeSinceStartupAsDouble - stageStartMs) * 1000d : 0d;
        stageStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        if (gameplayContext.recordIntel)
            AIIntelLedger.RecordVisibleContactsForSlot(
                gameplaySlot,
                currentTurn,
                this,
                affectedTargetCells);
        double intelMs = enableFogStepPerfLogs ? (Time.realtimeSinceStartupAsDouble - stageStartMs) * 1000d : 0d;
        stageStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        TryPlaySkillDetectionSfxForActedUnit(unit, boardMap);
        double detectionSfxMs = enableFogStepPerfLogs ? (Time.realtimeSinceStartupAsDouble - stageStartMs) * 1000d : 0d;
        stageStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        TryRefreshDetectedPersistenceForActedUnit(unit, boardMap);
        double detectedPersistenceMs = enableFogStepPerfLogs ? (Time.realtimeSinceStartupAsDouble - stageStartMs) * 1000d : 0d;
        if (splitPresentation &&
            !TryRefreshFogPresentationAfterForeignUnitCommit(
                CreateFogUpdateContext(
                    gameplaySlot,
                    presentationSlot,
                    presentationSlot,
                    FogOfWarRefreshMode.FullVisual),
                boardMap,
                snapshotUnits,
                deltaTargetCells: presentationTargetCells))
        {
            RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.FullVisual);
            return;
        }
        stageStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;
        OnFogOfWarUpdated?.Invoke();
        double callbacksMs = enableFogStepPerfLogs ? (Time.realtimeSinceStartupAsDouble - stageStartMs) * 1000d : 0d;
        if (enableFogStepPerfLogs)
        {
            double totalMs = (Time.realtimeSinceStartupAsDouble - incrementalStartMs) * 1000d;
            Debug.Log(
                $"[FoW][Perf][Incremental] unit={unit.name} total={totalMs:F3}ms " +
                $"updateCache={updateCacheMs:F3}ms collect={collectMs:F3}ms collected={collectExecuted} cells={visibleCellsCollected} " +
                $"render={renderOverlayMs:F3}ms visibility={runtimeVisibilityMs:F3}ms intel={intelMs:F3}ms " +
                $"detectionSfx={detectionSfxMs:F3}ms persistence={detectedPersistenceMs:F3}ms callbacks={callbacksMs:F3}ms " +
                $"splitPresentation={splitPresentation}");
        }
        }
        finally
        {
            ExitFogObserverScope(incrementalPrevious);
        }
    }

    private bool TryRefreshFogPresentationAfterForeignUnitCommit(
        FogUpdateContext context,
        Tilemap boardMap,
        UnitManager[] snapshotUnits,
        HashSet<Vector3Int> deltaTargetCells)
    {
        if (!context.gameplaySlot.IsValid || !context.observerSlot.IsValid ||
            context.gameplaySlot == context.observerSlot ||
            context.observerSlot != context.presentationSlot ||
            !context.publishVisuals ||
            boardMap == null)
        {
            return false;
        }

        FogObserverScopeState previous = EnterFogObserverScope(context);
        try
        {
            if (!TryActivateFogContributionRuntimeForSlot(context.observerSlot, boardMap))
                return false;

            // As fontes humanas nao mudaram; apenas o alvo inimigo mudou de hex.
            if (context.publishGameplayData)
            {
                PublishFogGameplaySnapshot(
                    context.observerSlot.Value,
                    boardMap,
                    snapshotUnits,
                    context.recordExplorationMemory,
                    deltaTargetCells);
            }
            StoreFogContributionRuntimeForSlot(context.observerSlot);
            if (context.publishVisuals)
            {
                RefreshRuntimeUnitFogVisibilityForCells(deltaTargetCells);
                RenderFogOverlayFromRuntimeCache(boardMap);
            }
            if (context.recordIntel)
                AIIntelLedger.RecordVisibleContactsForSlot(
                    context.observerSlot,
                    currentTurn,
                    this,
                    deltaTargetCells);
            return true;
        }
        finally
        {
            ExitFogObserverScope(previous);
        }
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
            if (unit.SlotIndex != ActiveSlotId.Value)
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

        int detectorSlotIndex = observer.SlotIndex;
        target.RegisterStealthReveal(detectorSlotIndex);
        target.AddCurrentlyObservedByTeam(detectorSlotIndex);
        target.RefreshRuntimeVisualState();

        // Jornal do Comandante — novo contato: deteccao PASSIVA durante o turno
        // alheio (meus sensores flagraram algo enquanto o inimigo se movia).
        // Deteccao no proprio turno foi vista ao vivo e nao entra.
        if (detectorSlotIndex != ActiveSlotId.Value &&
            observer.SlotIndex != target.SlotIndex &&
            detectorSlotIndex >= 0)
        {
            Vector3Int contactCell = target.CurrentCellPosition;
            contactCell.z = 0;
            PlayerSlotId observerSlot = PlayerSlotId.FromIndex(observer.SlotIndex);
            if (!HasTurnBriefingEntry(observerSlot, TurnBriefingCategory.NewContact, contactCell, currentTurn))
            {
                ReportTurnBriefingEvent(
                    observerSlot,
                    TurnBriefingCategory.NewContact,
                    ResolveRuntimeUnitName(target),
                    $"detectado por {ResolveRuntimeUnitName(observer)}",
                    contactCell);
            }
        }
    }

    // Dedupe barato do briefing: mesma categoria, mesmo destinatario, mesma
    // celula e mesmo turno = um evento so (a deteccao pode disparar varias
    // vezes por movimento).
    private bool HasTurnBriefingEntry(PlayerSlotId slot, TurnBriefingCategory category, Vector3Int cell, int turnNumber)
    {
        cell.z = 0;
        for (int i = 0; i < turnBriefingLedger.Count; i++)
        {
            TurnBriefingEventSaveData evt = turnBriefingLedger[i];
            if (evt == null)
                continue;
            int eventSlotIndex = evt.slotIndex;
            if (!IsValidPlayerSlotIndex(eventSlotIndex) &&
                TryGetUniqueSlotForTeam((TeamId)evt.teamId, out PlayerSlotId migratedSlot))
                eventSlotIndex = migratedSlot.Value;
            if (eventSlotIndex == slot.Value &&
                evt.category == (int)category &&
                evt.cellX == cell.x &&
                evt.cellY == cell.y &&
                evt.turnNumber == turnNumber)
            {
                return true;
            }
        }

        return false;
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
                RefreshRuntimeUnitFogVisibilityForUnit(actedUnit);
            }

            return;
        }

        bool observedTeamsCleared = actedUnit.ClearCurrentlyObservedByTeams();
        if (hadRevealBefore)
        {
            actedUnit.ClearStealthRevealState();
            actedUnit.RefreshRuntimeVisualState();
            RefreshRuntimeUnitFogVisibilityForUnit(actedUnit);
            if (ShouldLogAindaMeVeRuntime)
                Debug.Log($"[AindaMeVe][Runtime][Clear] target={actedUnit.name} -> nenhum inimigo detectando.");
            return;
        }

        if (observedTeamsCleared)
            actedUnit.RefreshRuntimeVisualState();
    }

    private void RefreshRuntimeUnitFogVisibilityForUnit(UnitManager unit)
    {
        if (unit == null)
            return;

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        RefreshRuntimeUnitFogVisibilityForCells(
            new HashSet<Vector3Int> { cell });
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
        int targetSlotIndex = target.SlotIndex;

        int maxRange = 1;
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager observer = units[i];
            if (observer == null || observer == target || !observer.gameObject.activeInHierarchy || observer.IsEmbarked)
                continue;
            if (observer.SlotIndex == targetSlotIndex)
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

        int ownerSlotIndex = target.SlotIndex;
        for (int slotIndex = 0; slotIndex < players.Count; slotIndex++)
        {
            if (slotIndex == ownerSlotIndex)
                continue;
            if (target.IsStealthRevealedForTeam(slotIndex, currentTurn))
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
            if (observer.SlotIndex == target.SlotIndex)
                continue;
            if (!IsUnitOnBoard(observer, map))
                continue;

            Vector3Int observerCell = observer.CurrentCellPosition;
            observerCell.z = 0;
            if (!cellsInRadius.Contains(observerCell))
                continue;

            int observerSlotIndex = observer.SlotIndex;
            if (!IsValidPlayerSlotIndex(observerSlotIndex))
                continue;

            // Lock de camada pendente equivale a "revelada": anula stealth como atirar anula.
            bool enforceStealthValidation = enableStealthValidation && !target.HasFiredThisTurn && !target.HasPendingForcedLayerLock;
            bool canObserveTarget = PodeDetectarSensor.IsTargetObservedByTeam(
                target,
                observerSlotIndex,
                map,
                ResolveFogTerrainDatabase(),
                ResolveFogDpqAirHeightConfig(),
                enableLosValidation,
                enableSpotter: false,
                enforceStealthValidation);
            if (!canObserveTarget)
                continue;

            observerTeamIds.Add(observerSlotIndex);
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
        int observerSlotIndex = ActiveSlotId.Value;
        if (fogCachedObserverSlotIndex == observerSlotIndex &&
            fogUnitVisibilityByCacheIndex.TryGetValue(cacheIndex, out bool cachedVisible))
        {
            return cachedVisible;
        }

        if (TryGetFogGameplaySnapshot(observerSlotIndex, out FogSlotGameplaySnapshot snapshot) &&
            snapshot.unitVisibility.TryGetValue(cacheIndex, out bool snapshotVisible))
        {
            return snapshotVisible;
        }

        return ComputeIsUnitVisibleForActiveTeam(unit);
    }

    public bool IsUnitOwnedByFogPresentationObserver(UnitManager unit)
    {
        if (unit == null)
            return false;

        PlayerSlotId observerSlot = ActiveSlotId;
        if (TryResolveFogPresentationSlot(out PlayerSlotId presentationSlot))
            observerSlot = presentationSlot;
        return IsValidPlayerSlot(observerSlot) && unit.SlotIndex == observerSlot.Value;
    }

    /// <summary>
    /// Consulta exclusivamente o snapshot de visibilidade ja publicado.
    /// Durante uma acao provisoria, ausencia no cache significa oculto: nunca
    /// recalcula usando a posicao runtime temporaria da unidade que esta movendo.
    /// </summary>
    public bool IsUnitVisibleForActiveTeamConfirmed(UnitManager unit)
    {
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return true;

        if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
            return false;

        int observerSlotIndex = ActiveSlotId.Value;
        if (unit.SlotIndex == observerSlotIndex)
            return true;

        int cacheIndex = ResolveFogCacheIndex(unit);
        if (TryGetFogGameplaySnapshot(observerSlotIndex, out FogSlotGameplaySnapshot snapshot))
            return snapshot.unitVisibility.TryGetValue(cacheIndex, out bool visible) && visible;

        return fogCachedObserverSlotIndex == observerSlotIndex &&
               fogUnitVisibilityByCacheIndex.TryGetValue(cacheIndex, out bool cachedVisible) &&
               cachedVisible;
    }

    /// <summary>
    /// Copia o conhecimento ja publicado do runtime. Nunca recalcula sensores.
    /// </summary>
    public bool TryCopyConfirmedFogKnowledgeSnapshotForSlot(
        PlayerSlotId observerSlot,
        Tilemap boardMap,
        out FogKnowledgeSnapshot knowledge,
        out string reason)
    {
        knowledge = null;
        reason = string.Empty;
        if (!IsValidPlayerSlot(observerSlot) || boardMap == null)
        {
            reason = "Slot ou tabuleiro invalido.";
            return false;
        }

        knowledge = new FogKnowledgeSnapshot(observerSlot, boardMap)
        {
            SourceHash = ThreatRevisionTracker.GlobalBoardRevision
        };

        bool visibilityDisabled = !debugFogOfWarEnabled || !enableTotalWar;
        FogSlotGameplaySnapshot runtimeSnapshot = null;
        if (!visibilityDisabled &&
            !TryGetFogGameplaySnapshot(observerSlot.Value, out runtimeSnapshot))
        {
            knowledge = null;
            reason = "Snapshot confirmado de FOW ainda nao foi publicado.";
            return false;
        }

        if (visibilityDisabled)
        {
            var allBoardCells = new List<Vector3Int>();
            CollectBoardCells(boardMap, allBoardCells);
            knowledge.GeographicallyVisibleCells.UnionWith(allBoardCells);
            knowledge.SensorCoveredCells.UnionWith(allBoardCells);
            knowledge.KnownCells.UnionWith(allBoardCells);
        }
        else
        {
            knowledge.GeographicallyVisibleCells.UnionWith(
                runtimeSnapshot.geographicallyVisibleCells);
            knowledge.SensorCoveredCells.UnionWith(
                runtimeSnapshot.sensorCoveredCells);
            knowledge.KnownCells.UnionWith(runtimeSnapshot.knownCells);
            knowledge.GeographicOnlyCells.UnionWith(
                runtimeSnapshot.geographicOnlyCells);
            if (fogExploredCellsBySlot.TryGetValue(
                    observerSlot.Value,
                    out HashSet<Vector3Int> explored))
            {
                knowledge.KnownCells.UnionWith(explored);
            }
            CopyRuntimeFogVisibilityContributors(
                observerSlot.Value,
                knowledge);
        }

        IReadOnlyList<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager target = units[i];
            if (target == null || !target.gameObject.activeInHierarchy ||
                target.IsEmbarked || target.IsDead ||
                !PlayerSlotRelations.AreEnemies(
                    observerSlot.Value,
                    target.SlotIndex))
            {
                continue;
            }

            bool visible = visibilityDisabled;
            if (!visible)
            {
                int cacheIndex = ResolveFogCacheIndex(target);
                visible = runtimeSnapshot.unitVisibility.TryGetValue(
                    cacheIndex,
                    out bool snapshotVisible) && snapshotVisible;
            }
            if (visible)
                knowledge.VisibleEnemyUnits.Add(target);
        }

        reason =
            $"snapshot confirmado slot={observerSlot.Value} " +
            $"known={knowledge.KnownCells.Count} " +
            $"visibleEnemies={knowledge.VisibleEnemyUnits.Count}";
        return true;
    }

    /// <summary>
    /// Cozinha manualmente a rodada zero de todos os slots e grava o resultado
    /// no MatchController. Nao e chamado por OnValidate, pintura, spawner ou
    /// ferramenta de auditoria.
    /// </summary>
    public bool TryCookRoundZeroFogForAllSlots(out string result)
    {
        result = string.Empty;
        if (Application.isPlaying)
        {
            result = "O bake da rodada 0 so pode ser escrito no Edit Mode.";
            return false;
        }
        if (fogOfWarTilemap == null)
            TryAutoAssignFogOfWarReferences();
        Tilemap boardMap = ResolveFogBoardTilemap();
        TerrainDatabase terrain = ResolveFogTerrainDatabase();
        if (fogOfWarTilemap == null || boardMap == null || terrain == null)
        {
            result =
                "Fog Tilemap, Tilemap do tabuleiro ou Terrain Database indisponivel.";
            return false;
        }
        if (players == null || players.Count == 0)
        {
            result = "A partida nao possui slots para cozinhar.";
            return false;
        }

        var cooked = new List<FogRoundZeroSlotBake>(players.Count);
        int totalKnown = 0;
        int totalContacts = 0;
        int totalSources = 0;
        for (int slotIndex = 0; slotIndex < players.Count; slotIndex++)
        {
            PlayerSlotId observerSlot = PlayerSlotId.FromIndex(slotIndex);
            var request = new FogKnowledgeBuildRequest
            {
                ObserverSlot = observerSlot,
                BoardMap = boardMap,
                TerrainDatabase = terrain,
                DpqAirHeightConfig = ResolveFogDpqAirHeightConfig(),
                EnableLos = enableLosValidation,
                EnableStealth = enableStealthValidation
            };
            if (!FogKnowledgeSnapshotBuilder.TryBuild(
                    request,
                    out FogKnowledgeSnapshot knowledge,
                    out string reason))
            {
                result = $"Slot {slotIndex} falhou: {reason}";
                return false;
            }

            // O builder acima e a autoridade da fotografia de conhecimento.
            // Esta passagem DataOnly produz tambem as contribuicoes por fonte,
            // no formato que o runtime/save ja sabe validar e restaurar.
            ExecuteFogRefreshContext(new FogUpdateContext(
                observerSlot,
                observerSlot,
                observerSlot,
                publishGameplayData: false,
                publishVisuals: false,
                recordExplorationMemory: false,
                recordIntel: false));
            if (!fogOverlayInitialized || fogCachedObserverSlotIndex != slotIndex)
            {
                result = $"Slot {slotIndex} falhou ao produzir contribuicoes DataOnly.";
                return false;
            }

            FogRoundZeroSlotBake bake = BuildRoundZeroSlotBake(
                knowledge,
                boardMap,
                request.EnableLos,
                request.EnableStealth);
            cooked.Add(bake);
            totalKnown += bake.knownCells.Count;
            totalContacts += bake.visibleEnemyUnits.Count;
            totalSources += bake.sourceContributions.Count;
        }

        // A lista serializada so e substituida depois que todos os slots
        // terminaram. Uma falha intermediaria preserva o bake anterior inteiro.
        fogRoundZeroBakes = cooked;
        result =
            $"Rodada 0 cozida: slots={cooked.Count}, fontes={totalSources}, " +
            $"hexes conhecidos={totalKnown}, contatos={totalContacts}.";
        return true;
    }

    public bool TryCopyRoundZeroFogKnowledgeSnapshotForSlot(
        PlayerSlotId observerSlot,
        Tilemap boardMap,
        out FogKnowledgeSnapshot knowledge,
        out string reason)
    {
        knowledge = null;
        reason = string.Empty;
        if (!IsValidPlayerSlot(observerSlot) || boardMap == null)
        {
            reason = "Slot ou tabuleiro invalido.";
            return false;
        }

        FogRoundZeroSlotBake bake = FindRoundZeroFogBake(observerSlot.Value);
        if (bake == null || bake.formatVersion != FogRoundZeroSlotBake.CurrentFormatVersion)
        {
            reason =
                $"Nao existe FOW de rodada 0 cozido para o slot {observerSlot.Value}. " +
                "Selecione o MatchController e use 'Cozinhar FOW da Rodada 0'.";
            return false;
        }
        if (bake.boardMap != null && bake.boardMap != boardMap)
        {
            reason = "O bake da rodada 0 pertence a outro Tilemap.";
            return false;
        }

        knowledge = new FogKnowledgeSnapshot(observerSlot, boardMap)
        {
            SourceHash = bake.sourceHash
        };
        UnionSerializedCells(bake.geographicallyVisibleCells, knowledge.GeographicallyVisibleCells);
        UnionSerializedCells(bake.sensorCoveredCells, knowledge.SensorCoveredCells);
        UnionSerializedCells(bake.knownCells, knowledge.KnownCells);
        UnionSerializedCells(bake.geographicOnlyCells, knowledge.GeographicOnlyCells);
        CopyBakedFogVisibilityContributors(
            bake.sourceContributions,
            knowledge);
        CopyValidUnits(bake.visibleEnemyUnits, knowledge.VisibleEnemyUnits);
        CopyValidUnits(
            bake.constructionDetectedTargets,
            knowledge.ConstructionDetectedTargets);

        if (bake.detectionByTarget != null)
        {
            for (int i = 0; i < bake.detectionByTarget.Count; i++)
            {
                FogRoundZeroDetectionBake saved = bake.detectionByTarget[i];
                if (saved == null || saved.target == null)
                    continue;
                var contributors = new List<UnitManager>();
                CopyValidUnits(saved.contributors, contributors);
                knowledge.DetectionContributorsByTarget[saved.target] = contributors;
            }
        }

        int currentHash = FogKnowledgeSnapshotBuilder.ComputeSourceHash(
            new FogKnowledgeBuildRequest
            {
                ObserverSlot = observerSlot,
                BoardMap = boardMap,
                TerrainDatabase = ResolveFogTerrainDatabase(),
                DpqAirHeightConfig = ResolveFogDpqAirHeightConfig(),
                EnableLos = bake.enableLos,
                EnableStealth = bake.enableStealth
            });
        bool stale = currentHash != bake.sourceHash;
        reason =
            $"rodada 0 manual slot={observerSlot.Value}, " +
            $"known={knowledge.KnownCells.Count}, " +
            $"visibleEnemies={knowledge.VisibleEnemyUnits.Count}, " +
            $"estado={(stale ? "Scene alterada depois do bake" : "atual")}; " +
            "nenhum recozimento automatico foi executado";
        return true;
    }

    /// <summary>
    /// Copia QUEM abriu cada hex a partir das contribuicoes confirmadas ja
    /// existentes. Nao recalcula sensores e nao publica estado de FOW.
    /// </summary>
    private void CopyRuntimeFogVisibilityContributors(
        int observerSlotIndex,
        FogKnowledgeSnapshot knowledge)
    {
        if (knowledge == null)
            return;

        IReadOnlyDictionary<
            FogContributionSourceId,
            FogSourceContributionCacheEntry> sources = null;
        if (observerSlotIndex == fogCachedObserverSlotIndex)
        {
            sources = fogContributionsBySource;
        }
        else if (fogContributionRuntimeBySlot.TryGetValue(
                     observerSlotIndex,
                     out FogSlotContributionRuntime runtime))
        {
            sources = runtime.sources;
        }

        if (sources == null || sources.Count == 0)
            return;

        Dictionary<int, UnitManager> unitsBySourceId =
            BuildFogUnitsBySourceId();
        foreach (KeyValuePair<
                     FogContributionSourceId,
                     FogSourceContributionCacheEntry> pair in sources)
        {
            if (pair.Key.type != FogContributionSourceType.Unit
                || pair.Value == null
                || !unitsBySourceId.TryGetValue(
                    pair.Key.instanceId,
                    out UnitManager contributor))
            {
                continue;
            }

            foreach (Vector3Int cell in pair.Value.geographicCells)
            {
                if (knowledge.GeographicallyVisibleCells.Contains(cell))
                    knowledge.AddVisibilityContributor(cell, contributor);
            }
        }
    }

    /// <summary>Mesmo vinculo, lido do bake manual da rodada zero.</summary>
    private static void CopyBakedFogVisibilityContributors(
        IReadOnlyList<FogSourceContributionSaveData> sources,
        FogKnowledgeSnapshot knowledge)
    {
        if (knowledge == null || sources == null || sources.Count == 0)
            return;

        Dictionary<int, UnitManager> unitsBySourceId =
            BuildFogUnitsBySourceId();
        for (int i = 0; i < sources.Count; i++)
        {
            FogSourceContributionSaveData source = sources[i];
            if (source == null
                || source.sourceType != (int)FogContributionSourceType.Unit
                || !unitsBySourceId.TryGetValue(
                    source.sourceInstanceId,
                    out UnitManager contributor)
                || source.geographicCells == null)
            {
                continue;
            }

            for (int cellIndex = 0;
                 cellIndex < source.geographicCells.Count;
                 cellIndex++)
            {
                Vector3Int cell = source.geographicCells[cellIndex];
                if (knowledge.GeographicallyVisibleCells.Contains(cell))
                    knowledge.AddVisibilityContributor(cell, contributor);
            }
        }
    }

    private static Dictionary<int, UnitManager> BuildFogUnitsBySourceId()
    {
        var result = new Dictionary<int, UnitManager>();
        IReadOnlyList<UnitManager> units =
            Application.isPlaying && UnitManager.AllActive.Count > 0
                ? UnitManager.AllActive
                : UnityEngine.Object.FindObjectsByType<UnitManager>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit != null)
                result[ResolveFogCacheIndex(unit)] = unit;
        }
        return result;
    }

    public string DescribeRoundZeroFogBake()
    {
        if (fogRoundZeroBakes == null || fogRoundZeroBakes.Count == 0)
            return "Rodada 0 ainda nao foi cozida.";

        int sources = 0;
        int contacts = 0;
        string cookedAt = string.Empty;
        for (int i = 0; i < fogRoundZeroBakes.Count; i++)
        {
            FogRoundZeroSlotBake bake = fogRoundZeroBakes[i];
            if (bake == null)
                continue;
            sources += bake.sourceContributions?.Count ?? 0;
            contacts += bake.visibleEnemyUnits?.Count ?? 0;
            if (string.IsNullOrEmpty(cookedAt))
                cookedAt = bake.cookedAtUtc;
        }
        return
            $"{fogRoundZeroBakes.Count} slot(s), {sources} fonte(s), " +
            $"{contacts} contato(s). UTC: {cookedAt}";
    }

    private FogRoundZeroSlotBake BuildRoundZeroSlotBake(
        FogKnowledgeSnapshot knowledge,
        Tilemap boardMap,
        bool enableLos,
        bool enableStealth)
    {
        var bake = new FogRoundZeroSlotBake
        {
            observerSlotIndex = knowledge.ObserverSlot.Value,
            sourceHash = knowledge.SourceHash,
            sourceCacheFormat = FogSourceCacheFormatVersion,
            sourceCacheConfigHash = BuildFogSourceCacheConfigHash(boardMap),
            enableLos = enableLos,
            enableStealth = enableStealth,
            cookedAtUtc = DateTime.UtcNow.ToString("O"),
            boardMap = boardMap
        };
        AppendFogSourceContributionsForSave(
            knowledge.ObserverSlot.Value,
            fogContributionsBySource,
            bake.sourceContributions);
        bake.geographicallyVisibleCells.AddRange(knowledge.GeographicallyVisibleCells);
        bake.sensorCoveredCells.AddRange(knowledge.SensorCoveredCells);
        bake.knownCells.AddRange(knowledge.KnownCells);
        bake.geographicOnlyCells.AddRange(knowledge.GeographicOnlyCells);
        SortFogCellList(bake.geographicallyVisibleCells);
        SortFogCellList(bake.sensorCoveredCells);
        SortFogCellList(bake.knownCells);
        SortFogCellList(bake.geographicOnlyCells);
        bake.visibleEnemyUnits.AddRange(knowledge.VisibleEnemyUnits);
        bake.constructionDetectedTargets.AddRange(knowledge.ConstructionDetectedTargets);
        foreach (KeyValuePair<UnitManager, List<UnitManager>> pair in
                 knowledge.DetectionContributorsByTarget)
        {
            var saved = new FogRoundZeroDetectionBake { target = pair.Key };
            if (pair.Value != null)
                saved.contributors.AddRange(pair.Value);
            bake.detectionByTarget.Add(saved);
        }
        return bake;
    }

    private FogRoundZeroSlotBake FindRoundZeroFogBake(int observerSlotIndex)
    {
        if (fogRoundZeroBakes == null)
            return null;
        for (int i = 0; i < fogRoundZeroBakes.Count; i++)
        {
            FogRoundZeroSlotBake bake = fogRoundZeroBakes[i];
            if (bake != null && bake.observerSlotIndex == observerSlotIndex)
                return bake;
        }
        return null;
    }

    private static void UnionSerializedCells(
        IList<Vector3Int> source,
        HashSet<Vector3Int> destination)
    {
        if (source == null || destination == null)
            return;
        for (int i = 0; i < source.Count; i++)
            destination.Add(source[i]);
    }

    private static void CopyValidUnits(
        IList<UnitManager> source,
        ICollection<UnitManager> destination)
    {
        if (source == null || destination == null)
            return;
        for (int i = 0; i < source.Count; i++)
        {
            UnitManager unit = source[i];
            if (unit != null && unit.gameObject.activeInHierarchy &&
                !unit.IsEmbarked && !unit.IsDead && !destination.Contains(unit))
            {
                destination.Add(unit);
            }
        }
    }

    private void RestoreRoundZeroFogBakesForRuntime()
    {
        if (!Application.isPlaying || fogRoundZeroBakes == null ||
            fogRoundZeroBakes.Count == 0 || !debugFogOfWarEnabled ||
            !enableTotalWar)
        {
            return;
        }

        int restored = 0;
        for (int i = 0; i < fogRoundZeroBakes.Count; i++)
        {
            FogRoundZeroSlotBake bake = fogRoundZeroBakes[i];
            if (bake == null || bake.formatVersion != FogRoundZeroSlotBake.CurrentFormatVersion ||
                bake.sourceContributions == null || bake.sourceContributions.Count == 0)
            {
                continue;
            }

            PlayerSlotId slot = PlayerSlotId.FromIndex(bake.observerSlotIndex);
            if (TryRestoreFogSourceRuntimeForSlotFromSave(
                    slot,
                    bake.observerSlotIndex,
                    bake.sourceCacheFormat,
                    bake.sourceCacheConfigHash,
                    bake.sourceContributions,
                    out string restoreResult))
            {
                restored++;
            }
            else if (enableFogValidationLogs)
            {
                Debug.LogWarning(
                    $"[FoW][RoundZeroBake] slot={bake.observerSlotIndex} " +
                    $"rejected={restoreResult}; runtime fara o fallback normal.");
            }
        }

        if (enableFogStepPerfLogs)
        {
            Debug.Log(
                $"[FoW][RoundZeroBake] restored={restored}/{fogRoundZeroBakes.Count}");
        }
    }

    public bool IsUnitVisibleForSlot(UnitManager unit, PlayerSlotId observerSlot)
    {
        if (!debugFogOfWarEnabled)
            return true;

        if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
            return false;

        if (!IsValidPlayerSlot(observerSlot))
            return false;

        if (unit.SlotIndex == observerSlot.Value)
            return true;

        if (!enableTotalWar)
            return true;

        if (observerSlot == ActiveSlotId)
        {
            int cacheIndex = ResolveFogCacheIndex(unit);
            if (TryGetFogGameplaySnapshot(observerSlot.Value, out FogSlotGameplaySnapshot snapshot) &&
                snapshot.unitVisibility.TryGetValue(cacheIndex, out bool snapshotVisible))
            {
                return snapshotVisible;
            }
            if (fogCachedObserverSlotIndex == observerSlot.Value &&
                fogUnitVisibilityByCacheIndex.TryGetValue(cacheIndex, out bool cachedVisible))
            {
                return cachedVisible;
            }
        }

        return ComputeIsUnitVisibleForSlotWithoutCache(unit, observerSlot);
    }

    public bool IsUnitVisibleForSlotNoCache(UnitManager unit, PlayerSlotId observerSlot)
    {
        if (!debugFogOfWarEnabled)
            return true;

        if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
            return false;

        if (!IsValidPlayerSlot(observerSlot))
            return false;

        if (unit.SlotIndex == observerSlot.Value)
            return true;

        if (!enableTotalWar)
            return true;

        return ComputeIsUnitVisibleForSlotWithoutCache(unit, observerSlot);
    }

    private bool ComputeIsUnitVisibleForSlotWithoutCache(UnitManager unit, PlayerSlotId observerSlot)
    {
        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return false;

        if (IsUnitOnFriendlyConstruction(unit, observerSlot, boardMap))
            return true;

        bool enforceStealthValidation = enableStealthValidation && !unit.HasFiredThisTurn && !unit.HasPendingForcedLayerLock;
        return PodeDetectarSensor.IsTargetObservedByTeamWithoutForwardObserver(
            unit,
            observerSlot.Value,
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

        PlayerSlotId observerSlot = ActiveSlotId;
        if (unit.SlotIndex == observerSlot.Value)
            return true;

        if (!enableTotalWar)
            return true;

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return false;

        if (IsValidPlayerSlot(observerSlot)
            && IsUnitOnFriendlyConstruction(unit, observerSlot, boardMap))
        {
            return true;
        }

        bool enforceStealthValidation = enableStealthValidation && !unit.HasFiredThisTurn && !unit.HasPendingForcedLayerLock;
        return PodeDetectarSensor.IsTargetObservedByTeamWithoutForwardObserver(
            unit,
            observerSlot.Value,
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
        PlayerSlotId surrenderingSlot = ActiveSlotId;
        TeamId surrenderingTeam = GetVisualTeamForSlot(surrenderingSlot);
        if (surrenderingSlot.IsValid)
            MarkSlotDefeated(surrenderingSlot, "rendicao");

        if (DeclareEliminationVictory(ResolveFirstAliveOpponentSlot(surrenderingSlot), surrenderingSlot, VictoryReason.Surrender))
            return;

        // Sem oponente vivo para coroar (config atipica): encerra como derrota simples.
        hasVictoryWinner = true;
        victoryWinnerSlotIndex = -1;
        victoryWinnerTeam = TeamId.Neutral;
        HandleVictoryAestheticPresentation(TeamId.Neutral, surrenderingTeam, VictoryReason.Surrender);
    }

    private bool IsUnitOnFriendlyConstruction(UnitManager unit, PlayerSlotId observerSlot, Tilemap boardMap)
    {
        if (unit == null || boardMap == null || !IsValidPlayerSlot(observerSlot))
            return false;
        // A regra e sobre OCUPACAO da construcao (quem esta DE PE nela, na banda
        // bloqueante — o inimigo que ameaca capturar seu predio e sempre visto).
        // Aeronave sobrevoando ou submarino passando por baixo do hex da
        // construcao amiga NAO esta "na construcao" e nao pode ser auto-revelado
        // por esta regra — senao um Apache em Baixas Altitudes sobre o seu
        // aeroporto vira alvo mesmo com o hex coberto pelo FoW.
        if (OccupancyResolver.GetHeightBand(unit) != HeightBand.Blocking)
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
            if (construction.SlotIndex != observerSlot.Value)
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

    public bool IsCellVisibleForActiveTeam(Vector3Int cell)
    {
        return IsCellGeographicallyVisibleForActiveSlot(cell);
    }

    public bool IsCellGeographicallyVisibleForActiveSlot(Vector3Int cell)
    {
        if (!debugFogOfWarEnabled)
            return true;
        if (!enableTotalWar)
            return true;
        cell.z = 0;
        int observerSlotIndex = ActiveSlotId.Value;
        if (TryGetFogGameplaySnapshot(observerSlotIndex, out FogSlotGameplaySnapshot snapshot))
            return snapshot.geographicallyVisibleCells.Contains(cell);
        if (fogCachedObserverSlotIndex != observerSlotIndex)
            return false;

        return fogGeographicContributorsByCell.TryGetValue(cell, out int contributors) && contributors > 0;
    }

    public bool IsCellCoveredBySensorForActiveSlot(Vector3Int cell)
    {
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return true;

        cell.z = 0;
        int observerSlotIndex = ActiveSlotId.Value;
        if (TryGetFogGameplaySnapshot(observerSlotIndex, out FogSlotGameplaySnapshot snapshot))
            return snapshot.sensorCoveredCells.Contains(cell);
        if (fogCachedObserverSlotIndex != observerSlotIndex)
            return false;

        return fogSensorContributorsByCell.TryGetValue(cell, out int contributors) && contributors > 0;
    }

    // Cache por frame do conhecimento de celulas do slot observador (uniao All-modes:
    // visao geral + especializacoes + construcoes aliadas). Rebuild no maximo
    // uma vez por frame; as views especializadas por unidade ja sao cacheadas.
    private readonly HashSet<Vector3Int> observerKnownCellsCache = new HashSet<Vector3Int>();
    private int observerKnownCellsCacheFrame = -1;
    private int observerKnownCellsCacheSlotIndex = int.MinValue;
    private int observerKnownCellsCacheExcludedIndex = int.MinValue;

    // Conhecimento confirmado do SLOT sobre a celula, independente do modo de
    // visao selecionado no HUD: visao geografica geral (fogGeographicContributorsByCell) +
    // especializacoes de camada (ex.: EWACS revela Air a alcance 9) + celulas
    // reveladas por construcoes aliadas. E o predicado correto para gates de
    // GAMEPLAY (supressao de sensores, captura, corredor de tiro): visao e
    // conhecimento do time, nao da unidade selecionada.
    //
    // excludeProvisionalUnit: a unidade em MOVIMENTO PROVISORIO nao pode
    // contribuir a propria visao para a uniao — a especializacao dela radiaria
    // do destino cancelavel e marcaria o escuro como "conhecido", desligando a
    // supressao anti-oraculo que protege exatamente esse fluxo. O restante do
    // time (posicoes confirmadas) continua contando.
    public bool IsCellKnownForActiveTeam(Vector3Int cell, UnitManager excludeProvisionalUnit = null)
    {
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return true;

        cell.z = 0;
        if (IsCellVisibleForActiveTeam(cell))
            return true;

        if (excludeProvisionalUnit == null &&
            TryGetFogGameplaySnapshot(ActiveSlotId.Value, out FogSlotGameplaySnapshot snapshot))
        {
            return snapshot.knownCells.Contains(cell);
        }

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return false;

        int excludedIndex = excludeProvisionalUnit != null ? ResolveFogCacheIndex(excludeProvisionalUnit) : int.MinValue;
        if (observerKnownCellsCacheFrame != Time.frameCount ||
            observerKnownCellsCacheSlotIndex != ActiveSlotId.Value ||
            observerKnownCellsCacheExcludedIndex != excludedIndex)
        {
            BuildFogDisplayVisibleCellsForAllModes(boardMap, observerKnownCellsCache, excludeProvisionalUnit);
            observerKnownCellsCacheFrame = Time.frameCount;
            observerKnownCellsCacheSlotIndex = ActiveSlotId.Value;
            observerKnownCellsCacheExcludedIndex = excludedIndex;
        }

        return observerKnownCellsCache.Contains(cell);
    }

    public bool IsCellVisibleInFogPresentation(Vector3Int cell)
    {
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return true;

        int expectedSlotIndex = ActiveSlotId.Value;
        if (TryResolveFogPresentationSlot(out PlayerSlotId presentationSlot))
            expectedSlotIndex = presentationSlot.Value;
        if (fogCachedObserverSlotIndex != expectedSlotIndex)
            return false;

        cell.z = 0;
        return fogGeographicContributorsByCell.TryGetValue(cell, out int contributors) && contributors > 0;
    }

    public bool IsCellVisibleOrExploredInFogPresentation(Vector3Int cell)
    {
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return true;

        cell.z = 0;
        if (IsCellVisibleInFogPresentation(cell))
            return true;

        PlayerSlotId presentationSlot = ResolveFogVisualObserverSlot();
        return IsValidPlayerSlot(presentationSlot)
            && IsCellExploredBySlot(presentationSlot, cell);
    }


    public bool ShouldHideActiveAiActionPresentation()
    {
        return !debugFogOfWarPartial && TryResolveFogPresentationSlot(out _);
    }

    /// <summary>
    /// A partida esconde informacao de alguem? Somente o Total War cobre o tabuleiro.
    /// Nos presets sem FOW (Game Boy Classico, Fisica Basica, A Montanha Avacalha,
    /// Neblina Leve) todos os participantes enxergam o mesmo tabuleiro, entao nao ha
    /// sigilo a proteger — nenhum overlay deve ser suprimido "por privacidade",
    /// independente de quem controla o time ativo (AI normal, AI rebelde, humano
    /// local ou, no futuro, humano remoto).
    /// </summary>
    public bool ConcealsInformationFromObservers()
    {
        return Application.isPlaying && debugFogOfWarEnabled && enableTotalWar;
    }

    /// <summary>
    /// A camera deve enquadrar o QG do participante ativo na virada?
    ///
    /// Sem sigilo na partida (presets sem Total War) nao existe nada a proteger:
    /// todos veem o mesmo tabuleiro, entao o enquadramento vale sempre, inclusive
    /// quando o time ativo e uma AI.
    ///
    /// Com sigilo, enquadrar o QG revela onde ele fica. So o humano LOCAL pode
    /// receber esse enquadramento, porque so ele e o dono daquela informacao.
    /// Uma AI nao tem observador a servir nesta maquina, e um humano REMOTO
    /// recebe o enquadramento na maquina dele.
    ///
    /// IsPlayerLocal ja e exatamente "humano E local" (!isAI && isLocal), que e
    /// a condicao pedida — nao basta "nao e AI", senao um slot remoto passaria.
    /// </summary>
    public bool ShouldFocusCameraOnActiveHeadQuarter()
    {
        if (!Application.isPlaying)
            return false;
        if (!ConcealsInformationFromObservers())
            return true;

        return IsPlayerLocal(ActiveSlotId);
    }

    /// <summary>
    /// Indica que a acao do participante ativo pode ser apresentada ao observador
    /// desta maquina acima do FOW. A origem dos comandos (humano, AI ou futuramente
    /// jogador remoto/replay) e deliberadamente separada desta politica visual.
    /// </summary>
    public bool ShouldPresentActiveActionToLocalObserver()
    {
        if (!Application.isPlaying || !debugFogOfWarEnabled || !enableTotalWar)
            return false;
        if (gameSetup != GameSetupPreset.FogOfWarTotal || activeTeamId < 0
            || !Enum.IsDefined(typeof(TeamId), activeTeamId))
            return false;

        if (debugFogOfWarPartial &&
            TryResolveFogPresentationSlot(out PlayerSlotId aiPresentationSlot))
            return ActiveSlotId == aiPresentationSlot;
        if (!IsPlayerAI(ActiveSlotId))
            return true;

        // Sem jogador humano local, a partida AI vs AI possui um observador neutro
        // autorizado a acompanhar a acao do time ativo. Um futuro RemotePlayer pode
        // entrar aqui por sua propria politica, sem ser rotulado como AI.
        return !AnyHumanPlayerExists();
    }

    public bool ShouldPromoteActiveAiActionFxAboveFog()
    {
        return IsActiveTeamAI() && ShouldPresentActiveActionToLocalObserver();
    }

    private bool TryResolveFogPresentationSlot(out PlayerSlotId presentationSlot)
    {
        presentationSlot = PlayerSlotId.Invalid;
        if (!Application.isPlaying || !debugFogOfWarEnabled || !enableTotalWar ||
            gameSetup != GameSetupPreset.FogOfWarTotal || players == null)
            return false;

        bool requireAI = debugFogOfWarPartial;

        // No modo PARTIAL, a perspectiva acompanha a IA que realmente esta jogando.
        // Escolher apenas "a primeira IA" fazia outros slots AI receberem a visao,
        // memoria e apresentacao visual do participante errado.
        if (requireAI && IsValidPlayerSlot(ActiveSlotId) &&
            IsPlayerAI(ActiveSlotId) && !players[ActiveSlotId.Value].defeated)
        {
            presentationSlot = ActiveSlotId;
            return true;
        }

        if (!requireAI)
        {
            // Um unico humano local e o observador fixo desta maquina, inclusive
            // durante turnos de AI ou de um futuro jogador remoto.
            if (TryGetSingleActiveLocalHumanSlot(out PlayerSlotId pinnedLocal))
            {
                if (pinnedLocal == ActiveSlotId)
                    return false;
                presentationSlot = pinnedLocal;
                return true;
            }

            // Hot-seat: cada humano local observa apenas o proprio turno.
            // Turnos nao-locais sao cobertos pela PrivacyCurtain e nao recebem
            // um observador visual artificial.
            if (IsPlayerLocal(ActiveSlotId))
                return false;
            return false;
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].isAI != requireAI || players[i].defeated)
                continue;
            presentationSlot = PlayerSlotId.FromIndex(i);
            return true;
        }

        return false;
    }

    private PlayerSlotId ResolveFogVisualObserverSlot()
    {
        if (activeFogUpdateContext.HasValue)
            return activeFogUpdateContext.Value.presentationSlot;
        if (ShouldUseHotSeatPrivacyCurtain())
            // A cortina e a barreira de privacidade do Game View. O mundo por
            // tras dela continua renderizado pela perspectiva do participante
            // ativo para permitir inspecao do desenvolvedor na Scene View.
            return ActiveSlotId;
        return TryResolveFogPresentationSlot(out PlayerSlotId presentationSlot)
            ? presentationSlot
            : ActiveSlotId;
    }

    private bool IsFogConfirmedMemoryWriteAuthorized(int observerSlotIndex, string operation)
    {
        bool isNeutral = turnStateManager == null ||
                         turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral;
        bool contextAllowsMemory =
            !activeFogUpdateContext.HasValue ||
            (activeFogUpdateContext.Value.recordExplorationMemory &&
             activeFogUpdateContext.Value.observerSlot.Value == observerSlotIndex);
        bool authorized = Application.isPlaying &&
                          isNeutral &&
                          contextAllowsMemory &&
                          IsValidPlayerSlotIndex(observerSlotIndex) &&
                          fogCachedObserverSlotIndex == observerSlotIndex;
        if (!authorized)
        {
            LogFogWriteBarrierRejection(
                operation,
                observerSlotIndex,
                int.MinValue,
                isNeutral);
        }

        return authorized;
    }

    private bool IsFogVisualWriteAuthorized(string operation)
    {
        PlayerSlotId presentationSlot = ResolveFogVisualObserverSlot();
        bool isNeutral = turnStateManager == null ||
                         turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral;
        bool contextAllowsVisuals =
            !activeFogUpdateContext.HasValue ||
            activeFogUpdateContext.Value.publishVisuals;
        bool authorized = contextAllowsVisuals &&
                          isNeutral &&
                          IsValidPlayerSlot(presentationSlot) &&
                          fogCachedObserverSlotIndex == presentationSlot.Value;
        if (!authorized)
        {
            LogFogWriteBarrierRejection(
                operation,
                fogCachedObserverSlotIndex,
                presentationSlot.Value,
                isNeutral);
        }

        return authorized;
    }

    private void LogFogWriteBarrierRejection(
        string operation,
        int requestedSlotIndex,
        int presentationSlotIndex,
        bool isNeutral)
    {
        if (!enableFogValidationLogs)
            return;

        string warning =
            $"[FoW][WriteBarrier] rejected={operation} requestedSlot={requestedSlotIndex} " +
            $"cacheSlot={fogCachedObserverSlotIndex} " +
            $"presentationSlot={presentationSlotIndex} neutral={isNeutral}";
        if (string.Equals(lastFogWriteBarrierWarning, warning, StringComparison.Ordinal))
            return;

        lastFogWriteBarrierWarning = warning;
        Debug.LogWarning(warning);
    }

    public void ExportFogRuntimeCacheForSave(
        out int observerSlotIndex,
        List<FogCellContributorSaveData> visibleContributorsByCell,
        List<FogUnitVisibilitySaveData> unitVisibilityByCacheIndex)
    {
        observerSlotIndex = fogCachedObserverSlotIndex;

        if (visibleContributorsByCell != null)
        {
            visibleContributorsByCell.Clear();
            foreach (var kv in fogGeographicContributorsByCell)
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

    public void ExportFogSourceContributionsForSave(
        out int cacheFormat,
        out int cacheConfigHash,
        List<FogSourceContributionSaveData> destination)
    {
        cacheFormat = FogSourceCacheFormatVersion;
        cacheConfigHash = BuildFogSourceCacheConfigHash(ResolveFogBoardTilemap());
        if (destination == null)
            return;
        destination.Clear();

        int observerSlotIndex = fogCachedObserverSlotIndex;
        if (observerSlotIndex < 0)
            return;

        AppendFogSourceContributionsForSave(
            observerSlotIndex,
            fogContributionsBySource,
            destination);
    }

    public void ExportFogSourceCachesByObserverSlotForSave(
        List<FogObserverSourceCacheSaveData> destination)
    {
        if (destination == null)
            return;
        destination.Clear();

        int cacheConfigHash = BuildFogSourceCacheConfigHash(ResolveFogBoardTilemap());
        List<int> observerSlots = new List<int>(fogContributionRuntimeBySlot.Keys);
        if (fogCachedObserverSlotIndex >= 0 && !observerSlots.Contains(fogCachedObserverSlotIndex))
            observerSlots.Add(fogCachedObserverSlotIndex);
        observerSlots.Sort();

        for (int i = 0; i < observerSlots.Count; i++)
        {
            int observerSlotIndex = observerSlots[i];
            IReadOnlyDictionary<FogContributionSourceId, FogSourceContributionCacheEntry> sources = null;
            if (observerSlotIndex == fogCachedObserverSlotIndex)
            {
                sources = fogContributionsBySource;
            }
            else if (fogContributionRuntimeBySlot.TryGetValue(
                         observerSlotIndex,
                         out FogSlotContributionRuntime runtime))
            {
                sources = runtime.sources;
            }

            if (sources == null || sources.Count == 0)
                continue;

            FogObserverSourceCacheSaveData block = new FogObserverSourceCacheSaveData
            {
                observerSlotIndex = observerSlotIndex,
                cacheFormat = FogSourceCacheFormatVersion,
                cacheConfigHash = cacheConfigHash
            };
            AppendFogSourceContributionsForSave(
                observerSlotIndex,
                sources,
                block.contributions);
            if (block.contributions.Count > 0)
                destination.Add(block);
        }
    }

    private static void AppendFogSourceContributionsForSave(
        int observerSlotIndex,
        IReadOnlyDictionary<FogContributionSourceId, FogSourceContributionCacheEntry> sources,
        List<FogSourceContributionSaveData> destination)
    {
        if (sources == null || destination == null)
            return;

        foreach (KeyValuePair<FogContributionSourceId, FogSourceContributionCacheEntry> pair
                 in sources)
        {
            FogSourceContributionCacheEntry entry = pair.Value;
            if (entry == null ||
                (entry.geographicCells.Count == 0 && entry.sensorCells.Count == 0))
            {
                continue;
            }

            FogSourceContributionSaveData saved = new FogSourceContributionSaveData
            {
                observerSlotIndex = observerSlotIndex,
                sourceType = (int)pair.Key.type,
                sourceInstanceId = pair.Key.instanceId,
                sourceStateHash = entry.sourceStateHash
            };
            saved.geographicCells.AddRange(entry.geographicCells);
            saved.sensorCells.AddRange(entry.sensorCells);
            SortFogCellList(saved.geographicCells);
            SortFogCellList(saved.sensorCells);
            saved.contributionChecksum = ComputeFogSourceContributionChecksum(saved);
            destination.Add(saved);
        }
    }

    public bool VerifyFogSourceContributionsFromSave(
        IList<FogSourceContributionSaveData> savedSources)
    {
        if (savedSources == null || savedSources.Count == 0)
            return false;

        const int maxDetails = 8;
        int observerSlotIndex = fogCachedObserverSlotIndex;
        int invalid = 0;
        int duplicate = 0;
        int missingRuntime = 0;
        int missingSaved = 0;
        int stateMismatch = 0;
        int geographicMismatch = 0;
        int sensorMismatch = 0;
        int matched = 0;
        List<string> details = new List<string>(maxDetails);
        Dictionary<FogContributionSourceId, FogSourceContributionSaveData> savedBySource =
            new Dictionary<FogContributionSourceId, FogSourceContributionSaveData>();

        for (int i = 0; i < savedSources.Count; i++)
        {
            FogSourceContributionSaveData saved = savedSources[i];
            if (saved == null ||
                saved.observerSlotIndex != observerSlotIndex ||
                (saved.sourceType != (int)FogContributionSourceType.Unit &&
                 saved.sourceType != (int)FogContributionSourceType.Construction))
            {
                invalid++;
                RegisterFogCacheVerificationDetail(details, maxDetails, $"invalid@index={i}");
                continue;
            }

            FogContributionSourceId sourceId = new FogContributionSourceId(
                (FogContributionSourceType)saved.sourceType,
                saved.sourceInstanceId);
            if (!savedBySource.TryAdd(sourceId, saved))
            {
                duplicate++;
                RegisterFogCacheVerificationDetail(details, maxDetails, $"duplicate={FormatFogSourceId(sourceId)}");
            }
        }

        foreach (KeyValuePair<FogContributionSourceId, FogSourceContributionSaveData> pair in savedBySource)
        {
            if (!fogContributionsBySource.TryGetValue(pair.Key, out FogSourceContributionCacheEntry runtime) ||
                runtime == null ||
                (runtime.geographicCells.Count == 0 && runtime.sensorCells.Count == 0))
            {
                missingRuntime++;
                RegisterFogCacheVerificationDetail(details, maxDetails, $"missingRuntime={FormatFogSourceId(pair.Key)}");
                continue;
            }

            FogSourceContributionSaveData saved = pair.Value;
            bool sourceMatches = true;
            if (runtime.sourceStateHash != saved.sourceStateHash)
            {
                stateMismatch++;
                sourceMatches = false;
            }
            if (!FogSavedCellsMatch(runtime.geographicCells, saved.geographicCells))
            {
                geographicMismatch++;
                sourceMatches = false;
            }
            if (!FogSavedCellsMatch(runtime.sensorCells, saved.sensorCells))
            {
                sensorMismatch++;
                sourceMatches = false;
            }

            if (sourceMatches)
            {
                matched++;
            }
            else
            {
                RegisterFogCacheVerificationDetail(
                    details,
                    maxDetails,
                    $"mismatch={FormatFogSourceId(pair.Key)} " +
                    $"geo={saved.geographicCells?.Count ?? 0}/{runtime.geographicCells.Count} " +
                    $"sensor={saved.sensorCells?.Count ?? 0}/{runtime.sensorCells.Count}");
            }
        }

        int runtimeComparable = 0;
        foreach (KeyValuePair<FogContributionSourceId, FogSourceContributionCacheEntry> pair in fogContributionsBySource)
        {
            FogSourceContributionCacheEntry runtime = pair.Value;
            if (runtime == null ||
                (runtime.geographicCells.Count == 0 && runtime.sensorCells.Count == 0))
            {
                continue;
            }

            runtimeComparable++;
            if (savedBySource.ContainsKey(pair.Key))
                continue;
            missingSaved++;
            RegisterFogCacheVerificationDetail(details, maxDetails, $"missingSaved={FormatFogSourceId(pair.Key)}");
        }

        bool exact = invalid == 0 && duplicate == 0 && missingRuntime == 0 && missingSaved == 0 &&
                     stateMismatch == 0 && geographicMismatch == 0 && sensorMismatch == 0 &&
                     matched == savedBySource.Count && runtimeComparable == savedBySource.Count;
        string summary =
            $"[FoW][LoadCacheVerify] exact={exact} slot={observerSlotIndex} " +
            $"saved={savedSources.Count} runtime={runtimeComparable} matched={matched} " +
            $"invalid={invalid} duplicate={duplicate} missingRuntime={missingRuntime} missingSaved={missingSaved} " +
            $"stateMismatch={stateMismatch} geographicMismatch={geographicMismatch} sensorMismatch={sensorMismatch}";
        if (exact)
        {
            Debug.Log(summary);
        }
        else
        {
            if (details.Count > 0)
                summary += $" details=[{string.Join(" | ", details)}]";
            Debug.LogWarning(summary);
        }
        return exact;
    }

    private static bool FogSavedCellsMatch(
        HashSet<Vector3Int> runtimeCells,
        IList<Vector3Int> savedCells)
    {
        int savedCount = savedCells?.Count ?? 0;
        if (runtimeCells == null)
            return savedCount == 0;
        if (runtimeCells.Count != savedCount)
            return false;
        if (savedCount == 0)
            return true;

        HashSet<Vector3Int> savedSet = new HashSet<Vector3Int>();
        for (int i = 0; i < savedCount; i++)
            savedSet.Add(savedCells[i]);
        return savedSet.Count == savedCount && runtimeCells.SetEquals(savedSet);
    }

    private static void RegisterFogCacheVerificationDetail(
        List<string> details,
        int maxDetails,
        string detail)
    {
        if (details != null && details.Count < maxDetails)
            details.Add(detail);
    }

    private static string FormatFogSourceId(FogContributionSourceId sourceId) =>
        $"{sourceId.type}:{sourceId.instanceId}";

    public bool TryRestoreFogSourceContributionsFromSave(
        int observerSlotIndex,
        int cacheFormat,
        int cacheConfigHash,
        IList<FogSourceContributionSaveData> savedSources,
        out string result)
    {
        if (observerSlotIndex != ActiveSlotId.Value)
        {
            result = "observer_slot_mismatch";
            return false;
        }
        if (TryResolveFogPresentationSlot(out PlayerSlotId presentationSlot) &&
            presentationSlot != ActiveSlotId)
        {
            result = "split_gameplay_presentation";
            return false;
        }

        return TryRestoreFogSourceContributionsFromSaveInternal(
            observerSlotIndex,
            cacheFormat,
            cacheConfigHash,
            savedSources,
            publishVisuals: true,
            out result);
    }

    public bool TryRestoreFogSourceRuntimeForSlotFromSave(
        PlayerSlotId gameplaySlot,
        int observerSlotIndex,
        int cacheFormat,
        int cacheConfigHash,
        IList<FogSourceContributionSaveData> savedSources,
        out string result)
    {
        PlayerSlotId observerSlot = PlayerSlotId.FromIndex(observerSlotIndex);
        if (!IsValidPlayerSlot(gameplaySlot) || !IsValidPlayerSlot(observerSlot))
        {
            result = "invalid_slot";
            return false;
        }

        PlayerSlotId presentationSlot = ResolveFogVisualObserverSlot();
        if (!IsValidPlayerSlot(presentationSlot))
            presentationSlot = gameplaySlot;
        FogUpdateContext context = CreateFogUpdateContext(
            gameplaySlot,
            observerSlot,
            presentationSlot,
            FogOfWarRefreshMode.DataOnly);
        FogObserverScopeState previous = EnterFogObserverScope(context);
        try
        {
            return TryRestoreFogSourceContributionsFromSaveInternal(
                observerSlotIndex,
                cacheFormat,
                cacheConfigHash,
                savedSources,
                publishVisuals: false,
                out result);
        }
        finally
        {
            ExitFogObserverScope(previous);
        }
    }

    private bool TryRestoreFogSourceContributionsFromSaveInternal(
        int observerSlotIndex,
        int cacheFormat,
        int cacheConfigHash,
        IList<FogSourceContributionSaveData> savedSources,
        bool publishVisuals,
        out string result)
    {
        result = "unknown";
        if (!debugFogOfWarEnabled || !enableTotalWar || fogOfWarTilemap == null)
        {
            result = "fog_unavailable";
            return false;
        }
        if (turnStateManager != null &&
            turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        {
            result = "not_neutral";
            return false;
        }
        if (observerSlotIndex != ActiveSlotId.Value)
        {
            result = "observer_scope_mismatch";
            return false;
        }
        if (cacheFormat != FogSourceCacheFormatVersion ||
            savedSources == null || savedSources.Count == 0)
        {
            result = "cache_format_or_empty";
            return false;
        }

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null || cacheConfigHash == 0 ||
            cacheConfigHash != BuildFogSourceCacheConfigHash(boardMap))
        {
            result = "config_mismatch";
            return false;
        }

        Dictionary<FogContributionSourceId, UnitManager> eligibleUnits =
            new Dictionary<FogContributionSourceId, UnitManager>();
        List<UnitManager> activeUnits = UnitManager.AllActive;
        for (int i = 0; i < activeUnits.Count; i++)
        {
            UnitManager unit = activeUnits[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked ||
                unit.SlotIndex != observerSlotIndex || !IsUnitOnBoard(unit, boardMap))
            {
                continue;
            }
            FogContributionSourceId sourceId = ResolveFogContributionSourceId(unit);
            if (!eligibleUnits.TryAdd(sourceId, unit))
            {
                result = $"duplicate_runtime_source:{FormatFogSourceId(sourceId)}";
                return false;
            }
        }

        Dictionary<FogContributionSourceId, ConstructionManager> eligibleConstructions =
            new Dictionary<FogContributionSourceId, ConstructionManager>();
        List<ConstructionManager> activeConstructions = ConstructionManager.AllActive;
        for (int i = 0; i < activeConstructions.Count; i++)
        {
            ConstructionManager construction = activeConstructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            bool owned = IsConstructionOwnedByActivePlayer(construction);
            if (!owned && !construction.IsPlayerHeadQuarter)
                continue;
            if (construction.BoardTilemap != boardMap ||
                construction.gameObject.scene != boardMap.gameObject.scene)
            {
                continue;
            }
            FogContributionSourceId sourceId = ResolveFogContributionSourceId(construction);
            if (!eligibleConstructions.TryAdd(sourceId, construction))
            {
                result = $"duplicate_runtime_source:{FormatFogSourceId(sourceId)}";
                return false;
            }
        }

        Dictionary<FogContributionSourceId, FogSourceContributionSaveData> validated =
            new Dictionary<FogContributionSourceId, FogSourceContributionSaveData>();
        for (int i = 0; i < savedSources.Count; i++)
        {
            FogSourceContributionSaveData saved = savedSources[i];
            if (saved == null || saved.observerSlotIndex != observerSlotIndex ||
                (saved.sourceType != (int)FogContributionSourceType.Unit &&
                 saved.sourceType != (int)FogContributionSourceType.Construction))
            {
                result = $"invalid_source:{i}";
                return false;
            }
            if (saved.contributionChecksum == 0 ||
                saved.contributionChecksum != ComputeFogSourceContributionChecksum(saved))
            {
                result = $"checksum_mismatch:{i}";
                return false;
            }
            string sourceLabel = $"{(FogContributionSourceType)saved.sourceType}:{saved.sourceInstanceId}";
            if (!ValidateSavedFogCells(
                    saved.geographicCells,
                    boardMap,
                    "geographic",
                    out string invalidCellDetail) ||
                !ValidateSavedFogCells(
                    saved.sensorCells,
                    boardMap,
                    "sensor",
                    out invalidCellDetail))
            {
                result = $"invalid_cells sourceIndex={i} source={sourceLabel} {invalidCellDetail}";
                return false;
            }

            FogContributionSourceId sourceId = new FogContributionSourceId(
                (FogContributionSourceType)saved.sourceType,
                saved.sourceInstanceId);
            if (!validated.TryAdd(sourceId, saved))
            {
                result = $"duplicate_saved_source:{FormatFogSourceId(sourceId)}";
                return false;
            }

            if (sourceId.type == FogContributionSourceType.Unit)
            {
                if (!eligibleUnits.TryGetValue(sourceId, out UnitManager unit) ||
                    saved.sourceStateHash != BuildFogUnitSourceStateHash(unit) ||
                    !FogSavedCellListsMatch(saved.geographicCells, saved.sensorCells))
                {
                    result = $"unit_validation:{FormatFogSourceId(sourceId)}";
                    return false;
                }
            }
            else
            {
                if (!eligibleConstructions.TryGetValue(sourceId, out ConstructionManager construction) ||
                    saved.sourceStateHash != BuildFogConstructionSourceStateHash(construction) ||
                    !ValidateSavedConstructionContribution(saved, construction, boardMap))
                {
                    result = $"construction_validation:{FormatFogSourceId(sourceId)}";
                    return false;
                }
            }
        }

        if (validated.Count != eligibleUnits.Count + eligibleConstructions.Count)
        {
            result = $"source_count:{validated.Count}/{eligibleUnits.Count + eligibleConstructions.Count}";
            return false;
        }

        Dictionary<Vector3Int, int> expectedGeographicContributors =
            BuildExpectedFogContributorCounts(validated.Values, useGeographicChannel: true);
        Dictionary<Vector3Int, int> expectedSensorContributors =
            BuildExpectedFogContributorCounts(validated.Values, useGeographicChannel: false);

        // Todas as validacoes terminaram antes da primeira mutacao.
        ValidateFogOfWarSortingLayer();
        ResetFogOfWarRuntime(clearTilemap: false);
        if (publishVisuals)
            InitializeFogOverlay(boardMap);
        else
            InitializeFogRuntimeData(boardMap);
        if (!fogOverlayInitialized)
        {
            result = publishVisuals ? "overlay_init_failed" : "runtime_init_failed";
            return false;
        }

        foreach (KeyValuePair<FogContributionSourceId, FogSourceContributionSaveData> pair in validated)
        {
            FogSourceContributionSaveData saved = pair.Value;
            FogSourceContributionCacheEntry runtime = new FogSourceContributionCacheEntry
            {
                sourceStateHash = saved.sourceStateHash
            };
            if (pair.Key.type == FogContributionSourceType.Unit &&
                eligibleUnits.TryGetValue(pair.Key, out UnitManager unit))
            {
                runtime.unitCacheKey = BuildFogUnitCacheKey(unit, boardMap);
            }

            fogContributionsBySource[pair.Key] = runtime;
            for (int i = 0; i < saved.geographicCells.Count; i++)
                AddFogSourceGeographicContribution(runtime, saved.geographicCells[i], boardMap, updateVisual: false);
            for (int i = 0; i < saved.sensorCells.Count; i++)
                AddFogSourceSensorContribution(runtime, saved.sensorCells[i], boardMap);
        }

        if (!RestoredFogSourcesMatch(validated) ||
            !FogContributorCountsMatch(fogGeographicContributorsByCell, expectedGeographicContributors) ||
            !FogContributorCountsMatch(fogSensorContributorsByCell, expectedSensorContributors))
        {
            // Ainda nao houve publicacao de snapshot, contatos, overlay ou eventos.
            // Descarte integralmente a tentativa para o caller executar o cold refresh.
            ResetFogOfWarRuntime(clearTilemap: false);
            result = "rebuild_invariant_mismatch";
            return false;
        }

        StoreFogContributionRuntimeForSlot(PlayerSlotId.FromIndex(observerSlotIndex));
        if (!publishVisuals)
        {
            result =
                $"cached sources={validated.Count} units={eligibleUnits.Count} " +
                $"constructions={eligibleConstructions.Count} geographic={fogGeographicContributorsByCell.Count} " +
                $"sensor={fogSensorContributorsByCell.Count}";
            return true;
        }

        UnitManager[] snapshotUnits = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude);
        PublishFogGameplaySnapshot(
            observerSlotIndex,
            boardMap,
            snapshotUnits,
            recordExplorationMemory: true);
        fogUnitVisibilityByCacheIndex.Clear();
        if (TryGetFogGameplaySnapshot(observerSlotIndex, out FogSlotGameplaySnapshot snapshot))
        {
            foreach (KeyValuePair<int, bool> pair in snapshot.unitVisibility)
                fogUnitVisibilityByCacheIndex[pair.Key] = pair.Value;
        }
        ApplyRuntimeUnitFogVisibilityFromCache(boardMap);
        if (Application.isPlaying)
            AIIntelLedger.RecordVisibleContactsForSlot(ActiveSlotId, currentTurn, this);
        RenderFogOverlayFromRuntimeCache(boardMap);
        if (Application.isPlaying)
            OnFogOfWarUpdated?.Invoke();

        result =
            $"restored sources={validated.Count} units={eligibleUnits.Count} " +
            $"constructions={eligibleConstructions.Count} geographic={fogGeographicContributorsByCell.Count} " +
            $"sensor={fogSensorContributorsByCell.Count}";
        return true;
    }

    private static Dictionary<Vector3Int, int> BuildExpectedFogContributorCounts(
        IEnumerable<FogSourceContributionSaveData> savedSources,
        bool useGeographicChannel)
    {
        Dictionary<Vector3Int, int> expected = new Dictionary<Vector3Int, int>();
        foreach (FogSourceContributionSaveData saved in savedSources)
        {
            IList<Vector3Int> cells = useGeographicChannel
                ? saved.geographicCells
                : saved.sensorCells;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int cell = cells[i];
                expected.TryGetValue(cell, out int contributors);
                expected[cell] = contributors + 1;
            }
        }
        return expected;
    }

    private bool RestoredFogSourcesMatch(
        Dictionary<FogContributionSourceId, FogSourceContributionSaveData> validated)
    {
        if (fogContributionsBySource.Count != validated.Count)
            return false;

        foreach (KeyValuePair<FogContributionSourceId, FogSourceContributionSaveData> pair in validated)
        {
            if (!fogContributionsBySource.TryGetValue(
                    pair.Key,
                    out FogSourceContributionCacheEntry runtime) ||
                runtime == null ||
                runtime.sourceStateHash != pair.Value.sourceStateHash ||
                !FogSavedCellsMatch(runtime.geographicCells, pair.Value.geographicCells) ||
                !FogSavedCellsMatch(runtime.sensorCells, pair.Value.sensorCells))
            {
                return false;
            }
        }
        return true;
    }

    private static bool FogContributorCountsMatch(
        Dictionary<Vector3Int, int> runtime,
        Dictionary<Vector3Int, int> expected)
    {
        if (runtime == null || expected == null || runtime.Count != expected.Count)
            return false;

        foreach (KeyValuePair<Vector3Int, int> pair in expected)
        {
            if (!runtime.TryGetValue(pair.Key, out int contributors) ||
                contributors != pair.Value)
            {
                return false;
            }
        }
        return true;
    }

    private static bool ValidateSavedFogCells(
        IList<Vector3Int> cells,
        Tilemap boardMap,
        string channel,
        out string invalidDetail)
    {
        invalidDetail = string.Empty;
        if (cells == null)
        {
            invalidDetail = $"channel={channel} cause=null_list";
            return false;
        }
        if (boardMap == null)
        {
            invalidDetail = $"channel={channel} cause=null_board";
            return false;
        }

        HashSet<Vector3Int> unique = new HashSet<Vector3Int>();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int cell = cells[i];
            string cause = null;
            if (cell.z != 0)
                cause = "nonzero_z";
            else if (!IsFogBoardCell(cell, boardMap))
                cause = "board_tile_missing";
            else if (!unique.Add(cell))
                cause = "duplicate";

            if (cause != null)
            {
                invalidDetail =
                    $"channel={channel} cellIndex={i} cell=({cell.x},{cell.y},{cell.z}) " +
                    $"cause={cause} board={boardMap.name} insideBounds={boardMap.cellBounds.Contains(cell)} " +
                    $"layers={DescribeFogCellTilemapOccupancy(cell, boardMap)}";
                return false;
            }
        }
        return true;
    }

    private static string DescribeFogCellTilemapOccupancy(Vector3Int cell, Tilemap boardMap)
    {
        if (boardMap == null)
            return "[]";

        const int maxLayers = 8;
        List<string> occupiedLayers = new List<string>(maxLayers);
        Tilemap[] tilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null ||
                tilemap.gameObject.scene != boardMap.gameObject.scene ||
                tilemap.layoutGrid != boardMap.layoutGrid)
            {
                continue;
            }

            TileBase tile = tilemap.GetTile(cell);
            if (tile == null)
                continue;

            if (occupiedLayers.Count < maxLayers)
                occupiedLayers.Add($"{tilemap.name}:{tile.name}");
        }

        return occupiedLayers.Count == 0
            ? "[]"
            : $"[{string.Join(",", occupiedLayers)}]";
    }

    private static bool IsFogBoardCell(Vector3Int cell, Tilemap boardMap) =>
        boardMap != null && cell.z == 0 && boardMap.GetTile(cell) != null;

    private static bool FogSavedCellListsMatch(
        IList<Vector3Int> left,
        IList<Vector3Int> right)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;
        return new HashSet<Vector3Int>(left).SetEquals(right);
    }

    private bool ValidateSavedConstructionContribution(
        FogSourceContributionSaveData saved,
        ConstructionManager construction,
        Tilemap boardMap)
    {
        Vector3Int origin = construction.CurrentCellPosition;
        origin.z = 0;
        bool owned = IsConstructionOwnedByActivePlayer(construction);
        if (!owned)
        {
            return construction.IsPlayerHeadQuarter &&
                   saved.geographicCells.Count == 1 &&
                   saved.geographicCells[0] == origin &&
                   saved.sensorCells.Count == 0;
        }

        int visionRange = 0;
        if (construction.TryResolveConstructionData(out ConstructionData data) && data != null)
            visionRange = Mathf.Max(0, data.visao);
        HashSet<Vector3Int> expectedGeographic = BuildCellsInRadius(boardMap, origin, visionRange);
        return expectedGeographic.SetEquals(saved.geographicCells) &&
               saved.sensorCells.Count == 1 &&
               saved.sensorCells[0] == origin;
    }

    public bool TryRestoreFogRuntimeCacheForObserverSlotFromSave(
        int observerSlotIndex,
        List<FogCellContributorSaveData> visibleContributorsByCell,
        List<FogUnitVisibilitySaveData> unitVisibilityByCacheIndex)
    {
        if (!debugFogOfWarEnabled || !enableTotalWar)
            return false;
        if (observerSlotIndex != ActiveSlotId.Value)
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

        fogCachedObserverSlotIndex = observerSlotIndex;

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
                fogGeographicContributorsByCell[cell] = contributors;
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

        UnitManager[] snapshotUnits = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude);
        PublishFogGameplaySnapshot(
            ActiveSlotId.Value,
            boardMap,
            snapshotUnits,
            recordExplorationMemory: true);
        ApplyRuntimeUnitFogVisibilityFromCache(boardMap);
        if (Application.isPlaying)
            OnFogOfWarUpdated?.Invoke();
        return true;
    }

    [Obsolete("Use TryRestoreFogRuntimeCacheForObserverSlotFromSave; cachedTeamId sempre representou um índice de slot.")]
    public bool TryRestoreFogRuntimeCacheFromSave(
        int cachedTeamId,
        List<FogCellContributorSaveData> visibleContributorsByCell,
        List<FogUnitVisibilitySaveData> unitVisibilityByCacheIndex)
    {
        return TryRestoreFogRuntimeCacheForObserverSlotFromSave(
            cachedTeamId,
            visibleContributorsByCell,
            unitVisibilityByCacheIndex);
    }

    private void ApplyRuntimeUnitFogVisibilityFromCache(Tilemap boardMap)
    {
        bool fogOverlayOwnsWorldOcclusion = UsesFogOverlayForWorldOcclusion();
        PlayerSlotId visualObserverSlot = ResolveFogVisualObserverSlot();
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if (boardMap != null && !IsUnitOnBoard(unit, boardMap))
                continue;

            int cacheIndex = ResolveFogCacheIndex(unit);
            bool visible = unit.SlotIndex == ActiveSlotId.Value;
            if (!visible)
            {
                if (!fogUnitVisibilityByCacheIndex.TryGetValue(cacheIndex, out visible))
                    visible = false;
            }

            ApplyFogDetectedContactPresentation(
                unit,
                visualObserverSlot,
                fogOverlayOwnsWorldOcclusion);
            unit.SetFogOfWarVisibility(ResolveFogRenderVisibility(
                unit, visible, fogOverlayOwnsWorldOcclusion, ActiveSlotId));
        }

        RefreshStackedHexFrontRendering(units, boardMap, ActiveSlotId);
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
            if (unit.SlotIndex == ActiveSlotId.Value)
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
        bool coverWorldPresentation = UsesFogOverlayForWorldOcclusion();
        bool playerTurn = activeTeamId >= 0
            && Enum.IsDefined(typeof(TeamId), activeTeamId)
            && !IsPlayerAI(ActiveSlotId);
        bool showCursorAboveFog = ShouldPresentActiveActionToLocalObserver();
        bool showHumanTurnTools = playerTurn
            || (debugFogOfWarPartial && showCursorAboveFog);

        // FogOfWar/FogOfWarTile pertencem exclusivamente ao preset Total.
        // Nos demais presets restaura cursor e ferramentas ao pipeline legado SFX,
        // mesmo que a cena ainda possua referencias/objetos de FOW serializados.
        cursorController?.ApplyFogOfWarSorting(coverWorldPresentation && showCursorAboveFog);
        turnStateManager?.ApplyMovementRangeFogOfWarSorting(coverWorldPresentation && showHumanTurnTools);

        if (fogOfWarTilemap == null)
            return;

        TilemapRenderer renderer = fogOfWarTilemap.GetComponent<TilemapRenderer>();
        if (renderer == null)
            return;

        string expectedLayer = coverWorldPresentation ? "FogOfWar" : "SFX";
        string currentLayer = SortingLayer.IDToName(renderer.sortingLayerID);
        if (!string.Equals(currentLayer, expectedLayer, StringComparison.OrdinalIgnoreCase))
        {
            renderer.sortingLayerName = expectedLayer;
            renderer.sortingOrder = 0;
            currentLayer = SortingLayer.IDToName(renderer.sortingLayerID);
        }

        EnsureFogOfWarMemoryTilemap();
        TilemapRenderer memoryRenderer = fogOfWarMemoryTilemap != null
            ? fogOfWarMemoryTilemap.GetComponent<TilemapRenderer>()
            : null;
        if (memoryRenderer != null)
        {
            memoryRenderer.sortingLayerName = coverWorldPresentation ? "FogOfWarTile" : "SFX";
            memoryRenderer.sortingOrder = 0;
        }
        TilemapRenderer breakwaterMemoryRenderer = fogOfWarBreakwaterMemoryTilemap != null
            ? fogOfWarBreakwaterMemoryTilemap.GetComponent<TilemapRenderer>()
            : null;
        if (breakwaterMemoryRenderer != null)
        {
            breakwaterMemoryRenderer.sortingLayerName = coverWorldPresentation ? "FogOfWarTile" : "SFX";
            breakwaterMemoryRenderer.sortingOrder = 1;
        }

        // PARTIAL e o humano sentado na cadeira da AI: alem do cursor, enxerga as
        // mesmas ferramentas visuais do seu turno (range map, linhas e overlays).
        // Fora dele, o observador neutro de AI vs AI continua sem planejamento interno.

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
        if (!Application.isPlaying || !debugFogOfWarEnabled)
            return false;
        if (!enableTotalWar || gameSetup != GameSetupPreset.FogOfWarTotal)
            return false;
        return fogOfWarTilemap != null;
    }

    // No FoW Total, o overlay opaco e a mascara espacial do mundo. Unidades comuns
    // permanecem renderizadas abaixo dele para surgirem e sumirem naturalmente ao
    // atravessar os recortes visiveis, inclusive durante a animacao de movimento.
    // Apenas unidades com stealth ativo precisam do hide individual, pois podem
    // continuar ocultas mesmo quando o hex estiver visualmente aberto.
    //
    // Nos setups sem overlay opaco (ex.: Neblina Leve), a visibilidade logica segue
    // controlando diretamente os renderers, ja que nao existe uma tampa para
    // realizar essa oclusao.
    private bool ResolveFogRenderVisibility(
        UnitManager unit,
        bool logicallyVisible,
        bool fogOverlayOwnsWorldOcclusion,
        PlayerSlotId observerSlot)
    {
        if (fogOverlayOwnsWorldOcclusion && !HasActiveIndividualFogConcealment(unit))
        {
            if (unit != null && unit.SlotIndex != observerSlot.Value &&
                TryGetFogGameplaySnapshot(observerSlot.Value, out FogSlotGameplaySnapshot snapshot))
            {
                Vector3Int cell = unit.CurrentCellPosition;
                cell.z = 0;

                // Com tile de FoW, o renderer pode continuar ligado por baixo da
                // tampa para atravessar seus recortes sem aparecer magicamente no
                // fim do movimento. Sem tile (hex aberto), a tampa ja nao protege:
                // obedece a deteccao logica individual. Isso separa terreno revelado
                // por alcance especializado (ex.: EWACS 9) de ocupante realmente
                // observado pela visao aplicavel ao alvo (ex.: Surface apenas 3).
                if (snapshot.geographicallyVisibleCells.Contains(cell))
                    return logicallyVisible;
            }

            return true;
        }

        return logicallyVisible;
    }

    private void ApplyFogDetectedContactPresentation(
        UnitManager unit,
        PlayerSlotId observerSlot,
        bool fogOverlayOwnsWorldOcclusion)
    {
        bool showContact = false;
        if (unit != null
            && fogOverlayOwnsWorldOcclusion
            && unit.SlotIndex != observerSlot.Value
            && TryGetFogGameplaySnapshot(
                observerSlot.Value,
                out FogSlotGameplaySnapshot snapshot))
        {
            int cacheIndex = ResolveFogCacheIndex(unit);
            bool logicallyVisible =
                snapshot.unitVisibility.TryGetValue(
                    cacheIndex,
                    out bool visible)
                && visible;
            if (logicallyVisible)
            {
                Vector3Int cell = unit.CurrentCellPosition;
                cell.z = 0;
                showContact =
                    !snapshot.geographicallyVisibleCells.Contains(cell);
            }
        }

        unit?.SetFogDetectedContactPresentation(showContact);
    }

    private bool HasActiveIndividualFogConcealment(UnitManager unit)
    {
        if (unit == null || !enableStealthValidation)
            return false;
        if (unit.HasFiredThisTurn || unit.HasPendingForcedLayerLock)
            return false;
        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null)
            return false;

        return unitData.IsStealthUnit(unit.GetDomain(), unit.GetHeightLevel());
    }

    public void RefreshMovingUnitFogPresentation(UnitManager unit)
    {
        if (unit == null || !debugFogOfWarEnabled || !enableTotalWar)
            return;

        PlayerSlotId observerSlot = ActiveSlotId;
        if (TryResolveFogPresentationSlot(out PlayerSlotId presentationSlot))
            observerSlot = presentationSlot;
        TeamId observerTeam = GetVisualTeamForSlot(observerSlot);

        bool logicallyVisible = unit.SlotIndex == observerSlot.Value;
        if (!logicallyVisible &&
            TryGetFogGameplaySnapshot(observerSlot.Value, out FogSlotGameplaySnapshot snapshot))
        {
            int cacheIndex = ResolveFogCacheIndex(unit);
            logicallyVisible = snapshot.unitVisibility.TryGetValue(cacheIndex, out bool visible) && visible;

            Vector3Int currentCell = unit.CurrentCellPosition;
            currentCell.z = 0;
            if (!logicallyVisible
                && snapshot.geographicallyVisibleCells.Contains(currentCell)
                && ComputeIsUnitVisibleForSlotWithoutCache(unit, observerSlot))
            {
                // A consulta usa a posicao apenas para a apresentacao corrente e nao
                // publica cache/intel. Uma vez detectada ao sair do tampao, a unidade
                // e seu HUD permanecem continuos ate commit ou rollback.
                unit.BeginTemporaryFogDetectionPresentation();
            }
        }

        if (unit.IsTemporaryFogDetectionPresentationActive)
        {
            ApplyFogDetectedContactPresentation(
                unit,
                observerSlot,
                UsesFogOverlayForWorldOcclusion());
            unit.SetFogOfWarVisibility(true);
            return;
        }

        ApplyFogDetectedContactPresentation(
            unit,
            observerSlot,
            UsesFogOverlayForWorldOcclusion());
        unit.SetFogOfWarVisibility(ResolveFogRenderVisibility(
            unit,
            logicallyVisible,
            UsesFogOverlayForWorldOcclusion(),
            observerSlot));
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

    private void PublishFogGameplaySnapshot(
        int slotIndex,
        Tilemap boardMap,
        UnitManager[] units,
        bool recordExplorationMemory,
        HashSet<Vector3Int> affectedTargetCells = null)
    {
        PlayerSlotId observerSlot = PlayerSlotId.FromIndex(slotIndex);
        if (!IsValidPlayerSlot(observerSlot) || boardMap == null)
            return;

        bool hadSnapshot = fogGameplaySnapshotsBySlot.TryGetValue(
            slotIndex,
            out FogSlotGameplaySnapshot snapshot);
        if (!hadSnapshot)
        {
            snapshot = new FogSlotGameplaySnapshot();
            fogGameplaySnapshotsBySlot[slotIndex] = snapshot;
        }

        // publish custa 10,6ms no primeiro refresh e 488ms na troca de turno, no
        // mesmo tabuleiro. Um destes seis blocos e condicional a algo que muda
        // entre os dois momentos; medir todos em vez de escolher um.
        double pubStartMs = enableFogStepPerfLogs ? Time.realtimeSinceStartupAsDouble : 0d;

        snapshot.geographicallyVisibleCells.Clear();
        foreach (var entry in fogGeographicContributorsByCell)
        {
            if (entry.Value > 0)
                snapshot.geographicallyVisibleCells.Add(entry.Key);
        }

        snapshot.sensorCoveredCells.Clear();
        foreach (var entry in fogSensorContributorsByCell)
        {
            if (entry.Value > 0)
                snapshot.sensorCoveredCells.Add(entry.Key);
        }
        double pubContributorsMs = 0d;
        double pubKnownCellsMs = 0d;
        double pubMemoryMs = 0d;
        double pubGeoOnlyMs = 0d;
        double pubUnitLoopMs = 0d;
        if (enableFogStepPerfLogs)
        {
            pubContributorsMs = (Time.realtimeSinceStartupAsDouble - pubStartMs) * 1000d;
            pubStartMs = Time.realtimeSinceStartupAsDouble;
        }

        snapshot.knownCells.Clear();
        BuildFogDisplayVisibleCellsForAllModes(boardMap, snapshot.knownCells);
        if (enableFogStepPerfLogs)
        {
            pubKnownCellsMs = (Time.realtimeSinceStartupAsDouble - pubStartMs) * 1000d;
            pubStartMs = Time.realtimeSinceStartupAsDouble;
        }

        if (recordExplorationMemory)
        {
            RecordConfirmedExploredCells(slotIndex, snapshot.knownCells);
            RecordConfirmedConstructionMemory(slotIndex, boardMap, snapshot.knownCells);
        }
        if (enableFogStepPerfLogs)
        {
            pubMemoryMs = (Time.realtimeSinceStartupAsDouble - pubStartMs) * 1000d;
            pubStartMs = Time.realtimeSinceStartupAsDouble;
        }

        snapshot.geographicOnlyCells.Clear();
        foreach (Vector3Int cell in snapshot.geographicallyVisibleCells)
        {
            if (!snapshot.sensorCoveredCells.Contains(cell))
                snapshot.geographicOnlyCells.Add(cell);
        }
        if (enableFogStepPerfLogs)
        {
            pubGeoOnlyMs = (Time.realtimeSinceStartupAsDouble - pubStartMs) * 1000d;
            pubStartMs = Time.realtimeSinceStartupAsDouble;
        }

        bool targetOnly = hadSnapshot &&
                          snapshot.unitVisibility.Count > 0 &&
                          affectedTargetCells != null &&
                          affectedTargetCells.Count > 0;
        if (!targetOnly)
            snapshot.unitVisibility.Clear();
        if (units == null)
            return;

        int evaluatedTargets = 0;
        int visibilityProbes = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if (!IsUnitOnBoard(unit, boardMap))
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            if (targetOnly && !affectedTargetCells.Contains(unitCell))
                continue;

            // O short-circuit por dono e o que separa barato de caro: unidade do
            // proprio observador nao paga sondagem de visibilidade.
            bool visible;
            if (unit.SlotIndex == slotIndex)
            {
                visible = true;
            }
            else
            {
                visibilityProbes++;
                visible = ComputeIsUnitVisibleForSlotWithoutCache(unit, observerSlot);
            }
            snapshot.unitVisibility[ResolveFogCacheIndex(unit)] = visible;
            evaluatedTargets++;
        }

        if (enableFogStepPerfLogs)
        {
            pubUnitLoopMs = (Time.realtimeSinceStartupAsDouble - pubStartMs) * 1000d;
            Debug.Log(
                $"[FoW][Perf][Publish] slot={slotIndex} contributors={pubContributorsMs:F3}ms " +
                $"knownCells={pubKnownCellsMs:F3}ms memory={pubMemoryMs:F3}ms " +
                $"geoOnly={pubGeoOnlyMs:F3}ms unitLoop={pubUnitLoopMs:F3}ms | " +
                $"recordMemory={recordExplorationMemory} targetOnly={targetOnly} " +
                $"evaluated={evaluatedTargets} visibilityProbes={visibilityProbes} " +
                $"knownCells.count={snapshot.knownCells.Count}");
        }

        if (enableFogStepPerfLogs && targetOnly)
        {
            Debug.Log(
                $"[FoW][AffectedTargets] slot={slotIndex} " +
                $"cells={affectedTargetCells.Count} evaluated={evaluatedTargets} totalUnits={units.Length}");
        }
    }

    private bool TryGetFogGameplaySnapshot(int observerSlotIndex, out FogSlotGameplaySnapshot snapshot)
    {
        if (observerSlotIndex < 0)
        {
            snapshot = null;
            return false;
        }

        return fogGameplaySnapshotsBySlot.TryGetValue(observerSlotIndex, out snapshot) && snapshot != null;
    }

    private void RecordConfirmedExploredCells(int observerSlotIndex, IEnumerable<Vector3Int> visibleCells)
    {
        if (!Application.isPlaying || visibleCells == null)
            return;
        if (!IsFogConfirmedMemoryWriteAuthorized(observerSlotIndex, "record_explored"))
            return;

        if (!fogExploredCellsBySlot.TryGetValue(observerSlotIndex, out HashSet<Vector3Int> explored))
        {
            explored = new HashSet<Vector3Int>();
            fogExploredCellsBySlot[observerSlotIndex] = explored;
        }

        foreach (Vector3Int sourceCell in visibleCells)
        {
            Vector3Int cell = sourceCell;
            cell.z = 0;
            explored.Add(cell);
        }
    }

    public bool IsCellExploredBySlot(PlayerSlotId slot, Vector3Int cell)
    {
        cell.z = 0;
        return IsValidPlayerSlot(slot) &&
               fogExploredCellsBySlot.TryGetValue(slot.Value, out HashSet<Vector3Int> explored) &&
               explored.Contains(cell);
    }

    private void RecordConfirmedConstructionMemory(
        int observerSlotIndex,
        Tilemap boardMap,
        HashSet<Vector3Int> visibleCells)
    {
        if (!Application.isPlaying || boardMap == null || visibleCells == null)
            return;
        if (!IsFogConfirmedMemoryWriteAuthorized(observerSlotIndex, "record_construction_memory"))
            return;

        if (!fogConstructionMemoryBySlot.TryGetValue(observerSlotIndex, out Dictionary<Vector3Int, FogConstructionMemoryEntry> memory))
        {
            memory = new Dictionary<Vector3Int, FogConstructionMemoryEntry>();
            fogConstructionMemoryBySlot[observerSlotIndex] = memory;
        }

        List<ConstructionManager> constructions = ConstructionManager.AllActive;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy ||
                construction.BoardTilemap != boardMap ||
                construction.gameObject.scene != boardMap.gameObject.scene)
            {
                continue;
            }

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (!visibleCells.Contains(cell) ||
                !construction.TryResolveConstructionData(out ConstructionData data) || data == null)
            {
                continue;
            }

            SpriteRenderer sourceRenderer = construction.GetMainSpriteRenderer();
            memory[cell] = new FogConstructionMemoryEntry
            {
                data = data,
                knownOwner = construction.TeamId,
                flipX = sourceRenderer != null && sourceRenderer.flipX
            };
        }
    }

    public bool TryGetKnownConstructionAtCell(
        PlayerSlotId observerSlot,
        Vector3Int cell,
        out ConstructionData constructionData,
        out TeamId knownOwner)
    {
        constructionData = null;
        knownOwner = TeamId.Neutral;
        cell.z = 0;
        if (!IsValidPlayerSlot(observerSlot) ||
            !fogConstructionMemoryBySlot.TryGetValue(observerSlot.Value, out Dictionary<Vector3Int, FogConstructionMemoryEntry> memory) ||
            !memory.TryGetValue(cell, out FogConstructionMemoryEntry entry) || entry == null || entry.data == null)
        {
            return false;
        }

        constructionData = entry.data;
        knownOwner = entry.knownOwner;
        return true;
    }

    public void ExportFogConstructionMemory(List<FogConstructionMemorySaveData> destination)
    {
        if (destination == null)
            return;
        destination.Clear();

        foreach (KeyValuePair<int, Dictionary<Vector3Int, FogConstructionMemoryEntry>> slotPair in fogConstructionMemoryBySlot)
        {
            foreach (KeyValuePair<Vector3Int, FogConstructionMemoryEntry> cellPair in slotPair.Value)
            {
                FogConstructionMemoryEntry entry = cellPair.Value;
                if (entry == null || entry.data == null)
                    continue;
                destination.Add(new FogConstructionMemorySaveData
                {
                    observerSlotIndex = slotPair.Key,
                    observerTeamId = (int)GetVisualTeamForSlot(PlayerSlotId.FromIndex(slotPair.Key)),
                    x = cellPair.Key.x,
                    y = cellPair.Key.y,
                    constructionDataId = !string.IsNullOrWhiteSpace(entry.data.id) ? entry.data.id : entry.data.name,
                    knownOwnerTeamId = (int)entry.knownOwner,
                    flipX = entry.flipX
                });
            }
        }

        destination.Sort((a, b) =>
        {
            int teamCompare = a.observerTeamId.CompareTo(b.observerTeamId);
            if (teamCompare != 0) return teamCompare;
            int xCompare = a.x.CompareTo(b.x);
            return xCompare != 0 ? xCompare : a.y.CompareTo(b.y);
        });
    }

    public void ImportFogConstructionMemory(IList<FogConstructionMemorySaveData> source)
    {
        fogConstructionMemoryBySlot.Clear();
        if (source == null)
            return;

        Tilemap boardMap = ResolveFogBoardTilemap();
        for (int i = 0; i < source.Count; i++)
        {
            FogConstructionMemorySaveData saved = source[i];
            if (saved == null || !Enum.IsDefined(typeof(TeamId), saved.observerTeamId) ||
                !Enum.IsDefined(typeof(TeamId), saved.knownOwnerTeamId))
            {
                continue;
            }

            Vector3Int cell = new Vector3Int(saved.x, saved.y, 0);
            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
            if (construction == null ||
                !construction.TryResolveConstructionData(out ConstructionData data) || data == null)
            {
                continue;
            }

            string dataId = !string.IsNullOrWhiteSpace(data.id) ? data.id : data.name;
            if (!string.Equals(dataId, saved.constructionDataId, StringComparison.OrdinalIgnoreCase))
                continue;

            int observerSlotIndex = saved.observerSlotIndex;
            PlayerSlotId migratedSlot = PlayerSlotId.Invalid;
            if (!IsValidPlayerSlotIndex(observerSlotIndex) &&
                !TryGetUniqueSlotForTeam((TeamId)saved.observerTeamId, out migratedSlot))
                continue;
            if (!IsValidPlayerSlotIndex(observerSlotIndex))
                observerSlotIndex = migratedSlot.Value;

            if (!fogConstructionMemoryBySlot.TryGetValue(observerSlotIndex, out Dictionary<Vector3Int, FogConstructionMemoryEntry> memory))
            {
                memory = new Dictionary<Vector3Int, FogConstructionMemoryEntry>();
                fogConstructionMemoryBySlot[observerSlotIndex] = memory;
            }
            memory[cell] = new FogConstructionMemoryEntry
            {
                data = data,
                knownOwner = (TeamId)saved.knownOwnerTeamId,
                flipX = saved.flipX
            };
        }
    }

    public void ExportFogExplorationMemory(List<TeamExploredCellsSaveData> destination)
    {
        if (destination == null)
            return;
        destination.Clear();

        foreach (KeyValuePair<int, HashSet<Vector3Int>> pair in fogExploredCellsBySlot)
        {
            if (!IsValidPlayerSlotIndex(pair.Key))
                continue;

            var saved = new TeamExploredCellsSaveData
            {
                slotIndex = pair.Key,
                teamId = (int)GetVisualTeamForSlot(PlayerSlotId.FromIndex(pair.Key))
            };
            if (pair.Value != null)
                saved.cells.AddRange(pair.Value);
            saved.cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            destination.Add(saved);
        }

        destination.Sort((a, b) => a.teamId.CompareTo(b.teamId));
    }

    public void ImportFogExplorationMemory(IList<TeamExploredCellsSaveData> source)
    {
        fogExploredCellsBySlot.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            TeamExploredCellsSaveData saved = source[i];
            if (saved == null || !Enum.IsDefined(typeof(TeamId), saved.teamId))
            {
                continue;
            }

            var explored = new HashSet<Vector3Int>();
            if (saved.cells != null)
            {
                for (int c = 0; c < saved.cells.Count; c++)
                {
                    Vector3Int cell = saved.cells[c];
                    cell.z = 0;
                    explored.Add(cell);
                }
            }
            int slotIndex = saved.slotIndex;
            PlayerSlotId migratedSlot = PlayerSlotId.Invalid;
            if (!IsValidPlayerSlotIndex(slotIndex) &&
                !TryGetUniqueSlotForTeam((TeamId)saved.teamId, out migratedSlot))
                continue;
            if (!IsValidPlayerSlotIndex(slotIndex))
                slotIndex = migratedSlot.Value;
            fogExploredCellsBySlot[slotIndex] = explored;
        }
    }

    private void ResetFogOfWarRuntime(bool clearTilemap)
    {
        fogBoardCellsBuffer.Clear();
        fogVisibleCellsBuffer.Clear();
        fogDisplayVisibleCellsBuffer.Clear();
        fogRenderedVisibleCellsBuffer.Clear();
        fogRenderedVisibleCellsValid = false;
        fogContributionsBySource.Clear();
        fogGeographicContributorsByCell.Clear();
        fogSensorContributorsByCell.Clear();
        fogUnitVisibilityByCacheIndex.Clear();
        fogUnitVisibleScratchBuffer.Clear();
        fogCachedObserverSlotIndex = int.MinValue;
        fogRenderedObserverSlotIndex = int.MinValue;
        fogOverlayInitialized = false;
        if (clearTilemap && fogOfWarTilemap != null)
            fogOfWarTilemap.ClearAllTiles();
        if (clearTilemap && fogOfWarMemoryTilemap != null)
            fogOfWarMemoryTilemap.ClearAllTiles();
        if (clearTilemap && fogOfWarBreakwaterMemoryTilemap != null)
            fogOfWarBreakwaterMemoryTilemap.ClearAllTiles();
        if (clearTilemap)
        {
            fogContributionRuntimeBySlot.Clear();
            SetFogConstructionMemoryRenderersActive(0);
            SetFogStructureMemoryRenderersActive(0);
        }
    }

    private void StoreFogContributionRuntimeForSlot(PlayerSlotId observerSlot)
    {
        if (!observerSlot.IsValid || fogCachedObserverSlotIndex != observerSlot.Value ||
            !fogOverlayInitialized)
        {
            return;
        }

        if (!fogContributionRuntimeBySlot.TryGetValue(
                observerSlot.Value,
                out FogSlotContributionRuntime stored))
        {
            stored = new FogSlotContributionRuntime();
            fogContributionRuntimeBySlot[observerSlot.Value] = stored;
        }

        stored.sources.Clear();
        foreach (KeyValuePair<FogContributionSourceId, FogSourceContributionCacheEntry> pair
                 in fogContributionsBySource)
        {
            FogSourceContributionCacheEntry source = pair.Value;
            if (source == null)
                continue;
            FogSourceContributionCacheEntry clone = new FogSourceContributionCacheEntry
            {
                unitCacheKey = source.unitCacheKey,
                sourceStateHash = source.sourceStateHash
            };
            clone.geographicCells.UnionWith(source.geographicCells);
            clone.sensorCells.UnionWith(source.sensorCells);
            stored.sources[pair.Key] = clone;
        }

        CopyFogContributorCounts(fogGeographicContributorsByCell, stored.geographicContributors);
        CopyFogContributorCounts(fogSensorContributorsByCell, stored.sensorContributors);
    }

    private bool TryActivateFogContributionRuntimeForSlot(
        PlayerSlotId observerSlot,
        Tilemap boardMap)
    {
        if (!observerSlot.IsValid || boardMap == null ||
            !fogContributionRuntimeBySlot.TryGetValue(
                observerSlot.Value,
                out FogSlotContributionRuntime stored) ||
            stored == null || stored.sources.Count == 0)
        {
            return false;
        }

        int previouslyRenderedObserverSlot = fogRenderedObserverSlotIndex;
        fogContributionsBySource.Clear();
        foreach (KeyValuePair<FogContributionSourceId, FogSourceContributionCacheEntry> pair
                 in stored.sources)
        {
            FogSourceContributionCacheEntry source = pair.Value;
            if (source == null)
                continue;
            FogSourceContributionCacheEntry clone = new FogSourceContributionCacheEntry
            {
                unitCacheKey = source.unitCacheKey,
                sourceStateHash = source.sourceStateHash
            };
            clone.geographicCells.UnionWith(source.geographicCells);
            clone.sensorCells.UnionWith(source.sensorCells);
            fogContributionsBySource[pair.Key] = clone;
        }

        CopyFogContributorCounts(stored.geographicContributors, fogGeographicContributorsByCell);
        CopyFogContributorCounts(stored.sensorContributors, fogSensorContributorsByCell);
        InitializeFogRuntimeData(boardMap);
        bool visualObserverChanged =
            previouslyRenderedObserverSlot != observerSlot.Value &&
            activeFogUpdateContext.HasValue &&
            activeFogUpdateContext.Value.publishVisuals &&
            activeFogUpdateContext.Value.observerSlot == observerSlot;
        if (visualObserverChanged)
        {
            // O buffer representa o estado desenhado pelo observador anterior.
            // Mesmo uma celula escondida nos dois slots precisa ser recolorida:
            // o alpha de explored e a memoria pertencem ao novo PlayerSlotId.
            fogRenderedVisibleCellsBuffer.Clear();
            fogRenderedVisibleCellsValid = false;
            if (enableFogStepPerfLogs)
            {
                Debug.Log(
                    $"[FoW][PresentationSwitch] from={previouslyRenderedObserverSlot} " +
                    $"to={observerSlot.Value} overlayInvalidated=true");
            }
        }
        return fogOverlayInitialized && fogCachedObserverSlotIndex == observerSlot.Value;
    }

    private static void CopyFogContributorCounts(
        Dictionary<Vector3Int, int> source,
        Dictionary<Vector3Int, int> destination)
    {
        destination.Clear();
        foreach (KeyValuePair<Vector3Int, int> pair in source)
        {
            if (pair.Value > 0)
                destination[pair.Key] = pair.Value;
        }
    }

    private void InitializeFogOverlay(Tilemap boardMap)
    {
        fogCachedObserverSlotIndex = ActiveSlotId.Value;
        if (!IsFogVisualWriteAuthorized("initialize_overlay"))
        {
            fogOverlayInitialized = false;
            return;
        }

        fogBoardCellsBuffer.Clear();
        CollectBoardCells(boardMap, fogBoardCellsBuffer);
        if (fogBoardCellsBuffer.Count <= 0)
        {
            fogOfWarTilemap.ClearAllTiles();
            fogOverlayInitialized = false;
            return;
        }

        fogOfWarTilemap.ClearAllTiles();
        for (int i = 0; i < fogBoardCellsBuffer.Count; i++)
        {
            Vector3Int cell = fogBoardCellsBuffer[i];
            TileBase tile = ResolveFogTileForCell(boardMap, cell);
            if (tile == null)
                continue;

            fogOfWarTilemap.SetTile(cell, tile);
            fogOfWarTilemap.SetTileFlags(cell, TileFlags.None);
            fogOfWarTilemap.SetColor(cell, ResolveFogColorForCell(cell));
        }

        fogOverlayInitialized = true;
        fogRenderedObserverSlotIndex = ActiveSlotId.Value;
        // InitializeFogOverlay acabou de desenhar nevoa em todas as celulas.
        fogRenderedVisibleCellsBuffer.Clear();
        fogRenderedVisibleCellsValid = true;
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
        fogCachedObserverSlotIndex = ActiveSlotId.Value;
        fogOverlayInitialized = true;
    }

    // Desenha o overlay a partir do canal geografico ja calculado.
    // Deve ser chamado apenas após todos os UpdateFogVisibilityForUnit do turno terem rodado.
    private void RenderFogOverlayFromRuntimeCache(Tilemap boardMap)
    {
        if (!IsFogVisualWriteAuthorized("render_overlay"))
            return;

        if (fogOfWarVisionMode == FogOfWarVisionMode.All)
            BuildFogDisplayVisibleCellsForAllModes(boardMap, fogDisplayVisibleCellsBuffer);
        else
            BuildFogDisplayVisibleCellsForMode(boardMap, fogOfWarVisionMode, fogDisplayVisibleCellsBuffer);

        RenderFogExplorationMemory(boardMap, fogDisplayVisibleCellsBuffer);

        if (!fogRenderedVisibleCellsValid)
        {
            fogOfWarTilemap.ClearAllTiles();
            for (int i = 0; i < fogBoardCellsBuffer.Count; i++)
            {
                Vector3Int cell = fogBoardCellsBuffer[i];
                if (fogDisplayVisibleCellsBuffer.Contains(cell))
                    continue;
                TileBase tile = ResolveFogTileForCell(boardMap, cell);
                if (tile == null)
                    continue;
                fogOfWarTilemap.SetTile(cell, tile);
                fogOfWarTilemap.SetTileFlags(cell, TileFlags.None);
                fogOfWarTilemap.SetColor(cell, ResolveFogColorForCell(cell));
            }

            fogRenderedVisibleCellsBuffer.Clear();
            fogRenderedVisibleCellsBuffer.UnionWith(fogDisplayVisibleCellsBuffer);
            fogRenderedVisibleCellsValid = true;
            fogRenderedObserverSlotIndex = ResolveFogVisualObserverSlot().Value;
            return;
        }

        // O cache confirmado anterior representa exatamente o que esta desenhado.
        // Atualize apenas celulas cujo estado visivel/nevoa realmente mudou.
        for (int i = 0; i < fogBoardCellsBuffer.Count; i++)
        {
            Vector3Int cell = fogBoardCellsBuffer[i];
            bool wasVisible = fogRenderedVisibleCellsBuffer.Contains(cell);
            bool visible = fogDisplayVisibleCellsBuffer.Contains(cell);
            if (visible == wasVisible)
                continue;
            if (visible)
            {
                fogOfWarTilemap.SetTile(cell, null);
                continue;
            }
            TileBase tile = ResolveFogTileForCell(boardMap, cell);
            if (tile == null)
                continue;
            fogOfWarTilemap.SetTile(cell, tile);
            fogOfWarTilemap.SetTileFlags(cell, TileFlags.None);
            fogOfWarTilemap.SetColor(cell, ResolveFogColorForCell(cell));
        }

        fogRenderedVisibleCellsBuffer.Clear();
        fogRenderedVisibleCellsBuffer.UnionWith(fogDisplayVisibleCellsBuffer);
        fogRenderedObserverSlotIndex = ResolveFogVisualObserverSlot().Value;
    }

    private void RenderFogExplorationMemory(Tilemap boardMap, HashSet<Vector3Int> visibleCells)
    {
        EnsureFogOfWarMemoryTilemap();
        if (fogOfWarMemoryTilemap == null || boardMap == null)
            return;

        fogOfWarMemoryTilemap.ClearAllTiles();
        EnsureFogBreakwaterMemoryTilemap();
        if (fogOfWarBreakwaterMemoryTilemap != null)
            fogOfWarBreakwaterMemoryTilemap.ClearAllTiles();
        SetFogConstructionMemoryRenderersActive(0);
        SetFogStructureMemoryRenderersActive(0);
        int renderedSlotIndex = ResolveFogVisualObserverSlot().Value;
        if (!IsValidPlayerSlotIndex(renderedSlotIndex) ||
            !fogExploredCellsBySlot.TryGetValue(renderedSlotIndex, out HashSet<Vector3Int> explored))
        {
            return;
        }

        foreach (Vector3Int sourceCell in explored)
        {
            Vector3Int cell = sourceCell;
            cell.z = 0;
            if (visibleCells != null && visibleCells.Contains(cell))
                continue;

            TileBase terrainTile = boardMap.GetTile(cell);
            if (terrainTile == null)
                continue;

            fogOfWarMemoryTilemap.SetTile(cell, terrainTile);
            fogOfWarMemoryTilemap.SetTileFlags(cell, TileFlags.None);
            fogOfWarMemoryTilemap.SetColor(cell, Color.white);
        }

        RenderFogBreakwaterMemory(visibleCells, explored);
        RenderFogStructureMemory(boardMap, visibleCells, explored);
        RenderFogConstructionMemory(boardMap, visibleCells, renderedSlotIndex);
    }

    private void RenderFogBreakwaterMemory(HashSet<Vector3Int> visibleCells, HashSet<Vector3Int> explored)
    {
        if (fogOfWarBreakwaterMemoryTilemap == null || explored == null)
            return;
        Tilemap source = FindTilemapByNameOnBoard("quebraMar", boardMap: ResolveFogBoardTilemap());
        if (source == null || source == fogOfWarBreakwaterMemoryTilemap)
            return;

        foreach (Vector3Int sourceCell in explored)
        {
            Vector3Int cell = sourceCell;
            cell.z = 0;
            if (visibleCells != null && visibleCells.Contains(cell))
                continue;
            TileBase tile = source.GetTile(cell);
            if (tile == null)
                continue;
            fogOfWarBreakwaterMemoryTilemap.SetTile(cell, tile);
            fogOfWarBreakwaterMemoryTilemap.SetTileFlags(cell, TileFlags.None);
            fogOfWarBreakwaterMemoryTilemap.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
            fogOfWarBreakwaterMemoryTilemap.SetColor(cell, source.GetColor(cell));
        }
    }

    private void RenderFogStructureMemory(
        Tilemap boardMap,
        HashSet<Vector3Int> visibleCells,
        HashSet<Vector3Int> explored)
    {
        if (boardMap == null || explored == null)
            return;

        int rendererIndex = 0;
        RoadNetworkManager[] networks = FindObjectsByType<RoadNetworkManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int n = 0; n < networks.Length; n++)
        {
            RoadNetworkManager network = networks[n];
            if (network == null || network.BoardTilemap == null
                || network.BoardTilemap.layoutGrid != boardMap.layoutGrid)
                continue;

            SpriteRenderer[] renderers = network.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                SpriteRenderer source = renderers[r];
                if (!network.TryGetGeneratedVisualCells(source, out Vector3Int fromCell, out Vector3Int toCell))
                    continue;
                fromCell.z = 0;
                toCell.z = 0;
                bool fromExplored = explored.Contains(fromCell);
                bool toExplored = explored.Contains(toCell);
                if (!fromExplored && !toExplored)
                    continue;
                bool fromVisible = visibleCells != null && visibleCells.Contains(fromCell);
                bool toVisible = visibleCells != null && visibleCells.Contains(toCell);
                bool fromKnownHidden = fromExplored && !fromVisible;
                bool toKnownHidden = toExplored && !toVisible;
                if (!fromKnownHidden && !toKnownHidden)
                    continue;

                SpriteRenderer memory = GetOrCreateFogStructureMemoryRenderer(rendererIndex++);
                memory.sprite = source.sprite;
                memory.color = source.color;
                memory.flipX = source.flipX;
                memory.flipY = source.flipY;
                memory.drawMode = source.drawMode;
                memory.size = source.size;
                memory.maskInteraction = SpriteMaskInteraction.None;
                memory.sortingLayerName = "FogOfWarTile";
                memory.sortingOrder = 2;
                Vector3 memoryPosition = source.transform.position;
                Vector3 memoryWorldScale = source.transform.lossyScale;
                // A fotografia pode ocupar somente a metade pertencente ao hex
                // conhecido e atualmente oculto. Isso tambem vale quando o outro
                // extremo ja foi explorado, mas esta visivel agora: copiar o
                // segmento inteiro faria a camada FogOfWarTile atravessar o recorte
                // aberto e cobrir unidades reais naquele hex.
                if (fromKnownHidden != toKnownHidden)
                {
                    Vector3Int hiddenCell = fromKnownHidden ? fromCell : toCell;
                    Vector3Int otherCell = fromKnownHidden ? toCell : fromCell;
                    Vector3 hiddenCenter = boardMap.GetCellCenterWorld(hiddenCell);
                    Vector3 otherCenter = boardMap.GetCellCenterWorld(otherCell);
                    memoryPosition = Vector3.Lerp(hiddenCenter, otherCenter, 0.25f);
                    memoryPosition.z = source.transform.position.z;
                    memoryWorldScale.y *= 0.5f;
                }
                memory.transform.position = memoryPosition;
                memory.transform.rotation = source.transform.rotation;
                memory.transform.localScale = ResolveLocalScaleForFogMemory(memoryWorldScale);
                memory.gameObject.SetActive(true);
            }
        }
        SetFogStructureMemoryRenderersActive(rendererIndex);
    }

    private SpriteRenderer GetOrCreateFogStructureMemoryRenderer(int index)
    {
        while (fogStructureMemoryRenderers.Count <= index)
        {
            GameObject memoryObject = new GameObject($"FogStructureMemory_{fogStructureMemoryRenderers.Count}");
            memoryObject.transform.SetParent(fogOfWarMemoryTilemap.transform, false);
            fogStructureMemoryRenderers.Add(memoryObject.AddComponent<SpriteRenderer>());
        }
        return fogStructureMemoryRenderers[index];
    }

    private void SetFogStructureMemoryRenderersActive(int activeCount)
    {
        for (int i = 0; i < fogStructureMemoryRenderers.Count; i++)
        {
            SpriteRenderer renderer = fogStructureMemoryRenderers[i];
            if (renderer != null)
                renderer.gameObject.SetActive(i < activeCount);
        }
    }

    private void RenderFogConstructionMemory(Tilemap boardMap, HashSet<Vector3Int> visibleCells, int renderedSlotIndex)
    {
        if (!fogConstructionMemoryBySlot.TryGetValue(renderedSlotIndex, out Dictionary<Vector3Int, FogConstructionMemoryEntry> memory))
            return;

        int rendererIndex = 0;
        foreach (KeyValuePair<Vector3Int, FogConstructionMemoryEntry> pair in memory)
        {
            if (visibleCells != null && visibleCells.Contains(pair.Key))
                continue;
            FogConstructionMemoryEntry entry = pair.Value;
            if (entry == null || entry.data == null)
                continue;

            ConstructionManager liveConstruction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, pair.Key);
            SpriteRenderer liveRenderer = liveConstruction != null ? liveConstruction.GetMainSpriteRenderer() : null;
            if (liveRenderer == null)
                continue;

            Sprite sprite = TeamUtils.GetTeamSprite(entry.data, entry.knownOwner);
            if (sprite == null)
                sprite = liveRenderer.sprite;
            if (sprite == null)
                continue;

            SpriteRenderer memoryRenderer = GetOrCreateFogConstructionMemoryRenderer(rendererIndex++);
            memoryRenderer.sprite = sprite;
            memoryRenderer.color = TeamUtils.GetColor(entry.knownOwner);
            memoryRenderer.flipX = entry.flipX;
            memoryRenderer.flipY = liveRenderer.flipY;
            memoryRenderer.drawMode = liveRenderer.drawMode;
            memoryRenderer.size = liveRenderer.size;
            memoryRenderer.maskInteraction = SpriteMaskInteraction.None;
            memoryRenderer.sortingLayerName = "FogOfWarTile";
            memoryRenderer.sortingOrder = 3;
            memoryRenderer.transform.position = liveRenderer.transform.position;
            memoryRenderer.transform.rotation = liveRenderer.transform.rotation;
            memoryRenderer.transform.localScale = ResolveLocalScaleForFogMemory(liveRenderer.transform.lossyScale);
            memoryRenderer.gameObject.SetActive(true);
        }

        SetFogConstructionMemoryRenderersActive(rendererIndex);
    }

    private SpriteRenderer GetOrCreateFogConstructionMemoryRenderer(int index)
    {
        while (fogConstructionMemoryRenderers.Count <= index)
        {
            GameObject memoryObject = new GameObject($"FogConstructionMemory_{fogConstructionMemoryRenderers.Count}");
            memoryObject.transform.SetParent(fogOfWarMemoryTilemap.transform, false);
            fogConstructionMemoryRenderers.Add(memoryObject.AddComponent<SpriteRenderer>());
        }
        return fogConstructionMemoryRenderers[index];
    }

    private void SetFogConstructionMemoryRenderersActive(int activeCount)
    {
        for (int i = 0; i < fogConstructionMemoryRenderers.Count; i++)
        {
            SpriteRenderer renderer = fogConstructionMemoryRenderers[i];
            if (renderer != null)
                renderer.gameObject.SetActive(i < activeCount);
        }
    }

    private Vector3 ResolveLocalScaleForFogMemory(Vector3 desiredWorldScale)
    {
        Vector3 parentScale = fogOfWarMemoryTilemap != null
            ? fogOfWarMemoryTilemap.transform.lossyScale
            : Vector3.one;
        return new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? desiredWorldScale.x : desiredWorldScale.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? desiredWorldScale.y : desiredWorldScale.y / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? desiredWorldScale.z : desiredWorldScale.z / parentScale.z);
    }

    private void BuildFogDisplayVisibleCellsForAllModes(Tilemap boardMap, HashSet<Vector3Int> output, UnitManager excludeUnit = null)
    {
        if (output == null)
            return;
        output.Clear();

        // A visao comum ja foi calculada por UpdateFogVisibilityForUnit. Reaproveitar esse
        // cache evita recalcular Air + Surface + Sub para toda unidade a cada spawn/refresh.
        foreach (KeyValuePair<Vector3Int, int> entry in fogGeographicContributorsByCell)
        {
            if (entry.Value > 0)
                output.Add(entry.Key);
        }

        // DETECCAO NAO REVELA FOW. As Detect Specializations — o radar aereo do
        // EWACS, o sonar do submarino — fazem UNIDADE aparecer, nunca hexagono.
        // Quem revela terreno e o campo visao, pelo PodeEnxergar, e ele ja
        // alimentou fogGeographicContributorsByCell acima.
        //
        // A varredura de especializacoes de Air que existia aqui era a segunda
        // porta pela qual o alcance aereo virava terreno conhecido, e daqui ela
        // seguia para knownCells e para a memoria permanente de exploracao.
        AddFriendlyConstructionDisplayCells(boardMap, output);
    }

    private void AddCachedFogLayerVisibleCellsForUnit(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqConfig,
        Domain targetDomain,
        HeightLevel targetHeight,
        HashSet<Vector3Int> output)
    {
        FogSpecializedViewCacheKey key = new FogSpecializedViewCacheKey(
            ResolveFogCacheIndex(unit),
            BuildFogUnitSnapshotHash(unit, boardMap),
            BuildFogSensorFlagsHash(enableLosValidation),
            targetDomain,
            targetHeight);

        if (!fogSpecializedViewCellsByUnit.TryGetValue(key, out HashSet<Vector3Int> cachedCells))
        {
            // Limite defensivo: posicoes antigas deixam de ser uteis depois que as unidades se movem.
            if (fogSpecializedViewCellsByUnit.Count >= 256)
                fogSpecializedViewCellsByUnit.Clear();

            fogVisibleCellsBuffer.Clear();
            AddFogLayerVisibleCellsForUnit(
                unit, boardMap, terrainDatabase, dpqConfig, targetDomain, targetHeight, fogVisibleCellsBuffer);
            cachedCells = new HashSet<Vector3Int>(fogVisibleCellsBuffer);
            fogSpecializedViewCellsByUnit[key] = cachedCells;
        }

        output.UnionWith(cachedCells);
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
            if (unit.SlotIndex != ActiveSlotId.Value)
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

        // Construcoes aliadas usam o mesmo alcance configurado em todas as camadas.
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
            forcedVirtualTargetHeight: targetHeight);
    }

    /// <summary>
    /// Consulta visual de uma unica unidade para o inspector. Nao publica FOW,
    /// nao altera contatos e nao alimenta caches confirmados.
    /// </summary>
    public void CollectInspectionVisibleCells(
        UnitManager unit,
        Tilemap boardMap,
        FogOfWarVisionMode mode,
        HashSet<Vector3Int> output)
    {
        if (output == null)
            return;

        output.Clear();
        if (unit == null || boardMap == null || unit.IsEmbarked)
            return;

        TerrainDatabase terrainDatabase = ResolveFogTerrainDatabase();
        DPQAirHeightConfig dpqConfig = ResolveFogDpqAirHeightConfig();

        if (mode == FogOfWarVisionMode.All || mode == FogOfWarVisionMode.Air)
        {
            AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig,
                Domain.Air, HeightLevel.AirLow, output);
            AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig,
                Domain.Air, HeightLevel.AirHigh, output);
        }

        if (mode == FogOfWarVisionMode.All || mode == FogOfWarVisionMode.Surface)
        {
            AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig,
                Domain.Land, HeightLevel.Surface, output);
            AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig,
                Domain.Naval, HeightLevel.Surface, output);
        }

        if (mode == FogOfWarVisionMode.All || mode == FogOfWarVisionMode.Sub)
        {
            AddFogLayerVisibleCellsForUnit(unit, boardMap, terrainDatabase, dpqConfig,
                Domain.Submarine, HeightLevel.Submerged, output);
        }
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
            if (boardMap.GetTile(cell) == null)
                continue;

            int visionRange = 0;
            if (construction.TryResolveConstructionData(out ConstructionData constructionData) &&
                constructionData != null)
            {
                visionRange = Mathf.Max(0, constructionData.visao);
            }

            HashSet<Vector3Int> visibleCells = BuildCellsInRadius(boardMap, cell, visionRange);
            foreach (Vector3Int visibleCell in visibleCells)
                output.Add(visibleCell);
        }
    }

    private void UpdateFogVisibilityForUnit(
        UnitManager unit,
        Tilemap boardMap,
        out double collectMs,
        out int visibleCellsCollected,
        out bool collectExecuted,
        bool updateVisual = true,
        HashSet<Vector3Int> affectedTargetCells = null)
    {
        collectMs = 0d;
        visibleCellsCollected = 0;
        collectExecuted = false;

        if (unit == null)
            return;

        int cacheIndex = ResolveFogCacheIndex(unit);
        FogContributionSourceId sourceId = ResolveFogContributionSourceId(unit);
        FogOfWarUnitCacheKey nextKey = BuildFogUnitCacheKey(unit, boardMap);
        if (fogContributionsBySource.TryGetValue(sourceId, out FogSourceContributionCacheEntry cacheEntry) &&
            cacheEntry != null &&
            cacheEntry.unitCacheKey.Equals(nextKey))
        {
            return;
        }

        if (cacheEntry == null)
        {
            cacheEntry = new FogSourceContributionCacheEntry();
            fogContributionsBySource[sourceId] = cacheEntry;
        }

        if (affectedTargetCells != null)
        {
            affectedTargetCells.UnionWith(cacheEntry.geographicCells);
            affectedTargetCells.UnionWith(cacheEntry.sensorCells);
        }
        RemoveFogSourceContributions(cacheEntry, boardMap, updateVisual);

        if (!unit.gameObject.activeInHierarchy || unit.IsEmbarked ||
            unit.SlotIndex != ActiveSlotId.Value)
        {
            RemoveFogSpecializedViewCacheForUnit(cacheIndex);
            cacheEntry.unitCacheKey = nextKey;
            cacheEntry.sourceStateHash = BuildFogUnitSourceStateHash(unit);
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
            AddFogSourceGeographicContribution(cacheEntry, cell, boardMap, updateVisual);
            AddFogSourceSensorContribution(cacheEntry, cell, boardMap);
        }

        cacheEntry.unitCacheKey = nextKey;
        cacheEntry.sourceStateHash = BuildFogUnitSourceStateHash(unit);
        if (affectedTargetCells != null)
        {
            affectedTargetCells.UnionWith(cacheEntry.geographicCells);
            affectedTargetCells.UnionWith(cacheEntry.sensorCells);
        }
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
        if (turnStateManager != null &&
            turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        {
            // A fila de morte ainda esta em apresentacao provisoria. A fonte so
            // deixa o snapshot confirmado depois do retorno a Neutral.
            CommittedBoardDelta delta = new CommittedBoardDelta();
            delta.RequireReconciliation(CommittedBoardChangeKind.UnitRemoved);
            SubmitCommittedBoardDelta(delta);
            return;
        }

        Tilemap boardMap = ResolveFogBoardTilemap();
        if (boardMap == null)
            return;

        int cacheIndex = ResolveFogCacheIndex(unit);
        FogContributionSourceId sourceId = ResolveFogContributionSourceId(unit);
        RemoveFogSpecializedViewCacheForUnit(cacheIndex);
        if (fogContributionsBySource.TryGetValue(sourceId, out FogSourceContributionCacheEntry cacheEntry) &&
            cacheEntry != null)
        {
            RemoveFogSourceContributions(cacheEntry, boardMap, updateVisual: true);
            fogContributionsBySource.Remove(sourceId);
        }

        fogUnitVisibilityByCacheIndex[cacheIndex] = false;
        RefreshRuntimeUnitFogVisibility();
        if (fogOfWarVisionMode != FogOfWarVisionMode.All)
            RenderFogOverlayFromRuntimeCache(boardMap);
        OnFogOfWarUpdated?.Invoke();
    }

    public void NotifyTurnStateReturnedToNeutral()
    {
        CommittedBoardDelta delta = pendingCommittedBoardDelta;
        pendingCommittedBoardDelta = null;
        if (delta == null || delta.IsEmpty)
            return;

        ApplyCommittedBoardDelta(delta);
    }

    private void RemoveFogSpecializedViewCacheForUnit(int unitIndex)
    {
        if (fogSpecializedViewCellsByUnit.Count == 0)
            return;

        List<FogSpecializedViewCacheKey> keysToRemove = null;
        foreach (FogSpecializedViewCacheKey key in fogSpecializedViewCellsByUnit.Keys)
        {
            if (key.unitIndex != unitIndex)
                continue;

            keysToRemove ??= new List<FogSpecializedViewCacheKey>();
            keysToRemove.Add(key);
        }

        if (keysToRemove == null)
            return;

        for (int i = 0; i < keysToRemove.Count; i++)
            fogSpecializedViewCellsByUnit.Remove(keysToRemove[i]);
    }

    private void RemoveFogSourceContributions(
        FogSourceContributionCacheEntry entry,
        Tilemap boardMap,
        bool updateVisual)
    {
        if (entry == null)
            return;

        foreach (Vector3Int cell in entry.geographicCells)
            ApplyFogGeographicContribution(cell, -1, boardMap, updateVisual);
        foreach (Vector3Int cell in entry.sensorCells)
            ApplyFogSensorContribution(cell, -1);

        entry.geographicCells.Clear();
        entry.sensorCells.Clear();
    }

    private void AddFogSourceGeographicContribution(
        FogSourceContributionCacheEntry entry,
        Vector3Int cell,
        Tilemap boardMap,
        bool updateVisual)
    {
        if (entry == null || !IsFogBoardCell(cell, boardMap) || !entry.geographicCells.Add(cell))
            return;
        ApplyFogGeographicContribution(cell, +1, boardMap, updateVisual);
    }

    private void AddFogSourceSensorContribution(
        FogSourceContributionCacheEntry entry,
        Vector3Int cell,
        Tilemap boardMap)
    {
        if (entry == null || !IsFogBoardCell(cell, boardMap) || !entry.sensorCells.Add(cell))
            return;
        ApplyFogSensorContribution(cell, +1);
    }

    private void ApplyFogGeographicContribution(Vector3Int cell, int delta, Tilemap boardMap, bool updateVisual = true)
    {
        if (delta == 0)
            return;

        if (!fogGeographicContributorsByCell.TryGetValue(cell, out int current))
            current = 0;

        int next = Mathf.Max(0, current + delta);
        if (next == current)
            return;

        if (next <= 0)
            fogGeographicContributorsByCell.Remove(cell);
        else
            fogGeographicContributorsByCell[cell] = next;

        if (!updateVisual)
            return;
        if (!IsFogVisualWriteAuthorized("apply_geographic_contribution"))
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
            fogOfWarTilemap.SetColor(cell, ResolveFogColorForCell(cell));
        }
    }

    private void ApplyFogSensorContribution(Vector3Int cell, int delta)
    {
        if (delta == 0)
            return;

        if (!fogSensorContributorsByCell.TryGetValue(cell, out int current))
            current = 0;

        int next = Mathf.Max(0, current + delta);
        if (next <= 0)
            fogSensorContributorsByCell.Remove(cell);
        else
            fogSensorContributorsByCell[cell] = next;
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

            FogContributionSourceId sourceId = ResolveFogContributionSourceId(construction);
            if (!fogContributionsBySource.TryGetValue(sourceId, out FogSourceContributionCacheEntry sourceEntry) ||
                sourceEntry == null)
            {
                sourceEntry = new FogSourceContributionCacheEntry();
                fogContributionsBySource[sourceId] = sourceEntry;
            }
            else
            {
                RemoveFogSourceContributions(sourceEntry, boardMap, updateVisual);
            }
            sourceEntry.sourceStateHash = BuildFogConstructionSourceStateHash(construction);

            // O QG e um marco global do tabuleiro: todos conhecem seu hex.
            // Apenas o dono recebe o restante do raio de visao configurado.
            if (!ownedByActivePlayer)
            {
                constructionsIncluded++;
                AddFogSourceGeographicContribution(sourceEntry, cell, boardMap, updateVisual);
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
                AddFogSourceGeographicContribution(sourceEntry, visibleCell, boardMap, updateVisual);

            // A construcao detecta o ocupante que esta efetivamente sobre ela.
            // O restante do raio apenas revela a geografia.
            AddFogSourceSensorContribution(sourceEntry, cell, boardMap);
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
        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        FogCollectPerfEntry candidate = new FogCollectPerfEntry(
            unitName,
            unit.name,
            unit.SlotIndex,
            unitCell,
            collectMs,
            visibleCellsCollected);

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

        PlayerSlotId observerSlot = ActiveSlotId;
        bool useHumanPresentation = TryResolveFogPresentationSlot(out PlayerSlotId presentationSlot);
        bool fogOverlayOwnsWorldOcclusion = UsesFogOverlayForWorldOcclusion();
        if (useHumanPresentation)
            observerSlot = presentationSlot;
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

            bool visible = unit.SlotIndex == observerSlot.Value
                || ComputeIsUnitVisibleForSlotWithoutCache(unit, observerSlot);
            fogUnitVisibilityByCacheIndex[ResolveFogCacheIndex(unit)] = visible;
            ApplyFogDetectedContactPresentation(
                unit,
                observerSlot,
                fogOverlayOwnsWorldOcclusion);
            unit.SetFogOfWarVisibility(ResolveFogRenderVisibility(
                unit, visible, fogOverlayOwnsWorldOcclusion, observerSlot));
        }

        RefreshStackedHexFrontRendering(units, boardMap, observerSlot);
    }

    private void RefreshRuntimeUnitFogVisibilityForCells(
        HashSet<Vector3Int> affectedTargetCells)
    {
        if (affectedTargetCells == null ||
            affectedTargetCells.Count == 0 ||
            fogUnitVisibilityByCacheIndex.Count == 0)
        {
            RefreshRuntimeUnitFogVisibility();
            return;
        }

        if (!debugFogOfWarEnabled || !enableTotalWar ||
            gameSetup != GameSetupPreset.FogOfWarTotal)
        {
            ShowAllUnitsIgnoringFog();
            return;
        }

        PlayerSlotId observerSlot = ActiveSlotId;
        if (TryResolveFogPresentationSlot(out PlayerSlotId presentationSlot))
            observerSlot = presentationSlot;

        bool fogOverlayOwnsWorldOcclusion = UsesFogOverlayForWorldOcclusion();
        Tilemap boardMap = ResolveFogBoardTilemap();
        List<UnitManager> units = UnitManager.AllActive;
        int evaluatedTargets = 0;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if (boardMap != null && !IsUnitOnBoard(unit, boardMap))
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            if (!affectedTargetCells.Contains(cell))
                continue;

            bool visible = unit.SlotIndex == observerSlot.Value ||
                           ComputeIsUnitVisibleForSlotWithoutCache(unit, observerSlot);
            fogUnitVisibilityByCacheIndex[ResolveFogCacheIndex(unit)] = visible;
            ApplyFogDetectedContactPresentation(
                unit,
                observerSlot,
                fogOverlayOwnsWorldOcclusion);
            unit.SetFogOfWarVisibility(ResolveFogRenderVisibility(
                unit,
                visible,
                fogOverlayOwnsWorldOcclusion,
                observerSlot));
            evaluatedTargets++;
        }

        // A prioridade visual de pilhas pode mudar nas células de origem e
        // destino; a rotina não recalcula sensores nem memória.
        RefreshStackedHexFrontRendering(units, boardMap, observerSlot);
        if (enableFogStepPerfLogs)
        {
            Debug.Log(
                $"[FoW][AffectedTargets][Visual] slot={observerSlot.Value} " +
                $"cells={affectedTargetCells.Count} evaluated={evaluatedTargets}");
        }
    }

    // Scratch do passo de empilhamento multicamada (ver metodo abaixo).
    private readonly HashSet<Vector3Int> stackedHexOtherTeamCellsScratch = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> stackedHexSubmergedCellsScratch = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> stackedHexSurfaceCellsScratch = new HashSet<Vector3Int>();

    // Hex multicamada compartilhado: Submarine/Submerged fica na frente de uma
    // unidade Surface no mesmo hex, tanto sob navio quanto sob Exercito em ponte.
    // Nos demais empilhamentos entre times, preserva a prioridade da unidade do
    // observador. E apenas apresentacao; o FoW ainda decide se o sprite aparece.
    private void RefreshStackedHexFrontRendering(
        List<UnitManager> units,
        Tilemap boardMap,
        PlayerSlotId observerSlot)
    {
        if (units == null)
            return;

        stackedHexOtherTeamCellsScratch.Clear();
        stackedHexSubmergedCellsScratch.Clear();
        stackedHexSurfaceCellsScratch.Clear();

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked || unit.IsDead)
                continue;
            if (boardMap != null && !IsUnitOnBoard(unit, boardMap))
                continue;
            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            bool submerged = unit.GetDomain() == Domain.Submarine || unit.GetHeightLevel() == HeightLevel.Submerged;
            if (submerged)
                stackedHexSubmergedCellsScratch.Add(cell);
            else if (unit.GetHeightLevel() == HeightLevel.Surface)
                stackedHexSurfaceCellsScratch.Add(cell);

            if (unit.SlotIndex != observerSlot.Value)
                stackedHexOtherTeamCellsScratch.Add(cell);
        }

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;

            bool eligible = unit.gameObject.activeInHierarchy
                && !unit.IsEmbarked
                && !unit.IsDead
                && (boardMap == null || IsUnitOnBoard(unit, boardMap));
            bool front = false;
            if (eligible)
            {
                Vector3Int cell = unit.CurrentCellPosition;
                cell.z = 0;
                bool layeredSubmarineStack = stackedHexSubmergedCellsScratch.Contains(cell)
                    && stackedHexSurfaceCellsScratch.Contains(cell);
                if (layeredSubmarineStack)
                    front = unit.GetDomain() == Domain.Submarine || unit.GetHeightLevel() == HeightLevel.Submerged;
                else
                    front = unit.SlotIndex == observerSlot.Value &&
                            stackedHexOtherTeamCellsScratch.Contains(cell);
            }

            unit.SetStackedHexFrontRendering(front);
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

            bool visible = !useConservativeFog || unit.SlotIndex == ActiveSlotId.Value;
            fogUnitVisibilityByCacheIndex[ResolveFogCacheIndex(unit)] = visible;
            unit.SetFogDetectedContactPresentation(false);
            unit.SetFogOfWarVisibility(ResolveFogRenderVisibility(
                unit, visible, fogOverlayOwnsWorldOcclusion, ActiveSlotId));
        }
    }

    public void SetFogOfWarDebugEnabled(bool enabled)
    {
        debugFogOfWarPartial = false;
        debugFogOfWarEnabled = enabled;
        TryAutoAssignFogOfWarReferences();
        EnsureFogOfWarMemoryTilemap();
        fogSortingLayerValidated = false;
        ValidateFogOfWarSortingLayer();
        if (!enabled)
        {
            ResetFogOfWarRuntime(clearTilemap: true);
            fogGameplaySnapshotsBySlot.Clear();
            ShowAllUnitsIgnoringFog();
            ConstructionManager.RefreshAllOccupancyVisuals();
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
        if (Application.isPlaying && !IsFogVisualWriteAuthorized("set_overlay_alpha"))
            return;

        BoundsInt bounds = fogOfWarTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (fogOfWarTilemap.HasTile(cell))
                fogOfWarTilemap.SetColor(cell, ResolveFogColorForCell(cell));
        }
    }

    private Color ResolveFogColorForCell(Vector3Int cell)
    {
        float alpha = ResolveFogOfWarAlpha();
        int renderedSlotIndex = Application.isPlaying
            ? ResolveFogVisualObserverSlot().Value
            : (fogCachedObserverSlotIndex >= 0 ? fogCachedObserverSlotIndex : ActiveSlotId.Value);
        PlayerSlotId renderedSlot = PlayerSlotId.FromIndex(renderedSlotIndex);
        if (IsValidPlayerSlot(renderedSlot) &&
            IsCellExploredBySlot(renderedSlot, cell))
        {
            float exploredMultiplier = fogOfWarController != null
                ? fogOfWarController.ExploredFogAlphaMultiplier
                : 0.8f;
            alpha *= exploredMultiplier;
        }

        return new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    private float ResolveFogOfWarAlpha()
    {
        return fogOfWarController != null
            ? fogOfWarController.FogOfWarAlpha
            : Mathf.Clamp01(fogOfWarAlpha);
    }

    public void SetFogOfWarDebugPartial()
    {
        debugFogOfWarEnabled = true;
        debugFogOfWarPartial = true;
        TryAutoAssignFogOfWarReferences();
        EnsureFogOfWarMemoryTilemap();
        fogSortingLayerValidated = false;
        ValidateFogOfWarSortingLayer();

        if (enableTotalWar)
            RefreshFogOfWarForActiveTeam();
        else
            ResetFogOfWarRuntime(clearTilemap: true);

        RefreshRuntimeUnitFogVisibility();
        string perspective = TryResolveFogPresentationSlot(out PlayerSlotId aiSlot)
            ? $"slot AI {aiSlot.Value}"
            : "slot ativo (nenhuma AI configurada)";
        Debug.Log($"[Debug Command] FoW PARTIAL (debug): perspectiva no {perspective}.");
    }

    public void SetPanelRodadaDebugEnabled(bool enabled)
    {
        debugPanelRodadaEnabled = enabled;
        if (!Application.isPlaying)
            return;
        if (panelRodada == null)
            panelRodada = FindAnyObjectByType<PanelRodadaController>(FindObjectsInactive.Include);
        if (panelRodada == null)
            return;

        if (!enabled)
        {
            panelRodada.CancelLoadingPresentation();
            hotSeatGateActive = false;
            return;
        }

        if (!ShouldUseHotSeatPrivacyCurtain())
        {
            panelRodada.HidePrivacyCurtainForDebug();
            return;
        }

        panelRodada.ShowPrivacyCurtain(
            ClampToTeamId(activeTeamId),
            currentTurn,
            IsActiveTeamAI());
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
            unit.SetFogDetectedContactPresentation(false);
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

    private static FogContributionSourceId ResolveFogContributionSourceId(UnitManager unit)
    {
        return new FogContributionSourceId(
            FogContributionSourceType.Unit,
            ResolveFogCacheIndex(unit));
    }

    private static FogContributionSourceId ResolveFogContributionSourceId(ConstructionManager construction)
    {
        if (construction == null)
            return new FogContributionSourceId(FogContributionSourceType.Construction, 0);

        int instanceId = construction.InstanceId;
        if (instanceId <= 0)
            instanceId = construction.GetEntityId().GetHashCode();
        return new FogContributionSourceId(FogContributionSourceType.Construction, instanceId);
    }

    private static int BuildFogUnitSourceStateHash(UnitManager unit)
    {
        unchecked
        {
            if (unit == null)
                return 0;

            Vector3Int cell = unit.CurrentCellPosition;
            int hash = 17;
            hash = (hash * 31) + cell.x;
            hash = (hash * 31) + cell.y;
            hash = (hash * 31) + unit.SlotIndex;
            hash = (hash * 31) + (int)unit.GetDomain();
            hash = (hash * 31) + (int)unit.GetHeightLevel();
            hash = (hash * 31) + (unit.IsEmbarked ? 1 : 0);
            hash = (hash * 31) + Mathf.Max(1, unit.Visao);
            if (unit.TryGetUnitData(out UnitData data) && data != null)
                hash = (hash * 31) + StableFogStringHash(!string.IsNullOrWhiteSpace(data.id) ? data.id : data.name);
            return hash;
        }
    }

    private static int BuildFogConstructionSourceStateHash(ConstructionManager construction)
    {
        unchecked
        {
            if (construction == null)
                return 0;

            Vector3Int cell = construction.CurrentCellPosition;
            int hash = 17;
            hash = (hash * 31) + cell.x;
            hash = (hash * 31) + cell.y;
            hash = (hash * 31) + construction.SlotIndex;
            hash = (hash * 31) + (construction.IsPlayerHeadQuarter ? 1 : 0);
            if (construction.TryResolveConstructionData(out ConstructionData data) && data != null)
            {
                hash = (hash * 31) + Mathf.Max(0, data.visao);
                hash = (hash * 31) + StableFogStringHash(!string.IsNullOrWhiteSpace(data.id) ? data.id : data.name);
            }
            return hash;
        }
    }

    private static int StableFogStringHash(string value)
    {
        unchecked
        {
            int hash = 23;
            if (string.IsNullOrEmpty(value))
                return hash;
            for (int i = 0; i < value.Length; i++)
                hash = (hash * 31) + value[i];
            return hash;
        }
    }

    private int BuildFogSourceCacheConfigHash(Tilemap boardMap)
    {
        unchecked
        {
            if (boardMap == null)
                return 0;

            int hash = 17;
            hash = (hash * 31) + StableFogStringHash(boardMap.gameObject.scene.name);
            hash = (hash * 31) + StableFogStringHash(boardMap.name);
            hash = (hash * 31) + (enableLosValidation ? 1 : 0);
            hash = (hash * 31) + (enableStealthValidation ? 1 : 0);
            TerrainDatabase terrain = ResolveFogTerrainDatabase();
            DPQAirHeightConfig airConfig = ResolveFogDpqAirHeightConfig();
            hash = (hash * 31) + StableFogStringHash(terrain != null ? terrain.name : string.Empty);
            hash = (hash * 31) + StableFogStringHash(airConfig != null ? airConfig.name : string.Empty);

            BoundsInt bounds = boardMap.cellBounds;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    TileBase tile = boardMap.GetTile(cell);
                    if (tile == null)
                        continue;
                    hash = (hash * 31) + x;
                    hash = (hash * 31) + y;
                    hash = (hash * 31) + StableFogStringHash(tile.name);
                }
            }
            return hash;
        }
    }

    private static void SortFogCellList(List<Vector3Int> cells)
    {
        cells?.Sort((a, b) =>
        {
            int byY = a.y.CompareTo(b.y);
            if (byY != 0)
                return byY;
            int byX = a.x.CompareTo(b.x);
            return byX != 0 ? byX : a.z.CompareTo(b.z);
        });
    }

    private static long ComputeFogSourceContributionChecksum(FogSourceContributionSaveData source)
    {
        unchecked
        {
            if (source == null)
                return 0L;

            long hash = 1469598103934665603L;
            hash = (hash ^ source.observerSlotIndex) * 1099511628211L;
            hash = (hash ^ source.sourceType) * 1099511628211L;
            hash = (hash ^ source.sourceInstanceId) * 1099511628211L;
            hash = (hash ^ source.sourceStateHash) * 1099511628211L;
            hash = AppendFogCellsToChecksum(hash, source.geographicCells);
            return AppendFogCellsToChecksum(hash, source.sensorCells);
        }
    }

    private static long AppendFogCellsToChecksum(long hash, IList<Vector3Int> cells)
    {
        unchecked
        {
            int count = cells?.Count ?? 0;
            hash = (hash ^ count) * 1099511628211L;
            for (int i = 0; i < count; i++)
            {
                Vector3Int cell = cells[i];
                hash = (hash ^ cell.x) * 1099511628211L;
                hash = (hash ^ cell.y) * 1099511628211L;
                hash = (hash ^ cell.z) * 1099511628211L;
            }
            return hash;
        }
    }

    private FogOfWarUnitCacheKey BuildFogUnitCacheKey(UnitManager unit, Tilemap boardMap)
    {
        int snapshotHash = BuildFogUnitSnapshotHash(unit, boardMap);
        int globalBoardRevision = ThreatRevisionTracker.GlobalBoardRevision;
        int teamObserverRevision = ThreatRevisionTracker.GetSlotObserverRevision(ActiveSlotId);
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
            hash = (hash * 31) + unit.SlotIndex;
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
        // Consumido uma vez so: o load ja posicionou o cursor onde o jogador
        // estava, e este teleport pertence a virada de turno, nao ao load.
        if (suppressNextHeadQuarterCursorFocus)
        {
            suppressNextHeadQuarterCursorFocus = false;
            return;
        }

        ResolveAndTeleportCursorToActiveTeamAnchor();

        // O cursor ja parou no destino final (QG, ou a unidade mais proxima
        // quando o time nao tem QG). Enquadrar aqui cobre as quatro saidas do
        // metodo acima com uma unica chamada.
        //
        // Instantaneo de proposito: na virada de turno o jogador nao acompanha
        // uma panoramica de A para B, e no inicio da partida nao existe um "de"
        // que signifique algo. SmoothFocus tambem seria interrompido pelo
        // proximo ClampCamera do Update.
        if (cursorController != null && ShouldFocusCameraOnActiveHeadQuarter())
            cursorController.FocusCameraOnCursor(instant: true);
    }

    private void ResolveAndTeleportCursorToActiveTeamAnchor()
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

        if (!TryResolveSlotHeadQuarterCell(ActiveSlotId, out Vector3Int anchorCell))
        {
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
            PlayerSlotId activeSlot = ActiveSlotId;
            if (activeSlot.IsValid
                ? !IsOwnedBySlot(unit, activeSlot)
                : unit.TeamId != TeamId.Neutral)
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

    public bool CanProduceUnit(PlayerSlotId slotId, UnitData unit, out string blockedReason)
    {
        blockedReason = string.Empty;
        if (unit == null || unit.requiredBuilding == null)
            return true;
        if (HasCapturedBuilding(slotId, unit.requiredBuilding))
            return true;

        blockedReason = $"Requer capturar {ResolveProgressionBuildingName(unit.requiredBuilding)} ao menos uma vez.";
        return false;
    }

    public bool CanCaptureConstruction(
        PlayerSlotId slotId,
        ConstructionData construction,
        out string blockedReason)
    {
        blockedReason = string.Empty;
        if (construction == null)
        {
            blockedReason = "ConstructionData indisponivel.";
            return false;
        }
        if (!IsValidPlayerSlot(slotId))
        {
            blockedReason = "Slot de jogador invalido.";
            return false;
        }

        // O pre-requisito deixou de bloquear a captura. Enquanto ele estiver
        // ausente, a forca efetiva e reduzida por
        // ShouldPenalizeCaptureForMissingPrerequisite.
        return true;
    }

    public bool ShouldPenalizeCaptureForMissingPrerequisite(
        PlayerSlotId slotId,
        ConstructionData construction,
        out string prerequisiteName)
    {
        prerequisiteName = string.Empty;
        if (construction == null || construction.requiredBuilding == null)
            return false;
        if (!IsValidPlayerSlot(slotId))
            return false;
        if (ConstructionManager.IsHeadQuarterlessTeam(GetVisualTeamForSlot(slotId)))
            return false;
        if (HasCapturedBuilding(slotId, construction.requiredBuilding))
            return false;

        prerequisiteName = ResolveProgressionBuildingName(construction.requiredBuilding);
        return true;
    }

    private static bool TryResolveSlotHeadQuarterCell(PlayerSlotId slotId, out Vector3Int cell)
    {
        cell = Vector3Int.zero;
        if (!slotId.IsValid)
            return false;

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        ConstructionManager bestHq = null;
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if (construction.SlotIndex != slotId.Value)
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

    private bool DeclareEliminationVictory(PlayerSlotId winnerSlot, PlayerSlotId defeatedSlot, VictoryReason reason)
    {
        if (hasVictoryWinner)
            return true;
        if (!IsValidPlayerSlot(winnerSlot) || winnerSlot == defeatedSlot)
            return false;

        hasVictoryWinner = true;
        victoryWinnerSlotIndex = winnerSlot.Value;
        TeamId winnerTeam = GetVisualTeamForSlot(winnerSlot);
        victoryWinnerTeam = winnerTeam;
        HandleVictoryAestheticPresentation(
            winnerTeam,
            IsValidPlayerSlot(defeatedSlot) ? GetVisualTeamForSlot(defeatedSlot) : TeamId.Neutral,
            reason);
        return true;
    }

    private PlayerSlotId ResolveFirstAliveOpponentSlot(PlayerSlotId defeatedSlot)
    {
        if (players == null)
            return PlayerSlotId.Invalid;
        for (int i = 0; i < players.Count; i++)
            if (i != defeatedSlot.Value && !players[i].defeated)
                return PlayerSlotId.FromIndex(i);
        return PlayerSlotId.Invalid;
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
