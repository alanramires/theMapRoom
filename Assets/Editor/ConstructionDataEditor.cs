using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionData))]
public class ConstructionDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "constructionConfiguration",
            "allowAircraftTakeoffAndLanding",
            "legacyRequiredLandingSkills",
            "requiredLandingSkillRules",
            "requireAtLeastOneLandingSkill");
        EditorGUILayout.Space();
        DrawAircraftOpsSection(serializedObject);
        EditorGUILayout.Space();
        DrawConstructionConfigurationExpanded(serializedObject.FindProperty("constructionConfiguration"));

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawAircraftOpsSection(SerializedObject so)
    {
        EditorGUILayout.LabelField("Aircraft Ops", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Regras de Air Ops (Construction):\n" +
            "- Allow Aicrafft Take Off and Landing: habilita pouso e decolagem neste contexto.\n" +
            "- Required Landing Skills: para cada skill voce define o take off mode usado neste contexto.",
            MessageType.Info);
        SerializedProperty allowProp = so.FindProperty("allowAircraftTakeoffAndLanding");
        DrawIfExists(allowProp, "Allow Aicrafft Take Off and Landing");
        DrawIfExists(so.FindProperty("requiredLandingSkillRules"), "Required Landing Skills");
        DrawIfExists(so.FindProperty("requireAtLeastOneLandingSkill"), "Pelo menos 1 skill");
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
        DrawIfExists(configProperty.FindPropertyRelative("capturedIncoming"), "Captured Incoming");
        DrawIfExists(configProperty.FindPropertyRelative("canProduceAndSellUnits"), "Can Produce And Sell Units");
        DrawIfExists(configProperty.FindPropertyRelative("offeredUnits"), "Offered Units");

        EditorGUI.indentLevel--;
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren: true);
    }

}
