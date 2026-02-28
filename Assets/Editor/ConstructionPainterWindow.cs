using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ConstructionPainterWindow : EditorWindow
{
    private ConstructionSpawner constructionSpawner;
    private ConstructionDatabase constructionDatabase;
    private MatchController matchController;
    private TeamId selectedTeamId = TeamId.Green;
    private int selectedConstructionIndex;
    private bool isPainting;
    private bool replaceExisting = true;
    [SerializeField] private bool persistToFieldDatabase = true;
    [SerializeField] private bool useSiteConfigurationOverride;
    [SerializeField] private int initialCapturePoints = -1;
    [SerializeField] private ConstructionSiteRuntime brushSiteRuntime = new ConstructionSiteRuntime();
    [SerializeField] private int lastSelectionInstanceId;
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
        EnsureBrushDefaults();
        SerializedObject windowSerialized = new SerializedObject(this);
        windowSerialized.Update();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        constructionSpawner = (ConstructionSpawner)EditorGUILayout.ObjectField("Construction Spawner", constructionSpawner, typeof(ConstructionSpawner), true);
        constructionDatabase = (ConstructionDatabase)EditorGUILayout.ObjectField("Construction Database", constructionDatabase, typeof(ConstructionDatabase), false);
        matchController = (MatchController)EditorGUILayout.ObjectField("Match Controller", matchController, typeof(MatchController), true);

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
        DrawTeamSelectorFromMatchController();
        DrawConstructionSelector();
        TryGetSelectedConstruction(out ConstructionData selectedConstruction);
        SyncBrushWithSelection(selectedConstruction);
        replaceExisting = EditorGUILayout.ToggleLeft("Replace Existing Construction On Cell", replaceExisting);
        persistToFieldDatabase = EditorGUILayout.ToggleLeft("Persist To Construction Database Field Entries", persistToFieldDatabase);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Instance Configuration", EditorStyles.boldLabel);
        useSiteConfigurationOverride = EditorGUILayout.ToggleLeft("Use Site Configuration Override", useSiteConfigurationOverride);
        initialCapturePoints = EditorGUILayout.IntField("Initial Capture Points (-1 uses max)", initialCapturePoints);
        if (initialCapturePoints < -1)
            initialCapturePoints = -1;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load Defaults From Construction"))
                LoadBrushFromSelectedConstruction(selectedConstruction);
            if (GUILayout.Button("Clear Production Preset"))
                ApplyNoProductionPreset();
        }

        if (useSiteConfigurationOverride)
        {
            SerializedProperty brushRuntimeProp = windowSerialized.FindProperty("brushSiteRuntime");
            DrawConstructionConfigurationExpanded(brushRuntimeProp, "Site Runtime Override");
        }

        EditorGUILayout.Space(8f);
        DrawTogglePaintButton(disabled: tilemap == null);
        if (isPainting)
            EditorGUILayout.HelpBox("Scene: Left Click pinta construcao. Right Click remove construcao no hex.", MessageType.None);

        EditorGUILayout.EndScrollView();
        windowSerialized.ApplyModifiedProperties();
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
            ApplySpawnOverrides(spawned);

            if (persistToFieldDatabase && constructionDatabase != null)
                UpsertFieldEntry(cell, selectedConstruction, spawned.GetComponent<ConstructionManager>());

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
        if (existing != null)
        {
            var scene = existing.gameObject.scene;
            Undo.DestroyObjectImmediate(existing.gameObject);
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        if (persistToFieldDatabase && constructionDatabase != null)
            RemoveFieldEntryAtCell(cell);
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

        if (!force && constructionDatabase != null && matchController != null)
            return;

        if (constructionSpawner != null)
        {
            SerializedObject so = new SerializedObject(constructionSpawner);
            SerializedProperty dbProp = so.FindProperty("constructionDatabase");
            SerializedProperty matchProp = so.FindProperty("matchController");
            if (dbProp != null && dbProp.objectReferenceValue is ConstructionDatabase dbFromSpawner)
                constructionDatabase = dbFromSpawner;
            if (matchProp != null && matchProp.objectReferenceValue is MatchController matchFromSpawner)
                matchController = matchFromSpawner;
        }

        if (constructionDatabase == null)
        {
            string[] dbGuids = AssetDatabase.FindAssets("t:ConstructionDatabase");
            for (int i = 0; i < dbGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(dbGuids[i]);
                ConstructionDatabase candidate = AssetDatabase.LoadAssetAtPath<ConstructionDatabase>(path);
                if (candidate == null)
                    continue;

                constructionDatabase = candidate;
                break;
            }
        }

        if (matchController == null)
            matchController = Object.FindAnyObjectByType<MatchController>();

    }

    private void EnsureBrushDefaults()
    {
        if (brushSiteRuntime == null)
            brushSiteRuntime = new ConstructionSiteRuntime();
        brushSiteRuntime.Sanitize();
    }

    private void SyncBrushWithSelection(ConstructionData selectedConstruction)
    {
        int selectedId = selectedConstruction != null ? selectedConstruction.GetInstanceID() : 0;
        if (selectedId == lastSelectionInstanceId)
            return;

        lastSelectionInstanceId = selectedId;
        LoadBrushFromSelectedConstruction(selectedConstruction);
    }

    private void LoadBrushFromSelectedConstruction(ConstructionData selectedConstruction)
    {
        if (selectedConstruction == null || selectedConstruction.constructionConfiguration == null)
        {
            brushSiteRuntime = new ConstructionSiteRuntime();
            brushSiteRuntime.Sanitize();
            return;
        }

        brushSiteRuntime = selectedConstruction.constructionConfiguration.Clone();
    }

    private void ApplyNoProductionPreset()
    {
        EnsureBrushDefaults();
        brushSiteRuntime.sellingRule = ConstructionUnitMarketRule.Disabled;
        brushSiteRuntime.offeredUnits = new List<UnitData>();
        brushSiteRuntime.Sanitize();
    }

    private void ApplySpawnOverrides(GameObject spawned)
    {
        if (spawned == null)
            return;

        ConstructionManager manager = spawned.GetComponent<ConstructionManager>();
        if (manager == null)
            return;

        Undo.RecordObject(manager, "Apply Construction Instance Overrides");
        if (useSiteConfigurationOverride && brushSiteRuntime != null)
            manager.ApplySiteRuntime(brushSiteRuntime);

        int capture = initialCapturePoints >= 0 ? initialCapturePoints : manager.CapturePointsMax;
        manager.SetCurrentCapturePoints(capture);
        EditorUtility.SetDirty(manager);
    }

    private void UpsertFieldEntry(Vector3Int cell, ConstructionData selectedConstruction, ConstructionManager spawnedManager)
    {
        if (constructionDatabase == null || selectedConstruction == null)
            return;

        Undo.RecordObject(constructionDatabase, "Paint Construction Field Entry");

        SerializedObject fieldDbSerialized = new SerializedObject(constructionDatabase);
        fieldDbSerialized.Update();

        SerializedProperty entriesProp = fieldDbSerialized.FindProperty("fieldEntries");
        if (entriesProp == null)
            return;

        int index = FindEntryIndexByCell(entriesProp, cell);
        if (index < 0)
        {
            index = entriesProp.arraySize;
            entriesProp.arraySize += 1;
        }

        SerializedProperty entry = entriesProp.GetArrayElementAtIndex(index);
        if (entry == null)
            return;

        SerializedProperty idProp = entry.FindPropertyRelative("id");
        SerializedProperty constructionProp = entry.FindPropertyRelative("construction");
        SerializedProperty teamProp = entry.FindPropertyRelative("initialTeamId");
        SerializedProperty cellProp = entry.FindPropertyRelative("cellPosition");
        SerializedProperty captureProp = entry.FindPropertyRelative("initialCapturePoints");
        SerializedProperty useOverrideProp = entry.FindPropertyRelative("useConstructionConfigurationOverride");
        SerializedProperty configProp = entry.FindPropertyRelative("constructionConfiguration");

        if (idProp != null)
            idProp.stringValue = BuildFieldEntryId(selectedConstruction, selectedTeamId, spawnedManager);

        if (constructionProp != null)
            constructionProp.objectReferenceValue = selectedConstruction;
        if (teamProp != null)
            teamProp.intValue = (int)selectedTeamId;
        if (cellProp != null)
            cellProp.vector3IntValue = new Vector3Int(cell.x, cell.y, 0);
        if (captureProp != null)
            captureProp.intValue = initialCapturePoints;
        if (useOverrideProp != null)
            useOverrideProp.boolValue = useSiteConfigurationOverride;
        if (configProp != null)
            CopySiteRuntimeToProperty(brushSiteRuntime, configProp);

        fieldDbSerialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(constructionDatabase);
    }

    private static string BuildFieldEntryId(ConstructionData selectedConstruction, TeamId teamId, ConstructionManager manager)
    {
        string baseName = manager != null
            ? (!string.IsNullOrWhiteSpace(manager.ConstructionDisplayName) ? manager.ConstructionDisplayName : manager.ConstructionId)
            : (selectedConstruction != null
                ? (!string.IsNullOrWhiteSpace(selectedConstruction.displayName) ? selectedConstruction.displayName : selectedConstruction.id)
                : "Construction");

        int instanceId = manager != null ? Mathf.Max(0, manager.InstanceId) : 0;
        string sanitized = string.IsNullOrWhiteSpace(baseName) ? "Construction" : baseName.Replace(" ", string.Empty);
        TeamId resolvedTeam = manager != null ? manager.TeamId : teamId;
        return $"{sanitized}_T{(int)resolvedTeam}_C{instanceId}";
    }

    private void RemoveFieldEntryAtCell(Vector3Int cell)
    {
        if (constructionDatabase == null)
            return;

        Undo.RecordObject(constructionDatabase, "Remove Construction Field Entry");

        SerializedObject fieldDbSerialized = new SerializedObject(constructionDatabase);
        fieldDbSerialized.Update();

        SerializedProperty entriesProp = fieldDbSerialized.FindProperty("fieldEntries");
        if (entriesProp == null)
            return;

        int index = FindEntryIndexByCell(entriesProp, cell);
        if (index < 0)
            return;

        entriesProp.DeleteArrayElementAtIndex(index);
        fieldDbSerialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(constructionDatabase);
    }

    private static int FindEntryIndexByCell(SerializedProperty entriesProp, Vector3Int cell)
    {
        if (entriesProp == null)
            return -1;

        Vector3Int fixedCell = new Vector3Int(cell.x, cell.y, 0);
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty cellProp = entry.FindPropertyRelative("cellPosition");
            if (cellProp == null)
                continue;

            Vector3Int entryCell = cellProp.vector3IntValue;
            entryCell.z = 0;
            if (entryCell == fixedCell)
                return i;
        }

        return -1;
    }

    private static void CopySiteRuntimeToProperty(ConstructionSiteRuntime source, SerializedProperty destination)
    {
        if (source == null || destination == null)
            return;

        ConstructionSiteRuntime copy = source.Clone();

        SetBool(destination, "isPlayerHeadQuarter", copy.isPlayerHeadQuarter);
        SetBool(destination, "isCapturable", copy.isCapturable);
        SetInt(destination, "capturePointsMax", copy.capturePointsMax);
        SetInt(destination, "capturedIncoming", copy.capturedIncoming);
        SetBool(destination, "canProvideSupplies", copy.canProvideSupplies);
        SetEnum(destination, "sellingRule", (int)copy.sellingRule);
        CopyObjectList(destination.FindPropertyRelative("offeredUnits"), copy.offeredUnits);
        CopyObjectList(destination.FindPropertyRelative("offeredServices"), copy.offeredServices);
        CopySupplyList(destination.FindPropertyRelative("offeredSupplies"), copy.offeredSupplies);
    }

    private static void SetBool(SerializedProperty parent, string name, bool value)
    {
        SerializedProperty prop = parent.FindPropertyRelative(name);
        if (prop != null)
            prop.boolValue = value;
    }

    private static void SetInt(SerializedProperty parent, string name, int value)
    {
        SerializedProperty prop = parent.FindPropertyRelative(name);
        if (prop != null)
            prop.intValue = value;
    }

    private static void SetEnum(SerializedProperty parent, string name, int enumValueIndex)
    {
        SerializedProperty prop = parent.FindPropertyRelative(name);
        if (prop != null)
            prop.enumValueIndex = enumValueIndex;
    }

    private static void CopyObjectList<T>(SerializedProperty destination, List<T> values) where T : Object
    {
        if (destination == null)
            return;

        destination.arraySize = values != null ? values.Count : 0;
        for (int i = 0; i < destination.arraySize; i++)
            destination.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void CopySupplyList(SerializedProperty destination, List<ConstructionSupplyOffer> values)
    {
        if (destination == null)
            return;

        destination.arraySize = values != null ? values.Count : 0;
        for (int i = 0; i < destination.arraySize; i++)
        {
            SerializedProperty item = destination.GetArrayElementAtIndex(i);
            if (item == null)
                continue;

            SerializedProperty supplyProp = item.FindPropertyRelative("supply");
            SerializedProperty quantityProp = item.FindPropertyRelative("quantity");
            ConstructionSupplyOffer offer = values[i];

            if (supplyProp != null)
                supplyProp.objectReferenceValue = offer != null ? offer.supply : null;
            if (quantityProp != null)
                quantityProp.intValue = offer != null ? Mathf.Max(0, offer.quantity) : 0;
        }

    }

    private void DrawTeamSelectorFromMatchController()
    {
        List<TeamId> allowed = BuildAllowedTeams();
        if (allowed == null || allowed.Count == 0)
        {
            selectedTeamId = (TeamId)EditorGUILayout.EnumPopup("Team ID", selectedTeamId);
            return;
        }

        int selectedIndex = 0;
        string[] labels = new string[allowed.Count];
        for (int i = 0; i < allowed.Count; i++)
        {
            TeamId team = allowed[i];
            labels[i] = team == TeamId.Neutral
                ? "Neutral (-1)"
                : $"{TeamUtils.GetName(team)} ({(int)team})";

            if (team == selectedTeamId)
                selectedIndex = i;
        }

        int newIndex = EditorGUILayout.Popup("Team ID", selectedIndex, labels);
        if (newIndex >= 0 && newIndex < allowed.Count)
            selectedTeamId = allowed[newIndex];

        if (matchController != null)
        {
            EditorGUILayout.HelpBox(
                "Times filtrados pelo MatchController.Players. Neutral permanece disponivel para pintar unidades/construcoes neutras.",
                MessageType.None);
        }
    }

    private List<TeamId> BuildAllowedTeams()
    {
        var result = new List<TeamId> { TeamId.Neutral };

        IReadOnlyList<TeamId> players = matchController != null ? matchController.Players : null;
        if (players == null || players.Count == 0)
        {
            if (matchController == null)
                return result;

            return result;
        }

        var seen = new HashSet<TeamId> { TeamId.Neutral };
        for (int i = 0; i < players.Count; i++)
        {
            TeamId team = players[i];
            if (team == TeamId.Neutral || seen.Contains(team))
                continue;

            seen.Add(team);
            result.Add(team);
        }

        return result;
    }

    private static void DrawConstructionConfigurationExpanded(SerializedProperty siteRuntimeProp, string label)
    {
        if (siteRuntimeProp == null)
            return;

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        DrawIfExists(siteRuntimeProp.FindPropertyRelative("isPlayerHeadQuarter"), "Is Player Head Quarter");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("isCapturable"), "Is Capturable");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("capturePointsMax"), "Capture Points Max");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("capturedIncoming"), "Captured Incoming");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("sellingRule"), "Selling Rules");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("offeredUnits"), "Offered Units");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("canProvideSupplies"), "Can Provide Supplies");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("offeredSupplies"), "Offered Supplies");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("offeredServices"), "Offered Services");

        EditorGUI.indentLevel--;
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
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
