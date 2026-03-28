using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeEmergirReport
{
    public bool status;
    public string explicacao;
}

public static class PodeEmergirSensor
{
    public static PodeEmergirReport Evaluate(
        UnitManager unit,
        Tilemap map,
        TerrainDatabase terrainDatabase)
    {
        var report = new PodeEmergirReport
        {
            status = false,
            explicacao = "Contexto nao avaliado."
        };

        bool sensorLogs = SensorLogGate.IsPodeEmergirEnabled();

        if (unit == null)
        {
            report.explicacao = "Selecione uma unidade.";
            return report;
        }

        if (unit.IsEmbarked)
        {
            report.explicacao = "Unidade embarcada nao pode emergir.";
            return report;
        }

        if (map == null)
        {
            report.explicacao = "Tilemap base nao encontrado.";
            return report;
        }

        if (terrainDatabase == null)
        {
            report.explicacao = "TerrainDatabase nao encontrado.";
            return report;
        }

        Domain currentDomain = unit.GetDomain();
        HeightLevel currentHeight = unit.GetHeightLevel();

        if (currentDomain != Domain.Submarine || currentHeight != HeightLevel.Submerged)
        {
            report.explicacao = "Unidade nao esta submersa (Submarine/Submerged).";
            return report;
        }

        if (!unit.SupportsLayerMode(Domain.Naval, HeightLevel.Surface))
        {
            report.explicacao = "Unidade nao suporta camada Naval/Surface.";
            return report;
        }

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;

        if (!CanSurfaceAtCell(unit, map, terrainDatabase, cell, out string cellReason))
        {
            report.explicacao = cellReason;
            if (sensorLogs)
                SensorLogGate.Log("PodeEmergir", $"{unit.name} nao pode emergir em {cell}: {cellReason}");
            return report;
        }

        report.status = true;
        report.explicacao = "Emersao disponivel neste hex.";

        if (sensorLogs)
            SensorLogGate.Log("PodeEmergir", $"{unit.name} pode emergir em {cell}.");

        return report;
    }

    public static bool CanSurfaceAtCell(
        UnitManager unit,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out string reason)
    {
        reason = string.Empty;

        if (unit == null || map == null)
        {
            reason = "Contexto de mapa/unidade invalido.";
            return false;
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(map, cell);
        if (construction != null)
        {
            if (!construction.SupportsLayerMode(Domain.Naval, HeightLevel.Surface))
            {
                reason = "Construcao no hex nao suporta Naval/Surface.";
                return false;
            }

            if (!UnitPassesSkillRequirement(unit, construction.GetRequiredSkillsToEnter()))
            {
                reason = "Unidade nao possui skill exigida pela construcao para emergir.";
                return false;
            }

            if (UnitHasAnyBlockedSkill(unit, construction.GetBlockedSkillsToEnter()))
            {
                reason = "Unidade possui skill bloqueada pela construcao para emergir.";
                return false;
            }

            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(map, cell);
        if (structure != null)
        {
            if (!StructureSupportsLayerMode(structure, Domain.Naval, HeightLevel.Surface))
            {
                reason = "Estrutura no hex nao suporta Naval/Surface.";
                return false;
            }

            bool isAdditionalMode = StructureSupportsAdditionalLayerMode(structure, Domain.Naval, HeightLevel.Surface);
            if (!TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrainWithStructure))
            {
                reason = "Terreno do hex nao encontrado para validar emersao com estrutura.";
                return false;
            }

            if (!isAdditionalMode && !UnitPassesSkillRequirement(unit, structure.GetRequiredSkillsToEnter(terrainWithStructure)))
            {
                reason = "Unidade nao possui skill exigida pela estrutura para emergir.";
                return false;
            }

            if (UnitHasAnyBlockedSkill(unit, structure.GetBlockedSkillsToEnter(terrainWithStructure)))
            {
                reason = "Unidade possui skill bloqueada pela estrutura para emergir.";
                return false;
            }

            if (!TerrainSupportsLayerMode(terrainWithStructure, Domain.Naval, HeightLevel.Surface))
            {
                reason = "Terreno no hex (com estrutura) nao suporta Naval/Surface.";
                return false;
            }

            return true;
        }

        if (!TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
        {
            reason = "Terreno do hex nao encontrado para validar emersao.";
            return false;
        }

        if (!TerrainSupportsLayerMode(terrain, Domain.Naval, HeightLevel.Surface))
        {
            reason = "Terreno no hex nao suporta Naval/Surface.";
            return false;
        }

        if (!UnitPassesSkillRequirement(unit, terrain.requiredSkillsToEnter))
        {
            reason = "Unidade nao possui skill exigida pelo terreno para emergir.";
            return false;
        }

        if (UnitHasAnyBlockedSkill(unit, terrain.blockedSkills))
        {
            reason = "Unidade possui skill bloqueada pelo terreno para emergir.";
            return false;
        }

        return true;
    }

    private static bool TerrainSupportsLayerMode(TerrainTypeData terrain, Domain domain, HeightLevel heightLevel)
    {
        if (terrain == null)
            return false;

        if (terrain.domain == domain && terrain.heightLevel == heightLevel)
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

    private static bool StructureSupportsLayerMode(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null)
            return false;

        if (structure.domain == domain && structure.heightLevel == heightLevel)
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

    private static bool StructureSupportsAdditionalLayerMode(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null || structure.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < structure.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = structure.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool TryResolveTerrainAtCell(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDb == null)
            return false;

        cell.z = 0;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDb.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
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
            Tilemap m = maps[i];
            if (m == null)
                continue;

            TileBase other = m.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDb.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }

    private static bool UnitPassesSkillRequirement(UnitManager unit, IReadOnlyList<SkillData> requiredSkills)
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

    private static bool UnitHasAnyBlockedSkill(UnitManager unit, IReadOnlyList<SkillData> blockedSkills)
    {
        if (unit == null || blockedSkills == null || blockedSkills.Count <= 0)
            return false;

        for (int i = 0; i < blockedSkills.Count; i++)
        {
            SkillData blockedSkill = blockedSkills[i];
            if (blockedSkill == null)
                continue;

            if (unit.HasSkill(blockedSkill))
                return true;
        }

        return false;
    }
}
