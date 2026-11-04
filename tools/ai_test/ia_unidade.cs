using System.Reflection;
// O que a IA faria com UMA unidade, no estado atual. NAO executa.
// A IA e SEQUENCIAL: pergunte uma unidade, execute a jogada, so entao pergunte a proxima.
// Perguntar todas de uma vez manda varias ao mesmo hex (sem a reserva da anterior).
// O "por que" esta no Console logo depois: unity command console get --tail 40
int meuSlot = 1;
int unidadeId = 4;

var mc = UnityEngine.Object.FindAnyObjectByType<MatchController>();
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
UnitManager u = null;
foreach (var x in UnitManager.AllActive) if (x != null && x.InstanceId == unidadeId) u = x;
if (u == null) return "unidade nao achada";
var a = (PlayerAction)typeof(AIController).GetMethod("DecideUnitAction", BindingFlags.NonPublic | BindingFlags.Instance)
    .Invoke(ai, new object[] { u, AIWorldSnapshot.Build(PlayerSlotId.FromIndex(meuSlot), mc) });
if (a == null) return "IA: nada";
var sb = new System.Text.StringBuilder();
sb.AppendLine("IA: " + a.SensorAction + " de " + a.MoveFrom.x + "," + a.MoveFrom.y + " para " + a.MoveTo.x + "," + a.MoveTo.y + " | " + a.DebugLabel);
foreach (var st in a.SubSteps) sb.AppendLine("  passo: " + st.Label + " alvo=" + st.TargetInstanceId + " hex=" + st.TargetHex.x + "," + st.TargetHex.y);
return sb.ToString();
