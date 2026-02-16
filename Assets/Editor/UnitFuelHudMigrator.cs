using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class UnitFuelHudMigrator
{
    private const string UnitPrefabPath = "Assets/Prefab/unit.prefab";
    private static readonly Color FuelDefaultOrange = new Color(0.8235295f, 0.4117647f, 0.1176471f, 1f);

    [MenuItem("Tools/Map Room/Migrate Fuel HUD In Open Scenes")]
    public static void MigrateFuelHudInOpenScenes()
    {
        GameObject unitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabPath);
        if (unitPrefab == null)
        {
            Debug.LogError($"[UnitFuelHudMigrator] Prefab nao encontrado em '{UnitPrefabPath}'.");
            return;
        }

        UnitHudController sourceHud = unitPrefab.GetComponentInChildren<UnitHudController>(true);
        if (sourceHud == null)
        {
            Debug.LogError("[UnitFuelHudMigrator] UnitHudController nao encontrado no prefab de unidade.");
            return;
        }

        Transform sourceCanvas = FindChildRecursive(sourceHud.transform, "Canvas");
        if (sourceCanvas == null)
        {
            Debug.LogError("[UnitFuelHudMigrator] Canvas nao encontrado no prefab de unidade.");
            return;
        }

        Transform sourceFuelContainer = FindChildRecursive(sourceCanvas, "fuel_container");
        Transform sourceFuel = FindChildRecursive(sourceCanvas, "fuel");
        Transform sourceFuelText = FindChildRecursive(sourceCanvas, "fuel_text");

        UnitHudController[] allHuds = Object.FindObjectsByType<UnitHudController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int updated = 0;
        int skipped = 0;

        for (int i = 0; i < allHuds.Length; i++)
        {
            UnitHudController hud = allHuds[i];
            if (hud == null)
            {
                skipped++;
                continue;
            }

            GameObject go = hud.gameObject;
            Scene scene = go.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                skipped++;
                continue;
            }

            Transform targetCanvas = FindChildRecursive(hud.transform, "Canvas");
            if (targetCanvas == null)
            {
                skipped++;
                continue;
            }

            bool changed = false;
            changed |= EnsureChildFromTemplate(sourceFuelContainer, targetCanvas);
            changed |= EnsureChildFromTemplate(sourceFuel, targetCanvas);
            changed |= EnsureChildFromTemplate(sourceFuelText, targetCanvas);
            changed |= RebindHudReferences(hud, targetCanvas);

            if (changed)
            {
                EditorUtility.SetDirty(hud);
                EditorSceneManager.MarkSceneDirty(scene);
                updated++;
            }
        }

        Debug.Log($"[UnitFuelHudMigrator] Concluido. Atualizados: {updated}. Ignorados: {skipped}.");
    }

    [MenuItem("Tools/Map Room/Force Fuel Default Color (Orange) In Open Scenes")]
    public static void ForceFuelDefaultColorOrangeInOpenScenes()
    {
        UnitHudController[] allHuds = Object.FindObjectsByType<UnitHudController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int updated = 0;
        int skipped = 0;

        for (int i = 0; i < allHuds.Length; i++)
        {
            UnitHudController hud = allHuds[i];
            if (hud == null)
            {
                skipped++;
                continue;
            }

            GameObject go = hud.gameObject;
            Scene scene = go.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                skipped++;
                continue;
            }

            SerializedObject so = new SerializedObject(hud);
            SerializedProperty colorProp = so.FindProperty("fuelDefaultColor");
            if (colorProp == null)
            {
                skipped++;
                continue;
            }

            if (colorProp.colorValue != FuelDefaultOrange)
            {
                colorProp.colorValue = FuelDefaultOrange;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(hud);
                EditorSceneManager.MarkSceneDirty(scene);
                updated++;
            }
        }

        Debug.Log($"[UnitFuelHudMigrator] Fuel default color forçado para laranja. Atualizados: {updated}. Ignorados: {skipped}.");
    }

    private static bool EnsureChildFromTemplate(Transform templateChild, Transform targetCanvas)
    {
        if (templateChild == null || targetCanvas == null)
            return false;

        Transform existing = FindChildRecursive(targetCanvas, templateChild.name);
        if (existing != null)
            return false;

        GameObject clone = Object.Instantiate(templateChild.gameObject);
        Undo.RegisterCreatedObjectUndo(clone, $"Create {templateChild.name}");
        clone.name = templateChild.name;
        clone.transform.SetParent(targetCanvas, false);
        return true;
    }

    private static bool RebindHudReferences(UnitHudController hud, Transform targetCanvas)
    {
        bool changed = false;
        SerializedObject so = new SerializedObject(hud);

        SerializedProperty fuelFillImageProp = so.FindProperty("fuelFillImage");
        SerializedProperty fuelFillRendererProp = so.FindProperty("fuelFillRenderer");
        SerializedProperty fuelTextProp = so.FindProperty("fuelText");

        Transform fuelT = FindChildRecursive(targetCanvas, "fuel");
        Transform fuelTextT = FindChildRecursive(targetCanvas, "fuel_text");

        Image fuelImage = fuelT != null ? fuelT.GetComponent<Image>() : null;
        SpriteRenderer fuelRenderer = fuelT != null ? fuelT.GetComponent<SpriteRenderer>() : null;
        TMP_Text fuelText = fuelTextT != null ? fuelTextT.GetComponent<TMP_Text>() : null;

        if (fuelFillImageProp != null && fuelFillImageProp.objectReferenceValue != fuelImage)
        {
            fuelFillImageProp.objectReferenceValue = fuelImage;
            changed = true;
        }

        if (fuelFillRendererProp != null && fuelFillRendererProp.objectReferenceValue != fuelRenderer)
        {
            fuelFillRendererProp.objectReferenceValue = fuelRenderer;
            changed = true;
        }

        if (fuelTextProp != null && fuelTextProp.objectReferenceValue != fuelText)
        {
            fuelTextProp.objectReferenceValue = fuelText;
            changed = true;
        }

        if (changed)
            so.ApplyModifiedProperties();

        return changed;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
