using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponData))]
public class WeaponDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawTopSection();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "id",
            "displayName",
            "description",
            "basicAttack",
            "weaponCategory",
            "weaponClass");

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTopSection()
    {
        SerializedProperty idProperty = serializedObject.FindProperty("id");
        SerializedProperty displayNameProperty = serializedObject.FindProperty("displayName");
        SerializedProperty descriptionProperty = serializedObject.FindProperty("description");
        SerializedProperty basicAttackProperty = serializedObject.FindProperty("basicAttack");
        SerializedProperty weaponCategoryProperty = serializedObject.FindProperty("weaponCategory");
        SerializedProperty weaponClassProperty = serializedObject.FindProperty("weaponClass");

        if (idProperty != null)
            EditorGUILayout.PropertyField(idProperty);
        if (displayNameProperty != null)
            EditorGUILayout.PropertyField(displayNameProperty);
        if (descriptionProperty != null)
            EditorGUILayout.PropertyField(descriptionProperty);
        if (basicAttackProperty != null)
            EditorGUILayout.PropertyField(basicAttackProperty);
        if (weaponCategoryProperty != null)
            EditorGUILayout.PropertyField(weaponCategoryProperty, new GUIContent("Weapon Category"));
        if (weaponClassProperty != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(weaponClassProperty, new GUIContent("Weapon Class (Auto by Attack)"));
        }
    }
}
