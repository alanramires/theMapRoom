using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fase 2: Ações de unidades
    // -------------------------------------------------------------------------

    private IEnumerator Phase2_UnitActions(AIWorldSnapshot snapshot)
    {
        if (ShouldStopAIForMatchEnd("phase2_start"))
            yield break;

        TeamId aiTeam = snapshot.AITeam;

        List<UnitManager> units = GetAvailableUnits(PlayerSlotId.FromIndex(snapshot.AISlotIndex), aiTeam);
        if (units.Count == 0)
        {
            Debug.Log($"{TL()} Fase 2 — sem unidades em campo, pulando.");
            if (matchController == null ||
                !matchController.IsPlayerCommandServiceAutomatic(PlayerSlotId.FromIndex(snapshot.AISlotIndex)))
                JogadasManager.EnsureInstance()?.RegistrarServicoComando(snapshot.TurnNumber, snapshot.AISlotIndex);
            yield break;
        }

        Debug.Log($"{TL()} Fase2 — iniciando ações.");
        plannedDestinations.Clear();
        rebelCaptureTargetReservations.Clear();
        assignedTransportClaims.Clear();
        transportPlanningSnapshots.Clear();

        // ---- Setup: executado uma única vez por fase ----
        SyncAIUnitCellsFromTransforms();
        AIWorldSnapshot current = AIWorldSnapshot.BuildLight(PlayerSlotId.FromIndex(snapshot.AISlotIndex), matchController);
        TeamObjectivePlan activePlan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(ResolveAISlotKey(aiTeam)));
        InvalidateStaleThreatObjectives(activePlan, aiTeam);

        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.SlotIndex == snapshot.AISlotIndex && !u.IsDead)
                UpdateRepairState(u, activePlan);
        }

        _sortIsInvading = snapshot.IsInvading;
        _groupCache.Clear();
        foreach (UnitManager u in units)
            _groupCache[u.InstanceId] = GetInitiativeGroup(u, activePlan, aiTeam);

        _sortAiTeam = aiTeam;
        _sortActivePlan = activePlan;
        units.Sort(_initiativeComparison);

        _initLogBuilder.Clear();
        _initLogBuilder.AppendLine($"{TL()} Fase2 iniciativa ({units.Count} unidades):");
        foreach (UnitManager u in units)
        {
            int g = _groupCache[u.InstanceId];
            Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
            Vector3Int? tgt = GetAssignedTargetCell(u, activePlan);
            string tgtStr = tgt.HasValue ? tgt.Value.ToString() : "null";
            _initLogBuilder.AppendLine($"  [grp={g}] {FormatInitiativeUnitName(u)} @ {uc} target={tgtStr}");
        }
        Debug.Log(_initLogBuilder.ToString());
        _initLogBuilder.Clear();

        // Atualiza indicador/cursor antes da primeira decisao potencialmente cara.
        yield return null;

        float phase2BatchDelay =
            GetAdaptivePhase2BatchDelay(units.Count);
        if (showAILogs
            && !Mathf.Approximately(
                phase2BatchDelay, GetBatchDelay()))
        {
            Debug.Log(
                $"{TL()} Fase2 pacing adaptativo: " +
                $"unidades={units.Count} " +
                $"delay={phase2BatchDelay:F3}s.");
        }

        // ---- Loop por unidade: decisão + execução ----
        var deferredUnitIds = new HashSet<int>();
        var perfSamples = new List<Phase2UnitPerfSample>();
        float perfDecisionTotalMs = 0f;
        float perfExecutionTotalMs = 0f;
        float perfSnapshotTotalMs = 0f;
        float perfDelayTotalMs = 0f;
        int perfDecisionCount = 0;
        AIDecisionPerf.BeginPhase();
        int cursor = 0;
        bool secondPass = false;

        while (isActive && !IsMatchEnded())
        {
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase2_loop_apos_pause"))
                yield break;

            // Avança o cursor sobre unidades já agidas, mortas ou deferidas (1ª passagem)
            while (cursor < units.Count &&
                   (units[cursor] == null || units[cursor].IsDead || units[cursor].HasActed ||
                    (!secondPass && deferredUnitIds.Contains(units[cursor].InstanceId))))
            {
                cursor++;
            }

            if (cursor >= units.Count)
            {
                if (!secondPass && deferredUnitIds.Count > 0)
                {
                    // Todas as unidades não-deferidas agiram; processa as deferidas
                    deferredUnitIds.Clear();
                    cursor = 0;
                    secondPass = true;
                    continue;
                }
                break;
            }

            UnitManager unit = units[cursor];
            AIDecisionPerf.Begin();
            float decisionStartedAt = Time.realtimeSinceStartup;
            PlayerAction action = null;
            System.Exception decisionError = null;
            try
            {
                PrepareAIThreatEnvelope(unit);
                action = DecideUnitAction(unit, current);
            }
            catch (System.Exception ex)
            {
                decisionError = ex;
            }
            float decisionMs = (Time.realtimeSinceStartup - decisionStartedAt) * 1000f;
            string decisionBreakdown = AIDecisionPerf.End();
            perfDecisionTotalMs += decisionMs;
            perfDecisionCount++;

            if (decisionError != null)
            {
                Vector3Int heldCell = unit.CurrentCellPosition;
                heldCell.z = 0;
                Debug.LogException(decisionError);
                Debug.LogWarning(
                    $"{TL()} Fase2 — decisão de {FormatInitiativeUnitName(unit)} " +
                    "falhou; executa Mover Parado para preservar o turno.");
                action = BuildMoveBatch(
                    unit, aiTeam, heldCell, heldCell);
            }

            if (!secondPass
                && ShouldDeferCapturerForAirTransportVacate(unit, action, activePlan, aiTeam,
                    out UnitManager airTransporter))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — capturador {unit.InstanceId} cede vez para heli {airTransporter.InstanceId} sair da producao antes do embarque");
                cursor++;
                yield return null;
                continue;
            }

            if (!secondPass
                && ShouldDeferAttackForFireSupportPrep(unit, action, aiTeam,
                    out UnitManager prepFireSupport, out UnitManager prepTarget, out Vector3Int prepCell))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — {FormatInitiativeUnitName(unit)} cede ataque em {prepTarget.InstanceId} para artilharia {prepFireSupport.InstanceId} amaciar via {prepCell}");
                cursor++;
                yield return null;
                continue;
            }

            if (!secondPass
                && ShouldDeferAttackForAirCombatPrep(unit, action, current, aiTeam,
                    out UnitManager prepAirCombat, out UnitManager prepAirTarget, out Vector3Int prepAirCell))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — {FormatInitiativeUnitName(unit)} cede ataque aereo em {prepAirTarget.InstanceId} para {FormatInitiativeUnitName(prepAirCombat)} atacar primeiro via {prepAirCell}");
                cursor++;
                yield return null;
                continue;
            }

            if (!secondPass
                && (action == null || IsNoOpUnitAction(action))
                && ShouldDeferCapturerForRogueEmbarkBlocker(unit, activePlan, aiTeam,
                    out UnitManager embarkBlocker, out UnitManager blockedTransporter))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — capturador {unit.InstanceId} cede vez para rogue {embarkBlocker.InstanceId} liberar embarque no transporte {blockedTransporter.InstanceId}");
                cursor++;
                yield return null;
                continue;
            }

            if (!secondPass && IsNoOpUnitAction(action) && ShouldDeferIdleAssaultForSectorCapturer(unit, activePlan, aiTeam))
            {
                SectorObjective obj = ResolveAssignedAssaultObjective(unit, activePlan);
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — batedor {unit.InstanceId} cede vez para capturador de {obj.Sector}");
                cursor++;
                yield return null;
                continue;
            }

            if (action == null)
            {
                if (showAILogs)
                    Debug.LogWarning($"[AI] Sem decisão para {unit.InstanceId} — marcando como agida.");
                unit.MarkAsActed();
                cursor++;
                continue;
            }

            if (TryFindBlockingOccupantForAIMove(
                    unit, action, out UnitManager moveBlocker))
            {
                Vector3Int blockedDestination = action.MoveTo;
                blockedDestination.z = 0;
                assignedTransportClaims.Remove(unit.InstanceId);
                rebelCaptureTargetReservations.Remove(
                    blockedDestination);

                bool blockerCanYieldFirst =
                    !secondPass
                    && moveBlocker != null
                    && moveBlocker.SlotIndex == snapshot.AISlotIndex
                    && !moveBlocker.HasActed
                    && units.Contains(moveBlocker);
                if (blockerCanYieldFirst)
                {
                    deferredUnitIds.Add(unit.InstanceId);
                    Debug.Log(
                        $"{TL()} Fase2 — {FormatInitiativeUnitName(unit)} " +
                        $"cede destino {blockedDestination} para " +
                        $"{FormatInitiativeUnitName(moveBlocker)} agir primeiro.");
                    cursor++;
                    yield return null;
                    continue;
                }

                Vector3Int heldCell = unit.CurrentCellPosition;
                heldCell.z = 0;
                Debug.LogWarning(
                    $"{TL()} Fase2 — {FormatInitiativeUnitName(unit)} " +
                    $"não pode terminar em {blockedDestination}" +
                    (moveBlocker != null
                        ? $" (ocupado por {FormatInitiativeUnitName(moveBlocker)})"
                        : " (ocupação incompatível)") +
                    "; executa Mover Parado.");
                action = BuildMoveBatch(
                    unit, aiTeam, heldCell, heldCell);
            }

            bool unitMoved    = action.HasMoveTo && action.MoveTo != action.MoveFrom;
            bool unitAttacked = !string.IsNullOrEmpty(action.TargetInstanceId);
            float executionStartedAt = Time.realtimeSinceStartup;
            yield return ExecuteAIBatchWithDebugStep(action);
            float executionMs = (Time.realtimeSinceStartup - executionStartedAt) * 1000f;
            perfExecutionTotalMs += executionMs;
            bool batchSucceeded = lastAIBatchSucceeded;
            if (batchSucceeded)
            {
                if (action.HasMoveTo
                    && action.MoveTo != action.MoveFrom)
                {
                    Vector3Int dest = action.MoveTo;
                    dest.z = 0;
                    plannedDestinations.Add(dest);
                }
                JogadasManager.RegistrarPlayerAction(action);
            }
            else
            {
                assignedTransportClaims.Remove(unit.InstanceId);
                unitMoved = false;
                unitAttacked = false;
                Debug.LogWarning(
                    $"{TL()} Fase2 — batch de " +
                    $"{FormatInitiativeUnitName(unit)} foi abortado " +
                    "sem compromisso; a fase continuará.");
            }
            if (ShouldStopAIForMatchEnd("phase2_apos_batch"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase2_apos_pause_batch"))
                yield break;

            if (batchSucceeded
                && (!IsNoOpUnitAction(action)
                    || unitMoved
                    || unitAttacked))
            {
                bool targetedConstruction = !string.IsNullOrEmpty(action.TargetConstructionId);

                // Só CAPTURA/predio dispara o refresh completo de FoW aqui. Movimento e ataque
                // ja tiveram o FoW CONFIRMADO atualizado pelo caminho incremental (delta) ao
                // comprometer a acao e voltar a Neutral — o MESMO contrato que o jogador humano
                // usa, que nunca passa por este commit e mesmo assim enxerga certo. Repetir um
                // recolhimento das N unidades por movimento e redundante: a chave do cache de
                // visao inclui o globalBoardRevision, que sobe a cada passo de qualquer unidade,
                // entao o full refresh perdia o cache das 35 e recoletava tudo (~3,8s por unidade
                // em mapa grande). A captura fica de fora do delta porque muda a visao de
                // CONSTRUCAO (predio recem-tomado passa a enxergar), que o delta do movedor nao
                // cobre — por isso ela, e so ela, mantem o refresh completo.
                bool changesConstructionVision =
                    targetedConstruction || action.SensorAction == SensorActionType.Capture;
                CommitAIWorldLightAfterAction(
                    PlayerSlotId.FromIndex(snapshot.AISlotIndex),
                    $"phase2:{FormatInitiativeUnitName(unit)}",
                    changesConstructionVision);
            }

            // Reconstrói o snapshot para a próxima decisão (hexes ocupados mudam após cada ação)
            float snapshotStartedAt = Time.realtimeSinceStartup;
            current = AIWorldSnapshot.BuildLight(PlayerSlotId.FromIndex(snapshot.AISlotIndex), matchController);
            float snapshotMs = (Time.realtimeSinceStartup - snapshotStartedAt) * 1000f;
            perfSnapshotTotalMs += snapshotMs;

            float delay = phase2BatchDelay;
            float delayMs = 0f;
            if (delay > 0f)
            {
                float delayStartedAt = Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(delay);
                delayMs = (Time.realtimeSinceStartup - delayStartedAt) * 1000f;
                perfDelayTotalMs += delayMs;
            }

            var perfSample = new Phase2UnitPerfSample(
                unit, GetPhase2ActionKind(action), decisionMs, executionMs, snapshotMs, delayMs,
                decisionBreakdown);
            perfSamples.Add(perfSample);
            LogPhase2UnitPerf(snapshot.TurnNumber, aiTeam, perfSample);

            cursor++;
        }

        string phaseDecisionBreakdown = AIDecisionPerf.EndPhase();
        LogPhase2PerfSummary(
            snapshot.TurnNumber,
            aiTeam,
            perfDecisionCount,
            perfDecisionTotalMs,
            perfExecutionTotalMs,
            perfSnapshotTotalMs,
            perfDelayTotalMs,
            perfSamples,
            phaseDecisionBreakdown);

        _initLogBuilder.Clear();
        int idleCount = 0;
        foreach (UnitManager u in units)
        {
            if (u == null || u.IsDead || u.IsEmbarked || u.HasActed)
                continue;
            idleCount++;
            Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
            _initLogBuilder.AppendLine($"  {FormatInitiativeUnitName(u)} @ {uc} (HasActed=false)");
        }

        if (idleCount > 0)
            Debug.LogWarning($"{TL()} Fase2 concluída — {idleCount} unidade(s) NÃO agiram:\n{_initLogBuilder}");
        else
            Debug.Log($"{TL()} Fase2 concluída — todas as {units.Count} unidades agiram.");
        _initLogBuilder.Clear();
    }

    private bool TryFindBlockingOccupantForAIMove(
        UnitManager unit,
        PlayerAction action,
        out UnitManager blocker)
    {
        blocker = null;
        if (unit == null
            || action == null
            || !action.HasMoveFrom
            || !action.HasMoveTo
            || action.MoveTo == action.MoveFrom
            || boardTilemap == null)
        {
            return false;
        }

        Vector3Int destination = action.MoveTo;
        destination.z = 0;
        List<UnitManager> occupants =
            UnitOccupancyRules.GetUnitsAtCell(
                boardTilemap, destination, unit);
        if (CanAIUnitEndMoveAtCell(
                unit, destination, occupants))
        {
            return false;
        }

        // Descobre quem realmente bloqueia a camada da unidade. Apenas
        // compartilhar a coordenada nao basta: aeronaves, submarinos e
        // superficie podem coexistir conforme a autoridade de ocupacao.
        var singleOccupant = new List<UnitManager>(1);
        for (int i = 0; i < occupants.Count; i++)
        {
            UnitManager candidate = occupants[i];
            if (candidate == null)
                continue;

            singleOccupant.Clear();
            singleOccupant.Add(candidate);
            if (!CanAIUnitEndMoveAtCell(
                    unit, destination, singleOccupant))
            {
                blocker = candidate;
                break;
            }
        }

        return true;
    }

    private static bool CanAIUnitEndMoveAtCell(
        UnitManager unit,
        Vector3Int destination,
        IEnumerable<UnitManager> occupants)
    {
        if (unit == null)
            return false;

        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;
        destination.z = 0;
        bool projectsToAir =
            unit.GetDomain() == Domain.Air
            && !unit.IsAircraftGrounded;
        if (!projectsToAir
            && unit.IsAircraftGrounded
            && destination != origin)
        {
            projectsToAir = true;
        }

        if (projectsToAir)
        {
            HeightLevel finalHeight =
                unit.GetDomain() == Domain.Air
                    ? unit.GetHeightLevel()
                    : HeightLevel.AirLow;
            return OccupancyResolver.CanEndMoveAsLayer(
                unit,
                Domain.Air,
                finalHeight,
                occupants);
        }

        return OccupancyResolver.CanEndMove(
            unit, destination, occupants);
    }

    private static string GetPhase2ActionKind(PlayerAction action)
    {
        if (action == null) return "null";
        if (!string.IsNullOrEmpty(action.TargetInstanceId)) return "attack";
        if (!string.IsNullOrEmpty(action.TargetConstructionId)) return "construction";
        if (action.HasMoveTo && action.HasMoveFrom)
            return action.MoveTo == action.MoveFrom ? "wait" : "move";
        return action.SensorAction.ToString();
    }

    private void LogPhase2UnitPerf(int turnNumber, TeamId aiTeam, Phase2UnitPerfSample sample)
    {
        Debug.Log($"[AI Perf][Unit][T{turnNumber}][{aiTeam}] {sample.UnitLabel} " +
                  $"action={sample.ActionKind} decision={sample.DecisionMs:F0}ms " +
                  $"execution={sample.ExecutionMs:F0}ms snapshot={sample.SnapshotMs:F0}ms " +
                  $"delay={sample.DelayMs:F0}ms total={sample.TotalMs:F0}ms {sample.DecisionBreakdown}");
    }

    private void LogPhase2PerfSummary(
        int turnNumber,
        TeamId aiTeam,
        int decisionCount,
        float decisionTotalMs,
        float executionTotalMs,
        float snapshotTotalMs,
        float delayTotalMs,
        List<Phase2UnitPerfSample> samples,
        string phaseDecisionBreakdown)
    {
        samples.Sort((a, b) => b.TotalMs.CompareTo(a.TotalMs));
        _initLogBuilder.Clear();
        _initLogBuilder.AppendLine(
            $"[AI Perf][Phase2 Breakdown][T{turnNumber}][{aiTeam}] " +
            $"decisions={decisionCount} completed={samples.Count}");
        _initLogBuilder.AppendLine(
            $"  decision={decisionTotalMs:F0}ms execution={executionTotalMs:F0}ms " +
            $"snapshot={snapshotTotalMs:F0}ms delay={delayTotalMs:F0}ms " +
            $"measuredTotal={decisionTotalMs + executionTotalMs + snapshotTotalMs + delayTotalMs:F0}ms");
        _initLogBuilder.AppendLine($"  boardQueries {phaseDecisionBreakdown}");

        int topCount = Mathf.Min(5, samples.Count);
        for (int i = 0; i < topCount; i++)
        {
            Phase2UnitPerfSample sample = samples[i];
            _initLogBuilder.AppendLine(
                $"  #{i + 1} {sample.UnitLabel} action={sample.ActionKind} total={sample.TotalMs:F0}ms " +
                $"decision={sample.DecisionMs:F0} execution={sample.ExecutionMs:F0} " +
                $"snapshot={sample.SnapshotMs:F0} delay={sample.DelayMs:F0}");
        }

        Debug.Log(_initLogBuilder.ToString());
        _initLogBuilder.Clear();
    }

    private sealed class Phase2UnitPerfSample
    {
        public readonly string UnitLabel;
        public readonly string ActionKind;
        public readonly float DecisionMs;
        public readonly float ExecutionMs;
        public readonly float SnapshotMs;
        public readonly float DelayMs;
        public readonly string DecisionBreakdown;

        public float TotalMs => DecisionMs + ExecutionMs + SnapshotMs + DelayMs;

        public Phase2UnitPerfSample(
            UnitManager unit,
            string actionKind,
            float decisionMs,
            float executionMs,
            float snapshotMs,
            float delayMs,
            string decisionBreakdown)
        {
            UnitLabel = unit != null ? $"{unit.UnitDisplayName}#{unit.InstanceId}" : "unit=null";
            ActionKind = actionKind;
            DecisionMs = decisionMs;
            ExecutionMs = executionMs;
            SnapshotMs = snapshotMs;
            DelayMs = delayMs;
            DecisionBreakdown = decisionBreakdown;
        }
    }

    private static bool IsNoOpUnitAction(PlayerAction action)
    {
        if (action == null) return false;
        if (!string.IsNullOrEmpty(action.TargetInstanceId)) return false;
        if (!string.IsNullOrEmpty(action.TargetConstructionId)) return false;
        if (!action.HasMoveTo || !action.HasMoveFrom) return false;

        Vector3Int from = action.MoveFrom; from.z = 0;
        Vector3Int to = action.MoveTo; to.z = 0;
        return from == to;
    }

    private int CompareFireSupportAttackInitiative(UnitManager a, UnitManager b, TeamId aiTeam)
    {
        if (!IsFireSupportUnit(a) || !IsFireSupportUnit(b))
            return 0;

        bool hasA = TryGetFireSupportCurrentAttackInitiative(a, aiTeam,
            out bool primaryA, out int eliteA, out BazookaTargetPriority prefA);
        bool hasB = TryGetFireSupportCurrentAttackInitiative(b, aiTeam,
            out bool primaryB, out int eliteB, out BazookaTargetPriority prefB);

        if (hasA != hasB)
            return hasA ? -1 : 1;
        if (!hasA)
            return 0;

        if (primaryA != primaryB)
            return primaryA ? -1 : 1;

        if (!primaryA)
        {
            int eliteCmp = eliteB.CompareTo(eliteA);
            if (eliteCmp != 0)
                return eliteCmp;
        }

        int prefCmp = prefB.CompareTo(prefA);
        return prefCmp != 0 ? prefCmp : 0;
    }

    private void SyncAIUnitCellsFromTransforms()
    {
        foreach (UnitManager unit in UnitManager.AllActive)
            GetLiveUnitCell(unit, syncState: true);
    }

    private Vector3Int GetLiveUnitCell(UnitManager unit, bool syncState = false)
    {
        if (unit == null)
            return Vector3Int.zero;

        Vector3Int stateCell = unit.CurrentCellPosition;
        stateCell.z = 0;

        if (unit.IsDead || unit.IsEmbarked || !unit.gameObject.activeInHierarchy || unit.BoardTilemap == null)
            return stateCell;

        Vector3Int worldCell = HexCoordinates.WorldToCell(unit.BoardTilemap, unit.transform.position);
        worldCell.z = 0;
        if (worldCell == stateCell)
            return stateCell;

        if (syncState)
        {
            unit.SetCurrentCellPosition(worldCell, enforceFinalOccupancyRule: false);
            Debug.Log($"{TL()} sync cell {unit.InstanceId}: state={stateCell} world={worldCell}");
        }

        return worldCell;
    }

    private bool ShouldDeferIdleAssaultForSectorCapturer(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (unit == null || plan == null) return false;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Assalto)
            return false;

        SectorObjective assaultObjective = ResolveAssignedAssaultObjective(unit, plan);
        if (assaultObjective == null) return false;

        foreach (SlotNeed slot in assaultObjective.Slots)
        {
            if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (capturer != null && !capturer.HasActed)
                return true;
        }

        return false;
    }

    private bool ShouldDeferAttackForFireSupportPrep(
        UnitManager attacker,
        PlayerAction action,
        TeamId aiTeam,
        out UnitManager fireSupport,
        out UnitManager target,
        out Vector3Int fireCell)
    {
        fireSupport = null;
        target = null;
        fireCell = Vector3Int.zero;

        if (attacker == null || action == null)
            return false;
        if (action.SensorAction != SensorActionType.Attack || string.IsNullOrEmpty(action.TargetInstanceId))
            return false;
        if (IsFireSupportUnit(attacker))
            return false;
        if (!int.TryParse(action.TargetInstanceId, out int targetId))
            return false;

        target = FindAttackPrepTarget(targetId, aiTeam);
        if (target == null)
            return false;

        int firesupportConsidered = 0;
        _initLogBuilder.Clear();
        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate == attacker)
                continue;
            if (!IsFireSupportUnit(candidate))
                continue;
            if (candidate.SlotIndex != currentAISlotIndex)
                continue;

            // Diagnóstico: por que esta artilharia não cedeu o amaciamento do alvo do atacante.
            if (candidate.HasActed || candidate.IsDead || candidate.IsEmbarked)
            {
                firesupportConsidered++;
                _initLogBuilder.AppendLine($"  {FormatInitiativeUnitName(candidate)}: indisponivel (acted={candidate.HasActed} dead={candidate.IsDead} embarked={candidate.IsEmbarked})");
                continue;
            }

            firesupportConsidered++;
            if (TryFindFireSupportPrepShot(candidate, target, aiTeam, out Vector3Int candidateCell, out string failReason))
            {
                fireSupport = candidate;
                fireCell = candidateCell;
                _initLogBuilder.Clear();
                return true;
            }

            _initLogBuilder.AppendLine($"  {FormatInitiativeUnitName(candidate)}: {failReason}");
        }

        // Atacante quer bater num alvo válido, existia artilharia no time, mas ninguém cedeu:
        // expõe o motivo (antes era um false silencioso).
        if (firesupportConsidered > 0)
        {
            Debug.Log($"{TL()} Fase2 — {FormatInitiativeUnitName(attacker)} NAO cedeu ataque em " +
                      $"{target.UnitDisplayName}#{target.InstanceId} (nenhum amaciamento de artilharia):\n{_initLogBuilder}");
        }
        _initLogBuilder.Clear();

        return false;
    }

    private bool ShouldDeferAttackForAirCombatPrep(
        UnitManager attacker,
        PlayerAction action,
        AIWorldSnapshot snapshot,
        TeamId aiTeam,
        out UnitManager airCombat,
        out UnitManager target,
        out Vector3Int attackCell)
    {
        airCombat = null;
        target = null;
        attackCell = Vector3Int.zero;

        if (attacker == null || action == null)
            return false;
        if (action.SensorAction != SensorActionType.Attack || string.IsNullOrEmpty(action.TargetInstanceId))
            return false;
        if (IsAirCombatUnit(attacker))
            return false;
        // Fire support amacia primeiro — nunca cede o tiro de prep a um combatente.
        // Sem esta guarda, uma AA (fire support, terrestre) cede ao air combat enquanto
        // o air combat cede a ela (ShouldDeferAttackForFireSupportPrep), gerando cessão mútua.
        if (IsFireSupportUnit(attacker))
            return false;
        if (!int.TryParse(action.TargetInstanceId, out int targetId))
            return false;

        target = FindAttackPrepTarget(targetId, aiTeam);
        if (target == null || !target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return false;
        bool targetIsAir = targetData.domain == Domain.Air;
        if (!targetIsAir && !IsGroundFrontlineEligibleToDeferToAirAttack(attacker))
            return false;

        _initLogBuilder.Clear();
        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate == attacker)
                continue;
            if (candidate.SlotIndex != snapshot.AISlotIndex || candidate.HasActed || candidate.IsDead || candidate.IsEmbarked || candidate.IsUnderRepair)
                continue;
            if (!IsAirCombatUnit(candidate))
                continue;
            if (!targetIsAir && !IsOffensiveAirCombatUnit(candidate))
                continue;
            if (TryFindAirCombatPrepShot(candidate, target, snapshot, aiTeam, applyAirCombatTargetGates: !targetIsAir, out Vector3Int candidateCell, out string failReason))
            {
                airCombat = candidate;
                attackCell = candidateCell;
                _initLogBuilder.Clear();
                return true;
            }

            _initLogBuilder.AppendLine($"  {FormatInitiativeUnitName(candidate)}: {failReason}");
        }

        if (_initLogBuilder.Length > 0)
        {
            Debug.Log($"{TL()} Fase2 — {FormatInitiativeUnitName(attacker)} NAO cedeu ataque aereo em " +
                      $"{target.UnitDisplayName}#{target.InstanceId}:\n{_initLogBuilder}");
            _initLogBuilder.Clear();
        }

        return false;
    }

    private static bool IsGroundFrontlineEligibleToDeferToAirAttack(UnitManager attacker)
    {
        if (attacker == null || !attacker.TryGetUnitData(out UnitData data) || data == null)
            return false;

        UnitRole role = UnitRoleCompatibility.ResolveCompositionRole(data);
        return data.domain == Domain.Land
            && (role == UnitRole.Assalto || role == UnitRole.Capturador);
    }

    private UnitManager FindAttackPrepTarget(int targetId, TeamId aiTeam)
    {
        MatchController mc = GetMatchController();
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.InstanceId != targetId)
                continue;
            if (unit.SlotIndex == currentAISlotIndex || unit.IsDead || unit.IsEmbarked)
                return null;
            if (mc != null &&
                !mc.IsUnitVisibleForSlot(unit, PlayerSlotId.FromIndex(currentAISlotIndex)))
                return null;
            return unit;
        }

        return null;
    }

    private bool TryFindFireSupportPrepShot(
        UnitManager fireSupport,
        UnitManager target,
        TeamId aiTeam,
        out Vector3Int fireCell,
        out string failReason)
    {
        fireCell = Vector3Int.zero;
        failReason = "sem-fire-support";
        if (fireSupport == null || target == null)
            return false;

        Vector3Int fromCell = fireSupport.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(fireSupport);
        HashSet<Vector3Int> occupied = BuildOccupied(fireSupport);
        bool stationary = IsLongRangeStationary(fireSupport);
        TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(ResolveAISlotKey(aiTeam)));
        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;

        bool sensorListedTarget = false;       // o sensor chegou a mirar o alvo de alguma célula?
        string lastDecisionReason = null;       // último motivo de PassesAttackDecision reprovar

        foreach (Vector3Int rawCell in EnumerateFireSupportCandidateCells(fromCell, paths, stationary))
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            if (cell != fromCell && IsCellACapturerTarget(cell, capPlan, aiTeam))
                continue;

            SensorMovementMode mode = cell != fromCell
                ? SensorMovementMode.MoveuAndando
                : SensorMovementMode.MoveuParado;

            var targets = new List<PodeMirarTargetOption>();
            if (!PodeMirarSensor.CollectTargets(
                    fireSupport,
                    boardTilemap,
                    terrainDatabase,
                    mode,
                    targets,
                    weaponPriorityData: weaponPriorityData,
                    dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
                    fromCell: cell))
                continue;

            foreach (PodeMirarTargetOption opt in targets)
            {
                if (opt == null || opt.targetUnit != target)
                    continue;
                sensorListedTarget = true;
                if (!PassesAttackDecision(fireSupport, target, cell, defensiveContext: false, out string decisionReason))
                {
                    lastDecisionReason = decisionReason;
                    continue;
                }

                fireCell = cell;
                failReason = null;
                return true;
            }
        }

        failReason = sensorListedTarget
            ? $"prep-reprovado-PassesAttackDecision ({lastDecisionReason})"
            : "sensor-nao-mira-alvo-de-nenhuma-celula";
        return false;
    }

    private bool TryFindAirCombatPrepShot(
        UnitManager airCombat,
        UnitManager target,
        AIWorldSnapshot snapshot,
        TeamId aiTeam,
        bool applyAirCombatTargetGates,
        out Vector3Int attackCell,
        out string failReason)
    {
        attackCell = Vector3Int.zero;
        failReason = "sem-air-combat";
        if (airCombat == null || target == null)
            return false;

        bool wasGrounded = airCombat.IsAircraftGrounded;
        List<int> takeoffMoveOptions = null;
        if (wasGrounded && !TryGetAITakeoffMoveOptions(airCombat, out takeoffMoveOptions, out _))
        {
            failReason = "decolagem-indisponivel";
            return false;
        }

        if (wasGrounded)
            airCombat.SetAircraftGrounded(false);

        try
        {
            Vector3Int fromCell = airCombat.CurrentCellPosition;
            fromCell.z = 0;
            Dictionary<Vector3Int, List<Vector3Int>> paths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap, airCombat, Mathf.Max(0, airCombat.RemainingMovementPoints), terrainDatabase);
            HashSet<Vector3Int> occupied = BuildAirOccupied(airCombat);
            if (paths == null || paths.Count == 0)
            {
                failReason = "sem-caminho";
                return false;
            }

            AIWorldSnapshot airSnapshot = snapshot ??
                AIWorldSnapshot.BuildLight(
                    PlayerSlotId.FromIndex(currentAISlotIndex),
                    matchController);
            List<UnitManager> visibleEnemies =
                CollectVisibleAirCombatEnemies(airCombat, airSnapshot);
            List<AirCombatTacticalCandidate> candidates =
                CollectAirCombatTacticalCandidates(
                    airCombat,
                    airSnapshot,
                    fromCell,
                    paths,
                    occupied,
                    takeoffMoveOptions,
                    visibleEnemies);
            ResolveAirCombatCandidateGates(
                candidates,
                out bool hasAttackableAircraft,
                out bool hasPreferredAttackableAircraft);
            bool targetBlockedByAirPriority = false;
            bool targetReachedBySensor = false;
            string lastAttackDecisionReason = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                AirCombatTacticalCandidate candidate = candidates[i];
                if (candidate == null || candidate.Target != target)
                    continue;

                Vector3Int cell = candidate.AttackCell;
                targetReachedBySensor = true;
                if (applyAirCombatTargetGates
                    && !ShouldConsiderAirCombatTarget(airCombat, target, hasAttackableAircraft, hasPreferredAttackableAircraft))
                {
                    targetBlockedByAirPriority = true;
                    continue;
                }
                if (applyAirCombatTargetGates
                    && !PassesAttackDecision(airCombat, target, cell, false, out lastAttackDecisionReason))
                    continue;

                attackCell = cell;
                failReason = null;
                return true;
            }

            if (targetBlockedByAirPriority)
                failReason = "prioridade-aerea-bloqueia-alvo-terrestre";
            else if (targetReachedBySensor)
                failReason = $"reprovado-PassesAttackDecision ({lastAttackDecisionReason})";
            else
                failReason = "nao-mira-alvo-de-nenhuma-celula";
        }
        finally
        {
            if (wasGrounded)
                airCombat.SetAircraftGrounded(true);
        }

        return false;
    }

    private bool ShouldDeferCapturerForAirTransportVacate(
        UnitManager unit,
        PlayerAction action,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out UnitManager airTransporter)
    {
        airTransporter = null;

        if (unit == null || unit.HasActed || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
            return false;

        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null
            || unitData.roles == null || !unitData.roles.Contains(UnitRole.Capturador))
            return false;

        if (action != null
            && (!string.IsNullOrEmpty(action.TargetInstanceId)
                || !string.IsNullOrEmpty(action.TargetConstructionId)
                || action.SensorAction == SensorActionType.Attack))
            return false;

        SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;
        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        foreach (UnitManager transporter in UnitManager.AllActive)
        {
            if (transporter == null || transporter == unit)
                continue;
            if (transporter.SlotIndex != currentAISlotIndex || transporter.HasActed || transporter.IsDead
                || transporter.IsEmbarked || transporter.IsUnderRepair)
                continue;
            if (!IsAirTransporter(transporter) || HasTransportCargo(transporter))
                continue;
            if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null
                || !transporterData.isTransporter)
                continue;
            if (FindFittingSlotIndex(transporter, transporterData, unit, unitData) < 0)
                continue;

            Vector3Int transporterCell = transporter.CurrentCellPosition;
            transporterCell.z = 0;
            if (!IsTeamProductionBuilding(transporterCell, aiTeam))
                continue;

            float pickupReach = Mathf.Max(8f,
                unit.RemainingMovementPoints + transporter.RemainingMovementPoints + ShuttlePickupRange + 1f);
            if (SectorManager.HexDistance(unitCell, transporterCell) > pickupReach)
                continue;

            SectorObjective transporterObjective = plan != null
                ? ResolveAssignedTransportObjective(transporter, plan)
                : null;
            if (assigned != null && transporterObjective != null
                && assigned.Sector != transporterObjective.Sector
                && !AreEmbarkSectorsCompatible(assigned.Sector, transporterObjective.Sector))
                continue;
            if (assigned == null && transporterObjective != null
                && !CanRogueUseAssignedTransporter(unit, transporter, transporterObjective, aiTeam))
                continue;

            airTransporter = transporter;
            return true;
        }

        return false;
    }
}

internal static class AIDecisionPerf
{
    private sealed class Entry
    {
        public float Milliseconds;
        public int Calls;
    }

    private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
    private static readonly Dictionary<string, long> Counters = new Dictionary<string, long>();
    private static readonly Dictionary<string, Entry> PhaseEntries = new Dictionary<string, Entry>();
    private static readonly Dictionary<string, long> PhaseCounters = new Dictionary<string, long>();
    private static bool active;
    private static bool phaseActive;

    public static void BeginPhase()
    {
        PhaseEntries.Clear();
        PhaseCounters.Clear();
        phaseActive = true;
    }

    public static void Begin()
    {
        Entries.Clear();
        Counters.Clear();
        active = true;
    }

    public static void Add(string stage, float milliseconds)
    {
        if (!active || string.IsNullOrEmpty(stage)) return;
        AddEntry(Entries, stage, milliseconds);
        if (phaseActive)
            AddEntry(PhaseEntries, stage, milliseconds);
    }

    public static void AddCount(string counter, long amount = 1)
    {
        if (!active || string.IsNullOrEmpty(counter) || amount == 0)
            return;

        AddCounter(Counters, counter, amount);
        if (phaseActive)
            AddCounter(PhaseCounters, counter, amount);
    }

    public static string End()
    {
        active = false;
        return Format("stages", Entries, "metrics", Counters);
    }

    public static string EndPhase()
    {
        phaseActive = false;
        return Format(
            "stages",
            PhaseEntries,
            "metrics",
            PhaseCounters);
    }

    private static void AddEntry(
        Dictionary<string, Entry> destination,
        string stage,
        float milliseconds)
    {
        if (!destination.TryGetValue(stage, out Entry entry))
        {
            entry = new Entry();
            destination.Add(stage, entry);
        }

        entry.Milliseconds += milliseconds;
        entry.Calls++;
    }

    private static void AddCounter(
        Dictionary<string, long> destination,
        string counter,
        long amount)
    {
        destination.TryGetValue(counter, out long current);
        destination[counter] = current + amount;
    }

    private static string Format(
        string stageLabel,
        Dictionary<string, Entry> entries,
        string counterLabel,
        Dictionary<string, long> counters)
    {
        var ordered = new List<KeyValuePair<string, Entry>>(entries);
        ordered.Sort((a, b) => b.Value.Milliseconds.CompareTo(a.Value.Milliseconds));
        var sb = new System.Text.StringBuilder(stageLabel).Append('=');
        if (ordered.Count == 0)
            sb.Append('-');
        for (int i = 0; i < ordered.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(ordered[i].Key)
              .Append(':').Append(ordered[i].Value.Milliseconds.ToString("F1"))
              .Append("ms/").Append(ordered[i].Value.Calls);
        }

        sb.Append(' ').Append(counterLabel).Append('=');
        if (counters.Count == 0)
        {
            sb.Append('-');
            return sb.ToString();
        }

        var orderedCounters =
            new List<KeyValuePair<string, long>>(counters);
        orderedCounters.Sort((a, b) =>
            string.CompareOrdinal(a.Key, b.Key));
        for (int i = 0; i < orderedCounters.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(orderedCounters[i].Key)
              .Append(':').Append(orderedCounters[i].Value);
        }
        return sb.ToString();
    }
}

internal readonly struct AIDecisionPerfScope : System.IDisposable
{
    private readonly string stage;
    private readonly float startedAt;

    public AIDecisionPerfScope(UnitManager unit, string stage)
    {
        this.stage = stage;
        startedAt = Time.realtimeSinceStartup;
    }

    public void Dispose()
    {
        AIDecisionPerf.Add(stage, (Time.realtimeSinceStartup - startedAt) * 1000f);
    }
}
