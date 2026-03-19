using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TurnStateManager))]
public class TurnStateManagerEditor : Editor
{
    private SerializedProperty matchControllerProp;
    private SerializedProperty showPerfRangeLineProp;
    private SerializedProperty showPerfSensorsLineProp;
    private SerializedProperty showPerfSelectionLineProp;
    private SerializedProperty showPerfTakeoffPrepLineProp;
    private SerializedProperty enableTurnStateRuntimeLogsProp;

    private void OnEnable()
    {
        matchControllerProp = serializedObject.FindProperty("matchController");
        showPerfRangeLineProp = serializedObject.FindProperty("showPerfRangeLine");
        showPerfSensorsLineProp = serializedObject.FindProperty("showPerfSensorsLine");
        showPerfSelectionLineProp = serializedObject.FindProperty("showPerfSelectionLine");
        showPerfTakeoffPrepLineProp = serializedObject.FindProperty("showPerfTakeoffPrepLine");
        enableTurnStateRuntimeLogsProp = serializedObject.FindProperty("enableTurnStateRuntimeLogs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug (Quick Access)", EditorStyles.boldLabel);
        if (enableTurnStateRuntimeLogsProp != null)
            EditorGUILayout.PropertyField(enableTurnStateRuntimeLogsProp, new GUIContent("Enable TurnState Runtime Logs"));
        if (showPerfRangeLineProp != null)
            EditorGUILayout.PropertyField(showPerfRangeLineProp, new GUIContent("Show Perf Range Line"));
        if (showPerfSensorsLineProp != null)
            EditorGUILayout.PropertyField(showPerfSensorsLineProp, new GUIContent("Show Perf Sensors Line"));
        if (showPerfSelectionLineProp != null)
            EditorGUILayout.PropertyField(showPerfSelectionLineProp, new GUIContent("Show Perf Selection Line"));
        if (showPerfTakeoffPrepLineProp != null)
            EditorGUILayout.PropertyField(showPerfTakeoffPrepLineProp, new GUIContent("Show Perf Takeoff Prep Line"));

        DrawMatchControllerSensorQuickAccess();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMatchControllerSensorQuickAccess()
    {
        MatchController match = matchControllerProp != null
            ? matchControllerProp.objectReferenceValue as MatchController
            : null;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sensors (MatchController)", EditorStyles.boldLabel);

        if (match == null)
        {
            EditorGUILayout.HelpBox("Referencie o MatchController para editar logs de AindaMeVe/PodeDetectar/PodeEnxergar aqui.", MessageType.Info);
            return;
        }

        SerializedObject matchSo = new SerializedObject(match);
        matchSo.Update();

        SerializedProperty sensorsMaster = matchSo.FindProperty("enableSensorsRuntimeLogs");
        SerializedProperty aindaMeVe = matchSo.FindProperty("enableAindaMeVeRuntimeLogs");
        SerializedProperty podeDetectar = matchSo.FindProperty("enablePodeDetectarRuntimeLogs");
        SerializedProperty podeEnxergar = matchSo.FindProperty("enablePodeEnxergarRuntimeLogs");

        if (sensorsMaster != null)
            EditorGUILayout.PropertyField(sensorsMaster, new GUIContent("Enable Sensors Runtime Logs (Master)"));
        if (aindaMeVe != null)
            EditorGUILayout.PropertyField(aindaMeVe, new GUIContent("Enable AindaMeVe Runtime Logs"));
        if (podeDetectar != null)
            EditorGUILayout.PropertyField(podeDetectar, new GUIContent("Enable PodeDetectar Runtime Logs"));
        if (podeEnxergar != null)
            EditorGUILayout.PropertyField(podeEnxergar, new GUIContent("Enable PodeEnxergar Runtime Logs"));

        matchSo.ApplyModifiedProperties();
    }
}
