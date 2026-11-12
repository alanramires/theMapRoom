// Somente leitura: custo unitario de ResolveAISlotKey e de FindAnyObjectByType<MatchController>.
if(!UnityEngine.Application.isPlaying)return "fora do Play";
var sb=new System.Text.StringBuilder();
var m=typeof(AIController).GetMethod("ResolveAISlotKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
var sw=System.Diagnostics.Stopwatch.StartNew();
for(int i=0;i<1000;i++)m.Invoke(null,new object[]{TeamId.Yellow});
sb.Append("ResolveAISlotKey x1000=").Append(sw.ElapsedMilliseconds).Append("ms").AppendLine();
sw.Restart(); for(int i=0;i<1000;i++)UnityEngine.Object.FindAnyObjectByType<MatchController>();
sb.Append("FindAnyObjectByType<MatchController> x1000=").Append(sw.ElapsedMilliseconds).Append("ms").AppendLine();
int cons=0;foreach(var c in ConstructionManager.AllActive)if(c!=null)cons++;
int units=0;foreach(var u in UnitManager.AllActive)if(u!=null&&!u.IsDead)units++;
sb.Append("construcoes=").Append(cons).Append(" unidades=").Append(units);
return sb.ToString();
