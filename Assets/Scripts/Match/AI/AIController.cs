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


    private bool   isActive;
    private bool   isDebugPaused;
    private bool   isDebugShoppingPaused;
    private Coroutine aiCoroutine;
    private int    aiTurnNumber;
    private string aiTeamTag;
    [SerializeField, Range(0, 4)] private int currentAIStage;
    [SerializeField] private TeamId currentAITeam = TeamId.Neutral;
    private enum DebugStepRequest
    {
        None,
        Prepare,
        Execute
    }

    private DebugStepRequest debugStepRequest;
    private PlayerAction debugStepPendingAction;

    private string TL(string category = "")
        => string.IsNullOrEmpty(category)
            ? $"[AI {aiTeamTag}][T{aiTurnNumber}]"
            : $"[AI {aiTeamTag}][T{aiTurnNumber}][{category}]";

    public static bool IsDebugPaused { get; private set; }
    public static bool IsDebugShoppingPaused { get; private set; }
    public bool IsAIRuntimeActive => isActive;
    public int CurrentAIStage => currentAIStage;
    public TeamId CurrentAITeam => currentAITeam;
    public int CurrentAITurnNumber => aiTurnNumber;

    public void RestoreAIRuntimeState(bool active, TeamId team, int turnNumber, int stage)
    {
        isActive = active;
        currentAITeam = team;
        aiTurnNumber = turnNumber;
        currentAIStage = Mathf.Clamp(stage, 0, 4);
        aiTeamTag = team == TeamId.Neutral ? string.Empty : TeamUtils.GetName(team).ToUpper();
    }

    /// <summary>
    /// Pausa ou retoma o loop da IA sem cancelar o batch em andamento.
    /// </summary>
    public void SetDebugPaused(bool paused)
    {
        isDebugPaused = paused;
        IsDebugPaused = paused;

        if (!paused)
        {
            debugStepRequest = DebugStepRequest.None;
            ClearDebugStepPreview();
            PanelDialogController.TrySetTransientText("AI RESUME", 1.8f);
        }

        Debug.Log(paused
            ? "[AI] Pausa de debug solicitada. Aguardando ponto seguro."
            : "[AI] Pausa de debug encerrada. Retomando IA.");

        if (paused)
            PanelDialogController.TrySetExternalText("AI PAUSE\nAguardando AI RESUME ou AI STEP");
    }

    public void RequestDebugStep()
    {
        if (!isDebugPaused)
        {
            Debug.Log("[AI Step] Ignorado: acione AI Pause antes de usar AI Step.");
            PanelDialogController.TrySetTransientText("AI STEP ignorado: use AI PAUSE primeiro", 2.0f);
            return;
        }

        debugStepRequest = debugStepPendingAction != null
            ? DebugStepRequest.Execute
            : DebugStepRequest.Prepare;

        Debug.Log(debugStepRequest == DebugStepRequest.Prepare
            ? "[AI Step] Preparando proximo batch."
            : "[AI Step] Executando batch preparado.");

        PanelDialogController.TrySetTransientText(
            debugStepRequest == DebugStepRequest.Prepare
                ? "AI STEP: preparando batch"
                : "AI STEP: executando batch",
            1.6f);
    }

    public void SetDebugShoppingPaused(bool paused)
    {
        isDebugShoppingPaused = paused;
        IsDebugShoppingPaused = paused;

        Debug.Log(paused
            ? "[AI Shopping] Fase 3 pausada. A IA vai parar antes das compras."
            : "[AI Shopping] Fase 3 liberada. A IA pode entrar nas compras.");

        if (paused)
            PanelDialogController.TrySetTransientText("AI SHOPPING PAUSE", 2.0f);
        else
            PanelDialogController.TrySetTransientText("AI SHOPPING RESUME", 1.8f);
    }

    public bool TryStartDebugStage(int stage)
    {
        if (stage < 1 || stage > 3)
        {
            Debug.Log($"[AI Stage] Stage invalido: {stage}. Use 1, 2 ou 3.");
            PanelDialogController.TrySetTransientText("AI STAGE invalido: use 1, 2 ou 3", 2.2f);
            return false;
        }

        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (replayManager == null)
            replayManager = FindAnyObjectByType<ReplayManager>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (matchController == null)
        {
            Debug.Log("[AI Stage] MatchController nao encontrado.");
            PanelDialogController.TrySetTransientText("AI STAGE: MatchController nao encontrado", 2.2f);
            return false;
        }

        TeamId aiTeam = matchController.ActiveTeam;
        if (!matchController.IsPlayerAI(aiTeam))
        {
            Debug.Log($"[AI Stage] Time ativo {aiTeam} nao e IA.");
            PanelDialogController.TrySetTransientText($"AI STAGE: time ativo {aiTeam} nao e IA", 2.2f);
            return false;
        }

        if (aiCoroutine != null)
            StopCoroutine(aiCoroutine);

        isActive = true;
        debugStepRequest = DebugStepRequest.None;
        ClearDebugStepPreview();
        aiCoroutine = StartCoroutine(RunAIDebugStage(aiTeam, stage));
        Debug.Log($"[AI Stage] Reiniciando IA no stage {stage} para {aiTeam}.");
        PanelDialogController.TrySetTransientText($"AI STAGE {stage}", 2.0f);
        return true;
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
        if (IsDebugShoppingPaused) IsDebugShoppingPaused = false;
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
            currentAIStage = 0;
            currentAITeam = TeamId.Neutral;
        }
    }

    // -------------------------------------------------------------------------
    // Loop principal de fases
    // -------------------------------------------------------------------------

    private IEnumerator RunAITurn(TeamId aiTeam)
    {
        Debug.Log($"[AI] RunAITurn iniciado para {aiTeam}.");
        currentAITeam = aiTeam;
        currentAIStage = 0;
        yield return Phase0_WaitForTurnReady();
        yield return WaitIfDebugPaused();

        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);
        aiTurnNumber = snapshot.TurnNumber;
        aiTeamTag    = TeamUtils.GetName(aiTeam).ToUpper();
        Debug.Log($"{TL()} Turno {snapshot.TurnNumber} | Stance: {snapshot.Stance} " +
                  $"| {snapshot.MyUnits.Count} unidades | {snapshot.EnemyUnits.Count} inimigos visíveis " +
                  $"| R$ {snapshot.Budget}");

        currentAIStage = 1;
        BuildObjectivePlan(snapshot);

        yield return Phase1_CommandService(snapshot);
        yield return WaitIfDebugPaused();
        currentAIStage = 2;
        yield return Phase2_UnitActions(snapshot);
        yield return WaitIfDebugPaused();
        yield return WaitIfDebugShoppingPaused();
        yield return WaitIfDebugPaused();
        currentAIStage = 3;
        yield return Phase3_Shopping(snapshot);
        yield return WaitIfDebugPaused();
        currentAIStage = 4;
        yield return Phase4_EndTurn();

        currentAIStage = 0;
        currentAITeam = TeamId.Neutral;
        aiCoroutine = null;
    }

    private IEnumerator RunAIDebugStage(TeamId aiTeam, int stage)
    {
        currentAITeam = aiTeam;
        currentAIStage = Mathf.Clamp(stage, 1, 3);
        yield return WaitIfDebugPaused();
        yield return new WaitUntil(() => replayManager == null || !replayManager.IsStepExecutionBusy);
        yield return new WaitUntil(() =>
            turnStateManager == null ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);

        AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiTeam, matchController);
        aiTurnNumber = snapshot.TurnNumber;
        aiTeamTag    = TeamUtils.GetName(aiTeam).ToUpper();
        Debug.Log($"{TL("Stage")} Inicio debug stage {stage} | Stance: {snapshot.Stance} " +
                  $"| {snapshot.MyUnits.Count} unidades | {snapshot.EnemyUnits.Count} inimigos visiveis " +
                  $"| R$ {snapshot.Budget}");

        currentAIStage = 1;
        BuildObjectivePlan(snapshot);

        if (stage <= 1)
        {
            currentAIStage = 1;
            yield return Phase1_CommandService(snapshot);
            yield return WaitIfDebugPaused();
        }

        if (stage <= 2)
        {
            currentAIStage = 2;
            yield return Phase2_UnitActions(snapshot);
            yield return WaitIfDebugPaused();
        }

        currentAIStage = 2;
        yield return WaitIfDebugShoppingPaused();
        yield return WaitIfDebugPaused();
        currentAIStage = 3;
        yield return Phase3_Shopping(snapshot);
        yield return WaitIfDebugPaused();
        currentAIStage = 4;
        yield return Phase4_EndTurn();

        currentAIStage = 0;
        currentAITeam = TeamId.Neutral;
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

        Debug.Log($"{TL()} Fase0 concluída.");
    }

    // -------------------------------------------------------------------------
    // Fase 1: Serviço do Comando
    // -------------------------------------------------------------------------

    private IEnumerator WaitIfDebugPaused()
    {
        if (!isDebugPaused) yield break;

        Debug.Log("[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.");
        yield return new WaitUntil(() => !isDebugPaused || debugStepRequest != DebugStepRequest.None);
        if (!isDebugPaused)
            Debug.Log("[AI] Retomando execucao da IA.");
    }

    private IEnumerator WaitIfDebugShoppingPaused()
    {
        if (!isDebugShoppingPaused) yield break;

        Debug.Log($"{TL("Shopping")} Pausado antes da Fase 3 - aguardando 'AI SHOPPING RESUME'.");
        PanelDialogController.TrySetExternalText("AI Shopping pausado\nAI SHOPPING RESUME para liberar compras");
        yield return new WaitUntil(() => !isDebugShoppingPaused);
        PanelDialogController.ClearExternalText();
        Debug.Log($"{TL("Shopping")} Resume recebido - entrando na Fase 3.");
    }

    private IEnumerator Phase1_CommandService(AIWorldSnapshot snapshot)
    {
        Debug.Log($"{TL()} Fase1 — iniciando. replayManager={replayManager != null} turnStateManager={turnStateManager != null}");

        if (replayManager == null)
        {
            Debug.LogWarning($"{TL()} Fase1 — replayManager é null, abortando.");
            yield break;
        }

        if (matchController == null || !matchController.IsPlayerCommandServiceAutomatic(snapshot.AITeam))
        {
            Debug.Log($"{TL()} Fase1 — commandServiceAutomatic=false, pulando.");
            yield break;
        }

        Debug.Log($"{TL()} Fase1 — enviando batch CommandService.");
        yield return ExecuteAIBatchWithDebugStep(BuildCommandServiceBatch(snapshot.AITeam));
        Debug.Log($"{TL()} Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...");

        if (turnStateManager != null)
            yield return new WaitUntil(() => !turnStateManager.IsAutoCommandServiceBusy);

        float delay = GetBatchDelay();
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        Debug.Log($"{TL()} Fase1 — Serviço do Comando concluído.");
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
            Debug.Log($"{TL()} Fase 2 — sem unidades em campo, pulando.");
            yield break;
        }

        Debug.Log($"{TL()} Fase2 — iniciando ações.");

        while (isActive)
        {
            yield return WaitIfDebugPaused();

            List<UnitManager> available = GetAvailableUnits(aiTeam);
            if (available.Count == 0) break;

            // Reconstrói a foto do mundo após cada batch — hexes ocupados mudam
            AIWorldSnapshot current = AIWorldSnapshot.Build(aiTeam, matchController);

            // Ordena iniciativa por grupo (menor = age primeiro):
            // 0 = vacater handoff  1 = reparo sobre capturável (libera prédio imediatamente)
            // 2 = objetivo normal  3 = rogue/sem objetivo
            // 4 = reparo em campo (age por último — base pode estar vazia antes das compras)
            TeamObjectivePlan activePlan = ObjectiveManager.GetPlanForTeam(aiTeam);
            available.Sort((a, b) =>
            {
                int groupA = GetInitiativeGroup(a, activePlan, aiTeam);
                int groupB = GetInitiativeGroup(b, activePlan, aiTeam);

                // Blocker cross-group: B fisicamente no target de A → B age primeiro para desocupar
                if (activePlan != null)
                {
                    Vector3Int? aTarget = GetAssignedTargetCell(a, activePlan);
                    if (aTarget.HasValue)
                    {
                        Vector3Int bCell = b.CurrentCellPosition; bCell.z = 0;
                        if (bCell == aTarget.Value) return 1;
                    }
                    Vector3Int? bTarget = GetAssignedTargetCell(b, activePlan);
                    if (bTarget.HasValue)
                    {
                        Vector3Int aCell = a.CurrentCellPosition; aCell.z = 0;
                        if (aCell == bTarget.Value) return -1;
                    }
                }

                if (groupA != groupB) return groupA.CompareTo(groupB);

                // Dentro do grupo 2: prioridade do objetivo (pri=1 = age primeiro)
                if (groupA == 2 && activePlan != null)
                {
                    SectorObjective objA = ResolveAssignedObjective(a, activePlan);
                    SectorObjective objB = ResolveAssignedObjective(b, activePlan);
                    if (objA == null && objB == null) return 0;
                    if (objA == null) return 1;
                    if (objB == null) return -1;
                    return objA.Priority.CompareTo(objB.Priority);
                }

                return 0;
            });

            UnitManager unit = available[0];
            PlayerAction action = DecideUnitAction(unit, current);

            if (action == null)
            {
                Debug.LogWarning($"[AI] Sem decisão para {unit.InstanceId} — marcando como agida.");
                unit.MarkAsActed();
                continue;
            }

            // Recalcula FoW apenas quando algo que altera visibilidade ocorreu:
            // movimento (nova posição = novo cone de visão) ou ataque (inimigo pode
            // ter morrido, liberando LOS para células antes bloqueadas).
            bool unitMoved    = action.HasMoveTo && action.MoveTo != action.MoveFrom;
            bool unitAttacked = !string.IsNullOrEmpty(action.TargetInstanceId);
            yield return ExecuteAIBatchWithDebugStep(action);
            yield return WaitIfDebugPaused();

            if (unitMoved || unitAttacked)
                matchController?.RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.DataOnly);

            float delay = GetBatchDelay();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        Debug.Log($"{TL()} Fase2 concluída.");
    }

    // -------------------------------------------------------------------------
    // Fase 3: Compras
    // -------------------------------------------------------------------------

    private IEnumerator Phase3_Shopping(AIWorldSnapshot snapshot)
    {
        Debug.Log($"{TL()} Fase3 — compras.");

        // Reconstrói snapshot para refletir o saldo atual pós-ações
        AIWorldSnapshot freshSnap = AIWorldSnapshot.Build(snapshot.AITeam, matchController);
        List<AIShoppingPlanner.ShoppingOrder> orders = AIShoppingPlanner.Decide(freshSnap);

        foreach (AIShoppingPlanner.ShoppingOrder order in orders)
        {
            if (!isActive) break;
            yield return WaitIfDebugPaused();
            yield return WaitIfDebugShoppingPaused();
            yield return WaitIfDebugPaused();

            PlayerAction batch = BuildShoppingBatch(snapshot.AITeam, order);
            Debug.Log($"{TL("Shopping")} {order.UnitToBuy.name} @ {order.Building.CurrentCellPosition}");

            yield return ExecuteAIBatchWithDebugStep(batch);
            yield return WaitIfDebugPaused();

            // Segurança: fecha o menu de shopping se ficou aberto (compra falhou)
            if (turnStateManager != null &&
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.ShoppingAndServices)
            {
                Debug.LogWarning($"{TL("Shopping")} Menu ficou aberto — fechando.");
                turnStateManager.HandleCancel();
            }

            float delay = GetBatchDelay();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        Debug.Log($"{TL()} Fase3 concluída.");
    }

    // -------------------------------------------------------------------------
    // Fase 4: Passa a vez
    // -------------------------------------------------------------------------

    private IEnumerator Phase4_EndTurn()
    {
        Debug.Log($"{TL()} Fase4 — passando a vez.");
        isActive = false;

        yield return new WaitUntil(() =>
            turnStateManager == null ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral);

        if (replayManager != null)
        {
            TeamId aiTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
            yield return ExecuteAIBatchWithDebugStep(BuildEndTurnBatch(aiTeam));
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

    private static Vector3Int? GetAssignedTargetCell(UnitManager unit, TeamObjectivePlan plan)
    {
        SectorObjective obj = ResolveAssignedObjective(unit, plan);
        if (obj == null) return null;
        ConstructionManager tgt = FindCapturableInSector(obj.Sector, unit.TeamId);
        if (tgt == null) return null;
        Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
        return tc;
    }

    // Grupo de iniciativa (menor = age primeiro):
    // 0 = vacater handoff, 1 = reparo sobre capturável não-completo (libera prédio),
    // 2 = objetivo normal, 3 = rogue/sem objetivo, 4 = reparo em campo (age por último).
    private int GetInitiativeGroup(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan != null && plan.HandoffVacaterIds.Contains(unit.InstanceId)) return 0;
        if (unit.IsUnderRepair)
        {
            Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            bool onCapturable = bldg != null && bldg.IsCapturable
                && !(bldg.TeamId == aiTeam && bldg.CurrentCapturePoints >= bldg.CapturePointsMax);
            return onCapturable ? 1 : 4;
        }
        bool hasObjective = plan != null && ResolveAssignedObjective(unit, plan) != null;
        return hasObjective ? 2 : 3;
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

    private IEnumerator ExecuteAIBatchWithDebugStep(PlayerAction action)
    {
        if (action == null)
            yield break;

        if (isDebugPaused)
        {
            debugStepPendingAction = action;
            debugStepRequest = DebugStepRequest.None;
            ShowDebugStepPreview(action);

            yield return new WaitUntil(() => !isDebugPaused || debugStepRequest == DebugStepRequest.Execute);
            debugStepRequest = DebugStepRequest.None;
            ClearDebugStepPreview();
        }

        if (replayManager == null)
            yield break;

        replayManager.ExecuteLiveAIBatch(action);
        yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);
        debugStepPendingAction = null;
    }

    private void ShowDebugStepPreview(PlayerAction action)
    {
        string description = BuildDebugStepDescription(action);
        string previewMessage = string.Empty;
        bool previewShown = turnStateManager != null &&
            turnStateManager.TryShowAIDebugStepPreview(action, out previewMessage);

        Debug.Log($"[AI Step] {description}");
        if (!string.IsNullOrWhiteSpace(previewMessage))
            Debug.Log($"[AI Step] {previewMessage}");

        PanelDialogController.TrySetExternalText(
            $"AI Step\n{description}\nF11: executar | F9: resume");

        if (!previewShown)
            Debug.Log("[AI Step] Preview visual indisponivel para este batch.");
    }

    private void ClearDebugStepPreview()
    {
        debugStepPendingAction = null;
        turnStateManager?.ClearAIDebugStepPreview();
        PanelDialogController.ClearExternalText();
    }

    private string BuildDebugStepDescription(PlayerAction action)
    {
        if (action == null)
            return "batch vazio.";

        string unitLabel = ResolveUnitLabel(action.UnitInstanceId);
        string from = action.HasMoveFrom ? FormatCell(action.MoveFrom) : "origem indefinida";
        string to = action.HasMoveTo ? FormatCell(action.MoveTo) : "destino indefinido";

        switch (action.ActionType)
        {
            case PlayerActionType.CommandService:
                return "servico do comando automatico sera executado.";
            case PlayerActionType.Shopping:
                return $"compra {action.ShoppingUnitTypeId} no hex {FormatCell(action.TargetHex)}.";
            case PlayerActionType.EndTurn:
                return "a IA vai passar a vez.";
            case PlayerActionType.UnitAction:
                switch (action.SensorAction)
                {
                    case SensorActionType.Attack:
                        return $"{unitLabel} vai de {from} ate {to} e vai atacar {action.TargetInstanceId} no hex {FormatCell(action.TargetHex)}.";
                    case SensorActionType.Capture:
                        return $"{unitLabel} vai de {from} ate {to} e vai capturar.";
                    case SensorActionType.Merge:
                        return $"{unitLabel} vai de {from} ate {to} e vai fundir.";
                    case SensorActionType.Supply:
                        return $"{unitLabel} vai de {from} ate {to} e vai suprir.";
                    default:
                        return $"{unitLabel} vai de {from} ate {to}.";
                }
            default:
                return !string.IsNullOrWhiteSpace(action.DebugLabel) ? action.DebugLabel : action.ActionType.ToString();
        }
    }

    private static string FormatCell(Vector3Int cell)
    {
        return $"({cell.x},{cell.y})";
    }

    private static string ResolveUnitLabel(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return "a unidade";

        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null)
                continue;
            if (unit.InstanceId.ToString() == instanceId)
                return $"{unit.UnitDisplayName} #{unit.InstanceId}";
        }

        return $"unidade #{instanceId}";
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
