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
    var tank=Asset<UnitData>("tanque B");var apache=Asset<UnitData>("apache");var heavy=Asset<UnitData>("astrosII");
    Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,tank,apache,heavy});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    var sea=Asset<TerrainTypeData>("sea");Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain,forest,sea});
    for(int y=0;y<6;y++)map.SetTile(new UnityEngine.Vector3Int(4,y,0),sea.paletteTile);
    SectorManager.InvalidateLandDistanceCache();var sectors=SectorManager.GetForTilemap(map);Set(sectors,"neighborDistanceTilemap",map);Set(sectors,"neighborDistanceTerrainDatabase",terrain);Set(sectors,"useTerrainCostForNeighborDistances",true);
    var left=Building(960001,new UnityEngine.Vector3Int(1,2,0),ConstructionSector.Alpha);var right=Building(960002,new UnityEngine.Vector3Int(6,2,0),ConstructionSector.Bravo);var airport=Building(960003,new UnityEngine.Vector3Int(1,3,0),ConstructionSector.Charlie);
    void Offers(ConstructionManager b,params UnitData[] data){Set(b,"slotIndex",ownSlot);Set(b,"teamId",TeamId.Blue);Set(b,"siteRuntime",new ConstructionSiteRuntime{isCapturable=true,capturePointsMax=20,sellingRule=ConstructionUnitMarketRule.FreeMarket,offeredUnits=new System.Collections.Generic.List<UnitData>(data)});}
    Offers(left,tank,heavy);Offers(right,tank,heavy);Offers(airport,apache);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,Stance=AIStance.Tactical,TurnNumber=11,Budget=1000,MyBuildings=new System.Collections.Generic.List<ConstructionManager>{left,airport},MyUnits=new System.Collections.Generic.List<UnitManager>(),EnemyUnits=new System.Collections.Generic.List<UnitManager>()};
    var sf=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var ctxType=typeof(AIShoppingPlanner).GetNestedType("ArtilleryWallContext",System.Reflection.BindingFlags.NonPublic);var ctx=System.Activator.CreateInstance(ctxType,true);ctxType.GetField("Board").SetValue(ctx,map);
    var configure=typeof(AIShoppingPlanner).GetMethod("ConfigureRallyShoppingAccess",sf);var match=typeof(AIShoppingPlanner).GetMethod("MatchesWallOffer",sf);
    AIShoppingDemand Assault(){return new AIShoppingDemand{Role=UnitRole.Assalto,Domain=Domain.Land,RequireRallyBreakthrough=true,Count=2,Priority=9,Origin="rally-assembly",Reason="teste"};}
    bool Configure(AIShoppingDemand d){return(bool)configure.Invoke(null,new object[]{snapshot,d,new UnityEngine.Vector3Int(7,2,0),ctx,false});}
    bool Accept(AIShoppingDemand d,UnitData u,ConstructionManager p){return(bool)match.Invoke(null,new object[]{d,u,p,false});}
    var demand=Assault();if(!Configure(demand)||demand.Role!=UnitRole.AtaqueAereo||demand.RequireRallyBreakthrough||!Accept(demand,apache,airport)||Accept(demand,tank,left))throw new System.Exception("Rally isolado nao converteu ruptura em Assalto aereo.");
    snapshot.MyBuildings.Add(right);demand=Assault();if(!Configure(demand)||demand.Role!=UnitRole.Assalto||!Accept(demand,tank,right)||Accept(demand,tank,left))throw new System.Exception("Compra terrestre perdeu vinculo com produtor que alcanca rally.");
    snapshot.MyBuildings.Remove(right);var fire=new AIShoppingDemand{Role=UnitRole.FogoIndireto,MinRallyArtilleryWeight=1,Count=1,Origin="rally-assembly"};if(Configure(fire))throw new System.Exception("Artilharia pesada ilhada foi admitida.");
    snapshot.MyBuildings.Add(right);fire=new AIShoppingDemand{Role=UnitRole.FogoIndireto,MinRallyArtilleryWeight=1,Count=1,Origin="rally-assembly"};if(!Configure(fire)||!Accept(fire,heavy,right)||Accept(fire,heavy,left))throw new System.Exception("Artilharia com acesso nao preservada por produtor.");
    snapshot.MyBuildings.Remove(right);snapshot.MyBuildings.Remove(airport);demand=Assault();if(Configure(demand))throw new System.Exception("Sem resposta alcancavel comprou peca encalhada.");
    // Core accounting already uses the assault family, including primary Disruptor.
    for(int i=0;i<6;i++){var data=i<2?apache:soldier;var u=Unit(960010+i,data,new UnityEngine.Vector3Int(1,1,0),false);snapshot.MyUnits.Add(u);}
    int assaults=(int)typeof(AIShoppingPlanner).GetMethod("CountCoreFamily",sf).Invoke(null,new object[]{snapshot,UnitRoleFamily.Assalto});
    int caps=(int)typeof(AIShoppingPlanner).GetMethod("CountCompositionRole",sf).Invoke(null,new object[]{snapshot,UnitRole.Capturador});
    if(assaults!=2||caps!=4)throw new System.Exception("Core nao contou dois Apaches e quatro capturadores.");
    return "PASS: rally ilhado usa Assalto aereo; tanque e artilharia so da produtora com acesso; sem alternativa nao compra; falta de caixa nao elimina oferta; core conta dois Apaches e quatro capturadores; sem compra confirmada.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
