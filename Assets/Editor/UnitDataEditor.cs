using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitData))]
public class UnitDataEditor : Editor
{
    private SerializedProperty embarkedWeaponsProperty;
    private SerializedProperty embarkedSuppliesProperty;
    private SerializedProperty isSupplierProperty;
    private SerializedProperty supplierTierProperty;
    private SerializedProperty maxUnitsServedPerTurnProperty;
    private SerializedProperty serviceRangeProperty;
    private SerializedProperty collectionRangeProperty;
    private SerializedProperty supplierOperationDomainsProperty;
    private SerializedProperty supplierServicesProvidedProperty;

    private void OnEnable()
    {
        embarkedWeaponsProperty = serializedObject.FindProperty("embarkedWeapons");
        embarkedSuppliesProperty = serializedObject.FindProperty("embarkedSupplies");
        isSupplierProperty = serializedObject.FindProperty("isSupplier");
        supplierTierProperty = serializedObject.FindProperty("supplierTier");
        maxUnitsServedPerTurnProperty = serializedObject.FindProperty("maxUnitsServedPerTurn");
        serviceRangeProperty = serializedObject.FindProperty("serviceRange");
        collectionRangeProperty = serializedObject.FindProperty("collectionRange");
        supplierOperationDomainsProperty = serializedObject.FindProperty("supplierOperationDomains");
        supplierServicesProvidedProperty = serializedObject.FindProperty("supplierServicesProvided");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPrimaryIdentitySection();
        DrawTopAttributesSection();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "id",
            "displayName",
            "militaryForce",
            "unitClass",
            "maxHP",
            "defense",
            "armorClass",
            "embarkedWeapons",
            "embarkedSupplies",
            "isSupplier",
            "supplierTier",
            "maxUnitsServedPerTurn",
            "serviceRange",
            "collectionRange",
            "supplierOperationDomains",
            "supplierServicesProvided");
        EditorGUILayout.Space();
        DrawEmbarkedWeaponsSection();
        EditorGUILayout.Space();
        DrawLogisticsSection();

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawIfExists(SerializedProperty property, string label)
    {
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren: true);
    }

    private void DrawLogisticsSection()
    {
        EditorGUILayout.LabelField("Logistics", EditorStyles.boldLabel);
        if (isSupplierProperty != null)
            EditorGUILayout.PropertyField(isSupplierProperty, new GUIContent("Is Supplier"));
        if (supplierTierProperty != null)
            EditorGUILayout.PropertyField(supplierTierProperty, new GUIContent("Supplier Tier"));
        if (maxUnitsServedPerTurnProperty != null)
            EditorGUILayout.PropertyField(maxUnitsServedPerTurnProperty, new GUIContent("Max Units Served Per Turn"));
        if (serviceRangeProperty != null)
            EditorGUILayout.PropertyField(serviceRangeProperty, new GUIContent("Service Range"));
        if (collectionRangeProperty != null)
            EditorGUILayout.PropertyField(collectionRangeProperty, new GUIContent("Collection Range"));

        if (supplierOperationDomainsProperty != null)
            EditorGUILayout.PropertyField(supplierOperationDomainsProperty, new GUIContent("Supplier Operation Domain"), includeChildren: true);
        if (supplierServicesProvidedProperty != null)
            EditorGUILayout.PropertyField(supplierServicesProvidedProperty, new GUIContent("Supplier Services Provided"), includeChildren: true);

        EditorGUILayout.Space();
        DrawEmbarkedSuppliesSection();
    }

    private void DrawPrimaryIdentitySection()
    {
        SerializedProperty idProperty = serializedObject.FindProperty("id");
        SerializedProperty displayNameProperty = serializedObject.FindProperty("displayName");
        SerializedProperty militaryForceProperty = serializedObject.FindProperty("militaryForce");
        SerializedProperty unitClassProperty = serializedObject.FindProperty("unitClass");

        if (idProperty != null)
            EditorGUILayout.PropertyField(idProperty);
        if (displayNameProperty != null)
            EditorGUILayout.PropertyField(displayNameProperty);
        if (militaryForceProperty != null)
            EditorGUILayout.PropertyField(militaryForceProperty);
        if (unitClassProperty != null)
            EditorGUILayout.PropertyField(unitClassProperty);
    }

    private void DrawTopAttributesSection()
    {
        SerializedProperty maxHpProperty = serializedObject.FindProperty("maxHP");
        SerializedProperty defenseProperty = serializedObject.FindProperty("defense");
        SerializedProperty armorClassProperty = serializedObject.FindProperty("armorClass");

        if (maxHpProperty != null)
            EditorGUILayout.PropertyField(maxHpProperty);
        if (defenseProperty != null)
            EditorGUILayout.PropertyField(defenseProperty);
        if (armorClassProperty != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(armorClassProperty, new GUIContent("Armor Class (Auto by Defense)"));
        }
    }

    private void DrawEmbarkedSuppliesSection()
    {
        EditorGUILayout.LabelField("Embarked Supplies", EditorStyles.boldLabel);
        SupplyDatabase database = ResolveSupplyDatabaseFromProject();
        List<SupplyData> catalog = BuildSupplyCatalog(database);
        if (database == null)
            EditorGUILayout.HelpBox("No SupplyDatabase found in project. Create one to drive supply slots.", MessageType.Warning);

        if (embarkedSuppliesProperty == null)
            return;

        for (int i = 0; i < embarkedSuppliesProperty.arraySize; i++)
        {
            SerializedProperty element = embarkedSuppliesProperty.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            SerializedProperty supplyProperty = element.FindPropertyRelative("supply");
            SerializedProperty amountProperty = element.FindPropertyRelative("amount");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Supply Slot {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                embarkedSuppliesProperty.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            DrawSupplyPopup(supplyProperty, catalog);
            if (amountProperty != null)
                EditorGUILayout.PropertyField(amountProperty, new GUIContent("Amount"));

            SupplyData selectedSupply = supplyProperty != null ? supplyProperty.objectReferenceValue as SupplyData : null;
            if (selectedSupply != null)
            {
                string functions = BuildSupplyFunctionText(selectedSupply);
                EditorGUILayout.HelpBox($"Functions: {functions}", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        if (GUILayout.Button("Add Supply Slot"))
            embarkedSuppliesProperty.InsertArrayElementAtIndex(embarkedSuppliesProperty.arraySize);
    }

    private void DrawEmbarkedWeaponsSection()
    {
        EditorGUILayout.LabelField("Embarked Weapons", EditorStyles.boldLabel);
        WeaponDatabase database = ResolveWeaponDatabaseFromProject();
        List<WeaponData> catalog = BuildCatalog(database);
        if (database == null)
            EditorGUILayout.HelpBox("No WeaponDatabase found in project. Create one to drive weapon slots.", MessageType.Warning);

        if (embarkedWeaponsProperty == null)
            return;

        for (int i = 0; i < embarkedWeaponsProperty.arraySize; i++)
        {
            SerializedProperty element = embarkedWeaponsProperty.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            SerializedProperty weaponProperty = element.FindPropertyRelative("weapon");
            SerializedProperty squadAmmunitionProperty = element.FindPropertyRelative("squadAmmunition");
            SerializedProperty operationRangeMinProperty = element.FindPropertyRelative("operationRangeMin");
            SerializedProperty operationRangeMaxProperty = element.FindPropertyRelative("operationRangeMax");
            SerializedProperty selectedTrajectoryProperty = element.FindPropertyRelative("selectedTrajectory");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Weapon Slot {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                embarkedWeaponsProperty.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            DrawWeaponPopup(weaponProperty, catalog);

            WeaponData selectedWeapon = weaponProperty != null ? weaponProperty.objectReferenceValue as WeaponData : null;
            using (new EditorGUI.DisabledScope(true))
            {
                int basicAttack = selectedWeapon != null ? selectedWeapon.basicAttack : 0;
                EditorGUILayout.IntField("Weapon Attack (Info)", basicAttack);
            }

            if (squadAmmunitionProperty != null)
                EditorGUILayout.PropertyField(squadAmmunitionProperty, new GUIContent("Squad Ammunition"));

            if (operationRangeMinProperty != null)
                EditorGUILayout.PropertyField(operationRangeMinProperty, new GUIContent("Operation Range Min"));
            if (operationRangeMaxProperty != null)
                EditorGUILayout.PropertyField(operationRangeMaxProperty, new GUIContent("Operation Range Max"));
            DrawTrajectoryPopup(selectedWeapon, selectedTrajectoryProperty);

            if (selectedWeapon != null)
            {
                string trajectories = selectedWeapon.trajectories != null && selectedWeapon.trajectories.Count > 0
                    ? string.Join(", ", selectedWeapon.trajectories)
                    : WeaponTrajectoryType.Straight.ToString();
                EditorGUILayout.HelpBox(
                    $"Weapon Default Range: {selectedWeapon.operationRangeMin}-{selectedWeapon.operationRangeMax} | Trajectories: {trajectories}",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        if (GUILayout.Button("Add Weapon Slot"))
            embarkedWeaponsProperty.InsertArrayElementAtIndex(embarkedWeaponsProperty.arraySize);
    }

    private static List<WeaponData> BuildCatalog(WeaponDatabase database)
    {
        List<WeaponData> catalog = new List<WeaponData>();
        if (database == null || database.Weapons == null)
            return catalog;

        for (int i = 0; i < database.Weapons.Count; i++)
        {
            WeaponData weapon = database.Weapons[i];
            if (weapon != null)
                catalog.Add(weapon);
        }

        return catalog;
    }

    private static void DrawWeaponPopup(SerializedProperty weaponProperty, List<WeaponData> catalog)
    {
        if (weaponProperty == null)
            return;

        if (catalog == null || catalog.Count == 0)
        {
            EditorGUILayout.PropertyField(weaponProperty, new GUIContent("Weapon"));
            return;
        }

        int selectedIndex = 0;
        WeaponData currentWeapon = weaponProperty.objectReferenceValue as WeaponData;

        string[] options = new string[catalog.Count + 1];
        options[0] = "(None)";
        for (int i = 0; i < catalog.Count; i++)
        {
            WeaponData optionWeapon = catalog[i];
            string label = !string.IsNullOrWhiteSpace(optionWeapon.displayName) ? optionWeapon.displayName : optionWeapon.name;
            options[i + 1] = $"{label} [{optionWeapon.id}]";
            if (optionWeapon == currentWeapon)
                selectedIndex = i + 1;
        }

        int newIndex = EditorGUILayout.Popup("Weapon", selectedIndex, options);
        WeaponData newWeapon = newIndex <= 0 ? null : catalog[newIndex - 1];
        if (newWeapon != currentWeapon)
            weaponProperty.objectReferenceValue = newWeapon;
    }

    private static void DrawTrajectoryPopup(WeaponData selectedWeapon, SerializedProperty trajectoryProperty)
    {
        if (trajectoryProperty == null)
            return;

        if (selectedWeapon == null || selectedWeapon.trajectories == null || selectedWeapon.trajectories.Count == 0)
        {
            trajectoryProperty.enumValueIndex = (int)WeaponTrajectoryType.Straight;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.EnumPopup("Selected Trajectory", WeaponTrajectoryType.Straight);
            return;
        }

        List<WeaponTrajectoryType> options = new List<WeaponTrajectoryType>(selectedWeapon.trajectories.Count);
        for (int i = 0; i < selectedWeapon.trajectories.Count; i++)
        {
            WeaponTrajectoryType option = selectedWeapon.trajectories[i];
            if (!options.Contains(option))
                options.Add(option);
        }

        if (options.Count == 0)
            options.Add(WeaponTrajectoryType.Straight);

        WeaponTrajectoryType current = (WeaponTrajectoryType)trajectoryProperty.enumValueIndex;
        int selectedIndex = 0;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == current)
            {
                selectedIndex = i;
                break;
            }
        }

        string[] labels = new string[options.Count];
        for (int i = 0; i < options.Count; i++)
            labels[i] = options[i].ToString();

        int newIndex = EditorGUILayout.Popup("Selected Trajectory", selectedIndex, labels);
        trajectoryProperty.enumValueIndex = (int)options[Mathf.Clamp(newIndex, 0, options.Count - 1)];
    }

    private static WeaponDatabase ResolveWeaponDatabaseFromProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:WeaponDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            WeaponDatabase db = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(path);
            if (db != null)
                return db;
        }

        return null;
    }

    private static List<SupplyData> BuildSupplyCatalog(SupplyDatabase database)
    {
        List<SupplyData> catalog = new List<SupplyData>();
        if (database == null || database.Supplies == null)
            return catalog;

        for (int i = 0; i < database.Supplies.Count; i++)
        {
            SupplyData supply = database.Supplies[i];
            if (supply != null)
                catalog.Add(supply);
        }

        return catalog;
    }

    private static void DrawSupplyPopup(SerializedProperty supplyProperty, List<SupplyData> catalog)
    {
        if (supplyProperty == null)
            return;

        if (catalog == null || catalog.Count == 0)
        {
            EditorGUILayout.PropertyField(supplyProperty, new GUIContent("Supply"));
            return;
        }

        int selectedIndex = 0;
        SupplyData currentSupply = supplyProperty.objectReferenceValue as SupplyData;

        string[] options = new string[catalog.Count + 1];
        options[0] = "(None)";
        for (int i = 0; i < catalog.Count; i++)
        {
            SupplyData optionSupply = catalog[i];
            string label = !string.IsNullOrWhiteSpace(optionSupply.displayName) ? optionSupply.displayName : optionSupply.name;
            options[i + 1] = $"{label} [{optionSupply.id}]";
            if (optionSupply == currentSupply)
                selectedIndex = i + 1;
        }

        int newIndex = EditorGUILayout.Popup("Supply", selectedIndex, options);
        SupplyData newSupply = newIndex <= 0 ? null : catalog[newIndex - 1];
        if (newSupply != currentSupply)
            supplyProperty.objectReferenceValue = newSupply;
    }

    private static string BuildSupplyFunctionText(SupplyData supply)
    {
        if (supply == null || supply.relatedServices == null || supply.relatedServices.Count == 0)
            return "None";

        List<string> labels = new List<string>(supply.relatedServices.Count);
        for (int i = 0; i < supply.relatedServices.Count; i++)
        {
            ServiceData service = supply.relatedServices[i];
            if (service == null)
                continue;

            string label = !string.IsNullOrWhiteSpace(service.displayName)
                ? service.displayName
                : service.name;
            string id = !string.IsNullOrWhiteSpace(service.id) ? service.id : "-";
            labels.Add($"{label} [{id}]");
        }

        if (labels.Count == 0)
            return "None";

        return string.Join(", ", labels);
    }

    private static SupplyDatabase ResolveSupplyDatabaseFromProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:SupplyDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SupplyDatabase db = AssetDatabase.LoadAssetAtPath<SupplyDatabase>(path);
            if (db != null)
                return db;
        }

        return null;
    }
}
