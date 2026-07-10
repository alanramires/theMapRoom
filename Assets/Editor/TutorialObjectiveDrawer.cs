using UnityEditor;
using UnityEngine;

// Cabecalho dos elementos de Objectives no inspector: "hist_1_01 — CAMERA_ZOOM"
// (key + tipo de evento), em vez do "Element N"/id cru.
[CustomPropertyDrawer(typeof(TutorialObjective))]
public sealed class TutorialObjectiveDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty key = property.FindPropertyRelative("key");
        SerializedProperty id = property.FindPropertyRelative("id");

        string keyText = key != null ? key.stringValue : string.Empty;
        string idText = id != null ? id.stringValue : string.Empty;

        string title;
        if (!string.IsNullOrWhiteSpace(keyText) && !string.IsNullOrWhiteSpace(idText))
            title = $"{keyText} — {idText}";
        else if (!string.IsNullOrWhiteSpace(idText))
            title = idText;
        else if (!string.IsNullOrWhiteSpace(keyText))
            title = keyText;
        else
            title = label.text;

        EditorGUI.PropertyField(position, property, new GUIContent(title), true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, true);
    }
}
