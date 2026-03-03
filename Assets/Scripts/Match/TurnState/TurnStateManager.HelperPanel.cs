using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    public bool TryBuildHelperPanel(out string title, out string body)
    {
        title = string.Empty;
        body = string.Empty;

        if (cursorState == CursorState.Neutral)
            return false;

        if (cursorState == CursorState.ShoppingAndServices)
            return TryBuildShoppingHelperPanel(out title, out body);

        if (cursorState == CursorState.Desembarcando)
            return TryBuildDisembarkHelperPanel(out title, out body);

        if (cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado)
            return TryBuildSensorsHelperPanel(out title, out body);

        return false;
    }

    private bool TryBuildShoppingHelperPanel(out string title, out string body)
    {
        title = "SHOPPING";
        body = string.Empty;
        if (shoppingUnitsForSale == null || shoppingUnitsForSale.Count <= 0)
            return false;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < shoppingUnitsForSale.Count; i++)
        {
            UnitData unit = shoppingUnitsForSale[i];
            if (unit == null)
                continue;

            sb.Append(i + 1);
            sb.Append(" - ");
            sb.Append(ResolveUnitName(unit));
            if (matchController != null)
            {
                int cost = matchController.ResolveEconomyCost(unit.cost);
                sb.Append(" ($");
                sb.Append(cost);
                sb.Append(')');
            }

            if (i < shoppingUnitsForSale.Count - 1)
                sb.AppendLine();
        }

        body = sb.ToString();
        return !string.IsNullOrWhiteSpace(body);
    }

    private bool TryBuildSensorsHelperPanel(out string title, out string body)
    {
        title = "SENSORS";
        body = string.Empty;
        if (scannerPromptStep != ScannerPromptStep.AwaitingAction)
            return false;

        StringBuilder sb = new StringBuilder();
        AppendSensorLine(sb, 'A', "Aim");
        AppendSensorLine(sb, 'E', "Embark");
        AppendSensorLine(sb, 'D', "Disembark");
        AppendSensorLine(sb, 'C', "Capture");
        AppendSensorLine(sb, 'F', "Fuse units");
        AppendSensorLine(sb, 'S', "Supply");
        AppendSensorLine(sb, 'T', "Transfer");
        AppendSensorLine(sb, 'L', "Layer");

        if (sb.Length > 0)
            sb.AppendLine();
        sb.Append("M - Move Only");

        body = sb.ToString();
        return !string.IsNullOrWhiteSpace(body);
    }

    private void AppendSensorLine(StringBuilder sb, char actionCode, string label)
    {
        if (availableSensorActionCodes == null || !availableSensorActionCodes.Contains(actionCode))
            return;

        if (sb.Length > 0)
            sb.AppendLine();
        sb.Append(actionCode);
        sb.Append(" - ");
        sb.Append(label);
    }

    private bool TryBuildDisembarkHelperPanel(out string title, out string body)
    {
        title = "DISEMBARK";
        body = string.Empty;
        if (disembarkPassengerEntries == null || disembarkPassengerEntries.Count <= 0)
            return false;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Order");
        if (disembarkQueuedOrders != null && disembarkQueuedOrders.Count > 0)
        {
            for (int i = 0; i < disembarkQueuedOrders.Count; i++)
            {
                DisembarkOrder order = disembarkQueuedOrders[i];
                if (order == null || order.passenger == null)
                    continue;

                string passengerName = ResolveUnitRuntimeName(order.passenger);
                string stats = BuildUnitStatInline(order.passenger);
                string terrainName = ResolveTerrainLabelForCell(order.targetCell);
                sb.Append(i);
                sb.Append(" - ");
                sb.Append(passengerName);
                sb.Append(" (");
                sb.Append(stats);
                sb.Append(") -> ");
                sb.Append(terrainName);
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("0 - (empty)");
        }

        sb.AppendLine();
        sb.AppendLine("Select Passenger");
        for (int i = 0; i < disembarkPassengerEntries.Count; i++)
        {
            DisembarkPassengerEntry entry = disembarkPassengerEntries[i];
            if (entry == null || entry.passenger == null)
                continue;

            string passengerName = ResolveUnitRuntimeName(entry.passenger);
            string stats = BuildUnitStatInline(entry.passenger);
            sb.Append(entry.selectionNumber);
            sb.Append(" - ");
            sb.Append(passengerName);
            sb.Append(" (");
            sb.Append(stats);
            sb.AppendLine(")");
        }

        if (disembarkQueuedOrders != null && disembarkQueuedOrders.Count > 0)
            sb.Append("0 - Process Order");

        body = sb.ToString().TrimEnd();
        return !string.IsNullOrWhiteSpace(body);
    }

    private string BuildUnitStatInline(UnitManager unit)
    {
        if (unit == null)
            return "-";

        int hp = Mathf.Max(0, unit.CurrentHP);
        int fuel = Mathf.Max(0, unit.CurrentFuel);
        int ammo = Mathf.Max(0, unit.CurrentAmmo);
        int maxAmmo = Mathf.Max(0, unit.GetMaxAmmo());
        return $"{hp}HP | {fuel}F | {ammo}/{maxAmmo}A";
    }

    private string ResolveTerrainLabelForCell(Vector3Int cell)
    {
        Tilemap map = terrainTilemap;
        if (map == null && selectedUnit != null)
            map = selectedUnit.BoardTilemap;

        if (TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) && terrain != null)
            return ResolveTerrainName(terrain);

        return $"({cell.x},{cell.y})";
    }
}
