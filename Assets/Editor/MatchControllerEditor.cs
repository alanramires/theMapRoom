using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MatchController))]
public class MatchControllerEditor : Editor
{
    private SerializedProperty currentTurnProp;
    private SerializedProperty activeTeamIdProp;
    private SerializedProperty playersProp;
    private SerializedProperty includeNeutralTeamProp;
    private SerializedProperty economyEnabledProp;
    private SerializedProperty gameSetupProp;
    private SerializedProperty enableLdtValidationProp;
    private SerializedProperty enableLosValidationProp;
    private SerializedProperty enableSpotterProp;
    private SerializedProperty enableStealthValidationProp;
    private SerializedProperty enableTotalWarProp;
    private SerializedProperty autonomyDatabaseProp;
    private SerializedProperty activePlayerListIndexProp;
    private SerializedProperty matchMusicAudioManagerProp;
    private SerializedProperty advanceTurnPreDelayProp;
    private SerializedProperty advanceTurnPostDelayProp;
    private SerializedProperty fogOfWarTilemapProp;
    private SerializedProperty fogOfWarOverlayTileProp;
    private SerializedProperty fogOfWarTerrainDatabaseProp;
    private SerializedProperty fogOfWarDpqAirHeightConfigProp;
    private SerializedProperty fogOfWarAlphaProp;

    private void OnEnable()
    {
        currentTurnProp = serializedObject.FindProperty("currentTurn");
        activeTeamIdProp = serializedObject.FindProperty("activeTeamId");
        playersProp = serializedObject.FindProperty("players");
        includeNeutralTeamProp = serializedObject.FindProperty("includeNeutralTeam");
        economyEnabledProp = serializedObject.FindProperty("economyEnabled");
        gameSetupProp = serializedObject.FindProperty("gameSetup");
        enableLdtValidationProp = serializedObject.FindProperty("enableLdtValidation");
        enableLosValidationProp = serializedObject.FindProperty("enableLosValidation");
        enableSpotterProp = serializedObject.FindProperty("enableSpotter");
        enableStealthValidationProp = serializedObject.FindProperty("enableStealthValidation");
        enableTotalWarProp = serializedObject.FindProperty("enableTotalWar");
        autonomyDatabaseProp = serializedObject.FindProperty("autonomyDatabase");
        activePlayerListIndexProp = serializedObject.FindProperty("activePlayerListIndex");
        matchMusicAudioManagerProp = serializedObject.FindProperty("matchMusicAudioManager");
        advanceTurnPreDelayProp = serializedObject.FindProperty("advanceTurnPreDelay");
        advanceTurnPostDelayProp = serializedObject.FindProperty("advanceTurnPostDelay");
        fogOfWarTilemapProp = serializedObject.FindProperty("fogOfWarTilemap");
        fogOfWarOverlayTileProp = serializedObject.FindProperty("fogOfWarOverlayTile");
        fogOfWarTerrainDatabaseProp = serializedObject.FindProperty("fogOfWarTerrainDatabase");
        fogOfWarDpqAirHeightConfigProp = serializedObject.FindProperty("fogOfWarDpqAirHeightConfig");
        fogOfWarAlphaProp = serializedObject.FindProperty("fogOfWarAlpha");
    }

    public override void OnInspectorGUI()
    {
        MatchController match = (MatchController)target;
        if (match != null)
            match.RefreshIncomeFromConstructionsNow();

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
        DrawPlayersList();
        DrawPlayersRecoveryTools();
        EditorGUILayout.PropertyField(includeNeutralTeamProp, new GUIContent("Include Neutral Team"));
        if (economyEnabledProp != null)
            EditorGUILayout.PropertyField(economyEnabledProp, new GUIContent("Economy Enabled"));
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
            if (enableTotalWarProp != null)
                EditorGUILayout.PropertyField(enableTotalWarProp, new GUIContent("Total War"));
        }
        if (autonomyDatabaseProp != null)
            EditorGUILayout.PropertyField(autonomyDatabaseProp, new GUIContent("Autonomy Database"));
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(activePlayerListIndexProp, new GUIContent("Active Player List Index"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Turn Transition", EditorStyles.boldLabel);
        if (matchMusicAudioManagerProp != null)
            EditorGUILayout.PropertyField(matchMusicAudioManagerProp, new GUIContent("Match Music Audio Manager"));
        if (advanceTurnPreDelayProp != null)
            EditorGUILayout.PropertyField(advanceTurnPreDelayProp, new GUIContent("Advance Turn Pre Delay"));
        if (advanceTurnPostDelayProp != null)
            EditorGUILayout.PropertyField(advanceTurnPostDelayProp, new GUIContent("Advance Turn Post Delay"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fog Of War", EditorStyles.boldLabel);
        if (fogOfWarTilemapProp != null)
            EditorGUILayout.PropertyField(fogOfWarTilemapProp, new GUIContent("Fog Of War Tilemap"));
        if (fogOfWarOverlayTileProp != null)
            EditorGUILayout.PropertyField(fogOfWarOverlayTileProp, new GUIContent("Fog Overlay Tile"));
        if (fogOfWarTerrainDatabaseProp != null)
            EditorGUILayout.PropertyField(fogOfWarTerrainDatabaseProp, new GUIContent("Terrain Database"));
        if (fogOfWarDpqAirHeightConfigProp != null)
            EditorGUILayout.PropertyField(fogOfWarDpqAirHeightConfigProp, new GUIContent("DPQ Air Height Config"));
        if (fogOfWarAlphaProp != null)
            EditorGUILayout.PropertyField(fogOfWarAlphaProp, new GUIContent("Fog Alpha"));

        serializedObject.ApplyModifiedProperties();

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
        string cursorLabel = active == TeamId.Neutral ? "Neutral" : $"{active} (Team {(int)active})";
        EditorGUILayout.LabelField("Cursor", cursorLabel);
    }

    public override bool RequiresConstantRepaint()
    {
        return true;
    }

    private void DrawPlayersList()
    {
        if (playersProp == null)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Team Control", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Players ({playersProp.arraySize})", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("Add Player", GUILayout.Width(90f)))
            AddPlayer(TeamId.Green);
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < playersProp.arraySize; i++)
        {
            SerializedProperty player = playersProp.GetArrayElementAtIndex(i);
            if (player == null)
                continue;

            SerializedProperty teamProp = player.FindPropertyRelative("teamId");
            SerializedProperty flipXProp = player.FindPropertyRelative("flipX");
            SerializedProperty startMoneyProp = player.FindPropertyRelative("startMoney");
            SerializedProperty actualMoneyProp = player.FindPropertyRelative("actualMoney");
            SerializedProperty incomePerTurnProp = player.FindPropertyRelative("incomePerTurn");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Element {i}", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                playersProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (teamProp != null)
                EditorGUILayout.PropertyField(teamProp, new GUIContent("Team"));
            if (flipXProp != null)
                EditorGUILayout.PropertyField(flipXProp, new GUIContent("Flip X"));
            if (startMoneyProp != null)
            {
                startMoneyProp.intValue = Mathf.Max(0, EditorGUILayout.IntField("Start Money", startMoneyProp.intValue));
            }

            if (actualMoneyProp != null)
                actualMoneyProp.intValue = Mathf.Max(0, EditorGUILayout.IntField("Actual Money", actualMoneyProp.intValue));

            using (new EditorGUI.DisabledScope(true))
            {
                if (incomePerTurnProp != null)
                    EditorGUILayout.IntField("Income Per Turn", Mathf.Max(0, incomePerTurnProp.intValue));
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.HelpBox("Actual Money agora pode ser ajustado manualmente. Income Per Turn continua automatico e somente leitura.", MessageType.None);
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
        if (p == null)
            return;

        SetTeamToEnumProp(GetPlayerTeamProperty(p), team);
        SerializedProperty flipXProp = p.FindPropertyRelative("flipX");
        SerializedProperty startMoneyProp = p.FindPropertyRelative("startMoney");
        SerializedProperty actualMoneyProp = p.FindPropertyRelative("actualMoney");
        SerializedProperty incomePerTurnProp = p.FindPropertyRelative("incomePerTurn");
        SerializedProperty startMoneyAppliedProp = p.FindPropertyRelative("startMoneyApplied");

        if (flipXProp != null) flipXProp.boolValue = team == TeamId.Red || team == TeamId.Yellow;
        if (startMoneyProp != null) startMoneyProp.intValue = 0;
        if (actualMoneyProp != null) actualMoneyProp.intValue = 0;
        if (incomePerTurnProp != null) incomePerTurnProp.intValue = 0;
        if (startMoneyAppliedProp != null) startMoneyAppliedProp.boolValue = false;
    }

    private static SerializedProperty GetPlayerTeamProperty(SerializedProperty playerElementProp)
    {
        if (playerElementProp == null)
            return null;

        if (playerElementProp.propertyType == SerializedPropertyType.Generic)
        {
            SerializedProperty nested = playerElementProp.FindPropertyRelative("teamId");
            if (nested != null)
                return nested;
        }

        return playerElementProp;
    }

    private static TeamId GetTeamFromEnumProp(SerializedProperty prop)
    {
        if (prop == null)
            return TeamId.Neutral;

        if (prop.propertyType == SerializedPropertyType.Enum)
        {
            string[] names = prop.enumNames;
            int idx = Mathf.Clamp(prop.enumValueIndex, 0, names.Length - 1);
            if (idx >= 0 && idx < names.Length && System.Enum.TryParse(names[idx], out TeamId parsed))
                return parsed;
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
