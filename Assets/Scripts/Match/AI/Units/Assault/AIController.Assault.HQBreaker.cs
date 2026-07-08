using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction DecideRogueAssaultBreakerAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        if (TryFindAssaultCaptureTargetVacateAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction targetVacateAction))
            return targetVacateAction;

        if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction vacateAction))
            return vacateAction;

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (TryFindAssaultBreakerAttack(unit, snapshot, fromCell, paths, occupied, enemies,
                out Vector3Int attackCell, out UnitManager attackTarget, out string attackReason))
        {
            Vector3Int targetCell = attackTarget.CurrentCellPosition; targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} breaker — ataca via {attackCell} → {attackTarget.UnitDisplayName}#{attackTarget.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                attackTarget.InstanceId.ToString(), targetCell, paths);
        }

        bool releaseRally = ShouldReleaseRogueAssaultFromRally(unit, snapshot, fromCell, out string releaseRallyReason);
        string rallyReason = "";
        if (!releaseRally
            && TryBuildNearbyHeldRallyObjective(snapshot.AITeam, fromCell, plan, snapshot.TurnNumber, out SectorObjective rallyObjective, out rallyReason))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rogue segura rally {rallyObjective.Sector}: {rallyReason}");
            return DecideRallyAssemblyAssaultAction(unit, snapshot, rallyObjective);
        }
        if (releaseRally)
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rogue libera rally: {releaseRallyReason}");
        if (!string.IsNullOrEmpty(rallyReason))
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rogue rally scan: {rallyReason}");

        // Alvo do rogue: SEM invasao ativa no plano, ele nao marcha sozinho pro QG inimigo (burro,
        // suicida). Em vez disso reforca o rally do PROPRIO eixo (fallback: eixo mais faminto),
        // concentrando massa numa das frentes. So pressiona o QG quando a invasao ja foi alocada
        // (aí faz sentido juntar-se ao ataque).
        Vector3Int pressureTarget;
        if (TryResolveRogueAssaultRallyTarget(unit, plan, snapshot, out Vector3Int rogueRallyCell, out string rogueRallyReason))
        {
            pressureTarget = rogueRallyCell;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rogue -> rally ({rogueRallyReason})");
        }
        else
        {
            pressureTarget = ResolveAssaultPressureTarget(snapshot, enemies, fromCell);
        }
        Vector3Int bestMove = FindAssaultPressureMove(unit, snapshot, fromCell, pressureTarget, paths, occupied, out string pressureReason);
        if (bestMove != fromCell)
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} breaker — pressiona via {bestMove} alvo={pressureTarget} ({pressureReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestMove, paths);
        }

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} breaker — mantém posição");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    // Alvo de "reforco" do rogue assault quando nao ha invasao ativa: o rally do PROPRIO eixo
    // (esprit de corps — fica na sua faixa), com fallback no eixo mais FAMINTO (menos presenca).
    // Deterministico (sem RNG) para nao quebrar a reprodutibilidade do save/load. Retorna false
    // quando a invasao ja esta no plano (aí o rogue pode pressionar o QG e juntar-se ao ataque).
    private bool TryResolveRogueAssaultRallyTarget(UnitManager unit, TeamObjectivePlan plan, AIWorldSnapshot snapshot,
        out Vector3Int rallyCell, out string reason)
    {
        rallyCell = Vector3Int.zero;
        reason = "";
        if (currentAxisMap == null || currentAxisMap.AxisCount == 0)
            return false;
        if (PlanHasActiveEnemyBaseObjective(plan, snapshot.AITeam))
            return false; // invasao ativa: deixa pressionar o QG

        int eixo = unit.AIEixo;
        if (eixo > 0 && currentAxisMap.TryGetAxis(eixo, out InvasionAxisMap.Axis own))
        {
            rallyCell = own.RallyCell; rallyCell.z = 0;
            reason = $"reforca rally do eixo {eixo}";
            return true;
        }

        InvasionAxisMap.Axis hungriest = null;
        int bestPresence = int.MaxValue;
        foreach (InvasionAxisMap.Axis a in currentAxisMap.Axes)
        {
            int p = GetEixoPresence(a.EixoIndex);
            if (p < bestPresence) { bestPresence = p; hungriest = a; }
        }
        if (hungriest != null)
        {
            rallyCell = hungriest.RallyCell; rallyCell.z = 0;
            reason = $"reforca rally do eixo faminto {hungriest.EixoIndex} (presenca={bestPresence})";
            return true;
        }
        return false;
    }

    private bool PlanHasActiveEnemyBaseObjective(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan?.Objectives == null) return false;
        foreach (SectorObjective o in plan.Objectives)
            if (o != null
                && ConstructionSectorHelper.IsBase(o.Sector)
                && FindHQTeamInSector(o.Sector) != aiTeam
                && o.Status != ObjectiveStatus.Complete
                && o.Status != ObjectiveStatus.Abandoned)
                return true;
        return false;
    }


    private bool TryFindAssaultBreakerAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        List<UnitManager> enemies,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        out string reason)
    {
        bestCell = fromCell;
        bestTarget = null;
        reason = "";
        if (enemies == null || enemies.Count == 0)
            return false;

        bool preferDpq = unit.TryGetUnitData(out UnitData attackerUd) && attackerUd != null && attackerUd.prioritizeDpqAtBattle;

        Vector3Int enemyHqCell = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : fromCell;
        enemyHqCell.z = 0;

        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied.Contains(cell)) continue;

            foreach (UnitManager enemy in enemies)
            {
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, cell, false, out string attackDecisionReason))
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition; enemyCell.z = 0;
                ConstructionManager enemyBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, enemyCell);
                bool inOwnConstruction = enemyBldg != null && enemyBldg.TeamId == snapshot.AITeam;
                bool inConstruction = enemyBldg != null;
                // Enemy on OUR building (capturing it) is far more urgent than any other position.
                float constructionBonus = inOwnConstruction ? 20000f : inConstruction ? 5000f : 0f;
                float enemyHqDist = SectorManager.HexDistance(enemyCell, enemyHqCell);
                float cellHqDist = SectorManager.HexDistance(cell, enemyHqCell);
                float dpq = GetTerrainDpqPontos(cell);
                BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, enemy);
                float targetPreferenceScore = GetAssaultTargetPreferenceScore(targetPreference);
                bool hasSim = TrySimulateAttackForAI(unit, enemy, cell, out AIAttackSimulationSummary simSummary);
                if (hasSim && simSummary.targetDamage <= 0)
                    continue;

                float combatScore = 0f;
                string simDetails = "sim=unavailable";
                if (hasSim)
                {
                    combatScore =
                        simSummary.targetDamagePct * 700f
                        + simSummary.targetDamage * 130f
                        - simSummary.attackerLossPct * 620f
                        - simSummary.attackerLoss * 100f
                        + (simSummary.result.killGuaranteed ? 22000f : 0f)
                        + (simSummary.result.attackerSurvives ? 2500f : -10000f);

                    if (simSummary.attackerLossPct >= 75 && !simSummary.result.killGuaranteed && !inOwnConstruction)
                        combatScore -= 14000f;

                    PositionDpqForAttackDecision attackerDpq = ResolveDpqForAttackDecision(unit, cell);
                    PositionDpqForAttackDecision defenderDpq = ResolveDpqForAttackDecision(enemy, enemyCell);
                    simDetails = $"sim dmg={simSummary.targetDamagePct}% loss={simSummary.attackerLossPct}% hp={simSummary.attackerHpBefore}->{simSummary.result.attackerHpAfter} target={simSummary.targetHpBefore}->{simSummary.result.defenderHpAfter} dpq={attackerDpq.points}/{defenderDpq.points} def={attackerDpq.defenseBonus}/{defenderDpq.defenseBonus} kill={simSummary.result.killGuaranteed} survive={simSummary.result.attackerSurvives}";
                }
                // enemyHqDist penalises enemies far from their HQ (advancing enemies).
                // If they are on OUR building, distance to their HQ is irrelevant — skip the penalty.
                float score =
                    combatScore
                    + targetPreferenceScore * 0.25f
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 95f
                    + constructionBonus
                    - (inOwnConstruction ? 0f : enemyHqDist * 45f)
                    - cellHqDist * 20f
                    + dpq * (preferDpq ? 420f : 55f)
                    - GetPathStepCount(paths, cell) * 5f
                    - enemy.InstanceId * 0.001f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = enemy;
                    reason = $"score={score:F0} pref={targetPreference} hp={enemy.CurrentHP} bldg={inConstruction} ownBldg={inOwnConstruction} enemyHqDist={enemyHqDist:F1} dpqCell={dpq:F1} preferDpq={preferDpq} {simDetails} {attackDecisionReason}";
                }
            }
        }

        return bestTarget != null;
    }


    private Vector3Int ResolveAssaultPressureTarget(AIWorldSnapshot snapshot, List<UnitManager> enemies, Vector3Int fromCell)
    {
        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition; hq.z = 0;
            return hq;
        }

        if (snapshot.EnemyBuildings != null && snapshot.EnemyBuildings.Count > 0)
        {
            ConstructionManager closest = null;
            float bestD = float.MaxValue;
            foreach (ConstructionManager eb in snapshot.EnemyBuildings)
            {
                Vector3Int ec = eb.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(fromCell, ec);
                if (d < bestD) { bestD = d; closest = eb; }
            }
            if (closest != null)
            {
                Vector3Int ec = closest.CurrentCellPosition; ec.z = 0;
                return ec;
            }
        }

        if (enemies != null && enemies.Count > 0)
        {
            UnitManager best = null;
            float bestDist = float.MaxValue;
            foreach (UnitManager enemy in enemies)
            {
                Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(fromCell, ec);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = enemy;
                }
            }
            if (best != null)
            {
                Vector3Int bc = best.CurrentCellPosition; bc.z = 0;
                return bc;
            }
        }

        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition; hq.z = 0;
            return hq;
        }

        // Fallback: edifício inimigo mais próximo (sem filtro FoW)
        if (snapshot.EnemyBuildings != null && snapshot.EnemyBuildings.Count > 0)
        {
            ConstructionManager closest = null;
            float bestD = float.MaxValue;
            foreach (ConstructionManager eb in snapshot.EnemyBuildings)
            {
                Vector3Int ec = eb.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(fromCell, ec);
                if (d < bestD) { bestD = d; closest = eb; }
            }
            if (closest != null)
            {
                Vector3Int ec = closest.CurrentCellPosition; ec.z = 0;
                return ec;
            }
        }

        return fromCell;
    }


    private Vector3Int FindAssaultPressureMove(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int pressureTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out string reason)
    {
        using var perf = new AIDecisionPerfScope(unit, "assaultPressureMove");
        reason = "sem progresso";
        float fromDist = SectorManager.HexDistance(fromCell, pressureTarget);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, pressureTarget, out float fromRouteDist);

        Vector3Int bestCell = fromCell;
        Vector3Int bestFallbackCell = fromCell;
        float bestProgress = float.MinValue;
        float bestLine = float.MinValue;
        int bestPathCost = int.MinValue;
        float bestThreat = float.MaxValue;
        float bestDpq = float.MinValue;
        float bestFallbackProgress = float.MinValue;
        float bestFallbackLine = float.MinValue;
        int bestFallbackPathCost = int.MinValue;
        float bestFallbackThreat = float.MaxValue;
        float bestFallbackDpq = float.MinValue;
        bool foundMove = false;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (cell != fromCell && occupied.Contains(cell)) continue;

            float dist = SectorManager.HexDistance(cell, pressureTarget);
            bool cellRouteFound = TryCalculateRouteDistance(unit, cell, pressureTarget, out float routeDist);
            float dpq;
            using (new AIDecisionPerfScope(unit, "assaultPressureDpq"))
                dpq = GetTerrainDpqPontos(cell);
            float threat;
            using (new AIDecisionPerfScope(unit, "assaultPressureThreat"))
                threat = CalculateThreatLevel(cell, snapshot.AITeam);
            // Bonus forte para células que avançam; penalidade leve para as que regridem
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - routeDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            float progress = recoversMissingRoute
                ? -routeDist
                : (fromRouteFound && cellRouteFound) ? routeProgress : fromDist - dist;
            float line = CalculateLineProgressTieBreak(fromCell, pressureTarget, cell);
            int pathCost = GetPathStepCount(paths, cell);

            if (IsBetterAssaultPressureMove(progress, line, pathCost, threat, dpq,
                    bestFallbackProgress, bestFallbackLine, bestFallbackPathCost, bestFallbackThreat, bestFallbackDpq))
            {
                bestFallbackProgress = progress;
                bestFallbackLine = line;
                bestFallbackPathCost = pathCost;
                bestFallbackThreat = threat;
                bestFallbackDpq = dpq;
                bestFallbackCell = cell;
            }

            bool movesCloser = recoversMissingRoute
                || routeProgress > 0f
                || (!fromRouteFound && !cellRouteFound && dist <= fromDist);
            if (!movesCloser) continue;

            if (IsBetterAssaultPressureMove(progress, line, pathCost, threat, dpq,
                    bestProgress, bestLine, bestPathCost, bestThreat, bestDpq))
            {
                bestProgress = progress;
                bestLine = line;
                bestPathCost = pathCost;
                bestThreat = threat;
                bestDpq = dpq;
                bestCell = cell;
                foundMove = true;
            }
        }

        Vector3Int fallback = foundMove ? bestCell : bestFallbackCell;
        if (fallback != fromCell)
        {
            reason = foundMove
                ? $"fallback progress={bestProgress:F1} line={bestLine:F1} path={bestPathCost} threat={bestThreat:F1}"
                : $"fallbackAny progress={bestFallbackProgress:F1} line={bestFallbackLine:F1} path={bestFallbackPathCost} threat={bestFallbackThreat:F1}";
        }

        return fallback;
    }


    private static bool IsBetterAssaultPressureMove(
        float candidateProgress,
        float candidateLine,
        int candidatePathCost,
        float candidateThreat,
        float candidateDpq,
        float bestProgress,
        float bestLine,
        int bestPathCost,
        float bestThreat,
        float bestDpq)
    {
        const float epsilon = 0.001f;
        if (candidateProgress > bestProgress + epsilon) return true;
        if (candidateProgress < bestProgress - epsilon) return false;

        if (candidateLine > bestLine + epsilon) return true;
        if (candidateLine < bestLine - epsilon) return false;

        if (candidatePathCost > bestPathCost) return true;
        if (candidatePathCost < bestPathCost) return false;

        if (candidateThreat < bestThreat - epsilon) return true;
        if (candidateThreat > bestThreat + epsilon) return false;

        return candidateDpq > bestDpq + epsilon;
    }


}
