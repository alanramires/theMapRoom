using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIController
{

    // -------------------------------------------------------------------------
    // Helpers de combate
    // -------------------------------------------------------------------------

    // 3 = defensor do prédio objetivo  2 = inimigo em qualquer construção  1 = terreno aberto
    private float AttackTargetPriority(Vector3Int targetUnitCell, Vector3Int captureTargetCell)
    {
        if (targetUnitCell == captureTargetCell) return 3f;
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetUnitCell);
        return bldg != null ? 2f : 1f;
    }

    // Perseguidor: prioriza inimigos mais próximos ao objetivo a capturar
    private float AttackTargetPriorityPursuer(Vector3Int targetUnitCell, Vector3Int captureTargetCell)
    {
        if (targetUnitCell == captureTargetCell) return 3f;
        float dist = SectorManager.HexDistance(targetUnitCell, captureTargetCell);
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetUnitCell);
        float bldgBonus = bldg != null ? 0.5f : 0f;
        return 2f - dist * 0.1f + bldgBonus;
    }

    private bool HasAttackTargetAtCurrentPos(UnitManager unit)
    {
        var targets = new List<PodeMirarTargetOption>();
        return PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
            SensorMovementMode.MoveuParado, targets) && targets.Count > 0;
    }

    // Escolhe o melhor alvo para o rogue: capturadores ativos em prédios têm prioridade máxima,
    // desempate por HP mais baixo (mais fácil de eliminar).
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

        if (TryFindHomeProductionVacateAttack(unit, snapshot, fromCell, paths, occupied,
                out Vector3Int attackCell, out UnitManager target, out string reason))
        {
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("Base")} {unit.InstanceId} libera producao ({threatReason}) - ataca via {attackCell} -> {target.UnitDisplayName}#{target.InstanceId} ({reason})");
            action = BuildAttackBatch(unit, aiTeam, fromCell, attackCell,
                target.InstanceId.ToString(), targetCell, paths);
            return true;
        }

        if (TryFindHomeProductionVacateMove(unit, aiTeam, fromCell, paths, occupied, out Vector3Int moveCell))
        {
            Debug.Log($"{TL("Base")} {unit.InstanceId} libera producao ({threatReason}) - reposiciona via {moveCell}");
            action = BuildMoveBatch(unit, aiTeam, fromCell, moveCell, paths);
            return true;
        }

        return false;
    }

    private bool IsActiveUnitBlockingThreatenedHomeProduction(UnitManager unit, AIWorldSnapshot snapshot, TeamId aiTeam, Vector3Int fromCell, out string reason)
    {
        reason = "";
        if (unit == null)
            return false;

        fromCell.z = 0;
        ConstructionManager current = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);
        if (current == null || !current.CanProduceUnitsForTeam(aiTeam))
            return false;

        if (unit.TryGetUnitData(out UnitData unitData) && unitData != null)
        {
            if (unitData.longRangeStationary)
                return false;
            if (unitData.roles != null && unitData.roles.Contains(UnitRole.FogoIndireto))
            {
                // Fire support stays if any non-fire-support unit can vacate instead.
                if (HasNonFireSupportUnitOnProductionBuilding(unit, aiTeam, snapshot))
                    return false;
                // All production slots occupied by fire support — only the least preferred
                // defender vacates: elite=0 first, then artillery-mode (can operate at range).
                if (!IsLeastPreferredFireSupportDefender(unit, unitData, aiTeam, snapshot))
                    return false;
            }
        }

        bool hasOtherFreeFactory = false;
        if (snapshot != null && snapshot.MyBuildings != null)
        {
            foreach (ConstructionManager bldg in snapshot.MyBuildings)
            {
                if (bldg == current || bldg == null || !bldg.CanProduceUnitsForTeam(aiTeam)) continue;
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
            if (otherData.roles != null && otherData.roles.Contains(UnitRole.FogoIndireto)) continue;
            Vector3Int otherCell = other.CurrentCellPosition; otherCell.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, otherCell);
            if (bldg != null && bldg.CanProduceUnitsForTeam(aiTeam))
                return true;
        }
        return false;
    }

    // Returns true if this fire support unit is the most appropriate one to vacate.
    // Vacate order: lowest eliteLevel first; tiebreak: preferArtilleryModeBeforeCombatant first
    // (artillery-mode units can operate effectively outside the production tile).
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
            if (otherData.roles == null || !otherData.roles.Contains(UnitRole.FogoIndireto)) continue;
            Vector3Int otherCell = other.CurrentCellPosition; otherCell.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, otherCell);
            if (bldg == null || !bldg.CanProduceUnitsForTeam(aiTeam)) continue;
            // If 'other' is a worse defender, this unit should not vacate.
            if (otherData.eliteLevel < myElite) return false;
            if (otherData.eliteLevel > myElite) continue;
            // Same elite: artillery-mode is a worse defender (can operate at range).
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

            bool fireSupport = unit.roles != null && unit.roles.Contains(UnitRole.FogoIndireto);
            bool assaultArmor = unit.unitClass == GameUnitClass.Armored
                && unit.roles != null && unit.roles.Count > 0 && unit.roles[0] == UnitRole.Assalto;
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
                float score =
                    AttackTargetPriority(targetCell, fromCell) * 10000f
                    + Mathf.Max(0, 20 - opt.targetUnit.CurrentHP) * 100f
                    + GetTerrainDpqPontos(cell) * 25f
                    - SectorManager.HexDistance(cell, targetCell) * 20f
                    - GetPathStepCount(paths, cell) * 5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = opt.targetUnit;
                    bestReason = $"score={score:F0} hp={opt.targetUnit.CurrentHP} dpq={GetTerrainDpqPontos(cell):F1} {decisionReason}";
                }
            }
        }

        return bestTarget != null;
    }

    private bool TryFindHomeProductionVacateMove(
        UnitManager unit,
        TeamId aiTeam,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell)
    {
        bestCell = fromCell;
        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;

            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            bool blocksProduction = bldg != null && bldg.CanProduceUnitsForTeam(aiTeam);
            float enemyDist = DistanceToNearestVisibleEnemy(cell, aiTeam);
            float score =
                (blocksProduction ? -10000f : 0f)
                - enemyDist * 50f
                + GetTerrainDpqPontos(cell) * 20f
                - GetPathStepCount(paths, cell) * 2f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
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
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            best = Mathf.Min(best, SectorManager.HexDistance(cell, enemyCell));
        }

        return best < float.MaxValue ? best : 99f;
    }

    private UnitManager PickBestRogueTarget(List<PodeMirarTargetOption> options, TeamId aiTeam)
    {
        return PickBestRogueTarget(options, aiTeam, null, default, false, out _);
    }

    private UnitManager PickBestRogueTarget(
        List<PodeMirarTargetOption> options,
        TeamId aiTeam,
        UnitManager attacker,
        Vector3Int attackCell,
        bool defensiveContext,
        out string attackDecisionReason)
    {
        attackDecisionReason = "";
        UnitManager best = null;
        float bestPriority = float.MinValue;
        foreach (PodeMirarTargetOption opt in options)
        {
            if (opt?.targetUnit == null || opt.targetUnit.TeamId == aiTeam) continue;
            string decisionReason = "";
            if (attacker != null
                && !PassesAttackDecision(attacker, opt.targetUnit, attackCell, defensiveContext, out decisionReason))
            {
                continue;
            }
            float priority = 10f - opt.targetUnit.CurrentHP;

            // Inimigo em prédio capturável → ameaça direta ao objetivo, prioridade máxima
            Vector3Int ec = opt.targetUnit.CurrentCellPosition; ec.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, ec);
            if (bldg != null && bldg.IsCapturable
                && !(bldg.TeamId == aiTeam && bldg.CurrentCapturePoints >= bldg.CapturePointsMax))
                priority += 1000f;

            if (priority > bestPriority)
            {
                bestPriority = priority;
                best = opt.targetUnit;
                attackDecisionReason = attacker != null ? decisionReason : "";
            }
        }
        return best;
    }

    private static bool HasEnemyInEngageRadius(UnitManager unit, Vector3Int fromCell, TeamId aiTeam)
    {
        int radius = Mathf.Max(0, unit.RemainingMovementPoints) + 1;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (Vector3Int.Distance(fromCell, ec) <= radius) return true;
        }
        return false;
    }

    // Inimigo visível dentro do raio de movimento+1 que está mais perto do alvo do que a unidade
    // (bloqueador de rota) → deve-se lutar, não rotear pelo QG.
    private static bool HasEnemyBlockingPath(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell, TeamId aiTeam)
    {
        float unitDist   = Vector3Int.Distance(fromCell, targetCell);
        int engageRadius = Mathf.Max(0, unit.RemainingMovementPoints) + 1;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (Vector3Int.Distance(fromCell, ec) > engageRadius) continue;
            if (Vector3Int.Distance(ec, targetCell) < unitDist) return true;
        }
        return false;
    }

    // Inimigo visível no hex alvo ou num dos seus adjacentes (ameaça direta ao objetivo).
    private bool HasEnemyNearCell(Vector3Int cell, TeamId aiTeam)
    {
        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, cell, neighbors);
        var nearCells = new HashSet<Vector3Int>(neighbors) { cell };

        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (nearCells.Contains(ec)) return true;
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Helpers de captura
    // -------------------------------------------------------------------------

    // Captura oportunista: primeiro prédio capturável alcançável, excluindo excludeCell.
    // excludeCurrentCell=true: ignora fromCell (usado após handoff — não re-capturar o setor abandonado).
    private bool TryFindOpportunisticCapture(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int excludeCell,
        out Vector3Int captureCell,
        bool excludeCurrentCell = false,
        HashSet<Vector3Int> skippedCaptureCells = null)
    {
        captureCell = Vector3Int.zero;
        Vector3Int currentCell = unit.CurrentCellPosition; currentCell.z = 0;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied.Contains(cell) || cell == excludeCell) continue;
            if (skippedCaptureCells != null && skippedCaptureCells.Contains(cell)) continue;
            if (excludeCurrentCell && cell == currentCell) continue;
            if (!SimulateCaptureSensor(unit, cell, out _)) continue;
            captureCell = cell;
            return true;
        }
        return false;
    }

    // FoW passinho: entre os hexes adjacentes ao alvo alcançáveis, prefere maior DPQ (prioritizeDpqAtBattle=true) ou maior EV.
    private bool ShouldReserveOpportunisticCaptureForCloserUnit(
        UnitManager opportunist,
        TeamId aiTeam,
        Vector3Int captureCell,
        Dictionary<Vector3Int, List<Vector3Int>> opportunistPaths,
        out UnitManager reservedFor)
    {
        reservedFor = null;

        if (!SimulateCaptureSensor(opportunist, captureCell, out ConstructionManager captureTarget))
            return false;

        int opportunistCost = GetPathStepCount(opportunistPaths, captureCell);
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);

        if (TryFindAssignedCapturerForCaptureTarget(
                opportunist,
                plan,
                captureTarget,
                aiTeam,
                captureCell,
                out UnitManager assignedCapturer))
        {
            reservedFor = assignedCapturer;
            return true;
        }

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == opportunist || candidate.TeamId != aiTeam) continue;
            if (candidate.HasActed || candidate.IsDead || candidate.IsEmbarked || candidate.IsUnderRepair) continue;
            if (!SimulateCaptureSensor(candidate, captureCell, out _)) continue;

            Dictionary<Vector3Int, List<Vector3Int>> candidatePaths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap, candidate, Mathf.Max(0, candidate.RemainingMovementPoints), terrainDatabase);
            if (candidatePaths == null || !candidatePaths.ContainsKey(captureCell)) continue;

            int candidateCost = GetPathStepCount(candidatePaths, captureCell);
            bool candidateOwnsTarget = IsAssignedToCaptureTarget(candidate, plan, captureTarget, aiTeam);

            if (candidateCost < opportunistCost || (candidateOwnsTarget && candidateCost <= opportunistCost))
            {
                reservedFor = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryFindAssignedCapturerForCaptureTarget(
        UnitManager opportunist,
        TeamObjectivePlan plan,
        ConstructionManager captureTarget,
        TeamId aiTeam,
        Vector3Int captureCell,
        out UnitManager assignedCapturer)
    {
        assignedCapturer = null;
        if (plan == null || captureTarget == null) return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Status == ObjectiveStatus.Defending) continue;
            if (obj.Sector != captureTarget.Sector) continue;

            ConstructionManager assignedTarget = FindCapturableInSector(obj.Sector, aiTeam);
            if (assignedTarget != captureTarget) continue;

            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;

                UnitManager candidate = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (candidate == null || candidate == opportunist) continue;
                if (candidate.HasActed || candidate.IsDead || candidate.IsEmbarked || candidate.IsUnderRepair) continue;
                if (!SimulateCaptureSensor(candidate, captureCell, out _)) continue;

                Dictionary<Vector3Int, List<Vector3Int>> candidatePaths =
                    UnitMovementPathRules.CalcularCaminhosValidos(
                        boardTilemap, candidate, Mathf.Max(0, candidate.RemainingMovementPoints), terrainDatabase);

                if (candidatePaths == null || !candidatePaths.ContainsKey(captureCell)) continue;

                assignedCapturer = candidate;
                return true;
            }
        }

        return false;
    }

    private static int GetPathStepCount(Dictionary<Vector3Int, List<Vector3Int>> paths, Vector3Int cell)
    {
        return paths != null && paths.TryGetValue(cell, out List<Vector3Int> path) && path != null
            ? path.Count
            : int.MaxValue;
    }

    private static bool IsAssignedToCaptureTarget(UnitManager unit, TeamObjectivePlan plan, ConstructionManager captureTarget, TeamId aiTeam)
    {
        if (plan == null || captureTarget == null) return false;

        SectorObjective assigned = ResolveAssignedObjective(unit, plan);
        if (assigned == null || assigned.Sector != captureTarget.Sector) return false;

        ConstructionManager assignedTarget = FindCapturableInSector(assigned.Sector, aiTeam, unit.CurrentCellPosition);
        return assignedTarget == captureTarget;
    }

    private bool TryFindBestLoSCell(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int targetCell,
        out Vector3Int bestCell)
    {
        bool preferDpq = unit.TryGetUnitData(out UnitData ud) && ud.prioritizeDpqAtBattle;

        bestCell = Vector3Int.zero;
        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, targetCell, neighbors);

        bool found = false;
        float bestScore = float.MinValue;
        foreach (Vector3Int n in neighbors)
        {
            Vector3Int nc = n; nc.z = 0;
            if (!paths.ContainsKey(nc) || occupied.Contains(nc)) continue;
            float score = preferDpq ? GetTerrainDpqPontos(nc) : GetTerrainEv(nc);
            if (!found || score > bestScore) { bestScore = score; bestCell = nc; found = true; }
        }
        return found;
    }

    // Fase 2: ameaça local por inimigos visíveis dentro do raio ThreatRadius.
    private float CalculateThreatLevel(Vector3Int cell, TeamId aiTeam)
    {
        float threat = 0f;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            float dist = Vector3Int.Distance(cell, ec);
            if (dist <= ThreatRadius)
                threat += (ThreatRadius - dist + 1f) * 10f;
        }
        return threat;
    }

    // Fase 3: verifica se a unidade consegue atacar o alvo a partir de toCell.
    private bool CanAttackTargetFrom(Vector3Int fromCell, Vector3Int toCell,
        UnitManager unit, UnitManager target)
    {
        SensorMovementMode mode = toCell != fromCell
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;

        var targets = new List<PodeMirarTargetOption>();
        bool hasAny = PodeMirarSensor.CollectTargets(
            unit,
            boardTilemap,
            terrainDatabase,
            mode,
            targets,
            dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
            fromCell: toCell);

        if (!hasAny) return false;
        foreach (PodeMirarTargetOption opt in targets)
            if (opt?.targetUnit == target) return true;
        return false;
    }

    private void AppendMissingDpqReachabilityDiagnostics(
        System.Text.StringBuilder log,
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int targetCell)
    {
        if (log == null || unit == null || boardTilemap == null)
            return;

        var cells = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, targetCell, cells);

        foreach (Vector3Int rawCell in cells)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (paths != null && paths.ContainsKey(cell))
                continue;

            float dpq = GetTerrainDpqPontos(cell);
            if (dpq <= 0f)
                continue;

            bool canEnter = UnitMovementPathRules.TryGetEnterCellCost(
                boardTilemap,
                unit,
                cell,
                terrainDatabase,
                applyOperationalAutonomyModifier: true,
                out int enterCost);

            string occupant = DescribeAnyUnitAtCellForDiagnostics(cell);
            string route = DescribeReachabilityFromKnownPaths(unit, paths, cell, enterCost, canEnter);
            log.AppendLine($"  {cell} MISS notReachable dpqPts={dpq:F1} enter={(canEnter ? enterCost.ToString() : "no")} {route} occ={occupant}");
        }
    }

    private void AppendSpecificReachabilityDiagnostics(
        System.Text.StringBuilder log,
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        params Vector3Int[] cells)
    {
        if (log == null || unit == null || cells == null)
            return;

        for (int i = 0; i < cells.Length; i++)
        {
            Vector3Int cell = cells[i];
            cell.z = 0;
            if (paths != null && paths.ContainsKey(cell))
                continue;

            bool canEnter = UnitMovementPathRules.TryGetEnterCellCost(
                boardTilemap,
                unit,
                cell,
                terrainDatabase,
                applyOperationalAutonomyModifier: true,
                out int enterCost);

            string occupant = DescribeAnyUnitAtCellForDiagnostics(cell);
            string route = DescribeReachabilityFromKnownPaths(unit, paths, cell, enterCost, canEnter);
            log.AppendLine($"  {cell} MISS probe dpqPts={GetTerrainDpqPontos(cell):F1} enter={(canEnter ? enterCost.ToString() : "no")} {route} occ={occupant}");
        }
    }

    private string DescribeReachabilityFromKnownPaths(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int blockedCell,
        int enterCost,
        bool canEnter)
    {
        if (unit == null || paths == null || boardTilemap == null)
            return "route=?";

        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;
        int maxMove = Mathf.Max(0, unit.RemainingMovementPoints);
        int maxFuel = Mathf.Max(0, unit.CurrentFuel);

        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, blockedCell, neighbors);

        Vector3Int bestNeighbor = Vector3Int.zero;
        int bestSteps = int.MaxValue;
        int bestPathLen = int.MaxValue;
        string bestOcc = "-";

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int neighbor = neighbors[i];
            neighbor.z = 0;
            if (paths.TryGetValue(neighbor, out List<Vector3Int> path) && path != null)
            {
                int steps = Mathf.Max(0, path.Count - 1);
                if (steps < bestSteps)
                {
                    bestSteps = steps;
                    bestPathLen = path.Count;
                    bestNeighbor = neighbor;
                }
            }
            else if (bestOcc == "-")
            {
                string occ = DescribeAnyUnitAtCellForDiagnostics(neighbor);
                if (occ != "-")
                    bestOcc = $"{neighbor}:{occ}";
            }
        }

        if (bestSteps == int.MaxValue)
            return $"route=noReachableNeighbor maxMove={maxMove} fuel={maxFuel} nearOcc={bestOcc}";

        int estimatedTotal = canEnter ? bestSteps + Mathf.Max(1, enterCost) : int.MaxValue;
        string totalText = canEnter ? estimatedTotal.ToString() : "?";
        return $"route=via {bestNeighbor} pathLen={bestPathLen} stepApprox={bestSteps}+{(canEnter ? enterCost.ToString() : "?")}={totalText} maxMove={maxMove} fuel={maxFuel}";
    }

    private string DescribeAnyUnitAtCellForDiagnostics(Vector3Int cell)
    {
        cell.z = 0;
        UnitManager found = null;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;

            Vector3Int uc = unit.CurrentCellPosition;
            uc.z = 0;
            if (uc != cell)
                continue;

            found = unit;
            break;
        }

        if (found == null)
            return "-";

        Tilemap map = found.BoardTilemap != null ? found.BoardTilemap : boardTilemap;
        Vector3Int worldCell = map != null
            ? HexCoordinates.WorldToCell(map, found.transform.position)
            : found.CurrentCellPosition;
        worldCell.z = 0;
        Vector3Int stateCell = found.CurrentCellPosition;
        stateCell.z = 0;
        bool stale = worldCell != stateCell;

        return $"{found.UnitDisplayName}#{found.InstanceId}/team={found.TeamId}/dead={found.IsDead}/emb={found.IsEmbarked}/acted={found.HasActed}/state={stateCell}/world={worldCell}/stale={stale}";
    }

    private float GetTerrainEv(Vector3Int cell)
    {
        if (boardTilemap == null || terrainDatabase == null) return 0f;
        cell.z = 0;
        TileBase tile = boardTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data) && data != null)
            return data.ev;
        return 0f;
    }

    private float GetTerrainDpqPontos(Vector3Int cell)
    {
        if (boardTilemap == null || terrainDatabase == null) return 0f;
        cell.z = 0;

        PositionDpqForAttackDecision dpq = ResolveDpqForAttackDecision(cell);
        return dpq.points;
    }

    private PositionDpqForAttackDecision ResolveDpqForAttackDecision(Vector3Int cell)
    {
        cell.z = 0;

        if (boardTilemap == null || terrainDatabase == null)
            return PositionDpqForAttackDecision.None;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction != null
            && construction.TryResolveConstructionData(out ConstructionData constructionData)
            && constructionData != null
            && constructionData.dpqData != null)
        {
            return new PositionDpqForAttackDecision(constructionData.dpqData.Pontos, constructionData.dpqData.DefesaBonus);
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardTilemap, cell);
        if (structure != null && structure.dpqData != null)
            return new PositionDpqForAttackDecision(structure.dpqData.Pontos, structure.dpqData.DefesaBonus);

        TileBase tile = boardTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data) && data?.dpqData != null)
            return new PositionDpqForAttackDecision(data.dpqData.Pontos, data.dpqData.DefesaBonus);

        GridLayout grid = boardTilemap.layoutGrid;
        if (grid != null)
        {
            Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
            for (int i = 0; i < maps.Length; i++)
            {
                Tilemap map = maps[i];
                if (map == null || map == boardTilemap)
                    continue;

                TileBase other = map.GetTile(cell);
                if (other != null && terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData otherData) && otherData?.dpqData != null)
                    return new PositionDpqForAttackDecision(otherData.dpqData.Pontos, otherData.dpqData.DefesaBonus);
            }
        }

        return PositionDpqForAttackDecision.None;
    }

    private PositionDpqForAttackDecision ResolveDpqForAttackDecision(UnitManager unit, Vector3Int cell)
    {
        if (unit != null
            && unit.GetDomain() == Domain.Air
            && turnStateManager != null
            && turnStateManager.DpqAirHeightConfigRef != null
            && turnStateManager.DpqAirHeightConfigRef.TryGetFor(unit.GetDomain(), unit.GetHeightLevel(), out DPQData airDpq)
            && airDpq != null)
        {
            return new PositionDpqForAttackDecision(airDpq.Pontos, airDpq.DefesaBonus);
        }

        return ResolveDpqForAttackDecision(cell);
    }

    private readonly struct PositionDpqForAttackDecision
    {
        public static PositionDpqForAttackDecision None => new PositionDpqForAttackDecision(0, 0);

        public readonly int points;
        public readonly int defenseBonus;

        public PositionDpqForAttackDecision(int points, int defenseBonus)
        {
            this.points = Mathf.Max(0, points);
            this.defenseBonus = defenseBonus;
        }
    }

    private static bool IsBetterAttackCandidate(
        bool preferDpqAtBattle,
        float targetPriority,
        float attackDpq,
        float score,
        float sectorTie,
        float hqTie,
        float bestTargetPriority,
        float bestAttackDpq,
        float bestScore,
        float bestSectorTie,
        float bestHqTie)
    {
        const float epsilon = 0.001f;

        if (preferDpqAtBattle)
        {
            if (targetPriority > bestTargetPriority + epsilon) return true;
            if (Mathf.Abs(targetPriority - bestTargetPriority) > epsilon) return false;

            if (attackDpq > bestAttackDpq + epsilon) return true;
            if (Mathf.Abs(attackDpq - bestAttackDpq) > epsilon) return false;
        }

        return IsBetterScore(score, sectorTie, hqTie, bestScore, bestSectorTie, bestHqTie);
    }

    private static bool IsBetterScore(float score, float sectorTie, float hqTie,
                                       float bestScore, float bestSectorTie, float bestHqTie)
    {
        const float epsilon = 0.001f;
        if (score > bestScore + epsilon) return true;
        if (Mathf.Abs(score - bestScore) > epsilon) return false;
        // desempate 1: setor atribuído (menor distância ao alvo do setor)
        if (sectorTie > bestSectorTie + epsilon) return true;
        if (Mathf.Abs(sectorTie - bestSectorTie) > epsilon) return false;
        // desempate 2: HQ inimigo
        return hqTie > bestHqTie + epsilon;
    }

    private static float CalculateEnemyHqDistance(Vector3Int cell, AIWorldSnapshot snapshot, UnitManager unit)
    {
        if (snapshot == null || snapshot.EnemyHQ == null)
            return float.MaxValue;

        Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition;
        hq.z = 0;
        cell.z = 0;

        if (unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && SectorManager.TryGetLandMovementDistance(cell, hq, unitData, out int movementCost))
        {
            return movementCost;
        }

        if (SectorManager.TryGetLandMovementDistance(cell, hq, out int fallbackCost))
            return fallbackCost;

        return SectorManager.HexDistance(cell, hq);
    }

    private static bool TryCalculateRouteDistance(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell, out float distance)
    {
        distance = 0f;
        fromCell.z = 0;
        targetCell.z = 0;

        if (unit != null && unit.GetDomain() == Domain.Air)
        {
            distance = SectorManager.HexDistance(fromCell, targetCell);
            return true;
        }

        if (unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && SectorManager.TryGetLandMovementDistance(fromCell, targetCell, unitData, out int unitCost))
        {
            distance = unitCost;
            return true;
        }

        if (SectorManager.TryGetLandMovementDistance(fromCell, targetCell, out int fallbackCost))
        {
            distance = fallbackCost;
            return true;
        }

        return false;
    }

    private static float CalculateRouteDistanceOrHex(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell)
    {
        return TryCalculateRouteDistance(unit, fromCell, targetCell, out float routeDistance)
            ? routeDistance
            : SectorManager.HexDistance(fromCell, targetCell);
    }

    private static float CalculateEnemyHqTieBreak(float hqDistance)
    {
        return hqDistance < float.MaxValue ? -hqDistance : 0f;
    }

    private static bool IsBetterRogueAdvance(Vector3Int from, Vector3Int target, Vector3Int candidate, float candidateHexDist, Vector3Int currentBest, float bestHexDist)
    {
        const float epsilon = 0.001f;
        if (candidateHexDist < bestHexDist - epsilon)
            return true;
        if (candidateHexDist > bestHexDist + epsilon)
            return false;

        float candidateLine = CalculateLineProgressTieBreak(from, target, candidate);
        float bestLine = CalculateLineProgressTieBreak(from, target, currentBest);
        if (candidateLine > bestLine + epsilon)
            return true;
        if (candidateLine < bestLine - epsilon)
            return false;

        return Vector3Int.Distance(candidate, target) < Vector3Int.Distance(currentBest, target) - epsilon;
    }

    private static float CalculateLineProgressTieBreak(Vector3Int from, Vector3Int target, Vector3Int candidate)
    {
        Vector2 origin = new Vector2(from.x, from.y);
        Vector2 goal = new Vector2(target.x, target.y);
        Vector2 point = new Vector2(candidate.x, candidate.y);
        Vector2 direction = goal - origin;
        float lengthSq = direction.sqrMagnitude;
        if (lengthSq <= 0.001f)
            return 0f;

        Vector2 advanced = point - origin;
        float projection = Vector2.Dot(advanced, direction.normalized);
        float lateral = Mathf.Abs(direction.x * advanced.y - direction.y * advanced.x) / Mathf.Sqrt(lengthSq);
        return projection - lateral * 0.25f;
    }

    private static SectorObjective ResolveAssignedObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Capturador && slot.Filled && slot.AssignedUnitId == unit.InstanceId) return obj;
        return null;
    }


    // Replica PodeCapturarSensor em um hex simulado sem mover a unidade.
    private bool SimulateCaptureSensor(UnitManager unit, Vector3Int simulatedCell,
        out ConstructionManager targetConstruction)
    {
        targetConstruction = null;
        if (!unit.TryGetUnitData(out UnitData data)) return false;
        if (data.roles == null || !data.roles.Contains(UnitRole.Capturador)) return false;
        if (unit.TeamId == TeamId.Neutral) return false;

        ConstructionManager c = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, simulatedCell);
        if (c == null || !c.IsCapturable || c.CapturePointsMax <= 0) return false;
        if (c.TeamId == unit.TeamId && c.CurrentCapturePoints >= c.CapturePointsMax) return false;

        targetConstruction = c;
        return true;
    }

    public static ConstructionManager FindCapturableInSector(ConstructionSector sector, TeamId aiTeam, Vector3Int? unitPos = null)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.Sector != sector || !c.IsCapturable) continue;
            if (c.TeamId == aiTeam && c.CurrentCapturePoints >= c.CapturePointsMax) continue;

            if (unitPos == null) return c;

            Vector3Int tc = c.CurrentCellPosition; tc.z = 0;
            float dist = Vector3Int.Distance(unitPos.Value, tc);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }

        return best;
    }

    private static List<UnitManager> GetAvailableCapturers(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (data.roles != null && data.roles.Contains(UnitRole.Capturador))
                list.Add(u);
        }
        return list;
    }
}
