using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;


[InitializeOnLoad]
[CustomEditor(typeof(BeachManager))]
public sealed class BeachManagerEditor : Editor
{
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

        var beaches = manager.Beaches;
        int cells = 0;
        for (int i = 0; i < beaches.Count; i++)
            cells += beaches[i] != null ? beaches[i].CellCount : 0;
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
        Handles.zTest = CompareFunction.Always;
        try
        {
            for (int i = 0; i < managers.Length; i++)
                DrawBeachStrips(managers[i]);
        }
        finally
        {
            Handles.zTest = previousZTest;
        }
    }

    private static void DrawBeachStrips(BeachManager manager)
    {
        if (manager == null || !manager.PaintBeachStripsInScene)
            return;
        Tilemap tilemap = manager.BoardTilemap;
        if (tilemap == null)
            return;

        var beaches = manager.Beaches;
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
                Vector3 world =
                    tilemap.GetCellCenterWorld(beach.Cells[c]);
                Handles.DrawSolidDisc(
                    world,
                    Vector3.back,
                    cellDotRadius);
            }

            Vector3 repWorld =
                tilemap.GetCellCenterWorld(beach.BeachRepCell);
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
