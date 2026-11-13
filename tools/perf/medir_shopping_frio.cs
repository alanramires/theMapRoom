// Somente leitura (limpa caches que se reconstroem sozinhos): cronometra as partes da compra com os caches
// por revisao do tabuleiro FRIOS, como no primeiro Decide do turno. Nao compra nada.
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var F=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static;
System.Action limpar=()=>{
  UnitReachEnvelopeService.ClearCache();
  foreach(var t in new[]{typeof(AIRouteDistance),typeof(QueroCaronaService),typeof(CaptureOpportunityClaimService),typeof(UnitMovementPathRules),typeof(AIShoppingPlanner)})
    foreach(var f in t.GetFields(F)){ if(!f.IsStatic)continue;
      if(f.Name.IndexOf("Cache",System.StringComparison.OrdinalIgnoreCase)>=0){var v=f.GetValue(null);var m=v?.GetType().GetMethod("Clear",System.Type.EmptyTypes);if(m!=null)m.Invoke(v,null);}
      if(f.FieldType==typeof(int)&&f.Name.IndexOf("Revision",System.StringComparison.OrdinalIgnoreCase)>=0)f.SetValue(null,-1);}
  typeof(AIController).GetMethod("ResetRallyDecisionFacts",F)?.Invoke(null,null);
};
var match=UnityEngine.Object.FindAnyObjectByType<MatchController>();
var sw=new System.Diagnostics.Stopwatch();
foreach(int s in new[]{1,0}){
  var slot=PlayerSlotId.FromIndex(s); var snap=AIWorldSnapshot.Build(slot,match);
  sb.Append("slot ").Append(s).AppendLine();
  void T(string nome,System.Action a){limpar();sw.Restart();try{a();}catch(System.Exception e){sb.Append("  erro ").Append(nome).Append(": ").Append((e.InnerException??e).Message).AppendLine();return;}long frio=sw.ElapsedMilliseconds;sw.Restart();a();sb.Append("  ").Append(nome).Append(" frio=").Append(frio).Append("ms quente=").Append(sw.ElapsedMilliseconds).Append("ms").AppendLine();}
  T("InspectRoleDemands",()=>AIShoppingPlanner.InspectRoleDemands(snap));
  T("InspectRosterKnowledge",()=>AIShoppingPlanner.InspectRosterKnowledge(snap));
  T("InspectOperationalPressure",()=>AIShoppingPlanner.InspectOperationalPressure(snap));
  T("InspectCounterPressure",()=>AIShoppingPlanner.InspectCounterPressure(snap));
  T("PressaoCaronaService.Read",()=>PressaoCaronaService.Read(AIController.Instance,s));
  var ring=typeof(AIShoppingPlanner).GetMethod("CountEnemyAirInRing",F,null,new[]{typeof(AIWorldSnapshot),typeof(int).MakeByRefType()},null);
  if(ring!=null)T("CountEnemyAirInRing",()=>ring.Invoke(null,new object[]{snap,0}));
  var ded=typeof(AITacticalAnalyzer).GetMethod("GetDeficits",F);
  if(ded!=null&&AITacticalAnalyzer.Instance!=null){var ps=ded.GetParameters();var args=new object[ps.Length];args[0]=slot;for(int i=1;i<ps.Length;i++)args[i]=ps[i].HasDefaultValue?ps[i].DefaultValue:null;T("Analyzer.GetDeficits",()=>ded.Invoke(AITacticalAnalyzer.Instance,args));}
}
return sb.ToString();
