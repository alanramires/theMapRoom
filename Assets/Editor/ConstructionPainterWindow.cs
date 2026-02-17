using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ConstructionPainterWindow : EditorWindow
{
    private ConstructionSpawner constructionSpawner;
    private ConstructionDatabase constructionDatabase;
    private TeamId selectedTeamId = TeamId.Green;
    private int selectedConstructionIndex;
    private bool isPainting;
    private bool replaceExisting = true;
    private Vector2 scroll;

    [MenuItem("Tools/Construction/Construction Painter")]
    public static void OpenWindow()
    {
        ConstructionPainterWindow window = GetWindow<ConstructionPainterWindow>("Construction Painter");
        window.minSize = new Vector2(360f, 240f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        TryAutoAssignReferences(force: false);
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnFocus()
    {
        TryAutoAssignReferences(force: false);
    }

    private void OnGUI()
    {
        TryAutoAssignReferences(force: false);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        constructionSpawner = (ConstructionSpawner)EditorGUILayout.ObjectField("Construction Spawner", constructionSpawner, typeof(ConstructionSpawner), true);
        constructionDatabase = (ConstructionDatabase)EditorGUILayout.ObjectField("Construction Database", constructionDatabase, typeof(ConstructionDatabase), false);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Auto Detect", GUILayout.Width(110f)))
            TryAutoAssignReferences(force: true);
        EditorGUILayout.EndHorizontal();

        if (constructionSpawner == null)
        {
            EditorGUILayout.HelpBox("Arraste um ConstructionSpawner da cena.", MessageType.Info);
            DrawTogglePaintButton(disabled: true);
            EditorGUILayout.EndScrollView();
            return;
        }

        Tilemap tilemap = GetSpawnerBoardTilemap();
        if (tilemap == null)
            EditorGUILayout.HelpBox("ConstructionSpawner precisa de Board Tilemap.", MessageType.Warning);

        if (constructionDatabase == null || constructionDatabase.Constructions == null || constructionDatabase.Constructions.Count == 0)
        {
            EditorGUILayout.HelpBox("Escolha um ConstructionDatabase com itens.", MessageType.Info);
            DrawTogglePaintButton(disabled: true);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space(6f);
        selectedTeamId = (TeamId)EditorGUILayout.EnumPopup("Team ID", selectedTeamId);
        DrawConstructionSelector();
        replaceExisting = EditorGUILayout.ToggleLeft("Replace Existing Construction On Cell", replaceExisting);

        EditorGUILayout.Space(8f);
        DrawTogglePaintButton(disabled: tilemap == null);
        if (isPainting)
            EditorGUILayout.HelpBox("Scene: Left Click pinta construcao. Right Click remove construcao no hex.", MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void DrawConstructionSelector()
    {
        int count = constructionDatabase.Constructions.Count;
        string[] labels = new string[count];
        selectedConstructionIndex = Mathf.Clamp(selectedConstructionIndex, 0, Mathf.Max(0, count - 1));

        for (int i = 0; i < count; i++)
        {
            ConstructionData data = constructionDatabase.Constructions[i];
            if (data == null)
            {
                labels[i] = "<null>";
                continue;
            }

            labels[i] = string.IsNullOrWhiteSpace(data.displayName)
                ? data.id
                : $"{data.id} ({data.displayName})";
        }

        selectedConstructionIndex = EditorGUILayout.Popup("Construction", selectedConstructionIndex, labels);
    }

    private void DrawTogglePaintButton(bool disabled)
    {
        using (new EditorGUI.DisabledScope(disabled))
        {
            string label = isPainting ? "Stop Painting" : "Start Painting";
            if (GUILayout.Button(label, GUILayout.Height(28f)))
                isPainting = !isPainting;
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPainting || constructionSpawner == null || constructionDatabase == null)
            return;

        Tilemap tilemap = GetSpawnerBoardTilemap();
        if (tilemap == null)
            return;
        if (!TryGetSelectedConstruction(out ConstructionData selectedConstruction) || selectedConstruction == null)
            return;

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type != EventType.MouseDown)
            return;
        if (e.button != 0 && e.button != 1)
            return;

        Vector3 world = GetMouseWorldOnTilemapPlane(e.mousePosition, tilemap);
        Vector3Int cell = tilemap.WorldToCell(world);
        cell.z = 0;
        if (!IsCellPaintedOnGrid(tilemap, cell))
        {
            ShowNotification(new GUIContent("Hex invalido (sem tile)"));
            e.Use();
            return;
        }

        if (e.button == 1)
        {
            RemoveConstructionAtCell(tilemap, cell);
            e.Use();
            return;
        }

        if (replaceExisting)
            RemoveConstructionAtCell(tilemap, cell);
        else if (ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell) != null)
        {
            ShowNotification(new GUIContent("Hex ja ocupado por construcao"));
            e.Use();
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Paint Construction");

        GameObject spawned = constructionSpawner.SpawnAtCell(selectedConstruction.id, selectedTeamId, cell);
        if (spawned != null)
        {
            Undo.RegisterCreatedObjectUndo(spawned, "Paint Construction");
            EditorSceneManager.MarkSceneDirty(spawned.scene);
        }
        else
        {
            ShowNotification(new GUIContent("Spawn falhou"));
        }

        Undo.CollapseUndoOperations(undoGroup);
        e.Use();
    }

    private bool TryGetSelectedConstruction(out ConstructionData construction)
    {
        construction = null;
        if (constructionDatabase == null || constructionDatabase.Constructions == null || constructionDatabase.Constructions.Count == 0)
            return false;

        selectedConstructionIndex = Mathf.Clamp(selectedConstructionIndex, 0, constructionDatabase.Constructions.Count - 1);
        construction = constructionDatabase.Constructions[selectedConstructionIndex];
        return construction != null && !string.IsNullOrWhiteSpace(construction.id);
    }

    private void RemoveConstructionAtCell(Tilemap tilemap, Vector3Int cell)
    {
        ConstructionManager existing = ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell);
        if (existing == null)
            return;

        var scene = existing.gameObject.scene;
        Undo.DestroyObjectImmediate(existing.gameObject);
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private Tilemap GetSpawnerBoardTilemap()
    {
        if (constructionSpawner == null)
            return null;

        SerializedObject so = new SerializedObject(constructionSpawner);
        SerializedProperty tilemapProp = so.FindProperty("boardTilemap");
        return tilemapProp != null ? tilemapProp.objectReferenceValue as Tilemap : null;
    }

    private void TryAutoAssignReferences(bool force)
    {
        if (force || constructionSpawner == null)
        {
            ConstructionSpawner[] spawners = Object.FindObjectsByType<ConstructionSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (spawners != null && spawners.Length > 0)
                constructionSpawner = spawners[0];
        }

        if (!force && constructionDatabase != null)
            return;

        if (constructionSpawner != null)
        {
            SerializedObject so = new SerializedObject(constructionSpawner);
            SerializedProperty dbProp = so.FindProperty("constructionDatabase");
            if (dbProp != null && dbProp.objectReferenceValue is ConstructionDatabase dbFromSpawner)
            {
                constructionDatabase = dbFromSpawner;
                return;
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:ConstructionDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ConstructionDatabase candidate = AssetDatabase.LoadAssetAtPath<ConstructionDatabase>(path);
            if (candidate == null)
                continue;

            constructionDatabase = candidate;
            return;
        }
    }

    private static Vector3 GetMouseWorldOnTilemapPlane(Vector2 mousePosition, Tilemap tilemap)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane tilemapPlane = new Plane(tilemap.transform.forward, tilemap.transform.position);
        if (tilemapPlane.Raycast(ray, out float enter) && enter >= 0f)
            return ray.GetPoint(enter);

        SceneView view = SceneView.currentDrawingSceneView;
        if (view != null && view.camera != null)
        {
            Camera cam = view.camera;
            Vector2 gui = mousePosition;
            Vector3 screen = new Vector3(gui.x, cam.pixelHeight - gui.y, Mathf.Abs(cam.transform.position.z - tilemap.transform.position.z));
            return cam.ScreenToWorldPoint(screen);
        }

        return tilemap.transform.position;
    }

    private static bool IsCellPaintedOnGrid(Tilemap referenceTilemap, Vector3Int cell)
    {
        if (referenceTilemap == null)
            return false;

        if (referenceTilemap.HasTile(cell))
            return true;

        GridLayout grid = referenceTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            if (map.HasTile(cell))
                return true;
        }

        return false;
    }
}
