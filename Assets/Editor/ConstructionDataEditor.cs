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
    private bool showLogisticsSection;
    private bool showProductionSection;
    private bool showAIBehaviorSection;
    private bool showUnitInformationSection;
    private bool showAircraftOpsSection;
    private bool showNavalOpsSection;

    // Toda secao nasce fechada a cada vez que o inspector se liga ao asset, sem depender
    // do valor default sobreviver a reuso de instancia do Editor entre recompilacoes.
    private void OnEnable()
    {
        showLogisticsSection = false;
        showProductionSection = false;
        showAIBehaviorSection = false;
        showUnitInformationSection = false;
        showAircraftOpsSection = false;
        showNavalOpsSection = false;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "constructionConfiguration",
            "allowRebelAIPurchase",
            "allowAircraftTakeoffAndLanding",
            "aircraftUnitsPaysUpkeep",
            "legacyRequiredLandingSkills",
            "requiredLandingSkillRules",
            "requireAtLeastOneLandingSkill",
            "forceEndMovementOnTerrainDomainForDomains",
            "forceDetectOnForcedEndMovementDomains",
            "forceDetectUnitsWithFollowingStealthSkills",
            "isSupplier",
            "supplierTier",
            "maxUnitsServedPerTurn",
            "serviceRange",
            "collectionRange",
            "supplierOperationDomains",
            "supplierServicesProvided",
            "supplierServiceProfile",
            "supplierResources",
            "aiStockRestockTriggerPercent");
        EditorGUILayout.Space();
        DrawUnitInformationSection();
        EditorGUILayout.Space();
        DrawConstructionConfigurationExpanded(serializedObject.FindProperty("constructionConfiguration"));
        EditorGUILayout.Space();
        DrawProductionSection(serializedObject.FindProperty("constructionConfiguration"));
        EditorGUILayout.Space();
        DrawLogisticsSection();
        EditorGUILayout.Space();
        DrawAIBehaviorSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLogisticsSection()
    {
        showLogisticsSection = EditorGUILayout.Foldout(
            showLogisticsSection,
            "Logistics",
            toggleOnLabelClick: true,
            EditorStyles.foldoutHeader);
        if (!showLogisticsSection)
            return;

        EditorGUI.indentLevel++;
        DrawIfExists(serializedObject.FindProperty("isSupplier"), "Is Supplier");
        DrawIfExists(serializedObject.FindProperty("supplierTier"), "Supplier Tier");
        DrawIfExists(serializedObject.FindProperty("maxUnitsServedPerTurn"), "Max Units Served Per Turn");
        DrawIfExists(serializedObject.FindProperty("serviceRange"), "Service Range");
        DrawIfExists(serializedObject.FindProperty("collectionRange"), "Collection Range");

        SerializedProperty isSupplier = serializedObject.FindProperty("isSupplier");
        if (isSupplier != null && isSupplier.boolValue)
        {
            DrawIfExists(serializedObject.FindProperty("supplierOperationDomains"), "Supplier Operation Domain");
            DrawIfExists(serializedObject.FindProperty("supplierServicesProvided"), "Supplier Services Provided");

            SerializedProperty profile = serializedObject.FindProperty("supplierServiceProfile");
            if (profile != null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(
                        profile,
                        new GUIContent("Supplier Service Profile"));
            }

            DrawIfExists(serializedObject.FindProperty("supplierResources"), "Supplier Services Supplies (Default)");
            DrawIfExists(
                serializedObject.FindProperty(
                    "aiStockRestockTriggerPercent"),
                "AI Stock Restock Trigger (%)");
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Ative Is Supplier para configurar Supplier Services e Supplier Resources.",
                MessageType.Info);
        }

        EditorGUI.indentLevel--;
    }

    // Unit Information agrupa o que a construcao permite/impoe as UNIDADES que a usam.
    private void DrawUnitInformationSection()
    {
        showUnitInformationSection = EditorGUILayout.Foldout(
            showUnitInformationSection,
            "Unit Information",
            toggleOnLabelClick: true,
            EditorStyles.foldoutHeader);
        if (!showUnitInformationSection)
            return;

        EditorGUI.indentLevel++;

        showAircraftOpsSection = EditorGUILayout.Foldout(
            showAircraftOpsSection,
            "Aircraft Ops",
            toggleOnLabelClick: true);
        if (showAircraftOpsSection)
        {
            EditorGUI.indentLevel++;
            DrawAircraftOpsSection(serializedObject);
            EditorGUI.indentLevel--;
        }

        showNavalOpsSection = EditorGUILayout.Foldout(
            showNavalOpsSection,
            "Naval Ops",
            toggleOnLabelClick: true);
        if (showNavalOpsSection)
        {
            EditorGUI.indentLevel++;
            DrawNavalOpsSection(serializedObject);
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
    }

    private static void DrawNavalOpsSection(SerializedObject so)
    {
        EditorGUILayout.HelpBox(
            "Unidades nesses dominios/alturas encerram movimento no dominio nativo da construcao.",
            MessageType.Info);
        DrawIfExists(so.FindProperty("forceEndMovementOnTerrainDomainForDomains"), "The Units On The Follow Domain Are Forced To Emerge");

        // Quem emerge aqui fica exposto: a deteccao acompanha a regra de emersao.
        EditorGUILayout.Space(2f);
        DrawIfExists(so.FindProperty("forceDetectOnForcedEndMovementDomains"), "Forced To Emerge Units Are Freely Detectable");
        DrawIfExists(so.FindProperty("forceDetectUnitsWithFollowingStealthSkills"), "Only These Stealth Skills Are Detectable");
    }

    private static void DrawAircraftOpsSection(SerializedObject so)
    {
        EditorGUILayout.HelpBox(
            "Regras de Air Ops (Construction):\n" +
            "- Allow Aicrafft Take Off and Landing: habilita pouso e decolagem neste contexto.\n" +
            "- Required Landing Skills: para cada skill voce define o take off mode usado neste contexto.",
            MessageType.Info);
        SerializedProperty allowProp = so.FindProperty("allowAircraftTakeoffAndLanding");
        DrawIfExists(allowProp, "Allow Aicrafft Take Off and Landing");
        DrawIfExists(so.FindProperty("requiredLandingSkillRules"), "Required Landing Skills");
        DrawIfExists(so.FindProperty("requireAtLeastOneLandingSkill"), "Pelo menos 1 skill");

        // Custo de estacionar aqui: concerne a aeronave pousada, nao a permissao de pousar.
        EditorGUILayout.Space(2f);
        DrawIfExists(so.FindProperty("aircraftUnitsPaysUpkeep"), "Landed Aircraft Pays Upkeep");
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

        EditorGUI.indentLevel--;
    }

    private void DrawAIBehaviorSection()
    {
        showAIBehaviorSection = EditorGUILayout.Foldout(
            showAIBehaviorSection,
            "AI Behavior",
            toggleOnLabelClick: true,
            EditorStyles.foldoutHeader);
        if (!showAIBehaviorSection)
            return;

        EditorGUI.indentLevel++;

        SerializedProperty rebelBuyProp = serializedObject.FindProperty("allowRebelAIPurchase");
        if (rebelBuyProp != null)
        {
            EditorGUILayout.HelpBox(
                "Allow Rebel AI Purchase: a faccao sem QG (rebelde) NUNCA produz por padrao — nem no que captura. " +
                "Marque para tornar ESTE predio uma excecao renegada: o rebelde que o capturar pode comprar aqui, " +
                "ignorando OriginalOwner/FirstOwner (rebelde nunca e o dono original do que toma). So Selling Rule = " +
                "Disabled ainda barra. Nao afeta times COM QG.",
                MessageType.Info);
            EditorGUILayout.PropertyField(rebelBuyProp, new GUIContent("Allow Rebel AI Purchase"));
        }

        EditorGUI.indentLevel--;
    }

    private void DrawProductionSection(SerializedProperty configProperty)
    {
        showProductionSection = EditorGUILayout.Foldout(
            showProductionSection,
            "Production",
            toggleOnLabelClick: true,
            EditorStyles.foldoutHeader);
        if (!showProductionSection)
            return;

        EditorGUI.indentLevel++;

        // Quem pode comprar aqui.
        DrawIfExists(configProperty.FindPropertyRelative("sellingRule"), "Selling Rules");

        // O que pode ser comprado aqui.
        EditorGUILayout.Space(4f);
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
