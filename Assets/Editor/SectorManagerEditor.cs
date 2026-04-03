using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SectorManager))]
public class SectorManagerEditor : Editor
{
    private SerializedProperty sectorLogProp;
    private SerializedProperty sectorInfosProp;

    private void OnEnable()
    {
        sectorLogProp = serializedObject.FindProperty("sectorLog");
        sectorInfosProp = serializedObject.FindProperty("sectorInfos");
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

        if (sectorInfosProp == null)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.LabelField($"Sector Infos ({sectorInfosProp.arraySize})", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        for (int i = 0; i < sectorInfosProp.arraySize; i++)
        {
            SerializedProperty element = sectorInfosProp.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            SerializedProperty sectorProp = element.FindPropertyRelative("sector");
            string sectorLabel = sectorProp != null
                ? sectorProp.enumDisplayNames[Mathf.Clamp(sectorProp.enumValueIndex, 0, sectorProp.enumDisplayNames.Length - 1)]
                : $"Setor {i}";

            EditorGUILayout.PropertyField(element, new GUIContent(sectorLabel), includeChildren: true);
        }
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
    }
}
