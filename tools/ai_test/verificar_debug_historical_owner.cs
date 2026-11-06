if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var parse=typeof(PanelDebugController).GetMethod("TryParseSetHistoricalOwnerCommand",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
foreach(var sample in new[]{"SET FIRST OWNER 1","SET ORIGINAL OWNER 2","SET FIRST OWNER -1","SET ORIGINAL OWNER -1"}){
    object[] values={sample,null,null};
    if(!(bool)parse.Invoke(null,values) || (bool)values[1]!=sample.StartsWith("SET FIRST"))throw new System.Exception("Parse incorreto: "+sample);
}
foreach(var sample in new[]{"SET FIRST OWNER","SET FIRST OWNER A","SET ORIGINAL OWNER 1 2","SET OWNER 1"}){
    if((bool)parse.Invoke(null,new object[]{sample,null,null}))throw new System.Exception("Comando invalido aceito: "+sample);
}
var go=new UnityEngine.GameObject("HistoricalOwnerCommandTest");go.SetActive(false);
try{
    var construction=go.AddComponent<ConstructionManager>();
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    typeof(ConstructionManager).GetField("slotIndex",flags).SetValue(construction,5);
    typeof(ConstructionManager).GetField("originalOwnerSlotIndex",flags).SetValue(construction,0);
    typeof(ConstructionManager).GetField("originalOwnerInitialized",flags).SetValue(construction,true);
    var rule=construction.SellingRule;
    construction.DebugSetHistoricalOwner(true,2);
    if(construction.FirstOwnerSlotIndex!=2 || !construction.HasFirstOwner || construction.OriginalOwnerSlotIndex!=0 || construction.SlotIndex!=5 || construction.SellingRule!=rule)
        throw new System.Exception("First owner alterou outro estado.");
    construction.DebugSetHistoricalOwner(false,3);
    if(construction.OriginalOwnerSlotIndex!=3 || !construction.HasOriginalOwner || construction.FirstOwnerSlotIndex!=2 || construction.SlotIndex!=5 || construction.SellingRule!=rule)
        throw new System.Exception("Original owner alterou outro estado.");
    construction.DebugSetHistoricalOwner(true,-1);
    if(construction.HasFirstOwner || construction.FirstOwnerSlotIndex!=-1)throw new System.Exception("First owner nao foi limpo.");
    construction.DebugSetHistoricalOwner(false,-1);
    if(!construction.HasOriginalOwner || construction.OriginalOwnerSlotIndex!=-1)throw new System.Exception("Dono original neutro nao foi preservado.");
    return "PASS: ambos comandos, rejeicao de sintaxe invalida, donos independentes, regra de venda/dono atual preservados e semantica -1.";
}finally{UnityEngine.Object.DestroyImmediate(go);}
