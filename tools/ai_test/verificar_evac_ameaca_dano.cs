// Fixture fora do Play: régua da ameaça do EVAC (autor 2026-11-14, P1–P4).
// A) GetAirEvacThreat mede dano previsto ao TRANSPORTE pela Matriz de HP (AICombatHpSimulator),
//    somado entre os inimigos cujo envelope de combate alcança a célula.
// B) TryPickEvacApproachMove: quem sobrevive vence; entre sobreviventes, avanço; ninguém sobrevive,
//    menor dano. No degrau seguro, ordem antiga (avanço).
// C) A célula do PASSAGEIRO continua na régua de proximidade (GetEvacDropThreat).
if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var created=new System.Collections.Generic.List<UnityEngine.Object>();
var units=new System.Collections.Generic.List<UnitManager>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var anyFlags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static;
void Set(object o,string field,object value){
    var t=o.GetType(); System.Reflection.FieldInfo f=null;
    while(t!=null&&f==null){f=t.GetField(field,flags);t=t.BaseType;}
    if(f==null)throw new System.Exception("Campo ausente: "+field);f.SetValue(o,value);}
T Asset<T>(string id)where T:UnityEngine.Object{
    foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:"+typeof(T).Name)){
        var item=UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
        var f=typeof(T).GetField("id");if(f!=null && (string)f.GetValue(item)==id)return item;
    }throw new System.Exception("Asset ausente: "+id);
}
T First<T>()where T:UnityEngine.Object{
    foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:"+typeof(T).Name)){
        var item=UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));if(item!=null)return item;}
    throw new System.Exception("Asset ausente: "+typeof(T).Name);
}
void Check(bool ok,string reason){if(!ok)throw new System.Exception(reason);}
var report=new System.Text.StringBuilder();
try{
    var grid=new UnityEngine.GameObject("EvacThreatGrid",typeof(UnityEngine.Grid));created.Add(grid);grid.transform.position=new UnityEngine.Vector3(20000,20000,0);
    grid.GetComponent<UnityEngine.Grid>().cellLayout=UnityEngine.GridLayout.CellLayout.Hexagon;
    var mapHost=new UnityEngine.GameObject("EvacThreatMap",typeof(UnityEngine.Tilemaps.Tilemap));mapHost.transform.SetParent(grid.transform,false);
    var map=mapHost.GetComponent<UnityEngine.Tilemaps.Tilemap>();map.tileAnchor=UnityEngine.Vector3.zero;
    var plain=Asset<TerrainTypeData>("plains");
    for(int x=0;x<16;x++)for(int y=0;y<7;y++)map.SetTile(new UnityEngine.Vector3Int(x,y,0),plain.paletteTile);
    var terrain=UnityEngine.ScriptableObject.CreateInstance<TerrainDatabase>();created.Add(terrain);Set(terrain,"terrains",new System.Collections.Generic.List<TerrainTypeData>{plain});
    var soldier=Asset<UnitData>("Soldado");var chinook=Asset<UnitData>("chinook");var aaa=Asset<UnitData>("aaa");
    var unitDb=UnityEngine.ScriptableObject.CreateInstance<UnitDatabase>();created.Add(unitDb);Set(unitDb,"units",new System.Collections.Generic.List<UnitData>{soldier,chinook,aaa});
    int ownSlot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{TeamId.Blue});
    if(ownSlot<0)throw new System.Exception("Sem MatchController ativo para resolver slot Blue.");
    int enemySlot=ownSlot==0?1:0;
    UnitManager Unit(int id,UnitData data,UnityEngine.Vector3Int cell,int slot,TeamId team,Domain domain,HeightLevel height){
        var go=new UnityEngine.GameObject("EvacThreatUnit");go.SetActive(false);created.Add(go);var u=go.AddComponent<UnitManager>();u.enabled=false;
        Set(u,"unitDatabase",unitDb);Set(u,"unitId",data.id);Set(u,"instanceId",id);Set(u,"boardTilemap",map);
        u.ApplyFromDatabase();
        Set(u,"slotIndex",slot);Set(u,"teamId",team);Set(u,"currentCellPosition",cell);Set(u,"currentHP",data.maxHP);Set(u,"isDead",false);
        Set(u,"currentFuel",100);Set(u,"remainingMovementPoints",data.movement);Set(u,"isEmbarked",false);
        Set(u,"currentDomain",domain);Set(u,"currentHeightLevel",height);
        u.transform.position=HexCoordinates.GetCellCenterWorld(map,cell);
        units.Add(u);return u;
    }
    var carrier=Unit(810001,chinook,new UnityEngine.Vector3Int(1,3,0),ownSlot,TeamId.Blue,Domain.Air,HeightLevel.AirLow);
    var rifle=Unit(810010,soldier,new UnityEngine.Vector3Int(2,3,0),enemySlot,TeamId.Red,Domain.Land,HeightLevel.Surface);
    var aa=Unit(810011,aaa,new UnityEngine.Vector3Int(12,3,0),enemySlot,TeamId.Red,Domain.Land,HeightLevel.Surface);

    var aiHost=new UnityEngine.GameObject("EvacThreatAI");aiHost.SetActive(false);created.Add(aiHost);var ai=aiHost.AddComponent<AIController>();
    Set(ai,"boardTilemap",map);Set(ai,"terrainDatabase",terrain);
    var tsmHost=new UnityEngine.GameObject("EvacThreatTSM");tsmHost.SetActive(false);created.Add(tsmHost);var tsm=tsmHost.AddComponent<TurnStateManager>();
    Set(tsm,"rpsDatabase",First<RPSDatabase>());Set(tsm,"dpqMatchupDatabase",First<DPQMatchupDatabase>());Set(tsm,"weaponPriorityData",First<WeaponPriorityData>());
    Set(ai,"turnStateManager",tsm);

    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=ownSlot,
        MyUnits=new System.Collections.Generic.List<UnitManager>{carrier},
        EnemyUnits=new System.Collections.Generic.List<UnitManager>{rifle,aa}};
    var mapType=typeof(AIController).GetNestedType("EvacThreatMap",anyFlags);
    Check(mapType!=null,"EvacThreatMap ausente");
    System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,float> NewMap()=>(System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,float>)System.Activator.CreateInstance(mapType,anyFlags,null,new object[]{carrier,snapshot},null);
    var threat=typeof(AIController).GetMethod("GetAirEvacThreat",anyFlags);
    var drop=typeof(AIController).GetMethod("GetEvacDropThreat",anyFlags);
    float T(System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,float> m,UnityEngine.Vector3Int c)=>(float)threat.Invoke(ai,new object[]{c,TeamId.Blue,m});

    // ---- A) régua por dano previsto
    var rps=First<RPSDatabase>();var dpq=First<DPQMatchupDatabase>();var prio=First<WeaponPriorityData>();
    int Expected(UnitData attacker){int best=0;for(int d=1;d<=5;d++){var r=AICombatHpSimulator.Simulate(attacker,chinook,attacker.maxHP,chinook.maxHP,d,rps,dpq,prio);if(r.isValid)best=UnityEngine.Mathf.Max(best,chinook.maxHP-r.defenderHpAfter);}return best;}
    int rifleDamage=Expected(soldier), aaDamage=Expected(aaa);
    Check(rifleDamage>0&&aaDamage>rifleDamage,"Matriz de HP nao distingue soldado ("+rifleDamage+") de AAA ("+aaDamage+") contra Chinook");
    var mapA=NewMap();
    float nearRifle=T(mapA,new UnityEngine.Vector3Int(1,3,0)); // célula do Chinook, ao lado do soldado
    Check(UnityEngine.Mathf.Approximately(nearRifle,rifleDamage),"Celula ao lado do soldado deveria valer o dano do rifle ("+rifleDamage+"), veio "+nearRifle);
    float farFromAll=T(mapA,new UnityEngine.Vector3Int(7,0,0));
    float nearAa=T(mapA,new UnityEngine.Vector3Int(11,3,0));
    Check(nearAa>=aaDamage-0.01f,"Celula ao lado da AAA deveria valer ao menos o dano da AAA ("+aaDamage+"), veio "+nearAa);
    Check(nearAa>nearRifle,"AAA pesou igual ou menos que soldado: "+nearAa+" x "+nearRifle);
    report.Append("A) dano previsto: rifle=").Append(nearRifle).Append(" AAA=").Append(nearAa).Append(" meio do mapa=").Append(farFromAll).AppendLine();

    // ---- C) célula do passageiro continua na proximidade
    var mapC=NewMap();
    float dropThreat=(float)drop.Invoke(ai,new object[]{new UnityEngine.Vector3Int(1,3,0),TeamId.Blue,mapC});
    float legacy=(float)typeof(AIController).GetMethod("CalculateThreatLevel",anyFlags,null,new[]{typeof(UnityEngine.Vector3Int),typeof(TeamId)},null).Invoke(ai,new object[]{new UnityEngine.Vector3Int(1,3,0),TeamId.Blue});
    Check(UnityEngine.Mathf.Approximately(dropThreat,legacy),"Celula do passageiro saiu da regua de proximidade: "+dropThreat+" x "+legacy);
    report.Append("C) passageiro na proximidade=").Append(dropThreat).AppendLine();

    // ---- B) ordem da aproximação (cache pré-preenchido: a ordem, não o envelope, é o que se testa)
    var pick=typeof(AIController).GetMethod("TryPickEvacApproachMove",anyFlags);
    Check(pick!=null,"TryPickEvacApproachMove ausente");
    var from=carrier.CurrentCellPosition; var anchor=new UnityEngine.Vector3Int(14,3,0);
    // Os três avançam rumo à âncora: curto 2h, ruim 7h (sempre letal), longo 8h.
    var shortStep=new UnityEngine.Vector3Int(3,3,0); var longStep=new UnityEngine.Vector3Int(7,3,0); var bad=new UnityEngine.Vector3Int(6,3,0);
    var paths=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>{
        {from,new System.Collections.Generic.List<UnityEngine.Vector3Int>{from}},
        {shortStep,new System.Collections.Generic.List<UnityEngine.Vector3Int>{from,shortStep}},
        {bad,new System.Collections.Generic.List<UnityEngine.Vector3Int>{from,bad}},
        {longStep,new System.Collections.Generic.List<UnityEngine.Vector3Int>{from,longStep}}};
    UnityEngine.Vector3Int Pick(float dShort,float dBad,float dLong,int hp,bool safeOnly){
        Set(carrier,"currentHP",hp);
        var m=NewMap(); m[from]=dShort; m[shortStep]=dShort; m[bad]=dBad; m[longStep]=dLong;
        // Matriz disponível: fontes montadas (vazias), o cache pré-preenchido responde.
        var mt=m.GetType(); mt.GetField("Sources").SetValue(m,System.Activator.CreateInstance(mt.GetField("Sources").FieldType)); mt.GetField("SourcesBuilt").SetValue(m,true);
        var risks=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,(float peak,float total)>{
            {from,(dShort,dShort)},{shortStep,(dShort,dShort)},{bad,(dBad,dBad)},{longStep,(dLong,dLong)}};
        var args=new object[]{carrier,snapshot,from,anchor,paths,risks,m,null,safeOnly,true,null,null};
        bool ok=(bool)pick.Invoke(ai,args);
        return ok?(UnityEngine.Vector3Int)args[10]:from;
    }
    // Caso da marca: o passo curto tem menos dano, o longo avança 6 e sobrevive. Sai o longo.
    var r1=Pick(2f,12f,4f,10,false);
    Check(r1==longStep,"Com HP 10, sobrevive nos dois e deveria avancar (longo), escolheu "+r1);
    // Longo mata (dano >= HP): passo curto sobrevive e vence.
    var r2=Pick(2f,12f,10f,10,false);
    Check(r2==shortStep,"Longo letal deveria perder para o curto que sobrevive, escolheu "+r2);
    // Ninguém sobrevive: menor dano.
    var r3=Pick(5f,12f,6f,3,false);
    Check(r3==shortStep,"Sem sobrevivente deveria escolher menor dano (curto), escolheu "+r3);
    // Degrau seguro: tudo zero, ordem antiga = avanço.
    var r4=Pick(0f,0f,0f,10,true);
    Check(r4==longStep,"Degrau seguro deveria avancar (longo), escolheu "+r4);
    // Regra do autor: sair da zona de ameaça rumo à base vence avançar mais dentro dela.
    var r5=Pick(0f,12f,3f,10,false);
    Check(r5==shortStep,"Celula fora da zona (dano 0) deveria vencer celula com dano que avanca mais, escolheu "+r5);
    report.Append("B) aproximacao: marca=longo, longo letal=curto, ninguem sobrevive=menor dano, seguro=avanco, sai da zona=borda").AppendLine();
    return "PASS: "+report.ToString().Replace("\n"," | ");
}finally{
    foreach(var u in units)if(u!=null)UnitManager.AllActive.Remove(u);
    for(int i=created.Count-1;i>=0;i--)if(created[i]!=null)UnityEngine.Object.DestroyImmediate(created[i]);
}
