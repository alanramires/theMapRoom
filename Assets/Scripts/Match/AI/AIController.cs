using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Cérebro da IA V2. Orquestra as 4 fases do turno via coroutine,
/// usando HexEvaluator para posicionamento e ExecuteLiveAIBatch para execução.
/// </summary>
public partial class AIController : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [SerializeField] private ReplayManager replayManager;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    [Header("AI HUD")]
    [SerializeField] private bool showAIUnitHUD;


    private bool isActive;
    private bool isDebugPaused;
    private Coroutine aiCoroutine;

    public static bool IsDebugPaused { get; private set; }

    /// <summary>
    /// Pausa ou retoma o loop da IA sem cancelar o batch em andamento.
    /// </summary>
    public void SetDebugPaused(bool paused)
    {
        isDebugPaused = paused;
        IsDebugPaused = paused;

        Debug.Log(paused
            ? "[AI] Pausa de debug solicitada. Aguardando ponto seguro."
            : "[AI] Pausa de debug encerrada. Retomando IA.");
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        MatchController.OnActiveTeamChanged += HandleTeamChanged;
    }

    private void Start()
    {
        if (matchController == null)  matchController  = FindAnyObjectByType<MatchController>();
        if (replayManager == null)    replayManager    = FindAnyObjectByType<ReplayManager>();
        if (turnStateManager == null) turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (boardTilemap == null)
        {
            CursorController cursor = FindAnyObjectByType<CursorController>();
            if (cursor != null) boardTilemap = cursor.BoardTilemap;
        }

        if (terrainDatabase == null)
            terrainDatabase = Resources.Load<TerrainDatabase>("TerrainDatabase");

        Debug.Log($"[AI] Start — matchController={matchController != null} replayManager={replayManager != null} turnStateManager={turnStateManager != null}");

        // OnActiveTeamChanged pode ter disparado antes do Awake (raro mas possível).
        // Verifica se o time já ativo é IA e inicia o turno se necessário.
        if (matchController != null && matchController.IsPlayerAI(matchController.ActiveTeam))
        {
            Debug.Log($"[AI] Start — time ativo ({matchController.ActiveTeam}) já é IA, iniciando turno.");
            HandleTeamChanged((int)matchController.ActiveTeam);
        }
    }

    private void OnDestroy()
    {
        MatchController.OnActiveTeamChanged -= HandleTeamChanged;
        if (aiCoroutine != null) StopCoroutine(aiCoroutine);
        if (IsDebugPaused) IsDebugPaused = false;
    }

    // -------------------------------------------------------------------------
    // Entrada do turno
    // -------------------------------------------------------------------------

    private void HandleTeamChanged(int teamIndex)
    {
        TeamId newTeam = (TeamId)teamIndex;
        bool aiCheck = matchController != null && matchController.IsPlayerAI(newTeam);
        Debug.Log($"[AI] HandleTeamChanged — teamIndex={teamIndex} newTeam={newTeam} matchController={matchController != null} isAI={aiCheck}");

        if (matchController == null) return;

        if (aiCheck)
        {
            isActive = true;
            if (aiCoroutine != null) StopCoroutine(aiCoroutine);
            aiCoroutine = StartCoroutine(RunAITurn(newTeam));
        }
        else
        {
            isActive = false;
        }
    }

    // -------------------------------------------------------------------------
    // Loop principal de fases
    // -------------------------------------------------------------------------

    private IEnumerator RunAITurn(TeamId aiTeam)
    {
        Debug.Log($"[AI] RunAITurn iniciado para {aiTeam}.");
        yield return Phase0_WaitForTurnReady();
        yield return WaitIfDebugPaused();

        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);
        Debug.Log($"[AI] Turno {snapshot.TurnNumber} | Stance: {snapshot.Stance} " +
                  $"| {snapshot.MyUnits.Count} unidades | {snapshot.EnemyUnits.Count} inimigos " +
                  $"| R$ {snapshot.Budget}");

        BuildObjectivePlan(snapshot);

        yield return Phase1_CommandService(snapshot);
        yield return WaitIfDebugPaused();
        yield return Phase2_UnitActions(snapshot);
        yield return WaitIfDebugPaused();
        yield return Phase3_Shopping(snapshot);
        yield return WaitIfDebugPaused();
        yield return Phase4_EndTurn();

        aiCoroutine = null;
    }

    // -------------------------------------------------------------------------
    // Fase 0: Aguarda serviços automáticos de início de turno
    // -------------------------------------------------------------------------

    private IEnumerator Phase0_WaitForTurnReady()
    {
        // Um frame para que os handlers de OnActiveTeamChanged das outras systems
        // (supply queue, auto command service) registrem suas coroutines primeiro.
        yield return null;

        if (turnStateManager != null)
        {
            yield return new WaitUntil(() => !turnStateManager.IsAutoCommandServiceBusy);
            yield return new WaitUntil(() =>
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);
        }

        float batchDelay = GetBatchDelay();
        if (batchDelay > 0f) yield return new WaitForSeconds(batchDelay);

        Debug.Log("[AI] Fase 0 concluída.");
    }

    // -------------------------------------------------------------------------
    // Fase 1: Serviço do Comando
    // -------------------------------------------------------------------------

    private IEnumerator WaitIfDebugPaused()
    {
        if (!isDebugPaused) yield break;

        Debug.Log("[AI] Pausa de debug ativa - aguardando 'AI RESUME'.");
        yield return new WaitUntil(() => !isDebugPaused);
        Debug.Log("[AI] Retomando execucao da IA.");
    }

    private IEnumerator Phase1_CommandService(AIWorldSnapshot snapshot)
    {
        Debug.Log($"[AI] Fase 1 — iniciando. replayManager={replayManager != null} turnStateManager={turnStateManager != null}");

        if (replayManager == null)
        {
            Debug.LogWarning("[AI] Fase 1 — replayManager é null, abortando.");
            yield break;
        }

        if (matchController == null || !matchController.IsPlayerCommandServiceAutomatic(snapshot.AITeam))
        {
            Debug.Log("[AI] Fase 1 — commandServiceAutomatic=false, pulando.");
            yield break;
        }

        Debug.Log("[AI] Fase 1 — enviando batch CommandService.");
        replayManager.ExecuteLiveAIBatch(BuildCommandServiceBatch(snapshot.AITeam));
        yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);
        Debug.Log("[AI] Fase 1 — batch concluído. Aguardando IsAutoCommandServiceBusy...");

        if (turnStateManager != null)
            yield return new WaitUntil(() => !turnStateManager.IsAutoCommandServiceBusy);

        float delay = GetBatchDelay();
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        Debug.Log("[AI] Fase 1 — Serviço do Comando concluído.");
    }

    // -------------------------------------------------------------------------
    // Fase 2: Ações de unidades
    // -------------------------------------------------------------------------

    private IEnumerator Phase2_UnitActions(AIWorldSnapshot snapshot)
    {
        TeamId aiTeam = snapshot.AITeam;

        List<UnitManager> initial = GetAvailableUnits(aiTeam);
        if (initial.Count == 0)
        {
            Debug.Log("[AI] Fase 2 — sem unidades em campo, pulando.");
            yield break;
        }

        Debug.Log("[AI] Fase 2 — iniciando ações.");

        while (isActive)
        {
            yield return WaitIfDebugPaused();

            List<UnitManager> available = GetAvailableUnits(aiTeam);
            if (available.Count == 0) break;

            // Reconstrói a foto do mundo após cada batch — hexes ocupados mudam
            AIWorldSnapshot current = AIWorldSnapshot.Build(aiTeam, matchController);

            UnitManager unit = available[0];
            PlayerAction action = DecideUnitAction(unit, current);

            if (action == null)
            {
                Debug.LogWarning($"[AI] Sem decisão para {unit.InstanceId} — marcando como agida.");
                unit.MarkAsActed();
                continue;
            }

            replayManager.ExecuteLiveAIBatch(action);
            yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);
            yield return WaitIfDebugPaused();

            // Recalcula FoW apenas quando algo que altera visibilidade ocorreu:
            // movimento (nova posição = novo cone de visão) ou ataque (inimigo pode
            // ter morrido, liberando LOS para células antes bloqueadas).
            bool unitMoved    = action.HasMoveTo && action.MoveTo != action.MoveFrom;
            bool unitAttacked = !string.IsNullOrEmpty(action.TargetInstanceId);
            if (unitMoved || unitAttacked)
                matchController?.RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.DataOnly);

            float delay = GetBatchDelay();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        Debug.Log("[AI] Fase 2 concluída.");
    }

    // -------------------------------------------------------------------------
    // Fase 3: Compras
    // -------------------------------------------------------------------------

    private IEnumerator Phase3_Shopping(AIWorldSnapshot snapshot)
    {
        Debug.Log("[AI] Fase 3 — compras.");

        // Reconstrói snapshot para refletir o saldo atual pós-ações
        AIWorldSnapshot freshSnap = AIWorldSnapshot.Build(snapshot.AITeam, matchController);
        List<AIShoppingPlanner.ShoppingOrder> orders = AIShoppingPlanner.Decide(freshSnap);

        foreach (AIShoppingPlanner.ShoppingOrder order in orders)
        {
            if (!isActive) break;
            yield return WaitIfDebugPaused();

            PlayerAction batch = BuildShoppingBatch(snapshot.AITeam, order);
            Debug.Log($"[AI][Shopping] {order.UnitToBuy.name} @ {order.Building.CurrentCellPosition}");

            replayManager.ExecuteLiveAIBatch(batch);
            yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);
            yield return WaitIfDebugPaused();

            // Segurança: fecha o menu de shopping se ficou aberto (compra falhou)
            if (turnStateManager != null &&
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.ShoppingAndServices)
            {
                Debug.LogWarning("[AI][Shopping] Menu ficou aberto — fechando.");
                turnStateManager.HandleCancel();
            }

            float delay = GetBatchDelay();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        Debug.Log("[AI] Fase 3 concluída.");
    }

    // -------------------------------------------------------------------------
    // Fase 4: Passa a vez
    // -------------------------------------------------------------------------

    private IEnumerator Phase4_EndTurn()
    {
        Debug.Log("[AI] Fase 4 — passando a vez.");
        isActive = false;

        yield return new WaitUntil(() =>
            turnStateManager == null ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);

        if (replayManager != null)
        {
            TeamId aiTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
            replayManager.ExecuteLiveAIBatch(BuildEndTurnBatch(aiTeam));
        }
        else
        {
            matchController?.AdvanceTurnWithTransition();
        }
    }

    // -------------------------------------------------------------------------
    // Decisão de ação de unidade
    // -------------------------------------------------------------------------

    private PlayerAction DecideUnitAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        if (plan != null)
        {
            PlayerAction objectiveAction = TryDecideCapturerAction(unit, snapshot, plan);
            if (objectiveAction != null) return objectiveAction;
        }

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
            sb.AppendLine($"[AI][Think] Unidade {unit.InstanceId} ({unit.UnitDisplayName}) | role={resolvedRole} target={resolvedTarget}");
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
            Debug.Log($"[AI] {unit.InstanceId} → captura @ {destCell}");
            return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, destCell, paths);
        }

        // 2. Ataque: posição escolhida tem valor de combate
        if (chosen.combatValue > 0f)
        {
            bool hasMoved = destCell != fromCell;
            PodeMirarTargetOption target = FindBestAttackTarget(unit, destCell, hasMoved);
            if (target?.targetUnit != null)
            {
                Vector3Int targetCell = target.targetUnit.CurrentCellPosition; targetCell.z = 0;
                Debug.Log($"[AI] {unit.InstanceId} → ataca {target.targetUnit.InstanceId} de {destCell}");
                return BuildAttackBatch(
                    unit, snapshot.AITeam, fromCell, destCell,
                    target.targetUnit.InstanceId.ToString(), targetCell, paths);
            }
        }

        // 3. Movimento simples
        Debug.Log($"[AI] {unit.InstanceId} → move para {destCell}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, destCell, paths);
    }

    private PodeMirarTargetOption FindBestAttackTarget(UnitManager unit, Vector3Int fromCell, bool hasMoved)
    {
        var targets = new List<PodeMirarTargetOption>();
        SensorMovementMode mode = hasMoved
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;

        bool hasAny = PodeMirarSensor.CollectTargets(
            unit, boardTilemap, terrainDatabase, mode, targets, fromCell: fromCell);

        if (!hasAny || targets.Count == 0) return null;

        PodeMirarTargetOption best = null;
        int bestScore = int.MinValue;

        unit.TryGetUnitData(out UnitData attackerData);
        bool isCapturador = attackerData != null && attackerData.roles != null
            && attackerData.roles.Contains(UnitRole.Capturador);

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

            // Bônus por matar (HP <= dano esperado)
            if (attackerData != null && opt.targetUnit.TryGetUnitData(out UnitData defData))
            {
                // Heurística simples: alvo com ≤ 2 HP provavelmente morre
                if (opt.targetUnit.CurrentHP <= 2)
                    score += 5000;
            }

            // Penalidade por distância
            score -= opt.distance * 50;

            if (score > bestScore) { bestScore = score; best = opt; }
        }

        return best;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private List<UnitManager> GetAvailableUnits(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.HasActed || u.IsDead || u.IsEmbarked || u.HasMerged)
                continue;
            list.Add(u);
        }

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);

        list.Sort((a, b) =>
        {
            // Unidades mais próximas do objetivo agem primeiro — evita bloqueio de rotas
            float da = GetDistanceToAssignedTarget(a, aiTeam, plan);
            float db = GetDistanceToAssignedTarget(b, aiTeam, plan);
            int cmp = da.CompareTo(db);
            if (cmp != 0) return cmp;

            // Desempate: aiInitiative menor age primeiro, depois HP maior
            int ia = a.TryGetUnitData(out UnitData ua) ? (int)ua.aiInitiative : (int)AiInitiative.Medium;
            int ib = b.TryGetUnitData(out UnitData ub) ? (int)ub.aiInitiative : (int)AiInitiative.Medium;
            cmp = ia.CompareTo(ib);
            return cmp != 0 ? cmp : b.CurrentHP.CompareTo(a.CurrentHP);
        });

        return list;
    }

    private HashSet<Vector3Int> BuildOccupied(UnitManager excludeUnit)
    {
        var set = new HashSet<Vector3Int>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == excludeUnit || u.IsEmbarked || u.IsDead) continue;
            Vector3Int p = u.CurrentCellPosition; p.z = 0;
            set.Add(p);
        }
        return set;
    }

    private float GetBatchDelay()
    {
        if (replayManager != null)
            return Mathf.Max(0f, replayManager.GetEffectiveTimeBetweenBatchesForAutoplay());
        return 0.5f;
    }

    // -------------------------------------------------------------------------
    // Construtores de PlayerAction
    // -------------------------------------------------------------------------

    private PlayerAction BuildMoveBatch(UnitManager unit, TeamId team, Vector3Int from, Vector3Int to,
        Dictionary<Vector3Int, List<Vector3Int>> paths = null)
    {
        List<Vector3Int> movementPath = null;
        paths?.TryGetValue(to, out movementPath);
        return new PlayerAction
        {
            IsAIGenerated  = true,
            ActionType     = PlayerActionType.UnitAction,
            ActingTeam     = team,
            TurnNumber     = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex      = from, HasCursorHex = true,
            UnitInstanceId = unit.InstanceId.ToString(),
            MoveFrom       = from, HasMoveFrom = true,
            MoveTo         = to,   HasMoveTo   = true,
            SensorAction   = SensorActionType.None,
            MovementPath   = movementPath,
            DebugLabel     = $"AI Move {unit.InstanceId} → {to}",
        };
    }

    private PlayerAction BuildCaptureBatch(UnitManager unit, TeamId team, Vector3Int from, Vector3Int to,
        Dictionary<Vector3Int, List<Vector3Int>> paths = null)
    {
        List<Vector3Int> movementPath = null;
        paths?.TryGetValue(to, out movementPath);
        return new PlayerAction
        {
            IsAIGenerated  = true,
            ActionType     = PlayerActionType.UnitAction,
            ActingTeam     = team,
            TurnNumber     = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex      = from, HasCursorHex = true,
            UnitInstanceId = unit.InstanceId.ToString(),
            MoveFrom       = from, HasMoveFrom = true,
            MoveTo         = to,   HasMoveTo   = true,
            SensorAction   = SensorActionType.Capture,
            MovementPath   = movementPath,
            DebugLabel     = $"AI Capture {unit.InstanceId} @ {to}",
        };
    }

    private PlayerAction BuildAttackBatch(UnitManager unit, TeamId team,
        Vector3Int from, Vector3Int to, string targetId, Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths = null)
    {
        List<Vector3Int> movementPath = null;
        paths?.TryGetValue(to, out movementPath);
        return new PlayerAction
        {
            IsAIGenerated   = true,
            ActionType      = PlayerActionType.UnitAction,
            ActingTeam      = team,
            TurnNumber      = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex       = from, HasCursorHex = true,
            UnitInstanceId  = unit.InstanceId.ToString(),
            MoveFrom        = from, HasMoveFrom = true,
            MoveTo          = to,   HasMoveTo   = true,
            SensorAction    = SensorActionType.Attack,
            MovementPath    = movementPath,
            TargetInstanceId = targetId,
            TargetHex       = targetCell, HasTargetHex = true,
            DebugLabel      = $"AI Attack {unit.InstanceId} → {targetId} @ {targetCell}",
        };
    }

    private PlayerAction BuildMergeBatch(UnitManager unit, TeamId team,
        Vector3Int from, Vector3Int to, UnitManager target,
        Dictionary<Vector3Int, List<Vector3Int>> paths = null)
    {
        List<Vector3Int> movementPath = null;
        paths?.TryGetValue(to, out movementPath);
        Vector3Int targetCell = target.CurrentCellPosition; targetCell.z = 0;
        var action = new PlayerAction
        {
            IsAIGenerated    = true,
            ActionType       = PlayerActionType.UnitAction,
            ActingTeam       = team,
            TurnNumber       = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex        = from, HasCursorHex = true,
            UnitInstanceId   = unit.InstanceId.ToString(),
            MoveFrom         = from, HasMoveFrom = true,
            MoveTo           = to,   HasMoveTo   = true,
            SensorAction     = SensorActionType.Merge,
            MovementPath     = movementPath,
            DebugLabel       = $"AI Merge {unit.InstanceId} → {target.InstanceId}",
        };
        action.SubSteps.Add(new PlayerActionSubStep
        {
            Label            = "AIFuse",
            TargetInstanceId = target.InstanceId.ToString(),
            TargetHex        = targetCell,
            HasTargetHex     = true,
        });
        return action;
    }

    private PlayerAction BuildEndTurnBatch(TeamId team)
    {
        return new PlayerAction
        {
            IsAIGenerated = true,
            ActionType    = PlayerActionType.EndTurn,
            ActingTeam    = team,
            TurnNumber    = matchController != null ? matchController.CurrentTurn : 0,
            DebugLabel    = "AI EndTurn",
        };
    }

    private PlayerAction BuildCommandServiceBatch(TeamId team)
    {
        return new PlayerAction
        {
            IsAIGenerated = true,
            ActionType    = PlayerActionType.CommandService,
            ActingTeam    = team,
            TurnNumber    = matchController != null ? matchController.CurrentTurn : 0,
            SensorAction  = SensorActionType.CommandService,
            Confirmed     = true,
            DebugLabel    = "AI CommandService",
        };
    }

    private PlayerAction BuildShoppingBatch(TeamId team, AIShoppingPlanner.ShoppingOrder order)
    {
        Vector3Int cell = order.Building.CurrentCellPosition; cell.z = 0;
        string unitId = !string.IsNullOrWhiteSpace(order.UnitToBuy.id)
            ? order.UnitToBuy.id
            : order.UnitToBuy.name;

        return new PlayerAction
        {
            IsAIGenerated         = true,
            ActionType            = PlayerActionType.Shopping,
            ActingTeam            = team,
            TurnNumber            = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex             = cell, HasCursorHex = true,
            TargetHex             = cell,
            SensorAction          = SensorActionType.Shopping,
            ShoppingSelectedIndex = order.SelectedIndex,
            ShoppingUnitTypeId    = unitId,
            Confirmed             = true,
            DebugLabel            = $"AI Shopping: {order.UnitToBuy.name} @ {cell}",
        };
    }
}
