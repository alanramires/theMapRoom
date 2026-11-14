using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // Only diagnostic deduplication. Recovery evaluation itself is unchanged.
    private AIWorldSnapshot repairFusionRejectionLogSnapshot;
    private readonly HashSet<(int actor, int partner, Vector3Int cell)>
        repairFusionRecoveryRejections = new HashSet<(int, int, Vector3Int)>();

    private readonly struct AircraftRecoveryAnchor
    {
        public readonly Vector3Int Cell;
        public readonly string Kind;
        public readonly int ServiceRadius;
        public readonly HashSet<Vector3Int> ServiceCells;
        public readonly UnitManager Transporter;
        public readonly ConstructionManager Construction;

        public AircraftRecoveryAnchor(
            Vector3Int cell,
            string kind,
            int serviceRadius = 0,
            HashSet<Vector3Int> serviceCells = null,
            UnitManager transporter = null,
            ConstructionManager construction = null)
        {
            cell.z = 0;
            Cell = cell;
            Kind = kind;
            ServiceRadius = Mathf.Max(0, serviceRadius);
            ServiceCells = serviceCells;
            Transporter = transporter;
            Construction = construction;
        }
    }

    private bool HasAircraftRecoveryWithinReach(
        UnitManager aircraft,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> tacticalPaths,
        HashSet<Vector3Int> occupied)
    {
        return EvaluateAircraftRecoveryReach(
            aircraft, snapshot, fromCell, tacticalPaths, occupied).Found;
    }

    private AIReachDecisionResult<AircraftRecoveryAnchor>
        EvaluateAircraftRecoveryReach(
            UnitManager aircraft,
            AIWorldSnapshot snapshot,
            Vector3Int fromCell,
            Dictionary<Vector3Int, List<Vector3Int>> tacticalPaths,
            HashSet<Vector3Int> occupied)
    {
        if (aircraft == null || snapshot == null
            || aircraft.GetAircraftType() == AircraftType.None)
        {
            return new AIReachDecisionResult<AircraftRecoveryAnchor>();
        }

        List<AircraftRecoveryAnchor> anchors =
            CollectAircraftRecoveryAnchors(aircraft, snapshot);
        var request = new AIReachDecisionRequest<AircraftRecoveryAnchor>
        {
            Context = $"AircraftRepairRecovery#{aircraft.InstanceId}",
            Policy = new AIReachDecisionPolicy(
                AIReachDecisionStages.Tactical | AIReachDecisionStages.Operational),
            CurrentMovementBudget = Mathf.Max(0, aircraft.RemainingMovementPoints),
            EvaluateTactical = (int budget, out AIReachDecisionCandidate<AircraftRecoveryAnchor> candidate) =>
                TryEvaluateAircraftRecoveryAnchor(
                    aircraft,
                    anchors, snapshot, fromCell, tacticalPaths, occupied, budget, tactical: true, out candidate),
            EvaluateOperational = (int budget, out AIReachDecisionCandidate<AircraftRecoveryAnchor> candidate) =>
                TryEvaluateAircraftRecoveryAnchor(
                    aircraft,
                    anchors, snapshot, fromCell, null, occupied, budget, tactical: false, out candidate),
            DiagnosticLog = showAILogs ? (System.Action<string>)Debug.Log : null
        };
        return AIActionReachCoordinator.Evaluate(request);
    }

    private bool TryBuildAircraftRecoveryApproachAction(
        UnitManager aircraft,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        AIReachDecisionResult<AircraftRecoveryAnchor> recovery =
            EvaluateAircraftRecoveryReach(
                aircraft, snapshot, fromCell, paths, occupied);
        if (!recovery.Found || recovery.Decision == null
            || paths == null || paths.Count == 0)
        {
            return false;
        }

        Vector3Int target = recovery.Decision.TargetCell;
        target.z = 0;
        Vector3Int destination = recovery.Decision.ActionCell;
        destination.z = 0;

        // Plataforma naval nao e apenas uma direcao de recuperacao: se o
        // Apache consegue alcancar a zona de embarque agora, o mesmo batch
        // precisa terminar no fluxo oficial mover -> embarcar. A legalidade,
        // vaga e custo continuam sendo revalidados por PodeEmbarcar.
        AircraftRecoveryAnchor chosenAnchor = recovery.Decision.Value;
        if (recovery.Tier == AIReachDecisionTier.Tactical
            && chosenAnchor.Transporter != null
            && TryBuildAircraftRecoveryPlatformEmbarkAction(
                aircraft, snapshot, fromCell, chosenAnchor.Transporter,
                paths, out action))
        {
            return true;
        }

        // No Operational o coordenador devolve a ancora; a aeronave so pode
        // materializar o trecho desta rodada. Escolhe o hex aereo valido que
        // mais reduz a distancia ate ela. No Tactical o ActionCell ja e a
        // celula de atendimento encontrada no path map.
        if (!paths.ContainsKey(destination))
        {
            float bestScore = float.MinValue;
            destination = fromCell;
            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell != fromCell && occupied != null && occupied.Contains(cell))
                    continue;
                float score = -AIActionReachCoordinator.CubicDistance(
                    cell, target) * 1000f
                    - CalculateThreatLevel(cell, snapshot.AITeam)
                        * ThreatWeight;
                if (score > bestScore)
                {
                    bestScore = score;
                    destination = cell;
                }
            }
        }

        if (destination == fromCell)
            return false;

        Debug.Log($"{TL("Repair")} aeronave #{aircraft.InstanceId} " +
                  $"recuperacao {recovery.Tier}: " +
                  $"{recovery.Decision.Reason} alvo={target} " +
                  $"via={destination}");
        action = BuildMoveBatch(
            aircraft, snapshot.AITeam, fromCell, destination, paths);
        return true;
    }

    private bool TryBuildAircraftRecoveryPlatformEmbarkAction(
        UnitManager aircraft,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        UnitManager platform,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out PlayerAction action)
    {
        action = null;
        if (aircraft == null || platform == null || snapshot == null)
            return false;

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        if (!TryBuildRepairEvacExtendedEmbarkAction(
                aircraft, snapshot, plan, snapshot.AITeam, fromCell,
                platform, paths, out action))
        {
            return false;
        }

        Debug.Log($"{TL("Repair")} aeronave #{aircraft.InstanceId} " +
                  $"recuperacao Tactical: embarca na plataforma " +
                  $"#{platform.InstanceId}.");
        return true;
    }

    private bool TryEvaluateAircraftRecoveryAnchor(
        UnitManager aircraft,
        List<AircraftRecoveryAnchor> anchors,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> tacticalPaths,
        HashSet<Vector3Int> occupied,
        int budget,
        bool tactical,
        out AIReachDecisionCandidate<AircraftRecoveryAnchor> candidate)
    {
        candidate = null;
        float bestScore = float.MinValue;
        bool bestIsSupplier = false;
        // Facility affinity compares hospitals; a field supplier keeps its existing score.
        bool hasPreferredConstruction = anchors.Exists(anchor => anchor.Construction != null
            && IsPreferredRepairFacility(aircraft, anchor.Construction)
            && AIActionReachCoordinator.CubicDistance(fromCell, anchor.Cell) <= budget
            && (anchor.Cell == fromCell || occupied == null || !occupied.Contains(anchor.Cell))
            && (!tactical || (tacticalPaths != null && tacticalPaths.ContainsKey(anchor.Cell))));
        for (int i = 0; i < anchors.Count; i++)
        {
            AircraftRecoveryAnchor anchor = anchors[i];
            if (hasPreferredConstruction && anchor.Construction != null
                && !IsPreferredRepairFacility(aircraft, anchor.Construction)) continue;
            int anchorDistance = Mathf.Max(0,
                AIActionReachCoordinator.CubicDistance(fromCell, anchor.Cell) - anchor.ServiceRadius);
            if (anchorDistance > budget)
                continue;

            // Supplier cells were validated once during collection, including
            // the future landing layer. Operational uses that same evidence.
            IEnumerable<Vector3Int> cells = tactical
                ? (IEnumerable<Vector3Int>)tacticalPaths?.Keys
                : anchor.ServiceCells ?? (IEnumerable<Vector3Int>)new[] { anchor.Cell };
            if (cells == null)
                continue;
            bool isSupplier = anchor.ServiceCells != null;
            foreach (Vector3Int rawCell in cells)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell != fromCell && occupied != null && occupied.Contains(cell))
                    continue;
                if (AIActionReachCoordinator.CubicDistance(cell, anchor.Cell) > anchor.ServiceRadius
                    || (isSupplier && !anchor.ServiceCells.Contains(cell)))
                    continue;
                int distance = isSupplier
                    ? AIActionReachCoordinator.CubicDistance(fromCell, cell)
                    : anchorDistance;
                if (distance > budget)
                    continue;
                float score = 10000f - distance * 100f
                    - CalculateThreatLevel(cell, snapshot.AITeam) * ThreatWeight;
                // No bonus can override distance or threat. A field supplier
                // only wins an exact tie, leaving a construction vacancy free.
                if (score < bestScore
                    || (score == bestScore && (!isSupplier || bestIsSupplier)))
                    continue;
                bestScore = score;
                bestIsSupplier = isSupplier;
                candidate = new AIReachDecisionCandidate<AircraftRecoveryAnchor>
                {
                    Value = anchor,
                    ActionCell = cell,
                    TargetCell = anchor.Cell,
                    Score = score,
                    Reason = anchor.Kind
                };
            }
        }
        return candidate != null;
    }

    private List<AircraftRecoveryAnchor> CollectAircraftRecoveryAnchors(
        UnitManager aircraft,
        AIWorldSnapshot snapshot)
    {
        if (!object.ReferenceEquals(repairFusionRejectionLogSnapshot, snapshot))
        {
            repairFusionRejectionLogSnapshot = snapshot;
            repairFusionRecoveryRejections.Clear();
        }
        var result = new List<AircraftRecoveryAnchor>();
        var landingFallbacks = new List<AircraftRecoveryAnchor>();
        var seenCells = new HashSet<Vector3Int>();
        bool hasPrimaryLandingRecovery = false;
        int aiSlot = ResolveAISlotKey(snapshot.AITeam);

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null
                || construction.SlotIndex != aiSlot
                || construction.CurrentCapturePoints < construction.CapturePointsMax
                || !IsAircraftRepairConstruction(construction)
                || !CanSurfaceRepairConstructionServe(construction, aircraft, snapshot.AITeam))
            {
                continue;
            }

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (PodePousarSensor.CanLandAtCell(
                    aircraft, boardTilemap, terrainDatabase, cell, out _,
                    SensorMovementMode.MoveuAndando))
            {
                if (seenCells.Add(cell))
                {
                    result.Add(new AircraftRecoveryAnchor(
                        cell, "aerodromo/construcao de reparo", construction: construction));
                    hasPrimaryLandingRecovery = true;
                }
            }
        }

        // Um aviao em reparo nao procura apenas construcoes. A mesma consulta
        // usada pela ferramenta Melhor Local para Pouso organiza LZs de
        // Terrain/Structure Aircraft Ops dentro da autonomia real; o repair
        // apenas converte suas respostas em ancoras.
        if (boardTilemap != null && terrainDatabase != null)
        {
            MelhorPousoResult landingOptions = MelhorPousoService.Evaluate(
                new MelhorPousoRequest
                {
                    aircraft = aircraft,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    tacticalBudget = Mathf.Max(
                        0, aircraft.RemainingMovementPoints),
                    operationalTurns = 2
                });
            for (int i = 0; i < landingOptions.options.Count; i++)
            {
                MelhorPousoOption landingOption = landingOptions.options[i];
                if (landingOption == null)
                    continue;
                Vector3Int cell = landingOption.cell;
                cell.z = 0;
                if (!seenCells.Add(cell))
                {
                    continue;
                }

                AirOperationTileContext context =
                    AirOperationResolver.ResolveContext(
                        boardTilemap, terrainDatabase, cell);
                string kind = context.landingSurface ==
                    LandingSurface.RoadRunway
                    ? "LZ estrada" : "LZ valida";
                landingFallbacks.Add(new AircraftRecoveryAnchor(cell, kind));
            }
        }

        if (snapshot.MyUnits != null)
        {
            for (int i = 0; i < snapshot.MyUnits.Count; i++)
            {
                UnitManager ally = snapshot.MyUnits[i];
                if (ally == null || ally == aircraft || ally.IsDead || ally.IsEmbarked)
                    continue;

                Vector3Int cell = ally.CurrentCellPosition;
                cell.z = 0;
                if (IsPrimaryLogisticsUnit(ally))
                {
                    var serviceCells = new HashSet<Vector3Int>();
                    var neighbors = new List<Vector3Int>(7);
                    UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, cell, neighbors);
                    neighbors.Add(cell);
                    foreach (Vector3Int serviceCell in neighbors)
                    {
                        if (CanSupplierRecoverAircraft(ally, aircraft, serviceCell, out string reason))
                            serviceCells.Add(serviceCell);
                        else if (showAILogs)
                            Debug.Log($"{TL("Repair")} supridor #{ally.InstanceId} recusado em {serviceCell}: {reason}");
                    }
                    if (serviceCells.Count > 0)
                    {
                        result.Add(new AircraftRecoveryAnchor(
                            cell, "supridor", serviceRadius: 1, serviceCells: serviceCells));
                        hasPrimaryLandingRecovery = true;
                    }
                }
                if (IsPotentialRepairFusionTarget(aircraft, ally))
                {
                    if (IsRepairFusionSiteSafe(aircraft, snapshot, cell))
                        result.Add(new AircraftRecoveryAnchor(cell, "fusao", serviceRadius: 1));
                    else if (repairFusionRecoveryRejections.Add((aircraft.InstanceId, ally.InstanceId, cell)))
                    {
                        string reason = !IsEvacDropCellSafe(cell, snapshot.AITeam)
                            ? "célula sob ameaça" : "fora da retaguarda";
                        Debug.Log($"{TL("Repair")} fusão com #{ally.InstanceId} recusada como recuperação: {reason} @ {cell}");
                    }
                }
            }
        }

        UnitManager platform = FindBestNavalRepairPlatform(
            aircraft, aircraft.CurrentCellPosition, snapshot.AITeam, out _, out _);
        if (platform != null)
        {
            result.Add(new AircraftRecoveryAnchor(
                platform.CurrentCellPosition, "plataforma naval",
                serviceRadius: 1, transporter: platform));

            hasPrimaryLandingRecovery = true;
        }

        // Pista, estrada STOVL e demais LZs validas sao o plano de pouso de
        // emergencia. Elas nao roubam a prioridade de uma fonte que realmente
        // possa atender/reparar a aeronave.
        if (!hasPrimaryLandingRecovery)
            result.AddRange(landingFallbacks);

        return result;
    }

    private bool CanSupplierRecoverAircraft(
        UnitManager supplier,
        UnitManager aircraft,
        Vector3Int cell,
        out string reason)
    {
        reason = "supridor incompativel";
        if (!IsPrimaryLogisticsUnit(supplier)
            || aircraft == null
            || !supplier.TryGetUnitData(out UnitData supplierData)
            || supplierData == null)
            return false;

        if (PodeSuprirSensor.SupportsOperationDomain(
                supplierData, aircraft.GetDomain(), aircraft.GetHeightLevel()))
        {
            reason = "atende na camada de voo";
            return CanAIUnitEndMoveAtCell(aircraft, cell, UnitOccupancyRules.GetUnitsAtCell(boardTilemap, cell, aircraft));
        }

        // Same pure per-cell authority as CanLandAtCell; retain its report
        // to check the supplier against the actual resulting landing layer.
        PodePousarReport landing = PodePousarSensor.Evaluate(
            aircraft, boardTilemap, terrainDatabase,
            SensorMovementMode.MoveuAndando,
            useManualRemainingMovement: false,
            manualRemainingMovement: 0,
            atCell: cell);
        if (landing == null || !landing.status)
        {
            reason = landing != null ? landing.explicacao : "pouso sem resultado";
            return false;
        }
        if (!PodeSuprirSensor.SupportsOperationDomain(
                supplierData, landing.landingDomain, landing.landingHeight))
        {
            reason = $"supridor nao atende {landing.landingDomain}/{landing.landingHeight}";
            return false;
        }
        reason = "pouso e camada de atendimento validos";
        return true;
    }

    private bool IsAircraftFuelCriticalForNextUpkeep(
        UnitManager aircraft,
        out int movementAllowance,
        out int nextUpkeep,
        out int threshold)
    {
        movementAllowance = aircraft != null
            ? Mathf.Max(0, aircraft.GetMovementRange())
            : 0;
        nextUpkeep = OperationalAutonomyRules.GetTurnStartAutonomyUpkeep(
            aircraft, matchController != null ? matchController.AutonomyDatabase : null);
        threshold = movementAllowance + nextUpkeep;
        return aircraft != null && aircraft.CurrentFuel <= threshold;
    }

    private bool TryBuildAircraftEmergencyHoverAction(
        UnitManager aircraft,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool critical,
        out PlayerAction action)
    {
        action = null;
        if (aircraft == null || paths == null || paths.Count == 0)
            return false;

        bool found = false;
        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        string bestReason = string.Empty;

        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in paths)
        {
            Vector3Int cell = pair.Key;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;

            SensorMovementMode mode = cell == fromCell
                ? SensorMovementMode.MoveuParado
                : SensorMovementMode.MoveuAndando;
            if (!PodePousarSensor.CanLandAtCell(
                    aircraft, boardTilemap, terrainDatabase, cell, out string landingReason, mode))
            {
                continue;
            }

            int fuelCost = UnitMovementPathRules.CalculateAutonomyCostForPath(
                boardTilemap, aircraft, pair.Value, terrainDatabase,
                applyOperationalAutonomyModifier: true);
            if (fuelCost > aircraft.CurrentFuel)
                continue;

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            ConstructionManager construction =
                ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            AirOperationTileContext context =
                AirOperationResolver.ResolveContext(boardTilemap, terrainDatabase, cell);
            float score =
                (construction != null && construction.SlotIndex == ResolveAISlotKey(snapshot.AITeam) ? 2500f : 0f)
                + (context.landingSurface == LandingSurface.RoadRunway ? 1200f : 0f)
                + (aircraft.CurrentFuel - fuelCost) * 20f
                - threat * ThreatWeight * 1.5f
                - fuelCost * 30f
                - cell.y * 0.001f
                - cell.x * 0.000001f;

            if (score <= bestScore)
                continue;

            found = true;
            bestScore = score;
            bestCell = cell;
            bestReason = $"{landingReason} threat={threat:F1} fuelCost={fuelCost}";
        }

        if (!found)
        {
            if (TryBuildAircraftLandingApproachAction(
                    aircraft, snapshot, fromCell, paths, occupied, out action))
            {
                return true;
            }
            if (critical)
            {
                Debug.LogWarning(
                    $"{TL("Repair")} aeronave #{aircraft.InstanceId} em autonomia critica sem hex " +
                    $"de pouso de emergencia alcancavel; risco de queda inevitavel.");
            }
            return false;
        }

        AirOperationTileContext emergencyLandingContext =
            AirOperationResolver.ResolveContext(
                boardTilemap,
                terrainDatabase,
                bestCell);
        bool landsOnFirmGround =
            emergencyLandingContext.landingSurface
                != LandingSurface.None;
        if (landsOnFirmGround
            && HasTransportCargo(aircraft)
            && TryBuildAirTransportRepairArrivalRelease(
                aircraft,
                snapshot,
                fromCell,
                bestCell,
                paths,
                out PlayerAction emergencyDropAction,
                forceEmergencyRelease: true))
        {
            Debug.Log(
                $"{TL("Repair")} aeronave #{aircraft.InstanceId} " +
                $"segue para LZ terrestre de emergencia {bestCell} e " +
                "tenta desembarcar a carga antes do pouso no upkeep " +
                $"({bestReason} score={bestScore:F0}).");
            emergencyDropAction.DebugLabel =
                $"EmergencyLandingCargoRelease " +
                $"{aircraft.InstanceId} via {bestCell}";
            action = emergencyDropAction;
            return true;
        }

        Debug.Log(
            $"{TL("Repair")} aeronave #{aircraft.InstanceId} {(critical ? "CRITICA" : "sem recuperacao")} " +
            $"permanece em voo sobre LZ {bestCell}; pouso somente no upkeep " +
            $"({bestReason} score={bestScore:F0})");
        action = BuildMoveBatch(aircraft, snapshot.AITeam, fromCell, bestCell, paths);
        return true;
    }

    private bool TryBuildAircraftLandingApproachAction(
        UnitManager aircraft,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (boardTilemap == null)
            return false;

        bool found = false;
        Vector3Int bestStep = fromCell;
        Vector3Int bestLandingCell = fromCell;
        float bestScore = float.MinValue;

        foreach (Vector3Int rawLandingCell in boardTilemap.cellBounds.allPositionsWithin)
        {
            Vector3Int landingCell = rawLandingCell;
            landingCell.z = 0;
            if (!boardTilemap.HasTile(landingCell)
                || !PodePousarSensor.CanLandAtCell(
                    aircraft, boardTilemap, terrainDatabase, landingCell, out _,
                    SensorMovementMode.MoveuAndando))
            {
                continue;
            }

            foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in paths)
            {
                Vector3Int step = pair.Key;
                step.z = 0;
                if (step != fromCell && occupied != null && occupied.Contains(step))
                    continue;

                int fuelCost = UnitMovementPathRules.CalculateAutonomyCostForPath(
                    boardTilemap, aircraft, pair.Value, terrainDatabase,
                    applyOperationalAutonomyModifier: true);
                int remainingDistance =
                    AIActionReachCoordinator.CubicDistance(step, landingCell);
                float score =
                    -remainingDistance * 1000f
                    -CalculateThreatLevel(step, snapshot.AITeam) * ThreatWeight
                    -fuelCost * 25f
                    -landingCell.y * 0.000001f
                    -landingCell.x * 0.000000001f;
                if (score <= bestScore)
                    continue;

                found = true;
                bestScore = score;
                bestStep = step;
                bestLandingCell = landingCell;
            }
        }

        if (!found)
            return false;

        Debug.LogWarning(
            $"{TL("Repair")} aeronave #{aircraft.InstanceId} sem LZ alcancavel nesta rodada; " +
            $"aproxima de {bestLandingCell} via {bestStep}, permanece em voo e conserva a opcao de upkeep.");
        action = BuildMoveBatch(aircraft, snapshot.AITeam, fromCell, bestStep, paths);
        return true;
    }
}
