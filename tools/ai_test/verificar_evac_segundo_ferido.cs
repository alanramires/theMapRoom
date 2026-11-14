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
UnityEngine.Application.LogCallback captureLog=(message,stack,type)=>{if(message.Contains("EVAC")||message.Contains("[Repair]"))evacLogs.Add(message);};
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
    var paths=UnitMovementPathRules.CalcularCaminhosValidos(map,carrier,carrier.RemainingMovementPoints,terrain);
    var method=typeof(AIController).GetMethod("DecideAirEvacDeliveryAction",flags);
    PlayerAction Decide(System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>> reach){return(PlayerAction)method.Invoke(ai,new object[]{carrier,main,snapshot,carrier.CurrentCellPosition,reach,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>()});}
    void Check(bool ok,string reason){if(!ok)throw new System.Exception(reason);}
    void Place(UnitManager u,UnityEngine.Vector3Int cell){Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,cell,cell);}
    void Site(ConstructionManager b,UnityEngine.Vector3Int cell){Set(b,"currentCellPosition",cell);b.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);}
    map.ClearAllTiles();for(int x=0;x<18;x++)for(int y=0;y<18;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
    var heavy=Asset<ConstructionData>("fabricaP");
    Set(constructionDb,"constructions",new System.Collections.Generic.List<ConstructionData>{city,factoryData,heavy});typeof(ConstructionDatabase).GetMethod("RebuildLookup",flags).Invoke(constructionDb,null);
    first.Apply(factoryData);Site(first,new UnityEngine.Vector3Int(2,14,0));
    second.Apply(heavy);Site(second,new UnityEngine.Vector3Int(3,13,0));
    var occupiedFactory=Building(800300,new UnityEngine.Vector3Int(1,14,0),ConstructionSector.Delta);occupiedFactory.Apply(factoryData);
    var hq=Building(800301,new UnityEngine.Vector3Int(1,15,0),ConstructionSector.Base0);hq.Apply(city);
    foreach(var b in new[]{first,second,occupiedFactory,hq}){Set(b,"slotIndex",ownSlot);Set(b,"teamId",TeamId.Blue);Set(b,"currentCapturePoints",b.CapturePointsMax);b.SetInfiniteSuppliesOverride(true);}
    ((ConstructionSiteRuntime)typeof(ConstructionManager).GetField("siteRuntime",flags).GetValue(hq)).isPlayerHeadQuarter=true;
    Place(ally,occupiedFactory.CurrentCellPosition);
    Place(carrier,new UnityEngine.Vector3Int(5,9,0));
    foreach(var p in new[]{main,extra}){Set(p,"teamId",TeamId.Blue);Set(p,"currentDomain",Domain.Land);Set(p,"currentHP",1);Set(p,"isUnderRepair",true);Set(p,"isEmbarked",true);Set(p,"receivedSuppliesThisTurn",false);Place(p,carrier.CurrentCellPosition);}
    snapshot.MyHQ=hq;snapshot.EnemyUnits=new System.Collections.Generic.List<UnitManager>();
    Set(ai,"defenseEnemyRange",2);
    var lz=new UnityEngine.Vector3Int(2,13,0);
    // Actual legal route crossing a visible threat; landing cells themselves stay safe.
    var allPaths=UnitMovementPathRules.CalcularCaminhosValidos(map,carrier,carrier.RemainingMovementPoints,terrain);
    Check(allPaths.ContainsKey(lz),"LZ da marca nao esta no Tatico do Chinook");
    var onlyLz=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>{
        {carrier.CurrentCellPosition,new System.Collections.Generic.List<UnityEngine.Vector3Int>{carrier.CurrentCellPosition}}, {lz,allPaths[lz]}};
    var threatCells=(System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<UnityEngine.Vector3Int>>)typeof(AIController).GetField("aiThreatEnemyCellsBySlot",flags).GetValue(ai);
    var route=allPaths[lz];
    var threatCell=new UnityEngine.Vector3Int(6,9,0);
    // Seed the existing threat-reading cache only, avoiding unrelated scene enemies.
    threatCells[ownSlot]=new System.Collections.Generic.List<UnityEngine.Vector3Int>{threatCell};
    var perf=typeof(AIController).Assembly.GetType("AIDecisionPerf");perf.GetMethod("Begin").Invoke(null,null);
    evacLogs.Clear();var delivered=Decide(onlyLz);
    var counts=(System.Collections.Generic.Dictionary<string,long>)perf.GetField("Counters",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
    Check(counts.TryGetValue("EvacRouteThreatBuilds",out long risks)&&risks==1,"Mapa de ameaca de rota reconstruido na mesma decisao");perf.GetMethod("End").Invoke(null,null);
    Check(delivered.SensorAction==SensorActionType.Disembark&&delivered.SubSteps.Count==2,"Nao entregou os dois feridos");
    Check(delivered.SubSteps.Exists(step=>step.TargetInstanceId==main.InstanceId.ToString()&&step.TargetHex==first.CurrentCellPosition),"Principal nao foi para Fabrica (2,14)");
    Check(delivered.SubSteps.Exists(step=>step.TargetInstanceId==extra.InstanceId.ToString()&&step.TargetHex==second.CurrentCellPosition),"Secundario nao foi para Fabrica Pesada (3,13)");
    Check(!delivered.SubSteps.Exists(step=>step.TargetHex==occupiedFactory.CurrentCellPosition),"Usou Fabrica Leve ocupada");
    Check(evacLogs.Exists(log=>log.Contains("EVAC hospital menor ameaca"))&&!evacLogs.Exists(log=>log.Contains("EVAC anel seguro: libera")),"Entrou no anel antes de hospital pela rota contestada");
    // Directly exercise the former Resolve veto with the actual sensor options.
    var options=new System.Collections.Generic.List<PodeDesembarcarOption>();PodeDesembarcarSensor.CollectOptionsFromCell(carrier,lz,map,terrain,options,out _);
    var build=typeof(AIController).GetMethod("BuildEvacRepairCells",flags);
    var facts=(System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>)build.Invoke(ai,new object[]{carrier,main,snapshot,carrier.CurrentCellPosition,null});
    var ring=facts.GetType().GetField("HqRing").GetValue(facts);
    var cells=(System.Collections.Generic.HashSet<UnityEngine.Vector3Int>)ring.GetType().GetField("Cells").GetValue(ring);
    Check(!cells.Contains(second.CurrentCellPosition)&&SectorManager.HexDistance(second.CurrentCellPosition,hq.CurrentCellPosition)==3,"Hospital da borda nao ficou a 3 do HQ fora do anel");
    var matching=typeof(AIController).GetMethod("MatchEvacPatients",flags);
    var matched=(System.Collections.Generic.List<PodeDesembarcarOption>)matching.Invoke(ai,new object[]{main,first.CurrentCellPosition,options,snapshot,facts,cells,true});
    Check(matched.Exists(option=>option.passengerUnit==extra&&option.disembarkCell==second.CurrentCellPosition),"Resolve ainda vetou hospital na borda fora do anel");
    // Healthy secondary uses ground, leaving the free hospital for a wounded unit.
    Set(extra,"currentHP",10);Set(extra,"isUnderRepair",false);evacLogs.Clear();
    var healthy=Decide(onlyLz);
    Check(healthy.SensorAction==SensorActionType.Disembark&&healthy.SubSteps.Count==2,"Saudavel nao acompanhou entrega valida no HQ");
    Check(healthy.SubSteps.Exists(step=>step.TargetInstanceId==extra.InstanceId.ToString()&&ConstructionOccupancyRules.GetConstructionAtCell(map,step.TargetHex)==null),"Saudavel ocupou hospital");
    // No repair service anywhere: both still leave via the HQ fallback.
    Set(extra,"currentHP",1);Set(extra,"isUnderRepair",true);
    Set(main,"receivedSuppliesThisTurn",true);Set(extra,"receivedSuppliesThisTurn",true);
    evacLogs.Clear();var fallback=Decide(onlyLz);
    Check(fallback.SensorAction==SensorActionType.Disembark&&fallback.SubSteps.Count==2,"Sem hospital reteve feridos em vez de entregar no anel");
    Check(evacLogs.Exists(log=>log.Contains("EVAC anel seguro: libera")),"Sem hospital nao acionou anel");
    Check(fallback.SubSteps.Exists(step=>step.TargetInstanceId==extra.InstanceId.ToString()&&ConstructionOccupancyRules.GetConstructionAtCell(map,step.TargetHex)==null),"Secundario sem hospital nao usou chao");
    Set(main,"receivedSuppliesThisTurn",false);Set(extra,"receivedSuppliesThisTurn",false);
    // Unsafe second hospital is refused without losing the safe ground option.
    var enemy=Unit(800302,soldier,new UnityEngine.Vector3Int(4,12,0),false);Set(enemy,"slotIndex",ownSlot==0?1:0);Set(enemy,"teamId",TeamId.Yellow);UnitManager.AllActive.Add(enemy);snapshot.EnemyUnits.Add(enemy);
    Check((bool)typeof(AIController).GetMethod("IsEvacDropCellSafe",flags).Invoke(ai,new object[]{first.CurrentCellPosition,TeamId.Blue}),"Fixture tornou o hospital principal inseguro");
    Check(!(bool)typeof(AIController).GetMethod("IsEvacDropCellSafe",flags).Invoke(ai,new object[]{second.CurrentCellPosition,TeamId.Blue}),"Fixture nao ameacou hospital secundario");
    var unsafeFacts=(System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>)build.Invoke(ai,new object[]{carrier,main,snapshot,carrier.CurrentCellPosition,null});
    // Pin the primary's known safe ring cell; geometry and sensor legality remain real.
    var unsafeMatch=(System.Collections.Generic.List<PodeDesembarcarOption>)matching.Invoke(ai,new object[]{main,first.CurrentCellPosition,options,snapshot,unsafeFacts,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>{first.CurrentCellPosition},true});
    Check(!unsafeMatch.Exists(option=>option.passengerUnit==extra&&option.disembarkCell==second.CurrentCellPosition),"Aceitou hospital inseguro");
    Check(unsafeMatch.Exists(option=>option.passengerUnit==extra&&ConstructionOccupancyRules.GetConstructionAtCell(map,option.disembarkCell)==null),"Hospital inseguro impediu chao seguro");
    Check(main.IsEmbarked&&extra.IsEmbarked&&main.CurrentHP==1&&extra.CurrentHP==1,"Preview alterou carga/HP");
    // Affinity outranks a shorter flight: factory direct (0) beats airport direct (2).
    UnitManager.AllActive.Remove(enemy);snapshot.EnemyUnits.Remove(enemy);
    threatCells[ownSlot]=new System.Collections.Generic.List<UnityEngine.Vector3Int>();
    snapshot.MyHQ=null;Set(hq,"currentCapturePoints",0);Set(occupiedFactory,"currentCapturePoints",0);
    Set(extra,"isEmbarked",false);
    var airportData=UnityEngine.Object.Instantiate(city);created.Add(airportData);airportData.id="evac_affinity_airport";airportData.isCity=false;airportData.isAirport=true;airportData.isHarbor=false;
    airportData.constructionConfiguration.offeredUnits=new System.Collections.Generic.List<UnitData>{chinook};
    Set(constructionDb,"constructions",new System.Collections.Generic.List<ConstructionData>{city,factoryData,heavy,airportData});typeof(ConstructionDatabase).GetMethod("RebuildLookup",flags).Invoke(constructionDb,null);
    second.Apply(airportData);Set(second,"slotIndex",ownSlot);Set(second,"teamId",TeamId.Blue);Set(second,"currentCapturePoints",second.CapturePointsMax);second.SetInfiniteSuppliesOverride(true);
    var shortLz=new UnityEngine.Vector3Int(3,12,0);
    Check(allPaths.ContainsKey(shortLz)&&allPaths[lz].Count==allPaths[shortLz].Count+1,"Fixture nao deu um passo de vantagem ao aeroporto");
    var affinityPaths=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>{
        {carrier.CurrentCellPosition,new System.Collections.Generic.List<UnityEngine.Vector3Int>{carrier.CurrentCellPosition}},
        {shortLz,allPaths[shortLz]},{lz,allPaths[lz]}};
    var preferred=Decide(affinityPaths);
    Check(preferred.SensorAction==SensorActionType.Disembark&&preferred.SubSteps.Count==1
        &&preferred.SubSteps[0].TargetHex==first.CurrentCellPosition,"Aeroporto com rota menor venceu Fabrica preferida");
    // Same-LZ matching: walking to own-force hospital beats direct other-force service.
    var affinityFacts=(System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>)build.Invoke(ai,new object[]{carrier,main,snapshot,carrier.CurrentCellPosition,null});
    var preference=typeof(AIController).GetMethod("ResolveEvacDisembarkPreference",flags);
    int Cost(UnityEngine.Vector3Int cell)=>(int)preference.Invoke(ai,new object[]{main,cell,snapshot,affinityFacts,true});
    var ground=new UnityEngine.Vector3Int(1,13,0);
    Check(Cost(first.CurrentCellPosition)==0&&Cost(ground)==1&&Cost(second.CurrentCellPosition)==2,"Nao restaurou degraus proprio direto/a pe e outro direto");
    Set(extra,"isEmbarked",true);
    affinityFacts=(System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>)build.Invoke(ai,new object[]{carrier,main,snapshot,carrier.CurrentCellPosition,null});
    var tiedOptions=new System.Collections.Generic.List<PodeDesembarcarOption>();PodeDesembarcarSensor.CollectOptionsFromCell(carrier,lz,map,terrain,tiedOptions,out _);
    tiedOptions.RemoveAll(option=>option.passengerUnit==main?option.disembarkCell!=first.CurrentCellPosition
        :option.passengerUnit!=extra||(option.disembarkCell!=ground&&option.disembarkCell!=second.CurrentCellPosition));
    Check(tiedOptions.Exists(option=>option.passengerUnit==extra&&option.disembarkCell==ground)
        &&tiedOptions.Exists(option=>option.passengerUnit==extra&&option.disembarkCell==second.CurrentCellPosition),"Sem opcoes reais para empate afinidade");
    var affinityMatch=(System.Collections.Generic.List<PodeDesembarcarOption>)matching.Invoke(ai,new object[]{main,first.CurrentCellPosition,tiedOptions,snapshot,affinityFacts,null,false});
    Check(affinityMatch.Count==2&&affinityMatch.Exists(option=>option.passengerUnit==extra&&option.disembarkCell==ground),"Matching preferiu outro ramo direto a proprio ramo a pe");
    return "PASS: marca #4 (2,14) e #25 (3,13), Fabrica Leve ocupada excluida; hospital por menor ameaca antes do anel, risco montado uma vez; Resolve aceita hospital fora do anel; sem atendimento usa chao; saudavel nao ocupa hospital; hospital inseguro recusado; Fabrica vence Aeroporto com voo um passo menor; proprio a pe vence outro direto. Fora do Play.";

}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
