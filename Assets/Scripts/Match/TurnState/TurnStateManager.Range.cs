using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private void PaintSelectedUnitMovementRange()
    {
        double perfStart = Time.realtimeSinceStartupAsDouble;
        try
        {
            ClearMovementRange(keepCommittedMovement: true);
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
            Tilemap occupancyMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
            bool totalWarEnabled = UnitRulesDefinition.IsTotalWarEnabled();
            HashSet<Vector3Int> alliedOccupiedCells = totalWarEnabled
                ? BuildAlliedOccupiedCellsSnapshot(occupancyMap, selectedUnit.TeamId, selectedUnit)
                : null;

            Color teamColor = TeamUtils.GetColor(selectedUnit.TeamId);
            Color overlayColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(movementRangeAlpha));
            Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
                terrainTilemap,
                selectedUnit,
                radius,
                terrainDatabase);
            foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in validPaths)
            {
                Vector3Int cell = pair.Key;
                if (terrainTilemap.GetTile(cell) == null)
                    continue;
                if (alliedOccupiedCells != null && alliedOccupiedCells.Contains(cell))
                {
                    // Em hex disputado, pode atravessar aliado, mas nao pode encerrar nele.
                    continue;
                }
                if (!IsRangeCellAllowedByTakeoffOptions(cell, pair.Value))
                    continue;

                rangeMapTilemap.SetTile(cell, rangeOverlayTile);
                rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
                rangeMapTilemap.SetColor(cell, overlayColor);
                paintedRangeCells.Add(cell);
                paintedRangeLookup.Add(cell);
                movementPathsByCell[cell] = pair.Value;
            }
        }
        finally
        {
            RegisterPerfRangeDuration((Time.realtimeSinceStartupAsDouble - perfStart) * 1000d);
        }
    }

    private void ClearMovementRange()
    {
        ClearMovementRange(keepCommittedMovement: false);
    }

    private void ClearMovementRange(bool keepCommittedMovement)
    {
        if (rangeMapTilemap != null)
        {
            for (int i = 0; i < paintedRangeCells.Count; i++)
            {
                Vector3Int cell = paintedRangeCells[i];
                rangeMapTilemap.SetTile(cell, null);
                rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
                rangeMapTilemap.SetColor(cell, Color.white);
            }
        }

        paintedRangeCells.Clear();
        paintedRangeLookup.Clear();
        movementPathsByCell.Clear();

        if (!keepCommittedMovement)
            ClearCommittedMovement();
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

    private static HashSet<Vector3Int> BuildAlliedOccupiedCellsSnapshot(Tilemap referenceTilemap, TeamId teamId, UnitManager exceptUnit)
    {
        HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();
        if (referenceTilemap == null)
            return occupied;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit == exceptUnit || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if (unit.TeamId != teamId)
                continue;
            if (unit.BoardTilemap == null || unit.BoardTilemap != referenceTilemap)
                continue;
            if (unit.gameObject.scene != referenceTilemap.gameObject.scene)
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            occupied.Add(cell);
        }

        return occupied;
    }
}
