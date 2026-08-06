using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tools > AI > Plan Evaluator
///
/// Layout: painel esquerdo (objetivos + unidades) | painel direito (distMatrix + trace).
/// RUNTIME  → lê o plano vivo do ObjectiveManager.
/// EDITOR   → simula BuildObjectivePlan e captura o trace completo para exibição.
/// </summary>
public class AIPlanEvaluatorWindow : EditorWindow
{
    // ------------------------------------------------------------------
    // Campos arrastáveis
    // ------------------------------------------------------------------
    [SerializeField] private Tilemap         boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private UnitData        referenceUnitData;
    [SerializeField] private TeamId          selectedTeam      = TeamId.Neutral;
    [SerializeField] private float           riskDecisionImpact = 0.5f;
    [SerializeField] private int             defenseEnemyRange  = 2;

    // ------------------------------------------------------------------
    // Estado do plano
    // ------------------------------------------------------------------
    private TeamObjectivePlan     _plan;
    private bool                  _isEditorSimulation;
    private readonly List<ObjRow> _rows = new List<ObjRow>();
    private string                _status = "Pronto.";

    // Seleção
    private int _selectedObjIdx  = -1;   // índice em _rows
    private int _selectedUnitId  = -1;   // InstanceId da unidade selecionada

    // Cache vivo
    private readonly Dictionary<int, UnitManager> _liveById = new Dictionary<int, UnitManager>();

    // Trace da simulação (só preenchido em editor mode)
    private SimTrace _trace;

    // SceneView
    private readonly List<SceneLine> _lines = new List<SceneLine>();

    // Scroll
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;

    // ------------------------------------------------------------------
    // Estruturas de dados
    // ------------------------------------------------------------------
    private struct ObjRow
    {
        public SectorObjective          Obj;
        public ConstructionManager      Target;
        public SectorManager.SectorInfo SectorInfo;
        public bool                     HasSectorInfo;
    }

    private struct SceneLine
    {
        public Vector3 From, To;
        public Color   Color;
        public string  Label;
    }

    private class SimTrace
    {
        public float[,]  DistMatrix;   // [nu, nObj]  custo = dist × riskMult
        public float[]   RawDist;      // [nu * nObj] distância pura (sem multiplicador)
        public float[]   RiskMults;    // [nObj]
        public int[]     BestAssign;   // [nu] → índice em SolverObjs
        public float     BestCost;
        public readonly List<UnitManager>                            SolverUnits      = new List<UnitManager>();
        public readonly List<(SectorObjective obj, Vector3Int cell)> SolverObjs       = new List<(SectorObjective, Vector3Int)>();
        public readonly List<ConstructionSector>                     CascadeBlocked   = new List<ConstructionSector>();
        public readonly List<(UnitManager unit, ConstructionSector)> ImmediateCaptures= new List<(UnitManager, ConstructionSector)>();
        public bool IsInitialDistribution;
    }

    private const float LeftPanelWidth = 240f;

    // ------------------------------------------------------------------
    [MenuItem("Tools/AI/Plan Evaluator")]
    public static void OpenWindow() => GetWindow<AIPlanEvaluatorWindow>("AI Plan Evaluator");

    private void OnEnable()  { AutoDetectContext(); SceneView.duringSceneGui += OnSceneGUI; }
    private void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; _lines.Clear(); }
    private void OnInspectorUpdate() => Repaint();

    // ------------------------------------------------------------------
    // GUI principal — layout dois painéis
    // ------------------------------------------------------------------
    private void OnGUI()
    {
        DrawHeader();
        EditorGUILayout.Space(3f);
        EditorGUILayout.HelpBox(_status, MessageType.None);
        EditorGUILayout.Space(2f);

        if (_plan == null)
        {
            EditorGUILayout.HelpBox("Pressione Simular para gerar o plano.", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    // ------------------------------------------------------------------
    // Cabeçalho
    // ------------------------------------------------------------------
    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("AI Plan Evaluator", EditorStyles.boldLabel);

        boardTilemap      = (Tilemap)EditorGUILayout.ObjectField(
            "Tilemap",            boardTilemap,      typeof(Tilemap),         true);
        terrainDatabase   = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain DB",         terrainDatabase,   typeof(TerrainDatabase), false);
        referenceUnitData = (UnitData)EditorGUILayout.ObjectField(
            "Unit Data (Editor)", referenceUnitData, typeof(UnitData),        false);
        selectedTeam       = (TeamId)EditorGUILayout.EnumPopup("Time", selectedTeam);
        riskDecisionImpact = EditorGUILayout.Slider(
            new GUIContent("Impacto do risco na tomada de decisão", "0 = ignora risco  |  2 = risco pesa muito"),
            riskDecisionImpact, 0f, 2f);
        defenseEnemyRange = EditorGUILayout.IntSlider(
            new GUIContent("Raio de defesa (hexes)", "Distância máxima do inimigo para criar objetivo defensivo"),
            defenseEnemyRange, 1, 5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))   AutoDetectContext();
        if (GUILayout.Button("Simular"))       RunSimulate();
        if (GUILayout.Button("Limpar Linhas")) ClearLines();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------
    // Painel esquerdo — objetivos + unidades
    // ------------------------------------------------------------------
    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));
        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

        string modeTag = _isEditorSimulation ? "EDITOR" : "RUNTIME";
        EditorGUILayout.LabelField($"Objetivos ({_rows.Count})  [{modeTag}]", EditorStyles.boldLabel);

        for (int i = 0; i < _rows.Count; i++)
            DrawObjCard(i);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Unidades", EditorStyles.boldLabel);
        DrawUnitList();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawObjCard(int i)
    {
        ObjRow row = _rows[i];
        SectorObjective obj = row.Obj;

        EditorGUILayout.BeginVertical("box");

        bool selected  = _selectedObjIdx == i;
        string lbl     = $"Pri {obj.Priority}  {obj.Sector}  [{obj.Status}]";
        bool toggled   = GUILayout.Toggle(selected, lbl, "Button");
        if (toggled && _selectedObjIdx != i)
        {
            _selectedObjIdx = i;
            _selectedUnitId = -1;
            RebuildLines();
        }

        if (row.HasSectorInfo)
        {
            SectorManager.SectorRiskLevel risk = row.SectorInfo.GetRiskLevelFor(ResolveSelectedSlot());
            float hq  = row.SectorInfo.GetDistanceToHQ(ResolveSelectedSlot());
            float fct = row.SectorInfo.GetNearestFactoryDistance(ResolveSelectedSlot());
            EditorGUILayout.LabelField(
                $"Risco:{risk}  HQ:{(hq==float.MaxValue?"—":$"{hq:F1}h")}  Fábr:{(fct==float.MaxValue?"—":$"{fct:F1}h")}",
                EditorStyles.miniLabel);

            string n1 = row.SectorInfo.ClosestNeighbor1 == ConstructionSector.None ? "—"
                : $"{row.SectorInfo.ClosestNeighbor1} ({row.SectorInfo.ClosestNeighbor1Distance:F1}h)";
            string n2 = row.SectorInfo.ClosestNeighbor2 == ConstructionSector.None ? "—"
                : $"{row.SectorInfo.ClosestNeighbor2} ({row.SectorInfo.ClosestNeighbor2Distance:F1}h)";
            EditorGUILayout.LabelField($"N1:{n1}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"N2:{n2}", EditorStyles.miniLabel);
        }

        if (row.Target != null)
        {
            Vector3Int tc = row.Target.CurrentCellPosition; tc.z = 0;
            EditorGUILayout.LabelField($"Alvo: {row.Target.name} ({tc.x},{tc.y})", EditorStyles.miniLabel);
        }
        else
            EditorGUILayout.LabelField("Alvo: — sem capturável", EditorStyles.miniLabel);

        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled) { EditorGUILayout.LabelField($"  [{slot.Role}] vazio", EditorStyles.miniLabel); continue; }
            _liveById.TryGetValue(slot.AssignedUnitId, out UnitManager u);
            string uName  = u != null ? $"{u.UnitDisplayName} #{slot.AssignedUnitId}" : $"#{slot.AssignedUnitId} (?)";
            float  dist   = ComputeGameDist(u, row.Target);
            string dStr   = dist < 0 ? "?" : $"{dist:F0}h";
            EditorGUILayout.LabelField($"  [{slot.Role}] {uName}  {dStr}", EditorStyles.miniLabel);
        }

        if (obj.HandoffEligible)
            EditorGUILayout.LabelField($"  Handoff (vacater #{obj.PreferredHandoffFromUnitId})", EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
    }

    private void DrawUnitList()
    {
        bool any = false;
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null) continue;
            if (selectedTeam != TeamId.Neutral && u.TeamId != selectedTeam) continue;
            any = true;

            bool isCap = IsCapturador(u);
            Vector3Int pos = u.CurrentCellPosition; pos.z = 0;

            string tags = string.Empty;
            if (!isCap)          tags += "[não-cap]";
            if (u.IsEmbarked)    tags += "[emb]";
            if (u.IsDead)        tags += "[dead]";
            if (u.IsUnderRepair) tags += "[repair]";

            string assignment = FindAssignmentLabel(u);

            bool selUnit = _selectedUnitId == u.InstanceId;
            Color prev   = GUI.backgroundColor;
            if (selUnit) GUI.backgroundColor = new Color(0.4f, 0.8f, 1f, 0.6f);

            EditorGUILayout.BeginVertical("box");
            string btnLbl = $"#{u.InstanceId} {u.UnitDisplayName} {tags}";
            if (GUILayout.Button(btnLbl, EditorStyles.miniButton))
            {
                _selectedUnitId = u.InstanceId;
                _selectedObjIdx = -1;
                RebuildLines();
            }
            EditorGUILayout.LabelField($"  ({pos.x},{pos.y})  {assignment}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = prev;
        }

        if (!any)
            EditorGUILayout.HelpBox("Nenhuma unidade encontrada.", MessageType.Info);
    }

    // ------------------------------------------------------------------
    // Painel direito — distMatrix + trace
    // ------------------------------------------------------------------
    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

        if (_trace == null)
        {
            EditorGUILayout.HelpBox(
                "Trace disponível apenas em modo simulação (editor).\n" +
                "Em runtime o plano é lido direto do ObjectiveManager.",
                MessageType.Info);
            DrawRuntimeSummary();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        DrawDistMatrix();
        EditorGUILayout.Space(6f);
        DrawTraceNotes();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawDistMatrix()
    {
        SimTrace t = _trace;
        if (t.SolverUnits.Count == 0 || t.SolverObjs.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma atribuição pelo solver (sem capturadores ou objetivos).", MessageType.Info);
            return;
        }

        int nu   = t.SolverUnits.Count;
        int nObj = t.SolverObjs.Count;

        EditorGUILayout.LabelField("distMatrix  (dist × risco)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Verde = atribuição escolhida  |  Azul col = objetivo selecionado  |  Amarelo linha = unidade selecionada",
            EditorStyles.miniLabel);
        EditorGUILayout.Space(4f);

        // Encontra o índice do objetivo selecionado no solver
        int selOjIdx = -1;
        if (_selectedObjIdx >= 0 && _selectedObjIdx < _rows.Count)
        {
            SectorObjective selObj = _rows[_selectedObjIdx].Obj;
            for (int oj = 0; oj < nObj; oj++)
                if (t.SolverObjs[oj].obj == selObj) { selOjIdx = oj; break; }
        }

        // Encontra o índice da unidade selecionada no solver
        int selUiIdx = -1;
        if (_selectedUnitId >= 0)
            for (int ui = 0; ui < nu; ui++)
                if (t.SolverUnits[ui].InstanceId == _selectedUnitId) { selUiIdx = ui; break; }

        // Largura da coluna de nomes (esquerda) e de cada objetivo
        const float nameColW = 110f;
        const float cellW    = 72f;

        // Cabeçalho: nomes dos objetivos
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Unidade \\ Objetivo", EditorStyles.miniBoldLabel, GUILayout.Width(nameColW));
        for (int oj = 0; oj < nObj; oj++)
        {
            bool isSelCol = oj == selOjIdx;
            Color prev = GUI.color;
            if (isSelCol) GUI.color = new Color(0.5f, 0.8f, 1f);
            string objName = t.SolverObjs[oj].obj.Sector.ToString();
            GUILayout.Label($"{objName}\n×{t.RiskMults[oj]:F2}", EditorStyles.miniBoldLabel, GUILayout.Width(cellW));
            GUI.color = prev;
        }
        EditorGUILayout.EndHorizontal();

        DrawHLine();

        // Linhas: uma por unidade
        for (int ui = 0; ui < nu; ui++)
        {
            UnitManager u        = t.SolverUnits[ui];
            bool        isSelRow = ui == selUiIdx;

            // Destaca a linha da unidade selecionada
            Color prevBg = GUI.backgroundColor;
            if (isSelRow) GUI.backgroundColor = new Color(1f, 0.9f, 0.3f, 0.35f);

            EditorGUILayout.BeginHorizontal("box");
            string uLabel = $"#{u.InstanceId}\n{u.UnitDisplayName}";
            GUILayout.Label(uLabel, EditorStyles.miniLabel, GUILayout.Width(nameColW));

            for (int oj = 0; oj < nObj; oj++)
            {
                bool isChosen = t.BestAssign != null && ui < t.BestAssign.Length && t.BestAssign[ui] == oj;
                bool isSelCol = oj == selOjIdx;

                float cost    = t.DistMatrix[ui, oj];
                int   rawIdx  = ui * nObj + oj;
                float raw     = t.RawDist != null && rawIdx < t.RawDist.Length ? t.RawDist[rawIdx] : -1f;
                string rawStr = raw >= 0 ? $"\n({raw:F0}h raw)" : "";

                Color prev = GUI.backgroundColor;
                if (isChosen)      GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f, 0.55f);
                else if (isSelCol) GUI.backgroundColor = new Color(0.5f, 0.8f, 1f, 0.25f);

                string cellLabel = isChosen ? $"★{cost:F1}{rawStr}" : $"{cost:F1}{rawStr}";
                GUILayout.Label(cellLabel,
                    isChosen ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel,
                    GUILayout.Width(cellW));

                GUI.backgroundColor = prev;
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = prevBg;
        }

        // Linha de custo total
        if (t.BestAssign != null)
        {
            DrawHLine();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Custo total ótimo: {t.BestCost:F1}", EditorStyles.miniBoldLabel, GUILayout.Width(nameColW + cellW * nObj));
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawTraceNotes()
    {
        SimTrace t = _trace;

        // Distribuição inicial?
        EditorGUILayout.LabelField("Trace", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            t.IsInitialDistribution ? "Distribuição inicial (T1) — cascade ativo" : "Redistribuição — cascade desativado",
            EditorStyles.miniLabel);

        // Capturas imediatas
        if (t.ImmediateCaptures.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Capturas imediatas (5a)", EditorStyles.miniBoldLabel);
            foreach ((UnitManager u, ConstructionSector sec) in t.ImmediateCaptures)
                EditorGUILayout.LabelField($"  #{u.InstanceId} {u.UnitDisplayName}  →  {sec}", EditorStyles.miniLabel);
        }

        // Setores bloqueados pelo cascade
        if (t.CascadeBlocked.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Suprimidos pelo cascade (não entraram no solver)", EditorStyles.miniBoldLabel);
            foreach (ConstructionSector sec in t.CascadeBlocked)
                EditorGUILayout.LabelField($"  {sec}", EditorStyles.miniLabel);
        }

        // Rogues
        if (_plan != null && _plan.RogueUnitIds.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Rogues (capturadores sem atribuição)", EditorStyles.miniBoldLabel);
            foreach (int id in _plan.RogueUnitIds)
            {
                _liveById.TryGetValue(id, out UnitManager u);
                string name = u != null ? $"#{id} {u.UnitDisplayName}" : $"#{id}";
                EditorGUILayout.LabelField($"  {name}", EditorStyles.miniLabel);
            }
        }
    }

    private void DrawRuntimeSummary()
    {
        if (_plan == null) return;
        EditorGUILayout.LabelField("Atribuições (runtime)", EditorStyles.boldLabel);
        foreach (ObjRow row in _rows)
        {
            foreach (SlotNeed slot in row.Obj.Slots)
            {
                if (!slot.Filled) continue;
                _liveById.TryGetValue(slot.AssignedUnitId, out UnitManager u);
                ConstructionManager tgt = row.Target;
                float d    = ComputeGameDist(u, tgt);
                string dStr = d < 0 ? "?" : $"{d:F0}h";
                string uName = u != null ? $"#{slot.AssignedUnitId} {u.UnitDisplayName}" : $"#{slot.AssignedUnitId}";
                EditorGUILayout.LabelField($"  {row.Obj.Sector} ← {uName}  ({dStr})", EditorStyles.miniLabel);
            }
        }
    }

    private static void DrawHLine()
    {
        Rect r = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.4f));
    }

    // ------------------------------------------------------------------
    // Simular
    // ------------------------------------------------------------------
    private void RunSimulate()
    {
        if (selectedTeam == TeamId.Neutral) { _status = "Selecione um time válido."; return; }

        SyncSceneRegistries();
        SyncLiveUnitCache();

        _plan           = null;
        _trace          = null;
        _rows.Clear();
        _selectedObjIdx = -1;
        _selectedUnitId = -1;
        ClearLines();

        if (Application.isPlaying)
        {
            _plan = ObjectiveManager.GetPlanForSlot(ResolveSelectedSlot());
            _isEditorSimulation = false;
            if (_plan == null) { _status = $"Nenhum plano encontrado para {selectedTeam}."; return; }
        }
        else
        {
            _trace = new SimTrace();
            _plan  = SimulatePlan(_trace);
            _isEditorSimulation = true;
            if (_plan == null) { _status = "Falha na simulação."; return; }
        }

        foreach (SectorObjective obj in _plan.Objectives)
        {
            ObjRow row = new ObjRow { Obj = obj };
            row.HasSectorInfo = SectorManager.TryGetSectorInfo(obj.Sector, out row.SectorInfo);
            row.Target        = FindCapturableInSector(obj.Sector, selectedTeam);
            _rows.Add(row);
        }

        int assigned = 0;
        foreach (ObjRow r in _rows)
            foreach (SlotNeed s in r.Obj.Slots)
                if (s.Filled) assigned++;

        string mode = _isEditorSimulation ? "simulação editor" : "runtime";
        _status = $"[{mode}] {selectedTeam}: {_rows.Count} objetivos, {assigned} slots, {_plan.RogueUnitIds.Count} rogues.";

        RebuildLines();
        SceneView.RepaintAll();
    }

    // ------------------------------------------------------------------
    // Simulação — idêntica ao BuildObjectivePlan, captura trace
    // ------------------------------------------------------------------
    private TeamObjectivePlan SimulatePlan(SimTrace trace)
    {
        var plan = new TeamObjectivePlan { Team = selectedTeam };

        IReadOnlyList<SectorManager.SectorInfo> allSectors = SectorManager.GetAllSectorInfos();
        if (allSectors == null || allSectors.Count == 0)
        {
            _status = "SectorManager sem dados. Rode 'Rebuild From Active Constructions' primeiro.";
            return null;
        }

        // Passo 2: objetivos
        foreach (SectorManager.SectorInfo info in allSectors)
        {
            if (info.IsFullyControlled && info.ControllingTeam == selectedTeam) continue;
            bool hasCap = false;
            foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
                if (c.OwnerTeam != selectedTeam) { hasCap = true; break; }
            if (!hasCap) continue;

            var obj = new SectorObjective
            {
                Sector = info.Sector, AssignedTeam = selectedTeam,
                Status = ObjectiveStatus.Pending, Priority = SimulateCalculatePriority(info),
            };
            int slots = info.GetRiskLevelFor(ResolveSelectedSlot()) >= SectorManager.SectorRiskLevel.High ? 2 : 1;
            for (int s = 0; s < slots; s++) obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
            plan.Objectives.Add(obj);
        }

        // Passo 3: ordena e renumera
        plan.Objectives.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        for (int i = 0; i < plan.Objectives.Count; i++) plan.Objectives[i].Priority = i + 1;

        // Passo 3c: objetivos defensivos — setores conquistados com inimigo visível a ≤3h
        {
            int DefenseEnemyRange = defenseEnemyRange;
            int defPriority = plan.Objectives.Count + 1;
            foreach (SectorManager.SectorInfo info in allSectors)
            {
                if (!info.IsFullyControlled || info.ControllingTeam != selectedTeam) continue;
                if (plan.GetObjectiveForSector(info.Sector) != null) continue;
                Vector3Int rc = info.RepresentativeCell; rc.z = 0;
                if (!SimHasNearbyEnemy(rc, DefenseEnemyRange)) continue;
                var defObj = new SectorObjective
                {
                    Sector = info.Sector, AssignedTeam = selectedTeam,
                    Status = ObjectiveStatus.Pending, Priority = defPriority++,
                };
                int defSlots = info.HasPartialCapture ? 2 : 1;
                for (int s = 0; s < defSlots; s++)
                    defObj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
                plan.Objectives.Add(defObj);
            }
        }

        // Passo 5: capturadores livres
        var freeCapturers = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != selectedTeam || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (data.roles != null && data.roles.Contains(UnitRole.Capturador)) freeCapturers.Add(u);
        }

        if (freeCapturers.Count == 0) { _status = "Nenhum capturador encontrado."; return plan; }

        // Passo 5a: captura imediata
        var immediateList = new List<UnitManager>();
        foreach (UnitManager u in freeCapturers)
        {
            if (boardTilemap == null) break;
            Vector3Int uCell = u.CurrentCellPosition; uCell.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, uCell);
            if (bldg == null || !bldg.IsCapturable || bldg.CapturePointsMax <= 0) continue;
            if (bldg.TeamId == selectedTeam && bldg.CurrentCapturePoints >= bldg.CapturePointsMax) continue;
            SectorObjective obj = plan.GetObjectiveForSector(bldg.Sector);
            if (obj == null || !obj.HasOpenSlot(UnitRole.Capturador)) continue;
            obj.TryFillSlot(UnitRole.Capturador, u.InstanceId);
            obj.Status = ObjectiveStatus.Pursuing;
            immediateList.Add(u);
            trace.ImmediateCaptures.Add((u, bldg.Sector));
        }
        foreach (UnitManager u in immediateList) freeCapturers.Remove(u);

        // Passo 5b: cascade + assignable
        bool isInitDist = !plan.Objectives.Exists(o => o.Status == ObjectiveStatus.Pursuing);
        trace.IsInitialDistribution = isInitDist;
        const float MaxCoArrivalGap = 5f;
        var cascadeCovered = new HashSet<ConstructionSector>();
        var assignable     = new List<(SectorObjective obj, Vector3Int cell)>();

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!obj.HasOpenSlot(UnitRole.Capturador)) continue;
            if (cascadeCovered.Contains(obj.Sector))
            {
                if (!trace.CascadeBlocked.Contains(obj.Sector)) trace.CascadeBlocked.Add(obj.Sector);
                continue;
            }
            bool isDefensive = false;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, selectedTeam);
            Vector3Int tc;
            if (tgt != null)
            {
                tc = tgt.CurrentCellPosition; tc.z = 0;
            }
            else if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)
                && defInfo.IsFullyControlled && defInfo.ControllingTeam == selectedTeam)
            {
                isDefensive = true;
                tc = defInfo.RepresentativeCell; tc.z = 0;
            }
            else continue;
            if (!isDefensive && cascadeCovered.Contains(obj.Sector)) continue;

            // 2º slot co-chegada
            if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo slotInfo)
                && slotInfo.GetRiskLevelFor(ResolveSelectedSlot()) >= SectorManager.SectorRiskLevel.High)
            {
                SlotNeed filled = obj.Slots.Find(s => s.Role == UnitRole.Capturador && s.Filled);
                if (filled != null)
                {
                    UnitManager assigned = null;
                    foreach (UnitManager u in UnitManager.AllActive)
                        if (u.InstanceId == filled.AssignedUnitId && u.TeamId == selectedTeam && !u.IsDead)
                        { assigned = u; break; }
                    Vector3Int aPos = assigned != null ? assigned.CurrentCellPosition : tc; aPos.z = 0;
                    float aDist = SectorManager.TryGetLandMovementDistance(aPos, tc, out int adc)
                        ? adc : SectorManager.HexDistance(aPos, tc);
                    float nearFree = float.MaxValue;
                    foreach (UnitManager cap in freeCapturers)
                    {
                        Vector3Int cc = cap.CurrentCellPosition; cc.z = 0;
                        float d = SectorManager.TryGetLandMovementDistance(cc, tc, out int td)
                            ? td : SectorManager.HexDistance(cc, tc);
                        if (d < nearFree) nearFree = d;
                    }
                    if (nearFree == float.MaxValue || nearFree > aDist + MaxCoArrivalGap) continue;
                }
            }

            assignable.Add((obj, tc));
            if (isInitDist && !isDefensive) SimMarkCascadeNeighbor(obj.Sector, cascadeCovered);
        }

        int nu   = Mathf.Min(freeCapturers.Count, assignable.Count);
        int nObj = assignable.Count;

        if (nu == 0)
        {
            foreach (UnitManager u in freeCapturers) plan.RogueUnitIds.Add(u.InstanceId);
            return plan;
        }

        // Pré-sort
        if (freeCapturers.Count > nu)
        {
            freeCapturers.Sort((a, b) =>
            {
                float minA = float.MaxValue, minB = float.MaxValue;
                Vector3Int ca = a.CurrentCellPosition; ca.z = 0;
                Vector3Int cb = b.CurrentCellPosition; cb.z = 0;
                foreach ((_, Vector3Int tc) in assignable)
                {
                    float da = Vector3Int.Distance(ca, tc);
                    float db = Vector3Int.Distance(cb, tc);
                    if (da < minA) minA = da;
                    if (db < minB) minB = db;
                }
                return minA.CompareTo(minB);
            });
        }

        // Risk multipliers
        float RiskCostWeight = riskDecisionImpact;
        var riskMults = new float[nObj];
        for (int oj = 0; oj < nObj; oj++)
        {
            riskMults[oj] = 1f;
            if (SectorManager.TryGetSectorInfo(assignable[oj].obj.Sector, out SectorManager.SectorInfo si))
                riskMults[oj] = 1f + si.GetRiskRatioFor(ResolveSelectedSlot()) * RiskCostWeight;
        }

        // distMatrix
        const int PlanBudget = 30;
        var distMatrix = new float[nu, nObj];
        var rawDist    = new float[nu * nObj];
        for (int ui = 0; ui < nu; ui++)
        {
            UnitManager u  = freeCapturers[ui];
            Vector3Int  uc = u.CurrentCellPosition; uc.z = 0;
            Dictionary<Vector3Int, List<Vector3Int>> paths = null;
            if (boardTilemap != null && terrainDatabase != null)
                try { paths = UnitMovementPathRules.CalcularCaminhosValidos(boardTilemap, u, PlanBudget, terrainDatabase); }
                catch { paths = null; }

            for (int oj = 0; oj < nObj; oj++)
            {
                Vector3Int tc = assignable[oj].cell;
                float raw;
                if (paths != null && paths.TryGetValue(tc, out List<Vector3Int> path) && path.Count > 0)
                    raw = path.Count;
                else if (SectorManager.TryGetLandMovementDistance(uc, tc, out int terrCost))
                    raw = terrCost;
                else
                    raw = SectorManager.HexDistance(uc, tc);

                rawDist[ui * nObj + oj] = raw;
                distMatrix[ui, oj]      = raw * riskMults[oj];
            }
        }

        // Solver
        int[] bestAssign = new int[nu];
        float bestCost   = float.MaxValue;
        SolveAssignment(new bool[nObj], new int[nu], 0, nu, 0f, distMatrix, nObj, ref bestCost, ref bestAssign);

        for (int i = 0; i < nu; i++)
        {
            SectorObjective obj = assignable[bestAssign[i]].obj;
            obj.TryFillSlot(UnitRole.Capturador, freeCapturers[i].InstanceId);
            obj.Status = ObjectiveStatus.Pursuing;
        }

        // Popula trace ANTES do RemoveRange — freeCapturers[0..nu-1] são os atribuídos
        trace.DistMatrix  = distMatrix;
        trace.RawDist     = rawDist;
        trace.RiskMults   = riskMults;
        trace.BestAssign  = bestAssign;
        trace.BestCost    = bestCost;
        for (int i = 0; i < nu; i++) trace.SolverUnits.Add(freeCapturers[i]);
        trace.SolverObjs.AddRange(assignable);

        freeCapturers.RemoveRange(0, nu);
        foreach (UnitManager u in freeCapturers) plan.RogueUnitIds.Add(u.InstanceId);

        return plan;
    }

    // ------------------------------------------------------------------
    // Helpers internos
    private bool SimHasNearbyEnemy(Vector3Int cell, int maxDistance)
    {
        TeamId enemyCheck = selectedTeam;
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.IsDead || u.IsEmbarked || u.TeamId == enemyCheck) continue;
            Vector3Int ec = u.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, cell) <= maxDistance) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    private void SimMarkCascadeNeighbor(ConstructionSector sector, HashSet<ConstructionSector> covered)
    {
        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info)) return;
        float myHQDist = info.GetDistanceToHQ(ResolveSelectedSlot());
        // None explicito: default(ConstructionSector) e Alpha, entao a simulacao
        // nunca marcava Alpha como vizinho coberto (e o AIController marca).
        ConstructionSector candidate = ConstructionSector.None;
        if (info.ClosestNeighbor1 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor1, out SectorManager.SectorInfo n1)
            && n1.GetDistanceToHQ(ResolveSelectedSlot()) > myHQDist)
            candidate = info.ClosestNeighbor1;
        else if (info.ClosestNeighbor2 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor2, out SectorManager.SectorInfo n2)
            && n2.GetDistanceToHQ(ResolveSelectedSlot()) > myHQDist)
            candidate = info.ClosestNeighbor2;
        if (candidate != ConstructionSector.None) covered.Add(candidate);
    }

    private static void SolveAssignment(
        bool[] usedObj, int[] current, int depth, int maxDepth,
        float cost, float[,] distMatrix, int nObj,
        ref float bestCost, ref int[] bestAssign)
    {
        if (depth == maxDepth)
        {
            if (cost < bestCost) { bestCost = cost; System.Array.Copy(current, bestAssign, maxDepth); }
            return;
        }
        for (int j = 0; j < nObj; j++)
        {
            if (usedObj[j]) continue;
            float nc = cost + distMatrix[depth, j];
            if (nc >= bestCost) continue;
            current[depth] = j; usedObj[j] = true;
            SolveAssignment(usedObj, current, depth + 1, maxDepth, nc, distMatrix, nObj, ref bestCost, ref bestAssign);
            usedObj[j] = false;
        }
    }

    private int SimulateCalculatePriority(SectorManager.SectorInfo info)
    {
        float dist = info.GetDistanceToHQ(ResolveSelectedSlot());
        if (dist == float.MaxValue) return 0;
        SectorManager.SectorRiskLevel risk = info.GetRiskLevelFor(ResolveSelectedSlot());
        int rb = risk == SectorManager.SectorRiskLevel.Safe ? 40
               : risk == SectorManager.SectorRiskLevel.Low  ? 30
               : risk == SectorManager.SectorRiskLevel.Medium ? 20
               : risk == SectorManager.SectorRiskLevel.High   ? 10 : 0;
        int dp = Mathf.RoundToInt(dist);
        int db = info.IsDisputed ? 15 : 0;
        int bb = 0;
        foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
        {
            if (c.Source == null || c.OwnerTeam == selectedTeam) continue;
            if (c.Source.IsPlayerHeadQuarter)  bb += 100;
            else if (c.Source.CanProduceUnits) bb += 30;
            else if (c.Source.CapturedIncoming > 0) bb += 15;
        }
        return rb - dp + db + Mathf.Clamp(bb, 0, 80);
    }

    // ------------------------------------------------------------------
    // SceneView
    // ------------------------------------------------------------------
    private void OnSceneGUI(SceneView _)
    {
        if (_lines.Count == 0 || boardTilemap == null) return;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        foreach (SceneLine line in _lines)
        {
            Handles.color = line.Color;
            Handles.DrawAAPolyLine(3f, line.From, line.To);
            Handles.SphereHandleCap(0, line.From, Quaternion.identity, 0.10f, EventType.Repaint);
            Handles.SphereHandleCap(0, line.To,   Quaternion.identity, 0.10f, EventType.Repaint);
            if (!string.IsNullOrEmpty(line.Label))
                Handles.Label(Vector3.Lerp(line.From, line.To, 0.5f) + new Vector3(0.08f, 0.08f, 0f),
                    line.Label, EditorStyles.miniLabel);
        }
    }

    private void RebuildLines()
    {
        ClearLines();
        if (_plan == null || boardTilemap == null) return;
        foreach (ObjRow row in _rows)
        {
            if (row.Target == null) continue;
            Vector3Int tc = row.Target.CurrentCellPosition; tc.z = 0;
            Vector3 tWorld = boardTilemap.GetCellCenterWorld(tc);
            foreach (SlotNeed slot in row.Obj.Slots)
            {
                if (!slot.Filled) continue;
                _liveById.TryGetValue(slot.AssignedUnitId, out UnitManager u);
                if (u == null) continue;
                Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                float dist = ComputeGameDist(u, row.Target);
                Color col  = dist < 0 ? Color.gray : dist <= 2f ? Color.green : dist <= 5f ? Color.yellow : Color.red;
                _lines.Add(new SceneLine
                {
                    From  = boardTilemap.GetCellCenterWorld(uc),
                    To    = tWorld, Color = col,
                    Label = $"#{slot.AssignedUnitId}→{row.Obj.Sector} ({(dist<0?"?":dist.ToString("F0"))}h)",
                });
            }
        }
        SceneView.RepaintAll();
    }

    private void ClearLines() { _lines.Clear(); SceneView.RepaintAll(); }

    // ------------------------------------------------------------------
    // Helpers estáticos — lógica idêntica ao AIController
    // ------------------------------------------------------------------
    private static ConstructionManager FindCapturableInSector(ConstructionSector sector, TeamId aiTeam)
    {
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.Sector != sector || !c.IsCapturable) continue;
            if (c.TeamId == aiTeam && c.CurrentCapturePoints >= c.CapturePointsMax) continue;
            return c;
        }
        return null;
    }

    private static float ComputeGameDist(UnitManager unit, ConstructionManager target)
    {
        if (unit == null || target == null) return -1f;
        Vector3Int uc = unit.CurrentCellPosition;   uc.z = 0;
        Vector3Int tc = target.CurrentCellPosition; tc.z = 0;
        if (SectorManager.TryGetLandMovementDistance(uc, tc, out int cost)) return cost;
        return SectorManager.HexDistance(uc, tc);
    }

    private static bool IsCapturador(UnitManager u)
    {
        if (!u.TryGetUnitData(out UnitData d)) return false;
        return d.roles != null && d.roles.Contains(UnitRole.Capturador);
    }

    private string FindAssignmentLabel(UnitManager u)
    {
        if (_plan == null) return "—";
        foreach (SectorObjective obj in _plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && slot.AssignedUnitId == u.InstanceId)
                {
                    ConstructionManager tgt = FindCapturableInSector(obj.Sector, selectedTeam);
                    float d = ComputeGameDist(u, tgt);
                    return $"→ {obj.Sector} (pri{obj.Priority} {(d<0?"?":d.ToString("F0"))}h)";
                }
        if (_plan.RogueUnitIds.Contains(u.InstanceId)) return "[rogue]";
        return "—";
    }

    // ------------------------------------------------------------------
    // Cache vivo
    // ------------------------------------------------------------------
    private void SyncLiveUnitCache()
    {
        _liveById.Clear();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.IsDead) continue;
            if (selectedTeam != TeamId.Neutral && u.TeamId != selectedTeam) continue;
            _liveById[u.InstanceId] = u;
        }
    }

    private static void SyncSceneRegistries()
    {
        if (!Application.isPlaying)
        {
            UnitManager.AllActive.Clear();
            UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (units != null)
                foreach (UnitManager u in units)
                    if (u != null && u.gameObject.activeInHierarchy) UnitManager.AllActive.Add(u);

            ConstructionManager.AllActive.Clear();
            ConstructionManager[] cons = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cons != null)
                foreach (ConstructionManager c in cons)
                    if (c != null && c.gameObject.activeInHierarchy) ConstructionManager.AllActive.Add(c);
        }
        else if (ConstructionManager.AllActive.Count == 0)
        {
            ConstructionManager[] cons = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cons != null)
                foreach (ConstructionManager c in cons)
                    if (c != null) ConstructionManager.AllActive.Add(c);
        }
    }

    // ------------------------------------------------------------------
    // Auto Detect
    // ------------------------------------------------------------------
    private void AutoDetectContext()
    {
        if (boardTilemap == null)
        {
            Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Tilemap m in maps)
                if (m != null && string.Equals(m.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                { boardTilemap = m; break; }
            if (boardTilemap == null && maps != null && maps.Length > 0) boardTilemap = maps[0];
        }
        if (terrainDatabase == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
            foreach (string g in guids)
            {
                TerrainDatabase db = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(AssetDatabase.GUIDToAssetPath(g));
                if (db != null) { terrainDatabase = db; break; }
            }
        }
        if (referenceUnitData == null)
        {
            string[] guids = AssetDatabase.FindAssets("Soldado t:UnitData");
            if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:UnitData");
            foreach (string g in guids)
            {
                UnitData ud = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g));
                if (ud != null) { referenceUnitData = ud; break; }
            }
        }
        if (Application.isPlaying)
        {
            AIController ai = Object.FindAnyObjectByType<AIController>();
            if (ai != null)
            {
                if (selectedTeam == TeamId.Neutral) selectedTeam = ai.CurrentAITeam;
                riskDecisionImpact = ai.RiskDecisionImpact;
                defenseEnemyRange  = ai.DefenseEnemyRange;
            }
            else if (selectedTeam == TeamId.Neutral)
            {
                MatchController mc = Object.FindAnyObjectByType<MatchController>();
                if (mc != null) selectedTeam = mc.ActiveTeam;
            }
        }
    }

    private PlayerSlotId ResolveSelectedSlot()
    {
        MatchController match = Object.FindAnyObjectByType<MatchController>();
        if (match == null)
            return PlayerSlotId.Invalid;
        if (match.ActiveTeam == selectedTeam && match.ActiveSlotId.IsValid)
            return match.ActiveSlotId;
        return match.TryGetUniqueSlotForTeam(selectedTeam, out PlayerSlotId slot)
            ? slot
            : PlayerSlotId.Invalid;
    }
}
