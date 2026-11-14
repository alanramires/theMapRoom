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
    var airportData=UnityEngine.Object.Instantiate(city);airportData.id="test_repair_airport";airportData.isCity=false;airportData.isAirport=true;airportData.isHarbor=false;created.Add(airportData);
    airportData.constructionConfiguration.offeredUnits=new System.Collections.Generic.List<UnitData>{chinook};
    var navalData=UnityEngine.Object.Instantiate(soldier);navalData.id="test_navy_patient";navalData.militaryForce=MilitaryForce.Navy;created.Add(navalData);
    var harborData=UnityEngine.Object.Instantiate(city);harborData.id="test_repair_harbor";harborData.isCity=false;harborData.isAirport=false;harborData.isHarbor=true;created.Add(harborData);
    harborData.constructionConfiguration.offeredUnits=new System.Collections.Generic.List<UnitData>{navalData};
    Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,navalData});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    Set(constructionDb,"constructions",new System.Collections.Generic.List<ConstructionData>{city,factoryData,airportData,harborData});typeof(ConstructionDatabase).GetMethod("RebuildLookup",flags).Invoke(constructionDb,null);
    var airport=Building(800201,new UnityEngine.Vector3Int(0,2,0),ConstructionSector.Charlie);airport.Apply(airportData);
    var harbor=Building(800202,new UnityEngine.Vector3Int(9,5,0),ConstructionSector.Delta);harbor.Apply(harborData);
    foreach(var building in new[]{airport,harbor}){Set(building,"slotIndex",ownSlot);Set(building,"teamId",TeamId.Blue);Set(building,"currentCapturePoints",building.CapturePointsMax);building.SetInfiniteSuppliesOverride(true);}
    var navy=Unit(800203,navalData,new UnityEngine.Vector3Int(8,5,0),false);
    var affinity=typeof(AIController).GetMethod("IsPreferredRepairFacility",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
    bool Preferred(UnitManager unit,ConstructionManager building)=>(bool)affinity.Invoke(null,new object[]{unit,building});
    if(!Preferred(main,first)||!Preferred(main,second)||Preferred(main,airport)||Preferred(main,harbor))throw new System.Exception("Afinidade Army incorreta");
    if(!Preferred(carrier,airport)||!Preferred(carrier,harbor)||Preferred(carrier,first)!=city.allowAircraftTakeoffAndLanding)throw new System.Exception("Afinidade Aeronautica incorreta");
    if(!Preferred(navy,harbor)||Preferred(navy,first)||Preferred(navy,airport))throw new System.Exception("Afinidade Marinha incorreta");
    var sectors=new UnityEngine.GameObject("RepairFacilitySectors").AddComponent<SectorManager>();SectorManager.RebuildNowFromActiveConstructions(scene,"facility-test");
    Set(main,"isEmbarked",false);main.gameObject.SetActive(true);Set(main,"boardTilemap",map);Set(main,"currentCellPosition",airport.CurrentCellPosition);main.transform.position=HexCoordinates.GetCellCenterWorld(map,airport.CurrentCellPosition);
    Set(main,"slotIndex",ownSlot);Set(main,"teamId",TeamId.Blue);Set(main,"currentDomain",Domain.Land);Set(main,"currentHP",1);Set(main,"isUnderRepair",true);Set(main,"remainingMovementPoints",soldier.movement);Set(main,"isDead",false);
    if(!UnitManager.AllActive.Contains(main))UnitManager.AllActive.Add(main);UnitOccupancyRules.NotifyUnitOccupancyChanged(main,main.CurrentCellPosition,main.CurrentCellPosition);
    snapshot.MyUnits=new System.Collections.Generic.List<UnitManager>{main,ally};snapshot.EnemyUnits=new System.Collections.Generic.List<UnitManager>();
    var repair=typeof(AIController).GetMethod("DecideUnderRepairAction",flags);
    Set(main,"receivedSuppliesThisTurn",true); // Fase 1 ja atendeu; capacidade da instalacao permanece.
    var distantStay=(PlayerAction)repair.Invoke(ai,new object[]{main,snapshot});
    if(distantStay.MoveTo!=airport.CurrentCellPosition)throw new System.Exception("D2: saiu de aeroporto que atende para preferido fora do Tatico");
    var factoryFar=second.CurrentCellPosition;
    Set(second,"currentCellPosition",new UnityEngine.Vector3Int(2,2,0));second.transform.position=HexCoordinates.GetCellCenterWorld(map,second.CurrentCellPosition);
    UnitReachEnvelopeService.ClearCache();
    var leave=(PlayerAction)repair.Invoke(ai,new object[]{main,snapshot});
    if(leave.MoveTo==airport.CurrentCellPosition||leave.ActionType!=PlayerActionType.UnitAction)throw new System.Exception("Soldado ficou curando no aeroporto apesar de fabrica apta: "+leave.DebugLabel);
    Set(second,"currentCellPosition",factoryFar);second.transform.position=HexCoordinates.GetCellCenterWorld(map,factoryFar);
    UnitReachEnvelopeService.ClearCache();
    Set(main,"receivedSuppliesThisTurn",false);
    // Current facility unable to serve: an eligible preferred facility may be distant.
    airport.SetInfiniteSuppliesOverride(false);Set(airport,"currentCapturePoints",0);
    var cannotServe=(PlayerAction)repair.Invoke(ai,new object[]{main,snapshot});
    if(cannotServe.MoveTo==airport.CurrentCellPosition)throw new System.Exception("D2: reteve paciente em instalacao sem atendimento");
    airport.SetInfiniteSuppliesOverride(true);Set(airport,"currentCapturePoints",airport.CapturePointsMax);
    // Ranking: occupied HQ and defensive reservations are last-resort tier 2.
    Set(first,"currentCapturePoints",0);Set(harbor,"currentCapturePoints",0);
    ((ConstructionSiteRuntime)typeof(ConstructionManager).GetField("siteRuntime",flags).GetValue(second)).isPlayerHeadQuarter=true;
    var rank=typeof(AIController).GetMethod("RankRepairConstructions",flags);
    System.Collections.Generic.List<ConstructionManager> Ranked(System.Collections.Generic.HashSet<UnityEngine.Vector3Int> occupied,System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,int> reserved=null)
        =>(System.Collections.Generic.List<ConstructionManager>)rank.Invoke(ai,new object[]{main,main.CurrentCellPosition,TeamId.Blue,occupied,false,reserved,snapshot,false,false});
    var occupiedRank=Ranked(new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>{second.CurrentCellPosition});
    if(occupiedRank.Count<2||occupiedRank[0]!=airport||!occupiedRank.Contains(second))throw new System.Exception("HQ ocupado passou aeroporto livre ou deixou de ser fallback");
    var reservedRank=Ranked(new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,int>{{second.CurrentCellPosition,ally.InstanceId}});
    if(reservedRank.Count<2||reservedRank[0]!=airport)throw new System.Exception("Reserva defensiva ganhou camada preferida");
    ((ConstructionSiteRuntime)typeof(ConstructionManager).GetField("siteRuntime",flags).GetValue(second)).isPlayerHeadQuarter=false;
    // Preferred beyond Operational competes on score with a suitable other-force facility.
    Set(second,"currentCellPosition",new UnityEngine.Vector3Int(9,5,0));second.transform.position=HexCoordinates.GetCellCenterWorld(map,second.CurrentCellPosition);
    UnitReachEnvelopeService.ClearCache();
    var farRank=Ranked(new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>());
    if(farRank.Count<2||farRank[0]!=airport)throw new System.Exception("D1: preferido fora do Operacional ganhou camada 0");
    Set(first,"currentCapturePoints",0);Set(second,"currentCapturePoints",0);Set(harbor,"currentCapturePoints",0);
    var stay=(PlayerAction)repair.Invoke(ai,new object[]{main,snapshot});
    if(stay.MoveTo!=airport.CurrentCellPosition)throw new System.Exception("Sem alternativa segura deveria manter atendimento existente");
    return "PASS: afinidade pelos tres ramos; civil pousavel; fabrica/QG/Army contra aeroporto; porto/naval; HQ ocupado e reserva defensiva sao ultimo recurso; preferido distante perde camada 0; aeroporto atendendo, inclusive apos servico da Fase 1, so libera para preferido Tatico e sem atendimento libera para distante. Decisao real, fora do Play.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
