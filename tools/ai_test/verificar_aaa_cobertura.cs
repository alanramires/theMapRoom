if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var select=typeof(AIController).GetMethod("TryChooseGroundAntiAirCoverage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
if(select==null)throw new System.Exception("Recompile o Editor antes do teste.");
var origin=new UnityEngine.Vector3Int(0,15,0);
var step=new UnityEngine.Vector3Int(1,15,0);
var guard=new UnityEngine.Vector3Int(2,15,0);
var longPaths=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>{
    {origin,new System.Collections.Generic.List<UnityEngine.Vector3Int>{origin}},
    {step,new System.Collections.Generic.List<UnityEngine.Vector3Int>{origin,step}},
    {guard,new System.Collections.Generic.List<UnityEngine.Vector3Int>{origin,step,guard}}
};
var tactical=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>{
    {origin,longPaths[origin]},{step,longPaths[step]}
};
var occupied=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
var reserved=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
var producers=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
var origins=new System.Collections.Generic.List<UnityEngine.Vector3Int>{new UnityEngine.Vector3Int(5,15,0)};
var arrivals=new System.Collections.Generic.List<System.Collections.Generic.HashSet<UnityEngine.Vector3Int>>{
    new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>{guard,new UnityEngine.Vector3Int(3,15,0)}
};
object[] args=new object[]{origin,longPaths,tactical,occupied,reserved,producers,origins,arrivals,1,null,null,null};
if(!(bool)select.Invoke(null,args) || (UnityEngine.Vector3Int)args[9]!=step || (UnityEngine.Vector3Int)args[10]!=guard)
    throw new System.Exception("Nao preparou cobertura avancando apenas pela rota Tactical.");
reserved.Add(step);
if((bool)select.Invoke(null,args))throw new System.Exception("Avancou por destino reservado ou inventou salto ate o alvo longo.");
reserved.Clear();occupied.Add(step);
if((bool)select.Invoke(null,args))throw new System.Exception("Escolheu destino ocupado.");
occupied.Clear();producers.Add(step);producers.Add(guard);
if((bool)select.Invoke(null,args))throw new System.Exception("Bloqueou produtor.");
producers.Clear();arrivals[0].Clear();arrivals[0].Add(new UnityEngine.Vector3Int(30,30,0));
if((bool)select.Invoke(null,args))throw new System.Exception("Perseguiu aeronave sem aproximacao cobrivel no componente terrestre.");
arrivals[0].Clear();arrivals[0].Add(origin);
var onlyOrigin=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,System.Collections.Generic.List<UnityEngine.Vector3Int>>{{origin,longPaths[origin]}};
args[1]=onlyOrigin;
if(!(bool)select.Invoke(null,args) || (UnityEngine.Vector3Int)args[9]!=origin)
    throw new System.Exception("Nao manteve cobertura ja preparada (deixaria coesao da base desfazer a posicao).");
return "PASS: cobertura de chegada futura, progresso na rota real/Tactical, reserva, ocupacao, produtor, aeronave distante e manutencao de posicao preparada. Nenhuma jogada executada.";
