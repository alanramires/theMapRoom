using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionSpawner))]
public class ConstructionSpawnerEditor : Editor
{
    private SerializedProperty constructionDatabaseProp;
    private SerializedProperty constructionFieldDatabaseProp;
    private SerializedProperty matchControllerProp;
    private SerializedProperty currentIdProp;
    private SerializedProperty constructionPrefabProp;
    private SerializedProperty boardTilemapProp;
    private SerializedProperty spawnParentProp;

    private SerializedProperty manualTeamIdProp;
    private SerializedProperty manualConstructionIdProp;
    private SerializedProperty manualCellPositionProp;

    private SerializedProperty spawnMapListOnStartProp;
    private SerializedProperty mapSpawnEntriesProp;
    private SerializedProperty spawnFieldDatabaseOnStartProp;

    private void OnEnable()
    {
        constructionDatabaseProp = serializedObject.FindProperty("constructionDatabase");
        constructionFieldDatabaseProp = serializedObject.FindProperty("constructionFieldDatabase");
        matchControllerProp = serializedObject.FindProperty("matchController");
        currentIdProp = serializedObject.FindProperty("currentId");
        constructionPrefabProp = serializedObject.FindProperty("constructionPrefab");
        boardTilemapProp = serializedObject.FindProperty("boardTilemap");
        spawnParentProp = serializedObject.FindProperty("spawnParent");

        manualTeamIdProp = serializedObject.FindProperty("manualTeamId");
        manualConstructionIdProp = serializedObject.FindProperty("manualConstructionId");
        manualCellPositionProp = serializedObject.FindProperty("manualCellPosition");

        spawnMapListOnStartProp = serializedObject.FindProperty("spawnMapListOnStart");
        mapSpawnEntriesProp = serializedObject.FindProperty("mapSpawnEntries");
        spawnFieldDatabaseOnStartProp = serializedObject.FindProperty("spawnFieldDatabaseOnStart");
    }

    public override void OnInspectorGUI()
    {
        if (constructionDatabaseProp == null || currentIdProp == null || constructionPrefabProp == null || boardTilemapProp == null || manualConstructionIdProp == null || mapSpawnEntriesProp == null)
        {
            EditorGUILayout.HelpBox("ConstructionSpawnerEditor: propriedades nao encontradas. Usando inspector padrao.", MessageType.Warning);
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();

        EditorGUILayout.LabelField("Data", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(constructionDatabaseProp);
        EditorGUILayout.PropertyField(constructionFieldDatabaseProp, new GUIContent("Construction Field Database"));
        EditorGUILayout.PropertyField(matchControllerProp, new GUIContent("Match Controller"));
        EditorGUILayout.PropertyField(currentIdProp, new GUIContent("Current ID"));
        EditorGUILayout.PropertyField(constructionPrefabProp);
        EditorGUILayout.PropertyField(boardTilemapProp);
        EditorGUILayout.PropertyField(spawnParentProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Manual Spawn", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(manualTeamIdProp, new GUIContent("Team ID"));
        DrawManualConstructionIdPopup();
        EditorGUILayout.PropertyField(manualCellPositionProp, new GUIContent("Cell Position"));
        bool spawnManualClicked = GUILayout.Button("Spawn");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Map Spawn", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(spawnMapListOnStartProp, new GUIContent("Spawn Map List On Start"));
        DrawMapSpawnEntries();
        bool spawnMapClicked = GUILayout.Button("Spawn Map List Now");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Field Database Spawn", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(spawnFieldDatabaseOnStartProp, new GUIContent("Spawn Field Database On Start"));
        bool spawnFieldDbClicked = GUILayout.Button("Spawn Field Database Now");

        serializedObject.ApplyModifiedProperties();

        ConstructionSpawner spawner = (ConstructionSpawner)target;
        if (spawnManualClicked)
            spawner.SpawnManual();
        if (spawnMapClicked)
            spawner.SpawnMapList(true);
        if (spawnFieldDbClicked)
            spawner.SpawnFieldDatabase(true);
    }

    private void DrawManualConstructionIdPopup()
    {
        DrawConstructionIdPopupForProperty(manualConstructionIdProp, "Construction ID");
    }

    private void DrawMapSpawnEntries()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
        if (GUILayout.Button("Add Entry", GUILayout.MaxWidth(100)))
            mapSpawnEntriesProp.arraySize += 1;
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < mapSpawnEntriesProp.arraySize; i++)
        {
            SerializedProperty entry = mapSpawnEntriesProp.GetArrayElementAtIndex(i);
            SerializedProperty teamIdProp = entry.FindPropertyRelative("teamId");
            SerializedProperty constructionIdProp = entry.FindPropertyRelative("constructionId");
            SerializedProperty cellProp = entry.FindPropertyRelative("cellPosition");
            SerializedProperty useSiteOverridesProp = entry.FindPropertyRelative("useSiteOverrides");
            SerializedProperty siteRuntimeProp = entry.FindPropertyRelative("siteRuntime");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Entry {i}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.MaxWidth(70)))
            {
                mapSpawnEntriesProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(teamIdProp, new GUIContent("Team ID"));
            DrawConstructionIdPopupForProperty(constructionIdProp, "Construction ID");
            EditorGUILayout.PropertyField(cellProp, new GUIContent("Cell Position"));
            EditorGUILayout.PropertyField(useSiteOverridesProp, new GUIContent("Use Construction Configuration Override"));
            if (useSiteOverridesProp != null && useSiteOverridesProp.boolValue && siteRuntimeProp != null)
                DrawConstructionConfigurationExpanded(siteRuntimeProp, "Construction Configuration");
            EditorGUILayout.EndVertical();
        }
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
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("canProduceUnits"), "Can Produce Units");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("offeredUnits"), "Offered Units");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("canProvideSupplies"), "Can Provide Supplies");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("offeredSupplies"), "Offered Supplies");
        DrawIfExists(siteRuntimeProp.FindPropertyRelative("offeredServices"), "Offered Services");

        EditorGUI.indentLevel--;
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren: true);
    }

    private void DrawConstructionIdPopupForProperty(SerializedProperty constructionIdProp, string label)
    {
        ConstructionDatabase db = constructionDatabaseProp.objectReferenceValue as ConstructionDatabase;
        if (db == null || db.Constructions == null || db.Constructions.Count == 0)
        {
            EditorGUILayout.PropertyField(constructionIdProp, new GUIContent(label));
            return;
        }

        int count = db.Constructions.Count;
        string[] labels = new string[count];
        int currentIndex = -1;

        for (int i = 0; i < count; i++)
        {
            ConstructionData construction = db.Constructions[i];
            if (construction == null)
            {
                labels[i] = "<null>";
                continue;
            }

            labels[i] = string.IsNullOrWhiteSpace(construction.displayName)
                ? construction.id
                : $"{construction.id} ({construction.displayName})";

            if (construction.id == constructionIdProp.stringValue)
                currentIndex = i;
        }

        int newIndex = EditorGUILayout.Popup(label, Mathf.Max(0, currentIndex), labels);
        if (newIndex >= 0 && newIndex < count && db.Constructions[newIndex] != null)
            constructionIdProp.stringValue = db.Constructions[newIndex].id;
    }
}
