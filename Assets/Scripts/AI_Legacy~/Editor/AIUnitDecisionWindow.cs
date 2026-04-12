using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AIUnitDecisionWindow : EditorWindow
{
    // ── contexto ──────────────────────────────────────────────────────────────
    [SerializeField] private MatchController matchController;
    [SerializeField] private TeamId selectedTeam = TeamId.Green;

    // ── resultado ─────────────────────────────────────────────────────────────
    private sealed class UnitEntry
    {
        public UnitManager unit;
        public AIInitiative initiative;
        public string initiativeLabel;
        public string planLabel;
        public AIPlanRole planRole;
        public string captureTarget;
        public bool inRepairMode;
        public string likelyAction;
        public List<AIUnitSensorKind> sensorPriority = new List<AIUnitSensorKind>();
        public AIUnitProfile profile;
        public AIPlanAssignment assignment;
        public AIPlanIntent intent;
        // movimento previsto
        public int moveRange;
        public bool hasObjectiveCell;
        public Vector3Int objectiveCell;
        public int hexDistToObjective;  // int.MaxValue = sem objetivo
        // sensor que intercepta o plano
        public AIUnitSensorKind? interceptSensor;
        public string interceptReason;
    }

    private readonly List<UnitEntry> entries = new List<UnitEntry>();
    private int selectedIndex = -1;
    private string statusMessage = "Pronto.";
    private AISnapshot lastSnapshot;

    // ── scene overlay ─────────────────────────────────────────────────────────
    private bool hasSelectedLine;
    private Vector3 selectedLineStart;
    private Vector3 selectedLineEnd;
    private bool hasMoveLine;
    private Vector3 moveLineStart;
    private Vector3 moveLineEnd;

    // ── scroll ────────────────────────────────────────────────────────────────
    private Vector2 windowScroll;
    private Vector2 leftScroll;
    private Vector2 rightScroll;

    // ── estilos (lazy) ────────────────────────────────────────────────────────
    private GUIStyle _cardStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _dimStyle;
    private GUIStyle _wrapStyle;

    private GUIStyle CardStyle    => _cardStyle    ??= new GUIStyle("box")                    { padding = new RectOffset(6, 6, 4, 4) };
    private GUIStyle SectionStyle => _sectionStyle ??= new GUIStyle(EditorStyles.boldLabel)   { fontSize = 11 };
    private GUIStyle DimStyle     => _dimStyle     ??= new GUIStyle(EditorStyles.miniLabel)   { normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } };
    private GUIStyle WrapStyle    => _wrapStyle    ??= new GUIStyle(EditorStyles.miniLabel)   { wordWrap = true, normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } };

    [MenuItem("Tools/AI/Simuladores/AI Unit Decision")]
    public static void OpenWindow()
    {
        GetWindow<AIUnitDecisionWindow>("AI Unit Decision");
    }

    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        hasSelectedLine = false;
        hasMoveLine = false;
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
        DrawHeader();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        if (entries.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();

            // ── esquerda: lista de unidades ───────────────────────────────────
            EditorGUILayout.BeginVertical(GUILayout.Width(260f));
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
            DrawUnitList();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // ── direita: detalhe da unidade selecionada ───────────────────────
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
            DrawUnitDetail();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6f);
        {
            UnitEntry sel = selectedIndex >= 0 && selectedIndex < entries.Count ? entries[selectedIndex] : null;
            bool canMove = sel != null && sel.hasObjectiveCell && sel.hexDistToObjective > 0 && !sel.unit.HasActed;

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!canMove))
            {
                if (GUILayout.Button("Executar Movimento", GUILayout.Height(26f)))
                    ExecuteMovement(sel);
            }
            using (new EditorGUI.DisabledScope(true))
                GUILayout.Button("Executar Decisão", GUILayout.Height(26f));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HEADER
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("AI Unit Decision", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Simula a ordem de iniciativa e o que a IA planeja fazer com cada unidade. " +
            "Clique em uma unidade para ver o detalhe da decisão.",
            MessageType.Info);

        matchController = (MatchController)EditorGUILayout.ObjectField(
            "Match Controller", matchController, typeof(MatchController), true);

        selectedTeam = (TeamId)EditorGUILayout.EnumPopup("Time", selectedTeam);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular", GUILayout.Height(24f)))
            RunSimulation();
        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LISTA
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawUnitList()
    {
        EditorGUILayout.LabelField($"Unidades ({entries.Count})", SectionStyle);

        for (int i = 0; i < entries.Count; i++)
        {
            UnitEntry e = entries[i];
            if (e == null) continue;

            bool isSelected = selectedIndex == i;
            Color bg = GetInitiativeColor(e.initiative, e.inRepairMode);
            GUI.color = bg;
            EditorGUILayout.BeginVertical(CardStyle);
            GUI.color = Color.white;

            string label = $"{i + 1}. {e.unit.name}";
            bool toggled = GUILayout.Toggle(isSelected, label, EditorStyles.miniButton);
            if (toggled && selectedIndex != i) selectedIndex = i;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"HP {e.unit.CurrentHP}/{e.unit.GetMaxHP()}", DimStyle, GUILayout.Width(70f));
            EditorGUILayout.LabelField(e.initiativeLabel, DimStyle);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(e.planLabel))
                GUILayout.Label(e.planLabel, WrapStyle);

            if (e.interceptSensor.HasValue)
            {
                GUI.color = GetSensorInterceptColor(e.interceptSensor.Value);
                EditorGUILayout.LabelField($"⚡ {e.interceptSensor.Value}", DimStyle);
                GUI.color = Color.white;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DETALHE
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawUnitDetail()
    {
        if (selectedIndex < 0 || selectedIndex >= entries.Count)
        {
            EditorGUILayout.HelpBox("Selecione uma unidade na lista.", MessageType.Info);
            return;
        }

        UnitEntry e = entries[selectedIndex];
        UnitManager u = e.unit;

        EditorGUILayout.LabelField(u.name, SectionStyle);
        EditorGUILayout.Space(2f);

        // ── estado ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Estado", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        DrawRow("HP",       $"{u.CurrentHP} / {u.GetMaxHP()}");
        int maxFuel = u.GetMaxFuel();
        if (maxFuel > 0)
            DrawRow("Combustível", $"{u.CurrentFuel} / {maxFuel}");
        DrawRow("Posição",  FormatCell(u.CurrentCellPosition));
        DrawRow("Agiu",     u.HasActed ? "sim" : "não");
        DrawRow("Embarcada",u.IsEmbarked ? "sim" : "não");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4f);

        // ── iniciativa ────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Iniciativa", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        DrawRow("Valor", e.initiativeLabel);
        if (e.inRepairMode)
        {
            GUI.color = new Color(1f, 0.85f, 0.4f);
            EditorGUILayout.LabelField("Modo Reparo ATIVO → initiative forçada para Retreat", WrapStyle);
            GUI.color = Color.white;
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4f);

        // ── objetivo da unidade ───────────────────────────────────────────────
        EditorGUILayout.LabelField("Objetivo da Unidade", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        GUILayout.Label(e.likelyAction, WrapStyle);
        EditorGUILayout.Space(2f);
        string lineBtn = hasSelectedLine ? "Ocultar Linha" : "Desenhar Linha";
        if (GUILayout.Button(lineBtn))
        {
            if (hasSelectedLine)
            {
                hasSelectedLine = false;
                SceneView.RepaintAll();
            }
            else
            {
                DrawLineForEntry(e);
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4f);

        // ── sensor que intercepta ─────────────────────────────────────────────
        EditorGUILayout.LabelField("Sensor que Intercepta", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        if (e.interceptSensor.HasValue)
        {
            GUI.color = GetSensorInterceptColor(e.interceptSensor.Value);
            EditorGUILayout.LabelField($"{e.interceptSensor.Value}", EditorStyles.boldLabel);
            GUI.color = Color.white;
            GUILayout.Label(e.interceptReason, WrapStyle);
            if (e.interceptSensor.Value != AIUnitSensorKind.Reposition)
            {
                GUI.color = new Color(1f, 0.85f, 0.4f);
                GUILayout.Label("⚠ Este sensor dispara antes do plano — o movimento previsto acima pode não acontecer.", WrapStyle);
                GUI.color = Color.white;
            }
        }
        else
        {
            EditorGUILayout.LabelField("Nenhum sensor avaliado (sem perfil).", DimStyle);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4f);

        // ── movimento previsto ────────────────────────────────────────────────
        EditorGUILayout.LabelField("Movimento Previsto", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        DrawRow("Alcance", $"{e.moveRange} células");
        if (!e.hasObjectiveCell)
        {
            EditorGUILayout.LabelField("Sem alvo definido (sem plano/captura).", DimStyle);
        }
        else if (e.hexDistToObjective == 0)
        {
            EditorGUILayout.LabelField("Já está no objetivo.", DimStyle);
        }
        else
        {
            DrawRow("Distância ao objetivo", $"{e.hexDistToObjective} células");
            if (e.hexDistToObjective <= e.moveRange)
            {
                GUI.color = new Color(0.6f, 1f, 0.6f);
                EditorGUILayout.LabelField("Alcança o objetivo nesta rodada.", WrapStyle);
                GUI.color = Color.white;
            }
            else
            {
                EditorGUILayout.LabelField($"Avança {e.moveRange} de {e.hexDistToObjective} células (aprox.).", WrapStyle);
            }
            EditorGUILayout.Space(2f);
            string moveBtn = hasMoveLine ? "Ocultar Movimento" : "Desenhar Movimento";
            if (GUILayout.Button(moveBtn))
            {
                if (hasMoveLine)
                {
                    hasMoveLine = false;
                    SceneView.RepaintAll();
                }
                else
                {
                    DrawMoveLineForEntry(e);
                }
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4f);

        // ── ação prevista após movimento ──────────────────────────────────────
        EditorGUILayout.LabelField("Ação Prevista Após Movimento", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        GUILayout.Label(DerivePostMoveAction(e), WrapStyle);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4f);

        // ── plano ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Plano", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        if (e.intent != null)
        {
            DrawRow("Plano",   !string.IsNullOrWhiteSpace(e.intent.DisplayName) ? e.intent.DisplayName : e.intent.Sector.ToString());
            DrawRow("Setor",   e.intent.Sector.ToString());
            DrawRow("Papel",   e.planRole.ToDebugLabel());
            if (!string.IsNullOrWhiteSpace(e.captureTarget))
                DrawRow("Alvo Captura", e.captureTarget);
            DrawRow("Risco",   e.intent.TacticalRiskScore.ToString());
        }
        else
        {
            EditorGUILayout.LabelField("Sem plano atribuído neste turno.", DimStyle);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4f);

        // ── sensores (prioridade) ─────────────────────────────────────────────
        EditorGUILayout.LabelField("Prioridade de Sensores", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        if (e.sensorPriority.Count == 0)
        {
            EditorGUILayout.LabelField("Nenhum sensor configurado.", DimStyle);
        }
        else
        {
            for (int s = 0; s < e.sensorPriority.Count; s++)
                EditorGUILayout.LabelField($"{s + 1}. {e.sensorPriority[s]}", DimStyle);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4f);

        // ── perfil ────────────────────────────────────────────────────────────
        if (e.profile != null)
        {
            EditorGUILayout.LabelField("Perfil (AIUnitProfile)", SectionStyle);
            EditorGUILayout.BeginVertical(CardStyle);
            DrawRow("Threshold Reparo HP", $"≤ {e.profile.hpRepairThreshold}");
            DrawRow("Saída Reparo HP",     $"≥ {e.profile.hpRepairExitThreshold}");
            DrawRow("Threshold Autonomia", $"≤ {e.profile.repairAutonomyThresholdPercent}%");
            if (e.profile.fuseWhileOnRepairMode)
                DrawRow("Fundir em reparo", "sim");
            if (e.profile.returnToPickupAfterDisembark)
                DrawRow("Voltar pickup pós desembarque", "sim");
            DrawRow("Asset", e.profile.name);
            EditorGUILayout.EndVertical();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SIMULAÇÃO
    // ─────────────────────────────────────────────────────────────────────────

    private void RunSimulation()
    {
        entries.Clear();
        selectedIndex = -1;

        SyncEditorRegistries();

        if (matchController == null)
        {
            statusMessage = "Erro: MatchController não encontrado.";
            return;
        }

        // snapshot + planner
        AISnapshot snapshot = AISnapshot.Build(selectedTeam, matchController);
        lastSnapshot = snapshot;
        List<AIPlanIntent> plans = AIPlanEvaluator.Evaluate(snapshot);
        snapshot.ActivePlans = plans ?? new List<AIPlanIntent>();

        var assignments = new Dictionary<int, AIPlanAssignment>();
        var roles       = new Dictionary<int, AIPlanIntent>();
        if (plans != null)
        {
            foreach (AIPlanIntent intent in plans)
                foreach (AIPlanAssignment a in intent.Assignments)
                {
                    assignments[a.UnitInstanceId] = a;
                    roles[a.UnitInstanceId]        = intent;
                }
        }

        // coleta unidades do time
        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager u = UnitManager.AllActive[i];
            if (u == null || !u.gameObject.activeInHierarchy || u.IsEmbarked || u.IsDead)
                continue;
            if (u.TeamId != selectedTeam)
                continue;

            u.TryGetUnitData(out UnitData data);
            AIUnitProfile profile = data?.aiUnitProfile;

            bool inRepair = profile != null
                && profile.hpRepairThreshold > 0
                && u.CurrentHP <= profile.hpRepairThreshold;

            AIInitiative initiative = inRepair
                ? AIInitiative.Retreat
                : (profile != null ? profile.initiative : AIInitiative.Medium);

            assignments.TryGetValue(u.InstanceId, out AIPlanAssignment assign);
            roles.TryGetValue(u.InstanceId, out AIPlanIntent intent);

            AIPlanRole role = assign != null ? assign.Role : AIPlanRole.Assault;

            string planLabel = intent != null
                ? $"{(string.IsNullOrWhiteSpace(intent.DisplayName) ? intent.Sector.ToString() : intent.DisplayName)} [{role.ToDebugLabel()}]"
                : string.Empty;

            string captureTarget = string.Empty;
            if (assign != null && assign.HasPlannedCaptureTarget)
                captureTarget = FormatCell(assign.PlannedCaptureCell);
            else if (intent != null && intent.HasCaptureTarget)
                captureTarget = FormatCell(intent.CaptureTargetCell);

            // sensores da stance padrão (primeiro elemento do profile)
            List<AIUnitSensorKind> sensors = new List<AIUnitSensorKind>();
            if (profile != null && profile.stanceBehaviors != null && profile.stanceBehaviors.Count > 0)
                sensors.AddRange(profile.stanceBehaviors[0].sensorPriority ?? new List<AIUnitSensorKind>());

            string likelyAction = DeriveLikelyAction(u, data, profile, inRepair, role, intent, captureTarget);

            // sensor intercept (avaliação sem executar)
            (AIUnitSensorKind? interceptSensor, string interceptReason) = EvaluateSensorIntercept(
                u, data, assign, sensors, snapshot);

            // movimento previsto
            int moveRange = u.MaxMovementPoints;
            bool hasObjCell = false;
            Vector3Int objCell = Vector3Int.zero;
            int hexDist = int.MaxValue;

            if (assign != null && assign.HasPlannedCaptureTarget)
            {
                objCell = assign.PlannedCaptureCell; objCell.z = 0;
                hasObjCell = true;
            }
            else if (intent != null && intent.HasCaptureTarget)
            {
                objCell = intent.CaptureTargetCell; objCell.z = 0;
                hasObjCell = true;
            }

            if (hasObjCell)
            {
                Vector3Int uCell = u.CurrentCellPosition; uCell.z = 0;
                hexDist = Mathf.Abs(uCell.x - objCell.x) + Mathf.Abs(uCell.y - objCell.y);
            }

            entries.Add(new UnitEntry
            {
                unit               = u,
                initiative         = initiative,
                initiativeLabel    = initiative.ToString(),
                planLabel          = planLabel,
                planRole           = role,
                captureTarget      = captureTarget,
                inRepairMode       = inRepair,
                likelyAction       = likelyAction,
                sensorPriority     = sensors,
                profile            = profile,
                assignment         = assign,
                intent             = intent,
                moveRange          = moveRange,
                hasObjectiveCell   = hasObjCell,
                objectiveCell      = objCell,
                hexDistToObjective = hexDist,
                interceptSensor    = interceptSensor,
                interceptReason    = interceptReason,
            });
        }

        // ordena: initiative asc, HP desc
        entries.Sort((a, b) =>
        {
            int cmp = ((int)a.initiative).CompareTo((int)b.initiative);
            if (cmp != 0) return cmp;
            return b.unit.CurrentHP.CompareTo(a.unit.CurrentHP);
        });

        statusMessage = $"Time {selectedTeam} | {entries.Count} unidade(s) | {plans?.Count ?? 0} plano(s) ativo(s)";
        Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DERIVAR AÇÃO PROVÁVEL
    // ─────────────────────────────────────────────────────────────────────────

    private static string DeriveLikelyAction(
        UnitManager u,
        UnitData data,
        AIUnitProfile profile,
        bool inRepair,
        AIPlanRole role,
        AIPlanIntent intent,
        string captureTarget)
    {
        if (inRepair)
            return "Voltar para base / Reparar  (HP abaixo do threshold do perfil)";

        bool isSupplier    = data != null && data.isSupplier;
        bool isTransporter = data != null && data.isTransporter;

        if (isSupplier)
        {
            // verifica autonomia baixa (simplificado)
            int maxFuel = u.GetMaxFuel();
            int threshold = profile != null ? profile.repairAutonomyThresholdPercent : 25;
            bool lowAuto = maxFuel > 0 && threshold > 0 && u.CurrentFuel * 100 <= maxFuel * threshold;
            if (lowAuto)
                return "Reabastecer autonomia própria antes de agir  (combustível abaixo do threshold)";
            return "Suprir unidades aliadas  (role: Support / Logistics)";
        }

        if (isTransporter)
            return "Transportar passageiro / Buscar unidade para embarque";

        switch (role)
        {
            case AIPlanRole.Capture:
                return string.IsNullOrWhiteSpace(captureTarget)
                    ? "Avançar para capturar objetivo do setor"
                    : $"Avançar para capturar {captureTarget}";

            case AIPlanRole.Escort:
                string escortPlan = intent != null ? (intent.DisplayName ?? intent.Sector.ToString()) : "plano";
                return $"Escolta — proteger capturadores do plano [{escortPlan}]";

            case AIPlanRole.Artillery:
                return "Reposicionar para alcance de tiro / Apoio de fogo indireto";

            case AIPlanRole.Support:
                return "Suprir / Reparar unidades aliadas no plano";

            default:
                return u.HasActed
                    ? "Já agiu neste turno"
                    : "Reposicionar / Combate (sem papel de plano atribuído)";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private static Color GetInitiativeColor(AIInitiative init, bool inRepair)
    {
        if (inRepair) return new Color(0.9f, 0.7f, 0.7f);
        return init switch
        {
            AIInitiative.Priority => new Color(0.7f, 0.9f, 1f),
            AIInitiative.High     => new Color(0.75f, 1f, 0.75f),
            AIInitiative.Medium   => Color.white,
            AIInitiative.Low      => new Color(1f, 1f, 0.75f),
            AIInitiative.Retreat  => new Color(0.9f, 0.7f, 0.7f),
            _                     => Color.white
        };
    }

    private static string FormatCell(Vector3Int c) => $"({c.x},{c.y})";

    private void DrawRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, DimStyle,  GUILayout.Width(150f));
        EditorGUILayout.LabelField(value, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private static void SyncEditorRegistries()
    {
        if (Application.isPlaying) return;

        UnitManager.AllActive.Clear();
        foreach (var u in Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (u != null && u.gameObject.activeInHierarchy)
                UnitManager.AllActive.Add(u);

        ConstructionManager.AllActive.Clear();
        foreach (var c in Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c != null && c.gameObject.activeInHierarchy)
                ConstructionManager.AllActive.Add(c);
    }

    private void ExecuteMovement(UnitEntry e)
    {
        if (e?.unit == null || !e.hasObjectiveCell) return;

        var board = e.unit.BoardTilemap;
        if (board == null)
        {
            statusMessage = "Erro: unidade sem BoardTilemap.";
            return;
        }

        Vector3Int unitCell = e.unit.CurrentCellPosition; unitCell.z = 0;
        int steps = Mathf.Min(e.moveRange, e.hexDistToObjective);

        // célula de destino: lerp no espaço do grid
        Vector3Int targetCell;
        if (steps >= e.hexDistToObjective)
        {
            targetCell = e.objectiveCell;
        }
        else
        {
            float t = steps / (float)e.hexDistToObjective;
            targetCell = new Vector3Int(
                Mathf.RoundToInt(Mathf.Lerp(unitCell.x, e.objectiveCell.x, t)),
                Mathf.RoundToInt(Mathf.Lerp(unitCell.y, e.objectiveCell.y, t)),
                0);
        }

        Undo.RecordObject(e.unit, "AI Executar Movimento");
        e.unit.SetCurrentCellPosition(targetCell, enforceFinalOccupancyRule: false);
        e.unit.SetRemainingMovementPoints(Mathf.Max(0, e.moveRange - steps));
        e.unit.SetCurrentFuel(Mathf.Max(0, e.unit.CurrentFuel - steps));
        EditorUtility.SetDirty(e.unit);

        // limpa overlay de movimento
        hasMoveLine = false;
        SceneView.RepaintAll();

        statusMessage = $"{e.unit.name} movido para ({targetCell.x},{targetCell.y}). Combustível: {e.unit.CurrentFuel}. Re-simule para atualizar.";
        Repaint();
    }

    private static (AIUnitSensorKind? sensor, string reason) EvaluateSensorIntercept(
        UnitManager unit,
        UnitData data,
        AIPlanAssignment assign,
        List<AIUnitSensorKind> priority,
        AISnapshot snapshot)
    {
        if (priority == null || priority.Count == 0)
            return (null, "Sem sensores configurados no perfil.");

        bool isTransporter = data != null && data.isTransporter;
        bool isSupplier    = data != null && data.isSupplier;

        // captureRoleFilter: unidade capturadora com plano+alvo definido — não faz fallback para Attack
        bool captureRoleFilter = assign != null
            && assign.Role == AIPlanRole.Capture
            && assign.Intent != null
            && assign.Intent.HasCaptureTarget;

        // alcance máximo de tiro
        int maxFireRange = 1;
        if (data?.embarkedWeapons != null)
            foreach (var w in data.embarkedWeapons)
                if (w != null) maxFireRange = Mathf.Max(maxFireRange, w.GetRangeMax());

        Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
        int moveRange = unit.MaxMovementPoints;

        foreach (var sensor in priority)
        {
            switch (sensor)
            {
                case AIUnitSensorKind.Transport:
                    if (isTransporter)
                    {
                        // verificação simplificada: se há passageiro embarcado ou aliados rogues sem plano próximos
                        bool hasPassenger = false;
                        foreach (var ally in snapshot.FriendlyUnits)
                        {
                            if (ally == null || ally == unit) continue;
                            if (ally.IsEmbarked) { hasPassenger = true; break; }
                        }
                        return (AIUnitSensorKind.Transport,
                            hasPassenger
                                ? "Transportador com passageiro embarcado — vai entregar no objetivo."
                                : "Transportador — verifica rendezvous ou passageiro rogue no mapa.");
                    }
                    else
                    {
                        // unidade não-transportadora: há APC disponível com assento?
                        bool apcAvailable = false;
                        foreach (var ally in snapshot.FriendlyUnits)
                        {
                            if (ally == null || ally == unit) continue;
                            ally.TryGetUnitData(out UnitData apcData);
                            if (apcData == null || !apcData.isTransporter) continue;
                            Vector3Int apcCell = ally.CurrentCellPosition; apcCell.z = 0;
                            int dist = Mathf.Abs(unitCell.x - apcCell.x) + Mathf.Abs(unitCell.y - apcCell.y);
                            if (dist <= moveRange * 2) { apcAvailable = true; break; }
                        }
                        if (apcAvailable)
                            return (AIUnitSensorKind.Transport, "APC aliado disponível próximo — pode solicitar carona.");
                        // sensor não disparou, próximo
                    }
                    break;

                case AIUnitSensorKind.Capture:
                    // Verificação simplificada: há construção capturável na posição atual ou no alcance?
                    foreach (var c in ConstructionManager.AllActive)
                    {
                        if (c == null) continue;
                        if (c.TeamId == unit.TeamId) continue; // aliada
                        Vector3Int cCell = c.CurrentCellPosition; cCell.z = 0;
                        int dist = Mathf.Abs(unitCell.x - cCell.x) + Mathf.Abs(unitCell.y - cCell.y);
                        if (dist == 0)
                            return (AIUnitSensorKind.Capture, $"Unidade está sobre {c.ConstructionDisplayName} — captura imediata.");
                        if (dist <= moveRange)
                            return (AIUnitSensorKind.Capture, $"{c.ConstructionDisplayName} alcançável neste turno ({dist} células).");
                    }
                    break;

                case AIUnitSensorKind.Attack:
                    if (captureRoleFilter)
                        break; // capturador não faz fallback para ataque

                    UnitManager closestEnemy = null;
                    int closestDist = int.MaxValue;
                    foreach (var enemy in snapshot.VisibleEnemies)
                    {
                        if (enemy == null || enemy.IsDead) continue;
                        Vector3Int eCell = enemy.CurrentCellPosition; eCell.z = 0;
                        int dist = Mathf.Abs(unitCell.x - eCell.x) + Mathf.Abs(unitCell.y - eCell.y);
                        if (dist < closestDist) { closestDist = dist; closestEnemy = enemy; }
                    }
                    // Phase2 dispara Attack para QUALQUER inimigo visível — sem checar distância.
                    // A IA move em direção ao alvo mesmo sem alcançar neste turno.
                    if (closestEnemy != null)
                        return (AIUnitSensorKind.Attack,
                            $"Inimigo visível: {closestEnemy.name} ({closestDist} células). A IA vai se aproximar para atacar.");
                    break;

                case AIUnitSensorKind.Supply:
                    if (!isSupplier) break;
                    foreach (var ally in snapshot.FriendlyUnits)
                    {
                        if (ally == null || ally == unit) continue;
                        bool needsFuel  = ally.CurrentFuel  < ally.MaxFuel  * 0.5f;
                        bool needsAmmo  = ally.CurrentAmmo  < ally.GetMaxAmmo() * 0.5f;
                        if (!needsFuel && !needsAmmo) continue;
                        Vector3Int aCell = ally.CurrentCellPosition; aCell.z = 0;
                        int dist = Mathf.Abs(unitCell.x - aCell.x) + Mathf.Abs(unitCell.y - aCell.y);
                        if (dist <= moveRange + 1)
                            return (AIUnitSensorKind.Supply,
                                $"Aliado {ally.name} precisa de {(needsFuel ? "combustível" : "munição")} ({dist} células).");
                    }
                    break;

                case AIUnitSensorKind.Reposition:
                    return (AIUnitSensorKind.Reposition,
                        "Nenhum sensor anterior disparou — segue o plano e repositiona em direção ao objetivo.");
            }
        }

        return (AIUnitSensorKind.Reposition, "Fallback: segue o plano.");
    }

    private static Color GetSensorInterceptColor(AIUnitSensorKind sensor) => sensor switch
    {
        AIUnitSensorKind.Attack     => new Color(1f, 0.4f, 0.4f),
        AIUnitSensorKind.Capture    => new Color(0.4f, 1f, 0.6f),
        AIUnitSensorKind.Transport  => new Color(0.4f, 0.8f, 1f),
        AIUnitSensorKind.Supply     => new Color(1f, 0.85f, 0.3f),
        AIUnitSensorKind.Reposition => new Color(0.75f, 0.75f, 0.75f),
        _                           => Color.white
    };

    private static string DerivePostMoveAction(UnitEntry e)
    {
        if (e.inRepairMode)
            return "Aguardar reparo na base (não age ofensivamente).";

        bool reachesObjective = e.hasObjectiveCell
            && e.hexDistToObjective != int.MaxValue
            && e.hexDistToObjective <= e.moveRange;

        switch (e.planRole)
        {
            case AIPlanRole.Capture:
                if (reachesObjective)
                    return $"Iniciar captura de {e.captureTarget} (ou oportunista se passar por construção neutra/inimiga no caminho).";
                return "Avançar em direção ao alvo de captura. Se passar por construção neutra/inimiga, captura oportunista.";

            case AIPlanRole.Escort:
                return "Escolta — reposiciona adjacente à unidade de captura designada.";

            case AIPlanRole.Assault:
                return reachesObjective
                    ? "Combate: ataca inimigo mais próximo ao chegar na posição."
                    : "Reposicionar para alcançar frente de combate.";

            case AIPlanRole.Artillery:
                return "Reposicionar para alcance de tiro. Apoia por fogo indireto se houver alvo.";

            case AIPlanRole.Support:
                return "Suprir ou reparar unidade aliada próxima no plano.";

            default:
                return e.unit.HasActed
                    ? "Já agiu neste turno — sem ação adicional."
                    : "Sem papel definido: combate ou reposicionamento oportunista.";
        }
    }

    private void AutoDetectContext()
    {
        if (matchController == null)
            matchController = Object.FindAnyObjectByType<MatchController>();
    }

    private void DrawLineForEntry(UnitEntry e)
    {
        if (e?.unit == null) return;

        var board = e.unit.BoardTilemap;
        Vector3 from = e.unit.transform.position;
        from.z = 0f;

        Vector3 to;
        if (e.assignment != null && e.assignment.HasPlannedCaptureTarget)
            to = CellToWorld(e.assignment.PlannedCaptureCell, board);
        else if (e.intent != null && e.intent.HasCaptureTarget)
            to = CellToWorld(e.intent.CaptureTargetCell, board);
        else if (e.intent != null)
            to = ComputeSectorCentroidWorld(e.intent.Sector);
        else
            return;

        to.z = 0f;
        selectedLineStart = from;
        selectedLineEnd   = to;
        hasSelectedLine   = true;
        SceneView.RepaintAll();
    }

    private void DrawMoveLineForEntry(UnitEntry e)
    {
        if (e?.unit == null || !e.hasObjectiveCell || e.hexDistToObjective <= 0) return;

        var board = e.unit.BoardTilemap;
        Vector3Int unitCell = e.unit.CurrentCellPosition; unitCell.z = 0;
        int steps = Mathf.Min(e.moveRange, e.hexDistToObjective);

        Vector3Int targetCell;
        if (steps >= e.hexDistToObjective)
        {
            targetCell = e.objectiveCell;
        }
        else
        {
            float t = steps / (float)e.hexDistToObjective;
            targetCell = new Vector3Int(
                Mathf.RoundToInt(Mathf.Lerp(unitCell.x, e.objectiveCell.x, t)),
                Mathf.RoundToInt(Mathf.Lerp(unitCell.y, e.objectiveCell.y, t)),
                0);
        }

        Vector3 from = e.unit.transform.position; from.z = 0f;
        Vector3 to   = CellToWorld(targetCell, board); to.z = 0f;

        moveLineStart = from;
        moveLineEnd   = to;
        hasMoveLine   = true;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView _)
    {
        if (hasSelectedLine)
        {
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Handles.DrawAAPolyLine(4f, selectedLineStart, selectedLineEnd);
            Handles.DrawSolidDisc(selectedLineEnd, Vector3.forward, 0.25f);
            Handles.Label(
                Vector3.Lerp(selectedLineStart, selectedLineEnd, 0.5f) + Vector3.up * 0.3f,
                "objetivo",
                EditorStyles.miniLabel);
        }

        if (hasMoveLine)
        {
            Handles.color = new Color(1f, 0.75f, 0.2f, 0.9f);
            Handles.DrawAAPolyLine(4f, moveLineStart, moveLineEnd);
            Handles.DrawSolidDisc(moveLineEnd, Vector3.forward, 0.2f);
            Handles.Label(
                Vector3.Lerp(moveLineStart, moveLineEnd, 0.5f) + Vector3.down * 0.3f,
                "move",
                EditorStyles.miniLabel);
        }
    }

    private static Vector3 CellToWorld(Vector3Int cell, UnityEngine.Tilemaps.Tilemap board = null)
    {
        cell.z = 0;
        if (board != null)
            return board.GetCellCenterWorld(cell);

        foreach (var c in Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c == null) continue;
            Vector3Int cc = c.CurrentCellPosition; cc.z = 0;
            if (cc == cell) return c.transform.position;
        }
        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }

    private static Vector3 ComputeSectorCentroidWorld(ConstructionSector sector)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var c in Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c == null || c.Sector != sector) continue;
            sum += c.transform.position;
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }
}
