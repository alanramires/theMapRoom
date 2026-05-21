using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int AirDropOffRange = 2;

    // -------------------------------------------------------------------------
    // Air transport entry — called from TryDecideTransportadorAction when
    // the transporter's domain is Domain.Air (e.g. Chinook helicopter).
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideAirTransportAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        // Grounded helicopters are in Domain.Land, so CalcularCaminhosValidos would apply
        // terrain costs (forest=2, mountain=∞) instead of uniform air costs (1 per hex).
        // Temporarily switch to air mode here — the replay engine does the same thing via
        // TryPrepareTemporaryTakeoffStateForSelection when the cursor selects the unit.
        bool wasGrounded = unit.IsAircraftGrounded;
        if (wasGrounded)
            unit.SetAircraftGrounded(false);

        try
        {
            bool hasCargo = HasTransportCargo(unit);
            SectorObjective assigned = plan != null ? ResolveAssignedTransportObjective(unit, plan) : null;

            if (assigned != null)
                return DecideAssignedAirTransportAction(unit, snapshot, plan, assigned, hasCargo);
            if (hasCargo)
                return DecideAirCourierAction(unit, snapshot);

            return DecideAirShuttleAction(unit, snapshot, plan);
        }
        finally
        {
            if (wasGrounded)
                unit.SetAircraftGrounded(true);
        }
    }

    // -------------------------------------------------------------------------
    // Air Courier — helicopter with passengers, delivering to objective.
    // Differences from APC courier:
    //   • Uses AirDropOffRange (tighter — helicopter flies precisely to target).
    //   • Does NOT redirect away from the target building; helicopters can
    //     hover directly on construction cells to disembark passengers there.
    //   • No conservative FireSupport tow logic.
    // -------------------------------------------------------------------------

    private PlayerAction DecideAirCourierAction(UnitManager unit, AIWorldSnapshot snapshot,
        Vector3Int assignedSectorTarget = default)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);

        List<UnitManager> passengers = CollectPassengers(unit);
        if (passengers.Count == 0)
        {
            Debug.LogWarning($"[AI] {TL("Transporte")} heli {unit.InstanceId} courier: cargo inconsistente, reverte para shuttle");
            return DecideAirShuttleAction(unit, snapshot, plan);
        }

        UnitManager primaryPassenger = ResolvePrimaryPassenger(passengers, plan);
        bool targetFound = TryResolveCourierPassengerTarget(primaryPassenger, plan, snapshot,
            assignedSectorTarget, fromCell, out Vector3Int primaryTarget);
        if (!targetFound) primaryTarget = fromCell;

        Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} courier — passageiro #{primaryPassenger.InstanceId} alvo={primaryTarget} dist={SectorManager.HexDistance(fromCell, primaryTarget):F0}h");

        int remainingMP = Mathf.Max(0, unit.RemainingMovementPoints);
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, remainingMP, terrainDatabase);
        HashSet<Vector3Int> occupied = BuildAirOccupied(unit);

        Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} courier — MP={remainingMP} grounded={unit.IsAircraftGrounded} domain={unit.GetDomain()} paths={paths?.Count ?? 0} occupied={occupied.Count}");

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        Vector3Int moveTarget = FindAirTransportMove(fromCell, primaryTarget, paths, occupied, snapshot.AITeam);

        // Helicopters can hover on construction cells — do NOT redirect away from the
        // target building. The passenger is disembarked directly onto it.

        float moveImprovement = CalculateRouteDistanceOrHex(unit, fromCell, primaryTarget)
                              - CalculateRouteDistanceOrHex(unit, moveTarget, primaryTarget);

        // Priority 1: move + disembark when moving gains ground AND the simulated
        // drop-off from the new position lands within AirDropOffRange.
        // When moveTarget is blocked by a ground unit (can't land there), scan all reachable
        // hexes by improvement order and pick the best landable hex that allows disembark.
        {
            var p1Candidates = new System.Collections.Generic.List<(Vector3Int cell, float improvement)>();
            foreach (Vector3Int candidate in paths.Keys)
            {
                if (candidate == fromCell || occupied.Contains(candidate)) continue;
                float imp = CalculateRouteDistanceOrHex(unit, fromCell, primaryTarget)
                          - CalculateRouteDistanceOrHex(unit, candidate, primaryTarget);
                if (imp >= 1f)
                    p1Candidates.Add((candidate, imp));
            }
            p1Candidates.Sort((a, b) => b.improvement.CompareTo(a.improvement));

            foreach (var (candidate, imp) in p1Candidates)
            {
                List<PodeDesembarcarOption> optsFromMove = SimulateDisembarkFromCell(unit, candidate);
                if (optsFromMove == null || optsFromMove.Count == 0) continue;

                List<PodeDesembarcarOption> selectedFromMove =
                    SelectBestDisembarkPerPassenger(optsFromMove, passengers, plan, snapshot);
                // Partial disembark: drop only passengers whose target is within AirDropOffRange.
                selectedFromMove = FilterDisembarkByTargetRange(selectedFromMove, plan, snapshot, AirDropOffRange);

                PodeDesembarcarOption primaryOpt = selectedFromMove.Count > 0
                    ? selectedFromMove.Find(o => o.passengerUnit == primaryPassenger) : null;
                if (primaryOpt == null) continue;

                Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                bool dcInRange   = SectorManager.HexDistance(dc, primaryTarget) <= AirDropOffRange;
                bool heliInRange = SectorManager.HexDistance(candidate, primaryTarget) <= AirDropOffRange;
                if (dcInRange || heliInRange)
                {
                    paths.TryGetValue(candidate, out List<Vector3Int> movePath);
                    int dropping = selectedFromMove.Count, keeping = passengers.Count - dropping;
                    Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} courier — move+desembarca via {candidate} dc={dc} dcDist={SectorManager.HexDistance(dc, primaryTarget):F0}h → {primaryTarget} (desembarca={dropping} fica={keeping})");
                    return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selectedFromMove, candidate, movePath);
                }
            }
        }

        // Priority 2: disembark from current position.
        bool isStuck = moveTarget == fromCell;
        var disembarkOptions = new List<PodeDesembarcarOption>();
        bool canDisembark = PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, disembarkOptions);
        if (canDisembark && disembarkOptions.Count > 0 && (moveImprovement <= 1f || isStuck))
        {
            List<PodeDesembarcarOption> selected =
                SelectBestDisembarkPerPassenger(disembarkOptions, passengers, plan, snapshot);
            // Partial disembark — when stuck, release all; otherwise only passengers in range of their target.
            selected = FilterDisembarkByTargetRange(selected, plan, snapshot, AirDropOffRange, allowStuck: isStuck);
            if (selected.Count > 0)
            {
                PodeDesembarcarOption primaryOption = selected.Find(o => o.passengerUnit == primaryPassenger);
                if (primaryOption != null)
                {
                    Vector3Int dc = primaryOption.disembarkCell; dc.z = 0;
                    bool inRange = isStuck
                        || SectorManager.HexDistance(dc, primaryTarget) <= AirDropOffRange
                        || SectorManager.HexDistance(fromCell, primaryTarget) <= AirDropOffRange;
                    if (inRange)
                    {
                        int dropping = selected.Count, keeping = passengers.Count - dropping;
                        string reason = isStuck ? "bloqueado, libera carga" : $"desembarca para {primaryTarget}";
                        Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} courier — {reason} (desembarca={dropping} fica={keeping})");
                        return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                    }
                }
            }
        }

        // Priority 3: move toward target.
        Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} courier — move para {moveTarget} alvo={primaryTarget} dist={SectorManager.HexDistance(moveTarget, primaryTarget):F0}h passageiro=#{primaryPassenger.InstanceId}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    // -------------------------------------------------------------------------
    // Air Shuttle — empty helicopter seeking pickup candidates.
    // First pass prefers candidates whose assigned sectors favour air transport.
    // -------------------------------------------------------------------------

    private PlayerAction DecideAirShuttleAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildAirOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // Pass 1: candidates heading to Air-preferred sectors.
        UnitManager bestCandidate = FindBestAirShuttleCandidate(
            unit, snapshot, plan, fromCell, out Vector3Int candidateCell, preferAirSectors: true);

        // Pass 2: any eligible candidate.
        if (bestCandidate == null)
            bestCandidate = FindBestAirShuttleCandidate(
                unit, snapshot, plan, fromCell, out candidateCell, preferAirSectors: false);

        // Pass 3: relax the distance threshold.
        if (bestCandidate == null)
        {
            int relaxed = Mathf.Max(2, GetEffectiveTransportThreshold(snapshot.AITeam) / 2);
            bestCandidate = FindBestAirShuttleCandidate(
                unit, snapshot, plan, fromCell, out candidateCell,
                preferAirSectors: false, thresholdReduction: relaxed);
            if (bestCandidate != null)
                Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} shuttle — candidato relaxado {bestCandidate.InstanceId}@{candidateCell} (threshold -{relaxed})");
        }

        if (bestCandidate != null)
        {
            Vector3Int candidateObjective = ResolveUnitObjectiveCell(bestCandidate, plan, snapshot);
            SectorObjective candidateAssigned = plan != null ? ResolveAssignedObjective(bestCandidate, plan) : null;
            var embarkablePaths = FilterPathsToEmbarkableCells(paths, unit, snapshot.AITeam);
            HashSet<Vector3Int> passengerReachable = BuildPassengerReachableSet(bestCandidate);
            Vector3Int moveTarget = FindTransportShuttleMove(
                unit, fromCell, candidateCell, embarkablePaths, occupied, snapshot.AITeam,
                candidateObjective, passengerReachable, plan, candidateAssigned);
            Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} shuttle — candidato {bestCandidate.InstanceId}@{candidateCell} via {moveTarget}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
        }

        // No candidate: return to HQ to pick up the next wave of capturers rather than
        // releasing to HexEvaluator, which would leave the helicopter idle (it cannot
        // meaningfully capture or attack and HexEvaluator picks its current cell).
        Vector3Int hqTarget = FindTransportWaitTarget(snapshot.AITeam, fromCell);
        Vector3Int hqMove = hqTarget != fromCell
            ? FindAirTransportMove(fromCell, hqTarget, paths, occupied, snapshot.AITeam)
            : fromCell;
        Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} shuttle — sem candidato, retorna ao HQ {hqTarget} via {hqMove}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, hqMove, paths);
    }

    private UnitManager FindBestAirShuttleCandidate(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int transporterCell,
        out Vector3Int bestCandidateCell,
        bool preferAirSectors = false,
        int thresholdReduction = 0,
        SectorObjective assignedSector = null)
    {
        bestCandidateCell = transporterCell;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
            return null;

        UnitManager best = null;
        float bestScore = float.MinValue;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == transporter) continue;
            if (candidate.TeamId != snapshot.AITeam || candidate.IsDead || candidate.IsEmbarked || candidate.HasActed) continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData)) continue;
            if (FindFittingSlotIndex(transporter, transporterData, candidate, candidateData) < 0) continue;
            if (IsAlreadyFormalPassenger(candidate, transporter, plan)) continue;

            SectorObjective candidateAssigned = plan != null ? ResolveAssignedObjective(candidate, plan) : null;
            bool candidateIsPrimary = candidateData.roles != null && candidateData.roles.Count > 0
                && candidateData.roles[0] == UnitRole.Capturador;
            if (assignedSector == null)
            {
                if (candidateIsPrimary && candidateAssigned != null) continue;
            }
            else
            {
                if (candidateAssigned == null) continue;
                if (candidateAssigned.Sector != assignedSector.Sector
                    && !AreEmbarkSectorsCompatible(candidateAssigned.Sector, assignedSector.Sector))
                    continue;
            }

            Vector3Int objectiveCell = ResolveUnitObjectiveCell(candidate, plan, snapshot);
            if (objectiveCell == Vector3Int.zero) continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition; candidateCell.z = 0;
            float objectiveDist = SectorManager.HexDistance(candidateCell, objectiveCell);

            int candidateThreshold = GetEffectiveTransportThreshold(snapshot.AITeam);
            int candidateMP = candidate.MaxMovementPoints;
            if (candidateMP < 3) candidateThreshold += (3 - candidateMP) * 2;
            candidateThreshold = Mathf.Max(2, candidateThreshold - thresholdReduction);
            if (objectiveDist < candidateThreshold) continue;

            if (preferAirSectors && plan != null)
            {
                bool isAirPreferred = false;
                foreach (SectorObjective obj in plan.Objectives)
                {
                    bool candidateInSlot = false;
                    foreach (SlotNeed slot in obj.Slots)
                        if (slot.Filled && slot.AssignedUnitId == candidate.InstanceId) { candidateInSlot = true; break; }
                    if (!candidateInSlot) continue;
                    if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo si)
                        && si.GetTransportPreference(snapshot.AITeam) == SectorManager.SectorInfo.TransportPreference.Air)
                        isAirPreferred = true;
                    break;
                }
                if (!isAirPreferred) continue;
            }

            float transportDist = SectorManager.HexDistance(transporterCell, candidateCell);
            int rolePriority = candidateData.roles != null && candidateData.roles.Count > 0
                ? (int)candidateData.roles[0] : 99;
            float score = objectiveDist * 100f - transportDist * 50f - rolePriority * 10f;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                bestCandidateCell = candidateCell;
            }
        }

        return best;
    }

    // -------------------------------------------------------------------------
    // Assigned Air Transport — helicopter with a formal transport slot in the plan.
    // -------------------------------------------------------------------------

    private PlayerAction DecideAssignedAirTransportAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        SectorObjective assigned,
        bool hasCargo)
    {
        if (hasCargo)
        {
            ConstructionManager assignedBldg = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            Vector3Int assignedTarget = assignedBldg != null
                ? (Vector3Int)(assignedBldg.CurrentCellPosition) : Vector3Int.zero;
            if (assignedTarget != Vector3Int.zero) assignedTarget.z = 0;
            return DecideAirCourierAction(unit, snapshot, assignedTarget);
        }

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildAirOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        UnitManager targetPassenger = ResolveAssignedPassengerUnit(assigned, snapshot.AITeam);
        if (targetPassenger != null)
        {
            ConstructionManager objCheck = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            if (objCheck != null)
            {
                Vector3Int objCheckCell = objCheck.CurrentCellPosition; objCheckCell.z = 0;
                Vector3Int passCell = targetPassenger.CurrentCellPosition; passCell.z = 0;
                int airThreshold = GetEffectiveTransportThreshold(snapshot.AITeam);
                int passTerrainCost = TerrainCostToCell(targetPassenger, passCell, objCheckCell, airThreshold);
                if (passTerrainCost < airThreshold)
                    targetPassenger = null;
            }
        }

        if (targetPassenger == null)
        {
            ConstructionManager sectorTarget = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            Vector3Int sectorCell = fromCell;
            if (sectorTarget != null) { sectorCell = sectorTarget.CurrentCellPosition; sectorCell.z = 0; }

            UnitManager nearbyCandidate = FindBestAirShuttleCandidate(
                unit, snapshot, plan, fromCell, out Vector3Int nearbyCell,
                preferAirSectors: false, assignedSector: assigned);
            if (nearbyCandidate != null)
            {
                Vector3Int objCell2 = ResolveUnitObjectiveCell(nearbyCandidate, plan, snapshot);
                if (objCell2 == Vector3Int.zero) objCell2 = sectorCell;
                var embarkablePaths2 = FilterPathsToEmbarkableCells(paths, unit, snapshot.AITeam);
                HashSet<Vector3Int> passengerReachable2 = BuildPassengerReachableSet(nearbyCandidate);
                Vector3Int shuttleMove = FindTransportShuttleMove(
                    unit, fromCell, nearbyCell, embarkablePaths2, occupied, snapshot.AITeam,
                    objCell2, passengerReachable2, plan, assigned);
                Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} assigned {assigned.Sector} — sem passageiro formal, aguarda candidato {nearbyCandidate.InstanceId}@{nearbyCell} via {shuttleMove}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, shuttleMove, paths);
            }

            Vector3Int sectorMove = FindAirTransportMove(fromCell, sectorCell, paths, occupied, snapshot.AITeam);
            Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} assigned {assigned.Sector} — sem passageiro, pressiona {sectorCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, sectorMove, paths);
        }

        Vector3Int passengerCell = targetPassenger.CurrentCellPosition; passengerCell.z = 0;
        Vector3Int objCell = ResolveUnitObjectiveCell(targetPassenger, plan, snapshot);
        ConstructionManager assignedPickupTarget = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
        if (assignedPickupTarget != null)
        {
            objCell = assignedPickupTarget.CurrentCellPosition;
            objCell.z = 0;
        }
        var embarkablePaths3 = FilterPathsToEmbarkableCells(paths, unit, snapshot.AITeam);
        HashSet<Vector3Int> passengerReachable3 = BuildPassengerReachableSet(targetPassenger);
        Vector3Int moveTarget = FindTransportShuttleMove(
            unit, fromCell, passengerCell, embarkablePaths3, occupied, snapshot.AITeam, objCell, passengerReachable3, plan, assigned);

        Debug.Log($"{TL("Transporte")} heli {unit.InstanceId} assigned {assigned.Sector} — pickup {targetPassenger.InstanceId}@{passengerCell} via {moveTarget}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    // -------------------------------------------------------------------------
    // Air-specific occupied set — only blocks cells taken by other air units.
    // Ground units are at a lower height level and do not prevent the helicopter
    // from landing on the same hex. BuildOccupied (used by ground units) adds ALL
    // unit positions and would incorrectly cut most of the helicopter's valid paths.
    // -------------------------------------------------------------------------

    private HashSet<Vector3Int> BuildAirOccupied(UnitManager excludeUnit)
    {
        var set = new HashSet<Vector3Int>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == excludeUnit || u.IsEmbarked || u.IsDead) continue;
            if (!u.TryGetUnitData(out UnitData d) || d.domain != Domain.Air) continue;
            Vector3Int p = u.CurrentCellPosition; p.z = 0;
            set.Add(p);
        }
        return set;
    }

    // -------------------------------------------------------------------------
    // Air-specific move selector — uses hex distance instead of CalculateMovementCostMap.
    // Helicopters have uniform movement cost (1 per hex regardless of terrain), so
    // hex distance to target IS the correct routing metric. CalculateMovementCostMap
    // starting from a building cell (capturable objective) fails for air units because
    // the BFS cannot expand from an impassable construction tile, returning an empty
    // cost map and causing FindTransportExplorationMove's pathSteps heuristic to pick
    // the wrong direction.
    // -------------------------------------------------------------------------

    private Vector3Int FindAirTransportMove(
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam)
    {
        Vector3Int bestCell = fromCell;
        float bestDist = SectorManager.HexDistance(fromCell, targetCell);
        bool bestIsNonTeamBldg = false;
        bool bestIsLandable = false;
        float bestThreat = float.MaxValue;
        const float eps = 0.01f;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (occupied.Contains(cell)) continue;

            float dist = SectorManager.HexDistance(cell, targetCell);
            bool isNonTeamBldg = IsNonTeamConstruction(cell, aiTeam);
            // Prefer hexes where the helicopter can actually land (no blocking ground unit).
            bool isLandable = !HasBlockingGroundUnitAtCell(cell, aiTeam);
            float threat = CalculateThreatLevel(cell, aiTeam);

            bool isBetter;
            if (dist < bestDist - eps)
                isBetter = true;
            else if (dist < bestDist + eps)
                isBetter = (!isNonTeamBldg && bestIsNonTeamBldg)
                           || (isNonTeamBldg == bestIsNonTeamBldg && isLandable && !bestIsLandable)
                           || (isNonTeamBldg == bestIsNonTeamBldg && isLandable == bestIsLandable && threat < bestThreat - eps);
            else
                isBetter = false;

            if (isBetter)
            {
                bestCell = cell;
                bestDist = dist;
                bestIsNonTeamBldg = isNonTeamBldg;
                bestIsLandable = isLandable;
                bestThreat = threat;
            }
        }

        return bestCell;
    }

    // Returns a copy of paths that only contains cells where the transporter can receive
    // passengers according to its allowedEmbarkWhenTransporterAt* asset rules.
    // The caller's fromCell is always included so the unit can stay in place if needed.
    // If no embarkable cell is reachable the original paths are returned unchanged so the
    // helicopter can still move toward the objective rather than being frozen.
    private Dictionary<Vector3Int, List<Vector3Int>> FilterPathsToEmbarkableCells(
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        UnitManager transporter,
        TeamId aiTeam)
    {
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
            return paths;
        bool hasAnyFilter = (transporterData.allowedEmbarkWhenTransporterAtTerrains != null && transporterData.allowedEmbarkWhenTransporterAtTerrains.Count > 0)
            || (transporterData.allowedEmbarkWhenTransporterAtTerrainStructures != null && transporterData.allowedEmbarkWhenTransporterAtTerrainStructures.Count > 0)
            || (transporterData.allowedEmbarkWhenTransporterAtConstructions != null && transporterData.allowedEmbarkWhenTransporterAtConstructions.Count > 0);

        var filtered = new System.Collections.Generic.Dictionary<Vector3Int, List<Vector3Int>>();
        foreach (var kv in paths)
        {
            Vector3Int c = kv.Key; c.z = 0;
            // Helicopter cannot land to receive passengers if a ground unit occupies the cell.
            if (HasBlockingGroundUnitAtCell(c, aiTeam)) continue;
            if (hasAnyFilter && !PodeEmbarcarSensor.IsTransporterCellValidForEmbark(boardTilemap, terrainDatabase, transporterData, c))
                continue;
            filtered[kv.Key] = kv.Value;
        }
        return filtered.Count > 0 ? filtered : paths;
    }

    // Computes the set of cells the passenger can reach within ShuttlePickupRange steps,
    // using terrain-aware BFS. Used so FindTransportShuttleMove picks a valid landing spot
    // even when the helicopter can fly over obstacles the passenger cannot cross.
    private HashSet<Vector3Int> BuildPassengerReachableSet(UnitManager passenger)
    {
        if (passenger == null) return null;
        int budget = Mathf.Min(Mathf.Max(0, passenger.RemainingMovementPoints), ShuttlePickupRange);
        var passPaths = UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap, passenger, budget, terrainDatabase);
        if (passPaths == null || passPaths.Count == 0)
        {
            Vector3Int passCell = passenger.CurrentCellPosition; passCell.z = 0;
            return new HashSet<Vector3Int> { passCell };
        }
        return new HashSet<Vector3Int>(passPaths.Keys);
    }

    private bool HasBlockingGroundUnitAtCell(Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.IsEmbarked || u.IsDead) continue;
            if (!u.gameObject.activeInHierarchy) continue;
            if (u.BoardTilemap != boardTilemap) continue;
            Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
            if (uc != cell) continue;
            if (OccupancyResolver.GetHeightBand(u) == HeightBand.Blocking)
                return true;
        }
        return false;
    }
}
