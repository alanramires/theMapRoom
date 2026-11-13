// Somente leitura (limpa caches que se reconstroem sozinhos): reproduz o BuildObjectivePlan do INICIO do turno,
// quando os caches por revisao do tabuleiro estao frios, e cronometra os helpers de leitura chamados por ele.
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var F=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static;
System.Action limpar=()=>{
  UnitReachEnvelopeService.ClearCache();
  foreach(var t in new[]{typeof(AIRouteDistance),typeof(QueroCaronaService),typeof(CaptureOpportunityClaimService),typeof(UnitMovementPathRules)})
    foreach(var f in t.GetFields(F)){ if(!f.IsStatic)continue;
      if(f.Name.IndexOf("Cache",System.StringComparison.OrdinalIgnoreCase)>=0){var v=f.GetValue(null);var m=v?.GetType().GetMethod("Clear",System.Type.EmptyTypes);if(m!=null)m.Invoke(v,null);}
      if(f.FieldType==typeof(int)&&f.Name.IndexOf("Revision",System.StringComparison.OrdinalIgnoreCase)>=0)f.SetValue(null,-1);}
  typeof(AIController).GetMethod("ResetRallyDecisionFacts",F)?.Invoke(null,null);
};
var ai=AIController.Instance; var match=UnityEngine.Object.FindAnyObjectByType<MatchController>();
var slot=PlayerSlotId.FromIndex(1); var snap=AIWorldSnapshot.Build(slot,match); var team=snap.AITeam;
var build=typeof(AIController).GetMethod("BuildObjectivePlan",F);
var sw=System.Diagnostics.Stopwatch.StartNew();
limpar(); sw.Restart(); build.Invoke(ai,new object[]{snap}); sb.Append("BuildObjectivePlan FRIO=").Append(sw.ElapsedMilliseconds).Append("ms");
sw.Restart(); build.Invoke(ai,new object[]{snap}); sb.Append(" quente=").Append(sw.ElapsedMilliseconds).Append("ms").AppendLine();
object Call(string n,params object[] a){var m=typeof(AIController).GetMethod(n,F);if(m==null){sb.Append("  (sem ").Append(n).Append(")").AppendLine();return null;}limpar();sw.Restart();object r=null;try{r=m.Invoke(m.IsStatic?null:ai,a);}catch(System.Exception e){sb.Append("  erro ").Append(n).Append(": ").Append((e.InnerException??e).Message).AppendLine();return null;}sb.Append("  ").Append(n).Append(" frio=").Append(sw.ElapsedMilliseconds).Append("ms").AppendLine();return r;}
limpar(); sw.Restart(); InvasionAxisMap.Build(slot); sb.Append("  InvasionAxisMap.Build frio=").Append(sw.ElapsedMilliseconds).Append("ms").AppendLine();
Call("UpdateAxisSidePolitics",snap);
Call("BuildEixoPresence",team);
Call("BuildPreviousObjectiveSnapshot",team);
object intel=Call("BuildPlanIntelReport",snap);
if(intel!=null)Call("BuildRallyPlanContext",team,snap.AISlotIndex,snap.TurnNumber,intel);
Call("BuildAnchorPlanContext",team,snap.TurnNumber);
Call("MapHasAnyRallyPointForSlot",snap.AISlotIndex);
limpar(); sw.Restart(); AITacticalAnalyzer.Instance.Rebuild(snap,ObjectiveManager.GetPlanForSlot(slot)); sb.Append("  Analyzer.Rebuild frio=").Append(sw.ElapsedMilliseconds).Append("ms").AppendLine();
return sb.ToString();
