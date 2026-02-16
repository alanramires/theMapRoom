using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MatchController))]
public class MatchControllerEditor : Editor
{
    private SerializedProperty currentTurnProp;
    private SerializedProperty activeTeamIdProp;
    private SerializedProperty playersProp;
    private SerializedProperty includeNeutralTeamProp;
    private SerializedProperty activePlayerListIndexProp;

    private void OnEnable()
    {
        currentTurnProp = serializedObject.FindProperty("currentTurn");
        activeTeamIdProp = serializedObject.FindProperty("activeTeamId");
        playersProp = serializedObject.FindProperty("players");
        includeNeutralTeamProp = serializedObject.FindProperty("includeNeutralTeam");
        activePlayerListIndexProp = serializedObject.FindProperty("activePlayerListIndex");
    }

    public override void OnInspectorGUI()
    {
        if (currentTurnProp == null || activeTeamIdProp == null || playersProp == null || includeNeutralTeamProp == null || activePlayerListIndexProp == null)
        {
            EditorGUILayout.HelpBox("MatchControllerEditor: propriedades nao encontradas. Usando inspector padrao.", MessageType.Warning);
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();

        EditorGUILayout.LabelField("Match State", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(currentTurnProp, new GUIContent("Current Turn"));
        EditorGUILayout.PropertyField(activeTeamIdProp, new GUIContent("Active Team ID"));
        EditorGUILayout.PropertyField(playersProp, new GUIContent("Players"), true);
        EditorGUILayout.PropertyField(includeNeutralTeamProp, new GUIContent("Include Neutral Team"));
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(activePlayerListIndexProp, new GUIContent("Active Player List Index"));

        serializedObject.ApplyModifiedProperties();

        MatchController match = (MatchController)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Advance Turn: avanca para o proximo player da lista. So incrementa Current Turn ao fechar ciclo.\nSe Include Neutral Team estiver ativo, passa por Neutral antes de fechar ciclo.", MessageType.Info);

        if (GUILayout.Button("Advance Turn"))
        {
            Undo.RecordObject(match, "Advance Turn");
            match.AdvanceTurn();
            EditorUtility.SetDirty(match);
        }

        if (GUILayout.Button("Advance Team (Debug)"))
        {
            Undo.RecordObject(match, "Advance Team");
            match.AdvanceTeam();
            EditorUtility.SetDirty(match);
        }

        TeamId active = match.ActiveTeam;
        string cursorLabel = active == TeamId.Neutral
            ? "Neutral"
            : $"{active} (Team {(int)active})";
        EditorGUILayout.LabelField("Cursor", cursorLabel);
    }
}
