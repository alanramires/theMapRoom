if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
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
    // Stress de alcance: o helicoptero chega a base distante pelo Operacional,
    // mas esse raio nao pode aumentar a area local nem virar presenca no rally.
    chinook=UnityEngine.Object.Instantiate(chinook);created.Add(chinook);chinook.movement=24;
    var unitDb=UnityEngine.ScriptableObject.CreateInstance<UnitDatabase>();created.Add(unitDb);Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck});
    var sectors=new UnityEngine.GameObject("FixtureSectors").AddComponent<SectorManager>();
    Set(sectors,"neighborDistanceTilemap",map);Set(sectors,"neighborDistanceTerrainDatabase",terrain);Set(sectors,"neighborDistanceReferenceUnitData",soldier);
    SectorManager.InvalidateLandDistanceCache();
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
    var rally=Building(920038,new UnityEngine.Vector3Int(4,2,0),ConstructionSector.Golf);rally.Apply(city);Set(rally,"slotIndex",ownSlot);Set(rally,"teamId",TeamId.Blue);Set(rally,"currentCapturePoints",rally.CapturePointsMax);Set(rally,"isRallyPoint",true);Set(rally,"rallyOwnerSlots",new System.Collections.Generic.List<int>{ownSlot});
    var carrier=Unit(920072,chinook,new UnityEngine.Vector3Int(1,1,0),false);
    var main=Unit(920010,soldier,carrier.CurrentCellPosition,true);var extra=Unit(920063,soldier,carrier.CurrentCellPosition,true);
    var guard=Unit(920100,soldier,rally.CurrentCellPosition,false);
    foreach(var u in new[]{carrier,main,extra,guard}){var cell=u.CurrentCellPosition;u.gameObject.SetActive(true);u.Apply(u==carrier?chinook:soldier);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"currentHP",7);Set(u,"currentFuel",49);Set(u,"remainingMovementPoints",u.GetMovementRange());Set(u,"isEmbarked",u==main||u==extra);Set(u,"isUnderRepair",false);Set(u,"hasActed",false);Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,cell,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);}
    foreach(var u in new[]{main,extra})Set(u,"embarkedTransporter",carrier);
    Set(carrier,"currentDomain",Domain.Air);Set(carrier,"currentHeightLevel",HeightLevel.AirLow);
    Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{slotId="passageiros",slotIndex=0,seatIndex=0,embarkedUnit=main,embarkedOnTurn=1},new UnitTransportSeatRuntime{slotId="passageiros",slotIndex=0,seatIndex=1,embarkedUnit=extra,embarkedOnTurn=2}});
    map.SetTile(carrier.CurrentCellPosition,sea.paletteTile);
    var host=new UnityEngine.GameObject("RallyDeliveryAI");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var matchHost=new UnityEngine.GameObject("RallyDeliveryMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);

    void Check(bool yes,string why){if(!yes)throw new System.Exception(why);}
    var stat=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    void Clear(){((System.Collections.IDictionary)typeof(AIController).GetField("rallyReadinessFacts",stat).GetValue(null)).Clear();((System.Collections.IDictionary)typeof(AIController).GetField("rallyReachFacts",stat).GetValue(null)).Clear();((System.Collections.IDictionary)typeof(AIController).GetField("rallyAreaFacts",stat).GetValue(null)).Clear();}
    var oldObjective=typeof(ObjectiveManager).GetField("instance",stat).GetValue(null);
    var objectiveHost=new UnityEngine.GameObject("ReadinessObjectives");objectiveHost.SetActive(false);
    var manager=objectiveHost.AddComponent<ObjectiveManager>();typeof(ObjectiveManager).GetField("instance",stat).SetValue(null,manager);
    try{
        var unrelated=new System.Collections.Generic.List<UnitManager>(UnitManager.AllActive);
        foreach(var unit in unrelated)if(!units.Contains(unit))UnitManager.AllActive.Remove(unit);
        var green=(System.Collections.IDictionary)typeof(AIController).GetField("rallyGoGreenTurns",stat).GetValue(null);
        var monitorMap=(System.Collections.IDictionary)typeof(AIController).GetField("invasionMonitors",stat).GetValue(null);
        var savedGreen=new System.Collections.Generic.Dictionary<object,object>();foreach(System.Collections.DictionaryEntry entry in green)savedGreen[entry.Key]=entry.Value;
        var savedMonitors=new System.Collections.Generic.Dictionary<object,object>();foreach(System.Collections.DictionaryEntry entry in monitorMap)savedMonitors[entry.Key]=entry.Value;
        try{
            map.ClearAllTiles();for(int x=0;x<11;x++)for(int y=0;y<17;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),(x==4||x==5)?sea.paletteTile:plain.paletteTile);
            var tank=UnityEngine.Object.Instantiate(Asset<UnitData>("tanque B"));created.Add(tank);tank.movement=1;
            var artillery=UnityEngine.Object.Instantiate(Asset<UnitData>("obusLeve"));created.Add(artillery);artillery.movement=1;
            Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,tank,artillery});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
            UnitManager Spawn(int id,UnitData data,UnityEngine.Vector3Int cell){var u=Unit(id,data,cell,false);u.gameObject.SetActive(true);u.Apply(data);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"currentHP",10);Set(u,"currentFuel",100);Set(u,"isUnderRepair",false);Set(u,"isEmbarked",false);Set(u,"currentCellPosition",cell);Set(u,"remainingMovementPoints",u.MaxMovementPoints);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;}
            void Place(UnitManager unit,UnityEngine.Vector3Int cell){Set(unit,"currentCellPosition",cell);unit.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(unit,cell,cell);Clear();}
            Place(guard,new UnityEngine.Vector3Int(2,2,0));Place(carrier,new UnityEngine.Vector3Int(2,1,0));
            Set(rally,"currentCellPosition",new UnityEngine.Vector3Int(1,2,0));
            var armor=Spawn(940001,tank,new UnityEngine.Vector3Int(6,2,0));
            var gun=Spawn(940002,artillery,new UnityEngine.Vector3Int(1,4,0));
            var assault=Spawn(940003,tank,new UnityEngine.Vector3Int(1,4,0));
            var enemyHq=Building(940010,new UnityEngine.Vector3Int(8,2,0),ConstructionSector.Base1);enemyHq.Apply(city);Set(enemyHq,"slotIndex",ownSlot==0?1:0);Set(enemyHq,"teamId",TeamId.Red);Set(enemyHq,"currentCapturePoints",enemyHq.CapturePointsMax);
            var group=new SectorObjective{Sector=ConstructionSector.Golf,ObjectiveType=AIObjectiveType.RallyAssembly,RallyState=AIRallyAssemblyState.GoGreen,RallyGoGreenTurn=12,RallyParticipantIds=new System.Collections.Generic.List<int>{guard.InstanceId,armor.InstanceId}};
            var target=new SectorObjective{Sector=ConstructionSector.Base1,ObjectiveType=AIObjectiveType.InvasionAttack,Status=ObjectiveStatus.Pursuing};target.Slots.Add(new SlotNeed{Role=UnitRole.Assalto,Filled=true,AssignedUnitId=armor.InstanceId});
            var plan=new TeamObjectivePlan{SlotIndex=ownSlot,Team=TeamId.Blue};plan.Objectives.Add(target);plan.RallyAdvances.Add(group);Set(manager,"plans",new System.Collections.Generic.List<TeamObjectivePlan>{plan});
            var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,TurnNumber=12,MyHQ=rally,EnemyHQ=enemyHq,MyUnits=new System.Collections.Generic.List<UnitManager>{guard,armor,carrier,gun,assault},EnemyUnits=new System.Collections.Generic.List<UnitManager>()};
            Set(match,"currentTurn",12);
            SectorManager.InvalidateLandDistanceCache();
            ((System.Collections.IDictionary)typeof(AIRouteDistance).GetField("Cache",stat).GetValue(null)).Clear();
            var refresh=typeof(AIController).GetMethod("RefreshRallyAdvanceCaptains",stat);
            refresh.Invoke(null,new object[]{plan,snapshot});Check(group.RallyCaptainId==armor.InstanceId,"Capturador ilhado nao cedeu capitania ao blindado com rota; capitao="+group.RallyCaptainId);
            var waitingForRally=typeof(AIController).GetMethod("UpdateRallyAdvanceWaiting",stat);
            Check(guard.AIIsWaitingForRide,"Capturador ilhado nao publicou carona antes do filtro de retardatarios");
            Check(!(bool)waitingForRally.Invoke(null,new object[]{plan,TeamId.Blue})
                && group.RallyParticipantIds.Contains(guard.InstanceId)&&!group.RallyWaiting,
                "Soldado ilhado com carona foi removido ou segurou o blindado capitao");
            object[] preservedHold={guard,snapshot,null};
            Check((bool)typeof(AIController).GetMethod("TryResolveRallyAdvanceHold",stat).Invoke(null,preservedHold),
                "Soldado preservado nao aguardou na area do rally");
            Set(guard,"isEmbarked",true);Set(guard,"embarkedTransporter",carrier);
            Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{
                new UnitTransportSeatRuntime{slotId=chinook.transportSlots[0].slotId,slotIndex=0,seatIndex=0,embarkedUnit=guard},
                new UnitTransportSeatRuntime{slotId=chinook.transportSlots[0].slotId,slotIndex=0,seatIndex=1,embarkedUnit=extra}});
            refresh.Invoke(null,new object[]{plan,snapshot});Check(group.RallyCaptainId==guard.InstanceId,"Capitania nao voltou ao capturador embarcado com alcance");
            var hold=typeof(AIController).GetMethod("TryResolveRallyAdvanceHold",stat);
            group.RallyParticipantIds.Add(gun.InstanceId);refresh.Invoke(null,new object[]{plan,snapshot});
            object[] holdArgs={gun,snapshot,null};Check((bool)hold.Invoke(null,holdArgs),"Terrestre sem rota perseguiu Chinook em vez de esperar no rally");
            object[] advancingArmor={armor,snapshot,null};Check(!(bool)hold.Invoke(null,advancingArmor),"Blindado ja com rota ao alvo voltou ao rally por causa do Chinook");
            group.RallyParticipantIds.Remove(gun.InstanceId);
            Set(guard,"isEmbarked",false);Set(guard,"embarkedTransporter",null);Place(armor,new UnityEngine.Vector3Int(1,2,0));
            refresh.Invoke(null,new object[]{plan,snapshot});Check(group.RallyCaptainId==-1,"Sem rota foi eleito capitao que nao chega");
            Set(match,"currentTurn",12);
            typeof(AIController).GetMethod("PublishRideNeed",flags).Invoke(ai,new object[]{guard,plan,snapshot});
            Check(guard.AIIsWaitingForRide,"Grupo sem capitao nao publicou pedido na fila existente");
            Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>());
            Set(carrier,"currentDomain",Domain.Land);Set(carrier,"currentHeightLevel",HeightLevel.Surface);
            UnitOccupancyRules.NotifyUnitOccupancyChanged(carrier,carrier.CurrentCellPosition,carrier.CurrentCellPosition);
            var boarding=(PlayerAction)typeof(AIController).GetMethod("DecideUnitAction",flags).Invoke(ai,new object[]{guard,snapshot});
            Check(boarding.SensorAction==SensorActionType.Embark&&boarding.MoveTo==guard.CurrentCellPosition&&!guard.IsEmbarked,
                "Hold do rally impediu embarque adjacente ou executou embarque no preview: "+boarding.SensorAction+" "+boarding.DebugLabel);
            Set(carrier,"currentDomain",Domain.Air);Set(carrier,"currentHeightLevel",HeightLevel.AirLow);
            Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{
                new UnitTransportSeatRuntime{slotId=chinook.transportSlots[0].slotId,slotIndex=0,seatIndex=0,embarkedUnit=main},
                new UnitTransportSeatRuntime{slotId=chinook.transportSlots[0].slotId,slotIndex=0,seatIndex=1,embarkedUnit=extra}});
            var evaluate=typeof(AIController).GetMethod("EvaluatePickupRideNeed",flags);
            var ride=(QueroCaronaResult)evaluate.Invoke(ai,new object[]{guard,plan,2,snapshot});
            Check(ride.wantsRide&&ride.pickupPriority==1&&ride.evaluatedTarget==enemyHq.CurrentCellPosition,"Carona de invasao sem prioridade/alvo explicitos");
            var compare=typeof(MelhorEmbarqueService).GetMethod("CompareOptions",stat);
            var priorityOption=new MelhorEmbarqueOption{passenger=guard,rideNeed=ride,score=-9999};
            var normalOption=new MelhorEmbarqueOption{passenger=main,rideNeed=new QueroCaronaResult{wantsRide=true},score=99999};
            Check((int)compare.Invoke(null,new object[]{priorityOption,normalOption})<0,"Prioridade da fila perdeu para score da missao de cidade");
            Place(armor,new UnityEngine.Vector3Int(1,8,0));Place(gun,new UnityEngine.Vector3Int(1,10,0));Place(assault,new UnityEngine.Vector3Int(1,10,0));
            group.RallyCaptainId=armor.InstanceId;group.RallyParticipantIds=new System.Collections.Generic.List<int>{armor.InstanceId,gun.InstanceId};
            Set(assault,"isDead",true);Clear();
            var waiting=typeof(AIController).GetMethod("UpdateRallyAdvanceWaiting",stat);
            Check(!(bool)waiting.Invoke(null,new object[]{plan,TeamId.Blue})&&group.RallyParticipantIds.Contains(gun.InstanceId),"Artilharia de Dissuasao segurou capitao, ou saiu sem testar filtro de Forca");
            Set(gun,"isDead",true);Set(assault,"isDead",false);group.RallyParticipantIds=new System.Collections.Generic.List<int>{armor.InstanceId,assault.InstanceId};Clear();
            Check((bool)waiting.Invoke(null,new object[]{plan,TeamId.Blue})&&group.RallyWaiting,"Assalto terrestre fora do Tatico nao segurou capitao");
            group.RallyParticipantIds=new System.Collections.Generic.List<int>{guard.InstanceId,armor.InstanceId};group.RallyCaptainId=-1;
            green[ownSlot+":"+ConstructionSector.Golf]=12;
            var valueType=monitorMap.GetType().GetGenericArguments()[1];var mon=System.Activator.CreateInstance(valueType);valueType.GetField("BestDistance").SetValue(mon,0);valueType.GetField("StallCounter").SetValue(mon,2);monitorMap[ownSlot]=mon;
            var pressure=PressaoCaronaService.Read(ai,ownSlot,13);Check(pressure.PersistentAirWaiting>0,"Fixture sem carona persistente util: "+pressure.PersistentAirWaiting+" pedidos="+pressure.Pedidos.Count+" invalidos="+pressure.UnserviceableAirWaiting);
            var monitor=typeof(AIController).GetMethod("UpdateInvasionMonitor",flags);
            foreach(int turn in new[]{13,14,15}){snapshot.TurnNumber=turn;monitor.Invoke(ai,new object[]{plan,TeamId.Blue,snapshot});Check(monitorMap.Contains(ownSlot)&&(int)valueType.GetField("StallCounter").GetValue(monitorMap[ownSlot])==2,"Estagnacao nao congelou com carona pendente T"+turn);}
            snapshot.TurnNumber=24;monitor.Invoke(ai,new object[]{plan,TeamId.Blue,snapshot});Check(!monitorMap.Contains(ownSlot)&&plan.RallyAdvances.Count==0,"Teto absoluto de 12 turnos foi prorrogado pela carona");
            // A connected land route may be far beyond the two-turn Operational envelope.
            for(int x=0;x<25;x++)for(int y=0;y<17;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
            var farTarget=new UnityEngine.Vector3Int(20,2,0);
            Set(enemyHq,"currentCellPosition",farTarget);enemyHq.transform.position=HexCoordinates.GetCellCenterWorld(map,farTarget);
            SectorManager.InvalidateLandDistanceCache();
            ((System.Collections.IDictionary)typeof(AIRouteDistance).GetField("Cache",stat).GetValue(null)).Clear();
            UnitReachEnvelopeService.ClearCache();Clear();
            group.RallyParticipantIds=new System.Collections.Generic.List<int>{guard.InstanceId};group.RallyCaptainId=-1;group.RallyGoGreenTurn=25;plan.RallyAdvances.Add(group);
            guard.ClearAIRideWait();snapshot.TurnNumber=25;Set(match,"currentTurn",25);
            Check(AIRouteDistance.TryGet(guard,guard.CurrentCellPosition,farTarget,out float farCost)&&farCost>=12,
                "Fixture sem estrada longa: "+farCost);
            Check(!AIController.IsRallyParticipantInBand(guard,farTarget,ReachBand.Operational),"Alvo longo deveria estar fora do Operacional");
            refresh.Invoke(null,new object[]{plan,snapshot});Check(group.RallyCaptainId==guard.InstanceId,"Estrada longa deixou grupo sem capitao");
            typeof(AIController).GetMethod("PublishRideNeed",flags).Invoke(ai,new object[]{guard,plan,snapshot});
            Check(!guard.AIIsWaitingForRide,"Estrada longa publicou carona da invasao");
            var distantRide=(QueroCaronaResult)evaluate.Invoke(ai,new object[]{guard,plan,2,snapshot});
            Check(!distantRide.wantsRide&&distantRide.pickupPriority==0,"Pickup transformou estrada longa em pedido de carona");
            var capturerRide=(QueroCaronaResult)typeof(AIController).GetMethod("EvaluateCapturerRideNeed",flags).Invoke(ai,new object[]{guard,target});
            Check(!capturerRide.wantsRide&&!guard.AIIsWaitingForRide,"Capturador voltou a publicar carona pelo fluxo normal");
            object[] longHold={guard,snapshot,null};Check(!(bool)hold.Invoke(null,longHold),"Grupo com estrada longa entrou em hold");
            Check(!(bool)waiting.Invoke(null,new object[]{plan,TeamId.Blue})&&!group.RallyWaiting,"Grupo so com capitao entrou em ESPERANDO pela distancia ao HQ");
            var roadPressure=PressaoCaronaService.Read(ai,ownSlot,26);
            Check(!roadPressure.Pedidos.Exists(request=>request.Unit==guard),"Pressao Carona ressuscitou pedido do capturador com estrada longa");
            return "PASS: estrada terrestre de 12+ hexes e fora do Operacional elege capturador sem carona/hold; blindado alcancavel assume; capturador embarca e retoma capitania; sem rota capitao -1 e pedido prioritario na mesma fila; retardatario com carona publicada preservado sem segurar capitao; terrestre espera no rally; Dissuasao nao segura e Ruptura terrestre segura; carona persistente congela contador 3 turnos; teto 12 encerra. Fora do Play.";
        }finally{green.Clear();foreach(var entry in savedGreen)green[entry.Key]=entry.Value;monitorMap.Clear();foreach(var entry in savedMonitors)monitorMap[entry.Key]=entry.Value;foreach(var unit in unrelated)if(unit!=null&&!UnitManager.AllActive.Contains(unit))UnitManager.AllActive.Add(unit);Clear();}
    }finally{typeof(ObjectiveManager).GetField("instance",stat).SetValue(null,oldObjective);}
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
