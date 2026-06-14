using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private Vector3Int FindRepairApproachStep(
        UnitManager unit,
        TeamId aiTeam,
        Vector3Int fromCell,
        Vector3Int destCell,
        ConstructionManager repairDest,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int? altDestCell,
        out bool usedEmergencyFlee)
    {
        usedEmergencyFlee = false;
        if (paths == null || paths.Count == 0)
            return fromCell;

        bool homeRepair = IsRepairHomeConstruction(repairDest, aiTeam);
        bool destOccupied = occupied != null && occupied.Contains(destCell) && destCell != fromCell;
        if (!destOccupied && paths.ContainsKey(destCell))
            return destCell;

        Vector3Int bestStep = ScoreRepairApproach(unit, aiTeam, fromCell, destCell, paths, occupied, homeRepair);

        // Secondary anchor: if still stuck in a hotzone, retry scoring toward HQ (or altDest).
        // A cell that's -5 toward Mike can be +1 toward HQ — either direction breaks the deadlock.
        if (bestStep == fromCell
            && altDestCell.HasValue
            && altDestCell.Value != destCell
            && HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange))
        {
            Vector3Int altDest = altDestCell.Value; altDest.z = 0;
            Vector3Int altStep = ScoreRepairApproach(unit, aiTeam, fromCell, altDest, paths, occupied, homeRepair: false);
            if (altStep != fromCell)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} redirecionado p/ âncora secundária {altDest} via {altStep} (destino primário {destCell} bloqueado)");
                bestStep = altStep;
            }
        }

        // Last-resort: if still stuck and in a hotzone, flee to the minimum-threat reachable cell.
        if (bestStep == fromCell && HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange))
        {
            Vector3Int fleeCell = fromCell;
            float lowestThreat = float.MaxValue;
            foreach (Vector3Int cell in paths.Keys)
            {
                if (cell == fromCell) continue;
                if (occupied != null && occupied.Contains(cell)) continue;
                float t = CalculateThreatLevel(cell, aiTeam);
                if (t < lowestThreat) { lowestThreat = t; fleeCell = cell; }
            }
            if (fleeCell != fromCell)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} fuga de emergência — todas âncoras bloqueadas; foge p/ {fleeCell} (threat={lowestThreat:F0})");
                usedEmergencyFlee = true;
                bestStep = fleeCell;
            }
        }

        return bestStep;
    }


    private Vector3Int ScoreRepairApproach(
        UnitManager unit, TeamId aiTeam, Vector3Int fromCell, Vector3Int target,
        Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied, bool homeRepair)
    {
        float fromDist = SectorManager.HexDistance(fromCell, target);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, target, out float fromRouteDist);
        Vector3Int bestStep = fromCell;
        float bestScore = float.MinValue;
        int bestToolScore = int.MinValue;
        float bestToolNextDist = float.MaxValue;
        int bestToolMoveCost = int.MaxValue;
        float bestLegacyScore = float.MinValue;
        float bestThreat = float.MaxValue;
        bool bestRoadBonus = false;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;

            float dist = SectorManager.HexDistance(cell, target);
            bool cellRouteFound = TryCalculateRouteDistance(unit, cell, target, out float routeDist);
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - routeDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            // When recovering a missing route, credit progress as if we closed fromDist worth of gap
            // (treating "no route" as effectively infinite distance). Without this, -routeDist produces
            // a catastrophic score that always loses to staying put.
            float progress = recoversMissingRoute
                ? fromDist
                : (fromRouteFound && cellRouteFound) ? routeProgress : fromDist - dist;
            float recoveryBonus = recoversMissingRoute ? 5000f : 0f;
            float effectiveDist = cellRouteFound ? routeDist : dist;
            float threat = CalculateThreatLevel(cell, aiTeam);
            float pathCost = cell == fromCell ? 0f : GetPathStepCount(paths, cell);

            float threatMult = homeRepair ? 0f : 0.35f;
            float score =
                progress * 1200f
                - effectiveDist * 180f
                - pathCost * 4f
                - threat * ThreatWeight * threatMult
                + recoveryBonus;

            if (homeRepair && progress > 0f)
                score += 350f;
            if (cell == target)
                score += 10000f;

            bool hasToolScore = TryScoreToolRouteProgression(
                unit,
                fromCell,
                target,
                cell,
                paths[cell],
                occupied,
                out int toolScore,
                out float toolNextDist,
                out int toolMoveCost);

            int candidateToolScore = hasToolScore ? toolScore : int.MinValue;
            float candidateToolNextDist = hasToolScore ? toolNextDist : float.MaxValue;
            int candidateToolMoveCost = hasToolScore ? toolMoveCost : int.MaxValue;
            bool roadBonus = cell != fromCell
                && UnitMovementPathRules.DidUseRoadFullMoveBonus(boardTilemap, unit, paths[cell], terrainDatabase);

            float candidateScore = hasToolScore ? candidateToolScore * 1000f : score;
            if (roadBonus)
                candidateScore += 500f;
            if (cell == target)
                candidateScore += 25000f;
            if (homeRepair && progress > 0f)
                candidateScore += 350f;
            candidateScore += Mathf.Clamp(score, -5000f, 5000f) * 0.01f;
            candidateScore -= threat * ThreatWeight * threatMult;

            bool better =
                candidateScore > bestScore + 0.01f
                || (Mathf.Abs(candidateScore - bestScore) <= 0.01f && candidateToolNextDist < bestToolNextDist - 0.01f)
                || (Mathf.Abs(candidateScore - bestScore) <= 0.01f && Mathf.Abs(candidateToolNextDist - bestToolNextDist) <= 0.01f && candidateToolMoveCost < bestToolMoveCost)
                || (Mathf.Abs(candidateScore - bestScore) <= 0.01f && Mathf.Abs(candidateToolNextDist - bestToolNextDist) <= 0.01f && candidateToolMoveCost == bestToolMoveCost && threat < bestThreat);

            if (better)
            {
                bestScore = candidateScore;
                bestToolScore = candidateToolScore;
                bestToolNextDist = candidateToolNextDist;
                bestToolMoveCost = candidateToolMoveCost;
                bestLegacyScore = score;
                bestThreat = threat;
                bestRoadBonus = roadBonus;
                bestStep = cell;
            }
        }

        if (bestStep != fromCell && bestToolScore != int.MinValue)
        {
            Debug.Log($"{TL("Repair")} {unit.InstanceId} tool-progress repair via {bestStep} target={target} " +
                      $"tool={bestToolScore} nextDist={bestToolNextDist:F1} moveCost={bestToolMoveCost} roadBonus={bestRoadBonus} final={bestScore:F0} legacy={bestLegacyScore:F0}");
        }

        return bestStep;
    }


    private bool TryDecideRepairFallbackToHQ(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (snapshot == null || snapshot.MyHQ == null || paths == null || paths.Count == 0)
            return false;

        TeamId aiTeam = snapshot.AITeam;
        Vector3Int hqCell = snapshot.MyHQ.CurrentCellPosition;
        hqCell.z = 0;

        bool safeNearHQ = SectorManager.HexDistance(fromCell, hqCell) <= DefenseEnemyRange
            && !HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange)
            && !HasNearbyVisibleEnemy(hqCell, aiTeam, DefenseEnemyRange);
        if (safeNearHQ)
        {
            Debug.Log($"{TL("Repair")} {unit.InstanceId} aguarda nos arredores do HQ {hqCell} (sem ameaça)");
            action = BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
            return true;
        }

        Vector3Int bestStep = fromCell;
        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied.Contains(cell)) continue;

            float dist = SectorManager.HexDistance(cell, hqCell);
            float threat = CalculateThreatLevel(cell, aiTeam);
            float hqAdjacencyBonus = SectorManager.HexDistance(cell, hqCell) <= DefenseEnemyRange ? 25f : 0f;
            float score = -dist * 100f - threat * ThreatWeight + hqAdjacencyBonus;
            if (score > bestScore)
            {
                bestScore = score;
                bestStep = cell;
            }
        }

        Debug.Log($"{TL("Repair")} {unit.InstanceId} sem destino de reparo — retorna ao HQ {hqCell} via {bestStep}");
        action = BuildMoveBatch(unit, aiTeam, fromCell, bestStep, paths);
        return true;
    }


    private ConstructionManager FindRepairConstruction(UnitManager unit, Vector3Int fromCell, TeamId aiTeam, HashSet<Vector3Int> occupied)
    {
        ConstructionManager best = null;
        float bestScore = float.MinValue;
        bool preferAircraftFacility = unit != null && unit.GetAircraftType() != AircraftType.None;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            Vector3Int cc = c.CurrentCellPosition; cc.z = 0;
            float dist = SectorManager.HexDistance(fromCell, cc);
            bool isHomeRepair = IsRepairHomeConstruction(c, aiTeam);
            bool isAircraftFacility = IsAircraftRepairConstruction(c);
            if (c.TeamId != aiTeam)
            {
                Debug.Log($"[Repair] skip {cc} team={c.TeamId} (need {aiTeam}) dist={dist:F1}");
                continue;
            }
            if (c.CurrentCapturePoints < c.CapturePointsMax)
            {
                Debug.Log($"[Repair] skip {cc} cap={c.CurrentCapturePoints}/{c.CapturePointsMax} (incompleto) dist={dist:F1}");
                continue;
            }
            if (!isHomeRepair && !IsRepairConstructionSectorSafe(c, aiTeam))
            {
                Debug.Log($"[Repair] skip {cc} setor inseguro sector={c.Sector} dist={dist:F1}");
                continue;
            }
            bool occupiedCell = occupied.Contains(cc) && cc != fromCell;
            if (occupiedCell && !isHomeRepair)
            {
                Debug.Log($"[Repair] skip {cc} ocupado dist={dist:F1}");
                continue;
            }
            if (occupiedCell && isHomeRepair)
                Debug.Log($"[Repair] home {cc} ocupado, mantendo como fallback de reparo dist={dist:F1}");

            bool safe = !HasNearbyVisibleEnemy(cc, aiTeam, DefenseEnemyRange);
            if (!safe && !isHomeRepair)
            {
                Debug.Log($"[Repair] skip {cc} unsafe (não-home) dist={dist:F1}");
                continue;
            }
            float score = -dist * 100f;
            if (safe) score += 500f;
            if (isHomeRepair) score += 25f;
            float aircraftCohesion = 0f;
            if (preferAircraftFacility)
            {
                aircraftCohesion = CalculateAircraftRepairCohesionScore(unit, cc, aiTeam);
                if (isAircraftFacility) score += 20000f + aircraftCohesion;
                else if (isHomeRepair) score -= 1000f;
            }
            if (occupiedCell && isHomeRepair) score -= 10000f;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }
        if (best != null)
        {
            Vector3Int bc = best.CurrentCellPosition; bc.z = 0;
            string home = IsRepairHomeConstruction(best, aiTeam) ? " home" : string.Empty;
            string aircraft = IsAircraftRepairConstruction(best) ? " airport" : string.Empty;
            string safe = !HasNearbyVisibleEnemy(bc, aiTeam, DefenseEnemyRange) ? " safe" : string.Empty;
            string cohesion = unit != null && unit.GetAircraftType() != AircraftType.None
                ? $" coh={CalculateAircraftRepairCohesionScore(unit, bc, aiTeam):F0}"
                : string.Empty;
            Debug.Log($"[Repair] destino{home}{aircraft}{safe} selecionado {bc} dist={SectorManager.HexDistance(fromCell, bc):F1}{cohesion} score={bestScore:F0}");
        }
        return best;
    }


    private float CalculateAircraftRepairCohesionScore(UnitManager unit, Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;

        int closeAllies = 0;
        int nearbyAllies = 0;
        float nearest = float.MaxValue;
        float weighted = 0f;

        foreach (UnitManager ally in UnitManager.AllActive)
        {
            if (ally == null || ally == unit || ally.TeamId != aiTeam)
                continue;
            if (ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(cell, allyCell);
            nearest = Mathf.Min(nearest, dist);

            if (dist <= 4f)
            {
                closeAllies++;
                weighted += (5f - dist) * 110f;
            }
            else if (dist <= 8f)
            {
                nearbyAllies++;
                weighted += (9f - dist) * 30f;
            }
        }

        if (nearest == float.MaxValue)
            return -1800f;

        float isolationPenalty = nearest > 8f ? -2200f
            : nearest > 6f ? -1200f
            : nearest > 4f ? -450f
            : 0f;

        float closeBonus = Mathf.Min(closeAllies, 5) * 260f;
        float nearbyBonus = Mathf.Min(nearbyAllies, 6) * 80f;
        return weighted + closeBonus + nearbyBonus + isolationPenalty;
    }


    private static bool IsAircraftRepairConstruction(ConstructionManager construction)
    {
        return construction != null
            && construction.TryResolveConstructionData(out ConstructionData data)
            && data != null
            && data.allowAircraftTakeoffAndLanding;
    }


    private static bool IsRepairConstructionSectorSafe(ConstructionManager construction, TeamId aiTeam)
    {
        if (construction == null || construction.TeamId != aiTeam)
            return false;

        if (IsRepairHomeConstruction(construction, aiTeam))
            return true;

        if (!TryGetAnySectorInfo(construction.Sector, out SectorManager.SectorInfo info) || info == null)
            return false;

        return info.IsFullyControlled
            && !info.IsDisputed
            && !info.HasPartialCapture
            && info.ControllingTeam == aiTeam;
    }


    private static bool IsRepairHomeConstruction(ConstructionManager construction, TeamId aiTeam)
    {
        return construction != null
            && construction.TeamId == aiTeam
            && (construction.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(construction.Sector));
    }

    // Like FindRepairConstruction but rejects any construction (including home/base) if enemies
    // are nearby — artillery should never be dropped in an actively invaded sector.

    private ConstructionManager FindNearestHomeConstruction(Vector3Int fromCell, TeamId aiTeam)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.TeamId != aiTeam || !IsRepairHomeConstruction(c, aiTeam)) continue;
            Vector3Int cc = c.CurrentCellPosition; cc.z = 0;
            float dist = SectorManager.HexDistance(fromCell, cc);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }
        return best;
    }

}
