using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StructureDatabase))]
public class StructureDatabaseEditor : Editor
{
    private SerializedProperty structuresProp;
    private SerializedProperty roadRoutesByStructureProp;

    private void OnEnable()
    {
        structuresProp = serializedObject.FindProperty("structures");
        roadRoutesByStructureProp = serializedObject.FindProperty("roadRoutesByStructure");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(structuresProp, new GUIContent("Structures"), includeChildren: true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Road Routes (Map Scope)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Rotas centralizadas neste StructureDatabase. Cada bucket representa uma StructureData e suas rotas neste mapa.",
            MessageType.Info);

        DrawMigrationButton();
        EditorGUILayout.PropertyField(roadRoutesByStructureProp, new GUIContent("Road Routes By Structure"), includeChildren: true);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMigrationButton()
    {
        StructureDatabase db = (StructureDatabase)target;
        if (db == null || structuresProp == null)
            return;

        if (!GUILayout.Button("Migrate Legacy Routes From StructureData"))
            return;

        Undo.RecordObject(db, "Migrate Legacy Road Routes");
        db.ResetRoadRoutes();
        int migrated = 0;
        int skippedOtherDb = 0;
        int skippedNoOwner = 0;

        for (int i = 0; i < structuresProp.arraySize; i++)
        {
            SerializedProperty structureRef = structuresProp.GetArrayElementAtIndex(i);
            if (structureRef == null)
                continue;

            StructureData structure = structureRef.objectReferenceValue as StructureData;
            if (structure == null || structure.roadRoutes == null || structure.roadRoutes.Count == 0)
                continue;

            var routes = db.GetOrCreateRoadRoutes(structure);
            if (routes == null)
                continue;

            // Reconstroi sempre o bucket para evitar sobras de migracoes anteriores.
            routes.Clear();

            for (int r = 0; r < structure.roadRoutes.Count; r++)
            {
                RoadRouteDefinition legacy = structure.roadRoutes[r];
                if (legacy == null)
                    continue;

                if (legacy.ownerDatabase == null)
                {
                    skippedNoOwner++;
                    continue;
                }

                if (legacy.ownerDatabase != db)
                {
                    skippedOtherDb++;
                    continue;
                }

                routes.Add(CloneRoute(legacy));
                migrated++;
            }
        }

        EditorUtility.SetDirty(db);
        serializedObject.Update();
        Debug.Log(
            $"[StructureDatabaseEditor] Migracao concluida para '{db.name}'. Copiadas={migrated} | PuladasOutrosDb={skippedOtherDb} | PuladasSemOwner={skippedNoOwner}.",
            db);
    }

    private static RoadRouteDefinition CloneRoute(RoadRouteDefinition source)
    {
        if (source == null)
            return null;

        return new RoadRouteDefinition
        {
            routeName = source.routeName,
            ownerDatabase = source.ownerDatabase,
            cells = source.cells != null ? new System.Collections.Generic.List<Vector3Int>(source.cells) : new System.Collections.Generic.List<Vector3Int>()
        };
    }
}
