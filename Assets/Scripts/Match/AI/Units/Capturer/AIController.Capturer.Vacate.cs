using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Desocupação de prédios de produção bloqueados
    // -------------------------------------------------------------------------

    private bool HasAttackTargetAtCurrentPos(UnitManager unit)
    {
        var targets = new List<PodeMirarTargetOption>();
        return PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
            SensorMovementMode.MoveuParado, targets) && targets.Count > 0;
    }

    private bool TryFindHomeProductionVacateCombatAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;
        TeamId aiTeam = snapshot.AITeam;
        if (!IsActiveUnitBlockingThreatenedHomeProduction(unit, snapshot, aiTeam, fromCell, out string threatReason))
            return false;

        bool conservativeBackline = IsBacklineSupportUnit(unit) && IsFireSupportConservative(unit);
        if (conservativeBackline
            && TryBuildBestFireSupportAttack(
                unit,
                snapshot,
                fromCell,
                paths,
                occupied,
                fromCell,
                defensiveContext: true,
                out action,
                out string fireReason))
        {
            Debug.Log($"{TL("Base")} {unit.InstanceId} libera producao ({threatReason}) - {fireReason}");
            return true;
        }

        if (conservativeBackline
            && TryBuildFireSupportBlockedShotRepositionAction(
                unit,
                snapshot,
                fromCell,
                paths,
                occupied,
                defensiveContext: true,
                out action,
                out string blockedReason))
        {
            Debug.Log($"{TL("Base")} {unit.InstanceId} libera producao ({threatReason}) - {blockedReason}");
            return true;
        }

        if (!conservativeBackline && TryFindHomeProductionVacateAttack(unit, snapshot, fromCell, paths, occupied,
                out Vector3Int attackCell, out UnitManager target, out string reason))
        {
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("Base")} {unit.InstanceId} libera producao ({threatReason}) - ataca via {attackCell} -> {target.UnitDisplayName}#{target.InstanceId} ({reason})");
            action = BuildAttackBatch(unit, aiTeam, fromCell, attackCell,
                target.InstanceId.ToString(), targetCell, paths);
            return true;
        }

        if (TryFindHomeProductionVacateMove(unit, snapshot, aiTeam, fromCell, paths, occupied, out Vector3Int moveCell, out string moveReason))
        {
            Debug.Log($"{TL("Base")} {unit.InstanceId} libera producao ({threatReason}) - reposiciona via {moveCell} ({moveReason})");
            action = BuildMoveBatch(unit, aiTeam, fromCell, moveCell, paths);
            return true;
        }

        return false;
    }

    private bool TryFindProductionUnlockVacateAction(UnitManager unit, AIWorldSnapshot snapshot, out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null)
            return false;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        ConstructionManager current = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);
        if (current == null || !current.CanProduceUnitsForSlot(snapshot.AISlotIndex))
            return false;
        if (!AreAllAffordableHomeProducersOccupied(snapshot, out int occupiedProducers, out int totalProducers, out int cheapestOffer))
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        if (paths == null || paths.Count == 0)
            return false;

        if (!TryFindProductionUnlockVacateCell(unit, snapshot, fromCell, paths, occupied, out Vector3Int moveCell, out string reason))
            return false;

        Debug.Log($"{TL("Base")} {unit.InstanceId} libera produtora travada ({occupiedProducers}/{totalProducers} ocupadas, cheapest=${cheapestOffer}) via {moveCell} ({reason})");
        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        return true;
    }

    private bool AreAllAffordableHomeProducersOccupied(
        AIWorldSnapshot snapshot,
        out int occupiedProducers,
        out int totalProducers,
        out int cheapestOffer)
    {
        occupiedProducers = 0;
        totalProducers = 0;
        cheapestOffer = int.MaxValue;
        if (snapshot?.MyBuildings == null)
            return false;

        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex))
                continue;
            if (building.OfferedUnits == null || building.OfferedUnits.Count == 0)
                continue;

            int localCheapest = int.MaxValue;
            foreach (UnitData offered in building.OfferedUnits)
            {
                if (offered == null)
                    continue;
                localCheapest = Mathf.Min(localCheapest, offered.cost);
            }
            if (localCheapest == int.MaxValue)
                continue;

            cheapestOffer = Mathf.Min(cheapestOffer, localCheapest);
            totalProducers++;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            if (UnitOccupancyRules.GetUnitAtCell(boardTilemap, cell, null) != null)
                occupiedProducers++;
        }

        return totalProducers > 0
            && occupiedProducers >= totalProducers
            && cheapestOffer != int.MaxValue
            && snapshot.Budget >= cheapestOffer;
    }

    private bool TryFindProductionUnlockVacateCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        float bestScore = float.MinValue;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;

            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (construction != null && construction.CanProduceUnitsForSlot(snapshot.AISlotIndex))
                continue;

            int pathCost = GetPathStepCount(paths, cell);
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float dpq = GetTerrainDpqPontos(cell);
            float distFromProducer = SectorManager.HexDistance(fromCell, cell);
            float allyCohesion = CalculateFireSupportCohesionScore(unit, snapshot, cell);
            float score =
                dpq * 80f
                + allyCohesion * 0.15f
                - threat * 90f
                - pathCost * 20f
                - distFromProducer * 15f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                reason = $"score={score:F0} dpq={dpq:F1} threat={threat:F1} path={pathCost}";
            }
        }

        return bestCell != fromCell;
    }

    private bool IsActiveUnitBlockingThreatenedHomeProduction(UnitManager unit, AIWorldSnapshot snapshot, TeamId aiTeam, Vector3Int fromCell, out string reason)
    {
        reason = "";
        if (unit == null)
            return false;

        fromCell.z = 0;
        ConstructionManager current = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);
        if (current == null || !current.CanProduceUnitsForSlot(AIController.ResolveAISlotKey(aiTeam)))
            return false;

        if (unit.TryGetUnitData(out UnitData unitData) && unitData != null)
        {
            if (unitData.longRangeStationary)
                return false;
            if (UnitRoleCompatibility.CanSatisfy(unitData, UnitRole.FogoIndireto))
            {
                if (HasNonFireSupportUnitOnProductionBuilding(unit, aiTeam, snapshot))
                    return false;
                if (!IsLeastPreferredFireSupportDefender(unit, unitData, aiTeam, snapshot))
                    return false;
            }
        }

        bool hasOtherFreeFactory = false;
        if (snapshot != null && snapshot.MyBuildings != null)
        {
            foreach (ConstructionManager bldg in snapshot.MyBuildings)
            {
                if (bldg == current || bldg == null || !bldg.CanProduceUnitsForSlot(AIController.ResolveAISlotKey(aiTeam))) continue;
                Vector3Int cell = bldg.CurrentCellPosition; cell.z = 0;
                if (UnitOccupancyRules.GetUnitAtCell(boardTilemap, cell, null) == null)
                {
                    hasOtherFreeFactory = true;
                    break;
                }
            }
        }

        if (hasOtherFreeFactory)
            return false;

        if (!unit.IsUnderRepair
            && IsCriticalHomeDefenseSector(current.Sector, aiTeam)
            && IsHomeDefenseThreatened(current.Sector, aiTeam, HomeDefenseThreatRange))
        {
            reason = "ameaca_base";
            return true;
        }

        if (IsEmergencyProductionDefenseUnblockNeeded(snapshot, current, out UnitData emergencyUnit, out int contestedOwned))
        {
            string unitName = emergencyUnit != null && !string.IsNullOrWhiteSpace(emergencyUnit.displayName)
                ? emergencyUnit.displayName
                : emergencyUnit != null ? emergencyUnit.name : "defesa";
            reason = $"emergencia_fabrica cash={snapshot.Budget} comprar={unitName} construcoes_contestadas={contestedOwned}";
            return true;
        }

        return false;
    }

    private bool HasNonFireSupportUnitOnProductionBuilding(UnitManager fireSupportUnit, TeamId aiTeam, AIWorldSnapshot snapshot)
    {
        if (snapshot?.MyUnits == null) return false;
        foreach (UnitManager other in snapshot.MyUnits)
        {
            if (other == null || other == fireSupportUnit) continue;
            if (other.IsDead || other.IsEmbarked || other.HasActed) continue;
            if (!other.TryGetUnitData(out UnitData otherData) || otherData == null) continue;
            if (otherData.longRangeStationary) continue;
            if (UnitRoleCompatibility.CanSatisfy(otherData, UnitRole.FogoIndireto)) continue;
            Vector3Int otherCell = other.CurrentCellPosition; otherCell.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, otherCell);
            if (bldg != null && bldg.CanProduceUnitsForSlot(AIController.ResolveAISlotKey(aiTeam)))
                return true;
        }
        return false;
    }

    private bool IsLeastPreferredFireSupportDefender(UnitManager unit, UnitData unitData, TeamId aiTeam, AIWorldSnapshot snapshot)
    {
        if (snapshot?.MyUnits == null) return true;
        int myElite = unitData != null ? unitData.eliteLevel : 0;
        bool myArtilleryMode = unitData != null && unitData.preferArtilleryModeBeforeCombatant;
        foreach (UnitManager other in snapshot.MyUnits)
        {
            if (other == null || other == unit) continue;
            if (other.IsDead || other.IsEmbarked || other.HasActed) continue;
            if (!other.TryGetUnitData(out UnitData otherData) || otherData == null) continue;
            if (otherData.longRangeStationary) continue;
            if (!UnitRoleCompatibility.CanSatisfy(otherData, UnitRole.FogoIndireto)) continue;
            Vector3Int otherCell = other.CurrentCellPosition; otherCell.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, otherCell);
            if (bldg == null || !bldg.CanProduceUnitsForSlot(AIController.ResolveAISlotKey(aiTeam))) continue;
            if (otherData.eliteLevel < myElite) return false;
            if (otherData.eliteLevel > myElite) continue;
            if (otherData.preferArtilleryModeBeforeCombatant && !myArtilleryMode) return false;
        }
        return true;
    }

    private static bool IsEmergencyProductionDefenseUnblockNeeded(
        AIWorldSnapshot snapshot,
        ConstructionManager blockedProduction,
        out UnitData emergencyUnit,
        out int contestedOwned)
    {
        emergencyUnit = null;
        contestedOwned = CountOwnedConstructionsUnderCapture(snapshot);
        if (snapshot == null || blockedProduction == null)
            return false;
        if (snapshot.MyUnits == null || snapshot.MyUnits.Count != 1)
            return false;
        if (contestedOwned <= 0)
            return false;

        emergencyUnit = FindBestAffordableEmergencyDefensePurchase(blockedProduction, snapshot.Budget);
        return emergencyUnit != null;
    }

    private static int CountOwnedConstructionsUnderCapture(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return 0;

        int count = 0;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.IsCapturable || building.CapturePointsMax <= 0)
                continue;
            if (building.CurrentCapturePoints < building.CapturePointsMax)
                count++;
        }

        return count;
    }

    private static UnitData FindBestAffordableEmergencyDefensePurchase(ConstructionManager building, int budget)
    {
        if (building == null || building.OfferedUnits == null)
            return null;

        UnitData best = null;
        int bestScore = int.MinValue;
        foreach (UnitData unit in building.OfferedUnits)
        {
            if (unit == null || unit.cost > budget || unit.domain != Domain.Land)
                continue;

            bool fireSupport = UnitRoleCompatibility.CanSatisfy(unit, UnitRole.FogoIndireto);
            bool assaultArmor = unit.unitClass == GameUnitClass.Armored
                && UnitRoleCompatibility.ResolveCompositionRole(unit) == UnitRole.Assalto;
            if (!fireSupport && !assaultArmor)
                continue;

            int score = unit.cost + Mathf.Max(0, unit.eliteLevel) * 10000;
            if (fireSupport) score += 100000;
            if (unit.longRangeStationary) score += 25000;
            if (unit.preferRepositionAtWeaponMaxRange) score += 15000;
            if (assaultArmor) score += 50000;

            if (score > bestScore)
            {
                bestScore = score;
                best = unit;
            }
        }

        return best;
    }

    private bool TryFindHomeProductionVacateAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        out string bestReason)
    {
        bestCell = fromCell;
        bestTarget = null;
        bestReason = "";
        float bestScore = float.MinValue;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;

            var targets = new List<PodeMirarTargetOption>();
            if (!PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuAndando, targets, fromCell: cell))
                continue;

            foreach (PodeMirarTargetOption opt in targets)
            {
                if (opt?.targetUnit == null) continue;
                if (!PassesAttackDecision(unit, opt.targetUnit, cell, true, out string decisionReason))
                    continue;

                Vector3Int targetCell = opt.targetUnit.CurrentCellPosition;
                targetCell.z = 0;
                BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, opt.targetUnit);
                float targetPreferenceScore = GetAssaultTargetPreferenceScore(targetPreference) * 2f;
                bool hasSim = TrySimulateAttackForAI(unit, opt.targetUnit, cell, out CombatEvaluationResult simSummary);
                if (hasSim && simSummary.TargetDamage <= 0)
                    continue;

                float combatScore = 0f;
                string simDetails = "sim=unavailable";
                if (hasSim)
                {
                    combatScore =
                        (simSummary.Simulation.killGuaranteed ? 24000f : 0f)
                        + simSummary.TargetDamagePercent * 650f
                        + simSummary.TargetDamage * 140f
                        - simSummary.AttackerLossPercent * 520f
                        - simSummary.AttackerLoss * 180f
                        + (simSummary.Simulation.attackerSurvives ? 2500f : -10000f);

                    if (simSummary.AttackerLossPercent >= 75 && !simSummary.Simulation.killGuaranteed)
                        combatScore -= 14000f;

                    PositionDpqResult attackerDpq = ResolveDpqForAttackDecision(unit, cell);
                    PositionDpqResult defenderDpq = ResolveDpqForAttackDecision(opt.targetUnit, targetCell);
                    simDetails = $"sim dmg={simSummary.TargetDamage}/{simSummary.TargetDamagePercent}% loss={simSummary.AttackerLoss}/{simSummary.AttackerLossPercent}% hp={simSummary.AttackerHpBefore}->{simSummary.Simulation.attackerHpAfter} target={simSummary.TargetHpBefore}->{simSummary.Simulation.defenderHpAfter} dpq={attackerDpq.Points}/{defenderDpq.Points} def={attackerDpq.DefenseBonus}/{defenderDpq.DefenseBonus} kill={simSummary.Simulation.killGuaranteed} survive={simSummary.Simulation.attackerSurvives}";
                }

                float score =
                    targetPreferenceScore
                    + combatScore
                    + AttackTargetPriority(targetCell, fromCell) * 10000f
                    + Mathf.Max(0, 20 - opt.targetUnit.CurrentHP) * 100f
                    + GetTerrainDpqPontos(cell) * 25f
                    - SectorManager.HexDistance(cell, targetCell) * 20f
                    - GetPathStepCount(paths, cell) * 5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = opt.targetUnit;
                    bestReason = $"score={score:F0} pref={targetPreference} prefScore={targetPreferenceScore:F0} combat={combatScore:F0} hp={opt.targetUnit.CurrentHP} dpq={GetTerrainDpqPontos(cell):F1} {simDetails} {decisionReason}";
                }
            }
        }

        return bestTarget != null;
    }

    private bool TryFindHomeProductionVacateMove(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamId aiTeam,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        float bestScore = float.MinValue;
        bool conservativeBackline = IsBacklineSupportUnit(unit) && IsFireSupportConservative(unit);

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;

            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            bool blocksProduction = bldg != null && bldg.CanProduceUnitsForSlot(AIController.ResolveAISlotKey(aiTeam));
            float enemyDist = DistanceToNearestVisibleEnemy(cell, aiTeam);
            float dpq = GetTerrainDpqPontos(cell);
            int pathCost = GetPathStepCount(paths, cell);
            float score;
            string localReason;

            if (conservativeBackline)
            {
                if (blocksProduction)
                    continue;
                if (!IsFireSupportConservativeCellAllowed(unit, snapshot, cell))
                    continue;

                float threat = CalculateThreatLevel(cell, aiTeam);
                float cohesion = CalculateFireSupportCohesionScore(unit, snapshot, cell);
                bool hasRearAnchor = TryFindNearestVisibleEnemyCell(fromCell, aiTeam, out Vector3Int rearAnchor);
                float rearLine = hasRearAnchor
                    ? CalculateFireSupportRearLineScore(unit, snapshot, cell, rearAnchor)
                    : 0f;
                float alliedConstructionPenalty = unit.GetDomain() == Domain.Air
                    && bldg != null
                    && bldg.SlotIndex == ResolveAISlotKey(aiTeam)
                        ? 180f
                        : 0f;
                float currentAlliedConstructionBonus = unit.GetDomain() == Domain.Air
                    && ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell) is ConstructionManager currentBldg
                    && currentBldg.SlotIndex == ResolveAISlotKey(aiTeam)
                        ? 240f
                        : 0f;
                float homeBias = 0f;
                if (snapshot != null && snapshot.MyHQ != null)
                {
                    Vector3Int hq = snapshot.MyHQ.CurrentCellPosition;
                    hq.z = 0;
                    homeBias = -SectorManager.HexDistance(cell, hq) * 25f;
                }

                float cappedEnemyDist = Mathf.Min(enemyDist, 8f);
                score =
                    cappedEnemyDist * 130f
                    + dpq * 55f
                    + cohesion * 1.05f
                    + rearLine * 1.25f
                    + currentAlliedConstructionBonus
                    + homeBias
                    - threat * 180f
                    - alliedConstructionPenalty
                    - pathCost * 28f;
                localReason = $"conservador score={score:F0} enemyDist={enemyDist:F1} threat={threat:F1} dpq={dpq:F1} coh={cohesion:F0} rear={rearLine:F0} buildPenalty={alliedConstructionPenalty:F0} leaveBuildBonus={currentAlliedConstructionBonus:F0} path={pathCost}";
            }
            else
            {
                score =
                    (blocksProduction ? -10000f : 0f)
                    - enemyDist * 50f
                    + dpq * 20f
                    - pathCost * 2f;
                localReason = $"score={score:F0} enemyDist={enemyDist:F1} dpq={dpq:F1} path={pathCost}";
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                reason = localReason;
            }
        }

        return bestCell != fromCell;
    }

    private static float DistanceToNearestVisibleEnemy(Vector3Int cell, TeamId aiTeam)
    {
        float best = float.MaxValue;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.SlotIndex == ResolveAISlotKey(aiTeam) || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForSlot(enemy, PlayerSlotId.FromIndex(ResolveAISlotKey(aiTeam)))) continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            best = Mathf.Min(best, SectorManager.HexDistance(cell, enemyCell));
        }

        return best < float.MaxValue ? best : 99f;
    }

    private static bool TryFindNearestVisibleEnemyCell(Vector3Int fromCell, TeamId aiTeam, out Vector3Int enemyCell)
    {
        enemyCell = fromCell;
        enemyCell.z = 0;
        float best = float.MaxValue;
        MatchController mc = GetMatchController();

        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.SlotIndex == ResolveAISlotKey(aiTeam) || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForSlot(enemy, PlayerSlotId.FromIndex(ResolveAISlotKey(aiTeam)))) continue;

            Vector3Int cell = enemy.CurrentCellPosition;
            cell.z = 0;
            float dist = SectorManager.HexDistance(fromCell, cell);
            if (dist >= best) continue;

            best = dist;
            enemyCell = cell;
        }

        return best < float.MaxValue;
    }
}
