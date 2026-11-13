// Somente leitura: custo por capturador das consultas que a atribuicao do plano (Passo 5b) faz uma
// vez por turno — mapa de custo em MP com orcamento 30, envelope tatico de captura, distancia terrestre.
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var F=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static;
var ai=AIController.Instance; var board=ai.BoardTilemapRef; var terrain=ai.TerrainDatabaseRef;
var match=UnityEngine.Object.FindAnyObjectByType<MatchController>();
var tact=typeof(AIController).GetMethod("BuildPlanCaptureTactical",F);
var sw=new System.Diagnostics.Stopwatch();
foreach(int s in new[]{0,1}){
  var snap=AIWorldSnapshot.Build(PlayerSlotId.FromIndex(s),match);
  int n=0; long tPaths=0,tTact=0,tTact2=0; int maxPaths=0;
  UnitReachEnvelopeService.ClearCache();
  foreach(var u in snap.MyUnits){
    if(u==null||u.IsDead||u.IsEmbarked||!u.TryGetUnitData(out UnitData d)||d==null||d.roles==null||d.roles.Count==0||d.roles[0]!=UnitRole.Capturador)continue;
    n++;
    sw.Restart(); var paths=UnitMovementPathRules.CalculateMovementCostMap(board,u,u.CurrentCellPosition,30,terrain); tPaths+=sw.ElapsedMilliseconds; if(paths!=null&&paths.Count>maxPaths)maxPaths=paths.Count;
    sw.Restart(); tact.Invoke(ai,new object[]{u}); tTact+=sw.ElapsedMilliseconds;
    sw.Restart(); tact.Invoke(ai,new object[]{u}); tTact2+=sw.ElapsedMilliseconds;
  }
  sb.Append("slot ").Append(s).Append(": capturadores=").Append(n).Append(" CalculateMovementCostMap(30) total=").Append(tPaths).Append("ms (maior malha ").Append(maxPaths).Append(" celulas) BuildPlanCaptureTactical frio total=").Append(tTact).Append("ms quente=").Append(tTact2).Append("ms").AppendLine();
}
var a=new UnityEngine.Vector3Int(8,12,0); var b=new UnityEngine.Vector3Int(14,16,0);
sw.Restart(); for(int i=0;i<200;i++)SectorManager.TryGetLandMovementDistance(a,b,out int c); sb.Append("TryGetLandMovementDistance: ").Append(sw.Elapsed.TotalMilliseconds/200).Append(" ms").AppendLine();
return sb.ToString();
