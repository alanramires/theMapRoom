using System.Globalization;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Utils > Estatísticas
///
/// Janela de inspeção interna para validar os bastidores de estatísticas da partida.
/// Lê MatchStatsManager; não é painel de jogador ainda.
/// </summary>
public sealed class MatchStatsWindow : EditorWindow
{
    [SerializeField] private bool followActiveTeam = true;
    [SerializeField] private TeamId selectedTeam = TeamId.Green;
    [SerializeField] private bool showUnitBreakdown = true;
    [SerializeField] private bool showEmptyUnitRows;

    private MatchController matchController;
    private Vector2 scroll;
    private GUIStyle headerStyle;
    private GUIStyle sectionStyle;
    private GUIStyle subtleStyle;
    private GUIStyle rightMiniStyle;
    private bool stylesReady;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [MenuItem("Tools/Utils/Estatísticas")]
    public static void OpenWindow()
    {
        MatchStatsWindow window = GetWindow<MatchStatsWindow>("Estatísticas");
        window.minSize = new Vector2(620f, 360f);
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void AutoDetectContext()
    {
        if (matchController == null)
            matchController = Object.FindAnyObjectByType<MatchController>();
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        subtleStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        subtleStyle.normal.textColor = new Color(0.62f, 0.62f, 0.62f);
        rightMiniStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        stylesReady = true;
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (matchController == null)
            AutoDetectContext();

        DrawToolbar();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Entre em Play mode para inspecionar estatísticas vivas da partida.", MessageType.Info);
            return;
        }

        MatchStatsManager manager = MatchStatsManager.EnsureInstance();
        if (manager == null)
        {
            EditorGUILayout.HelpBox("MatchStatsManager não encontrado.", MessageType.Warning);
            return;
        }

        if (followActiveTeam && matchController != null)
            selectedTeam = matchController.ActiveTeam;

        manager.RefreshTerritoryStats();

        scroll = EditorGUILayout.BeginScrollView(
            scroll,
            alwaysShowHorizontal: true,
            alwaysShowVertical: true);
        DrawAllSlotsSummary(manager);
        EditorGUILayout.Space(6f);
        DrawSelectedSlot(manager);
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        followActiveTeam = GUILayout.Toggle(followActiveTeam, "Seguir time da vez", EditorStyles.toolbarButton, GUILayout.Width(130f));
        using (new EditorGUI.DisabledScope(followActiveTeam))
            selectedTeam = (TeamId)EditorGUILayout.EnumPopup(selectedTeam, EditorStyles.toolbarPopup, GUILayout.Width(100f));

        GUILayout.Space(8f);
        showUnitBreakdown = GUILayout.Toggle(showUnitBreakdown, "Unidades", EditorStyles.toolbarButton, GUILayout.Width(80f));
        showEmptyUnitRows = GUILayout.Toggle(showEmptyUnitRows, "Linhas vazias", EditorStyles.toolbarButton, GUILayout.Width(90f));
        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                MatchStatsManager.EnsureInstance()?.RefreshTerritoryStats();
                Repaint();
            }

            if (GUILayout.Button("Rebuild log", EditorStyles.toolbarButton, GUILayout.Width(85f)))
            {
                MatchStatsManager.EnsureInstance()?.RebuildFromJogadas();
                Repaint();
            }

        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawAllSlotsSummary(MatchStatsManager manager)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string turnLabel = matchController != null
            ? $"Turno {matchController.CurrentTurn}  |  ativo: {TeamUtils.GetName(matchController.ActiveTeam)} ({(int)matchController.ActiveTeam})"
            : "Turno ?";
        EditorGUILayout.LabelField($"Resumo da partida — {turnLabel}  |  Stats v2 logística", headerStyle);
        EditorGUILayout.LabelField(
            $"Jogadas carregadas: {manager.LastJogadasCount}  |  compras: {manager.LastPurchaseJogadasCount}  |  ataques: {manager.LastAttackJogadasCount}  |  capturas: {manager.LastCaptureJogadasCount}",
            subtleStyle);
        if (manager.LastJogadasCount == 0)
            EditorGUILayout.HelpBox("Histórico de jogadas vazio. Compras/kills/dano não podem ser reconstruídos desse save.", MessageType.Warning);
        else if (manager.LastPurchaseJogadasCount == 0)
            EditorGUILayout.HelpBox("Histórico carregado sem jogadas de Compra. Se este save veio de versão antiga, compras históricas não serão recuperadas.", MessageType.Info);

        DrawSummaryHeader();
        IReadOnlyListSafe(manager, out int count);
        for (int i = 0; i < count; i++)
        {
            SlotMatchStats stats = manager.SlotStats[i];
            if (stats == null || stats.team == TeamId.Neutral)
                continue;

            DrawSummaryRow(stats);
        }

        EditorGUILayout.EndVertical();
    }

    private static void IReadOnlyListSafe(MatchStatsManager manager, out int count)
    {
        count = manager != null && manager.SlotStats != null ? manager.SlotStats.Count : 0;
    }

    private void DrawSummaryHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Slot", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
        GUILayout.Label("Unid", rightMiniStyle, GUILayout.Width(45f));
        GUILayout.Label("Compras", rightMiniStyle, GUILayout.Width(60f));
        GUILayout.Label("Perdas", rightMiniStyle, GUILayout.Width(55f));
        GUILayout.Label("Kills", rightMiniStyle, GUILayout.Width(55f));
        GUILayout.Label("Dano", rightMiniStyle, GUILayout.Width(55f));
        GUILayout.Label("Território", rightMiniStyle, GUILayout.Width(75f));
        GUILayout.Label("Prédios", rightMiniStyle, GUILayout.Width(60f));
        GUILayout.Label("Setores", rightMiniStyle, GUILayout.Width(60f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSummaryRow(SlotMatchStats stats)
    {
        bool hiddenByFog = ShouldHideTeamByFog(stats.team);
        Color prev = GUI.color;
        GUI.color = TeamUtils.GetColor(stats.team);
        using (new EditorGUI.DisabledScope(hiddenByFog))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{TeamUtils.GetName(stats.team)} ({(int)stats.team})", EditorStyles.label, GUILayout.Width(90f));
            GUI.color = prev;
            if (hiddenByFog)
            {
                GUILayout.Label("dados ocultos pelo FOW", subtleStyle);
            }
            else
            {
                GUILayout.Label(stats.currentUnits.ToString(), rightMiniStyle, GUILayout.Width(45f));
                GUILayout.Label(stats.totalPurchases.ToString(), rightMiniStyle, GUILayout.Width(60f));
                GUILayout.Label(stats.totalLosses.ToString(), rightMiniStyle, GUILayout.Width(55f));
                GUILayout.Label(stats.totalKills.ToString(), rightMiniStyle, GUILayout.Width(55f));
                GUILayout.Label(stats.damageCaused.ToString(), rightMiniStyle, GUILayout.Width(55f));
                GUILayout.Label($"{stats.territoryControlPercent:0.0}%", rightMiniStyle, GUILayout.Width(75f));
                GUILayout.Label(stats.controlledConstructions.ToString(), rightMiniStyle, GUILayout.Width(60f));
                GUILayout.Label(stats.controlledSectors.ToString(), rightMiniStyle, GUILayout.Width(60f));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        GUI.color = prev;
    }

    private void DrawSelectedSlot(MatchStatsManager manager)
    {
        if (ShouldHideTeamByFog(selectedTeam))
        {
            EditorGUILayout.HelpBox("Estatísticas inimigas desativadas enquanto o Fog of War estiver ligado.", MessageType.Info);
            return;
        }

        if (!manager.TryGetStats(selectedTeam, out SlotMatchStats stats) || stats == null)
        {
            EditorGUILayout.HelpBox($"Sem estatísticas para {TeamUtils.GetName(selectedTeam)} ainda.", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        Color prev = GUI.color;
        GUI.color = TeamUtils.GetColor(stats.team);
        EditorGUILayout.LabelField($"{TeamUtils.GetName(stats.team)} ({(int)stats.team})", headerStyle);
        GUI.color = prev;

        DrawEconomyAndCombat(stats);
        DrawTerritory(stats);
        DrawConstructions(stats.team);
        DrawOperation(stats);
        DrawLogistics(stats);

        if (showUnitBreakdown)
            DrawUnitBreakdown(stats);

        EditorGUILayout.EndVertical();
    }

    private void DrawConstructions(TeamId observerTeam)
    {
        ConstructionCounts controlled = default;
        ConstructionCounts neutral = default;
        ConstructionCounts enemy = default;

        IReadOnlyList<ConstructionManager> constructions = ConstructionManager.AllActive;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || construction.IsFakeBuilding)
                continue;

            ConstructionCounts counts = ClassifyConstruction(construction);
            if (construction.TeamId == observerTeam)
                controlled.Add(counts);
            else if (construction.TeamId == TeamId.Neutral)
                neutral.Add(counts);
            else
                enemy.Add(counts);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Construções", sectionStyle);
        DrawConstructionRow("controladas", controlled, hidden: false);
        DrawConstructionRow("neutras", neutral, hidden: false);
        DrawConstructionRow("inimigas", enemy, ShouldHideEnemyConstructionsByFog());
    }

    private void DrawConstructionRow(string label, ConstructionCounts counts, bool hidden)
    {
        using (new EditorGUI.DisabledScope(hidden))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.miniBoldLabel, GUILayout.Width(90f));
            if (hidden)
                GUILayout.Label("dados ocultos pelo FOW", subtleStyle);
            else
                GUILayout.Label($"{counts.hq} HQ, {counts.factories} fábricas, {counts.airports} aeroportos, {counts.ports} portos", EditorStyles.label);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    private static ConstructionCounts ClassifyConstruction(ConstructionManager construction)
    {
        ConstructionCounts result = default;
        if (construction.IsPlayerHeadQuarter)
            result.hq = 1;

        if (!construction.TryResolveConstructionData(out ConstructionData data) || data == null)
            return result;

        if (data.isAirport)
            result.airports = 1;
        else if (data.isHarbor)
            result.ports = 1;
        else if (string.Equals(data.sufixo, "Factory", System.StringComparison.OrdinalIgnoreCase))
            result.factories = 1;

        return result;
    }

    private bool ShouldHideTeamByFog(TeamId team)
    {
        return team != TeamId.Neutral
            && matchController != null
            && matchController.IsFogOfWarDebugEnabled
            && team != matchController.ActiveTeam;
    }

    private bool ShouldHideEnemyConstructionsByFog()
    {
        return matchController != null && matchController.IsFogOfWarDebugEnabled;
    }

    private struct ConstructionCounts
    {
        public int hq;
        public int factories;
        public int airports;
        public int ports;

        public void Add(ConstructionCounts other)
        {
            hq += other.hq;
            factories += other.factories;
            airports += other.airports;
            ports += other.ports;
        }
    }

    private void DrawEconomyAndCombat(SlotMatchStats stats)
    {
        EditorGUILayout.LabelField("Economia e combate", sectionStyle);
        EditorGUILayout.BeginHorizontal();
        DrawMetricBox("compras", stats.totalPurchases.ToString());
        DrawMetricBox("gasto", Money(stats.totalSpent));
        DrawMetricBox("unidades atuais", stats.currentUnits.ToString());
        DrawMetricBox("kills", stats.totalKills.ToString());
        DrawMetricBox("perdas", stats.totalLosses.ToString());
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawMetricBox("$ inimigo destruído", Money(stats.totalDestroyedValue));
        DrawMetricBox("$ próprio perdido", Money(stats.totalLostValue));
        DrawMetricBox("dano causado", stats.damageCaused.ToString());
        DrawMetricBox("dano recebido", stats.damageReceived.ToString());
        DrawMetricBox("elite K/L", $"{stats.eliteKills}/{stats.eliteLosses}");
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTerritory(SlotMatchStats stats)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Território", sectionStyle);
        EditorGUILayout.BeginHorizontal();
        DrawMetricBox("controle", $"{stats.territoryControlPercent:0.0}%");
        DrawMetricBox("capture points", $"{stats.controlledCapturePoints}/{stats.totalCapturePointsMax}");
        DrawMetricBox("prédios", stats.controlledConstructions.ToString());
        DrawMetricBox("setores", stats.controlledSectors.ToString());
        DrawMetricBox("bases", stats.controlledBases.ToString());
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawMetricBox("full setores", stats.fullyControlledSectors.ToString());
        DrawMetricBox("disputados", stats.disputedSectors.ToString());
        DrawMetricBox("cap sob ataque", stats.contestedOwnedCapturePoints.ToString());
        DrawMetricBox("cap atacando", stats.contestingCapturePoints.ToString());
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawOperation(SlotMatchStats stats)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Operação", sectionStyle);
        EditorGUILayout.BeginHorizontal();
        DrawMetricBox("ações captura", stats.captureActions.ToString());
        DrawMetricBox("capturas", stats.capturesCompleted.ToString());
        DrawMetricBox("reconquistas", stats.recaptureActions.ToString());
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawMetricBox("embarques", stats.embarkActions.ToString());
        DrawMetricBox("desembarques", stats.disembarkActions.ToString());
        DrawMetricBox("fusões", stats.mergeActions.ToString());
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLogistics(SlotMatchStats stats)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Logística", sectionStyle);
        EditorGUILayout.BeginHorizontal();
        DrawMetricBox("$ comando", Money(stats.commandServiceCost));
        DrawMetricBox("$ logística", Money(stats.fieldLogisticsCost));
        DrawMetricBox("$ manutenção", Money(stats.totalMaintenanceCost));
        DrawMetricBox("serviços", stats.serviceApplications.ToString());
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawMetricBox("$ reparo", Money(stats.repairServiceCost));
        DrawMetricBox("$ reabastecer", Money(stats.refuelServiceCost));
        DrawMetricBox("$ rearmar", Money(stats.rearmServiceCost));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawMetricBox("HP restaurado", stats.serviceHpRecovered.ToString());
        DrawMetricBox("AUT restaurada", stats.serviceFuelRecovered.ToString());
        DrawMetricBox("MUN restaurada", stats.serviceAmmoRecovered.ToString());
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawUnitBreakdown(SlotMatchStats stats)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Unidades por tipo", sectionStyle);

        DrawUnitHeader();
        if (stats.units == null || stats.units.Count == 0)
        {
            EditorGUILayout.LabelField("Sem unidades registradas.", subtleStyle);
            return;
        }

        for (int i = 0; i < stats.units.Count; i++)
        {
            UnitTypeMatchStats unit = stats.units[i];
            if (unit == null)
                continue;

            if (!showEmptyUnitRows
                && unit.purchased == 0
                && unit.current == 0
                && unit.lost == 0
                && unit.destroyedEnemy == 0)
            {
                continue;
            }

            DrawUnitRow(unit);
        }
    }

    private void DrawUnitHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Tipo", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        GUILayout.Label("Nome", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
        GUILayout.Label("Classe", EditorStyles.miniBoldLabel, GUILayout.Width(85f));
        GUILayout.Label("Atual", rightMiniStyle, GUILayout.Width(45f));
        GUILayout.Label("Compr", rightMiniStyle, GUILayout.Width(45f));
        GUILayout.Label("Perd", rightMiniStyle, GUILayout.Width(45f));
        GUILayout.Label("Destr", rightMiniStyle, GUILayout.Width(45f));
        GUILayout.Label("$ gasto", rightMiniStyle, GUILayout.Width(70f));
        GUILayout.Label("$ perd", rightMiniStyle, GUILayout.Width(70f));
        GUILayout.Label("$ destr", rightMiniStyle, GUILayout.Width(70f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawUnitRow(UnitTypeMatchStats unit)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(unit.sigla ?? unit.key ?? "?", GUILayout.Width(70f));
        GUILayout.Label(string.IsNullOrWhiteSpace(unit.displayName) ? "-" : unit.displayName, GUILayout.Width(150f));
        GUILayout.Label(unit.unitClass.ToString(), GUILayout.Width(85f));
        GUILayout.Label(unit.current.ToString(), rightMiniStyle, GUILayout.Width(45f));
        GUILayout.Label(unit.purchased.ToString(), rightMiniStyle, GUILayout.Width(45f));
        GUILayout.Label(unit.lost.ToString(), rightMiniStyle, GUILayout.Width(45f));
        GUILayout.Label(unit.destroyedEnemy.ToString(), rightMiniStyle, GUILayout.Width(45f));
        GUILayout.Label(Money(unit.valuePurchased), rightMiniStyle, GUILayout.Width(70f));
        GUILayout.Label(Money(unit.valueLost), rightMiniStyle, GUILayout.Width(70f));
        GUILayout.Label(Money(unit.valueDestroyed), rightMiniStyle, GUILayout.Width(70f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawMetricBox(string label, string value)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(118f));
        EditorGUILayout.LabelField(label, subtleStyle);
        EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();
    }

    private static string Money(int value)
    {
        return "$" + Mathf.Max(0, value).ToString("N0", PtBr);
    }

}
