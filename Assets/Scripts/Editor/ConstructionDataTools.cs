using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConstructionDataTools
{
    private const string ApplyDataMenuPath = "Tools/Construction/Propagate Construction Data (Force Override)";

    [MenuItem(ApplyDataMenuPath)]
    private static void PropagateConstructionData()
    {
        ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (constructions == null || constructions.Length == 0)
        {
            Debug.Log("[ConstructionDataTools] Nenhuma construcao encontrada na cena.");
            return;
        }

        int updated = 0;
        int skipped = 0;
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
                continue;

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

            construction.SetCurrentCapturePoints(construction.CapturePointsMax);
            EditorUtility.SetDirty(construction);
            updated++;
        }

        if (updated > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[ConstructionDataTools] {updated} construcao(oes) atualizada(s). Ignoradas: {skipped}.");
    }
}
