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
var evacLogs=new System.Collections.Generic.List<string>();
UnityEngine.Application.LogCallback captureLog=(message,stack,type)=>{if(message.Contains("EVAC")||message.Contains("[Repair]")||message.Contains("ferido no anel"))evacLogs.Add(message);};
UnityEngine.Application.logMessageReceived+=captureLog;
try{
    foreach(var mc in UnityEngine.Object.FindObjectsByType<MatchController>()){fogRestore.Add(new System.Collections.Generic.KeyValuePair<MatchController,bool>(mc,(bool)typeof(MatchController).GetField("debugFogOfWarEnabled",flags).GetValue(mc)));Set(mc,"debugFogOfWarEnabled",false);}
    var grid=new UnityEngine.GameObject("CourierTestGrid",typeof(UnityEngine.Grid));grid.transform.position=new UnityEngine.Vector3(10000,10000,0);
    grid.GetComponent<UnityEngine.Grid>().cellLayout=UnityEngine.GridLayout.CellLayout.Hexagon;
    var mapHost=new UnityEngine.GameObject("CourierTestMap",typeof(UnityEngine.Tilemaps.Tilemap));mapHost.transform.SetParent(grid.transform,false);
    var map=mapHost.GetComponent<UnityEngine.Tilemaps.Tilemap>();
    var plain=Asset<TerrainTypeData>("plains");
    for(int x=0;x<10;x++)for(int y=0;y<6;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
    var terrain=UnityEngine.ScriptableObject.CreateInstance<TerrainDatabase>();created.Add(terrain);Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain});
    var soldier=Asset<UnitData>("Soldado");var chinook=Asset<UnitData>("chinook");
    var unitDb=UnityEngine.ScriptableObject.CreateInstance<UnitDatabase>();created.Add(unitDb);Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook});
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
    var first=Building(800001,new UnityEngine.Vector3Int(4,2,0),ConstructionSector.Foxtrot);
    var second=Building(800002,new UnityEngine.Vector3Int(7,2,0),ConstructionSector.Juliet);
    var carrier=Unit(800030,chinook,new UnityEngine.Vector3Int(0,2,0),false);
    var main=Unit(800032,soldier,carrier.CurrentCellPosition,true);var extra=Unit(800033,soldier,carrier.CurrentCellPosition,true);
    var ally=Unit(800016,soldier,new UnityEngine.Vector3Int(3,2,0),false);
    foreach(var u in new[]{main,extra})Set(u,"embarkedTransporter",carrier);
    void Mission(UnitManager u,UnityEngine.Vector3Int cell){Set(u,"aiDesignatedMissionIntent",AIPlanRuntimeIntent.Capture);Set(u,"aiDesignatedMissionTargetCell",cell);}
    Mission(main,first.CurrentCellPosition);Mission(extra,second.CurrentCellPosition);Mission(ally,first.CurrentCellPosition);
    var cargo=new System.Collections.Generic.List<UnitManager>{main,extra};
    Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{
        new UnitTransportSeatRuntime{slotId="passageiros",slotIndex=0,seatIndex=0,embarkedUnit=main,embarkedOnTurn=1},new UnitTransportSeatRuntime{slotId="passageiros",slotIndex=0,seatIndex=1,embarkedUnit=extra,embarkedOnTurn=2}});
    var aiHost=new UnityEngine.GameObject("CourierTestAI");aiHost.SetActive(false);var ai=aiHost.AddComponent<AIController>();
    Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var matchHost=new UnityEngine.GameObject("CourierTestMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();
    Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=0,MyUnits=new System.Collections.Generic.List<UnitManager>{ally}};
    map.tileAnchor=UnityEngine.Vector3.zero;
    var factoryData=Asset<ConstructionData>("fabricaL");
    Set(constructionDb,"constructions",new System.Collections.Generic.List<ConstructionData>{city,factoryData});
    typeof(ConstructionDatabase).GetMethod("RebuildLookup",flags).Invoke(constructionDb,null);first.Apply(city);second.Apply(factoryData); Set(first,"hasInfiniteSuppliesOverride",true);Set(second,"hasInfiniteSuppliesOverride",true);
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    if(ownSlot<0)throw new System.Exception("Sem MatchController ativo para resolver slot Blue.");
    snapshot.AISlotIndex=ownSlot; foreach(var u in units)Set(u,"slotIndex",ownSlot);
    foreach(var b in new[]{first,second}){Set(b,"slotIndex",ownSlot);Set(b,"teamId",TeamId.Blue);Set(b,"currentCapturePoints",b.CapturePointsMax);}
    Set(second,"currentCellPosition",new UnityEngine.Vector3Int(5,2,0));second.transform.position=HexCoordinates.GetCellCenterWorld(map,second.CurrentCellPosition);
    Set(ally,"currentCellPosition",first.CurrentCellPosition);ally.transform.position=HexCoordinates.GetCellCenterWorld(map,first.CurrentCellPosition);ally.gameObject.SetActive(true);Set(ally,"boardTilemap",map);Set(ally,"currentCellPosition",first.CurrentCellPosition);Set(ally,"isDead",false);
    Set(ally,"currentDomain",Domain.Land);Set(ally,"teamId",TeamId.Blue);
    if(!UnitManager.AllActive.Contains(ally))UnitManager.AllActive.Add(ally);UnitOccupancyRules.NotifyUnitOccupancyChanged(ally,first.CurrentCellPosition,first.CurrentCellPosition);
    Set(carrier,"currentDomain",Domain.Air);Set(carrier,"currentHeightLevel",HeightLevel.AirLow);Set(carrier,"teamId",TeamId.Blue);
    Set(main,"currentDomain",Domain.Land);Set(main,"teamId",TeamId.Blue);Set(main,"currentHP",1);
    void Check(bool ok,string why){if(!ok)throw new System.Exception(why);}
    void Place(UnitManager u,UnityEngine.Vector3Int cell){Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,cell,cell);}
    map.ClearAllTiles();for(int x=0;x<22;x++)for(int y=0;y<20;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
    Set(ai,"defenseEnemyRange",2);
    Set(first,"currentCellPosition",new UnityEngine.Vector3Int(1,15,0));first.transform.position=HexCoordinates.GetCellCenterWorld(map,first.CurrentCellPosition);
    snapshot.MyHQ=first;
    var aaa=Asset<UnitData>("aaa");var tank=Asset<UnitData>("tanque B");
    Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,aaa,tank});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    UnitManager Spawn(int id,UnitData data,UnityEngine.Vector3Int cell,bool wounded){
        var u=Unit(id,data,cell,false);u.gameObject.SetActive(true);u.Apply(data);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"currentHP",wounded?3:10);Set(u,"currentFuel",100);Set(u,"isUnderRepair",wounded);Set(u,"isEmbarked",false);Set(u,"hasActed",false);Set(u,"remainingMovementPoints",data.movement);Place(u,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;
    }
    var wounded=Spawn(950038,aaa,new UnityEngine.Vector3Int(2,15,0),true);
    var screen=Spawn(950039,tank,new UnityEngine.Vector3Int(0,15,0),false);
    var enemy=Spawn(950040,chinook,new UnityEngine.Vector3Int(5,15,0),false);Set(enemy,"slotIndex",ownSlot==0?1:0);Set(enemy,"teamId",TeamId.Yellow);
    snapshot.MyUnits=new System.Collections.Generic.List<UnitManager>{wounded,screen};snapshot.EnemyUnits=new System.Collections.Generic.List<UnitManager>{enemy};
    var group=typeof(AIController).GetMethod("GetInitiativeGroup",flags);
    var front=typeof(AIController).GetMethod("IsWoundedInVanguard",flags);
    var within=typeof(AIController).GetMethod("IsWithinOwnHqRing",flags);
    int Group(UnitManager u,bool invading){Set(ai,"_sortSnapshot",snapshot);Set(ai,"_sortIsInvading",invading);snapshot.IsInvading=invading;return(int)group.Invoke(ai,new object[]{u,null,TeamId.Blue});}
    Check(SectorManager.HexDistance(wounded.CurrentCellPosition,first.CurrentCellPosition)==1,"Fixture nao ficou a 1 hex do HQ");
    Check((bool)front.Invoke(ai,new object[]{wounded,snapshot}),"Fixture nao confirmou ferido na vanguarda pela massa inimiga perto da base");
    Check(Group(wounded,false)==5,"Ferido em casa foi antecipado pela vanguarda");
    Check(Group(wounded,true)==5,"Invasao antecipou ferido no anel do HQ");
    Check(evacLogs.Exists(log=>log.Contains("#950038")&&log.Contains("ferido no anel do HQ")),"Setup nao explicou prioridade no log");
    Place(wounded,new UnityEngine.Vector3Int(4,15,0));
    Check(!(bool)within.Invoke(ai,new object[]{wounded.CurrentCellPosition,snapshot}),"Fixture externo ainda esta no anel");
    Check((bool)front.Invoke(ai,new object[]{wounded,snapshot}),"Fixture externo nao ficou na vanguarda");
    Check(Group(wounded,false)==1,"Ferido fora do anel na vanguarda perdeu grupo 1");
    // A healthy unit follows exactly its former grouping, even at the HQ.
    Place(wounded,new UnityEngine.Vector3Int(2,15,0));Set(wounded,"isUnderRepair",false);
    int healthyAtHome=Group(wounded,false);
    snapshot.MyHQ=null;int healthyWithoutRing=Group(wounded,false);
    Check(healthyAtHome==healthyWithoutRing&&healthyAtHome!=5,"Saudavel recebeu prioridade de reparo no anel");
    snapshot.MyHQ=first;
    Set(wounded,"isUnderRepair",true);Set(wounded,"isEmbarked",true);
    Check(Group(wounded,true)==1,"Excecao de casa alterou unidade embarcada");
    Set(wounded,"isEmbarked",false);
    // The home rule is absolute, including older early promotions.
    var plan=new TeamObjectivePlan{SlotIndex=ownSlot,Team=TeamId.Blue};plan.HandoffVacaterIds.Add(wounded.InstanceId);
    Check((int)group.Invoke(ai,new object[]{wounded,plan,TeamId.Blue})==5,"Promocao de handoff sobrepos reparo no anel");
    Check(wounded.CurrentHP==3&&!wounded.HasActed,"Consulta de iniciativa alterou estado confirmado");
    return "PASS: ferido a 1 hex do HQ com vanguarda confirmada vai ao grupo 5; invasao tambem; fora do anel vanguarda segue grupo 1; saudavel preservado; embarcado nao recebe excecao; log com id e sem mutacao. Fora do Play.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
