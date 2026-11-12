// Somente leitura: cronometra as partes da simulacao de compra (mesmas chamadas da janela Shopping Pressure, sem comprar).
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var match=UnityEngine.Object.FindAnyObjectByType<MatchController>();
foreach(int s in new[]{0,1}){
  var slot=PlayerSlotId.FromIndex(s);
  var sw=System.Diagnostics.Stopwatch.StartNew();
  var snap=AIWorldSnapshot.Build(slot,match); long tSnap=sw.ElapsedMilliseconds; sw.Restart();
  var demands=AIShoppingPlanner.InspectRoleDemands(snap); long tDem=sw.ElapsedMilliseconds; sw.Restart();
  var roster=AIShoppingPlanner.InspectRosterKnowledge(snap); long tRos=sw.ElapsedMilliseconds; sw.Restart();
  var op=AIShoppingPlanner.InspectOperationalPressure(snap); long tOp=sw.ElapsedMilliseconds; sw.Restart();
  var counter=AIShoppingPlanner.InspectCounterPressure(snap); long tCnt=sw.ElapsedMilliseconds; sw.Restart();
  var queue=PressaoCaronaService.Read(AIController.Instance,s); long tQ=sw.ElapsedMilliseconds;
  sb.Append("slot ").Append(s).Append(": snapshot=").Append(tSnap).Append("ms demandas=").Append(tDem).Append("ms roster=").Append(tRos).Append("ms operacional=").Append(tOp).Append("ms counter=").Append(tCnt).Append("ms filaCarona=").Append(tQ).Append("ms").AppendLine();
}
return sb.ToString();
