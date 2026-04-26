using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SectorManager.SectorHQDistance))]
public class SectorHQDistanceDrawer : PropertyDrawer
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

[CustomPropertyDrawer(typeof(SectorManager.SectorRiskEntry))]
public class SectorRiskEntryDrawer : PropertyDrawer
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

[CustomEditor(typeof(SectorManager))]
public class SectorManagerEditor : Editor
{
    private SerializedProperty sectorLogProp;
    private SerializedProperty sectorInfosProp;
    private SerializedProperty baseInfosProp;

    private void OnEnable()
    {
        sectorLogProp   = serializedObject.FindProperty("sectorLog");
        sectorInfosProp = serializedObject.FindProperty("sectorInfos");
        baseInfosProp   = serializedObject.FindProperty("baseInfos");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (sectorLogProp != null)
            EditorGUILayout.PropertyField(sectorLogProp);

        EditorGUILayout.Space(4f);

        SectorManager manager = (SectorManager)target;
        if (GUILayout.Button("Rebuild From Active Constructions"))
        {
            manager.RebuildFromActiveConstructions();
            EditorUtility.SetDirty(manager);
        }

        EditorGUILayout.Space(6f);

        DrawSectorList(sectorInfosProp, "Sector Infos");
        EditorGUILayout.Space(4f);
        DrawSectorList(baseInfosProp, "Base Infos");

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawSectorList(SerializedProperty prop, string label)
    {
        if (prop == null)
            return;

        EditorGUILayout.LabelField($"{label} ({prop.arraySize})", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        for (int i = 0; i < prop.arraySize; i++)
        {
            SerializedProperty element = prop.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            SerializedProperty sectorProp = element.FindPropertyRelative("sector");
            string sectorLabel = sectorProp != null
                ? sectorProp.enumDisplayNames[Mathf.Clamp(sectorProp.enumValueIndex, 0, sectorProp.enumDisplayNames.Length - 1)]
                : $"Setor {i}";

            EditorGUILayout.PropertyField(element, new GUIContent(sectorLabel), includeChildren: true);
        }
        EditorGUI.indentLevel--;
    }
}
