using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ServiceData))]
public class ServiceDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty idProp = serializedObject.FindProperty("id");
        SerializedProperty displayNameProp = serializedObject.FindProperty("displayName");
        SerializedProperty descriptionProp = serializedObject.FindProperty("description");
        SerializedProperty spriteDefaultProp = serializedObject.FindProperty("spriteDefault");
        SerializedProperty serviceTypeProp = serializedObject.FindProperty("serviceType");
        SerializedProperty isServiceProp = serializedObject.FindProperty("isService");
        SerializedProperty recuperaHpProp = serializedObject.FindProperty("recuperaHp");
        SerializedProperty recuperaAutonomiaProp = serializedObject.FindProperty("recuperaAutonomia");
        SerializedProperty recuperaMunicaoProp = serializedObject.FindProperty("recuperaMunicao");
        SerializedProperty apenasEntreSupridoresProp = serializedObject.FindProperty("apenasEntreSupridores");
        SerializedProperty percentCostProp = serializedObject.FindProperty("percentCost");
        SerializedProperty suppliesUsedProp = serializedObject.FindProperty("suppliesUsed");
        SerializedProperty serviceEfficiencyProp = serializedObject.FindProperty("serviceEfficiency");
        SerializedProperty serviceLimitProp = serializedObject.FindProperty("serviceLimitPerUnitPerTurn");

        if (idProp != null)
            EditorGUILayout.PropertyField(idProp);
        if (displayNameProp != null)
            EditorGUILayout.PropertyField(displayNameProp);
        if (descriptionProp != null)
            EditorGUILayout.PropertyField(descriptionProp);
        if (spriteDefaultProp != null)
            EditorGUILayout.PropertyField(spriteDefaultProp);
        if (serviceTypeProp != null)
            EditorGUILayout.PropertyField(serviceTypeProp);
        if (isServiceProp != null)
            EditorGUILayout.PropertyField(isServiceProp, new GUIContent("Is Service"));
        if (recuperaHpProp != null)
            EditorGUILayout.PropertyField(recuperaHpProp, new GUIContent("Recupera HP"));
        if (recuperaAutonomiaProp != null)
            EditorGUILayout.PropertyField(recuperaAutonomiaProp, new GUIContent("Recupera Autonomia"));
        if (recuperaMunicaoProp != null)
            EditorGUILayout.PropertyField(recuperaMunicaoProp, new GUIContent("Recupera Municao"));
        if (apenasEntreSupridoresProp != null)
            EditorGUILayout.PropertyField(apenasEntreSupridoresProp, new GUIContent("Apenas Entre Supridores"));
        if (percentCostProp != null)
            EditorGUILayout.PropertyField(percentCostProp, new GUIContent("Economy Percent Cost"));
        if (suppliesUsedProp != null)
            EditorGUILayout.PropertyField(suppliesUsedProp, new GUIContent("Supply Used"), includeChildren: true);
        EditorGUILayout.Space();
        DrawPointsRecoverList(serviceEfficiencyProp);
        if (serviceLimitProp != null)
            EditorGUILayout.PropertyField(serviceLimitProp);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawPointsRecoverList(SerializedProperty listProp)
    {
        if (listProp == null)
            return;

        EditorGUILayout.LabelField("Points Recover", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty element = listProp.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            SerializedProperty classProp = element.FindPropertyRelative("armorWeaponClass");
            SerializedProperty valueProp = element.FindPropertyRelative("value");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.MaxWidth(70f)))
            {
                listProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (classProp != null)
                EditorGUILayout.PropertyField(classProp, new GUIContent("Armor / Weapon Class"));
            if (valueProp != null)
                EditorGUILayout.PropertyField(valueProp, new GUIContent("Value"));

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add Points Recover Entry"))
            listProp.arraySize += 1;

        EditorGUI.indentLevel--;
    }
}
