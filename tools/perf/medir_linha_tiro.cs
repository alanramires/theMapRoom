// Somente leitura: custo de HasValidStraightLineOfFire (PodeMirarSensor) para um par de celulas,
// e quanto dele e GetStructureAtCell (contadores do ObservationCellService).
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var F=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static;
var ai=AIController.Instance; var board=ai.BoardTilemapRef; var terrain=ai.TerrainDatabaseRef;
UnitManager alvo=null; foreach(var u in UnitManager.AllActive){ if(u!=null&&!u.IsDead&&!u.IsEmbarked&&u.GetAircraftType()!=AircraftType.None&&u.GetDomain()==Domain.Air){alvo=u;break;} }
if(alvo==null)return "sem aeronave em voo";
var origem=alvo.CurrentCellPosition; origem.z=0; var destino=origem+new UnityEngine.Vector3Int(2,0,0);
var armas=alvo.GetEmbarkedWeapons(); if(armas==null||armas.Count==0)return "sem arma";
WeaponData arma=armas[0].weapon;
var m=typeof(PodeMirarSensor).GetMethod("HasValidStraightLineOfFire",F);
if(m==null)return "sem HasValidStraightLineOfFire";
var ps=m.GetParameters(); sb.Append("assinatura: "); foreach(var p in ps)sb.Append(p.ParameterType.Name).Append(" "); sb.AppendLine();
object[] args=new object[ps.Length];
args[0]=board;args[1]=terrain;args[2]=origem;args[3]=destino;args[4]=alvo;args[5]=null;args[6]=null;args[7]=arma;
for(int i=8;i<ps.Length;i++)args[i]=ps[i].ParameterType==typeof(bool)?(object)true:null;
var sw=new System.Diagnostics.Stopwatch();
m.Invoke(null,args);
ObservationCellService.ResetCounters();
sw.Restart(); for(int i=0;i<50;i++)m.Invoke(null,args);
sb.Append("HasValidStraightLineOfFire (ldt+los): ").Append(sw.Elapsed.TotalMilliseconds/50).Append(" ms/chamada").AppendLine();
sb.Append("ObservationCellService: CellVisionCalls=").Append(ObservationCellService.CellVisionCalls).Append(" ConstructionMs=").Append(ObservationCellService.ConstructionMs).Append(" StructureMs=").Append(ObservationCellService.StructureMs).AppendLine();
args[ps.Length-1]=false; sw.Restart(); for(int i=0;i<50;i++)m.Invoke(null,args);
sb.Append("HasValidStraightLineOfFire (los off): ").Append(sw.Elapsed.TotalMilliseconds/50).Append(" ms/chamada").AppendLine();
sw.Restart(); for(int i=0;i<200;i++)UnityEngine.Object.FindObjectsByType<RoadNetworkManager>(UnityEngine.FindObjectsInactive.Exclude,UnityEngine.FindObjectsSortMode.None);
sb.Append("FindObjectsByType<RoadNetworkManager>: ").Append(sw.Elapsed.TotalMilliseconds/200).Append(" ms").AppendLine();
return sb.ToString();
