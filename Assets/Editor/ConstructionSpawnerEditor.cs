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

    private void OnEnable()
    {
        constructionDatabaseProp = serializedObject.FindProperty("constructionDatabase");
        matchControllerProp = serializedObject.FindProperty("matchController");
        currentIdProp = serializedObject.FindProperty("currentId");
        constructionPrefabProp = serializedObject.FindProperty("constructionPrefab");
        boardTilemapProp = serializedObject.FindProperty("boardTilemap");
        spawnParentProp = serializedObject.FindProperty("spawnParent");
    }

    public override void OnInspectorGUI()
    {
        if (constructionDatabaseProp == null || currentIdProp == null || constructionPrefabProp == null || boardTilemapProp == null)
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spawn Template", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(constructionPrefabProp);
        EditorGUILayout.PropertyField(boardTilemapProp);
        EditorGUILayout.PropertyField(spawnParentProp);

        serializedObject.ApplyModifiedProperties();
    }
}
