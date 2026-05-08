using System.Collections.Generic;
using UnityEngine;

public class AIShoppingPlanner : MonoBehaviour
{
    private const int DefensiveBaseThreatRange = 3;
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

    [Header("Economia")]
    [Range(0f, 20f)] public float SavingPercentualForElite = 15f;
    [Range(0f, 1f)]  public float EliteCapturerFillRatio   = 0.6f;
    [Range(0, 5)]    public int   MinFilledAssaultSlots     = 1;

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

        var orders = new List<ShoppingOrder>();
        if (snapshot == null) return orders;

        var occupied  = new HashSet<Vector3Int>(snapshot.OccupiedCells);
        int remaining = snapshot.Budget;
        int openCapturerSlots   = CountOpenSlots(snapshot.AITeam, UnitRole.Capturador);
        int openAssaultSlots    = CountOpenSlots(snapshot.AITeam, UnitRole.Assalto);
        int openTransportSlots = ComputeTransportDemand(snapshot);
        if (openAssaultSlots <= 0
            && !HasActivePrimaryRole(snapshot, UnitRole.Assalto)
            && CanAffordPurePrimaryRole(snapshot, UnitRole.Assalto, remaining))
        {
            openAssaultSlots = 1;
        }
        if (openAssaultSlots > 0 && openCapturerSlots > 2)
            openCapturerSlots = 2;

        UnitData eliteAssaultTarget = FindEliteAssaultReserveTarget(snapshot);

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

        int cheapestTransportCost = openTransportSlots > 0 ? FindCheapestAvailableTransportCost(snapshot) : 0;

        Debug.Log($"[AI Shopping] budget={remaining} cap_slots={openCapturerSlots} ass_slots={openAssaultSlots} trans_slots={openTransportSlots} cheapest_transport={cheapestTransportCost} onlyCap={onlyCapturers} onlyAss={onlyAssault} onlyTrans={onlyTransporter}");

        bool wantsEliteAssault = eliteAssaultTarget != null && openAssaultSlots > 0;
        bool eliteAssaultBought = false;
        bool defensiveBaseTankBought = false;
        int defensiveBaseResponseReserveCost = FindCheapestDefensiveBaseThreatPurchaseCost(snapshot);
        int defensiveBaseBasicMassCost = FindCheapestDefensiveBaseBasicMassPurchaseCost(snapshot);
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

            bool manpowerShortage = HasDefensiveBaseManpowerShortage(snapshot);
            int tankReserve = Mathf.Max(0, defensiveBaseBasicMassCost) * 2;
            int tankA = manpowerShortage && HasVisibleEnemyNearBase(a, snapshot, DefensiveBaseThreatRange)
                && CanOfferAffordableDefensiveTank(a, remaining, tankReserve) ? 0 : 1;
            int tankB = manpowerShortage && HasVisibleEnemyNearBase(b, snapshot, DefensiveBaseThreatRange)
                && CanOfferAffordableDefensiveTank(b, remaining, tankReserve) ? 0 : 1;
            if (tankA != tankB) return tankA.CompareTo(tankB);

            int transA = openTransportSlots > 0 && cheapestTransportCost > 0 && remaining >= cheapestTransportCost
                && CanOfferPrimaryRoleUnit(a, UnitRole.Transportador) ? 0 : 1;
            int transB = openTransportSlots > 0 && cheapestTransportCost > 0 && remaining >= cheapestTransportCost
                && CanOfferPrimaryRoleUnit(b, UnitRole.Transportador) ? 0 : 1;
            if (transA != transB) return transA.CompareTo(transB);

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

            bool defensiveBaseThreat = HasVisibleEnemyNearBase(building, snapshot, DefensiveBaseThreatRange);
            bool allowDefensiveEliteAssault = defensiveBaseThreat
                && wantsEliteAssault
                && !eliteAssaultBought
                && CanOfferUnit(building, eliteAssaultTarget)
                && remaining >= eliteAssaultTarget.cost + CalculateEliteAssaultSafetyBuffer(eliteAssaultTarget);
            bool defensiveBaseManpowerShortage = defensiveBaseThreat && HasDefensiveBaseManpowerShortage(snapshot);
            int spendBudget = remaining;
            if (!defensiveBaseThreat && wantsEliteAssault && !eliteAssaultBought)
            {
                if (remaining >= eliteAssaultTarget.cost)
                    spendBudget = CanOfferUnit(building, eliteAssaultTarget) ? remaining : 0;
                else if (reserveForEliteAssault > 0)
                    spendBudget = Mathf.Max(0, remaining - reserveForEliteAssault);
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

            UnitData unit = PickUnit(building, snapshot, spendBudget, onlyCapturers, onlyAssault, onlyTransporter,
                openCapturerSlots, openAssaultSlots, openTransportSlots, eliteAssaultTarget, defensiveBaseThreat,
                allowDefensiveEliteAssault, defensiveBaseResponseReserveCost,
                defensiveBaseManpowerShortage, defensiveBaseBasicMassCost, defensiveBaseTankBought);
            if (unit == null)
            {
                Debug.Log($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} — nenhuma unidade selecionada (sem fit ou sem budget)");
                continue;
            }

            if (defensiveBaseThreat)
            {
                string heavyStatus = IsDefensiveBaseAssaultTankPurchase(unit)
                    ? $" tanque-ok reserva={defensiveBaseResponseReserveCost}"
                    : IsDefensiveBaseBasicMassPurchase(unit) ? " numeros-ok"
                    : allowDefensiveEliteAssault ? " elite-ok" : string.Empty;
                Debug.Log($"[AI Shopping] defesa base {building.ConstructionDisplayName} inimigo<=3h{heavyStatus} -> {unit.displayName}");
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
            if (unit == eliteAssaultTarget)
                eliteAssaultBought = true;
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
        int openCapturerSlots,
        int openAssaultSlots,
        int openTransportSlots = 0,
        UnitData eliteAssaultTarget = null,
        bool defensiveBaseThreat = false,
        bool allowDefensiveEliteAssault = false,
        int defensiveBaseResponseReserveCost = 0,
        bool defensiveBaseManpowerShortage = false,
        int defensiveBaseBasicMassCost = 0,
        bool defensiveBaseTankBought = false)
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

            bool isPrimaryCapturer   = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Capturador;
            bool isPrimaryAssault    = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Assalto;
            bool isPrimaryTransporter = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Transportador;
            bool isHybridCapturer    = isPrimaryAssault && u.roles.Contains(UnitRole.Capturador);
            bool isSecondary       = !isPrimaryCapturer && u.roles != null && u.roles.Contains(UnitRole.Capturador);

            bool isAllowedDefensiveElite = allowDefensiveEliteAssault && u == eliteAssaultTarget;
            bool canAffordDefensiveTank = IsDefensiveBaseAssaultTankPurchase(u)
                && budget >= u.cost + Mathf.Max(0, defensiveBaseResponseReserveCost);
            bool canBuyBasicMass = defensiveBaseManpowerShortage
                && defensiveBaseTankBought
                && IsDefensiveBaseBasicMassPurchase(u);
            if (defensiveBaseThreat
                && !IsDefensiveBaseThreatPurchase(u)
                && !isAllowedDefensiveElite
                && !canAffordDefensiveTank
                && !canBuyBasicMass) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — defThreat filter (notThreat={!IsDefensiveBaseThreatPurchase(u)} notElite={!isAllowedDefensiveElite} notTank={!canAffordDefensiveTank} notMass={!canBuyBasicMass})"); continue; }
            if (!defensiveBaseThreat && isHybridCapturer && !hasOpenDefensiveSlot) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — hybrid sem slot defensivo"); continue; }
            if ((onlyCapturers || onlyAssault || onlyTransporter)
                && !((onlyCapturers && isPrimaryCapturer) || (onlyAssault && isPrimaryAssault) || (onlyTransporter && isPrimaryTransporter))) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — onlyFilter (cap={isPrimaryCapturer} ass={isPrimaryAssault} trans={isPrimaryTransporter})"); continue; }

            int score = u.cost;
            if (defensiveBaseThreat && defensiveBaseManpowerShortage)
            {
                int basicReserve = Mathf.Max(0, defensiveBaseBasicMassCost) * 2;
                if (IsDefensiveBaseAssaultTankPurchase(u) && budget >= u.cost + basicReserve)
                    score += 180000;
                else if (IsDefensiveBaseBasicMassPurchase(u))
                    score += 70000;
            }
            if (openTransportSlots > 0 && isPrimaryTransporter)
                score += 101000;
            if (openCapturerSlots > 0)
            {
                if (isPrimaryCapturer)              score += 100000;
                else if (isSecondary && defensiveStance) score +=  10000;
                else if (openAssaultSlots <= 0)     score -= 100000;
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

            string roleStr = isPrimaryTransporter ? "TRANS" : isPrimaryCapturer ? "CAP" : isPrimaryAssault ? $"ASS(hybrid={isHybridCapturer})" : "other";
            Debug.Log($"[AI PickUnit] {u.displayName} ${u.cost} role={roleStr} score={score} mov={u.movement} | trans={openTransportSlots} cap={openCapturerSlots} ass={openAssaultSlots} defThreat={defensiveBaseThreat}");
            if (score > bestScore) { bestScore = score; best = u; }
        }

        return best;
    }

    private static UnitData FindEliteAssaultReserveTarget(AIWorldSnapshot snapshot)
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
                if (unit.eliteLevel < 1) continue;

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

    private static bool CanOfferUnit(ConstructionManager building, UnitData target)
    {
        if (building == null || target == null || building.OfferedUnits == null) return false;
        for (int i = 0; i < building.OfferedUnits.Count; i++)
            if (building.OfferedUnits[i] == target) return true;
        return false;
    }

    private static bool CanOfferAffordableDefensiveTank(ConstructionManager building, int budget, int reserve)
    {
        if (building == null || building.OfferedUnits == null) return false;

        int safeReserve = Mathf.Max(0, reserve);
        foreach (UnitData unit in building.OfferedUnits)
        {
            if (!IsDefensiveBaseAssaultTankPurchase(unit)) continue;
            if (budget >= unit.cost + safeReserve) return true;
        }

        return false;
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

    // Returns how many APCs to buy this round based on actual capturer-to-objective distances.
    // Only capturers who are currently far (>= MinDistanceForTransportSlot) from their target generate demand.
    // Subtracts free APCs already in the field; caps at 1 per shopping round.
    private static int ComputeTransportDemand(AIWorldSnapshot snapshot)
    {
        TeamId aiTeam = snapshot.AITeam;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return 0;

        int minDist = AIController.Instance != null
            ? AIController.Instance.MinDistanceForTransportSlot : 7;

        // Count free (no-cargo) transporters already in the field
        int freeAPCs = 0;
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.TeamId != aiTeam || u.IsDead || u.IsEmbarked) continue;
            if (!u.TryGetUnitData(out UnitData d)) continue;
            if (d.roles == null || d.roles.Count == 0 || d.roles[0] != UnitRole.Transportador) continue;
            bool hasCargo = false;
            if (u.TransportedUnitSlots != null)
                foreach (UnitTransportSeatRuntime seat in u.TransportedUnitSlots)
                    if (seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked) { hasCargo = true; break; }
            if (!hasCargo) freeAPCs++;
        }

        // Count capturers assigned to transport-needing objectives who are far from their target
        int needed = 0;
        foreach (SectorObjective obj in plan.Objectives)
        {
            bool hasTransportSlot = false;
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Transportador) { hasTransportSlot = true; break; }
            if (!hasTransportSlot) continue;

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

            ConstructionManager tgt = AIController.FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt == null) continue;

            Vector3Int capCell = capturer.CurrentCellPosition; capCell.z = 0;
            Vector3Int tgtCell = tgt.CurrentCellPosition; tgtCell.z = 0;
            float dist = SectorManager.HexDistance(capCell, tgtCell);

            if (dist >= minDist) needed++;
        }

        int deficit = Mathf.Max(0, needed - freeAPCs);
        int demand  = Mathf.Min(deficit, 1); // cap: max 1 APC per shopping round
        Debug.Log($"[AI Shopping] transport_demand: needed={needed} freeAPCs={freeAPCs} deficit={deficit} demand={demand} minDist={minDist}");
        return demand;
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
}
