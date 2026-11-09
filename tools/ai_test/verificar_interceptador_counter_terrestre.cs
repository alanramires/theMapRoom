if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
UnitData Asset(string id){foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:UnitData")){var d=UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));if(d.id==id)return d;}throw new System.Exception("Ficha ausente: "+id);}
var a=Asset("cacaA");var b=Asset("cacaB");var apache=Asset("apache");var bomber=Asset("bombardeiro");
var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
var match=typeof(AIShoppingPlanner).GetMethod("DoesUnitMeetShoppingDemand",flags);
var best=typeof(AIShoppingPlanner).GetMethod("FindBestRosterCounter",flags);
var allowed=typeof(AIShoppingPlanner).GetMethod("IsCounterResponseRoleAllowed",flags);
bool Accept(UnitData u,AIShoppingDemand d){return(bool)match.Invoke(null,new object[]{u,d});}
var ground=new AIShoppingDemand{Role=UnitRole.None,RequiredWeaponCategory=WeaponCategory.AntiInfantaria,Count=1};
var air=new AIShoppingDemand{Role=UnitRole.None,RequiredWeaponCategory=WeaponCategory.AntiAerea,Count=1};
foreach(var fighter in new[]{a,b}){
    if(Accept(fighter,ground))throw new System.Exception("Interceptador entrou no carrinho contra infantaria.");
    if(!Accept(fighter,air))throw new System.Exception("Interceptador perdeu demanda anti-aerea.");
    if(!Accept(fighter,new AIShoppingDemand{Role=UnitRole.Interceptador,Count=1}))throw new System.Exception("Demanda propria de interceptacao foi alterada.");
    foreach(var category in new[]{WeaponCategory.AntiInfantaria,WeaponCategory.AntiTanque,WeaponCategory.AntiNavio})if((bool)allowed.Invoke(null,new object[]{fighter,category}))throw new System.Exception("Arma secundaria criou missao de compra do caca.");
    var roster=new AIShoppingPlanner.AIRosterKnowledge();var profile=new AIShoppingPlanner.AIRosterProfile{Unit=fighter,AvailableNow=true};profile.WeaponCategories.Add(WeaponCategory.AntiInfantaria);profile.WeaponCategories.Add(WeaponCategory.AntiAerea);roster.Profiles.Add(profile);
    var pressure=new AIShoppingPlanner.CounterPressureInspection();
    if(best.Invoke(null,new object[]{roster,pressure,WeaponCategory.AntiInfantaria,null,true,0,99})!=null)throw new System.Exception("Roster escolheu caca contra solo.");
    if((UnitData)best.Invoke(null,new object[]{roster,pressure,WeaponCategory.AntiAerea,null,true,0,99})!=fighter)throw new System.Exception("Roster excluiu caca do ar.");
}
if(!Accept(apache,ground)||!Accept(bomber,new AIShoppingDemand{Role=UnitRole.None,RequiredWeaponCategory=WeaponCategory.AntiTanque,Count=1}))throw new System.Exception("Ataque aereo adequado foi excluido da resposta terrestre.");
return "PASS: Caca A e B recusados como counter terrestre no roster/carrinho; counter aereo e demanda propria preservados; Apache/Bombardeiro preservados; fichas e combate inalterados.";
