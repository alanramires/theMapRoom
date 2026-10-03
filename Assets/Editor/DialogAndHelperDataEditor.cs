using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogData))]
[CanEditMultipleObjects]
public class DialogDataEditor : Editor
{
    public override void OnInspectorGUI() => PanelMessageDataInspector.Draw(serializedObject);
}

[CustomEditor(typeof(HelperData))]
[CanEditMultipleObjects]
public class HelperDataEditor : Editor
{
    public override void OnInspectorGUI() => PanelMessageDataInspector.Draw(serializedObject);
}

internal static class PanelMessageDataInspector
{
    public static void Draw(SerializedObject data)
    {
        data.Update();
        EditorGUILayout.PropertyField(data.FindProperty("id"));
        EditorGUILayout.PropertyField(data.FindProperty("condition"));
        EditorGUILayout.PropertyField(data.FindProperty("message"), new GUIContent("Message (Português — Brasil)"));
        EditorGUILayout.PropertyField(data.FindProperty("messageEnglish"), new GUIContent("Message (English)"));
        EditorGUILayout.HelpBox(
            "Inglês vazio usa a mensagem em português. Preserve os nomes dos tokens <unit>, <domain>, etc.; você pode mudar sua ordem. " +
            "Selecione o idioma antes do Play em Tools > Messages > Idioma dos paineis.", MessageType.Info);
        data.ApplyModifiedProperties();
    }

    [MenuItem("Tools/Messages/Idioma dos paineis")]
    public static void SelectLanguageSettings()
    {
        var settings = Resources.Load<PanelMessageLanguageSettings>(PanelMessageLanguageSettings.ResourceName);
        if (settings == null)
        {
            Debug.LogError("Configuração ausente: Assets/DB/Messages/Resources/Panel Message Language.asset");
            return;
        }
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }
}
