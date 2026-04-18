using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tools > AI > AI Unit Planner
///
/// Layout: painel esquerdo (fila de unidades) | painel direito (resultado do HexEvaluator).
/// Snapshot da fila tirado no momento do AI Pause, não em OnFocus.
/// Apenas a primeira unidade da fila pode ser avaliada (as demais verão estado diferente).
/// </summary>
public class AIUnitPlannerWindow : EditorWindow
{
    // -----------------------------------------------------------------
    // Contexto de cena
    // -----------------------------------------------------------------
    [SerializeField] private Tilemap         boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    private MatchController                  _matchController;

    // -----------------------------------------------------------------
    // Fila de unidades (snapshot tirado no AI Pause — nunca muda depois)
    // -----------------------------------------------------------------
    private readonly List<UnitManager> _queue      = new List<UnitManager>();
    private int                        _selectedIdx = 0;  // clicado pelo usuário (info + evaluate)
    private int                        _currentIdx  = 0;  // cursor > : primeiro com HasActed == false
    private Vector2                    _leftScroll;

    // -----------------------------------------------------------------
    // Resultado do Evaluate
    // -----------------------------------------------------------------
    private List<HexEvaluation>         _lastResult;
    private Dictionary<Vector3Int, int> _cellPathLength = new Dictionary<Vector3Int, int>();
    private Vector3Int                  _lastFromCell;
    private string                      _contextLabel   = "—";
    private string                      _statusMsg      = string.Empty;
    private Vector2                     _rightScroll;

    // -----------------------------------------------------------------
    // Largura do painel esquerdo
    // -----------------------------------------------------------------
    private const float LeftPanelWidth = 180f;

    // -----------------------------------------------------------------
    // Estilos
    // -----------------------------------------------------------------
    private GUIStyle _cardSelected;
    private GUIStyle _cardNormal;
    private GUIStyle _chosenRow;
    private bool     _stylesReady;

    [MenuItem("Tools/AI/AI Unit Planner")]
    public static void OpenWindow() => GetWindow<AIUnitPlannerWindow>("AI Unit Planner");

    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnInspectorUpdate()
    {

        // Detecta transição para pausado: re-snapshot DEPOIS que o batch atual terminou


        // Avança o cursor > para o próximo que ainda não agiu
        _currentIdx = _queue.Count;
        for (int i = 0; i < _queue.Count; i++)
        {
            if (_queue[i] != null && !_queue[i].HasActed)
            {
                _currentIdx = i;
                break;
            }
        }

        Repaint();
    }

    // -----------------------------------------------------------------
    // GUI principal
    // -----------------------------------------------------------------

    private void OnGUI()
    {
        EnsureStyles();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Entre em Play Mode para usar o AI Unit Planner.", MessageType.Info);
            DrawContextFields();
            return;
        }

        // Barra superior: contexto + controles de execução
        DrawTopBar();

        EditorGUILayout.Space(2);

        // Corpo: dois painéis lado a lado
        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    // -----------------------------------------------------------------
    // Barra superior
    // -----------------------------------------------------------------

    private void DrawTopBar()
    {
        if (_matchController == null)
            _matchController = Object.FindAnyObjectByType<MatchController>();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawContextFields();

        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capturar fila", GUILayout.Height(22)))
            SnapshotQueue();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("AI legacy removida da compilacao. Use Capturar fila para avaliar unidades com HexEvaluator.", MessageType.Info);

        EditorGUILayout.EndVertical();
    }
    private void DrawContextFields()
    {
        boardTilemap    = (Tilemap)EditorGUILayout.ObjectField("Tilemap",   boardTilemap,    typeof(Tilemap),          true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain DB", terrainDatabase, typeof(TerrainDatabase), false);
    }

    // -----------------------------------------------------------------
    // Painel esquerdo — fila de unidades
    // -----------------------------------------------------------------

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));
        EditorGUILayout.LabelField("Fila de Iniciativa", EditorStyles.miniBoldLabel);

        if (_queue.Count == 0)
        {
            EditorGUILayout.HelpBox("Sem unidades.\nPause a IA para capturar a fila.", MessageType.None);
            EditorGUILayout.EndVertical();
            return;
        }

        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

        for (int i = 0; i < _queue.Count; i++)
        {
            UnitManager u = _queue[i];
            if (u == null) continue;

            bool isCurrent  = i == _currentIdx;   // cursor de execução
            bool isSelected = i == _selectedIdx;   // seleção do usuário
            bool hasActed   = u.HasActed;

            GUIStyle style = isSelected ? _cardSelected : _cardNormal;

            // Cursor de execução: ">" na frente
            string cursor = isCurrent ? ">" : " ";
            string label  = $"{cursor} {u.UnitDisplayName} #{u.InstanceId}\n   HP {u.CurrentHP}";

            // Unidades que já agiram: nome riscado via cor mais apagada
            Color prevColor = GUI.color;
            if (hasActed) GUI.color = new Color(1f, 1f, 1f, 0.35f);

            if (GUILayout.Button(label, style, GUILayout.Width(LeftPanelWidth - 6), GUILayout.Height(34)))
                _selectedIdx = i;

            GUI.color = prevColor;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // -----------------------------------------------------------------
    // Painel direito — info + evaluate + resultados
    // -----------------------------------------------------------------

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();

        UnitManager selected = (_selectedIdx < _queue.Count) ? _queue[_selectedIdx] : null;

        if (selected == null)
        {
            EditorGUILayout.HelpBox("Selecione uma unidade na fila.", MessageType.None);
            EditorGUILayout.EndVertical();
            return;
        }

        // Info da unidade
        DrawUnitInfo(selected);

        EditorGUILayout.Space(4);

        // Botão Evaluate — só para a unidade com cursor >
        bool isCurrent = _selectedIdx == _currentIdx;
        bool canEval   = isCurrent && boardTilemap != null && terrainDatabase != null;

        EditorGUI.BeginDisabledGroup(!canEval);
        if (GUILayout.Button("▶  Evaluate", GUILayout.Height(24)))
            RunEvaluate(selected);
        EditorGUI.EndDisabledGroup();

        if (!isCurrent)
            EditorGUILayout.HelpBox(
                "Apenas a unidade marcada com > pode ser avaliada com precisão.\n" +
                "O estado do jogo que as demais verão depende de tudo que acontecer antes.",
                MessageType.None);

        if (!string.IsNullOrEmpty(_statusMsg))
            EditorGUILayout.HelpBox(_statusMsg, MessageType.Warning);

        // Resultados
        if (_lastResult != null && _lastResult.Count > 0)
            DrawResults();

        EditorGUILayout.EndVertical();
    }

    private void DrawUnitInfo(UnitManager unit)
    {
        Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawInfoRow("Posição",    $"{cell.x}, {cell.y}");
        DrawInfoRow("HP",         $"{unit.CurrentHP}");

        ConstructionManager c = boardTilemap != null
            ? ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell)
            : null;

        if (c != null)
        {
            string cap = c.IsCapturable ? $"  [{c.CurrentCapturePoints}/{c.CapturePointsMax} pts]" : "";
            DrawInfoRow("Construção", $"{c.name}  (dono: {c.TeamId}){cap}");
        }
        else
        {
            DrawInfoRow("Construção", "—");
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawInfoRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(72));
        EditorGUILayout.LabelField(value, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawResults()
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField($"Contexto: {_contextLabel}", EditorStyles.miniBoldLabel);
        EditorGUILayout.Space(2);

        // Cabeçalho
        EditorGUILayout.BeginHorizontal();
        ColHeader("Hex",       64);
        ColHeader("Move",      34);
        ColHeader("Tipo",      88);
        ColHeader("CapProx",   56);
        ColHeader("Combat",    50);
        ColHeader("Cohesion",  58);
        ColHeader("Dev",       40);
        ColHeader("Safety",    50);
        ColHeader("Total",     50);
        ColHeader("✓",         18);
        EditorGUILayout.EndHorizontal();

        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

        foreach (HexEvaluation e in _lastResult)
        {
            GUIStyle rowSt = e.isChosen ? _chosenRow : GUIStyle.none;
            EditorGUILayout.BeginHorizontal(rowSt);

            _cellPathLength.TryGetValue(e.cell, out int steps);
            string moveLabel = steps == 0 ? "—" : $"+{steps}";

            Col($"{e.cell.x},{e.cell.y}",         64);
            Col(moveLabel,                         34);
            Col(e.type.ToString(),                 88);
            Col(e.captureProximity.ToString("F3"), 56);
            Col(e.combatValue.ToString("F3"),      50);
            Col(e.cohesion.ToString("F3"),         58);
            Col(e.deviation.ToString("F3"),        40);
            Col(e.safety.ToString("F3"),           50);
            Col(e.total.ToString("F3"),            50);
            Col(e.isChosen ? "★" : "",            18);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"  {e.actionSummary}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(e.combatSummary) && e.combatSummary != "—")
                EditorGUILayout.LabelField(
                    $"  ⚔ {e.combatSummary}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
        }

        EditorGUILayout.EndScrollView();
    }

    // -----------------------------------------------------------------
    // Evaluate
    // -----------------------------------------------------------------

    private void RunEvaluate(UnitManager unit)
    {
        _lastResult   = null;
        _statusMsg    = string.Empty;
        _contextLabel = "—";

        if (unit == null)            { _statusMsg = "Unidade nula."; return; }
        if (boardTilemap == null)    { _statusMsg = "Board Tilemap não configurado."; return; }
        if (terrainDatabase == null) { _statusMsg = "TerrainDatabase não configurado."; return; }

        TeamId     myTeam   = unit.TeamId;
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        _lastFromCell = fromCell;

        Dictionary<Vector3Int, List<Vector3Int>> validPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        var occupied = new HashSet<Vector3Int>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == unit || u.IsEmbarked) continue;
            Vector3Int p = u.CurrentCellPosition; p.z = 0;
            occupied.Add(p);
        }

        var freeCells = new List<Vector3Int>();
        if (validPaths != null)
            foreach (Vector3Int c in validPaths.Keys)
                if (!occupied.Contains(c)) freeCells.Add(c);

        _cellPathLength.Clear();
        if (validPaths != null)
            foreach (var kv in validPaths)
                if (kv.Value != null) _cellPathLength[kv.Key] = Mathf.Max(0, kv.Value.Count - 1);
        _cellPathLength[fromCell] = 0;

        TurnStateManager tsm = Object.FindAnyObjectByType<TurnStateManager>();

        _lastResult = HexEvaluator.Evaluate(
            unit, myTeam, fromCell, freeCells, boardTilemap, terrainDatabase,
            out CandidateType resolvedRole, out Vector3Int resolvedTarget, out bool resolvedHasTarget,
            tsm);

        if (_lastResult == null || _lastResult.Count == 0)
        {
            _statusMsg = "HexEvaluator não retornou candidatos.";
            return;
        }

        var chosen    = _lastResult.Find(e => e.isChosen);
        string tgt    = resolvedHasTarget ? $"alvo {resolvedTarget.x},{resolvedTarget.y}" : "sem alvo";
        string choice = chosen.isChosen
            ? $"  →  escolheu {chosen.cell.x},{chosen.cell.y} (total {chosen.total:F3})"
            : string.Empty;
        _contextLabel = $"{resolvedRole}  |  {tgt}{choice}";

        Repaint();
    }

    // -----------------------------------------------------------------
    // Snapshot da fila (chamado no AI Pause ou no botão ↺)
    // -----------------------------------------------------------------

    private void SnapshotQueue()
    {
        _queue.Clear();
        _selectedIdx = 0;
        _lastResult  = null;

        if (!Application.isPlaying) return;

        if (_matchController == null)
            _matchController = Object.FindAnyObjectByType<MatchController>();

        TeamId aiTeam = _matchController != null ? _matchController.ActiveTeam : TeamId.Neutral;

        // Replica EXATAMENTE a lógica do AIBatchExecutionLoop — incluindo LINQ estável
        // para preservar a ordem de AllActive no desempate (igual ao orquestrador)
        var sorted = UnitManager.AllActive
            .Where(u => u != null && !u.IsDead && !u.IsEmbarked && !u.HasMerged && !u.HasActed
                        && (_matchController == null || u.TeamId == aiTeam))
            .OrderBy(u =>
            {
                u.TryGetUnitData(out UnitData ud);
                return ud != null ? (int)ud.aiInitiative : (int)AiInitiative.Medium;
            })
            .ThenByDescending(u => u.CurrentHP)
            .ToList();

        _queue.AddRange(sorted);
        Repaint();
    }

    // -----------------------------------------------------------------
    // Auto-detect de contexto
    // -----------------------------------------------------------------

    private void AutoDetectContext()
    {
        if (_matchController == null)
            _matchController = Object.FindAnyObjectByType<MatchController>();

        if (boardTilemap == null)
        {
            Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Tilemap m in maps)
            {
                if (m == null) continue;
                if (string.Equals(m.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                { boardTilemap = m; break; }
            }
            if (boardTilemap == null && maps.Length > 0)
                boardTilemap = maps[0];
        }

        if (terrainDatabase == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TerrainDatabase db = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(path);
                if (db != null) { terrainDatabase = db; break; }
            }
        }
    }

    // -----------------------------------------------------------------
    // Scene view overlay
    // -----------------------------------------------------------------

    private void OnSceneGUI(SceneView _)
    {
        if (_lastResult == null || _lastResult.Count == 0 || boardTilemap == null) return;

        float hexRadius = boardTilemap.cellSize.x * 0.42f;

        foreach (HexEvaluation e in _lastResult)
        {
            Vector3 center = boardTilemap.GetCellCenterWorld(e.cell);
            center.z = 0f;

            if (e.isChosen)
            {
                Handles.color = new Color(0.2f, 1f, 0.3f, 0.55f);
                Handles.DrawSolidDisc(center, Vector3.forward, hexRadius);

                Vector3 fromWorld = boardTilemap.GetCellCenterWorld(_lastFromCell);
                fromWorld.z = 0f;
                Handles.color = new Color(0.2f, 1f, 0.3f, 0.9f);
                Handles.DrawAAPolyLine(4f, fromWorld, center);

                Handles.color = Color.white;
                Handles.Label(center + Vector3.up * hexRadius * 1.4f,
                    $"★ {e.total:F2}", EditorStyles.boldLabel);
            }
            else
            {
                Handles.color = new Color(0.7f, 0.7f, 0.7f, 0.25f);
                Handles.DrawSolidDisc(center, Vector3.forward, hexRadius * 0.6f);

                Handles.color = new Color(0.9f, 0.9f, 0.9f, 0.55f);
                Handles.Label(center + Vector3.up * hexRadius * 0.8f,
                    e.total.ToString("F2"), EditorStyles.miniLabel);
            }
        }
    }

    // -----------------------------------------------------------------
    // Estilos e helpers
    // -----------------------------------------------------------------

    private void EnsureStyles()
    {
        if (_stylesReady) return;

        _cardSelected = new GUIStyle(EditorStyles.helpBox)
        {
            normal    = { background = MakeTexture(new Color(0.25f, 0.5f, 0.9f, 0.45f)), textColor = Color.white },
            fontStyle = FontStyle.Bold,
            fontSize  = 10,
            wordWrap  = false,
            alignment = TextAnchor.MiddleLeft,
            padding   = new RectOffset(6, 4, 4, 4),
        };

        _cardNormal = new GUIStyle(EditorStyles.helpBox)
        {
            normal    = { textColor = new Color(0.85f, 0.85f, 0.85f) },
            fontSize  = 10,
            wordWrap  = false,
            alignment = TextAnchor.MiddleLeft,
            padding   = new RectOffset(6, 4, 4, 4),
        };

        _chosenRow = new GUIStyle(EditorStyles.helpBox)
        {
            normal = { background = MakeTexture(new Color(0.2f, 0.6f, 0.2f, 0.35f)) }
        };

        _stylesReady = true;
    }

    private static void ColHeader(string text, float width)
        => GUILayout.Label(text, EditorStyles.miniBoldLabel, GUILayout.Width(width));

    private static void Col(string text, float width)
        => GUILayout.Label(text, EditorStyles.miniLabel, GUILayout.Width(width));

    private static Texture2D MakeTexture(Color col)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, col);
        tex.Apply();
        return tex;
    }
}
