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
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    var aircraft=Unit(900002,chinook,new UnityEngine.Vector3Int(2,2,0),false);
    var supplier=Unit(900067,truck,new UnityEngine.Vector3Int(3,2,0),false);
    var patient=Unit(900001,soldier,new UnityEngine.Vector3Int(4,2,0),false);
    foreach(var u in new[]{aircraft,supplier,patient}){
        var cell=u.CurrentCellPosition;u.gameObject.SetActive(true);u.Apply(u==aircraft?chinook:u==supplier?truck:soldier);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"isEmbarked",false);Set(u,"hasActed",false);Set(u,"remainingMovementPoints",u.GetMovementRange());Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,cell,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);
    }
    Set(patient,"currentHP",2);Set(patient,"currentFuel",patient.GetMaxFuel());Set(patient,"isUnderRepair",true);
    Set(aircraft,"currentHP",9);Set(aircraft,"isUnderRepair",true);Set(aircraft,"currentDomain",Domain.Air);Set(aircraft,"currentHeightLevel",HeightLevel.AirLow);
    var host=new UnityEngine.GameObject("LogisticsNeedAI");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var matchHost=new UnityEngine.GameObject("LogisticsNeedMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,MyUnits=new System.Collections.Generic.List<UnitManager>{aircraft,supplier,patient},EnemyUnits=new System.Collections.Generic.List<UnitManager>()};
    var resolve=typeof(AIController).GetMethod("ResolveLogisticsNeed",flags);
    object Need(UnitManager u){return resolve.Invoke(ai,new object[]{snapshot,u});}
    object Member(object o,string name){return o.GetType().GetField(name)?.GetValue(o)??o.GetType().GetProperty(name)?.GetValue(o);}
    float Score(UnitManager u){return(float)Member(Need(u),"Score");}
    int Tier(UnitManager u){return System.Convert.ToInt32(Member(Need(u),"Tier"));}
    var fuelQuery=typeof(AIController).GetMethod("IsAircraftFuelCriticalForNextUpkeep",flags);object[] fuelArgs={aircraft,0,0,0};fuelQuery.Invoke(ai,fuelArgs);int threshold=(int)fuelArgs[3];
    Set(aircraft,"currentFuel",threshold);
    if(Tier(aircraft)!=3||Tier(patient)!=2||Score(aircraft)<=Score(patient))throw new System.Exception("No limite do upkeep Chinook nao venceu soldado critico.");
    var paths=UnitMovementPathRules.CalcularCaminhosValidos(map,supplier,supplier.RemainingMovementPoints,terrain);
    var occupied=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
    var find=typeof(AIController).GetMethod("FindLogisticsServiceTarget",flags);
    UnitManager Approach(){
        // Evaluate from a previous position, then place the fixture at the
        // service origin to compare the actual arrival sensor ordering.
        var arrival=supplier.CurrentCellPosition;var origin=new UnityEngine.Vector3Int(0,2,0);
        Set(supplier,"currentCellPosition",origin);supplier.transform.position=HexCoordinates.GetCellCenterWorld(map,origin);UnitOccupancyRules.NotifyUnitOccupancyChanged(supplier,arrival,origin);
        try{var approachPaths=UnitMovementPathRules.CalcularCaminhosValidos(map,supplier,supplier.RemainingMovementPoints,terrain);return(UnitManager)find.Invoke(ai,new object[]{supplier,snapshot,origin,approachPaths,occupied,false,AIReachDecisionStages.Operational});}
        finally{Set(supplier,"currentCellPosition",arrival);supplier.transform.position=HexCoordinates.GetCellCenterWorld(map,arrival);UnitOccupancyRules.NotifyUnitOccupancyChanged(supplier,origin,arrival);}
    }
    var collect=typeof(AIController).GetMethod("CollectLogisticsTargetsBySupplySensorAtCell",flags);
    UnitManager Serve(){object[] args={supplier,snapshot,supplier.CurrentCellPosition,1,true,0,0,null};var list=(System.Collections.Generic.List<UnitManager>)collect.Invoke(ai,args);if(list.Count==0)throw new System.Exception("Sensor nao ofereceu atendimento: "+args[7]);return list[0];}
    if(Approach()!=aircraft||Serve()!=aircraft)throw new System.Exception("Aproximacao/atendimento divergiram na emergencia de autonomia.");
    Set(aircraft,"currentFuel",threshold+1);
    if(Tier(aircraft)!=1||Score(patient)<=Score(aircraft)||Approach()!=patient||Serve()!=patient)throw new System.Exception("Acima do limite soldado critico nao venceu nas duas etapas.");
    var expensive=UnityEngine.Object.Instantiate(soldier);created.Add(expensive);expensive.id="rank_test_elite";expensive.cost=1000000;expensive.eliteLevel=1000000;
    var dbUnits=new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,expensive};Set(unitDb,"units",dbUnits);typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    var elite=Unit(900003,expensive,new UnityEngine.Vector3Int(8,4,0),false);elite.Apply(expensive);Set(elite,"currentHP",9);Set(elite,"currentFuel",elite.GetMaxFuel());Set(elite,"isUnderRepair",true);
    if(Tier(elite)!=1||Score(elite)>=Score(patient)||(float)Member(Need(elite),"Value")>9000f)throw new System.Exception("Preco/elite sem teto atravessou faixa critica.");
    expensive.cost=soldier.cost;expensive.eliteLevel=1;Set(patient,"currentHP",6);Set(elite,"currentHP",6);
    if(Tier(elite)!=Tier(patient)||Score(elite)<=Score(patient))throw new System.Exception("Elite nao desempatou necessidades equivalentes.");
    Set(elite,"isUnderRepair",false);Set(elite,"currentHP",9);
    if(Tier(elite)!=0||Score(elite)>=Score(patient))throw new System.Exception("Preventivo ultrapassou corretivo.");
    float severity=(float)Member(Need(patient),"Severity");Set(patient,"currentFuel",patient.GetMaxFuel()/2);
    if((float)Member(Need(patient),"Severity")<=severity)throw new System.Exception("Combustivel sumiu no corretivo.");
    severity=(float)Member(Need(patient),"Severity");foreach(var weapon in patient.GetEmbarkedWeapons())weapon.squadAmmunition=0;
    if((float)Member(Need(patient),"Severity")<=severity)throw new System.Exception("Municao nao participa proporcionalmente do corretivo.");
    string diagnostic=(string)Member(Need(patient),"Diagnostic");foreach(var component in new[]{"faixa=","gravidade=","valor=","estrategico="})if(!diagnostic.Contains(component))throw new System.Exception("Diagnostico sem "+component);
    // Even extreme contextual bonuses cannot cross tiers.
    object lower=Need(elite),higher=Need(patient);float boosted=(float)lower.GetType().GetMethod("WithContext").Invoke(lower,new object[]{float.MaxValue});float penalized=(float)higher.GetType().GetMethod("WithContext").Invoke(higher,new object[]{float.MinValue});
    if(boosted>=penalized)throw new System.Exception("Contexto quebrou faixa estrita.");
    if(aircraft.CurrentHP!=9||aircraft.CurrentFuel!=threshold+1||aircraft.HasActed||aircraft.GetDomain()!=Domain.Air||patient.HasActed)throw new System.Exception("Ranking executou atendimento ou mudou camada.");
    return "PASS: Chinook no limite de autonomia vence soldado 2HP; acima perde; aproximacao e atendimento pelo sensor concordam nos dois casos; cara levemente ferida perde para barata critica; elite desempata; valor tem teto; preventivo abaixo do corretivo; combustivel e municao proporcionais; contexto nao atravessa faixas; diagnostico completo; consultas sem atendimento confirmado.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
