using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AIPlannerWindow : EditorWindow
{
    [SerializeField] private AIPlanDatabase database;
    [SerializeField] private MatchController previewMatchController;
    [SerializeField] private AIPlayerController previewAiController;
    [SerializeField] private TeamId previewTeam = TeamId.Blue;
    [SerializeField] private bool previewUseFogVisibility;
    [SerializeField] private bool previewShowSceneOverlay = true;
    [SerializeField] private TeamId previewSelectedPlanTeam = TeamId.Neutral;
    [SerializeField] private string previewSelectedPlanKey = string.Empty;
    [SerializeField] private int previewSelectedAssignmentUnitId = -1;
    [SerializeField] private Vector2 scroll;
    [SerializeField] private string previewStatus = "Sem preview.";

    [MenuItem("Tools/AI/AI Planner")]
    public static void OpenWindow()
    {
        GetWindow<AIPlannerWindow>("AI Planner");
    }

    private void OnEnable()
    {
        AutoDetect();
        AutoDetectPreviewContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("AI Planner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Painel de preparacao de planos da IA. Usa o mesmo catalogo/runtime do AI Manager: Fixed Plans, Active Plans e Inactive Plans por time IA.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        database = (AIPlanDatabase)EditorGUILayout.ObjectField(
            "Plan Database",
            database,
            typeof(AIPlanDatabase),
            false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))
            AutoDetect();
        if (GUILayout.Button("Create Defaults"))
            CreateDefaultAssets();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        if (database == null)
        {
            EditorGUILayout.HelpBox("Nenhum AIPlanDatabase selecionado.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        database.EnsureDefaults();
        DrawDatabaseSummary();
        DrawPlanField("Defense Plan", database.defensePlan);
        DrawPlanField("Attack Plan", database.attackPlan);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Variable Sector Plans Budget", EditorStyles.boldLabel);
        SerializedObject so = new SerializedObject(database);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("maxVariablePlans"));
        so.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);
        DrawPreviewSection();

        if (GUI.changed)
            EditorUtility.SetDirty(database);

        EditorGUILayout.EndScrollView();
    }

    private void DrawDatabaseSummary()
    {
        string path = AssetDatabase.GetAssetPath(database);
        int fixedCount = (database.defensePlan != null ? 1 : 0) + (database.attackPlan != null ? 1 : 0);

        EditorGUILayout.HelpBox(
            $"Asset: {path}\nFixed: {fixedCount}/2 | Variable Active Budget: {database.maxVariablePlans} setores ativos por turno",
            MessageType.None);
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("AI Player Automatic Planner (Preview)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Gera planos usando as unidades e construcoes da cena atual para prever o comportamento da IA no opener.",
            MessageType.None);

        previewAiController = (AIPlayerController)EditorGUILayout.ObjectField(
            "AI Player Controller",
            previewAiController,
            typeof(AIPlayerController),
            true);
        previewMatchController = (MatchController)EditorGUILayout.ObjectField(
            "Match Controller (opcional)",
            previewMatchController,
            typeof(MatchController),
            true);
        previewTeam = (TeamId)EditorGUILayout.EnumPopup("Overlay Team", previewTeam);
        previewShowSceneOverlay = EditorGUILayout.ToggleLeft(
            "Mostrar overlay no Scene (circulos e participantes)",
            previewShowSceneOverlay);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect Context"))
            AutoDetectPreviewContext();
        if (GUILayout.Button("Generate Preview (All AI)"))
            GeneratePreview();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(previewStatus, MessageType.None);

        DrawPreviewTeams();

        EditorGUILayout.EndVertical();
    }

    private static void DrawPlanField(string label, AIPlanData plan)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        if (plan == null)
        {
            EditorGUILayout.LabelField("(not assigned)");
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField("Name", string.IsNullOrWhiteSpace(plan.displayName) ? plan.name : plan.displayName);
        EditorGUILayout.LabelField("Kind", plan.kind.ToString());
        EditorGUILayout.LabelField("Target Sector", plan.targetSector.ToString());
        EditorGUILayout.LabelField("Participants", plan.participants != null ? plan.participants.Count.ToString() : "0");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select", GUILayout.Width(80f)))
            Selection.activeObject = plan;
        if (GUILayout.Button("Ping", GUILayout.Width(80f)))
            EditorGUIUtility.PingObject(plan);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewTeams()
    {
        if (previewAiController == null)
        {
            EditorGUILayout.HelpBox("Defina um AIPlayerController para usar o preview do planner atual.", MessageType.Info);
            return;
        }

        IReadOnlyList<AIPlayerController.TeamPlannerDebugView> teams = previewAiController.PlannerDebugView;
        if (teams == null || teams.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum plano gerado. Clique em Generate Preview (All AI).", MessageType.None);
            return;
        }

        for (int i = 0; i < teams.Count; i++)
        {
            AIPlayerController.TeamPlannerDebugView team = teams[i];
            if (team == null)
                continue;

            DrawPreviewTeamBlock(team);
        }
    }

    private void DrawPreviewTeamBlock(AIPlayerController.TeamPlannerDebugView team)
    {
        string teamLabel = TeamUtils.GetName(team.team);
        string teamKey = $"AIPlannerWindow.Team.{team.team}";
        bool expanded = SessionState.GetBool(teamKey, true);
        expanded = EditorGUILayout.Foldout(expanded, teamLabel, true);
        SessionState.SetBool(teamKey, expanded);
        if (!expanded)
            return;

        EditorGUI.indentLevel++;
        DrawPreviewPlanGroup(team, "Fixed Plans", IsFixedPlan);
        DrawPreviewPlanGroup(team, "Active Plans", IsActiveVariablePlan);
        DrawPreviewPlanGroup(team, "Inactive Plans", IsInactiveVariablePlan);
        EditorGUI.indentLevel--;
    }

    private void DrawPreviewPlanGroup(AIPlayerController.TeamPlannerDebugView team, string label, System.Func<AIPlayerController.PlanDebugView, bool> predicate)
    {
        List<AIPlayerController.PlanDebugView> plans = new List<AIPlayerController.PlanDebugView>();
        if (team.plans != null)
        {
            for (int i = 0; i < team.plans.Count; i++)
            {
                AIPlayerController.PlanDebugView plan = team.plans[i];
                if (plan != null && predicate(plan))
                    plans.Add(plan);
            }
        }

        string sectionKey = $"AIPlannerWindow.Team.{team.team}.{label}";
        bool expanded = SessionState.GetBool(sectionKey, true);
        expanded = EditorGUILayout.Foldout(expanded, $"{label} ({plans.Count})", true);
        SessionState.SetBool(sectionKey, expanded);
        if (!expanded)
            return;

        EditorGUI.indentLevel++;
        if (plans.Count == 0)
        {
            EditorGUILayout.LabelField("(nenhum)", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
            return;
        }

        for (int i = 0; i < plans.Count; i++)
            DrawPreviewPlanRow(team.team, plans[i]);
        EditorGUI.indentLevel--;
    }

    private void DrawPreviewPlanRow(TeamId team, AIPlayerController.PlanDebugView plan)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{plan.displayName} [{plan.status}]", EditorStyles.boldLabel);
        bool isSelectedPlan = previewSelectedPlanTeam == team && string.Equals(previewSelectedPlanKey, plan.planKey, System.StringComparison.Ordinal);
        bool canOverlay = TryFindIntentForPlan(team, plan.planKey, out AIPlanIntent _);
        EditorGUI.BeginDisabledGroup(!canOverlay);
        if (GUILayout.Button(isSelectedPlan ? "Hide" : "Show", GUILayout.Width(64f)))
        {
            if (isSelectedPlan)
                ClearPreviewSelection();
            else
            {
                previewSelectedPlanTeam = team;
                previewSelectedPlanKey = plan.planKey ?? string.Empty;
                previewSelectedAssignmentUnitId = -1;
                previewTeam = team;
            }
            SceneView.RepaintAll();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        string riskText = plan.tacticalRiskScore > 0 ? $" | risk={plan.tacticalRiskScore}" : string.Empty;
        EditorGUILayout.LabelField($"Sector: {plan.sector} | progresso={plan.progressCurrent}/{plan.progressMax} | conquistado={plan.conquered}{riskText}", EditorStyles.miniLabel);
        if (!string.IsNullOrWhiteSpace(plan.selectionReason))
            EditorGUILayout.LabelField($"criteria: {plan.selectionReason}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"key={plan.planKey}", EditorStyles.miniLabel);

        if (plan.assignments == null || plan.assignments.Count == 0)
        {
            EditorGUILayout.LabelField("participantes: (nenhum)", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        for (int i = 0; i < plan.assignments.Count; i++)
        {
            AIPlayerController.AssignmentDebugView assignment = plan.assignments[i];
            if (assignment == null)
                continue;

            EditorGUILayout.BeginHorizontal();
            bool isSelectedAssignment = isSelectedPlan && previewSelectedAssignmentUnitId == assignment.unitInstanceId;
            if (GUILayout.Button(isSelectedAssignment ? "On" : "Sel", GUILayout.Width(36f)))
            {
                previewSelectedPlanTeam = team;
                previewSelectedPlanKey = plan.planKey ?? string.Empty;
                previewSelectedAssignmentUnitId = isSelectedAssignment ? -1 : assignment.unitInstanceId;
                UnitManager unit = FindUnitByInstanceId(assignment.unitInstanceId);
                if (unit != null)
                {
                    Selection.activeObject = unit.gameObject;
                    EditorGUIUtility.PingObject(unit.gameObject);
                }
                SceneView.RepaintAll();
            }
            EditorGUILayout.LabelField($"- {assignment.unitName} ({assignment.role})", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private static bool IsFixedPlan(AIPlayerController.PlanDebugView plan)
    {
        return plan != null && string.Equals(plan.sector, ConstructionSector.BaseTeam.ToString(), System.StringComparison.Ordinal);
    }

    private static bool IsActiveVariablePlan(AIPlayerController.PlanDebugView plan)
    {
        return plan != null && !IsFixedPlan(plan) && string.Equals(plan.status, "Active", System.StringComparison.Ordinal);
    }

    private static bool IsInactiveVariablePlan(AIPlayerController.PlanDebugView plan)
    {
        return plan != null && !IsFixedPlan(plan) && !string.Equals(plan.status, "Active", System.StringComparison.Ordinal);
    }

    private void ClearPreviewSelection()
    {
        previewSelectedPlanTeam = TeamId.Neutral;
        previewSelectedPlanKey = string.Empty;
        previewSelectedAssignmentUnitId = -1;
    }

    private static int CountPreviewPlans(IReadOnlyList<AIPlayerController.TeamPlannerDebugView> teams)
    {
        int count = 0;
        if (teams == null)
            return count;

        for (int i = 0; i < teams.Count; i++)
        {
            AIPlayerController.TeamPlannerDebugView team = teams[i];
            if (team?.plans != null)
                count += team.plans.Count;
        }

        return count;
    }

    private bool TryGetSelectedPreviewIntent(out AIPlanIntent intent)
    {
        return TryFindIntentForPlan(previewSelectedPlanTeam, previewSelectedPlanKey, out intent);
    }

    private bool TryFindIntentForPlan(TeamId team, string planKey, out AIPlanIntent intent)
    {
        intent = null;
        if (previewAiController == null || string.IsNullOrWhiteSpace(planKey))
            return false;

        IReadOnlyList<AIPlanIntent> plans = previewAiController.GetCurrentTurnPlansForDebugTeam(team);
        if (plans == null)
            return false;

        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent candidate = plans[i];
            if (candidate == null)
                continue;
            if (string.Equals(AIPlanEvaluator.BuildPlanKey(candidate), planKey, System.StringComparison.Ordinal))
            {
                intent = candidate;
                return true;
            }
        }

        return false;
    }

    private void AutoDetect()
    {
        if (database != null)
            return;

        string[] guids = AssetDatabase.FindAssets("t:AIPlanDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AIPlanDatabase found = AssetDatabase.LoadAssetAtPath<AIPlanDatabase>(path);
            if (found == null)
                continue;

            database = found;
            Repaint();
            return;
        }
    }

    private void AutoDetectPreviewContext()
    {
        if (previewAiController == null)
            previewAiController = Object.FindAnyObjectByType<AIPlayerController>();
        if (previewMatchController == null)
            previewMatchController = Object.FindAnyObjectByType<MatchController>();
    }

    private void SyncControllerPlanDatabase()
    {
        if (previewAiController == null || database == null)
            return;

        SerializedObject controllerSo = new SerializedObject(previewAiController);
        SerializedProperty planDbProp = controllerSo.FindProperty("aiPlanDatabase");
        if (planDbProp == null)
            return;

        if (planDbProp.objectReferenceValue == database)
            return;

        planDbProp.objectReferenceValue = database;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(previewAiController);
    }

    private void GeneratePreview()
    {
        if (database == null)
        {
            previewStatus = "Selecione um AIPlanDatabase.";
            ClearPreviewSelection();
            Repaint();
            return;
        }

        AutoDetectPreviewContext();
        if (previewAiController == null)
        {
            previewStatus = "Nenhum AIPlayerController encontrado na cena.";
            ClearPreviewSelection();
            Repaint();
            return;
        }

        SyncControllerPlanDatabase();
        previewAiController.SimulatePlannerGenerationForDebugAllAiTeams();
        IReadOnlyList<AIPlayerController.TeamPlannerDebugView> teams = previewAiController.PlannerDebugView;
        int teamCount = teams != null ? teams.Count : 0;
        int totalPlans = CountPreviewPlans(teams);

        previewStatus =
            $"AI Controller={previewAiController.name} | Times IA={teamCount} | Planos catalogados={totalPlans} | Overlay Team={TeamUtils.GetName(previewTeam)}";
        SceneView.RepaintAll();
        Repaint();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!previewShowSceneOverlay)
            return;
        if (previewAiController == null)
            return;
        if (!TryGetSelectedPreviewIntent(out AIPlanIntent intent))
            return;
        if (intent.Assignments == null || intent.Assignments.Count == 0)
            return;

        Color planColor = GetPlanColor(intent.Sector);
        ConstructionManager[] constructions =
            Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Handles.color = new Color(planColor.r, planColor.g, planColor.b, 0.9f);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager c = constructions[i];
            if (c == null || c.Sector != intent.Sector)
                continue;

            Vector3 pos = c.transform.position;
            pos.z = 0f;
            Handles.DrawWireDisc(pos, Vector3.forward, 1.2f);
        }

        Vector3 fallbackTarget = intent.HasCaptureTarget
            ? CellToWorld(intent.CaptureTargetCell)
            : ComputeSectorCentroidWorld(intent.Sector);

        for (int a = 0; a < intent.Assignments.Count; a++)
        {
            AIPlanAssignment assignment = intent.Assignments[a];
            if (assignment == null)
                continue;

            UnitManager unit = FindUnitByInstanceId(assignment.UnitInstanceId);
            if (unit == null)
                continue;

            Vector3 from = unit.transform.position;
            from.z = 0f;
            Vector3 to = assignment.HasPlannedCaptureTarget
                ? CellToWorld(assignment.PlannedCaptureCell)
                : fallbackTarget;
            to.z = 0f;

            bool selected = previewSelectedAssignmentUnitId == assignment.UnitInstanceId;
            Handles.color = selected
                ? new Color(1f, 0.95f, 0.2f, 1f)
                : new Color(planColor.r, planColor.g, planColor.b, 0.7f);
            Handles.DrawAAPolyLine(selected ? 5f : 3f, new[] { from, to });
            Handles.SphereHandleCap(0, to, Quaternion.identity, selected ? 0.22f : 0.14f, EventType.Repaint);
        }
    }

    private static AISnapshot BuildEditorSnapshot(TeamId aiTeam, MatchController matchController, bool respectFogVisibility)
    {
        AISnapshot snapshot = new AISnapshot
        {
            AiTeam = aiTeam,
            HqDefendRadius = AISnapshot.DefaultDefendRadius
        };

        ConstructionManager[] constructions =
            Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        UnitManager[] units =
            Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager c = constructions[i];
            if (c == null || !c.gameObject.activeInHierarchy)
                continue;

            AIConstructionInfo info = new AIConstructionInfo
            {
                Cell = c.CurrentCellPosition,
                TeamId = c.TeamId,
                IsHq = c.IsPlayerHeadQuarter,
                IsCapturable = c.IsCapturable,
                CapturePoints = c.CurrentCapturePoints,
                CapturePointsMax = c.CapturePointsMax,
                IsVictoryBuilding = c.IsVictoryBuilding,
                CanProduceUnits = c.CanProduceUnits,
                DisplayName = c.ConstructionDisplayName,
                Sector = c.Sector,
                Source = c
            };

            snapshot.KnownConstructions.Add(info);

            if (c.IsPlayerHeadQuarter)
            {
                if (c.TeamId == aiTeam)
                {
                    snapshot.HasHq = true;
                    snapshot.HqCell = c.CurrentCellPosition;
                    snapshot.HqCell.z = 0;
                }
                else
                {
                    snapshot.EnemyHqs.Add(info);
                }
            }
        }

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager u = units[i];
            if (u != null && u.BoardTilemap != null)
            {
                snapshot.BoardTilemap = u.BoardTilemap;
                break;
            }
        }

        if (snapshot.BoardTilemap == null)
        {
            for (int i = 0; i < constructions.Length; i++)
            {
                ConstructionManager c = constructions[i];
                if (c != null && c.BoardTilemap != null)
                {
                    snapshot.BoardTilemap = c.BoardTilemap;
                    break;
                }
            }
        }

        if (snapshot.HasHq && snapshot.BoardTilemap != null)
        {
            Vector3Int hqCell = snapshot.HqCell;
            for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
            {
                AIConstructionInfo info = snapshot.KnownConstructions[i];
                Vector3Int cell = info.Cell;
                cell.z = 0;
                if (HexCoordinates.IsWithinRange(snapshot.BoardTilemap, hqCell, cell, snapshot.HqDefendRadius))
                    snapshot.ConstructionsNearHq.Add(info);
            }
        }

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager u = units[i];
            if (u == null || !u.gameObject.activeInHierarchy || u.IsDead || u.IsEmbarked)
                continue;

            if (u.TeamId == aiTeam)
            {
                snapshot.FriendlyUnits.Add(u);
                continue;
            }

            bool visible = true;
            if (respectFogVisibility && Application.isPlaying && matchController != null)
                visible = matchController.IsUnitVisibleForTeam(u, aiTeam);

            if (visible)
                snapshot.VisibleEnemies.Add(u);
        }

        return snapshot;
    }

    private static string GetIntentTitle(AIPlanIntent intent)
    {
        if (!string.IsNullOrWhiteSpace(intent.DisplayName))
            return intent.DisplayName;
        if (intent.Plan != null && !string.IsNullOrWhiteSpace(intent.Plan.planId))
            return intent.Plan.planId;
        if (intent.Plan != null)
            return intent.Plan.kind.ToString();
        return "Dynamic";
    }

    private static string FindUnitNameByInstanceId(int instanceId)
    {
        UnitManager[] units =
            Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager u = units[i];
            if (u != null && u.InstanceId == instanceId)
                return u.name;
        }

        return $"Unit#{instanceId}";
    }

    private static UnitManager FindUnitByInstanceId(int instanceId)
    {
        UnitManager[] units =
            Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager u = units[i];
            if (u != null && u.InstanceId == instanceId)
                return u;
        }

        return null;
    }

    private static Color GetPlanColor(ConstructionSector sector)
    {
        switch (sector)
        {
            case ConstructionSector.Alpha: return new Color(0.30f, 0.60f, 1.00f);
            case ConstructionSector.Bravo: return new Color(0.20f, 0.75f, 0.35f);
            case ConstructionSector.Charlie: return new Color(1.00f, 0.55f, 0.10f);
            case ConstructionSector.Delta: return new Color(0.85f, 0.20f, 0.20f);
            default: return Color.white;
        }
    }

    private static Vector3 CellToWorld(Vector3Int cell)
    {
        ConstructionManager[] constructions =
            Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager c = constructions[i];
            if (c == null)
                continue;
            Vector3Int cc = c.CurrentCellPosition;
            cc.z = 0;
            if (cc == cell)
                return c.transform.position;
        }
        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }

    private static Vector3 ComputeSectorCentroidWorld(ConstructionSector sector)
    {
        ConstructionManager[] constructions =
            Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager c = constructions[i];
            if (c == null || c.Sector != sector)
                continue;
            sum += c.transform.position;
            count++;
        }
        return count > 0 ? (sum / count) : Vector3.zero;
    }

    private static string FormatCell(Vector3Int cell) => $"({cell.x},{cell.y})";

    private void CreateDefaultAssets()
    {
        const string folder = "Assets/Data/AI/Plans";
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/AI");
        EnsureFolder(folder);

        AIPlanData defense = CreatePlanAsset(
            folder,
            "AIPlan_Defense.asset",
            "defense",
            "PLAN: DEFESA",
            AIPlanKind.Fixed,
            ConstructionSector.BaseTeam,
            "Nao ha unidades inimigas visiveis a 5 hex do HQ.",
            "HQ capturado.");

        if (defense.selectionCriteria.Count == 0)
        {
            defense.selectionCriteria.Add("chave-fechadura");
            defense.selectionCriteria.Add("unidades proximas disponiveis");
            defense.selectionCriteria.Add("realocacao de unidades rapidas de outros planos");
            defense.selectionCriteria.Add("unidades compradas");
            EditorUtility.SetDirty(defense);
        }

        AIPlanData attack = CreatePlanAsset(
            folder,
            "AIPlan_Attack.asset",
            "attack",
            "PLAN: ATAQUE",
            AIPlanKind.Fixed,
            ConstructionSector.BaseTeam,
            "HQ inimigo capturado.",
            "Infantaria aliada destruida.");

        if (attack.selectionCriteria.Count == 0)
        {
            attack.selectionCriteria.Add("unidades compradas");
            attack.selectionCriteria.Add("unidades de outros planos que podem ser realocadas");
            EditorUtility.SetDirty(attack);
        }

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<AIPlanDatabase>();
            AssetDatabase.CreateAsset(database, folder + "/AIPlanDatabase_Default.asset");
        }

        database.defensePlan = defense;
        database.attackPlan = attack;
        database.EnsureDefaults();
        EditorUtility.SetDirty(database);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Repaint();
    }

    private static AIPlanData CreatePlanAsset(
        string folder,
        string fileName,
        string planId,
        string displayName,
        AIPlanKind kind,
        ConstructionSector targetSector,
        string completedWhen,
        string failedWhen)
    {
        string path = folder + "/" + fileName;
        AIPlanData plan = AssetDatabase.LoadAssetAtPath<AIPlanData>(path);
        if (plan == null)
        {
            plan = ScriptableObject.CreateInstance<AIPlanData>();
            AssetDatabase.CreateAsset(plan, path);
        }

        plan.planId = planId;
        plan.displayName = displayName;
        plan.kind = kind;
        plan.targetSector = targetSector;
        plan.objectiveCompletedWhen = completedWhen;
        plan.objectiveFailedWhen = failedWhen;

        EditorUtility.SetDirty(plan);
        return plan;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int slash = path.LastIndexOf('/');
        if (slash <= 0)
            return;

        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }
}
