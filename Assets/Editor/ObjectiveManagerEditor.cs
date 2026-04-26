using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TeamObjectivePlan))]
public class TeamObjectivePlanDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty teamProp = property.FindPropertyRelative("Team");
        if (teamProp != null)
        {
            int idx = Mathf.Clamp(teamProp.enumValueIndex, 0, teamProp.enumDisplayNames.Length - 1);
            label.text = teamProp.enumDisplayNames[idx];
        }
        EditorGUI.PropertyField(position, property, label, includeChildren: true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
}

[CustomPropertyDrawer(typeof(SectorObjective))]
public class SectorObjectiveDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty sectorProp = property.FindPropertyRelative("Sector");
        if (sectorProp != null)
        {
            int idx = Mathf.Clamp(sectorProp.enumValueIndex, 0, sectorProp.enumDisplayNames.Length - 1);
            label.text = sectorProp.enumDisplayNames[idx];
        }
        EditorGUI.PropertyField(position, property, label, includeChildren: true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
}
