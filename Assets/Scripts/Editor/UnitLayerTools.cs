using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnitLayerTools
{
    private const string KeepCurrentMenuPath = "Tools/Units/Propagate Layer State (Keep Current If Valid)";
    private const string ForceNativeMenuPath = "Tools/Units/Propagate Layer State (Force Native Default)";

    [MenuItem(KeepCurrentMenuPath)]
    private static void PropagateKeepCurrentIfValid()
    {
        PropagateLayerState(forceNativeDefault: false);
    }

    [MenuItem(ForceNativeMenuPath)]
    private static void PropagateForceNativeDefault()
    {
        PropagateLayerState(forceNativeDefault: true);
    }

    private static void PropagateLayerState(bool forceNativeDefault)
    {
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (units == null || units.Length == 0)
        {
            Debug.Log("[UnitLayerTools] Nenhuma unidade encontrada na cena.");
            return;
        }

        int updated = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;

            Undo.RecordObject(unit, "Propagate Unit Layer State");
            unit.SyncLayerStateFromData(forceNativeDefault);
            EditorUtility.SetDirty(unit);
            updated++;
        }

        EditorSceneManager.MarkAllScenesDirty();

        string mode = forceNativeDefault ? "Force Native Default" : "Keep Current If Valid";
        Debug.Log($"[UnitLayerTools] {updated} unidade(s) atualizadas. Modo: {mode}.");
    }
}
