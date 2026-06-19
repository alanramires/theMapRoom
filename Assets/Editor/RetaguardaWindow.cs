using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RetaguardaWindow : EditorWindow
{
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private bool useSelectionTeam = true;
    [SerializeField] private TeamId team = TeamId.Green;
    [SerializeField] private bool includeOtherRoles = false;

    [SerializeField] private Vector3Int anchorHex;
    [SerializeField] private int frontBandWidth = 3;
    [SerializeField] private float desiredRearGap = 2f;
    [SerializeField] private int paintRadius = 6;
    [SerializeField] private float coneSpread = 0.8f;
    [SerializeField] private int enemyThreatRadius = 3;
    [SerializeField] private float enemyThreatWeight = 120f;
    [SerializeField] private int enemyAvoidRange = 1;
    [SerializeField] private float allyScreenStrength = 0.6f;

    private bool pickingAnchor;
    private Vector3Int hoverCell;
    private bool hasResult;
    private readonly List<Vector3Int> combatantCells = new List<Vector3Int>();
    private readonly List<Vector3Int> frontBandCells = new List<Vector3Int>();
    private readonly List<Vector3Int> enemyCells = new List<Vector3Int>();
    private float frontBandDist;
    private Dictionary<Vector3Int, float> rearScoreMap;
    private HashSet<Vector3Int> vanguardCells;
    private float maxRearScore = 1f;
    private Vector3Int bestRearCell;
    private bool hasBestRear;
    private string statusMessage = string.Empty;
    private Vector2 scroll;

    [MenuItem("Tools/Utils/Retaguarda")]
    public static void Open() => GetWindow<RetaguardaWindow>("Retaguarda").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        if (tilemap == null)
            tilemap = FindFirstObjectByType<Tilemap>();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        pickingAnchor = false;
    }

    private void OnSelectionChange()
    {
        if (useSelectionTeam)
            Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Contexto", EditorStyles.boldLabel);
        tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", tilemap, typeof(Tilemap), true);

        useSelectionTeam = EditorGUILayout.Toggle("Time da selecao", useSelectionTeam);
        if (useSelectionTeam)
        {
            TeamId resolved = ResolveSelectionTeam(out string label);
            EditorGUILayout.LabelField("  Time ativo:", label, EditorStyles.miniLabel);
            team = resolved;
        }
        else
        {
            team = (TeamId)EditorGUILayout.EnumPopup("Time", team);
        }

        includeOtherRoles = EditorGUILayout.Toggle("Incluir transporte/log/apoio na linha", includeOtherRoles);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Objetivo", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        anchorHex = EditorGUILayout.Vector3IntField("Hex", anchorHex);
        GUI.backgroundColor = pickingAnchor ? Color.red : Color.white;
        if (GUILayout.Button(pickingAnchor ? "X" : "<", GUILayout.Width(28)))
        {
            pickingAnchor = !pickingAnchor;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Usar centro dos inimigos"))
            SetAnchorFromEnemies();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Parametros", EditorStyles.boldLabel);
        frontBandWidth = EditorGUILayout.IntSlider("Largura da faixa", frontBandWidth, 1, 6);
        desiredRearGap = EditorGUILayout.Slider("Hexes de retaguarda", desiredRearGap, 1f, 4f);
        paintRadius = EditorGUILayout.IntSlider("Raio de pintura", paintRadius, 3, 12);
        coneSpread = EditorGUILayout.Slider("Abertura da fatia", coneSpread, 0f, 2f);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Ameaca inimiga", EditorStyles.miniLabel);
        enemyThreatRadius = EditorGUILayout.IntSlider("Alcance da ameaca", enemyThreatRadius, 0, 6);
        enemyThreatWeight = EditorGUILayout.Slider("Peso da ameaca", enemyThreatWeight, 0f, 300f);
        enemyAvoidRange = EditorGUILayout.IntSlider("Descartar ate", enemyAvoidRange, 0, 4);
        allyScreenStrength = EditorGUILayout.Slider("Escudo aliado", allyScreenStrength, 0f, 1f);

        EditorGUILayout.Space(6f);
        EditorGUI.BeginDisabledGroup(tilemap == null);
        if (GUILayout.Button("Calcular Retaguarda", GUILayout.Height(28)))
            Recalculate();
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("Limpar"))
            ClearResult();

        if (hasResult)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"Combatentes: {combatantCells.Count} | Faixa: {frontBandCells.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Distancia media da faixa ao objetivo: {frontBandDist:F1}", EditorStyles.miniLabel);
            if (hasBestRear)
                EditorGUILayout.LabelField($"Melhor retaguarda: {bestRearCell} (score {maxRearScore:F0})", EditorStyles.boldLabel);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Vermelho = vanguarda | Verde = retaguarda", EditorStyles.miniLabel);

        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private void Recalculate()
    {
        ClearResult();

        if (tilemap == null)
        {
            statusMessage = "Tilemap obrigatorio.";
            return;
        }

        TeamId activeTeam = useSelectionTeam ? ResolveSelectionTeam(out _) : team;
        CollectSceneCells(activeTeam);

        if (combatantCells.Count == 0)
        {
            statusMessage = "Nenhum combatente aliado em campo.";
            return;
        }

        AIBacklineSettings settings = BuildSettings();
        Vector3Int anchor = anchorHex;
        anchor.z = 0;
        AIBacklineResult result = AIBacklineAnalyzer.Analyze(combatantCells, enemyCells, anchor, settings);
        if (!result.Success)
        {
            statusMessage = result.Error;
            return;
        }

        frontBandCells.AddRange(result.FrontBandCells);
        frontBandDist = result.FrontBandDist;
        rearScoreMap = result.RearScoreMap;
        vanguardCells = result.VanguardCells;
        maxRearScore = result.MaxRearScore;
        bestRearCell = result.BestRearCell;
        hasBestRear = result.HasBestRear;

        hasResult = true;
        SceneView.RepaintAll();
        Repaint();
    }

    private AIBacklineSettings BuildSettings()
    {
        AIBacklineSettings settings = AIBacklineSettings.Default;
        settings.FrontBandWidth = frontBandWidth;
        settings.DesiredRearGap = desiredRearGap;
        settings.PaintRadius = paintRadius;
        settings.ConeSpread = coneSpread;
        settings.EnemyThreatRadius = enemyThreatRadius;
        settings.EnemyThreatWeight = enemyThreatWeight;
        settings.EnemyAvoidRange = enemyAvoidRange;
        settings.AllyScreenStrength = allyScreenStrength;
        settings.CellToWorld = c => tilemap.GetCellCenterWorld(c);
        return settings;
    }

    private void CollectSceneCells(TeamId activeTeam)
    {
        UnitManager[] all = FindObjectsByType<UnitManager>(FindObjectsSortMode.None);
        foreach (UnitManager u in all)
        {
            if (u == null || u.IsDead || u.IsEmbarked)
                continue;

            Vector3Int cell = u.CurrentCellPosition;
            cell.z = 0;
            if (u.TeamId != activeTeam)
            {
                enemyCells.Add(cell);
                continue;
            }

            // Linha de frente = só Capturador + Assalto. Demais papéis (transporte, logística,
            // suprimento, artilharia, intel) só entram se o toggle estiver ligado.
            bool eligible = includeOtherRoles ? !IsBacklineSupportUnit(u) : IsFrontlineCombatant(u);
            if (!eligible)
                continue;

            combatantCells.Add(cell);
        }
    }

    private static bool IsFrontlineCombatant(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.roles == null)
            return false;

        return data.roles.Contains(UnitRole.Capturador) || data.roles.Contains(UnitRole.Assalto);
    }

    private static bool IsBacklineSupportUnit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.roles == null)
            return false;

        return data.roles.Contains(UnitRole.FogoIndireto) || data.roles.Contains(UnitRole.Intel);
    }

    private void SetAnchorFromEnemies()
    {
        TeamId activeTeam = useSelectionTeam ? ResolveSelectionTeam(out _) : team;
        UnitManager[] all = FindObjectsByType<UnitManager>(FindObjectsSortMode.None);
        Vector3 acc = Vector3.zero;
        int n = 0;
        foreach (UnitManager u in all)
        {
            if (u == null || u.TeamId == activeTeam || u.IsDead || u.IsEmbarked)
                continue;

            Vector3Int cell = u.CurrentCellPosition;
            acc += new Vector3(cell.x, cell.y, 0);
            n++;
        }

        if (n == 0)
        {
            statusMessage = "Nenhum inimigo na cena para inferir o objetivo.";
            return;
        }

        acc /= n;
        anchorHex = new Vector3Int(Mathf.RoundToInt(acc.x), Mathf.RoundToInt(acc.y), 0);
        statusMessage = string.Empty;
        Repaint();
    }

    private void ClearResult()
    {
        hasResult = false;
        combatantCells.Clear();
        frontBandCells.Clear();
        enemyCells.Clear();
        rearScoreMap = null;
        vanguardCells = null;
        hasBestRear = false;
        statusMessage = string.Empty;
        SceneView.RepaintAll();
        Repaint();
    }

    private TeamId ResolveSelectionTeam(out string label)
    {
        if (Selection.activeGameObject != null)
        {
            UnitManager u = Selection.activeGameObject.GetComponent<UnitManager>();
            if (u != null)
            {
                label = $"{u.name} -> {u.TeamId}";
                return u.TeamId;
            }
        }

        label = $"(nenhuma unidade selecionada) -> {team}";
        return team;
    }

    private void OnSceneGUI(SceneView _)
    {
        if (tilemap == null)
            return;

        HandlePickingInput();

        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        if (hasResult)
            DrawResult(labelStyle);

        if (pickingAnchor)
            DrawPicker();
    }

    private void DrawResult(GUIStyle labelStyle)
    {
        if (rearScoreMap != null)
        {
            foreach (KeyValuePair<Vector3Int, float> kv in rearScoreMap)
            {
                float t = Mathf.Clamp01(kv.Value / maxRearScore);
                Handles.color = Color.Lerp(new Color(0.35f, 0.7f, 0.35f, 0.45f), new Color(0f, 0.85f, 0.1f, 0.85f), t);
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(kv.Key), Vector3.back, 0.22f);
            }
        }

        if (vanguardCells != null)
        {
            Handles.color = new Color(0.9f, 0.15f, 0.1f, 0.45f);
            foreach (Vector3Int cell in vanguardCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.20f);
        }

        Handles.color = new Color(1f, 0.1f, 0.1f, 0.95f);
        foreach (Vector3Int cell in frontBandCells)
            Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.30f);

        Handles.color = new Color(0.7f, 0.25f, 0.25f, 0.7f);
        foreach (Vector3Int cell in combatantCells)
            if (!frontBandCells.Contains(cell))
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.26f);

        if (hasBestRear)
        {
            Handles.color = new Color(0.1f, 1f, 0.2f, 1f);
            Vector3 bestWorld = tilemap.GetCellCenterWorld(bestRearCell);
            Handles.DrawWireDisc(bestWorld, Vector3.back, 0.36f);
            Handles.DrawWireDisc(bestWorld, Vector3.back, 0.30f);
            Handles.Label(bestWorld + Vector3.up * 0.42f, "RET", labelStyle);
        }

        Handles.color = new Color(0.55f, 0f, 0.55f, 0.95f);
        foreach (Vector3Int cell in enemyCells)
            Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.24f);

        Handles.color = new Color(1f, 0.85f, 0f, 0.95f);
        Vector3 anchorWorld = tilemap.GetCellCenterWorld(anchorHex);
        Handles.DrawSolidDisc(anchorWorld, Vector3.back, 0.32f);
        Handles.Label(anchorWorld + Vector3.up * 0.42f, "OBJ", labelStyle);
    }

    private void DrawPicker()
    {
        Handles.color = Color.red;
        Vector3 hoverWorld = tilemap.GetCellCenterWorld(hoverCell);
        Handles.DrawWireDisc(hoverWorld, Vector3.back, 0.35f);
        Handles.Label(hoverWorld + Vector3.up * 0.42f, "Objetivo " + hoverCell,
            new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.red } });
    }

    private void HandlePickingInput()
    {
        if (!pickingAnchor)
            return;

        Event e = Event.current;
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            hoverCell = ScreenToCell(e.mousePosition);
            HandleUtility.Repaint();
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Vector3Int picked = ScreenToCell(e.mousePosition);
            picked.z = 0;
            anchorHex = picked;
            pickingAnchor = false;
            e.Use();
            Repaint();
        }

        if ((e.type == EventType.MouseDown && e.button == 1)
            || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
        {
            pickingAnchor = false;
            e.Use();
            Repaint();
        }

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    private Vector3Int ScreenToCell(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        float t = ray.direction.z != 0f ? -ray.origin.z / ray.direction.z : 0f;
        return tilemap.WorldToCell(ray.origin + ray.direction * t);
    }
}
