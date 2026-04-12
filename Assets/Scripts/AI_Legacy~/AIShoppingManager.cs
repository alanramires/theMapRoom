using System;
using System.Collections.Generic;
using UnityEngine;

public enum AIShoppingDecisionKind { None, Buy, Save, Blocked, NoOffer }

public sealed class AIShoppingBudgetState { public int totalMoney, incomePerTurn, reservedMoney, freeMoney, saveTargetMoney, strategicReserveMoney; public bool massFloorBlocked; }
public sealed class AIShoppingPlanDemand { public string planKey, planLabel; public ConstructionSector sector; public AIPlanCapability capability; public int desiredCount, assignedCount, missingCount, tacticalRiskScore; public bool hasCaptureTarget; }
public sealed class AIShoppingCapabilityDemand { public AIPlanCapability capability; public int totalDemand, totalAssigned, missingCount; }
public sealed class AIShoppingCapabilityPressure { public AIPlanCapability capability; public int orderCount, basePressure, missingPressure, riskPressure, criticalPressure, dynamicPressure, totalPressure; public string criteriaSummary; }
public sealed class AIShoppingOrder { public string orderId, planKey, planLabel, reason; public ConstructionSector sector; public AIPlanCapability capability; public int desiredCount, assignedCount, remainingCount, priorityScore; public bool critical, deferredByStrategicSave; }
public sealed class AIShoppingConstructionDecision { public ConstructionManager construction; public string constructionLabel, plannedUnitId, plannedReason, orderId, planKey; public AIShoppingDecisionKind kind; public int targetIndex = -1, cost; public bool usedSavingFallback; public AIPlanCapability capability; public UnitManager blockingUnit; }
public sealed class AIShoppingTurnPlan {
    public TeamId teamId; public int turnNumber, friendlyUnitCount, activeProductionBuildings, massFloorRequiredUnits; public bool hasCriticalCaptureGap, strategicSaveActive, urgentLogisticsNeed; public string strategicSaveUnitId, strategicSaveSourceLabel, strategicSaveReason, massFloorReason, supplyChainReason; public int strategicSaveCost, strategicSaveTurnsToAfford, strategicSaveDeferredOrders, supplyChainPressureScore, supplyChainAvailableSuppliers, supplyChainPlansMissingLogistics, supplyChainLowAutonomyUnits, supplyChainOutOfAmmoUnits, supplyChainDamagedUnits, supplyChainFrontlineCriticalUnits, supplyChainTargetSuppliers, supplyChainAdditionalSuppliersNeeded; public readonly AIShoppingBudgetState budget = new AIShoppingBudgetState();
    public readonly List<AIShoppingPlanDemand> planDemands = new List<AIShoppingPlanDemand>(); public readonly List<AIShoppingCapabilityDemand> capabilityDemands = new List<AIShoppingCapabilityDemand>(); public readonly List<AIShoppingCapabilityPressure> capabilityPressures = new List<AIShoppingCapabilityPressure>(); public readonly List<AIShoppingOrder> orders = new List<AIShoppingOrder>(); public readonly List<AIShoppingConstructionDecision> constructionDecisions = new List<AIShoppingConstructionDecision>();
    public bool TryGetDecision(ConstructionManager construction, out AIShoppingConstructionDecision decision) { decision = null; for (int i = 0; i < constructionDecisions.Count; i++) { var c = constructionDecisions[i]; if (c != null && c.construction == construction) { decision = c; return true; } } return false; }
}

public sealed class AIShoppingManager {
    private struct Candidate { public UnitData unit; public int offerIndex, cost, groupPriority, turnsToAfford, ladderRank, strategicScore; public bool affordableNow, fromFallback; public string sourceLabel; public AIPlanCapability capability; }
    private sealed class Ctx { public TeamId aiTeam; public AISnapshot snapshot; public AIDataMode mode; public bool defenseMode; public int money, income, turn, maxVariablePlans, savingFallbackPurchasesThisTurn, reservedMoney, reservedOrders, reservedFallbacks, saveTargetMoney, strategicReservedMoney, strategicDeferredOrders; public bool saveModeActive, strategicPurchaseCommitted; public string saveModeReason, strategicSaveReason; public Candidate? strategicSaveCandidate; public Candidate? compositionSaveCandidate; public AIConstructionInfo compositionSaveProducer; public bool compositionSaveCommitted; public List<AIDataGroup> groups = new List<AIDataGroup>(); public Dictionary<AIDataGroup, int> groupCounts = new Dictionary<AIDataGroup, int>(); public Dictionary<string, int> reservedUnitCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); public List<AIConstructionInfo> producers = new List<AIConstructionInfo>(); }

    public AIShoppingTurnPlan BuildTurnPlan(TeamId aiTeam, AISnapshot snapshot, AIDataMode mode, bool defenseMode, int currentMoney, int incomePerTurn, int currentTurn, int maxVariablePlans, int savingFallbackPurchasesThisTurn) {
        var plan = new AIShoppingTurnPlan { teamId = aiTeam, turnNumber = currentTurn, friendlyUnitCount = snapshot != null && snapshot.FriendlyUnits != null ? snapshot.FriendlyUnits.Count : 0, activeProductionBuildings = CountActiveProductionBuildings(snapshot, aiTeam) }; plan.budget.totalMoney = Mathf.Max(0, currentMoney); plan.budget.incomePerTurn = Mathf.Max(0, incomePerTurn); plan.budget.freeMoney = plan.budget.totalMoney;
        if (snapshot == null || mode == null) return plan;
        var ctx = new Ctx { aiTeam = aiTeam, snapshot = snapshot, mode = mode, defenseMode = defenseMode, money = Mathf.Max(0, currentMoney), income = Mathf.Max(0, incomePerTurn), turn = currentTurn, maxVariablePlans = Mathf.Max(0, maxVariablePlans), savingFallbackPurchasesThisTurn = Mathf.Max(0, savingFallbackPurchasesThisTurn), groups = GetGroupsByPriority(mode.groups), producers = CollectProducers(snapshot, aiTeam) }; ctx.groupCounts = CountFriendlyUnitsByConfiguredGroup(snapshot, ctx.groups);
        BuildDemands(ctx, plan); EvaluateUrgentLogisticsNeed(ctx, plan); BuildCapabilityPressures(plan); BuildOrders(plan); EvaluateStrategicSave(ctx, plan); EvaluateCompositionSave(ctx, plan); Assign(ctx, plan); plan.budget.reservedMoney = ctx.reservedMoney; plan.budget.freeMoney = Mathf.Max(0, ctx.money - ctx.reservedMoney - ctx.strategicReservedMoney); plan.budget.saveTargetMoney = ctx.saveTargetMoney; plan.budget.strategicReserveMoney = ctx.strategicReservedMoney; plan.budget.massFloorBlocked = !EvaluateMassFloor(plan, currentTurn, out int massFloorRequiredUnits, out string massFloorReason); plan.massFloorRequiredUnits = massFloorRequiredUnits; plan.massFloorReason = massFloorReason; if (ctx.strategicSaveCandidate.HasValue) { var strategic = ctx.strategicSaveCandidate.Value; plan.strategicSaveActive = true; plan.strategicSaveUnitId = strategic.unit != null ? strategic.unit.id : null; plan.strategicSaveSourceLabel = strategic.sourceLabel; plan.strategicSaveCost = strategic.cost; plan.strategicSaveTurnsToAfford = strategic.turnsToAfford; plan.strategicSaveDeferredOrders = ctx.strategicDeferredOrders; plan.strategicSaveReason = ctx.strategicSaveReason; } return plan;
    }

    public static bool TryGetBlockingShoppingOccupant(ConstructionManager construction, out UnitManager blockingUnit) {
        blockingUnit = null; if (construction == null) return false; var constructionCell = construction.CurrentCellPosition; constructionCell.z = 0; for (int i = 0; i < UnitManager.AllActive.Count; i++) { var unit = UnitManager.AllActive[i]; if (unit == null || unit.IsDead || unit.IsEmbarked || !unit.gameObject.activeInHierarchy) continue; var unitCell = unit.CurrentCellPosition; unitCell.z = 0; if (unitCell != constructionCell) continue; if (!IsBlockingUnitForConstructionShopping(unit)) continue; blockingUnit = unit; return true; } return false; }
    public static bool IsBlockingUnitForConstructionShopping(UnitManager unit) { if (unit == null) return false; if (unit.GetHeightLevel() != HeightLevel.Surface) return false; Domain d = unit.GetDomain(); return d == Domain.Land || d == Domain.Naval; }

    private static void BuildDemands(Ctx ctx, AIShoppingTurnPlan plan) { var byCap = new Dictionary<AIPlanCapability, AIShoppingCapabilityDemand>(); var activePlans = ctx.snapshot.ActivePlans; if (activePlans == null) return; Dictionary<string, int> assignedTransportByPlan = BuildAssignedTransportCoverageByPlan(ctx.snapshot, activePlans); for (int i = 0; i < activePlans.Count; i++) { var p = activePlans[i]; if (p == null) continue; string planKey = PlanKey(p); int assignedTransport = assignedTransportByPlan.TryGetValue(planKey, out int transportCount) ? transportCount : 0; AddDemand(ctx.snapshot, plan, byCap, p, AIPlanCapability.Capture, Mathf.Max(p.DesiredCaptureCount, p.HasCaptureTarget ? CountOutstandingCaptureTargets(ctx.snapshot, p.Sector) : 0), CountAssignedRole(p, AIPlanRole.Capture)); AddDemand(ctx.snapshot, plan, byCap, p, AIPlanCapability.Escort, Mathf.Max(0, p.DesiredEscortCount), CountAssignedRole(p, AIPlanRole.Escort)); AddDemand(ctx.snapshot, plan, byCap, p, AIPlanCapability.FireSupport, Mathf.Max(0, p.DesiredArtilleryCount), CountAssignedRole(p, AIPlanRole.Artillery)); AddDemand(ctx.snapshot, plan, byCap, p, AIPlanCapability.Transport, Mathf.Max(0, p.DesiredTransportCount), assignedTransport); AddDemand(ctx.snapshot, plan, byCap, p, AIPlanCapability.Logistics, Mathf.Max(0, p.DesiredSupportCount), CountAssignedRole(p, AIPlanRole.Support)); } foreach (var kv in byCap) plan.capabilityDemands.Add(kv.Value); }
    private static void AddDemand(AISnapshot snapshot, AIShoppingTurnPlan plan, Dictionary<AIPlanCapability, AIShoppingCapabilityDemand> byCap, AIPlanIntent intent, AIPlanCapability cap, int desired, int assigned) { int miss = Mathf.Max(0, desired - assigned); plan.planDemands.Add(new AIShoppingPlanDemand { planKey = PlanKey(intent), planLabel = PlanLabel(intent), sector = intent.Sector, capability = cap, desiredCount = Mathf.Max(0, desired), assignedCount = Mathf.Max(0, assigned), missingCount = miss, tacticalRiskScore = intent.TacticalRiskScore, hasCaptureTarget = intent.HasCaptureTarget }); if (!byCap.TryGetValue(cap, out var agg)) { agg = new AIShoppingCapabilityDemand { capability = cap }; byCap[cap] = agg; } agg.totalDemand += Mathf.Max(0, desired); agg.totalAssigned += Mathf.Max(0, assigned); agg.missingCount += miss; if (cap == AIPlanCapability.Capture && desired > 0 && assigned <= 0 && !IsProtectPlan(intent)) plan.hasCriticalCaptureGap = true; }
    private static void EvaluateUrgentLogisticsNeed(Ctx ctx, AIShoppingTurnPlan plan) {
        if (ctx == null || plan == null || ctx.snapshot == null || ctx.snapshot.FriendlyUnits == null) return;

        var logisticsDemand = FindCapabilityDemand(plan, AIPlanCapability.Logistics);
        plan.supplyChainPlansMissingLogistics = logisticsDemand != null ? Mathf.Max(0, logisticsDemand.missingCount) : 0;

        int availableSuppliers = 0;
        int relevantCombatUnits = 0;
        int lowAutonomyUnits = 0;
        int outOfAmmoUnits = 0;
        int damagedUnits = 0;
        int frontlineCriticalUnits = 0;
        int pressure = 0;

        for (int i = 0; i < ctx.snapshot.FriendlyUnits.Count; i++)
        {
            var unit = ctx.snapshot.FriendlyUnits[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;

            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;

            if (HasLogisticsCapability(unit, data))
                availableSuppliers++;

            if (!IsCombatRelevantForSupplyPressure(data))
                continue;

            relevantCombatUnits++;

            bool lowAutonomy = IsLowAutonomyForSupplyPressure(unit);
            bool outOfAmmo = HasNoCombatAmmoForSupplyPressure(unit);
            bool damaged = unit.CurrentHP < unit.GetMaxHP();
            bool frontline = IsFrontlineAssignedUnit(ctx.snapshot, unit);

            if (lowAutonomy)
            {
                lowAutonomyUnits++;
                pressure += frontline ? 6000 : 2500;
            }

            if (outOfAmmo)
            {
                outOfAmmoUnits++;
                pressure += frontline ? 7000 : 3000;
            }

            if (damaged)
            {
                damagedUnits++;
                pressure += frontline ? 2500 : 1000;
            }

            if (frontline && (lowAutonomy || outOfAmmo || damaged))
            {
                frontlineCriticalUnits++;
                pressure += 1500;
            }
        }

        int targetSuppliers = plan.supplyChainPlansMissingLogistics > 0 ? Mathf.Clamp(Mathf.CeilToInt(relevantCombatUnits / 12f), 1, 3) : 0;
        int additionalSuppliersNeeded = Mathf.Max(0, targetSuppliers - availableSuppliers);

        if (plan.supplyChainPlansMissingLogistics > 0)
            pressure += 5000 * Mathf.Min(plan.supplyChainPlansMissingLogistics, Mathf.Max(1, additionalSuppliersNeeded));

        if (availableSuppliers <= 0)
        {
            if (plan.supplyChainPlansMissingLogistics > 0)
                pressure += 6000;
            if (lowAutonomyUnits > 0 || outOfAmmoUnits > 0 || frontlineCriticalUnits > 0)
                pressure += 12000;
        }
        else if (additionalSuppliersNeeded <= 0)
        {
            pressure = Mathf.RoundToInt(pressure * 0.35f);
        }
        else
        {
            pressure = Mathf.RoundToInt(pressure * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(additionalSuppliersNeeded / 2f)));
        }

        plan.supplyChainAvailableSuppliers = availableSuppliers;
        plan.supplyChainLowAutonomyUnits = lowAutonomyUnits;
        plan.supplyChainOutOfAmmoUnits = outOfAmmoUnits;
        plan.supplyChainDamagedUnits = damagedUnits;
        plan.supplyChainFrontlineCriticalUnits = frontlineCriticalUnits;
        plan.supplyChainTargetSuppliers = targetSuppliers;
        plan.supplyChainAdditionalSuppliersNeeded = additionalSuppliersNeeded;
        plan.supplyChainPressureScore = Mathf.Max(0, pressure);
        plan.supplyChainReason = string.Format(
            "suppliers={0}/{1} add={2} missingLOG={3} lowFuel={4} noAmmo={5} damaged={6} frontlineCritical={7}",
            availableSuppliers,
            targetSuppliers,
            additionalSuppliersNeeded,
            plan.supplyChainPlansMissingLogistics,
            lowAutonomyUnits,
            outOfAmmoUnits,
            damagedUnits,
            frontlineCriticalUnits);

        plan.urgentLogisticsNeed = plan.supplyChainPlansMissingLogistics > 0
            && additionalSuppliersNeeded > 0
            && (lowAutonomyUnits > 0 || outOfAmmoUnits > 0 || frontlineCriticalUnits > 0 || availableSuppliers <= 0);
    }
    private static void BuildOrders(AIShoppingTurnPlan plan) { int remainingAdditionalSuppliers = plan != null ? Mathf.Max(0, plan.supplyChainAdditionalSuppliersNeeded) : 0; for (int i = 0; i < plan.planDemands.Count; i++) { var d = plan.planDemands[i]; if (d == null || d.missingCount <= 0) continue; int effectiveMissing = d.missingCount; if (d.capability == AIPlanCapability.Logistics) { effectiveMissing = Mathf.Min(effectiveMissing, remainingAdditionalSuppliers); remainingAdditionalSuppliers = Mathf.Max(0, remainingAdditionalSuppliers - effectiveMissing); } if (effectiveMissing <= 0) continue; plan.orders.Add(new AIShoppingOrder { orderId = d.planKey + "|" + d.capability, planKey = d.planKey, planLabel = d.planLabel, sector = d.sector, capability = d.capability, desiredCount = d.desiredCount, assignedCount = d.assignedCount, remainingCount = effectiveMissing, critical = d.capability == AIPlanCapability.Capture && d.hasCaptureTarget && d.assignedCount <= 0, priorityScore = Priority(d, plan), reason = string.Format("plan={0} capability={1} missing={2}/{3}", d.planLabel, d.capability, effectiveMissing, d.desiredCount) }); } plan.orders.Sort((a, b) => b.priorityScore.CompareTo(a.priorityScore)); }
    private static int Priority(AIShoppingPlanDemand d, AIShoppingTurnPlan plan) { int p = BasePriority(d.capability); if (d.capability == AIPlanCapability.Capture && d.hasCaptureTarget && d.assignedCount <= 0) p += 20000; if (d.capability == AIPlanCapability.Logistics && plan != null) p += Mathf.Min(plan.urgentLogisticsNeed ? 22000 : 0, 22000) + Mathf.Min(26000, Mathf.Max(0, plan.supplyChainPressureScore)); if (d.capability == AIPlanCapability.Transport) p += GetTransportDynamicPriority(d, plan); p += d.missingCount * 500 + Mathf.Max(0, d.tacticalRiskScore) * 10; return p; }
    private static void BuildCapabilityPressures(AIShoppingTurnPlan plan) {
        if (plan == null) return;
        plan.capabilityPressures.Clear();
        AIPlanCapability[] capabilities = new[] { AIPlanCapability.Capture, AIPlanCapability.Escort, AIPlanCapability.FireSupport, AIPlanCapability.Transport, AIPlanCapability.Logistics };
        for (int ci = 0; ci < capabilities.Length; ci++)
        {
            AIPlanCapability capability = capabilities[ci];
            int orderCount = 0, basePressure = 0, missingPressure = 0, riskPressure = 0, criticalPressure = 0, dynamicPressure = 0;
            for (int i = 0; i < plan.planDemands.Count; i++)
            {
                var d = plan.planDemands[i];
                if (d == null || d.capability != capability || d.missingCount <= 0)
                    continue;
                orderCount++;
                basePressure += BasePriority(capability);
                missingPressure += d.missingCount * 500;
                riskPressure += Mathf.Max(0, d.tacticalRiskScore) * 10;
                if (capability == AIPlanCapability.Capture && d.hasCaptureTarget && d.assignedCount <= 0)
                    criticalPressure += 20000;
                if (capability == AIPlanCapability.Logistics)
                    dynamicPressure += Mathf.Min(plan.urgentLogisticsNeed ? 22000 : 0, 22000) + Mathf.Min(26000, Mathf.Max(0, plan.supplyChainPressureScore));
                else if (capability == AIPlanCapability.Transport)
                    dynamicPressure += GetTransportDynamicPriority(d, plan);
            }

            string criteria = capability == AIPlanCapability.Logistics
                ? string.Format("orders={0} base={1} missing={2} risk={3} urgent={4} pressure={5}", orderCount, basePressure, missingPressure, riskPressure, plan.urgentLogisticsNeed ? 22000 : 0, Mathf.Min(26000, Mathf.Max(0, plan.supplyChainPressureScore)))
                : capability == AIPlanCapability.Transport
                    ? string.Format("orders={0} base={1} missing={2} risk={3} dynamic={4}", orderCount, basePressure, missingPressure, riskPressure, dynamicPressure)
                : capability == AIPlanCapability.Capture
                    ? string.Format("orders={0} base={1} missing={2} risk={3} critical={4}", orderCount, basePressure, missingPressure, riskPressure, criticalPressure)
                    : string.Format("orders={0} base={1} missing={2} risk={3}", orderCount, basePressure, missingPressure, riskPressure);

            plan.capabilityPressures.Add(new AIShoppingCapabilityPressure {
                capability = capability,
                orderCount = orderCount,
                basePressure = basePressure,
                missingPressure = missingPressure,
                riskPressure = riskPressure,
                criticalPressure = criticalPressure,
                dynamicPressure = dynamicPressure,
                totalPressure = basePressure + missingPressure + riskPressure + criticalPressure + dynamicPressure,
                criteriaSummary = criteria
            });
        }
    }
    private static int BasePriority(AIPlanCapability capability) { return capability == AIPlanCapability.Capture ? 40000 : capability == AIPlanCapability.Escort ? 30000 : capability == AIPlanCapability.FireSupport ? 20000 : capability == AIPlanCapability.Transport ? 14000 : 10000; }
    private static void EvaluateStrategicSave(Ctx ctx, AIShoppingTurnPlan plan) { if (ctx.mode == null || !ctx.mode.saveForNextRound) return; Candidate best = default; bool found = false; for (int i = 0; i < ctx.producers.Count; i++) { var info = ctx.producers[i]; if (info == null || info.Source == null) continue; if (!TryCollectStrategicCandidateForConstruction(ctx, plan, info.Source, out var candidate)) continue; if (!found || BetterStrategic(candidate, best)) { best = candidate; found = true; } } if (!found || best.turnsToAfford > 2) return; bool canForceImmediateTopBuy = plan.hasCriticalCaptureGap && best.affordableNow; if (!canForceImmediateTopBuy && !PassesMassFloor(plan, ctx.turn)) return; ctx.strategicSaveCandidate = best; ctx.strategicReservedMoney = Mathf.Max(0, best.cost - ctx.money); ctx.saveTargetMoney = Mathf.Max(ctx.saveTargetMoney, best.cost); if (best.affordableNow) { ctx.saveModeActive = false; ctx.saveModeReason = string.Empty; ctx.strategicSaveReason = Reason(plan, canForceImmediateTopBuy ? "strategic-ready-gap-override" : "strategic-ready", string.Format("alvo={0} | capability={1} | origem={2} | custo={3} | score={4}", best.unit != null ? best.unit.id : "(null)", best.capability, best.sourceLabel, best.cost, best.strategicScore)); } else { ctx.saveModeActive = true; ctx.saveModeReason = Reason(plan, "strategic-save", string.Format("alvo={0} | capability={1} | origem={2} | custo={3} | turns={4} | score={5}", best.unit != null ? best.unit.id : "(null)", best.capability, best.sourceLabel, best.cost, best.turnsToAfford, best.strategicScore)); ctx.strategicSaveReason = ctx.saveModeReason; } }
    private static bool TryCollectStrategicCandidateForConstruction(Ctx ctx, AIShoppingTurnPlan plan, ConstructionManager construction, out Candidate strategic) { strategic = default; bool found = false; for (int capabilityIndex = 0; capabilityIndex < 2; capabilityIndex++) { var capability = capabilityIndex == 0 ? AIPlanCapability.Escort : AIPlanCapability.FireSupport; var ladder = CollectCapabilityCandidatesFromGroups(construction, ctx.groups, capability, ctx.money, ctx.income); if (!HasRealLadderChoice(ladder)) continue; Candidate? baseline = PickBestAffordableCandidate(ladder); for (int i = 0; i < ladder.Count; i++) { var option = ladder[i]; if (option.unit == null) continue; if (baseline.HasValue && !IsMeaningfulUpgrade(baseline.Value, option)) continue; if (!baseline.HasValue && option.turnsToAfford > 2) continue; option.capability = capability; option.strategicScore = StrategicPressureScore(plan, capability) + UpgradeValueScore(baseline, option) - WaitPenaltyScore(option.turnsToAfford); if (option.strategicScore <= 0) continue; if (!found || BetterStrategic(option, strategic)) { strategic = option; found = true; } } } return found; }
    // Pré-passo: identifica o primeiro grupo de composicao abaixo do alvo cuja unidade ideal nao e acessivel
    // neste turno mas sera em <= 2 turnos. Designa o melhor produtor e reserva o dinheiro para que outros
    // branches (fallback, save-fallback) nao o gastem antes que a compra possa ser executada.
    private static void EvaluateCompositionSave(Ctx ctx, AIShoppingTurnPlan plan) {
        // Nao conflita com strategic save — se ja ha um alvo estrategico, ele tem prioridade.
        if (ctx.strategicSaveCandidate.HasValue) return;
        if (ctx.mode == null || !ctx.mode.saveForNextRound) return;
        if (!PassesMassFloor(plan, ctx.turn)) return;
        int money = Mathf.Max(0, ctx.money - ctx.reservedMoney);
        int denom = Mathf.Max(1, plan.friendlyUnitCount);
        for (int gi = 0; gi < ctx.groups.Count; gi++) {
            var group = ctx.groups[gi];
            if (group == null) continue;
            float target = Mathf.Clamp01(group.targetPercentage / 100f);
            int current = GroupCount(ctx, group);
            float ratio = Mathf.Clamp01((float)current / denom);
            if (ratio + 0.0001f >= target) continue;
            // Grupo esta abaixo do alvo. Verifica se algum produtor pode comprar AGORA.
            bool anyAffordableNow = false;
            for (int pi = 0; pi < ctx.producers.Count; pi++) {
                var prod = ctx.producers[pi];
                if (prod == null || prod.Source == null) continue;
                if (TryResolveFirstAffordableFromGroup(prod.Source, group, money, out _, out _, out _)) { anyAffordableNow = true; break; }
            }
            // Se acessivel agora, TryAssignComposition normal vai resolver — nao interfere.
            if (anyAffordableNow) continue;
            // Nao acessivel agora: busca candidato futuro (turnsToAfford <= 2).
            if (!TryFindFutureGroupCandidate(ctx, group, money, out var future)) continue;
            if (future.turnsToAfford > 2) continue;
            // Encontra o melhor produtor que tem essa unidade no catalogo.
            AIConstructionInfo bestProducer = null;
            for (int pi = 0; pi < ctx.producers.Count; pi++) {
                var prod = ctx.producers[pi];
                if (prod == null || prod.Source == null) continue;
                if (TryGetOfferIndex(prod.Source, future.unit, out _, out _)) { bestProducer = prod; break; }
            }
            if (bestProducer == null) continue;
            // Reserva: marca produtor designado e bloqueia o dinheiro para que outros branches nao o gastem.
            ctx.compositionSaveCandidate = future;
            ctx.compositionSaveProducer = bestProducer;
            ctx.compositionSaveCommitted = false;
            ctx.strategicReservedMoney = Mathf.Max(ctx.strategicReservedMoney, future.cost);
            ctx.saveTargetMoney = Mathf.Max(ctx.saveTargetMoney, future.cost);
            ctx.saveModeActive = true;
            ctx.saveModeReason = Reason(plan, "composition-save", string.Format("grupo={0} | aguardando={1} | turns={2} | produtor={3}", group.label, future.unit != null ? future.unit.id : "(null)", future.turnsToAfford, bestProducer.DisplayName));
            break; // Trata apenas o grupo de maior prioridade por turno.
        }
    }

    // Executa a compra reservada pelo EvaluateCompositionSave no produtor designado.
    // Usa money = total - reservedMoney (ignora strategicReservedMoney, pois este e o comprador legitimo).
    private static bool TryAssignCompositionSaveReserved(Ctx ctx, AIShoppingTurnPlan plan, ConstructionManager construction, AIShoppingConstructionDecision d) {
        if (!ctx.compositionSaveCandidate.HasValue || ctx.compositionSaveCommitted) return false;
        var candidate = ctx.compositionSaveCandidate.Value;
        if (candidate.unit == null) return false;
        int money = Mathf.Max(0, ctx.money - ctx.reservedMoney); // nao deduz strategicReservedMoney — este e o comprador designado
        if (money < candidate.cost) return false; // ainda nao e acessivel neste turno
        if (!TryGetOfferIndex(construction, candidate.unit, out int offerIndex, out UnitData offer) || offer == null) return false;
        ctx.compositionSaveCommitted = true;
        ctx.strategicReservedMoney = Mathf.Max(0, ctx.strategicReservedMoney - candidate.cost);
        ctx.reservedMoney += Mathf.Max(0, offer.cost);
        ctx.reservedOrders++;
        ctx.saveModeActive = false;
        ctx.saveModeReason = string.Empty;
        TrackReserved(ctx, offer.id);
        d.kind = AIShoppingDecisionKind.Buy;
        d.plannedUnitId = offer.id;
        d.targetIndex = offerIndex;
        d.cost = Mathf.Max(0, offer.cost);
        d.plannedReason = Reason(plan, "composition-save-execute", string.Format("alvo={0} | custo={1}", offer.id, offer.cost));
        return true;
    }

    private static void Assign(Ctx ctx, AIShoppingTurnPlan plan) { bool massFloorAllowsFallback = !(plan.hasCriticalCaptureGap && !PassesMassFloor(plan, ctx.turn)); for (int i = 0; i < ctx.producers.Count; i++) { var info = ctx.producers[i]; if (info == null || info.Source == null) continue; var d = new AIShoppingConstructionDecision { construction = info.Source, constructionLabel = !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : info.Source.name, kind = AIShoppingDecisionKind.None, plannedReason = "sem decisao" }; if (TryGetBlockingShoppingOccupant(info.Source, out var blocker)) { d.kind = AIShoppingDecisionKind.Blocked; d.blockingUnit = blocker; d.plannedReason = string.Format("construcao bloqueada por {0}", blocker != null ? blocker.name : "(null)"); plan.constructionDecisions.Add(d); continue; }
        // Produtor designado pelo EvaluateCompositionSave executa a compra reservada com prioridade maxima.
        bool isDesignatedCompositionProducer = ctx.compositionSaveCandidate.HasValue && !ctx.compositionSaveCommitted && info == ctx.compositionSaveProducer;
        if (isDesignatedCompositionProducer && TryAssignCompositionSaveReserved(ctx, plan, info.Source, d)) { plan.constructionDecisions.Add(d); continue; }
        if (TryAssignStrategicPurchase(ctx, plan, info.Source, d) || TryAssignOverflowPressurePurchase(ctx, plan, info.Source, d) || TryAssignOrder(ctx, plan, info.Source, d) || TryAssignSaveFallback(ctx, plan, info.Source, d, massFloorAllowsFallback) || TryAssignComposition(ctx, plan, info.Source, d) || TryAssignFallback(ctx, plan, info.Source, d, massFloorAllowsFallback)) { plan.constructionDecisions.Add(d); continue; } d.kind = ctx.saveModeActive ? AIShoppingDecisionKind.Save : AIShoppingDecisionKind.NoOffer; d.plannedReason = ctx.saveModeActive ? ctx.saveModeReason : "nenhuma oferta acessivel no catalogo"; plan.constructionDecisions.Add(d); } }
    private static bool TryAssignOverflowPressurePurchase(Ctx ctx, AIShoppingTurnPlan plan, ConstructionManager construction, AIShoppingConstructionDecision d) { if (ctx == null || plan == null || construction == null) return false; int money = Mathf.Max(0, ctx.money - ctx.reservedMoney); if (money <= 0) return false; if (ctx.strategicSaveCandidate.HasValue && !ctx.strategicPurchaseCommitted) return false; if (plan.hasCriticalCaptureGap) return false; if (!TryFindOverflowPressureCandidate(ctx, construction, money, out var candidate)) return false; if (candidate.unit == null || !TryGetOfferIndex(construction, candidate.unit, out int offerIndex, out UnitData offer) || offer == null) return false; ctx.reservedMoney += Mathf.Max(0, offer.cost); ctx.reservedOrders++; TrackReserved(ctx, offer.id); d.kind = AIShoppingDecisionKind.Buy; d.plannedUnitId = offer.id; d.targetIndex = offerIndex; d.cost = Mathf.Max(0, offer.cost); d.capability = candidate.capability; d.plannedReason = Reason(plan, "big-cash-buy", string.Format("alvo={0} | capability={1} | origem={2} | custo={3}", offer.id, candidate.capability, candidate.sourceLabel, offer.cost)); return true; }
    private static bool TryAssignStrategicPurchase(Ctx ctx, AIShoppingTurnPlan plan, ConstructionManager construction, AIShoppingConstructionDecision d) { if (ctx == null || !ctx.strategicSaveCandidate.HasValue || ctx.strategicPurchaseCommitted) return false; var candidate = ctx.strategicSaveCandidate.Value; int money = Mathf.Max(0, ctx.money - ctx.reservedMoney); if (money < candidate.cost) return false; if (candidate.unit == null || !TryGetOfferIndex(construction, candidate.unit, out int offerIndex, out UnitData offer) || offer == null) return false; ctx.strategicPurchaseCommitted = true; ctx.strategicReservedMoney = 0; ctx.reservedMoney += Mathf.Max(0, offer.cost); ctx.reservedOrders++; ctx.saveModeActive = false; ctx.saveModeReason = string.Empty; TrackReserved(ctx, offer.id); d.kind = AIShoppingDecisionKind.Buy; d.plannedUnitId = offer.id; d.targetIndex = offerIndex; d.cost = Mathf.Max(0, offer.cost); d.capability = candidate.capability; d.plannedReason = Reason(plan, "strategic-buy", string.Format("alvo={0} | capability={1} | origem={2} | custo={3} | score={4}", offer.id, candidate.capability, candidate.sourceLabel, offer.cost, candidate.strategicScore)); return true; }
    private static bool TryAssignOrder(Ctx ctx, AIShoppingTurnPlan plan, ConstructionManager construction, AIShoppingConstructionDecision d) { int money = Mathf.Max(0, ctx.money - ctx.reservedMoney - ctx.strategicReservedMoney); string saveReason = null; int saveTarget = 0; for (int i = 0; i < plan.orders.Count; i++) { var order = plan.orders[i]; if (order == null || order.remainingCount <= 0) continue; if (ctx.strategicSaveCandidate.HasValue && !ctx.strategicPurchaseCommitted && ShouldDeferOrderForStrategicSave(order)) { if (!order.deferredByStrategicSave) { order.deferredByStrategicSave = true; ctx.strategicDeferredOrders++; } continue; } if (TryResolveCapabilityChoice(construction, ctx.mode, ctx.groups, order.capability, money, ctx.income, plan, ctx.defenseMode, out var pick, out var reason, out bool save, out int targetMoney)) { order.remainingCount--; ctx.reservedMoney += pick.cost; ctx.reservedOrders++; TrackReserved(ctx, pick.unit != null ? pick.unit.id : null); d.kind = AIShoppingDecisionKind.Buy; d.plannedUnitId = pick.unit != null ? pick.unit.id : null; d.targetIndex = pick.offerIndex; d.cost = pick.cost; d.plannedReason = Reason(plan, "capability-buy", "order=" + order.orderId + " | " + reason); d.orderId = order.orderId; d.planKey = order.planKey; d.capability = order.capability; return true; } if (save && saveReason == null) { saveReason = Reason(plan, "capability-save", "order=" + order.orderId + " | " + reason); saveTarget = targetMoney; } } if (saveReason != null) { ctx.saveModeActive = true; ctx.saveModeReason = saveReason; ctx.saveTargetMoney = Mathf.Max(ctx.saveTargetMoney, saveTarget); } return false; }
    private static bool ShouldDeferOrderForStrategicSave(AIShoppingOrder order) { if (order == null) return false; return order.capability != AIPlanCapability.Capture; }
    private static bool TryAssignSaveFallback(Ctx ctx, AIShoppingTurnPlan plan, ConstructionManager construction, AIShoppingConstructionDecision d, bool massFloorAllowsFallback) { if (!ctx.saveModeActive || ctx.mode == null || !ctx.mode.allowFallbackWhenSaving || !massFloorAllowsFallback) return false; bool unlimited = ctx.defenseMode && ctx.mode.buyFallbackWhenSavingOnDefenseMode; bool consumed = ctx.mode.allowFallbackWhenSavingButOnce && (ctx.savingFallbackPurchasesThisTurn + ctx.reservedFallbacks) > 0; if (consumed && !unlimited) return false; int money = Mathf.Max(0, ctx.money - ctx.reservedMoney - ctx.strategicReservedMoney); if (!TryResolveFirstAffordableFromUnitList(construction, ctx.mode.fallbackUnits, money, out int index, out string unitId, out int cost)) return false; ctx.reservedMoney += cost; ctx.reservedFallbacks++; d.kind = AIShoppingDecisionKind.Buy; d.plannedUnitId = unitId; d.targetIndex = index; d.cost = cost; d.usedSavingFallback = true; d.plannedReason = Reason(plan, "save-fallback", ctx.saveModeReason + " | fallback=" + unitId); TrackReserved(ctx, unitId); return true; }
    private static bool TryAssignComposition(Ctx ctx, AIShoppingTurnPlan plan, ConstructionManager construction, AIShoppingConstructionDecision d) { int money = Mathf.Max(0, ctx.money - ctx.reservedMoney - ctx.strategicReservedMoney); int denom = Mathf.Max(1, plan.friendlyUnitCount + ctx.reservedOrders); for (int i = 0; i < ctx.groups.Count; i++) { var g = ctx.groups[i]; if (g == null) continue; float target = Mathf.Clamp01(g.targetPercentage / 100f); int current = GroupCount(ctx, g); float ratio = Mathf.Clamp01((float)current / denom); if (ratio + 0.0001f >= target) continue; if (TryResolveFirstAffordableFromGroup(construction, g, money, out int index, out string unitId, out int cost)) { ctx.reservedMoney += cost; ctx.reservedOrders++; d.kind = AIShoppingDecisionKind.Buy; d.plannedUnitId = unitId; d.targetIndex = index; d.cost = cost; d.plannedReason = Reason(plan, "composition-target", string.Format("grupo={0} | composicao={1}/{2} ({3:P0}) | alvo={4:P0} | prioridade={5}", g.label, current, denom, ratio, target, g.priority)); TrackReserved(ctx, unitId); return true; }
            // Nao duplicar a logica de save para o grupo ja reservado pelo EvaluateCompositionSave.
            if (ctx.compositionSaveCandidate.HasValue) { ctx.saveModeActive = true; break; }
            if (ctx.mode != null && ctx.mode.saveForNextRound && TryFindFutureGroupCandidate(ctx, g, money, out var future)) { ctx.saveModeActive = true; ctx.saveModeReason = Reason(plan, "composition-save", string.Format("grupo={0} | aguardando={1} | turns={2}", g.label, future.unit != null ? future.unit.id : "(null)", future.turnsToAfford)); ctx.saveTargetMoney = Mathf.Max(ctx.saveTargetMoney, future.cost); break; } } return false; }
    private static bool TryAssignFallback(Ctx ctx, AIShoppingTurnPlan plan, ConstructionManager construction, AIShoppingConstructionDecision d, bool massFloorAllowsFallback) { int money = Mathf.Max(0, ctx.money - ctx.reservedMoney - ctx.strategicReservedMoney); if (massFloorAllowsFallback && TryResolveFirstAffordableFromUnitList(construction, ctx.mode != null ? ctx.mode.fallbackUnits : null, money, out int index, out string unitId, out int cost)) { ctx.reservedMoney += cost; ctx.reservedOrders++; d.kind = AIShoppingDecisionKind.Buy; d.plannedUnitId = unitId; d.targetIndex = index; d.cost = cost; d.plannedReason = Reason(plan, "fallback-mode", "fallback-modo=" + (ctx.mode != null ? ctx.mode.label : "(null)")); TrackReserved(ctx, unitId); return true; } if (TryResolveAnyAffordableOffer(construction, money, out index, out unitId, out cost)) { ctx.reservedMoney += cost; ctx.reservedOrders++; d.kind = AIShoppingDecisionKind.Buy; d.plannedUnitId = unitId; d.targetIndex = index; d.cost = cost; d.plannedReason = Reason(plan, "fallback-catalog", "qualquer oferta acessivel"); TrackReserved(ctx, unitId); return true; } return false; }
    private static bool TryResolveCapabilityChoice(ConstructionManager construction, AIDataMode mode, IReadOnlyList<AIDataGroup> groups, AIPlanCapability capability, int currentMoney, int incomePerTurn, AIShoppingTurnPlan plan, bool defenseMode, out Candidate pick, out string reason, out bool save, out int targetMoney) { pick = default; reason = "demanda=" + capability + " | sem-oferta-compativel"; save = false; targetMoney = 0; var ladder = CollectCapabilityCandidatesFromGroups(construction, groups, capability, currentMoney, incomePerTurn); var fallback = CollectCapabilityCandidatesFromFallback(construction, mode, capability, currentMoney, incomePerTurn); Candidate? ideal = PickBestCandidate(ladder); Candidate? buyNow = PickBestAffordableCandidate(ladder); Candidate? future = PickBestUnaffordableCandidate(ladder); Candidate? fallbackNow = PickBestAffordableCandidate(fallback); bool hasRealLadderChoice = HasRealLadderChoice(ladder); if (mode != null && mode.saveForNextRound && hasRealLadderChoice && future.HasValue && ShouldSave(buyNow, future.Value, plan, defenseMode, plan.turnNumber)) { save = true; targetMoney = future.Value.cost; reason = string.Format("ladder=ideal-save | demanda={0} | ideal={1} | origem={2} | turns={3} | custo={4}", capability, future.Value.unit != null ? future.Value.unit.id : "(null)", future.Value.sourceLabel, future.Value.turnsToAfford, future.Value.cost); return false; } if (buyNow.HasValue) { pick = buyNow.Value; reason = string.Format("ladder=buy-now | demanda={0} | unidade={1} | origem={2} | custo={3}", capability, pick.unit != null ? pick.unit.id : "(null)", pick.sourceLabel, pick.cost); return true; } if (fallbackNow.HasValue) { pick = fallbackNow.Value; reason = string.Format("ladder=fallback | demanda={0} | unidade={1} | origem={2} | custo={3}", capability, pick.unit != null ? pick.unit.id : "(null)", pick.sourceLabel, pick.cost); return true; } if (hasRealLadderChoice && future.HasValue && mode != null && mode.saveForNextRound && future.Value.turnsToAfford <= 2 && PassesMassFloor(plan, plan.turnNumber)) { save = true; targetMoney = future.Value.cost; reason = string.Format("ladder=ideal-save | demanda={0} | ideal={1} | origem={2} | turns={3} | custo={4}", capability, future.Value.unit != null ? future.Value.unit.id : "(null)", future.Value.sourceLabel, future.Value.turnsToAfford, future.Value.cost); } else if (ideal.HasValue) { reason = string.Format("ladder=ideal-unavailable | demanda={0} | ideal={1} | origem={2} | turns={3} | custo={4}", capability, ideal.Value.unit != null ? ideal.Value.unit.id : "(null)", ideal.Value.sourceLabel, ideal.Value.turnsToAfford, ideal.Value.cost); } return false; }
    private static Candidate? PickBestCandidate(List<Candidate> candidates) { if (candidates == null || candidates.Count <= 0) return null; Candidate best = candidates[0]; for (int i = 1; i < candidates.Count; i++) if (Better(candidates[i], best)) best = candidates[i]; return best; }
    private static Candidate? PickBestAffordableCandidate(List<Candidate> candidates) { if (candidates == null || candidates.Count <= 0) return null; bool found = false; Candidate best = default; for (int i = 0; i < candidates.Count; i++) { var candidate = candidates[i]; if (!candidate.affordableNow) continue; if (!found || Better(candidate, best)) { best = candidate; found = true; } } return found ? best : (Candidate?)null; }
    private static Candidate? PickBestUnaffordableCandidate(List<Candidate> candidates) { if (candidates == null || candidates.Count <= 0) return null; bool found = false; Candidate best = default; for (int i = 0; i < candidates.Count; i++) { var candidate = candidates[i]; if (candidate.affordableNow) continue; if (!found || Better(candidate, best)) { best = candidate; found = true; } } return found ? best : (Candidate?)null; }
    private static bool HasRealLadderChoice(List<Candidate> ladder) { if (ladder == null) return false; string firstUnitId = null; int distinct = 0; for (int i = 0; i < ladder.Count; i++) { var unitId = ladder[i].unit != null ? ladder[i].unit.id : null; if (string.IsNullOrWhiteSpace(unitId)) continue; if (string.Equals(firstUnitId, unitId, StringComparison.OrdinalIgnoreCase)) continue; if (firstUnitId == null) firstUnitId = unitId; distinct++; if (distinct >= 2) return true; } return false; }
    private static bool IsMeaningfulUpgrade(Candidate buyNow, Candidate future) { if (future.unit == null) return false; if (buyNow.unit == null) return true; string buyId = buyNow.unit.id; string futureId = future.unit.id; if (!string.IsNullOrWhiteSpace(buyId) && !string.IsNullOrWhiteSpace(futureId) && string.Equals(buyId, futureId, StringComparison.OrdinalIgnoreCase)) return false; if (future.cost <= buyNow.cost) return false; return future.cost >= (buyNow.cost + 4000); }
    private static int StrategicPressureScore(AIShoppingTurnPlan plan, AIPlanCapability capability) { if (plan == null) return 0; int missing = 0; int planCount = 0; int risk = 0; for (int i = 0; i < plan.planDemands.Count; i++) { var demand = plan.planDemands[i]; if (demand == null || demand.capability != capability || demand.missingCount <= 0) continue; missing += demand.missingCount; planCount++; risk += Mathf.Max(0, demand.tacticalRiskScore); } int capabilityBias = capability == AIPlanCapability.Escort ? 1500 : capability == AIPlanCapability.FireSupport ? 1000 : capability == AIPlanCapability.Transport ? 250 : 0; int score = (missing * 12000) + (planCount * 5000) + (risk * 10) + capabilityBias; if (capability == AIPlanCapability.Transport && plan.hasCriticalCaptureGap) score = Mathf.RoundToInt(score * 0.5f); return score; }
    private static int UpgradeValueScore(Candidate? buyNow, Candidate future) { if (future.unit == null) return 0; if (!buyNow.HasValue || buyNow.Value.unit == null) return 12000 + Mathf.Max(0, future.cost / 2); int delta = Mathf.Max(0, future.cost - buyNow.Value.cost); return 6000 + (delta * 2); }
    private static int WaitPenaltyScore(int turnsToAfford) { return Mathf.Max(0, turnsToAfford) * 4000; }
    private static bool TryFindOverflowPressureCandidate(Ctx ctx, ConstructionManager construction, int money, out Candidate best) { best = default; if (ctx == null || construction == null) return false; bool found = false; int maxCatalogCost = 0; AIPlanCapability[] preferredCapabilities = new[] { AIPlanCapability.Escort, AIPlanCapability.FireSupport, AIPlanCapability.Assault }; for (int c = 0; c < preferredCapabilities.Length; c++) { var capability = preferredCapabilities[c]; var ladder = CollectCapabilityCandidatesFromGroups(construction, ctx.groups, capability, money, ctx.income); for (int i = 0; i < ladder.Count; i++) { var candidate = ladder[i]; if (candidate.unit == null) continue; candidate.capability = capability; maxCatalogCost = Mathf.Max(maxCatalogCost, candidate.cost); if (!candidate.affordableNow) continue; if (!found || BetterOverflow(candidate, best)) { best = candidate; found = true; } } } if (!found) return false; return money >= maxCatalogCost; }
    private static bool BetterOverflow(Candidate a, Candidate b) { int rankA = OverflowCapabilityPriority(a.capability); int rankB = OverflowCapabilityPriority(b.capability); if (rankA != rankB) return rankA < rankB; if (a.cost != b.cost) return a.cost > b.cost; if (a.ladderRank != b.ladderRank) return a.ladderRank < b.ladderRank; return a.offerIndex < b.offerIndex; }
    private static int OverflowCapabilityPriority(AIPlanCapability capability) { return capability == AIPlanCapability.Escort ? 0 : capability == AIPlanCapability.FireSupport ? 1 : capability == AIPlanCapability.Assault ? 2 : capability == AIPlanCapability.Transport ? 3 : 4; }
    private static int GetTransportDynamicPriority(AIShoppingPlanDemand demand, AIShoppingTurnPlan plan)
    {
        if (demand == null || demand.capability != AIPlanCapability.Transport || demand.missingCount <= 0)
            return 0;

        int bonus = demand.missingCount * 1200;
        bonus += Mathf.Min(3000, Mathf.Max(0, demand.tacticalRiskScore) * 8);

        if (demand.desiredCount <= 1)
            bonus = Mathf.RoundToInt(bonus * 0.75f);

        if (plan != null && plan.hasCriticalCaptureGap)
            bonus = Mathf.RoundToInt(bonus * 0.35f);

        return Mathf.Clamp(bonus, 0, 6000);
    }
    private static bool BetterStrategic(Candidate a, Candidate b) { if (a.strategicScore != b.strategicScore) return a.strategicScore > b.strategicScore; if (a.turnsToAfford != b.turnsToAfford) return a.turnsToAfford < b.turnsToAfford; if (a.cost != b.cost) return a.cost > b.cost; if (a.fromFallback != b.fromFallback) return !a.fromFallback; if (a.ladderRank != b.ladderRank) return a.ladderRank < b.ladderRank; return a.offerIndex < b.offerIndex; }
    private static bool ShouldSave(Candidate? bestNow, Candidate bestFuture, AIShoppingTurnPlan plan, bool defenseMode, int turn) { if (bestFuture.unit == null || bestFuture.affordableNow || !PassesMassFloor(plan, turn) || plan.hasCriticalCaptureGap) return false; var cap = FindCapabilityDemand(plan, AIPlanCapability.Capture); var esc = FindCapabilityDemand(plan, AIPlanCapability.Escort); if (cap != null && cap.missingCount > 0) return false; if (bestFuture.turnsToAfford > 2) return false; if (defenseMode && ((cap != null && cap.missingCount > 0) || (esc != null && esc.missingCount > 0))) return false; if (!bestNow.HasValue) return true; return Better(bestFuture, bestNow.Value); }
    private static bool PassesMassFloor(AIShoppingTurnPlan plan, int turn) { return EvaluateMassFloor(plan, turn, out _, out _); }
    private static bool EvaluateMassFloor(AIShoppingTurnPlan plan, int turn, out int requiredUnits, out string reason) { requiredUnits = 0; reason = "mass-floor ok"; if (plan == null) { reason = "mass-floor: plan nulo"; return false; } int totalCap = 0, totalEsc = 0, totalFs = 0, totalTransport = 0, totalLog = 0, missing = 0, activePlanCount = 0; for (int i = 0; i < plan.capabilityDemands.Count; i++) { var d = plan.capabilityDemands[i]; if (d == null) continue; missing += d.missingCount; if (d.capability == AIPlanCapability.Capture) totalCap += d.totalDemand; else if (d.capability == AIPlanCapability.Escort) totalEsc += d.totalDemand; else if (d.capability == AIPlanCapability.FireSupport) totalFs += d.totalDemand; else if (d.capability == AIPlanCapability.Transport) totalTransport += d.totalDemand; else if (d.capability == AIPlanCapability.Logistics) totalLog += d.totalDemand; } for (int i = 0; i < plan.planDemands.Count; i++) { var demand = plan.planDemands[i]; if (demand != null && demand.missingCount > 0) activePlanCount++; } int structuralFloor = Mathf.Max(2 * Mathf.Max(1, plan.activeProductionBuildings), Mathf.Max(4, activePlanCount)); int captureEscortFloor = totalCap + Mathf.CeilToInt(totalEsc * 0.75f); int supportTailFloor = Mathf.CeilToInt((totalFs + totalTransport + totalLog) * 0.25f); requiredUnits = Mathf.Max(structuralFloor, captureEscortFloor + supportTailFloor); if (plan.hasCriticalCaptureGap) { reason = "state-based: critical capture gap ativo"; return false; } if (plan.friendlyUnitCount < structuralFloor) { reason = string.Format("state-based: unidades abaixo do minimo estrutural ({0}/{1})", plan.friendlyUnitCount, structuralFloor); return false; } if (plan.friendlyUnitCount < requiredUnits) { reason = string.Format("state-based: unidades abaixo do minimo dinamico ({0}/{1})", plan.friendlyUnitCount, requiredUnits); return false; } int missingLimit = Mathf.Max(2, Mathf.CeilToInt(activePlanCount * 0.75f)); int surplusUnits = Mathf.Max(0, plan.friendlyUnitCount - requiredUnits); int surplusThreshold = Mathf.Max(2, Mathf.CeilToInt(requiredUnits * 0.20f)); if (missing > missingLimit && surplusUnits < surplusThreshold) { reason = string.Format("state-based: faltas taticas acima do limite (missing={0}, limit={1}, surplus={2}/{3})", missing, missingLimit, surplusUnits, surplusThreshold); return false; } if (missing > missingLimit) { reason = string.Format("mass-floor ok: backlog tatico tolerado com folga de massa (missing={0}, limit={1}, surplus={2}/{3})", missing, missingLimit, surplusUnits, surplusThreshold); } return true; }
    private static AIShoppingCapabilityDemand FindCapabilityDemand(AIShoppingTurnPlan plan, AIPlanCapability capability) { for (int i = 0; i < plan.capabilityDemands.Count; i++) { var d = plan.capabilityDemands[i]; if (d != null && d.capability == capability) return d; } return null; }
    private static bool HasTransportCapability(UnitData data) { if (data == null || !data.isTransporter || data.domain != Domain.Land) return false; return data.aiUnitProfile != null && data.aiUnitProfile.HasPlanCapability(AIPlanCapability.Transport, data); }
    private static Dictionary<string, int> BuildAssignedTransportCoverageByPlan(AISnapshot snapshot, IReadOnlyList<AIPlanIntent> activePlans)
    {
        var coverage = new Dictionary<string, int>(StringComparer.Ordinal);
        if (snapshot == null || activePlans == null || activePlans.Count == 0 || snapshot.FriendlyUnits == null)
            return coverage;

        var transporters = new List<UnitManager>();
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager unit = snapshot.FriendlyUnits[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || !HasTransportCapability(data))
                continue;
            transporters.Add(unit);
        }

        var consumedTransporters = new HashSet<int>();
        for (int i = 0; i < activePlans.Count; i++)
        {
            AIPlanIntent plan = activePlans[i];
            if (plan == null)
                continue;

            string planKey = PlanKey(plan);
            int desired = Mathf.Max(0, plan.DesiredTransportCount);
            if (desired <= 0)
            {
                coverage[planKey] = 0;
                continue;
            }

            int assigned = 0;
            while (assigned < desired)
            {
                UnitManager best = null;
                int bestScore = int.MinValue;
                for (int t = 0; t < transporters.Count; t++)
                {
                    UnitManager transporter = transporters[t];
                    if (transporter == null || consumedTransporters.Contains(transporter.InstanceId))
                        continue;

                    int score = ScoreTransporterCoverageForPlan(snapshot, transporter, plan, planKey);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = transporter;
                    }
                }

                if (best == null || bestScore <= int.MinValue / 2)
                    break;

                consumedTransporters.Add(best.InstanceId);
                assigned++;
            }

            coverage[planKey] = assigned;
        }

        return coverage;
    }

    private static int ScoreTransporterCoverageForPlan(AISnapshot snapshot, UnitManager transporter, AIPlanIntent plan, string planKey)
    {
        if (snapshot == null || transporter == null || plan == null || string.IsNullOrWhiteSpace(planKey))
            return int.MinValue;

        string committedPlanKey = GetTransporterCommittedPlanKey(snapshot, transporter);
        if (!string.IsNullOrWhiteSpace(committedPlanKey) && !string.Equals(committedPlanKey, planKey, StringComparison.Ordinal))
            return int.MinValue;

        Vector3Int anchor = GetTransportPlanAnchorCell(plan);
        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;
        int distToAnchor = GetHexDistance(transporterCell, anchor);

        int score = -distToAnchor * 100;
        if (string.Equals(committedPlanKey, planKey, StringComparison.Ordinal))
            score += 100000;
        else if (HasAnyTransportPassenger(transporter))
            return int.MinValue;
        else if (HasAnyAvailableTransportSeat(transporter))
            score += 1000;
        else
            return int.MinValue;

        return score;
    }

    private static string GetTransporterCommittedPlanKey(AISnapshot snapshot, UnitManager transporter)
    {
        if (snapshot == null || transporter == null)
            return null;

        if (snapshot.UnitPlanAssignments != null
            && snapshot.UnitPlanAssignments.TryGetValue(transporter.InstanceId, out AIPlanAssignment transporterAssignment)
            && transporterAssignment != null
            && transporterAssignment.Intent != null)
        {
            return AIPlanEvaluator.BuildPlanKey(transporterAssignment.Intent);
        }

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null)
            return null;

        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            UnitManager passenger = seat != null ? seat.embarkedUnit : null;
            if (passenger == null || !passenger.IsEmbarked)
                continue;
            if (snapshot.UnitPlanAssignments == null
                || !snapshot.UnitPlanAssignments.TryGetValue(passenger.InstanceId, out AIPlanAssignment assignment)
                || assignment == null
                || assignment.Intent == null)
                continue;

            return AIPlanEvaluator.BuildPlanKey(assignment.Intent);
        }

        return null;
    }

    private static bool HasAnyTransportPassenger(UnitManager transporter)
    {
        if (transporter == null)
            return false;

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null)
            return false;

        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i] != null && seats[i].embarkedUnit != null)
                return true;
        }

        return false;
    }

    private static bool HasAnyAvailableTransportSeat(UnitManager transporter)
    {
        if (transporter == null || !transporter.TryGetUnitData(out UnitData data) || data == null || data.transportSlots == null)
            return false;

        for (int i = 0; i < data.transportSlots.Count; i++)
        {
            UnitTransportSlotRule slot = data.transportSlots[i];
            if (slot == null || slot.capacity <= 0)
                continue;
            if (transporter.GetOccupiedTransportSeatCountForSlot(i) < slot.capacity)
                return true;
        }

        return false;
    }

    private static int GetHexDistance(Vector3Int a, Vector3Int b)
    {
        a.z = 0;
        b.z = 0;
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static Vector3Int GetTransportPlanAnchorCell(AIPlanIntent plan)
    {
        Vector3Int anchor = plan != null && plan.HasCaptureTarget ? plan.CaptureTargetCell : Vector3Int.zero;
        anchor.z = 0;
        return anchor;
    }
    private static bool HasLogisticsCapability(UnitManager unit, UnitData data) { if (unit == null || data == null) return false; if (data.isSupplier) return true; return data.aiUnitProfile != null && data.aiUnitProfile.HasPlanCapability(AIPlanCapability.Logistics, data); }
    private static bool IsCombatRelevantForSupplyPressure(UnitData data) { if (data == null || data.isSupplier) return false; return data.CombatClassification == UnitCombatClassification.Combatente || data.CombatClassification == UnitCombatClassification.Artilheiro || data.CombatClassification == UnitCombatClassification.Hibrido; }
    private static bool IsLowAutonomyForSupplyPressure(UnitManager unit) { if (unit == null) return false; int maxFuel = unit.GetMaxFuel(); if (maxFuel <= 0) return false; int threshold = Mathf.Max(5, Mathf.CeilToInt(maxFuel * 0.25f)); return unit.CurrentFuel <= threshold; }
    private static bool HasNoCombatAmmoForSupplyPressure(UnitManager unit) {
        if (unit == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = unit.GetEmbarkedWeapons();
        if (runtimeWeapons != null && unit.TryGetUnitData(out UnitData data) && data != null && data.embarkedWeapons != null)
        {
            bool hasAmmoBasedWeapon = false;
            bool allAmmoBasedWeaponsEmpty = true;
            int count = Mathf.Min(runtimeWeapons.Count, data.embarkedWeapons.Count);
            for (int i = 0; i < count; i++)
            {
                UnitEmbarkedWeapon runtime = runtimeWeapons[i];
                UnitEmbarkedWeapon baseline = data.embarkedWeapons[i];
                if (runtime == null || baseline == null || baseline.weapon == null)
                    continue;

                int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
                if (maxAmmo <= 0)
                    continue;

                hasAmmoBasedWeapon = true;
                if (runtime.squadAmmunition > 0)
                {
                    allAmmoBasedWeaponsEmpty = false;
                    break;
                }
            }

            if (hasAmmoBasedWeapon)
                return allAmmoBasedWeaponsEmpty;
        }

        return unit.GetMaxAmmo() > 0 && unit.CurrentAmmo <= 0;
    }
    private static bool IsFrontlineAssignedUnit(AISnapshot snapshot, UnitManager unit) { if (snapshot == null || unit == null || snapshot.UnitPlanAssignments == null) return false; if (!snapshot.UnitPlanAssignments.TryGetValue(unit.InstanceId, out AIPlanAssignment assignment) || assignment == null) return false; return assignment.Role == AIPlanRole.Capture || assignment.Role == AIPlanRole.Escort || assignment.Role == AIPlanRole.Artillery || assignment.Role == AIPlanRole.Assault; }
    private static List<Candidate> CollectCapabilityCandidatesFromGroups(ConstructionManager construction, IReadOnlyList<AIDataGroup> groups, AIPlanCapability capability, int currentMoney, int incomePerTurn) { var list = new List<Candidate>(); if (construction == null || groups == null) return list; int ladderRank = 0; for (int g = 0; g < groups.Count; g++) { var group = groups[g]; if (group == null || group.specificUnits == null || group.specificUnits.Count == 0) continue; for (int i = 0; i < group.specificUnits.Count; i++) { var wanted = group.specificUnits[i]; if (wanted == null || string.IsNullOrWhiteSpace(wanted.id)) continue; if (!TryGetOfferIndex(construction, wanted, out int offerIndex, out UnitData offer) || offer == null) continue; if (offer.aiUnitProfile == null || !offer.aiUnitProfile.HasPlanCapability(capability, offer)) continue; if (capability == AIPlanCapability.Transport && !HasTransportCapability(offer)) continue; int cost = Mathf.Max(0, offer.cost); list.Add(new Candidate { unit = offer, offerIndex = offerIndex, cost = cost, groupPriority = group.priority, affordableNow = cost <= currentMoney, turnsToAfford = TurnsToAfford(cost, currentMoney, incomePerTurn), ladderRank = ladderRank++, sourceLabel = "grupo:" + group.label, fromFallback = false }); } } return list; }
    private static List<Candidate> CollectCapabilityCandidatesFromFallback(ConstructionManager construction, AIDataMode mode, AIPlanCapability capability, int currentMoney, int incomePerTurn) { var list = new List<Candidate>(); if (construction == null || mode == null || mode.fallbackUnits == null) return list; int ladderRank = 100000; for (int i = 0; i < mode.fallbackUnits.Count; i++) { var wanted = mode.fallbackUnits[i]; if (wanted == null || string.IsNullOrWhiteSpace(wanted.id)) continue; if (!TryGetOfferIndex(construction, wanted, out int offerIndex, out UnitData offer) || offer == null) continue; if (offer.aiUnitProfile == null || !offer.aiUnitProfile.HasPlanCapability(capability, offer)) continue; if (capability == AIPlanCapability.Transport && !HasTransportCapability(offer)) continue; int cost = Mathf.Max(0, offer.cost); list.Add(new Candidate { unit = offer, offerIndex = offerIndex, cost = cost, groupPriority = int.MaxValue, affordableNow = cost <= currentMoney, turnsToAfford = TurnsToAfford(cost, currentMoney, incomePerTurn), ladderRank = ladderRank++, sourceLabel = "fallback", fromFallback = true }); } return list; }
    private static bool Better(Candidate a, Candidate b) { if (a.fromFallback != b.fromFallback) return !a.fromFallback; if (a.ladderRank != b.ladderRank) return a.ladderRank < b.ladderRank; if (a.cost != b.cost) return a.cost > b.cost; if (a.groupPriority != b.groupPriority) return a.groupPriority < b.groupPriority; return a.offerIndex < b.offerIndex; }
    private static int TurnsToAfford(int cost, int currentMoney, int incomePerTurn) { if (currentMoney >= cost) return 0; return Mathf.CeilToInt((Mathf.Max(0, cost) - currentMoney) / (float)Mathf.Max(1, incomePerTurn)); }
    private static bool TryFindFutureGroupCandidate(Ctx ctx, AIDataGroup group, int currentMoney, out Candidate future) { future = default; if (ctx == null || group == null || group.specificUnits == null) return false; bool found = false; int groupIndex = 0; for (int i = 0; i < ctx.groups.Count; i++) { if (ctx.groups[i] == group) { groupIndex = i; break; } } for (int i = 0; i < ctx.producers.Count; i++) { var info = ctx.producers[i]; if (info == null || info.Source == null) continue; for (int u = 0; u < group.specificUnits.Count; u++) { var wanted = group.specificUnits[u]; if (wanted == null || string.IsNullOrWhiteSpace(wanted.id)) continue; if (!TryGetOfferIndex(info.Source, wanted, out int index, out UnitData offer) || offer == null) continue; int cost = Mathf.Max(0, offer.cost); var c = new Candidate { unit = offer, offerIndex = index, cost = cost, groupPriority = group.priority, affordableNow = cost <= currentMoney, turnsToAfford = TurnsToAfford(cost, currentMoney, ctx.income), ladderRank = (groupIndex * 1000) + u, sourceLabel = "grupo:" + group.label, fromFallback = false }; if (c.affordableNow) continue; if (!found || Better(c, future)) { future = c; found = true; } } } return found; }
    private static string Reason(AIShoppingTurnPlan plan, string branch, string detail) { var cap = FindCapabilityDemand(plan, AIPlanCapability.Capture); var esc = FindCapabilityDemand(plan, AIPlanCapability.Escort); var fs = FindCapabilityDemand(plan, AIPlanCapability.FireSupport); var trn = FindCapabilityDemand(plan, AIPlanCapability.Transport); var log = FindCapabilityDemand(plan, AIPlanCapability.Logistics); return string.Format("shopping-demand: CAP={0}/{1} ESC={2}/{3} FS={4}/{5} TRN={6}/{7} LOG={8}/{9} crit={10} units={11} prod={12} budget={13}/{14} strategic={15} | branch={16} | {17}", cap != null ? cap.missingCount : 0, cap != null ? cap.totalDemand : 0, esc != null ? esc.missingCount : 0, esc != null ? esc.totalDemand : 0, fs != null ? fs.missingCount : 0, fs != null ? fs.totalDemand : 0, trn != null ? trn.missingCount : 0, trn != null ? trn.totalDemand : 0, log != null ? log.missingCount : 0, log != null ? log.totalDemand : 0, plan.hasCriticalCaptureGap ? 1 : 0, plan.friendlyUnitCount, plan.activeProductionBuildings, plan.budget.reservedMoney, plan.budget.totalMoney, plan.budget.strategicReserveMoney, branch, detail); }
    private static void TrackReserved(Ctx ctx, string unitId) { if (string.IsNullOrWhiteSpace(unitId)) return; if (!ctx.reservedUnitCounts.TryGetValue(unitId, out int c)) c = 0; ctx.reservedUnitCounts[unitId] = c + 1; }
    private static int GroupCount(Ctx ctx, AIDataGroup group) { int count = ctx.groupCounts.TryGetValue(group, out int c) ? c : 0; foreach (var kv in ctx.reservedUnitCounts) if (kv.Value > 0 && GroupContainsUnitId(group, kv.Key)) count += kv.Value; return count; }
    private static List<AIConstructionInfo> CollectProducers(AISnapshot snapshot, TeamId aiTeam) { var list = new List<AIConstructionInfo>(); if (snapshot == null || snapshot.KnownConstructions == null) return list; for (int i = 0; i < snapshot.KnownConstructions.Count; i++) { var info = snapshot.KnownConstructions[i]; if (info == null || info.Source == null) continue; if (info.TeamId != aiTeam || !info.CanProduceUnits) continue; list.Add(info); } list.Sort((a, b) => GetProducerPriority(a).CompareTo(GetProducerPriority(b))); return list; }
    private static int GetProducerPriority(AIConstructionInfo info) { string label = info != null ? (!string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : (info.Source != null ? info.Source.name : string.Empty)) : string.Empty; return !string.IsNullOrWhiteSpace(label) && label.IndexOf("hq", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1; }
    private static int CountActiveProductionBuildings(AISnapshot snapshot, TeamId aiTeam) { int count = 0; if (snapshot == null || snapshot.KnownConstructions == null) return 0; for (int i = 0; i < snapshot.KnownConstructions.Count; i++) { var info = snapshot.KnownConstructions[i]; if (info != null && info.Source != null && info.TeamId == aiTeam && info.CanProduceUnits) count++; } return count; }
    private static int CountAssignedRole(AIPlanIntent intent, AIPlanRole role) { int count = 0; if (intent == null || intent.Assignments == null) return 0; for (int i = 0; i < intent.Assignments.Count; i++) { var a = intent.Assignments[i]; if (a != null && a.Role == role) count++; } return count; }
    private static int CountOutstandingCaptureTargets(AISnapshot snapshot, ConstructionSector sector) { int count = 0; if (snapshot == null || snapshot.KnownConstructions == null) return 0; for (int i = 0; i < snapshot.KnownConstructions.Count; i++) { var info = snapshot.KnownConstructions[i]; if (info == null || info.Sector != sector || !info.IsCapturable) continue; if (info.TeamId == snapshot.AiTeam && info.CapturePoints >= info.CapturePointsMax) continue; count++; } return count; }
    private static List<AIDataGroup> GetGroupsByPriority(IReadOnlyList<AIDataGroup> groups) { var ordered = new List<AIDataGroup>(); if (groups == null) return ordered; for (int i = 0; i < groups.Count; i++) if (groups[i] != null) ordered.Add(groups[i]); ordered.Sort((a, b) => { int cmp = (a != null ? a.priority : int.MaxValue).CompareTo(b != null ? b.priority : int.MaxValue); return cmp != 0 ? cmp : string.CompareOrdinal(a != null ? a.label : string.Empty, b != null ? b.label : string.Empty); }); return ordered; }
    private static Dictionary<AIDataGroup, int> CountFriendlyUnitsByConfiguredGroup(AISnapshot snapshot, List<AIDataGroup> groups) { var counts = new Dictionary<AIDataGroup, int>(); if (groups == null) return counts; for (int i = 0; i < groups.Count; i++) counts[groups[i]] = 0; if (snapshot == null || snapshot.FriendlyUnits == null) return counts; for (int i = 0; i < snapshot.FriendlyUnits.Count; i++) { var unit = snapshot.FriendlyUnits[i]; if (unit == null || unit.IsDead || string.IsNullOrWhiteSpace(unit.UnitId)) continue; for (int g = 0; g < groups.Count; g++) { var group = groups[g]; if (group == null || group.specificUnits == null || group.specificUnits.Count == 0) continue; if (!GroupContainsUnitId(group, unit.UnitId)) continue; counts[group] = counts[group] + 1; break; } } return counts; }
    private static bool GroupContainsUnitId(AIDataGroup group, string unitId) { if (group == null || group.specificUnits == null || string.IsNullOrWhiteSpace(unitId)) return false; for (int i = 0; i < group.specificUnits.Count; i++) { var c = group.specificUnits[i]; if (c != null && !string.IsNullOrWhiteSpace(c.id) && string.Equals(c.id, unitId, StringComparison.OrdinalIgnoreCase)) return true; } return false; }
    private static bool TryResolveFirstAffordableFromGroup(ConstructionManager construction, AIDataGroup group, int money, out int targetIndex, out string unitId, out int cost) { targetIndex = -1; unitId = null; cost = 0; if (construction == null || group == null || group.specificUnits == null) return false; for (int i = 0; i < group.specificUnits.Count; i++) { var wanted = group.specificUnits[i]; if (wanted == null || string.IsNullOrWhiteSpace(wanted.id)) continue; if (!TryGetAffordableOfferIndex(construction, wanted, money, out int index, out UnitData offer)) continue; targetIndex = index; unitId = offer != null ? offer.id : wanted.id; cost = offer != null ? Mathf.Max(0, offer.cost) : 0; return true; } return false; }
    private static bool TryResolveFirstAffordableFromUnitList(ConstructionManager construction, IReadOnlyList<UnitData> units, int money, out int targetIndex, out string unitId, out int cost) { targetIndex = -1; unitId = null; cost = 0; if (construction == null || units == null) return false; for (int i = 0; i < units.Count; i++) { var wanted = units[i]; if (wanted == null || string.IsNullOrWhiteSpace(wanted.id)) continue; if (!TryGetAffordableOfferIndex(construction, wanted, money, out int index, out UnitData offer)) continue; targetIndex = index; unitId = offer != null ? offer.id : wanted.id; cost = offer != null ? Mathf.Max(0, offer.cost) : 0; return true; } return false; }
    private static bool TryResolveAnyAffordableOffer(ConstructionManager construction, int money, out int targetIndex, out string unitId, out int cost) { targetIndex = -1; unitId = null; cost = 0; var offered = construction != null ? construction.OfferedUnits : null; if (offered == null) return false; for (int i = 0; i < offered.Count; i++) { var unit = offered[i]; if (unit == null) continue; int c = Mathf.Max(0, unit.cost); if (c > money) continue; targetIndex = i; unitId = unit.id; cost = c; return true; } return false; }
    private static bool TryGetAffordableOfferIndex(ConstructionManager construction, UnitData wanted, int money, out int index, out UnitData offer) { index = -1; offer = null; if (!TryGetOfferIndex(construction, wanted, out int found, out UnitData unit) || unit == null) return false; if (money < Mathf.Max(0, unit.cost)) return false; index = found; offer = unit; return true; }
    private static bool TryGetOfferIndex(ConstructionManager construction, UnitData wanted, out int index, out UnitData offer) { index = -1; offer = null; var offered = construction != null ? construction.OfferedUnits : null; if (wanted == null || offered == null) return false; string wantedId = wanted.id; for (int i = 0; i < offered.Count; i++) { var unit = offered[i]; if (unit == null) continue; if (ReferenceEquals(unit, wanted) || (!string.IsNullOrWhiteSpace(wantedId) && !string.IsNullOrWhiteSpace(unit.id) && string.Equals(unit.id, wantedId, StringComparison.OrdinalIgnoreCase))) { index = i; offer = unit; return true; } } return false; }
    private static string PlanKey(AIPlanIntent p) { if (p == null) return "plan:null"; string label = !string.IsNullOrWhiteSpace(p.DisplayName) ? p.DisplayName : p.Sector.ToString(); return string.Format("{0}|{1}|{2}", p.Sector, label, p.HasCaptureTarget ? p.CaptureTargetCell.ToString() : "no-target"); }
    private static string PlanLabel(AIPlanIntent p) { if (p == null) return "(null)"; return !string.IsNullOrWhiteSpace(p.DisplayName) ? p.DisplayName : p.Sector.ToString(); }
    private static bool IsProtectPlan(AIPlanIntent intent) { return intent != null && !string.IsNullOrWhiteSpace(intent.SelectionReason) && (intent.SelectionReason.IndexOf("sector-hold", StringComparison.OrdinalIgnoreCase) >= 0 || intent.SelectionReason.IndexOf("backup-retake", StringComparison.OrdinalIgnoreCase) >= 0); }
}





