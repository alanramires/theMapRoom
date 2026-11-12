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
    int enemySlot=ownSlot==0?1:0;
    void Check(bool value,string reason){if(!value)throw new System.Exception(reason);}
    T Table<T>()where T:UnityEngine.Object{var ids=UnityEditor.AssetDatabase.FindAssets("t:"+typeof(T).Name);return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(ids[0]));}
    var bomber=Asset<UnitData>("cacaA");var aaa=Asset<UnitData>("aaa");var fighter=Asset<UnitData>("cacaB");var apache=Asset<UnitData>("apache");
    Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,bomber,fighter,apache,aaa});
    for(int x=0;x<45;x++)for(int y=0;y<21;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
    var rally=Building(940001,new UnityEngine.Vector3Int(20,8,0),ConstructionSector.Golf);
    rally.Apply(city);Set(rally,"slotIndex",ownSlot);Set(rally,"teamId",TeamId.Blue);Set(rally,"isRallyPoint",true);Set(rally,"rallyOwnerSlots",new System.Collections.Generic.List<int>{ownSlot});
    UnitManager Spawn(int id,UnitData data,UnityEngine.Vector3Int cell,int slot){
        var u=Unit(id,data,cell,false);u.gameObject.SetActive(true);u.Apply(data);
        Set(u,"boardTilemap",map);Set(u,"slotIndex",slot);Set(u,"teamId",slot==ownSlot?TeamId.Blue:TeamId.Red);Set(u,"isDead",false);Set(u,"currentHP",10);Set(u,"currentAmmo",100);Set(u,"currentFuel",100);Set(u,"remainingMovementPoints",u.MaxMovementPoints);Set(u,"isUnderRepair",false);Set(u,"hasActed",false);Set(u,"isEmbarked",false);Set(u,"currentDomain",data.domain);Set(u,"currentHeightLevel",data.heightLevel);Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);
        if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;
    }
    var bomb=Spawn(940087,bomber,new UnityEngine.Vector3Int(10,8,0),ownSlot);
    var guard=Spawn(940010,soldier,new UnityEngine.Vector3Int(20,8,0),ownSlot);
    var enemy=Spawn(940036,aaa,new UnityEngine.Vector3Int(9,8,0),enemySlot);Set(enemy,"currentHP",1);
    var aiHost=new UnityEngine.GameObject("AirRallyAI");aiHost.SetActive(false);var ai=aiHost.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);Set(ai,"aiStep",true);Set(ai,"currentAISlotIndex",ownSlot);
    var matchHost=new UnityEngine.GameObject("AirRallyMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);
    var stateHost=new UnityEngine.GameObject("AirRallyCombat");stateHost.SetActive(false);var state=stateHost.AddComponent<TurnStateManager>();Set(state,"rpsDatabase",Table<RPSDatabase>());Set(state,"dpqMatchupDatabase",Table<DPQMatchupDatabase>());Set(state,"weaponPriorityData",Table<WeaponPriorityData>());Set(ai,"turnStateManager",state);
    var stat=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var objectiveField=typeof(ObjectiveManager).GetField("instance",stat);var oldObjective=objectiveField.GetValue(null);
    var objectiveHost=new UnityEngine.GameObject("AirRallyObjectives");objectiveHost.SetActive(false);var manager=objectiveHost.AddComponent<ObjectiveManager>();objectiveField.SetValue(null,manager);
    var unrelated=new System.Collections.Generic.List<UnitManager>();foreach(var u in UnitManager.AllActive)if(!units.Contains(u))unrelated.Add(u);foreach(var u in unrelated)UnitManager.AllActive.Remove(u);
    try{
        var need=new SlotNeed{Role=UnitRole.AtaqueAereo,Filled=true,AssignedUnitId=bomb.InstanceId};
        var objective=new SectorObjective{Sector=ConstructionSector.Golf,AssignedTeam=TeamId.Blue,ObjectiveType=AIObjectiveType.RallyAssembly,RallyState=AIRallyAssemblyState.Assembling,Slots=new System.Collections.Generic.List<SlotNeed>{need}};
        var plan=new TeamObjectivePlan{SlotIndex=ownSlot,Team=TeamId.Blue,Objectives=new System.Collections.Generic.List<SectorObjective>{objective}};
        Set(manager,"plans",new System.Collections.Generic.List<TeamObjectivePlan>{plan});
        var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,TurnNumber=11,MyUnits=new System.Collections.Generic.List<UnitManager>{bomb,guard},EnemyUnits=new System.Collections.Generic.List<UnitManager>{enemy},MyBuildings=new System.Collections.Generic.List<ConstructionManager>{rally},EnemyBuildings=new System.Collections.Generic.List<ConstructionManager>()};
        var decide=typeof(AIController).GetMethod("TryDecideAirCombatAction",flags);
        var anchor=typeof(AIController).GetMethod("ResolveAirCombatFallbackAnchor",flags);
        var areaQuery=typeof(AIController).GetMethod("GetRallyArea",stat);
        void Clear(){foreach(var name in new[]{"rallyAreaFacts","rallyReachFacts","rallyReadinessFacts"})((System.Collections.IDictionary)typeof(AIController).GetField(name,stat).GetValue(null)).Clear();MovementReachCache.ClearAll();}
        void Position(UnitManager u,UnityEngine.Vector3Int cell){Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);Clear();}
        PlayerAction Decide(UnitManager u){typeof(AIController).GetMethod("BeginAIReviewCapture",flags).Invoke(ai,null);try{return(PlayerAction)decide.Invoke(ai,new object[]{u,snapshot});}finally{typeof(AIController).GetMethod("EndAIReviewCapture",flags).Invoke(ai,null);}}
        (UnityEngine.Vector3Int cell,string tier) Anchor(UnitManager u){object[] args={u,snapshot,u.CurrentCellPosition,snapshot.EnemyUnits,null,false,null,null,null,false};var cell=(UnityEngine.Vector3Int)anchor.Invoke(ai,args);return(cell,(string)args[4]);}
        bool InArea(UnitManager u,UnityEngine.Vector3Int cell){return RallyFormationReport.LocateParticipant((RallyArea)areaQuery.Invoke(null,new object[]{rally,TeamId.Blue}),cell,u)==RallyLocation.Core;}
        var gateField=typeof(AIController).GetField("aiReviewCandidates",flags);
        bool Blocked(){return ((System.Collections.Generic.List<AIController.AIReviewCandidate>)gateField.GetValue(ai)).Exists(c=>c.category=="Ataque.Policy"&&c.status=="blocked"&&c.gate=="interceptor_tertiary"&&c.reason=="interceptador não inicia tiro terciário");}
        var prey=Spawn(940200,bomber,new UnityEngine.Vector3Int(28,8,0),enemySlot);Set(prey,"isDead",true);
        foreach(var data in new[]{bomber,fighter}){
            var u=Spawn(940300+units.Count,data,new UnityEngine.Vector3Int(10,8,0),ownSlot);
            snapshot.MyUnits=new System.Collections.Generic.List<UnitManager>{u,guard};snapshot.EnemyUnits=new System.Collections.Generic.List<UnitManager>{enemy};
            Position(enemy,new UnityEngine.Vector3Int(11,8,0));Set(enemy,"isDead",false);Set(enemy,"currentHP",1);need.Filled=false;Clear();
            var pref=typeof(AIController).GetMethod("ResolveAirCombatTargetPreference",stat);
            Check((BazookaTargetPriority)pref.Invoke(null,new object[]{u,enemy})==BazookaTargetPriority.Tertiary,data.id+": AAA nao e terciaria.");
            var origin=u.CurrentCellPosition;int hp=u.CurrentHP,ammo=u.CurrentAmmo,fuel=u.CurrentFuel;
            var action=Decide(u);Check(action!=null&&action.SensorAction!=SensorActionType.Attack&&Blocked(),data.id+": iniciou terciario ou nao registrou gate. action="+action?.DebugLabel+" sensor="+action?.SensorAction+" blocked="+Blocked()+" essence="+UnitRoleCompatibility.MatchesLabel(data.roles[0],UnitRole.Interceptador));
            // Preserve real preferences: A hunts a fighter; B hunts a helicopter (fighters are tertiary to B).
            prey.Apply(data==fighter?apache:bomber);Set(prey,"currentDomain",Domain.Air);Set(prey,"currentHeightLevel",(data==fighter?apache:bomber).heightLevel);
            // Pick a real Operational-only air cell, beyond a move-and-shot this turn.
            var reach=UnitReachEnvelopeService.Build(new UnitReachRequest{Unit=u,BoardMap=map,TerrainDatabase=terrain,Intent=ReachIntent.Mobility,Band=ReachBand.Operational});
            var tactical=UnitReachEnvelopeService.Build(new UnitReachRequest{Unit=u,BoardMap=map,TerrainDatabase=terrain,Intent=ReachIntent.Mobility,Band=ReachBand.Tactical});
            UnityEngine.Vector3Int preyCell=default;bool found=false;
            foreach(var cell in reach.MovementCells)if(map.HasTile(cell)&&!tactical.MovementCells.Contains(cell)&&SectorManager.HexDistance(origin,cell)>u.MaxMovementPoints+2){preyCell=cell;found=true;break;}
            Check(found,data.id+": sem celula operacional para presa.");Position(prey,preyCell);Set(prey,"isDead",false);snapshot.EnemyUnits.Add(prey);Clear();
            var hunt=Anchor(u);action=Decide(u);
            Check(hunt.cell==preyCell&&action!=null&&action.SensorAction!=SensorActionType.Attack&&SectorManager.HexDistance(action.MoveTo,preyCell)<SectorManager.HexDistance(origin,preyCell),data.id+": nao perseguiu presa operacional. tier="+hunt.tier+" anchor="+hunt.cell+" prey="+preyCell+" action="+action?.DebugLabel+" pref="+pref.Invoke(null,new object[]{u,prey}));
            snapshot.EnemyUnits.Remove(prey);Set(prey,"isDead",true);need.AssignedUnitId=u.InstanceId;need.Role=UnitRole.Interceptador;need.Filled=true;
            Position(u,new UnityEngine.Vector3Int(20,7,0));Position(enemy,new UnityEngine.Vector3Int(19,7,0));
            Check(AIController.IsRallyParticipantPresent(u,rally,ReachBand.Tactical),"Caso nao esta no Core.");
            action=Decide(u);Check(action!=null&&action.SensorAction!=SensorActionType.Attack&&Blocked()&&InArea(u,action.MoveTo),data.id+": montagem iniciou terciario ou saiu da area.");
            Check(u.CurrentHP==hp&&u.CurrentAmmo==ammo&&u.CurrentFuel==fuel&&!u.HasActed,"Decisao alterou estado confirmado.");
        }
        need.Filled=false;var control=Spawn(940400,apache,new UnityEngine.Vector3Int(20,7,0),ownSlot);snapshot.MyUnits=new System.Collections.Generic.List<UnitManager>{control,guard};Clear();
        var attack=Decide(control);Check(attack!=null&&attack.SensorAction==SensorActionType.Attack,"Apache perdeu ataque contra AAA.");
        Clear();return "PASS: Caca A e B recusam AAA terciaria sem presa e em montagem com gate interceptor_tertiary; com presa operacional A persegue caca e B persegue Apache; Apache conserva ataque; sem batch executado. Play nao observado.";

    }finally{typeof(AIController).GetMethod("EndAIReviewCapture",flags).Invoke(ai,null);objectiveField.SetValue(null,oldObjective);foreach(var u in unrelated)if(u!=null&&!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);}
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
