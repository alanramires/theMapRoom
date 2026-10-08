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
/// Fonte unica do DPQ de combate: a execucao do combate, a previsao da IA e os
/// paineis leem daqui. Ordem: DPQ da camada configurada (Ar, submerso), depois
/// construcao, estrutura e terreno — cada um so vale se aceitar a camada ativa
/// da unidade. Consulta somente leitura; nao altera unidade, tabuleiro nem ocupacao.
/// </summary>
public static class PositionDpqResolver
{
    /// <summary>
    /// DPQ da unidade se ela estivesse em <paramref name="cell"/> na camada atual.
    /// Sem unidade, cai na leitura da celula sem filtro de camada.
    /// </summary>
    public static PositionDpqResult Resolve(
        UnitManager unit,
        Vector3Int cell,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig airHeightConfig = null)
    {
        if (unit == null)
            return Resolve(cell, boardTilemap, terrainDatabase);

        return TryResolveData(unit, cell, boardTilemap, terrainDatabase, airHeightConfig, out DPQData dpq, out _)
            ? FromData(dpq)
            : PositionDpqResult.None;
    }

    public static bool TryResolveData(
        UnitManager unit,
        Vector3Int cell,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig airHeightConfig,
        out DPQData dpq,
        out string source)
    {
        dpq = null;
        source = "-";
        if (unit == null)
            return false;

        return TryResolveDataForLayer(
            unit.GetDomain(),
            unit.GetHeightLevel(),
            cell,
            boardTilemap,
            terrainDatabase,
            airHeightConfig,
            out dpq,
            out source);
    }

    public static bool TryResolveDataForLayer(
        Domain activeDomain,
        HeightLevel activeHeight,
        Vector3Int cell,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig airHeightConfig,
        out DPQData dpq,
        out string source)
    {
        dpq = null;
        source = "-";
        cell.z = 0;

        if (UsesConfiguredLayerDpq(activeDomain, activeHeight)
            && airHeightConfig != null
            && airHeightConfig.TryGetFor(activeDomain, activeHeight, out DPQData layerDpq)
            && layerDpq != null)
        {
            dpq = layerDpq;
            source = $"Camada ativa: {activeDomain}/{activeHeight}";
            return true;
        }

        if (boardTilemap == null)
            return false;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction != null
            && !construction.IsForwardObserverSpot
            && ConstructionSupportsLayer(construction, activeDomain, activeHeight)
            && construction.TryResolveConstructionData(out ConstructionData constructionData)
            && constructionData != null
            && constructionData.dpqData != null)
        {
            dpq = constructionData.dpqData;
            source = $"Construcao: {ResolveConstructionName(construction)}";
            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardTilemap, cell);
        if (structure != null
            && StructureSupportsLayer(structure, activeDomain, activeHeight)
            && structure.dpqData != null)
        {
            dpq = structure.dpqData;
            source = $"Estrutura: {ResolveStructureName(structure)}";
            return true;
        }

        if (TryResolveTerrainForLayer(boardTilemap, terrainDatabase, cell, activeDomain, activeHeight, out TerrainTypeData terrain)
            && terrain != null
            && terrain.dpqData != null)
        {
            dpq = terrain.dpqData;
            source = $"Terreno: {ResolveTerrainName(terrain)}";
            return true;
        }

        return false;
    }

    public static bool TryResolveUnitLayer(
        UnitManager unit,
        DPQAirHeightConfig airHeightConfig,
        out PositionDpqResult result)
    {
        result = PositionDpqResult.None;
        if (unit == null || airHeightConfig == null)
            return false;

        Domain domain = unit.GetDomain();
        HeightLevel height = unit.GetHeightLevel();
        if (!UsesConfiguredLayerDpq(domain, height)
            || !airHeightConfig.TryGetFor(domain, height, out DPQData layerDpq)
            || layerDpq == null)
        {
            return false;
        }

        result = FromData(layerDpq);
        return true;
    }

    /// <summary>
    /// DPQ da celula sem unidade e sem filtro de camada. Serve a leituras de
    /// terreno genericas; combate deve usar a sobrecarga com unidade.
    /// </summary>
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

    // Ar e submerso tiram o DPQ da propria camada; o chao embaixo nao protege.
    private static bool UsesConfiguredLayerDpq(Domain domain, HeightLevel height)
    {
        return domain == Domain.Air
            || (domain == Domain.Submarine && height == HeightLevel.Submerged);
    }

    // Terreno que aceita a camada. Se nenhum tilemap da celula aceitar, fica o
    // primeiro encontrado (o principal antes dos demais), como na execucao.
    private static bool TryResolveTerrainForLayer(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        Domain activeDomain,
        HeightLevel activeHeight,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDb == null)
            return false;

        cell.z = 0;
        TerrainTypeData fallback = null;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDb.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
        {
            if (TerrainSupportsLayer(byMainTile, activeDomain, activeHeight))
            {
                terrain = byMainTile;
                return true;
            }

            fallback = byMainTile;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
        {
            terrain = fallback;
            return terrain != null;
        }

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase other = map.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDb.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
            {
                if (fallback == null)
                    fallback = byGridTile;

                if (TerrainSupportsLayer(byGridTile, activeDomain, activeHeight))
                {
                    terrain = byGridTile;
                    return true;
                }
            }
        }

        terrain = fallback;
        return terrain != null;
    }

    private static bool TerrainSupportsLayer(TerrainTypeData terrain, Domain domain, HeightLevel heightLevel)
    {
        if (terrain == null)
            return false;
        if (terrain.domain == domain && terrain.heightLevel == heightLevel)
            return true;
        if (domain == Domain.Air && terrain.alwaysAllowAirDomain)
            return true;
        if (terrain.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < terrain.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrain.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool StructureSupportsLayer(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null)
            return false;
        if (structure.domain == domain && structure.heightLevel == heightLevel)
            return true;
        if (domain == Domain.Air && structure.alwaysAllowAirDomain)
            return true;
        if (structure.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < structure.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = structure.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool ConstructionSupportsLayer(ConstructionManager construction, Domain domain, HeightLevel heightLevel)
    {
        if (construction == null)
            return false;
        if (construction.SupportsLayerMode(domain, heightLevel))
            return true;
        return domain == Domain.Air && construction.AllowsAirDomain();
    }

    private static string ResolveConstructionName(ConstructionManager construction)
    {
        if (!string.IsNullOrWhiteSpace(construction.ConstructionDisplayName))
            return construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(construction.ConstructionId))
            return construction.ConstructionId;
        return construction.name;
    }

    private static string ResolveStructureName(StructureData structure)
    {
        if (!string.IsNullOrWhiteSpace(structure.displayName))
            return structure.displayName;
        if (!string.IsNullOrWhiteSpace(structure.id))
            return structure.id;
        return structure.name;
    }

    private static string ResolveTerrainName(TerrainTypeData terrain)
    {
        if (!string.IsNullOrWhiteSpace(terrain.displayName))
            return terrain.displayName;
        if (!string.IsNullOrWhiteSpace(terrain.id))
            return terrain.id;
        return terrain.name;
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
