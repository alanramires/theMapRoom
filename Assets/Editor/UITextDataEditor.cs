using UnityEditor;

[CustomEditor(typeof(UITextData))]
[CanEditMultipleObjects]
public sealed class UITextDataEditor : Editor
{
    public override void OnInspectorGUI() => PanelMessageDataInspector.Draw(serializedObject);
}
