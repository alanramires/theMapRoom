// D3: o catalogo de papeis e a fonte; marcar uma flag muda a vaga do rally.
if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
void Check(bool ok,string why){if(!ok)throw new System.Exception(why);}
var stat=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
var catalog=UnitRoleCatalog.Active;
Check(catalog!=null,"UnitRoleCatalog ausente (rode criar_catalogo_papeis.cs).");
Check(UnityEditor.AssetDatabase.GetAssetPath(catalog).StartsWith("Assets/DB/Resources/"),"Catalogo fora de Assets/DB/Resources: "+UnityEditor.AssetDatabase.GetAssetPath(catalog));
foreach(UnitRole r in System.Enum.GetValues(typeof(UnitRole)))if(r!=UnitRole.None)Check(catalog.Get(r)!=null,"Papel sem linha: "+r);
// (a) a transcricao bate com a tabela que vivia em codigo
Check(UnitRoleCompatibility.ResolveFamily(UnitRole.AtaqueAereo)==UnitRoleFamily.FogoDeSuporte&&UnitRoleCompatibility.ResolveFamily(UnitRole.ArtilheiroCombatente)==UnitRoleFamily.FogoDeSuporte,"D1 perdida: AtaqueAereo/ArtilheiroCombatente fora de FogoDeSuporte.");
Check(UnitRoleCompatibility.ResolveFamily(UnitRole.DisruptorDeCaptura)==UnitRoleFamily.Assalto&&UnitRoleCompatibility.ResolveForce(UnitRole.DisruptorDeCaptura)==UnitForce.Ruptura,"Apache (Disruptor) saiu de Assalto/Ruptura.");
Check(UnitRoleCompatibility.ResolveForce(UnitRole.Transportador)==UnitForce.Ocupacao&&UnitRoleCompatibility.ResolveForce(UnitRole.Estoque)==UnitForce.Apoio&&UnitRoleCompatibility.ResolveForce(UnitRole.Antiaereo)==UnitForce.Dissuasao,"Forcas de Ocupacao/Apoio/Dissuasao alteradas.");
Check(UnitRoleCompatibility.ResolveBattleParticipation(UnitRole.FogoIndireto)==UnitBattleParticipation.Indirect&&UnitRoleCompatibility.ResolveBattleParticipation(UnitRole.Logistica)==UnitBattleParticipation.None&&UnitRoleCompatibility.ResolveBattleParticipation(UnitRole.Assalto)==UnitBattleParticipation.Direct,"Participacao alterada.");
Check(UnitRoleCompatibility.CanSatisfy(UnitRole.CapturadorCombatente,UnitRole.Assalto)&&UnitRoleCompatibility.CanSatisfy(UnitRole.AntiaereoCombatente,UnitRole.FogoIndireto)&&!UnitRoleCompatibility.CanSatisfy(UnitRole.Interceptador,UnitRole.Assalto)&&!UnitRoleCompatibility.CanSatisfy(UnitRole.Capturador,UnitRole.Assalto),"Tabela 'satisfaz tambem' alterada.");
Check(UnitRoleCompatibility.MatchesLabel(UnitRole.DisruptorDeCaptura,UnitRole.AtaqueAereo)&&!UnitRoleCompatibility.MatchesLabel(UnitRole.CapturadorCombatente,UnitRole.Capturador),"MatchesLabel mudou.");
UnitData Ficha(string id){foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:UnitData")){var d=UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));if(d!=null&&d.id==id)return d;}throw new System.Exception("Ficha ausente: "+id);}
var cacaB=Ficha("cacaB");var tanqueZ=Ficha("tanqueZ");var apache=Ficha("apache");
Check(UnitRoleCompatibility.ResolveCompositionRole(tanqueZ)==UnitRole.FogoIndireto&&UnitRoleCompatibility.ResolveCompositionRole(apache)==UnitRole.AtaqueAereo&&UnitRoleCompatibility.ResolveCompositionRole(cacaB)==UnitRole.Interceptador,"'conta como' alterado.");
Check(!UnitRoleCompatibility.CanSatisfy(cacaB,UnitRole.Assalto),"Caca B ja satisfaz Assalto sem a flag.");
// (b) a flag no catalogo muda a resposta — num clone, sem tocar o asset
var clone=UnityEngine.Object.Instantiate(catalog);
try{
    UnitRoleCatalog.SetActiveForTests(clone);
    clone.GetOrAdd(UnitRole.Interceptador).satisfazTambem.Add(UnitRole.Assalto);clone.Invalidate();
    Check(UnitRoleCompatibility.CanSatisfy(UnitRole.Interceptador,UnitRole.Assalto)&&UnitRoleCompatibility.CanSatisfy(cacaB,UnitRole.Assalto),"Flag marcada nao fez o caca satisfazer Assalto.");
    // A Forca e da ESSENCIA (roles[0]): o Caca B e InterceptadorDeDisruptor.
    clone.GetOrAdd(UnitRole.InterceptadorDeDisruptor).forca=UnitForce.Dissuasao;clone.Invalidate();
    Check(UnitRoleCompatibility.ResolveForce(cacaB)==UnitForce.Dissuasao,"Trocar a Forca no catalogo nao mudou a Forca da ficha.");
    clone.GetOrAdd(UnitRole.Interceptador).satisfazTambem.Remove(UnitRole.Assalto);clone.GetOrAdd(UnitRole.InterceptadorDeDisruptor).forca=UnitForce.Ruptura;clone.Invalidate();
    Check(!UnitRoleCompatibility.CanSatisfy(cacaB,UnitRole.Assalto)&&UnitRoleCompatibility.ResolveForce(cacaB)==UnitForce.Ruptura,"Desmarcar nao voltou ao estado anterior.");
}finally{UnitRoleCatalog.SetActiveForTests(null);UnityEngine.Object.DestroyImmediate(clone);}
Check(UnitRoleCatalog.Active==catalog,"Catalogo real nao foi restaurado.");
Check(!UnitRoleCompatibility.CanSatisfy(cacaB,UnitRole.Assalto),"O asset real foi alterado pelo teste.");
// (c) vaga do rally por Forca: Interceptador recrutado cobre uma das duas vagas de ruptura
var obj=new SectorObjective{Sector=ConstructionSector.Golf,ObjectiveType=AIObjectiveType.RallyAssembly};
obj.Slots.Add(new SlotNeed{Role=UnitRole.Interceptador,Filled=true,AssignedUnitId=424242});
var ensure=typeof(AIController).GetMethod("EnsureRallyAssemblySlots",stat);
ensure.Invoke(null,new object[]{obj,4,2,TeamId.Blue,UnityEngine.Vector3Int.zero});
int openAssault=0,openCap=0,interceptors=0;foreach(var s in obj.Slots){if(s.Role==UnitRole.Assalto&&!s.Filled)openAssault++;if(s.Role==UnitRole.Capturador&&!s.Filled)openCap++;if(s.Role==UnitRole.Interceptador)interceptors++;}
Check(openAssault==1&&openCap==4&&interceptors==1,"Vaga por Forca errada: assalto aberto="+openAssault+" cap="+openCap+" interceptador="+interceptors);
ensure.Invoke(null,new object[]{obj,4,2,TeamId.Blue,UnityEngine.Vector3Int.zero});
openAssault=0;foreach(var s in obj.Slots)if(s.Role==UnitRole.Assalto&&!s.Filled)openAssault++;
Check(openAssault==1&&obj.Slots.Count==6,"Segunda chamada nao foi idempotente: "+obj.Slots.Count+" vagas.");
// Transportador NAO cobre vaga de capturador (Ocupacao e por rotulo, nao por Forca)
var obj2=new SectorObjective{Sector=ConstructionSector.Golf,ObjectiveType=AIObjectiveType.RallyAssembly};
obj2.Slots.Add(new SlotNeed{Role=UnitRole.Transportador,Filled=true,AssignedUnitId=424243});
ensure.Invoke(null,new object[]{obj2,4,2,TeamId.Blue,UnityEngine.Vector3Int.zero});
openCap=0;foreach(var s in obj2.Slots)if(s.Role==UnitRole.Capturador&&!s.Filled)openCap++;
Check(openCap==4,"Transportador cobriu vaga de capturador.");
return "PASS D3: catalogo em Assets/DB/Resources cobre todos os papeis; tabela transcrita bate (D1 inclusa); flag 'satisfaz Assalto' e troca de Forca no clone mudam a resposta e o asset real fica intacto; vaga de ruptura do rally por Forca (Interceptador cobre 1 de 2), capturador por rotulo. Sem Play.";
