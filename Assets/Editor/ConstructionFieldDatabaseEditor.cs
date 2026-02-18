using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionFieldDatabase))]
public class ConstructionFieldDatabaseEditor : Editor
{
    private SerializedProperty entriesProp;

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

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
        if (GUILayout.Button("Add Entry", GUILayout.MaxWidth(100f)))
            entriesProp.arraySize += 1;
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty idProp = entry.FindPropertyRelative("id");
            SerializedProperty constructionProp = entry.FindPropertyRelative("construction");
            SerializedProperty teamProp = entry.FindPropertyRelative("initialTeamId");
            SerializedProperty cellProp = entry.FindPropertyRelative("cellPosition");
            SerializedProperty initialCaptureProp = entry.FindPropertyRelative("initialCapturePoints");
            SerializedProperty useOverrideProp = entry.FindPropertyRelative("useConstructionConfigurationOverride");
            SerializedProperty configProp = entry.FindPropertyRelative("constructionConfiguration");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.MaxWidth(70f)))
            {
                entriesProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(idProp, new GUIContent("ID"));
            EditorGUILayout.PropertyField(constructionProp, new GUIContent("Construction"));
            EditorGUILayout.PropertyField(teamProp, new GUIContent("Initial Team"));
            EditorGUILayout.PropertyField(cellProp, new GUIContent("Cell Position"));
            EditorGUILayout.PropertyField(initialCaptureProp, new GUIContent("Initial Capture Points (-1 uses max)"));
            EditorGUILayout.PropertyField(useOverrideProp, new GUIContent("Use Construction Configuration Override"));

            if (useOverrideProp != null && useOverrideProp.boolValue && configProp != null)
            {
                EditorGUILayout.LabelField("Construction Configuration (Override)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(configProp.FindPropertyRelative("isPlayerHeadQuarter"), new GUIContent("Is Player Head Quarter"));
                EditorGUILayout.PropertyField(configProp.FindPropertyRelative("isCapturable"), new GUIContent("Is Capturable"));
                EditorGUILayout.PropertyField(configProp.FindPropertyRelative("capturePointsMax"), new GUIContent("Capture Points Max"));
                EditorGUILayout.PropertyField(configProp.FindPropertyRelative("canProduceUnits"), new GUIContent("Can Produce Units"));
                EditorGUILayout.PropertyField(configProp.FindPropertyRelative("offeredUnits"), new GUIContent("Offered Units"), true);
                EditorGUILayout.PropertyField(configProp.FindPropertyRelative("canProvideSupplies"), new GUIContent("Can Provide Supplies"));
                EditorGUILayout.PropertyField(configProp.FindPropertyRelative("offeredSupplies"), new GUIContent("Offered Supplies"), true);
                EditorGUILayout.PropertyField(configProp.FindPropertyRelative("offeredServices"), new GUIContent("Offered Services"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
