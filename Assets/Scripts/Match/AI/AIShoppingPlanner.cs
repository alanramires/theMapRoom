using System.Collections.Generic;
using UnityEngine;

public class AIShoppingPlanner : MonoBehaviour
{
    private const int DefensiveBaseThreatRange = 3;
    private const int DefensiveArmorThreatRange = 3;
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
    public bool onlyAirTransporter;
    public bool onlyInterceptador;
    public bool onlyAtaqueAereo;

    [Header("Economia Exército")]
    [Range(0f, 20f)] public float SavingPercentualForElite = 15f;
    [Range(0f, 1f)]  public float EliteCapturerFillRatio   = 0.6f;
    [Range(0, 5)]    public int   MinFilledAssaultSlots     = 1;
    [Range(1, 12)]   public int   MinTurnForFireSupport     = 3;
    [Range(0, 8)]    public int   MinActiveCapturersForFireSupport = 2;
    [Range(0, 5)]    public int   MinActiveAssaultForFireSupport   = 1;
    [Range(2, 8)]    public int   CapturersPerPreventiveTransport = 4;
    [Range(1, 4)]    public int   ProgressiveCapturerBatchSize = 2;
    [Range(1, 4)]    public int   AssaultPerFireSupportRatio = 2;
    [Range(0, 2)]    public int   MaxProactiveDefensiveFireSupport = 1;
    [Range(0, 5)]    public int   MaxProactiveAntiAirSAM          = 3;
    [Range(2, 10)]   public int   AntiAirCoverageRange             = 5;

    [Header("Defesa de Base")]
    [Range(0, 3)]    public int   MinBaseArtilharia               = 1;
    [Range(0, 3)]    public int   MinBaseAAA                      = 1;
    [Range(1, 12)]   public int   MinTurnBaseDefense              = 3;

    [Header("Economia Aeronáutica")]
    [Range(1, 8)]    public int   MaxAirTransporters               = 3;
    [Range(1, 12)]   public int   MinTurnForInterceptador          = 4;
    [Range(1, 6)]    public int   HelicopterosPorCacaB             = 3;
    [Range(0, 6)]    public int   MaxCacaB                         = 4;
    [Range(0, 4)]    public int   MaxCacaA                         = 2;
    [Range(1, 12)]   public int   MinTurnForAtaqueAereo            = 5;
    [Range(1, 4)]    public int   ChinooksPorApache                = 2;
    [Range(1, 4)]    public int   ApachesParaBombardeiro           = 2;
    public bool                  ComprarApacheEmModoDefesa         = true;
    [Range(0, 4)]    public int   MinCacaBPresence                 = 1;
    [Range(0, 4)]    public int   MinApachePresence                = 1;
    [Range(0, 2)]    public int   MinBombaPresence                 = 0;

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
        bool onlyCapturers       = Instance != null && Instance.onlyCapturers;
        bool onlyAssault         = Instance != null && Instance.onlyAssault;
        bool onlyTransporter     = Instance != null && Instance.onlyTransporter;
        bool onlyLogistics       = Instance != null && Instance.onlyLogistics;
        bool onlyFireSupport     = Instance != null && Instance.onlyFireSupport;
        bool onlyAirTransporter  = Instance != null && Instance.onlyAirTransporter;
        bool onlyInterceptador   = Instance != null && Instance.onlyInterceptador;
        bool onlyAtaqueAereo     = Instance != null && Instance.onlyAtaqueAereo;

        var orders = new List<ShoppingOrder>();
        if (snapshot == null) return orders;

        var occupied  = new HashSet<Vector3Int>(snapshot.OccupiedCells);
        int remaining = snapshot.Budget;
        int openCapturerSlots   = CountOpenSlots(snapshot.AITeam, UnitRole.Capturador);
        int openAssaultSlots    = CountOpenSlots(snapshot.AITeam, UnitRole.Assalto);
        int openTransportSlots    = ComputeTransportDemand(snapshot, out bool urgentTransportDemand);
        int openAirTransportSlots = ComputeAirTransportDemand(snapshot, openCapturerSlots);
        int openFireSupportSlots = ComputeFireSupportDemand(snapshot, openCapturerSlots, openAssaultSlots, out bool preferDefensiveFireSupport);
        int openCacaBSlots  = ComputeCacaBDemand(snapshot);
        int openCacaASlots  = ComputeCacaADemand(snapshot);
        int openApacheSlots = ComputeApacheDemand(snapshot);
        int openBombaSlots  = ComputeBombaDemand(snapshot);
        bool proactiveDefFireSupport = !preferDefensiveFireSupport
            && ComputeProactiveDefensiveFireSupportNeeded(snapshot);
        if (proactiveDefFireSupport)
        {
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = true;
            Debug.Log($"[AI Shopping] proactive_def_fire_support: abrindo slot preventivo (1 art. defensiva antes da ameaça)");
        }
        int openLogisticsSlots = ComputeLogisticsDemand(snapshot, out int repairDemandCount, out int activeLogisticsCount);
        bool proactiveAntiAir = ComputeProactiveAntiAirNeeded(snapshot, out int activeSAMs, out int activeAAAs);
        int maxSAMCap = Instance != null ? Instance.MaxProactiveAntiAirSAM : 3;
        // SAM proactive: requires AAA in field (chain gate) and cap not reached.
        // AAA proactive: bypasses the IsAntiAirOnlyUnit gate via proactiveAntiAir flag — competes normally for open assault slots.
        bool proactiveSAM = proactiveAntiAir && activeAAAs >= 1 && activeSAMs < 1; // proactive: 1 SAM is enough; reactive path uses maxSAMCap
        if (proactiveSAM)
        {
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = true;
            Debug.Log($"[AI Shopping] proactive_anti_air: SAM proativo activeAAAs={activeAAAs} activeSAMs={activeSAMs}/{maxSAMCap} → slot fire_support defensivo aberto");
        }
        ComputeGuaranteedBaseDefense(snapshot, out int baseArtSlots, out bool forceBaseAAA);
        if (baseArtSlots > 0)
        {
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, baseArtSlots);
            preferDefensiveFireSupport = true;
            proactiveDefFireSupport = true;
            Debug.Log($"[AI Shopping] base_defense: abrindo {baseArtSlots} slot(s) artilharia preventiva");
        }
        if (forceBaseAAA)
        {
            proactiveAntiAir = true;
            if (openAssaultSlots <= 0)
                openAssaultSlots = 1;
            Debug.Log($"[AI Shopping] base_defense: forçando AAA preventiva (proactiveAntiAir=true, assault_slot=1)");
        }
        int aaaCoverageRange = Instance != null ? Instance.AntiAirCoverageRange : 5;
        int aaaCap = CountVisibleEnemyAircraftNearHQ(snapshot, aaaCoverageRange);
        bool aaaThreat = aaaCap > 0;
        if (forceBaseAAA)
            aaaCap = Mathf.Max(aaaCap, Instance != null ? Instance.MinBaseAAA : 1);
        if (aaaCap > 0 && activeAAAs < aaaCap && openAssaultSlots <= 0)
        {
            openAssaultSlots = 1;
            Debug.Log($"[AI Shopping] reactive_anti_air: {aaaCap} aeronave(s) visivel <= {aaaCoverageRange}h do HQ, activeAAAs={activeAAAs} → slot assault aberto para AAA");
        }
        // Wider reactive AAA: any visible enemy aircraft (anywhere on map) opens one slot if none near HQ yet.
        if (aaaCap == 0 && activeAAAs == 0 && openAssaultSlots <= 0)
        {
            int totalVisibleAir = CountTotalVisibleEnemyAircraft(snapshot);
            if (totalVisibleAir > 0)
            {
                openAssaultSlots = 1;
                proactiveAntiAir = true;
                Debug.Log($"[AI Shopping] reactive_anti_air_wide: {totalVisibleAir} aeronave(s) inimiga(s) visivel (longe do HQ), sem AAA no campo → slot assault para AAA");
            }
        }
        if (openAssaultSlots <= 0
            && !HasActivePrimaryRole(snapshot, UnitRole.Assalto)
            && CanAffordPurePrimaryRole(snapshot, UnitRole.Assalto, remaining))
        {
            openAssaultSlots = 1;
        }
        int rawOpenCapturerSlots = openCapturerSlots;
        openCapturerSlots = LimitCapturerDemandForProgression(snapshot, openCapturerSlots, openAssaultSlots, openTransportSlots, openLogisticsSlots, openFireSupportSlots);
        openCapturerSlots = RestoreCapturerDemandForIdleAirlift(snapshot, rawOpenCapturerSlots, openCapturerSlots);

        // Cap assault demand to 1 per 2 active capturers (min 1). Prevents the plan from
        // creating 5+ assault slots in T1 when all sectors are high-risk and no units exist,
        // which would otherwise cause the shopping loop to buy 5 tanks before any capturer.
        {
            int activeCap = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
            openAssaultSlots = Mathf.Min(openAssaultSlots, Mathf.Max(1, activeCap / 2));
        }

        // Escalada via cadeia eliteFrom: level 2 só fica disponível quando level 1 já está em campo
        // (chain gate em FindEliteAssaultReserveTarget). Não há mais necessidade de branch por contagem.
        int activeEliteAssaultCount = CountActiveEliteAssaultUnits(snapshot);
        const int DreamTeamEliteAssaultThreshold = 2;  // 2 elites → pivot para fire support
        UnitData eliteLevel2Candidate = FindEliteAssaultReserveTarget(snapshot, 2);
        UnitData eliteLevel1Candidate = FindEliteAssaultReserveTarget(snapshot, 1);
        UnitData eliteAssaultTarget = eliteLevel2Candidate ?? eliteLevel1Candidate;

        // Save the target for reserve purposes BEFORE the composition check may null it.
        // Reserve (saving money) applies even when composition isn't ready yet;
        // only the actual purchase is gated by composition.
        UnitData eliteAssaultTargetForReserve = eliteAssaultTarget;

        // Compra elite apenas quando a composição mínima do exército já foi atingida:
        // proporção dos slots de capturador preenchidos >= EliteCapturerFillRatio
        // E pelo menos MinFilledAssaultSlots slots de assault preenchidos.
        CountSlots(snapshot.AITeam, UnitRole.Capturador, out int totalCap, out int filledCap);
        if (eliteAssaultTarget != null)
        {
            CountSlots(snapshot.AITeam, UnitRole.Assalto, out int totalAss, out int filledAss);
            float capFill       = totalCap > 0 ? filledCap / (float)totalCap : 0f;
            float fillThreshold = Instance != null ? Instance.EliteCapturerFillRatio : 0.6f;
            int   minAssault    = Instance != null ? Instance.MinFilledAssaultSlots   : 1;
            bool  capOk         = capFill >= fillThreshold;
            bool  assOk         = filledAss >= minAssault;
            string status       = (capOk && assOk) ? "ELITE LIBERADO" : $"bloqueado ({(!capOk ? $"cap {filledCap}/{totalCap} {capFill:P0}<{fillThreshold:P0}" : "cap OK")} | {(!assOk ? $"ass {filledAss}<{minAssault}" : "ass OK")})";
            Debug.Log($"[AI Shopping] composição: cap={filledCap}/{totalCap} ({capFill:P0}) ass={filledAss}/{totalAss} — {status}");
            if (!capOk || !assOk)
                eliteAssaultTarget = null; // blocks purchase; eliteAssaultTargetForReserve still set
        }

        if (eliteAssaultTarget != null && openAssaultSlots <= 0 && remaining >= eliteAssaultTarget.cost)
        {
            openAssaultSlots = 1;
            Debug.Log($"[AI Shopping] elite excedente liberado: {eliteAssaultTarget.displayName} custo={eliteAssaultTarget.cost} cash={remaining}");
        }

        UnitData eliteFireSupportTarget = FindEliteFireSupportReserveTarget(snapshot, preferDefensiveFireSupport, remaining);
        // Proactive SAM: AAA in field satisfies chain gate — find SAM as elite fire support target.
        if (proactiveSAM)
        {
            UnitData samTarget = FindEliteFireSupportReserveTarget(snapshot, true, remaining, requireChain: true);
            if (samTarget != null && IsAntiAirOnlyUnit(samTarget)
                && (eliteFireSupportTarget == null || !IsAntiAirOnlyUnit(eliteFireSupportTarget)))
            {
                eliteFireSupportTarget = samTarget;
                Debug.Log($"[AI Shopping] proactive_anti_air: SAM target={samTarget.displayName} custo={samTarget.cost}");
            }
        }
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
            eliteFireSupportTarget = FindEliteFireSupportReserveTarget(snapshot, preferDefensiveFireSupport: true, remaining, requireChain: false);
            Debug.Log($"[AI Shopping] dream_team_pivot: {activeEliteAssaultCount} elite assault em campo → target={eliteFireSupportTarget?.displayName ?? "nenhum"} custo={eliteFireSupportTarget?.cost ?? 0}");
        }

        bool eliteFireSupportReserveReady = IsEliteFireSupportReserveReady(snapshot);
        bool eliteFireSupportNowAffordable = eliteFireSupportTarget != null && remaining >= eliteFireSupportTarget.cost;
        float eliteFireCapFill = totalCap > 0 ? filledCap / (float)totalCap : 0f;
        float eliteFireFillThreshold = Instance != null ? Instance.EliteCapturerFillRatio : 0.6f;
        bool wantsEliteFireSupport = eliteFireSupportTarget != null
            && (eliteFireSupportReserveReady || eliteFireSupportNowAffordable)
            && (dreamTeamPivot || (activeFireSupportCount > 0 && eliteFireCapFill >= eliteFireFillThreshold));
        // Proactive SAM bypasses the composition gate — buy immediately if affordable.
        if (proactiveSAM && eliteFireSupportTarget != null
            && IsAntiAirOnlyUnit(eliteFireSupportTarget) && eliteFireSupportNowAffordable)
            wantsEliteFireSupport = true;
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

        int cheapestTransportCost    = openTransportSlots    > 0 ? FindCheapestAvailableTransportCost(snapshot)    : 0;
        int cheapestAirTransportCost = openAirTransportSlots > 0 ? FindCheapestAirTransportCost(snapshot)         : 0;
        int reserveForAirTransport   = 0;
        if (cheapestAirTransportCost > 0 && openAirTransportSlots > 0)
        {
            reserveForAirTransport = Mathf.Min(remaining, cheapestAirTransportCost * openAirTransportSlots);
            Debug.Log($"[AI Shopping] reserva_ar: air_slots={openAirTransportSlots} custo={cheapestAirTransportCost} reserva={reserveForAirTransport}");
        }
        int anyAirCombatDemand    = openCacaBSlots + openCacaASlots + openApacheSlots + openBombaSlots;
        int cheapestAirCombatCost = anyAirCombatDemand > 0 ? FindCheapestAirCombatCost(snapshot) : 0;
        int reserveForAirCombat   = 0;
        if (cheapestAirCombatCost > 0 && anyAirCombatDemand > 0)
        {
            int budgetAfterAirTransport = Mathf.Max(0, remaining - reserveForAirTransport);
            reserveForAirCombat = Mathf.Min(budgetAfterAirTransport, cheapestAirCombatCost * Mathf.Min(anyAirCombatDemand, 2));
            Debug.Log($"[AI Shopping] reserva_combate_ar: slots={anyAirCombatDemand} custo={cheapestAirCombatCost} reserva={reserveForAirCombat}");
        }

        Debug.Log($"[AI Shopping] budget={remaining} cap_slots={openCapturerSlots} ass_slots={openAssaultSlots} trans_slots={openTransportSlots} trans_urgent={urgentTransportDemand} air_trans_slots={openAirTransportSlots} log_slots={openLogisticsSlots} repairs={repairDemandCount} active_log={activeLogisticsCount} fire_slots={openFireSupportSlots} fire_def={preferDefensiveFireSupport} cacaB_slots={openCacaBSlots} cacaA_slots={openCacaASlots} apache_slots={openApacheSlots} bomba_slots={openBombaSlots} cheapest_transport={cheapestTransportCost} cheapest_air={cheapestAirTransportCost} reserva_ar={reserveForAirTransport} onlyCap={onlyCapturers} onlyAss={onlyAssault} onlyTrans={onlyTransporter} onlyLog={onlyLogistics} onlyFire={onlyFireSupport}");

        bool strategicEliteAssaultReserve = eliteAssaultTarget != null
            && !dreamTeamPivot
            && openCapturerSlots <= 0
            && openAssaultSlots <= 0
            && openTransportSlots <= 0
            && openLogisticsSlots <= 0
            && openFireSupportSlots <= 0
            && IsEliteAssaultReserveReady(snapshot);
        float reserveCapFill = totalCap > 0 ? filledCap / (float)totalCap : 0f;
        float reserveCapThreshold = (Instance != null ? Instance.EliteCapturerFillRatio : 0.6f) * 0.85f;
        bool nextTurnEliteAssaultReserve = eliteAssaultTargetForReserve != null
            && !dreamTeamPivot
            && remaining < eliteAssaultTargetForReserve.cost
            && remaining + Mathf.Max(0, snapshot.IncomePerTurn) >= eliteAssaultTargetForReserve.cost
            && reserveCapFill >= reserveCapThreshold;
        bool wantsEliteAssault = eliteAssaultTarget != null
            && (openAssaultSlots > 0 || strategicEliteAssaultReserve || nextTurnEliteAssaultReserve);
        if (strategicEliteAssaultReserve)
            Debug.Log($"[AI Shopping] reserva estrategica elite assalto: composicao completa alvo={eliteAssaultTarget.displayName} custo={eliteAssaultTarget.cost}");
        else if (nextTurnEliteAssaultReserve)
            Debug.Log($"[AI Shopping] reserva proximo turno elite assalto: alvo={eliteAssaultTargetForReserve.displayName} custo={eliteAssaultTargetForReserve.cost} cash={remaining} income={snapshot.IncomePerTurn}");
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
        if (nextTurnEliteAssaultReserve)
        {
            int baseReserve = Mathf.Max(0, eliteAssaultTargetForReserve.cost - Mathf.Max(0, snapshot.IncomePerTurn));
            eliteAssaultSafetyBuffer = CalculateEliteAssaultSafetyBuffer(eliteAssaultTargetForReserve);
            reserveForEliteAssault = Mathf.Min(remaining, baseReserve + eliteAssaultSafetyBuffer);
        }

        if (reserveForEliteAssault > 0)
        {
            Debug.Log($"[AI Shopping] reserva elite assalto {eliteAssaultTargetForReserve.displayName} elite={eliteAssaultTargetForReserve.eliteLevel} custo={eliteAssaultTargetForReserve.cost} cash={remaining} income={snapshot.IncomePerTurn} reserva={reserveForEliteAssault} colchao={eliteAssaultSafetyBuffer} gastoLivre={Mathf.Max(0, remaining - reserveForEliteAssault)}");
        }

        // Separa edifícios por domínio das unidades que oferecem
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        var landBuildings = new List<ConstructionManager>();
        var airBuildings  = new List<ConstructionManager>();
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null) continue;
            bool offersLand = false, offersAir = false;
            if (b.OfferedUnits != null)
                foreach (UnitData u in b.OfferedUnits)
                {
                    if (u == null) continue;
                    if (u.domain == Domain.Land) offersLand = true;
                    else if (u.domain == Domain.Air) offersAir = true;
                }
            if (offersLand) landBuildings.Add(b);
            else if (offersAir) airBuildings.Add(b);
        }

        if (airBuildings.Count > 0)
            Debug.Log($"[AI Shopping] aerodromos={airBuildings.Count} air_trans_slots={openAirTransportSlots} custo_heli={cheapestAirTransportCost}");

        // T1: sem reserva de elite — gasta tudo em capturadores e suporte básico.
        // reserveForAirTransport é mantida para que soldados não consumam o orçamento do chinook.
        if (snapshot.TurnNumber <= 1)
        {
            reserveForEliteAssault      = 0;
            reserveForEliteFireSupport  = 0;
            reserveForAirCombat         = 0;
            wantsEliteAssault           = false;
            wantsEliteFireSupport       = false;
        }

        var sortedBuildings = landBuildings;
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
            if (!eliteAssaultBought)
            {
                bool canBuyEliteNow = wantsEliteAssault && !defensiveBaseThreat
                    && eliteAssaultTarget != null && remaining >= eliteAssaultTarget.cost;
                if (canBuyEliteNow)
                {
                    // Composition OK and can afford.
                    // If this building offers the elite: full budget.
                    // Otherwise: reserve the elite cost and allow spending the excess on other units —
                    // zeroing budget here causes the AI to accumulate cash when the elite factory is occupied.
                    spendBudget = CanOfferUnit(building, eliteAssaultTarget)
                        ? remaining
                        : Mathf.Max(0, remaining - eliteAssaultTarget.cost);
                }
                else if (reserveForEliteAssault > 0
                         && (!defensiveBaseThreat || (!defensiveArmorThreat && !emergencyProductionDefense)))
                {
                    // Saving for elite (composition not yet ready, or can't afford):
                    // honour the reserve in non-emergency scenarios.
                    spendBudget = Mathf.Min(spendBudget, Mathf.Max(0, remaining - reserveForEliteAssault));
                }
            }
            if (wantsEliteFireSupport && !eliteFireSupportBought)
            {
                if (!defensiveBaseThreat)
                {
                    if (remaining >= eliteFireSupportTarget.cost)
                        // Same fix: reserve elite fire support cost, spend excess elsewhere.
                        spendBudget = CanOfferUnit(building, eliteFireSupportTarget)
                            ? spendBudget
                            : Mathf.Max(0, spendBudget - eliteFireSupportTarget.cost);
                    else if (reserveForEliteFireSupport > 0)
                        spendBudget = Mathf.Min(spendBudget, Mathf.Max(0, remaining - reserveForEliteFireSupport));
                }
                else if (!defensiveArmorThreat && !emergencyProductionDefense
                         && remaining < eliteFireSupportTarget.cost && reserveForEliteFireSupport > 0)
                {
                    // Proximity threat only: still honour the elite fire support reserve.
                    spendBudget = Mathf.Min(spendBudget, Mathf.Max(0, remaining - reserveForEliteFireSupport));
                }
            }
            // Reserva para transporte aéreo + combate aéreo — suspensa em emergências defensivas.
            if ((reserveForAirTransport > 0 || reserveForAirCombat > 0) && !defensiveBaseThreat)
                spendBudget = Mathf.Min(spendBudget, Mathf.Max(0, remaining - reserveForAirTransport - reserveForAirCombat));

            // Log das opções deste edifício
            {
                var offerLog = new System.Text.StringBuilder();
                offerLog.Append($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} budget={spendBudget}/{remaining}:");
                if (building.OfferedUnits != null)
                    foreach (UnitData ou in building.OfferedUnits)
                        if (ou != null) offerLog.Append($" [{ou.displayName} ${ou.cost}]");
                Debug.Log(offerLog.ToString());
            }

            UnitData unit = PickUnit(building, snapshot, spendBudget, onlyCapturers, onlyAssault, onlyTransporter, onlyLogistics, onlyFireSupport, onlyAirTransporter,
                openCapturerSlots, openAssaultSlots, openTransportSlots, urgentTransportDemand, openLogisticsSlots, openFireSupportSlots, preferDefensiveFireSupport,
                eliteAssaultTarget, eliteFireSupportTarget, defensiveBaseThreat,
                allowDefensiveEliteAssault, defensiveTankReserveCost,
                defensiveBaseManpowerShortage, defensiveMassReserveCost, defensiveBaseTankBought,
                defensiveArmorThreat, wantsEliteFireSupport, activeFireSupportCount,
                proactiveDefFireSupport, proactiveAntiAir, activeSAMs, activeAAAs, aaaCap, aaaThreat);
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
            if (IsAntiAirOnlyUnit(unit) && IsPrimaryRole(unit, UnitRole.FogoIndireto))
                activeSAMs++;
            if (IsAntiAirOnlyUnit(unit) && IsPrimaryRole(unit, UnitRole.Assalto))
                activeAAAs++;

            if (remaining <= 0) break;
        }

        // Air shopping — separate pass over air buildings (transporters, interceptors, ground attack)
        {
            bool wantsAirTransport = openAirTransportSlots > 0;
            bool wantsCacaB        = openCacaBSlots  > 0;
            bool wantsCacaA        = openCacaASlots  > 0;
            bool wantsApache       = openApacheSlots > 0;
            bool wantsBomba        = openBombaSlots  > 0;
            bool anyAirDemand      = wantsAirTransport || wantsCacaB || wantsCacaA || wantsApache || wantsBomba;
            bool noLandOnlyFilter  = !onlyCapturers && !onlyAssault && !onlyTransporter && !onlyLogistics && !onlyFireSupport;
            bool airOnlyFilter     = onlyAirTransporter || onlyInterceptador || onlyAtaqueAereo;

            if (airBuildings.Count > 0 && anyAirDemand && (noLandOnlyFilter || airOnlyFilter))
            {
                foreach (ConstructionManager building in airBuildings)
                {
                    Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
                    if (!building.CanProduceUnitsForTeam(snapshot.AITeam))
                    {
                        Debug.Log($"[AI Shopping Air] {building.ConstructionDisplayName} @ {cell} — não produz para o time, pulando");
                        continue;
                    }
                    if (occupied.Contains(cell))
                    {
                        Debug.Log($"[AI Shopping Air] {building.ConstructionDisplayName} @ {cell} — célula ocupada, pulando");
                        continue;
                    }

                    UnitData airUnit = PickAirUnit(building, remaining,
                        wantsAirTransport, wantsCacaB, wantsCacaA, wantsApache, wantsBomba);
                    if (airUnit == null)
                    {
                        Debug.Log($"[AI Shopping Air] {building.ConstructionDisplayName} @ {cell} — sem unidade aérea disponível ou sem budget");
                        continue;
                    }

                    int idx = IndexOf(building.OfferedUnits, airUnit);
                    orders.Add(new ShoppingOrder { Building = building, UnitToBuy = airUnit, SelectedIndex = idx });
                    Debug.Log($"[AI Shopping Air] {building.ConstructionDisplayName} @ {cell} → compra {airUnit.displayName} ${airUnit.cost}");
                    remaining -= airUnit.cost;
                    occupied.Add(cell);

                    bool isIntercept = IsPrimaryRole(airUnit, UnitRole.Interceptador);
                    bool isAtaque    = IsPrimaryRole(airUnit, UnitRole.AtaqueAereo);
                    bool isElite     = airUnit.eliteLevel >= 1;
                    if (IsPrimaryRole(airUnit, UnitRole.Transportador)) { if (openAirTransportSlots > 0) openAirTransportSlots--; wantsAirTransport = openAirTransportSlots > 0; }
                    else if (isIntercept && !isElite)                   { if (openCacaBSlots  > 0) openCacaBSlots--;  wantsCacaB  = openCacaBSlots  > 0; }
                    else if (isIntercept &&  isElite)                   { if (openCacaASlots  > 0) openCacaASlots--;  wantsCacaA  = openCacaASlots  > 0; }
                    else if (isAtaque    && !isElite)                   { if (openApacheSlots > 0) openApacheSlots--; wantsApache = openApacheSlots > 0; }
                    else if (isAtaque    &&  isElite)                   { if (openBombaSlots  > 0) openBombaSlots--;  wantsBomba  = openBombaSlots  > 0; }

                    if (!wantsAirTransport && !wantsCacaB && !wantsCacaA && !wantsApache && !wantsBomba) break;
                    if (remaining <= 0) break;
                }
            }
        }

        return orders;
    }

    private static UnitData PickAirUnit(
        ConstructionManager building, int budget,
        bool wantsTransport, bool wantsCacaB, bool wantsCacaA, bool wantsApache, bool wantsBomba)
    {
        if (building == null || building.OfferedUnits == null) return null;

        UnitData best      = null;
        int      bestScore = int.MinValue;

        foreach (UnitData u in building.OfferedUnits)
        {
            if (u == null || u.cost > budget || u.domain != Domain.Air) continue;
            if (u.roles == null || u.roles.Count == 0) continue;

            UnitRole primary = u.roles[0];
            bool elite = u.eliteLevel >= 1;
            int score;
            if      (primary == UnitRole.Transportador && wantsTransport)       score = 10000 + u.cost;
            else if (primary == UnitRole.Interceptador && !elite && wantsCacaB) score = 25000 + u.cost;
            else if (primary == UnitRole.Interceptador &&  elite && wantsCacaA) score = 30000 + u.cost;
            else if (primary == UnitRole.AtaqueAereo   && !elite && wantsApache) score = 20000 + u.cost;
            else if (primary == UnitRole.AtaqueAereo   &&  elite && wantsBomba) score = 22000 + u.cost;
            else continue;

            Debug.Log($"[AI Shopping Air] candidato {u.displayName} ${u.cost} role={primary} elite={elite} score={score}");
            if (score > bestScore) { bestScore = score; best = u; }
        }
        return best;
    }

    // Caça B: anti-helicopter primary + anti-bomber backup (cheaper fallback when Caça A unavailable).
    // Turn gate is bypassed when enemy aircraft are already visible — respond to real threats regardless of turn.
    private static int ComputeCacaBDemand(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return 0;

        int enemyHelicos  = 0;
        int enemyBombers  = 0;
        if (snapshot.EnemyUnits != null)
            foreach (UnitManager u in snapshot.EnemyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain != Domain.Air) continue;
                UnitRole r = d.roles[0];
                if (r == UnitRole.Transportador || (r == UnitRole.AtaqueAereo && d.eliteLevel == 0))
                    enemyHelicos++;
                else if (r == UnitRole.AtaqueAereo && d.eliteLevel >= 1)
                    enemyBombers++;
            }

        // How many Caça A are already in field (covering bombers)?
        int activeCacaA = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain == Domain.Air && d.roles[0] == UnitRole.Interceptador && d.eliteLevel >= 1) activeCacaA++;
            }
        // Uncovered bombers create Caça B demand (backup role): each uncovered bomber needs 1 Caça B.
        int uncoveredBombers = Mathf.Max(0, enemyBombers - activeCacaA);

        int minTurn = Instance != null ? Instance.MinTurnForInterceptador : 4;
        bool tooEarly = snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn;
        bool hasVisibleThreat = enemyHelicos > 0 || uncoveredBombers > 0;
        // Bypass turn gate if enemy aircraft already visible — reactive threat overrides timing.
        if (tooEarly && !hasVisibleThreat) return 0;

        int ratio       = Instance != null ? Instance.HelicopterosPorCacaB : 3;
        int maxCacaB    = Instance != null ? Instance.MaxCacaB : 4;
        int minPresence = tooEarly ? 0 : (Instance != null ? Instance.MinCacaBPresence : 1);
        int heliDesired = Mathf.CeilToInt(enemyHelicos / (float)ratio);
        // Each uncovered bomber adds 1 Caça B demand (as backup coverage).
        int desired = Mathf.Max(minPresence, Mathf.Min(maxCacaB, heliDesired + uncoveredBombers));

        int active = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain == Domain.Air && d.roles[0] == UnitRole.Interceptador && d.eliteLevel == 0) active++;
            }

        int demand = Mathf.Max(0, desired - active);
        Debug.Log($"[AI Shopping] cacaB_demand: demand={demand} desired={desired} active={active} enemyHelicos={enemyHelicos} enemyBombers={enemyBombers} uncoveredBombers={uncoveredBombers} activeCacaA={activeCacaA} ratio=1:{ratio} max={maxCacaB} tooEarly={tooEarly} bypassed={tooEarly && hasVisibleThreat}");
        return demand;
    }

    // Caça A: 1 per enemy Caça B or Bombardeiro visible, capped at MaxCacaA.
    // Turn gate is bypassed when enemy aircraft are already visible — respond to real threats regardless of turn.
    private static int ComputeCacaADemand(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return 0;

        int enemyFighters = 0;
        if (snapshot.EnemyUnits != null)
            foreach (UnitManager u in snapshot.EnemyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain != Domain.Air) continue;
                UnitRole r = d.roles[0];
                // Counts: enemy Caça B/A (Interceptador) and Bombardeiro (AtaqueAereo elite)
                if (r == UnitRole.Interceptador || (r == UnitRole.AtaqueAereo && d.eliteLevel >= 1))
                    enemyFighters++;
            }

        int minTurn = Instance != null ? Instance.MinTurnForInterceptador : 4;
        bool tooEarly = snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn;
        // Bypass turn gate if enemy fighters/bombers already visible.
        if (tooEarly && enemyFighters == 0) return 0;

        int maxCacaA = Instance != null ? Instance.MaxCacaA : 2;
        int desired  = Mathf.Min(maxCacaA, enemyFighters);
        Debug.Log($"[AI Shopping] cacaA_demand: tooEarly={tooEarly} bypassed={tooEarly && enemyFighters>0} enemyFighters={enemyFighters}");

        int active = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.roles[0] == UnitRole.Interceptador && d.eliteLevel >= 1) active++;
            }

        int demand = Mathf.Max(0, desired - active);
        Debug.Log($"[AI Shopping] cacaA_demand: demand={demand} desired={desired} active={active} enemyFighters={enemyFighters} max={maxCacaA}");
        return demand;
    }

    // Apache: 1 per ChinooksPorApache own Chinooks in field. +1 in defensive mode if ComprarApacheEmModoDefesa.
    private static int ComputeApacheDemand(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return 0;
        int minTurn = Instance != null ? Instance.MinTurnForAtaqueAereo : 5;
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn) return 0;

        int activeChinooks = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain == Domain.Air && d.roles[0] == UnitRole.Transportador) activeChinooks++;
            }

        int ratio       = Instance != null ? Instance.ChinooksPorApache : 2;
        int minPresence = Instance != null ? Instance.MinApachePresence : 1;
        // MinApachePresence garante compra independente da quantidade de Chinooks.
        int desired = Mathf.Max(minPresence, Mathf.CeilToInt(activeChinooks / (float)ratio));

        bool defenseBonus = snapshot.Stance == AIStance.Defensive
            && Instance != null && Instance.ComprarApacheEmModoDefesa;
        if (defenseBonus) desired = Mathf.Max(desired, 1);

        int active = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.roles[0] == UnitRole.AtaqueAereo && d.eliteLevel == 0) active++;
            }

        int demand = Mathf.Max(0, desired - active);
        Debug.Log($"[AI Shopping] apache_demand: demand={demand} desired={desired} active={active} chinooks={activeChinooks} ratio=1:{ratio} defBonus={defenseBonus}");
        return demand;
    }

    // Bombardeiro: 1 per ApachesParaBombardeiro active Apaches. No cap — scales to endgame.
    private static int ComputeBombaDemand(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return 0;
        int minTurn = Instance != null ? Instance.MinTurnForAtaqueAereo : 5;
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn) return 0;

        int activeApaches = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.roles[0] == UnitRole.AtaqueAereo && d.eliteLevel == 0) activeApaches++;
            }

        int ratio       = Instance != null ? Instance.ApachesParaBombardeiro : 2;
        int minPresence = Instance != null ? Instance.MinBombaPresence : 0;
        int desired     = Mathf.Max(minPresence, Mathf.FloorToInt(activeApaches / (float)ratio));

        int active = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.roles[0] == UnitRole.AtaqueAereo && d.eliteLevel >= 1) active++;
            }

        int demand = Mathf.Max(0, desired - active);
        Debug.Log($"[AI Shopping] bomba_demand: demand={demand} desired={desired} active={active} apaches={activeApaches} ratio=1:{ratio}");
        return demand;
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
        bool onlyAirTransporter,
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
        bool defensiveArmorThreat = false,
        bool wantsEliteFireSupport = false,
        int activeFireSupportCount = 0,
        bool proactiveDefFireSupport = false,
        bool proactiveAntiAir = false,
        int activeSAMs = 0,
        int activeAAAs = 0,
        int aaaCap = 0,
        bool aaaThreat = false)
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
            bool isAntiAirOnly = IsAntiAirOnlyUnit(u);
            bool isSAMType = isAntiAirOnly && IsPrimaryRole(u, UnitRole.FogoIndireto);
            bool isAAAType = isAntiAirOnly && IsPrimaryRole(u, UnitRole.Assalto);
            int samCap = Instance != null ? Instance.MaxProactiveAntiAirSAM : 3;
            if (isSAMType && activeSAMs >= samCap)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — SAM cap atingido ({activeSAMs}/{samCap})");
                continue;
            }
            if (isAAAType && aaaCap > 0 && activeAAAs >= aaaCap)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — AAA cap atingido ({activeAAAs}/{aaaCap} aeronaves vis.)");
                continue;
            }
            if (isAntiAirOnly && !HasAnyAirThreat() && !proactiveAntiAir)
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
            bool isDefensiveOnlyUnit = u.aiPurchaseMode == AIPurchaseMode.Defensive;
            bool isOffensiveOnlyUnit = u.aiPurchaseMode == AIPurchaseMode.Offensive;

            bool proactiveAntiAirSAMBypass = proactiveAntiAir && isSAMType;
            bool proactiveAntiAirAAABypass = proactiveAntiAir && isAAAType && (aaaCap == 0 || activeAAAs < aaaCap);
            bool proactiveDefBypass = (proactiveDefFireSupport || proactiveAntiAirSAMBypass) && isDefensiveOnlyUnit && isFireSupportCapable;
            if (!defensiveBaseThreat && isDefensiveOnlyUnit && !proactiveDefBypass && !proactiveAntiAirAAABypass)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — Defensive-only, sem ameaça"); continue; }
            if (defensiveBaseThreat && isOffensiveOnlyUnit)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — Offensive-only, modo defensivo"); continue; }

            if (isPrimaryCapturer && openCapturerSlots <= 0)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda capturador");
                continue;
            }

            if (isPrimaryLogistics && openLogisticsSlots <= 0)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda logistics");
                continue;
            }

            if (isPrimaryAssault && !isHybridCapturer && openAssaultSlots <= 0 && !defensiveBaseThreat && !proactiveAntiAirAAABypass)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda assault");
                continue;
            }

            if (isPrimaryTransporter && openTransportSlots <= 0 && !urgentTransportDemand && !defensiveBaseThreat)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda transporte");
                continue;
            }

            bool defensiveFireSupportBypass = defensiveBaseThreat && isDefensiveOnlyUnit && isFireSupportCapable;
            if (isFireSupportCapable && !isPrimaryAssault && openFireSupportSlots <= 0 && !defensiveFireSupportBypass)
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
            bool isAAADefense = isAAAType && aaaThreat && activeAAAs < aaaCap;
            if (defensiveBaseThreat && canBuyLogistics)
            {
                // Logistics demand remains valid during defense, but scores below direct combat buys.
            }
            else if (defensiveBaseThreat
                && !isDefensiveOnlyUnit
                && !IsDefensiveBaseThreatPurchase(u)
                && !isAllowedDefensiveElite
                && !isAllowedDefensiveFireSupport
                && !canAffordDefensiveTank
                && !canBuyBasicMass
                && !isAAADefense) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — defThreat filter (notThreat={!IsDefensiveBaseThreatPurchase(u)} notElite={!isAllowedDefensiveElite} notTank={!canAffordDefensiveTank} notMass={!canBuyBasicMass} notAAA={!isAAADefense})"); continue; }
            if (!defensiveBaseThreat && isHybridCapturer && !hasOpenDefensiveSlot) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — hybrid sem slot defensivo"); continue; }
            if ((onlyCapturers || onlyAssault || onlyTransporter || onlyLogistics || onlyFireSupport || onlyAirTransporter)
                && !((onlyCapturers && isPrimaryCapturer) || (onlyAssault && isPrimaryAssault) || (onlyTransporter && isPrimaryTransporter) || (onlyLogistics && isPrimaryLogistics) || (onlyFireSupport && isPrimaryFireSupport))) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — onlyFilter (cap={isPrimaryCapturer} ass={isPrimaryAssault} trans={isPrimaryTransporter} log={isPrimaryLogistics} fire={isPrimaryFireSupport} airTrans={onlyAirTransporter})"); continue; }

            int score = u.cost;
            if (defensiveBaseThreat && isDefensiveOnlyUnit && isFireSupportCapable)
                score += 55000; // below Bazooka (90k) and tanks (180k) but valid as cheap defensive fire support
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
                if (activeFireSupportCount == 0 && u.eliteLevel >= 1)
                    score -= 120000; // prefer non-elite on first fire support buy
                if (wantsEliteFireSupport && u == eliteFireSupportTarget) score += 200000;
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

            if (isAAADefense)
                score += 100000; // visible aircraft near HQ: strongly prefer AAA over other assault units
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

        HashSet<UnitData> inField = BuildFieldUnitDataSet(snapshot);
        UnitData best = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.domain != Domain.Land) continue;
                if (unit.unitClass != GameUnitClass.Armored) continue;
                if (unit.roles == null || !unit.roles.Contains(UnitRole.Assalto)) continue;
                if (unit.roles.Contains(UnitRole.Capturador)) continue;
                if (unit.roles.Contains(UnitRole.Transportador)) continue;
                if (unit.eliteLevel < minEliteLevel) continue;
                // Chain gate: predecessor must be in field (if chain is configured)
                if (unit.eliteFrom != null && !inField.Contains(unit.eliteFrom)) continue;

                if (best == null
                    || unit.eliteLevel < best.eliteLevel
                    || (unit.eliteLevel == best.eliteLevel && unit.cost < best.cost))
                    best = unit;
            }
        }

        return best;
    }

    private static HashSet<UnitData> BuildFieldUnitDataSet(AIWorldSnapshot snapshot)
    {
        var set = new HashSet<UnitData>();
        if (snapshot?.MyUnits == null) return set;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead) continue;
            if (unit.TryGetUnitData(out UnitData data) && data != null)
                set.Add(data);
        }
        return set;
    }

    private static int CalculateEliteAssaultSafetyBuffer(UnitData eliteAssault)
    {
        if (eliteAssault == null) return 0;
        float percent = Instance != null ? Mathf.Clamp(Instance.SavingPercentualForElite, 0f, 20f) : 15f;
        if (percent <= 0f) return 0;
        return Mathf.CeilToInt(Mathf.Max(0, eliteAssault.cost) * (percent / 100f));
    }

    private static UnitData FindEliteFireSupportReserveTarget(AIWorldSnapshot snapshot, bool preferDefensiveFireSupport, int budget = int.MaxValue, bool requireChain = true)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return null;

        HashSet<UnitData> inField = requireChain ? BuildFieldUnitDataSet(snapshot) : null;
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
                if (IsPrimaryRole(unit, UnitRole.Assalto)) continue; // assault-primary units go through elite assault path
                // Chain gate: predecessor must be in field (bypassed for dreamTeamPivot)
                if (requireChain && unit.eliteFrom != null && !inField.Contains(unit.eliteFrom)) continue;

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

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);

        // T1 / opener: no active capturers yet — buy the full demand as the plan specifies.
        // The batch limit exists to slow down purchases once the army is running, not to cap T1.
        if (activeCapturers == 0)
            return openCapturerSlots;

        int batchSize = Instance != null ? Instance.ProgressiveCapturerBatchSize : 2;
        int capped = Mathf.Min(openCapturerSlots, Mathf.Max(1, batchSize));
        int supportDemand = openAssaultSlots + openTransportSlots + openLogisticsSlots + openFireSupportSlots;

        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);

        // Dynamic pause threshold: scales with total capturer slots so the same
        // fill-ratio breakpoint applies on both small and large maps.
        CountSlots(snapshot.AITeam, UnitRole.Capturador, out int totalCapSlots, out int _);
        int supportPauseThreshold;
        if (totalCapSlots > 0)
        {
            float pauseRatio = (Instance != null ? Instance.EliteCapturerFillRatio : 0.6f) * 0.85f;
            supportPauseThreshold = Mathf.Max(1, Mathf.CeilToInt(totalCapSlots * pauseRatio));
        }
        else
        {
            supportPauseThreshold = Instance != null ? Instance.CapturersPerPreventiveTransport : 4;
        }

        // Pause capturer purchases once we have enough active capturers.
        // supportDemand check removed: when all support slots are already filled (demand=0)
        // and activeCap exceeds the threshold, we should still stop buying more capturers.
        if (activeCapturers >= supportPauseThreshold && activeAssault >= 1)
            capped = 0;

        if (capped != openCapturerSlots)
        {
            Debug.Log($"[AI Shopping] capturer_progression: raw={openCapturerSlots} capped={capped} activeCap={activeCapturers} activeAss={activeAssault} supportDemand={supportDemand} batch={batchSize} pauseAt={supportPauseThreshold} totalCapSlots={totalCapSlots}");
        }

        return capped;
    }

    private static int RestoreCapturerDemandForIdleAirlift(AIWorldSnapshot snapshot, int rawOpenCapturerSlots, int cappedOpenCapturerSlots)
    {
        if (cappedOpenCapturerSlots > 0 || rawOpenCapturerSlots <= 0 || snapshot == null)
            return cappedOpenCapturerSlots;
        if (!MapNeedsAirTransport(snapshot, out int minDist))
            return cappedOpenCapturerSlots;

        int emptyAirTransporters = CountAirTransporters(snapshot, requireEmpty: true);
        if (emptyAirTransporters <= 0)
            return cappedOpenCapturerSlots;

        const int HeliCapacity = 2;
        int pickupCapturers = CountAirTransportPickupCapturers(snapshot);
        int spareSeats = Mathf.Max(0, emptyAirTransporters * HeliCapacity - pickupCapturers);
        if (spareSeats <= 0)
            return cappedOpenCapturerSlots;

        int batchSize = Instance != null ? Instance.ProgressiveCapturerBatchSize : 2;
        int airliftSeats = Mathf.Max(1, spareSeats);
        int restored = Mathf.Max(rawOpenCapturerSlots, airliftSeats);
        restored = Mathf.Min(restored, spareSeats);
        Debug.Log($"[AI Shopping] capturer_airlift_feed: raw={rawOpenCapturerSlots} capped={cappedOpenCapturerSlots}->{restored} emptyAir={emptyAirTransporters} pickupCap={pickupCapturers} spareSeats={spareSeats} batch={batchSize} airliftSeats={airliftSeats} minDist={minDist}");
        return restored;
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

    private static int CountVisibleEnemyAircraftNearHQ(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot == null || snapshot.EnemyUnits == null || snapshot.MyBuildings == null) return 0;

        Vector3Int hqCell = Vector3Int.zero;
        bool hqFound = false;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.IsPlayerHeadQuarter) continue;
            hqCell = b.CurrentCellPosition; hqCell.z = 0;
            hqFound = true;
            break;
        }
        if (!hqFound) return 0;

        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData d) || d == null) continue;
            if (d.domain != Domain.Air) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(hqCell, ec) <= range) count++;
        }
        return count;
    }

    private static int CountTotalVisibleEnemyAircraft(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.EnemyUnits == null) return 0;
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData d) || d == null) continue;
            if (d.domain == Domain.Air) count++;
        }
        return count;
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
            // APCs are logistics, not assault armor — don't treat them as armor threats.
            if (enemyData.roles != null && enemyData.roles.Count > 0 && enemyData.roles[0] == UnitRole.Transportador) continue;
            // Only elite tanks (level >= 1) justify emergency defense spending.
            if (enemyData.eliteLevel < 1) continue;

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

    private static bool ComputeProactiveAntiAirNeeded(AIWorldSnapshot snapshot, out int activeSAMs, out int activeAAAs)
    {
        activeSAMs = 0;
        activeAAAs = 0;
        if (snapshot?.MyUnits == null) return false;

        foreach (UnitManager u in snapshot.MyUnits)
        {
            if (u == null || u.IsDead || u.IsEmbarked) continue;
            if (!u.TryGetUnitData(out UnitData d) || d == null) continue;
            if (!IsAntiAirOnlyUnit(d)) continue;
            if (IsPrimaryRole(d, UnitRole.FogoIndireto)) activeSAMs++;
            else if (IsPrimaryRole(d, UnitRole.Assalto)) activeAAAs++;
        }

        // Reactive case (air already on map) is handled by the normal gate in PickUnit.
        if (HasAnyAirThreat()) return false;

        int minTurn = Instance != null ? Instance.MinTurnForFireSupport : 3;
        bool richEarly = HasPreventiveDefenseBudget(snapshot);
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn && !richEarly) return false;

        bool attackStance = snapshot.Stance == AIStance.Offensive || snapshot.Stance == AIStance.Tactical;
        if (!attackStance) return false;

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault   = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto,    requirePrimary: true);
        int minCap = Instance != null ? Instance.MinActiveCapturersForFireSupport : 2;
        int minAss = Instance != null ? Instance.MinActiveAssaultForFireSupport   : 1;
        bool armyReady = activeCapturers >= minCap && activeAssault >= minAss;

        Debug.Log($"[AI Shopping] proactive_anti_air: armyReady={armyReady} activeSAMs={activeSAMs} activeAAAs={activeAAAs} stance={snapshot.Stance} turn={snapshot.TurnNumber}/{minTurn} richEarly={richEarly} cap={activeCapturers}/{minCap} ass={activeAssault}/{minAss}");
        return armyReady;
    }

    private static bool HasPreventiveDefenseBudget(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return false;
        int income = Mathf.Max(1, snapshot.IncomePerTurn);
        return snapshot.Budget >= 40000 || snapshot.Budget >= Mathf.Max(20000, income * 2);
    }

    // Garante presença mínima de artilharia e anti-aérea na base, independente de ameaça visível.
    private static void ComputeGuaranteedBaseDefense(AIWorldSnapshot snapshot,
        out int openArtSlots, out bool forceBaseAAA)
    {
        openArtSlots = 0; forceBaseAAA = false;
        if (snapshot == null) return;
        int minTurn = Instance != null ? Instance.MinTurnBaseDefense : 3;
        bool richEarly = HasPreventiveDefenseBudget(snapshot);
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn && !richEarly)
        {
            Debug.Log($"[AI Shopping] base_defense: bloqueado por turno {snapshot.TurnNumber}<{minTurn} budget={snapshot.Budget}");
            return;
        }

        int minArt = Instance != null ? Instance.MinBaseArtilharia : 1;
        int minAAA = Instance != null ? Instance.MinBaseAAA : 1;

        int activeArt = 0, activeAntiAir = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d == null) continue;
                if (d.roles != null && d.roles.Contains(UnitRole.FogoIndireto) && !IsAntiAirOnlyUnit(d)) activeArt++;
                if (IsAntiAirOnlyUnit(d)) activeAntiAir++;
            }

        openArtSlots = Mathf.Max(0, minArt - activeArt);
        forceBaseAAA = activeAntiAir < minAAA;
        Debug.Log($"[AI Shopping] base_defense: activeArt={activeArt}/{minArt} activeAAA={activeAntiAir}/{minAAA} artSlots={openArtSlots} forceAAA={forceBaseAAA} richEarly={richEarly}");
    }

    private static bool ComputeProactiveDefensiveFireSupportNeeded(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return false;

        int cap = Instance != null ? Instance.MaxProactiveDefensiveFireSupport : 1;
        if (cap <= 0) return false;

        int minTurn = Instance != null ? Instance.MinTurnForFireSupport : 3;
        bool richEarly = HasPreventiveDefenseBudget(snapshot);
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn && !richEarly) return false;

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault   = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto,    requirePrimary: true);
        int minCap = Instance != null ? Instance.MinActiveCapturersForFireSupport : 2;
        int minAss = Instance != null ? Instance.MinActiveAssaultForFireSupport   : 1;
        if (activeCapturers < minCap || activeAssault < minAss) return false;

        int activeDefFS = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d == null) continue;
                if (IsDefensiveFireSupportPurchase(d)) activeDefFS++;
            }

        bool needed = activeDefFS < cap;
        Debug.Log($"[AI Shopping] proactive_def_fire_support: needed={needed} activeDefFS={activeDefFS} cap={cap} cap={activeCapturers}/{minCap} ass={activeAssault}/{minAss} richEarly={richEarly}");
        return needed;
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

        bool hasNeed = defensiveNeed || offensiveNeed || snapshot.Stance == AIStance.Tactical;
        int ratio = Instance != null ? Instance.AssaultPerFireSupportRatio : 2;
        int desiredFireSupport = hasNeed
            ? Mathf.Max(1, Mathf.CeilToInt(activeAssault / (float)ratio))
            : 0;
        int demand = Mathf.Max(0, desiredFireSupport - activeFireSupport);
        Debug.Log($"[AI Shopping] fire_support_demand: demand={demand} desired={desiredFireSupport} activeFire={activeFireSupport} activeAss={activeAssault} ratio=1:{ratio} stance={snapshot.Stance} defensive={defensiveNeed} offensive={offensiveNeed} preferDef={preferDefensiveFireSupport}");
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
        if (repairDemandCount >= 1) desiredLogistics = 1;
        if (repairDemandCount >= 3) desiredLogistics = 2;
        if (repairDemandCount >= 5) desiredLogistics = 3;
        if (repairDemandCount >= 7) desiredLogistics = 4;

        // Army-size floor for preventive maintenance coverage.
        // Only scales up when there is already repair demand (avoids over-buying early).
        if (desiredLogistics >= 1)
        {
            int myUnitCount = snapshot?.MyUnits?.Count ?? 0;
            int desiredBySize = myUnitCount >= 35 ? 4 : myUnitCount >= 20 ? 3 : myUnitCount >= 13 ? 2 : 1;
            desiredLogistics = Mathf.Max(desiredLogistics, desiredBySize);
        }

        int demand = Mathf.Max(0, desiredLogistics - activeLogisticsCount);
        Debug.Log($"[AI Shopping] logistics_demand: demand={demand} repairs={repairDemandCount} activeLog={activeLogisticsCount} desired={desiredLogistics} units={snapshot?.MyUnits?.Count ?? 0}");
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

        // Count land transporters only — air transporters are handled separately.
        int activeTransporters = 0;
        int freeAPCs = 0;
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.TeamId != aiTeam || u.IsDead || u.IsEmbarked) continue;
            if (!u.TryGetUnitData(out UnitData d)) continue;
            if (d.roles == null || d.roles.Count == 0 || d.roles[0] != UnitRole.Transportador) continue;
            if (d.domain == Domain.Air) continue;
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

            bool sectorInfoFound = SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info);
            // Air-preferred sectors are handled by ComputeAirTransportDemand.
            if (sectorInfoFound && info.GetTransportPreference(aiTeam) == SectorManager.SectorInfo.TransportPreference.Air)
                continue;

            if (hasOpenTransportSlot
                && activeCapturers >= 2
                && activeAssault >= 1
                && ObjectiveHasOpenOrFilledCapturer(obj)
                && sectorInfoFound
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

        CountSlots(snapshot.AITeam, UnitRole.Capturador, out int totalCapSlotsForMass, out int _);
        int capturersPerTransport;
        if (totalCapSlotsForMass > 0)
        {
            float massRatio = (Instance != null ? Instance.EliteCapturerFillRatio : 0.6f) * 0.85f;
            capturersPerTransport = Mathf.Max(1, Mathf.CeilToInt(totalCapSlotsForMass * massRatio));
        }
        else
        {
            capturersPerTransport = Instance != null ? Instance.CapturersPerPreventiveTransport : 4;
        }
        int massNeeded = 0;
        if (activeCapturers >= capturersPerTransport && activeAssault >= 1)
            massNeeded = activeCapturers / Mathf.Max(1, capturersPerTransport);

        int assignedDeficit = Mathf.Max(0, assignedNeeded - freeAPCs);
        int preventiveDeficit = Mathf.Max(0, Mathf.Max(preventiveNeeded, massNeeded) - activeTransporters);
        urgentTransportDemand = assignedDeficit > 0;
        int needed = Mathf.Max(assignedNeeded, Mathf.Max(preventiveNeeded, massNeeded));
        int deficit = urgentTransportDemand ? assignedDeficit : preventiveDeficit;
        int demand  = Mathf.Min(deficit, 1); // cap: max 1 APC per shopping round
        Debug.Log($"[AI Shopping] transport_demand: needed={needed} assigned={assignedNeeded} preventive={preventiveNeeded} mass={massNeeded} capPerTrans={capturersPerTransport} activeCap={activeCapturers} activeAss={activeAssault} activeAPCs={activeTransporters} freeAPCs={freeAPCs} assignedDef={assignedDeficit} preventiveDef={preventiveDeficit} urgent={urgentTransportDemand} demand={demand} minDist={minDist}");
        return demand;
    }

    private static int ComputeAirTransportDemand(AIWorldSnapshot snapshot, int openCapturerSlots = 0)
    {
        TeamId aiTeam = snapshot.AITeam;

        // Gate: map must have at least one uncaptured sector far enough to warrant air transport.
        // Mirrors the plan evaluator's transport-slot logic but reads SectorManager directly,
        // so helicopters can be bought pre-emptively in T1 before the plan is populated.
        if (!MapNeedsAirTransport(snapshot, out int minDist))
        {
            Debug.Log($"[AI Shopping] air_transport_demand: mapa pequeno (threshold={minDist}) → demand=0");
            return 0;
        }

        int activeAirTransporters = CountAirTransporters(snapshot, requireEmpty: false);
        int activeGroundCapturers = 0;
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.TeamId != aiTeam || u.IsDead || u.IsEmbarked) continue;
            if (!u.TryGetUnitData(out UnitData d)) continue;
            if (d.roles == null || d.roles.Count == 0) continue;
            if (d.roles[0] == UnitRole.Capturador && d.domain == Domain.Land)
                activeGroundCapturers++;
        }

        // Demand = ceil(troops needing transport / helicopter capacity).
        // T1 (no active capturers yet): mirror LimitCapturerDemandForProgression — full opener will be bought.
        // T2+: only capturers still near the home air pickup area + this turn's batch.
        const int HeliCapacity = 2;
        int pickupCapturers = CountAirTransportPickupCapturers(snapshot);
        int troopsNeedingTransport;
        if (activeGroundCapturers == 0)
        {
            troopsNeedingTransport = openCapturerSlots;
        }
        else
        {
            int batchSize = Instance != null ? Instance.ProgressiveCapturerBatchSize : 2;
            int incomingCapturers = openCapturerSlots > 0 ? batchSize : 0;
            troopsNeedingTransport = pickupCapturers + incomingCapturers;
        }
        if (troopsNeedingTransport <= 0)
        {
            Debug.Log($"[AI Shopping] air_transport_demand: 0 sem passageiro pickup/base groundCap={activeGroundCapturers} pickupCap={pickupCapturers} openCapSlots={openCapturerSlots} activeAirTrans={activeAirTransporters} minDist={minDist}");
            return 0;
        }
        int helicoptersNeeded = Mathf.CeilToInt((float)troopsNeedingTransport / HeliCapacity);

        int maxFleet = Instance != null ? Instance.MaxAirTransporters : 3;
        int demand = Mathf.Max(0, Mathf.Min(helicoptersNeeded, maxFleet) - activeAirTransporters);
        Debug.Log($"[AI Shopping] air_transport_demand: groundCap={activeGroundCapturers} pickupCap={pickupCapturers} openCapSlots={openCapturerSlots} troops={troopsNeedingTransport} heliCap={HeliCapacity} heliNeeded={helicoptersNeeded} activeAirTrans={activeAirTransporters} maxFleet={maxFleet} minDist={minDist} demand={demand}");
        return demand;
    }

    private static bool MapNeedsAirTransport(AIWorldSnapshot snapshot, out int minDist)
    {
        TeamId aiTeam = snapshot != null ? snapshot.AITeam : TeamId.Neutral;
        minDist = AIController.Instance != null
            ? AIController.Instance.GetEffectiveTransportThreshold(aiTeam) : 7;

        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
        {
            if (info.IsFullyControlled && info.ControllingTeam == aiTeam) continue;
            if (info.GetDistanceToHQ(aiTeam) >= minDist) return true;
        }

        foreach (SectorManager.SectorInfo baseInfo in SectorManager.GetAllBaseInfos())
        {
            // Own base is at distance ~0; only enemy base has large enough distance.
            if (baseInfo.GetDistanceToHQ(aiTeam) >= minDist) return true;
        }

        return false;
    }

    private static int CountAirTransporters(AIWorldSnapshot snapshot, bool requireEmpty)
    {
        if (snapshot == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.TeamId != snapshot.AITeam || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != Domain.Air || !IsPrimaryRole(data, UnitRole.Transportador))
                continue;
            if (requireEmpty && HasTransportCargo(unit))
                continue;
            count++;
        }

        return count;
    }

    private static bool HasTransportCargo(UnitManager unit)
    {
        if (unit == null || unit.TransportedUnitSlots == null)
            return false;
        foreach (UnitTransportSeatRuntime seat in unit.TransportedUnitSlots)
        {
            if (seat != null && seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked)
                return true;
        }
        return false;
    }

    private static int CountAirTransportPickupCapturers(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return 0;

        var pickupCells = new List<Vector3Int>();
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam))
                continue;
            if (!CanOfferAirTransporter(building))
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            pickupCells.Add(cell);
        }

        if (pickupCells.Count == 0)
            return 0;

        const float PickupRadius = 3f;
        int count = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.TeamId != snapshot.AITeam || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || !IsPrimaryRole(data, UnitRole.Capturador) || data.domain != Domain.Land)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            for (int i = 0; i < pickupCells.Count; i++)
            {
                if (SectorManager.HexDistance(unitCell, pickupCells[i]) <= PickupRadius)
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private static bool CanOfferAirTransporter(ConstructionManager building)
    {
        if (building == null || building.OfferedUnits == null)
            return false;
        foreach (UnitData unit in building.OfferedUnits)
        {
            if (unit == null || unit.domain != Domain.Air)
                continue;
            if (IsPrimaryRole(unit, UnitRole.Transportador))
                return true;
        }
        return false;
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

    private static int FindCheapestAirTransportCost(AIWorldSnapshot snapshot)
    {
        int cheapest = 0;
        if (snapshot == null || snapshot.MyBuildings == null) return cheapest;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.CanProduceUnitsForTeam(snapshot.AITeam) || b.OfferedUnits == null) continue;
            foreach (UnitData u in b.OfferedUnits)
            {
                if (u == null || u.domain != Domain.Air || !IsPrimaryRole(u, UnitRole.Transportador)) continue;
                if (cheapest == 0 || u.cost < cheapest) cheapest = u.cost;
            }
        }
        return cheapest;
    }

    private static int FindCheapestAirCombatCost(AIWorldSnapshot snapshot)
    {
        int cheapest = 0;
        if (snapshot == null || snapshot.MyBuildings == null) return cheapest;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.CanProduceUnitsForTeam(snapshot.AITeam) || b.OfferedUnits == null) continue;
            foreach (UnitData u in b.OfferedUnits)
            {
                if (u == null || u.domain != Domain.Air) continue;
                UnitRole r = u.roles != null && u.roles.Count > 0 ? u.roles[0] : UnitRole.None;
                if (r != UnitRole.Interceptador && r != UnitRole.AtaqueAereo) continue;
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
