using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Alerta individual da pilha de estoque de supridor (supply_top/middle/bottom
// nos prefabs de unidade e de construcao).
public struct SupplierStockAlert
{
    public float ratio;
    public bool empty;
    public Sprite sprite;
}

// Visual e empilhamento compartilhados da pilha de alerta de estoque, para
// unidade (UnitManager) e construcao (ConstructionManager) nunca divergirem.
// A pilha enche de baixo pra cima: o item mais critico (vazio primeiro, depois
// menor razao) ocupa o bottom, perto das barras. A REGRA de coleta e de cada
// lado: unidade alerta por razao contra a capacidade (half/empty); construcao
// nao tem teto de recursos, entao so alerta o "acabou" (empty).
public static class SupplierStockAlertView
{
    public static void ConfigureSlot(Image slot)
    {
        if (slot == null) return;
        slot.raycastTarget = false;
        slot.preserveAspect = true;
    }

    public static Sprite ResolveAlertSprite(SupplyData supply, bool empty)
    {
        if (supply == null)
            return null;
        Sprite chosen = empty ? supply.spriteEmpty : supply.spriteHalf;
        return chosen != null ? chosen : supply.spriteDefault;
    }

    public static void SortMostCriticalFirst(List<SupplierStockAlert> alerts)
    {
        alerts?.Sort((a, b) =>
        {
            int byEmpty = b.empty.CompareTo(a.empty);
            return byEmpty != 0 ? byEmpty : a.ratio.CompareTo(b.ratio);
        });
    }

    public static void ApplyStack(Image bottom, Image middle, Image top, List<SupplierStockAlert> alerts)
    {
        ApplySlot(bottom, alerts, 0);
        ApplySlot(middle, alerts, 1);
        ApplySlot(top, alerts, 2);
    }

    public static void HideStack(Image bottom, Image middle, Image top)
    {
        HideSlot(bottom);
        HideSlot(middle);
        HideSlot(top);
    }

    public static void ApplySlot(Image slot, List<SupplierStockAlert> alerts, int index)
    {
        if (slot == null) return;
        bool visible = alerts != null && index >= 0 && index < alerts.Count && alerts[index].sprite != null;
        slot.sprite = visible ? alerts[index].sprite : null;
        slot.enabled = visible;
        if (slot.gameObject.activeSelf != visible)
            slot.gameObject.SetActive(visible);
    }

    public static void HideSlot(Image slot)
    {
        if (slot == null) return;
        slot.sprite = null;
        slot.enabled = false;
        if (slot.gameObject.activeSelf)
            slot.gameObject.SetActive(false);
    }
}
