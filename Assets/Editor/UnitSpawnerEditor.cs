using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitSpawner))]
public class UnitSpawnerEditor : Editor
{
    private SerializedProperty unitDatabaseProp;
    private SerializedProperty matchControllerProp;
    private SerializedProperty currentIdProp;
    private SerializedProperty unitPrefabProp;
    private SerializedProperty boardTilemapProp;
    private SerializedProperty spawnWithHasActedFalseProp;
    private SerializedProperty spawnParentProp;

    private SerializedProperty manualTeamIdProp;
    private SerializedProperty manualUnitIdProp;
    private SerializedProperty manualCellPositionProp;

    private SerializedProperty spawnMapListOnStartProp;
    private SerializedProperty mapSpawnEntriesProp;

    private void OnEnable()
    {
        unitDatabaseProp = serializedObject.FindProperty("unitDatabase");
        matchControllerProp = serializedObject.FindProperty("matchController");
        currentIdProp = serializedObject.FindProperty("currentId");
        unitPrefabProp = serializedObject.FindProperty("unitPrefab");
        boardTilemapProp = serializedObject.FindProperty("boardTilemap");
        spawnWithHasActedFalseProp = serializedObject.FindProperty("spawnWithHasActedFalse");
        spawnParentProp = serializedObject.FindProperty("spawnParent");

        manualTeamIdProp = serializedObject.FindProperty("manualTeamId");
        manualUnitIdProp = serializedObject.FindProperty("manualUnitId");
        manualCellPositionProp = serializedObject.FindProperty("manualCellPosition");

        spawnMapListOnStartProp = serializedObject.FindProperty("spawnMapListOnStart");
        mapSpawnEntriesProp = serializedObject.FindProperty("mapSpawnEntries");
    }

    public override void OnInspectorGUI()
    {
        if (unitDatabaseProp == null || currentIdProp == null || unitPrefabProp == null || boardTilemapProp == null || manualUnitIdProp == null || mapSpawnEntriesProp == null)
        {
            EditorGUILayout.HelpBox("UnitSpawnerEditor: propriedades nao encontradas. Usando inspector padrao.", MessageType.Warning);
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();

        EditorGUILayout.LabelField("Data", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(unitDatabaseProp);
        EditorGUILayout.PropertyField(matchControllerProp, new GUIContent("Match Controller"));
        EditorGUILayout.PropertyField(currentIdProp, new GUIContent("Current ID"));
        EditorGUILayout.PropertyField(unitPrefabProp);
        EditorGUILayout.PropertyField(boardTilemapProp);
        EditorGUILayout.PropertyField(spawnWithHasActedFalseProp, new GUIContent("Spawn With Has Acted False"));
        EditorGUILayout.PropertyField(spawnParentProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Manual Spawn", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(manualTeamIdProp, new GUIContent("Team ID"));
        DrawManualUnitIdPopup();
        EditorGUILayout.PropertyField(manualCellPositionProp, new GUIContent("Cell Position"));
        bool spawnManualClicked = GUILayout.Button("Spawn");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Map Spawn", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(spawnMapListOnStartProp, new GUIContent("Spawn Map List On Start"));
        DrawMapSpawnEntries();
        bool spawnMapClicked = GUILayout.Button("Spawn Map List Now");

        serializedObject.ApplyModifiedProperties();

        UnitSpawner spawner = (UnitSpawner)target;
        if (spawnManualClicked)
            spawner.SpawnManual();
        if (spawnMapClicked)
            spawner.SpawnMapList(true);
    }

    private void DrawManualUnitIdPopup()
    {
        DrawUnitIdPopupForProperty(manualUnitIdProp, "Unit ID");
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
            SerializedProperty unitIdProp = entry.FindPropertyRelative("unitId");
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
            DrawUnitIdPopupForProperty(unitIdProp, "Unit ID");
            EditorGUILayout.PropertyField(cellProp, new GUIContent("Cell Position"));
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawUnitIdPopupForProperty(SerializedProperty unitIdProp, string label)
    {
        UnitDatabase db = unitDatabaseProp.objectReferenceValue as UnitDatabase;
        if (db == null || db.Units == null || db.Units.Count == 0)
        {
            EditorGUILayout.PropertyField(unitIdProp, new GUIContent(label));
            return;
        }

        int count = db.Units.Count;
        string[] labels = new string[count];
        int currentIndex = -1;

        for (int i = 0; i < count; i++)
        {
            UnitData unit = db.Units[i];
            if (unit == null)
            {
                labels[i] = "<null>";
                continue;
            }

            labels[i] = string.IsNullOrWhiteSpace(unit.displayName) ? unit.id : $"{unit.id} ({unit.displayName})";
            if (unit.id == unitIdProp.stringValue)
                currentIndex = i;
        }

        int newIndex = EditorGUILayout.Popup(label, Mathf.Max(0, currentIndex), labels);
        if (newIndex >= 0 && newIndex < count && db.Units[newIndex] != null)
            unitIdProp.stringValue = db.Units[newIndex].id;
    }
}
