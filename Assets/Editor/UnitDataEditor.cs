using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitData))]
public class UnitDataEditor : Editor
{
    private SerializedProperty embarkedWeaponsProperty;
    private SerializedProperty isSupplierProperty;
    private SerializedProperty supplierTierProperty;
    private SerializedProperty maxUnitsServedPerTurnProperty;
    private SerializedProperty serviceRangeProperty;
    private SerializedProperty collectionRangeProperty;
    private SerializedProperty supplierOperationDomainsProperty;
    private SerializedProperty supplierServicesProvidedProperty;
    private SerializedProperty supplierResourcesProperty;
    private SerializedProperty stealthSkillRulesProperty;
    private SerializedProperty aiUnitProfileProperty;
    private SerializedProperty aiTargetPreferenceByClassProperty;

    private void OnEnable()
    {
        embarkedWeaponsProperty = serializedObject.FindProperty("embarkedWeapons");
        isSupplierProperty = serializedObject.FindProperty("isSupplier");
        supplierTierProperty = serializedObject.FindProperty("supplierTier");
        maxUnitsServedPerTurnProperty = serializedObject.FindProperty("maxUnitsServedPerTurn");
        serviceRangeProperty = serializedObject.FindProperty("serviceRange");
        collectionRangeProperty = serializedObject.FindProperty("collectionRange");
        supplierOperationDomainsProperty = serializedObject.FindProperty("supplierOperationDomains");
        supplierServicesProvidedProperty = serializedObject.FindProperty("supplierServicesProvided");
        supplierResourcesProperty = serializedObject.FindProperty("supplierResources");
        stealthSkillRulesProperty = serializedObject.FindProperty("stealthSkillRules");
        aiUnitProfileProperty = serializedObject.FindProperty("aiUnitProfile");
        aiTargetPreferenceByClassProperty = serializedObject.FindProperty("aiTargetPreferenceByClass");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPrimaryIdentitySection();
        DrawNativeDomainSection();
        DrawTopAttributesSection();
        DrawAircraftInformationSection();
        DrawNavalInformationSection();
        DrawVisionAndDetectionSection();
        DrawTrainingAndAbilitiesSection();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "id",
            "displayName",
            "apelido",
            "description",
            "militaryForce",
            "unitClass",
            "combatClassification",
            "roles",
            "visao",
            "visionSpecializations",
            "eliteLevel",
            "combatModifiers",
            "skills",
            "domain",
            "heightLevel",
            "aditionalDomainsAllowed",
            "maxHP",
            "defense",
            "armorClass",
            "movement",
            "movementCategory",
            "autonomia",
            "autonomyData",
            "cost",
            "spriteDefault",
            "spriteGreen",
            "spriteRed",
            "spriteBlue",
            "spriteYellow",
            "teamVariantSprites",
            "bazookaTargetPriority",
            "aiUnitProfile",
            "aiInitiative",
            "aiTargetPreferenceByClass",
            "aiSensorPriority",
            "prioritizeDpqAtBattle",
            "playConservative",
            "preferMoveOnBestDPQ",
            "useAttackDecision",
            "attackAcceptHpLossPercent",
            "attackEliminationMinPercent",
            "attackMustSurvive",
            "ignoreAttackDecisionWhenDefending",
            "defensiveAttackExtraHpLossPercent",
            "repairTriggerHpBelow",
            "repairTriggerAutonomyPct",
            "repairTriggerAmmoEnabled",
            "repairTriggerAmmoPct",
            "repairRecoverHpAbove",
            "fuseWhileInRepair",
            "stealthSkills",
            "stealthSkillRules",
            "useExplicitPreferredAirHeight",
            "preferredAirHeight",
            "useExplicitPreferredNavalHeight",
            "preferredNavalHeight",
            "cantUseWeaponsOnTheFollowDomain",
            "embarkedWeapons",
            "isSupplier",
            "supplierTier",
            "maxUnitsServedPerTurn",
            "serviceRange",
            "collectionRange",
            "supplierOperationDomains",
            "supplierServicesProvided",
            "supplierResources",
            "isTransporter",
            "spriteTransport",
            "allowedEmbarkWhenTransporterAtTerrains",
            "allowedEmbarkWhenTransporterAtTerrainStructures",
            "allowedEmbarkWhenTransporterAtConstructions",
            "allowedDisembarkWhenTransporterAtTerrains",
            "allowedDisembarkWhenTransporterAtTerrainStructures",
            "allowedDisembarkWhenTransporterAtConstructions",
            "passengersCanDisembarkAndGoesToTerrains",
            "passengersCanDisembarkAndGoesToTerrainStructures",
            "passengersCanDisembarkAndGoesToConstructions",
            "transportSlots");
        EditorGUILayout.Space();
        DrawAISection();
        EditorGUILayout.Space();
        DrawWeaponsSection();
        EditorGUILayout.Space();
        DrawTransportSection();
        EditorGUILayout.Space();
        DrawLogisticsSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawAISection()
    {
        EditorGUILayout.LabelField("AI", EditorStyles.boldLabel);

        SerializedProperty aiInitiativeProp      = serializedObject.FindProperty("aiInitiative");
        SerializedProperty aiTargetPrefProp      = serializedObject.FindProperty("aiTargetPreferenceByClass");
        SerializedProperty sensorPriorityProp    = serializedObject.FindProperty("aiSensorPriority");
        if (aiInitiativeProp != null)
            EditorGUILayout.PropertyField(aiInitiativeProp, new GUIContent("Ai Initiative"));
        if (aiTargetPrefProp != null)
            DrawAiTargetPreferenceByClassSection(aiTargetPrefProp);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Combat Behavior", EditorStyles.boldLabel);
        if (sensorPriorityProp != null)
            EditorGUILayout.PropertyField(sensorPriorityProp, new GUIContent("Ai Sensor Priority"), includeChildren: true);

        SerializedProperty dpqProp           = serializedObject.FindProperty("prioritizeDpqAtBattle");
        SerializedProperty conservativeProp  = serializedObject.FindProperty("playConservative");
        SerializedProperty preferDpqMoveProp = serializedObject.FindProperty("preferMoveOnBestDPQ");
        if (dpqProp != null)
            EditorGUILayout.PropertyField(dpqProp,           new GUIContent("Prioritize DPQ At Battle"));
        if (conservativeProp != null)
            EditorGUILayout.PropertyField(conservativeProp,  new GUIContent("Play Conservative"));
        if (preferDpqMoveProp != null)
            EditorGUILayout.PropertyField(preferDpqMoveProp, new GUIContent("Prefer Move On Best DPQ"));

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Attack Decision", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Campos de decisao para a IA antes de aceitar combate. A aplicacao no score/gate de ataque sera feita no AIController.",
            MessageType.None);

        SerializedProperty useAttackDecisionProp = serializedObject.FindProperty("useAttackDecision");
        SerializedProperty acceptHpLossProp = serializedObject.FindProperty("attackAcceptHpLossPercent");
        SerializedProperty eliminationMinProp = serializedObject.FindProperty("attackEliminationMinPercent");
        SerializedProperty mustSurviveProp = serializedObject.FindProperty("attackMustSurvive");
        SerializedProperty ignoreWhenDefendingProp = serializedObject.FindProperty("ignoreAttackDecisionWhenDefending");
        SerializedProperty defensiveExtraProp = serializedObject.FindProperty("defensiveAttackExtraHpLossPercent");

        if (useAttackDecisionProp != null)
            EditorGUILayout.PropertyField(useAttackDecisionProp, new GUIContent("Use Attack Decision"));

        using (new EditorGUI.DisabledScope(useAttackDecisionProp != null && !useAttackDecisionProp.boolValue))
        {
            if (acceptHpLossProp != null)
                EditorGUILayout.PropertyField(acceptHpLossProp, new GUIContent("Accept HP Loss %", "Perda maxima de HP aceita ao iniciar combate."));
            if (eliminationMinProp != null)
                EditorGUILayout.PropertyField(eliminationMinProp, new GUIContent("Elimination Min %", "Dano minimo no alvo, em % do HP maximo dele, para aceitar o combate."));
            if (mustSurviveProp != null)
                EditorGUILayout.PropertyField(mustSurviveProp, new GUIContent("Must Survive", "Rejeita ataques em que a simulacao mata esta unidade."));
            if (ignoreWhenDefendingProp != null)
                EditorGUILayout.PropertyField(ignoreWhenDefendingProp, new GUIContent("Ignore When Defending", "Ignora o gate quando esta unidade estiver em plano defensivo."));
            if (defensiveExtraProp != null)
                EditorGUILayout.PropertyField(defensiveExtraProp, new GUIContent("Defensive Extra HP Loss %", "Tolerancia extra quando estiver defendendo setor/base."));
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Repair Decision", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Runtime flag 'isUnderRepair' lives on UnitManager (per instance).\n" +
            "These thresholds (UnitData) drive when to activate and deactivate it.\n\n" +
            "ACTIVATE: unit falls to Retreat initiative, skips all sensor priorities and seeks the nearest available allied captured building with no unit on it.\n" +
            "DEACTIVATE: unit recovers its normal initiative and resumes sensor priorities.",
            MessageType.None);

        SerializedProperty hpBelowProp        = serializedObject.FindProperty("repairTriggerHpBelow");
        SerializedProperty autonomyPctProp    = serializedObject.FindProperty("repairTriggerAutonomyPct");
        SerializedProperty ammoEnabledProp    = serializedObject.FindProperty("repairTriggerAmmoEnabled");
        SerializedProperty ammoPctProp        = serializedObject.FindProperty("repairTriggerAmmoPct");
        SerializedProperty recoverHpProp      = serializedObject.FindProperty("repairRecoverHpAbove");

        EditorGUILayout.LabelField("Trigger (activate isUnderRepair)", EditorStyles.miniBoldLabel);
        if (hpBelowProp != null)
            EditorGUILayout.PropertyField(hpBelowProp, new GUIContent("HP ≤", "Enter repair when HP drops to this value or below. Set 0 to disable."));
        if (autonomyPctProp != null)
            EditorGUILayout.PropertyField(autonomyPctProp, new GUIContent("Autonomy Left ≤ %", "Enter repair when autonomy remaining is at or below this %. Set 0 to disable."));
        if (ammoEnabledProp != null)
            EditorGUILayout.PropertyField(ammoEnabledProp, new GUIContent("Ammo Trigger Enabled"));
        if (ammoEnabledProp != null && ammoEnabledProp.boolValue && ammoPctProp != null)
        {
            using (new EditorGUI.IndentLevelScope())
                EditorGUILayout.PropertyField(ammoPctProp, new GUIContent("Any Weapon Ammo ≤ %", "Enter repair when any embarked weapon ammo is at or below this %. 0 = completely out of ammo."));
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Recovery (deactivate isUnderRepair)", EditorStyles.miniBoldLabel);
        if (recoverHpProp != null)
            EditorGUILayout.PropertyField(recoverHpProp, new GUIContent("HP ≥", "Leave repair mode when HP reaches or above this value AND autonomy/ammo (if they were triggers) are also above their thresholds."));

        SerializedProperty fuseWhileInRepairProp = serializedObject.FindProperty("fuseWhileInRepair");
        if (fuseWhileInRepairProp != null)
            EditorGUILayout.PropertyField(fuseWhileInRepairProp, new GUIContent("Fuse While In Repair", "While under repair, attempt to fuse with a damaged allied unit of the same type nearby instead of waiting passively."));
    }

    private void DrawNativeDomainSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Native Domain", EditorStyles.boldLabel);
        SerializedProperty domainProperty = serializedObject.FindProperty("domain");
        SerializedProperty heightLevelProperty = serializedObject.FindProperty("heightLevel");
        SerializedProperty aditionalDomainsAllowedProperty = serializedObject.FindProperty("aditionalDomainsAllowed");

        if (domainProperty != null) EditorGUILayout.PropertyField(domainProperty);
        if (heightLevelProperty != null) EditorGUILayout.PropertyField(heightLevelProperty);
        if (aditionalDomainsAllowedProperty != null) 
            EditorGUILayout.PropertyField(aditionalDomainsAllowedProperty, new GUIContent("Adicional Domains of Operations"), includeChildren: true);
    }

    private static void DrawAiTargetPreferenceByClassSection(SerializedProperty listProperty)
    {
        EditorGUILayout.LabelField("Target Preference (By Class)", EditorStyles.boldLabel);

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            SerializedProperty element = listProperty.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            SerializedProperty targetClassProperty = element.FindPropertyRelative("targetClass");
            SerializedProperty priorityProperty = element.FindPropertyRelative("priority");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Element {i}", EditorStyles.boldLabel);
            if (GUILayout.Button("-", GUILayout.Width(28f)))
            {
                listProperty.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (targetClassProperty != null)
                EditorGUILayout.PropertyField(targetClassProperty, new GUIContent("Target Class"));

            if (priorityProperty != null)
            {
                int[] priorityValues = { (int)BazookaTargetPriority.Primary, (int)BazookaTargetPriority.Secondary };
                string[] priorityLabels = { "Primary", "Secondary" };

                int currentValue = priorityProperty.intValue;
                int selectedIndex = currentValue == (int)BazookaTargetPriority.Secondary ? 1 : 0;
                int newIndex = EditorGUILayout.Popup("Priority", selectedIndex, priorityLabels);
                priorityProperty.intValue = priorityValues[Mathf.Clamp(newIndex, 0, priorityValues.Length - 1)];
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ Add Target Preference"))
        {
            int index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);

            SerializedProperty newElement = listProperty.GetArrayElementAtIndex(index);
            if (newElement != null)
            {
                SerializedProperty targetClassProperty = newElement.FindPropertyRelative("targetClass");
                SerializedProperty priorityProperty = newElement.FindPropertyRelative("priority");

                if (targetClassProperty != null)
                    targetClassProperty.intValue = (int)GameUnitClass.Infantry;
                if (priorityProperty != null)
                    priorityProperty.intValue = (int)BazookaTargetPriority.Primary;
            }
        }
    }

    private void DrawAircraftInformationSection()
    {
        UnitData unitData = (UnitData)target;
        bool isAircraft = unitData.IsAircraft();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Aircraft Information", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Toggle("Is Aircraft (Derived)", isAircraft);

        using (new EditorGUI.DisabledScope(!isAircraft))
        {
            SerializedProperty useExplicitProp = serializedObject.FindProperty("useExplicitPreferredAirHeight");
            SerializedProperty preferredAirHeightProp = serializedObject.FindProperty("preferredAirHeight");
            SerializedProperty domainProp = serializedObject.FindProperty("domain");
            SerializedProperty heightProp = serializedObject.FindProperty("heightLevel");

            if (useExplicitProp != null)
                EditorGUILayout.PropertyField(useExplicitProp, new GUIContent("Use Explicit Preferred Air Height"));

            using (new EditorGUI.DisabledScope(useExplicitProp == null || !useExplicitProp.boolValue))
            {
                if (preferredAirHeightProp != null)
                    EditorGUILayout.PropertyField(preferredAirHeightProp, new GUIContent("Preferred Air Height"));
            }

            if (domainProp != null && heightProp != null)
            {
                Domain domain = (Domain)domainProp.intValue;
                HeightLevel height = (HeightLevel)heightProp.intValue;
                string fallback = domain == Domain.Air
                    ? $"Derivado do dominio nativo: {domain}/{height}"
                    : "Derivado de modos adicionais com Domain.Air (fallback Air/Low)";
                EditorGUILayout.HelpBox(useExplicitProp != null && useExplicitProp.boolValue
                    ? "Override ativo: a altura preferencial aerea usa o campo acima."
                    : fallback, MessageType.None);
            }
        }
    }

    private void DrawNavalInformationSection()
    {
        UnitData unitData = (UnitData)target;
        bool isMaritime = unitData.IsMaritime();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Naval Information", EditorStyles.boldLabel);
        
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Toggle("Is Maritime (Derived)", isMaritime);

        using (new EditorGUI.DisabledScope(!isMaritime))
        {
            SerializedProperty useExplicitProp = serializedObject.FindProperty("useExplicitPreferredNavalHeight");
            SerializedProperty preferredNavalHeightProp = serializedObject.FindProperty("preferredNavalHeight");

            if (useExplicitProp != null)
                EditorGUILayout.PropertyField(useExplicitProp, new GUIContent("Use Explicit Preferred Naval Height"));

            using (new EditorGUI.DisabledScope(useExplicitProp == null || !useExplicitProp.boolValue))
            {
                if (preferredNavalHeightProp != null)
                    EditorGUILayout.PropertyField(preferredNavalHeightProp, new GUIContent("Preferred Naval Height"));
            }

            EditorGUILayout.HelpBox(
                useExplicitProp != null && useExplicitProp.boolValue
                    ? "Quando valido no hex final e sem force de emerge, o sistema tenta auto-ajustar para a altura naval preferencial antes dos sensores."
                    : "Override naval desativado.",
                MessageType.None);
        }
    }

    private static void DrawIfExists(SerializedProperty property, string label)
    {
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren: true);
    }

    private void DrawLogisticsSection()
    {
        EditorGUILayout.LabelField("Logistics", EditorStyles.boldLabel);
        if (isSupplierProperty != null)
            EditorGUILayout.PropertyField(isSupplierProperty, new GUIContent("Is Supplier"));
        if (supplierTierProperty != null)
            EditorGUILayout.PropertyField(supplierTierProperty, new GUIContent("Supplier Tier"));
        if (maxUnitsServedPerTurnProperty != null)
            EditorGUILayout.PropertyField(maxUnitsServedPerTurnProperty, new GUIContent("Max Units Served Per Turn"));
        if (serviceRangeProperty != null)
            EditorGUILayout.PropertyField(serviceRangeProperty, new GUIContent("Service Range"));
        if (collectionRangeProperty != null)
            EditorGUILayout.PropertyField(collectionRangeProperty, new GUIContent("Collection Range"));

        bool isSupplier = isSupplierProperty != null && isSupplierProperty.boolValue;
        if (isSupplier)
        {
            if (supplierOperationDomainsProperty != null)
                EditorGUILayout.PropertyField(supplierOperationDomainsProperty, new GUIContent("Supplier Operation Domain"), includeChildren: true);
            if (supplierServicesProvidedProperty != null)
                EditorGUILayout.PropertyField(supplierServicesProvidedProperty, new GUIContent("Supplier Services Provided"), includeChildren: true);
            if (supplierResourcesProperty != null)
                EditorGUILayout.PropertyField(supplierResourcesProperty, new GUIContent("Supplier Services Supplies (Default)"), includeChildren: true);
        }
        else
        {
            EditorGUILayout.HelpBox("Ative Is Supplier para configurar Supplier Services e Supplier Resources.", MessageType.Info);
        }
    }

    private void DrawTransportSection()
    {
        EditorGUILayout.LabelField("Transport", EditorStyles.boldLabel);
        DrawIfExists(serializedObject.FindProperty("isTransporter"), "Is Transporter");
        DrawIfExists(serializedObject.FindProperty("spriteTransport"), "Sprite Transport");
        DrawIfExists(serializedObject.FindProperty("allowedEmbarkWhenTransporterAtTerrains"), "Allowed Embark Terrain When Transporter At: Terrain");
        DrawIfExists(serializedObject.FindProperty("allowedEmbarkWhenTransporterAtTerrainStructures"), "Allowed Embark Terrain When Transporter At: Terrain + Structure");
        DrawIfExists(serializedObject.FindProperty("allowedEmbarkWhenTransporterAtConstructions"), "Allowed Embark Terrain When Transporter At: Constructions");
        DrawIfExists(serializedObject.FindProperty("allowedDisembarkWhenTransporterAtTerrains"), "Allowed Disembark Terrain When Transporter At: Terrain");
        DrawIfExists(serializedObject.FindProperty("allowedDisembarkWhenTransporterAtTerrainStructures"), "Allowed Disembark Terrain When Transporter At: Terrain + Structure");
        DrawIfExists(serializedObject.FindProperty("allowedDisembarkWhenTransporterAtConstructions"), "Allowed Disembark Terrain When Transporter At: Constructions");
        DrawIfExists(serializedObject.FindProperty("passengersCanDisembarkAndGoesToTerrains"), "Passengers Can Disembark And Goes To: Terrain");
        DrawIfExists(serializedObject.FindProperty("passengersCanDisembarkAndGoesToTerrainStructures"), "Passengers Can Disembark And Goes To: Terrain + Structure");
        DrawIfExists(serializedObject.FindProperty("passengersCanDisembarkAndGoesToConstructions"), "Passengers Can Disembark And Goes To: Constructions");
        DrawIfExists(serializedObject.FindProperty("transportSlots"), "Transport Slots");
    }

    private void DrawPrimaryIdentitySection()
    {
        SerializedProperty idProperty = serializedObject.FindProperty("id");
        SerializedProperty displayNameProperty = serializedObject.FindProperty("displayName");
        SerializedProperty apelidoProperty = serializedObject.FindProperty("apelido");
        SerializedProperty descriptionProperty = serializedObject.FindProperty("description");
        SerializedProperty militaryForceProperty = serializedObject.FindProperty("militaryForce");
        SerializedProperty unitClassProperty = serializedObject.FindProperty("unitClass");
        SerializedProperty combatClassificationProperty = serializedObject.FindProperty("combatClassification");
        SerializedProperty rolesProperty = serializedObject.FindProperty("roles");
        SerializedProperty spriteDefaultProperty = serializedObject.FindProperty("spriteDefault");
        SerializedProperty teamVariantSpritesProperty = serializedObject.FindProperty("teamVariantSprites");
        if (idProperty != null)
            EditorGUILayout.PropertyField(idProperty);
        if (displayNameProperty != null)
            EditorGUILayout.PropertyField(displayNameProperty);
        if (apelidoProperty != null)
            EditorGUILayout.PropertyField(apelidoProperty);
        if (descriptionProperty != null)
            EditorGUILayout.PropertyField(descriptionProperty);
        if (militaryForceProperty != null)
            EditorGUILayout.PropertyField(militaryForceProperty);
        if (unitClassProperty != null)
            EditorGUILayout.PropertyField(unitClassProperty);
        if (combatClassificationProperty != null)
            EditorGUILayout.PropertyField(combatClassificationProperty);
        if (rolesProperty != null)
            EditorGUILayout.PropertyField(rolesProperty, new GUIContent("Roles"), includeChildren: true);

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
        if (spriteDefaultProperty != null)
            EditorGUILayout.PropertyField(spriteDefaultProperty, new GUIContent("Sprite (Default)"));
        if (teamVariantSpritesProperty != null)
            EditorGUILayout.PropertyField(teamVariantSpritesProperty, new GUIContent("Team Sprites Variants"), includeChildren: true);
    }

    private void DrawTopAttributesSection()
    {
        SerializedProperty maxHpProperty = serializedObject.FindProperty("maxHP");
        SerializedProperty defenseProperty = serializedObject.FindProperty("defense");
        SerializedProperty armorClassProperty = serializedObject.FindProperty("armorClass");
        SerializedProperty movementProperty = serializedObject.FindProperty("movement");
        SerializedProperty movementCategoryProperty = serializedObject.FindProperty("movementCategory");
        SerializedProperty autonomiaProperty = serializedObject.FindProperty("autonomia");
        SerializedProperty autonomyDataProperty = serializedObject.FindProperty("autonomyData");
        SerializedProperty costProperty = serializedObject.FindProperty("cost");

        if (maxHpProperty != null)
            EditorGUILayout.PropertyField(maxHpProperty);
        if (defenseProperty != null)
            EditorGUILayout.PropertyField(defenseProperty);
        if (armorClassProperty != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(armorClassProperty, new GUIContent("Armor Class (Auto by Defense)"));
        }
        if (movementProperty != null)
            EditorGUILayout.PropertyField(movementProperty);
        if (movementCategoryProperty != null)
            EditorGUILayout.PropertyField(movementCategoryProperty);
        if (autonomiaProperty != null)
            EditorGUILayout.PropertyField(autonomiaProperty);
        if (autonomyDataProperty != null)
            EditorGUILayout.PropertyField(autonomyDataProperty, new GUIContent("Autonomia (Upkeep e Consumo)"), includeChildren: true);
        if (costProperty != null)
            EditorGUILayout.PropertyField(costProperty);
    }

    private void DrawWeaponsSection()
    {
        EditorGUILayout.LabelField("Weapons", EditorStyles.boldLabel);
        
        SerializedProperty cantUseWeaponsOnTheFollowDomainProperty = serializedObject.FindProperty("cantUseWeaponsOnTheFollowDomain");
        if (cantUseWeaponsOnTheFollowDomainProperty != null)
        {
            EditorGUILayout.PropertyField(cantUseWeaponsOnTheFollowDomainProperty, new GUIContent("Can't use weapons when at domain"), includeChildren: true);
            EditorGUILayout.Space(2f);
        }

        if (aiTargetPreferenceByClassProperty != null)
        {
            DrawAiTargetPreferenceByClassSection(aiTargetPreferenceByClassProperty);
            EditorGUILayout.Space(2f);
        }

        EditorGUILayout.LabelField("Embarked Weapons", EditorStyles.boldLabel);
        WeaponDatabase database = ResolveWeaponDatabaseFromProject();
        List<WeaponData> catalog = BuildCatalog(database);
        if (database == null)
            EditorGUILayout.HelpBox("No WeaponDatabase found in project. Create one to drive weapon slots.", MessageType.Warning);

        if (embarkedWeaponsProperty == null)
            return;

        for (int i = 0; i < embarkedWeaponsProperty.arraySize; i++)
        {
            SerializedProperty element = embarkedWeaponsProperty.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            SerializedProperty weaponProperty = element.FindPropertyRelative("weapon");
            SerializedProperty squadAmmunitionProperty = element.FindPropertyRelative("squadAmmunition");
            SerializedProperty operationRangeMinProperty = element.FindPropertyRelative("operationRangeMin");
            SerializedProperty operationRangeMaxProperty = element.FindPropertyRelative("operationRangeMax");
            SerializedProperty selectedTrajectoryProperty = element.FindPropertyRelative("selectedTrajectory");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Weapon Slot {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                embarkedWeaponsProperty.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            DrawWeaponPopup(weaponProperty, catalog);

            WeaponData selectedWeapon = weaponProperty != null ? weaponProperty.objectReferenceValue as WeaponData : null;
            using (new EditorGUI.DisabledScope(true))
            {
                int basicAttack = selectedWeapon != null ? selectedWeapon.basicAttack : 0;
                EditorGUILayout.IntField("Weapon Attack (Info)", basicAttack);
            }

            if (squadAmmunitionProperty != null)
                EditorGUILayout.PropertyField(squadAmmunitionProperty, new GUIContent("Squad Ammunition"));

            if (operationRangeMinProperty != null)
                EditorGUILayout.PropertyField(operationRangeMinProperty, new GUIContent("Operation Range Min"));
            if (operationRangeMaxProperty != null)
                EditorGUILayout.PropertyField(operationRangeMaxProperty, new GUIContent("Operation Range Max"));
            DrawTrajectoryPopup(selectedWeapon, selectedTrajectoryProperty);

            if (selectedWeapon != null)
            {
                string trajectories = selectedWeapon.trajectories != null && selectedWeapon.trajectories.Count > 0
                    ? string.Join(", ", selectedWeapon.trajectories)
                    : WeaponTrajectoryType.Straight.ToString();
                EditorGUILayout.HelpBox(
                    $"Weapon Default Range: {selectedWeapon.operationRangeMin}-{selectedWeapon.operationRangeMax} | Trajectories: {trajectories}",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        if (GUILayout.Button("Add Weapon Slot"))
            embarkedWeaponsProperty.InsertArrayElementAtIndex(embarkedWeaponsProperty.arraySize);
    }

    private void DrawVisionAndDetectionSection()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("View and Detect", EditorStyles.boldLabel);

        SerializedProperty visaoProperty = serializedObject.FindProperty("visao");
        SerializedProperty visionSpecializationsProperty = serializedObject.FindProperty("visionSpecializations");

        if (visaoProperty != null) EditorGUILayout.PropertyField(visaoProperty);
        if (visionSpecializationsProperty != null) EditorGUILayout.PropertyField(visionSpecializationsProperty, new GUIContent("Vision Specializations"), includeChildren: true);

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Stealth Skills", EditorStyles.boldLabel);

        if (stealthSkillRulesProperty != null)
        {
            EditorGUILayout.HelpBox(
                "Config principal por camada. Cada elemento define: Skill + Domain + Height.",
                MessageType.None);

            for (int i = 0; i < stealthSkillRulesProperty.arraySize; i++)
            {
                SerializedProperty element = stealthSkillRulesProperty.GetArrayElementAtIndex(i);
                if (element == null)
                    continue;

                SerializedProperty skillProperty = element.FindPropertyRelative("skill");
                SerializedProperty domainProperty = element.FindPropertyRelative("domain");
                SerializedProperty heightProperty = element.FindPropertyRelative("heightLevel");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Element {i}", EditorStyles.boldLabel);
                if (GUILayout.Button("-", GUILayout.Width(28f)))
                {
                    stealthSkillRulesProperty.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (skillProperty != null)
                    EditorGUILayout.PropertyField(skillProperty, new GUIContent("Skill"));
                if (domainProperty != null)
                    EditorGUILayout.PropertyField(domainProperty, new GUIContent("Domain"));
                if (heightProperty != null)
                    EditorGUILayout.PropertyField(heightProperty, new GUIContent("Height"));
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Stealth Skill Rule"))
                stealthSkillRulesProperty.InsertArrayElementAtIndex(stealthSkillRulesProperty.arraySize);
        }
    }

    private void DrawTrainingAndAbilitiesSection()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Training & Abilities", EditorStyles.boldLabel);

        SerializedProperty eliteLevelProperty = serializedObject.FindProperty("eliteLevel");
        SerializedProperty skillsProperty = serializedObject.FindProperty("skills");
        SerializedProperty combatModifiersProperty = serializedObject.FindProperty("combatModifiers");

        if (eliteLevelProperty != null) EditorGUILayout.PropertyField(eliteLevelProperty);
        if (skillsProperty != null) EditorGUILayout.PropertyField(skillsProperty, includeChildren: true);
        if (combatModifiersProperty != null) EditorGUILayout.PropertyField(combatModifiersProperty, includeChildren: true);
    }

    private static List<WeaponData> BuildCatalog(WeaponDatabase database)
    {
        List<WeaponData> catalog = new List<WeaponData>();
        if (database == null || database.Weapons == null)
            return catalog;

        for (int i = 0; i < database.Weapons.Count; i++)
        {
            WeaponData weapon = database.Weapons[i];
            if (weapon != null)
                catalog.Add(weapon);
        }

        return catalog;
    }

    private static void DrawWeaponPopup(SerializedProperty weaponProperty, List<WeaponData> catalog)
    {
        if (weaponProperty == null)
            return;

        if (catalog == null || catalog.Count == 0)
        {
            EditorGUILayout.PropertyField(weaponProperty, new GUIContent("Weapon"));
            return;
        }

        int selectedIndex = 0;
        WeaponData currentWeapon = weaponProperty.objectReferenceValue as WeaponData;

        string[] options = new string[catalog.Count + 1];
        options[0] = "(None)";
        for (int i = 0; i < catalog.Count; i++)
        {
            WeaponData optionWeapon = catalog[i];
            string label = !string.IsNullOrWhiteSpace(optionWeapon.displayName) ? optionWeapon.displayName : optionWeapon.name;
            options[i + 1] = $"{label} [{optionWeapon.id}]";
            if (optionWeapon == currentWeapon)
                selectedIndex = i + 1;
        }

        int newIndex = EditorGUILayout.Popup("Weapon", selectedIndex, options);
        WeaponData newWeapon = newIndex <= 0 ? null : catalog[newIndex - 1];
        if (newWeapon != currentWeapon)
            weaponProperty.objectReferenceValue = newWeapon;
    }

    private static void DrawTrajectoryPopup(WeaponData selectedWeapon, SerializedProperty trajectoryProperty)
    {
        if (trajectoryProperty == null)
            return;

        if (selectedWeapon == null || selectedWeapon.trajectories == null || selectedWeapon.trajectories.Count == 0)
        {
            trajectoryProperty.enumValueIndex = (int)WeaponTrajectoryType.Straight;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.EnumPopup("Selected Trajectory", WeaponTrajectoryType.Straight);
            return;
        }

        List<WeaponTrajectoryType> options = new List<WeaponTrajectoryType>(selectedWeapon.trajectories.Count);
        for (int i = 0; i < selectedWeapon.trajectories.Count; i++)
        {
            WeaponTrajectoryType option = selectedWeapon.trajectories[i];
            if (!options.Contains(option))
                options.Add(option);
        }

        if (options.Count == 0)
            options.Add(WeaponTrajectoryType.Straight);

        WeaponTrajectoryType current = (WeaponTrajectoryType)trajectoryProperty.enumValueIndex;
        int selectedIndex = 0;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == current)
            {
                selectedIndex = i;
                break;
            }
        }

        string[] labels = new string[options.Count];
        for (int i = 0; i < options.Count; i++)
            labels[i] = options[i].ToString();

        int newIndex = EditorGUILayout.Popup("Selected Trajectory", selectedIndex, labels);
        trajectoryProperty.enumValueIndex = (int)options[Mathf.Clamp(newIndex, 0, options.Count - 1)];
    }

    private static WeaponDatabase ResolveWeaponDatabaseFromProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:WeaponDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            WeaponDatabase db = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(path);
            if (db != null)
                return db;
        }

        return null;
    }

    private static List<SupplyData> BuildSupplyCatalog(SupplyDatabase database)
    {
        List<SupplyData> catalog = new List<SupplyData>();
        if (database == null || database.Supplies == null)
            return catalog;

        for (int i = 0; i < database.Supplies.Count; i++)
        {
            SupplyData supply = database.Supplies[i];
            if (supply != null)
                catalog.Add(supply);
        }

        return catalog;
    }

    private static void DrawSupplyPopup(SerializedProperty supplyProperty, List<SupplyData> catalog)
    {
        if (supplyProperty == null)
            return;

        if (catalog == null || catalog.Count == 0)
        {
            EditorGUILayout.PropertyField(supplyProperty, new GUIContent("Supply"));
            return;
        }

        int selectedIndex = 0;
        SupplyData currentSupply = supplyProperty.objectReferenceValue as SupplyData;

        string[] options = new string[catalog.Count + 1];
        options[0] = "(None)";
        for (int i = 0; i < catalog.Count; i++)
        {
            SupplyData optionSupply = catalog[i];
            string label = !string.IsNullOrWhiteSpace(optionSupply.displayName) ? optionSupply.displayName : optionSupply.name;
            options[i + 1] = $"{label} [{optionSupply.id}]";
            if (optionSupply == currentSupply)
                selectedIndex = i + 1;
        }

        int newIndex = EditorGUILayout.Popup("Supply", selectedIndex, options);
        SupplyData newSupply = newIndex <= 0 ? null : catalog[newIndex - 1];
        if (newSupply != currentSupply)
            supplyProperty.objectReferenceValue = newSupply;
    }

    private static string BuildSupplyFunctionText(SupplyData supply)
    {
        if (supply == null || supply.relatedServices == null || supply.relatedServices.Count == 0)
            return "None";

        List<string> labels = new List<string>(supply.relatedServices.Count);
        for (int i = 0; i < supply.relatedServices.Count; i++)
        {
            ServiceData service = supply.relatedServices[i];
            if (service == null)
                continue;

            string label = !string.IsNullOrWhiteSpace(service.displayName)
                ? service.displayName
                : service.name;
            string id = !string.IsNullOrWhiteSpace(service.id) ? service.id : "-";
            labels.Add($"{label} [{id}]");
        }

        if (labels.Count == 0)
            return "None";

        return string.Join(", ", labels);
    }

    private static SupplyDatabase ResolveSupplyDatabaseFromProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:SupplyDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SupplyDatabase db = AssetDatabase.LoadAssetAtPath<SupplyDatabase>(path);
            if (db != null)
                return db;
        }

        return null;
    }
}
