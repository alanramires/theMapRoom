using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

public static class ConstructionDataTools
{
    private const string ApplyDataMenuPath = "Tools/Construction/Propagate Construction Data (Force Override)";
    private const string ConstructionPrefabPath = "Assets/Prefab/construction.prefab";

    [MenuItem(ApplyDataMenuPath)]
    private static void PropagateConstructionData()
    {
        GameObject constructionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConstructionPrefabPath);
        ConstructionManager[] constructions = UnityEngine.Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (constructions == null || constructions.Length == 0)
        {
            Debug.Log("[ConstructionDataTools] Nenhuma construcao encontrada na cena.");
            return;
        }

        int updated = 0;
        int hudUpdated = 0;
        int skipped = 0;
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
                continue;

            List<UnitData> preservedOfferedUnits = SnapshotOfferedUnits(construction);

            if (!construction.TryResolveConstructionData(out ConstructionData data) || data == null)
            {
                skipped++;
                continue;
            }

            data.SyncSupplierSettingsToConstructionConfiguration();
            EditorUtility.SetDirty(data);

            Undo.RecordObject(construction, "Propagate Construction Data");

            // Forca o manager a aceitar novamente o runtime default vindo do ConstructionData.
            SerializedObject so = new SerializedObject(construction);
            SerializedProperty hasOverrideProp = so.FindProperty("hasSiteRuntimeOverride");
            SerializedProperty captureProp = so.FindProperty("currentCapturePoints");
            SerializedProperty teamProp = so.FindProperty("teamId");
            SerializedProperty originalOwnerTeamProp = so.FindProperty("originalOwnerTeamId");
            SerializedProperty originalOwnerInitializedProp = so.FindProperty("originalOwnerInitialized");
            if (hasOverrideProp != null)
                hasOverrideProp.boolValue = false;
            // -1 garante reinit para o maximo quando aplicar os defaults.
            if (captureProp != null)
                captureProp.intValue = -1;
            if (teamProp != null && originalOwnerTeamProp != null)
                originalOwnerTeamProp.intValue = teamProp.intValue;
            if (originalOwnerInitializedProp != null)
                originalOwnerInitializedProp.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!construction.ApplyFromDatabase())
            {
                skipped++;
                continue;
            }

            RestoreOfferedUnits(construction, preservedOfferedUnits);

            if (PropagateHudLayoutForConstruction(constructionPrefab, construction))
                hudUpdated++;

            ConstructionHudController hud = construction.GetComponentInChildren<ConstructionHudController>(true);
            if (hud != null)
            {
                Undo.RecordObject(hud, "Propagate Construction HUD Refresh Bindings");
                hud.RefreshBindings();
                EditorUtility.SetDirty(hud);
            }

            construction.SetCurrentCapturePoints(construction.CapturePointsMax);
            EditorUtility.SetDirty(construction);
            updated++;
        }

        if (updated > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[ConstructionDataTools] {updated} construcao(oes) atualizada(s). HUD propagado em {hudUpdated} instancia(s). Ignoradas: {skipped}.");
    }

    private static List<UnitData> SnapshotOfferedUnits(ConstructionManager construction)
    {
        List<UnitData> snapshot = new List<UnitData>();
        if (construction == null)
            return snapshot;

        IReadOnlyList<UnitData> offered = construction.OfferedUnits;
        if (offered == null || offered.Count <= 0)
            return snapshot;

        for (int i = 0; i < offered.Count; i++)
        {
            UnitData unit = offered[i];
            if (unit != null)
                snapshot.Add(unit);
        }

        return snapshot;
    }

    private static void RestoreOfferedUnits(ConstructionManager construction, List<UnitData> preservedOfferedUnits)
    {
        if (construction == null)
            return;

        SerializedObject so = new SerializedObject(construction);
        SerializedProperty offeredUnitsProp = so.FindProperty("siteRuntime").FindPropertyRelative("offeredUnits");
        if (offeredUnitsProp == null)
            return;

        offeredUnitsProp.arraySize = 0;
        if (preservedOfferedUnits != null)
        {
            for (int i = 0; i < preservedOfferedUnits.Count; i++)
            {
                UnitData unit = preservedOfferedUnits[i];
                if (unit == null)
                    continue;

                int index = offeredUnitsProp.arraySize;
                offeredUnitsProp.InsertArrayElementAtIndex(index);
                offeredUnitsProp.GetArrayElementAtIndex(index).objectReferenceValue = unit;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool PropagateHudLayoutForConstruction(GameObject sourcePrefab, ConstructionManager construction)
    {
        if (sourcePrefab == null || construction == null)
            return false;

        GameObject targetGo = construction.gameObject;
        Scene scene = targetGo.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        bool changed = false;
        Transform sourceRoot = sourcePrefab.transform;
        Transform targetRoot = targetGo.transform;
        HashSet<string> allowedHudRootNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sourceRoot.childCount; i++)
        {
            Transform sourceChild = sourceRoot.GetChild(i);
            if (!IsHudRoot(sourceChild))
                continue;

            allowedHudRootNames.Add(sourceChild.name);
            Transform targetChild = FindDirectChildByName(targetRoot, sourceChild.name);
            if (targetChild == null)
            {
                Undo.RecordObject(targetRoot.gameObject, "Propagate Construction HUD Missing Root");
                GameObject created = UnityEngine.Object.Instantiate(sourceChild.gameObject, targetRoot);
                created.name = sourceChild.name;
                targetChild = created.transform;
                changed = true;
            }

            changed |= CopyTransformTreeLayout(sourceChild, targetChild);
        }

        // Clean stale HUD roots left by older scene instances.
        for (int i = targetRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = targetRoot.GetChild(i);
            if (child == null || !IsHudRoot(child))
                continue;

            if (allowedHudRootNames.Contains(child.name))
                continue;

            Undo.DestroyObjectImmediate(child.gameObject);
            changed = true;
        }

        if (!changed)
            return false;

        EditorUtility.SetDirty(targetGo);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static bool IsHudRoot(Transform root)
    {
        if (root == null)
            return false;

        string name = root.name != null ? root.name.ToLowerInvariant() : string.Empty;
        if (name.Contains("hud"))
            return true;

        if (root.GetComponent<Canvas>() != null)
            return true;

        return false;
    }

    private static bool CopyTransformTreeLayout(Transform sourceRoot, Transform targetRoot)
    {
        if (sourceRoot == null || targetRoot == null)
            return false;

        bool changed = false;
        changed |= CopySingleTransformLayout(sourceRoot, targetRoot);

        for (int i = 0; i < sourceRoot.childCount; i++)
        {
            Transform sourceChild = sourceRoot.GetChild(i);
            Transform targetChild = FindDirectChildByName(targetRoot, sourceChild.name);
            if (targetChild == null)
            {
                Undo.RecordObject(targetRoot.gameObject, "Propagate Construction HUD Missing Child");
                GameObject created = UnityEngine.Object.Instantiate(sourceChild.gameObject, targetRoot);
                created.name = sourceChild.name;
                targetChild = created.transform;
                changed = true;
            }

            changed |= CopyTransformTreeLayout(sourceChild, targetChild);
        }

        // Remove stale direct children not present in source tree (old propagated leftovers).
        for (int i = targetRoot.childCount - 1; i >= 0; i--)
        {
            Transform targetChild = targetRoot.GetChild(i);
            if (targetChild == null)
                continue;

            Transform sourceChild = FindDirectChildByName(sourceRoot, targetChild.name);
            if (sourceChild != null)
                continue;

            Undo.DestroyObjectImmediate(targetChild.gameObject);
            changed = true;
        }

        return changed;
    }

    private static bool CopySingleTransformLayout(Transform source, Transform target)
    {
        if (source == null || target == null)
            return false;

        bool changed = false;
        if (source.localPosition != target.localPosition)
        {
            Undo.RecordObject(target, "Propagate Construction HUD Position");
            target.localPosition = source.localPosition;
            changed = true;
        }

        if (source.localRotation != target.localRotation)
        {
            Undo.RecordObject(target, "Propagate Construction HUD Rotation");
            target.localRotation = source.localRotation;
            changed = true;
        }

        if (source.localScale != target.localScale)
        {
            Undo.RecordObject(target, "Propagate Construction HUD Scale");
            target.localScale = source.localScale;
            changed = true;
        }

        RectTransform sourceRect = source as RectTransform;
        RectTransform targetRect = target as RectTransform;
        if (sourceRect != null && targetRect != null)
        {
            if (sourceRect.anchorMin != targetRect.anchorMin)
            {
                Undo.RecordObject(targetRect, "Propagate Construction HUD AnchorMin");
                targetRect.anchorMin = sourceRect.anchorMin;
                changed = true;
            }

            if (sourceRect.anchorMax != targetRect.anchorMax)
            {
                Undo.RecordObject(targetRect, "Propagate Construction HUD AnchorMax");
                targetRect.anchorMax = sourceRect.anchorMax;
                changed = true;
            }

            if (sourceRect.pivot != targetRect.pivot)
            {
                Undo.RecordObject(targetRect, "Propagate Construction HUD Pivot");
                targetRect.pivot = sourceRect.pivot;
                changed = true;
            }

            if (sourceRect.sizeDelta != targetRect.sizeDelta)
            {
                Undo.RecordObject(targetRect, "Propagate Construction HUD SizeDelta");
                targetRect.sizeDelta = sourceRect.sizeDelta;
                changed = true;
            }

            if (sourceRect.anchoredPosition != targetRect.anchoredPosition)
            {
                Undo.RecordObject(targetRect, "Propagate Construction HUD AnchoredPosition");
                targetRect.anchoredPosition = sourceRect.anchoredPosition;
                changed = true;
            }

            if (sourceRect.anchoredPosition3D != targetRect.anchoredPosition3D)
            {
                Undo.RecordObject(targetRect, "Propagate Construction HUD AnchoredPosition3D");
                targetRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
                changed = true;
            }
        }

        if (source.gameObject.activeSelf != target.gameObject.activeSelf)
        {
            Undo.RecordObject(target.gameObject, "Propagate Construction HUD Active State");
            target.gameObject.SetActive(source.gameObject.activeSelf);
            changed = true;
        }

        changed |= CopyGameObjectComponents(source.gameObject, target.gameObject);

        return changed;
    }

    private static bool CopyGameObjectComponents(GameObject source, GameObject target)
    {
        if (source == null || target == null)
            return false;

        bool changed = false;
        Component[] sourceComponents = source.GetComponents<Component>();
        if (sourceComponents == null || sourceComponents.Length == 0)
            return false;

        var seenPerType = new Dictionary<Type, int>();
        for (int i = 0; i < sourceComponents.Length; i++)
        {
            Component sourceComponent = sourceComponents[i];
            if (sourceComponent == null)
                continue;

            Type type = sourceComponent.GetType();
            if (type == typeof(Transform) || type == typeof(RectTransform))
                continue;

            int ordinal = 0;
            seenPerType.TryGetValue(type, out ordinal);
            seenPerType[type] = ordinal + 1;

            Component targetComponent = GetComponentByTypeAndOrdinal(target, type, ordinal);
            if (targetComponent == null)
            {
                targetComponent = Undo.AddComponent(target, type);
                if (targetComponent == null)
                    continue;
                changed = true;
            }

            Undo.RecordObject(targetComponent, $"Propagate Construction HUD Component {type.Name}");
            EditorUtility.CopySerialized(sourceComponent, targetComponent);
            EditorUtility.SetDirty(targetComponent);
            changed = true;
        }

        return changed;
    }

    private static Component GetComponentByTypeAndOrdinal(GameObject target, Type type, int ordinal)
    {
        if (target == null || type == null || ordinal < 0)
            return null;

        Component[] components = target.GetComponents(type);
        if (components == null || components.Length <= ordinal)
            return null;

        return components[ordinal];
    }

    private static Transform FindDirectChildByName(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrWhiteSpace(name))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }
}
