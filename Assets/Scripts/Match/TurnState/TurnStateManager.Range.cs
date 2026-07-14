using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private struct MovementRangeCacheKey : System.IEquatable<MovementRangeCacheKey>
    {
        public int unitInstanceId;
        public int remainingMovementPoints;
        public int currentFuel;
        public int globalBoardRevision;
        public Vector3Int unitCell;
        public Domain domain;
        public HeightLevel height;

        public bool Equals(MovementRangeCacheKey other)
        {
            return unitInstanceId == other.unitInstanceId &&
                   remainingMovementPoints == other.remainingMovementPoints &&
                   currentFuel == other.currentFuel &&
                   globalBoardRevision == other.globalBoardRevision &&
                   unitCell == other.unitCell &&
                   domain == other.domain &&
                   height == other.height;
        }

        public override bool Equals(object obj)
        {
            return obj is MovementRangeCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + unitInstanceId;
                hash = hash * 31 + remainingMovementPoints;
                hash = hash * 31 + currentFuel;
                hash = hash * 31 + globalBoardRevision;
                hash = hash * 31 + unitCell.GetHashCode();
                hash = hash * 31 + (int)domain;
                hash = hash * 31 + (int)height;
                return hash;
            }
        }
    }

    private MovementRangeCacheKey? movementRangeCacheKey;
    private Dictionary<Vector3Int, List<Vector3Int>> movementRangeCache;
    private readonly Dictionary<MovementRangeCacheKey, Dictionary<Vector3Int, List<Vector3Int>>> movementRangeCacheByUnit =
        new Dictionary<MovementRangeCacheKey, Dictionary<Vector3Int, List<Vector3Int>>>();
    private int movementRangeCacheBoardRevision = int.MinValue;
    private const int MaxMovementRangeCacheEntries = 16;

    public void ApplyMovementRangeFogOfWarSorting(bool playerTurn)
    {
        if (rangeMapTilemap == null)
            rangeMapTilemap = FindRangeMapTilemap();

        TilemapRenderer renderer = rangeMapTilemap != null
            ? rangeMapTilemap.GetComponent<TilemapRenderer>()
            : null;
        if (renderer == null)
            return;

        renderer.sortingLayerName = playerTurn ? "FogOfWar" : "SFX";
        // Acima da nevoa no turno humano, mas abaixo do cursor (ordem 100).
        renderer.sortingOrder = playerTurn ? 50 : 0;
    }

    private void PaintSelectedUnitMovementRange()
    {
        double perfStart = Time.realtimeSinceStartupAsDouble;
        try
        {
            ClearMovementRangeVisualOnly(keepCommittedMovement: true);
            if (selectedUnit == null)
                return;

            if (rangeMapTilemap == null || terrainTilemap == null)
                return;
            if (rangeOverlayTile == null)
            {
                Debug.LogWarning("[TurnStateManager] Defina 'Range Overlay Tile' (hex simples) para pintar o alcance.");
                return;
            }

            int radius = GetAvailableMovementSteps(selectedUnit);
            if (radius < 0)
                return;

            MovementRangeCacheKey cacheKey = new MovementRangeCacheKey
            {
                unitInstanceId = selectedUnit.GetEntityId().GetHashCode(),
                remainingMovementPoints = Mathf.Max(0, selectedUnit.RemainingMovementPoints),
                currentFuel = Mathf.Max(0, selectedUnit.CurrentFuel),
                globalBoardRevision = ThreatRevisionTracker.GlobalBoardRevision,
                unitCell = selectedUnit.CurrentCellPosition,
                domain = selectedUnit.GetDomain(),
                height = selectedUnit.GetHeightLevel()
            };
            cacheKey.unitCell.z = 0;

            if (movementRangeCacheBoardRevision != cacheKey.globalBoardRevision)
            {
                movementRangeCacheByUnit.Clear();
                movementRangeCacheBoardRevision = cacheKey.globalBoardRevision;
            }

            if (movementRangeCacheByUnit.TryGetValue(cacheKey, out Dictionary<Vector3Int, List<Vector3Int>> cachedPaths))
            {
                movementRangeCacheKey = cacheKey;
                movementRangeCache = cachedPaths;
                ApplyMovementRangePaint(cachedPaths);
                return;
            }

            if (movementRangeCacheKey.HasValue &&
                movementRangeCache != null &&
                movementRangeCacheKey.Value.Equals(cacheKey))
            {
                if (enableRangeCacheDebugLogs && Application.isPlaying)
                {
                    Debug.Log(
                        $"[RangeCache] HIT unit={selectedUnit.name} " +
                        $"mp={cacheKey.remainingMovementPoints} fuel={cacheKey.currentFuel} rev={cacheKey.globalBoardRevision}");
                }
                ApplyMovementRangePaint(movementRangeCache);
                return;
            }

            if (enableRangeCacheDebugLogs && Application.isPlaying)
            {
                if (!movementRangeCacheKey.HasValue || movementRangeCache == null)
                {
                    string reason = !movementRangeCacheKey.HasValue
                        ? "empty key"
                        : "cache storage missing";
                    Debug.Log(
                        $"[RangeCache] MISS - reason: {reason} | " +
                        $"unit={selectedUnit.name} unitId={cacheKey.unitInstanceId} " +
                        $"mp={cacheKey.remainingMovementPoints} fuel={cacheKey.currentFuel} rev={cacheKey.globalBoardRevision}");
                }
                else
                {
                    MovementRangeCacheKey previousKey = movementRangeCacheKey.Value;
                    if (previousKey.globalBoardRevision != cacheKey.globalBoardRevision)
                    {
                        Debug.Log(
                            $"[RangeCache] MISS - reason: boardRevision changed " +
                            $"(was {previousKey.globalBoardRevision}, now {cacheKey.globalBoardRevision})");
                    }

                    if (previousKey.unitInstanceId != cacheKey.unitInstanceId)
                    {
                        Debug.Log(
                            $"[RangeCache] MISS - reason: unitId changed " +
                            $"(was {previousKey.unitInstanceId}, now {cacheKey.unitInstanceId})");
                    }

                    if (previousKey.currentFuel != cacheKey.currentFuel)
                    {
                        Debug.Log(
                            $"[RangeCache] MISS - reason: fuel changed " +
                            $"(was {previousKey.currentFuel}, now {cacheKey.currentFuel})");
                    }

                    if (previousKey.remainingMovementPoints != cacheKey.remainingMovementPoints)
                    {
                        Debug.Log(
                            $"[RangeCache] MISS - reason: remainingMovementPoints changed " +
                            $"(was {previousKey.remainingMovementPoints}, now {cacheKey.remainingMovementPoints})");
                    }
                }
            }

            Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
                terrainTilemap,
                selectedUnit,
                radius,
                terrainDatabase);

            movementRangeCacheKey = cacheKey;
            movementRangeCache = validPaths;
            if (movementRangeCacheByUnit.Count >= MaxMovementRangeCacheEntries)
                movementRangeCacheByUnit.Clear();
            movementRangeCacheByUnit[cacheKey] = validPaths;
            ApplyMovementRangePaint(validPaths);
        }
        finally
        {
            RegisterPerfRangeDuration((Time.realtimeSinceStartupAsDouble - perfStart) * 1000d);
        }
    }

    private void ClearMovementRange()
    {
        movementRangeCacheKey = null;
        ClearMovementRange(keepCommittedMovement: false);
    }

    private void ClearMovementRange(bool keepCommittedMovement)
    {
        movementRangeCacheKey = null;
        ClearMovementRangeVisualOnly(keepCommittedMovement);
    }

    private void ClearMovementRangeVisualOnly(bool keepCommittedMovement)
    {
        if (rangeMapTilemap != null && paintedRangeCells.Count > 0)
        {
            int count = paintedRangeCells.Count;
            Vector3Int[] clearPositions = new Vector3Int[count];
            TileBase[] clearTiles = new TileBase[count]; // null tiles
            for (int i = 0; i < count; i++)
                clearPositions[i] = paintedRangeCells[i];

            // Batch clear to reduce per-cell Tilemap refresh overhead.
            rangeMapTilemap.SetTiles(clearPositions, clearTiles);
        }

        paintedRangeCells.Clear();
        paintedRangeLookup.Clear();
        movementPathsByCell.Clear();

        if (!keepCommittedMovement)
            ClearCommittedMovement();
    }

    private void ApplyMovementRangePaint(Dictionary<Vector3Int, List<Vector3Int>> pathsByDestination)
    {
        if (selectedUnit == null || rangeMapTilemap == null || terrainTilemap == null || pathsByDestination == null)
            return;

        Color teamColor = TeamUtils.GetColor(selectedUnit.TeamId);
        Color overlayColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(movementRangeAlpha));
        List<Vector3Int> paintCells = new List<Vector3Int>(pathsByDestination.Count);
        Dictionary<Vector3Int, List<UnitManager>> occupantsByCell = OccupancyResolver.IsLayerAwareRulesActive
            ? BuildRangeOccupancyIndex(selectedUnit)
            : null;

        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in pathsByDestination)
        {
            Vector3Int cell = pair.Key;
            if (terrainTilemap.GetTile(cell) == null)
                continue;
            if (!IsRangeCellAllowedByTakeoffOptions(cell, pair.Value))
                continue;
            if (OccupancyResolver.IsLayerAwareRulesActive)
            {
                List<string> occupantDebug = null;
                if (occupantsByCell == null || !occupantsByCell.TryGetValue(cell, out List<UnitManager> occupants))
                    occupants = null;
                if (occupants != null)
                {
                    for (int occupantIndex = 0; occupantIndex < occupants.Count; occupantIndex++)
                    {
                        UnitManager occupant = occupants[occupantIndex];
                        if (occupant == null)
                            continue;
                        if (PathManager.IsPathfindingDebugLogsEnabled && Application.isPlaying)
                        {
                            if (occupantDebug == null)
                                occupantDebug = new List<string>(2);
                            occupantDebug.Add($"{occupant.name}[team={(int)occupant.TeamId},band={OccupancyResolver.GetHeightBand(occupant)}]");
                        }
                    }
                }

                if (!CanPaintMovementStopAtCell(selectedUnit, cell, occupants))
                {
                    if (PathManager.IsPathfindingDebugLogsEnabled && Application.isPlaying)
                    {
                        string info = (occupantDebug != null && occupantDebug.Count > 0)
                            ? string.Join(", ", occupantDebug)
                            : "none";
                        Debug.Log(
                            $"[RangePaint][EndMoveBlocked] cell=({cell.x},{cell.y},{cell.z}) " +
                            $"mover={selectedUnit.name}[team={(int)selectedUnit.TeamId},band={OccupancyResolver.GetHeightBand(selectedUnit)}] " +
                            $"occupants={info}");
                    }

                    // Cannot stop here, but paint and keep navigable for cursor passthrough.
                    // Confirmation will be blocked by CanEndMove / FindUnitAtCell.
                    paintCells.Add(cell);
                    paintedRangeCells.Add(cell);
                    paintedRangeLookup.Add(cell);
                    movementPathsByCell[cell] = pair.Value;
                    continue;
                }
            }

            paintCells.Add(cell);
            paintedRangeCells.Add(cell);
            paintedRangeLookup.Add(cell);
            movementPathsByCell[cell] = pair.Value;
        }

        int paintCount = paintCells.Count;
        if (paintCount <= 0)
            return;

        Vector3Int[] paintPositions = new Vector3Int[paintCount];
        TileBase[] paintTiles = new TileBase[paintCount];
        for (int i = 0; i < paintCount; i++)
        {
            paintPositions[i] = paintCells[i];
            paintTiles[i] = rangeOverlayTile;
        }

        // Batch paint to reduce per-cell Tilemap refresh overhead.
        rangeMapTilemap.SetTiles(paintPositions, paintTiles);

        // SetTileFlags/SetColor are still applied per cell; no native batch API for both.
        for (int i = 0; i < paintCount; i++)
        {
            Vector3Int cell = paintPositions[i];
            rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
            rangeMapTilemap.SetColor(cell, overlayColor);
        }
    }

    private Dictionary<Vector3Int, List<UnitManager>> BuildRangeOccupancyIndex(UnitManager exceptUnit)
    {
        Dictionary<Vector3Int, List<UnitManager>> result = new Dictionary<Vector3Int, List<UnitManager>>();
        List<UnitManager> units = UnitManager.AllActive;
        if (units == null || terrainTilemap == null)
            return result;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked || unit.IsDead)
                continue;
            if (unit.BoardTilemap != terrainTilemap || unit.gameObject.scene != terrainTilemap.gameObject.scene)
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            if (!result.TryGetValue(cell, out List<UnitManager> occupants))
            {
                occupants = new List<UnitManager>(1);
                result[cell] = occupants;
            }
            occupants.Add(unit);
        }

        return result;
    }

    private static bool CanPaintMovementStopAtCell(UnitManager mover, Vector3Int cell, IEnumerable<UnitManager> occupants)
    {
        if (mover == null)
            return false;

        if (!OccupancyResolver.IsLayerAwareRulesActive)
            return OccupancyResolver.CanEndMove(mover, cell, occupants);

        HeightBand moverBand = OccupancyResolver.GetHeightBand(mover);
        if (moverBand != HeightBand.Blocking)
            return true;

        if (occupants == null)
            return true;

        foreach (UnitManager occupant in occupants)
        {
            if (occupant == null || occupant == mover || occupant.IsDead || occupant.IsEmbarked)
                continue;
            if (OccupancyResolver.GetHeightBand(occupant) == moverBand)
                return false;
        }

        return true;
    }

    private Tilemap FindRangeMapTilemap()
    {
        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map != null && map.name.ToLowerInvariant() == "rangemap")
                return map;
        }

        return null;
    }

}
