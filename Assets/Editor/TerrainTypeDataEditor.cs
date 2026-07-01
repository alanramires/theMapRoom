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
            "requireAtLeastOneLandingSkill",
            "forceEndMovementOnTerrainDomainForDomains",
            "ev",
            "shooterInheritsTerrainEv",
            "shooterInheritedEvOverride",
            "blockLoS");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Vision", EditorStyles.boldLabel);
        DrawIfExists(serializedObject.FindProperty("ev"), "EV");

        SerializedProperty inheritsTerrainEv = serializedObject.FindProperty("shooterInheritsTerrainEv");
        DrawIfExists(inheritsTerrainEv, "Shooter Inherits Terrain EV");

        using (new EditorGUI.DisabledScope(inheritsTerrainEv == null || !inheritsTerrainEv.boolValue))
        {
            DrawIfExists(serializedObject.FindProperty("shooterInheritedEvOverride"), "Override EV To");
        }

        DrawIfExists(serializedObject.FindProperty("blockLoS"), "Block LoS");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Aircraft Ops", EditorStyles.boldLabel);
        DrawIfExists(serializedObject.FindProperty("allowAircraftTakeoffAndLanding"), "Allow Aicrafft Take Off and Landing");
        DrawIfExists(serializedObject.FindProperty("requiredLandingSkills"), "Required Landing Skills");
        DrawIfExists(serializedObject.FindProperty("requireAtLeastOneLandingSkill"), "Pelo menos 1 skill");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Naval Ops", EditorStyles.boldLabel);
        DrawIfExists(serializedObject.FindProperty("forceEndMovementOnTerrainDomainForDomains"), "The Units On The Follow Domain Are Forced To Emerge");

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren: true);
    }
}
