using System.Reflection;
// Executa UMA jogada pelo MESMO caminho da IA e do humano: batch -> ReplayManager.ExecuteLiveAIBatch.
// O automata emula a entrada: o cursor anda, o menu abre, a escolha e confirmada (aparece na tela).
// NUNCA chame funcoes por dentro (TryAIDirectPurchase etc.): a jogada vale, mas o menu e o
// automata nao sao testados. Espere ~6 s e confira com conferir.cs antes da proxima.
int meuSlot = 1;
string tipo = "capturar";   // "mover" | "capturar" | "comprar" | "passar"
int unidadeId = 4;          // mover / capturar
int x = 10, y = 5;          // destino (mover/capturar) ou predio (comprar)
string sigla = "SD";        // comprar

var mc = UnityEngine.Object.FindAnyObjectByType<MatchController>();
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
var rm = UnityEngine.Object.FindAnyObjectByType<ReplayManager>();
if (mc.ActiveSlotId.Value != meuSlot) return "nao e a vez do slot " + meuSlot;
if (rm.IsStepExecutionBusy) return "automata ocupado";
var flags = BindingFlags.NonPublic | BindingFlags.Instance;
var time = mc.GetTeamIdForSlot(meuSlot);
PlayerAction action = null;

if (tipo == "mover" || tipo == "capturar")
{
    UnitManager u = null;
    foreach (var o in UnitManager.AllActive) if (o != null && o.InstanceId == unidadeId) u = o;
    if (u == null) return "unidade nao achada";
    var from = u.CurrentCellPosition; from.z = 0;
    var to = new UnityEngine.Vector3Int(x, y, 0);
    var paths = UnitMovementPathRules.CalcularCaminhosValidos(u.BoardTilemap, u, u.RemainingMovementPoints, mc.TerrainDatabaseRef);
    action = (PlayerAction)typeof(AIController).GetMethod(tipo == "capturar" ? "BuildCaptureBatch" : "BuildMoveBatch", flags)
        .Invoke(ai, new object[] { u, time, from, to, paths });
}
else if (tipo == "comprar")
{
    ConstructionManager predio = null;
    foreach (var k in UnityEngine.Object.FindObjectsByType<ConstructionManager>())
    { var c = k.CurrentCellPosition; if (c.x == x && c.y == y) predio = k; }
    if (predio == null) return "predio nao achado";
    var oferta = predio.GetSiteRuntimeSnapshot().offeredUnits;
    int indice = -1;
    for (int i = 0; i < oferta.Count; i++) if (oferta[i] != null && oferta[i].apelido == sigla) indice = i;
    if (indice < 0) return "predio nao vende " + sigla;
    var pedido = new AIShoppingPlanner.ShoppingOrder { Building = predio, UnitToBuy = oferta[indice], SelectedIndex = indice };
    action = (PlayerAction)typeof(AIController).GetMethod("BuildShoppingBatch", flags).Invoke(ai, new object[] { time, pedido });
}
else if (tipo == "passar")
{
    action = (PlayerAction)typeof(AIController).GetMethod("BuildEndTurnBatch", flags).Invoke(ai, new object[] { time });
}

if (action == null) return "batch nulo";
action.IsAIGenerated = false;   // jogada do jogador humano do slot
rm.ExecuteLiveAIBatch(action, false);
return "enviado: " + action.DebugLabel;
