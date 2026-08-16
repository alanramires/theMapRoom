using UnityEditor;
using UnityEngine;

/// <summary>
/// O catalogo diz o que uma construcao E, e so isso.
///
/// Este editor tinha 311 linhas: lista de fieldEntries, migracao do
/// ConstructionFieldDatabase "legacy", e busca da instancia em cena por celula.
/// Tudo isso servia a um campo de LAYOUT que morava no catalogo — o que obrigava
/// a existir um catalogo por mapa (eram sete) e fazia o teste de aceitacao do
/// CLAUDE.md ("duplique uma cena e ela nasce vazia") falhar.
///
/// O campo tinha zero leitores em runtime e foi removido. Layout mora na cena; no
/// modelo de campanha, em QuadranteData.bakedConstrucoes.
/// </summary>
[CustomEditor(typeof(ConstructionDatabase))]
public class ConstructionDatabaseEditor : Editor
{
    private SerializedProperty constructionsProp;

    private void OnEnable()
    {
        constructionsProp = serializedObject.FindProperty("constructions");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Catálogo compartilhado: diz o que uma construção É.\n\n"
            + "Agrupe por conteúdo (\"básico\", \"com naval\"), nunca por mapa — "
            + "layout mora na cena.",
            MessageType.Info);

        EditorGUILayout.PropertyField(constructionsProp, new GUIContent("Constructions"), includeChildren: true);

        serializedObject.ApplyModifiedProperties();
    }
}
