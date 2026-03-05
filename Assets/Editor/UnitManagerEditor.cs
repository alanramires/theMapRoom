using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

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
    private SerializedProperty currentFuelProp;
    private SerializedProperty maxFuelProp;
    private SerializedProperty remainingMovementPointsProp;
    private SerializedProperty visaoProp;
    private SerializedProperty embarkedWeaponsRuntimeProp;
    private SerializedProperty embarkedResourcesRuntimeProp;
    private SerializedProperty embarkedServicesRuntimeProp;
    private SerializedProperty hasActedProp;
    private SerializedProperty receivedSuppliesThisTurnProp;
    private SerializedProperty isEmbarkedProp;
    private SerializedProperty matchControllerProp;
    private SerializedProperty currentDomainProp;
    private SerializedProperty currentHeightLevelProp;
    private SerializedProperty currentLayerModeIndexProp;
    private SerializedProperty layerStateInitializedProp;
    private SerializedProperty useExplicitPreferredAirHeightRuntimeProp;
    private SerializedProperty preferredAirHeightRuntimeProp;
    private SerializedProperty useExplicitPreferredNavalHeightRuntimeProp;
    private SerializedProperty preferredNavalHeightRuntimeProp;

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
        currentFuelProp = serializedObject.FindProperty("currentFuel");
        maxFuelProp = serializedObject.FindProperty("maxFuel");
        remainingMovementPointsProp = serializedObject.FindProperty("remainingMovementPoints");
        visaoProp = serializedObject.FindProperty("visao");
        embarkedWeaponsRuntimeProp = serializedObject.FindProperty("embarkedWeaponsRuntime");
        embarkedResourcesRuntimeProp = serializedObject.FindProperty("embarkedResourcesRuntime");
        embarkedServicesRuntimeProp = serializedObject.FindProperty("embarkedServicesRuntime");
        hasActedProp = serializedObject.FindProperty("hasActed");
        receivedSuppliesThisTurnProp = serializedObject.FindProperty("receivedSuppliesThisTurn");
        isEmbarkedProp = serializedObject.FindProperty("isEmbarked");
        matchControllerProp = serializedObject.FindProperty("matchController");
        currentDomainProp = serializedObject.FindProperty("currentDomain");
        currentHeightLevelProp = serializedObject.FindProperty("currentHeightLevel");
        currentLayerModeIndexProp = serializedObject.FindProperty("currentLayerModeIndex");
        layerStateInitializedProp = serializedObject.FindProperty("layerStateInitialized");
        useExplicitPreferredAirHeightRuntimeProp = serializedObject.FindProperty("useExplicitPreferredAirHeightRuntime");
        preferredAirHeightRuntimeProp = serializedObject.FindProperty("preferredAirHeightRuntime");
        useExplicitPreferredNavalHeightRuntimeProp = serializedObject.FindProperty("useExplicitPreferredNavalHeightRuntime");
        preferredNavalHeightRuntimeProp = serializedObject.FindProperty("preferredNavalHeightRuntime");
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
        UnitManager unit = (UnitManager)target;

        EditorGUILayout.PropertyField(spriteRendererProp);
        EditorGUILayout.PropertyField(unitHudProp, new GUIContent("Unit HUD"));
        EditorGUILayout.PropertyField(unitDatabaseProp);
        EditorGUILayout.PropertyField(matchControllerProp, new GUIContent("Match Controller"));
        EditorGUILayout.PropertyField(boardTilemapProp, new GUIContent("Board Tilemap"));
        EditorGUILayout.PropertyField(snapToCellCenterProp, new GUIContent("Snap To Cell Center"));
        EditorGUILayout.PropertyField(autoSnapWhenMovedInEditorProp, new GUIContent("Auto Snap When Moved In Editor"));
        EditorGUILayout.PropertyField(currentCellPositionProp, new GUIContent("Cell Position"));
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(hasActedProp, new GUIContent("Has Acted"));
            if (receivedSuppliesThisTurnProp != null)
                EditorGUILayout.PropertyField(receivedSuppliesThisTurnProp, new GUIContent("Received Supply This Turn"));
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Movement Max", Mathf.Max(0, unit.MaxMovementPoints));
        }
        if (remainingMovementPointsProp != null)
        {
            int maxMovement = Mathf.Max(0, unit.MaxMovementPoints);
            remainingMovementPointsProp.intValue = EditorGUILayout.IntSlider(
                "Movement Remaining",
                remainingMovementPointsProp.intValue,
                0,
                maxMovement);
        }
        else
            EditorGUILayout.IntField("Movement Remaining", 0);
        if (isEmbarkedProp != null)
            EditorGUILayout.PropertyField(isEmbarkedProp, new GUIContent("Is Embarked"));

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

        maxFuelProp.intValue = Mathf.Max(1, maxFuelProp.intValue);
        currentFuelProp.intValue = Mathf.Clamp(currentFuelProp.intValue, 0, maxFuelProp.intValue);

        EditorGUILayout.IntSlider(currentFuelProp, 0, Mathf.Max(1, maxFuelProp.intValue), new GUIContent("Current Fuel"));
        EditorGUILayout.PropertyField(maxFuelProp, new GUIContent("Max Fuel"));
        if (visaoProp != null)
            EditorGUILayout.IntSlider(visaoProp, 1, 12, new GUIContent("Visao (hex)"));
        DrawEmbarkedWeaponsRuntimeSection();
        DrawSupplierRuntimeSection(unit);
        DrawTransportRuntimeSection((UnitManager)target);

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
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Aircraft Grounded", unit.IsAircraftGrounded);
            EditorGUILayout.Toggle("Aircraft Embarked In Carrier", unit.IsAircraftEmbarkedInCarrier);
            EditorGUILayout.IntField("Aircraft Operation Lock Turns", unit.AircraftOperationLockTurns);
            EditorGUILayout.EnumPopup("Preferred Air Height (Resolved)", unit.GetPreferredAirHeight());
            if (useExplicitPreferredAirHeightRuntimeProp != null)
                EditorGUILayout.PropertyField(useExplicitPreferredAirHeightRuntimeProp, new GUIContent("Use Explicit Preferred Air Height (Runtime)"));
            if (preferredAirHeightRuntimeProp != null)
                EditorGUILayout.PropertyField(preferredAirHeightRuntimeProp, new GUIContent("Preferred Air Height (Runtime)"));
            if (useExplicitPreferredNavalHeightRuntimeProp != null)
                EditorGUILayout.PropertyField(useExplicitPreferredNavalHeightRuntimeProp, new GUIContent("Use Explicit Preferred Naval Height (Runtime)"));
            if (preferredNavalHeightRuntimeProp != null)
                EditorGUILayout.PropertyField(preferredNavalHeightRuntimeProp, new GUIContent("Preferred Naval Height (Runtime)"));
        }

        UnitData resolvedData = null;
        if (unit.TryGetUnitData(out resolvedData) && resolvedData != null)
        {
            string prefSource = resolvedData.useExplicitPreferredAirHeight
                ? $"Override UnitData: {resolvedData.preferredAirHeight}"
                : "Derivado automaticamente de Domain/Height + Additional Domains.";
            EditorGUILayout.HelpBox($"Preferencia aerea: {prefSource}", MessageType.None);
        }

        serializedObject.ApplyModifiedProperties();

        DrawLayerStateCycleButtons(unit);

        if (GUILayout.Button("Apply From Database"))
            unit.ApplyFromDatabase();
        if (GUILayout.Button("Snap To Cell Center"))
            unit.SnapToCellCenter();
        if (GUILayout.Button("Pull Cell From Transform"))
            unit.PullCellFromTransform();
    }

    private void DrawTransportRuntimeSection(UnitManager unit)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Transport Runtime", EditorStyles.boldLabel);
        if (unit == null)
            return;

        UnitData data = null;
        bool hasData = unit.TryGetUnitData(out data) && data != null;
        bool isTransporter = hasData && data.isTransporter && data.transportSlots != null && data.transportSlots.Count > 0;
        if (!isTransporter)
        {
            EditorGUILayout.HelpBox("Unidade atual nao eh transportadora (ou sem slots configurados no UnitData).", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Slots From Data"))
            {
                Undo.RecordObject(unit, "Refresh Transport Slots");
                unit.RefreshTransportSlotsFromData();
                MarkUnitAndSceneDirty(unit);
                serializedObject.Update();
            }

            if (GUILayout.Button("Limpar Todas as Vagas"))
            {
                Undo.RecordObject(unit, "Clear Transport Seats");
                bool changed = false;
                IReadOnlyList<UnitTransportSeatRuntime> seats = unit.TransportedUnitSlots;
                for (int i = 0; i < seats.Count; i++)
                {
                    UnitTransportSeatRuntime seat = seats[i];
                    if (seat == null)
                        continue;

                    if (unit.TryDisembarkPassengerFromSeat(seat.slotIndex, seat.seatIndex, out UnitManager removed, out _))
                    {
                        changed = true;
                        if (removed != null)
                            MarkUnitAndSceneDirty(removed);
                    }
                }

                if (changed)
                    MarkUnitAndSceneDirty(unit);
                serializedObject.Update();
            }
        }

        IReadOnlyList<UnitTransportSeatRuntime> runtimeSeats = unit.TransportedUnitSlots;
        if (runtimeSeats == null || runtimeSeats.Count == 0)
        {
            EditorGUILayout.HelpBox("Sem vagas runtime. Clique em 'Refresh Slots From Data'.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < runtimeSeats.Count; i++)
        {
            UnitTransportSeatRuntime seat = runtimeSeats[i];
            if (seat == null)
                continue;

            string seatLabel = $"{seat.slotId} | vaga {seat.seatIndex + 1}";
            UnitManager occupied = seat.embarkedUnit;
            string status = occupied != null ? $"ocupada ({occupied.name})" : "livre";

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"{seatLabel} - {status}", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            UnitManager desired = (UnitManager)EditorGUILayout.ObjectField("Passageiro", occupied, typeof(UnitManager), true);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(unit, "Assign Transport Seat");
                if (occupied != null)
                    Undo.RecordObject(occupied, "Assign Transport Seat");
                if (desired != null && desired != occupied)
                    Undo.RecordObject(desired, "Assign Transport Seat");

                if (desired == null)
                {
                    if (!unit.TryDisembarkPassengerFromSeat(seat.slotIndex, seat.seatIndex, out UnitManager removed, out string reason))
                    {
                        if (!string.IsNullOrWhiteSpace(reason))
                            Debug.LogWarning($"[UnitManagerEditor] Falha no desembarque manual: {reason}", unit);
                    }
                    else
                    {
                        MarkUnitAndSceneDirty(unit);
                        if (removed != null)
                            MarkUnitAndSceneDirty(removed);
                    }
                }
                else
                {
                    UnitManager replaced = null;
                    if (occupied != null && occupied != desired)
                        unit.TryDisembarkPassengerFromSeat(seat.slotIndex, seat.seatIndex, out replaced, out _);

                    if (!unit.TryEmbarkPassengerInSeat(desired, seat.slotIndex, seat.seatIndex, out string reason))
                    {
                        Debug.LogWarning($"[UnitManagerEditor] Falha no embarque manual: {reason}", unit);
                    }
                    else
                    {
                        MarkUnitAndSceneDirty(unit);
                        if (desired != null)
                            MarkUnitAndSceneDirty(desired);
                        if (replaced != null)
                            MarkUnitAndSceneDirty(replaced);
                    }
                }

                serializedObject.Update();
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawSupplierRuntimeSection(UnitManager unit)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Supplier Runtime", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Embarked Supplies e Embarked Services podem ser ajustados por instancia (runtime/debug).", MessageType.None);

        if (unit == null)
            return;

        UnitData data = null;
        bool hasData = unit.TryGetUnitData(out data) && data != null;
        bool isSupplier = hasData && data.isSupplier;
        if (!isSupplier)
        {
            EditorGUILayout.HelpBox("Unidade atual nao esta marcada como supplier no UnitData.", MessageType.Info);
            return;
        }

        DrawEmbarkedResourcesRuntimeSection(data);
        DrawEmbarkedServicesRuntimeSection();
    }

    private void DrawLayerStateCycleButtons(UnitManager unit)
    {
        if (unit == null)
            return;

        System.Collections.Generic.List<UnitLayerMode> orderedModes = BuildOrderedLayerModes(unit);
        int modeCount = orderedModes.Count;
        int currentIndex = ResolveCurrentLayerModeIndex(unit, orderedModes);
        bool canGoDown = currentIndex > 0;
        bool canGoUp = currentIndex >= 0 && currentIndex < modeCount - 1;
        bool canDebugGoDown = Application.isPlaying && CanStepLayerStateForDebug(unit, -1);
        bool canDebugGoUp = Application.isPlaying && CanStepLayerStateForDebug(unit, +1);
        bool allowDown = (modeCount > 1 && canGoDown) || canDebugGoDown;
        bool allowUp = (modeCount > 1 && canGoUp) || canDebugGoUp;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Layer State Controls", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!allowDown))
        {
            if (GUILayout.Button("Descer Domain (D)"))
                TryStepLayerModeWithDebugFallback(unit, orderedModes, currentIndex, -1);
        }

        using (new EditorGUI.DisabledScope(!allowUp))
        {
            if (GUILayout.Button("Subir Domain (S)"))
                TryStepLayerModeWithDebugFallback(unit, orderedModes, currentIndex, +1);
        }
        EditorGUILayout.EndHorizontal();

        Event currentEvent = Event.current;
        if (currentEvent != null &&
            currentEvent.type == EventType.KeyDown &&
            !currentEvent.alt &&
            !currentEvent.control &&
            !currentEvent.command &&
            !currentEvent.shift &&
            !EditorGUIUtility.editingTextField)
        {
            if (currentEvent.keyCode == KeyCode.D && allowDown)
            {
                TryStepLayerModeWithDebugFallback(unit, orderedModes, currentIndex, -1);
                currentEvent.Use();
                GUI.changed = true;
            }
            else if (currentEvent.keyCode == KeyCode.S && allowUp)
            {
                TryStepLayerModeWithDebugFallback(unit, orderedModes, currentIndex, +1);
                currentEvent.Use();
                GUI.changed = true;
            }
        }

        if (modeCount > 0)
        {
            UnitLayerMode current = orderedModes[Mathf.Clamp(currentIndex, 0, modeCount - 1)];
            EditorGUILayout.HelpBox(
                $"Layer atual: {current.domain}/{current.heightLevel} ({currentIndex + 1}/{modeCount})",
                MessageType.None);
        }
    }

    private static bool CanStepLayerStateForDebug(UnitManager unit, int delta)
    {
        if (unit == null || unit.GetAircraftType() == AircraftType.None || delta == 0)
            return false;

        Domain domain = unit.GetDomain();
        HeightLevel height = unit.GetHeightLevel();

        if (delta < 0)
            return domain == Domain.Air && (height == HeightLevel.AirHigh || height == HeightLevel.AirLow);

        return domain != Domain.Air || height == HeightLevel.AirLow;
    }

    private static void TryStepLayerModeWithDebugFallback(UnitManager unit, System.Collections.Generic.IReadOnlyList<UnitLayerMode> orderedModes, int currentIndex, int delta)
    {
        if (unit == null || delta == 0)
            return;

        bool applied = false;
        if (orderedModes != null && orderedModes.Count > 1)
        {
            int clampedCurrent = Mathf.Clamp(currentIndex, 0, orderedModes.Count - 1);
            int next = clampedCurrent + delta;
            if (next >= 0 && next < orderedModes.Count)
            {
                UnitLayerMode targetMode = orderedModes[next];
                Undo.RecordObject(unit, "Cycle Unit Layer State");
                applied = unit.TrySetCurrentLayerMode(targetMode.domain, targetMode.heightLevel);
            }
        }

        if (!applied && Application.isPlaying && CanStepLayerStateForDebug(unit, delta))
        {
            Undo.RecordObject(unit, "Cycle Unit Layer State (Debug)");
            applied = unit.TryStepLayerStateForDebug(delta);
        }

        if (!applied)
            return;

        EditorUtility.SetDirty(unit);
        PrefabUtility.RecordPrefabInstancePropertyModifications(unit);
        if (unit.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(unit.gameObject.scene);
    }

    private static System.Collections.Generic.List<UnitLayerMode> BuildOrderedLayerModes(UnitManager unit)
    {
        var ordered = new System.Collections.Generic.List<UnitLayerMode>();
        if (unit == null)
            return ordered;

        var modes = unit.GetAllLayerModes();
        if (modes == null)
            return ordered;

        for (int i = 0; i < modes.Count; i++)
            ordered.Add(modes[i]);

        ordered.Sort((a, b) =>
        {
            int byHeight = ((int)a.heightLevel).CompareTo((int)b.heightLevel);
            if (byHeight != 0)
                return byHeight;

            return ((int)a.domain).CompareTo((int)b.domain);
        });

        return ordered;
    }

    private static int ResolveCurrentLayerModeIndex(UnitManager unit, System.Collections.Generic.IReadOnlyList<UnitLayerMode> modes)
    {
        if (unit == null || modes == null || modes.Count == 0)
            return 0;

        UnitLayerMode current = unit.GetCurrentLayerMode();
        for (int i = 0; i < modes.Count; i++)
        {
            if (modes[i].domain == current.domain && modes[i].heightLevel == current.heightLevel)
                return i;
        }

        return 0;
    }

    private static void StepLayerMode(UnitManager unit, System.Collections.Generic.IReadOnlyList<UnitLayerMode> orderedModes, int currentIndex, int delta)
    {
        if (unit == null || orderedModes == null || orderedModes.Count <= 1)
            return;

        int clampedCurrent = Mathf.Clamp(currentIndex, 0, orderedModes.Count - 1);
        int next = clampedCurrent + delta;
        if (next < 0 || next >= orderedModes.Count)
            return;

        UnitLayerMode targetMode = orderedModes[next];

        Undo.RecordObject(unit, "Cycle Unit Layer State");
        if (!unit.TrySetCurrentLayerMode(targetMode.domain, targetMode.heightLevel))
            return;

        EditorUtility.SetDirty(unit);
        PrefabUtility.RecordPrefabInstancePropertyModifications(unit);
        if (unit.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(unit.gameObject.scene);
    }

    private void DrawEmbarkedWeaponsRuntimeSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Embarked Weapons (Runtime)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("UnitData define os valores base. Aqui voce ajusta estado da instancia em campo (municao e alcance).", MessageType.None);
        UnitManager unit = target as UnitManager;
        UnitData unitData = null;
        bool hasUnitData = unit != null && unit.TryGetUnitData(out unitData) && unitData != null;
        if (embarkedWeaponsRuntimeProp != null)
            embarkedWeaponsRuntimeProp.isExpanded = true;

        if (embarkedWeaponsRuntimeProp == null || !embarkedWeaponsRuntimeProp.isArray || embarkedWeaponsRuntimeProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No embarked weapons on this instance.", MessageType.Info);
            if (GUILayout.Button("Add Weapon Slot"))
                embarkedWeaponsRuntimeProp.InsertArrayElementAtIndex(embarkedWeaponsRuntimeProp.arraySize);
            return;
        }

        for (int i = 0; i < embarkedWeaponsRuntimeProp.arraySize; i++)
        {
            SerializedProperty entry = embarkedWeaponsRuntimeProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty weaponProp = entry.FindPropertyRelative("weapon");
            SerializedProperty ammoProp = entry.FindPropertyRelative("squadAmmunition");
            SerializedProperty minProp = entry.FindPropertyRelative("operationRangeMin");
            SerializedProperty maxProp = entry.FindPropertyRelative("operationRangeMax");

            string weaponName = "(No Weapon)";
            if (weaponProp != null && weaponProp.objectReferenceValue != null)
            {
                WeaponData weapon = weaponProp.objectReferenceValue as WeaponData;
                if (weapon != null)
                    weaponName = string.IsNullOrWhiteSpace(weapon.displayName) ? weapon.name : weapon.displayName;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Slot {i + 1}: {weaponName}", EditorStyles.boldLabel);

            if (weaponProp != null)
                EditorGUILayout.PropertyField(weaponProp, new GUIContent("Weapon"));

            EditorGUILayout.LabelField("Squad Setup", EditorStyles.miniBoldLabel);
            if (ammoProp != null)
            {
                EditorGUILayout.PropertyField(ammoProp, new GUIContent("Ammo / Attacks Remaining"));
                ammoProp.intValue = Mathf.Max(0, ammoProp.intValue);
            }

            int maxAttacks = ResolveRuntimeWeaponMaxAttacks(unitData, i);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField(hasUnitData ? "Ammo / Attacks Max (UnitData)" : "Ammo / Attacks Max", maxAttacks);
            }

            if (minProp != null)
            {
                EditorGUILayout.PropertyField(minProp, new GUIContent("Range Min"));
                minProp.intValue = Mathf.Max(0, minProp.intValue);
            }

            if (maxProp != null)
            {
                EditorGUILayout.PropertyField(maxProp, new GUIContent("Range Max"));
                int minValue = minProp != null ? minProp.intValue : 0;
                maxProp.intValue = Mathf.Max(minValue, maxProp.intValue);
            }

            if (GUILayout.Button("Remove Slot"))
            {
                embarkedWeaponsRuntimeProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add Weapon Slot"))
            embarkedWeaponsRuntimeProp.InsertArrayElementAtIndex(embarkedWeaponsRuntimeProp.arraySize);
    }

    private static int ResolveRuntimeWeaponMaxAttacks(UnitData unitData, int runtimeWeaponIndex)
    {
        if (unitData == null || unitData.embarkedWeapons == null)
            return 0;
        if (runtimeWeaponIndex < 0 || runtimeWeaponIndex >= unitData.embarkedWeapons.Count)
            return 0;

        UnitEmbarkedWeapon baseline = unitData.embarkedWeapons[runtimeWeaponIndex];
        if (baseline == null)
            return 0;

        return Mathf.Max(0, baseline.squadAmmunition);
    }

    private void DrawEmbarkedResourcesRuntimeSection(UnitData unitData)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Embarked Supplies (Runtime)", EditorStyles.boldLabel);

        if (embarkedResourcesRuntimeProp == null || !embarkedResourcesRuntimeProp.isArray)
        {
            EditorGUILayout.HelpBox("Lista de recursos nao encontrada no UnitManager.", MessageType.Warning);
            return;
        }

        if (embarkedResourcesRuntimeProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No embarked supplies on this instance.", MessageType.Info);
            if (GUILayout.Button("Add Supply Slot"))
                embarkedResourcesRuntimeProp.InsertArrayElementAtIndex(embarkedResourcesRuntimeProp.arraySize);
            return;
        }

        for (int i = 0; i < embarkedResourcesRuntimeProp.arraySize; i++)
        {
            SerializedProperty entry = embarkedResourcesRuntimeProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty supplyProp = entry.FindPropertyRelative("supply");
            SerializedProperty amountProp = entry.FindPropertyRelative("amount");

            string supplyName = "(No Supply)";
            if (supplyProp != null && supplyProp.objectReferenceValue != null)
            {
                SupplyData supply = supplyProp.objectReferenceValue as SupplyData;
                if (supply != null)
                    supplyName = string.IsNullOrWhiteSpace(supply.displayName) ? supply.name : supply.displayName;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Slot {i + 1}: {supplyName}", EditorStyles.boldLabel);

            if (supplyProp != null)
                EditorGUILayout.PropertyField(supplyProp, new GUIContent("Supply"));
            if (amountProp != null)
            {
                EditorGUILayout.PropertyField(amountProp, new GUIContent("Amount"));
                amountProp.intValue = Mathf.Max(0, amountProp.intValue);
            }
            SupplyData selectedSupply = supplyProp != null ? supplyProp.objectReferenceValue as SupplyData : null;
            int maxAmount = ResolveRuntimeSupplyMaxAmount(unitData, selectedSupply);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Max Amount (UnitData)", maxAmount);

            if (GUILayout.Button("Remove Slot"))
            {
                embarkedResourcesRuntimeProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add Supply Slot"))
            embarkedResourcesRuntimeProp.InsertArrayElementAtIndex(embarkedResourcesRuntimeProp.arraySize);
    }

    private static int ResolveRuntimeSupplyMaxAmount(UnitData unitData, SupplyData supply)
    {
        if (unitData == null || supply == null || unitData.supplierResources == null)
            return 0;

        int total = 0;
        for (int i = 0; i < unitData.supplierResources.Count; i++)
        {
            UnitEmbarkedSupply entry = unitData.supplierResources[i];
            if (entry == null || entry.supply != supply)
                continue;
            total += Mathf.Max(0, entry.amount);
        }

        return Mathf.Max(0, total);
    }

    private void DrawEmbarkedServicesRuntimeSection()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Embarked Services (Runtime)", EditorStyles.boldLabel);

        if (embarkedServicesRuntimeProp == null || !embarkedServicesRuntimeProp.isArray)
        {
            EditorGUILayout.HelpBox("Lista de servicos nao encontrada no UnitManager.", MessageType.Warning);
            return;
        }

        if (embarkedServicesRuntimeProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No embarked services on this instance.", MessageType.Info);
            if (GUILayout.Button("Add Service Slot"))
                embarkedServicesRuntimeProp.InsertArrayElementAtIndex(embarkedServicesRuntimeProp.arraySize);
            return;
        }

        for (int i = 0; i < embarkedServicesRuntimeProp.arraySize; i++)
        {
            SerializedProperty serviceProp = embarkedServicesRuntimeProp.GetArrayElementAtIndex(i);
            if (serviceProp == null)
                continue;

            string serviceName = "(No Service)";
            if (serviceProp.objectReferenceValue != null)
            {
                ServiceData service = serviceProp.objectReferenceValue as ServiceData;
                if (service != null)
                    serviceName = string.IsNullOrWhiteSpace(service.displayName) ? service.name : service.displayName;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Slot {i + 1}: {serviceName}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serviceProp, new GUIContent("Service"));

            if (GUILayout.Button("Remove Slot"))
            {
                embarkedServicesRuntimeProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add Service Slot"))
            embarkedServicesRuntimeProp.InsertArrayElementAtIndex(embarkedServicesRuntimeProp.arraySize);
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

    private static void MarkUnitAndSceneDirty(UnitManager unit)
    {
        if (unit == null)
            return;

        EditorUtility.SetDirty(unit);
        PrefabUtility.RecordPrefabInstancePropertyModifications(unit);
        if (unit.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(unit.gameObject.scene);
    }
}
