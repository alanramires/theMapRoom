using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AIPlannerWindow : EditorWindow
{
    [SerializeField] private AIPlanDatabase database;
    [SerializeField] private MatchController previewMatchController;
    [SerializeField] private TeamId previewTeam = TeamId.Blue;
    [SerializeField] private bool previewUseFogVisibility;
    [SerializeField] private bool previewShowSceneOverlay = true;
    [SerializeField] private int previewSelectedPlanIndex = -1;
    [SerializeField] private int previewSelectedAssignmentIndex = -1;
    [SerializeField] private Vector2 scroll;
    [SerializeField] private List<AIPlanIntent> previewPlans = new List<AIPlanIntent>();
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
            "Painel de preparacao de planos da IA. Defesa e ataque sao fixos; planos variaveis sao gerados em runtime.",
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
        EditorGUILayout.LabelField("Dynamic Variable Plans", EditorStyles.boldLabel);
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
            $"Asset: {path}\nFixed: {fixedCount}/2 | Dynamic Variable Budget: {database.maxVariablePlans} planos por turno",
            MessageType.None);
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("AI Player Automatic Planner (Preview)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Gera planos usando as unidades e construcoes da cena atual para prever o comportamento da IA no opener.",
            MessageType.None);

        previewMatchController = (MatchController)EditorGUILayout.ObjectField(
            "Match Controller (opcional)",
            previewMatchController,
            typeof(MatchController),
            true);
        previewTeam = (TeamId)EditorGUILayout.EnumPopup("Preview Team", previewTeam);
        previewUseFogVisibility = EditorGUILayout.ToggleLeft(
            "Usar visibilidade/FoW (quando houver MatchController em runtime)",
            previewUseFogVisibility);
        previewShowSceneOverlay = EditorGUILayout.ToggleLeft(
            "Mostrar overlay no Scene (circulos e participantes)",
            previewShowSceneOverlay);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect Context"))
            AutoDetectPreviewContext();
        if (GUILayout.Button("Generate Preview"))
            GeneratePreview();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(previewStatus, MessageType.None);

        if (previewPlans != null && previewPlans.Count > 0)
        {
            for (int i = 0; i < previewPlans.Count; i++)
            {
                AIPlanIntent intent = previewPlans[i];
                if (intent == null)
                    continue;

                EditorGUILayout.BeginVertical("box");
                string title = $"[{i}] {GetIntentTitle(intent)}";
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                bool isSelectedPlan = previewSelectedPlanIndex == i;
                if (GUILayout.Button(isSelectedPlan ? "Hide" : "Show", GUILayout.Width(64f)))
                {
                    previewSelectedPlanIndex = isSelectedPlan ? -1 : i;
                    previewSelectedAssignmentIndex = -1;
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Sector", intent.Sector.ToString());
                EditorGUILayout.LabelField("Assignments", intent.Assignments != null ? intent.Assignments.Count.ToString() : "0");
                if (intent.HasCaptureTarget)
                    EditorGUILayout.LabelField("Capture Target", FormatCell(intent.CaptureTargetCell));

                if (intent.Assignments != null)
                {
                    for (int a = 0; a < intent.Assignments.Count; a++)
                    {
                        AIPlanAssignment assignment = intent.Assignments[a];
                        if (assignment == null)
                            continue;

                        string unitName = FindUnitNameByInstanceId(assignment.UnitInstanceId);
                        string target = assignment.HasPlannedCaptureTarget
                            ? $" -> {assignment.PlannedCaptureLabel} {FormatCell(assignment.PlannedCaptureCell)}"
                            : string.Empty;
                        EditorGUILayout.BeginHorizontal();
                        bool isSelectedAssignment = isSelectedPlan && previewSelectedAssignmentIndex == a;
                        if (GUILayout.Button(isSelectedAssignment ? "On" : "Sel", GUILayout.Width(36f)))
                        {
                            previewSelectedPlanIndex = i;
                            previewSelectedAssignmentIndex = isSelectedAssignment ? -1 : a;
                            UnitManager unit = FindUnitByInstanceId(assignment.UnitInstanceId);
                            if (unit != null)
                            {
                                Selection.activeObject = unit.gameObject;
                                EditorGUIUtility.PingObject(unit.gameObject);
                            }
                            SceneView.RepaintAll();
                        }
                        EditorGUILayout.LabelField(
                            $"- {unitName} ({assignment.Role}){target}",
                            EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

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
        if (previewMatchController == null)
            previewMatchController = Object.FindAnyObjectByType<MatchController>();
    }

    private void GeneratePreview()
    {
        if (database == null)
        {
            previewStatus = "Selecione um AIPlanDatabase.";
            previewPlans.Clear();
            Repaint();
            return;
        }

        database.EnsureDefaults();
        AISnapshot snapshot = BuildEditorSnapshot(previewTeam, previewMatchController, previewUseFogVisibility);
        previewPlans = AIPlanEvaluator.Evaluate(database, snapshot);
        previewSelectedPlanIndex = -1;
        previewSelectedAssignmentIndex = -1;

        previewStatus =
            $"Team={TeamUtils.GetName(previewTeam)} | Friendly={snapshot.FriendlyUnits.Count} " +
            $"| Enemies={snapshot.VisibleEnemies.Count} | Constructions={snapshot.KnownConstructions.Count} " +
            $"| Plans={previewPlans.Count}";
        SceneView.RepaintAll();
        Repaint();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!previewShowSceneOverlay)
            return;
        if (previewPlans == null || previewPlans.Count == 0)
            return;
        if (previewSelectedPlanIndex < 0 || previewSelectedPlanIndex >= previewPlans.Count)
            return;

        AIPlanIntent intent = previewPlans[previewSelectedPlanIndex];
        if (intent == null)
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

            bool selected = previewSelectedAssignmentIndex == a;
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
