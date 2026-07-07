using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionManager))]
public class ConstructionManagerEditor : Editor
{
    private enum ForceCopyFilter
    {
        Army,
        Navy,
        Aeronautic
    }

    private SerializedProperty spriteRendererProp;
    private SerializedProperty constructionDatabaseProp;
    private SerializedProperty boardTilemapProp;
    private SerializedProperty snapToCellCenterProp;
    private SerializedProperty autoSnapWhenMovedInEditorProp;
    private SerializedProperty currentCellPositionProp;
    private SerializedProperty teamIdProp;
    private SerializedProperty slotIndexProp;
    private SerializedProperty constructionIdProp;
    private SerializedProperty instanceIdProp;
    private SerializedProperty currentPositionProp;
    private SerializedProperty constructionDisplayNameProp;
    private SerializedProperty isVisibleProp;
    private SerializedProperty autoApplyOnStartProp;
    private SerializedProperty siteRuntimeProp;
    private SerializedProperty hasSiteRuntimeOverrideProp;
    private SerializedProperty currentCapturePointsProp;
    private SerializedProperty hasInfiniteSuppliesOverrideProp;
    private SerializedProperty originalOwnerSlotIndexProp;
    private SerializedProperty firstOwnerSlotIndexProp;
    private SerializedProperty sectorProp;
    private SerializedProperty eixoOverridesProp;
    private SerializedProperty isForwardObserverSpotProp;
    private SerializedProperty forwardObserverSpotUsageProp;
    private SerializedProperty isRallyPointProp;
    private SerializedProperty rallyOwnerSlotIndexProp;
    private SerializedProperty isAnchorSectorProp;
    private SerializedProperty anchorSectorSlotIndexProp;
    private ForceCopyFilter forceCopyFilter = ForceCopyFilter.Army;

    private static readonly Color[] SectorColors = new Color[]
    {
        new Color(0.30f, 0.60f, 1.00f), // Alpha
        new Color(0.20f, 0.75f, 0.35f), // Bravo
        new Color(1.00f, 0.55f, 0.10f), // Charlie
        new Color(0.85f, 0.20f, 0.20f), // Delta
        new Color(0.75f, 0.35f, 0.90f), // Echo
        new Color(0.15f, 0.80f, 0.85f), // Foxtrot
        new Color(1.00f, 0.85f, 0.10f), // Golf
        new Color(0.95f, 0.40f, 0.65f), // Hotel
        new Color(0.48f, 0.48f, 0.95f), // India
        new Color(0.40f, 0.85f, 0.55f), // Juliet
        new Color(0.85f, 0.65f, 0.25f), // Kilo
        new Color(0.65f, 0.30f, 0.80f), // Lima
        new Color(0.25f, 0.70f, 0.95f), // Mike
        new Color(0.90f, 0.45f, 0.35f), // November
        new Color(0.55f, 0.70f, 0.25f), // Oscar
        new Color(0.95f, 0.70f, 0.45f), // Papa
        new Color(0.30f, 0.60f, 0.55f), // Quebec
        new Color(0.85f, 0.35f, 0.50f), // Romeo
        new Color(0.70f, 0.70f, 0.70f), // Tango
        new Color(0.95f, 0.95f, 0.95f), // BaseTeam
    };

    private void OnEnable()
    {
        spriteRendererProp = serializedObject.FindProperty("spriteRenderer");
        constructionDatabaseProp = serializedObject.FindProperty("constructionDatabase");
        boardTilemapProp = serializedObject.FindProperty("boardTilemap");
        snapToCellCenterProp = serializedObject.FindProperty("snapToCellCenter");
        autoSnapWhenMovedInEditorProp = serializedObject.FindProperty("autoSnapWhenMovedInEditor");
        currentCellPositionProp = serializedObject.FindProperty("currentCellPosition");
        teamIdProp = serializedObject.FindProperty("teamId");
        slotIndexProp = serializedObject.FindProperty("slotIndex");
        constructionIdProp = serializedObject.FindProperty("constructionId");
        instanceIdProp = serializedObject.FindProperty("instanceId");
        currentPositionProp = serializedObject.FindProperty("currentPosition");
        constructionDisplayNameProp = serializedObject.FindProperty("constructionDisplayName");
        isVisibleProp = serializedObject.FindProperty("isVisible");
        autoApplyOnStartProp = serializedObject.FindProperty("autoApplyOnStart");
        siteRuntimeProp = serializedObject.FindProperty("siteRuntime");
        hasSiteRuntimeOverrideProp = serializedObject.FindProperty("hasSiteRuntimeOverride");
        currentCapturePointsProp = serializedObject.FindProperty("currentCapturePoints");
        hasInfiniteSuppliesOverrideProp = serializedObject.FindProperty("hasInfiniteSuppliesOverride");
        originalOwnerSlotIndexProp = serializedObject.FindProperty("originalOwnerSlotIndex");
        firstOwnerSlotIndexProp = serializedObject.FindProperty("firstOwnerSlotIndex");
        sectorProp = serializedObject.FindProperty("sector");
        eixoOverridesProp = serializedObject.FindProperty("eixoOverrides");
        isForwardObserverSpotProp = serializedObject.FindProperty("isForwardObserverSpot");
        forwardObserverSpotUsageProp = serializedObject.FindProperty("forwardObserverSpotUsage");
        isRallyPointProp = serializedObject.FindProperty("isRallyPoint");
        rallyOwnerSlotIndexProp = serializedObject.FindProperty("rallyOwnerSlotIndex");
        isAnchorSectorProp = serializedObject.FindProperty("isAnchorSector");
        anchorSectorSlotIndexProp = serializedObject.FindProperty("anchorSectorSlotIndex");
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
        DrawSlotAndTeamBlock();
        DrawSectorPopup();
        DrawEixoOverrideBlock();

        DrawRoleBlock();
        DrawConstructionIdPopup();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(instanceIdProp, new GUIContent("Instance ID"));

        EditorGUILayout.PropertyField(currentPositionProp, new GUIContent("Current Position"));
        EditorGUILayout.PropertyField(constructionDisplayNameProp, new GUIContent("Construction Display Name"));
        DrawGlobalVictoryFallbackToggle();

        DrawCaptureEditorBlock();

        DrawOwnershipSlotPopup("Original Owner Slot", originalOwnerSlotIndexProp);
        DrawOwnershipSlotPopup("First Owner Slot", firstOwnerSlotIndexProp);

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
            DrawSiteRuntime(siteRuntimeProp, hasInfiniteSuppliesOverrideProp, hasSiteRuntimeOverrideProp);
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

    private void DrawRoleBlock()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Role", EditorStyles.boldLabel);
        if (isForwardObserverSpotProp != null)
        {
            EditorGUILayout.PropertyField(isForwardObserverSpotProp, new GUIContent("Forward Observer Spot"));
            if (isForwardObserverSpotProp.boolValue && forwardObserverSpotUsageProp != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    forwardObserverSpotUsageProp,
                    new GUIContent("Usage"));
                EditorGUI.indentLevel--;
            }
        }
        if (isRallyPointProp != null)
            EditorGUILayout.PropertyField(isRallyPointProp, new GUIContent("Rally Point"));
        DrawRallyOwnerSlot();
        if (isAnchorSectorProp != null)
            EditorGUILayout.PropertyField(isAnchorSectorProp, new GUIContent("Anchor Sector"));
        DrawAnchorSectorSlot();
        if (isVisibleProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(isVisibleProp, new GUIContent("Is Visible"));
            bool visibilityChanged = EditorGUI.EndChangeCheck();
            bool nextVisibility = isVisibleProp.boolValue;
            if (visibilityChanged && Application.isPlaying)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] is ConstructionManager manager)
                    {
                        manager.SetVisible(nextVisibility);
                        EditorUtility.SetDirty(manager);
                    }
                }
            }
        }
    }

    private void DrawAnchorSectorSlot()
    {
        if (anchorSectorSlotIndexProp == null)
            return;

        using (new EditorGUI.DisabledScope(isAnchorSectorProp != null && !isAnchorSectorProp.boolValue))
        {
            MatchController mc = Object.FindAnyObjectByType<MatchController>();
            int slotCount = mc != null ? mc.SlotCount : 0;
            if (slotCount <= 0)
            {
                EditorGUILayout.PropertyField(anchorSectorSlotIndexProp, new GUIContent("Anchor Sector Slot"));
                return;
            }

            string[] labels = new string[slotCount + 1];
            labels[0] = "None";
            for (int i = 0; i < slotCount; i++)
            {
                TeamId team = mc.GetTeamIdForSlot(i);
                labels[i + 1] = $"Slot {i} - {TeamUtils.GetName(team)}";
            }

            int currentOption = Mathf.Clamp(anchorSectorSlotIndexProp.intValue + 1, 0, slotCount);
            int selectedOption = EditorGUILayout.Popup("Anchor Sector Slot", currentOption, labels);
            anchorSectorSlotIndexProp.intValue = selectedOption - 1;
        }
    }

    private void DrawRallyOwnerSlot()
    {
        if (rallyOwnerSlotIndexProp == null)
            return;

        using (new EditorGUI.DisabledScope(isRallyPointProp != null && !isRallyPointProp.boolValue))
        {
            MatchController mc = Object.FindAnyObjectByType<MatchController>();
            int slotCount = mc != null ? mc.SlotCount : 0;
            if (slotCount <= 0)
            {
                EditorGUILayout.PropertyField(rallyOwnerSlotIndexProp, new GUIContent("Rally Owner Slot"));
                return;
            }

            string[] labels = new string[slotCount + 1];
            labels[0] = "None";
            for (int i = 0; i < slotCount; i++)
            {
                TeamId team = mc.GetTeamIdForSlot(i);
                labels[i + 1] = $"Slot {i} - {TeamUtils.GetName(team)}";
            }

            int currentOption = Mathf.Clamp(rallyOwnerSlotIndexProp.intValue + 1, 0, slotCount);
            int selectedOption = EditorGUILayout.Popup("Rally Owner Slot", currentOption, labels);
            rallyOwnerSlotIndexProp.intValue = selectedOption - 1;
        }
    }

    private void DrawSectorPopup()
    {
        if (sectorProp == null)
            return;

        int current = sectorProp.enumValueIndex;
        string[] names = sectorProp.enumNames;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Sector");

        Color prev = GUI.backgroundColor;
        if (current > 0 && current - 1 < SectorColors.Length)
            GUI.backgroundColor = SectorColors[current - 1];

        int next = EditorGUILayout.Popup(current, names);
        GUI.backgroundColor = prev;
        EditorGUILayout.EndHorizontal();

        if (next != current)
            sectorProp.enumValueIndex = next;
    }

    private void DrawEixoOverrideBlock()
    {
        if (eixoOverridesProp == null)
            return;

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Override Eixo (por slot)", EditorStyles.boldLabel);

        ConstructionManager cm = target as ConstructionManager;
        MatchController mc = Object.FindAnyObjectByType<MatchController>();
        int slotCount = mc != null ? mc.SlotCount : 0;

        string[] slotLabels = new string[slotCount + 1];
        slotLabels[0] = "Todos os slots";
        for (int i = 0; i < slotCount; i++)
            slotLabels[i + 1] = $"Slot {i} - {TeamUtils.GetName(mc.GetTeamIdForSlot(i))}";

        if (eixoOverridesProp.arraySize == 0)
            EditorGUILayout.LabelField("   (nenhum — usa o cálculo automático por ângulo)");

        int removeIndex = -1;
        EditorGUI.indentLevel++;
        for (int i = 0; i < eixoOverridesProp.arraySize; i++)
        {
            SerializedProperty entry = eixoOverridesProp.GetArrayElementAtIndex(i);
            SerializedProperty slotProp = entry.FindPropertyRelative("slotIndex");
            SerializedProperty eixoProp = entry.FindPropertyRelative("eixo");

            EditorGUILayout.BeginHorizontal();
            if (slotCount > 0)
            {
                int cur = Mathf.Clamp(slotProp.intValue + 1, 0, slotCount);
                int sel = EditorGUILayout.Popup(cur, slotLabels);
                slotProp.intValue = sel - 1;
            }
            else
            {
                slotProp.intValue = EditorGUILayout.IntField(slotProp.intValue);
            }
            GUILayout.Label("Eixo", GUILayout.Width(34));
            int slotForEixo = slotProp.intValue;
            if (cm != null && cm.Sector != ConstructionSector.None && mc != null
                && slotForEixo >= 0 && slotForEixo < slotCount)
            {
                BuildEixoOptionsForSlot(mc.GetTeamIdForSlot(slotForEixo), cm.Sector,
                    out string[] eLabels, out int[] eValues);
                int curIdx = System.Array.IndexOf(eValues, eixoProp.intValue);
                EditorGUI.BeginChangeCheck();
                int newIdx = EditorGUILayout.Popup(Mathf.Max(0, curIdx), eLabels, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                    eixoProp.intValue = eValues[Mathf.Clamp(newIdx, 0, eValues.Length - 1)];
            }
            else
            {
                // Slot "Todos" ou sem MatchController: cai pro campo numérico simples.
                eixoProp.intValue = EditorGUILayout.IntField(eixoProp.intValue, GUILayout.Width(42));
            }
            if (GUILayout.Button("−", GUILayout.Width(24)))
                removeIndex = i;
            EditorGUILayout.EndHorizontal();

            // Referencia: eixo automatico (sem override) do setor pro slot desta entrada.
            if (cm != null && cm.Sector != ConstructionSector.None && mc != null && slotCount > 0)
            {
                int slot = slotProp.intValue;
                string autoInfo;
                if (slot >= 0 && slot < slotCount)
                    autoInfo = $"Slot {slot}: {DescribeAutoEixo(mc.GetTeamIdForSlot(slot), cm.Sector)}";
                else
                {
                    var parts = new System.Text.StringBuilder();
                    for (int s = 0; s < slotCount; s++)
                    {
                        if (s > 0) parts.Append("   ");
                        parts.Append($"S{s}:{DescribeAutoEixo(mc.GetTeamIdForSlot(s), cm.Sector)}");
                    }
                    autoInfo = parts.ToString();
                }
                EditorGUILayout.LabelField("auto (sem override)", autoInfo);
            }
        }
        EditorGUI.indentLevel--;

        if (removeIndex >= 0)
            eixoOverridesProp.DeleteArrayElementAtIndex(removeIndex);

        if (GUILayout.Button("+ Adicionar override de eixo"))
        {
            int idx = eixoOverridesProp.arraySize;
            eixoOverridesProp.InsertArrayElementAtIndex(idx);
            SerializedProperty newEntry = eixoOverridesProp.GetArrayElementAtIndex(idx);
            newEntry.FindPropertyRelative("slotIndex").intValue = -1; // default: todos
            newEntry.FindPropertyRelative("eixo").intValue = 0;
        }
    }

    private static string DescribeAutoEixo(TeamId team, ConstructionSector sector)
    {
        InvasionAxisMap map = InvasionAxisMap.Build(team, null, applyOverrides: false);
        int e = map.GetEixo(sector);
        return e > 0 ? $"E{e}" : "fora de eixo";
    }

    // Opcoes de eixo do leque do slot: "0 - fora de eixo" + cada eixo principal como "E{n}: I-R"
    // (inicial do 1o no -> inicial do rally, ex.: Bravo->Foxtrot = "B-F"). Exclui o eixo de invasao
    // (sintetico, nao e alvo valido de override). values[] guarda o EixoIndex correspondente (0=fora).
    private static void BuildEixoOptionsForSlot(TeamId team, ConstructionSector sector,
        out string[] labels, out int[] values)
    {
        InvasionAxisMap map = InvasionAxisMap.Build(team, null, applyOverrides: false);
        var lbls = new System.Collections.Generic.List<string> { "0 - fora de eixo" };
        var vals = new System.Collections.Generic.List<int> { 0 };
        foreach (InvasionAxisMap.Axis axis in map.Axes)
        {
            if (axis.IsInvasionAxis)
                continue;
            string firstSec = axis.Corridor.Count > 0
                ? axis.Corridor[0].ToString()
                : axis.RallySector.ToString();
            string here = map.GetEixo(sector) == axis.EixoIndex ? "  (atual)" : "";
            lbls.Add($"E{axis.EixoIndex}: {Initial(firstSec)}-{Initial(axis.RallySector.ToString())}{here}");
            vals.Add(axis.EixoIndex);
        }
        labels = lbls.ToArray();
        values = vals.ToArray();
    }

    private static string Initial(string s)
        => string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1).ToUpperInvariant();

    private void DrawOwnershipSlotPopup(string label, SerializedProperty slotProp)
    {
        if (slotProp == null) return;
        MatchController mc = Object.FindAnyObjectByType<MatchController>();
        int slotCount = mc != null ? mc.SlotCount : 0;

        int totalOptions = slotCount + 1;
        string[] labels = new string[totalOptions];
        labels[0] = "Neutral";
        for (int i = 0; i < slotCount; i++)
        {
            TeamId t = mc.GetTeamIdForSlot(i);
            labels[i + 1] = "Slot " + i + " — " + TeamUtils.GetName(t);
        }

        int currentSlot = slotProp.intValue;
        int popupIndex = currentSlot < 0 ? 0 : Mathf.Clamp(currentSlot + 1, 0, totalOptions - 1);

        EditorGUI.BeginChangeCheck();
        int newPopupIndex = EditorGUILayout.Popup(label, popupIndex, labels);
        if (EditorGUI.EndChangeCheck())
        {
            slotProp.intValue = newPopupIndex == 0 ? -1 : newPopupIndex - 1;
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawSlotAndTeamBlock()
    {
        if (slotIndexProp == null)
        {
            EditorGUILayout.PropertyField(teamIdProp, new GUIContent("Team ID"));
            return;
        }

        MatchController mc = Object.FindAnyObjectByType<MatchController>();
        int slotCount = mc != null ? mc.SlotCount : 0;

        // Monta labels: index 0 = Neutral, indices 1..N = slots
        int totalOptions = slotCount + 1;
        string[] labels = new string[totalOptions];
        labels[0] = "Neutral";
        for (int i = 0; i < slotCount; i++)
        {
            TeamId t = mc.GetTeamIdForSlot(i);
            labels[i + 1] = "Slot " + i + " — " + TeamUtils.GetName(t);
        }

        int currentSlot = slotIndexProp.intValue;
        int popupIndex = currentSlot < 0 ? 0 : Mathf.Clamp(currentSlot + 1, 0, totalOptions - 1);

        EditorGUI.BeginChangeCheck();
        int newPopupIndex = EditorGUILayout.Popup("Slot ID", popupIndex, labels);
        if (EditorGUI.EndChangeCheck())
        {
            int newSlot = newPopupIndex == 0 ? -1 : newPopupIndex - 1;
            slotIndexProp.intValue = newSlot;
            serializedObject.ApplyModifiedProperties();

            // Resolve teamId imediatamente
            ConstructionManager cm = (ConstructionManager)target;
            Undo.RecordObject(cm, "Change Slot");
            cm.SetSlotIndex(newSlot);
            EditorUtility.SetDirty(cm);
        }

        // Team ID como read-only — derivado do slot
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(teamIdProp, new GUIContent("Team ID (resolved)"));
    }

    private void DrawGlobalVictoryFallbackToggle()
    {
        ConstructionManager construction = target as ConstructionManager;
        if (construction == null)
            return;

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Victory (Fallback)", EditorStyles.boldLabel);
        bool current = construction.GetVictoryBuildingRuntimeFlag();
        bool next = EditorGUILayout.Toggle("Is Victory Building", current);
        if (next == current)
            return;

        Undo.RecordObject(construction, "Toggle Victory Building");
        construction.SetVictoryBuildingRuntimeFlag(next);
        if (hasSiteRuntimeOverrideProp != null)
            hasSiteRuntimeOverrideProp.boolValue = true;
        EditorUtility.SetDirty(construction);
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

    private void DrawSiteRuntime(SerializedProperty siteRuntime, SerializedProperty infiniteOverrideProp, SerializedProperty runtimeOverrideProp)
    {
        if (siteRuntime == null)
            return;

        EditorGUI.indentLevel++;
        DrawIfExists(siteRuntime.FindPropertyRelative("isPlayerHeadQuarter"), "Is Player Head Quarter");
        DrawVictoryBuildingToggle(runtimeOverrideProp);

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);
        DrawIfExists(siteRuntime.FindPropertyRelative("isCapturable"), "Is Capturable");
        DrawIfExists(siteRuntime.FindPropertyRelative("capturePointsMax"), "Capture Points Max");
        DrawIfExists(siteRuntime.FindPropertyRelative("capturedIncoming"), "Captured Incoming");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Production", EditorStyles.boldLabel);
        DrawIfExists(siteRuntime.FindPropertyRelative("sellingRule"), "Selling Rules");
        SerializedProperty offeredUnitsProp = siteRuntime.FindPropertyRelative("offeredUnits");
        DrawIfExists(offeredUnitsProp, "Offered Units");
        DrawOfferedUnitsQuickFill(offeredUnitsProp, runtimeOverrideProp);

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

    private void DrawVictoryBuildingToggle(SerializedProperty runtimeOverrideProp)
    {
        ConstructionManager construction = target as ConstructionManager;
        if (construction == null)
            return;

        bool current = construction.GetVictoryBuildingRuntimeFlag();
        bool next = EditorGUILayout.Toggle("Is Victory Building", current);
        if (next == current)
            return;

        Undo.RecordObject(construction, "Toggle Victory Building");
        construction.SetVictoryBuildingRuntimeFlag(next);
        EditorUtility.SetDirty(construction);
        if (runtimeOverrideProp != null)
            runtimeOverrideProp.boolValue = true;
    }

    private void DrawOfferedUnitsQuickFill(SerializedProperty offeredUnitsProp, SerializedProperty runtimeOverrideProp)
    {
        if (offeredUnitsProp == null)
            return;

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Quick Fill Offered Units", EditorStyles.miniBoldLabel);
        forceCopyFilter = (ForceCopyFilter)EditorGUILayout.EnumPopup("Copy Units Of", forceCopyFilter);

        UnitDatabase db = ResolveUnitDatabaseFromScene();
        using (new EditorGUI.DisabledScope(db == null))
        {
            if (GUILayout.Button("Copy From Current Unit Database"))
            {
                int copied = CopyOfferedUnitsByForce(offeredUnitsProp, db, forceCopyFilter);
                if (runtimeOverrideProp != null)
                    runtimeOverrideProp.boolValue = true;
                Debug.Log($"[ConstructionManagerEditor] Offered Units atualizadas: {copied} unidade(s) copiadas ({forceCopyFilter}).");
            }
        }

        if (db == null)
            EditorGUILayout.HelpBox("Unit Database nao encontrada na cena. Verifique se existe UnitSpawner com UnitDatabase configurado.", MessageType.Warning);
        else
            EditorGUILayout.ObjectField("Current Unit Database", db, typeof(UnitDatabase), false);
    }

    private static UnitDatabase ResolveUnitDatabaseFromScene()
    {
        UnitSpawner[] spawners = Object.FindObjectsByType<UnitSpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
        {
            UnitSpawner spawner = spawners[i];
            if (spawner == null)
                continue;

            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty dbProp = so.FindProperty("unitDatabase");
            if (dbProp == null)
                continue;

            UnitDatabase db = dbProp.objectReferenceValue as UnitDatabase;
            if (db != null)
                return db;
        }

        return null;
    }

    private static int CopyOfferedUnitsByForce(SerializedProperty offeredUnitsProp, UnitDatabase db, ForceCopyFilter filter)
    {
        if (offeredUnitsProp == null || db == null || db.Units == null)
            return 0;

        MilitaryForce wanted = MilitaryForce.Army;
        if (filter == ForceCopyFilter.Navy)
            wanted = MilitaryForce.Navy;
        else if (filter == ForceCopyFilter.Aeronautic)
            wanted = MilitaryForce.Aeronautic;

        offeredUnitsProp.arraySize = 0;
        int copied = 0;
        for (int i = 0; i < db.Units.Count; i++)
        {
            UnitData unit = db.Units[i];
            if (unit == null || unit.militaryForce != wanted)
                continue;

            int index = offeredUnitsProp.arraySize;
            offeredUnitsProp.InsertArrayElementAtIndex(index);
            SerializedProperty elem = offeredUnitsProp.GetArrayElementAtIndex(index);
            if (elem != null)
            {
                elem.objectReferenceValue = unit;
                copied++;
            }
        }

        return copied;
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren: true);
    }

}
