using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionManager))]
public class ConstructionManagerEditor : Editor
{
    private SerializedProperty spriteRendererProp;
    private SerializedProperty constructionDatabaseProp;
    private SerializedProperty boardTilemapProp;
    private SerializedProperty snapToCellCenterProp;
    private SerializedProperty autoSnapWhenMovedInEditorProp;
    private SerializedProperty currentCellPositionProp;
    private SerializedProperty teamIdProp;
    private SerializedProperty constructionIdProp;
    private SerializedProperty instanceIdProp;
    private SerializedProperty currentPositionProp;
    private SerializedProperty constructionDisplayNameProp;
    private SerializedProperty currentHpProp;
    private SerializedProperty autoApplyOnStartProp;

    private void OnEnable()
    {
        spriteRendererProp = serializedObject.FindProperty("spriteRenderer");
        constructionDatabaseProp = serializedObject.FindProperty("constructionDatabase");
        boardTilemapProp = serializedObject.FindProperty("boardTilemap");
        snapToCellCenterProp = serializedObject.FindProperty("snapToCellCenter");
        autoSnapWhenMovedInEditorProp = serializedObject.FindProperty("autoSnapWhenMovedInEditor");
        currentCellPositionProp = serializedObject.FindProperty("currentCellPosition");
        teamIdProp = serializedObject.FindProperty("teamId");
        constructionIdProp = serializedObject.FindProperty("constructionId");
        instanceIdProp = serializedObject.FindProperty("instanceId");
        currentPositionProp = serializedObject.FindProperty("currentPosition");
        constructionDisplayNameProp = serializedObject.FindProperty("constructionDisplayName");
        currentHpProp = serializedObject.FindProperty("currentHP");
        autoApplyOnStartProp = serializedObject.FindProperty("autoApplyOnStart");
    }

    public override void OnInspectorGUI()
    {
        if (spriteRendererProp == null || constructionDatabaseProp == null || boardTilemapProp == null || teamIdProp == null || constructionIdProp == null)
        {
            EditorGUILayout.HelpBox("ConstructionManagerEditor: propriedades nao encontradas. Usando inspector padrao.", MessageType.Warning);
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();

        EditorGUILayout.PropertyField(spriteRendererProp);
        EditorGUILayout.PropertyField(constructionDatabaseProp);
        EditorGUILayout.PropertyField(boardTilemapProp, new GUIContent("Board Tilemap"));
        EditorGUILayout.PropertyField(snapToCellCenterProp, new GUIContent("Snap To Cell Center"));
        EditorGUILayout.PropertyField(autoSnapWhenMovedInEditorProp, new GUIContent("Auto Snap When Moved In Editor"));
        EditorGUILayout.PropertyField(currentCellPositionProp, new GUIContent("Cell Position"));
        EditorGUILayout.PropertyField(teamIdProp, new GUIContent("Team ID"));

        DrawConstructionIdPopup();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(instanceIdProp, new GUIContent("Instance ID"));

        EditorGUILayout.PropertyField(currentPositionProp, new GUIContent("Current Position"));
        EditorGUILayout.PropertyField(constructionDisplayNameProp, new GUIContent("Construction Display Name"));
        EditorGUILayout.PropertyField(currentHpProp, new GUIContent("Current HP"));

        EditorGUILayout.PropertyField(autoApplyOnStartProp);
        serializedObject.ApplyModifiedProperties();

        ConstructionManager construction = (ConstructionManager)target;
        if (GUILayout.Button("Apply From Database"))
            construction.ApplyFromDatabase();
        if (GUILayout.Button("Snap To Cell Center"))
            construction.SnapToCellCenter();
        if (GUILayout.Button("Pull Cell From Transform"))
            construction.PullCellFromTransform();
    }

    private void DrawConstructionIdPopup()
    {
        if (constructionDatabaseProp == null || constructionIdProp == null)
        {
            if (constructionIdProp != null)
                EditorGUILayout.PropertyField(constructionIdProp, new GUIContent("Construction ID"));
            return;
        }

        ConstructionDatabase db = constructionDatabaseProp.objectReferenceValue as ConstructionDatabase;
        if (db == null || db.Constructions == null || db.Constructions.Count == 0)
        {
            EditorGUILayout.PropertyField(constructionIdProp, new GUIContent("Construction ID"));
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

        int newIndex = EditorGUILayout.Popup("Construction ID", Mathf.Max(0, currentIndex), labels);
        if (newIndex >= 0 && newIndex < count && db.Constructions[newIndex] != null)
            constructionIdProp.stringValue = db.Constructions[newIndex].id;
    }

}
