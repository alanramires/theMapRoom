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
    UnitData Clone(string id){var data=UnityEngine.Object.Instantiate(Asset<UnitData>(id));created.Add(data);data.bannedOnHardMode=false;return data;}
    var tank=Clone("tanque B");var elite=Clone("tanqueA");var apache=Clone("apache");elite.eliteFrom=tank;
    var factory=Building(970001,new UnityEngine.Vector3Int(1,2,0),ConstructionSector.Alpha);var airport=Building(970002,new UnityEngine.Vector3Int(1,3,0),ConstructionSector.Bravo);
    void Offers(ConstructionManager b,params UnitData[] data){Set(b,"slotIndex",ownSlot);Set(b,"teamId",TeamId.Blue);Set(b,"siteRuntime",new ConstructionSiteRuntime{isCapturable=true,capturePointsMax=20,sellingRule=ConstructionUnitMarketRule.FreeMarket,offeredUnits=new System.Collections.Generic.List<UnitData>(data)});}
    Offers(factory,tank,elite);Offers(airport,apache);
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,Stance=AIStance.Tactical,TurnNumber=11,Budget=45000,MyBuildings=new System.Collections.Generic.List<ConstructionManager>{factory,airport},MyUnits=new System.Collections.Generic.List<UnitManager>(),EnemyUnits=new System.Collections.Generic.List<UnitManager>()};
    var sf=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var roster=new AIShoppingPlanner.AIRosterKnowledge();
    void Profile(UnitData data,bool available){var profile=new AIShoppingPlanner.AIRosterProfile{Unit=data,AvailableNow=available,Unlockable=!available};profile.WeaponCategories.Add(WeaponCategory.AntiInfantaria);roster.Profiles.Add(profile);}
    Profile(tank,true);Profile(elite,false);Profile(apache,true);
    var cls=new AIShoppingPlanner.EnemyClassPressureInspection{UnitClass=GameUnitClass.Infantry,CounterCategory=WeaponCategory.AntiInfantaria,Score=8,Unmet=8,Count=8};
    var types=(System.Collections.Generic.Dictionary<UnitData,float>)cls.GetType().GetField("UnitTypeScores",flags).GetValue(cls);types[soldier]=8;
    var off=(System.Collections.Generic.Dictionary<UnitData,float>)cls.GetType().GetField("UnitTypeOffLandScores",flags).GetValue(cls);off[soldier]=8;
    var pressure=new AIShoppingPlanner.CounterPressureInspection{AntiInfantry=8,RawAntiInfantry=8,VisibleUnits=8,OffLandUnits=8};pressure.Classes.Add(cls);
    var find=typeof(AIShoppingPlanner).GetMethod("FindBestEliteCounter",sf);var add=typeof(AIShoppingPlanner).GetMethod("AddCounterPressureDemand",sf);var match=typeof(AIShoppingPlanner).GetMethod("DoesUnitMeetShoppingDemand",sf);
    UnitData Elite(){return(UnitData)find.Invoke(null,new object[]{snapshot,pressure,roster,WeaponCategory.AntiInfantaria,null,false});}
    System.Collections.Generic.List<AIShoppingDemand> Demands(){var demands=new System.Collections.Generic.List<AIShoppingDemand>();add.Invoke(null,new object[]{snapshot,demands,pressure,WeaponCategory.AntiInfantaria,null,8f,0f,8f,12,roster,true,false});return demands;}
    if(Elite()!=null)throw new System.Exception("Elite terrestre isolado entrou na escolha.");
    var ds=Demands();if(ds.Count!=1||ds[0].RequiredUnitId==tank.id||!ds[0].ExcludeLandResponse||(bool)match.Invoke(null,new object[]{tank,ds[0]}))throw new System.Exception("Pre-requisito terrestre furou restricao de ilha.");
    if(!(bool)match.Invoke(null,new object[]{apache,ds[0]}))throw new System.Exception("Resposta aerea basica foi perdida.");
    // Exact-id demands must obey exclusion too, including old/synthetic requirements.
    ds[0].RequiredUnitId=tank.id;if((bool)match.Invoke(null,new object[]{tank,ds[0]}))throw new System.Exception("Ficha exata ignorou exclusao.");
    // Local pressure still permits progression through the basic tank.
    off.Clear();if(Elite()!=elite)throw new System.Exception("Elite terrestre local foi excluido.");
    ds=Demands();if(ds[0].RequiredUnitId!=tank.id||ds[0].ExcludeLandResponse)throw new System.Exception("Cadeia local deixou de pedir Tanque B.");
    // An offered elite air response competes normally, before requesting a prerequisite.
    off[soldier]=8;var airElite=Clone("apache");airElite.id="fixture-air-elite";airElite.eliteLevel=1;airElite.eliteFrom=null;Offers(airport,apache,airElite);Profile(airElite,true);
    if(Elite()!=airElite)throw new System.Exception("Elite aereo nao substituiu elite isolado.");
    ds=Demands();if(ds[0].RequiredUnitId!=airElite.id||!ds[0].ExcludeLandResponse)throw new System.Exception("Ficha exata aerea nao respeitou regra de acesso.");
    return "PASS: pressao em outra ilha exclui elite terrestre antes do pre-requisito; ficha exata nao fura exclusao; Apache basico e elite aereo preservados; pressao local conserva cadeia Tanque B -> A; sem compras ou ledger alterados.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
