using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIPlayerController
{
    private bool TryGetImmediateSupplyTargetsNow(UnitManager supplier, out List<UnitManager> targets)
    {
        return TryCollectPrioritizedSupplyTargetsNow(supplier, null, out targets);
    }

    private bool TryCollectPrioritizedSupplyTargetsNow(UnitManager supplier, UnitManager preferredTarget, out List<UnitManager> targets)
    {
        targets = new List<UnitManager>();
        if (supplier == null || turnStateManager == null)
            return false;

        List<PodeSuprirOption> sensorOptions = new List<PodeSuprirOption>();
        if (!turnStateManager.TryGetSupplyTargets(supplier, sensorOptions, out _) || sensorOptions.Count <= 0)
            return false;

        int queueLimit = 1;
        AIUnitProfile supplierProfile = null;
        if (supplier.TryGetUnitData(out UnitData supplierData) && supplierData != null)
        {
            queueLimit = Mathf.Max(1, supplierData.maxUnitsServedPerTurn);
            supplierProfile = supplierData.aiUnitProfile;
        }

        Vector3Int supplierCell = supplier.CurrentCellPosition;
        supplierCell.z = 0;

        List<(UnitManager target, int distance, int score)> ranked = new List<(UnitManager target, int distance, int score)>();
        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < sensorOptions.Count; i++)
        {
            UnitManager candidate = sensorOptions[i] != null ? sensorOptions[i].targetUnit : null;
            if (candidate == null || candidate.IsDead || candidate.ReceivedSuppliesThisTurn || !seen.Add(candidate.InstanceId))
                continue;

            bool isPreferred = preferredTarget != null && candidate == preferredTarget;
            int score = 0;
            if (!IsSupplyTruckTargetThresholdMet(candidate, out score, supplierProfile))
            {
                if (!isPreferred)
                    continue;

                // Mantem coerencia entre a decisao tomada antes do movimento e a execucao final.
                score = 100000;
            }
            else if (isPreferred)
            {
                score += 100000;
            }

            Tilemap boardTilemap = supplier.BoardTilemap != null ? supplier.BoardTilemap : candidate.BoardTilemap;
            Vector3Int candidateCell = candidate.CurrentCellPosition;
            candidateCell.z = 0;
            int distance = boardTilemap != null
                ? GetHexDistance(boardTilemap, supplierCell, candidateCell, 64)
                : 64;
            if (distance == int.MaxValue)
                distance = 64;

            ranked.Add((candidate, distance, score));
        }

        ranked.Sort((a, b) =>
        {
            int distanceCompare = a.distance.CompareTo(b.distance);
            if (distanceCompare != 0)
                return distanceCompare;
            return b.score.CompareTo(a.score);
        });
        for (int i = 0; i < ranked.Count && targets.Count < queueLimit; i++)
            targets.Add(ranked[i].target);

        return targets.Count > 0;
    }

    private static bool IsSupplyTruckTargetThresholdMet(UnitManager ally, out int score, AIUnitProfile supplierProfile = null)
    {
        score = 0;
        if (ally == null || ally.IsDead)
            return false;

        int fuelThresholdPct = supplierProfile != null ? supplierProfile.supplyAllyFuelThresholdPercent : 25;
        int ammoThresholdPct = supplierProfile != null ? supplierProfile.supplyAllyAmmoThresholdPercent : 0;
        int hpThresholdPct = supplierProfile != null ? supplierProfile.supplyAllyHpThresholdPercent : 50;

        int maxHp = Mathf.Max(1, ally.GetMaxHP());
        bool lowHp = hpThresholdPct > 0 && ally.CurrentHP * 100 < maxHp * hpThresholdPct;

        bool lowAmmo = false;
        int ammoCurrentTotal = 0;
        int ammoMaxTotal = 0;
        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = ally.GetEmbarkedWeapons();
        if (runtimeWeapons != null && runtimeWeapons.Count > 0)
        {
            if (ally.TryGetUnitData(out UnitData allyData) && allyData != null && allyData.embarkedWeapons != null)
            {
                int count = Mathf.Min(runtimeWeapons.Count, allyData.embarkedWeapons.Count);
                for (int i = 0; i < count; i++)
                {
                    UnitEmbarkedWeapon runtime = runtimeWeapons[i];
                    UnitEmbarkedWeapon baseline = allyData.embarkedWeapons[i];
                    if (runtime == null || baseline == null || baseline.weapon == null)
                        continue;

                    int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
                    if (maxAmmo <= 0)
                        continue;

                    ammoMaxTotal += maxAmmo;
                    ammoCurrentTotal += Mathf.Clamp(runtime.squadAmmunition, 0, maxAmmo);
                }
            }
        }

        if (ammoMaxTotal > 0)
        {
            int ammoPercentCurrent = ammoCurrentTotal * 100 / ammoMaxTotal;
            lowAmmo = ammoPercentCurrent <= ammoThresholdPct;
        }

        int maxFuel = ally.GetMaxFuel();
        bool lowAutonomy = false;
        if (maxFuel > 0 && fuelThresholdPct > 0)
            lowAutonomy = ally.CurrentFuel * 100 <= maxFuel * fuelThresholdPct;

        if (!lowHp && !lowAmmo && !lowAutonomy)
            return false;

        int hpMissing = Mathf.Max(0, maxHp - ally.CurrentHP);
        int ammoMissing = Mathf.Max(0, ammoMaxTotal - ammoCurrentTotal);
        int fuelMissing = maxFuel > 0 ? Mathf.Max(0, maxFuel - ally.CurrentFuel) : 0;
        score = hpMissing * 120 + ammoMissing * 45 + fuelMissing * 35
            + (lowHp ? 5000 : 0) + (lowAmmo ? 3000 : 0) + (lowAutonomy ? 2500 : 0);
        return true;
    }

    private bool TryGetSupplierIdleParkingCell(UnitManager unit, AISnapshot snapshot, HashSet<Vector3Int> occupiedByAllies, out Vector3Int targetCell)
    {
        targetCell = default;
        if (unit == null || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        Vector3Int anchorCell;
        if (TryGetNearestOwnedConstruction(unit, snapshot, out _, out anchorCell, out _))
        {
            anchorCell.z = 0;
        }
        else if (snapshot.HasHq)
        {
            anchorCell = snapshot.HqCell;
            anchorCell.z = 0;
        }
        else
        {
            return false;
        }

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(snapshot.BoardTilemap, anchorCell, neighbors);

        bool found = false;
        int bestDistance = int.MaxValue;
        Vector3Int bestCell = default;

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int candidate = neighbors[i];
            candidate.z = 0;
            if (IsFriendlyConstructionCell(snapshot, unit.TeamId, candidate))
                continue;
            if (occupiedByAllies != null && occupiedByAllies.Contains(candidate))
                continue;
            if (IsCellOccupiedBySnapshotUnit(snapshot, unit, null, candidate))
                continue;
            if (!CanAiUnitEndMoveAtCell(unit, snapshot.BoardTilemap, candidate))
                continue;

            int distance = GetHexDistance(snapshot.BoardTilemap, unitCell, candidate, 64);
            if (distance == int.MaxValue)
                continue;

            if (!found || distance < bestDistance)
            {
                found = true;
                bestDistance = distance;
                bestCell = candidate;
            }
        }

        if (!found)
            return false;

        targetCell = bestCell;
        targetCell.z = 0;
        return true;
    }

    private bool TryGetSupplyTruckDefensivePatrolCell(UnitManager unit, AISnapshot snapshot, out Vector3Int targetCell)
    {
        targetCell = default;
        if (unit == null || snapshot == null || snapshot.BoardTilemap == null || !snapshot.HasHq)
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        Vector3Int hqCell = snapshot.HqCell;
        hqCell.z = 0;

        List<Vector3Int> frontier = new List<Vector3Int> { hqCell };
        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { hqCell };
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        int bestDistToUnit = int.MaxValue;
        int bestDistToHq = int.MaxValue;
        Vector3Int best = default;
        bool found = false;

        for (int depth = 0; depth < 3; depth++)
        {
            List<Vector3Int> nextFrontier = new List<Vector3Int>();
            for (int i = 0; i < frontier.Count; i++)
            {
                Vector3Int current = frontier[i];
                UnitMovementPathRules.GetImmediateHexNeighbors(snapshot.BoardTilemap, current, neighbors);
                for (int n = 0; n < neighbors.Count; n++)
                {
                    Vector3Int cell = neighbors[n];
                    cell.z = 0;
                    if (visited.Contains(cell))
                        continue;
                    visited.Add(cell);
                    nextFrontier.Add(cell);

                    if (IsFriendlyConstructionCell(snapshot, unit.TeamId, cell))
                        continue;

                    int distToHq = GetHexDistance(snapshot.BoardTilemap, hqCell, cell, 32);
                    if (distToHq == int.MaxValue || distToHq <= 0 || distToHq > 2)
                        continue;

                    int distToUnit = GetHexDistance(snapshot.BoardTilemap, unitCell, cell, 64);
                    if (distToUnit == int.MaxValue)
                        continue;

                    bool better = !found
                        || distToUnit < bestDistToUnit
                        || (distToUnit == bestDistToUnit && distToHq < bestDistToHq);
                    if (!better)
                        continue;

                    found = true;
                    bestDistToUnit = distToUnit;
                    bestDistToHq = distToHq;
                    best = cell;
                }
            }

            frontier = nextFrontier;
            if (frontier.Count <= 0)
                break;
        }

        if (!found)
            return false;

        targetCell = best;
        targetCell.z = 0;
        return true;
    }

    private HashSet<Vector3Int> BuildFriendlySupportPreferenceCells(
        AISnapshot snapshot,
        UnitManager requester,
        bool supportOnlyCombatAnchors)
    {
        if (snapshot == null || snapshot.BoardTilemap == null || requester == null || snapshot.FriendlyUnits == null)
            return null;

        HashSet<Vector3Int> seeds = null;
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager ally = snapshot.FriendlyUnits[i];
            if (ally == null || ally.IsDead || ally == requester)
                continue;

            if (supportOnlyCombatAnchors)
            {
                UnitCombatClassification allyClass = ally.CombatClassification;
                if (allyClass != UnitCombatClassification.Combatente && allyClass != UnitCombatClassification.Hibrido)
                    continue;
            }

            if (seeds == null)
                seeds = new HashSet<Vector3Int>();

            Vector3Int cell = ally.CurrentCellPosition;
            cell.z = 0;
            seeds.Add(cell);
        }

        if (seeds == null || seeds.Count == 0)
            return null;

        return ExpandCellsByHexRadius(snapshot.BoardTilemap, seeds, 1);
    }

    private HashSet<Vector3Int> BuildEnemyDangerCells(AISnapshot snapshot, int radius)
    {
        if (snapshot == null || snapshot.BoardTilemap == null || snapshot.VisibleEnemies == null || snapshot.VisibleEnemies.Count == 0)
            return null;

        HashSet<Vector3Int> seeds = null;
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            if (seeds == null)
                seeds = new HashSet<Vector3Int>();

            Vector3Int cell = enemy.CurrentCellPosition;
            cell.z = 0;
            seeds.Add(cell);
        }

        if (seeds == null || seeds.Count == 0)
            return null;

        return ExpandCellsByHexRadius(snapshot.BoardTilemap, seeds, Mathf.Max(0, radius));
    }

    private static HashSet<Vector3Int> ExpandCellsByHexRadius(Tilemap boardTilemap, HashSet<Vector3Int> seeds, int radius)
    {
        if (boardTilemap == null || seeds == null || seeds.Count == 0)
            return null;

        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        Queue<int> depths = new Queue<int>();
        foreach (Vector3Int seed in seeds)
        {
            Vector3Int normalized = seed;
            normalized.z = 0;
            if (!visited.Add(normalized))
                continue;

            frontier.Enqueue(normalized);
            depths.Enqueue(0);
        }

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int depth = depths.Dequeue();
            if (depth >= radius)
                continue;

            neighbors.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (!visited.Add(next))
                    continue;

                frontier.Enqueue(next);
                depths.Enqueue(depth + 1);
            }
        }

        return visited;
    }

    private static HashSet<Vector3Int> MergeCellSets(HashSet<Vector3Int> primary, HashSet<Vector3Int> secondary)
    {
        if (primary == null || primary.Count == 0)
            return secondary;
        if (secondary == null || secondary.Count == 0)
            return primary;

        HashSet<Vector3Int> merged = new HashSet<Vector3Int>(primary);
        merged.UnionWith(secondary);
        return merged;
    }

    private bool IsCellTooDangerousForSupport(AISnapshot snapshot, Vector3Int cell, bool allowModerateRiskForSupply = false, AIUnitProfile supplierProfile = null)
    {
        if (snapshot == null || snapshot.BoardTilemap == null || snapshot.VisibleEnemies == null)
            return false;

        int hardDangerRadius = supplierProfile != null ? supplierProfile.supplyHardDangerRadius : 2;
        int softDangerRadius = supplierProfile != null ? supplierProfile.supplySoftDangerRadius : 4;
        int softThreatTolerance = supplierProfile != null
            ? (allowModerateRiskForSupply ? supplierProfile.supplySoftThreatToleranceServing : supplierProfile.supplySoftThreatToleranceIdle)
            : (allowModerateRiskForSupply ? 2 : 1);
        int softThreatLimit = softThreatTolerance + 1;

        cell.z = 0;
        int nearbyThreats = 0;
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            int dist = GetHexDistance(snapshot.BoardTilemap, cell, enemyCell, 64);
            if (dist == int.MaxValue)
                continue;

            if (hardDangerRadius > 0 && dist <= hardDangerRadius)
                return true;
            if (softDangerRadius > hardDangerRadius && dist <= softDangerRadius)
                nearbyThreats++;
        }

        return nearbyThreats >= softThreatLimit;
    }

    private HashSet<Vector3Int> BuildReservedCaptureCellsForEscort(UnitManager unit, AIPlanIntent unitIntent)
    {
        if (unit == null || unitIntent == null || unitIntent.Assignments == null || unitIntent.Assignments.Count == 0)
            return null;

        HashSet<Vector3Int> reserved = null;
        for (int i = 0; i < unitIntent.Assignments.Count; i++)
        {
            AIPlanAssignment asgn = unitIntent.Assignments[i];
            if (asgn == null || asgn.UnitInstanceId == unit.InstanceId || asgn.Role != AIPlanRole.Capture || !asgn.HasPlannedCaptureTarget)
                continue;

            if (reserved == null)
                reserved = new HashSet<Vector3Int>();

            Vector3Int cell = asgn.PlannedCaptureCell;
            cell.z = 0;
            reserved.Add(cell);
        }

        return reserved;
    }
}
