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
    UnitData Clone(string id){var d=UnityEngine.Object.Instantiate(Asset<UnitData>(id));created.Add(d);d.bannedOnHardMode=false;return d;}
    var assault=Clone("apache");var b=Clone("cacaB");var a=Clone("cacaA");a.eliteFrom=b;var f=Clone("cacaA");f.id="fixture-cacaF";f.eliteLevel=2;f.eliteFrom=a;f.cost=40000;
    Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,b,a,f,assault});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    var airportData=UnityEngine.Object.Instantiate(city);created.Add(airportData);airportData.isAirport=true;Set(constructionDb,"constructions",new System.Collections.Generic.List<ConstructionData>{airportData});typeof(ConstructionDatabase).GetMethod("RebuildLookup",flags).Invoke(constructionDb,null);
    var ownAirport=Building(980001,new UnityEngine.Vector3Int(1,2,0),ConstructionSector.Alpha);var enemyAirport=Building(980002,new UnityEngine.Vector3Int(8,2,0),ConstructionSector.Bravo);
    void Offers(ConstructionManager building,int slot,params UnitData[] data){Set(building,"slotIndex",slot);Set(building,"teamId",slot==ownSlot?TeamId.Blue:TeamId.Yellow);Set(building,"siteRuntime",new ConstructionSiteRuntime{isPlayerHeadQuarter=true,isCapturable=true,capturePointsMax=20,sellingRule=ConstructionUnitMarketRule.FreeMarket,offeredUnits=new System.Collections.Generic.List<UnitData>(data)});}
    Offers(ownAirport,ownSlot,b,a,f);Offers(enemyAirport,ownSlot==0?1:0,b,a);
    var fighter=Unit(980010,b,new UnityEngine.Vector3Int(2,2,0),false);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,Stance=AIStance.Tactical,TurnNumber=12,Budget=100000,IncomePerTurn=30000,MyBuildings=new System.Collections.Generic.List<ConstructionManager>{ownAirport},EnemyBuildings=new System.Collections.Generic.List<ConstructionManager>{enemyAirport},MyUnits=new System.Collections.Generic.List<UnitManager>{fighter},EnemyUnits=new System.Collections.Generic.List<UnitManager>()};
    for(int i=0;i<6;i++)snapshot.MyUnits.Add(Unit(980030+i,i<4?soldier:assault,new UnityEngine.Vector3Int(2+i%3,4,0),false));
    var sf=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var progression=typeof(AIShoppingPlanner).GetMethod("AddEliteProgressionDemand",sf);var reserve=typeof(AIShoppingPlanner).GetMethod("FindEliteInterceptorReserveTarget",sf);var select=typeof(AIShoppingPlanner).GetMethod("SelectEliteReserveTarget",sf);
    var count=typeof(AIShoppingPlanner).GetMethod("CountCompositionRole",sf);
    System.Collections.Generic.List<AIShoppingDemand> Demand(){var ds=new System.Collections.Generic.List<AIShoppingDemand>();int active=(int)count.Invoke(null,new object[]{snapshot,UnitRole.Interceptador});progression.Invoke(null,new object[]{snapshot,ds,UnitRole.Interceptador,active,25,1});return ds;}
    if(UnitRoleCompatibility.ResolveCompositionRole(a)!=UnitRole.Interceptador||UnitRoleCompatibility.ResolveCompositionRole(b)!=UnitRole.Interceptador)throw new System.Exception("Ficha nao resolve Interceptador.");
    var ds=Demand();if(ds.Count!=1||!ds[0].IsPreventiveAirControl||ds[0].MinEliteLevel!=1||ds[0].MaxEliteLevel!=1||ds[0].Origin!="elite"||!ds[0].Reason.Contains("controle do ar"))throw new System.Exception("Aeroporto sem bomber nao pediu Caca A preventivo.");
    foreach(var stance in new[]{AIStance.Tactical,AIStance.Defensive,AIStance.Offensive}){snapshot.Stance=stance;if(Demand().Count!=1)throw new System.Exception("Either nao permitiu controle em "+stance);}snapshot.Stance=AIStance.Tactical;
    if((UnitData)reserve.Invoke(null,new object[]{snapshot})!=a)throw new System.Exception("Reserva nao encontrou terceira linha.");
    var orders=new System.Collections.Generic.List<AIShoppingPlanner.ShoppingOrder>();var occupied=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
    object[] preArgs={snapshot,ds[0],null,orders,new System.Collections.Generic.HashSet<ConstructionManager>(),occupied,snapshot.Budget,snapshot.Budget,"controle_do_ar","fixture"};
    if(!(bool)typeof(AIShoppingPlanner).GetMethod("TryPreCommitDemand",sf).Invoke(null,preArgs)||orders.Count!=1||orders[0].UnitToBuy!=a||ds[0].Count!=0)throw new System.Exception("Pre-compra existente nao garantiu piso A.");
    snapshot.EnemyBuildings.Clear();if(Demand().Count!=0||reserve.Invoke(null,new object[]{snapshot})!=null)throw new System.Exception("Sem aviacao inimiga pediu caca.");
    var enemy=Unit(980020,b,new UnityEngine.Vector3Int(8,3,0),false);snapshot.EnemyUnits.Add(enemy);if(Demand().Count!=1)throw new System.Exception("Aeronave vista nao ativou controle.");
    var tank=Clone("tanqueA");var fire=Clone("sam");
    if((UnitData)select.Invoke(null,new object[]{snapshot,tank,a,fire})!=a)throw new System.Exception("Ar visivel nao priorizou interceptor.");
    snapshot.EnemyUnits.Clear();snapshot.EnemyBuildings.Add(enemyAirport);if((UnitData)select.Invoke(null,new object[]{snapshot,tank,a,fire})!=tank)throw new System.Exception("Sem aeronave visivel trocou prioridade do Assalto.");
    var top=Unit(980011,a,new UnityEngine.Vector3Int(2,3,0),false);snapshot.MyUnits.Add(top);if(Demand().Count!=0||reserve.Invoke(null,new object[]{snapshot})!=null)throw new System.Exception("Caca A em campo escalou para F ou duplicou piso.");
    snapshot.MyUnits.Clear();if(Demand().Count!=0||reserve.Invoke(null,new object[]{snapshot})!=null)throw new System.Exception("Sem cadeia comprou A.");
    snapshot.MyUnits.Add(fighter);snapshot.IncomePerTurn=0;if(Demand().Count!=0)throw new System.Exception("Core/gate economico ignorado.");
    if(fighter.HasActed)throw new System.Exception("Consulta alterou acao.");
    return "PASS: aeroporto sem bomber pede 1 Interceptador elite 1; sem aviacao nao pede; cadeia B e teto A preservados; reserva paralela prioriza ar visivel e conserva Assalto sem avistamento; core/economia respeitados; sem compra confirmada.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
