// O que a IA compraria no lugar de um slot, e o que cada produtor dele vende. NAO executa.
// Rode ia_plano.cs antes: a IA monta o plano antes de comprar, e o shopping le as vagas dele.
int meuSlot = 1;

var mc = UnityEngine.Object.FindAnyObjectByType<MatchController>();
var meu = PlayerSlotId.FromIndex(meuSlot);
var sb = new System.Text.StringBuilder();
sb.AppendLine("dinheiro=" + mc.GetActualMoney(meu));
var orders = AIShoppingPlanner.Decide(AIWorldSnapshot.Build(meu, mc));
sb.AppendLine("IA compraria:");
if (orders != null)
    foreach (var o in orders)
    {
        var bc = o.Building != null ? o.Building.CurrentCellPosition : default;
        sb.AppendLine("  " + (o.UnitToBuy != null ? o.UnitToBuy.apelido + " $" + o.UnitToBuy.cost : "?") + " em @" + bc.x + "," + bc.y);
    }
sb.AppendLine("Produtores:");
foreach (var k in UnityEngine.Object.FindObjectsByType<ConstructionManager>())
{
    if (k.SlotIndex != meuSlot || !k.CanProduceUnitsForSlot(meuSlot)) continue;
    var lista = new System.Collections.Generic.List<string>();
    foreach (var u in k.GetSiteRuntimeSnapshot().offeredUnits) if (u != null) lista.Add(u.apelido + "$" + u.cost);
    var c = k.CurrentCellPosition;
    sb.AppendLine("  @" + c.x + "," + c.y + ": " + string.Join(", ", lista));
}
return sb.ToString();
