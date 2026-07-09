using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class UnitPainterWindow : EditorWindow
{
    private UnitSpawner unitSpawner;
    private UnitDatabase unitDatabase;
    private int selectedSlotIndex = 0;
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
        DrawSlotSelector();
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
        Event e = Event.current;
        // Diagnostico: clique com o paint armado nunca deve falhar em silencio.
        bool isClick = e != null && e.type == EventType.MouseDown && (e.button == 0 || e.button == 1);

        if (!isPainting || unitSpawner == null || unitDatabase == null)
        {
            if (isClick && isPainting)
                Debug.LogWarning($"[UnitPainter] Clique ignorado: unitSpawner={(unitSpawner != null ? unitSpawner.name : "<null>")} unitDatabase={(unitDatabase != null ? unitDatabase.name : "<null>")}.");
            return;
        }

        Tilemap tilemap = GetSpawnerBoardTilemap();
        if (tilemap == null)
        {
            if (isClick)
                Debug.LogWarning("[UnitPainter] Clique ignorado: board tilemap nao resolvido (procuro um Tilemap chamado 'TileMap' na cena ativa).");
            return;
        }
        if (!TryGetSelectedUnit(out UnitData selectedUnit) || selectedUnit == null)
        {
            if (isClick)
                Debug.LogWarning($"[UnitPainter] Clique ignorado: unidade selecionada invalida (indice {selectedUnitIndex} do database '{unitDatabase.name}' e nula ou sem id).");
            return;
        }

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
            Debug.LogWarning($"[UnitPainter] Hex invalido (sem tile) em ({cell.x},{cell.y}) no tilemap '{tilemap.name}'.");
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
        else if (ResolveUnitAtCell(tilemap, cell) != null)
        {
            ShowNotification(new GUIContent("Hex ja ocupado por unidade"));
            e.Use();
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Paint Unit");

        TeamId resolvedTeam = ResolveTeamFromSlot(selectedSlotIndex);
        GameObject spawned = unitSpawner.SpawnAtCell(selectedUnit.id, resolvedTeam, cell);
        if (spawned != null)
        {
            UnitManager manager = spawned.GetComponent<UnitManager>();
            if (manager != null)
            {
                SerializedObject so = new SerializedObject(manager);
                SerializedProperty slotProp = so.FindProperty("slotIndex");
                if (slotProp != null)
                {
                    slotProp.intValue = selectedSlotIndex;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            Undo.RegisterCreatedObjectUndo(spawned, "Paint Unit");
            EditorSceneManager.MarkSceneDirty(spawned.scene);
        }
        else
        {
            Debug.LogWarning($"[UnitPainter] Spawn falhou para '{selectedUnit.id}' em ({cell.x},{cell.y}) — veja o warning do UnitSpawner acima para o motivo.");
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
        UnitManager existing = ResolveUnitAtCell(tilemap, cell);
        if (existing == null)
            return;

        var scene = existing.gameObject.scene;
        Undo.DestroyObjectImmediate(existing.gameObject);
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private static UnitManager ResolveUnitAtCell(Tilemap tilemap, Vector3Int cell)
    {
        cell.z = 0;

        UnitManager existing = UnitOccupancyRules.GetUnitAtCell(tilemap, cell);
        if (existing != null)
            return existing;

        UnitManager[] sceneUnits = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneUnits.Length; i++)
        {
            UnitManager unit = sceneUnits[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            if (unit.BoardTilemap != tilemap)
                continue;
            if (unit.gameObject.scene != tilemap.gameObject.scene)
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return unit;
        }

        return null;
    }

    private Tilemap GetSpawnerBoardTilemap()
    {
        if (unitSpawner == null)
            return null;

        SerializedObject so = new SerializedObject(unitSpawner);
        SerializedProperty tilemapProp = so.FindProperty("boardTilemap");
        Tilemap current = tilemapProp != null ? tilemapProp.objectReferenceValue as Tilemap : null;
        Tilemap resolved = ResolvePreferredBoardTilemap(current);
        if (resolved != null && tilemapProp != null && current != resolved)
        {
            tilemapProp.objectReferenceValue = resolved;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unitSpawner);
        }

        return resolved;
    }

    private void DrawSlotSelector()
    {
        MatchController mc = Object.FindAnyObjectByType<MatchController>();
        int slotCount = mc != null ? mc.SlotCount : 0;

        string[] options = new string[slotCount + 1];
        options[0] = "Neutral (-1)";
        for (int i = 0; i < slotCount; i++)
        {
            TeamId t = mc != null ? mc.GetTeamIdForSlot(i) : TeamId.Neutral;
            options[i + 1] = $"Slot {i} — {t}";
        }

        int currentPopupIndex = selectedSlotIndex < 0 ? 0 : Mathf.Clamp(selectedSlotIndex + 1, 0, options.Length - 1);
        int newPopupIndex = EditorGUILayout.Popup("Slot", currentPopupIndex, options);
        selectedSlotIndex = newPopupIndex == 0 ? -1 : newPopupIndex - 1;
    }

    private TeamId ResolveTeamFromSlot(int slot)
    {
        if (slot < 0)
            return TeamId.Neutral;

        MatchController mc = Object.FindAnyObjectByType<MatchController>();
        return mc != null ? mc.GetTeamIdForSlot(slot) : TeamId.Neutral;
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

    private static Tilemap ResolvePreferredBoardTilemap(Tilemap current)
    {
        if (IsTileMapByName(current))
            return current;

        Tilemap byName = FindSceneTileMapByName();
        if (byName != null)
            return byName;

        return current;
    }

    private static bool IsTileMapByName(Tilemap tilemap)
    {
        return tilemap != null &&
               string.Equals(tilemap.name, "TileMap", System.StringComparison.OrdinalIgnoreCase);
    }

    private static Tilemap FindSceneTileMapByName()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (IsTileMapByName(map))
                return map;
        }

        return null;
    }
}
