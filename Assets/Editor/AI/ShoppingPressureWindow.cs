using System.Collections.Generic;
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
    private bool _stylesReady;
    private bool _showCounterPressure = true;
    private bool _showOperationalPressure = true;

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

        DrawHeader(_snapshot, plan, _demands);
        DrawCounterPressure(_snapshot, _demands);
        DrawOperationalPressure(_snapshot);

        EditorGUILayout.BeginHorizontal();
        DrawObjectivesColumn(plan);
        DrawPressureColumn(_snapshot, _demands);
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

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"[{team}] T{snapshot.TurnNumber}  •  stance={snapshot.Stance}  •  budget={snapshot.Budget}  •  renda={snapshot.IncomePerTurn}",
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
        EditorGUILayout.LabelField(
            $"força: {macro.OwnForce} suas / {macro.EnemyForce} inimigas conhecidas  ({macro.ForceRatio:P0})",
            _subtle);

        EditorGUILayout.EndVertical();
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
            $"anti-infantaria {pressure.AntiInfantry:F1}  ·  anti-tank {pressure.AntiTank:F1}  ·  " +
            $"anti-aérea {pressure.AntiAir:F1}  ·  anti-navio {pressure.AntiShip:F1}",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"maior pressão: {pressure.DominantCategory}  ·  score pondera quantidade, elite, custo e HP atual",
            _subtle);
        EditorGUILayout.LabelField(
            $"fontes: {pressure.VisibleUnits} visíveis + {pressure.RememberedUnits} lembradas pelo ledger",
            _subtle);
        EditorGUILayout.LabelField(
            $"ledger subjetivo: sensor={pressure.SensorContacts} combate={pressure.CombatContacts} " +
            $"ameaças anônimas={pressure.AnonymousThreatSignals} (compras ocultas não entram)",
            _subtle);

        foreach (AIShoppingPlanner.EnemyClassPressureInspection entry in pressure.Classes)
            EditorGUILayout.LabelField(
                $"  {entry.UnitClass,-11} x{entry.Count} (vis {entry.VisibleCount} + mem {entry.RememberedCount})  " +
                $"score={entry.Score:F1} [{entry.VisibleScore:F1}+{entry.RememberedScore:F1}]  → {entry.CounterCategory}",
                _subtle);

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
            EditorGUILayout.LabelField("melhores counters disponíveis:", _subtle);
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

            var eligibleOffers = new List<UnitData>();
            foreach (UnitData unit in offered)
                if (AIShoppingPlanner.InspectPurchaseEligibility(
                    snapshot, unit, demands, out _))
                    eligibleOffers.Add(unit);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("elegíveis para a fila agora:", _subtle);
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
        EditorGUILayout.EndVertical();
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
            EditorGUILayout.LabelField(
                $"  eixo {axis.Eixo}: frente={axis.Front} rally={axis.Rally} " +
                $"avanço={axis.Advance:F1}/{axis.Total} ({axis.Progress:P0}) " +
                $"prof={axis.Depth:F1} trans={axis.AssignedTransports}/{axis.DesiredTransports} " +
                $"score={axis.Score:F1}",
                _subtle);

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

    private void DrawObjectivesColumn(TeamObjectivePlan plan)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f - 8f));
        EditorGUILayout.LabelField("Objetivos  (preenchido / demandado)", _objTitle);
        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

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

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
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

        foreach (UnitRole role in order)
        {
            int f = filled[role];
            int t = total[role];
            bool complete = f >= t;
            Color prev = GUI.color;
            GUI.color = complete ? new Color(0.55f, 0.9f, 0.55f) : new Color(1f, 0.75f, 0.35f);
            EditorGUILayout.LabelField($"  {role,-14} {f}/{t}{(complete ? "  ✓" : "")}");
            GUI.color = prev;
        }

        EditorGUILayout.EndVertical();
    }

    // ----------------------------------------------------------------------------
    // Coluna 2: pressão no shopping (fila de demandas)
    // ----------------------------------------------------------------------------
    private void DrawPressureColumn(AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f - 8f));
        EditorGUILayout.LabelField("Pressão no Shopping  (fila por papel)", _demandTitle);
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

        if (demands == null || demands.Count == 0)
            EditorGUILayout.LabelField("— fila vazia —", _subtle);
        else
            foreach (AIShoppingDemand d in demands)
                DrawDemandCard(d);

        DrawOfferCatalog(snapshot, demands);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
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

        string roleLabel = d.ExactRole != UnitRole.None ? $"{d.Role}/{d.ExactRole}" : d.Role.ToString();
        string elite = (d.MinEliteLevel > 0 || d.MaxEliteLevel != int.MaxValue)
            ? $"  elite={d.MinEliteLevel}-{(d.MaxEliteLevel == int.MaxValue ? "∞" : d.MaxEliteLevel.ToString())}"
            : "";
        string domain = d.Domain.HasValue ? $"  [{d.Domain.Value}]" : "";
        string weapon = d.RequiredWeaponCategory.HasValue
            ? $"  arma={d.RequiredWeaponCategory.Value}"
            : "";

        Color prev = GUI.color;
        if (d.Urgent) GUI.color = new Color(1f, 0.55f, 0.55f);
        EditorGUILayout.LabelField(
            $"pri={d.Priority}  {(d.Urgent ? "‼ " : "")}{roleLabel} x{d.Count}{domain}{elite}{weapon}",
            EditorStyles.boldLabel);
        GUI.color = prev;

        if (!string.IsNullOrEmpty(d.Origin))
            EditorGUILayout.LabelField($"origem: {d.Origin}", _subtle);
        if (!string.IsNullOrEmpty(d.Reason))
            EditorGUILayout.LabelField($"motivo: {d.Reason}", _subtle);

        EditorGUILayout.EndVertical();
    }
}
