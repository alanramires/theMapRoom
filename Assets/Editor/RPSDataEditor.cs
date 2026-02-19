using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RPSData))]
public class RPSDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty entriesProp = serializedObject.FindProperty("entries");
        if (entriesProp == null)
        {
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.LabelField("RPS Entries", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Cada entrada tem dois blocos: Chave Ataque e Chave Defesa.", MessageType.Info);

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
            if (entryProp == null)
                continue;

            SerializedProperty attackProp = entryProp.FindPropertyRelative("ataque");
            SerializedProperty defenseProp = entryProp.FindPropertyRelative("defesa");
            string label = BuildEntryLabel(attackProp);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            entryProp.isExpanded = EditorGUILayout.Foldout(entryProp.isExpanded, $"{i + 1}. {label}", true);
            if (GUILayout.Button("▲", GUILayout.Width(26f)) && i > 0)
            {
                entriesProp.MoveArrayElement(i, i - 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            if (GUILayout.Button("▼", GUILayout.Width(26f)) && i < entriesProp.arraySize - 1)
            {
                entriesProp.MoveArrayElement(i, i + 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            if (GUILayout.Button("X", GUILayout.Width(26f)))
            {
                entriesProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (entryProp.isExpanded)
            {
                MirrorDefenseKeyFromAttack(attackProp, defenseProp);
                DrawAttackBlock(attackProp);
                EditorGUILayout.Space(4f);
                DrawDefenseBlock(defenseProp);
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(6f);
        if (GUILayout.Button("Adicionar Entrada"))
            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawAttackBlock(SerializedProperty attackProp)
    {
        if (attackProp == null)
            return;

        EditorGUILayout.LabelField("Chave Ataque", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(attackProp.FindPropertyRelative("unitClass"), new GUIContent("Unit Class"));
        EditorGUILayout.PropertyField(attackProp.FindPropertyRelative("weaponCategory"), new GUIContent("Weapon Category"));
        EditorGUILayout.PropertyField(attackProp.FindPropertyRelative("targetClass"), new GUIContent("Target Class"));
        EditorGUILayout.PropertyField(attackProp.FindPropertyRelative("attackBonus"), new GUIContent("Attack Bonus"));
        EditorGUILayout.PropertyField(attackProp.FindPropertyRelative("notes"), new GUIContent("Notes"));

        SerializedProperty textProp = attackProp.FindPropertyRelative("rpsAttackText");
        if (textProp != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(textProp, new GUIContent("RPS Attack Text"));
        }
    }

    private static void DrawDefenseBlock(SerializedProperty defenseProp)
    {
        if (defenseProp == null)
            return;

        EditorGUILayout.LabelField("Chave Defesa", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(defenseProp.FindPropertyRelative("targetClass"), new GUIContent("Target Class"));
        EditorGUILayout.PropertyField(defenseProp.FindPropertyRelative("unitClass"), new GUIContent("Unit Class"));
        EditorGUILayout.PropertyField(defenseProp.FindPropertyRelative("weaponCategory"), new GUIContent("Weapon Category"));
        EditorGUILayout.PropertyField(defenseProp.FindPropertyRelative("defenseBonus"), new GUIContent("Defense Bonus"));
        EditorGUILayout.PropertyField(defenseProp.FindPropertyRelative("notes"), new GUIContent("Notes"));

        SerializedProperty textProp = defenseProp.FindPropertyRelative("rpsDefenseText");
        if (textProp != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(textProp, new GUIContent("RPS Defense Text"));
        }
    }

    private static void MirrorDefenseKeyFromAttack(SerializedProperty attackProp, SerializedProperty defenseProp)
    {
        if (attackProp == null || defenseProp == null)
            return;

        SerializedProperty atkUnitClass = attackProp.FindPropertyRelative("unitClass");
        SerializedProperty atkWeaponCategory = attackProp.FindPropertyRelative("weaponCategory");
        SerializedProperty atkTargetClass = attackProp.FindPropertyRelative("targetClass");

        SerializedProperty defUnitClass = defenseProp.FindPropertyRelative("unitClass");
        SerializedProperty defWeaponCategory = defenseProp.FindPropertyRelative("weaponCategory");
        SerializedProperty defTargetClass = defenseProp.FindPropertyRelative("targetClass");

        CopyEnumValue(atkUnitClass, defUnitClass);
        CopyEnumValue(atkWeaponCategory, defWeaponCategory);
        CopyEnumValue(atkTargetClass, defTargetClass);
    }

    private static void CopyEnumValue(SerializedProperty source, SerializedProperty target)
    {
        if (source == null || target == null)
            return;
        if (source.propertyType != SerializedPropertyType.Enum || target.propertyType != SerializedPropertyType.Enum)
            return;
        if (source.enumValueIndex == target.enumValueIndex)
            return;

        target.enumValueIndex = source.enumValueIndex;
    }

    private static string BuildEntryLabel(SerializedProperty attackProp)
    {
        if (attackProp == null)
            return "Entry";

        string attacker = GetEnumDisplayName(attackProp.FindPropertyRelative("unitClass"));
        string weaponCategory = GetEnumDisplayName(attackProp.FindPropertyRelative("weaponCategory"));
        string target = GetEnumDisplayName(attackProp.FindPropertyRelative("targetClass"));
        return $"{attacker} [{weaponCategory}] vs {target}";
    }

    private static string GetEnumDisplayName(SerializedProperty prop)
    {
        if (prop == null || prop.propertyType != SerializedPropertyType.Enum)
            return "-";

        int idx = prop.enumValueIndex;
        if (idx < 0 || idx >= prop.enumDisplayNames.Length)
            return "-";

        return prop.enumDisplayNames[idx];
    }
}
