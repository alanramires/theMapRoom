using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(StructureSkillTerrainRule))]
public sealed class StructureSkillTerrainRuleDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, ResolveTerrainLabel(property, label), true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            SerializedProperty child = property.Copy();
            SerializedProperty end = child.GetEndProperty();
            bool enterChildren = true;
            float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                float height = EditorGUI.GetPropertyHeight(child, includeChildren: true);
                Rect childRect = new Rect(position.x, y, position.width, height);
                EditorGUI.PropertyField(childRect, child, includeChildren: true);
                y += height + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;

        SerializedProperty child = property.Copy();
        SerializedProperty end = child.GetEndProperty();
        bool enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            height += EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(child, includeChildren: true);
            enterChildren = false;
        }

        return height;
    }

    private static GUIContent ResolveTerrainLabel(SerializedProperty property, GUIContent fallback)
    {
        SerializedProperty terrainProperty = property.FindPropertyRelative("terrainData");
        TerrainTypeData terrain = terrainProperty != null
            ? terrainProperty.objectReferenceValue as TerrainTypeData
            : null;
        if (terrain == null)
            return fallback;

        string terrainName = string.IsNullOrWhiteSpace(terrain.displayName) ? terrain.name : terrain.displayName;
        return new GUIContent(terrainName, fallback.tooltip);
    }
}
