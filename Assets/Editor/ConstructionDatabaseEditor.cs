using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionDatabase))]
public class ConstructionDatabaseEditor : Editor
{
    private SerializedProperty constructionsProp;
    private SerializedProperty fieldEntriesProp;
    private ConstructionFieldDatabase legacyFieldDatabase;

    private void OnEnable()
    {
        constructionsProp = serializedObject.FindProperty("constructions");
        fieldEntriesProp = serializedObject.FindProperty("fieldEntries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(constructionsProp, new GUIContent("Constructions"), includeChildren: true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Field Entries (Map Scope)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Layout de construcoes deste mapa centralizado no proprio ConstructionDatabase.",
            MessageType.Info);

        legacyFieldDatabase = (ConstructionFieldDatabase)EditorGUILayout.ObjectField(
            "Legacy Field Database",
            legacyFieldDatabase,
            typeof(ConstructionFieldDatabase),
            false);

        if (GUILayout.Button("Migrate From Legacy Field Database"))
            MigrateFromLegacyFieldDatabase();

        DrawFieldEntriesList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFieldEntriesList()
    {
        if (fieldEntriesProp == null)
            return;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Field Entries", EditorStyles.boldLabel);
        if (GUILayout.Button("Add Entry", GUILayout.MaxWidth(90f)))
            fieldEntriesProp.arraySize += 1;
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < fieldEntriesProp.arraySize; i++)
        {
            SerializedProperty entry = fieldEntriesProp.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty idProp = entry.FindPropertyRelative("id");
            string label = !string.IsNullOrWhiteSpace(idProp != null ? idProp.stringValue : string.Empty)
                ? idProp.stringValue
                : $"Entry {i + 1}";

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, label, true);

            if (GUILayout.Button("Rect", GUILayout.MaxWidth(56f)))
                SelectEntryInstance(entry, useRectTool: true);

            if (GUILayout.Button("Select", GUILayout.MaxWidth(62f)))
                SelectEntryInstance(entry, useRectTool: false);

            if (GUILayout.Button("Remove", GUILayout.MaxWidth(70f)))
            {
                fieldEntriesProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndHorizontal();

            if (entry.isExpanded)
                EditorGUILayout.PropertyField(entry, includeChildren: true);

            EditorGUILayout.EndVertical();
        }
    }

    private void MigrateFromLegacyFieldDatabase()
    {
        ConstructionDatabase db = (ConstructionDatabase)target;
        if (db == null || fieldEntriesProp == null)
            return;

        IReadOnlyList<ConstructionFieldEntry> legacy = legacyFieldDatabase != null ? legacyFieldDatabase.Entries : null;
        if (legacy == null)
            return;

        Undo.RecordObject(db, "Migrate Legacy Construction Field");
        fieldEntriesProp.ClearArray();

        int added = 0;
        for (int i = 0; i < legacy.Count; i++)
        {
            ConstructionFieldEntry src = legacy[i];
            if (src == null || src.construction == null || string.IsNullOrWhiteSpace(src.construction.id))
                continue;

            if (!ContainsConstructionById(constructionsProp, src.construction.id))
                continue;

            int idx = fieldEntriesProp.arraySize;
            fieldEntriesProp.arraySize += 1;
            SerializedProperty entry = fieldEntriesProp.GetArrayElementAtIndex(idx);
            if (entry == null)
                continue;

            SetString(entry, "id", src.id);
            SetObject(entry, "construction", src.construction);
            SetInt(entry, "initialTeamId", (int)src.initialTeamId);
            SetVector3Int(entry, "cellPosition", new Vector3Int(src.cellPosition.x, src.cellPosition.y, 0));
            SetInt(entry, "initialCapturePoints", src.initialCapturePoints);
            SetBool(entry, "useConstructionConfigurationOverride", src.useConstructionConfigurationOverride);
            CopySiteRuntimeToProperty(src.constructionConfiguration, entry.FindPropertyRelative("constructionConfiguration"));
            added++;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(db);
        serializedObject.Update();
        Debug.Log($"[ConstructionDatabaseEditor] Migracao concluida para '{db.name}'. Entries copiadas: {added}.", db);
    }

    private static bool ContainsConstructionById(SerializedProperty constructions, string id)
    {
        if (constructions == null || string.IsNullOrWhiteSpace(id))
            return false;

        for (int i = 0; i < constructions.arraySize; i++)
        {
            SerializedProperty item = constructions.GetArrayElementAtIndex(i);
            ConstructionData data = item != null ? item.objectReferenceValue as ConstructionData : null;
            if (data == null)
                continue;
            if (string.Equals(data.id, id, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static void SetString(SerializedProperty parent, string name, string value)
    {
        SerializedProperty p = parent != null ? parent.FindPropertyRelative(name) : null;
        if (p != null)
            p.stringValue = value ?? string.Empty;
    }

    private static void SetObject(SerializedProperty parent, string name, Object value)
    {
        SerializedProperty p = parent != null ? parent.FindPropertyRelative(name) : null;
        if (p != null)
            p.objectReferenceValue = value;
    }

    private static void SetInt(SerializedProperty parent, string name, int value)
    {
        SerializedProperty p = parent != null ? parent.FindPropertyRelative(name) : null;
        if (p != null)
            p.intValue = value;
    }

    private static void SetBool(SerializedProperty parent, string name, bool value)
    {
        SerializedProperty p = parent != null ? parent.FindPropertyRelative(name) : null;
        if (p != null)
            p.boolValue = value;
    }

    private static void SetVector3Int(SerializedProperty parent, string name, Vector3Int value)
    {
        SerializedProperty p = parent != null ? parent.FindPropertyRelative(name) : null;
        if (p != null)
            p.vector3IntValue = value;
    }

    private static void CopySiteRuntimeToProperty(ConstructionSiteRuntime source, SerializedProperty destination)
    {
        if (destination == null)
            return;

        ConstructionSiteRuntime copy = source != null ? source.Clone() : new ConstructionSiteRuntime();
        copy.Sanitize();

        SetBool(destination, "isPlayerHeadQuarter", copy.isPlayerHeadQuarter);
        SetBool(destination, "isCapturable", copy.isCapturable);
        SetInt(destination, "capturePointsMax", copy.capturePointsMax);
        SetInt(destination, "capturedIncoming", copy.capturedIncoming);
        SetBool(destination, "canProvideSupplies", copy.canProvideSupplies);
        SetEnum(destination, "sellingRule", (int)copy.sellingRule);
        CopyObjectList(destination.FindPropertyRelative("offeredUnits"), copy.offeredUnits);
        CopyObjectList(destination.FindPropertyRelative("offeredServices"), copy.offeredServices);
        CopySupplyList(destination.FindPropertyRelative("offeredSupplies"), copy.offeredSupplies);
    }

    private static void SetEnum(SerializedProperty parent, string name, int enumValueIndex)
    {
        SerializedProperty p = parent != null ? parent.FindPropertyRelative(name) : null;
        if (p != null)
            p.enumValueIndex = enumValueIndex;
    }

    private static void CopyObjectList<T>(SerializedProperty destination, List<T> values) where T : Object
    {
        if (destination == null)
            return;

        destination.arraySize = values != null ? values.Count : 0;
        if (values == null)
            return;

        for (int i = 0; i < values.Count; i++)
        {
            SerializedProperty item = destination.GetArrayElementAtIndex(i);
            if (item != null)
                item.objectReferenceValue = values[i];
        }
    }

    private static void CopySupplyList(SerializedProperty destination, List<ConstructionSupplyOffer> values)
    {
        if (destination == null)
            return;

        destination.arraySize = values != null ? values.Count : 0;
        if (values == null)
            return;

        for (int i = 0; i < values.Count; i++)
        {
            SerializedProperty item = destination.GetArrayElementAtIndex(i);
            if (item == null)
                continue;

            ConstructionSupplyOffer offer = values[i];
            SetObject(item, "supply", offer != null ? offer.supply : null);
            SetInt(item, "quantity", offer != null ? offer.quantity : 0);
        }
    }

    private static void SelectEntryInstance(SerializedProperty entry, bool useRectTool)
    {
        ConstructionManager instance = FindConstructionManagerForEntry(entry);
        if (instance == null || instance.gameObject == null)
        {
            Debug.LogWarning("[ConstructionDatabaseEditor] Nenhuma construcao instanciada encontrada para este Field Entry.");
            return;
        }

        Selection.activeGameObject = instance.gameObject;
        EditorGUIUtility.PingObject(instance.gameObject);
        if (useRectTool)
            Tools.current = Tool.Rect;
    }

    private static ConstructionManager FindConstructionManagerForEntry(SerializedProperty entry)
    {
        if (entry == null)
            return null;

        SerializedProperty idProp = entry.FindPropertyRelative("id");
        SerializedProperty cellProp = entry.FindPropertyRelative("cellPosition");

        string id = idProp != null ? idProp.stringValue : string.Empty;
        Vector3Int cell = cellProp != null ? cellProp.vector3IntValue : Vector3Int.zero;
        cell.z = 0;

        ConstructionManager[] managers = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < managers.Length; i++)
        {
            ConstructionManager manager = managers[i];
            if (manager == null || !manager.gameObject.activeInHierarchy)
                continue;

            Vector3Int managerCell = manager.CurrentCellPosition;
            managerCell.z = 0;
            if (managerCell == cell)
                return manager;
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            for (int i = 0; i < managers.Length; i++)
            {
                ConstructionManager manager = managers[i];
                if (manager == null || !manager.gameObject.activeInHierarchy)
                    continue;

                if (string.Equals(manager.gameObject.name, id, System.StringComparison.Ordinal))
                    return manager;
            }
        }

        return null;
    }
}
