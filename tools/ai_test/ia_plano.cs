using System.Reflection;
// Monta o plano da IA para um slot, como ela faz no topo do turno. NAO executa nada.
// Rode uma vez no comeco do turno, antes de perguntar decisoes (ia_unidade.cs) ou compra (ia_compra.cs).
int meuSlot = 1;

var mc = UnityEngine.Object.FindAnyObjectByType<MatchController>();
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
var meu = PlayerSlotId.FromIndex(meuSlot);
typeof(AIController).GetMethod("BuildObjectivePlan", BindingFlags.NonPublic | BindingFlags.Instance)
    .Invoke(ai, new object[] { AIWorldSnapshot.Build(meu, mc) });
var plan = ObjectiveManager.GetPlanForSlot(meu);
if (plan == null) return "sem plano";
var sb = new System.Text.StringBuilder();
foreach (var o in plan.Objectives)
{
    var linha = o.Sector + " " + o.ObjectiveType + " " + o.Status + " pri=" + o.Priority + " |";
    foreach (var s in o.Slots) linha += " " + s.Role + (s.Filled ? "=#" + s.AssignedUnitId : "=vazio");
    sb.AppendLine(linha);
}
return sb.ToString();
