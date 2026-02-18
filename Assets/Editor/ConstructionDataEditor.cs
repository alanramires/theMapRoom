using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionData))]
public class ConstructionDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script", "constructionConfiguration");
        EditorGUILayout.Space();
        DrawConstructionConfigurationExpanded(serializedObject.FindProperty("constructionConfiguration"));

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawConstructionConfigurationExpanded(SerializedProperty configProperty)
    {
        if (configProperty == null)
            return;

        EditorGUILayout.LabelField("Construction Configuration", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        DrawIfExists(configProperty.FindPropertyRelative("isPlayerHeadQuarter"), "Is Player Head Quarter");
        DrawIfExists(configProperty.FindPropertyRelative("isCapturable"), "Is Capturable");
        DrawIfExists(configProperty.FindPropertyRelative("capturePointsMax"), "Capture Points Max");
        DrawIfExists(configProperty.FindPropertyRelative("canProduceUnits"), "Can Produce Units");
        DrawIfExists(configProperty.FindPropertyRelative("offeredUnits"), "Offered Units");

        EditorGUI.indentLevel--;
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren: true);
    }
}
