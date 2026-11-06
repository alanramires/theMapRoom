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
        new UnitTransportSeatRuntime{embarkedUnit=main,embarkedOnTurn=1},new UnitTransportSeatRuntime{embarkedUnit=extra,embarkedOnTurn=2}});
    var aiHost=new UnityEngine.GameObject("CourierTestAI");aiHost.SetActive(false);var ai=aiHost.AddComponent<AIController>();
    Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var matchHost=new UnityEngine.GameObject("CourierTestMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();
    Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=0,MyUnits=new System.Collections.Generic.List<UnitManager>{ally}};
    bool Served(){return(bool)typeof(AIController).GetMethod("IsAirDeliveryCaptureAlreadyServed",flags).Invoke(ai,new object[]{first.CurrentCellPosition,null,snapshot});}
    if(!Served()){
        bool legal=PodeCapturarSensor.TryGetCaptureTargetAtCell(ally,map,first.CurrentCellPosition,SensorMovementMode.MoveuAndando,out _,out _,out var why,match);
        var routes=UnitMovementPathRules.CalcularCaminhosValidos(map,ally,ally.RemainingMovementPoints,terrain);
        throw new System.Exception("Capturador nao reconhecido. legal="+legal+" reason="+why+" routes="+routes?.Count+" target="+first.CurrentCellPosition+" origin="+ally.CurrentCellPosition+" mp="+ally.RemainingMovementPoints+" key="+PodeCapturarSensor.HasAnyCaptureKey(ally)+" hasRoute="+routes.ContainsKey(first.CurrentCellPosition)+" dead="+ally.IsDead+" acted="+ally.HasActed+" repair="+ally.IsUnderRepair+" intent="+ally.AIDesignatedMissionIntent+" targetMission="+ally.AIDesignatedMissionTargetCell);
    }
    Set(ally,"hasActed",true);if(Served())throw new System.Exception("Contou aliado que ja agiu.");Set(ally,"hasActed",false);
    Set(ally,"aiDesignatedMissionIntent",AIPlanRuntimeIntent.None);if(Served())throw new System.Exception("Proximidade sem compromisso contou como captura garantida.");Mission(ally,first.CurrentCellPosition);
    typeof(AIController).GetMethod("PromoteUnservedAirPassenger",flags).Invoke(ai,new object[]{carrier,cargo,null,snapshot,UnityEngine.Vector3Int.zero});
    var resolve=typeof(AIController).GetMethod("ResolvePrimaryPassenger",flags);
    if((UnitManager)resolve.Invoke(ai,new object[]{carrier,cargo,null})!=extra)throw new System.Exception("Secundario nao promovido.");
    var ordered=(System.Collections.Generic.List<UnitManager>)typeof(AIController).GetMethod("OrderPassengersForDelivery",flags).Invoke(ai,new object[]{carrier,cargo});
    if(ordered[0]!=extra)throw new System.Exception("Ranking/LZ nao segue promovido.");
    Set(ally,"hasActed",true);
    typeof(AIController).GetMethod("PromoteUnservedAirPassenger",flags).Invoke(ai,new object[]{carrier,cargo,null,snapshot,UnityEngine.Vector3Int.zero});
    if((UnitManager)resolve.Invoke(ai,new object[]{carrier,cargo,null})!=extra)throw new System.Exception("Prioridade oscilou dentro da mesma entrega.");
    Set(extra,"isEmbarked",false);
    if((UnitManager)resolve.Invoke(ai,new object[]{carrier,cargo,null})!=main)throw new System.Exception("Promovido ja entregue continuou mandando.");
    Set(extra,"isEmbarked",true);Set(ally,"hasActed",false);
    var third=Building(800003,new UnityEngine.Vector3Int(4,3,0),ConstructionSector.Foxtrot);
    object[] addressArgs={main,first.CurrentCellPosition,null,snapshot,null,null};
    if(!(bool)typeof(AIController).GetMethod("TryResolveUnservedAirCapture",flags).Invoke(ai,addressArgs)
        || (UnityEngine.Vector3Int)addressArgs[5]!=third.CurrentCellPosition)
        throw new System.Exception("Ignorou outro predio util no mesmo setor.");
    typeof(AIController).GetMethod("PromoteUnservedAirPassenger",flags).Invoke(ai,new object[]{carrier,cargo,null,snapshot,UnityEngine.Vector3Int.zero});
    if((UnitManager)resolve.Invoke(ai,new object[]{carrier,cargo,null})!=main)throw new System.Exception("Promoveu sem necessidade quando havia outro trabalho no setor.");
    ConstructionManager.AllActive.Remove(third);UnityEngine.Object.DestroyImmediate(third.gameObject);Set(ally,"hasActed",true);
    var greedy=typeof(AIController).GetMethod("TryFindGreedyCaptureFromSpot",flags);
    var emptySnapshot=new AIWorldSnapshot();
    var claims=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
    object[] args={main,first.CurrentCellPosition,null,claims,emptySnapshot,null};
    if(!(bool)greedy.Invoke(ai,args))throw new System.Exception("Baseline captura oportunista livre indisponivel.");
    claims.Add(first.CurrentCellPosition);claims.Add(second.CurrentCellPosition);
    if((bool)greedy.Invoke(ai,args))throw new System.Exception("Desembarcou oportunista para alvo ja entregue.");
    claims.Remove(second.CurrentCellPosition);
    args[1]=new UnityEngine.Vector3Int(6,2,0);
    if(!(bool)greedy.Invoke(ai,args) || (UnityEngine.Vector3Int)args[2]!=second.CurrentCellPosition)throw new System.Exception("Impediu segundo passageiro de trabalhar num predio distinto.");
    var enemy=Unit(800099,soldier,first.CurrentCellPosition,false);Set(enemy,"slotIndex",1);
    emptySnapshot.EnemyUnits.Add(enemy);args[1]=first.CurrentCellPosition;
    if(!(bool)greedy.Invoke(ai,args))throw new System.Exception("Bloqueou reforco em alvo sob pressao confirmada.");
    if(main.AIDesignatedMissionTargetCell!=first.CurrentCellPosition || extra.AIDesignatedMissionTargetCell!=second.CurrentCellPosition)
        throw new System.Exception("A selecao alterou as missoes originais.");
    return "PASS: captura ja atendida exige compromisso e acao; secundario promovido e LZ reordenada; prioridade estavel; oportunista exige predio distinto; sem alteracao de missao/captura.";
}finally{
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
