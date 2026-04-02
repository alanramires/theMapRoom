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
    private SerializedProperty aiPlanDatabaseProp;
    private SerializedProperty plannerDebugViewProp;

    private void OnEnable()
    {
        matchControllerProp = serializedObject.FindProperty("matchController");
        turnStateManagerProp = serializedObject.FindProperty("turnStateManager");
        aiLogProp = serializedObject.FindProperty("aiLog");
        showPlanDebugAtUnitProp = serializedObject.FindProperty("showPlanDebugAtUnit");
        aiDatabaseProp = serializedObject.FindProperty("aiDatabase");
        aiPlanDatabaseProp = serializedObject.FindProperty("aiPlanDatabase");
        plannerDebugViewProp = serializedObject.FindProperty("plannerDebugView");
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
        EditorGUILayout.PropertyField(aiPlanDatabaseProp, new GUIContent("AI Plan Database"));

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
        if (GUILayout.Button("Refresh Planner Debug View"))
        {
            controller?.RefreshPlannerDebugViewNow();
            EditorUtility.SetDirty(controller);
            serializedObject.Update();
        }
        if (plannerDebugViewProp != null)
            EditorGUILayout.PropertyField(plannerDebugViewProp, new GUIContent("Planner Debug View"), true);

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
        }

        if (!anyAi)
            EditorGUILayout.HelpBox("Nenhum time com isAI=true detectado no MatchController.", MessageType.Info);
    }
}
