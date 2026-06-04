using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private bool TryGetEmbarkStopPath(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int stopCell,
        Dictionary<Vector3Int, List<Vector3Int>> movePaths,
        out List<Vector3Int> path)
    {
        path = null;
        fromCell.z = 0;
        stopCell.z = 0;

        if (movePaths != null && movePaths.TryGetValue(stopCell, out path) && path != null && path.Count > 0)
        {
            Vector3Int last = path[path.Count - 1];
            last.z = 0;
            if (last == stopCell && CanUseCellForEmbarkApproach(unit, stopCell, isDestination: true))
                return true;

            Debug.Log($"{TL("Capturador")} {unit.InstanceId} descarta path de embarque stale para {stopCell}: last={last} usable={CanUseCellForEmbarkApproach(unit, stopCell, isDestination: true)}");
        }

        // Pathfinding can miss an otherwise valid embark stop when the target hex is
        // occupied by a non-blocking air layer unit or the occupancy grid is stale after
        // another AI action. Rebuild a small local path using current unit positions.
        return TryFindEmbarkStopPath(unit, fromCell, stopCell, Mathf.Max(0, unit.RemainingMovementPoints), out path);
    }


    private bool TryFindEmbarkStopPath(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int stopCell,
        int maxMovement,
        out List<Vector3Int> path)
    {
        path = null;
        if (unit == null || maxMovement <= 0)
            return false;

        fromCell.z = 0;
        stopCell.z = 0;

        var frontier = new Queue<Vector3Int>();
        var costByCell = new Dictionary<Vector3Int, int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var neighbors = new List<Vector3Int>(6);

        frontier.Enqueue(fromCell);
        costByCell[fromCell] = 0;
        cameFrom[fromCell] = fromCell;

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            if (current == stopCell)
                break;

            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, current, neighbors);
            foreach (Vector3Int rawNext in neighbors)
            {
                Vector3Int next = rawNext;
                next.z = 0;

                if (!UnitMovementPathRules.TryGetEnterCellCost(
                        boardTilemap, unit, next, terrainDatabase, false, out int enterCost))
                    continue;

                enterCost = Mathf.Max(1, enterCost);
                int nextCost = costByCell[current] + enterCost;
                if (nextCost > maxMovement)
                    continue;

                if (!CanUseCellForEmbarkApproach(unit, next, isDestination: next == stopCell))
                    continue;

                if (costByCell.TryGetValue(next, out int knownCost) && knownCost <= nextCost)
                    continue;

                costByCell[next] = nextCost;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(stopCell))
            return false;

        path = new List<Vector3Int>();
        Vector3Int cursor = stopCell;
        while (true)
        {
            path.Add(cursor);
            if (cursor == fromCell)
                break;
            cursor = cameFrom[cursor];
        }
        path.Reverse();
        return path.Count >= 2;
    }


    private bool CanUseCellForEmbarkApproach(UnitManager unit, Vector3Int cell, bool isDestination)
    {
        foreach (UnitManager occupant in UnitManager.AllActive)
        {
            if (occupant == null || occupant == unit || occupant.IsDead || occupant.IsEmbarked)
                continue;

            Vector3Int occCell = occupant.CurrentCellPosition;
            occCell.z = 0;
            if (occCell != cell)
                continue;

            if (!OccupancyResolver.CanPassThrough(unit, occupant, cell))
                return false;

            if (isDestination
                && occupant.TeamId == unit.TeamId
                && OccupancyResolver.GetHeightBand(occupant) == OccupancyResolver.GetHeightBand(unit))
                return false;
        }

        return true;
    }


    private int CalculateRemainingMovementAfterPath(UnitManager unit, IReadOnlyList<Vector3Int> path)
    {
        if (unit == null)
            return 0;
        int used = UnitMovementPathRules.CalculateAutonomyCostForPath(
            boardTilemap,
            unit,
            path,
            terrainDatabase,
            applyOperationalAutonomyModifier: false);
        return Mathf.Max(0, unit.RemainingMovementPoints - used);
    }


    private void CollectEmbarkTargetCells(
        Vector3Int passengerCell,
        UnitManager passenger,
        List<Vector3Int> neighborBuf,
        List<Vector3Int> output,
        HashSet<Vector3Int> seenCells)
    {
        output.Clear();
        seenCells.Clear();
        neighborBuf.Clear();

        passengerCell.z = 0;
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, passengerCell, neighborBuf);
        foreach (Vector3Int n in neighborBuf)
        {
            Vector3Int cell = n;
            cell.z = 0;
            if (seenCells.Add(cell))
                output.Add(cell);
        }
    }

    // Retorna true se outro capturer do mesmo setor está mais longe do objetivo e
    // ainda dentro do pickup range do APC — este capturer deve ceder a vaga.

    private bool ShouldYieldEmbarkToNeedierCapturer(
        UnitManager unit, UnitManager transporter, SectorObjective assigned, TeamObjectivePlan plan)
    {
        if (assigned == null || plan == null) return false;

        ConstructionManager objBuilding = FindCapturableInSector(assigned.Sector, unit.TeamId);
        if (objBuilding == null) return false;
        Vector3Int objCell = objBuilding.CurrentCellPosition; objCell.z = 0;

        Vector3Int myCell = unit.CurrentCellPosition; myCell.z = 0;
        float myDist = SectorManager.HexDistance(myCell, objCell);

        Vector3Int apcCell = transporter.CurrentCellPosition; apcCell.z = 0;

        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
            if (slot.AssignedUnitId == unit.InstanceId) continue;

            UnitManager other = FindActiveUnit(slot.AssignedUnitId, unit.TeamId);
            if (other == null || other.HasActed || other.IsEmbarked || other.IsDead) continue;

            Vector3Int otherCell = other.CurrentCellPosition; otherCell.z = 0;
            float otherDist = SectorManager.HexDistance(otherCell, objCell);
            if (otherDist <= myDist) continue; // não está mais longe

            float otherDistToAPC = SectorManager.HexDistance(otherCell, apcCell);
            if (otherDistToAPC > ShuttlePickupRange + 1 + 0.5f) continue; // fora do alcance do APC

            int openSeats = CountAvailableSeatsForPassenger(transporter, unit);
            if (openSeats > 1)
            {
                Debug.Log($"{TL("Capturador")} {unit.InstanceId} mantem embarque com {other.InstanceId}: transporter={transporter.InstanceId}@{apcCell} openSeats={openSeats} ({otherDist:F0}h > {myDist:F0}h ao objetivo)");
                return false;
            }

            Debug.Log($"{TL("Capturador")} {unit.InstanceId} cede embarque para {other.InstanceId}: transporter={transporter.InstanceId}@{apcCell} openSeats={openSeats} myCell={myCell} otherCell={otherCell} ({otherDist:F0}h > {myDist:F0}h ao objetivo)");
            return true;
        }

        return false;
    }


}
