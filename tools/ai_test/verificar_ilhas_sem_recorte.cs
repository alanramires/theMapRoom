if(UnityEngine.Application.isPlaying)throw new System.Exception("Execute fora do Play.");
var saved=InvasionAxisMap.AuthoringQuadrante;
var manager=UnityEngine.Object.FindAnyObjectByType<BeachManager>();
if(manager==null)throw new System.Exception("BeachManager ausente.");
var world=UnityEditor.AssetDatabase.LoadAssetAtPath<MundoData>("Assets/DB/Campanha/Mundo Fixture.asset");
QuadranteData selected=null;
foreach(var q in world.AllQuadrantes())if(q.quadranteId=="A_IA_Q3")selected=q;
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
var visible=typeof(BeachManagerEditor).GetMethod("VisibleIslands",flags);
try {
    InvasionAxisMap.AuthoringQuadrante=null;
    var all=(System.Collections.Generic.IReadOnlyList<BeachManager.IslandInfo>)visible.Invoke(null,new object[]{manager});
    int count=0;
    var rect=new UnityEngine.RectInt(selected.originX,selected.originY,selected.width,selected.height);
    foreach(var island in all){
        bool fits=false;
        foreach(var q in world.AllQuadrantes()){
            var box=new UnityEngine.RectInt(q.originX,q.originY,q.width,q.height);
            bool inside=true;foreach(var cell in island.Cells)if(!box.Contains(new UnityEngine.Vector2Int(cell.x,cell.y))){inside=false;break;}
            if(inside){fits=true;break;}
        }
        if(!fits)throw new System.Exception("Ilha ultrapassou o quadrante sem recorte ativo.");
        if(rect.Contains(new UnityEngine.Vector2Int(island.RepresentativeCell.x,island.RepresentativeCell.y)))count++;
    }
    if(count!=4)throw new System.Exception("Peixe Pequeno sem selecao deve manter 4 ilhas; obteve "+count);
    var before=manager.GetIslandsInRegion(rect);
    manager.GetIslandsInRegion(new UnityEngine.RectInt(9999,9999,2,2));
    if(!object.ReferenceEquals(before,manager.GetIslandsInRegion(rect)))throw new System.Exception("Cache por quadrante nao foi preservado.");
    InvasionAxisMap.AuthoringQuadrante=selected;
    var clipped=(System.Collections.Generic.IReadOnlyList<BeachManager.IslandInfo>)visible.Invoke(null,new object[]{manager});
    if(clipped.Count!=4)throw new System.Exception("Selecao Q3 nao isolou 4 ilhas.");
    return "PASS: sem selecao, cada quadrante separado; Q3 com 4 ilhas; com selecao, somente Q3; cache independente por retangulo.";
} finally {InvasionAxisMap.AuthoringQuadrante=saved;UnityEditor.SceneView.RepaintAll();}
