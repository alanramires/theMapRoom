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
UnityEngine.Application.LogCallback captureLog=(message,stack,type)=>{if(message.Contains("TETO DE CONSCRIÇÃO mantido em defesa"))rejections.Add(message);};
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
        if(!ConstructionManager.AllActive.Contains(b))ConstructionManager.AllActive.Add(b);buildings.Add(b);return b;
    }
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    int enemySlot=ownSlot==0?1:0;
    var stat=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var oldUnits=new System.Collections.Generic.List<UnitManager>(UnitManager.AllActive);
    var oldBuildings=new System.Collections.Generic.List<ConstructionManager>(ConstructionManager.AllActive);
    var oldSlots=new System.Collections.Generic.Dictionary<UnitManager,int>();
    foreach(var u in UnityEngine.Object.FindObjectsByType<UnitManager>(UnityEngine.FindObjectsInactive.Exclude)){oldSlots[u]=u.SlotIndex;Set(u,"slotIndex",-1);}
    UnitManager.AllActive.Clear();ConstructionManager.AllActive.Clear();
    var aiInstance=typeof(AIController).GetField("_instance",stat);var previousAi=aiInstance.GetValue(null);
    var matchCache=typeof(AIController).GetField("cachedMatchController",stat);var previousMatch=matchCache.GetValue(null);
    var macroCache=(System.Collections.IDictionary)typeof(AIController).GetField("s_lastMacroBySlot",stat).GetValue(null);
    bool hadMacro=macroCache.Contains(ownSlot);object oldMacro=hadMacro?macroCache[ownSlot]:null;
    try{
        void Check(bool yes,string why){if(!yes)throw new System.Exception(why);}
        var matchHost=new UnityEngine.GameObject("ConscriptionMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"maxUnitsPerTeam",60);Set(match,"debugFogOfWarEnabled",false);matchCache.SetValue(null,match);
        // ResolveAISlotKey consulta o match ativo da cena; o match isolado deve
        // ter a mesma cor nesse slot, sem reutilizar/mutar os PlayerEntry reais.
        var players=(System.Collections.IList)typeof(MatchController).GetField("players",flags).GetValue(match);
        var player=players[ownSlot];player.GetType().GetField("teamId",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(player,TeamId.Blue);players[ownSlot]=player;
        var aiHost=new UnityEngine.GameObject("ConscriptionAI");aiHost.SetActive(false);var ai=aiHost.AddComponent<AIController>();Set(ai,"matchController",match);Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);Set(ai,"currentAISlotIndex",ownSlot);
        Set(ai,"massacreUnitCapFillRatio",0.5f);Set(ai,"massacreUnitCapExitFillRatio",0.45f);aiInstance.SetValue(null,ai);
        var preset=UnityEditor.AssetDatabase.LoadAssetAtPath<AIPresetData>("Assets/DB/AI/Presets/AIPreset_Dificil.asset").CloneRuntime();created.Add(preset);preset.capacidades.projetarProducaoInimiga=true;Set(ai,"activePreset",preset);
        UnitData Clone(string id){var data=UnityEngine.Object.Instantiate(Asset<UnitData>(id));created.Add(data);data.bannedOnHardMode=false;data.aiPurchaseMode=AIPurchaseMode.Either;return data;}
        var basic=Clone("Soldado");basic.cost=1000;basic.eliteLevel=0;
        var heavy=Clone("tanqueZ");var campaign=Clone("artCampanha");var aircraft=Clone("cacaA");
        heavy.eliteLevel=2;campaign.eliteLevel=2;aircraft.eliteLevel=1;
        Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{basic,chinook,heavy,campaign,aircraft});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
        for(int x=0;x<50;x++)for(int y=0;y<10;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
        void Offers(ConstructionManager b,int slot,params UnitData[] data){Set(b,"matchController",match);Set(b,"slotIndex",slot);Set(b,"teamId",slot==ownSlot?TeamId.Blue:match.GetVisualTeamForSlot(PlayerSlotId.FromIndex(slot)));Set(b,"siteRuntime",new ConstructionSiteRuntime{isCapturable=true,capturePointsMax=20,sellingRule=ConstructionUnitMarketRule.FreeMarket,offeredUnits=new System.Collections.Generic.List<UnitData>(data)});Set(b,"currentCapturePoints",20);}
        var factory=Building(950001,new UnityEngine.Vector3Int(1,2,0),ConstructionSector.Alpha);Offers(factory,ownSlot,heavy,campaign);
        var airport=Building(950002,new UnityEngine.Vector3Int(2,2,0),ConstructionSector.Alpha);Offers(airport,ownSlot,aircraft);
        var hq=Building(950003,new UnityEngine.Vector3Int(3,2,0),ConstructionSector.Alpha);Offers(hq,ownSlot,basic);
        var ownProducer4=Building(950004,new UnityEngine.Vector3Int(4,2,0),ConstructionSector.Alpha);Offers(ownProducer4,ownSlot,basic);
        var ownProducer5=Building(950005,new UnityEngine.Vector3Int(5,2,0),ConstructionSector.Alpha);Offers(ownProducer5,ownSlot,basic);
        var enemyProducers=new System.Collections.Generic.List<ConstructionManager>();
        for(int i=0;i<14;i++){var b=Building(951000+i,new UnityEngine.Vector3Int(20+i,2,0),ConstructionSector.Bravo);Offers(b,enemySlot,basic);enemyProducers.Add(b);}
        SectorManager.SectorInfo Sector(ConstructionSector sector,TeamId team,ConstructionManager source){var info=new SectorManager.SectorInfo();Set(info,"sector",sector);Set(info,"controllingTeam",team);Set(info,"controllingSlotIndex",team==TeamId.Blue?ownSlot:team==TeamId.Neutral?-1:enemySlot);if(source!=null)Set(info,"constructions",new System.Collections.Generic.List<SectorManager.SectorConstructionInfo>{new SectorManager.SectorConstructionInfo(source)});return info;}
        var sectors=new System.Collections.Generic.List<SectorManager.SectorInfo>{Sector(ConstructionSector.Alpha,TeamId.Blue,hq),Sector(ConstructionSector.Bravo,match.GetVisualTeamForSlot(PlayerSlotId.FromIndex(enemySlot)),enemyProducers[0]),Sector(ConstructionSector.Charlie,TeamId.Neutral,null)};
        var own=new System.Collections.Generic.List<UnitManager>();var enemies=new System.Collections.Generic.List<UnitManager>();
        UnitManager Spawn(int id,UnitData data,int slot,int x){var cell=new UnityEngine.Vector3Int(x,6,0);var u=Unit(id,data,cell,false);u.gameObject.SetActive(true);u.Apply(data);Set(u,"boardTilemap",map);Set(u,"slotIndex",slot);Set(u,"teamId",slot==ownSlot?TeamId.Blue:match.GetVisualTeamForSlot(PlayerSlotId.FromIndex(slot)));Set(u,"currentCellPosition",cell);Set(u,"isDead",false);Set(u,"hasActed",false);Set(u,"isEmbarked",false);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;}
        for(int i=0;i<34;i++)own.Add(Spawn(952000+i,i==0?chinook:basic,ownSlot,1+i%15));
        for(int i=0;i<36;i++)enemies.Add(Spawn(953000+i,basic,enemySlot,25+i%15));
        foreach(var u in new[]{own[1],own[2]}){Set(u,"isEmbarked",true);Set(u,"embarkedTransporter",own[0]);}
        var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,Stance=AIStance.Defensive,TurnNumber=11,Budget=30000,MyUnits=new System.Collections.Generic.List<UnitManager>(),EnemyUnits=enemies,MyBuildings=new System.Collections.Generic.List<ConstructionManager>{factory,airport,hq}};
        void Count(int count){snapshot.MyUnits.Clear();for(int i=0;i<own.Count;i++){own[i].gameObject.SetActive(i<count);if(i<count){if(!UnitManager.AllActive.Contains(own[i]))UnitManager.AllActive.Add(own[i]);if(!own[i].IsEmbarked)snapshot.MyUnits.Add(own[i]);}else UnitManager.AllActive.Remove(own[i]);}}
        var buildMacro=typeof(AIController).GetMethod("BuildMacroTerritoryContext",stat);
        object Macro(int ownCount,int enemyCount,bool live=false){return buildMacro.Invoke(null,new object[]{TeamId.Blue,sectors,6,ownCount,enemyCount,0,live?ai:null});}
        T Read<T>(object ctx,string name){return(T)ctx.GetType().GetField(name).GetValue(ctx);}
        var massacre=typeof(AIShoppingPlanner).GetMethod("ResolveMassacrePhase",stat);
        bool Stopped(){return(bool)massacre.Invoke(null,new object[]{snapshot,ai});}
        Count(34);Macro(32,50);ai.MassacrePhaseActive=false;
        Check(AIController.GetMacroTerritoryForInspection(TeamId.Blue).Losing,"Caso 34/60 deveria estar Losing.");
        Check(Stopped(),"Defesa reabriu conscricao acima do teto 34/60.");
        Check(rejections.Count>0,"Teto em defesa nao explicou o motivo no log.");
        Count(28);Macro(20,36);Check(Stopped(),"Histerese perdeu a parada entre 45% e 50%.");
        Count(20);Macro(20,36);Check(!Stopped(),"20/60 Losing nao reabriu a conscricao abaixo da saida.");
        Count(30);Macro(30,50);ai.MassacrePhaseActive=false;Check(Stopped(),"Limite exato de 50% nao desligou conscricao.");
        Count(20);Macro(20,10);ai.MassacrePhaseActive=false;Check(!Stopped(),"Defesa nao anulou a vantagem 2:1 abaixo do teto.");
        // Um produtor elite-only nao pode consumir reserva como massa ou imposto.
        var occupied=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
        var tax=typeof(AIShoppingPlanner).GetMethod("ComputeConscriptionTax",stat,null,new[]{typeof(AIWorldSnapshot),typeof(System.Collections.Generic.HashSet<UnityEngine.Vector3Int>)},null);
        snapshot.MyBuildings=new System.Collections.Generic.List<ConstructionManager>{factory,airport};
        Check((int)tax.Invoke(null,new object[]{snapshot,occupied})==0,"Produtor elite-only reservou imposto de massa.");
        var orders=new System.Collections.Generic.List<AIShoppingPlanner.ShoppingOrder>();var used=new System.Collections.Generic.HashSet<ConstructionManager>();
        var buy=typeof(AIShoppingPlanner).GetMethod("BuySurgeMassBody",stat);object[] args={snapshot,orders,used,occupied,30000,factory};buy.Invoke(null,args);
        Check(orders.Count==0&&(int)args[4]==30000&&!used.Contains(factory),"Fabrica Pesada comprou elite como massa sem demanda.");
        snapshot.MyBuildings.Add(hq);snapshot.Stance=AIStance.Tactical;
        Check((int)tax.Invoke(null,new object[]{snapshot,occupied})==1000,"Imposto deveria reservar apenas Soldado do HQ.");
        var surge=typeof(AIShoppingPlanner).GetMethod("RecruitmentSurgeFill",stat);object[] surgeArgs={snapshot,orders,used,occupied,30000,new System.Collections.Generic.List<AIShoppingDemand>(),null};surge.Invoke(null,surgeArgs);
        Check(orders.Count==1&&orders[0].Building==hq&&orders[0].UnitToBuy==basic&&(int)surgeArgs[4]==29000,"Conscricao sem demanda nao ficou limitada ao Soldado elite 0.");
        foreach(var producer in enemyProducers)Check(producer.CanProduceUnitsForSlot(enemySlot),"Produtor inimigo do fixture nao esta apto.");
        Check(factory.CanProduceUnitsForSlot(ownSlot)&&airport.CanProduceUnitsForSlot(ownSlot)&&hq.CanProduceUnitsForSlot(ownSlot)&&ownProducer4.CanProduceUnitsForSlot(ownSlot)&&ownProducer5.CanProduceUnitsForSlot(ownSlot),"Produtor proprio do fixture nao esta apto.");
        // Mesma funcao que planner e macroDominating do rally: 30 no snapshot,
        // 32 no limite (dois embarcados), mais 5 produtores contra 36+14.
        Count(32);preset.capacidades.projetarProducaoInimiga=true;
        Check(snapshot.MyUnits.Count==30,"Snapshot deveria omitir os dois embarcados.");
        var context=Macro(snapshot.MyUnits.Count,enemies.Count,true);
        Check(Read<int>(context,"OwnForce")==37&&Read<int>(context,"OwnProducersProjected")==5,"Nao contou 32 com embarcados + 5 produtores proprios: force="+Read<int>(context,"OwnForce")+" prod="+Read<int>(context,"OwnProducersProjected"));
        Check(Read<int>(context,"EnemyForce")==50&&Read<int>(context,"EnemyProducersProjected")==14,"Projecao inimiga mudou de regua: force="+Read<int>(context,"EnemyForce")+" prod="+Read<int>(context,"EnemyProducersProjected"));
        Check(UnityEngine.Mathf.Abs(Read<float>(context,"ForceRatio")-37f/87f)<0.0001f&&Read<object>(context,"Phase").ToString()=="Balanced","Hard deveria 37/87, Balanced; real="+Read<float>(context,"ForceRatio")+" "+Read<object>(context,"Phase"));
        preset.capacidades.projetarProducaoInimiga=false;context=Macro(snapshot.MyUnits.Count,enemies.Count,true);
        Check(Read<int>(context,"OwnForce")==32&&Read<int>(context,"EnemyForce")==36&&Read<int>(context,"OwnProducersProjected")==0&&Read<int>(context,"EnemyProducersProjected")==0,"Sem projecao deveria manter 32/36 com embarcados e sem produtores.");
        Check(Read<object>(context,"Phase").ToString()=="Balanced","Sem projecao deveria continuar Empatado.");
        return "PASS: 34/60 Losing/Defensiva respeita teto, 30/60 tambem; histerese 28/60 mantida; 20/60 Losing conscreve; defesa anula so vantagem 2:1; elite-only nao compra nem paga imposto; HQ compra Soldado elite 0; Hard 32 (2 embarcados)+5 vs 36+14 = 37/87 (42,53%), Balanced; sem projecao 32/36, Balanced. Fora do Play, sem compras executadas.";
    }finally{
        if(hadMacro)macroCache[ownSlot]=oldMacro;else macroCache.Remove(ownSlot);
        aiInstance.SetValue(null,previousAi);matchCache.SetValue(null,previousMatch);
        foreach(var pair in oldSlots)if(pair.Key!=null)Set(pair.Key,"slotIndex",pair.Value);
        UnitManager.AllActive.Clear();UnitManager.AllActive.AddRange(oldUnits);ConstructionManager.AllActive.Clear();ConstructionManager.AllActive.AddRange(oldBuildings);
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
