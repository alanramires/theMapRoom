using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionSpawner))]
public class ConstructionSpawnerEditor : Editor
{
    private SerializedProperty constructionDatabaseProp;
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

    private void OnEnable()
    {
        constructionDatabaseProp = serializedObject.FindProperty("constructionDatabase");
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

        serializedObject.ApplyModifiedProperties();

        ConstructionSpawner spawner = (ConstructionSpawner)target;
        if (spawnManualClicked)
            spawner.SpawnManual();
        if (spawnMapClicked)
            spawner.SpawnMapList(true);
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
            EditorGUILayout.EndVertical();
        }
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
