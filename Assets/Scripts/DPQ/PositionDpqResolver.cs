using UnityEngine;
using UnityEngine.Tilemaps;

public readonly struct PositionDpqResult
{
    public static PositionDpqResult None => new PositionDpqResult(0, 0);

    public int Points { get; }
    public int DefenseBonus { get; }

    public PositionDpqResult(int points, int defenseBonus)
    {
        Points = Mathf.Max(0, points);
        DefenseBonus = defenseBonus;
    }
}

/// <summary>
/// Resolves the DPQ supplied by a unit's layer or by the board position it occupies.
/// This is a read-only query and does not alter the unit, board or occupancy state.
/// </summary>
public static class PositionDpqResolver
{
    public static PositionDpqResult Resolve(
        UnitManager unit,
        Vector3Int cell,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig airHeightConfig = null)
    {
        if (TryResolveUnitLayer(unit, airHeightConfig, out PositionDpqResult layerDpq))
            return layerDpq;

        return Resolve(cell, boardTilemap, terrainDatabase);
    }

    public static bool TryResolveUnitLayer(
        UnitManager unit,
        DPQAirHeightConfig airHeightConfig,
        out PositionDpqResult result)
    {
        result = PositionDpqResult.None;
        if (unit == null
            || unit.GetDomain() != Domain.Air
            || airHeightConfig == null
            || !airHeightConfig.TryGetFor(unit.GetDomain(), unit.GetHeightLevel(), out DPQData airDpq)
            || airDpq == null)
        {
            return false;
        }

        result = FromData(airDpq);
        return true;
    }

    public static PositionDpqResult Resolve(
        Vector3Int cell,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase)
    {
        cell.z = 0;

        if (boardTilemap == null || terrainDatabase == null)
            return PositionDpqResult.None;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction != null
            && !construction.IsForwardObserverSpot
            && construction.TryResolveConstructionData(out ConstructionData constructionData)
            && constructionData != null
            && constructionData.dpqData != null)
        {
            return FromData(constructionData.dpqData);
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardTilemap, cell);
        if (structure != null && structure.dpqData != null)
            return FromData(structure.dpqData);

        TileBase tile = boardTilemap.GetTile(cell);
        if (TryResolveTerrainDpq(tile, terrainDatabase, out PositionDpqResult terrainDpq))
            return terrainDpq;

        GridLayout grid = boardTilemap.layoutGrid;
        if (grid != null)
        {
            Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
            for (int i = 0; i < maps.Length; i++)
            {
                Tilemap map = maps[i];
                if (map == null || map == boardTilemap)
                    continue;

                if (TryResolveTerrainDpq(map.GetTile(cell), terrainDatabase, out PositionDpqResult otherTerrainDpq))
                    return otherTerrainDpq;
            }
        }

        return PositionDpqResult.None;
    }

    private static bool TryResolveTerrainDpq(
        TileBase tile,
        TerrainDatabase terrainDatabase,
        out PositionDpqResult result)
    {
        result = PositionDpqResult.None;
        if (tile == null
            || !terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData terrain)
            || terrain?.dpqData == null)
        {
            return false;
        }

        result = FromData(terrain.dpqData);
        return true;
    }

    private static PositionDpqResult FromData(DPQData dpq)
    {
        return dpq != null
            ? new PositionDpqResult(dpq.Pontos, dpq.DefesaBonus)
            : PositionDpqResult.None;
    }
}
