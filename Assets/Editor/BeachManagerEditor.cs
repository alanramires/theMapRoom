using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;


[InitializeOnLoad]
[CustomEditor(typeof(BeachManager))]
public sealed class BeachManagerEditor : Editor
{
    private static BeachManager focusedIslandManager;
    private static string focusedIslandId;
    private static string IslandPaintKey(BeachManager manager) => "BeachManager.PaintIslands." + manager.gameObject.scene.path;
    private static Color IslandColor(int index) => Color.HSVToRGB(Mathf.Repeat(index * 0.618034f, 1f), 0.7f, 1f);
    private static RectInt? AuthoringRegion()
    {
        var q = Application.isPlaying ? null : InvasionAxisMap.AuthoringQuadrante;
        return q == null ? (RectInt?)null : new RectInt(q.originX, q.originY, Mathf.Max(1, q.width), Mathf.Max(1, q.height));
    }
    private static IReadOnlyList<BeachManager.IslandInfo> VisibleIslands(BeachManager manager)
    {
        var region = AuthoringRegion();
        return region.HasValue ? manager.GetIslandsInRegion(region.Value) : manager.Islands;
    }
    private static bool VisibleCell(Vector3Int cell)
    {
        var region = AuthoringRegion();
        return !region.HasValue || region.Value.Contains(new Vector2Int(cell.x, cell.y));
    }
    private static List<BeachManager.BeachInfo> VisibleBeaches(BeachManager manager)
    {
        var result = new List<BeachManager.BeachInfo>();
        foreach (var beach in manager.Beaches)
            foreach (var cell in beach.Cells)
                if (VisibleCell(cell)) { result.Add(beach); break; }
        return result;
    }

    private SerializedProperty boardTilemapProp;
    private SerializedProperty terrainDatabaseProp;
    private SerializedProperty beachTerrainTypeProp;
    private SerializedProperty maximumConnectedStripLengthProp;
    private SerializedProperty beachLogProp;
    private SerializedProperty paintBeachStripsInSceneProp;
    private SerializedProperty beachLabelFontSizeProp;

    static BeachManagerEditor()
    {
        SceneView.duringSceneGui -= DrawAllBeachStrips;
        SceneView.duringSceneGui += DrawAllBeachStrips;
    }

    private void OnEnable()
    {
        boardTilemapProp = serializedObject.FindProperty("boardTilemap");
        terrainDatabaseProp = serializedObject.FindProperty("terrainDatabase");
        beachTerrainTypeProp =
            serializedObject.FindProperty("beachTerrainType");
        maximumConnectedStripLengthProp =
            serializedObject.FindProperty("maximumConnectedStripLength");
        beachLogProp = serializedObject.FindProperty("beachLog");
        paintBeachStripsInSceneProp =
            serializedObject.FindProperty("paintBeachStripsInScene");
        beachLabelFontSizeProp =
            serializedObject.FindProperty("beachLabelFontSize");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(
            boardTilemapProp,
            new GUIContent("Board Tilemap"));
        EditorGUILayout.PropertyField(
            terrainDatabaseProp,
            new GUIContent("Terrain Database"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("islandLandTerrains"),
            new GUIContent("Terrenos das ilhas"), true);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Tamanho da Praia Militar",
            EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            beachTerrainTypeProp,
            new GUIContent(
                "Terrain Type da Praia",
                "Arraste o TerrainTypeData que representa praia. O nome/ID " +
                "do asset nao e interpretado pelo BeachManager."));
        maximumConnectedStripLengthProp.intValue = EditorGUILayout.IntSlider(
            new GUIContent(
                "Extensao maxima da faixa",
                "Distancia percorrida pela cadeia natural antes de iniciar " +
                "outro nome. O padrao 6 equivale ao Operational do Soldado."),
            maximumConnectedStripLengthProp.intValue,
            1,
            24);

        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(beachLogProp, new GUIContent("Log"));
        beachLabelFontSizeProp.intValue = EditorGUILayout.IntSlider(
            new GUIContent("Tamanho da fonte"),
            beachLabelFontSizeProp.intValue,
            8,
            48);

        serializedObject.ApplyModifiedProperties();

        BeachManager manager = (BeachManager)target;
        EditorGUILayout.Space(6f);
        bool painting = paintBeachStripsInSceneProp.boolValue;
        if (GUILayout.Button(
                painting
                    ? "Desligar pintura das faixas de praia"
                    : "Pintar faixas de praia por cores"))
        {
            serializedObject.Update();
            paintBeachStripsInSceneProp.boolValue = !painting;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Rebuild Military Beaches"))
        {
            manager.RebuildMilitaryBeaches();
            EditorUtility.SetDirty(manager);
            SceneView.RepaintAll();
        }

        if (manager.BeachTerrainType == null)
        {
            EditorGUILayout.HelpBox(
                "Defina o TerrainTypeData que representa praia.",
                MessageType.Warning);
        }
        else if (manager.BeachTerrainType.paletteTile == null)
        {
            EditorGUILayout.HelpBox(
                "O TerrainTypeData selecionado nao possui Palette Tile.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Mapa de trabalho", EditorStyles.boldLabel);
        if (GUILayout.Button("Abrir Map Helper")) MapHelperWindow.OpenWindow();
        var authoring = Application.isPlaying ? null : InvasionAxisMap.AuthoringQuadrante;
        EditorGUILayout.HelpBox(authoring != null
            ? $"Recorte do Map Helper / Axis Editor: {authoring.displayName} ({authoring.quadranteId})."
            : "Mapa inteiro. Na autoria, selecione um quadrante no Map Helper para recortar ilhas e praias.", MessageType.Info);
        bool paintIslands = SessionState.GetBool(IslandPaintKey(manager), false);
        bool nextPaint = EditorGUILayout.ToggleLeft("Colorir ilhas no Scene View", paintIslands);
        if (nextPaint != paintIslands)
        {
            SessionState.SetBool(IslandPaintKey(manager), nextPaint);
            SceneView.RepaintAll();
        }
        var islands = VisibleIslands(manager);
        EditorGUILayout.LabelField($"Ilhas terrestres: {islands.Count}", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Informativo: conectividade geografica, nao caminho de tanque. Sem praias, o nome vem do primeiro setor em ordem numerica. Lista vazia de terrenos usa planicie, floresta, montanha e praia.", MessageType.Info);
        for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
        {
            var island = islands[islandIndex];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(16f, 18f, GUILayout.Width(16f)), IslandColor(islandIndex));
            EditorGUILayout.LabelField($"I{islandIndex + 1} - {island.DisplayName}", EditorStyles.boldLabel);
            if (GUILayout.Button("Focar", GUILayout.Width(55f))) FocusIsland(manager, island);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.SelectableLabel(island.IslandId, GUILayout.Height(18f));
            EditorGUILayout.LabelField("Tiles / praias", $"{island.CellCount} / {island.BeachIds.Count}");
            EditorGUILayout.LabelField("Setores", string.Join(", ", island.Sectors), EditorStyles.wordWrappedLabel);
            foreach (var count in island.TerrainCounts)
                EditorGUILayout.LabelField(count.terrain.displayName, count.count.ToString());
            EditorGUILayout.EndVertical();
        }

        var beaches = VisibleBeaches(manager);
        int cells = 0;
        for (int i = 0; i < beaches.Count; i++)
            foreach (var cell in beaches[i].Cells) if (VisibleCell(cell)) cells++;
        EditorGUILayout.HelpBox(
            $"{beaches.Count} praia(s) militar(es), {cells} hex(es) de praia. " +
            "Componentes descontinuos recebem identidades diferentes.",
            MessageType.Info);

        for (int i = 0; i < beaches.Count; i++)
        {
            BeachManager.BeachInfo beach = beaches[i];
            if (beach == null)
                continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                beach.DisplayName,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID", beach.BeachId);
            EditorGUILayout.LabelField("Inicio", beach.StartCell.ToString());
            EditorGUILayout.LabelField("Fim", beach.EndCell.ToString());
            EditorGUILayout.LabelField(
                "Beach Rep Cell",
                beach.BeachRepCell.ToString());
            EditorGUILayout.LabelField("Hexes", beach.CellCount.ToString());
            EditorGUILayout.LabelField(
                "Extensao real",
                beach.ChainExtent.ToString());
            EditorGUILayout.EndVertical();
        }
    }

    private static void DrawAllBeachStrips(SceneView sceneView)
    {
        BeachManager[] managers =
            Object.FindObjectsByType<BeachManager>(
                FindObjectsInactive.Include);
        CompareFunction previousZTest = Handles.zTest;
        Color previousColor = Handles.color;
        Handles.zTest = CompareFunction.Always;
        try
        {
            for (int i = 0; i < managers.Length; i++)
            {
                DrawIslands(managers[i]);
                DrawBeachStrips(managers[i]);
            }
        }
        finally
        {
            Handles.zTest = previousZTest;
            Handles.color = previousColor;
        }
    }

    private static void DrawBeachStrips(BeachManager manager)
    {
        if (manager == null || !manager.PaintBeachStripsInScene)
            return;
        Tilemap tilemap = manager.BoardTilemap;
        if (tilemap == null)
            return;

        var beaches = VisibleBeaches(manager);
        float cellDotRadius = Mathf.Max(0.10f, tilemap.cellSize.x * 0.24f);
        float repDotRadius = Mathf.Max(0.14f, tilemap.cellSize.x * 0.32f);
        for (int i = 0; i < beaches.Count; i++)
        {
            BeachManager.BeachInfo beach = beaches[i];
            if (beach == null)
                continue;
            Color color = Color.HSVToRGB(
                Mathf.Repeat(i * 0.173f, 1f),
                0.72f,
                1f);
            color.a = 0.82f;
            Handles.color = color;
            for (int c = 0; c < beach.Cells.Count; c++)
            {
                if (!VisibleCell(beach.Cells[c])) continue;
                Vector3 world =
                    tilemap.GetCellCenterWorld(beach.Cells[c]);
                Handles.DrawSolidDisc(
                    world,
                    Vector3.back,
                    cellDotRadius);
            }

            Vector3Int repCell = beach.BeachRepCell;
            if (!VisibleCell(repCell))
                foreach (var cell in beach.Cells) if (VisibleCell(cell)) { repCell = cell; break; }
            Vector3 repWorld = tilemap.GetCellCenterWorld(repCell);
            color.a = 0.95f;
            Handles.color = color;
            Handles.DrawSolidDisc(
                repWorld,
                Vector3.back,
                repDotRadius);
            Handles.DrawWireDisc(
                repWorld,
                Vector3.back,
                repDotRadius * 1.25f);
            float luminance = color.r * 0.2126f
                + color.g * 0.7152f
                + color.b * 0.0722f;
            Color textColor = luminance >= 0.55f
                ? Color.black
                : Color.white;
            Handles.Label(
                repWorld,
                BuildInitialLabel(beach.DisplayName),
                BeachLabelStyle(
                    textColor,
                    manager.BeachLabelFontSize));
        }
    }

    private static Bounds IslandBounds(Tilemap map, BeachManager.IslandInfo island)
    {
        var bounds = new Bounds(map.GetCellCenterWorld(island.Cells[0]), Vector3.zero);
        foreach (var cell in island.Cells) bounds.Encapsulate(map.GetCellCenterWorld(cell));
        bounds.Expand(Mathf.Max(1f, map.cellSize.magnitude));
        return bounds;
    }

    private static void FocusIsland(BeachManager manager, BeachManager.IslandInfo island)
    {
        focusedIslandManager = manager; focusedIslandId = island.IslandId;
        SessionState.SetBool(IslandPaintKey(manager), true);
        if (manager.BoardTilemap != null && SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.Frame(IslandBounds(manager.BoardTilemap, island), false);
        SceneView.RepaintAll();
    }

    private static void DrawIslands(BeachManager manager)
    {
        if (manager == null || !SessionState.GetBool(IslandPaintKey(manager), false) || manager.BoardTilemap == null) return;
        var map = manager.BoardTilemap;
        var islands = VisibleIslands(manager);
        float radius = Mathf.Max(0.1f, map.cellSize.x * 0.27f);
        for (int i = 0; i < islands.Count; i++)
        {
            var island = islands[i];
            var color = IslandColor(i);
            bool selected = focusedIslandManager == manager && focusedIslandId == island.IslandId;
            color.a = selected ? 0.65f : 0.3f;
            Handles.color = color;
            Vector3 center = IslandBounds(map, island).center;
            Vector3 label = map.GetCellCenterWorld(island.Cells[0]);
            float nearest = float.MaxValue;
            foreach (var cell in island.Cells)
            {
                Vector3 world = map.GetCellCenterWorld(cell);
                Handles.DrawSolidDisc(world, Vector3.back, radius);
                float distance = (world - center).sqrMagnitude;
                if (distance < nearest) { nearest = distance; label = world; }
            }
            Handles.color = IslandColor(i);
            Handles.DrawWireDisc(label, Vector3.back, radius * 1.7f);
            Handles.Label(label, $"I{i + 1} {island.DisplayName}", BeachLabelStyle(Color.white, manager.BeachLabelFontSize));
        }
    }

    private static GUIStyle BeachLabelStyle(Color color, int fontSize) =>
        new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            normal = { textColor = color }
        };

    private static string BuildInitialLabel(string beachName)
    {
        if (string.IsNullOrWhiteSpace(beachName))
            return "?";
        return char.ToUpperInvariant(beachName.Trim()[0]).ToString();
    }
}
