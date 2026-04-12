using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIPlayerController
{
    private UnitManager AssignTargetForUnit(UnitManager unit, AISnapshot snapshot, List<int> allUnitIds, int unitIndex)
    {
        if (snapshot.VisibleEnemies.Count == 0)
            return null;

        Vector3Int friendlyCell = unit.CurrentCellPosition;
        friendlyCell.z = 0;
        bool defendMode = currentStance == AIStance.Defend && snapshot.HasHq && snapshot.BoardTilemap != null;
        Vector3Int defenseReferenceCell = defendMode ? snapshot.HqCell : friendlyCell;
        defenseReferenceCell.z = 0;

        snapshot.UnitRoles.TryGetValue(unit.InstanceId, out AIPlanIntent unitIntent);
        snapshot.UnitPlanAssignments.TryGetValue(unit.InstanceId, out AIPlanAssignment unitAssignment);
        bool escortRoleActive = unitAssignment != null && IsEscortMissionRole(unitAssignment.Role);
        Vector3Int escortPlanCohesionCell = friendlyCell;
        string escortPlanCohesionLabel = string.Empty;
        bool escortPlanCohesionActive = escortRoleActive
            && unitIntent != null
            && TryGetPlanCohesionObjective(unit, snapshot, unitIntent, unitAssignment, out escortPlanCohesionCell, out escortPlanCohesionLabel);

        bool captureRoleFilter = unitAssignment != null
            && unitAssignment.Role == AIPlanRole.Capture
            && unitAssignment.Intent != null
            && unitAssignment.Intent.HasCaptureTarget;
        Vector3Int captureFilterTarget = captureRoleFilter ? unitAssignment.Intent.CaptureTargetCell : default;

        UnitManager best = null;
        int bestTargetPriorityRank = int.MinValue;
        int bestEscortThreatBand = int.MaxValue;
        int bestBand = int.MaxValue;
        int bestScore = int.MinValue;
        int bestTieDistance = int.MaxValue;
        int bestEffectiveAttackDistance = int.MaxValue;

        int moveBudget = Mathf.Max(0, unit.RemainingMovementPoints);
        unit.TryGetUnitData(out UnitData unitDataAssign);
        AIUnitProfile aiProfileAssign = unitDataAssign != null ? unitDataAssign.aiUnitProfile : null;
        AIUnitStanceBehavior stanceBehaviorAssign = aiProfileAssign != null
            ? aiProfileAssign.GetStanceBehavior(currentStance)
            : new AIUnitStanceBehavior();
        bool hasAttackerData = unit.TryGetUnitData(out UnitData attackerData) && attackerData != null;
        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        RPSDatabase rpsDb = turnStateManager != null ? turnStateManager.RpsDatabaseRef : null;
        DPQMatchupDatabase dpqDb = turnStateManager != null ? turnStateManager.DpqMatchupDatabaseRef : null;
        WeaponPriorityData wpDb = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        Dictionary<Vector3Int, List<Vector3Int>> reachablePaths = null;
        HashSet<Vector3Int> occupiedByAllies = null;
        List<int> reachableAttackDistances = new List<int>(16);
        if (snapshot.BoardTilemap != null)
        {
            reachablePaths = UnitMovementPathRules.CalcularCaminhosValidos(
                snapshot.BoardTilemap,
                unit,
                moveBudget,
                terrainDb);
            occupiedByAllies = BuildAllyCellSet(snapshot, unit);
        }

        HashSet<int> stationaryFireTargets = null;
        bool hasAnyStationaryFireTarget = false;
        if (stanceBehaviorAssign.requireSightlineBeforeEngaging || stanceBehaviorAssign.holdPositionWhenInRange)
        {
            stationaryFireTargets = new HashSet<int>();
            for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
            {
                UnitManager enemy = snapshot.VisibleEnemies[i];
                if (enemy == null || enemy.IsDead)
                    continue;
                if (!CanUnitFireAtTargetFromCurrentPosition(unit, enemy))
                    continue;

                stationaryFireTargets.Add(enemy.InstanceId);
            }

            hasAnyStationaryFireTarget = stationaryFireTargets.Count > 0;
        }

        for (int j = 0; j < snapshot.VisibleEnemies.Count; j++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[j];
            if (enemy == null || enemy.IsDead)
                continue;
            if (matchController != null && !IsEnemyVisibleForAiTeam(enemy, snapshot.AiTeam))
            {
                if (aiLog)
                {
                    Debug.Log(
                        $"{T(snapshot.AiTeam, 2)} [score] {unit.name} -> {enemy.name} | " +
                        "descartado: alvo nao visivel no FoW atual");
                }
                continue;
            }

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;

            int escortThreatBand = int.MaxValue;
            string escortThreatReason = string.Empty;
            if (escortPlanCohesionActive)
                escortThreatBand = GetEscortThreatPriority(unit, enemy, snapshot, unitIntent, escortPlanCohesionCell, out escortThreatReason);

            int defenseBand = 0;
            if (defendMode && !IsEnemyWithinDefendRadius(snapshot, enemy))
                defenseBand = 1;

            int tieDistance = GetHexDistance(snapshot.BoardTilemap, defendMode ? defenseReferenceCell : friendlyCell, enemyCell, 64);
            if (tieDistance == int.MaxValue)
                tieDistance = 64;

            bool canReachAndFire = false;
            reachableAttackDistances.Clear();
            bool canFireFromCurrentPosition = stationaryFireTargets != null && stationaryFireTargets.Contains(enemy.InstanceId);
            if (stanceBehaviorAssign.requireSightlineBeforeEngaging)
            {
                canReachAndFire = canFireFromCurrentPosition;
                if (canReachAndFire)
                    reachableAttackDistances.Add(Mathf.Max(1, GetHexDistance(snapshot.BoardTilemap, friendlyCell, enemyCell, 64)));
            }
            else if (stanceBehaviorAssign.holdPositionWhenInRange)
            {
                if (hasAnyStationaryFireTarget && canFireFromCurrentPosition)
                {
                    canReachAndFire = true;
                    reachableAttackDistances.Add(Mathf.Max(1, GetHexDistance(snapshot.BoardTilemap, friendlyCell, enemyCell, 64)));
                }
                else
                {
                    canReachAndFire = TryCollectReachableAttackDistances(
                        snapshot.BoardTilemap,
                        unit,
                        enemy,
                        friendlyCell,
                        enemyCell,
                        reachablePaths,
                        occupiedByAllies,
                        reachableAttackDistances);
                }
            }
            else
            {
                canReachAndFire = TryCollectReachableAttackDistances(
                    snapshot.BoardTilemap,
                    unit,
                    enemy,
                    friendlyCell,
                    enemyCell,
                    reachablePaths,
                    occupiedByAllies,
                    reachableAttackDistances);
            }

            if (!canReachAndFire)
            {
                if (aiLog)
                {
                    Debug.Log(
                        $"{T(snapshot.AiTeam, 2)} [score] {unit.name} -> {enemy.name} | " +
                        "descartado: sem caminho/alcance valido para finalizar com ataque");
                }
                continue;
            }

            if (captureRoleFilter)
            {
                Vector3Int capTarget = captureFilterTarget;
                capTarget.z = 0;
                int distEnemyToCapture = GetHexDistance(snapshot.BoardTilemap, capTarget, enemyCell, 64);
                bool nearCaptureTarget = distEnemyToCapture <= 2;
                bool inLane = IsEnemyInCaptureLane(snapshot.BoardTilemap, friendlyCell, capTarget, enemyCell);
                if (!nearCaptureTarget && !inLane)
                {
                    if (aiLog)
                        Debug.Log($"{T(snapshot.AiTeam, 2)} [score] {unit.name} -> {enemy.name} | descartado: capturador ignorou alvo fora do corredor (distToCapture={distEnemyToCapture})");
                    continue;
                }
            }

            int effectiveAttackDistance = int.MaxValue;
            for (int d = 0; d < reachableAttackDistances.Count; d++)
            {
                int candidateDist = reachableAttackDistances[d];
                if (candidateDist < effectiveAttackDistance)
                    effectiveAttackDistance = candidateDist;
            }

            BazookaTargetPriority targetPriority = BazookaTargetPriority.Tertiary;
            int targetPriorityRank = 0;
            if (hasAttackerData && enemy.TryGetUnitData(out UnitData targetPriorityData) && targetPriorityData != null)
            {
                targetPriority = attackerData.ResolveAiTargetPriorityForTargetClass(targetPriorityData.unitClass);
                targetPriorityRank = ResolveAttackTargetPreferenceRank(stanceBehaviorAssign, targetPriority);
            }

            int score;
            string scoreDetail = "fallback-sem-simulacao";

            if (hasAttackerData && enemy.TryGetUnitData(out UnitData defenderData) && defenderData != null)
            {
                score = EvaluateAttackTargetScore(
                    attackerData,
                    defenderData,
                    unit.CurrentHP,
                    enemy.CurrentHP,
                    tieDistance,
                    reachableAttackDistances,
                    defendMode,
                    rpsDb,
                    dpqDb,
                    wpDb,
                    out scoreDetail);
            }
            else
            {
                score = -tieDistance * 100;
            }

            if (aiLog)
            {
                Debug.Log(
                    $"{T(snapshot.AiTeam, 2)} [score] {unit.name} -> {enemy.name} | " +
                    $"prefMode={stanceBehaviorAssign.targetPreference} pref={targetPriority}/{targetPriorityRank} " +
                    $"{(escortPlanCohesionActive ? $"escortBand={(escortThreatBand == int.MaxValue ? "off-plan" : escortThreatBand.ToString())} " : string.Empty)}" +
                    $"band={defenseBand} dist={tieDistance}->{effectiveAttackDistance} score={score} | {scoreDetail}" +
                    $"{(escortPlanCohesionActive && !string.IsNullOrWhiteSpace(escortThreatReason) ? $" | escort={escortThreatReason}" : string.Empty)}");
            }

            bool invalidScore = score <= (int.MinValue / 8);
            if (invalidScore)
            {
                if (aiLog)
                {
                    Debug.Log(
                        $"{T(snapshot.AiTeam, 2)} [score] {unit.name} -> {enemy.name} | " +
                        "descartado: score invalido de simulacao");
                }
                continue;
            }

            bool better = targetPriorityRank > bestTargetPriorityRank
                || (targetPriorityRank == bestTargetPriorityRank
                    && (escortThreatBand < bestEscortThreatBand
                        || (escortThreatBand == bestEscortThreatBand
                            && (defenseBand < bestBand
                                || (defenseBand == bestBand && IsBetterTargetInBand(defendMode, score, tieDistance, bestScore, bestTieDistance))))));

            if (!better)
                continue;

            bestTargetPriorityRank = targetPriorityRank;
            bestEscortThreatBand = escortThreatBand;
            bestBand = defenseBand;
            bestScore = score;
            bestTieDistance = tieDistance;
            bestEffectiveAttackDistance = effectiveAttackDistance;
            best = enemy;
        }

        if (best != null && captureRoleFilter)
        {
            Vector3Int bestCell = best.CurrentCellPosition;
            bestCell.z = 0;
            Vector3Int filterCell = captureFilterTarget;
            filterCell.z = 0;
            bool isBlockingObjective = bestCell == filterCell;

            if (!isBlockingObjective)
            {
                if (stanceBehaviorAssign.captureInterruptBias == CaptureInterruptBias.None)
                {
                    if (aiLog)
                        Debug.Log($"{T(snapshot.AiTeam, 2)} [score] {unit.name} -> {best.name} | descartado: captureInterruptBias=None");
                    best = null;
                }
                else
                {
                    int minPreMoveBias = stanceBehaviorAssign.captureInterruptBias switch
                    {
                        CaptureInterruptBias.Aggressive => 22000,
                        CaptureInterruptBias.Normal => 28000,
                        CaptureInterruptBias.Passive => 38000,
                        _ => 28000
                    };
                    if (bestScore < minPreMoveBias)
                    {
                        if (aiLog)
                            Debug.Log($"{T(snapshot.AiTeam, 2)} [score] {unit.name} -> {best.name} | descartado: captureInterruptBias ({stanceBehaviorAssign.captureInterruptBias}, min={minPreMoveBias}) > score={bestScore}");
                        best = null;
                    }
                }
            }
        }

        if (best != null && aiLog)
        {
            string escortWinnerLabel = escortPlanCohesionActive
                ? $" escortBand={(bestEscortThreatBand == int.MaxValue ? "off-plan" : bestEscortThreatBand.ToString())}"
                : string.Empty;
            Debug.Log($"{T(snapshot.AiTeam, 2)} [score] vencedor: {best.name} | prefRank={bestTargetPriorityRank} score={bestScore}{escortWinnerLabel} band={bestBand} dist={bestTieDistance}->{bestEffectiveAttackDistance}");
        }

        return best;
    }

    private static int ResolveAttackTargetPreferenceRank(AIUnitStanceBehavior stanceBehavior, BazookaTargetPriority priority)
    {
        switch (stanceBehavior != null ? stanceBehavior.targetPreference : AIAttackTargetPreference.Either)
        {
            case AIAttackTargetPreference.Primary:
                return priority switch
                {
                    BazookaTargetPriority.Primary => 3,
                    BazookaTargetPriority.Secondary => 2,
                    _ => 1
                };
            case AIAttackTargetPreference.Secondary:
                return priority switch
                {
                    BazookaTargetPriority.Secondary => 3,
                    BazookaTargetPriority.Primary => 2,
                    _ => 1
                };
            default:
                return priority switch
                {
                    BazookaTargetPriority.Primary => 3,
                    BazookaTargetPriority.Secondary => 2,
                    _ => 1
                };
        }
    }

    private bool CanUnitFireAtTargetFromCurrentPosition(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null || target.IsDead)
            return false;

        Tilemap map = attacker.BoardTilemap;
        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        if (map == null)
            return false;

        var tempTargets = new List<PodeMirarTargetOption>();
        bool canAim = PodeMirarSensor.CollectTargets(
            attacker,
            map,
            terrainDb,
            SensorMovementMode.MoveuParado,
            tempTargets);

        if (!canAim || tempTargets.Count == 0)
            return false;

        for (int i = 0; i < tempTargets.Count; i++)
        {
            PodeMirarTargetOption option = tempTargets[i];
            if (option == null || option.targetUnit == null)
                continue;
            if (option.targetUnit == target)
                return true;
        }

        return false;
    }

    private static int EvaluateAttackTargetScore(
        UnitData attackerData,
        UnitData defenderData,
        int attackerHpBefore,
        int defenderHpBefore,
        int rawDistance,
        IReadOnlyList<int> reachableAttackDistances,
        bool defendMode,
        RPSDatabase rpsDb,
        DPQMatchupDatabase dpqDb,
        WeaponPriorityData wpDb,
        out string detail)
    {
        detail = string.Empty;

        int safeRawDistance = Mathf.Max(1, rawDistance);
        if (reachableAttackDistances == null || reachableAttackDistances.Count <= 0)
        {
            detail = $"sim=invalid(noReachableFireDist rawDist={safeRawDistance})";
            return int.MinValue / 4;
        }

        AICombatHpSimulator.AICombatHpResult best = default;
        int bestDistance = -1;
        int bestScore = int.MinValue / 4;
        int bestIndex = -1;
        bool hasValid = false;

        for (int i = 0; i < reachableAttackDistances.Count; i++)
        {
            int distance = Mathf.Max(1, reachableAttackDistances[i]);
            AICombatHpSimulator.AICombatHpResult sim = AICombatHpSimulator.Simulate(
                attackerData, defenderData, attackerHpBefore, defenderHpBefore, distance, rpsDb, dpqDb, wpDb);
            if (!sim.isValid)
                continue;

            int scoreAtDistance = ScoreSimulation(sim, distance, defendMode, attackerHpBefore, defenderHpBefore, attackerData, defenderData);
            if (!hasValid || scoreAtDistance > bestScore)
            {
                hasValid = true;
                best = sim;
                bestDistance = distance;
                bestScore = scoreAtDistance;
                bestIndex = i;
            }
        }

        if (!hasValid)
        {
            detail = $"sim=invalid(allReachableDistancesInvalid count={reachableAttackDistances.Count})";
            return int.MinValue / 4;
        }

        int score = bestScore;

        bool safeStayNoCounter = false;
        if (bestDistance == safeRawDistance
            && !CanUnitDataCounterAtDistance(defenderData, attackerData, safeRawDistance, wpDb))
        {
            int safeFireBonus = defendMode ? 18000 : 22000;
            score += safeFireBonus;
            safeStayNoCounter = true;
        }

        detail =
            $"sim(reachable={reachableAttackDistances.Count},pickIndex={bestIndex}," +
            $"bestDist={bestDistance},A={best.attackerHpAfter},D={best.defenderHpAfter},kill={best.killGuaranteed}," +
            $"survive={best.attackerSurvives},safeStayNoCounter={safeStayNoCounter})";

        return score;
    }

    private bool TryCollectReachableAttackDistances(
        Tilemap boardTilemap,
        UnitManager attacker,
        UnitManager enemy,
        Vector3Int originCell,
        Vector3Int enemyCell,
        Dictionary<Vector3Int, List<Vector3Int>> movementPaths,
        HashSet<Vector3Int> occupiedByAllies,
        List<int> outputDistances)
    {
        if (outputDistances == null)
            return false;

        outputDistances.Clear();
        if (boardTilemap == null || movementPaths == null || movementPaths.Count <= 0 || attacker == null || enemy == null)
            return false;

        originCell.z = 0;
        enemyCell.z = 0;

        bool enableLdt = matchController == null || matchController.EnableLdtValidation;
        bool enableLos = matchController == null || matchController.EnableLosValidation;
        bool enableSpotter = matchController == null || matchController.EnableSpotter;
        bool enableStealth = matchController == null || matchController.EnableStealthValidation;

        if (enableStealth && enemy.TryGetUnitData(out UnitData enemyData) && enemyData != null)
        {
            bool isStealth = enemyData.IsStealthUnit(enemy.GetDomain(), enemy.GetHeightLevel());
            if (isStealth && !PodeMirarSensor.IsStealthTargetRevealedForTeam(enemy, (int)attacker.TeamId))
                return false;
        }

        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        DPQAirHeightConfig airCfg = turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null;
        HashSet<int> unique = new HashSet<int>();
        HashSet<Vector3Int> validFireCells = new HashSet<Vector3Int>();

        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in movementPaths)
        {
            Vector3Int cell = pair.Key;
            cell.z = 0;

            bool isOrigin = cell == originCell;
            if (!isOrigin && occupiedByAllies != null && occupiedByAllies.Contains(cell))
                continue;

            SensorMovementMode mode = isOrigin ? SensorMovementMode.MoveuParado : SensorMovementMode.MoveuAndando;
            PodeMirarSensor.CollectValidFireCellsFromOrigin(
                attacker,
                boardTilemap,
                terrainDb,
                mode,
                cell,
                validFireCells,
                airCfg,
                enableLdt,
                enableLos,
                enableSpotter);

            if (!validFireCells.Contains(enemyCell))
                continue;

            int dist = GetHexDistance(boardTilemap, cell, enemyCell, 64);
            if (dist == int.MaxValue)
                continue;

            dist = Mathf.Max(1, dist);
            if (unique.Add(dist))
                outputDistances.Add(dist);
        }

        return outputDistances.Count > 0;
    }

    private static bool IsBetterTargetInBand(
        bool defendMode,
        int score,
        int tieDistance,
        int bestScore,
        int bestTieDistance)
    {
        if (!defendMode)
            return score > bestScore || (score == bestScore && tieDistance < bestTieDistance);

        int scoreDelta = score - bestScore;
        if (scoreDelta >= 1200)
            return true;
        if (scoreDelta <= -1200)
            return false;

        if (tieDistance != bestTieDistance)
            return tieDistance < bestTieDistance;

        return score > bestScore;
    }

    private static int ScoreSimulation(AICombatHpSimulator.AICombatHpResult sim, int distance, bool defendMode, int attackerHpBefore, int defenderHpBefore, UnitData attackerData = null, UnitData defenderData = null)
    {
        if (!sim.isValid)
            return int.MinValue / 4;

        int attackerEliminated = Mathf.Max(0, Mathf.Max(0, attackerHpBefore) - sim.attackerHpAfter);
        int defenderEliminated = Mathf.Max(0, Mathf.Max(0, defenderHpBefore) - sim.defenderHpAfter);

        int score = 0;
        int killWeight = defendMode ? 70000 : 100000;
        int surviveWeight = defendMode ? 30000 : 20000;
        int selfLossWeight = defendMode ? 1200 : 700;
        int enemyLossWeight = defendMode ? 900 : 1200;
        int enemyRemainingWeight = defendMode ? 60 : 90;
        int selfRemainingWeight = defendMode ? 80 : 40;
        int distanceWeight = defendMode ? 4 : 10;
        int attackerCost = attackerData != null ? Mathf.Max(0, attackerData.cost) : 0;
        int defenderCost = defenderData != null ? Mathf.Max(0, defenderData.cost) : 0;
        int defenderCostTier = Mathf.Clamp(defenderCost / 1000, 0, 20);
        int attackerCostTier = Mathf.Clamp(attackerCost / 1000, 0, 20);
        int targetValuePerHpWeight = defendMode ? 55 : 95;
        int selfValuePerHpWeight = defendMode ? 70 : 45;
        int killValueWeight = defendMode ? 900 : 1500;

        if (sim.killGuaranteed)
            score += killWeight;
        if (sim.attackerSurvives)
            score += surviveWeight;
        else
            score -= 50000;

        score += defenderEliminated * enemyLossWeight;
        score -= attackerEliminated * selfLossWeight;
        score -= sim.defenderHpAfter * enemyRemainingWeight;
        score += sim.attackerHpAfter * selfRemainingWeight;
        score += defenderEliminated * defenderCostTier * targetValuePerHpWeight;
        score -= attackerEliminated * attackerCostTier * selfValuePerHpWeight;
        if (sim.killGuaranteed)
            score += defenderCostTier * killValueWeight;
        score -= Mathf.Max(1, distance) * distanceWeight;
        return score;
    }

    private static bool CanUnitDataCounterAtDistance(UnitData defenderData, UnitData attackerData, int distance, WeaponPriorityData wpDb)
    {
        if (defenderData == null || attackerData == null)
            return false;

        return PodeMirarSensor.TryResolveCounterAttackFromData(
            defenderData,
            attackerData,
            Mathf.Max(1, distance),
            wpDb,
            out _,
            out _,
            out _);
    }

    private bool CanReachAndAttackThisTurn(
        UnitManager attacker,
        UnitManager enemy,
        AISnapshot snapshot,
        HashSet<Vector3Int> occupiedByAllies,
        Vector3Int attackerCell)
    {
        if (attacker == null || enemy == null || enemy.IsDead || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        int moveBudget = Mathf.Max(0, attacker.RemainingMovementPoints);
        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(
            snapshot.BoardTilemap,
            attacker,
            moveBudget,
            terrainDb);
        if (paths == null || paths.Count <= 0)
            return false;

        Vector3Int enemyCell = enemy.CurrentCellPosition;
        enemyCell.z = 0;
        attackerCell.z = 0;
        List<int> distances = new List<int>(8);
        return TryCollectReachableAttackDistances(
            snapshot.BoardTilemap,
            attacker,
            enemy,
            attackerCell,
            enemyCell,
            paths,
            occupiedByAllies,
            distances);
    }

    private bool TryEvaluateReachableAttackScore(
        UnitManager attacker,
        UnitManager enemy,
        AISnapshot snapshot,
        HashSet<Vector3Int> occupiedByAllies,
        out int score)
    {
        score = int.MinValue / 4;
        if (attacker == null || enemy == null || enemy.IsDead || snapshot == null || snapshot.BoardTilemap == null)
            return false;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return false;
        if (!enemy.TryGetUnitData(out UnitData defenderData) || defenderData == null)
            return false;

        RPSDatabase rpsDb = turnStateManager != null ? turnStateManager.RpsDatabaseRef : null;
        DPQMatchupDatabase dpqDb = turnStateManager != null ? turnStateManager.DpqMatchupDatabaseRef : null;
        WeaponPriorityData wpDb = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        if (rpsDb == null || dpqDb == null || wpDb == null)
            return false;

        int moveBudget = Mathf.Max(0, attacker.RemainingMovementPoints);
        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(
            snapshot.BoardTilemap,
            attacker,
            moveBudget,
            terrainDb);
        if (paths == null || paths.Count <= 0)
            return false;

        Vector3Int attackerCell = attacker.CurrentCellPosition;
        attackerCell.z = 0;
        Vector3Int enemyCell = enemy.CurrentCellPosition;
        enemyCell.z = 0;
        List<int> reachableAttackDistances = new List<int>(8);
        if (!TryCollectReachableAttackDistances(
                snapshot.BoardTilemap,
                attacker,
                enemy,
                attackerCell,
                enemyCell,
                paths,
                occupiedByAllies,
                reachableAttackDistances))
            return false;

        int tieDistance = GetHexDistance(snapshot.BoardTilemap, attackerCell, enemyCell, 64);
        if (tieDistance == int.MaxValue)
            tieDistance = 64;

        score = EvaluateAttackTargetScore(
            attackerData,
            defenderData,
            attacker.CurrentHP,
            enemy.CurrentHP,
            tieDistance,
            reachableAttackDistances,
            currentStance == AIStance.Defend,
            rpsDb,
            dpqDb,
            wpDb,
            out _);

        return score > (int.MinValue / 8);
    }
}
