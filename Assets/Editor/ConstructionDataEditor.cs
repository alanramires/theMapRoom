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
            "requireAtLeastOneLandingSkill",
            "forceEndMovementOnTerrainDomainForDomains");
        EditorGUILayout.Space();
        DrawAircraftOpsSection(serializedObject);
        EditorGUILayout.Space();
        DrawNavalOpsSection(serializedObject);
        EditorGUILayout.Space();
        DrawConstructionConfigurationExpanded(serializedObject.FindProperty("constructionConfiguration"));

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawNavalOpsSection(SerializedObject so)
    {
        EditorGUILayout.LabelField("Naval Ops", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Unidades nesses dominios/alturas encerram movimento no dominio nativo da construcao.",
            MessageType.Info);
        DrawIfExists(so.FindProperty("forceEndMovementOnTerrainDomainForDomains"), "The Units On The Follow Domains End Their Movement On The Terrain Domain");
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
        EditorGUILayout.HelpBox(
            "Quando Is Supplier estiver ativo, Services/Resources de Supplier Settings sao sincronizados automaticamente para Offered Services/Offered Supplies. Offered Units continua 100% manual por construcao.",
            MessageType.Info);
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
