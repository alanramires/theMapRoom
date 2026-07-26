using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PodeSubmergirReport
{
    public bool status;
    public string explicacao;
    public Vector3Int cell;
}

/// <summary>
/// Consulta pura e autoritativa para Naval/Surface -> Submarine/Submerged.
/// Nao move a unidade, nao altera locks e nao atualiza deteccao/FOW.
/// </summary>
public static class PodeSubmergirSensor
{
    public static bool CanSubmergeAtCell(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out string reason)
    {
        PodeSubmergirReport report = Evaluate(unit, boardMap, terrainDatabase, cell);
        reason = report != null ? report.explicacao : "PodeSubmergir sem resultado.";
        return report != null && report.status;
    }

    public static PodeSubmergirReport Evaluate(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int? atCell = null)
    {
        Vector3Int cell = atCell ?? (unit != null ? unit.CurrentCellPosition : Vector3Int.zero);
        cell.z = 0;

        var report = new PodeSubmergirReport
        {
            status = false,
            explicacao = "Contexto nao avaliado.",
            cell = cell
        };

        if (unit == null)
        {
            report.explicacao = "Selecione uma unidade.";
            return report;
        }

        if (unit.IsEmbarked)
        {
            report.explicacao = "Unidade embarcada nao pode submergir.";
            return report;
        }

        if (boardMap == null)
        {
            report.explicacao = "Tilemap base nao encontrado.";
            return report;
        }

        if (terrainDatabase == null)
        {
            report.explicacao = "TerrainDatabase nao encontrado.";
            return report;
        }

        if (unit.GetDomain() != Domain.Naval || unit.GetHeightLevel() != HeightLevel.Surface)
        {
            report.explicacao = "Submergir exige unidade em Naval/Surface.";
            return report;
        }

        if (!unit.SupportsLayerMode(Domain.Submarine, HeightLevel.Submerged))
        {
            report.explicacao = "Unidade nao suporta Submarine/Submerged.";
            return report;
        }

        if (unit.HasFiredThisTurn)
        {
            report.explicacao = "Unidade disparou nesta rodada e permanece exposta na superficie.";
            return report;
        }

        if (unit.IsLayerChangeBlockedByForcedLock(
                Domain.Submarine,
                HeightLevel.Submerged,
                out string lockReason))
        {
            report.explicacao = lockReason;
            return report;
        }

        if (unit.IsCurrentlyObservedByOpponent())
        {
            report.explicacao = "Unidade detectada recentemente por um oponente nao pode submergir.";
            return report;
        }

        if (!CanUseSubmergedLayerAtCell(
                unit,
                boardMap,
                terrainDatabase,
                cell,
                out string cellReason))
        {
            report.explicacao = cellReason;
            return report;
        }

        report.status = true;
        report.explicacao = "Submersao disponivel neste hex.";
        return report;
    }

    private static bool CanUseSubmergedLayerAtCell(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out string reason)
    {
        reason = string.Empty;
        cell.z = 0;

        if (ForcesSurfaceAtCell(boardMap, terrainDatabase, cell, out string forceReason))
        {
            reason = forceReason;
            return false;
        }

        if (!UnitOccupancyRules.CanEndLayerTransitionAtCell(
                boardMap,
                cell,
                unit,
                Domain.Submarine,
                HeightLevel.Submerged,
                out UnitManager blocker))
        {
            string blockerName = blocker != null && !string.IsNullOrWhiteSpace(blocker.UnitDisplayName)
                ? blocker.UnitDisplayName
                : "aliado";
            reason = $"Camada Submarine/Submerged ocupada por {blockerName}.";
            return false;
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null)
        {
            if (!construction.SupportsLayerMode(Domain.Submarine, HeightLevel.Submerged))
            {
                reason = "Construcao no hex nao suporta Submarine/Submerged.";
                return false;
            }

            if (!UnitPassesSkillRequirement(unit, construction.GetRequiredSkillsToEnter()))
            {
                reason = "Unidade nao possui skill exigida pela construcao para submergir.";
                return false;
            }

            if (UnitHasAnyBlockedSkill(unit, construction.GetBlockedSkillsToEnter()))
            {
                reason = "Unidade possui skill bloqueada pela construcao para submergir.";
                return false;
            }

            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        if (structure != null)
        {
            TryResolveTerrainAtCell(boardMap, terrainDatabase, cell, out TerrainTypeData terrainWithStructure);

            if (!StructureSupportsSubmerged(structure))
            {
                reason = "Estrutura no hex nao suporta Submarine/Submerged.";
                return false;
            }

            bool usesAdditionalStructureMode = StructureSupportsAdditionalSubmergedMode(structure);
            if (!usesAdditionalStructureMode &&
                !UnitPassesSkillRequirement(unit, structure.GetRequiredSkillsToEnter(terrainWithStructure)))
            {
                reason = "Unidade nao possui skill exigida pela estrutura para submergir.";
                return false;
            }

            if (UnitHasAnyBlockedSkill(unit, structure.GetBlockedSkillsToEnter(terrainWithStructure)))
            {
                reason = "Unidade possui skill bloqueada pela estrutura para submergir.";
                return false;
            }

            if (terrainWithStructure == null)
            {
                reason = "Terreno do hex nao encontrado para validar submersao com estrutura.";
                return false;
            }

            if (!TerrainSupportsSubmerged(terrainWithStructure))
            {
                reason = "Terreno no hex (com estrutura) nao suporta Submarine/Submerged.";
                return false;
            }

            return true;
        }

        if (!TryResolveTerrainAtCell(boardMap, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
        {
            reason = "Terreno do hex nao encontrado para validar submersao.";
            return false;
        }

        if (!TerrainSupportsSubmerged(terrain))
        {
            reason = "Terreno no hex nao suporta Submarine/Submerged.";
            return false;
        }

        if (!UnitPassesSkillRequirement(unit, terrain.requiredSkillsToEnter))
        {
            reason = "Unidade nao possui skill exigida pelo terreno para submergir.";
            return false;
        }

        if (UnitHasAnyBlockedSkill(unit, terrain.blockedSkills))
        {
            reason = "Unidade possui skill bloqueada pelo terreno para submergir.";
            return false;
        }

        return true;
    }

    private static bool ForcesSurfaceAtCell(
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out string reason)
    {
        reason = string.Empty;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null &&
            construction.TryResolveConstructionData(out ConstructionData constructionData) &&
            ContainsSubmergedMode(constructionData != null
                ? constructionData.forceEndMovementOnTerrainDomainForDomains
                : null))
        {
            reason = "Construcao no hex exige emersao e impede submersao.";
            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        bool hasTerrain = TryResolveTerrainAtCell(
            boardMap,
            terrainDatabase,
            cell,
            out TerrainTypeData terrain) && terrain != null;

        if (structure != null)
        {
            StructureNavalOpsTerrainRule pairRule = null;
            bool hasPairRule = hasTerrain && structure.TryGetNavalOpsRuleForTerrain(terrain, out pairRule);
            IReadOnlyList<TerrainLayerMode> structureModes = hasPairRule
                ? pairRule.forceEndMovementOnTerrainDomainForDomains
                : structure.forceEndMovementOnTerrainDomainForDomains;

            if (ContainsSubmergedMode(structureModes))
            {
                reason = "Combinacao de estrutura e terreno exige emersao e impede submersao.";
                return true;
            }
        }

        if (hasTerrain && ContainsSubmergedMode(terrain.forceEndMovementOnTerrainDomainForDomains))
        {
            reason = structure != null
                ? "Terreno sob a estrutura exige emersao e impede submersao."
                : "Terreno no hex exige emersao e impede submersao.";
            return true;
        }

        return false;
    }

    private static bool TerrainSupportsSubmerged(TerrainTypeData terrain)
    {
        if (terrain == null)
            return false;
        if (terrain.domain == Domain.Submarine && terrain.heightLevel == HeightLevel.Submerged)
            return true;
        return ContainsSubmergedMode(terrain.aditionalDomainsAllowed);
    }

    private static bool StructureSupportsSubmerged(StructureData structure)
    {
        if (structure == null)
            return false;
        if (structure.domain == Domain.Submarine && structure.heightLevel == HeightLevel.Submerged)
            return true;
        return ContainsSubmergedMode(structure.aditionalDomainsAllowed);
    }

    private static bool StructureSupportsAdditionalSubmergedMode(StructureData structure)
    {
        return structure != null && ContainsSubmergedMode(structure.aditionalDomainsAllowed);
    }

    private static bool ContainsSubmergedMode(IReadOnlyList<TerrainLayerMode> modes)
    {
        if (modes == null)
            return false;

        for (int i = 0; i < modes.Count; i++)
        {
            TerrainLayerMode mode = modes[i];
            if (mode.domain == Domain.Submarine && mode.heightLevel == HeightLevel.Submerged)
                return true;
        }

        return false;
    }

    private static bool TryResolveTerrainAtCell(
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
            if (other == null)
                continue;

            if (terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) &&
                byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }

    private static bool UnitPassesSkillRequirement(
        UnitManager unit,
        IReadOnlyList<SkillData> requiredSkills)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;
        if (unit == null)
            return false;

        bool hasAnyValidRequiredSkill = false;
        for (int i = 0; i < requiredSkills.Count; i++)
        {
            SkillData requiredSkill = requiredSkills[i];
            if (requiredSkill == null)
                continue;

            hasAnyValidRequiredSkill = true;
            if (unit.HasSkill(requiredSkill))
                return true;
        }

        return !hasAnyValidRequiredSkill;
    }

    private static bool UnitHasAnyBlockedSkill(
        UnitManager unit,
        IReadOnlyList<SkillData> blockedSkills)
    {
        if (unit == null || blockedSkills == null)
            return false;

        for (int i = 0; i < blockedSkills.Count; i++)
        {
            SkillData blockedSkill = blockedSkills[i];
            if (blockedSkill != null && unit.HasSkill(blockedSkill))
                return true;
        }

        return false;
    }
}
