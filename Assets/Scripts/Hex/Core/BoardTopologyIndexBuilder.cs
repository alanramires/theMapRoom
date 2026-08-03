using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

internal static class BoardTopologyIndexBuilder
{
    private const int MaxDetailedValidationMessages = 24;

    private sealed class StructureCellCandidate
    {
        public StructureData structure;
        public StructureDatabase database;
    }

    public static BoardTopologyBuildResult Build(
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase)
    {
        var result = new BoardTopologyBuildResult();
        if (boardTilemap == null)
        {
            result.validation.AddError(
                "Board Tilemap ausente; o índice não pode ser construído.");
            return result;
        }

        result.mapId = BuildMapId(boardTilemap);
        if (terrainDatabase == null)
        {
            result.validation.AddError(
                "TerrainDatabase ausente; terrenos não podem ser resolvidos.");
        }

        List<Tilemap> tilemaps = CollectCompatibleTilemaps(boardTilemap);
        HashSet<Vector3Int> paintedCells = CollectPaintedCells(tilemaps);
        Dictionary<Vector3Int, ConstructionData> constructions =
            CollectConstructions(
                boardTilemap,
                paintedCells,
                result.validation);
        Dictionary<Vector3Int, StructureCellCandidate> structures =
            new Dictionary<Vector3Int, StructureCellCandidate>();
        CollectStructuresAndRoutes(
            boardTilemap,
            paintedCells,
            structures,
            result.routeEdges,
            result.validation);

        var orderedCells = new List<Vector3Int>(paintedCells);
        orderedCells.Sort(CompareCells);
        var neighborScratch = new List<Vector3Int>(6);
        int unmappedTerrainCount = 0;

        for (int i = 0; i < orderedCells.Count; i++)
        {
            Vector3Int cell = orderedCells[i];
            TerrainTypeData terrain = ResolveTerrain(
                cell,
                boardTilemap,
                tilemaps,
                terrainDatabase);
            structures.TryGetValue(
                cell,
                out StructureCellCandidate structureCandidate);
            constructions.TryGetValue(
                cell,
                out ConstructionData construction);

            if (terrain == null)
            {
                unmappedTerrainCount++;
                if (unmappedTerrainCount <= MaxDetailedValidationMessages)
                {
                    result.validation.AddWarning(
                        $"Célula {FormatCell(cell)} possui tile, mas nenhum " +
                        "tile foi mapeado no TerrainDatabase.");
                }
            }

            UnitMovementPathRules.GetImmediateHexNeighbors(
                boardTilemap,
                cell,
                neighborScratch);
            var indexedNeighbors = new List<Vector3Int>(6);
            for (int n = 0; n < neighborScratch.Count; n++)
            {
                Vector3Int neighbor = neighborScratch[n];
                neighbor.z = 0;
                if (paintedCells.Contains(neighbor))
                    indexedNeighbors.Add(neighbor);
            }
            indexedNeighbors.Sort(CompareCells);

            StructureData structure =
                structureCandidate != null
                    ? structureCandidate.structure
                    : null;
            Domain primaryDomain = ResolvePrimaryDomain(
                construction,
                structure,
                terrain);

            var record = new BoardTopologyCellRecord
            {
                cell = cell,
                terrain = terrain,
                structure = structure,
                construction = construction,
                neighbors = indexedNeighbors,
                hasAnyPaintedTile = true,
                isBeach = IsBeach(terrain),
                isPotentialLandingSurface =
                    IsPotentialLandingSurface(
                        construction,
                        structure,
                        terrain),
                isPotentialEmbarkCell =
                    primaryDomain != Domain.Air
                    && (construction != null
                        || structure != null
                        || terrain != null),
                isPotentialDisembarkCell =
                    primaryDomain != Domain.Air
                    && (construction != null
                        || structure != null
                        || terrain != null)
            };
            record.SetSourceTileSignature(
                BuildTileSignature(cell, tilemaps));
            result.cells.Add(record);
        }

        if (unmappedTerrainCount > MaxDetailedValidationMessages)
        {
            result.validation.AddWarning(
                $"{unmappedTerrainCount - MaxDetailedValidationMessages} " +
                "outra(s) célula(s) com tile não mapeado foram omitidas.");
        }

        MarkCoastalCells(result.cells);
        result.routeEdges.Sort(CompareRouteEdges);
        result.fingerprint = ComputeFingerprint(
            result.mapId,
            result.cells,
            result.routeEdges);
        return result;
    }

    public static string ComputeFingerprint(
        string mapId,
        IReadOnlyList<BoardTopologyCellRecord> cells,
        IReadOnlyList<BoardTopologyRouteEdgeRecord> routeEdges)
    {
        var source = new StringBuilder(4096);
        source.Append("BoardTopology/")
            .Append(BoardTopologyIndex.CurrentTopologyVersion)
            .Append('\n')
            .Append(mapId ?? string.Empty)
            .Append('\n');

        if (cells != null)
        {
            var orderedCells =
                new List<BoardTopologyCellRecord>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null)
                    orderedCells.Add(cells[i]);
            }
            orderedCells.Sort((left, right) =>
                CompareCells(left.cell, right.cell));

            for (int i = 0; i < orderedCells.Count; i++)
            {
                BoardTopologyCellRecord record = orderedCells[i];
                source.Append("C|")
                    .Append(record.cell.x).Append('|')
                    .Append(record.cell.y).Append('|')
                    .Append(record.SourceTileSignature).Append('|')
                    .Append(TerrainSignature(record.terrain)).Append('|')
                    .Append(StructureSignature(record.structure)).Append('|')
                    .Append(ConstructionSignature(record.construction))
                    .Append('|')
                    .Append(record.isBeach ? '1' : '0')
                    .Append(record.isCoastal ? '1' : '0')
                    .Append(record.isPotentialLandingSurface ? '1' : '0')
                    .Append(record.isPotentialEmbarkCell ? '1' : '0')
                    .Append(record.isPotentialDisembarkCell ? '1' : '0')
                    .Append('\n');
            }
        }

        if (routeEdges != null)
        {
            var orderedEdges =
                new List<BoardTopologyRouteEdgeRecord>(routeEdges.Count);
            for (int i = 0; i < routeEdges.Count; i++)
            {
                if (routeEdges[i] != null)
                    orderedEdges.Add(routeEdges[i]);
            }
            orderedEdges.Sort(CompareRouteEdges);

            for (int i = 0; i < orderedEdges.Count; i++)
            {
                BoardTopologyRouteEdgeRecord edge = orderedEdges[i];
                BoardTopologyEdgeKey key = edge.EdgeKey;
                source.Append("E|")
                    .Append(key.a.x).Append('|')
                    .Append(key.a.y).Append('|')
                    .Append(key.b.x).Append('|')
                    .Append(key.b.y).Append('|')
                    .Append(StableId(edge.structure != null
                        ? edge.structure.id
                        : string.Empty))
                    .Append('|')
                    .Append(edge.routeName ?? string.Empty)
                    .Append('\n');
            }
        }

        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(source.ToString());
            byte[] hash = sha.ComputeHash(bytes);
            var hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                hex.Append(hash[i].ToString("x2"));
            return hex.ToString();
        }
    }

    private static List<Tilemap> CollectCompatibleTilemaps(
        Tilemap boardTilemap)
    {
        var maps = new List<Tilemap>();
        maps.Add(boardTilemap);
        GridLayout grid = boardTilemap.layoutGrid;
        if (grid == null)
            return maps;

        Tilemap[] candidates =
            grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < candidates.Length; i++)
        {
            Tilemap map = candidates[i];
            if (map == null
                || map == boardTilemap
                || map.gameObject.scene != boardTilemap.gameObject.scene)
            {
                continue;
            }
            maps.Add(map);
        }
        return maps;
    }

    private static HashSet<Vector3Int> CollectPaintedCells(
        IReadOnlyList<Tilemap> tilemaps)
    {
        var cells = new HashSet<Vector3Int>();
        for (int i = 0; i < tilemaps.Count; i++)
        {
            Tilemap map = tilemaps[i];
            if (map == null)
                continue;

            foreach (Vector3Int rawCell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(rawCell))
                    continue;
                Vector3Int cell = rawCell;
                cell.z = 0;
                cells.Add(cell);
            }
        }
        return cells;
    }

    private static Dictionary<Vector3Int, ConstructionData>
        CollectConstructions(
            Tilemap boardTilemap,
            HashSet<Vector3Int> paintedCells,
            BoardTopologyValidationReport validation)
    {
        var byCell = new Dictionary<Vector3Int, ConstructionData>();
        ConstructionManager[] managers =
            UnityEngine.Object.FindObjectsByType<ConstructionManager>(
                FindObjectsInactive.Exclude);

        for (int i = 0; i < managers.Length; i++)
        {
            ConstructionManager manager = managers[i];
            if (manager == null
                || manager.gameObject.scene
                    != boardTilemap.gameObject.scene
                || !manager.TryResolveConstructionData(
                    out ConstructionData data)
                || data == null
                || data.isFakeBuilding)
            {
                continue;
            }

            Vector3Int cell =
                manager.BoardTilemap == boardTilemap
                    ? manager.CurrentCellPosition
                    : HexCoordinates.WorldToCell(
                        boardTilemap,
                        manager.transform.position);
            cell.z = 0;
            if (!paintedCells.Contains(cell))
            {
                validation.AddError(
                    $"Construção '{StableId(data.id)}' em " +
                    $"{FormatCell(cell)} está fora do tabuleiro indexado.");
                continue;
            }

            if (!byCell.TryGetValue(
                    cell,
                    out ConstructionData current))
            {
                byCell.Add(cell, data);
                continue;
            }

            validation.AddError(
                $"Mais de uma construção física ocupa {FormatCell(cell)}: " +
                $"'{StableId(current.id)}' e '{StableId(data.id)}'.");
            if (string.CompareOrdinal(
                    StableId(data.id),
                    StableId(current.id)) < 0)
            {
                byCell[cell] = data;
            }
        }
        return byCell;
    }

    private static void CollectStructuresAndRoutes(
        Tilemap boardTilemap,
        HashSet<Vector3Int> paintedCells,
        Dictionary<Vector3Int, StructureCellCandidate> structures,
        List<BoardTopologyRouteEdgeRecord> routeEdges,
        BoardTopologyValidationReport validation)
    {
        RoadNetworkManager[] networks =
            UnityEngine.Object.FindObjectsByType<RoadNetworkManager>(
                FindObjectsInactive.Exclude);
        var edgeDedup = new HashSet<string>(
            StringComparer.Ordinal);

        for (int i = 0; i < networks.Length; i++)
        {
            RoadNetworkManager network = networks[i];
            if (network == null
                || network.gameObject.scene
                    != boardTilemap.gameObject.scene
                || !IsCompatibleReference(
                    boardTilemap,
                    network.BoardTilemap))
            {
                continue;
            }

            StructureDatabase database = network.StructureDatabase;
            IReadOnlyList<StructureData> definitions =
                database != null ? database.Structures : null;
            if (definitions == null)
                continue;

            for (int s = 0; s < definitions.Count; s++)
            {
                StructureData structure = definitions[s];
                if (structure == null)
                    continue;

                IReadOnlyList<RoadRouteDefinition> routes =
                    database.GetRoadRoutes(structure);
                if (routes == null)
                    routes = structure.roadRoutes;
                if (routes == null)
                    continue;

                for (int r = 0; r < routes.Count; r++)
                {
                    RoadRouteDefinition route = routes[r];
                    if (route == null || route.cells == null)
                        continue;

                    if (route.ownerDatabase != null
                        && route.ownerDatabase != database)
                    {
                        validation.AddWarning(
                            $"Rota '{ResolveRouteName(route, r)}' de " +
                            $"'{StableId(structure.id)}' referencia outro " +
                            "StructureDatabase. Mantida por compatibilidade.");
                    }

                    for (int c = 0; c < route.cells.Count; c++)
                    {
                        Vector3Int cell = route.cells[c];
                        cell.z = 0;
                        if (!paintedCells.Contains(cell))
                        {
                            validation.AddError(
                                $"Rota '{ResolveRouteName(route, r)}' contém " +
                                $"{FormatCell(cell)} fora do tabuleiro.");
                            continue;
                        }

                        var candidate = new StructureCellCandidate
                        {
                            structure = structure,
                            database = database
                        };
                        if (!structures.TryGetValue(
                                cell,
                                out StructureCellCandidate current)
                            || ShouldReplace(current, candidate))
                        {
                            structures[cell] = candidate;
                        }
                    }

                    for (int c = 1; c < route.cells.Count; c++)
                    {
                        Vector3Int from = route.cells[c - 1];
                        Vector3Int to = route.cells[c];
                        from.z = 0;
                        to.z = 0;
                        if (!AreImmediateNeighbors(
                                boardTilemap,
                                from,
                                to))
                        {
                            validation.AddError(
                                $"Rota '{ResolveRouteName(route, r)}' salta " +
                                $"de {FormatCell(from)} para {FormatCell(to)}.");
                        }

                        BoardTopologyEdgeKey key =
                            new BoardTopologyEdgeKey(from, to);
                        string dedupKey =
                            $"{key.a.x},{key.a.y}>{key.b.x},{key.b.y}|" +
                            $"{StableId(structure.id)}|" +
                            $"{ResolveRouteName(route, r)}";
                        if (!edgeDedup.Add(dedupKey))
                            continue;

                        routeEdges.Add(
                            new BoardTopologyRouteEdgeRecord
                            {
                                from = key.a,
                                to = key.b,
                                structure = structure,
                                routeName =
                                    ResolveRouteName(route, r)
                            });
                    }
                }
            }
        }
    }

    private static bool ShouldReplace(
        StructureCellCandidate current,
        StructureCellCandidate candidate)
    {
        if (candidate == null || candidate.structure == null)
            return false;
        if (current == null || current.structure == null)
            return true;
        if (candidate.database != null
            && candidate.database == current.database)
        {
            return candidate.database.ComparePriority(
                candidate.structure,
                current.structure) > 0;
        }
        if (candidate.structure.priorityOrder
            != current.structure.priorityOrder)
        {
            return candidate.structure.priorityOrder
                > current.structure.priorityOrder;
        }
        return string.CompareOrdinal(
                StableId(candidate.structure.id),
                StableId(current.structure.id)) < 0;
    }

    private static TerrainTypeData ResolveTerrain(
        Vector3Int cell,
        Tilemap boardTilemap,
        IReadOnlyList<Tilemap> tilemaps,
        TerrainDatabase terrainDatabase)
    {
        if (terrainDatabase == null)
            return null;

        TileBase primary = boardTilemap.GetTile(cell);
        if (primary != null
            && terrainDatabase.TryGetByPaletteTile(
                primary,
                out TerrainTypeData terrain)
            && terrain != null)
        {
            return terrain;
        }

        for (int i = 0; i < tilemaps.Count; i++)
        {
            Tilemap map = tilemaps[i];
            if (map == null || map == boardTilemap)
                continue;
            TileBase tile = map.GetTile(cell);
            if (tile != null
                && terrainDatabase.TryGetByPaletteTile(
                    tile,
                    out terrain)
                && terrain != null)
            {
                return terrain;
            }
        }
        return null;
    }

    private static string BuildTileSignature(
        Vector3Int cell,
        IReadOnlyList<Tilemap> tilemaps)
    {
        var names = new List<string>();
        for (int i = 0; i < tilemaps.Count; i++)
        {
            Tilemap map = tilemaps[i];
            TileBase tile = map != null ? map.GetTile(cell) : null;
            if (tile == null)
                continue;
            names.Add($"{map.name}:{tile.name}");
        }
        names.Sort(StringComparer.Ordinal);
        return string.Join(",", names);
    }

    private static void MarkCoastalCells(
        IReadOnlyList<BoardTopologyCellRecord> cells)
    {
        var byCell =
            new Dictionary<Vector3Int, BoardTopologyCellRecord>();
        for (int i = 0; i < cells.Count; i++)
        {
            BoardTopologyCellRecord record = cells[i];
            if (record != null)
                byCell[record.cell] = record;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            BoardTopologyCellRecord record = cells[i];
            if (record == null || record.terrain == null)
                continue;

            Domain domain = record.terrain.domain;
            if (domain != Domain.Land && domain != Domain.Naval)
                continue;

            for (int n = 0; n < record.neighbors.Count; n++)
            {
                if (!byCell.TryGetValue(
                        record.neighbors[n],
                        out BoardTopologyCellRecord neighbor)
                    || neighbor.terrain == null)
                {
                    continue;
                }
                Domain neighborDomain = neighbor.terrain.domain;
                if ((domain == Domain.Land
                        && neighborDomain == Domain.Naval)
                    || (domain == Domain.Naval
                        && neighborDomain == Domain.Land))
                {
                    record.isCoastal = true;
                    neighbor.isCoastal = true;
                }
            }
        }
    }

    private static Domain ResolvePrimaryDomain(
        ConstructionData construction,
        StructureData structure,
        TerrainTypeData terrain)
    {
        if (construction != null)
            return construction.domain;
        if (structure != null)
            return structure.domain;
        return terrain != null ? terrain.domain : Domain.Land;
    }

    private static bool IsPotentialLandingSurface(
        ConstructionData construction,
        StructureData structure,
        TerrainTypeData terrain)
    {
        if (construction != null)
            return construction.allowAircraftTakeoffAndLanding;
        if (structure != null)
        {
            IReadOnlyList<StructureAirOpsTerrainRule> rules =
                structure.aircraftOpsByTerrain;
            if (rules == null)
                return false;
            for (int i = 0; i < rules.Count; i++)
            {
                StructureAirOpsTerrainRule rule = rules[i];
                if (rule == null || rule.terrainData == null)
                    continue;
                if (rule.terrainData == terrain
                    || (!string.IsNullOrWhiteSpace(terrain?.id)
                        && rule.terrainData.id == terrain.id))
                {
                    return rule.allowTakeoffAndLanding;
                }
            }
            return false;
        }
        return terrain != null
            && terrain.allowAircraftTakeoffAndLanding;
    }

    private static bool IsBeach(TerrainTypeData terrain)
    {
        return terrain != null
            && string.Equals(
                terrain.id?.Trim(),
                "beach",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreImmediateNeighbors(
        Tilemap boardTilemap,
        Vector3Int from,
        Vector3Int to)
    {
        var neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(
            boardTilemap,
            from,
            neighbors);
        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int candidate = neighbors[i];
            candidate.z = 0;
            if (candidate == to)
                return true;
        }
        return false;
    }

    private static bool IsCompatibleReference(
        Tilemap reference,
        Tilemap candidate)
    {
        if (reference == null || candidate == null)
            return true;
        if (reference == candidate)
            return true;
        return reference.layoutGrid != null
            && reference.layoutGrid == candidate.layoutGrid;
    }

    private static string BuildMapId(Tilemap boardTilemap)
    {
        Scene scene = boardTilemap.gameObject.scene;
        string sceneId = !string.IsNullOrWhiteSpace(scene.path)
            ? scene.path
            : scene.name;
        return $"{sceneId}::{boardTilemap.name}";
    }

    private static string ResolveRouteName(
        RoadRouteDefinition route,
        int index)
    {
        return route != null
            && !string.IsNullOrWhiteSpace(route.routeName)
                ? route.routeName.Trim()
                : $"route_{index}";
    }

    private static string TerrainSignature(TerrainTypeData terrain)
    {
        if (terrain == null)
            return "-";
        return $"{StableId(terrain.id)}:{(int)terrain.domain}:" +
            $"{(int)terrain.heightLevel}:" +
            $"{(terrain.allowAircraftTakeoffAndLanding ? 1 : 0)}";
    }

    private static string StructureSignature(StructureData structure)
    {
        if (structure == null)
            return "-";

        var signature = new StringBuilder()
            .Append(StableId(structure.id)).Append(':')
            .Append(structure.priorityOrder).Append(':')
            .Append((int)structure.domain).Append(':')
            .Append((int)structure.heightLevel).Append(':')
            .Append((int)structure.routeNetworkType).Append(':')
            .Append(structure.exigeRotaDeclarada ? 1 : 0).Append(':')
            .Append(structure.roadBoost ? 1 : 0);

        if (structure.skillRulesByTerrain != null)
        {
            for (int i = 0; i < structure.skillRulesByTerrain.Count; i++)
            {
                StructureSkillTerrainRule rule =
                    structure.skillRulesByTerrain[i];
                signature.Append("|rb:");
                if (rule == null)
                {
                    signature.Append('-');
                    continue;
                }

                signature
                    .Append(StableId(
                        rule.terrainData != null
                            ? rule.terrainData.id
                            : string.Empty))
                    .Append('=')
                    .Append((int)rule.roadBoost);
            }
        }

        // Mantem o fingerprint sensivel ao fallback legado enquanto ainda houver
        // assets antigos com roadBoostOff serializado.
        if (structure.descriptionsByTerrain != null)
        {
            for (int i = 0; i < structure.descriptionsByTerrain.Count; i++)
            {
                StructureTerrainDescription pair =
                    structure.descriptionsByTerrain[i];
                if (pair == null || !pair.roadBoostOff)
                    continue;

                signature
                    .Append("|legacy-rb-off:")
                    .Append(StableId(
                        pair.terrainData != null
                            ? pair.terrainData.id
                            : string.Empty));
            }
        }

        return signature.ToString();
    }

    private static string ConstructionSignature(
        ConstructionData construction)
    {
        if (construction == null)
            return "-";

        var signature = new StringBuilder()
            .Append(StableId(construction.id)).Append(':')
            .Append((int)construction.domain).Append(':')
            .Append((int)construction.heightLevel).Append(':')
            .Append(
                construction.allowAircraftTakeoffAndLanding ? 1 : 0)
            .Append(':')
            .Append(Mathf.Max(1, construction.baseMovementCost));
        AppendSkillListSignature(
            signature,
            "req",
            construction.requiredSkillsToEnter);
        AppendSkillListSignature(
            signature,
            "block",
            construction.blockedSkills);
        AppendCostOverrideSignature(
            signature,
            "cost",
            construction.skillCostOverrides);

        if (construction.skillRulesByTerrain != null)
        {
            for (int i = 0;
                 i < construction.skillRulesByTerrain.Count;
                 i++)
            {
                ConstructionSkillTerrainRule terrainRule =
                    construction.skillRulesByTerrain[i];
                signature.Append("|terrain:");
                if (terrainRule == null)
                {
                    signature.Append('-');
                    continue;
                }

                signature.Append(StableId(
                    terrainRule.terrainData != null
                        ? terrainRule.terrainData.id
                        : string.Empty));
                AppendSkillListSignature(
                    signature,
                    "req",
                    terrainRule.requiredSkillsToEnter);
                AppendSkillListSignature(
                    signature,
                    "block",
                    terrainRule.blockedSkills);
                AppendCostOverrideSignature(
                    signature,
                    "cost",
                    terrainRule.skillCostOverrides);

            }
        }

        if (construction.inheritStructureRulesOnlyOn != null)
        {
            for (int i = 0;
                 i < construction.inheritStructureRulesOnlyOn.Count;
                 i++)
            {
                TerrainTypeData terrain =
                    construction.inheritStructureRulesOnlyOn[i];
                signature
                    .Append("|inherit-structure:")
                    .Append(StableId(
                        terrain != null
                            ? terrain.id
                            : string.Empty));
            }
        }

        if (construction.inheritTerrainRulesOnlyOn != null)
        {
            for (int i = 0;
                 i < construction.inheritTerrainRulesOnlyOn.Count;
                 i++)
            {
                TerrainTypeData terrain =
                    construction.inheritTerrainRulesOnlyOn[i];
                signature
                    .Append("|inherit-terrain:")
                    .Append(StableId(
                        terrain != null
                            ? terrain.id
                            : string.Empty));
            }
        }

        return signature.ToString();
    }

    private static void AppendSkillListSignature(
        StringBuilder signature,
        string label,
        IReadOnlyList<SkillData> skills)
    {
        signature.Append('|').Append(label).Append(':');
        if (skills == null)
            return;

        for (int i = 0; i < skills.Count; i++)
        {
            if (i > 0)
                signature.Append(',');
            SkillData skill = skills[i];
            signature.Append(StableId(
                skill != null ? skill.id : string.Empty));
        }
    }

    private static void AppendCostOverrideSignature(
        StringBuilder signature,
        string label,
        IReadOnlyList<TerrainSkillCostOverride> overrides)
    {
        signature.Append('|').Append(label).Append(':');
        if (overrides == null)
            return;

        for (int i = 0; i < overrides.Count; i++)
        {
            if (i > 0)
                signature.Append(',');
            TerrainSkillCostOverride entry = overrides[i];
            if (entry == null)
            {
                signature.Append('-');
                continue;
            }

            signature
                .Append(StableId(
                    entry.skill != null
                        ? entry.skill.id
                        : string.Empty))
                .Append('=')
                .Append(entry.autonomyCost);
        }
    }

    private static int CompareCells(
        Vector3Int left,
        Vector3Int right)
    {
        int x = left.x.CompareTo(right.x);
        if (x != 0)
            return x;
        return left.y.CompareTo(right.y);
    }

    private static int CompareRouteEdges(
        BoardTopologyRouteEdgeRecord left,
        BoardTopologyRouteEdgeRecord right)
    {
        BoardTopologyEdgeKey leftKey = left.EdgeKey;
        BoardTopologyEdgeKey rightKey = right.EdgeKey;
        int first = CompareCells(leftKey.a, rightKey.a);
        if (first != 0)
            return first;
        int second = CompareCells(leftKey.b, rightKey.b);
        if (second != 0)
            return second;
        int structure = string.CompareOrdinal(
            StableId(left.structure != null
                ? left.structure.id
                : string.Empty),
            StableId(right.structure != null
                ? right.structure.id
                : string.Empty));
        if (structure != 0)
            return structure;
        return string.CompareOrdinal(
            left.routeName ?? string.Empty,
            right.routeName ?? string.Empty);
    }

    private static string StableId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim();
    }

    private static string FormatCell(Vector3Int cell)
    {
        return $"({cell.x},{cell.y})";
    }
}
