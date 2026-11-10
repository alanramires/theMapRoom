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
    var sf=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var oldUnits=new System.Collections.Generic.List<UnitManager>(UnitManager.AllActive);var oldBuildings=new System.Collections.Generic.List<ConstructionManager>(ConstructionManager.AllActive);
    UnitManager.AllActive.Clear();ConstructionManager.AllActive.Clear();
    var host=new UnityEngine.GameObject("PlanlessPolicyFixture");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var match=host.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);
    var objectiveField=typeof(ObjectiveManager).GetField("instance",sf);var oldObjective=objectiveField.GetValue(null);var objectives=host.AddComponent<ObjectiveManager>();objectiveField.SetValue(null,objectives);
    var publications=(System.Collections.IDictionary)typeof(CaptureOpportunityClaimService).GetField("PublishedBySlot",sf).GetValue(null);bool hadPublished=publications.Contains(ownSlot);var oldPublished=hadPublished?publications[ownSlot]:null;
    try{
        object Call(string name,params object[] args)=>typeof(AIController).GetMethod(name,flags).Invoke(ai,args);
        void Check(bool v,string why){if(!v)throw new System.Exception(why);}
        var apacheData=Asset<UnitData>("apache");var apcData=Asset<UnitData>("apc");var tankData=Asset<UnitData>("tanque B");
        Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,apacheData,apcData,tankData});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
        UnitManager Spawn(int id,UnitData data,int x,int y,bool own){var cell=new UnityEngine.Vector3Int(x,y,0);var u=Unit(id,data,cell,false);u.gameObject.SetActive(true);u.Apply(data);Set(u,"boardTilemap",map);Set(u,"slotIndex",own?ownSlot:ownSlot==0?1:0);Set(u,"teamId",own?TeamId.Blue:TeamId.Yellow);Set(u,"currentCellPosition",cell);Set(u,"isDead",false);Set(u,"isEmbarked",false);Set(u,"hasActed",false);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;}
        var ownHq=Building(995001,new UnityEngine.Vector3Int(0,0,0),ConstructionSector.Alpha);var enemyHq=Building(995002,new UnityEngine.Vector3Int(9,5,0),ConstructionSector.Bravo);var target=Building(995003,new UnityEngine.Vector3Int(2,2,0),ConstructionSector.Charlie);
        Set(ownHq,"slotIndex",ownSlot);Set(ownHq,"teamId",TeamId.Blue);Set(ownHq,"siteRuntime",new ConstructionSiteRuntime{isPlayerHeadQuarter=true,isCapturable=true,capturePointsMax=20});
        Set(enemyHq,"slotIndex",ownSlot==0?1:0);Set(enemyHq,"teamId",TeamId.Yellow);Set(enemyHq,"siteRuntime",new ConstructionSiteRuntime{isPlayerHeadQuarter=true,isCapturable=true,capturePointsMax=20});
        var cap=Spawn(995010,soldier,2,2,true);var apc=Spawn(995011,apcData,1,1,true);
        var plan=new TeamObjectivePlan{SlotIndex=ownSlot,Team=TeamId.Blue,RogueUnitIds=new System.Collections.Generic.List<int>{cap.InstanceId}};
        Set(objectives,"plans",new System.Collections.Generic.List<TeamObjectivePlan>{plan});
        var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,TurnNumber=3,Stance=AIStance.Tactical,MyHQ=ownHq,EnemyHQ=enemyHq,MyUnits=new System.Collections.Generic.List<UnitManager>{cap,apc},EnemyUnits=new System.Collections.Generic.List<UnitManager>(),MyBuildings=new System.Collections.Generic.List<ConstructionManager>{ownHq},NeutralBuildings=new System.Collections.Generic.List<ConstructionManager>{target},EnemyBuildings=new System.Collections.Generic.List<ConstructionManager>{enemyHq}};
        var action=(PlayerAction)Call("TryDecidePlanlessCapturerAction",cap,snapshot,plan);
        var pending=(System.Collections.Generic.Dictionary<int,ConstructionManager>)typeof(AIController).GetField("pendingPlanlessCaptureTargets",flags).GetValue(ai);
        Check(action!=null&&pending.ContainsKey(cap.InstanceId),"Capturador com QG e sem vaga nao declarou missao antes da captura oportunista.");
        Check(!cap.AIHasDesignatedMission&&!cap.HasActed,"Previa confirmou missao/acao antes do batch.");
        Call("CommitPendingPlanlessCaptureTarget",cap);Check(cap.AIHasDesignatedCaptureTarget&&cap.AIDesignatedCaptureTargetCell==target.CurrentCellPosition,"Commit simulado nao publicou missao do rogue com QG.");
        Set(cap,"isEmbarked",true);Set(cap,"embarkedTransporter",apc);Set(apc,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{embarkedUnit=cap}});
        object[] deliveryArgs={cap,plan,snapshot,enemyHq.CurrentCellPosition,apc.CurrentCellPosition,null};
        Check((bool)Call("TryResolveCourierPassengerTarget",deliveryArgs)&&(UnityEngine.Vector3Int)deliveryArgs[5]==target.CurrentCellPosition,"APC trocou missao do passageiro pelo QG/setor do transporte.");
        var probe=new UnityEngine.Vector3Int(5,3,0);
        Check((bool)Call("IsTransportInvasionCourierCellAllowed",apc,snapshot,probe,enemyHq.CurrentCellPosition),"Carga sem vaga com QG ficou presa no GoGreen/screen.");
        Check((bool)Call("IsTransportInvasionDropAllowed",apc,snapshot,probe,probe,enemyHq.CurrentCellPosition),"Carga sem vaga com QG nao pode desembarcar por falta de GoGreen.");
        var formal=Spawn(995012,soldier,1,1,true);Set(formal,"isEmbarked",true);Set(formal,"embarkedTransporter",apc);
        Set(apc,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{embarkedUnit=cap},new UnitTransportSeatRuntime{embarkedUnit=formal}});
        var cargo=new System.Collections.Generic.List<UnitManager>{cap,formal};var targets=new System.Collections.Generic.Dictionary<int,UnityEngine.Vector3Int>{{cap.InstanceId,target.CurrentCellPosition},{formal.InstanceId,enemyHq.CurrentCellPosition}};
        Check((bool)Call("UsesFreeCargoDisembarkFlow",cargo,plan,snapshot,targets),"Dupla sem vaga nao entrou no fluxo livre de desembarque.");
        Check((int)Call("ResolveRequiredJointDeliveries",apc,cargo,plan,snapshot,targets)==2,"Dupla sem vaga nao pediu entrega conjunta.");
        var objective=new SectorObjective{Sector=ConstructionSector.Bravo,ObjectiveType=AIObjectiveType.InvasionAttack,Slots=new System.Collections.Generic.List<SlotNeed>{new SlotNeed{Role=UnitRole.Capturador,Filled=true,AssignedUnitId=formal.InstanceId}}};plan.Objectives.Add(objective);
        Check(!(bool)Call("IsTransportInvasionCourierCellAllowed",apc,snapshot,probe,enemyHq.CurrentCellPosition),"Carga mista burlou GoGreen da operacao formal.");
        Check(!(bool)Call("IsTransportInvasionDropAllowed",apc,snapshot,probe,probe,enemyHq.CurrentCellPosition),"Carga mista burlou screen de desembarque formal.");
        Check(!(bool)Call("UsesFreeCargoDisembarkFlow",cargo,plan,snapshot,targets),"Carga mista foi tratada como todos sem plano.");
        Check((int)Call("ResolveRequiredJointDeliveries",apc,cargo,plan,snapshot,targets)==1,"Carga mista herdou exigencia conjunta dos rogues.");
        // Sem QG a mesma aeronave de ataque interdita poder de captura.
        Set(ownHq,"siteRuntime",new ConstructionSiteRuntime{isCapturable=true,capturePointsMax=20});snapshot.MyHQ=null;
        var apache=Spawn(995020,apacheData,3,3,true);var carrier=Spawn(995021,chinook,8,3,false);var enemyCap=Spawn(995022,soldier,8,3,false);
        Set(carrier,"currentDomain",Domain.Air);Set(carrier,"currentHeightLevel",HeightLevel.AirLow);Set(apache,"currentDomain",Domain.Air);Set(apache,"currentHeightLevel",HeightLevel.AirLow);
        Set(enemyCap,"isEmbarked",true);Set(enemyCap,"embarkedTransporter",carrier);Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{embarkedUnit=enemyCap}});
        snapshot.MyUnits.Add(apache);snapshot.EnemyUnits.Add(carrier);
        Check(ConstructionManager.IsHeadQuarterlessTeam(TeamId.Blue),"Fixture nao esta sem QG no teste Apache.");
        object[] anchorArgs={apache,snapshot,apache.CurrentCellPosition,new System.Collections.Generic.List<UnitManager>{carrier},null,false,null,null,null,false};
        var anchor=(UnityEngine.Vector3Int)Call("ResolveAirCombatFallbackAnchor",anchorArgs);
        Check(anchor==carrier.CurrentCellPosition&&((string)anchorArgs[4]).StartsWith("Interdicao:"),"Apache sem QG ignorou Chinook carregado: "+anchorArgs[4]);
        // Sem plano nem alvo de combate, o tanque entra no papel e segue o capitao.
        UnitManager.AllActive.Remove(carrier);UnitManager.AllActive.Remove(enemyCap);UnitManager.AllActive.Remove(apache);UnitManager.AllActive.Remove(apc);
        var captain=Spawn(995030,soldier,7,3,true);var tank=Spawn(995031,tankData,1,3,true);
        Set(objectives,"plans",new System.Collections.Generic.List<TeamObjectivePlan>());snapshot.MyUnits=new System.Collections.Generic.List<UnitManager>{captain,tank};snapshot.EnemyUnits.Clear();
        var follow=(PlayerAction)Call("DecideUnitAction",tank,snapshot);
        Check(follow!=null&&follow.HasMoveTo&&SectorManager.HexDistance(follow.MoveTo,captain.CurrentCellPosition)<SectorManager.HexDistance(tank.CurrentCellPosition,captain.CurrentCellPosition),"Tanque sem plano nao entrou no papel para seguir capitao sem objetivo.");
        Check(!tank.HasActed&&tank.CurrentCellPosition==new UnityEngine.Vector3Int(1,3,0),"Consulta do tanque confirmou movimento.");
        // Ausencia de QG no snapshot limpa o plano/eixo sem depender de faccao.
        Set(ownHq,"siteRuntime",new ConstructionSiteRuntime{isPlayerHeadQuarter=true,isCapturable=true,capturePointsMax=20});
        Set(objectives,"plans",new System.Collections.Generic.List<TeamObjectivePlan>{new TeamObjectivePlan{SlotIndex=ownSlot,Team=TeamId.Blue,Objectives=new System.Collections.Generic.List<SectorObjective>{new SectorObjective{Sector=ConstructionSector.Alpha}}}});
        Call("BuildObjectivePlan",snapshot);Check(ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(ownSlot)).Objectives.Count==0&&typeof(AIController).GetField("currentAxisMap",flags).GetValue(ai)==null,"Planner conservou plano sem QG no snapshot.");
        return "PASS C: rogue com QG declara antes do atalho e so grava no commit simulado; APC respeita missao e carga sem vaga passa GoGreen/screen, carga mista nao; Apache sem QG interdita Chinook carregado; roteador inclui tanque sem plano seguindo capitao; snapshot sem QG limpa plano. Fora do Play, sem batch executado.";
    }finally{
        objectiveField.SetValue(null,oldObjective);if(hadPublished)publications[ownSlot]=oldPublished;else publications.Remove(ownSlot);
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
