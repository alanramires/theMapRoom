using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MatchController))]
public class MatchControllerEditor : Editor
{
    private SerializedProperty currentTurnProp;
    private SerializedProperty activeTeamIdProp;
    private SerializedProperty playersProp;
    private SerializedProperty includeNeutralTeamProp;
    private SerializedProperty gameSetupProp;
    private SerializedProperty enableLdtValidationProp;
    private SerializedProperty enableLosValidationProp;
    private SerializedProperty enableSpotterProp;
    private SerializedProperty enableStealthValidationProp;
    private SerializedProperty autonomyDatabaseProp;
    private SerializedProperty activePlayerListIndexProp;
    private SerializedProperty matchMusicAudioManagerProp;
    private SerializedProperty advanceTurnSfxProp;
    private SerializedProperty advanceTurnPreDelayProp;
    private SerializedProperty advanceTurnPostDelayProp;
    private SerializedProperty advanceTurnSfxVolumeProp;

    private void OnEnable()
    {
        currentTurnProp = serializedObject.FindProperty("currentTurn");
        activeTeamIdProp = serializedObject.FindProperty("activeTeamId");
        playersProp = serializedObject.FindProperty("players");
        includeNeutralTeamProp = serializedObject.FindProperty("includeNeutralTeam");
        gameSetupProp = serializedObject.FindProperty("gameSetup");
        enableLdtValidationProp = serializedObject.FindProperty("enableLdtValidation");
        enableLosValidationProp = serializedObject.FindProperty("enableLosValidation");
        enableSpotterProp = serializedObject.FindProperty("enableSpotter");
        enableStealthValidationProp = serializedObject.FindProperty("enableStealthValidation");
        autonomyDatabaseProp = serializedObject.FindProperty("autonomyDatabase");
        activePlayerListIndexProp = serializedObject.FindProperty("activePlayerListIndex");
        matchMusicAudioManagerProp = serializedObject.FindProperty("matchMusicAudioManager");
        advanceTurnSfxProp = serializedObject.FindProperty("advanceTurnSfx");
        advanceTurnPreDelayProp = serializedObject.FindProperty("advanceTurnPreDelay");
        advanceTurnPostDelayProp = serializedObject.FindProperty("advanceTurnPostDelay");
        advanceTurnSfxVolumeProp = serializedObject.FindProperty("advanceTurnSfxVolume");
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
        if (gameSetupProp != null)
            EditorGUILayout.PropertyField(gameSetupProp, new GUIContent("Game Setup"));
        using (new EditorGUI.DisabledScope(true))
        {
            if (enableLdtValidationProp != null)
                EditorGUILayout.PropertyField(enableLdtValidationProp, new GUIContent("LdT"));
            if (enableLosValidationProp != null)
                EditorGUILayout.PropertyField(enableLosValidationProp, new GUIContent("LoS"));
            if (enableSpotterProp != null)
                EditorGUILayout.PropertyField(enableSpotterProp, new GUIContent("Spotter"));
            if (enableStealthValidationProp != null)
                EditorGUILayout.PropertyField(enableStealthValidationProp, new GUIContent("Stealth"));
        }
        if (autonomyDatabaseProp != null)
            EditorGUILayout.PropertyField(autonomyDatabaseProp, new GUIContent("Autonomy Database"));
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(activePlayerListIndexProp, new GUIContent("Active Player List Index"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Turn Transition", EditorStyles.boldLabel);
        if (matchMusicAudioManagerProp != null)
            EditorGUILayout.PropertyField(matchMusicAudioManagerProp, new GUIContent("Match Music Audio Manager"));
        if (advanceTurnSfxProp != null)
            EditorGUILayout.PropertyField(advanceTurnSfxProp, new GUIContent("Advance Turn Sfx"));
        if (advanceTurnPreDelayProp != null)
            EditorGUILayout.PropertyField(advanceTurnPreDelayProp, new GUIContent("Advance Turn Pre Delay"));
        if (advanceTurnPostDelayProp != null)
            EditorGUILayout.PropertyField(advanceTurnPostDelayProp, new GUIContent("Advance Turn Post Delay"));
        if (advanceTurnSfxVolumeProp != null)
            EditorGUILayout.PropertyField(advanceTurnSfxVolumeProp, new GUIContent("Advance Turn Sfx Volume"));

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
