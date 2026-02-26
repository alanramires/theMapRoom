using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private sealed class SupplyCandidateEntry
    {
        public PodeSuprirOption option;
        public UnitManager targetUnit;
        public int selectionNumber;
        public Vector3Int cell;
        public string label;
    }

    private sealed class SupplyQueuePreviewTrack
    {
        public UnitManager target;
        public readonly List<LineRenderer> renderers = new List<LineRenderer>();
        public readonly List<Vector3> pathPoints = new List<Vector3>(2);
        public readonly List<Vector3> tempSegmentPoints = new List<Vector3>(8);
        public float pathLength;
        public float headDistance;
    }

    private readonly List<SupplyCandidateEntry> supplyCandidateEntries = new List<SupplyCandidateEntry>();
    private readonly Dictionary<Vector3Int, int> supplyCandidateIndexByCell = new Dictionary<Vector3Int, int>();
    private readonly List<PodeSuprirOption> supplyQueuedOrders = new List<PodeSuprirOption>();
    private readonly List<SupplyQueuePreviewTrack> supplyQueuePreviewTracks = new List<SupplyQueuePreviewTrack>();
    private int supplySelectedCandidateIndex = -1;
    private bool supplyTargetAutoEntered;
    private bool supplySuppressDefaultConfirmSfxOnce;
    private bool supplyExecutionInProgress;
    private UnitManager supplyPreviewLastTarget;
    private readonly List<LineRenderer> supplyPreviewRenderers = new List<LineRenderer>();
    private readonly List<Vector3> supplyPreviewPathPoints = new List<Vector3>(2);
    private readonly List<Vector3> supplyPreviewSegmentPoints = new List<Vector3>(8);
    private float supplyPreviewPathLength;
    private float supplyPreviewHeadDistance;
    private CursorState cursorStateBeforeSuprindo = CursorState.MoveuParado;
    private static readonly List<ServiceData> tempSingleService = new List<ServiceData>(1);

    private enum SupplyServiceLayerPlan
    {
        DefaultSameDomain = 0,
        AirLow = 1,
        NavalSurface = 2
    }

    private void EnterSupplyStateFromSensors()
    {
        if (selectedUnit == null)
            return;
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return;

        cursorController?.PlayConfirmSfx();
        cursorStateBeforeSuprindo = cursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(CursorState.Suprindo, "EnterSupplyStateFromSensors");
        ClearCommittedPathVisual();
        supplyQueuedOrders.Clear();
        RebuildSupplyQueuePreviewTracks();
        EnterSupplyCandidateSelectStep();
    }

    private void ProcessSupplyPromptInput()
    {
        if (cursorState != CursorState.Suprindo || scannerPromptStep != ScannerPromptStep.MergeParticipantSelect)
            return;

        if (!TryReadPressedDigitIncludingZero(out int number))
            return;

        if (number == 0)
        {
            if (supplyQueuedOrders.Count > 0)
            {
                StartSupplyExecution();
                return;
            }

            Debug.Log("[Suprimento] Nenhuma ordem em fila para executar.");
            return;
        }

        int index = number - 1;
        SupplyCandidateEntry picked = null;
        for (int i = 0; i < supplyCandidateEntries.Count; i++)
        {
            SupplyCandidateEntry entry = supplyCandidateEntries[i];
            if (entry != null && entry.selectionNumber == number)
            {
                picked = entry;
                index = i;
                break;
            }
        }

        if (picked == null || index < 0 || index >= supplyCandidateEntries.Count)
        {
            Debug.Log($"[Suprimento] Candidato invalido: {number}. Escolha uma das opcoes listadas.");
            return;
        }

        supplySelectedCandidateIndex = index;
        cursorController?.PlayConfirmSfx();
        cursorController?.SetCell(picked.cell, playMoveSfx: false);
        UpdateSupplyPreviewFromCurrentContext();
        Debug.Log($"[Suprimento] Candidato selecionado: {picked.selectionNumber}. {picked.label} | Enter para continuar.");
    }

    private bool TryConfirmScannerSupply()
    {
        if (cursorState != CursorState.Suprindo)
            return false;

        if (scannerPromptStep == ScannerPromptStep.MergeParticipantSelect)
        {
            if (supplySelectedCandidateIndex < 0)
                SyncSupplySelectedCandidateFromCursor();
            if (!TryGetSelectedSupplyCandidate(out SupplyCandidateEntry selected))
            {
                Debug.Log("[Suprimento] Selecione um candidato valido por numero ou cursor antes de confirmar.");
                return true;
            }

            scannerPromptStep = ScannerPromptStep.MergeConfirm;
            supplyTargetAutoEntered = false;
            cursorController?.PlayConfirmSfx();
            UpdateSupplyPreviewFromCurrentContext();
            LogSupplyConfirmPrompt(selected);
            return true;
        }

        if (scannerPromptStep != ScannerPromptStep.MergeConfirm)
            return true;

        if (!TryGetSelectedSupplyCandidate(out SupplyCandidateEntry target) || target.option == null || target.targetUnit == null)
        {
            ReturnToSupplyCandidateSelect();
            return true;
        }

        if (IsSupplyTargetAlreadyQueued(target.targetUnit))
        {
            Debug.Log($"[Suprimento] {target.targetUnit.name} ja esta na fila. Escolha outra unidade.");
            ReturnToSupplyCandidateSelect();
            return true;
        }

        int queueLimit = 0;
        if (selectedUnit != null && selectedUnit.TryGetUnitData(out UnitData supplierData) && supplierData != null)
            queueLimit = Mathf.Max(0, supplierData.maxUnitsServedPerTurn);
        if (queueLimit <= 0)
        {
            Debug.Log("[Suprimento] Supplier sem capacidade de atendimento (maxUnitsServedPerTurn=0).");
            ExitSupplyStateToMovement();
            return true;
        }
        if (queueLimit > 0 && supplyQueuedOrders.Count >= queueLimit)
        {
            Debug.Log($"[Suprimento] Limite de fila atingido ({supplyQueuedOrders.Count}/{queueLimit}). Execute (0) ou desfaça (ESC).");
            ReturnToSupplyCandidateSelect();
            return true;
        }

        supplyQueuedOrders.Add(target.option);
        supplySuppressDefaultConfirmSfxOnce = true;
        cursorController?.PlayLoadSfx();
        RebuildSupplyQueuePreviewTracks();

        if (queueLimit > 0 && supplyQueuedOrders.Count >= queueLimit)
        {
            StartSupplyExecution();
            return true;
        }

        int remaining = CountRemainingSupplyCandidates();
        if (remaining <= 0)
        {
            StartSupplyExecution();
            return true;
        }

        Debug.Log($"[Suprimento] Ordem adicionada: {target.targetUnit.name}.");
        EnterSupplyCandidateSelectStep();
        return true;
    }

    private void EnterSupplyCandidateSelectStep()
    {
        RebuildSupplyCandidateEntries();
        PaintSupplyCandidateOptions();
        scannerPromptStep = ScannerPromptStep.MergeParticipantSelect;
        supplySelectedCandidateIndex = supplyCandidateEntries.Count > 0 ? 0 : -1;
        supplyTargetAutoEntered = false;

        if (cursorController != null && supplySelectedCandidateIndex >= 0 && supplySelectedCandidateIndex < supplyCandidateEntries.Count)
            cursorController.SetCell(supplyCandidateEntries[supplySelectedCandidateIndex].cell, playMoveSfx: false);

        UpdateSupplyPreviewFromCurrentContext();

        if (supplyCandidateEntries.Count <= 0)
        {
            if (supplyQueuedOrders.Count > 0)
                StartSupplyExecution();
            else
                ExitSupplyStateToMovement();
            return;
        }

        if (supplyCandidateEntries.Count == 1 && supplyQueuedOrders.Count <= 0)
        {
            supplySelectedCandidateIndex = 0;
            supplyTargetAutoEntered = true;
            scannerPromptStep = ScannerPromptStep.MergeConfirm;
            if (cursorController != null)
                cursorController.SetCell(supplyCandidateEntries[0].cell, playMoveSfx: false);
            UpdateSupplyPreviewFromCurrentContext();
            LogSupplyConfirmPrompt(supplyCandidateEntries[0]);
            return;
        }

        LogSupplyCandidateSelectionPanel();
    }

    private void ReturnToSupplyCandidateSelect()
    {
        scannerPromptStep = ScannerPromptStep.MergeParticipantSelect;
        RebuildSupplyCandidateEntries();
        PaintSupplyCandidateOptions();
        if (supplySelectedCandidateIndex < 0 || supplySelectedCandidateIndex >= supplyCandidateEntries.Count)
            supplySelectedCandidateIndex = supplyCandidateEntries.Count > 0 ? 0 : -1;
        UpdateSupplyPreviewFromCurrentContext();
        LogSupplyCandidateSelectionPanel();
    }

    private bool TryUndoLastQueuedSupplyOrderAndReturnToTarget()
    {
        if (supplyQueuedOrders.Count <= 0)
            return false;

        int lastIndex = supplyQueuedOrders.Count - 1;
        UnitManager lastTarget = supplyQueuedOrders[lastIndex] != null ? supplyQueuedOrders[lastIndex].targetUnit : null;
        supplyQueuedOrders.RemoveAt(lastIndex);
        RebuildSupplyQueuePreviewTracks();

        RebuildSupplyCandidateEntries();
        supplySelectedCandidateIndex = -1;
        for (int i = 0; i < supplyCandidateEntries.Count; i++)
        {
            SupplyCandidateEntry entry = supplyCandidateEntries[i];
            if (entry != null && entry.targetUnit == lastTarget)
            {
                supplySelectedCandidateIndex = i;
                break;
            }
        }

        ReturnToSupplyCandidateSelect();
        if (lastTarget != null)
            Debug.Log($"[Suprimento] Ordem desfeita para {lastTarget.name}. Retornando para selecao.");
        return true;
    }

    private void ExitSupplyStateToMovement()
    {
        if (cursorState != CursorState.Suprindo)
            return;

        CursorState targetMovementState = cursorStateBeforeSuprindo == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(targetMovementState, "ExitSupplyStateToMovement", rollback: true);
        if (targetMovementState == CursorState.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
            DrawCommittedPathVisual(committedMovementPath);
        if (cursorController != null && selectedUnit != null)
            cursorController.SetCell(selectedUnit.CurrentCellPosition, playMoveSfx: false);

        ResetSupplyRuntimeState();
        LogScannerPanel();
    }

    private void StartSupplyExecution()
    {
        if (supplyExecutionInProgress)
            return;
        if (selectedUnit == null || supplyQueuedOrders.Count <= 0)
        {
            ExitSupplyStateToMovement();
            return;
        }

        ClearMovementRange(keepCommittedMovement: true);
        // Ao iniciar a execucao, remove qualquer preview de confirmacao/fila.
        SetSupplyPreviewVisible(false);
        SetSupplyQueuedPreviewVisible(false);
        if (cursorController != null && selectedUnit != null)
            cursorController.SetCell(selectedUnit.CurrentCellPosition, playMoveSfx: true);
        StartCoroutine(ExecuteQueuedSupplyOrdersSequence());
    }

    private IEnumerator ExecuteQueuedSupplyOrdersSequence()
    {
        supplyExecutionInProgress = true;
        UnitManager supplier = selectedUnit;
        if (supplier == null)
        {
            supplyExecutionInProgress = false;
            ExitSupplyStateToMovement();
            yield break;
        }

        List<ServiceData> services = BuildDistinctServiceList(supplier.GetEmbarkedServices());
        int servedTargets = 0;
        int recoveredHp = 0;
        int recoveredFuel = 0;
        int recoveredAmmo = 0;
        int targetBudget = int.MaxValue;
        if (supplier.TryGetUnitData(out UnitData supplierData) && supplierData != null && supplierData.maxUnitsServedPerTurn > 0)
            targetBudget = supplierData.maxUnitsServedPerTurn;
        if (targetBudget <= 0)
        {
            Debug.Log("[Suprimento] Execucao cancelada: maxUnitsServedPerTurn=0.");
            supplyExecutionInProgress = false;
            ExitSupplyStateToMovement();
            yield break;
        }

        List<ServiceData> effectiveServices = new List<ServiceData>(services.Count);
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service != null && service.isService)
                effectiveServices.Add(service);
        }
        if (effectiveServices.Count <= 0)
        {
            Debug.Log("[Suprimento] Execucao cancelada: supplier sem servicos operacionais.");
            supplyExecutionInProgress = false;
            ExitSupplyStateToMovement();
            yield break;
        }

        SupplyServiceLayerPlan layerPlan = ResolveSupplyServiceLayerPlanForExecution(supplier, supplyQueuedOrders);
        if (TryGetSupplyLayerFromPlan(layerPlan, out Domain serviceDomain, out HeightLevel serviceHeight))
        {
            Vector3Int supplierCell = supplier.CurrentCellPosition;
            supplierCell.z = 0;
            if (!CanUseLayerModeAtCurrentCell(supplier, terrainTilemap != null ? terrainTilemap : supplier.BoardTilemap, terrainDatabase, supplierCell, serviceDomain, serviceHeight, out string supplierLayerReason))
            {
                Debug.Log($"[Suprimento] Supplier nao pode operar em {serviceDomain}/{serviceHeight} no hex atual ({supplierLayerReason}).");
                supplyExecutionInProgress = false;
                EnterSupplyCandidateSelectStep();
                yield break;
            }
            yield return ApplySupplyLayerTransitionIfNeeded(supplier, serviceDomain, serviceHeight);
        }

        for (int i = 0; i < supplyQueuedOrders.Count && servedTargets < targetBudget; i++)
        {
            PodeSuprirOption order = supplyQueuedOrders[i];
            if (order == null || order.targetUnit == null)
                continue;

            UnitManager target = order.targetUnit;
            if (!target.gameObject.activeInHierarchy || target.IsEmbarked || (int)target.TeamId != (int)supplier.TeamId)
                continue;

            Tilemap boardMap = terrainTilemap != null ? terrainTilemap : target.BoardTilemap;
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            if (cursorController != null)
                cursorController.SetCell(targetCell, playMoveSfx: true);
            float cursorFocusDelay = GetSupplyCursorFocusDelay();
            if (cursorFocusDelay > 0f)
                yield return new WaitForSeconds(cursorFocusDelay);

            if (order.forceLandBeforeSupply || order.forceTakeoffBeforeSupply)
            {
                if (!CanUseLayerModeAtCurrentCell(target, boardMap, terrainDatabase, targetCell, order.plannedServiceDomain, order.plannedServiceHeight, out string plannedLayerReason))
                {
                    Debug.Log($"[Suprimento] {target.name} ignorado: camada planejada {order.plannedServiceDomain}/{order.plannedServiceHeight} invalida no hex atual ({plannedLayerReason}).");
                    continue;
                }
                yield return ApplySupplyLayerTransitionIfNeeded(target, order.plannedServiceDomain, order.plannedServiceHeight);
            }
            if (order.forceSurfaceBeforeSupply)
            {
                if (!CanUseLayerModeAtCurrentCell(target, boardMap, terrainDatabase, targetCell, Domain.Naval, HeightLevel.Surface, out string surfaceReason))
                {
                    Debug.Log($"[Suprimento] {target.name} ignorado: nao pode emergir para Naval/Surface no hex atual ({surfaceReason}).");
                    continue;
                }
                yield return ApplySupplyLayerTransitionIfNeeded(target, Domain.Naval, HeightLevel.Surface);
            }
            if (TryGetSupplyLayerFromPlan(layerPlan, out Domain targetServiceDomain, out HeightLevel targetServiceHeight))
            {
                if (!CanUseLayerModeAtCurrentCell(target, boardMap, terrainDatabase, targetCell, targetServiceDomain, targetServiceHeight, out string queueLayerReason))
                {
                    Debug.Log($"[Suprimento] {target.name} ignorado: camada de atendimento {targetServiceDomain}/{targetServiceHeight} invalida no hex atual ({queueLayerReason}).");
                    continue;
                }
                yield return ApplySupplyLayerTransitionIfNeeded(target, targetServiceDomain, targetServiceHeight);
            }

            int hpGain = 0;
            int fuelGain = 0;
            int ammoGain = 0;
            int startFuel = Mathf.Max(0, target.CurrentFuel);
            for (int serviceIndex = 0; serviceIndex < effectiveServices.Count; serviceIndex++)
            {
                ServiceData service = effectiveServices[serviceIndex];
                if (service == null)
                    continue;
                if (service.apenasEntreSupridores && !IsSupplier(target))
                    continue;
                if (!UnitNeedsServiceForSupplyExecution(target, service))
                    continue;
                if (!ServiceHasAvailableSuppliesNow(supplier, service))
                    continue;

                float flightDuration = PlaySupplyServiceProjectile(supplier, target, service);
                float spawnInterval = GetSupplySpawnInterval();
                if (spawnInterval > 0f)
                    yield return new WaitForSeconds(spawnInterval);
                if (flightDuration > 0f)
                    yield return new WaitForSeconds(flightDuration + GetSupplyFlightPadding());

                tempSingleService.Clear();
                tempSingleService.Add(service);
                bool changed = ApplyServicesToTarget(supplier, target, tempSingleService, out int hpStep, out int fuelStep, out int ammoStep);
                tempSingleService.Clear();
                if (!changed)
                    continue;

                if (fuelStep > 0)
                {
                    int desiredFuel = Mathf.Clamp(startFuel + fuelStep, 0, target.MaxFuel);
                    target.SetCurrentFuel(startFuel);
                    yield return AnimateFuelRecoverFill(target, startFuel, desiredFuel);
                    startFuel = desiredFuel;
                }

                hpGain += hpStep;
                fuelGain += fuelStep;
                ammoGain += ammoStep;
            }

            if (hpGain <= 0 && fuelGain <= 0 && ammoGain <= 0)
                continue;

            servedTargets++;
            recoveredHp += hpGain;
            recoveredFuel += fuelGain;
            recoveredAmmo += ammoGain;
            target.MarkAsActed();
            cursorController?.PlayLoadSfx();
            float postTargetDelay = GetSupplyPostTargetDelay();
            if (postTargetDelay > 0f)
                yield return new WaitForSeconds(postTargetDelay);
        }

        if (servedTargets <= 0)
        {
            Debug.Log("[Suprimento] Nenhum alvo recebeu servico (necessidade/estoque).");
            supplyExecutionInProgress = false;
            EnterSupplyCandidateSelectStep();
            yield break;
        }

        Debug.Log($"[Suprimento] alvos atendidos={servedTargets} | HP +{recoveredHp} | autonomia +{recoveredFuel} | municao +{recoveredAmmo}");
        if (cursorController != null)
        {
            Vector3Int supplierCell = supplier.CurrentCellPosition;
            supplierCell.z = 0;
            cursorController.SetCell(supplierCell, playMoveSfx: true);
        }
        float supplierFocusDelay = GetSupplyCursorFocusDelay();
        if (supplierFocusDelay > 0f)
            yield return new WaitForSeconds(supplierFocusDelay);
        float supplierFinalDelay = GetSupplySupplierFinalDelay();
        if (supplierFinalDelay > 0f)
            yield return new WaitForSeconds(supplierFinalDelay);

        bool finalized = TryFinalizeSelectedUnitActionFromDebug();
        if (!finalized)
        {
            supplier.MarkAsActed();
            ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: true);
        }
        cursorController?.PlayDoneSfx();

        ResetSupplyRuntimeState();
        supplyExecutionInProgress = false;
    }

    private static bool UnitNeedsServiceForSupplyExecution(UnitManager target, ServiceData service)
    {
        if (target == null || service == null)
            return false;
        if (service.recuperaHp && target.CurrentHP < target.GetMaxHP())
            return true;
        if (service.recuperaAutonomia && target.CurrentFuel < target.MaxFuel)
            return true;
        if (service.recuperaMunicao && HasAnyMissingAmmo(target))
            return true;
        return false;
    }

    private static bool HasAnyMissingAmmo(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = unit.GetEmbarkedWeapons();
        List<UnitEmbarkedWeapon> dataWeapons = data.embarkedWeapons;
        if (runtimeWeapons == null || dataWeapons == null)
            return false;

        int count = Mathf.Min(runtimeWeapons.Count, dataWeapons.Count);
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = dataWeapons[i];
            if (runtime == null || baseline == null)
                continue;

            int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
            if (runtime.squadAmmunition < maxAmmo)
                return true;
        }

        return false;
    }

    private static bool ServiceHasAvailableSuppliesNow(UnitManager supplier, ServiceData service)
    {
        if (supplier == null || service == null)
            return false;
        if (service.suppliesUsed == null || service.suppliesUsed.Count <= 0)
            return true;
        return TryResolveSupplyForService(supplier, service, out _, out int stock) && stock > 0;
    }

    private float PlaySupplyServiceProjectile(UnitManager supplier, UnitManager target, ServiceData service)
    {
        if (supplier == null || target == null)
            return 0f;

        if (animationManager != null)
            return animationManager.PlayServiceProjectileStraight(supplier.transform.position, target.transform.position, service != null ? service.spriteDefault : null);

        return 0f;
    }

    private static IEnumerator AnimateFuelRecoverFill(UnitManager target, int fromFuel, int toFuel)
    {
        if (target == null)
            yield break;

        int start = Mathf.Clamp(fromFuel, 0, target.MaxFuel);
        int end = Mathf.Clamp(toFuel, 0, target.MaxFuel);
        if (end <= start)
            yield break;

        for (int value = start + 1; value <= end; value++)
        {
            target.SetCurrentFuel(value);
            yield return null;
        }
    }

    private float GetSupplySpawnInterval()
    {
        return animationManager != null ? animationManager.SupplySpawnInterval : 0.12f;
    }

    private float GetSupplyCursorFocusDelay()
    {
        return animationManager != null ? animationManager.SupplyCursorFocusDelay : 0.10f;
    }

    private float GetSupplyFlightPadding()
    {
        return animationManager != null ? animationManager.SupplyFlightPadding : 0.05f;
    }

    private float GetSupplyPostTargetDelay()
    {
        return animationManager != null ? animationManager.SupplyPostTargetDelay : 0.18f;
    }

    private float GetSupplySupplierFinalDelay()
    {
        return animationManager != null ? animationManager.SupplySupplierFinalDelay : 0.25f;
    }

    private IEnumerator ApplySupplyLayerTransitionIfNeeded(UnitManager unit, Domain domain, HeightLevel height)
    {
        if (unit == null)
            yield break;
        if (unit.GetDomain() == domain && unit.GetHeightLevel() == height)
            yield break;
        if (!unit.SupportsLayerMode(domain, height))
            yield break;

        PlayMovementStartSfx(unit);

        bool landingToSurface =
            domain == Domain.Land &&
            height == HeightLevel.Surface &&
            unit.GetDomain() == Domain.Air;

        // Usa o mesmo timing base de transicao de camada dos fluxos de landing/domain transition.
        float transitionDuration = GetLayerOperationTransitionDuration();
        if (transitionDuration > 0f)
            yield return new WaitForSeconds(transitionDuration);

        unit.TrySetCurrentLayerMode(domain, height);

        float afterSettleDuration = 0f;
        if (landingToSurface)
        {
            float vtolFxDuration = animationManager != null ? animationManager.PlayVtolLandingEffect(unit) : 0f;
            afterSettleDuration = Mathf.Max(afterSettleDuration, vtolFxDuration);
        }

        if (afterSettleDuration > 0f)
            yield return new WaitForSeconds(afterSettleDuration);

        float postTransitionDelay = GetLayerOperationAfterTransitionDelay();
        if (postTransitionDelay > 0f)
            yield return new WaitForSeconds(postTransitionDelay);
    }

    private void ResetSupplyRuntimeState()
    {
        supplyCandidateEntries.Clear();
        supplyCandidateIndexByCell.Clear();
        supplyQueuedOrders.Clear();
        supplyQueuePreviewTracks.Clear();
        supplySelectedCandidateIndex = -1;
        supplyTargetAutoEntered = false;
        supplySuppressDefaultConfirmSfxOnce = false;
        supplyPreviewLastTarget = null;
        supplyPreviewPathPoints.Clear();
        supplyPreviewSegmentPoints.Clear();
        supplyPreviewPathLength = 0f;
        supplyPreviewHeadDistance = 0f;
        ClearMovementRange(keepCommittedMovement: true);
        SetSupplyPreviewVisible(false);
        SetSupplyQueuedPreviewVisible(false);
    }

    private void RebuildSupplyCandidateEntries()
    {
        supplyCandidateEntries.Clear();
        supplyCandidateIndexByCell.Clear();
        if (cachedPodeSuprirTargets == null)
            return;

        int number = 0;
        for (int i = 0; i < cachedPodeSuprirTargets.Count; i++)
        {
            PodeSuprirOption option = cachedPodeSuprirTargets[i];
            if (option == null || option.targetUnit == null || IsSupplyTargetAlreadyQueued(option.targetUnit))
                continue;
            if (!option.targetUnit.gameObject.activeInHierarchy || option.targetUnit.IsEmbarked)
                continue;

            Vector3Int cell = option.targetUnit.CurrentCellPosition;
            cell.z = 0;
            number++;
            supplyCandidateEntries.Add(new SupplyCandidateEntry
            {
                option = option,
                targetUnit = option.targetUnit,
                selectionNumber = number,
                cell = cell,
                label = $"{option.targetUnit.name} ({cell.x},{cell.y})"
            });
            supplyCandidateIndexByCell[cell] = supplyCandidateEntries.Count - 1;
        }
    }

    private void PaintSupplyCandidateOptions()
    {
        ClearMovementRange(keepCommittedMovement: true);
        if (rangeMapTilemap == null || rangeOverlayTile == null || selectedUnit == null)
            return;

        Color teamColor = TeamUtils.GetColor(selectedUnit.TeamId);
        Color overlayColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(movementRangeAlpha));
        for (int i = 0; i < supplyCandidateEntries.Count; i++)
        {
            SupplyCandidateEntry candidate = supplyCandidateEntries[i];
            if (candidate == null)
                continue;

            Vector3Int cell = candidate.cell;
            cell.z = 0;
            rangeMapTilemap.SetTile(cell, rangeOverlayTile);
            rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
            rangeMapTilemap.SetColor(cell, overlayColor);
            paintedRangeCells.Add(cell);
            paintedRangeLookup.Add(cell);
        }
    }

    private bool TryResolveSupplyCursorMove(Vector3Int currentCell, Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = currentCell;
        if (cursorState != CursorState.Suprindo || scannerPromptStep != ScannerPromptStep.MergeParticipantSelect || supplyCandidateEntries.Count <= 0)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        if (supplySelectedCandidateIndex < 0 || supplySelectedCandidateIndex >= supplyCandidateEntries.Count)
            SyncSupplySelectedCandidateFromCursor();

        int currentIndex = supplySelectedCandidateIndex;
        if (currentIndex < 0 || currentIndex >= supplyCandidateEntries.Count)
            currentIndex = 0;

        int nextIndex = (currentIndex + step + supplyCandidateEntries.Count) % supplyCandidateEntries.Count;
        SupplyCandidateEntry next = supplyCandidateEntries[nextIndex];
        if (next == null)
            return false;

        supplySelectedCandidateIndex = nextIndex;
        resolvedCell = next.cell;
        UpdateSupplyPreviewFromCurrentContext();
        return true;
    }

    private void SyncSupplySelectedCandidateFromCursor()
    {
        if (cursorController == null)
            return;
        Vector3Int cell = cursorController.CurrentCell;
        cell.z = 0;
        if (!supplyCandidateIndexByCell.TryGetValue(cell, out int index))
            return;
        if (index < 0 || index >= supplyCandidateEntries.Count)
            return;
        supplySelectedCandidateIndex = index;
    }

    private bool TryGetSelectedSupplyCandidate(out SupplyCandidateEntry entry)
    {
        entry = null;
        if (supplySelectedCandidateIndex < 0 || supplySelectedCandidateIndex >= supplyCandidateEntries.Count)
            return false;
        entry = supplyCandidateEntries[supplySelectedCandidateIndex];
        return entry != null && entry.targetUnit != null && entry.option != null;
    }

    private int CountRemainingSupplyCandidates()
    {
        int count = 0;
        for (int i = 0; i < supplyCandidateEntries.Count; i++)
        {
            SupplyCandidateEntry entry = supplyCandidateEntries[i];
            if (entry == null || entry.targetUnit == null || IsSupplyTargetAlreadyQueued(entry.targetUnit))
                continue;
            count++;
        }
        return count;
    }

    private bool IsSupplyTargetAlreadyQueued(UnitManager target)
    {
        for (int i = 0; i < supplyQueuedOrders.Count; i++)
        {
            PodeSuprirOption queued = supplyQueuedOrders[i];
            if (queued != null && queued.targetUnit == target)
                return true;
        }
        return false;
    }

    private void UpdateSupplyPreviewFromCurrentContext()
    {
        if (cursorState != CursorState.Suprindo || selectedUnit == null)
        {
            supplyPreviewLastTarget = null;
            supplyPreviewPathPoints.Clear();
            supplyPreviewPathLength = 0f;
            SetSupplyPreviewVisible(false);
            return;
        }

        // Espelha a subetapa de confirmacao da fusao: preview apenas no "confirmar unidade".
        if (scannerPromptStep != ScannerPromptStep.MergeConfirm)
        {
            supplyPreviewLastTarget = null;
            supplyPreviewPathPoints.Clear();
            supplyPreviewPathLength = 0f;
            SetSupplyPreviewVisible(false);
            return;
        }

        UnitManager target = null;
        if (TryGetSelectedSupplyCandidate(out SupplyCandidateEntry selected))
            target = selected.targetUnit;

        if (target == null)
        {
            supplyPreviewLastTarget = null;
            supplyPreviewPathPoints.Clear();
            supplyPreviewPathLength = 0f;
            SetSupplyPreviewVisible(false);
            return;
        }

        if (supplyPreviewLastTarget == target && supplyPreviewPathLength > 0.0001f && supplyPreviewPathPoints.Count >= 2)
            return;

        Vector3 from = target.transform.position;
        Vector3 to = selectedUnit.transform.position;
        from.z = to.z;
        supplyPreviewPathPoints.Clear();
        supplyPreviewPathPoints.Add(from);
        supplyPreviewPathPoints.Add(to);
        supplyPreviewPathLength = ComputePathLength(supplyPreviewPathPoints);
        supplyPreviewHeadDistance = 0f;
        supplyPreviewLastTarget = target;
    }

    private void UpdateSupplyQueuePreviewAnimation()
    {
        if (supplyExecutionInProgress)
        {
            SetSupplyPreviewVisible(false);
            SetSupplyQueuedPreviewVisible(false);
            return;
        }

        UpdateSupplyPreviewFromCurrentContext();

        bool shouldShow =
            cursorState == CursorState.Suprindo &&
            (supplyQueuedOrders.Count > 0 || scannerPromptStep == ScannerPromptStep.MergeConfirm);
        if (!shouldShow)
        {
            SetSupplyPreviewVisible(false);
            SetSupplyQueuedPreviewVisible(false);
            return;
        }

        int segmentQuantities = Mathf.Max(1, GetMergeQueuePreviewSegmentQuantities());
        float previewMultiplier = GetMergeQueuePreviewMultiplier();
        float speed = Mathf.Max(0.2f, GetMergeQueuePreviewSegmentSpeed());
        float spacingMultiplier = Mathf.Max(0.2f, GetMergeQueuePreviewSegmentSpacingMultiplier());
        float segmentLen = Mathf.Max(0.08f, GetMirandoPreviewSegmentLength() * previewMultiplier);
        float width = Mathf.Max(0.02f, GetMirandoPreviewWidth() * previewMultiplier);
        Color baseColor = GetMirandoPreviewColor();
        Color color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(baseColor.a * 0.75f));

        // Preview persistente da fila (igual fusao): mantem linhas dos ja confirmados.
        for (int i = 0; i < supplyQueuePreviewTracks.Count; i++)
        {
            SupplyQueuePreviewTrack track = supplyQueuePreviewTracks[i];
            if (track == null)
                continue;

            EnsureSupplyQueuePreviewRenderers(track, i, segmentQuantities);
            if (track.renderers.Count == 0)
                continue;

            UnitManager target = track.target;
            if (target == null || !target.gameObject.activeInHierarchy || target == selectedUnit)
            {
                HideSupplyTrack(track);
                continue;
            }

            Vector3 from = target.transform.position;
            Vector3 to = selectedUnit.transform.position;
            from.z = to.z;
            track.pathPoints.Clear();
            track.pathPoints.Add(from);
            track.pathPoints.Add(to);
            track.pathLength = ComputePathLength(track.pathPoints);
            if (track.pathLength <= 0.0001f)
            {
                HideSupplyTrack(track);
                continue;
            }

            float cycleLenQueued = track.pathLength + segmentLen;
            track.headDistance += speed * Time.deltaTime;
            if (track.headDistance > cycleLenQueued)
                track.headDistance = 0f;

            float spacing = (cycleLenQueued / segmentQuantities) * spacingMultiplier;
            for (int segmentIndex = 0; segmentIndex < segmentQuantities; segmentIndex++)
            {
                if (segmentIndex >= track.renderers.Count)
                    break;

                LineRenderer renderer = track.renderers[segmentIndex];
                if (renderer == null)
                    continue;

                float segmentHeadDistance = track.headDistance - (spacing * segmentIndex);
                while (segmentHeadDistance < 0f)
                    segmentHeadDistance += cycleLenQueued;
                while (segmentHeadDistance > cycleLenQueued)
                    segmentHeadDistance -= cycleLenQueued;

                float startDist = Mathf.Max(0f, segmentHeadDistance - segmentLen);
                float endDist = Mathf.Min(segmentHeadDistance, track.pathLength);
                if (endDist <= startDist + 0.0001f)
                {
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                    continue;
                }

                BuildPathSegmentPointsFrom(track.pathPoints, startDist, endDist, track.tempSegmentPoints);
                if (track.tempSegmentPoints.Count < 2)
                {
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                    continue;
                }

                renderer.startWidth = width;
                renderer.endWidth = width;
                renderer.startColor = color;
                renderer.endColor = color;
                renderer.positionCount = track.tempSegmentPoints.Count;
                for (int p = 0; p < track.tempSegmentPoints.Count; p++)
                    renderer.SetPosition(p, track.tempSegmentPoints[p]);
                renderer.enabled = true;
            }
        }

        // Preview de confirmacao (candidato atual -> supridor)
        bool showConfirmPreview =
            scannerPromptStep == ScannerPromptStep.MergeConfirm &&
            supplyPreviewPathLength > 0.0001f &&
            supplyPreviewPathPoints.Count >= 2;
        if (!showConfirmPreview)
        {
            SetSupplyPreviewVisible(false);
            return;
        }

        EnsureSupplyPreviewRenderers(segmentQuantities);
        if (supplyPreviewRenderers.Count == 0)
            return;

        float cycleLen = supplyPreviewPathLength + segmentLen;
        supplyPreviewHeadDistance += speed * Time.deltaTime;
        if (supplyPreviewHeadDistance > cycleLen)
            supplyPreviewHeadDistance = 0f;

        float confirmSpacing = (cycleLen / segmentQuantities) * spacingMultiplier;
        for (int segmentIndex = 0; segmentIndex < segmentQuantities; segmentIndex++)
        {
            if (segmentIndex >= supplyPreviewRenderers.Count)
                break;

            LineRenderer renderer = supplyPreviewRenderers[segmentIndex];
            if (renderer == null)
                continue;

            float segmentHeadDistance = supplyPreviewHeadDistance - (confirmSpacing * segmentIndex);
            while (segmentHeadDistance < 0f)
                segmentHeadDistance += cycleLen;
            while (segmentHeadDistance > cycleLen)
                segmentHeadDistance -= cycleLen;

            float startDist = Mathf.Max(0f, segmentHeadDistance - segmentLen);
            float endDist = Mathf.Min(segmentHeadDistance, supplyPreviewPathLength);
            if (endDist <= startDist + 0.0001f)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            BuildPathSegmentPointsFrom(supplyPreviewPathPoints, startDist, endDist, supplyPreviewSegmentPoints);
            if (supplyPreviewSegmentPoints.Count < 2)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            renderer.startWidth = width;
            renderer.endWidth = width;
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.positionCount = supplyPreviewSegmentPoints.Count;
            for (int p = 0; p < supplyPreviewSegmentPoints.Count; p++)
                renderer.SetPosition(p, supplyPreviewSegmentPoints[p]);
            renderer.enabled = true;
        }
    }

    private void EnsureSupplyPreviewRenderers(int count)
    {
        int desired = Mathf.Max(1, count);
        while (supplyPreviewRenderers.Count < desired)
        {
            int segmentIndex = supplyPreviewRenderers.Count;
            string rendererName = $"SupplyConfirmPreviewLine_{segmentIndex + 1}";
            GameObject go = new GameObject(rendererName);
            go.transform.SetParent(transform, false);
            LineRenderer renderer = go.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.numCapVertices = 2;
            renderer.numCornerVertices = 2;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material previewMaterial = GetMirandoPreviewMaterial();
            renderer.material = previewMaterial != null ? previewMaterial : new Material(Shader.Find("Sprites/Default"));
            int sortingLayerId = GetMirandoPreviewSortingLayerId();
            if (sortingLayerId != 0)
                renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = Mathf.Max(0, GetMirandoPreviewSortingOrder() - 1);
            renderer.enabled = false;
            supplyPreviewRenderers.Add(renderer);
        }
    }

    private void RebuildSupplyQueuePreviewTracks()
    {
        while (supplyQueuePreviewTracks.Count < supplyQueuedOrders.Count)
            supplyQueuePreviewTracks.Add(new SupplyQueuePreviewTrack());

        for (int i = 0; i < supplyQueuePreviewTracks.Count; i++)
        {
            SupplyQueuePreviewTrack track = supplyQueuePreviewTracks[i];
            if (track == null)
                continue;

            UnitManager newTarget = i < supplyQueuedOrders.Count && supplyQueuedOrders[i] != null ? supplyQueuedOrders[i].targetUnit : null;
            if (track.target != newTarget)
                track.headDistance = 0f;
            track.target = newTarget;
            track.pathPoints.Clear();
            track.tempSegmentPoints.Clear();
            track.pathLength = 0f;
        }
    }

    private void EnsureSupplyQueuePreviewRenderers(SupplyQueuePreviewTrack track, int trackIndex, int count)
    {
        if (track == null)
            return;

        int desired = Mathf.Max(1, count);
        while (track.renderers.Count < desired)
        {
            int segmentIndex = track.renderers.Count;
            string rendererName = $"SupplyQueuePreviewLine_{trackIndex + 1}_{segmentIndex + 1}";
            GameObject go = new GameObject(rendererName);
            go.transform.SetParent(transform, false);
            LineRenderer renderer = go.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.numCapVertices = 2;
            renderer.numCornerVertices = 2;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material previewMaterial = GetMirandoPreviewMaterial();
            renderer.material = previewMaterial != null ? previewMaterial : new Material(Shader.Find("Sprites/Default"));
            int sortingLayerId = GetMirandoPreviewSortingLayerId();
            if (sortingLayerId != 0)
                renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = Mathf.Max(0, GetMirandoPreviewSortingOrder() - 1);
            renderer.enabled = false;
            track.renderers.Add(renderer);
        }
    }

    private void HideSupplyTrack(SupplyQueuePreviewTrack track)
    {
        if (track == null || track.renderers == null)
            return;

        for (int i = 0; i < track.renderers.Count; i++)
        {
            LineRenderer renderer = track.renderers[i];
            if (renderer == null)
                continue;
            renderer.positionCount = 0;
            renderer.enabled = false;
        }
    }

    private void SetSupplyPreviewVisible(bool visible)
    {
        for (int i = 0; i < supplyPreviewRenderers.Count; i++)
        {
            LineRenderer renderer = supplyPreviewRenderers[i];
            if (renderer == null)
                continue;

            if (!visible)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            renderer.enabled = true;
        }
    }

    private void SetSupplyQueuedPreviewVisible(bool visible)
    {
        for (int i = 0; i < supplyQueuePreviewTracks.Count; i++)
        {
            SupplyQueuePreviewTrack track = supplyQueuePreviewTracks[i];
            if (track == null || track.renderers == null)
                continue;

            for (int r = 0; r < track.renderers.Count; r++)
            {
                LineRenderer renderer = track.renderers[r];
                if (renderer == null)
                    continue;

                if (!visible)
                {
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                    continue;
                }

                renderer.enabled = true;
            }
        }
    }

    private static SupplyServiceLayerPlan ResolveSupplyServiceLayerPlanForExecution(UnitManager supplier, List<PodeSuprirOption> queue)
    {
        if (supplier == null)
            return SupplyServiceLayerPlan.DefaultSameDomain;
        if (!supplier.TryGetUnitData(out UnitData supplierData) || supplierData == null)
            return SupplyServiceLayerPlan.DefaultSameDomain;
        if (queue == null || queue.Count <= 0)
            return SupplyServiceLayerPlan.DefaultSameDomain;

        Domain supplierDomain = supplier.GetDomain();
        HeightLevel supplierHeight = supplier.GetHeightLevel();
        bool hasDifferentEffectiveLayer = false;
        bool hasAirTargets = false;
        bool needsAirLow = false;
        bool needsNavalSurface = false;

        for (int i = 0; i < queue.Count; i++)
        {
            PodeSuprirOption option = queue[i];
            if (option == null || option.targetUnit == null)
                continue;

            UnitManager target = option.targetUnit;
            Domain effectiveDomain = target.GetDomain();
            HeightLevel effectiveHeight = target.GetHeightLevel();
            if (option.forceLandBeforeSupply)
            {
                effectiveDomain = supplierDomain;
                effectiveHeight = supplierHeight;
            }
            if (option.forceTakeoffBeforeSupply || option.forceSurfaceBeforeSupply)
            {
                effectiveDomain = option.plannedServiceDomain;
                effectiveHeight = option.plannedServiceHeight;
            }

            if (effectiveDomain == Domain.Air)
                hasAirTargets = true;
            if (effectiveDomain != supplierDomain || effectiveHeight != supplierHeight)
                hasDifferentEffectiveLayer = true;
            if (effectiveDomain == Domain.Air && effectiveHeight == HeightLevel.AirLow)
                needsAirLow = true;
            if (effectiveDomain == Domain.Naval && effectiveHeight == HeightLevel.Surface)
                needsNavalSurface = true;
        }

        if (!hasDifferentEffectiveLayer)
            return SupplyServiceLayerPlan.DefaultSameDomain;

        bool supportsAirLow = SupportsSupplierOperationLayer(supplierData, Domain.Air, HeightLevel.AirLow);
        bool supportsNavalSurface = SupportsSupplierOperationLayer(supplierData, Domain.Naval, HeightLevel.Surface);

        if (hasAirTargets && supportsAirLow)
            return SupplyServiceLayerPlan.AirLow;
        if (needsAirLow && supportsAirLow)
            return SupplyServiceLayerPlan.AirLow;
        if (needsNavalSurface && supportsNavalSurface)
            return SupplyServiceLayerPlan.NavalSurface;
        if (supportsAirLow)
            return SupplyServiceLayerPlan.AirLow;
        if (supportsNavalSurface)
            return SupplyServiceLayerPlan.NavalSurface;

        return SupplyServiceLayerPlan.DefaultSameDomain;
    }

    private static bool SupportsSupplierOperationLayer(UnitData supplierData, Domain domain, HeightLevel height)
    {
        if (supplierData == null || supplierData.supplierOperationDomains == null)
            return false;

        for (int i = 0; i < supplierData.supplierOperationDomains.Count; i++)
        {
            SupplierOperationDomain mode = supplierData.supplierOperationDomains[i];
            if (mode.domain == domain && mode.heightLevel == height)
                return true;
        }

        return false;
    }

    private static bool TryGetSupplyLayerFromPlan(SupplyServiceLayerPlan plan, out Domain domain, out HeightLevel height)
    {
        domain = Domain.Land;
        height = HeightLevel.Surface;
        switch (plan)
        {
            case SupplyServiceLayerPlan.AirLow:
                domain = Domain.Air;
                height = HeightLevel.AirLow;
                return true;
            case SupplyServiceLayerPlan.NavalSurface:
                domain = Domain.Naval;
                height = HeightLevel.Surface;
                return true;
            default:
                return false;
        }
    }

    private void LogSupplyCandidateSelectionPanel()
    {
        string text = $"[Suprimento] Candidatos elegiveis: {supplyCandidateEntries.Count}\n";
        text += "Use numero (1..9) ou mova o cursor entre os hexes pintados.\n";
        text += "Enter confirma o candidato atual.\n";
        if (supplyQueuedOrders.Count > 0)
            text += "Digite 0 para executar as ordens em fila. ESC desfaz a ultima ordem.\n";
        else
            text += "ESC volta para sensores.\n";

        for (int i = 0; i < supplyCandidateEntries.Count; i++)
            text += $"{supplyCandidateEntries[i].selectionNumber}. {supplyCandidateEntries[i].label}\n";

        Debug.Log(text);
    }

    private void LogSupplyConfirmPrompt(SupplyCandidateEntry entry)
    {
        if (entry == null || entry.targetUnit == null)
            return;
        Debug.Log($"[Suprimento] Confirmar adicionar {entry.targetUnit.name} na fila? (Enter=sim, ESC=voltar)");
    }

    private bool ConsumeSupplySuppressDefaultConfirmSfxOnce()
    {
        if (!supplySuppressDefaultConfirmSfxOnce)
            return false;
        supplySuppressDefaultConfirmSfxOnce = false;
        return true;
    }
}
