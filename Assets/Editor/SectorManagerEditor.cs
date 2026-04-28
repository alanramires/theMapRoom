using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SectorManager.SectorTeamDistances))]
public class SectorTeamDistancesDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty teamProp    = property.FindPropertyRelative("Team");
        SerializedProperty entriesProp = property.FindPropertyRelative("Entries");

        string teamName = teamProp != null
            ? teamProp.enumDisplayNames[Mathf.Clamp(teamProp.enumValueIndex, 0, teamProp.enumDisplayNames.Length - 1)]
            : label.text;

        float lineH   = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        Rect foldRect = new Rect(position.x, position.y, position.width, lineH);
        property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, teamName, true);

        if (!property.isExpanded || entriesProp == null)
            return;

        float y = position.y + lineH + spacing;
        EditorGUI.indentLevel++;
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry    = entriesProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = entry.FindPropertyRelative("ConstructionName");
            SerializedProperty distProp = entry.FindPropertyRelative("Distance");
            SerializedProperty hqProp   = entry.FindPropertyRelative("IsHQ");

            string entryLabel = (nameProp != null ? nameProp.stringValue : "?")
                + (distProp != null ? $": {distProp.floatValue:F0}h" : "")
                + (hqProp != null && hqProp.boolValue ? " [HQ]" : "");

            Rect entryRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.LabelField(entryRect, entryLabel);
            y += lineH + spacing;
        }
        EditorGUI.indentLevel--;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineH   = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded) return lineH;

        SerializedProperty entriesProp = property.FindPropertyRelative("Entries");
        int count = entriesProp != null ? entriesProp.arraySize : 0;
        return lineH + (lineH + spacing) * count;
    }
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

            if (element.isExpanded)
            {
                SerializedProperty n1 = element.FindPropertyRelative("closestNeighbor1");
                SerializedProperty d1 = element.FindPropertyRelative("closestNeighbor1Distance");
                SerializedProperty n2 = element.FindPropertyRelative("closestNeighbor2");
                SerializedProperty d2 = element.FindPropertyRelative("closestNeighbor2Distance");

                string FormatNeighbor(SerializedProperty nProp, SerializedProperty dProp)
                {
                    if (nProp == null || dProp == null || dProp.floatValue >= float.MaxValue * 0.5f)
                        return "—";
                    string name = nProp.enumDisplayNames[Mathf.Clamp(nProp.enumValueIndex, 0, nProp.enumDisplayNames.Length - 1)];
                    return $"{name} ({dProp.floatValue:F0}h)";
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Neighbors", $"{FormatNeighbor(n1, d1)}  |  {FormatNeighbor(n2, d2)}");
                EditorGUI.indentLevel--;
            }
        }
        EditorGUI.indentLevel--;
    }
}
