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
    var sf=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var oldUnits=new System.Collections.Generic.List<UnitManager>(UnitManager.AllActive);var oldBuildings=new System.Collections.Generic.List<ConstructionManager>(ConstructionManager.AllActive);
    UnitManager.AllActive.Clear();ConstructionManager.AllActive.Clear();
    var aiInstance=typeof(AIController).GetField("_instance",sf);var oldAi=aiInstance.GetValue(null);
    var objectiveField=typeof(ObjectiveManager).GetField("instance",sf);var oldObjective=objectiveField.GetValue(null);
    var queueField=typeof(AIShoppingPlanner).GetField("shoppingRideQueue",sf);var oldQueue=queueField.GetValue(null);var queueSnapshotField=typeof(AIShoppingPlanner).GetField("shoppingRideSnapshot",sf);var oldQueueSnapshot=queueSnapshotField.GetValue(null);
    try{
        void Check(bool yes,string why){if(!yes)throw new System.Exception(why);}
        var sea=Asset<TerrainTypeData>("sea");Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain,forest,sea});
        for(int x=0;x<42;x++)for(int y=0;y<13;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),sea.paletteTile);
        for(int x=0;x<6;x++)for(int y=0;y<10;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
        for(int x=18;x<41;x++)for(int y=0;y<12;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
        var matchHost=new UnityEngine.GameObject("FleetMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);
        var players=(System.Collections.IList)typeof(MatchController).GetField("players",flags).GetValue(match);var player=players[ownSlot];player.GetType().GetField("teamId",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(player,TeamId.Blue);players[ownSlot]=player;
        var host=new UnityEngine.GameObject("FleetAI");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);Set(ai,"matchController",match);Set(ai,"hardMode",false);aiInstance.SetValue(null,ai);
        var omHost=new UnityEngine.GameObject("FleetObjectives");omHost.SetActive(false);var om=omHost.AddComponent<ObjectiveManager>();objectiveField.SetValue(null,om);
        var plan=new TeamObjectivePlan{SlotIndex=ownSlot,Team=TeamId.Blue};Set(om,"plans",new System.Collections.Generic.List<TeamObjectivePlan>{plan});
        UnitManager Spawn(int id,UnitData d,UnityEngine.Vector3Int cell){var u=Unit(id,d,cell,false);u.gameObject.SetActive(true);u.Apply(d);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"isUnderRepair",false);Set(u,"hasActed",false);Set(u,"currentFuel",60);Set(u,"remainingMovementPoints",d.movement);Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,cell,cell);return u;}
        var bus1=Spawn(970001,chinook,new UnityEngine.Vector3Int(2,10,0));var bus2=Spawn(970002,chinook,new UnityEngine.Vector3Int(3,10,0));
        var waiting=new System.Collections.Generic.List<UnitManager>();var targets=new System.Collections.Generic.List<ConstructionManager>();
        for(int i=0;i<6;i++){
            var cell=new UnityEngine.Vector3Int(20+i*3,5,0);var building=Building(971000+i,cell,(ConstructionSector)(i+1));building.Apply(city);Set(building,"slotIndex",-1);Set(building,"currentCellPosition",cell);targets.Add(building);
            var u=Spawn(972000+i,soldier,new UnityEngine.Vector3Int(1+i%3,1+i/3,0));u.SetAIDesignatedMission(AIPlanRuntimeIntent.Capture,cell,targetConstructionInstanceId:building.InstanceId);u.PublishAIRideNeed(true,15);waiting.Add(u);
            if(i<3)plan.Objectives.Add(new SectorObjective{Sector=building.Sector,ObjectiveType=AIObjectiveType.CaptureSector,Slots=new System.Collections.Generic.List<SlotNeed>{new SlotNeed{Role=UnitRole.Capturador,Filled=true,AssignedUnitId=u.InstanceId}}});else plan.RogueUnitIds.Add(u.InstanceId);
        }
        var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,TurnNumber=16,Budget=20000,IncomePerTurn=5000,Stance=AIStance.Tactical,MyUnits=new System.Collections.Generic.List<UnitManager>(units),EnemyUnits=new System.Collections.Generic.List<UnitManager>(),MyBuildings=new System.Collections.Generic.List<ConstructionManager>(),EnemyBuildings=new System.Collections.Generic.List<ConstructionManager>()};
        var q=PressaoCaronaService.Read(ai,ownSlot,16);
        Check(q.PersistentAirWaiting==6&&q.AirOperationalSeats==4&&q.AirCongested,"Fila real esperava 6 persistentes e 4 bancos: "+q.PersistentAirWaiting+"/"+q.AirOperationalSeats+" pedidos="+q.Pedidos.Count+" invalidos="+q.UnserviceableAirWaiting);
        Check(q.Pedidos.Exists(p=>p.Categoria==PressaoCaronaCategoria.Rogue&&p.UsefulAirDelivery),"Rogue util nao contou.");
        // Filled seats are still one-trip capacity, while repair makes the entire bus unavailable.
        var cargo=Spawn(972100,soldier,bus1.CurrentCellPosition);Set(cargo,"isEmbarked",true);Set(cargo,"embarkedTransporter",bus1);Set(bus1,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{embarkedUnit=cargo,slotIndex=0,seatIndex=0}});
        var loaded=PressaoCaronaService.Read(ai,ownSlot,16);Check(loaded.AirOperationalSeats==4&&loaded.AirSeatsFree==3,"Carga reduziu capacidade por viagem.");
        Set(bus2,"isUnderRepair",true);var repair=PressaoCaronaService.Read(ai,ownSlot,16);Check(repair.AirOperationalSeats==2,"Onibus em reparo contou como capacidade.");Set(bus2,"isUnderRepair",false);
        var fresh=PressaoCaronaService.Read(ai,ownSlot,15);Check(fresh.PersistentAirWaiting==0&&!fresh.AirCongested,"Fila recem formada congestionou compra.");
        // Terrain with no ground destination must not justify more helicopters.
        for(int x=18;x<41;x++)for(int y=0;y<12;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),sea.paletteTile);
        MovementReachCache.ClearAll(); // capturaveis continuam, mas suas ilhas nao tem LZ adjacente.
        var noDelivery=PressaoCaronaService.Read(ai,ownSlot,16);Check(noDelivery.PersistentAirWaiting==0&&!noDelivery.AirCongested,"Entrega impossivel pediu frota.");
        // Same published queue fact drives both the demand brake and the mass/tax brake.
        void Publish(PressaoCaronaLeitura fact){queueSnapshotField.SetValue(null,snapshot);queueField.SetValue(null,fact);}
        Publish(q);
        var expansion=new AIShoppingDemand{Role=UnitRole.Capturador,Count=8,Priority=30,Origin="composition",Reason="expansao"};var defense=new AIShoppingDemand{Role=UnitRole.Capturador,Count=1,Priority=1,Origin="operation",Reason="defesa concreta",IsConcreteDefense=true};var ds=new System.Collections.Generic.List<AIShoppingDemand>{expansion,defense};
        typeof(AIShoppingPlanner).GetMethod("ApplyRideQueueCapturerBrake",sf).Invoke(null,new object[]{snapshot,ds,false});Check(expansion.Count==0&&defense.Count==1,"Freio apagou defesa ou manteve expansao.");
        snapshot.Stance=AIStance.Defensive;expansion.Count=8;typeof(AIShoppingPlanner).GetMethod("ApplyRideQueueCapturerBrake",sf).Invoke(null,new object[]{snapshot,ds,false});Check(expansion.Count==0,"Postura Defensiva burlou freio.");
        var producer=Building(973000,new UnityEngine.Vector3Int(4,2,0),ConstructionSector.Base0);Set(producer,"matchController",match);Set(producer,"slotIndex",ownSlot);Set(producer,"teamId",TeamId.Blue);Set(producer,"siteRuntime",new ConstructionSiteRuntime{isCapturable=true,capturePointsMax=20,sellingRule=ConstructionUnitMarketRule.FreeMarket,offeredUnits=new System.Collections.Generic.List<UnitData>{soldier}});Set(producer,"currentCapturePoints",20);snapshot.MyBuildings.Add(producer);
        var occupied=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();var tax=typeof(AIShoppingPlanner).GetMethod("ComputeConscriptionTax",sf,null,new[]{typeof(AIWorldSnapshot),occupied.GetType()},null);Check((int)tax.Invoke(null,new object[]{snapshot,occupied})==0,"Soldado bloqueado pagou imposto.");
        var orders=new System.Collections.Generic.List<AIShoppingPlanner.ShoppingOrder>();object[] buyArgs={snapshot,orders,new System.Collections.Generic.HashSet<ConstructionManager>(),occupied,10000,producer};typeof(AIShoppingPlanner).GetMethod("BuySurgeMassBody",sf).Invoke(null,buyArgs);Check(orders.Count==0,"Conscricao ignorou congestionamento.");
        Publish(new PressaoCaronaLeitura{PersistentAirWaiting=4,AirOperationalSeats=4});Check((int)tax.Invoke(null,new object[]{snapshot,occupied})==1000,"Producao nao voltou com fila nao congestionada.");
        var pressure=new AIShoppingPlanner.OperationalPressureInspection{RideQueue=q,RideSeatsPerAirBus=2};ds.Clear();typeof(AIShoppingPlanner).GetMethod("AddOperationalPressureDemands",sf).Invoke(null,new object[]{snapshot,ds,pressure});
        var ride=ds.Find(d=>d.IsRideQueueReinforcement);Check(ride!=null&&ride.Count==1&&!ride.Urgent&&!ride.StrategicEscalation&&ride.Priority<30,"Reforco nao e unico, normal ou prioritario sobre expansao.");
        // Normal purchase covers queue; queue purchase covers one normal transport need.
        var operation=new AIShoppingDemand{Role=UnitRole.Transportador,Domain=Domain.Air,Count=2,Priority=14,Origin="operation",Reason="airlift"};
        ds=new System.Collections.Generic.List<AIShoppingDemand>{operation,ride};var cartType=typeof(AIShoppingPlanner).GetNestedType("RoleShoppingCart",System.Reflection.BindingFlags.NonPublic);var candidateType=typeof(AIShoppingPlanner).GetNestedType("RoleShoppingCandidate",System.Reflection.BindingFlags.NonPublic);
        object Cart(){var c=System.Activator.CreateInstance(cartType);cartType.GetField("RemainingDemand").SetValue(c,new[]{2,1});cartType.GetField("CoveredDemand").SetValue(c,new[]{false,false});return c;}
        object Candidate(int index){var c=System.Activator.CreateInstance(candidateType);candidateType.GetField("Unit").SetValue(c,chinook);candidateType.GetField("Demand").SetValue(c,ds[index]);candidateType.GetField("DemandIndex").SetValue(c,index);return c;}
        var add=typeof(AIShoppingPlanner).GetMethod("AddCandidateToRoleShoppingCart",sf);var cart=Cart();add.Invoke(null,new object[]{cart,Candidate(0),ds});Check(((int[])cartType.GetField("RemainingDemand").GetValue(cart))[1]==0,"Compra de operacao nao cobriu fila.");
        cart=Cart();add.Invoke(null,new object[]{cart,Candidate(1),ds});Check(((int[])cartType.GetField("RemainingDemand").GetValue(cart))[0]==1,"Reforco nao cobriu operacao.");
        // Real shopping beam: an operation hull covers the queue, and savings remain protected.
        foreach(var b in new[]{producer})Set(b,"siteRuntime",new ConstructionSiteRuntime{isCapturable=true,capturePointsMax=20,sellingRule=ConstructionUnitMarketRule.FreeMarket,offeredUnits=new System.Collections.Generic.List<UnitData>{chinook}});
        for(int n=0;n<2;n++){var b=Building(973001+n,new UnityEngine.Vector3Int(4,3+n,0),ConstructionSector.Base0);Set(b,"matchController",match);Set(b,"slotIndex",ownSlot);Set(b,"teamId",TeamId.Blue);Set(b,"siteRuntime",new ConstructionSiteRuntime{isCapturable=true,capturePointsMax=20,sellingRule=ConstructionUnitMarketRule.FreeMarket,offeredUnits=new System.Collections.Generic.List<UnitData>{chinook}});Set(b,"currentCapturePoints",20);snapshot.MyBuildings.Add(b);}
        var beam=typeof(AIShoppingPlanner).GetMethod("BuildBestRoleShoppingCart",sf);
        int BuyCount(System.Collections.Generic.List<AIShoppingDemand> needs,int freeBudget){var c=beam.Invoke(null,new object[]{snapshot,needs,new AIShoppingPlanner.CounterPressureInspection(),occupied,freeBudget,false,false,false,0,null,null});return c==null?0:((System.Collections.IList)cartType.GetField("Items").GetValue(c)).Count;}
        Publish(q);
        Check(BuyCount(new System.Collections.Generic.List<AIShoppingDemand>{ride},20000)==1,"Fila sozinha comprou mais de um reforco.");
        Check(BuyCount(ds,20000)==2,"Operacao comprou reforco adicional duplicado.");
        Check(BuyCount(new System.Collections.Generic.List<AIShoppingDemand>{ride},chinook.cost-1)==0,"Fila gastou reserva fora do saldo livre.");
        var generic=new AIShoppingDemand{Role=UnitRole.None,Domain=Domain.Air,Count=2,Priority=5,Origin="counter",Reason="outra porta"};
        Check(BuyCount(new System.Collections.Generic.List<AIShoppingDemand>{generic,ride},20000)==2,"Compra por outra porta duplicou reforco.");
        Check(typeof(AIShoppingPlanner).GetMethod("FindReserveBreakingEmergency",sf).Invoke(null,new object[]{new System.Collections.Generic.List<AIShoppingDemand>{ride}})==null,"Fila virou emergencia que rompe poupanca.");
        Check(!waiting[0].IsEmbarked&&waiting[0].AIIsWaitingForRide&&bus1.CurrentFuel==60,"Consulta alterou estado confirmado.");
        return "PASS: fila real inclui planejados e rogues uteis; 6 persistentes contra 4 bancos congestionam; carregado conta, reparo nao; fila nova e entrega impossivel nao pedem reforco; demanda normal unica com cobertura compartilhada; freio de expansao/conscricao/imposto preserva defesa concreta e retoma sem exigir fila vazia; sem batch. Play nao observado.";
    }finally{
        aiInstance.SetValue(null,oldAi);objectiveField.SetValue(null,oldObjective);queueField.SetValue(null,oldQueue);queueSnapshotField.SetValue(null,oldQueueSnapshot);
        UnitManager.AllActive.Clear();UnitManager.AllActive.AddRange(oldUnits);ConstructionManager.AllActive.Clear();ConstructionManager.AllActive.AddRange(oldBuildings);
    }

}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
