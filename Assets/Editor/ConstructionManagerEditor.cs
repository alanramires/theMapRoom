using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionManager))]
public class ConstructionManagerEditor : Editor
{
    private SerializedProperty spriteRendererProp;
    private SerializedProperty constructionDatabaseProp;
    private SerializedProperty boardTilemapProp;
    private SerializedProperty snapToCellCenterProp;
    private SerializedProperty autoSnapWhenMovedInEditorProp;
    private SerializedProperty currentCellPositionProp;
    private SerializedProperty teamIdProp;
    private SerializedProperty constructionIdProp;
    private SerializedProperty instanceIdProp;
    private SerializedProperty currentPositionProp;
    private SerializedProperty constructionDisplayNameProp;
    private SerializedProperty autoApplyOnStartProp;
    private SerializedProperty siteRuntimeProp;
    private SerializedProperty hasSiteRuntimeOverrideProp;
    private SerializedProperty currentCapturePointsProp;
    private SerializedProperty hasInfiniteSuppliesOverrideProp;
    private SerializedProperty originalOwnerTeamIdProp;
    private SerializedProperty firstOwnerTeamIdProp;
    private SerializedProperty firstOwnerInitializedProp;

    private void OnEnable()
    {
        spriteRendererProp = serializedObject.FindProperty("spriteRenderer");
        constructionDatabaseProp = serializedObject.FindProperty("constructionDatabase");
        boardTilemapProp = serializedObject.FindProperty("boardTilemap");
        snapToCellCenterProp = serializedObject.FindProperty("snapToCellCenter");
        autoSnapWhenMovedInEditorProp = serializedObject.FindProperty("autoSnapWhenMovedInEditor");
        currentCellPositionProp = serializedObject.FindProperty("currentCellPosition");
        teamIdProp = serializedObject.FindProperty("teamId");
        constructionIdProp = serializedObject.FindProperty("constructionId");
        instanceIdProp = serializedObject.FindProperty("instanceId");
        currentPositionProp = serializedObject.FindProperty("currentPosition");
        constructionDisplayNameProp = serializedObject.FindProperty("constructionDisplayName");
        autoApplyOnStartProp = serializedObject.FindProperty("autoApplyOnStart");
        siteRuntimeProp = serializedObject.FindProperty("siteRuntime");
        hasSiteRuntimeOverrideProp = serializedObject.FindProperty("hasSiteRuntimeOverride");
        currentCapturePointsProp = serializedObject.FindProperty("currentCapturePoints");
        hasInfiniteSuppliesOverrideProp = serializedObject.FindProperty("hasInfiniteSuppliesOverride");
        originalOwnerTeamIdProp = serializedObject.FindProperty("originalOwnerTeamId");
        firstOwnerTeamIdProp = serializedObject.FindProperty("firstOwnerTeamId");
        firstOwnerInitializedProp = serializedObject.FindProperty("firstOwnerInitialized");
    }

    public override void OnInspectorGUI()
    {
        if (spriteRendererProp == null || constructionDatabaseProp == null || boardTilemapProp == null || teamIdProp == null || constructionIdProp == null)
        {
            EditorGUILayout.HelpBox("ConstructionManagerEditor: propriedades nao encontradas. Usando inspector padrao.", MessageType.Warning);
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();

        EditorGUILayout.PropertyField(spriteRendererProp);
        EditorGUILayout.PropertyField(constructionDatabaseProp);
        EditorGUILayout.PropertyField(boardTilemapProp, new GUIContent("Board Tilemap"));
        EditorGUILayout.PropertyField(snapToCellCenterProp, new GUIContent("Snap To Cell Center"));
        EditorGUILayout.PropertyField(autoSnapWhenMovedInEditorProp, new GUIContent("Auto Snap When Moved In Editor"));
        EditorGUILayout.PropertyField(currentCellPositionProp, new GUIContent("Cell Position"));
        EditorGUILayout.PropertyField(teamIdProp, new GUIContent("Team ID"));

        DrawConstructionIdPopup();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(instanceIdProp, new GUIContent("Instance ID"));

        EditorGUILayout.PropertyField(currentPositionProp, new GUIContent("Current Position"));
        EditorGUILayout.PropertyField(constructionDisplayNameProp, new GUIContent("Construction Display Name"));

        DrawCaptureEditorBlock();

        if (originalOwnerTeamIdProp != null)
            EditorGUILayout.PropertyField(originalOwnerTeamIdProp, new GUIContent("Original Owner Team"));
        if (firstOwnerInitializedProp != null)
            EditorGUILayout.PropertyField(firstOwnerInitializedProp, new GUIContent("First Owner Initialized"));
        if (firstOwnerTeamIdProp != null)
            EditorGUILayout.PropertyField(firstOwnerTeamIdProp, new GUIContent("First Owner Team"));

        if (hasSiteRuntimeOverrideProp != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(hasSiteRuntimeOverrideProp, new GUIContent("Has Site Runtime Override"));
        }

        if (siteRuntimeProp != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Site Runtime (Live)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edicoes aqui sao da instancia em campo. Ao editar, a instancia passa a usar override local.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            DrawSiteRuntime(siteRuntimeProp, hasInfiniteSuppliesOverrideProp);
            if (EditorGUI.EndChangeCheck() && hasSiteRuntimeOverrideProp != null)
                hasSiteRuntimeOverrideProp.boolValue = true;
        }

        EditorGUILayout.PropertyField(autoApplyOnStartProp);
        serializedObject.ApplyModifiedProperties();

        ConstructionManager construction = (ConstructionManager)target;

        if (GUILayout.Button("Apply From Database"))
            construction.ApplyFromDatabase();
        if (GUILayout.Button("Reset Instance Override (Use Database Defaults)"))
        {
            SerializedObject so = new SerializedObject(construction);
            SerializedProperty overrideProp = so.FindProperty("hasSiteRuntimeOverride");
            if (overrideProp != null)
                overrideProp.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            construction.ApplyFromDatabase();
            EditorUtility.SetDirty(construction);
        }
        if (GUILayout.Button("Snap To Cell Center"))
            construction.SnapToCellCenter();
        if (GUILayout.Button("Pull Cell From Transform"))
            construction.PullCellFromTransform();
    }

    private void DrawConstructionIdPopup()
    {
        if (constructionDatabaseProp == null || constructionIdProp == null)
        {
            if (constructionIdProp != null)
                EditorGUILayout.PropertyField(constructionIdProp, new GUIContent("Construction ID"));
            return;
        }

        ConstructionDatabase db = constructionDatabaseProp.objectReferenceValue as ConstructionDatabase;
        if (db == null || db.Constructions == null || db.Constructions.Count == 0)
        {
            EditorGUILayout.PropertyField(constructionIdProp, new GUIContent("Construction ID"));
            return;
        }

        int count = db.Constructions.Count;
        string[] labels = new string[count];
        int currentIndex = -1;

        for (int i = 0; i < count; i++)
        {
            ConstructionData construction = db.Constructions[i];
            if (construction == null)
            {
                labels[i] = "<null>";
                continue;
            }

            labels[i] = string.IsNullOrWhiteSpace(construction.displayName)
                ? construction.id
                : $"{construction.id} ({construction.displayName})";

            if (construction.id == constructionIdProp.stringValue)
                currentIndex = i;
        }

        int newIndex = EditorGUILayout.Popup("Construction ID", Mathf.Max(0, currentIndex), labels);
        if (newIndex >= 0 && newIndex < count && db.Constructions[newIndex] != null)
            constructionIdProp.stringValue = db.Constructions[newIndex].id;
    }

    private void DrawCaptureEditorBlock()
    {
        if (currentCapturePointsProp == null)
            return;

        int captureMax = ResolveCaptureMaxFromSiteRuntime();
        captureMax = Mathf.Max(0, captureMax);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.IntField("Capture Points Max", captureMax);

        if (captureMax <= 0)
        {
            EditorGUILayout.IntField("Current Capture Points", 0);
            currentCapturePointsProp.intValue = 0;
            return;
        }

        int clampedCurrent = Mathf.Clamp(currentCapturePointsProp.intValue, 0, captureMax);
        int newValue = EditorGUILayout.IntSlider("Current Capture Points", clampedCurrent, 0, captureMax);
        if (newValue != currentCapturePointsProp.intValue)
            currentCapturePointsProp.intValue = newValue;
    }

    private int ResolveCaptureMaxFromSiteRuntime()
    {
        if (siteRuntimeProp == null)
            return 0;

        SerializedProperty captureMaxProp = siteRuntimeProp.FindPropertyRelative("capturePointsMax");
        if (captureMaxProp == null)
            return 0;

        return captureMaxProp.intValue;
    }

    private static void DrawSiteRuntime(SerializedProperty siteRuntime, SerializedProperty infiniteOverrideProp)
    {
        if (siteRuntime == null)
            return;

        EditorGUI.indentLevel++;
        DrawIfExists(siteRuntime.FindPropertyRelative("isPlayerHeadQuarter"), "Is Player Head Quarter");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);
        DrawIfExists(siteRuntime.FindPropertyRelative("isCapturable"), "Is Capturable");
        DrawIfExists(siteRuntime.FindPropertyRelative("capturePointsMax"), "Capture Points Max");
        DrawIfExists(siteRuntime.FindPropertyRelative("capturedIncoming"), "Captured Incoming");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Production", EditorStyles.boldLabel);
        DrawIfExists(siteRuntime.FindPropertyRelative("sellingRule"), "Selling Rules");
        DrawIfExists(siteRuntime.FindPropertyRelative("offeredUnits"), "Offered Units");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Supplies", EditorStyles.boldLabel);
        DrawIfExists(siteRuntime.FindPropertyRelative("canProvideSupplies"), "Can Provide Supplies");
        DrawIfExists(infiniteOverrideProp, "Has Infinite Supplies (Override)");
        DrawIfExists(siteRuntime.FindPropertyRelative("offeredSupplies"), "Offered Supplies");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Services", EditorStyles.boldLabel);
        DrawIfExists(siteRuntime.FindPropertyRelative("offeredServices"), "Offered Services");
        EditorGUI.indentLevel--;
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren: true);
    }

}
