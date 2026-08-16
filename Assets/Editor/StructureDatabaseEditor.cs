using UnityEditor;
using UnityEngine;

/// <summary>
/// O catalogo diz o que uma estrutura E, e so isso.
///
/// Este editor tinha 108 linhas, e quase todas eram a migracao
/// StructureData.roadRoutes -> StructureDatabase.roadRoutesByStructure. As DUAS
/// pontas eram layout em catalogo, e as duas foram removidas: 16 rotas nos tipos
/// (o asset "Rodovias" carregava 11 tracados concretos) e 93 nos 16 catalogos.
///
/// Layout de estrada mora na CENA, no RoadNetworkManager — e, no modelo de
/// campanha, no bake do quadrante.
/// </summary>
[CustomEditor(typeof(StructureDatabase))]
public class StructureDatabaseEditor : Editor
{
    private SerializedProperty structuresProp;

    private void OnEnable()
    {
        structuresProp = serializedObject.FindProperty("structures");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Catálogo compartilhado: diz o que uma estrutura É.\n\n"
            + "Agrupe por conteúdo, nunca por mapa — o traçado das estradas mora na cena.",
            MessageType.Info);

        EditorGUILayout.PropertyField(structuresProp, new GUIContent("Structures"), includeChildren: true);

        serializedObject.ApplyModifiedProperties();
    }
}
