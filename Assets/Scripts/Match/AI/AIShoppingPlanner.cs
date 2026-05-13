using System.Collections.Generic;
using UnityEngine;

public class AIShoppingPlanner : MonoBehaviour
{
    private const int DefensiveBaseThreatRange = 3;
    private const int DefensiveArmorThreatRange = 5;
    private const int DefensiveLowTroopCountThreshold = 7;

    public struct ShoppingOrder
    {
        public ConstructionManager Building;
        public UnitData UnitToBuy;
        public int SelectedIndex;
    }

    [Header("Debug")]
    public bool onlyCapturers;
    public bool onlyAssault;
    public bool onlyTransporter;
    public bool onlyLogistics;
    public bool onlyFireSupport;

    [Header("Economia")]
    [Range(0f, 20f)] public float SavingPercentualForElite = 15f;
    [Range(0f, 1f)]  public float EliteCapturerFillRatio   = 0.6f;
    [Range(0, 5)]    public int   MinFilledAssaultSlots     = 1;
    [Range(1, 12)]   public int   MinTurnForFireSupport     = 3;
    [Range(0, 8)]    public int   MinActiveCapturersForFireSupport = 2;
    [Range(0, 5)]    public int   MinActiveAssaultForFireSupport   = 1;
    [Range(2, 8)]    public int   CapturersPerPreventiveTransport = 4;
    [Range(1, 4)]    public int   ProgressiveCapturerBatchSize = 2;

    private static AIShoppingPlanner instance;
    public static AIShoppingPlanner Instance => EnsureInstance();

    private static AIShoppingPlanner EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<AIShoppingPlanner>();
        if (instance != null) return instance;
        GameObject go = new GameObject(nameof(AIShoppingPlanner));
        instance = go.AddComponent<AIShoppingPlanner>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static List<ShoppingOrder> Decide(AIWorldSnapshot snapshot)
    {
        bool onlyCapturers    = Instance != null && Instance.onlyCapturers;
        bool onlyAssault      = Instance != null && Instance.onlyAssault;
        bool onlyTransporter  = Instance != null && Instance.onlyTransporter;
        bool onlyLogistics    = Instance != null && Instance.onlyLogistics;
        bool onlyFireSupport  = Instance != null && Instance.onlyFireSupport;

        var orders = new List<ShoppingOrder>();
        if (snapshot == null) return orders;

        var occupied  = new HashSet<Vector3Int>(snapshot.OccupiedCells);
        int remaining = snapshot.Budget;
        int openCapturerSlots   = CountOpenSlots(snapshot.AITeam, UnitRole.Capturador);
        int openAssaultSlots    = CountOpenSlots(snapshot.AITeam, UnitRole.Assalto);
        int openTransportSlots = ComputeTransportDemand(snapshot, out bool urgentTransportDemand);
        int openFireSupportSlots = ComputeFireSupportDemand(snapshot, openCapturerSlots, openAssaultSlots, out bool preferDefensiveFireSupport);
        int openLogisticsSlots = ComputeLogisticsDemand(snapshot, out int repairDemandCount, out int activeLogisticsCount);
        if (openAssaultSlots <= 0
            && !HasActivePrimaryRole(snapshot, UnitRole.Assalto)
            && CanAffordPurePrimaryRole(snapshot, UnitRole.Assalto, remaining))
        {
            openAssaultSlots = 1;
        }
        openCapturerSlots = LimitCapturerDemandForProgression(snapshot, openCapturerSlots, openAssaultSlots, openTransportSlots, openLogisticsSlots, openFireSupportSlots);

        // Escalada: se já tem N tanks elite em campo, mira o nível acima na próxima compra.
        // Usa contagem de unidades em campo, não slots do plano (plano fica vazio em modo rogue).
        int activeEliteAssaultCount = CountActiveEliteAssaultUnits(snapshot);
        const int DreamTeamEliteAssaultThreshold = 2;
        int eliteAssaultTargetLevel = activeEliteAssaultCount >= DreamTeamEliteAssaultThreshold ? 2 : 1;
        UnitData eliteAssaultTarget = FindEliteAssaultReserveTarget(snapshot, eliteAssaultTargetLevel);
        if (eliteAssaultTarget == null && eliteAssaultTargetLevel > 1)
            eliteAssaultTarget = FindEliteAssaultReserveTarget(snapshot, 1); // fallback se nível 2 indisponível

        // Reserva elite apenas quando a composição mínima do exército já foi atingida:
        // proporção dos slots de capturador preenchidos >= EliteCapturerFillRatio
        // E pelo menos MinFilledAssaultSlots slots de assault preenchidos.
        if (eliteAssaultTarget != null)
        {
            CountSlots(snapshot.AITeam, UnitRole.Capturador, out int totalCap, out int filledCap);
            CountSlots(snapshot.AITeam, UnitRole.Assalto,    out int totalAss, out int filledAss);
            float capFill       = totalCap > 0 ? filledCap / (float)totalCap : 0f;
            float fillThreshold = Instance != null ? Instance.EliteCapturerFillRatio : 0.6f;
            int   minAssault    = Instance != null ? Instance.MinFilledAssaultSlots   : 1;
            bool  capOk         = capFill >= fillThreshold;
            bool  assOk         = filledAss >= minAssault;
            string status       = (capOk && assOk) ? "ELITE LIBERADO" : $"bloqueado ({(!capOk ? $"cap {filledCap}/{totalCap} {capFill:P0}<{fillThreshold:P0}" : "cap OK")} | {(!assOk ? $"ass {filledAss}<{minAssault}" : "ass OK")})";
            Debug.Log($"[AI Shopping] composição: cap={filledCap}/{totalCap} ({capFill:P0}) ass={filledAss}/{totalAss} — {status}");
            if (!capOk || !assOk)
                eliteAssaultTarget = null;
        }

        if (eliteAssaultTarget != null && openAssaultSlots <= 0 && remaining >= eliteAssaultTarget.cost)
        {
            openAssaultSlots = 1;
            Debug.Log($"[AI Shopping] elite excedente liberado: {eliteAssaultTarget.displayName} custo={eliteAssaultTarget.cost} cash={remaining}");
        }

        UnitData eliteFireSupportTarget = FindEliteFireSupportReserveTarget(snapshot, preferDefensiveFireSupport, remaining);
        int activeFireSupportCount = CountActiveUnitsWithRole(snapshot, UnitRole.FogoIndireto, requirePrimary: false);

        // Dream team pivot: once the AI has N+ elite assault units in the field, stop
        // adding more elite tanks and invest in elite defensive fire support instead.
        // Uses field unit count, not plan slots — plan slots are empty in rogue mode.
        int activeEliteFireSupportCount = CountActiveEliteFireSupportUnits(snapshot);
        int desiredEliteFireSupport = activeEliteAssaultCount / DreamTeamEliteAssaultThreshold;
        bool dreamTeamPivot = activeEliteAssaultCount >= DreamTeamEliteAssaultThreshold
            && activeEliteFireSupportCount < desiredEliteFireSupport;
        if (dreamTeamPivot)
        {
            preferDefensiveFireSupport = true;
            eliteFireSupportTarget = FindEliteFireSupportReserveTarget(snapshot, preferDefensiveFireSupport: true, remaining);
            Debug.Log($"[AI Shopping] dream_team_pivot: {activeEliteAssaultCount} elite assault em campo → target={eliteFireSupportTarget?.displayName ?? "nenhum"} custo={eliteFireSupportTarget?.cost ?? 0}");
        }

        bool eliteFireSupportReserveReady = IsEliteFireSupportReserveReady(snapshot);
        bool eliteFireSupportNowAffordable = eliteFireSupportTarget != null && remaining >= eliteFireSupportTarget.cost;
        bool wantsEliteFireSupport = eliteFireSupportTarget != null
            && (eliteFireSupportReserveReady || eliteFireSupportNowAffordable)
            && (dreamTeamPivot || (activeFireSupportCount > 0 && activeFireSupportCount < 2));
        int reserveForEliteFireSupport = 0;
        bool eliteFireSupportBought = false;
        bool emergencyProductionDefense = TryFindEmergencyProductionDefensePurchase(snapshot, remaining, out UnitData emergencyDefenseTarget, out int emergencyContestedOwned);
        if (emergencyProductionDefense)
        {
            eliteFireSupportTarget = emergencyDefenseTarget;
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = true;
            wantsEliteFireSupport = true;
            eliteFireSupportNowAffordable = true;
            Debug.Log($"[AI Shopping] emergencia fabrica: construcoes_contestadas={emergencyContestedOwned} cash={remaining} compra_prioritaria={emergencyDefenseTarget.displayName} custo={emergencyDefenseTarget.cost}");
        }
        if (wantsEliteFireSupport)
        {
            if (remaining >= eliteFireSupportTarget.cost)
            {
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
                Debug.Log($"[AI Shopping] elite fire_support liberado: {eliteFireSupportTarget.displayName} custo={eliteFireSupportTarget.cost} cash={remaining}");
            }
            else
            {
                reserveForEliteFireSupport = remaining;
                Debug.Log($"[AI Shopping] reserva elite fire_support {eliteFireSupportTarget.displayName} elite={eliteFireSupportTarget.eliteLevel} custo={eliteFireSupportTarget.cost} cash={remaining} income={snapshot.IncomePerTurn} reserva={reserveForEliteFireSupport}");
            }
        }

        int cheapestTransportCost = openTransportSlots > 0 ? FindCheapestAvailableTransportCost(snapshot) : 0;

        Debug.Log($"[AI Shopping] budget={remaining} cap_slots={openCapturerSlots} ass_slots={openAssaultSlots} trans_slots={openTransportSlots} trans_urgent={urgentTransportDemand} log_slots={openLogisticsSlots} repairs={repairDemandCount} active_log={activeLogisticsCount} fire_slots={openFireSupportSlots} fire_def={preferDefensiveFireSupport} cheapest_transport={cheapestTransportCost} onlyCap={onlyCapturers} onlyAss={onlyAssault} onlyTrans={onlyTransporter} onlyLog={onlyLogistics} onlyFire={onlyFireSupport}");

        bool strategicEliteAssaultReserve = eliteAssaultTarget != null
            && !dreamTeamPivot
            && openCapturerSlots <= 0
            && openAssaultSlots <= 0
            && openTransportSlots <= 0
            && openLogisticsSlots <= 0
            && openFireSupportSlots <= 0
            && IsEliteAssaultReserveReady(snapshot);
        bool nextTurnEliteAssaultReserve = eliteAssaultTarget != null
            && !dreamTeamPivot
            && remaining < eliteAssaultTarget.cost
            && remaining + Mathf.Max(0, snapshot.IncomePerTurn) >= eliteAssaultTarget.cost
            && IsEliteAssaultReserveReady(snapshot);
        bool wantsEliteAssault = eliteAssaultTarget != null
            && (openAssaultSlots > 0 || strategicEliteAssaultReserve || nextTurnEliteAssaultReserve);
        if (strategicEliteAssaultReserve)
            Debug.Log($"[AI Shopping] reserva estrategica elite assalto: composicao completa alvo={eliteAssaultTarget.displayName} custo={eliteAssaultTarget.cost}");
        else if (nextTurnEliteAssaultReserve)
            Debug.Log($"[AI Shopping] reserva proximo turno elite assalto: alvo={eliteAssaultTarget.displayName} custo={eliteAssaultTarget.cost} cash={remaining} income={snapshot.IncomePerTurn}");
        bool eliteAssaultBought = false;
        bool defensiveBaseTankBought = false;
        int defensiveBaseResponseReserveCost = FindCheapestDefensiveBaseThreatPurchaseCost(snapshot);
        int defensiveBaseBasicMassCost = FindCheapestDefensiveBaseBasicMassPurchaseCost(snapshot);
        int defensiveArmorThreatCount = CountVisibleEnemyArmorNearOwnedBase(snapshot, DefensiveArmorThreatRange);
        bool defensiveArmorThreat = defensiveArmorThreatCount > 0;
        if (defensiveArmorThreat)
            Debug.Log($"[AI Shopping] defesa blindada: {defensiveArmorThreatCount} armored visivel <= {DefensiveArmorThreatRange}h da base/HQ");
        UnitData eliteDefensiveTankTarget = defensiveArmorThreat ? FindEliteDefensiveTankReserveTarget(snapshot) : null;
        int reserveForEliteDefensiveTank = 0;
        if (eliteDefensiveTankTarget != null && remaining < eliteDefensiveTankTarget.cost)
        {
            int nextTurnCash = remaining + Mathf.Max(0, snapshot.IncomePerTurn);
            if (nextTurnCash >= eliteDefensiveTankTarget.cost)
            {
                reserveForEliteDefensiveTank = Mathf.Min(remaining, Mathf.Max(0, eliteDefensiveTankTarget.cost - Mathf.Max(0, snapshot.IncomePerTurn)));
                Debug.Log($"[AI Shopping] reserva tank elite {eliteDefensiveTankTarget.displayName} custo={eliteDefensiveTankTarget.cost} cash={remaining} income={snapshot.IncomePerTurn} reserva={reserveForEliteDefensiveTank} gastoLivre={Mathf.Max(0, remaining - reserveForEliteDefensiveTank)}");
            }
        }
        UnitData supremeFireSupport = null;
        if (defensiveArmorThreat)
        {
            supremeFireSupport = FindAffordableSupremeDefensiveFireSupportTarget(snapshot, remaining);
            if (supremeFireSupport != null)
            {
                eliteFireSupportTarget = supremeFireSupport;
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
                preferDefensiveFireSupport = true;
                wantsEliteFireSupport = true;
                reserveForEliteFireSupport = 0;
                Debug.Log($"[AI Shopping] defesa blindada suprema: priorizando fire_support elite{supremeFireSupport.eliteLevel} {supremeFireSupport.displayName} custo={supremeFireSupport.cost} cash={remaining}");
            }
        }
        bool needsAffordableArmorFallback = defensiveArmorThreat
            && supremeFireSupport == null
            && reserveForEliteDefensiveTank <= 0
            && !CanAffordEliteDefensiveTank(snapshot, remaining);
        if (needsAffordableArmorFallback)
        {
            UnitData armorFallbackFireSupport = FindAffordableArmorFallbackFireSupportTarget(snapshot, remaining);
            if (armorFallbackFireSupport != null)
            {
                eliteFireSupportTarget = armorFallbackFireSupport;
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
                preferDefensiveFireSupport = true;
                wantsEliteFireSupport = true;
                reserveForEliteFireSupport = 0;
                Debug.Log($"[AI Shopping] defesa blindada fallback: sem caixa para elite tank, tentando apoio anti-blindado {armorFallbackFireSupport.displayName} custo={armorFallbackFireSupport.cost} cash={remaining}");
            }
        }
        int reserveForEliteAssault = 0;
        int eliteAssaultSafetyBuffer = 0;
        if (wantsEliteAssault && remaining < eliteAssaultTarget.cost)
        {
            int nextTurnCash = remaining + Mathf.Max(0, snapshot.IncomePerTurn);
            if (nextTurnCash >= eliteAssaultTarget.cost)
            {
                int baseReserve = Mathf.Max(0, eliteAssaultTarget.cost - Mathf.Max(0, snapshot.IncomePerTurn));
                eliteAssaultSafetyBuffer = CalculateEliteAssaultSafetyBuffer(eliteAssaultTarget);
                reserveForEliteAssault = Mathf.Min(remaining, baseReserve + eliteAssaultSafetyBuffer);
            }
        }

        if (reserveForEliteAssault > 0)
        {
            Debug.Log($"[AI Shopping] reserva elite assalto {eliteAssaultTarget.displayName} elite={eliteAssaultTarget.eliteLevel} custo={eliteAssaultTarget.cost} cash={remaining} income={snapshot.IncomePerTurn} reserva={reserveForEliteAssault} colchao={eliteAssaultSafetyBuffer} gastoLivre={Mathf.Max(0, remaining - reserveForEliteAssault)}");
        }

        // Ordena fábricas: as mais próximas de slots abertos compram primeiro
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        var sortedBuildings = new List<ConstructionManager>(snapshot.MyBuildings);
        sortedBuildings.Sort((a, b) =>
        {
            int eliteA = wantsEliteAssault && !eliteAssaultBought && CanOfferUnit(a, eliteAssaultTarget) ? 0 : 1;
            int eliteB = wantsEliteAssault && !eliteAssaultBought && CanOfferUnit(b, eliteAssaultTarget) ? 0 : 1;
            if (eliteA != eliteB) return eliteA.CompareTo(eliteB);

            int eliteFireA = wantsEliteFireSupport && !eliteFireSupportBought && CanOfferUnit(a, eliteFireSupportTarget) ? 0 : 1;
            int eliteFireB = wantsEliteFireSupport && !eliteFireSupportBought && CanOfferUnit(b, eliteFireSupportTarget) ? 0 : 1;
            if (eliteFireA != eliteFireB) return eliteFireA.CompareTo(eliteFireB);

            bool manpowerShortage = HasDefensiveBaseManpowerShortage(snapshot);
            bool tankThreat = defensiveArmorThreat || manpowerShortage;
            bool supremeFirePending = defensiveArmorThreat
                && wantsEliteFireSupport
                && !eliteFireSupportBought
                && eliteFireSupportTarget != null
                && eliteFireSupportTarget.eliteLevel >= 2
                && IsDefensiveFireSupportPurchase(eliteFireSupportTarget)
                && remaining >= eliteFireSupportTarget.cost;
            int tankReserve = defensiveArmorThreat ? 0 : Mathf.Max(0, defensiveBaseBasicMassCost) * 2;
            int tankA = !supremeFirePending && tankThreat && (defensiveArmorThreat || HasVisibleEnemyNearBase(a, snapshot, DefensiveBaseThreatRange))
                && CanOfferAffordableDefensiveTank(a, remaining, tankReserve) ? 0 : 1;
            int tankB = !supremeFirePending && tankThreat && (defensiveArmorThreat || HasVisibleEnemyNearBase(b, snapshot, DefensiveBaseThreatRange))
                && CanOfferAffordableDefensiveTank(b, remaining, tankReserve) ? 0 : 1;
            if (tankA != tankB) return tankA.CompareTo(tankB);

            int transA = urgentTransportDemand && openTransportSlots > 0 && cheapestTransportCost > 0 && remaining >= cheapestTransportCost
                && CanOfferPrimaryRoleUnit(a, UnitRole.Transportador) ? 0 : 1;
            int transB = urgentTransportDemand && openTransportSlots > 0 && cheapestTransportCost > 0 && remaining >= cheapestTransportCost
                && CanOfferPrimaryRoleUnit(b, UnitRole.Transportador) ? 0 : 1;
            if (transA != transB) return transA.CompareTo(transB);

            int logA = openLogisticsSlots > 0 && CanOfferPrimaryRoleUnit(a, UnitRole.Logistica) ? 0 : 1;
            int logB = openLogisticsSlots > 0 && CanOfferPrimaryRoleUnit(b, UnitRole.Logistica) ? 0 : 1;
            if (logA != logB) return logA.CompareTo(logB);

            int fireA = openFireSupportSlots > 0 && CanOfferFireSupportUnit(a) ? 0 : 1;
            int fireB = openFireSupportSlots > 0 && CanOfferFireSupportUnit(b) ? 0 : 1;
            if (fireA != fireB) return fireA.CompareTo(fireB);

            return GetMinDistanceToOpenObjective(a, plan, snapshot.AITeam)
                .CompareTo(GetMinDistanceToOpenObjective(b, plan, snapshot.AITeam));
        });

        foreach (ConstructionManager building in sortedBuildings)
        {
            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            if (!building.CanProduceUnitsForTeam(snapshot.AITeam))
            {
                Debug.Log($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} — não produz para o time, pulando");
                continue;
            }
            if (occupied.Contains(cell))
            {
                Debug.Log($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} — célula ocupada, pulando");
                continue;
            }

            bool defensiveBaseThreat = HasVisibleEnemyNearBase(building, snapshot, DefensiveBaseThreatRange)
                || defensiveArmorThreat
                || emergencyProductionDefense;
            bool allowDefensiveEliteAssault = defensiveBaseThreat
                && wantsEliteAssault
                && !eliteAssaultBought
                && CanOfferUnit(building, eliteAssaultTarget)
                && remaining >= eliteAssaultTarget.cost + CalculateEliteAssaultSafetyBuffer(eliteAssaultTarget);
            bool defensiveBaseManpowerShortage = defensiveBaseThreat
                && (HasDefensiveBaseManpowerShortage(snapshot) || defensiveArmorThreat);
            int defensiveTankReserveCost = defensiveArmorThreat ? 0 : defensiveBaseResponseReserveCost;
            int defensiveMassReserveCost = defensiveArmorThreat ? 0 : defensiveBaseBasicMassCost;
            int spendBudget = remaining;
            if (defensiveArmorThreat
                && reserveForEliteDefensiveTank > 0
                && (eliteDefensiveTankTarget == null || remaining < eliteDefensiveTankTarget.cost))
            {
                spendBudget = Mathf.Max(0, remaining - reserveForEliteDefensiveTank);
            }
            if (!defensiveBaseThreat && wantsEliteAssault && !eliteAssaultBought)
            {
                if (remaining >= eliteAssaultTarget.cost)
                    spendBudget = CanOfferUnit(building, eliteAssaultTarget) ? remaining : 0;
                else if (reserveForEliteAssault > 0)
                    spendBudget = Mathf.Max(0, remaining - reserveForEliteAssault);
            }
            if (!defensiveBaseThreat && wantsEliteFireSupport && !eliteFireSupportBought)
            {
                if (remaining >= eliteFireSupportTarget.cost)
                    spendBudget = CanOfferUnit(building, eliteFireSupportTarget) ? spendBudget : 0;
                else if (reserveForEliteFireSupport > 0)
                    spendBudget = Mathf.Min(spendBudget, Mathf.Max(0, remaining - reserveForEliteFireSupport));
            }

            // Log das opções deste edifício
            {
                var offerLog = new System.Text.StringBuilder();
                offerLog.Append($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} budget={spendBudget}/{remaining}:");
                if (building.OfferedUnits != null)
                    foreach (UnitData ou in building.OfferedUnits)
                        if (ou != null) offerLog.Append($" [{ou.displayName} ${ou.cost}]");
                Debug.Log(offerLog.ToString());
            }

            UnitData unit = PickUnit(building, snapshot, spendBudget, onlyCapturers, onlyAssault, onlyTransporter, onlyLogistics, onlyFireSupport,
                openCapturerSlots, openAssaultSlots, openTransportSlots, urgentTransportDemand, openLogisticsSlots, openFireSupportSlots, preferDefensiveFireSupport,
                eliteAssaultTarget, eliteFireSupportTarget, defensiveBaseThreat,
                allowDefensiveEliteAssault, defensiveTankReserveCost,
                defensiveBaseManpowerShortage, defensiveMassReserveCost, defensiveBaseTankBought,
                defensiveArmorThreat);
            if (unit == null)
            {
                Debug.Log($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} — nenhuma unidade selecionada (sem fit ou sem budget)");
                continue;
            }

            if (defensiveBaseThreat)
            {
                string heavyStatus = IsDefensiveBaseAssaultTankPurchase(unit)
                    ? $" tanque-ok reserva={defensiveTankReserveCost}"
                    : IsDefensiveBaseBasicMassPurchase(unit) ? " numeros-ok"
                    : allowDefensiveEliteAssault ? " elite-ok" : string.Empty;
                string threatLabel = defensiveArmorThreat ? $" armored<={DefensiveArmorThreatRange}h" : " inimigo<=3h";
                Debug.Log($"[AI Shopping] defesa base {building.ConstructionDisplayName}{threatLabel}{heavyStatus} -> {unit.displayName}");
            }

            int idx = IndexOf(building.OfferedUnits, unit);
            orders.Add(new ShoppingOrder
            {
                Building      = building,
                UnitToBuy     = unit,
                SelectedIndex = idx,
            });

            remaining -= unit.cost;
            occupied.Add(cell);
            if (IsPrimaryRole(unit, UnitRole.Capturador) && openCapturerSlots > 0)
                openCapturerSlots--;
            else if (IsPrimaryRole(unit, UnitRole.Assalto) && openAssaultSlots > 0)
                openAssaultSlots--;
            else if (IsPrimaryRole(unit, UnitRole.Transportador) && openTransportSlots > 0)
                openTransportSlots--;
            else if (IsPrimaryRole(unit, UnitRole.Logistica) && openLogisticsSlots > 0)
                openLogisticsSlots--;
            else if (IsFireSupportPurchase(unit) && openFireSupportSlots > 0)
                openFireSupportSlots--;
            if (unit == eliteAssaultTarget)
                eliteAssaultBought = true;
            if (unit == eliteFireSupportTarget)
                eliteFireSupportBought = true;
            if (IsDefensiveBaseAssaultTankPurchase(unit))
                defensiveBaseTankBought = true;

            if (remaining <= 0) break;
        }

        return orders;
    }

    private static float GetMinDistanceToOpenObjective(ConstructionManager building, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null) return float.MaxValue;
        float minDist = float.MaxValue;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!obj.HasOpenSlot(UnitRole.Capturador) && !obj.HasOpenSlot(UnitRole.Assalto)) continue;
            if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)) continue;

            foreach (SectorManager.SectorTeamDistances td in info.SectorDistances)
            {
                if (td.Team != aiTeam) continue;
                foreach (SectorManager.SectorDistanceEntry e in td.Entries)
                {
                    bool match = building.IsPlayerHeadQuarter
                        ? e.IsHQ
                        : (!e.IsHQ && e.InstanceId == building.InstanceId);
                    if (match && e.Distance < minDist) minDist = e.Distance;
                }
            }
        }

        return minDist;
    }

    private static UnitData PickUnit(
        ConstructionManager building,
        AIWorldSnapshot snapshot,
        int budget,
        bool onlyCapturers,
        bool onlyAssault,
        bool onlyTransporter,
        bool onlyLogistics,
        bool onlyFireSupport,
        int openCapturerSlots,
        int openAssaultSlots,
        int openTransportSlots = 0,
        bool urgentTransportDemand = false,
        int openLogisticsSlots = 0,
        int openFireSupportSlots = 0,
        bool preferDefensiveFireSupport = false,
        UnitData eliteAssaultTarget = null,
        UnitData eliteFireSupportTarget = null,
        bool defensiveBaseThreat = false,
        bool allowDefensiveEliteAssault = false,
        int defensiveBaseResponseReserveCost = 0,
        bool defensiveBaseManpowerShortage = false,
        int defensiveBaseBasicMassCost = 0,
        bool defensiveBaseTankBought = false,
        bool defensiveArmorThreat = false)
    {
        if (building.OfferedUnits == null || building.OfferedUnits.Count == 0) return null;

        bool defensiveStance  = snapshot.Stance == AIStance.Defensive;
        bool hasOpenDefensiveSlot = HasOpenDefensiveSlot(snapshot.AITeam);

        UnitData best      = null;
        int      bestScore = int.MinValue;

        foreach (UnitData u in building.OfferedUnits)
        {
            if (u == null || u.cost > budget) { if (u != null) Debug.Log($"[AI PickUnit] SKIP {u.displayName} ${u.cost} — custo>{budget}"); continue; }
            if (u.domain != Domain.Land) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — domain={u.domain} (não Land)"); continue; }
            if (IsAntiAirOnlyUnit(u) && !HasAnyAirThreat())
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — anti-aerea sem ameaca aerea em campo");
                continue;
            }

            bool isPrimaryCapturer   = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Capturador;
            bool isPrimaryAssault    = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Assalto;
            bool isPrimaryTransporter = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Transportador;
            bool isPrimaryLogistics = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Logistica;
            bool isPrimaryFireSupport = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.FogoIndireto;
            bool isFireSupportCapable = u.roles != null && u.roles.Contains(UnitRole.FogoIndireto);
            bool isHybridCapturer    = isPrimaryAssault && u.roles.Contains(UnitRole.Capturador);
            bool isSecondary       = !isPrimaryCapturer && u.roles != null && u.roles.Contains(UnitRole.Capturador);
            bool fireSupportAllowedNow = openFireSupportSlots > 0 || IsFireSupportAllowedByTiming(snapshot);

            if (isPrimaryCapturer && openCapturerSlots <= 0 && !onlyCapturers)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda capturador");
                continue;
            }

            if (isPrimaryLogistics && openLogisticsSlots <= 0)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda logistics");
                continue;
            }

            if (isFireSupportCapable && !isPrimaryAssault && openFireSupportSlots <= 0)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda fire_support");
                continue;
            }

            if (isFireSupportCapable && isPrimaryAssault && !fireSupportAllowedNow && !defensiveBaseThreat)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} - fire_support cedo demais");
                continue;
            }

            bool isAllowedDefensiveElite = allowDefensiveEliteAssault && u == eliteAssaultTarget;
            bool isAllowedDefensiveFireSupport = openFireSupportSlots > 0
                && ((preferDefensiveFireSupport && IsDefensiveFireSupportPurchase(u))
                    || (defensiveBaseThreat && isFireSupportCapable));
            bool canAffordDefensiveTank = CanAffordDefensiveBaseTankPurchase(u, budget, defensiveBaseResponseReserveCost);
            bool canBuyBasicMass = defensiveBaseManpowerShortage
                && defensiveBaseTankBought
                && IsDefensiveBaseBasicMassPurchase(u);
            bool canBuyLogistics = openLogisticsSlots > 0 && isPrimaryLogistics;
            if (defensiveBaseThreat && canBuyLogistics)
            {
                // Logistics demand remains valid during defense, but scores below direct combat buys.
            }
            else if (defensiveBaseThreat
                && !IsDefensiveBaseThreatPurchase(u)
                && !isAllowedDefensiveElite
                && !isAllowedDefensiveFireSupport
                && !canAffordDefensiveTank
                && !canBuyBasicMass) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — defThreat filter (notThreat={!IsDefensiveBaseThreatPurchase(u)} notElite={!isAllowedDefensiveElite} notTank={!canAffordDefensiveTank} notMass={!canBuyBasicMass})"); continue; }
            if (!defensiveBaseThreat && isHybridCapturer && !hasOpenDefensiveSlot) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — hybrid sem slot defensivo"); continue; }
            if ((onlyCapturers || onlyAssault || onlyTransporter || onlyLogistics || onlyFireSupport)
                && !((onlyCapturers && isPrimaryCapturer) || (onlyAssault && isPrimaryAssault) || (onlyTransporter && isPrimaryTransporter) || (onlyLogistics && isPrimaryLogistics) || (onlyFireSupport && isPrimaryFireSupport))) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — onlyFilter (cap={isPrimaryCapturer} ass={isPrimaryAssault} trans={isPrimaryTransporter} log={isPrimaryLogistics} fire={isPrimaryFireSupport})"); continue; }

            int score = u.cost;
            if (defensiveBaseThreat && defensiveBaseManpowerShortage)
            {
                int basicReserve = Mathf.Max(0, defensiveBaseBasicMassCost) * 2;
                if (IsDefensiveBaseAssaultTankPurchase(u) && budget >= u.cost + basicReserve)
                    score += 180000;
                else if (IsDefensiveBaseThreatPurchase(u))
                    score += 90000;
                else if (IsDefensiveBaseBasicMassPurchase(u))
                    score += 70000;
            }
            if (defensiveArmorThreat && IsDefensiveBaseThreatPurchase(u))
                score += 80000;
            if (openTransportSlots > 0 && isPrimaryTransporter)
                score += urgentTransportDemand ? 144000 : 108000;
            if (openLogisticsSlots > 0 && isPrimaryLogistics)
            {
                score += openLogisticsSlots >= 2 ? 128000 : 108000;
                if (defensiveBaseThreat)
                    score -= 25000;
            }
            if (openFireSupportSlots > 0 && isFireSupportCapable)
            {
                bool preferredProfile = preferDefensiveFireSupport
                    ? IsDefensiveFireSupportPurchase(u)
                    : IsOffensiveFireSupportPurchase(u);
                bool fallbackProfile = preferDefensiveFireSupport
                    ? IsOffensiveFireSupportPurchase(u)
                    : IsDefensiveFireSupportPurchase(u);

                score += preferredProfile ? 118000 : fallbackProfile ? 72000 : 35000;
                if (!isPrimaryFireSupport)
                    score -= 18000;
                score += Mathf.Max(0, u.eliteLevel) * 1500;
                if (u == eliteFireSupportTarget) score += 200000;
                if (defensiveBaseThreat)
                {
                    score += 150000;
                    if (isPrimaryAssault) score += 25000;
                    else if (isPrimaryFireSupport) score += 18000;
                }
                if (!preferredProfile && !fallbackProfile)
                    score -= 25000;
            }
            if (openCapturerSlots > 0)
            {
                if (isPrimaryCapturer)              score += 100000;
                else if (isSecondary && defensiveStance) score +=  10000;
                else if (openAssaultSlots <= 0 && !(openTransportSlots > 0 && isPrimaryTransporter)) score -= 100000;
            }
            if (openAssaultSlots > 0)
            {
                if (u == eliteAssaultTarget) score += 200000;
                if (isPrimaryAssault && !isHybridCapturer) score += 90000;
                else if (isPrimaryAssault && defensiveStance) score += 10000;
                else if (isPrimaryAssault) score -= 90000;
                else if (openCapturerSlots <= 0 && !isPrimaryTransporter) score -= 90000;
            }

            // Penalise slow units in non-defensive stance — only decisive in the fallback
            // case (no open slots), where the base score is just u.cost.
            if (!defensiveStance && u.movement < 3)
                score -= (3 - u.movement) * 1500;

            string roleStr = isFireSupportCapable && !isPrimaryFireSupport ? "ASS/FIRE" : isPrimaryFireSupport ? "FIRE" : isPrimaryLogistics ? "LOG" : isPrimaryTransporter ? "TRANS" : isPrimaryCapturer ? "CAP" : isPrimaryAssault ? $"ASS(hybrid={isHybridCapturer})" : "other";
            Debug.Log($"[AI PickUnit] {u.displayName} ${u.cost} role={roleStr} score={score} mov={u.movement} | trans={openTransportSlots} transUrg={urgentTransportDemand} log={openLogisticsSlots} cap={openCapturerSlots} ass={openAssaultSlots} fire={openFireSupportSlots} fireDef={preferDefensiveFireSupport} defThreat={defensiveBaseThreat}");
            if (score > bestScore) { bestScore = score; best = u; }
        }

        return best;
    }

    private static UnitData FindEliteAssaultReserveTarget(AIWorldSnapshot snapshot, int minEliteLevel = 1)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return null;

        UnitData best = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.domain != Domain.Land) continue;
                if (!IsPurePrimaryAssault(unit)) continue;
                if (unit.eliteLevel < minEliteLevel) continue;

                if (best == null
                    || unit.eliteLevel < best.eliteLevel
                    || (unit.eliteLevel == best.eliteLevel && unit.cost < best.cost))
                    best = unit;
            }
        }

        return best;
    }

    private static int CalculateEliteAssaultSafetyBuffer(UnitData eliteAssault)
    {
        if (eliteAssault == null) return 0;
        float percent = Instance != null ? Mathf.Clamp(Instance.SavingPercentualForElite, 0f, 20f) : 15f;
        if (percent <= 0f) return 0;
        return Mathf.CeilToInt(Mathf.Max(0, eliteAssault.cost) * (percent / 100f));
    }

    private static UnitData FindEliteFireSupportReserveTarget(AIWorldSnapshot snapshot, bool preferDefensiveFireSupport, int budget = int.MaxValue)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return null;

        UnitData bestPreferred = null;
        UnitData bestFallback = null;
        UnitData bestAffordable = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.domain != Domain.Land) continue;
                if (!IsFireSupportPurchase(unit)) continue;
                if (unit.eliteLevel < 1) continue;

                bool preferredProfile = preferDefensiveFireSupport
                    ? IsDefensiveFireSupportPurchase(unit)
                    : IsOffensiveFireSupportPurchase(unit);
                UnitData current = preferredProfile ? bestPreferred : bestFallback;
                if (current == null
                    || unit.eliteLevel < current.eliteLevel
                    || (unit.eliteLevel == current.eliteLevel && unit.cost < current.cost))
                {
                    if (preferredProfile) bestPreferred = unit;
                    else bestFallback = unit;
                }

                if (unit.cost <= budget
                    && (bestAffordable == null
                        || unit.eliteLevel < bestAffordable.eliteLevel
                        || (unit.eliteLevel == bestAffordable.eliteLevel && unit.cost < bestAffordable.cost)))
                {
                    bestAffordable = unit;
                }
            }
        }

        if (bestAffordable != null)
            return bestAffordable;

        return bestPreferred != null ? bestPreferred : bestFallback;
    }

    private static UnitData FindAffordableSupremeDefensiveFireSupportTarget(AIWorldSnapshot snapshot, int budget)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return null;

        UnitData best = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.domain != Domain.Land) continue;
                if (!IsDefensiveFireSupportPurchase(unit)) continue;
                if (unit.eliteLevel < 2) continue;
                if (unit.cost > budget) continue;

                if (best == null
                    || unit.eliteLevel > best.eliteLevel
                    || (unit.eliteLevel == best.eliteLevel && unit.cost > best.cost))
                    best = unit;
            }
        }

        return best;
    }

    private static UnitData FindAffordableArmorFallbackFireSupportTarget(AIWorldSnapshot snapshot, int budget)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return null;

        UnitData best = null;
        int bestPriority = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.domain != Domain.Land) continue;
                if (!IsFireSupportPurchase(unit)) continue;
                if (unit.cost > budget) continue;

                bool isPrimaryAssault = IsPrimaryRole(unit, UnitRole.Assalto);
                bool isPrimaryFireSupport = IsPrimaryRole(unit, UnitRole.FogoIndireto);
                int priority = isPrimaryAssault ? 0 : isPrimaryFireSupport ? 1 : 2;

                if (best == null
                    || priority < bestPriority
                    || (priority == bestPriority && unit.eliteLevel > best.eliteLevel)
                    || (priority == bestPriority && unit.eliteLevel == best.eliteLevel && unit.cost < best.cost))
                {
                    best = unit;
                    bestPriority = priority;
                }
            }
        }

        return best;
    }

    private static bool IsEliteFireSupportReserveReady(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return false;

        CountSlots(snapshot.AITeam, UnitRole.Capturador, out int totalCap, out int filledCap);
        CountSlots(snapshot.AITeam, UnitRole.Assalto, out _, out int filledAss);
        float fillThreshold = Instance != null ? Instance.EliteCapturerFillRatio : 0.6f;
        int minAssault = Instance != null ? Instance.MinFilledAssaultSlots : 1;
        float capFill = totalCap > 0 ? filledCap / (float)totalCap : 1f;
        return capFill >= fillThreshold && filledAss >= minAssault;
    }

    private static bool IsEliteAssaultReserveReady(AIWorldSnapshot snapshot)
    {
        return IsEliteFireSupportReserveReady(snapshot);
    }

    private static int CountOpenSlots(TeamId aiTeam, UnitRole role)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return 0;

        int open = 0;
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == role && !slot.Filled) open++;
        return open;
    }

    private static int LimitCapturerDemandForProgression(
        AIWorldSnapshot snapshot,
        int openCapturerSlots,
        int openAssaultSlots,
        int openTransportSlots,
        int openLogisticsSlots,
        int openFireSupportSlots)
    {
        if (openCapturerSlots <= 0 || snapshot == null)
            return 0;

        int batchSize = Instance != null ? Instance.ProgressiveCapturerBatchSize : 2;
        int capped = Mathf.Min(openCapturerSlots, Mathf.Max(1, batchSize));
        int supportDemand = openAssaultSlots + openTransportSlots + openLogisticsSlots + openFireSupportSlots;

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        int supportPauseThreshold = Instance != null ? Instance.CapturersPerPreventiveTransport : 4;

        if (supportDemand > 0 && activeCapturers >= supportPauseThreshold && activeAssault >= 1)
            capped = 0;

        if (capped != openCapturerSlots)
        {
            Debug.Log($"[AI Shopping] capturer_progression: raw={openCapturerSlots} capped={capped} activeCap={activeCapturers} activeAss={activeAssault} supportDemand={supportDemand} batch={batchSize} pauseAt={supportPauseThreshold}");
        }

        return capped;
    }

    private static void CountSlots(TeamId aiTeam, UnitRole role, out int total, out int filled)
    {
        total = 0; filled = 0;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return;
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == role) { total++; if (slot.Filled) filled++; }
    }

    private static bool HasOpenDefensiveSlot(TeamId aiTeam)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Status != ObjectiveStatus.Defending) continue;
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Capturador && !slot.Filled) return true;
        }

        return false;
    }

    private static bool IsPrimaryRole(UnitData unit, UnitRole role)
    {
        return unit != null && unit.roles != null && unit.roles.Count > 0 && unit.roles[0] == role;
    }

    private static bool IsFireSupportPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && unit.roles != null
            && unit.roles.Contains(UnitRole.FogoIndireto);
    }

    private static bool IsFireSupportAllowedByTiming(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return false;

        int minTurn = Instance != null ? Instance.MinTurnForFireSupport : 3;
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn)
            return false;

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        int minCapturers = Instance != null ? Instance.MinActiveCapturersForFireSupport : 2;
        int minAssault = Instance != null ? Instance.MinActiveAssaultForFireSupport : 1;

        return activeCapturers >= minCapturers
            && activeAssault >= minAssault;
    }

    private static bool IsPurePrimaryAssault(UnitData unit)
    {
        return IsPrimaryRole(unit, UnitRole.Assalto)
            && (unit.roles == null || !unit.roles.Contains(UnitRole.Capturador));
    }

    private static bool IsDefensiveBaseThreatPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && unit.unitClass == GameUnitClass.Infantry
            && unit.roles != null
            && unit.roles.Count > 0
            && unit.roles[0] == UnitRole.Assalto
            && unit.roles.Contains(UnitRole.Capturador);
    }

    private static bool IsDefensiveBaseAssaultTankPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && unit.unitClass == GameUnitClass.Armored
            && IsPurePrimaryAssault(unit);
    }

    private static bool IsDefensiveBaseBasicMassPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && unit.unitClass == GameUnitClass.Infantry
            && IsPrimaryRole(unit, UnitRole.Capturador);
    }

    private static bool HasDefensiveBaseManpowerShortage(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null || snapshot.EnemyUnits == null)
            return false;

        int myCount = snapshot.MyUnits.Count;
        int visibleEnemyCount = snapshot.EnemyUnits.Count;
        return myCount > 0
            && myCount <= DefensiveLowTroopCountThreshold
            && visibleEnemyCount > myCount;
    }

    private static bool TryFindEmergencyProductionDefensePurchase(
        AIWorldSnapshot snapshot,
        int budget,
        out UnitData bestUnit,
        out int contestedOwned)
    {
        bestUnit = null;
        contestedOwned = CountOwnedConstructionsUnderCapture(snapshot);
        if (snapshot == null || snapshot.MyUnits == null || snapshot.MyBuildings == null)
            return false;
        if (snapshot.MyUnits.Count != 1)
            return false;
        if (contestedOwned <= 0)
            return false;

        int bestScore = int.MinValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            UnitData unit = FindBestAffordableEmergencyDefensePurchase(building, budget);
            if (unit == null) continue;

            int score = ScoreEmergencyDefensePurchase(unit);
            if (score > bestScore)
            {
                bestScore = score;
                bestUnit = unit;
            }
        }

        return bestUnit != null;
    }

    private static int CountOwnedConstructionsUnderCapture(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return 0;

        int count = 0;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.IsCapturable || building.CapturePointsMax <= 0)
                continue;
            if (!IsCriticalHomeConstruction(building, snapshot.AITeam))
                continue;
            if (building.CurrentCapturePoints < building.CapturePointsMax)
                count++;
        }

        return count;
    }

    private static UnitData FindBestAffordableEmergencyDefensePurchase(ConstructionManager building, int budget)
    {
        if (building == null || building.OfferedUnits == null)
            return null;

        UnitData best = null;
        int bestScore = int.MinValue;
        foreach (UnitData unit in building.OfferedUnits)
        {
            if (unit == null || unit.cost > budget || unit.domain != Domain.Land)
                continue;
            if (!IsEmergencyDefensePurchase(unit))
                continue;

            int score = ScoreEmergencyDefensePurchase(unit);
            if (score > bestScore)
            {
                bestScore = score;
                best = unit;
            }
        }

        return best;
    }

    private static bool IsEmergencyDefensePurchase(UnitData unit)
    {
        if (unit == null || unit.roles == null)
            return false;

        bool fireSupport = unit.roles.Contains(UnitRole.FogoIndireto);
        bool assaultArmor = unit.unitClass == GameUnitClass.Armored
            && unit.roles.Count > 0
            && unit.roles[0] == UnitRole.Assalto;
        return fireSupport || assaultArmor;
    }

    private static int ScoreEmergencyDefensePurchase(UnitData unit)
    {
        if (unit == null)
            return int.MinValue;

        bool fireSupport = unit.roles != null && unit.roles.Contains(UnitRole.FogoIndireto);
        bool assaultArmor = unit.unitClass == GameUnitClass.Armored
            && unit.roles != null && unit.roles.Count > 0 && unit.roles[0] == UnitRole.Assalto;

        int score = unit.cost + Mathf.Max(0, unit.eliteLevel) * 10000;
        if (fireSupport) score += 100000;
        if (unit.longRangeStationary) score += 25000;
        if (unit.preferRepositionAtWeaponMaxRange) score += 15000;
        if (assaultArmor) score += 50000;
        return score;
    }

    private static int FindCheapestDefensiveBaseThreatPurchaseCost(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return 0;

        int cheapest = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (!IsDefensiveBaseThreatPurchase(unit)) continue;
                if (unit.cost < cheapest) cheapest = unit.cost;
            }
        }

        return cheapest < int.MaxValue ? cheapest : 0;
    }

    private static int FindCheapestDefensiveBaseBasicMassPurchaseCost(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return 0;

        int cheapest = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (!IsDefensiveBaseBasicMassPurchase(unit)) continue;
                if (unit.cost < cheapest) cheapest = unit.cost;
            }
        }

        return cheapest < int.MaxValue ? cheapest : 0;
    }

    private static bool HasVisibleEnemyNearBase(ConstructionManager building, AIWorldSnapshot snapshot, int range)
    {
        if (building == null || snapshot == null || snapshot.EnemyUnits == null)
            return false;
        if (!IsCriticalHomeConstruction(building, snapshot.AITeam))
            return false;

        Vector3Int baseCell = building.CurrentCellPosition;
        baseCell.z = 0;
        int safeRange = Mathf.Max(0, range);

        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (SectorManager.HexDistance(baseCell, enemyCell) <= safeRange)
                return true;
        }

        return false;
    }

    private static int CountVisibleEnemyArmorNearOwnedBase(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot == null || snapshot.EnemyUnits == null || snapshot.MyBuildings == null)
            return 0;

        int safeRange = Mathf.Max(0, range);
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData enemyData) || enemyData == null) continue;
            if (enemyData.unitClass != GameUnitClass.Armored) continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            foreach (ConstructionManager building in snapshot.MyBuildings)
            {
                if (building == null) continue;
                if (!IsCriticalHomeConstruction(building, snapshot.AITeam)) continue;

                Vector3Int baseCell = building.CurrentCellPosition;
                baseCell.z = 0;
                if (SectorManager.HexDistance(baseCell, enemyCell) > safeRange) continue;

                count++;
                break;
            }
        }

        return count;
    }

    private static bool IsCriticalHomeConstruction(ConstructionManager building, TeamId aiTeam)
    {
        if (building == null || building.TeamId != aiTeam)
            return false;
        return building.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(building.Sector);
    }

    private static bool CanOfferUnit(ConstructionManager building, UnitData target)
    {
        if (building == null || target == null || building.OfferedUnits == null) return false;
        for (int i = 0; i < building.OfferedUnits.Count; i++)
            if (building.OfferedUnits[i] == target) return true;
        return false;
    }

    private static bool CanOfferFireSupportUnit(ConstructionManager building)
    {
        if (building == null || building.OfferedUnits == null) return false;
        for (int i = 0; i < building.OfferedUnits.Count; i++)
            if (IsFireSupportPurchase(building.OfferedUnits[i])) return true;
        return false;
    }

    private static bool CanOfferAffordableDefensiveTank(ConstructionManager building, int budget, int reserve)
    {
        if (building == null || building.OfferedUnits == null) return false;

        foreach (UnitData unit in building.OfferedUnits)
        {
            if (CanAffordDefensiveBaseTankPurchase(unit, budget, reserve)) return true;
        }

        return false;
    }

    private static bool CanAffordDefensiveBaseTankPurchase(UnitData unit, int budget, int reserve)
    {
        if (!IsDefensiveBaseAssaultTankPurchase(unit))
            return false;

        int safeReserve = unit.eliteLevel >= 1 ? 0 : Mathf.Max(0, reserve);
        return budget >= unit.cost + safeReserve;
    }

    private static bool CanAffordEliteDefensiveTank(AIWorldSnapshot snapshot, int budget)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return false;

        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (!IsDefensiveBaseAssaultTankPurchase(unit)) continue;
                if (unit.eliteLevel < 1) continue;
                if (unit.cost <= budget) return true;
            }
        }

        return false;
    }

    private static UnitData FindEliteDefensiveTankReserveTarget(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return null;

        UnitData best = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (!IsDefensiveBaseAssaultTankPurchase(unit)) continue;
                if (unit.eliteLevel < 1) continue;

                if (best == null
                    || unit.eliteLevel > best.eliteLevel
                    || (unit.eliteLevel == best.eliteLevel && unit.cost < best.cost))
                    best = unit;
            }
        }

        return best;
    }

    private static bool HasActivePrimaryRole(AIWorldSnapshot snapshot, UnitRole role)
    {
        if (snapshot == null) return false;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (unit.TryGetUnitData(out UnitData data) && IsPrimaryRole(data, role)) return true;
        }
        return false;
    }

    private static bool CanAffordPurePrimaryRole(AIWorldSnapshot snapshot, UnitRole role, int budget)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return false;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.cost > budget || unit.domain != Domain.Land) continue;
                if (!IsPrimaryRole(unit, role)) continue;
                if (unit.roles != null && unit.roles.Contains(UnitRole.Capturador)) continue;
                return true;
            }
        }
        return false;
    }

    private static int ComputeFireSupportDemand(
        AIWorldSnapshot snapshot,
        int openCapturerSlots,
        int openAssaultSlots,
        out bool preferDefensiveFireSupport)
    {
        preferDefensiveFireSupport = false;
        if (snapshot == null) return 0;

        int minTurn = Instance != null ? Instance.MinTurnForFireSupport : 3;
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn)
        {
            Debug.Log($"[AI Shopping] fire_support_demand: 0 turn={snapshot.TurnNumber}<{minTurn}");
            return 0;
        }

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        // Exclude units under repair — they are not combat-ready and must not block buying replacements.
        int activeFireSupport = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager fsUnit in snapshot.MyUnits)
            {
                if (fsUnit == null || fsUnit.IsDead || fsUnit.IsEmbarked || fsUnit.IsUnderRepair) continue;
                if (!fsUnit.TryGetUnitData(out UnitData fsData) || fsData?.roles == null) continue;
                if (fsData.roles.Contains(UnitRole.FogoIndireto)) activeFireSupport++;
            }

        int minCapturers = Instance != null ? Instance.MinActiveCapturersForFireSupport : 2;
        int minAssault = Instance != null ? Instance.MinActiveAssaultForFireSupport : 1;
        bool compositionReady = activeCapturers >= minCapturers
            && activeAssault >= minAssault;

        if (!compositionReady)
        {
            Debug.Log($"[AI Shopping] fire_support_demand: 0 composition cap={activeCapturers}/{minCapturers} ass={activeAssault}/{minAssault} openCap={openCapturerSlots} openAss={openAssaultSlots}");
            return 0;
        }

        bool defensiveNeed = snapshot.Stance == AIStance.Defensive || HasAnyVisibleEnemyNearOwnedBase(snapshot, DefensiveBaseThreatRange);
        bool offensiveNeed = snapshot.Stance == AIStance.Offensive || HasAnyOffensiveObjective(snapshot.AITeam);
        preferDefensiveFireSupport = defensiveNeed && !offensiveNeed || snapshot.Stance == AIStance.Defensive;

        if (activeFireSupport > 0)
        {
            Debug.Log($"[AI Shopping] fire_support_demand: 0 activeFire={activeFireSupport} preferDef={preferDefensiveFireSupport}");
            return 0;
        }

        int demand = (defensiveNeed || offensiveNeed || snapshot.Stance == AIStance.Tactical) ? 1 : 0;
        Debug.Log($"[AI Shopping] fire_support_demand: demand={demand} activeFire={activeFireSupport} stance={snapshot.Stance} defensive={defensiveNeed} offensive={offensiveNeed} preferDef={preferDefensiveFireSupport}");
        return demand;
    }

    private static int CountActiveEliteFireSupportUnits(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null) return 0;
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null) continue;
            if (data.roles == null || !data.roles.Contains(UnitRole.FogoIndireto)) continue;
            if (data.eliteLevel >= 1) count++;
        }
        return count;
    }

    private static int CountActiveEliteAssaultUnits(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null) return 0;
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null) continue;
            if (!IsPurePrimaryAssault(data) || data.eliteLevel < 1) continue;
            count++;
        }
        return count;
    }

    private static int CountActiveUnitsWithRole(AIWorldSnapshot snapshot, UnitRole role, bool requirePrimary)
    {
        if (snapshot == null || snapshot.MyUnits == null) return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.roles == null || data.roles.Count == 0) continue;
            if (requirePrimary)
            {
                if (data.roles[0] == role) count++;
            }
            else if (data.roles.Contains(role))
            {
                count++;
            }
        }
        return count;
    }

    private static int ComputeLogisticsDemand(AIWorldSnapshot snapshot, out int repairDemandCount, out int activeLogisticsCount)
    {
        repairDemandCount = CountUnitsUnderRepair(snapshot);
        activeLogisticsCount = CountActiveUnitsWithRole(snapshot, UnitRole.Logistica, requirePrimary: false);

        if (snapshot != null && snapshot.TurnNumber <= 1)
        {
            Debug.Log($"[AI Shopping] logistics_demand: 0 turn={snapshot.TurnNumber}<=1");
            return 0;
        }

        int desiredLogistics = 0;
        if (repairDemandCount >= 1)
            desiredLogistics = 1;
        if (repairDemandCount >= 4)
            desiredLogistics = 2;

        int demand = Mathf.Max(0, desiredLogistics - activeLogisticsCount);
        Debug.Log($"[AI Shopping] logistics_demand: demand={demand} repairs={repairDemandCount} activeLog={activeLogisticsCount} desired={desiredLogistics}");
        return demand;
    }

    private static int CountUnitsUnderRepair(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (unit.IsUnderRepair)
                count++;
        }

        return count;
    }

    private static bool HasAnyVisibleEnemyNearOwnedBase(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return false;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null) continue;
            if (!IsCriticalHomeConstruction(building, snapshot.AITeam)) continue;
            if (HasVisibleEnemyNearBase(building, snapshot, range)) return true;
        }
        return false;
    }

    private static bool HasAnyOffensiveObjective(TeamId aiTeam)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status == ObjectiveStatus.Defending || obj.Status == ObjectiveStatus.Complete) continue;
            if (obj.HasOpenSlot(UnitRole.Capturador) || obj.HasOpenSlot(UnitRole.Assalto))
                return true;

            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && (slot.Role == UnitRole.Capturador || slot.Role == UnitRole.Assalto))
                    return true;
        }
        return false;
    }

    private static bool IsDefensiveFireSupportPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && IsFireSupportPurchase(unit)
            && unit.longRangeStationary;
    }

    private static bool IsOffensiveFireSupportPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && IsFireSupportPurchase(unit)
            && unit.preferRepositionAtWeaponMaxRange;
    }

    // Returns how many APCs to buy this round.
    // Layer 1: assigned capturers who are already far from their target.
    // Layer 2: open transport slots in far objective rings, so the APC can be bought before the capturer is stranded.
    // Subtracts free APCs already in the field; caps at 1 per shopping round.
    private static int ComputeTransportDemand(AIWorldSnapshot snapshot, out bool urgentTransportDemand)
    {
        urgentTransportDemand = false;
        TeamId aiTeam = snapshot.AITeam;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return 0;

        int minDist = AIController.Instance != null
            ? AIController.Instance.GetEffectiveTransportThreshold(aiTeam) : 7;

        // Count transporters already in the field. Preventive demand only needs
        // existing APC coverage; urgent assigned demand needs an APC with no cargo.
        int activeTransporters = 0;
        int freeAPCs = 0;
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.TeamId != aiTeam || u.IsDead || u.IsEmbarked) continue;
            if (!u.TryGetUnitData(out UnitData d)) continue;
            if (d.roles == null || d.roles.Count == 0 || d.roles[0] != UnitRole.Transportador) continue;
            activeTransporters++;
            bool hasCargo = false;
            if (u.TransportedUnitSlots != null)
                foreach (UnitTransportSeatRuntime seat in u.TransportedUnitSlots)
                    if (seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked) { hasCargo = true; break; }
            if (!hasCargo) freeAPCs++;
        }

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        int assignedNeeded = 0;
        int preventiveNeeded = 0;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                continue;

            bool hasTransportSlot = false;
            bool hasOpenTransportSlot = false;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot.Role != UnitRole.Transportador) continue;
                hasTransportSlot = true;
                if (!slot.Filled) hasOpenTransportSlot = true;
            }
            if (!hasTransportSlot) continue;

            ConstructionManager tgt = AIController.FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt == null) continue;

            if (hasOpenTransportSlot
                && activeCapturers >= 2
                && activeAssault >= 1
                && ObjectiveHasOpenOrFilledCapturer(obj)
                && SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)
                && info.GetDistanceToHQ(aiTeam) >= minDist)
            {
                preventiveNeeded++;
            }

            // Find the assigned capturer
            UnitManager capturer = null;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot.Role != UnitRole.Capturador || !slot.Filled) continue;
                foreach (UnitManager u in UnitManager.AllActive)
                    if (u.InstanceId == slot.AssignedUnitId && !u.IsDead) { capturer = u; break; }
                if (capturer != null) break;
            }
            if (capturer == null || capturer.IsEmbarked) continue;

            Vector3Int capCell = capturer.CurrentCellPosition; capCell.z = 0;
            Vector3Int tgtCell = tgt.CurrentCellPosition; tgtCell.z = 0;
            float dist = SectorManager.HexDistance(capCell, tgtCell);

            if (dist >= minDist) assignedNeeded++;
        }

        int capturersPerTransport = Instance != null ? Instance.CapturersPerPreventiveTransport : 4;
        int massNeeded = 0;
        if (activeCapturers >= capturersPerTransport && activeAssault >= 1)
            massNeeded = activeCapturers / Mathf.Max(1, capturersPerTransport);

        int assignedDeficit = Mathf.Max(0, assignedNeeded - freeAPCs);
        int preventiveDeficit = Mathf.Max(0, Mathf.Max(preventiveNeeded, massNeeded) - activeTransporters);
        urgentTransportDemand = assignedDeficit > 0;
        int needed = Mathf.Max(assignedNeeded, Mathf.Max(preventiveNeeded, massNeeded));
        int deficit = urgentTransportDemand ? assignedDeficit : preventiveDeficit;
        int demand  = Mathf.Min(deficit, 1); // cap: max 1 APC per shopping round
        Debug.Log($"[AI Shopping] transport_demand: needed={needed} assigned={assignedNeeded} preventive={preventiveNeeded} mass={massNeeded} activeCap={activeCapturers} activeAss={activeAssault} activeAPCs={activeTransporters} freeAPCs={freeAPCs} assignedDef={assignedDeficit} preventiveDef={preventiveDeficit} urgent={urgentTransportDemand} demand={demand} minDist={minDist}");
        return demand;
    }

    private static bool ObjectiveHasOpenOrFilledCapturer(SectorObjective obj)
    {
        if (obj == null || obj.Slots == null) return false;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == UnitRole.Capturador) return true;
        return false;
    }

    private static bool CanOfferPrimaryRoleUnit(ConstructionManager building, UnitRole role)
    {
        if (building == null || building.OfferedUnits == null) return false;
        foreach (UnitData u in building.OfferedUnits)
            if (u != null && u.domain == Domain.Land && IsPrimaryRole(u, role)) return true;
        return false;
    }

    private static int FindCheapestAvailableTransportCost(AIWorldSnapshot snapshot)
    {
        int cheapest = 0;
        if (snapshot == null || snapshot.MyBuildings == null) return cheapest;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.CanProduceUnitsForTeam(snapshot.AITeam) || b.OfferedUnits == null) continue;
            foreach (UnitData u in b.OfferedUnits)
            {
                if (u == null || u.domain != Domain.Land || !IsPrimaryRole(u, UnitRole.Transportador)) continue;
                if (cheapest == 0 || u.cost < cheapest) cheapest = u.cost;
            }
        }
        return cheapest;
    }

    private static int IndexOf(IReadOnlyList<UnitData> list, UnitData target)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == target) return i;
        return 0;
    }

    // Returns true when all of the unit's weapons target only air (AntiAerea).
    // Units with at least one non-AA weapon retain some general-purpose value.
    private static bool IsAntiAirOnlyUnit(UnitData unit)
    {
        if (unit == null || unit.embarkedWeapons == null || unit.embarkedWeapons.Count == 0)
            return false;
        foreach (UnitEmbarkedWeapon ew in unit.embarkedWeapons)
        {
            if (ew?.weapon == null) continue;
            if (ew.weapon.WeaponCategory != WeaponCategory.AntiAerea)
                return false;
        }
        return true;
    }

    // Returns true when at least one active unit on the map has a native Air domain.
    // This covers both flying and grounded aircraft.
    private static bool HasAnyAirThreat()
    {
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.IsDead) continue;
            if (!u.TryGetUnitData(out UnitData data) || data == null) continue;
            if (data.domain == Domain.Air) return true;
        }
        return false;
    }
}
