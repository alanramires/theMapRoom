using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Rogue Shuttle — empty transporter, no formal plan
    // -------------------------------------------------------------------------

    private PlayerAction DecideRogueShuttleAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        UnitManager bestCandidate = FindBestShuttleCandidate(unit, snapshot, plan, fromCell, out Vector3Int candidateCell);

        // Second pass with reduced threshold — catches units slightly below the formal cutoff
        // that would still benefit from a ride rather than leaving the APC idle.
        if (bestCandidate == null)
        {
            int relaxed = Mathf.Max(2, GetEffectiveTransportThresholdForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex)) / 2);
            bestCandidate = FindBestShuttleCandidate(unit, snapshot, plan, fromCell, out candidateCell,
                thresholdReduction: relaxed);
            if (bestCandidate != null)
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} shuttle — candidato relaxado {bestCandidate.InstanceId}@{candidateCell} (threshold -{relaxed})");
        }

        bool preferNoMove = unit.TryGetUnitData(out UnitData shuttleData) && shuttleData.prioritizeDpqAtBattle;

        if (bestCandidate != null)
        {
            if (TryFindTransportBreakerAttack(unit, snapshot, fromCell, paths, occupied, candidateCell,
                    out Vector3Int attackCell, out UnitManager attackTarget, preferNoMove, plan))
            {
                Vector3Int targetCell = attackTarget.CurrentCellPosition; targetCell.z = 0;
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} shuttle — ataca oportunista {attackTarget.InstanceId} via {attackCell}");
                return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                    attackTarget.InstanceId.ToString(), targetCell, paths);
            }

            Vector3Int candidateObjective = ResolveUnitObjectiveCell(bestCandidate, plan, snapshot);
            Vector3Int moveTarget = FindTransportShuttleMove(unit, fromCell, candidateCell, paths, occupied, snapshot.AITeam, candidateObjective);
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} shuttle — candidato {bestCandidate.InstanceId}@{candidateCell} via {moveTarget}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
        }

        // EVAC: no capturer to shuttle — check for frontline unit-under-repair before releasing.
        PlayerAction evacAction = TryDecideEvacShuttleAction(unit, snapshot, plan, paths, occupied);
        if (evacAction != null) return evacAction;

        Debug.Log($"{TL("Transporte")} {unit.InstanceId} shuttle — sem candidato de embarque");
        return null;
    }

    // -------------------------------------------------------------------------
    // Candidate selection
    // -------------------------------------------------------------------------

    private UnitManager FindBestShuttleCandidate(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int transporterCell,
        out Vector3Int bestCandidateCell,
        SectorObjective assignedSector = null,
        int thresholdReduction = 0)
    {
        bestCandidateCell = transporterCell;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
            return null;

        UnitManager best = null;
        float bestScore = float.MinValue;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == transporter) continue;
            if (candidate.SlotIndex != snapshot.AISlotIndex || candidate.IsDead || candidate.IsEmbarked || candidate.HasActed) continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData)) continue;

            if (FindFittingSlotIndex(transporter, transporterData, candidate, candidateData) < 0) continue;

            // Skip candidates that are already the formal passenger of another transporter.
            // A candidate is "taken" when both conditions hold in the same SectorObjective:
            //   1. they occupy a filled Capturador slot, AND
            //   2. a different transporter occupies a filled Transportador slot.
            if (IsAlreadyFormalPassenger(candidate, transporter, plan)) continue;

            // Transporte sem objetivo formal pode atender um capturador planejado:
            // o embarque atual classifica esse caso como freeTransport. A reserva de
            // passageiro de OUTRO transportador ja foi protegida acima por
            // IsAlreadyFormalPassenger; portanto nao descarte toda unidade com plano.
            SectorObjective candidateAssigned = plan != null ? ResolveAssignedObjective(candidate, plan) : null;
            if (assignedSector != null && plan != null)
            {
                // Assigned shuttle: only candidates heading to the same sector will accept.
                if (candidateAssigned == null || candidateAssigned.Sector != assignedSector.Sector) continue;
            }

            if (IsPassengerAlreadyAtCaptureObjective(candidate, snapshot.AITeam)) continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition; candidateCell.z = 0;
            bool hasResolvedObjective = TryResolveCourierPassengerTarget(
                candidate,
                plan,
                snapshot,
                Vector3Int.zero,
                candidateCell,
                out Vector3Int objectiveCell);
            if (!hasResolvedObjective)
                objectiveCell = Vector3Int.zero;

            // "Consegue ir a pe" só vale quando o prédio continua disponível
            // para ESTE passageiro. Se outro aliado já ocupa/captura o alvo,
            // procura o próximo capturável livre antes de medir os 6 MP.
            if (hasResolvedObjective
                && IsPickupObjectiveClaimedByAlly(
                    candidate, objectiveCell, snapshot.AISlotIndex))
            {
                Vector3Int claimedObjective = objectiveCell;
                if (TryFindAlternatePickupObjective(
                        candidate, snapshot, candidateCell,
                        out Vector3Int alternateObjective))
                {
                    objectiveCell = alternateObjective;
                    Debug.Log($"{TL("Transporte")} pickup global #{candidate.InstanceId}: " +
                              $"objetivo {claimedObjective} ja ocupado; " +
                              $"reavalia contra livre {objectiveCell}.");
                }
                else
                {
                    hasResolvedObjective = false;
                    objectiveCell = Vector3Int.zero;
                    Debug.Log($"{TL("Transporte")} pickup global #{candidate.InstanceId}: " +
                              $"objetivo {claimedObjective} ja ocupado e sem alternativa livre; " +
                              $"nao conta como 'consegue ir a pe'.");
                }
            }

            float objectiveDist = hasResolvedObjective
                ? SectorManager.HexDistance(candidateCell, objectiveCell)
                : 0f;
            // APC segue a mesma fronteira usada pelo matching formal: se o
            // passageiro não alcança o objetivo dentro de 6 MP, precisa da
            // carona. Não use o threshold dinâmico do planner aqui, pois ele
            // pode crescer acima de 6 e fazer um rogue distante desaparecer
            // da lista de pickup.
            bool groundTransporter = transporter.GetDomain() != Domain.Air;
            int candidateThreshold = groundTransporter
                ? ResolvePassengerWalkWithoutTransportBudget(candidate)
                : GetEffectiveTransportThresholdForSlot(
                    PlayerSlotId.FromIndex(snapshot.AISlotIndex));
            if (!groundTransporter)
            {
                int candidateMP = candidate.MaxMovementPoints;
                if (candidateMP < 3)
                    candidateThreshold += (3 - candidateMP) * 2;
                candidateThreshold =
                    Mathf.Max(2, candidateThreshold - thresholdReduction);
            }
            int walkThreshold = candidateThreshold;
            if (hasResolvedObjective)
            {
                int objectiveTerrainCost = TerrainCostToCell(
                    candidate, candidateCell, objectiveCell, walkThreshold);
                if (objectiveTerrainCost <= walkThreshold)
                {
                    if (assignedSector == null)
                        Debug.Log($"{TL("Transporte")} pickup global descarta " +
                                  $"#{candidate.InstanceId}: chega ao objetivo " +
                                  $"{objectiveCell} a pe cost={objectiveTerrainCost}" +
                                  $"<={walkThreshold}.");
                    continue;
                }
            }

            float transportDist = SectorManager.HexDistance(transporterCell, candidateCell);
            int rolePriority = candidateData.roles != null && candidateData.roles.Count > 0
                ? (int)candidateData.roles[0] : 99;

            // Sem objetivo pronto continua sendo passageiro potencial: o transporte
            // volta para perto dele e o destino pode ser definido após o embarque.
            // Não o descarte só porque o planner o classificou como rogue/target=null.
            float score = (hasResolvedObjective ? objectiveDist * 100f : 250f)
                - transportDist * 50f
                - rolePriority * 10f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                bestCandidateCell = candidateCell;
                if (assignedSector == null)
                    Debug.Log($"{TL("Transporte")} pickup global aceita " +
                              $"#{candidate.InstanceId}@{candidateCell}: " +
                              $"target={(hasResolvedObjective ? objectiveCell.ToString() : "pendente")} " +
                              $"walkThreshold={walkThreshold}.");
            }
        }

        return best;
    }

    private static bool IsPickupObjectiveClaimedByAlly(
        UnitManager passenger,
        Vector3Int objectiveCell,
        int aiSlotIndex)
    {
        objectiveCell.z = 0;
        foreach (UnitManager ally in UnitManager.AllActive)
        {
            if (ally == null || ally == passenger || ally.IsDead || ally.IsEmbarked)
                continue;
            if (ally.SlotIndex != aiSlotIndex)
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            if (allyCell == objectiveCell)
                return true;
        }

        return false;
    }

    private bool TryFindAlternatePickupObjective(
        UnitManager passenger,
        AIWorldSnapshot snapshot,
        Vector3Int passengerCell,
        out Vector3Int bestCell)
    {
        bestCell = Vector3Int.zero;
        if (passenger == null || snapshot == null || matchController == null)
            return false;

        float bestDistance = float.MaxValue;
        foreach (ConstructionManager building in ConstructionManager.AllActive)
        {
            if (building == null || !building.IsCapturable)
                continue;
            if (!building.TryResolveConstructionData(out ConstructionData data)
                || data == null)
                continue;
            if (!matchController.CanCaptureConstruction(
                    PlayerSlotId.FromIndex(passenger.SlotIndex), data, out _))
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            if (IsPickupObjectiveClaimedByAlly(
                    passenger, cell, snapshot.AISlotIndex))
                continue;

            float distance = SectorManager.HexDistance(passengerCell, cell);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestCell = cell;
        }

        return bestDistance < float.MaxValue;
    }

    private static bool IsAlreadyFormalPassenger(UnitManager candidate, UnitManager thisTransporter, TeamObjectivePlan plan)
    {
        if (plan == null) return false;
        foreach (SectorObjective obj in plan.Objectives)
        {
            bool candidateIsPassenger = false;
            bool otherTransporterAssigned = false;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (IsGroundTransportPassengerSlot(obj, slot, candidate.TeamId)
                    && slot.AssignedUnitId == candidate.InstanceId)
                    candidateIsPassenger = true;
                if (slot.Role == UnitRole.Transportador && slot.Filled && slot.AssignedUnitId != thisTransporter.InstanceId)
                    otherTransporterAssigned = true;
            }
            if (candidateIsPassenger && otherTransporterAssigned) return true;
        }
        return false;
    }

    private static int FindFittingSlotIndex(UnitManager transporter, UnitData transporterData, UnitManager candidate, UnitData candidateData)
    {
        if (transporterData.transportSlots == null) return -1;
        for (int i = 0; i < transporterData.transportSlots.Count; i++)
        {
            UnitTransportSlotRule slot = transporterData.transportSlots[i];
            if (slot == null) continue;
            if (!PodeEmbarcarSensor.CanUseSlot(candidate, candidateData, slot, out _)) continue;
            int occupancy = transporter.GetOccupiedTransportSeatCountForSlot(i);
            if (occupancy >= Mathf.Max(1, slot.capacity)) continue;
            return i;
        }
        return -1;
    }

    // -------------------------------------------------------------------------
    // Shuttle movement
    // -------------------------------------------------------------------------

    // pickupRange: max hexes the passenger can walk to reach the APC for boarding
    private const int ShuttlePickupRange = 2;

    private Vector3Int FindTransportShuttleMove(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int candidateCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam,
        Vector3Int objectiveCell = default,
        HashSet<Vector3Int> passengerReachable = null,
        TeamObjectivePlan plan = null,
        SectorObjective transportObjective = null,
        Dictionary<Vector3Int, List<Vector3Int>> movementPaths = null)
    {
        bool fromIsProductionBldg = IsTeamProductionBuilding(fromCell, aiTeam);
        bool fromCanReceivePassengers = CanUseTransporterPickupCell(unit, aiTeam, fromCell);
        bool hasObjective = objectiveCell != default && objectiveCell != Vector3Int.zero;
        bool preferGroupPickup = unit != null && unit.GetDomain() == Domain.Air;
        int waitingRange = preferGroupPickup ? 1 : ShuttlePickupRange;
        const float eps = 0.1f;

        // Pickup confirmado tem precedencia sobre "adiantar a viagem": se o
        // transportador ja esta numa parada valida e o passageiro consegue
        // chegar ao seu hex/adjacencia dentro do envelope de ate 2h, fica
        // esperando. Mover mais perto do objetivo neste ponto quebra o encontro
        // e pode deixar o passageiro para tras.
        if (fromCanReceivePassengers
            && !fromIsProductionBldg
            && IsPassengerInPickupRange(
                fromCell,
                candidateCell,
                waitingRange,
                passengerReachable))
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} pickup confirmado: " +
                      $"aguarda em {fromCell} passageiro@{candidateCell} " +
                      $"envelope={waitingRange}h.");
            return fromCell;
        }

        // When we know the destination, park on the path: find the reachable cell
        // within pickup range of the passenger that is closest to the objective.
        // When passengerReachable is provided (terrain-aware), use it instead of hex distance.
        // Otherwise try preferred range (2h) first; expand to extended range (3h) if nothing found.
        // "Stay" counts as a candidate if already in range and not blocking production.
        if (hasObjective)
        {
            for (int pickupRange = waitingRange; pickupRange <= waitingRange; pickupRange++)
            {
                Vector3Int best = fromCell;
                float bestScore = float.MinValue;
                float bestDistToObj = float.MaxValue;
                int bestSupport = -1;
                float bestBalancePenalty = float.MaxValue;
                bool bestIsProductionBldg = true;
                bool bestIsConstruction = true;
                float bestThreat = float.MaxValue;
                bool found = false;

                if (fromCanReceivePassengers
                    && IsPassengerInPickupRange(fromCell, candidateCell, pickupRange, passengerReachable)
                    && !fromIsProductionBldg)
                {
                    best = fromCell;
                    bestDistToObj = SectorManager.HexDistance(fromCell, objectiveCell);
                    bestIsProductionBldg = false;
                    bestIsConstruction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell) != null;
                    bestThreat = CalculateThreatLevel(fromCell, aiTeam);
                    (bestSupport, bestBalancePenalty) = GetAirPickupSupportStats(fromCell, aiTeam, plan, transportObjective);
                    bestSupport = Mathf.Max(1, bestSupport);
                    float bestTravel = SectorManager.HexDistance(fromCell, fromCell);
                    bestScore = ScoreAirPickupCell(bestDistToObj, bestTravel, bestSupport, bestBalancePenalty,
                        bestIsProductionBldg, bestIsConstruction, bestThreat, preferGroupPickup);
                    found = true;
                }

                foreach (Vector3Int cell in paths.Keys)
                {
                    if (cell == fromCell) continue;
                    if (occupied.Contains(cell)) continue;
                    if (IsNonTeamConstruction(cell, aiTeam)) continue;
                    if (!CanUseTransporterPickupCell(unit, aiTeam, cell)) continue;
                    if (!IsPassengerInPickupRange(cell, candidateCell, pickupRange, passengerReachable)) continue;

                    float distToObj = SectorManager.HexDistance(cell, objectiveCell);
                    float travelDist = SectorManager.HexDistance(fromCell, cell);
                    bool isProductionBldg = IsTeamProductionBuilding(cell, aiTeam);
                    bool isConstruction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell) != null;
                    float threat = CalculateThreatLevel(cell, aiTeam);
                    (int support, float balancePenalty) = GetAirPickupSupportStats(cell, aiTeam, plan, transportObjective);
                    support = Mathf.Max(1, support);
                    float score = ScoreAirPickupCell(distToObj, travelDist, support, balancePenalty,
                        isProductionBldg, isConstruction, threat, preferGroupPickup);

                    bool isBetter = !found
                        || score > bestScore + eps
                        || (score > bestScore - eps && distToObj < bestDistToObj - eps)
                        || (score > bestScore - eps && distToObj < bestDistToObj + eps && support > bestSupport)
                        || (score > bestScore - eps && distToObj < bestDistToObj + eps && support == bestSupport && balancePenalty < bestBalancePenalty - 0.001f)
                        || (score > bestScore - eps && distToObj < bestDistToObj + eps && support == bestSupport && Mathf.Abs(balancePenalty - bestBalancePenalty) < 0.001f && !isProductionBldg && bestIsProductionBldg)
                        || (score > bestScore - eps && distToObj < bestDistToObj + eps && support == bestSupport && Mathf.Abs(balancePenalty - bestBalancePenalty) < 0.001f && isProductionBldg == bestIsProductionBldg && !isConstruction && bestIsConstruction)
                        || (score > bestScore - eps && distToObj < bestDistToObj + eps && support == bestSupport && Mathf.Abs(balancePenalty - bestBalancePenalty) < 0.001f && isProductionBldg == bestIsProductionBldg && isConstruction == bestIsConstruction && threat < bestThreat - 0.001f);

                    if (isBetter)
                    {
                        best = cell;
                        bestScore = score;
                        bestDistToObj = distToObj;
                        bestSupport = support;
                        bestBalancePenalty = balancePenalty;
                        bestIsProductionBldg = isProductionBldg;
                        bestIsConstruction = isConstruction;
                        bestThreat = threat;
                        found = true;
                    }
                }

                // When using terrain-aware check the range loop is irrelevant — one pass is enough.
                if (found || passengerReachable != null)
                {
                    if (found)
                    {
                        if (unit != null && unit.GetDomain() == Domain.Air)
                        {
                            float travel = SectorManager.HexDistance(fromCell, best);
                            Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} pickup-score cell={best} objDist={bestDistToObj:F0} travel={travel:F0} support={bestSupport} score={bestScore:F0}");
                        }
                        return best;
                    }
                    break;
                }
            }
            // No cell within extended pickup range reachable — fall through to rendezvous move
        }

        // Fallback: original adjacent-first behavior
        if (fromCanReceivePassengers
            && IsPassengerInPickupRange(fromCell, candidateCell, 1f, passengerReachable)
            && !fromIsProductionBldg)
            return fromCell;

        Vector3Int bestAdj = fromCell;
        float bestAdjThreat = float.MaxValue;
        bool bestAdjIsProductionBldg = fromIsProductionBldg;
        bool bestAdjIsConstruction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell) != null;
        bool foundAdj = false;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (occupied.Contains(cell)) continue;
            if (!IsPassengerInPickupRange(cell, candidateCell, 1f, passengerReachable)) continue;
            if (IsNonTeamConstruction(cell, aiTeam)) continue;
            if (!CanUseTransporterPickupCell(unit, aiTeam, cell)) continue;

            bool cellIsProductionBldg = IsTeamProductionBuilding(cell, aiTeam);
            bool cellIsConstruction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell) != null;
            float threat = CalculateThreatLevel(cell, aiTeam);

            bool isBetter = !foundAdj
                || (!cellIsProductionBldg && bestAdjIsProductionBldg)
                || (cellIsProductionBldg == bestAdjIsProductionBldg && !cellIsConstruction && bestAdjIsConstruction)
                || (cellIsProductionBldg == bestAdjIsProductionBldg && cellIsConstruction == bestAdjIsConstruction && threat < bestAdjThreat - 0.001f);

            if (isBetter)
            {
                bestAdj = cell;
                bestAdjThreat = threat;
                bestAdjIsProductionBldg = cellIsProductionBldg;
                bestAdjIsConstruction = cellIsConstruction;
                foundAdj = true;
            }
        }

        if (foundAdj) return bestAdj;
        if (fromCanReceivePassengers && IsPassengerInPickupRange(fromCell, candidateCell, 1f, passengerReachable)) return fromCell;

        // When we know the objective, head toward the rendezvous: the cell ~2h from the
        // capturer along the capturer→objective direction. This keeps the APC on the
        // delivery route so both sides converge; falling back to the capturer directly
        // forces a needless detour away from the objective.
        if (hasObjective)
        {
            if (unit != null && unit.GetDomain() == Domain.Air)
            {
                Dictionary<Vector3Int, List<Vector3Int>> airMovePaths = movementPaths ?? paths;
                Vector3Int pickupReturnTarget = ResolveAirPickupReturnTarget(unit, candidateCell, passengerReachable, aiTeam, airMovePaths);
                Vector3Int airPickupMove = FindAirTransportMove(fromCell, pickupReturnTarget, airMovePaths, occupied, aiTeam);
                Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} pickup sem intersecao neste turno - retorna pickup/base alvo={pickupReturnTarget} passageiro={candidateCell} objetivo={objectiveCell} via {airPickupMove}");
                return airPickupMove;
            }

            float cToObj = SectorManager.HexDistance(candidateCell, objectiveCell);
            Vector3Int rendezvous = cToObj > ShuttlePickupRange + 0.5f
                ? new Vector3Int(
                      Mathf.RoundToInt(Mathf.Lerp(candidateCell.x, objectiveCell.x, ShuttlePickupRange / cToObj)),
                      Mathf.RoundToInt(Mathf.Lerp(candidateCell.y, objectiveCell.y, ShuttlePickupRange / cToObj)),
                      0)
                : candidateCell;
            return FindTransportMove(unit, fromCell, rendezvous, paths, occupied, aiTeam);
        }

        return FindTransportMove(unit, fromCell, candidateCell, paths, occupied, aiTeam);
    }

    private Vector3Int ResolveAirPickupReturnTarget(
        UnitManager transporter,
        Vector3Int candidateCell,
        HashSet<Vector3Int> passengerReachable,
        TeamId aiTeam,
        Dictionary<Vector3Int, List<Vector3Int>> transportPaths)
    {
        candidateCell.z = 0;
        Vector3Int homeTarget = FindTransportWaitTarget(aiTeam, candidateCell);

        Vector3Int best = candidateCell;
        float bestHomeDist = SectorManager.HexDistance(candidateCell, homeTarget);
        float bestCandidateDist = 0f;
        float bestThreat = CalculateThreatLevel(candidateCell, aiTeam);
        bool bestIsProduction = IsTeamProductionBuilding(candidateCell, aiTeam);
        bool candidateReachableByTransporter =
            candidateCell == transporter.CurrentCellPosition
            || (transportPaths != null && transportPaths.ContainsKey(candidateCell));
        bool found = candidateReachableByTransporter
            && CanUseTransporterPickupCell(transporter, aiTeam, candidateCell)
            && !IsNonTeamConstruction(candidateCell, aiTeam);
        const float eps = 0.01f;

        if (transportPaths != null && transportPaths.Count > 0)
        {
            foreach (Vector3Int rawCell in transportPaths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (IsNonTeamConstruction(cell, aiTeam)) continue;
                if (!CanUseTransporterPickupCell(transporter, aiTeam, cell)) continue;

                bool passengerHasTerrainAwareReach =
                    passengerReachable != null && passengerReachable.Count > 0;
                if (passengerHasTerrainAwareReach
                    && !CanPassengerReachEmbarkStopForTransporterCell(cell, passengerReachable))
                    continue;

                float homeDist = SectorManager.HexDistance(cell, homeTarget);
                float candidateDist = SectorManager.HexDistance(cell, candidateCell);
                float threat = CalculateThreatLevel(cell, aiTeam);
                bool isProduction = IsTeamProductionBuilding(cell, aiTeam);

                bool isBetter = !found
                    || candidateDist < bestCandidateDist - eps
                    || (candidateDist < bestCandidateDist + eps && homeDist < bestHomeDist - eps)
                    || (candidateDist < bestCandidateDist + eps && homeDist < bestHomeDist + eps && isProduction && !bestIsProduction)
                    || (candidateDist < bestCandidateDist + eps && homeDist < bestHomeDist + eps && isProduction == bestIsProduction && threat < bestThreat - eps);

                if (!isBetter)
                    continue;

                best = cell;
                bestHomeDist = homeDist;
                bestCandidateDist = candidateDist;
                bestThreat = threat;
                bestIsProduction = isProduction;
                found = true;
            }
        }

        if (passengerReachable != null)
        {
            foreach (Vector3Int rawCell in passengerReachable)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (IsNonTeamConstruction(cell, aiTeam)) continue;
                if (!CanUseTransporterPickupCell(transporter, aiTeam, cell)) continue;

                float homeDist = SectorManager.HexDistance(cell, homeTarget);
                float candidateDist = SectorManager.HexDistance(cell, candidateCell);
                float threat = CalculateThreatLevel(cell, aiTeam);
                bool isProduction = IsTeamProductionBuilding(cell, aiTeam);

                bool isBetter = !found
                    ||
                    homeDist < bestHomeDist - eps
                    || (homeDist < bestHomeDist + eps && isProduction && !bestIsProduction)
                    || (homeDist < bestHomeDist + eps && isProduction == bestIsProduction && candidateDist < bestCandidateDist - eps)
                    || (homeDist < bestHomeDist + eps && isProduction == bestIsProduction && candidateDist < bestCandidateDist + eps && threat < bestThreat - eps);

                if (!isBetter)
                    continue;

                best = cell;
                bestHomeDist = homeDist;
                bestCandidateDist = candidateDist;
                bestThreat = threat;
                bestIsProduction = isProduction;
                found = true;
            }
        }

        // Nunca transforme uma célula incompatível com UnitData.Transport.Allow
        // Embark At em "waiting zone". Se não houver parada de pickup válida
        // alcançável neste turno, mantenha a posição; o chamador poderá tentar
        // novamente com um novo envelope no turno seguinte.
        if (!found)
        {
            Debug.Log($"{TL("Transporte")} {transporter.InstanceId} sem waiting zone compativel " +
                      $"com Allow Embark At para passageiro@{candidateCell}; mantem {transporter.CurrentCellPosition}.");
            Vector3Int current = transporter.CurrentCellPosition;
            current.z = 0;
            return current;
        }

        return best;
    }

    private static bool CanPassengerReachEmbarkStopForTransporterCell(
        Vector3Int transporterCell,
        HashSet<Vector3Int> passengerReachable)
    {
        if (passengerReachable == null || passengerReachable.Count == 0)
            return false;

        transporterCell.z = 0;
        foreach (Vector3Int rawStop in passengerReachable)
        {
            Vector3Int stop = rawStop;
            stop.z = 0;
            if (SectorManager.HexDistance(stop, transporterCell) <= 1.5f)
                return true;
        }

        return false;
    }

    private bool CanUseTransporterPickupCell(UnitManager transporter, TeamId aiTeam, Vector3Int cell)
    {
        if (transporter == null)
            return true;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
            return true;

        cell.z = 0;
        if (transporter.GetDomain() == Domain.Air && HasBlockingGroundUnitAtCell(cell, aiTeam))
            return false;

        return PodeEmbarcarSensor.IsTransporterCellValidForEmbark(
            boardTilemap, terrainDatabase, transporterData, cell);
    }

    private float ScoreAirPickupCell(
        float distToObjective,
        float travelDist,
        int supportCount,
        float balancePenalty,
        bool isProductionBuilding,
        bool isConstruction,
        float threat,
        bool preferGroupPickup)
    {
        float score = preferGroupPickup
            ? -distToObjective * 22f - travelDist * 70f + supportCount * 420f - balancePenalty * 120f - threat * 5f
            : -distToObjective * 80f - travelDist * 55f + supportCount * 90f - threat * 5f;
        if (isProductionBuilding) score -= preferGroupPickup ? 320f : 60f;
        if (isConstruction) score -= 15f;
        return score;
    }

    private (int count, float balancePenalty) GetAirPickupSupportStats(
        Vector3Int cell,
        TeamId aiTeam,
        TeamObjectivePlan plan,
        SectorObjective transportObjective)
    {
        if (plan == null || transportObjective == null)
            return (0, 0f);

        int count = 0;
        float balancePenalty = 0f;
        cell.z = 0;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate.SlotIndex != ResolveAISlotKey(aiTeam)) continue;
            if (candidate.IsDead || candidate.IsEmbarked || candidate.HasActed) continue;
            if (!candidate.TryGetUnitData(out UnitData data) || data?.roles == null) continue;
            if (!data.roles.Contains(UnitRole.Capturador)) continue;

            SectorObjective assigned = ResolveAssignedObjective(candidate, plan);
            if (assigned == null)
                continue;
            if (assigned.Sector != transportObjective.Sector
                && !AreEmbarkSectorsCompatible(assigned.Sector, transportObjective.Sector))
                continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition;
            candidateCell.z = 0;
            float pickupDist = SectorManager.HexDistance(candidateCell, cell);
            if (pickupDist < 0.5f || pickupDist > ShuttlePickupRange + 0.5f)
                continue;

            HashSet<Vector3Int> reachable = BuildPassengerReachableSet(candidate);
            if (reachable != null && reachable.Contains(cell))
            {
                count++;
                balancePenalty += Mathf.Abs(pickupDist - 1.5f);
            }
        }

        return (count, balancePenalty);
    }

    // Returns true if the transporter cell is within the passenger's reach.
    // When passengerReachable is provided (terrain-aware BFS), uses that set.
    // Falls back to plain hex distance when null (ground APCs, no passenger data available).
    private static bool IsPassengerInPickupRange(
        Vector3Int cell, Vector3Int candidateCell, float maxHexDist,
        HashSet<Vector3Int> passengerReachable)
    {
        Vector3Int c = cell; c.z = 0;
        Vector3Int passengerCell = candidateCell; passengerCell.z = 0;
        if (SectorManager.HexDistance(c, passengerCell) > maxHexDist + 0.01f)
            return false;
        if (passengerReachable != null)
            return CanPassengerReachEmbarkStopForTransporterCell(
                c, passengerReachable);
        return true;
    }

    private bool IsTeamProductionBuilding(Vector3Int cell, TeamId aiTeam)
    {
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return bldg != null && bldg.SlotIndex == ResolveAISlotKey(aiTeam) && bldg.CanProduceUnits;
    }

    // Returns the nearest friendly factory or HQ to wait at between deliveries.
    // Returns the best enemy to attack from attackCell (moving from fromCell), or null if none qualify.
    private UnitManager TryFindAttackFromCell(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int fromCell, Vector3Int attackCell)
    {
        SensorMovementMode mode = attackCell != fromCell ? SensorMovementMode.MoveuAndando : SensorMovementMode.MoveuParado;
        var targets = new List<PodeMirarTargetOption>();
        WeaponPriorityData wpData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        if (!PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase, mode, targets, weaponPriorityData: wpData, fromCell: attackCell))
            return null;

        UnitManager best = null;
        float bestScore = float.MinValue;
        foreach (PodeMirarTargetOption opt in targets)
        {
            if (opt?.targetUnit == null || opt.targetUnit.SlotIndex == snapshot.AISlotIndex || opt.targetUnit.IsDead) continue;
            if (!PassesAttackDecision(unit, opt.targetUnit, attackCell, false, out _)) continue;
            float score = (20f - opt.targetUnit.CurrentHP) * 100f - opt.targetUnit.InstanceId * 0.001f;
            if (score > bestScore) { bestScore = score; best = opt.targetUnit; }
        }
        return best;
    }

    private static Vector3Int FindTransportWaitTarget(TeamId aiTeam, Vector3Int fromCell)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;

        foreach (ConstructionManager bldg in ConstructionManager.AllActive)
        {
            if (bldg == null || bldg.SlotIndex != ResolveAISlotKey(aiTeam)) continue;
            if (!bldg.CanProduceUnits && !bldg.IsPlayerHeadQuarter) continue;
            Vector3Int bc = bldg.CurrentCellPosition; bc.z = 0;
            float dist = SectorManager.HexDistance(fromCell, bc);
            if (dist < bestDist) { bestDist = dist; best = bldg; }
        }

        if (best != null) { Vector3Int bc = best.CurrentCellPosition; bc.z = 0; return bc; }
        return fromCell;
    }

    // -------------------------------------------------------------------------
    // Opportunistic attack (shuttle, empty) — max 1h deviation from pickup route
    // -------------------------------------------------------------------------

    private static BazookaTargetPriority ResolveTransportTargetPreference(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return BazookaTargetPriority.Tertiary;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return BazookaTargetPriority.Tertiary;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return BazookaTargetPriority.Tertiary;

        return attackerData.ResolveAiTargetPriorityForTargetClass(targetData.unitClass);
    }
    private bool TryFindTransportBreakerAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int candidateCell,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        bool preferNoMove = false,
        TeamObjectivePlan plan = null)
    {
        bestCell = fromCell;
        bestTarget = null;

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (enemies == null || enemies.Count == 0) return false;

        float fromDistToCandidate = SectorManager.HexDistance(fromCell, candidateCell);
        float bestScore = float.MinValue;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied.Contains(cell)) continue;
            if (!preferNoMove && SectorManager.HexDistance(cell, candidateCell) > fromDistToCandidate + 1f) continue;

            // Don't park on a capturable building that an assigned capturer can reach this turn.
            if (cell != fromCell && plan != null)
            {
                ConstructionManager captureAtCell = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
                if (captureAtCell != null && captureAtCell.IsCapturable
                    && TryFindAssignedCapturerForCaptureTarget(unit, plan, captureAtCell, snapshot.AITeam, cell, out _))
                    continue;
            }

            foreach (UnitManager enemy in enemies)
            {
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, cell, false, out _)) continue;
                if (ResolveTransportTargetPreference(unit, enemy) != BazookaTargetPriority.Primary) continue;

                float dpq = GetTerrainDpqPontos(cell);
                float score = preferNoMove
                    ? dpq * 2000f + (20f - enemy.CurrentHP) * 100f - SectorManager.HexDistance(cell, candidateCell) * 10f
                    : (20f - enemy.CurrentHP) * 100f - SectorManager.HexDistance(cell, candidateCell) * 50f + dpq * 200f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = enemy;
                }
            }
        }

        return bestTarget != null;
    }
}
