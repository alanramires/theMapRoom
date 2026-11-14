if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
void Set(object o,string name,object value){o.GetType().GetField(name,flags).SetValue(o,value);}
void Check(bool ok,string why){if(!ok)throw new System.Exception(why);}
var buildings=new System.Collections.Generic.List<ConstructionManager>();
ConstructionDatabase db=null;
try{
 var grid=new UnityEngine.GameObject("InfiniteSuppliesGrid",typeof(UnityEngine.Grid));
 var map=new UnityEngine.GameObject("Board",typeof(UnityEngine.Tilemaps.Tilemap)).GetComponent<UnityEngine.Tilemaps.Tilemap>();map.transform.SetParent(grid.transform);
 ConstructionData city=null;
 foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:ConstructionData")){
  var data=UnityEditor.AssetDatabase.LoadAssetAtPath<ConstructionData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));if(data.id=="cidade"){city=data;break;}
 }
 Check(city!=null,"Ficha cidade ausente");
 db=UnityEngine.ScriptableObject.CreateInstance<ConstructionDatabase>();Set(db,"constructions",new System.Collections.Generic.List<ConstructionData>{city});
 ConstructionManager Building(int id,int slot,ConstructionSector sector,int x){
  var go=new UnityEngine.GameObject("DebugSupplyBuilding"+id);go.SetActive(false);
  var b=go.AddComponent<ConstructionManager>();b.enabled=false;
  Set(b,"constructionDatabase",db);Set(b,"constructionId",city.id);Set(b,"instanceId",id);Set(b,"boardTilemap",map);Set(b,"slotIndex",slot);Set(b,"sector",sector);Set(b,"currentCellPosition",new UnityEngine.Vector3Int(x,0,0));
  go.transform.position=map.GetCellCenterWorld(new UnityEngine.Vector3Int(x,0,0));go.SetActive(true);
  if(!ConstructionManager.AllActive.Contains(b))ConstructionManager.AllActive.Add(b);buildings.Add(b);return b;
 }
 var a=Building(910001,0,ConstructionSector.Alpha,0);var other=Building(910002,1,ConstructionSector.Alpha,1);
 var b=Building(910003,0,ConstructionSector.Base1,2);var c=Building(910004,1,ConstructionSector.Base1,3);
 var sectorHost=new UnityEngine.GameObject("DebugSupplySector");var sectors=sectorHost.AddComponent<SectorManager>();
 SectorManager.RebuildNowFromActiveConstructions(scene,"test");
 var host=new UnityEngine.GameObject("DebugSupplyState");host.SetActive(false);var state=host.AddComponent<TurnStateManager>();
 var cursor=host.AddComponent<CursorController>();var match=host.AddComponent<MatchController>();
 Set(state,"terrainTilemap",map);Set(state,"cursorController",cursor);Set(state,"matchController",match);Set(cursor,"boardTilemap",map);
 Check(state.TrySetInfiniteSuppliesFromDebug("alpha:0 on",out var msg),msg);
 Check(a.HasInfiniteSuppliesOverride&&!other.HasInfiniteSuppliesOverride,"Filtro de dono do setor falhou");
 Check(state.TrySetInfiniteSuppliesFromDebug("base1 on",out msg),msg);
 Check(b.HasInfiniteSuppliesOverride&&c.HasInfiniteSuppliesOverride,"Base nao aplicou a todos os donos");
 Check(state.TrySetInfiniteSuppliesFromDebug("base1 off",out msg),msg);
 Check(!b.HasInfiniteSuppliesOverride&&!c.HasInfiniteSuppliesOverride,"OFF nao limpou override");
 Set(cursor,"currentCell",c.CurrentCellPosition);
 Check(state.TrySetInfiniteSuppliesFromDebug("",out msg),msg);
 Check(c.HasInfiniteSuppliesOverride&&!b.HasInfiniteSuppliesOverride,"Cursor afetou setor inteiro em vez de so o predio");
 Check(state.TrySetInfiniteSuppliesFromDebug("off",out msg),msg);Check(!c.HasInfiniteSuppliesOverride,"Cursor OFF falhou");
 Set(cursor,"currentCell",new UnityEngine.Vector3Int(50,50,0));
 Check(!state.TrySetInfiniteSuppliesFromDebug("",out msg),"Cursor vazio deve ser no-op");
 foreach(var bad in new[]{"alpha:banana on","alpha:999 on","banana on","alpha yes","base1 on off"})Check(!state.TrySetInfiniteSuppliesFromDebug(bad,out msg),"Sintaxe invalida aceita: "+bad);
 Check(a.HasInfiniteSuppliesOverride&&!other.HasInfiniteSuppliesOverride&&!b.HasInfiniteSuppliesOverride&&!c.HasInfiniteSuppliesOverride,"Comando invalido alterou estado");
 Check(SectorManager.TryGetSectorInfo(map,ConstructionSector.Alpha,out var info)&&info.Constructions.Count==2,"SectorManager perdeu membros");
 return "PASS: alpha:0 filtra dono atual; base1 aplica a todos os membros; on/off; sem argumento ativa so o predio sob o cursor; cursor vazio e sintaxe invalida nao alteram nada; SectorManager atualizado. Fora do Play.";
}finally{
 foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
 UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
 UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
 if(db!=null)UnityEngine.Object.DestroyImmediate(db);
}
