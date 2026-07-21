using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tools > Utils > Shopping Pressure
///
/// Janela de runtime para inspecionar, em tempo real, o que o shopping por papéis está
/// "vendo": a coluna esquerda mostra os objetivos do plano e seus slots demandados vs.
/// preenchidos; a coluna direita mostra a fila de pressão de compra (AIShoppingDemand),
/// equivalente ao bloco "[AI Shopping Roles] fila unica" do log.
///
/// Só funciona em Play mode (precisa do plano, do snapshot e do AITacticalAnalyzer vivos).
/// </summary>
public class ShoppingPressureWindow : EditorWindow
{
    [SerializeField] private Tilemap boardTilemap;   // recebido se precisar (overlay futuro)
    [SerializeField] private TeamId  team = TeamId.Green;
    [SerializeField] private bool    followActiveTeam = true;

    private MatchController _matchController;
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;

    // A reconstrução do snapshot dispara GetDeficits, que loga no console. Por isso a
    // coluna de pressão é recomputada no máximo a cada RefreshInterval (ou no botão),
    // enquanto a coluna de objetivos lê o plano vivo a cada repaint (silencioso).
    private const double RefreshInterval = 1.0;
    private List<AIShoppingDemand> _demands = new List<AIShoppingDemand>();
    private AIWorldSnapshot _snapshot;
    private TeamId _snapshotTeam = TeamId.Neutral;
    private double _lastBuild = -999;

    private GUIStyle _header;
    private GUIStyle _objTitle;
    private GUIStyle _demandTitle;
    private GUIStyle _subtle;
    private GUIStyle _boldFoldout;
    private bool _stylesReady;
    private bool _showCounterPressure = true;
    private bool _showOperationalPressure = true;
    private bool _showBestCounters = true;
    private bool _showEligibleQueue = true;
    private bool _showAxisOverview = true;
    private readonly Dictionary<string, bool> _counterCategoryFoldouts = new Dictionary<string, bool>();

    [MenuItem("Tools/Utils/Shopping Pressure")]
    public static void OpenWindow()
    {
        ShoppingPressureWindow w = GetWindow<ShoppingPressureWindow>("Shopping Pressure");
        w.minSize = new Vector2(560f, 320f);
    }

    private void OnEnable() => AutoDetectContext();

    // Repaint contínuo (~10Hz) para refletir o estado vivo.
    private void OnInspectorUpdate() => Repaint();

    private void AutoDetectContext()
    {
        if (_matchController == null)
            _matchController = Object.FindAnyObjectByType<MatchController>();

        if (boardTilemap == null)
        {
            Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Tilemap m in maps)
            {
                if (m != null && string.Equals(m.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                { boardTilemap = m; break; }
            }
            if (boardTilemap == null && maps.Length > 0)
                boardTilemap = maps[0];
        }
    }

    private void EnsureStyles()
    {
        if (_stylesReady) return;
        _header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        _objTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        _demandTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        _subtle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        _subtle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        _boldFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        _stylesReady = true;
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (_matchController == null)
            AutoDetectContext();

        DrawToolbar();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Entre em Play mode: a tela precisa do plano, do snapshot e do AITacticalAnalyzer vivos.", MessageType.Info);
            return;
        }
        if (_matchController == null)
        {
            EditorGUILayout.HelpBox("MatchController não encontrado na cena.", MessageType.Warning);
            return;
        }

        if (followActiveTeam)
            team = _matchController.ActiveTeam;

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(team);   // leitura viva, silenciosa
        RebuildPressureIfStale(force: false);

        EditorGUILayout.BeginHorizontal();

        // Coluna esquerda: visão tática do time + pressões de counter e operacional.
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f - 8f));
        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
        DrawHeader(_snapshot, plan, _demands);
        DrawCounterPressure(_snapshot, _demands);
        DrawOperationalPressure(_snapshot);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Coluna direita: objetivos do plano + fila de pressão no shopping.
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f - 8f));
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
        DrawAxisOverview(_snapshot);
        DrawObjectivesColumn(plan);
        DrawPressureColumn(_snapshot, _demands);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void RebuildPressureIfStale(bool force)
    {
        bool teamChanged = _snapshotTeam != team;
        bool stale = EditorApplication.timeSinceStartup - _lastBuild >= RefreshInterval;
        if (!force && !teamChanged && !stale)
            return;

        _snapshot = AIWorldSnapshot.Build(team, _matchController);
        _demands = AIShoppingPlanner.InspectRoleDemands(_snapshot);
        _snapshotTeam = team;
        _lastBuild = EditorApplication.timeSinceStartup;
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        followActiveTeam = GUILayout.Toggle(followActiveTeam, "Seguir time da vez", EditorStyles.toolbarButton, GUILayout.Width(130f));
        using (new EditorGUI.DisabledScope(followActiveTeam))
            team = (TeamId)EditorGUILayout.EnumPopup(team, EditorStyles.toolbarPopup, GUILayout.Width(90f));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Atualizar", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            if (Application.isPlaying && _matchController != null)
                RebuildPressureIfStale(force: true);
            Repaint();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawHeader(AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AIShoppingDemand> demands)
    {
        if (snapshot == null) return;
        int totalDemand = 0;
        foreach (AIShoppingDemand d in demands) totalDemand += Mathf.Max(0, d.Count);
        int objCount = plan != null ? plan.Objectives.Count : 0;

        // Invasão é um macro-estado sobreposto à postura ofensiva (não um valor de AIStance — isso
        // quebraria as dezenas de gates "== Offensive" que dirigem ar/fogo/compra). A flag vem do
        // snapshot (snapshot.IsInvading); os setores/turno vêm da inspeção para detalhar o bloco.
        AIController.GoGreenInvasionInspection invasion =
            AIController.GetGoGreenInvasionForInspection(team, snapshot.TurnNumber);
        string stanceLabel = snapshot.IsInvading ? "Invasão" : snapshot.Stance.ToString();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"[{team}] T{snapshot.TurnNumber}  •  stance={stanceLabel}  •  budget={snapshot.Budget}  •  renda={snapshot.IncomePerTurn}",
            _header);
        EditorGUILayout.LabelField($"objetivos={objCount}  •  demandas na fila={demands.Count}  •  unidades pedidas={totalDemand}", _subtle);

        // Visão macro-territorial da AI: setores seus/inimigos/neutros + como ela classifica a
        // partida (perdendo/empate/ganhando). Mesma fonte do log [AI Macro] (BuildMacroTerritoryContext).
        AIController.MacroTerritoryInspection macro = AIController.GetMacroTerritoryForInspection(team);
        Color prevColor = GUI.color;
        GUI.color = macro.Losing
            ? new Color(1f, 0.55f, 0.55f)
            : macro.Winning ? new Color(0.55f, 0.9f, 0.55f) : new Color(0.85f, 0.82f, 0.55f);
        EditorGUILayout.LabelField(
            $"visão da AI: {macro.PhaseLabel}  ·  controle {macro.OwnedRatio:P0}", EditorStyles.boldLabel);
        GUI.color = prevColor;
        EditorGUILayout.LabelField(
            $"setores: {macro.OwnedSectors} seus / {macro.EnemySectors} inimigos / {macro.NeutralSectors} neutros  (total {macro.TotalSectors})",
            _subtle);
        if (macro.DisputedControlPoints > 0)
        {
            EditorGUILayout.LabelField(
                $"capture points: {macro.OwnedControlPoints} seus / "
                + $"{macro.EnemyControlPoints} inimigos / "
                + $"{macro.DisputedControlPoints} em disputa",
                _subtle);
        }
        string enemyForceTxt = macro.EnemyProducersProjected > 0
            ? $"{macro.EnemyForce} inimigas ({macro.EnemyForce - macro.EnemyProducersProjected} conhec + {macro.EnemyProducersProjected} projeção Hard)"
            : $"{macro.EnemyForce} inimigas conhecidas";
        EditorGUILayout.LabelField(
            $"força: {macro.OwnForce} suas / {enemyForceTxt}  ({macro.ForceRatio:P0})",
            _subtle);

        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField("ORDENS GERAIS", EditorStyles.boldLabel);
        DrawRecrutamentoForcado(macro);
        DrawEliteReserveOverride(snapshot, demands, macro.Losing);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("ELITE", EditorStyles.boldLabel);
        DrawEliteQualityStatus(
            snapshot,
            AIShoppingPlanner.InspectCounterPressure(snapshot),
            demands);
        DrawActiveEliteCommitment(snapshot);

        DrawGoGreenHeader(plan, invasion);

        EditorGUILayout.EndVertical();
    }

    private void DrawRecrutamentoForcado(AIController.MacroTerritoryInspection macro)
    {
        Color prev = GUI.color;
        if (macro.Losing)
        {
            GUI.color = new Color(1f, 0.6f, 0.3f);
            EditorGUILayout.LabelField("RECRUTAMENTO FORÇADO: ATIVO (Perdendo)", EditorStyles.boldLabel);
        }
        else
        {
            EditorGUILayout.LabelField("recrutamento: normal", _subtle);
        }
        GUI.color = prev;
    }

    private void DrawEliteReserveOverride(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        bool macroLosing)
    {
        AIElitePurchaseCommitment commitment = snapshot != null
            ? AIIntelLedger.GetElitePurchaseCommitment(snapshot.AITeam)
            : null;
        if (commitment == null || string.IsNullOrEmpty(commitment.unitId))
        {
            EditorGUILayout.LabelField("reserva elite: sem compromisso persistente", _subtle);
            return;
        }

        AIShoppingDemand emergency =
            AIShoppingPlanner.FindReserveBreakingEmergencyForInspection(demands);
        Color previous = GUI.color;
        if (macroLosing && emergency != null)
        {
            GUI.color = new Color(1f, 0.55f, 0.55f);
            EditorGUILayout.LabelField(
                $"RESERVA ELITE ROMPIDA: URGENTE pri={emergency.Priority} "
                    + $"{emergency.Role} x{emergency.Count} · origem={emergency.Origin}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"motivo: {emergency.Reason}", _subtle);
        }
        else
        {
            GUI.color = new Color(0.55f, 0.9f, 0.55f);
            EditorGUILayout.LabelField(
                $"RESERVA ELITE PRESERVADA: compromisso={commitment.unitId}"
                    + (emergency != null
                        ? " · urgente aberta, mas fora de Collapsing"
                        : " · nenhuma demanda Urgent"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "critical/counter/escalada não rompem a reserva sem flag Urgent",
                _subtle);
        }
        GUI.color = previous;
    }

    private void DrawGoGreenHeader(TeamObjectivePlan plan, AIController.GoGreenInvasionInspection invasion)
    {
        var rallies = new List<SectorObjective>();
        if (plan?.Objectives != null)
            foreach (SectorObjective objective in plan.Objectives)
                if (objective != null
                    && objective.ObjectiveType == AIObjectiveType.RallyAssembly
                    && objective.RallyState != AIRallyAssemblyState.Expired)
                    rallies.Add(objective);

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("GO GREEN", EditorStyles.boldLabel);
        if (rallies.Count == 0)
        {
            // Após o GoGreen o objetivo RallyAssembly é removido do plano (a massa marcha para a
            // base inimiga), então não há rally ativo — mas a invasão segue em andamento dentro da
            // janela de supressão. Reflete isso em vez de dizer "sem rally".
            if (invasion.Active)
            {
                string sectors = string.Join(", ", invasion.Sectors);
                EditorGUILayout.LabelField(
                    $"  ⚑ Invasão em Andamento: {sectors}"
                        + (invasion.SinceTurn >= 0 ? $"  ·  desde T{invasion.SinceTurn}" : ""),
                    _subtle);
                return;
            }

            EditorGUILayout.LabelField("  sem rally de invasão ativo", _subtle);
            return;
        }

        rallies.Sort((a, b) =>
        {
            int state = RallyStateRank(b.RallyState).CompareTo(RallyStateRank(a.RallyState));
            if (state != 0) return state;
            return a.Priority.CompareTo(b.Priority);
        });

        foreach (SectorObjective rally in rallies)
        {
            if (rally.RallyState == AIRallyAssemblyState.GoGreen)
            {
                EditorGUILayout.LabelField(
                    $"  ✓ {rally.Sector}: LIBERADO"
                        + (rally.RallyGoGreenTurn >= 0
                            ? $" desde T{rally.RallyGoGreenTurn}"
                            : ""),
                    _subtle);
                continue;
            }

            string missing = ExtractRallyReadinessValue(
                rally.RallyReadinessReason, "missing=");
            string force = ExtractRallyReadinessValue(
                rally.RallyReadinessReason, "force=");
            string detail = !string.IsNullOrEmpty(missing) && missing != "-"
                ? $"falta {missing}"
                : "requisitos de força completos";
            if (!string.IsNullOrEmpty(force))
                detail += $"  ·  força {force}";

            // Composição atual da massa — quem/quantos ja estao no rally.
            string cap = ExtractRallyReadinessValue(rally.RallyReadinessReason, "cap=");
            string rupt = ExtractRallyReadinessValue(rally.RallyReadinessReason, "ass=");
            string air = ExtractRallyReadinessValue(rally.RallyReadinessReason, "airAtk=");
            string art = ExtractRallyReadinessValue(rally.RallyReadinessReason, "artGlobal=");
            string composition = string.Join("  ", new[]
                {
                    !string.IsNullOrEmpty(cap) ? $"{cap} cap" : null,
                    !string.IsNullOrEmpty(rupt) ? $"ruptura {rupt}" : null,
                    !string.IsNullOrEmpty(air) && air != "0" ? $"{air} ar" : null,
                    !string.IsNullOrEmpty(art) ? $"art {art}" : null,
                }.Where(s => !string.IsNullOrEmpty(s)));

            EditorGUILayout.LabelField(
                $"  {rally.Sector}: {rally.RallyState}  ·  {detail}",
                _subtle);
            if (!string.IsNullOrEmpty(composition))
                EditorGUILayout.LabelField($"      massa: {composition}", _subtle);
        }
    }

    private static int RallyStateRank(AIRallyAssemblyState state)
    {
        switch (state)
        {
            case AIRallyAssemblyState.GoGreen: return 4;
            case AIRallyAssemblyState.Ready: return 3;
            case AIRallyAssemblyState.Assembling: return 2;
            case AIRallyAssemblyState.WaitHold: return 1;
            default: return 0;
        }
    }

    private static string ExtractRallyReadinessValue(string reason, string marker)
    {
        if (string.IsNullOrEmpty(reason) || string.IsNullOrEmpty(marker))
            return null;
        int start = reason.IndexOf(marker, System.StringComparison.Ordinal);
        if (start < 0)
            return null;
        start += marker.Length;
        int end = reason.IndexOf(' ', start);
        return end < 0
            ? reason.Substring(start)
            : reason.Substring(start, end - start);
    }

    private void DrawCounterPressure(AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        if (snapshot == null)
            return;

        _showCounterPressure = EditorGUILayout.Foldout(
            _showCounterPressure, "Counter pressure  (composição inimiga → arma adequada)", true);
        if (!_showCounterPressure)
            return;

        AIShoppingPlanner.CounterPressureInspection pressure =
            AIShoppingPlanner.InspectCounterPressure(snapshot);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"saldo: anti-infantaria {pressure.AntiInfantry:F1}  ·  anti-tank {pressure.AntiTank:F1}  ·  " +
            $"anti-aérea {pressure.AntiAir:F1}  ·  anti-navio {pressure.AntiShip:F1}",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"bruto/cobertura: anti-inf {pressure.RawAntiInfantry:F1}/{pressure.AntiInfantryCoverage:F1}  ·  " +
            $"anti-tank {pressure.RawAntiTank:F1}/{pressure.AntiTankCoverage:F1}  ·  " +
            $"AA {pressure.RawAntiAir:F1}/{pressure.AntiAirCoverage:F1}  ·  " +
            $"navio {pressure.RawAntiShip:F1}/{pressure.AntiShipCoverage:F1}",
            _subtle);
        float dominantScore = Mathf.Max(
            pressure.AntiInfantry, pressure.AntiTank, pressure.AntiAir, pressure.AntiShip);
        string dominantLabel = dominantScore > 0f
            ? pressure.DominantCategory.ToString()
            : "nenhuma";
        EditorGUILayout.LabelField(
            $"maior pressão: {dominantLabel}  ·  score pondera quantidade, elite, custo e HP atual",
            _subtle);
        EditorGUILayout.LabelField(
            $"fontes: {pressure.VisibleUnits} visíveis + {pressure.RememberedUnits} lembradas pelo ledger",
            _subtle);
        EditorGUILayout.LabelField(
            $"ledger subjetivo: sensor={pressure.SensorContacts} combate={pressure.CombatContacts} " +
            $"ameaças anônimas={pressure.AnonymousThreatSignals} (compras ocultas não entram)",
            _subtle);

        bool hasBreakdown = false;
        hasBreakdown |= DrawCounterCategoryBreakdown(
            "anti-infantaria", WeaponCategory.AntiInfantaria, pressure, demands);
        hasBreakdown |= DrawCounterCategoryBreakdown(
            "anti-tank", WeaponCategory.AntiTanque, pressure, demands);
        hasBreakdown |= DrawCounterCategoryBreakdown(
            "anti-aérea", WeaponCategory.AntiAerea, pressure, demands);
        hasBreakdown |= DrawCounterCategoryBreakdown(
            "anti-navio", WeaponCategory.AntiNavio, pressure, demands);
        if (!hasBreakdown)
            EditorGUILayout.LabelField("  — nenhuma pressão conhecida para detalhar —", _subtle);

        var offered = new List<UnitData>();
        if (snapshot.MyBuildings != null)
            foreach (ConstructionManager building in snapshot.MyBuildings)
            {
                if (building?.OfferedUnits == null) continue;
                foreach (UnitData unit in building.OfferedUnits)
                    if (unit != null && !offered.Contains(unit)
                        && AIShoppingPlanner.InspectCounterFit(unit, pressure) > 0f)
                        offered.Add(unit);
            }

        offered.Sort((a, b) =>
            AIShoppingPlanner.InspectCounterFit(b, pressure)
                .CompareTo(AIShoppingPlanner.InspectCounterFit(a, pressure)));

        if (offered.Count > 0)
        {
            EditorGUILayout.Space(2f);
            _showBestCounters = EditorGUILayout.Foldout(
                _showBestCounters, "melhores counters disponíveis", true, _boldFoldout);
            if (_showBestCounters)
            {
                int count = Mathf.Min(8, offered.Count);
                for (int i = 0; i < count; i++)
                {
                    UnitData unit = offered[i];
                    float fit = AIShoppingPlanner.InspectCounterFit(unit, pressure);
                    bool eligible = AIShoppingPlanner.InspectPurchaseEligibility(
                        snapshot, unit, demands, out string reason);
                    EditorGUILayout.LabelField(
                        $"  {i + 1}. {unit.displayName}  fit={fit:F1}  elite={unit.eliteLevel}  ${unit.cost}" +
                        (eligible ? "  [elegível]" : $"  — {reason}"),
                        _subtle);
                }
            }

            var eligibleOffers = new List<UnitData>();
            foreach (UnitData unit in offered)
                if (AIShoppingPlanner.InspectPurchaseEligibility(
                    snapshot, unit, demands, out _))
                    eligibleOffers.Add(unit);

            EditorGUILayout.Space(2f);
            _showEligibleQueue = EditorGUILayout.Foldout(
                _showEligibleQueue, "elegíveis para a fila agora", true, _boldFoldout);
            if (_showEligibleQueue)
            {
                if (eligibleOffers.Count == 0)
                    EditorGUILayout.LabelField("  — nenhum counter elegível —", _subtle);
                else
                    for (int i = 0; i < Mathf.Min(5, eligibleOffers.Count); i++)
                    {
                        UnitData unit = eligibleOffers[i];
                        float fit = AIShoppingPlanner.InspectCounterFit(unit, pressure);
                        EditorGUILayout.LabelField(
                            $"  {i + 1}. {unit.displayName}  fit={fit:F1}  elite={unit.eliteLevel}  ${unit.cost}",
                            _subtle);
                    }
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawEliteQualityStatus(
        AIWorldSnapshot snapshot,
        AIShoppingPlanner.CounterPressureInspection pressure,
        List<AIShoppingDemand> demands)
    {
        bool covered = pressure.AntiTank <= 0.05f
            && pressure.AntiInfantry <= 0.05f;
        // Razões agora vêm do AI Manager (AIController), com par próprio por modo (normal/hard).
        float pressureRatio = AIController.Instance != null
            ? Mathf.Clamp01(AIController.Instance.EliteRatioPressure)
            : 0.33f;
        float safeRatio = AIController.Instance != null
            ? Mathf.Clamp01(AIController.Instance.EliteRatioSafe)
            : 0.5f;
        bool rallyAssemblyActive = snapshot != null
            && AIShoppingPlanner.HasActiveRallyAssembly(snapshot.AITeam);
        float ratio = covered || rallyAssemblyActive
            ? Mathf.Max(pressureRatio, safeRatio)
            : pressureRatio;

        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField(
            $"Superioridade qualitativa  ·  meta elite={ratio:P0}  ·  "
                + (rallyAssemblyActive ? "rally em montagem"
                    : covered ? "pressões terrestres cobertas" : "pressão terrestre aberta"),
            EditorStyles.boldLabel);
        DrawEliteQualityRole(snapshot, demands, UnitRole.Assalto, ratio);
        DrawEliteQualityRole(snapshot, demands, UnitRole.FogoIndireto, ratio);
    }

    private void DrawEliteQualityRole(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        UnitRole role,
        float ratio)
    {
        int total = 0;
        int elites = 0;
        if (snapshot?.MyUnits != null)
            foreach (UnitManager unit in snapshot.MyUnits)
                if (unit != null && !unit.IsDead
                    && unit.TryGetUnitData(out UnitData data) && data != null
                    && UnitRoleCompatibility.ResolveCompositionRole(data) == role)
                {
                    total++;
                    if (data.eliteLevel > 0) elites++;
                }
        int target = Mathf.CeilToInt(total * ratio);

        AIShoppingDemand queued = null;
        if (demands != null)
            foreach (AIShoppingDemand demand in demands)
                if (demand != null && demand.Count > 0 && demand.Role == role
                    && demand.MinEliteLevel > 0)
                {
                    queued = demand;
                    if (demand.Origin != null && demand.Origin.Contains("elite-quality"))
                        break;
                }

        string state;
        if (elites >= target)
            state = "meta satisfeita";
        else if (queued != null)
            state = $"fila={queued.Origin} x{queued.Count} tier≥{queued.MinEliteLevel}";
        else
            state = "sem demanda agora (core/economia/cadeia)";

        EditorGUILayout.LabelField(
            $"  {role}: elites={elites}/{target}  força do papel={total}  {state}",
            _subtle);
    }

    private bool DrawCounterCategoryBreakdown(
        string label,
        WeaponCategory category,
        AIShoppingPlanner.CounterPressureInspection pressure,
        List<AIShoppingDemand> demands)
    {
        float raw = GetRawCounterPressure(pressure, category);
        float coverage = GetCounterCoverage(pressure, category);
        bool hasClass = false;
        foreach (AIShoppingPlanner.EnemyClassPressureInspection entry in pressure.Classes)
            if (entry.CounterCategory == category)
            {
                hasClass = true;
                break;
            }
        if (!hasClass && raw <= 0f && coverage <= 0f)
            return false;

        EditorGUILayout.Space(3f);
        if (!_counterCategoryFoldouts.TryGetValue(label, out bool expanded))
            expanded = true;
        expanded = EditorGUILayout.Foldout(
            expanded,
            $"{label}: bruto={raw:F1}  cobertura={coverage:F1}  saldo={Mathf.Max(0f, raw - coverage):F1}",
            true,
            _boldFoldout);
        _counterCategoryFoldouts[label] = expanded;
        if (!expanded)
            return true;

        foreach (AIShoppingPlanner.EnemyClassPressureInspection entry in pressure.Classes)
        {
            if (entry.CounterCategory != category)
                continue;
            float share = raw > 0f ? entry.Score / raw : 0f;
            EditorGUILayout.LabelField(
                $"    {entry.UnitClass}: x{entry.Count} (vis {entry.VisibleCount} + mem {entry.RememberedCount})  " +
                $"peso={entry.Score:F1} ({share:P0})  coberto={entry.Coverage:F1}  saldo={entry.Unmet:F1}",
                _subtle);

            bool contributorFound = false;
            foreach (AIShoppingPlanner.OwnCounterContributionInspection own in pressure.OwnContributions)
            {
                if (own.Category != category || own.TargetClass != entry.UnitClass)
                    continue;
                contributorFound = true;
                EditorGUILayout.LabelField(
                    $"        ↳ cobre: {own.UnitName}#{own.UnitInstanceId}  elite={own.EliteLevel}  contribuição={own.Coverage:F1}",
                    _subtle);
            }
            if (!contributorFound && entry.Unmet > 0f)
                EditorGUILayout.LabelField("        ↳ cobertura própria: nenhuma", _subtle);

            AIShoppingDemand response = FindCounterDemand(demands, category, entry.UnitClass);
            if (response != null)
            {
                string escalation = response.StrategicEscalation
                    ? "ESCALADA ELITE SOLICITADA"
                    : response.Origin != null && response.Origin.Contains("prerequisite")
                        ? "PRÉ-REQUISITO ELITE"
                        : "resposta comum";
                EditorGUILayout.LabelField(
                    $"        ↳ fila: {escalation} x{response.Count}" +
                    (!string.IsNullOrEmpty(response.RequiredUnitId)
                        ? $"  alvo={response.RequiredUnitId}"
                        : ""),
                    _subtle);
            }
            else if (entry.Unmet <= 0f)
                EditorGUILayout.LabelField("        ↳ fila: satisfeita", _subtle);
        }
        return true;
    }

    private void DrawActiveEliteCommitment(AIWorldSnapshot snapshot)
    {
        AIElitePurchaseCommitment commitment =
            AIIntelLedger.GetElitePurchaseCommitment(snapshot.AITeam);
        if (commitment == null || string.IsNullOrEmpty(commitment.unitId))
        {
            EditorGUILayout.LabelField("compromisso persistente: nenhum", _subtle);
            return;
        }

        UnitData target = FindUnitData(snapshot, commitment.unitId);
        string targetName = target != null && !string.IsNullOrEmpty(target.displayName)
            ? target.displayName
            : commitment.unitId;
        string counter = commitment.counterEscalation
            ? $"  ·  counter={commitment.counterCategory}"
                + (commitment.counterHasTargetClass
                    ? $"→{commitment.counterTargetClass}"
                    : "")
            : "";

        EditorGUILayout.LabelField(
            $"COMPROMISSO PERSISTENTE ATIVO: {targetName} [{commitment.unitId}]  "
                + $"elite={commitment.eliteLevel}  custo=${commitment.targetCost}  "
                + $"desde T{commitment.committedTurn}{counter}",
            EditorStyles.boldLabel);
    }

    private static UnitData FindUnitData(AIWorldSnapshot snapshot, string unitId)
    {
        if (snapshot == null || string.IsNullOrEmpty(unitId))
            return null;

        if (snapshot.MyBuildings != null)
            foreach (ConstructionManager building in snapshot.MyBuildings)
            {
                if (building?.OfferedUnits == null) continue;
                foreach (UnitData data in building.OfferedUnits)
                    if (data != null && string.Equals(
                        data.id, unitId, System.StringComparison.Ordinal))
                        return data;
            }

        if (snapshot.MyUnits != null)
            foreach (UnitManager unit in snapshot.MyUnits)
                if (unit != null && unit.TryGetUnitData(out UnitData data)
                    && data != null && string.Equals(
                        data.id, unitId, System.StringComparison.Ordinal))
                    return data;

        return null;
    }

    private static AIShoppingDemand FindCounterDemand(
        List<AIShoppingDemand> demands,
        WeaponCategory category,
        GameUnitClass targetClass)
    {
        if (demands == null)
            return null;
        foreach (AIShoppingDemand demand in demands)
            if (demand != null && demand.Count > 0
                && demand.RequiredWeaponCategory == category
                && demand.TargetClass == targetClass
                && demand.Origin != null
                && demand.Origin.Contains("counter-pressure"))
                return demand;
        return null;
    }

    private static float GetRawCounterPressure(
        AIShoppingPlanner.CounterPressureInspection pressure,
        WeaponCategory category)
    {
        switch (category)
        {
            case WeaponCategory.AntiInfantaria: return pressure.RawAntiInfantry;
            case WeaponCategory.AntiTanque: return pressure.RawAntiTank;
            case WeaponCategory.AntiAerea: return pressure.RawAntiAir;
            case WeaponCategory.AntiNavio: return pressure.RawAntiShip;
            default: return 0f;
        }
    }

    private static float GetCounterCoverage(
        AIShoppingPlanner.CounterPressureInspection pressure,
        WeaponCategory category)
    {
        switch (category)
        {
            case WeaponCategory.AntiInfantaria: return pressure.AntiInfantryCoverage;
            case WeaponCategory.AntiTanque: return pressure.AntiTankCoverage;
            case WeaponCategory.AntiAerea: return pressure.AntiAirCoverage;
            case WeaponCategory.AntiNavio: return pressure.AntiShipCoverage;
            default: return 0f;
        }
    }

    private void DrawOperationalPressure(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        _showOperationalPressure = EditorGUILayout.Foldout(
            _showOperationalPressure,
            "Operational pressure  (eixos, transporte e desgaste logístico)",
            true);
        if (!_showOperationalPressure)
            return;

        AIShoppingPlanner.OperationalPressureInspection pressure =
            AIShoppingPlanner.InspectOperationalPressure(snapshot);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"transporte {pressure.Transport:F1}  ·  logística {pressure.Logistics:F1}",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"transportes: cobertura {pressure.ActiveTransports}/{pressure.DesiredTransports}  gap={pressure.TransportGap}",
            _subtle);

        foreach (AIShoppingPlanner.AxisTransportPressureInspection axis in pressure.Axes)
        {
            string axisLabel = axis.IsInvasionAxis ? $"eixo {axis.Eixo} >>" : $"eixo {axis.Eixo}";
            string axisProgress = axis.IsInvasionAxis
                ? $"campanha={axis.Advance:F1}/{axis.Total} ({axis.Progress:P0}) alvo={axis.Front} "
                : $"frente={axis.Front} rally={axis.Rally} avanço={axis.Advance:F1}/{axis.Total} ({axis.Progress:P0}) ";
            EditorGUILayout.LabelField(
                $"  {axisLabel}: {axisProgress}" +
                $"prof={axis.Depth:F1} força={axis.AssignedUnits} " +
                $"trans={axis.AssignedTransports}/{axis.DesiredTransports} " +
                $"score={axis.Score:F1}",
                _subtle);
        }

        EditorGUILayout.LabelField(
            $"logística: repair atual={pressure.CurrentRepairUnits} score={pressure.CurrentRepair:F1}  " +
            $"memória={pressure.RememberedRepairUnits} score={pressure.RememberedRepair:F1}  " +
            $"preventivo={pressure.Preventive:F1}",
            _subtle);
        EditorGUILayout.LabelField(
            $"logísticos: cobertura {pressure.ActiveLogistics}/{pressure.DesiredLogistics}  gap={pressure.LogisticsGap}",
            _subtle);
        EditorGUILayout.EndVertical();
    }

    // ----------------------------------------------------------------------------
    // Coluna 1: objetivos e slots demandados vs preenchidos
    // ----------------------------------------------------------------------------
    // Classifica o objetivo no "plano especial" a que pertence, pra agrupar e badge.
    private enum PlanKind { Invasion, Rally, Defend, Capture }

    private static PlanKind ClassifyObjective(SectorObjective obj)
    {
        if (obj.ObjectiveType == AIObjectiveType.InvasionAttack) return PlanKind.Invasion;
        if (obj.ObjectiveType == AIObjectiveType.RallyAssembly) return PlanKind.Rally;
        if (obj.Status == ObjectiveStatus.Defending) return PlanKind.Defend;
        return PlanKind.Capture;
    }

    private static string KindSymbol(PlanKind k)
    {
        switch (k)
        {
            case PlanKind.Invasion: return ">>";
            case PlanKind.Rally:    return "+";
            case PlanKind.Defend:   return "!";
            default:                return "•";
        }
    }

    private static string KindLabel(PlanKind k)
    {
        switch (k)
        {
            case PlanKind.Invasion: return ">>  Invasão";
            case PlanKind.Rally:    return "+  Rally";
            case PlanKind.Defend:   return "!  Defesa";
            default:                return "•  Captura";
        }
    }

    private static readonly PlanKind[] KindOrder =
        { PlanKind.Invasion, PlanKind.Rally, PlanKind.Defend, PlanKind.Capture };

    // ----------------------------------------------------------------------------
    // Visão por eixo: progresso e classificação relativa (estética por enquanto).
    // ----------------------------------------------------------------------------
    private enum SideClass { Strong, Balanced, Weak }

    // Peso composto de um eixo: mistura o SCORE (critério próprio da AI para
    // importância do eixo), o PROGRESSO (o quanto a guerra avançou nele) e a
    // PROFUNDIDADE (investimento acumulado). Só serve para ranquear os eixos
    // entre si — a escala absoluta não importa, só a comparação com a média.
    private static float AxisCompositeWeight(AIShoppingPlanner.AxisTransportPressureInspection axis)
    {
        return axis.Score + (axis.Progress * 50f) + (axis.Depth * 5f);
    }

    private static string SideLabel(SideClass side)
    {
        switch (side)
        {
            case SideClass.Strong: return "strong side";
            case SideClass.Weak: return "weak side";
            default: return "balanced side";
        }
    }

    private static Color SideColor(SideClass side)
    {
        switch (side)
        {
            case SideClass.Strong: return new Color(0.45f, 0.85f, 0.45f);
            case SideClass.Weak: return new Color(0.85f, 0.5f, 0.45f);
            default: return new Color(0.80f, 0.78f, 0.45f);
        }
    }

    private void DrawAxisOverview(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        _showAxisOverview = EditorGUILayout.Foldout(
            _showAxisOverview, "Eixos  (progresso e classificação)", true);
        if (!_showAxisOverview)
            return;

        AIShoppingPlanner.OperationalPressureInspection pressure =
            AIShoppingPlanner.InspectOperationalPressure(snapshot);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (pressure.Axes.Count == 0)
        {
            EditorGUILayout.LabelField("— sem eixos ativos —", _subtle);
            EditorGUILayout.EndVertical();
            return;
        }

        // Turno 0 e 1: a AI ainda não decidiu onde focar, tudo começa equilibrado.
        bool earlyGame = snapshot.TurnNumber <= 1;

        float sum = 0f;
        for (int i = 0; i < pressure.Axes.Count; i++)
            sum += AxisCompositeWeight(pressure.Axes[i]);
        float mean = pressure.Axes.Count > 0 ? sum / pressure.Axes.Count : 0f;
        // Sem sinal ainda (tudo zerado no começo) → não há lado forte/fraco.
        bool undecided = earlyGame || mean < 0.01f;

        EditorGUILayout.LabelField(
            undecided
                ? "guerra ainda indefinida — todos os eixos equilibrados"
                : $"média composta={mean:F1}  ·  forte ≥ {mean * 1.15f:F1}  ·  fraco ≤ {mean * 0.85f:F1}",
            _subtle);

        var lineStyle = new GUIStyle(_subtle) { richText = false };

        foreach (AIShoppingPlanner.AxisTransportPressureInspection axis in pressure.Axes)
        {
            float weight = AxisCompositeWeight(axis);
            SideClass side;
            if (undecided)
                side = SideClass.Balanced;
            else if (weight >= mean * 1.15f)
                side = SideClass.Strong;
            else if (weight <= mean * 0.85f)
                side = SideClass.Weak;
            else
                side = SideClass.Balanced;

            EditorGUILayout.BeginHorizontal();

            Rect bar = GUILayoutUtility.GetRect(
                60f, 16f, GUILayout.Width(120f));
            string axisLabel = axis.IsInvasionAxis ? $"eixo {axis.Eixo} >>" : $"eixo {axis.Eixo}";
            EditorGUI.ProgressBar(bar, Mathf.Clamp01(axis.Progress), $"{axisLabel}  {axis.Progress:P0}");

            Color prev = lineStyle.normal.textColor;
            lineStyle.normal.textColor = SideColor(side);
            EditorGUILayout.LabelField(
                $"[{SideLabel(side)}]  score={axis.Score:F1}  prof={axis.Depth:F1}  peso={weight:F1}",
                lineStyle);
            lineStyle.normal.textColor = prev;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawObjectivesColumn(TeamObjectivePlan plan)
    {
        EditorGUILayout.LabelField("Objetivos  (preenchido / demandado)", _objTitle);

        if (plan == null || plan.Objectives.Count == 0)
        {
            EditorGUILayout.LabelField("— sem plano —", _subtle);
        }
        else
        {
            var sorted = new List<SectorObjective>(plan.Objectives);
            sorted.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            // Agrupa por tipo de plano (ordem fixa), com cabeçalho por seção.
            foreach (PlanKind kind in KindOrder)
            {
                var inKind = sorted.FindAll(o => o != null && ClassifyObjective(o) == kind);
                if (inKind.Count == 0) continue;
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField($"{KindLabel(kind)}  ({inKind.Count})", _objTitle);
                foreach (SectorObjective obj in inKind)
                    DrawObjectiveCard(obj, KindSymbol(kind));
            }

            if (plan.RogueUnitIds != null && plan.RogueUnitIds.Count > 0)
                EditorGUILayout.LabelField($"rogues: {plan.RogueUnitIds.Count}", _subtle);
        }

        DrawAnchorSection();
    }

    // Seção "# Base guard / âncora": vem do AIController (EnumerateOwnAnchors), não do plano de
    // objetivos — por isso não aparece nas seções acima. Lista cada âncora, se está segura, e os
    // guardas (unidades amigas em cima/adjacentes ao anchor, HexDistance <= 1).
    private void DrawAnchorSection()
    {
        List<AIController.AnchorInspection> anchors = AIController.GetOwnAnchorsForInspection(team);
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField($"#  Base guard / âncora  ({(anchors != null ? anchors.Count : 0)})", _objTitle);
        if (anchors == null || anchors.Count == 0)
        {
            EditorGUILayout.LabelField("— sem âncora —", _subtle);
            return;
        }

        foreach (AIController.AnchorInspection a in anchors)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Color prev = GUI.color;
            GUI.color = a.Held ? new Color(0.55f, 0.9f, 0.55f) : new Color(1f, 0.75f, 0.35f);
            EditorGUILayout.LabelField($"#  {a.Sector}  {(a.Held ? "seguro ✓" : "EXPOSTO")}", EditorStyles.boldLabel);
            GUI.color = prev;

            var guards = new List<int>();
            if (_snapshot != null && _snapshot.MyUnits != null)
            {
                foreach (UnitManager u in _snapshot.MyUnits)
                {
                    if (u == null || u.IsDead) continue;
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    if (SectorManager.HexDistance(uc, a.Cell) <= 1f) guards.Add(u.InstanceId);
                }
            }
            EditorGUILayout.LabelField(guards.Count > 0
                ? $"  guardas: {string.Join(", ", guards.ConvertAll(id => "#" + id))}"
                : "  guardas: nenhum", _subtle);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawObjectiveCard(SectorObjective obj, string symbol)
    {
        if (obj == null) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string rally = obj.ObjectiveType == AIObjectiveType.RallyAssembly
            && obj.RallyState != AIRallyAssemblyState.None
                ? $"  · {obj.RallyState}"
                : "";
        EditorGUILayout.LabelField($"{symbol} pri={obj.Priority}  {obj.Sector}  [{obj.Status}]{rally}", EditorStyles.boldLabel);

        if (obj.Status == ObjectiveStatus.Defending && !string.IsNullOrEmpty(obj.DefenseReason))
            EditorGUILayout.LabelField($"  defende: {obj.DefenseReason}", _subtle);

        if (obj.Slots == null || obj.Slots.Count == 0)
        {
            EditorGUILayout.LabelField("sem slots", _subtle);
            EditorGUILayout.EndVertical();
            return;
        }

        // Agrupa por papel preservando a ordem de primeira aparição.
        var order = new List<UnitRole>();
        var total = new Dictionary<UnitRole, int>();
        var filled = new Dictionary<UnitRole, int>();
        foreach (SlotNeed slot in obj.Slots)
        {
            if (!total.ContainsKey(slot.Role)) { total[slot.Role] = 0; filled[slot.Role] = 0; order.Add(slot.Role); }
            total[slot.Role]++;
            if (slot.Filled) filled[slot.Role]++;
        }

        // No plano de invasão (">>"), o capturador é a MASSA ilimitada (preenchimento final): exibe
        // ∞ em vez de um teto e nunca marca "completo" — sempre quer mais corpos baratos.
        bool invasionPlan = obj.ObjectiveType == AIObjectiveType.InvasionAttack;
        foreach (UnitRole role in order)
        {
            int f = filled[role];
            int t = total[role];
            bool unlimited = invasionPlan && role == UnitRole.Capturador;
            bool complete = !unlimited && f >= t;
            Color prev = GUI.color;
            GUI.color = complete ? new Color(0.55f, 0.9f, 0.55f) : new Color(1f, 0.75f, 0.35f);
            EditorGUILayout.LabelField($"  {role,-14} {f}/{(unlimited ? "∞" : t.ToString())}{(complete ? "  ✓" : "")}");
            GUI.color = prev;
        }

        EditorGUILayout.EndVertical();
    }

    // ----------------------------------------------------------------------------
    // Coluna 2: pressão no shopping (fila de demandas)
    // ----------------------------------------------------------------------------
    private void DrawPressureColumn(AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Pressão no Shopping  (fila por papel)", _demandTitle);

        if (demands == null || demands.Count == 0)
            EditorGUILayout.LabelField("— fila vazia —", _subtle);
        else
            foreach (AIShoppingDemand d in demands)
                DrawDemandCard(d);

        DrawOfferCatalog(snapshot, demands);
    }

    // ----------------------------------------------------------------------------
    // Catálogo: papéis à venda nas construções + demanda atual (mesmo zerada)
    // ----------------------------------------------------------------------------
    private void DrawOfferCatalog(AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("À venda nas construções  (por papel · demanda atual)", _demandTitle);

        if (snapshot == null || snapshot.MyBuildings == null || snapshot.MyBuildings.Count == 0)
        {
            EditorGUILayout.LabelField("— sem construções —", _subtle);
            return;
        }

        var order = new List<UnitRole>();
        var byRole = new Dictionary<UnitRole, List<UnitData>>();
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || b.OfferedUnits == null) continue;
            foreach (UnitData u in b.OfferedUnits)
            {
                if (u == null || u.roles == null || u.roles.Count == 0) continue;
                UnitRole role = u.roles[0];
                if (!byRole.TryGetValue(role, out List<UnitData> list))
                {
                    list = new List<UnitData>();
                    byRole[role] = list;
                    order.Add(role);
                }
                if (!list.Contains(u)) list.Add(u);
            }
        }

        if (order.Count == 0)
        {
            EditorGUILayout.LabelField("— construções não vendem unidades —", _subtle);
            return;
        }

        order.Sort((a, b) => ((int)a).CompareTo((int)b));
        foreach (UnitRole role in order)
            DrawOfferRoleCard(role, byRole[role], demands);
    }

    private void DrawOfferRoleCard(UnitRole role, List<UnitData> units, List<AIShoppingDemand> demands)
    {
        int demand = ComputeRoleDemand(units, demands);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        Color prev = GUI.color;
        GUI.color = demand > 0 ? new Color(0.55f, 0.9f, 0.55f) : new Color(0.62f, 0.62f, 0.62f);
        EditorGUILayout.LabelField($"{role}   demanda: {demand}", EditorStyles.boldLabel);
        GUI.color = prev;

        foreach (UnitData u in units)
        {
            string elite = u.eliteLevel > 0 ? $"  e{u.eliteLevel}" : "";
            EditorGUILayout.LabelField($"  {u.displayName}  ${u.cost}{elite}", _subtle);
        }
        EditorGUILayout.EndVertical();
    }

    private static int ComputeRoleDemand(List<UnitData> units, List<AIShoppingDemand> demands)
    {
        if (demands == null || units == null) return 0;
        int total = 0;
        foreach (AIShoppingDemand d in demands)
        {
            if (d == null || d.Count <= 0) continue;
            bool matched = false;
            foreach (UnitData u in units)
                if (AIShoppingPlanner.UnitMeetsDemandForInspection(u, d)) { matched = true; break; }
            if (matched) total += d.Count;
        }
        return total;
    }

    private void DrawDemandCard(AIShoppingDemand d)
    {
        if (d == null) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        string roleLabel;
        if (d.Role == UnitRole.None && d.RequiredWeaponCategory.HasValue)
            roleLabel = d.TargetClass.HasValue
                ? $"Counter {d.TargetClass.Value}"
                : "Counter desconhecido";
        else
            roleLabel = d.ExactRole != UnitRole.None
                ? $"{d.Role}/{d.ExactRole}"
                : d.Role.ToString();
        string elite = (d.MinEliteLevel > 0 || d.MaxEliteLevel != int.MaxValue)
            ? $"  elite={d.MinEliteLevel}-{(d.MaxEliteLevel == int.MaxValue ? "∞" : d.MaxEliteLevel.ToString())}"
            : "";
        string domain = d.Domain.HasValue ? $"  [{d.Domain.Value}]" : "";
        string weapon = d.RequiredWeaponCategory.HasValue
            ? $"  arma={d.RequiredWeaponCategory.Value}"
            : "";
        string rallyArt = d.MinRallyArtilleryWeight > 0f
            ? $"  rallyArt>={d.MinRallyArtilleryWeight:0.#}"
            : "";

        Color prev = GUI.color;
        if (d.Urgent) GUI.color = new Color(1f, 0.55f, 0.55f);
        EditorGUILayout.LabelField(
            $"pri={d.Priority}  {(d.Urgent ? "‼ " : "")}{roleLabel} x{d.Count}{domain}{elite}{weapon}{rallyArt}",
            EditorStyles.boldLabel);
        GUI.color = prev;

        if (!string.IsNullOrEmpty(d.Origin))
            EditorGUILayout.LabelField($"origem: {d.Origin}", _subtle);
        if (d.StrategicEscalation)
            EditorGUILayout.LabelField(
                "tipo: ESCALADA ELITE SOLICITADA (ainda não é o compromisso persistente)",
                _subtle);
        else if (d.Origin != null && d.Origin.Contains("elite-commitment"))
            EditorGUILayout.LabelField("tipo: COMPROMISSO PERSISTENTE ATIVO", _subtle);
        if (d.RequireRallyBreakthrough)
            EditorGUILayout.LabelField("filtro: ruptura blindada de rally", _subtle);
        if (!string.IsNullOrEmpty(d.Reason))
            EditorGUILayout.LabelField($"motivo: {d.Reason}", _subtle);

        EditorGUILayout.EndVertical();
    }
}
