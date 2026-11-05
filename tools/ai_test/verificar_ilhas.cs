if (UnityEngine.Application.isPlaying) throw new System.Exception("Execute fora do Play.");
var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
var terrain = new System.Collections.Generic.List<TerrainTypeData>();
var tiles = new System.Collections.Generic.List<UnityEngine.Tilemaps.Tile>();
TerrainDatabase database = null;
var buildings = new System.Collections.Generic.List<ConstructionManager>();
try {
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
    var root = new UnityEngine.GameObject("IslandTestGrid", typeof(UnityEngine.Grid));
    root.GetComponent<UnityEngine.Grid>().cellLayout = UnityEngine.GridLayout.CellLayout.Hexagon;
    var host = new UnityEngine.GameObject("IslandTestMap", typeof(UnityEngine.Tilemaps.Tilemap));
    host.transform.SetParent(root.transform);
    var map = host.GetComponent<UnityEngine.Tilemaps.Tilemap>();
    var managerHost = new UnityEngine.GameObject("IslandTestManager");
    managerHost.SetActive(false);
    var manager = managerHost.AddComponent<BeachManager>();
    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    typeof(BeachManager).GetField("boardTilemap",flags).SetValue(manager,map);
    var land = new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,TerrainTypeData>();
    foreach(var id in new[]{"plains","forest","mountain","beach"}) {
        var t = UnityEngine.ScriptableObject.CreateInstance<TerrainTypeData>(); t.id=id; terrain.Add(t);
    }
    // Follow actual six-neighbor hex adjacency rather than assuming Cartesian neighbors.
    var neighbors = new System.Collections.Generic.List<UnityEngine.Vector3Int>();
    var cell = UnityEngine.Vector3Int.zero;
    for(int i=0;i<4;i++) {
        land.Add(cell,terrain[i]);
        UnitMovementPathRules.GetImmediateHexNeighbors(map,cell,neighbors);
        neighbors.Sort((a,b)=>a.x!=b.x?b.x.CompareTo(a.x):b.y.CompareTo(a.y));
        cell=neighbors[0];
    }
    land.Add(new UnityEngine.Vector3Int(30,30,0),terrain[0]);
    var build=typeof(BeachManager).GetMethod("BuildIslandComponents",flags);
    build.Invoke(manager,new object[]{land,"test-map"});
    var islands=(System.Collections.Generic.List<BeachManager.IslandInfo>)typeof(BeachManager).GetField("islands",flags).GetValue(manager);
    if(islands.Count!=2 || islands[0].CellCount!=4 || islands[0].TerrainCounts.Count!=4 || islands[1].CellCount!=1)
        throw new System.Exception("Conectividade/contagem dos quatro terrenos incorreta.");
    string stableId=islands[0].IslandId;
    var reversed=new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,TerrainTypeData>();
    var keys=new System.Collections.Generic.List<UnityEngine.Vector3Int>(land.Keys);keys.Reverse();
    foreach(var key in keys)reversed.Add(key,land[key]);
    build.Invoke(manager,new object[]{reversed,"test-map"});
    if(islands[0].IslandId!=stableId)throw new System.Exception("ID mudou com ordem de insercao.");
    var indexed=(System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,BeachManager.IslandInfo>)typeof(BeachManager).GetField("islandByCell",flags).GetValue(manager);
    if(indexed.ContainsKey(new UnityEngine.Vector3Int(15,15,0)))throw new System.Exception("Agua/lacuna virou terra.");
    var sea=UnityEngine.ScriptableObject.CreateInstance<TerrainTypeData>();sea.id="sea";terrain.Add(sea);
    database=UnityEngine.ScriptableObject.CreateInstance<TerrainDatabase>();
    typeof(TerrainDatabase).GetField("terrains",flags).SetValue(database,terrain);
    foreach(var t in terrain){var tile=UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();tiles.Add(tile);t.paletteTile=tile;}
    foreach(var entry in land)map.SetTile(entry.Key,entry.Value.paletteTile);
    map.SetTile(new UnityEngine.Vector3Int(15,15,0),sea.paletteTile);
    manager.ConfigureSources(map,database);
    var resolved=manager.Islands;
    if(resolved.Count!=2 || manager.TryGetIslandAtCell(new UnityEngine.Vector3Int(15,15,0),out _))
        throw new System.Exception("Catalogo integrou agua ou perdeu ilha sem praia.");
    foreach(var island in resolved)
        if(island.BeachIds.Count!=0 || string.IsNullOrEmpty(island.DisplayName))throw new System.Exception("Ilha sem praia ficou sem identidade.");
    foreach(var position in new[]{UnityEngine.Vector3Int.zero,new UnityEngine.Vector3Int(30,30,0)}){
        var buildingHost=new UnityEngine.GameObject("SyntheticSector");buildingHost.SetActive(false);
        var building=buildingHost.AddComponent<ConstructionManager>();
        typeof(ConstructionManager).GetField("sector",flags).SetValue(building,ConstructionSector.Alpha);
        typeof(ConstructionManager).GetField("currentCellPosition",flags).SetValue(building,position);
        buildings.Add(building);ConstructionManager.AllActive.Add(building);
    }
    resolved=manager.Islands;
    if(resolved[0].DisplayName!="Ilha Alpha" || resolved[0].DisplayName==resolved[1].DisplayName || manager.GetIslandsForSector(ConstructionSector.Alpha).Count!=2)
        throw new System.Exception("Nome de setor, Alpha=0, homonimos ou setor entre ilhas incorreto.");
    build.Invoke(manager,new object[]{new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int,TerrainTypeData>(),"test-map"});
    if(islands.Count!=0 || indexed.Count!=0)throw new System.Exception("Reconstrucao manteve ilhas antigas.");
    return "PASS: seis vizinhos hex, quatro terrenos juntos, ilha sem praia, separacao por agua/lacuna, contagens, IDs deterministas e limpeza do cache. Cena de teste descartada.";
} finally {
    foreach(var building in buildings)ConstructionManager.AllActive.Remove(building);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    foreach(var t in terrain)UnityEngine.Object.DestroyImmediate(t);
    foreach(var t in tiles)UnityEngine.Object.DestroyImmediate(t);
    if(database!=null)UnityEngine.Object.DestroyImmediate(database);
}
