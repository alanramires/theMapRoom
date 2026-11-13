// Somente leitura: CalcularCaminhosValidos de um capturador com orcamentos crescentes (MP real, 6, 10, 30),
// para separar "malha maior" de "custo por celula".
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var ai=AIController.Instance; var board=ai.BoardTilemapRef; var terrain=ai.TerrainDatabaseRef;
var match=UnityEngine.Object.FindAnyObjectByType<MatchController>();
var snap=AIWorldSnapshot.Build(PlayerSlotId.FromIndex(0),match);
UnitManager cap=null; foreach(var u in snap.MyUnits){ if(u!=null&&!u.IsDead&&!u.IsEmbarked&&u.TryGetUnitData(out UnitData d)&&d!=null&&d.roles!=null&&d.roles.Count>0&&d.roles[0]==UnitRole.Capturador){cap=u;break;} }
if(cap==null)return "sem capturador";
sb.Append(cap.UnitDisplayName).Append("#").Append(cap.InstanceId).Append(" em ").Append(cap.CurrentCellPosition).Append(" MP=").Append(cap.MaxMovementPoints).Append("/").Append(cap.RemainingMovementPoints).AppendLine();
var sw=new System.Diagnostics.Stopwatch();
foreach(int b in new[]{cap.RemainingMovementPoints,cap.MaxMovementPoints,6,10,15,30}){
  sw.Restart(); var p=UnitMovementPathRules.CalcularCaminhosValidos(board,cap,b,terrain); long t=sw.ElapsedMilliseconds;
  sw.Restart(); UnitMovementPathRules.CalcularCaminhosValidos(board,cap,b,terrain); long t2=sw.ElapsedMilliseconds;
  int maior=0; foreach(var kv in p)if(kv.Value.Count>maior)maior=kv.Value.Count;
  sb.Append("  orcamento ").Append(b).Append(": ").Append(t).Append("ms (repetido ").Append(t2).Append("ms) celulas=").Append(p.Count).Append(" caminho mais longo=").Append(maior).AppendLine();
}
sw.Restart(); var cm=UnitMovementPathRules.CalculateMovementCostMap(board,cap,cap.CurrentCellPosition,30,terrain); sb.Append("CalculateMovementCostMap(30): ").Append(sw.ElapsedMilliseconds).Append("ms celulas=").Append(cm.Count).AppendLine();
return sb.ToString();
