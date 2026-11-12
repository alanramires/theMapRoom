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
    var city=UnityEngine.Object.Instantiate(Asset<ConstructionData>("cidade"));created.Add(city);city.id="commandCityTest";city.maxUnitsServedPerTurn=5;
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
    var savedUnits=new System.Collections.Generic.List<UnitManager>(UnitManager.AllActive);UnitManager.AllActive.Clear();
    try{
        void Check(bool yes,string why){if(!yes)throw new System.Exception(why);}
        var cell=new UnityEngine.Vector3Int(4,2,0);
        var town=Building(980010,cell,ConstructionSector.Alpha);town.Apply(city);Set(town,"slotIndex",ownSlot);Set(town,"teamId",TeamId.Blue);Set(town,"currentCapturePoints",town.CapturePointsMax);Set(town,"hasInfiniteSuppliesOverride",true);
        var matchHost=new UnityEngine.GameObject("CommandFamilyMatch");matchHost.SetActive(false);var match=matchHost.AddComponent<MatchController>();Set(match,"debugFogOfWarEnabled",false);Set(match,"currentTurn",15);Set(match,"activePlayerListIndex",ownSlot);Set(match,"activeTeamId",(int)TeamId.Blue);
        var players=(System.Collections.IList)typeof(MatchController).GetField("players",flags).GetValue(match);var p=players[ownSlot];p.GetType().GetField("teamId",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(p,TeamId.Blue);players[ownSlot]=p;match.TrySetActualMoney(PlayerSlotId.FromIndex(ownSlot),100000);
        var host=new UnityEngine.GameObject("CommandFamilyState");host.SetActive(false);var state=host.AddComponent<TurnStateManager>();Set(state,"terrainTilemap",map);Set(state,"terrainDatabase",terrain);Set(state,"matchController",match);
        UnitManager Spawn(int id,UnitData data,UnityEngine.Vector3Int at,bool aboard=false){var u=Unit(id,data,at,aboard);u.gameObject.SetActive(true);u.Apply(data);Set(u,"boardTilemap",map);Set(u,"slotIndex",ownSlot);Set(u,"teamId",TeamId.Blue);Set(u,"isDead",false);Set(u,"hasActed",false);Set(u,"isEmbarked",aboard);Set(u,"currentHP",3);Set(u,"currentFuel",data.autonomia);Set(u,"remainingMovementPoints",data.movement);Set(u,"currentDomain",data.domain);Set(u,"currentHeightLevel",data.heightLevel);Set(u,"currentCellPosition",at);u.transform.position=HexCoordinates.GetCellCenterWorld(map,at);if(!UnitManager.AllActive.Contains(u))UnitManager.AllActive.Add(u);UnitOccupancyRules.NotifyUnitOccupancyChanged(u,at,at);return u;}
        var heli=Spawn(980056,chinook,cell);var first=Spawn(980003,soldier,cell,true);var second=Spawn(980004,soldier,cell,true);var blocker=Spawn(980099,soldier,cell);
        Set(first,"embarkedTransporter",heli);Set(second,"embarkedTransporter",heli);Set(heli,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{slotId="passageiros",slotIndex=0,seatIndex=0,embarkedUnit=first},new UnitTransportSeatRuntime{slotId="passageiros",slotIndex=0,seatIndex=1,embarkedUnit=second}});
        var queueField=typeof(TurnStateManager).GetField("commandServiceQueuedOrders",flags);var queue=(System.Collections.Generic.List<ServicoDoComandoOption>)queueField.GetValue(state);
        System.Collections.Generic.List<ServicoDoComandoOption> Options(){var list=new System.Collections.Generic.List<ServicoDoComandoOption>();ServicoDoComandoSensor.CollectOptions(ownSlot,map,terrain,list,out _);return list;}
        // Even an Air-capable construction cannot serve a loose aircraft over a blocked floor.
        city.supplierOperationDomains.Add(new TerrainLayerMode{domain=Domain.Air,heightLevel=HeightLevel.AirLow});
        var blocked=Options();Check(!blocked.Exists(o=>o.targetUnit==heli||o.targetUnit==first||o.targetUnit==second)&&blocked.Exists(o=>o.targetUnit==blocker),"Aliado no chao nao teve prioridade ou familia voadora recebeu por cima.");
        Set(blocker,"slotIndex",ownSlot==0?1:0);UnitOccupancyRules.NotifyUnitOccupancyChanged(blocker,cell,cell);blocked=Options();Check(!blocked.Exists(o=>o.targetUnit==heli||o.targetUnit==first||o.targetUnit==second),"Inimigo embaixo nao bloqueou a familia.");
        var away=new UnityEngine.Vector3Int(9,5,0);Set(blocker,"currentCellPosition",away);blocker.transform.position=HexCoordinates.GetCellCenterWorld(map,away);UnitOccupancyRules.NotifyUnitOccupancyChanged(blocker,cell,away);
        Set(heli,"currentHP",heli.GetMaxHP());var healthy=Options();Check(!healthy.Exists(o=>o.targetUnit==heli)&&healthy.Exists(o=>o.targetUnit==first),"Heli inteiro impediu atendimento do passageiro.");
        // A full family must stay landed from the first service until the last passenger.
        void Reset(bool airborne,int heliHp=3){
            foreach(var u in new[]{heli,first,second}){u.ClearReceivedSuppliesThisTurn();Set(u,"hasActed",false);Set(u,"currentHP",u==heli?heliHp:3);Set(u,"currentFuel",u.GetMaxFuel());}
            Set(heli,"currentDomain",airborne?Domain.Air:Domain.Land);Set(heli,"currentHeightLevel",airborne?HeightLevel.AirLow:HeightLevel.Surface);Set(heli,"currentCellPosition",cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(heli,cell,cell);
            var cache=typeof(TurnStateManager).GetField("commandServiceServedUnitInstanceIds",flags).GetValue(state);cache.GetType().GetMethod("Clear").Invoke(cache,null);
            match.TrySetActualMoney(PlayerSlotId.FromIndex(ownSlot),100000);queue.Clear();
        }
        void Run(bool expectAll){
            var routine=(System.Collections.IEnumerator)typeof(TurnStateManager).GetMethod("ExecuteCommandServiceOrderSequence",flags).Invoke(state,null);
            var stack=new System.Collections.Generic.Stack<System.Collections.IEnumerator>();stack.Push(routine);bool landed=false;int steps=0;
            try{while(stack.Count>0){if(++steps>20000)throw new System.Exception("Corrotina de servico nao terminou.");var it=stack.Peek();if(!it.MoveNext()){(it as System.IDisposable)?.Dispose();stack.Pop();continue;}if(heli.GetDomain()!=Domain.Air)landed=true;if(expectAll&&landed&&heli.GetDomain()==Domain.Air&&(!first.ReceivedSuppliesThisTurn||!second.ReceivedSuppliesThisTurn))throw new System.Exception("Decolou antes de atender todos os passageiros.");if(it.Current is System.Collections.IEnumerator child)stack.Push(child);}}
            finally{while(stack.Count>0)(stack.Pop()as System.IDisposable)?.Dispose();}
        }
        Reset(true);queue.AddRange(Options());Check(queue.Exists(o=>o.targetUnit==heli)&&queue.Exists(o=>o.targetUnit==first)&&queue.Exists(o=>o.targetUnit==second),"Familia incompleta antes da execucao.");Run(true);
        Check(heli.GetDomain()==Domain.Air&&heli.ReceivedSuppliesThisTurn&&first.ReceivedSuppliesThisTurn&&second.ReceivedSuppliesThisTurn,"Familia nao recebeu servicos e voltou ao voo.");
        Reset(true,heli.GetMaxHP());queue.AddRange(Options());Run(true);Check(heli.GetDomain()==Domain.Air&&first.ReceivedSuppliesThisTurn&&second.ReceivedSuppliesThisTurn,"Heli inteiro nao pousou para servir carga e retornar.");
        // Only enough money for the root: skipped cargo must not keep it landed forever.
        Reset(true,9);var limitedOptions=Options();var rootOrder=limitedOptions.Find(o=>o.targetUnit==heli);Check(rootOrder!=null,"Sem ordem do heli para caso parcial.");queue.Add(rootOrder);
        var estimate=typeof(TurnStateManager).GetMethod("EstimateCommandServiceQueuedOrders",flags).Invoke(state,new object[]{null});int rootCost=(int)estimate.GetType().GetField("totalCost").GetValue(estimate);Check(rootCost>0,"Fixture parcial precisa de custo positivo.");
        queue.Clear();queue.AddRange(limitedOptions);match.TrySetActualMoney(PlayerSlotId.FromIndex(ownSlot),rootCost);Run(false);
        Check(heli.GetDomain()==Domain.Air&&heli.ReceivedSuppliesThisTurn&&(!first.ReceivedSuppliesThisTurn||!second.ReceivedSuppliesThisTurn),"Sem saldo para passageiros, heli nao encerrou a tentativa e decolou.");
        Reset(false);queue.AddRange(Options());Run(false);Check(heli.GetDomain()==Domain.Land&&first.ReceivedSuppliesThisTurn,"Heli que comecou pousado decolou.");
        // A stale order blocked after preview must fail before HP, stock or money is consumed.
        Reset(true);queue.AddRange(Options());Set(blocker,"slotIndex",ownSlot);Set(blocker,"currentCellPosition",cell);blocker.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);UnitOccupancyRules.NotifyUnitOccupancyChanged(blocker,away,cell);
        int beforeMoney=match.GetActualMoney(PlayerSlotId.FromIndex(ownSlot));Run(false);Check(heli.CurrentHP==3&&first.CurrentHP==3&&!first.ReceivedSuppliesThisTurn&&match.GetActualMoney(PlayerSlotId.FromIndex(ownSlot))==beforeMoney,"Ordem obsoleta consumiu servico com pouso negado.");
        // Supplier cargo uses its original path: an artillery passenger in a supply truck remains eligible.
        Set(blocker,"currentCellPosition",away);UnitOccupancyRules.NotifyUnitOccupancyChanged(blocker,cell,away);
        var obus=Asset<UnitData>("artCampanha");Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,obus});var supplier=Spawn(980067,truck,new UnityEngine.Vector3Int(1,1,0));var artillery=Spawn(980068,obus,supplier.CurrentCellPosition,true);Set(artillery,"embarkedTransporter",supplier);Set(supplier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{embarkedUnit=artillery,slotIndex=0,seatIndex=0}});
        Check(Options().Exists(o=>o.sourceSupplierUnit==supplier&&o.targetUnit==artillery),"Artilharia embarcada no supridor perdeu atendimento automatico.");
        // Aircraft embarked in a naval supplier still receive automatic service; KC does not serve loose flyers.
        var carrierData=Asset<UnitData>("portaAvioes");var fighterData=Asset<UnitData>("cacaA");var tankerData=Asset<UnitData>("aviaoTanque");Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,truck,obus,carrierData,fighterData,tankerData});
        var sea=Asset<TerrainTypeData>("sea");Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain,forest,sea});var shipCell=new UnityEngine.Vector3Int(7,4,0);map.SetTile(shipCell,sea.paletteTile);
        var carrier=Spawn(980100,carrierData,shipCell);var fighter=Spawn(980101,fighterData,shipCell,true);Set(fighter,"embarkedTransporter",carrier);Set(carrier,"transportedUnitSlots",new System.Collections.Generic.List<UnitTransportSeatRuntime>{new UnitTransportSeatRuntime{embarkedUnit=fighter,slotIndex=0,seatIndex=0}});
        Check(Options().Exists(o=>o.sourceSupplierUnit==carrier&&o.targetUnit==fighter),"Caca embarcado no porta-avioes perdeu atendimento.");
        var tanker=Spawn(980102,tankerData,new UnityEngine.Vector3Int(8,4,0));var airborne=Spawn(980103,fighterData,new UnityEngine.Vector3Int(8,5,0));Set(airborne,"currentFuel",1);
        Check(!Options().Exists(o=>o.sourceSupplierUnit==tanker&&o.targetUnit==airborne),"KC abasteceu aeronave solta no Servico do Comando.");
        return "PASS: pouso negado por aliado/inimigo bloqueia heli e carga; aliado no solo recebe; heli inteiro atende passageiros; familia so decola apos a carga; saldo parcial nao prende decolagem; pousado permanece; fila obsoleta revalidada sem cobrar; artilharia no supridor e caca no porta-avioes preservados; KC nao atende voador solto. Corrotina executada apenas em cenario isolado fora do Play.";
    }finally{UnitManager.AllActive.Clear();UnitManager.AllActive.AddRange(savedUnits);}

}finally{
    UnityEngine.Application.logMessageReceived-=captureLog;
    foreach(var pair in fogRestore)Set(pair.Key,"debugFogOfWarEnabled",pair.Value);
    foreach(var u in units)UnitManager.AllActive.Remove(u);
    foreach(var b in buildings)ConstructionManager.AllActive.Remove(b);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);
}
