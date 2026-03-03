using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeDesembarcarSensor
{
    public static PodeDesembarcarReport CollectReport(
        UnitManager selectedTransporter,
        Tilemap map,
        TerrainDatabase terrainDatabase)
    {
        var report = new PodeDesembarcarReport();
        var valid = new List<PodeDesembarcarOption>();
        var invalid = new List<PodeDesembarcarInvalidOption>();

        CollectOptionsInternal(
            selectedTransporter,
            map,
            terrainDatabase,
            valid,
            invalid,
            out PodeDesembarcarLandingStatus landingStatus);

        report.localDePouso = landingStatus ?? new PodeDesembarcarLandingStatus();
        report.locaisValidosDeDesembarque = valid;
        report.locaisInvalidosDeDesembarque = invalid;
        return report;
    }

    public static bool CollectOptions(
        UnitManager selectedTransporter,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        List<PodeDesembarcarOption> output,
        List<PodeDesembarcarInvalidOption> invalidOutput = null)
    {
        return CollectOptionsInternal(
            selectedTransporter,
            map,
            terrainDatabase,
            output,
            invalidOutput,
            out _);
    }

    public static bool CollectOptions(
        UnitManager selectedTransporter,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        List<PodeDesembarcarOption> output,
        List<PodeDesembarcarInvalidOption> invalidOutput,
        out PodeDesembarcarReport report)
    {
        bool result = CollectOptionsInternal(
            selectedTransporter,
            map,
            terrainDatabase,
            output,
            invalidOutput,
            out PodeDesembarcarLandingStatus landingStatus);

        report = new PodeDesembarcarReport
        {
            localDePouso = landingStatus ?? new PodeDesembarcarLandingStatus(),
            locaisValidosDeDesembarque = output ?? new List<PodeDesembarcarOption>(),
            locaisInvalidosDeDesembarque = invalidOutput ?? new List<PodeDesembarcarInvalidOption>()
        };

        return result;
    }

    private static bool CollectOptionsInternal(
        UnitManager selectedTransporter,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        List<PodeDesembarcarOption> output,
        List<PodeDesembarcarInvalidOption> invalidOutput,
        out PodeDesembarcarLandingStatus landingStatus)
    {
        landingStatus = new PodeDesembarcarLandingStatus
        {
            isValid = false,
            explanation = "Contexto nao avaliado."
        };

        if (output == null)
            return false;

        output.Clear();
        invalidOutput?.Clear();

        if (selectedTransporter == null || map == null || selectedTransporter.IsEmbarked)
        {
            landingStatus.explanation = "Transportador invalido, mapa ausente ou unidade embarcada.";
            return false;
        }

        if (!selectedTransporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
        {
            landingStatus.explanation = "Transportador sem UnitData valido.";
            return false;
        }

        if (!transporterData.isTransporter || transporterData.transportSlots == null || transporterData.transportSlots.Count == 0)
        {
            landingStatus.explanation = "Unidade nao e um transportador com vagas.";
            return false;
        }

        landingStatus = EvaluateLandingStatus(selectedTransporter, map, terrainDatabase);

        if (!CanTransporterDisembarkAtCurrentContext(selectedTransporter, transporterData, map, terrainDatabase, out string transporterContextReason))
        {
            Vector3Int transporterFailCell = selectedTransporter.CurrentCellPosition;
            transporterFailCell.z = 0;
            AppendInvalid(
                invalidOutput,
                selectedTransporter,
                null,
                -1,
                -1,
                transporterFailCell,
                -1,
                transporterContextReason);
            return false;
        }

        IReadOnlyList<UnitTransportSeatRuntime> seats = selectedTransporter.TransportedUnitSlots;
        if (seats == null || seats.Count == 0)
            return false;

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        Vector3Int transporterCell = selectedTransporter.CurrentCellPosition;
        transporterCell.z = 0;
        UnitMovementPathRules.GetImmediateHexNeighbors(map, transporterCell, neighbors);

        bool hasAnyPassenger = false;
        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat == null || seat.embarkedUnit == null)
                continue;

            UnitManager passenger = seat.embarkedUnit;
            if (!passenger.IsEmbarked)
                continue;

            hasAnyPassenger = true;

            for (int n = 0; n < neighbors.Count; n++)
            {
                Vector3Int targetCell = neighbors[n];
                targetCell.z = 0;

                if (!CanDisembarkAtCell(
                        selectedTransporter,
                        transporterData,
                        passenger,
                        map,
                        terrainDatabase,
                        targetCell,
                        out int enterCost,
                        out string reason))
                {
                    AppendInvalid(
                        invalidOutput,
                        selectedTransporter,
                        passenger,
                        seat.slotIndex,
                        seat.seatIndex,
                        targetCell,
                        enterCost,
                        reason);
                    continue;
                }

                string slotLabel = !string.IsNullOrWhiteSpace(seat.slotId) ? seat.slotId : $"slot {seat.slotIndex}";
                string passengerName = passenger != null ? passenger.name : "passageiro";
                output.Add(new PodeDesembarcarOption
                {
                    transporterUnit = selectedTransporter,
                    passengerUnit = passenger,
                    transporterSlotIndex = seat.slotIndex,
                    transporterSeatIndex = seat.seatIndex,
                    disembarkCell = targetCell,
                    disembarkCost = 1,
                    enterCost = enterCost,
                    displayLabel = $"{passengerName} | {slotLabel} vaga {seat.seatIndex + 1} -> hex {targetCell.x},{targetCell.y} | custo={enterCost}"
                });
            }
        }

        if (!hasAnyPassenger)
        {
            AppendInvalid(
                invalidOutput,
                selectedTransporter,
                null,
                -1,
                -1,
                transporterCell,
                -1,
                "Transportador sem passageiros embarcados.");
        }

        return output.Count > 0;
    }

    private static PodeDesembarcarLandingStatus EvaluateLandingStatus(
        UnitManager transporter,
        Tilemap map,
        TerrainDatabase terrainDatabase)
    {
        var status = new PodeDesembarcarLandingStatus();
        if (transporter == null)
        {
            status.isValid = false;
            status.explanation = "Transportador invalido.";
            return status;
        }

        if (map == null)
        {
            status.isValid = false;
            status.explanation = "Tilemap base nao encontrado.";
            return status;
        }

        if (transporter.GetDomain() != Domain.Air)
        {
            status.isValid = true;
            status.explanation = "Transportador nao aereo; pouso nao se aplica.";
            return status;
        }

        AircraftOperationDecision landingProbe = AircraftOperationRules.Evaluate(
            transporter,
            map,
            terrainDatabase,
            SensorMovementMode.MoveuParado);

        bool canLand = landingProbe.available && landingProbe.action == AircraftOperationAction.Land;
        status.isValid = canLand;
        status.explanation = canLand
            ? "Pouso autorizado neste hex para o transportador."
            : (string.IsNullOrWhiteSpace(landingProbe.reason)
                ? "Pouso nao autorizado neste hex para o transportador."
                : landingProbe.reason);
        return status;
    }

    private static bool CanDisembarkAtCell(
        UnitManager transporter,
        UnitData transporterData,
        UnitManager passenger,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int targetCell,
        out int enterCost,
        out string reason)
    {
        enterCost = -1;
        reason = string.Empty;

        if (transporter == null || transporterData == null || passenger == null || map == null)
        {
            reason = "Contexto invalido para desembarque.";
            return false;
        }

        UnitManager blocker = UnitOccupancyRules.GetUnitAtCell(map, targetCell, transporter);
        if (blocker != null)
        {
            reason = $"Hex ocupado por {blocker.name}.";
            return false;
        }

        if (CanCarrierAirPassengerDisembarkAnywhere(transporter, transporterData, passenger))
        {
            enterCost = 1;
            reason = string.Empty;
            return true;
        }

        if (!CanPassengerAircraftUseTakeoffRuleForDisembark(
                transporter,
                transporterData,
                passenger,
                map,
                terrainDatabase,
                transporter.CurrentCellPosition,
                out string takeoffReason))
        {
            reason = takeoffReason;
            return false;
        }

        if (!IsContextAllowedByPassengerDisembarkDestinationRules(map, terrainDatabase, targetCell, transporterData, out string contextReason))
        {
            reason = contextReason;
            return false;
        }

        if (terrainDatabase != null &&
            TryResolveTerrainAtCell(map, terrainDatabase, targetCell, out TerrainTypeData targetTerrain) &&
            targetTerrain != null &&
            !targetTerrain.allowDisembark)
        {
            reason = $"Terreno '{ResolveTerrainLabel(targetTerrain)}' nao permite desembarque.";
            return false;
        }

        // Aeronave desembarcante segue regra de decolagem (0/1/full) e sai em Air/Low.
        // Nao deve ser bloqueada por custo de entrada terrestre do hex de destino.
        if (passenger.TryGetUnitData(out UnitData passengerData) && passengerData != null && passengerData.IsAircraft())
        {
            enterCost = 1;
            reason = string.Empty;
            return true;
        }

        if (!UnitMovementPathRules.TryGetEnterCellCost(
                map,
                passenger,
                targetCell,
                terrainDatabase,
                applyOperationalAutonomyModifier: false,
                out enterCost))
        {
            reason = "Passageiro nao pode desembarcar neste hex (dominio/altura/skill/restricao).";
            return false;
        }

        if (enterCost > 1)
        {
            reason = $"Custo de desembarque maior que 1 (custo={enterCost}).";
            return false;
        }

        enterCost = 1;
        return true;
    }

    private static bool CanPassengerAircraftUseTakeoffRuleForDisembark(
        UnitManager transporter,
        UnitData transporterData,
        UnitManager passenger,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int transporterCell,
        out string reason)
    {
        reason = string.Empty;
        if (passenger == null || map == null)
        {
            reason = "Contexto invalido para validar decolagem do passageiro.";
            return false;
        }

        if (!passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null || !passengerData.IsAircraft())
            return true;

        // Caça/aviao saindo de carrier naval deve seguir regra especial de 1 hex
        // já tratada no sensor de desembarque (CanCarrierAirPassengerDisembarkAnywhere).
        if (CanCarrierAirPassengerDisembarkAnywhere(transporter, transporterData, passenger))
            return true;

        transporterCell.z = 0;
        AirOperationTileContext tileContext = AirOperationResolver.ResolveContext(map, terrainDatabase, transporterCell);
        if (!AirOperationResolver.TryGetTakeoffPlan(
                passenger,
                tileContext,
                SensorMovementMode.MoveuParado,
                out AirTakeoffPlan plan,
                out string takeoffPlanReason))
        {
            reason = string.IsNullOrWhiteSpace(takeoffPlanReason)
                ? "Aeronave nao pode decolar deste hex para desembarque."
                : $"Aeronave nao pode decolar deste hex: {takeoffPlanReason}";
            return false;
        }

        bool canFullMove = plan.procedure == TakeoffProcedure.InstantToPreferredHeight;
        bool can0 = plan.rollMinHex == 0;
        bool can1 = plan.rollMaxHex >= 1;
        if (canFullMove || can1)
            return true;

        if (can0)
        {
            reason = "Aeronave neste hex permite apenas decolagem 0; desembarque exige saida de 1 hex.";
            return false;
        }

        reason = "Aeronave sem regra de decolagem valida (0/1/[0,1]) para desembarque.";
        return false;
    }

    private static bool CanCarrierAirPassengerDisembarkAnywhere(
        UnitManager transporter,
        UnitData transporterData,
        UnitManager passenger)
    {
        if (transporter == null || transporterData == null || passenger == null)
            return false;

        if (transporter.GetDomain() != Domain.Naval)
            return false;

        if (!PassengerSupportsAnyAirLayer(passenger))
            return false;

        return HasAnyCarrierLikeSlot(transporterData);
    }

    private static bool PassengerSupportsAnyAirLayer(UnitManager passenger)
    {
        if (passenger == null)
            return false;

        if (passenger.GetDomain() == Domain.Air)
            return true;

        return passenger.SupportsLayerMode(Domain.Air, HeightLevel.AirLow) ||
               passenger.SupportsLayerMode(Domain.Air, HeightLevel.AirHigh);
    }

    private static bool HasAnyCarrierLikeSlot(UnitData transporterData)
    {
        if (transporterData == null || transporterData.transportSlots == null || transporterData.transportSlots.Count == 0)
            return false;

        for (int i = 0; i < transporterData.transportSlots.Count; i++)
        {
            UnitTransportSlotRule slot = transporterData.transportSlots[i];
            if (slot == null || slot.allowedLayerModes == null || slot.allowedLayerModes.Count == 0)
                continue;

            for (int j = 0; j < slot.allowedLayerModes.Count; j++)
            {
                TransportSlotLayerMode mode = slot.allowedLayerModes[j];
                if (mode.domain == Domain.Air &&
                    (mode.heightLevel == HeightLevel.AirLow || mode.heightLevel == HeightLevel.AirHigh))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsContextAllowedByPassengerDisembarkDestinationRules(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        UnitData transporterData,
        out string reason)
    {
        reason = string.Empty;
        if (map == null || transporterData == null)
        {
            reason = "Contexto invalido para filtro de desembarque.";
            return false;
        }

        bool hasConstructionFilter = transporterData.passengersCanDisembarkAndGoesToConstructions != null && transporterData.passengersCanDisembarkAndGoesToConstructions.Count > 0;
        bool hasStructureFilter = transporterData.passengersCanDisembarkAndGoesToTerrainStructures != null && transporterData.passengersCanDisembarkAndGoesToTerrainStructures.Count > 0;
        bool hasTerrainFilter = transporterData.passengersCanDisembarkAndGoesToTerrains != null && transporterData.passengersCanDisembarkAndGoesToTerrains.Count > 0;
        if (!hasConstructionFilter && !hasStructureFilter && !hasTerrainFilter)
            return true;

        cell.z = 0;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(map, cell);
        if (construction != null && construction.TryResolveConstructionData(out ConstructionData constructionData) && constructionData != null)
        {
            if (hasConstructionFilter)
            {
                if (transporterData.passengersCanDisembarkAndGoesToConstructions.Contains(constructionData))
                    return true;

                reason = "Construcao do hex nao permitida para desembarque deste transportador.";
                return false;
            }

            // Lista vazia = sem restricao por construcao.
            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(map, cell);
        if (structure != null)
        {
            if (!hasStructureFilter)
            {
                reason = "Estrutura do hex nao permitida para desembarque deste transportador.";
                return false;
            }

            if (!TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrainAtStructure) || terrainAtStructure == null)
            {
                reason = "Estrutura encontrada, mas sem terreno base valido no hex.";
                return false;
            }

            PairRuleMatchResult destinationPairResult = EvaluateDisembarkStructureTerrainPair(
                transporterData.passengersCanDisembarkAndGoesToTerrainStructures,
                structure,
                terrainAtStructure);
            if (destinationPairResult == PairRuleMatchResult.Blocked)
            {
                reason = "Par estrutura+terreno base bloqueado para destino de desembarque.";
                return false;
            }

            if (destinationPairResult != PairRuleMatchResult.Allowed)
            {
                reason = "Par estrutura+terreno base nao permitido para desembarque deste transportador.";
                return false;
            }

            return true;
        }

        if (hasTerrainFilter &&
            TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) &&
            terrain != null &&
            transporterData.passengersCanDisembarkAndGoesToTerrains.Contains(terrain))
        {
            return true;
        }

        reason = "Contexto nao permitido (fora de terreno/estrutura/construcao permitidos para desembarque).";
        return false;
    }

    private static PairRuleMatchResult EvaluateDisembarkStructureTerrainPair(
        List<TransportStructureTerrainRule> rules,
        StructureData structure,
        TerrainTypeData baseTerrain)
    {
        if (rules == null || structure == null || baseTerrain == null)
            return PairRuleMatchResult.NotListed;

        for (int i = 0; i < rules.Count; i++)
        {
            TransportStructureTerrainRule rule = rules[i];
            if (rule == null || rule.structure == null || rule.baseTerrain == null)
                continue;
            if (rule.structure == structure && rule.baseTerrain == baseTerrain)
                return rule.isBlocked ? PairRuleMatchResult.Blocked : PairRuleMatchResult.Allowed;
        }

        return PairRuleMatchResult.NotListed;
    }

    private static bool CanTransporterDisembarkAtCurrentContext(
        UnitManager transporter,
        UnitData transporterData,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        out string reason)
    {
        reason = string.Empty;
        if (transporter == null)
        {
            reason = "Transportador invalido para desembarque.";
            return false;
        }

        if (transporter.GetDomain() != Domain.Air)
            return IsContextAllowedByTransporterCurrentHexDisembarkRules(
                map,
                terrainDatabase,
                transporter.CurrentCellPosition,
                transporterData,
                out reason);

        AircraftOperationDecision landingProbe = AircraftOperationRules.Evaluate(
            transporter,
            map,
            terrainDatabase,
            SensorMovementMode.MoveuParado);

        if (!(landingProbe.available && landingProbe.action == AircraftOperationAction.Land))
        {
            reason = string.IsNullOrWhiteSpace(landingProbe.reason)
                ? "Transportador aereo sem pouso valido; desembarque indisponivel."
                : $"Transportador aereo sem pouso valido: {landingProbe.reason}";
            return false;
        }

        return IsContextAllowedByTransporterCurrentHexDisembarkRules(
            map,
            terrainDatabase,
            transporter.CurrentCellPosition,
            transporterData,
            out reason);
    }

    private static bool IsContextAllowedByTransporterCurrentHexDisembarkRules(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        UnitData transporterData,
        out string reason)
    {
        reason = string.Empty;
        if (map == null || transporterData == null)
        {
            reason = "Contexto invalido para filtro de desembarque no hex do transportador.";
            return false;
        }

        bool hasConstructionFilter = transporterData.allowedDisembarkWhenTransporterAtConstructions != null && transporterData.allowedDisembarkWhenTransporterAtConstructions.Count > 0;
        bool hasStructureFilter = transporterData.allowedDisembarkWhenTransporterAtTerrainStructures != null && transporterData.allowedDisembarkWhenTransporterAtTerrainStructures.Count > 0;
        bool hasTerrainFilter = transporterData.allowedDisembarkWhenTransporterAtTerrains != null && transporterData.allowedDisembarkWhenTransporterAtTerrains.Count > 0;
        if (!hasConstructionFilter && !hasStructureFilter && !hasTerrainFilter)
            return true;

        cell.z = 0;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(map, cell);
        if (construction != null && construction.TryResolveConstructionData(out ConstructionData constructionData) && constructionData != null)
        {
            if (hasConstructionFilter)
            {
                if (transporterData.allowedDisembarkWhenTransporterAtConstructions.Contains(constructionData))
                    return true;

                reason = "Hex atual do transportador (construcao) nao permitido para desembarque.";
                return false;
            }

            // Lista vazia = sem restricao por construcao.
            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(map, cell);
        if (structure != null)
        {
            if (!hasStructureFilter)
            {
                reason = "Hex atual do transportador (estrutura) nao permitido para desembarque.";
                return false;
            }

            if (!TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrainAtStructure) || terrainAtStructure == null)
            {
                reason = "Hex atual com estrutura, mas sem terreno base valido.";
                return false;
            }

            PairRuleMatchResult currentHexPairResult = EvaluateDisembarkStructureTerrainPair(
                transporterData.allowedDisembarkWhenTransporterAtTerrainStructures,
                structure,
                terrainAtStructure);
            if (currentHexPairResult == PairRuleMatchResult.Blocked)
            {
                reason = "Hex atual do transportador com par estrutura+terreno base bloqueado para desembarque.";
                return false;
            }

            if (currentHexPairResult != PairRuleMatchResult.Allowed)
            {
                reason = "Hex atual do transportador (par estrutura+terreno base) nao permitido para desembarque.";
                return false;
            }

            return true;
        }

        if (hasTerrainFilter &&
            TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) &&
            terrain != null &&
            transporterData.allowedDisembarkWhenTransporterAtTerrains.Contains(terrain))
        {
            return true;
        }

        reason = "Hex atual do transportador fora de terreno/estrutura/construcao permitidos para desembarque.";
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

    private static void AppendInvalid(
        List<PodeDesembarcarInvalidOption> invalidOutput,
        UnitManager transporter,
        UnitManager passenger,
        int slotIndex,
        int seatIndex,
        Vector3Int cell,
        int enterCost,
        string reason)
    {
        if (invalidOutput == null)
            return;

        invalidOutput.Add(new PodeDesembarcarInvalidOption
        {
            transporterUnit = transporter,
            passengerUnit = passenger,
            transporterSlotIndex = slotIndex,
            transporterSeatIndex = seatIndex,
            evaluatedCell = cell,
            enterCost = enterCost,
            reason = reason
        });
    }

    private static string ResolveTerrainLabel(TerrainTypeData terrain)
    {
        if (terrain == null)
            return "(terreno)";
        if (!string.IsNullOrWhiteSpace(terrain.displayName))
            return terrain.displayName;
        if (!string.IsNullOrWhiteSpace(terrain.id))
            return terrain.id;
        return terrain.name;
    }

    private enum PairRuleMatchResult
    {
        NotListed = 0,
        Allowed = 1,
        Blocked = 2
    }
}
