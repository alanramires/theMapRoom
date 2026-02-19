using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnitLayerTools
{
    private const string ApplyDataMenuPath = "Tools/Units/Propagate Unit Data (Apply From Database)";

    [MenuItem(ApplyDataMenuPath)]
    private static void PropagateUnitData()
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

            Undo.RecordObject(unit, "Propagate Unit Data");
            if (!unit.ApplyFromDatabase())
                continue;

            EditorUtility.SetDirty(unit);
            updated++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[UnitLayerTools] {updated} unidade(s) sincronizadas com UnitData (armas embarcadas incluidas).");
    }
}
