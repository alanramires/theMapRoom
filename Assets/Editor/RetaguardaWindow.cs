using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Ferramenta de edição (Scene) que visualiza a "faixa de vanguarda" dos combatentes
// aliados e a "retaguarda" ~2 hexes atrás dela, espelhando o cálculo usado pela AI em
// CalculateIntelFrontlineRearScore / CalculateFireSupportRearLineScore.
//
//   Bolas VERMELHAS  -> vanguarda (combatentes mais próximos do objetivo + zona à frente)
//   Bolas VERDES     -> retaguarda (onde um apoio deveria ficar, ~2 hexes atrás da faixa)
//
// Precisa de pelo menos 1 combatente aliado na cena. Só faz sentido no modo edição.
public class RetaguardaWindow : EditorWindow
{
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private bool useSelectionTeam = true;
    [SerializeField] private TeamId team = TeamId.Green;
    [SerializeField] private bool includeSupportAsCombatant = false;

    [SerializeField] private Vector3Int anchorHex;            // Objetivo (direção do inimigo)
    [SerializeField] private int frontBandWidth = 3;          // largura da faixa de vanguarda (igual à AI)
    [SerializeField] private float desiredRearGap = 2f;       // hexes atrás da faixa (igual à AI)
    [SerializeField] private int paintRadius = 6;             // raio de células avaliadas ao redor do grupo
    [SerializeField] private float coneSpread = 0.8f;          // alargamento da fatia por hex de profundidade
    [SerializeField] private int enemyThreatRadius = 3;       // alcance da ameaça inimiga (igual à AI)
    [SerializeField] private float enemyThreatWeight = 120f;  // peso da penalidade por ameaça
    [SerializeField] private int enemyAvoidRange = 1;         // células coladas no inimigo são descartadas
    [SerializeField] private float allyScreenStrength = 0.6f; // o quanto um aliado "na frente" anula a ameaça

    private bool pickingAnchor;
    private Vector3Int hoverCell;

    // Resultado calculado
    private bool hasResult;
    private readonly List<Vector3Int> combatantCells = new List<Vector3Int>();
    private readonly List<Vector3Int> frontBandCells = new List<Vector3Int>();
    private readonly List<Vector3Int> enemyCells = new List<Vector3Int>();
    private float frontBandDist;
    private Vector3Int centroid;
    private Dictionary<Vector3Int, float> rearScoreMap;       // célula -> score de retaguarda
    private HashSet<Vector3Int> vanguardCells;                // células da zona de vanguarda (à frente)
    private float maxRearScore;
    private Vector3Int bestRearCell;
    private bool hasBestRear;

    private string statusMessage = string.Empty;
    private Vector2 scroll;

    [MenuItem("Tools/Utils/Retaguarda")]
    public static void Open() => GetWindow<RetaguardaWindow>("Retaguarda").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetectContext();
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

    private void AutoDetectContext()
    {
        if (tilemap == null)
            tilemap = FindFirstObjectByType<Tilemap>();
    }

    // ── GUI ──────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Contexto", EditorStyles.boldLabel);
        tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", tilemap, typeof(Tilemap), true);

        useSelectionTeam = EditorGUILayout.Toggle("Time da seleção", useSelectionTeam);
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
        includeSupportAsCombatant = EditorGUILayout.Toggle("Apoio conta como combatente", includeSupportAsCombatant);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Objetivo (direção do inimigo)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        anchorHex = EditorGUILayout.Vector3IntField("Objetivo", anchorHex);
        GUI.backgroundColor = pickingAnchor ? Color.red : Color.white;
        if (GUILayout.Button(pickingAnchor ? "■" : "↖", GUILayout.Width(28)))
        {
            pickingAnchor = !pickingAnchor;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Usar centro dos inimigos"))
            SetAnchorFromEnemies();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Parâmetros (iguais à AI)", EditorStyles.boldLabel);
        frontBandWidth = EditorGUILayout.IntSlider("Largura da faixa", frontBandWidth, 1, 6);
        desiredRearGap = EditorGUILayout.Slider("Hexes de retaguarda", desiredRearGap, 1f, 4f);
        paintRadius = EditorGUILayout.IntSlider("Raio de pintura", paintRadius, 3, 12);
        coneSpread = EditorGUILayout.Slider("Abertura da fatia", coneSpread, 0f, 2f);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Ameaça inimiga (encolhe a retaguarda)", EditorStyles.miniLabel);
        enemyThreatRadius = EditorGUILayout.IntSlider("Alcance da ameaça", enemyThreatRadius, 0, 6);
        enemyThreatWeight = EditorGUILayout.Slider("Peso da ameaça", enemyThreatWeight, 0f, 300f);
        enemyAvoidRange = EditorGUILayout.IntSlider("Descartar até (hexes)", enemyAvoidRange, 0, 4);
        allyScreenStrength = EditorGUILayout.Slider("Escudo aliado (anula ameaça)", allyScreenStrength, 0f, 1f);

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
            EditorGUILayout.LabelField($"Combatentes: {combatantCells.Count}  |  Faixa de vanguarda: {frontBandCells.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Distância média da faixa ao objetivo: {frontBandDist:F1} hexes", EditorStyles.miniLabel);
            if (hasBestRear)
                EditorGUILayout.LabelField($"Melhor célula de retaguarda: {bestRearCell} (score {maxRearScore:F0})", EditorStyles.boldLabel);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Vermelho = vanguarda (à frente) | Verde = retaguarda (~2 atrás)", EditorStyles.miniLabel);

        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private TeamId ResolveSelectionTeam(out string label)
    {
        if (Selection.activeGameObject != null)
        {
            UnitManager u = Selection.activeGameObject.GetComponent<UnitManager>();
            if (u != null)
            {
                label = $"{u.name} → {u.TeamId}";
                return u.TeamId;
            }
        }
        label = $"(nenhuma unidade selecionada) → {team}";
        return team;
    }

    // ── Cálculo ──────────────────────────────────────────────────────────────

    private void Recalculate()
    {
        ClearResult();

        if (tilemap == null)
        {
            statusMessage = "Tilemap obrigatório.";
            return;
        }

        TeamId activeTeam = useSelectionTeam ? ResolveSelectionTeam(out _) : team;
        Vector3Int anchor = anchorHex; anchor.z = 0;

        // 1) Coleta combatentes aliados e inimigos na cena (modo edição usa FindObjects, não AllActive).
        UnitManager[] all = FindObjectsByType<UnitManager>(FindObjectsSortMode.None);
        foreach (UnitManager u in all)
        {
            if (u == null || u.IsDead || u.IsEmbarked)
                continue;

            Vector3Int c = u.CurrentCellPosition; c.z = 0;
            if (u.TeamId != activeTeam)
            {
                enemyCells.Add(c);
                continue;
            }
            if (!includeSupportAsCombatant && IsBacklineSupportUnit(u))
                continue;

            combatantCells.Add(c);
        }

        if (combatantCells.Count == 0)
        {
            statusMessage = "Nenhum combatente aliado em campo. Coloque ao menos 1 unidade na cena.";
            return;
        }

        // 2) Faixa de vanguarda = combatentes mais próximos do objetivo (espelha CalculateIntelFrontlineRearScore).
        float frontDist = float.MaxValue;
        foreach (Vector3Int c in combatantCells)
            frontDist = Mathf.Min(frontDist, SectorManager.HexDistance(c, anchor));

        float bandSum = 0f;
        Vector3 centroidAcc = Vector3.zero;
        foreach (Vector3Int c in combatantCells)
        {
            if (SectorManager.HexDistance(c, anchor) > frontDist + frontBandWidth)
                continue;
            frontBandCells.Add(c);
            bandSum += SectorManager.HexDistance(c, anchor);
            centroidAcc += new Vector3(c.x, c.y, 0);
        }

        if (frontBandCells.Count == 0)
        {
            statusMessage = "Faixa de vanguarda vazia (objetivo inválido?).";
            return;
        }

        frontBandDist = bandSum / frontBandCells.Count;
        Vector3 mid = centroidAcc / frontBandCells.Count;
        centroid = new Vector3Int(Mathf.RoundToInt(mid.x), Mathf.RoundToInt(mid.y), 0);

        // 3) Eixo direcional: frente = da formação para o objetivo; tras = oposto.
        //    A retaguarda passa a ser uma FATIA atrás do grupo (e dentro da largura dele),
        //    em vez de um anel radial — assim flancos longe do OBJ não viram retaguarda.
        Vector2 frontCentroidW = Vector2.zero;
        foreach (Vector3Int fc in frontBandCells)
            frontCentroidW += (Vector2)tilemap.GetCellCenterWorld(fc);
        frontCentroidW /= frontBandCells.Count;

        Vector2 anchorW = tilemap.GetCellCenterWorld(anchor);
        Vector2 fwd = anchorW - frontCentroidW;
        fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector2.up;
        Vector2 lat = new Vector2(-fwd.y, fwd.x);

        float hexStep = Vector2.Distance(
            tilemap.GetCellCenterWorld(centroid),
            tilemap.GetCellCenterWorld(centroid + Vector3Int.right));
        if (hexStep < 1e-4f) hexStep = 1f;

        // Largura lateral da formação (em hexes), define a base da fatia.
        float minLat = float.MaxValue, maxLat = float.MinValue;
        foreach (Vector3Int fc in frontBandCells)
        {
            float l = Vector2.Dot((Vector2)tilemap.GetCellCenterWorld(fc) - frontCentroidW, lat) / hexStep;
            minLat = Mathf.Min(minLat, l);
            maxLat = Mathf.Max(maxLat, l);
        }
        float latCenter = (minLat + maxLat) * 0.5f;
        float halfWidth = (maxLat - minLat) * 0.5f + 0.75f;

        // 4) Avalia células ao redor do grupo classificando vanguarda x retaguarda.
        rearScoreMap = new Dictionary<Vector3Int, float>();
        vanguardCells = new HashSet<Vector3Int>();
        var occupied = new HashSet<Vector3Int>(combatantCells);
        maxRearScore = 1f;
        hasBestRear = false;

        for (int dx = -paintRadius; dx <= paintRadius; dx++)
        for (int dy = -paintRadius; dy <= paintRadius; dy++)
        {
            Vector3Int cell = new Vector3Int(centroid.x + dx, centroid.y + dy, 0);
            if (SectorManager.HexDistance(cell, centroid) > paintRadius)
                continue;
            if (cell == anchor || occupied.Contains(cell))
                continue;

            Vector2 rel = (Vector2)tilemap.GetCellCenterWorld(cell) - frontCentroidW;
            float forward = Vector2.Dot(rel, fwd) / hexStep;   // + em direção ao inimigo
            float lateral = Vector2.Dot(rel, lat) / hexStep;

            if (forward > 0.5f)
            {
                // Em direção ao inimigo -> vanguarda.
                vanguardCells.Add(cell);
                continue;
            }

            float depth = -forward;                            // quão atrás da formação
            if (depth < 0.5f)
                continue;                                      // linha da própria formação (neutro)

            // Fatia: só conta dentro da largura da formação, alargando com a profundidade.
            float lateralOffset = Mathf.Abs(lateral - latCenter);
            float allow = halfWidth + coneSpread * depth;
            if (lateralOffset > allow)
                continue;                                      // flanco fora da fatia

            // Inimigos colados na célula a descartam de vez (igual ao "conservative cell allowed").
            if (IsWithinEnemyAvoidRange(cell))
                continue;

            float depthError = Mathf.Abs(depth - desiredRearGap);
            float baseScore = 360f - depthError * 120f - lateralOffset * 45f;
            float score = baseScore - CalculateThreat(cell) * enemyThreatWeight;
            if (score <= 0f)
                continue;

            rearScoreMap[cell] = score;
            if (score > maxRearScore)
            {
                maxRearScore = score;
                bestRearCell = cell;
                hasBestRear = true;
            }
        }

        hasResult = true;
        SceneView.RepaintAll();
        Repaint();
    }

    // Soma de ameaça por proximidade de inimigos (espelha CalculateDebugThreatLevel da AI):
    // quanto mais perto e mais inimigos, maior a penalidade — encolhendo a fatia da retaguarda.
    // Aliados posicionados ENTRE a célula e o inimigo funcionam como escudo (contra-fatia) e
    // anulam parte da ameaça daquele inimigo, espelhando HasAlliedScreenAheadOfFireSupportCell.
    private float CalculateThreat(Vector3Int cell)
    {
        if (enemyThreatRadius <= 0)
            return 0f;

        float threat = 0f;
        foreach (Vector3Int ec in enemyCells)
        {
            float dist = SectorManager.HexDistance(cell, ec);
            if (dist > enemyThreatRadius)
                continue;

            float baseThreat = enemyThreatRadius - dist + 1f;
            float shield = AllyScreenFactor(cell, ec, dist);
            threat += baseThreat * Mathf.Clamp01(1f - shield * allyScreenStrength);
        }
        return threat;
    }

    // Conta aliados combatentes que cobrem a linha célula→inimigo (mais perto do inimigo que a
    // célula e próximos da linha de visão). Cada um reduz a ameaça daquele inimigo.
    private float AllyScreenFactor(Vector3Int cell, Vector3Int enemy, float cellToEnemy)
    {
        if (allyScreenStrength <= 0f)
            return 0f;

        int screeners = 0;
        foreach (Vector3Int ac in combatantCells)
        {
            if (SectorManager.HexDistance(ac, enemy) >= cellToEnemy)
                continue; // aliado não está à frente (entre a célula e o inimigo)
            if (PerpendicularDistance(ac, cell, enemy) <= 1.2f)
                screeners++;
        }
        return screeners;
    }

    private static float PerpendicularDistance(Vector3Int point, Vector3Int lineStart, Vector3Int lineEnd)
    {
        Vector2 p = new Vector2(point.x, point.y);
        Vector2 a = new Vector2(lineStart.x, lineStart.y);
        Vector2 b = new Vector2(lineEnd.x, lineEnd.y);
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq <= 0.0001f)
            return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        return Vector2.Distance(p, a + ab * t);
    }

    private bool IsWithinEnemyAvoidRange(Vector3Int cell)
    {
        if (enemyAvoidRange <= 0)
            return false;
        foreach (Vector3Int ec in enemyCells)
            if (SectorManager.HexDistance(cell, ec) <= enemyAvoidRange)
                return true;
        return false;
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
            Vector3Int c = u.CurrentCellPosition;
            acc += new Vector3(c.x, c.y, 0);
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

    // ── Scene GUI ──────────────────────────────────────────────────────────────

    private void OnSceneGUI(SceneView _)
    {
        if (tilemap == null) return;
        HandlePickingInput();

        var lblStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        if (hasResult)
        {
            // Retaguarda (verde) — intensidade pelo score, pico ~desiredRearGap atrás da faixa.
            if (rearScoreMap != null)
            {
                foreach (var kv in rearScoreMap)
                {
                    float t = Mathf.Clamp01(kv.Value / maxRearScore);
                    Handles.color = Color.Lerp(new Color(0.35f, 0.7f, 0.35f, 0.45f), new Color(0.0f, 0.85f, 0.1f, 0.85f), t);
                    Vector3 cw = tilemap.GetCellCenterWorld(kv.Key);
                    Handles.DrawSolidDisc(cw, Vector3.back, 0.22f);
                }
            }

            // Vanguarda (vermelho) — zona à frente da faixa.
            if (vanguardCells != null)
            {
                Handles.color = new Color(0.9f, 0.15f, 0.1f, 0.45f);
                foreach (Vector3Int cell in vanguardCells)
                    Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.20f);
            }

            // Combatentes da faixa de vanguarda — vermelho forte.
            Handles.color = new Color(1f, 0.1f, 0.1f, 0.95f);
            foreach (Vector3Int cell in frontBandCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.30f);

            // Demais combatentes (fora da faixa) — vermelho apagado.
            Handles.color = new Color(0.7f, 0.25f, 0.25f, 0.7f);
            foreach (Vector3Int cell in combatantCells)
                if (!frontBandCells.Contains(cell))
                    Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.26f);

            // Melhor célula de retaguarda — anel destacado.
            if (hasBestRear)
            {
                Handles.color = new Color(0.1f, 1f, 0.2f, 1f);
                Vector3 bw = tilemap.GetCellCenterWorld(bestRearCell);
                Handles.DrawWireDisc(bw, Vector3.back, 0.36f);
                Handles.DrawWireDisc(bw, Vector3.back, 0.30f);
                Handles.Label(bw + Vector3.up * 0.42f, "RET", lblStyle);
            }

            // Inimigos — marcadores escuros para mostrar o que encolheu a retaguarda.
            Handles.color = new Color(0.55f, 0.0f, 0.55f, 0.95f);
            foreach (Vector3Int cell in enemyCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.24f);

            // Objetivo.
            Handles.color = new Color(1f, 0.85f, 0f, 0.95f);
            Vector3 aw = tilemap.GetCellCenterWorld(anchorHex);
            Handles.DrawSolidDisc(aw, Vector3.back, 0.32f);
            Handles.Label(aw + Vector3.up * 0.42f, "OBJ", lblStyle);
        }

        if (pickingAnchor)
        {
            Handles.color = Color.red;
            Vector3 hw = tilemap.GetCellCenterWorld(hoverCell);
            Handles.DrawWireDisc(hw, Vector3.back, 0.35f);
            Handles.Label(hw + Vector3.up * 0.42f, "Objetivo " + hoverCell,
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.red } });
        }
    }

    private void HandlePickingInput()
    {
        if (!pickingAnchor) return;

        Event e = Event.current;
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            hoverCell = ScreenToCell(e.mousePosition);
            HandleUtility.Repaint();
        }
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Vector3Int picked = ScreenToCell(e.mousePosition); picked.z = 0;
            anchorHex = picked;
            pickingAnchor = false;
            e.Use();
            Repaint();
        }
        if ((e.type == EventType.MouseDown && e.button == 1) ||
            (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
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
