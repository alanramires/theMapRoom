using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatModifierData))]
public class CombatModifierDataEditor : Editor
{
    private readonly System.Collections.Generic.List<UnitData> equippedByUnits = new System.Collections.Generic.List<UnitData>();
    private bool equippedScanDone;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIdentity();
        DrawFilters();
        DrawEliteComparison();
        DrawRpsModifiers();
        DrawEquippedByReadOnly();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawIdentity()
    {
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        DrawProperty("id");
        DrawProperty("displayName");
        DrawProperty("description");
        EditorGUILayout.Space(6f);
    }

    private void DrawFilters()
    {
        EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
        DrawProperty("modifierType", "Modifier Type");
        SerializedProperty modifierTypeProp = serializedObject.FindProperty("modifierType");
        if (modifierTypeProp != null)
        {
            CombatModifierType modifierType = (CombatModifierType)modifierTypeProp.enumValueIndex;
            if (modifierType == CombatModifierType.Attack)
            {
                EditorGUILayout.HelpBox(
                    "Modifier Type: Attack\nYou must face: (class)\nWith the weapon: (weapon)",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Modifier Type: Defense\nYou must face: (class)\nAnd he must attack you with: (weapon)",
                    MessageType.None);
            }
        }
        DrawProperty("requiredOpponentClass", "You Must Face");
        DrawProperty("requiredWeaponCategory", "Weapon Condition");
        EditorGUILayout.Space(6f);
    }

    private void DrawEliteComparison()
    {
        SerializedProperty modeProp = serializedObject.FindProperty("eliteComparison");
        SerializedProperty diffProp = serializedObject.FindProperty("minEliteDifference");

        EditorGUILayout.LabelField("Comparison", EditorStyles.boldLabel);

        if (modeProp != null)
        {
            string[] labels =
            {
                "Ignore",
                "Owner > Opponent",
                "Owner < Opponent",
                "Owner != Opponent",
                "Owner == Opponent",
                "Owner <= Opponent",
                "Owner >= Opponent"
            };

            int[] values =
            {
                (int)CombatEliteComparisonMode.Ignore,
                (int)CombatEliteComparisonMode.AttackerGreater,
                (int)CombatEliteComparisonMode.DefenderGreater,
                (int)CombatEliteComparisonMode.Different,
                (int)CombatEliteComparisonMode.Equal,
                (int)CombatEliteComparisonMode.OwnerLessOrEqual,
                (int)CombatEliteComparisonMode.OwnerGreaterOrEqual
            };

            int current = modeProp.enumValueIndex;
            int selected = IndexOfValue(values, current);
            if (selected < 0) selected = 0;
            selected = EditorGUILayout.Popup("Activated When", selected, labels);
            modeProp.enumValueIndex = values[selected];
        }

        if (diffProp != null)
        {
            using (new EditorGUI.DisabledScope(modeProp != null && (CombatEliteComparisonMode)modeProp.enumValueIndex == CombatEliteComparisonMode.Equal))
                EditorGUILayout.PropertyField(diffProp, new GUIContent("With Min Difference"));
        }

        EditorGUILayout.Space(6f);
    }

    private void DrawRpsModifiers()
    {
        EditorGUILayout.LabelField("Elite Modifiers", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("You Get", EditorStyles.miniBoldLabel);
        DrawProperty("ownerAttackRpsModifier", "FA");
        DrawProperty("ownerDefenseRpsModifier", "FD");
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("The Opponent Get", EditorStyles.miniBoldLabel);
        DrawProperty("opponentAttackRpsModifier", "FA");
        DrawProperty("opponentDefenseRpsModifier", "FD");
        EditorGUILayout.Space(6f);
    }

    private void DrawEquippedByReadOnly()
    {
        if (!equippedScanDone)
            RefreshEquippedByCache();

        EditorGUILayout.LabelField("Read Only: Equipped By", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Units Found: {equippedByUnits.Count}", EditorStyles.miniLabel);
        if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
            RefreshEquippedByCache();
        EditorGUILayout.EndHorizontal();

        if (equippedByUnits.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma UnitData equipada com este Combat Modifier.", MessageType.Info);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            for (int i = 0; i < equippedByUnits.Count; i++)
            {
                UnitData unit = equippedByUnits[i];
                if (unit == null)
                    continue;

                string label = string.IsNullOrWhiteSpace(unit.displayName)
                    ? unit.id
                    : $"{unit.displayName} [{unit.id}]";
                EditorGUILayout.ObjectField(label, unit, typeof(UnitData), false);
            }
        }
    }

    private void RefreshEquippedByCache()
    {
        equippedByUnits.Clear();
        equippedScanDone = true;

        CombatModifierData current = target as CombatModifierData;
        if (current == null)
            return;

        string[] guids = AssetDatabase.FindAssets("t:UnitData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (unit == null || unit.combatModifiers == null)
                continue;

            for (int j = 0; j < unit.combatModifiers.Count; j++)
            {
                if (unit.combatModifiers[j] != current)
                    continue;

                equippedByUnits.Add(unit);
                break;
            }
        }
    }

    private void DrawProperty(string name, string label = null)
    {
        SerializedProperty p = serializedObject.FindProperty(name);
        if (p == null)
            return;

        if (string.IsNullOrWhiteSpace(label))
            EditorGUILayout.PropertyField(p, true);
        else
            EditorGUILayout.PropertyField(p, new GUIContent(label), true);
    }

    private static int IndexOfValue(int[] values, int value)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == value)
                return i;
        }

        return -1;
    }
}
