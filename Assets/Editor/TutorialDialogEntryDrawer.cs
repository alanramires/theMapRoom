using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TutorialDialogEntry))]
public sealed class TutorialDialogEntryDrawer : PropertyDrawer
{
    private static readonly string[] AlwaysVisibleProperties =
    {
        "text",
        "voice",
        "spawnCommand",
        "statCommand",
        "turn",
        "movement"
    };

    private const float EntryButtonWidth = 24f;
    private const int EntryButtonCount = 4;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float buttonsWidth = EntryButtonWidth * EntryButtonCount + 4f;
        Rect line = new Rect(position.x, position.y, position.width - buttonsWidth, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

        // Controles por fala (mover/duplicar/remover) sem precisar arrastar pro fim da lista.
        DrawEntryControls(
            new Rect(line.xMax + 4f, position.y, buttonsWidth - 4f, EditorGUIUtility.singleLineHeight),
            property);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;

        DrawProperty(ref y, position, property.FindPropertyRelative("advance"));

        SerializedProperty reveal = property.FindPropertyRelative("revealObjective");
        DrawProperty(ref y, position, reveal);

        SerializedProperty advance = property.FindPropertyRelative("advance");
        bool needsObjective = reveal.boolValue ||
            advance.enumValueIndex == (int)TutorialAdvanceCondition.ObjectiveCompleted;
        if (needsObjective)
            DrawProperty(ref y, position, property.FindPropertyRelative("objectiveKey"));

        for (int i = 0; i < AlwaysVisibleProperties.Length; i++)
            DrawProperty(ref y, position, property.FindPropertyRelative(AlwaysVisibleProperties[i]));

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;

        height += EditorGUIUtility.standardVerticalSpacing;
        height += PropertyHeight(property.FindPropertyRelative("advance"));

        SerializedProperty reveal = property.FindPropertyRelative("revealObjective");
        height += PropertyHeight(reveal);

        SerializedProperty advance = property.FindPropertyRelative("advance");
        if (reveal.boolValue || advance.enumValueIndex == (int)TutorialAdvanceCondition.ObjectiveCompleted)
            height += PropertyHeight(property.FindPropertyRelative("objectiveKey"));

        for (int i = 0; i < AlwaysVisibleProperties.Length; i++)
            height += PropertyHeight(property.FindPropertyRelative(AlwaysVisibleProperties[i]));

        return height;
    }

    // ▲/▼ movem, ＋ duplica logo abaixo, ✕ remove. As operacoes rodam via delayCall
    // para nao mutar o array no meio do layout do IMGUI (evita mismatch de GUI).
    private static void DrawEntryControls(Rect rect, SerializedProperty property)
    {
        if (!TryResolveArrayContext(property, out SerializedProperty array, out int index))
            return;

        float w = rect.width / EntryButtonCount;
        Rect up = new Rect(rect.x, rect.y, w, rect.height);
        Rect down = new Rect(rect.x + w, rect.y, w, rect.height);
        Rect dup = new Rect(rect.x + w * 2f, rect.y, w, rect.height);
        Rect del = new Rect(rect.x + w * 3f, rect.y, w, rect.height);

        string arrayPath = array.propertyPath;
        SerializedObject so = property.serializedObject;

        using (new EditorGUI.DisabledScope(index <= 0))
        {
            if (GUI.Button(up, "▲"))
                DeferArrayOp(so, arrayPath, arr => arr.MoveArrayElement(index, index - 1));
        }

        using (new EditorGUI.DisabledScope(index >= array.arraySize - 1))
        {
            if (GUI.Button(down, "▼"))
                DeferArrayOp(so, arrayPath, arr => arr.MoveArrayElement(index, index + 1));
        }

        if (GUI.Button(dup, "+"))
            DeferArrayOp(so, arrayPath, arr => arr.InsertArrayElementAtIndex(index));

        if (GUI.Button(del, "✕"))
            DeferArrayOp(so, arrayPath, arr => arr.DeleteArrayElementAtIndex(index));
    }

    private static void DeferArrayOp(SerializedObject so, string arrayPath, System.Action<SerializedProperty> operation)
    {
        EditorApplication.delayCall += () =>
        {
            if (so == null || so.targetObject == null)
                return;

            so.Update();
            SerializedProperty array = so.FindProperty(arrayPath);
            if (array == null || !array.isArray)
                return;

            operation(array);
            so.ApplyModifiedProperties();
        };
    }

    // Resolve "script.Array.data[5]" -> (array "script", index 5).
    private static bool TryResolveArrayContext(SerializedProperty property, out SerializedProperty array, out int index)
    {
        array = null;
        index = -1;

        string path = property.propertyPath;
        int marker = path.LastIndexOf(".Array.data[", System.StringComparison.Ordinal);
        if (marker < 0)
            return false;

        int start = marker + ".Array.data[".Length;
        int end = path.IndexOf(']', start);
        if (end < 0 || !int.TryParse(path.Substring(start, end - start), out index))
            return false;

        array = property.serializedObject.FindProperty(path.Substring(0, marker));
        return array != null && array.isArray;
    }

    private static void DrawProperty(ref float y, Rect position, SerializedProperty property)
    {
        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect rect = new Rect(position.x, y, position.width, height);
        EditorGUI.PropertyField(rect, property, true);
        y += height + EditorGUIUtility.standardVerticalSpacing;
    }

    private static float PropertyHeight(SerializedProperty property)
    {
        return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
    }
}
