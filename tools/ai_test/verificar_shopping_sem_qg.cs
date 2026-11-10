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
UnityEngine.Application.LogCallback captureLog=(message,stack,type)=>{if(message.Contains("recusada como recuperaÃ§Ã£o"))rejections.Add(message);};
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
    var originalBuildings = new System.Collections.Generic.List<ConstructionManager>(ConstructionManager.AllActive);
    var originalUnits = new System.Collections.Generic.List<UnitManager>(UnitManager.AllActive);
    ConstructionManager.AllActive.Clear(); UnitManager.AllActive.Clear();
    foreach(var b in buildings) ConstructionManager.AllActive.Add(b);
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    var sf=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    T First<T>()where T:UnityEngine.Object{var gs=UnityEditor.AssetDatabase.FindAssets("t:"+typeof(T).Name);if(gs.Length==0)throw new System.Exception("Catalogo ausente: "+typeof(T).Name);return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(gs[0]));}
    var combatHost=new UnityEngine.GameObject("FixtureCombat");combatHost.SetActive(false);var combat=combatHost.AddComponent<TurnStateManager>();
    Set(combat,"rpsDatabase",First<RPSDatabase>());Set(combat,"dpqMatchupDatabase",First<DPQMatchupDatabase>());Set(combat,"weaponPriorityData",First<WeaponPriorityData>());
    var combatField=typeof(AIShoppingPlanner).GetField("counterTurnStateManager",sf);var oldCombat=combatField.GetValue(null);combatField.SetValue(null,combat);
    var analyzerField=typeof(AITacticalAnalyzer).GetField("instance",sf);var oldAnalyzer=analyzerField.GetValue(null);analyzerField.SetValue(null,combatHost.AddComponent<AITacticalAnalyzer>());
    var publications=(System.Collections.IDictionary)typeof(CaptureOpportunityClaimService).GetField("PublishedBySlot",sf).GetValue(null);
    bool hadPublication=publications.Contains(ownSlot);object oldPublication=hadPublication?publications[ownSlot]:null;
    try{
        UnitData Clone(string id){var d=UnityEngine.Object.Instantiate(Asset<UnitData>(id));created.Add(d);d.bannedOnHardMode=false;return d;}
        var tank=Clone("tanque B");var bazooka=Clone("Bazooka");var elite=Clone("tanqueA");elite.eliteFrom=tank;
        Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,tank,bazooka,elite});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
        var factory=Building(990001,new UnityEngine.Vector3Int(1,2,0),ConstructionSector.Alpha);
        var a=Building(990002,new UnityEngine.Vector3Int(2,2,0),ConstructionSector.Bravo);
        var b=Building(990003,new UnityEngine.Vector3Int(3,2,0),ConstructionSector.Charlie);
        var far=Building(990004,new UnityEngine.Vector3Int(8,2,0),ConstructionSector.Delta);
        var enemyHq=Building(990005,new UnityEngine.Vector3Int(8,4,0),ConstructionSector.Echo);
        void Owner(ConstructionManager item,int slot,TeamId team){Set(item,"slotIndex",slot);Set(item,"teamId",team);}
        Owner(factory,ownSlot,TeamId.Blue);Owner(enemyHq,ownSlot==0?1:0,TeamId.Yellow);
        var market=new ConstructionSiteRuntime{isCapturable=true,capturePointsMax=20,sellingRule=ConstructionUnitMarketRule.FreeMarket,offeredUnits=new System.Collections.Generic.List<UnitData>{soldier,bazooka,tank,elite}};
        Set(factory,"siteRuntime",market);Set(enemyHq,"siteRuntime",new ConstructionSiteRuntime{isPlayerHeadQuarter=true,isCapturable=false,capturePointsMax=20});
        var sea=Asset<TerrainTypeData>("sea");Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain,forest,sea});
        for(int y=0;y<6;y++)map.SetTile(new UnityEngine.Vector3Int(4,y,0),sea.paletteTile);
        SectorManager.InvalidateLandDistanceCache();var sectors=SectorManager.GetForTilemap(map);Set(sectors,"neighborDistanceTilemap",map);Set(sectors,"neighborDistanceTerrainDatabase",terrain);Set(sectors,"useTerrainCostForNeighborDistances",true);
        var enemy=Unit(990010,elite,new UnityEngine.Vector3Int(3,3,0),false);Set(enemy,"slotIndex",ownSlot==0?1:0);Set(enemy,"teamId",TeamId.Yellow);
        var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,Stance=AIStance.Tactical,TurnNumber=10,Budget=50000,IncomePerTurn=15000,MyBuildings=new System.Collections.Generic.List<ConstructionManager>{factory},NeutralBuildings=new System.Collections.Generic.List<ConstructionManager>{a,b,far},EnemyBuildings=new System.Collections.Generic.List<ConstructionManager>{enemyHq},MyUnits=new System.Collections.Generic.List<UnitManager>(),EnemyUnits=new System.Collections.Generic.List<UnitManager>{enemy}};
        CaptureOpportunityClaimService.ClearPublishedForSlot(PlayerSlotId.FromIndex(ownSlot));
        var build=typeof(AIShoppingPlanner).GetMethod("BuildRoleShoppingDemands",sf);
        System.Collections.Generic.List<AIShoppingDemand> Demands()=>(System.Collections.Generic.List<AIShoppingDemand>)build.Invoke(null,new object[]{snapshot,false});
        void Check(bool value,string text){if(!value)throw new System.Exception(text);}
        Check(ConstructionManager.IsHeadQuarterlessTeam(TeamId.Blue),"Fixture nao iniciou sem QG.");
        var ds=Demands();var capture=ds.Find(d=>d.Role==UnitRole.Capturador&&d.Origin=="capture-opportunity");
        Check(capture!=null&&capture.Count==2,"Dois predios livres nao produziram demanda 2 (ou contou ilha distante).");
        Check(ds.Exists(d=>d.RequiredWeaponCategory==WeaponCategory.AntiTanque&&d.Count>0),"Tanque visivel nao pediu counter antitanque: pressure="+UnityEngine.JsonUtility.ToJson(AIShoppingPlanner.InspectCounterPressure(snapshot))+" ds="+string.Join("; ",ds.ConvertAll(d=>d.Origin+" role="+d.Role+" weapon="+d.RequiredWeaponCategory+" count="+d.Count)));
        Check(!ds.Exists(d=>d.Origin.Contains("composition")||d.Origin.Contains("elite")||d.Origin.Contains("vigilancia")||d.Origin.Contains("operation")||d.Origin.Contains("rally")),"Demanda de base vazou para time sem QG.");
        foreach(string name in new[]{"FindEliteAssaultReserveTarget","FindEliteFireSupportReserveTarget","FindEliteInterceptorReserveTarget"}){
            var m=typeof(AIShoppingPlanner).GetMethod(name,sf);object[] args=name=="FindEliteAssaultReserveTarget"?new object[]{snapshot,1}:name=="FindEliteFireSupportReserveTarget"?new object[]{snapshot,false,int.MaxValue,true,false}:new object[]{snapshot};
            Check(m.Invoke(null,args)==null,"Sem QG abriu reserva "+name);
        }
        // A publicacao real do matching reserva um dos dois enderecos.
        var cap=Unit(990011,soldier,new UnityEngine.Vector3Int(1,3,0),false);Set(cap,"slotIndex",ownSlot);Set(cap,"teamId",TeamId.Blue);
        UnitManager.AllActive.Add(cap);snapshot.MyUnits.Add(cap);
        var claims=CaptureOpportunityClaimService.PublishForPlan(PlayerSlotId.FromIndex(ownSlot),map,terrain,2,null);
        Check(claims.Claims.Count==1,"Matching nao publicou claim do capturador.");
        ds=Demands();capture=ds.Find(d=>d.Origin=="capture-opportunity");Check(capture!=null&&capture.Count==1,"Shopping ignorou destinatario publicado.");
        // Embarcado sai do matching mas conserva o destino de entrega.
        Set(cap,"isEmbarked",true);
        CaptureOpportunityClaimService.PublishForPlan(PlayerSlotId.FromIndex(ownSlot),map,terrain,2,null);
        Check(cap.AIHasDesignatedCaptureTarget,"Passageiro perdeu missao de captura.");
        ds=Demands();capture=ds.Find(d=>d.Origin=="capture-opportunity");Check(capture!=null&&capture.Count==1,"Passageiro com destino nao descontou oportunidade.");
        // Sem oferta legal, nem a demanda nem o carrinho prometem capturadores.
        market.sellingRule=ConstructionUnitMarketRule.Disabled;Check(!Demands().Exists(d=>d.Origin=="capture-opportunity"),"Mercado Disabled ainda contou acesso de compra.");market.sellingRule=ConstructionUnitMarketRule.FreeMarket;
        market.isPlayerHeadQuarter=true;
        ds=Demands();Check(ds.Exists(d=>d.Origin.Contains("composition")),"Com QG perdeu pacote normal.");
        return "PASS B: sem QG/FreeMarket pede 2 capturadores pelos dois predios livres alcancaveis e counter antitanque; claim publicado e passageiro embarcado reduzem para 1; ilha sem acesso e mercado Disabled excluidos; sem pacote/elite/vigilancia/rally/operacao e sem reservas de elite; QG religa pacote. Fora do Play, sem compra confirmada.";
    }finally{
        analyzerField.SetValue(null,oldAnalyzer);
        combatField.SetValue(null,oldCombat);
        if(hadPublication)publications[ownSlot]=oldPublication;else publications.Remove(ownSlot);
        ConstructionManager.AllActive.Clear();ConstructionManager.AllActive.AddRange(originalBuildings);
        UnitManager.AllActive.Clear();UnitManager.AllActive.AddRange(originalUnits);
    }
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
