using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------

    // Decisão de ação de unidade

    // -------------------------------------------------------------------------

    private PlayerAction DecideUnitAction(UnitManager unit, AIWorldSnapshot snapshot)

    {

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);

        if (TryFindProductionUnlockVacateAction(unit, snapshot, out PlayerAction productionUnlockAction))
            return productionUnlockAction;

        if (plan != null)

        {

            PlayerAction objectiveAction = TryDecideCapturerAction(unit, snapshot, plan);

            if (objectiveAction != null) return objectiveAction;

            bool preferFireSupportFirst = PreferFireSupportBeforeAssault(unit);

            if (preferFireSupportFirst)
            {
                PlayerAction earlyFireSupportAction = TryDecideFireSupportAction(unit, snapshot, plan);
                if (earlyFireSupportAction != null) return earlyFireSupportAction;
            }

            PlayerAction assaultAction = TryDecideAssaultAction(unit, snapshot, plan);

            if (assaultAction != null) return assaultAction;

            if (!preferFireSupportFirst)
            {
                PlayerAction fireSupportAction = TryDecideFireSupportAction(unit, snapshot, plan);
                if (fireSupportAction != null) return fireSupportAction;
            }

        }

        PlayerAction airCombatAction = TryDecideAirCombatAction(unit, snapshot);

        if (airCombatAction != null) return airCombatAction;

        PlayerAction logisticsAction = TryDecideLogisticsAction(unit, snapshot, plan);

        if (logisticsAction != null) return logisticsAction;

        PlayerAction transportAction = TryDecideTransportadorAction(unit, snapshot, plan);

        if (transportAction != null) return transportAction;

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        Dictionary<Vector3Int, List<Vector3Int>> paths =

            UnitMovementPathRules.CalcularCaminhosValidos(

                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        var freeCells = new List<Vector3Int>();

        if (paths != null)

            foreach (Vector3Int cell in paths.Keys)

                if (!occupied.Contains(cell))

                    freeCells.Add(cell);

        List<HexEvaluation> evaluations = HexEvaluator.Evaluate(

            unit, snapshot.AITeam, fromCell, freeCells,

            boardTilemap, terrainDatabase,

            out CandidateType resolvedRole,

            out Vector3Int resolvedTarget,

            out bool hasTarget,

            turnStateManager);

        HexEvaluation chosen = default;

        bool foundChosen = false;

        foreach (HexEvaluation e in evaluations)

        {

            if (e.isChosen) { chosen = e; foundChosen = true; break; }

        }

        if (showAIUnitHUD)

        {

            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"{TL("Think")} Unidade {unit.InstanceId} ({unit.UnitDisplayName}) | role={resolvedRole} target={resolvedTarget}");

            foreach (HexEvaluation e in evaluations)

                sb.AppendLine($"  {(e.isChosen ? "★" : " ")} {e.cell} | total={e.total:F2}" +

                              $"  cap={e.captureProximity:F2} cbt={e.combatValue:F2} dpq={e.positionQuality:F2}" +

                              $"  coh={e.cohesion:F2} dev={e.deviation:F2} saf={e.safety:F2}" +

                              $"  → {e.actionSummary}");

            Debug.Log(sb.ToString());

        }

        if (!foundChosen)

        {

            Debug.LogWarning($"[AI] {unit.InstanceId}: HexEvaluator sem vencedor — aguardando no lugar.");

            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        }

        Vector3Int destCell = chosen.cell;

        // 1. Captura: contexto aponta que devemos capturar neste hex

        bool isCaptureContext = chosen.type == CandidateType.CaptureNow

            || (chosen.type == CandidateType.CaptureAdvance && hasTarget && destCell == resolvedTarget);

        if (isCaptureContext)

        {

            bool canCapture = unit.TryGetUnitData(out UnitData hexUnitData)
                && hexUnitData.roles != null && hexUnitData.roles.Count > 0
                && hexUnitData.roles.Contains(UnitRole.Capturador);

            if (canCapture)
            {

            Debug.Log($"[AI] {unit.InstanceId} → captura @ {destCell}");

            return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, destCell, paths);

            }

        }

        // 2. Ataque: posição escolhida tem valor de combate

        if (chosen.combatValue > 0f)

        {

            bool hasMoved = destCell != fromCell;

            var attackCandidates = FindAttackTargetsSorted(unit, destCell, hasMoved);

            if (attackCandidates != null)

            {

                foreach (var (target, _) in attackCandidates)

                {

                    if (target?.targetUnit == null) continue;

                    if (!PassesAttackDecision(unit, target.targetUnit, destCell, false, out string atkReason))
                    {
                        Debug.Log($"[AI] {unit.InstanceId} → ataque bloqueado por AttackDecision ({target.targetUnit.InstanceId}): {atkReason}");
                        continue;
                    }

                    Vector3Int targetCell = target.targetUnit.CurrentCellPosition; targetCell.z = 0;

                    Debug.Log($"[AI] {unit.InstanceId} → ataca {target.targetUnit.InstanceId} de {destCell}");

                    return BuildAttackBatch(

                        unit, snapshot.AITeam, fromCell, destCell,

                        target.targetUnit.InstanceId.ToString(), targetCell, paths);

                }

            }

        }

        // 3. Movimento simples

        Debug.Log($"[AI] {unit.InstanceId} → move para {destCell}");

        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, destCell, paths);

    }

    private static bool PreferFireSupportBeforeAssault(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return data.preferArtilleryModeBeforeCombatant
            && data.roles != null
            && data.roles.Contains(UnitRole.FogoIndireto);
    }

    private List<(PodeMirarTargetOption opt, int score)> FindAttackTargetsSorted(UnitManager unit, Vector3Int fromCell, bool hasMoved)

    {

        var targets = new List<PodeMirarTargetOption>();

        SensorMovementMode mode = hasMoved

            ? SensorMovementMode.MoveuAndando

            : SensorMovementMode.MoveuParado;

        bool hasAny = PodeMirarSensor.CollectTargets(

            unit, boardTilemap, terrainDatabase, mode, targets, fromCell: fromCell);

        if (!hasAny || targets.Count == 0) return null;

        unit.TryGetUnitData(out UnitData attackerData);

        bool isCapturador = attackerData != null && attackerData.roles != null

            && attackerData.roles.Contains(UnitRole.Capturador);

        var scored = new List<(PodeMirarTargetOption opt, int score)>();

        foreach (PodeMirarTargetOption opt in targets)

        {

            if (opt?.targetUnit == null || opt.targetUnit.IsDead) continue;

            int score = 0;

            // Capturadores priorizam inimigos sobre construções

            if (isCapturador)

            {

                Vector3Int ec = opt.targetUnit.CurrentCellPosition; ec.z = 0;

                if (ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, ec) != null)

                    score += 10000;

            }

            // Preferir inimigos com HP baixo (mais fáceis de eliminar)

            score += (10 - opt.targetUnit.CurrentHP) * 200;

            // Heurística simples: alvo com ≤ 2 HP provavelmente morre

            if (opt.targetUnit.CurrentHP <= 2)

                score += 5000;

            // Penalidade por distância

            score -= opt.distance * 50;

            scored.Add((opt, score));

        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));

        return scored;

    }
}
