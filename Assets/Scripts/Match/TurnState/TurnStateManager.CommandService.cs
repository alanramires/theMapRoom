using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private sealed class CommandServiceTargetReport
    {
        public UnitManager target;
        public readonly List<string> serviceLines = new List<string>();
        public int hpRecovered;
        public int fuelRecovered;
        public int ammoRecovered;
    }

    private readonly List<ServicoDoComandoOption> commandServiceQueuedOrders = new List<ServicoDoComandoOption>();
    private readonly List<ServicoDoComandoInvalidOption> commandServiceInvalidOrders = new List<ServicoDoComandoInvalidOption>();
    private bool commandServiceExecutionInProgress;
    public bool IsPlayerCursorLockedByCommandService => commandServiceExecutionInProgress;

    private void ProcessCommandServiceHotkeyInput()
    {
        if (!WasLetterPressedThisFrame('X'))
            return;

        TryStartCommandServiceOrder(out _, emitLogs: true);
    }

    public bool TryStartCommandServiceOrder(out string message)
    {
        return TryStartCommandServiceOrder(out message, emitLogs: true);
    }

    private bool TryStartCommandServiceOrder(out string message, bool emitLogs)
    {
        message = string.Empty;
        if (commandServiceExecutionInProgress ||
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
                Debug.Log(message);
            return false;
        }

        if (cursorState != CursorState.Neutral)
        {
            message = $"Servico do Comando (\"X\") exige cursor em Neutral (atual: {cursorState}).";
            if (emitLogs)
                Debug.Log(message);
            return false;
        }

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : (cursorController != null ? cursorController.BoardTilemap : null);
        int activeTeamId = matchController != null ? matchController.ActiveTeamId : -1;
        if (activeTeamId < 0)
        {
            message = "Servico do Comando (\"X\"): sem time ativo valido.";
            if (emitLogs)
                Debug.Log(message);
            return false;
        }

        bool canRun = ServicoDoComandoSensor.CollectOptions(
            (TeamId)activeTeamId,
            boardMap,
            terrainDatabase,
            commandServiceQueuedOrders,
            out string reason,
            commandServiceInvalidOrders);
        if (!canRun || commandServiceQueuedOrders.Count <= 0)
        {
            string suffix = commandServiceInvalidOrders.Count > 0 ? $" | invalidos={commandServiceInvalidOrders.Count}" : string.Empty;
            message = $"Servico do Comando (\"X\"): {reason}{suffix}";
            if (emitLogs)
                Debug.Log(message);
            cursorController?.PlayLoadSfx();
            return false;
        }

        message = $"Servico do Comando (\"X\"): iniciando ordem com {commandServiceQueuedOrders.Count} unidade(s).";
        if (emitLogs)
            Debug.Log(message);
        StartCoroutine(ExecuteCommandServiceOrderSequence());
        return true;
    }

    private IEnumerator ExecuteCommandServiceOrderSequence()
    {
        commandServiceExecutionInProgress = true;
        RestoreSupplyEmbarkedSelectionVisuals();

        Debug.Log($"[ServicoComando][Fila] Inicio da execucao: {commandServiceQueuedOrders.Count} ordem(ns).");
        for (int q = 0; q < commandServiceQueuedOrders.Count; q++)
        {
            ServicoDoComandoOption queued = commandServiceQueuedOrders[q];
            UnitManager queuedTarget = queued != null ? queued.targetUnit : null;
            string targetName = queuedTarget != null ? queuedTarget.name : "(null)";
            string sourceName = queued != null && queued.sourceConstruction != null
                ? $"construcao={queued.sourceConstruction.name}"
                : (queued != null && queued.sourceSupplierUnit != null ? $"fornecedor={queued.sourceSupplierUnit.name}" : "fonte=(null)");
            Debug.Log($"[ServicoComando][Fila] {q + 1}/{commandServiceQueuedOrders.Count} alvo={targetName} | {sourceName}");
        }

        int servedTargets = 0;
        int recoveredHp = 0;
        int recoveredFuel = 0;
        int recoveredAmmo = 0;
        int totalMoneySpent = 0;
        List<CommandServiceTargetReport> detailedReport = new List<CommandServiceTargetReport>();
        bool stopByEconomy = false;

        for (int i = 0; i < commandServiceQueuedOrders.Count; i++)
        {
            if (stopByEconomy)
                break;

            ServicoDoComandoOption order = commandServiceQueuedOrders[i];
            if (order == null || order.targetUnit == null)
                continue;
            if (order.targetUnit.ReceivedSuppliesThisTurn)
                continue;

            UnitManager target = order.targetUnit;
            ConstructionManager sourceConstruction = order.sourceConstruction;
            UnitManager sourceSupplierUnit = order.sourceSupplierUnit;
            bool fromConstruction = sourceConstruction != null;
            bool fromSupplierUnit = sourceSupplierUnit != null;
            if (!fromConstruction && !fromSupplierUnit)
                continue;

            string sourceLabel = fromConstruction
                ? $"construcao={sourceConstruction.name}"
                : $"fornecedor={sourceSupplierUnit.name}";
            Debug.Log($"[ServicoComando][Fila] {i + 1}/{commandServiceQueuedOrders.Count} alvo={target.name} | {sourceLabel}");

            if (fromConstruction && (int)target.TeamId != (int)sourceConstruction.TeamId)
                continue;
            if (fromSupplierUnit && (int)target.TeamId != (int)sourceSupplierUnit.TeamId)
                continue;
            bool isEmbarkedPassenger = fromSupplierUnit && target.IsEmbarked && target.EmbarkedTransporter == sourceSupplierUnit;
            if (fromSupplierUnit && !isEmbarkedPassenger)
                continue;
            if ((!target.gameObject.activeInHierarchy && !isEmbarkedPassenger) || (target.IsEmbarked && !isEmbarkedPassenger))
                continue;

            Tilemap boardMap = terrainTilemap != null ? terrainTilemap : (target != null ? target.BoardTilemap : null);

            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            bool supplierHudHiddenForEmbarked = false;
            if (isEmbarkedPassenger)
            {
                Debug.Log($"detectado, embarcado em {sourceSupplierUnit.name}");
                ForceHideEmbarkedPassengersExcept(sourceSupplierUnit, target);
                HideAllSupplyEmbarkedPreviewExcept(target);
                if (!supplyEmbarkedPreviewStates.ContainsKey(target))
                {
                    SupplyEmbarkedPreviewState state = new SupplyEmbarkedPreviewState
                    {
                        domain = target.GetDomain(),
                        height = target.GetHeightLevel()
                    };
                    supplyEmbarkedPreviewStates[target] = state;
                    ShowEmbarkedPassengerForSupply(target, sourceSupplierUnit);
                }
                SetSupplierHudVisibleForCommandSource(sourceSupplierUnit, false);
                supplierHudHiddenForEmbarked = true;
                Debug.Log($"ocultando HUD do {sourceSupplierUnit.name}");
                targetCell = sourceSupplierUnit.CurrentCellPosition;
                targetCell.z = 0;
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
                        Debug.Log($"[ServicoComando] {target.name} ignorado: camada planejada {order.plannedServiceDomain}/{order.plannedServiceHeight} invalida ({plannedLayerReason}).");
                        continue;
                    }

                    yield return ApplySupplyLayerTransitionIfNeeded(target, order.plannedServiceDomain, order.plannedServiceHeight);
                }

                if (!isEmbarkedPassenger && order.forceSurfaceBeforeSupply)
                {
                    if (!CanUseLayerModeAtCurrentCell(target, boardMap, terrainDatabase, targetCell, Domain.Naval, HeightLevel.Surface, out string surfaceReason))
                    {
                        Debug.Log($"[ServicoComando] {target.name} ignorado: nao pode emergir para Naval/Surface ({surfaceReason}).");
                        continue;
                    }

                    yield return ApplySupplyLayerTransitionIfNeeded(target, Domain.Naval, HeightLevel.Surface);
                }

                IReadOnlyList<ServiceData> offered = fromConstruction
                    ? sourceConstruction.OfferedServices
                    : sourceSupplierUnit.GetEmbarkedServices();
                List<ServiceData> services = BuildDistinctServiceList(offered);
                if (matchController != null)
                {
                    int availableMoney = matchController.GetActualMoney(target.TeamId);
                    bool canAffordAnyServiceForTarget = false;
                    for (int s = 0; s < services.Count; s++)
                    {
                        ServiceData service = services[s];
                        if (service == null || !service.isService)
                            continue;
                        if (service.apenasEntreSupridores && !IsSupplier(target))
                            continue;
                        if (!UnitNeedsServiceForSupplyExecution(target, service))
                            continue;
                        bool hasServiceStock = fromConstruction
                            ? ServiceHasAvailableSuppliesNow(sourceConstruction, service)
                            : ServiceHasAvailableSuppliesNow(sourceSupplierUnit, service);
                        if (!hasServiceStock)
                            continue;
                        bool willActuallyApply = fromConstruction
                            ? CanServiceApplyNow(sourceConstruction, target, service)
                            : CanServiceApplyNow(sourceSupplierUnit, target, service);
                        if (!willActuallyApply)
                            continue;

                        int hpPlannedGain;
                        int fuelPlannedGain;
                        int ammoPlannedGain;
                        if (fromConstruction)
                            EstimateServiceGainsFromConstruction(sourceConstruction, target, service, out hpPlannedGain, out fuelPlannedGain, out ammoPlannedGain);
                        else
                            EstimateServiceGainsFromSupplier(sourceSupplierUnit, target, service, out hpPlannedGain, out fuelPlannedGain, out ammoPlannedGain);

                        if (hpPlannedGain <= 0 && fuelPlannedGain <= 0 && ammoPlannedGain <= 0)
                            continue;

                        int projectedCost = matchController.ResolveEconomyCost(
                            ComputeServiceMoneyCost(target, service, hpPlannedGain, fuelPlannedGain, ammoPlannedGain));
                        if (projectedCost <= availableMoney)
                        {
                            canAffordAnyServiceForTarget = true;
                            break;
                        }
                    }

                    if (!canAffordAnyServiceForTarget)
                    {
                        stopByEconomy = true;
                        cursorController?.PlayErrorSfx();
                        Debug.Log($"[ServicoComando] Interrompido: saldo insuficiente para continuar no alvo {target.name} (saldo atual=${Mathf.Max(0, availableMoney)}).");
                        break;
                    }
                }

                int hpGain = 0;
                int fuelGain = 0;
                int ammoGain = 0;
                CommandServiceTargetReport targetReport = new CommandServiceTargetReport
                {
                    target = target
                };

                for (int s = 0; s < services.Count; s++)
                {
                    ServiceData service = services[s];
                    if (service == null || !service.isService)
                        continue;
                    if (service.apenasEntreSupridores && !IsSupplier(target))
                        continue;
                    if (!UnitNeedsServiceForSupplyExecution(target, service))
                        continue;
                    bool hasServiceStock = fromConstruction
                        ? ServiceHasAvailableSuppliesNow(sourceConstruction, service)
                        : ServiceHasAvailableSuppliesNow(sourceSupplierUnit, service);
                    if (!hasServiceStock)
                        continue;
                    bool willActuallyApply = fromConstruction
                        ? CanServiceApplyNow(sourceConstruction, target, service)
                        : CanServiceApplyNow(sourceSupplierUnit, target, service);
                    if (!willActuallyApply)
                        continue;

                    int hpPlannedGain;
                    int fuelPlannedGain;
                    int ammoPlannedGain;
                    if (fromConstruction)
                        EstimateServiceGainsFromConstruction(sourceConstruction, target, service, out hpPlannedGain, out fuelPlannedGain, out ammoPlannedGain);
                    else
                        EstimateServiceGainsFromSupplier(sourceSupplierUnit, target, service, out hpPlannedGain, out fuelPlannedGain, out ammoPlannedGain);

                    if (hpPlannedGain <= 0 && fuelPlannedGain <= 0 && ammoPlannedGain <= 0)
                        continue;
                    if (!TryPayServiceCostForExecution(
                            target.TeamId,
                            target,
                            service,
                            hpPlannedGain,
                            fuelPlannedGain,
                            ammoPlannedGain,
                            "ServicoComando",
                            out int serviceMoneySpent))
                    {
                        stopByEconomy = true;
                        Debug.Log("[ServicoComando] Execucao interrompida por saldo insuficiente.");
                        break;
                    }
                    totalMoneySpent += Mathf.Max(0, serviceMoneySpent);

                    Vector3 sourceWorld = fromConstruction
                        ? sourceConstruction.transform.position
                        : sourceSupplierUnit.transform.position;
                    float flightDuration = animationManager != null
                        ? animationManager.PlayServiceProjectileStraight(sourceWorld, target.transform.position, service.spriteDefault)
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
                    bool changed = fromConstruction
                        ? ApplyConstructionServicesToTarget(sourceConstruction, target, tempSingleService, out int hpStep, out int fuelStep, out int ammoStep)
                        : ApplyServicesToTarget(sourceSupplierUnit, target, tempSingleService, out hpStep, out fuelStep, out ammoStep);
                    tempSingleService.Clear();
                    if (!changed)
                        continue;

                    int hpAfterApply = Mathf.Clamp(target.CurrentHP, 0, target.GetMaxHP());
                    int actualHpGain = Mathf.Max(0, hpAfterApply - hpBeforeApply);
                    if (actualHpGain > 0)
                    {
                        int desiredHp = Mathf.Clamp(hpBeforeApply + actualHpGain, 0, target.GetMaxHP());
                        Debug.Log($"[ServicoComando][HpAnim] {target.name}: {hpBeforeApply} -> {desiredHp} (+{actualHpGain})");
                        target.SetCurrentHP(hpBeforeApply);
                        yield return AnimateHpRecoverFill(target, hpBeforeApply, desiredHp);
                    }

                    int fuelAfterApply = Mathf.Clamp(target.CurrentFuel, 0, target.MaxFuel);
                    int actualFuelGain = Mathf.Max(0, fuelAfterApply - fuelBeforeApply);
                    if (actualFuelGain > 0)
                    {
                        int desiredFuel = Mathf.Clamp(fuelBeforeApply + actualFuelGain, 0, target.MaxFuel);
                        Debug.Log($"[ServicoComando][FuelAnim] {target.name}: {fuelBeforeApply} -> {desiredFuel} (+{actualFuelGain})");
                        target.SetCurrentFuel(fuelBeforeApply);
                        yield return AnimateFuelRecoverFill(target, fuelBeforeApply, desiredFuel);
                    }
                    else if (fuelStep > 0)
                    {
                        Debug.Log($"[ServicoComando][FuelAnim] {target.name}: fuelStep={fuelStep}, mas ganho real=0 (antes={fuelBeforeApply}, depois={fuelAfterApply}, max={target.MaxFuel}).");
                    }
                    else
                    {
                        string fuelReason = service.recuperaAutonomia
                            ? (fuelBeforeApply >= target.MaxFuel
                                ? "alvo com autonomia cheia"
                                : "servico aplicado sem ganho de autonomia")
                            : "servico sem recuperaAutonomia";
                        Debug.Log($"[ServicoComando][FuelAnim] {target.name}: sem animacao de fuel ({fuelReason}) | fuel={fuelBeforeApply}/{target.MaxFuel} | service={ResolveServiceLabel(service)}");
                    }

                    hpGain += actualHpGain;
                    fuelGain += actualFuelGain;
                    ammoGain += ammoStep;

                    string serviceName = ResolveServiceLabel(service);
                    string line = $"{serviceName}: HP +{actualHpGain} | AUT +{actualFuelGain} | MUN +{ammoStep}";
                    targetReport.serviceLines.Add(line);
                }

                if (stopByEconomy)
                    break;

                if (hpGain <= 0 && fuelGain <= 0 && ammoGain <= 0)
                {
                    Debug.Log($"[ServicoComando][Fila] {target.name}: sem ganhos aplicados (HP/AUT/MUN).");
                    continue;
                }
                if (fuelGain <= 0)
                    Debug.Log($"[ServicoComando][Fila] {target.name}: sem ganho de AUT no alvo (HP +{hpGain} | AUT +{fuelGain} | MUN +{ammoGain}).");

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

                if (supplierHudHiddenForEmbarked)
                {
                    SetSupplierHudVisibleForCommandSource(sourceSupplierUnit, true);
                    Debug.Log($"[ServicoComando][Fila] restaurando HUD do {sourceSupplierUnit.name}, seguindo para proximo da fila...");
                }
            }
        }

        commandServiceQueuedOrders.Clear();
        RestoreSupplyEmbarkedSelectionVisuals();

        if (servedTargets <= 0)
        {
            Debug.Log("[ServicoComando] Nenhum alvo recebeu servico (necessidade/estoque).");
            cursorController?.PlayLoadSfx();
            commandServiceExecutionInProgress = false;
            yield break;
        }

        Debug.Log($"[ServicoComando] alvos atendidos={servedTargets} | HP +{recoveredHp} | autonomia +{recoveredFuel} | municao +{recoveredAmmo} | custo ${Mathf.Max(0, totalMoneySpent)}");
        Debug.Log(BuildCommandServiceDetailedReportLog(detailedReport));
        cursorController?.PlayLoadSfx();
        commandServiceExecutionInProgress = false;
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
            SetUnitSpriteRenderersVisible(passenger, false);

            UnitHudController passengerHud = passenger.GetComponentInChildren<UnitHudController>(true);
            if (passengerHud != null)
                passengerHud.gameObject.SetActive(false);

            passenger.ClearTemporarySortingOrder();
        }
    }

    private static void SetSupplierHudVisibleForCommandSource(UnitManager supplier, bool visible)
    {
        if (supplier == null)
            return;

        UnitHudController supplierHud = supplier.GetComponentInChildren<UnitHudController>(true);
        if (supplierHud != null)
            supplierHud.gameObject.SetActive(visible);
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
            Debug.Log($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (HP cheio: {target.CurrentHP}/{target.GetMaxHP()})");
            return 0;
        }

        int cap = service.serviceLimitPerUnitPerTurn > 0
            ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn)
            : missing;
        if (cap <= 0)
        {
            Debug.Log($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (cap=0, limite por turno={service.serviceLimitPerUnitPerTurn})");
            return 0;
        }

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
        {
            Debug.Log($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (eficiencia HP <= 0 para classe)");
            return 0;
        }

        if (!TryResolveSupplyForService(sourceConstruction, service, out SupplyData supply, out int stock))
        {
            Debug.Log($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (sem estoque de suprimento para o servico)");
            return 0;
        }

        int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
        int recovered = Mathf.Min(cap, maxByStock);
        if (recovered <= 0)
        {
            Debug.Log($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (recuperacao calculada=0; stock={stock}, pontosPorSup={pointsPerSupply}, maxByStock={maxByStock})");
            return 0;
        }

        int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        if (!TryConsumeSupplyFromConstruction(sourceConstruction, supply, supplies))
        {
            Debug.Log($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem reparo (falha ao consumir suprimento; qtdNec={supplies})");
            return 0;
        }

        int beforeHp = target.CurrentHP;
        target.SetCurrentHP(target.CurrentHP + recovered);
        int afterHp = target.CurrentHP;
        int actualGain = Mathf.Max(0, afterHp - beforeHp);
        Debug.Log($"[HpRepair] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | {beforeHp}->{afterHp} (+{actualGain})");
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
                Debug.Log($"[AmmoGain] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | arma={weaponLabel} | {beforeAmmo}->{afterAmmo} (+{actualGain})");
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
            Debug.Log($"[AmmoGain] modo=ServicoComando | alvo={targetName} | construcao={sourceName} | servico={serviceLabel} | sem rearm ({reason})");
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
}




