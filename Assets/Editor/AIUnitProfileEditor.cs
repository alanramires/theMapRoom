using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIUnitProfile))]
public class AIUnitProfileEditor : Editor
{
    private SerializedProperty sensorPriorityProperty;
    private SerializedProperty allowCaptureProperty;
    private SerializedProperty allowAttackProperty;
    private SerializedProperty allowSupplyProperty;
    private SerializedProperty allowRepositionProperty;
    private SerializedProperty canEscortProperty;
    private SerializedProperty preferConservativeStanceProperty;

    private SerializedProperty minDamageDealtPercentProperty;
    private SerializedProperty maxDamageReceivedPercentProperty;
    private SerializedProperty mustSurviveProperty;
    private SerializedProperty targetPreferenceProperty;
    private SerializedProperty preferFallbackWhenDefendProperty;

    private SerializedProperty fuseWhileOnRepairModeProperty;
    private SerializedProperty hpRepairThresholdProperty;
    private SerializedProperty hpRepairExitThresholdProperty;
    private SerializedProperty repairAutonomyThresholdPercentProperty;
    private SerializedProperty repairWhenAnyWeaponOutOfAmmoProperty;

    private SerializedProperty restockFuelThresholdPercentProperty;
    private SerializedProperty restockAmmoThresholdPercentProperty;
    private SerializedProperty restockPartsThresholdPercentProperty;

    private SerializedProperty supplyAllyFuelThresholdPercentProperty;
    private SerializedProperty supplyAllyAmmoThresholdPercentProperty;
    private SerializedProperty supplyAllyHpThresholdPercentProperty;

    private static readonly AIUnitSensorKind[] AllSensors = new[]
    {
        AIUnitSensorKind.Capture,
        AIUnitSensorKind.Attack,
        AIUnitSensorKind.Supply,
        AIUnitSensorKind.Reposition,
    };

    private void OnEnable()
    {
        sensorPriorityProperty       = serializedObject.FindProperty("sensorPriority");
        allowCaptureProperty         = serializedObject.FindProperty("allowCapture");
        allowAttackProperty          = serializedObject.FindProperty("allowAttack");
        allowSupplyProperty          = serializedObject.FindProperty("allowSupply");
        allowRepositionProperty      = serializedObject.FindProperty("allowReposition");
        canEscortProperty            = serializedObject.FindProperty("canEscort");
        preferConservativeStanceProperty     = serializedObject.FindProperty("preferConservativeStance");

        minDamageDealtPercentProperty    = serializedObject.FindProperty("minDamageDealtPercent");
        maxDamageReceivedPercentProperty = serializedObject.FindProperty("maxDamageReceivedPercent");
        mustSurviveProperty              = serializedObject.FindProperty("mustSurvive");
        targetPreferenceProperty         = serializedObject.FindProperty("targetPreference");
        preferFallbackWhenDefendProperty = serializedObject.FindProperty("preferFallbackWhenDefend");

        fuseWhileOnRepairModeProperty            = serializedObject.FindProperty("fuseWhileOnRepairMode");
        hpRepairThresholdProperty                = serializedObject.FindProperty("hpRepairThreshold");
        hpRepairExitThresholdProperty            = serializedObject.FindProperty("hpRepairExitThreshold");
        repairAutonomyThresholdPercentProperty   = serializedObject.FindProperty("repairAutonomyThresholdPercent");
        repairWhenAnyWeaponOutOfAmmoProperty     = serializedObject.FindProperty("repairWhenAnyWeaponOutOfAmmo");

        restockFuelThresholdPercentProperty  = serializedObject.FindProperty("restockFuelThresholdPercent");
        restockAmmoThresholdPercentProperty  = serializedObject.FindProperty("restockAmmoThresholdPercent");
        restockPartsThresholdPercentProperty = serializedObject.FindProperty("restockPartsThresholdPercent");

        supplyAllyFuelThresholdPercentProperty = serializedObject.FindProperty("supplyAllyFuelThresholdPercent");
        supplyAllyAmmoThresholdPercentProperty = serializedObject.FindProperty("supplyAllyAmmoThresholdPercent");
        supplyAllyHpThresholdPercentProperty   = serializedObject.FindProperty("supplyAllyHpThresholdPercent");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((AIUnitProfile)target), typeof(MonoScript), false);
        EditorGUI.EndDisabledGroup();

        // ── Sensor Priority ──────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Sensor Priority", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Ordem de execucao dos sensores. O primeiro que encontrar um objetivo valido vence.", MessageType.None);

        DrawSensorPriorityList();

        // ── Sensor Toggles ───────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Sensors Permitidos", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(allowCaptureProperty,    new GUIContent("Allow Capture"));
        EditorGUILayout.PropertyField(allowAttackProperty,     new GUIContent("Allow Attack"));
        EditorGUILayout.PropertyField(allowSupplyProperty,     new GUIContent("Allow Supply"));
        EditorGUILayout.PropertyField(allowRepositionProperty, new GUIContent("Allow Reposition"));

        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(canEscortProperty,        new GUIContent("Can Escort"));
        EditorGUILayout.PropertyField(preferConservativeStanceProperty, new GUIContent("Prefer Conservative Stance"));

        // ── Attack Decision ───────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Attack Decision", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(minDamageDealtPercentProperty,    new GUIContent("Min Damage Dealt %"));
        EditorGUILayout.PropertyField(maxDamageReceivedPercentProperty, new GUIContent("Max Damage Received %"));
        EditorGUILayout.PropertyField(mustSurviveProperty,              new GUIContent("Must Survive"));
        EditorGUILayout.PropertyField(targetPreferenceProperty,         new GUIContent("Target Preference"));
        EditorGUILayout.PropertyField(preferFallbackWhenDefendProperty, new GUIContent("Prefer Fallback When Defend"));

        // ── Return to Base / Repair ───────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Return to Base / Repair", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hpRepairThresholdProperty,              new GUIContent("HP Repair Threshold"));
        EditorGUILayout.PropertyField(hpRepairExitThresholdProperty,           new GUIContent("HP Repair Exit Threshold"));
        EditorGUILayout.PropertyField(repairAutonomyThresholdPercentProperty,  new GUIContent("Autonomy Repair Threshold %"));
        EditorGUILayout.PropertyField(repairWhenAnyWeaponOutOfAmmoProperty,    new GUIContent("Repair When Any Weapon Out of Ammo"));
        EditorGUILayout.PropertyField(fuseWhileOnRepairModeProperty,           new GUIContent("Fuse with Nearby Units While Repairing"));

        // ── Restock Decision ─────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Restock Decision", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Limiares da carroceria para voltar a base e reabastecer via transferencia. 0 = apenas quando zerado.", MessageType.None);
        EditorGUILayout.PropertyField(restockFuelThresholdPercentProperty,  new GUIContent("Galoes %"));
        EditorGUILayout.PropertyField(restockAmmoThresholdPercentProperty,  new GUIContent("Caixas %"));
        EditorGUILayout.PropertyField(restockPartsThresholdPercentProperty, new GUIContent("Pecas %"));

        // ── Supply Decision ───────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Supply Decision", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Limiares para considerar um aliado como alvo de suprimento. Usado apenas por unidades com allowSupply = true.", MessageType.None);
        EditorGUILayout.PropertyField(supplyAllyFuelThresholdPercentProperty, new GUIContent("Refuel Ally Threshold %"));
        EditorGUILayout.PropertyField(supplyAllyAmmoThresholdPercentProperty, new GUIContent("Rearm Ally Threshold %"));
        EditorGUILayout.PropertyField(supplyAllyHpThresholdPercentProperty,   new GUIContent("Repair Ally Threshold %"));

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSensorPriorityList()
    {
        int count = sensorPriorityProperty.arraySize;

        for (int i = 0; i < count; i++)
        {
            SerializedProperty element = sensorPriorityProperty.GetArrayElementAtIndex(i);
            AIUnitSensorKind kind = (AIUnitSensorKind)element.enumValueIndex;

            EditorGUILayout.BeginHorizontal();

            // Badge numerado
            EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(20f));
            EditorGUILayout.LabelField(kind.ToString(), EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Subir
            EditorGUI.BeginDisabledGroup(i == 0);
            if (GUILayout.Button("▲", GUILayout.Width(26f)))
            {
                sensorPriorityProperty.MoveArrayElement(i, i - 1);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            EditorGUI.EndDisabledGroup();

            // Descer
            EditorGUI.BeginDisabledGroup(i == count - 1);
            if (GUILayout.Button("▼", GUILayout.Width(26f)))
            {
                sensorPriorityProperty.MoveArrayElement(i, i + 1);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            EditorGUI.EndDisabledGroup();

            // Remover
            if (GUILayout.Button("✕", GUILayout.Width(26f)))
            {
                sensorPriorityProperty.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.EndHorizontal();
        }

        // Botoes para adicionar sensores ausentes
        bool anyMissing = false;
        foreach (AIUnitSensorKind kind in AllSensors)
        {
            if (IsInList(kind)) continue;
            if (!anyMissing)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Adicionar:", EditorStyles.miniLabel);
                anyMissing = true;
            }
            if (GUILayout.Button($"+ {kind}", GUILayout.Height(20f)))
            {
                int idx = sensorPriorityProperty.arraySize;
                sensorPriorityProperty.InsertArrayElementAtIndex(idx);
                sensorPriorityProperty.GetArrayElementAtIndex(idx).enumValueIndex = (int)kind;
                serializedObject.ApplyModifiedProperties();
                return;
            }
        }

        if (count == 0)
            EditorGUILayout.HelpBox("Lista vazia — a unidade nao executara nenhum sensor.", MessageType.Warning);
    }

    private bool IsInList(AIUnitSensorKind kind)
    {
        for (int i = 0; i < sensorPriorityProperty.arraySize; i++)
        {
            if ((AIUnitSensorKind)sensorPriorityProperty.GetArrayElementAtIndex(i).enumValueIndex == kind)
                return true;
        }
        return false;
    }
}
