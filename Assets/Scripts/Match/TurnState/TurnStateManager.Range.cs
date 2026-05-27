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

        public bool Equals(MovementRangeCacheKey other)
        {
            return unitInstanceId == other.unitInstanceId &&
                   remainingMovementPoints == other.remainingMovementPoints &&
                   currentFuel == other.currentFuel &&
                   globalBoardRevision == other.globalBoardRevision;
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
                return hash;
            }
        }
    }

    private MovementRangeCacheKey? movementRangeCacheKey;
    private Dictionary<Vector3Int, List<Vector3Int>> movementRangeCache;

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
                unitInstanceId = selectedUnit.GetInstanceID(),
                remainingMovementPoints = Mathf.Max(0, selectedUnit.RemainingMovementPoints),
                currentFuel = Mathf.Max(0, selectedUnit.CurrentFuel),
                globalBoardRevision = ThreatRevisionTracker.GlobalBoardRevision
            };

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
                List<UnitManager> occupants = new List<UnitManager>();
                foreach (UnitManager occupant in GetOccupantsAtCellForRange(cell, selectedUnit))
                {
                    if (occupant == null)
                        continue;
                    occupants.Add(occupant);

                    if (PathManager.IsPathfindingDebugLogsEnabled && Application.isPlaying)
                    {
                        if (occupantDebug == null)
                            occupantDebug = new List<string>(2);
                        occupantDebug.Add($"{occupant.name}[team={(int)occupant.TeamId},band={OccupancyResolver.GetHeightBand(occupant)}]");
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

    private IEnumerable<UnitManager> GetOccupantsAtCellForRange(Vector3Int cell, UnitManager exceptUnit)
    {
        cell.z = 0;
        List<UnitManager> units = UnitManager.AllActive;
        if (units == null || units.Count <= 0)
            yield break;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                continue;
            if (unit.BoardTilemap == null || unit.BoardTilemap != terrainTilemap)
                continue;
            if (unit.gameObject.scene != terrainTilemap.gameObject.scene)
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
                yield return unit;
        }
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
