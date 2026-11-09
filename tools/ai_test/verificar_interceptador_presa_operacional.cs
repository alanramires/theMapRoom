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
    var fighterData=Asset<UnitData>("cacaB");var samData=Asset<UnitData>("sam");var apacheData=Asset<UnitData>("apache");
    Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,fighterData,samData,apacheData});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    // Enlarge the board for a genuine operational-only air target.
    for(int x=0;x<45;x++)for(int y=0;y<12;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
    UnitManager Spawn(int id,UnitData data,int x,int y,bool own){var cell=new UnityEngine.Vector3Int(x,y,0);var u=Unit(id,data,cell,false);u.gameObject.SetActive(true);u.Apply(data);Set(u,"boardTilemap",map);Set(u,"slotIndex",own?ownSlot:ownSlot==0?1:0);Set(u,"teamId",own?TeamId.Blue:TeamId.Yellow);Set(u,"currentCellPosition",cell);Set(u,"isDead",false);Set(u,"isEmbarked",false);Set(u,"hasActed",false);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;}
    var fighter=Spawn(950001,fighterData,2,5,true);var sam=Spawn(950002,samData,4,5,false);var heli=Spawn(950003,chinook,15,5,false);var apache=Spawn(950004,apacheData,15,6,true);
    Set(fighter,"currentDomain",Domain.Air);Set(fighter,"currentHeightLevel",HeightLevel.AirHigh);Set(fighter,"remainingMovementPoints",fighterData.movement);Set(heli,"currentDomain",Domain.Air);Set(heli,"currentHeightLevel",HeightLevel.AirLow);
    var host=new UnityEngine.GameObject("InterceptorOperationalTest");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var query=typeof(AIController).GetMethod("TryResolveInterceptorOperationalPursuit",flags);
    bool Pursuit(UnitManager actor,UnitManager ground,System.Collections.Generic.List<UnitManager> enemies){object[] args={actor,ground,actor.CurrentCellPosition,enemies,null};bool found=(bool)query.Invoke(ai,args);if(found&&(UnitManager)args[4]!=heli)throw new System.Exception("Presa incorreta.");return found;}
    var enemies=new System.Collections.Generic.List<UnitManager>{sam,heli};
    var tactical=UnitReachEnvelopeService.Build(new UnitReachRequest{Unit=fighter,BoardMap=map,TerrainDatabase=terrain,Intent=ReachIntent.Combat,Band=ReachBand.Tactical});
    var operational=UnitReachEnvelopeService.Build(new UnitReachRequest{Unit=fighter,BoardMap=map,TerrainDatabase=terrain,Intent=ReachIntent.Combat,Band=ReachBand.Operational});
    // Pick a real envelope cell instead of assuming geometric distance is reach.
    bool placed=false;for(int x=3;x<44&&!placed;x++){var cell=new UnityEngine.Vector3Int(x,5,0);if(operational.CanAct(cell)&&!tactical.CanAct(cell)){Set(heli,"currentCellPosition",cell);heli.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);placed=true;}}
    if(!placed)throw new System.Exception("Fixture nao encontrou celula so Operacional.");
    if(!Pursuit(fighter,sam,enemies))throw new System.Exception("SAM terciario venceu presa aerea operacional.");
    var paths=UnitMovementPathRules.CalcularCaminhosValidos(map,fighter,fighter.RemainingMovementPoints,terrain);
    var move=(UnityEngine.Vector3Int)typeof(AIController).GetMethod("FindAirCombatAdvanceMove",flags).Invoke(ai,new object[]{fighter.CurrentCellPosition,heli.CurrentCellPosition,paths,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),TeamId.Blue,null,false});
    if(move==fighter.CurrentCellPosition||!paths.ContainsKey(move)||SectorManager.HexDistance(move,heli.CurrentCellPosition)>=SectorManager.HexDistance(fighter.CurrentCellPosition,heli.CurrentCellPosition))throw new System.Exception("Aproximacao nao avancou por um caminho legal.");
    if(Pursuit(fighter,heli,enemies))throw new System.Exception("Ataque aereo tatico foi substituido.");
    if(Pursuit(apache,sam,enemies))throw new System.Exception("Disruptor foi desviado para interceptacao.");
    if(Pursuit(fighter,sam,new System.Collections.Generic.List<UnitManager>{sam}))throw new System.Exception("Sem presa suprimiu tiro terrestre.");
    Set(heli,"isEmbarked",true);if(Pursuit(fighter,sam,enemies))throw new System.Exception("Embarcado virou presa.");Set(heli,"isEmbarked",false);
    Set(heli,"currentDomain",Domain.Land);if(Pursuit(fighter,sam,enemies))throw new System.Exception("Aeronave no solo virou presa aerea.");Set(heli,"currentDomain",Domain.Air);
    Set(heli,"currentCellPosition",new UnityEngine.Vector3Int(44,11,0));if(Pursuit(fighter,sam,enemies))throw new System.Exception("Fora do Operacional virou presa.");
    if(fighter.HasActed||fighter.CurrentCellPosition!=new UnityEngine.Vector3Int(2,5,0))throw new System.Exception("Consulta confirmou acao.");
    return "PASS: interceptador prefere presa aerea no Operacional ao SAM terciario; preserva alvo aereo tatico, disruptor e tiro sem presa; recusa embarcado, pousado e fora do envelope; sem acao confirmada.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
