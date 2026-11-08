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
try{
    var grid=new UnityEngine.GameObject("CourierTestGrid",typeof(UnityEngine.Grid));grid.transform.position=new UnityEngine.Vector3(10000,10000,0);
    grid.GetComponent<UnityEngine.Grid>().cellLayout=UnityEngine.GridLayout.CellLayout.Hexagon;
    var mapHost=new UnityEngine.GameObject("CourierTestMap",typeof(UnityEngine.Tilemaps.Tilemap));mapHost.transform.SetParent(grid.transform,false);
    var map=mapHost.GetComponent<UnityEngine.Tilemaps.Tilemap>();map.tileAnchor=UnityEngine.Vector3.zero;
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
        new UnitTransportSeatRuntime{embarkedUnit=main,embarkedOnTurn=1},new UnitTransportSeatRuntime{embarkedUnit=extra,embarkedOnTurn=2}});
    var aiHost=new UnityEngine.GameObject("CourierTestAI");aiHost.SetActive(false);var ai=aiHost.AddComponent<AIController>();
    Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var matchHost=new UnityEngine.GameObject("CourierTestMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();
    Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=0,MyUnits=new System.Collections.Generic.List<UnitManager>{ally}};
    map.tileAnchor=UnityEngine.Vector3.zero;
    var factory=Asset<ConstructionData>("fabricaL");
    Set(constructionDb,"constructions",new System.Collections.Generic.List<ConstructionData>{city,factory});
    typeof(ConstructionDatabase).GetMethod("RebuildLookup",flags).Invoke(constructionDb,null);
    first.Apply(factory);
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    if(ownSlot<0)throw new System.Exception("Fixture sem slot Blue.");
    Set(first,"slotIndex",ownSlot);Set(first,"teamId",TeamId.Blue);Set(first,"currentCapturePoints",first.CapturePointsMax);
    first.DebugSetSellingRule(ConstructionUnitMarketRule.FreeMarket,ownSlot);
    snapshot.AISlotIndex=ownSlot;
    var cell=first.CurrentCellPosition;
    carrier.gameObject.SetActive(true);ally.gameObject.SetActive(true);
    foreach(var u in new[]{carrier,ally}){
        Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"isEmbarked",false);
        Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);
        if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);
    }
    Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>());
    Set(carrier,"currentDomain",Domain.Air);Set(carrier,"currentHeightLevel",HeightLevel.AirLow);
    Set(carrier,"currentHP",5);Set(carrier,"currentFuel",15);Set(carrier,"isUnderRepair",true);
    Set(ally,"currentDomain",Domain.Land);Set(ally,"currentHeightLevel",HeightLevel.Surface);Set(ally,"currentHP",10);Set(ally,"remainingMovementPoints",3);
    UnitOccupancyRules.NotifyUnitOccupancyChanged(carrier,cell,cell);
    bool Reserved(){return(bool)typeof(AIController).GetMethod("IsAircraftReservingRepairLanding",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{carrier});}
    var aiEndMove=typeof(AIController).GetMethod("CanAIUnitEndMoveAtCell",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
    var buildAiOccupied=typeof(AIController).GetMethod("BuildOccupied",flags);
    if(!Reserved())throw new System.Exception("Nao reservou pouso para manutencao no produtor.");
    if(!OccupancyResolver.CanEndMove(ally,cell,new[]{carrier})
        ||!OccupancyResolver.CanEndMoveAsLayer(ally,Domain.Land,HeightLevel.Surface,new[]{carrier})
        ||!OccupancyResolver.CanEndLayerTransition(ally,Domain.Land,HeightLevel.Surface,new[]{carrier}))
        throw new System.Exception("Politica de IA alterou regra de movimento/pouso do humano.");
    if((bool)aiEndMove.Invoke(null,new object[]{ally,cell,new[]{carrier}}))throw new System.Exception("Guarda final da IA ignorou reserva.");
    if(!((System.Collections.Generic.HashSet<UnityEngine.Vector3Int>)buildAiOccupied.Invoke(ai,new object[]{ally,true})).Contains(cell))
        throw new System.Exception("Filtro de destinos da IA nao contem pouso reservado.");
    var injected=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
    typeof(AIController).GetMethod("AddRepairLandingReservedCells",flags).Invoke(ai,new object[]{injected,ally});
    if(!injected.Contains(cell))throw new System.Exception("Injecao da reserva no conjunto de destinos falhou.");
    injected.Clear();typeof(AIController).GetMethod("AddRepairLandingReservedCells",flags).Invoke(ai,new object[]{injected,carrier});
    if(injected.Contains(cell))throw new System.Exception("Reserva bloqueou a propria aeronave no filtro da IA.");
    if(!OccupancyResolver.CanPassThrough(ally,carrier,cell))throw new System.Exception("Reserva bloqueou travessia.");
    if(!OccupancyResolver.CanEndLayerTransition(carrier,Domain.Land,HeightLevel.Surface,new[]{carrier}))throw new System.Exception("Aeronave bloqueou o proprio pouso.");
    var approachCell=new UnityEngine.Vector3Int(3,2,0);Set(ally,"currentCellPosition",approachCell);ally.transform.position=HexCoordinates.GetCellCenterWorld(map,approachCell);UnitOccupancyRules.NotifyUnitOccupancyChanged(ally,cell,approachCell);
    var buildOccupied=typeof(AIShoppingPlanner).GetMethod("BuildProductionOccupiedCells",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
    if(!((System.Collections.Generic.HashSet<UnityEngine.Vector3Int>)buildOccupied.Invoke(null,null)).Contains(cell))throw new System.Exception("Shopping nao reservou produtor.");
    var stateHost=new UnityEngine.GameObject("LandingReservationShoppingTest");stateHost.SetActive(false);var state=stateHost.AddComponent<TurnStateManager>();
    Set(state,"terrainTilemap",map);Set(state,"terrainDatabase",terrain);
    var spawnCheck=typeof(TurnStateManager).GetMethod("IsShoppingSpawnCellBlocked",flags);
    foreach(var purchase in new[]{soldier,chinook}){
        object[] shoppingArgs={first,purchase,cell,ownSlot,null};
        if((bool)spawnCheck.Invoke(state,shoppingArgs))throw new System.Exception("Politica de IA bloqueou compra do humano.");
    }
    Set(ally,"currentCellPosition",cell);ally.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(ally,approachCell,cell);
    var vacate=typeof(AIController).GetMethod("TryFindProductionUnlockVacateAction",flags);
    object[] vacateArgs={ally,snapshot,null};
    if(!(bool)vacate.Invoke(ai,vacateArgs)||vacateArgs[2] is not PlayerAction action||action.MoveTo==cell)
        throw new System.Exception("Unidade sob aeronave nao liberou solo mesmo sem caixa de shopping.");
    if((bool)vacate.Invoke(ai,new object[]{carrier,snapshot,null}))throw new System.Exception("Expulsou a propria aeronave em manutencao.");

    if(carrier.CurrentHP!=5||carrier.CurrentFuel!=15f||ally.HasActed)throw new System.Exception("Planejamento alterou estado confirmado.");
    // Reservation automatically disappears when repair state ends or aircraft leaves.
    Set(carrier,"isUnderRepair",false);
    if(Reserved()||!OccupancyResolver.CanEndMove(ally,cell,new[]{carrier}))
        throw new System.Exception("Aeronave sem manutencao manteve reserva.");
    if(!(bool)aiEndMove.Invoke(null,new object[]{ally,cell,new[]{carrier}}))throw new System.Exception("Guarda da IA manteve reserva encerrada.");
    var away=new UnityEngine.Vector3Int(2,2,0);Set(ally,"currentCellPosition",away);ally.transform.position=HexCoordinates.GetCellCenterWorld(map,away);
    if(((System.Collections.Generic.HashSet<UnityEngine.Vector3Int>)buildOccupied.Invoke(null,null)).Contains(cell))throw new System.Exception("Shopping manteve reserva encerrada.");
    Set(carrier,"isUnderRepair",true);Set(carrier,"currentHP",10);Set(carrier,"currentFuel",carrier.GetMaxFuel());
    if(Reserved())throw new System.Exception("Flag antigo com aeronave ja atendida manteve reserva.");
    Set(carrier,"currentHP",5);Set(carrier,"currentFuel",15);Set(carrier,"currentCellPosition",away);
    carrier.transform.position=HexCoordinates.GetCellCenterWorld(map,away);
    if(Reserved())throw new System.Exception("Aeronave fora de predio reservou terreno.");
    return "PASS: politica apenas da IA: movimento/pouso/compra do humano preservados; BuildOccupied e guarda final da IA reservam pouso; shopping IA evita produtor; permite cruzar e pousar o dono; ocupante sai sem exigir shopping; aeronave nao e expulsa; reserva libera ao terminar, atender ou sair.";
}finally{
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
