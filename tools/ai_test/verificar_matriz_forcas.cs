if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var sf=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
void Set(object target,string name,object value)=>target.GetType().GetField(name,flags).SetValue(target,value);
void Check(bool ok,string why){if(!ok)throw new System.Exception(why);}
var rows=new System.Collections.Generic.List<string>();
var catalog=new System.Collections.Generic.Dictionary<string,(UnitData Data,UnitDatabase Database)>(System.StringComparer.OrdinalIgnoreCase);
foreach(string guid in UnityEditor.AssetDatabase.FindAssets("t:UnitDatabase"))
{
    string path=UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var db=UnityEditor.AssetDatabase.LoadAssetAtPath<UnitDatabase>(path);
    if(db==null)continue;
    rows.Add("\nUnitDatabase: "+path);
    var sorted=new System.Collections.Generic.List<UnitData>();foreach(var data in db.Units)if(data!=null)sorted.Add(data);
    sorted.Sort((a,b)=>string.Compare(a.id,b.id,System.StringComparison.OrdinalIgnoreCase));
    foreach(var data in sorted)
    {
        var role=data.roles!=null&&data.roles.Count>0?data.roles[0]:UnitRole.None;
        rows.Add(data.displayName+" ["+data.id+"]: "+role+" -> "+UnitRoleCompatibility.ResolveFamily(data)+" -> "+UnitRoleCompatibility.ResolveForce(data));
        if(!string.IsNullOrEmpty(data.id)&&!catalog.ContainsKey(data.id))catalog.Add(data.id,(data,db));
    }
}
Check(catalog.Count>0,"Nenhuma ficha nos UnitDatabase.");
void Force(string id,UnitForce expected){Check(catalog.ContainsKey(id),"Ficha ausente do UnitDatabase: "+id);Check(UnitRoleCompatibility.ResolveForce(catalog[id].Data)==expected,id+" fora de "+expected);}
foreach(string id in new[]{"bombardeiro","obusLeve","tanqueZ"})Force(id,UnitForce.Dissuasao);
foreach(string id in new[]{"apache","cacaB","cacaA","tanque B"})Force(id,UnitForce.Ruptura);
foreach(string id in new[]{"Soldado","chinook","apc"})Force(id,UnitForce.Ocupacao);
foreach(string id in new[]{"ewacs","suprimentos"})Force(id,UnitForce.Apoio);
Check(UnitRoleCompatibility.ResolveForce((UnitData)null)==UnitForce.Nenhuma,"Ficha ausente recebeu forca.");
Check((int)UnitRole.AtaqueAereo==9&&UnitRoleCompatibility.MatchesLabel(UnitRole.DisruptorDeCaptura,UnitRole.AtaqueAereo)
    &&UnitRoleCompatibility.CanSatisfy(UnitRole.ArtilheiroCombatente,UnitRole.Assalto),"Rotulo ou compatibilidade alterados.");
Check(UnitRoleCompatibility.ResolveCompositionRole(catalog["tanqueZ"].Data)==UnitRole.FogoIndireto,"Z ainda preenche composicao de Assalto.");
var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
var oldBuildings=new System.Collections.Generic.List<ConstructionManager>(ConstructionManager.AllActive);
var objectiveField=typeof(ObjectiveManager).GetField("instance",sf);var oldObjective=objectiveField.GetValue(null);
var analyzerField=typeof(AITacticalAnalyzer).GetField("instance",sf);var oldAnalyzer=analyzerField.GetValue(null);
var shoppingField=typeof(AIShoppingPlanner).GetField("instance",sf);var oldShopping=shoppingField.GetValue(null);
try
{
    var host=new UnityEngine.GameObject("ForceMatrixFixture");host.SetActive(false);
    objectiveField.SetValue(null,host.AddComponent<ObjectiveManager>());
    analyzerField.SetValue(null,host.AddComponent<AITacticalAnalyzer>());
    shoppingField.SetValue(null,host.AddComponent<AIShoppingPlanner>());
    int slot=(int)typeof(AIController).GetMethod("ResolveAISlotKey",sf).Invoke(null,new object[]{TeamId.Blue});
    var hq=host.AddComponent<ConstructionManager>();Set(hq,"teamId",TeamId.Blue);Set(hq,"slotIndex",slot);Set(hq,"siteRuntime",new ConstructionSiteRuntime{isPlayerHeadQuarter=true});
    ConstructionManager.AllActive.Clear();ConstructionManager.AllActive.Add(hq);
    UnitManager Unit(string id,int instance)
    {
        var go=new UnityEngine.GameObject(id);go.SetActive(false);go.transform.SetParent(host.transform);
        var unit=go.AddComponent<UnitManager>();Set(unit,"unitDatabase",catalog[id].Database);Set(unit,"unitId",id);Set(unit,"instanceId",instance);Set(unit,"slotIndex",slot);Set(unit,"teamId",TeamId.Blue);Set(unit,"currentHP",10);Set(unit,"isDead",false);
        // Distante e sem armas runtime: expectativa de formacao deve vir so da Forca.
        Set(unit,"currentCellPosition",new UnityEngine.Vector3Int(1000,1000,0));
        return unit;
    }
    var expected=typeof(RallyFormationReport).GetMethod("ExpectedPosition",sf);
    var z=Unit("tanqueZ",997001);var tank=Unit("tanque B",997002);var bomber=Unit("bombardeiro",997003);
    Check((string)expected.Invoke(null,new object[]{tank})=="vanguarda","Ruptura longe/sem arma runtime nao ficou na vanguarda.");
    Check((string)expected.Invoke(null,new object[]{z})=="retaguarda"&&(string)expected.Invoke(null,new object[]{bomber})=="retaguarda","Dissuasao nao ficou atras.");
    var snapshot=new AIWorldSnapshot{AITeam=TeamId.Blue,AISlotIndex=slot,TurnNumber=1,Stance=AIStance.Tactical,MyHQ=hq};
    for(int i=0;i<4;i++)snapshot.MyUnits.Add(Unit("Soldado",997010+i));
    snapshot.MyUnits.Add(z);
    var build=typeof(AIShoppingPlanner).GetMethod("BuildRoleShoppingDemands",sf);
    System.Collections.Generic.List<AIShoppingDemand> Demands()=>(System.Collections.Generic.List<AIShoppingDemand>)build.Invoke(null,new object[]{snapshot,false});
    int Count(System.Collections.Generic.List<AIShoppingDemand> ds,UnitRoleFamily family){int n=0;foreach(var demand in ds)if(demand.Origin.Contains("composition")&&demand.RequiredFamily==family)n+=demand.Count;return n;}
    var withZ=Demands();int zAssault=Count(withZ,UnitRoleFamily.Assalto),zFire=Count(withZ,UnitRoleFamily.FogoDeSuporte);
    snapshot.MyUnits[4]=tank;var withTank=Demands();int tankAssault=Count(withTank,UnitRoleFamily.Assalto),tankFire=Count(withTank,UnitRoleFamily.FogoDeSuporte);
    Check(zAssault==2&&zFire==0&&tankAssault==1&&tankFire==1,"Pacote nao refletiu Z como fogo: Z="+zAssault+"/"+zFire+" tanque="+tankAssault+"/"+tankFire);
    // A linha escolhe por composicao; a cadeia pergunta SOMENTE eliteFrom.
    var mbt=Unit("tanqueA",997020);var howitzer=Unit("obusMedio",997021);
    var zData=catalog["tanqueZ"].Data;var campaignData=catalog["artCampanha"].Data;
    Check(zData.eliteFrom==catalog["tanqueA"].Data,"Ficha do Pesado nao aponta para o MBT.");
    Check(campaignData.eliteFrom==catalog["obusMedio"].Data,"Ficha da Campanha nao aponta para o Obuseiro Movel.");
    Set(hq,"siteRuntime",new ConstructionSiteRuntime{isPlayerHeadQuarter=true,sellingRule=ConstructionUnitMarketRule.FreeMarket,
        offeredUnits=new System.Collections.Generic.List<UnitData>{catalog["tanqueA"].Data,zData,campaignData}});
    snapshot.MyBuildings.Add(hq);snapshot.Budget=100000;snapshot.IncomePerTurn=10000;
    snapshot.MyUnits.Add(mbt);snapshot.MyUnits.Add(howitzer);
    var progression=typeof(AIShoppingPlanner).GetMethod("AddEliteProgressionDemand",sf);
    var chain=typeof(AIShoppingPlanner).GetMethod("IsEliteChainAvailable",sf);
    var accepts=typeof(AIShoppingPlanner).GetMethod("DoesUnitMeetShoppingDemand",sf);
    System.Collections.Generic.List<AIShoppingDemand> Progress(UnitRole role,int active)
    {var demands=new System.Collections.Generic.List<AIShoppingDemand>();progression.Invoke(null,new object[]{snapshot,demands,role,active,25,int.MaxValue});return demands;}
    Check((bool)chain.Invoke(null,new object[]{zData,snapshot})&&(bool)chain.Invoke(null,new object[]{campaignData,snapshot}),"Pais reais em campo nao liberaram as duas cadeias.");
    var assaultProgress=Progress(UnitRole.Assalto,2);var fireProgress=Progress(UnitRole.FogoIndireto,1);
    Check(assaultProgress.Count==0,"Assalto progrediu alem do MBT usando o Pesado.");
    Check(fireProgress.Count==1&&fireProgress[0].MinEliteLevel==2&&fireProgress[0].MaxEliteLevel==2,"Fogo nao pediu elite 2 com os pais em campo.");
    Check((bool)accepts.Invoke(null,new object[]{zData,fireProgress[0]})&&(bool)accepts.Invoke(null,new object[]{campaignData,fireProgress[0]}),"Pesado/Campanha nao compartilham a demanda de Fogo elite 2.");
    snapshot.MyUnits.Remove(mbt);var secondBasic=Unit("tanque B",997022);snapshot.MyUnits.Add(secondBasic);
    Check(!(bool)chain.Invoke(null,new object[]{zData,snapshot})&&(bool)chain.Invoke(null,new object[]{campaignData,snapshot}),"Sem MBT, Pesado ignorou eliteFrom ou bloqueou a Campanha.");
    var firstArmor=Progress(UnitRole.Assalto,1);
    Check(firstArmor.Count==1&&firstArmor[0].MinEliteLevel==1&&firstArmor[0].MaxEliteLevel==1,"Tanque Leve nao progride ate o MBT.");
    snapshot.MyUnits.Remove(secondBasic);snapshot.MyUnits.Add(mbt);snapshot.MyUnits.Remove(howitzer);
    Check((bool)chain.Invoke(null,new object[]{zData,snapshot})&&!(bool)chain.Invoke(null,new object[]{campaignData,snapshot}),"Sem Obuseiro Movel, Campanha ignorou eliteFrom ou bloqueou o Pesado.");
    snapshot.MyUnits.Add(howitzer);
    rows.Add("Progressao medida em verificar_matriz_forcas/casoPesado: Leve -> MBT (elite 1); com MBT, Assalto sem proximo degrau. Fogo elite 2 aceita Pesado e Campanha; cada um exige seu eliteFrom, inclusive entre familias.");
    float zAssaultWeight=(float)typeof(AIController).GetMethod("GetRallyAssaultWeight",sf).Invoke(null,new object[]{catalog["tanqueZ"].Data});
    float zArtilleryWeight=(float)typeof(AIController).GetMethod("GetRallyArtilleryWeight",sf).Invoke(null,new object[]{catalog["tanqueZ"].Data});
    Check(zAssaultWeight==0f,"Z ainda contou como ruptura no helper por composicao.");
    Check(zArtilleryWeight==1f,"D2: Z deveria contar suporte 1 pela Forca de Dissuasao.");
    int wall=(int)typeof(AIShoppingPlanner).GetMethod("ResolveWallPressure",sf).Invoke(null,new object[]{catalog["tanqueZ"].Data,false});
    Check(wall==12,"Z perdeu os 3 pontos ja existentes da parede elite 2.");
    rows.Add("\nDemanda medida (4 Soldados + 1 peca, sem ameaca/oferta): Tanque Z -> Assalto x"+zAssault+", Fogo x"+zFire+"; Tanque B -> Assalto x"+tankAssault+", Fogo x"+tankFire+".");
    rows.Add("Rally atual Z: ruptura="+zAssaultWeight+", artilharia="+zArtilleryWeight+". Parede ponderada Z="+(wall/4f)+" pontos (ja era artilharia). D2: suporte pela Forca de Dissuasao, sem gate.");
    rows.Add("PASS D1: matriz integral; grupos reais; compatibilidades/valor 9 preservados; expectativa por Forca independente de posicao/arma; demanda do Z medida. Sem fichas modificadas nem partida em Play.");
    return string.Join("\n",rows);
}
finally
{
    objectiveField.SetValue(null,oldObjective);analyzerField.SetValue(null,oldAnalyzer);shoppingField.SetValue(null,oldShopping);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    ConstructionManager.AllActive.Clear();ConstructionManager.AllActive.AddRange(oldBuildings);
}
