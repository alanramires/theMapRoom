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
    var action=Decide(paths);
    if(action.SensorAction!=SensorActionType.Disembark||action.SubSteps.Count!=1||action.SubSteps[0].TargetHex!=second.CurrentCellPosition)
        throw new System.Exception("Nao entregou na produtora livre: "+action.DebugLabel);
    if(action.MoveTo==second.CurrentCellPosition||SectorManager.HexDistance(action.MoveTo,second.CurrentCellPosition)!=1f)
        throw new System.Exception("Helicoptero nao ficou ao lado do predio de reparo.");
    // One repair vacancy: only the primary leaves; secondary remains aboard.
    Set(extra,"currentDomain",Domain.Land);Set(extra,"teamId",TeamId.Blue);
    Set(extra,"currentHP",1);Set(extra,"isUnderRepair",true);
    var pairPerf=typeof(AIController).Assembly.GetType("AIDecisionPerf");
    evacLogs.Clear();pairPerf.GetMethod("Begin").Invoke(null,null);
    var pairDrop=Decide(paths);
    var pairCounters=(System.Collections.Generic.Dictionary<string,long>)pairPerf.GetField("Counters",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
    if(!pairCounters.TryGetValue("EvacRepairCellsBuilds",out long builds)||builds!=1
        ||!pairCounters.TryGetValue("RepairConstructionRankings",out long ranks)||ranks!=2)
        throw new System.Exception("Entrega reconstruiu repairCells/ranking por ferido.");
    pairPerf.GetMethod("End").Invoke(null,null);
    if(evacLogs.Exists(message=>message.Contains("[Repair] skip")))throw new System.Exception("Consulta auxiliar imprimiu logs de skip.");
    if(pairDrop.SensorAction!=SensorActionType.Disembark||pairDrop.SubSteps.Count!=1
        ||pairDrop.SubSteps[0].TargetInstanceId!=main.InstanceId.ToString()
        ||pairDrop.SubSteps[0].TargetHex!=second.CurrentCellPosition||!extra.IsEmbarked)
        throw new System.Exception("Uma vaga de reparo deveria reter o secundario a bordo.");
    // After the primary has left, the remaining wounded owns the next delivery.
    Set(main,"isEmbarked",false);
    var nextDelivery=(PlayerAction)method.Invoke(ai,new object[]{carrier,extra,snapshot,carrier.CurrentCellPosition,paths,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>()});
    if(nextDelivery.SensorAction!=SensorActionType.Disembark||nextDelivery.SubSteps.Count!=1
        ||nextDelivery.SubSteps[0].TargetInstanceId!=extra.InstanceId.ToString()
        ||nextDelivery.SubSteps[0].TargetHex!=second.CurrentCellPosition)
        throw new System.Exception("Ferido retido nao assumiu entrega seguinte no hospital elegivel.");
    Set(main,"isEmbarked",true);
    // Two free hospitals next to a common LZ: prefer both in repair.
    ally.gameObject.SetActive(false);UnitOccupancyRules.NotifyUnitOccupancyChanged(ally,first.CurrentCellPosition,first.CurrentCellPosition);
    var twoHospitals=Decide(paths);
    if(twoHospitals.SubSteps.Count!=2||!twoHospitals.SubSteps.Exists(step=>step.TargetHex==first.CurrentCellPosition)
        ||!twoHospitals.SubSteps.Exists(step=>step.TargetHex==second.CurrentCellPosition))
        throw new System.Exception("Nao preferiu dois hospitais livres em vez de soltar no chao.");
    ally.gameObject.SetActive(true);Set(ally,"boardTilemap",map);Set(ally,"currentCellPosition",first.CurrentCellPosition);Set(ally,"isDead",false);
    UnitOccupancyRules.NotifyUnitOccupancyChanged(ally,first.CurrentCellPosition,first.CurrentCellPosition);
    Set(extra,"currentHP",10);Set(extra,"isUnderRepair",false);
    var healthyCargo=Decide(paths);
    if(healthyCargo.SubSteps.Count!=1||healthyCargo.SubSteps[0].TargetInstanceId!=main.InstanceId.ToString())
        throw new System.Exception("Passageiro saudavel foi descarregado pelo EVAC.");
    // A ranked hospital may exist while no current endpoint advances to it.
    // A theoretical future LZ must not suppress the HQ fallback.
    var homeBefore=first.CurrentCellPosition;
    var producerBefore=second.CurrentCellPosition;
    var carrierBefore=carrier.CurrentCellPosition;
    var nearHq=new UnityEngine.Vector3Int(2,2,0);
    Set(first,"currentCellPosition",nearHq);first.transform.position=HexCoordinates.GetCellCenterWorld(map,nearHq);
    snapshot.MyHQ=first;
    var noProgressPaths=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>{
        {carrierBefore,new System.Collections.Generic.List<UnityEngine.Vector3Int>{carrierBefore}}};
    var stalledHospital=Decide(noProgressPaths);
    if(stalledHospital.SensorAction!=SensorActionType.Disembark||stalledHospital.SubSteps.Count!=1)
        throw new System.Exception("Hospital estimado sem progresso suprimiu fallback materializavel no HQ.");
    snapshot.MyHQ=null;
    Set(first,"currentCellPosition",homeBefore);first.transform.position=HexCoordinates.GetCellCenterWorld(map,homeBefore);

    // The top hospital is left, but only a rightward step exists: use the next hospital.
    var leftHospital=new UnityEngine.Vector3Int(0,2,0);
    var rightHospital=new UnityEngine.Vector3Int(9,2,0);
    var middle=new UnityEngine.Vector3Int(4,2,0);
    var rightStep=new UnityEngine.Vector3Int(6,2,0);
    Set(first,"currentCellPosition",leftHospital);first.transform.position=HexCoordinates.GetCellCenterWorld(map,leftHospital);
    Set(second,"currentCellPosition",rightHospital);second.transform.position=HexCoordinates.GetCellCenterWorld(map,rightHospital);
    Set(carrier,"currentCellPosition",middle);carrier.transform.position=HexCoordinates.GetCellCenterWorld(map,middle);
    var rightPaths=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>{
        {middle,new System.Collections.Generic.List<UnityEngine.Vector3Int>{middle}},
        {rightStep,new System.Collections.Generic.List<UnityEngine.Vector3Int>{middle,new UnityEngine.Vector3Int(5,2,0),rightStep}}};
    var alternativeHospital=Decide(rightPaths);
    if(alternativeHospital.SensorAction!=SensorActionType.None||alternativeHospital.MoveTo!=rightStep)
        throw new System.Exception("Nao pulou hospital sem progresso para aproximar de outro hospital.");
    Set(first,"currentCellPosition",homeBefore);first.transform.position=HexCoordinates.GetCellCenterWorld(map,homeBefore);
    Set(second,"currentCellPosition",producerBefore);second.transform.position=HexCoordinates.GetCellCenterWorld(map,producerBefore);
    Set(carrier,"currentCellPosition",carrierBefore);carrier.transform.position=HexCoordinates.GetCellCenterWorld(map,carrierBefore);
    second.DebugSetSellingRule(ConstructionUnitMarketRule.FreeMarket,ownSlot);
    Set(second,"sector",ConstructionSector.Base0);Set(ai,"conscriptionDoctrine",true);
    if(!(bool)typeof(AIController).GetMethod("IsConscriptionClosedProducerConstruction",flags).Invoke(ai,new object[]{second,TeamId.Blue}))
        throw new System.Exception("Fixture nao fechou produtor Army do cluster da base: owner="+second.SlotIndex+" slot="+ownSlot+" sector="+second.Sector+" canProduce="+second.CanProduceUnitsForSlot(ownSlot)+" offers="+second.OfferedUnits.Count);
    var conscription=Decide(paths);
    if(conscription.SensorAction!=SensorActionType.None||conscription.MoveTo!=carrier.CurrentCellPosition)
        throw new System.Exception("EVAC ignorou conscricao no produtor da base.");
    Set(ai,"conscriptionDoctrine",false);Set(second,"sector",ConstructionSector.Juliet);
    Set(main,"receivedSuppliesThisTurn",true);
    var alreadyServed=Decide(paths);
    if(alreadyServed.SensorAction!=SensorActionType.None||alreadyServed.MoveTo!=carrier.CurrentCellPosition)
        throw new System.Exception("EVAC ignorou paciente ja atendido nesta rodada.");
    Set(main,"receivedSuppliesThisTurn",false);
    Set(second,"sector",ConstructionSector.Base0);
    var pressureEnemy=Unit(800102,soldier,second.CurrentCellPosition,false);Set(pressureEnemy,"slotIndex",1);Set(pressureEnemy,"teamId",TeamId.Yellow);
    snapshot.EnemyUnits.Add(pressureEnemy);
    var pressure=Decide(paths);
    if(pressure.SensorAction!=SensorActionType.None||pressure.MoveTo!=carrier.CurrentCellPosition)
        throw new System.Exception("EVAC nao-elite ignorou bloqueio da base sob pressao.");
    snapshot.EnemyUnits.Remove(pressureEnemy);Set(second,"sector",ConstructionSector.Juliet);
    var shortPaths=UnitMovementPathRules.CalcularCaminhosValidos(map,carrier,1,terrain);
    var perfType=typeof(AIController).Assembly.GetType("AIDecisionPerf");
    perfType.GetMethod("Begin").Invoke(null,null);
    var approach=Decide(shortPaths);
    string metrics=(string)perfType.GetMethod("End").Invoke(null,null);
    if(metrics.Contains("AirEvacDisembarkProbes"))throw new System.Exception("Aproximacao distante executou sensor de desembarque: "+metrics);
    if(approach.SensorAction!=SensorActionType.None||approach.MoveTo==carrier.CurrentCellPosition||approach.MoveTo==first.CurrentCellPosition||approach.MoveTo==second.CurrentCellPosition)
        throw new System.Exception("Nao aproximou mantendo carga fora dos predios.");
    Set(ally,"currentCellPosition",second.CurrentCellPosition);ally.transform.position=HexCoordinates.GetCellCenterWorld(map,second.CurrentCellPosition);
    var freeCity=Decide(paths);
    if(freeCity.SensorAction!=SensorActionType.Disembark||freeCity.SubSteps[0].TargetHex!=first.CurrentCellPosition)
        throw new System.Exception("Nao trocou de predio quando a fabrica ficou ocupada.");
    var blocker=Unit(800101,soldier,first.CurrentCellPosition,false);
    blocker.gameObject.SetActive(true);Set(blocker,"boardTilemap",map);Set(blocker,"slotIndex",ownSlot);
    Set(blocker,"currentCellPosition",first.CurrentCellPosition);Set(blocker,"isDead",false);Set(blocker,"currentDomain",Domain.Land);
    UnitOccupancyRules.NotifyUnitOccupancyChanged(blocker,first.CurrentCellPosition,first.CurrentCellPosition);
    var noDelivery=Decide(paths);
    if(noDelivery.SensorAction!=SensorActionType.None||noDelivery.MoveTo!=carrier.CurrentCellPosition)
        throw new System.Exception("Sem hospital nem HQ de referencia nao pode inventar destino.");
    snapshot.MyHQ=first;
    var hqDrop=Decide(paths);
    int ringRange=ai.DefenseEnemyRange;
    if(hqDrop.SensorAction!=SensorActionType.Disembark||hqDrop.SubSteps.Count!=1
        ||SectorManager.HexDistance(hqDrop.SubSteps[0].TargetHex,first.CurrentCellPosition)>ringRange
        ||hqDrop.SubSteps[0].TargetHex==first.CurrentCellPosition||hqDrop.SubSteps[0].TargetHex==second.CurrentCellPosition)
        throw new System.Exception("Hospitais ocupados nao liberaram paciente no anel seguro do HQ.");
    var invadedNeighbors=new System.Collections.Generic.List<UnityEngine.Vector3Int>();
    UnitMovementPathRules.GetImmediateHexNeighbors(map,first.CurrentCellPosition,invadedNeighbors);
    var invasion=invadedNeighbors[0];
    Set(extra,"currentHP",1);Set(extra,"isUnderRepair",true);
    pairPerf.GetMethod("Begin").Invoke(null,null);
    var twoInRing=Decide(paths);
    if(!pairCounters.TryGetValue("EvacRepairCellsBuilds",out builds)||builds!=1
        ||!pairCounters.TryGetValue("RepairConstructionRankings",out ranks)||ranks!=2)
        throw new System.Exception("Fallback HQ reconstruiu o mapa de reparo da mesma decisao.");
    pairPerf.GetMethod("End").Invoke(null,null);
    if(twoInRing.SensorAction!=SensorActionType.Disembark||twoInRing.SubSteps.Count!=2
        ||twoInRing.SubSteps[0].TargetHex==twoInRing.SubSteps[1].TargetHex)
        throw new System.Exception("Fallback do HQ nao descarregou os dois feridos.");
    // The second patient need not fit the primary's exact accepted ring.
    // Reuse real sensor options, with only one accepted primary cell.
    var ringOptions=new System.Collections.Generic.List<PodeDesembarcarOption>();
    PodeDesembarcarSensor.CollectOptionsFromCell(carrier,twoInRing.MoveTo,map,terrain,ringOptions,out _);
    var primaryStep=twoInRing.SubSteps.Find(step=>step.TargetInstanceId==main.InstanceId.ToString());
    var secondaryStep=twoInRing.SubSteps.Find(step=>step.TargetInstanceId==extra.InstanceId.ToString());
    var primaryOption=ringOptions.Find(option=>option.passengerUnit==main&&option.disembarkCell==primaryStep.TargetHex);
    var secondaryOption=ringOptions.Find(option=>option.passengerUnit==extra&&option.disembarkCell==secondaryStep.TargetHex);
    if(primaryOption==null||secondaryOption==null)throw new System.Exception("Fixture sem opcoes reais para dupla no HQ.");
    var oneCellRing=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>{primaryStep.TargetHex};
    var pairFacts=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>{
        {main.InstanceId,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>()},
        {extra.InstanceId,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>()}};
    Set(main,"currentHP",4);Set(extra,"currentHP",4);
    var relaxed=(System.Collections.Generic.List<PodeDesembarcarOption>)typeof(AIController).GetMethod("MatchEvacPatients",flags).Invoke(ai,
        new object[]{main,primaryStep.TargetHex,new System.Collections.Generic.List<PodeDesembarcarOption>{primaryOption,secondaryOption},snapshot,pairFacts,oneCellRing,true});
    if(relaxed.Count!=2||oneCellRing.Contains(secondaryOption.disembarkCell))
        throw new System.Exception("Contorno estrito do anel reteve secundario que pode completar a pe.");
    // Safe ring: an external spot near an enemy must be rejected.
    // Contested ring: the same still-materializable option may accept risk.
    var edgeNeighbors=new System.Collections.Generic.List<UnityEngine.Vector3Int>();
    UnitMovementPathRules.GetImmediateHexNeighbors(map,secondaryOption.disembarkCell,edgeNeighbors);
    var edgeCell=edgeNeighbors.Find(cell=>cell!=primaryOption.disembarkCell&&map.HasTile(cell));
    var edgeEnemy=Unit(800104,soldier,edgeCell,false);
    Set(edgeEnemy,"slotIndex",ownSlot==0?1:0);Set(edgeEnemy,"teamId",TeamId.Yellow);
    UnitManager.AllActive.Add(edgeEnemy);snapshot.EnemyUnits.Add(edgeEnemy);
    var matchMethod=typeof(AIController).GetMethod("MatchEvacPatients",flags);
    var edgeOptions=new System.Collections.Generic.List<PodeDesembarcarOption>{primaryOption,secondaryOption};
    var safeEdge=(System.Collections.Generic.List<PodeDesembarcarOption>)matchMethod.Invoke(ai,
        new object[]{main,primaryStep.TargetHex,edgeOptions,snapshot,pairFacts,oneCellRing,true});
    var contestedEdge=(System.Collections.Generic.List<PodeDesembarcarOption>)matchMethod.Invoke(ai,
        new object[]{main,primaryStep.TargetHex,edgeOptions,snapshot,pairFacts,oneCellRing,false});
    if(safeEdge.Count!=1||safeEdge[0].passengerUnit!=main||contestedEdge.Count!=2)
        throw new System.Exception("Celula externa ameacada nao distinguiu anel seguro de contestado.");
    UnitManager.AllActive.Remove(edgeEnemy);snapshot.EnemyUnits.Remove(edgeEnemy);
    Set(main,"currentHP",1);Set(extra,"currentHP",10);Set(extra,"isUnderRepair",false);
    var danger=Unit(800103,soldier,invasion,false);
    Set(danger,"slotIndex",ownSlot==0?1:0);Set(danger,"teamId",TeamId.Yellow);
    UnitManager.AllActive.Add(danger);snapshot.EnemyUnits.Add(danger);
    var unsafeRing=Decide(paths);
    if(unsafeRing.SensorAction!=SensorActionType.Disembark
        ||SectorManager.HexDistance(unsafeRing.SubSteps[0].TargetHex,first.CurrentCellPosition)>ringRange)
        throw new System.Exception("Base invadida prendeu paciente em vez de entregar no anel.");
    if(!evacLogs.Exists(message=>message.Contains("anel contestado, menor ameaça")))
        throw new System.Exception("Nao registrou degrau contestado.");
    var lastResort=typeof(AIController).GetMethod("DecideAirEvacHqFallback",flags);
    var hotMap=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,float>();
    for(int x=-4;x<15;x++)for(int y=-4;y<11;y++)hotMap[new UnityEngine.Vector3Int(x,y,0)]=100f;
    var leastThreatCell=unsafeRing.SubSteps[0].TargetHex;
    hotMap[leastThreatCell]=1f;
    var contested=(PlayerAction)lastResort.Invoke(ai,new object[]{carrier,main,snapshot,carrier.CurrentCellPosition,paths,
        new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),
        new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<PodeDesembarcarOption>>(),hotMap,true,
        new System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>{{main.InstanceId,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>()}}});
    if(contested.SensorAction!=SensorActionType.Disembark||contested.SubSteps[0].TargetHex!=leastThreatCell)
        throw new System.Exception("Fallback nao entregou na celula livre com menor ameaca, aceitando rota contestada.");
    UnitManager.AllActive.Remove(danger);snapshot.EnemyUnits.Remove(danger);

    var farHq=new UnityEngine.Vector3Int(9,5,0);
    Set(first,"currentCellPosition",farHq);first.transform.position=HexCoordinates.GetCellCenterWorld(map,farHq);
    Set(blocker,"currentCellPosition",farHq);blocker.transform.position=HexCoordinates.GetCellCenterWorld(map,farHq);
    UnitOccupancyRules.NotifyUnitOccupancyChanged(blocker,farHq,farHq);
    perfType.GetMethod("Begin").Invoke(null,null);
    var farApproach=Decide(shortPaths);
    string farMetrics=(string)perfType.GetMethod("End").Invoke(null,null);
    if(farApproach.SensorAction!=SensorActionType.None||farApproach.MoveTo==carrier.CurrentCellPosition
        ||SectorManager.HexDistance(farApproach.MoveTo,farHq)>=SectorManager.HexDistance(carrier.CurrentCellPosition,farHq)
        ||farMetrics.Contains("AirEvacDisembarkProbes"))
        throw new System.Exception("HQ distante nao recebeu aproximacao segura sem sondagem remota.");

    var origin=carrier.CurrentCellPosition;
    var hot=new UnityEngine.Vector3Int(1,2,0);
    var beyond=new UnityEngine.Vector3Int(2,2,0);
    var safeEnd=new UnityEngine.Vector3Int(0,3,0);
    hotMap[carrier.CurrentCellPosition]=200f;
    var hotEscape=(PlayerAction)lastResort.Invoke(ai,new object[]{carrier,main,snapshot,carrier.CurrentCellPosition,shortPaths,
        new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),
        new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<PodeDesembarcarOption>>(),hotMap,true,
        new System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>{{main.InstanceId,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>()}}});
    if(hotEscape.SensorAction==SensorActionType.None&&hotEscape.MoveTo==carrier.CurrentCellPosition)
        throw new System.Exception("Fallback devolveu espera em origem ameacada com rotas disponiveis.");
    // Uniform danger on every step: accumulated exposure must not shrink a
    // full tactical flight to one hex when peak danger is identical.
    var distantHq=new UnityEngine.Vector3Int(20,5,0);
    for(int x=10;x<23;x++)for(int y=0;y<8;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
    Set(first,"currentCellPosition",distantHq);first.transform.position=HexCoordinates.GetCellCenterWorld(map,distantHq);
    var uniformRisk=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,float>();
    for(int x=-4;x<27;x++)for(int y=-4;y<13;y++)uniformRisk[new UnityEngine.Vector3Int(x,y,0)]=30f;
    var fullAdvance=(PlayerAction)lastResort.Invoke(ai,new object[]{carrier,main,snapshot,carrier.CurrentCellPosition,paths,
        new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),
        new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<PodeDesembarcarOption>>(),uniformRisk,true,
        new System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>{{main.InstanceId,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>()}}});
    int travelled=fullAdvance.MovementPath!=null?fullAdvance.MovementPath.Count-1:0;
    if(fullAdvance.SensorAction!=SensorActionType.None||travelled<carrier.RemainingMovementPoints
        ||SectorManager.HexDistance(fullAdvance.MoveTo,distantHq)>=SectorManager.HexDistance(carrier.CurrentCellPosition,distantHq)-1)
        throw new System.Exception("Ameaca uniforme fez aproximacao ao HQ economizar movimento: passos="+travelled);
    Set(first,"currentCellPosition",farHq);first.transform.position=HexCoordinates.GetCellCenterWorld(map,farHq);
    var routeFixture=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>{
        {origin,new System.Collections.Generic.List<UnityEngine.Vector3Int>{origin}},
        {hot,new System.Collections.Generic.List<UnityEngine.Vector3Int>{origin,hot}},
        {beyond,new System.Collections.Generic.List<UnityEngine.Vector3Int>{origin,hot,beyond}},
        {safeEnd,new System.Collections.Generic.List<UnityEngine.Vector3Int>{origin,safeEnd}}};
    var threats=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,float>{{origin,0},{hot,100},{beyond,0},{safeEnd,0}};
    var safeRoutes=(System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>)typeof(AIController).GetMethod("BuildSafeAirEvacPaths",flags).Invoke(ai,new object[]{origin,routeFixture,TeamId.Blue,threats});
    if(safeRoutes.ContainsKey(hot)||safeRoutes.ContainsKey(beyond)||!safeRoutes.ContainsKey(safeEnd))
        throw new System.Exception("Rota atravessou ameaca para chegar a destino seguro, ou perdeu alternativa segura.");
    // Terrestrial EVAC: parking/selection share the entry-point repair map.
    var apc=Asset<UnitData>("apc");
    Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,apc});
    typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    Set(carrier,"unitId",apc.id);Set(carrier,"currentDomain",Domain.Land);Set(carrier,"currentHeightLevel",HeightLevel.Surface);
    Set(extra,"currentHP",1);Set(extra,"isUnderRepair",true);
    Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{
        new UnitTransportSeatRuntime{slotId=apc.transportSlots[0].slotId,slotIndex=0,seatIndex=0,embarkedUnit=main}});
    var groundPaths=UnitMovementPathRules.CalcularCaminhosValidos(map,carrier,carrier.RemainingMovementPoints,terrain);
    pairPerf.GetMethod("Begin").Invoke(null,null);
    typeof(AIController).GetMethod("DecideEvacCourierAction",flags).Invoke(ai,new object[]{carrier,main,new System.Collections.Generic.List<UnitManager>{main,extra},snapshot,carrier.CurrentCellPosition,groundPaths,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>()});
    if(!pairCounters.TryGetValue("EvacRepairCellsBuilds",out builds)||builds!=1
        ||!pairCounters.TryGetValue("RepairConstructionRankings",out ranks)||ranks!=1)
        throw new System.Exception("EVAC terrestre reconstruiu repairCells/ranking na mesma decisao: builds="+builds+" ranks="+ranks);
    pairPerf.GetMethod("End").Invoke(null,null);
    if(!main.IsEmbarked||main.CurrentHP!=1)throw new System.Exception("Planejamento alterou passageiro confirmado.");
    return "PASS: cidade ocupada descartada; entrega direta na fabrica livre a partir de LZ vizinha; alcance curto aproxima mantendo carga; nova ocupacao troca destino; sem hospital nem HQ de referencia mantem carga; conscricao e pressao respeitadas; paciente ja atendido rejeitado; aproximacao sem sondagem distante; fallback descarrega no anel HQ e aproxima quando longe; base invadida entrega no anel contestado; todas as rotas ameacadas ainda entregam; origem ameacada nao espera; caminho com ameaca rejeitado mesmo com destino seguro; uma vaga retem secundario, que assume entrega seguinte; dois hospitais entregam dupla; repairCells unica no aereo, HQ e terrestre; ranking auxiliar silencioso; dupla no anel HQ com secundario fora do contorno e trecho tatico a pe; celula externa com inimigo perto rejeitada no seguro e aceita no contestado; hospital sem progresso tenta outro e depois HQ; ameaca uniforme usa deslocamento tatico completo rumo ao HQ; saudavel permanece a bordo; passageiro inalterado.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
