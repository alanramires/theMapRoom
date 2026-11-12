// Equivale a um AI Replan do slot 1: cronometra BuildObjectivePlan e AITacticalAnalyzer.Rebuild em separado.
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
var ai=AIController.Instance; var match=UnityEngine.Object.FindAnyObjectByType<MatchController>();
var slot=PlayerSlotId.FromIndex(1);
var snap=AIWorldSnapshot.Build(slot,match);
var build=typeof(AIController).GetMethod("BuildObjectivePlan",flags);
var sw=System.Diagnostics.Stopwatch.StartNew();
build.Invoke(ai,new object[]{snap}); long tPlan=sw.ElapsedMilliseconds; sw.Restart();
AITacticalAnalyzer.Instance.Rebuild(snap,ObjectiveManager.GetPlanForSlot(slot)); long tAn=sw.ElapsedMilliseconds; sw.Restart();
build.Invoke(ai,new object[]{snap}); long tPlan2=sw.ElapsedMilliseconds; sw.Restart();
AITacticalAnalyzer.Instance.Rebuild(snap,ObjectiveManager.GetPlanForSlot(slot)); long tAn2=sw.ElapsedMilliseconds;
sb.Append("slot 1: BuildObjectivePlan=").Append(tPlan).Append("ms Analyzer.Rebuild=").Append(tAn).Append("ms | segunda vez: plano=").Append(tPlan2).Append("ms analyzer=").Append(tAn2).Append("ms");
return sb.ToString();
