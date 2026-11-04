// O JogadasLog da partida aberta: o que cada slot FEZ, com os fatos estruturados
// (custo, HP, DPQ, captura). Base para comparar "jogador x IA" depois da partida.
var jm = JogadasManager.Instance;
var mc = UnityEngine.Object.FindAnyObjectByType<MatchController>();
var sb = new System.Text.StringBuilder();
sb.AppendLine("turno=" + mc.CurrentTurn + " jogadas=" + (jm == null ? 0 : jm.log.jogadas.Count));
if (jm == null) return sb.ToString();
foreach (var j in jm.log.jogadas)
{
    string extra = "";
    if (j.acao == "Compra") extra = " $" + j.valor;
    if (j.acao == "Ataque" && j.hasCombatResult) extra = " hp " + j.hpAntes + "->" + j.hpDepois + " vs " + j.unidadeSigla2 + " " + j.hp2Antes + "->" + j.hp2Depois + (j.hasDpq ? " dpq " + j.dpqAtacante + "x" + j.dpqDefensor : "");
    if (j.acao == "Capturar") extra = " " + j.obs + (string.IsNullOrEmpty(j.predioId) ? "" : " " + j.predioId);
    if (j.acao == "Renda") extra = " +" + j.valor;
    sb.AppendLine("T" + j.turno + " s" + j.team + " " + j.acao + " " + j.unidadeSigla + "#" + j.uid + " (" + j.cx + "," + j.cy + ")->(" + j.dx + "," + j.dy + ")" + extra);
}
return sb.ToString();
