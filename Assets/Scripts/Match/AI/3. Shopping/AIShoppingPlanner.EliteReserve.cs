using System.Collections.Generic;
using UnityEngine;

// Lógica de reserva e seleção de unidades elite (assault e fire support).
public partial class AIShoppingPlanner
{
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

    private static UnitData FindEliteFireSupportReserveTarget(
        AIWorldSnapshot snapshot,
        bool preferDefensiveFireSupport,
        int budget = int.MaxValue,
        bool requireChain = true,
        bool antiAirOnly = false)
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
                if (IsPrimaryRole(unit, UnitRole.Assalto)) continue;
                if (IsAntiAirOnlyUnit(unit) != antiAirOnly) continue;
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

    private static UnitData FindAffordableDefensiveBaseAssaultTankTarget(AIWorldSnapshot snapshot, int budget)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return null;

        UnitData best = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.cost > budget) continue;
                if (!IsDefensiveBaseAssaultTankPurchase(unit)) continue;

                if (best == null
                    || unit.eliteLevel > best.eliteLevel
                    || (unit.eliteLevel == best.eliteLevel && unit.cost > best.cost))
                    best = unit;
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
}
