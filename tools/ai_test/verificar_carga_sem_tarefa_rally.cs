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
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,MyUnits=new System.Collections.Generic.List<UnitManager>{carrier,main,extra,guard},EnemyUnits=new System.Collections.Generic.List<UnitManager>()};
    var objective=new SectorObjective{Sector=ConstructionSector.Golf,AssignedTeam=TeamId.Blue,ObjectiveType=AIObjectiveType.RallyAssembly,Status=ObjectiveStatus.Defending,RallyState=AIRallyAssemblyState.Assembling,Slots=new System.Collections.Generic.List<SlotNeed>{new SlotNeed{Role=UnitRole.Capturador,Filled=true,AssignedUnitId=main.InstanceId},new SlotNeed{Role=UnitRole.Capturador,Filled=true,AssignedUnitId=extra.InstanceId},new SlotNeed{Role=UnitRole.Transportador,Filled=true,AssignedUnitId=carrier.InstanceId}}};
    var plan=new TeamObjectivePlan{Objectives=new System.Collections.Generic.List<SectorObjective>{objective}};
    var cargo=new System.Collections.Generic.List<UnitManager>{main,extra};
    var hotel=Building(920200,new UnityEngine.Vector3Int(8,2,0),ConstructionSector.Hotel);hotel.Apply(city);Set(hotel,"slotIndex",-1);Set(hotel,"currentCellPosition",new UnityEngine.Vector3Int(8,2,0));
    var hotelObjective=new SectorObjective{Sector=ConstructionSector.Hotel,ObjectiveType=AIObjectiveType.CaptureSector,Status=ObjectiveStatus.PartialReadyForHandoff,Slots=new System.Collections.Generic.List<SlotNeed>{objective.Slots[0],objective.Slots[1]}};
    objective.Slots.RemoveRange(0,2);plan.Objectives.Add(hotelObjective);plan.SlotIndex=ownSlot;plan.Team=TeamId.Blue;
    var server=Unit(920201,soldier,hotel.CurrentCellPosition,false);server.gameObject.SetActive(true);server.Apply(soldier);Set(server,"boardTilemap",map);Set(server,"slotIndex",ownSlot);Set(server,"teamId",TeamId.Blue);Set(server,"isDead",false);Set(server,"isUnderRepair",false);Set(server,"hasActed",false);Set(server,"remainingMovementPoints",soldier.movement);Set(server,"currentCellPosition",hotel.CurrentCellPosition);server.transform.position=HexCoordinates.GetCellCenterWorld(map,hotel.CurrentCellPosition);if(!UnitManager.AllActive.Contains(server))UnitManager.AllActive.Add(server);snapshot.MyUnits.Add(server);
    hotelObjective.Slots.Add(new SlotNeed{Role=UnitRole.Capturador,Filled=true,AssignedUnitId=server.InstanceId});
    var manager=(ObjectiveManager)typeof(ObjectiveManager).GetMethod("EnsureInstance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
    var plansField=typeof(ObjectiveManager).GetField("plans",flags);var savedPlans=plansField.GetValue(manager);
    try{
        plansField.SetValue(manager,new System.Collections.Generic.List<TeamObjectivePlan>{plan});
        var courier=typeof(AIController).GetMethod("DecideAirCourierAction",flags);
        var targetMethod=typeof(AIController).GetMethod("TryResolveCourierPassengerTarget",flags);
        bool HasTarget(UnitManager p){object[] args={p,plan,snapshot,UnityEngine.Vector3Int.zero,carrier.CurrentCellPosition,null};return(bool)targetMethod.Invoke(ai,args);}
        if(HasTarget(main)||HasTarget(extra))throw new System.Exception("Fixture nao reproduziu captura ja atendida.");
        PlayerAction Decide(){return(PlayerAction)courier.Invoke(ai,new object[]{carrier,snapshot,UnityEngine.Vector3Int.zero});}
        var action=Decide();
        var area=(RallyArea)typeof(AIController).GetMethod("GetRallyArea",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{rally,TeamId.Blue});
        if(action==null||action.SensorAction!=SensorActionType.Disembark||action.SubSteps.Count!=2)throw new System.Exception("Carga sem tarefa nao entregou dois no rally: "+action?.DebugLabel);
        foreach(var step in action.SubSteps)if(!area.Core.Contains(step.TargetHex))throw new System.Exception("Passageiro fora do Core de Golf.");
        if(!area.Core.Contains(action.MoveTo))throw new System.Exception("LZ fora do Core de Golf.");
        var carrierSlot=objective.Slots.Find(n=>n.AssignedUnitId==carrier.InstanceId);objective.Slots.Remove(carrierSlot);
        action=Decide();if(action==null||action.SensorAction!=SensorActionType.None||action.MoveTo!=carrier.CurrentCellPosition)throw new System.Exception("Sem vaga do transporte nao preservou hold.");
        objective.Slots.Add(carrierSlot);
        Set(server,"hasActed",true);Set(server,"currentCellPosition",new UnityEngine.Vector3Int(9,5,0));server.transform.position=HexCoordinates.GetCellCenterWorld(map,server.CurrentCellPosition);UnitOccupancyRules.NotifyUnitOccupancyChanged(server,hotel.CurrentCellPosition,server.CurrentCellPosition);
        if(!HasTarget(main))throw new System.Exception("Hotel deveria ter alvo valido.");
        action=Decide();if(action==null||action.SensorAction!=SensorActionType.Disembark)throw new System.Exception("Alvo valido em Hotel nao recebeu entrega: "+action?.DebugLabel);
        foreach(var step in action.SubSteps)if(!FaixaDeEntregaService.PassageiroChegaAoAlvo(main,step.TargetHex,hotel.CurrentCellPosition,map,terrain,1))throw new System.Exception("Entrega desviada de Hotel.");
        if(!main.IsEmbarked||!extra.IsEmbarked||carrier.HasActed||carrier.CurrentCellPosition!=new UnityEngine.Vector3Int(1,1,0))throw new System.Exception("Decisao alterou tabuleiro confirmado.");
        if(!hotelObjective.Slots.Exists(n=>n.AssignedUnitId==main.InstanceId)||!hotelObjective.Slots.Exists(n=>n.AssignedUnitId==extra.InstanceId))throw new System.Exception("Alterou vagas dos passageiros.");
        return "PASS: carga sem tarefa entrega dois no Core de Golf; sem vaga do transporte mantem hold; alvo valido preserva Hotel; vagas e estado confirmado intactos.";
    }finally{plansField.SetValue(manager,savedPlans);}

}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
