using System.Collections.Generic;
using UnityEngine;

public class AIShoppingPlanner : MonoBehaviour
{
    public struct ShoppingOrder
    {
        public ConstructionManager Building;
        public UnitData UnitToBuy;
        public int SelectedIndex;
    }

    [Header("Debug")]
    public bool onlyCapturers;

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

        var orders = new List<ShoppingOrder>();
        if (snapshot == null) return orders;

        var occupied  = new HashSet<Vector3Int>(snapshot.OccupiedCells);
        int remaining = snapshot.Budget;

        // Ordena fábricas: as mais próximas de slots abertos compram primeiro
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        var sortedBuildings = new List<ConstructionManager>(snapshot.MyBuildings);
        sortedBuildings.Sort((a, b) =>
            GetMinDistanceToOpenObjective(a, plan, snapshot.AITeam)
            .CompareTo(GetMinDistanceToOpenObjective(b, plan, snapshot.AITeam)));

        foreach (ConstructionManager building in sortedBuildings)
        {
            if (!building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;

            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            if (occupied.Contains(cell)) continue;

            UnitData unit = PickUnit(building, snapshot, remaining, onlyCapturers);
            if (unit == null) continue;

            int idx = IndexOf(building.OfferedUnits, unit);
            orders.Add(new ShoppingOrder
            {
                Building      = building,
                UnitToBuy     = unit,
                SelectedIndex = idx,
            });

            remaining -= unit.cost;
            occupied.Add(cell);

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
            if (!obj.HasOpenSlot(UnitRole.Capturador)) continue;
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

    private static UnitData PickUnit(ConstructionManager building, AIWorldSnapshot snapshot, int budget, bool onlyCapturers)
    {
        if (building.OfferedUnits == null || building.OfferedUnits.Count == 0) return null;

        int  nearbyOpenSlots  = CountNearbyOpenCapturerSlots(snapshot.AITeam);
        bool defensiveStance  = snapshot.Stance == AIStance.Defensive;

        UnitData best      = null;
        int      bestScore = int.MinValue;

        foreach (UnitData u in building.OfferedUnits)
        {
            if (u == null || u.cost > budget) continue;
            if (u.domain != Domain.Land) continue;

            bool isPrimary   = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Capturador;
            bool isSecondary = !isPrimary && u.roles != null && u.roles.Contains(UnitRole.Capturador);

            if (onlyCapturers && !isPrimary) continue;

            int score = u.cost;
            if (nearbyOpenSlots > 0)
            {
                if (isPrimary)                      score += 100000;
                else if (isSecondary && defensiveStance) score +=  10000;
                else                                score -= 100000;
            }

            if (score > bestScore) { bestScore = score; best = u; }
        }

        return best;
    }

    private static int CountNearbyOpenCapturerSlots(TeamId aiTeam)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return 0;

        int open = 0;
        foreach (SectorObjective obj in plan.Objectives)
        {
            SectorManager.SectorRiskLevel risk = SectorManager.SectorRiskLevel.Medium;
            if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info))
                risk = info.GetRiskLevelFor(aiTeam);

            if (risk != SectorManager.SectorRiskLevel.Safe &&
                risk != SectorManager.SectorRiskLevel.Low) continue;

            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Capturador && !slot.Filled) open++;
        }
        return open;
    }

    private static int IndexOf(IReadOnlyList<UnitData> list, UnitData target)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == target) return i;
        return 0;
    }
}
