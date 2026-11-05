if(UnityEngine.Application.isPlaying)throw new System.Exception("Fora do Play apenas.");
var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var path="Assets/Scenes/Autoria/Fixture.unity";
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
bool opened=!scene.IsValid() || !scene.isLoaded;
if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
try {
    var lines=new System.Collections.Generic.List<string>();
    int checkedManagers=0;
    foreach(var root in scene.GetRootGameObjects())
        foreach(var m in root.GetComponentsInChildren<BeachManager>(true)) {
            int fullCount=m.Islands.Count;
            var region=new UnityEngine.RectInt(-3,10,18,17);
            var clipped=m.GetIslandsInRegion(region);
            if(clipped.Count!=4)throw new System.Exception("Recorte Q3 deve conter 4 ilhas.");
            int tiles=0;
            foreach(var island in clipped){
                tiles+=island.CellCount;
                foreach(var cell in island.Cells)if(!region.Contains(new UnityEngine.Vector2Int(cell.x,cell.y)))throw new System.Exception("Tile vazou do recorte.");
                lines.Add(island.DisplayName+" tiles="+island.CellCount+" sectors="+string.Join(",",island.Sectors));
            }
            if(tiles!=207 || m.Islands.Count!=fullCount)throw new System.Exception("Recorte alterou catalogo global ou contagem.");
            var empty=m.GetIslandsInRegion(new UnityEngine.RectInt(9999,9999,2,2));
            if(empty.Count!=0)throw new System.Exception("Troca de recorte reteve cache anterior.");
            if(m.GetIslandsInRegion(region).Count!=4)throw new System.Exception("Retorno ao recorte incorreto.");
            checkedManagers++;
        }
    if(checkedManagers==0)throw new System.Exception("Fixture sem BeachManager.");
    return "PASS: recorte Q3, 4 ilhas / 207 tiles, nomes/setores locais, troca de recorte e catalogo global preservado.\n"+string.Join("\n",lines);
}finally {
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
    if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
}
