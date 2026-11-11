// Uma vez (D3, 2026-11-11): tira do asset a linha do antigo TransportadorAereo (15),
// que saiu do enum. Idempotente.
if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var catalog=UnitRoleCatalog.Active;if(catalog==null)return "catalogo ausente";
var entries=(System.Collections.Generic.List<UnitRoleCatalogEntry>)typeof(UnitRoleCatalog).GetField("entries",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(catalog);
int removed=entries.RemoveAll(e=>(int)e.role==15||!System.Enum.IsDefined(typeof(UnitRole),e.role));
foreach(var e in entries)e.satisfazTambem.RemoveAll(r=>(int)r==15||!System.Enum.IsDefined(typeof(UnitRole),r));
catalog.Invalidate();UnityEditor.EditorUtility.SetDirty(catalog);UnityEditor.AssetDatabase.SaveAssets();
return "removidas "+removed+" linhas; "+entries.Count+" papeis no catalogo";
