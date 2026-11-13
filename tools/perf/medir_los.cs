// Somente leitura: custo unitario das consultas por celula usadas pela linha de visada
// (traco completo, construcao na celula, estrutura na celula, terreno na celula).
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var F=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static;
var ai=AIController.Instance; var board=ai.BoardTilemapRef; var terrain=ai.TerrainDatabaseRef;
UnitManager alvo=null; foreach(var u in UnitManager.AllActive){ if(u!=null&&!u.IsDead&&!u.IsEmbarked&&u.GetAircraftType()!=AircraftType.None&&u.GetDomain()==Domain.Air){alvo=u;break;} }
if(alvo==null)return "sem aeronave em voo";
var origem=alvo.CurrentCellPosition; origem.z=0;
var destino=origem+new UnityEngine.Vector3Int(2,0,0);
sb.Append(alvo.UnitDisplayName).Append("#").Append(alvo.InstanceId).Append(" ").Append(origem).Append(" -> ").Append(destino).Append(" tile destino=").Append(board.GetTile(destino)?.name).AppendLine();
var sw=new System.Diagnostics.Stopwatch();
System.Collections.Generic.List<UnityEngine.Vector3Int> inter; System.Collections.Generic.List<float> ev; UnityEngine.Vector3Int blk;
bool r=ObservationLineService.TryTrace(board,terrain,origem,destino,alvo,null,null,out inter,out ev,out blk,true,null,null);
sw.Restart(); for(int i=0;i<50;i++)ObservationLineService.TryTrace(board,terrain,origem,destino,alvo,null,null,out inter,out ev,out blk,true,null,null);
sb.Append("TryTrace (LoS on): ").Append(sw.Elapsed.TotalMilliseconds/50).Append(" ms  resultado=").Append(r).Append(" intermediarias=").Append(inter?.Count).AppendLine();
sw.Restart(); for(int i=0;i<50;i++)ObservationLineService.TryTrace(board,terrain,origem,destino,alvo,null,null,out inter,out ev,out blk,false,null,null);
sb.Append("TryTrace (LoS off): ").Append(sw.Elapsed.TotalMilliseconds/50).Append(" ms").AppendLine();
sw.Restart(); for(int i=0;i<1000;i++)ConstructionOccupancyRules.GetConstructionAtCell(board,destino);
sb.Append("GetConstructionAtCell: ").Append(sw.Elapsed.TotalMilliseconds/1000).Append(" ms").AppendLine();
sw.Restart(); for(int i=0;i<1000;i++)StructureOccupancyRules.GetStructureAtCell(board,destino);
sb.Append("GetStructureAtCell: ").Append(sw.Elapsed.TotalMilliseconds/1000).Append(" ms").AppendLine();
var trt=typeof(PodeMirarSensor).GetMethod("TryResolveTerrainAtCell",F);
if(trt!=null){var args=new object[]{board,terrain,destino,null};sw.Restart();for(int i=0;i<1000;i++)trt.Invoke(null,args);sb.Append("TryResolveTerrainAtCell: ").Append(sw.Elapsed.TotalMilliseconds/1000).Append(" ms terreno=").Append((args[3] as TerrainTypeData)?.name).AppendLine();}
var grid=board.layoutGrid; sw.Restart(); for(int i=0;i<1000;i++)grid.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true); sb.Append("grid.GetComponentsInChildren<Tilemap>: ").Append(sw.Elapsed.TotalMilliseconds/1000).Append(" ms (").Append(grid.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true).Length).Append(" tilemaps)").AppendLine();
sw.Restart(); for(int i=0;i<1000;i++){foreach(var u in UnitManager.AllActive){if(u!=null&&!u.IsDead&&u.CurrentCellPosition==destino)break;}} sb.Append("varredura AllActive por celula: ").Append(sw.Elapsed.TotalMilliseconds/1000).Append(" ms").AppendLine();
return sb.ToString();
