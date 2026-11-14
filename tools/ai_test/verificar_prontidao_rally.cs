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
    // Stress de alcance: o helicoptero chega a base distante pelo Operacional,
    // mas esse raio nao pode aumentar a area local nem virar presenca no rally.
    chinook=UnityEngine.Object.Instantiate(chinook);created.Add(chinook);chinook.movement=24;
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
    var sea=Asset<TerrainTypeData>("sea");Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain,forest,sea});
    var rally=Building(920038,new UnityEngine.Vector3Int(4,2,0),ConstructionSector.Golf);rally.Apply(city);Set(rally,"slotIndex",ownSlot);Set(rally,"teamId",TeamId.Blue);Set(rally,"currentCapturePoints",rally.CapturePointsMax);Set(rally,"isRallyPoint",true);Set(rally,"rallyOwnerSlots",new System.Collections.Generic.List<int>{ownSlot});
    var carrier=Unit(920072,chinook,new UnityEngine.Vector3Int(1,1,0),false);
    var main=Unit(920010,soldier,carrier.CurrentCellPosition,true);var extra=Unit(920063,soldier,carrier.CurrentCellPosition,true);
    var guard=Unit(920100,soldier,rally.CurrentCellPosition,false);
    foreach(var u in new[]{carrier,main,extra,guard}){var cell=u.CurrentCellPosition;u.gameObject.SetActive(true);u.Apply(u==carrier?chinook:soldier);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"currentHP",7);Set(u,"currentFuel",49);Set(u,"remainingMovementPoints",u.GetMovementRange());Set(u,"isEmbarked",u==main||u==extra);Set(u,"isUnderRepair",false);Set(u,"hasActed",false);Set(u,"currentCellPosition",cell);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,cell,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);}
    foreach(var u in new[]{main,extra})Set(u,"embarkedTransporter",carrier);
    Set(carrier,"currentDomain",Domain.Air);Set(carrier,"currentHeightLevel",HeightLevel.AirLow);
    Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{slotId="passageiros",slotIndex=0,seatIndex=0,embarkedUnit=main,embarkedOnTurn=1},new UnitTransportSeatRuntime{slotId="passageiros",slotIndex=0,seatIndex=1,embarkedUnit=extra,embarkedOnTurn=2}});
    map.SetTile(carrier.CurrentCellPosition,sea.paletteTile);
    var host=new UnityEngine.GameObject("RallyDeliveryAI");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var matchHost=new UnityEngine.GameObject("RallyDeliveryMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);Set(ai,"matchController",match);

    void Check(bool yes,string why){if(!yes)throw new System.Exception(why);}
    var stat=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    void Clear(){((System.Collections.IDictionary)typeof(AIController).GetField("rallyReadinessFacts",stat).GetValue(null)).Clear();((System.Collections.IDictionary)typeof(AIController).GetField("rallyReachFacts",stat).GetValue(null)).Clear();((System.Collections.IDictionary)typeof(AIController).GetField("rallyAreaFacts",stat).GetValue(null)).Clear();}
    var oldObjective=typeof(ObjectiveManager).GetField("instance",stat).GetValue(null);
    var objectiveHost=new UnityEngine.GameObject("ReadinessObjectives");objectiveHost.SetActive(false);
    var manager=objectiveHost.AddComponent<ObjectiveManager>();typeof(ObjectiveManager).GetField("instance",stat).SetValue(null,manager);
    try{
        var apache=Asset<UnitData>("apache");var fighter=Asset<UnitData>("cacaB");var bomber=Asset<UnitData>("bombardeiro");var tank=Asset<UnitData>("tanqueA");
        apache=UnityEngine.Object.Instantiate(apache);created.Add(apache);apache.movement=16;
        Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,apache,fighter,bomber,tank});
        foreach(var profile in new[]{("Facil",1.2f),("Medio",1.5f),("Dificil",2f)})
            Check(UnityEngine.Mathf.Approximately(UnityEditor.AssetDatabase.LoadAssetAtPath<AIPresetData>("Assets/DB/AI/Presets/AIPreset_"+profile.Item1+".asset").rallyLocalRatio,profile.Item2),"Razao local do perfil autorado incorreta: "+profile.Item1);
        var preset=UnityEditor.AssetDatabase.LoadAssetAtPath<AIPresetData>("Assets/DB/AI/Presets/AIPreset_Dificil.asset").CloneRuntime();created.Add(preset);Set(ai,"activePreset",preset);
        Check(preset.rallyLocalRatio==2f,"Razao Dificil nao aplicada.");
        var objective=new SectorObjective{Sector=ConstructionSector.Golf,ObjectiveType=AIObjectiveType.RallyAssembly,RallyState=AIRallyAssemblyState.Assembling};
        var plan=new TeamObjectivePlan{SlotIndex=ownSlot,Team=TeamId.Blue};plan.Objectives.Add(objective);Set(manager,"plans",new System.Collections.Generic.List<TeamObjectivePlan>{plan});
        UnitManager Spawn(int id,UnitData data,UnityEngine.Vector3Int cell,int slot){
            var u=Unit(id,data,cell,false);u.gameObject.SetActive(true);u.Apply(data);Set(u,"boardTilemap",map);Set(u,"slotIndex",slot);Set(u,"teamId",slot==ownSlot?TeamId.Blue:TeamId.Red);Set(u,"isDead",false);Set(u,"currentHP",10);Set(u,"currentFuel",100);Set(u,"currentAmmo",100);Set(u,"isEmbarked",false);Set(u,"isUnderRepair",false);Set(u,"currentCellPosition",cell);Set(u,"remainingMovementPoints",u.MaxMovementPoints);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;
        }
        for(int x=0;x<65;x++)for(int y=0;y<20;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
        var s2=Spawn(930002,soldier,new UnityEngine.Vector3Int(4,3,0),ownSlot);
        var s3=Spawn(930003,soldier,new UnityEngine.Vector3Int(5,3,0),ownSlot);
        var c2=Spawn(930004,chinook,new UnityEngine.Vector3Int(3,1,0),ownSlot);
        Set(c2,"currentDomain",Domain.Air);Set(c2,"currentHeightLevel",HeightLevel.AirLow);
        var attack=Spawn(930085,apache,new UnityEngine.Vector3Int(5,2,0),ownSlot);
        var caca=Spawn(930080,fighter,new UnityEngine.Vector3Int(3,2,0),ownSlot);
        foreach(var air in new[]{attack,caca}){Set(air,"currentDomain",Domain.Air);Set(air,"currentHeightLevel",HeightLevel.AirLow);}
        for(int i=0;i<38;i++)Spawn(931000+i,soldier,new UnityEngine.Vector3Int(i<2?8:40+i%20,i<2?2+i:15,0),ownSlot==0?1:0);
        // Full editor scenes may have units already: only fixture units enter this isolated evaluation.
        var unrelated=new System.Collections.Generic.List<UnitManager>();foreach(var u in UnitManager.AllActive)if(!units.Contains(u))unrelated.Add(u);foreach(var u in unrelated)UnitManager.AllActive.Remove(u);
        try{
            Set(main,"currentAmmo",100);Set(extra,"currentAmmo",100);Set(carrier,"currentAmmo",100);
            var evaluate=typeof(AIController).GetMethod("EvaluateRallyReadiness",stat,null,new[]{typeof(ConstructionManager),typeof(TeamId),typeof(int),typeof(AIIntelReport)},null);
            object Eval(int turn=1){Clear();return evaluate.Invoke(null,new object[]{rally,TeamId.Blue,turn,null});}
            T Value<T>(object r,string field){return(T)r.GetType().GetField(field).GetValue(r);}
            var before=new System.Collections.Generic.Dictionary<int,(UnityEngine.Vector3Int cell,int hp,bool embarked)>();foreach(var u in units)before[u.InstanceId]=(u.CurrentCellPosition,u.CurrentHP,u.IsEmbarked);
            var r=Eval();
            Check(Value<int>(r,"Capturers")==5,"Capturadores: 3 no solo + 2 passageiros deveriam contar uma vez. Real="+Value<int>(r,"Capturers"));
            Check(Value<float>(r,"Assault")==2f,"Apache + Caca B no Tatico proprio deveriam somar ruptura 2. Real="+Value<float>(r,"Assault"));
            Check(Value<float>(r,"Artillery")==0f,"Caso inicial deveria ter zero suporte.");
            Check(Value<bool>(r,"GoGreen"),"Golf deveria GO_GREEN com piso e vantagem local. Missing="+Value<string>(r,"Missing"));
            Check(Value<int>(r,"VisibleThreats")==2,"Nao isolou os dois inimigos locais confirmados. Real="+Value<int>(r,"VisibleThreats"));
            Check(Value<int>(r,"RequiredForce")==4,"A banda aerea inflou a razao local: "+Value<int>(r,"RequiredForce"));
            var areaQuery=typeof(AIController).GetMethod("GetRallyArea",stat);
            var sharedArea=(RallyArea)areaQuery.Invoke(null,new object[]{rally,TeamId.Blue});
            Check(object.ReferenceEquals(sharedArea,areaQuery.Invoke(null,new object[]{rally,TeamId.Blue})),"A area foi recalculada na mesma revisao.");
            var positions=new System.Collections.Generic.Dictionary<UnitManager,UnityEngine.Vector3Int>{{attack,attack.CurrentCellPosition},{caca,caca.CurrentCellPosition},{carrier,carrier.CurrentCellPosition},{c2,c2.CurrentCellPosition}};
            var far=new UnityEngine.Vector3Int(16,2,0);
            Check(SectorManager.HexDistance(far,rally.CurrentCellPosition)==12f,"Caso distante nao esta a 12h.");
            Set(attack,"currentCellPosition",far);Set(caca,"currentCellPosition",new UnityEngine.Vector3Int(16,3,0));
            Set(carrier,"currentCellPosition",far);Set(c2,"currentCellPosition",new UnityEngine.Vector3Int(17,2,0));Clear();
            Check(AIController.IsRallyParticipantInBand(attack,rally.CurrentCellPosition,ReachBand.Tactical),"Apache de teste deveria ter alcance ate o rally.");
            Check(AIController.IsRallyParticipantInBand(carrier,rally.CurrentCellPosition,ReachBand.Tactical),"Chinook de teste deveria ter alcance ate o rally.");
            r=Eval();Check(Value<float>(r,"Assault")==0f,"Apache fora da area contou como ruptura pelo alcance.");
            Check(Value<int>(r,"Capturers")==3,"Passageiros do Chinook a 12h contaram como presentes.");
            Check(!AIController.IsRallyParticipantPresent(carrier,rally,ReachBand.Operational),"Chinook fora do Approach contou pelo raio de voo.");
            sharedArea=(RallyArea)areaQuery.Invoke(null,new object[]{rally,TeamId.Blue});
            UnityEngine.Vector3Int? forwardCell=null;
            foreach(var cell in sharedArea.Vanguard)if(!sharedArea.Core.Contains(cell)){forwardCell=cell;break;}
            Check(forwardCell.HasValue,"Fixture nao criou Vanguarda fora do Core terrestre.");
            var previousTile=map.GetTile(forwardCell.Value);map.SetTile(forwardCell.Value,sea.paletteTile);
            Set(caca,"currentCellPosition",forwardCell.Value);Clear();
            r=Eval();sharedArea=(RallyArea)areaQuery.Invoke(null,new object[]{rally,TeamId.Blue});
            Check(sharedArea.Vanguard.Contains(caca.CurrentCellPosition),"Celula do caca deixou de ser Vanguarda.");
            Check(Value<float>(r,"Assault")==1f&&AIController.IsRallyParticipantPresent(caca,rally,ReachBand.Tactical),"Caca B sobre o mar na Vanguarda nao contou ruptura 1.");
            Check(RallyFormationReport.LocateParticipant(sharedArea,caca.CurrentCellPosition,caca)==RallyLocation.Core,"Painel divergiu da presenca na Vanguarda.");
            map.SetTile(forwardCell.Value,previousTile);foreach(var position in positions)Set(position.Key,"currentCellPosition",position.Value);Clear();
            Set(main,"isEmbarked",false);Set(extra,"isEmbarked",false);Set(main,"currentCellPosition",new UnityEngine.Vector3Int(60,0,0));Set(extra,"currentCellPosition",new UnityEngine.Vector3Int(60,1,0));
            r=Eval();Check(!Value<bool>(r,"HoldReady")&&!Value<bool>(r,"GoGreen"),"Tres capturadores nao cumprem piso.");
            Set(main,"isEmbarked",true);Set(extra,"isEmbarked",true);Set(main,"currentCellPosition",carrier.CurrentCellPosition);Set(extra,"currentCellPosition",carrier.CurrentCellPosition);
            // Blindado embarcado e Ocupacao: nao infla Ruptura ou Suporte.
            var cargoTank=Spawn(930090,tank,c2.CurrentCellPosition,ownSlot);Set(cargoTank,"isEmbarked",true);Set(cargoTank,"embarkedTransporter",c2);
            r=Eval();Check(Value<float>(r,"Assault")==2f,"Tanque embarcado somou ruptura.");
            Check((string)typeof(RallyFormationReport).GetMethod("ExpectedPosition",stat).Invoke(null,new object[]{cargoTank})=="retaguarda","Carga embarcada foi apresentada como vanguarda.");
            var bomb=Spawn(930091,bomber,new UnityEngine.Vector3Int(5,5,0),ownSlot);Set(bomb,"currentDomain",Domain.Air);Set(bomb,"currentHeightLevel",HeightLevel.AirLow);
            r=Eval();Check(Value<float>(r,"Artillery")==1f&&Value<bool>(r,"GoGreen"),"Bombardeiro deve somar suporte sem trava.");
            var screen=typeof(AIController).GetMethod("HasAlliedCombatScreenAheadOfDrop",flags);
            var screenSnapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,MyUnits=new System.Collections.Generic.List<UnitManager>{carrier,main,caca}};
            Check((bool)screen.Invoke(ai,new object[]{carrier,main,screenSnapshot,rally.CurrentCellPosition,new UnityEngine.Vector3Int(4,8,0)}),"Ruptura aerea no Tatico da LZ nao contou como tela.");
            Set(caca,"currentAmmo",0);Check(!(bool)screen.Invoke(ai,new object[]{carrier,main,screenSnapshot,rally.CurrentCellPosition,new UnityEngine.Vector3Int(4,8,0)}),"Caca sem municao entrou pela tela generica.");Set(caca,"currentAmmo",100);
            // Saturacao: crescimento de 1 soldado/turno nao estende o limite de quatro turnos.
            var observe=typeof(AIController).GetMethod("ObserveRallySaturation",stat);var saturated=typeof(AIController).GetMethod("IsRallySaturated",stat);
            var sat=new SectorObjective();for(int t=1;t<=5;t++){observe.Invoke(null,new object[]{sat,t,true,2f,10+t});observe.Invoke(null,new object[]{sat,t,true,2f,10+t});}
            Check(sat.RallyFloorTurn==1&&(bool)saturated.Invoke(null,new object[]{sat,5,true}),"Saturacao foi adiada pelo crescimento marginal ou chamada repetida.");
            Check(sat.RallyStagnantTurns==4,"Observacao duplicada contou mais de uma vez por turno.");
            observe.Invoke(null,new object[]{sat,6,false,1f,8});Check(!(bool)saturated.Invoke(null,new object[]{sat,6,false}),"Perda do piso nao suspendeu saturacao.");
            // Progresso real reinicia estagnacao, mas nao o relogio desde o piso.
            var growing=new SectorObjective();for(int t=1;t<=5;t++)observe.Invoke(null,new object[]{growing,t,true,2f+t,10+3*t});
            Check(growing.RallyStagnantTurns==0&&(bool)saturated.Invoke(null,new object[]{growing,5,true}),"Crescimento real impediu limite de 4 turnos.");
            // Mesmo piso com outro rally held: nenhum relogio antes de controlar este predio.
            var otherRally=Building(939001,new UnityEngine.Vector3Int(4,1,0),ConstructionSector.Alpha);
            otherRally.Apply(city);Set(otherRally,"slotIndex",ownSlot);Set(otherRally,"teamId",TeamId.Blue);Set(otherRally,"isRallyPoint",true);Set(otherRally,"rallyOwnerSlots",new System.Collections.Generic.List<int>{ownSlot});
            var notHeld=new SectorObjective{Sector=ConstructionSector.Golf,ObjectiveType=AIObjectiveType.RallyAssembly};
            var update=typeof(AIController).GetMethod("UpdateRallyObjectiveState",stat);
            Set(rally,"slotIndex",ownSlot==0?1:0);
            for(int t=1;t<=4;t++){var pending=Eval(t);Check(!Value<bool>(pending,"Held")&&Value<int>(pending,"Capturers")>=4&&Value<bool>(pending,"BreakthroughReady"),"Caso sem hold nao montou piso numerico para testar o relogio.");update.Invoke(null,new object[]{notHeld,rally,TeamId.Blue,t,null,null});}
            Check(notHeld.RallyFloorTurn<0,"Saturacao comecou antes do hold.");
            Set(rally,"slotIndex",ownSlot);Clear();update.Invoke(null,new object[]{notHeld,rally,TeamId.Blue,5,null,null});
            Check(notHeld.RallyFloorTurn==5&&notHeld.RallyStagnantTurns==0&&notHeld.RallyReadinessReason.IndexOf("GO_GREEN(saturado)",System.StringComparison.Ordinal)<0,"Captura herdou quatro turnos de piso sem hold.");
            ConstructionManager.AllActive.RemoveAll(b=>b==otherRally);otherRally.gameObject.SetActive(false);
            plan.RallyAdvances.Clear();Clear();
            // Capitão congelado e historico sobrevivem ao roundtrip do objetivo.
            objective.RallyCaptainId=guard.InstanceId;objective.RallyParticipantIds.Add(attack.InstanceId);objective.RallyFloorTurn=2;objective.RallyPreviousForce=14;objective.RallyPreviousRupture=2f;objective.RallyLastObservedTurn=4;objective.RallyStagnantTurns=2;
            plan.RallyAdvances.Add(objective);plan.Objectives.Clear();
            var saved=ObjectiveManager.BuildSaveData();ObjectiveManager.RestoreSaveData(saved);
            var restored=ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(ownSlot));
            Check(restored.RallyAdvances.Count==1&&restored.RallyAdvances[0].RallyCaptainId==guard.InstanceId&&restored.RallyAdvances[0].RallyStagnantTurns==2,"Roundtrip perdeu capitao/saturacao.");
            var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,MyUnits=new System.Collections.Generic.List<UnitManager>()};
            foreach(var u in units)if(u.SlotIndex==ownSlot)snapshot.MyUnits.Add(u);
            var resolveCaptain=typeof(AIController).GetMethod("TryResolveRallyAdvanceCaptain",stat);
            var captainFollowerOrigin=attack.CurrentCellPosition;Set(attack,"currentCellPosition",new UnityEngine.Vector3Int(4,6,0));Clear();
            object[] captainArgs={attack,snapshot,null};
            Check((bool)resolveCaptain.Invoke(null,captainArgs)&&(UnitManager)captainArgs[2]==guard,"Escolta recalculou capitao em vez de ler o persistido.");
            var secondGroup=new SectorObjective{Sector=ConstructionSector.Alpha,RallyCaptainId=s3.InstanceId};restored.RallyAdvances.Add(secondGroup);
            Clear();captainArgs=new object[]{attack,snapshot,null};
            Check((bool)resolveCaptain.Invoke(null,captainArgs)&&(UnitManager)captainArgs[2]==s3,"Dois grupos: nao escolheu o primeiro pelo Operacional real.");
            secondGroup.RallyParticipantIds.Add(attack.InstanceId);
            typeof(AIController).GetMethod("ReconcileRallyAdvanceParticipants",stat).Invoke(null,new object[]{restored,TeamId.Blue});
            Check(secondGroup.RallyParticipantIds.Contains(attack.InstanceId)&&!restored.RallyAdvances[0].RallyParticipantIds.Contains(attack.InstanceId),"Dois capitaes ficaram esperando o mesmo reforco.");
            restored.RallyAdvances.Remove(secondGroup);
            Set(attack,"currentCellPosition",captainFollowerOrigin);Clear();
            var group=restored.RallyAdvances[0];group.RallyParticipantIds=new System.Collections.Generic.List<int>{s2.InstanceId};
            var captainOrigin=guard.CurrentCellPosition;Set(guard,"currentCellPosition",new UnityEngine.Vector3Int(9,2,0));Clear();
            var waiting=typeof(AIController).GetMethod("UpdateRallyAdvanceWaiting",stat);
            Check((bool)waiting.Invoke(null,new object[]{restored,TeamId.Blue})&&group.RallyWaiting,"Capitao nao esperou participante fora do Tatico.");
            var decide=typeof(AIController).GetMethod("DecideUnitAction",flags);
            var hold=(PlayerAction)decide.Invoke(ai,new object[]{guard,snapshot});Check(hold!=null&&hold.MoveTo==guard.CurrentCellPosition,"Capitao ESPERANDO continuou andando.");
            // O capitao continua esperando, mas pode atirar sem sair da propria celula.
            T Table<T>()where T:UnityEngine.Object{var ids=UnityEditor.AssetDatabase.FindAssets("t:"+typeof(T).Name);return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(ids[0]));}
            var stateHost=new UnityEngine.GameObject("RallyWaitingCombat");stateHost.SetActive(false);var state=stateHost.AddComponent<TurnStateManager>();
            Set(state,"rpsDatabase",Table<RPSDatabase>());Set(state,"dpqMatchupDatabase",Table<DPQMatchupDatabase>());Set(state,"weaponPriorityData",Table<WeaponPriorityData>());Set(ai,"turnStateManager",state);Set(ai,"currentAISlotIndex",ownSlot);
            var nearEnemy=units.Find(u=>u.SlotIndex!=ownSlot&&u.CurrentCellPosition==new UnityEngine.Vector3Int(8,2,0));
            int enemyHp=nearEnemy.CurrentHP,captainHp=guard.CurrentHP,captainAmmo=guard.CurrentAmmo;
            Set(nearEnemy,"currentHP",1);Set(guard,"currentHP",10);Set(guard,"currentAmmo",100);Clear();
            var shot=(PlayerAction)decide.Invoke(ai,new object[]{guard,snapshot});
            Check(shot!=null&&shot.SensorAction==SensorActionType.Attack&&shot.MoveTo==guard.CurrentCellPosition,"Capitao em espera nao tentou tiro parado legal.");
            Set(nearEnemy,"currentHP",enemyHp);Set(guard,"currentHP",captainHp);Set(guard,"currentAmmo",captainAmmo);
            Set(guard,"currentCellPosition",captainOrigin);Clear();
            Check(!(bool)waiting.Invoke(null,new object[]{restored,TeamId.Blue})&&!group.RallyWaiting,"Grupo nao saiu da espera ao reunir.");
            var stragglerOrigin=s2.CurrentCellPosition;Set(s2,"currentCellPosition",new UnityEngine.Vector3Int(44,2,0));Clear();
            Check(!(bool)waiting.Invoke(null,new object[]{restored,TeamId.Blue})&&!group.RallyWaiting&&!group.RallyParticipantIds.Contains(s2.InstanceId),"Retardatario a 40h congelou a invasao ou permaneceu no grupo.");
            Set(s2,"currentCellPosition",stragglerOrigin);Clear();
            foreach(var u in units)if(before.TryGetValue(u.InstanceId,out var old)&&u!=main&&u!=extra)Check(u.CurrentCellPosition==old.cell&&u.CurrentHP==old.hp&&u.IsEmbarked==old.embarked,"Prontidao alterou tabuleiro confirmado.");
            return "PASS D2: Golf 36 inimigos na base/2 locais, RequiredForce=4 com Chinook de longo alcance; Apache e carga a 12h fora da area nao contam; Caca B sobre o mar na Vanguarda conta 1; area cacheada compartilhada; captura 5, ruptura 2, suporte zero GO_GREEN; tres capturadores recusados; embarcado e Ocupacao; Bombardeiro suporte opcional; saturacao idempotente e limite de quatro turnos; capitao/saturacao persistidos; escolha entre grupos por Operacional; capitao espera, atira parado e retoma; retardatario 40h removido; piso sem hold nao satura; tela aerea apta/sem municao; sem mutacao da decisao. Play nao observado.";
        }finally{foreach(var u in unrelated)if(u!=null&&!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);Clear();}
    }finally{typeof(ObjectiveManager).GetField("instance",stat).SetValue(null,oldObjective);}
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
