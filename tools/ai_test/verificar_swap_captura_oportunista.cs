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
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    var first=Building(880011,new UnityEngine.Vector3Int(3,2,0),ConstructionSector.Lima);
    var second=Building(880012,new UnityEngine.Vector3Int(2,2,0),ConstructionSector.Juliet);
    foreach(var b in new[]{first,second}){b.Apply(city);Set(b,"slotIndex",-1);Set(b,"teamId",(TeamId)(-1));Set(b,"currentCapturePoints",10);}
    var occupant=Unit(880058,soldier,first.CurrentCellPosition,false);
    var incoming=Unit(880015,soldier,new UnityEngine.Vector3Int(4,2,0),false);
    var third=Unit(880016,soldier,new UnityEngine.Vector3Int(8,5,0),false);
    void Position(UnitManager u,UnityEngine.Vector3Int cell){var from=u.CurrentCellPosition;Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,from,cell);}
    foreach(var u in new[]{occupant,incoming,third}){
        var cell=u.CurrentCellPosition;u.gameObject.SetActive(true);u.Apply(soldier);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"isEmbarked",false);Set(u,"remainingMovementPoints",3);Set(u,"hasActed",false);Position(u,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);
    }
    Set(occupant,"currentHP",4);Set(incoming,"currentHP",10);Set(third,"currentHP",10);
    var host=new UnityEngine.GameObject("SwapOpportunityAI");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var matchHost=new UnityEngine.GameObject("SwapOpportunityMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,MyUnits=new System.Collections.Generic.List<UnitManager>{occupant,incoming,third},EnemyUnits=new System.Collections.Generic.List<UnitManager>(),EnemyHQ=second};
    var decide=typeof(AIController).GetMethod("DecideSwapVacateAction",flags);
    PlayerAction Decide(){return(PlayerAction)decide.Invoke(ai,new object[]{occupant,incoming,snapshot});}
    var action=Decide();
    if(action==null||action.SensorAction!=SensorActionType.Capture||action.MoveTo!=second.CurrentCellPosition||action.MoveFrom!=first.CurrentCellPosition)throw new System.Exception("Swap nao capturou outro predio antes de apenas mover.");
    var paths=UnitMovementPathRules.CalcularCaminhosValidos(map,occupant,3,terrain);
    var reserve=typeof(AIController).GetMethod("ShouldReserveOpportunisticCaptureForCloserUnit",flags);
    object[] normalArgs={occupant,TeamId.Blue,second.CurrentCellPosition,paths,null,null};
    if(!(bool)reserve.Invoke(ai,normalArgs)||(UnitManager)normalArgs[4]!=incoming)throw new System.Exception("Fixture nao exercita a disputa com o substituto mais forte.");
    object[] swapArgs={occupant,TeamId.Blue,second.CurrentCellPosition,paths,null,incoming};
    if((bool)reserve.Invoke(ai,swapArgs))throw new System.Exception("Substituto comprometido monopolizou a segunda captura.");
    // A different available capturer still retains the existing reservation.
    Position(third,new UnityEngine.Vector3Int(2,3,0));swapArgs[4]=null;
    if(!(bool)reserve.Invoke(ai,swapArgs)||(UnitManager)swapArgs[4]!=third)throw new System.Exception("Swap ignorou reserva legitima de terceiro.");
    action=Decide();if(action!=null&&action.SensorAction==SensorActionType.Capture)throw new System.Exception("Swap tomou captura reservada para terceiro.");
    Position(third,new UnityEngine.Vector3Int(8,5,0));Set(second,"slotIndex",ownSlot);Set(second,"teamId",TeamId.Blue);Set(second,"currentCapturePoints",second.CapturePointsMax);
    action=Decide();if(action==null||action.SensorAction!=SensorActionType.None||action.MoveTo==first.CurrentCellPosition)throw new System.Exception("Sem captura/combate, movimento de fallback nao liberou o predio.");
    if(occupant.CurrentCellPosition!=first.CurrentCellPosition||occupant.CurrentHP!=4||occupant.HasActed||first.CurrentCapturePoints!=10)throw new System.Exception("Planejamento confirmou movimento/captura/HP.");
    return "PASS: swap captura outro predio antes de mover; exclui hex cedido; substituto comprometido nao monopoliza oportunidades; reserva de terceiro e regra normal preservadas; sem outra captura/combate move como fallback; sem mutacao confirmada.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
