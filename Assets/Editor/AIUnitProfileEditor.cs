using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIUnitProfile))]
public class AIUnitProfileEditor : Editor
{
    private SerializedProperty sensorPriorityProperty;
    private SerializedProperty allowCaptureProperty;
    private SerializedProperty allowAttackProperty;
    private SerializedProperty allowRepositionProperty;
    private SerializedProperty canEscortProperty;

    private SerializedProperty minDamageDealtPercentProperty;
    private SerializedProperty maxDamageReceivedPercentProperty;
    private SerializedProperty mustSurviveProperty;
    private SerializedProperty targetPreferenceProperty;
    private SerializedProperty preferFallbackWhenDefendProperty;

    private SerializedProperty hpRepairThresholdProperty;
    private SerializedProperty hpRepairExitThresholdProperty;

    private void OnEnable()
    {
        sensorPriorityProperty = serializedObject.FindProperty("sensorPriority");
        allowCaptureProperty = serializedObject.FindProperty("allowCapture");
        allowAttackProperty = serializedObject.FindProperty("allowAttack");
        allowRepositionProperty = serializedObject.FindProperty("allowReposition");
        canEscortProperty = serializedObject.FindProperty("canEscort");

        minDamageDealtPercentProperty = serializedObject.FindProperty("minDamageDealtPercent");
        maxDamageReceivedPercentProperty = serializedObject.FindProperty("maxDamageReceivedPercent");
        mustSurviveProperty = serializedObject.FindProperty("mustSurvive");
        targetPreferenceProperty = serializedObject.FindProperty("targetPreference");
        preferFallbackWhenDefendProperty = serializedObject.FindProperty("preferFallbackWhenDefend");

        hpRepairThresholdProperty = serializedObject.FindProperty("hpRepairThreshold");
        hpRepairExitThresholdProperty = serializedObject.FindProperty("hpRepairExitThreshold");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((AIUnitProfile)target), typeof(MonoScript), false);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(sensorPriorityProperty, new GUIContent("Sensor Priority"), true);

        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(allowCaptureProperty, new GUIContent("Allow Capture"));
        EditorGUILayout.PropertyField(allowAttackProperty, new GUIContent("Allow Attack"));
        EditorGUILayout.PropertyField(allowRepositionProperty, new GUIContent("Allow Reposition"));
        if (canEscortProperty != null)
        {
            EditorGUILayout.PropertyField(canEscortProperty, new GUIContent("Can Escort"));
        }
        else
        {
            EditorGUILayout.HelpBox("Campo 'canEscort' nao encontrado no serialized object. Force recompile/reload do Unity.", MessageType.Warning);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Attack Decision", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(minDamageDealtPercentProperty, new GUIContent("Min Damage Dealt Percent"));
        EditorGUILayout.PropertyField(maxDamageReceivedPercentProperty, new GUIContent("Max Damage Received Percent"));
        EditorGUILayout.PropertyField(mustSurviveProperty, new GUIContent("Must Survive"));
        EditorGUILayout.PropertyField(targetPreferenceProperty, new GUIContent("Target Preference"));
        EditorGUILayout.PropertyField(preferFallbackWhenDefendProperty, new GUIContent("Prefer Fallback When Defend"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Repair", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hpRepairThresholdProperty, new GUIContent("Hp Repair Threshold"));
        EditorGUILayout.PropertyField(hpRepairExitThresholdProperty, new GUIContent("Hp Repair Exit Threshold"));

        serializedObject.ApplyModifiedProperties();
    }
}
