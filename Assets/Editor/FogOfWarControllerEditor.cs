using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FogOfWarController))]
public class FogOfWarControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script");

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        FogOfWarController controller = (FogOfWarController)target;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild Snapshot", GUILayout.Height(28f)))
            {
                controller.RebuildSnapshot();
                EditorUtility.SetDirty(controller);
            }

            if (GUILayout.Button("Dump Console", GUILayout.Height(28f)))
            {
                controller.DumpSnapshotToConsole();
            }

            if (GUILayout.Button("Clear", GUILayout.Height(28f)))
            {
                controller.ClearSnapshot();
                EditorUtility.SetDirty(controller);
            }
        }

        EditorGUILayout.Space(6f);
        if (controller.Snapshot != null)
        {
            EditorGUILayout.LabelField($"Rows: {controller.Snapshot.Count}", EditorStyles.boldLabel);
            for (int i = 0; i < controller.Snapshot.Count; i++)
            {
                FogOfWarController.FogUnitContributorsView row = controller.Snapshot[i];
                if (row == null)
                    continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{i + 1}. {row.targetUnit}");
                EditorGUILayout.LabelField($"ID: {row.targetInstanceId}");
                EditorGUILayout.LabelField($"Visible: {row.visibleForActiveTeam}");
                EditorGUILayout.LabelField($"Contributors: {row.contributorsCount}");
                if (!string.IsNullOrWhiteSpace(row.contributors))
                    EditorGUILayout.HelpBox(row.contributors, MessageType.None);
                EditorGUILayout.EndVertical();
            }
        }
    }
}
