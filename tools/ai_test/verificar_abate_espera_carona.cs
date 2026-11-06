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
        go.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);go.SetActive(true);b.enabled=true;Set(b,"currentCellPosition",cell);Set(b,"boardTilemap",map);
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
    foreach(var b in buildings){ConstructionManager.AllActive.Remove(b);UnityEngine.Object.DestroyImmediate(b.gameObject);}
    T Table<T>()where T:UnityEngine.Object{
        var ids=UnityEditor.AssetDatabase.FindAssets("t:"+typeof(T).Name);return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(ids[0]));
    }
    var stateHost=new UnityEngine.GameObject("RideKillTestState");stateHost.SetActive(false);var state=stateHost.AddComponent<TurnStateManager>();
    Set(state,"rpsDatabase",Table<RPSDatabase>());Set(state,"dpqMatchupDatabase",Table<DPQMatchupDatabase>());Set(state,"weaponPriorityData",Table<WeaponPriorityData>());
    Set(ai,"turnStateManager",state);Set(ai,"currentAISlotIndex",0);
    foreach(var u in new[]{main,extra}){
        Set(u,"isEmbarked",false);Set(u,"snapToCellCenter",false);Set(u,"autoSnapWhenMovedInEditor",false);u.Apply(soldier);
        u.transform.position=HexCoordinates.GetCellCenterWorld(map,new UnityEngine.Vector3Int(3,2,0));u.gameObject.SetActive(true);u.enabled=true;
        Set(u,"boardTilemap",map);Set(u,"isDead",false);Set(u,"hasActed",false);Set(u,"currentDomain",Domain.Land);Set(u,"currentHeightLevel",HeightLevel.Surface);
        Set(u,"remainingMovementPoints",3);Set(u,"currentHP",10);
        if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);
    }
    Set(main,"currentCellPosition",new UnityEngine.Vector3Int(2,2,0));main.transform.position=HexCoordinates.GetCellCenterWorld(map,main.CurrentCellPosition);
    Set(extra,"slotIndex",1);Set(extra,"currentCellPosition",new UnityEngine.Vector3Int(4,2,0));extra.transform.position=HexCoordinates.GetCellCenterWorld(map,extra.CurrentCellPosition);Set(extra,"currentHP",3);
    Set(main,"teamId",TeamId.Blue);Set(extra,"teamId",TeamId.Yellow);
    snapshot.MyUnits=new System.Collections.Generic.List<UnitManager>{main};snapshot.EnemyUnits=new System.Collections.Generic.List<UnitManager>{extra};
    main.SetCurrentCellPosition(new UnityEngine.Vector3Int(2,2,0));extra.SetCurrentCellPosition(new UnityEngine.Vector3Int(4,2,0));
    var from=main.CurrentCellPosition;var meetingCell=new UnityEngine.Vector3Int(3,2,0);
    var paths=UnitMovementPathRules.CalcularCaminhosValidos(map,main,3,terrain);
    var wait=new PlayerAction{ActionType=PlayerActionType.UnitAction,SensorAction=SensorActionType.None,HasMoveTo=true,MoveTo=meetingCell};
    var rendezvous=new MelhorEmbarqueOption{passenger=main,transporter=carrier,lzCell=new UnityEngine.Vector3Int(3,1,0),hasPassengerMeetingCell=true,passengerMeetingCell=meetingCell};
    var helper=typeof(AIController).GetMethod("TryBuildRideWaitOpportunityKill",flags);
    object[] args={main,snapshot,wait,rendezvous,paths,null};
        if(!(bool)helper.Invoke(ai,args)){
        var visible=(System.Collections.Generic.List<UnitManager>)typeof(AIController).GetMethod("CollectVisibleAssaultEnemies",flags).Invoke(ai,new object[]{snapshot.AITeam});
        var can=typeof(AIController).GetMethod("CanAttackTargetFrom",flags).Invoke(ai,new object[]{from,meetingCell,main,extra});
        object[] simArgs={main,extra,meetingCell,null};
        bool simulated=(bool)typeof(AIController).GetMethod("TrySimulateAttackForAI",flags).Invoke(ai,simArgs);
        var opts=new System.Collections.Generic.List<PodeMirarTargetOption>();var invalid=new System.Collections.Generic.List<PodeMirarInvalidOption>();
        PodeMirarSensor.CollectTargets(main,map,terrain,SensorMovementMode.MoveuAndando,opts,invalid,fromCell:meetingCell,targetCandidates:new[]{extra});
        var errors=UnityEngine.JsonUtility.ToJson(new UnityEngine.Vector3Int());foreach(var item in invalid) errors+=UnityEngine.JsonUtility.ToJson(item);
        throw new System.Exception("active="+extra.gameObject.activeInHierarchy+" fire="+main.GetEmbarkedWeapons()[0].CanFireAtLayer(main.GetDomain(),main.GetHeightLevel())+" layer="+main.GetDomain()+"/"+main.GetHeightLevel()+" enemy="+PlayerSlotRelations.AreEnemies(main,extra)+" targetCell="+extra.CurrentCellPosition+" originCell="+meetingCell+" dist="+SectorManager.HexDistance(meetingCell,extra.CurrentCellPosition)+" weapons="+main.GetEmbarkedWeapons().Count+" enemyAlive="+(!extra.IsDead)+" enemyEmbarked="+extra.IsEmbarked+" slots="+main.SlotIndex+"/"+extra.SlotIndex+" opts="+opts.Count+" invalid="+errors+" Falhou: paths="+paths.Count+" visible="+visible.Count+" can="+can+" simulated="+simulated+" hp="+main.CurrentHP+"/"+extra.CurrentHP+" repair="+main.IsUnderRepair);
    }
    var attack=(PlayerAction)args[5];
    if(attack.SensorAction!=SensorActionType.Attack || attack.TargetInstanceId!=extra.InstanceId.ToString())throw new System.Exception("Alvo incorreto.");
    if(attack.MoveTo!=meetingCell && SectorManager.HexDistance(attack.MoveTo,rendezvous.lzCell)!=1)throw new System.Exception("Abandonou a LZ.");
    UnityEngine.Vector3Int betterCell=meetingCell;
    foreach(var cell in paths.Keys)
        if(cell!=meetingCell && cell!=main.CurrentCellPosition && cell!=extra.CurrentCellPosition
            && SectorManager.HexDistance(cell,rendezvous.lzCell)==1 && SectorManager.HexDistance(cell,extra.CurrentCellPosition)==1){betterCell=cell;break;}
    if(betterCell==meetingCell)throw new System.Exception("Teste sem segunda posicao de tiro.");
    var forest=Asset<TerrainTypeData>("forest");
    Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain,forest});
    typeof(TerrainDatabase).GetMethod("RebuildLookup",flags).Invoke(terrain,null);
    map.SetTile(betterCell,forest.paletteTile);
    UnitReachEnvelopeService.ClearCache();
    foreach(var field in new[]{"aiDpqByCell","aiDpqByCellLayer"}){var cache=typeof(AIController).GetField(field,flags).GetValue(ai);cache.GetType().GetMethod("Clear").Invoke(cache,null);}
    paths=UnitMovementPathRules.CalcularCaminhosValidos(map,main,3,terrain);args[4]=paths;
    if(!(bool)helper.Invoke(ai,args) || ((PlayerAction)args[5]).MoveTo!=betterCell)
        throw new System.Exception("Nao escolheu o melhor DPQ entre posicoes que preservam o encontro.");
    if(main.CurrentHP!=10 || extra.CurrentHP!=3)throw new System.Exception("Simulacao aplicou dano.");
    wait.SensorAction=SensorActionType.Embark;
    if((bool)helper.Invoke(ai,args))throw new System.Exception("Ataque tomou prioridade do embarque.");wait.SensorAction=SensorActionType.None;
    Set(main,"isUnderRepair",true);if((bool)helper.Invoke(ai,args))throw new System.Exception("Ferido abandonou recuperacao.");Set(main,"isUnderRepair",false);
    Set(extra,"currentHP",10);
    if((bool)helper.Invoke(ai,args))throw new System.Exception("Aceitou luta sem abate/perda pequena.");
    return "PASS: abate 10 vs3 em planicie, melhor DPQ e LZ preservada, embarque imediato vence, recuperacao preservada e luta sem abate recusada. Nenhuma acao executada.";
}finally{
    foreach(var u in units) UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
