using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIPlayerController))]
public class AIPlayerControllerEditor : Editor
{
    private SerializedProperty matchControllerProp;
    private SerializedProperty turnStateManagerProp;
    private SerializedProperty aiLogProp;
    private SerializedProperty showPlanDebugAtUnitProp;
    private SerializedProperty aiDatabaseProp;
    private SerializedProperty battleStanceDatabaseProp;
    private SerializedProperty plannerDebugViewProp;
    private SerializedProperty shoppingDebugViewProp;
    private SerializedProperty plannerDebugSimulationTeamProp;

    private void OnEnable()
    {
        matchControllerProp = serializedObject.FindProperty("matchController");
        turnStateManagerProp = serializedObject.FindProperty("turnStateManager");
        aiLogProp = serializedObject.FindProperty("aiLog");
        showPlanDebugAtUnitProp = serializedObject.FindProperty("showPlanDebugAtUnit");
        aiDatabaseProp = serializedObject.FindProperty("aiDatabase");
        battleStanceDatabaseProp = serializedObject.FindProperty("battleStanceDatabase");
        plannerDebugViewProp = serializedObject.FindProperty("plannerDebugView");
        shoppingDebugViewProp = serializedObject.FindProperty("shoppingDebugView");
        plannerDebugSimulationTeamProp = serializedObject.FindProperty("plannerDebugSimulationTeam");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(matchControllerProp);
        EditorGUILayout.PropertyField(turnStateManagerProp);
        EditorGUILayout.PropertyField(aiLogProp);
        EditorGUILayout.PropertyField(showPlanDebugAtUnitProp, new GUIContent("Show Plan (Debug) At Unit"));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("AI Setup", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(aiDatabaseProp, new GUIContent("AI Database"));
        EditorGUILayout.PropertyField(battleStanceDatabaseProp, new GUIContent("Battle Stance Database"));

        AIPlayerController controller = target as AIPlayerController;
        MatchController match = matchControllerProp != null ? matchControllerProp.objectReferenceValue as MatchController : null;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Detectar Times IA + Pendurar Perfis"))
        {
            InvokeRefreshAssignments(controller);
            EditorUtility.SetDirty(controller);
        }
        if (GUILayout.Button("Ping AI Database"))
        {
            if (aiDatabaseProp.objectReferenceValue != null)
                EditorGUIUtility.PingObject(aiDatabaseProp.objectReferenceValue);
        }
        EditorGUILayout.EndHorizontal();

        DrawDetectedAssignments(controller, match);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Planner Runtime (Debug)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("A simulacao principal monta a lista de planos para todos os times com isAI=true no MatchController.", MessageType.None);
        if (plannerDebugSimulationTeamProp != null)
            EditorGUILayout.PropertyField(plannerDebugSimulationTeamProp, new GUIContent("Simulation Team (Manual)"));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Planner Debug View"))
        {
            controller?.RefreshPlannerDebugViewNow();
            EditorUtility.SetDirty(controller);
            serializedObject.Update();
        }
        if (GUILayout.Button("Simular Planos (Todos IA)"))
        {
            controller?.SimulatePlannerGenerationForDebugAllAiTeams();
            EditorUtility.SetDirty(controller);
            serializedObject.Update();
        }
        if (GUILayout.Button("Simular Time Manual"))
        {
            controller?.SimulatePlannerGenerationForDebugSelectedTeam();
            EditorUtility.SetDirty(controller);
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();

        DrawPlannerDebugView(controller, plannerDebugViewProp);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Shopping Runtime (Debug)", EditorStyles.boldLabel);
        DrawShoppingDebugView(controller, shoppingDebugViewProp);

        serializedObject.ApplyModifiedProperties();
    }

    private static void InvokeRefreshAssignments(AIPlayerController controller)
    {
        if (controller == null)
            return;

        System.Reflection.MethodInfo method = typeof(AIPlayerController).GetMethod(
            "RefreshAiTeamProfileAssignments",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        method?.Invoke(controller, null);
    }

    private static void DrawDetectedAssignments(AIPlayerController controller, MatchController match)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Binds Detectados", EditorStyles.boldLabel);

        if (controller == null)
        {
            EditorGUILayout.HelpBox("AIPlayerController invalido.", MessageType.Warning);
            return;
        }

        if (match == null)
        {
            EditorGUILayout.HelpBox("Defina um MatchController para detectar times IA.", MessageType.Info);
            return;
        }

        bool anyAi = false;
        IReadOnlyList<TeamId> players = match.Players;
        for (int i = 0; i < players.Count; i++)
        {
            TeamId team = players[i];
            if (!match.IsPlayerAI(team))
                continue;

            anyAi = true;
            bool hasData = controller.TryGetAssignedAIDataForTeam(team, out AIData data, out AIGeneralProfile profile);

            string profileName = profile != null ? profile.name : "(sem profile)";
            string dataName = hasData && data != null ? data.name : "(sem AIData)";
            string prefered = profile != null ? TeamUtils.GetName(profile.preferedTeamAssignment) : "-";
            EditorGUILayout.LabelField(
                TeamUtils.GetName(team),
                "Profile: " + profileName + " | AIData: " + dataName + " | PreferedTeam: " + prefered);

            if (profile != null)
            {
                EditorGUI.indentLevel++;
                int originalMaxPlans = profile.maxVariablePlans;
                int newMaxPlans = EditorGUILayout.IntField("Max Variable Plans", originalMaxPlans);
                if (newMaxPlans != originalMaxPlans)
                {
                    Undo.RecordObject(profile, "Alterar Max Variable Plans");
                    profile.maxVariablePlans = Mathf.Max(0, newMaxPlans);
                    EditorUtility.SetDirty(profile);
                }
                EditorGUI.indentLevel--;
            }
        }

        if (!anyAi)
            EditorGUILayout.HelpBox("Nenhum time com isAI=true detectado no MatchController.", MessageType.Info);
    }

    private static void DrawPlannerDebugView(AIPlayerController controller, SerializedProperty plannerDebugViewProperty)
    {
        if (controller == null)
        {
            EditorGUILayout.HelpBox("AIPlayerController invalido.", MessageType.Warning);
            return;
        }

        IReadOnlyList<AIPlayerController.TeamPlannerDebugView> teams = controller.PlannerDebugView;
        if ((teams == null || teams.Count == 0) && (plannerDebugViewProperty == null || !plannerDebugViewProperty.isArray || plannerDebugViewProperty.arraySize == 0))
        {
            EditorGUILayout.HelpBox("Nenhum plano em debug. Clique em Simular Planos (Todos IA).", MessageType.None);
            return;
        }

        if (plannerDebugViewProperty == null)
        {
            EditorGUILayout.HelpBox("plannerDebugView nao encontrado para desenhar a lista.", MessageType.Warning);
            return;
        }

        DrawPlannerDebugTeamsList(plannerDebugViewProperty);
    }

    private static void DrawPlannerDebugTeamsList(SerializedProperty plannerDebugViewProperty)
    {
        plannerDebugViewProperty.isExpanded = EditorGUILayout.Foldout(plannerDebugViewProperty.isExpanded, $"Planner Debug View ({plannerDebugViewProperty.arraySize})", true);
        if (!plannerDebugViewProperty.isExpanded)
            return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < plannerDebugViewProperty.arraySize; i++)
        {
            SerializedProperty teamElement = plannerDebugViewProperty.GetArrayElementAtIndex(i);
            if (teamElement == null)
                continue;

            DrawPlannerTeamElement(teamElement, i);
        }
        EditorGUI.indentLevel--;
    }

    private static void DrawShoppingDebugView(AIPlayerController controller, SerializedProperty shoppingDebugViewProperty)
    {
        if (controller == null)
        {
            EditorGUILayout.HelpBox("AIPlayerController invalido.", MessageType.Warning);
            return;
        }

        IReadOnlyList<AIPlayerController.TeamShoppingDebugView> teams = controller.ShoppingDebugView;
        if ((teams == null || teams.Count == 0) && (shoppingDebugViewProperty == null || !shoppingDebugViewProperty.isArray || shoppingDebugViewProperty.arraySize == 0))
        {
            EditorGUILayout.HelpBox("Nenhum plano de compras em debug ainda. A lista aparece depois que a IA monta a Fase 3.", MessageType.None);
            return;
        }

        if (shoppingDebugViewProperty == null)
        {
            EditorGUILayout.HelpBox("shoppingDebugView nao encontrado para desenhar a lista.", MessageType.Warning);
            return;
        }

        shoppingDebugViewProperty.isExpanded = EditorGUILayout.Foldout(shoppingDebugViewProperty.isExpanded, $"Shopping Debug View ({shoppingDebugViewProperty.arraySize})", true);
        if (!shoppingDebugViewProperty.isExpanded)
            return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < shoppingDebugViewProperty.arraySize; i++)
        {
            SerializedProperty teamElement = shoppingDebugViewProperty.GetArrayElementAtIndex(i);
            if (teamElement == null)
                continue;

            DrawShoppingTeamElement(teamElement, i);
        }
        EditorGUI.indentLevel--;
    }

    private static void DrawShoppingTeamElement(SerializedProperty teamElement, int index)
    {
        SerializedProperty teamProp = teamElement.FindPropertyRelative("team");
        SerializedProperty stanceProp = teamElement.FindPropertyRelative("currentStanceName");
        SerializedProperty turnProp = teamElement.FindPropertyRelative("turnNumber");
        SerializedProperty totalMoneyProp = teamElement.FindPropertyRelative("totalMoney");
        SerializedProperty reservedMoneyProp = teamElement.FindPropertyRelative("reservedMoney");
        SerializedProperty freeMoneyProp = teamElement.FindPropertyRelative("freeMoney");
        SerializedProperty saveTargetMoneyProp = teamElement.FindPropertyRelative("saveTargetMoney");
        SerializedProperty strategicReserveMoneyProp = teamElement.FindPropertyRelative("strategicReserveMoney");
        SerializedProperty massFloorBlockedProp = teamElement.FindPropertyRelative("massFloorBlocked");
        SerializedProperty massFloorCurrentUnitsProp = teamElement.FindPropertyRelative("massFloorCurrentUnits");
        SerializedProperty massFloorRequiredUnitsProp = teamElement.FindPropertyRelative("massFloorRequiredUnits");
        SerializedProperty massFloorReasonProp = teamElement.FindPropertyRelative("massFloorReason");
        SerializedProperty criticalGapProp = teamElement.FindPropertyRelative("hasCriticalCaptureGap");
        SerializedProperty strategicSaveActiveProp = teamElement.FindPropertyRelative("strategicSaveActive");
        SerializedProperty strategicSaveUnitIdProp = teamElement.FindPropertyRelative("strategicSaveUnitId");
        SerializedProperty strategicSaveSourceLabelProp = teamElement.FindPropertyRelative("strategicSaveSourceLabel");
        SerializedProperty strategicSaveCostProp = teamElement.FindPropertyRelative("strategicSaveCost");
        SerializedProperty strategicSaveTurnsProp = teamElement.FindPropertyRelative("strategicSaveTurnsToAfford");
        SerializedProperty strategicSaveDeferredOrdersProp = teamElement.FindPropertyRelative("strategicSaveDeferredOrders");
        SerializedProperty strategicSaveReasonProp = teamElement.FindPropertyRelative("strategicSaveReason");
        SerializedProperty ordersProp = teamElement.FindPropertyRelative("orders");
        SerializedProperty decisionsProp = teamElement.FindPropertyRelative("decisions");
        string teamLabel = ResolveTeamElementLabel(teamProp, index, stanceProp);

        teamElement.isExpanded = EditorGUILayout.Foldout(teamElement.isExpanded, teamLabel, true);
        if (!teamElement.isExpanded)
            return;

        EditorGUI.indentLevel++;
        if (turnProp != null) EditorGUILayout.PropertyField(turnProp);
        if (totalMoneyProp != null) EditorGUILayout.PropertyField(totalMoneyProp);
        if (reservedMoneyProp != null) EditorGUILayout.PropertyField(reservedMoneyProp);
        if (freeMoneyProp != null) EditorGUILayout.PropertyField(freeMoneyProp);
        if (saveTargetMoneyProp != null) EditorGUILayout.PropertyField(saveTargetMoneyProp);
        if (strategicReserveMoneyProp != null) EditorGUILayout.PropertyField(strategicReserveMoneyProp);
        if (massFloorBlockedProp != null) EditorGUILayout.PropertyField(massFloorBlockedProp);
        if (massFloorCurrentUnitsProp != null) EditorGUILayout.PropertyField(massFloorCurrentUnitsProp);
        if (massFloorRequiredUnitsProp != null) EditorGUILayout.PropertyField(massFloorRequiredUnitsProp);
        if (massFloorReasonProp != null) EditorGUILayout.PropertyField(massFloorReasonProp);
        if (criticalGapProp != null) EditorGUILayout.PropertyField(criticalGapProp);
        if (strategicSaveActiveProp != null) EditorGUILayout.PropertyField(strategicSaveActiveProp);
        if (strategicSaveUnitIdProp != null) EditorGUILayout.PropertyField(strategicSaveUnitIdProp);
        if (strategicSaveSourceLabelProp != null) EditorGUILayout.PropertyField(strategicSaveSourceLabelProp);
        if (strategicSaveCostProp != null) EditorGUILayout.PropertyField(strategicSaveCostProp);
        if (strategicSaveTurnsProp != null) EditorGUILayout.PropertyField(strategicSaveTurnsProp);
        if (strategicSaveDeferredOrdersProp != null) EditorGUILayout.PropertyField(strategicSaveDeferredOrdersProp);
        if (strategicSaveReasonProp != null) EditorGUILayout.PropertyField(strategicSaveReasonProp);

        if (ordersProp != null)
            DrawSimpleArray(ordersProp, "Orders");
        if (decisionsProp != null)
            DrawSimpleArray(decisionsProp, "Decisions");
        EditorGUI.indentLevel--;
    }

    private static void DrawSimpleArray(SerializedProperty arrayProp, string label)
    {
        if (arrayProp == null || !arrayProp.isArray)
        {
            EditorGUILayout.LabelField(label, "(indisponivel)");
            return;
        }

        arrayProp.isExpanded = EditorGUILayout.Foldout(arrayProp.isExpanded, $"{label} ({arrayProp.arraySize})", true);
        if (!arrayProp.isExpanded)
            return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            string itemLabel = BuildArrayElementLabel(element, i, label);
            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, itemLabel, true);
            if (!element.isExpanded)
                continue;

            EditorGUI.indentLevel++;
            SerializedProperty iterator = element.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    private static string BuildArrayElementLabel(SerializedProperty element, int index, string fallbackLabel)
    {
        SerializedProperty orderId = element.FindPropertyRelative("orderId");
        if (orderId != null && !string.IsNullOrWhiteSpace(orderId.stringValue))
            return $"[{index}] {orderId.stringValue}";

        SerializedProperty constructionLabel = element.FindPropertyRelative("constructionLabel");
        if (constructionLabel != null && !string.IsNullOrWhiteSpace(constructionLabel.stringValue))
            return $"[{index}] {constructionLabel.stringValue}";

        SerializedProperty planLabel = element.FindPropertyRelative("planLabel");
        if (planLabel != null && !string.IsNullOrWhiteSpace(planLabel.stringValue))
            return $"[{index}] {planLabel.stringValue}";

        return $"{fallbackLabel} {index}";
    }

    private static void DrawPlannerTeamElement(SerializedProperty teamElement, int index)
    {
        SerializedProperty teamProp = teamElement.FindPropertyRelative("team");
        SerializedProperty stanceProp = teamElement.FindPropertyRelative("currentStanceName");
        SerializedProperty plansProp = teamElement.FindPropertyRelative("plans");
        string teamLabel = ResolveTeamElementLabel(teamProp, index, stanceProp);

        teamElement.isExpanded = EditorGUILayout.Foldout(teamElement.isExpanded, teamLabel, true);
        if (!teamElement.isExpanded)
            return;

        EditorGUI.indentLevel++;
        if (teamProp != null)
            EditorGUILayout.PropertyField(teamProp);

        if (plansProp == null || !plansProp.isArray)
        {
            EditorGUILayout.LabelField("Plans", "(indisponivel)");
            EditorGUI.indentLevel--;
            return;
        }

        DrawPlanSection(plansProp, "Active Plans", IsActiveVariablePlanElement);
        DrawPlanSection(plansProp, "Inactive Plans", IsInactiveVariablePlanElement);
        EditorGUI.indentLevel--;
    }

    private static void DrawPlanSection(SerializedProperty plansProp, string label, System.Func<SerializedProperty, bool> predicate)
    {
        List<int> indexes = new List<int>();
        for (int i = 0; i < plansProp.arraySize; i++)
        {
            SerializedProperty planElement = plansProp.GetArrayElementAtIndex(i);
            if (planElement != null && predicate(planElement))
                indexes.Add(i);
        }

        bool expanded = SessionState.GetBool(GetPlanSectionStateKey(plansProp.propertyPath, label), true);
        expanded = EditorGUILayout.Foldout(expanded, $"{label} ({indexes.Count})", true);
        SessionState.SetBool(GetPlanSectionStateKey(plansProp.propertyPath, label), expanded);
        if (!expanded)
            return;

        EditorGUI.indentLevel++;
        if (indexes.Count == 0)
        {
            EditorGUILayout.LabelField("(nenhum)", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
            return;
        }

        for (int i = 0; i < indexes.Count; i++)
        {
            SerializedProperty planElement = plansProp.GetArrayElementAtIndex(indexes[i]);
            if (planElement == null)
                continue;

            string planLabel = ResolvePlanElementLabel(planElement, indexes[i]);
            EditorGUILayout.PropertyField(planElement, new GUIContent(planLabel), true);
        }
        EditorGUI.indentLevel--;
    }

    private static string GetPlanSectionStateKey(string propertyPath, string label)
    {
        return $"AIPlayerControllerEditor.{propertyPath}.{label}";
    }

    private static bool IsActiveVariablePlanElement(SerializedProperty planElement)
    {
        return string.Equals(GetStringRelative(planElement, "status"), "Active", System.StringComparison.Ordinal);
    }

    private static bool IsInactiveVariablePlanElement(SerializedProperty planElement)
    {
        return !string.Equals(GetStringRelative(planElement, "status"), "Active", System.StringComparison.Ordinal);
    }

    private static string ResolvePlanElementLabel(SerializedProperty planElement, int index)
    {
        string displayName = GetStringRelative(planElement, "displayName");
        string status = GetStringRelative(planElement, "status");
        int risk = GetIntRelative(planElement, "tacticalRiskScore");
        string roleSuffix = BuildAssignmentCountSuffix(planElement != null ? planElement.FindPropertyRelative("assignments") : null);
        string riskSuffix = risk > 0 ? $" | risk={risk}" : string.Empty;
        string suffix = roleSuffix + riskSuffix;
        if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(status))
            return $"{displayName} [{status}]{suffix}";
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName + suffix;
        return $"Plan {index}{suffix}";
    }

    private static string BuildAssignmentCountSuffix(SerializedProperty assignmentsProp)
    {
        int capture = 0;
        int escort = 0;
        int artillery = 0;
        int support = 0;

        if (assignmentsProp != null && assignmentsProp.isArray)
        {
            for (int i = 0; i < assignmentsProp.arraySize; i++)
            {
                SerializedProperty assignmentProp = assignmentsProp.GetArrayElementAtIndex(i);
                string role = GetStringRelative(assignmentProp, "role");
                CountRole(role, ref capture, ref escort, ref artillery, ref support);
            }
        }

        return $" | CAP={capture} ESC={escort} ART={artillery} SUP={support}";
    }

    private static void CountRole(string role, ref int capture, ref int escort, ref int artillery, ref int support)
    {
        if (string.IsNullOrWhiteSpace(role))
            return;

        switch (role.Trim().ToLowerInvariant())
        {
            case "capture":
                capture++;
                break;
            case "escort":
                escort++;
                break;
            case "artillery":
            case "firesupport":
            case "fire support":
                artillery++;
                break;
            case "support":
            case "logistics":
                support++;
                break;
        }
    }

    private static string GetStringRelative(SerializedProperty property, string relativeName)
    {
        SerializedProperty child = property != null ? property.FindPropertyRelative(relativeName) : null;
        return child != null ? child.stringValue ?? string.Empty : string.Empty;
    }

    private static int GetIntRelative(SerializedProperty property, string relativeName)
    {
        SerializedProperty child = property != null ? property.FindPropertyRelative(relativeName) : null;
        return child != null ? child.intValue : 0;
    }

    private static string ResolveTeamElementLabel(SerializedProperty teamProp, int index, SerializedProperty stanceProp = null)
    {
        string fallbackLabel = $"Time {index}";
        string stanceName = stanceProp != null ? stanceProp.stringValue : string.Empty;

        if (teamProp == null)
            return !string.IsNullOrWhiteSpace(stanceName)
                ? $"{fallbackLabel} [{stanceName}]"
                : fallbackLabel;

        if (teamProp.propertyType == SerializedPropertyType.Enum)
        {
            string enumName = teamProp.enumDisplayNames != null
                && teamProp.enumValueIndex >= 0
                && teamProp.enumValueIndex < teamProp.enumDisplayNames.Length
                ? teamProp.enumDisplayNames[teamProp.enumValueIndex]
                : teamProp.enumNames != null
                    && teamProp.enumValueIndex >= 0
                    && teamProp.enumValueIndex < teamProp.enumNames.Length
                        ? teamProp.enumNames[teamProp.enumValueIndex]
                        : string.Empty;

            if (!string.IsNullOrWhiteSpace(enumName))
                return !string.IsNullOrWhiteSpace(stanceName)
                    ? $"{enumName} [{stanceName}]"
                    : enumName;
        }

        return !string.IsNullOrWhiteSpace(stanceName)
            ? $"{fallbackLabel} [{stanceName}]"
            : fallbackLabel;
    }
}
