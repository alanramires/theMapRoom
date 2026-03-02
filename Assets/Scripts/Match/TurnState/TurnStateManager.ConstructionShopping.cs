using System.Text;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        if (!TryReadShoppingPressedNumber(out int number))
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
        int unitCost = matchController != null
            ? matchController.ResolveEconomyCost(unit.cost)
            : Mathf.Max(0, unit.cost);
        if (matchController != null)
        {
            int currentMoney = matchController.GetActualMoney(spawnTeam);
            if (currentMoney < unitCost)
            {
                cursorController?.PlayErrorSfx();
                Debug.LogError($"[Shopping] Dinheiro insuficiente para comprar {ResolveUnitName(unit)}. Custo=${unitCost}, saldo=${currentMoney}.");
                return;
            }
        }

        Vector3Int spawnCell = shoppingConstruction.CurrentCellPosition;
        spawnCell.z = 0;

        GameObject spawned = unitSpawner.SpawnAtCell(unit, spawnTeam, spawnCell);
        if (spawned == null)
        {
            Debug.LogWarning($"[Shopping] Falha ao comprar {ResolveUnitName(unit)}. Verifique ocupacao/camada da celula.");
            return;
        }

        if (matchController != null && !matchController.TrySpendActualMoney(spawnTeam, unitCost, out int remainingMoney))
        {
            // Protecao contra corrida/estado inesperado: se falhou no debito, desfaz spawn.
            Destroy(spawned);
            cursorController?.PlayErrorSfx();
            Debug.LogError($"[Shopping] Falha ao debitar custo da unidade {ResolveUnitName(unit)}. Saldo atual=${remainingMoney}, custo=${unitCost}.");
            return;
        }

        cursorController?.PlayDoneSfx();
        Debug.Log($"[Shopping] Compra concluida: {ResolveUnitName(unit)} por ${unitCost} em {ResolveConstructionName(shoppingConstruction)}.");
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

        sb.Append("Atalhos: 1-9, 0=10, Shift+1=11, Shift+2=12 ... Shift+9=19. ESC cancela.");
        Debug.Log(sb.ToString());
    }

    private static bool TryReadShoppingPressedNumber(out int number)
    {
        number = 0;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            bool shift = (Keyboard.current.leftShiftKey != null && Keyboard.current.leftShiftKey.isPressed) ||
                         (Keyboard.current.rightShiftKey != null && Keyboard.current.rightShiftKey.isPressed);

            if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame) { number = 10; return true; }
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) { number = shift ? 11 : 1; return true; }
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) { number = shift ? 12 : 2; return true; }
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) { number = shift ? 13 : 3; return true; }
            if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) { number = shift ? 14 : 4; return true; }
            if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) { number = shift ? 15 : 5; return true; }
            if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) { number = shift ? 16 : 6; return true; }
            if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame) { number = shift ? 17 : 7; return true; }
            if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame) { number = shift ? 18 : 8; return true; }
            if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame) { number = shift ? 19 : 9; return true; }
        }
#else
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) { number = 10; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { number = shift ? 11 : 1; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { number = shift ? 12 : 2; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { number = shift ? 13 : 3; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { number = shift ? 14 : 4; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { number = shift ? 15 : 5; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) { number = shift ? 16 : 6; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) { number = shift ? 17 : 7; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) { number = shift ? 18 : 8; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) { number = shift ? 19 : 9; return true; }
#endif

        return false;
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
