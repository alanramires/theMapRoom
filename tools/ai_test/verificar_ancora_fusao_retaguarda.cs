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
    var apache=Asset<UnitData>("apache");
    Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,apache});
    typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    if(ownSlot<0)throw new System.Exception("Sem slot Blue para fixture.");
    var receiver=Unit(800065,apache,new UnityEngine.Vector3Int(2,2,0),false);
    var partner=Unit(800035,apache,new UnityEngine.Vector3Int(5,2,0),false);
    var enemy=Unit(800132,soldier,new UnityEngine.Vector3Int(5,3,0),false);
    foreach(var u in new[]{receiver,partner,enemy}){
        u.gameObject.SetActive(true);Set(u,"boardTilemap",map);Set(u,"isDead",false);Set(u,"isEmbarked",false);
        Set(u,"slotIndex",u==enemy?(ownSlot==0?1:0):ownSlot);Set(u,"teamId",u==enemy?TeamId.Yellow:TeamId.Blue);
        if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);
    }
    void Position(UnitManager u,int x,int y){var cell=new UnityEngine.Vector3Int(x,y,0);Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,cell,cell);}
    Position(receiver,2,2);Position(partner,5,2);Position(enemy,5,3);
    foreach(var u in new[]{receiver,partner}){Set(u,"currentDomain",Domain.Air);Set(u,"currentHeightLevel",HeightLevel.AirLow);Set(u,"isUnderRepair",true);Set(u,"currentFuel",56);}
    Set(receiver,"currentHP",2);Set(partner,"currentHP",3);Set(partner,"hasActed",true);Set(receiver,"remainingMovementPoints",5);
    snapshot.AISlotIndex=ownSlot;snapshot.MyUnits=new System.Collections.Generic.List<UnitManager>{receiver,partner};snapshot.EnemyUnits=new System.Collections.Generic.List<UnitManager>{enemy};
    var collect=typeof(AIController).GetMethod("CollectAircraftRecoveryAnchors",flags);
    var safe=typeof(AIController).GetMethod("IsRepairFusionSiteSafe",flags);
    var potential=typeof(AIController).GetMethod("IsPotentialRepairFusionTarget",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
    if(!(bool)potential.Invoke(null,new object[]{receiver,partner}))throw new System.Exception("Parceiro 2+3 HP nao era compativel no fixture.");
    bool FusionAnchor(){foreach(var anchor in(System.Collections.IEnumerable)collect.Invoke(ai,new object[]{receiver,snapshot}))if((string)anchor.GetType().GetField("Kind").GetValue(anchor)=="fusao")return true;return false;}
    if((bool)safe.Invoke(ai,new object[]{receiver,snapshot,partner.CurrentCellPosition})||FusionAnchor())
        throw new System.Exception("Parceiro cercado virou ancora de recuperacao.");
    if(FusionAnchor())throw new System.Exception("Segunda avaliacao reintroduziu fusao insegura.");
    if(rejections.Count!=1||!rejections[0].Contains("célula sob ameaça"))throw new System.Exception("Recusa nao registrada uma unica vez com motivo.");
    object Member(object value,string name){return value.GetType().GetField(name)?.GetValue(value)??value.GetType().GetProperty(name)?.GetValue(value);}
    var paths=UnitMovementPathRules.CalcularCaminhosValidos(map,receiver,receiver.RemainingMovementPoints,terrain);
    var recovery=typeof(AIController).GetMethod("EvaluateAircraftRecoveryReach",flags).Invoke(ai,new object[]{receiver,snapshot,receiver.CurrentCellPosition,paths});
    if((bool)Member(recovery,"Found")){
        var decision=Member(recovery,"Decision");if((string)Member(decision,"Reason")=="fusao")throw new System.Exception("Recuperacao escolheu parceiro cercado.");
    }else{
        snapshot.MyHQ=first;
        object[] fallback={receiver,snapshot,receiver.CurrentCellPosition,paths,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),null,false};
        if(!(bool)typeof(AIController).GetMethod("TryDecideRepairFallbackToHQ",flags).Invoke(ai,fallback))throw new System.Exception("Sem recuperacao, HQ nao disponivel como saida.");
    }
    Position(partner,1,2);Position(enemy,9,5);snapshot.EnemyUnits.Clear();
    if(!(bool)safe.Invoke(ai,new object[]{receiver,snapshot,partner.CurrentCellPosition})||!FusionAnchor())
        throw new System.Exception("Parceiro compativel em celula segura perdeu ancora.");
    if(receiver.CurrentHP!=2||partner.CurrentHP!=3||receiver.HasActed)throw new System.Exception("Consulta alterou estado confirmado.");
    return "PASS: parceiro compativel 2+3 HP cercado nao cria ancora; motivo unico apesar de avaliacao repetida; recuperacao nao escolhe fusao insegura; mesmo parceiro seguro cria ancora; sem fusao executada ou mutacao de HP.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
