using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeSuprirSensor
{
    public static bool CollectOptions(
        UnitManager supplier,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        List<PodeSuprirOption> output,
        out string reason,
        List<PodeSuprirInvalidOption> invalidOutput = null)
    {
        reason = string.Empty;
        if (output == null)
            return false;

        output.Clear();
        invalidOutput?.Clear();

        if (supplier == null)
        {
            reason = "Selecione um supridor.";
            return false;
        }

        if (supplier.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode suprir.";
            return false;
        }

        if (!supplier.TryGetUnitData(out UnitData supplierData) || supplierData == null || !supplierData.isSupplier)
        {
            reason = "Unidade selecionada nao e supridora.";
            return false;
        }

        bool embarkedOnlyRange = supplierData.serviceRange == SupplierRangeMode.EmbarkedOnly;
        if (!embarkedOnlyRange && !SupportsOperationDomain(supplierData, supplier.GetDomain(), supplier.GetHeightLevel()))
        {
            reason =
                $"Supplier fora do Supplier Operation Domain atual ({supplier.GetDomain()}/{supplier.GetHeightLevel()}). " +
                "Reposicione para um dominio/altura permitido para prestar servicos.";
            return false;
        }

        Tilemap boardMap = map != null ? map : supplier.BoardTilemap;
        if (boardMap == null)
        {
            reason = "Tilemap indisponivel para avaliar suprimento.";
            return false;
        }

        List<ServiceData> services = GetDistinctServicesFromUnit(supplier);
        if (services.Count == 0)
        {
            reason = "Supridor sem servicos configurados.";
            return false;
        }
        if (!HasAtLeastOneOperationalServiceWithStock(supplier, services))
        {
            reason = "Supplier sem servico operacional com estoque disponivel. Atua apenas como hub/carga.";
            return false;
        }

        int limit = Mathf.Max(0, supplierData.maxUnitsServedPerTurn);
        if (limit <= 0)
        {
            reason = "Supplier sem capacidade de atendimento (maxUnitsServedPerTurn=0).";
            return false;
        }

        Dictionary<SupplyData, int> stock = BuildStockMap(supplier);
        if (stock.Count == 0)
        {
            reason = "Supridor sem estoque embarcado.";
            return false;
        }

        Vector3Int origin = supplier.CurrentCellPosition;
        origin.z = 0;

        bool adjacentRange = supplierData.serviceRange == SupplierRangeMode.Adjacent1Hex;
        List<UnitManager> targetsInRange = CollectTargetsInSupplyRange(supplier, boardMap, origin, adjacentRange, embarkedOnlyRange);

        for (int i = 0; i < targetsInRange.Count; i++)
        {
            UnitManager target = targetsInRange[i];
            bool isEmbarkedPassenger = target != null && target.IsEmbarked && target.EmbarkedTransporter == supplier;
            if (target == null || target == supplier || (!target.gameObject.activeInHierarchy && !isEmbarkedPassenger))
                continue;

            Vector3Int cell = target.CurrentCellPosition;
            cell.z = 0;

            if ((int)target.TeamId != (int)supplier.TeamId)
            {
                AppendInvalid(invalidOutput, supplier, target, cell, "Unidade alvo de outro time.");
                continue;
            }

            if (!TryEvaluateSupplyCandidate(
                    supplier,
                    supplierData,
                    target,
                    boardMap,
                    terrainDatabase,
                    services,
                    stock,
                    out bool forceLandBeforeSupply,
                    out bool forceTakeoffBeforeSupply,
                    out bool forceSurfaceBeforeSupply,
                    out Domain plannedServiceDomain,
                    out HeightLevel plannedServiceHeight,
                    out string invalidReason))
            {
                AppendInvalid(invalidOutput, supplier, target, cell, invalidReason);
                continue;
            }

            output.Add(new PodeSuprirOption
            {
                supplierUnit = supplier,
                targetUnit = target,
                targetCell = cell,
                forceSurfaceBeforeSupply = forceSurfaceBeforeSupply,
                forceLandBeforeSupply = forceLandBeforeSupply,
                forceTakeoffBeforeSupply = forceTakeoffBeforeSupply,
                plannedServiceDomain = plannedServiceDomain,
                plannedServiceHeight = plannedServiceHeight,
                displayLabel = $"{target.name} @ {cell.x},{cell.y}"
            });

        }

        if (output.Count <= 0)
        {
            reason = embarkedOnlyRange
                ? "Sem unidades embarcadas validas para suprir."
                : "Sem candidatos adjacentes validos para suprir.";
            return false;
        }

        return true;
    }

    private static List<UnitManager> CollectTargetsInSupplyRange(
        UnitManager supplier,
        Tilemap boardMap,
        Vector3Int originCell,
        bool adjacentRange,
        bool embarkedOnlyRange)
    {
        var result = new List<UnitManager>();
        if (supplier == null)
            return result;

        if (embarkedOnlyRange)
        {
            IReadOnlyList<UnitTransportSeatRuntime> seats = supplier.TransportedUnitSlots;
            if (seats == null || seats.Count <= 0)
                return result;

            for (int i = 0; i < seats.Count; i++)
            {
                UnitTransportSeatRuntime seat = seats[i];
                UnitManager passenger = seat != null ? seat.embarkedUnit : null;
                if (passenger == null || !passenger.IsEmbarked || passenger.EmbarkedTransporter != supplier)
                    continue;
                if (result.Contains(passenger))
                    continue;
                result.Add(passenger);
            }

            return result;
        }

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (units == null || units.Length <= 0)
            return result;

        originCell.z = 0;
        if (!adjacentRange)
        {
            for (int i = 0; i < units.Length; i++)
            {
                UnitManager target = units[i];
                if (target == null || target == supplier)
                    continue;

                Vector3Int cell = target.CurrentCellPosition;
                cell.z = 0;
                if (cell == originCell)
                    result.Add(target);
            }

            return result;
        }
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(boardMap, originCell, neighbors);
        HashSet<Vector3Int> adjacentLookup = new HashSet<Vector3Int>();
        for (int n = 0; n < neighbors.Count; n++)
        {
            Vector3Int neighbor = neighbors[n];
            neighbor.z = 0;
            adjacentLookup.Add(neighbor);
        }

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager target = units[i];
            if (target == null || target == supplier)
                continue;

            Vector3Int cell = target.CurrentCellPosition;
            cell.z = 0;
            if (!adjacentLookup.Contains(cell))
                continue;

            result.Add(target);
        }

        return result;
    }

    private static bool TryEvaluateSupplyCandidate(
        UnitManager supplier,
        UnitData supplierData,
        UnitManager target,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        List<ServiceData> services,
        Dictionary<SupplyData, int> stock,
        out bool forceLandBeforeSupply,
        out bool forceTakeoffBeforeSupply,
        out bool forceSurfaceBeforeSupply,
        out Domain plannedServiceDomain,
        out HeightLevel plannedServiceHeight,
        out string reason)
    {
        forceLandBeforeSupply = false;
        forceTakeoffBeforeSupply = false;
        forceSurfaceBeforeSupply = false;
        plannedServiceDomain = supplier != null ? supplier.GetDomain() : Domain.Land;
        plannedServiceHeight = supplier != null ? supplier.GetHeightLevel() : HeightLevel.Surface;
        reason = string.Empty;

        if (supplier == null || supplierData == null || target == null || services == null || services.Count == 0)
        {
            reason = "Contexto de suprimento invalido.";
            return false;
        }

        bool isEmbarkedPassenger = target.IsEmbarked && target.EmbarkedTransporter == supplier;
        if (!isEmbarkedPassenger)
        {
            bool domainCompatible = IsDomainCompatibleForSupply(
                supplier,
                supplierData,
                target,
                boardMap,
                terrainDatabase,
                out forceLandBeforeSupply,
                out forceTakeoffBeforeSupply,
                out forceSurfaceBeforeSupply,
                out plannedServiceDomain,
                out plannedServiceHeight,
                out string domainReason);
            if (!domainCompatible)
            {
                reason = string.IsNullOrWhiteSpace(domainReason)
                    ? "Dominio/altura incompativeis para suprimento."
                    : domainReason;
                return false;
            }
        }
        else
        {
            plannedServiceDomain = supplier.GetDomain();
            plannedServiceHeight = supplier.GetHeightLevel();
        }

        bool hasAnyNeedMatch = false;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;

            if (service.apenasEntreSupridores && !IsSupplier(target))
                continue;

            bool needsThisService = UnitNeedsService(target, service);
            if (!needsThisService)
                continue;

            hasAnyNeedMatch = true;
            if (HasSupplyForService(service, stock))
                return true;
        }

        reason = hasAnyNeedMatch
            ? "Sem estoque dos suprimentos exigidos pelos servicos aplicaveis."
            : "Unidade nao precisa de nenhum servico oferecido pelo supridor.";
        return false;
    }

    private static bool IsDomainCompatibleForSupply(
        UnitManager supplier,
        UnitData supplierData,
        UnitManager target,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        out bool forceLandBeforeSupply,
        out bool forceTakeoffBeforeSupply,
        out bool forceSurfaceBeforeSupply,
        out Domain plannedServiceDomain,
        out HeightLevel plannedServiceHeight,
        out string reason)
    {
        forceLandBeforeSupply = false;
        forceTakeoffBeforeSupply = false;
        forceSurfaceBeforeSupply = false;
        plannedServiceDomain = supplier != null ? supplier.GetDomain() : Domain.Land;
        plannedServiceHeight = supplier != null ? supplier.GetHeightLevel() : HeightLevel.Surface;
        reason = string.Empty;
        if (supplierData.supplierOperationDomains == null || supplierData.supplierOperationDomains.Count == 0)
        {
            reason = "Supplier sem supplierOperationDomains configurado.";
            return false;
        }

        Domain targetDomain = target.GetDomain();
        HeightLevel targetHeight = target.GetHeightLevel();

        for (int i = 0; i < supplierData.supplierOperationDomains.Count; i++)
        {
            SupplierOperationDomain mode = supplierData.supplierOperationDomains[i];
            if (mode.domain == targetDomain && mode.heightLevel == targetHeight)
            {
                plannedServiceDomain = targetDomain;
                plannedServiceHeight = targetHeight;
                return true;
            }
        }

        bool submergedSub = targetDomain == Domain.Submarine && targetHeight == HeightLevel.Submerged;
        if (submergedSub)
        {
            bool supportsNavalSurface = SupportsOperationDomain(supplierData, Domain.Naval, HeightLevel.Surface);
            bool targetCanSurface = target.SupportsLayerMode(Domain.Naval, HeightLevel.Surface);
            if (supportsNavalSurface && targetCanSurface)
            {
                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;
                if (!CanUseLayerModeAtCurrentCellForSupply(target, boardMap, terrainDatabase, targetCell, Domain.Naval, HeightLevel.Surface, out string surfaceReason))
                {
                    reason = $"Alvo submerso pode emergir, mas o hex atual nao aceita Naval/Surface ({surfaceReason}).";
                    return false;
                }

                forceSurfaceBeforeSupply = true;
                plannedServiceDomain = Domain.Naval;
                plannedServiceHeight = HeightLevel.Surface;
                return true;
            }
        }

        // Dominio diferente: tenta pouso/decolagem como no tool de logistica.
        if (targetDomain != supplier.GetDomain())
        {
            if (boardMap == null || terrainDatabase == null)
            {
                reason = "Dominio incompativel e sem contexto de terreno para validar pouso/decolagem.";
                return false;
            }

            PodePousarReport landing = PodePousarSensor.Evaluate(
                target,
                boardMap,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                useManualRemainingMovement: false,
                manualRemainingMovement: 0);

            if (landing != null && landing.status)
            {
                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;
                if (!CanUseLayerModeAtCurrentCellForSupply(
                        target,
                        boardMap,
                        terrainDatabase,
                        targetCell,
                        supplier.GetDomain(),
                        supplier.GetHeightLevel(),
                        out string landingLayerReason))
                {
                    reason =
                        $"Pouso valido, mas o hex atual nao permite atendimento em {supplier.GetDomain()}/{supplier.GetHeightLevel()} ({landingLayerReason}).";
                    return false;
                }

                forceLandBeforeSupply = true;
                plannedServiceDomain = supplier.GetDomain();
                plannedServiceHeight = supplier.GetHeightLevel();
                return true;
            }

            if (IsAirFamilyUnit(target))
            {
                PodeDecolarReport takeoff = PodeDecolarSensor.Evaluate(target, boardMap, terrainDatabase);
                HeightLevel takeoffHeight = ResolveTakeoffServiceHeight(target);
                bool supplierSupportsTakeoffLayer =
                    SupportsOperationDomain(supplierData, Domain.Air, takeoffHeight) ||
                    (supplier.GetDomain() == Domain.Air && supplier.GetHeightLevel() == takeoffHeight);

                if (takeoff != null && takeoff.status && supplierSupportsTakeoffLayer)
                {
                    Vector3Int targetCell = target.CurrentCellPosition;
                    targetCell.z = 0;
                    if (!CanUseLayerModeAtCurrentCellForSupply(
                            target,
                            boardMap,
                            terrainDatabase,
                            targetCell,
                            Domain.Air,
                            takeoffHeight,
                            out string takeoffLayerReason))
                    {
                        reason = $"Decolagem valida, mas o hex atual nao permite atendimento em Air/{takeoffHeight} ({takeoffLayerReason}).";
                        return false;
                    }

                    forceTakeoffBeforeSupply = true;
                    plannedServiceDomain = Domain.Air;
                    plannedServiceHeight = takeoffHeight;
                    return true;
                }
            }
        }

        reason = "Dominio/altura incompativeis para suprimento.";
        return false;
    }

    private static bool IsAirFamilyUnit(UnitManager unit)
    {
        if (unit == null)
            return false;

        if (unit.GetDomain() == Domain.Air)
            return true;
        if (unit.SupportsLayerMode(Domain.Air, HeightLevel.AirLow) || unit.SupportsLayerMode(Domain.Air, HeightLevel.AirHigh))
            return true;

        if (!unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        return data.IsAircraft();
    }

    private static HeightLevel ResolveTakeoffServiceHeight(UnitManager unit)
    {
        return HeightLevel.AirLow;
    }

    private static bool SupportsOperationDomain(UnitData supplierData, Domain domain, HeightLevel height)
    {
        if (supplierData == null || supplierData.supplierOperationDomains == null)
            return false;

        for (int i = 0; i < supplierData.supplierOperationDomains.Count; i++)
        {
            SupplierOperationDomain mode = supplierData.supplierOperationDomains[i];
            if (mode.domain == domain && mode.heightLevel == height)
                return true;
        }

        return false;
    }

    private static bool UnitNeedsService(UnitManager target, ServiceData service)
    {
        if (target == null || service == null)
            return false;

        if (service.recuperaHp && target.CurrentHP < target.GetMaxHP())
            return true;
        if (service.recuperaAutonomia && target.CurrentFuel < target.MaxFuel)
            return true;
        if (service.recuperaMunicao && HasAnyMissingAmmo(target))
            return true;

        return false;
    }

    private static bool HasAnyMissingAmmo(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = unit.GetEmbarkedWeapons();
        List<UnitEmbarkedWeapon> dataWeapons = data.embarkedWeapons;
        if (runtimeWeapons == null || dataWeapons == null)
            return false;

        int count = Mathf.Min(runtimeWeapons.Count, dataWeapons.Count);
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = dataWeapons[i];
            if (runtime == null || baseline == null)
                continue;

            int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
            if (runtime.squadAmmunition < maxAmmo)
                return true;
        }

        return false;
    }

    private static bool HasSupplyForService(ServiceData service, Dictionary<SupplyData, int> stock)
    {
        if (service == null || stock == null)
            return false;

        if (service.suppliesUsed == null || service.suppliesUsed.Count == 0)
            return true;

        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData supply = service.suppliesUsed[i];
            if (supply == null)
                continue;

            if (stock.TryGetValue(supply, out int amount) && amount > 0)
                return true;
        }

        return false;
    }

    private static Dictionary<SupplyData, int> BuildStockMap(UnitManager supplier)
    {
        var map = new Dictionary<SupplyData, int>();
        if (supplier == null)
            return map;

        IReadOnlyList<UnitEmbarkedSupply> resources = supplier.GetEmbarkedResources();
        if (resources == null)
            return map;

        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;

            int amount = Mathf.Max(0, entry.amount);
            if (amount <= 0)
                continue;

            if (map.TryGetValue(entry.supply, out int current))
                map[entry.supply] = current + amount;
            else
                map.Add(entry.supply, amount);
        }

        return map;
    }

    private static List<ServiceData> GetDistinctServicesFromUnit(UnitManager supplier)
    {
        var list = new List<ServiceData>();
        if (supplier == null)
            return list;

        IReadOnlyList<ServiceData> services = supplier.GetEmbarkedServices();
        if (services == null)
            return list;

        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || list.Contains(service))
                continue;
            list.Add(service);
        }

        return list;
    }

    private static bool IsSupplier(UnitManager unit)
    {
        return unit != null && unit.TryGetUnitData(out UnitData data) && data != null && data.isSupplier;
    }

    private static bool HasAtLeastOneOperationalServiceWithStock(UnitManager supplier, List<ServiceData> services)
    {
        if (supplier == null || services == null)
            return false;

        Dictionary<SupplyData, int> stock = BuildStockMap(supplier);
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;
            if (HasSupplyForService(service, stock))
                return true;
        }

        return false;
    }

    private static bool CanUseLayerModeAtCurrentCellForSupply(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        Domain targetDomain,
        HeightLevel targetHeight,
        out string reason)
    {
        reason = string.Empty;
        if (unit == null || boardMap == null)
        {
            reason = "contexto de mapa/unidade invalido";
            return false;
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null)
        {
            if (!construction.SupportsLayerMode(targetDomain, targetHeight))
            {
                reason = $"construcao nao suporta {targetDomain}/{targetHeight}";
                return false;
            }

            if (!UnitPassesAnyRequiredSkill(unit, construction.GetRequiredSkillsToEnter()))
            {
                reason = "skill exigida pela construcao ausente";
                return false;
            }

            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        if (structure != null)
        {
            if (!StructureSupportsLayerModeForSupply(structure, targetDomain, targetHeight))
            {
                reason = $"estrutura nao suporta {targetDomain}/{targetHeight}";
                return false;
            }

            if (!UnitPassesAnyRequiredSkill(unit, structure.requiredSkillsToEnter))
            {
                reason = "skill exigida pela estrutura ausente";
                return false;
            }

            if (!TryResolveTerrainAtCellForSupply(boardMap, terrainDb, cell, out TerrainTypeData terrainWithStructure) || terrainWithStructure == null)
            {
                reason = "terreno nao encontrado";
                return false;
            }

            if (!TerrainSupportsLayerModeForSupply(terrainWithStructure, targetDomain, targetHeight))
            {
                reason = $"terreno nao suporta {targetDomain}/{targetHeight}";
                return false;
            }

            if (!UnitPassesAnyRequiredSkill(unit, terrainWithStructure.requiredSkillsToEnter))
            {
                reason = "skill exigida pelo terreno ausente";
                return false;
            }

            return true;
        }

        if (!TryResolveTerrainAtCellForSupply(boardMap, terrainDb, cell, out TerrainTypeData terrain) || terrain == null)
        {
            reason = "terreno nao encontrado";
            return false;
        }

        if (!TerrainSupportsLayerModeForSupply(terrain, targetDomain, targetHeight))
        {
            reason = $"terreno nao suporta {targetDomain}/{targetHeight}";
            return false;
        }

        if (!UnitPassesAnyRequiredSkill(unit, terrain.requiredSkillsToEnter))
        {
            reason = "skill exigida pelo terreno ausente";
            return false;
        }

        return true;
    }

    private static bool UnitPassesAnyRequiredSkill(UnitManager unit, IReadOnlyList<SkillData> requiredSkills)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;
        if (unit == null)
            return false;

        for (int i = 0; i < requiredSkills.Count; i++)
        {
            SkillData required = requiredSkills[i];
            if (required == null)
                continue;
            if (unit.HasSkill(required))
                return true;
        }

        return false;
    }

    private static bool TerrainSupportsLayerModeForSupply(TerrainTypeData terrain, Domain domain, HeightLevel heightLevel)
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

    private static bool StructureSupportsLayerModeForSupply(StructureData structure, Domain domain, HeightLevel heightLevel)
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

    private static bool TryResolveTerrainAtCellForSupply(
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
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase altTile = map.GetTile(cell);
            if (altTile == null)
                continue;
            if (terrainDb.TryGetByPaletteTile(altTile, out TerrainTypeData byAltTile) && byAltTile != null)
            {
                terrain = byAltTile;
                return true;
            }
        }

        return false;
    }

    private static void AppendInvalid(
        List<PodeSuprirInvalidOption> invalidOutput,
        UnitManager supplier,
        UnitManager target,
        Vector3Int cell,
        string reason)
    {
        if (invalidOutput == null)
            return;

        invalidOutput.Add(new PodeSuprirInvalidOption
        {
            supplierUnit = supplier,
            targetUnit = target,
            targetCell = cell,
            reason = string.IsNullOrWhiteSpace(reason) ? "Candidato invalido." : reason
        });
    }
}
