using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    public void SuppressNextNeutralConfirm()
    {
        suppressNextNeutralConfirm = true;
    }
    public bool HandleAutomatedSensorActionRequested(SensorActionType action)
    {
        switch (action)
        {
            case SensorActionType.None:
                return HandleAutomatedMoveOnlyActionRequested();
            case SensorActionType.Attack:
                HandleAimActionRequested(automatedSelection: true);
                return true;
            case SensorActionType.Embark:
                HandleEmbarkActionRequested();
                return true;
            case SensorActionType.Disembark:
                HandleDisembarkActionRequested();
                return true;
            case SensorActionType.Capture:
                HandleCaptureActionRequested();
                return true;
            case SensorActionType.Merge:
                HandleMergeActionRequested();
                return true;
            case SensorActionType.Supply:
                HandleSupplyActionRequested();
                return true;
            case SensorActionType.Transfer:
                HandleTransferActionRequested();
                return true;
            case SensorActionType.Land:
                HandleLandingSensorRequested();
                return true;
            case SensorActionType.CommandService:
                return HandleAutomatedCommandServiceRequested();
            case SensorActionType.RemoveUnit:
                return HandleAutomatedRemoveUnitRequested();
            case SensorActionType.Shopping:
                return false;
            default:
                return false;
        }
    }

    public bool HandleAutomatedMoveOnlyActionRequested()
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return false;

        HandleMoveOnlyActionRequested();
        return CurrentCursorState == CursorState.Neutral;
    }

    public bool TryAutomatedSelectUnitAndEnterMoveuParado(UnitManager unit)
    {
        if (unit == null || cursorController == null)
            return false;
        if (CurrentCursorState != CursorState.Neutral)
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        cursorController.SetCell(unitCell, playMoveSfx: false);

        // Confirm #1: seleciona unidade aliada.
        HandleConfirm();
        if (selectedUnit != unit)
            return false;

        // Confirm #2 no mesmo hex: entra em MoveuParado (sensores habilitados).
        cursorController.SetCell(unitCell, playMoveSfx: false);
        HandleConfirm();
        return CurrentCursorState == CursorState.MoveuParado || CurrentCursorState == CursorState.MoveuAndando;
    }

    // Seleciona a unidade e mantem em UnitSelected (sem confirmar "moveu parado").
    // Uso recomendado para IA quando ainda vai escolher destino de movimento.
    public bool TryAutomatedSelectUnitOnly(UnitManager unit)
    {
        if (unit == null || cursorController == null)
            return false;
        if (CurrentCursorState != CursorState.Neutral)
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        cursorController.SetCell(unitCell, playMoveSfx: false);

        HandleConfirm();
        return selectedUnit == unit && CurrentCursorState == CursorState.UnitSelected;
    }

    // Move a unidade selecionada (estado UnitSelected) ate destCell via confirm de cursor.
    // O movimento e assincrono (animacao); aguarde com WaitUntilMovementAnimationDone.
    public bool TryAutomatedMoveSelectedUnitToCell(Vector3Int destCell)
    {
        if (CurrentCursorState != CursorState.UnitSelected || selectedUnit == null || cursorController == null)
            return false;

        destCell.z = 0;
        if (!cursorController.SetCell(destCell, playMoveSfx: false))
            return false;

        HandleConfirm();
        return CurrentCursorState != CursorState.UnitSelected || IsMovementAnimationRunning();
    }

    public bool TryAutomatedSelectUnitByInstanceId(string unitInstanceId, Vector3Int expectedCell)
    {
        if (cursorController == null || string.IsNullOrWhiteSpace(unitInstanceId))
            return false;
        if (CurrentCursorState != CursorState.Neutral)
            return false;
        if (!int.TryParse(unitInstanceId, out int expectedId))
            return false;

        int activeTeam = matchController != null ? matchController.ActiveSlotId.Value : -1;
        UnitManager unit = null;
        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate.InstanceId != expectedId)
                continue;
            unit = candidate;
            break;
        }

        if (unit == null || unit.IsDead || unit.IsEmbarked || unit.HasActed)
            return false;
        if (activeTeam >= 0 && unit.SlotIndex != activeTeam)
            return false;

        expectedCell.z = 0;
        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        if (unitCell != expectedCell)
            return false;

        cursorController.SetCell(unitCell, playMoveSfx: false);

        double takeoffPerfStart = Time.realtimeSinceStartupAsDouble;
        TryPrepareTemporaryTakeoffStateForSelection(unit, out string takeoffInfo);
        RegisterPerfTakeoffPrepDuration((Time.realtimeSinceStartupAsDouble - takeoffPerfStart) * 1000d);
        if (!string.IsNullOrWhiteSpace(takeoffInfo) && SensorLogGate.IsPodeDecolarEnabled())
            SensorLogGate.Log("PodeDecolar", takeoffInfo);

        replayManager?.EnsureCurrentUnitActionBuffer(unit, unitCell);
        SetSelectedUnit(unit);
        ClearInspectedHelper();
        Advance(CursorState.UnitSelected);
        DialogManager.Instance?.MarkHintLearned(PlayerSlotId.FromIndex(unit.SlotIndex), HelpHintId.Act);
        return selectedUnit == unit && CurrentCursorState == CursorState.UnitSelected;
    }

    public bool TryExecuteAutomatedAttackFirstTarget()
    {
        return TryExecuteAutomatedAttackBestTarget(null);
    }

    // Ataque automatizado com criterio "burro por escolha" (AutomataData.targetPreference):
    // sem score tatico — ordem de spawn (fila do sensor), menor HP, maior HP ou aleatorio.
    // Usado pelo automata de tutorial; a IA oficial continua no BestTarget.
    public bool TryExecuteAutomatedAttackWithPreference(AutomataTargetPreference preference)
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return false;
        if (selectedUnit == null || selectedUnit.HasActed)
            return false;
        if (!HasAutomatedAttackAvailable())
            return false;
        if (!HandleAutomatedSensorActionRequested(SensorActionType.Attack))
            return false;

        if (cachedPodeMirarTargets == null || cachedPodeMirarTargets.Count <= 0)
        {
            HandleCancel();
            return false;
        }

        List<(UnitManager target, int order)> candidates = new List<(UnitManager target, int order)>();
        HashSet<UnitManager> seenTargets = new HashSet<UnitManager>();
        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[i];
            if (option == null || option.targetUnit == null)
                continue;

            UnitManager target = option.targetUnit;
            if (!seenTargets.Add(target))
                continue;
            if (!IsAutomatedAttackCandidateStillValid(selectedUnit, target))
                continue;

            candidates.Add((target, i));
        }

        if (candidates.Count <= 0)
        {
            HandleCancel();
            return false;
        }

        switch (preference)
        {
            case AutomataTargetPreference.LessHp:
                candidates.Sort((a, b) => a.target.CurrentHP != b.target.CurrentHP
                    ? a.target.CurrentHP.CompareTo(b.target.CurrentHP)
                    : a.order.CompareTo(b.order));
                break;
            case AutomataTargetPreference.MoreHp:
                candidates.Sort((a, b) => a.target.CurrentHP != b.target.CurrentHP
                    ? b.target.CurrentHP.CompareTo(a.target.CurrentHP)
                    : a.order.CompareTo(b.order));
                break;
            case AutomataTargetPreference.Random:
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }
                break;
            // SpawnOrder: mantem a fila do sensor (ordem de registro/spawn).
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            UnitManager target = candidates[i].target;
            if (target == null || target.IsDead)
                continue;
            if (!IsAutomatedAttackCandidateStillValid(selectedUnit, target))
                continue;

            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            if (TryExecuteAutomatedAttackReplayTarget(target.InstanceId.ToString(), targetCell))
            {
                Debug.Log($"[Automata][Attack] alvo por criterio {preference}: {target.name} (hp={target.CurrentHP}).");
                return true;
            }
        }

        HandleCancel();
        return false;
    }

    public bool TryExecuteAutomatedAttackBestTarget(UnitManager preferredTarget = null)
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return false;
        if (selectedUnit == null || selectedUnit.HasActed)
            return false;
        if (!HasAutomatedAttackAvailable())
            return false;
        if (!HandleAutomatedSensorActionRequested(SensorActionType.Attack))
            return false;

        if (cachedPodeMirarTargets == null || cachedPodeMirarTargets.Count <= 0)
        {
            HandleCancel();
            return false;
        }

        List<(UnitManager target, int score, int order)> rankedTargets = new List<(UnitManager target, int score, int order)>();
        HashSet<UnitManager> seenTargets = new HashSet<UnitManager>();

        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[i];
            if (option == null || option.targetUnit == null)
                continue;

            UnitManager target = option.targetUnit;
            if (seenTargets.Contains(target))
                continue;
            seenTargets.Add(target);

            if (!IsAutomatedAttackCandidateStillValid(selectedUnit, target))
                continue;

            int distance = Mathf.Max(1, option.distance);
            if (!TryEvaluateAutomatedAttackCandidateScore(
                    selectedUnit,
                    target,
                    distance,
                    // attackerProfile,
                    out int candidateScore))
            {
                continue;
            }

            if (preferredTarget != null && target == preferredTarget)
                candidateScore += 1;

            rankedTargets.Add((target, candidateScore, i));
        }

        if (rankedTargets.Count <= 0)
        {
            HandleCancel();
            return false;
        }

        rankedTargets.Sort((a, b) =>
        {
            int scoreCompare = b.score.CompareTo(a.score);
            if (scoreCompare != 0)
                return scoreCompare;

            // Tie-break deterministico: preserva a ordem original da fila.
            return a.order.CompareTo(b.order);
        });

        for (int i = 0; i < rankedTargets.Count; i++)
        {
            UnitManager target = rankedTargets[i].target;
            if (target == null || target.IsDead)
                continue;
            if (!IsAutomatedAttackCandidateStillValid(selectedUnit, target))
                continue;

            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            if (TryExecuteAutomatedAttackReplayTarget(target.InstanceId.ToString(), targetCell))
            {
                Debug.Log($"[AI][Attack] melhor alvo executado: {target.name} (score={rankedTargets[i].score}).");
                return true;
            }
        }

        HandleCancel();
        return false;
    }

    private bool IsAutomatedAttackCandidateStillValid(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return false;
        if (target.IsDead)
            return false;
        if (matchController != null && !matchController.IsUnitVisibleForActiveTeam(target))
            return false;
        if (attacker.HasActed)
            return false;
        if (!HasAutomatedAttackAvailable())
            return false;

        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[i];
            if (option == null || option.targetUnit == null)
                continue;
            if (option.targetUnit != target)
                continue;
            if (option.attackerUnit != null && option.attackerUnit != attacker)
                continue;
            return true;
        }

        return false;
    }

    private bool TryEvaluateAutomatedAttackCandidateScore(
        UnitManager attacker,
        UnitManager target,
        int distance,
        // AIUnitStanceBehavior attackerProfile,
        out int score)
    {
        score = int.MinValue;
        if (attacker == null || target == null)
            return false;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return false;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return false;
        if (rpsDatabase == null || dpqMatchupDatabase == null || weaponPriorityData == null)
            return false;

        AICombatHpSimulator.AICombatHpResult sim = AICombatHpSimulator.Simulate(
            attackerData,
            targetData,
            attacker.CurrentHP,
            target.CurrentHP,
            Mathf.Max(1, distance),
            rpsDatabase,
            dpqMatchupDatabase,
            weaponPriorityData);

        if (!sim.isValid)
            return false;

        int attackerMaxHp = Mathf.Max(1, attackerData.maxHP);
        int targetMaxHp = Mathf.Max(1, targetData.maxHP);
        int dealt = Mathf.Max(0, target.CurrentHP - sim.defenderHpAfter);
        int received = Mathf.Max(0, attacker.CurrentHP - sim.attackerHpAfter);
        bool surviveGate = sim.attackerHpAfter > 0;

        BazookaTargetPriority targetPriority = attackerData.ResolveAiTargetPriorityForTargetClass(targetData.unitClass);

        float dealtPercent = targetMaxHp > 0 ? (dealt * 100f) / targetMaxHp : 0f;
        float receivedPercent = attackerMaxHp > 0 ? (received * 100f) / attackerMaxHp : 0f;

        int targetPreferenceBonus = 0;
        /*if (attackerProfile != null)
        {
            targetPreferenceBonus = attackerProfile.GetTargetPreferenceBonus(targetPriority);

            if (!attackerProfile.PassesAttackThresholds(dealtPercent, receivedPercent, surviveGate))
                return false;
        }
        else*/
        {
            bool dealtGate = dealt >= Mathf.CeilToInt(targetMaxHp * 0.10f);
            bool receivedGate = received <= Mathf.FloorToInt(attackerMaxHp * 0.50f);
            if (!dealtGate || !receivedGate || !surviveGate)
                return false;
        }

        float damageRelative = targetMaxHp > 0 ? (float)dealt / targetMaxHp : 0f;
        float tieBreak = target.CurrentHP > 0 ? (1f / target.CurrentHP) : 1f;

        score = targetPreferenceBonus;
        if (sim.defenderHpAfter <= 0)
            score += 1000000;
        score += Mathf.RoundToInt(damageRelative * 10000f);
        score += Mathf.RoundToInt(tieBreak * 1000f);
        return true;
    }

    public bool TryExecuteAutomatedAttackPreferredTarget(UnitManager preferredTarget)
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return false;
        if (selectedUnit == null || selectedUnit.HasActed)
            return false;
        if (!HasAutomatedAttackAvailable())
            return false;
        if (!HandleAutomatedSensorActionRequested(SensorActionType.Attack))
            return false;

        if (cachedPodeMirarTargets == null || cachedPodeMirarTargets.Count <= 0)
        {
            HandleCancel();
            return false;
        }

        List<(UnitManager target, int score, int order)> rankedTargets = new List<(UnitManager target, int score, int order)>();
        HashSet<UnitManager> seenTargets = new HashSet<UnitManager>();
        bool preferredFoundInCurrentTargets = false;

        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[i];
            if (option == null || option.targetUnit == null)
                continue;

            UnitManager target = option.targetUnit;
            if (seenTargets.Contains(target))
                continue;
            seenTargets.Add(target);

            if (target == preferredTarget)
                preferredFoundInCurrentTargets = true;

            if (!IsAutomatedAttackCandidateStillValid(selectedUnit, target))
                continue;

            int distance = Mathf.Max(1, option.distance);
            if (!TryEvaluateAutomatedAttackCandidateScore(
                    selectedUnit,
                    target,
                    distance,
                    // null,
                    out int candidateScore))
            {
                continue;
            }

            if (preferredTarget != null && target == preferredTarget)
                candidateScore += 1;

            rankedTargets.Add((target, candidateScore, i));
        }

        if (rankedTargets.Count <= 0)
        {
            HandleCancel();
            return false;
        }

        rankedTargets.Sort((a, b) =>
        {
            int scoreCompare = b.score.CompareTo(a.score);
            if (scoreCompare != 0)
                return scoreCompare;

            return a.order.CompareTo(b.order);
        });

        if (preferredTarget != null && !preferredFoundInCurrentTargets)
            Debug.Log($"[AI][Attack] alvo preferido fora dos alvos miraveis apos mover: {preferredTarget.name}. Aplicando fallback para melhor alvo valido.");

        for (int i = 0; i < rankedTargets.Count; i++)
        {
            UnitManager target = rankedTargets[i].target;
            if (target == null || target.IsDead)
                continue;
            if (!IsAutomatedAttackCandidateStillValid(selectedUnit, target))
                continue;

            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            if (TryExecuteAutomatedAttackReplayTarget(target.InstanceId.ToString(), targetCell))
            {
                if (preferredTarget != null && target == preferredTarget)
                    Debug.Log($"[AI][Attack] alvo preferido executado: {target.name} (score={rankedTargets[i].score}).");
                else if (preferredTarget != null)
                    Debug.Log($"[AI][Attack] fallback executado: atacando {target.name} no lugar de {preferredTarget.name} (score={rankedTargets[i].score}).");
                else
                    Debug.Log($"[AI][Attack] ataque executado em: {target.name} (score={rankedTargets[i].score}).");
                return true;
            }
        }

        HandleCancel();
        return false;
    }

    public bool HasAutomatedAttackAvailable()
    {
        return availableSensorActionCodes != null && availableSensorActionCodes.Contains('A');
    }

    public bool HasAutomatedMergeAvailable()
    {
        return availableSensorActionCodes != null && availableSensorActionCodes.Contains('F');
    }

    public bool TryExecuteAutomatedMergePreferredTarget(UnitManager preferredTarget)
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return false;
        if (!HandleAutomatedSensorActionRequested(SensorActionType.Merge))
            return false;

        string targetId = preferredTarget != null ? preferredTarget.InstanceId.ToString() : null;
        if (!TryQueueAutomatedMergeReplayOrder(targetId))
        {
            HandleCancel();
            return false;
        }

        if (!TryStartAutomatedMergeReplayExecution())
        {
            HandleCancel();
            return false;
        }

        return true;
    }

    public bool HasAutomatedMoveAvailable()
    {
        return CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado;
    }

    public float GetAutomatedPhaseDelay()
    {
        if (replayManager != null)
        {
            float batchDelay = Mathf.Max(0f, replayManager.GetEffectiveTimeBetweenBatchesForAutoplay());
            float confirmDelay = Mathf.Max(0f, replayManager.GetEffectiveReplayConfirmVisualDelayForRuntimeMotion());
            return Mathf.Max(batchDelay, confirmDelay);
        }
        return AnimationManager.Instance != null ? AnimationManager.Instance.AIPhaseDuration : 0.5f;
    }

    public float GetAutomatedConfirmDelay()
    {
        if (replayManager != null)
            return Mathf.Max(0f, replayManager.GetEffectiveReplayConfirmVisualDelayForRuntimeMotion());
        return AnimationManager.Instance != null ? AnimationManager.Instance.AIUnitSelectDelay : 0.12f;
    }

    public float GetAutomatedShoppingNavDelay()
    {
        if (replayManager != null)
            return Mathf.Max(0f, replayManager.GetEffectiveShoppingNavDelayForRuntimeMotion());
        return GetAutomatedConfirmDelay();
    }

    // Abre ShoppingAndServices diretamente na construcao indicada,
    // mesmo quando ha unidade aliada (inclusive ja agida) sobre o hex.
    public bool TryAutomatedEnterShoppingAtConstruction(ConstructionManager construction)
    {
        if (construction == null || cursorController == null)
            return false;
        if (CurrentCursorState != CursorState.Neutral)
            return false;

        Vector3Int constructionCell = construction.CurrentCellPosition;
        constructionCell.z = 0;
        cursorController.SetCell(constructionCell, playMoveSfx: false, adjustCamera: false);

        // Automacao executa pela verdade privada do participante ativo, nao pela
        // perspectiva visual local. Em "fow on" o overlay/cache apresentado pode
        // pertencer ao humano enquanto a IA joga; usar ActiveTeamId (cor) aqui
        // fazia a abertura cair no gate de ownership do slot errado. Em
        // "fow partial" funcionava apenas porque observador visual e IA coincidiam.
        int activeSlot = matchController != null ? matchController.ActiveSlotId.Value : -1;
        if (!TryEnterConstructionShoppingState(construction, activeSlot))
            return false;

        // Mantem a semantica audiovisual do "Confirm" usada no fluxo normal/replay.
        cursorController.PlayConfirmSfx();

        return CurrentCursorState == CursorState.ShoppingAndServices;
    }

    // Intel da IA (Fase 0): verifica via PodeMirarSensor (MoveuParado) se a unidade
    // pode atacar QUALQUER inimigo visivel da posicao atual, sem precisar mover.
    // Retorna o primeiro alvo valido encontrado em 'firstTarget' (ou null se nenhum).
    public bool CanUnitFireAtAnyTargetFromCurrentPosition(UnitManager attacker, out UnitManager firstTarget)
    {
        firstTarget = null;
        if (attacker == null)
            return false;

        Tilemap map = terrainTilemap != null ? terrainTilemap : attacker.BoardTilemap;
        var tempTargets = new List<PodeMirarTargetOption>();
        bool canAim = PodeMirarSensor.CollectTargets(
            attacker,
            map,
            terrainDatabase,
            SensorMovementMode.MoveuParado,
            tempTargets);

        if (!canAim || tempTargets.Count == 0)
            return false;

        firstTarget = tempTargets[0]?.targetUnit;
        return true;
    }
    public bool CanUnitCaptureFromCurrentPosition(
        UnitManager unit,
        out ConstructionManager targetConstruction,
        out PodeCapturarSensor.CaptureOperationType operationType,
        out string reason)
    {
        targetConstruction = null;
        operationType = PodeCapturarSensor.CaptureOperationType.None;
        reason = string.Empty;

        if (unit == null)
            return false;

        Tilemap map = terrainTilemap != null ? terrainTilemap : unit.BoardTilemap;
        return PodeCapturarSensor.TryGetCaptureTarget(
            unit,
            map,
            SensorMovementMode.MoveuParado,
            out targetConstruction,
            out operationType,
            out reason,
            matchController);
    }

    // Retorna os alvos de suprimento validos para 'supplier' na posicao atual.
    // Delega para PodeSuprirSensor — mesma validacao usada pelo botao S do jogador.
    // A IA usa esta lista como candidatos e aplica seu proprio scoring de prioridade.
    public bool TryGetSupplyTargets(
        UnitManager supplier,
        List<PodeSuprirOption> output,
        out string reason)
    {
        reason = string.Empty;
        if (output == null || supplier == null)
            return false;

        Tilemap map = terrainTilemap != null ? terrainTilemap : supplier.BoardTilemap;
        return PodeSuprirSensor.CollectOptions(
            supplier, map, terrainDatabase, matchController,
            output, out reason);
    }

    // Marca a acao corrente no buffer de replay como gerada pela IA.
    // Chamar logo apos confirmar a selecao de uma unidade no turno da IA.
    public void MarkCurrentActionAsAIGenerated()
    {
        if (replayManager != null && replayManager.CurrentBuffer != null)
            replayManager.CurrentBuffer.IsAIGenerated = true;
    }

    public bool TryExecuteAutomatedSupplyPreferredTarget(UnitManager preferredTarget)
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return false;
        if (!HandleAutomatedSensorActionRequested(SensorActionType.Supply))
            return false;

        string targetId = preferredTarget != null ? preferredTarget.InstanceId.ToString() : null;
        if (!TryQueueAutomatedSupplyReplayOrder(targetId))
        {
            HandleCancel();
            return false;
        }

        if (!TryStartAutomatedSupplyReplayExecution())
        {
            HandleCancel();
            return false;
        }

        return true;
    }

    public bool TryExecuteAutomatedSupplyPreferredTargets(List<UnitManager> preferredTargets)
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return false;
        if (preferredTargets == null || preferredTargets.Count <= 0)
            return false;
        if (!HandleAutomatedSensorActionRequested(SensorActionType.Supply))
            return false;

        bool queuedAny = false;
        HashSet<int> seenTargets = new HashSet<int>();
        for (int i = 0; i < preferredTargets.Count; i++)
        {
            UnitManager target = preferredTargets[i];
            if (target == null || target.IsDead)
                continue;
            if (!seenTargets.Add(target.InstanceId))
                continue;

            if (TryQueueAutomatedSupplyReplayOrder(target.InstanceId.ToString()))
                queuedAny = true;
        }

        if (!queuedAny)
        {
            HandleCancel();
            return false;
        }

        if (TryStartAutomatedSupplyReplayExecution())
            return true;

        return true;
    }

    public bool TryExecuteAutomatedTransferReceive(
        ConstructionManager preferredConstruction = null,
        UnitManager preferredUnit = null)
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return false;
        if (!HandleAutomatedSensorActionRequested(SensorActionType.Transfer))
            return false;
        if (!IsTransferPromptActive() || transferPromptOptions.Count <= 0)
        {
            HandleCancel();
            return false;
        }

        int selectedIndex = -1;

        // 1) Preferencia explicita por construcao.
        if (preferredConstruction != null)
        {
            for (int i = 0; i < transferPromptOptions.Count; i++)
            {
                PodeTransferirOption option = transferPromptOptions[i];
                if (option == null)
                    continue;
                if (option.flowMode != TransferFlowMode.Recebedor)
                    continue;
                if (option.targetConstruction != preferredConstruction)
                    continue;
                selectedIndex = i;
                break;
            }
        }

        // 2) Preferencia explicita por unidade hub.
        if (selectedIndex < 0 && preferredUnit != null)
        {
            for (int i = 0; i < transferPromptOptions.Count; i++)
            {
                PodeTransferirOption option = transferPromptOptions[i];
                if (option == null)
                    continue;
                if (option.flowMode != TransferFlowMode.Recebedor)
                    continue;
                if (option.targetUnit != preferredUnit)
                    continue;
                selectedIndex = i;
                break;
            }
        }

        // 3) Primeiro Recebedor valido.
        if (selectedIndex < 0)
        {
            for (int i = 0; i < transferPromptOptions.Count; i++)
            {
                PodeTransferirOption option = transferPromptOptions[i];
                if (option == null)
                    continue;
                if (option.flowMode != TransferFlowMode.Recebedor)
                    continue;
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
        {
            HandleCancel();
            return false;
        }

        transferPromptSelectedIndex = selectedIndex;
        FocusTransferOptionByIndex(transferPromptSelectedIndex, playSfx: false);

        PodeTransferirOption selected = transferPromptOptions[selectedIndex];
        string targetId = selected != null && selected.targetUnit != null
            ? selected.targetUnit.InstanceId.ToString()
            : null;
        string targetConstructionId =
            selected != null
            && selected.targetConstruction != null
                ? selected.targetConstruction.InstanceId.ToString()
                : null;
        Vector3Int targetCell = selected != null ? selected.targetCell : Vector3Int.zero;
        targetCell.z = 0;

        // Usa o mesmo caminho oficial de replay/execucao do transfer automatizado.
        if (!TryExecuteAutomatedTransferReplayOrder(
                targetId,
                targetCell,
                TransferFlowMode.Recebedor,
                null,
                targetConstructionId))
        {
            HandleCancel();
            return false;
        }

        return true;
    }
    public bool TryExecuteAutomatedCaptureIfAvailable()
    {
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return false;
        if (availableSensorActionCodes == null || !availableSensorActionCodes.Contains('C'))
            return false;

        HandleAutomatedSensorActionRequested(SensorActionType.Capture);
        return true;
    }

    public IEnumerator WaitUntilAutomatedNeutralReady(float timeoutSeconds)
    {
        float timeout = Mathf.Max(0.2f, timeoutSeconds);
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            if (CurrentCursorState == CursorState.Neutral && !IsScannerActionExecutionInProgress && !IsMovementAnimationRunning())
                yield break;

            // PlayerMenu/Replay pausam a simulacao automatica sem consumir timeout.
            if (CurrentCursorState == CursorState.PlayerMenu || CurrentCursorState == CursorState.Replay)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    public IEnumerator MoveCursorToCellWithAutomatedTravel(Vector3Int targetCell, float stepDelay = -1f)
    {
        float resolvedStepDelay = stepDelay >= 0f
            ? stepDelay
            : (replayManager != null
                ? Mathf.Max(0f, replayManager.GetEffectiveCursorTravelStepDelayForRuntimeMotion())
                : 0.08f);

        if (!ShouldConcealAutomatedAiCursorTravelInFog()
            && TryBuildAutomatedTravelPathForSelectedUnit(targetCell, out List<Vector3Int> selectedUnitPath)
            && selectedUnitPath != null
            && selectedUnitPath.Count > 0)
        {
            for (int i = 0; i < selectedUnitPath.Count; i++)
            {
                Vector3Int stepCell = selectedUnitPath[i];
                stepCell.z = 0;
                cursorController.SetCell(stepCell, playMoveSfx: true, adjustCamera: false);
                cursorController.TryAdjustCameraToCursor();

                if (resolvedStepDelay > 0f)
                    yield return new WaitForSeconds(resolvedStepDelay);
                else
                    yield return null;
            }

            yield break;
        }

        yield return MoveCursorToCellLikeReplayAtTurnStart(targetCell, resolvedStepDelay);
    }

    private bool TryBuildAutomatedTravelPathForSelectedUnit(Vector3Int targetCell, out List<Vector3Int> path)
    {
        path = null;
        if (cursorController == null)
            return false;
        if (CurrentCursorState != CursorState.UnitSelected || selectedUnit == null)
            return false;
        if (movementPathsByCell == null || movementPathsByCell.Count == 0)
            return false;

        targetCell.z = 0;
        if (!movementPathsByCell.TryGetValue(targetCell, out List<Vector3Int> movementPath)
            || movementPath == null
            || movementPath.Count == 0)
            return false;

        Vector3Int currentCell = cursorController.CurrentCell;
        currentCell.z = 0;

        int startIndex = -1;
        for (int i = 0; i < movementPath.Count; i++)
        {
            Vector3Int p = movementPath[i];
            p.z = 0;
            if (p == currentCell)
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex < 0)
        {
            Vector3Int origin = selectedUnit.CurrentCellPosition;
            origin.z = 0;
            if (currentCell != origin)
                cursorController.SetCell(origin, playMoveSfx: false, adjustCamera: false);
            startIndex = 0;
        }

        path = new List<Vector3Int>();
        for (int i = startIndex + 1; i < movementPath.Count; i++)
        {
            Vector3Int step = movementPath[i];
            step.z = 0;
            path.Add(step);
        }

        return true;
    }

    public bool IsAutomatedMovementCellReachable(Vector3Int cell)
    {
        if (movementPathsByCell == null || movementPathsByCell.Count == 0)
            return false;

        cell.z = 0;
        return movementPathsByCell.ContainsKey(cell);
    }
    // Retorna os pontos de DPQ da celula para a unidade (terreno/estrutura/construcao).
    // Default = 1. Usado pela IA para preferir terreno defensivo.
    public int GetCellDpqPoints(Vector3Int cell, UnitManager unit)
    {
        if (unit == null)
            return 1;

        Tilemap board = terrainTilemap != null ? terrainTilemap : unit.BoardTilemap;
        if (board == null)
            return 1;

        cell.z = 0;
        Domain domain = unit.GetDomain();
        HeightLevel height = unit.GetHeightLevel();

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(board, cell);
        if (construction != null && TryGetConstructionDpq(construction, out DPQData constructionDpq) && constructionDpq != null)
            return constructionDpq.Pontos;

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(board, cell);
        if (structure != null && structure.dpqData != null)
            return structure.dpqData.Pontos;

        if (TryResolveTerrainAtCellForLayer(board, terrainDatabase, cell, domain, height, out TerrainTypeData terrain)
            && terrain != null && terrain.dpqData != null)
            return terrain.dpqData.Pontos;

        return 1;
    }

    // Equivalente ao ExecuteReplayConfirmInput do ReplayManager: confirma e toca o SFX correspondente.
    public void HandleConfirmWithFeedback()
    {
        ActionSfx sfx = HandleConfirm();
        if (cursorController == null)
            return;
        switch (sfx)
        {
            case ActionSfx.Confirm: cursorController.PlayConfirmSfx(); break;
            case ActionSfx.Cancel: cursorController.PlayCancelSfx(); break;
            case ActionSfx.Error:  cursorController.PlayErrorSfx();  break;
        }
    }

    // Retorna a melhor celula alcancavel em direcao ao targetCell.
    // Score = proximidade ao alvo + bonus de DPQ (quando prioritizeDpq=true e unidade fornecida).
    // Celulas em occupiedByAllies sao ignoradas.
    public bool TryGetBestReachableCellTowards(
        Vector3Int targetCell,
        HashSet<Vector3Int> occupiedByAllies,
        out Vector3Int bestCell,
        bool prioritizeDpq = false,
        UnitManager unit = null,
        HashSet<Vector3Int> penalizedCells = null,
        HashSet<Vector3Int> preferredCells = null)
    {
        bestCell = default;
        if (movementPathsByCell == null || movementPathsByCell.Count == 0)
            return false;

        targetCell.z = 0;
        Tilemap reference = terrainTilemap;
        Vector3 targetWorld = reference != null
            ? reference.GetCellCenterWorld(targetCell)
            : new Vector3(targetCell.x, targetCell.y, 0f);

        // Descobre o alcance maximo de distancia para normalizar
        float maxDistSq = 0f;
        foreach (var kvp in movementPathsByCell)
        {
            Vector3Int c = kvp.Key; c.z = 0;
            if (occupiedByAllies != null && occupiedByAllies.Contains(c)) continue;
            Vector3 w = reference != null ? reference.GetCellCenterWorld(c) : new Vector3(c.x, c.y, 0f);
            float d = (w - targetWorld).sqrMagnitude;
            if (d > maxDistSq) maxDistSq = d;
        }
        if (maxDistSq <= 0f) maxDistSq = 1f;

        float bestScore = float.MinValue;
        bool found = false;
        foreach (var kvp in movementPathsByCell)
        {
            Vector3Int cell = kvp.Key;
            cell.z = 0;
            if (occupiedByAllies != null && occupiedByAllies.Contains(cell))
                continue;

            Vector3 cellWorld = reference != null
                ? reference.GetCellCenterWorld(cell)
                : new Vector3(cell.x, cell.y, 0f);

            // Score de proximidade: 1 = no alvo, 0 = mais longe possivel
            float proximityScore = 1f - (cellWorld - targetWorld).sqrMagnitude / maxDistSq;

            // Score de DPQ: normalizado 0-1 (pontos vao de 0 a 4)
            float dpqScore = 0f;
            if (prioritizeDpq && unit != null)
                dpqScore = Mathf.Clamp01(GetCellDpqPoints(cell, unit) / 4f);

            // DPQ tem peso de 40% quando priorizando, para nao dominar completamente a decisao
            float score = prioritizeDpq
                ? proximityScore * 0.6f + dpqScore * 0.4f
                : proximityScore;
            if (penalizedCells != null && penalizedCells.Contains(cell))
                score -= 0.2f;
            if (preferredCells != null && preferredCells.Contains(cell))
                score += 0.12f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                found = true;
            }
        }
        return found;
    }

    // Versao alternativa para IA: escolhe a celula alcancavel com menor distancia hex ao alvo.
    // Evita desvios laterais causados por distancia euclidiana em mapas hex.
    public bool TryGetBestReachableCellTowardsHexDistance(
        Tilemap boardTilemap,
        Vector3Int targetCell,
        HashSet<Vector3Int> occupiedByAllies,
        out Vector3Int bestCell,
        bool prioritizeDpq = false,
        UnitManager unit = null,
        bool preferLongerAdvanceOnTie = false,
        bool preferShorterAdvanceOnTie = false,
        HashSet<Vector3Int> penalizedCells = null,
        HashSet<Vector3Int> preferredCells = null)
    {
        bestCell = default;
        if (movementPathsByCell == null || movementPathsByCell.Count == 0)
            return false;
        if (boardTilemap == null)
            return TryGetBestReachableCellTowards(targetCell, occupiedByAllies, out bestCell, prioritizeDpq, unit, penalizedCells, preferredCells);

        targetCell.z = 0;
        int bestHexDistance = int.MaxValue;
        int bestDpqBand = int.MinValue;
        int bestDpqPoints = int.MinValue;
        int bestAdvanceLen = int.MinValue;
        Vector3Int originCell = default;
        int originHexDistance = int.MaxValue;
        bool originResolved = false;
        bool found = false;

        foreach (var kvp in movementPathsByCell)
        {
            Vector3Int cell = kvp.Key;
            cell.z = 0;
            if (occupiedByAllies != null && occupiedByAllies.Contains(cell))
                continue;

            int dist = GetHexDistance(boardTilemap, cell, targetCell, 64);
            if (dist == int.MaxValue)
                continue;

            int dpqPoints = 1;
            int dpqBand = 0;
            int advanceLen = kvp.Value != null ? Mathf.Max(0, kvp.Value.Count - 1) : 0;
            if (!originResolved && advanceLen == 0)
            {
                originCell = cell;
                originHexDistance = dist;
                originResolved = true;
            }
            if (prioritizeDpq && unit != null)
            {
                dpqPoints = GetCellDpqPoints(cell, unit);
                dpqBand = ResolveDpqBand(dpqPoints);
            }

            bool cellPenalized = penalizedCells != null && penalizedCells.Contains(cell);
            bool bestPenalized = penalizedCells != null && found && penalizedCells.Contains(bestCell);
            bool cellPreferred = preferredCells != null && preferredCells.Contains(cell);
            bool bestPreferred = preferredCells != null && found && preferredCells.Contains(bestCell);
            bool better = dist < bestHexDistance
                || (dist == bestHexDistance && cellPenalized != bestPenalized && !cellPenalized)
                || (dist == bestHexDistance && cellPenalized == bestPenalized && cellPreferred != bestPreferred && cellPreferred)
                || (dist == bestHexDistance && cellPenalized == bestPenalized && cellPreferred == bestPreferred && dpqBand > bestDpqBand)
                || (dist == bestHexDistance && cellPenalized == bestPenalized && cellPreferred == bestPreferred && dpqBand == bestDpqBand && dpqPoints > bestDpqPoints)
                || (preferLongerAdvanceOnTie
                    && dist == bestHexDistance
                    && cellPenalized == bestPenalized
                    && cellPreferred == bestPreferred
                    && dpqBand == bestDpqBand
                    && dpqPoints == bestDpqPoints
                    && advanceLen > bestAdvanceLen)
                || (preferShorterAdvanceOnTie
                    && dist == bestHexDistance
                    && cellPenalized == bestPenalized
                    && cellPreferred == bestPreferred
                    && dpqBand == bestDpqBand
                    && dpqPoints == bestDpqPoints
                    && (bestAdvanceLen < 0 || advanceLen < bestAdvanceLen));
            if (!better)
                continue;

            bestHexDistance = dist;
            bestDpqBand = dpqBand;
            bestDpqPoints = dpqPoints;
            bestAdvanceLen = advanceLen;
            bestCell = cell;
            found = true;
        }

        // Destrava avancos em ataque quando o melhor local ainda eh "ficar parado".
        // Permite um pequeno detour (ex.: 1 hex para tras/lado) para preparar progresso no turno seguinte.
        if (preferLongerAdvanceOnTie
            && found
            && bestAdvanceLen <= 0
            && originResolved
            && bestCell == originCell)
        {
            int detourBestDist = int.MaxValue;
            int detourBestAdvance = int.MinValue;
            int detourBestDpqBand = int.MinValue;
            int detourBestDpqPoints = int.MinValue;
            Vector3Int detourCell = default;
            bool detourFound = false;

            foreach (var kvp in movementPathsByCell)
            {
                Vector3Int cell = kvp.Key;
                cell.z = 0;
                if (cell == originCell)
                    continue;
                if (occupiedByAllies != null && occupiedByAllies.Contains(cell))
                    continue;

                int dist = GetHexDistance(boardTilemap, cell, targetCell, 64);
                if (dist == int.MaxValue)
                    continue;

                // Limita detour para nao andar aleatoriamente para longe do objetivo.
                if (dist > originHexDistance + 2)
                    continue;

                int advanceLen = kvp.Value != null ? Mathf.Max(0, kvp.Value.Count - 1) : 0;
                int dpqPoints = 1;
                int dpqBand = 0;
                if (prioritizeDpq && unit != null)
                {
                    dpqPoints = GetCellDpqPoints(cell, unit);
                    dpqBand = ResolveDpqBand(dpqPoints);
                }

                bool cellPenalized = penalizedCells != null && penalizedCells.Contains(cell);
                bool detourBestPenalized = penalizedCells != null && detourFound && penalizedCells.Contains(detourCell);
                bool cellPreferred = preferredCells != null && preferredCells.Contains(cell);
                bool detourBestPreferred = preferredCells != null && detourFound && preferredCells.Contains(detourCell);
                bool better = !detourFound
                    || dist < detourBestDist
                    || (dist == detourBestDist && cellPenalized != detourBestPenalized && !cellPenalized)
                    || (dist == detourBestDist && cellPenalized == detourBestPenalized && cellPreferred != detourBestPreferred && cellPreferred)
                    || (dist == detourBestDist && cellPenalized == detourBestPenalized && cellPreferred == detourBestPreferred && advanceLen > detourBestAdvance)
                    || (dist == detourBestDist && cellPenalized == detourBestPenalized && cellPreferred == detourBestPreferred && advanceLen == detourBestAdvance && dpqBand > detourBestDpqBand)
                    || (dist == detourBestDist && cellPenalized == detourBestPenalized && cellPreferred == detourBestPreferred && advanceLen == detourBestAdvance && dpqBand == detourBestDpqBand && dpqPoints > detourBestDpqPoints);
                if (!better)
                    continue;

                detourBestDist = dist;
                detourBestAdvance = advanceLen;
                detourBestDpqBand = dpqBand;
                detourBestDpqPoints = dpqPoints;
                detourCell = cell;
                detourFound = true;
            }

            if (detourFound)
            {
                bestCell = detourCell;
                return true;
            }
        }

        return found;
    }

    // Retorna a melhor celula alcancavel cuja distancia hex ao alvo esteja entre [minHexDistance, maxHexDistance].
    // Quando preferMaxDistance=true, prefere ficar o mais longe possivel dentro da banda (util para artilharia de alcance).
    public bool TryGetBestReachableCellAtHexDistanceBand(
        Tilemap boardTilemap,
        Vector3Int targetCell,
        int minHexDistance,
        int maxHexDistance,
        HashSet<Vector3Int> occupiedByAllies,
        out Vector3Int bestCell,
        bool prioritizeDpq = false,
        UnitManager unit = null,
        bool preferMaxDistance = true,
        HashSet<Vector3Int> penalizedCells = null,
        HashSet<Vector3Int> preferredCells = null)
    {
        bestCell = default;
        if (movementPathsByCell == null || movementPathsByCell.Count == 0)
            return false;
        if (boardTilemap == null)
            return false;

        targetCell.z = 0;
        int minDist = Mathf.Max(0, minHexDistance);
        int maxDist = Mathf.Max(minDist, maxHexDistance);

        int bestHexDistance = preferMaxDistance ? int.MinValue : int.MaxValue;
        int bestDpqBand = int.MinValue;
        int bestDpqPoints = int.MinValue;
        bool found = false;

        foreach (var kvp in movementPathsByCell)
        {
            Vector3Int cell = kvp.Key;
            cell.z = 0;
            if (occupiedByAllies != null && occupiedByAllies.Contains(cell))
                continue;

            int dist = GetHexDistance(boardTilemap, cell, targetCell, 64);
            if (dist == int.MaxValue || dist < minDist || dist > maxDist)
                continue;

            int dpqPoints = 1;
            int dpqBand = 0;
            bool cellPenalized = penalizedCells != null && penalizedCells.Contains(cell);
            bool bestPenalized = penalizedCells != null && found && penalizedCells.Contains(bestCell);
            bool cellPreferred = preferredCells != null && preferredCells.Contains(cell);
            bool bestPreferred = preferredCells != null && found && preferredCells.Contains(bestCell);
            if (prioritizeDpq && unit != null)
            {
                dpqPoints = GetCellDpqPoints(cell, unit);
                dpqBand = ResolveDpqBand(dpqPoints);
            }

            bool better = false;
            if (!found)
            {
                better = true;
            }
            else if (preferMaxDistance)
            {
                better = dist > bestHexDistance
                    || (dist == bestHexDistance && cellPenalized != bestPenalized && !cellPenalized)
                    || (dist == bestHexDistance && cellPenalized == bestPenalized && cellPreferred != bestPreferred && cellPreferred)
                    || (dist == bestHexDistance && cellPenalized == bestPenalized && cellPreferred == bestPreferred && dpqBand > bestDpqBand)
                    || (dist == bestHexDistance && cellPenalized == bestPenalized && cellPreferred == bestPreferred && dpqBand == bestDpqBand && dpqPoints > bestDpqPoints);
            }
            else
            {
                better = dist < bestHexDistance
                    || (dist == bestHexDistance && cellPenalized != bestPenalized && !cellPenalized)
                    || (dist == bestHexDistance && cellPenalized == bestPenalized && cellPreferred != bestPreferred && cellPreferred)
                    || (dist == bestHexDistance && cellPenalized == bestPenalized && cellPreferred == bestPreferred && dpqBand > bestDpqBand)
                    || (dist == bestHexDistance && cellPenalized == bestPenalized && cellPreferred == bestPreferred && dpqBand == bestDpqBand && dpqPoints > bestDpqPoints);
            }

            if (!better)
                continue;

            bestHexDistance = dist;
            bestDpqBand = dpqBand;
            bestDpqPoints = dpqPoints;
            bestCell = cell;
            found = true;
        }

        return found;
    }

    // Faixas de prioridade pedidas para engajamento:
    // favoravel > melhorado > padrao > desfavoravel.
    private static int ResolveDpqBand(int dpqPoints)
    {
        if (dpqPoints >= 3) return 3; // favoravel (ex.: montanha)
        if (dpqPoints == 2) return 2; // melhorado
        if (dpqPoints == 1) return 1; // padrao
        return 0;                     // desfavoravel
    }

    private static int GetHexDistance(Tilemap tilemap, Vector3Int from, Vector3Int to, int maxSteps)
    {
        if (tilemap == null)
            return int.MaxValue;

        from.z = 0;
        to.z = 0;
        if (from == to)
            return 0;

        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { from };
        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        Queue<int> depth = new Queue<int>();
        frontier.Enqueue(from);
        depth.Enqueue(0);

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        int safeMax = Mathf.Clamp(maxSteps, 1, 256);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int d = depth.Dequeue();
            if (d >= safeMax)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int n = neighbors[i];
                n.z = 0;
                if (visited.Contains(n))
                    continue;

                if (n == to)
                    return d + 1;

                visited.Add(n);
                frontier.Enqueue(n);
                depth.Enqueue(d + 1);
            }
        }

        return int.MaxValue;
    }

    // Aguarda a animacao de movimento concluir e o estado ficar em MoveuAndando ou MoveuParado.
    public IEnumerator WaitUntilMovementAnimationDone(float timeoutSeconds)
    {
        float endTime = Time.time + Mathf.Max(0.5f, timeoutSeconds);
        while (Time.time < endTime)
        {
            if (!IsMovementAnimationRunning() &&
                (CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado))
                yield break;
            yield return null;
        }
    }

    public float GetAutomatedPreSelectDelay()
    {
        return animationManager != null ? animationManager.TurnStartFuelDeathCursorFocusDelay : 0.20f;
    }

    public float GetAutomatedBetweenUnitsDelay()
    {
        return animationManager != null ? animationManager.TurnStartFuelDeathBetweenKillsDelay : 0.15f;
    }

    public bool HandleAutomatedCommandServiceRequested()
    {
        if (CurrentCursorState != CursorState.Neutral)
            return false;

        TryCloseThreatLayerHotzone();
        if (!TryPreviewCommandServiceOrder(out _, emitLogs: false))
            return false;

        EnterCommandServiceState("HandleAutomatedCommandServiceRequested");
        return true;
    }

    public bool HandleAutomatedRemoveUnitRequested()
    {
        if (CurrentCursorState != CursorState.Neutral)
            return false;

        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out _))
            return false;

        string targetName = ResolveDebugUnitName(target);
        PanelDialogController.TrySetExternalText($"Destroy Unit :: {targetName} {FormatMapCellWithZ(cursorCell)} :: Confirm");
        Advance(CursorState.RemovingUnit);
        return true;
    }
}
