// Transcricao UNICA da tabela que vivia em switch no UnitRole.cs (ate a D3, 2026-11-11)
// para o asset UnitRoleCatalog. Recusa sobrescrever: depois de criado, o asset e a
// fonte e se edita em Tools > Role Families.
if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
const string folder="Assets/DB/Resources";const string path=folder+"/UnitRoleCatalog.asset";
if(UnityEditor.AssetDatabase.LoadAssetAtPath<UnitRoleCatalog>(path)!=null)return "JA EXISTE: "+path+" (edite em Tools > Role Families)";
if(!UnityEditor.AssetDatabase.IsValidFolder(folder))UnityEditor.AssetDatabase.CreateFolder("Assets/DB","Resources");
var catalog=UnityEngine.ScriptableObject.CreateInstance<UnitRoleCatalog>();
var entries=(System.Collections.Generic.List<UnitRoleCatalogEntry>)typeof(UnitRoleCatalog).GetField("entries",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(catalog);
void Row(UnitRole role,UnitRoleFamily familia,UnitForce forca,UnitBattleParticipation part,UnitRole contaComo,params UnitRole[] satisfaz)
{entries.Add(new UnitRoleCatalogEntry{role=role,familia=familia,forca=forca,participacao=part,contaComo=contaComo,satisfazTambem=new System.Collections.Generic.List<UnitRole>(satisfaz)});}
var D=UnitBattleParticipation.Direct;var I=UnitBattleParticipation.Indirect;var N=UnitBattleParticipation.None;
// Capturador
Row(UnitRole.Capturador,UnitRoleFamily.Capturador,UnitForce.Ocupacao,D,UnitRole.Capturador);
Row(UnitRole.CapturadorCombatente,UnitRoleFamily.Capturador,UnitForce.Ocupacao,D,UnitRole.Capturador,UnitRole.Capturador,UnitRole.Assalto);
// Assalto (Ruptura)
Row(UnitRole.Assalto,UnitRoleFamily.Assalto,UnitForce.Ruptura,D,UnitRole.Assalto);
Row(UnitRole.DisruptorDeCaptura,UnitRoleFamily.Assalto,UnitForce.Ruptura,D,UnitRole.AtaqueAereo,UnitRole.AtaqueAereo);
Row(UnitRole.Interceptador,UnitRoleFamily.Assalto,UnitForce.Ruptura,D,UnitRole.Interceptador);
Row(UnitRole.InterceptadorDeDisruptor,UnitRoleFamily.Assalto,UnitForce.Ruptura,D,UnitRole.Interceptador,UnitRole.Interceptador);
// Fogo de Suporte (Dissuasao) — D1: AtaqueAereo e ArtilheiroCombatente aqui
Row(UnitRole.FogoIndireto,UnitRoleFamily.FogoDeSuporte,UnitForce.Dissuasao,I,UnitRole.FogoIndireto);
Row(UnitRole.AtaqueAereo,UnitRoleFamily.FogoDeSuporte,UnitForce.Dissuasao,D,UnitRole.AtaqueAereo);
Row(UnitRole.ArtilheiroCombatente,UnitRoleFamily.FogoDeSuporte,UnitForce.Dissuasao,D,UnitRole.FogoIndireto,UnitRole.Assalto,UnitRole.FogoIndireto);
Row(UnitRole.Antiaereo,UnitRoleFamily.FogoDeSuporte,UnitForce.Dissuasao,D,UnitRole.Antiaereo,UnitRole.FogoIndireto);
Row(UnitRole.AntiaereoCombatente,UnitRoleFamily.FogoDeSuporte,UnitForce.Dissuasao,D,UnitRole.AntiaereoCombatente,UnitRole.Antiaereo,UnitRole.Assalto,UnitRole.FogoIndireto);
// Transportador (Ocupacao)
Row(UnitRole.Transportador,UnitRoleFamily.Transportador,UnitForce.Ocupacao,N,UnitRole.Transportador);
// Vigilancia / Logistica (Apoio)
Row(UnitRole.Vigilancia,UnitRoleFamily.Vigilancia,UnitForce.Apoio,N,UnitRole.Vigilancia);
Row(UnitRole.Logistica,UnitRoleFamily.Logistica,UnitForce.Apoio,N,UnitRole.Logistica);
Row(UnitRole.Estoque,UnitRoleFamily.Logistica,UnitForce.Apoio,N,UnitRole.Estoque);
foreach(UnitRole r in System.Enum.GetValues(typeof(UnitRole)))if(r!=UnitRole.None&&!entries.Exists(e=>e.role==r))throw new System.Exception("Papel sem linha na transcricao: "+r);
UnityEditor.AssetDatabase.CreateAsset(catalog,path);UnityEditor.AssetDatabase.SaveAssets();UnityEditor.AssetDatabase.Refresh();
return "CRIADO: "+path+" com "+entries.Count+" papeis";
