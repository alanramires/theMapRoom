using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeTransferirSensor
{
    private const int InfiniteConstructionSupplyQuantity = int.MaxValue;

    public static bool CollectOptions(
        UnitManager supplier,
        Tilemap map,
        List<PodeTransferirOption> output,
        out string reason,
        List<PodeTransferirInvalidOption> invalidOutput = null)
    {
        reason = string.Empty;
        if (output == null)
            return false;

        output.Clear();
        invalidOutput?.Clear();

        if (supplier == null)
        {
            reason = "Selecione um supridor logistico.";
            return false;
        }

        if (supplier.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode transferir.";
            return false;
        }

        if (!supplier.TryGetUnitData(out UnitData supplierData) || supplierData == null || !supplierData.isSupplier)
        {
            reason = "Unidade selecionada nao e supridora.";
            return false;
        }

        int transferLimit = Mathf.Max(0, supplierData.maxUnitsServedPerTurn);
        if (transferLimit <= 0)
        {
            reason = "Supplier sem capacidade de transferencia (maxUnitsServedPerTurn=0).";
            return false;
        }

        if (supplierData.supplierTier == SupplierTier.SelfSupplier)
        {
            reason = "Tier SelfSupplier nao participa do fluxo de transferencia.";
            return false;
        }

        if (!HasTransferService(supplier))
        {
            reason = "Unidade sem servico de transferencia.";
            return false;
        }

        bool collectionEmbarkedOnly = supplierData.collectionRange == SupplierRangeMode.EmbarkedOnly;
        if (!collectionEmbarkedOnly && !SupportsOperationDomain(supplierData, supplier.GetDomain(), supplier.GetHeightLevel()))
        {
            reason =
                $"Supplier fora do Supplier Operation Domain atual ({supplier.GetDomain()}/{supplier.GetHeightLevel()}). " +
                "Reposicione para um dominio/altura permitido para transferir.";
            return false;
        }

        Tilemap boardMap = map != null ? map : supplier.BoardTilemap;
        if (boardMap == null)
        {
            reason = "Tilemap indisponivel para avaliar transferencia.";
            return false;
        }

        Vector3Int origin = supplier.CurrentCellPosition;
        origin.z = 0;

        ConstructionManager alliedConstruction = ResolveAlliedConstructionAtCell(boardMap, origin, supplier.TeamId);
        List<ConstructionManager> constructionsInCollectionRange = CollectConstructionsInCollectionRange(
            supplierData,
            boardMap,
            origin,
            supplier.TeamId);
        List<UnitManager> unitsInCollectionRange = CollectUnitsInCollectionRange(supplier, supplierData, boardMap, origin);
        List<UnitManager> nearbyHubUnits = CollectNearbyHubUnits(unitsInCollectionRange, supplier.TeamId);

        bool isOnAlliedConstruction = alliedConstruction != null;
        if (!isOnAlliedConstruction && nearbyHubUnits.Count <= 0 && constructionsInCollectionRange.Count <= 0)
        {
            reason = "Transferencia exige construcao/hub aliado no collection range.";
            return false;
        }

        switch (supplierData.supplierTier)
        {
            case SupplierTier.Hub:
                CollectHubOptions(
                    supplier,
                    supplierData,
                    alliedConstruction,
                    constructionsInCollectionRange,
                    unitsInCollectionRange,
                    output,
                    invalidOutput);
                break;
            case SupplierTier.Receiver:
                CollectReceiverOptions(
                    supplier,
                    supplierData,
                    alliedConstruction,
                    constructionsInCollectionRange,
                    nearbyHubUnits,
                    output,
                    invalidOutput);
                break;
            default:
                reason = "Tier de supplier nao suportado para transferencia.";
                return false;
        }

        SortTransferOptions(output);

        if (output.Count <= 0)
        {
            reason = "Sem opcoes validas de transferencia neste contexto.";
            return false;
        }

        return true;
    }

    private static void CollectHubOptions(
        UnitManager supplier,
        UnitData supplierData,
        ConstructionManager alliedConstruction,
        List<ConstructionManager> constructionsInCollectionRange,
        List<UnitManager> unitsInCollectionRange,
        List<PodeTransferirOption> output,
        List<PodeTransferirInvalidOption> invalidOutput)
    {
        bool hasEmbarkedStock = GetUnitTotalStock(supplier) > 0;
        bool hasAnyConstructionForFornecimento = false;

        List<UnitManager> transferableTargets = new List<UnitManager>();
        for (int i = 0; i < unitsInCollectionRange.Count; i++)
        {
            UnitManager unit = unitsInCollectionRange[i];
            if (unit == null)
                continue;
            if (!TryGetSupplierData(unit, out UnitData targetData))
                continue;
            if (targetData.supplierTier != SupplierTier.Hub && targetData.supplierTier != SupplierTier.Receiver)
                continue;
            if (!HasTransferService(unit))
                continue;
            if (!IsDomainCompatibleForTransfer(supplier, supplierData, unit))
                continue;
            if (!CanTransferAtLeastOneSupply(supplier, null, unit))
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    unit,
                    null,
                    unit.CurrentCellPosition,
                    TransferFlowMode.Fornecimento,
                    "Alvo sem capacidade disponivel para receber recursos.");
                continue;
            }
            transferableTargets.Add(unit);
        }

        if (constructionsInCollectionRange != null)
        {
            for (int i = 0; i < constructionsInCollectionRange.Count; i++)
            {
                ConstructionManager construction = constructionsInCollectionRange[i];
                if (construction == null)
                    continue;

                if (GetConstructionTotalSupply(construction) > 0)
                {
                    if (!CanTransferAtLeastOneSupply(null, construction, supplier))
                    {
                        AppendInvalid(
                            invalidOutput,
                            supplier,
                            null,
                            construction,
                            construction.CurrentCellPosition,
                            TransferFlowMode.Recebedor,
                            "Supplier sem capacidade disponivel para receber recursos desta construcao.");
                        continue;
                    }

                    output.Add(new PodeTransferirOption
                    {
                        supplierUnit = supplier,
                        targetConstruction = construction,
                        targetCell = construction.CurrentCellPosition,
                        flowMode = TransferFlowMode.Recebedor,
                        displayLabel = BuildTransferDisplayLabel(supplier, TransferFlowMode.Recebedor, null, construction)
                    });
                }
                else
                {
                    AppendInvalid(
                        invalidOutput,
                        supplier,
                        null,
                        construction,
                        construction.CurrentCellPosition,
                        TransferFlowMode.Recebedor,
                        "Construcao sem estoque para modo recebedor.");
                }
            }
        }

        if (!hasEmbarkedStock)
        {
            if (constructionsInCollectionRange != null && constructionsInCollectionRange.Count > 0)
            {
                for (int i = 0; i < constructionsInCollectionRange.Count; i++)
                {
                    ConstructionManager construction = constructionsInCollectionRange[i];
                    if (construction == null)
                        continue;
                    AppendInvalid(
                        invalidOutput,
                        supplier,
                        null,
                        construction,
                        origin: construction.CurrentCellPosition,
                        mode: TransferFlowMode.Fornecimento,
                        reason: "Hub sem estoque embarcado para fornecer.");
                }
            }
            else
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    null,
                    alliedConstruction,
                    origin: supplier.CurrentCellPosition,
                    mode: TransferFlowMode.Fornecimento,
                    reason: "Hub sem estoque embarcado para fornecer.");
            }
            return;
        }

        if (constructionsInCollectionRange != null)
        {
            for (int i = 0; i < constructionsInCollectionRange.Count; i++)
            {
                ConstructionManager construction = constructionsInCollectionRange[i];
                if (construction == null)
                    continue;

                hasAnyConstructionForFornecimento = true;
                if (ConstructionHasInfiniteSupply(construction))
                {
                    AppendInvalid(
                        invalidOutput,
                        supplier,
                        null,
                        construction,
                        origin: construction.CurrentCellPosition,
                        mode: TransferFlowMode.Fornecimento,
                        reason: "Construcao com suprimento infinito bloqueia modo fornecimento para hub.");
                    continue;
                }

                output.Add(new PodeTransferirOption
                {
                    supplierUnit = supplier,
                    targetConstruction = construction,
                    targetCell = construction.CurrentCellPosition,
                    flowMode = TransferFlowMode.Fornecimento,
                    displayLabel = BuildTransferDisplayLabel(supplier, TransferFlowMode.Fornecimento, null, construction)
                });
            }
        }

        if (transferableTargets.Count <= 0 && !hasAnyConstructionForFornecimento)
        {
            AppendInvalid(
                invalidOutput,
                supplier,
                null,
                alliedConstruction,
                origin: supplier.CurrentCellPosition,
                mode: TransferFlowMode.Fornecimento,
                reason: "Hub sem alvo elegivel para fornecimento (hub/receiver).");
            return;
        }

        for (int i = 0; i < transferableTargets.Count; i++)
        {
            UnitManager target = transferableTargets[i];
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            output.Add(new PodeTransferirOption
            {
                supplierUnit = supplier,
                targetUnit = target,
                targetCell = targetCell,
                flowMode = TransferFlowMode.Fornecimento,
                displayLabel = BuildTransferDisplayLabel(supplier, TransferFlowMode.Fornecimento, target, null)
            });
        }
    }

    private static void CollectReceiverOptions(
        UnitManager supplier,
        UnitData supplierData,
        ConstructionManager alliedConstruction,
        List<ConstructionManager> constructionsInCollectionRange,
        List<UnitManager> nearbyHubUnits,
        List<PodeTransferirOption> output,
        List<PodeTransferirInvalidOption> invalidOutput)
    {
        bool hasValidHub = false;

        if (constructionsInCollectionRange != null)
        {
            for (int i = 0; i < constructionsInCollectionRange.Count; i++)
            {
                ConstructionManager construction = constructionsInCollectionRange[i];
                if (construction == null)
                    continue;

                if (!TryGetConstructionSupplierTier(construction, out SupplierTier constructionTier))
                    continue;

                if (constructionTier != SupplierTier.Hub)
                {
                    AppendInvalid(
                        invalidOutput,
                        supplier,
                        null,
                        construction,
                        construction.CurrentCellPosition,
                        TransferFlowMode.Recebedor,
                        "Construcao com tier Receiver so recebe (nao fornece).");
                    continue;
                }

                if (GetConstructionTotalSupply(construction) <= 0)
                    continue;

                if (!CanTransferAtLeastOneSupply(null, construction, supplier))
                {
                    AppendInvalid(
                        invalidOutput,
                        supplier,
                        null,
                        construction,
                        construction.CurrentCellPosition,
                        TransferFlowMode.Recebedor,
                        "Receiver sem necessidade/capacidade para receber recursos desta construcao.");
                    continue;
                }

                hasValidHub = true;
                output.Add(new PodeTransferirOption
                {
                    supplierUnit = supplier,
                    targetConstruction = construction,
                    targetCell = construction.CurrentCellPosition,
                    flowMode = TransferFlowMode.Recebedor,
                    displayLabel = BuildTransferDisplayLabel(supplier, TransferFlowMode.Recebedor, null, construction)
                });
            }
        }

        for (int i = 0; i < nearbyHubUnits.Count; i++)
        {
            UnitManager hub = nearbyHubUnits[i];
            if (hub == null || hub == supplier)
                continue;
            if (!TryGetSupplierData(hub, out UnitData hubData) || hubData.supplierTier != SupplierTier.Hub)
                continue;
            if (!HasTransferService(hub))
                continue;
            if (GetUnitTotalStock(hub) <= 0)
                continue;
            if (!IsDomainCompatibleForTransfer(supplier, supplierData, hub))
                continue;
            if (!CanTransferAtLeastOneSupply(hub, null, supplier))
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    hub,
                    null,
                    hub.CurrentCellPosition,
                    TransferFlowMode.Recebedor,
                    "Supplier sem capacidade disponivel para receber recursos do hub.");
                continue;
            }

            hasValidHub = true;
            Vector3Int hubCell = hub.CurrentCellPosition;
            hubCell.z = 0;
            output.Add(new PodeTransferirOption
            {
                supplierUnit = supplier,
                targetUnit = hub,
                targetCell = hubCell,
                flowMode = TransferFlowMode.Recebedor,
                displayLabel = BuildTransferDisplayLabel(supplier, TransferFlowMode.Recebedor, hub, null)
            });
        }

        if (!hasValidHub)
        {
            AppendInvalid(
                invalidOutput,
                supplier,
                null,
                null,
                origin: supplier.CurrentCellPosition,
                mode: TransferFlowMode.Recebedor,
                reason: "Receiver sem hub aliado valido no collection range.");
        }
    }

    private static string ResolveConstructionLabel(ConstructionManager construction)
    {
        if (construction == null)
            return "(null)";
        if (!string.IsNullOrWhiteSpace(construction.ConstructionDisplayName))
            return construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(construction.ConstructionId))
            return construction.ConstructionId;
        return construction.name;
    }

    private static string ResolveUnitLabel(UnitManager unit)
    {
        if (unit == null)
            return "(unidade)";
        return !string.IsNullOrWhiteSpace(unit.name) ? unit.name : "(unidade)";
    }

    private static bool TryGetConstructionSupplierTier(ConstructionManager construction, out SupplierTier tier)
    {
        tier = SupplierTier.Hub;
        if (construction == null || !construction.TryResolveConstructionData(out ConstructionData data) || data == null)
            return false;

        tier = data.supplierTier;
        return true;
    }

    private static string BuildTransferDisplayLabel(
        UnitManager supplier,
        TransferFlowMode mode,
        UnitManager targetUnit,
        ConstructionManager targetConstruction)
    {
        string supplierLabel = ResolveUnitLabel(supplier);
        string endpointLabel = targetUnit != null
            ? ResolveUnitLabel(targetUnit)
            : ResolveConstructionLabel(targetConstruction);

        if (mode == TransferFlowMode.Fornecimento)
            return $"{supplierLabel} -> Fornece -> {endpointLabel}";

        return $"{supplierLabel} <- Recebe <- {endpointLabel}";
    }

    private static List<UnitManager> CollectUnitsInCollectionRange(
        UnitManager supplier,
        UnitData supplierData,
        Tilemap boardMap,
        Vector3Int originCell)
    {
        var result = new List<UnitManager>();
        if (supplier == null || supplierData == null || boardMap == null)
            return result;

        bool adjacentRange = supplierData.collectionRange == SupplierRangeMode.Adjacent1Hex
            || supplierData.collectionRange == SupplierRangeMode.Hybrid0Or1Hex;
        bool includeOriginCell = supplierData.collectionRange == SupplierRangeMode.Hybrid0Or1Hex;
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

        if (includeOriginCell)
        {
            for (int i = 0; i < units.Length; i++)
            {
                UnitManager target = units[i];
                if (target == null || target == supplier)
                    continue;

                Vector3Int cell = target.CurrentCellPosition;
                cell.z = 0;
                if (cell == originCell && !result.Contains(target))
                    result.Add(target);
            }
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
            if (adjacentLookup.Contains(cell))
                result.Add(target);
        }

        return result;
    }

    private static List<ConstructionManager> CollectConstructionsInCollectionRange(
        UnitData supplierData,
        Tilemap boardMap,
        Vector3Int originCell,
        TeamId supplierTeam)
    {
        var result = new List<ConstructionManager>();
        if (supplierData == null || boardMap == null)
            return result;

        bool adjacentRange = supplierData.collectionRange == SupplierRangeMode.Adjacent1Hex
            || supplierData.collectionRange == SupplierRangeMode.Hybrid0Or1Hex;
        bool includeOriginCell = supplierData.collectionRange != SupplierRangeMode.Adjacent1Hex;
        originCell.z = 0;

        if (includeOriginCell)
        {
            ConstructionManager originConstruction = ResolveAlliedConstructionAtCell(boardMap, originCell, supplierTeam);
            if (originConstruction != null)
                result.Add(originConstruction);
        }

        if (!adjacentRange)
            return result;

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(boardMap, originCell, neighbors);
        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int cell = neighbors[i];
            cell.z = 0;
            ConstructionManager construction = ResolveAlliedConstructionAtCell(boardMap, cell, supplierTeam);
            if (construction == null || result.Contains(construction))
                continue;
            result.Add(construction);
        }

        return result;
    }

    private static List<UnitManager> CollectNearbyHubUnits(List<UnitManager> unitsInRange, TeamId teamId)
    {
        var hubs = new List<UnitManager>();
        if (unitsInRange == null)
            return hubs;

        for (int i = 0; i < unitsInRange.Count; i++)
        {
            UnitManager candidate = unitsInRange[i];
            if (candidate == null || (int)candidate.TeamId != (int)teamId)
                continue;
            if (!TryGetSupplierData(candidate, out UnitData data))
                continue;
            if (data.supplierTier != SupplierTier.Hub)
                continue;
            hubs.Add(candidate);
        }

        return hubs;
    }

    private static bool TryGetSupplierData(UnitManager unit, out UnitData data)
    {
        data = null;
        return unit != null &&
               unit.TryGetUnitData(out data) &&
               data != null &&
               data.isSupplier;
    }

    private static bool HasTransferService(UnitManager unit)
    {
        if (unit == null)
            return false;

        IReadOnlyList<ServiceData> services = unit.GetEmbarkedServices();
        if (services == null)
            return false;

        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service != null && service.serviceType == ServiceType.Transfer)
                return true;
        }

        return false;
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

    private static bool IsDomainCompatibleForTransfer(UnitManager supplier, UnitData supplierData, UnitManager target)
    {
        if (supplier == null || supplierData == null || target == null)
            return false;

        Domain supplierDomain = supplier.GetDomain();
        HeightLevel supplierHeight = supplier.GetHeightLevel();
        Domain targetDomain = target.GetDomain();
        HeightLevel targetHeight = target.GetHeightLevel();

        if (supplierDomain == targetDomain && supplierHeight == targetHeight)
            return true;

        return SupportsOperationDomain(supplierData, targetDomain, targetHeight);
    }

    private static int GetUnitTotalStock(UnitManager unit)
    {
        if (unit == null)
            return 0;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null)
            return 0;

        int total = 0;
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;
            total += Mathf.Max(0, entry.amount);
        }

        return total;
    }

    private static bool CanTransferAtLeastOneSupply(
        UnitManager sourceUnit,
        ConstructionManager sourceConstruction,
        UnitManager destinationUnit)
    {
        if (destinationUnit == null)
            return false;

        Dictionary<SupplyData, long> sourceStock = sourceUnit != null
            ? ReadUnitStockMap(sourceUnit)
            : ReadConstructionStockMap(sourceConstruction);
        if (sourceStock == null || sourceStock.Count <= 0)
            return false;

        Dictionary<SupplyData, long> destinationStock = ReadUnitStockMap(destinationUnit);
        Dictionary<SupplyData, long> destinationCapacity = ReadUnitCapacityMap(destinationUnit);
        if (destinationCapacity == null || destinationCapacity.Count <= 0)
            return false;

        foreach (KeyValuePair<SupplyData, long> pair in sourceStock)
        {
            SupplyData supply = pair.Key;
            if (supply == null || pair.Value <= 0)
                continue;
            if (!destinationCapacity.TryGetValue(supply, out long capacity))
                continue;

            long current = destinationStock != null && destinationStock.TryGetValue(supply, out long existing)
                ? existing
                : 0;
            long remaining = System.Math.Max(0L, capacity - current);
            if (remaining > 0)
                return true;
        }

        return false;
    }

    private static Dictionary<SupplyData, long> ReadUnitStockMap(UnitManager unit)
    {
        Dictionary<SupplyData, long> map = new Dictionary<SupplyData, long>();
        if (unit == null)
            return map;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null)
            return map;

        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;

            long amount = System.Math.Max(0L, entry.amount);
            if (map.TryGetValue(entry.supply, out long existing))
                map[entry.supply] = existing + amount;
            else
                map[entry.supply] = amount;
        }

        return map;
    }

    private static Dictionary<SupplyData, long> ReadUnitCapacityMap(UnitManager unit)
    {
        Dictionary<SupplyData, long> map = new Dictionary<SupplyData, long>();
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.supplierResources == null)
            return map;

        for (int i = 0; i < data.supplierResources.Count; i++)
        {
            UnitEmbarkedSupply entry = data.supplierResources[i];
            if (entry == null || entry.supply == null)
                continue;

            long capacity = System.Math.Max(0L, entry.amount);
            if (map.TryGetValue(entry.supply, out long existing))
                map[entry.supply] = existing + capacity;
            else
                map[entry.supply] = capacity;
        }

        return map;
    }

    private static Dictionary<SupplyData, long> ReadConstructionStockMap(ConstructionManager construction)
    {
        Dictionary<SupplyData, long> map = new Dictionary<SupplyData, long>();
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

            long amount = construction.HasInfiniteSuppliesFor(offer.supply)
                ? InfiniteConstructionSupplyQuantity
                : System.Math.Max(0L, offer.quantity);
            if (map.TryGetValue(offer.supply, out long existing))
                map[offer.supply] = existing >= InfiniteConstructionSupplyQuantity || amount >= InfiniteConstructionSupplyQuantity
                    ? InfiniteConstructionSupplyQuantity
                    : existing + amount;
            else
                map[offer.supply] = amount;
        }

        return map;
    }

    private static ConstructionManager ResolveAlliedConstructionAtCell(Tilemap boardMap, Vector3Int cell, TeamId teamId)
    {
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction == null)
            return null;
        if ((int)construction.TeamId != (int)teamId)
            return null;
        return construction;
    }

    private static int GetConstructionTotalSupply(ConstructionManager construction)
    {
        if (construction == null || !construction.CanProvideSupplies)
            return 0;
        if (construction.HasInfiniteSuppliesFor())
            return int.MaxValue;

        IReadOnlyList<ConstructionSupplyOffer> offers = construction.OfferedSupplies;
        if (offers == null)
            return 0;

        long total = 0;
        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply == null)
                continue;
            total += Mathf.Max(0, offer.quantity);
            if (total >= int.MaxValue)
                return int.MaxValue;
        }

        return total <= 0 ? 0 : (total >= int.MaxValue ? int.MaxValue : (int)total);
    }

    private static bool ConstructionHasInfiniteSupply(ConstructionManager construction)
    {
        if (construction == null)
            return false;
        return construction.HasInfiniteSuppliesFor();
    }

    private static void AppendInvalid(
        List<PodeTransferirInvalidOption> invalidOutput,
        UnitManager supplier,
        UnitManager target,
        ConstructionManager targetConstruction,
        Vector3Int origin,
        TransferFlowMode mode,
        string reason)
    {
        if (invalidOutput == null)
            return;

        origin.z = 0;
        invalidOutput.Add(new PodeTransferirInvalidOption
        {
            supplierUnit = supplier,
            targetUnit = target,
            targetConstruction = targetConstruction,
            targetCell = origin,
            flowMode = mode,
            reason = reason
        });
    }

    private static void SortTransferOptions(List<PodeTransferirOption> options)
    {
        if (options == null || options.Count <= 1)
            return;

        options.Sort((a, b) =>
        {
            if (ReferenceEquals(a, b))
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            int modeCmp = a.flowMode.CompareTo(b.flowMode);
            if (modeCmp != 0)
                return modeCmp;

            int yCmp = a.targetCell.y.CompareTo(b.targetCell.y);
            if (yCmp != 0)
                return yCmp;

            int xCmp = a.targetCell.x.CompareTo(b.targetCell.x);
            if (xCmp != 0)
                return xCmp;

            string aLabel = string.IsNullOrWhiteSpace(a.displayLabel) ? string.Empty : a.displayLabel;
            string bLabel = string.IsNullOrWhiteSpace(b.displayLabel) ? string.Empty : b.displayLabel;
            return string.Compare(aLabel, bLabel, System.StringComparison.OrdinalIgnoreCase);
        });
    }

}
