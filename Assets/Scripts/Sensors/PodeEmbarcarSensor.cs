using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeEmbarcarSensor
{
    public static bool CollectOptions(
        UnitManager selectedUnit,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        int remainingMovementPoints,
        List<PodeEmbarcarOption> output,
        List<PodeEmbarcarInvalidOption> invalidOutput = null)
    {
        if (output == null)
            return false;

        output.Clear();
        invalidOutput?.Clear();
        if (selectedUnit == null || map == null || selectedUnit.IsEmbarked)
            return false;
        if (!selectedUnit.TryGetUnitData(out UnitData sourceData) || sourceData == null)
            return false;

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        Vector3Int sourceCell = selectedUnit.CurrentCellPosition;
        sourceCell.z = 0;
        UnitMovementPathRules.GetImmediateHexNeighbors(map, sourceCell, neighbors);

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int cell = neighbors[i];
            cell.z = 0;

            UnitManager transporter = UnitOccupancyRules.GetUnitAtCell(map, cell, selectedUnit);
            if (!IsValidTransporter(selectedUnit, transporter, out UnitData transporterData, out string transporterReason))
            {
                AppendInvalid(invalidOutput, selectedUnit, transporter, cell, -1, transporterReason, -1, remainingMovementPoints);
                continue;
            }
            if (!CanEmbarkAtTransporterContext(map, terrainDatabase, transporter, transporterData, out string contextReason))
            {
                AppendInvalid(invalidOutput, selectedUnit, transporter, cell, -1, contextReason, -1, remainingMovementPoints);
                continue;
            }

            bool hasValidSlot = false;
            for (int slotIndex = 0; slotIndex < transporterData.transportSlots.Count; slotIndex++)
            {
                UnitTransportSlotRule slot = transporterData.transportSlots[slotIndex];
                if (!CanUseSlot(selectedUnit, sourceData, slot, out string slotReason))
                {
                    AppendInvalid(invalidOutput, selectedUnit, transporter, cell, slotIndex, slotReason, -1, remainingMovementPoints);
                    continue;
                }

                if (!TryResolveEmbarkCost(map, terrainDatabase, selectedUnit, cell, out int embarkCost, out string costReason))
                {
                    AppendInvalid(invalidOutput, selectedUnit, transporter, cell, slotIndex, costReason, -1, remainingMovementPoints);
                    continue;
                }

                if (remainingMovementPoints < embarkCost)
                {
                    AppendInvalid(
                        invalidOutput,
                        selectedUnit,
                        transporter,
                        cell,
                        slotIndex,
                        $"Movimento insuficiente para embarcar (restante={remainingMovementPoints}, custo={embarkCost}).",
                        embarkCost,
                        remainingMovementPoints);
                    continue;
                }

                int occupied = CountSlotOccupancy(map, transporter, slot);
                if (occupied >= Mathf.Max(1, slot.capacity))
                {
                    AppendInvalid(invalidOutput, selectedUnit, transporter, cell, slotIndex, $"Slot lotado ({occupied}/{Mathf.Max(1, slot.capacity)}).", embarkCost, remainingMovementPoints);
                    continue;
                }

                hasValidSlot = true;

                output.Add(new PodeEmbarcarOption
                {
                    sourceUnit = selectedUnit,
                    transporterUnit = transporter,
                    transporterSlotIndex = slotIndex,
                    displayLabel = BuildLabel(transporter, slot, occupied, embarkCost, remainingMovementPoints),
                    enterCost = embarkCost,
                    remainingMovementBeforeEmbark = Mathf.Max(0, remainingMovementPoints)
                });
            }

            if (!hasValidSlot && (transporterData.transportSlots == null || transporterData.transportSlots.Count == 0))
                AppendInvalid(invalidOutput, selectedUnit, transporter, cell, -1, "Transportador sem slots configurados.", -1, remainingMovementPoints);
        }

        return output.Count > 0;
    }

    private static bool CanEmbarkAtTransporterContext(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        UnitManager transporter,
        UnitData transporterData,
        out string reason)
    {
        reason = string.Empty;
        if (map == null || transporter == null || transporterData == null)
        {
            reason = "Contexto de embarque invalido (mapa/transportador/dados).";
            return false;
        }

        bool hasConstructionFilter = transporterData.allowedEmbarkConstructions != null && transporterData.allowedEmbarkConstructions.Count > 0;
        bool hasTerrainFilter = transporterData.allowedEmbarkTerrains != null && transporterData.allowedEmbarkTerrains.Count > 0;
        Vector3Int cell = transporter.CurrentCellPosition;
        cell.z = 0;

        if (!hasConstructionFilter && !hasTerrainFilter)
        {
            if (TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrainByCell) && terrainByCell != null)
            {
                bool supports = TerrainSupportsMode(terrainByCell, transporter.GetDomain(), transporter.GetHeightLevel());
                if (!supports && transporter.GetDomain() == Domain.Air && terrainByCell.alwaysAllowAirDomain)
                    supports = true;
                if (supports)
                    return true;
            }

            reason = "Transportador fora de um contexto compativel com seu dominio/altura.";
            return false;
        }

        bool constructionMatch = false;
        if (hasConstructionFilter)
        {
            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(map, cell);
            if (construction != null && construction.TryResolveConstructionData(out ConstructionData constructionData) && constructionData != null)
                constructionMatch = transporterData.allowedEmbarkConstructions.Contains(constructionData);
        }

        bool terrainMatch = false;
        if (hasTerrainFilter && TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) && terrain != null)
            terrainMatch = transporterData.allowedEmbarkTerrains.Contains(terrain);

        bool allowed = constructionMatch || terrainMatch;
        if (!allowed)
            reason = "Contexto nao permitido (transportador fora dos terrenos/construcoes de embarque).";
        return allowed;
    }

    private static bool IsValidTransporter(UnitManager sourceUnit, UnitManager transporter, out UnitData transporterData, out string reason)
    {
        reason = string.Empty;
        transporterData = null;
        if (sourceUnit == null || transporter == null || transporter == sourceUnit)
        {
            reason = "Nao ha transportador adjacente valido.";
            return false;
        }
        if ((int)sourceUnit.TeamId != (int)transporter.TeamId)
        {
            reason = "Transportador adjacente eh de outro time.";
            return false;
        }
        if (transporter.GetDomain() == Domain.Air)
        {
            reason = "Embarque bloqueado: transportador em dominio aereo.";
            return false;
        }
        if (!transporter.TryGetUnitData(out transporterData) || transporterData == null)
        {
            reason = "Transportador adjacente sem UnitData valido.";
            return false;
        }
        if (!transporterData.isTransporter || transporterData.transportSlots == null || transporterData.transportSlots.Count == 0)
        {
            reason = "Unidade adjacente nao eh transportador (ou sem slot).";
            return false;
        }
        return true;
    }

    private static bool CanUseSlot(UnitManager sourceUnit, UnitData sourceData, UnitTransportSlotRule slot, out string reason)
    {
        reason = string.Empty;
        if (sourceUnit == null || sourceData == null || slot == null)
        {
            reason = "Slot invalido para avaliacao.";
            return false;
        }

        if (!SlotSupportsPassengerLayer(slot, sourceUnit))
        {
            reason = $"Slot incompativel com dominio/altura do passageiro ({sourceUnit.GetDomain()}/{sourceUnit.GetHeightLevel()}).";
            return false;
        }

        if (slot.allowedClasses != null && slot.allowedClasses.Count > 0)
        {
            if (!slot.allowedClasses.Contains(sourceData.unitClass))
            {
                reason = $"Classe do passageiro ({sourceData.unitClass}) nao permitida no slot.";
                return false;
            }
        }

        if (slot.requiredSkills != null && slot.requiredSkills.Count > 0)
        {
            bool hasAny = false;
            for (int i = 0; i < slot.requiredSkills.Count; i++)
            {
                SkillData required = slot.requiredSkills[i];
                if (required == null)
                    continue;
                if (sourceUnit.HasSkill(required))
                {
                    hasAny = true;
                    break;
                }
            }

            if (!hasAny)
            {
                reason = "Falta skill obrigatoria para o slot.";
                return false;
            }
        }

        if (slot.blockedSkills != null && slot.blockedSkills.Count > 0)
        {
            for (int i = 0; i < slot.blockedSkills.Count; i++)
            {
                SkillData blocked = slot.blockedSkills[i];
                if (blocked == null)
                    continue;
                if (sourceUnit.HasSkill(blocked))
                {
                    string skillName = !string.IsNullOrWhiteSpace(blocked.displayName) ? blocked.displayName : blocked.name;
                    reason = $"Passageiro bloqueado por skill: {skillName}.";
                    return false;
                }
            }
        }

        return true;
    }

    private static int CountSlotOccupancy(Tilemap map, UnitManager transporter, UnitTransportSlotRule slot)
    {
        if (map == null || transporter == null || slot == null)
            return 0;

        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;

        int count = 0;
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || !unit.IsEmbarked || unit == transporter)
                continue;

            Vector3Int cell = unit.BoardTilemap == map
                ? unit.CurrentCellPosition
                : HexCoordinates.WorldToCell(map, unit.transform.position);
            cell.z = 0;
            if (cell != transporterCell)
                continue;

            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;

            if (slot.allowedClasses != null && slot.allowedClasses.Count > 0 && !slot.allowedClasses.Contains(data.unitClass))
                continue;

            count++;
        }

        return count;
    }

    private static string BuildLabel(UnitManager transporter, UnitTransportSlotRule slot, int occupied, int enterCost, int remaining)
    {
        string transporterName = transporter != null ? transporter.name : "Transportador";
        string slotName = !string.IsNullOrWhiteSpace(slot.slotId) ? slot.slotId : "slot";
        int cap = Mathf.Max(1, slot.capacity);
        return $"{transporterName} | {slotName} ({occupied}/{cap}) | custo={enterCost} | movRest={Mathf.Max(0, remaining)}";
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
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
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

            if (terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveEmbarkCost(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        UnitManager passenger,
        Vector3Int transporterCell,
        out int cost,
        out string reason)
    {
        cost = 0;
        reason = string.Empty;
        if (map == null || passenger == null)
        {
            reason = "Contexto invalido para calcular custo de embarque.";
            return false;
        }

        transporterCell.z = 0;
        if (UnitMovementPathRules.TryGetEnterCellCost(map, passenger, transporterCell, terrainDatabase, out int normalCost))
        {
            cost = Mathf.Max(1, normalCost);
            return true;
        }

        // Fallback de embarque: quando o passageiro nao pisaria no hex (ex.: naval), usa custo do terreno base.
        if (!TryResolveTerrainAtCell(map, terrainDatabase, transporterCell, out TerrainTypeData terrain) || terrain == null)
        {
            reason = "Sem terreno valido para fallback de custo de embarque.";
            return false;
        }

        if (!UnitPassesTerrainSkillRequirement(passenger, terrain))
        {
            reason = "Passageiro nao cumpre skill minima do terreno para custo de embarque.";
            return false;
        }

        cost = GetAutonomyCostWithSkillOverrides(terrain.basicAutonomyCost, terrain.skillCostOverrides, passenger);
        cost = Mathf.Max(1, cost);
        return true;
    }

    private static bool UnitPassesTerrainSkillRequirement(UnitManager unit, TerrainTypeData terrain)
    {
        if (unit == null || terrain == null)
            return false;

        if (terrain.requiredSkillsToEnter == null || terrain.requiredSkillsToEnter.Count == 0)
            return true;

        for (int i = 0; i < terrain.requiredSkillsToEnter.Count; i++)
        {
            SkillData skill = terrain.requiredSkillsToEnter[i];
            if (skill == null)
                continue;
            if (unit.HasSkill(skill))
                return true;
        }

        return false;
    }

    private static int GetAutonomyCostWithSkillOverrides(
        int baseCost,
        IReadOnlyList<TerrainSkillCostOverride> overrides,
        UnitManager unit)
    {
        int safeBase = Mathf.Max(1, baseCost);
        if (unit == null || overrides == null)
            return safeBase;

        for (int i = 0; i < overrides.Count; i++)
        {
            TerrainSkillCostOverride entry = overrides[i];
            if (entry == null || entry.skill == null)
                continue;

            if (unit.HasSkill(entry.skill))
                return Mathf.Max(1, entry.autonomyCost);
        }

        return safeBase;
    }

    private static bool SlotSupportsPassengerLayer(UnitTransportSlotRule slot, UnitManager sourceUnit)
    {
        if (slot == null || sourceUnit == null)
            return false;

        if (slot.allowedLayerModes == null || slot.allowedLayerModes.Count == 0)
            return sourceUnit.GetDomain() == Domain.Land && sourceUnit.GetHeightLevel() == HeightLevel.Surface;

        for (int i = 0; i < slot.allowedLayerModes.Count; i++)
        {
            TransportSlotLayerMode mode = slot.allowedLayerModes[i];
            if (mode.domain == sourceUnit.GetDomain() && mode.heightLevel == sourceUnit.GetHeightLevel())
                return true;
        }

        return false;
    }

    private static bool TerrainSupportsMode(TerrainTypeData terrain, Domain domain, HeightLevel height)
    {
        if (terrain == null)
            return false;

        if (terrain.domain == domain && terrain.heightLevel == height)
            return true;

        if (terrain.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < terrain.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrain.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == height)
                return true;
        }

        return false;
    }

    private static void AppendInvalid(
        List<PodeEmbarcarInvalidOption> invalidOutput,
        UnitManager source,
        UnitManager transporter,
        Vector3Int cell,
        int slotIndex,
        string reason,
        int enterCost,
        int remainingMovement)
    {
        if (invalidOutput == null)
            return;

        invalidOutput.Add(new PodeEmbarcarInvalidOption
        {
            sourceUnit = source,
            transporterUnit = transporter,
            evaluatedCell = cell,
            transporterSlotIndex = slotIndex,
            reason = string.IsNullOrWhiteSpace(reason) ? "Sem motivo detalhado." : reason,
            enterCost = enterCost,
            remainingMovementBeforeEmbark = Mathf.Max(0, remainingMovement)
        });
    }
}
