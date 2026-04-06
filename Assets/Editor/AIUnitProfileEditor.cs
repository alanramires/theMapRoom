using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIUnitProfile))]
public class AIUnitProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}

[CustomPropertyDrawer(typeof(AIUnitStanceBehavior))]
public class AIUnitStanceBehaviorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.PropertyField(position, property, new GUIContent(GetBehaviorLabel(property)), true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    private string GetBehaviorLabel(SerializedProperty property)
    {
        SerializedProperty stancesProp = property.FindPropertyRelative("stances");
        if (stancesProp == null || stancesProp.arraySize == 0)
            return "Stance Behavior (sem stances)";

        var names = new List<string>();
        for (int i = 0; i < stancesProp.arraySize; i++)
        {
            SerializedProperty elem = stancesProp.GetArrayElementAtIndex(i);
            if (elem.objectReferenceValue is BattleStanceData bsd)
                names.Add(!string.IsNullOrEmpty(bsd.displayName) ? bsd.displayName : bsd.name);
            else
                names.Add("?");
        }

        return string.Join(" / ", names);
    }
}
