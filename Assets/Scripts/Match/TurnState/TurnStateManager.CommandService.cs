using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// observacao: esta classe partial contem apenas a logica relacionada ao Servico do Comando, para manter o foco e facilitar a navegacao.
public partial class TurnStateManager
{
    // classes auxiliares para o Servico do Comando.
    private sealed class CommandServiceTargetReport
    {
        public UnitManager target;
        public readonly List<string> serviceLines = new List<string>();
        public int hpRecovered;
        public int fuelRecovered;
        public int ammoRecovered;
    }

    // Linha de plano do Servico do Comando: uma decisao ja comprometida (servico + ganhos + custo)
    // produzida UMA vez pelo planner. O preview desenha o plano e a execucao reproduz exatamente
    // estas linhas, garantindo que "o que foi prometido e o que e executado".
    private sealed class CommandServicePlanLine
    {
        public ServiceData service;
        public int hpGain;
        public int fuelGain;
        public int ammoGain;
        public List<int> ammoByWeapon;
        public int cost;
    }

    // classe auxiliar para compilar informacoes de estimativa do Servico do Comando durante a fase de confirmacao, para alimentar a UI de resumo e preview.
    private sealed class CommandServiceEstimateSummary
    {
        public int servedTargets;
        public int recoveredHp;
        public int recoveredFuel;
        public int recoveredAmmo;
        public int totalCost;
        public int moneyBefore;
        public int moneyAfter;
        public bool hasSkippedServices;
        public readonly List<HelperCommandServiceTargetLine> targetLines = new List<HelperCommandServiceTargetLine>();
        public readonly List<UnitManager> targetLineUnits = new List<UnitManager>();
        public readonly List<HelperCommandServiceSkippedUnitLine> skippedUnitLines = new List<HelperCommandServiceSkippedUnitLine>();
        public readonly List<UnitManager> skippedLineUnits = new List<UnitManager>();
        public readonly List<UnitManager> skippedUnits = new List<UnitManager>();
        public readonly List<CommandServicePreviewEntry> previewEntries = new List<CommandServicePreviewEntry>();
    }

    // classe auxiliar para alimentar a visualizacao de preview das ordens do Servico do Comando durante a fase de confirmacao, associando unidades, celulas e linhas de relatorio.
    private sealed class CommandServicePreviewEntry
    {
        public UnitManager targetUnit;
        public Vector3Int cell;
        public bool willBeServed;
        public int targetLineIndex = -1;
        public int skippedLineIndex = -1;
    }

    private readonly List<ServicoDoComandoOption> commandServiceQueuedOrders = new List<ServicoDoComandoOption>();
    private readonly List<ServicoDoComandoInvalidOption> commandServiceInvalidOrders = new List<ServicoDoComandoInvalidOption>();
    private readonly List<CommandServicePreviewEntry> commandServicePreviewEntries = new List<CommandServicePreviewEntry>();
    private readonly HashSet<int> commandServiceServedUnitInstanceIds = new HashSet<int>();
    private readonly HashSet<UnitManager> commandServicePreviewDimmedUnits = new HashSet<UnitManager>();
    private int commandServiceServedCacheTurn = int.MinValue;
    private int commandServiceServedCacheTeamId = int.MinValue;
    private int commandServicePreviewSelectedIndex = -1;
    private Coroutine commandServiceExecutionRoutine;
    private Coroutine autoCommandServiceRoutine;
    public bool IsPlayerCursorLockedByCommandService => IsCommandServiceExecutingState || IsCommandServiceExecutionRunning;

    /// <summary>
    /// Verdadeiro enquanto o Serviço do Comando automático (início de turno) ainda
    /// está aguardando janela ou executando. A IA espera este flag zerar antes de
    /// iniciar seus batches, para não conflitar com o cursor em CursorState.CommandService.
    /// </summary>
    public bool IsAutoCommandServiceBusy => autoCommandServiceRoutine != null || IsCommandServiceExecutionRunning;

    private bool IsCommandServiceState => CurrentCursorState == CursorState.CommandService;
    private bool IsCommandServiceExecutingState => CurrentCursorState == CursorState.CommandServiceExecuting;
    private bool IsCommandServiceExecutionRunning => commandServiceExecutionRoutine != null;
    private bool IsCommandServiceAwaitingConfirmation =>
        IsCommandServiceState &&
        !IsCommandServiceExecutionRunning &&
        commandServiceQueuedOrders.Count > 0;

    private static bool ShouldLogCommandServiceRuntime => SensorLogGate.IsServicoDoComandoEnabled();

    private static void CommandServiceLog(string message)
    {
        if (!ShouldLogCommandServiceRuntime || string.IsNullOrWhiteSpace(message))
            return;

        Debug.Log(message);
    }

    private void EnterCommandServiceState(string reason)
    {
        if (CurrentCursorState != CursorState.CommandService)
            Advance(CursorState.CommandService, reason);
    }

    private void EnterCommandServiceExecutingState(string reason)
    {
        if (CurrentCursorState == CursorState.CommandServiceExecuting)
            return;

        if (CurrentCursorState != CursorState.CommandService)
            EnterCommandServiceState($"{reason}: ensure Command");

        Advance(CursorState.CommandServiceExecuting, reason);
    }

    // logica de input para acionar o Servico do Comando via hotkey.
    private void ProcessCommandServiceHotkeyInput()
    {
        if (replayManager != null && replayManager.IsReplaying)
            return;

        if (UiInputBlocker.IsTextInputFocused())
            return;

        if (!WasLetterPressedThisFrame('X'))
            return;

        if (TutorialManager.IsCommandServiceBlockedByTutorial)
        {
            TutorialManager.ShowBlockedActionScold(TutorialScoldKind.CommandService);
            cursorController?.PlayErrorSfx();
            return;
        }

        if (CurrentCursorState == CursorState.CommandService)
        {
            if (!IsCommandServiceExecutionRunning)
                TryConfirmPendingCommandServiceOrder();
            return;
        }

        if (CurrentCursorState != CursorState.Neutral)
            return;

        TryCloseThreatLayerHotzone();
        if (TryPreviewCommandServiceOrder(out _, emitLogs: true))
            EnterCommandServiceState("ProcessCommandServiceHotkeyInput");
    }

    // logica para acionar o Servico do Comando a partir do menu de unidade no painel lateral, com mensagens de feedback mais especificas para o contexto de uso via menu.
    public bool TryOpenCommandServiceFromMenu(out string message)
    {
        message = string.Empty;

        if (TutorialManager.IsCommandServiceBlockedByTutorial)
        {
            TutorialManager.ShowBlockedActionScold(TutorialScoldKind.CommandService);
            return false;
        }

        if (replayManager != null && replayManager.IsReplaying)
        {
            message = "Servico do Comando indisponivel durante replay.";
            return false;
        }

        if (CurrentCursorState != CursorState.Neutral && CurrentCursorState != CursorState.PlayerMenu)
        {
            message = $"Servico do Comando exige cursor em Neutral/PlayerMenu (atual: {CurrentCursorState}).";
            return false;
        }

        TryCloseThreatLayerHotzone();
        if (!TryPreviewCommandServiceOrder(out message, emitLogs: true))
            return false;

        EnterCommandServiceState("TryOpenCommandServiceFromMenu");
        return true;
    }

    // logica para confirmar a ordem do Servico do Comando apos a fase de preview/confirmacao, com mensagens de feedback mais especificas para o contexto de uso via menu.
    public bool TryStartCommandServiceOrder(out string message)
    {
        return TryStartCommandServiceOrder(out message, emitLogs: true);
    }

    // logica para confirmar a ordem do Servico do Comando apos a fase de preview/confirmacao, sem emitir mensagens de feedback redundantes, para o caso de uso via hotkey, onde as mensagens ja sao emitidas durante a fase de preview.
    private bool TryStartCommandServiceOrder(out string message, bool emitLogs)
    {
        Debug.Log("[CS] TryStartCommandServiceOrder chamado");
        if (!TryPrepareCommandServiceOrders(out message, emitLogs))
        {
            Debug.Log($"[CS] TryStartCommandServiceOrder: TryPrepareCommandServiceOrders falhou: {message}");
            return false;
        }

        ClearPendingCommandServiceConfirmation();
        message = $"Servico do Comando (\"X\"): iniciando ordem com {commandServiceQueuedOrders.Count} unidade(s).";
        if (emitLogs)
            CommandServiceLog(message);
        PushPanelUnitMessage(
            PanelDialogController.ResolveDialogMessage(
                "command_service.executing",
                "Servico do comando: executando"),
            2.2f);
        JogadasManager.EnsureInstance()?.RegistrarServicoComando(
            matchController != null ? matchController.CurrentTurn : 0,
            matchController != null ? (int)matchController.ActiveTeam : (int)TeamId.Neutral);
        EnterCommandServiceState("TryStartCommandServiceOrder");
        EnterCommandServiceExecutingState("TryStartCommandServiceOrder: executing");
        commandServiceExecutionRoutine = StartCoroutine(ExecuteCommandServiceOrderSequence());
        return true;
    }

    // Foco de teclado no preview do Servico do Comando: 0 = EXECUTAR, 1 = CANCELAR.
    private const int CommandServicePreviewExecuteIndex = 0;
    private const int CommandServicePreviewCancelIndex = 1;
    private int commandServicePreviewFocusIndex = CommandServicePreviewExecuteIndex;
    // 0..N-1 = unidades; N = EXECUTAR; N+1 = CANCELAR.
    private int commandServicePreviewNavigationIndex;

    public int CommandServicePreviewFocusIndex => commandServicePreviewFocusIndex;

    // Navega o foco entre os dois botoes do preview (cima/baixo), com wrap: de EXECUTAR pra cima vai
    // pro CANCELAR e vice-versa (mesma flexibilidade da lista de compra). So vale no estado CommandService.
    public bool NavigateCommandServicePreviewFocus(int delta)
    {
        if (CurrentCursorState != CursorState.CommandService || delta == 0)
            return false;

        int unitCount = commandServicePreviewEntries.Count;
        int total = unitCount + 2;
        commandServicePreviewNavigationIndex =
            (commandServicePreviewNavigationIndex + (delta > 0 ? 1 : -1) + total) % total;
        if (commandServicePreviewNavigationIndex < unitCount)
        {
            commandServicePreviewSelectedIndex = commandServicePreviewNavigationIndex;
            commandServicePreviewFocusIndex = -1;
            RefreshCommandServiceHelperFocus();
            PanCommandServicePreviewCamera(commandServicePreviewEntries[commandServicePreviewSelectedIndex].cell);
        }
        else
        {
            commandServicePreviewFocusIndex = commandServicePreviewNavigationIndex == unitCount
                ? CommandServicePreviewExecuteIndex
                : CommandServicePreviewCancelIndex;
            RefreshCommandServiceHelperFocus(clearSelection: true);
        }
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    // Fixa o foco (usado pelo clique: clicar num botao passa o foco pra ele antes de agir, pra o
    // Enter subsequente nao herdar um foco antigo e disparar a acao errada).
    public void SetCommandServicePreviewFocus(int index)
    {
        if (CurrentCursorState != CursorState.CommandService)
            return;
        commandServicePreviewFocusIndex = Mathf.Clamp(
            index, CommandServicePreviewExecuteIndex, CommandServicePreviewCancelIndex);
        commandServicePreviewNavigationIndex = commandServicePreviewEntries.Count + commandServicePreviewFocusIndex;
        RefreshCommandServiceHelperFocus(clearSelection: true);
    }

    // logica para cancelar a ordem do Servico do Comando durante a fase de preview/confirmacao, com mensagens de feedback mais especificas para o contexto de uso via menu.
    private bool TryPreviewCommandServiceOrder(out string message, bool emitLogs)
    {
        if (!TryPrepareCommandServiceOrders(out message, emitLogs))
            return false;

        // Todo preview comeca com o foco em EXECUTAR (comportamento antigo do Enter preservado).
        commandServicePreviewFocusIndex = CommandServicePreviewExecuteIndex;

        CommandServiceEstimateSummary estimate = EstimateCommandServiceQueuedOrders();
        ShowCommandServiceHelperEstimate(
            estimate.servedTargets,
            estimate.recoveredHp,
            estimate.recoveredFuel,
            estimate.recoveredAmmo,
            estimate.totalCost,
            estimate.hasSkippedServices,
            estimate.moneyBefore,
            estimate.moneyAfter,
            estimate.targetLines,
            estimate.skippedUnitLines);
        ApplyCommandServicePreviewNavigation(estimate.previewEntries);
        ApplyCommandServicePreviewDimmedUnits(estimate.skippedUnits);
        PanelDialogController.TrySetExternalText(
            PanelDialogController.ResolveDialogMessage(
                "command_service.confirm",
                "Servico do Comando :: Confirmar"));
        message = $"Servico do Comando (\"X\"): confirmacao pendente para {estimate.servedTargets} alvo(s) | custo previsto=${Mathf.Max(0, estimate.totalCost)}.";
        if (emitLogs)
            CommandServiceLog(message);
        cursorController?.PlayConfirmSfx();
        return true;
    }

    // logica para preparar a execucao do Servico do Comando, validando as condicoes de disparo e coletando as ordens a serem executadas, com mensagens de feedback mais especificas para o contexto de uso via menu.
    private bool TryPrepareCommandServiceOrders(out string message, bool emitLogs)
    {
        message = string.Empty;
        if (IsCommandServiceExecutionRunning ||
            IsMovementAnimationRunning() ||
            embarkExecutionInProgress ||
            landingExecutionInProgress ||
            combatExecutionInProgress ||
            captureExecutionInProgress ||
            mergeExecutionInProgress ||
            supplyExecutionInProgress ||
            disembarkExecutionInProgress)
        {
            message = "Servico do Comando indisponivel durante outra execucao.";
            if (emitLogs)
                CommandServiceLog(message);
            ClearPendingCommandServiceConfirmation();
            PushPanelUnitMessage(PanelDialogController.ResolveDialogMessage(
                "command_service.unavailable",
                "Servico do comando: indisponivel"));
            return false;
        }

        if (CurrentCursorState != CursorState.Neutral &&
            CurrentCursorState != CursorState.PlayerMenu &&
            CurrentCursorState != CursorState.CommandService)
        {
            message = $"Servico do Comando (\"X\") exige cursor em Neutral/PlayerMenu/CommandService (atual: {CurrentCursorState}).";
            if (emitLogs)
                CommandServiceLog(message);
            ClearPendingCommandServiceConfirmation();
            PushPanelUnitMessage(PanelDialogController.ResolveDialogMessage(
                "command_service.neutral_only",
                "Servico do comando: use no Neutral"));
            return false;
        }

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : (cursorController != null ? cursorController.BoardTilemap : null);
        int activeTeamId = matchController != null ? matchController.ActiveTeamId : -1;
        if (activeTeamId < 0)
        {
            message = "Servico do Comando (\"X\"): sem time ativo valido.";
            if (emitLogs)
                CommandServiceLog(message);
            ClearPendingCommandServiceConfirmation();
            PushPanelUnitMessage(PanelDialogController.ResolveDialogMessage(
                "command_service.invalid_team",
                "Servico do comando: time invalido"));
            return false;
        }

        RefreshCommandServiceServedCacheScope();

        bool canRun = ServicoDoComandoSensor.CollectOptions(
            (TeamId)activeTeamId,
            boardMap,
            terrainDatabase,
            commandServiceQueuedOrders,
            out string reason,
            commandServiceInvalidOrders);

        int collectedOrderCount = commandServiceQueuedOrders.Count;
        int filteredAlreadyServedCount = 0;
        List<string> filteredAlreadyServedNames = null;
        for (int i = commandServiceQueuedOrders.Count - 1; i >= 0; i--)
        {
            ServicoDoComandoOption order = commandServiceQueuedOrders[i];
            UnitManager target = order != null ? order.targetUnit : null;
            if (target == null)
            {
                commandServiceQueuedOrders.RemoveAt(i);
                continue;
            }

            if (target.ReceivedSuppliesThisTurn || WasUnitServedByCommandThisTurn(target))
            {
                filteredAlreadyServedCount++;
                filteredAlreadyServedNames ??= new List<string>();
                if (filteredAlreadyServedNames.Count < 4)
                    filteredAlreadyServedNames.Add(ResolveUnitRuntimeName(target));
                commandServiceQueuedOrders.RemoveAt(i);
            }
        }

        if (commandServiceQueuedOrders.Count <= 0 &&
            (canRun || string.IsNullOrWhiteSpace(reason)))
        {
            if (filteredAlreadyServedCount > 0 && filteredAlreadyServedCount >= collectedOrderCount)
            {
                string examples = filteredAlreadyServedNames != null && filteredAlreadyServedNames.Count > 0
                    ? $" Ex.: {string.Join(", ", filteredAlreadyServedNames)}."
                    : string.Empty;
                reason = $"Todas as unidades elegiveis ja receberam servico nesta rodada ({filteredAlreadyServedCount}).{examples}";
            }
            else
            {
                reason = "Todas as unidades elegiveis foram filtradas antes da execucao.";
            }
        }

        if (!canRun || commandServiceQueuedOrders.Count <= 0)
        {
            string suffix = commandServiceInvalidOrders.Count > 0 ? $" | invalidos={commandServiceInvalidOrders.Count}" : string.Empty;
            message = $"Servico do Comando (\"X\"): {reason}{suffix}";
            if (emitLogs)
                CommandServiceLog(message);
            ClearPendingCommandServiceConfirmation();
            PushPanelUnitMessage(
                PanelDialogController.ResolveDialogMessage(
                    "command_service.no_candidates",
                    "Servico do comando: sem candidatos"),
                2.6f);
            cursorController?.PlayLoadSfx();
            return false;
        }

        NormalizeCommandServiceQueueForEmbarkedFamilies(commandServiceQueuedOrders);
        return true;
    }

    private IEnumerator ExecuteCommandServiceOrderSequence()
    {
        RefreshCommandServiceServedCacheScope();
        ClearCommandServicePreviewDimmedUnits();
        PanelDialogController.ClearExternalText();
        ClearCommandServiceHelper();
        RestoreSupplyEmbarkedSelectionVisuals();
        HashSet<UnitManager> hiddenEmbarkedSuppliers = new HashSet<UnitManager>();
        HashSet<UnitManager> touchedSupplierUnits = new HashSet<UnitManager>();
        HashSet<UnitManager> touchedTargetUnits = new HashSet<UnitManager>();

        try
        {
            NormalizeCommandServiceQueueForEmbarkedFamilies(commandServiceQueuedOrders);
            CommandServiceLog($"[ServicoComando][Fila] Inicio da execucao: {commandServiceQueuedOrders.Count} ordem(ns).");
            for (int q = 0; q < commandServiceQueuedOrders.Count; q++)
            {
                ServicoDoComandoOption queued = commandServiceQueuedOrders[q];
                UnitManager queuedTarget = queued != null ? queued.targetUnit : null;
                string targetName = queuedTarget != null ? queuedTarget.name : "(null)";
                string sourceName = queued != null && queued.sourceConstruction != null
                    ? $"construcao={queued.sourceConstruction.name}"
                    : (queued != null && queued.sourceSupplierUnit != null ? $"fornecedor={queued.sourceSupplierUnit.name}" : "fonte=(null)");
                CommandServiceLog($"[ServicoComando][Fila] {q + 1}/{commandServiceQueuedOrders.Count} alvo={targetName} | {sourceName}");
            }

        // Planeja UMA vez, a partir do estado real atual (mesmo metodo do preview). A execucao apenas
        // reproduz este plano linha a linha: cobra o custo ja calculado e aplica o servico. Sem
        // recalcular ganho/saldo aqui, o que foi prometido no preview e exatamente o que roda.
        var planByOrder = new Dictionary<ServicoDoComandoOption, List<CommandServicePlanLine>>();
        CommandServiceEstimateSummary plan = EstimateCommandServiceQueuedOrders(planByOrder);

        int servedTargets = 0;
        int recoveredHp = 0;
        int recoveredFuel = 0;
        int recoveredAmmo = 0;
        int totalMoneySpent = 0;
        List<CommandServiceTargetReport> detailedReport = new List<CommandServiceTargetReport>();
        // Unica situacao de "saldo insuficiente" na execucao: o plano nao conseguiu servir NENHUM alvo
        // e o saldo foi o motivo. Nao ha mais aborto de fila no meio — o plano ja decide tudo antes.
        bool stopByEconomy = plan != null && plan.servedTargets <= 0 && plan.hasSkippedServices;

        for (int i = 0; i < commandServiceQueuedOrders.Count; i++)
        {
            if (stopByEconomy)
                break;

            ServicoDoComandoOption order = commandServiceQueuedOrders[i];
            if (order == null || order.targetUnit == null)
                continue;
            if (order.targetUnit.ReceivedSuppliesThisTurn || WasUnitServedByCommandThisTurn(order.targetUnit))
                continue;

            UnitManager target = order.targetUnit;

            // Alvo fora do plano (sem necessidade real ou nao coberto pelo saldo) nem entra na
            // animacao: nao foca o cursor, nao mexe em HUD. So processa quem o plano decidiu servir.
            if (!planByOrder.TryGetValue(order, out List<CommandServicePlanLine> plannedLines) || plannedLines.Count == 0)
                continue;

            ConstructionManager sourceConstruction = order.sourceConstruction;
            UnitManager sourceSupplierUnit = order.sourceSupplierUnit;
            bool fromConstruction = sourceConstruction != null;
            bool fromSupplierUnit = sourceSupplierUnit != null;
            if (!fromConstruction && !fromSupplierUnit)
                continue;
            if (fromSupplierUnit)
                touchedSupplierUnits.Add(sourceSupplierUnit);

            string sourceLabel = fromConstruction
                ? $"construcao={sourceConstruction.name}"
                : $"fornecedor={sourceSupplierUnit.name}";
            CommandServiceLog($"[ServicoComando][Fila] {i + 1}/{commandServiceQueuedOrders.Count} alvo={target.name} | {sourceLabel}");

            if (fromConstruction && (int)target.TeamId != (int)sourceConstruction.TeamId)
                continue;
            if (fromSupplierUnit && (int)target.TeamId != (int)sourceSupplierUnit.TeamId)
                continue;
            bool isEmbarkedPassenger = IsEmbarkedPassengerOfSupplier(target, sourceSupplierUnit);
            if (fromSupplierUnit && !isEmbarkedPassenger && target != sourceSupplierUnit)
                continue;
            if ((!target.gameObject.activeInHierarchy && !isEmbarkedPassenger) || (target.IsEmbarked && !isEmbarkedPassenger))
                continue;

            if (!isEmbarkedPassenger && hiddenEmbarkedSuppliers.Count > 0)
                RestoreTransporterHudVisibility(hiddenEmbarkedSuppliers);

            Tilemap boardMap = terrainTilemap != null ? terrainTilemap : (target != null ? target.BoardTilemap : null);

            Vector3Int targetCell;
            if (isEmbarkedPassenger)
            {
                CommandServiceLog($"detectado, embarcado em {sourceSupplierUnit.name}");
                if (!TryPrepareEmbarkedSupplyTarget(target, sourceSupplierUnit, hiddenEmbarkedSuppliers, out targetCell))
                    continue;
                CommandServiceLog($"ocultando HUD do {sourceSupplierUnit.name}");
            }
            else if (!TryPrepareIndividualSupplyTarget(target, target.TeamId, out targetCell))
            {
                continue;
            }

            try
            {
                if (cursorController != null)
                    cursorController.SetCell(targetCell, playMoveSfx: true);
                float cursorFocusDelay = GetSupplyCursorFocusDelay();
                if (cursorFocusDelay > 0f)
                    yield return new WaitForSeconds(cursorFocusDelay);

                if (!isEmbarkedPassenger && (order.forceLandBeforeSupply || order.forceTakeoffBeforeSupply))
                {
                    if (!CanUseLayerModeAtCurrentCell(target, boardMap, terrainDatabase, targetCell, order.plannedServiceDomain, order.plannedServiceHeight, out string plannedLayerReason))
                    {
                        CommandServiceLog($"[ServicoComando] {target.name} ignorado: camada planejada {order.plannedServiceDomain}/{order.plannedServiceHeight} invalida ({plannedLayerReason}).");
                        continue;
                    }

                    yield return ApplySupplyLayerTransitionIfNeeded(target, order.plannedServiceDomain, order.plannedServiceHeight);
                }

                if (!isEmbarkedPassenger && order.forceSurfaceBeforeSupply)
                {
                    PodeEmergirReport emergirReport = PodeEmergirSensor.Evaluate(target, boardMap, terrainDatabase);
                    if (!emergirReport.status)
                    {
                        CommandServiceLog($"[ServicoComando] {target.name} ignorado: nao pode emergir para Naval/Surface ({emergirReport.explicacao}).");
                        continue;
                    }

                    yield return ApplySupplyLayerTransitionIfNeeded(target, Domain.Naval, HeightLevel.Surface);
                }

                int hpGain = 0;
                int fuelGain = 0;
                int ammoGain = 0;
                CommandServiceTargetReport targetReport = new CommandServiceTargetReport
                {
                    target = target
                };

                for (int s = 0; s < plannedLines.Count; s++)
                {
                    CommandServicePlanLine plannedLine = plannedLines[s];
                    ServiceData service = plannedLine != null ? plannedLine.service : null;
                    if (service == null || !service.isService)
                        continue;

                    // Cobra o custo JA calculado pelo plano. O planner reservou o saldo em ordem, entao
                    // este pagamento e garantido; nao ha recalculo de custo/ganho nem aborto de fila aqui.
                    int economyBefore = matchController != null ? matchController.GetActualMoney(target.TeamId) : 0;
                    int serviceMoneySpent = 0;
                    if (matchController != null && plannedLine.cost > 0)
                    {
                        if (!matchController.TrySpendActualMoney(target.TeamId, plannedLine.cost, out int remainingAfterCharge))
                        {
                            // Nao deveria ocorrer (o plano garante que cabe). Se ocorrer, pula a linha
                            // em vez de abortar a fila, preservando o resto do plano prometido.
                            CommandServiceLog($"[ServicoComando] Linha planejada ignorada por saldo inesperado: {ResolveServiceLabel(service)} (custo=${plannedLine.cost}).");
                            continue;
                        }
                        serviceMoneySpent = plannedLine.cost;
                        PanelMoneyController.PushContextualUpdate(target.TeamId, remainingAfterCharge, ResolveServiceUpdateLabel(service), -plannedLine.cost);
                    }
                    totalMoneySpent += Mathf.Max(0, serviceMoneySpent);
                    int economyAfter = matchController != null ? matchController.GetActualMoney(target.TeamId) : economyBefore;

                    Vector3 sourceWorld = fromConstruction
                        ? sourceConstruction.transform.position
                        : sourceSupplierUnit.transform.position;
                    float flightDuration = animationManager != null
                        ? animationManager.PlayServiceProjectileStraight(
                            sourceWorld,
                            target.transform.position,
                            service.spriteDefault,
                            matchController != null && matchController.ShouldPromoteActiveAiActionFxAboveFog())
                        : 0f;
                    float spawnInterval = GetSupplySpawnInterval();
                    if (spawnInterval > 0f)
                        yield return new WaitForSeconds(spawnInterval);
                    if (flightDuration > 0f)
                        yield return new WaitForSeconds(flightDuration + GetSupplyFlightPadding());

                    tempSingleService.Clear();
                    tempSingleService.Add(service);
                    int hpBeforeApply = Mathf.Max(0, target.CurrentHP);
                    int fuelBeforeApply = Mathf.Max(0, target.CurrentFuel);
                    List<int> ammoBeforeApply = SnapshotUnitAmmoByWeapon(target);
                    bool changed = fromConstruction
                        ? ApplyConstructionServicesToTarget(sourceConstruction, target, tempSingleService, out int hpStep, out int fuelStep, out int ammoStep)
                        : ApplyServicesToTarget(sourceSupplierUnit, target, tempSingleService, out hpStep, out fuelStep, out ammoStep, out _);
                    tempSingleService.Clear();
                    if (!changed)
                        continue;
                    touchedTargetUnits.Add(target);

                    int hpAfterApply = Mathf.Clamp(target.CurrentHP, 0, target.GetMaxHP());
                    int actualHpGain = Mathf.Max(0, hpAfterApply - hpBeforeApply);
                    if (actualHpGain > 0)
                    {
                        int desiredHp = Mathf.Clamp(hpBeforeApply + actualHpGain, 0, target.GetMaxHP());
                        CommandServiceLog($"[ServicoComando][HpAnim] {target.name}: {hpBeforeApply} -> {desiredHp} (+{actualHpGain})");
                        target.SetCurrentHP(hpBeforeApply);
                        yield return AnimateHpRecoverFill(target, hpBeforeApply, desiredHp, ShouldLogCommandServiceRuntime);
                    }

                    int fuelAfterApply = Mathf.Clamp(target.CurrentFuel, 0, target.MaxFuel);
                    int actualFuelGain = Mathf.Max(0, fuelAfterApply - fuelBeforeApply);
                    if (actualFuelGain > 0)
                    {
                        int desiredFuel = Mathf.Clamp(fuelBeforeApply + actualFuelGain, 0, target.MaxFuel);
                        CommandServiceLog($"[ServicoComando][FuelAnim] {target.name}: {fuelBeforeApply} -> {desiredFuel} (+{actualFuelGain})");
                        target.SetCurrentFuel(fuelBeforeApply);
                        yield return AnimateFuelRecoverFill(target, fuelBeforeApply, desiredFuel, ShouldLogCommandServiceRuntime);
                    }
                    else if (fuelStep > 0)
                    {
                        CommandServiceLog($"[ServicoComando][FuelAnim] {target.name}: fuelStep={fuelStep}, mas ganho real=0 (antes={fuelBeforeApply}, depois={fuelAfterApply}, max={target.MaxFuel}).");
                    }
                    else
                    {
                        string fuelReason = service.recuperaAutonomia
                            ? (fuelBeforeApply >= target.MaxFuel
                                ? "alvo com autonomia cheia"
                                : "servico aplicado sem ganho de autonomia")
                            : "servico sem recuperaAutonomia";
                        CommandServiceLog($"[ServicoComando][FuelAnim] {target.name}: sem animacao de fuel ({fuelReason}) | fuel={fuelBeforeApply}/{target.MaxFuel} | service={ResolveServiceLabel(service)}");
                    }

                    List<int> ammoAfterApply = SnapshotUnitAmmoByWeapon(target);
                    if (ammoStep > 0)
                        RefreshUnitHudImmediate(target);

                    RecordSupplyReplayCommand(
                        supplier: sourceSupplierUnit,
                        sourceConstruction: sourceConstruction,
                        receiver: target,
                        payingTeam: target.TeamId,
                        economyBefore: economyBefore,
                        economyAfter: economyAfter,
                        hpBefore: hpBeforeApply,
                        hpAfter: hpAfterApply,
                        fuelBefore: fuelBeforeApply,
                        fuelAfter: fuelAfterApply,
                        ammoBefore: ammoBeforeApply,
                        ammoAfter: ammoAfterApply);

                    hpGain += actualHpGain;
                    fuelGain += actualFuelGain;
                    ammoGain += Mathf.Max(0, ammoStep);

                    int sourceUid = sourceSupplierUnit != null
                        ? sourceSupplierUnit.InstanceId
                        : (sourceConstruction != null ? sourceConstruction.InstanceId : 0);
                    JogadasManager.EnsureInstance()?.RegistrarServicoLogistico(
                        matchController != null ? matchController.CurrentTurn : 0,
                        (int)target.TeamId,
                        target.CurrentCellPosition.x,
                        target.CurrentCellPosition.y,
                        target.TryGetUnitData(out UnitData targetData) && targetData != null ? targetData.apelido : "-",
                        target.InstanceId,
                        sourceUid,
                        "ServicoComando",
                        ResolveServiceStatsId(service),
                        serviceMoneySpent,
                        actualHpGain,
                        actualFuelGain,
                        Mathf.Max(0, ammoStep));

                    string serviceName = ResolveServiceLabel(service);
                    string line = $"{serviceName}: HP +{actualHpGain} | AUT +{actualFuelGain} | MUN +{ammoStep}";
                    targetReport.serviceLines.Add(line);
                }

                if (stopByEconomy)
                    break;

                if (hpGain <= 0 && fuelGain <= 0 && ammoGain <= 0)
                {
                    CommandServiceLog($"[ServicoComando][Fila] {target.name}: sem ganhos aplicados (HP/AUT/MUN).");
                    continue;
                }
                if (fuelGain <= 0)
                    CommandServiceLog($"[ServicoComando][Fila] {target.name}: sem ganho de AUT no alvo (HP +{hpGain} | AUT +{fuelGain} | MUN +{ammoGain}).");

                if (!isEmbarkedPassenger && order.forceLandBeforeSupply)
                    yield return ExecutePostSupplyAircraftTakeoff(target, boardMap, "[ServicoComando]");

                NotifyUnitSupplied(sourceSupplierUnit, target);
                MarkUnitServedByCommandThisTurn(target);

                bool embarkedHiddenAfterService = false;
                if (isEmbarkedPassenger && supplyEmbarkedPreviewStates.TryGetValue(target, out SupplyEmbarkedPreviewState servedStateAfterService))
                {
                    HideEmbarkedPassengerAfterSupply(target, servedStateAfterService.domain, servedStateAfterService.height);
                    supplyEmbarkedPreviewStates.Remove(target);
                    embarkedHiddenAfterService = true;
                }

                targetReport.hpRecovered = hpGain;
                targetReport.fuelRecovered = fuelGain;
                targetReport.ammoRecovered = ammoGain;
                detailedReport.Add(targetReport);

                servedTargets++;
                recoveredHp += hpGain;
                recoveredFuel += fuelGain;
                recoveredAmmo += ammoGain;
                cursorController?.PlayLoadSfx();
                float postTargetDelay = GetSupplyPostTargetDelay();
                if (postTargetDelay > 0f)
                    yield return new WaitForSeconds(postTargetDelay);

                if (embarkedHiddenAfterService)
                    ForceHideEmbarkedPassengersExcept(sourceSupplierUnit, keepVisible: null);
            }
            finally
            {
                if (isEmbarkedPassenger && supplyEmbarkedPreviewStates.TryGetValue(target, out SupplyEmbarkedPreviewState servedState))
                {
                    HideEmbarkedPassengerAfterSupply(target, servedState.domain, servedState.height);
                    supplyEmbarkedPreviewStates.Remove(target);
                }
            }
        }

        if (hiddenEmbarkedSuppliers.Count > 0)
            RestoreTransporterHudVisibility(hiddenEmbarkedSuppliers);

        commandServiceQueuedOrders.Clear();
        RestoreSupplyEmbarkedSelectionVisuals();

        if (servedTargets <= 0)
        {
            CommandServiceLog("[ServicoComando] Nenhum alvo recebeu servico (necessidade/estoque).");
            if (stopByEconomy)
            {
                PushPanelUnitMessage(
                    PanelDialogController.ResolveDialogMessage(
                        "command_service.insufficient_money",
                        "Servico do comando: saldo insuficiente"),
                    2.8f);
            }
            else
            {
                PushPanelUnitMessage(
                    PanelDialogController.ResolveDialogMessage(
                        "command_service.no_candidates",
                        "Servico do comando: sem candidatos"),
                    2.6f);
            }
            cursorController?.PlayLoadSfx();
            ExecuteAndReset("ExecuteCommandServiceOrderSequence: no served targets");
            yield break;
        }

        CommandServiceLog($"[ServicoComando] alvos atendidos={servedTargets} | HP +{recoveredHp} | autonomia +{recoveredFuel} | municao +{recoveredAmmo} | custo ${Mathf.Max(0, totalMoneySpent)}");
        ShowCommandServiceHelperSummary(
            servedTargets,
            recoveredHp,
            recoveredFuel,
            recoveredAmmo,
            totalMoneySpent,
            stopByEconomy,
            durationSeconds: 3.2f);
        if (stopByEconomy)
        {
            PushPanelUnitMessage(
                PanelDialogController.ResolveDialogMessage(
                    "command_service.insufficient_money",
                    "Servico do comando: saldo insuficiente"),
                2.8f);
        }
        else
        {
            PushPanelUnitMessage(
                PanelDialogController.ResolveDialogMessage(
                    "command_service.summary",
                    $"Servico comando: {servedTargets} alvos | ${Mathf.Max(0, totalMoneySpent)}",
                    new Dictionary<string, string>
                    {
                        { "targets", servedTargets.ToString() },
                        { "valor", Mathf.Max(0, totalMoneySpent).ToString() }
                    }),
                3.2f);
        }
            CommandServiceLog(BuildCommandServiceDetailedReportLog(detailedReport));
            cursorController?.PlayLoadSfx();
            ExecuteAndReset("ExecuteCommandServiceOrderSequence: completed");
        }
        finally
        {
            if (hiddenEmbarkedSuppliers.Count > 0)
                RestoreTransporterHudVisibility(hiddenEmbarkedSuppliers);
            RefreshTransporterHudAfterCommandService(touchedSupplierUnits);
            RefreshCommandServiceTargetHuds(touchedTargetUnits);

            commandServiceQueuedOrders.Clear();
            commandServiceInvalidOrders.Clear();
            ClearCommandServicePreviewDimmedUnits();
            RestoreSupplyEmbarkedSelectionVisuals();
            commandServiceExecutionRoutine = null;
            ExecuteAndReset("ExecuteCommandServiceOrderSequence: cleanup");
        }
    }

    private static void RefreshCommandServiceTargetHuds(HashSet<UnitManager> touchedTargetUnits)
    {
        if (touchedTargetUnits == null || touchedTargetUnits.Count <= 0)
            return;

        foreach (UnitManager target in touchedTargetUnits)
        {
            if (target == null)
                continue;
            if (!target.gameObject.activeInHierarchy && !(target.IsEmbarked && target.IsEmbarkedVisualPreviewActive))
                continue;

            RefreshUnitHudImmediate(target);
        }
    }

    private bool TryConfirmPendingCommandServiceOrder()
    {
        if (!IsCommandServiceAwaitingConfirmation)
        {
            Debug.Log($"[CS] TryConfirmPendingCommandServiceOrder: IsCommandServiceAwaitingConfirmation=false (state={CurrentCursorState} executionRunning={IsCommandServiceExecutionRunning} ordersCount={commandServiceQueuedOrders.Count})");
            return false;
        }

        if (commandServiceQueuedOrders.Count <= 0)
        {
            Debug.Log("[CS] TryConfirmPendingCommandServiceOrder: ordersCount=0 — retornando sem registrar");
            ClearPendingCommandServiceConfirmation();
            cursorController?.PlayErrorSfx();
            return true;
        }

        string message = $"Servico do Comando (\"X\"): iniciando ordem com {commandServiceQueuedOrders.Count} unidade(s).";
        CommandServiceLog(message);
        ClearCommandServicePreviewDimmedUnits();
        PanelDialogController.ClearExternalText();
        ClearCommandServiceHelper();
        PushPanelUnitMessage(
            PanelDialogController.ResolveDialogMessage(
                "command_service.executing",
                "Servico do comando: executando"),
            2.2f);
        replayManager?.RecordStandaloneAction(new PlayerAction
        {
            ActionType = PlayerActionType.CommandService,
            TurnNumber = matchController != null ? matchController.CurrentTurn : 0,
            ActingTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral,
            CursorHex = cursorController != null ? cursorController.CurrentCell : Vector3Int.zero,
            SensorAction = SensorActionType.CommandService,
            Confirmed = true,
            DebugLabel = "CommandService: confirm"
        });
        JogadasManager.EnsureInstance()?.RegistrarServicoComando(
            matchController != null ? matchController.CurrentTurn : 0,
            matchController != null ? (int)matchController.ActiveTeam : (int)TeamId.Neutral);
        EnterCommandServiceExecutingState("TryConfirmPendingCommandServiceOrder");
        commandServiceExecutionRoutine = StartCoroutine(ExecuteCommandServiceOrderSequence());
        return true;
    }

    private void ExitCommandServiceStateToNeutral()
    {
        if (!IsCommandServiceState)
            return;
        ClearPendingCommandServiceConfirmation();
        Retreat("ExitCommandServiceStateToNeutral");
    }

    private void ClearPendingCommandServiceConfirmation()
    {
        commandServiceQueuedOrders.Clear();
        commandServiceInvalidOrders.Clear();
        ClearCommandServicePreviewDimmedUnits();
        PanelDialogController.ClearExternalText();
        ClearCommandServiceHelper();
    }

    public void ResetCommandServiceReplayTransientState()
    {
        if (commandServiceExecutionRoutine != null)
        {
            StopCoroutine(commandServiceExecutionRoutine);
            commandServiceExecutionRoutine = null;
        }

        commandServiceServedCacheTurn = int.MinValue;
        commandServiceServedCacheTeamId = int.MinValue;
        commandServiceServedUnitInstanceIds.Clear();
        ClearPendingCommandServiceConfirmation();
        ClearCommandServicePreviewNavigation();
        if (CurrentCursorState == CursorState.CommandService || CurrentCursorState == CursorState.CommandServiceExecuting)
            ExecuteAndReset("ResetCommandServiceReplayTransientState");
    }

    private void RefreshCommandServiceServedCacheScope()
    {
        int currentTurn = matchController != null ? matchController.CurrentTurn : int.MinValue;
        int currentTeam = matchController != null ? matchController.ActiveTeamId : int.MinValue;
        if (commandServiceServedCacheTurn == currentTurn && commandServiceServedCacheTeamId == currentTeam)
            return;

        commandServiceServedCacheTurn = currentTurn;
        commandServiceServedCacheTeamId = currentTeam;
        commandServiceServedUnitInstanceIds.Clear();
    }

    private bool WasUnitServedByCommandThisTurn(UnitManager unit)
    {
        if (unit == null)
            return false;

        int id = unit.InstanceId;
        return id > 0 && commandServiceServedUnitInstanceIds.Contains(id);
    }

    private void MarkUnitServedByCommandThisTurn(UnitManager unit)
    {
        if (unit == null)
            return;

        int id = unit.InstanceId;
        if (id > 0)
            commandServiceServedUnitInstanceIds.Add(id);

        if (!unit.ReceivedSuppliesThisTurn)
            unit.MarkReceivedSuppliesThisTurn();
    }

    // Planner unico do Servico do Comando. Percorre a fila em ordem, deplecionando UM snapshot de
    // estoque por fonte e reservando o saldo sequencialmente (pula servico individualmente inviavel e
    // CONTINUA — nunca aborta a fila). Quando `planByOrder` != null, emite as linhas comprometidas que
    // a execucao vai reproduzir. Preview e execucao chamam este mesmo metodo, entao nao podem divergir.
    private CommandServiceEstimateSummary EstimateCommandServiceQueuedOrders(
        Dictionary<ServicoDoComandoOption, List<CommandServicePlanLine>> planByOrder = null)
    {
        CommandServiceEstimateSummary summary = new CommandServiceEstimateSummary();
        int remainingMoney = matchController != null && matchController.ActiveTeamId >= 0
            ? Mathf.Max(0, matchController.GetActualMoney((TeamId)matchController.ActiveTeamId))
            : 0;
        summary.moneyBefore = remainingMoney;

        Dictionary<ConstructionManager, Dictionary<SupplyData, int>> constructionStockBySource = new Dictionary<ConstructionManager, Dictionary<SupplyData, int>>();
        Dictionary<UnitManager, Dictionary<SupplyData, int>> supplierStockBySource = new Dictionary<UnitManager, Dictionary<SupplyData, int>>();

        for (int i = 0; i < commandServiceQueuedOrders.Count; i++)
        {
            ServicoDoComandoOption order = commandServiceQueuedOrders[i];
            if (order == null || order.targetUnit == null)
                continue;
            if (order.targetUnit.ReceivedSuppliesThisTurn || WasUnitServedByCommandThisTurn(order.targetUnit))
                continue;

            UnitManager target = order.targetUnit;
            IReadOnlyList<ServiceData> offered = order.sourceConstruction != null
                ? order.sourceConstruction.OfferedServices
                : order.sourceSupplierUnit != null ? order.sourceSupplierUnit.GetEmbarkedServices() : null;
            List<ServiceData> services = BuildDistinctServiceList(offered);
            if (services == null || services.Count <= 0)
                continue;

            Dictionary<SupplyData, int> sourceStock = null;
            if (order.sourceConstruction != null)
            {
                if (!constructionStockBySource.TryGetValue(order.sourceConstruction, out sourceStock))
                {
                    sourceStock = BuildConstructionStockSnapshot(order.sourceConstruction);
                    constructionStockBySource.Add(order.sourceConstruction, sourceStock);
                }
            }
            else if (order.sourceSupplierUnit != null)
            {
                if (!supplierStockBySource.TryGetValue(order.sourceSupplierUnit, out sourceStock))
                {
                    sourceStock = BuildSupplierStockSnapshot(order.sourceSupplierUnit);
                    supplierStockBySource.Add(order.sourceSupplierUnit, sourceStock);
                }
            }

            int targetHp = 0;
            int targetFuel = 0;
            int targetAmmo = 0;
            List<int> targetAmmoByWeapon = new List<int>();
            int simulatedHp = Mathf.Clamp(target.CurrentHP, 0, target.GetMaxHP());
            int simulatedFuel = Mathf.Clamp(target.CurrentFuel, 0, target.GetMaxFuel());
            List<int> simulatedAmmoByWeapon = BuildRuntimeAmmoSnapshot(target);
            bool targetServed = false;
            bool targetSkippedByMoney = false;

            for (int s = 0; s < services.Count; s++)
            {
                ServiceData service = services[s];
                if (service == null || !service.isService)
                    continue;
                if (service.apenasEntreSupridores && !IsSupplier(target))
                    continue;
                if (!CanServiceApplyByClassAndNeed(target, service))
                    continue;

                Dictionary<SupplyData, int> candidateStock = CloneSupplySnapshot(sourceStock);
                List<int> candidateSimulatedAmmo = CloneAmmoSnapshot(simulatedAmmoByWeapon);
                List<int> candidateAmmoByWeaponGain = new List<int>();
                EstimatePotentialServiceGains(
                    target,
                    service,
                    candidateStock,
                    out int hpGain,
                    out int fuelGain,
                    out int ammoGain,
                    candidateAmmoByWeaponGain,
                    simulatedHp,
                    simulatedFuel,
                    candidateSimulatedAmmo);
                if (hpGain <= 0 && fuelGain <= 0 && ammoGain <= 0)
                    continue;

                int finalCost = matchController != null
                    ? matchController.ResolveEconomyCost(ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, candidateAmmoByWeaponGain))
                    : Mathf.Max(0, ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, candidateAmmoByWeaponGain));
                if (finalCost > remainingMoney)
                {
                    summary.hasSkippedServices = true;
                    targetSkippedByMoney = true;
                    continue;
                }

                OverwriteSupplySnapshot(sourceStock, candidateStock);
                remainingMoney -= Mathf.Max(0, finalCost);
                summary.totalCost += Mathf.Max(0, finalCost);
                if (planByOrder != null)
                {
                    if (!planByOrder.TryGetValue(order, out List<CommandServicePlanLine> plannedLines))
                    {
                        plannedLines = new List<CommandServicePlanLine>();
                        planByOrder.Add(order, plannedLines);
                    }
                    plannedLines.Add(new CommandServicePlanLine
                    {
                        service = service,
                        hpGain = hpGain,
                        fuelGain = fuelGain,
                        ammoGain = ammoGain,
                        ammoByWeapon = candidateAmmoByWeaponGain != null && candidateAmmoByWeaponGain.Count > 0
                            ? new List<int>(candidateAmmoByWeaponGain)
                            : null,
                        cost = Mathf.Max(0, finalCost)
                    });
                }
                targetHp += hpGain;
                targetFuel += fuelGain;
                targetAmmo += ammoGain;
                MergeAmmoGainSnapshot(targetAmmoByWeapon, candidateAmmoByWeaponGain);
                simulatedHp = Mathf.Clamp(simulatedHp + hpGain, 0, target.GetMaxHP());
                simulatedFuel = Mathf.Clamp(simulatedFuel + fuelGain, 0, target.GetMaxFuel());
                simulatedAmmoByWeapon = candidateSimulatedAmmo;
                targetServed = true;
            }

            if (targetServed)
            {
                summary.servedTargets++;
                summary.recoveredHp += targetHp;
                summary.recoveredFuel += targetFuel;
                summary.recoveredAmmo += targetAmmo;
                summary.targetLines.Add(new HelperCommandServiceTargetLine
                {
                    unitName = ResolveUnitRuntimeName(target),
                    sourceLabel = ResolveCommandServiceSourceLabel(order),
                    gainsLabel = BuildCommandServiceGainsInline(targetHp, targetFuel, targetAmmoByWeapon),
                    unitSprite = target.GetMainSpriteRenderer() != null ? target.GetMainSpriteRenderer().sprite : null,
                    unitColor = TeamUtils.GetColor(target.TeamId),
                    cell = ResolveCommandServicePreviewCell(order),
                    isFullyAffordable = !targetSkippedByMoney
                });
                summary.targetLineUnits.Add(target);
                int targetLineIndex = summary.targetLines.Count - 1;
                summary.previewEntries.Add(new CommandServicePreviewEntry
                {
                    targetUnit = target,
                    cell = ResolveCommandServicePreviewCell(order),
                    willBeServed = true,
                    targetLineIndex = targetLineIndex
                });
            }
            else if (targetSkippedByMoney)
            {
                summary.skippedUnits.Add(target);
                summary.skippedUnitLines.Add(new HelperCommandServiceSkippedUnitLine
                {
                    unitName = ResolveUnitRuntimeName(target),
                    sourceLabel = ResolveCommandServiceSourceLabel(order),
                    unitSprite = target.GetMainSpriteRenderer() != null ? target.GetMainSpriteRenderer().sprite : null,
                    unitColor = TeamUtils.GetColor(target.TeamId),
                    cell = ResolveCommandServicePreviewCell(order)
                });
                summary.skippedLineUnits.Add(target);
                int skippedLineIndex = summary.skippedUnitLines.Count - 1;
                summary.previewEntries.Add(new CommandServicePreviewEntry
                {
                    targetUnit = target,
                    cell = ResolveCommandServicePreviewCell(order),
                    willBeServed = false,
                    skippedLineIndex = skippedLineIndex
                });
            }
        }

        // As linhas e o preview devem refletir exatamente a fila de execucao.
        // A fila ja esta ordenada pelo valor da unidade e mantem familias
        // embarcadas agrupadas; nao reordenar aqui pela posicao no mapa.
        summary.moneyAfter = Mathf.Max(0, remainingMoney);
        return summary;
    }

    private void SortCommandServiceEstimateSummary(CommandServiceEstimateSummary summary)
    {
        if (summary == null)
            return;

        SortCommandServiceTargetLines(summary.targetLines, summary.targetLineUnits);
        SortCommandServiceSkippedLines(summary.skippedUnitLines, summary.skippedLineUnits);

        for (int i = 0; i < summary.previewEntries.Count; i++)
        {
            CommandServicePreviewEntry entry = summary.previewEntries[i];
            if (entry == null || entry.targetUnit == null)
                continue;

            entry.targetLineIndex = entry.willBeServed
                ? FindCommandServiceUnitIndex(summary.targetLineUnits, entry.targetUnit)
                : -1;
            entry.skippedLineIndex = !entry.willBeServed
                ? FindCommandServiceUnitIndex(summary.skippedLineUnits, entry.targetUnit)
                : -1;
        }

        summary.previewEntries.Sort(CompareCommandServicePreviewEntries);
    }

    private void SortCommandServiceTargetLines(List<HelperCommandServiceTargetLine> lines, List<UnitManager> units)
    {
        if (lines == null || units == null || lines.Count != units.Count || lines.Count <= 1)
            return;

        List<KeyValuePair<HelperCommandServiceTargetLine, UnitManager>> zipped = new List<KeyValuePair<HelperCommandServiceTargetLine, UnitManager>>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
            zipped.Add(new KeyValuePair<HelperCommandServiceTargetLine, UnitManager>(lines[i], units[i]));

        zipped.Sort((a, b) => CompareCommandServiceUnitsTopToBottom(a.Value, b.Value));

        lines.Clear();
        units.Clear();
        for (int i = 0; i < zipped.Count; i++)
        {
            lines.Add(zipped[i].Key);
            units.Add(zipped[i].Value);
        }
    }

    private void SortCommandServiceSkippedLines(List<HelperCommandServiceSkippedUnitLine> lines, List<UnitManager> units)
    {
        if (lines == null || units == null || lines.Count != units.Count || lines.Count <= 1)
            return;

        List<KeyValuePair<HelperCommandServiceSkippedUnitLine, UnitManager>> zipped = new List<KeyValuePair<HelperCommandServiceSkippedUnitLine, UnitManager>>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
            zipped.Add(new KeyValuePair<HelperCommandServiceSkippedUnitLine, UnitManager>(lines[i], units[i]));

        zipped.Sort((a, b) => CompareCommandServiceUnitsTopToBottom(a.Value, b.Value));

        lines.Clear();
        units.Clear();
        for (int i = 0; i < zipped.Count; i++)
        {
            lines.Add(zipped[i].Key);
            units.Add(zipped[i].Value);
        }
    }

    private int CompareCommandServicePreviewEntries(CommandServicePreviewEntry a, CommandServicePreviewEntry b)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;
        if (a.willBeServed != b.willBeServed)
            return a.willBeServed ? -1 : 1;
        return CompareCommandServiceUnitsTopToBottom(a.targetUnit, b.targetUnit);
    }

    private static int FindCommandServiceUnitIndex(List<UnitManager> units, UnitManager target)
    {
        if (units == null || target == null)
            return -1;

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == target)
                return i;
        }

        return -1;
    }

    private static int CompareCommandServiceUnitsTopToBottom(UnitManager a, UnitManager b)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        Vector3 aPos = a.transform.position;
        Vector3 bPos = b.transform.position;
        int byY = -aPos.y.CompareTo(bPos.y);
        if (byY != 0)
            return byY;

        int byX = aPos.x.CompareTo(bPos.x);
        if (byX != 0)
            return byX;

        Vector3Int aCell = a.CurrentCellPosition;
        Vector3Int bCell = b.CurrentCellPosition;
        int byCellY = aCell.y.CompareTo(bCell.y);
        if (byCellY != 0)
            return byCellY;

        return aCell.x.CompareTo(bCell.x);
    }

    private void ApplyCommandServicePreviewDimmedUnits(IReadOnlyList<UnitManager> skippedUnits)
    {
        ClearCommandServicePreviewDimmedUnits();
        if (skippedUnits == null || skippedUnits.Count <= 0)
            return;

        for (int i = 0; i < skippedUnits.Count; i++)
        {
            UnitManager unit = skippedUnits[i];
            if (unit == null)
                continue;

            commandServicePreviewDimmedUnits.Add(unit);
            unit.SetPreviewDimmed(true);
        }
    }

    private void ClearCommandServicePreviewDimmedUnits()
    {
        if (commandServicePreviewDimmedUnits.Count <= 0)
            return;

        foreach (UnitManager unit in commandServicePreviewDimmedUnits)
        {
            if (unit != null)
                unit.SetPreviewDimmed(false);
        }

        commandServicePreviewDimmedUnits.Clear();
    }

    private void ApplyCommandServicePreviewNavigation(IReadOnlyList<CommandServicePreviewEntry> previewEntries)
    {
        commandServicePreviewEntries.Clear();
        commandServicePreviewSelectedIndex = -1;

        if (previewEntries == null || previewEntries.Count <= 0)
            return;

        for (int i = 0; i < previewEntries.Count; i++)
        {
            CommandServicePreviewEntry entry = previewEntries[i];
            if (entry == null || entry.targetUnit == null)
                continue;

            commandServicePreviewEntries.Add(new CommandServicePreviewEntry
            {
                targetUnit = entry.targetUnit,
                cell = entry.cell,
                willBeServed = entry.willBeServed,
                targetLineIndex = entry.targetLineIndex,
                skippedLineIndex = entry.skippedLineIndex
            });
        }

        // Executar e a acao principal. As linhas de unidade continuam na mesma
        // navegacao para inspecao opcional, mas nao capturam o foco ao abrir.
        commandServicePreviewSelectedIndex = -1;
        commandServicePreviewNavigationIndex = commandServicePreviewEntries.Count;
        commandServicePreviewFocusIndex = CommandServicePreviewExecuteIndex;
        RefreshCommandServiceHelperFocus(clearSelection: true);
    }

    public bool NavigateCommandServicePreviewUnits(int delta)
    {
        if (!IsCommandServiceAwaitingConfirmation || delta == 0 || commandServicePreviewEntries.Count <= 0)
            return false;
        int current = Mathf.Clamp(commandServicePreviewSelectedIndex, 0, commandServicePreviewEntries.Count - 1);
        commandServicePreviewSelectedIndex = (current + (delta > 0 ? 1 : -1) + commandServicePreviewEntries.Count) % commandServicePreviewEntries.Count;
        RefreshCommandServiceHelperFocus();
        PanCommandServicePreviewCamera(commandServicePreviewEntries[commandServicePreviewSelectedIndex].cell);
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public void FocusCommandServicePreviewUnit(int index)
    {
        if (!IsCommandServiceAwaitingConfirmation || index < 0 || index >= commandServicePreviewEntries.Count)
            return;
        commandServicePreviewSelectedIndex = index;
        commandServicePreviewNavigationIndex = index;
        commandServicePreviewFocusIndex = -1;
        RefreshCommandServiceHelperFocus();
        PanCommandServicePreviewCamera(commandServicePreviewEntries[index].cell);
    }

    public void FocusCommandServicePreviewLine(bool served, int lineIndex)
    {
        for (int i = 0; i < commandServicePreviewEntries.Count; i++)
        {
            CommandServicePreviewEntry entry = commandServicePreviewEntries[i];
            if (entry != null && (served ? entry.targetLineIndex : entry.skippedLineIndex) == lineIndex)
            {
                FocusCommandServicePreviewUnit(i);
                return;
            }
        }
    }

    private void PanCommandServicePreviewCamera(Vector3Int cell)
    {
        PanelHelperController.TryPanToAutonomyCell(cell);
    }

    private void ClearCommandServicePreviewNavigation()
    {
        commandServicePreviewEntries.Clear();
        commandServicePreviewSelectedIndex = -1;
        RefreshCommandServiceHelperFocus();
    }

    private bool TryResolveCommandServicePreviewCursorMove(Vector3Int currentCell, Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = currentCell;
        if (!IsCommandServiceAwaitingConfirmation || commandServicePreviewEntries.Count <= 0)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        SyncCommandServicePreviewSelectionFromCursor();

        int currentIndex = commandServicePreviewSelectedIndex;
        if (currentIndex < 0 || currentIndex >= commandServicePreviewEntries.Count)
            currentIndex = 0;

        int nextIndex = (currentIndex + step + commandServicePreviewEntries.Count) % commandServicePreviewEntries.Count;
        CommandServicePreviewEntry next = commandServicePreviewEntries[nextIndex];
        if (next == null)
            return false;

        commandServicePreviewSelectedIndex = nextIndex;
        RefreshCommandServiceHelperFocus();
        resolvedCell = next.cell;
        resolvedCell.z = 0;
        return true;
    }

    private void SyncCommandServicePreviewSelectionFromCursor()
    {
        if (cursorController == null || commandServicePreviewEntries.Count <= 0)
            return;

        Vector3Int cell = cursorController.CurrentCell;
        cell.z = 0;
        for (int i = 0; i < commandServicePreviewEntries.Count; i++)
        {
            CommandServicePreviewEntry entry = commandServicePreviewEntries[i];
            if (entry == null)
                continue;
            if (entry.cell == cell)
            {
                commandServicePreviewSelectedIndex = i;
                RefreshCommandServiceHelperFocus();
                return;
            }
        }
    }

    private void RefreshCommandServiceHelperFocus(bool clearSelection = false)
    {
        for (int i = 0; i < commandServiceHelperTargetLines.Count; i++)
        {
            HelperCommandServiceTargetLine line = commandServiceHelperTargetLines[i];
            if (line != null)
                line.isFocused = false;
        }

        for (int i = 0; i < commandServiceHelperSkippedUnitLines.Count; i++)
        {
            HelperCommandServiceSkippedUnitLine line = commandServiceHelperSkippedUnitLines[i];
            if (line != null)
                line.isFocused = false;
        }

        if (clearSelection || commandServicePreviewSelectedIndex < 0 || commandServicePreviewSelectedIndex >= commandServicePreviewEntries.Count)
            return;

        CommandServicePreviewEntry focused = commandServicePreviewEntries[commandServicePreviewSelectedIndex];
        if (focused == null)
            return;

        if (focused.targetLineIndex >= 0 && focused.targetLineIndex < commandServiceHelperTargetLines.Count)
        {
            HelperCommandServiceTargetLine line = commandServiceHelperTargetLines[focused.targetLineIndex];
            if (line != null)
                line.isFocused = true;
        }

        if (focused.skippedLineIndex >= 0 && focused.skippedLineIndex < commandServiceHelperSkippedUnitLines.Count)
        {
            HelperCommandServiceSkippedUnitLine line = commandServiceHelperSkippedUnitLines[focused.skippedLineIndex];
            if (line != null)
                line.isFocused = true;
        }
    }

    private static Vector3Int ResolveCommandServicePreviewCell(ServicoDoComandoOption order)
    {
        if (order == null || order.targetUnit == null)
            return Vector3Int.zero;

        UnitManager target = order.targetUnit;
        UnitManager supplier = order.sourceSupplierUnit;
        bool isEmbarkedPassenger = supplier != null && target.IsEmbarked && target.EmbarkedTransporter == supplier;
        Vector3Int cell = isEmbarkedPassenger ? supplier.CurrentCellPosition : target.CurrentCellPosition;
        cell.z = 0;
        return cell;
    }

    private static string BuildCommandServiceGainsInline(int hp, int fuel, List<int> ammoByWeapon)
    {
        List<string> segments = new List<string>();
        if (hp > 0)
            segments.Add($"HP +{hp}");
        if (fuel > 0)
            segments.Add($"FUEL +{fuel}");
        if (ammoByWeapon != null)
        {
            for (int i = 0; i < ammoByWeapon.Count; i++)
            {
                int amount = Mathf.Max(0, ammoByWeapon[i]);
                if (amount <= 0)
                    continue;

                string slotLabel = $"W{i + 1}";
                segments.Add($"{slotLabel}+{amount}");
            }
        }
        return segments.Count > 0 ? string.Join(" | ", segments) : "-";
    }

    private static string ResolveCommandServiceSourceLabel(ServicoDoComandoOption order)
    {
        if (order == null)
            return string.Empty;

        if (order.sourceConstruction != null)
        {
            Vector3Int cell = order.sourceConstruction.CurrentCellPosition;
            string label = !string.IsNullOrWhiteSpace(order.sourceConstruction.ConstructionDisplayName)
                ? order.sourceConstruction.ConstructionDisplayName
                : order.sourceConstruction.name;
            return $"{label} {FormatMapCell(cell)}";
        }

        if (order.sourceSupplierUnit != null)
        {
            Vector3Int cell = order.sourceSupplierUnit.CurrentCellPosition;
            return $"{order.sourceSupplierUnit.name} {FormatMapCell(cell)}";
        }

        return string.Empty;
    }

    private void ForceHideEmbarkedPassengersExcept(UnitManager supplier, UnitManager keepVisible)
    {
        if (supplier == null)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = supplier.TransportedUnitSlots;
        if (seats == null || seats.Count <= 0)
            return;

        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            UnitManager passenger = seat != null ? seat.embarkedUnit : null;
            if (passenger == null || passenger == keepVisible || !passenger.IsEmbarked || passenger.EmbarkedTransporter != supplier)
                continue;

            if (supplyEmbarkedPreviewStates.TryGetValue(passenger, out SupplyEmbarkedPreviewState state))
            {
                HideEmbarkedPassengerAfterSupply(passenger, state.domain, state.height);
                supplyEmbarkedPreviewStates.Remove(passenger);
                continue;
            }

            // Fallback defensivo: garante que passageiros nao selecionados permaneçam ocultos.
            passenger.EndEmbarkedVisualPreview();
            SetUnitSpriteRenderersVisible(passenger, false);

            UnitHudController passengerHud = ResolveOwnUnitHud(passenger);
            if (passengerHud != null)
                passengerHud.gameObject.SetActive(false);

            passenger.ClearTemporarySortingOrder();
        }
    }

    private void SetSupplierHudVisibleForCommandSource(UnitManager supplier, bool visible)
    {
        if (supplier == null)
            return;

        UnitHudController supplierHud = ResolveOwnUnitHud(supplier);
        if (supplierHud == null)
            return;

        supplierHud.gameObject.SetActive(visible);
        if (!visible)
            return;

        // Reaplica o estado do HUD ao restaurar o transportador para garantir
        // que o indicador "T" reflita corretamente passageiros embarcados.
        bool showTransportIndicator = HasEmbarkedPassengersForTransportIndicator(supplier);
        supplierHud.RefreshBindings();
        supplierHud.Apply(
            supplier.CurrentHP,
            supplier.GetMaxHP(),
            supplier.CurrentAmmo,
            supplier.GetMaxAmmo(),
            supplier.CurrentFuel,
            supplier.MaxFuel,
            TeamUtils.GetColor(supplier.TeamId),
            supplier.GetDomain(),
            supplier.GetHeightLevel(),
            showTransportIndicator,
            showDetectedIndicator: ResolveDetectedIndicatorForHud(supplier));
        ReapplyForcedUnitVisualForFog(supplier);
    }

    private void RefreshTransporterHudAfterCommandService(HashSet<UnitManager> touchedSupplierUnits)
    {
        if (touchedSupplierUnits == null || touchedSupplierUnits.Count <= 0)
            return;

        foreach (UnitManager supplier in touchedSupplierUnits)
        {
            if (supplier == null || !supplier.gameObject.activeInHierarchy)
                continue;
            if (supplier.IsEmbarked)
                continue;

            SetSupplierHudVisibleForCommandSource(supplier, true);
        }
    }

    private static bool HasEmbarkedPassengersForTransportIndicator(UnitManager supplier)
    {
        if (supplier == null)
            return false;

        IReadOnlyList<UnitTransportSeatRuntime> seats = supplier.TransportedUnitSlots;
        if (seats == null || seats.Count <= 0)
            return false;

        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            UnitManager passenger = seat != null ? seat.embarkedUnit : null;
            if (passenger != null && passenger.IsEmbarked && passenger.EmbarkedTransporter == supplier)
                return true;
        }

        return false;
    }

    private static bool ResolveDetectedIndicatorForHud(UnitManager unit)
    {
        if (unit == null)
            return false;

        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null || !unitData.IsStealthUnit(unit.GetDomain(), unit.GetHeightLevel()))
            return false;

        MatchController controller = Object.FindAnyObjectByType<MatchController>();
        int viewerTeamId = controller != null ? controller.ActiveTeamId : (int)unit.TeamId;
        return PodeMirarSensor.IsStealthTargetRevealedForTeam(unit, viewerTeamId);
    }

    private static string BuildCommandServiceDetailedReportLog(List<CommandServiceTargetReport> rows)
    {
        if (rows == null || rows.Count <= 0)
            return "[ServicoComando] Relatorio detalhado: nenhum alvo atendido.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[ServicoComando] Relatorio detalhado por unidade:");
        for (int i = 0; i < rows.Count; i++)
        {
            CommandServiceTargetReport row = rows[i];
            if (row == null || row.target == null)
                continue;

            string unitId = !string.IsNullOrWhiteSpace(row.target.UnitId) ? row.target.UnitId : "(sem-id)";
            sb.Append(i + 1);
            sb.Append(". ");
            sb.Append(row.target.name);
            sb.Append(" [id=");
            sb.Append(unitId);
            sb.Append("] => HP +");
            sb.Append(row.hpRecovered);
            sb.Append(" | AUT +");
            sb.Append(row.fuelRecovered);
            sb.Append(" | MUN +");
            sb.Append(row.ammoRecovered);
            sb.AppendLine();

            for (int s = 0; s < row.serviceLines.Count; s++)
            {
                sb.Append("   - ");
                sb.AppendLine(row.serviceLines[s]);
            }
        }

        return sb.ToString();
    }

    private static void NormalizeCommandServiceQueueForEmbarkedFamilies(List<ServicoDoComandoOption> orders)
    {
        if (orders == null || orders.Count <= 1)
            return;

        var blocks = new List<(List<ServicoDoComandoOption> items, UnitManager sortUnit, int originalIndex)>();
        HashSet<ServicoDoComandoOption> used = new HashSet<ServicoDoComandoOption>();

        // Identifica transportadores raiz: nao sao passageiros de outro transporter na lista.
        var allTransporters = new HashSet<UnitManager>();
        for (int i = 0; i < orders.Count; i++)
        {
            ServicoDoComandoOption opt = orders[i];
            if (opt != null && opt.sourceSupplierUnit != null &&
                opt.targetUnit != null && opt.targetUnit.IsEmbarked &&
                opt.targetUnit.EmbarkedTransporter == opt.sourceSupplierUnit)
                allTransporters.Add(opt.sourceSupplierUnit);
        }

        var handledTransporters = new HashSet<UnitManager>();
        for (int i = 0; i < orders.Count; i++)
        {
            ServicoDoComandoOption opt = orders[i];
            if (opt == null || opt.sourceSupplierUnit == null || opt.targetUnit == null)
                continue;
            if (!opt.targetUnit.IsEmbarked || opt.targetUnit.EmbarkedTransporter != opt.sourceSupplierUnit)
                continue;

            UnitManager transporter = opt.sourceSupplierUnit;
            if (handledTransporters.Contains(transporter))
                continue;
            handledTransporters.Add(transporter);

            bool isRoot = !allTransporters.Contains(transporter.EmbarkedTransporter);
            if (isRoot)
            {
                var family = new List<ServicoDoComandoOption>();
                AppendTransportFamilyPreOrder(transporter, orders, family, used);
                if (family.Count > 0)
                    blocks.Add((family, transporter, i));
            }
        }

        // Unidades sem familia formam blocos individuais.
        for (int i = 0; i < orders.Count; i++)
        {
            ServicoDoComandoOption option = orders[i];
            if (option == null || used.Contains(option))
                continue;
            blocks.Add((new List<ServicoDoComandoOption> { option }, option.targetUnit, i));
            used.Add(option);
        }

        // Ordena os blocos pelo custo da unidade raiz. Assim uma familia embarcada
        // permanece junta e ocupa a posicao correspondente ao custo do transportador.
        blocks.Sort((a, b) =>
        {
            int byCost = ResolveCommandServiceUnitCost(b.sortUnit)
                .CompareTo(ResolveCommandServiceUnitCost(a.sortUnit));
            return byCost != 0 ? byCost : a.originalIndex.CompareTo(b.originalIndex);
        });

        List<ServicoDoComandoOption> normalized = new List<ServicoDoComandoOption>(orders.Count);
        for (int i = 0; i < blocks.Count; i++)
            normalized.AddRange(blocks[i].items);

        orders.Clear();
        orders.AddRange(normalized);
    }

    private static int ResolveCommandServiceUnitCost(UnitManager unit)
    {
        return unit != null && unit.TryGetUnitData(out UnitData data) && data != null
            ? Mathf.Max(0, data.cost)
            : 0;
    }

    private static void AppendTransportFamilyPreOrder(
        UnitManager transporter,
        List<ServicoDoComandoOption> source,
        List<ServicoDoComandoOption> result,
        HashSet<ServicoDoComandoOption> used)
    {
        // 1. Opcao do proprio transporter primeiro.
        for (int i = 0; i < source.Count; i++)
        {
            ServicoDoComandoOption opt = source[i];
            if (opt == null || used.Contains(opt) || opt.targetUnit != transporter)
                continue;
            result.Add(opt);
            used.Add(opt);
            break;
        }

        // 2. Filhos por ordem de assento; filhos que sao transportadores recursam.
        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null)
            return;

        for (int s = 0; s < seats.Count; s++)
        {
            UnitTransportSeatRuntime seat = seats[s];
            UnitManager passenger = seat != null ? seat.embarkedUnit : null;
            if (passenger == null || !passenger.IsEmbarked || passenger.EmbarkedTransporter != transporter)
                continue;

            bool passengerIsTransporter = false;
            for (int i = 0; i < source.Count; i++)
            {
                ServicoDoComandoOption opt = source[i];
                if (opt == null || used.Contains(opt))
                    continue;
                if (opt.sourceSupplierUnit != transporter || opt.targetUnit != passenger)
                    continue;
                result.Add(opt);
                used.Add(opt);

                if (!passengerIsTransporter && passenger.TransportedUnitSlots != null && passenger.TransportedUnitSlots.Count > 0)
                {
                    passengerIsTransporter = true;
                    AppendTransportFamilyPreOrder(passenger, source, result, used);
                }
            }
        }
    }

    private static string ResolveServiceLabel(ServiceData service)
    {
        if (service == null)
            return "(servico)";
        if (!string.IsNullOrWhiteSpace(service.displayName))
            return service.displayName;
        if (!string.IsNullOrWhiteSpace(service.id))
            return service.id;
        return service.name;
    }

    private static string ResolveServiceUpdateLabel(ServiceData service)
    {
        if (service == null)
            return "(servico)";
        if (!string.IsNullOrWhiteSpace(service.apelido))
            return service.apelido;
        if (!string.IsNullOrWhiteSpace(service.displayName))
            return service.displayName;
        if (!string.IsNullOrWhiteSpace(service.id))
            return service.id;
        return service.name;
    }

    private static bool ApplyConstructionServicesToTarget(
        ConstructionManager sourceConstruction,
        UnitManager target,
        List<ServiceData> services,
        out int hpRecovered,
        out int fuelRecovered,
        out int ammoRecovered)
    {
        hpRecovered = 0;
        fuelRecovered = 0;
        ammoRecovered = 0;
        if (sourceConstruction == null || target == null || services == null || services.Count <= 0)
            return false;

        bool any = false;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;

            if (service.apenasEntreSupridores && !IsSupplier(target))
                continue;

            if (service.recuperaHp)
            {
                int applied = ApplyHpService(sourceConstruction, target, service);
                if (applied > 0)
                {
                    hpRecovered += applied;
                    any = true;
                }
            }

            if (service.recuperaAutonomia)
            {
                int applied = ApplyFuelService(sourceConstruction, target, service);
                if (applied > 0)
                {
                    fuelRecovered += applied;
                    any = true;
                }
            }

            if (service.recuperaMunicao)
            {
                int applied = ApplyAmmoService(sourceConstruction, target, service);
                if (applied > 0)
                {
                    ammoRecovered += applied;
                    any = true;
                }
            }
        }

        if (any)
            target.MarkReceivedSuppliesThisTurn();

        return any;
    }

    private static int ApplyHpService(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        string sourceName = sourceConstruction != null ? sourceConstruction.name : "(construcao-null)";
        string targetName = target != null ? target.name : "(target-null)";
        string serviceLabel = ResolveServiceLabel(service);

        int missing = Mathf.Max(0, target.GetMaxHP() - target.CurrentHP);
        if (missing <= 0)
        {
            CommandServiceLog($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (HP cheio: {target.CurrentHP}/{target.GetMaxHP()})");
            return 0;
        }

        int cap = service.serviceLimitPerUnitPerTurn > 0
            ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn)
            : missing;
        if (cap <= 0)
        {
            CommandServiceLog($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (cap=0, limite por turno={service.serviceLimitPerUnitPerTurn})");
            return 0;
        }

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
        {
            CommandServiceLog($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (eficiencia HP <= 0 para classe)");
            return 0;
        }

        if (!TryResolveSupplyForService(sourceConstruction, service, out SupplyData supply, out int stock))
        {
            CommandServiceLog($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (sem estoque de suprimento para o servico)");
            return 0;
        }

        int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
        int recovered = Mathf.Min(cap, maxByStock);
        if (recovered <= 0)
        {
            CommandServiceLog($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (recuperacao calculada=0; stock={stock}, pontosPorSup={pointsPerSupply}, maxByStock={maxByStock})");
            return 0;
        }

        int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        if (!TryConsumeSupplyFromConstruction(sourceConstruction, supply, supplies))
        {
            CommandServiceLog($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (falha ao consumir suprimento; qtdNec={supplies})");
            return 0;
        }

        int beforeHp = target.CurrentHP;
        target.SetCurrentHP(target.CurrentHP + recovered);
        int afterHp = target.CurrentHP;
        int actualGain = Mathf.Max(0, afterHp - beforeHp);
        CommandServiceLog($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | {beforeHp}->{afterHp} (+{actualGain})");
        return recovered;
    }

    private static int ApplyFuelService(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        int missing = Mathf.Max(0, target.MaxFuel - target.CurrentFuel);
        if (missing <= 0)
            return 0;

        int cap = service.serviceLimitPerUnitPerTurn > 0
            ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn)
            : missing;
        if (cap <= 0)
            return 0;

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
            return 0;

        if (!TryResolveSupplyForService(sourceConstruction, service, out SupplyData supply, out int stock))
            return 0;

        int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
        int recovered = Mathf.Min(cap, maxByStock);
        if (recovered <= 0)
            return 0;

        int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        if (!TryConsumeSupplyFromConstruction(sourceConstruction, supply, supplies))
            return 0;

        target.SetCurrentFuel(target.CurrentFuel + recovered);
        return recovered;
    }

    private static int ApplyAmmoService(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        List<UnitEmbarkedWeapon> baselineWeapons = targetData.embarkedWeapons;
        if (runtimeWeapons == null || baselineWeapons == null)
            return 0;

        int count = Mathf.Min(runtimeWeapons.Count, baselineWeapons.Count);
        if (count <= 0)
            return 0;

        int serviceBudget = service.serviceLimitPerUnitPerTurn > 0
            ? service.serviceLimitPerUnitPerTurn
            : int.MaxValue;
        int recoveredTotal = 0;
        bool hasMissingAmmo = false;
        bool hasPositiveEfficiency = false;
        bool hasStockForAmmo = false;
        bool consumeFailed = false;
        string sourceName = sourceConstruction != null ? sourceConstruction.name : "(construcao-null)";
        string targetName = target != null ? target.name : "(target-null)";
        string serviceLabel = ResolveServiceLabel(service);

        for (int i = 0; i < count && serviceBudget > 0; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = baselineWeapons[i];
            if (runtime == null || baseline == null || baseline.weapon == null)
                continue;

            int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
            int beforeAmmo = Mathf.Max(0, runtime.squadAmmunition);
            int missing = Mathf.Max(0, maxAmmo - beforeAmmo);
            if (missing <= 0)
                continue;
            hasMissingAmmo = true;

            int cap = Mathf.Min(missing, serviceBudget);
            if (cap <= 0)
                continue;

            int pointsPerSupply = ResolvePointsPerSupply(service, ResolveWeaponClass(baseline.weapon));
            if (pointsPerSupply <= 0)
                continue;
            hasPositiveEfficiency = true;

            if (!TryResolveSupplyForService(sourceConstruction, service, out SupplyData supply, out int stock))
                break;
            hasStockForAmmo = true;

            int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
            int recovered = Mathf.Min(cap, maxByStock);
            if (recovered <= 0)
                continue;

            int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
            if (!TryConsumeSupplyFromConstruction(sourceConstruction, supply, supplies))
            {
                consumeFailed = true;
                continue;
            }

            runtime.squadAmmunition = Mathf.Clamp(beforeAmmo + recovered, 0, maxAmmo);
            int afterAmmo = runtime.squadAmmunition;
            int actualGain = Mathf.Max(0, afterAmmo - beforeAmmo);
            if (actualGain > 0)
            {
                string weaponLabel = ResolveWeaponLabel(baseline.weapon);
                CommandServiceLog($"[AmmoGain] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | arma={weaponLabel} | {beforeAmmo}->{afterAmmo} (+{actualGain})");
            }
            recoveredTotal += recovered;
            serviceBudget -= recovered;
        }

        if (recoveredTotal <= 0)
        {
            string reason = !hasMissingAmmo
                ? "todas as armas ja estao com municao cheia"
                : !hasPositiveEfficiency
                    ? "eficiencia de municao <= 0 para as classes das armas com falta"
                    : !hasStockForAmmo
                        ? "sem estoque de suprimento para municao"
                        : consumeFailed
                            ? "falha ao consumir suprimento de municao"
                            : "sem ganho calculado por limites/cap";
            CommandServiceLog($"[AmmoGain] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem rearm ({reason})");
        }

        return recoveredTotal;
    }

    private static bool CanServiceApplyNow(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        if (sourceConstruction == null || target == null || service == null)
            return false;
        if (!TryResolveSupplyForService(sourceConstruction, service, out _, out int stock) || stock <= 0)
            return false;
        return CanServiceApplyByClassAndNeed(target, service);
    }

    private static bool CanServiceApplyNow(UnitManager supplier, UnitManager target, ServiceData service)
    {
        if (supplier == null || target == null || service == null)
            return false;
        if (!TryResolveSupplyForService(supplier, service, out _, out int stock) || stock <= 0)
            return false;
        return CanServiceApplyByClassAndNeed(target, service);
    }

    private static bool CanServiceApplyByClassAndNeed(UnitManager target, ServiceData service)
    {
        if (target == null || service == null)
            return false;

        if (service.recuperaHp && target.CurrentHP < target.GetMaxHP())
        {
            int hpPoints = ResolvePointsPerSupply(service, ResolveArmorClass(target));
            if (hpPoints > 0)
                return true;
        }

        if (service.recuperaAutonomia && target.CurrentFuel < target.MaxFuel)
        {
            int fuelPoints = ResolvePointsPerSupply(service, ResolveArmorClass(target));
            if (fuelPoints > 0)
                return true;
        }

        if (service.recuperaMunicao && target.TryGetUnitData(out UnitData data) && data != null)
        {
            IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
            List<UnitEmbarkedWeapon> baselineWeapons = data.embarkedWeapons;
            if (runtimeWeapons != null && baselineWeapons != null)
            {
                int count = Mathf.Min(runtimeWeapons.Count, baselineWeapons.Count);
                for (int i = 0; i < count; i++)
                {
                    UnitEmbarkedWeapon runtime = runtimeWeapons[i];
                    UnitEmbarkedWeapon baseline = baselineWeapons[i];
                    if (runtime == null || baseline == null || baseline.weapon == null)
                        continue;

                    int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
                    if (runtime.squadAmmunition >= maxAmmo)
                        continue;

                    int ammoPoints = ResolvePointsPerSupply(service, ResolveWeaponClass(baseline.weapon));
                    if (ammoPoints > 0)
                        return true;
                }
            }
        }

        return false;
    }

    private static bool ServiceHasAvailableSuppliesNow(ConstructionManager sourceConstruction, ServiceData service)
    {
        if (sourceConstruction == null || service == null)
            return false;
        if (service.suppliesUsed == null || service.suppliesUsed.Count <= 0)
            return true;
        return TryResolveSupplyForService(sourceConstruction, service, out _, out int stock) && stock > 0;
    }

    private static bool TryResolveSupplyForService(ConstructionManager sourceConstruction, ServiceData service, out SupplyData supply, out int stockAmount)
    {
        supply = null;
        stockAmount = 0;
        if (sourceConstruction == null || service == null)
            return false;
        if (service.suppliesUsed == null || service.suppliesUsed.Count <= 0)
            return false;

        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData candidate = service.suppliesUsed[i];
            if (candidate == null)
                continue;

            int amount = GetConstructionSupplyAmount(sourceConstruction, candidate);
            if (amount <= 0)
                continue;

            supply = candidate;
            stockAmount = amount;
            return true;
        }

        return false;
    }

    private static int GetConstructionSupplyAmount(ConstructionManager sourceConstruction, SupplyData supply)
    {
        if (sourceConstruction == null || supply == null)
            return 0;
        if (sourceConstruction.HasInfiniteSuppliesFor(supply))
            return int.MaxValue;

        IReadOnlyList<ConstructionSupplyOffer> offers = sourceConstruction.OfferedSupplies;
        if (offers == null)
            return 0;

        int total = 0;
        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply != supply)
                continue;
            total += Mathf.Max(0, offer.quantity);
            if (total >= int.MaxValue)
                return int.MaxValue;
        }

        return total;
    }

    private static bool TryConsumeSupplyFromConstruction(ConstructionManager sourceConstruction, SupplyData supply, int amount)
    {
        if (sourceConstruction == null || supply == null || amount <= 0)
            return false;
        if (sourceConstruction.HasInfiniteSuppliesFor(supply))
            return sourceConstruction.ContainsOfferedSupply(supply);

        IReadOnlyList<ConstructionSupplyOffer> offers = sourceConstruction.OfferedSupplies;
        if (offers == null)
            return false;

        int remaining = amount;
        for (int i = 0; i < offers.Count && remaining > 0; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply != supply || offer.quantity <= 0)
                continue;

            if (offer.quantity >= int.MaxValue)
                return true;

            int spent = Mathf.Min(offer.quantity, remaining);
            offer.quantity -= spent;
            remaining -= spent;
        }

        return remaining <= 0;
    }

    private static Dictionary<SupplyData, int> BuildConstructionStockSnapshot(ConstructionManager sourceConstruction)
    {
        Dictionary<SupplyData, int> map = new Dictionary<SupplyData, int>();
        if (sourceConstruction == null)
            return map;

        IReadOnlyList<ConstructionSupplyOffer> offers = sourceConstruction.OfferedSupplies;
        if (offers == null)
            return map;

        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply == null)
                continue;

            int current = map.TryGetValue(offer.supply, out int existing) ? existing : 0;
            int add = sourceConstruction.HasInfiniteSuppliesFor(offer.supply) || offer.quantity >= int.MaxValue
                ? int.MaxValue
                : Mathf.Max(0, offer.quantity);
            if (current == int.MaxValue || add == int.MaxValue)
            {
                map[offer.supply] = int.MaxValue;
            }
            else
            {
                long sum = (long)current + add;
                map[offer.supply] = sum >= int.MaxValue ? int.MaxValue : (int)sum;
            }
        }

        return map;
    }

    private static Dictionary<SupplyData, int> BuildSupplierStockSnapshot(UnitManager supplier)
    {
        Dictionary<SupplyData, int> map = new Dictionary<SupplyData, int>();
        if (supplier == null)
            return map;

        IReadOnlyList<UnitEmbarkedSupply> resources = supplier.GetEmbarkedResources();
        if (resources == null)
            return map;

        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;

            int current = map.TryGetValue(entry.supply, out int existing) ? existing : 0;
            long sum = (long)current + Mathf.Max(0, entry.amount);
            map[entry.supply] = sum >= int.MaxValue ? int.MaxValue : (int)sum;
        }

        return map;
    }

    private static void EstimatePotentialServiceGains(
        UnitManager target,
        ServiceData service,
        Dictionary<SupplyData, int> sourceStock,
        out int hpGain,
        out int fuelGain,
        out int ammoGain,
        List<int> ammoByWeapon = null,
        int simulatedHp = -1,
        int simulatedFuel = -1,
        List<int> simulatedAmmoByWeapon = null)
    {
        ServiceLogisticsFormula.EstimatePotentialServiceGains(
            target,
            service,
            sourceStock,
            out hpGain,
            out fuelGain,
            out ammoGain,
            ammoByWeapon,
            simulatedHp,
            simulatedFuel,
            simulatedAmmoByWeapon);
    }

    private static List<int> BuildRuntimeAmmoSnapshot(UnitManager target)
    {
        List<int> snapshot = new List<int>();
        if (target == null)
            return snapshot;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        if (runtimeWeapons == null)
            return snapshot;

        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            snapshot.Add(runtime != null ? Mathf.Max(0, runtime.squadAmmunition) : 0);
        }

        return snapshot;
    }

    private static Dictionary<SupplyData, int> CloneSupplySnapshot(Dictionary<SupplyData, int> sourceStock)
    {
        return sourceStock != null
            ? new Dictionary<SupplyData, int>(sourceStock)
            : new Dictionary<SupplyData, int>();
    }

    private static List<int> CloneAmmoSnapshot(List<int> source)
    {
        return source != null ? new List<int>(source) : new List<int>();
    }

    private static void OverwriteSupplySnapshot(Dictionary<SupplyData, int> destination, Dictionary<SupplyData, int> source)
    {
        if (destination == null || source == null)
            return;

        destination.Clear();
        foreach (KeyValuePair<SupplyData, int> pair in source)
            destination[pair.Key] = pair.Value;
    }

    private static void MergeAmmoGainSnapshot(List<int> destination, List<int> gains)
    {
        if (destination == null || gains == null || gains.Count <= 0)
            return;

        while (destination.Count < gains.Count)
            destination.Add(0);

        for (int i = 0; i < gains.Count; i++)
            destination[i] += Mathf.Max(0, gains[i]);
    }

    private static bool TryResolveSupplyFromSnapshot(ServiceData service, Dictionary<SupplyData, int> stockBySupply, out SupplyData supply, out int amount)
    {
        supply = null;
        amount = 0;
        if (service == null || stockBySupply == null || service.suppliesUsed == null)
            return false;

        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData candidate = service.suppliesUsed[i];
            if (candidate == null)
                continue;

            int current = ReadStockAmount(stockBySupply, candidate);
            if (current <= 0)
                continue;

            supply = candidate;
            amount = current;
            return true;
        }

        return false;
    }

    private static int ReadStockAmount(Dictionary<SupplyData, int> stockBySupply, SupplyData supply)
    {
        if (stockBySupply == null || supply == null)
            return 0;

        return stockBySupply.TryGetValue(supply, out int current) ? current : 0;
    }

    private static int ConsumeFromSnapshot(Dictionary<SupplyData, int> stockBySupply, SupplyData supply, int amount)
    {
        if (stockBySupply == null || supply == null || amount <= 0)
            return 0;
        if (!stockBySupply.TryGetValue(supply, out int current) || current <= 0)
            return 0;
        if (current == int.MaxValue)
            return amount;

        int spent = Mathf.Min(current, amount);
        stockBySupply[supply] = Mathf.Max(0, current - spent);
        return spent;
    }

    // ── Serviço do Comando automático ─────────────────────────────────────

    public void HandleAutoCommandServiceTeamChanged(int teamId)
    {
        if (matchController == null) return;
        if (!matchController.IsPlayerCommandServiceAutomatic((TeamId)teamId))
            return;
        // IA faz o serviço visualmente via ReplayManager; não usa a rotina automática.
        if (matchController.IsPlayerAI((TeamId)teamId))
            return;

        // Turno 0 = frame de inicializacao: nao roda automaticamente.
        if (matchController.CurrentTurn < 1)
            return;

        if (autoCommandServiceRoutine != null)
            StopCoroutine(autoCommandServiceRoutine);

        autoCommandServiceRoutine = StartCoroutine(AutoCommandServiceRoutine());
    }

    private IEnumerator AutoCommandServiceRoutine()
    {
        Debug.Log("[CS] AutoCommandServiceRoutine: iniciado");
        // Aguarda ao menos um frame para que todos os Start() das unidades
        // tenham sido executados antes de consultar UnitManager.AllActive.
        yield return null;

        // Aguarda supply queue e quaisquer outras animacoes terminarem,
        // e cursor voltar ao Neutral.
        while (supplyExecutionInProgress ||
               IsCommandServiceExecutionRunning ||
               CurrentCursorState != CursorState.Neutral)
        {
            yield return null;
        }

        // Simula "X": abre o preview exatamente como o jogador faria.
        // O helper panel mostra o resumo e o cursor vai para CommandService.
        if (!TryOpenCommandServiceFromMenu(out _))
        {
            JogadasManager.EnsureInstance()?.RegistrarServicoComando(
                matchController != null ? matchController.CurrentTurn : 0,
                matchController != null ? matchController.ActiveTeamId : 0);
            autoCommandServiceRoutine = null;
            yield break;
        }

        // Aguarda 1 frame para o estado se estabelecer.
        yield return null;

        // Simula "Enter": confirma usando os orders ja populados pelo preview,
        // sem re-executar o sensor nem limpar a fila (exatamente como o jogador faria).
        TryConfirmPendingCommandServiceOrder();

        // Só nula depois de TryStartCommandServiceOrder, garantindo que
        // IsCommandServiceExecutionRunning já esteja true antes de IsAutoCommandServiceBusy
        // retornar false — sem janela de race condition para a IA escapar.
        autoCommandServiceRoutine = null;
    }
}
