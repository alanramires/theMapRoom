using UnityEditor;
using UnityEngine;

public class AIPlannerWindow : EditorWindow
{
    [SerializeField] private AIPlanDatabase database;
    [SerializeField] private Vector2 scroll;

    [MenuItem("Tools/AI/AI Planner")]
    public static void OpenWindow()
    {
        GetWindow<AIPlannerWindow>("AI Planner");
    }

    private void OnEnable()
    {
        AutoDetect();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("AI Planner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Painel de preparacao de planos da IA. Nesta fase ele fornece a base de dados (2 fixos + ate N variaveis) e nao executa o planner runtime ainda.",
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
        EditorGUILayout.LabelField("Variable Plans", EditorStyles.boldLabel);
        SerializedObject so = new SerializedObject(database);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("maxVariablePlans"));
        EditorGUILayout.PropertyField(so.FindProperty("variablePlans"), includeChildren: true);
        so.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(database);

        EditorGUILayout.EndScrollView();
    }

    private void DrawDatabaseSummary()
    {
        string path = AssetDatabase.GetAssetPath(database);
        int fixedCount = (database.defensePlan != null ? 1 : 0) + (database.attackPlan != null ? 1 : 0);
        int variableCount = database.variablePlans != null ? database.variablePlans.Count : 0;

        EditorGUILayout.HelpBox(
            $"Asset: {path}\nFixed: {fixedCount}/2 | Variable: {variableCount}/{database.maxVariablePlans}",
            MessageType.None);
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
            "Unidades inimigas visiveis a 5 hex do HQ.",
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
            "Construcoes capturadas > 50% do total em campo.",
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
        string activation,
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
        plan.activationCondition = activation;
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