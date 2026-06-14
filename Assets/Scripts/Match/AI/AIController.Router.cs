using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------

    // Definição parcial da classe AIController, organizada em múltiplos arquivos para melhor legibilidade 
    // e manutenção. Cada arquivo foca em um aspecto específico do comportamento da IA, como ciclo de vida,
    // tomada de decisões, avaliação de hexágonos e interação com o sistema de objetivos.
    // A classe é responsável por controlar a IA inimiga, incluindo a execução de suas ações, 
    // planejamento de objetivos e tomada de decisões, utilizando uma abordagem baseada
    //  em estágios para organizar seu comportamento.    

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
                PlayerAction earlyFireSupportAction = IsPrimaryAssaultFireSupportHybrid(unit)
                    ? TryDecideFireSupportAttackOnlyAction(unit, snapshot, plan)
                    : TryDecideFireSupportAction(unit, snapshot, plan);
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

                sb.AppendLine($"  {(e.isChosen ? "â˜…" : " ")} {e.cell} | total={e.total:F2}" +

                              $"  cap={e.captureProximity:F2} cbt={e.combatValue:F2} dpq={e.positionQuality:F2}" +

                              $"  coh={e.cohesion:F2} dev={e.deviation:F2} saf={e.safety:F2}" +

                              $"  â†’ {e.actionSummary}");

            Debug.Log(sb.ToString());

        }

        if (!foundChosen)

        {

            Debug.LogWarning($"[AI] {unit.InstanceId}: HexEvaluator sem vencedor â€” aguardando no lugar.");

            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        }

        Vector3Int destCell = chosen.cell;

        if (IsReservedCaptureCellForAnotherUnit(unit, snapshot.AITeam, destCell, paths, out UnitManager reservedFor))
        {
            Debug.Log($"[AI] {unit.InstanceId} evita mover para captura reservada @ {destCell} por {reservedFor.InstanceId}");
            if (TrySelectFallbackHexEvaluation(unit, snapshot.AITeam, evaluations, paths, out HexEvaluation fallbackChosen))
            {
                chosen = fallbackChosen;
                destCell = chosen.cell;
            }
            else
            {
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
            }
        }

        // 1. Captura: contexto aponta que devemos capturar neste hex

        bool isCaptureContext = chosen.type == CandidateType.CaptureNow

            || (chosen.type == CandidateType.CaptureAdvance && hasTarget && destCell == resolvedTarget);

        if (!isCaptureContext
            && chosen.type == CandidateType.CaptureAdvance
            && SimulateCaptureSensor(unit, destCell, out _)
            && !IsReservedCaptureCellForAnotherUnit(unit, snapshot.AITeam, destCell, paths, out _))
        {
            isCaptureContext = true;
        }

        if (isCaptureContext)

        {

            bool canCapture = unit.TryGetUnitData(out UnitData hexUnitData)
                && hexUnitData.roles != null && hexUnitData.roles.Count > 0
                && hexUnitData.roles.Contains(UnitRole.Capturador);

            if (canCapture)
            {

            Debug.Log($"[AI] {unit.InstanceId} â†’ captura @ {destCell}");

            return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, destCell, paths);

            }

        }

        // 2. Ataque: posiÃ§Ã£o escolhida tem valor de combate

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
                        Debug.Log($"[AI] {unit.InstanceId} â†’ ataque bloqueado por AttackDecision ({target.targetUnit.InstanceId}): {atkReason}");
                        continue;
                    }

                    Vector3Int targetCell = target.targetUnit.CurrentCellPosition; targetCell.z = 0;

                    Debug.Log($"[AI] {unit.InstanceId} â†’ ataca {target.targetUnit.InstanceId} de {destCell}");

                    return BuildAttackBatch(

                        unit, snapshot.AITeam, fromCell, destCell,

                        target.targetUnit.InstanceId.ToString(), targetCell, paths);

                }

            }

            if (TryBuildFallbackAttackFromEvaluations(
                    unit, snapshot, fromCell, paths, evaluations, destCell, out PlayerAction fallbackAttack))
                return fallbackAttack;

        }

        // 3. Movimento simples

        Debug.Log($"[AI] {unit.InstanceId} â†’ move para {destCell}");

        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, destCell, paths);

    }

    private bool IsReservedCaptureCellForAnotherUnit(
        UnitManager unit,
        TeamId aiTeam,
        Vector3Int cell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out UnitManager reservedFor)
    {
        reservedFor = null;
        if (unit == null || paths == null)
            return false;
        cell.z = 0;
        if (!paths.ContainsKey(cell))
            return false;
        if (!SimulateCaptureSensor(unit, cell, out _))
            return false;
        return ShouldReserveOpportunisticCaptureForCloserUnit(unit, aiTeam, cell, paths, out reservedFor);
    }

    private bool TrySelectFallbackHexEvaluation(
        UnitManager unit,
        TeamId aiTeam,
        List<HexEvaluation> evaluations,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out HexEvaluation fallback)
    {
        fallback = default;
        if (evaluations == null)
            return false;

        bool found = false;
        float bestTotal = float.MinValue;
        foreach (HexEvaluation candidate in evaluations)
        {
            if (candidate.isChosen)
                continue;
            if (IsReservedCaptureCellForAnotherUnit(unit, aiTeam, candidate.cell, paths, out _))
                continue;
            if (!found || candidate.total > bestTotal)
            {
                fallback = candidate;
                bestTotal = candidate.total;
                found = true;
            }
        }

        return found;
    }
    private static bool PreferFireSupportBeforeAssault(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return data.preferArtilleryModeBeforeCombatant
            && data.roles != null
            && data.roles.Contains(UnitRole.FogoIndireto);
    }

    private static bool IsPrimaryAssaultFireSupportHybrid(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return data.roles != null
            && data.roles.Count > 0
            && data.roles[0] == UnitRole.Assalto
            && data.roles.Contains(UnitRole.FogoIndireto);
    }

    private bool TryBuildFallbackAttackFromEvaluations(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        List<HexEvaluation> evaluations,
        Vector3Int excludedCell,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || evaluations == null || evaluations.Count == 0)
            return false;

        var ordered = new List<HexEvaluation>();
        foreach (HexEvaluation eval in evaluations)
        {
            if (eval.cell == excludedCell) continue;
            if (eval.combatValue <= 0f) continue;
            ordered.Add(eval);
        }

        bool prioritizeDpqAtBattle = unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.prioritizeDpqAtBattle;

        ordered.Sort((a, b) =>
        {
            if (prioritizeDpqAtBattle)
            {
                int dpqCompare = b.positionQuality.CompareTo(a.positionQuality);
                if (dpqCompare != 0) return dpqCompare;
            }

            return b.total.CompareTo(a.total);
        });

        foreach (HexEvaluation eval in ordered)
        {
            Vector3Int attackCell = eval.cell;
            attackCell.z = 0;
            bool hasMoved = attackCell != fromCell;
            var attackCandidates = FindAttackTargetsSorted(unit, attackCell, hasMoved);
            if (attackCandidates == null) continue;

            foreach (var (target, _) in attackCandidates)
            {
                if (target?.targetUnit == null) continue;
                if (!PassesAttackDecision(unit, target.targetUnit, attackCell, false, out string atkReason))
                {
                    Debug.Log($"[AI] {unit.InstanceId} -> fallback ataque bloqueado por AttackDecision ({target.targetUnit.InstanceId}): {atkReason}");
                    continue;
                }

                Vector3Int targetCell = target.targetUnit.CurrentCellPosition;
                targetCell.z = 0;
                Debug.Log($"[AI] {unit.InstanceId} -> fallback ataca {target.targetUnit.InstanceId} de {attackCell}");
                action = BuildAttackBatch(
                    unit, snapshot.AITeam, fromCell, attackCell,
                    target.targetUnit.InstanceId.ToString(), targetCell, paths);
                return true;
            }
        }

        return false;
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

            BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, opt.targetUnit);
            score += Mathf.RoundToInt(GetAssaultTargetPreferenceScore(targetPreference));

            // Capturadores priorizam inimigos sobre construÃ§Ãµes

            if (isCapturador)

            {

                Vector3Int ec = opt.targetUnit.CurrentCellPosition; ec.z = 0;

                if (ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, ec) != null)

                    score += 10000;

            }

            // Preferir inimigos com HP baixo (mais fÃ¡ceis de eliminar)

            score += (10 - opt.targetUnit.CurrentHP) * 200;

            // HeurÃ­stica simples: alvo com â‰¤ 2 HP provavelmente morre

            if (opt.targetUnit.CurrentHP <= 2)

                score += 5000;

            // Penalidade por distÃ¢ncia

            score -= opt.distance * 50;

            scored.Add((opt, score));

        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));

        return scored;

    }
}
