// Somente leitura: custo de UMA chamada de PodeMirarSensor.CollectValidFireCellsFromOrigin para uma aeronave,
// com e sem as validacoes (LDT, LoS, spotter). Nao move nada.
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var ai=AIController.Instance; var board=ai.BoardTilemapRef; var terrain=ai.TerrainDatabaseRef;
UnitManager alvo=null; foreach(var u in UnitManager.AllActive){ if(u!=null&&!u.IsDead&&!u.IsEmbarked&&u.GetAircraftType()!=AircraftType.None&&u.GetDomain()==Domain.Air){alvo=u;break;} }
if(alvo==null)return "sem aeronave em voo";
var origem=alvo.CurrentCellPosition; origem.z=0;
sb.Append(alvo.UnitDisplayName).Append("#").Append(alvo.InstanceId).Append(" slot=").Append(alvo.SlotIndex).Append(" em ").Append(origem).AppendLine();
var sw=new System.Diagnostics.Stopwatch();
void T(string nome,bool ldt,bool los,bool spot,SensorMovementMode modo){
  var cells=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
  PodeMirarSensor.CollectValidFireCellsFromOrigin(alvo,board,terrain,modo,origem,cells,null,ldt,los,spot);
  sw.Restart(); for(int i=0;i<20;i++){cells.Clear();PodeMirarSensor.CollectValidFireCellsFromOrigin(alvo,board,terrain,modo,origem,cells,null,ldt,los,spot);}
  sb.Append("  ").Append(nome).Append(": ").Append(sw.Elapsed.TotalMilliseconds/20.0).Append(" ms/chamada cells=").Append(cells.Count).AppendLine();
}
T("andando ldt+los+spot",true,true,true,SensorMovementMode.MoveuAndando);
T("andando sem ldt",false,true,true,SensorMovementMode.MoveuAndando);
T("andando sem los",true,false,true,SensorMovementMode.MoveuAndando);
T("andando sem spot",true,true,false,SensorMovementMode.MoveuAndando);
T("andando sem nada",false,false,false,SensorMovementMode.MoveuAndando);
T("parado ldt+los+spot",true,true,true,SensorMovementMode.MoveuParado);
int vivos=0; foreach(var u in UnitManager.AllActive)if(u!=null&&!u.IsDead)vivos++;
int hier=UnityEngine.Object.FindObjectsByType<UnitManager>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Length;
sb.Append("unidades vivas=").Append(vivos).Append(" UnitManager na hierarquia (inclui inativos)=").Append(hier);
return sb.ToString();
