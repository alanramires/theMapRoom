using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConstructionData))]
public class ConstructionDataEditor : Editor
{
    private enum ForceCopyFilter
    {
        Army,
        Navy,
        Aeronautic
    }

    private ForceCopyFilter forceCopyFilter = ForceCopyFilter.Army;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "constructionConfiguration",
            "allowRebelAIPurchase",
            "allowAircraftTakeoffAndLanding",
            "legacyRequiredLandingSkills",
            "requiredLandingSkillRules",
            "requireAtLeastOneLandingSkill",
            "forceEndMovementOnTerrainDomainForDomains");
        EditorGUILayout.Space();
        DrawAircraftOpsSection(serializedObject);
        EditorGUILayout.Space();
        DrawNavalOpsSection(serializedObject);
        EditorGUILayout.Space();
        DrawConstructionConfigurationExpanded(serializedObject.FindProperty("constructionConfiguration"));

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawNavalOpsSection(SerializedObject so)
    {
        EditorGUILayout.LabelField("Naval Ops", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Unidades nesses dominios/alturas encerram movimento no dominio nativo da construcao.",
            MessageType.Info);
        DrawIfExists(so.FindProperty("forceEndMovementOnTerrainDomainForDomains"), "The Units On The Follow Domain Are Forced To Emerge");
    }

    private static void DrawAircraftOpsSection(SerializedObject so)
    {
        EditorGUILayout.LabelField("Aircraft Ops", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Regras de Air Ops (Construction):\n" +
            "- Allow Aicrafft Take Off and Landing: habilita pouso e decolagem neste contexto.\n" +
            "- Required Landing Skills: para cada skill voce define o take off mode usado neste contexto.",
            MessageType.Info);
        SerializedProperty allowProp = so.FindProperty("allowAircraftTakeoffAndLanding");
        DrawIfExists(allowProp, "Allow Aicrafft Take Off and Landing");
        DrawIfExists(so.FindProperty("requiredLandingSkillRules"), "Required Landing Skills");
        DrawIfExists(so.FindProperty("requireAtLeastOneLandingSkill"), "Pelo menos 1 skill");
    }

    private void DrawConstructionConfigurationExpanded(SerializedProperty configProperty)
    {
        if (configProperty == null)
            return;

        EditorGUILayout.LabelField("Construction Configuration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Quando Is Supplier estiver ativo, Services/Resources de Supplier Settings sao sincronizados automaticamente para Offered Services/Offered Supplies. Offered Units continua 100% manual por construcao.",
            MessageType.Info);
        EditorGUI.indentLevel++;

        DrawIfExists(configProperty.FindPropertyRelative("isPlayerHeadQuarter"), "Is Player Head Quarter");
        DrawIfExists(configProperty.FindPropertyRelative("isVictoryBuilding"), "Is Victory Building");
        DrawIfExists(configProperty.FindPropertyRelative("isCapturable"), "Is Capturable");
        DrawIfExists(configProperty.FindPropertyRelative("capturePointsMax"), "Capture Points Max");
        DrawIfExists(configProperty.FindPropertyRelative("capturedIncoming"), "Captured Incoming");
        DrawIfExists(configProperty.FindPropertyRelative("sellingRule"), "Selling Rules");

        SerializedProperty rebelBuyProp = serializedObject.FindProperty("allowRebelAIPurchase");
        if (rebelBuyProp != null)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.HelpBox(
                "Allow Rebel AI Purchase: a faccao sem QG (rebelde) NUNCA produz por padrao — nem no que captura. " +
                "Marque para tornar ESTE predio uma excecao renegada: o rebelde que o capturar pode comprar aqui, " +
                "ignorando OriginalOwner/FirstOwner (rebelde nunca e o dono original do que toma). So Selling Rule = " +
                "Disabled ainda barra. Nao afeta times COM QG.",
                MessageType.Info);
            EditorGUILayout.PropertyField(rebelBuyProp, new GUIContent("Allow Rebel AI Purchase"));
        }

        SerializedProperty offeredUnitsProp = configProperty.FindPropertyRelative("offeredUnits");
        DrawIfExists(offeredUnitsProp, "Offered Units");
        DrawOfferedUnitsQuickFill(offeredUnitsProp);

        EditorGUI.indentLevel--;
    }

    private void DrawOfferedUnitsQuickFill(SerializedProperty offeredUnitsProp)
    {
        if (offeredUnitsProp == null)
            return;

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Quick Fill Offered Units", EditorStyles.miniBoldLabel);
        forceCopyFilter = (ForceCopyFilter)EditorGUILayout.EnumPopup("Copy Units Of", forceCopyFilter);

        UnitDatabase db = ResolvePreferredUnitDatabase();
        using (new EditorGUI.DisabledScope(db == null))
        {
            if (GUILayout.Button("Copy From Current Unit Database"))
            {
                int copied = CopyOfferedUnitsByForce(offeredUnitsProp, db, forceCopyFilter);
                Debug.Log($"[ConstructionDataEditor] Offered Units atualizadas: {copied} unidade(s) copiadas ({forceCopyFilter}).");
            }
        }

        if (db == null)
            EditorGUILayout.HelpBox("Unit Database nao encontrada na cena/projeto. Verifique se existe UnitSpawner com UnitDatabase configurado ou um asset UnitDatabase no projeto.", MessageType.Warning);
        else
            EditorGUILayout.ObjectField("Current Unit Database", db, typeof(UnitDatabase), false);
    }

    private static UnitDatabase ResolvePreferredUnitDatabase()
    {
        UnitDatabase fromScene = ResolveUnitDatabaseFromScene();
        if (fromScene != null)
            return fromScene;

        string[] guids = AssetDatabase.FindAssets("t:UnitDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnitDatabase db = AssetDatabase.LoadAssetAtPath<UnitDatabase>(path);
            if (db != null)
                return db;
        }

        return null;
    }

    private static UnitDatabase ResolveUnitDatabaseFromScene()
    {
        UnitSpawner[] spawners = Object.FindObjectsByType<UnitSpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
        {
            UnitSpawner spawner = spawners[i];
            if (spawner == null)
                continue;

            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty dbProp = so.FindProperty("unitDatabase");
            if (dbProp == null)
                continue;

            UnitDatabase db = dbProp.objectReferenceValue as UnitDatabase;
            if (db != null)
                return db;
        }

        return null;
    }

    private static int CopyOfferedUnitsByForce(SerializedProperty offeredUnitsProp, UnitDatabase db, ForceCopyFilter filter)
    {
        if (offeredUnitsProp == null || db == null || db.Units == null)
            return 0;

        MilitaryForce wanted = MilitaryForce.Army;
        if (filter == ForceCopyFilter.Navy)
            wanted = MilitaryForce.Navy;
        else if (filter == ForceCopyFilter.Aeronautic)
            wanted = MilitaryForce.Aeronautic;

        offeredUnitsProp.arraySize = 0;
        int copied = 0;
        for (int i = 0; i < db.Units.Count; i++)
        {
            UnitData unit = db.Units[i];
            if (unit == null || unit.militaryForce != wanted)
                continue;

            int index = offeredUnitsProp.arraySize;
            offeredUnitsProp.InsertArrayElementAtIndex(index);
            SerializedProperty elem = offeredUnitsProp.GetArrayElementAtIndex(index);
            if (elem != null)
            {
                elem.objectReferenceValue = unit;
                copied++;
            }
        }

        return copied;
    }

    private static void DrawIfExists(SerializedProperty prop, string label)
    {
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren: true);
    }

}
