using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RetaguardaWindow : EditorWindow
{
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private bool useSelectionTeam = true;
    [SerializeField] private TeamId team = TeamId.Green;
    [SerializeField] private bool includeOtherRoles = false;
    [SerializeField] private bool dynamicEnemyMassAnchor = true;
    [SerializeField] private bool includeDetectedAlliedPoints = true;
    [SerializeField] private List<Vector3Int> manualAlliedPoints = new List<Vector3Int>();
    [SerializeField] private Vector3Int evaluationHex;
    [SerializeField] private UnitManager evaluationUnit;
    [SerializeField] private bool hasEvaluationLocation;
    [SerializeField] private bool showAdvanced;
    [SerializeField] private ConstructionManager selectedSpot;

    [Header("Camadas")]
    [SerializeField] private bool showRear = true;
    [SerializeField] private bool showVanguard;
    [SerializeField] private bool showNeutralBand;
    [SerializeField] private bool showFrontBand;
    [SerializeField] private bool showFrontlineUnits;
    [SerializeField] private bool showEnemies;
    [SerializeField] private bool showSpottingPoints;
    [SerializeField] private bool showSpottingCone;
    [SerializeField] private bool showSpotReleaseFront = true;
    [SerializeField] private bool showObjective;

    [SerializeField] private Vector3Int anchorHex;
    [SerializeField] private int frontBandWidth = 3;
    [SerializeField] private float desiredRearGap = 2f;
    [SerializeField] private int paintRadius = 6;
    [SerializeField] private float coneSpread = 0.8f;
    [SerializeField] private int enemyThreatRadius = 3;
    [SerializeField] private float enemyThreatWeight = 120f;
    [SerializeField] private int enemyAvoidRange = 1;
    [SerializeField] private float allyScreenStrength = 0.6f;
    [SerializeField] private float frontlineDepthTolerance = 1.25f;
    [SerializeField] private int spottingRadius = 3;
    [SerializeField] private float spottingConeSpread = 1f;

    private bool pickingAnchor;
    private bool pickingAlliedPoint;
    private bool pickingEvaluation;
    private bool pickingSpot;
    private Vector3Int hoverCell;
    private bool hasResult;
    private readonly List<Vector3Int> combatantCells = new List<Vector3Int>();
    private readonly List<Vector3Int> frontBandCells = new List<Vector3Int>();
    private readonly List<Vector3Int> lineHeadCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> isolatedAdvanceCells = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> enemyCells = new List<Vector3Int>();
    private float frontBandDist;
    private Dictionary<Vector3Int, float> rearScoreMap;
    private HashSet<Vector3Int> vanguardCells;
    private readonly HashSet<Vector3Int> neutralBandCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> spottingCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> spottingConeCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> spotReleaseFrontCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> spotReleaseCoveredCells = new HashSet<Vector3Int>();
    private bool selectedSpotCanBeReleased;
    private float maxRearScore = 1f;
    private Vector3Int bestRearCell;
    private bool hasBestRear;
    private AIBacklineScore evaluationScore;
    private bool hasEvaluation;
    private string statusMessage = "Selecione uma unidade ou escolha um local.";
    private Vector2 scroll;

    [MenuItem("Tools/Utils/Retaguarda")]
    public static void Open() => GetWindow<RetaguardaWindow>("Retaguarda").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        if (tilemap == null)
            tilemap = ResolveBoardTilemap();
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAsset<DPQAirHeightConfig>();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        pickingAnchor = false;
        pickingAlliedPoint = false;
        pickingEvaluation = false;
        pickingSpot = false;
    }

    private void OnSelectionChange()
    {
        ConstructionManager selectedConstruction = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<ConstructionManager>()
            : null;
        if (selectedConstruction != null && selectedConstruction.IsForwardObserverSpot)
        {
            selectedSpot = selectedConstruction;
            ClearResult();
        }

        if (useSelectionTeam)
            Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Retaguarda", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Selecione a unidade ou o local que deseja investigar. A ferramenta detecta automaticamente a massa inimiga e a frente aliada.",
            MessageType.Info);

        evaluationUnit = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade investigada", evaluationUnit, typeof(UnitManager), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            UseSelectedEvaluationUnit();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectEvaluationContext();
        GUI.backgroundColor = pickingEvaluation
            ? new Color(1f, 0.75f, 0.2f)
            : Color.white;
        if (GUILayout.Button(
                pickingEvaluation ? "Clique no Scene View..." : "Escolher Local"))
        {
            pickingEvaluation = !pickingEvaluation;
            pickingAnchor = false;
            pickingAlliedPoint = false;
            pickingSpot = false;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(
                   evaluationUnit == null && !hasEvaluationLocation))
        {
            if (GUILayout.Button("Analisar Retaguarda", GUILayout.Height(30f)))
            {
                SyncEvaluationHexFromUnit();
                AutoDetectEvaluationContext();
                Recalculate();
            }
        }

        EditorGUILayout.Space(6f);
        if (hasEvaluation)
        {
            string classification = evaluationScore.IsVanguard
                ? "VANGUARDA"
                : evaluationScore.InRearSlice
                    ? "RETAGUARDA"
                    : "FLANCO / FAIXA NEUTRA";
            MessageType type = evaluationScore.InRearSlice
                ? MessageType.Info
                : evaluationScore.IsVanguard
                    ? MessageType.Warning
                    : MessageType.None;
            EditorGUILayout.HelpBox(
                $"{classification}\nHex {evaluationHex} | profundidade {evaluationScore.Depth:F1} | ameaça {evaluationScore.Threat:F1} | score {evaluationScore.Score:F0}",
                type);
        }
        else
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        showAdvanced = EditorGUILayout.Foldout(
            showAdvanced, "Avançado / visualizar geometria", true);
        if (showAdvanced)
            DrawAdvancedPanel();
    }

    private void UseSelectedEvaluationUnit()
    {
        UnitManager selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<UnitManager>()
            : null;
        if (selected == null)
        {
            statusMessage = "Selecione uma unidade na Hierarchy ou Scene View.";
            return;
        }

        evaluationUnit = selected;
        SyncEvaluationHexFromUnit();
        AutoDetectEvaluationContext();
        statusMessage = $"Pronto para analisar {selected.name}.";
        ClearResultPreservingStatus();
    }

    private void AutoDetectEvaluationContext()
    {
        if (evaluationUnit != null)
        {
            team = evaluationUnit.TeamId;
            if (evaluationUnit.BoardTilemap != null)
                tilemap = evaluationUnit.BoardTilemap;
        }
        if (tilemap == null)
            tilemap = ResolveBoardTilemap();

        TeamId activeTeam = evaluationUnit != null
            ? evaluationUnit.TeamId
            : useSelectionTeam
                ? ResolveSelectionTeam(out _)
                : team;
        SetAnchorFromEnemies(activeTeam, repaint: false);
    }

    private void SyncEvaluationHexFromUnit()
    {
        if (evaluationUnit == null)
            return;

        evaluationHex = ResolveSceneCell(
            evaluationUnit.transform, evaluationUnit.CurrentCellPosition);
        evaluationHex.z = 0;
        hasEvaluationLocation = true;
    }

    private void ClearResultPreservingStatus()
    {
        string preserved = statusMessage;
        ClearResult();
        statusMessage = preserved;
    }

    private void DrawAdvancedPanel()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "1) Massa inimiga: direcao da ameaca. 2) Pontos aliados: formam a frente/arco. 3) Unidade atual ou local desejado: ponto que sera classificado.",
            MessageType.Info);

        EditorGUILayout.LabelField("Contexto", EditorStyles.boldLabel);
        tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", tilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        if (GUILayout.Button("Detectar Tilemap do tabuleiro"))
        {
            tilemap = ResolveBoardTilemap();
            ClearResult();
        }

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
        EditorGUILayout.LabelField("Camadas", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        showRear = EditorGUILayout.ToggleLeft("Retaguarda", showRear);
        showVanguard = EditorGUILayout.ToggleLeft("Vanguarda", showVanguard);
        showNeutralBand = EditorGUILayout.ToggleLeft("Flancos", showNeutralBand);
        showFrontBand = EditorGUILayout.ToggleLeft("Linha de combate", showFrontBand);
        showFrontlineUnits = EditorGUILayout.ToggleLeft("Unidades da linha", showFrontlineUnits);
        showEnemies = EditorGUILayout.ToggleLeft("Inimigos", showEnemies);
        showSpottingPoints = EditorGUILayout.ToggleLeft("Pontos de spotting", showSpottingPoints);
        showSpottingCone = EditorGUILayout.ToggleLeft("Cone de spotting", showSpottingCone);
        showSpotReleaseFront = EditorGUILayout.ToggleLeft("Linha de liberacao do spot", showSpotReleaseFront);
        showObjective = EditorGUILayout.ToggleLeft("Referencia/direcao", showObjective);
        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("1. Massa inimiga", EditorStyles.boldLabel);
        dynamicEnemyMassAnchor = EditorGUILayout.Toggle(
            "Usar massa inimiga dinamica", dynamicEnemyMassAnchor);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(dynamicEnemyMassAnchor);
        anchorHex = EditorGUILayout.Vector3IntField("Hex de referencia", anchorHex);
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = pickingAnchor ? Color.red : Color.white;
        if (GUILayout.Button(pickingAnchor ? "X" : "<", GUILayout.Width(28)))
        {
            pickingAnchor = !pickingAnchor;
            pickingAlliedPoint = false;
            pickingEvaluation = false;
            pickingSpot = false;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Atualizar pela massa inimiga"))
            SetAnchorFromEnemies();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("2. Pontos aliados", EditorStyles.boldLabel);
        includeDetectedAlliedPoints = EditorGUILayout.Toggle(
            "Usar massa aliada detectada", includeDetectedAlliedPoints);
        for (int i = 0; i < manualAlliedPoints.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            manualAlliedPoints[i] = EditorGUILayout.Vector3IntField(
                $"Ponto aliado {i + 1}", manualAlliedPoints[i]);
            if (GUILayout.Button("-", GUILayout.Width(28)))
            {
                manualAlliedPoints.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Adicionar ponto aliado"))
            manualAlliedPoints.Add(Vector3Int.zero);
        GUI.backgroundColor = pickingAlliedPoint ? Color.red : Color.white;
        if (GUILayout.Button(
                pickingAlliedPoint ? "Cancelar selecao" : "Selecionar no mapa",
                GUILayout.Width(120)))
        {
            pickingAlliedPoint = !pickingAlliedPoint;
            pickingAnchor = false;
            pickingEvaluation = false;
            pickingSpot = false;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "3. Unidade atual / local desejado", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        evaluationHex = EditorGUILayout.Vector3IntField(
            "Hex investigado", evaluationHex);
        GUI.backgroundColor = pickingEvaluation ? Color.yellow : Color.white;
        if (GUILayout.Button(pickingEvaluation ? "X" : "<", GUILayout.Width(28)))
        {
            pickingEvaluation = !pickingEvaluation;
            pickingAnchor = false;
            pickingAlliedPoint = false;
            pickingSpot = false;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Usar unidade selecionada"))
            UseSelectedEvaluationUnit();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Parametros", EditorStyles.boldLabel);
        frontBandWidth = EditorGUILayout.IntSlider("Largura da faixa", frontBandWidth, 1, 6);
        desiredRearGap = EditorGUILayout.Slider("Hexes de retaguarda", desiredRearGap, 1f, 4f);
        paintRadius = EditorGUILayout.IntSlider("Raio de pintura", paintRadius, 3, 12);
        coneSpread = EditorGUILayout.Slider("Abertura da fatia", coneSpread, 0f, 2f);
        frontlineDepthTolerance = EditorGUILayout.Slider(
            "Coesao da cabeca", frontlineDepthTolerance, 0.5f, 2.5f);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Ameaca inimiga", EditorStyles.miniLabel);
        enemyThreatRadius = EditorGUILayout.IntSlider("Alcance da ameaca", enemyThreatRadius, 0, 6);
        enemyThreatWeight = EditorGUILayout.Slider("Peso da ameaca", enemyThreatWeight, 0f, 300f);
        enemyAvoidRange = EditorGUILayout.IntSlider("Descartar ate", enemyAvoidRange, 0, 4);
        allyScreenStrength = EditorGUILayout.Slider("Escudo aliado", allyScreenStrength, 0f, 1f);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Spotting", EditorStyles.miniLabel);
        spottingRadius = EditorGUILayout.IntSlider("Raio do cone", spottingRadius, 1, 6);
        spottingConeSpread = EditorGUILayout.Slider("Abertura do cone", spottingConeSpread, 0.25f, 2f);
        EditorGUILayout.BeginHorizontal();
        selectedSpot = (ConstructionManager)EditorGUILayout.ObjectField(
            "Spot selecionado", selectedSpot, typeof(ConstructionManager), true);
        GUI.backgroundColor = pickingSpot ? Color.cyan : Color.white;
        if (GUILayout.Button(pickingSpot ? "X" : "<", GUILayout.Width(28)))
        {
            pickingSpot = !pickingSpot;
            pickingAnchor = false;
            pickingAlliedPoint = false;
            pickingEvaluation = false;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUI.BeginDisabledGroup(true);
        Vector3Int selectedSpotCell = selectedSpot != null
            ? ResolveSceneCell(selectedSpot.transform, selectedSpot.CurrentCellPosition)
            : default;
        EditorGUILayout.Vector3IntField(
            "Spot selecionado (x,y,z)", selectedSpotCell);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.LabelField(
            "O cone pertence ao spot selecionado e independe da linha/vanguarda.",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(6f);
        EditorGUI.BeginDisabledGroup(tilemap == null);
        if (GUILayout.Button(
                new GUIContent(
                    "Calcular mapa tatico",
                    "Um ponto aliado basta; varios pontos formam um arco."),
                GUILayout.Height(28)))
            Recalculate();
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("Limpar"))
            ClearResult();

        if (hasResult)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"Combatentes: {combatantCells.Count} | Faixa: {frontBandCells.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Cabeca da linha: {lineHeadCells.Count} | Avancados isolados: {isolatedAdvanceCells.Count}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Spots: {spottingCells.Count} | Hexes nos cones: {spottingConeCells.Count}",
                EditorStyles.miniLabel);
            if (selectedSpot != null)
                EditorGUILayout.LabelField(
                    selectedSpotCanBeReleased
                        ? "Spot selecionado: LIBERAVEL (vanguarda coberta)"
                        : "Spot selecionado: MANTER OBSERVADOR",
                    selectedSpotCanBeReleased ? EditorStyles.boldLabel : EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Distancia media da faixa ao objetivo: {frontBandDist:F1}", EditorStyles.miniLabel);
            if (hasBestRear)
                EditorGUILayout.LabelField($"Melhor retaguarda: {bestRearCell} (score {maxRearScore:F0})", EditorStyles.boldLabel);
            if (hasEvaluation)
            {
                string classification = evaluationScore.IsVanguard
                    ? "VANGUARDA"
                    : evaluationScore.InRearSlice
                        ? "RETAGUARDA"
                        : "FLANCO/FAIXA NEUTRA";
                EditorGUILayout.LabelField(
                    $"Hex investigado: {classification} | profundidade={evaluationScore.Depth:F1} | ameaca={evaluationScore.Threat:F1} | score={evaluationScore.Score:F0}",
                    EditorStyles.boldLabel);
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Verde=retaguarda | Vermelho=vanguarda | Amarelo=flancos | Ciano=spotting",
            EditorStyles.miniLabel);

        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private void Recalculate()
    {
        SyncEvaluationHexFromUnit();
        ClearResult();

        if (tilemap == null)
        {
            statusMessage = "Tilemap obrigatorio.";
            return;
        }

        TeamId activeTeam = evaluationUnit != null
            ? evaluationUnit.TeamId
            : useSelectionTeam
                ? ResolveSelectionTeam(out _)
                : team;
        if (dynamicEnemyMassAnchor)
            SetAnchorFromEnemies(activeTeam, repaint: false);
        CollectSceneCells(activeTeam);
        if (!includeDetectedAlliedPoints)
            combatantCells.Clear();
        foreach (Vector3Int rawPoint in manualAlliedPoints)
        {
            Vector3Int point = rawPoint;
            point.z = 0;
            if (!combatantCells.Contains(point))
                combatantCells.Add(point);
        }
        Vector3Int anchor = anchorHex;
        anchor.z = 0;
        BuildSpottingGeometry(anchor);

        if (combatantCells.Count == 0)
        {
            statusMessage = "Nenhum combatente aliado em campo.";
            hasResult = spottingCells.Count > 0;
            SceneView.RepaintAll();
            Repaint();
            return;
        }

        AIBacklineSettings settings = BuildSettings();
        BuildFrontlineHead(anchor);
        IReadOnlyList<Vector3Int> geometryCombatants = lineHeadCells.Count > 0
            ? lineHeadCells
            : combatantCells;
        AIBacklineResult result = AIBacklineAnalyzer.Analyze(
            geometryCombatants, enemyCells, anchor, settings);
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
        evaluationHex.z = 0;
        evaluationScore = AIBacklineAnalyzer.ScoreCell(
            geometryCombatants, enemyCells, evaluationHex, anchor, settings, result);
        hasEvaluation = true;
        BuildIsolatedAdvanceCells(result, settings, anchor, geometryCombatants);
        BuildNeutralBand(result, settings, anchor, geometryCombatants);

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

            Vector3Int cell = ResolveSceneCell(u.transform, u.CurrentCellPosition);
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
        SetAnchorFromEnemies(activeTeam, repaint: true);
    }

    private void SetAnchorFromEnemies(TeamId activeTeam, bool repaint)
    {
        UnitManager[] all = FindObjectsByType<UnitManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Vector3 acc = Vector3.zero;
        int n = 0;
        foreach (UnitManager unit in all)
        {
            if (unit == null || unit.TeamId == activeTeam || unit.TeamId == TeamId.Neutral
                || unit.IsDead || unit.IsEmbarked)
                continue;

            Vector3Int cell = ResolveSceneCell(unit.transform, unit.CurrentCellPosition);
            acc += new Vector3(cell.x, cell.y, 0);
            n++;
        }

        if (n > 0)
        {
            acc /= n;
            anchorHex = new Vector3Int(Mathf.RoundToInt(acc.x), Mathf.RoundToInt(acc.y), 0);
            statusMessage = string.Empty;
            if (repaint)
                Repaint();
            return;
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        ConstructionManager enemyHq = null;
        foreach (ConstructionManager construction in constructions)
        {
            if (construction == null || !construction.IsPlayerHeadQuarter
                || construction.TeamId == activeTeam || construction.TeamId == TeamId.Neutral)
                continue;
            enemyHq = construction;
            break;
        }

        if (enemyHq != null)
        {
            anchorHex = ResolveSceneCell(enemyHq.transform, enemyHq.CurrentCellPosition);
            statusMessage = string.Empty;
            if (repaint)
                Repaint();
            return;
        }

        statusMessage = "Nenhuma unidade ou HQ inimigo na cena para inferir a frente.";
        if (repaint)
            Repaint();
    }

    private void ClearResult()
    {
        hasResult = false;
        combatantCells.Clear();
        frontBandCells.Clear();
        lineHeadCells.Clear();
        isolatedAdvanceCells.Clear();
        enemyCells.Clear();
        neutralBandCells.Clear();
        spottingCells.Clear();
        spottingConeCells.Clear();
        spotReleaseFrontCells.Clear();
        spotReleaseCoveredCells.Clear();
        selectedSpotCanBeReleased = false;
        rearScoreMap = null;
        vanguardCells = null;
        hasBestRear = false;
        hasEvaluation = false;
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


            ConstructionManager construction =
                Selection.activeGameObject.GetComponent<ConstructionManager>();
            if (construction != null && construction.TeamId != TeamId.Neutral)
            {
                label = $"{construction.name} -> {construction.TeamId}";
                return construction.TeamId;
            }
        }

        label = $"(nenhuma unidade selecionada) -> {team}";
        return team;
    }

    private Tilemap ResolveBoardTilemap()
    {
        if (Selection.activeGameObject != null)
        {
            UnitManager selectedUnit = Selection.activeGameObject.GetComponent<UnitManager>();
            if (selectedUnit != null && selectedUnit.BoardTilemap != null)
                return selectedUnit.BoardTilemap;

            ConstructionManager selectedConstruction =
                Selection.activeGameObject.GetComponent<ConstructionManager>();
            if (selectedConstruction != null && selectedConstruction.BoardTilemap != null)
                return selectedConstruction.BoardTilemap;
        }

        UnitManager[] units = FindObjectsByType<UnitManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UnitManager unit in units)
            if (unit != null && unit.BoardTilemap != null)
                return unit.BoardTilemap;

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ConstructionManager construction in constructions)
            if (construction != null && construction.BoardTilemap != null)
                return construction.BoardTilemap;

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Tilemap candidate in tilemaps)
            if (candidate != null
                && candidate.name.IndexOf("ameaca", System.StringComparison.OrdinalIgnoreCase) < 0
                && candidate.name.IndexOf("threat", System.StringComparison.OrdinalIgnoreCase) < 0)
                return candidate;

        return null;
    }

    private void BuildFrontlineHead(Vector3Int anchor)
    {
        lineHeadCells.Clear();
        if (combatantCells.Count == 0 || tilemap == null)
            return;

        Vector2 centroid = Vector2.zero;
        foreach (Vector3Int cell in combatantCells)
            centroid += (Vector2)tilemap.GetCellCenterWorld(cell);
        centroid /= combatantCells.Count;

        Vector2 forward = (Vector2)tilemap.GetCellCenterWorld(anchor) - centroid;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector2.up;
        else
            forward.Normalize();
        Vector2 lateral = new Vector2(-forward.y, forward.x);

        float hexStep = Vector2.Distance(
            tilemap.GetCellCenterWorld(combatantCells[0]),
            tilemap.GetCellCenterWorld(combatantCells[0] + Vector3Int.right));
        if (hexStep <= 0.0001f)
            hexStep = 1f;

        var ordered = new List<Vector3Int>(combatantCells);
        ordered.Sort((a, b) => GetFrontProjection(b, forward, hexStep)
            .CompareTo(GetFrontProjection(a, forward, hexStep)));

        foreach (Vector3Int leader in ordered)
        {
            float leaderDepth = GetFrontProjection(leader, forward, hexStep);
            var band = new List<Vector3Int>();
            foreach (Vector3Int candidate in combatantCells)
            {
                float depth = GetFrontProjection(candidate, forward, hexStep);
                if (depth <= leaderDepth + 0.05f
                    && depth >= leaderDepth - frontlineDepthTolerance)
                    band.Add(candidate);
            }

            if (band.Count < 2)
                continue;
            lineHeadCells.AddRange(band);
            break;
        }

        if (lineHeadCells.Count == 0)
            lineHeadCells.Add(ordered[0]);

        lineHeadCells.Sort((a, b) => GetLateralProjection(a, lateral, hexStep)
            .CompareTo(GetLateralProjection(b, lateral, hexStep)));
    }

    private float GetFrontProjection(Vector3Int cell, Vector2 forward, float hexStep)
    {
        return Vector2.Dot((Vector2)tilemap.GetCellCenterWorld(cell), forward) / hexStep;
    }

    private float GetLateralProjection(Vector3Int cell, Vector2 lateral, float hexStep)
    {
        return Vector2.Dot((Vector2)tilemap.GetCellCenterWorld(cell), lateral) / hexStep;
    }

    private void BuildIsolatedAdvanceCells(
        AIBacklineResult result,
        AIBacklineSettings settings,
        Vector3Int anchor,
        IReadOnlyList<Vector3Int> geometryCombatants)
    {
        isolatedAdvanceCells.Clear();
        if (result == null || geometryCombatants == null)
            return;

        var lineSet = new HashSet<Vector3Int>(lineHeadCells);
        foreach (Vector3Int cell in combatantCells)
        {
            if (lineSet.Contains(cell))
                continue;
            AIBacklineScore score = AIBacklineAnalyzer.ScoreCell(
                geometryCombatants, enemyCells, cell, anchor, settings, result);
            if (score.IsVanguard)
                isolatedAdvanceCells.Add(cell);
        }
    }

    private void BuildNeutralBand(
        AIBacklineResult result,
        AIBacklineSettings settings,
        Vector3Int anchor,
        IReadOnlyList<Vector3Int> geometryCombatants)
    {
        neutralBandCells.Clear();
        if (result == null || !result.Success)
            return;

        var occupied = new HashSet<Vector3Int>(combatantCells);
        for (int dx = -settings.PaintRadius; dx <= settings.PaintRadius; dx++)
        for (int dy = -settings.PaintRadius; dy <= settings.PaintRadius; dy++)
        {
            Vector3Int cell = new Vector3Int(result.Centroid.x + dx, result.Centroid.y + dy, 0);
            if (SectorManager.HexDistance(cell, result.Centroid) > settings.PaintRadius
                || cell == anchor || occupied.Contains(cell))
                continue;

            AIBacklineScore score = AIBacklineAnalyzer.ScoreCell(
                geometryCombatants, enemyCells, cell, anchor, settings, result);
            if (!score.IsVanguard && !score.InRearSlice)
                neutralBandCells.Add(cell);
        }
    }

    private void BuildSpottingGeometry(Vector3Int anchor)
    {
        spottingCells.Clear();
        spottingConeCells.Clear();
        spotReleaseFrontCells.Clear();
        spotReleaseCoveredCells.Clear();
        selectedSpotCanBeReleased = false;
        if (tilemap == null)
            return;

        ConstructionManager[] constructions =
            FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ConstructionManager construction in constructions)
        {
            if (construction == null || !construction.IsForwardObserverSpot)
                continue;

            Vector3Int spot = ResolveSceneCell(construction.transform, construction.CurrentCellPosition);
            spottingCells.Add(spot);
            if (construction != selectedSpot)
                continue;

            UnitManager observer = FindUnitAtCell(spot);
            if (observer == null)
            {
                statusMessage = "Coloque uma unidade no spot selecionado para calcular a visao real.";
                continue;
            }

            Vector3Int spottingAnchor = ResolveEnemyHqAnchor(observer.TeamId, anchor);
            PodeDetectarSensor.CollectVisibleCellsForFogOfWar(
                observer,
                tilemap,
                terrainDatabase,
                spottingConeCells,
                dpqAirHeightConfig,
                enableLosValidation: true);

            Vector2 spotWorld = tilemap.GetCellCenterWorld(spot);
            Vector2 anchorWorld = tilemap.GetCellCenterWorld(spottingAnchor);
            Vector2 forward = anchorWorld - spotWorld;
            if (forward.sqrMagnitude <= 0.0001f)
                continue;
            forward.Normalize();

            foreach (Vector3Int cell in spottingConeCells)
            {
                Vector2 relative = (Vector2)tilemap.GetCellCenterWorld(cell) - spotWorld;
                if (Vector2.Dot(relative, forward) <= 0.05f)
                    continue;

                bool hasUnseenCellAhead = false;
                for (int dx = -1; dx <= 1 && !hasUnseenCellAhead; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector3Int neighbour = cell + new Vector3Int(dx, dy, 0);
                    if (SectorManager.HexDistance(cell, neighbour) != 1
                        || spottingConeCells.Contains(neighbour))
                        continue;

                    Vector2 step = (Vector2)tilemap.GetCellCenterWorld(neighbour)
                        - (Vector2)tilemap.GetCellCenterWorld(cell);
                    if (Vector2.Dot(step, forward) > 0.05f)
                    {
                        hasUnseenCellAhead = true;
                        break;
                    }
                }

                if (hasUnseenCellAhead)
                    spotReleaseFrontCells.Add(cell);
            }

            MarkCoveredSpotReleaseCells(observer.TeamId, observer);
        }
    }

    private UnitManager FindUnitAtCell(Vector3Int cell)
    {
        UnitManager[] units = FindObjectsByType<UnitManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UnitManager unit in units)
            if (unit != null && !unit.IsDead && !unit.IsEmbarked
                && ResolveSceneCell(unit.transform, unit.CurrentCellPosition) == cell)
                return unit;
        return null;
    }

    private Vector3Int ResolveEnemyHqAnchor(TeamId observerTeam, Vector3Int fallback)
    {
        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ConstructionManager construction in constructions)
        {
            if (construction == null || !construction.IsPlayerHeadQuarter
                || construction.TeamId == TeamId.Neutral || construction.TeamId == observerTeam)
                continue;
            return ResolveSceneCell(construction.transform, construction.CurrentCellPosition);
        }

        fallback.z = 0;
        return fallback;
    }

    private void MarkCoveredSpotReleaseCells(TeamId observerTeam, UnitManager observer)
    {
        UnitManager[] units = FindObjectsByType<UnitManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UnitManager unit in units)
        {
            if (unit == null || unit == observer || unit.TeamId != observerTeam
                || unit.IsDead || unit.IsEmbarked || unit.GetDomain() != Domain.Land
                || !IsFrontlineCombatant(unit))
                continue;

            Vector3Int cell = ResolveSceneCell(unit.transform, unit.CurrentCellPosition);
            if (spotReleaseFrontCells.Contains(cell))
                spotReleaseCoveredCells.Add(cell);
        }

        selectedSpotCanBeReleased = spotReleaseCoveredCells.Count > 0;
    }

    private Vector3Int ResolveSceneCell(Transform source, Vector3Int fallback)
    {
        if (!Application.isPlaying && tilemap != null && source != null)
        {
            Vector3Int sceneCell = tilemap.WorldToCell(source.position);
            sceneCell.z = 0;
            return sceneCell;
        }

        fallback.z = 0;
        return fallback;
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

        if (pickingAnchor || pickingAlliedPoint || pickingEvaluation || pickingSpot)
            DrawPicker();
    }

    private void DrawResult(GUIStyle labelStyle)
    {
        if (showRear && rearScoreMap != null)
        {
            foreach (KeyValuePair<Vector3Int, float> kv in rearScoreMap)
            {
                float t = Mathf.Clamp01(kv.Value / maxRearScore);
                Handles.color = Color.Lerp(new Color(0.35f, 0.7f, 0.35f, 0.45f), new Color(0f, 0.85f, 0.1f, 0.85f), t);
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(kv.Key), Vector3.back, 0.22f);
            }
        }

        if (showVanguard && vanguardCells != null)
        {
            Handles.color = new Color(0.9f, 0.15f, 0.1f, 0.45f);
            foreach (Vector3Int cell in vanguardCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.20f);
        }

        if (showNeutralBand)
        {
            Handles.color = new Color(1f, 0.75f, 0.05f, 0.45f);
            foreach (Vector3Int cell in neutralBandCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.20f);
        }

        if (showSpottingCone)
        {
            Handles.color = new Color(0f, 0.85f, 1f, 0.34f);
            foreach (Vector3Int cell in spottingConeCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.21f);
        }

        if (showSpotReleaseFront)
        {
            Handles.color = new Color(0.45f, 1f, 0f, 0.95f);
            foreach (Vector3Int cell in spotReleaseFrontCells)
                Handles.DrawWireDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.29f);

            Handles.color = new Color(1f, 0.85f, 0f, 1f);
            foreach (Vector3Int cell in spotReleaseCoveredCells)
            {
                Vector3 world = tilemap.GetCellCenterWorld(cell);
                Handles.DrawSolidDisc(world, Vector3.back, 0.27f);
                Handles.Label(world + Vector3.up * 0.4f, "COBERTO", labelStyle);
            }
        }

        if (showFrontBand)
        {
            Handles.color = new Color(1f, 0.1f, 0.1f, 0.95f);
            foreach (Vector3Int cell in frontBandCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.30f);
        }

        if (showFrontlineUnits)
        {
            Handles.color = new Color(0.7f, 0.25f, 0.25f, 0.7f);
            foreach (Vector3Int cell in combatantCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.26f);

            Handles.color = new Color(1f, 0.1f, 0.1f, 0.95f);
            if (lineHeadCells.Count >= 2)
            {
                var linePoints = new Vector3[lineHeadCells.Count];
                for (int i = 0; i < lineHeadCells.Count; i++)
                    linePoints[i] = tilemap.GetCellCenterWorld(lineHeadCells[i]);
                Handles.DrawAAPolyLine(6f, linePoints);
            }

            Handles.color = new Color(1f, 0.45f, 0f, 1f);
            foreach (Vector3Int cell in isolatedAdvanceCells)
            {
                Vector3 world = tilemap.GetCellCenterWorld(cell);
                Handles.DrawWireDisc(world, Vector3.back, 0.37f);
                Handles.Label(world + Vector3.up * 0.45f, "AVANCO ISOLADO", labelStyle);
            }
        }

        if (showRear && hasBestRear)
        {
            Handles.color = new Color(0.1f, 1f, 0.2f, 1f);
            Vector3 bestWorld = tilemap.GetCellCenterWorld(bestRearCell);
            Handles.DrawWireDisc(bestWorld, Vector3.back, 0.36f);
            Handles.DrawWireDisc(bestWorld, Vector3.back, 0.30f);
            Handles.Label(bestWorld + Vector3.up * 0.42f, "RET", labelStyle);
        }

        if (showSpottingPoints)
        {
            Handles.color = new Color(0f, 1f, 1f, 1f);
            foreach (Vector3Int cell in spottingCells)
            {
                Vector3 world = tilemap.GetCellCenterWorld(cell);
                Handles.DrawWireDisc(world, Vector3.back, 0.34f);
                Handles.Label(world + Vector3.up * 0.42f, "SPOT", labelStyle);
            }
        }

        if (showEnemies)
        {
            Handles.color = new Color(0.55f, 0f, 0.55f, 0.95f);
            foreach (Vector3Int cell in enemyCells)
                Handles.DrawSolidDisc(tilemap.GetCellCenterWorld(cell), Vector3.back, 0.24f);
        }

        if (showObjective)
        {
            Handles.color = new Color(1f, 0.85f, 0f, 0.95f);
            Vector3 anchorWorld = tilemap.GetCellCenterWorld(anchorHex);
            Handles.DrawSolidDisc(anchorWorld, Vector3.back, 0.32f);
            Handles.Label(anchorWorld + Vector3.up * 0.42f,
                dynamicEnemyMassAnchor ? "MASSA" : "REF", labelStyle);
        }

        if (hasEvaluation)
        {
            Handles.color = evaluationScore.IsVanguard
                ? Color.red
                : evaluationScore.InRearSlice
                    ? Color.green
                    : Color.yellow;
            Vector3 evaluationWorld = tilemap.GetCellCenterWorld(evaluationHex);
            Handles.DrawWireDisc(evaluationWorld, Vector3.back, 0.42f);
            Handles.DrawWireDisc(evaluationWorld, Vector3.back, 0.34f);
            Handles.Label(evaluationWorld + Vector3.up * 0.5f,
                "INVESTIGADO", labelStyle);
        }
    }

    private void DrawPicker()
    {
        Handles.color = pickingSpot
            ? Color.cyan
            : pickingEvaluation
                ? Color.yellow
                : Color.red;
        Vector3 hoverWorld = tilemap.GetCellCenterWorld(hoverCell);
        Handles.DrawWireDisc(hoverWorld, Vector3.back, 0.35f);
        Handles.Label(hoverWorld + Vector3.up * 0.42f,
            (pickingSpot
                ? "Spot "
                : pickingAlliedPoint
                    ? "Aliado "
                    : pickingEvaluation
                        ? "Investigar "
                    : "Referencia ") + hoverCell,
            new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = pickingSpot ? Color.cyan : Color.red }
            });
    }

    private void HandlePickingInput()
    {
        if (!pickingAnchor && !pickingAlliedPoint && !pickingEvaluation && !pickingSpot)
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
            if (pickingSpot)
                selectedSpot = FindSpotAtCell(picked);
            else if (pickingAlliedPoint)
                manualAlliedPoints.Add(picked);
            else if (pickingEvaluation)
            {
                evaluationUnit = null;
                evaluationHex = picked;
                hasEvaluationLocation = true;
            }
            else
                anchorHex = picked;
            pickingAnchor = false;
            pickingAlliedPoint = false;
            pickingEvaluation = false;
            pickingSpot = false;
            e.Use();
            Repaint();
        }

        if ((e.type == EventType.MouseDown && e.button == 1)
            || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
        {
            pickingAnchor = false;
            pickingAlliedPoint = false;
            pickingEvaluation = false;
            pickingSpot = false;
            e.Use();
            Repaint();
        }

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    private ConstructionManager FindSpotAtCell(Vector3Int cell)
    {
        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ConstructionManager construction in constructions)
        {
            if (construction != null && construction.IsForwardObserverSpot
                && ResolveSceneCell(construction.transform, construction.CurrentCellPosition) == cell)
                return construction;
        }

        statusMessage = $"O hex {cell} nao possui um Forward Observer Spot.";
        return null;
    }

    private static T FindFirstAsset<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private Vector3Int ScreenToCell(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        float t = ray.direction.z != 0f ? -ray.origin.z / ray.direction.z : 0f;
        return tilemap.WorldToCell(ray.origin + ray.direction * t);
    }
}
