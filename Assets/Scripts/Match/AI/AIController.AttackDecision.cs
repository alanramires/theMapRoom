using UnityEngine;

public partial class AIController
{
    private bool TrySimulateAttackForAI(
        UnitManager attacker,
        UnitManager target,
        Vector3Int attackCell,
        out CombatEvaluationResult evaluation)
    {
        evaluation = CombatEvaluationResult.Invalid;
        if (attacker == null || target == null)
            return false;

        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return false;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return false;
        if (turnStateManager == null
            || turnStateManager.RpsDatabaseRef == null
            || turnStateManager.DpqMatchupDatabaseRef == null
            || turnStateManager.WeaponPriorityDataRef == null)
            return false;

        CombatEvaluationRequest request = CreateCombatEvaluationRequest(
            attacker,
            target,
            attackCell,
            defensiveContext: false);
        PopulateCombatEvaluationInputs(request);
        return CombatEvaluationService.TryEvaluate(request, out evaluation);
    }

    private CombatEvaluationRequest CreateCombatEvaluationRequest(
        UnitManager attacker,
        UnitManager target,
        Vector3Int attackCell,
        bool defensiveContext)
    {
        attackCell.z = 0;
        return new CombatEvaluationRequest
        {
            Attacker = attacker,
            Target = target,
            AttackCell = attackCell,
            RpsDatabase = turnStateManager != null ? turnStateManager.RpsDatabaseRef : null,
            DpqMatchupDatabase = turnStateManager != null ? turnStateManager.DpqMatchupDatabaseRef : null,
            WeaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null,
            DefensiveContext = defensiveContext,
            AllowLegacyAutomaticWeaponFallback = true
        };
    }

    private void PopulateCombatEvaluationInputs(CombatEvaluationRequest request)
    {
        if (request == null || request.Attacker == null || request.Target == null)
            return;

        Vector3Int targetCell = request.Target.CurrentCellPosition;
        targetCell.z = 0;
        request.AttackerDpq = ResolveDpqForAttackDecision(request.Attacker, request.AttackCell);
        request.DefenderDpq = ResolveDpqForAttackDecision(request.Target, targetCell);
        request.SensorOption = TryFindAttackDecisionOption(
            request.Attacker,
            request.Target,
            request.AttackCell,
            out PodeMirarTargetOption option)
            ? option
            : null;
    }

    private bool TryFindAttackDecisionOption(
        UnitManager attacker,
        UnitManager target,
        Vector3Int attackCell,
        out PodeMirarTargetOption option)
    {
        option = null;
        if (attacker == null || target == null)
            return false;

        Vector3Int fromCell = attacker.CurrentCellPosition;
        fromCell.z = 0;
        attackCell.z = 0;
        SensorMovementMode mode = attackCell != fromCell
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;

        var targets = new System.Collections.Generic.List<PodeMirarTargetOption>();
        bool hasAny = PodeMirarSensor.CollectTargets(
            attacker,
            boardTilemap,
            terrainDatabase,
            mode,
            targets,
            weaponPriorityData: turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null,
            dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
            fromCell: attackCell);

        if (!hasAny || targets.Count == 0)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            PodeMirarTargetOption candidate = targets[i];
            if (candidate == null || candidate.targetUnit != target)
                continue;

            option = candidate;
            return option.weapon != null;
        }

        return false;
    }

    private bool PassesAttackDecision(
        UnitManager attacker,
        UnitManager target,
        Vector3Int attackCell,
        bool defensiveContext,
        out string reason)
    {
        AttackDecisionResult result = EvaluateAttackDecision(
            attacker,
            target,
            attackCell,
            defensiveContext);
        reason = result.Reason;
        return result.IsAllowed;
    }

    private AttackDecisionResult EvaluateAttackDecision(
        UnitManager attacker,
        UnitManager target,
        Vector3Int attackCell,
        bool defensiveContext)
    {
        CombatEvaluationRequest request = CreateCombatEvaluationRequest(
            attacker,
            target,
            attackCell,
            defensiveContext);

        if (CombatEvaluationService.TryEvaluateAttackDecisionPreconditions(
                request,
                out AttackDecisionResult terminal))
        {
            return terminal;
        }

        PopulateCombatEvaluationInputs(request);
        return CombatEvaluationService.EvaluateAttackDecision(request);
    }
}
