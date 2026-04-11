using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIPlayerController
{
    private struct AIShoppingCapabilityDemand
    {
        public AIPlanCapability capability;
        public int missingCount;
        public int totalDemand;
    }

    private struct AIShoppingDemandSummary
    {
        public int missingCapture;
        public int missingEscort;
        public int missingFireSupport;
        public int missingTransport;
        public int missingLogistics;
        public int totalCaptureDemand;
        public int totalEscortDemand;
        public int totalFireSupportDemand;
        public int totalTransportDemand;
        public int totalLogisticsDemand;
        public bool hasCriticalCaptureGap;
        public int turnNumber;
        public int friendlyUnitCount;
        public int activeProductionBuildings;

        public int TotalMissingCount => missingCapture + missingEscort + missingFireSupport + missingTransport + missingLogistics;
    }

    private struct AIShoppingCandidate
    {
        public UnitData unit;
        public int offerIndex;
        public int cost;
        public int groupPriority;
        public bool affordableNow;
        public int turnsToAfford;
        public bool fromFallback;
        public string sourceLabel;
    }

    private void UpdateShoppingDebugView(TeamId team, AIShoppingTurnPlan shoppingPlan)
    {
        if (shoppingDebugView == null)
            shoppingDebugView = new List<TeamShoppingDebugView>();

        int index = -1;
        for (int i = 0; i < shoppingDebugView.Count; i++)
        {
            if (shoppingDebugView[i] != null && shoppingDebugView[i].team == team)
            {
                index = i;
                break;
            }
        }

        TeamShoppingDebugView view = new TeamShoppingDebugView
        {
            team = team,
            currentStanceName = currentStance.ToString(),
            turnNumber = shoppingPlan != null ? shoppingPlan.turnNumber : 0,
            totalMoney = shoppingPlan != null ? shoppingPlan.budget.totalMoney : 0,
            reservedMoney = shoppingPlan != null ? shoppingPlan.budget.reservedMoney : 0,
            freeMoney = shoppingPlan != null ? shoppingPlan.budget.freeMoney : 0,
            saveTargetMoney = shoppingPlan != null ? shoppingPlan.budget.saveTargetMoney : 0,
            strategicReserveMoney = shoppingPlan != null ? shoppingPlan.budget.strategicReserveMoney : 0,
            massFloorBlocked = shoppingPlan != null && shoppingPlan.budget.massFloorBlocked,
            massFloorCurrentUnits = shoppingPlan != null ? shoppingPlan.friendlyUnitCount : 0,
            massFloorRequiredUnits = shoppingPlan != null ? shoppingPlan.massFloorRequiredUnits : 0,
            massFloorReason = shoppingPlan != null ? shoppingPlan.massFloorReason : null,
            hasCriticalCaptureGap = shoppingPlan != null && shoppingPlan.hasCriticalCaptureGap,
            strategicSaveActive = shoppingPlan != null && shoppingPlan.strategicSaveActive,
            strategicSaveUnitId = shoppingPlan != null ? shoppingPlan.strategicSaveUnitId : null,
            strategicSaveSourceLabel = shoppingPlan != null ? shoppingPlan.strategicSaveSourceLabel : null,
            strategicSaveCost = shoppingPlan != null ? shoppingPlan.strategicSaveCost : 0,
            strategicSaveTurnsToAfford = shoppingPlan != null ? shoppingPlan.strategicSaveTurnsToAfford : 0,
            strategicSaveDeferredOrders = shoppingPlan != null ? shoppingPlan.strategicSaveDeferredOrders : 0,
            strategicSaveReason = shoppingPlan != null ? shoppingPlan.strategicSaveReason : null,
        };

        if (shoppingPlan != null)
        {
            for (int i = 0; i < shoppingPlan.capabilityPressures.Count; i++)
            {
                AIShoppingCapabilityPressure pressure = shoppingPlan.capabilityPressures[i];
                if (pressure == null)
                    continue;

                view.capabilityPressures.Add(new ShoppingCapabilityPressureDebugView
                {
                    capability = pressure.capability.ToString(),
                    orderCount = pressure.orderCount,
                    basePressure = pressure.basePressure,
                    missingPressure = pressure.missingPressure,
                    riskPressure = pressure.riskPressure,
                    criticalPressure = pressure.criticalPressure,
                    dynamicPressure = pressure.dynamicPressure,
                    totalPressure = pressure.totalPressure,
                    criteriaSummary = pressure.criteriaSummary,
                    urgentLogisticsNeed = pressure.capability == AIPlanCapability.Logistics && shoppingPlan.urgentLogisticsNeed,
                    supplyChainPressureScore = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainPressureScore : 0,
                    supplyChainAvailableSuppliers = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainAvailableSuppliers : 0,
                    supplyChainTargetSuppliers = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainTargetSuppliers : 0,
                    supplyChainAdditionalSuppliersNeeded = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainAdditionalSuppliersNeeded : 0,
                    supplyChainPlansMissingLogistics = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainPlansMissingLogistics : 0,
                    supplyChainLowAutonomyUnits = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainLowAutonomyUnits : 0,
                    supplyChainOutOfAmmoUnits = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainOutOfAmmoUnits : 0,
                    supplyChainDamagedUnits = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainDamagedUnits : 0,
                    supplyChainFrontlineCriticalUnits = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainFrontlineCriticalUnits : 0,
                    supplyChainReason = pressure.capability == AIPlanCapability.Logistics ? shoppingPlan.supplyChainReason : null
                });
            }

            for (int i = 0; i < shoppingPlan.orders.Count; i++)
            {
                AIShoppingOrder order = shoppingPlan.orders[i];
                if (order == null)
                    continue;

                view.orders.Add(new ShoppingOrderDebugView
                {
                    orderId = order.orderId,
                    planLabel = order.planLabel,
                    capability = order.capability.ToString(),
                    remainingCount = order.remainingCount,
                    priorityScore = order.priorityScore,
                    critical = order.critical,
                    reason = order.reason
                });
            }

            for (int i = 0; i < shoppingPlan.constructionDecisions.Count; i++)
            {
                AIShoppingConstructionDecision decision = shoppingPlan.constructionDecisions[i];
                if (decision == null)
                    continue;

                view.decisions.Add(new ShoppingDecisionDebugView
                {
                    constructionLabel = decision.constructionLabel,
                    kind = decision.kind.ToString(),
                    plannedUnitId = decision.plannedUnitId,
                    targetIndex = decision.targetIndex,
                    cost = decision.cost,
                    usedSavingFallback = decision.usedSavingFallback,
                    planKey = decision.planKey,
                    capability = decision.capability.ToString(),
                    reason = decision.plannedReason
                });
            }
        }

        if (index >= 0)
            shoppingDebugView[index] = view;
        else
            shoppingDebugView.Add(view);
    }

    private IEnumerator Phase3_BuyUnits(TeamId aiTeam, AISnapshot snapshot)
    {
        if (turnStateManager == null)
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 3)} TurnStateManager nao encontrado, pulando");
            yield break;
        }

        int bought = 0;
        int savingFallbackPurchases = 0;
        AIShoppingManager shoppingManager = new AIShoppingManager();
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info.TeamId != aiTeam || !info.CanProduceUnits || info.Source == null)
                continue;

            yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(2f));

            AISnapshot current = TakeSnapshot(aiTeam, refreshStance: false);
            int currentMoney = matchController != null ? matchController.GetActualMoney(aiTeam) : 0;
            int incomePerTurn = matchController != null ? matchController.GetIncomePerTurn(aiTeam) : 0;
            AIData effectiveData = GetEffectiveAIData(aiTeam);
            AIDataMode mode = currentStance == AIStance.Defend ? effectiveData.defenseMode : effectiveData.attackMode;
            bool defenseMode = currentStance == AIStance.Defend;
            var plannerConfig = BuildPlannerRuntimeConfig(aiTeam);
            int maxVariablePlans = plannerConfig.MaxVariablePlans + plannerConfig.MaxBackupPlans;
            AIShoppingTurnPlan shoppingPlan = shoppingManager.BuildTurnPlan(aiTeam, current, mode, defenseMode, currentMoney, incomePerTurn, matchController != null ? matchController.CurrentTurn : 0, maxVariablePlans, savingFallbackPurchases);
            UpdateShoppingDebugView(aiTeam, shoppingPlan);

            if (!shoppingPlan.TryGetDecision(info.Source, out AIShoppingConstructionDecision shoppingDecision) || shoppingDecision == null)
            {
                if (aiLog) Debug.Log($"{T(aiTeam, 3)} sem compra planejada em {info.DisplayName} (saldo={currentMoney}) | motivo: sem decisao central de shopping");
                continue;
            }

            if (shoppingDecision.kind != AIShoppingDecisionKind.Buy)
            {
                if (aiLog) Debug.Log($"{T(aiTeam, 3)} sem compra planejada em {info.DisplayName} (saldo={currentMoney}) | motivo: {shoppingDecision.plannedReason}");
                continue;
            }

            int targetIndex = shoppingDecision.targetIndex;
            string plannedUnitId = shoppingDecision.plannedUnitId;
            string plannedReason = shoppingDecision.plannedReason;
            bool usedSavingFallback = shoppingDecision.usedSavingFallback;

            Vector3Int cell = info.Source.CurrentCellPosition;
            cell.z = 0;

            yield return StartCoroutine(turnStateManager.MoveCursorToCellWithAutomatedTravel(cell));
            bool openedShopping = turnStateManager.TryAutomatedEnterShoppingAtConstruction(info.Source);

            float selectDelay = turnStateManager != null ? turnStateManager.GetAutomatedConfirmDelay() : (AnimationManager.Instance != null ? AnimationManager.Instance.AIUnitSelectDelay : 0.12f);
            float shoppingNavDelay = turnStateManager != null ? turnStateManager.GetAutomatedShoppingNavDelay() : selectDelay;
            yield return new WaitForSeconds(selectDelay);

            if (!openedShopping)
            {
                if (aiLog) Debug.Log($"{T(aiTeam, 3)} falha ao abrir shopping em {info.DisplayName}; pulando compra ({plannedUnitId}).");
                turnStateManager.HandleCancel();
                yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(2f));
                continue;
            }

            int guard = 0;
            const int maxGuard = 256;
            while (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.ShoppingAndServices)
            {
                int currentIndex = turnStateManager.GetShoppingSelectedIndexForReplay();
                if (currentIndex >= targetIndex)
                    break;

                if (guard++ >= maxGuard)
                {
                    if (aiLog) Debug.Log($"{T(aiTeam, 3)} guarda de navegacao atingida (targetIndex={targetIndex})");
                    break;
                }

                bool moved = turnStateManager.TryResolveShoppingCursorMoveForReplay(Vector3Int.right);
                if (!moved)
                    moved = turnStateManager.TryResolveShoppingCursorMoveForReplay(new Vector3Int(0, -1, 0));
                if (!moved)
                {
                    if (aiLog) Debug.Log($"{T(aiTeam, 3)} falha ao navegar catalogo (current={currentIndex}, target={targetIndex})");
                    break;
                }

                if (shoppingNavDelay > 0f)
                    yield return new WaitForSeconds(shoppingNavDelay);
                yield return null;
            }

            bool success = turnStateManager.TryConfirmSelectedShoppingOptionForReplay();
            string posturaLabel = GetStanceLabelLower(currentStance);
            if (aiLog) Debug.Log($"{T(aiTeam, 3)} compra em {info.DisplayName}: {(success ? "OK" : "falhou")} ({plannedUnitId}) | motivo: {plannedReason} | postura: {posturaLabel}");
            if (success)
            {
                bought++;
                if (usedSavingFallback)
                    savingFallbackPurchases++;
            }

            yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(2f));
        }

        if (aiLog) Debug.Log($"{T(aiTeam, 3)} total comprado: {bought}");
    }

    private static bool TryGetBlockingShoppingOccupant(ConstructionManager construction, out UnitManager blockingUnit)
    {
        blockingUnit = null;
        if (construction == null)
            return false;

        Vector3Int constructionCell = construction.CurrentCellPosition;
        constructionCell.z = 0;

        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager unit = UnitManager.AllActive[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked || !unit.gameObject.activeInHierarchy)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            if (unitCell != constructionCell)
                continue;
            if (!IsBlockingUnitForConstructionShopping(unit))
                continue;

            blockingUnit = unit;
            return true;
        }

        return false;
    }

    private static bool IsBlockingUnitForConstructionShopping(UnitManager unit)
    {
        if (unit == null)
            return false;

        Domain domain = unit.GetDomain();
        HeightLevel height = unit.GetHeightLevel();
        if (height != HeightLevel.Surface)
            return false;

        return domain == Domain.Land || domain == Domain.Naval;
    }

    private bool TryResolveShoppingPlan(
        TeamId aiTeam,
        AISnapshot snapshot,
        ConstructionManager construction,
        int currentMoney,
        int incomePerTurn,
        int savingFallbackPurchasesThisTurn,
        out int targetIndex,
        out string plannedUnitId,
        out string plannedReason,
        out bool usedSavingFallback)
    {
        targetIndex = -1;
        plannedUnitId = null;
        plannedReason = "nenhum";
        usedSavingFallback = false;

        if (construction == null)
        {
            plannedReason = "construcao invalida";
            return false;
        }

        AIData effectiveData = GetEffectiveAIData(aiTeam);
        AIDataMode mode = currentStance == AIStance.Defend
            ? effectiveData.defenseMode
            : effectiveData.attackMode;

        var plannerConfig = BuildPlannerRuntimeConfig(aiTeam);
        int maxVariablePlans = plannerConfig.MaxVariablePlans + plannerConfig.MaxBackupPlans;
        bool defenseMode = currentStance == AIStance.Defend;
        return TryResolveShoppingPlanFromMode(snapshot, construction, currentMoney, incomePerTurn, mode, defenseMode, savingFallbackPurchasesThisTurn, maxVariablePlans, out targetIndex, out plannedUnitId, out plannedReason, out usedSavingFallback);
    }

    private bool TryResolveShoppingPlanFromMode(
        AISnapshot snapshot,
        ConstructionManager construction,
        int currentMoney,
        int incomePerTurn,
        AIDataMode mode,
        bool defenseMode,
        int savingFallbackPurchasesThisTurn,
        int maxVariablePlans,
        out int targetIndex,
        out string plannedUnitId,
        out string plannedReason,
        out bool usedSavingFallback)
    {
        targetIndex = -1;
        plannedUnitId = null;
        plannedReason = "nenhum";
        usedSavingFallback = false;

        if (mode == null || mode.groups == null || mode.groups.Count == 0)
        {
            plannedReason = "modo sem grupos configurados";
            return false;
        }

        List<AIDataGroup> orderedGroups = GetGroupsByPriority(mode.groups);
        Dictionary<AIDataGroup, int> countsByGroup = CountFriendlyUnitsByConfiguredGroup(snapshot, orderedGroups);
        int totalUnits = snapshot != null && snapshot.FriendlyUnits != null ? snapshot.FriendlyUnits.Count : 0;
        int denominator = Mathf.Max(1, totalUnits);
        AIShoppingDemandSummary demandSummary = BuildShoppingDemandSummary(snapshot, snapshot != null ? snapshot.ActivePlans : null, maxVariablePlans);

        if (TryResolveMostUrgentCapabilityDemand(demandSummary, out AIPlanCapability urgentCapability, out string urgentReason))
        {
            if (TryResolveBestUnitForCapability(
                    construction,
                    mode,
                    orderedGroups,
                    urgentCapability,
                    currentMoney,
                    incomePerTurn,
                    demandSummary,
                    defenseMode,
                    out targetIndex,
                    out plannedUnitId,
                    out plannedReason,
                    out usedSavingFallback))
            {
                plannedReason = BuildShoppingReason("capability-buy", demandSummary, $"demanda={urgentCapability} | {urgentReason} | {plannedReason}");
                return true;
            }

            if (!string.IsNullOrWhiteSpace(plannedReason) && plannedReason.StartsWith("save=", System.StringComparison.Ordinal))
            {
                string saveReason = plannedReason;
                if (TryResolveSavingFallbackPurchase(construction, mode, currentMoney, defenseMode, savingFallbackPurchasesThisTurn, demandSummary, out targetIndex, out plannedUnitId, out string fallbackReason))
                {
                    usedSavingFallback = true;
                    plannedReason = BuildShoppingReason("capability-save-fallback", demandSummary, $"demanda={urgentCapability} | {urgentReason} | {saveReason} | {fallbackReason}");
                    return true;
                }

                plannedReason = BuildShoppingReason("capability-save", demandSummary, $"demanda={urgentCapability} | {urgentReason} | {saveReason}");
                return false;
            }
        }

        for (int i = 0; i < orderedGroups.Count; i++)
        {
            AIDataGroup group = orderedGroups[i];
            if (group == null)
                continue;

            float targetRatio = Mathf.Clamp01(group.targetPercentage / 100f);
            int currentCount = countsByGroup.TryGetValue(group, out int value) ? value : 0;
            float currentRatio = Mathf.Clamp01((float)currentCount / denominator);

            if (currentRatio + 0.0001f >= targetRatio)
                continue;

            if (TryResolveFirstAffordableFromGroup(construction, group, currentMoney, out targetIndex, out plannedUnitId))
            {
                plannedReason = BuildShoppingReason("composition-target", demandSummary, $"grupo={group.label} | composicao={currentCount}/{denominator} ({currentRatio:P0}) | alvo={targetRatio:P0} | prioridade={group.priority}");
                return true;
            }

            if (mode.saveForNextRound)
            {
                string saveReason = $"economizou para proxima rodada | grupo pendente={group.label} | composicao={currentCount}/{denominator} ({currentRatio:P0}) | alvo={targetRatio:P0}";
                if (TryResolveSavingFallbackPurchase(construction, mode, currentMoney, defenseMode, savingFallbackPurchasesThisTurn, demandSummary, out targetIndex, out plannedUnitId, out string fallbackReason))
                {
                    usedSavingFallback = true;
                    plannedReason = BuildShoppingReason("composition-save-fallback", demandSummary, $"{saveReason} | {fallbackReason}");
                    return true;
                }

                plannedReason = BuildShoppingReason("composition-save", demandSummary, saveReason);
                return false;
            }
        }

        for (int i = 0; i < orderedGroups.Count; i++)
        {
            AIDataGroup group = orderedGroups[i];
            if (group == null)
                continue;

            if (TryResolveFirstAffordableFromGroup(construction, group, currentMoney, out targetIndex, out plannedUnitId))
            {
                int currentCount = countsByGroup.TryGetValue(group, out int value) ? value : 0;
                float currentRatio = Mathf.Clamp01((float)currentCount / denominator);
                float targetRatio = Mathf.Clamp01(group.targetPercentage / 100f);
                plannedReason = BuildShoppingReason("composition-priority", demandSummary, $"grupo-prioridade={group.label} | composicao={currentCount}/{denominator} ({currentRatio:P0}) | alvo={targetRatio:P0} | prioridade={group.priority}");
                return true;
            }
        }

        bool allowFallbackPurchase = !(demandSummary.hasCriticalCaptureGap && !PassesEarlyGameMassFloor(demandSummary, demandSummary.turnNumber));
        if (allowFallbackPurchase && TryResolveFirstAffordableFromUnitList(construction, mode.fallbackUnits, currentMoney, out targetIndex, out plannedUnitId))
        {
            plannedReason = BuildShoppingReason("fallback-mode", demandSummary, $"fallback-modo={mode.label} | motivo=sem oferta acessivel nos grupos");
            return true;
        }

        if (mode.saveForNextRound)
        {
            plannedReason = BuildShoppingReason("fallback-save", demandSummary, allowFallbackPurchase
                ? "economizou para proxima rodada | sem ofertas acessiveis nos grupos/fallback"
                : "economizou para proxima rodada | fallback bloqueado pela massa minima");
            return false;
        }

        if (TryResolveAnyAffordableOffer(construction, currentMoney, out targetIndex, out plannedUnitId))
        {
            plannedReason = BuildShoppingReason("fallback-catalog", demandSummary, "fallback-catalogo | motivo=qualquer oferta acessivel");
            return true;
        }

        plannedReason = BuildShoppingReason("no-offer", demandSummary, "nenhuma oferta acessivel no catalogo");
        return false;
    }

    private static string BuildShoppingReason(string branch, AIShoppingDemandSummary summary, string detail)
    {
        return $"shopping-demand: CAP={summary.missingCapture}/{summary.totalCaptureDemand} ESC={summary.missingEscort}/{summary.totalEscortDemand} FS={summary.missingFireSupport}/{summary.totalFireSupportDemand} TRN={summary.missingTransport}/{summary.totalTransportDemand} LOG={summary.missingLogistics}/{summary.totalLogisticsDemand} crit={(summary.hasCriticalCaptureGap ? 1 : 0)} units={summary.friendlyUnitCount} prod={summary.activeProductionBuildings} | branch={branch} | {detail}";
    }

    private AIShoppingDemandSummary BuildShoppingDemandSummary(
        AISnapshot snapshot,
        IReadOnlyList<AIPlanIntent> plans,
        int maxVariablePlans)
    {
        AIShoppingDemandSummary summary = new AIShoppingDemandSummary
        {
            turnNumber = matchController != null ? matchController.CurrentTurn : 0,
            friendlyUnitCount = snapshot != null && snapshot.FriendlyUnits != null ? snapshot.FriendlyUnits.Count : 0,
            activeProductionBuildings = CountActiveProductionBuildings(snapshot)
        };

        if (plans == null)
            return summary;

        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent intent = plans[i];
            if (intent == null)
                continue;

            int desiredCapture = Mathf.Max(intent.DesiredCaptureCount, intent.HasCaptureTarget ? CountOutstandingCaptureTargets(snapshot, intent.Sector) : 0);
            int desiredEscort = Mathf.Max(0, intent.DesiredEscortCount);
            int desiredArtillery = Mathf.Max(0, intent.DesiredArtilleryCount);
            int desiredTransport = Mathf.Max(0, intent.DesiredTransportCount);
            int desiredSupport = Mathf.Max(0, intent.DesiredSupportCount);

            int assignedCapture = CountAssignedRole(intent, AIPlanRole.Capture);
            int assignedEscort = CountAssignedRole(intent, AIPlanRole.Escort);
            int assignedArtillery = CountAssignedRole(intent, AIPlanRole.Artillery);
            int assignedSupport = CountAssignedRole(intent, AIPlanRole.Support);

            summary.totalCaptureDemand += desiredCapture;
            summary.totalEscortDemand += desiredEscort;
            summary.totalFireSupportDemand += desiredArtillery;
            summary.totalTransportDemand += desiredTransport;
            summary.totalLogisticsDemand += desiredSupport;

            summary.missingCapture += Mathf.Max(0, desiredCapture - assignedCapture);
            summary.missingEscort += Mathf.Max(0, desiredEscort - assignedEscort);
            summary.missingFireSupport += Mathf.Max(0, desiredArtillery - assignedArtillery);
            summary.missingTransport += desiredTransport;
            summary.missingLogistics += Mathf.Max(0, desiredSupport - assignedSupport);

            if (desiredCapture > 0 && assignedCapture <= 0 && !IsProtectIntent(intent))
                summary.hasCriticalCaptureGap = true;
        }

        return summary;
    }

    private static bool TryResolveMostUrgentCapabilityDemand(
        AIShoppingDemandSummary summary,
        out AIPlanCapability capability,
        out string reason)
    {
        capability = AIPlanCapability.Capture;
        reason = string.Empty;

        if (summary.hasCriticalCaptureGap && summary.missingCapture > 0)
        {
            capability = AIPlanCapability.Capture;
            reason = $"critical-capture-gap={summary.missingCapture}";
            return true;
        }

        List<AIShoppingCapabilityDemand> demands = new List<AIShoppingCapabilityDemand>
        {
            new AIShoppingCapabilityDemand { capability = AIPlanCapability.Capture, missingCount = summary.missingCapture, totalDemand = summary.totalCaptureDemand },
            new AIShoppingCapabilityDemand { capability = AIPlanCapability.Escort, missingCount = summary.missingEscort, totalDemand = summary.totalEscortDemand },
            new AIShoppingCapabilityDemand { capability = AIPlanCapability.FireSupport, missingCount = summary.missingFireSupport, totalDemand = summary.totalFireSupportDemand },
            new AIShoppingCapabilityDemand { capability = AIPlanCapability.Transport, missingCount = summary.missingTransport, totalDemand = summary.totalTransportDemand },
            new AIShoppingCapabilityDemand { capability = AIPlanCapability.Logistics, missingCount = summary.missingLogistics, totalDemand = summary.totalLogisticsDemand }
        };

        AIShoppingCapabilityDemand best = default;
        bool found = false;
        for (int i = 0; i < demands.Count; i++)
        {
            AIShoppingCapabilityDemand candidate = demands[i];
            if (candidate.missingCount <= 0)
                continue;

            int candidateScore = GetCapabilityUrgencyScore(candidate);
            int bestScore = found ? GetCapabilityUrgencyScore(best) : int.MinValue;
            if (!found || candidateScore > bestScore)
            {
                best = candidate;
                found = true;
            }
        }

        if (!found)
            return false;

        capability = best.capability;
        reason = $"missing={best.missingCount}/{best.totalDemand}";
        return true;
    }

    private static int GetCapabilityUrgencyScore(AIShoppingCapabilityDemand demand)
    {
        if (demand.missingCount <= 0)
            return int.MinValue;

        int basePriority = demand.capability == AIPlanCapability.Capture ? 40000
            : demand.capability == AIPlanCapability.Escort ? 30000
            : demand.capability == AIPlanCapability.FireSupport ? 20000
            : demand.capability == AIPlanCapability.Transport ? 14000
            : 10000;

        return basePriority + (demand.missingCount * 500) + Mathf.Max(0, demand.totalDemand);
    }

    private bool TryResolveBestUnitForCapability(
        ConstructionManager construction,
        AIDataMode mode,
        IReadOnlyList<AIDataGroup> orderedGroups,
        AIPlanCapability capability,
        int currentMoney,
        int incomePerTurn,
        AIShoppingDemandSummary summary,
        bool defenseMode,
        out int targetIndex,
        out string plannedUnitId,
        out string plannedReason,
        out bool usedSavingFallback)
    {
        targetIndex = -1;
        plannedUnitId = null;
        plannedReason = $"demanda={capability} | sem-oferta-compativel";
        usedSavingFallback = false;

        List<AIShoppingCandidate> candidates = CollectCapabilityCandidatesFromGroups(construction, orderedGroups, capability, currentMoney, incomePerTurn);
        if (candidates.Count <= 0)
        {
            candidates = CollectCapabilityCandidatesFromFallback(construction, mode, capability, currentMoney, incomePerTurn);
            if (candidates.Count > 0)
                usedSavingFallback = true;
        }

        if (candidates.Count <= 0)
            return false;

        AIShoppingCandidate? bestAffordableNow = null;
        AIShoppingCandidate? bestPreferredFuture = null;
        for (int i = 0; i < candidates.Count; i++)
        {
            AIShoppingCandidate candidate = candidates[i];
            if (!bestPreferredFuture.HasValue || IsCandidateBetter(candidate, bestPreferredFuture.Value))
                bestPreferredFuture = candidate;
            if (candidate.affordableNow && (!bestAffordableNow.HasValue || IsCandidateBetter(candidate, bestAffordableNow.Value)))
                bestAffordableNow = candidate;
        }

        if (mode.saveForNextRound && bestPreferredFuture.HasValue && ShouldSaveForCapabilityPurchase(bestAffordableNow, bestPreferredFuture.Value, currentMoney, incomePerTurn, summary, defenseMode, summary.turnNumber))
        {
            plannedReason = $"save={bestPreferredFuture.Value.unit.id} | demanda={capability} | turns={bestPreferredFuture.Value.turnsToAfford} | massa-minima-ok";
            usedSavingFallback = false;
            return false;
        }

        if (bestAffordableNow.HasValue)
        {
            AIShoppingCandidate pick = bestAffordableNow.Value;
            targetIndex = pick.offerIndex;
            plannedUnitId = pick.unit != null ? pick.unit.id : null;
            plannedReason = $"unidade={plannedUnitId} | origem={pick.sourceLabel} | custo={pick.cost}";
            return true;
        }

        if (bestPreferredFuture.HasValue && mode.saveForNextRound && bestPreferredFuture.Value.turnsToAfford <= 2 && PassesEarlyGameMassFloor(summary, summary.turnNumber))
        {
            plannedReason = $"save={bestPreferredFuture.Value.unit.id} | demanda={capability} | turns={bestPreferredFuture.Value.turnsToAfford} | aguardando-melhor-unidade";
            usedSavingFallback = false;
            return false;
        }

        plannedReason = $"demanda={capability} | sem-oferta-acessivel";
        return false;
    }

    private static bool ShouldSaveForCapabilityPurchase(
        AIShoppingCandidate? bestAffordableNow,
        AIShoppingCandidate bestPreferredFuture,
        int currentMoney,
        int incomePerTurn,
        AIShoppingDemandSummary summary,
        bool defenseMode,
        int currentTurn)
    {
        if (bestPreferredFuture.unit == null)
            return false;
        if (bestPreferredFuture.affordableNow)
            return false;
        if (!PassesEarlyGameMassFloor(summary, currentTurn))
            return false;
        if (summary.hasCriticalCaptureGap)
            return false;
        if (summary.missingCapture > 0)
            return false;
        if (bestPreferredFuture.turnsToAfford > 2)
            return false;
        if (defenseMode && (summary.missingCapture > 0 || summary.missingEscort > 0))
            return false;
        if (!bestAffordableNow.HasValue)
            return true;
        return IsCandidateBetter(bestPreferredFuture, bestAffordableNow.Value);
    }

    private bool TryResolveSavingFallbackPurchase(
        ConstructionManager construction,
        AIDataMode mode,
        int currentMoney,
        bool defenseMode,
        int savingFallbackPurchasesThisTurn,
        AIShoppingDemandSummary summary,
        out int targetIndex,
        out string plannedUnitId,
        out string plannedReason)
    {
        targetIndex = -1;
        plannedUnitId = null;
        plannedReason = "fallback-save indisponivel";

        if (construction == null || mode == null || !mode.allowFallbackWhenSaving)
            return false;

        bool allowUnlimitedDefenseFallback = defenseMode && mode.buyFallbackWhenSavingOnDefenseMode;
        bool fallbackAlreadyConsumed = mode.allowFallbackWhenSavingButOnce && savingFallbackPurchasesThisTurn > 0;
        if (fallbackAlreadyConsumed && !allowUnlimitedDefenseFallback)
        {
            plannedReason = "fallback-save bloqueado | limite-por-turno";
            return false;
        }

        if (summary.hasCriticalCaptureGap && !PassesEarlyGameMassFloor(summary, summary.turnNumber))
        {
            plannedReason = "fallback-save bloqueado | massa-minima";
            return false;
        }

        if (!TryResolveFirstAffordableFromUnitList(construction, mode.fallbackUnits, currentMoney, out targetIndex, out plannedUnitId))
        {
            plannedReason = "fallback-save indisponivel | sem-oferta-acessivel";
            return false;
        }

        plannedReason = $"fallback-save | modo={mode.label}";
        return true;
    }

    private static bool PassesEarlyGameMassFloor(AIShoppingDemandSummary summary, int currentTurn)
    {
        if (currentTurn <= 4)
        {
            if (summary.friendlyUnitCount < (2 * Mathf.Max(1, summary.activeProductionBuildings)))
                return false;
            if (summary.friendlyUnitCount < summary.totalCaptureDemand + 2)
                return false;
            if (summary.hasCriticalCaptureGap)
                return false;
        }
        else if (currentTurn <= 6)
        {
            if (summary.friendlyUnitCount < summary.totalCaptureDemand + summary.totalEscortDemand)
                return false;
            if (summary.TotalMissingCount >= 2)
                return false;
        }

        return true;
    }

    private static int CountActiveProductionBuildings(AISnapshot snapshot)
    {
        if (snapshot == null || snapshot.KnownConstructions == null)
            return 0;

        int count = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Source == null)
                continue;
            if (info.TeamId != snapshot.AiTeam || !info.CanProduceUnits)
                continue;
            count++;
        }

        return count;
    }

    private static int CountAssignedRole(AIPlanIntent intent, AIPlanRole role)
    {
        if (intent == null || intent.Assignments == null)
            return 0;

        int count = 0;
        for (int i = 0; i < intent.Assignments.Count; i++)
        {
            AIPlanAssignment assignment = intent.Assignments[i];
            if (assignment != null && assignment.Role == role)
                count++;
        }

        return count;
    }

    private List<AIShoppingCandidate> CollectCapabilityCandidatesFromGroups(
        ConstructionManager construction,
        IReadOnlyList<AIDataGroup> orderedGroups,
        AIPlanCapability capability,
        int currentMoney,
        int incomePerTurn)
    {
        List<AIShoppingCandidate> candidates = new List<AIShoppingCandidate>();
        if (construction == null || orderedGroups == null)
            return candidates;

        for (int g = 0; g < orderedGroups.Count; g++)
        {
            AIDataGroup group = orderedGroups[g];
            if (group == null || group.specificUnits == null || group.specificUnits.Count == 0)
                continue;

            List<AIShoppingCandidate> groupCandidates = new List<AIShoppingCandidate>();
            for (int i = 0; i < group.specificUnits.Count; i++)
            {
                UnitData wantedUnit = group.specificUnits[i];
                if (wantedUnit == null || string.IsNullOrWhiteSpace(wantedUnit.id))
                    continue;
                if (!TryGetOfferIndex(construction, wantedUnit, out int offerIndex, out UnitData offer) || offer == null)
                    continue;
                if (offer.aiUnitProfile == null || !offer.aiUnitProfile.HasPlanCapability(capability, offer))
                    continue;
                if (capability == AIPlanCapability.Transport && !IsTransportShoppingCandidate(offer))
                    continue;

                int cost = Mathf.Max(0, offer.cost);
                groupCandidates.Add(new AIShoppingCandidate
                {
                    unit = offer,
                    offerIndex = offerIndex,
                    cost = cost,
                    groupPriority = group.priority,
                    affordableNow = cost <= currentMoney,
                    turnsToAfford = CalculateTurnsToAfford(cost, currentMoney, incomePerTurn),
                    fromFallback = false,
                    sourceLabel = $"grupo:{group.label}"
                });
            }

            if (groupCandidates.Count > 0)
                return groupCandidates;
        }

        return candidates;
    }

    private List<AIShoppingCandidate> CollectCapabilityCandidatesFromFallback(
        ConstructionManager construction,
        AIDataMode mode,
        AIPlanCapability capability,
        int currentMoney,
        int incomePerTurn)
    {
        List<AIShoppingCandidate> candidates = new List<AIShoppingCandidate>();
        if (construction == null || mode == null || mode.fallbackUnits == null)
            return candidates;

        for (int i = 0; i < mode.fallbackUnits.Count; i++)
        {
            UnitData wantedUnit = mode.fallbackUnits[i];
            if (wantedUnit == null || string.IsNullOrWhiteSpace(wantedUnit.id))
                continue;
            if (!TryGetOfferIndex(construction, wantedUnit, out int offerIndex, out UnitData offer) || offer == null)
                continue;
            if (offer.aiUnitProfile == null || !offer.aiUnitProfile.HasPlanCapability(capability, offer))
                continue;
            if (capability == AIPlanCapability.Transport && !IsTransportShoppingCandidate(offer))
                continue;

            int cost = Mathf.Max(0, offer.cost);
            candidates.Add(new AIShoppingCandidate
            {
                unit = offer,
                offerIndex = offerIndex,
                cost = cost,
                groupPriority = int.MaxValue,
                affordableNow = cost <= currentMoney,
                turnsToAfford = CalculateTurnsToAfford(cost, currentMoney, incomePerTurn),
                fromFallback = true,
                sourceLabel = "fallback"
            });
        }

        return candidates;
    }

    private static int CalculateTurnsToAfford(int cost, int currentMoney, int incomePerTurn)
    {
        int safeCost = Mathf.Max(0, cost);
        if (currentMoney >= safeCost)
            return 0;

        int safeIncome = Mathf.Max(1, incomePerTurn);
        return Mathf.CeilToInt((safeCost - currentMoney) / (float)safeIncome);
    }

    private static bool IsCandidateBetter(AIShoppingCandidate a, AIShoppingCandidate b)
    {
        if (a.cost != b.cost)
            return a.cost > b.cost;
        if (a.groupPriority != b.groupPriority)
            return a.groupPriority < b.groupPriority;
        return a.offerIndex < b.offerIndex;
    }

    private static bool IsTransportShoppingCandidate(UnitData data)
    {
        return data != null
            && data.isTransporter
            && data.domain == Domain.Land
            && data.aiUnitProfile != null
            && data.aiUnitProfile.HasPlanCapability(AIPlanCapability.Transport, data);
    }

    private static List<AIDataGroup> GetGroupsByPriority(IReadOnlyList<AIDataGroup> groups)
    {
        List<AIDataGroup> ordered = new List<AIDataGroup>();
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null)
                ordered.Add(groups[i]);
        }

        ordered.Sort((a, b) =>
        {
            int pa = a != null ? a.priority : int.MaxValue;
            int pb = b != null ? b.priority : int.MaxValue;
            int cmp = pa.CompareTo(pb);
            if (cmp != 0)
                return cmp;

            string la = a != null ? a.label : string.Empty;
            string lb = b != null ? b.label : string.Empty;
            return string.CompareOrdinal(la, lb);
        });

        return ordered;
    }

    private static Dictionary<AIDataGroup, int> CountFriendlyUnitsByConfiguredGroup(AISnapshot snapshot, List<AIDataGroup> orderedGroups)
    {
        Dictionary<AIDataGroup, int> counts = new Dictionary<AIDataGroup, int>();
        if (orderedGroups == null)
            return counts;

        for (int i = 0; i < orderedGroups.Count; i++)
            counts[orderedGroups[i]] = 0;

        if (snapshot == null || snapshot.FriendlyUnits == null)
            return counts;

        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager unit = snapshot.FriendlyUnits[i];
            if (unit == null || unit.IsDead || string.IsNullOrWhiteSpace(unit.UnitId))
                continue;

            for (int g = 0; g < orderedGroups.Count; g++)
            {
                AIDataGroup group = orderedGroups[g];
                if (group == null || group.specificUnits == null || group.specificUnits.Count == 0)
                    continue;

                if (!GroupContainsUnitId(group, unit.UnitId))
                    continue;

                counts[group] = counts[group] + 1;
                break;
            }
        }

        return counts;
    }

    private static bool GroupContainsUnitId(AIDataGroup group, string unitId)
    {
        if (group == null || group.specificUnits == null || string.IsNullOrWhiteSpace(unitId))
            return false;

        for (int i = 0; i < group.specificUnits.Count; i++)
        {
            UnitData candidate = group.specificUnits[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.id))
                continue;

            if (string.Equals(candidate.id, unitId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TryResolveFirstAffordableFromGroup(
        ConstructionManager construction,
        AIDataGroup group,
        int currentMoney,
        out int targetIndex,
        out string plannedUnitId)
    {
        targetIndex = -1;
        plannedUnitId = null;

        if (construction == null || group == null || group.specificUnits == null)
            return false;

        for (int i = 0; i < group.specificUnits.Count; i++)
        {
            UnitData wantedUnit = group.specificUnits[i];
            if (wantedUnit == null || string.IsNullOrWhiteSpace(wantedUnit.id))
                continue;

            if (!TryGetAffordableOfferIndex(construction, wantedUnit, currentMoney, out int index, out UnitData offer))
                continue;

            targetIndex = index;
            plannedUnitId = offer != null ? offer.id : wantedUnit.id;
            return true;
        }

        return false;
    }

    private static bool TryResolveFirstAffordableFromUnitList(
        ConstructionManager construction,
        IReadOnlyList<UnitData> fallbackUnits,
        int currentMoney,
        out int targetIndex,
        out string plannedUnitId)
    {
        targetIndex = -1;
        plannedUnitId = null;

        if (construction == null || fallbackUnits == null)
            return false;

        for (int i = 0; i < fallbackUnits.Count; i++)
        {
            UnitData wantedUnit = fallbackUnits[i];
            if (wantedUnit == null || string.IsNullOrWhiteSpace(wantedUnit.id))
                continue;

            if (!TryGetAffordableOfferIndex(construction, wantedUnit, currentMoney, out int index, out UnitData offer))
                continue;

            targetIndex = index;
            plannedUnitId = offer != null ? offer.id : wantedUnit.id;
            return true;
        }

        return false;
    }

    private static bool TryResolveAnyAffordableOffer(
        ConstructionManager construction,
        int currentMoney,
        out int targetIndex,
        out string plannedUnitId)
    {
        targetIndex = -1;
        plannedUnitId = null;

        if (construction == null)
            return false;

        IReadOnlyList<UnitData> offered = construction.OfferedUnits;
        if (offered == null)
            return false;

        for (int i = 0; i < offered.Count; i++)
        {
            UnitData unit = offered[i];
            if (unit == null)
                continue;

            int cost = Mathf.Max(0, unit.cost);
            if (cost > currentMoney)
                continue;

            targetIndex = i;
            plannedUnitId = unit.id;
            return true;
        }

        return false;
    }

    private static bool TryGetAffordableOfferIndex(ConstructionManager construction, UnitData wantedUnit, int currentMoney, out int index, out UnitData offer)
    {
        index = -1;
        offer = null;
        if (!TryGetOfferIndex(construction, wantedUnit, out int found, out UnitData unit) || unit == null)
            return false;

        int cost = Mathf.Max(0, unit.cost);
        if (currentMoney < cost)
            return false;

        index = found;
        offer = unit;
        return true;
    }

    private static bool TryGetOfferIndex(ConstructionManager construction, UnitData wantedUnit, out int index, out UnitData offer)
    {
        index = -1;
        offer = null;

        if (construction == null || wantedUnit == null)
            return false;

        IReadOnlyList<UnitData> offered = construction.OfferedUnits;
        if (offered == null || offered.Count == 0)
            return false;

        string wantedId = wantedUnit.id;
        for (int i = 0; i < offered.Count; i++)
        {
            UnitData unit = offered[i];
            if (unit == null)
                continue;

            if (ReferenceEquals(unit, wantedUnit))
            {
                index = i;
                offer = unit;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(wantedId) && !string.IsNullOrWhiteSpace(unit.id)
                && string.Equals(unit.id, wantedId, System.StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                offer = unit;
                return true;
            }
        }

        return false;
    }
}
