using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitManager))]
public class UnitManagerEditor : Editor
{
    private SerializedProperty spriteRendererProp;
    private SerializedProperty unitHudProp;
    private SerializedProperty unitDatabaseProp;
    private SerializedProperty boardTilemapProp;
    private SerializedProperty snapToCellCenterProp;
    private SerializedProperty autoSnapWhenMovedInEditorProp;
    private SerializedProperty currentCellPositionProp;
    private SerializedProperty teamIdProp;
    private SerializedProperty unitIdProp;
    private SerializedProperty instanceIdProp;
    private SerializedProperty currentPositionProp;
    private SerializedProperty unitDisplayNameProp;
    private SerializedProperty currentHpProp;
    private SerializedProperty currentAmmoProp;
    private SerializedProperty maxAmmoProp;
    private SerializedProperty currentFuelProp;
    private SerializedProperty maxFuelProp;
    private SerializedProperty hasActedProp;
    private SerializedProperty matchControllerProp;
    private SerializedProperty autoApplyOnStartProp;
    private SerializedProperty manualMoveAnimationSpeedProp;
    private SerializedProperty currentDomainProp;
    private SerializedProperty currentHeightLevelProp;
    private SerializedProperty currentLayerModeIndexProp;
    private SerializedProperty layerStateInitializedProp;

    private void OnEnable()
    {
        spriteRendererProp = serializedObject.FindProperty("spriteRenderer");
        unitHudProp = serializedObject.FindProperty("unitHud");
        unitDatabaseProp = serializedObject.FindProperty("unitDatabase");
        boardTilemapProp = serializedObject.FindProperty("boardTilemap");
        snapToCellCenterProp = serializedObject.FindProperty("snapToCellCenter");
        autoSnapWhenMovedInEditorProp = serializedObject.FindProperty("autoSnapWhenMovedInEditor");
        currentCellPositionProp = serializedObject.FindProperty("currentCellPosition");
        teamIdProp = serializedObject.FindProperty("teamId");
        unitIdProp = serializedObject.FindProperty("unitId");
        instanceIdProp = serializedObject.FindProperty("instanceId");
        currentPositionProp = serializedObject.FindProperty("currentPosition");
        unitDisplayNameProp = serializedObject.FindProperty("unitDisplayName");
        currentHpProp = serializedObject.FindProperty("currentHP");
        currentAmmoProp = serializedObject.FindProperty("currentAmmo");
        maxAmmoProp = serializedObject.FindProperty("maxAmmo");
        currentFuelProp = serializedObject.FindProperty("currentFuel");
        maxFuelProp = serializedObject.FindProperty("maxFuel");
        hasActedProp = serializedObject.FindProperty("hasActed");
        matchControllerProp = serializedObject.FindProperty("matchController");
        autoApplyOnStartProp = serializedObject.FindProperty("autoApplyOnStart");
        manualMoveAnimationSpeedProp = serializedObject.FindProperty("manualMoveAnimationSpeed");
        currentDomainProp = serializedObject.FindProperty("currentDomain");
        currentHeightLevelProp = serializedObject.FindProperty("currentHeightLevel");
        currentLayerModeIndexProp = serializedObject.FindProperty("currentLayerModeIndex");
        layerStateInitializedProp = serializedObject.FindProperty("layerStateInitialized");
    }

    public override void OnInspectorGUI()
    {
        if (spriteRendererProp == null || unitDatabaseProp == null || boardTilemapProp == null || teamIdProp == null || unitIdProp == null)
        {
            EditorGUILayout.HelpBox("UnitManagerEditor: propriedades nao encontradas. Usando inspector padrao.", MessageType.Warning);
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();

        EditorGUILayout.PropertyField(spriteRendererProp);
        EditorGUILayout.PropertyField(unitHudProp, new GUIContent("Unit HUD"));
        EditorGUILayout.PropertyField(unitDatabaseProp);
        EditorGUILayout.PropertyField(boardTilemapProp, new GUIContent("Board Tilemap"));
        EditorGUILayout.PropertyField(snapToCellCenterProp, new GUIContent("Snap To Cell Center"));
        EditorGUILayout.PropertyField(autoSnapWhenMovedInEditorProp, new GUIContent("Auto Snap When Moved In Editor"));
        EditorGUILayout.PropertyField(currentCellPositionProp, new GUIContent("Cell Position"));

        EditorGUILayout.PropertyField(teamIdProp, new GUIContent("Team ID"));

        DrawUnitIdPopup();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(instanceIdProp, new GUIContent("Instance ID"));

        EditorGUILayout.PropertyField(currentPositionProp, new GUIContent("Current Position"));
        EditorGUILayout.PropertyField(unitDisplayNameProp);

        int maxHp = GetMaxHpFromSelection();
        if (maxHp > 0)
            currentHpProp.intValue = EditorGUILayout.IntSlider("Current HP", currentHpProp.intValue, 0, maxHp);
        else
            EditorGUILayout.PropertyField(currentHpProp, new GUIContent("Current HP"));

        maxAmmoProp.intValue = Mathf.Max(1, maxAmmoProp.intValue);
        maxFuelProp.intValue = Mathf.Max(1, maxFuelProp.intValue);
        currentAmmoProp.intValue = Mathf.Clamp(currentAmmoProp.intValue, 0, maxAmmoProp.intValue);
        currentFuelProp.intValue = Mathf.Clamp(currentFuelProp.intValue, 0, maxFuelProp.intValue);

        EditorGUILayout.IntSlider(currentAmmoProp, 0, Mathf.Max(1, maxAmmoProp.intValue), new GUIContent("Current Ammo"));
        EditorGUILayout.PropertyField(maxAmmoProp, new GUIContent("Max Ammo"));
        EditorGUILayout.IntSlider(currentFuelProp, 0, Mathf.Max(1, maxFuelProp.intValue), new GUIContent("Current Fuel"));
        EditorGUILayout.PropertyField(maxFuelProp, new GUIContent("Max Fuel"));

        EditorGUILayout.PropertyField(hasActedProp, new GUIContent("Has Acted"));
        EditorGUILayout.PropertyField(matchControllerProp, new GUIContent("Match Controller"));
        EditorGUILayout.PropertyField(autoApplyOnStartProp);
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Movement Animation", EditorStyles.boldLabel);
        if (manualMoveAnimationSpeedProp != null)
            EditorGUILayout.PropertyField(manualMoveAnimationSpeedProp, new GUIContent("Manual Move Animation Speed"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Layer State", EditorStyles.boldLabel);
        if (currentDomainProp != null)
            EditorGUILayout.PropertyField(currentDomainProp, new GUIContent("Current Domain"));
        if (currentHeightLevelProp != null)
            EditorGUILayout.PropertyField(currentHeightLevelProp, new GUIContent("Current Height Level"));
        if (currentLayerModeIndexProp != null)
            EditorGUILayout.PropertyField(currentLayerModeIndexProp, new GUIContent("Current Layer Mode Index"));
        if (layerStateInitializedProp != null)
            EditorGUILayout.PropertyField(layerStateInitializedProp, new GUIContent("Layer State Initialized"));

        serializedObject.ApplyModifiedProperties();

        UnitManager unit = (UnitManager)target;
        if (GUILayout.Button("Apply From Database"))
            unit.ApplyFromDatabase();
        if (GUILayout.Button("Snap To Cell Center"))
            unit.SnapToCellCenter();
        if (GUILayout.Button("Pull Cell From Transform"))
            unit.PullCellFromTransform();
        if (GUILayout.Button("Sync Layer State From Data (Keep Current If Valid)"))
            unit.SyncLayerStateFromData(forceNativeDefault: false);
        if (GUILayout.Button("Sync Layer State From Data (Force Native Default)"))
            unit.SyncLayerStateFromData(forceNativeDefault: true);
    }

    private void DrawUnitIdPopup()
    {
        if (unitDatabaseProp == null || unitIdProp == null)
        {
            if (unitIdProp != null)
                EditorGUILayout.PropertyField(unitIdProp, new GUIContent("Unit ID"));
            return;
        }

        UnitDatabase db = unitDatabaseProp.objectReferenceValue as UnitDatabase;
        if (db == null || db.Units == null || db.Units.Count == 0)
        {
            EditorGUILayout.PropertyField(unitIdProp);
            return;
        }

        int count = db.Units.Count;
        string[] labels = new string[count];
        int currentIndex = -1;

        for (int i = 0; i < count; i++)
        {
            UnitData unit = db.Units[i];
            if (unit == null)
            {
                labels[i] = "<null>";
                continue;
            }

            string label = string.IsNullOrWhiteSpace(unit.displayName) ? unit.id : $"{unit.id} ({unit.displayName})";
            labels[i] = label;

            if (unit.id == unitIdProp.stringValue)
                currentIndex = i;
        }

        int newIndex = EditorGUILayout.Popup("Unit ID", Mathf.Max(0, currentIndex), labels);
        if (newIndex >= 0 && newIndex < count && db.Units[newIndex] != null)
            unitIdProp.stringValue = db.Units[newIndex].id;
    }

    private int GetMaxHpFromSelection()
    {
        if (unitDatabaseProp == null || unitIdProp == null)
            return 0;

        UnitDatabase db = unitDatabaseProp.objectReferenceValue as UnitDatabase;
        if (db == null || string.IsNullOrWhiteSpace(unitIdProp.stringValue))
            return 0;

        if (!db.TryGetById(unitIdProp.stringValue, out UnitData data) || data == null)
            return 0;

        return Mathf.Max(1, data.maxHP);
    }
}
