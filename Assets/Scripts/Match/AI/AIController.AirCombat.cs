using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction TryDecideAirCombatAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        if (!IsAirCombatUnit(unit) || snapshot == null)
            return null;

        bool wasGrounded = unit.IsAircraftGrounded;
        if (wasGrounded)
            unit.SetAircraftGrounded(false);

        try
        {
            return DecideRogueAirCombatAction(unit, snapshot);
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

    private PlayerAction DecideRogueAirCombatAction(UnitManager unit, AIWorldSnapshot snapshot)
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

        if (TryFindAirCombatAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                out Vector3Int attackCell, out UnitManager target, out string attackReason))
        {
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("AirCombat")} {unit.InstanceId} rogue ataca via {attackCell} -> {target.UnitDisplayName}#{target.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                target.InstanceId.ToString(), targetCell, paths);
        }

        Vector3Int moveCell = FindAirCombatAdvanceMove(fromCell, anchor, paths, occupied, snapshot.AITeam);
        Debug.Log($"{TL("AirCombat")} {unit.InstanceId} rogue avanca via {moveCell} alvo={anchor}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
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

            foreach (UnitManager enemy in UnitManager.AllActive)
            {
                if (enemy == null || enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked)
                    continue;
                if (matchController != null && !matchController.IsUnitVisibleForTeam(enemy, snapshot.AITeam))
                    continue;
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy))
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
                float score =
                    GetAirCombatTargetPreferenceScore(targetPreference)
                    + combatScore
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 700f
                    - SectorManager.HexDistance(enemyCell, anchor) * 350f
                    - SectorManager.HexDistance(cell, anchor) * 40f
                    - GetPathStepCount(paths, cell) * 8f
                    - enemy.InstanceId * 0.001f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = enemy;
                    reason = $"score={score:F0} pref={targetPreference} hp={enemy.CurrentHP}{combatScoreReason} {attackDecisionReason}";
                }
            }
        }

        return bestTarget != null;
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
        TeamId aiTeam)
    {
        Vector3Int bestCell = fromCell;
        float bestDist = SectorManager.HexDistance(fromCell, targetCell);
        float bestThreat = CalculateThreatLevel(fromCell, aiTeam);
        const float eps = 0.01f;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell || occupied.Contains(cell))
                continue;

            float dist = SectorManager.HexDistance(cell, targetCell);
            float threat = CalculateThreatLevel(cell, aiTeam);

            bool isBetter = dist < bestDist - eps
                || (dist < bestDist + eps && threat < bestThreat - eps);

            if (!isBetter)
                continue;

            bestCell = cell;
            bestDist = dist;
            bestThreat = threat;
        }

        return bestCell;
    }
}
