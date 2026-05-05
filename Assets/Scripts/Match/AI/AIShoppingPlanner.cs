using System.Collections.Generic;
using UnityEngine;

public class AIShoppingPlanner : MonoBehaviour
{
    private const int DefensiveBaseThreatRange = 3;

    public struct ShoppingOrder
    {
        public ConstructionManager Building;
        public UnitData UnitToBuy;
        public int SelectedIndex;
    }

    [Header("Debug")]
    public bool onlyCapturers;
    public bool onlyAssault;

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
        bool onlyCapturers = Instance != null && Instance.onlyCapturers;
        bool onlyAssault   = Instance != null && Instance.onlyAssault;

        var orders = new List<ShoppingOrder>();
        if (snapshot == null) return orders;

        var occupied  = new HashSet<Vector3Int>(snapshot.OccupiedCells);
        int remaining = snapshot.Budget;
        int openCapturerSlots = CountOpenSlots(snapshot.AITeam, UnitRole.Capturador);
        int openAssaultSlots  = CountOpenSlots(snapshot.AITeam, UnitRole.Assalto);
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

        bool wantsEliteAssault = eliteAssaultTarget != null && openAssaultSlots > 0;
        bool eliteAssaultBought = false;
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

            return GetMinDistanceToOpenObjective(a, plan, snapshot.AITeam)
                .CompareTo(GetMinDistanceToOpenObjective(b, plan, snapshot.AITeam));
        });

        foreach (ConstructionManager building in sortedBuildings)
        {
            if (!building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;

            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            if (occupied.Contains(cell)) continue;

            bool defensiveBaseThreat = HasVisibleEnemyNearBase(building, snapshot, DefensiveBaseThreatRange);
            int spendBudget = remaining;
            if (!defensiveBaseThreat && wantsEliteAssault && !eliteAssaultBought)
            {
                if (remaining >= eliteAssaultTarget.cost)
                    spendBudget = CanOfferUnit(building, eliteAssaultTarget) ? remaining : 0;
                else if (reserveForEliteAssault > 0)
                    spendBudget = Mathf.Max(0, remaining - reserveForEliteAssault);
            }

            UnitData unit = PickUnit(building, snapshot, spendBudget, onlyCapturers, onlyAssault,
                openCapturerSlots, openAssaultSlots, eliteAssaultTarget, defensiveBaseThreat);
            if (unit == null) continue;

            if (defensiveBaseThreat)
            {
                Debug.Log($"[AI Shopping] defesa base {building.ConstructionDisplayName} inimigo<=3h -> {unit.displayName}");
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
            if (unit == eliteAssaultTarget)
                eliteAssaultBought = true;

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
        int openCapturerSlots,
        int openAssaultSlots,
        UnitData eliteAssaultTarget = null,
        bool defensiveBaseThreat = false)
    {
        if (building.OfferedUnits == null || building.OfferedUnits.Count == 0) return null;

        bool defensiveStance  = snapshot.Stance == AIStance.Defensive;
        bool hasOpenDefensiveSlot = HasOpenDefensiveSlot(snapshot.AITeam);

        UnitData best      = null;
        int      bestScore = int.MinValue;

        foreach (UnitData u in building.OfferedUnits)
        {
            if (u == null || u.cost > budget) continue;
            if (u.domain != Domain.Land) continue;

            bool isPrimaryCapturer = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Capturador;
            bool isPrimaryAssault  = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Assalto;
            bool isHybridCapturer  = isPrimaryAssault && u.roles.Contains(UnitRole.Capturador);
            bool isSecondary       = !isPrimaryCapturer && u.roles != null && u.roles.Contains(UnitRole.Capturador);

            if (defensiveBaseThreat && !IsDefensiveBaseThreatPurchase(u)) continue;
            if (!defensiveBaseThreat && isHybridCapturer && !hasOpenDefensiveSlot) continue;
            if ((onlyCapturers || onlyAssault)
                && !((onlyCapturers && isPrimaryCapturer) || (onlyAssault && isPrimaryAssault))) continue;

            int score = u.cost;
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
                else if (openCapturerSlots <= 0) score -= 90000;
            }

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

    private static int IndexOf(IReadOnlyList<UnitData> list, UnitData target)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == target) return i;
        return 0;
    }
}
