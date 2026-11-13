// Somente leitura (limpa caches que se reconstroem sozinhos): decompoe CountEnemyAirInRing frio —
// ilha propria, envelope por aeronave inimiga, guarda-chuva antiaereo por classe.
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var F=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static;
System.Action limpar=()=>{
  UnitReachEnvelopeService.ClearCache();
  foreach(var t in new[]{typeof(AIRouteDistance),typeof(QueroCaronaService),typeof(CaptureOpportunityClaimService),typeof(UnitMovementPathRules),typeof(AIShoppingPlanner),typeof(AIAirExposureService)})
    foreach(var f in t.GetFields(F)){ if(!f.IsStatic)continue;
      if(f.Name.IndexOf("Cache",System.StringComparison.OrdinalIgnoreCase)>=0){var v=f.GetValue(null);var m=v?.GetType().GetMethod("Clear",System.Type.EmptyTypes);if(m!=null)m.Invoke(v,null);}
      if(f.FieldType==typeof(int)&&f.Name.IndexOf("Revision",System.StringComparison.OrdinalIgnoreCase)>=0)f.SetValue(null,-1);}
};
var ai=AIController.Instance; var match=UnityEngine.Object.FindAnyObjectByType<MatchController>();
var slot=PlayerSlotId.FromIndex(1); var snap=AIWorldSnapshot.Build(slot,match);
var board=ai.BoardTilemapRef; var terrain=ai.TerrainDatabaseRef;
var sw=new System.Diagnostics.Stopwatch();
limpar();
var cut=typeof(AIShoppingPlanner).GetMethod("IsGroundCutOffFromEnemy",F,null,new[]{typeof(AIWorldSnapshot),typeof(bool)},null);
sw.Restart(); object cutOff=cut!=null?cut.Invoke(null,new object[]{snap,false}):null; sb.Append("IsGroundCutOffFromEnemy=").Append(cutOff).Append(" ").Append(sw.ElapsedMilliseconds).Append("ms").AppendLine();
var probe=typeof(AIShoppingPlanner).GetMethod("BuildOwnLandProbe",F,null,new[]{typeof(AIWorldSnapshot)},null);
sw.Restart(); if(probe!=null)probe.Invoke(null,new object[]{snap}); sb.Append("BuildOwnLandProbe ").Append(sw.ElapsedMilliseconds).Append("ms").AppendLine();
long totalEnv=0;
foreach(var enemy in snap.EnemyUnits){
  if(enemy==null||enemy.IsDead||!enemy.TryGetUnitData(out UnitData data)||data==null||data.domain!=Domain.Air)continue;
  int budget=UnityEngine.Mathf.Max(1,enemy.MaxMovementPoints);
  sw.Restart(); var env=AIAirExposureService.BuildEnvelope(enemy,board,terrain,ReachIntent.Combat,budget); long t1=sw.ElapsedMilliseconds;
  sw.Restart(); if(env==null)AIAirExposureService.BuildEnvelope(enemy,board,terrain,ReachIntent.Capture,budget); long t2=sw.ElapsedMilliseconds;
  totalEnv+=t1+t2;
  sb.Append("  ").Append(enemy.UnitDisplayName).Append("#").Append(enemy.InstanceId).Append(" MP=").Append(budget).Append(" fuel=").Append(enemy.CurrentFuel).Append(" combat=").Append(t1).Append("ms").Append(env==null?" capture="+t2+"ms":"").Append(env!=null?" mov="+env.MovementCells.Count+" atk="+env.AttackableCells.Count:"").AppendLine();
}
sb.Append("envelopes total=").Append(totalEnv).Append("ms").AppendLine();
var umb=new System.Collections.Generic.Dictionary<GameUnitClass,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>();
foreach(GameUnitClass k in System.Enum.GetValues(typeof(GameUnitClass))){ sw.Restart(); var u=AIAirExposureService.GetAntiAirUmbrella(snap,board,terrain,k,umb); sb.Append("  umbrella ").Append(k).Append(" ").Append(sw.ElapsedMilliseconds).Append("ms cells=").Append(u!=null?u.Count:-1).AppendLine(); }
return sb.ToString();
