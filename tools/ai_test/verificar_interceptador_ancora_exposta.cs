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
    UnitData Clone(string id){var data=UnityEngine.Object.Instantiate(Asset<UnitData>(id));created.Add(data);return data;}
    var fighterData=Clone("cacaA");var fastEnemy=Clone("cacaA");fastEnemy.id="fixture-fast-air";fastEnemy.movement=60;var samData=Asset<UnitData>("sam");
    UnitData ewacsData=null;foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:UnitData")){var data=UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));if(data.id.ToLowerInvariant().Contains("ewacs")){ewacsData=data;break;}}
    if(ewacsData==null)throw new System.Exception("EWACS ausente.");
    Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,fighterData,fastEnemy,samData,ewacsData});typeof(UnitDatabase).GetMethod("RebuildLookup",flags).Invoke(unitDb,null);
    for(int x=0;x<45;x++)for(int y=0;y<20;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
    UnitManager Spawn(int id,UnitData data,int x,int y,bool own){var cell=new UnityEngine.Vector3Int(x,y,0);var u=Unit(id,data,cell,false);u.gameObject.SetActive(true);u.Apply(data);Set(u,"boardTilemap",map);Set(u,"slotIndex",own?ownSlot:ownSlot==0?1:0);Set(u,"teamId",own?TeamId.Blue:TeamId.Yellow);Set(u,"currentCellPosition",cell);Set(u,"isDead",false);Set(u,"isEmbarked",false);Set(u,"hasActed",false);u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);return u;}
    var fighter=Spawn(981001,fighterData,2,3,true);var ewacs=Spawn(981002,ewacsData,3,3,true);var captain=Spawn(981003,soldier,9,3,true);var enemy=Spawn(981004,fastEnemy,35,15,false);var sam=Spawn(981005,samData,7,3,true);
    Set(fighter,"currentDomain",Domain.Air);Set(fighter,"currentHeightLevel",HeightLevel.AirHigh);Set(enemy,"currentDomain",Domain.Air);Set(enemy,"currentHeightLevel",HeightLevel.AirHigh);Set(ewacs,"currentDomain",Domain.Air);Set(ewacs,"currentHeightLevel",HeightLevel.AirHigh);
    void Position(UnitManager u,int x,int y){var old=u.CurrentCellPosition;var c=new UnityEngine.Vector3Int(x,y,0);Set(u,"currentCellPosition",c);u.transform.position=HexCoordinates.GetCellCenterWorld(map,c);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,old,c);}
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,MyUnits=new System.Collections.Generic.List<UnitManager>{fighter,ewacs,captain},EnemyUnits=new System.Collections.Generic.List<UnitManager>{enemy}};
    var host=new UnityEngine.GameObject("AirControlPostureTest");host.SetActive(false);var ai=host.AddComponent<AIController>();Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var choose=typeof(AIController).GetMethod("ResolveAirCombatFallbackAnchor",flags);
    (UnityEngine.Vector3Int Cell,string Tier,bool Shared) Anchor(UnitManager actor=null,bool register=true){actor=actor??fighter;var paths=UnitMovementPathRules.CalcularCaminhosValidos(map,actor,actor.RemainingMovementPoints,terrain);object[] args={actor,snapshot,actor.CurrentCellPosition,new System.Collections.Generic.List<UnitManager>{enemy},null,false,paths,new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(),null,register};var cell=(UnityEngine.Vector3Int)choose.Invoke(ai,args);return(cell,(string)args[4],(bool)args[5]);}
    bool Exposed(UnitManager target){return AIAirExposureService.IsExposed(target.CurrentCellPosition,snapshot,snapshot.EnemyUnits,map,terrain,new System.Collections.Generic.Dictionary<int,UnitReachEnvelope>(),new System.Collections.Generic.Dictionary<GameUnitClass,System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>());}
    if(!Exposed(ewacs)||!Exposed(captain))throw new System.Exception("Fixture nao expoe ambos os elos.");
    var both=Anchor();if(!both.Shared||!both.Tier.StartsWith("Exposed:EWACS#")||!both.Tier.Contains("+Vanguarda#"))throw new System.Exception("Nao encontrou cobertura conjunta: "+both.Tier);
    var tactical=UnitReachEnvelopeService.Build(new UnitReachRequest{Unit=fighter,BoardMap=map,TerrainDatabase=terrain,Intent=ReachIntent.Mobility,Band=ReachBand.Tactical});if(!tactical.CanReach(both.Cell))throw new System.Exception("Cobertura conjunta fora do Tatico.");
    var planned=(PlayerAction)typeof(AIController).GetMethod("DecideRogueAirCombatAction",flags).Invoke(ai,new object[]{fighter,snapshot,null});
    if(planned==null||!planned.HasMoveTo||planned.MoveTo!=both.Cell)throw new System.Exception("Acao final perdeu a celula de cobertura conjunta.");
    var claims=(System.Collections.Generic.Dictionary<int,int>)typeof(AIController).GetField("plannedEwacsEscorts",flags).GetValue(ai);
    if(!claims.TryGetValue(ewacs.InstanceId,out int firstEscort)||firstEscort!=fighter.InstanceId)throw new System.Exception("Primeiro caca nao registrou escolta do EWACS.");
    var second=Spawn(981006,fighterData,2,4,true);Set(second,"currentDomain",Domain.Air);Set(second,"currentHeightLevel",HeightLevel.AirHigh);snapshot.MyUnits.Add(second);
    var secondChoice=Anchor(second);if(secondChoice.Shared||secondChoice.Cell!=captain.CurrentCellPosition||!secondChoice.Tier.Contains("escoltado por #"+fighter.InstanceId))throw new System.Exception("Segundo caca nao desceu ao capitao exposto: "+secondChoice.Tier);
    snapshot.MyUnits.Remove(captain);var duplicate=Anchor(second);if(duplicate.Cell!=ewacs.CurrentCellPosition||claims[ewacs.InstanceId]!=fighter.InstanceId)throw new System.Exception("Sem outro elo nao aceitou EWACS ou sobrescreveu primeiro escolta.");
    snapshot.MyUnits.Add(captain);claims.Clear();Anchor(second,register:false);if(claims.Count!=0)throw new System.Exception("Consulta auxiliar criou claim.");
    var fresh=Anchor(second);if(!fresh.Shared||claims[ewacs.InstanceId]!=second.InstanceId)throw new System.Exception("Nova passada simulada manteve claim antigo.");
    if(!Anchor(second).Shared)throw new System.Exception("Reavaliacao desviou o proprio escolta.");
    snapshot.MyUnits.Remove(second);claims.Clear();
    Position(captain,38,3);var split=Anchor();if(split.Shared||split.Cell!=ewacs.CurrentCellPosition||split.Tier.Contains("+Vanguarda"))throw new System.Exception("Sem cobertura conjunta nao priorizou EWACS exposto: "+split.Tier);
    snapshot.MyUnits.Add(sam);if(Exposed(ewacs))throw new System.Exception("SAM nao cobriu EWACS.");
    var covered=Anchor();if(covered.Cell!=captain.CurrentCellPosition||!covered.Tier.Contains("Vanguarda"))throw new System.Exception("EWACS coberto puxou caca: "+covered.Tier);
    snapshot.MyUnits.Remove(captain);snapshot.MyHQ=Building(981010,new UnityEngine.Vector3Int(0,1,0),ConstructionSector.Alpha);
    var noCaptain=Anchor();if(noCaptain.Shared||noCaptain.Cell!=enemy.CurrentCellPosition||noCaptain.Tier!="Operational:CubicNearestEnemy")throw new System.Exception("EWACS coberto sem capitao nao seguiu cadeia antiga: "+noCaptain.Tier);
    snapshot.MyUnits.Add(captain);snapshot.MyHQ=null;
    snapshot.MyUnits.Remove(ewacs);var noEwacs=Anchor();if(noEwacs.Cell!=captain.CurrentCellPosition)throw new System.Exception("Sem EWACS perdeu vanguarda.");
    if(fighter.CurrentCellPosition!=new UnityEngine.Vector3Int(2,3,0)||fighter.HasActed)throw new System.Exception("Consulta mudou estado confirmado.");
    return "PASS: exposicao compartilhada com shopping; cobertura conjunta no Tatico do caca; EWACS exposto vence quando separados; SAM coberto nao puxa; sem EWACS segue vanguarda; EWACS coberto sem capitao segue inimigo mais proximo, nao QG; claim local distribui segundo caca, permite duplicar sem outro elo, nao nasce em consulta auxiliar e reinicia apos limpeza; sem mover unidade ou confirmar acao.";
}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
