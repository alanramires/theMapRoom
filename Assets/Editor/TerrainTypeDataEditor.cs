using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainTypeData))]
public class TerrainTypeDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "allowAircraftTakeoffAndLanding",
            "requiredLandingSkills",
            "requireAtLeastOneLandingSkill");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Aircraft Ops", EditorStyles.boldLabel);
        DrawIfExists(serializedObject.FindProperty("allowAircraftTakeoffAndLanding"), "Allow Aicrafft Take Off and Landing");
        DrawIfExists(serializedObject.FindProperty("requiredLandingSkills"), "Required Landing Skills");
        DrawIfExists(serializedObject.FindProperty("requireAtLeastOneLandingSkill"), "Pelo menos 1 skill");

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren: true);
    }
}
