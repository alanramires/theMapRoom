using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class TurnStateManager
{
    [Header("Helper - Inspection")]
    [SerializeField] [Range(0.5f, 20f)] private float inspectedHelperDurationSeconds = 4f;
    [Header("Helper - Turn Start Autonomy")]
    [SerializeField] [Range(0.5f, 20f)] private float turnStartAutonomyHelperDurationFallbackSeconds = 6f;

    public enum HelperPanelKind
    {
        None = 0,
        Shopping = 1,
        Sensors = 2,
        Disembark = 3,
        Merge = 4,
        CommandService = 5,
        UnitStats = 6,
        Embark = 7,
        Supply = 8,
        TurnStartAutonomy = 9,
        Transfer = 10,
        ConstructionStats = 11,
        RemovingUnit = 12,
        AimTargets = 13,
        AimConfirm = 14,
        EmbarkConfirm = 15,
        TerrainStats = 16
    }

    public sealed class HelperPanelData
    {
        public HelperPanelKind Kind = HelperPanelKind.None;
        public readonly List<HelperShoppingLine> ShoppingLines = new List<HelperShoppingLine>();
        public readonly List<HelperSensorLine> SensorLines = new List<HelperSensorLine>();
        public readonly List<HelperThreatLayerTeamLine> ThreatLayerTeamLines = new List<HelperThreatLayerTeamLine>();
        public readonly List<HelperDisembarkOrderLine> DisembarkOrderLines = new List<HelperDisembarkOrderLine>();
        public readonly List<HelperDisembarkPassengerLine> DisembarkPassengerLines = new List<HelperDisembarkPassengerLine>();
        public int DisembarkStep;
        public string DisembarkSelectedPassengerName;
        public string DisembarkSelectedLandingLabel;
        public readonly List<HelperMergeQueueLine> MergeQueueLines = new List<HelperMergeQueueLine>();
        public readonly List<HelperMergeCandidateLine> MergeCandidateLines = new List<HelperMergeCandidateLine>();
        public readonly List<HelperEmbarkCandidateLine> EmbarkCandidateLines = new List<HelperEmbarkCandidateLine>();
        public readonly List<HelperSupplyTargetLine> SupplyTargetLines = new List<HelperSupplyTargetLine>();
        public readonly List<HelperSupplyCandidateLine> SupplyCandidateLines = new List<HelperSupplyCandidateLine>();
        public readonly List<HelperSupplyResourceLine> SupplyResourceLines = new List<HelperSupplyResourceLine>();
        public readonly List<HelperTransferCandidateLine> TransferCandidateLines = new List<HelperTransferCandidateLine>();
        public readonly List<HelperTransferResourceLine> TransferResourceLines = new List<HelperTransferResourceLine>();
        public readonly List<HelperCommandServiceTargetLine> CommandServiceTargetLines = new List<HelperCommandServiceTargetLine>();
        public readonly List<HelperCommandServiceSkippedUnitLine> CommandServiceSkippedUnitLines = new List<HelperCommandServiceSkippedUnitLine>();
        public readonly List<HelperTurnStartAutonomyLine> TurnStartAutonomyLines = new List<HelperTurnStartAutonomyLine>();
        public string ShoppingConstructionName;
        public string UnitStatsName;
        public readonly List<string> UnitStatsLines = new List<string>();
        public bool UnitStatsShowKeepPositionAimHint;
        public string UnitStatsLocalLabel;
        public Sprite UnitStatsLocalSprite;
        public Color UnitStatsLocalColor = Color.white;
        public int UnitStatsDefensePoints;
        public string TerrainStatsName;
        public string TerrainStatsDescription;
        public string ConstructionStatsName;
        public readonly List<string> ConstructionStatsLines = new List<string>();
        public string RemovingUnitName;
        public readonly List<HelperAimTargetLine> AimTargetLines = new List<HelperAimTargetLine>();
        public string AimConfirmTargetName;
        public Sprite AimConfirmTargetSprite;
        public Color AimConfirmTargetColor = Color.white;
        public int AimConfirmHp;
        public string AimConfirmTerrainLabel;
        public Sprite AimConfirmLocalSprite;
        public Color AimConfirmLocalColor = Color.white;
        public int SupplyServedTargets;
        public int SupplyRecoveredHp;
        public int SupplyRecoveredFuel;
        public int SupplyRecoveredAmmo;
        public int SupplyTotalCost;
        public bool SupplyIsConfirmStep;
        public bool SupplyHasQueuedOrders;
        public bool TransferIsConfirmStep;
        public bool TransferHasCursorOption;
        public bool TransferCursorOptionFocused;
        public string TransferSelectedLabel;
        public string TransferSourceLabel;
        public string TransferDestinationLabel;
        public bool HasQueuedDisembarkOrders;
        public bool IsMergeConfirmStep;
        public bool HasSelectedMergeCandidate;
        public int SelectedMergeCandidateNumber;
        public string SelectedMergeCandidateName;
        public string SelectedMergeCandidateStats;
        public string MergeConfirmPreview;
        public string MergeQueuePreview;
        public int CommandServiceServedTargets;
        public int CommandServiceRecoveredHp;
        public int CommandServiceRecoveredFuel;
        public int CommandServiceRecoveredAmmo;
        public int CommandServiceTotalCost;
        public bool CommandServiceStoppedByEconomy;
        public bool CommandServiceIsEstimate;
        public int CommandServiceMoneyBefore;
        public int CommandServiceMoneyAfter;
        public bool ThreatLayerSelectionActive;
        public int ThreatLayerInspectedTeamId = int.MinValue;
        public int SubjectTeamId = int.MinValue;
    }

    private float commandServiceHelperVisibleUntil = -1f;
    private int commandServiceHelperServedTargets;
    private int commandServiceHelperRecoveredHp;
    private int commandServiceHelperRecoveredFuel;
    private int commandServiceHelperRecoveredAmmo;
    private int commandServiceHelperTotalCost;
    private bool commandServiceHelperStoppedByEconomy;
    private bool commandServiceHelperIsEstimate;
    private int commandServiceHelperMoneyBefore;
    private int commandServiceHelperMoneyAfter;
    private readonly List<HelperCommandServiceTargetLine> commandServiceHelperTargetLines = new List<HelperCommandServiceTargetLine>();
    private readonly List<HelperCommandServiceSkippedUnitLine> commandServiceHelperSkippedUnitLines = new List<HelperCommandServiceSkippedUnitLine>();
    private UnitManager inspectedHelperUnit;
    private ConstructionManager inspectedHelperConstruction;
    private bool inspectedHelperTerrain;
    private readonly List<Vector3Int> inspectedThreatRangeCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> inspectedThreatRangeLookup = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> inspectedThreatLineCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> inspectedThreatLineLookup = new HashSet<Vector3Int>();
    private bool enemyThreatLayersEnabled;
    private int enemyThreatLayersInspectedTeamId = int.MinValue;
    private readonly List<int> threatLayerSelectableTeamIds = new List<int>();
    private readonly List<int> threatLayerSelectableOptionNumbers = new List<int>();
    private readonly List<Vector3Int> enemyThreatRangeCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> enemyThreatRangeLookup = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> enemyThreatLineCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> enemyThreatLineLookup = new HashSet<Vector3Int>();
    private readonly Dictionary<int, ThreatOverlayCacheEntry> threatOverlayCacheByUnitInstanceId = new Dictionary<int, ThreatOverlayCacheEntry>();
    private readonly Dictionary<int, ThreatOverlayCacheMetrics> threatOverlayCacheMetricsByUnitInstanceId = new Dictionary<int, ThreatOverlayCacheMetrics>();
    private int threatOverlayCacheTotalHits;
    private int threatOverlayCacheTotalMisses;
    private float inspectedHelperVisibleUntil = -1f;
    private int inspectedHelperActivatedFrame = -1;
    private Vector3Int inspectedHelperCursorCell;
    private readonly List<HelperTurnStartAutonomyLine> turnStartAutonomyHelperLines = new List<HelperTurnStartAutonomyLine>();
    private float turnStartAutonomyHelperVisibleUntil = -1f;
    private int turnStartAutonomyHelperActivatedFrame = -1;
    private Vector3Int turnStartAutonomyHelperCursorCell;
    private Vector3Int lastHoveredCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    private float hoveredCellStartTime = -1f;
    private bool hasTriggeredHoverAtCurrentCell = false;

    private void OnEnable()
    {
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        MatchController.OnActiveTeamChanged += HandleAutoCommandServiceTeamChanged;
        MatchController.OnBeforeAdvanceTurn += HandleBeforeAdvanceTurn;
    }

    private void OnDisable()
    {
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        MatchController.OnActiveTeamChanged -= HandleAutoCommandServiceTeamChanged;
        MatchController.OnBeforeAdvanceTurn -= HandleBeforeAdvanceTurn;
    }

    private void HandleActiveTeamChanged(int teamId)
    {
        ResetHoverState();
    }

    private void HandleBeforeAdvanceTurn()
    {
        ClearDebugTempMoveOverridesAtTurnAdvance();
    }

    public void ResetHoverState()
    {
        lastHoveredCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        hoveredCellStartTime = Time.unscaledTime;
        hasTriggeredHoverAtCurrentCell = false;
    }


    public readonly struct TurnStartAutonomyUpkeepEntry
    {
        public readonly string unitName;
        public readonly Vector3Int cell;
        public readonly int autonomyConsumed;
        public readonly int fuelBefore;
        public readonly int fuelAfter;

        public TurnStartAutonomyUpkeepEntry(string unitName, Vector3Int cell, int autonomyConsumed, int fuelBefore, int fuelAfter)
        {
            this.unitName = unitName ?? string.Empty;
            this.cell = cell;
            this.autonomyConsumed = Mathf.Max(0, autonomyConsumed);
            this.fuelBefore = Mathf.Max(0, fuelBefore);
            this.fuelAfter = Mathf.Max(0, fuelAfter);
        }
    }

    public sealed class HelperShoppingLine
    {
        public int index;
        public string unitName;
        public int? cost;
        public bool canAfford = true;
        public bool isFocused;
        public bool isCancel;
    }

    public sealed class HelperSensorLine
    {
        public char actionCode;
        public string sensorKey;
    }

    public sealed class HelperThreatLayerTeamLine
    {
        public int optionNumber;
        public int teamId;
        public string teamName;
        public bool isOwnTeam;
    }

    public sealed class HelperDisembarkOrderLine
    {
        public int index;
        public string unitName;
        public string stats;
        public string terrainName;
        public Sprite unitSprite;
        public Color unitColor = Color.white;
        public Sprite localSprite;
        public Color localColor = Color.white;
    }

    public sealed class HelperDisembarkPassengerLine
    {
        public int index;
        public string unitName;
        public string stats;
    }

    public sealed class HelperMergeQueueLine
    {
        public int index;
        public string unitName;
        public string stats;
        public Sprite unitSprite;
        public Color unitColor = Color.white;
    }

    public sealed class HelperMergeCandidateLine
    {
        public int index;
        public string unitName;
        public string stats;
        public bool isValid;
        public string invalidReason;
        public Sprite unitSprite;
        public Color unitColor = Color.white;
    }

    public sealed class HelperEmbarkCandidateLine
    {
        public int index;
        public string unitName;
        public string stats;
        public bool isValid;
        public string invalidReason;
        public bool isFocused;
    }

    public sealed class HelperSupplyTargetLine
    {
        public int index;
        public string unitName;
        public string gainsLabel;
        public int estimatedCost;
        public bool isFocused;
        public Sprite unitSprite;
        public Color unitColor = Color.white;
    }

    public sealed class HelperSupplyCandidateLine
    {
        public int index;
        public string unitName;
        public string stats;
        public Sprite unitSprite;
        public Color unitColor = Color.white;
        public bool isValid = true;
        public string invalidReason;
    }

    public sealed class HelperSupplyResourceLine
    {
        public string supplyName;
        public int beforeAmount;
        public int afterAmount;
        public int maxAmount;
    }

    public sealed class HelperTransferCandidateLine
    {
        public int index;
        public string unitName;
        public bool isDonate;
        public bool isFocused;
        public Sprite targetSprite;
        public Color targetColor = Color.white;
    }

    public sealed class HelperTransferResourceLine
    {
        public string supplyName;
        public int movedAmount;
        public int sourceBefore;
        public int sourceAfter;
        public int destinationBefore;
        public int destinationAfter;
        public bool sourceIsInfinite;
        public bool destinationIsInfinite;
    }

    private sealed class SupplyEstimateLine
    {
        public UnitManager target;
        public int hp;
        public int fuel;
        public int ammo;
        public int cost;
        public bool isFocused;
    }

    public sealed class HelperCommandServiceTargetLine
    {
        public string unitName;
        public string sourceLabel;
        public string gainsLabel;
        public bool isFocused;
        public bool isFullyAffordable = true;
    }

    public sealed class HelperCommandServiceSkippedUnitLine
    {
        public string unitName;
        public string sourceLabel;
        public bool isFocused;
    }

    private readonly struct ThreatOverlayCacheKey
    {
        public readonly int unitSnapshotHash;
        public readonly int globalBoardRevision;
        public readonly int teamObserverRevision;
        public readonly int matchFlagsHash;

        public ThreatOverlayCacheKey(int unitSnapshotHash, int globalBoardRevision, int teamObserverRevision, int matchFlagsHash)
        {
            this.unitSnapshotHash = unitSnapshotHash;
            this.globalBoardRevision = globalBoardRevision;
            this.teamObserverRevision = teamObserverRevision;
            this.matchFlagsHash = matchFlagsHash;
        }

        public bool Equals(ThreatOverlayCacheKey other)
        {
            return unitSnapshotHash == other.unitSnapshotHash &&
                   globalBoardRevision == other.globalBoardRevision &&
                   teamObserverRevision == other.teamObserverRevision &&
                   matchFlagsHash == other.matchFlagsHash;
        }
    }

    private sealed class ThreatOverlayCacheEntry
    {
        public ThreatOverlayCacheKey key;
        public readonly List<Vector3Int> rangeCells = new List<Vector3Int>();
        public readonly List<Vector3Int> lineCells = new List<Vector3Int>();
    }

    private sealed class ThreatOverlayCacheMetrics
    {
        public int hits;
        public int misses;
    }

    public sealed class HelperTurnStartAutonomyLine
    {
        public string unitName;
        public int autonomyConsumed;
        public int fuelBefore;
        public int fuelAfter;
        public Vector3Int cell;
    }

    public bool TryBuildHelperPanelData(out HelperPanelData data)
    {
        data = new HelperPanelData();

        if (TryBuildCommandServiceHelperPanelData(data))
            return true;

        if (TryBuildTurnStartAutonomyHelperPanelData(data))
            return true;

        if (TryBuildUnitStatsHelperPanelData(data))
            return true;
        if (TryBuildConstructionStatsHelperPanelData(data))
            return true;
        if (TryBuildTerrainStatsHelperPanelData(data))
            return true;

        if (scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect)
            return TryBuildSensorsHelperPanelData(data);

        if (CurrentCursorState == CursorState.Neutral)
            return false;

        if (CurrentCursorState == CursorState.ShoppingAndServices)
            return TryBuildShoppingHelperPanelData(data);

        if (CurrentCursorState == CursorState.RemovingUnit)
            return TryBuildRemovingUnitHelperPanelData(data);

        if (CurrentCursorState == CursorState.Mirando)
            return TryBuildAimTargetsHelperPanelData(data);

        if (CurrentCursorState == CursorState.Desembarcando)
            return TryBuildDisembarkHelperPanelData(data);

        if (CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado)
        {
            if (IsTransferPromptActive())
                return TryBuildTransferHelperPanelData(data);
            return TryBuildSensorsHelperPanelData(data);
        }

        if (CurrentCursorState == CursorState.Suprindo)
            return TryBuildSupplyHelperPanelData(data);

        if (CurrentCursorState == CursorState.Embarcando)
            return TryBuildEmbarkHelperPanelData(data);

        if (CurrentCursorState == CursorState.Fundindo)
            return TryBuildMergeHelperPanelData(data);

        return false;
    }

    public sealed class HelperAimTargetLine
    {
        public int index;
        public string unitName;
        public bool isValid;
        public bool isFocused;
        public bool isCancel;
        public int hp;
        public string terrainLabel;
        public Sprite unitSprite;
        public Color unitColor = Color.white;
    }

    private bool TryBuildAimTargetsHelperPanelData(HelperPanelData data)
    {
        if (data == null || cachedMirandoSelectionEntries.Count <= 0)
            return false;
        if (scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget)
        {
            data.Kind = HelperPanelKind.AimConfirm;
            if (scannerSelectedTargetIndex >= 0 && scannerSelectedTargetIndex < cachedMirandoSelectionEntries.Count)
            {
                UnitManager target = cachedMirandoSelectionEntries[scannerSelectedTargetIndex].TargetUnit;
                data.AimConfirmTargetName = target != null ? ResolveDebugUnitName(target) : "Alvo";
                if (target != null)
                {
                    Vector3Int targetCell = target.CurrentCellPosition;
                    SpriteRenderer targetRenderer = target.GetMainSpriteRenderer();
                    data.AimConfirmTargetSprite = targetRenderer != null ? targetRenderer.sprite : null;
                    data.AimConfirmTargetColor = targetRenderer != null ? targetRenderer.color : Color.white;
                    data.AimConfirmHp = Mathf.Max(0, target.CurrentHP);
                    data.AimConfirmTerrainLabel = ResolveCellTerrainLabel(targetCell);
                    ResolveCellLocalVisual(targetCell,
                        out data.AimConfirmLocalSprite,
                        out data.AimConfirmLocalColor);
                }
            }
            return true;
        }
        data.Kind = HelperPanelKind.AimTargets;
        for (int i = 0; i < cachedMirandoSelectionEntries.Count; i++)
        {
            MirandoSelectionEntry entry = cachedMirandoSelectionEntries[i];
            UnitManager target = entry.TargetUnit;
            SpriteRenderer renderer = target != null ? target.GetMainSpriteRenderer() : null;
            data.AimTargetLines.Add(new HelperAimTargetLine
            {
                index = i,
                unitName = target != null ? ResolveDebugUnitName(target) : "Alvo invalido",
                isValid = entry.isValid,
                isFocused = !mirandoCancelFocused && scannerSelectedTargetIndex == i,
                hp = target != null ? Mathf.Max(0, target.CurrentHP) : 0,
                terrainLabel = target != null ? ResolveCellTerrainLabel(target.CurrentCellPosition) : string.Empty,
                unitSprite = renderer != null ? renderer.sprite : null,
                unitColor = renderer != null ? renderer.color : Color.white
            });
        }

        // Slot CANCELAR ao final da lista (igual ao shopping): da pra chegar nele com as setas e sair
        // do modo de ataque sem mouse/Esc. O cancel de rodape fica escondido no passo de escolher alvo.
        data.AimTargetLines.Add(new HelperAimTargetLine
        {
            index = -1,
            unitName = "CANCELAR",
            isValid = true,
            isFocused = mirandoCancelFocused,
            isCancel = true
        });
        return true;
    }

    private RoadNetworkManager[] cachedRoadNetworks;

    // Descreve o hex do alvo: construcao (Cidade) tem prioridade; senao estrutura sobre terreno
    // (Estrada na Floresta); senao so o terreno (Floresta). Tudo resolvido dos mesmos bancos/tilemap.
    private string ResolveCellTerrainLabel(Vector3Int cell)
    {
        cell.z = 0;
        Tilemap board = terrainTilemap;
        if (board == null)
            return string.Empty;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(board, cell);
        if (construction != null && !string.IsNullOrWhiteSpace(construction.ConstructionDisplayName))
            return construction.ConstructionDisplayName;

        string terrainName = null;
        if (terrainDatabase != null &&
            terrainDatabase.TryGetByPaletteTile(board.GetTile(cell), out TerrainTypeData terrain) && terrain != null)
            terrainName = terrain.displayName;

        StructureData structure = ResolveStructureAtCell(cell);
        if (structure != null && !string.IsNullOrWhiteSpace(structure.displayName))
            return string.IsNullOrWhiteSpace(terrainName)
                ? structure.displayName
                : $"{structure.displayName} na {terrainName}";

        return terrainName ?? string.Empty;
    }

    // Visual do LOCAL: a construcao ocupa o hex visualmente e tem prioridade sobre o terreno.
    // O sprite do tilemap fica como fallback quando nao existe ConstructionManager na celula.
    private void ResolveCellLocalVisual(Vector3Int cell, out Sprite sprite, out Color color)
    {
        cell.z = 0;
        sprite = null;
        color = Color.white;

        Tilemap board = terrainTilemap;
        if (board == null)
            return;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(board, cell);
        SpriteRenderer constructionRenderer = construction != null
            ? construction.GetMainSpriteRenderer()
            : null;
        if (constructionRenderer != null && constructionRenderer.sprite != null)
        {
            sprite = constructionRenderer.sprite;
            color = constructionRenderer.color;
            return;
        }

        sprite = board.GetSprite(cell);
    }

    private StructureData ResolveStructureAtCell(Vector3Int cell)
    {
        if (cachedRoadNetworks == null)
            cachedRoadNetworks = FindObjectsByType<RoadNetworkManager>(FindObjectsSortMode.None);

        for (int i = 0; i < cachedRoadNetworks.Length; i++)
        {
            RoadNetworkManager road = cachedRoadNetworks[i];
            if (road != null && road.TryGetStructureAtCell(cell, out StructureData structure) && structure != null)
                return structure;
        }
        return null;
    }

    private bool TryBuildRemovingUnitHelperPanelData(HelperPanelData data)
    {
        if (data == null || cursorController == null)
            return false;

        UnitManager target = FindUnitAtCell(cursorController.CurrentCell);
        if (target == null)
            return false;

        data.Kind = HelperPanelKind.RemovingUnit;
        data.RemovingUnitName = ResolveDebugUnitName(target);
        data.SubjectTeamId = (int)target.TeamId;
        return true;
    }

    public void ShowTurnStartAutonomyUpkeepHelper(IReadOnlyList<TurnStartAutonomyUpkeepEntry> entries)
    {
        turnStartAutonomyHelperLines.Clear();
        if (entries == null || entries.Count <= 0)
        {
            ClearTurnStartAutonomyHelper();
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            TurnStartAutonomyUpkeepEntry entry = entries[i];
            if (entry.autonomyConsumed <= 0)
                continue;

            turnStartAutonomyHelperLines.Add(new HelperTurnStartAutonomyLine
            {
                unitName = entry.unitName ?? string.Empty,
                autonomyConsumed = Mathf.Max(0, entry.autonomyConsumed),
                fuelBefore = Mathf.Max(0, entry.fuelBefore),
                fuelAfter = Mathf.Max(0, entry.fuelAfter),
                cell = entry.cell
            });
        }

        if (turnStartAutonomyHelperLines.Count <= 0)
        {
            ClearTurnStartAutonomyHelper();
            return;
        }

        float helperDuration = animationManager != null
            ? animationManager.TurnStartAutonomyHelperTextDuration
            : Mathf.Max(0.5f, turnStartAutonomyHelperDurationFallbackSeconds);
        turnStartAutonomyHelperVisibleUntil = Time.time + Mathf.Max(0.1f, helperDuration);
        turnStartAutonomyHelperActivatedFrame = Time.frameCount;
        turnStartAutonomyHelperCursorCell = cursorController != null ? cursorController.CurrentCell : default;
    }

    private bool TryBuildTurnStartAutonomyHelperPanelData(HelperPanelData data)
    {
        if (data == null || !IsTurnStartAutonomyHelperActive())
            return false;

        data.Kind = HelperPanelKind.TurnStartAutonomy;
        for (int i = 0; i < turnStartAutonomyHelperLines.Count; i++)
            data.TurnStartAutonomyLines.Add(turnStartAutonomyHelperLines[i]);

        return data.TurnStartAutonomyLines.Count > 0;
    }

    private bool IsTurnStartAutonomyHelperActive()
    {
        return turnStartAutonomyHelperLines.Count > 0 &&
               turnStartAutonomyHelperVisibleUntil > 0f &&
               Time.time <= turnStartAutonomyHelperVisibleUntil;
    }

    private void ClearTurnStartAutonomyHelper()
    {
        turnStartAutonomyHelperLines.Clear();
        turnStartAutonomyHelperVisibleUntil = -1f;
        turnStartAutonomyHelperActivatedFrame = -1;
        turnStartAutonomyHelperCursorCell = default;
    }

    private bool TryBuildUnitStatsHelperPanelData(HelperPanelData data)
    {
        if (data == null)
            return false;

        UnitManager unit = null;
        if (CurrentCursorState == CursorState.UnitSelected && selectedUnit != null)
        {
            unit = selectedUnit;
        }
        else if ((CurrentCursorState == CursorState.Neutral || IsInspectingState()) && IsInspectedHelperActive())
        {
            unit = inspectedHelperUnit;
        }

        if (unit == null)
            return false;

        data.Kind = HelperPanelKind.UnitStats;
        data.UnitStatsName = ResolveUnitRuntimeName(unit);
        data.SubjectTeamId = (int)unit.TeamId;
        data.UnitStatsShowKeepPositionAimHint =
            CurrentCursorState == CursorState.UnitSelected &&
            HasEmbarkedLongRangeWeapon(unit);

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        ResolveUnitActiveLocalVisual(unit, unitCell,
            out data.UnitStatsLocalLabel,
            out data.UnitStatsLocalSprite,
            out data.UnitStatsLocalColor);
        data.UnitStatsDefensePoints = Mathf.Max(0, ResolveDpqAtUnitPosition(unit, null).points);

        int hpCurrent = Mathf.Max(0, unit.CurrentHP);
        int hpMax = Mathf.Max(1, unit.GetMaxHP());
        int movement = 0;
        if (unit.TryGetUnitData(out UnitData selectedData) && selectedData != null)
            movement = Mathf.Max(0, selectedData.movement);
        else
            movement = Mathf.Max(0, unit.MaxMovementPoints);
        int fuelCurrent = Mathf.Max(0, unit.CurrentFuel);
        int fuelMax = Mathf.Max(1, unit.GetMaxFuel());

        // 1. Basic Stats
        data.UnitStatsLines.Add($"HP: {hpCurrent}/{hpMax}");
        data.UnitStatsLines.Add($"MOV: {movement}");
        data.UnitStatsLines.Add($"AUT: {fuelCurrent}/{fuelMax}");

        // 2. Weapons
        AppendUnitWeaponsDetailedLines(data.UnitStatsLines, unit);

        // 3. Transport
        IReadOnlyList<UnitTransportSeatRuntime> seats = unit.TransportedUnitSlots;
        bool hasPassengers = false;
        if (seats != null)
        {
            for (int i = 0; i < seats.Count; i++)
            {
                UnitManager passenger = seats[i] != null ? seats[i].embarkedUnit : null;
                if (passenger != null && passenger.IsEmbarked && passenger.EmbarkedTransporter == unit)
                {
                    hasPassengers = true;
                    break;
                }
            }
        }

        if (hasPassengers)
        {
            data.UnitStatsLines.Add(string.Empty);
            data.UnitStatsLines.Add("SECTION:Transporting");
            AppendTransportedUnitStatsLines(data.UnitStatsLines, unit, depth: 0);
        }

        // 4. Services
        AppendUnitServicesDetailedLines(data.UnitStatsLines, unit);

        // 5. Supplies
        AppendUnitSuppliesDetailedLines(data.UnitStatsLines, unit);

        // 6. Vision (somente em partidas com nevoa de guerra)
        if (matchController != null && matchController.EnableTotalWar)
            AppendUnitVisionDetailedLines(data.UnitStatsLines, unit);

        return data.UnitStatsLines.Count > 0;
    }

    private void ResolveUnitActiveLocalVisual(
        UnitManager unit,
        Vector3Int cell,
        out string label,
        out Sprite sprite,
        out Color color)
    {
        label = string.Empty;
        sprite = null;
        color = Color.white;

        if (unit != null && dpqAirHeightConfig != null)
        {
            Domain domain = unit.GetDomain();
            HeightLevel height = unit.GetHeightLevel();
            TileBase layerTile = null;

            if (domain == Domain.Air && height == HeightLevel.AirLow)
            {
                label = dpqAirHeightConfig.airLowDisplayName;
                layerTile = dpqAirHeightConfig.airLowTile;
            }
            else if (domain == Domain.Air && height == HeightLevel.AirHigh)
            {
                label = dpqAirHeightConfig.airHighDisplayName;
                layerTile = dpqAirHeightConfig.airHighTile;
            }
            else if (domain == Domain.Submarine && height == HeightLevel.Submerged)
            {
                label = dpqAirHeightConfig.subDisplayName;
                layerTile = dpqAirHeightConfig.subTile;
            }

            if (layerTile != null)
            {
                if (layerTile is Tile tile)
                {
                    sprite = tile.sprite;
                    color = tile.color;
                }
                if (string.IsNullOrWhiteSpace(label))
                    label = layerTile.name;
                return;
            }
        }

        label = ResolveCellTerrainLabel(cell);
        ResolveCellLocalVisual(cell, out sprite, out color);
    }

    private static void AppendUnitVisionDetailedLines(List<string> lines, UnitManager unit)
    {
        if (lines == null || unit == null || !unit.TryGetUnitData(out UnitData unitData) || unitData == null)
            return;

        lines.Add(string.Empty);
        lines.Add("SECTION:Vision");
        lines.Add($"Alcance: {Mathf.Max(1, unitData.visao)}");

        IReadOnlyList<UnitVisionException> specializations = unitData.visionSpecializations;
        if (specializations == null)
            return;

        for (int i = 0; i < specializations.Count; i++)
        {
            UnitVisionException entry = specializations[i];
            if (entry == null)
                continue;

            string layer = ResolveVisionLayerLabel(entry);
            string detection = ResolveVisionDetectionSkillsLabel(entry.detectUnitsWithFollowingSkills);
            lines.Add(string.IsNullOrWhiteSpace(detection)
                ? $"- {layer}: {Mathf.Max(0, entry.vision)}"
                : $"- {layer}: {Mathf.Max(0, entry.vision)} | Detecta: {detection}");
        }
    }

    private static string ResolveVisionLayerLabel(UnitVisionException entry)
    {
        if (entry == null)
            return "Especial";

        switch (entry.domain)
        {
            case Domain.Land: return entry.allHeights ? "Terrestre" : "Terrestre/Superficie";
            case Domain.Naval: return entry.allHeights ? "Naval" : "Naval/Superficie";
            case Domain.Submarine: return entry.allHeights ? "Submarino" : "Submarino/Submerso";
            case Domain.Air:
                if (entry.allHeights) return "Aereo";
                return entry.heightLevel == HeightLevel.AirHigh ? "Aereo/Alto" : "Aereo/Baixo";
            default: return entry.domain.ToString();
        }
    }

    private static string ResolveVisionDetectionSkillsLabel(IReadOnlyList<SkillData> skills)
    {
        if (skills == null || skills.Count == 0)
            return string.Empty;

        List<string> names = new List<string>();
        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];
            if (skill == null)
                continue;
            string name = !string.IsNullOrWhiteSpace(skill.displayName)
                ? skill.displayName
                : (!string.IsNullOrWhiteSpace(skill.id) ? skill.id : skill.name);
            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                names.Add(name);
        }
        return string.Join(", ", names);
    }

    private static bool HasEmbarkedLongRangeWeapon(UnitManager unit)
    {
        if (unit == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null)
            return false;

        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;

            int min = embarked.GetRangeMin();
            int max = embarked.GetRangeMax();
            if ((min == 1 && max > 1) || min > 1)
                return true;
        }

        return false;
    }

    private void AppendUnitWeaponsDetailedLines(List<string> lines, UnitManager unit)
    {
        if (lines == null || unit == null)
            return;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count <= 0)
            return;

        bool hasAnyWeapon = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null && weapons[i].weapon != null)
            {
                hasAnyWeapon = true;
                break;
            }
        }

        if (!hasAnyWeapon)
            return;

        lines.Add(string.Empty);
        lines.Add("SECTION:Weapons");
        int weaponCounter = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;

            weaponCounter++;
            string weaponName = !string.IsNullOrWhiteSpace(embarked.weapon.displayName) ? embarked.weapon.displayName : embarked.weapon.name;
            int ammo = Mathf.Max(0, embarked.squadAmmunition);
            int min = embarked.GetRangeMin();
            int max = embarked.GetRangeMax();
            string range = min == max ? min.ToString() : $"{min} ~ {max}";
            lines.Add($"{weaponCounter}: {weaponName} ({ammo}) R:{range}");
        }
    }

    private void AppendUnitServicesDetailedLines(List<string> lines, UnitManager unit)
    {
        if (lines == null || unit == null)
            return;

        IReadOnlyList<ServiceData> services = unit.GetEmbarkedServices();
        if (services == null || services.Count <= 0)
            return;

        bool hasAnyService = false;
        for (int i = 0; i < services.Count; i++)
        {
            if (services[i] != null && services[i].isService)
            {
                hasAnyService = true;
                break;
            }
        }

        if (!hasAnyService)
            return;

        lines.Add(string.Empty);
        lines.Add("SECTION:Services");
        int serviceCounter = 0;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;

            serviceCounter++;
            string serviceName = !string.IsNullOrWhiteSpace(service.displayName) ? service.displayName : service.name;
            lines.Add($"{serviceCounter}: {serviceName}");
        }
    }

    private void AppendUnitSuppliesDetailedLines(List<string> lines, UnitManager unit)
    {
        if (lines == null || unit == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null || resources.Count <= 0)
            return;

        lines.Add(string.Empty);
        lines.Add("SECTION:Supplies");
        int supplyCounter = 0;
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply runtime = resources[i];
            if (runtime == null || runtime.supply == null)
                continue;

            supplyCounter++;
            string supplyName = ResolveSupplyDisplayName(runtime.supply);
            int current = Mathf.Max(0, runtime.amount);
            int max = ResolveSupplierResourceMaxAmount(unit, runtime.supply, current);
            lines.Add($"{supplyCounter}: {supplyName} ({current} / {max})");
        }
    }

    private bool TryBuildConstructionStatsHelperPanelData(HelperPanelData data)
    {
        bool canRenderInspectConstruction =
            CurrentCursorState == CursorState.Neutral ||
            CurrentCursorState == CursorState.InspectingBuilding;
        if (data == null || !canRenderInspectConstruction || !IsInspectedHelperActive() || inspectedHelperConstruction == null)
            return false;

        ConstructionManager construction = inspectedHelperConstruction;
        string constructionName = !string.IsNullOrWhiteSpace(construction.ConstructionDisplayName)
            ? construction.ConstructionDisplayName
            : (!string.IsNullOrWhiteSpace(construction.ConstructionId) ? construction.ConstructionId : construction.name);

        data.Kind = HelperPanelKind.ConstructionStats;
        data.SubjectTeamId = (int)construction.TeamId;
        data.ConstructionStatsName = constructionName;
        Vector3Int constructionCell = construction.CurrentCellPosition;
        constructionCell.z = 0;
        data.UnitStatsLocalLabel = ResolveCellTerrainLabel(constructionCell);
        ResolveCellLocalVisual(constructionCell, out data.UnitStatsLocalSprite, out data.UnitStatsLocalColor);
        data.UnitStatsDefensePoints = ResolveCellDefensePoints(constructionCell);
        data.ConstructionStatsLines.Add($"Dono Atual: {TeamUtils.GetName(construction.TeamId)} ({(int)construction.TeamId})");
        data.ConstructionStatsLines.Add($"Capture: {construction.CurrentCapturePoints}/{construction.CapturePointsMax}");

        IReadOnlyList<ConstructionSupplyOffer> offers = construction.OfferedSupplies;
        data.ConstructionStatsLines.Add(string.Empty);
        data.ConstructionStatsLines.Add("Estoques");
        if (offers == null || offers.Count <= 0)
        {
            data.ConstructionStatsLines.Add("- nenhum");
        }
        else
        {
            bool addedAny = false;
            for (int i = 0; i < offers.Count; i++)
            {
                ConstructionSupplyOffer offer = offers[i];
                if (offer == null || offer.supply == null)
                    continue;

                string name = ResolveSupplyDisplayName(offer.supply);
                string amount = construction.HasInfiniteSuppliesFor(offer.supply) ? "INF" : Mathf.Max(0, offer.quantity).ToString();
                data.ConstructionStatsLines.Add($"- {name}: {amount}");
                addedAny = true;
            }

            if (!addedAny)
                data.ConstructionStatsLines.Add("- nenhum");
        }

        IReadOnlyList<ServiceData> services = construction.OfferedServices;
        data.ConstructionStatsLines.Add(string.Empty);
        data.ConstructionStatsLines.Add("Servicos");
        if (services == null || services.Count <= 0)
        {
            data.ConstructionStatsLines.Add("- nenhum");
        }
        else
        {
            bool addedAnyService = false;
            for (int i = 0; i < services.Count; i++)
            {
                ServiceData service = services[i];
                if (service == null)
                    continue;

                string label;
                if (service.serviceType == ServiceType.Transfer)
                    label = ResolveConstructionTransferRoleLabelForHelper(construction);
                else
                    label = !string.IsNullOrWhiteSpace(service.displayName) ? service.displayName : (!string.IsNullOrWhiteSpace(service.id) ? service.id : service.name);

                if (string.IsNullOrWhiteSpace(label))
                    continue;

                data.ConstructionStatsLines.Add($"- {label}");
                addedAnyService = true;
            }

            if (!addedAnyService)
                data.ConstructionStatsLines.Add("- nenhum");
        }

        return data.ConstructionStatsLines.Count > 0;
    }

    private bool IsInspectedHelperActive()
    {
        return (inspectedHelperUnit != null || inspectedHelperConstruction != null || inspectedHelperTerrain) &&
            inspectedHelperVisibleUntil > 0f &&
            Time.time <= inspectedHelperVisibleUntil;
    }

    private bool TryBuildTerrainStatsHelperPanelData(HelperPanelData data)
    {
        if (data == null || !inspectedHelperTerrain || !IsInspectedHelperActive())
            return false;

        Vector3Int cell = inspectedHelperCursorCell;
        cell.z = 0;
        data.Kind = HelperPanelKind.TerrainStats;
        data.UnitStatsLocalLabel = ResolveCellTerrainLabel(cell);
        data.TerrainStatsName = string.IsNullOrWhiteSpace(data.UnitStatsLocalLabel)
            ? "LOCAL"
            : data.UnitStatsLocalLabel;
        ResolveCellLocalVisual(cell, out data.UnitStatsLocalSprite, out data.UnitStatsLocalColor);
        data.UnitStatsDefensePoints = ResolveCellDefensePoints(cell);
        data.TerrainStatsDescription = ResolveCellLocationDescription(cell);
        return !string.IsNullOrWhiteSpace(data.UnitStatsLocalLabel) || data.UnitStatsLocalSprite != null;
    }

    private int ResolveCellDefensePoints(Vector3Int cell)
    {
        cell.z = 0;
        Tilemap board = terrainTilemap;
        if (board == null)
            return 0;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(board, cell);
        if (construction != null && TryGetConstructionDpq(construction, out DPQData constructionDpq))
            return Mathf.Max(0, constructionDpq.Pontos);

        StructureData structure = ResolveStructureAtCell(cell);
        if (structure != null && structure.dpqData != null)
            return Mathf.Max(0, structure.dpqData.Pontos);

        if (terrainDatabase != null &&
            terrainDatabase.TryGetByPaletteTile(board.GetTile(cell), out TerrainTypeData terrain) &&
            terrain != null && terrain.dpqData != null)
            return Mathf.Max(0, terrain.dpqData.Pontos);

        return 0;
    }

    private string ResolveCellLocationDescription(Vector3Int cell)
    {
        cell.z = 0;
        Tilemap board = terrainTilemap;
        if (board == null)
            return string.Empty;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(board, cell);
        if (construction != null && construction.TryResolveConstructionData(out ConstructionData constructionData) && constructionData != null)
            return constructionData.description ?? string.Empty;

        StructureData structure = ResolveStructureAtCell(cell);
        if (structure != null && !string.IsNullOrWhiteSpace(structure.description))
            return structure.description;

        if (terrainDatabase != null &&
            terrainDatabase.TryGetByPaletteTile(board.GetTile(cell), out TerrainTypeData terrain) && terrain != null)
            return terrain.description ?? string.Empty;

        return string.Empty;
    }

    private void BeginInspectedTerrainHelper(Vector3Int cell)
    {
        if (cursorController == null)
            return;

        cell.z = 0;
        inspectedHelperUnit = null;
        inspectedHelperConstruction = null;
        inspectedHelperTerrain = true;
        ClearEnemyThreatLayersOverlay();
        ClearInspectedThreatOverlay();
        inspectedHelperVisibleUntil = Time.time + Mathf.Max(0.1f, inspectedHelperDurationSeconds);
        inspectedHelperActivatedFrame = Time.frameCount;
        inspectedHelperCursorCell = cell;
    }

    private void BeginInspectedHelper(UnitManager unit, bool paintThreatOverlay = true, bool triggerEvents = true)
    {
        if (unit == null || cursorController == null)
            return;

        inspectedHelperUnit = unit;
        inspectedHelperConstruction = null;
        inspectedHelperTerrain = false;
        ClearEnemyThreatLayersOverlay();
        if (paintThreatOverlay)
            ApplyInspectedThreatOverlay(unit);
        else
            ClearInspectedThreatOverlay();
        inspectedHelperVisibleUntil = Time.time + Mathf.Max(0.1f, GetInspectUnitHelperDurationSeconds());
        inspectedHelperActivatedFrame = Time.frameCount;
        inspectedHelperCursorCell = cursorController.CurrentCell;
        
        if (triggerEvents)
            OnUnitInspected?.Invoke(unit);
    }

    private void BeginInspectedConstructionHelper(ConstructionManager construction, bool triggerEvents = true)
    {
        if (construction == null || cursorController == null)
            return;

        inspectedHelperConstruction = construction;
        inspectedHelperUnit = null;
        inspectedHelperTerrain = false;
        ClearEnemyThreatLayersOverlay();
        ClearInspectedThreatOverlay();
        inspectedHelperVisibleUntil = Time.time + Mathf.Max(0.1f, GetInspectConstructionHelperDurationSeconds());
        inspectedHelperActivatedFrame = Time.frameCount;
        inspectedHelperCursorCell = cursorController.CurrentCell;
        
        if (triggerEvents)
            OnConstructionInspected?.Invoke(construction);
    }

    private float GetInspectUnitHelperDurationSeconds()
    {
        if (animationManager != null)
            return animationManager.InspectUnitDisplayDuration;
        return Mathf.Max(0.1f, inspectedHelperDurationSeconds);
    }

    private float GetInspectConstructionHelperDurationSeconds()
    {
        if (animationManager != null)
            return animationManager.InspectConstructionDisplayDuration;
        return Mathf.Max(0.1f, inspectedHelperDurationSeconds);
    }

    private void ClearInspectedHelper()
    {
        inspectedHelperUnit = null;
        inspectedHelperConstruction = null;
        inspectedHelperTerrain = false;
        ClearInspectedThreatOverlay();
        inspectedHelperVisibleUntil = -1f;
        inspectedHelperActivatedFrame = -1;
        inspectedHelperCursorCell = default;
    }

    private void ExitInspectStateToNeutral()
    {
        ClearInspectedHelper();
        if (CurrentCursorState == CursorState.InspectingUnit ||
            CurrentCursorState == CursorState.InspectingBuilding ||
            CurrentCursorState == CursorState.InspectingHotZone)
            Retreat("ExitInspectStateToNeutral");
    }

    private void ApplyInspectedThreatOverlay(UnitManager unit)
    {
        ClearInspectedThreatOverlay();
        ClearLineOfFireArea();
        PaintThreatOverlayForUnit(unit, inspectedThreatRangeCells, inspectedThreatRangeLookup, inspectedThreatLineCells, inspectedThreatLineLookup);
    }

    private bool EnterThreatLayerTeamSelection()
    {
        if (matchController != null && matchController.EnableTotalWar)
            return false;

        if (!BuildThreatLayerSelectableTeams(threatLayerSelectableTeamIds))
            return false;

        threatLayerSelectableOptionNumbers.Clear();
        int activeTeam = matchController != null ? matchController.ActiveTeamId : int.MinValue;
        int activeIndex = threatLayerSelectableTeamIds.IndexOf(activeTeam);
        if (activeIndex > 0)
        {
            threatLayerSelectableTeamIds.RemoveAt(activeIndex);
            threatLayerSelectableTeamIds.Insert(0, activeTeam);
        }

        for (int i = 0; i < threatLayerSelectableTeamIds.Count; i++)
        {
            threatLayerSelectableOptionNumbers.Add(i + 1);
        }

        bool inspectedTeamInvalid = enemyThreatLayersInspectedTeamId == int.MinValue ||
                                    !threatLayerSelectableTeamIds.Contains(enemyThreatLayersInspectedTeamId);
        bool inspectedTeamIsActive = enemyThreatLayersInspectedTeamId == activeTeam;
        if (inspectedTeamInvalid || inspectedTeamIsActive)
        {
            enemyThreatLayersInspectedTeamId = ResolveDefaultThreatLayerTeamId(activeTeam);
        }

        ApplyEnemyThreatLayersOverlayForTeam(enemyThreatLayersInspectedTeamId);
        enemyThreatLayersEnabled = enemyThreatLineCells.Count > 0 || enemyThreatRangeCells.Count > 0;
        return true;
    }

    public bool TryKeepSelectedUnitPositionFromHelper()
    {
        if (CurrentCursorState != CursorState.UnitSelected || selectedUnit == null || cursorController == null)
            return false;

        Vector3Int unitCell = selectedUnit.CurrentCellPosition;
        unitCell.z = 0;
        if (!cursorController.SetCell(unitCell, playMoveSfx: false))
            return false;

        HandleConfirmWithFeedback();
        return CurrentCursorState == CursorState.MoveuParado;
    }

    private int ResolveDefaultThreatLayerTeamId(int activeTeam)
    {
        int selectedTeamId = int.MinValue;
        int bestOption = int.MaxValue;
        for (int i = 0; i < threatLayerSelectableTeamIds.Count && i < threatLayerSelectableOptionNumbers.Count; i++)
        {
            int teamId = threatLayerSelectableTeamIds[i];
            if (teamId == activeTeam)
                continue;

            int optionNumber = threatLayerSelectableOptionNumbers[i];
            if (optionNumber < bestOption)
            {
                bestOption = optionNumber;
                selectedTeamId = teamId;
            }
        }

        if (selectedTeamId != int.MinValue)
            return selectedTeamId;

        // Fallback: se so existir o proprio time.
        return threatLayerSelectableTeamIds.Count > 0 ? threatLayerSelectableTeamIds[0] : int.MinValue;
    }

    private bool TryApplyThreatLayerSelection(int optionNumber, out int selectedTeamId)
    {
        selectedTeamId = int.MinValue;
        int index = optionNumber - 1;
        int teamId = (index >= 0 && index < threatLayerSelectableTeamIds.Count)
            ? threatLayerSelectableTeamIds[index]
            : int.MinValue;

        if (teamId == int.MinValue)
            return false;

        selectedTeamId = teamId;
        enemyThreatLayersInspectedTeamId = teamId;
        ApplyEnemyThreatLayersOverlayForTeam(teamId);
        enemyThreatLayersEnabled = enemyThreatLineCells.Count > 0 || enemyThreatRangeCells.Count > 0;
        return true;
    }

    public bool TryCloseThreatLayerHotzone()
    {
        bool hadActiveSelection = scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect;
        bool hadOverlay = enemyThreatLayersEnabled || enemyThreatLineCells.Count > 0 || enemyThreatRangeCells.Count > 0;
        if (!hadActiveSelection && !hadOverlay)
            return false;

        ClearEnemyThreatLayersOverlay();
        if (hadActiveSelection)
            scannerPromptStep = ScannerPromptStep.AwaitingAction;
        if (CurrentCursorState == CursorState.InspectingHotZone)
            Retreat("TryCloseThreatLayerHotzone");
        return true;
    }

    public void ClearThreatLayerHotzoneCache()
    {
        ClearEnemyThreatLayersOverlay();
        threatOverlayCacheByUnitInstanceId.Clear();
        threatOverlayCacheMetricsByUnitInstanceId.Clear();
        threatOverlayCacheTotalHits = 0;
        threatOverlayCacheTotalMisses = 0;
        threatLayerSelectableTeamIds.Clear();
        threatLayerSelectableOptionNumbers.Clear();
        enemyThreatLayersInspectedTeamId = int.MinValue;
    }

    private void RefreshEnemyThreatLayersOverlayIfEnabled()
    {
        if (matchController != null && matchController.EnableTotalWar)
        {
            if (enemyThreatLayersEnabled || enemyThreatLineCells.Count > 0 || enemyThreatRangeCells.Count > 0)
                ClearEnemyThreatLayersOverlay();
            return;
        }

        if (!enemyThreatLayersEnabled || enemyThreatLayersInspectedTeamId == int.MinValue)
            return;

        ApplyEnemyThreatLayersOverlayForTeam(enemyThreatLayersInspectedTeamId);
        enemyThreatLayersEnabled = enemyThreatLineCells.Count > 0 || enemyThreatRangeCells.Count > 0;
    }

    private static bool BuildThreatLayerSelectableTeams(List<int> output)
    {
        if (output == null)
            return false;

        output.Clear();
        UnitManager[] units = UnityEngine.Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;

            int teamId = (int)unit.TeamId;
            if (teamId < 0 || teamId > 9)
                continue;
            if (!seen.Add(teamId))
                continue;
            output.Add(teamId);
        }

        output.Sort();
        return output.Count > 0;
    }

    private void ApplyEnemyThreatLayersOverlayForTeam(int teamId)
    {
        ClearEnemyThreatLayersOverlay();
        ClearLineOfFireArea();
        UnitManager[] units = UnityEngine.Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if ((int)unit.TeamId != teamId)
                continue;

            PaintThreatOverlayForUnit(unit, enemyThreatRangeCells, enemyThreatRangeLookup, enemyThreatLineCells, enemyThreatLineLookup);
        }
    }

    private void ClearEnemyThreatLayersOverlay()
    {
        if (rangeMapTilemap != null)
        {
            for (int i = 0; i < enemyThreatRangeCells.Count; i++)
            {
                Vector3Int cell = enemyThreatRangeCells[i];
                rangeMapTilemap.SetTile(cell, null);
                rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
                rangeMapTilemap.SetColor(cell, Color.white);
            }
        }
        enemyThreatRangeCells.Clear();
        enemyThreatRangeLookup.Clear();

        if (lineOfFireMapTilemap != null)
        {
            for (int i = 0; i < enemyThreatLineCells.Count; i++)
            {
                Vector3Int cell = enemyThreatLineCells[i];
                lineOfFireMapTilemap.SetTile(cell, null);
                lineOfFireMapTilemap.SetTileFlags(cell, TileFlags.None);
                lineOfFireMapTilemap.SetColor(cell, Color.white);
            }
        }
        enemyThreatLineCells.Clear();
        enemyThreatLineLookup.Clear();
        enemyThreatLayersEnabled = false;
    }

    private void PaintThreatOverlayForUnit(
        UnitManager unit,
        List<Vector3Int> targetRangeCells,
        HashSet<Vector3Int> targetRangeLookup,
        List<Vector3Int> targetLineCells,
        HashSet<Vector3Int> targetLineLookup)
    {
        if (unit == null || targetRangeCells == null || targetRangeLookup == null || targetLineCells == null || targetLineLookup == null)
            return;

        InspectedThreatProfile threatProfile = ResolveInspectedThreatProfile(unit);
        if (threatProfile == InspectedThreatProfile.None)
            return;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : unit.BoardTilemap;
        if (boardMap == null || lineOfFireOverlayTile == null)
            return;
        if (rangeMapTilemap == null)
            rangeMapTilemap = FindRangeMapTilemap();
        if (lineOfFireMapTilemap == null)
            lineOfFireMapTilemap = FindLineOfFireMapTilemap();
        if (lineOfFireMapTilemap == null)
            return;

        bool enableLdt = matchController != null ? matchController.EnableLdtValidation : true;
        bool enableLos = matchController != null ? matchController.EnableLosValidation : true;
        bool enableSpotter = matchController != null ? matchController.EnableSpotter : true;

        Color teamColor = TeamUtils.GetColor(unit.TeamId);

        bool includeStaticThreat = threatProfile == InspectedThreatProfile.DistanceStatic || threatProfile == InspectedThreatProfile.Hybrid;
        bool includeMovementThreat = threatProfile == InspectedThreatProfile.Movement || threatProfile == InspectedThreatProfile.Hybrid;
        if (!TryResolveThreatOverlayCells(
                unit,
                boardMap,
                threatProfile,
                includeStaticThreat,
                includeMovementThreat,
                enableLdt,
                enableLos,
                enableSpotter,
                out List<Vector3Int> cachedRangeCells,
                out List<Vector3Int> cachedLineCells))
        {
            return;
        }

        Color movementColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(movementRangeAlpha));
        if (rangeMapTilemap != null && rangeOverlayTile != null)
        {
            for (int i = 0; i < cachedRangeCells.Count; i++)
            {
                Vector3Int cell = cachedRangeCells[i];
                if (targetRangeLookup.Contains(cell))
                    continue;

                rangeMapTilemap.SetTile(cell, rangeOverlayTile);
                rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
                rangeMapTilemap.SetColor(cell, movementColor);
                targetRangeCells.Add(cell);
                targetRangeLookup.Add(cell);
            }
        }

        if (cachedLineCells.Count <= 0)
            return;
        Color threatColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(lineOfFireAlpha));
        for (int i = 0; i < cachedLineCells.Count; i++)
        {
            Vector3Int cell = cachedLineCells[i];
            if (targetLineLookup.Contains(cell))
                continue;
            lineOfFireMapTilemap.SetTile(cell, lineOfFireOverlayTile);
            lineOfFireMapTilemap.SetTileFlags(cell, TileFlags.None);
            lineOfFireMapTilemap.SetColor(cell, threatColor);
            targetLineCells.Add(cell);
            targetLineLookup.Add(cell);
        }
    }

    private bool TryResolveThreatOverlayCells(
        UnitManager unit,
        Tilemap boardMap,
        InspectedThreatProfile threatProfile,
        bool includeStaticThreat,
        bool includeMovementThreat,
        bool enableLdt,
        bool enableLos,
        bool enableSpotter,
        out List<Vector3Int> rangeCells,
        out List<Vector3Int> lineCells)
    {
        rangeCells = null;
        lineCells = null;
        if (unit == null || boardMap == null)
            return false;

        ThreatOverlayCacheKey key = BuildThreatOverlayCacheKey(unit, boardMap, enableLdt, enableLos, enableSpotter);
        int cacheIndex = ResolveThreatOverlayCacheIndex(unit);
        if (threatOverlayCacheByUnitInstanceId.TryGetValue(cacheIndex, out ThreatOverlayCacheEntry existing) &&
            existing != null &&
            existing.key.Equals(key))
        {
            RegisterThreatOverlayCacheResult(unit, cacheIndex, wasHit: true);
            rangeCells = existing.rangeCells;
            lineCells = existing.lineCells;
            return true;
        }

        HashSet<Vector3Int> staticThreatCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> mobileThreatCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> movementCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> finalRangeCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> finalLineCells = new HashSet<Vector3Int>();

        if (includeStaticThreat)
        {
            HashSet<Vector3Int> staticThreatRawCells = new HashSet<Vector3Int>();
            PodeMirarSensor.CollectValidFireCellsFromOrigin(
                unit,
                boardMap,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                unit.CurrentCellPosition,
                staticThreatRawCells,
                dpqAirHeightConfig,
                enableLdt,
                enableLos,
                enableSpotter);

            foreach (Vector3Int targetCell in staticThreatRawCells)
            {
                Vector3Int cell = targetCell;
                cell.z = 0;
                if (boardMap.GetTile(cell) == null)
                    continue;
                staticThreatCells.Add(cell);
            }
        }

        if (includeMovementThreat)
        {
            int movementSteps = ResolveInspectionMovementSteps(unit);
            if (movementSteps >= 0)
            {
                Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
                    boardMap,
                    unit,
                    movementSteps,
                    terrainDatabase);

                foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in validPaths)
                {
                    Vector3Int cell = pair.Key;
                    cell.z = 0;
                    if (boardMap.GetTile(cell) == null)
                        continue;
                    movementCells.Add(cell);
                }

                Vector3Int origin = unit.CurrentCellPosition;
                origin.z = 0;
                if (boardMap.GetTile(origin) != null)
                    movementCells.Add(origin);

                foreach (Vector3Int cell in movementCells)
                {
                    if (threatProfile == InspectedThreatProfile.Hybrid && staticThreatCells.Contains(cell))
                        continue;
                    finalRangeCells.Add(cell);
                }

                if (movementCells.Count > 0)
                {
                    HashSet<Vector3Int> localThreatCells = new HashSet<Vector3Int>();
                    foreach (Vector3Int moveCell in movementCells)
                    {
                        localThreatCells.Clear();
                        PodeMirarSensor.CollectValidFireCellsFromOrigin(
                            unit,
                            boardMap,
                            terrainDatabase,
                            SensorMovementMode.MoveuAndando,
                            moveCell,
                            localThreatCells,
                            dpqAirHeightConfig,
                            enableLdt,
                            enableLos,
                            enableSpotter);
                        foreach (Vector3Int targetCell in localThreatCells)
                        {
                            Vector3Int cell = targetCell;
                            cell.z = 0;
                            if (boardMap.GetTile(cell) == null)
                                continue;
                            mobileThreatCells.Add(cell);
                        }
                    }
                }
            }
        }

        HashSet<Vector3Int> threatCells = new HashSet<Vector3Int>(staticThreatCells);
        threatCells.UnionWith(mobileThreatCells);
        foreach (Vector3Int cell in threatCells)
        {
            if (threatProfile == InspectedThreatProfile.Hybrid)
            {
                bool isStaticThreat = staticThreatCells.Contains(cell);
                if (!isStaticThreat && movementCells.Contains(cell))
                    continue;
            }
            else if (includeMovementThreat && movementCells.Contains(cell))
            {
                continue;
            }

            finalLineCells.Add(cell);
        }

        ThreatOverlayCacheEntry cached = existing ?? new ThreatOverlayCacheEntry();
        cached.key = key;
        cached.rangeCells.Clear();
        cached.lineCells.Clear();
        if (finalRangeCells.Count > 0)
            cached.rangeCells.AddRange(finalRangeCells);
        if (finalLineCells.Count > 0)
            cached.lineCells.AddRange(finalLineCells);
        threatOverlayCacheByUnitInstanceId[cacheIndex] = cached;
        RegisterThreatOverlayCacheResult(unit, cacheIndex, wasHit: false);

        rangeCells = cached.rangeCells;
        lineCells = cached.lineCells;
        return true;
    }

    public IEnumerator WarmUpThreatCacheFromScene(Action<int, int> onProgress = null, int unitsPerFrame = 4)
    {
        if (!Application.isPlaying)
            yield break;
        if (matchController != null && matchController.EnableTotalWar)
        {
            onProgress?.Invoke(0, 0);
            Debug.Log("[HotzoneCache] Skip warm-up: Total War ativo.");
            yield break;
        }

        UnitManager[] allUnits = UnityEngine.Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<UnitManager> candidates = new List<UnitManager>(allUnits.Length);
        for (int i = 0; i < allUnits.Length; i++)
        {
            UnitManager unit = allUnits[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;

            candidates.Add(unit);
        }

        int total = candidates.Count;
        int processed = 0;
        onProgress?.Invoke(processed, total);
        if (total <= 0)
            yield break;

        int batchSize = Mathf.Max(1, unitsPerFrame);
        for (int i = 0; i < candidates.Count; i++)
        {
            PreWarmThreatOverlayCacheForUnit(candidates[i]);
            processed++;
            onProgress?.Invoke(processed, total);

            if (processed % batchSize == 0)
                yield return null;
        }
    }

    private void PreWarmThreatOverlayCacheForUnit(UnitManager unit)
    {
        if (unit == null)
            return;

        InspectedThreatProfile threatProfile = ResolveInspectedThreatProfile(unit);
        if (threatProfile == InspectedThreatProfile.None)
            return;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : unit.BoardTilemap;
        if (boardMap == null)
            return;

        bool enableLdt = matchController != null ? matchController.EnableLdtValidation : true;
        bool enableLos = matchController != null ? matchController.EnableLosValidation : true;
        bool enableSpotter = matchController != null ? matchController.EnableSpotter : true;
        bool includeStaticThreat = threatProfile == InspectedThreatProfile.DistanceStatic || threatProfile == InspectedThreatProfile.Hybrid;
        bool includeMovementThreat = threatProfile == InspectedThreatProfile.Movement || threatProfile == InspectedThreatProfile.Hybrid;

        TryResolveThreatOverlayCells(
            unit,
            boardMap,
            threatProfile,
            includeStaticThreat,
            includeMovementThreat,
            enableLdt,
            enableLos,
            enableSpotter,
            out _,
            out _);
    }

    private static int ResolveThreatOverlayCacheIndex(UnitManager unit)
    {
        if (unit == null)
            return 0;

        int instanceId = unit.InstanceId;
        if (instanceId > 0)
            return instanceId;
        return unit.GetInstanceID();
    }

    private static ThreatOverlayCacheKey BuildThreatOverlayCacheKey(UnitManager unit, Tilemap boardMap, bool enableLdt, bool enableLos, bool enableSpotter)
    {
        int snapshotHash = BuildUnitSnapshotHash(unit, boardMap);
        int globalBoardRevision = ThreatRevisionTracker.GlobalBoardRevision;
        int teamObserverRevision = ThreatRevisionTracker.GetTeamObserverRevision(unit != null ? unit.TeamId : TeamId.Neutral);
        int matchFlagsHash = ThreatRevisionTracker.MatchFlagsHash;

        // Mantem consistencia mesmo em contextos antigos sem sincronizacao externa.
        int requestedFlagsHash = BuildThreatFlagsHash(enableLdt, enableLos, enableSpotter);
        if (matchFlagsHash != requestedFlagsHash)
            matchFlagsHash = requestedFlagsHash;

        return new ThreatOverlayCacheKey(snapshotHash, globalBoardRevision, teamObserverRevision, matchFlagsHash);
    }

    private static int BuildUnitSnapshotHash(UnitManager unit, Tilemap boardMap)
    {
        unchecked
        {
            if (unit == null)
                return 0;

            int hash = 17;
            Vector3Int cell = unit.CurrentCellPosition;
            hash = (hash * 31) + cell.x;
            hash = (hash * 31) + cell.y;
            hash = (hash * 31) + (int)unit.GetDomain();
            hash = (hash * 31) + (int)unit.GetHeightLevel();
            hash = (hash * 31) + (int)unit.TeamId;
            hash = (hash * 31) + (unit.IsEmbarked ? 1 : 0);
            hash = (hash * 31) + (unit.IsAircraftGrounded ? 1 : 0);
            hash = (hash * 31) + Mathf.Max(0, unit.CurrentFuel);
            hash = (hash * 31) + Mathf.Max(0, unit.MaxMovementPoints);
            hash = (hash * 31) + (boardMap != null ? boardMap.GetInstanceID() : 0);

            IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
            int weaponCount = weapons != null ? weapons.Count : 0;
            hash = (hash * 31) + weaponCount;
            for (int i = 0; i < weaponCount; i++)
            {
                UnitEmbarkedWeapon runtimeWeapon = weapons[i];
                if (runtimeWeapon == null)
                {
                    hash = (hash * 31) + 7;
                    continue;
                }

                WeaponData weaponData = runtimeWeapon.weapon;
                hash = (hash * 31) + (weaponData != null ? weaponData.GetInstanceID() : 0);
                hash = (hash * 31) + Mathf.Max(0, runtimeWeapon.squadAmmunition);
                hash = (hash * 31) + (int)runtimeWeapon.selectedTrajectory;
                hash = (hash * 31) + runtimeWeapon.GetRangeMin();
                hash = (hash * 31) + runtimeWeapon.GetRangeMax();
            }

            return hash;
        }
    }

    private static int BuildThreatFlagsHash(bool enableLdt, bool enableLos, bool enableSpotter)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (enableLdt ? 1 : 0);
            hash = hash * 31 + (enableLos ? 1 : 0);
            hash = hash * 31 + (enableSpotter ? 1 : 0);
            return hash;
        }
    }

    private void RegisterThreatOverlayCacheResult(UnitManager unit, int cacheIndex, bool wasHit)
    {
        if (!Application.isPlaying)
            return;

        if (!threatOverlayCacheMetricsByUnitInstanceId.TryGetValue(cacheIndex, out ThreatOverlayCacheMetrics metrics) || metrics == null)
        {
            metrics = new ThreatOverlayCacheMetrics();
            threatOverlayCacheMetricsByUnitInstanceId[cacheIndex] = metrics;
        }

        if (wasHit)
        {
            metrics.hits++;
            threatOverlayCacheTotalHits++;
        }
        else
        {
            metrics.misses++;
            threatOverlayCacheTotalMisses++;
        }

        int unitHits = metrics.hits;
        int unitMisses = metrics.misses;
        int unitTotal = unitHits + unitMisses;
        float unitHitRate = unitTotal > 0 ? (100f * unitHits / unitTotal) : 0f;

        int totalHits = threatOverlayCacheTotalHits;
        int totalMisses = threatOverlayCacheTotalMisses;
        int total = totalHits + totalMisses;
        float totalHitRate = total > 0 ? (100f * totalHits / total) : 0f;

        string unitName = unit != null ? ResolveUnitRuntimeName(unit) : $"unit#{cacheIndex}";
        string result = wasHit ? "HIT" : "MISS";
        Debug.Log(
            $"[HotzoneCache] {result} | unit={unitName} ({cacheIndex}) | " +
            $"unit[h={unitHits},m={unitMisses},rate={unitHitRate:0.0}%] | " +
            $"session[h={totalHits},m={totalMisses},rate={totalHitRate:0.0}%]");
    }

    private static int ResolveInspectionMovementSteps(UnitManager unit)
    {
        if (unit == null)
            return 0;

        // Inspecao de ameaca deve representar capacidade potencial da unidade,
        // nao os pontos restantes da rodada atual.
        return Mathf.Max(0, unit.MaxMovementPoints);
    }

    private void ClearInspectedThreatOverlay()
    {
        if (rangeMapTilemap != null)
        {
            for (int i = 0; i < inspectedThreatRangeCells.Count; i++)
            {
                Vector3Int cell = inspectedThreatRangeCells[i];
                rangeMapTilemap.SetTile(cell, null);
                rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
                rangeMapTilemap.SetColor(cell, Color.white);
            }
        }
        inspectedThreatRangeCells.Clear();
        inspectedThreatRangeLookup.Clear();

        if (lineOfFireMapTilemap != null)
        {
            for (int i = 0; i < inspectedThreatLineCells.Count; i++)
            {
                Vector3Int cell = inspectedThreatLineCells[i];
                lineOfFireMapTilemap.SetTile(cell, null);
                lineOfFireMapTilemap.SetTileFlags(cell, TileFlags.None);
                lineOfFireMapTilemap.SetColor(cell, Color.white);
            }
        }
        inspectedThreatLineCells.Clear();
        inspectedThreatLineLookup.Clear();
    }

    private enum InspectedThreatProfile
    {
        None = 0,
        Movement = 1,
        DistanceStatic = 2,
        Hybrid = 3
    }

    private static InspectedThreatProfile ResolveInspectedThreatProfile(UnitManager unit)
    {
        if (unit == null)
            return InspectedThreatProfile.None;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count <= 0)
            return InspectedThreatProfile.None;

        bool hasOneToOne = false;
        bool hasGreaterThanOne = false;
        bool hasHybridRange = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null || embarked.squadAmmunition <= 0)
                continue;

            int min = embarked.GetRangeMin();
            int max = embarked.GetRangeMax();
            if (max < min)
                max = min;
            if (max > 1)
                hasGreaterThanOne = true;
            if (min == 1 && max > 1)
                hasHybridRange = true;
            if (min == 1 && max == 1)
                hasOneToOne = true;
        }

        if (hasHybridRange || (hasOneToOne && hasGreaterThanOne))
            return InspectedThreatProfile.Hybrid;
        if (hasGreaterThanOne)
            return InspectedThreatProfile.DistanceStatic;
        if (hasOneToOne)
            return InspectedThreatProfile.Movement;
        return InspectedThreatProfile.None;
    }

    private void UpdateInspectedHelperAutoDismiss()
    {
        UpdateHoverInspection();
        UpdateTurnStartAutonomyHelperAutoDismiss();


        if (!IsInspectedHelperActive())
        {
            bool hadHelper = inspectedHelperUnit != null || inspectedHelperConstruction != null || inspectedHelperTerrain;
            // Timeout de um helper de unidade/construcao: se ainda estamos presos no estado
            // de inspecao, volta pra neutral (Retreat). So limpar deixaria o CursorState travado
            // em InspectingUnit/Building. (InspectingHotZone nao usa helper e tem dismiss proprio.)
            if (hadHelper && IsInspectingState())
                ExitInspectStateToNeutral();
            else if (hadHelper)
                ClearInspectedHelper();
            return;
        }

        if (Time.frameCount <= inspectedHelperActivatedFrame)
            return;

        if (cursorController != null && cursorController.CurrentCell != inspectedHelperCursorCell)
        {
            ExitInspectStateToNeutral();
            return;
        }

        bool anyInput = WasAnyInputPressedThisFrame();
        if (anyInput)
            ExitInspectStateToNeutral();
    }

    private void UpdateHoverInspection()
    {
        if (cursorController == null || CurrentCursorState != CursorState.Neutral)
        {
            lastHoveredCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            hoveredCellStartTime = -1f;
            hasTriggeredHoverAtCurrentCell = false;
            return;
        }

        Vector3Int currentCell = cursorController.CurrentCell;
        if (currentCell != lastHoveredCell)
        {
            lastHoveredCell = currentCell;
            hoveredCellStartTime = Time.unscaledTime;
            hasTriggeredHoverAtCurrentCell = false;
            return;
        }

        if (hasTriggeredHoverAtCurrentCell)
            return;

        float delay = DialogManager.Instance != null ? DialogManager.Instance.HoverHelpDelay : 0.5f;
        // Se o usuário espera 0.5s, garantimos que não passe disso por padrão se não configurado
        if (delay <= 0) delay = 0.5f;

        if (Time.unscaledTime - hoveredCellStartTime >= delay)
        {
            hasTriggeredHoverAtCurrentCell = true;
            if (matchController != null && !matchController.IsCellVisibleInFogPresentation(currentCell))
                return;

            int activeTeamId = matchController != null ? matchController.ActiveTeamId : -1;
            TeamId activeTeam = activeTeamId >= 0 ? (TeamId)activeTeamId : TeamId.Neutral;

            UnitManager unit = FindUnitAtCell(currentCell);
            if (unit != null)
            {
                string unitName = ResolveUnitRuntimeName(unit);
                bool isAlly = (int)unit.TeamId == activeTeamId;
                bool canAct = isAlly && !unit.HasActed;
                
                HelpHintId hint = canAct ? HelpHintId.Act : HelpHintId.Inspect;
                DialogManager.Instance?.TryShowHint(activeTeam, hint, unitName);
            }
            else
            {
                ConstructionManager construction = FindConstructionAtCell(currentCell);
                if (construction != null)
                {
                    string constructionName = !string.IsNullOrWhiteSpace(construction.ConstructionDisplayName) 
                        ? construction.ConstructionDisplayName 
                        : construction.name;
                    
                    HelpHintId hint = construction.CanProduceUnitsForTeam(activeTeam) 
                        ? HelpHintId.Produce 
                        : HelpHintId.Construction;

                    DialogManager.Instance?.TryShowHint(activeTeam, hint, constructionName);
                }

                bool hasLocation = terrainTilemap != null && terrainTilemap.HasTile(currentCell);
                bool visibleToPlayer = matchController == null || matchController.IsCellVisibleInFogPresentation(currentCell);
                if (hasLocation && visibleToPlayer)
                    BeginInspectedTerrainHelper(currentCell);
            }
        }
    }

    private void UpdateTurnStartAutonomyHelperAutoDismiss()
    {
        if (!IsTurnStartAutonomyHelperActive())
        {
            if (turnStartAutonomyHelperLines.Count > 0)
                ClearTurnStartAutonomyHelper();
            return;
        }

        if (Time.frameCount <= turnStartAutonomyHelperActivatedFrame)
            return;

        if (cursorController != null && cursorController.CurrentCell != turnStartAutonomyHelperCursorCell)
        {
            ClearTurnStartAutonomyHelper();
            return;
        }

        if (WasAnyInputPressedThisFrame())
            ClearTurnStartAutonomyHelper();
    }

    private static bool WasAnyInputPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            return true;

        Mouse mouse = Mouse.current;
        if (mouse != null &&
            (mouse.leftButton.wasPressedThisFrame ||
             mouse.rightButton.wasPressedThisFrame ||
             mouse.middleButton.wasPressedThisFrame))
            return true;

        return false;
#else
        return Input.anyKeyDown ||
            Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1) ||
            Input.GetMouseButtonDown(2);
#endif
    }

    private void AppendTransportedUnitStatsLines(List<string> lines, UnitManager transporter, int depth)
    {
        if (lines == null || transporter == null)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null || seats.Count <= 0)
            return;

        string indent = new string(' ', Mathf.Max(0, depth) * 4);
        for (int i = 0; i < seats.Count; i++)
        {
            UnitManager passenger = seats[i] != null ? seats[i].embarkedUnit : null;
            if (passenger == null || !passenger.IsEmbarked || passenger.EmbarkedTransporter != transporter)
                continue;

            string stats = BuildUnitStatInlineWithoutSupplies(passenger);
            string supplies = BuildUnitSuppliesInline(passenger);
            lines.Add($"{indent}{ResolveUnitRuntimeName(passenger)} ({stats})||SUPPLIES||{supplies}");
            AppendTransportedUnitStatsLines(lines, passenger, depth + 1);
        }
    }

    private void AppendSupplierStockLines(List<string> lines, UnitManager supplier)
    {
        if (lines == null || supplier == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> resources = supplier.GetEmbarkedResources();
        if (resources == null || resources.Count <= 0)
            return;

        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply runtime = resources[i];
            if (runtime == null || runtime.supply == null)
                continue;

            int current = Mathf.Max(0, runtime.amount);
            int max = ResolveSupplierResourceMaxAmount(supplier, runtime.supply, current);
            string label = ResolveSupplyDisplayName(runtime.supply);
            lines.Add($"{current}/{max} {label}");
        }
    }

    private static int ResolveSupplierResourceMaxAmount(UnitManager supplier, SupplyData supply, int fallbackCurrent)
    {
        if (supplier != null && supply != null && supplier.TryGetUnitData(out UnitData data) && data != null && data.supplierResources != null)
        {
            for (int i = 0; i < data.supplierResources.Count; i++)
            {
                UnitEmbarkedSupply baseline = data.supplierResources[i];
                if (baseline == null || baseline.supply != supply)
                    continue;
                return Mathf.Max(0, baseline.amount);
            }
        }

        return Mathf.Max(0, fallbackCurrent);
    }

    private static string ResolveSupplyDisplayName(SupplyData supply)
    {
        if (supply == null)
            return "Supply";
        if (!string.IsNullOrWhiteSpace(supply.displayName))
            return supply.displayName;
        if (!string.IsNullOrWhiteSpace(supply.id))
            return supply.id;
        return supply.name;
    }

    private static string ResolveConstructionTransferRoleLabelForHelper(ConstructionManager construction)
    {
        if (construction == null || !construction.TryResolveConstructionData(out ConstructionData data) || data == null || !data.isSupplier)
            return string.Empty;

        if (data.supplierTier == SupplierTier.Receiver)
            return "Transferir - Recebedor";
        if (data.supplierTier != SupplierTier.Hub)
            return string.Empty;
        if (construction.HasInfiniteSuppliesFor())
            return "Transferir - Fornecedor";
        return "Transferir - Recebedor/Fornecedor";
    }

    private bool TryBuildCommandServiceHelperPanelData(HelperPanelData data)
    {
        bool shouldShowEstimate = IsCommandServiceAwaitingConfirmation && commandServiceHelperServedTargets > 0;
        bool shouldShowSummary = !IsCommandServiceAwaitingConfirmation &&
            Time.time <= commandServiceHelperVisibleUntil &&
            commandServiceHelperServedTargets > 0;
        if (data == null || (!shouldShowEstimate && !shouldShowSummary))
            return false;

        data.Kind = HelperPanelKind.CommandService;
        data.CommandServiceServedTargets = Mathf.Max(0, commandServiceHelperServedTargets);
        data.CommandServiceRecoveredHp = Mathf.Max(0, commandServiceHelperRecoveredHp);
        data.CommandServiceRecoveredFuel = Mathf.Max(0, commandServiceHelperRecoveredFuel);
        data.CommandServiceRecoveredAmmo = Mathf.Max(0, commandServiceHelperRecoveredAmmo);
        data.CommandServiceTotalCost = Mathf.Max(0, commandServiceHelperTotalCost);
        data.CommandServiceStoppedByEconomy = commandServiceHelperStoppedByEconomy;
        data.CommandServiceIsEstimate = commandServiceHelperIsEstimate;
        data.CommandServiceMoneyBefore = Mathf.Max(0, commandServiceHelperMoneyBefore);
        data.CommandServiceMoneyAfter = Mathf.Max(0, commandServiceHelperMoneyAfter);
        for (int i = 0; i < commandServiceHelperTargetLines.Count; i++)
        {
            HelperCommandServiceTargetLine line = commandServiceHelperTargetLines[i];
            if (line == null)
                continue;

            data.CommandServiceTargetLines.Add(new HelperCommandServiceTargetLine
            {
                unitName = line.unitName,
                sourceLabel = line.sourceLabel,
                gainsLabel = line.gainsLabel,
                isFocused = line.isFocused,
                isFullyAffordable = line.isFullyAffordable
            });
        }

        for (int i = 0; i < commandServiceHelperSkippedUnitLines.Count; i++)
        {
            HelperCommandServiceSkippedUnitLine line = commandServiceHelperSkippedUnitLines[i];
            if (line == null)
                continue;

            data.CommandServiceSkippedUnitLines.Add(new HelperCommandServiceSkippedUnitLine
            {
                unitName = line.unitName,
                sourceLabel = line.sourceLabel,
                isFocused = line.isFocused
            });
        }
        return true;
    }

    private void ShowCommandServiceHelperSummary(
        int servedTargets,
        int recoveredHp,
        int recoveredFuel,
        int recoveredAmmo,
        int totalCost,
        bool stoppedByEconomy,
        float durationSeconds = 3.2f)
    {
        commandServiceHelperServedTargets = Mathf.Max(0, servedTargets);
        commandServiceHelperRecoveredHp = Mathf.Max(0, recoveredHp);
        commandServiceHelperRecoveredFuel = Mathf.Max(0, recoveredFuel);
        commandServiceHelperRecoveredAmmo = Mathf.Max(0, recoveredAmmo);
        commandServiceHelperTotalCost = Mathf.Max(0, totalCost);
        commandServiceHelperStoppedByEconomy = stoppedByEconomy;
        commandServiceHelperIsEstimate = false;
        commandServiceHelperMoneyBefore = 0;
        commandServiceHelperMoneyAfter = 0;
        commandServiceHelperTargetLines.Clear();
        commandServiceHelperSkippedUnitLines.Clear();
        commandServiceHelperVisibleUntil = Time.time + Mathf.Max(0.1f, durationSeconds);
    }

    private void ShowCommandServiceHelperEstimate(
        int servedTargets,
        int recoveredHp,
        int recoveredFuel,
        int recoveredAmmo,
        int totalCost,
        bool stoppedByEconomy,
        int moneyBefore,
        int moneyAfter,
        List<HelperCommandServiceTargetLine> targetLines = null,
        List<HelperCommandServiceSkippedUnitLine> skippedUnitLines = null)
    {
        commandServiceHelperServedTargets = Mathf.Max(0, servedTargets);
        commandServiceHelperRecoveredHp = Mathf.Max(0, recoveredHp);
        commandServiceHelperRecoveredFuel = Mathf.Max(0, recoveredFuel);
        commandServiceHelperRecoveredAmmo = Mathf.Max(0, recoveredAmmo);
        commandServiceHelperTotalCost = Mathf.Max(0, totalCost);
        commandServiceHelperStoppedByEconomy = stoppedByEconomy;
        commandServiceHelperIsEstimate = true;
        commandServiceHelperMoneyBefore = Mathf.Max(0, moneyBefore);
        commandServiceHelperMoneyAfter = Mathf.Max(0, moneyAfter);
        commandServiceHelperTargetLines.Clear();
        if (targetLines != null)
        {
            for (int i = 0; i < targetLines.Count; i++)
            {
                HelperCommandServiceTargetLine line = targetLines[i];
                if (line == null)
                    continue;

                commandServiceHelperTargetLines.Add(new HelperCommandServiceTargetLine
                {
                    unitName = line.unitName,
                    sourceLabel = line.sourceLabel,
                    gainsLabel = line.gainsLabel,
                    isFocused = line.isFocused,
                    isFullyAffordable = line.isFullyAffordable
                });
            }
        }
        commandServiceHelperSkippedUnitLines.Clear();
        if (skippedUnitLines != null)
        {
            for (int i = 0; i < skippedUnitLines.Count; i++)
            {
                HelperCommandServiceSkippedUnitLine line = skippedUnitLines[i];
                if (line == null)
                    continue;

                commandServiceHelperSkippedUnitLines.Add(new HelperCommandServiceSkippedUnitLine
                {
                    unitName = line.unitName,
                    sourceLabel = line.sourceLabel,
                    isFocused = line.isFocused
                });
            }
        }
        commandServiceHelperVisibleUntil = -1f;
    }

    private void ClearCommandServiceHelper()
    {
        commandServiceHelperVisibleUntil = -1f;
        commandServiceHelperServedTargets = 0;
        commandServiceHelperRecoveredHp = 0;
        commandServiceHelperRecoveredFuel = 0;
        commandServiceHelperRecoveredAmmo = 0;
        commandServiceHelperTotalCost = 0;
        commandServiceHelperStoppedByEconomy = false;
        commandServiceHelperIsEstimate = false;
        commandServiceHelperMoneyBefore = 0;
        commandServiceHelperMoneyAfter = 0;
        commandServiceHelperTargetLines.Clear();
        commandServiceHelperSkippedUnitLines.Clear();
    }

    private bool TryBuildShoppingHelperPanelData(HelperPanelData data)
    {
        if (data == null || shoppingUnitsForSale == null || shoppingUnitsForSale.Count <= 0)
            return false;

        data.Kind = HelperPanelKind.Shopping;
        data.ShoppingConstructionName = ResolveConstructionName(shoppingConstruction);

        for (int i = 0; i < shoppingUnitsForSale.Count; i++)
        {
            UnitData unit = shoppingUnitsForSale[i];
            if (unit == null)
                continue;

            int? resolvedCost = null;
            if (matchController != null)
                resolvedCost = matchController.ResolveEconomyCost(unit.cost);
            bool canAfford = !resolvedCost.HasValue || matchController == null ||
                matchController.GetActualMoney((TeamId)matchController.ActiveTeamId) >= resolvedCost.Value;

            data.ShoppingLines.Add(new HelperShoppingLine
            {
                index = i + 1,
                unitName = ResolveUnitName(unit),
                cost = resolvedCost,
                canAfford = canAfford,
                isFocused = !shoppingCancelFocused && shoppingSelectedIndex == i
            });
        }

        // Slot CANCELAR ao final da lista: da pra chegar nele com as setas e sair da loja sem mouse/Esc.
        data.ShoppingLines.Add(new HelperShoppingLine
        {
            index = -1,
            unitName = "CANCELAR",
            cost = null,
            isFocused = shoppingCancelFocused,
            isCancel = true
        });

        return data.ShoppingLines.Count > 0;
    }

    private bool TryBuildSensorsHelperPanelData(HelperPanelData data)
    {
        if (data == null)
            return false;

        bool isMovementSensorState = CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado;
        bool isThreatLayerSelectionStep = scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect;
        if (!isMovementSensorState && !isThreatLayerSelectionStep)
            return false;

        data.Kind = HelperPanelKind.Sensors;
        if (isMovementSensorState)
        {
            // Capturador: se "Capturar" estiver disponivel, sobe pro topo pra ser a primeira opcao
            // (o proprio papel da unidade prioriza captura). Para os demais papeis, ordem padrao.
            bool capturerFirst = IsSelectedUnitPrimaryCapturer();
            if (capturerFirst)
                TryAddSensorLine(data, 'C', "capture");

            TryAddSensorLine(data, 'A', "aim");
            TryAddSensorLine(data, 'E', "embark");
            TryAddSensorLine(data, 'D', "disembark");
            if (!capturerFirst)
                TryAddSensorLine(data, 'C', "capture");
            TryAddSensorLine(data, 'F', "fuse");
            TryAddSensorLine(data, 'S', "supply");
            TryAddSensorLine(data, 'T', "transfer");
            TryAddSensorLine(data, 'M', "move_only", forceInclude: true);
        }

        if (isThreatLayerSelectionStep)
        {
            data.ThreatLayerSelectionActive = true;
            data.ThreatLayerInspectedTeamId = enemyThreatLayersInspectedTeamId;
            int activeTeam = matchController != null ? matchController.ActiveTeamId : int.MinValue;
            for (int i = 0; i < threatLayerSelectableTeamIds.Count && i < threatLayerSelectableOptionNumbers.Count; i++)
            {
                int teamId = threatLayerSelectableTeamIds[i];
                data.ThreatLayerTeamLines.Add(new HelperThreatLayerTeamLine
                {
                    optionNumber = threatLayerSelectableOptionNumbers[i],
                    teamId = teamId,
                    teamName = TeamUtils.GetName((TeamId)teamId),
                    isOwnTeam = teamId == activeTeam
                });
            }
        }

        return data.SensorLines.Count > 0 || data.ThreatLayerSelectionActive;
    }

    private void TryAddSensorLine(HelperPanelData data, char actionCode, string sensorKey, bool forceInclude = false)
    {
        if (data == null)
            return;
        if (!forceInclude && (availableSensorActionCodes == null || !availableSensorActionCodes.Contains(actionCode)))
            return;

        data.SensorLines.Add(new HelperSensorLine
        {
            actionCode = actionCode,
            sensorKey = sensorKey ?? string.Empty
        });
    }

    // Papel de composicao Capturador (inclui CapturadorAgressivo via ResolveCompositionRole).
    // Usado para priorizar "Capturar" no topo do painel de opcoes do sensor.
    private bool IsSelectedUnitPrimaryCapturer()
    {
        if (selectedUnit == null || !selectedUnit.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Capturador;
    }

    private bool TryBuildDisembarkHelperPanelData(HelperPanelData data)
    {
        if (data == null || disembarkPassengerEntries == null || disembarkPassengerEntries.Count <= 0)
            return false;

        data.Kind = HelperPanelKind.Disembark;
        data.DisembarkStep = scannerPromptStep == ScannerPromptStep.DisembarkPassengerSelect ? 0 :
                             scannerPromptStep == ScannerPromptStep.DisembarkLandingSelect ? 1 : 2;

        if (TryGetSelectedPassengerEntry(out DisembarkPassengerEntry selectedPassenger))
            data.DisembarkSelectedPassengerName = ResolveUnitRuntimeName(selectedPassenger.passenger);
        if (disembarkSelectedLandingCellValid)
        {
            string terrain = ResolveTerrainLabelForCell(disembarkSelectedLandingCell);
            data.DisembarkSelectedLandingLabel = string.IsNullOrWhiteSpace(terrain)
                ? FormatMapCell(disembarkSelectedLandingCell)
                : $"{terrain} {FormatMapCell(disembarkSelectedLandingCell)}";
        }

        if (disembarkQueuedOrders != null && disembarkQueuedOrders.Count > 0)
        {
            for (int i = 0; i < disembarkQueuedOrders.Count; i++)
            {
                DisembarkOrder order = disembarkQueuedOrders[i];
                if (order == null || order.passenger == null)
                    continue;

                data.DisembarkOrderLines.Add(new HelperDisembarkOrderLine
                {
                    index = i + 1,
                    unitName = ResolveUnitRuntimeName(order.passenger),
                    stats = BuildUnitStatInline(order.passenger),
                    terrainName = ResolveCellTerrainLabel(order.targetCell),
                    unitSprite = order.passenger.GetMainSpriteRenderer() != null
                        ? order.passenger.GetMainSpriteRenderer().sprite : null,
                    unitColor = order.passenger.GetMainSpriteRenderer() != null
                        ? order.passenger.GetMainSpriteRenderer().color : Color.white
                });
                HelperDisembarkOrderLine added = data.DisembarkOrderLines[data.DisembarkOrderLines.Count - 1];
                ResolveCellLocalVisual(order.targetCell, out added.localSprite, out added.localColor);
            }
        }

        for (int i = 0; i < disembarkPassengerEntries.Count; i++)
        {
            DisembarkPassengerEntry entry = disembarkPassengerEntries[i];
            if (entry == null || entry.passenger == null)
                continue;

            data.DisembarkPassengerLines.Add(new HelperDisembarkPassengerLine
            {
                index = entry.selectionNumber,
                unitName = ResolveUnitRuntimeName(entry.passenger),
                stats = BuildUnitStatInline(entry.passenger)
            });
        }

        data.HasQueuedDisembarkOrders = data.DisembarkOrderLines.Count > 0;
        return data.DisembarkPassengerLines.Count > 0;
    }

    private bool TryBuildMergeHelperPanelData(HelperPanelData data)
    {
        bool isMergeAnimating = animationManager != null && animationManager.IsAnimatingMovement;
        if (data == null || CurrentCursorState != CursorState.Fundindo || mergeExecutionInProgress || isMergeAnimating)
            return false;

        data.Kind = HelperPanelKind.Merge;
        data.IsMergeConfirmStep = scannerPromptStep == ScannerPromptStep.MergeConfirm;

        if (mergeQueuedUnits != null && mergeQueuedUnits.Count > 0)
        {
            for (int i = 0; i < mergeQueuedUnits.Count; i++)
            {
                UnitManager unit = mergeQueuedUnits[i];
                if (unit == null)
                    continue;

                data.MergeQueueLines.Add(new HelperMergeQueueLine
                {
                    index = i + 1,
                    unitName = ResolveUnitRuntimeName(unit),
                    stats = BuildUnitStatInline(unit),
                    unitSprite = unit.GetMainSpriteRenderer() != null ? unit.GetMainSpriteRenderer().sprite : null,
                    unitColor = unit.GetMainSpriteRenderer() != null ? unit.GetMainSpriteRenderer().color : Color.white
                });
            }
        }

        if (mergeCandidateEntries != null && mergeCandidateEntries.Count > 0)
        {
            for (int i = 0; i < mergeCandidateEntries.Count; i++)
            {
                MergeCandidateEntry entry = mergeCandidateEntries[i];
                if (entry == null || entry.unit == null)
                    continue;

                data.MergeCandidateLines.Add(new HelperMergeCandidateLine
                {
                    index = entry.selectionNumber,
                    unitName = ResolveUnitRuntimeName(entry.unit),
                    stats = BuildUnitStatInline(entry.unit),
                    isValid = entry.isValid,
                    invalidReason = ResolveMergeInvalidReason(entry),
                    unitSprite = entry.unit.GetMainSpriteRenderer() != null ? entry.unit.GetMainSpriteRenderer().sprite : null,
                    unitColor = entry.unit.GetMainSpriteRenderer() != null ? entry.unit.GetMainSpriteRenderer().color : Color.white
                });
            }
        }

        if (data.IsMergeConfirmStep &&
            mergeSelectedCandidateIndex >= 0 &&
            mergeSelectedCandidateIndex < mergeCandidateEntries.Count)
        {
            MergeCandidateEntry selected = mergeCandidateEntries[mergeSelectedCandidateIndex];
            if (selected != null && selected.unit != null)
            {
                data.HasSelectedMergeCandidate = true;
                data.SelectedMergeCandidateNumber = selected.selectionNumber;
                data.SelectedMergeCandidateName = ResolveUnitRuntimeName(selected.unit);
                data.SelectedMergeCandidateStats = BuildUnitStatInline(selected.unit);
                data.MergeConfirmPreview = BuildMergePreviewInline(selected.unit);
            }
        }

        if (data.MergeQueueLines.Count > 0)
            data.MergeQueuePreview = BuildMergePreviewInline(null);

        return data.MergeQueueLines.Count > 0 || data.MergeCandidateLines.Count > 0 || data.HasSelectedMergeCandidate;
    }

    private bool TryBuildEmbarkHelperPanelData(HelperPanelData data)
    {
        if (data == null || CurrentCursorState != CursorState.Embarcando)
            return false;

        if (IsEmbarkConfirmStep)
        {
            data.Kind = HelperPanelKind.EmbarkConfirm;
            if (scannerSelectedEmbarkIndex >= 0 && scannerSelectedEmbarkIndex < cachedPodeEmbarcarTargets.Count)
            {
                PodeEmbarcarOption selected = cachedPodeEmbarcarTargets[scannerSelectedEmbarkIndex];
                UnitManager transporter = selected != null ? selected.transporterUnit : null;
                data.AimConfirmTargetName = transporter != null ? ResolveUnitRuntimeName(transporter) : "Transportador";
                if (transporter != null)
                {
                    SpriteRenderer renderer = transporter.GetMainSpriteRenderer();
                    data.AimConfirmTargetSprite = renderer != null ? renderer.sprite : null;
                    data.AimConfirmTargetColor = renderer != null ? renderer.color : Color.white;
                    data.AimConfirmHp = Mathf.Max(0, transporter.CurrentHP);
                    Vector3Int cell = transporter.CurrentCellPosition;
                    data.AimConfirmTerrainLabel = ResolveCellTerrainLabel(cell);
                    ResolveCellLocalVisual(cell, out data.AimConfirmLocalSprite, out data.AimConfirmLocalColor);
                }
            }
            return true;
        }

        data.Kind = HelperPanelKind.Embark;
        if (cachedPodeEmbarcarTargets != null)
        {
            HashSet<UnitManager> addedTransporters = new HashSet<UnitManager>();
            for (int i = 0; i < cachedPodeEmbarcarTargets.Count; i++)
            {
                PodeEmbarcarOption option = cachedPodeEmbarcarTargets[i];
                UnitManager transporter = option != null ? option.transporterUnit : null;
                if (transporter == null || addedTransporters.Contains(transporter))
                    continue;

                addedTransporters.Add(transporter);
                int shownIndex = i + 1;
                bool isFocused = scannerSelectedEmbarkIndex == i;
                data.EmbarkCandidateLines.Add(new HelperEmbarkCandidateLine
                {
                    index = shownIndex,
                    unitName = ResolveUnitRuntimeName(transporter),
                    stats = BuildUnitStatInline(transporter),
                    isValid = true,
                    invalidReason = string.Empty,
                    isFocused = isFocused
                });
                data.AimTargetLines.Add(new HelperAimTargetLine
                {
                    index = i,
                    unitName = ResolveUnitRuntimeName(transporter),
                    isValid = true,
                    isFocused = !embarkCancelFocused && isFocused,
                    hp = Mathf.Max(0, transporter.CurrentHP),
                    terrainLabel = ResolveCellTerrainLabel(transporter.CurrentCellPosition),
                    unitSprite = transporter.GetMainSpriteRenderer() != null
                        ? transporter.GetMainSpriteRenderer().sprite : null,
                    unitColor = transporter.GetMainSpriteRenderer() != null
                        ? transporter.GetMainSpriteRenderer().color : Color.white
                });
            }

        }

        data.AimTargetLines.Add(new HelperAimTargetLine
        {
            index = -1,
            unitName = "CANCELAR",
            isValid = true,
            isFocused = embarkCancelFocused,
            isCancel = true
        });

        return data.AimTargetLines.Count > 1;
    }

    private bool TryBuildSupplyHelperPanelData(HelperPanelData data)
    {
        if (data == null || CurrentCursorState != CursorState.Suprindo || selectedUnit == null || supplyExecutionInProgress)
            return false;
        if (scannerPromptStep != ScannerPromptStep.MergeParticipantSelect && scannerPromptStep != ScannerPromptStep.MergeConfirm)
            return false;

        data.Kind = HelperPanelKind.Supply;
        data.SupplyIsConfirmStep = scannerPromptStep == ScannerPromptStep.MergeConfirm;
        data.SupplyHasQueuedOrders = supplyQueuedOrders != null && supplyQueuedOrders.Count > 0;

        for (int i = 0; i < supplyCandidateEntries.Count; i++)
        {
            SupplyCandidateEntry candidate = supplyCandidateEntries[i];
            if (candidate == null || candidate.targetUnit == null)
                continue;
            SpriteRenderer renderer = candidate.targetUnit.GetMainSpriteRenderer();
            data.SupplyCandidateLines.Add(new HelperSupplyCandidateLine
            {
                index = candidate.selectionNumber,
                unitName = ResolveUnitRuntimeName(candidate.targetUnit),
                stats = BuildUnitStatInline(candidate.targetUnit),
                unitSprite = renderer != null ? renderer.sprite : null,
                unitColor = renderer != null ? renderer.color : Color.white,
                isValid = true
            });
        }
        for (int i = 0; i < supplyInvalidCandidateEntries.Count; i++)
        {
            SupplyInvalidCandidateEntry candidate = supplyInvalidCandidateEntries[i];
            if (candidate == null || candidate.targetUnit == null)
                continue;
            SpriteRenderer renderer = candidate.targetUnit.GetMainSpriteRenderer();
            data.SupplyCandidateLines.Add(new HelperSupplyCandidateLine
            {
                index = supplyCandidateEntries.Count + i + 1,
                unitName = ResolveUnitRuntimeName(candidate.targetUnit),
                stats = BuildUnitStatInline(candidate.targetUnit),
                unitSprite = renderer != null ? renderer.sprite : null,
                unitColor = renderer != null ? renderer.color : Color.white,
                isValid = false,
                invalidReason = candidate.reason
            });
        }

        List<UnitManager> executionOrder = new List<UnitManager>();
        if (supplyQueuedOrders != null)
        {
            for (int i = 0; i < supplyQueuedOrders.Count; i++)
            {
                UnitManager queuedTarget = supplyQueuedOrders[i] != null ? supplyQueuedOrders[i].targetUnit : null;
                if (queuedTarget == null || executionOrder.Contains(queuedTarget))
                    continue;
                executionOrder.Add(queuedTarget);
            }
        }

        UnitManager focusedTarget = null;
        if (scannerPromptStep == ScannerPromptStep.MergeConfirm && TryGetSelectedSupplyCandidate(out SupplyCandidateEntry selected) && selected != null)
        {
            focusedTarget = selected.targetUnit;
            if (focusedTarget != null && !executionOrder.Contains(focusedTarget))
                executionOrder.Add(focusedTarget);
        }

        if (executionOrder.Count <= 0)
            return data.SupplyCandidateLines.Count > 0;

        List<SupplyEstimateLine> estimateLines = EstimateSupplyQueueForHelper(selectedUnit, executionOrder, focusedTarget);
        if (estimateLines.Count <= 0)
            return data.SupplyCandidateLines.Count > 0;

        int totalHp = 0;
        int totalFuel = 0;
        int totalAmmo = 0;
        int totalCost = 0;
        for (int i = 0; i < estimateLines.Count; i++)
        {
            SupplyEstimateLine line = estimateLines[i];
            if (line == null || line.target == null)
                continue;

            totalHp += Mathf.Max(0, line.hp);
            totalFuel += Mathf.Max(0, line.fuel);
            totalAmmo += Mathf.Max(0, line.ammo);
            totalCost += Mathf.Max(0, line.cost);

            data.SupplyTargetLines.Add(new HelperSupplyTargetLine
            {
                index = i + 1,
                unitName = ResolveUnitRuntimeName(line.target),
                gainsLabel = FormatSupplyGains(line.hp, line.fuel, line.ammo),
                estimatedCost = Mathf.Max(0, line.cost),
                isFocused = line.isFocused,
                unitSprite = line.target.GetMainSpriteRenderer() != null
                    ? line.target.GetMainSpriteRenderer().sprite : null,
                unitColor = line.target.GetMainSpriteRenderer() != null
                    ? line.target.GetMainSpriteRenderer().color : Color.white
            });
        }

        data.SupplyServedTargets = data.SupplyTargetLines.Count;
        data.SupplyRecoveredHp = Mathf.Max(0, totalHp);
        data.SupplyRecoveredFuel = Mathf.Max(0, totalFuel);
        data.SupplyRecoveredAmmo = Mathf.Max(0, totalAmmo);
        data.SupplyTotalCost = Mathf.Max(0, totalCost);
        BuildSupplyResourcePreviewLines(data, selectedUnit, executionOrder);
        return data.SupplyTargetLines.Count > 0 || data.SupplyCandidateLines.Count > 0;
    }

    private bool TryBuildTransferHelperPanelData(HelperPanelData data)
    {
        if (data == null || !IsTransferPromptActive() || selectedUnit == null || transferExecutionInProgress)
            return false;

        data.Kind = HelperPanelKind.Transfer;
        data.TransferIsConfirmStep = IsTransferConfirmStepActive();
        data.TransferHasCursorOption = false;
        data.TransferCursorOptionFocused = false;

        for (int i = 0; i < transferPromptOptions.Count; i++)
        {
            PodeTransferirOption option = transferPromptOptions[i];
            if (option == null)
                continue;

            data.TransferCandidateLines.Add(new HelperTransferCandidateLine
            {
                index = i + 1,
                unitName = ResolveTransferOptionTargetName(option),
                isDonate = option.flowMode == TransferFlowMode.Fornecimento,
                isFocused = transferPromptSelectedIndex == i,
                targetSprite = option.targetUnit != null
                    ? (option.targetUnit.GetMainSpriteRenderer() != null ? option.targetUnit.GetMainSpriteRenderer().sprite : null)
                    : (option.targetConstruction != null && option.targetConstruction.GetMainSpriteRenderer() != null
                        ? option.targetConstruction.GetMainSpriteRenderer().sprite : null),
                targetColor = option.targetUnit != null
                    ? (option.targetUnit.GetMainSpriteRenderer() != null ? option.targetUnit.GetMainSpriteRenderer().color : Color.white)
                    : (option.targetConstruction != null && option.targetConstruction.GetMainSpriteRenderer() != null
                        ? option.targetConstruction.GetMainSpriteRenderer().color : Color.white)
            });
        }

        if (data.TransferIsConfirmStep &&
            transferPromptSelectedIndex >= 0 &&
            transferPromptSelectedIndex < transferPromptOptions.Count)
        {
            PodeTransferirOption selectedOption = transferPromptOptions[transferPromptSelectedIndex];
            data.TransferSelectedLabel = ResolveTransferOptionLabel(selectedOption, transferPromptSelectedIndex + 1);
            ResolveTransferEndpoints(
                selectedOption,
                selectedUnit,
                out UnitManager sourceUnit,
                out ConstructionManager sourceConstruction,
                out UnitManager destinationUnit,
                out ConstructionManager destinationConstruction);
            data.TransferSourceLabel = sourceUnit != null
                ? ResolveUnitRuntimeName(sourceUnit)
                : ResolveConstructionName(sourceConstruction);
            data.TransferDestinationLabel = destinationUnit != null
                ? ResolveUnitRuntimeName(destinationUnit)
                : ResolveConstructionName(destinationConstruction);
            RebuildTransferPreviewLines();
            for (int i = 0; i < transferPreviewLines.Count; i++)
            {
                TransferEstimateLine line = transferPreviewLines[i];
                if (line == null || line.supply == null)
                    continue;

                data.TransferResourceLines.Add(new HelperTransferResourceLine
                {
                    supplyName = ResolveSupplyDisplayName(line.supply),
                    movedAmount = Mathf.Max(0, line.moved),
                    sourceBefore = line.sourceBefore >= int.MaxValue ? int.MaxValue : Mathf.Max(0, line.sourceBefore),
                    sourceAfter = line.sourceAfter >= int.MaxValue ? int.MaxValue : Mathf.Max(0, line.sourceAfter),
                    destinationBefore = line.destinationBefore >= int.MaxValue ? int.MaxValue : Mathf.Max(0, line.destinationBefore),
                    destinationAfter = line.destinationAfter >= int.MaxValue ? int.MaxValue : Mathf.Max(0, line.destinationAfter),
                    sourceIsInfinite = line.sourceBefore >= int.MaxValue || line.sourceAfter >= int.MaxValue,
                    destinationIsInfinite = line.destinationBefore >= int.MaxValue || line.destinationAfter >= int.MaxValue
                });
            }
        }

        return data.TransferCandidateLines.Count > 0;
    }

    private static string ResolveTransferOptionTargetName(PodeTransferirOption option)
    {
        if (option == null)
            return "(invalido)";

        if (option.targetUnit != null)
            return ResolveUnitRuntimeName(option.targetUnit);

        if (option.targetConstruction != null)
            return ResolveConstructionName(option.targetConstruction);

        return "(sem alvo)";
    }

    private void BuildSupplyResourcePreviewLines(HelperPanelData data, UnitManager supplier, List<UnitManager> executionOrder)
    {
        if (data == null || supplier == null || executionOrder == null || executionOrder.Count <= 0)
            return;

        List<ServiceData> services = BuildDistinctServiceList(supplier.GetEmbarkedServices());
        if (services == null || services.Count <= 0)
            return;

        Dictionary<SupplyData, int> initialStock = BuildSupplierStockSnapshot(supplier);
        Dictionary<SupplyData, int> simulatedStock = CloneSupplySnapshot(initialStock);
        int remainingMoney = matchController != null
            ? Mathf.Max(0, matchController.GetActualMoney(supplier.TeamId))
            : int.MaxValue;

        for (int i = 0; i < executionOrder.Count; i++)
        {
            UnitManager target = executionOrder[i];
            if (target == null)
                continue;

            int simulatedHp = Mathf.Clamp(target.CurrentHP, 0, target.GetMaxHP());
            int simulatedFuel = Mathf.Clamp(target.CurrentFuel, 0, target.GetMaxFuel());
            List<int> simulatedAmmoByWeapon = BuildRuntimeAmmoSnapshot(target);

            for (int s = 0; s < services.Count; s++)
            {
                ServiceData service = services[s];
                if (service == null || !service.isService)
                    continue;
                if (service.apenasEntreSupridores && !IsSupplier(target))
                    continue;
                if (!CanServiceApplyByClassAndNeed(target, service))
                    continue;

                Dictionary<SupplyData, int> candidateStock = CloneSupplySnapshot(simulatedStock);
                List<int> candidateSimulatedAmmo = CloneAmmoSnapshot(simulatedAmmoByWeapon);
                List<int> ammoByWeaponGain = new List<int>();
                EstimatePotentialServiceGains(
                    target,
                    service,
                    candidateStock,
                    out int hpGain,
                    out int fuelGain,
                    out int ammoGain,
                    ammoByWeaponGain,
                    simulatedHp,
                    simulatedFuel,
                    candidateSimulatedAmmo);
                if (hpGain <= 0 && fuelGain <= 0 && ammoGain <= 0)
                    continue;

                int finalCost = matchController != null
                    ? matchController.ResolveEconomyCost(ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, ammoByWeaponGain))
                    : Mathf.Max(0, ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, ammoByWeaponGain));
                if (finalCost > remainingMoney)
                    continue;

                OverwriteSupplySnapshot(simulatedStock, candidateStock);
                remainingMoney = Mathf.Max(0, remainingMoney - Mathf.Max(0, finalCost));
                simulatedHp = Mathf.Clamp(simulatedHp + hpGain, 0, target.GetMaxHP());
                simulatedFuel = Mathf.Clamp(simulatedFuel + fuelGain, 0, target.GetMaxFuel());
                simulatedAmmoByWeapon = candidateSimulatedAmmo;
            }
        }

        foreach (KeyValuePair<SupplyData, int> pair in initialStock)
        {
            SupplyData supply = pair.Key;
            if (supply == null)
                continue;

            int before = Mathf.Max(0, pair.Value);
            int after = simulatedStock.TryGetValue(supply, out int simulated) ? Mathf.Max(0, simulated) : 0;
            data.SupplyResourceLines.Add(new HelperSupplyResourceLine
            {
                supplyName = ResolveSupplyDisplayName(supply),
                beforeAmount = before,
                afterAmount = after,
                maxAmount = ResolveSupplierResourceMaxAmount(supplier, supply, before)
            });
        }
    }

    private List<SupplyEstimateLine> EstimateSupplyQueueForHelper(UnitManager supplier, List<UnitManager> executionOrder, UnitManager focusedTarget)
    {
        List<SupplyEstimateLine> lines = new List<SupplyEstimateLine>();
        if (supplier == null || executionOrder == null || executionOrder.Count <= 0)
            return lines;

        List<ServiceData> services = BuildDistinctServiceList(supplier.GetEmbarkedServices());
        if (services == null || services.Count <= 0)
            return lines;

        Dictionary<SupplyData, int> sourceStock = BuildSupplierStockSnapshot(supplier);
        int remainingMoney = matchController != null
            ? Mathf.Max(0, matchController.GetActualMoney(supplier.TeamId))
            : int.MaxValue;

        for (int i = 0; i < executionOrder.Count; i++)
        {
            UnitManager target = executionOrder[i];
            if (target == null)
                continue;

            int hpTotal = 0;
            int fuelTotal = 0;
            int ammoTotal = 0;
            int costTotal = 0;
            int simulatedHp = Mathf.Clamp(target.CurrentHP, 0, target.GetMaxHP());
            int simulatedFuel = Mathf.Clamp(target.CurrentFuel, 0, target.GetMaxFuel());
            List<int> simulatedAmmoByWeapon = BuildRuntimeAmmoSnapshot(target);

            for (int s = 0; s < services.Count; s++)
            {
                ServiceData service = services[s];
                if (service == null || !service.isService)
                    continue;
                if (service.apenasEntreSupridores && !IsSupplier(target))
                    continue;
                if (!CanServiceApplyByClassAndNeed(target, service))
                    continue;

                Dictionary<SupplyData, int> candidateStock = CloneSupplySnapshot(sourceStock);
                List<int> candidateSimulatedAmmo = CloneAmmoSnapshot(simulatedAmmoByWeapon);
                List<int> ammoByWeaponGain = new List<int>();
                EstimatePotentialServiceGains(
                    target,
                    service,
                    candidateStock,
                    out int hpGain,
                    out int fuelGain,
                    out int ammoGain,
                    ammoByWeaponGain,
                    simulatedHp,
                    simulatedFuel,
                    candidateSimulatedAmmo);
                if (hpGain <= 0 && fuelGain <= 0 && ammoGain <= 0)
                    continue;

                int finalCost = matchController != null
                    ? matchController.ResolveEconomyCost(ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, ammoByWeaponGain))
                    : Mathf.Max(0, ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, ammoByWeaponGain));
                if (finalCost > remainingMoney)
                    continue;

                OverwriteSupplySnapshot(sourceStock, candidateStock);
                remainingMoney = Mathf.Max(0, remainingMoney - Mathf.Max(0, finalCost));
                hpTotal += hpGain;
                fuelTotal += fuelGain;
                ammoTotal += ammoGain;
                costTotal += Mathf.Max(0, finalCost);
                simulatedHp = Mathf.Clamp(simulatedHp + hpGain, 0, target.GetMaxHP());
                simulatedFuel = Mathf.Clamp(simulatedFuel + fuelGain, 0, target.GetMaxFuel());
                simulatedAmmoByWeapon = candidateSimulatedAmmo;
            }

            if (hpTotal <= 0 && fuelTotal <= 0 && ammoTotal <= 0 && costTotal <= 0)
                continue;

            lines.Add(new SupplyEstimateLine
            {
                target = target,
                hp = hpTotal,
                fuel = fuelTotal,
                ammo = ammoTotal,
                cost = costTotal,
                isFocused = target == focusedTarget
            });
        }

        return lines;
    }

    private static string FormatSupplyGains(int hp, int fuel, int ammo)
    {
        List<string> segments = new List<string>();
        if (hp > 0)
            segments.Add($"HP +{hp}");
        if (fuel > 0)
            segments.Add($"FUEL +{fuel}");
        if (ammo > 0)
            segments.Add($"AMMO +{ammo}");
        return segments.Count > 0 ? string.Join(" | ", segments) : "-";
    }

    private string BuildUnitStatInline(UnitManager unit)
    {
        if (unit == null)
            return ResolveUnitStatInlineEmpty();

        int hp = Mathf.Max(0, unit.CurrentHP);
        int fuel = Mathf.Max(0, unit.CurrentFuel);
        List<string> segments = new List<string>
        {
            PanelHelperController.ResolveHelperMessage(
                "helper.unit_stats.inline.hp",
                "<value>HP",
                new Dictionary<string, string>
                {
                    { "value", hp.ToString() }
                }),
            PanelHelperController.ResolveHelperMessage(
                "helper.unit_stats.inline.fuel",
                "<value>F",
                new Dictionary<string, string>
                {
                    { "value", fuel.ToString() }
                })
        };

        AppendWeaponStatSegments(segments, unit);
        AppendSupplyStatSegments(segments, unit);
        if (segments.Count <= 0)
            return ResolveUnitStatInlineEmpty();

        string separator = PanelHelperController.ResolveHelperMessage("helper.unit_stats.inline.separator", " | ");
        return string.Join(separator, segments);
    }

    private string BuildUnitStatInlineWithoutSupplies(UnitManager unit)
    {
        if (unit == null)
            return ResolveUnitStatInlineEmpty();

        int hp = Mathf.Max(0, unit.CurrentHP);
        int fuel = Mathf.Max(0, unit.CurrentFuel);
        List<string> segments = new List<string>
        {
            PanelHelperController.ResolveHelperMessage(
                "helper.unit_stats.inline.hp",
                "<value>HP",
                new Dictionary<string, string>
                {
                    { "value", hp.ToString() }
                }),
            PanelHelperController.ResolveHelperMessage(
                "helper.unit_stats.inline.fuel",
                "<value>F",
                new Dictionary<string, string>
                {
                    { "value", fuel.ToString() }
                })
        };

        AppendWeaponStatSegments(segments, unit);
        if (segments.Count <= 0)
            return ResolveUnitStatInlineEmpty();

        string separator = PanelHelperController.ResolveHelperMessage("helper.unit_stats.inline.separator", " | ");
        return string.Join(separator, segments);
    }

    private string BuildUnitSuppliesInline(UnitManager unit)
    {
        if (unit == null)
            return string.Empty;

        List<string> segments = new List<string>();
        AppendSupplyStatSegments(segments, unit);
        if (segments.Count <= 0)
            return string.Empty;

        string separator = PanelHelperController.ResolveHelperMessage("helper.unit_stats.inline.separator", " | ");
        return string.Join(separator, segments);
    }

    private void AppendWeaponStatSegments(List<string> segments, UnitManager unit)
    {
        if (segments == null || unit == null)
            return;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = unit.GetEmbarkedWeapons();
        List<UnitEmbarkedWeapon> baselineWeapons = null;
        if (unit.TryGetUnitData(out UnitData data) && data != null)
            baselineWeapons = data.embarkedWeapons;

        int maxEntries = Mathf.Max(runtimeWeapons != null ? runtimeWeapons.Count : 0, baselineWeapons != null ? baselineWeapons.Count : 0);
        int weaponCounter = 0;
        for (int i = 0; i < maxEntries; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons != null && i < runtimeWeapons.Count ? runtimeWeapons[i] : null;
            UnitEmbarkedWeapon baseline = baselineWeapons != null && i < baselineWeapons.Count ? baselineWeapons[i] : null;
            bool hasWeapon = (runtime != null && runtime.weapon != null) || (baseline != null && baseline.weapon != null);
            if (!hasWeapon)
                continue;

            weaponCounter++;
            int currentAmmo = runtime != null ? Mathf.Max(0, runtime.squadAmmunition) : 0;
            segments.Add(PanelHelperController.ResolveHelperMessage(
                "helper.unit_stats.inline.weapon",
                "W<index>:<value>",
                new Dictionary<string, string>
                {
                    { "index", weaponCounter.ToString() },
                    { "value", currentAmmo.ToString() }
                }));
        }
    }

    private void AppendSupplyStatSegments(List<string> segments, UnitManager unit)
    {
        if (segments == null || unit == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null || resources.Count <= 0)
            return;

        int supplyCounter = 0;
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;

            supplyCounter++;
            int amount = Mathf.Max(0, entry.amount);
            segments.Add(PanelHelperController.ResolveHelperMessage(
                "helper.unit_stats.inline.supply",
                "R<index>:<value>",
                new Dictionary<string, string>
                {
                    { "index", supplyCounter.ToString() },
                    { "value", amount.ToString() }
                }));
        }
    }

    private static string ResolveUnitStatInlineEmpty()
    {
        return PanelHelperController.ResolveHelperMessage(
            "helper.unit_stats.inline.empty",
            "-");
    }

    private string BuildMergePreviewInline(UnitManager candidateOrNull)
    {
        if (selectedUnit == null)
            return string.Empty;

        List<UnitManager> participants = new List<UnitManager>();
        if (mergeQueuedUnits != null)
        {
            for (int i = 0; i < mergeQueuedUnits.Count; i++)
            {
                UnitManager queued = mergeQueuedUnits[i];
                if (queued == null || queued == selectedUnit || participants.Contains(queued))
                    continue;
                participants.Add(queued);
            }
        }

        if (candidateOrNull != null && candidateOrNull != selectedUnit && !participants.Contains(candidateOrNull))
            participants.Add(candidateOrNull);

        if (participants.Count <= 0)
            return string.Empty;

        int baseHp = Mathf.Max(0, selectedUnit.CurrentHP);
        int baseAutonomy = Mathf.Max(0, selectedUnit.CurrentFuel);
        int baseSteps = baseHp * baseAutonomy;

        int participantsHp = 0;
        int participantsSteps = 0;
        for (int i = 0; i < participants.Count; i++)
        {
            UnitManager participant = participants[i];
            if (participant == null)
                continue;

            int hp = Mathf.Max(0, participant.CurrentHP);
            int autonomy = Mathf.Max(0, participant.CurrentFuel);
            participantsHp += hp;
            participantsSteps += hp * autonomy;
        }

        int resultHp = Mathf.Min(10, baseHp + participantsHp);
        int totalSteps = baseSteps + participantsSteps;
        int resultAutonomy = resultHp > 0 ? Mathf.Max(0, totalSteps / resultHp) : 0;

        Dictionary<WeaponData, int> projectilesByWeapon = BuildMergeWeaponProjectileTotals(selectedUnit, participants);
        Dictionary<SupplyData, int> supplyStepsByType = BuildMergeSupplyStepTotals(selectedUnit, participants);

        List<string> segments = new List<string>
        {
            $"{resultHp}HP",
            $"{resultAutonomy}F"
        };

        AppendMergeResultWeaponSegments(segments, selectedUnit, resultHp, projectilesByWeapon);
        AppendMergeResultSupplySegments(segments, selectedUnit, resultHp, supplyStepsByType);
        return string.Join(" | ", segments);
    }

    private static void AppendMergeResultWeaponSegments(
        List<string> segments,
        UnitManager baseUnit,
        int resultHp,
        Dictionary<WeaponData, int> projectilesByWeapon)
    {
        if (segments == null || baseUnit == null || projectilesByWeapon == null)
            return;

        IReadOnlyList<UnitEmbarkedWeapon> baseWeapons = baseUnit.GetEmbarkedWeapons();
        if (baseWeapons == null)
            return;

        int weaponCounter = 0;
        for (int i = 0; i < baseWeapons.Count; i++)
        {
            UnitEmbarkedWeapon runtime = baseWeapons[i];
            if (runtime == null || runtime.weapon == null)
                continue;

            weaponCounter++;
            int projectedAmmo = 0;
            if (projectilesByWeapon.TryGetValue(runtime.weapon, out int totalProjectiles) && resultHp > 0)
                projectedAmmo = Mathf.Max(0, totalProjectiles / resultHp);
            segments.Add($"W{weaponCounter}:{projectedAmmo}");
        }
    }

    private static void AppendMergeResultSupplySegments(
        List<string> segments,
        UnitManager baseUnit,
        int resultHp,
        Dictionary<SupplyData, int> supplyStepsByType)
    {
        if (segments == null || baseUnit == null || supplyStepsByType == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> baseSupplies = baseUnit.GetEmbarkedResources();
        if (baseSupplies == null)
            return;

        int supplyCounter = 0;
        for (int i = 0; i < baseSupplies.Count; i++)
        {
            UnitEmbarkedSupply runtime = baseSupplies[i];
            if (runtime == null || runtime.supply == null)
                continue;

            supplyCounter++;
            int projectedAmount = 0;
            if (supplyStepsByType.TryGetValue(runtime.supply, out int totalSteps) && resultHp > 0)
                projectedAmount = Mathf.Max(0, totalSteps / resultHp);
            segments.Add($"R{supplyCounter}:{projectedAmount}");
        }
    }

    private string ResolveTerrainLabelForCell(Vector3Int cell)
    {
        Tilemap map = terrainTilemap;
        if (map == null && selectedUnit != null)
            map = selectedUnit.BoardTilemap;

        if (TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) && terrain != null)
            return ResolveTerrainName(terrain);

        return FormatMapCellWithZ(cell);
    }
}
