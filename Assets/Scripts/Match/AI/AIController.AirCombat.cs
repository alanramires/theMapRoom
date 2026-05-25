using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIController
{
    private PlayerAction TryDecideAirCombatAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        if (!IsAirCombatUnit(unit) || snapshot == null)
            return null;

        bool wasGrounded = unit.IsAircraftGrounded;
        List<int> takeoffMoveOptions = null;
        if (wasGrounded && !TryGetAITakeoffMoveOptions(unit, out takeoffMoveOptions, out string takeoffReason))
        {
            Debug.Log($"{TL("AirCombat")} {unit.InstanceId} sem acao: decolagem indisponivel ({takeoffReason})");
            return null;
        }

        if (wasGrounded)
            unit.SetAircraftGrounded(false);

        try
        {
            return DecideRogueAirCombatAction(unit, snapshot, takeoffMoveOptions);
        }
        finally
        {
            if (wasGrounded)
                unit.SetAircraftGrounded(true);
        }
    }

    private static bool IsAirCombatUnit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        if (data.domain != Domain.Air || data.roles == null || data.roles.Count == 0)
            return false;

        return data.roles[0] == UnitRole.AtaqueAereo
            || data.roles[0] == UnitRole.Interceptador;
    }

    private PlayerAction DecideRogueAirCombatAction(UnitManager unit, AIWorldSnapshot snapshot, List<int> takeoffMoveOptions = null)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildAirOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        Vector3Int anchor = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : ResolveAirCombatFallbackAnchor(snapshot, fromCell);
        anchor.z = 0;

        if (TryFindAirCombatAttack(unit, snapshot, fromCell, paths, occupied, anchor, takeoffMoveOptions,
                out Vector3Int attackCell, out UnitManager target, out string attackReason))
        {
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("AirCombat")} {unit.InstanceId} rogue ataca via {attackCell} -> {target.UnitDisplayName}#{target.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                target.InstanceId.ToString(), targetCell, paths);
        }

        Vector3Int moveCell = FindAirCombatAdvanceMove(fromCell, anchor, paths, occupied, snapshot.AITeam, takeoffMoveOptions);
        Debug.Log($"{TL("AirCombat")} {unit.InstanceId} rogue avanca via {moveCell} alvo={anchor}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
    }

    private bool TryGetAITakeoffMoveOptions(UnitManager unit, out List<int> moveOptions, out string reason)
    {
        moveOptions = null;
        reason = string.Empty;

        Tilemap boardMap = boardTilemap != null ? boardTilemap : unit != null ? unit.BoardTilemap : null;
        PodeDecolarReport takeoff = PodeDecolarSensor.Evaluate(unit, boardMap, terrainDatabase);
        reason = takeoff != null ? takeoff.explicacao : "sensor indisponivel";

        if (takeoff == null || takeoff.takeoffMoveOptions == null || takeoff.takeoffMoveOptions.Count == 0)
            return false;

        if (!takeoff.status)
            return false;

        moveOptions = new List<int>(takeoff.takeoffMoveOptions);
        return true;
    }

    private Vector3Int ResolveAirCombatFallbackAnchor(AIWorldSnapshot snapshot, Vector3Int fromCell)
    {
        if (snapshot != null && snapshot.EnemyBuildings != null && snapshot.EnemyBuildings.Count > 0)
        {
            ConstructionManager best = null;
            float bestDist = float.MaxValue;
            foreach (ConstructionManager building in snapshot.EnemyBuildings)
            {
                if (building == null) continue;
                Vector3Int cell = building.CurrentCellPosition;
                cell.z = 0;
                float dist = SectorManager.HexDistance(fromCell, cell);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = building;
                }
            }

            if (best != null)
            {
                Vector3Int cell = best.CurrentCellPosition;
                cell.z = 0;
                return cell;
            }
        }

        return fromCell;
    }

    private bool TryFindAirCombatAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int anchor,
        List<int> takeoffMoveOptions,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        out string reason)
    {
        bestCell = fromCell;
        bestTarget = null;
        reason = "";
        float bestScore = float.MinValue;
        MatchController matchController = GetMatchController();

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied.Contains(cell))
                continue;
            if (!IsAITakeoffDestinationAllowed(paths, cell, takeoffMoveOptions))
                continue;

            foreach (UnitManager enemy in UnitManager.AllActive)
            {
                if (enemy == null || enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked)
                    continue;
                if (matchController != null && !matchController.IsUnitVisibleForTeam(enemy, snapshot.AITeam))
                    continue;
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy))
                    continue;
                if (!TryFindAttackDecisionOption(unit, enemy, cell, out PodeMirarTargetOption attackOption))
                    continue;
                if (!PassesAttackDecision(unit, enemy, cell, false, out string attackDecisionReason))
                    continue;
                float combatScore = 0f;
                string combatScoreReason = "";
                if (TrySimulateAttackForAI(unit, enemy, cell, out AIAttackSimulationSummary simSummary))
                {
                    combatScore =
                        (simSummary.result.killGuaranteed ? 26000f : 0f)
                        + simSummary.targetDamagePct * 420f
                        + simSummary.targetDamage * 1100f
                        - simSummary.attackerLossPct * 260f
                        - simSummary.attackerLoss * 900f;
                    combatScoreReason =
                        $" combatScore={combatScore:F0} kill={simSummary.result.killGuaranteed} dmg={simSummary.targetDamage}/{simSummary.targetDamagePct}% loss={simSummary.attackerLoss}/{simSummary.attackerLossPct}%";
                }

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                BazookaTargetPriority targetPreference = ResolveAirCombatTargetPreference(unit, enemy);
                bool attacksFromCurrentCell = cell == fromCell;
                float targetValueScore = ScoreAirCombatTargetValue(enemy, attackOption, unit);
                float score =
                    GetAirCombatTargetPreferenceScore(targetPreference)
                    + targetValueScore
                    + combatScore
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 700f
                    + (attacksFromCurrentCell ? 5000f : 0f)
                    - SectorManager.HexDistance(enemyCell, anchor) * 350f
                    - SectorManager.HexDistance(cell, anchor) * 40f
                    - GetPathStepCount(paths, cell) * 8f
                    - enemy.InstanceId * 0.001f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = enemy;
                    string weaponName = attackOption.weapon != null ? ResolveWeaponName(attackOption.weapon) : "semArma";
                    reason = $"score={score:F0} pref={targetPreference} value={targetValueScore:F0} weapon={weaponName} hp={enemy.CurrentHP} noMove={attacksFromCurrentCell}{combatScoreReason} {attackDecisionReason}";
                }
            }
        }

        return bestTarget != null;
    }

    private float ScoreAirCombatTargetValue(UnitManager target, PodeMirarTargetOption option, UnitManager attacker)
    {
        if (target == null || !target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return 0f;

        float score = targetData.cost * 1.2f + targetData.eliteLevel * 6000f;

        bool isAirAttack = targetData.roles != null
            && targetData.roles.Count > 0
            && targetData.roles[0] == UnitRole.AtaqueAereo;
        bool isTransport = targetData.roles != null
            && targetData.roles.Count > 0
            && targetData.roles[0] == UnitRole.Transportador;

        if (targetData.domain == Domain.Air && isAirAttack)
            score += targetData.eliteLevel >= 1 ? 18000f : 9000f;
        if (targetData.domain == Domain.Air && targetData.unitClass == GameUnitClass.Plane)
            score += 4500f;
        if (targetData.domain == Domain.Air && isTransport)
            score -= 6000f;

        WeaponPriorityData weaponPriorityData = turnStateManager != null
            ? turnStateManager.WeaponPriorityDataRef
            : null;

        if (option != null && option.weapon != null
            && PodeMirarSensor.IsPreferredWeaponForTarget(weaponPriorityData, option.weapon, targetData.unitClass))
        {
            score += 6500f;
        }

        return score;
    }

    private static string ResolveWeaponName(WeaponData weapon)
    {
        if (weapon == null)
            return "-";
        if (!string.IsNullOrWhiteSpace(weapon.apelido))
            return weapon.apelido.Trim();
        if (!string.IsNullOrWhiteSpace(weapon.displayName))
            return weapon.displayName.Trim();
        if (!string.IsNullOrWhiteSpace(weapon.id))
            return weapon.id.Trim();
        return weapon.name;
    }

    private static BazookaTargetPriority ResolveAirCombatTargetPreference(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return BazookaTargetPriority.Tertiary;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return BazookaTargetPriority.Tertiary;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return BazookaTargetPriority.Tertiary;

        return attackerData.ResolveAiTargetPriorityForTargetClass(targetData.unitClass);
    }

    private static float GetAirCombatTargetPreferenceScore(BazookaTargetPriority priority)
    {
        switch (priority)
        {
            case BazookaTargetPriority.Primary:
                return 30000f;
            case BazookaTargetPriority.Secondary:
                return 15000f;
            default:
                return 0f;
        }
    }

    private Vector3Int FindAirCombatAdvanceMove(
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam,
        List<int> takeoffMoveOptions = null)
    {
        Vector3Int bestCell = fromCell;
        float startDist = SectorManager.HexDistance(fromCell, targetCell);
        float bestScore = float.MinValue;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell || occupied.Contains(cell))
                continue;
            if (!IsAITakeoffDestinationAllowed(paths, cell, takeoffMoveOptions))
                continue;

            float dist = SectorManager.HexDistance(cell, targetCell);
            float progress = startDist - dist;
            if (progress <= 0f)
                continue;

            float threat = CalculateThreatLevel(cell, aiTeam);
            float routeLineDeviation = DistanceFromHexLine(cell, fromCell, targetCell);
            int pathSteps = GetPathStepCount(paths, cell);

            float score =
                progress * 2400f
                - dist * 180f
                - routeLineDeviation * 420f
                - threat * 120f
                - pathSteps * 3f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    private static float DistanceFromHexLine(Vector3Int cell, Vector3Int lineStart, Vector3Int lineEnd)
    {
        Vector2 p = new Vector2(cell.x, cell.y);
        Vector2 a = new Vector2(lineStart.x, lineStart.y);
        Vector2 b = new Vector2(lineEnd.x, lineEnd.y);
        Vector2 ab = b - a;
        float abLenSq = ab.sqrMagnitude;
        if (abLenSq <= 0.0001f)
            return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
        Vector2 projection = a + ab * t;
        return Vector2.Distance(p, projection);
    }

    private static bool IsAITakeoffDestinationAllowed(
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int cell,
        List<int> takeoffMoveOptions)
    {
        if (takeoffMoveOptions == null || takeoffMoveOptions.Count == 0)
            return true;

        if (takeoffMoveOptions.Contains(9) || takeoffMoveOptions.Contains(-1))
            return true;

        int movementHexes = 0;
        if (paths != null && paths.TryGetValue(cell, out List<Vector3Int> path) && path != null)
            movementHexes = Mathf.Max(0, path.Count - 1);

        return takeoffMoveOptions.Contains(movementHexes);
    }
}
