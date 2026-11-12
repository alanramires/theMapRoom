if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
var instanceFlags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var instance=typeof(AIController).GetField("_instance",flags);var oldInstance=instance.GetValue(null);
var cache=typeof(AIController).GetField("cachedMatchController",flags);var oldCache=cache.GetValue(null);
var resolved=typeof(AIController).GetField("cachedMatchControllerResolved",flags);var oldResolved=resolved.GetValue(null);
var get=typeof(AIController).GetMethod("GetMatchController",flags);
var resolve=typeof(AIController).GetMethod("ResolveAISlotKey",flags);
var host=new UnityEngine.GameObject("SlotCacheFixture");host.SetActive(false);
void Check(bool value,string reason){if(!value)throw new System.Exception(reason);}
try{
    var ai=host.AddComponent<AIController>();var match=host.AddComponent<MatchController>();
    typeof(AIController).GetField("matchController",instanceFlags).SetValue(ai,match);
    var players=(System.Collections.IList)typeof(MatchController).GetField("players",instanceFlags).GetValue(match);
    var teamField=players[0].GetType().GetField("teamId",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
    var teams=new[]{TeamId.Blue,TeamId.Blue,TeamId.Yellow,TeamId.Red};
    for(int i=0;i<teams.Length;i++){var p=players[i];teamField.SetValue(p,teams[i]);players[i]=p;}
    typeof(MatchController).GetField("activePlayerListIndex",instanceFlags).SetValue(match,1);
    instance.SetValue(null,ai);cache.SetValue(null,UnityEngine.Object.FindAnyObjectByType<MatchController>());resolved.SetValue(null,true);
    Check(object.ReferenceEquals(get.Invoke(null,null),match),"Instancia nao venceu cache global.");
    Check((int)resolve.Invoke(null,new object[]{TeamId.Blue})==1,"Slot ativo da cor repetida mudou.");
    Check((int)resolve.Invoke(null,new object[]{TeamId.Red})==3,"Slot unico nao ativo mudou.");
    typeof(MatchController).GetField("activePlayerListIndex",instanceFlags).SetValue(match,2);
    Check((int)resolve.Invoke(null,new object[]{TeamId.Blue})==-1,"Cor ambigua sem ativo nao retornou -1.");
    instance.SetValue(null,null);cache.SetValue(null,match);resolved.SetValue(null,true);
    for(int i=0;i<1000;i++)Check(object.ReferenceEquals(get.Invoke(null,null),match),"Fallback procurou cena apesar de cache preenchido.");
    cache.SetValue(null,null);resolved.SetValue(null,true);
    Check((int)resolve.Invoke(null,new object[]{TeamId.Blue})==-1&&get.Invoke(null,null)==null,"Ausencia cacheada refez busca ou mudou retorno.");
    cache.SetValue(null,match);resolved.SetValue(null,true);
    typeof(AIController).GetMethod("HandleMatchControllerSceneLoaded",flags).Invoke(null,new object[]{UnityEngine.SceneManagement.SceneManager.GetActiveScene(),UnityEngine.SceneManagement.LoadSceneMode.Single});
    Check(cache.GetValue(null)==null&&!(bool)resolved.GetValue(null),"Gancho de sceneLoaded nao invalidou cache.");
    get.Invoke(null,null);Check((bool)resolved.GetValue(null),"Fallback nao preencheu cache apos invalidacao.");
    cache.SetValue(null,match);resolved.SetValue(null,true);
    typeof(AIController).GetMethod("OnDestroy",instanceFlags).Invoke(ai,null);
    Check(cache.GetValue(null)==null&&!(bool)resolved.GetValue(null),"OnDestroy da IA nao invalidou cache.");
    return "PASS: Match da instancia precede cache; ativo de cor repetida, unico e ambiguo preservados; mil leituras reutilizam fallback; ausencia cacheada nao busca; sceneLoaded e OnDestroy invalidam. Fora do Play.";
}finally{
    UnityEngine.Object.DestroyImmediate(host);instance.SetValue(null,oldInstance);cache.SetValue(null,oldCache);resolved.SetValue(null,oldResolved);
}
