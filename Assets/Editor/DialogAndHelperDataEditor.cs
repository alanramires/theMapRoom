using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogData))]
public class DialogDataEditor : Editor
{
    private const string DialogMessageTooltip =
        "Tokens disponiveis: <unit>, <state>, <sensor>.\n\n" +
        "Uso por ID:\n" +
        "- panel_dialog.state.moving -> <unit>, <state>\n" +
        "- panel_dialog.label.moving -> sem token\n" +
        "- panel_dialog.state.sensor -> <unit>, <sensor>\n" +
        "- panel_dialog.state.sensor_confirm -> <unit>, <sensor>\n" +
        "- panel_dialog.sensor.* -> sem token";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("condition"));
        DrawMessageWithHelp(serializedObject.FindProperty("message"), DialogMessageTooltip);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawMessageWithHelp(SerializedProperty messageProperty, string tooltip)
    {
        EditorGUILayout.PropertyField(messageProperty);
        EditorGUILayout.HelpBox(tooltip, MessageType.Info);
    }
}

[CustomEditor(typeof(HelperData))]
public class HelperDataEditor : Editor
{
    private const string HelperMessageTooltip =
        "Tokens comuns: <action>, <label>, <index>, <unit>, <valor>, <stats>, <terrain>.\n\n" +
        "Uso por ID:\n" +
        "- helper.shopping.line.with_cost -> <index>, <unit>, <valor>\n" +
        "- helper.shopping.line.no_cost -> <index>, <unit>\n" +
        "- helper.sensors.line.format -> <action>, <label>\n" +
        "- helper.sensors.line.move_only -> <action>, <label>\n" +
        "- helper.disembark.order.line -> <index>, <unit>, <stats>, <terrain>\n" +
        "- helper.disembark.passenger.line -> <index>, <unit>, <stats>\n" +
        "- demais IDs de titulo/label -> sem token";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("condition"));
        DrawMessageWithHelp(serializedObject.FindProperty("message"), HelperMessageTooltip);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawMessageWithHelp(SerializedProperty messageProperty, string tooltip)
    {
        EditorGUILayout.PropertyField(messageProperty);
        EditorGUILayout.HelpBox(tooltip, MessageType.Info);
    }
}
