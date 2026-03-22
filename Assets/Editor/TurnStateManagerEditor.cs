using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TurnStateManager))]
public class TurnStateManagerEditor : Editor
{
    private SerializedProperty matchControllerProp;
    private SerializedProperty enableRangeCacheDebugLogsProp;
    private SerializedProperty showPerfRangeLineProp;
    private SerializedProperty showPerfSensorsLineProp;
    private SerializedProperty showPerfSelectionLineProp;
    private SerializedProperty showPerfTakeoffPrepLineProp;
    private SerializedProperty enableTurnStateRuntimeLogsProp;

    private void OnEnable()
    {
        matchControllerProp = serializedObject.FindProperty("matchController");
        enableRangeCacheDebugLogsProp = serializedObject.FindProperty("enableRangeCacheDebugLogs");
        showPerfRangeLineProp = serializedObject.FindProperty("showPerfRangeLine");
        showPerfSensorsLineProp = serializedObject.FindProperty("showPerfSensorsLine");
        showPerfSelectionLineProp = serializedObject.FindProperty("showPerfSelectionLine");
        showPerfTakeoffPrepLineProp = serializedObject.FindProperty("showPerfTakeoffPrepLine");
        enableTurnStateRuntimeLogsProp = serializedObject.FindProperty("enableTurnStateRuntimeLogs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "enableTurnStateRuntimeLogs",
            "enableRangeCacheDebugLogs",
            "showPerfRangeLine",
            "showPerfSensorsLine",
            "showPerfSelectionLine",
            "showPerfTakeoffPrepLine");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Logs & Perf (Quick Access)", EditorStyles.boldLabel);
        if (enableTurnStateRuntimeLogsProp != null)
            EditorGUILayout.PropertyField(enableTurnStateRuntimeLogsProp, new GUIContent("Enable TurnState Runtime Logs"));
        if (enableRangeCacheDebugLogsProp != null)
            EditorGUILayout.PropertyField(enableRangeCacheDebugLogsProp, new GUIContent("Enable Range Cache Debug Logs"));
        if (showPerfRangeLineProp != null)
            EditorGUILayout.PropertyField(showPerfRangeLineProp, new GUIContent("Show Perf Range Line"));
        if (showPerfSensorsLineProp != null)
            EditorGUILayout.PropertyField(showPerfSensorsLineProp, new GUIContent("Show Perf Sensors Line"));
        if (showPerfSelectionLineProp != null)
            EditorGUILayout.PropertyField(showPerfSelectionLineProp, new GUIContent("Show Perf Selection Line"));
        if (showPerfTakeoffPrepLineProp != null)
            EditorGUILayout.PropertyField(showPerfTakeoffPrepLineProp, new GUIContent("Show Perf Takeoff Prep Line"));

        DrawMatchControllerLogQuickAccess();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMatchControllerLogQuickAccess()
    {
        MatchController match = matchControllerProp != null
            ? matchControllerProp.objectReferenceValue as MatchController
            : null;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("MatchController Logs", EditorStyles.boldLabel);

        if (match == null)
        {
            EditorGUILayout.HelpBox("Referencie o MatchController para editar logs relacionados ao turno/sensores aqui.", MessageType.Info);
            return;
        }

        SerializedObject matchSo = new SerializedObject(match);
        matchSo.Update();

        SerializedProperty turnPerfLogs = matchSo.FindProperty("enableTurnPerfLogs");
        SerializedProperty fogSource = matchSo.FindProperty("enableFogSourceDebugLogs");
        SerializedProperty fogStepPerf = matchSo.FindProperty("enableFogStepPerfLogs");
        SerializedProperty fogValidation = matchSo.FindProperty("enableFogValidationLogs");
        SerializedProperty sensorsMaster = matchSo.FindProperty("enableSensorsRuntimeLogs");
        SerializedProperty podeMirar = matchSo.FindProperty("enablePodeMirarSensorLogs");
        SerializedProperty podeEmbarcar = matchSo.FindProperty("enablePodeEmbarcarSensorLogs");
        SerializedProperty podeDesembarcar = matchSo.FindProperty("enablePodeDesembarcarSensorLogs");
        SerializedProperty podeCapturar = matchSo.FindProperty("enablePodeCapturarSensorLogs");
        SerializedProperty podeFundir = matchSo.FindProperty("enablePodeFundirSensorLogs");
        SerializedProperty podeSuprir = matchSo.FindProperty("enablePodeSuprirSensorLogs");
        SerializedProperty podeTransferir = matchSo.FindProperty("enablePodeTransferirSensorLogs");
        SerializedProperty servicoDoComando = matchSo.FindProperty("enableServicoDoComandoSensorLogs");
        SerializedProperty podePousar = matchSo.FindProperty("enablePodePousarSensorLogs");
        SerializedProperty podeDecolar = matchSo.FindProperty("enablePodeDecolarSensorLogs");
        SerializedProperty aindaMeVe = matchSo.FindProperty("enableAindaMeVeRuntimeLogs");
        SerializedProperty podeDetectar = matchSo.FindProperty("enablePodeDetectarRuntimeLogs");
        SerializedProperty podeEnxergar = matchSo.FindProperty("enablePodeEnxergarRuntimeLogs");

        if (turnPerfLogs != null)
            EditorGUILayout.PropertyField(turnPerfLogs, new GUIContent("Enable Turn Perf Logs"));
        if (fogSource != null)
            EditorGUILayout.PropertyField(fogSource, new GUIContent("Enable Fog Source Debug Logs"));
        if (fogStepPerf != null)
            EditorGUILayout.PropertyField(fogStepPerf, new GUIContent("Enable Fog Step Perf Logs"));
        if (fogValidation != null)
            EditorGUILayout.PropertyField(fogValidation, new GUIContent("Enable Fog Validation Logs"));
        if (sensorsMaster != null)
            EditorGUILayout.PropertyField(sensorsMaster, new GUIContent("Enable Sensors Runtime Logs (Master)"));
        if (podeMirar != null)
            EditorGUILayout.PropertyField(podeMirar, new GUIContent("Enable PodeMirar Sensor Logs"));
        if (podeEmbarcar != null)
            EditorGUILayout.PropertyField(podeEmbarcar, new GUIContent("Enable PodeEmbarcar Sensor Logs"));
        if (podeDesembarcar != null)
            EditorGUILayout.PropertyField(podeDesembarcar, new GUIContent("Enable PodeDesembarcar Sensor Logs"));
        if (podeCapturar != null)
            EditorGUILayout.PropertyField(podeCapturar, new GUIContent("Enable PodeCapturar Sensor Logs"));
        if (podeFundir != null)
            EditorGUILayout.PropertyField(podeFundir, new GUIContent("Enable PodeFundir Sensor Logs"));
        if (podeSuprir != null)
            EditorGUILayout.PropertyField(podeSuprir, new GUIContent("Enable PodeSuprir Sensor Logs"));
        if (podeTransferir != null)
            EditorGUILayout.PropertyField(podeTransferir, new GUIContent("Enable PodeTransferir Sensor Logs"));
        if (servicoDoComando != null)
            EditorGUILayout.PropertyField(servicoDoComando, new GUIContent("Enable ServicoDoComando Sensor Logs"));
        if (podePousar != null)
            EditorGUILayout.PropertyField(podePousar, new GUIContent("Enable PodePousar Sensor Logs"));
        if (podeDecolar != null)
            EditorGUILayout.PropertyField(podeDecolar, new GUIContent("Enable PodeDecolar Sensor Logs"));
        if (aindaMeVe != null)
            EditorGUILayout.PropertyField(aindaMeVe, new GUIContent("Enable AindaMeVe Runtime Logs"));
        if (podeDetectar != null)
            EditorGUILayout.PropertyField(podeDetectar, new GUIContent("Enable PodeDetectar Runtime Logs"));
        if (podeEnxergar != null)
            EditorGUILayout.PropertyField(podeEnxergar, new GUIContent("Enable PodeEnxergar Runtime Logs"));

        matchSo.ApplyModifiedProperties();
    }
}
