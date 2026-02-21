using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UnitLayerTools
{
    private const string ApplyDataMenuPath = "Tools/Units/Propagate Unit Data (Apply From Database)";
    private const string UnitPrefabPath = "Assets/Prefab/unit.prefab";

    [MenuItem(ApplyDataMenuPath)]
    private static void PropagateUnitData()
    {
        UnitHudController sourceHud = LoadSourceHudFromPrefab();
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (units == null || units.Length == 0)
        {
            Debug.Log("[UnitLayerTools] Nenhuma unidade encontrada na cena.");
            return;
        }

        int updated = 0;
        int hudUpdated = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;

            Undo.RecordObject(unit, "Propagate Unit Data");
            if (PropagateHudLayoutForUnit(sourceHud, unit))
                hudUpdated++;

            if (!unit.ApplyFromDatabase())
                continue;

            EditorUtility.SetDirty(unit);
            updated++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[UnitLayerTools] {updated} unidade(s) sincronizadas com UnitData. Layout HUD propagado em {hudUpdated} unidade(s).");
    }

    private static UnitHudController LoadSourceHudFromPrefab()
    {
        GameObject unitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabPath);
        if (unitPrefab == null)
        {
            Debug.LogWarning($"[UnitLayerTools] Prefab de unidade nao encontrado em '{UnitPrefabPath}'. Propagacao de HUD sera ignorada.");
            return null;
        }

        UnitHudController sourceHud = unitPrefab.GetComponentInChildren<UnitHudController>(true);
        if (sourceHud == null)
            Debug.LogWarning("[UnitLayerTools] UnitHudController nao encontrado no prefab. Propagacao de HUD sera ignorada.");

        return sourceHud;
    }

    private static bool PropagateHudLayoutForUnit(UnitHudController sourceHud, UnitManager unit)
    {
        if (sourceHud == null || unit == null)
            return false;

        GameObject go = unit.gameObject;
        Scene scene = go.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        UnitHudController targetHud = unit.GetComponentInChildren<UnitHudController>(true);
        if (targetHud == null)
            return false;

        Undo.RecordObject(targetHud, "Propagate Unit HUD Layout");
        bool changed = CopyTransformTreeLayout(sourceHud.transform, targetHud.transform);
        targetHud.RefreshBindings();
        changed = true;
        if (!changed)
            return false;

        EditorUtility.SetDirty(targetHud);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
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
                Undo.RecordObject(targetRoot.gameObject, "Propagate HUD Missing Child");
                GameObject created = Object.Instantiate(sourceChild.gameObject, targetRoot);
                created.name = sourceChild.name;
                targetChild = created.transform;
                changed = true;
            }

            changed |= CopyTransformTreeLayout(sourceChild, targetChild);
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
            Undo.RecordObject(target, "Propagate HUD Position");
            target.localPosition = source.localPosition;
            changed = true;
        }

        if (source.localRotation != target.localRotation)
        {
            Undo.RecordObject(target, "Propagate HUD Rotation");
            target.localRotation = source.localRotation;
            changed = true;
        }

        if (source.localScale != target.localScale)
        {
            Undo.RecordObject(target, "Propagate HUD Scale");
            target.localScale = source.localScale;
            changed = true;
        }

        RectTransform sourceRect = source as RectTransform;
        RectTransform targetRect = target as RectTransform;
        if (sourceRect != null && targetRect != null)
        {
            if (sourceRect.anchorMin != targetRect.anchorMin)
            {
                Undo.RecordObject(targetRect, "Propagate HUD AnchorMin");
                targetRect.anchorMin = sourceRect.anchorMin;
                changed = true;
            }

            if (sourceRect.anchorMax != targetRect.anchorMax)
            {
                Undo.RecordObject(targetRect, "Propagate HUD AnchorMax");
                targetRect.anchorMax = sourceRect.anchorMax;
                changed = true;
            }

            if (sourceRect.pivot != targetRect.pivot)
            {
                Undo.RecordObject(targetRect, "Propagate HUD Pivot");
                targetRect.pivot = sourceRect.pivot;
                changed = true;
            }

            if (sourceRect.sizeDelta != targetRect.sizeDelta)
            {
                Undo.RecordObject(targetRect, "Propagate HUD SizeDelta");
                targetRect.sizeDelta = sourceRect.sizeDelta;
                changed = true;
            }

            if (sourceRect.anchoredPosition != targetRect.anchoredPosition)
            {
                Undo.RecordObject(targetRect, "Propagate HUD AnchoredPosition");
                targetRect.anchoredPosition = sourceRect.anchoredPosition;
                changed = true;
            }

            if (sourceRect.anchoredPosition3D != targetRect.anchoredPosition3D)
            {
                Undo.RecordObject(targetRect, "Propagate HUD AnchoredPosition3D");
                targetRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
                changed = true;
            }
        }

        changed |= CopyGameObjectState(source.gameObject, target.gameObject);

        return changed;
    }

    private static bool CopyGameObjectState(GameObject source, GameObject target)
    {
        if (source == null || target == null)
            return false;

        if (source.activeSelf == target.activeSelf)
            return false;

        Undo.RecordObject(target, "Propagate HUD Active State");
        target.SetActive(source.activeSelf);
        return true;
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
