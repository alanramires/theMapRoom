using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(ConstructionFieldDatabase))]
public class ConstructionFieldDatabaseEditor : Editor
{
    private SerializedProperty entriesProp;
    private bool showAllTeams = true;
    private TeamId focusedTeam = TeamId.Neutral;
    private float focusZoomMin = 5f;
    private bool neutralFoldout = true;
    private bool greenFoldout = true;
    private bool redFoldout = true;
    private bool blueFoldout = true;
    private bool yellowFoldout = true;

    private static readonly TeamId[] TeamOrder =
    {
        TeamId.Neutral,
        TeamId.Green,
        TeamId.Red,
        TeamId.Blue,
        TeamId.Yellow
    };

    private void OnEnable()
    {
        entriesProp = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (entriesProp == null)
        {
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawToolbar();
        DrawGroupingControls();

        bool removed = false;
        if (showAllTeams)
        {
            for (int i = 0; i < TeamOrder.Length; i++)
            {
                if (DrawTeamSection(TeamOrder[i]))
                {
                    removed = true;
                    break;
                }
            }
        }
        else
        {
            removed = DrawTeamSection(focusedTeam);
        }

        serializedObject.ApplyModifiedProperties();
        if (removed)
            GUIUtility.ExitGUI();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
        if (GUILayout.Button("Add Entry", GUILayout.MaxWidth(100f)))
            entriesProp.arraySize += 1;
        if (GUILayout.Button("Detectar Construcoes Ja em Campo", GUILayout.MaxWidth(270f)))
            DetectConstructionsAlreadyInField();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawGroupingControls()
    {
        showAllTeams = EditorGUILayout.ToggleLeft("Mostrar todos os times (inclui neutro)", showAllTeams);
        focusZoomMin = EditorGUILayout.Slider("Focus Zoom Min", focusZoomMin, 1.5f, 20f);
        if (!showAllTeams)
            focusedTeam = (TeamId)EditorGUILayout.EnumPopup("Time para trabalhar", focusedTeam);
    }

    private bool DrawTeamSection(TeamId team)
    {
        int count = CountEntriesForTeam(team);
        SetTeamFoldout(team, EditorGUILayout.Foldout(GetTeamFoldout(team), $"{TeamUtils.GetName(team)} ({count})", true));
        if (!GetTeamFoldout(team))
            return false;

        EditorGUI.indentLevel++;
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty teamProp = entry.FindPropertyRelative("initialTeamId");
            if (teamProp == null || teamProp.intValue != (int)team)
                continue;

            if (DrawEntryCard(entry, i))
            {
                entriesProp.DeleteArrayElementAtIndex(i);
                EditorGUI.indentLevel--;
                return true;
            }
        }

        EditorGUI.indentLevel--;
        return false;
    }

    private bool DrawEntryCard(SerializedProperty entry, int index)
    {
        SerializedProperty idProp = entry.FindPropertyRelative("id");
        SerializedProperty constructionProp = entry.FindPropertyRelative("construction");
        SerializedProperty teamProp = entry.FindPropertyRelative("initialTeamId");
        SerializedProperty cellProp = entry.FindPropertyRelative("cellPosition");
        SerializedProperty initialCaptureProp = entry.FindPropertyRelative("initialCapturePoints");
        SerializedProperty useOverrideProp = entry.FindPropertyRelative("useConstructionConfigurationOverride");
        SerializedProperty configProp = entry.FindPropertyRelative("constructionConfiguration");
        string resolvedId = ResolveAndApplyEntryId(idProp, constructionProp, teamProp, cellProp);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Entry {index + 1}", EditorStyles.boldLabel);
        if (GUILayout.Button("Ir", GUILayout.MaxWidth(50f)))
            FocusEntryInScene(entry);
        if (GUILayout.Button("Remove", GUILayout.MaxWidth(70f)))
        {
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.TextField("ID", resolvedId);
        EditorGUILayout.PropertyField(constructionProp, new GUIContent("Construction"));
        EditorGUILayout.PropertyField(teamProp, new GUIContent("Initial Team"));
        EditorGUILayout.PropertyField(cellProp, new GUIContent("Cell Position"));
        EditorGUILayout.PropertyField(initialCaptureProp, new GUIContent("Initial Capture Points (-1 uses max)"));
        EditorGUILayout.PropertyField(useOverrideProp, new GUIContent("Use Construction Configuration Override"));

        if (useOverrideProp != null && useOverrideProp.boolValue && configProp != null)
            DrawConstructionConfiguration(configProp, "Construction Configuration (Override)");

        EditorGUILayout.EndVertical();
        return false;
    }

    private int CountEntriesForTeam(TeamId team)
    {
        int count = 0;
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty teamProp = entry.FindPropertyRelative("initialTeamId");
            if (teamProp != null && teamProp.intValue == (int)team)
                count++;
        }

        return count;
    }

    private bool GetTeamFoldout(TeamId team)
    {
        switch (team)
        {
            case TeamId.Neutral: return neutralFoldout;
            case TeamId.Green: return greenFoldout;
            case TeamId.Red: return redFoldout;
            case TeamId.Blue: return blueFoldout;
            case TeamId.Yellow: return yellowFoldout;
            default: return true;
        }
    }

    private void SetTeamFoldout(TeamId team, bool value)
    {
        switch (team)
        {
            case TeamId.Neutral: neutralFoldout = value; break;
            case TeamId.Green: greenFoldout = value; break;
            case TeamId.Red: redFoldout = value; break;
            case TeamId.Blue: blueFoldout = value; break;
            case TeamId.Yellow: yellowFoldout = value; break;
        }
    }

    private void DetectConstructionsAlreadyInField()
    {
        ConstructionFieldDatabase database = (ConstructionFieldDatabase)target;
        if (database == null)
            return;

        Undo.RecordObject(database, "Detect constructions already in field");
        serializedObject.Update();

        int added = 0;
        int updated = 0;
        ConstructionManager[] managers = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            ConstructionManager manager = managers[i];
            if (manager == null || !manager.gameObject.activeInHierarchy)
                continue;

            if (!manager.TryResolveConstructionData(out ConstructionData constructionData) || constructionData == null)
                continue;

            Vector3Int cell = manager.CurrentCellPosition;
            cell.z = 0;
            int entryIndex = FindEntryIndexByCell(cell);
            bool isNew = entryIndex < 0;
            if (isNew)
            {
                entryIndex = entriesProp.arraySize;
                entriesProp.arraySize += 1;
                added++;
            }
            else
            {
                updated++;
            }

            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(entryIndex);
            if (entry == null)
                continue;

            SerializedProperty idProp = entry.FindPropertyRelative("id");
            SerializedProperty constructionProp = entry.FindPropertyRelative("construction");
            SerializedProperty teamProp = entry.FindPropertyRelative("initialTeamId");
            SerializedProperty cellProp = entry.FindPropertyRelative("cellPosition");
            SerializedProperty captureProp = entry.FindPropertyRelative("initialCapturePoints");
            SerializedProperty useOverrideProp = entry.FindPropertyRelative("useConstructionConfigurationOverride");
            SerializedProperty configProp = entry.FindPropertyRelative("constructionConfiguration");

            if (idProp != null)
                idProp.stringValue = BuildInstanceEntryId(manager, constructionData);
            if (constructionProp != null)
                constructionProp.objectReferenceValue = constructionData;
            if (teamProp != null)
                teamProp.intValue = (int)manager.TeamId;
            if (cellProp != null)
                cellProp.vector3IntValue = cell;
            if (captureProp != null)
                captureProp.intValue = manager.CurrentCapturePoints;

            ConstructionSiteRuntime snapshot = manager.GetSiteRuntimeSnapshot();
            ConstructionSiteRuntime defaults = constructionData.constructionConfiguration != null
                ? constructionData.constructionConfiguration
                : new ConstructionSiteRuntime();

            bool useOverride = !AreSiteRuntimesEquivalent(snapshot, defaults);
            if (useOverrideProp != null)
                useOverrideProp.boolValue = useOverride;
            if (configProp != null && useOverride)
                CopySiteRuntimeToProperty(snapshot, configProp);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        Debug.Log($"[ConstructionFieldDatabaseEditor] Detectado em cena. Added={added} Updated={updated} TotalScene={managers.Length}.");
    }

    private static string ResolveAndApplyEntryId(
        SerializedProperty idProp,
        SerializedProperty constructionProp,
        SerializedProperty teamProp,
        SerializedProperty cellProp)
    {
        string current = idProp != null ? idProp.stringValue : string.Empty;
        int instanceId = ExtractInstanceId(current);
        TeamId team = teamProp != null ? (TeamId)teamProp.intValue : TeamId.Neutral;

        ConstructionManager manager = null;
        if (cellProp != null)
        {
            Vector3Int cell = cellProp.vector3IntValue;
            cell.z = 0;
            manager = FindConstructionManagerAtCell(cell);
        }

        if (manager != null)
            instanceId = Mathf.Max(0, manager.InstanceId);

        string baseName = ResolveBaseName(constructionProp, manager);
        string resolved = BuildInstanceEntryId(baseName, manager != null ? manager.TeamId : team, Mathf.Max(0, instanceId));

        if (idProp != null && idProp.stringValue != resolved)
            idProp.stringValue = resolved;

        return resolved;
    }

    private int FindEntryIndexByCell(Vector3Int cell)
    {
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty cellProp = entry.FindPropertyRelative("cellPosition");
            if (cellProp == null)
                continue;

            Vector3Int existing = cellProp.vector3IntValue;
            existing.z = 0;
            if (existing == cell)
                return i;
        }

        return -1;
    }

    private static bool AreSiteRuntimesEquivalent(ConstructionSiteRuntime a, ConstructionSiteRuntime b)
    {
        ConstructionSiteRuntime left = a != null ? a.Clone() : new ConstructionSiteRuntime();
        ConstructionSiteRuntime right = b != null ? b.Clone() : new ConstructionSiteRuntime();

        if (left.isPlayerHeadQuarter != right.isPlayerHeadQuarter) return false;
        if (left.isCapturable != right.isCapturable) return false;
        if (left.capturePointsMax != right.capturePointsMax) return false;
        if (left.capturedIncoming != right.capturedIncoming) return false;
        if (left.canProvideSupplies != right.canProvideSupplies) return false;
        if (!EqualsEnumList(left.canProduceAndSellUnits, right.canProduceAndSellUnits)) return false;
        if (!EqualsObjectList(left.offeredUnits, right.offeredUnits)) return false;
        if (!EqualsObjectList(left.offeredServices, right.offeredServices)) return false;
        if (!EqualsSupplyList(left.offeredSupplies, right.offeredSupplies)) return false;
        return true;
    }

    private static bool EqualsEnumList(List<ConstructionUnitMarketRule> a, List<ConstructionUnitMarketRule> b)
    {
        if (a == null || b == null)
            return (a == null || a.Count == 0) && (b == null || b.Count == 0);
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }

    private static bool EqualsObjectList<T>(List<T> a, List<T> b) where T : Object
    {
        if (a == null || b == null)
            return (a == null || a.Count == 0) && (b == null || b.Count == 0);
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }

    private static bool EqualsSupplyList(List<ConstructionSupplyOffer> a, List<ConstructionSupplyOffer> b)
    {
        if (a == null || b == null)
            return (a == null || a.Count == 0) && (b == null || b.Count == 0);
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            ConstructionSupplyOffer left = a[i];
            ConstructionSupplyOffer right = b[i];
            SupplyData leftSupply = left != null ? left.supply : null;
            SupplyData rightSupply = right != null ? right.supply : null;
            int leftQty = left != null ? Mathf.Max(0, left.quantity) : 0;
            int rightQty = right != null ? Mathf.Max(0, right.quantity) : 0;

            if (leftSupply != rightSupply || leftQty != rightQty)
                return false;
        }

        return true;
    }

    private static string ResolveBaseName(SerializedProperty constructionProp, ConstructionManager manager)
    {
        if (manager != null)
        {
            string managerName = !string.IsNullOrWhiteSpace(manager.ConstructionDisplayName)
                ? manager.ConstructionDisplayName
                : manager.ConstructionId;
            if (!string.IsNullOrWhiteSpace(managerName))
                return managerName;
        }

        if (constructionProp != null && constructionProp.objectReferenceValue is ConstructionData data)
        {
            string dataName = !string.IsNullOrWhiteSpace(data.displayName) ? data.displayName : data.id;
            if (!string.IsNullOrWhiteSpace(dataName))
                return dataName;
        }

        return "Construction";
    }

    private static string BuildInstanceEntryId(ConstructionManager manager, ConstructionData data)
    {
        string baseName = manager != null
            ? (!string.IsNullOrWhiteSpace(manager.ConstructionDisplayName) ? manager.ConstructionDisplayName : manager.ConstructionId)
            : (data != null ? (!string.IsNullOrWhiteSpace(data.displayName) ? data.displayName : data.id) : "Construction");

        TeamId team = manager != null ? manager.TeamId : TeamId.Neutral;
        int instanceId = manager != null ? Mathf.Max(0, manager.InstanceId) : 0;
        return BuildInstanceEntryId(baseName, team, instanceId);
    }

    private static string BuildInstanceEntryId(string baseName, TeamId team, int instanceId)
    {
        string sanitized = string.IsNullOrWhiteSpace(baseName) ? "Construction" : baseName.Replace(" ", string.Empty);
        return $"{sanitized}_T{(int)team}_C{Mathf.Max(0, instanceId)}";
    }

    private static int ExtractInstanceId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return 0;

        int idx = id.LastIndexOf("_C", System.StringComparison.OrdinalIgnoreCase);
        if (idx < 0 || idx + 2 >= id.Length)
            return 0;

        string suffix = id.Substring(idx + 2);
        return int.TryParse(suffix, out int parsed) ? Mathf.Max(0, parsed) : 0;
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
        CopyEnumList(destination.FindPropertyRelative("canProduceAndSellUnits"), copy.canProduceAndSellUnits);
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

    private static void CopyEnumList(SerializedProperty destination, List<ConstructionUnitMarketRule> values)
    {
        if (destination == null)
            return;

        destination.arraySize = values != null ? values.Count : 0;
        for (int i = 0; i < destination.arraySize; i++)
            destination.GetArrayElementAtIndex(i).enumValueIndex = (int)values[i];
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

    private static void DrawConstructionConfiguration(SerializedProperty configProp, string label)
    {
        if (configProp == null)
            return;

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(configProp.FindPropertyRelative("isPlayerHeadQuarter"), new GUIContent("Is Player Head Quarter"));
        EditorGUILayout.PropertyField(configProp.FindPropertyRelative("isCapturable"), new GUIContent("Is Capturable"));
        EditorGUILayout.PropertyField(configProp.FindPropertyRelative("capturePointsMax"), new GUIContent("Capture Points Max"));
        EditorGUILayout.PropertyField(configProp.FindPropertyRelative("capturedIncoming"), new GUIContent("Captured Incoming"));
        EditorGUILayout.PropertyField(configProp.FindPropertyRelative("canProduceAndSellUnits"), new GUIContent("Can Produce And Sell Units"));
        EditorGUILayout.PropertyField(configProp.FindPropertyRelative("offeredUnits"), new GUIContent("Offered Units"), true);
        EditorGUILayout.PropertyField(configProp.FindPropertyRelative("canProvideSupplies"), new GUIContent("Can Provide Supplies"));
        EditorGUILayout.PropertyField(configProp.FindPropertyRelative("offeredSupplies"), new GUIContent("Offered Supplies"), true);
        EditorGUILayout.PropertyField(configProp.FindPropertyRelative("offeredServices"), new GUIContent("Offered Services"), true);
        EditorGUI.indentLevel--;
    }

    private void FocusEntryInScene(SerializedProperty entry)
    {
        if (entry == null)
            return;

        SerializedProperty cellProp = entry.FindPropertyRelative("cellPosition");
        if (cellProp == null)
            return;

        Vector3Int cell = cellProp.vector3IntValue;
        cell.z = 0;

        Tilemap map = ResolveReferenceTilemap();
        Vector3 world = map != null
            ? map.GetCellCenterWorld(cell)
            : new Vector3(cell.x, cell.y, 0f);

        SceneView view = GetAvailableSceneView();
        if (view == null)
            return;

        Vector3 pivot = view.pivot;
        pivot.x = world.x;
        pivot.y = world.y;
        view.pivot = pivot;
        // Aplica o zoom configurado para foco direto da camera.
        view.size = Mathf.Max(1.5f, focusZoomMin);
        view.Repaint();
    }

    private static ConstructionManager FindConstructionManagerAtCell(Vector3Int cell)
    {
        ConstructionManager[] managers = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            ConstructionManager manager = managers[i];
            if (manager == null || !manager.gameObject.activeInHierarchy)
                continue;

            Vector3Int managerCell = manager.CurrentCellPosition;
            managerCell.z = 0;
            if (managerCell == cell)
                return manager;
        }

        return null;
    }

    private static Tilemap ResolveReferenceTilemap()
    {
        ConstructionSpawner[] spawners = Object.FindObjectsByType<ConstructionSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
        {
            ConstructionSpawner spawner = spawners[i];
            if (spawner == null)
                continue;

            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty tilemapProp = so.FindProperty("boardTilemap");
            if (tilemapProp != null && tilemapProp.objectReferenceValue is Tilemap board)
                return board;
        }

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null || map.layoutGrid == null)
                continue;

            if (map.layoutGrid.cellLayout == GridLayout.CellLayout.Hexagon)
                return map;
        }

        return null;
    }

    private static SceneView GetAvailableSceneView()
    {
        if (SceneView.lastActiveSceneView != null)
            return SceneView.lastActiveSceneView;

        if (SceneView.sceneViews != null && SceneView.sceneViews.Count > 0)
            return SceneView.sceneViews[0] as SceneView;

        return null;
    }
}
