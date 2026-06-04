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

    private void OnEnable()
    {
        unitDatabaseProp = serializedObject.FindProperty("unitDatabase");
        matchControllerProp = serializedObject.FindProperty("matchController");
        currentIdProp = serializedObject.FindProperty("currentId");
        unitPrefabProp = serializedObject.FindProperty("unitPrefab");
        boardTilemapProp = serializedObject.FindProperty("boardTilemap");
        spawnWithHasActedFalseProp = serializedObject.FindProperty("spawnWithHasActedFalse");
        spawnParentProp = serializedObject.FindProperty("spawnParent");
    }

    public override void OnInspectorGUI()
    {
        if (unitDatabaseProp == null || currentIdProp == null || unitPrefabProp == null || boardTilemapProp == null)
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

        serializedObject.ApplyModifiedProperties();
    }
}
