if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
var created=new System.Collections.Generic.List<UnityEngine.Object>();
var units=new System.Collections.Generic.List<UnitManager>();
var buildings=new System.Collections.Generic.List<ConstructionManager>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
void Set(object o,string field,object value){o.GetType().GetField(field,flags).SetValue(o,value);}
T Asset<T>(string id)where T:UnityEngine.Object{
    foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:"+typeof(T).Name)){
        var item=UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
        var f=typeof(T).GetField("id");if(f!=null && (string)f.GetValue(item)==id)return item;
    }throw new System.Exception("Asset ausente: "+id);
}
var fogRestore=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<MatchController,bool>>();
var rejections=new System.Collections.Generic.List<string>();
UnityEngine.Application.LogCallback captureLog=(message,stack,type)=>{if(message.Contains("recusada como recuperação"))rejections.Add(message);};
UnityEngine.Application.logMessageReceived+=captureLog;
try{
    foreach(var mc in UnityEngine.Object.FindObjectsByType<MatchController>()){fogRestore.Add(new System.Collections.Generic.KeyValuePair<MatchController,bool>(mc,(bool)typeof(MatchController).GetField("debugFogOfWarEnabled",flags).GetValue(mc)));Set(mc,"debugFogOfWarEnabled",false);}
    var grid=new UnityEngine.GameObject("CourierTestGrid",typeof(UnityEngine.Grid));grid.transform.position=new UnityEngine.Vector3(10000,10000,0);
    grid.GetComponent<UnityEngine.Grid>().cellLayout=UnityEngine.GridLayout.CellLayout.Hexagon;
    var mapHost=new UnityEngine.GameObject("CourierTestMap",typeof(UnityEngine.Tilemaps.Tilemap));mapHost.transform.SetParent(grid.transform,false);
    var map=mapHost.GetComponent<UnityEngine.Tilemaps.Tilemap>();map.tileAnchor=UnityEngine.Vector3.zero;
    var plain=Asset<TerrainTypeData>("plains");
    var forest=Asset<TerrainTypeData>("forest");
    for(int x=0;x<10;x++)for(int y=0;y<6;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
    var terrain=UnityEngine.ScriptableObject.CreateInstance<TerrainDatabase>();created.Add(terrain);Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain,forest});
    var soldier=Asset<UnitData>("Soldado");var chinook=Asset<UnitData>("chinook");var truck=Asset<UnitData>("suprimentos");
    var unitDb=UnityEngine.ScriptableObject.CreateInstance<UnitDatabase>();created.Add(unitDb);Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck});
    var city=Asset<ConstructionData>("cidade");
    var constructionDb=UnityEngine.ScriptableObject.CreateInstance<ConstructionDatabase>();created.Add(constructionDb);Set(constructionDb,"constructions",new System.Collections.Generic.List<ConstructionData>{city});
    UnitManager Unit(int id,UnitData data,UnityEngine.Vector3Int cell,bool aboard){
        var go=new UnityEngine.GameObject("CourierTestUnit");go.SetActive(false);var u=go.AddComponent<UnitManager>();u.enabled=false;
        Set(u,"unitDatabase",unitDb);Set(u,"unitId",data.id);Set(u,"instanceId",id);Set(u,"slotIndex",0);Set(u,"boardTilemap",map);
        Set(u,"currentCellPosition",cell);Set(u,"currentHP",10);Set(u,"isDead",false);Set(u,"currentFuel",100);Set(u,"remainingMovementPoints",data.movement);Set(u,"isEmbarked",aboard);
        units.Add(u);return u;
    }
    ConstructionManager Building(int id,UnityEngine.Vector3Int cell,ConstructionSector sector){
        var go=new UnityEngine.GameObject("CourierTestBuilding");go.SetActive(false);var b=go.AddComponent<ConstructionManager>();b.enabled=false;
        Set(b,"constructionDatabase",constructionDb);Set(b,"constructionId",city.id);Set(b,"instanceId",id);Set(b,"boardTilemap",map);
        Set(b,"slotIndex",-1);Set(b,"sector",sector);Set(b,"currentCellPosition",cell);Set(b,"currentCapturePoints",20);
        go.transform.position=map.GetCellCenterWorld(cell);go.SetActive(true);b.enabled=true;Set(b,"currentCellPosition",cell);Set(b,"boardTilemap",map);
        ConstructionManager.AllActive.Add(b);buildings.Add(b);return b;
    }
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    var aircraft=Unit(890002,chinook,new UnityEngine.Vector3Int(1,2,0),false);
    var supplier=Unit(890067,truck,new UnityEngine.Vector3Int(5,2,0),false);
    var blocker=Unit(890099,soldier,new UnityEngine.Vector3Int(9,5,0),false);
    void Position(UnitManager u,UnityEngine.Vector3Int cell){var previousCell=u.CurrentCellPosition;Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,previousCell,cell);}
    foreach(var u in new[]{aircraft,supplier,blocker}){
        var cell=u.CurrentCellPosition;u.gameObject.SetActive(true);u.Apply(u==aircraft?chinook:u==supplier?truck:soldier);
        Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"isEmbarked",false);Position(u,cell);
        if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);
    }
    Set(aircraft,"currentHP",9);Set(aircraft,"currentFuel",13);Set(aircraft,"remainingMovementPoints",6);Set(aircraft,"currentDomain",Domain.Air);Set(aircraft,"currentHeightLevel",HeightLevel.AirLow);Set(aircraft,"isUnderRepair",true);Set(aircraft,"hasActed",false);
    var host=new UnityEngine.GameObject("RecoveryServiceAI");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,MyUnits=new System.Collections.Generic.List<UnitManager>{aircraft,supplier},EnemyUnits=new System.Collections.Generic.List<UnitManager>()};
    var collect=typeof(AIController).GetMethod("CollectAircraftRecoveryAnchors",flags);
    var evaluate=typeof(AIController).GetMethod("TryEvaluateAircraftRecoveryAnchor",flags);
    var query=typeof(AIController).GetMethod("CanSupplierRecoverAircraft",flags);
    object Member(object value,string name){return value.GetType().GetField(name)?.GetValue(value)??value.GetType().GetProperty(name)?.GetValue(value);}
    bool CanServe(UnityEngine.Vector3Int cell){object[] args={supplier,aircraft,cell,null};return(bool)query.Invoke(ai,args);}
    object Collect(){return collect.Invoke(ai,new object[]{aircraft,snapshot});}
    System.Collections.IList Filter(object anchors,System.Func<object,bool> accept){var result=(System.Collections.IList)System.Activator.CreateInstance(anchors.GetType());foreach(var a in(System.Collections.IEnumerable)anchors)if(accept(a))result.Add(a);return result;}
    System.Collections.IList Suppliers(object anchors){return Filter(anchors,a=>(string)Member(a,"Kind")=="supridor");}
    object Decide(object anchors,System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>> paths,bool tactical=true){object[] args={aircraft,anchors,snapshot,aircraft.CurrentCellPosition,paths,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),20,tactical,null};return(bool)evaluate.Invoke(ai,args)?args[8]:null;}
    var neighbors=new System.Collections.Generic.List<UnityEngine.Vector3Int>();UnitMovementPathRules.GetImmediateHexNeighbors(map,supplier.CurrentCellPosition,neighbors);
    var bad=neighbors[0];var good=neighbors[1];map.SetTile(bad,forest.paletteTile);
    if(CanServe(bad)||!CanServe(good))throw new System.Exception("Consulta nao distinguiu floresta de planicie futura.");
    var paths=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>();
    paths[bad]=new System.Collections.Generic.List<UnityEngine.Vector3Int>{aircraft.CurrentCellPosition,bad};paths[good]=new System.Collections.Generic.List<UnityEngine.Vector3Int>{aircraft.CurrentCellPosition,good};
    var chosen=Decide(Suppliers(Collect()),paths);
    if(chosen==null||(UnityEngine.Vector3Int)Member(chosen,"ActionCell")!=good)throw new System.Exception("Primeiro vizinho invalido impediu alternativa valida.");
    foreach(var cell in neighbors)map.SetTile(cell,forest.paletteTile);map.SetTile(supplier.CurrentCellPosition,forest.paletteTile);
    var absent=Suppliers(Collect());
    if(absent.Count!=0||Decide(absent,paths)!=null||Decide(absent,null,false)!=null)throw new System.Exception("Supridor sem vizinho atendivel entrou no Tatico/Operacional.");
    foreach(var cell in neighbors)map.SetTile(cell,plain.paletteTile);map.SetTile(supplier.CurrentCellPosition,plain.paletteTile);
    // A valid first neighbor must not hide a better valid neighbor either.
    paths.Clear();var ordered=new System.Collections.Generic.List<UnityEngine.Vector3Int>(neighbors);
    ordered.Sort((a,b)=>AIActionReachCoordinator.CubicDistance(aircraft.CurrentCellPosition,b).CompareTo(AIActionReachCoordinator.CubicDistance(aircraft.CurrentCellPosition,a)));
    foreach(var cell in ordered)paths[cell]=new System.Collections.Generic.List<UnityEngine.Vector3Int>{aircraft.CurrentCellPosition,cell};
    chosen=Decide(Suppliers(Collect()),paths);
    var minDistance=AIActionReachCoordinator.CubicDistance(aircraft.CurrentCellPosition,ordered[ordered.Count-1]);
    if(chosen==null||AIActionReachCoordinator.CubicDistance(aircraft.CurrentCellPosition,(UnityEngine.Vector3Int)Member(chosen,"ActionCell"))!=minDistance)throw new System.Exception("Primeiro vizinho valido ocultou outro melhor.");
    if(Decide(Suppliers(Collect()),null,false)==null)throw new System.Exception("Supridor com vizinho valido ausente no Operacional.");
    var hospital=Building(890019,new UnityEngine.Vector3Int(2,2,0),ConstructionSector.Alpha);hospital.Apply(city);Set(hospital,"slotIndex",ownSlot);Set(hospital,"teamId",TeamId.Blue);Set(hospital,"currentCapturePoints",hospital.CapturePointsMax);Set(hospital,"hasInfiniteSuppliesOverride",true);
    bool IsHospital(object a){return(string)Member(a,"Kind")=="aerodromo/construcao de reparo"&&(UnityEngine.Vector3Int)Member(a,"Cell")==hospital.CurrentCellPosition;}
    var all=Collect();if(Filter(all,IsHospital).Count!=1)throw new System.Exception("Hospital apto ausente.");
    paths.Clear();paths[hospital.CurrentCellPosition]=new System.Collections.Generic.List<UnityEngine.Vector3Int>{aircraft.CurrentCellPosition,hospital.CurrentCellPosition};foreach(var cell in neighbors)paths[cell]=new System.Collections.Generic.List<UnityEngine.Vector3Int>{aircraft.CurrentCellPosition,cell};
    var competition=Filter(all,a=>IsHospital(a)||(string)Member(a,"Kind")=="supridor");chosen=Decide(competition,paths);
    if(chosen==null||(string)Member(chosen,"Reason")!="aerodromo/construcao de reparo")throw new System.Exception("Supridor distante venceu hospital no caminho.");
    Position(blocker,hospital.CurrentCellPosition);
    if(Filter(Collect(),IsHospital).Count!=0)throw new System.Exception("Hospital ocupado aceito.");
    Position(blocker,new UnityEngine.Vector3Int(9,5,0));Set(hospital,"siteRuntime",new ConstructionSiteRuntime());
    if(Filter(Collect(),IsHospital).Count!=0)throw new System.Exception("Hospital sem servico aceito.");
    hospital.Apply(city);Set(hospital,"slotIndex",ownSlot);Set(hospital,"teamId",TeamId.Blue);Set(hospital,"currentCapturePoints",hospital.CapturePointsMax);Set(hospital,"hasInfiniteSuppliesOverride",true);
    // The same service cell can be a valid hospital and a supplier neighbor:
    // an exact distance/threat tie must choose the supplier without a bonus.
    Position(supplier,new UnityEngine.Vector3Int(3,2,0));neighbors.Clear();UnitMovementPathRules.GetImmediateHexNeighbors(map,supplier.CurrentCellPosition,neighbors);
    paths.Clear();paths[hospital.CurrentCellPosition]=new System.Collections.Generic.List<UnityEngine.Vector3Int>{aircraft.CurrentCellPosition,hospital.CurrentCellPosition};
    competition=Filter(Collect(),a=>IsHospital(a)||(string)Member(a,"Kind")=="supridor");chosen=Decide(competition,paths);
    if(chosen==null||(string)Member(chosen,"Reason")!="supridor")throw new System.Exception("Empate nao favoreceu supridor.");
    var reversed=(System.Collections.IList)System.Activator.CreateInstance(competition.GetType());for(int i=competition.Count-1;i>=0;i--)reversed.Add(competition[i]);
    if((string)Member(Decide(reversed,paths),"Reason")!="supridor")throw new System.Exception("Desempate depende da ordem.");
    // Supplier closer than the hospital wins on distance too.
    Position(aircraft,new UnityEngine.Vector3Int(4,2,0));paths.Clear();paths[hospital.CurrentCellPosition]=new System.Collections.Generic.List<UnityEngine.Vector3Int>{aircraft.CurrentCellPosition,hospital.CurrentCellPosition};paths[aircraft.CurrentCellPosition]=new System.Collections.Generic.List<UnityEngine.Vector3Int>{aircraft.CurrentCellPosition};
    competition=Filter(Collect(),a=>IsHospital(a)||(string)Member(a,"Kind")=="supridor");chosen=Decide(competition,paths);
    if(chosen==null||(string)Member(chosen,"Reason")!="supridor")throw new System.Exception("Supridor mais proximo nao venceu.");
    if(aircraft.CurrentHP!=9||aircraft.CurrentFuel!=13||aircraft.HasActed||aircraft.GetDomain()!=Domain.Air||aircraft.GetHeightLevel()!=HeightLevel.AirLow)throw new System.Exception("Consulta mudou estado confirmado.");
    return "PASS: floresta recusada e outro vizinho aceito; sem vizinhos, sem ancora Tatica/Operacional; hospital no caminho vence; hospital ocupado/sem servico recusado; supridor mais proximo vence e desempata sem depender da ordem; consultas sem mutacao de HP/combustivel/camada/HasActed.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
