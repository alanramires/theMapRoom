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

            Vector3Int moveTarget = FindTransportShuttleMove(fromCell, candidateCell, paths, occupied, snapshot.AITeam);
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} shuttle — candidato {bestCandidate.InstanceId}@{candidateCell} via {moveTarget}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
        }

        // No pickup candidate: head toward nearest friendly factory/HQ
        Vector3Int waitTarget = FindTransportWaitTarget(snapshot.AITeam, fromCell);
        Vector3Int waitMove = FindTransportMove(fromCell, waitTarget, paths, occupied, snapshot.AITeam);
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

    private Vector3Int FindTransportShuttleMove(
        Vector3Int fromCell,
        Vector3Int candidateCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam)
    {
        // Already adjacent — stay so the capturer can embark
        if (SectorManager.HexDistance(fromCell, candidateCell) < 1.5f)
            return fromCell;

        // Find reachable cell that is adjacent (1 step) to the candidate
        Vector3Int bestAdj = fromCell;
        float bestAdjThreat = float.MaxValue;
        bool foundAdj = false;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (occupied.Contains(cell)) continue;
            if (SectorManager.HexDistance(cell, candidateCell) > 1.5f) continue;
            if (IsNonTeamConstruction(cell, aiTeam)) continue;

            float threat = CalculateThreatLevel(cell, aiTeam);
            if (!foundAdj || threat < bestAdjThreat - 0.001f)
            {
                bestAdj = cell;
                bestAdjThreat = threat;
                foundAdj = true;
            }
        }

        if (foundAdj) return bestAdj;

        // Candidate not reachable in one step: max displacement toward them
        return FindTransportMove(fromCell, candidateCell, paths, occupied, aiTeam);
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
