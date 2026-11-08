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
UnityEngine.Application.LogCallback captureLog=(message,stack,type)=>{if(message.Contains("cai antes do atendimento")||message.Contains("recusada como recuperação"))rejections.Add(message);};
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
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    var aircraft=Unit(900002,chinook,new UnityEngine.Vector3Int(2,2,0),false);
    var supplier=Unit(900067,truck,new UnityEngine.Vector3Int(3,2,0),false);
    var patient=Unit(900001,soldier,new UnityEngine.Vector3Int(4,2,0),false);
    foreach(var u in new[]{aircraft,supplier,patient}){
        var cell=u.CurrentCellPosition;u.gameObject.SetActive(true);u.Apply(u==aircraft?chinook:u==supplier?truck:soldier);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"isEmbarked",false);Set(u,"hasActed",false);Set(u,"remainingMovementPoints",u.GetMovementRange());Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,cell,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);
    }
    Set(patient,"currentHP",2);Set(patient,"currentFuel",patient.GetMaxFuel());Set(patient,"isUnderRepair",true);
    Set(aircraft,"currentHP",9);Set(aircraft,"isUnderRepair",true);Set(aircraft,"currentDomain",Domain.Air);Set(aircraft,"currentHeightLevel",HeightLevel.AirLow);
    var host=new UnityEngine.GameObject("LogisticsNeedAI");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);Set(ai,"showAILogs",true);
    var matchHost=new UnityEngine.GameObject("LogisticsNeedMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,MyUnits=new System.Collections.Generic.List<UnitManager>{aircraft,supplier,patient},EnemyUnits=new System.Collections.Generic.List<UnitManager>()};
    void Position(UnitManager u,UnityEngine.Vector3Int cell){var previousCell=u.CurrentCellPosition;Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,previousCell,cell);}
    Position(supplier,new UnityEngine.Vector3Int(1,2,0));Set(supplier,"remainingMovementPoints",1);
    Position(patient,new UnityEngine.Vector3Int(2,2,0));Set(patient,"currentHP",4);
    Position(aircraft,new UnityEngine.Vector3Int(7,2,0));Set(aircraft,"currentHP",2);Set(aircraft,"currentFuel",aircraft.GetMaxFuel());
    var find=typeof(AIController).GetMethod("FindLogisticsServiceTarget",flags);
    var resolve=typeof(AIController).GetMethod("ResolveLogisticsNeed",flags);
    object Member(object value,string name){return value.GetType().GetField(name)?.GetValue(value)??value.GetType().GetProperty(name)?.GetValue(value);}
    float Need(UnitManager u){return(float)Member(resolve.Invoke(ai,new object[]{snapshot,u}),"Score");}
    UnitReachEnvelope Envelope(){return UnitReachEnvelopeService.Build(new UnitReachRequest{Unit=supplier,BoardMap=map,TerrainDatabase=terrain,Intent=ReachIntent.Service,SubStep=ReachSubStep.Terrestre,Band=ReachBand.Operational,OperationalTurns=2,FilterByOperationDomain=false});}
    UnitManager Approach(){var paths=UnitMovementPathRules.CalcularCaminhosValidos(map,supplier,supplier.RemainingMovementPoints,terrain);return(UnitManager)find.Invoke(ai,new object[]{supplier,snapshot,supplier.CurrentCellPosition,paths,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),false,AIReachDecisionStages.Operational});}
    var collect=typeof(AIController).GetMethod("CollectLogisticsTargetsBySupplySensorAtCell",flags);
    UnitManager Serve(){object[] args={supplier,snapshot,supplier.CurrentCellPosition,1,true,0,0,null};var list=(System.Collections.Generic.List<UnitManager>)collect.Invoke(ai,args);return list.Count>0?list[0]:null;}
    var timing=typeof(AIController).GetMethod("CanSupplyLogisticsTargetFromAnyLegalOrigin",flags);
    int Turns(UnitManager target){object[] args={supplier,target,snapshot,Envelope(),new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnitManager>>(),null,0,default(UnityEngine.Vector3Int)};return(bool)timing.Invoke(ai,args)?(int)args[6]:int.MaxValue;}
    if(Turns(patient)!=1||Turns(aircraft)!=2||Need(aircraft)<=Need(patient))throw new System.Exception("Fixture nao tem critico Tatico fraco e Operacional forte.");
    if(Approach()!=patient||Serve()!=patient)throw new System.Exception("Critico Tatico nao venceu critico Operacional, ou atendimento divergiu.");
    // Service range reaches a target outside movement range this turn.
    Position(patient,new UnityEngine.Vector3Int(3,2,0));
    var movement=UnitMovementPathRules.CalcularCaminhosValidos(map,supplier,supplier.RemainingMovementPoints,terrain);
    var planning=new TransportPlanningSnapshot{Transporter=supplier,WorldSnapshot=snapshot,Origin=supplier.CurrentCellPosition,MovementBudget=supplier.RemainingMovementPoints,TransporterReach=movement};
    typeof(AIController).GetMethod("EnsureTransportSupplyPlanning",flags).Invoke(ai,new object[]{planning,snapshot});
    if(planning.SupplyTarget!=patient||planning.SupplyTier!=AIReachDecisionTier.Tactical)throw new System.Exception("Planejador usou distancia de movimento em vez do prazo legal do servico.");
    Position(supplier,new UnityEngine.Vector3Int(2,2,0));if(Serve()!=patient)throw new System.Exception("Atendimento depois de mover divergiu do alvo Tatico.");
    Position(supplier,new UnityEngine.Vector3Int(1,2,0));Position(patient,new UnityEngine.Vector3Int(2,2,0));
    // A preventive now must still lose to corrective later.
    Set(patient,"currentHP",6);Set(patient,"isUnderRepair",false);Set(aircraft,"currentHP",9);
    if(Turns(patient)!=1||Turns(aircraft)!=2||Approach()!=aircraft)throw new System.Exception("Preventivo Tatico ultrapassou corretivo Operacional.");
    // Arrive beside the selected corrective patient and ask the real sensor.
    var start=supplier.CurrentCellPosition;Position(supplier,new UnityEngine.Vector3Int(6,2,0));
    if(Serve()!=aircraft)throw new System.Exception("Atendimento ao chegar discordou da aproximacao corretiva.");
    Position(supplier,start);
    var fuelQuery=typeof(AIController).GetMethod("IsAircraftFuelCriticalForNextUpkeep",flags);object[] fuelArgs={aircraft,0,0,0};fuelQuery.Invoke(ai,fuelArgs);int threshold=(int)fuelArgs[3];Set(aircraft,"currentFuel",threshold);
    if(Turns(aircraft)!=2||Approach()!=patient)throw new System.Exception("Aeronave cai antes do atendimento mas continuou na disputa.");
    if(!rejections.Exists(message=>message.Contains("cai antes do atendimento")))throw new System.Exception("Recusa sem log do prazo fatal.");
    // A geometric Tactical origin occupied by another unit is not legal.
    Set(aircraft,"currentFuel",aircraft.GetMaxFuel());Position(aircraft,new UnityEngine.Vector3Int(3,2,0));
    var around=new System.Collections.Generic.List<UnityEngine.Vector3Int>();UnitMovementPathRules.GetImmediateHexNeighbors(map,aircraft.CurrentCellPosition,around);around.Add(aircraft.CurrentCellPosition);
    var env=Envelope();int nextId=901000,blocked=0;
    foreach(var cell in around){if(cell==aircraft.CurrentCellPosition||!env.TryGetTurns(cell,out int turns)||turns!=1)continue;if(UnitOccupancyRules.GetUnitsAtCell(map,cell,supplier).Count>0)continue;
        var blocker=Unit(nextId++,soldier,cell,false);blocker.gameObject.SetActive(true);blocker.Apply(soldier);Set(blocker,"boardTilemap",map);Set(blocker,"slotIndex",ownSlot);Set(blocker,"teamId",TeamId.Blue);Set(blocker,"isDead",false);Position(blocker,cell);UnitManager.AllActive.Add(blocker);blocked++;
    }
    if(env.ResolveActionBand(aircraft.CurrentCellPosition)!=ReachBand.Tactical||Turns(aircraft)!=2)throw new System.Exception("Prazo geometrico foi usado sem origem legal: bloqueados="+blocked+" prazo="+Turns(aircraft));
    // The transport planner consumes that same timing; geometric distance
    // alone would incorrectly label this target Tactical.
    snapshot.MyUnits=new System.Collections.Generic.List<UnitManager>{supplier,aircraft};
    var withTiming=typeof(AIController).GetMethod("FindLogisticsServiceTargetWithTiming",flags);
    var timingPaths=UnitMovementPathRules.CalcularCaminhosValidos(map,supplier,supplier.RemainingMovementPoints,terrain);
    object[] selectedArgs={supplier,snapshot,supplier.CurrentCellPosition,timingPaths,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),false,null,AIReachDecisionStages.Operational};
    if((UnitManager)withTiming.Invoke(ai,selectedArgs)!=aircraft||(int)Member(selectedArgs[6],"Turns")!=2)throw new System.Exception("Selecao nao publicou o prazo legal para o planejador.");
    if(aircraft.CurrentHP!=9||aircraft.HasActed||aircraft.GetDomain()!=Domain.Air||patient.HasActed)throw new System.Exception("Consulta executou atendimento ou mudou camada.");
    return "PASS: faixa > prazo > score; critico Tatico vence Operacional mais valioso/grave; preventivo Tatico perde para corretivo Operacional; aeronave sem tempo para resgate sai; aproximacao e atendimento real concordam; origem geometrica ocupada nao conta como atendimento Tatico; sem atendimento confirmado.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
