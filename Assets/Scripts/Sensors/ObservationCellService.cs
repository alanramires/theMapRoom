using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// SERVICO BURRO. Responde, para uma celula do tabuleiro, os fatos que qualquer
/// pergunta de observacao precisa: que terreno esta ali, que construcao, que
/// estrutura, em que camada ela responde, qual o EV dela e se ela bloqueia
/// linha.
///
/// Nao sabe de alcance, de chave de deteccao, de metodo, de especializacao, de
/// furtividade nem de time. Nao decide quem enxerga o que — devolve o fato.
///
/// Existe porque `PodeEnxergar` e `PodeDetectar` sao duas entidades diferentes e
/// precisavam consultar os mesmos fatos sem uma depender da outra. Enquanto
/// isso morava dentro do `PodeDetectar`, revelar hexagono herdava regra de
/// deteccao por tabela de flags — foi assim que o mar do submarino sumiu.
///
/// Os caches sao de escopo de refresh: valem enquanto o tabuleiro nao muda e
/// sao limpos por <see cref="ClearRefreshScopedCaches"/>.
/// </summary>
public static class ObservationCellService
{
    private readonly struct CellCacheKey : IEquatable<CellCacheKey>
    {
        public readonly int tilemapInstanceId;
        public readonly int x;
        public readonly int y;

        public CellCacheKey(int tilemapInstanceId, int x, int y)
        {
            this.tilemapInstanceId = tilemapInstanceId;
            this.x = x;
            this.y = y;
        }

        public bool Equals(CellCacheKey other)
        {
            return tilemapInstanceId == other.tilemapInstanceId &&
                x == other.x &&
                y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is CellCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + tilemapInstanceId;
                hash = (hash * 31) + x;
                hash = (hash * 31) + y;
                return hash;
            }
        }
    }

    private static readonly Dictionary<CellCacheKey, TerrainTypeData> terrainCacheForRefresh =
        new Dictionary<CellCacheKey, TerrainTypeData>(4096);
    private static readonly Dictionary<CellCacheKey, ConstructionData> constructionCacheForRefresh =
        new Dictionary<CellCacheKey, ConstructionData>(4096);
    private static readonly Dictionary<CellCacheKey, StructureData> structureCacheForRefresh =
        new Dictionary<CellCacheKey, StructureData>(4096);
    private static readonly Dictionary<int, Tilemap[]> gridTilemapsCache =
        new Dictionary<int, Tilemap[]>();

    /// <summary>
    /// Instrumentacao. Fica publica porque o consumidor e quem reporta; o
    /// servico so conta o que fez.
    /// </summary>
    public static int CellVisionCalls;
    public static double ConstructionMs;
    public static double StructureMs;

    public static void ResetCounters()
    {
        CellVisionCalls = 0;
        ConstructionMs = 0d;
        StructureMs = 0d;
    }

    public static void ClearRefreshScopedCaches()
    {
        terrainCacheForRefresh.Clear();
        constructionCacheForRefresh.Clear();
        structureCacheForRefresh.Clear();
    }

    /// <summary>
    /// EV e bloqueio de linha de uma celula, na camada que ela responde.
    ///
    /// A camada sai desta ordem: forcada pelo chamador, senao a do ocupante
    /// quando ele e passado, senao a nativa do terreno. E por isso que um
    /// helicoptero alvo responde pelo EV de air/low e um hex vazio responde
    /// pelo EV da superficie dele.
    /// </summary>
    public static bool TryResolveCellVision(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        UnitManager occupantUnit,
        DPQAirHeightConfig dpqAirHeightConfig,
        out float ev,
        out bool blockLoS,
        Domain? forcedDomain = null,
        HeightLevel? forcedHeightLevel = null)
    {
        ev = 0f;
        blockLoS = true;
        if (!TryResolveTerrain(tilemap, terrainDatabase, cell, out TerrainTypeData terrain)
            || terrain == null)
        {
            return false;
        }

        CellVisionCalls++;
        double constructionStartMs = Time.realtimeSinceStartupAsDouble;
        TryResolveConstruction(tilemap, cell, out ConstructionData constructionData);
        double structureStartMs = Time.realtimeSinceStartupAsDouble;
        ConstructionMs += (structureStartMs - constructionStartMs) * 1000d;
        StructureData structureData = ResolveStructure(tilemap, cell);
        StructureMs += (Time.realtimeSinceStartupAsDouble - structureStartMs) * 1000d;

        Domain domain = Domain.Land;
        HeightLevel height = HeightLevel.Surface;
        if (forcedDomain.HasValue && forcedHeightLevel.HasValue)
        {
            domain = forcedDomain.Value;
            height = forcedHeightLevel.Value;
        }
        else if (occupantUnit != null)
        {
            domain = occupantUnit.GetDomain();
            height = occupantUnit.GetHeightLevel();
        }
        else
        {
            TryResolveObservationLayer(
                tilemap,
                terrainDatabase,
                cell,
                out domain,
                out height,
                useOccupantLayerForTarget: false);
        }

        TerrainVisionResolver.Resolve(
            terrain,
            domain,
            height,
            dpqAirHeightConfig,
            constructionData,
            structureData,
            out ev,
            out blockLoS);

        return true;
    }

    /// <summary>
    /// Em que camada esta celula responde. Ocupante manda quando pedido,
    /// depois construcao, depois estrutura, depois o proprio terreno.
    /// </summary>
    public static bool TryResolveObservationLayer(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out Domain domain,
        out HeightLevel height,
        bool useOccupantLayerForTarget = true)
    {
        domain = Domain.Land;
        height = HeightLevel.Surface;

        if (useOccupantLayerForTarget)
        {
            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(cell);
            if (occupant != null)
            {
                domain = occupant.GetDomain();
                height = occupant.GetHeightLevel();
                return true;
            }
        }

        if (!TryResolveTerrain(map, terrainDatabase, cell, out TerrainTypeData terrain)
            || terrain == null)
        {
            return true;
        }

        if (TryResolveConstruction(map, cell, out ConstructionData constructionData)
            && constructionData != null)
        {
            domain = constructionData.domain;
            height = constructionData.heightLevel;
            return true;
        }

        StructureData structureData = ResolveStructure(map, cell);
        if (structureData != null)
        {
            domain = structureData.domain;
            height = structureData.heightLevel;
            return true;
        }

        domain = terrain.domain;
        height = terrain.heightLevel;
        return true;
    }

    public static bool TryResolveTerrain(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDatabase == null)
            return false;

        cell.z = 0;
        CellCacheKey cacheKey = new CellCacheKey(
            terrainTilemap.GetEntityId().GetHashCode(),
            cell.x,
            cell.y);
        if (terrainCacheForRefresh.TryGetValue(cacheKey, out TerrainTypeData cachedTerrain))
        {
            terrain = cachedTerrain;
            return terrain != null;
        }

        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null
            && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile)
            && byMainTile != null)
        {
            terrain = byMainTile;
            terrainCacheForRefresh[cacheKey] = terrain;
            return true;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = GetCachedTilemapsForGrid(grid);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap scan = maps[i];
            if (scan == null)
                continue;

            TileBase other = scan.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGridTile)
                && byGridTile != null)
            {
                terrain = byGridTile;
                terrainCacheForRefresh[cacheKey] = terrain;
                return true;
            }
        }

        terrainCacheForRefresh[cacheKey] = null;
        return false;
    }

    public static bool TryResolveConstruction(
        Tilemap tilemap,
        Vector3Int cell,
        out ConstructionData constructionData)
    {
        constructionData = null;
        if (tilemap == null)
            return false;

        cell.z = 0;
        CellCacheKey cacheKey = new CellCacheKey(
            tilemap.GetEntityId().GetHashCode(),
            cell.x,
            cell.y);
        if (constructionCacheForRefresh.TryGetValue(cacheKey, out ConstructionData cached))
        {
            constructionData = cached;
            return constructionData != null;
        }

        ConstructionManager construction =
            ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell);
        ConstructionData resolved = null;
        if (construction != null)
        {
            ConstructionDatabase db = construction.ConstructionDatabase;
            string id = construction.ConstructionId;
            if (db != null &&
                !string.IsNullOrWhiteSpace(id) &&
                db.TryGetById(id, out ConstructionData data))
            {
                resolved = data;
            }
        }

        constructionCacheForRefresh[cacheKey] = resolved;
        constructionData = resolved;
        return constructionData != null;
    }

    /// <summary>Mesma chave e mesmo escopo de refresh da construcao.</summary>
    public static StructureData ResolveStructure(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null)
            return StructureOccupancyRules.GetStructureAtCell(tilemap, cell);

        cell.z = 0;
        CellCacheKey cacheKey = new CellCacheKey(
            tilemap.GetEntityId().GetHashCode(),
            cell.x,
            cell.y);
        if (structureCacheForRefresh.TryGetValue(cacheKey, out StructureData cached))
            return cached;

        StructureData resolved = StructureOccupancyRules.GetStructureAtCell(tilemap, cell);
        structureCacheForRefresh[cacheKey] = resolved;
        return resolved;
    }

    public static Tilemap[] GetCachedTilemapsForGrid(GridLayout grid)
    {
        if (grid == null)
            return Array.Empty<Tilemap>();

        int gridId = grid.GetEntityId().GetHashCode();
        if (gridTilemapsCache.TryGetValue(gridId, out Tilemap[] cached) &&
            cached != null &&
            cached.Length > 0)
        {
            return cached;
        }

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        gridTilemapsCache[gridId] = maps ?? Array.Empty<Tilemap>();
        return gridTilemapsCache[gridId];
    }
}
