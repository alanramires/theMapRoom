using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
        MoveuParado = 3
    }

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private PathManager pathManager;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private Tilemap rangeMapTilemap;
    [SerializeField] private Tilemap terrainTilemap;
    [SerializeField] private TileBase rangeOverlayTile;

    [Header("State")]
    [SerializeField] private CursorState cursorState = CursorState.Neutral;
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] [Range(0.05f, 1f)] private float movementRangeAlpha = 0.6f;

    private readonly List<Vector3Int> paintedRangeCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> paintedRangeLookup = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, List<Vector3Int>> movementPathsByCell = new Dictionary<Vector3Int, List<Vector3Int>>();
    private readonly List<Vector3Int> committedMovementPath = new List<Vector3Int>();
    private Vector3Int committedOriginCell;
    private Vector3Int committedDestinationCell;
    private bool hasCommittedMovement;
    private int preparedFuelCost;
    private bool hasPreparedFuelCost;

    public CursorState CurrentCursorState => cursorState;
    public UnitManager SelectedUnit => selectedUnit;

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

        if (keepPreparedFuelCost)
            CommitPreparedFuelCost();
        else
            RestorePreparedFuelCostIfAny();

        if (selectedUnit != null)
            animationManager?.ClearSelectionVisual(selectedUnit);

        selectedUnit = null;
        cursorState = CursorState.Neutral;
        ClearMovementRange();
        ClearCommittedMovement();
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

        if (terrainTilemap == null && cursorController != null)
            terrainTilemap = cursorController.BoardTilemap;

        if (rangeMapTilemap == null)
            rangeMapTilemap = FindRangeMapTilemap();
    }

    private void ClearCommittedMovement()
    {
        committedMovementPath.Clear();
        committedOriginCell = Vector3Int.zero;
        committedDestinationCell = Vector3Int.zero;
        hasCommittedMovement = false;
        ClearCommittedPathVisual();
    }

    private int GetAvailableMovementSteps(UnitManager unit)
    {
        if (unit == null)
            return 0;

        int moveRange = Mathf.Max(0, unit.GetMovementRange());
        int fuel = Mathf.Max(0, unit.CurrentFuel);
        return Mathf.Min(moveRange, fuel);
    }

    private void PrepareFuelCostForCommittedPath()
    {
        int movementCost = Mathf.Max(0, committedMovementPath.Count - 1);
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
        return animationManager != null && animationManager.IsAnimatingMovement;
    }
}
