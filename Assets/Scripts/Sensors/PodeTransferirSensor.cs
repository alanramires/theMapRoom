using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeTransferirSensor
{
    private const int InfiniteConstructionSupplyQuantity = int.MaxValue;

    public static bool CollectOptions(
        UnitManager supplier,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        List<PodeTransferirOption> output,
        out string reason,
        List<PodeTransferirInvalidOption> invalidOutput = null)
    {
        Vector3Int origin = supplier != null
            ? supplier.CurrentCellPosition
            : Vector3Int.zero;
        return CollectOptionsFromCell(
            supplier,
            map,
            terrainDatabase,
            movementMode,
            origin,
            output,
            out reason,
            invalidOutput);
    }

    /// <summary>
    /// Consulta prospectiva pura: responde quais transferencias seriam validas
    /// se o supplier estivesse em <paramref name="originCell"/>. Nao move a
    /// unidade e nao altera ocupacao, recursos, FOW ou estado transacional.
    /// A chamada tradicional acima continua sendo a autoridade para o hex
    /// corrente no momento de confirmar a acao.
    /// </summary>
    public static bool CollectOptionsFromCell(
        UnitManager supplier,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        Vector3Int originCell,
        List<PodeTransferirOption> output,
        out string reason,
        List<PodeTransferirInvalidOption> invalidOutput = null)
    {
        reason = string.Empty;
        bool sensorLogs = SensorLogGate.IsPodeTransferirEnabled();
        if (output == null)
            return false;

        output.Clear();
        invalidOutput?.Clear();

        if (sensorLogs)
            SensorLogGate.Log("PodeTransferirSensor", $"collect supplier={(supplier != null ? supplier.name : "(null)")}");

        if (supplier == null)
        {
            reason = "Selecione um supridor logistico.";
            return false;
        }

        if (supplier.TeamId == TeamId.Neutral)
        {
            reason = "Exercito neutro nao pode transferir recursos.";
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

        if (!HasTransferService(supplier))
        {
            reason = "Unidade sem servico de transferencia.";
            return false;
        }

        originCell.z = 0;
        bool sameHexOrEmbarked = supplierData.collectionRange == SupplierRangeMode.SameHexOrEmbarked;
        bool requiresLanding = sameHexOrEmbarked && supplier.GetDomain() == Domain.Air && !supplier.IsAircraftGrounded;
        Domain operationDomain = supplier.GetDomain();
        HeightLevel operationHeight = supplier.GetHeightLevel();
        if (requiresLanding)
        {
            AircraftOperationDecision landingDecision = AircraftOperationRules.Evaluate(
                supplier,
                map,
                terrainDatabase,
                movementMode,
                allowSameTeamAirBlockerForMovementTakeoff: false,
                atCell: originCell);
            if (!landingDecision.available || landingDecision.action != AircraftOperationAction.Land)
            {
                reason = string.IsNullOrWhiteSpace(landingDecision.reason)
                    ? "Aeronave precisa de pouso valido para transferir."
                    : landingDecision.reason;
                return false;
            }

            AircraftOperationRules.ResolveGroundedLayerForCell(
                supplier, map, terrainDatabase, originCell,
                out operationDomain, out operationHeight);
        }

        if ((!sameHexOrEmbarked || requiresLanding) && !SupportsOperationDomain(supplierData, operationDomain, operationHeight))
        {
            reason =
                $"Supplier fora do Supplier Operation Domain de transferencia ({operationDomain}/{operationHeight}). " +
                "Reposicione para um dominio/altura permitido para transferir.";
            return false;
        }

        Tilemap boardMap = map != null ? map : supplier.BoardTilemap;
        if (boardMap == null)
        {
            reason = "Tilemap indisponivel para avaliar transferencia.";
            return false;
        }

        Vector3Int origin = originCell;

        ConstructionManager alliedConstruction = ResolveAlliedConstructionAtCell(
            boardMap,
            origin,
            supplier.TeamId,
            operationDomain,
            operationHeight);
        List<ConstructionManager> constructionsInCollectionRange = CollectConstructionsInCollectionRange(
            supplierData,
            boardMap,
            origin,
            supplier.TeamId,
            operationDomain,
            operationHeight);
        List<UnitManager> unitsInExchangeNeighborhood =
            CollectUnitsInExchangeNeighborhood(
                supplier, boardMap, origin);
        bool hasTransferPeerCandidate = HasTransferPeerCandidate(
            supplier,
            unitsInExchangeNeighborhood);

        bool isOnAlliedConstruction = alliedConstruction != null;
        if (!isOnAlliedConstruction
            && constructionsInCollectionRange.Count <= 0
            && !hasTransferPeerCandidate)
        {
            reason =
                "Transferencia exige construcao ou unidade logistica aliada " +
                "com servico Transfer no collection range.";
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
                    unitsInExchangeNeighborhood,
                    origin,
                    operationDomain,
                    operationHeight,
                    boardMap,
                    terrainDatabase,
                    movementMode,
                    output,
                    invalidOutput);
                break;
            case SupplierTier.Receiver:
                CollectReceiverOptions(
                    supplier,
                    supplierData,
                    alliedConstruction,
                    constructionsInCollectionRange,
                    unitsInExchangeNeighborhood,
                    origin,
                    operationDomain,
                    operationHeight,
                    boardMap,
                    terrainDatabase,
                    movementMode,
                    output,
                    invalidOutput);
                break;
            default:
                reason = "Tier de supplier nao suportado para transferencia.";
                return false;
        }

        SortTransferOptions(output);

        if (requiresLanding)
        {
            for (int i = 0; i < output.Count; i++)
            {
                PodeTransferirOption option = output[i];
                if (option == null)
                    continue;
                option.requiresSupplierLanding = true;
                option.landingDomain = operationDomain;
                option.landingHeight = operationHeight;
                option.landingMovementMode = movementMode;
                option.displayLabel = $"Pousar + {option.displayLabel}";
            }
        }

        if (output.Count <= 0)
        {
            reason = "Sem opcoes validas de transferencia neste contexto.";
            if (sensorLogs)
                SensorLogGate.Log("PodeTransferirSensor", $"result valid={output.Count} invalid={(invalidOutput != null ? invalidOutput.Count : 0)} hasAny=false");
            return false;
        }

        if (sensorLogs)
            SensorLogGate.Log("PodeTransferirSensor", $"result valid={output.Count} invalid={(invalidOutput != null ? invalidOutput.Count : 0)} hasAny=true");
        return true;
    }

    private static void CollectHubOptions(
        UnitManager supplier,
        UnitData supplierData,
        ConstructionManager alliedConstruction,
        List<ConstructionManager> constructionsInCollectionRange,
        List<UnitManager> unitsInCollectionRange,
        Vector3Int originCell,
        Domain operationDomain,
        HeightLevel operationHeight,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        List<PodeTransferirOption> output,
        List<PodeTransferirInvalidOption> invalidOutput)
    {
        bool hasEmbarkedStock = GetUnitTotalStock(supplier) > 0;
        bool foundDonationTarget = false;

        // CONSTRUCOES: Hub recebe de Hub e doa para Hub/Receiver no range.
        if (constructionsInCollectionRange != null)
        {
            for (int i = 0; i < constructionsInCollectionRange.Count; i++)
            {
                ConstructionManager construction = constructionsInCollectionRange[i];
                if (construction == null || !TryGetConstructionSupplierTier(construction, out SupplierTier constructionTier))
                    continue;

                bool constructionIsHub = constructionTier == SupplierTier.Hub;
                bool constructionIsReceiver = constructionTier == SupplierTier.Receiver;
                if (!constructionIsHub && !constructionIsReceiver)
                    continue;

                if (constructionIsHub)
                {
                    if (GetConstructionTotalSupply(construction) > 0)
                    {
                        if (CanTransferAtLeastOneSupply(null, construction, supplier))
                        {
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
                                "Supplier sem capacidade disponivel para receber recursos desta construcao.");
                        }
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
                            "Construcao hub sem estoque para modo recebedor.");
                    }
                }

                if (!hasEmbarkedStock)
                    continue;

                if (constructionIsHub && ConstructionHasInfiniteSupply(construction))
                {
                    AppendInvalid(
                        invalidOutput,
                        supplier,
                        null,
                        construction,
                        construction.CurrentCellPosition,
                        TransferFlowMode.Fornecimento,
                        "Construcao hub com suprimento infinito bloqueia modo doar.");
                    continue;
                }

                foundDonationTarget = true;
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

        bool foundUnitReceiveSource = false;
        CollectUnitExchangeOptions(
            supplier,
            supplierData,
            unitsInCollectionRange,
            originCell,
            operationDomain,
            operationHeight,
            boardMap,
            terrainDatabase,
            movementMode,
            output,
            invalidOutput,
            ref foundUnitReceiveSource,
            ref foundDonationTarget);

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
                        construction.CurrentCellPosition,
                        TransferFlowMode.Fornecimento,
                        "Hub sem estoque embarcado para doar.");
                }
            }
            else
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    null,
                    alliedConstruction,
                    originCell,
                    TransferFlowMode.Fornecimento,
                    "Hub sem estoque embarcado para doar.");
            }
            return;
        }

        if (!foundDonationTarget)
        {
            AppendInvalid(
                invalidOutput,
                supplier,
                null,
                alliedConstruction,
                originCell,
                TransferFlowMode.Fornecimento,
                "Hub sem alvo elegivel para doar (hub/receiver).");
        }
    }

    private static void CollectReceiverOptions(
        UnitManager supplier,
        UnitData supplierData,
        ConstructionManager alliedConstruction,
        List<ConstructionManager> constructionsInCollectionRange,
        List<UnitManager> unitsInCollectionRange,
        Vector3Int originCell,
        Domain operationDomain,
        HeightLevel operationHeight,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        List<PodeTransferirOption> output,
        List<PodeTransferirInvalidOption> invalidOutput)
    {
        bool hasValidHubSource = false;

        if (constructionsInCollectionRange != null)
        {
            for (int i = 0; i < constructionsInCollectionRange.Count; i++)
            {
                ConstructionManager construction = constructionsInCollectionRange[i];
                if (construction == null)
                    continue;

                if (!TryGetConstructionSupplierTier(construction, out SupplierTier constructionTier))
                    continue;

                bool constructionIsHub = constructionTier == SupplierTier.Hub;
                bool constructionIsReceiver = constructionTier == SupplierTier.Receiver;
                if (!constructionIsHub && !constructionIsReceiver)
                    continue;

                if (constructionIsHub && GetConstructionTotalSupply(construction) > 0)
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
                            "Receiver sem necessidade/capacidade para receber recursos desta construcao.");
                    }
                    else
                    {
                        hasValidHubSource = true;
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
            }
        }

        bool foundDonationTarget = false;
        CollectUnitExchangeOptions(
            supplier,
            supplierData,
            unitsInCollectionRange,
            originCell,
            operationDomain,
            operationHeight,
            boardMap,
            terrainDatabase,
            movementMode,
            output,
            invalidOutput,
            ref hasValidHubSource,
            ref foundDonationTarget);

        if (!hasValidHubSource)
        {
            AppendInvalid(
                invalidOutput,
                supplier,
                null,
                null,
                origin: originCell,
                mode: TransferFlowMode.Recebedor,
                reason:
                    "Receiver sem construcao ou unidade logistica aliada " +
                    "com estoque compativel no collection range.");
        }
    }

    /// <summary>
    /// Troca fisica entre unidades conforme a ficha:
    /// Hub-Hub e bidirecional; Hub-Receiver flui apenas do Hub ao Receiver.
    /// O alcance de cada sentido pertence a unidade que cede o estoque.
    /// </summary>
    private static void CollectUnitExchangeOptions(
        UnitManager supplier,
        UnitData supplierData,
        List<UnitManager> unitsInCollectionRange,
        Vector3Int supplierCell,
        Domain supplierOperationDomain,
        HeightLevel supplierOperationHeight,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        List<PodeTransferirOption> output,
        List<PodeTransferirInvalidOption> invalidOutput,
        ref bool foundReceiveSource,
        ref bool foundDonationTarget)
    {
        if (supplier == null
            || supplierData == null
            || unitsInCollectionRange == null)
            return;

        bool supplierHasStock = GetUnitTotalStock(supplier) > 0;
        for (int i = 0; i < unitsInCollectionRange.Count; i++)
        {
            UnitManager peer = unitsInCollectionRange[i];
            if (!TryGetTransferPeerData(
                    supplier, peer, out UnitData peerData))
                continue;

            Vector3Int peerCell = peer.CurrentCellPosition;
            peerCell.z = 0;
            bool tierAllowsReceive =
                peerData.supplierTier == SupplierTier.Hub;
            bool tierAllowsDonation =
                supplierData.supplierTier == SupplierTier.Hub;
            string receiveRangeReason = string.Empty;
            string donationRangeReason = string.Empty;
            UnitManager receiveLandingUnit = null;
            Domain receiveLandingDomain = Domain.Land;
            HeightLevel receiveLandingHeight = HeightLevel.Surface;
            UnitManager donationLandingUnit = null;
            Domain donationLandingDomain = Domain.Land;
            HeightLevel donationLandingHeight = HeightLevel.Surface;
            bool canReceiveFromPeer =
                tierAllowsReceive
                && CanUseUnitTransferDirection(
                    peer,
                    peerData,
                    peerCell,
                    peer.GetDomain(),
                    peer.GetHeightLevel(),
                    supplier,
                    supplierCell,
                    supplierOperationDomain,
                    supplierOperationHeight,
                    boardMap,
                    terrainDatabase,
                    movementMode,
                    out receiveLandingUnit,
                    out receiveLandingDomain,
                    out receiveLandingHeight,
                    out receiveRangeReason);
            bool canDonateToPeer =
                tierAllowsDonation
                && CanUseUnitTransferDirection(
                    supplier,
                    supplierData,
                    supplierCell,
                    supplierOperationDomain,
                    supplierOperationHeight,
                    peer,
                    peerCell,
                    peer.GetDomain(),
                    peer.GetHeightLevel(),
                    boardMap,
                    terrainDatabase,
                    movementMode,
                    out donationLandingUnit,
                    out donationLandingDomain,
                    out donationLandingHeight,
                    out donationRangeReason);

            if (tierAllowsReceive && !canReceiveFromPeer)
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    peer,
                    null,
                    peerCell,
                    TransferFlowMode.Recebedor,
                    receiveRangeReason);
            }
            else if (tierAllowsReceive
                     && GetUnitTotalStock(peer) <= 0)
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    peer,
                    null,
                    peerCell,
                    TransferFlowMode.Recebedor,
                    "Unidade logistica aliada sem estoque para ceder.");
            }
            else if (tierAllowsReceive
                     && CanTransferAtLeastOneSupply(
                         peer, null, supplier))
            {
                foundReceiveSource = true;
                PodeTransferirOption option =
                    new PodeTransferirOption
                {
                    supplierUnit = supplier,
                    targetUnit = peer,
                    targetCell = peerCell,
                    flowMode = TransferFlowMode.Recebedor,
                    displayLabel = BuildTransferDisplayLabel(
                        supplier,
                        TransferFlowMode.Recebedor,
                        peer,
                        null)
                };
                ApplyTransferLandingPlan(
                    option,
                    supplier,
                    peer,
                    receiveLandingUnit,
                    receiveLandingDomain,
                    receiveLandingHeight,
                    movementMode);
                output.Add(option);
            }
            else if (tierAllowsReceive)
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    peer,
                    null,
                    peerCell,
                    TransferFlowMode.Recebedor,
                    "Sem carga compativel ou capacidade livre para receber desta unidade logistica.");
            }

            if (tierAllowsDonation && !canDonateToPeer)
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    peer,
                    null,
                    peerCell,
                    TransferFlowMode.Fornecimento,
                    donationRangeReason);
            }
            else if (tierAllowsDonation
                     && !supplierHasStock)
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    peer,
                    null,
                    peerCell,
                    TransferFlowMode.Fornecimento,
                    "Unidade selecionada sem estoque para ceder.");
            }
            else if (tierAllowsDonation
                     && CanTransferAtLeastOneSupply(
                         supplier, null, peer))
            {
                foundDonationTarget = true;
                PodeTransferirOption option =
                    new PodeTransferirOption
                {
                    supplierUnit = supplier,
                    targetUnit = peer,
                    targetCell = peerCell,
                    flowMode = TransferFlowMode.Fornecimento,
                    displayLabel = BuildTransferDisplayLabel(
                        supplier,
                        TransferFlowMode.Fornecimento,
                        peer,
                        null)
                };
                ApplyTransferLandingPlan(
                    option,
                    supplier,
                    peer,
                    donationLandingUnit,
                    donationLandingDomain,
                    donationLandingHeight,
                    movementMode);
                output.Add(option);
            }
            else if (tierAllowsDonation)
            {
                AppendInvalid(
                    invalidOutput,
                    supplier,
                    peer,
                    null,
                    peerCell,
                    TransferFlowMode.Fornecimento,
                    "Unidade logistica aliada sem capacidade para receber carga compativel.");
            }
        }
    }

    private static void ApplyTransferLandingPlan(
        PodeTransferirOption option,
        UnitManager supplier,
        UnitManager peer,
        UnitManager landingUnit,
        Domain landingDomain,
        HeightLevel landingHeight,
        SensorMovementMode movementMode)
    {
        if (option == null || landingUnit == null)
            return;

        if (landingUnit == supplier)
        {
            option.requiresSupplierLanding = true;
            option.landingDomain = landingDomain;
            option.landingHeight = landingHeight;
            option.landingMovementMode = movementMode;
            option.displayLabel =
                $"Pousar {ResolveUnitLabel(supplier)} + " +
                option.displayLabel;
            return;
        }

        if (landingUnit != peer)
            return;

        option.requiresTargetUnitLanding = true;
        option.targetLandingDomain = landingDomain;
        option.targetLandingHeight = landingHeight;
        option.targetLandingMovementMode = movementMode;
        option.displayLabel =
            $"Pousar {ResolveUnitLabel(peer)} + " +
            option.displayLabel;
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
        string endpointLabel = targetUnit != null
            ? ResolveUnitLabel(targetUnit)
            : ResolveConstructionLabel(targetConstruction);

        if (targetConstruction != null)
        {
            string transferRole = ResolveConstructionTransferRoleLabel(targetConstruction);
            if (!string.IsNullOrWhiteSpace(transferRole))
                return $"{transferRole} :: {endpointLabel}";
        }

        if (mode == TransferFlowMode.Fornecimento)
            return $"Transferir: Doar -> {endpointLabel}";

        return $"Transferir: Receber <- {endpointLabel}";
    }

    private static string ResolveConstructionTransferRoleLabel(ConstructionManager construction)
    {
        if (construction == null)
            return string.Empty;
        if (!construction.TryResolveConstructionData(out ConstructionData data) || data == null || !data.isSupplier)
            return string.Empty;

        if (data.supplierTier == SupplierTier.Receiver)
            return "Transferir - Recebedor";
        if (data.supplierTier != SupplierTier.Hub)
            return string.Empty;
        if (construction.HasInfiniteSuppliesFor())
            return "Transferir - Fornecedor";
        return "Transferir - Recebedor/Fornecedor";
    }

    private static List<UnitManager> CollectUnitsInExchangeNeighborhood(
        UnitManager supplier,
        Tilemap boardMap,
        Vector3Int originCell)
    {
        var result = new List<UnitManager>();
        if (supplier == null || boardMap == null)
            return result;

        // Envelope de descoberta, nao permissao. Como todos os modos atuais
        // alcancam no maximo um hex, coletamos proprio hex + adjacentes e cada
        // sentido e filtrado depois pelo collectionRange da unidade que CEDE.
        IReadOnlyList<UnitManager> units =
            ResolveUnitsForTransferQuery();
        if (units == null || units.Count <= 0)
            return result;

        originCell.z = 0;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager target = units[i];
            if (target == null || target == supplier)
                continue;

            Vector3Int cell = target.CurrentCellPosition;
            cell.z = 0;
            int distance = Mathf.RoundToInt(
                SectorManager.HexDistance(originCell, cell));
            if (distance <= 1)
                result.Add(target);
        }

        return result;
    }

    private static IReadOnlyList<UnitManager>
        ResolveUnitsForTransferQuery()
    {
        if (!Application.isPlaying)
        {
            return Object.FindObjectsByType<UnitManager>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }

        List<UnitManager> runtimeUnits =
            UnitManager.AllActive;
        if (runtimeUnits != null
            && runtimeUnits.Count > 0)
            return runtimeUnits;

        // Fallback defensivo para cenas de teste que ainda nao registraram
        // suas unidades ao iniciar o Play Mode.
        return Object.FindObjectsByType<UnitManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
    }

    private static List<ConstructionManager> CollectConstructionsInCollectionRange(
        UnitData supplierData,
        Tilemap boardMap,
        Vector3Int originCell,
        TeamId supplierTeam,
        Domain supplierDomain,
        HeightLevel supplierHeight)
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
            ConstructionManager originConstruction = ResolveAlliedConstructionAtCell(
                boardMap,
                originCell,
                supplierTeam,
                supplierDomain,
                supplierHeight);
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
            ConstructionManager construction = ResolveAlliedConstructionAtCell(
                boardMap,
                cell,
                supplierTeam,
                supplierDomain,
                supplierHeight);
            if (construction == null || result.Contains(construction))
                continue;
            result.Add(construction);
        }

        return result;
    }

    private static bool HasTransferPeerCandidate(
        UnitManager supplier,
        List<UnitManager> unitsInRange)
    {
        if (unitsInRange == null)
            return false;

        for (int i = 0; i < unitsInRange.Count; i++)
        {
            if (TryGetTransferPeerData(
                    supplier,
                    unitsInRange[i],
                    out _))
                return true;
        }

        return false;
    }

    private static bool TryGetTransferPeerData(
        UnitManager supplier,
        UnitManager peer,
        out UnitData peerData)
    {
        peerData = null;
        if (supplier == null
            || peer == null
            || peer == supplier
            || peer.IsDead
            || !AreAlliedForTransferQuery(peer, supplier)
            || !TryGetSupplierData(peer, out peerData)
            || peerData == null
            || !HasTransferService(peer))
            return false;

        bool tierAllowsExchange =
            peerData.supplierTier == SupplierTier.Hub
            || (supplier.TryGetUnitData(out UnitData supplierData)
                && supplierData != null
                && supplierData.supplierTier == SupplierTier.Hub);
        if (!tierAllowsExchange)
            return false;

        // Passageiro do proprio supplier participa da distribuicao interna.
        // Passageiro escondido em outro transportador nao fica exposto para
        // uma troca externa apenas por compartilhar a coordenada.
        if (peer.IsEmbarked
            && peer.EmbarkedTransporter != supplier)
            return false;

        return true;
    }

    private static bool AreAlliedForTransferQuery(
        UnitManager first,
        UnitManager second)
    {
        if (PlayerSlotRelations.AreAllies(first, second))
            return true;

        // No Scene/Editor os slots podem ainda estar em -1. A ficha de time
        // continua disponivel e permite a consulta pura da ferramenta.
        return !Application.isPlaying
            && first != null
            && second != null
            && first.SlotIndex < 0
            && second.SlotIndex < 0
            && first.TeamId != TeamId.Neutral
            && first.TeamId == second.TeamId;
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
        if (services != null)
        {
            for (int i = 0; i < services.Count; i++)
            {
                ServiceData service = services[i];
                if (service != null
                    && service.serviceType == ServiceType.Transfer)
                    return true;
            }
        }

        // Em Scene/Editor a copia runtime pode ainda nao ter sido sincronizada.
        // A permissao vem da ficha; estoque e capacidade continuam runtime.
        if (!unit.TryGetUnitData(out UnitData data)
            || data == null
            || data.supplierServicesProvided == null)
            return false;

        for (int i = 0;
             i < data.supplierServicesProvided.Count;
             i++)
        {
            ServiceData service =
                data.supplierServicesProvided[i];
            if (service != null
                && service.serviceType == ServiceType.Transfer)
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

    private static bool CanUseUnitTransferDirection(
        UnitManager source,
        UnitData sourceData,
        Vector3Int sourceCell,
        Domain sourceDomain,
        HeightLevel sourceHeight,
        UnitManager destination,
        Vector3Int destinationCell,
        Domain destinationDomain,
        HeightLevel destinationHeight,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        out UnitManager landingUnit,
        out Domain landingDomain,
        out HeightLevel landingHeight,
        out string reason)
    {
        landingUnit = null;
        landingDomain = Domain.Land;
        landingHeight = HeightLevel.Surface;
        reason = string.Empty;
        if (source == null
            || sourceData == null
            || destination == null)
        {
            reason = "Origem ou destino logistico invalido.";
            return false;
        }

        sourceCell.z = 0;
        destinationCell.z = 0;
        bool internalTransfer =
            (destination.IsEmbarked
             && destination.EmbarkedTransporter == source)
            || (source.IsEmbarked
                && source.EmbarkedTransporter == destination);
        int distance = Mathf.RoundToInt(
            SectorManager.HexDistance(sourceCell, destinationCell));
        bool withinRange;
        switch (sourceData.collectionRange)
        {
            case SupplierRangeMode.Adjacent1Hex:
                withinRange = distance == 1;
                break;
            case SupplierRangeMode.Hybrid0Or1Hex:
                withinRange = distance <= 1;
                break;
            default:
                withinRange = internalTransfer || distance == 0;
                break;
        }
        if (!withinRange)
        {
            reason =
                $"Fora do collection range de quem cede " +
                $"({sourceData.collectionRange}, distancia={distance}).";
            return false;
        }

        if (internalTransfer)
            return true;

        if (sourceDomain == destinationDomain
            && sourceHeight == destinationHeight
            && SupportsOperationDomain(
                sourceData,
                sourceDomain,
                sourceHeight))
            return true;

        if (TryPlanTransferLanding(
                destination,
                destinationCell,
                boardMap,
                terrainDatabase,
                movementMode,
                out Domain destinationLandingDomain,
                out HeightLevel destinationLandingHeight,
                out string destinationLandingReason)
            && sourceDomain == destinationLandingDomain
            && sourceHeight == destinationLandingHeight
            && SupportsOperationDomain(
                sourceData,
                destinationLandingDomain,
                destinationLandingHeight))
        {
            landingUnit = destination;
            landingDomain = destinationLandingDomain;
            landingHeight = destinationLandingHeight;
            return true;
        }

        if (TryPlanTransferLanding(
                source,
                sourceCell,
                boardMap,
                terrainDatabase,
                movementMode,
                out Domain sourceLandingDomain,
                out HeightLevel sourceLandingHeight,
                out string sourceLandingReason)
            && sourceLandingDomain == destinationDomain
            && sourceLandingHeight == destinationHeight
            && SupportsOperationDomain(
                sourceData,
                sourceLandingDomain,
                sourceLandingHeight))
        {
            landingUnit = source;
            landingDomain = sourceLandingDomain;
            landingHeight = sourceLandingHeight;
            return true;
        }

        reason =
            $"Camadas incompativeis para transferencia: quem cede esta em " +
            $"{sourceDomain}/{sourceHeight} e quem recebe em " +
            $"{destinationDomain}/{destinationHeight}. " +
            $"Pouso de quem recebe: {destinationLandingReason} " +
            $"Pouso de quem cede: {sourceLandingReason}";
        return false;
    }

    private static bool TryPlanTransferLanding(
        UnitManager aircraft,
        Vector3Int cell,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        out Domain landingDomain,
        out HeightLevel landingHeight,
        out string reason)
    {
        landingDomain = Domain.Land;
        landingHeight = HeightLevel.Surface;
        reason = "unidade nao esta em voo";
        if (aircraft == null
            || aircraft.GetDomain() != Domain.Air
            || aircraft.IsAircraftGrounded)
            return false;

        if (boardMap == null || terrainDatabase == null)
        {
            reason = "tilemap ou Terrain Database indisponivel";
            return false;
        }

        cell.z = 0;
        PodePousarReport landing = PodePousarSensor.Evaluate(
            aircraft,
            boardMap,
            terrainDatabase,
            movementMode,
            useManualRemainingMovement: false,
            manualRemainingMovement: 0,
            atCell: cell);
        if (landing == null || !landing.status)
        {
            reason = landing != null
                ? landing.explicacao
                : "PodePousar sem resultado";
            return false;
        }

        landingDomain = landing.landingDomain;
        landingHeight = landing.landingHeight;
        reason =
            $"pouso autorizado em {landingDomain}/{landingHeight}";
        return true;
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

        foreach (KeyValuePair<SupplyData, long> pair in sourceStock)
        {
            SupplyData supply = pair.Key;
            if (supply == null || pair.Value <= 0)
                continue;
            long remaining = GetUnitRemainingCapacityForSupply(destinationUnit, supply);
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

    private static long GetUnitRemainingCapacityForSupply(UnitManager unit, SupplyData supply)
    {
        if (unit == null || supply == null)
            return 0L;

        Dictionary<SupplyData, long> stockBySupply = ReadUnitStockMap(unit);
        Dictionary<SupplyData, long> capacityBySupply = ReadUnitCapacityMap(unit);
        if (capacityBySupply == null || !capacityBySupply.TryGetValue(supply, out long capacity) || capacity <= 0L)
            return 0L;

        long current = stockBySupply != null && stockBySupply.TryGetValue(supply, out long existing)
            ? existing
            : 0L;
        return System.Math.Max(0L, capacity - current);
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

    private static ConstructionManager ResolveAlliedConstructionAtCell(
        Tilemap boardMap,
        Vector3Int cell,
        TeamId teamId,
        Domain supplierDomain,
        HeightLevel supplierHeight)
    {
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction == null)
            return null;
        if ((int)construction.TeamId != (int)teamId)
            return null;
        if (!construction.SupportsLayerMode(supplierDomain, supplierHeight))
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
