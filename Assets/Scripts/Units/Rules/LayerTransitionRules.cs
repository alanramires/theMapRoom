using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Fonte comum para a pergunta estrutural "a camada de destino cabe neste hex?".
/// Nao avalia timing da operacao, combustível, exposicao, locks ou estado de turno.
/// </summary>
public static class LayerTransitionRules
{
    public static bool TryResolvePrimaryLayerAtCell(
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out Domain domain,
        out HeightLevel height,
        out string source)
    {
        domain = Domain.Land;
        height = HeightLevel.Surface;
        source = string.Empty;
        if (boardMap == null)
            return false;

        cell.z = 0;
        ConstructionManager construction =
            ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null)
        {
            domain = construction.GetDomain();
            height = construction.GetHeightLevel();
            source = "construcao";
            return true;
        }

        StructureData structure =
            StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        if (structure != null)
        {
            // A estrutura define o andar principal; o terreno sob ela continua
            // participando da validacao do par em CanUseLayerModeAtCell.
            domain = structure.domain;
            height = structure.heightLevel;
            source = "estrutura+terreno";
            return true;
        }

        if (!TryResolveTerrainAtCell(
                boardMap, terrainDatabase, cell, out TerrainTypeData terrain)
            || terrain == null)
        {
            return false;
        }

        domain = terrain.domain;
        height = terrain.heightLevel;
        source = "terreno";
        return true;
    }

    public static bool CanUseLayerModeAtCell(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        Domain targetDomain,
        HeightLevel targetHeight,
        out string reason)
    {
        reason = string.Empty;
        cell.z = 0;

        if (unit == null || boardMap == null)
        {
            reason = "Contexto de mapa/unidade invalido.";
            return false;
        }

        if (!unit.SupportsLayerMode(targetDomain, targetHeight))
        {
            reason = $"Unidade nao suporta {targetDomain}/{targetHeight}.";
            return false;
        }

        if (!UnitOccupancyRules.CanEndLayerTransitionAtCell(
                boardMap, cell, unit, targetDomain, targetHeight, out UnitManager blocker))
        {
            string blockerName = blocker != null && !string.IsNullOrWhiteSpace(blocker.UnitDisplayName)
                ? blocker.UnitDisplayName
                : "aliado";
            reason = $"Camada {targetDomain}/{targetHeight} ocupada por {blockerName}.";
            return false;
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null)
            return CanUseConstruction(unit, construction, targetDomain, targetHeight, out reason);

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        if (structure != null)
        {
            TryResolveTerrainAtCell(boardMap, terrainDatabase, cell, out TerrainTypeData terrainWithStructure);
            return CanUseStructureAndTerrain(
                unit, structure, terrainWithStructure, targetDomain, targetHeight, out reason);
        }

        if (!TryResolveTerrainAtCell(boardMap, terrainDatabase, cell, out TerrainTypeData terrain) ||
            terrain == null)
        {
            reason = "Terreno do hex nao encontrado para validar camada.";
            return false;
        }

        return CanUseTerrain(unit, terrain, targetDomain, targetHeight, "terreno", out reason);
    }

    public static bool TerrainSupportsLayerMode(
        TerrainTypeData terrain,
        Domain domain,
        HeightLevel heightLevel)
    {
        if (terrain == null)
            return false;
        if (terrain.domain == domain && terrain.heightLevel == heightLevel)
            return true;
        if (domain == Domain.Air && terrain.alwaysAllowAirDomain)
            return true;
        return ContainsMode(terrain.aditionalDomainsAllowed, domain, heightLevel);
    }

    public static bool StructureSupportsLayerMode(
        StructureData structure,
        Domain domain,
        HeightLevel heightLevel)
    {
        if (structure == null)
            return false;
        if (structure.domain == domain && structure.heightLevel == heightLevel)
            return true;
        if (domain == Domain.Air && structure.alwaysAllowAirDomain)
            return true;
        return ContainsMode(structure.aditionalDomainsAllowed, domain, heightLevel);
    }

    public static bool TryResolveTerrainAtCell(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDatabase == null)
            return false;

        cell.z = 0;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null &&
            terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) &&
            byMainTile != null)
        {
            terrain = byMainTile;
            return true;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase other = map.GetTile(cell);
            if (other != null &&
                terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) &&
                byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }

    private static bool CanUseConstruction(
        UnitManager unit,
        ConstructionManager construction,
        Domain domain,
        HeightLevel height,
        out string reason)
    {
        reason = string.Empty;
        if (!construction.SupportsLayerMode(domain, height))
        {
            reason = $"Construcao no hex nao suporta {domain}/{height}.";
            return false;
        }

        if (construction.TryResolveConstructionData(out ConstructionData data) &&
            data != null &&
            ContainsMode(data.forceEndMovementOnTerrainDomainForDomains, domain, height))
        {
            reason = "Construcao no hex exige outra camada e impede a transicao.";
            return false;
        }

        return PassesSkillRules(
            unit,
            construction.GetRequiredSkillsToEnter(),
            construction.GetBlockedSkillsToEnter(),
            "construcao",
            out reason);
    }

    private static bool CanUseStructureAndTerrain(
        UnitManager unit,
        StructureData structure,
        TerrainTypeData terrain,
        Domain domain,
        HeightLevel height,
        out string reason)
    {
        reason = string.Empty;
        if (!StructureSupportsLayerMode(structure, domain, height))
        {
            reason = $"Estrutura no hex nao suporta {domain}/{height}.";
            return false;
        }

        if (terrain == null)
        {
            reason = "Terreno do hex nao encontrado para validar camada com estrutura.";
            return false;
        }

        StructureNavalOpsTerrainRule pairRule = null;
        bool hasPairRule = structure.TryGetNavalOpsRuleForTerrain(terrain, out pairRule);
        IReadOnlyList<TerrainLayerMode> forcedModes = hasPairRule
            ? pairRule.forceEndMovementOnTerrainDomainForDomains
            : structure.forceEndMovementOnTerrainDomainForDomains;
        if (ContainsMode(forcedModes, domain, height))
        {
            reason = "Combinacao de estrutura e terreno exige outra camada.";
            return false;
        }

        bool additionalMode = ContainsMode(structure.aditionalDomainsAllowed, domain, height);
        if (!additionalMode &&
            !PassesSkillRules(
                unit,
                structure.GetRequiredSkillsToEnter(terrain),
                structure.GetBlockedSkillsToEnter(terrain),
                "estrutura",
                out reason))
        {
            return false;
        }
        if (additionalMode &&
            HasAnyBlockedSkill(unit, structure.GetBlockedSkillsToEnter(terrain)))
        {
            reason = "Unidade possui skill bloqueada pela estrutura para trocar de camada.";
            return false;
        }

        return CanUseTerrain(unit, terrain, domain, height, "terreno sob a estrutura", out reason);
    }

    private static bool CanUseTerrain(
        UnitManager unit,
        TerrainTypeData terrain,
        Domain domain,
        HeightLevel height,
        string source,
        out string reason)
    {
        reason = string.Empty;
        if (!TerrainSupportsLayerMode(terrain, domain, height))
        {
            reason = $"{source} nao suporta {domain}/{height}.";
            return false;
        }

        if (ContainsMode(terrain.forceEndMovementOnTerrainDomainForDomains, domain, height))
        {
            reason = $"{source} exige outra camada e impede a transicao.";
            return false;
        }

        return PassesSkillRules(
            unit,
            terrain.requiredSkillsToEnter,
            terrain.blockedSkills,
            source,
            out reason);
    }

    private static bool PassesSkillRules(
        UnitManager unit,
        IReadOnlyList<SkillData> required,
        IReadOnlyList<SkillData> blocked,
        string source,
        out string reason)
    {
        reason = string.Empty;
        if (HasAnyBlockedSkill(unit, blocked))
        {
            reason = $"Unidade possui skill bloqueada por {source} para trocar de camada.";
            return false;
        }

        if (HasAnyRequiredSkill(unit, required))
            return true;

        bool hasRequirement = false;
        if (required != null)
        {
            for (int i = 0; i < required.Count; i++)
                hasRequirement |= required[i] != null;
        }

        if (!hasRequirement)
            return true;

        reason = $"Unidade nao possui skill exigida por {source} para trocar de camada.";
        return false;
    }

    private static bool HasAnyRequiredSkill(UnitManager unit, IReadOnlyList<SkillData> skills)
    {
        if (unit == null || skills == null)
            return false;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];
            if (skill != null && unit.HasSkill(skill))
                return true;
        }
        return false;
    }

    private static bool HasAnyBlockedSkill(UnitManager unit, IReadOnlyList<SkillData> skills)
    {
        if (unit == null || skills == null)
            return false;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];
            if (skill != null && unit.HasSkill(skill))
                return true;
        }
        return false;
    }

    private static bool ContainsMode(
        IReadOnlyList<TerrainLayerMode> modes,
        Domain domain,
        HeightLevel height)
    {
        if (modes == null)
            return false;
        for (int i = 0; i < modes.Count; i++)
        {
            TerrainLayerMode mode = modes[i];
            if (mode.domain == domain && mode.heightLevel == height)
                return true;
        }
        return false;
    }
}
