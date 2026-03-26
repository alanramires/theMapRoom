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
    private SerializedProperty maxUnitsPerTeamProp;
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
    private SerializedProperty enableFogSourceDebugLogsProp;
    private SerializedProperty enableFogStepPerfLogsProp;
    private SerializedProperty enableFogValidationLogsProp;
    private SerializedProperty enableSensorsRuntimeLogsProp;
    private SerializedProperty enablePodeMirarSensorLogsProp;
    private SerializedProperty enablePodeEmbarcarSensorLogsProp;
    private SerializedProperty enablePodeDesembarcarSensorLogsProp;
    private SerializedProperty enablePodeCapturarSensorLogsProp;
    private SerializedProperty enablePodeFundirSensorLogsProp;
    private SerializedProperty enablePodeSuprirSensorLogsProp;
    private SerializedProperty enablePodeTransferirSensorLogsProp;
    private SerializedProperty enableServicoDoComandoSensorLogsProp;
    private SerializedProperty enablePodePousarSensorLogsProp;
    private SerializedProperty enableAindaMeVeRuntimeLogsProp;
    private SerializedProperty enablePodeDetectarRuntimeLogsProp;
    private SerializedProperty enablePodeEnxergarRuntimeLogsProp;
    private SerializedProperty fogStepPerfTopUnitsProp;
    private SerializedProperty showVictoryOverlayProp;
    private SerializedProperty victoryOverlayTilemapProp;
    private SerializedProperty victoryOverlayTileProp;
    private SerializedProperty victoryOverlayAlphaProp;
    private SerializedProperty enableVictoryStarsProp;
    private SerializedProperty victoryStarsToWinProp;
    private SerializedProperty freezeTurnAdvanceAfterVictoryProp;
    private SerializedProperty victoryStarsByTeamProp;
    private SerializedProperty hasVictoryWinnerProp;
    private SerializedProperty victoryWinnerTeamProp;
    private SerializedProperty dialogManagerProp;
    private SerializedProperty activeTutorialProp;

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
        maxUnitsPerTeamProp = serializedObject.FindProperty("maxUnitsPerTeam");
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
        enableFogSourceDebugLogsProp = serializedObject.FindProperty("enableFogSourceDebugLogs");
        enableFogStepPerfLogsProp = serializedObject.FindProperty("enableFogStepPerfLogs");
        enableFogValidationLogsProp = serializedObject.FindProperty("enableFogValidationLogs");
        enableSensorsRuntimeLogsProp = serializedObject.FindProperty("enableSensorsRuntimeLogs");
        enablePodeMirarSensorLogsProp = serializedObject.FindProperty("enablePodeMirarSensorLogs");
        enablePodeEmbarcarSensorLogsProp = serializedObject.FindProperty("enablePodeEmbarcarSensorLogs");
        enablePodeDesembarcarSensorLogsProp = serializedObject.FindProperty("enablePodeDesembarcarSensorLogs");
        enablePodeCapturarSensorLogsProp = serializedObject.FindProperty("enablePodeCapturarSensorLogs");
        enablePodeFundirSensorLogsProp = serializedObject.FindProperty("enablePodeFundirSensorLogs");
        enablePodeSuprirSensorLogsProp = serializedObject.FindProperty("enablePodeSuprirSensorLogs");
        enablePodeTransferirSensorLogsProp = serializedObject.FindProperty("enablePodeTransferirSensorLogs");
        enableServicoDoComandoSensorLogsProp = serializedObject.FindProperty("enableServicoDoComandoSensorLogs");
        enablePodePousarSensorLogsProp = serializedObject.FindProperty("enablePodePousarSensorLogs");
        enableAindaMeVeRuntimeLogsProp = serializedObject.FindProperty("enableAindaMeVeRuntimeLogs");
        enablePodeDetectarRuntimeLogsProp = serializedObject.FindProperty("enablePodeDetectarRuntimeLogs");
        enablePodeEnxergarRuntimeLogsProp = serializedObject.FindProperty("enablePodeEnxergarRuntimeLogs");
        fogStepPerfTopUnitsProp = serializedObject.FindProperty("fogStepPerfTopUnits");
        showVictoryOverlayProp = serializedObject.FindProperty("showVictoryOverlay");
        victoryOverlayTilemapProp = serializedObject.FindProperty("victoryOverlayTilemap");
        victoryOverlayTileProp = serializedObject.FindProperty("victoryOverlayTile");
        victoryOverlayAlphaProp = serializedObject.FindProperty("victoryOverlayAlpha");
        enableVictoryStarsProp = serializedObject.FindProperty("enableVictoryStars");
        victoryStarsToWinProp = serializedObject.FindProperty("victoryStarsToWin");
        freezeTurnAdvanceAfterVictoryProp = serializedObject.FindProperty("freezeTurnAdvanceAfterVictory");
        victoryStarsByTeamProp = serializedObject.FindProperty("victoryStarsByTeam");
        hasVictoryWinnerProp = serializedObject.FindProperty("hasVictoryWinner");
        victoryWinnerTeamProp = serializedObject.FindProperty("victoryWinnerTeam");
        dialogManagerProp = serializedObject.FindProperty("dialogManager");
        activeTutorialProp = serializedObject.FindProperty("activeTutorial");
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Managers", EditorStyles.boldLabel);
        if (dialogManagerProp != null)
        {
            EditorGUILayout.PropertyField(dialogManagerProp, new GUIContent("Dialog Manager"));
            EditorGUILayout.Space();
        }

        EditorGUILayout.LabelField("Tutorial", EditorStyles.boldLabel);
        if (activeTutorialProp != null)
        {
            EditorGUILayout.PropertyField(activeTutorialProp, new GUIContent("Active Tutorial"));
            EditorGUILayout.Space();
        }

        EditorGUILayout.LabelField("Match State", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(currentTurnProp, new GUIContent("Current Turn"));
        EditorGUILayout.PropertyField(activeTeamIdProp, new GUIContent("Active Team ID"));
        DrawPlayersList();
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
        if (maxUnitsPerTeamProp != null)
            EditorGUILayout.PropertyField(maxUnitsPerTeamProp, new GUIContent("Max Units Per Team"));

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
        if (enableFogSourceDebugLogsProp != null)
            EditorGUILayout.PropertyField(enableFogSourceDebugLogsProp, new GUIContent("Enable Fog Source Debug Logs"));
        if (enableFogStepPerfLogsProp != null)
            EditorGUILayout.PropertyField(enableFogStepPerfLogsProp, new GUIContent("Enable Fog Step Perf Logs"));
        if (enableFogValidationLogsProp != null)
            EditorGUILayout.PropertyField(enableFogValidationLogsProp, new GUIContent("Enable Fog Validation Logs"));
        if (enableSensorsRuntimeLogsProp != null)
            EditorGUILayout.PropertyField(enableSensorsRuntimeLogsProp, new GUIContent("Enable Sensors Runtime Logs"));
        if (enablePodeMirarSensorLogsProp != null)
            EditorGUILayout.PropertyField(enablePodeMirarSensorLogsProp, new GUIContent("Enable PodeMirar Sensor Logs"));
        if (enablePodeEmbarcarSensorLogsProp != null)
            EditorGUILayout.PropertyField(enablePodeEmbarcarSensorLogsProp, new GUIContent("Enable PodeEmbarcar Sensor Logs"));
        if (enablePodeDesembarcarSensorLogsProp != null)
            EditorGUILayout.PropertyField(enablePodeDesembarcarSensorLogsProp, new GUIContent("Enable PodeDesembarcar Sensor Logs"));
        if (enablePodeCapturarSensorLogsProp != null)
            EditorGUILayout.PropertyField(enablePodeCapturarSensorLogsProp, new GUIContent("Enable PodeCapturar Sensor Logs"));
        if (enablePodeFundirSensorLogsProp != null)
            EditorGUILayout.PropertyField(enablePodeFundirSensorLogsProp, new GUIContent("Enable PodeFundir Sensor Logs"));
        if (enablePodeSuprirSensorLogsProp != null)
            EditorGUILayout.PropertyField(enablePodeSuprirSensorLogsProp, new GUIContent("Enable PodeSuprir Sensor Logs"));
        if (enablePodeTransferirSensorLogsProp != null)
            EditorGUILayout.PropertyField(enablePodeTransferirSensorLogsProp, new GUIContent("Enable PodeTransferir Sensor Logs"));
        if (enableServicoDoComandoSensorLogsProp != null)
            EditorGUILayout.PropertyField(enableServicoDoComandoSensorLogsProp, new GUIContent("Enable ServicoDoComando Sensor Logs"));
        if (enablePodePousarSensorLogsProp != null)
            EditorGUILayout.PropertyField(enablePodePousarSensorLogsProp, new GUIContent("Enable PodePousar Sensor Logs"));
        if (enableAindaMeVeRuntimeLogsProp != null)
            EditorGUILayout.PropertyField(enableAindaMeVeRuntimeLogsProp, new GUIContent("Enable AindaMeVe Runtime Logs"));
        if (enablePodeDetectarRuntimeLogsProp != null)
            EditorGUILayout.PropertyField(enablePodeDetectarRuntimeLogsProp, new GUIContent("Enable PodeDetectar Runtime Logs"));
        if (enablePodeEnxergarRuntimeLogsProp != null)
            EditorGUILayout.PropertyField(enablePodeEnxergarRuntimeLogsProp, new GUIContent("Enable PodeEnxergar Runtime Logs"));
        if (enableFogStepPerfLogsProp != null &&
            enableFogStepPerfLogsProp.boolValue &&
            fogStepPerfTopUnitsProp != null)
        {
            EditorGUILayout.PropertyField(fogStepPerfTopUnitsProp, new GUIContent("Fog Step Perf Top Units"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Victory Overlay", EditorStyles.boldLabel);
        if (showVictoryOverlayProp != null)
            EditorGUILayout.PropertyField(showVictoryOverlayProp, new GUIContent("Show Victory Overlay"));
        if (victoryOverlayTilemapProp != null)
            EditorGUILayout.PropertyField(victoryOverlayTilemapProp, new GUIContent("Victory Overlay Tilemap"));
        if (victoryOverlayTileProp != null)
            EditorGUILayout.PropertyField(victoryOverlayTileProp, new GUIContent("Victory Overlay Tile"));
        if (victoryOverlayAlphaProp != null)
            EditorGUILayout.PropertyField(victoryOverlayAlphaProp, new GUIContent("Victory Overlay Alpha"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Victory Stars", EditorStyles.boldLabel);
        if (enableVictoryStarsProp != null)
            EditorGUILayout.PropertyField(enableVictoryStarsProp, new GUIContent("Enable Victory Stars"));
        if (victoryStarsToWinProp != null)
            EditorGUILayout.PropertyField(victoryStarsToWinProp, new GUIContent("Victory Stars To Win"));
        if (freezeTurnAdvanceAfterVictoryProp != null)
            EditorGUILayout.PropertyField(freezeTurnAdvanceAfterVictoryProp, new GUIContent("Freeze Turn Advance After Victory"));
        using (new EditorGUI.DisabledScope(true))
        {
            if (hasVictoryWinnerProp != null)
                EditorGUILayout.PropertyField(hasVictoryWinnerProp, new GUIContent("Has Victory Winner"));
            if (victoryWinnerTeamProp != null)
                EditorGUILayout.PropertyField(victoryWinnerTeamProp, new GUIContent("Victory Winner Team"));
        }

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

            TeamId team = GetTeamFromEnumProp(teamProp);
            int vp = ResolveVictoryStarsForTeam(team);
            int nextVp = Mathf.Max(0, EditorGUILayout.IntField("Current VP", Mathf.Max(0, vp)));
            if (nextVp != vp)
                SetVictoryStarsForTeam(team, nextVp);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.HelpBox("Actual Money agora pode ser ajustado manualmente. Income Per Turn continua automatico e somente leitura.", MessageType.None);
    }

    private int ResolveVictoryStarsForTeam(TeamId team)
    {
        if (victoryStarsByTeamProp == null || team == TeamId.Neutral)
            return 0;

        for (int i = 0; i < victoryStarsByTeamProp.arraySize; i++)
        {
            SerializedProperty entry = victoryStarsByTeamProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty teamProp = entry.FindPropertyRelative("teamId");
            SerializedProperty starsProp = entry.FindPropertyRelative("stars");
            TeamId entryTeam = GetTeamFromEnumProp(teamProp);
            if (entryTeam != team)
                continue;

            return starsProp != null ? Mathf.Max(0, starsProp.intValue) : 0;
        }

        return 0;
    }

    private void SetVictoryStarsForTeam(TeamId team, int value)
    {
        if (victoryStarsByTeamProp == null || team == TeamId.Neutral)
            return;

        int safe = Mathf.Max(0, value);
        for (int i = 0; i < victoryStarsByTeamProp.arraySize; i++)
        {
            SerializedProperty entry = victoryStarsByTeamProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty teamProp = entry.FindPropertyRelative("teamId");
            SerializedProperty starsProp = entry.FindPropertyRelative("stars");
            TeamId entryTeam = GetTeamFromEnumProp(teamProp);
            if (entryTeam != team || starsProp == null)
                continue;

            starsProp.intValue = safe;
            return;
        }

        int index = victoryStarsByTeamProp.arraySize;
        victoryStarsByTeamProp.InsertArrayElementAtIndex(index);
        SerializedProperty added = victoryStarsByTeamProp.GetArrayElementAtIndex(index);
        if (added == null)
            return;

        SerializedProperty addedTeam = added.FindPropertyRelative("teamId");
        SerializedProperty addedStars = added.FindPropertyRelative("stars");
        SetTeamToEnumProp(addedTeam, team);
        if (addedStars != null)
            addedStars.intValue = safe;
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
