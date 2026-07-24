using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const float ExplorerForwardObserverProgressWeight = 1200f;
    private const float ExplorerForwardObserverDpqWeight = 35f;
    private const float ExplorerForwardObserverThreatWeight = 90f;
    private const float ExplorerForwardObserverRecommendedBonus = 3500f;
    private const float ExplorerForwardObserverRecommendedDriftPenalty = 900f;
    private const int ExplorerForwardObserverTargetRadius = 3;

    // -------------------------------------------------------------------------
    // Capturador Explorador - revela alvo oculto por FoW
    // -------------------------------------------------------------------------

    private bool TryDecideCapturerExplorer(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int recommendedAdvanceCell,
        bool hasRecommendedAdvanceCell,
        out PlayerAction action)
    {
        using var perf = new AIDecisionPerfScope(unit, "explorer");
        action = null;
        // Explorador: ocupante invisível no alvo → DPQ mais elevado + ataque lateral oportunista
        {
            bool hiddenObjectiveOccupant = false;
            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(targetCell);
            if (occupant != null && occupant.SlotIndex != snapshot.AISlotIndex)
            {
                MatchController mc = GetMatchController();
                if (mc == null || !mc.IsUnitVisibleForTeam(occupant, snapshot.AITeam))
                {
                    hiddenObjectiveOccupant = true;
                    if (TryFindBestForwardObserverSpot(
                        unit,
                        snapshot,
                        assigned,
                        fromCell,
                        targetCell,
                        paths,
                        occupied,
                        recommendedAdvanceCell,
                        hasRecommendedAdvanceCell,
                        occupant,
                        requireImmediateReveal: true,
                        out ConstructionManager revealSpot,
                        out Vector3Int revealObserverCell,
                        out string revealObserverDebug))
                    {
                        assigned.Status = ObjectiveStatus.Pursuing;

                        if (revealObserverCell == fromCell)
                        {
                            Debug.Log($"{TL("Explorador")} {unit.InstanceId} segura observador avancado {revealSpot.ConstructionDisplayName} para revelar {assigned.Sector} @ {revealObserverCell}");
                            action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
                            return true;
                        }

                        Vector3Int revealSpotCell = revealSpot.CurrentCellPosition;
                        revealSpotCell.z = 0;
                        string revealVerb = revealObserverCell == revealSpotCell ? "ocupa" : "aproxima";
                        string recommendedText = hasRecommendedAdvanceCell ? $" recomendado={recommendedAdvanceCell}" : string.Empty;
                        Debug.Log($"{TL("Explorador")} {unit.InstanceId} {revealVerb} observador avancado {revealSpot.ConstructionDisplayName} para revelar {assigned.Sector} via {revealObserverCell}{recommendedText}");
                        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, revealObserverCell, paths);
                        return true;
                    }

                    Debug.Log($"{TL("Explorador")} {unit.InstanceId} sem observador avancado para revelar {assigned.Sector}: {revealObserverDebug}");

                    if (TryFindBestLoSCell(unit, paths, occupied, targetCell, out Vector3Int dpqCell))
                    {
                        assigned.Status = ObjectiveStatus.Pursuing;

                        SensorMovementMode dpqMode = dpqCell != fromCell
                            ? SensorMovementMode.MoveuAndando
                            : SensorMovementMode.MoveuParado;
                        var dpqTargets = new List<PodeMirarTargetOption>();
                        if (PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                                dpqMode, dpqTargets, fromCell: dpqCell) && dpqTargets.Count > 0)
                        {
                            UnitManager lateralTarget = null; float lateralPri = float.MinValue;
                            foreach (PodeMirarTargetOption opt in dpqTargets)
                            {
                                if (opt?.targetUnit == null) continue;
                                if (!PassesAttackDecision(unit, opt.targetUnit, dpqCell, assigned.Status == ObjectiveStatus.Defending, out _)) continue;
                                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                                if (SectorManager.HexDistance(tc, targetCell) > DefenseEnemyRange + 1) continue;
                                float p = AttackTargetPriorityPursuer(tc, targetCell);
                                if (p > lateralPri) { lateralPri = p; lateralTarget = opt.targetUnit; }
                            }
                            if (lateralTarget != null)
                            {
                                Vector3Int ltCell = lateralTarget.CurrentCellPosition; ltCell.z = 0;
                                Debug.Log($"{TL("Explorador")} {unit.InstanceId} DPQ {assigned.Sector} via {dpqCell} + ataque lateral → {lateralTarget.UnitDisplayName}#{lateralTarget.InstanceId}");
                                action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, dpqCell,
                                    lateralTarget.InstanceId.ToString(), ltCell, paths);
                                return true;
                            }
                        }

                        Debug.Log($"{TL("Explorador")} {unit.InstanceId} DPQ para revelar {assigned.Sector} via {dpqCell} (ev={GetTerrainEv(dpqCell):F0})");
                        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, dpqCell, paths);
                        return true;
                    }
                }
            }

            if (hiddenObjectiveOccupant)
                return false;
        }

        if (hasRecommendedAdvanceCell && IsObjectiveCellVisibleForExplorer(snapshot.AITeam, targetCell))
        {
            Debug.Log($"{TL("Explorador")} {unit.InstanceId} ignora observador avancado em {assigned.Sector}: objetivo visivel e avancavel via {recommendedAdvanceCell}");
            return false;
        }

        if (HasCapturerCombatOpportunityNearObjective(
            unit,
            snapshot,
            assigned,
            fromCell,
            targetCell,
            paths,
            occupied))
        {
            Debug.Log($"{TL("Explorador")} {unit.InstanceId} adia observador avancado em {assigned.Sector}: combate visivel disponivel perto do alvo");
            return false;
        }

        if (TryFindBestForwardObserverSpot(
            unit,
            snapshot,
            assigned,
            fromCell,
            targetCell,
            paths,
            occupied,
            recommendedAdvanceCell,
            hasRecommendedAdvanceCell,
            targetOccupant: null,
            requireImmediateReveal: false,
            out ConstructionManager observerSpot,
            out Vector3Int observerCell,
            out _))
        {
            assigned.Status = ObjectiveStatus.Pursuing;

            if (observerCell == fromCell)
            {
                Debug.Log($"{TL("Explorador")} {unit.InstanceId} segura observador avancado {observerSpot.ConstructionDisplayName} em {assigned.Sector} @ {observerCell}");
                action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
                return true;
            }

            Vector3Int spotCell = observerSpot.CurrentCellPosition;
            spotCell.z = 0;
            string verb = observerCell == spotCell ? "ocupa" : "aproxima";
            string recommendedText = hasRecommendedAdvanceCell ? $" recomendado={recommendedAdvanceCell}" : string.Empty;
            Debug.Log($"{TL("Explorador")} {unit.InstanceId} {verb} observador avancado {observerSpot.ConstructionDisplayName} em {assigned.Sector} via {observerCell}{recommendedText}");
            action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, observerCell, paths);
            return true;
        }

        return false;
    }

    private bool IsObjectiveCellVisibleForExplorer(TeamId aiTeam, Vector3Int targetCell)
    {
        MatchController mc = GetMatchController();
        if (mc == null)
            return true;

        targetCell.z = 0;
        return mc.IsCellVisibleForActiveTeam(targetCell);
    }

    private bool TryFindBestForwardObserverSpot(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int recommendedAdvanceCell,
        bool hasRecommendedAdvanceCell,
        UnitManager targetOccupant,
        bool requireImmediateReveal,
        out ConstructionManager bestSpot,
        out Vector3Int bestCell,
        out string debugSummary)
    {
        bestSpot = null;
        bestCell = fromCell;
        debugSummary = "sem dados";

        if (unit == null || assigned == null || paths == null || paths.Count == 0)
            return false;

        float bestScore = float.MinValue;
        int inspectedSpots = 0;
        int nearSpots = 0;
        int reachableCandidates = 0;
        int occupiedCandidates = 0;
        int noProgressCandidates = 0;
        int outOfObserverRangeCandidates = 0;
        int observerRange = GetForwardObserverRevealRange(unit, targetOccupant);

        foreach (ConstructionManager construction in EnumerateForwardObserverSpots())
        {
            if (construction == null || !construction.IsForwardObserverSpot)
                continue;

            inspectedSpots++;
            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;

            float distToTarget = SectorManager.HexDistance(cell, targetCell);
            if (distToTarget > ExplorerForwardObserverTargetRadius)
                continue;

            nearSpots++;
            float fromSpotDist = SectorManager.HexDistance(fromCell, cell);
            bool alreadyHoldingSpot = cell == fromCell;

            foreach (Vector3Int candidate in paths.Keys)
            {
                if (candidate != fromCell && occupied != null && occupied.Contains(candidate))
                {
                    occupiedCandidates++;
                    continue;
                }

                float candidateSpotDist = SectorManager.HexDistance(candidate, cell);
                float progressToSpot = fromSpotDist - candidateSpotDist;
                bool reachesSpot = candidate == cell;
                if (reachesSpot && candidate != fromCell && occupied != null && occupied.Contains(candidate))
                {
                    occupiedCandidates++;
                    continue;
                }
                if (progressToSpot <= 0f && !reachesSpot && !alreadyHoldingSpot)
                {
                    noProgressCandidates++;
                    continue;
                }

                reachableCandidates++;
                float candidateTargetDist = SectorManager.HexDistance(candidate, targetCell);
                if (requireImmediateReveal && candidateTargetDist > observerRange)
                {
                    outOfObserverRangeCandidates++;
                    continue;
                }

                float threat = CalculateThreatLevel(candidate, snapshot.AITeam);
                float moveCost = paths.TryGetValue(candidate, out List<Vector3Int> path) && path != null ? path.Count : 0f;
                float recommendedBonus = 0f;
                if (hasRecommendedAdvanceCell)
                {
                    recommendedBonus = candidate == recommendedAdvanceCell
                        ? ExplorerForwardObserverRecommendedBonus
                        : -SectorManager.HexDistance(candidate, recommendedAdvanceCell) * ExplorerForwardObserverRecommendedDriftPenalty;
                }

                float score = progressToSpot * ExplorerForwardObserverProgressWeight
                    + Mathf.Max(0f, ExplorerForwardObserverTargetRadius - distToTarget) * 200f
                    + GetTerrainDpqPontos(candidate) * ExplorerForwardObserverDpqWeight
                    + recommendedBonus
                    - candidateTargetDist * 20f
                    - threat * ExplorerForwardObserverThreatWeight
                    - moveCost;

                if (reachesSpot)
                    score += 5000f;
                if (alreadyHoldingSpot)
                    score += 500f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSpot = construction;
                    bestCell = candidate;
                }
            }
        }

        debugSummary = $"spots={inspectedSpots} perto={nearSpots} candidatos={reachableCandidates} ocupados={occupiedCandidates} semAvanco={noProgressCandidates} foraAlcanceObs={outOfObserverRangeCandidates} alvo={targetCell} raio={ExplorerForwardObserverTargetRadius} alcanceObs={observerRange}";
        return bestSpot != null;
    }

    private bool HasCapturerCombatOpportunityNearObjective(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (unit == null || snapshot == null || assigned == null || paths == null || paths.Count == 0)
            return false;

        float fromDist = SectorManager.HexDistance(fromCell, targetCell);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, targetCell, out float fromRouteDist);
        bool preferDpqAtBattle = unit.TryGetUnitData(out UnitData dpqUd) && dpqUd.prioritizeDpqAtBattle;
        bool defensiveContext = assigned.Status == ObjectiveStatus.Defending;
        MatchController mc = GetMatchController();

        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.SlotIndex == snapshot.AISlotIndex || enemy.IsDead || enemy.IsEmbarked)
                continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, snapshot.AITeam))
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (SectorManager.HexDistance(enemyCell, targetCell) > fromDist)
                continue;

            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (occupied != null && occupied.Contains(cell))
                    continue;

                float cellDist = SectorManager.HexDistance(cell, targetCell);
                bool cellRouteFound = TryCalculateRouteDistance(unit, cell, targetCell, out float cellRouteDist);
                float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - cellRouteDist : 0f;
                bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
                bool advancesByRoute = recoversMissingRoute || routeProgress > 0f;
                bool advancesByHex = !fromRouteFound && !cellRouteFound && cellDist < fromDist;
                bool eligibleForAttack = advancesByRoute || advancesByHex || preferDpqAtBattle;
                if (!eligibleForAttack)
                    continue;

                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy))
                    continue;
                if (!PassesAttackDecision(unit, enemy, cell, defensiveContext, out _))
                    continue;

                return true;
            }
        }

        return false;
    }

    private static int GetForwardObserverRevealRange(UnitManager observer, UnitManager target)
    {
        if (observer != null && observer.TryGetUnitData(out UnitData data) && data != null)
        {
            if (target != null)
                return Mathf.Max(1, data.ResolveVisionFor(target.GetDomain(), target.GetHeightLevel()));

            return Mathf.Max(1, data.ResolveVisionFor(observer.GetDomain(), observer.GetHeightLevel()));
        }

        if (observer != null)
            return Mathf.Max(1, observer.Visao);

        return 3;
    }

    private IEnumerable<ConstructionManager> EnumerateForwardObserverSpots()
    {
        HashSet<ConstructionManager> seen = new HashSet<ConstructionManager>();

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || !seen.Add(construction))
                continue;
            yield return construction;
        }

        ConstructionManager[] sceneConstructions =
            UnityEngine.Object.FindObjectsByType<ConstructionManager>(
                FindObjectsInactive.Include);

        for (int i = 0; i < sceneConstructions.Length; i++)
        {
            ConstructionManager construction = sceneConstructions[i];
            if (construction == null || !seen.Add(construction))
                continue;
            yield return construction;
        }
    }

    private bool HasForwardObserverApproachForHiddenObjective(
        UnitManager unit,
        TeamObjectivePlan plan,
        TeamId aiTeam)
    {
        if (unit == null || plan == null || unit.HasActed || unit.IsDead || unit.IsEmbarked)
            return false;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || !UnitRoleCompatibility.CanSatisfy(data, UnitRole.Capturador)
            || data.unitClass != GameUnitClass.Infantry)
            return false;

        SectorObjective assigned = ResolveAnyAssignedObjective(unit, plan);
        if (assigned == null || assigned.Status == ObjectiveStatus.Defending)
            return false;

        ConstructionManager target = FindCapturableInSector(assigned.Sector, aiTeam);
        if (target == null)
            return false;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;

        MatchController mc = GetMatchController();
        if (mc != null && mc.IsCellVisibleForActiveTeam(targetCell))
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || !construction.IsForwardObserverSpot)
                continue;

            Vector3Int spotCell = construction.CurrentCellPosition;
            spotCell.z = 0;
            if (SectorManager.HexDistance(spotCell, targetCell) > ExplorerForwardObserverTargetRadius)
                continue;

            float fromSpotDist = SectorManager.HexDistance(fromCell, spotCell);
            foreach (Vector3Int candidate in paths.Keys)
            {
                if (candidate != fromCell && occupied.Contains(candidate))
                    continue;

                if (candidate == spotCell)
                    return true;

                if (SectorManager.HexDistance(candidate, spotCell) < fromSpotDist)
                    return true;
            }
        }

        return false;
    }
}
