using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Serviço de suprimento: seleção de alvos, manutenção preventiva,
    // construção de ações de supply e transfer.
    // -------------------------------------------------------------------------

    private UnitManager FindLogisticsServiceTarget(
        UnitManager logistics,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool baseDefense,
        AIReachDecisionStages stages =
            AIReachDecisionStages.Operational
            | AIReachDecisionStages.Strategic)
    {
        if (logistics == null || snapshot == null || snapshot.MyUnits == null)
            return null;

        var compatibilityByTargetId =
            new Dictionary<int, bool>();
        bool IsCandidate(UnitManager ally)
        {
            bool structurallyEligible = ally != null
                   && ally != logistics
                   && !ally.IsDead
                   && (!ally.IsEmbarked
                       || IsOwnEmbarkedPassenger(logistics, ally))
                   && ally.SlotIndex == logistics.SlotIndex
                   && !ally.ReceivedSuppliesThisTurn
                   && (ally.IsUnderRepair
                       || IsPreventiveLogisticsTarget(
                           logistics, ally));
            if (!structurallyEligible)
                return false;

            if (!compatibilityByTargetId.TryGetValue(
                    ally.InstanceId, out bool compatible))
            {
                compatible =
                    CanSupplyLogisticsTargetFromAnyLegalOrigin(
                        logistics, ally, snapshot,
                        out string compatibilityReason);
                compatibilityByTargetId[ally.InstanceId] =
                    compatible;
                if (!compatible && showAILogs)
                {
                    Debug.Log($"{TL("Logistics")} " +
                              $"{logistics.InstanceId} descarta " +
                              $"{ally.UnitDisplayName}#{ally.InstanceId}: " +
                              $"PodeSuprir sem origem compativel " +
                              $"({compatibilityReason}).");
                }
            }

            return compatible;
        }

        float ScoreCandidate(UnitManager ally)
        {
            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float need = ScoreLogisticsTargetNeed(
                snapshot, fromCell, ally);
            int maxFuel = Mathf.Max(1, ally.GetMaxFuel());
            float fuelPct =
                ally.CurrentFuel * 100f / maxFuel;
            float fuelEmergency = fuelPct <= 20f
                ? 20000f + (20f - fuelPct) * 1000f
                : 0f;
            int cubic =
                AIActionReachCoordinator.CubicDistance(
                    fromCell, allyCell);
            return need
                   + fuelEmergency
                   - cubic * 45f
                   - ally.InstanceId * 0.001f;
        }

        bool EvaluateOperational(
            int budget,
            out AIReachDecisionCandidate<UnitManager> candidate)
        {
            candidate = null;
            Dictionary<Vector3Int, List<Vector3Int>> operationalPaths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap,
                    logistics,
                    Mathf.Max(0, budget),
                    terrainDatabase);
            if (operationalPaths == null
                || operationalPaths.Count == 0
                || !logistics.TryGetUnitData(
                    out UnitData logisticsData)
                || logisticsData == null)
                return false;

            UnitThreatEnvelope envelope =
                UnitThreatEnvelopeService.BuildServiceEnvelope(
                    logistics,
                    boardTilemap,
                    operationalPaths,
                    logisticsData.serviceRange);
            UnitManager best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < snapshot.MyUnits.Count; i++)
            {
                UnitManager ally = snapshot.MyUnits[i];
                if (!IsCandidate(ally))
                    continue;
                Vector3Int allyCell =
                    ally.CurrentCellPosition;
                allyCell.z = 0;
                if (!envelope.CanThreaten(allyCell))
                    continue;
                float score = ScoreCandidate(ally);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = ally;
            }

            if (best == null)
                return false;
            Vector3Int target = best.CurrentCellPosition;
            target.z = 0;
            candidate = new AIReachDecisionCandidate<UnitManager>
            {
                Value = best,
                ActionCell = target,
                TargetCell = target,
                Score = bestScore,
                Reason = "service_hotzone_2t"
            };
            return true;
        }

        bool EvaluateStrategic(
            int _,
            out AIReachDecisionCandidate<UnitManager> candidate)
        {
            candidate = null;
            // Anotacao operacional do servico de retaguarda: um supridor so
            // abandona sua ancora por demanda distante se nao houver ameaca
            // no reach tatico nem no reach operacional de duas rodadas.
            if (!baseDefense
                && HasEnemyInsideLogisticsReach(
                    logistics, snapshot, fromCell, 2))
                return false;

            UnitManager best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < snapshot.MyUnits.Count; i++)
            {
                UnitManager ally = snapshot.MyUnits[i];
                if (!IsCandidate(ally))
                    continue;
                float score = ScoreCandidate(ally);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = ally;
            }

            if (best == null)
                return false;
            Vector3Int target = best.CurrentCellPosition;
            target.z = 0;
            candidate = new AIReachDecisionCandidate<UnitManager>
            {
                Value = best,
                ActionCell = target,
                TargetCell = target,
                Score = bestScore,
                Reason = "critical_need_cubic"
            };
            return true;
        }

        AIReachDecisionResult<UnitManager> reach =
            AIActionReachCoordinator.Evaluate(
                new AIReachDecisionRequest<UnitManager>
                {
                    Context =
                        $"FieldSupply:{logistics.InstanceId}",
                    Policy = new AIReachDecisionPolicy(
                        stages, operationalTurns: 2),
                    CurrentMovementBudget = Mathf.Max(
                        0, logistics.RemainingMovementPoints),
                    EvaluateOperational = EvaluateOperational,
                    EvaluateStrategic = EvaluateStrategic,
                    DiagnosticLog = showAILogs
                        ? message => Debug.Log(message)
                        : null
                });
        return reach.Found ? reach.Decision.Value : null;
    }

    private bool CanSupplyLogisticsTargetFromAnyLegalOrigin(
        UnitManager logistics,
        UnitManager target,
        AIWorldSnapshot snapshot,
        out string reason)
    {
        reason = "sem origem candidata";
        if (logistics == null
            || target == null
            || snapshot == null)
            return false;

        Vector3Int targetCell =
            target.CurrentCellPosition;
        targetCell.z = 0;
        var origins = new List<Vector3Int>(7);
        var neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(
            boardTilemap, targetCell, neighbors);
        origins.Add(targetCell);
        origins.AddRange(neighbors);
        var visited = new HashSet<Vector3Int>();
        int limit = Mathf.Max(1, GetLogisticsServiceLimit(logistics));
        string lastSensorReason = reason;

        for (int i = 0; i < origins.Count; i++)
        {
            Vector3Int origin = origins[i];
            origin.z = 0;
            if (!visited.Add(origin)
                || !IsConfirmedVisibleCellForAI(origin)
                || !IsLogisticsServiceCellAllowed(
                    logistics, snapshot, origin))
                continue;

            List<UnitManager> occupants =
                UnitOccupancyRules.GetUnitsAtCell(
                    boardTilemap, origin, logistics);
            if (!OccupancyResolver.CanEndMove(
                    logistics, origin, occupants))
                continue;

            List<UnitManager> validTargets =
                CollectLogisticsTargetsBySupplySensorAtCell(
                    logistics,
                    snapshot,
                    origin,
                    limit,
                    allowPreventiveMaintenance: true,
                    out _,
                    out _,
                    out string sensorReason);
            lastSensorReason = sensorReason;
            for (int t = 0; t < validTargets.Count; t++)
            {
                UnitManager valid = validTargets[t];
                if (valid != null
                    && valid.InstanceId == target.InstanceId)
                {
                    reason = $"origem={origin}";
                    return true;
                }
            }
        }

        reason = string.IsNullOrWhiteSpace(lastSensorReason)
            ? "dominio/camada/terreno/servico incompativeis"
            : lastSensorReason;
        return false;
    }

    private static bool HasEnemyInsideLogisticsReach(
        UnitManager logistics,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        int turns)
    {
        if (logistics == null
            || snapshot?.EnemyUnits == null)
            return false;

        fromCell.z = 0;
        int reach = Mathf.Max(
            0, logistics.RemainingMovementPoints)
            * Mathf.Max(1, turns);
        for (int i = 0; i < snapshot.EnemyUnits.Count; i++)
        {
            UnitManager enemy = snapshot.EnemyUnits[i];
            if (enemy == null
                || enemy.IsDead
                || enemy.IsEmbarked)
                continue;
            Vector3Int enemyCell =
                enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (AIActionReachCoordinator.CubicDistance(
                    fromCell, enemyCell) <= reach)
                return true;
        }

        return false;
    }

    private bool TryBuildLogisticsSupplyAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool baseDefense,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null || snapshot == null)
            return false;

        int limit = GetLogisticsServiceLimit(unit);
        if (limit <= 0)
            return false;
        bool allowPreventiveMaintenance = IsPreventiveLogisticsAllowed(unit, snapshot, fromCell, paths, occupied);
        bool mustVacateProducer = IsLogisticsProductionCell(snapshot.AITeam, fromCell)
            && HasReachableNonProductionLogisticsCell(unit, snapshot, fromCell, paths, occupied);

        Vector3Int bestCell = fromCell;
        List<UnitManager> bestTargets = null;
        float bestScore = float.MinValue;
        Vector3Int anchor = ResolveLogisticsAnchor(snapshot, fromCell);
        var currentOptions = new List<PodeSuprirOption>();
        var currentInvalidOptions = new List<PodeSuprirInvalidOption>();
        if (PodeSuprirSensor.CollectOptions(unit, boardTilemap, terrainDatabase, matchController, currentOptions, out _, currentInvalidOptions))
        {
            List<UnitManager> currentTargets = PickBestLogisticsSupplyTargets(unit, snapshot, fromCell, currentOptions, limit, allowPreventiveMaintenance);
            if (currentTargets.Count < Mathf.Min(limit, currentOptions.Count) || currentInvalidOptions.Count > 0)
            {
                Debug.Log($"{TL("Logistics")} {unit.InstanceId} supplySensor agora valid={currentOptions.Count} selected={currentTargets.Count}/{limit} invalid={currentInvalidOptions.Count} " +
                          $"{BuildLogisticsSupplyDebug(unit, currentOptions, currentTargets, currentInvalidOptions, allowPreventiveMaintenance)}");
            }
            if (currentTargets.Count > 0 && !mustVacateProducer)
            {
                bestTargets = currentTargets;
                bestScore = ScoreLogisticsSupplyCell(unit, snapshot, fromCell, fromCell, currentTargets, paths, anchor, baseDefense, preferCurrentCell: true);
                bool currentHasCritical = HasCriticalLogisticsTarget(currentTargets);
                if (currentTargets.Count >= limit || paths == null || paths.Count == 0)
                {
                    action = BuildSupplyBatch(unit, snapshot.AITeam, fromCell, fromCell, currentTargets, paths);
                    reason = currentHasCritical
                        ? $"critico agora count={currentTargets.Count}"
                        : allowPreventiveMaintenance ? $"preventivo agora count={currentTargets.Count}" : $"agora count={currentTargets.Count}";
                    return true;
                }

                Debug.Log($"{TL("Logistics")} {unit.InstanceId} encontrou {currentTargets.Count}/{limit} agora; verifica se andar atende mais alvos");
            }
            else if (currentTargets.Count > 0)
            {
                Debug.Log($"{TL("Logistics")} {unit.InstanceId} nao atende parado em {fromCell}: desocupa produtora primeiro");
            }
        }
        else if (currentInvalidOptions.Count > 0)
        {
            Debug.Log($"{TL("Logistics")} {unit.InstanceId} supplySensor agora sem validos invalid={currentInvalidOptions.Count} " +
                      $"{BuildLogisticsSupplyDebug(unit, currentOptions, null, currentInvalidOptions, allowPreventiveMaintenance)}");
        }

        if (paths == null || paths.Count == 0)
            return false;

        // Zona de atendimento: os hexes de onde o supridor alcancaria ALGUEM. Sem isto o
        // laco abaixo roda um PodeSuprir completo por celula alcancavel — num mapa grande,
        // centenas de varreduras por caminhao, quase todas sobre hexes onde nao ha ninguem
        // para atender. Aqui o conjunto de candidatos e montado UMA vez a partir das
        // unidades aliadas, e so as celulas dentro do alcance de servico entram no laco.
        HashSet<Vector3Int> serviceZone = BuildLogisticsServiceZone(unit, snapshot, paths);

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell && bestTargets != null)
                continue;
            if (serviceZone != null && !serviceZone.Contains(cell))
                continue;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            if (IsLogisticsProductionCell(snapshot.AITeam, cell)
                && (cell != fromCell || mustVacateProducer))
                continue;
            if (!IsLogisticsServiceCellAllowed(unit, snapshot, cell))
                continue;

            List<UnitManager> targets = CollectLogisticsTargetsBySupplySensorAtCell(
                unit,
                snapshot,
                cell,
                limit,
                allowPreventiveMaintenance,
                out int validCount,
                out int invalidCount,
                out string sensorDebug);
            if (targets.Count <= 0)
            {
                if (invalidCount > 0)
                    Debug.Log($"{TL("Logistics")} {unit.InstanceId} ignora supply via {cell}: PodeSuprir valid={validCount} invalid={invalidCount} {sensorDebug}");
                continue;
            }
            bool hasCriticalTarget = HasCriticalLogisticsTarget(targets);
            if (!baseDefense && cell != fromCell && IsLogisticsForwardOfMainLine(unit, snapshot, cell, anchor) && !hasCriticalTarget)
                continue;

            float score = ScoreLogisticsSupplyCell(unit, snapshot, fromCell, cell, targets, paths, anchor, baseDefense, preferCurrentCell: false);

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestTargets = targets;
            }
        }

        if (bestTargets == null || bestTargets.Count <= 0)
            return false;

        action = BuildSupplyBatch(unit, snapshot.AITeam, fromCell, bestCell, bestTargets, paths);
        bool now = bestCell == fromCell;
        bool bestHasCritical = HasCriticalLogisticsTarget(bestTargets);
        bool bestHasPreventive = HasPreventiveLogisticsTarget(unit, bestTargets);
        reason = bestHasCritical && bestHasPreventive
            ? (now ? $"critico+preventivo agora count={bestTargets.Count} score={bestScore:F0}" : $"critico+preventivo via={bestCell} count={bestTargets.Count} score={bestScore:F0}")
            : bestHasCritical
            ? (now ? $"critico agora count={bestTargets.Count} score={bestScore:F0}" : $"critico via={bestCell} count={bestTargets.Count} score={bestScore:F0}")
            : allowPreventiveMaintenance
                ? (now ? $"preventivo agora count={bestTargets.Count} score={bestScore:F0}" : $"preventivo via={bestCell} count={bestTargets.Count} score={bestScore:F0}")
                : (now ? $"agora count={bestTargets.Count} score={bestScore:F0}" : $"via={bestCell} count={bestTargets.Count} score={bestScore:F0}");
        return true;
    }

    private bool TryBuildStationaryLogisticsSupplyAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (!IsPrimaryLogisticsUnit(unit) || snapshot == null)
            return false;

        int limit = GetLogisticsServiceLimit(unit);
        if (limit <= 0)
            return false;

        bool allowPreventiveMaintenance = IsPreventiveLogisticsAllowed(unit, snapshot, fromCell, paths, occupied);
        var options = new List<PodeSuprirOption>();
        var invalidOptions = new List<PodeSuprirInvalidOption>();
        if (!PodeSuprirSensor.CollectOptions(unit, boardTilemap, terrainDatabase, matchController, options, out _, invalidOptions)
            || options.Count <= 0)
            return false;

        List<UnitManager> targets = PickBestLogisticsSupplyTargets(
            unit,
            snapshot,
            fromCell,
            options,
            limit,
            allowPreventiveMaintenance);

        if (targets.Count <= 0)
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < options.Count && targets.Count < limit; i++)
            {
                UnitManager target = options[i] != null ? options[i].targetUnit : null;
                if (target == null
                    || target == unit
                    || target.IsDead
                    || (target.IsEmbarked && !IsOwnEmbarkedPassenger(unit, target))
                    || target.SlotIndex != unit.SlotIndex
                    || target.ReceivedSuppliesThisTurn)
                    continue;
                if (!seen.Add(target.InstanceId))
                    continue;

                targets.Add(target);
            }
        }

        if (targets.Count <= 0)
            return false;

        action = BuildSupplyBatch(unit, snapshot.AITeam, fromCell, fromCell, targets, paths);
        bool hasCritical = HasCriticalLogisticsTarget(targets);
        bool hasPreventive = HasPreventiveLogisticsTarget(unit, targets);
        reason = hasCritical
            ? $"critico parado count={targets.Count}"
            : hasPreventive
                ? $"preventivo parado count={targets.Count}"
                : $"oportunista parado count={targets.Count}";
        return true;
    }

    private float ScoreLogisticsSupplyCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int cell,
        List<UnitManager> targets,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int anchor,
        bool baseDefense,
        bool preferCurrentCell)
    {
        if (targets == null || targets.Count <= 0)
            return float.MinValue;

        float threat = CalculateThreatLevel(cell, snapshot.AITeam);
        float dpq = GetTerrainDpqPontos(cell);
        float pairBonus = targets.Count >= 2 ? 1500f : 0f;
        float hpNeed = 0f;
        int criticalCount = 0;
        int emergencyCriticalCount = 0;
        int preventiveCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null)
                continue;

            int maxHp = Mathf.Max(1, targets[i].GetMaxHP());
            int missingHp = Mathf.Max(0, maxHp - targets[i].CurrentHP);
            bool critical = targets[i].IsUnderRepair;
            hpNeed += missingHp * (critical ? 1400f : 90f);
            if (critical && targets[i].CurrentHP * 2 <= maxHp)
            {
                hpNeed += 7500f;
                emergencyCriticalCount++;
            }
            if (critical)
            {
                criticalCount++;
                hpNeed += ScoreCriticalLogisticsStrategicBonus(snapshot, targets[i]);
            }
            else if (IsPreventiveLogisticsTarget(unit, targets[i]))
            {
                preventiveCount++;
                hpNeed += ScorePreventiveLogisticsStrategicBonus(targets[i]);
            }
        }
        float criticalPreventiveComboBonus = criticalCount > 0 && preventiveCount > 0 ? 6500f : 0f;
        float multiCriticalBonus = criticalCount >= 2 ? 11000f + emergencyCriticalCount * 3500f : 0f;

        float rearArea = CalculateLogisticsRearAreaScore(unit, snapshot, cell, anchor);
        int pathCost = cell == fromCell || paths == null ? 0 : GetPathStepCount(paths, cell);
        float score = targets.Count * 5000f
            + criticalCount * 9000f
            + preventiveCount * 1800f
            + criticalPreventiveComboBonus
            + multiCriticalBonus
            + pairBonus
            + hpNeed
            + dpq * 80f
            + rearArea * 0.55f
            - threat * (baseDefense ? 30f : 110f)
            - pathCost * 12f
            - cell.GetHashCode() * 0.000001f;

        if (preferCurrentCell)
            score += 250f;
        if (!baseDefense && IsLogisticsForwardOfMainLine(unit, snapshot, cell, anchor))
            score -= 2200f;

        return score;
    }

    private static bool HasCriticalLogisticsTarget(List<UnitManager> targets)
    {
        if (targets == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            UnitManager target = targets[i];
            if (target != null && target.IsUnderRepair)
                return true;
        }

        return false;
    }

    private static bool HasPreventiveLogisticsTarget(UnitManager logistics, List<UnitManager> targets)
    {
        if (logistics == null || targets == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            UnitManager target = targets[i];
            if (target != null && !target.IsUnderRepair && IsPreventiveLogisticsTarget(logistics, target))
                return true;
        }

        return false;
    }

    private List<UnitManager> PickBestLogisticsSupplyTargets(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int serviceCell,
        List<PodeSuprirOption> options,
        int limit,
        bool allowPreventiveMaintenance)
    {
        var result = new List<UnitManager>();
        if (options == null || limit <= 0)
            return result;

        options.Sort((a, b) =>
        {
            float sa = ScoreLogisticsSupplyOption(snapshot, serviceCell, a);
            float sb = ScoreLogisticsSupplyOption(snapshot, serviceCell, b);
            return sb.CompareTo(sa);
        });

        var seen = new HashSet<int>();
        for (int i = 0; i < options.Count && result.Count < limit; i++)
        {
            UnitManager target = options[i] != null ? options[i].targetUnit : null;
            if (!IsLogisticsServiceTarget(unit, target, allowPreventiveMaintenance))
                continue;
            if (!seen.Add(target.InstanceId))
                continue;
            result.Add(target);
        }

        return result;
    }

    private string BuildLogisticsSupplyDebug(
        UnitManager logistics,
        List<PodeSuprirOption> validOptions,
        List<UnitManager> selectedTargets,
        List<PodeSuprirInvalidOption> invalidOptions,
        bool allowPreventiveMaintenance)
    {
        var selectedIds = new HashSet<int>();
        if (selectedTargets != null)
        {
            for (int i = 0; i < selectedTargets.Count; i++)
            {
                UnitManager target = selectedTargets[i];
                if (target != null)
                    selectedIds.Add(target.InstanceId);
            }
        }

        string valid = "valid=[";
        if (validOptions != null)
        {
            for (int i = 0; i < validOptions.Count; i++)
            {
                UnitManager target = validOptions[i] != null ? validOptions[i].targetUnit : null;
                if (target == null)
                    continue;

                if (valid.Length > 7)
                    valid += "; ";
                bool eligible = IsLogisticsServiceTarget(logistics, target, allowPreventiveMaintenance);
                valid += $"#{target.InstanceId}@{FormatCellForDebug(target.CurrentCellPosition)} hp={target.CurrentHP}/{target.GetMaxHP()} repair={target.IsUnderRepair} recv={target.ReceivedSuppliesThisTurn} tookOff={target.TookOffRecently} aiEligible={eligible} selected={selectedIds.Contains(target.InstanceId)}";
            }
        }
        valid += "]";

        string invalid = "invalid=[";
        if (invalidOptions != null)
        {
            int shown = 0;
            for (int i = 0; i < invalidOptions.Count && shown < 6; i++)
            {
                PodeSuprirInvalidOption option = invalidOptions[i];
                UnitManager target = option != null ? option.targetUnit : null;
                if (target == null)
                    continue;

                if (shown > 0)
                    invalid += "; ";
                invalid += $"#{target.InstanceId}@{FormatCellForDebug(option.targetCell)} hp={target.CurrentHP}/{target.GetMaxHP()} repair={target.IsUnderRepair} recv={target.ReceivedSuppliesThisTurn} tookOff={target.TookOffRecently} reason={option.reason}";
                shown++;
            }
        }
        invalid += "]";

        return $"{valid} {invalid}";
    }

    private static string FormatCellForDebug(Vector3Int cell)
    {
        cell.z = 0;
        return $"({cell.x},{cell.y})";
    }

    private bool TryBuildTargetedLogisticsSupplyAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        UnitManager serviceTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool baseDefense,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null || snapshot == null || snapshot.MyUnits == null)
            return false;

        int limit = GetLogisticsServiceLimit(unit);
        if (limit <= 0)
            return false;

        bool allowPreventiveMaintenance = IsPreventiveLogisticsAllowed(unit, snapshot, fromCell, paths, occupied);
        if (paths == null || paths.Count == 0)
            return false;
        bool mustVacateProducer = IsLogisticsProductionCell(snapshot.AITeam, fromCell)
            && HasReachableNonProductionLogisticsCell(unit, snapshot, fromCell, paths, occupied);

        Vector3Int anchor = ResolveLogisticsAnchor(snapshot, fromCell);
        Vector3Int bestCell = fromCell;
        List<UnitManager> bestTargets = null;
        float bestScore = float.MinValue;
        string bestDetails = "";

        for (int u = 0; u < snapshot.MyUnits.Count; u++)
        {
            UnitManager candidateTarget = snapshot.MyUnits[u];
            if (!IsLogisticsServiceTarget(unit, candidateTarget, allowPreventiveMaintenance))
                continue;

            Vector3Int targetCell = candidateTarget.CurrentCellPosition;
            targetCell.z = 0;
            bool critical = candidateTarget.IsUnderRepair;

            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell != fromCell && occupied != null && occupied.Contains(cell))
                    continue;
                if (IsLogisticsProductionCell(snapshot.AITeam, cell)
                    && (cell != fromCell || mustVacateProducer))
                    continue;
                if (!IsLogisticsServiceCellAllowed(unit, snapshot, cell))
                    continue;
                List<UnitManager> targets = CollectLogisticsTargetsBySupplySensorAtCell(
                    unit,
                    snapshot,
                    cell,
                    limit,
                    allowPreventiveMaintenance,
                    out _,
                    out _,
                    out _);
                bool containsTarget = false;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null && targets[i].InstanceId == candidateTarget.InstanceId)
                    {
                        containsTarget = true;
                        break;
                    }
                }
                if (!containsTarget)
                    continue;

                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                float dpq = GetTerrainDpqPontos(cell);
                float rearArea = CalculateLogisticsRearAreaScore(unit, snapshot, cell, anchor);
                float targetNeed = ScoreLogisticsTargetNeed(snapshot, cell, candidateTarget);
                float serviceDist = SectorManager.HexDistance(cell, targetCell);
                int pathCost = GetPathStepCount(paths, cell);
                bool forward = !baseDefense && IsLogisticsForwardOfMainLine(unit, snapshot, cell, anchor);
                bool preferred = serviceTarget != null && candidateTarget.InstanceId == serviceTarget.InstanceId;

                float score = targetNeed
                    + (critical ? 8000f : 0f)
                    + (preferred ? 2500f : 0f)
                    + targets.Count * 1200f
                    + dpq * 80f
                    + rearArea * 0.45f
                    - threat * (baseDefense ? 35f : 120f)
                    - pathCost * 14f
                    - serviceDist * 50f
                    - candidateTarget.InstanceId * 0.001f;

                if (forward)
                    score -= critical ? 900f : 2600f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTargets = targets;
                    bestDetails = $"target={candidateTarget.UnitDisplayName}#{candidateTarget.InstanceId} critical={critical} preferred={preferred} count={targets.Count} need={targetNeed:F0} threat={threat:F1} dpq={dpq:F1} rear={rearArea:F0} path={pathCost} forward={forward}";
                }
            }
        }

        if (bestTargets == null || bestTargets.Count <= 0)
            return false;

        action = BuildSupplyBatch(unit, snapshot.AITeam, fromCell, bestCell, bestTargets, paths);
        reason = $"via={bestCell} score={bestScore:F0} {bestDetails}";
        return true;
    }

    private float ScoreLogisticsSupplyOption(AIWorldSnapshot snapshot, Vector3Int serviceCell, PodeSuprirOption option)
    {
        UnitManager target = option != null ? option.targetUnit : null;
        if (target == null)
            return float.MinValue;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        TeamId aiTeam = snapshot != null ? snapshot.AITeam : target.TeamId;
        return ScoreLogisticsTargetNeed(snapshot, serviceCell, target)
            + CalculateThreatLevel(targetCell, aiTeam) * 35f
            - SectorManager.HexDistance(serviceCell, targetCell) * 10f
            - target.InstanceId * 0.001f;
    }

    private List<UnitManager> CollectLogisticsTargetsInServiceRange(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int serviceCell,
        int limit,
        bool allowPreventiveMaintenance)
    {
        var result = new List<UnitManager>();
        if (unit == null || snapshot == null || snapshot.MyUnits == null || limit <= 0)
            return result;

        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (!IsLogisticsServiceTarget(unit, ally, allowPreventiveMaintenance))
                continue;
            if (!IsInLogisticsServiceRange(unit, serviceCell, ally))
                continue;

            result.Add(ally);
        }

        result.Sort((a, b) =>
        {
            Vector3Int ac = a.CurrentCellPosition; ac.z = 0;
            Vector3Int bc = b.CurrentCellPosition; bc.z = 0;
            float sa = ScoreLogisticsTargetNeed(snapshot, ac, a) + CalculateThreatLevel(ac, snapshot.AITeam) * 35f - a.InstanceId * 0.001f;
            float sb = ScoreLogisticsTargetNeed(snapshot, bc, b) + CalculateThreatLevel(bc, snapshot.AITeam) * 35f - b.InstanceId * 0.001f;
            return sb.CompareTo(sa);
        });

        if (result.Count > limit)
            result.RemoveRange(limit, result.Count - limit);
        return result;
    }

    private List<UnitManager> CollectLogisticsTargetsBySupplySensorAtCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int serviceCell,
        int limit,
        bool allowPreventiveMaintenance,
        out int validCount,
        out int invalidCount,
        out string debug)
    {
        validCount = 0;
        invalidCount = 0;
        debug = string.Empty;
        var empty = new List<UnitManager>();
        if (unit == null || limit <= 0)
            return empty;

        serviceCell.z = 0;

        var options = new List<PodeSuprirOption>();
        var invalidOptions = new List<PodeSuprirInvalidOption>();
        bool hasAny = PodeSuprirSensor.CollectOptionsFromCell(
            unit,
            boardTilemap,
            terrainDatabase,
            matchController,
            serviceCell,
            options,
            out string sensorReason,
            invalidOptions);

        validCount = options.Count;
        invalidCount = invalidOptions.Count;
        List<UnitManager> targets = PickBestLogisticsSupplyTargets(
            unit,
            snapshot,
            serviceCell,
            options,
            limit,
            allowPreventiveMaintenance);

        if (!hasAny || targets.Count <= 0)
            debug = BuildLogisticsSupplyDebug(unit, options, targets, invalidOptions, allowPreventiveMaintenance);

        if (!hasAny && string.IsNullOrWhiteSpace(debug))
            debug = sensorReason;

        return targets;
    }

    // Passageiro embarcado NESTE supridor. O PodeSuprir ja o aceita como alvo legal
    // (serviceRange SameHexOrEmbarked coleta exatamente os proprios embarcados, e o
    // modo hibrido aceita quem esta no mesmo hex), e IsInLogisticsServiceRange tambem.
    // Sem este recorte a IA descartava o proprio paciente e o navio-hospital virava
    // supridor no-op. Embarcado de terceiros continua fora: nao ha alcance ate ele.
    private static bool IsOwnEmbarkedPassenger(UnitManager logistics, UnitManager target)
    {
        return logistics != null
            && target != null
            && target.IsEmbarked
            && target.EmbarkedTransporter == logistics;
    }

    private static bool IsLogisticsServiceTarget(UnitManager logistics, UnitManager target, bool allowPreventiveMaintenance)
    {
        if (target == null
            || logistics == null
            || target == logistics
            || target.IsDead
            || (target.IsEmbarked && !IsOwnEmbarkedPassenger(logistics, target))
            || target.SlotIndex != logistics.SlotIndex
            || target.ReceivedSuppliesThisTurn)
            return false;

        if (target.IsUnderRepair)
            return true;

        return allowPreventiveMaintenance && IsPreventiveLogisticsTarget(logistics, target);
    }

    private bool IsPreventiveLogisticsAllowed(
        UnitManager logistics,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (logistics == null
            || !logistics.TryGetUnitData(out UnitData data)
            || data == null
            || !data.aiPreventiveMaintenanceEnabled)
            return false;

        return data.aiPreventiveSupplyCanRunWithUnderRepair
            || !HasReachableCriticalLogisticsTarget(logistics, snapshot, fromCell, paths, occupied);
    }

    private bool HasReachableCriticalLogisticsTarget(
        UnitManager logistics,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (logistics == null || snapshot == null || snapshot.MyUnits == null)
            return false;

        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (ally == null
                || ally == logistics
                || ally.IsDead
                || (ally.IsEmbarked && !IsOwnEmbarkedPassenger(logistics, ally))
                || ally.SlotIndex != logistics.SlotIndex
                || ally.ReceivedSuppliesThisTurn
                || !ally.IsUnderRepair)
                continue;

            if (IsReachableLogisticsServiceTarget(logistics, ally, fromCell, paths, occupied))
                return true;
        }

        return false;
    }

    private bool IsReachableLogisticsServiceTarget(
        UnitManager logistics,
        UnitManager target,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        // Alcancabilidade aqui significa pertencer ao envelope movimento +
        // alcance de servico desta rodada. Nao execute PodeSuprir para cada
        // origem: o sensor e a validacao final feita apenas pelo construtor da
        // acao, depois que a hotzone reduziu os candidatos.
        if (IsInLogisticsServiceRange(logistics, fromCell, target)
            && IsLogisticsServiceCellAllowed(logistics, null, fromCell))
        {
            return true;
        }
        if (paths == null || paths.Count == 0)
            return false;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            if (!IsLogisticsServiceCellAllowed(logistics, null, cell))
                continue;
            if (IsInLogisticsServiceRange(logistics, cell, target))
                return true;
        }

        return false;
    }

    private static bool IsPreventiveLogisticsTarget(UnitManager logistics, UnitManager target)
    {
        if (logistics == null || target == null || !logistics.TryGetUnitData(out UnitData data) || data == null)
            return false;

        if (data.aiPreventiveSupplyHpBelowPct > 0)
        {
            int maxHp = Mathf.Max(1, target.GetMaxHP());
            if (target.CurrentHP * 100f / maxHp < data.aiPreventiveSupplyHpBelowPct)
                return true;
        }

        if (data.aiPreventiveSupplyAutonomyBelowPct > 0)
        {
            int maxFuel = Mathf.Max(1, target.GetMaxFuel());
            if (target.CurrentFuel * 100f / maxFuel < data.aiPreventiveSupplyAutonomyBelowPct)
                return true;
        }

        return data.aiPreventiveSupplyWeaponAmmoAtOrBelow > 0
            && HasAnyWeaponAmmoAtOrBelow(target, data.aiPreventiveSupplyWeaponAmmoAtOrBelow);
    }

    private static bool HasAnyWeaponAmmoAtOrBelow(UnitManager unit, int ammoThreshold)
    {
        if (ammoThreshold < 0 || unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.embarkedWeapons == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = unit.GetEmbarkedWeapons();
        if (runtimeWeapons == null)
            return false;

        int count = Mathf.Min(runtimeWeapons.Count, data.embarkedWeapons.Count);
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = data.embarkedWeapons[i];
            if (runtime == null || baseline == null)
                continue;
            if (baseline.squadAmmunition > 0 && runtime.squadAmmunition <= ammoThreshold)
                return true;
        }

        return false;
    }

    private float ScoreLogisticsTargetNeed(AIWorldSnapshot snapshot, Vector3Int serviceCell, UnitManager target)
    {
        if (target == null)
            return 0f;

        float valueBonus = target.TryGetUnitData(out UnitData vd) && vd != null ? vd.cost / 100f : 0f;

        if (target.IsUnderRepair)
        {
            int criticalMaxHp = Mathf.Max(1, target.GetMaxHP());
            int criticalMissingHp = Mathf.Max(0, criticalMaxHp - target.CurrentHP);
            float emergencyBonus = target.CurrentHP * 2 <= criticalMaxHp ? 7500f : 0f;
            return 10000f
                + criticalMissingHp * 1800f
                + emergencyBonus
                + valueBonus
                + ScoreCriticalLogisticsStrategicBonus(snapshot, target);
        }

        float score = valueBonus;
        int maxHp = Mathf.Max(1, target.GetMaxHP());
        int maxFuel = Mathf.Max(1, target.GetMaxFuel());
        score += Mathf.Max(0f, 100f - target.CurrentHP * 100f / maxHp) * 18f;
        score += Mathf.Max(0f, 100f - target.CurrentFuel * 100f / maxFuel) * 10f;
        score += ScorePreventiveLogisticsStrategicBonus(target);
        return score;
    }

    private float ScoreCriticalLogisticsStrategicBonus(AIWorldSnapshot snapshot, UnitManager target)
    {
        if (target == null || !target.TryGetUnitData(out UnitData data) || data == null)
            return 0f;

        float score = data.cost / 12f;
        score += Mathf.Max(0, data.eliteLevel) * 3000f;

        bool fireSupport = HasLogisticsRole(data, UnitRole.FogoIndireto) || data.unitClass == GameUnitClass.Artillery
            || data.preferArtilleryModeBeforeCombatant || data.longRangeStationary;
        if (fireSupport)
        {
            score += 6500f;
            if (HasAnyWeaponAmmoAtOrBelow(target, 0))
                score += 9500f;
            else if (HasAnyWeaponAmmoAtOrBelow(target, 1))
                score += 4500f;
        }

        if (HasLogisticsRole(data, UnitRole.Logistica))
            score += 5500f;
        if (HasLogisticsRole(data, UnitRole.Transportador))
            score += 2500f;
        if (HasLogisticsRole(data, UnitRole.Assalto))
            score += 1800f;

        bool mergeableInfantry = data.fuseWhileInRepair
            && data.unitClass == GameUnitClass.Infantry
            && HasNearbyFusionCandidate(snapshot, target, data);
        if (mergeableInfantry)
            score -= 9000f;

        return score;
    }

    private static float ScorePreventiveLogisticsStrategicBonus(UnitManager target)
    {
        if (target == null || !target.TryGetUnitData(out UnitData data) || data == null)
            return 0f;

        float score = data.cost / 25f + Mathf.Max(0, data.eliteLevel) * 900f;
        bool fireSupport = HasLogisticsRole(data, UnitRole.FogoIndireto) || data.unitClass == GameUnitClass.Artillery
            || data.preferArtilleryModeBeforeCombatant || data.longRangeStationary;
        if (fireSupport)
        {
            if (HasAnyWeaponAmmoAtOrBelow(target, 0))
                score += 9000f;
            else if (HasAnyWeaponAmmoAtOrBelow(target, 1))
                score += 4200f;
            else
                score += 1200f;
        }
        else if (HasAnyWeaponAmmoAtOrBelow(target, 1))
        {
            score += 1400f;
        }

        return score;
    }

    private static bool HasLogisticsRole(UnitData data, UnitRole role)
    {
        return data != null && data.roles != null && data.roles.Contains(role);
    }

    private static bool HasNearbyFusionCandidate(AIWorldSnapshot snapshot, UnitManager target, UnitData targetData)
    {
        if (snapshot == null || snapshot.MyUnits == null || target == null || targetData == null)
            return false;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (ally == null
                || ally == target
                || ally.IsDead
                || ally.IsEmbarked
                || ally.IsUnderRepair
                || ally.SlotIndex != target.SlotIndex
                || !ally.TryGetUnitData(out UnitData allyData)
                || allyData != targetData)
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            if (SectorManager.HexDistance(targetCell, allyCell) <= 2f)
                return true;
        }

        return false;
    }

    // Origens de servico que formam a hotzone logistica desta rodada.
    // A intersecao deste conjunto com paths.Keys equivale a uma arma ficticia:
    // movimento do supridor + alcance de servico. Um caminhao com 5 MP e alcance
    // adjacente atende corretamente um alvo na casa 6.
    //
    private HashSet<Vector3Int> BuildLogisticsServiceZone(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        if (unit == null || snapshot?.MyUnits == null || snapshot.MyUnits.Count == 0 ||
            paths == null || paths.Count == 0 ||
            !unit.TryGetUnitData(out UnitData data) || data == null)
            return new HashSet<Vector3Int>();

        Tilemap map = boardTilemap != null ? boardTilemap : unit.BoardTilemap;
        if (map == null)
            return new HashSet<Vector3Int>();

        // Fonte compartilhada da hotzone: exatamente os destinos legais de
        // movimento, expandidos pelo alcance da ferramenta de servico.
        UnitThreatEnvelope hotzone = UnitThreatEnvelopeService.BuildServiceEnvelope(
            unit,
            map,
            paths,
            data.serviceRange);
        HashSet<Vector3Int> zone = new HashSet<Vector3Int>();
        bool allowPreventiveMaintenance =
            IsPreventiveLogisticsAllowed(unit, snapshot, unit.CurrentCellPosition, null, null);

        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (!IsLogisticsServiceTarget(unit, ally, allowPreventiveMaintenance))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            if (!hotzone.CanThreaten(allyCell))
                continue;

            // A hotzone determina se o alvo pertence a esta rodada. Depois,
            // preservamos somente as origens legais que efetivamente o atendem;
            // PodeSuprir valida a acao final nessas poucas origens.
            foreach (Vector3Int rawOrigin in hotzone.MovementCells)
            {
                Vector3Int origin = rawOrigin;
                origin.z = 0;
                if (IsInLogisticsServiceRange(unit, origin, ally))
                    zone.Add(origin);
            }
        }

        return zone;
    }

    private static bool IsInLogisticsServiceRange(UnitManager logistics, Vector3Int serviceCell, UnitManager target)
    {
        if (logistics == null || target == null || !logistics.TryGetUnitData(out UnitData data) || data == null)
            return false;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        float dist = SectorManager.HexDistance(serviceCell, targetCell);
        switch (data.serviceRange)
        {
            case SupplierRangeMode.Hybrid0Or1Hex:
                return dist <= 1f;
            case SupplierRangeMode.Adjacent1Hex:
                return Mathf.Approximately(dist, 1f);
            case SupplierRangeMode.SameHexOrEmbarked:
                return target.IsEmbarked && target.EmbarkedTransporter == logistics;
            default:
                return false;
        }
    }
}
