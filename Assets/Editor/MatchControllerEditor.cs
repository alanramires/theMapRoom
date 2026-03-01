using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MatchController))]
public class MatchControllerEditor : Editor
{
    private SerializedProperty currentTurnProp;
    private SerializedProperty activeTeamIdProp;
    private SerializedProperty playersProp;
    private SerializedProperty includeNeutralTeamProp;
    private SerializedProperty teamFlipConfigsProp;
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
        teamFlipConfigsProp = serializedObject.FindProperty("teamFlipConfigs");
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
        DrawPlayersWithFlipX();
        DrawPlayersRecoveryTools();
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

    private void DrawPlayersWithFlipX()
    {
        if (playersProp == null || teamFlipConfigsProp == null)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Players", EditorStyles.boldLabel);

        if (playersProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("Sem players na lista atual.", MessageType.None);
        }
        else
        {
            EditorGUILayout.BeginVertical("box");
            for (int i = 0; i < playersProp.arraySize; i++)
            {
                SerializedProperty playerProp = playersProp.GetArrayElementAtIndex(i);
                if (playerProp == null)
                    continue;

                TeamId team = GetTeamFromEnumProp(playerProp);
                int configIndex = EnsureTeamFlipConfigEntry(team);
                SerializedProperty configProp = configIndex >= 0 ? teamFlipConfigsProp.GetArrayElementAtIndex(configIndex) : null;
                SerializedProperty flipXProp = configProp != null ? configProp.FindPropertyRelative("flipX") : null;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Element {i}", GUILayout.Width(64f));
                TeamId newTeam = (TeamId)EditorGUILayout.EnumPopup(team, GUILayout.MinWidth(140f));
                if (newTeam != team)
                {
                    SetTeamToEnumProp(playerProp, newTeam);
                    team = newTeam;
                    configIndex = EnsureTeamFlipConfigEntry(team);
                    configProp = configIndex >= 0 ? teamFlipConfigsProp.GetArrayElementAtIndex(configIndex) : null;
                    flipXProp = configProp != null ? configProp.FindPropertyRelative("flipX") : null;
                }

                bool flipX = flipXProp != null && flipXProp.boolValue;
                bool newFlipX = EditorGUILayout.ToggleLeft("Flip X", flipX, GUILayout.Width(70f));
                if (flipXProp != null)
                    flipXProp.boolValue = newFlipX;

                if (GUILayout.Button("-", GUILayout.Width(22f)))
                {
                    playersProp.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add Player"))
        {
            int newIndex = playersProp.arraySize;
            playersProp.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newProp = playersProp.GetArrayElementAtIndex(newIndex);
            if (newProp != null)
                SetTeamToEnumProp(newProp, TeamId.Green);
            EnsureTeamFlipConfigEntry(TeamId.Green);
        }
    }

    private void DrawPlayersRecoveryTools()
    {
        MatchController match = target as MatchController;
        if (match == null)
            return;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Restore Default Teams (Green/Red)"))
        {
            Undo.RecordObject(match, "Restore Default Teams");
            SetPlayersToDefault();
            EditorUtility.SetDirty(match);
            serializedObject.Update();
        }

        if (GUILayout.Button("Add All Teams"))
        {
            Undo.RecordObject(match, "Add All Teams");
            SetPlayersToAll();
            EditorUtility.SetDirty(match);
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void SetPlayersToDefault()
    {
        if (playersProp == null)
            return;

        playersProp.arraySize = 0;
        AddPlayer(TeamId.Green);
        AddPlayer(TeamId.Red);
    }

    private void SetPlayersToAll()
    {
        if (playersProp == null)
            return;

        playersProp.arraySize = 0;
        AddPlayer(TeamId.Green);
        AddPlayer(TeamId.Red);
        AddPlayer(TeamId.Blue);
        AddPlayer(TeamId.Yellow);
    }

    private void AddPlayer(TeamId team)
    {
        int idx = playersProp.arraySize;
        playersProp.InsertArrayElementAtIndex(idx);
        SerializedProperty p = playersProp.GetArrayElementAtIndex(idx);
        if (p != null)
            SetTeamToEnumProp(p, team);
        EnsureTeamFlipConfigEntry(team);
    }


    private int EnsureTeamFlipConfigEntry(TeamId team)
    {
        if (teamFlipConfigsProp == null)
            return -1;

        for (int i = 0; i < teamFlipConfigsProp.arraySize; i++)
        {
            SerializedProperty configProp = teamFlipConfigsProp.GetArrayElementAtIndex(i);
            if (configProp == null)
                continue;

            SerializedProperty teamIdProp = configProp.FindPropertyRelative("teamId");
            if (teamIdProp != null && GetTeamFromEnumProp(teamIdProp) == team)
                return i;
        }

        int newIndex = teamFlipConfigsProp.arraySize;
        teamFlipConfigsProp.InsertArrayElementAtIndex(newIndex);
        SerializedProperty newProp = teamFlipConfigsProp.GetArrayElementAtIndex(newIndex);
        if (newProp == null)
            return -1;

        SerializedProperty newTeamId = newProp.FindPropertyRelative("teamId");
        SerializedProperty newFlipX = newProp.FindPropertyRelative("flipX");
        if (newTeamId != null)
            SetTeamToEnumProp(newTeamId, team);
        if (newFlipX != null)
            newFlipX.boolValue = false;

        return newIndex;
    }

    private static TeamId GetTeamFromEnumProp(SerializedProperty prop)
    {
        if (prop == null)
            return TeamId.Neutral;

        if (prop.propertyType == SerializedPropertyType.Enum)
        {
            string[] names = prop.enumNames;
            int idx = Mathf.Clamp(prop.enumValueIndex, 0, names.Length - 1);
            if (idx >= 0 && idx < names.Length)
            {
                if (System.Enum.TryParse(names[idx], out TeamId parsed))
                    return parsed;
            }
        }

        return (TeamId)prop.intValue;
    }

    private static void SetTeamToEnumProp(SerializedProperty prop, TeamId team)
    {
        if (prop == null)
            return;

        if (prop.propertyType == SerializedPropertyType.Enum)
        {
            string target = team.ToString();
            string[] names = prop.enumNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], target, System.StringComparison.Ordinal))
                {
                    prop.enumValueIndex = i;
                    return;
                }
            }
        }

        prop.intValue = (int)team;
    }
}
