using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decide quais unidades comprar e em quais construções.
/// V1: compra sempre a melhor unidade terrestre acessível no saldo atual.
/// A poupança natural acontece porque o saldo acumula entre turnos no MatchController.
/// </summary>
public static class AIShoppingPlanner
{
    public struct ShoppingOrder
    {
        public ConstructionManager Building;
        public UnitData UnitToBuy;
        public int SelectedIndex;
    }

    public static List<ShoppingOrder> Decide(AIWorldSnapshot snapshot)
    {
        var orders = new List<ShoppingOrder>();
        if (snapshot == null) return orders;

        // Monta lista de compras em construções aliadas desocupadas
        var occupied = new HashSet<Vector3Int>(snapshot.OccupiedCells);
        int remaining = snapshot.Budget;

        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (!building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;

            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            if (occupied.Contains(cell)) continue;

            UnitData unit = PickUnit(building, snapshot, remaining);
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

    // Escolhe a melhor unidade acessível para comprar nesta construção
    private static UnitData PickUnit(ConstructionManager building, AIWorldSnapshot snapshot, int budget)
    {
        if (building.OfferedUnits == null || building.OfferedUnits.Count == 0) return null;

        int myCapturors = 0;
        foreach (UnitManager u in snapshot.MyUnits)
        {
            if (!u.TryGetUnitData(out UnitData ud) || ud == null) continue;
            if (ud.aiSensorPriority != null && ud.aiSensorPriority.Contains(AISensorPriority.Capture))
                myCapturors++;
        }
        bool needCapturor = myCapturors < 2;

        UnitData best = null;
        int bestScore = int.MinValue;

        foreach (UnitData u in building.OfferedUnits)
        {
            if (u == null || u.cost > budget) continue;
            if (u.domain != Domain.Land) continue; // V1: apenas unidades terrestres

            int score = u.cost; // preferir unidade mais cara (mais poderosa)
            bool isCapturer = u.aiSensorPriority != null && u.aiSensorPriority.Contains(AISensorPriority.Capture);
            if (needCapturor && isCapturer) score += 10000;

            if (score > bestScore) { bestScore = score; best = u; }
        }

        return best;
    }

    private static int IndexOf(IReadOnlyList<UnitData> list, UnitData target)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == target) return i;
        return 0;
    }
}
