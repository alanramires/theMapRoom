using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ReplayManager))]
public class ReplayManagerEditor : Editor
{
    SerializedProperty enableReplayRuntimeLogsProperty;
    SerializedProperty enableReplayRuntimeWarningsProperty;
    SerializedProperty snapshotTelemetryEnabledProperty;

    private void OnEnable()
    {
        enableReplayRuntimeLogsProperty = serializedObject.FindProperty("enableReplayRuntimeLogs");
        enableReplayRuntimeWarningsProperty = serializedObject.FindProperty("enableReplayRuntimeWarnings");
        snapshotTelemetryEnabledProperty = serializedObject.FindProperty("snapshotTelemetryEnabled");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "enableReplayRuntimeLogs",
            "enableReplayRuntimeWarnings",
            "snapshotTelemetryEnabled");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Replay Debug", EditorStyles.boldLabel);
        DrawIfPresent(enableReplayRuntimeLogsProperty, "Enable Replay Runtime Logs");
        DrawIfPresent(enableReplayRuntimeWarningsProperty, "Enable Replay Runtime Warnings");
        DrawIfPresent(snapshotTelemetryEnabledProperty, "Enable Snapshot Telemetry");

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawIfPresent(SerializedProperty property, string label)
    {
        if (property == null)
        {
            EditorGUILayout.HelpBox($"{label}: property not found on ReplayManager.", MessageType.None);
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label));
    }
}
