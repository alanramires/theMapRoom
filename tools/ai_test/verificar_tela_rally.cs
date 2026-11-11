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
    var objectiveField=typeof(ObjectiveManager).GetField("instance",sf);var oldObjective=objectiveField.GetValue(null);
    var host=new UnityEngine.GameObject("RallyReportFixture");host.SetActive(false);var objectives=host.AddComponent<ObjectiveManager>();objectiveField.SetValue(null,objectives);
    try{
        void Check(bool ok,string why){if(!ok)throw new System.Exception(why);}
        // D2: o relatorio le a banda da unidade no contexto real do tabuleiro.
        var aiHost=new UnityEngine.GameObject("RallyReportAI");aiHost.SetActive(false);
        var ai=aiHost.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
        var matchHost=new UnityEngine.GameObject("RallyReportMatch");matchHost.SetActive(false);
        var match=matchHost.AddComponent<MatchController>();Set(ai,"matchController",match);
        for(int x=0;x<28;x++)for(int y=0;y<24;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
        var model=UnityEngine.Object.Instantiate(soldier);created.Add(model);model.movement=3;
        var rally=Building(996001,new UnityEngine.Vector3Int(12,12,0),ConstructionSector.Alpha);
        Set(rally,"isRallyPoint",true);Set(rally,"rallyOwnerSlots",new System.Collections.Generic.List<int>{ownSlot});Set(rally,"slotIndex",ownSlot);Set(rally,"teamId",TeamId.Blue);
        UnitManager Spawn(int id,int x,int y){var cell=new UnityEngine.Vector3Int(x,y,0);var u=Unit(id,soldier,cell,false);u.gameObject.SetActive(true);u.Apply(soldier);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"currentCellPosition",cell);Set(u,"hasActed",false);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;}
        var core=Spawn(996010,12,12);var going=Spawn(996011,22,12);var outside=Spawn(996012,1,1);
        var allies=new System.Collections.Generic.List<UnitManager>{core,going,outside};
        var plan=new TeamObjectivePlan{SlotIndex=ownSlot,Team=TeamId.Blue,RogueUnitIds=new System.Collections.Generic.List<int>{going.InstanceId,outside.InstanceId}};
        plan.Objectives.Add(new SectorObjective{Sector=ConstructionSector.Alpha,ObjectiveType=AIObjectiveType.RallyAssembly,Slots=new System.Collections.Generic.List<SlotNeed>{new SlotNeed{Role=UnitRole.Capturador,Filled=true,AssignedUnitId=core.InstanceId},new SlotNeed{Role=UnitRole.Assalto,Filled=false}}});
        Set(objectives,"plans",new System.Collections.Generic.List<TeamObjectivePlan>{plan});
        int revision=ThreatRevisionTracker.GlobalBoardRevision,activeCount=UnitManager.AllActive.Count;
        var area=RallyAreaService.Build(rally,model,TeamId.Blue,ownSlot,map,terrain,allies,new System.Collections.Generic.List<UnitManager>(),new UnityEngine.Vector3Int(3,12,0));
        Check(area.Core.Count>1&&area.Approach.Count>area.Core.Count,"Core e Approach nao refletem bandas diferentes do modelo: "+area.Core.Count+"/"+area.Approach.Count);
        Check(area.Core.IsSubsetOf(area.Approach),"Core nao esta contido no Operacional.");
        Check(area.Vanguard.Count>0&&area.Rear.Count>0,"Geometria nao produziu vanguarda e retaguarda para testar intersecao.");
        var assemblyArea=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(area.Core);assemblyArea.UnionWith(area.Approach);
        Check(area.Rear.IsSubsetOf(assemblyArea),"Rear escapou de Core uniao Approach.");
        Check(!area.Vanguard.Overlaps(area.Rear),"Vanguard e Rear se sobrepoem.");
        var rallyWorld=(UnityEngine.Vector2)HexCoordinates.GetCellCenterWorld(map,rally.CurrentCellPosition);
        foreach(var cell in area.Vanguard)
            Check(UnityEngine.Vector2.Dot((UnityEngine.Vector2)HexCoordinates.GetCellCenterWorld(map,cell)-rallyWorld,area.Forward)>0f,"Vanguard ficou atras do rally.");
        Check(area.Orientation.Contains("eixo"),"Sem inimigo conhecido nao orientou pelo eixo.");
        var moves=new System.Collections.Generic.Dictionary<int,UnityEngine.Vector3Int>{{going.InstanceId,new UnityEngine.Vector3Int(13,12,0)},{outside.InstanceId,new UnityEngine.Vector3Int(2,1,0)}};
        var readiness=new RallyReadinessReport{Status="ASSEMBLING",Missing="fixture",Hold=true};
        var report=RallyFormationReport.Build(ownSlot,area,readiness,allies,moves);
        Check(report.Participants.Count==3,"Relatorio nao incluiu vaga mais rogues exatamente uma vez.");
        var pCore=report.Participants.Find(p=>p.Unit==core);var pGoing=report.Participants.Find(p=>p.Unit==going);var pOutside=report.Participants.Find(p=>p.Unit==outside);
        Check(pCore.Location==RallyLocation.Core&&!pCore.Destination.HasValue&&!pCore.GoingToArea,"Fatos da unidade no core incorretos.");
        Check(pGoing.Location==RallyLocation.Outside&&pGoing.Destination==moves[going.InstanceId]&&pGoing.DestinationLocation==RallyLocation.Core&&pGoing.GoingToArea,"Fatos da unidade indo ao rally incorretos.");
        Check(pOutside.Location==RallyLocation.Outside&&pOutside.Destination==moves[outside.InstanceId]&&pOutside.DestinationLocation==RallyLocation.Outside&&!pOutside.GoingToArea,"Fatos da unidade fora incorretos.");
        Check(report.ToDisplayText().Contains("preenchidas 1, vazias 0")&&report.ToDisplayText().Contains("preenchidas 0, vazias 1"),"Demanda publicada nao aparece com preenchidas/vazias.");
        going.SetAIDesignatedCaptureTarget(rally.InstanceId,rally.CurrentCellPosition);moves.Remove(going.InstanceId);
        var mission=RallyFormationReport.Build(ownSlot,area,readiness,allies,moves).Participants.Find(p=>p.Unit==going);
        Check(mission.MissionDestination==rally.CurrentCellPosition&&mission.GoingToArea,"Missao publicada nao substituiu ausencia de decisao observada.");
        model.movement=1;
        var slower=RallyAreaService.Build(rally,model,TeamId.Blue,ownSlot,map,terrain,allies,new System.Collections.Generic.List<UnitManager>(),new UnityEngine.Vector3Int(3,12,0));
        Check(slower.Core.Count<area.Core.Count,"Troca do movimento do modelo nao alterou core.");
        Check(ThreatRevisionTracker.GlobalBoardRevision==revision&&UnitManager.AllActive.Count==activeCount,"Consulta registrou modelo ou alterou revisao confirmada do tabuleiro.");
        Check(!core.HasActed&&!going.HasActed&&!outside.HasActed&&going.CurrentCellPosition==new UnityEngine.Vector3Int(22,12,0),"Relatorio executou/alterou unidade.");
        var parser=typeof(PanelDebugController).GetMethod("TryParseRallyDebugCommand",sf);
        foreach(string command in new[]{"AI SHOW RALLY","SHOW RALLY 1","AI SHOW RALLY OFF 1","SHOW RALLY 1 ON"}){object[] args={command,false,-1};Check((bool)parser.Invoke(null,args),"Comando recusado: "+command);}
        object[] invalid={"AI SHOW RALLY 99",false,-1};Check(!(bool)parser.Invoke(null,invalid),"Slot invalido aceito.");
        return "PASS: modelo define Tactical/Core e Operational/Approach distintos; Vanguard/Rear nao vazios e disjuntos; Rear contida na montagem; Vanguard a frente do rally sem recorte terrestre; eixo sem inimigo conhecido; vaga+rogues uma vez; core/indo/fora e destinos exatos; missao fallback; demanda preenchida/vazia; movimento do modelo altera area; parser/aliases; sem unidade registrada, revisao de tabuleiro alterada ou acao executada. UI e trilhas nao vistas em Play.";
    }finally{objectiveField.SetValue(null,oldObjective);}
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
