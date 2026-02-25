using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class TurnStateManager : MonoBehaviour
{
    public enum ActionSfx
    {
        None = 0,
        Confirm = 1,
        Cancel = 2,
        Error = 3
    }

    public enum CursorState
    {
        Neutral = 0,
        UnitSelected = 1,
        MoveuAndando = 2,
        MoveuParado = 3,
        Capturando = 4,
        Mirando = 5,
        Pousando = 6,
        Embarcando = 7,
        Desembarcando = 8,
        ShoppingAndServices = 9
    }

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private PathManager pathManager;
    [SerializeField] private UnitSpawner unitSpawner;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private WeaponPriorityData weaponPriorityData;
    [SerializeField] private DPQMatchupDatabase dpqMatchupDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private RPSDatabase rpsDatabase;
    [SerializeField] private Tilemap rangeMapTilemap;
    [SerializeField] private Tilemap lineOfFireMapTilemap;
    [SerializeField] private Tilemap terrainTilemap;
    [SerializeField] private TileBase rangeOverlayTile;
    [SerializeField] private TileBase lineOfFireOverlayTile;
    [Header("Combat Audio")]
    [SerializeField] private AudioClip meleeAttackSfx;
    [SerializeField] private AudioClip rangedAttackSfx;
    [SerializeField] [Range(0f, 1f)] private float combatSfxVolume = 1f;
    [Header("State Audio")]
    [SerializeField] private AudioSource stateAudioSource;
    [SerializeField] private AudioClip capturingSfx;
    [SerializeField] private AudioClip capturedSfx;
    [SerializeField] [Range(0f, 1f)] private float stateSfxVolume = 1f;

    [Header("State")]
    [SerializeField] private CursorState cursorState = CursorState.Neutral;
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] [Range(0.05f, 1f)] private float movementRangeAlpha = 0.6f;
    [SerializeField] [Range(0.05f, 1f)] private float lineOfFireAlpha = 0.45f;

    private readonly List<Vector3Int> paintedRangeCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> paintedRangeLookup = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> paintedLineOfFireCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> paintedLineOfFireLookup = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, List<Vector3Int>> movementPathsByCell = new Dictionary<Vector3Int, List<Vector3Int>>();
    private AircraftOperationDecision cachedAircraftOperationDecision;
    private readonly List<Vector3Int> committedMovementPath = new List<Vector3Int>();
    private Vector3Int committedOriginCell;
    private Vector3Int committedDestinationCell;
    private bool hasCommittedMovement;
    private int preparedFuelCost;
    private bool hasPreparedFuelCost;
    private bool hasTemporaryTakeoffSelectionState;
    private UnitManager temporaryTakeoffUnit;
    private Domain temporaryTakeoffOriginalDomain = Domain.Land;
    private HeightLevel temporaryTakeoffOriginalHeight = HeightLevel.Surface;
    private bool temporaryTakeoffOriginalGrounded = true;
    private bool temporaryTakeoffOriginalEmbarkedInCarrier;
    private readonly List<int> temporaryTakeoffMoveOptions = new List<int>();
    private bool hasAutoPromotionEntryLayer;
    private UnitManager autoPromotionUnit;
    private Domain autoPromotionEntryDomain = Domain.Land;
    private HeightLevel autoPromotionEntryHeight = HeightLevel.Surface;
    private bool hasForcedLayerRollbackSnapshot;
    private Domain forcedLayerRollbackDomain = Domain.Land;
    private HeightLevel forcedLayerRollbackHeight = HeightLevel.Surface;
    private ConstructionManager shoppingConstruction;
    private readonly List<UnitData> shoppingUnitsForSale = new List<UnitData>();
    private bool captureExecutionInProgress;

    public CursorState CurrentCursorState => cursorState;
    public UnitManager SelectedUnit => selectedUnit;

    private void LogStateStep(string step, bool rollback = false)
    {
        string rollbackTag = rollback ? " [roll back]" : string.Empty;
        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        Debug.Log($"[TurnState]{rollbackTag} state={cursorState} | step={step} | selected={selectedName}");
    }

    private void SetCursorState(CursorState nextState, string reason, bool rollback = false)
    {
        CursorState previous = cursorState;
        cursorState = nextState;

        string rollbackTag = rollback ? " [roll back]" : string.Empty;
        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        Debug.Log($"[TurnState]{rollbackTag} transition={previous} -> {nextState} | reason={reason} | selected={selectedName}");
    }

    public bool TryFinalizeSelectedUnitActionFromDebug()
    {
        if (selectedUnit == null)
            return false;

        CommitPreparedFuelCost();
        selectedUnit.MarkAsActed();
        ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: true);
        return true;
    }

    public bool TryGetSelectedUnitPath(Vector3Int destinationCell, out List<Vector3Int> path)
    {
        destinationCell.z = 0;
        if (movementPathsByCell.TryGetValue(destinationCell, out List<Vector3Int> storedPath))
        {
            path = new List<Vector3Int>(storedPath);
            return true;
        }

        path = null;
        return false;
    }

    public bool TryGetCommittedMovementPath(out List<Vector3Int> path, out Vector3Int originCell, out Vector3Int destinationCell)
    {
        if (!hasCommittedMovement || committedMovementPath.Count < 2)
        {
            path = null;
            originCell = Vector3Int.zero;
            destinationCell = Vector3Int.zero;
            return false;
        }

        path = new List<Vector3Int>(committedMovementPath);
        originCell = committedOriginCell;
        destinationCell = committedDestinationCell;
        return true;
    }

    private void Awake()
    {
        TryAutoAssignReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
    }
#endif

    public void ForceNeutral()
    {
        ClearSelectionAndReturnToNeutral();
    }

    private void SetSelectedUnit(UnitManager unit)
    {
        if (selectedUnit == unit)
            return;

        ClearSensorResults();

        if (selectedUnit != null)
            animationManager?.ClearSelectionVisual(selectedUnit);

        selectedUnit = unit;
        if (selectedUnit != null)
        {
            animationManager?.ApplySelectionVisual(selectedUnit);
            PaintSelectedUnitMovementRange();
        }
    }

    private void ClearSelectionAndReturnToNeutral(bool keepPreparedFuelCost = false)
    {
        animationManager?.StopCurrentMovement();
        ClearCommittedPathVisual();
        ClearSensorResults();
        shoppingConstruction = null;
        shoppingUnitsForSale.Clear();

        if (keepPreparedFuelCost)
            CommitPreparedFuelCost();
        else
            RestorePreparedFuelCostIfAny();

        if (selectedUnit != null)
            animationManager?.ClearSelectionVisual(selectedUnit);

        if (keepPreparedFuelCost)
            CommitTemporaryTakeoffSelectionState();
        else
            RestoreTemporaryTakeoffSelectionStateIfAny();

        selectedUnit = null;
        SetCursorState(CursorState.Neutral, "ClearSelectionAndReturnToNeutral", rollback: !keepPreparedFuelCost);
        ClearMovementRange();
        ClearCommittedMovement();
    }

    private void CommitTemporaryTakeoffSelectionState()
    {
        hasTemporaryTakeoffSelectionState = false;
        temporaryTakeoffUnit = null;
        temporaryTakeoffMoveOptions.Clear();
        hasAutoPromotionEntryLayer = false;
        autoPromotionUnit = null;
    }

    private void RestoreTemporaryTakeoffSelectionStateIfAny()
    {
        if (hasTemporaryTakeoffSelectionState && temporaryTakeoffUnit != null)
        {
            temporaryTakeoffUnit.TrySetCurrentLayerMode(temporaryTakeoffOriginalDomain, temporaryTakeoffOriginalHeight);
            temporaryTakeoffUnit.SetAircraftGrounded(temporaryTakeoffOriginalGrounded);
            temporaryTakeoffUnit.SetAircraftEmbarkedInCarrier(temporaryTakeoffOriginalEmbarkedInCarrier);
        }

        if (hasAutoPromotionEntryLayer && autoPromotionUnit != null)
            autoPromotionUnit.TrySetCurrentLayerMode(autoPromotionEntryDomain, autoPromotionEntryHeight);

        hasTemporaryTakeoffSelectionState = false;
        temporaryTakeoffUnit = null;
        temporaryTakeoffMoveOptions.Clear();
        hasAutoPromotionEntryLayer = false;
        autoPromotionUnit = null;
    }

    private bool TryPrepareTemporaryTakeoffStateForSelection(UnitManager unit, out string reason)
    {
        reason = string.Empty;
        CommitTemporaryTakeoffSelectionState();

        if (unit == null)
            return false;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : unit.BoardTilemap;
        PodeDecolarReport takeoffReport = PodeDecolarSensor.Evaluate(unit, boardMap, terrainDatabase);
        if (takeoffReport == null || takeoffReport.takeoffMoveOptions == null || takeoffReport.takeoffMoveOptions.Count == 0)
        {
            reason = takeoffReport != null ? takeoffReport.explicacao : string.Empty;
            return false;
        }

        if (takeoffReport.takeoffMoveOptions.Count == 1 && takeoffReport.takeoffMoveOptions[0] == -1)
        {
            TryPromoteAirborneUnitToPreferredHeight(unit, out reason);
            return false;
        }

        if (!takeoffReport.status)
        {
            reason = takeoffReport.explicacao;
            return false;
        }

        Domain originalDomain = unit.GetDomain();
        HeightLevel originalHeight = unit.GetHeightLevel();
        bool originalGrounded = unit.IsAircraftGrounded;
        bool originalEmbarkedInCarrier = unit.IsAircraftEmbarkedInCarrier;

        bool fullMoveTakeoff = takeoffReport.takeoffMoveOptions.Contains(9);
        HeightLevel targetHeight = fullMoveTakeoff ? unit.GetPreferredAirHeight() : HeightLevel.AirLow;
        if (!unit.TrySetCurrentLayerMode(Domain.Air, targetHeight))
        {
            reason = "Falha ao aplicar decolagem temporaria para selecao.";
            return false;
        }

        temporaryTakeoffUnit = unit;
        temporaryTakeoffOriginalDomain = originalDomain;
        temporaryTakeoffOriginalHeight = originalHeight;
        temporaryTakeoffOriginalGrounded = originalGrounded;
        temporaryTakeoffOriginalEmbarkedInCarrier = originalEmbarkedInCarrier;
        hasTemporaryTakeoffSelectionState = true;
        temporaryTakeoffMoveOptions.Clear();
        temporaryTakeoffMoveOptions.AddRange(takeoffReport.takeoffMoveOptions);

        unit.SetAircraftGrounded(false);
        unit.SetAircraftEmbarkedInCarrier(false);
        reason = takeoffReport.explicacao;
        return true;
    }

    private bool TryPromoteAirborneUnitToPreferredHeight(UnitManager unit, out string info)
    {
        info = string.Empty;
        if (unit == null)
            return false;
        if (unit.GetDomain() != Domain.Air || unit.IsAircraftGrounded)
            return false;

        Domain startDomain = unit.GetDomain();
        HeightLevel startHeight = unit.GetHeightLevel();
        HeightLevel preferred = unit.GetPreferredAirHeight();
        if (startHeight == preferred)
        {
            info = "Aeronave em voo ja esta na altitude nativa.";
            return false;
        }

        if (!unit.TrySetCurrentLayerMode(Domain.Air, preferred))
            return false;

        hasAutoPromotionEntryLayer = true;
        autoPromotionUnit = unit;
        autoPromotionEntryDomain = startDomain;
        autoPromotionEntryHeight = startHeight;
        info = $"Aeronave em voo ajustada para altitude nativa ({preferred}).";
        return true;
    }

    private bool IsTakeoffMoveDistanceAllowed(int movementSteps)
    {
        if (!hasTemporaryTakeoffSelectionState || temporaryTakeoffMoveOptions.Count == 0)
            return true;

        movementSteps = Mathf.Max(0, movementSteps);
        if (temporaryTakeoffMoveOptions.Contains(9))
            return movementSteps == 0 || movementSteps >= 1;

        return temporaryTakeoffMoveOptions.Contains(movementSteps);
    }

    private bool IsRangeCellAllowedByTakeoffOptions(Vector3Int cell, IReadOnlyList<Vector3Int> path)
    {
        if (!hasTemporaryTakeoffSelectionState || temporaryTakeoffMoveOptions.Count == 0 || selectedUnit == null)
            return true;

        if (temporaryTakeoffMoveOptions.Contains(-1) || temporaryTakeoffMoveOptions.Contains(9))
            return true;

        int movementHexes = (path != null && path.Count > 0) ? Mathf.Max(0, path.Count - 1) : 0;
        if (temporaryTakeoffMoveOptions.Contains(0) && temporaryTakeoffMoveOptions.Contains(1))
            return movementHexes <= 1;
        if (temporaryTakeoffMoveOptions.Contains(0))
            return movementHexes == 0;
        if (temporaryTakeoffMoveOptions.Contains(1))
            return movementHexes == 1;

        return true;
    }

    private bool TryGetAutoPromotionEntryLayer(out Domain domain, out HeightLevel height)
    {
        domain = autoPromotionEntryDomain;
        height = autoPromotionEntryHeight;
        return hasAutoPromotionEntryLayer;
    }

    private void TryAutoAssignReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();

        if (animationManager == null)
            animationManager = FindAnyObjectByType<AnimationManager>();

        if (pathManager == null)
            pathManager = FindAnyObjectByType<PathManager>();
        if (pathManager == null)
        {
            GameObject go = new GameObject("Path Manager");
            pathManager = go.AddComponent<PathManager>();
        }

        if (unitSpawner == null)
            unitSpawner = FindAnyObjectByType<UnitSpawner>();

        if (terrainTilemap == null && cursorController != null)
            terrainTilemap = cursorController.BoardTilemap;

        if (rangeMapTilemap == null)
            rangeMapTilemap = FindRangeMapTilemap();

        if (lineOfFireMapTilemap == null)
            lineOfFireMapTilemap = FindLineOfFireMapTilemap();

        if (stateAudioSource == null)
            stateAudioSource = GetComponent<AudioSource>();
        if (stateAudioSource == null)
            stateAudioSource = gameObject.AddComponent<AudioSource>();
        stateAudioSource.playOnAwake = false;
        stateAudioSource.loop = false;
        stateAudioSource.spatialBlend = 0f;
        stateAudioSource.volume = Mathf.Clamp01(stateSfxVolume);

#if UNITY_EDITOR
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAssetEditor<DPQAirHeightConfig>();
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAssetEditor<TerrainDatabase>();
        if (weaponPriorityData == null)
            weaponPriorityData = FindFirstAssetEditor<WeaponPriorityData>();
        if (dpqMatchupDatabase == null)
            dpqMatchupDatabase = FindFirstAssetEditor<DPQMatchupDatabase>();
        if (rpsDatabase == null)
            rpsDatabase = FindFirstAssetEditor<RPSDatabase>();

        if (meleeAttackSfx == null)
            meleeAttackSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/combat/melee attack.mp3");
        if (rangedAttackSfx == null)
            rangedAttackSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/combat/ranged attack.mp3");
        if (capturingSfx == null)
            capturingSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/state/capturing.MP3");
        if (capturedSfx == null)
            capturedSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/state/captured.MP3");
#endif
    }

    private void ClearCommittedMovement()
    {
        committedMovementPath.Clear();
        committedOriginCell = Vector3Int.zero;
        committedDestinationCell = Vector3Int.zero;
        hasCommittedMovement = false;
        hasForcedLayerRollbackSnapshot = false;
        ClearCommittedPathVisual();
    }

#if UNITY_EDITOR
    private static T FindFirstAssetEditor<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }
#endif

    private int GetAvailableMovementSteps(UnitManager unit)
    {
        if (unit == null)
            return 0;

        int moveRange = Mathf.Max(0, unit.GetMovementRange());
        return moveRange;
    }

    private void PrepareFuelCostForCommittedPath()
    {
        Tilemap movementTilemap = terrainTilemap != null ? terrainTilemap : (selectedUnit != null ? selectedUnit.BoardTilemap : null);
        int movementCost = UnitMovementPathRules.CalculateAutonomyCostForPath(
            movementTilemap,
            selectedUnit,
            committedMovementPath,
            terrainDatabase);
        ApplyPreparedFuelCost(movementCost);
    }

    private void ApplyPreparedFuelCost(int movementCost)
    {
        if (selectedUnit == null)
        {
            preparedFuelCost = 0;
            hasPreparedFuelCost = false;
            return;
        }

        RestorePreparedFuelCostIfAny();

        int clampedCost = Mathf.Clamp(movementCost, 0, selectedUnit.CurrentFuel);
        if (clampedCost <= 0)
        {
            preparedFuelCost = 0;
            hasPreparedFuelCost = false;
            return;
        }

        selectedUnit.SetCurrentFuel(selectedUnit.CurrentFuel - clampedCost);
        preparedFuelCost = clampedCost;
        hasPreparedFuelCost = true;
    }

    private void RestorePreparedFuelCostIfAny()
    {
        if (!hasPreparedFuelCost || preparedFuelCost <= 0 || selectedUnit == null)
        {
            preparedFuelCost = 0;
            hasPreparedFuelCost = false;
            return;
        }

        selectedUnit.SetCurrentFuel(selectedUnit.CurrentFuel + preparedFuelCost);
        preparedFuelCost = 0;
        hasPreparedFuelCost = false;
    }

    private void CommitPreparedFuelCost()
    {
        preparedFuelCost = 0;
        hasPreparedFuelCost = false;
    }

    private bool IsMovementAnimationRunning()
    {
        return (animationManager != null && animationManager.IsAnimatingMovement) || embarkExecutionInProgress || disembarkExecutionInProgress;
    }
}
