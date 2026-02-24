using System.Text;
using System.Collections.Generic;
using UnityEngine;

public partial class TurnStateManager
{
    private bool TryEnterConstructionShoppingState(ConstructionManager construction, int activeTeam)
    {
        if (construction == null || activeTeam < 0)
            return false;

        TeamId buyerTeam = (TeamId)activeTeam;
        if (!construction.CanProduceUnitsForTeam(buyerTeam))
            return false;

        IReadOnlyList<UnitData> offered = construction.OfferedUnits;
        if (offered == null || offered.Count == 0)
            return false;

        shoppingUnitsForSale.Clear();
        for (int i = 0; i < offered.Count; i++)
        {
            UnitData unit = offered[i];
            if (unit == null)
                continue;

            shoppingUnitsForSale.Add(unit);
        }

        if (shoppingUnitsForSale.Count == 0)
            return false;

        shoppingConstruction = construction;
        SetCursorState(CursorState.ShoppingAndServices, "TryEnterConstructionShoppingState: ally construction with units for sale");
        LogConstructionShoppingPanel();
        return true;
    }

    private void ExitConstructionShoppingStateToNeutral(bool rollback)
    {
        shoppingConstruction = null;
        shoppingUnitsForSale.Clear();
        SetCursorState(CursorState.Neutral, "ExitConstructionShoppingStateToNeutral", rollback: rollback);
    }

    private void ProcessConstructionShoppingInput()
    {
        if (cursorState != CursorState.ShoppingAndServices)
            return;

        if (shoppingConstruction == null || shoppingUnitsForSale.Count == 0)
        {
            ExitConstructionShoppingStateToNeutral(rollback: true);
            return;
        }

        if (!TryReadPressedNumber(out int number))
            return;

        int index = number - 1;
        if (index < 0 || index >= shoppingUnitsForSale.Count)
        {
            Debug.Log($"[Shopping] Opcao invalida: {number}. Escolha entre 1 e {shoppingUnitsForSale.Count}.");
            return;
        }

        UnitData unit = shoppingUnitsForSale[index];
        if (unit == null)
        {
            Debug.LogWarning("[Shopping] Unidade selecionada esta nula.");
            return;
        }

        if (unitSpawner == null)
        {
            Debug.LogWarning("[Shopping] UnitSpawner nao encontrado na cena.");
            return;
        }

        int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;
        TeamId spawnTeam = activeTeam >= 0 ? (TeamId)activeTeam : shoppingConstruction.TeamId;
        Vector3Int spawnCell = shoppingConstruction.CurrentCellPosition;
        spawnCell.z = 0;

        GameObject spawned = unitSpawner.SpawnAtCell(unit, spawnTeam, spawnCell);
        if (spawned == null)
        {
            Debug.LogWarning($"[Shopping] Falha ao comprar {ResolveUnitName(unit)}. Verifique ocupacao/camada da celula.");
            return;
        }

        cursorController?.PlayDoneSfx();
        Debug.Log($"[Shopping] Compra concluida: {ResolveUnitName(unit)} por ${Mathf.Max(0, unit.cost)} em {ResolveConstructionName(shoppingConstruction)}.");
        ExitConstructionShoppingStateToNeutral(rollback: false);
    }

    private void LogConstructionShoppingPanel()
    {
        if (cursorState != CursorState.ShoppingAndServices || shoppingConstruction == null)
            return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[Shopping] {ResolveConstructionName(shoppingConstruction)} - escolha a opcao de compra:");

        for (int i = 0; i < shoppingUnitsForSale.Count; i++)
        {
            UnitData unit = shoppingUnitsForSale[i];
            if (unit == null)
                continue;

            sb.Append(i + 1);
            sb.Append(". ");
            sb.Append(ResolveUnitName(unit));
            sb.Append(" $");
            sb.Append(Mathf.Max(0, unit.cost));
            sb.AppendLine();
        }

        sb.Append("Pressione 1-9 para comprar, ESC para cancelar.");
        Debug.Log(sb.ToString());
    }

    private static string ResolveUnitName(UnitData unit)
    {
        if (unit == null)
            return "<null>";
        if (!string.IsNullOrWhiteSpace(unit.displayName))
            return unit.displayName;
        if (!string.IsNullOrWhiteSpace(unit.id))
            return unit.id;
        return unit.name;
    }

}
