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
    var sea=Asset<TerrainTypeData>("sea");Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain,forest,sea});
    for(int x=0;x<10;x++)for(int y=0;y<6;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),sea.paletteTile);
    var aircraft=Unit(895056,chinook,new UnityEngine.Vector3Int(1,2,0),false);
    var blocker=Unit(895002,chinook,new UnityEngine.Vector3Int(3,2,0),false);
    var cargo=Unit(895003,soldier,aircraft.CurrentCellPosition,true);
    void Position(UnitManager u,UnityEngine.Vector3Int cell){var before=u.CurrentCellPosition;Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,before,cell);}
    foreach(var u in new[]{aircraft,blocker,cargo}){
        var cell=u.CurrentCellPosition;u.gameObject.SetActive(true);u.Apply(u==cargo?soldier:chinook);
        Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"isEmbarked",u==cargo);Set(u,"hasActed",false);Set(u,"currentHP",10);Position(u,cell);
        if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);
    }
    Set(aircraft,"currentFuel",21);Set(aircraft,"remainingMovementPoints",6);Set(aircraft,"currentDomain",Domain.Air);Set(aircraft,"currentHeightLevel",HeightLevel.AirLow);Set(aircraft,"isUnderRepair",true);
    Set(blocker,"currentDomain",Domain.Air);Set(blocker,"currentHeightLevel",HeightLevel.AirLow);Set(blocker,"isUnderRepair",false);
    Set(cargo,"embarkedTransporter",aircraft);
    Set(aircraft,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{slotId="passageiros",slotIndex=0,seatIndex=0,embarkedUnit=cargo,embarkedOnTurn=1}});
    ConstructionManager Hospital(int id,UnityEngine.Vector3Int cell){map.SetTile(cell,plain.paletteTile);var h=Building(id,cell,ConstructionSector.Lima);h.Apply(city);Set(h,"slotIndex",ownSlot);Set(h,"teamId",TeamId.Blue);Set(h,"currentCapturePoints",h.CapturePointsMax);Set(h,"hasInfiniteSuppliesOverride",true);return h;}
    var occupiedCity=Hospital(895010,blocker.CurrentCellPosition);var freeCity=Hospital(895011,new UnityEngine.Vector3Int(6,2,0));
    var host=new UnityEngine.GameObject("RecoveryOccupiedAI");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);Set(ai,"showAILogs",true);
    var matchHost=new UnityEngine.GameObject("RecoveryOccupiedMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,TurnNumber=13,MyUnits=new System.Collections.Generic.List<UnitManager>{aircraft,blocker,cargo},EnemyUnits=new System.Collections.Generic.List<UnitManager>(),MyBuildings=new System.Collections.Generic.List<ConstructionManager>{occupiedCity,freeCity},EnemyBuildings=new System.Collections.Generic.List<ConstructionManager>()};
    var occupiedMethod=typeof(AIController).GetMethod("BuildAirOccupied",flags);
    var eval=typeof(AIController).GetMethod("EvaluateAircraftRecoveryReach",flags);
    var approach=typeof(AIController).GetMethod("TryBuildAircraftRecoveryApproachAction",flags);
    var has=typeof(AIController).GetMethod("HasAircraftRecoveryWithinReach",flags);
    object Member(object o,string n){return o.GetType().GetField(n)?.GetValue(o)??o.GetType().GetProperty(n)?.GetValue(o);}
    System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>> Paths(){MovementReachCache.ClearAll();return UnitMovementPathRules.CalcularCaminhosValidos(map,aircraft,aircraft.RemainingMovementPoints,terrain);}
    System.Collections.Generic.HashSet<UnityEngine.Vector3Int> Occupied(){return(System.Collections.Generic.HashSet<UnityEngine.Vector3Int>)occupiedMethod.Invoke(ai,new object[]{aircraft});}
    PlayerAction Approach(){object[] args={aircraft,snapshot,aircraft.CurrentCellPosition,Paths(),Occupied(),null};return(bool)approach.Invoke(ai,args)?(PlayerAction)args[5]:null;}
    if(!Occupied().Contains(occupiedCity.CurrentCellPosition))throw new System.Exception("BuildAirOccupied nao viu Chinook pairando.");
    var action=Approach();if(action==null||action.MoveTo!=freeCity.CurrentCellPosition)throw new System.Exception("Recuperacao nao escolheu cidade livre a 5h: "+action?.DebugLabel);
    // No free tactical recovery: keep the free city at Operational distance.
    Position(aircraft,new UnityEngine.Vector3Int(0,2,0));Set(aircraft,"remainingMovementPoints",3);
    var result=eval.Invoke(ai,new object[]{aircraft,snapshot,aircraft.CurrentCellPosition,Paths(),Occupied()});
    if(!(bool)Member(result,"Found")||Member(result,"Tier").ToString()!="Operational")throw new System.Exception("Ancora ocupada desligou fallback Operacional.");
    action=Approach();if(action==null||action.MoveTo==aircraft.CurrentCellPosition||Occupied().Contains(action.MoveTo))throw new System.Exception("Aproximacao operacional parou no mar ou em celula ocupada.");
    // With only an occupied hospital, no claimed recovery may suppress emergency hover.
    freeCity.gameObject.SetActive(false);ConstructionManager.AllActive.Remove(freeCity);map.SetTile(freeCity.CurrentCellPosition,sea.paletteTile);snapshot.MyBuildings.Remove(freeCity);
    if((bool)has.Invoke(ai,new object[]{aircraft,snapshot,aircraft.CurrentCellPosition,Paths(),Occupied()}))throw new System.Exception("HasRecovery contou cidade ocupada.");
    var landing=new UnityEngine.Vector3Int(0,3,0);map.SetTile(landing,plain.paletteTile);
    object[] hoverArgs={aircraft,snapshot,aircraft.CurrentCellPosition,Paths(),Occupied(),false,null};
    if(!(bool)typeof(AIController).GetMethod("TryBuildAircraftEmergencyHoverAction",flags).Invoke(ai,hoverArgs))throw new System.Exception("Sem recuperacao, hover nao encontrou solo livre.");
    action=(PlayerAction)hoverArgs[6];if(action.MoveTo==aircraft.CurrentCellPosition||Occupied().Contains(action.MoveTo)||!PodePousarSensor.CanLandAtCell(aircraft,map,terrain,action.MoveTo,out _,SensorMovementMode.MoveuAndando))throw new System.Exception("Hover escolheu mar, ocupada ou nao pousavel.");
    if(aircraft.CurrentFuel!=21||aircraft.HasActed||!cargo.IsEmbarked||aircraft.GetDomain()!=Domain.Air)throw new System.Exception("Planejamento alterou estado confirmado.");
    return "PASS: cidade ocupada por Chinook pairando excluida; livre a 5h recebe recuperacao; sem livre Tatica segue Operacional com trecho livre; so ocupada nao conta como recuperacao e libera hover em solo pousavel; carga e estado confirmado preservados. Play nao observado.";

}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
