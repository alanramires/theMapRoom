using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SaveGameManager))]
public class SaveGameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SaveGameManager manager = (SaveGameManager)target;
        if (manager == null)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Slots 1/2/3", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use os botoes abaixo para salvar, carregar ou limpar cada slot.", MessageType.Info);

        DrawSlotActions(manager, 1);
        DrawSlotActions(manager, 2);
        DrawSlotActions(manager, 3);
    }

    private static void DrawSlotActions(SaveGameManager manager, int slot)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Slot {slot}", GUILayout.Width(52f));

        if (GUILayout.Button("Salvar", GUILayout.Height(22f)))
            manager.SaveSlot(slot);

        if (GUILayout.Button("Carregar", GUILayout.Height(22f)))
            manager.LoadSlot(slot);

        if (GUILayout.Button("Limpar", GUILayout.Height(22f)))
            manager.ClearSlot(slot);

        EditorGUILayout.EndHorizontal();
    }
}
