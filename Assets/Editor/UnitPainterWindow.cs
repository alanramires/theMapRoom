using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class UnitPainterWindow : EditorWindow
{
    private UnitSpawner unitSpawner;
    private UnitDatabase unitDatabase;
    private TeamId selectedTeamId = TeamId.Green;
    private int selectedUnitIndex;
    private bool isPainting;
    private bool replaceExisting = true;
    private Vector2 scroll;

    [MenuItem("Tools/Units/Unit Painter")]
    public static void OpenWindow()
    {
        UnitPainterWindow window = GetWindow<UnitPainterWindow>("Unit Painter");
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
        unitSpawner = (UnitSpawner)EditorGUILayout.ObjectField("Unit Spawner", unitSpawner, typeof(UnitSpawner), true);
        unitDatabase = (UnitDatabase)EditorGUILayout.ObjectField("Unit Database", unitDatabase, typeof(UnitDatabase), false);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Auto Detect", GUILayout.Width(110f)))
            TryAutoAssignReferences(force: true);
        EditorGUILayout.EndHorizontal();

        if (unitSpawner == null)
        {
            EditorGUILayout.HelpBox("Arraste um UnitSpawner da cena.", MessageType.Info);
            DrawTogglePaintButton(disabled: true);
            EditorGUILayout.EndScrollView();
            return;
        }

        Tilemap tilemap = GetSpawnerBoardTilemap();
        if (tilemap == null)
            EditorGUILayout.HelpBox("UnitSpawner precisa de Board Tilemap.", MessageType.Warning);

        if (unitDatabase == null || unitDatabase.Units == null || unitDatabase.Units.Count == 0)
        {
            EditorGUILayout.HelpBox("Escolha um UnitDatabase com itens.", MessageType.Info);
            DrawTogglePaintButton(disabled: true);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space(6f);
        selectedTeamId = (TeamId)EditorGUILayout.EnumPopup("Team ID", selectedTeamId);
        DrawUnitSelector();
        replaceExisting = EditorGUILayout.ToggleLeft("Replace Existing Unit On Cell", replaceExisting);

        EditorGUILayout.Space(8f);
        DrawTogglePaintButton(disabled: tilemap == null);
        if (isPainting)
            EditorGUILayout.HelpBox("Scene: Left Click pinta unidade. Right Click remove unidade no hex.", MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void DrawUnitSelector()
    {
        int count = unitDatabase.Units.Count;
        string[] labels = new string[count];
        selectedUnitIndex = Mathf.Clamp(selectedUnitIndex, 0, Mathf.Max(0, count - 1));

        for (int i = 0; i < count; i++)
        {
            UnitData data = unitDatabase.Units[i];
            if (data == null)
            {
                labels[i] = "<null>";
                continue;
            }

            labels[i] = string.IsNullOrWhiteSpace(data.displayName)
                ? data.id
                : $"{data.id} ({data.displayName})";
        }

        selectedUnitIndex = EditorGUILayout.Popup("Unit", selectedUnitIndex, labels);
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
        if (!isPainting || unitSpawner == null || unitDatabase == null)
            return;

        Tilemap tilemap = GetSpawnerBoardTilemap();
        if (tilemap == null)
            return;
        if (!TryGetSelectedUnit(out UnitData selectedUnit) || selectedUnit == null)
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
            RemoveUnitAtCell(tilemap, cell);
            e.Use();
            return;
        }

        if (replaceExisting)
            RemoveUnitAtCell(tilemap, cell);
        else if (UnitOccupancyRules.GetUnitAtCell(tilemap, cell) != null)
        {
            ShowNotification(new GUIContent("Hex ja ocupado por unidade"));
            e.Use();
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Paint Unit");

        GameObject spawned = unitSpawner.SpawnAtCell(selectedUnit.id, selectedTeamId, cell);
        if (spawned != null)
        {
            Undo.RegisterCreatedObjectUndo(spawned, "Paint Unit");
            EditorSceneManager.MarkSceneDirty(spawned.scene);
        }
        else
        {
            ShowNotification(new GUIContent("Spawn falhou"));
        }

        Undo.CollapseUndoOperations(undoGroup);
        e.Use();
    }

    private bool TryGetSelectedUnit(out UnitData unit)
    {
        unit = null;
        if (unitDatabase == null || unitDatabase.Units == null || unitDatabase.Units.Count == 0)
            return false;

        selectedUnitIndex = Mathf.Clamp(selectedUnitIndex, 0, unitDatabase.Units.Count - 1);
        unit = unitDatabase.Units[selectedUnitIndex];
        return unit != null && !string.IsNullOrWhiteSpace(unit.id);
    }

    private void RemoveUnitAtCell(Tilemap tilemap, Vector3Int cell)
    {
        UnitManager existing = UnitOccupancyRules.GetUnitAtCell(tilemap, cell);
        if (existing == null)
            return;

        var scene = existing.gameObject.scene;
        Undo.DestroyObjectImmediate(existing.gameObject);
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private Tilemap GetSpawnerBoardTilemap()
    {
        if (unitSpawner == null)
            return null;

        SerializedObject so = new SerializedObject(unitSpawner);
        SerializedProperty tilemapProp = so.FindProperty("boardTilemap");
        return tilemapProp != null ? tilemapProp.objectReferenceValue as Tilemap : null;
    }

    private void TryAutoAssignReferences(bool force)
    {
        if (force || unitSpawner == null)
        {
            UnitSpawner[] spawners = Object.FindObjectsByType<UnitSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (spawners != null && spawners.Length > 0)
                unitSpawner = spawners[0];
        }

        if (!force && unitDatabase != null)
            return;

        if (unitSpawner != null)
        {
            SerializedObject so = new SerializedObject(unitSpawner);
            SerializedProperty dbProp = so.FindProperty("unitDatabase");
            if (dbProp != null && dbProp.objectReferenceValue is UnitDatabase dbFromSpawner)
            {
                unitDatabase = dbFromSpawner;
                return;
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:UnitDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnitDatabase candidate = AssetDatabase.LoadAssetAtPath<UnitDatabase>(path);
            if (candidate == null)
                continue;

            unitDatabase = candidate;
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
