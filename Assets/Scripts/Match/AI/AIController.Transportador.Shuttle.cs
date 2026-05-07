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

        if (bestCandidate != null)
        {
            if (TryFindTransportBreakerAttack(unit, snapshot, fromCell, paths, occupied, candidateCell,
                    out Vector3Int attackCell, out UnitManager attackTarget))
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

        // No pickup candidate: head toward nearest friendly factory/HQ
        Vector3Int waitTarget = FindTransportWaitTarget(snapshot.AITeam, fromCell);
        Vector3Int waitMove = FindTransportMove(unit, fromCell, waitTarget, paths, occupied, snapshot.AITeam);
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} shuttle — sem candidato, aguarda em {waitMove}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, waitMove, paths);
    }

    // -------------------------------------------------------------------------
    // Candidate selection
    // -------------------------------------------------------------------------

    private UnitManager FindBestShuttleCandidate(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int transporterCell,
        out Vector3Int bestCandidateCell)
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

            if (FindFittingSlotIndex(transporter, transporterData, candidateData) < 0) continue;

            Vector3Int objectiveCell = ResolveUnitObjectiveCell(candidate, plan, snapshot);
            if (objectiveCell == Vector3Int.zero) continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition; candidateCell.z = 0;
            float objectiveDist = SectorManager.HexDistance(candidateCell, objectiveCell);
            if (objectiveDist <= MinDistanceForTransportSlot) continue;

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

    private static int FindFittingSlotIndex(UnitManager transporter, UnitData transporterData, UnitData candidateData)
    {
        if (transporterData.transportSlots == null) return -1;
        for (int i = 0; i < transporterData.transportSlots.Count; i++)
        {
            UnitTransportSlotRule slot = transporterData.transportSlots[i];
            if (slot == null) continue;
            if (slot.allowedClasses != null && slot.allowedClasses.Count > 0
                && !slot.allowedClasses.Contains(candidateData.unitClass)) continue;
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
        // within ShuttlePickupRange of the passenger that is closest to the objective.
        // "Stay" counts as a candidate if already in range and not blocking production.
        if (hasObjective)
        {
            Vector3Int best = fromCell;
            float bestDistToObj = float.MaxValue;
            bool bestIsProductionBldg = true;
            float bestThreat = float.MaxValue;
            bool found = false;

            if (SectorManager.HexDistance(fromCell, candidateCell) <= ShuttlePickupRange && !fromIsProductionBldg)
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
                if (SectorManager.HexDistance(cell, candidateCell) > ShuttlePickupRange) continue;

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
            // No cell within pickup range reachable — fall through to adjacent/displacement
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
        return FindTransportMove(unit, fromCell, candidateCell, paths, occupied, aiTeam);
    }

    private bool IsTeamProductionBuilding(Vector3Int cell, TeamId aiTeam)
    {
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return bldg != null && bldg.TeamId == aiTeam && bldg.CanProduceUnits;
    }

    // Returns the nearest friendly factory or HQ to wait at between deliveries.
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
        out UnitManager bestTarget)
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
