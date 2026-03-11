using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class ServicoDoComandoSensor
{
    public static bool CollectOptions(
        TeamId activeTeam,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        List<ServicoDoComandoOption> output,
        out string reason,
        List<ServicoDoComandoInvalidOption> invalidOutput = null)
    {
        reason = string.Empty;
        if (output == null)
            return false;

        output.Clear();
        invalidOutput?.Clear();

        if (activeTeam == TeamId.Neutral)
        {
            reason = "Time ativo invalido para servico do comando.";
            return false;
        }

        if (map == null)
        {
            reason = "Tilemap indisponivel para avaliar servico do comando.";
            return false;
        }

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (units == null || units.Length <= 0)
        {
            reason = "Nenhuma unidade em cena.";
            return false;
        }

        Dictionary<Vector3Int, List<UnitManager>> unitsByCell = BuildUnitsByCell(units);
        CollectConstructionSupplierOptions(activeTeam, map, terrainDatabase, unitsByCell, output, invalidOutput);
        CollectConstructionSupplierEmbarkedOptions(activeTeam, map, terrainDatabase, units, output, invalidOutput);
        CollectTransportSupplierOptions(activeTeam, units, output, invalidOutput);

        output.Sort((a, b) =>
        {
            if (ReferenceEquals(a, b))
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            bool aEmbarked = IsEmbarkedCommandOption(a);
            bool bEmbarked = IsEmbarkedCommandOption(b);
            if (aEmbarked != bEmbarked)
                return aEmbarked ? -1 : 1;

            if (aEmbarked && bEmbarked)
            {
                int aSupplierId = a.sourceSupplierUnit != null ? a.sourceSupplierUnit.InstanceId : 0;
                int bSupplierId = b.sourceSupplierUnit != null ? b.sourceSupplierUnit.InstanceId : 0;
                int supplierCmp = aSupplierId.CompareTo(bSupplierId);
                if (supplierCmp != 0)
                    return supplierCmp;

                int aSeat = ResolveEmbarkedSeatOrder(a.sourceSupplierUnit, a.targetUnit);
                int bSeat = ResolveEmbarkedSeatOrder(b.sourceSupplierUnit, b.targetUnit);
                int seatCmp = aSeat.CompareTo(bSeat);
                if (seatCmp != 0)
                    return seatCmp;
            }

            int yCmp = a.targetCell.y.CompareTo(b.targetCell.y);
            if (yCmp != 0)
                return yCmp;
            int xCmp = a.targetCell.x.CompareTo(b.targetCell.x);
            if (xCmp != 0)
                return xCmp;

            string left = a.displayLabel ?? string.Empty;
            string right = b.displayLabel ?? string.Empty;
            return string.Compare(left, right, System.StringComparison.OrdinalIgnoreCase);
        });

        ReorderTransportFamiliesInQueue(output);

        if (output.Count <= 0)
        {
            reason = "Sem unidades elegiveis sobre construcoes supridoras aliadas ou embarcadas em transportadores supridores.";
            return false;
        }

        return true;
    }

    private static void ReorderTransportFamiliesInQueue(List<ServicoDoComandoOption> output)
    {
        if (output == null || output.Count <= 1)
            return;

        var result = new List<ServicoDoComandoOption>(output.Count);
        var used = new HashSet<ServicoDoComandoOption>();
        var suppliersWithEmbarked = new HashSet<UnitManager>();

        for (int i = 0; i < output.Count; i++)
        {
            ServicoDoComandoOption option = output[i];
            if (!IsEmbarkedCommandOption(option))
                continue;

            UnitManager supplier = option.sourceSupplierUnit;
            if (supplier == null || suppliersWithEmbarked.Contains(supplier))
                continue;

            suppliersWithEmbarked.Add(supplier);

            // Embarcados primeiro (ja estao ordenados por assento no sort base).
            for (int j = 0; j < output.Count; j++)
            {
                ServicoDoComandoOption embarked = output[j];
                if (!IsEmbarkedCommandOption(embarked))
                    continue;
                if (embarked.sourceSupplierUnit != supplier)
                    continue;
                if (used.Contains(embarked))
                    continue;

                result.Add(embarked);
                used.Add(embarked);
            }

            // Em seguida o proprio transportador, se ele for elegivel na mesma ordem.
            for (int j = 0; j < output.Count; j++)
            {
                ServicoDoComandoOption supplierSelf = output[j];
                if (supplierSelf == null || used.Contains(supplierSelf))
                    continue;
                if (supplierSelf.targetUnit != supplier)
                    continue;

                result.Add(supplierSelf);
                used.Add(supplierSelf);
                break;
            }
        }

        // Demais opcoes preservam a ordem original ja classificada.
        for (int i = 0; i < output.Count; i++)
        {
            ServicoDoComandoOption option = output[i];
            if (option == null || used.Contains(option))
                continue;
            result.Add(option);
            used.Add(option);
        }

        output.Clear();
        output.AddRange(result);
    }

    private static bool IsEmbarkedCommandOption(ServicoDoComandoOption option)
    {
        if (option == null || option.sourceSupplierUnit == null || option.targetUnit == null)
            return false;
        return option.targetUnit.IsEmbarked && option.targetUnit.EmbarkedTransporter == option.sourceSupplierUnit;
    }

    private static int ResolveEmbarkedSeatOrder(UnitManager supplier, UnitManager passenger)
    {
        if (supplier == null || passenger == null)
            return int.MaxValue;

        IReadOnlyList<UnitTransportSeatRuntime> seats = supplier.TransportedUnitSlots;
        if (seats == null || seats.Count <= 0)
            return int.MaxValue;

        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat != null && seat.embarkedUnit == passenger)
                return i;
        }

        return int.MaxValue;
    }

    private static void CollectConstructionSupplierOptions(
        TeamId activeTeam,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Dictionary<Vector3Int, List<UnitManager>> unitsByCell,
        List<ServicoDoComandoOption> output,
        List<ServicoDoComandoInvalidOption> invalidOutput)
    {
        ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (constructions == null || constructions.Length <= 0)
            return;

        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || (int)construction.TeamId != (int)activeTeam)
                continue;
            if (!construction.TryResolveConstructionData(out ConstructionData data) || data == null || !data.isSupplier)
                continue;
            if (!construction.CanProvideSupplies)
                continue;

            List<ServiceData> services = GetDistinctServicesFromConstruction(construction);
            if (services.Count <= 0)
                continue;
            if (!HasAtLeastOneOperationalServiceWithStock(construction, services))
                continue;

            int limit = Mathf.Max(0, data.maxUnitsServedPerTurn);
            if (limit <= 0)
                continue;

            Dictionary<SupplyData, int> stock = BuildStockMap(construction);
            if (stock.Count <= 0)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (!unitsByCell.TryGetValue(cell, out List<UnitManager> unitsOnTop) || unitsOnTop == null || unitsOnTop.Count <= 0)
                continue;

            for (int u = 0; u < unitsOnTop.Count; u++)
            {
                UnitManager target = unitsOnTop[u];
                if (target == null || target.IsEmbarked || !target.gameObject.activeInHierarchy)
                    continue;

                if ((int)target.TeamId != (int)activeTeam)
                {
                    AppendInvalid(invalidOutput, construction, null, target, cell, "Unidade alvo de outro time.");
                    continue;
                }

                if (target.ReceivedSuppliesThisTurn)
                {
                    AppendInvalid(invalidOutput, construction, null, target, cell, "Unidade ja recebeu suprimentos nesta rodada.");
                    continue;
                }

                if (!TryEvaluateConstructionCandidate(
                        construction,
                        data,
                        target,
                        map,
                        terrainDatabase,
                        services,
                        stock,
                        out bool forceLand,
                        out bool forceTakeoff,
                        out bool forceSurface,
                        out Domain plannedDomain,
                        out HeightLevel plannedHeight,
                        out string invalidReason))
                {
                    AppendInvalid(invalidOutput, construction, null, target, cell, invalidReason);
                    continue;
                }

                output.Add(new ServicoDoComandoOption
                {
                    sourceConstruction = construction,
                    sourceSupplierUnit = null,
                    targetUnit = target,
                    targetCell = cell,
                    forceLandBeforeSupply = forceLand,
                    forceTakeoffBeforeSupply = forceTakeoff,
                    forceSurfaceBeforeSupply = forceSurface,
                    plannedServiceDomain = plannedDomain,
                    plannedServiceHeight = plannedHeight,
                    displayLabel = $"{target.name} @ {cell.x},{cell.y} via {ResolveConstructionLabel(construction)}",
                    plannedServices = CollectPlannedServiceLabels(target, services, stock)
                });
            }
        }
    }

    private static void CollectTransportSupplierOptions(
        TeamId activeTeam,
        UnitManager[] units,
        List<ServicoDoComandoOption> output,
        List<ServicoDoComandoInvalidOption> invalidOutput)
    {
        if (units == null || units.Length <= 0)
            return;

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager supplier = units[i];
            if (supplier == null || !supplier.gameObject.activeInHierarchy || supplier.IsEmbarked)
                continue;
            if ((int)supplier.TeamId != (int)activeTeam)
                continue;
            if (!supplier.TryGetUnitData(out UnitData supplierData) || supplierData == null || !supplierData.isSupplier)
                continue;

            List<ServiceData> services = GetDistinctServicesFromUnit(supplier);
            if (services.Count <= 0)
                continue;
            if (!HasAtLeastOneOperationalServiceWithStock(supplier, services))
                continue;

            int limit = Mathf.Max(0, supplierData.maxUnitsServedPerTurn);
            if (limit <= 0)
                continue;

            Dictionary<SupplyData, int> stock = BuildStockMap(supplier);
            if (stock.Count <= 0)
                continue;

            IReadOnlyList<UnitTransportSeatRuntime> seats = supplier.TransportedUnitSlots;
            if (seats == null || seats.Count <= 0)
                continue;

            for (int s = 0; s < seats.Count; s++)
            {
                UnitTransportSeatRuntime seat = seats[s];
                UnitManager target = seat != null ? seat.embarkedUnit : null;
                if (target == null || !target.IsEmbarked || target.EmbarkedTransporter != supplier)
                    continue;

                Vector3Int cell = supplier.CurrentCellPosition;
                cell.z = 0;

                if ((int)target.TeamId != (int)activeTeam)
                {
                    AppendInvalid(invalidOutput, null, supplier, target, cell, "Unidade embarcada de outro time.");
                    continue;
                }

                if (target.ReceivedSuppliesThisTurn)
                {
                    AppendInvalid(invalidOutput, null, supplier, target, cell, "Unidade embarcada ja recebeu suprimentos nesta rodada.");
                    continue;
                }

                if (!TryEvaluateEmbarkedCandidateFromSupplierUnit(
                        supplier,
                        target,
                        services,
                        stock,
                        out string invalidReason))
                {
                    AppendInvalid(invalidOutput, null, supplier, target, cell, invalidReason);
                    continue;
                }

                output.Add(new ServicoDoComandoOption
                {
                    sourceConstruction = null,
                    sourceSupplierUnit = supplier,
                    targetUnit = target,
                    targetCell = cell,
                    forceLandBeforeSupply = false,
                    forceTakeoffBeforeSupply = false,
                    forceSurfaceBeforeSupply = false,
                    plannedServiceDomain = supplier.GetDomain(),
                    plannedServiceHeight = supplier.GetHeightLevel(),
                    displayLabel = $"{target.name} @ {cell.x},{cell.y} via {ResolveSupplierUnitLabel(supplier)} (embarcada)",
                    plannedServices = CollectPlannedServiceLabels(target, services, stock)
                });
            }
        }
    }

    private static void CollectConstructionSupplierEmbarkedOptions(
        TeamId activeTeam,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        UnitManager[] units,
        List<ServicoDoComandoOption> output,
        List<ServicoDoComandoInvalidOption> invalidOutput)
    {
        if (units == null || units.Length <= 0)
            return;

        ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (constructions == null || constructions.Length <= 0)
            return;

        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || (int)construction.TeamId != (int)activeTeam)
                continue;
            if (!construction.TryResolveConstructionData(out ConstructionData data) || data == null || !data.isSupplier)
                continue;
            if (!construction.CanProvideSupplies)
                continue;

            List<ServiceData> services = GetDistinctServicesFromConstruction(construction);
            if (services.Count <= 0)
                continue;
            if (!HasAtLeastOneOperationalServiceWithStock(construction, services))
                continue;

            int limit = Mathf.Max(0, data.maxUnitsServedPerTurn);
            if (limit <= 0)
                continue;

            Dictionary<SupplyData, int> stock = BuildStockMap(construction);
            if (stock.Count <= 0)
                continue;

            Vector3Int constructionCell = construction.CurrentCellPosition;
            constructionCell.z = 0;

            for (int u = 0; u < units.Length; u++)
            {
                UnitManager transporter = units[u];
                if (transporter == null || !transporter.gameObject.activeInHierarchy || transporter.IsEmbarked)
                    continue;
                if ((int)transporter.TeamId != (int)activeTeam)
                    continue;

                Vector3Int transporterCell = transporter.CurrentCellPosition;
                transporterCell.z = 0;
                if (transporterCell != constructionCell)
                    continue;

                // Evita duplicar opcoes que ja entram pela trilha de transportador supridor.
                if (transporter.TryGetUnitData(out UnitData transporterData) &&
                    transporterData != null &&
                    transporterData.isSupplier)
                    continue;

                IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
                if (seats == null || seats.Count <= 0)
                    continue;

                for (int s = 0; s < seats.Count; s++)
                {
                    UnitTransportSeatRuntime seat = seats[s];
                    UnitManager target = seat != null ? seat.embarkedUnit : null;
                    if (target == null || !target.IsEmbarked || target.EmbarkedTransporter != transporter)
                        continue;

                    if ((int)target.TeamId != (int)activeTeam)
                    {
                        AppendInvalid(invalidOutput, construction, transporter, target, constructionCell, "Unidade embarcada de outro time.");
                        continue;
                    }

                    if (target.ReceivedSuppliesThisTurn)
                    {
                        AppendInvalid(invalidOutput, construction, transporter, target, constructionCell, "Unidade embarcada ja recebeu suprimentos nesta rodada.");
                        continue;
                    }

                    if (!TryEvaluateConstructionCandidate(
                            construction,
                            data,
                            target,
                            map,
                            terrainDatabase,
                            services,
                            stock,
                            out bool forceLand,
                            out bool forceTakeoff,
                            out bool forceSurface,
                            out Domain plannedDomain,
                            out HeightLevel plannedHeight,
                            out string invalidReason))
                    {
                        AppendInvalid(invalidOutput, construction, transporter, target, constructionCell, invalidReason);
                        continue;
                    }

                    output.Add(new ServicoDoComandoOption
                    {
                        sourceConstruction = construction,
                        sourceSupplierUnit = transporter,
                        targetUnit = target,
                        targetCell = constructionCell,
                        forceLandBeforeSupply = forceLand,
                        forceTakeoffBeforeSupply = forceTakeoff,
                        forceSurfaceBeforeSupply = forceSurface,
                        plannedServiceDomain = plannedDomain,
                        plannedServiceHeight = plannedHeight,
                        displayLabel = $"{target.name} @ {constructionCell.x},{constructionCell.y} via {ResolveConstructionLabel(construction)} (embarcada em {ResolveSupplierUnitLabel(transporter)})",
                        plannedServices = CollectPlannedServiceLabels(target, services, stock)
                    });
                }
            }
        }
    }

    private static bool TryEvaluateEmbarkedCandidateFromSupplierUnit(
        UnitManager supplier,
        UnitManager target,
        List<ServiceData> services,
        Dictionary<SupplyData, int> stock,
        out string reason)
    {
        reason = string.Empty;
        if (supplier == null || target == null || services == null || services.Count <= 0)
        {
            reason = "Contexto de transporte/suprimento invalido.";
            return false;
        }

        if (!target.IsEmbarked || target.EmbarkedTransporter != supplier)
        {
            reason = "Alvo nao esta embarcado no transportador supridor.";
            return false;
        }

        bool hasAnyNeedMatch = false;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;

            if (service.apenasEntreSupridores && !PodeSuprirSensor.IsSupplier(target))
                continue;

            bool needsThisService = PodeSuprirSensor.UnitNeedsService(target, service);
            if (!needsThisService)
                continue;

            hasAnyNeedMatch = true;
            if (PodeSuprirSensor.HasSupplyForService(service, stock))
                return true;
        }

        reason = hasAnyNeedMatch
            ? "Sem estoque dos suprimentos exigidos pelos servicos aplicaveis."
            : "Unidade nao precisa de nenhum servico oferecido pelo transportador.";
        return false;
    }

    private static bool TryEvaluateConstructionCandidate(
        ConstructionManager sourceConstruction,
        ConstructionData sourceData,
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
        plannedServiceDomain = sourceConstruction != null ? sourceConstruction.GetDomain() : Domain.Land;
        plannedServiceHeight = sourceConstruction != null ? sourceConstruction.GetHeightLevel() : HeightLevel.Surface;
        reason = string.Empty;

        if (sourceConstruction == null || sourceData == null || target == null || services == null || services.Count <= 0)
        {
            reason = "Contexto de servico do comando invalido.";
            return false;
        }

        if (!IsDomainCompatibleForConstructionCommandService(
                sourceConstruction,
                sourceData,
                target,
                boardMap,
                terrainDatabase,
                out forceLandBeforeSupply,
                out forceTakeoffBeforeSupply,
                out forceSurfaceBeforeSupply,
                out plannedServiceDomain,
                out plannedServiceHeight,
                out string domainReason))
        {
            reason = string.IsNullOrWhiteSpace(domainReason)
                ? "Dominio/altura incompativeis para servico do comando."
                : domainReason;
            return false;
        }

        bool hasAnyNeedMatch = false;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;

            if (service.apenasEntreSupridores && !PodeSuprirSensor.IsSupplier(target))
                continue;

            bool needsThisService = PodeSuprirSensor.UnitNeedsService(target, service);
            if (!needsThisService)
                continue;

            hasAnyNeedMatch = true;
            if (PodeSuprirSensor.HasSupplyForService(service, stock))
                return true;
        }

        reason = hasAnyNeedMatch
            ? "Sem estoque dos suprimentos exigidos pelos servicos aplicaveis."
            : "Unidade nao precisa de nenhum servico oferecido pela construcao.";
        return false;
    }

    private static bool IsDomainCompatibleForConstructionCommandService(
        ConstructionManager sourceConstruction,
        ConstructionData sourceData,
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
        plannedServiceDomain = sourceConstruction != null ? sourceConstruction.GetDomain() : Domain.Land;
        plannedServiceHeight = sourceConstruction != null ? sourceConstruction.GetHeightLevel() : HeightLevel.Surface;
        reason = string.Empty;

        if (sourceConstruction == null || sourceData == null || target == null)
        {
            reason = "Contexto de construcao/alvo invalido.";
            return false;
        }

        if (sourceData.supplierOperationDomains == null || sourceData.supplierOperationDomains.Count <= 0)
        {
            reason = "Construcao sem supplierOperationDomains configurado.";
            return false;
        }

        Domain targetDomain = target.GetDomain();
        HeightLevel targetHeight = target.GetHeightLevel();
        if (ConstructionSupportsOperationDomain(sourceData, targetDomain, targetHeight))
        {
            plannedServiceDomain = targetDomain;
            plannedServiceHeight = targetHeight;
            return true;
        }

        bool submergedSub = targetDomain == Domain.Submarine && targetHeight == HeightLevel.Submerged;
        if (submergedSub)
        {
            bool supportsNavalSurface = ConstructionSupportsOperationDomain(sourceData, Domain.Naval, HeightLevel.Surface);
            bool targetCanSurface = target.SupportsLayerMode(Domain.Naval, HeightLevel.Surface);
            if (supportsNavalSurface && targetCanSurface)
            {
                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;
                if (!PodeSuprirSensor.CanUseLayerModeAtCurrentCellForSupply(
                        target,
                        boardMap,
                        terrainDatabase,
                        targetCell,
                        Domain.Naval,
                        HeightLevel.Surface,
                        out string surfaceReason))
                {
                    reason = $"Alvo submerso pode emergir, mas o hex atual nao aceita Naval/Surface ({surfaceReason}).";
                    return false;
                }
                if (target.IsLayerChangeBlockedByForcedLock(Domain.Naval, HeightLevel.Surface, out string lockReason))
                {
                    reason = lockReason;
                    return false;
                }

                forceSurfaceBeforeSupply = true;
                plannedServiceDomain = Domain.Naval;
                plannedServiceHeight = HeightLevel.Surface;
                return true;
            }
        }

        if (targetDomain != sourceConstruction.GetDomain())
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

            Domain constructionDomain = sourceConstruction.GetDomain();
            HeightLevel constructionHeight = sourceConstruction.GetHeightLevel();
            bool constructionSupportsNative = ConstructionSupportsOperationDomain(sourceData, constructionDomain, constructionHeight);
            if (landing != null && landing.status && constructionSupportsNative)
            {
                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;
                if (target.IsLayerChangeBlockedByForcedLock(constructionDomain, constructionHeight, out string lockReason))
                {
                    reason = lockReason;
                    return false;
                }
                if (!PodeSuprirSensor.CanUseLayerModeAtCurrentCellForSupply(
                        target,
                        boardMap,
                        terrainDatabase,
                        targetCell,
                        constructionDomain,
                        constructionHeight,
                        out string landingLayerReason))
                {
                    reason = $"Pouso valido, mas o hex atual nao permite atendimento em {constructionDomain}/{constructionHeight} ({landingLayerReason}).";
                    return false;
                }

                forceLandBeforeSupply = true;
                plannedServiceDomain = constructionDomain;
                plannedServiceHeight = constructionHeight;
                return true;
            }

            if (PodeSuprirSensor.IsAirFamilyUnit(target))
            {
                PodeDecolarReport takeoff = PodeDecolarSensor.Evaluate(target, boardMap, terrainDatabase);
                HeightLevel takeoffHeight = PodeSuprirSensor.ResolveTakeoffServiceHeight(target);
                bool sourceSupportsTakeoffLayer = ConstructionSupportsOperationDomain(sourceData, Domain.Air, takeoffHeight);

                if (takeoff != null && takeoff.status && sourceSupportsTakeoffLayer)
                {
                    Vector3Int targetCell = target.CurrentCellPosition;
                    targetCell.z = 0;
                    if (target.IsLayerChangeBlockedByForcedLock(Domain.Air, takeoffHeight, out string lockReason))
                    {
                        reason = lockReason;
                        return false;
                    }
                    if (!PodeSuprirSensor.CanUseLayerModeAtCurrentCellForSupply(
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

        reason = "Dominio/altura incompativeis para servico do comando.";
        return false;
    }

    private static bool ConstructionSupportsOperationDomain(ConstructionData constructionData, Domain domain, HeightLevel height)
    {
        if (constructionData == null || constructionData.supplierOperationDomains == null)
            return false;

        for (int i = 0; i < constructionData.supplierOperationDomains.Count; i++)
        {
            TerrainLayerMode mode = constructionData.supplierOperationDomains[i];
            if (mode.domain == domain && mode.heightLevel == height)
                return true;
        }

        return false;
    }

    private static Dictionary<Vector3Int, List<UnitManager>> BuildUnitsByCell(UnitManager[] units)
    {
        var map = new Dictionary<Vector3Int, List<UnitManager>>();
        if (units == null)
            return map;

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsEmbarked)
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            if (!map.TryGetValue(cell, out List<UnitManager> bucket) || bucket == null)
            {
                bucket = new List<UnitManager>(1);
                map[cell] = bucket;
            }

            bucket.Add(unit);
        }

        return map;
    }

    private static List<ServiceData> GetDistinctServicesFromConstruction(ConstructionManager construction)
    {
        var list = new List<ServiceData>();
        if (construction == null)
            return list;

        IReadOnlyList<ServiceData> services = construction.OfferedServices;
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

    private static Dictionary<SupplyData, int> BuildStockMap(ConstructionManager construction)
    {
        var map = new Dictionary<SupplyData, int>();
        if (construction == null)
            return map;

        IReadOnlyList<ConstructionSupplyOffer> offers = construction.OfferedSupplies;
        if (offers == null)
            return map;

        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply == null)
                continue;

            int amount = Mathf.Max(0, offer.quantity);
            if (amount <= 0)
                continue;

            if (map.TryGetValue(offer.supply, out int current))
                map[offer.supply] = current + amount;
            else
                map.Add(offer.supply, amount);
        }

        return map;
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

    private static bool HasAtLeastOneOperationalServiceWithStock(ConstructionManager sourceConstruction, List<ServiceData> services)
    {
        if (sourceConstruction == null || services == null)
            return false;

        Dictionary<SupplyData, int> stock = BuildStockMap(sourceConstruction);
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;
            if (PodeSuprirSensor.HasSupplyForService(service, stock))
                return true;
        }

        return false;
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
            if (PodeSuprirSensor.HasSupplyForService(service, stock))
                return true;
        }

        return false;
    }

    private static List<string> CollectPlannedServiceLabels(UnitManager target, List<ServiceData> services, Dictionary<SupplyData, int> stock)
    {
        List<string> labels = new List<string>();
        if (target == null || services == null || stock == null)
            return labels;

        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;
            if (service.apenasEntreSupridores && !PodeSuprirSensor.IsSupplier(target))
                continue;
            if (!PodeSuprirSensor.UnitNeedsService(target, service))
                continue;
            if (!PodeSuprirSensor.HasSupplyForService(service, stock))
                continue;

            string label = ResolveServiceLabel(service);
            if (!labels.Contains(label))
                labels.Add(label);
        }

        return labels;
    }

    private static void AppendInvalid(
        List<ServicoDoComandoInvalidOption> invalidOutput,
        ConstructionManager construction,
        UnitManager supplierUnit,
        UnitManager target,
        Vector3Int cell,
        string reason)
    {
        if (invalidOutput == null)
            return;

        invalidOutput.Add(new ServicoDoComandoInvalidOption
        {
            sourceConstruction = construction,
            sourceSupplierUnit = supplierUnit,
            targetUnit = target,
            targetCell = cell,
            reason = string.IsNullOrWhiteSpace(reason) ? "Candidato invalido." : reason
        });
    }

    private static string ResolveConstructionLabel(ConstructionManager construction)
    {
        if (construction == null)
            return "(construcao)";
        if (!string.IsNullOrWhiteSpace(construction.ConstructionDisplayName))
            return construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(construction.ConstructionId))
            return construction.ConstructionId;
        return construction.name;
    }

    private static string ResolveSupplierUnitLabel(UnitManager supplier)
    {
        if (supplier == null)
            return "(supridor)";
        if (!string.IsNullOrWhiteSpace(supplier.UnitDisplayName))
            return supplier.UnitDisplayName;
        if (!string.IsNullOrWhiteSpace(supplier.UnitId))
            return supplier.UnitId;
        return supplier.name;
    }

    private static string ResolveServiceLabel(ServiceData service)
    {
        if (service == null)
            return "(servico)";
        if (!string.IsNullOrWhiteSpace(service.displayName))
            return service.displayName;
        if (!string.IsNullOrWhiteSpace(service.id))
            return service.id;
        return service.name;
    }
}
