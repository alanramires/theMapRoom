using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StructureData))]
public class StructureDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "aircraftOpsByTerrain",
            "roadRoutes",
            "forceEndMovementOnTerrainDomainForDomains");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Aircraft Ops", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "As regras de air ops em estrutura sao avaliadas em par: Estrutura + Terreno base.\n" +
            "Cada elemento define:\n" +
            "- Terrain Data: terreno base do par\n" +
            "- Allow Take Off and Landing: se este par permite pouso/decolagem\n" +
            "- Required Landing Skills: para cada skill do par, configure o take off mode\n" +
            "- Require At Least One Landing Skill: quando true, basta 1 skill da lista; quando false, exige todas.",
            MessageType.Info);

        DrawIfExists(serializedObject.FindProperty("aircraftOpsByTerrain"), "Aircraft Ops By Terrain");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Naval Ops", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "No par Estrutura+Terreno, unidades nesses dominios/alturas encerram movimento no dominio do terreno base.",
            MessageType.Info);
        DrawIfExists(serializedObject.FindProperty("forceEndMovementOnTerrainDomainForDomains"), "The Units On The Follow Domain Are Forced To Emerge");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Road Routes", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "As rotas de estrada deste mapa agora sao centralizadas no StructureDatabase (catalogo), nao no StructureData.\n" +
            "Use: Tools > Logistica > Road Route Painter para editar.",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren: true);
    }
}
