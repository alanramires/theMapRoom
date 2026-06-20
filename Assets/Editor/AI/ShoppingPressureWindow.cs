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
        EditorGUILayout.EndVertical();
    }

    // ----------------------------------------------------------------------------
    // Coluna 1: objetivos e slots demandados vs preenchidos
    // ----------------------------------------------------------------------------
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
            foreach (SectorObjective obj in sorted)
                DrawObjectiveCard(obj);

            if (plan.RogueUnitIds != null && plan.RogueUnitIds.Count > 0)
                EditorGUILayout.LabelField($"rogues: {plan.RogueUnitIds.Count}", _subtle);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawObjectiveCard(SectorObjective obj)
    {
        if (obj == null) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"pri={obj.Priority}  {obj.Sector}  [{obj.Status}]", EditorStyles.boldLabel);

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

        Color prev = GUI.color;
        if (d.Urgent) GUI.color = new Color(1f, 0.55f, 0.55f);
        EditorGUILayout.LabelField($"pri={d.Priority}  {(d.Urgent ? "‼ " : "")}{roleLabel} x{d.Count}{domain}{elite}", EditorStyles.boldLabel);
        GUI.color = prev;

        if (!string.IsNullOrEmpty(d.Origin))
            EditorGUILayout.LabelField($"origem: {d.Origin}", _subtle);
        if (!string.IsNullOrEmpty(d.Reason))
            EditorGUILayout.LabelField($"motivo: {d.Reason}", _subtle);

        EditorGUILayout.EndVertical();
    }
}
