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
    UnitData Clone(string id){var d=UnityEngine.Object.Instantiate(Asset<UnitData>(id));created.Add(d);d.eliteFrom=null;d.bannedOnHardMode=false;return d;}
    var tank=Clone("tanque B");var bomber=Clone("bombardeiro");var apache=Clone("apache");var aa=Clone("aaa");var gun=Clone("obusLeve");
    var catalog=new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,tank,bomber,apache,aa,gun};Set(unitDb,"units",catalog);typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    var sea=Asset<TerrainTypeData>("sea");Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain,forest,sea});
    var sectors=SectorManager.GetForTilemap(map);Set(sectors,"neighborDistanceTilemap",map);Set(sectors,"neighborDistanceTerrainDatabase",terrain);Set(sectors,"useTerrainCostForNeighborDistances",true);
    var ground=Building(930001,new UnityEngine.Vector3Int(1,2,0),ConstructionSector.Alpha);
    var airport=Building(930002,new UnityEngine.Vector3Int(1,3,0),ConstructionSector.Bravo);
    var otherGround=Building(930003,new UnityEngine.Vector3Int(2,2,0),ConstructionSector.Charlie);
    void Offers(ConstructionManager b,params UnitData[] data){Set(b,"slotIndex",ownSlot);Set(b,"teamId",TeamId.Blue);Set(b,"siteRuntime",new ConstructionSiteRuntime{isPlayerHeadQuarter=b==ground,isCapturable=true,capturePointsMax=20,sellingRule=ConstructionUnitMarketRule.FreeMarket,offeredUnits=new System.Collections.Generic.List<UnitData>(data)});Set(b,"currentCapturePoints",20);}
    Offers(ground,tank);Offers(airport,bomber,apache);Offers(otherGround,tank);
    foreach(var b in new[]{ground,airport,otherGround})if(!b.CanProduceUnitsForSlot(ownSlot))throw new System.Exception("Produtor fixture indisponivel.");
    UnitManager Spawn(int id,UnitData data,int x,int y,int slot){var u=Unit(id,data,new UnityEngine.Vector3Int(x,y,0),false);u.gameObject.SetActive(true);u.Apply(data);Set(u,"boardTilemap",map);Set(u,"slotIndex",slot);Set(u,"teamId",slot==ownSlot?TeamId.Blue:TeamId.Yellow);Set(u,"isDead",false);Set(u,"currentHP",10);Set(u,"isEmbarked",false);var cell=new UnityEngine.Vector3Int(x,y,0);Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,cell,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;}
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,Stance=AIStance.Tactical,TurnNumber=10,Budget=1000,IncomePerTurn=15000,MyBuildings=new System.Collections.Generic.List<ConstructionManager>{ground,airport,otherGround},MyUnits=new System.Collections.Generic.List<UnitManager>(),EnemyUnits=new System.Collections.Generic.List<UnitManager>()};
    aa.eliteLevel=0;gun.eliteLevel=0;
    int idCounter=931000;
    void Wall(int antiAir,int support){snapshot.EnemyUnits.Clear();for(int i=0;i<antiAir;i++)snapshot.EnemyUnits.Add(Spawn(idCounter++,aa,7+i%2,1+i%4,enemySlot));for(int i=0;i<support;i++)snapshot.EnemyUnits.Add(Spawn(idCounter++,gun,7+i%2,1+i%4,enemySlot));}
    var staticFlags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var build=typeof(AIShoppingPlanner).GetMethod("BuildArtilleryWallDemands",staticFlags);
    System.Collections.Generic.List<AIShoppingDemand> Demands(){return(System.Collections.Generic.List<AIShoppingDemand>)build.Invoke(null,new object[]{snapshot,false});}
    int Count(System.Collections.Generic.List<AIShoppingDemand> demands){int n=0;foreach(var d in demands)n+=d.Count;return n;}
    Wall(3,0);var demands=Demands();if(Count(demands)!=1||demands[0].Role!=UnitRole.Assalto||demands[0].MinTargetPriority!=BazookaTargetPriority.Secondary)throw new System.Exception("SAM/AAA nao pediu tanque regular secundario na ausencia de primario.");
    Wall(0,3);demands=Demands();if(Count(demands)!=1||demands[0].Role!=UnitRole.AtaqueAereo||!demands[0].WallResponseOffers.ContainsKey(bomber.id)||demands[0].WallResponseOffers.ContainsKey(apache.id))throw new System.Exception("Obuses nao pediram bombardeiro ou Apache entrou na parede.");
    Wall(2,1);if(Count(Demands())!=1)throw new System.Exception("Parede mista de tres nao gerou resposta.");
    Wall(3,3);demands=Demands();if(Count(demands)!=2||demands.Count!=2)throw new System.Exception("Parede mista nao dividiu as vagas.");
    snapshot.IsInvading=true;Wall(6,6);if(Count(Demands())!=12)throw new System.Exception("Invasao nao aplicou 1:1 sem teto.");snapshot.IsInvading=false;if(Count(Demands())!=3)throw new System.Exception("Normal nao preservou teto global de tres.");
    // A primary counter, when offered, wins over secondary alternatives.
    var primaryTank=Clone("tanque B");primaryTank.id="fixture-primary-tank";
    primaryTank.aiTargetPreferenceByClass=new System.Collections.Generic.List<AITargetPreferenceByClassRule>{new AITargetPreferenceByClassRule{targetClass=GameUnitClass.Artillery,priority=BazookaTargetPriority.Primary}};
    Offers(ground,tank,primaryTank);Wall(3,0);demands=Demands();
    if(demands[0].MinTargetPriority!=BazookaTargetPriority.Primary||demands[0].WallResponseOffers.ContainsKey(tank.id)||!demands[0].WallResponseOffers.ContainsKey(primaryTank.id))throw new System.Exception("Resposta primaria ofertada nao precedeu secundaria.");
    Offers(ground,tank);
    // Weighted pressure: artillery levels 0/1/2 = 1/1.5/3;
    // assault uses half, without requiring a tank name or class.
    var pressureMethod=typeof(AIShoppingPlanner).GetMethod("ResolveWallPressure",staticFlags);
    var assault=Clone("tanque B");assault.id="fixture-assault";catalog.Add(assault);typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);assault.roles=new System.Collections.Generic.List<UnitRole>{UnitRole.Assalto};
    for(int level=0;level<=2;level++){
        gun.eliteLevel=level;assault.eliteLevel=level;
        int a=(int)pressureMethod.Invoke(null,new object[]{gun,false});int b=(int)pressureMethod.Invoke(null,new object[]{assault,false});
        if(a!=new[]{4,6,12}[level]||b!=new[]{2,3,6}[level])throw new System.Exception("Peso por papel/elite incorreto.");
    }
    Wall(0,1);if(Count(Demands())!=1)throw new System.Exception("Artilharia elite 2 nao gerou resposta sozinha.");
    gun.eliteLevel=0;snapshot.EnemyUnits.Clear();
    for(int i=0;i<2;i++)snapshot.EnemyUnits.Add(Spawn(idCounter++,assault,7,2+i,enemySlot));
    if(Count(Demands())!=1)throw new System.Exception("Dois assaltos elite 2 nao geraram resposta.");
    snapshot.EnemyUnits.RemoveAt(1);if(Count(Demands())!=0)throw new System.Exception("Um assalto elite 2 disparou compra precoce.");
    assault.eliteLevel=0;snapshot.EnemyUnits.Clear();for(int i=0;i<6;i++)snapshot.EnemyUnits.Add(Spawn(idCounter++,assault,7,1+i%4,enemySlot));
    if(Count(Demands())!=1)throw new System.Exception("Seis assaltos basicos nao geraram resposta.");
    snapshot.IsInvading=true;gun.eliteLevel=2;Wall(0,1);if(Count(Demands())!=1)throw new System.Exception("Elite multiplicou 1:1 da invasao.");snapshot.IsInvading=false;gun.eliteLevel=0;
    gun.eliteLevel=1;Wall(0,2);if(Count(Demands())!=1)throw new System.Exception("Duas artilharias elite 1 nao somaram tres pontos.");gun.eliteLevel=0;
    assault.eliteLevel=1;snapshot.EnemyUnits.Clear();for(int i=0;i<4;i++)snapshot.EnemyUnits.Add(Spawn(idCounter++,assault,7,1+i,enemySlot));
    if(Count(Demands())!=1)throw new System.Exception("Quatro assaltos elite 1 nao somaram tres pontos.");
    snapshot.EnemyUnits.Add(snapshot.EnemyUnits[0]);if(Count(Demands())!=1)throw new System.Exception("InstanceId duplicado aumentou pressao.");
    gun.roles=new System.Collections.Generic.List<UnitRole>{UnitRole.ArtilheiroCombatente,UnitRole.Assalto};gun.eliteLevel=1;Wall(0,1);
    if(Count(Demands())!=0)throw new System.Exception("Artilheiro combatente contou tambem como assalto.");gun.eliteLevel=0;
    // Existing hulls cover one bucket each, including ordinary composition tanks.
    Wall(3,3);var ownedTank=Spawn(idCounter++,tank,2,3,ownSlot);snapshot.MyUnits.Add(ownedTank);demands=Demands();if(Count(demands)!=1||demands[0].Role!=UnitRole.AtaqueAereo)throw new System.Exception("Tanque existente nao descontou a cota terrestre uma vez.");snapshot.MyUnits.Clear();
    var ownedBomber=Spawn(idCounter++,bomber,2,4,ownSlot);snapshot.MyUnits.Add(ownedBomber);demands=Demands();if(Count(demands)!=1||demands[0].Role!=UnitRole.Assalto)throw new System.Exception("Bombardeiro existente contou duas vezes ou nao cobriu suporte.");snapshot.MyUnits.Clear();
    // A spare capable hull may cover the remaining mixed quota, but only once.
    Wall(2,2);snapshot.MyUnits.Add(ownedTank);demands=Demands();if(Count(demands)!=0)throw new System.Exception("Peca existente apta nao cobriu a vaga mista restante.");snapshot.MyUnits.Clear();
    // Isolated islands: no tank can reach the wall; air is the available alternative.
    for(int y=0;y<6;y++)map.SetTile(new UnityEngine.Vector3Int(4,y,0),sea.paletteTile);
    SectorManager.InvalidateLandDistanceCache();Wall(3,0);demands=Demands();if(Count(demands)!=1||demands[0].Role!=UnitRole.AtaqueAereo)throw new System.Exception("Ilha sem acesso ainda pediu tanque.");snapshot.MyUnits.Add(ownedTank);if(Count(Demands())!=1)throw new System.Exception("Tanque ilhado cobriu parede inacessivel.");snapshot.MyUnits.Clear();
    // Two sellers of the same tank: only one has access to this wall.
    otherGround.transform.position=HexCoordinates.GetCellCenterWorld(map,new UnityEngine.Vector3Int(6,2,0));Set(otherGround,"currentCellPosition",new UnityEngine.Vector3Int(6,2,0));demands=Demands();if(demands[0].Role!=UnitRole.Assalto||demands[0].WallResponseOffers[tank.id].Contains(ground.InstanceId)||!demands[0].WallResponseOffers[tank.id].Contains(otherGround.InstanceId))throw new System.Exception("Acesso nao foi separado por produtor.");
    // No appropriate aircraft offered: ground alternative works, regardless of cash.
    for(int y=0;y<6;y++)map.SetTile(new UnityEngine.Vector3Int(4,y,0),plain.paletteTile);SectorManager.InvalidateLandDistanceCache();Offers(airport,apache);Wall(0,3);demands=Demands();if(Count(demands)!=1||demands[0].Role!=UnitRole.Assalto)throw new System.Exception("Sem ataque aereo adequado nao converteu para tanque.");Offers(airport,bomber,apache);demands=Demands();if(demands[0].Role!=UnitRole.AtaqueAereo)throw new System.Exception("Falta de dinheiro alterou preferencia por oferta.");
    // Cart credit: one composition purchase can fill only ONE wall quota.
    snapshot.IsInvading=true;Wall(1,1);var walls=Demands();if(walls.Count!=2)throw new System.Exception("Fixture de carrinho sem dois grupos.");
    var cartType=typeof(AIShoppingPlanner).GetNestedType("RoleShoppingCart",System.Reflection.BindingFlags.NonPublic);var candidateType=typeof(AIShoppingPlanner).GetNestedType("RoleShoppingCandidate",System.Reflection.BindingFlags.NonPublic);
    var composition=new AIShoppingDemand{Role=UnitRole.Assalto,Count=1,Priority=12,Origin="composition"};var cartDemands=new System.Collections.Generic.List<AIShoppingDemand>{composition,walls[0],walls[1]};
    var cart=System.Activator.CreateInstance(cartType,true);cartType.GetField("RemainingDemand").SetValue(cart,new[]{1,1,1});cartType.GetField("CoveredDemand").SetValue(cart,new bool[3]);cartType.GetField("RemainingBudget").SetValue(cart,50000);
    var candidate=System.Activator.CreateInstance(candidateType,true);candidateType.GetField("Building").SetValue(candidate,ground);candidateType.GetField("Unit").SetValue(candidate,tank);candidateType.GetField("Demand").SetValue(candidate,composition);candidateType.GetField("DemandIndex").SetValue(candidate,0);
    typeof(AIShoppingPlanner).GetMethod("AddCandidateToRoleShoppingCart",staticFlags).Invoke(null,new object[]{cart,candidate,cartDemands});var remaining=(int[])cartType.GetField("RemainingDemand").GetValue(cart);if(remaining[0]!=0||remaining[1]+remaining[2]!=1)throw new System.Exception("Compra no carrinho cobriu dois grupos ou nao descontou parede.");
    // Main role classifies an AA combatant only once, never as ground support too.
    var classify=typeof(AIShoppingPlanner).GetMethod("ResolveArtilleryWallGroup",staticFlags);if((int)classify.Invoke(null,new object[]{aa})!=1||(int)classify.Invoke(null,new object[]{gun})!=0)throw new System.Exception("Classificacao funcional da parede falhou.");
    if(snapshot.Budget!=1000||ownedTank.CurrentHP!=10||ownedTank.HasActed)throw new System.Exception("Planejamento alterou estado confirmado.");
    return "PASS: pesos artilharia/assalto por elite, limiares e identidade unica; 3 pontos/resposta e teto 3; invasao 1:1 sem teto; AAA prefere tanque e obus prefere bombardeiro; Apache excluido; primario/secundario e fallback simetricos; caixa nao converte; cobertura existente e carrinho sem dupla contagem; cobertura apta da vaga mista; tanque ilhado nao cobre; produtor com acesso separado; sem compra confirmada.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
