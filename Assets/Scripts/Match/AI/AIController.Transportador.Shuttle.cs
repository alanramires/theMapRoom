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
            int relaxed = Mathf.Max(2, GetEffectiveTransportThreshold(snapshot.AITeam) / 2);
            bestCandidate = FindBestShuttleCandidate(unit, snapshot, plan, fromCell, out candidateCell,
                thresholdReduction: relaxed);
            if (bestCandidate != null)
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} shuttle — candidato relaxado {bestCandidate.InstanceId}@{candidateCell} (threshold -{relaxed})");
        }

        bool preferNoMove = unit.TryGetUnitData(out UnitData shuttleData) && shuttleData.prioritizeDpqAtBattle;

        if (bestCandidate != null)
        {
            if (TryFindTransportBreakerAttack(unit, snapshot, fromCell, paths, occupied, candidateCell,
                    out Vector3Int attackCell, out UnitManager attackTarget, preferNoMove))
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

        // No pickup candidate: release to HexEvaluator for opportunistic combat/positioning.
        // Capture actions are suppressed for Transportador units in the Router.
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} shuttle — sem candidato, libera para suporte de combate");
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
            if (candidate.TeamId != snapshot.AITeam || candidate.IsDead || candidate.IsEmbarked || candidate.HasActed) continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData)) continue;

            if (FindFittingSlotIndex(transporter, transporterData, candidate, candidateData) < 0) continue;

            // Skip candidates that are already the formal passenger of another transporter.
            // A candidate is "taken" when both conditions hold in the same SectorObjective:
            //   1. they occupy a filled Capturador slot, AND
            //   2. a different transporter occupies a filled Transportador slot.
            if (IsAlreadyFormalPassenger(candidate, transporter, plan)) continue;

            // Embark compatibility: only pick candidates that will actually accept the ride.
            // Primary capturers with a plan assignment will refuse a rogue shuttle (no sector
            // assignment) because the embark check requires sameSector. Skip them here to avoid
            // the APC wasting movement toward a capturer that will refuse on their own turn.
            SectorObjective candidateAssigned = plan != null ? ResolveAssignedObjective(candidate, plan) : null;
            bool candidateIsPrimary = candidateData.roles != null && candidateData.roles.Count > 0
                && candidateData.roles[0] == UnitRole.Capturador;
            if (assignedSector == null)
            {
                // Rogue shuttle: primary capturer with a plan assignment will refuse.
                if (candidateIsPrimary && candidateAssigned != null) continue;
            }
            else if (plan != null)
            {
                // Assigned shuttle: only candidates heading to the same sector will accept.
                if (candidateAssigned == null || candidateAssigned.Sector != assignedSector.Sector) continue;
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

    private static bool IsAlreadyFormalPassenger(UnitManager candidate, UnitManager thisTransporter, TeamObjectivePlan plan)
    {
        if (plan == null) return false;
        foreach (SectorObjective obj in plan.Objectives)
        {
            bool candidateIsPassenger = false;
            bool otherTransporterAssigned = false;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot.Role == UnitRole.Capturador && slot.Filled && slot.AssignedUnitId == candidate.InstanceId)
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
        Vector3Int objectiveCell = default)
    {
        bool fromIsProductionBldg = IsTeamProductionBuilding(fromCell, aiTeam);
        bool hasObjective = objectiveCell != default && objectiveCell != Vector3Int.zero;
        const float eps = 0.1f;

        // When we know the destination, park on the path: find the reachable cell
        // within pickup range of the passenger that is closest to the objective.
        // Try preferred range (2h) first; expand to extended range (3h) if nothing found.
        // "Stay" counts as a candidate if already in range and not blocking production.
        if (hasObjective)
        {
            for (int pickupRange = ShuttlePickupRange; pickupRange <= ShuttlePickupRange + 1; pickupRange++)
            {
                Vector3Int best = fromCell;
                float bestDistToObj = float.MaxValue;
                bool bestIsProductionBldg = true;
                float bestThreat = float.MaxValue;
                bool found = false;

                if (SectorManager.HexDistance(fromCell, candidateCell) <= pickupRange && !fromIsProductionBldg)
                {
                    best = fromCell;
                    bestDistToObj = SectorManager.HexDistance(fromCell, objectiveCell);
                    bestIsProductionBldg = false;
                    bestThreat = CalculateThreatLevel(fromCell, aiTeam);
                    found = true;
                }

                foreach (Vector3Int cell in paths.Keys)
                {
                    if (cell == fromCell) continue;
                    if (occupied.Contains(cell)) continue;
                    if (IsNonTeamConstruction(cell, aiTeam)) continue;
                    if (SectorManager.HexDistance(cell, candidateCell) > pickupRange) continue;

                    float distToObj = SectorManager.HexDistance(cell, objectiveCell);
                    bool isProductionBldg = IsTeamProductionBuilding(cell, aiTeam);
                    float threat = CalculateThreatLevel(cell, aiTeam);

                    bool isBetter = !found
                        || distToObj < bestDistToObj - eps
                        || (distToObj < bestDistToObj + eps && !isProductionBldg && bestIsProductionBldg)
                        || (distToObj < bestDistToObj + eps && isProductionBldg == bestIsProductionBldg && threat < bestThreat - 0.001f);

                    if (isBetter)
                    {
                        best = cell;
                        bestDistToObj = distToObj;
                        bestIsProductionBldg = isProductionBldg;
                        bestThreat = threat;
                        found = true;
                    }
                }

                if (found) return best;
            }
            // No cell within extended pickup range reachable — fall through to rendezvous move
        }

        // Fallback: original adjacent-first behavior
        if (SectorManager.HexDistance(fromCell, candidateCell) < 1.5f && !fromIsProductionBldg)
            return fromCell;

        Vector3Int bestAdj = fromCell;
        float bestAdjThreat = float.MaxValue;
        bool bestAdjIsProductionBldg = fromIsProductionBldg;
        bool foundAdj = false;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (occupied.Contains(cell)) continue;
            if (SectorManager.HexDistance(cell, candidateCell) > 1.5f) continue;
            if (IsNonTeamConstruction(cell, aiTeam)) continue;

            bool cellIsProductionBldg = IsTeamProductionBuilding(cell, aiTeam);
            float threat = CalculateThreatLevel(cell, aiTeam);

            bool isBetter = !foundAdj
                || (!cellIsProductionBldg && bestAdjIsProductionBldg)
                || (cellIsProductionBldg == bestAdjIsProductionBldg && threat < bestAdjThreat - 0.001f);

            if (isBetter)
            {
                bestAdj = cell;
                bestAdjThreat = threat;
                bestAdjIsProductionBldg = cellIsProductionBldg;
                foundAdj = true;
            }
        }

        if (foundAdj) return bestAdj;
        if (SectorManager.HexDistance(fromCell, candidateCell) < 1.5f) return fromCell;

        // When we know the objective, head toward the rendezvous: the cell ~2h from the
        // capturer along the capturer→objective direction. This keeps the APC on the
        // delivery route so both sides converge; falling back to the capturer directly
        // forces a needless detour away from the objective.
        if (hasObjective)
        {
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

    private bool IsTeamProductionBuilding(Vector3Int cell, TeamId aiTeam)
    {
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return bldg != null && bldg.TeamId == aiTeam && bldg.CanProduceUnits;
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
            if (opt?.targetUnit == null || opt.targetUnit.TeamId == snapshot.AITeam || opt.targetUnit.IsDead) continue;
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
            if (bldg == null || bldg.TeamId != aiTeam) continue;
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

    private bool TryFindTransportBreakerAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int candidateCell,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        bool preferNoMove = false)
    {
        bestCell = fromCell;
        bestTarget = null;

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (enemies == null || enemies.Count == 0) return false;

        // When prioritizeDpqAtBattle: try attacking from current position first (no movement).
        if (preferNoMove)
        {
            foreach (UnitManager enemy in enemies)
            {
                if (!CanAttackTargetFrom(fromCell, fromCell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, fromCell, false, out _)) continue;
                bestCell = fromCell;
                bestTarget = enemy;
                return true;
            }
        }

        float fromDistToCandidate = SectorManager.HexDistance(fromCell, candidateCell);
        float bestScore = float.MinValue;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied.Contains(cell)) continue;
            if (SectorManager.HexDistance(cell, candidateCell) > fromDistToCandidate + 1f) continue;

            foreach (UnitManager enemy in enemies)
            {
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, cell, false, out _)) continue;

                float score = (20f - enemy.CurrentHP) * 100f
                    - SectorManager.HexDistance(cell, candidateCell) * 50f;
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
